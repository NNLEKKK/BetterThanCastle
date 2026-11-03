using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using ILGPU.Runtime.OpenCL;
using Btc.RL;

namespace Btc.Gpu;

public struct GemmArgs { public int M, N, K, sAM, sAK, sBN, sBK, cOff, KSplit, KChunk; public float Beta; }

/// <summary>
/// OpenCL (ILGPU) backend of the PPO update: actor + critic forward/backward on the GPU (RX 5700: ~2 TFLOPS per GEMM vs ~0.45 on
/// the 8-core CPU). Samples of an update are uploaded once (obs per valid row, central per (step, env) group); every minibatch
/// gathers its rows on the GPU. Loss math and Adam stay on the CPU (PpoTrainer.TrainGpu).
/// </summary>
public sealed class GpuPpo : IPpoGpu, IDisposable
{
    Context _ctx;
    Accelerator _acc;
    public string Name { get; private set; }
    /// <summary>Profiling (Stopwatch ticks): uploads incl. queue drain, forward, backward, gradient download, BeginUpdate packing+upload, weight upload.</summary>
    public long TUp, TFwd, TBwd, TDown, TBegin, TW;
    public string Prof() { double f = 1.0 / System.Diagnostics.Stopwatch.Frequency; return FormattableString.Invariant($"begin {TBegin * f:F3} wup {TW * f:F3} up {TUp * f:F3} fwd {TFwd * f:F3} bwd {TBwd * f:F3} down {TDown * f:F3}"); }

    Action<KernelConfig, ArrayView<float>, ArrayView<float>, ArrayView<float>, GemmArgs> _gemm;
    Action<Index1D, ArrayView<float>, ArrayView<float>, int, int> _biasTanh, _bias;
    Action<Index1D, ArrayView<float>, ArrayView<float>> _tanhGrad;
    Action<Index1D, ArrayView<float>, ArrayView<float>, int, int> _colSum;
    Action<Index1D, ArrayView<float>, ArrayView<int>, ArrayView<float>, int> _gatherObs;
    Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<int>, ArrayView<int>, ArrayView<float>, int, int> _gatherCritic;

    sealed class Net
    {
        public Mlp Host;
        public MemoryBuffer1D<float, Stride1D.Dense> P, G;
        public MemoryBuffer1D<float, Stride1D.Dense>[] A, D;   // A[l] = layer input / output activations, D[l] = grads (l >= 1)
        public int Cap;
    }
    readonly Net _actor = new(), _critic = new();
    MemoryBuffer1D<float, Stride1D.Dense> _obsV, _cenG;
    MemoryBuffer1D<int, Stride1D.Dense> _rowGroup, _ord;
    float[] _hObs = Array.Empty<float>(), _hCen = Array.Empty<float>(); int[] _hGroup = Array.Empty<int>(), _groupOf = Array.Empty<int>();
    int _O, _C, _rows;

    public GpuPpo() { Init(); }

    public void Reset()
    {
        foreach (var n in new[] { _actor, _critic })
        {
            n.P?.Dispose(); n.G?.Dispose(); if (n.A != null) { foreach (var x in n.A) x?.Dispose(); foreach (var x in n.D) x?.Dispose(); }
            n.Host = null; n.P = n.G = null; n.A = n.D = null; n.Cap = 0;
        }
        _obsV?.Dispose(); _cenG?.Dispose(); _rowGroup?.Dispose(); _ord?.Dispose(); _obsV = _cenG = null; _rowGroup = _ord = null;
        try { _acc?.Dispose(); _ctx?.Dispose(); } catch { }
        Init();
    }

    void Init()
    {
        _ctx = Context.Create(b => b.OpenCL().EnableAlgorithms().Optimize(OptimizationLevel.O2));
        var dev = _ctx.GetPreferredDevice(preferCPU: false);
        if (dev.AcceleratorType == AcceleratorType.CPU) throw new InvalidOperationException("no OpenCL GPU found");
        _acc = dev.CreateAccelerator(_ctx);
        Name = dev.Name + " (" + (dev.MemorySize >> 20) + " MB)";
        _gemm = _acc.LoadStreamKernel<ArrayView<float>, ArrayView<float>, ArrayView<float>, GemmArgs>(K.Gemm);
        _biasTanh = _acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, int, int>(K.BiasTanh);
        _bias = _acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, int, int>(K.Bias);
        _tanhGrad = _acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(K.TanhGrad);
        _colSum = _acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, int, int>(K.ColSum);
        _gatherObs = _acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<int>, ArrayView<float>, int>(K.GatherObs);
        _gatherCritic = _acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<int>, ArrayView<int>, ArrayView<float>, int, int>(K.GatherCritic);
    }

