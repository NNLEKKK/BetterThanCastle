using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using static Btc.Mcp.Registry;

namespace Btc.Mcp;

/// <summary>status, api, ref_grep.</summary>
public static class InfoTools
{
    public static void Register(List<Tool> l)
    {
        l.Add(T("status", "STATUS.md '## NEXT' section + last 5 commits + count of changed files.", "", "", Status));
        l.Add(T("api", "scripts/api.ps1: index line and members of a vanilla type.", P(Str("type", "type name"), Str("member", "member substring filter")), "type", Api));
        l.Add(T("ref_grep", "Regex search in ref/vanilla *.cs only, bounded output (file:line text).",
            P(Str("pattern", "regex"), Str("path", "subdir of ref/vanilla"), Int("max", "max hits, default 30"), Bool("ignoreCase", "case-insensitive")), "pattern", RefGrep));
    }

    static async Task<ToolResult> Status(Args a)
    {
        var sb = new StringBuilder();
        string st = Env.P("docs", "STATUS.md");
        if (File.Exists(st))
        {
            var lines = File.ReadAllLines(st);
            int i = Array.FindIndex(lines, s => s.StartsWith("## NEXT"));
            if (i >= 0)
            {
                sb.AppendLine(lines[i]);
                foreach (var s in lines.Skip(i + 1).TakeWhile(s => !s.StartsWith("## "))) if (s.Trim().Length > 0) sb.AppendLine(Txt.Head(s, null, 400));
            }
            else sb.AppendLine("(no '## NEXT' section)");
        }
        var (_, log, _) = await Proc.Run(ProcSpec.Git("log", "--oneline", "-5"), 15000);
        var (_, stat, _) = await Proc.Run(ProcSpec.Git("status", "--short"), 20000);
        sb.AppendLine("--- git log").Append(log.TrimEnd()).AppendLine();
        sb.Append("--- git status --short: ").Append(Txt.Lines(stat.TrimEnd()).Count(s => s.Length > 0)).Append(" entries");
        return new ToolResult(Txt.Head(sb.ToString(), "docs/STATUS.md"));
    }

    static async Task<ToolResult> Api(Args a)
    {
        var (ex, o, to) = await Proc.Run(ProcSpec.Ps("api.ps1", ("Type", a.Str("type")), ("Member", a.Has("member") ? a.Str("member") : null)), 60000);
        return new ToolResult(Txt.Head(o.Trim(), "docs/gen/game-api-index.md"), ex != 0 || to);
    }

    static async Task<ToolResult> RefGrep(Args a)
    {
        string path = a.Str("path"); int max = Math.Clamp(a.Int("max", 30), 1, 100);
        if (path.Contains("..") || Path.IsPathRooted(path)) return new ToolResult("bad path", true);
        string root = Env.P("ref", "vanilla");
        string start = path.Length > 0 ? Path.Combine(root, path) : root;
        if (!Directory.Exists(start)) return new ToolResult("no such dir under ref/vanilla: " + path, true);
        Regex rx;
        try { rx = new Regex(a.Str("pattern"), a.Bool("ignoreCase") ? RegexOptions.IgnoreCase : RegexOptions.None, TimeSpan.FromSeconds(2)); }
        catch (Exception e) { return new ToolResult("bad regex: " + e.Message, true); }
        return await Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            var hits = new List<string>(); int files = 0; bool cut = false;
            foreach (var f in Directory.EnumerateFiles(start, "*.cs", SearchOption.AllDirectories))
            {
                files++;
                if (sw.Elapsed.TotalSeconds > 40) { cut = true; break; }
                int n = 0;
                try
                {
                    foreach (var line in File.ReadLines(f))
                    {
                        n++;
                        bool m; try { m = rx.IsMatch(line); } catch (RegexMatchTimeoutException) { m = false; }
                        if (m)
                        {
                            hits.Add($"{f.Substring(root.Length + 1)}:{n}: {Txt.Head(line.Trim(), null, 180)}");
                            if (hits.Count >= max) { cut = true; break; }
                        }
                    }
                }
                catch { }
                if (hits.Count >= max) break;
            }
            string head = $"{hits.Count} hit(s) in {files} file(s) scanned{(cut ? " (stopped at limit/time)" : "")}";
            return new ToolResult(Txt.Head(head + "\n" + string.Join("\n", hits), null));
        });
    }
}
