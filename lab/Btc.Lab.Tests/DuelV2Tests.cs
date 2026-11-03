using Btc.RL;
using Btc.RL.Duel;
using Btc.Shared;
using Btc.Sim;

namespace Btc.Lab.Tests;

/// <summary>LuminaZero v2: infinite heal, 15v18 episodes, obs v2 features (filled, bounded, mirror-consistent), actions v2 decode, rewards v2.</summary>
static class DuelV2Tests
{
    // ---------------- helpers ----------------
    static DuelView Fixture(int n0, int n1)
    {
        var v = new DuelView();
        int n = n0 + n1;
        v.Count = n; v.StartUnits[0] = n0; v.StartUnits[1] = n1; v.Time = 30f; v.Skips = true;
        for (int i = 0; i < n; i++)
        {
            v.Side[i] = i < n0 ? 0 : 1; v.Dead[i] = false; v.MaxHp[i] = 500f; v.Hp[i] = 500f;
            v.StrikeAt[i] = -100f; v.HurtAt[i] = -100f; v.TargetOf[i] = -1; v.OrdKind[i] = 0; v.OrdTarget[i] = -1; v.OrdAt[i] = -100f;
            v.OrdHasDest[i] = false; v.Type[i] = DuelView.TypeHalberdier; v.FX[i] = v.Side[i] == 0 ? 1f : -1f;
        }
        return v;
    }

    static DuelView Random(ulong seed, int n0, int n1)
    {
        var r = new Rng(seed);
        var v = Fixture(n0, n1);
        int n = n0 + n1;
        v.InfiniteHeal = r.NextFloat() < 0.5f;
        for (int i = 0; i < n; i++)
        {
            v.X[i] = 2f + 96f * r.NextFloat(); v.Z[i] = 1f + 18f * r.NextFloat();
            v.VX[i] = (r.NextFloat() - 0.5f) * 8f; v.VZ[i] = (r.NextFloat() - 0.5f) * 8f;
            v.Hp[i] = 500f * r.NextFloat(); v.Dead[i] = r.NextFloat() < 0.1f;
            v.Type[i] = r.Next(DuelView.TypeCount);
            v.StrikeAt[i] = v.Time - 3f * r.NextFloat(); v.HurtAt[i] = v.Time - 3f * r.NextFloat();
            v.OrdKind[i] = r.Next(4); v.OrdAt[i] = v.Time - 10f * r.NextFloat();
            int foe0 = v.Side[i] == 0 ? n0 : 0, foeN = v.Side[i] == 0 ? n1 : n0;
            if (v.OrdKind[i] == 1 || v.OrdKind[i] == 3) { v.OrdHasDest[i] = true; v.OrdX[i] = 100f * r.NextFloat(); v.OrdZ[i] = 20f * r.NextFloat(); }
            if (v.OrdKind[i] == 2) v.OrdTarget[i] = r.NextFloat() < 0.8f ? foe0 + r.Next(foeN) : r.Next(n);
            v.TargetOf[i] = r.NextFloat() < 0.5f ? foe0 + r.Next(foeN) : -1;
            v.Hp[i] = v.Dead[i] ? 0f : v.Hp[i];
        }
        v.Kills[0] = r.Next(3); v.Kills[1] = r.Next(3); v.Ctrl[0] = 3f * r.NextFloat(); v.Ctrl[1] = 2f * r.NextFloat();
        v.Escaping[0] = r.NextFloat() < 0.2f;
        return v;
    }

