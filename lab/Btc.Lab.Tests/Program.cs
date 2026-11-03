using System.Diagnostics;
using Btc.RL;
using Btc.RL.Toy;
using Btc.Shared;

namespace Btc.Lab.Tests;

static class Program
{
    static int _fail;

    static void Check(string name, bool ok, string detail)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}: {detail}");
        if (!ok) _fail++;
    }

    static void Run(string name, Func<(bool, string)> f)
    {
        var sw = Stopwatch.StartNew();
        try { var (ok, d) = f(); Check(name, ok, $"{d} [{sw.Elapsed.TotalSeconds:F1}s]"); }
        catch (Exception ex) { Check(name, false, ex.GetType().Name + ": " + ex.Message); }
    }

    static int Main()
    {
        Run("gradcheck", () =>
        {
            var r = GradCheck.Run();
            return (r.ActorMaxRel < 1e-3 && r.CriticMaxRel < 1e-3 && r.ActorChecked > 100 && r.CriticChecked > 100,
                $"actor maxrel {r.ActorMaxRel:E2} ({r.ActorChecked}), critic maxrel {r.CriticMaxRel:E2} ({r.CriticChecked})");
        });
        Run("policy-roundtrip+equality", PolicyEquality);
        Run("ppo-bandit", Bandit);
        Run("toy-determinism", ToyDeterminism);
        Run("trainer-determinism", TrainerDeterminism);
        Run("split-critic-equality", SpeedTests.SplitCritic);
        Run("tile2-identical", SpeedTests.Tile2Identical);
        Run("opp-batch-equality", SpeedTests.OppBatch);
        Run("kl-adapt+ckpt-trailer", SpeedTests.KlAdapt);
        bool quick = Environment.GetCommandLineArgs().Contains("--quick");
        Run("sim-selftest", () =>
        {
            string rep = Btc.Sim.SimSelfTest.Run(null, false);
            string first = rep.Split('\n')[0];
            foreach (var l in rep.Split('\n')) if (l.StartsWith("  FAIL")) Console.WriteLine("    " + l.Trim());
            return (first.Contains("ALL OK"), first + " (log parity skipped)");
        });
        if (!quick)
            Run("bot-selftest", () =>
            {
                var f = Btc.Bots.BotSelfTest.Run();
                foreach (var l in f.Take(10)) Console.WriteLine("    " + l);
                return (f.Count == 0, f.Count == 0 ? "host + bots ok" : f.Count + " failures");
            });
        Run("duel-env-determinism", DuelEnvDeterminism);
        Run("duel-env-obs-masks", DuelEnvObsMasks);
        Run("duel-env-ppo-smoke", DuelEnvPpoSmoke);
        Run("v2-heal-tick", DuelV2Tests.Heal);
        Run("v2-obs-features-mirror", DuelV2Tests.ObsFeatures);
        Run("v2-actions-decode", DuelV2Tests.ActionsDecode);
        Run("v2-rewards", DuelV2Tests.Rewards);
        Run("v2-15v18-episodes", DuelV2Tests.Big15v18);
        Run("lz-draw-reward", DuelV2Tests.DrawReward);
        Run("lz-roles-passive", DuelV2Tests.RolesPassive);
        Run("lz-handicap-own", DuelV2Tests.HandicapOwnSide);
        Run("lz-stage-switch", DuelV2Tests.StageSwitch);
        Run("lz-policy-meta+controller", DuelV2Tests.PolicyMeta);
        Run("opp-mix-parse", LeagueTests.MixParse);
        Run("league-determinism+pfsp", LeagueTests.LeagueDeterminism);
        Run("dead-agent-credit-gae", LeagueTests.DeadCredit);
        Run("lumina-argmax-flag", LeagueTests.LuminaArgmax);
        Run("league-trainer-smoke", LeagueTests.LeagueTrainerSmoke);
        Console.WriteLine(_fail == 0 ? "ALL PASS" : $"{_fail} FAILED");
        return _fail == 0 ? 0 : 1;
    }

    static Btc.RL.Duel.SimDuelEnv MakeDuelEnv(int n, string units, string arena, params string[] opps)
    {
        var cur = new Btc.RL.Duel.DuelCurriculum { SkipsProb = 0.5f, HealProb = 0.3f, TimeLimit = 60f };
        cur.SetUnits(units); cur.SetArena(arena);
        return new Btc.RL.Duel.SimDuelEnv(n, cur, opps.Select(o => Btc.RL.Duel.DuelOpponents.Get(o)).ToArray());
    }

    static (bool, string) DuelEnvDeterminism()
    {
        var r1 = DuelEnvTests.Play(MakeDuelEnv(4, "1-3", "1", "prod2", "hunter", "ext", "prod4"), 7, 1500, false);
        var r2 = DuelEnvTests.Play(MakeDuelEnv(4, "1-3", "1", "prod2", "hunter", "ext", "prod4"), 7, 1500, true);
        var r3 = DuelEnvTests.Play(MakeDuelEnv(4, "1-3", "1", "prod2", "hunter", "ext", "prod4"), 8, 1500, false);
        bool ok = r1.hash == r2.hash && r1.outcomes == r2.outcomes && r3.hash != r1.hash;
        return (ok, $"seq vs parallel equal={r1.hash == r2.hash && r1.outcomes == r2.outcomes}, other seed differs={r3.hash != r1.hash}, episodes {r1.eps} [{r1.outcomes}]");
    }

    static (bool, string) DuelEnvObsMasks()
    {
        int bad = 0, badMask = 0, active = 0; float lo = 0, hi = 0;
        var env = MakeDuelEnv(4, "1,5,10", "1,3", "prod4", "x", "ext", "clump");
        var r = DuelEnvTests.Play(env, 3, 1000, true, (obs, mask, act) =>
        {
            for (int a = 0; a < act.Length; a++)
            {
                if (!act[a]) continue;
                System.Threading.Interlocked.Increment(ref active);
                bool any = false;
                for (int j = 0; j < Btc.Shared.DuelActions.Count; j++) if (mask[a * Btc.Shared.DuelActions.Count + j] != 0) { any = true; break; }
                if (!any) System.Threading.Interlocked.Increment(ref badMask);
                for (int j = 0; j < Btc.Shared.DuelObs.Size; j++)
                {
                    float x = obs[a * Btc.Shared.DuelObs.Size + j];
                    if (!float.IsFinite(x) || x < -6f || x > 6f) System.Threading.Interlocked.Increment(ref bad);
                    if (x < lo) lo = x; if (x > hi) hi = x;
                }
            }
        });
        return (bad == 0 && badMask == 0 && active > 0, $"active agent obs {active}, out-of-range {bad}, empty masks {badMask}, range [{lo:F2},{hi:F2}], episodes {r.eps}");
    }

    static (bool, string) DuelEnvPpoSmoke()
    {
        var env = MakeDuelEnv(8, "1-2", "1", "prod2", "hunter");
        var cfg = new PpoConfig { NumEnvs = 8, Steps = 32, Threads = 4, Seed = 3, Hidden = "32,32", CriticHidden = "32,32" };
        var tr = new PpoTrainer(env, env.Opps.Select(o => o.Spec).ToArray(), cfg);
        UpdateStats st = null;
        for (int i = 0; i < 2; i++) st = tr.Update();
        bool ok = st.Samples > 0 && double.IsFinite(st.Entropy) && double.IsFinite(st.VLoss);
        return (ok, $"2 updates samples={st.Samples} ent={st.Entropy:F3} vloss={st.VLoss:F4} eps={st.Episodes}");
    }

    static (bool, string) PolicyEquality()
    {
        var rng = new Rng(3);
        double worst = 0; bool same = true;
        foreach (var act in new[] { Act.Tanh, Act.Relu })
        {
            var net = new Mlp(46, new[] { 128, 96 }, 23, act);
            net.InitOrthogonal(rng, 1.4f, 0.5f);
            for (int i = 0; i < net.ParamCount; i++) net.P[i] += 0.01f * (float)rng.Gaussian();
            var pol = net.ToPolicy(new[] { 22 }, true, MlpPolicy.Fnv1a("test"));
            var bytes = pol.Save();
            var p2 = MlpPolicy.Load(bytes);
            same &= p2.Save().AsSpan().SequenceEqual(bytes) && p2.SchemaHash == MlpPolicy.Fnv1a("test") && p2.HasValue;
            var back = Mlp.FromPolicy(p2);
            same &= back.P.AsSpan().SequenceEqual(net.P);
            int rows = 37;
            var ws = new MlpWorkspace(net, rows);
            for (int i = 0; i < rows * 46; i++) ws.Input[i] = (float)rng.Gaussian();
            MlpOps.Forward(net, ws, rows);
            var obs = new float[46]; var o = new float[p2.OutputSize]; var scratch = new float[p2.ScratchSize];
            for (int r = 0; r < rows; r++)
            {
                Array.Copy(ws.Input, r * 46, obs, 0, 46);
                p2.Forward(obs, o, scratch);
                for (int j = 0; j < o.Length; j++) worst = Math.Max(worst, Math.Abs(o[j] - ws.Output[r * 23 + j]));
            }
        }
        return (same && worst < 1e-4, $"bytes/params equal={same}, max |scalar-batched| = {worst:E2}");
    }

    static (bool, string) Bandit()
    {
        var env = new BanditEnv(32, 2);
        var cfg = new PpoConfig { NumEnvs = 32, Steps = 16, Epochs = 4, Minibatches = 4, Gamma = 0.99f, Lr = 1e-3f, CriticLr = 1e-3f, Hidden = "32", CriticHidden = "32", Threads = 4, Seed = 5, EntCoef = 0.003f };
        var tr = new PpoTrainer(env, null, cfg);
        int maxUpdates = 60, reached = -1; double acc = 0;
        var obs = new float[BanditEnv.K]; var mask = new byte[7]; var acts = new int[2];
        for (int u = 1; u <= maxUpdates; u++)
        {
            tr.Update();
            var pol = tr.ExportPolicy();
            var outp = new float[pol.OutputSize]; var sc = new float[pol.ScratchSize];
            int good = 0, total = 0;
            for (int c = 0; c < BanditEnv.K; c++)
                for (int m = 0; m < 4; m++)
                {
                    if (m == BanditEnv.Target0(c)) continue;
                    Array.Clear(obs); obs[c] = 1;
                    for (int j = 0; j < 7; j++) mask[j] = 1; mask[m] = 0;
                    pol.Forward(obs, outp, sc); pol.ArgmaxPerHead(outp, mask, acts);
                    total++; if (acts[0] == BanditEnv.Target0(c) && acts[1] == BanditEnv.Target1(c)) good++;
                }
            acc = (double)good / total;
            if (acc > 0.95) { reached = u; break; }
        }
        return (reached > 0, reached > 0 ? $"greedy optimal {acc:P0} after {reached} updates (limit {maxUpdates})" : $"only {acc:P0} after {maxUpdates} updates");
    }

    static (bool, string) ToyDeterminism()
    {
        ulong Play(ulong seed, int units)
        {
            var g = new ToyDuelGame(units);
            g.Reset(seed);
            var r = new Rng(seed ^ 0xABCDEF);
            var a0 = new int[units]; var a1 = new int[units];
            ulong h = 0; int steps = 0;
            while (!g.Done && steps < 2000)
            {
                for (int i = 0; i < units; i++)
                {
                    // biased random play so episodes actually reach fights and rule endings
                    a0[i] = r.NextFloat() < 0.6f ? ToyDuelGame.ActAttack0 : r.Next(ToyDuelGame.NumActions);
                    a1[i] = r.NextFloat() < 0.6f ? ToyDuelGame.ActAttack0 : r.Next(ToyDuelGame.NumActions);
                }
                g.Step(a0, a1); steps++;
                h = h * 31 + g.StateHash();
            }
            return h ^ (ulong)g.Outcome ^ ((ulong)steps << 40);
        }
        bool ok = true; var outcomes = new HashSet<ulong>();
        for (int u = 1; u <= 3; u++)
            for (ulong s = 1; s <= 5; s++)
            {
                ulong a = Play(s, u), b = Play(s, u);
                ok &= a == b; outcomes.Add(a);
            }
        return (ok && outcomes.Count == 15, $"15 seeded episodes replayed identically={ok}, distinct={outcomes.Count}");
    }

    static (bool, string) TrainerDeterminism()
    {
        float[] Train()
        {
            var env = new ToyDuelBatch(8, 2);
            var cfg = new PpoConfig { NumEnvs = 8, Steps = 32, Threads = 4, Seed = 9, Hidden = "32,32", CriticHidden = "32,32" };
            var tr = new PpoTrainer(env, new[] { ToyBots.Spec("rusher"), ToyBots.Spec("random") }, cfg);
            for (int i = 0; i < 3; i++) tr.Update();
            return tr.Actor.P;
        }
        var a = Train(); var b = Train();
        bool ok = a.AsSpan().SequenceEqual(b);
        return (ok, $"two 3-update 2v2 runs bit-identical={ok}");
    }
}