    public void Dispose() { _acc.Dispose(); _ctx.Dispose(); }

    static void Grow<T>(Accelerator acc, ref MemoryBuffer1D<T, Stride1D.Dense> b, long n) where T : unmanaged
    {
        if (b != null && b.Length >= n) return;
        b?.Dispose();
        b = acc.Allocate1D<T>(Math.Max(1, n + n / 4));
    }

    void EnsureNet(Net net, Mlp host, int rows)
    {
        if (net.Host != host) { net.Host = host; net.P?.Dispose(); net.G?.Dispose(); net.P = _acc.Allocate1D<float>(host.ParamCount); net.G = _acc.Allocate1D<float>(host.ParamCount); net.Cap = 0; }
        if (net.Cap >= rows) return;
        int cap = rows + rows / 4, L = host.NumLayers;
        if (net.A != null) { foreach (var x in net.A) x?.Dispose(); foreach (var x in net.D) x?.Dispose(); }
        net.A = new MemoryBuffer1D<float, Stride1D.Dense>[L + 1]; net.D = new MemoryBuffer1D<float, Stride1D.Dense>[L + 1];
        for (int l = 0; l <= L; l++)
        {
            net.A[l] = _acc.Allocate1D<float>((long)cap * host.Sizes[l]);
            if (l > 0) net.D[l] = _acc.Allocate1D<float>((long)cap * host.Sizes[l]);
        }
        net.Cap = cap;
    }

    public void BeginUpdate(PpoTrainer t) { long q = System.Diagnostics.Stopwatch.GetTimestamp(); BeginUpdateCore(t); TBegin += System.Diagnostics.Stopwatch.GetTimestamp() - q; }
    void BeginUpdateCore(PpoTrainer t)
    {
        int nv = t.ValidCount, O = t.ObsSize, C = t.CentralSize, M = t.Agents;
        _O = O; _C = C;
        var valid = t.ValidOrig; var obs = t.ObsBuffer; var cen = t.CentralBuffer;
        if (_hObs.Length < (long)nv * O) _hObs = new float[(long)nv * O + O];
        if (_hGroup.Length < nv) _hGroup = new int[nv + 16];
        int groups = obs.Length / O / M;
        if (_groupOf.Length < groups) _groupOf = new int[groups];
        Array.Fill(_groupOf, -1, 0, groups);
        int ng = 0;
        for (int j = 0; j < nv; j++)
        {
            int s = valid[j];
            Array.Copy(obs, (long)s * O, _hObs, (long)j * O, O);
            int g = s / M;
            if (_groupOf[g] < 0)
            {
                _groupOf[g] = ng;
                if (C > 0)
                {
                    if (_hCen.Length < (long)(ng + 1) * C) Array.Resize(ref _hCen, Math.Max(_hCen.Length * 2, (ng + 1) * C));
                    Array.Copy(cen, (long)s * C, _hCen, (long)ng * C, C);   // central is identical for the live agents of one (step, env)
                }
                ng++;
            }
            _hGroup[j] = _groupOf[g];
        }
        Grow(_acc, ref _obsV, (long)nv * O); Grow(_acc, ref _rowGroup, nv); Grow(_acc, ref _cenG, Math.Max(1, (long)ng * C));
        _obsV.AV().SubView(0, (long)nv * O).CopyFromCPU(new ReadOnlySpan<float>(_hObs, 0, nv * O));
        _rowGroup.AV().SubView(0, nv).CopyFromCPU(new ReadOnlySpan<int>(_hGroup, 0, nv));
        if (C > 0) _cenG.AV().SubView(0, (long)ng * C).CopyFromCPU(new ReadOnlySpan<float>(_hCen, 0, ng * C));
        _acc.Synchronize();   // span copies may complete asynchronously: the host staging arrays are reused right after
        WeightsChanged(t);
    }

