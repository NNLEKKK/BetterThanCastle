using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Brix.Engine;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Everything the in-game bug report attaches. Each section is built on demand (preview / send), can be switched off by the player, and has the
    /// Windows user name stripped. The text is meant to be read by a person or an agent without asking the reporter anything.
    /// </summary>
    internal static class BtcReportData
    {
        internal sealed class Section
        {
            internal string Key, En, Ru;
            internal bool On = true;
            internal Func<string> Build;
            internal string Cache;
            internal float CacheAt = -100f;
        }

        internal static readonly List<Section> Sections = new List<Section>
        {
            new Section { Key = "system", En = "System", Ru = "Система", Build = BuildSystem },
            new Section { Key = "state", En = "Game state", Ru = "Состояние игры", Build = BuildState },
            new Section { Key = "mods", En = "Installed mods", Ru = "Установленные моды", Build = BuildMods },
            new Section { Key = "settings", En = "Mod settings (changed ones)", Ru = "Настройки мода (изменённые)", Build = BuildSettings },
            new Section { Key = "errors", En = "Errors and warnings from the log", Ru = "Ошибки и предупреждения из лога", Build = BuildErrors },
            new Section { Key = "log", En = "Log tail (last 120 lines)", Ru = "Хвост лога (последние 120 строк)", On = true, Build = BuildLogTail },
            new Section { Key = "replays", En = "Latest replays (names only)", Ru = "Последние реплеи (только имена)", On = false, Build = BuildReplays }
        };

        internal static string Text(Section s)
        {
            if (Time.realtimeSinceStartup - s.CacheAt > 3f || s.Cache == null)
            {
                string t;
                try { t = Redact(s.Build()); }
                catch (Exception e) { t = "(failed to collect: " + e.Message + ")"; }
                s.Cache = t; s.CacheAt = Time.realtimeSinceStartup;
            }
            return s.Cache;
        }

        internal static int Lines(string t)
        {
            if (string.IsNullOrEmpty(t)) return 0;
            int n = 1;
            for (int i = 0; i < t.Length; i++) if (t[i] == '\n') n++;
            return n;
        }

        // ---- redaction: the Windows user name is part of most paths
        private static string Redact(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            try
            {
                string u = Environment.UserName;
                if (!string.IsNullOrEmpty(u) && u.Length >= 2)
                {
                    int i = 0;
                    while ((i = s.IndexOf(u, i, StringComparison.OrdinalIgnoreCase)) >= 0) { s = s.Substring(0, i) + "<user>" + s.Substring(i + u.Length); i += 6; }
                }
            }
            catch (Exception) { }
            return s;
        }

        // ---- builders
        private static string BuildSystem()
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine("mod: Better Than Castle v" + BtcPlugin.Version
#if DEV
                + " (DEV build)"
#endif
                );
            b.AppendLine("BetterConnection: " + (BtcNetworkGate.Enabled ? "ON" : "OFF"));
            b.AppendLine("unity: " + Application.unityVersion + ", platform " + Application.platform + ", language " + Application.systemLanguage);
            b.AppendLine("os: " + SystemInfo.operatingSystem);
            b.AppendLine("cpu: " + SystemInfo.processorType + " x" + SystemInfo.processorCount);
            b.AppendLine("ram: " + SystemInfo.systemMemorySize + " MB");
            b.AppendLine("gpu: " + SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceVersion + "), vram " + SystemInfo.graphicsMemorySize + " MB");
            b.AppendLine("screen: " + Screen.width + "x" + Screen.height + (Screen.fullScreen ? " fullscreen" : " windowed") + ", quality " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
            try { b.AppendLine("bepinex: " + typeof(BepInEx.Bootstrap.Chainloader).Assembly.GetName().Version); } catch (Exception) { }
            return b.ToString().TrimEnd();
        }

        private static string BuildState()
        {
            StringBuilder b = new StringBuilder();
            try { b.AppendLine("scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name); } catch (Exception) { }
            b.AppendLine("multiplayer: " + Neo.multiplayer);
            b.AppendLine("replay: " + BtcReplay.Mode + (BtcReplay.Active ? " (active)" : ""));
            b.AppendLine("session time: " + (Time.realtimeSinceStartup / 60f).ToString("0.0") + " min");
            float dt = Time.smoothDeltaTime;
            b.AppendLine("fps: " + (dt > 0.0001f ? (1f / dt).ToString("0") : "?") + " (frame " + (dt * 1000f).ToString("0.0") + " ms)");
            b.AppendLine("managed heap: " + (GC.GetTotalMemory(false) / (1024 * 1024)) + " MB");
            return b.ToString().TrimEnd();
        }

        private static string BuildMods()
        {
            StringBuilder b = new StringBuilder();
            foreach (KeyValuePair<string, BepInEx.PluginInfo> kv in BepInEx.Bootstrap.Chainloader.PluginInfos)
                b.AppendLine(kv.Value.Metadata.Name + " " + kv.Value.Metadata.Version + "  [" + kv.Key + "]");
            return b.ToString().TrimEnd();
        }

        private static string BuildSettings()
        {
            StringBuilder b = new StringBuilder();
            BepInEx.PluginInfo pi;
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(BtcPlugin.Guid, out pi) || pi.Instance == null) return "(config unavailable)";
            BepInEx.Configuration.ConfigFile cfg = pi.Instance.Config;
            int n = 0;
            foreach (BepInEx.Configuration.ConfigDefinition d in cfg.Keys)
            {
                BepInEx.Configuration.ConfigEntryBase e = cfg[d];
                object v = e.BoxedValue, def = e.DefaultValue;
                if (v != null && v.Equals(def)) continue;
                if (n++ >= 150) { b.AppendLine("..."); break; }
                b.AppendLine(d.Section + "/" + d.Key + " = " + v + "   (default " + def + ")");
            }
            if (n == 0) b.AppendLine("(all defaults)");
            return b.ToString().TrimEnd();
        }

        private static string[] ReadTail(int maxBytes)
        {
            string path = Path.Combine(BepInEx.Paths.BepInExRootPath, "LogOutput.log");
            if (!File.Exists(path)) return new string[0];
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long start = Math.Max(0, fs.Length - maxBytes);
                fs.Seek(start, SeekOrigin.Begin);
                byte[] buf = new byte[fs.Length - start];
                int got = 0;
                while (got < buf.Length) { int r = fs.Read(buf, got, buf.Length - got); if (r <= 0) break; got += r; }
                string t = Encoding.UTF8.GetString(buf, 0, got);
                string[] l = t.Replace("\r", "").Split('\n');
                return l;
            }
        }

        private static string BuildErrors()
        {
            string[] l = ReadTail(600 * 1024);
            List<string> hits = new List<string>();
            for (int i = 0; i < l.Length; i++)
            {
                string s = l[i];
                if (s.Length == 0) continue;
                bool bad = s.IndexOf("[Error", StringComparison.Ordinal) >= 0 || s.IndexOf("[Warning", StringComparison.Ordinal) >= 0
                    || s.IndexOf("Exception", StringComparison.Ordinal) >= 0 || s.IndexOf("[Patch] FAIL", StringComparison.Ordinal) >= 0
                    || s.StartsWith("  at ", StringComparison.Ordinal);
                if (bad) hits.Add(s.Length > 400 ? s.Substring(0, 400) + "..." : s);
            }
            if (hits.Count == 0) return "(none in the log)";
            int from = Math.Max(0, hits.Count - 80);
            StringBuilder b = new StringBuilder();
            if (from > 0) b.AppendLine("(" + from + " older lines skipped)");
            for (int i = from; i < hits.Count; i++) b.AppendLine(hits[i]);
            return b.ToString().TrimEnd();
        }

        private static string BuildLogTail()
        {
            string[] l = ReadTail(96 * 1024);
            int from = Math.Max(0, l.Length - 121);
            StringBuilder b = new StringBuilder();
            for (int i = from; i < l.Length; i++)
            {
                if (l[i].Length == 0) continue;
                b.AppendLine(l[i].Length > 300 ? l[i].Substring(0, 300) + "..." : l[i]);
            }
            return b.ToString().TrimEnd();
        }

        private static string BuildReplays()
        {
            string dir = Path.Combine(BtcPaths.Root, "Replays");
            if (!Directory.Exists(dir)) return "(no replays folder)";
            FileInfo[] f = new DirectoryInfo(dir).GetFiles("*.*");
            Array.Sort(f, delegate (FileInfo a, FileInfo c) { return c.LastWriteTime.CompareTo(a.LastWriteTime); });
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < f.Length && i < 5; i++)
                b.AppendLine(f[i].Name + "  " + (f[i].Length / 1024) + " KB  " + f[i].LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
            return b.Length == 0 ? "(none)" : b.ToString().TrimEnd();
        }

        // ---- report text
        internal sealed class Form
        {
            internal string Title = "", What = "", Steps = "", Expected = "";
            internal int Area, Severity;
        }

        internal static readonly string[] AreaKeys = { "replay", "hotkeys", "visuals", "gameplay", "multiplayer", "duel", "packs", "menu", "other" };
        internal static readonly string[] AreaEn = { "Replay", "Hotkeys", "Visuals", "Gameplay", "Multiplayer", "Duel", "Packs", "Menu / UI", "Other" };
        internal static readonly string[] AreaRu = { "Реплеи", "Хоткеи", "Визуал", "Геймплей", "Мультиплеер", "Дуэль", "Паки", "Меню / UI", "Другое" };
        internal static readonly string[] SevKeys = { "crash", "broken", "minor", "cosmetic" };
        internal static readonly string[] SevEn = { "Crash / freeze", "Broken feature", "Minor bug", "Cosmetic" };
        internal static readonly string[] SevRu = { "Вылет / зависание", "Не работает", "Мелкий баг", "Косметика" };

        private static void Head(StringBuilder b, Form f)
        {
            b.AppendLine("area: " + AreaKeys[f.Area]);
            b.AppendLine("severity: " + SevKeys[f.Severity]);
            b.AppendLine("version: " + BtcPlugin.Version + ", BetterConnection " + (BtcNetworkGate.Enabled ? "ON" : "OFF"));
            b.AppendLine();
            b.AppendLine("## What happened"); b.AppendLine(Clean(f.What)); b.AppendLine();
            if (f.Steps.Trim().Length > 0) { b.AppendLine("## Steps to reproduce"); b.AppendLine(Clean(f.Steps)); b.AppendLine(); }
            if (f.Expected.Trim().Length > 0) { b.AppendLine("## Expected"); b.AppendLine(Clean(f.Expected)); b.AppendLine(); }
        }

        private static string Clean(string s) { return Redact((s ?? "").Replace("\r", "").Trim()); }

        /// <summary>The whole report (file + clipboard).</summary>
        internal static string Full(Form f)
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine("# " + f.Title.Trim());
            Head(b, f);
            b.AppendLine("created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " (local time)");
            b.AppendLine();
            for (int i = 0; i < Sections.Count; i++)
            {
                if (!Sections[i].On) continue;
                b.AppendLine("## [" + Sections[i].Key + "]");
                b.AppendLine("```");
                b.AppendLine(Text(Sections[i]));
                b.AppendLine("```");
                b.AppendLine();
            }
            return b.ToString();
        }

        /// <summary>The short body for a prefilled GitHub issue (URL length limit): description + system + state + the last errors. The full text goes to the clipboard.</summary>
        internal static string Short(Form f, int budget)
        {
            StringBuilder b = new StringBuilder();
            Head(b, f);
            for (int i = 0; i < Sections.Count; i++)
            {
                Section s = Sections[i];
                if (!s.On || (s.Key != "system" && s.Key != "state" && s.Key != "errors")) continue;
                string t = Text(s);
                if (s.Key == "errors")
                {
                    string[] l = t.Split('\n');
                    int from = Math.Max(0, l.Length - 8);
                    t = string.Join("\n", l, from, l.Length - from);
                }
                b.AppendLine("<details><summary>" + s.En + "</summary>"); b.AppendLine(); b.AppendLine("```"); b.AppendLine(t); b.AppendLine("```"); b.AppendLine("</details>"); b.AppendLine();
            }
            b.AppendLine("<!-- Full diagnostics are in your clipboard: press Ctrl+V here (or attach the saved report file). -->");
            string r = b.ToString();
            return r.Length > budget ? r.Substring(0, budget) : r;
        }

        internal static string ReportDir { get { return Path.Combine(BtcPaths.Root, "reports"); } }

        internal static string Save(Form f)
        {
            Directory.CreateDirectory(ReportDir);
            string p = Path.Combine(ReportDir, "bug-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(p, Full(f), new UTF8Encoding(false));
            return p;
        }

        internal static string IssueUrl(Form f)
        {
            string title = Redact(f.Title.Trim());
            string baseUrl = "https://github.com/" + BtcUpdater.Repo + "/issues/new?labels=bug&title=" + WWW.EscapeURL("[" + AreaEn[f.Area] + "] " + title) + "&body=";
            int budget = 4200;
            string url = baseUrl + WWW.EscapeURL(Short(f, budget));
            while (url.Length > 7000 && budget > 600)
            {
                budget = (int)(budget * 0.8f);
                url = baseUrl + WWW.EscapeURL(Short(f, budget));
            }
            return url;
        }
    }
}