static class DuelEnvTests
{
    /// <summary>Random legal play (biased toward attacks/sweeps so fights happen). Returns per-env (reward hash, outcomes, steps).</summary>
    public static (ulong hash, string outcomes, long steps, int eps) Play(Btc.RL.Duel.SimDuelEnv env, ulong seed, int stepsPerEnv, bool parallel,
        Action<float[], byte[], bool[]> check = null)
    {
        int N = env.NumEnvs, M = env.MaxAgents, O = env.ObsSize, C = env.CentralSize, L = Btc.Shared.DuelActions.Count;
        var hashes = new ulong[N]; var outs = new string[N]; var epsN = new int[N];
        void One(int e)
        {
            var obs = new float[M * O]; var mask = new byte[M * L]; var cen = new float[M * C]; var act = new bool[M];
            var obs1 = new float[M * O]; var mask1 = new byte[M * L]; var cen1 = new float[M * C]; var act1 = new bool[M];
            var a0 = new int[M]; var a1 = new int[M]; var rew = new float[M];
            var rng = new Rng(Rng.Mix(seed, (ulong)e));
            env.Reset(e, Rng.Mix(seed, 1000 + (ulong)e));
            ulong h = 14695981039346656037UL; var sb = new System.Text.StringBuilder();
            for (int t = 0; t < stepsPerEnv; t++)
            {
                env.Observe(e, 0, obs, 0, mask, 0, cen, 0, act, 0);
                env.Observe(e, 1, obs1, 0, mask1, 0, cen1, 0, act1, 0);
                check?.Invoke(obs, mask, act);
                Pick(rng, mask, act, a0); Pick(rng, mask1, act1, a1);
                env.Step(e, a0, a1, rew, 0, out bool done, out int outcome);
                for (int a = 0; a < M; a++) { h ^= BitConverter.SingleToUInt32Bits(rew[a]); h *= 1099511628211UL; }
                if (done) { sb.Append(outcome > 0 ? 'W' : outcome < 0 ? 'L' : 'D'); epsN[e]++; env.Reset(e, rng.NextU64()); }
            }
            hashes[e] = h; outs[e] = sb.ToString();
        }
        if (parallel) Parallel.For(0, N, One); else for (int e = 0; e < N; e++) One(e);
        ulong hh = 0; foreach (var x in hashes) hh = hh * 31 + x;
        return (hh, string.Join("|", outs), (long)N * stepsPerEnv, epsN.Sum());
    }