    public void WeightsChanged(PpoTrainer t)
    {
        EnsureNet(_actor, t.Actor, 1); EnsureNet(_critic, t.Critic, 1);
        long q = System.Diagnostics.Stopwatch.GetTimestamp(); _actor.P.CopyFromCPU(t.Actor.P); _critic.P.CopyFromCPU(t.Critic.P); _acc.Synchronize(); TW += System.Diagnostics.Stopwatch.GetTimestamp() - q;
    }

    /// <summary>C = A * B^T (see K.Gemm). zeroedC: C is already zero, so a long K may be split over blocks with atomic adds
    /// (the weight-gradient GEMMs have a small M x N and K = rows: without the split only 16-72 blocks would run on 36 CUs).</summary>
    void Gemm(ArrayView<float> A, ArrayView<float> B, ArrayView<float> Cv, int M, int N, int Kd, int sAM, int sAK, int sBN, int sBK, bool zeroedC = false)
    {
        int blocks = ((N + 63) / 64) * ((M + 63) / 64);
        int split = 1;
        if (zeroedC && blocks < 288) split = Math.Max(1, Math.Min((288 + blocks - 1) / blocks, Kd / 512));
        int chunk = ((Kd + split - 1) / split + 15) / 16 * 16;
        split = (Kd + chunk - 1) / chunk;
        var g = new GemmArgs { M = M, N = N, K = Kd, sAM = sAM, sAK = sAK, sBN = sBN, sBK = sBK, Beta = 0, KSplit = split, KChunk = chunk };
        _gemm((new Index1D(blocks * split), new Index1D(256)), A, B, Cv, g);
    }

    void Forward(Net net, int rows)
    {
        var h = net.Host;
        for (int l = 0; l < h.NumLayers; l++)
        {
            int nIn = h.Sizes[l], nOut = h.Sizes[l + 1];
            var W = net.P.AV().SubView(h.WOff[l], (long)nIn * nOut); var b = net.P.AV().SubView(h.BOff[l], nOut);
            var x = net.A[l].View; var y = net.A[l + 1].View;
            Gemm(x, W, y, rows, nOut, nIn, nIn, 1, nIn, 1);
            if (h.Acts[l] == Act.Tanh) _biasTanh(rows * nOut, y, b, nOut, rows * nOut);
            else if (h.Acts[l] == Act.None) _bias(rows * nOut, y, b, nOut, rows * nOut);
            else throw new NotSupportedException("GPU backend: tanh / linear layers only");
        }
    }

    void Backward(Net net, int rows)
    {
        var h = net.Host;
        net.G.MemSetToZero();
        for (int l = h.NumLayers - 1; l >= 0; l--)
        {
            int nIn = h.Sizes[l], nOut = h.Sizes[l + 1];
            var dz = net.D[l + 1].View; var x = net.A[l].View;
            // dW[o,i] = sum_r dz[r,o] x[r,i]
            Gemm(dz, x, net.G.AV().SubView(h.WOff[l], (long)nIn * nOut), nOut, nIn, rows, 1, nOut, 1, nIn, true);
            _colSum(nOut * 64, dz, net.G.AV().SubView(h.BOff[l], nOut), nOut, rows);
            if (l == 0) break;
            // dX[r,i] = sum_o dz[r,o] W[o,i], then * (1 - x^2) for the tanh that produced x
            var dx = net.D[l].View;
            Gemm(dz, net.P.AV().SubView(h.WOff[l], (long)nIn * nOut), dx, rows, nIn, nOut, nOut, 1, 1, nIn);
            if (h.Acts[l - 1] == Act.Tanh) _tanhGrad(rows * nIn, dx.SubView(0, (long)rows * nIn), x.SubView(0, (long)rows * nIn));
        }
    }

