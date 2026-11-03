using System.Diagnostics;
using System.Text;

namespace Btc.Sim;

/// <summary>Engine self test: determinism, rules, 1v1 cycles, speeds, benchmark, RulesParity over the real logs. Returns a text report.</summary>
public static class SimSelfTest
{
    sealed class Rec : ISimListener
    {
        public readonly List<(float t, int a, int b)> Strikes = new();
        public readonly List<(float t, int a, int b, float hp)> HitsL = new();
        public int KillsN, EndN; public DuelSim S;
        public Action<int, int> OnStrikeHook;
        public void OnStrike(int unit, int target) { Strikes.Add((S.Time, unit, target)); OnStrikeHook?.Invoke(unit, target); }
        public void OnHit(int attacker, int victim, float dealt, float hpAfter) => HitsL.Add(((float)S.RenderTime, attacker, victim, hpAfter));
        public void OnKill(int victim, int killer) => KillsN++;
        public void OnEnd(int winner, string reason) => EndN++;
    }

    static int _fail;
    static void Check(StringBuilder sb, bool ok, string what) { if (!ok) _fail++; sb.Append(ok ? "  ok   " : "  FAIL ").AppendLine(what); }

    /// <summary>Simple scripted controller: every second each unit MoveAttacks toward the enemy safe line (no bots).</summary>
    public static void ScriptTick(DuelSim s)
    {
        if (!s.ControllersActive || s.Frame % 50 != 0) return;
        for (int u = 0; u < s.Count; u++)
        {
            if (s.Dead[u]) continue;
            float tx = s.Side[u] == 0 ? s.Len - s.SafeLen - 3f : s.SafeLen + 3f;
            s.Order(u, SimOrder.MoveAttack, tx, s.Z[u], -1);
        }
    }