    static void Pick(Rng rng, byte[] mask, bool[] act, int[] a)
    {
        int L = Btc.Shared.DuelActions.Count;
        for (int i = 0; i < act.Length; i++)
        {
            a[i] = 0;
            if (!act[i]) continue;
            float u = rng.NextFloat();
            int want = u < 0.5f ? 0 : u < 0.7f ? Btc.Shared.DuelActions.AttackSlot + rng.Next(6) : u < 0.85f ? Btc.Shared.DuelActions.Sweep + rng.Next(8) : rng.Next(L);
            if (mask[i * L + want] != 0) a[i] = want;
        }
    }
}

static class LeagueTests
{
    static MlpPolicy RandPolicy(ulong seed, int inSize = 6, int outSize = 3)
    {
        var net = new Mlp(inSize, new[] { 8 }, outSize, Act.Tanh);
        net.InitOrthogonal(new Rng(seed), 1.4f, 1f);
        return net.ToPolicy(new[] { outSize }, false, MlpPolicy.Fnv1a("t"));
    }

    static MlpPolicy DuelPolicy(ulong seed)
    {
        var net = new Mlp(DuelObs.Size, new[] { 16 }, DuelActions.Count, Act.Tanh);
        net.InitOrthogonal(new Rng(seed), 1.4f, 0.05f);   // near-uniform logits: sampling wanders, argmax does not
        return net.ToPolicy(new[] { DuelActions.Count }, false, MlpPolicy.Fnv1a(DuelObs.Schema));
    }

