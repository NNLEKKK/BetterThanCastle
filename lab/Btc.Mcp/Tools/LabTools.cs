using System.Text.RegularExpressions;
using static Btc.Mcp.Registry;

namespace Btc.Mcp;

/// <summary>lab_build and the btclab passthrough.</summary>
public static class LabTools
{
    public static void Register(List<Tool> l)
    {
        l.Add(T("lab_build", "dotnet build of the lab (Release), errors parsed. project: cli (default, Btc.Lab.Cli + deps) | tests | sln (note: sln includes Btc.Mcp, whose dll is locked while MCP is connected).",
            P(Str("project", "cli|tests|sln")), "", LabBuild));
        l.Add(T("lab", "Run btclab (lab/Btc.Lab.Cli) with an argument string, e.g. 'bench ...'. Foreground waits 50 s then continues as a job; background returns the job id at once.",
            P(Str("args", "raw command line for btclab"), Bool("background", "start as job and return immediately"), Bool("lowPriority", "BelowNormal priority")), "args", Lab));
    }

    static readonly Regex ErrRx = new(@"error\s+\w+\d+", RegexOptions.Compiled);

    static Task<ToolResult> LabBuild(Args a)
    {
        string proj = a.Str("project", "cli");
        string target = proj switch
        {
            "sln" => "lab/Btc.Lab.sln",
            "tests" => "lab/Btc.Lab.Tests/Btc.Lab.Tests.csproj",
            _ => "lab/Btc.Lab.Cli/Btc.Lab.Cli.csproj",
        };
        var spec = ProcSpec.Dotnet($"build \"{Env.P(target.Split('/'))}\" -c Release -v q -nologo");
        return Jobs.RunOrJob("lab_build " + proj, spec, 50, 900, (o, ex) =>
        {
            var errs = Txt.Lines(o).Where(s => ErrRx.IsMatch(s)).Select(s => s.Replace(Env.Root + "\\", "").Trim()).Select(s => Regex.Replace(s, @"\s*\[[^\]]*\]$", "")).Distinct().ToList();
            if (errs.Count == 0 && ex == 0) return "LAB BUILD OK (" + proj + ")";
            return $"LAB BUILD FAIL exit={ex} errors={errs.Count}\n" + string.Join("\n", errs.Take(25).Select(s => Txt.Head(s, null, 230)))
                + (errs.Count == 0 ? "\n" + Txt.LastLines(o, 12) : "");
        }, j => j.Exit != 0);
    }

    static async Task<ToolResult> Lab(Args a)
    {
        string dll = Env.P("lab", "Btc.Lab.Cli", "bin", "Release", "net10.0", "btclab.dll");
        if (!File.Exists(dll)) return new ToolResult("btclab.dll not built: run lab_build first", true);
        var spec = ProcSpec.Dotnet($"\"{dll}\" {a.Str("args")}");
        spec.Low = a.Bool("lowPriority");
        var j = Jobs.Start("lab " + Txt.Head(a.Str("args"), null, 40), spec, 6 * 3600, (o, ex) => Txt.LastLines(o, 60));
        return await Jobs.Finish(j, a.Bool("background") ? 2 : 50, x => x.State != "done");
    }
}
