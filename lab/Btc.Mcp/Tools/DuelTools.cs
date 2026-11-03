using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static Btc.Mcp.Registry;

namespace Btc.Mcp;

/// <summary>scenario_run, duel_par, duel_logs, duel_ab.</summary>
public static class DuelTools
{
    public static void Register(List<Tool> l)
    {
        l.Add(T("scenario_run", "Job: scripts/scenario.ps1 (hidden DEV game runs tests/scenarios/<name>.txt). Skips if a game is running.",
            P(Str("name", "scenario file name without .txt"), Int("clients", "MP clients"), Int("bc", "BetterConnection 0/1"), Int("maxSeconds", "script timeout")), "name", Scenario));
        l.Add(T("duel_par", "Job: scripts/duel-par.ps1, parallel hidden duel games (one per entry of featList), prints a tally per instance.",
            P(Str("featList", "comma list of feature masks, one game each"), Int("units", "units per side"), Int("matches", "matches per game"), Int("opp", "opponent kind"),
              Int("oppTier", "opponent tier"), Int("oppLevel", "opponent level"), Int("skip", "skip toggle 0/1"), Str("extra", "extra scenario lines separated by ;"),
              Bool("noBuild", "skip the Debug build"), Int("maxSeconds", "overall timeout")), "featList", DuelPar));
        l.Add(T("duel_logs", "Parse duel logs: one row per match (CFG + END) and totals. dir: '' (main) | par0..par3 | kirusha.",
            P(Str("dir", "'' | par0..par3 | kirusha"), Int("last", "rows to show, default 20")), "", DuelLogs));
        l.Add(T("duel_ab", "Compare bot win rate of two sets of duel-log dirs (comma lists, e.g. a='par0,par1' b='par2') with Wilson 95% CI and a two-proportion verdict.",
            P(Str("a", "dir label(s) for set A"), Str("b", "dir label(s) for set B"), Int("last", "use only the newest N matches per set")), "a,b", DuelAb));
    }

    static Task<ToolResult> Scenario(Args a)
    {
        var spec = ProcSpec.Ps("scenario.ps1", ("Name", a.Str("name")), ("Clients", a.Has("clients") ? a.Int("clients", 0).ToString() : null),
            ("Bc", a.Has("bc") ? a.Int("bc", 1).ToString() : null), ("MaxSeconds", a.Has("maxSeconds") ? a.Int("maxSeconds", 420).ToString() : null));
        return Jobs.RunOrJob("scenario " + a.Str("name"), spec, 10, a.Int("maxSeconds", 420) + 600, (o, ex) => Txt.LastLines(o, 25),
            j => j.State != "done" || Jobs.Output(j).Contains("SKIP") || Jobs.Output(j).Contains("FAIL"));
    }

    static Task<ToolResult> DuelPar(Args a)
    {
        string N(string k) => a.Has(k) ? a.Int(k, 0).ToString() : null;
        var spec = ProcSpec.Ps("duel-par.ps1", ("FeatList", a.Str("featList")), ("Units", N("units")), ("Matches", N("matches")), ("Opp", N("opp")),
            ("OppTier", N("oppTier")), ("OppLevel", N("oppLevel")), ("Skip", N("skip")), ("MaxSeconds", N("maxSeconds")),
            ("Extra", a.Has("extra") ? a.Str("extra") : null), ("NoBuild", a.Bool("noBuild") ? "" : null));
        return Jobs.RunOrJob("duel_par " + a.Str("featList"), spec, 15, a.Int("maxSeconds", 1500) + 900, (o, ex) => Txt.LastLines(o, 25),
            j => j.State != "done" || Jobs.Output(j).Contains("DUELPAR SKIP") || Jobs.Output(j).Contains("DUELPAR FAIL"));
    }

    // ---- duel logs ----

    public sealed class Match
    {
        public string Name, Diff, Skip, Heal, Player, Arena, Winner = "noend", Reason = "", Hits = "", Skips = "", Kills = "", Id = "";
        public double T; public int BotLosses, OppLosses; public DateTime When;
    }

    static readonly Regex KvRx = new(@"(\w+)=(\S+)", RegexOptions.Compiled);
    static readonly Regex EndRx = new(@"winner=(\w+) reason=(.*?) t=([0-9.]+) hits=(\S+) skips=(\S+) kills=(\S+)", RegexOptions.Compiled);
    static readonly Regex KillRx = new(@"\|KILL\|u\d+ s(\d) ", RegexOptions.Compiled);

    public static string DirFor(string token)
    {
        token = token.Trim();
        string root = Path.Combine(Env.Game, "BetterThanCastle");
        if (token.Length == 0 || token == "main") return Path.Combine(root, "duel-logs");
        if (token == "kirusha") return Path.Combine(root, "duel-logs-kirusha");
        if (token.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || token.Contains("..")) return null;
        return Path.Combine(root, "duel-logs", token);
    }

