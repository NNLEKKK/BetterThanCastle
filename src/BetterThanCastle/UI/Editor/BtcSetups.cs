using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Global setups: the whole look of the mod (UI editor, replay interface, visuals, hotkeys, profile, optionally gameplay rules) in ONE text.
    /// Parts can be switched on and off when copying or applying. A setup is a named file (configs/setups/NAME.txt) or a one-line code
    /// ("BTCALL1:" + deflate + base64) to paste to a friend. Every part reuses the capture / apply of its own tab, so each part keeps its own rules.
    /// </summary>
    internal static class BtcSetups
    {
        internal sealed class Part
        {
            public string Key, En, Ru, HintEn, HintRu;
            public int Icon;
            public bool DefaultOn = true;
            public Func<string> Capture;
            public Func<string, int> Apply;      // returns the number of applied items (0 = nothing / failed)
        }

        internal static readonly List<Part> Parts = new List<Part>();
        private const string Magic = "BTCALL1";

        private static void Init()
        {
            if (Parts.Count > 0) return;
            Parts.Add(new Part
            {
                Key = "ui", En = "UI editor: screen layout, sizes, menu look", Ru = "UI редактор: расположение, размеры, вид меню", Icon = BtcReplayIcons.Layout,
                Capture = BtcUiEdit.Serialize,
                Apply = delegate (string t) { BtcUiEdit.ApplyPreset(t); return 1; }
            });
            Parts.Add(new Part
            {
                Key = "replay", En = "Replay interface", Ru = "Интерфейс реплея", Icon = BtcReplayIcons.Play,
                Capture = delegate { return "keys=" + BtcConfig.ReplayKeysMode.Value + "\napm=" + BtcConfig.ReplayApmMode.Value + "\nlayout=" + BtcReplayLayout.Serialize() + "\n"; },
                Apply = delegate (string t)
                {
                    int n = 0;
                    foreach (string l in t.Replace("\r", "").Split('\n'))
                    {
                        int eq = l.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = l.Substring(0, eq), v = l.Substring(eq + 1);
                        int iv;
                        if (k == "keys" && int.TryParse(v, out iv)) { BtcConfig.ReplayKeysMode.Value = Mathf.Clamp(iv, 0, 2); n++; }
                        else if (k == "apm" && int.TryParse(v, out iv)) { BtcConfig.ReplayApmMode.Value = Mathf.Clamp(iv, 0, 2); n++; }
                        else if (k == "layout") { BtcReplayLayout.Restore(v); n++; }
                    }
                    return n;
                }
            });
            Parts.Add(new Part
            {
                Key = "visual", En = "Visuals: rings, overlays, plates, colours", Ru = "Визуал: кольца, оверлеи, плашки, цвета", Icon = BtcReplayIcons.Eye,
                Capture = delegate { ResourcePackManager.EnsureVisualBuilt(); return BtcVisualData.Ready != null && BtcVisualData.Ready() ? BtcVisualProfiles.Capture() : ""; },
                Apply = delegate (string t) { ResourcePackManager.EnsureVisualBuilt(); return BtcVisualProfiles.Apply(t); }
            });
            Parts.Add(new Part
            {
                Key = "hotkeys", En = "Hotkeys (mod and vanilla binds)", Ru = "Хоткеи (моды и ванильные бинды)", Icon = BtcReplayIcons.Keys,
                Capture = BtcHotkeysProfiles.Snapshot,
                Apply = BtcHotkeysProfiles.Apply
            });
            Parts.Add(new Part
            {
                Key = "profile", En = "Profile: nick, plate, avatar", Ru = "Профиль: ник, плашка, аватар", Icon = BtcReplayIcons.Group,
                Capture = BtcProfileSets.Capture,
                Apply = delegate (string t) { BtcProfileSets.Apply(t); return 1; }
            });
            Parts.Add(new Part
            {
                Key = "rules", En = "Gameplay rules (host only, BetterConnection)", Ru = "Правила геймплея (только хост, BetterConnection)", Icon = BtcReplayIcons.Swords, DefaultOn = false,
                HintEn = "Applied only when you can edit the rules right now", HintRu = "Применяются, только если сейчас можно менять правила",
                Capture = delegate { return "rules=" + BtcGameplay.Encode(BtcGameplay.Raw) + "\n"; },
                Apply = delegate (string t)
                {
                    if (!BtcGameplay.CanEdit) return 0;
                    foreach (string l in t.Replace("\r", "").Split('\n'))
                        if (l.StartsWith("rules=", StringComparison.Ordinal)) { BtcGameplay.SetAll(BtcGameplay.Decode(l.Substring(6))); return 1; }
                    return 0;
                }
            });
        }

        internal static List<Part> All() { Init(); return Parts; }

        internal static bool PartOn(string key, bool def) { return BtcMenuState.Bool("setup:part:" + key, def); }
        internal static void SetPartOn(string key, bool on) { BtcMenuState.SetBool("setup:part:" + key, on); }

        // ------------------------------------------------------------------ text format
        /// <summary>The setup as text: [[key]] sections for the selected parts.</summary>
        internal static string Build(bool selectedOnly)
        {
            Init();
            StringBuilder sb = new StringBuilder();
            sb.Append(Magic).Append("\n");
            for (int i = 0; i < Parts.Count; i++)
            {
                Part p = Parts[i];
                if (selectedOnly && !PartOn(p.Key, p.DefaultOn)) continue;
                string c = "";
                try { c = p.Capture(); } catch (Exception e) { BtcLog.Error("Setups", "capture " + p.Key + ": " + e.Message); }
                if (string.IsNullOrEmpty(c)) continue;
                sb.Append("[[").Append(p.Key).Append("]]\n").Append(c.Replace("\r\n", "\n").TrimEnd('\n')).Append("\n");
            }
            return sb.ToString();
        }

        internal static Dictionary<string, string> Split(string text)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            if (text == null) return d;
            string[] lines = text.Replace("\r", "").Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != Magic) return d;
            string key = null;
            StringBuilder sb = new StringBuilder();
            for (int i = 1; i <= lines.Length; i++)
            {
                string l = i < lines.Length ? lines[i] : "[[end]]";
                if (l.StartsWith("[[", StringComparison.Ordinal) && l.EndsWith("]]", StringComparison.Ordinal))
                {
                    if (key != null) d[key] = sb.ToString();
                    key = l.Substring(2, l.Length - 4);
                    sb.Length = 0;
                }
                else if (key != null) sb.Append(l).Append("\n");
            }
            return d;
        }

        /// <summary>Applies the selected parts of a setup text as ONE undo step. Returns the names of the applied parts (EN) or null when the text is no setup.</summary>
        internal static List<string> ApplyText(string text, bool selectedOnly)
        {
            Init();
            Dictionary<string, string> d = Split(text);
            if (d.Count == 0) return null;
            List<string> done = new List<string>();
            BtcUndo.Group("uiedit", BtcReplayUI.T("Setup applied", "Применён сетап"), delegate
            {
                // undo of the whole thing = every touched part back to its state before
                Dictionary<string, string> before = new Dictionary<string, string>();
                for (int i = 0; i < Parts.Count; i++)
                {
                    Part p = Parts[i];
                    string body;
                    if (!d.TryGetValue(p.Key, out body)) continue;
                    if (selectedOnly && !PartOn(p.Key, p.DefaultOn)) continue;
                    try { before[p.Key] = p.Capture(); } catch (Exception) { }
                    int n = 0;
                    try { n = p.Apply(body); } catch (Exception e) { BtcLog.Error("Setups", "apply " + p.Key + ": " + e.Message); }
                    if (n > 0) done.Add(p.Key);
                }
                BtcUndo.Push("uiedit", BtcReplayUI.T("Setup applied", "Применён сетап"), delegate
                {
                    for (int i = 0; i < Parts.Count; i++)
                    {
                        string b;
                        if (!before.TryGetValue(Parts[i].Key, out b) || string.IsNullOrEmpty(b)) continue;
                        try { Parts[i].Apply(b); } catch (Exception) { }
                    }
                });
            });
            return done;
        }

        // ------------------------------------------------------------------ one-line code
        internal static string ToCode(string text)
        {
            byte[] raw = Encoding.UTF8.GetBytes(text);
            using (MemoryStream ms = new MemoryStream())
            {
                using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Compress, true)) ds.Write(raw, 0, raw.Length);
                return Magic + ":" + Convert.ToBase64String(ms.ToArray());
            }
        }

        internal static string FromCode(string code)
        {
            try
            {
                code = (code ?? "").Trim();
                if (code.StartsWith(Magic + "\n", StringComparison.Ordinal) || code.StartsWith(Magic + "\r", StringComparison.Ordinal)) return code;   // plain text pasted
                if (!code.StartsWith(Magic + ":", StringComparison.Ordinal)) return null;
                byte[] zip = Convert.FromBase64String(code.Substring(Magic.Length + 1).Trim());
                using (MemoryStream inp = new MemoryStream(zip))
                using (DeflateStream ds = new DeflateStream(inp, CompressionMode.Decompress))
                using (MemoryStream o = new MemoryStream())
                {
                    byte[] buf = new byte[4096];
                    int n;
                    while ((n = ds.Read(buf, 0, buf.Length)) > 0) o.Write(buf, 0, n);
                    return Encoding.UTF8.GetString(o.ToArray());
                }
            }
            catch (Exception) { return null; }
        }

        // ------------------------------------------------------------------ files
        internal static string Dir { get { return BtcPaths.Config("setups"); } }

        internal sealed class Entry { public string Name, Text; }

        internal static List<Entry> Own()
        {
            List<Entry> l = new List<Entry>();
            try
            {
                if (!Directory.Exists(Dir)) return l;
                string[] files = Directory.GetFiles(Dir, "*.txt");
                Array.Sort(files);
                for (int i = 0; i < files.Length; i++) l.Add(new Entry { Name = Path.GetFileNameWithoutExtension(files[i]), Text = File.ReadAllText(files[i], Encoding.UTF8) });
            }
            catch (Exception) { }
            return l;
        }

        internal static bool Save(string name, string text)
        {
            name = BtcUiEdit.SafeName(name);
            if (name.Length == 0) return false;
            try
            {
                if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, name + ".txt"), text, new UTF8Encoding(false));
                return true;
            }
            catch (Exception) { return false; }
        }

        internal static void Delete(string name) { try { string p = Path.Combine(Dir, name + ".txt"); if (File.Exists(p)) File.Delete(p); } catch (Exception) { } }

        /// <summary>Names of the parts of a setup text (for the list: what is inside).</summary>
        internal static string Contents(string text)
        {
            Init();
            Dictionary<string, string> d = Split(text);
            List<string> n = new List<string>();
            for (int i = 0; i < Parts.Count; i++) if (d.ContainsKey(Parts[i].Key)) n.Add(BtcReplayUI.T(Parts[i].En, Parts[i].Ru).Split(':')[0]);
            return string.Join(", ", n.ToArray());
        }
    }
}