    public static (bool, string) MixParse()
    {
        var (n, w) = Btc.RL.Duel.DuelOpponents.ParseMix("prod4:3,prod2:1, v2 ,pool:4,lumina:C:\\p\\a.btcpol,lumina:C:\\p\\b.btcpol:2.5");
        bool ok = n.SequenceEqual(new[] { "prod4", "prod2", "v2", "pool", "lumina:C:\\p\\a.btcpol", "lumina:C:\\p\\b.btcpol" })
               && w.SequenceEqual(new[] { 3f, 1f, 1f, 4f, 1f, 2.5f });
        bool threw = false;
        try { Btc.RL.Duel.DuelOpponents.ParseMix("prod4:-1"); } catch (ArgumentException) { threw = true; }
        var (n2, w2) = Btc.RL.Duel.DuelOpponents.ParseMix("prod4:3,prod2:1,v2:1,hunter:1,sweeper:1,baiter:1,clump:1,pool:4");
        ok &= threw && n2.Length == 8 && w2.Sum() == 13f;
        return (ok, $"names [{string.Join("|", n)}] weights [{string.Join(",", w)}] negative rejected={threw} preset {n2.Length} opps sum {w2.Sum()}");
    }

    public static (bool, string) LeagueDeterminism()
    {
        Btc.RL.Duel.DuelLeague Make()
        {
            var lg = new Btc.RL.Duel.DuelLeague(new[] { "prod4", "pool" }, new[] { 1f, 3f }, 4) { Keep = 3 };
            for (int i = 0; i < 5; i++) lg.Add(RandPolicy((ulong)i), i * 10);
            lg.Add(RandPolicy(99), 55, best: true);
            lg.Add(RandPolicy(100), 60);
            return lg;
        }
        var a = Make(); var b = Make();
        bool evict = a.Count == 4 && a.Snaps.Count(s => s.Best) == 1 && a.Snaps.Where(s => !s.Best).Select(s => s.Update).SequenceEqual(new[] { 30, 40, 60 });
        // results: the learner always beats the u30 snapshot and always loses to the u60 one
        void Feed(Btc.RL.Duel.DuelLeague lg)
        {
            lg.BeginRollout();
            var r = new Rng(5);
            for (int k = 0; k < 400; k++)
            {
                int e = k % 4;
                int opp = lg.Pick(e, r);
                if (opp != lg.PoolIndex) continue;
                var s = lg.SnapOf(e);
                lg.Report(e, opp, s.Update == 30 ? 1 : s.Update == 60 ? -1 : 0);
            }
        }
        Feed(a); Feed(b);
        a.BeginRollout(); b.BeginRollout();
        var ra = new Rng(11); var rb = new Rng(11);
        var seqA = new List<string>(); var seqB = new List<string>();
        var counts = new Dictionary<int, int>();
        int pool = 0;
        for (int k = 0; k < 4000; k++)
        {
            int e = k % 4;
            int oa = a.Pick(e, ra), ob = b.Pick(e, rb);
            seqA.Add(oa + ":" + (a.SnapOf(e)?.Id ?? 0)); seqB.Add(ob + ":" + (b.SnapOf(e)?.Id ?? 0));
            if (oa == a.PoolIndex) { pool++; int u = a.SnapOf(e).Update; counts[u] = counts.GetValueOrDefault(u) + 1; }
        }
        bool same = seqA.SequenceEqual(seqB);
        double poolShare = pool / 4000.0;
        bool pfsp = counts.GetValueOrDefault(60) > 3 * counts.GetValueOrDefault(30);
        var ms = new MemoryStream(); using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) a.Write(w);
        ms.Position = 0; var c = new Btc.RL.Duel.DuelLeague(new[] { "prod4", "pool" }, new[] { 1f, 3f }, 4) { Keep = 3 };
        using (var r = new BinaryReader(ms)) c.Read(r);
        bool rt = c.Count == a.Count && c.Snaps.Zip(a.Snaps).All(p => p.First.Id == p.Second.Id && p.First.Games == p.Second.Games && p.First.WinsX2 == p.Second.WinsX2 && p.First.Best == p.Second.Best && p.First.Bytes.AsSpan().SequenceEqual(p.Second.Bytes));
        bool ok = evict && same && pfsp && Math.Abs(poolShare - 0.75) < 0.04 && rt;
        return (ok, $"evict(keep 3 + best)={evict} same-seq={same} pool share {poolShare:F3} (want .75) picks u30(beaten) {counts.GetValueOrDefault(30)} u60(loses to) {counts.GetValueOrDefault(60)} pfsp={pfsp} roundtrip={rt} [{a.Describe()}]");
    }

    public static (bool, string) DeadCredit()
    {
        // 1 env, 2 agents, T = 6. Agent 0 lives to the end; agent 1 dies after its transition at t = 1.
        // The env writes the team reward into every slot: terminal +1 at t = 5 (done), shaping 0.1 at t = 3.
        int T = 6, N = 1, M = 2; float g = 0.9f;
        float[] Rew() { var r = new float[T * M]; for (int a = 0; a < M; a++) { r[3 * M + a] = 0.1f; r[5 * M + a] = 1f; } r[1 * M + 1] += -0.05f; return r; }
        var active = new bool[T * M]; for (int t = 0; t < T; t++) { active[t * M + 0] = true; active[t * M + 1] = t <= 1; }
        var done = new bool[T]; done[5] = true;
        var lastA = new bool[M]; var lastV = new float[M];
        var rew = Rew();
        PpoTrainer.CreditDeadAgents(rew, active, done, lastA, lastV, T, N, M, g);
        // death transition t = 1: own -0.05 + g^2 * 0.1 (t = 3) + g^4 * 1 (t = 5)
        float want1 = -0.05f + g * g * 0.1f + g * g * g * g * 1f;
        bool c1 = Math.Abs(rew[1 * M + 1] - want1) < 1e-6f && Math.Abs(rew[0 * M + 1]) < 1e-9f && Math.Abs(rew[5 * M + 0] - 1f) < 1e-9f;
        var val = new float[T * M]; var adv = new float[T * M]; var ret = new float[T * M];
        PpoTrainer.Gae(rew, val, active, done, lastV, lastA, adv, ret, T, N, M, g, 1f);
        // lambda 1, V = 0: agent 1's return at t = 0 = g * r(t=1); agent 0's = g^3 * .1 + g^5 (both see the same team outcome)
        bool c2 = Math.Abs(ret[0 * M + 1] - g * want1) < 1e-6f && Math.Abs(ret[0 * M + 0] - (g * g * g * 0.1f + g * g * g * g * g)) < 1e-6f;
        // without the credit the dead agent never sees the win
        var raw = Rew(); PpoTrainer.Gae(raw, val, active, done, lastV, lastA, adv, ret, T, N, M, g, 1f);
        bool c3 = ret[0 * M + 1] < 0f;
        // rollout ends before the episode: bootstrap with the live teammate's value 0.5 at T -> g^(6-1) * 0.5
        var r2 = new float[T * M]; var done2 = new bool[T]; var lastA2 = new[] { true, false }; var lastV2 = new[] { 0.5f, 0f };
        PpoTrainer.CreditDeadAgents(r2, active, done2, lastA2, lastV2, T, N, M, g);
        float want2 = MathF.Pow(g, 5) * 0.5f;
        bool c4 = Math.Abs(r2[1 * M + 1] - want2) < 1e-6f;
        // done closes the window: agent 1 (alive to t = 2 = done) gets nothing from the next episode
        var r3 = new float[T * M]; for (int a = 0; a < M; a++) r3[4 * M + a] = 1f;
        var act3 = new bool[T * M]; for (int t = 0; t < T; t++) { act3[t * M] = true; act3[t * M + 1] = t <= 2; }
        var done3 = new bool[T]; done3[2] = true;
        PpoTrainer.CreditDeadAgents(r3, act3, done3, new[] { true, false }, new[] { 0f, 0f }, T, N, M, g);
        bool c5 = Math.Abs(r3[2 * M + 1]) < 1e-9f;
        bool ok = c1 && c2 && c3 && c4 && c5;
        return (ok, $"death credit {rew[1 * M + 1]:F5} (want {want1:F5})={c1} gae={c2} no-credit-negative={c3} bootstrap {r2[1 * M + 1]:F5} (want {want2:F5})={c4} done-closes={c5}");
    }

    public static (bool, string) LuminaArgmax()
    {
        var pol = DuelPolicy(7);
        var dArg = Btc.RL.Duel.DuelOpponents.Lumina(pol, true);
        var dSam = Btc.RL.Duel.DuelOpponents.Lumina(pol, false);
        dArg.Bot(); bool t1 = Btc.RL.Duel.DuelOpponents.LuminaArgmax;
        bool other = true; var th = new Thread(() => other = Btc.RL.Duel.DuelOpponents.LuminaArgmax); th.Start(); th.Join();
        dSam.Bot(); bool t2 = Btc.RL.Duel.DuelOpponents.LuminaArgmax;
        // the flag reaches the controller on the worker threads: same seeds, argmax vs sampling -> different play
        var cur = new Btc.RL.Duel.DuelCurriculum { SkipsProb = 1, HealProb = 0, FpsMin = 60, FpsMax = 60, DomainRand = 0, TimeLimit = 60 };
        cur.SetUnits("2");
        var opp = Btc.RL.Duel.DuelOpponents.Get("prod2");
        var sA = Btc.RL.Duel.DuelMatchRunner.Run(dArg, opp, cur, 4, 2, 5);
        var sA2 = Btc.RL.Duel.DuelMatchRunner.Run(dArg, opp, cur, 4, 2, 5);
        var sS = Btc.RL.Duel.DuelMatchRunner.Run(dSam, opp, cur, 4, 2, 5);
        bool repro = sA.Len == sA2.Len && sA.W == sA2.W;
        // a near-random policy loses the same way either way, so compare the orders the controller issued in one episode
        string Orders(Btc.RL.Duel.DuelOppDef d)
        {
            var env = new Btc.RL.Duel.SimDuelEnv(1, cur, new[] { opp }, d);
            var z = new int[env.MaxAgents]; var rw = new float[env.MaxAgents];
            env.Reset(0, 99); bool dn; int k = 0;
            do { env.Step(0, z, z, rw, 0, out dn, out _); k++; } while (!dn && k < 300);
            var inf = env.Info(0); var sim = env.Host(0).Sim;
            return inf.Orders + "/" + inf.FightTime.ToString("F2") + "@" + sim.X[0].ToString("F3") + "," + sim.Z[0].ToString("F3");
        }
        string oA = Orders(dArg), oA2 = Orders(dArg), oS = Orders(dSam);
        repro &= oA == oA2;
        bool differ = oA != oS;
        bool ok = t1 && !other && !t2 && repro && differ;
        return (ok, $"argmax spec sets flag={t1}, other thread untouched={!other}, sample spec clears={!t2}, argmax reproducible={repro}, argmax vs sample differ={differ} (orders/len {oA} vs {oS})");
    }

    public static (bool, string) LeagueTrainerSmoke()
    {
        var cur = new Btc.RL.Duel.DuelCurriculum { SkipsProb = 0.5f, HealProb = 0.3f, TimeLimit = 60f, OrderCost = 0.002f };
        var stage = Btc.RL.Duel.DuelEval.Stage(cur, 2, 0.2f);
        var (names, w) = Btc.RL.Duel.DuelOpponents.ParseMix("prod2:1,hunter:1,pool:2");
        float[] Train()
        {
            var lg = new Btc.RL.Duel.DuelLeague(names, w, 8) { Keep = 2 };
            var defs = names.Select(n => n == "pool"
                ? new Btc.RL.Duel.DuelOppDef { Name = "pool", Bot = null, Create = () => new PolicyOpponent(e => lg.PolicyFor(e), true) }
                : Btc.RL.Duel.DuelOpponents.Get(n)).ToArray();
            var env = new Btc.RL.Duel.SimDuelEnv(8, stage, defs);
            var cfg = new PpoConfig { NumEnvs = 8, Steps = 32, Threads = 4, Seed = 3, Hidden = "32,32", CriticHidden = "32,32", EntEnd = 0.001f, EntAnneal = 1000 };
            var tr = new PpoTrainer(env, defs.Select(d => d.Spec).ToArray(), cfg, lg);
            lg.Add(tr.ExportPolicy(), 0); tr.ResetAll(1);
            for (int i = 0; i < 3; i++) { tr.Update(); lg.Add(tr.ExportPolicy(), tr.UpdateCount); }
            return tr.Actor.P;
        }
        var a = Train(); var b = Train();
        bool ok = a.AsSpan().SequenceEqual(b);
        return (ok, $"3 updates with league pool + stage-2 curriculum (earlier 0.2) + order cost bit-identical={ok}");
    }
}
