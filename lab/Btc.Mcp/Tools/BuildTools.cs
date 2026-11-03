using System.Text.RegularExpressions;
using static Btc.Mcp.Registry;

namespace Btc.Mcp;

/// <summary>build, errors, smoke, deploy_status.</summary>
public static class BuildTools
{
    public static void Register(List<Tool> l)
    {
        l.Add(T("build", "Run scripts/build.ps1 (Debug deploys to game plugins dir; Release builds dist + verify-dist). Job if >50 s.",
            P(Str("config", "Debug (default) or Release")), "", Build));
        l.Add(T("errors", "Run scripts/errors.ps1 (Debug compile into obj/<tag>) and list compiler errors as 'CODE file:line msg'.",
            P(Str("tag", "parallel-build tag (own obj/bin)"), Str("filter", "regex on error path")), "", Errors));
        l.Add(T("smoke", "scripts/smoke.ps1: build + launch hidden game + digest, one-line verdict. Job (60-90 s), long-polls 50 s.",
            P(Int("seconds", "game run length, default 40")), "", Smoke));
        l.Add(T("deploy_status", "Compare mtimes/sizes of src build dll, deployed game plugin dll and dist dll vs newest src .cs; verdict OK/STALE.", "", "", DeployStatus));
    }

    static async Task<ToolResult> Build(Args a)
    {
        string cfg = a.Str("config", "Debug");
        bool rel = cfg.Equals("Release", StringComparison.OrdinalIgnoreCase);
        if (!rel)
        {
            var games = await GameTools.List();
            if (games.Count > 0)
                return new ToolResult("REFUSED: Castle Story is running (DLL locked): " + GameTools.Describe(games) + (games.Any(g => !g.Ours) ? "\nNot ours: do not kill." : "\nOurs: use kill_my_instances."), true);
        }
        return await Jobs.RunOrJob("build " + (rel ? "Release" : "Debug"), ProcSpec.Ps("build.ps1", ("Release", rel ? "" : null)), 50, 1200,
            (o, ex) => Txt.LastLines(o, 40), j => j.State != "done" || !Jobs.Output(j).Contains("OK ("));
    }

    static readonly Regex ErrRx = new(@"^(?<f>.*?)\((?<l>\d+)(?:,\d+)?\):\s*error\s+(?<c>\w+):\s*(?<m>.*?)(\s*\[[^\]]*\])?$", RegexOptions.Compiled);

    static async Task<ToolResult> Errors(Args a)
    {
        string tag = a.Str("tag"), filter = a.Str("filter");
        var spec = ProcSpec.Ps("errors.ps1", ("Tag", tag.Length > 0 ? tag : null), ("Filter", filter.Length > 0 ? filter : null));
        string file = Env.P("BTClogs", tag.Length > 0 ? $"build-errors-{tag}.txt" : "build-errors.txt");
        return await Jobs.RunOrJob("errors" + (tag.Length > 0 ? " " + tag : ""), spec, 50, 900, (o, ex) =>
        {
            if (!File.Exists(file)) return "no error file; script output:\n" + Txt.LastLines(o, 15);
            var rows = new List<string>();
            foreach (var line in File.ReadAllLines(file))
            {
                var m = ErrRx.Match(line.Trim());
                string msg = m.Success ? m.Groups["m"].Value : line.Trim();
                if (msg.Length > 170) msg = msg.Substring(0, 170);
                rows.Add(m.Success ? $"{m.Groups["c"].Value} {Path.GetFileName(m.Groups["f"].Value)}:{m.Groups["l"].Value} {msg}" : msg);
            }
            string head = $"errors={rows.Count} (file {Txt.Rel(file)})";
            if (rows.Count == 0 && ex != 0) return head + "\nscript exit " + ex + ":\n" + Txt.LastLines(o, 12);
            return head + (rows.Count > 0 ? "\n" + string.Join("\n", rows.Take(40)) + (rows.Count > 40 ? $"\n...+{rows.Count - 40} more" : "") : "");
        }, j => !Jobs.Output(j).Contains("unique errors: 0"));
    }

    static Task<ToolResult> Smoke(Args a)
    {
        var spec = ProcSpec.Ps("smoke.ps1", ("Seconds", a.Has("seconds") ? a.Int("seconds", 40).ToString() : null));
        return Jobs.RunOrJob("smoke", spec, 50, 900, (o, ex) => string.Join("\n", Txt.Lines(o).Where(s => s.Trim().Length > 0).Take(10)),
            j => !Jobs.Output(j).Contains("SMOKE OK"));
    }

    static Task<ToolResult> DeployStatus(Args a)
    {
        string dbg = Env.P("src", "BetterThanCastle", "bin", "Debug", "net35", "BetterThanCastle.dll");
        string dep = Path.Combine(Env.Game, "BepInEx", "plugins", "BetterThanCastle", "BetterThanCastle.dll");
        string dist = Env.P("dist", "BetterThanCastle.dll");
        DateTime newest = DateTime.MinValue; string newestF = "";
        string srcRoot = Env.P("src", "BetterThanCastle");
        foreach (var f in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains("\\obj\\") || f.Contains("\\bin\\")) continue;
            var t = File.GetLastWriteTime(f);
            if (t > newest) { newest = t; newestF = f; }
        }
        string Row(string label, string p) => File.Exists(p) ? $"{label,-9} {File.GetLastWriteTime(p):MM-dd HH:mm:ss} {new FileInfo(p).Length / 1024} KB" : $"{label,-9} MISSING";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(Row("src-dbg", dbg)); sb.AppendLine(Row("deployed", dep)); sb.AppendLine(Row("dist", dist));
        sb.AppendLine($"newest src: {newest:MM-dd HH:mm:ss} {Txt.Rel(newestF)}");
        var notes = new List<string>();
        if (!File.Exists(dbg) || File.GetLastWriteTime(dbg) < newest) notes.Add("debug build older than source (run errors/build)");
        if (!File.Exists(dep) || (File.Exists(dbg) && File.GetLastWriteTime(dep) < File.GetLastWriteTime(dbg))) notes.Add("game plugin dll older than debug build (run build, not errors)");
        if (File.Exists(dbg) && File.Exists(dep) && new FileInfo(dbg).Length != new FileInfo(dep).Length) notes.Add("deployed size differs from debug build");
        bool distStale = !File.Exists(dist) || File.GetLastWriteTime(dist) < newest;
        sb.Append(notes.Count == 0 ? "verdict: OK (debug deploy current)" : "verdict: STALE - " + string.Join("; ", notes));
        if (distStale) sb.Append("\nnote: dist dll older than source (Release build needed before release)");
        return Ok(sb.ToString());
    }
}
