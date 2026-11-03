using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Btc.Mcp;

public sealed record ToolResult(string Text, bool IsError = false);

public sealed class Tool
{
    public string Name, Description, Schema;
    public Func<Args, Task<ToolResult>> Run;
}

/// <summary>Typed accessors over the tools/call arguments object.</summary>
public sealed class Args
{
    readonly JsonObject _o;
    public Args(JsonObject o) { _o = o ?? new JsonObject(); }
    public bool Has(string k) => _o.TryGetPropertyValue(k, out var v) && v != null;
    public string Str(string k, string def = "")
    {
        if (!_o.TryGetPropertyValue(k, out var v) || v == null) return def;
        try { return v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : v.ToJsonString(); } catch { return def; }
    }
    public int Int(string k, int def)
    {
        if (!_o.TryGetPropertyValue(k, out var v) || v == null) return def;
        try { return v.GetValueKind() == JsonValueKind.String ? int.Parse(v.GetValue<string>()) : (int)v.GetValue<double>(); } catch { return def; }
    }
    public bool Bool(string k, bool def = false)
    {
        if (!_o.TryGetPropertyValue(k, out var v) || v == null) return def;
        try { return v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() == "true" : v.GetValue<bool>(); } catch { return def; }
    }
    public List<string> List(string k)
    {
        var r = new List<string>();
        if (_o.TryGetPropertyValue(k, out var v) && v is JsonArray a) foreach (var e in a) r.Add(e?.ToString() ?? "");
        else if (v != null) r.Add(v.ToString());
        return r;
    }
}

public static class Env
{
    static string _root;
    public static string Root
    {
        get
        {
            if (_root != null) return _root;
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "CLAUDE.md"))) d = d.Parent;
            _root = d?.FullName ?? throw new InvalidOperationException("repo root (CLAUDE.md) not found above " + AppContext.BaseDirectory);
            return _root;
        }
    }
    public static string P(params string[] parts) => Path.Combine(new[] { Root }.Concat(parts).ToArray());
    public static string Logs => P("BTClogs");
    public static string Game => P("game", "csmod");

    [DllImport("kernel32")] static extern uint GetOEMCP();
    static Encoding _oem;
    public static Encoding Oem
    {
        get
        {
            if (_oem != null) return _oem;
            try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); _oem = Encoding.GetEncoding((int)GetOEMCP()); }
            catch { _oem = new UTF8Encoding(false); }
            return _oem;
        }
    }
    public static void Log(string s) { try { Console.Error.WriteLine("[btc-mcp] " + s); } catch { } }
}

public static class Txt
{
    public const int Max = 6000;
    /// <summary>Keep the head; append a note when cut.</summary>
    public static string Head(string s, string hint = null, int max = Max)
    {
        if (s.Length <= max) return s;
        return s.Substring(0, max) + "\n...[truncated, " + (s.Length - max) + " chars more" + (hint != null ? "; full: " + hint : "") + "]";
    }
    /// <summary>Keep the tail; prefix a note when cut.</summary>
    public static string Tail(string s, string hint = null, int max = Max)
    {
        if (s.Length <= max) return s;
        return "[truncated, " + (s.Length - max) + " chars before" + (hint != null ? "; full: " + hint : "") + "]\n..." + s.Substring(s.Length - max);
    }
    public static string[] Lines(string s) => s.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
    public static string LastLines(string s, int n)
    {
        var l = Lines(s.TrimEnd());
        return string.Join("\n", l.Skip(Math.Max(0, l.Length - n)));
    }
    public static string ReadShared(string path, int maxBytes = 4_000_000)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long len = fs.Length;
        if (len > maxBytes) fs.Seek(len - maxBytes, SeekOrigin.Begin);
        using var sr = new StreamReader(fs, new UTF8Encoding(false), true);
        return sr.ReadToEnd();
    }
    public static string Rel(string p) => p.StartsWith(Env.Root, StringComparison.OrdinalIgnoreCase) ? p.Substring(Env.Root.Length).TrimStart('\\', '/') : p;
}
