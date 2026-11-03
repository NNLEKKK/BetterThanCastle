using System.Text.Json.Nodes;

namespace Btc.Mcp;

/// <summary>In-process JSON-RPC round trip through Server.Handle (same code path as stdio).</summary>
public static class SelfTest
{
    static int _fail;

    public static async Task<int> Run()
    {
        Console.WriteLine("btc-mcp selftest, repo " + Env.Root);
        var r = await Rpc(1, "initialize", """{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"t","version":"1"}}""");
        Check("initialize", r?["result"]?["protocolVersion"]?.ToString() == "2025-03-26" && r["result"]["serverInfo"]["name"].ToString() == "btc");
        string n = await Server.Handle("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        Check("initialized notification (no reply)", n == null);
        r = await Rpc(2, "ping", null);
        Check("ping", r?["result"] is JsonObject);
        r = await Rpc(3, "tools/list", null);
        var tools = r?["result"]?["tools"] as JsonArray;
        Check($"tools/list ({tools?.Count} tools)", tools != null && tools.Count == 21 && tools.All(t => t["inputSchema"]?["type"]?.ToString() == "object"));
        r = await Rpc(4, "nope/nope", null);
        Check("unknown method -> -32601", r?["error"]?["code"]?.GetValue<int>() == -32601);

        await Call("status", "{}", t => t.Contains("git log"));
        await Call("game_status", "{}", t => t.Length > 0);
        await Call("duel_logs", """{"dir":"par0","last":3}""", t => t.Contains("matches") || t.Contains("no duel"), allowError: true);
        await Call("duel_logs", """{"last":3}""", t => t.Contains("ALL:") || t.Contains("no duel"), allowError: true);
        await Call("duel_ab", """{"a":"par0","b":"kirusha"}""", t => t.Contains("verdict") || t.Contains("empty set"), allowError: true);
        await Call("deploy_status", "{}", t => t.Contains("verdict"));
        await Call("logs", """{"kind":"hits","last":5}""", t => t.Length > 0, allowError: true);
        await Call("ref_grep", """{"pattern":"class UNetManager\\b","max":3}""", t => t.Contains("hit"));
        await Call("job_status", "{}", t => t.Length > 0);
        await Call("lab_build", "{}", t => t.Contains("LAB BUILD OK") || t.StartsWith("RUNNING"));
        Console.WriteLine(_fail == 0 ? "SELFTEST PASS" : "SELFTEST FAIL (" + _fail + ")");
        return _fail == 0 ? 0 : 1;
    }

    static async Task<JsonObject> Rpc(int id, string method, string prm)
    {
        string line = $$"""{"jsonrpc":"2.0","id":{{id}},"method":"{{method}}"{{(prm != null ? ",\"params\":" + prm : "")}}}""";
        string resp = await Server.Handle(line);
        return resp == null ? null : JsonNode.Parse(resp).AsObject();
    }

    static async Task Call(string tool, string argsJson, Func<string, bool> ok, bool allowError = false)
    {
        var r = await Rpc(100, "tools/call", $$"""{"name":"{{tool}}","arguments":{{argsJson}}}""");
        string text = r?["result"]?["content"]?[0]?["text"]?.ToString() ?? "";
        bool isErr = r?["result"]?["isError"]?.GetValue<bool>() ?? true;
        bool pass = r?["result"] != null && (!isErr || allowError) && ok(text);
        Check($"tools/call {tool}", pass, text);
    }

    static void Check(string name, bool pass, string detail = null)
    {
        if (!pass) _fail++;
        Console.WriteLine((pass ? "PASS " : "FAIL ") + name + (detail != null ? "  | " + Txt.Head(detail.Replace("\n", " / "), null, 110) : ""));
    }
}
