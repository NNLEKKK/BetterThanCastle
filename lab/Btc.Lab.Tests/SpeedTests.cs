using Btc.RL;
using Btc.RL.Duel;
using Btc.Shared;

namespace Btc.Lab.Tests;

/// <summary>Training-speed changes: split critic first layer, batched opponent inference, KL-adaptive lr, checkpoint trailer.</summary>
static class SpeedTests
{
    /// <summary>W*[obs ++ central] (concatenated) must equal W_obs*obs + W_cen*central[group]: outputs and every gradient.</summary>
    public static (bool, string) SplitCritic()
    {
        var rng = new Rng(11);
        double worstF = 0, worstG = 0, gMax = 0;
        foreach (var act in new[] { Act.Tanh, Act.Relu })
        {
            int O = 37, C = 29, CI = O + C, rows = 53, G = 11;
            var net = new Mlp(CI, new[] { 40, 24 }, 1, act);
            net.InitOrthogonal(rng, 1.4f, 1f);
            for (int i = 0; i < net.ParamCount; i++) net.P[i] += 0.05f * (float)rng.Gaussian();
            var cen = new float[G * C]; for (int i = 0; i < cen.Length; i++) cen[i] = (float)rng.Gaussian();
            var obs = new float[rows * O]; for (int i = 0; i < obs.Length; i++) obs[i] = (float)rng.Gaussian();
            var grp = new int[rows]; for (int r = 0; r < rows; r++) grp[r] = rng.Next(G);   // unsorted, some groups may be unused
            var dv = new float[rows]; for (int r = 0; r < rows; r++) dv[r] = (float)rng.Gaussian();

            var wp = new MlpWorkspace(net, rows);
            for (int r = 0; r < rows; r++)
            {
                Array.Copy(obs, r * O, wp.Input, r * CI, O);
                Array.Copy(cen, grp[r] * C, wp.Input, r * CI + O, C);
            }
            var ws = new MlpWorkspace(net, rows);
            ws.EnableSplit(O, C);
            Array.Copy(obs, ws.Input, rows * O);
            Array.Copy(cen, ws.Cb.Cen, G * C);
            Array.Copy(grp, ws.Cb.Grp, rows);
            ws.Cb.G = G;
            MlpOps.Forward(net, wp, rows); MlpOps.Forward(net, ws, rows);
            for (int r = 0; r < rows; r++) worstF = Math.Max(worstF, Math.Abs(wp.Output[r] - ws.Output[r]));
            Array.Copy(dv, wp.OutGrad, rows); Array.Copy(dv, ws.OutGrad, rows);
            MlpOps.Backward(net, wp, rows); MlpOps.Backward(net, ws, rows);
            for (int i = 0; i < net.ParamCount; i++) { worstG = Math.Max(worstG, Math.Abs(wp.Grad[i] - ws.Grad[i])); gMax = Math.Max(gMax, Math.Abs(wp.Grad[i])); }
        }
        return (worstF < 1e-4 && worstG < 1e-4 * Math.Max(1, gMax), $"max |dout| {worstF:E2}, max |dgrad| {worstG:E2} (grad scale {gMax:E1})");
    }

    /// <summary>Row tiles (4 / 2 / 1) of LinearForward give bit-identical rows regardless of the batch size.</summary>
    public static (bool, string) Tile2Identical()
    {
        var rng = new Rng(5);
        var net = new Mlp(549, new[] { 256, 256 }, 69, Act.Tanh);
        net.InitOrthogonal(rng, 1.4f, 1f);
        int rows = 11;
        var a = new MlpWorkspace(net, rows); var b = new MlpWorkspace(net, rows);
        for (int i = 0; i < rows * 549; i++) a.Input[i] = b.Input[i] = (float)rng.Gaussian();
        MlpOps.Tile2 = false; MlpOps.Forward(net, a, rows);
        MlpOps.Tile2 = true; MlpOps.Forward(net, b, rows);
        bool same = true;
        for (int i = 0; i < rows * 69; i++) same &= a.Output[i] == b.Output[i];
        return (same, "outputs bit-identical with the 2-row tile");
    }

