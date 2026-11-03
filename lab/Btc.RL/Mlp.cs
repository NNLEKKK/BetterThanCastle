using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Btc.Shared;

namespace Btc.RL;

public enum Act : byte { None = 0, Tanh = 1, Relu = 2 }

/// <summary>
/// Trainable MLP. All parameters live in one flat array P (per layer: W [out][in] row-major, then b [out]),
/// so the optimizer and checkpoints treat it as a single vector. Same layout as .btcpol.
/// </summary>
public sealed class Mlp
{
    public readonly int[] Sizes;   // [in, h1, ..., out]
    public readonly Act[] Acts;    // per layer; last layer is None
    public readonly int[] WOff, BOff;
    public readonly int ParamCount;
    public readonly float[] P;

    public int NumLayers => Acts.Length;
    public int InSize => Sizes[0];
    public int OutSize => Sizes[^1];
    public int MaxWidth { get { int m = 0; foreach (var s in Sizes) m = Math.Max(m, s); return m; } }

    public Mlp(int inSize, int[] hidden, int outSize, Act hiddenAct)
    {
        Sizes = new int[hidden.Length + 2];
        Sizes[0] = inSize;
        for (int i = 0; i < hidden.Length; i++) Sizes[i + 1] = hidden[i];
        Sizes[^1] = outSize;
        int L = Sizes.Length - 1;
        Acts = new Act[L];
        for (int l = 0; l < L; l++) Acts[l] = l == L - 1 ? Act.None : hiddenAct;
        WOff = new int[L]; BOff = new int[L];
        int off = 0;
        for (int l = 0; l < L; l++)
        {
            WOff[l] = off; off += Sizes[l] * Sizes[l + 1];
            BOff[l] = off; off += Sizes[l + 1];
        }
        ParamCount = off;
        P = new float[off];
    }

    public Mlp CloneShape() => new Mlp(Sizes[0], Sizes[1..^1], Sizes[^1], NumLayers > 1 ? Acts[0] : Act.None);

    /// <summary>Orthogonal init (Gram-Schmidt on a Gaussian matrix), zero bias.</summary>
    public void InitOrthogonal(Rng rng, float hiddenGain, float outGain)
    {
        for (int l = 0; l < NumLayers; l++)
        {
            int nIn = Sizes[l], nOut = Sizes[l + 1];
            float gain = l == NumLayers - 1 ? outGain : hiddenGain;
            var m = Orthogonal(rng, nOut, nIn);
            for (int i = 0; i < m.Length; i++) P[WOff[l] + i] = (float)(m[i] * gain);
            Array.Clear(P, BOff[l], nOut);
        }
    }

    static double[] Orthogonal(Rng rng, int rows, int cols)
    {
        // Orthonormalize along the smaller dimension.
        bool byRows = rows <= cols;
        int nv = byRows ? rows : cols, len = byRows ? cols : rows;
        var v = new double[nv * len];
        for (int i = 0; i < v.Length; i++) v[i] = rng.Gaussian();
        for (int a = 0; a < nv; a++)
        {
            for (int b = 0; b < a; b++)
            {
                double d = 0;
                for (int k = 0; k < len; k++) d += v[a * len + k] * v[b * len + k];
                for (int k = 0; k < len; k++) v[a * len + k] -= d * v[b * len + k];
            }
            double n = 0;
            for (int k = 0; k < len; k++) n += v[a * len + k] * v[a * len + k];
            n = Math.Sqrt(Math.Max(n, 1e-12));
            for (int k = 0; k < len; k++) v[a * len + k] /= n;
        }
        var m = new double[rows * cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                m[r * cols + c] = byRows ? v[r * len + c] : v[c * len + r];
        return m;
    }

    public MlpPolicy ToPolicy(int[] heads, bool hasValue, uint schemaHash)
    {
        var p = new MlpPolicy
        {
            SchemaHash = schemaHash, InputSize = Sizes[0], HeadSizes = (int[])heads.Clone(), HasValue = hasValue,
            LayerOut = new int[NumLayers], LayerAct = new byte[NumLayers],
            Weights = new float[NumLayers][], Biases = new float[NumLayers][],
        };
        for (int l = 0; l < NumLayers; l++)
        {
            int nIn = Sizes[l], nOut = Sizes[l + 1];
            p.LayerOut[l] = nOut; p.LayerAct[l] = (byte)Acts[l];
            p.Weights[l] = P.AsSpan(WOff[l], nIn * nOut).ToArray();
            p.Biases[l] = P.AsSpan(BOff[l], nOut).ToArray();
        }
        return p;
    }

    public static Mlp FromPolicy(MlpPolicy p)
    {
        int L = p.NumLayers;
        var hidden = new int[L - 1];
        for (int i = 0; i < L - 1; i++) hidden[i] = p.LayerOut[i];
        var m = new Mlp(p.InputSize, hidden, p.OutputSize, L > 1 ? (Act)p.LayerAct[0] : Act.None);
        for (int l = 0; l < L; l++)
        {
            Array.Copy(p.Weights[l], 0, m.P, m.WOff[l], p.Weights[l].Length);
            Array.Copy(p.Biases[l], 0, m.P, m.BOff[l], p.Biases[l].Length);
        }
        return m;
    }
}

/// <summary>Per-thread buffers: activations, deltas, gradient accumulator.</summary>
public sealed class MlpWorkspace
{
    public readonly int Capacity;
    public readonly float[][] A;  // A[0] = input rows, A[l+1] = post-activation output of layer l
    public readonly float[][] D;  // D[l+1] = dLoss/d(pre-activation of layer l); D[L] filled by the caller (linear output)
    public readonly float[] Grad;
    public readonly Mlp Net;