    public void Forward(int[] ord, int rows, float[] logits, float[] values)
    {
        _rows = rows;
        EnsureNet(_actor, _actor.Host, rows); EnsureNet(_critic, _critic.Host, rows);
        Grow(_acc, ref _ord, rows);
        _ord.AV().SubView(0, rows).CopyFromCPU(new ReadOnlySpan<int>(ord, 0, rows));
        _acc.Synchronize();
        _gatherObs(rows * _O, _obsV.View, _ord.View, _actor.A[0].View, _O);
        _gatherCritic(rows * (_O + _C), _obsV.View, _cenG.View, _rowGroup.View, _ord.View, _critic.A[0].View, _O, _C);
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp(); _acc.Synchronize(); long t1 = System.Diagnostics.Stopwatch.GetTimestamp(); TUp += t1 - t0;
        Forward(_actor, rows); Forward(_critic, rows); _acc.Synchronize(); long t2 = System.Diagnostics.Stopwatch.GetTimestamp(); TFwd += t2 - t1;
        int L = _actor.Host.OutSize;
        _actor.A[^1].AV().SubView(0, (long)rows * L).CopyToCPU(new Span<float>(logits, 0, rows * L));
        _critic.A[^1].AV().SubView(0, rows).CopyToCPU(new Span<float>(values, 0, rows));
        _acc.Synchronize();   // results are read by the CPU loss right away
    }

    public void Backward(int rows, float[] dLogits, float[] dValues, float[] gA, float[] gC)
    {
        int L = _actor.Host.OutSize;
        _actor.D[^1].AV().SubView(0, (long)rows * L).CopyFromCPU(new ReadOnlySpan<float>(dLogits, 0, rows * L));
        _critic.D[^1].AV().SubView(0, rows).CopyFromCPU(new ReadOnlySpan<float>(dValues, 0, rows));
        _acc.Synchronize();
        long b0 = System.Diagnostics.Stopwatch.GetTimestamp(); _acc.Synchronize(); long b1 = System.Diagnostics.Stopwatch.GetTimestamp(); TUp += b1 - b0;
        Backward(_actor, rows); Backward(_critic, rows); _acc.Synchronize(); long b2 = System.Diagnostics.Stopwatch.GetTimestamp(); TBwd += b2 - b1;
        _actor.G.CopyToCPU(gA); _critic.G.CopyToCPU(gC); _acc.Synchronize(); TDown += System.Diagnostics.Stopwatch.GetTimestamp() - b2;
    }

    // ---------------- kernels ----------------
    static class K
    {
        // C[cOff + m*N + n] = Beta*C + sum_k A[m*sAM + k*sAK] * B[n*sBN + k*sBK]. Block 64x64, 256 threads, 4x4 per thread, K tile 16.
        public static void Gemm(ArrayView<float> A, ArrayView<float> B, ArrayView<float> C, GemmArgs g)
        {
            var As = SharedMemory.Allocate<float>(16 * 64);
            var Bs = SharedMemory.Allocate<float>(16 * 64);
            int nbN = (g.N + 63) / 64;
            int nbM = (g.M + 63) / 64, blk = Grid.IdxX % (nbN * nbM), ks = Grid.IdxX / (nbN * nbM);
            int bm = blk / nbN, bn = blk % nbN;
            int kBeg = ks * g.KChunk, kEnd = XMath.Min(g.K, kBeg + g.KChunk);
            int m0 = bm * 64, n0 = bn * 64;
            int t = Group.IdxX, tx = t & 15, ty = t >> 4;
            float c00 = 0, c01 = 0, c02 = 0, c03 = 0, c10 = 0, c11 = 0, c12 = 0, c13 = 0;
            float c20 = 0, c21 = 0, c22 = 0, c23 = 0, c30 = 0, c31 = 0, c32 = 0, c33 = 0;
            for (int k0 = kBeg; k0 < kEnd; k0 += 16)
            {
                for (int q = 0; q < 4; q++)
                {
                    int e = t + q * 256;
                    int kk, mm;
                    if (g.sAK == 1) { kk = e & 15; mm = e >> 4; } else { mm = e & 63; kk = e >> 6; }
                    int gm = m0 + mm, gk = k0 + kk;
                    As[kk * 64 + mm] = gm < g.M && gk < kEnd ? A[(long)gm * g.sAM + (long)gk * g.sAK] : 0f;
                    int nn;
                    if (g.sBK == 1) { kk = e & 15; nn = e >> 4; } else { nn = e & 63; kk = e >> 6; }
                    int gn = n0 + nn; gk = k0 + kk;
                    Bs[kk * 64 + nn] = gn < g.N && gk < kEnd ? B[(long)gn * g.sBN + (long)gk * g.sBK] : 0f;
                }
                Group.Barrier();
                for (int kk = 0; kk < 16; kk++)
                {
                    int ra = kk * 64 + ty, rb = kk * 64 + tx;
                    float a0 = As[ra], a1 = As[ra + 16], a2 = As[ra + 32], a3 = As[ra + 48];
                    float b0 = Bs[rb], b1 = Bs[rb + 16], b2 = Bs[rb + 32], b3 = Bs[rb + 48];
                    c00 += a0 * b0; c01 += a0 * b1; c02 += a0 * b2; c03 += a0 * b3;
                    c10 += a1 * b0; c11 += a1 * b1; c12 += a1 * b2; c13 += a1 * b3;
                    c20 += a2 * b0; c21 += a2 * b1; c22 += a2 * b2; c23 += a2 * b3;
                    c30 += a3 * b0; c31 += a3 * b1; c32 += a3 * b2; c33 += a3 * b3;
                }
                Group.Barrier();
            }
            St(C, g, m0 + ty, n0 + tx, c00, c01, c02, c03);
            St(C, g, m0 + ty + 16, n0 + tx, c10, c11, c12, c13);
            St(C, g, m0 + ty + 32, n0 + tx, c20, c21, c22, c23);
            St(C, g, m0 + ty + 48, n0 + tx, c30, c31, c32, c33);
        }