    static DuelView Mirror(DuelView a)
    {
        var m = new DuelView();
        m.Len = a.Len; m.Wid = a.Wid; m.SafeLen = a.SafeLen; m.NeutralLen = a.NeutralLen; m.MidLen = a.MidLen;
        m.Skips = a.Skips; m.InfiniteHeal = a.InfiniteHeal; m.SpaceControlSec = a.SpaceControlSec; m.Time = a.Time; m.Count = a.Count;
        for (int s = 0; s < 2; s++) { m.Escaping[s] = a.Escaping[1 - s]; m.Ctrl[s] = a.Ctrl[1 - s]; m.Kills[s] = a.Kills[1 - s]; m.StartUnits[s] = a.StartUnits[1 - s]; }
        for (int i = 0; i < a.Count; i++)
        {
            m.Side[i] = 1 - a.Side[i]; m.Dead[i] = a.Dead[i];
            m.X[i] = a.Len - a.X[i]; m.Z[i] = a.Z[i]; m.VX[i] = -a.VX[i]; m.VZ[i] = a.VZ[i]; m.FX[i] = -a.FX[i]; m.FZ[i] = a.FZ[i];
            m.Hp[i] = a.Hp[i]; m.MaxHp[i] = a.MaxHp[i]; m.StrikeAt[i] = a.StrikeAt[i]; m.HurtAt[i] = a.HurtAt[i];
            m.TargetOf[i] = a.TargetOf[i]; m.OrdKind[i] = a.OrdKind[i]; m.OrdAt[i] = a.OrdAt[i]; m.OrdTarget[i] = a.OrdTarget[i];
            m.OrdHasDest[i] = a.OrdHasDest[i]; m.OrdX[i] = a.Len - a.OrdX[i]; m.OrdZ[i] = a.OrdZ[i]; m.Type[i] = a.Type[i];
        }
        return m;
    }

    static void Enc(DuelView v, int u, float[] obs, int[] slots)
    {
        DuelObs.Encode(v, u, obs, 0, slots, new int[DuelView.MaxUnits], new float[DuelView.MaxUnits]);
    }

    // ---------------- tests ----------------
    public static (bool, string) Heal()
    {
        DuelSim Make(bool heal)
        {
            var s = new DuelSim { P = SimParams.Lab() };
            s.Reset(new SimRules { UnitsPerSide = 1, Skips = true, InfiniteHeal = heal, TimeLimit = 1000f }, 1);
            s.Hp[0] = 100f;
            return s;
        }
        var h = Make(true); var n = Make(false);
        int before = 0; float gain0 = 0;
        for (int i = 0; i < 40; i++) { h.Step(); n.Step(); }            // 0.8 s: no heal tick yet
        gain0 = h.Hp[0] - n.Hp[0]; before = 1;
        for (int i = 0; i < 210; i++) { h.Step(); n.Step(); }           // 5.0 s: ticks at 1 s .. 5 s
        float gain = h.Hp[0] - n.Hp[0];
        bool ok = before == 1 && Math.Abs(gain0) < 1e-3f && gain >= 39.9f && gain <= 50.1f && Math.Abs(gain / 10f - MathF.Round(gain / 10f)) < 1e-3f && h.P.HealPerStep == 10f && h.P.HealStep == 1f;
        return (ok, $"after 0.8 s extra hp {gain0:F1}, after 5.0 s extra hp {gain:F1} (4-5 ticks of {h.P.HealPerStep:F0} hp / {h.P.HealStep:F1} s)");
    }

    public static (bool, string) Big15v18()
    {
        var cur = new DuelCurriculum { SkipsProb = 1f, HealProb = 0f, TimeLimit = 40f, HandicapProb = 1f, HandicapMin = 3, HandicapMax = 3, DomainRand = 0f };
        cur.SetUnits("15"); cur.SetArena("3");
        var env = new SimDuelEnv(2, cur, new[] { DuelOpponents.Get("prod4"), DuelOpponents.Get("v2") });
        int bad = 0, active = 0, eps = 0, seen18 = 0;
        var r = DuelEnvTests.Play(env, 11, 600, false, (obs, mask, act) =>
        {
            for (int a = 0; a < act.Length; a++)
            {
                if (!act[a]) continue;
                active++;
                for (int j = 0; j < DuelObs.Size; j++) { float x = obs[a * DuelObs.Size + j]; if (!float.IsFinite(x) || x < -6f || x > 6f) bad++; }
            }
        });
        eps = r.eps;
        var inf = env.Info(0);
        if (inf.Units0 == 15 && inf.Units1 == 18) seen18++;
        bool ok = bad == 0 && active > 0 && seen18 > 0 && env.MaxAgents == 18;
        return (ok, $"units {inf.Units0}v{inf.Units1} arena {inf.Arena}, episodes {eps}, active agent rows {active}, bad obs {bad}, MaxAgents {env.MaxAgents}");
    }

