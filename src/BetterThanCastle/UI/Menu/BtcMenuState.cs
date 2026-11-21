using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BetterThanCastle
{
    /// <summary>
    /// Remembers the menu UI state between sessions (file configs/menu-state.txt, "key=value" lines): the open tab, the page of every tab, anything small a tab wants kept.
    /// RULE (docs/menu.md): every piece of UI state of a tab (page, filter, view mode, ...) goes through here; collapsed folds are kept by BtcUi.Fold (menu-ui.txt).
    /// Writes happen only when a value changes.
    /// </summary>
    internal static class BtcMenuState
    {
        private static readonly Dictionary<string, string> Map = new Dictionary<string, string>();
        private static bool _loaded;

        private static string FilePath { get { return BtcPaths.Config("menu-state.txt"); } }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                string[] l = File.ReadAllLines(FilePath, Encoding.UTF8);
                for (int i = 0; i < l.Length; i++)
                {
                    int eq = l[i].IndexOf('=');
                    if (eq > 0) Map[l[i].Substring(0, eq)] = l[i].Substring(eq + 1);
                }
            }
            catch (Exception) { }
        }

        private static void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, string> kv in Map) sb.Append(kv.Key).Append('=').Append(kv.Value).Append("\r\n");
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        internal static string Get(string key, string def)
        {
            Load();
            string v;
            return Map.TryGetValue(key, out v) ? v : def;
        }

        internal static void Set(string key, string value)
        {
            Load();
            string v;
            if (Map.TryGetValue(key, out v) && v == value) return;
            Map[key] = value;
            Save();
        }

        internal static int Int(string key, int def)
        {
            int r;
            return int.TryParse(Get(key, null), out r) ? r : def;
        }

        internal static void SetInt(string key, int value) { Set(key, value.ToString()); }

        internal static bool Bool(string key, bool def) { return Get(key, def ? "1" : "0") == "1"; }
        internal static void SetBool(string key, bool value) { Set(key, value ? "1" : "0"); }
    }
}