    public MlpWorkspace(Mlp net, int capacity)
    {
        Net = net; Capacity = capacity;
        int L = net.NumLayers;
        A = new float[L + 1][];
        D = new float[L + 1][];
        for (int l = 0; l <= L; l++)
        {
            A[l] = new float[capacity * net.Sizes[l] + 8];
            D[l] = l == 0 ? Array.Empty<float>() : new float[capacity * net.Sizes[l] + 8];
        }
        Grad = new float[net.ParamCount];
    }

    /// <summary>Critic split: first layer = W_obs*obs (per row) + W_cen*central (per row GROUP). Null = plain concatenated input.</summary>
    public CenBatch Cb;
    public void EnableSplit(int obsSize, int cenSize) { Cb = new CenBatch(obsSize, cenSize, Capacity, Net.Sizes[1]); }

    public float[] Input => A[0];
    public float[] Output => A[^1];
    public float[] OutGrad => D[^1];
}

/// <summary>Batched forward/backward with Vector256 FMA kernels. Row-major activations [rows][width].</summary>
public static unsafe partial class MlpOps
{
    public static void Forward(Mlp net, MlpWorkspace ws, int rows)
    {
        if (ws.Cb != null) ForwardSplit(net, ws, rows);
        else ForwardFrom(net, ws, rows, 0);
    }

    static void ForwardFrom(Mlp net, MlpWorkspace ws, int rows, int l0)
    {
        fixed (float* p = net.P)
        {
            for (int l = l0; l < net.NumLayers; l++)
            {
                int nIn = net.Sizes[l], nOut = net.Sizes[l + 1];
                fixed (float* x = ws.A[l]) fixed (float* y = ws.A[l + 1])
                {
                    LinearForward(x, rows, nIn, p + net.WOff[l], p + net.BOff[l], nOut, y);
                    int n = rows * nOut;
                    if (net.Acts[l] == Act.Tanh) TanhInPlace(y, n);
                    else if (net.Acts[l] == Act.Relu) for (int i = 0; i < n; i++) if (y[i] < 0f) y[i] = 0f;
                }
            }
        }
    }

    /// <summary>Accumulates parameter gradients into ws.Grad. ws.OutGrad must hold dLoss/dOutput (rows x out).</summary>
    public static void Backward(Mlp net, MlpWorkspace ws, int rows)
    {
        if (ws.Cb != null) BackwardSplit(net, ws, rows);
        else BackwardTo(net, ws, rows, 0);
    }

