using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Profile sets: a named snapshot of everything on the Profile tab (nick slots and colours, gradient, animation, plate settings, plate image,
    /// avatar, master switch) as a UTF-8 text file in configs/profile-sets/*.txt. The same text can be copied to the clipboard and pasted by a friend.
    /// Sections: plain "key=value" lines for the nick state, "plate." prefix for the plate settings, "avatar=", "plateimg=", "on=".
    /// </summary>
    internal static class BtcProfileSets
    {
        internal static string Dir
        {
            get
            {
                string d = Path.Combine(Path.GetDirectoryName(BtcPaths.Config("btc_nick.txt")), "profile-sets");
                try { if (!Directory.Exists(d)) Directory.CreateDirectory(d); } catch (Exception) { }
                return d;
            }
        }

        internal static string Safe(string n)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n.Trim();
        }

        internal static string Capture()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("# Better Than Castle: profile set\n");
            sb.Append("on=").Append(BtcNick.StyleOn ? "1" : "0").Append("\n");
            sb.Append(BtcNick.ExportState());
            string[] pl = BtcPlate.Export().Split('\n');
            for (int i = 0; i < pl.Length; i++) if (pl[i].Length > 0) sb.Append("plate.").Append(pl[i]).Append("\n");
            sb.Append("avatar=").Append(BtcAvatar.CurrentFile ?? "").Append("\n");
            sb.Append("plateimg=").Append(BtcPlateImg.SelectedFile ?? "").Append("\n");
            return sb.ToString();
        }

        internal static Dictionary<string, string> Parse(string text)
        {
            Dictionary<string, string> kv = new Dictionary<string, string>();
            if (text == null) return kv;
            string[] lines = text.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                if (l.Length == 0 || l[0] == '#') continue;
                int eq = l.IndexOf('=');
                if (eq > 0) kv[l.Substring(0, eq).Trim()] = l.Substring(eq + 1);
            }
            return kv;
        }

        /// <summary>True when the text looks like a profile set (has at least one known key).</summary>
        internal static bool Looks(string text)
        {
            Dictionary<string, string> kv = Parse(text);
            return kv.ContainsKey("nick") || kv.ContainsKey("plate.style") || kv.ContainsKey("grad");
        }

        internal static void Apply(string text)
        {
            Dictionary<string, string> kv = Parse(text);
            StringBuilder plate = new StringBuilder();
            foreach (KeyValuePair<string, string> p in kv)
                if (p.Key.StartsWith("plate.")) plate.Append(p.Key.Substring(6)).Append('=').Append(p.Value).Append("\n");
            BtcNick.ImportState(kv);
            if (plate.Length > 0) BtcPlate.Import(plate.ToString());
            string v;
            if (kv.TryGetValue("avatar", out v) && v != (BtcAvatar.CurrentFile ?? "")) BtcAvatar.Choose(v);
            if (kv.TryGetValue("plateimg", out v) && v != (BtcPlateImg.SelectedFile ?? "")) BtcPlateImg.Choose(v);
            if (kv.TryGetValue("on", out v)) BtcNick.SetStyleOn(v != "0");
        }

        internal struct Entry
        {
            internal string Name; internal string Text; internal DateTime Time;
        }

        internal static List<Entry> List()
        {
            List<Entry> l = new List<Entry>();
            try
            {
                FileInfo[] fi = new DirectoryInfo(Dir).GetFiles("*.txt");
                for (int i = 0; i < fi.Length; i++)
                {
                    Entry e = new Entry();
                    e.Name = Path.GetFileNameWithoutExtension(fi[i].Name);
                    e.Time = fi[i].LastWriteTime;
                    e.Text = File.ReadAllText(fi[i].FullName, Encoding.UTF8);
                    l.Add(e);
                }
                l.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception) { }
            return l;
        }

        internal static void Write(string name, string text)
        {
            try { File.WriteAllText(Path.Combine(Dir, Safe(name) + ".txt"), text, new UTF8Encoding(false)); }
            catch (Exception ex) { BtcLog.Warn("Profile", "save set failed: " + ex.Message); }
        }

        internal static void Delete(string name)
        {
            try { File.Delete(Path.Combine(Dir, Safe(name) + ".txt")); } catch (Exception) { }
        }

        internal static bool Exists(string name) { return File.Exists(Path.Combine(Dir, Safe(name) + ".txt")); }
    }
}
