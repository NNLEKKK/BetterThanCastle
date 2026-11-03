using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Btc.Shared;

namespace Btc.RL.Duel;

public sealed class MatchStats
{
    public int W, D, L, KillsA, KillsB; public double Len; public long Decisions; public double Sec;
    public readonly Dictionary<string, int> Reasons = new();
    public int N => W + D + L;
    public double WinRate => N > 0 ? (double)W / N : 0;
    /// <summary>Win / loss counts by end category (Cat): ke, sc, dead, safe (the loser entered the winner's safe zone or hit a unit in it), time, other.</summary>
    public readonly Dictionary<string, int> WinBy = new(), LossBy = new();
    /// <summary>Win rate without wins handed over by the opponent's safe-zone violations (the score the eval, best and rollback use).</summary>
    public double HonestWinRate => N > 0 ? (double)(W - WinBy.GetValueOrDefault("safe")) / N : 0;
    public static string Cat(string reason)
    {
        reason ??= "";
        if (reason.Contains("safe zone")) return "safe";
        if (reason.Contains("escape")) return "ke";
        if (reason.Contains("space control")) return "sc";
        if (reason.Contains("dead") || reason.Contains("elimin")) return "dead";
        if (reason.Contains("time")) return "time";
        return "other";
    }
    public string ByText()
    {
        string F(Dictionary<string, int> d) => string.Join(" ", d.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value));
        return "W[" + F(WinBy) + "] L[" + F(LossBy) + "]";
    }
}

/// <summary>Plays n matches side A (west) vs side B, deterministic per seed (episode i always gets seed Mix(seed, i)).</summary>
public static class DuelMatchRunner
{
    public static MatchStats Run(DuelOppDef aDef, DuelOppDef bDef, DuelCurriculum cur, int n, int threads, ulong seed)
    {
        threads = Math.Max(1, Math.Min(threads, n));
        var env = new SimDuelEnv(threads, cur, new[] { bDef }, aDef.External ? null : aDef);
        var parts = new MatchStats[threads];
        var sw = Stopwatch.StartNew();
        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, t =>
        {
            var ms = new MatchStats(); parts[t] = ms;
            var oa = aDef.Create(); var ob = bDef.Create();
            var rng = new Rng(Rng.Mix(seed, (ulong)t + 7));
            int M = env.MaxAgents;
            int[] a0 = new int[M], a1 = new int[M]; float[] rew = new float[M];
            for (int ep = t; ep < n; ep += threads)
            {
                env.Reset(t, Rng.Mix(seed, (ulong)ep + 100000));
                bool done; int outcome;
                do
                {
                    if (aDef.External) oa.Act(env, t, 0, rng, a0); else Array.Clear(a0);
                    ob.Act(env, t, 1, rng, a1);
                    env.Step(t, a0, a1, rew, 0, out done, out outcome);
                } while (!done);
                var info = env.Info(t);
                if (outcome > 0) ms.W++; else if (outcome < 0) ms.L++; else ms.D++;
                ms.Len += info.FightTime; ms.Decisions += info.Decisions;
                ms.KillsA += info.Kills0; ms.KillsB += info.Kills1;
                string r = Regex.Replace(info.Reason ?? "?", @"u\d+", "u#");
                ms.Reasons[r] = ms.Reasons.GetValueOrDefault(r) + 1;
                if (outcome != 0) { var by = outcome > 0 ? ms.WinBy : ms.LossBy; string k = MatchStats.Cat(info.Reason); by[k] = by.GetValueOrDefault(k) + 1; }
            }
        });
        var all = new MatchStats { Sec = sw.Elapsed.TotalSeconds };
        foreach (var p in parts)
        {
            all.W += p.W; all.D += p.D; all.L += p.L; all.KillsA += p.KillsA; all.KillsB += p.KillsB; all.Len += p.Len; all.Decisions += p.Decisions;
            foreach (var kv in p.Reasons) all.Reasons[kv.Key] = all.Reasons.GetValueOrDefault(kv.Key) + kv.Value;
            foreach (var kv in p.WinBy) all.WinBy[kv.Key] = all.WinBy.GetValueOrDefault(kv.Key) + kv.Value;
            foreach (var kv in p.LossBy) all.LossBy[kv.Key] = all.LossBy.GetValueOrDefault(kv.Key) + kv.Value;
        }
        return all;
    }
}

/// <summary>One fixed rule set of the periodic evaluation panel.</summary>
public sealed class EvalCell
{
    public string Name, Opp; public int Units, Arena, Extra; public bool Skips, Heal;   // Opp = per-cell opponent (null: the panel default)   // Extra = handicap units for the opponent
    public EvalCell(string name, int units, int arena, bool skips, bool heal, int extra = 0, string opp = null) { Opp = opp; Name = name; Units = units; Arena = arena; Skips = skips; Heal = heal; Extra = extra; }
}

