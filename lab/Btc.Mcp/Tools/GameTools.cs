using System.Diagnostics;
using System.Text;
using static Btc.Mcp.Registry;

namespace Btc.Mcp;

public sealed record GameProc(int Pid, string Cmd, bool Ours);

/// <summary>game_status, kill_my_instances, game_cmd.</summary>
public static class GameTools
{
    public static void Register(List<Tool> l)
    {
        l.Add(T("game_status", "List Castle Story processes; ours (cmdline has -screen-width 640, hidden test games) vs the user's.", "", "", async a =>
        {
            var g = await List();
            return new ToolResult(g.Count == 0 ? "no Castle Story process running" : Describe(g));
        }));
        l.Add(T("kill_my_instances", "Kill ONLY our test game instances (cmdline -screen-width 640). Never touches the user's game.", "", "", Kill));
        l.Add(T("game_cmd", "Append DEV scenario commands to BTClogs/<dir>/cmd.txt (polled each second by the DEV build) and return new agent.log lines (waits <=5 s).",
            P(Str("dir", "BTClogs subdir e.g. par0; empty = BTClogs root"), "\"lines\":{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"description\":\"commands, one per entry\"}"), "lines", Cmd));
    }

    public static async Task<List<GameProc>> List()
    {
        var (_, o, _) = await Proc.Run(ProcSpec.Cmd(
            "Get-CimInstance Win32_Process -Filter \"Name like 'Castle Story%'\" | ForEach-Object { \"$($_.ProcessId)|$($_.CommandLine)\" }"), 30000);
        var r = new List<GameProc>();
        foreach (var line in Txt.Lines(o))
        {
            int i = line.IndexOf('|');
            if (i <= 0 || !int.TryParse(line.Substring(0, i), out int pid)) continue;
            string cmd = line.Substring(i + 1);
            r.Add(new GameProc(pid, cmd, cmd.Contains("-screen-width 640")));
        }
        return r;
    }

    public static string Describe(List<GameProc> g) =>
        string.Join("\n", g.Select(p => $"pid {p.Pid} {(p.Ours ? "OURS (test)" : "USER'S")} {Txt.Head(p.Cmd, null, 160)}"));

    static async Task<ToolResult> Kill(Args a)
    {
        var g = await List();
        var sb = new StringBuilder();
        foreach (var p in g)
        {
            if (!p.Ours) { sb.AppendLine($"pid {p.Pid}: skipped (user's game)"); continue; }
            try { Process.GetProcessById(p.Pid).Kill(true); sb.AppendLine($"pid {p.Pid}: killed"); }
            catch (Exception e) { sb.AppendLine($"pid {p.Pid}: {e.Message}"); }
        }
        return new ToolResult(sb.Length == 0 ? "nothing to kill" : sb.ToString().TrimEnd());
    }

    static async Task<ToolResult> Cmd(Args a)
    {
        string dir = a.Str("dir");
        if (dir.Contains("..") || dir.IndexOfAny(new[] { '/', '\\', ':' }) >= 0) return new ToolResult("bad dir", true);
        var lines = a.List("lines").Where(s => s.Trim().Length > 0).ToList();
        if (lines.Count == 0) return new ToolResult("no lines", true);
        string d = dir.Length > 0 ? Path.Combine(Env.Logs, dir) : Env.Logs;
        if (!Directory.Exists(d)) return new ToolResult($"BTClogs/{dir} does not exist (is a DEV game running with that BTC_LOGDIR?)", true);
        string cmdFile = Path.Combine(d, "cmd.txt"), log = Path.Combine(d, "agent.log");
        long before = File.Exists(log) ? new FileInfo(log).Length : 0;
        File.AppendAllText(cmdFile, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
        int expect = lines.Count(s => !s.TrimStart().StartsWith("#"));
        var t0 = DateTime.Now;
        string fresh = "";
        int seen = 0;
        while ((DateTime.Now - t0).TotalSeconds < 5)
        {
            await Task.Delay(300);
            if (!File.Exists(log)) continue;
            long len = new FileInfo(log).Length;
            long off = len < before ? 0 : before;
            if (len == off) continue;
            fresh = ReadFrom(log, off);
            seen = Txt.Lines(fresh).Count(s => s.Contains("|Cmd|"));
            if (seen >= expect) break;
        }
        bool consumed = !File.Exists(cmdFile);
        var outLines = Txt.Lines(fresh.TrimEnd()).Where(s => s.Length > 0).Select(s => Txt.Head(s, null, 220)).ToList();
        string tail = string.Join("\n", outLines.Skip(Math.Max(0, outLines.Count - 30)));
        return new ToolResult($"queued {lines.Count} line(s); cmd.txt {(consumed ? "consumed" : "still pending (game idle/not polling?)")}; Cmd lines seen {seen}/{expect}\n" + (tail.Length > 0 ? tail : "(no new agent.log lines)"));
    }

    static string ReadFrom(string path, long off)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        fs.Seek(off, SeekOrigin.Begin);
        return new StreamReader(fs, new UTF8Encoding(false)).ReadToEnd();
    }
}