    /// <summary>OppBatcher (argmax) picks the same actions as the per-env PolicyOpponent; envs with different policies are mixed.</summary>
    public static (bool, string) OppBatch()
    {
        var cur = new DuelCurriculum { SkipsProb = 1f, HealProb = 0f, TimeLimit = 40f, DomainRand = 0f };
        cur.SetUnits("6"); cur.SetArena("1");
        var refDef = new PolicyRef(null);
        var def = new DuelOppDef { Name = "p", Bot = null, Create = () => new PolicyOpponent(refDef, false) };
        int N = 4;
        var env = new SimDuelEnv(N, cur, new[] { def });
        var rng = new Rng(3);
        var pols = new MlpPolicy[2];
        for (int k = 0; k < 2; k++)
        {
            var net = new Mlp(env.ObsSize, new[] { 64, 64 }, env.HeadSizes.Sum(), Act.Tanh);
            net.InitOrthogonal(rng, 1.4f, 0.5f);
            pols[k] = net.ToPolicy(env.HeadSizes, false, MlpPolicy.Fnv1a(env.Schema));
        }
        for (int e = 0; e < N; e++) env.Reset(e, (ulong)(100 + e));
        int MH = env.MaxAgents * env.HeadSizes.Length;
        var batcher = new OppBatcher(env, N);
        var envs = new[] { 0, 1, 2, 3 }; var pe = new[] { pols[0], pols[1], pols[0], pols[1] }; var smp = new bool[N];
        var got = new int[N * MH]; batcher.Run(env, N, envs, pe, smp, new Rng(1), got);
        var one = new PolicyOpponent(refDef, false); var exp = new int[MH];
        int diff = 0, live = 0;
        for (int e = 0; e < N; e++)
        {
            refDef.Policy = pe[e];
            one.Act(env, e, 1, new Rng(1), exp);
            for (int i = 0; i < MH; i++) { if (exp[i] != got[e * MH + i]) diff++; if (exp[i] != 0) live++; }
        }
        return (diff == 0 && live > 0, $"{N} envs / 2 policies: {diff} differing action heads, {live} non-zero heads");
    }

    /// <summary>The adaptive lr multiplier moves 1.5x per update, clamps to [min,max], and survives a checkpoint round trip (and old files load).</summary>
    public static (bool, string) KlAdapt()
    {
        var env = new Btc.RL.Toy.BanditEnv(8, 2);
        var cfg = new PpoConfig { NumEnvs = 8, Steps = 8, Epochs = 2, Minibatches = 2, Lr = 1e-4f, CriticLr = 1e-4f, Hidden = "16", CriticHidden = "16", Threads = 2, Seed = 5, KlAdapt = 1e-9f, KlAdaptMin = 0.25f, KlAdaptMax = 4f, TargetKl = 0 };
        var tr = new PpoTrainer(env, null, cfg);
        double m0 = tr.LrMult;
        for (int i = 0; i < 8; i++) tr.Update();            // KL ~ 1e-4 > 2e-9: lr shrinks to the floor
        bool floor = Math.Abs(tr.LrMult - 0.25f) < 1e-6 && m0 == 1.0;
        string p = Path.Combine(Path.GetTempPath(), "btc-kladapt-" + Environment.ProcessId + ".bin");
        string p2 = p + ".old";
        try
        {
            tr.SaveCheckpoint(p);
            var tr2 = new PpoTrainer(new Btc.RL.Toy.BanditEnv(8, 2), null, PpoTrainer.ReadCheckpointConfig(p));
            tr2.LoadCheckpoint(p);
            bool rt = Math.Abs(tr2.LrMult - tr.LrMult) < 1e-7;
            cfg.KlAdapt = 0f;                                  // checkpoint without trailer (what old binaries write)
            var tr3 = new PpoTrainer(new Btc.RL.Toy.BanditEnv(8, 2), null, cfg); tr3.Update(); tr3.SaveCheckpoint(p2);
            var tr4 = new PpoTrainer(new Btc.RL.Toy.BanditEnv(8, 2), null, cfg); tr4.LoadCheckpoint(p2);
            bool old = tr4.LrMult == 1f && tr4.UpdateCount == 1;
            return (floor && rt && old, $"floor {tr.LrMult:F2} ok={floor}, roundtrip {tr2.LrMult:F2} ok={rt}, no-trailer file loads ok={old}");
        }
        finally { try { File.Delete(p); File.Delete(p2); } catch { } }
    }
}
