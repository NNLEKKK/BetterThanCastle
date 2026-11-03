using System.Text;
using static Btc.Mcp.Registry;

namespace Btc.Mcp;

/// <summary>job_status, job_wait, job_tail, job_cancel.</summary>
public static class JobTools
{
    public static void Register(List<Tool> l)
    {
        l.Add(T("job_status", "Without id: list recent jobs. With id: header plus last lines.", P(Str("id", "job id")), "", Status));
        l.Add(T("job_wait", "Long-poll a job up to 'seconds' (<=50). Returns the formatted result when finished, else RUNNING plus tail.",
            P(Str("id", "job id"), Int("seconds", "max 50")), "id", Wait));
        l.Add(T("job_tail", "Last N lines of a job's log (default 30).", P(Str("id", "job id"), Int("lines", "default 30")), "id", Tail));
        l.Add(T("job_cancel", "Kill a running job's process tree.", P(Str("id", "job id")), "id", Cancel));
    }

    static Task<ToolResult> Status(Args a)
    {
        string id = a.Str("id");
        if (id.Length == 0)
        {
            var sb = new StringBuilder();
            foreach (var j in Jobs.Recent(10)) sb.AppendLine(j.Header() + "  " + Txt.Head(j.Name, null, 60));
            return Ok(sb.Length == 0 ? "no jobs" : sb.ToString().TrimEnd());
        }
        var job = Jobs.Get(id);
        return job == null ? Fail("unknown job " + id) : Ok(job.Header() + "\ncmd: " + Txt.Head(job.Cmd, null, 300) + "\n--- tail\n" + Jobs.Tail(job, 10));
    }

    static async Task<ToolResult> Wait(Args a)
    {
        var job = Jobs.Get(a.Str("id"));
        if (job == null) return new ToolResult("unknown job " + a.Str("id"), true);
        return await Jobs.Finish(job, a.Int("seconds", 50));
    }

    static Task<ToolResult> Tail(Args a)
    {
        var job = Jobs.Get(a.Str("id"));
        return job == null ? Fail("unknown job") : Ok(job.Header() + "\n" + Jobs.Tail(job, a.Int("lines", 30)));
    }

    static Task<ToolResult> Cancel(Args a)
    {
        var job = Jobs.Get(a.Str("id"));
        if (job == null) return Fail("unknown job");
        return Ok(Jobs.Cancel(job) ? $"cancelled {job.Id}" : $"{job.Id} is not running ({job.State})");
    }
}