    public static (bool, string) ObsFeatures()
    {
        // fixture: unit 0 (side 0) moves to (50,12); enemy 2 attacks unit 0; unit 1 (ally) attacks enemy 3
        var v = Fixture(2, 2);
        v.X[0] = 30f; v.Z[0] = 10f; v.X[1] = 28f; v.Z[1] = 6f; v.X[2] = 40f; v.Z[2] = 10f; v.X[3] = 45f; v.Z[3] = 14f;
        v.OrdKind[0] = 3; v.OrdHasDest[0] = true; v.OrdX[0] = 50f; v.OrdZ[0] = 12f; v.OrdAt[0] = v.Time - 1f;
        v.OrdKind[2] = 2; v.OrdTarget[2] = 0; v.TargetOf[2] = 0; v.StrikeAt[2] = v.Time - 0.2f;
        v.OrdKind[1] = 2; v.OrdTarget[1] = 3; v.TargetOf[1] = 3;
        var o = new float[DuelObs.Size]; var slots = new int[DuelObs.SlotCount];
        Enc(v, 0, o, slots);
        int S = DuelObs.SelfSize, E = DuelObs.EnemySize;
        int eAt = S, aAt = S + DuelObs.KEnemy * E, gAt = aAt + DuelObs.KAlly * DuelObs.AllySize;
        bool self = o[26] == 1f && Math.Abs(o[27] - 1f) < 1e-5f && Math.Abs(o[28] - 0.1f) < 1e-5f && Math.Abs(o[29] - 20.0998f / 20f) < 1e-3f
                    && Math.Abs(o[35 + 4] - 1f) < 1e-6f && Math.Abs(o[34] - 0.5f) < 1e-5f && o[18] == 1f;
        // enemy slot 0 = unit 2 (nearest, d 10): present, attacks me, order = attack, targets me, strike in progress, type halberdier
        bool e0 = slots[0] == 2 && o[eAt] == 1f && o[eAt + 9] == 1f && o[eAt + 12] == 1f && o[eAt + 16] == 1f && o[eAt + 21] == 1f && o[eAt + 28 + 4] == 1f;
        // enemy slot 1 = unit 3: our attackers 1/4, is the team focus (ally 1 targets it)
        bool e1 = slots[1] == 3 && Math.Abs(o[eAt + E + 14] - 0.25f) < 1e-6f && o[eAt + E + 27] == 1f && slots[DuelObs.FocusSlot] == 3 && slots[DuelObs.AttackerSlot] == 2;
        // ally slot 0 = unit 1: order target is an enemy of mine (flag index 14+4+1), has attack order
        int a0 = aAt;
        bool ally = o[a0] == 1f && o[a0 + 10] == 1f && o[a0 + 18 + 2] == 1f && o[a0 + 18 + 3] != 0f;
        bool glob = Math.Abs(o[gAt + 17] - 2f / 18f) < 1e-6f && Math.Abs(o[gAt + 19]) < 1e-6f && o[gAt + 16] == 0f;
        // size / schema consistency
        bool sizes = DuelObs.Size == 43 + 8 * 36 + 6 * 32 + 27 && DuelObs.CentralSize == 553 && DuelObs.Schema.Contains("c553") && DuelObs.Schema.Contains("act" + DuelActions.Count);
        // all random scenarios: finite + bounded + mirror-consistent (obs and central)
        int bad = 0, mism = 0, rows = 0; float worst = 0f;
        var o2 = new float[DuelObs.Size]; var s2 = new int[DuelObs.SlotCount];
        var c1 = new float[DuelObs.CentralSize]; var c2 = new float[DuelObs.CentralSize];
        for (int t = 0; t < 60; t++)
        {
            int n0 = 4 + t % 12, n1 = n0 + t % 4;
            var rv = Random(1000 + (ulong)t, n0, n1);
            var mv = Mirror(rv);
            for (int u = 0; u < rv.Count; u++)
            {
                if (rv.Dead[u]) continue;
                Enc(rv, u, o, slots); Enc(mv, u, o2, s2);
                rows++;
                for (int j = 0; j < DuelObs.Size; j++)
                {
                    if (!float.IsFinite(o[j]) || o[j] < -6f || o[j] > 6f) bad++;
                    float d = Math.Abs(o[j] - o2[j]); if (d > worst) worst = d;
                    if (d > 2e-4f) mism++;
                }
                for (int k = 0; k < DuelObs.SlotCount; k++) if (slots[k] != s2[k]) mism++;
            }
            DuelObs.EncodeCentral(rv, 0, c1, 0); DuelObs.EncodeCentral(mv, 1, c2, 0);
            for (int j = 0; j < DuelObs.CentralSize; j++)
            {
                if (!float.IsFinite(c1[j]) || c1[j] < -6f || c1[j] > 6f) bad++;
                if (Math.Abs(c1[j] - c2[j]) > 2e-4f) mism++;
            }
        }
        bool ok = self && e0 && e1 && ally && glob && sizes && bad == 0 && mism == 0;
        return (ok, $"self {self} enemy0 {e0} enemy1/focus {e1} ally {ally} globals {glob} sizes {sizes} (obs {DuelObs.Size}, central {DuelObs.CentralSize}); {rows} random rows: out-of-range {bad}, mirror mismatches {mism}, worst {worst:E1}");
    }