    static void BackwardTo(Mlp net, MlpWorkspace ws, int rows, int minLayer)
    {
        fixed (float* p = net.P) fixed (float* g = ws.Grad)
        {
            for (int l = net.NumLayers - 1; l >= minLayer; l--)
            {
                int nIn = net.Sizes[l], nOut = net.Sizes[l + 1];
                fixed (float* dz = ws.D[l + 1]) fixed (float* x = ws.A[l])
                {
                    LinearBackwardWeights(dz, x, rows, nOut, nIn, g + net.WOff[l], g + net.BOff[l]);
                    if (l == 0) break;
                    fixed (float* dx = ws.D[l])
                    {
                        LinearBackwardInput(dz, rows, nOut, p + net.WOff[l], nIn, dx);
                        int n = rows * nIn;
                        Act a = net.Acts[l - 1];
                        if (a == Act.Tanh) for (int i = 0; i < n; i++) { float yv = x[i]; dx[i] *= 1f - yv * yv; }
                        else if (a == Act.Relu) for (int i = 0; i < n; i++) if (x[i] <= 0f) dx[i] = 0f;
                    }
                }
            }
        }
    }


    // ---- critic split: z1 = W_obs*obs + b + W_cen*central[group(r)]; identical math to the concatenated [obs ++ central] input ----
    static void ForwardSplit(Mlp net, MlpWorkspace ws, int rows)
    {
        var cb = ws.Cb; int O = cb.ObsSize, C = cb.CenSize, ci = net.Sizes[0], n0 = net.Sizes[1];
        fixed (float* p = net.P) fixed (float* x = ws.A[0]) fixed (float* y = ws.A[1]) fixed (float* cen = cb.Cen) fixed (float* zc = cb.Zc) fixed (float* zb = cb.Zero)
        {
            LinearForward(x, rows, O, p + net.WOff[0], p + net.BOff[0], n0, y, ci);
            LinearForward(cen, cb.G, C, p + net.WOff[0] + O, zb, n0, zc, ci);
            for (int r = 0; r < rows; r++)
            {
                float* yr = y + r * n0; float* zr = zc + cb.Grp[r] * n0;
                int k = 0;
                for (; k + 8 <= n0; k += 8) (Vector256.Load(yr + k) + Vector256.Load(zr + k)).Store(yr + k);
                for (; k < n0; k++) yr[k] += zr[k];
            }
            int n = rows * n0;
            if (net.Acts[0] == Act.Tanh) TanhInPlace(y, n);
            else if (net.Acts[0] == Act.Relu) for (int i = 0; i < n; i++) if (y[i] < 0f) y[i] = 0f;
        }
        ForwardFrom(net, ws, rows, 1);
    }

    static void BackwardSplit(Mlp net, MlpWorkspace ws, int rows)
    {
        BackwardTo(net, ws, rows, 1);
        var cb = ws.Cb; int O = cb.ObsSize, C = cb.CenSize, ci = net.Sizes[0], n0 = net.Sizes[1];
        Array.Clear(cb.Dzg, 0, cb.G * n0);
        float[] dzA = ws.D[1], dzg = cb.Dzg;
        for (int r = 0; r < rows; r++)
        {
            int ro = r * n0, go = cb.Grp[r] * n0;
            for (int k = 0; k < n0; k++) dzg[go + k] += dzA[ro + k];
        }
        fixed (float* g = ws.Grad) fixed (float* dz = ws.D[1]) fixed (float* x = ws.A[0]) fixed (float* cen = cb.Cen) fixed (float* dg = cb.Dzg) fixed (float* db = cb.Dummy)
        {
            LinearBackwardWeights(dz, x, rows, n0, O, g + net.WOff[0], g + net.BOff[0], ci);
            LinearBackwardWeights(dg, cen, cb.G, n0, C, g + net.WOff[0] + O, db, ci);
        }
    }
}

/// <summary>Per-workspace central-feature groups for the split critic: Cen[G][C] unique rows, Grp[r] = group of row r. Fill Cen/Grp/G, then Forward.</summary>
public sealed class CenBatch
{
    public readonly int ObsSize, CenSize;
    public readonly float[] Cen, Zc, Dzg, Zero, Dummy;
    public readonly int[] Grp;
    public int G;
    public CenBatch(int obsSize, int cenSize, int rowCap, int n0)
    {
        ObsSize = obsSize; CenSize = cenSize;
        Cen = new float[rowCap * cenSize + 8]; Zc = new float[rowCap * n0 + 8]; Dzg = new float[rowCap * n0 + 8];
        Zero = new float[n0 + 8]; Dummy = new float[n0 + 8]; Grp = new int[rowCap];
    }
}
