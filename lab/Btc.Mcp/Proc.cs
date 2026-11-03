using System.Diagnostics;
using System.Text;

namespace Btc.Mcp;

public sealed class ProcSpec
{
    public string File;
    public List<string> Args = new();
    public string RawArgs;          // used instead of Args when set
    public string Cwd = Env.Root;
    public bool Oem = true;         // decode output as the OEM console code page (PowerShell 5.1, dotnet) instead of UTF-8
    public bool Low;                // BelowNormal priority

    /// <summary>Run scripts/&lt;script&gt; with named params; value null = omitted, "" = switch.</summary>
    public static ProcSpec Ps(string script, params (string name, string val)[] ps)
    {
        var sb = new StringBuilder("& '" + Env.P("scripts", script).Replace("'", "''") + "'");
        foreach (var (n, v) in ps)
        {
            if (v == null) continue;
            sb.Append(" -").Append(n);
            if (v.Length > 0) sb.Append(" '").Append(v.Replace("'", "''")).Append('\'');
        }
        sb.Append("; exit $LASTEXITCODE");
        return Cmd(sb.ToString());
    }
    public static ProcSpec Cmd(string psCommand)
    {
        var s = new ProcSpec { File = "powershell" };
        s.Args.AddRange(new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", psCommand });
        return s;
    }
    public static ProcSpec Dotnet(string raw) => new() { File = "dotnet", RawArgs = raw };
    public static ProcSpec Git(params string[] a) { var s = new ProcSpec { File = "git", Oem = false }; s.Args.AddRange(a); return s; }

    public Process Create()
    {
        var psi = new ProcessStartInfo(File)
        {
            WorkingDirectory = Cwd, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
        };
        var enc = Oem ? Env.Oem : new UTF8Encoding(false);
        psi.StandardOutputEncoding = enc; psi.StandardErrorEncoding = enc;
        if (RawArgs != null) psi.Arguments = RawArgs; else foreach (var a in Args) psi.ArgumentList.Add(a);
        return new Process { StartInfo = psi, EnableRaisingEvents = true };
    }
}

public static class Proc
{
    /// <summary>Run to completion, merged stdout+stderr. Kills the tree on timeout.</summary>
    public static async Task<(int exit, string output, bool timedOut)> Run(ProcSpec spec, int timeoutMs = 60000)
    {
        var sb = new StringBuilder();
        using var p = spec.Create();
        p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
        try { p.Start(); } catch (Exception ex) { return (-1, "cannot start " + spec.File + ": " + ex.Message, false); }
        try { p.StandardInput.Close(); } catch { }
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        var done = Task.Run(() => p.WaitForExit());
        if (await Task.WhenAny(done, Task.Delay(timeoutMs)) != done)
        {
            try { p.Kill(true); } catch { }
            await Task.Delay(200);
            lock (sb) return (-1, sb.ToString(), true);
        }
        lock (sb) return (p.ExitCode, sb.ToString(), false);
    }
}