    public static (bool, string) ActionsDecode()
    {
        var v = Fixture(3, 3);
        // own: 0 (me, x 30), 1, 2; enemies 3 (near), 4, 5. Units 1 and 2 attack enemy 4; enemy 5 attacks me.
        v.X[0] = 30f; v.Z[0] = 10f; v.X[1] = 28f; v.Z[1] = 8f; v.X[2] = 28f; v.Z[2] = 12f;
        v.X[3] = 36f; v.Z[3] = 10f; v.X[4] = 45f; v.Z[4] = 10f; v.X[5] = 50f; v.Z[5] = 16f;
        v.TargetOf[1] = 4; v.TargetOf[2] = 4; v.TargetOf[5] = 0;
        var o = new float[DuelObs.Size]; var slots = new int[DuelObs.SlotCount]; var mask = new byte[DuelActions.Count];
        Enc(v, 0, o, slots);
        DuelActions.Mask(v, 0, slots, mask, 0);
        int kind, tgt; float x, z;
        DuelActions.Decode(v, 0, DuelActions.Focus, slots, out kind, out x, out z, out tgt);
        bool focus = slots[DuelObs.FocusSlot] == 4 && kind == DuelActions.OrdAttack && tgt == 4 && mask[DuelActions.Focus] == 1;
        DuelActions.Decode(v, 0, DuelActions.Attacker, slots, out kind, out x, out z, out tgt);
        bool attacker = slots[DuelObs.AttackerSlot] == 5 && kind == DuelActions.OrdAttack && tgt == 5 && mask[DuelActions.Attacker] == 1;
        DuelActions.Decode(v, 0, DuelActions.AttackSlot, slots, out kind, out x, out z, out tgt);
        bool slot0 = kind == DuelActions.OrdAttack && tgt == 3 && mask[DuelActions.AttackSlot] == 1 && mask[DuelActions.AttackSlot + 3] == 0;   // slot 3 is empty (only 3 enemies)
        DuelActions.Decode(v, 0, DuelActions.Run5 + 0, slots, out kind, out x, out z, out tgt);
        bool run = kind == DuelActions.OrdMove && Math.Abs(x - 35f) < 1e-4f && Math.Abs(z - 10f) < 1e-4f;
        DuelActions.Decode(v, 0, DuelActions.Run2 + 8, slots, out kind, out x, out z, out tgt);
        bool run2 = Math.Abs(x - 28f) < 1e-4f && Math.Abs(z - 10f) < 1e-3f;                     // direction 8 = 180 deg = away from the enemy
        DuelActions.Decode(v, 0, DuelActions.Sweep + 0, slots, out kind, out x, out z, out tgt);
        bool sweep = kind == DuelActions.OrdMoveAttack && Math.Abs(x - 38f) < 1e-4f;
        // mirrored side: run 12 m towards the enemy goes to -x
        var mv = Mirror(v);
        Enc(mv, 0, o, slots);
        DuelActions.Decode(mv, 0, DuelActions.Run12, slots, out kind, out x, out z, out tgt);
        bool mir = Math.Abs(x - (100f - 30f - 12f)) < 1e-4f && slots[DuelObs.FocusSlot] == 4;
        // no attacker -> attacker action masked; protected focus target is skipped; cancel guard masks everything but Continue
        v.TargetOf[5] = -1;
        Enc(v, 0, o, slots); DuelActions.Mask(v, 0, slots, mask, 0);
        bool noAtt = slots[DuelObs.AttackerSlot] == -1 && mask[DuelActions.Attacker] == 0;
        v.StrikeAt[0] = v.Time - 0.05f; DuelActions.Mask(v, 0, slots, mask, 0);
        int allowed = 0; for (int a = 0; a < DuelActions.Count; a++) allowed += mask[a];
        bool guard = allowed == 1 && mask[0] == 1;
        v.StrikeAt[0] = -100f; v.X[4] = 97f; v.Z[4] = 10f;                                       // enemy 4 inside its own safe zone: cannot be focus / attacked
        Enc(v, 0, o, slots); DuelActions.Mask(v, 0, slots, mask, 0);
        bool prot = slots[DuelObs.FocusSlot] != 4 && mask[DuelActions.Focus] == (slots[DuelObs.FocusSlot] >= 0 ? 1 : 0);
        bool ok = focus && attacker && slot0 && run && run2 && sweep && mir && noAtt && guard && prot && DuelActions.Count == 69;
        return (ok, $"focus {focus} attacker {attacker} slot0 {slot0} run5 {run} run2-back {run2} sweep {sweep} mirrored {mir} no-attacker-masked {noAtt} cancel-guard {guard} protected-skipped {prot}");
    }