    public static List<Match> Load(string token)
    {
        var res = new List<Match>();
        string d = DirFor(token);
        if (d == null || !Directory.Exists(d)) return res;
        foreach (var f in Directory.GetFiles(d, "duel-*.txt", token == "kirusha" ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
        {
            var m = new Match { Name = Path.GetFileNameWithoutExtension(f), When = File.GetLastWriteTime(f) };
            foreach (var line in File.ReadLines(f))
            {
                if (line.Contains("|CFG|"))
                {
                    var kv = KvRx.Matches(line).Cast<System.Text.RegularExpressions.Match>().GroupBy(x => x.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
                    string G(string k) => kv.TryGetValue(k, out var v) ? v : "?";
                    m.Id = G("match"); m.Diff = G("diff"); m.Skip = G("skip") == "True" ? "T" : "F"; m.Heal = G("heal") == "True" ? "T" : "F";
                    m.Player = G("player").Replace("Halberdier", "H"); m.Arena = G("arenaX0") + "," + G("Z0");
                }
                else if (line.Contains("|END|"))
                {
                    var e = EndRx.Match(line);
                    if (e.Success)
                    {
                        m.Winner = e.Groups[1].Value; m.Reason = e.Groups[2].Value;
                        double.TryParse(e.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out m.T);
                        m.Hits = e.Groups[4].Value; m.Skips = e.Groups[5].Value; m.Kills = e.Groups[6].Value;
                    }
                }
                else if (line.Contains("|KILL|"))
                {
                    var k = KillRx.Match(line);
                    if (k.Success) { if (k.Groups[1].Value == "1") m.BotLosses++; else m.OppLosses++; }
                }
            }
            res.Add(m);
        }
        return res.OrderBy(m => m.When).ThenBy(m => m.Name).ToList();
    }

    static Task<ToolResult> DuelLogs(Args a)
    {
        string dir = a.Str("dir"); int last = a.Int("last", 20);
        if (DirFor(dir) == null) return Fail("bad dir");
        var all = Load(dir);
        if (all.Count == 0) return Fail($"no duel-*.txt in {Txt.Rel(DirFor(dir))}");
        var sb = new StringBuilder();
        sb.AppendLine($"{Txt.Rel(DirFor(dir))}: {all.Count} matches (showing last {Math.Min(last, all.Count)})");
        foreach (var m in all.Skip(Math.Max(0, all.Count - last)))
            sb.AppendLine($"#{m.Id} {m.Name.Replace("duel-2026", "").Replace("duel-", "")} | {m.Diff} sk={m.Skip} h={m.Heal} {m.Player} a={m.Arena} | {m.Winner} {m.Reason} {m.T:F1}s hit={m.Hits} skip={m.Skips} kill={m.Kills}");
        sb.Append(Totals(all, "ALL"));
        return Ok(Txt.Head(sb.ToString(), null));
    }

    static string Totals(List<Match> ms, string label)
    {
        int bot = ms.Count(m => m.Winner == "bot"), pl = ms.Count(m => m.Winner == "player"), other = ms.Count - bot - pl;
        int dec = bot + pl;
        double mean = dec > 0 ? ms.Where(m => m.Winner == "bot" || m.Winner == "player").Average(m => m.T) : 0;
        double bl = dec > 0 ? ms.Where(m => m.Winner == "bot" || m.Winner == "player").Average(m => m.BotLosses) : 0;
        return $"{label}: n={ms.Count} bot={bot} player={pl} other={other} meanLen={mean:F1}s botLosses/match={bl:F2}";
    }

    public static (double lo, double hi) Wilson(int k, int n)
    {
        if (n == 0) return (0, 1);
        double z = 1.96, p = (double)k / n, z2 = z * z;
        double c = (p + z2 / (2 * n)) / (1 + z2 / n);
        double h = z * Math.Sqrt(p * (1 - p) / n + z2 / (4.0 * n * n)) / (1 + z2 / n);
        return (Math.Max(0, c - h), Math.Min(1, c + h));
    }

    static Task<ToolResult> DuelAb(Args a)
    {
        int last = a.Int("last", 0);
        (string label, List<Match> ms) Set(string spec)
        {
            var all = new List<Match>();
            foreach (var tok in spec.Split(',', StringSplitOptions.TrimEntries)) all.AddRange(Load(tok));
            all = all.OrderBy(m => m.When).ToList();
            if (last > 0 && all.Count > last) all = all.Skip(all.Count - last).ToList();
            return (spec, all.Where(m => m.Winner == "bot" || m.Winner == "player").ToList());
        }
        var A = Set(a.Str("a")); var B = Set(a.Str("b"));
        if (A.ms.Count == 0 || B.ms.Count == 0) return Fail($"empty set: A n={A.ms.Count}, B n={B.ms.Count} (tokens: '' | parN | kirusha; comma separated)");
        string Row((string label, List<Match> ms) s)
        {
            int k = s.ms.Count(m => m.Winner == "bot"), n = s.ms.Count;
            var (lo, hi) = Wilson(k, n);
            return $"{s.label}: bot wins {k}/{n} = {100.0 * k / n:F0}% CI95 [{100 * lo:F0}%..{100 * hi:F0}%] botLosses/match={s.ms.Average(m => m.BotLosses):F2} meanLen={s.ms.Average(m => m.T):F1}s";
        }
        int ka = A.ms.Count(m => m.Winner == "bot"), na = A.ms.Count, kb = B.ms.Count(m => m.Winner == "bot"), nb = B.ms.Count;
        double pa = (double)ka / na, pb = (double)kb / nb, pp = (double)(ka + kb) / (na + nb);
        double se = Math.Sqrt(pp * (1 - pp) * (1.0 / na + 1.0 / nb));
        double z = se > 0 ? (pa - pb) / se : 0;
        string verdict = Math.Abs(z) < 1.96 ? "no significant difference" : (z > 0 ? "A better" : "B better");
        var (alo, ahi) = Wilson(ka, na); var (blo, bhi) = Wilson(kb, nb);
        string overlap = alo > bhi || blo > ahi ? "CIs disjoint" : "CIs overlap";
        return Ok($"A {Row(A)}\nB {Row(B)}\nz={z:F2} ({overlap}) verdict: {verdict} (bot win rate; two-sided 5%)");
    }
}
