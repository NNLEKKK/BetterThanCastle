namespace Btc.Mcp;

/// <summary>Tool registry. To add a tool: write a static method in Tools/*.cs and add one T(...) line to that file's Register().</summary>
public static class Registry
{
    public static readonly List<Tool> All = Build();

    static List<Tool> Build()
    {
        var l = new List<Tool>();
        BuildTools.Register(l);
        GameTools.Register(l);
        DuelTools.Register(l);
        LogTools.Register(l);
        LabTools.Register(l);
        JobTools.Register(l);
        InfoTools.Register(l);
        return l;
    }

    /// <summary>props = JSON object body of "properties" (without outer braces), required = comma list.</summary>
    public static Tool T(string name, string desc, string props, string required, Func<Args, Task<ToolResult>> run)
    {
        string req = string.IsNullOrEmpty(required) ? "" : ",\"required\":[" + string.Join(",", required.Split(',').Select(s => "\"" + s.Trim() + "\"")) + "]";
        return new Tool { Name = name, Description = desc, Run = run, Schema = "{\"type\":\"object\",\"properties\":{" + props + "}" + req + "}" };
    }

    public static Task<ToolResult> Ok(string s) => Task.FromResult(new ToolResult(s));
    public static Task<ToolResult> Fail(string s) => Task.FromResult(new ToolResult(s, true));

    // property helpers
    public static string Str(string n, string d) => $"\"{n}\":{{\"type\":\"string\",\"description\":\"{d}\"}}";
    public static string Int(string n, string d) => $"\"{n}\":{{\"type\":\"integer\",\"description\":\"{d}\"}}";
    public static string Bool(string n, string d) => $"\"{n}\":{{\"type\":\"boolean\",\"description\":\"{d}\"}}";
    public static string P(params string[] props) => string.Join(",", props);
}
