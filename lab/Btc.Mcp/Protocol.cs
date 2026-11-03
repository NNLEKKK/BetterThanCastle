using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Btc.Mcp;

/// <summary>JSON-RPC 2.0 / MCP message handling. Handle() maps one input line to one output line (null for notifications).</summary>
public static class Server
{
    public const string Version = "0.1.0";
    static readonly string[] Supported = { "2025-06-18", "2025-03-26", "2024-11-05" };

    const string Instructions =
        "BTC project tools. Prefer these over raw shell: results are compact (<6k chars). " +
        "Long work runs as jobs: build/smoke/scenario_run/duel_par/lab return a job id when not finished in ~50 s; poll with job_wait {id} (long-poll <=50 s), job_tail, job_cancel; job_status lists recent jobs. " +
        "Flow: errors -> smoke -> deploy_status; game_status before any game run; kill_my_instances only kills our hidden test games (-screen-width 640). " +
        "Duel research: duel_par (parallel hidden games) -> duel_logs / duel_ab (Wilson CI verdict). Logs: logs {kind}. Never touch game DLLs or ref/.";

    public static async Task<string> Handle(string line)
    {
        JsonNode id = null; JsonObject req;
        try
        {
            var n = JsonNode.Parse(line);
            if (n is not JsonObject o) return Err(null, -32600, "Invalid Request");
            req = o;
        }
        catch (Exception e) { return Err(null, -32700, "Parse error: " + e.Message); }

        bool hasId = req.TryGetPropertyValue("id", out id) && id != null;
        string method = req["method"]?.GetValue<string>();
        if (method == null) return hasId ? Err(id, -32600, "missing method") : null;
        if (!hasId) return null;                     // notification: never answered
        id = id.DeepClone();
        try
        {
            var prm = req["params"] as JsonObject;
            switch (method)
            {
                case "initialize":
                    string cv = prm?["protocolVersion"]?.GetValue<string>();
                    string pv = cv != null && Array.IndexOf(Supported, cv) >= 0 ? cv : Supported[0];
                    return Ok(id, new JsonObject
                    {
                        ["protocolVersion"] = pv,
                        ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
                        ["serverInfo"] = new JsonObject { ["name"] = "btc", ["version"] = Version },
                        ["instructions"] = Instructions,
                    });
                case "ping": return Ok(id, new JsonObject());
                case "tools/list":
                    var arr = new JsonArray();
                    foreach (var t in Registry.All)
                        arr.Add(new JsonObject { ["name"] = t.Name, ["description"] = t.Description, ["inputSchema"] = JsonNode.Parse(t.Schema) });
                    return Ok(id, new JsonObject { ["tools"] = arr });
                case "tools/call":
                    string name = prm?["name"]?.GetValue<string>();
                    var tool = Registry.All.FirstOrDefault(t => t.Name == name);
                    if (tool == null) return Err(id, -32602, "Unknown tool: " + name);
                    ToolResult r;
                    try { r = await tool.Run(new Args(prm["arguments"] as JsonObject)); }
                    catch (Exception e) { r = new ToolResult("tool crashed: " + e.GetType().Name + ": " + e.Message, true); Env.Log(e.ToString()); }
                    return Ok(id, new JsonObject
                    {
                        ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = Txt.Head(r.Text ?? "", null, 12000) }),
                        ["isError"] = r.IsError,
                    });
                default:
                    return Err(id, -32601, "Method not found: " + method);
            }
        }
        catch (Exception e) { Env.Log(e.ToString()); return Err(id, -32603, e.Message); }
    }

    static string Ok(JsonNode id, JsonNode result) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result }.ToJsonString();

    static string Err(JsonNode id, int code, string msg) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = msg } }.ToJsonString();
}

/// <summary>Stdio transport: newline-delimited messages, stdout carries protocol only.</summary>
public static class Transport
{
    static readonly object Lock = new();
    static StreamWriter _out;

    public static async Task Serve()
    {
        _out = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = false, NewLine = "\n" };
        using var inp = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
        var pending = new List<Task>();
        string line;
        while ((line = await inp.ReadLineAsync()) != null)
        {
            if (line.Trim().Length == 0) continue;
            string l = line;
            var t = Task.Run(async () =>
            {
                string resp = await Server.Handle(l);
                if (resp != null) Write(resp);
            });
            lock (pending) { pending.RemoveAll(x => x.IsCompleted); pending.Add(t); }
        }
        Task[] rest; lock (pending) rest = pending.ToArray();
        try { await Task.WhenAll(rest).WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
    }

    static void Write(string json)
    {
        lock (Lock) { _out.Write(json); _out.Write('\n'); _out.Flush(); }
    }
}
