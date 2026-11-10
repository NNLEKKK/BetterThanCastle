using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Brix.Utils;
using Rewired;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Adapter over the vanilla key bindings (BindingsProvider: merged bind items per Rewired action, saved to Info/Lua/userconfigs.lua by the game's own
    /// options menu). The hotkeys tab edits them through the same static maps and file, then calls BindingsProvider.Reload, so the result is
    /// identical to what the vanilla menu produces. Vanilla can carry only keyboard combos of one key or one modifier + key; mouse and wheel binds
    /// come from the Rewired mouse map and are shown read-only. Every call is wrapped: the maps are empty until the game loaded its controls.
    /// </summary>
    internal static class BtcVanillaBinds
    {
        internal sealed class Act { public string name, title, cat; }

        private static readonly Dictionary<string, string> CatTitle = new Dictionary<string, string>
        {
            { "Camera", "Camera" }, { "General", "General" }, { "tb_constr_menu", "Build menu" }, { "Bricktron", "Units" }, { "TaskMenu", "Tasks" },
            { "Projects", "Projects" }, { "BlocksMenu", "Blocks" }, { "StructuresMenu", "Structures" }, { "Blueprints", "Blueprints" },
            { "Crafting", "Crafting" }, { "we_menu", "Editor" }, { "we_terrain_menu", "Editor: terrain" }, { "we_markers_menu", "Editor: markers" }
        };

        private static List<Act> _cache;
        private static int _cacheCount = -1;
        private static float _loadTry = -100f;

        /// <summary>Vanilla actions the user may rebind, in category order. Empty while the game has not loaded its bindings.</summary>
        internal static List<Act> Actions()
        {
            try
            {
                int n = BindingsProvider.mergedBindItemMap.Count;
                if (n == 0 && Time.realtimeSinceStartup - _loadTry > 3f)
                {
                    // the game fills the maps only when its options menu or the Lua loader asks: do the same ourselves, so the tab never needs the vanilla menu
                    _loadTry = Time.realtimeSinceStartup;
                    try { BindingsProvider.Reload(false); } catch (Exception) { }
                    n = BindingsProvider.mergedBindItemMap.Count;
                }
                if (_cache != null && n == _cacheCount) return _cache;
                _cacheCount = n;
                List<Act> list = new List<Act>();
                if (n > 0)
                {
                    for (int c = 0; c < BindingsProvider.ActionCategories.Count; c++)
                    {
                        string cat = BindingsProvider.ActionCategories[c];
                        foreach (InputAction a in ReInput.mapping.ActionsInCategory(cat))
                        {
                            if (!a.userAssignable || !BindingsProvider.mergedBindItemMap.ContainsKey(a.name)) continue;
                            string title = a.descriptiveName;
                            try { title = I2Helper.TryGet(a.descriptiveName); } catch (Exception) { }
                            if (string.IsNullOrEmpty(title)) title = a.name;
                            string ct;
                            if (!CatTitle.TryGetValue(cat, out ct)) ct = cat;
                            list.Add(new Act { name = a.name, title = title, cat = ct });
                        }
                    }
                }
                _cache = list;
                return list;
            }
            catch (Exception e)
            {
                BtcLog.Warn("Hotkeys", "vanilla actions: " + e.Message);
                _cache = new List<Act>();
                return _cache;
            }
        }

        internal static string TitleOf(string action)
        {
            List<Act> acts = Actions();
            for (int i = 0; i < acts.Count; i++) if (acts[i].name == action) return acts[i].title;
            return action;
        }

        internal static List<BindingsProvider.BindItem> Items(string action)
        {
            List<BindingsProvider.BindItem> l;
            return BindingsProvider.mergedBindItemMap.TryGetValue(action, out l) ? l : null;
        }

        /// <summary>Keyboard keys of one item (modifier first), or null when the item is not a plain keyboard combo.</summary>
        internal static KeyCode[] Combo(BindingsProvider.BindItem it)
        {
            if (it == null || it._entries == null || it._entries.Count == 0 || it._entries.Count > 2) return null;
            KeyCode[] r = new KeyCode[it._entries.Count];
            for (int i = 0; i < r.Length; i++)
            {
                BindingsProvider.BindItem.KeyboardEntry k = it._entries[i] as BindingsProvider.BindItem.KeyboardEntry;
                if (k == null) return null;
                r[i] = k._keyCode;
            }
            if (r.Length == 2 && IsModifier(r[1]) && !IsModifier(r[0])) { KeyCode t = r[0]; r[0] = r[1]; r[1] = t; }
            return r;
        }

        internal static string Label(BindingsProvider.BindItem it)
        {
            KeyCode[] c = Combo(it);
            if (c != null) return BtcHotkeys.ComboLabel(c, false);
            if (it == null || it._entries == null || it._entries.Count == 0) return string.Empty;
            string[] p = new string[it._entries.Count];
            for (int i = 0; i < p.Length; i++) p[i] = it._entries[i].ToPrettyEntryString();
            return string.Join(" + ", p);
        }

        internal static bool IsModifier(KeyCode k)
        {
            return k == KeyCode.LeftShift || k == KeyCode.RightShift || k == KeyCode.LeftControl || k == KeyCode.RightControl || k == KeyCode.LeftAlt || k == KeyCode.RightAlt;
        }

        /// <summary>Vanilla takes at most [modifier, key] (or one key).</summary>
        internal static bool Fits(KeyCode[] combo)
        {
            if (combo == null || combo.Length == 0 || combo.Length > 2) return false;
            return combo.Length == 1 || IsModifier(combo[0]);
        }

        internal static bool Editable(BindingsProvider.BindItem it) { return Combo(it) != null; }

        /// <summary>Sets slot idx of an action (idx past the end appends a new slot). combo == null clears the slot. Writes the file and reloads.</summary>
        internal static bool Set(string action, int idx, KeyCode[] combo)
        {
            try
            {
                List<BindingsProvider.BindItem> l = Items(action);
                if (l == null) return false;
                if (combo != null && !Fits(combo)) return false;
                string snap = Serialize();
                List<BindingsProvider.BindItem.Entry> entries = new List<BindingsProvider.BindItem.Entry>();
                if (combo != null)
                {
                    for (int i = 0; i < combo.Length; i++) entries.Add(BindingsProvider.BindItem.Entry.FromEntryString(combo[i].ToString()));
                }
                if (idx >= 0 && idx < l.Count)
                {
                    // never edit the shared default item in place: swap in a new one
                    if (combo == null && l.Count > 2) l.RemoveAt(idx);
                    else l[idx] = new BindingsProvider.BindItem(action, entries, l[idx]._rewiredElementIdentifierId, true);
                }
                else if (combo != null)
                {
                    BindingsProvider.BindItem n = new BindingsProvider.BindItem(action, entries, -1, true);
                    l.Add(n);
                }
                bool okSet = Save();
                if (okSet) PushUndo(BtcReplayUI.T("Vanilla bind: ", "Ванильный бинд: ") + TitleOf(action), snap);
                return okSet;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hotkeys", "vanilla bind set " + action, e);
                return false;
            }
        }

        /// <summary>"v:action=Combo|Combo" lines for the keyboard slots of every vanilla action (used by bind profiles).</summary>
        internal static List<string> ExportLines()
        {
            List<string> lines = new List<string>();
            try
            {
                List<Act> acts = Actions();
                for (int i = 0; i < acts.Count; i++)
                {
                    List<BindingsProvider.BindItem> l = Items(acts[i].name);
                    if (l == null) continue;
                    List<string> parts = new List<string>();
                    for (int k = 0; k < l.Count; k++)
                    {
                        KeyCode[] c = Combo(l[k]);
                        if (c != null) parts.Add(string.Join("+", Array.ConvertAll(c, x => x.ToString())));
                    }
                    lines.Add("v:" + acts[i].name + "=" + string.Join("|", parts.ToArray()));
                }
            }
            catch (Exception e) { BtcLog.Error("Hotkeys", "vanilla export", e); }
            return lines;
        }

        /// <summary>Applies "v:action=..." lines (other lines are ignored), writes the file once. Returns the number of actions set.</summary>
        internal static int ImportLines(IList<string> lines)
        {
            int n = 0;
            string snapImport = Serialize();
            try
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    string t = lines[i].Trim();
                    int eq = t.IndexOf('=');
                    if (!t.StartsWith("v:") || eq < 3) continue;
                    string action = t.Substring(2, eq - 2);
                    List<BindingsProvider.BindItem> old = Items(action);
                    if (old == null) continue;
                    List<BindingsProvider.BindItem> fresh = new List<BindingsProvider.BindItem>();
                    string[] alts = t.Substring(eq + 1).Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int a = 0; a < alts.Length; a++)
                    {
                        List<KeyCode> combo = new List<KeyCode>();
                        string[] ks = alts[a].Split('+');
                        bool ok = true;
                        for (int k = 0; k < ks.Length && ok; k++)
                        {
                            try { combo.Add((KeyCode)Enum.Parse(typeof(KeyCode), ks[k].Trim(), true)); } catch (Exception) { ok = false; }
                        }
                        if (!ok || !Fits(combo.ToArray())) continue;
                        List<BindingsProvider.BindItem.Entry> entries = new List<BindingsProvider.BindItem.Entry>();
                        for (int k = 0; k < combo.Count; k++) entries.Add(BindingsProvider.BindItem.Entry.FromEntryString(combo[k].ToString()));
                        fresh.Add(new BindingsProvider.BindItem(action, entries, -1, true));
                    }
                    for (int k = 0; k < old.Count; k++) if (old[k]._entries != null && old[k]._entries.Count > 0 && Combo(old[k]) == null) fresh.Add(old[k]);
                    while (fresh.Count < 2) fresh.Add(new BindingsProvider.BindItem(action));
                    BindingsProvider.mergedBindItemMap[action] = fresh;
                    n++;
                }
                if (n > 0) { Save(); PushUndo(BtcReplayUI.T("Vanilla binds from profile", "Ванильные бинды из профиля"), snapImport); }
            }
            catch (Exception e) { BtcLog.Error("Hotkeys", "vanilla import", e); }
            return n;
        }

        internal static bool ResetAll()
        {
            try
            {
                string snapReset = Serialize();
                BindingsProvider.Reload(true);
                bool okReset = Save();
                if (okReset) PushUndo(BtcReplayUI.T("Reset vanilla binds", "Сброс ванильных биндов"), snapReset);
                return okReset;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hotkeys", "vanilla reset", e);
                return false;
            }
        }

        /// <summary>Same file and format as the vanilla options menu (BindingsProvider.SaveLuaFile), then Reload so Rewired takes the change at once.</summary>
        private static string Serialize()
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, List<BindingsProvider.BindItem>> kv in BindingsProvider.mergedBindItemMap)
            {
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    BindingsProvider.BindItem it = kv.Value[i];
                    sb.Append("Bind(\"").Append(it._trigger).Append("\", {");
                    for (int k = 0; k < it._entries.Count; k++)
                    {
                        if (k > 0) sb.Append(", ");
                        sb.Append('"').Append(it._entries[k].ToEntryString(false)).Append('"');
                    }
                    sb.Append("}, {})\n");
                }
            }
            return sb.ToString();
        }

        private static void Restore(string text)
        {
            File.WriteAllText("Info/Lua/userconfigs.lua", text);
            BindingsProvider.Reload(false);
            _cache = null;
        }

        private static void PushUndo(string label, string snap)
        {
            BtcUndo.Push("hotkeys", label, delegate { Restore(snap); });
        }

        private static bool Save()
        {
            File.WriteAllText("Info/Lua/userconfigs.lua", Serialize());
            BindingsProvider.Reload(false);
            _cache = null;
            return true;
        }
    }
}