    public static string Run(string logRoot = null, bool parity = true)
    {
        _fail = 0;
        var sb = new StringBuilder();
        // ---- determinism ----
        {
            ulong h1 = RunHash(42, 3000), h2 = RunHash(42, 3000), h3 = RunHash(43, 3000);
            Check(sb, h1 == h2, $"determinism: same seed same hash ({h1:X16}); other seed {(h3 != h1 ? "differs" : "SAME")}");
        }
        // ---- rules ----
        {
            var s = NewSim(5, 1, false); s.MechanicsEnabled = false;
            s.SetPosition(0, s.Len - 5f, 10f); StepN(s, 10);
            Check(sb, s.Winner == 1 && s.Reason.Contains("entered the safe zone"), $"rules: safe entry -> {s.Winner} '{s.Reason}'");

            s = NewSim(5, 1, false); s.MechanicsEnabled = false; StepN(s, 5);
            s.ExternalHit(5, 0, 32f, 468f);
            Check(sb, s.Winner == 0 && s.Reason.Contains("attacked a unit in the safe zone"), $"rules: safe-zone hit -> {s.Winner} '{s.Reason}'");

            s = NewSim(5, 1, false); s.MechanicsEnabled = false;
            for (int u = 0; u < 10; u++) s.SetPosition(u, 50f, 2f + u);    // everyone in mid
            StepN(s, 5);
            s.ExternalKill(6, 1);
            bool esc = s.Escaping[0] && !s.Escaping[1];
            s.ExternalKill(1, 7);
            bool reset = !s.Escaping[0] && !s.Escaping[1] && !s.Over;
            s.ExternalKill(7, 2);
            for (int u = 0; u < 5; u++) if (!s.Dead[u]) s.SetPosition(u, 3f, 2f + u);
            StepN(s, 10);
            Check(sb, esc && reset && s.Winner == 0 && s.Reason == "kill and escape", $"rules: kill&escape (escape={esc}, equal-kill reset={reset}) -> {s.Winner} '{s.Reason}'");

            s = NewSim(5, 1, false); s.MechanicsEnabled = false;
            s.SetPosition(0, 50f, 10f);
            while (!s.Over && s.Time < 15f) s.Step();
            Check(sb, s.Winner == 0 && s.Reason == "space control" && MathF.Abs(s.FightTime - 10f) < 0.15f, $"rules: space control 10 s -> {s.Winner} '{s.Reason}' at {s.FightTime:0.00}");
        }
        // ---- 1v1 fights / strike timings (measured.md) ----
        {
            var a = Fight1v1(false, 0);
            Check(sb, a.HitsToKill >= 15 && a.HitsToKill <= 17 && MathF.Abs(a.Cycle - 1.5f) < 0.03f, $"1v1 skips off, no orders: kill after {a.HitsToKill} hits, strike cycle {a.Cycle * 1000:0} ms (1500), t={a.T:0.0}");
            Check(sb, MathF.Abs(a.DFirst - 0.46f) < 0.03f && MathF.Abs(a.DRest - 0.34f) < 0.03f, $"strike->damage: first {a.DFirst / DuelSim.FixedDt:0.0} ticks (23), in stance {a.DRest / DuelSim.FixedDt:0.0} ticks (17)");
            var f20 = Fight1v1(false, 0, -1f, true, 20f); var f60 = Fight1v1(false, 0, -1f, true, 60f);
            Check(sb, MathF.Abs(f20.DFirst - 0.53f) < 0.035f && MathF.Abs(f20.DRest - 0.38f) < 0.035f && MathF.Abs(f60.DFirst - 0.48f) < 0.03f && MathF.Abs(f60.DRest - 0.36f) < 0.03f,
                $"strike->damage vs fps (render-frame animator): 20 fps first {f20.DFirst:0.000} s (0.53) stance {f20.DRest:0.000} (0.38); 60 fps {f60.DFirst:0.000} (0.48) / {f60.DRest:0.000} (0.36)");
            var c8 = Fight1v1(true, 8, 0f, false);
            Check(sb, c8.Hits0 == 0 && MathF.Abs(c8.Restrike - 0.34f) < 0.03f, $"cancel: re-order at K=8 -> {c8.Hits0} hits of {c8.Strikes0} strikes, next strike {c8.Restrike / DuelSim.FixedDt:0.0} ticks after the order (17)");
            var c9 = Fight1v1(true, 9, 0f, false);
            Check(sb, c9.Hits0 >= c9.Strikes0 - 1 && c9.Hits0 > 3, $"cancel: re-order at K=9 -> {c9.Hits0} hits of {c9.Strikes0} strikes (lands)");
            var k10 = Fight1v1(true, 10, 0f);
            Check(sb, k10.Cycle > 0.54f && k10.Cycle < 0.60f, $"skip K=10 fast mode: cycle {k10.Cycle * 1000:0} ms (560-580), {k10.HitsToKill} hits to kill, t={k10.T:0.0}");
            var kb = Fight1v1(true, 10);
            Check(sb, kb.Cycle > 0.6f && kb.Cycle < 0.85f, $"skip K=10 bimodal (SkipSlowProb {SimParams.Default().SkipSlowProb}): mean cycle {kb.Cycle * 1000:0} ms (modes 570 / 880)");
            var g = Fight1v1(false, 10, 0f);
            Check(sb, MathF.Abs(g.Cycle - 1.5f) < 0.03f, $"skip K=10 with skips OFF (DuelPatches guard): cycle {g.Cycle * 1000:0} ms");
        }
        // ---- speeds ----
        {
            float ax = Speed(30f, 0f), dg = Speed(14f, 14f);
            Check(sb, MathF.Abs(ax - 5.95f) < 0.15f, $"run speed axis {ax:0.00} m/s (measured 5.95), diagonal {dg:0.00} m/s");
            float m1 = MoveTime(1, 0), m2 = MoveTime(2, 0), m3 = MoveTime(3, 0), m58 = MoveTime(4, 4), m40 = MoveTime(40, 0), st = StartDelay();
            Check(sb, MathF.Abs(m40 - 7.0f) < 0.2f && MathF.Abs(st - 0.13f) < 0.03f,
                $"order->first move {st:0.00} s (0.12-0.14); order->arrive 1 m {m1:0.00} (0.62-0.70), 2 m {m2:0.00} (0.66-0.82), 3 m {m3:0.00} (0.92-1.12), 5.7 m diag {m58:0.00} (1.40-1.72), 40 m {m40:0.00} (6.96-7.08)");
        }
        // ---- benchmark ----
        {
            var s = new DuelSim();
            double best = double.MaxValue, sum = 0; int runs = 40, warm = 20; float gt = 0; long alloc0 = 0, alloc1 = 0;
            for (int r = 0; r < runs; r++)
            {
                s.Reset(new SimRules { UnitsPerSide = 10, Skips = false, TimeLimit = 90f }, (ulong)(100 + r % 5));
                if (r == runs - 1) alloc0 = GC.GetAllocatedBytesForCurrentThread();
                var sw = Stopwatch.StartNew();
                while (!s.Over && s.FightTime < 90f) { ScriptTick(s); s.Step(); }
                sw.Stop();
                if (r == runs - 1) alloc1 = GC.GetAllocatedBytesForCurrentThread();
                if (r >= warm) { sum += sw.Elapsed.TotalMilliseconds; best = Math.Min(best, sw.Elapsed.TotalMilliseconds); }
                gt += s.FightTime;
            }
            sb.AppendLine($"  bench 10v10 scripted MoveAttack: avg {sum / (runs - warm):0.00} ms/match (after {warm} warm-up matches), best {best:0.00} ms, avg game time {gt / runs:0.0} s, last result {s.Winner} '{s.Reason}', hits {s.Hits[0]}/{s.Hits[1]} kills {s.Kills[0]}/{s.Kills[1]}, alloc {(alloc1 - alloc0)} B");
            s.Reset(new SimRules { UnitsPerSide = 10, Skips = false, TimeLimit = 1000f }, 7);
            var sw2 = Stopwatch.StartNew();
            for (int i = 0; i < 4500; i++) { ScriptTick(s); s.Step(); }
            sw2.Stop();
            sb.AppendLine($"  bench 4500 steps (90 s) 10v10 regardless of end: {sw2.Elapsed.TotalMilliseconds:0.00} ms (bfs {s.StatBfs} visits {s.StatBfsVisits}, astar {s.StatAStar} exp {s.StatAStarExp}, repath {s.StatRepath}, render frames {s.RenderFrames})");
        }
        // ---- parity ----
        if (parity)
        {
            logRoot ??= LogReplay.DefaultLogRoot();
            if (logRoot == null) sb.AppendLine("  parity: log root not found");
            else
            {
                int usable = 0, pass = 0; var fails = new List<string>();
                int ol = 0; double olR = 0, olS = 0;
                foreach (string f in LogReplay.FindLogs(logRoot))
                {
                    LogReplay.DuelLog L;
                    try { L = LogReplay.Parse(f); } catch (Exception e) { fails.Add(Path.GetFileName(f) + " parse " + e.Message); continue; }
                    var r = LogReplay.RulesParity(L);
                    if (!r.Usable) continue;
                    usable++;
                    if (r.Pass) pass++;
                    else fails.Add($"{r.File}: log {r.LogWinner} '{r.LogReason}' {r.LogT:0.0} vs sim {r.SimWinner} '{r.SimReason}' {r.SimT:0.0}");
                    if (L.HasDest) { var o = LogReplay.OpenLoop(L, 2f); if (o.HorizonSamples > 0) { ol++; olR += o.PosRmseAtHorizon; olS += o.StillRmseAtHorizon; } }
                }
                sb.AppendLine($"  RulesParity: {pass}/{usable} pass");
                foreach (string s in fails.Take(12)) sb.AppendLine("    " + s);
                sb.AppendLine(ol > 0 ? $"  OpenLoop 2 s: {ol} logs, mean pos RMSE at horizon {olR / ol:0.00} m (stand-still baseline {olS / ol:0.00} m)" : "  OpenLoop: no logs with ORD/BORD destinations yet");
            }
        }
        sb.Insert(0, $"SimSelfTest: {(_fail == 0 ? "ALL OK" : _fail + " FAILED")}\n");
        return sb.ToString();
    }