    public static (bool, string) Rewards()
    {
        // (1) all intermediate terms off: the episode total of every agent is exactly the outcome
        double w1 = Worst(Cur(0f, 0f, 0f, 0f, 0f), 4, 8, out _);
        // (2) kill / death terms only: mean agent total = outcome + 0.4 * kills / N_enemy - 0.4 * deaths / N_own (any TeamShare)
        double w2 = Worst(Cur(1f, 0.5f, 0f, 0f, 0.4f), 5, 10, out int kills);
        double w2b = Worst(Cur(1f, 0f, 0f, 0f, 0.4f), 6, 6, out _);
        // (3) all terms on: finite and of sane size (|total| <= 3.5 for a 5v5 episode), then the InterScale multiplier scales exactly
        double w3 = Worst(Cur(1f, 0.5f, 0.3f, 0.3f, 0.4f), 7, 8, out _, bound: true);
        bool ok = w1 < 1e-4 && w2 < 1e-3 && w2b < 1e-3 && w3 < 3.5 && kills > 0;
        return (ok, $"terms off |total-outcome| {w1:E1}; kill/death terms exact dev {w2:E1} (team-share 0.5), {w2b:E1} (per-unit), {kills} kills seen; all terms: max |agent total| {w3:F2} (<= 3.5)");
    }

    static DuelCurriculum Cur(float inter, float ts, float dealt, float taken, float kd)
    {
        var c = new DuelCurriculum { SkipsProb = 1f, HealProb = 0f, TimeLimit = 60f, DomainRand = 0f, Shaping = 0f, Indiv = 0f,
            InterScale = inter, TeamShare = ts, DmgDealt = dealt, DmgTaken = taken, KillReward = kd, DeathPenalty = kd };
        c.SetUnits("5"); c.SetArena("1");
        return c;
    }