/// <summary>
/// Periodic evaluation of the learner with the SHIPPED controller (BtcDuelBotLuminaZero, sampling by default) vs a scripted opponent
/// (prod4 = Chosen) on a fixed panel of rule sets with fixed seeds, plus the auto-curriculum stage definitions.
/// </summary>
public static class DuelEval
{
    public static readonly EvalCell[] Panel =
    {
        new("1v1x1", 1, 1, true, false),
        new("3v3x1", 3, 1, true, false),
        new("5v5x3", 5, 3, true, false),
        new("10v10x3", 10, 3, true, false),
        new("5v5x1ns", 5, 1, false, false),
        new("10v10x3h", 10, 3, true, true),
    };

    /// <summary>Panel of the luminazero preset: sizes 4..15, skips on/off, heal on/off, handicap (opponent +N units).</summary>
    public static readonly EvalCell[] Panel2 =
    {
        new("4v4x3", 4, 3, true, false),
        new("10v10x1", 10, 1, true, false),
        new("15v15x3", 15, 3, true, false),
        new("10v10x3ns", 10, 3, false, false),
        new("10v10x1h", 10, 1, true, true),
        new("6v8x1H2", 6, 1, true, false, 2),
        new("8v11x3H3", 8, 3, true, false, 3),
        new("15v18x1H3", 15, 1, true, false, 3),
        new("10v10x3-p2", 10, 3, true, false, 0, "prod2"),
        new("10v10x1-hunt", 10, 1, true, false, 0, "hunter"),
    };

    // ---------------- LuminaZero curriculum (v2 tables, separate from the v1 stages below) ----------------
    /// <summary>Union of every opponent used by any LuminaZero stage (the opponent array is fixed at env construction; stages switch the weights).</summary>
    // Training is pure self-play (league pool, PFSP); scripted bots are evaluation-only (docs/decisions.md 2026-11-01).
    public static readonly string[] ZNames = { "pool" };
    public static readonly float[][] ZWeights = { new float[] { 1 }, new float[] { 1 }, new float[] { 1 }, new float[] { 1 } };
    public static readonly float[] ZDecision = { 0.2f, 0.1f, 0.1f, 0.1f };
    public static readonly float[] ZTimeLimit = { 90f, 90f, 150f, 150f };   // 60 s made the time-limit draw a safe haven in self-play; 90 s on x3 with 4-15 units ended 93% of self-play games in a draw (the game has no limit)
    // stage 0 on the small arena (a random team never reaches the middle of x3 before space control); later both sizes
    public static readonly string[] ZArena = { "1", "1:1,3:1", "1:1,3:1", "1:1,3:1" };
    public static int ZLast => ZWeights.Length - 1;

    /// <summary>Training distribution of LuminaZero stage s: a clone of baseCur with the stage's units / handicap / time limit / decision period.</summary>
    public static DuelCurriculum StageZ(DuelCurriculum baseCur, int s)
    {
        var c = baseCur.Clone();
        c.TimeLimit = ZTimeLimit[s]; c.DecisionSec = ZDecision[s];
        c.SetArena(ZArena[s]);   // stage 0 on the small arena: on x3 a random team never reaches the middle (150 m) before space control
        switch (s)
        {
            case 0: c.SetUnits("6"); c.HandicapProb = 0f; break;
            case 1: c.SetUnits("4-10"); c.HandicapProb = 0.2f; c.HandicapMin = 1; c.HandicapMax = 3; break;
            // stage 2 = softer bridge (4-15 at once with a 40% handicap always against the learner stalled for 2000 updates); handicap favours either side
            case 2: c.SetUnits("4-12"); c.HandicapProb = 0.2f; c.HandicapMin = 1; c.HandicapMax = 3; c.HandicapOwnProb = 0.5f; break;
            default: c.SetUnits("4-15"); c.HandicapProb = 0.4f; c.HandicapMin = 1; c.HandicapMax = 3; c.HandicapOwnProb = 0.5f; break;
        }
        return c;
    }

    /// <summary>Stage-matched evaluation cells (the stage advances when their mean win rate exceeds the threshold).</summary>
    public static EvalCell[] PanelZ(int s)
    {
        switch (s)
        {
            // eval-only bots: prod4 (= production Chosen, 5b86a49), prod2, hunter2 (fair hunter; the old hunter mostly lost by fouls), presser
            case 0: return new[]
            {
                new EvalCell("6v6x1-p4", 6, 1, true, false, 0, "prod4"), new EvalCell("6v6x1-p4ns", 6, 1, false, false, 0, "prod4"),
                new EvalCell("6v6x1-p2ns", 6, 1, false, false, 0, "prod2"),
                new EvalCell("6v6x1-hunt2", 6, 1, true, false, 0, "hunter2"), new EvalCell("6v6x1-hunt2ns", 6, 1, false, false, 0, "hunter2"),
                new EvalCell("6v6x1-press", 6, 1, true, false, 0, "presser"), new EvalCell("6v6x1-pressns", 6, 1, false, false, 0, "presser"),
            };
            case 1: return new[]
            {
                new EvalCell("6v6x3-p4", 6, 3, true, false, 0, "prod4"), new EvalCell("10v10x1-p4", 10, 1, true, false, 0, "prod4"),
                new EvalCell("5v5x3-p2ns", 5, 3, false, false, 0, "prod2"), new EvalCell("8v10x1H2-p4", 8, 1, true, false, 2, "prod4"),
                new EvalCell("7v7x3heal-p4", 7, 3, true, true, 0, "prod4"), new EvalCell("8v8x1-hunt", 8, 1, true, false, 0, "hunter"),
            };
            default: return Panel2;
        }
    }

