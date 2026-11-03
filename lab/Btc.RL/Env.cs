using Btc.Shared;

namespace Btc.RL;

/// <summary>
/// N independent environments, each with up to MaxAgents controlled units per side.
/// Thread safety: calls for DIFFERENT env indices may run concurrently; one env is only touched by one thread.
/// Side 0 is the learner; side 1 is driven by an IOpponent (or absent for single-side envs).
/// Buffers are laid out per agent slot: obs [MaxAgents*ObsSize], mask [MaxAgents*MaskSize] (1 = allowed),
/// central [MaxAgents*CentralSize] (centralized-critic features), active [MaxAgents].
/// Actions: [MaxAgents*HeadSizes.Length].
/// </summary>
public interface IEnvBatch
{
    int NumEnvs { get; }
    int MaxAgents { get; }
    int ObsSize { get; }
    int CentralSize { get; }
    int[] HeadSizes { get; }
    bool HasOpponent { get; }
    /// <summary>Observation/action layout string; its FNV hash is stored in exported policies.</summary>
    string Schema { get; }

    void Reset(int env, ulong seed);
    void Observe(int env, int side, float[] obs, int obsOff, byte[] mask, int maskOff, float[] central, int centralOff, bool[] active, int activeOff);
    /// <summary>Advance one decision step. rewards0 gets the per-agent reward of side 0. outcome: +1 side 0 won, -1 lost, 0 draw (valid when done).</summary>
    void Step(int env, int[] actions0, int[] actions1, float[] rewards0, int rewOff, out bool done, out int outcome);
}

/// <summary>Drives side 1 of one env. One instance per worker thread (instances are never shared between threads).</summary>
public interface IOpponent
{
    void Act(IEnvBatch env, int e, int side, Rng rng, int[] actionsOut);
}

/// <summary>
/// Per-episode opponent choice (weighted mix / league). Pick runs on worker threads (each env index on one thread) and must only READ
/// shared state; Report must be thread-safe and order-independent; BeginRollout runs on the trainer thread between rollouts
/// (the place to refresh weights, so a rollout is deterministic for a fixed thread count).
/// </summary>
public interface IOpponentSampler
{
    int Pick(int env, Rng rng);
    void Report(int env, int opp, int outcome);
    void BeginRollout();
}

/// <summary>Env that can change the opponent of one env index before its next Reset.</summary>
public interface IOpponentSwitch
{
    void SetOpponent(int env, int opp);
}

public sealed class OpponentSpec
{
    public string Name;
    public Func<IOpponent> Create;
    public OpponentSpec(string name, Func<IOpponent> create) { Name = name; Create = create; }
}

/// <summary>Shared, swappable frozen policy (self-play snapshot). Readers take the reference once per decision.</summary>
public sealed class PolicyRef
{
    public volatile MlpPolicy Policy;
    public PolicyRef(MlpPolicy p) { Policy = p; }
}

/// <summary>Opponent driven by a frozen MlpPolicy (argmax or sampled) using the env's own observations for its side.</summary>
public sealed class PolicyOpponent : IOpponent
{
    readonly PolicyRef _ref;
    readonly Func<int, MlpPolicy> _perEnv;
    readonly bool _sample;
    float[] _obs, _central, _out, _scratch, _row, _u;
    byte[] _mask, _maskRow;
    bool[] _active;
    int[] _act;

    public PolicyOpponent(PolicyRef r, bool sample) { _ref = r; _sample = sample; }
    /// <summary>Policy chosen per env index (league pool: each env plays its own snapshot).</summary>
    public PolicyOpponent(Func<int, MlpPolicy> perEnv, bool sample) { _perEnv = perEnv; _sample = sample; }
    /// <summary>Snapshot this opponent plays in env e (null = idles). Used by OppBatcher.</summary>
    public MlpPolicy PolicyFor(int e) => _perEnv != null ? _perEnv(e) : _ref.Policy;
    public bool Sample => _sample;

    public void Act(IEnvBatch env, int e, int side, Rng rng, int[] actionsOut)
    {
        var pol = _perEnv != null ? _perEnv(e) : _ref.Policy;
        if (pol == null) { Array.Clear(actionsOut); return; }
        int M = env.MaxAgents, O = env.ObsSize, H = env.HeadSizes.Length, L = 0;
        foreach (var h in env.HeadSizes) L += h;
        if (_obs == null || _obs.Length != M * O || _out.Length != pol.OutputSize || _scratch.Length != pol.ScratchSize)
        {
            _obs = new float[M * O]; _central = new float[Math.Max(1, M * env.CentralSize)];
            _mask = new byte[M * L]; _maskRow = new byte[L]; _active = new bool[M];
            _out = new float[pol.OutputSize]; _scratch = new float[pol.ScratchSize]; _row = new float[O];
            _act = new int[H]; _u = new float[H];
        }
        env.Observe(e, side, _obs, 0, _mask, 0, _central, 0, _active, 0);
        // batched AVX forward of the live units (same weights as pol; tanh differs from Math.Tanh by ~1e-7)
        var (net, ws) = Net(pol, M);
        int n = 0;
        for (int a = 0; a < M; a++) if (_active[a]) Array.Copy(_obs, a * O, ws.Input, n++ * O, O);
        if (n > 0) MlpOps.Forward(net, ws, n);
        int OS = pol.OutputSize, j = 0;
        for (int a = 0; a < M; a++)
        {
            if (!_active[a]) { for (int h = 0; h < H; h++) actionsOut[a * H + h] = 0; continue; }
            Array.Copy(ws.Output, j++ * OS, _out, 0, OS);
            Array.Copy(_mask, a * L, _maskRow, 0, L);
            if (_sample) { for (int h = 0; h < H; h++) _u[h] = rng.NextFloat(); pol.SampleWithRng(_out, _maskRow, _u, _act); }
            else pol.ArgmaxPerHead(_out, _maskRow, _act);
            for (int h = 0; h < H; h++) actionsOut[a * H + h] = _act[h];
        }
    }

    // per-instance (= per worker thread) cache of the trainable copy of each frozen policy; league snapshots come and go
    readonly Dictionary<MlpPolicy, (Mlp, MlpWorkspace)> _nets = new(ReferenceEqualityComparer.Instance);
    (Mlp, MlpWorkspace) Net(MlpPolicy pol, int rows)
    {
        if (_nets.TryGetValue(pol, out var v)) return v;
        if (_nets.Count >= 16) _nets.Clear();
        var net = Mlp.FromPolicy(pol);
        v = (net, new MlpWorkspace(net, rows));
        _nets[pol] = v;
        return v;
    }
}