    /// <summary>Random-play episodes vs prod0. Returns the worst deviation of the mean per-agent total from outcome + kill/death terms
    /// (bound=true: the worst |total of one agent| instead). kills = enemy deaths seen over all episodes.</summary>
    static double Worst(DuelCurriculum cur, ulong seed, int episodes, out int kills, bool bound = false)
    {
        var env = new SimDuelEnv(1, cur, new[] { DuelOpponents.Get("prod0") });
        int M = env.MaxAgents, O = env.ObsSize, C = env.CentralSize, L = DuelActions.Count;
        var obs = new float[M * O]; var mask = new byte[M * L]; var cen = new float[M * C]; var act = new bool[M];
        var a0 = new int[M]; var a1 = new int[M]; var rew = new float[M];
        double worst = 0; kills = 0;
        var rng = new Rng(seed);
        for (int ep = 0; ep < episodes; ep++)
        {
            env.Reset(0, seed * 1000 + (ulong)ep);
            var tot = new double[M];
            bool done = false; int outcome = 0, steps = 0;
            while (!done && steps++ < 100000)
            {
                env.Observe(0, 0, obs, 0, mask, 0, cen, 0, act, 0);
                for (int a = 0; a < M; a++) { a0[a] = 0; if (act[a] && rng.NextFloat() < 0.4f) { int w = DuelActions.AttackSlot + rng.Next(4); if (mask[a * L + w] != 0) a0[a] = w; } }
                env.Step(0, a0, a1, rew, 0, out done, out outcome);
                for (int a = 0; a < M; a++) tot[a] += rew[a];
            }
            var inf = env.Info(0);
            kills += inf.Kills0;
            double mean = 0; for (int a = 0; a < inf.Units0; a++) { mean += tot[a]; if (bound) worst = Math.Max(worst, Math.Abs(tot[a])); }
            mean /= inf.Units0;
            double want = outcome + cur.KillReward * inf.Kills0 / inf.Units1 - cur.DeathPenalty * inf.Kills1 / inf.Units0;
            if (cur.InterScale == 0f) want = outcome;
            if (!bound) worst = Math.Max(worst, Math.Abs(mean - want));
        }
        return worst;
    }

    // ---------------- LuminaZero curriculum / draw reward / decision period ----------------
    public static (bool, string) DrawReward()
    {
        double Run(float draw, out int outcome)
        {
            var c = new DuelCurriculum { SkipsProb = 1f, HealProb = 0f, TimeLimit = 4f, DomainRand = 0f, Shaping = 0f, Indiv = 0f, InterScale = 0f, DrawReward = draw };
            c.SetUnits("3"); c.SetArena("1");
            var env = new SimDuelEnv(1, c, new[] { DuelOpponents.Get("idle") }, DuelOpponents.Get("idle"));
            int M = env.MaxAgents; var a0 = new int[M]; var a1 = new int[M]; var rew = new float[M];
            env.Reset(0, 5); bool done = false; outcome = 9; double tot = 0; int k = 0;
            while (!done && k++ < 5000) { env.Step(0, a0, a1, rew, 0, out done, out outcome); tot += rew[0]; }
            return tot;
        }
        double d0 = Run(0f, out int o0), d1 = Run(-0.5f, out int o1);
        bool ok = o0 == 0 && o1 == 0 && Math.Abs(d0) < 1e-6 && Math.Abs(d1 + 0.5) < 1e-6;
        return (ok, $"draw (outcome {o1}) total reward: DrawReward 0 -> {d0:F2}, -0.5 -> {d1:F2}");
    }

