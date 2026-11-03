namespace Btc.RL.Toy;

/// <summary>
/// Contextual bandit for PPO sanity tests: K contexts (one-hot obs), two heads [4,3]; reward 0.5 per head whose
/// action equals the context's target (ctx%4, ctx%3). One wrong head-0 action is masked at random. Episodes last
/// one step; MaxAgents agents per env each get their own context and reward. No opponent.
/// </summary>
public sealed class BanditEnv : IEnvBatch
{
    public const int K = 8;
    readonly int[][] _ctx; readonly int[][] _masked; readonly Rng[] _rng;
    readonly int _agents;

    public BanditEnv(int numEnvs, int agents)
    {
        _agents = agents;
        _ctx = new int[numEnvs][]; _masked = new int[numEnvs][]; _rng = new Rng[numEnvs];
        for (int i = 0; i < numEnvs; i++) { _ctx[i] = new int[agents]; _masked[i] = new int[agents]; _rng[i] = new Rng((ulong)i + 1); }
    }

    public int NumEnvs => _ctx.Length;
    public int MaxAgents => _agents;
    public int ObsSize => K;
    public int CentralSize => 2;
    public int[] HeadSizes { get; } = { 4, 3 };
    public bool HasOpponent => false;
    public string Schema => "bandit/v1|obs8|heads4,3";

    public static int Target0(int c) => c % 4;
    public static int Target1(int c) => c % 3;

    public void Reset(int env, ulong seed)
    {
        var r = _rng[env]; r.State = seed;
        for (int a = 0; a < _agents; a++) Draw(env, a);
    }

    void Draw(int env, int a)
    {
        var r = _rng[env];
        int c = r.Next(K); _ctx[env][a] = c;
        int m = r.Next(4); if (m == Target0(c)) m = (m + 1) % 4;
        _masked[env][a] = m;
    }

    public void Observe(int env, int side, float[] obs, int obsOff, byte[] mask, int maskOff, float[] central, int centralOff, bool[] active, int activeOff)
    {
        for (int a = 0; a < _agents; a++)
        {
            Array.Clear(obs, obsOff + a * K, K);
            obs[obsOff + a * K + _ctx[env][a]] = 1;
            for (int j = 0; j < 7; j++) mask[maskOff + a * 7 + j] = 1;
            mask[maskOff + a * 7 + _masked[env][a]] = 0;
            if (central != null && central.Length >= centralOff + (a + 1) * 2) { central[centralOff + a * 2] = 1; central[centralOff + a * 2 + 1] = _ctx[env][a] / (float)K; }
            active[activeOff + a] = true;
        }
    }

    public void Step(int env, int[] actions0, int[] actions1, float[] rewards0, int rewOff, out bool done, out int outcome)
    {
        for (int a = 0; a < _agents; a++)
        {
            int c = _ctx[env][a];
            float r = 0;
            if (actions0[a * 2] == Target0(c)) r += 0.5f;
            if (actions0[a * 2 + 1] == Target1(c)) r += 0.5f;
            rewards0[rewOff + a] = r;
        }
        done = true; outcome = 0;
    }
}
