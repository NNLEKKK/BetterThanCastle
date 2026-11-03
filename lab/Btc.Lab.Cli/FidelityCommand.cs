using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Btc.Bots;
using Btc.RL;
using Btc.RL.Duel;
using Btc.Shared;
using Btc.Sim;

namespace Btc.Lab.Cli;

/// <summary>
/// Closed-loop sim-vs-real fidelity: the same matchup in the real game (duel logs) and in the sim, reduced by ONE metric code path
/// (MatchRec) and compared per metric (mean +- 95% CI, z, verdict). Exit code = number of MISMATCH rows.
///   fidelity --real dir1,dir2 --a v2 --b prod4 --units 10 --arena 3 --skips 1 --heal 0 --n 400 [--threads T] [--out docs/sim/fidelity.md]
///            [--params key=value,..] [--seed S] [--time-limit 300] [--min-real N]
/// --params: SimParams field names (fps=60, RunSpeed=5.95, ...) and rules.SpaceControlSec / rules.SettleTime / rules.TimeLimit.
/// Side A = side 0 = west = "player" in the log (here: the frozen V2 auto-player), side B = side 1 = "bot" (production bot).
/// </summary>
static class FidelityCommand
{
    public const string Help =
        "  fidelity --real dir1,dir2 --a v2 --b prod4 --units 10 --arena 3 --skips 1 --heal 0 --n 400 [--threads T --out docs/sim/fidelity.md --params k=v,.. --seed S --time-limit 300 --min-real N]";

    // =====================================================================================
    // One match reduced to metrics. Fed by the real-log parser and by the sim taps through the SAME methods.
    sealed class MatchRec
    {
        public int Units;                                   // units per side (id < Units = side 0 in both sources unless UNIT lines say otherwise)
        public readonly Dictionary<int, int> SideOf = new();
        public int SideId(int id) { return SideOf.TryGetValue(id, out int s) ? s : (id < Units ? 0 : 1); }

        public int Strikes, Winner = -9; public string Reason = ""; public float EndT = -1f;
        public readonly int[] Hits = new int[2], Skips = new int[2], Kills = new int[2];
        public float FirstKillT = float.NaN; public int FirstKillSide = -1;
        readonly List<float> _dist = new(), _d = new(), _gap = new();
        readonly Dictionary<int, float> _lastHit = new();
        public float Width;                                 // x spread of the first snapshot (arena size)
        bool _firstSnap = true;
        float _prevT = -1f; float[] _px = new float[32], _pz = new float[32]; bool[] _pa = new bool[32];
        double _spSum, _cdSum; long _spN, _cdN;

        public void AddStrike() { Strikes++; }
        public void AddSkip(int unit) { Skips[SideId(unit)]++; }
        public void AddHit(float t, int att, float dist, float dMs)
        {
            Hits[SideId(att)]++;
            if (dist >= 0f) _dist.Add(dist);
            if (dMs >= 0f) _d.Add(dMs);
            if (_lastHit.TryGetValue(att, out float lt)) _gap.Add((t - lt) * 1000f);
            _lastHit[att] = t;
        }
        public void AddKill(float t, int victimSide)
        {
            int killer = 1 - victimSide;
            Kills[killer]++;
            if (float.IsNaN(FirstKillT)) { FirstKillT = t; FirstKillSide = killer; }
        }
        public void AddEnd(float t, int winner, string reason) { EndT = t; Winner = winner; Reason = reason ?? ""; }