    /// <summary>Rules / speed / timing checks run under lab conditions (SimParams.Lab: steady fps, no hitches), like the measurements.</summary>
    static DuelSim NewSim(int n, int scale, bool skips)
    {
        var s = new DuelSim { P = SimParams.Lab() };
        s.Reset(new SimRules { UnitsPerSide = n, ArenaScale = scale, Skips = skips, TimeLimit = 1000f }, 1);
        return s;
    }

    static void StepN(DuelSim s, int n) { for (int i = 0; i < n; i++) s.Step(); }

    static ulong RunHash(ulong seed, int steps)
    {
        var s = new DuelSim { P = SimParams.Default().Jittered(seed, 1f) };
        s.Reset(new SimRules { UnitsPerSide = 6, Skips = true, TimeLimit = 1000f }, seed);
        for (int i = 0; i < steps; i++) { ScriptTick(s); s.Step(); }
        return s.StateHash();
    }

    sealed class FightStats { public int HitsToKill, Strikes0, Hits0; public float Cycle, T, DFirst, DRest, Restrike; }

    /// <summary>Unit 0 vs unit 1 placed 2 cells apart in mid, both Attack each other. reorderK > 0: unit 0 re-sends Attack K frames after each strike start
    /// (skipMode: MoveAttack(own) + Attack, like the bot skip).</summary>
    static FightStats Fight1v1(bool skips, int reorderK, float slowProb = -1f, bool skipMode = true, float fps = 90f)
    {
        var s = new DuelSim { P = SimParams.Lab(fps) };
        if (slowProb >= 0f) s.P.SkipSlowProb = slowProb;
        var rec = new Rec { S = s };
        s.Listener = rec;
        s.Reset(new SimRules { UnitsPerSide = 1, Skips = skips, TimeLimit = 1000f }, 3);
        s.SetUnitState(0, 50f, 10f, 500f, false);
        s.SetUnitState(1, 52f, 10f, 500f, false);
        s.Order(0, SimOrder.Attack, 0, 0, 1);
        s.Order(1, SimOrder.Attack, 0, 0, 0);
        int due = -1;
        var orderFrames = new List<int>();
        rec.OnStrikeHook = (a, b) => { if (a == 0 && reorderK > 0) due = s.Frame + reorderK; };
        while (!s.Dead[0] && !s.Dead[1] && s.Time < 80f)
        {
            if (due >= 0 && s.Frame >= due)
            {
                due = -1; orderFrames.Add(s.Frame);
                if (skipMode) s.Order(0, SimOrder.MoveAttack, s.X[0], s.Z[0], -1);
                s.Order(0, SimOrder.Attack, 0, 0, 1);
            }
            s.Step();
        }
        var st = new FightStats { T = s.Time };
        var s0 = rec.Strikes.Where(x => x.a == 0).Select(x => x.t).ToList();
        var h0 = rec.HitsL.Where(h => h.a == 0).Select(h => h.t).ToList();
        st.Strikes0 = s0.Count; st.Hits0 = h0.Count;
        st.Cycle = s0.Count > 2 ? (s0[^1] - s0[1]) / (s0.Count - 2) : 0f;
        st.HitsToKill = rec.HitsL.Count(h => h.b == (s.Dead[1] ? 1 : 0));
        // strike -> damage delay of unit 0 (each hit vs the latest strike start before it)
        var ds = new List<float>();
        foreach (float ht in h0) { float best = -1; foreach (float t in s0) if (t <= ht) best = t; if (best >= 0) ds.Add(ht - best); }
        st.DFirst = ds.Count > 0 ? ds[0] : 0f;
        st.DRest = ds.Count > 1 ? ds.Skip(1).Average() : 0f;
        var rs = new List<float>();
        foreach (int of in orderFrames) { float ot = of * DuelSim.FixedDt; float nx = s0.FirstOrDefault(t => t > ot + 1e-4f); if (nx > 0) rs.Add(nx - ot); }
        st.Restrike = rs.Count > 0 ? rs.Average() : 0f;
        return st;
    }

