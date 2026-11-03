using System.Text;
using System.Text.RegularExpressions;
using static Btc.Mcp.Registry;

namespace Btc.Mcp;

/// <summary>logs: digest | agent | hits | errors from BTClogs.</summary>
public static class LogTools
{
    public static void Register(List<Tool> l)
    {
        l.Add(T("logs", "Read compact BTClogs: kind digest|agent|hits|errors. grep = regex filter (agent/digest/errors), last = N lines (default 60), dir = BTClogs subdir (par0...), tag = build tag for errors.",
            P(Str("kind", "digest|agent|hits|errors"), Str("grep", "regex filter"), Int("last", "max lines, default 60"), Str("dir", "BTClogs subdir"), Str("tag", "build-errors tag")), "kind", Logs));
    }

    static Task<ToolResult> Logs(Args a)
    {
        string kind = a.Str("kind"), grep = a.Str("grep"), dir = a.Str("dir"), tag = a.Str("tag");
        int last = a.Int("last", 60);
        if (dir.Contains("..") || dir.IndexOfAny(new[] { '/', '\\', ':' }) >= 0) return Fail("bad dir");
        string d = dir.Length > 0 ? Path.Combine(Env.Logs, dir) : Env.Logs;
        string file = kind switch
        {
            "digest" => "digest.txt", "agent" => "agent.log", "hits" => "hits.txt",
            "errors" => tag.Length > 0 ? $"build-errors-{tag}.txt" : "build-errors.txt",
            _ => null,
        };
        if (file == null) return Fail("kind must be digest|agent|hits|errors");
        string path = Path.Combine(d, file);
        if (!File.Exists(path)) return Fail("missing " + Txt.Rel(path));
        var lines = Txt.Lines(Txt.ReadShared(path, 2_000_000)).ToList();
        if (kind == "hits")
        {
            int i = lines.FindIndex(s => s.StartsWith("# --- never"));
            var head = lines.Count > 0 && lines[0].StartsWith("#") ? lines[0] : "";
            if (i < 0) return Ok(head + "\n(no 'never called' section)");
            var sec = lines.Skip(i + 1).TakeWhile(s => !s.StartsWith("# ---")).Where(s => s.Trim().Length > 0).ToList();
            return Ok($"{head}\nnever called: {sec.Count} (showing {Math.Min(sec.Count, last)}; from {Txt.Rel(path)})\n" + string.Join("\n", sec.Take(last)));
        }
        if (grep.Length > 0)
        {
            Regex rx;
            try { rx = new Regex(grep, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2)); } catch (Exception e) { return Fail("bad regex: " + e.Message); }
            lines = lines.Where(s => rx.IsMatch(s)).ToList();
        }
        lines = lines.Where(s => s.Length > 0).ToList();
        int total = lines.Count;
        var take = lines.Skip(Math.Max(0, total - last)).Select(s => Txt.Head(s, null, 260));
        return Ok($"{Txt.Rel(path)}: {total} line(s){(grep.Length > 0 ? " matching" : "")}, showing last {Math.Min(last, total)}\n" + Txt.Tail(string.Join("\n", take), Txt.Rel(path)));
    }
}
