using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

namespace Btc.Mcp;

public sealed class Job
{
    public string Id, Name, Cmd, State = "running", LogPath;
    public DateTime Started = DateTime.Now, Ended;
    public int Exit = -999, Pid;
    public Process P;
    public Func<string, int, string> Fmt;     // formats the final output (in memory only)
    public TaskCompletionSource<bool> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string JsonPath => Path.ChangeExtension(LogPath, ".json");

    public void Save()
    {
        try
        {
            var o = new JsonObject { ["id"] = Id, ["name"] = Name, ["cmd"] = Cmd, ["state"] = State, ["exit"] = Exit, ["pid"] = Pid,
                ["started"] = Started.ToString("o"), ["ended"] = Ended == default ? null : Ended.ToString("o") };
            File.WriteAllText(JsonPath, o.ToJsonString());
        }
        catch (Exception e) { Env.Log("job save: " + e.Message); }
    }

    public static Job Load(string jsonPath)
    {
        try
        {
            var o = JsonNode.Parse(File.ReadAllText(jsonPath)).AsObject();
            var j = new Job
            {
                Id = (string)o["id"], Name = (string)o["name"], Cmd = (string)o["cmd"], State = (string)o["state"],
                Exit = (int)o["exit"], Pid = (int)o["pid"], Started = DateTime.Parse((string)o["started"]),
                LogPath = Path.ChangeExtension(jsonPath, ".log"),
            };
            if (o["ended"] != null) j.Ended = DateTime.Parse((string)o["ended"]);
            if (j.State == "running") j.State = "lost";   // server restarted while it ran
            j.Done.TrySetResult(true);
            return j;
        }
        catch { return null; }
    }

    public string Header()
    {
        var end = State == "running" ? DateTime.Now : (Ended == default ? Started : Ended);
        return $"job {Id} [{Name}] {State}" + (State == "running" ? "" : $" exit={Exit}") + $" {(end - Started).TotalSeconds:F0}s pid={Pid}";
    }
}

public static class Jobs
{
    static readonly ConcurrentDictionary<string, Job> All = new();
    static int _n;
    static string Dir { get { var d = Path.Combine(Env.Logs, "jobs"); Directory.CreateDirectory(d); return d; } }

    public static Job Start(string name, ProcSpec spec, int maxSeconds = 3600, Func<string, int, string> fmt = null)
    {
        string id = "j" + DateTime.Now.ToString("MMdd-HHmmss") + "-" + Interlocked.Increment(ref _n);
        var job = new Job { Id = id, Name = name, Fmt = fmt, LogPath = Path.Combine(Dir, id + ".log"),
            Cmd = spec.File + " " + (spec.RawArgs ?? string.Join(" ", spec.Args)) };
        var log = new StreamWriter(new FileStream(job.LogPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete), new UTF8Encoding(false)) { AutoFlush = true };
        var p = spec.Create();
        p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (log) { try { log.WriteLine(e.Data); } catch { } } };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (log) { try { log.WriteLine(e.Data); } catch { } } };
        try { p.Start(); }
        catch (Exception ex)
        {
            job.State = "failed"; job.Ended = DateTime.Now; lock (log) log.WriteLine("cannot start: " + ex.Message); log.Dispose();
            job.Save(); All[id] = job; job.Done.TrySetResult(true); return job;
        }
        job.P = p; job.Pid = p.Id;
        try { p.StandardInput.Close(); } catch { }
        if (spec.Low) { try { p.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { } }
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        All[id] = job; job.Save();
        _ = Task.Run(async () =>
        {
            var w = Task.Run(() => p.WaitForExit());
            bool timeout = false;
            if (await Task.WhenAny(w, Task.Delay(maxSeconds * 1000)) != w) { timeout = true; try { p.Kill(true); } catch { } await w; }
            job.Exit = p.ExitCode; job.Ended = DateTime.Now;
            if (job.State == "running") job.State = timeout ? "timeout" : (job.Exit == 0 ? "done" : "failed");
            lock (log) log.Dispose();
            job.Save(); job.Done.TrySetResult(true);
        });
        return job;
    }

    public static Job Get(string id)
    {
        if (All.TryGetValue(id, out var j)) return j;
        var f = Path.Combine(Dir, id + ".json");
        return File.Exists(f) ? Job.Load(f) : null;
    }

    public static IEnumerable<Job> Recent(int n)
    {
        var list = new Dictionary<string, Job>();
        foreach (var f in Directory.GetFiles(Dir, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(n * 2))
        {
            var j = Get(Path.GetFileNameWithoutExtension(f)); if (j != null) list[j.Id] = j;
        }
        foreach (var j in All.Values) list[j.Id] = j;
        return list.Values.OrderByDescending(j => j.Started).Take(n);
    }

    public static string Output(Job j) => File.Exists(j.LogPath) ? Txt.ReadShared(j.LogPath) : "";

    public static string Tail(Job j, int lines) => Txt.Tail(Txt.LastLines(Output(j), lines), Txt.Rel(j.LogPath));

    public static string Final(Job j)
    {
        string o = Output(j);
        string body;
        try { body = j.Fmt != null && j.State != "lost" ? j.Fmt(o, j.Exit) : Txt.LastLines(o, 30); }
        catch (Exception e) { body = "formatter failed: " + e.Message + "\n" + Txt.LastLines(o, 30); }
        return Txt.Tail(body.TrimEnd(), Txt.Rel(j.LogPath));
    }

    /// <summary>Wait up to sec seconds (max 50); true when finished.</summary>
    public static async Task<bool> Wait(Job j, int sec)
    {
        if (j.State != "running") return true;
        return await Task.WhenAny(j.Done.Task, Task.Delay(Math.Clamp(sec, 0, 50) * 1000)) == j.Done.Task;
    }

    public static async Task<ToolResult> RunOrJob(string name, ProcSpec spec, int waitSec = 50, int maxSeconds = 3600, Func<string, int, string> fmt = null, Func<Job, bool> isErr = null)
        => await Finish(Start(name, spec, maxSeconds, fmt), waitSec, isErr);

    public static async Task<ToolResult> Finish(Job j, int waitSec, Func<Job, bool> isErr = null)
    {
        if (await Wait(j, waitSec))
            return new ToolResult(j.Header() + "\n" + Final(j), isErr != null ? isErr(j) : j.State != "done");
        return new ToolResult($"RUNNING {j.Header()}\nPoll: job_wait {{id:\"{j.Id}\"}} (<=50 s per call), job_tail, job_cancel. Log: {Txt.Rel(j.LogPath)}\n--- tail\n{Tail(j, 5)}");
    }

    public static bool Cancel(Job j)
    {
        if (j.P == null || j.State != "running") return false;
        j.State = "cancelled";
        try { j.P.Kill(true); } catch { }
        return true;
    }
}
