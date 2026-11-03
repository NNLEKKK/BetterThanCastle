using Btc.Shared;

namespace Btc.RL;

/// <summary>
/// Batched evaluation of frozen-policy opponents (side 1) for a set of envs of one rollout worker: the envs that play the same
/// policy snapshot are forwarded as ONE batch through the blocked AVX kernels instead of one small forward per env.
/// RNG order differs from the per-env path (draws happen per policy group, in env order inside a group); results stay deterministic.
/// </summary>
public sealed class OppBatcher
{
    readonly int _m, _o, _l, _h;
    readonly float[] _obs, _cen, _u;
    readonly byte[] _mask, _maskRow;
    readonly bool[] _active;
    readonly int[] _act;
    readonly int[] _rowEnv;                 // batch row j -> slot index i
    readonly bool[] _done;
    readonly Dictionary<MlpPolicy, (Mlp net, MlpWorkspace ws)> _nets = new(ReferenceEqualityComparer.Instance);
    readonly int _cap;

    /// <param name="maxEnvs">envs of the worker that can be batched in one call</param>
    public OppBatcher(IEnvBatch env, int maxEnvs)
    {
        _m = env.MaxAgents; _o = env.ObsSize; _h = env.HeadSizes.Length; _l = 0;
        foreach (var h in env.HeadSizes) _l += h;
        _cap = maxEnvs * _m;
        _obs = new float[_cap * _o]; _cen = new float[Math.Max(1, _cap * env.CentralSize)];
        _mask = new byte[_cap * _l]; _maskRow = new byte[_l]; _active = new bool[_cap];
        _act = new int[_h]; _u = new float[_h]; _rowEnv = new int[_cap]; _done = new bool[maxEnvs];
    }

    (Mlp, MlpWorkspace) Net(MlpPolicy pol)
    {
        if (_nets.TryGetValue(pol, out var v)) return v;
        if (_nets.Count >= 16) _nets.Clear();
        var net = Mlp.FromPolicy(pol);
        v = (net, new MlpWorkspace(net, _cap));
        _nets[pol] = v;
        return v;
    }

    /// <summary>
    /// Fills actionsOut[(slot)*M*H ..] for each of the n envs (slot i = envs[i], policy pols[i], sampled or argmax). Needs n &lt;= maxEnvs.
    /// </summary>
    public void Run(IEnvBatch env, int n, int[] envs, MlpPolicy[] pols, bool[] sample, Rng rng, int[] actionsOut)
    {
        int M = _m, O = _o, L = _l, H = _h, C = env.CentralSize;
        for (int i = 0; i < n; i++)
            env.Observe(envs[i], 1, _obs, i * M * O, _mask, i * M * L, _cen, i * M * C, _active, i * M);
        Array.Clear(_done, 0, n);
        for (int i = 0; i < n; i++)
        {
            if (_done[i]) continue;
            var pol = pols[i];
            var (net, ws) = Net(pol);
            int rows = 0;
            for (int k = i; k < n; k++)
            {
                if (_done[k] || !ReferenceEquals(pols[k], pol)) continue;
                _done[k] = true;
                for (int a = 0; a < M; a++)
                    if (_active[k * M + a]) { Array.Copy(_obs, (k * M + a) * O, ws.Input, rows * O, O); _rowEnv[rows++] = k * M + a; }
            }
            if (rows > 0) MlpOps.Forward(net, ws, rows);
            int OS = pol.OutputSize;
            var outRow = _outBuf(OS);
            int j = 0;
            for (int k = i; k < n; k++)
            {
                if (!ReferenceEquals(pols[k], pol)) continue;
                int ao = k * M * H;
                for (int a = 0; a < M; a++)
                {
                    int slot = k * M + a;
                    if (!_active[slot]) { for (int h = 0; h < H; h++) actionsOut[ao + a * H + h] = 0; continue; }
                    Array.Copy(ws.Output, j++ * OS, outRow, 0, OS);
                    Array.Copy(_mask, slot * L, _maskRow, 0, L);
                    if (sample[k]) { for (int h = 0; h < H; h++) _u[h] = rng.NextFloat(); pol.SampleWithRng(outRow, _maskRow, _u, _act); }
                    else pol.ArgmaxPerHead(outRow, _maskRow, _act);
                    for (int h = 0; h < H; h++) actionsOut[ao + a * H + h] = _act[h];
                }
            }
        }
    }

    float[] _ob;
    float[] _outBuf(int os) => _ob != null && _ob.Length == os ? _ob : (_ob = new float[os]);
}