        /// <summary>One position snapshot; arrays indexed by unit id; side/alive per unit.</summary>
        public void AddSnap(float t, int count, int[] id, float[] x, float[] z, bool[] alive)
        {
            if (_px.Length < 1024 && id != null) { int mx = 0; for (int i = 0; i < count; i++) if (id[i] > mx) mx = id[i]; if (mx >= _px.Length) { Array.Resize(ref _px, mx + 16); Array.Resize(ref _pz, mx + 16); Array.Resize(ref _pa, mx + 16); } }
            if (_firstSnap)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                for (int i = 0; i < count; i++) if (alive[i]) { lo = Math.Min(lo, x[i]); hi = Math.Max(hi, x[i]); }
                Width = hi - lo; _firstSnap = false;
            }
            float dt = t - _prevT;
            float[] cx = new float[2], cz = new float[2]; int[] cn = new int[2];
            for (int i = 0; i < count; i++)
            {
                if (!alive[i]) continue;
                int u = id[i], s = SideId(u);
                cx[s] += x[i]; cz[s] += z[i]; cn[s]++;
                if (_prevT >= 0f && dt > 0.05f && dt <= 0.6f && u < _pa.Length && _pa[u])
                {
                    float dx = x[i] - _px[u], dz = z[i] - _pz[u];
                    float v = MathF.Sqrt(dx * dx + dz * dz) / dt;
                    if (v > 1.0f && v < 12f) { _spSum += v; _spN++; }          // moving units only
                }
            }
            if (cn[0] > 0 && cn[1] > 0)
            {
                float dx = cx[0] / cn[0] - cx[1] / cn[1], dz = cz[0] / cn[0] - cz[1] / cn[1];
                _cdSum += Math.Sqrt(dx * dx + dz * dz); _cdN++;
            }
            for (int u = 0; u < _pa.Length; u++) _pa[u] = false;
            for (int i = 0; i < count; i++) if (alive[i] && id[i] < _pa.Length) { _px[id[i]] = x[i]; _pz[id[i]] = z[i]; _pa[id[i]] = true; }
            _prevT = t;
        }

        // ---- shared line feed (tags that exist in both the real log and the host LogSink) ----
        static string Tok(string msg, string key)
        {
            int i = msg.IndexOf(key, StringComparison.Ordinal); if (i < 0) return null; i += key.Length;
            int j = msg.IndexOf(' ', i); return j < 0 ? msg.Substring(i) : msg.Substring(i, j - i);
        }
        static int Uid(string tok) { return tok != null && tok.Length > 1 && tok[0] == 'u' && int.TryParse(tok.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : -1; }
        static float Fl(string s, float def) { return s != null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : def; }

        /// <summary>Returns false for tags this record ignores. realHits: take HIT lines (real log); the sim feeds hits from its own tap.</summary>
        public void Feed(float t, string tag, string msg, bool realHits)
        {
            switch (tag)
            {
                case "STRIKE": AddStrike(); break;
                case "SKIP": { int gt = msg.IndexOf('>'); AddSkip(Uid(gt > 0 ? msg.Substring(0, gt) : null)); break; }
                case "KILL":
                {
                    string[] w = msg.Split(' ');
                    int vs = w.Length > 1 && w[1].Length > 1 && w[1][0] == 's' ? (w[1][1] == '1' ? 1 : 0) : SideId(Uid(w[0]));
                    AddKill(t, vs); break;
                }
                case "HIT":
                {
                    if (!realHits) break;
                    int gt = msg.IndexOf('>');
                    AddHit(t, Uid(gt > 0 ? msg.Substring(0, gt) : null), Fl(Tok(msg, "dist="), -1f), Fl(Tok(msg, "D="), -1f));
                    break;
                }
                case "END":
                {
                    string w = Tok(msg, "winner="); int win = 2;
                    if (w == "player" || w == "0") win = 0; else if (w == "bot" || w == "1") win = 1;
                    int r = msg.IndexOf("reason=", StringComparison.Ordinal);
                    string reason = r < 0 ? "" : msg.Substring(r + 7);
                    int ti = reason.IndexOf(" t=", StringComparison.Ordinal); if (ti > 0) reason = reason.Substring(0, ti);
                    AddEnd(t, win, reason); break;
                }
            }
        }

        public static string Category(string reason)
        {
            if (reason.Contains("escape")) return "kill and escape";
            if (reason.Contains("space control")) return "space control";
            if (reason.Contains("all enemy units dead") || reason.Contains("everyone dead")) return "all dead";
            if (reason.Contains("safe zone")) return "safe zone";
            return "other";
        }

        // ---- metrics ----
        static float Q(List<float> v, double q)
        {
            if (v.Count == 0) return float.NaN;
            var a = v.ToArray(); Array.Sort(a);
            double pos = q * (a.Length - 1); int lo = (int)Math.Floor(pos), hi = Math.Min(a.Length - 1, lo + 1);
            return (float)(a[lo] + (a[hi] - a[lo]) * (pos - lo));
        }

        public void Metrics(double[] m)
        {
            int k = 0; double nan = double.NaN;
            m[k++] = Winner == 1 ? 1 : 0;                 // win_bot
            m[k++] = Winner == 0 ? 1 : 0;                 // win_player
            m[k++] = Winner >= 2 || Winner < 0 ? 1 : 0;   // none
            string cat = Category(Reason);
            foreach (string c in Cats) m[k++] = cat == c ? 1 : 0;
            m[k++] = EndT;
            m[k++] = Kills[0]; m[k++] = Kills[1];
            m[k++] = Hits[0]; m[k++] = Hits[1];
            m[k++] = Skips[0]; m[k++] = Skips[1];
            m[k++] = EndT > 1f ? (Hits[0] + Hits[1]) / (EndT / 60.0) : nan;
            m[k++] = float.IsNaN(FirstKillT) ? nan : FirstKillT;
            m[k++] = FirstKillSide < 0 ? nan : (FirstKillSide == 1 ? 1 : 0);
            m[k++] = _dist.Count > 0 ? _dist.Average() : nan;
            m[k++] = Q(_d, 0.5); m[k++] = Q(_d, 0.1); m[k++] = Q(_d, 0.9);
            m[k++] = Q(_gap, 0.5);
            m[k++] = Strikes > 0 ? Math.Max(0.0, 1.0 - (double)(Hits[0] + Hits[1]) / Strikes) : nan;
            m[k++] = _spN > 0 ? _spSum / _spN : nan;
            m[k++] = _cdN > 0 ? _cdSum / _cdN : nan;
        }

        public static readonly string[] Cats = { "kill and escape", "space control", "all dead", "safe zone", "other" };
        public static readonly (string name, bool prop)[] Spec =
        {
            ("win rate bot (side 1)", true), ("win rate player (side 0)", true), ("no winner / time limit", true),
            ("reason: kill and escape", true), ("reason: space control", true), ("reason: all dead", true), ("reason: safe zone", true), ("reason: other", true),
            ("match length t (s)", false),
            ("kills side 0", false), ("kills side 1", false), ("hits side 0", false), ("hits side 1", false), ("skips side 0", false), ("skips side 1", false),
            ("hits per minute", false), ("time to first kill (s)", false), ("first blood by bot (of matches with a kill)", true),
            ("mean HIT dist (m)", false), ("strike->damage D median (ms)", false), ("D p10 (ms)", false), ("D p90 (ms)", false),
            ("gap between hits, same attacker, median (ms)", false), ("fraction of STRIKE without HIT", false),
            ("mean speed of moving units (m/s)", false), ("mean centroid distance (m)", false),
        };
    }