    // ---------------- curriculum stages ----------------
    public static readonly string[] StageUnits = { "1", "1-3", "1-5", "1-10" };
    public static readonly string[] StageArena = { "1", "1", "1,3", "1,3" };
    /// <summary>Panel cells that gate the advance out of each stage (the stage's "own" cells). The last stage never advances.</summary>
    public static readonly int[][] StageCells = { new[] { 0 }, new[] { 0, 1 }, new[] { 0, 1, 2, 4 }, new[] { 0, 1, 2, 3, 4, 5 } };
    public static int LastStage => StageUnits.Length - 1;

    /// <summary>Training distribution of a stage: a clone of the base (rules probabilities, fps, shaping...) with the stage's units/arena,
    /// replaying earlier stages with probability earlierProb.</summary>
    public static DuelCurriculum Stage(DuelCurriculum baseCur, int stage, float earlierProb)
    {
        DuelCurriculum Mk(int s) { var c = baseCur.Clone(); c.SetUnits(StageUnits[s]); c.SetArena(StageArena[s]); return c; }
        var cur = Mk(stage);
        if (stage > 0 && earlierProb > 0f)
        {
            cur.Earlier = Enumerable.Range(0, stage).Select(Mk).ToArray();
            cur.EarlierProb = earlierProb;
        }
        return cur;
    }

    public static DuelCurriculum CellCurriculum(EvalCell c, float timeLimit = 300f)
    {
        var cur = new DuelCurriculum { SkipsProb = c.Skips ? 1f : 0f, HealProb = c.Heal ? 1f : 0f, FpsMin = 60f, FpsMax = 60f, DomainRand = 0f, TimeLimit = timeLimit, HandicapProb = c.Extra > 0 ? 1f : 0f, HandicapMin = Math.Max(1, c.Extra), HandicapMax = Math.Max(1, c.Extra) };
        cur.SetUnits(c.Units.ToString(CultureInfo.InvariantCulture));
        cur.SetArena(c.Arena.ToString(CultureInfo.InvariantCulture));
        return cur;
    }

    /// <summary>Runs the panel; returns per-cell stats (index = Panel index). report gets one line per cell.</summary>
    public static MatchStats[] RunPanel(MlpPolicy pol, bool argmax, string oppName, int n, int threads, ulong seed, StringBuilder report, EvalCell[] panel = null, float timeLimit = 300f)
    {
        panel ??= Panel;
        var res = new MatchStats[panel.Length];
        var a = DuelOpponents.Lumina(pol, argmax);
        var bDef = new Dictionary<string, DuelOppDef>();
        for (int i = 0; i < panel.Length; i++)
        {
            var c = panel[i];
            string on = c.Opp ?? oppName;
            if (!bDef.TryGetValue(on, out var b)) bDef[on] = b = DuelOpponents.Get(on);
            res[i] = DuelMatchRunner.Run(a, b, CellCurriculum(c, timeLimit), n, threads, Rng.Mix(seed, (ulong)(i + 1)));
            var s = res[i];
            report?.AppendLine(FormattableString.Invariant(
                $"{c.Name,-9} W {s.W,3} D {s.D,3} L {s.L,3}  wr {s.WinRate:F3} honest {s.HonestWinRate:F3}  {s.ByText()}  len {(s.N > 0 ? s.Len / s.N : 0),5:F1}s  kills {(double)s.KillsA / Math.Max(1, s.N):F2}/{(double)s.KillsB / Math.Max(1, s.N):F2}  {s.Sec:F1}s"));
        }
        if (report != null) report.AppendLine(Split(panel, res));
        return res;
    }

    /// <summary>Mean win rate split by rule: skips on/off, heal on/off, handicap none/some (cells of a panel).</summary>
    public static string Split(EvalCell[] panel, MatchStats[] res)
    {
        double M(Func<EvalCell, bool> f) { double s = 0; int k = 0; for (int i = 0; i < panel.Length; i++) if (f(panel[i])) { s += res[i].HonestWinRate; k++; } return k > 0 ? s / k : double.NaN; }
        return FormattableString.Invariant($"split wr: skips {M(c => c.Skips):F3} noskips {M(c => !c.Skips):F3} | heal {M(c => c.Heal):F3} noheal {M(c => !c.Heal):F3} | even {M(c => c.Extra == 0):F3} handicap {M(c => c.Extra > 0):F3}");
    }

    public static double Mean(MatchStats[] r, int[] cells = null)
    {
        cells ??= Enumerable.Range(0, r.Length).ToArray();
        double s = 0; foreach (int i in cells) s += r[i].HonestWinRate;
        return cells.Length > 0 ? s / cells.Length : 0;
    }
}
