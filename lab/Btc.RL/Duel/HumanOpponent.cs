using Btc.RL;
using Btc.Shared;

namespace Btc.RL.Duel;

/// <summary>
/// "Humanized" opponent: wraps a policy opponent with the limits measured from human duel logs (2026-11-02, 3200 commands of the
/// user and a tester): ~0.9-1.3 commands per second (gap median ~0.6 s), each command goes to ONE selection (45% the whole army,
/// 30% one unit, 25% 2-4 units) and is based on what the player saw a reaction delay ago. Every other unit keeps its order
/// (Continue). The policy still decides every step; only the delayed decision of one chosen unit is issued, to its selection.
/// The learner trained against it can exploit attention / reaction limits (pressure in two places, punish slow retreats).
/// </summary>
public sealed class HumanOpponent : IOpponent
{
    public static float RateMin = 0.8f, RateMax = 1.4f;        // commands per second (per episode, uniform)
    public static float ReactMin = 0.25f, ReactMax = 0.5f;     // seconds between seeing and issuing
    public static float PAll = 0.45f, POne = 0.30f;            // selection size; rest = 2-4 units
    const int Ring = 16;

    readonly PolicyOpponent _inner;
    readonly float _dec;
    State[] _st = Array.Empty<State>();
    int[] _buf = Array.Empty<int>();

    sealed class State
    {
        public float LastFt = float.MaxValue, NextCmd, Rate, React;
        public int Head;                     // ring write index
        public float[] T = new float[Ring];  // fight time of each stored decision
        public int[][] A = new int[Ring][];
    }

    public HumanOpponent(PolicyOpponent inner, float decisionSec) { _inner = inner; _dec = decisionSec; }

    public void Act(IEnvBatch env, int e, int side, Rng rng, int[] actionsOut)
    {
        int M = env.MaxAgents, H = env.HeadSizes.Length, MH = M * H;
        if (_st.Length <= e) Array.Resize(ref _st, env.NumEnvs);
        if (_buf.Length != MH) _buf = new int[MH];
        var s = _st[e] ??= new State();
        float ft = env is SimDuelEnv sd ? sd.Host(e).FightTime : s.LastFt + _dec;
        if (ft < s.LastFt)
        {
            // new episode: new "player"
            s.Rate = RateMin + (RateMax - RateMin) * rng.NextFloat();
            s.React = ReactMin + (ReactMax - ReactMin) * rng.NextFloat();
            s.NextCmd = ft + s.React;
            for (int i = 0; i < Ring; i++) s.T[i] = float.MinValue;
        }
        s.LastFt = ft;
        _inner.Act(env, e, side, rng, _buf);
        var slot = s.A[s.Head] ??= new int[MH];
        if (slot.Length != MH) slot = s.A[s.Head] = new int[MH];
        Array.Copy(_buf, slot, MH); s.T[s.Head] = ft; s.Head = (s.Head + 1) % Ring;

        Array.Clear(actionsOut, 0, MH);    // Continue everywhere
        if (ft < s.NextCmd) return;
        // the newest stored decision that is at least React old
        int[] seen = null;
        for (int k = 1; k <= Ring; k++)
        {
            int i = (s.Head - k + Ring) % Ring;
            if (s.T[i] != float.MinValue && ft - s.T[i] >= s.React - 1e-4f) { seen = s.A[i]; break; }
        }
        if (seen == null) return;
        // candidates: units the policy wanted to re-order (alive units output a real action; dead ones 0 = Continue)
        int nc = 0; Span<int> cand = stackalloc int[M];
        for (int a = 0; a < M; a++) if (seen[a * H] != DuelActions.Continue) cand[nc++] = a;
        if (nc == 0) return;              // nothing to do: the command slot stays open
        int c = cand[rng.Next(nc)];
        double u = rng.NextDouble();
        int want = u < PAll ? M : u < PAll + POne ? 1 : 2 + rng.Next(3);
        // selection: the chosen unit, then units that wanted the same action, then the other candidates (whole army = every
        // unit the policy re-orders); dead units ignore orders
        int got = 0;
        void Give(int a) { if (got >= want) return; for (int h = 0; h < H; h++) actionsOut[a * H + h] = seen[c * H + h]; got++; }
        Give(c);
        for (int i = 0; i < nc && got < want; i++) { int a = cand[i]; if (a != c && seen[a * H] == seen[c * H]) Give(a); }
        if (want == M) for (int i = 0; i < nc; i++) { int a = cand[i]; if (a != c && seen[a * H] != seen[c * H]) for (int h = 0; h < H; h++) actionsOut[a * H + h] = seen[c * H + h]; }
        else for (int i = 0; i < nc && got < want; i++) { int a = cand[i]; if (a != c && seen[a * H] != seen[c * H]) Give(a); }
        // next command: exponential gap around 1/rate, at least 0.15 s
        float gap = (float)(-Math.Log(1.0 - rng.NextDouble() * 0.999) / s.Rate);
        s.NextCmd = ft + Math.Max(0.15f, gap);
    }
}
