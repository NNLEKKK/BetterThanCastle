using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// The saved-replays part of the Replay tab. Grouping into collapsible folders (by map, date, player, mode, own folder), three views (big cards,
    /// list, compact lines), search, sorting, MP/SP filter, favourites, own folders and names (Replays/replays-meta.txt), a wide details panel under
    /// the selected item. Map thumbnails come from BtcReplayPreview and are shown big and cropped, without frames.
    /// </summary>
    internal static class BtcReplayLibrary
    {
        internal static bool SettingsOpen;

        private sealed class Meta { internal bool Fav; internal string Folder = ""; internal string Name = ""; }
        private sealed class Grp { internal string Key, Name; internal int Icon; internal Color Col = Color.clear; internal readonly List<BtcReplay.Data> Items = new List<BtcReplay.Data>(); }

        private static Dictionary<string, Meta> _meta;
        private static readonly List<string> Folders = new List<string>();
        private static readonly HashSet<string> Open = new HashSet<string>();
        private static bool _metaDirty;
        private static float _metaSaveAt;

        private static int _filter;            // 0 all, 1 favourites, 2+ folder index + 2
        private static int _sort;              // 0 newest, 1 oldest, 2 longest, 3 map, 4 players
        private static int _group = 1;         // 0 none, 1 map, 2 date, 3 player, 4 mode, 5 own folder
        private static int _viewMode;          // 0 cards, 1 list, 2 compact
        private static int _mode;              // 0 all, 1 MP, 2 SP
        private static string _q = "", _newFolder = "";
        private static bool _addingFolder;
        private static string _selected, _confirmDelete;
        private static float _confirmUntil;
        private static readonly List<Grp> Groups = new List<Grp>();
        private static string _viewKey = "";
        private static int _shown;
        private static GUIStyle _lbl, _dim, _ttl, _badge, _tf;
        private static int _styleFs;
        private static Texture2D _grad;
        private static readonly Color Blue = new Color(0.35f, 0.65f, 1f, 1f);
        private static readonly HashSet<string> Seen = new HashSet<string>();
        private static DateTime _procStart = DateTime.MinValue;
        private static readonly Dictionary<string, DateTime> Born = new Dictionary<string, DateTime>();
        private static readonly HashSet<string> ClosedFresh = new HashSet<string>();
        private static float _nextScan;
        private static string _scanSig = "";

        // a replay recorded since the game started and not opened yet is "new" (blue); restarting the game clears it
        private static bool IsFresh(BtcReplay.Data d)
        {
            if (_procStart == DateTime.MinValue) { try { _procStart = System.Diagnostics.Process.GetCurrentProcess().StartTime; } catch (Exception) { _procStart = DateTime.Now; } }
            if (Seen.Contains(d.FileName)) return false;
            DateTime born;
            if (!Born.TryGetValue(d.FilePath, out born))
            {
                try { born = File.GetCreationTime(d.FilePath); } catch (Exception) { born = DateTime.MinValue; }
                if (d.Ticks > 0L && new DateTime(d.Ticks) > born) born = new DateTime(d.Ticks);   // recorded now (creation time of a copied file is its copy time)
                Born[d.FilePath] = born;
            }
            return born >= _procStart;
        }

        // match format badge (1v1, 2v2, FFA3 ...) right of the MP/SP badge; returns the x where the next badge starts
        private static float FmtBadge(Rect mb, BtcReplay.Data d, int fs)
        {
            string f = InfoOf(d).Format;
            float x = mb.xMax + fs * 0.3f;
            if (f.Length == 0) return x;
            Rect r = new Rect(x, mb.y, fs * (0.9f + f.Length * 0.5f), mb.height);
            BtcReplayUI.Fill(r, new Color(0f, 0f, 0f, 0.6f));
            Txt(r, f, _badge, new Color(1f, 0.92f, 0.6f, 1f));
            return r.xMax + fs * 0.3f;
        }

        private static bool HasFresh(Grp g) { for (int i = 0; i < g.Items.Count; i++) if (IsFresh(g.Items[i])) return true; return false; }

        private static void NewPill(Rect r, BtcReplay.Data d, Grp g)
        {
            BtcReplayUI.Fill(r, r.Contains(Event.current.mousePosition) ? new Color(0.55f, 0.78f, 1f, 1f) : Blue);
            Txt(r, Tt("NEW", "НОВЫЙ"), _badge, new Color(0.02f, 0.05f, 0.1f, 1f));
            BtcReplayIcons.Hint(r, g != null ? Tt("Mark all in this folder as seen", "Отметить всё в папке просмотренным") : Tt("Mark as seen", "Отметить просмотренным"));
            if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn))
            {
                if (g != null) { for (int i = 0; i < g.Items.Count; i++) Seen.Add(g.Items[i].FileName); }
                else Seen.Add(d.FileName);
                _viewKey = "";
            }
        }

        private static void AutoRefresh()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 2f;
            try
            {
                string dir = BtcReplay.ReplaysDir;
                if (!Directory.Exists(dir)) return;
                string rec = BtcReplay.Mode == BtcReplay.State.Recording && BtcReplay.RecFile != null ? Path.GetFileName(BtcReplay.RecFile) : null;
                StringBuilder sb = new StringBuilder();
                foreach (string fp in Directory.GetFiles(dir, "*.btcr"))
                {
                    string n = Path.GetFileName(fp);
                    if (n == rec) continue;
                    sb.Append(n).Append(new FileInfo(fp).Length).Append(';');
                }
                string sig = sb.ToString();
                if (sig == _scanSig) return;
                bool first = _scanSig.Length == 0 && BtcReplayUI.list.Count > 0;
                _scanSig = sig;
                if (!first) { BtcReplayUI.Refresh(); _viewKey = ""; }
            }
            catch (Exception ex) { BtcLog.Warn("ReplayLib", "scan: " + ex.Message); }
        }

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }
        private static Color Accent { get { return BtcMenu.Accent; } }

        // ------------------------------------------------------------------ meta (favourites, folders, names, view prefs, open groups)
        private static string MetaFile { get { return Path.Combine(Path.Combine(BtcPaths.Root, "Replays"), "replays-meta.txt"); } }

        private static void LoadMeta()
        {
            if (_meta != null) return;
            _meta = new Dictionary<string, Meta>();
            PurgeTrash();
            Folders.Clear(); Open.Clear();
            try
            {
                if (!File.Exists(MetaFile)) return;
                string[] lines = File.ReadAllLines(MetaFile, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] p = lines[i].Split('\t');
                    if (p.Length >= 2 && p[0] == "#folder") { if (p[1].Length > 0 && !Folders.Contains(p[1])) Folders.Add(p[1]); continue; }
                    if (p.Length >= 2 && p[0] == "#open") { Open.Add(p[1]); continue; }
                    if (p.Length >= 3 && p[0] == "#pref")
                    {
                        int v; int.TryParse(p[2], out v);
                        if (p[1] == "group") _group = Mathf.Clamp(v, 0, 5); else if (p[1] == "view") _viewMode = Mathf.Clamp(v, 0, 2); else if (p[1] == "sort") _sort = Mathf.Clamp(v, 0, 4);
                        continue;
                    }
                    if (p.Length < 4) continue;
                    Meta m = new Meta { Fav = p[1] == "1", Folder = p[2], Name = p[3] };
                    _meta[p[0]] = m;
                    if (m.Folder.Length > 0 && !Folders.Contains(m.Folder)) Folders.Add(m.Folder);
                }
            }
            catch (Exception ex) { BtcLog.Error("ReplayLib", "meta load: " + ex.Message); }
        }

        private static void SaveMeta()
        {
            _metaDirty = false;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("#pref\tgroup\t").Append(_group).Append('\n').Append("#pref\tview\t").Append(_viewMode).Append('\n').Append("#pref\tsort\t").Append(_sort).Append('\n');
                for (int i = 0; i < Folders.Count; i++) sb.Append("#folder\t").Append(Folders[i]).Append('\n');
                foreach (string o in Open) sb.Append("#open\t").Append(o.Replace('\t', ' ').Replace('\n', ' ')).Append('\n');
                foreach (KeyValuePair<string, Meta> kv in _meta)
                {
                    Meta m = kv.Value;
                    if (!m.Fav && m.Folder.Length == 0 && m.Name.Length == 0) continue;
                    sb.Append(kv.Key).Append('\t').Append(m.Fav ? '1' : '0').Append('\t').Append(m.Folder).Append('\t').Append(m.Name.Replace('\t', ' ').Replace('\n', ' ')).Append('\n');
                }
                Directory.CreateDirectory(Path.GetDirectoryName(MetaFile));
                File.WriteAllText(MetaFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex) { BtcLog.Error("ReplayLib", "meta save: " + ex.Message); }
        }

        // ---- soft delete: the file moves to Replays/.trash (restored by Ctrl+Z, cleaned after 14 days); a file that is open falls back to the plain delete
        private static string TrashDir { get { return Path.Combine(Path.Combine(BtcPaths.Root, "Replays"), ".trash"); } }

        private static bool SoftDelete(BtcReplay.Data d)
        {
            string src = d.FilePath;
            try
            {
                Directory.CreateDirectory(TrashDir);
                string dst = Path.Combine(TrashDir, Path.GetFileName(src));
                if (File.Exists(dst)) dst = Path.Combine(TrashDir, DateTime.Now.ToString("yyyyMMdd-HHmmss-") + Path.GetFileName(src));
                File.Move(src, dst);
                BtcUndo.Push("replay", Tt("Replay file", "Файл реплея"), delegate
                {
                    try { if (File.Exists(dst) && !File.Exists(src)) File.Move(dst, src); BtcReplayUI.Refresh(); _viewKey = ""; } catch (Exception ex) { BtcLog.Error("ReplayLib", "restore: " + ex.Message); }
                });
                return true;
            }
            catch (Exception ex)
            {
                BtcLog.Warn("ReplayLib", "soft delete failed, deleting: " + ex.Message);
                return BtcReplay.Delete(src);
            }
        }

        private static void PurgeTrash()
        {
            try
            {
                if (!Directory.Exists(TrashDir)) return;
                foreach (string fp in Directory.GetFiles(TrashDir)) if ((DateTime.Now - File.GetLastWriteTime(fp)).TotalDays > 14.0) File.Delete(fp);
            }
            catch (Exception) { }
        }

        // ---- undo (scope "replay"): favourites, folders, names; the state is compared after every change and the previous one is restored by Ctrl+Z
        private static string _undoSnap;

        private static string SerializeUndo()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Folders.Count; i++) sb.Append("F\t").Append(Folders[i]).Append('\n');
            List<string> keys = new List<string>(_meta.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                Meta m = _meta[keys[i]];
                if (!m.Fav && m.Folder.Length == 0 && m.Name.Length == 0) continue;
                sb.Append("M\t").Append(keys[i]).Append('\t').Append(m.Fav ? '1' : '0').Append('\t').Append(m.Folder).Append('\t').Append(m.Name.Replace('\t', ' ').Replace('\n', ' ')).Append('\n');
            }
            return sb.ToString();
        }

        private static void RestoreUndoState(string state)
        {
            _meta.Clear(); Folders.Clear();
            string[] lines = state.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string[] p = lines[i].Split('\t');
                if (p.Length >= 2 && p[0] == "F") Folders.Add(p[1]);
                else if (p.Length >= 5 && p[0] == "M") _meta[p[1]] = new Meta { Fav = p[2] == "1", Folder = p[3], Name = p[4] };
            }
            _undoSnap = state;
            Dirty(true);
        }

        private static void CheckUndo()
        {
            if (_meta == null) return;
            string cur = SerializeUndo();
            if (_undoSnap == null) { _undoSnap = cur; return; }
            if (cur == _undoSnap) return;
            string prev = _undoSnap, kind = Tt("Replay library", "Библиотека реплеев");
            if (prev.Split('F').Length != cur.Split('F').Length || FolderLines(prev) != FolderLines(cur)) kind = Tt("Folders", "Папки");
            else
            {
                string[] a = prev.Split('\n'), b = cur.Split('\n');
                string ka = ChangedLine(a, b);
                if (ka != null)
                {
                    string[] pa = ka.Split('\t');
                    Meta before;
                    string[] oldp = FindLine(a, pa.Length > 1 ? pa[1] : "");
                    if (oldp == null || (pa.Length > 4 && oldp.Length > 4 && oldp[2] != pa[2])) kind = Tt("Favourite", "Избранное");
                    else if (pa.Length > 4 && oldp.Length > 4 && oldp[3] != pa[3]) kind = Tt("Moved to a folder", "Перенос в папку");
                    else kind = Tt("Replay name", "Название реплея");
                    before = null;
                }
            }
            BtcUndo.Push("replay", kind, delegate { RestoreUndoState(prev); }, kind);
            _undoSnap = cur;
        }

        private static string FolderLines(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string l in s.Split('\n')) if (l.StartsWith("F\t")) sb.Append(l).Append('|');
            return sb.ToString();
        }

        private static string ChangedLine(string[] a, string[] b)
        {
            HashSet<string> sa = new HashSet<string>(a);
            for (int i = 0; i < b.Length; i++) if (b[i].StartsWith("M\t") && !sa.Contains(b[i])) return b[i];
            return null;
        }

        private static string[] FindLine(string[] a, string file)
        {
            for (int i = 0; i < a.Length; i++) { string[] p = a[i].Split('\t'); if (p.Length > 4 && p[0] == "M" && p[1] == file) return p; }
            return null;
        }

        private static Meta M(BtcReplay.Data d)
        {
            LoadMeta();
            Meta m;
            if (!_meta.TryGetValue(d.FileName, out m)) { m = new Meta(); _meta[d.FileName] = m; }
            return m;
        }

        private static void Dirty(bool rebuild) { CheckUndo(); _metaDirty = true; _metaSaveAt = Time.unscaledTime + 1.5f; if (rebuild) _viewKey = ""; }

        // ------------------------------------------------------------------ helpers
        private static string MapOf(BtcReplay.Data d) { return string.IsNullOrEmpty(d.MapName) ? (string.IsNullOrEmpty(d.Map) ? "?" : d.Map) : d.MapName; }

        private static string Title(BtcReplay.Data d)
        {
            Meta m = M(d);
            return m.Name.Length > 0 ? m.Name : MapOf(d);
        }

        private static string Clip(string s, int max) { return s != null && s.Length > max ? s.Substring(0, Mathf.Max(1, max - 1)) + ".." : s; }

        private struct Pl { internal string Nick; internal Color Col; internal int Team; }   // Team -1 = not recorded (older replays)

        private sealed class Info { internal List<Pl> Pl; internal string Format; }
        private static readonly Dictionary<string, Info> InfoCache = new Dictionary<string, Info>();

        private static Info InfoOf(BtcReplay.Data d)
        {
            string key = d.FilePath + "|" + d.FileBytes;
            Info inf;
            if (InfoCache.TryGetValue(key, out inf)) return inf;
            inf = new Info { Pl = ParsePlayers(d) };
            inf.Format = FormatOf(inf.Pl);
            InfoCache[key] = inf;
            return inf;
        }

        private static List<Pl> Players(BtcReplay.Data d) { return InfoOf(d).Pl; }

        /// <summary>Match format badge: 1v1, 2v2, 3v1, 2v2v2, FFA3, FFA4... (the recording ghost "REC" is not a player). Teams exist only in replays recorded since this feature; older ones are guessed from the player count and end with "?".</summary>
        private static string FormatOf(List<Pl> pl)
        {
            int n = pl.Count;
            if (n == 0) return "";
            if (n == 1) return "Solo";
            bool known = true;
            for (int i = 0; i < n; i++) if (pl[i].Team < 0) known = false;
            if (!known) return n == 2 ? "1v1?" : "FFA" + n + "?";
            Dictionary<int, int> teams = new Dictionary<int, int>();
            for (int i = 0; i < n; i++) { int c; teams.TryGetValue(pl[i].Team, out c); teams[pl[i].Team] = c + 1; }
            if (teams.Count == n) return n == 2 ? "1v1" : "FFA" + n;
            if (teams.Count == 1) return "Co-op" + n;
            List<int> sizes = new List<int>(teams.Values);
            sizes.Sort(delegate (int a, int b) { return b.CompareTo(a); });
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < sizes.Count; i++) { if (i > 0) sb.Append('v'); sb.Append(sizes[i]); }
            return sb.ToString();
        }

        private static List<Pl> ParsePlayers(BtcReplay.Data d)
        {
            List<Pl> r = new List<Pl>();
            try
            {
                if (!string.IsNullOrEmpty(d.Pl2))
                {
                    string[] e = d.Pl2.Split(';');
                    for (int i = 0; i < e.Length; i++)
                    {
                        string[] p = e[i].Split(':');
                        if (p.Length < 3) continue;
                        Color c = Color.white;
                        ColorUtility.TryParseHtmlString("#" + p[1], out c);
                        int team = -1;
                        if (p.Length >= 4) int.TryParse(p[3], out team);
                        string nick = BtcStr.Plain(Encoding.UTF8.GetString(Convert.FromBase64String(p[2])));
                        if (nick == "REC") continue;
                        r.Add(new Pl { Nick = nick, Col = c, Team = team });
                    }
                }
            }
            catch (Exception) { r.Clear(); }
            if (r.Count == 0)
                for (int i = 0; i < d.Players.Count; i++) if (d.Players[i] != "REC") r.Add(new Pl { Nick = d.Players[i], Col = new Color(1f, 1f, 1f, 0.6f), Team = -1 });
            return r;
        }

        private static string DateGroup(BtcReplay.Data d)
        {
            if (d.Ticks <= 0L) return Tt("Unknown date", "Дата неизвестна");
            DateTime t = new DateTime(d.Ticks), now = DateTime.Now;
            if (t.Date == now.Date) return Tt("Today", "Сегодня");
            if (t.Date == now.Date.AddDays(-1)) return Tt("Yesterday", "Вчера");
            if ((now.Date - t.Date).TotalDays < 7) return Tt("This week", "На этой неделе");
            if (t.Year == now.Year && t.Month == now.Month) return Tt("This month", "В этом месяце");
            return t.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        }

        private static void Styles(int fs)
        {
            if (_lbl != null && _styleFs == fs) return;
            _styleFs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _lbl = new GUIStyle(b); _lbl.fontSize = Mathf.RoundToInt(fs * 0.9f); _lbl.normal.textColor = Color.white; _lbl.alignment = TextAnchor.MiddleLeft; _lbl.clipping = TextClipping.Clip;
            _dim = new GUIStyle(_lbl); _dim.fontSize = Mathf.RoundToInt(fs * 0.75f); _dim.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            _ttl = new GUIStyle(_lbl); _ttl.fontSize = Mathf.RoundToInt(fs * 1.0f); _ttl.fontStyle = FontStyle.Bold;
            _badge = new GUIStyle(_lbl); _badge.fontSize = Mathf.RoundToInt(fs * 0.7f); _badge.alignment = TextAnchor.MiddleCenter;
            _tf = new GUIStyle(GUI.skin.textField); _tf.font = b.font; _tf.fontSize = Mathf.RoundToInt(fs * 0.9f); _tf.alignment = TextAnchor.MiddleLeft;
            _tf.normal.background = null; _tf.focused.background = null; _tf.hover.background = null; _tf.active.background = null;
            _tf.normal.textColor = Color.white; _tf.focused.textColor = Color.white; _tf.hover.textColor = Color.white; _tf.padding = new RectOffset(4, 4, 0, 0);
        }

        private static void Txt(Rect r, string s, GUIStyle st, Color c)
        {
            Color old = GUI.contentColor;
            GUI.contentColor = c;
            GUI.Label(r, s, st);
            GUI.contentColor = old;
        }

        private static Texture2D Grad()
        {
            if (_grad != null) return _grad;
            _grad = new Texture2D(1, 32, TextureFormat.RGBA32, false);
            _grad.hideFlags = HideFlags.HideAndDontSave;
            _grad.wrapMode = TextureWrapMode.Clamp;
            for (int i = 0; i < 32; i++) _grad.SetPixel(0, i, new Color(0f, 0f, 0f, Mathf.Pow(1f - i / 31f, 1.6f) * 0.92f));   // row 0 = bottom
            _grad.Apply(false);
            return _grad;
        }

        private static void Shade(Rect r)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Grad());
        }

        private static bool Matches(BtcReplay.Data d)
        {
            if (_mode == 1 && !d.Multiplayer) return false;
            if (_mode == 2 && d.Multiplayer) return false;
            Meta m = M(d);
            if (_filter == 1 && !m.Fav) return false;
            if (_filter >= 2 && (_filter - 2 >= Folders.Count || m.Folder != Folders[_filter - 2])) return false;
            if (_q.Length > 0)
            {
                string q = _q.ToLowerInvariant();
                bool hit = Title(d).ToLowerInvariant().Contains(q) || InfoOf(d).Format.ToLowerInvariant().Contains(q) || MapOf(d).ToLowerInvariant().Contains(q) || d.FileName.ToLowerInvariant().Contains(q);
                for (int i = 0; i < d.Players.Count && !hit; i++) hit = d.Players[i].ToLowerInvariant().Contains(q);
                if (!hit) return false;
            }
            return true;
        }

        private static void AddTo(Dictionary<string, Grp> map, List<Grp> order, string key, string name, int icon, Color col, BtcReplay.Data d)
        {
            Grp g;
            if (!map.TryGetValue(key, out g)) { g = new Grp { Key = key, Name = name, Icon = icon, Col = col }; map[key] = g; order.Add(g); }
            g.Items.Add(d);
        }

        private static void Rebuild()
        {
            string key = _filter + "|" + _sort + "|" + _group + "|" + _mode + "|" + _q + "|" + BtcReplayUI.list.Count + "|" + Folders.Count + "|" + (BtcReplayUI.list.Count > 0 ? BtcReplayUI.list[0].FileName : "") + "|" + Tt("a", "b") + "|" + Seen.Count;
            if (key == _viewKey) return;
            _viewKey = key;
            List<BtcReplay.Data> flat = new List<BtcReplay.Data>();
            for (int i = 0; i < BtcReplayUI.list.Count; i++) if (Matches(BtcReplayUI.list[i])) flat.Add(BtcReplayUI.list[i]);
            Comparison<BtcReplay.Data> cmp;
            switch (_sort)
            {
                case 1: cmp = delegate (BtcReplay.Data a, BtcReplay.Data b) { return a.Ticks.CompareTo(b.Ticks); }; break;
                case 2: cmp = delegate (BtcReplay.Data a, BtcReplay.Data b) { return b.Duration.CompareTo(a.Duration); }; break;
                case 3: cmp = delegate (BtcReplay.Data a, BtcReplay.Data b) { int c = string.Compare(MapOf(a), MapOf(b), StringComparison.OrdinalIgnoreCase); return c != 0 ? c : b.Ticks.CompareTo(a.Ticks); }; break;
                case 4: cmp = delegate (BtcReplay.Data a, BtcReplay.Data b) { int c = b.Players.Count.CompareTo(a.Players.Count); return c != 0 ? c : b.Ticks.CompareTo(a.Ticks); }; break;
                default: cmp = delegate (BtcReplay.Data a, BtcReplay.Data b) { return b.Ticks.CompareTo(a.Ticks); }; break;
            }
            flat.Sort(cmp);
            _shown = flat.Count;
            Groups.Clear();
            Dictionary<string, Grp> map = new Dictionary<string, Grp>();
            List<Grp> order = new List<Grp>();
            for (int i = 0; i < flat.Count; i++)
            {
                BtcReplay.Data d = flat[i];
                switch (_group)
                {
                    case 1: AddTo(map, order, MapOf(d).ToLowerInvariant(), MapOf(d), BtcReplayIcons.Map, Color.clear, d); break;
                    case 2: { string g = DateGroup(d); AddTo(map, order, g, g, BtcReplayIcons.Wave, Color.clear, d); break; }
                    case 3:
                    {
                        List<Pl> pl = Players(d);
                        if (pl.Count == 0) AddTo(map, order, "?", "?", BtcReplayIcons.Cursor, Color.clear, d);
                        for (int k = 0; k < pl.Count; k++) AddTo(map, order, pl[k].Nick.ToLowerInvariant(), pl[k].Nick, BtcReplayIcons.Cursor, pl[k].Col, d);
                        break;
                    }
                    case 4: AddTo(map, order, d.Multiplayer ? "mp" : "sp", d.Multiplayer ? Tt("Multiplayer", "Сетевые") : Tt("Singleplayer", "Одиночные"), d.Multiplayer ? BtcReplayIcons.Camera : BtcReplayIcons.Cursor, Color.clear, d); break;
                    case 5: { string f = M(d).Folder; AddTo(map, order, f, f.Length == 0 ? Tt("No folder", "Без папки") : f, BtcReplayIcons.Folder, Color.clear, d); break; }
                    default: AddTo(map, order, "", "", BtcReplayIcons.Layout, Color.clear, d); break;
                }
            }
            if (_group == 1 || _group == 4) order.Sort(delegate (Grp a, Grp b) { return _group == 4 ? string.Compare(b.Key, a.Key, StringComparison.Ordinal) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
            else if (_group == 3) order.Sort(delegate (Grp a, Grp b) { int c = b.Items.Count.CompareTo(a.Items.Count); return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
            else if (_group == 5) order.Sort(delegate (Grp a, Grp b) { int ia = a.Key.Length == 0 ? 999 : Folders.IndexOf(a.Key), ib = b.Key.Length == 0 ? 999 : Folders.IndexOf(b.Key); return ia.CompareTo(ib); });
            // new replays go to the top of every folder (stable: the chosen sort order is kept inside both parts)
            for (int gi = 0; gi < order.Count; gi++)
            {
                List<BtcReplay.Data> its = order[gi].Items, top = new List<BtcReplay.Data>(), rest = new List<BtcReplay.Data>();
                for (int k = 0; k < its.Count; k++) (IsFresh(its[k]) ? top : rest).Add(its[k]);
                if (top.Count == 0) continue;
                its.Clear(); its.AddRange(top); its.AddRange(rest);
            }
            Groups.AddRange(order);
        }

        // ------------------------------------------------------------------ small widgets
        private static bool Icon(Rect r, int kind, bool on, string tip) { return BtcReplayIcons.Button(r, kind, on, tip); }

        private static bool Chip(Rect r, int icon, string text, bool on)
        {
            Event e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            BtcReplayUI.Fill(r, on ? new Color(Accent.r, Accent.g, Accent.b, 0.16f) : new Color(1f, 1f, 1f, hover ? 0.12f : 0.05f));
            Color c = on ? Accent : new Color(1f, 1f, 1f, hover ? 0.95f : 0.65f);
            float g = r.height * 0.6f;
            BtcReplayIcons.Glyph(icon, new Rect(r.x + r.height * 0.25f, r.center.y - g * 0.5f, g, g), c);
            Txt(new Rect(r.x + r.height * 0.95f, r.y, r.width - r.height, r.height), text, _lbl, c);
            return BtcSfx.Btn(r, "", BtcReplayUI.sBtn);
        }

        private static float ChipW(string text, int fs) { return fs * (1.7f + text.Length * 0.6f); }

        private static void PlayersLine(Rect a, List<Pl> pl, int fs, bool nicks)
        {
            float x = a.x, sq = fs * 0.7f, cy = a.center.y;
            for (int i = 0; i < pl.Count; i++)
            {
                string nk = nicks ? Clip(pl[i].Nick, 7) : "";
                float need = sq + 3f + (nicks ? nk.Length * fs * 0.5f + fs * 0.5f : 2f);
                if (x + need > a.xMax)
                {
                    Txt(new Rect(x, a.y, fs * 2.5f, a.height), "+" + (pl.Count - i), _dim, new Color(1f, 1f, 1f, 0.6f));
                    return;
                }
                BtcReplayUI.Fill(new Rect(x, cy - sq * 0.5f, sq, sq), pl[i].Col);
                x += sq + 3f;
                if (nicks) { Txt(new Rect(x, a.y, nk.Length * fs * 0.5f + fs * 0.4f, a.height), nk, _dim, new Color(1f, 1f, 1f, 0.8f)); x += nk.Length * fs * 0.5f + fs * 0.5f; }
                else x += 2f;
            }
        }

        private static void Thumb(Rect t, BtcReplay.Data d, int fs)
        {
            BtcReplayUI.Fill(t, new Color(0.06f, 0.08f, 0.1f, 1f));
            Texture2D tex = BtcReplayPreview.Get(d);
            if (tex != null) { if (Event.current.type == EventType.Repaint) GUI.DrawTexture(t, tex, ScaleMode.ScaleAndCrop, false); }
            else BtcReplayIcons.Glyph(BtcReplayIcons.Map, new Rect(t.center.x - fs * 0.8f, t.center.y - fs * 0.8f, fs * 1.6f, fs * 1.6f), new Color(1f, 1f, 1f, 0.12f));
            if (d.Error != null || !d.Complete || d.SnapBytes <= 0) BtcReplayUI.Fill(t, new Color(0.35f, 0f, 0f, 0.35f));
        }

        // ------------------------------------------------------------------ main draw
        internal static float Draw(Event e, float y, float w, float rowH, int fs)
        {
            LoadMeta();
            Styles(fs);
            AutoRefresh();
            Rebuild();
            if (_undoSnap == null) _undoSnap = SerializeUndo();
            if (_metaDirty && Time.unscaledTime >= _metaSaveAt) SaveMeta();
            if (e.type == EventType.KeyDown && e.keyCode == BtcMenu.MenuKeyMain)
            {
                // the search field eats the keys: PgDown must still close the menu
                GUIUtility.keyboardControl = 0;
                BtcMenu.PgDownFrame = Time.frameCount;
                ResourcePackManager.CloseMenu();
                e.Use();
                return y;
            }
            BtcUi.ClickAway(e);
            float pad = fs * 1.0f, ib = fs * 1.9f, y0 = y;
            bool filtering = _q.Length > 0;

            // ---- toolbar: title left, icon groups right
            Txt(new Rect(pad, y, w * 0.35f, ib + fs * 0.4f), Tt("Replays", "Реплеи") + "  " + _shown + (_shown != BtcReplayUI.list.Count ? "/" + BtcReplayUI.list.Count : ""), _ttl, Color.white);
            float x = w - pad - ib;
            float ty = y + fs * 0.2f;
            if (Icon(new Rect(x, ty, ib, ib), BtcReplayIcons.Gear, SettingsOpen, Tt("Recording and playback settings", "Настройки записи и просмотра"))) SettingsOpen = !SettingsOpen;
            x -= ib + 3f;
            if (Icon(new Rect(x, ty, ib, ib), BtcReplayIcons.Folder, false, Tt("Open the replays folder", "Открыть папку с реплеями"))) BtcReplayUI.OpenFolder();
            x -= ib + 3f;
            if (Icon(new Rect(x, ty, ib, ib), BtcReplayIcons.Refresh, false, Tt("Refresh the list", "Обновить список"))) { BtcReplayUI.Refresh(); BtcReplayPreview.Reset(); _viewKey = ""; }
            bool anyFresh = false;
            for (int i = 0; i < BtcReplayUI.list.Count && !anyFresh; i++) anyFresh = IsFresh(BtcReplayUI.list[i]);
            if (anyFresh)
            {
                x -= ib + 3f;
                if (BtcReplayIcons.Plain(new Rect(x, ty, ib, ib), BtcReplayIcons.Check, Blue, Tt("Mark all new replays as seen", "Отметить все новые просмотренными"))) { for (int i = 0; i < BtcReplayUI.list.Count; i++) Seen.Add(BtcReplayUI.list[i].FileName); _viewKey = ""; }
            }
            x -= ib * 1.6f;
            // view modes (segmented)
            int[] vi = { BtcReplayIcons.Grid, BtcReplayIcons.List, BtcReplayIcons.Lines };
            string[] vt = { Tt("View: big cards", "Вид: крупные карточки"), Tt("View: list", "Вид: список"), Tt("View: compact lines", "Вид: компактные строки") };
            for (int i = 2; i >= 0; i--)
            {
                if (Icon(new Rect(x, ty, ib, ib), vi[i], _viewMode == i, vt[i])) { _viewMode = i; Dirty(false); }
                x -= ib + 2f;
            }
            x -= ib * 0.6f;
            string[] sorts = { Tt("Sort: newest first", "Сортировка: сначала новые"), Tt("Sort: oldest first", "Сортировка: сначала старые"), Tt("Sort: longest first", "Сортировка: по длительности"), Tt("Sort: by map", "Сортировка: по карте"), Tt("Sort: by players", "Сортировка: по числу игроков") };
            if (Icon(new Rect(x, ty, ib, ib), BtcReplayIcons.Sort, _sort != 0, sorts[_sort])) { _sort = (_sort + 1) % sorts.Length; Dirty(true); }
            x -= ib + 3f;
            string[] groups = { Tt("Group: none", "Группы: нет"), Tt("Group: by map", "Группы: по картам"), Tt("Group: by date", "Группы: по датам"), Tt("Group: by player", "Группы: по игрокам"), Tt("Group: by mode", "Группы: по режиму"), Tt("Group: by my folders", "Группы: по моим папкам") };
            if (Icon(new Rect(x, ty, ib, ib), BtcReplayIcons.Group, _group != 0, groups[_group])) { _group = (_group + 1) % groups.Length; Dirty(true); }
            float toolbarBottom = y0 + ib + fs * 0.6f;
            y = toolbarBottom;

            // ---- search + mode + expand/collapse
            Rect sr = new Rect(pad, y, Mathf.Min(w * 0.42f, fs * 22f), ib);
            BtcReplayUI.Fill(sr, new Color(1f, 1f, 1f, 0.06f));
            BtcReplayIcons.Glyph(BtcReplayIcons.Search, new Rect(sr.x + fs * 0.4f, sr.y + ib * 0.2f, ib * 0.6f, ib * 0.6f), new Color(1f, 1f, 1f, 0.5f));
            BtcUi.Field(sr);
            string nq = GUI.TextField(new Rect(sr.x + ib * 0.95f, sr.y, sr.width - ib * 0.95f, ib), _q, 40, _tf);
            if (nq != _q) { _q = nq; _viewKey = ""; }
            if (_q.Length == 0 && GUIUtility.keyboardControl == 0) Txt(new Rect(sr.x + ib * 0.95f + 4f, sr.y, sr.width, ib), Tt("Search: map, player, name", "Поиск: карта, игрок, название"), _dim, new Color(1f, 1f, 1f, 0.35f));
            x = sr.xMax + fs * 0.6f;
            string[] mt = { Tt("Mode: all games", "Режим: все игры"), Tt("Mode: multiplayer only", "Режим: только сетевые"), Tt("Mode: singleplayer only", "Режим: только одиночные") };
            int[] mi = { BtcReplayIcons.Layout, BtcReplayIcons.Camera, BtcReplayIcons.Cursor };
            for (int i = 0; i < 3; i++)
            {
                if (Icon(new Rect(x, y, ib, ib), mi[i], _mode == i, mt[i])) { _mode = i; _viewKey = ""; }
                x += ib + 3f;
            }
            if (_group != 0)
            {
                x += fs * 0.6f;
                if (Icon(new Rect(x, y, ib, ib), BtcReplayIcons.ChevD, false, Tt("Expand all groups", "Развернуть все группы"))) { for (int i = 0; i < Groups.Count; i++) Open.Add(_group + ":" + Groups[i].Key); Dirty(false); }
                x += ib + 3f;
                if (Icon(new Rect(x, y, ib, ib), BtcReplayIcons.ChevR, false, Tt("Collapse all groups", "Свернуть все группы"))) { Open.Clear(); Dirty(false); }
            }
            y += ib + fs * 0.5f;

            // ---- filter chips: all / favourites / own folders / new folder
            float cx = pad, ch = fs * 1.7f;
            string[] fn = { Tt("All", "Все"), Tt("Favourites", "Избранное") };
            int[] fic = { BtcReplayIcons.Layout, BtcReplayIcons.Star };
            for (int i = 0; i < 2; i++)
            {
                float cw = ChipW(fn[i], fs);
                if (Chip(new Rect(cx, y, cw, ch), fic[i], fn[i], _filter == i)) { _filter = i; _viewKey = ""; }
                cx += cw + 3f;
            }
            for (int i = 0; i < Folders.Count; i++)
            {
                string nm = Clip(Folders[i], 16);
                float cw = ChipW(nm, fs);
                if (cx + cw > w - pad) { cx = pad; y += ch + 3f; }
                bool on = _filter == i + 2;
                if (Chip(new Rect(cx, y, cw, ch), BtcReplayIcons.Folder, nm, on)) { _filter = on ? 0 : i + 2; _viewKey = ""; }
                cx += cw + 3f;
            }
            if (_addingFolder)
            {
                float tw = fs * 11f;
                if (cx + tw + ch * 2.5f > w - pad) { cx = pad; y += ch + 3f; }
                Rect fr = new Rect(cx, y, tw, ch);
                BtcReplayUI.Fill(fr, new Color(1f, 1f, 1f, 0.08f));
                BtcUi.Field(fr);
                _newFolder = GUI.TextField(fr, _newFolder, 20, _tf);
                cx += tw + 3f;
                if (BtcReplayIcons.Plain(new Rect(cx, y, ch, ch), BtcReplayIcons.Check, BtcReplayUI.Green, Tt("Create the folder", "Создать папку")) && _newFolder.Trim().Length > 0)
                {
                    string nf = _newFolder.Trim().Replace('\t', ' ');
                    if (!Folders.Contains(nf)) Folders.Add(nf);
                    _filter = Folders.IndexOf(nf) + 2; _newFolder = ""; _addingFolder = false; Dirty(true);
                }
                cx += ch + 3f;
                if (BtcReplayIcons.Plain(new Rect(cx, y, ch, ch), BtcReplayIcons.Cross, BtcReplayUI.Red, Tt("Cancel", "Отмена"))) { _addingFolder = false; _newFolder = ""; }
            }
            else if (Icon(new Rect(cx, y, ch, ch), BtcReplayIcons.Folder, false, Tt("New folder", "Новая папка"))) _addingFolder = true;
            y += ch + fs * 0.7f;

            int total = BtcReplayUI.list.Count;
            if (total == 0) { Txt(new Rect(pad, y, w - pad * 2f, rowH), Tt("No replays yet. Start a match and the replay appears here when it ends.", "Пока нет реплеев. Начните матч, реплей появится здесь, когда он закончится."), _dim, new Color(1f, 1f, 1f, 0.6f)); y += rowH; BtcReplayIcons.DrawTip(fs, w, BtcReplayIcons.TipAnchor.y < toolbarBottom); return y; }
            if (_shown == 0) { Txt(new Rect(pad, y, w - pad * 2f, rowH), Tt("Nothing matches the filters.", "По фильтрам ничего не найдено."), _dim, new Color(1f, 1f, 1f, 0.6f)); y += rowH; BtcReplayIcons.DrawTip(fs, w, BtcReplayIcons.TipAnchor.y < toolbarBottom); return y; }

            // ---- groups (collapsed by default)
            for (int gi = 0; gi < Groups.Count; gi++)
            {
                Grp g = Groups[gi];
                bool open = _group == 0 || filtering || (HasFresh(g) && !ClosedFresh.Contains(_group + ":" + g.Key)) || Open.Contains(_group + ":" + g.Key);
                if (_group != 0)
                {
                    Rect hr = new Rect(pad, y, w - pad * 2f, fs * 2.1f);
                    bool hover = hr.Contains(e.mousePosition);
                    BtcReplayUI.Fill(hr, new Color(1f, 1f, 1f, hover ? 0.09f : 0.05f));
                    bool gf = HasFresh(g);
                    if (gf) BtcReplayUI.Fill(new Rect(hr.x, hr.y, 3f, hr.height), Blue); else if (open) BtcReplayUI.Fill(new Rect(hr.x, hr.y, 3f, hr.height), Accent);
                    if (gf) NewPill(new Rect(hr.xMax - fs * 9f, hr.center.y - fs * 0.6f, fs * 3.4f, fs * 1.2f), null, g);
                    float gs = fs * 1.1f;
                    BtcReplayIcons.Glyph(open ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, new Rect(hr.x + fs * 0.5f, hr.center.y - gs * 0.5f, gs, gs), new Color(1f, 1f, 1f, 0.7f));
                    if (g.Col.a > 0f) BtcReplayUI.Fill(new Rect(hr.x + fs * 2.2f, hr.center.y - fs * 0.4f, fs * 0.8f, fs * 0.8f), g.Col);
                    else BtcReplayIcons.Glyph(g.Icon, new Rect(hr.x + fs * 2.1f, hr.center.y - gs * 0.5f, gs, gs), open ? Accent : new Color(1f, 1f, 1f, 0.7f));
                    Txt(new Rect(hr.x + fs * 3.8f, hr.y, hr.width - fs * 8f, hr.height), g.Name, _ttl, Color.white);
                    Txt(new Rect(hr.xMax - fs * 4f, hr.y, fs * 3.6f, hr.height), g.Items.Count.ToString(), _dim, new Color(1f, 1f, 1f, 0.6f));
                    if (BtcSfx.Btn(hr, "", BtcReplayUI.sBtn))
                    {
                        string k = _group + ":" + g.Key;
                        if (open) { Open.Remove(k); if (HasFresh(g)) ClosedFresh.Add(k); }
                        else { ClosedFresh.Remove(k); Open.Add(k); }
                        Dirty(false);
                    }
                    y += hr.height + 3f;
                }
                if (!open) continue;
                y = DrawItems(e, g.Items, y, w, fs, pad);
                y += fs * 0.5f;
            }
            BtcReplayIcons.DrawTip(fs, w, BtcReplayIcons.TipAnchor.y < toolbarBottom);
            return y + rowH * 0.5f;
        }

        private static float DrawItems(Event e, List<BtcReplay.Data> items, float y, float w, int fs, float pad)
        {
            float gap = fs * 0.5f;
            int cols = 1;
            float cardW = w - pad * 2f, itemH;
            if (_viewMode == 0)
            {
                cols = Mathf.Max(1, (int)((w - pad * 2f + gap) / (fs * 16f + gap)));
                cardW = (w - pad * 2f - gap * (cols - 1)) / cols;
                itemH = cardW * 0.6f + fs * 2.4f;
            }
            else itemH = _viewMode == 1 ? fs * 4.6f : fs * 2.0f;
            for (int i = 0; i < items.Count; i += cols)
            {
                bool sel = false;
                for (int k = i; k < i + cols && k < items.Count; k++)
                {
                    BtcReplay.Data d = items[k];
                    Rect r = new Rect(pad + (k - i) * (cardW + gap), y, cardW, itemH);
                    if (_viewMode == 0) DrawCard(e, r, d, fs);
                    else if (_viewMode == 1) DrawRow(e, r, d, fs);
                    else DrawLine(e, r, d, fs);
                    if (d.FileName == _selected) sel = true;
                }
                y += itemH + (_viewMode == 2 ? 2f : gap);
                if (sel) y = DrawDetails(e, y, w, fs, pad);
            }
            return y;
        }

        private static string Dur(BtcReplay.Data d) { return d.Error != null ? "!" : (d.Complete ? BtcReplayUI.Clock(d.Duration) : Tt("incomplete", "не завершён")); }

        private static void Click(Event e, Rect r, BtcReplay.Data d)
        {
            if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn))
            {
                if (e.clickCount >= 2 && d.Error == null && d.Complete && d.SnapBytes > 0) { Seen.Add(d.FileName); BtcReplay.StartReplay(d); ResourcePackManager.CloseMenu(); return; }
                _selected = _selected == d.FileName ? null : d.FileName;
                _confirmDelete = null;
            }
        }

        private static bool Star(Rect r, BtcReplay.Data d, bool show)
        {
            Meta m = M(d);
            if (!show && !m.Fav) return false;
            if (BtcReplayIcons.Plain(r, BtcReplayIcons.Star, m.Fav ? new Color(1f, 0.85f, 0.25f, 1f) : new Color(1f, 1f, 1f, 0.75f), m.Fav ? Tt("Remove from favourites", "Убрать из избранного") : Tt("Add to favourites", "В избранное")))
            {
                m.Fav = !m.Fav; Dirty(_filter == 1);
                return true;
            }
            return false;
        }

        // big card: full-bleed map, gradient, title on it, badges, nicks under it
        private static void DrawCard(Event e, Rect r, BtcReplay.Data d, int fs)
        {
            bool sel = _selected == d.FileName, hover = r.Contains(e.mousePosition);
            float thumbH = r.height - fs * 2.4f;
            Rect t = new Rect(r.x, r.y, r.width, thumbH);
            Thumb(t, d, fs);
            Shade(new Rect(t.x, t.y + thumbH * 0.45f, t.width, thumbH * 0.55f));
            if (hover && !sel) BtcReplayUI.Fill(t, new Color(1f, 1f, 1f, 0.05f));
            Txt(new Rect(t.x + fs * 0.6f, t.yMax - fs * 2.5f, t.width - fs * 5f, fs * 1.5f), Clip(Title(d), (int)(t.width / (fs * 0.55f))), _ttl, Color.white);
            Txt(new Rect(t.x + fs * 0.6f, t.yMax - fs * 1.2f, t.width - fs * 1.2f, fs * 1.1f), (d.Ticks > 0L ? new DateTime(d.Ticks).ToString("dd.MM.yy HH:mm") : d.FileName) + "  -  " + BtcReplayUI.Size(d.FileBytes), _dim, new Color(1f, 1f, 1f, 0.75f));
            Rect mb = new Rect(t.x + fs * 0.4f, t.y + fs * 0.4f, fs * 1.9f, fs * 1.1f);
            BtcReplayUI.Fill(mb, new Color(0f, 0f, 0f, 0.6f)); Txt(mb, d.Multiplayer ? "MP" : "SP", _badge, Color.white);
            float bx = FmtBadge(mb, d, fs); if (IsFresh(d)) NewPill(new Rect(bx, mb.y, fs * 3.4f, mb.height), d, null);
            string dur = Dur(d);
            float dw = fs * (0.9f + dur.Length * 0.5f);
            Rect db = new Rect(t.xMax - dw - fs * 0.4f, t.yMax - fs * 1.7f, dw, fs * 1.2f);
            BtcReplayUI.Fill(db, new Color(0f, 0f, 0f, 0.6f)); Txt(db, dur, _badge, Color.white);
            bool kf = BtcReplaySeek.HasCacheFor(d.FilePath) && d.Complete;
            if (kf) BtcReplayIcons.Glyph(BtcReplayIcons.Wave, new Rect(t.xMax - fs * 4.6f, t.yMax - fs * 1.7f, fs * 1.2f, fs * 1.2f), new Color(Accent.r, Accent.g, Accent.b, 0.9f));
            if (sel) BtcReplayUI.Fill(new Rect(r.x, r.yMax - 3f, r.width, 3f), Accent);
            if (IsFresh(d)) BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, r.height), Blue);
            BtcReplayUI.Fill(new Rect(r.x, t.yMax, r.width, r.height - thumbH), new Color(1f, 1f, 1f, sel ? 0.1f : 0.045f));
            PlayersLine(new Rect(r.x + fs * 0.5f, t.yMax, r.width - fs, r.height - thumbH), Players(d), fs, true);
            float sb = fs * 1.8f;
            Star(new Rect(t.xMax - sb - fs * 0.3f, t.y + fs * 0.3f, sb, sb), d, hover);
            Click(e, r, d);
        }

        // list row: map on the left, text on the right
        private static void DrawRow(Event e, Rect r, BtcReplay.Data d, int fs)
        {
            bool sel = _selected == d.FileName, hover = r.Contains(e.mousePosition);
            BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, sel ? 0.1f : (hover ? 0.07f : 0.04f)));
            Rect t = new Rect(r.x, r.y, r.height * 1.7f, r.height);
            Thumb(t, d, fs);
            Rect mb = new Rect(t.x + fs * 0.3f, t.y + fs * 0.3f, fs * 1.9f, fs * 1.1f);
            BtcReplayUI.Fill(mb, new Color(0f, 0f, 0f, 0.6f)); Txt(mb, d.Multiplayer ? "MP" : "SP", _badge, Color.white);
            float bx = FmtBadge(mb, d, fs); if (IsFresh(d)) NewPill(new Rect(bx, mb.y, fs * 3.4f, mb.height), d, null);
            float x0 = t.xMax + fs * 0.8f, w = r.xMax - x0 - fs * 0.5f;
            Txt(new Rect(x0, r.y + fs * 0.25f, w - fs * 8f, fs * 1.4f), Title(d), _ttl, Color.white);
            Txt(new Rect(x0, r.y + fs * 1.65f, w, fs * 1.1f), (d.Ticks > 0L ? new DateTime(d.Ticks).ToString("dd.MM.yyyy HH:mm") : d.FileName) + "   " + BtcReplayUI.Size(d.FileBytes) + (d.Mode != null && d.Mode.Length > 0 ? "   " + d.Mode : ""), _dim, new Color(1f, 1f, 1f, 0.6f));
            PlayersLine(new Rect(x0, r.y + fs * 2.9f, w - fs * 6f, fs * 1.4f), Players(d), fs, true);
            string dur = Dur(d);
            Txt(new Rect(r.xMax - fs * 7f, r.y + fs * 0.25f, fs * 4.6f, fs * 1.4f), dur, _ttl, new Color(1f, 1f, 1f, 0.9f));
            if (BtcReplaySeek.HasCacheFor(d.FilePath) && d.Complete) BtcReplayIcons.Glyph(BtcReplayIcons.Wave, new Rect(r.xMax - fs * 7f, r.yMax - fs * 1.7f, fs * 1.2f, fs * 1.2f), new Color(Accent.r, Accent.g, Accent.b, 0.9f));
            float sb = fs * 1.8f;
            Star(new Rect(r.xMax - sb - fs * 0.4f, r.y + fs * 0.3f, sb, sb), d, hover);
            if (sel) BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, r.height), Accent);
            if (IsFresh(d)) BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, r.height), Blue);
            Click(e, r, d);
        }

        // compact line: everything in one row
        private static void DrawLine(Event e, Rect r, BtcReplay.Data d, int fs)
        {
            bool sel = _selected == d.FileName, hover = r.Contains(e.mousePosition);
            BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, sel ? 0.1f : (hover ? 0.07f : 0.035f)));
            float x = r.x + fs * 0.3f, h = r.height;
            Rect sr = new Rect(x, r.y + h * 0.1f, h * 0.8f, h * 0.8f);
            Star(sr, d, hover);
            x += h;
            if (IsFresh(d)) { NewPill(new Rect(x, r.y + h * 0.15f, fs * 3.4f, h * 0.7f), d, null); x += fs * 3.6f; }
            Txt(new Rect(x, r.y, fs * 2.2f, h), d.Multiplayer ? "MP" : "SP", _dim, new Color(1f, 1f, 1f, 0.55f));
            x += fs * 2.4f;
            Txt(new Rect(x, r.y, fs * 4.2f, h), InfoOf(d).Format, _lbl, new Color(1f, 0.92f, 0.6f, 1f));
            x += fs * 4.4f;
            float col1 = Mathf.Min(r.width * 0.26f, fs * 14f);
            Txt(new Rect(x, r.y, col1, h), Title(d), _lbl, Color.white);
            x += col1 + fs * 0.4f;
            Txt(new Rect(x, r.y, fs * 8.5f, h), d.Ticks > 0L ? new DateTime(d.Ticks).ToString("dd.MM.yy HH:mm") : d.FileName, _dim, new Color(1f, 1f, 1f, 0.6f));
            x += fs * 8.8f;
            Txt(new Rect(x, r.y, fs * 5f, h), Dur(d), _lbl, new Color(1f, 1f, 1f, 0.9f));
            x += fs * 5.2f;
            float endX = r.xMax - fs * 2.2f;
            PlayersLine(new Rect(x, r.y, Mathf.Max(fs * 4f, endX - x), h), Players(d), fs, true);
            if (BtcReplaySeek.HasCacheFor(d.FilePath) && d.Complete) BtcReplayIcons.Glyph(BtcReplayIcons.Wave, new Rect(r.xMax - fs * 1.8f, r.center.y - fs * 0.5f, fs, fs), new Color(Accent.r, Accent.g, Accent.b, 0.9f));
            if (sel) BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, r.height), Accent);
            if (IsFresh(d)) BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, r.height), Blue);
            Click(e, r, d);
        }

        // ------------------------------------------------------------------ details panel
        private static float DrawDetails(Event e, float y, float w, int fs, float pad)
        {
            BtcReplay.Data d = null;
            foreach (BtcReplay.Data x in BtcReplayUI.list) if (x.FileName == _selected) { d = x; break; }
            if (d == null) return y;
            Meta m = M(d);
            List<Pl> pl = Players(d);
            float rowH = fs * 1.7f;
            int plRows = Mathf.Max(1, (pl.Count + 1) / 2);
            float h = Mathf.Max(fs * 15f, fs * 7.5f + rowH * plRows + fs * 3.2f);
            Rect p = new Rect(pad, y, w - pad * 2f, h);
            BtcReplayUI.Fill(p, new Color(1f, 1f, 1f, 0.05f));
            BtcReplayUI.Fill(new Rect(p.x, p.y, p.width, 2f), Accent);
            bool ok = d.Error == null && d.Complete && d.SnapBytes > 0;
            // big map on the left, full height, no frame
            float pw = Mathf.Min(p.width * 0.34f, fs * 26f);
            Rect pr = new Rect(p.x, p.y + 2f, pw, p.height - 2f);
            Thumb(pr, d, fs);
            Shade(new Rect(pr.x, pr.y + pr.height * 0.6f, pr.width, pr.height * 0.4f));
            Txt(new Rect(pr.x + fs * 0.7f, pr.yMax - fs * 2.6f, pr.width - fs * 1.4f, fs * 1.6f), MapOf(d), _ttl, Color.white);
            Txt(new Rect(pr.x + fs * 0.7f, pr.yMax - fs * 1.3f, pr.width - fs * 1.4f, fs * 1.1f), (d.Multiplayer ? "MP" : "SP") + "  -  " + InfoOf(d).Format + (string.IsNullOrEmpty(d.Mode) ? "" : "  -  " + d.Mode) + "  -  " + Dur(d), _dim, new Color(1f, 1f, 1f, 0.8f));
            float x0 = pr.xMax + fs * 1.2f, rx = p.xMax - fs, y0 = p.y + fs * 0.8f;
            // name
            Txt(new Rect(x0, y0, fs * 5f, rowH), Tt("Name", "Название"), _dim, new Color(1f, 1f, 1f, 0.55f));
            Rect nr = new Rect(x0 + fs * 5f, y0 + 2f, Mathf.Min(fs * 22f, rx - x0 - fs * 5f), rowH - 4f);
            BtcReplayUI.Fill(nr, new Color(1f, 1f, 1f, 0.07f));
            BtcUi.Field(nr);
            string nn = GUI.TextField(nr, m.Name, 40, _tf);
            if (nn != m.Name) { m.Name = nn; Dirty(false); }
            if (m.Name.Length == 0 && GUIUtility.keyboardControl == 0) Txt(new Rect(nr.x + 4f, nr.y, nr.width, nr.height), Title(d), _dim, new Color(1f, 1f, 1f, 0.35f));
            y0 += rowH * 1.2f;
            string[] info =
            {
                Tt("Date  ", "Дата  ") + (d.Ticks > 0L ? new DateTime(d.Ticks).ToString("yyyy-MM-dd HH:mm") : "?"),
                Tt("File  ", "Файл  ") + BtcReplayUI.Size(d.FileBytes) + "   " + d.FileName,
                Tt("Keyframes  ", "Ключ. кадры  ") + (BtcReplaySeek.HasCacheFor(d.FilePath) ? Tt("ready, instant rewind", "готовы, мгновенная перемотка") : Tt("placed on the first open", "расставятся при первом открытии")) + "   (" + d.Keyframes + " " + Tt("recorded", "записано") + ")"
            };
            for (int i = 0; i < info.Length; i++) { Txt(new Rect(x0, y0, rx - x0, rowH), info[i], _dim, new Color(1f, 1f, 1f, 0.7f)); y0 += rowH * 0.85f; }
            y0 += rowH * 0.3f;
            Txt(new Rect(x0, y0, fs * 8f, rowH), Tt("Players", "Игроки") + "  " + pl.Count, _ttl, Color.white);
            y0 += rowH * 1.1f;
            float colW = Mathf.Max(fs * 10f, (rx - x0) / 2f);
            for (int i = 0; i < pl.Count; i++)
            {
                float px = x0 + (i % 2) * colW, py = y0 + (i / 2) * rowH;
                BtcReplayUI.Fill(new Rect(px, py + rowH * 0.2f, fs * 0.9f, fs * 0.9f), pl[i].Col);
                Txt(new Rect(px + fs * 1.3f, py, colW - fs * 1.5f, rowH), Clip(pl[i].Nick, (int)(colW / (fs * 0.55f))), _lbl, Color.white);
            }
            // actions along the bottom
            float ab = fs * 2.2f, ay = p.yMax - ab - fs * 0.6f, ax = x0;
            if (BtcReplayIcons.Plain(new Rect(ax, ay, ab * 1.8f, ab), BtcReplayIcons.Play, ok ? BtcReplayUI.Green : new Color(1f, 1f, 1f, 0.3f), ok ? Tt("Play the replay (double click)", "Смотреть реплей (двойной клик)") : Tt("No world snapshot in this replay", "В этом реплее нет снимка мира")))
            {
                if (ok) { Seen.Add(d.FileName); BtcReplay.StartReplay(d); ResourcePackManager.CloseMenu(); }
                else BtcReplay.Say(d.Complete ? Tt("This replay has no world snapshot (recorded by an older version).", "В этом реплее нет снимка мира (записан старой версией).") : Tt("This replay is incomplete.", "Реплей не завершён."));
            }
            ax += ab * 1.8f + 5f;
            if (IsFresh(d))
            {
                if (BtcReplayIcons.Plain(new Rect(ax, ay, ab, ab), BtcReplayIcons.Check, Blue, Tt("Mark as seen", "Отметить просмотренным"))) { Seen.Add(d.FileName); _viewKey = ""; }
                ax += ab + 5f;
            }
            if (Icon(new Rect(ax, ay, ab, ab), BtcReplayIcons.Star, m.Fav, m.Fav ? Tt("Remove from favourites", "Убрать из избранного") : Tt("Add to favourites", "В избранное"))) { m.Fav = !m.Fav; Dirty(_filter == 1); }
            ax += ab + 5f;
            string folderTip = Tt("Folder: ", "Папка: ") + (m.Folder.Length == 0 ? Tt("none", "нет") : m.Folder) + Tt("  (click: next folder)", "  (клик: следующая папка)");
            if (Icon(new Rect(ax, ay, ab, ab), BtcReplayIcons.Folder, m.Folder.Length > 0, folderTip))
            {
                int fi = Folders.IndexOf(m.Folder);
                m.Folder = fi + 1 < Folders.Count ? Folders[fi + 1] : "";
                Dirty(true);
            }
            ax += ab + 5f;
            bool copied = BtcReplayShare.FileCopied(d.FilePath);
            if (d.Error == null && Icon(new Rect(ax, ay, ab, ab), BtcReplayIcons.Copy, copied, BtcReplayShare.UiCopyFile(copied))) BtcReplayShare.CopyFile(d.FilePath);
            ax += ab + 5f;
            int st = (d.Error == null && d.Complete) ? BtcReplayShare.State(d.FilePath) : 0;
            string shareTip = st == 1 ? BtcReplayShare.UiUploading() : (st == 2 ? BtcReplayShare.UiCopy(BtcReplayShare.Copied(d.FilePath)) : (st == 3 ? BtcReplayShare.UiRetry() : BtcReplayShare.UiCreate()));
            if (d.Error == null && d.Complete && Icon(new Rect(ax, ay, ab, ab), BtcReplayIcons.Share, st == 2, shareTip))
            {
                if (st == 2) BtcReplayShare.Copy(d.FilePath);
                else if (st != 1) BtcReplayShare.Create(d.FilePath);
            }
            ax += ab + 8f;
            if (st == 3) Txt(new Rect(ax, ay, rx - ax - ab * 2f, ab), BtcReplayShare.UiFailed(BtcReplayShare.Error(d.FilePath)), _dim, new Color(1f, 0.5f, 0.45f, 1f));
            else if (st == 2) Txt(new Rect(ax, ay, rx - ax - ab * 2f, ab), BtcReplayShare.Url(d.FilePath) + (BtcReplayShare.IsTemp(d.FilePath) ? BtcReplayShare.UiTempNote() : string.Empty), _dim, new Color(1f, 1f, 1f, 0.6f));
            bool sure = _confirmDelete == d.FileName && Time.unscaledTime < _confirmUntil;
            Rect dr = new Rect(rx - ab, ay, ab, ab);
            if (sure) Txt(new Rect(dr.x - fs * 5f, ay, fs * 4.8f, ab), Tt("Sure?", "Точно?"), _lbl, BtcReplayUI.Red);
            if (BtcReplayIcons.Plain(dr, BtcReplayIcons.Trash, BtcReplayUI.Red, sure ? Tt("Click again to delete", "Нажмите ещё раз для удаления") : Tt("Delete the replay", "Удалить реплей")))
            {
                if (sure)
                {
                    string delTitle = Title(d);
                    BtcUndo.Group("replay", Tt("Replay deleted: ", "Реплей удалён: ") + delTitle, delegate
                    {
                        if (SoftDelete(d)) { BtcSfx.Delete(); _meta.Remove(d.FileName); Dirty(true); _selected = null; _confirmDelete = null; BtcReplayUI.Refresh(); }
                    });
                }
                else { _confirmDelete = d.FileName; _confirmUntil = Time.unscaledTime + 3f; }
            }
            return y + h + fs * 0.5f;
        }
    }
}
