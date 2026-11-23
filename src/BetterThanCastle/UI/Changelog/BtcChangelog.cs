using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// In-game changelog data. Source = the embedded text file src/BetterThanCastle/Resources/changelog.btcl (format in its header; docs/menu.md "Changelog").
    /// Every entry can carry a link target that jumps to the tab / setting the entry is about.
    /// </summary>
    internal static class BtcChangelog
    {
        internal sealed class Entry
        {
            internal char Kind;          // + new, ~ reworked, * improved, = issue fixed (reported by a user), - removed
            internal string Area;        // AreaKeys value
            internal bool Exp;           // experimental badge
            internal string Target;      // null or "tab:N[:arg]" / "url:..."
            internal string En, Ru;
        }

        internal sealed class Release
        {
            internal string Version, Date, Label, Title;
            internal readonly List<Entry> Entries = new List<Entry>();
        }

        private static List<Release> _list;
        internal static List<Release> Releases { get { if (_list == null) _list = Load(); return _list; } }

        private static List<Release> Load()
        {
            List<Release> res = new List<Release>();
            try
            {
                using (Stream s = typeof(BtcChangelog).Assembly.GetManifestResourceStream("BetterThanCastle.changelog.btcl"))
                {
                    if (s == null) { BtcLog.Warn("Changelog", "resource missing"); return res; }
                    string text = new StreamReader(s, Encoding.UTF8).ReadToEnd();
                    string[] lines = text.Replace("\r", "").Split('\n');
                    Release cur = null;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string l = lines[i].Trim();
                        if (l.Length == 0 || l[0] == '#') continue;
                        string[] p = l.Substring(1).Split('|');
                        for (int k = 0; k < p.Length; k++) p[k] = p[k].Trim();
                        if (l[0] == '@')
                        {
                            cur = new Release { Version = p.Length > 0 ? p[0] : "?", Date = p.Length > 1 ? p[1] : "", Label = p.Length > 2 ? p[2] : "", Title = p.Length > 3 ? p[3] : "" };
                            res.Add(cur);
                        }
                        else if (cur != null && (l[0] == '+' || l[0] == '~' || l[0] == '*' || l[0] == '=' || l[0] == '-') && p.Length >= 4)
                        {
                            cur.Entries.Add(new Entry { Kind = l[0], Area = p[0], Target = p[1] == "-" || p[1].Length == 0 ? null : p[1], En = p[2], Ru = p[3], Exp = p.Length > 4 && p[4] == "exp" });
                        }
                        else BtcLog.Warn("Changelog", "bad line " + (i + 1) + ": " + l);
                    }
                }
            }
            catch (Exception e) { BtcLog.Error("Changelog", "load: " + e); }
            return res;
        }

        // ---- "new" badge on the rail until the player has opened the changelog for the current content
        private static string SeenFile { get { return BtcPaths.Config("changelog-seen.txt"); } }
        private static bool _seenLoaded;
        private static string _seen;

        private static string Signature()
        {
            List<Release> r = Releases;
            return r.Count == 0 ? "" : r[0].Version + ":" + r[0].Entries.Count + ":" + r.Count;
        }

        internal static bool HasUnseen
        {
            get
            {
                if (!_seenLoaded)
                {
                    _seenLoaded = true;
                    try { _seen = File.Exists(SeenFile) ? File.ReadAllText(SeenFile, Encoding.UTF8).Trim() : ""; } catch (Exception) { _seen = ""; }
                }
                return Releases.Count > 0 && _seen != Signature();
            }
        }

        internal static void MarkSeen()
        {
            try
            {
                _seen = Signature(); _seenLoaded = true;
                File.WriteAllText(SeenFile, _seen, new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        // changelog places (own list: the bug report form indexes BtcReportData.Area*)
        internal static readonly string[] AreaKeys = { "replay", "hotkeys", "visuals", "gameplay", "terraform", "mapedit", "shields", "spectate", "hud", "profile", "perf", "multiplayer", "duel", "packs", "menu", "other" };
        internal static readonly string[] AreaEn = { "Replay", "Hotkeys", "Visuals", "Gameplay", "Terraforming", "Map editor", "Base shields", "Spectators and pings", "Statistics and HUD", "Profile", "Performance and loading", "Multiplayer", "Duel", "Packs", "Menu / UI", "Other" };
        internal static readonly string[] AreaRu = { "Реплеи", "Хоткеи", "Визуал", "Геймплей", "Терраформинг", "Редактор карт", "Щиты баз", "Зрители и пинги", "Статистика и HUD", "Профиль", "Производительность и загрузка", "Мультиплеер", "Дуэль", "Паки", "Меню / UI", "Другое" };

        // ---- link targets
        internal static readonly string[] TabNameEn = { "Resource Packs", "Visuals", "BetterConnection", "Advanced Hotkeys", "Gameplay", "Share", "Profile", "Replay", "Practice", "Report a bug", "Changelog", "Statistics", "UI editor", "Notifications", "Performance" };
        internal static readonly string[] TabNameRu = { "Ресурс-паки", "Визуал", "BetterConnection", "Advanced Hotkeys", "Геймплей", "Share", "Профиль", "Реплеи", "Практика", "Сообщить о баге", "Что нового", "Статистика", "UI редактор", "Уведомления", "Производительность" };

        /// <summary>Where a target leads, for the tooltip / chip ("Replay", "Advanced Hotkeys", "web").</summary>
        internal static string TargetName(string target)
        {
            if (target == null) return null;
            if (target.StartsWith("url:", StringComparison.Ordinal)) return BtcReplayUI.T("Open link", "Открыть ссылку");
            int id;
            if (ParseTab(target, out id, null) && id >= 0 && id < TabNameEn.Length) return BtcReplayUI.T(TabNameEn[id], TabNameRu[id]);
            return null;
        }

        private static bool ParseTab(string target, out int id, string[] arg)
        {
            id = -1;
            if (!target.StartsWith("tab:", StringComparison.Ordinal)) return false;
            string[] p = target.Split(new[] { ':' }, 3);
            if (!int.TryParse(p[1], out id)) return false;
            if (arg != null) arg[0] = p.Length > 2 ? p[2] : null;
            return true;
        }

        /// <summary>Follows a link: opens the tab and (optionally) the exact page / bind. Returns false when the target is unknown.</summary>
        internal static bool Go(string target)
        {
            try
            {
                if (target == null) return false;
                if (target.StartsWith("url:", StringComparison.Ordinal)) { Application.OpenURL(target.Substring(4)); return true; }
                string[] arg = new string[1];
                int id;
                if (!ParseTab(target, out id, arg)) return false;
                if (!ResourcePackManager.GoTab(id)) return false;
                // the target tab opens unfiltered: its own search could hide the row the link points at
                BtcMenu.ClearSearch(id);
                string a = arg[0];
                if (a != null)
                {
                    int pg;
                    bool num = int.TryParse(a, out pg);
                    if (id == 3) BtcHotkeysTab.Instance.Jump(a);
                    else if (id == 1)
                    {
                        if (num) BtcVisualTab.Instance.SetPage(pg);
                        else { BtcVisualSetting s = BtcVisualData.Find(a); if (s != null) BtcVisualTab.Instance.JumpSetting(s); }
                    }
                    else if (id == 4)
                    {
                        if (num) BtcGameplayTab.Page = pg;
                        else { int ri = BtcGameplay.Idx(a); if (ri >= 0) BtcGameplayTab.JumpRule(ri); else BtcLog.Warn("Changelog", "unknown rule " + a); }
                    }
                    else if (id == 2) BtcBcTab.Instance.Jump(a);
                    else if (id == 14) BtcPerfTab.Instance.Jump(a);
                    else if (id == 6 && num) BtcProfileTab.Instance.SetPage(pg);
                }
                return true;
            }
            catch (Exception e) { BtcLog.Error("Changelog", "go " + target + ": " + e); return false; }
        }
    }
}