    public static (bool, string) RolesPassive()
    {
        // idle vs idle, time limit 10 s: a learner-attacker loses at the limit and pays 0.1/s after the 2 s grace (gap never closes);
        // a learner-defender wins at the limit and pays nothing
        double Run(int roles, float attackProb, out int outcome)
        {
            var c = new DuelCurriculum { SkipsProb = 1f, HealProb = 0f, TimeLimit = 10f, DomainRand = 0f, Shaping = 0f, Indiv = 0f, InterScale = 0f, Roles = roles, AttackProb = attackProb, PassivePen = 0.1f, PassiveGrace = 2f };
            c.SetUnits("3"); c.SetArena("1");
            var env = new SimDuelEnv(1, c, new[] { DuelOpponents.Get("idle") }, DuelOpponents.Get("idle"));
            int M = env.MaxAgents; var a0 = new int[M]; var a1 = new int[M]; var rew = new float[M];
            env.Reset(0, 5); bool done = false; outcome = 9; double tot = 0; int k = 0;
            while (!done && k++ < 5000) { env.Step(0, a0, a1, rew, 0, out done, out outcome); tot += rew[0]; }
            return tot;
        }
        double att = Run(2, 1f, out int oa), def = Run(1, 0f, out int od);
        bool ok = oa == -1 && od == 1 && att < -1.5 && att > -1.85 && Math.Abs(def - 1) < 1e-6;
        return (ok, $"attacker outcome {oa} reward {att:F2} (expect ~-1.7), defender outcome {od} reward {def:F2}");
    }

    public static (bool, string) HandicapOwnSide()
    {
        // HandicapOwnProb .5: the extra units go to side 0 in about half of the handicap episodes, never to both
        var cur = new DuelCurriculum { SkipsProb = 1f, HealProb = 0f, TimeLimit = 4f, HandicapProb = 1f, HandicapMin = 2, HandicapMax = 2, HandicapOwnProb = 0.5f, DomainRand = 0f };
        cur.SetUnits("5"); cur.SetArena("1");
        var env = new SimDuelEnv(1, cur, new[] { DuelOpponents.Get("prod4") });
        int own = 0, opp = 0, bad = 0;
        for (int e = 0; e < 200; e++)
        {
            env.Reset(0, (ulong)(e + 1));
            var inf = env.Info(0);
            if (inf.Units0 == 7 && inf.Units1 == 5) own++; else if (inf.Units0 == 5 && inf.Units1 == 7) opp++; else bad++;
        }
        return (bad == 0 && own > 60 && opp > 60, $"own {own} opp {opp} bad {bad} of 200");
    }

    public static (bool, string) StageSwitch()
    {
        var b = new DuelCurriculum { DomainRand = 0f };
        b.SetUnits("4-15"); b.SetArena("3");
        var s0 = DuelEval.StageZ(b, 0); var s1 = DuelEval.StageZ(b, 1); var s2 = DuelEval.StageZ(b, 2); var s3 = DuelEval.StageZ(b, 3);
        bool tbl = s0.Units.Length == 1 && s0.Units[0] == 6 && s0.HandicapProb == 0f && s0.TimeLimit == 90f && s0.DecisionSec == 0.2f
                && s1.Units.Max() == 10 && s1.Units.Min() == 4 && s1.HandicapProb == 0.2f && s1.TimeLimit == 90f && s1.DecisionSec == 0.1f
                && s2.Units.Max() == 12 && s2.HandicapProb == 0.2f && s2.HandicapOwnProb == 0.5f && s2.TimeLimit == 150f
                && s3.Units.Max() == 15 && s3.HandicapProb == 0.4f && s3.HandicapOwnProb == 0.5f && s3.TimeLimit == 150f && b.Units.Max() == 15;
        // live switch of a running env: units, time limit, decision period (via Decisions per fight second), opponent weights
        var names = DuelEval.ZNames;
        var lg = new DuelLeague(names, (float[])DuelEval.ZWeights[0].Clone(), 4);
        var defs = names.Select(n => n == "pool" ? new DuelOppDef { Name = "pool", Bot = null, Create = () => new NullOpponent() } : DuelOpponents.Get(n)).ToArray();
        var env = new SimDuelEnv(4, s0, defs);
        int M = env.MaxAgents; var a0 = new int[M]; var a1 = new int[M]; var rew = new float[M];
        (int u0, int u1, double decPerSec) Ep(DuelCurriculum c)
        {
            env.Cur = c; env.Reset(0, 77); bool done = false; int k = 0;
            while (!done && k++ < 3000) env.Step(0, a0, a1, rew, 0, out done, out _);
            var i = env.Info(0);
            return (i.Units0, i.Units1, i.Decisions / Math.Max(1e-3, i.FightTime - 1.2));
        }
        var e0 = Ep(s0); var e2 = Ep(s2);
        bool env0 = e0.u0 == 6 && e0.u1 == 6 && Math.Abs(e0.decPerSec - 5.0) < 1.0;      // 0.2 s -> 5 decisions / s
        bool env2 = e2.u0 >= 4 && e2.u0 <= 15 && Math.Abs(e2.decPerSec - 10.0) < 1.5;    // 0.1 s -> 10 / s
        var cnt0 = new int[names.Length]; var cnt2 = new int[names.Length];
        var rng = new Rng(3);
        lg.BeginRollout(); for (int i = 0; i < 4000; i++) cnt0[lg.Pick(0, rng)]++;
        lg.SetWeights(DuelEval.ZWeights[2]); lg.BeginRollout(); for (int i = 0; i < 4000; i++) cnt2[lg.Pick(0, rng)]++;
        int pp = Array.IndexOf(names, "pool");
        bool mix = names.Length == 1 && pp == 0 && cnt0[pp] == 4000 && cnt2[pp] == 4000;   // self-play only: every episode is a pool episode
        bool panels = DuelEval.PanelZ(0).All(c => c.Units == 6 && c.Arena == 1 && c.Opp != null) && DuelEval.PanelZ(0).Length == 7;
        return (tbl && env0 && env2 && mix && panels, $"tables {tbl}; stage0 {e0.u0}v{e0.u1} {e0.decPerSec:F1} dec/s, stage2 {e2.u0}v{e2.u1} {e2.decPerSec:F1} dec/s; self-play mix {mix}; stage-0 panel {panels}");
    }