    static float MoveTime(int dx, int dz)
    {
        var s = NewSim(1, 3, false);
        s.SetUnitState(0, 100f, 20f, 500f, false);
        s.SetUnitState(1, 280f, 50f, 500f, false);
        s.Order(0, SimOrder.Move, 100f + dx, 20f + dz, -1);
        for (int i = 0; i < 1000; i++)
        {
            s.Step();
            if (MathF.Abs(s.X[0] - (100f + dx)) < 1e-3f && MathF.Abs(s.Z[0] - (20f + dz)) < 1e-3f) return (float)s.RenderTime;
        }
        return -1f;
    }

    static float StartDelay()
    {
        var s = NewSim(1, 3, false);
        s.SetUnitState(0, 100f, 20f, 500f, false);
        s.SetUnitState(1, 280f, 50f, 500f, false);
        s.Order(0, SimOrder.Move, 120f, 20f, -1);
        for (int i = 0; i < 100; i++) { s.Step(); if (s.X[0] > 100.001f) return (float)s.RenderTime; }
        return -1f;
    }

    static float Speed(float dx, float dz)
    {
        var s = NewSim(1, 1, false);
        s.SetUnitState(0, 40f, 2f, 500f, false);
        s.SetUnitState(1, 90f, 18f, 500f, false);
        s.Order(0, SimOrder.Move, 40f + dx, 2f + dz, -1);
        float x0 = s.X[0], z0 = s.Z[0], t0 = -1f, xs = 0, zs = 0;
        for (int i = 0; i < 1000; i++)
        {
            s.Step();
            float d = MathF.Sqrt((s.X[0] - x0) * (s.X[0] - x0) + (s.Z[0] - z0) * (s.Z[0] - z0));
            if (t0 < 0 && d > 2f) { t0 = s.Time; xs = s.X[0]; zs = s.Z[0]; }
            float rem = MathF.Sqrt((40f + dx - s.X[0]) * (40f + dx - s.X[0]) + (2f + dz - s.Z[0]) * (2f + dz - s.Z[0]));
            if (t0 >= 0 && rem < 2f) return MathF.Sqrt((s.X[0] - xs) * (s.X[0] - xs) + (s.Z[0] - zs) * (s.Z[0] - zs)) / (s.Time - t0);
        }
        return 0f;
    }
}