    // =====================================================================================
    sealed class Cfg { public int Units; public bool Skip, Heal; }

    static Cfg ParseCfg(string msg)
    {
        var c = new Cfg();
        foreach (string w in msg.Split(' '))
        {
            if (w.StartsWith("skip=")) c.Skip = w == "skip=True";
            else if (w.StartsWith("heal=")) c.Heal = w == "heal=True";
            else if (w.StartsWith("player=") && w.Contains('x')) int.TryParse(w.Substring(w.LastIndexOf('x') + 1), out c.Units);
        }
        return c;
    }

    /// <summary>Real log -> MatchRec, or null when it has no END / does not match the wanted config.</summary>
    static MatchRec ParseReal(string path, int units, bool skips, bool heal, int arena, out string why)
    {
        why = null;
        string[] lines;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs); lines = sr.ReadToEnd().Replace("\r", "").Split('\n');
        }
        catch (Exception e) { why = "unreadable " + e.Message; return null; }
        var rec = new MatchRec { Units = units };
        Cfg cfg = null; bool end = false;
        var ids = new int[64]; var xs = new float[64]; var zs = new float[64]; var al = new bool[64];
        foreach (string ln in lines)
        {
            int a = ln.IndexOf('|'); if (a < 0) continue;
            int b = ln.IndexOf('|', a + 1); if (b < 0) continue;
            if (!float.TryParse(ln.AsSpan(0, a), NumberStyles.Float, CultureInfo.InvariantCulture, out float t)) continue;
            string tag = ln.Substring(a + 1, b - a - 1), msg = ln.Substring(b + 1);
            switch (tag)
            {
                case "CFG": cfg = ParseCfg(msg); break;
                case "UNIT":
                {
                    string[] w = msg.Split(' ');
                    if (w.Length > 1 && w[0].Length > 1 && int.TryParse(w[0].AsSpan(1), out int id)) rec.SideOf[id] = w[1] == "s1" ? 1 : 0;
                    break;
                }
                case "SNAP":
                {
                    int n = 0;
                    foreach (string tok in msg.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] p = tok.Split(':'); if (p.Length < 4 || n >= 64) continue;
                        string[] xz = p[2].Split(',');
                        ids[n] = int.Parse(p[0], CultureInfo.InvariantCulture);
                        xs[n] = float.Parse(xz[0], CultureInfo.InvariantCulture); zs[n] = float.Parse(xz[1], CultureInfo.InvariantCulture);
                        al[n] = float.Parse(p[3], CultureInfo.InvariantCulture) > 0f; n++;
                    }
                    rec.AddSnap(t, n, ids, xs, zs, al);
                    break;
                }
                case "END": rec.Feed(t, tag, msg, true); end = true; break;
                default: rec.Feed(t, tag, msg, true); break;
            }
        }
        if (!end) { why = "no END"; return null; }
        if (cfg == null) { why = "no CFG"; return null; }
        if (cfg.Skip != skips || cfg.Heal != heal) { why = "cfg skip/heal"; return null; }
        if (units > 0 && cfg.Units != units) { why = "cfg units"; return null; }
        int realArena = rec.Width > 200f ? 3 : 1;
        if (realArena != arena) { why = "arena"; return null; }
        return rec;
    }

    // =====================================================================================
    sealed class SimTap : ISimListener
    {
        public MatchRec Rec; public SimBotHost Host; public DuelSim Sim;
        readonly float[] _strikeT = new float[64];
        public void Reset(MatchRec r) { Rec = r; Array.Fill(_strikeT, -1f); }
        public void OnStrike(int unit, int target) { if (unit >= 0 && unit < 64) _strikeT[unit] = Host.FightTime; }
        public void OnHit(int attacker, int victim, float dealt, float hpAfter)
        {
            if (attacker < 0 || victim < 0 || attacker >= Sim.Count || victim >= Sim.Count) return;
            if (Sim.Side[attacker] == Sim.Side[victim]) return;
            float dx = Sim.X[attacker] - Sim.X[victim], dz = Sim.Z[attacker] - Sim.Z[victim];
            float d = attacker < 64 && _strikeT[attacker] >= 0f ? (Host.FightTime - _strikeT[attacker]) * 1000f : -1f;
            Rec.AddHit(Host.FightTime, attacker, MathF.Sqrt(dx * dx + dz * dz), d);
        }
        public void OnKill(int victim, int killer) { }
        public void OnEnd(int winner, string reason) { }
    }

    static MatchRec[] RunSim(DuelOppDef aDef, DuelOppDef bDef, SimRules rulesT, Dictionary<string, string> prm, int n, int threads, ulong seed)
    {
        var res = new MatchRec[n];
        threads = Math.Max(1, Math.Min(threads, n));
        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, th =>
        {
            var sim = new DuelSim(); var host = new SimBotHost(sim);
            var tap = new SimTap { Host = host, Sim = sim }; host.Downstream = tap;
            MatchRec cur = null;
            var ids = new int[64]; var xs = new float[64]; var zs = new float[64]; var al = new bool[64];
            for (int ep = th; ep < n; ep += threads)
            {
                var p = SimParams.Default(); ApplyParams(p, prm);
                sim.P = p;
                var rules = new SimRules
                {
                    UnitsPerSide = rulesT.UnitsPerSide, ArenaScale = rulesT.ArenaScale, Skips = rulesT.Skips, InfiniteHeal = rulesT.InfiniteHeal,
                    TimeLimit = rulesT.TimeLimit, SettleTime = rulesT.SettleTime, SpaceControlSec = rulesT.SpaceControlSec
                };
                ApplyRules(rules, prm);
                cur = new MatchRec { Units = rules.UnitsPerSide };
                tap.Reset(cur);
                var rec = cur;
                host.Begin(rules, Rng.Mix(seed, (ulong)ep + 100000), new SimBotSetup
                {
                    Side0 = aDef.Bot(), Side1 = bDef.Bot(), LogBotOrders = false,
                    LogSink = (t, tag, msg) => { if (tag != "HIT") rec.Feed(t, tag, msg, false); }
                });
                float nextSnap = 0f; int guard = 0, maxFrames = (int)((rules.TimeLimit + 30f) * 400f);
                while (!sim.Over && guard++ < maxFrames)
                {
                    host.Update(sim.RenderDt);
                    if (host.FightTime >= nextSnap)
                    {
                        nextSnap = host.FightTime + 0.2f;
                        int c = sim.Count;
                        for (int i = 0; i < c && i < 64; i++) { ids[i] = i; xs[i] = sim.X[i]; zs[i] = sim.Z[i]; al[i] = !sim.Dead[i]; }
                        rec.AddSnap(host.FightTime, Math.Min(c, 64), ids, xs, zs, al);
                    }
                }
                if (rec.Winner == -9) rec.AddEnd(host.FightTime, 2, "time limit");
                res[ep] = rec;
            }
            SimBotHost.DeactivateThread();
        });
        return res;
    }

    static void ApplyParams(SimParams p, Dictionary<string, string> prm)
    {
        foreach (var kv in prm)
        {
            string k = kv.Key;
            if (k.StartsWith("rules.", StringComparison.OrdinalIgnoreCase)) continue;
            if (k.Equals("fps", StringComparison.OrdinalIgnoreCase)) k = "RenderFps";
            var f = typeof(SimParams).GetField(k, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (f == null) throw new ArgumentException("unknown SimParams field '" + kv.Key + "'");
            f.SetValue(p, Convert.ChangeType(kv.Value, f.FieldType, CultureInfo.InvariantCulture));
        }
    }

    static void ApplyRules(SimRules r, Dictionary<string, string> prm)
    {
        foreach (var kv in prm)
        {
            if (!kv.Key.StartsWith("rules.", StringComparison.OrdinalIgnoreCase)) continue;
            var f = typeof(SimRules).GetField(kv.Key.Substring(6), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (f == null) throw new ArgumentException("unknown SimRules field '" + kv.Key + "'");
            f.SetValue(r, Convert.ChangeType(kv.Value, f.FieldType, CultureInfo.InvariantCulture));
        }
    }

    // =====================================================================================
    sealed class Agg
    {
        public int N; public double Mean, Sd, Ci, Lo, Hi, K;
    }

    static Agg Aggregate(IEnumerable<double> vals, bool prop)
    {
        var v = vals.Where(x => !double.IsNaN(x)).ToArray();
        var g = new Agg { N = v.Length };
        if (g.N == 0) { g.Mean = double.NaN; return g; }
        g.Mean = v.Average(); g.K = v.Sum();
        if (prop)
        {
            (g.Lo, g.Hi) = Wilson((int)Math.Round(g.K), g.N); g.Lo = Math.Max(0, g.Lo); g.Hi = Math.Min(1, g.Hi);
            g.Sd = Math.Sqrt(g.Mean * (1 - g.Mean));
            g.Ci = (g.Hi - g.Lo) / 2;
        }
        else
        {
            g.Sd = g.N > 1 ? Math.Sqrt(v.Sum(x => (x - g.Mean) * (x - g.Mean)) / (g.N - 1)) : 0;
            g.Ci = 1.96 * g.Sd / Math.Sqrt(g.N); g.Lo = g.Mean - g.Ci; g.Hi = g.Mean + g.Ci;
        }
        return g;
    }

    static (double lo, double hi) Wilson(int k, int n, double z = 1.96)
    {
        if (n == 0) return (0, 1);
        double p = (double)k / n, d = 1 + z * z / n, c = p + z * z / (2 * n), m = z * Math.Sqrt(p * (1 - p) / n + z * z / (4.0 * n * n));
        return ((c - m) / d, (c + m) / d);
    }

    static string Fmt(double v, bool prop) => double.IsNaN(v) ? "n/a" : prop ? (v * 100).ToString("F1", CultureInfo.InvariantCulture) + "%" : v.ToString(Math.Abs(v) >= 100 ? "F0" : "F2", CultureInfo.InvariantCulture);
    static string FmtCi(Agg g, bool prop) => g.N == 0 ? "n/a" : prop
        ? $"{g.Mean * 100:F1}% [{g.Lo * 100:F1}, {g.Hi * 100:F1}]"
        : $"{Fmt(g.Mean, false)} ± {Fmt(g.Ci, false)}";

    // =====================================================================================
    static string RepoRoot()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "docs", "sim"))) return d.FullName;
        return Directory.GetCurrentDirectory();
    }

    public static int Run(string[] argv)
    {
        if (argv.Length > 0 && argv[0].StartsWith("--")) argv = new[] { "fidelity" }.Concat(argv).ToArray();
        var a = new Args(argv);
        string root = RepoRoot();
        string realArg = a.Str("real", "");
        string aName = a.Str("a", "v2"), bName = a.Str("b", "prod4");
        int units = a.Int("units", 10), arena = a.Int("arena", 3), n = a.Int("n", 400), threads = a.Int("threads", Environment.ProcessorCount);
        bool skips = a.Str("skips", "1") != "0" && a.Str("skips", "1") != "false", heal = a.Str("heal", "0") != "0" && a.Str("heal", "0") != "false";
        ulong seed = (ulong)a.Long("seed", 4242);
        float timeLimit = (float)a.Dbl("time-limit", 300);
        int minReal = a.Int("min-real", 1);
        string outPath = a.Str("out", Path.Combine(root, "docs", "sim", "fidelity.md"));
        string prmStr = a.Str("params", "");
        foreach (var kv in a.Unused) throw new ArgumentException("unknown option --" + kv.Key);
        if (realArg.Length == 0) throw new ArgumentException("--real <dir1,dir2,..> is required");
        var prm = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string kv in prmStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int e = kv.IndexOf('='); if (e <= 0) throw new ArgumentException("--params expects key=value,..");
            prm[kv.Substring(0, e)] = kv.Substring(e + 1);
        }

        // ---- real side ----
        var real = new List<MatchRec>(); int files = 0; var skipWhy = new Dictionary<string, int>();
        foreach (string d0 in realArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string dir = Directory.Exists(d0) ? d0 : Path.Combine(root, d0);
            if (!Directory.Exists(dir)) { Console.WriteLine("warning: no such real dir " + d0); continue; }
            foreach (string f in Directory.GetFiles(dir, "duel-*.txt", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            {
                files++;
                var r = ParseReal(f, units, skips, heal, arena, out string why);
                if (r != null) real.Add(r); else skipWhy[why ?? "?"] = skipWhy.GetValueOrDefault(why ?? "?") + 1;
            }
        }
        Console.WriteLine($"real: {real.Count} usable of {files} logs" + (skipWhy.Count > 0 ? " (skipped: " + string.Join(", ", skipWhy.Select(kv => kv.Value + "x " + kv.Key)) + ")" : ""));
        if (real.Count < minReal) { Console.WriteLine("not enough real matches"); return 1; }

        // ---- sim side ----
        var aDef = DuelOpponents.Get(a.Str("a", "v2"), new PolicyRef(null)); var bDef = DuelOpponents.Get(bName, new PolicyRef(null));
        var rulesT = new SimRules { UnitsPerSide = units, ArenaScale = arena, Skips = skips, InfiniteHeal = heal, TimeLimit = timeLimit };
        var sw = Stopwatch.StartNew();
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
        var sim = RunSim(aDef, bDef, rulesT, prm, n, threads, seed);
        Console.WriteLine($"sim: {sim.Length} matches in {sw.Elapsed.TotalSeconds:F1}s ({threads} threads)");

        // ---- compare ----
        int M = MatchRec.Spec.Length;
        var rv = real.Select(r => { var m = new double[M]; r.Metrics(m); return m; }).ToList();
        var sv = sim.Select(r => { var m = new double[M]; r.Metrics(m); return m; }).ToList();
        var sb = new StringBuilder();
        string date = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        sb.Append($"\n## Closed-loop run {date}\n\n");
        sb.Append($"- matchup: side 0 `{aName}` vs side 1 `{bName}`, units {units}v{units}, arena x{arena}, skips {(skips ? "on" : "off")}, heal {(heal ? "on" : "off")}, time limit {timeLimit:0}s\n");
        sb.Append($"- real: {real.Count} matches from `{realArg}` ({files} logs scanned); sim: n={sim.Length}, seed={seed}, threads={threads}, params: {(prm.Count == 0 ? "defaults (RenderFps 60)" : prmStr)}\n");
        sb.Append($"- bot statics: Feat={SimBotDefaults.Feat} Feat2={SimBotDefaults.Feat2}; verdict: OK if |z| < 2 or relative diff < 10%\n\n");
        sb.Append("| metric | real mean ± 95% CI (n) | sim mean ± 95% CI (n) | diff (sim-real) | z | verdict |\n|---|---|---|---|---|---|\n");
        int mismatches = 0; var worst = new List<(double z, string name, string real, string sim)>();
        for (int i = 0; i < M; i++)
        {
            var (name, prop) = MatchRec.Spec[i];
            var gr = Aggregate(rv.Select(x => x[i]), prop); var gs = Aggregate(sv.Select(x => x[i]), prop);
            string verdict, zs = "n/a", ds = "n/a";
            if (gr.N == 0 || gs.N == 0) verdict = "n/a";
            else
            {
                double diff = gs.Mean - gr.Mean, z;
                if (prop)
                {
                    double pp = (gr.K + gs.K) / (gr.N + gs.N), se = Math.Sqrt(pp * (1 - pp) * (1.0 / gr.N + 1.0 / gs.N));
                    z = se > 0 ? diff / se : (Math.Abs(diff) < 1e-12 ? 0 : double.PositiveInfinity);
                }
                else
                {
                    double se = Math.Sqrt(gr.Sd * gr.Sd / gr.N + gs.Sd * gs.Sd / gs.N);
                    z = se > 0 ? diff / se : (Math.Abs(diff) < 1e-12 ? 0 : double.PositiveInfinity);
                }
                double rel = Math.Abs(diff) / Math.Max(Math.Abs(gr.Mean), 1e-9);
                if (Math.Abs(diff) < 1e-12) rel = 0;
                bool ok = Math.Abs(z) < 2 || rel < 0.10;
                verdict = ok ? "OK" : "MISMATCH";
                if (!ok) { mismatches++; worst.Add((Math.Abs(z), name, FmtCi(gr, prop), FmtCi(gs, prop))); }
                zs = double.IsInfinity(z) ? "inf" : z.ToString("F1", CultureInfo.InvariantCulture);
                ds = prop ? (diff * 100).ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + " pp" : diff.ToString(Math.Abs(diff) >= 100 ? "+0;-0" : "+0.00;-0.00", CultureInfo.InvariantCulture)
                     + (rel >= 0.005 ? $" ({(gs.Mean - gr.Mean) / Math.Max(Math.Abs(gr.Mean), 1e-9) * 100:+0;-0}%)" : "");
            }
            sb.Append($"| {name} | {FmtCi(gr, prop)} ({gr.N}) | {FmtCi(gs, prop)} ({gs.N}) | {ds} | {zs} | {verdict} |\n");
        }
        sb.Append($"\nMISMATCH rows: {mismatches} of {M} (no multiple-comparison correction: expect ~1 false alarm per 20 rows).\n");
        if (worst.Count > 0)
        {
            sb.Append("\nLargest mismatches by |z|: ");
            sb.Append(string.Join("; ", worst.OrderByDescending(w => w.z).Take(5).Select(w => $"{w.name} (z={w.z:F1}, real {w.real}, sim {w.sim})")));
            sb.Append('\n');
        }
        string text = sb.ToString();
        Console.WriteLine(text);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            File.AppendAllText(outPath, text);
            Console.WriteLine("appended to " + outPath);
        }
        catch (Exception e) { Console.WriteLine("could not write " + outPath + ": " + e.Message); }
        return mismatches;
    }
}