        static void St(ArrayView<float> C, GemmArgs g, int m, int n, float v0, float v1, float v2, float v3)
        {
            if (m >= g.M) return;
            long b = (long)m * g.N + g.cOff;
            if (g.KSplit > 1)
            {
                if (n < g.N) Atomic.Add(ref C[b + n], v0);
                if (n + 16 < g.N) Atomic.Add(ref C[b + n + 16], v1);
                if (n + 32 < g.N) Atomic.Add(ref C[b + n + 32], v2);
                if (n + 48 < g.N) Atomic.Add(ref C[b + n + 48], v3);
                return;
            }
            if (n < g.N) C[b + n] = g.Beta * C[b + n] + v0;
            if (n + 16 < g.N) C[b + n + 16] = g.Beta * C[b + n + 16] + v1;
            if (n + 32 < g.N) C[b + n + 32] = g.Beta * C[b + n + 32] + v2;
            if (n + 48 < g.N) C[b + n + 48] = g.Beta * C[b + n + 48] + v3;
        }

        public static void BiasTanh(Index1D i, ArrayView<float> y, ArrayView<float> b, int n, int total)
        {
            if (i >= total) return;
            y[i] = XMath.Tanh(y[i] + b[i % n]);
        }

        public static void Bias(Index1D i, ArrayView<float> y, ArrayView<float> b, int n, int total)
        {
            if (i >= total) return;
            y[i] = y[i] + b[i % n];
        }

        public static void TanhGrad(Index1D i, ArrayView<float> dx, ArrayView<float> x)
        {
            float v = x[i];
            dx[i] = dx[i] * (1f - v * v);
        }

        // dB[o] += sum_r dz[r,o]: thread (o, part) sums rows part, part+64, ... and adds atomically (dB zeroed with G)
        public static void ColSum(Index1D i, ArrayView<float> dz, ArrayView<float> dB, int n, int rows)
        {
            int o = i % n, part = i / n;
            float s = 0;
            for (int r = part; r < rows; r += 64) s += dz[(long)r * n + o];
            Atomic.Add(ref dB[o], s);
        }

        public static void GatherObs(Index1D i, ArrayView<float> obsV, ArrayView<int> ord, ArrayView<float> x, int O)
        {
            int r = i / O, c = i % O;
            x[i] = obsV[(long)ord[r] * O + c];
        }

        public static void GatherCritic(Index1D i, ArrayView<float> obsV, ArrayView<float> cenG, ArrayView<int> rowGroup, ArrayView<int> ord,
            ArrayView<float> x, int O, int C)
        {
            int CI = O + C, r = i / CI, c = i % CI;
            int j = ord[r];
            x[i] = c < O ? obsV[(long)j * O + c] : cenG[(long)rowGroup[j] * C + (c - O)];
        }
    }
}

static class BufExt
{
    /// <summary>Contiguous view of a dense 1D buffer (ArrayView implements IContiguousArrayView: span copies, sub views).</summary>
    public static ArrayView<T> AV<T>(this MemoryBuffer1D<T, Stride1D.Dense> b) where T : unmanaged => b.View;
}
