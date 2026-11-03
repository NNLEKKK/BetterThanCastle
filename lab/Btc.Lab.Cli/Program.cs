using System.Diagnostics;
using System.Globalization;
using System.Text;
using Btc.RL;
using Btc.RL.Toy;
using Btc.Shared;

namespace Btc.Lab.Cli;

/// <summary>--key value / --flag parser. Unknown keys stay available for PpoConfig overrides.</summary>
sealed class Args
{
    public readonly string Command;
    readonly Dictionary<string, string> _kv = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);

    public Args(string[] a)
    {
        Command = a.Length > 0 ? a[0] : "help";
        for (int i = 1; i < a.Length; i++)
        {
            if (!a[i].StartsWith("--")) throw new ArgumentException("unexpected argument: " + a[i]);
            string k = a[i][2..];
            if (i + 1 < a.Length && !a[i + 1].StartsWith("--")) _kv[k] = a[++i]; else _kv[k] = "true";
        }
    }

    public bool Has(string k) { _used.Add(k); return _kv.ContainsKey(k); }
    public string Str(string k, string def) { _used.Add(k); return _kv.TryGetValue(k, out var v) ? v : def; }
    public int Int(string k, int def) => int.Parse(Str(k, def.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
    public long Long(string k, long def) => (long)double.Parse(Str(k, def.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
    public double Dbl(string k, double def) => double.Parse(Str(k, def.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
    public bool Flag(string k) => Str(k, "false") != "false";
    public IEnumerable<KeyValuePair<string, string>> Unused => _kv.Where(p => !_used.Contains(p.Key));
}

static class Program
{
    static int Main(string[] argv)
    {
        try
        {
            var a = new Args(argv);
            switch (a.Command)
            {
                case "gradcheck": return GradCheckCmd(a);
                case "bench-mlp": return BenchMlp(a);
                case "gpu-check": return DuelCommands.GpuCheck(a);
                case "toy-train": return ToyTrain(a);
                case "toy-eval": return ToyEval(a);
                case "export": return Export(a);
                case "pol-migrate": return PolMigrate(a);
                case "train": return DuelCommands.Train(a, LabRoot());
                case "bench-train": return DuelCommands.BenchTrain(a, LabRoot());
                case "eval": return DuelCommands.Eval(a);
                case "match": return DuelCommands.Match(a);
                case "fidelity": return FidelityCommand.Run(argv);
                case "bench": return DuelCommands.Bench(a);
                case "view": return DuelCommands.View(a);
                case "reorder-lab": return ReorderLab.Run(a.Int("set", 1));
                case "trace-unit":
                {
                    var L = Btc.Sim.LogReplay.Parse(a.Str("log", ""));
                    int id = a.Int("unit", 0); float sec = (float)a.Dbl("sec", 10);
                    Console.WriteLine($"log {Path.GetFileName(L.Path)} unit u{id}: t  game(x,z)  sim(x,z)  dist  (arena-local)");
                    var sp = Btc.Sim.SimParams.Default();
                    if (a.Has("busy")) sp.BusyOrderSec = (float)a.Dbl("busy", 0.38); if (a.Has("win")) sp.BusyWindowSec = (float)a.Dbl("win", 0.5); if (a.Has("busyoff")) sp.BusyOrders = false;
                    int fromSnap = 0; if (a.Has("from")) { float ft = (float)a.Dbl("from", 0); while (fromSnap < L.Snaps.Count - 1 && L.Snaps[fromSnap].T < ft) fromSnap++; }
                    var tr = Btc.Sim.LogReplay.TraceUnit(L, id, sec, fromSnap, sp);
                    double gl = 0, sl = 0;
                    for (int i = 1; i < tr.Count; i++) { gl += Math.Sqrt(Math.Pow(tr[i].gx - tr[i - 1].gx, 2) + Math.Pow(tr[i].gz - tr[i - 1].gz, 2)); sl += Math.Sqrt(Math.Pow(tr[i].sx - tr[i - 1].sx, 2) + Math.Pow(tr[i].sz - tr[i - 1].sz, 2)); }
                    if (a.Flag("summary")) { Console.WriteLine(FormattableString.Invariant($"u{id}: path game {gl:F1} m, sim {sl:F1} m over {sec:F0} s")); return 0; }
                    foreach (var r in tr)
                        Console.WriteLine(FormattableString.Invariant($"{r.t,6:F2}  {r.gx,6:F1},{r.gz,5:F1}   {r.sx,6:F1},{r.sz,5:F1}   {MathF.Sqrt((r.gx - r.sx) * (r.gx - r.sx) + (r.gz - r.sz) * (r.gz - r.sz)),5:F1}"));
                    return 0;
                }
                default:
                    Console.WriteLine("btclab gradcheck | bench-mlp [--batch 4096 --threads T --sec 3] | toy-train --opp rusher[,hunter,random,self] --units 1 --steps N --threads T --run name [--minutes M --target-wr 0.9 --print 10 --ckpt-every 50 --snap 20 --resume --<PpoConfig field> v] | toy-eval --policy p.btcpol --opp X --n 1000 [--units 1 --sample] | export --ckpt file [--out p.btcpol]\nduel (simulator):\n" + DuelCommands.Help);
                    return a.Command == "help" ? 0 : 2;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR " + ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    static void LowPriority()
    {
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
    }

    static string LabRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var d = new DirectoryInfo(start);
            while (d != null)
            {
                if (File.Exists(Path.Combine(d.FullName, "Btc.Lab.sln"))) return d.FullName;
                if (File.Exists(Path.Combine(d.FullName, "lab", "Btc.Lab.sln"))) return Path.Combine(d.FullName, "lab");
                d = d.Parent;
            }
        }
        return Directory.GetCurrentDirectory();
    }

    static int GradCheckCmd(Args a)
    {
        var r = GradCheck.Run((ulong)a.Long("seed", 7), a.Dbl("eps", 1e-5), a.Dbl("minmag", 1e-4));
        bool ok = r.ActorMaxRel < 1e-3 && r.CriticMaxRel < 1e-3;
        Console.WriteLine($"gradcheck actor maxrel={r.ActorMaxRel:E2} ({r.ActorChecked} params) critic maxrel={r.CriticMaxRel:E2} ({r.CriticChecked} params) {(ok ? "PASS" : "FAIL")}");
        return ok ? 0 : 1;
    }

    static int BenchMlp(Args a)
    {
        LowPriority();
        int batch = a.Int("batch", 4096), threads = a.Int("threads", Environment.ProcessorCount);
        double sec = a.Dbl("sec", 3);
        int inSize = a.Int("in", 64), outSize = a.Int("out", 32);
        MlpOps.Tile2 = a.Int("tile2", 1) != 0;
        if (a.Flag("kernels")) return BenchKernels(a.Int("rows", 600), threads, sec);
        Console.WriteLine($"bench-mlp batch={batch} threads={threads} net={inSize}-H-H-{outSize} tanh avx2={System.Runtime.Intrinsics.X86.Avx2.IsSupported} fma={System.Runtime.Intrinsics.X86.Fma.IsSupported}");
        foreach (int hdim in new[] { 128, 256 })
        {
            var net = new Mlp(inSize, new[] { hdim, hdim }, outSize, Act.Tanh);
            net.InitOrthogonal(new Rng(1), 1.4f, 1f);
            int shard = (batch + threads - 1) / threads;
            var ws = new MlpWorkspace[threads];
            var rng = new Rng(2);
            for (int t = 0; t < threads; t++)
            {
                ws[t] = new MlpWorkspace(net, shard);
                for (int i = 0; i < shard * inSize; i++) ws[t].Input[i] = (float)rng.Gaussian();
                for (int i = 0; i < shard * outSize; i++) ws[t].OutGrad[i] = (float)rng.Gaussian() * 1e-3f;
            }
            var po = new ParallelOptions { MaxDegreeOfParallelism = threads };
            double Run(bool bw)
            {
                long n = 0; var sw = Stopwatch.StartNew();
                int iters = 0;
                while (sw.Elapsed.TotalSeconds < sec || iters < 3)
                {
                    Parallel.For(0, threads, po, t =>
                    {
                        int rows = Math.Min(shard, batch - t * shard);
                        if (rows <= 0) return;
                        MlpOps.Forward(net, ws[t], rows);
                        if (bw) MlpOps.Backward(net, ws[t], rows);
                    });
                    n += batch; iters++;
                }
                return n / sw.Elapsed.TotalSeconds;
            }
            Run(true); // warm-up (JIT tiering)
            double f = Run(false), fb = Run(true);
            long macs = (long)inSize * hdim + (long)hdim * hdim + (long)hdim * outSize;
            Console.WriteLine($"  H={hdim}: fwd {f / 1e6:F2} M samples/s ({f * macs * 2 / 1e9:F0} GFLOP/s), fwd+bwd {fb / 1e6:F2} M samples/s ({fb * macs * 6 / 1e9:F0} GFLOP/s)");
        }
        return 0;
    }

    /// <summary>MlpOps (blocked) vs MlpOpsRef (simple) on the LuminaZero layer shapes: max abs diff and GFLOP/s, `threads` copies in parallel.</summary>
    static unsafe int BenchKernels(int rows, int threads, double sec)
    {
        var shapes = new[] { (549, 256), (1101, 256), (256, 256), (256, 69), (256, 1) };
        Console.WriteLine($"bench-mlp --kernels rows={rows} threads={threads}");
        var po = new ParallelOptions { MaxDegreeOfParallelism = threads };
        foreach (var (nIn, nOut) in shapes)
        {
            var rng = new Rng(5);
            float[] Rand(int n, float s) { var v = new float[n + 16]; for (int i = 0; i < n; i++) v[i] = (float)rng.Gaussian() * s; return v; }
            float[] X = Rand(rows * nIn, 1f), W = Rand(nOut * nIn, 0.05f), B = Rand(nOut, 0.1f), dY = Rand(rows * nOut, 0.01f);
            float[] y1 = new float[rows * nOut + 16], y2 = new float[rows * nOut + 16], dx1 = new float[rows * nIn + 16], dx2 = new float[rows * nIn + 16];
            float[] dw1 = new float[nOut * nIn + 16], dw2 = new float[nOut * nIn + 16], db1 = new float[nOut + 16], db2 = new float[nOut + 16];
            fixed (float* x = X, w = W, b = B, dy = dY, py1 = y1, py2 = y2, pdx1 = dx1, pdx2 = dx2, pdw1 = dw1, pdw2 = dw2, pdb1 = db1, pdb2 = db2)
            {
                MlpOpsRef.LinearForward(x, rows, nIn, w, b, nOut, py1); MlpOps.LinearForward(x, rows, nIn, w, b, nOut, py2);
                MlpOpsRef.LinearBackwardInput(dy, rows, nOut, w, nIn, pdx1); MlpOps.LinearBackwardInput(dy, rows, nOut, w, nIn, pdx2);
                MlpOpsRef.LinearBackwardWeights(dy, x, rows, nOut, nIn, pdw1, pdb1); MlpOps.LinearBackwardWeights(dy, x, rows, nOut, nIn, pdw2, pdb2);
            }
            static double MaxDiff(float[] p, float[] q) { double m = 0; for (int i = 0; i < p.Length; i++) m = Math.Max(m, Math.Abs(p[i] - q[i])); return m; }
            double dF = MaxDiff(y1, y2), dX = MaxDiff(dx1, dx2), dW = Math.Max(MaxDiff(dw1, dw2), MaxDiff(db1, db2));
            double Time(bool newK, int which)
            {
                var sw = Stopwatch.StartNew(); long n = 0;
                while (sw.Elapsed.TotalSeconds < sec / 6)
                {
                    Parallel.For(0, threads, po, t =>
                    {
                        var yy = new float[rows * nOut + 16]; var dxx = new float[rows * nIn + 16]; var dww = new float[nOut * nIn + 16]; var dbb = new float[nOut + 16];
                        fixed (float* x = X, w = W, b = B, dy = dY, py = yy, pdx = dxx, pdw = dww, pdb = dbb)
                            for (int k = 0; k < 4; k++)
                            {
                                if (which == 0) { if (newK) MlpOps.LinearForward(x, rows, nIn, w, b, nOut, py); else MlpOpsRef.LinearForward(x, rows, nIn, w, b, nOut, py); }
                                else if (which == 1) { if (newK) MlpOps.LinearBackwardInput(dy, rows, nOut, w, nIn, pdx); else MlpOpsRef.LinearBackwardInput(dy, rows, nOut, w, nIn, pdx); }
                                else { if (newK) MlpOps.LinearBackwardWeights(dy, x, rows, nOut, nIn, pdw, pdb); else MlpOpsRef.LinearBackwardWeights(dy, x, rows, nOut, nIn, pdw, pdb); }
                            }
                    });
                    n += 4L * threads;
                }
                return n * 2.0 * rows * nIn * nOut / sw.Elapsed.TotalSeconds / 1e9;
            }
            Console.WriteLine(FormattableString.Invariant(
                $"  {nIn}x{nOut}: fwd ref {Time(false, 0):F0} new {Time(true, 0):F0} | dX ref {Time(false, 1):F0} new {Time(true, 1):F0} | dW ref {Time(false, 2):F0} new {Time(true, 2):F0} GFLOP/s | maxdiff fwd {dF:E1} dX {dX:E1} dW {dW:E1}"));
        }
        // tanh accuracy
        var tv = new float[4096]; var r2 = new Rng(9); for (int i = 0; i < tv.Length; i++) tv[i] = (float)(r2.Gaussian() * 4);
        var tc = (float[])tv.Clone(); double te = 0;
        fixed (float* p = tc) MlpOps.TanhInPlace(p, tc.Length);
        for (int i = 0; i < tv.Length; i++) te = Math.Max(te, Math.Abs(tc[i] - Math.Tanh(tv[i])));
        Console.WriteLine($"  tanh max abs err {te:E2}");
        return 0;
    }

    static OpponentSpec[] ParseOpps(string list, PolicyRef self)
    {
        var res = new List<OpponentSpec>();
        foreach (var n in list.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (n == "self") res.Add(new OpponentSpec("self", () => new PolicyOpponent(self, true)));
            else if (n.EndsWith(".btcpol"))
            {
                var p = new PolicyRef(MlpPolicy.Load(File.ReadAllBytes(n)));
                res.Add(new OpponentSpec(Path.GetFileNameWithoutExtension(n), () => new PolicyOpponent(p, false)));
            }
            else res.Add(ToyBots.Spec(n));
        }
        return res.ToArray();
    }

    static int ToyTrain(Args a)
    {
        LowPriority();
        string run = a.Str("run", "toy");
        string dir = Path.Combine(LabRoot(), "runs", run);
        Directory.CreateDirectory(dir);
        int units = a.Int("units", 1);
        var cfg = new PpoConfig
        {
            NumEnvs = a.Int("envs", 64), Steps = a.Int("rollout", 128), Threads = a.Int("threads", Environment.ProcessorCount),
            TotalSteps = a.Long("steps", 5_000_000), Seed = a.Long("seed", 1),
        };
        string oppList = a.Str("opp", "rusher");
        int printEvery = a.Int("print", 10), ckptEvery = a.Int("ckpt-every", 50), snapEvery = a.Int("snap", 20);
        double minutes = a.Dbl("minutes", 0), targetWr = a.Dbl("target-wr", 0);
        bool resume = a.Flag("resume");
        float shape = (float)a.Dbl("shape", 0.5);
        foreach (var kv in a.Unused.ToList())
            if (!cfg.Set(kv.Key, kv.Value)) throw new ArgumentException("unknown option --" + kv.Key);

        var env = new ToyDuelBatch(cfg.NumEnvs, units, shape);
        var selfRef = new PolicyRef(null);
        var opps = ParseOpps(oppList, selfRef);
        var tr = new PpoTrainer(env, opps, cfg);
        string metrics = Path.Combine(dir, "metrics.csv");
        if (resume)
        {
            var last = Directory.GetFiles(dir, "ckpt-*.bin").OrderBy(f => f.Length).ThenBy(f => f, StringComparer.Ordinal).LastOrDefault();
            if (last == null) throw new FileNotFoundException("no checkpoint in " + dir);
            var extra = tr.LoadCheckpoint(last);
            if (extra != null) selfRef.Policy = MlpPolicy.Load(extra);
            Console.WriteLine($"resumed {Path.GetFileName(last)} update={tr.UpdateCount} step={tr.StepCount}");
        }
        if (selfRef.Policy == null) selfRef.Policy = tr.ExportPolicy();
        if (!resume || !File.Exists(metrics))
            File.WriteAllText(metrics, "update,step,episodes," + string.Join(",", opps.Select(o => "wr_" + o.Name)) + ",ep_reward,ep_len,entropy,kl,clipfrac,vloss,gradnorm,samples_per_s,steps_per_s\n");
        Console.WriteLine($"toy-train run={run} units={units}v{units} opp={oppList} envs={cfg.NumEnvs}x{cfg.Steps} threads={cfg.Threads} hidden={cfg.Hidden} steps={cfg.TotalSteps}");

        var wall = Stopwatch.StartNew();
        int[] wEp = new int[opps.Length], wWin = new int[opps.Length], wDraw = new int[opps.Length];
        double wRew = 0, wLen = 0, wSps = 0; int wEpAll = 0, wN = 0;
        UpdateStats st = null;
        string reason = "steps";
        while (tr.StepCount < cfg.TotalSteps)
        {
            st = tr.Update();
            var line = new StringBuilder();
            line.Append(st.Update).Append(',').Append(st.Step).Append(',').Append(st.Episodes);
            for (int k = 0; k < opps.Length; k++)
            {
                line.Append(',').Append(st.OppEpisodes[k] > 0 ? ((double)st.OppWins[k] / st.OppEpisodes[k]).ToString("F4", CultureInfo.InvariantCulture) : "");
                wEp[k] += st.OppEpisodes[k]; wWin[k] += st.OppWins[k]; wDraw[k] += st.OppDraws[k];
            }
            line.Append(FormattableString.Invariant($",{st.EpReward:F4},{st.EpLength:F1},{st.Entropy:F4},{st.Kl:F5},{st.ClipFrac:F4},{st.VLoss:F5},{st.GradNorm:F4},{st.SamplesPerSec:F0},{st.StepsPerSec:F0}\n"));
            File.AppendAllText(metrics, line.ToString());
            wRew += st.EpReward * st.EpCount; wLen += st.EpLength * st.EpCount; wEpAll += st.EpCount; wSps += st.StepsPerSec; wN++;

            if (snapEvery > 0 && st.Update % snapEvery == 0) selfRef.Policy = tr.ExportPolicy();
            if (ckptEvery > 0 && st.Update % ckptEvery == 0) Save(tr, dir, selfRef);
            bool timeUp = minutes > 0 && wall.Elapsed.TotalMinutes >= minutes;
            if (st.Update % printEvery == 0 || timeUp)
            {
                var sb = new StringBuilder();
                sb.Append(FormattableString.Invariant($"u {st.Update} step {st.Step} eps {st.Episodes} t {wall.Elapsed.TotalMinutes:F1}m"));
                double wr0 = 0;
                for (int k = 0; k < opps.Length; k++)
                {
                    double wr = wEp[k] > 0 ? (double)wWin[k] / wEp[k] : 0, dr = wEp[k] > 0 ? (double)wDraw[k] / wEp[k] : 0;
                    if (k == 0) wr0 = wr;
                    sb.Append(FormattableString.Invariant($" wr[{opps[k].Name}] {wr:F3} d {dr:F2}"));
                }
                sb.Append(FormattableString.Invariant($" rew {(wEpAll > 0 ? wRew / wEpAll : 0):F2} len {(wEpAll > 0 ? wLen / wEpAll : 0):F0} ent {st.Entropy:F2} kl {st.Kl:F4} cf {st.ClipFrac:F3} vl {st.VLoss:F4} mb {st.MinibatchesDone} sps {wSps / wN:F0}"));
                Console.WriteLine(sb.ToString());
                bool hit = targetWr > 0 && opps.Length > 0 && opps[0].Name != "self" && wEp[0] >= 100 && wr0 >= targetWr;
                Array.Clear(wEp); Array.Clear(wWin); Array.Clear(wDraw); wRew = wLen = wSps = 0; wEpAll = wN = 0;
                if (hit) { reason = "target-wr"; break; }
            }
            if (timeUp) { reason = "minutes"; break; }
        }
        string ck = Save(tr, dir, selfRef);
        string pol = Path.Combine(dir, "policy.btcpol");
        File.WriteAllBytes(pol, tr.ExportPolicy().Save());
        Console.WriteLine($"done ({reason}) update={tr.UpdateCount} step={tr.StepCount} wall={wall.Elapsed.TotalMinutes:F1}m ckpt={ck} policy={pol}");
        return 0;
    }

    static string Save(PpoTrainer tr, string dir, PolicyRef self)
    {
        string p = Path.Combine(dir, $"ckpt-{tr.UpdateCount:D6}.bin");
        tr.SaveCheckpoint(p, self.Policy?.Save());
        return p;
    }

    static int ToyEval(Args a)
    {
        LowPriority();
        var pol = MlpPolicy.Load(File.ReadAllBytes(a.Str("policy", "")));
        if (pol.SchemaHash != MlpPolicy.Fnv1a(ToyDuelGame.Schema)) throw new InvalidDataException("policy schema hash does not match toyduel");
        string oppName = a.Str("opp", "rusher");
        int n = a.Int("n", 1000), units = a.Int("units", 1), threads = a.Int("threads", Environment.ProcessorCount);
        bool sample = a.Flag("sample");
        ulong seed = (ulong)a.Long("seed", 12345);
        var env = new ToyDuelBatch(threads, units);
        var me = new PolicyRef(pol);
        var opp = ParseOpps(oppName, me)[0];
        int[] win = new int[threads], draw = new int[threads], loss = new int[threads]; long[] len = new long[threads];
        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, t =>
        {
            var self = new PolicyOpponent(me, sample); var o = opp.Create(); var rng = new Rng(Rng.Mix(seed, (ulong)t));
            int[] a0 = new int[units], a1 = new int[units]; float[] rew = new float[units];
            for (int ep = t; ep < n; ep += threads)
            {
                env.Reset(t, Rng.Mix(seed, (ulong)ep + 100000));
                bool done; int outcome; int steps = 0;
                do
                {
                    self.Act(env, t, 0, rng, a0); o.Act(env, t, 1, rng, a1);
                    env.Step(t, a0, a1, rew, 0, out done, out outcome); steps++;
                } while (!done);
                if (outcome > 0) win[t]++; else if (outcome < 0) loss[t]++; else draw[t]++;
                len[t] += steps;
            }
        });
        int W = win.Sum(), D = draw.Sum(), Lo = loss.Sum();
        Console.WriteLine(FormattableString.Invariant($"toy-eval opp={oppName} units={units} n={n} {(sample ? "sample" : "argmax")}: win {W} ({(double)W / n:F3}) draw {D} loss {Lo} avg_len {(double)len.Sum() / n * 0.1:F1}s"));
        return 0;
    }

    /// <summary>pol-migrate --in X.btcpol [--out Y.btcpol]: duelobs-v2 policy -> v3 (appends the role input with zero weights, new schema hash).</summary>
    static int PolMigrate(Args a)
    {
        string inp = a.Str("in", ""), outp = a.Str("out", inp);
        var p = MlpPolicy.Load(File.ReadAllBytes(inp));
        if (p.InputSize != DuelObs.Size - 1) throw new InvalidDataException($"input {p.InputSize}, expected v2 size {DuelObs.Size - 1}");
        int nIn = p.InputSize, nOut = p.LayerOut[0];
        var w = new float[(nIn + 1) * nOut];
        for (int o = 0; o < nOut; o++) Array.Copy(p.Weights[0], o * nIn, w, o * (nIn + 1), nIn);
        p.Weights[0] = w; p.InputSize = nIn + 1; p.SchemaHash = MlpPolicy.Fnv1a(DuelObs.Schema);
        File.WriteAllBytes(outp, p.Save());
        Console.WriteLine($"migrated {inp} -> {outp} (input {nIn} -> {nIn + 1}, {DuelObs.Schema})");
        return 0;
    }

    static int Export(Args a)
    {
        string ck = a.Str("ckpt", "");
        var cfg = PpoTrainer.ReadCheckpointConfig(ck);
        cfg.NumEnvs = 1; cfg.Steps = 1; cfg.Threads = 1;
        var env = new ToyDuelBatch(1, 1);
        var tr = new PpoTrainer(env, new[] { ToyBots.Spec("random") }, cfg);
        tr.LoadCheckpoint(ck);
        string outp = a.Str("out", Path.ChangeExtension(ck, ".btcpol"));
        File.WriteAllBytes(outp, tr.ExportPolicy().Save());
        Console.WriteLine($"exported {outp} (update {tr.UpdateCount}, step {tr.StepCount})");
        return 0;
    }
}