    public static (bool, string) PolicyMeta()
    {
        var net = new Mlp(DuelObs.Size, new[] { 16 }, DuelActions.Count, Act.Tanh);
        net.InitOrthogonal(new Rng(5), 1.4f, 0.05f);
        var pol = net.ToPolicy(new[] { DuelActions.Count }, false, MlpPolicy.Fnv1a(DuelObs.Schema));
        byte[] plain = pol.Save();
        pol.DecisionSec = 0.2f;
        byte[] meta = pol.Save();
        var l0 = MlpPolicy.Load(plain); var l1 = MlpPolicy.Load(meta);
        bool rt = l0.DecisionSec == 0f && Math.Abs(l1.DecisionSec - 0.2f) < 1e-6f && meta.Length == plain.Length + 8 && l1.Save().AsSpan().SequenceEqual(meta)
               && meta.AsSpan(0, plain.Length).SequenceEqual(plain);
        // controller: orders issued in 20 sim seconds of a near-random policy scale with the decision rate
        int Orders(float sec)
        {
            pol.DecisionSec = sec;
            var c = new DuelCurriculum { SkipsProb = 0f, HealProb = 0f, TimeLimit = 20f, DomainRand = 0f, FpsMin = 60f, FpsMax = 60f };
            c.SetUnits("3"); c.SetArena("1");
            var def = DuelOpponents.Lumina(MlpPolicy.Load(pol.Save()), false);
            var env = new SimDuelEnv(1, c, new[] { DuelOpponents.Get("idle") }, def);
            var z = new int[env.MaxAgents]; var rw = new float[env.MaxAgents];
            env.Reset(0, 9); bool dn; int k = 0;
            do { env.Step(0, z, z, rw, 0, out dn, out _); } while (!dn && k++ < 400);
            return env.Info(0).Orders;
        }
        int o1 = Orders(0.1f), o3 = Orders(0.3f), o0 = Orders(0f);
        bool ctl = o1 > 20 && o3 < o1 * 0.6 && Math.Abs(o0 - o1) <= o1 * 0.1;
        return (rt && ctl, $"roundtrip {rt} (trailer 8 bytes, old format loads with 0); controller orders in 20 s: period 0.1 -> {o1}, 0.3 -> {o3}, unset -> {o0} (fallback 0.1)");
    }
}
