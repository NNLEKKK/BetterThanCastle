using System;
using System.Collections.Generic;
using System.Text;
using Brix.Game;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Statistics tab (menu tab 11, in the rail after Gameplay, active only in a game or replay). Pages: Table (players x stats with search,
    /// sections, favourites, profiles, compare modes and per-item detail rows), Graph (any stat over time, hover values) and Display
    /// (what the on-screen bar and the F7 panel show, scale, opacity). The F7 panel and the bar stay as they were; this tab reads the same
    /// data (BtcMatchStats) through BtcMatchStatsUI. Everything is remembered via BtcMenuState. Undo scope "stats".
    /// </summary>
    internal sealed class BtcStatsTab : BtcMenuTab
    {
        internal static readonly BtcStatsTab Instance = new BtcStatsTab();

        internal override int Id { get { return 11; } }
        internal override bool IsActive { get { return BtcMenu.CurrentId == 11; } }
        internal override string UndoScope { get { return Scope; } }

        internal const string Scope = "stats";

        internal static bool MasterOn { get { return BtcConfig.StatsEnabled == null || BtcConfig.StatsEnabled.Value; } }

        /// <summary>The rail enables the tab only in a game (or a replay), not in the lobby or the main menu.</summary>
        internal static bool Available
        {
            get
            {
                if (BtcMatchStats.Active) return true;
                try { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex == 2 && !BtcGameplay.InLobby; }
                catch (Exception) { return false; }
            }
        }

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static readonly Color PerfectBlue = new Color(0.25f, 0.6f, 1f, 1f);

        // ------------------------------------------------------------------ state
        private Vector2 _scroll, _listScroll;
        private float _h = 100f;
        private string _newName = "";
        private bool _naming;
        private GUIStyle _ttl, _lbl, _bold, _dim, _right, _tf, _chip;
        private int _fs;
        private float _next;
        private string _q;

        private static int Page { get { return BtcMenuState.Int("stats:page", 0); } set { BtcMenuState.SetInt("stats:page", value); } }

        private string Q
        {
            get { if (_q == null) _q = BtcMenuState.Get("stats:q", ""); return _q; }
            set { _q = value; BtcMenuState.Set("stats:q", value); }
        }

        // sections of the table
        private static readonly string[] SecKey = { "army", "eco", "res", "act" };
        private static string[] SecName() { return new[] { Tt("Army", "Армия"), Tt("Economy and building", "Экономика и стройка"), Tt("Resources gathered", "Добытые ресурсы"), Tt("Activity", "Активность") }; }
        private static readonly string[] EcoTags = { "E", "C", "Ct", "Hold", "W", "Idle", "Cr", "Bu", "BL", "Bx" };
        private static readonly string[] ResTags = { "St", "Lg", "Fe", "Br", "Pl", "Pk", "Bk", "In", "Gl", "Cg", "Rp", "Fb" };
        private static readonly string[] ActTags = { "Apm", "Epm", "ApmPk" };

        private static int SecOf(int k)
        {
            string t = BtcMatchStatsUI.Stats[k].Tag;
            if (Array.IndexOf(EcoTags, t) >= 0) return 1;
            if (Array.IndexOf(ResTags, t) >= 0) return 2;
            if (Array.IndexOf(ActTags, t) >= 0) return 3;
            return 0;
        }

        // ------------------------------------------------------------------ small persisted sets (favourites, profiles)
        private static readonly List<int> Favs = new List<int>();
        private static string _favRaw;

        private static void LoadFavs()
        {
            string raw = BtcMenuState.Get("stats:fav", "");
            if (raw == _favRaw) return;
            _favRaw = raw;
            Favs.Clear();
            string[] p = raw.Split(',');
            for (int i = 0; i < p.Length; i++) { int v; if (int.TryParse(p[i], out v) && v >= 0 && v < BtcMatchStatsUI.NS && !Favs.Contains(v)) Favs.Add(v); }
        }

        private static string FavRaw(List<int> l)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < l.Count; i++) { if (i > 0) sb.Append(','); sb.Append(l[i]); }
            return sb.ToString();
        }

        private static void SetFavs(string raw) { BtcMenuState.Set("stats:fav", raw); LoadFavs(); }

        private void ToggleFav(int k)
        {
            LoadFavs();
            string was = _favRaw;
            List<int> l = new List<int>(Favs);
            if (!l.Remove(k)) l.Add(k);
            SetFavs(FavRaw(l));
            BtcUndo.Push(Scope, Tt("favourite", "избранное"), delegate { SetFavs(was); });
        }

        // user profiles: "name=1,2,3;name2=4,5"
        private static List<KeyValuePair<string, string>> Profiles()
        {
            List<KeyValuePair<string, string>> r = new List<KeyValuePair<string, string>>();
            string raw = BtcMenuState.Get("stats:profiles", "");
            string[] items = raw.Split(';');
            for (int i = 0; i < items.Length; i++)
            {
                int eq = items[i].IndexOf('=');
                if (eq > 0) r.Add(new KeyValuePair<string, string>(items[i].Substring(0, eq), items[i].Substring(eq + 1)));
            }
            return r;
        }

        private static void SaveProfiles(List<KeyValuePair<string, string>> l)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < l.Count; i++) { if (i > 0) sb.Append(';'); sb.Append(l[i].Key).Append('=').Append(l[i].Value); }
            BtcMenuState.Set("stats:profiles", sb.ToString());
        }

        private static string CleanName(string n)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < n.Length && sb.Length < 14; i++) { char c = n[i]; if (c != '=' && c != ';' && c != '|' && c != ',' && c >= ' ') sb.Append(c); }
            return sb.ToString().Trim();
        }

        private static bool InProfile(string prof, int k)
        {
            if (prof == "all" || string.IsNullOrEmpty(prof)) return true;
            if (prof == "fav") return Favs.Contains(k);
            if (prof == "eco") { int s = SecOf(k); return s == 1 || s == 2; }
            if (prof == "army") return SecOf(k) == 0;
            if (prof == "act") return SecOf(k) == 3;
            if (prof.StartsWith("u:"))
            {
                List<KeyValuePair<string, string>> ps = Profiles();
                for (int i = 0; i < ps.Count; i++)
                    if (ps[i].Key == prof.Substring(2))
                    {
                        string[] p = ps[i].Value.Split(',');
                        for (int j = 0; j < p.Length; j++) { int v; if (int.TryParse(p[j], out v) && v == k) return true; }
                        return false;
                    }
            }
            return true;
        }

        // ------------------------------------------------------------------ styles
        private void Styles(int fs)
        {
            if (_ttl != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _ttl = new GUIStyle(b); _ttl.fontSize = Mathf.RoundToInt(fs * 1.15f); _ttl.fontStyle = FontStyle.Bold; _ttl.alignment = TextAnchor.MiddleLeft; _ttl.normal.textColor = Color.white;
            _lbl = new GUIStyle(b); _lbl.fontSize = Mathf.RoundToInt(fs * 0.95f); _lbl.alignment = TextAnchor.MiddleLeft; _lbl.normal.textColor = Color.white; _lbl.clipping = TextClipping.Clip; _lbl.fontStyle = FontStyle.Normal;
            _bold = new GUIStyle(_lbl); _bold.fontStyle = FontStyle.Bold;
            _dim = new GUIStyle(_lbl); _dim.fontSize = Mathf.RoundToInt(fs * 0.78f); _dim.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            _right = new GUIStyle(_lbl); _right.alignment = TextAnchor.MiddleRight;
            _chip = new GUIStyle(_lbl); _chip.alignment = TextAnchor.MiddleCenter; _chip.fontSize = Mathf.RoundToInt(fs * 0.85f);
            _tf = new GUIStyle(GUI.skin.textField); _tf.font = b.font; _tf.fontSize = Mathf.RoundToInt(fs * 0.9f); _tf.alignment = TextAnchor.MiddleLeft;
            _tf.normal.background = null; _tf.focused.background = null; _tf.hover.background = null; _tf.active.background = null;
            _tf.normal.textColor = Color.white; _tf.focused.textColor = Color.white; _tf.hover.textColor = Color.white; _tf.padding = new RectOffset(4, 4, 0, 0);
        }

        private void Txt(Rect r, string text, GUIStyle st, Color c)
        {
            Color oc = GUI.contentColor;
            GUI.contentColor = c;
            GUI.Label(r, text, st);
            GUI.contentColor = oc;
        }

        private static void DrawSprite(Rect r, Sprite s)
        {
            if (s == null || s.texture == null || Event.current.type != EventType.Repaint) return;
            Texture2D t = s.texture;
            Rect tr = s.textureRect;
            Color old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(r, t, new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height));
            GUI.color = old;
        }

        // ------------------------------------------------------------------ helpers for settings with undo
        private static void SetIntU(string key, int v, string label)
        {
            int old = BtcMenuState.Int(key, 0);
            if (old == v) return;
            BtcMenuState.SetInt(key, v);
            BtcUndo.Push(Scope, label, delegate { BtcMenuState.SetInt(key, old); });
        }

        private static void SetStrU(string key, string v, string label)
        {
            string old = BtcMenuState.Get(key, "");
            if (old == v) return;
            BtcMenuState.Set(key, v);
            BtcUndo.Push(Scope, label, delegate { BtcMenuState.Set(key, old); });
        }

        private static void SetBoolCfg(BepInEx.Configuration.ConfigEntry<bool> e, bool v, string label)
        {
            if (e == null || e.Value == v) return;
            bool old = e.Value;
            e.Value = v;
            BtcMatchStatsUI.Poke();
            BtcUndo.Push(Scope, label, delegate { e.Value = old; BtcMatchStatsUI.Poke(); });
        }

        private static void SetFloatCfg(BepInEx.Configuration.ConfigEntry<float> e, float v, float lo, float hi, string label)
        {
            if (e == null) return;
            v = Mathf.Clamp(Mathf.Round(v * 100f) / 100f, lo, hi);
            if (Mathf.Abs(e.Value - v) < 0.0005f) return;
            float old = e.Value;
            e.Value = v;
            BtcMatchStatsUI.Poke();
            BtcUndo.Push(Scope, label, delegate { e.Value = old; BtcMatchStatsUI.Poke(); }, "stats:" + label);
        }

        // ------------------------------------------------------------------ frame
        internal override void Draw(Rect area, int fs)
        {
            BtcReplayUI.Ensure(fs);
            Styles(fs);
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == BtcMenu.MenuKeyMain)
            {
                GUIUtility.keyboardControl = 0;
                BtcMenu.PgDownFrame = Time.frameCount;
                ResourcePackManager.CloseMenu();
                e.Use();
                return;
            }
            BtcUi.ClickAway(e);
            LoadFavs();
            float pad = fs, ib = fs * 1.9f;
            float y0 = area.y + fs * 0.4f;

            // title + master switch
            GUI.Label(new Rect(area.x + pad, y0, area.width * 0.4f, ib), Tt("Statistics", "Статистика"), _ttl);
            bool on = MasterOn;
            Rect sw = new Rect(area.xMax - pad - fs * 2.4f, y0 + (ib - fs * 1.2f) * 0.5f, fs * 2.4f, fs * 1.2f);
            if (BtcUi.HeaderSwitch(sw, on, on ? Tt("Statistics are ON (click to stop collecting, hide the bar and the panel)", "Статистика включена (клик - не собирать, скрыть полосу и панель)") : Tt("Statistics are OFF", "Статистика выключена")))
            {
                SetBoolCfg(BtcConfig.StatsEnabled, !on, Tt("statistics", "статистика"));
                BtcSfx.Switch(!on);
            }
            string sub = SubTitle();
            Txt(new Rect(area.x + pad + fs * 9f, y0, area.width - pad * 2f - fs * 9f - fs * 3.2f, ib), sub, _dim, Color.white);

            float y = y0 + ib + fs * 0.4f;
            if (!on)
            {
                BtcUi.Note(ref y, area.width, fs, Tt("Statistics are switched off: nothing is collected, sent or recorded. Switch them on in the header.", "Статистика выключена: ничего не собирается, не отправляется и не записывается. Включите её в шапке."));
                return;
            }
            if (!Available)
            {
                BtcUi.Note(ref y, area.width, fs, Tt("Statistics work in a game or a replay. Start or open one and come back.", "Статистика работает в игре или реплее. Начните или откройте его и вернитесь."));
                return;
            }
            if (e.type == EventType.Layout && Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + 0.25f;
                try { BtcMatchStatsUI.RefreshFactions(); BuildGraph(); }
                catch (Exception ex) { BtcLog.Error("StatsTab", "refresh: " + ex); }
            }

            // pages
            int page = Page;
            string[] pn = { Tt("Table", "Таблица"), Tt("Graph", "График"), Tt("Display", "Показ") };
            int[] pi = { BtcReplayIcons.Lines, BtcReplayIcons.Chart, BtcReplayIcons.Eye };
            float px = area.x + pad;
            for (int i = 0; i < pn.Length; i++)
            {
                float cw = _chip.CalcSize(new GUIContent(pn[i])).x + fs * 3f;
                Rect r = new Rect(px, y, cw, ib);
                bool sel = page == i, hov = r.Contains(e.mousePosition);
                BtcReplayUI.Fill(r, sel ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.2f) : new Color(1f, 1f, 1f, hov ? 0.09f : 0.04f));
                if (sel) BtcReplayUI.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), BtcMenu.Accent);
                BtcReplayIcons.Glyph(pi[i], new Rect(r.x + fs * 0.5f, r.y + (ib - fs * 1.2f) * 0.5f, fs * 1.2f, fs * 1.2f), sel ? BtcMenu.Accent : new Color(1f, 1f, 1f, 0.6f));
                Txt(new Rect(r.x + fs * 2.1f, r.y, r.width - fs * 2.2f, r.height), pn[i], _chip, sel ? Color.white : new Color(1f, 1f, 1f, 0.75f));
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn) && !sel) { SetIntU("stats:page", i, Tt("page", "страница")); page = i; }
                px += cw + fs * 0.3f;
            }
            // search (table and graph pages)
            if (page < 2)
            {
                float sw2 = Mathf.Min(area.width * 0.32f, fs * 20f);
                Rect sr = new Rect(area.xMax - pad - sw2, y, sw2, ib);
                BtcReplayUI.Fill(sr, new Color(1f, 1f, 1f, 0.06f));
                BtcReplayIcons.Glyph(BtcReplayIcons.Search, new Rect(sr.x + fs * 0.3f, sr.y + (ib - fs * 1.3f) * 0.5f, fs * 1.3f, fs * 1.3f), new Color(1f, 1f, 1f, 0.5f));
                Rect tr = new Rect(sr.x + fs * 1.8f, sr.y, sr.width - fs * 3.6f, sr.height);
                BtcUi.Field(sr);
                GUI.SetNextControlName("btcst_q");
                string nq = GUI.TextField(tr, Q, 30, _tf);
                if (nq != Q) Q = nq;
                if (Q.Length == 0 && GUI.GetNameOfFocusedControl() != "btcst_q") Txt(tr, Tt("Search a stat", "Поиск показателя"), _dim, Color.white);
                if (Q.Length > 0 && BtcReplayIcons.Plain(new Rect(sr.xMax - fs * 1.7f, sr.y + fs * 0.1f, fs * 1.7f, ib - fs * 0.2f), BtcReplayIcons.Cross, Color.white, Tt("Clear search", "Очистить поиск"))) { Q = ""; GUIUtility.keyboardControl = 0; }
            }
            y += ib + fs * 0.5f;
            BtcReplayUI.Fill(new Rect(area.x + pad, y - fs * 0.25f, area.width - pad * 2f, 1f), BtcMenu.Line);

            Rect content = new Rect(area.x, y, area.width, area.yMax - y);
            try
            {
                if (page == 0) PageTable(content, fs);
                else if (page == 1) PageGraph(content, fs);
                else PageDisplay(content, fs);
            }
            catch (Exception ex) { if (e.type == EventType.Repaint) BtcLog.Error("StatsTab", "draw: " + ex); }
            BtcReplayIcons.DrawTip(fs, area.xMax, false);
        }

        private static string SubTitle()
        {
            try
            {
                if (!BtcMatchStats.Active) return "";
                bool ru = BtcLang.Russian;
                string src = BtcMatchStats.RemoteFresh ? (BtcReplay.Mode == BtcReplay.State.Playing ? Tt("recorded in the replay", "записано в реплее") : Tt("host data", "данные хоста")) : (BtcMatchStats.Authoritative ? Tt("local", "локально") : Tt("waiting for host data (host needs BC ON)", "ждём данные хоста (нужен BC ON у хоста)"));
                return BtcMatchStatsUI.FmtTime(BtcMatchStats.Current.T) + "  |  " + src + "  |  " + (BtcMatchStatsUI.SpectatorView() ? Tt("spectator: all players", "зритель: все игроки") : Tt("your stats only", "только ваша статистика")) + (ru ? "" : "");
            }
            catch (Exception) { return ""; }
        }

        // ------------------------------------------------------------------ compare
        private static int MeIdx()
        {
            User u = User.LocalUser;
            if (u == null || u.faction == null) return -1;
            int id = u.faction.ID;
            for (int i = 0; i < BtcMatchStatsUI.Vis.Count; i++) if (BtcMatchStatsUI.Vis[i].Id == id) return i;
            return -1;
        }

        private static int TeamOf(int vi)
        {
            try
            {
                Faction f = BtcMatchStats.FactionOf(BtcMatchStatsUI.Vis[vi].Id);
                if (f != null && f.Owner != null) return f.Owner.TeamNumber;
            }
            catch (Exception) { }
            return -1000 - vi;
        }

        /// <summary>Difference of column i against the reference chosen by the compare mode. has=false: nothing to compare with.</summary>
        private static double RefDiff(int i, double[] vals, bool[] ok, int mode, int refId, out bool has)
        {
            has = false;
            int n = BtcMatchStatsUI.Vis.Count;
            if (n <= 1 || !ok[i]) return 0;
            double sum = 0; int c = 0;
            int me = MeIdx();
            if (mode == 1) { if (me >= 0 && me != i && ok[me]) { sum = vals[me]; c = 1; } }
            else if (mode == 2 || mode == 3)
            {
                if (me >= 0)
                {
                    int mt = TeamOf(me);
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i || !ok[j]) continue;
                        bool ally = TeamOf(j) == mt;
                        if ((mode == 2) == ally) { sum += vals[j]; c++; }
                    }
                }
            }
            else if (mode == 4)
            {
                for (int j = 0; j < n; j++) if (j != i && ok[j] && BtcMatchStatsUI.Vis[j].Id == refId) { sum = vals[j]; c = 1; }
            }
            if (c == 0)
            {
                for (int j = 0; j < n; j++) if (j != i && ok[j]) { sum += vals[j]; c++; }
            }
            if (c == 0) return 0;
            has = true;
            return vals[i] - sum / c;
        }

        // ------------------------------------------------------------------ table page
        private bool _editProf;

        private static List<string> HiddenProfiles()
        {
            List<string> l = new List<string>();
            string raw = BtcMenuState.Get("stats:hiddenprof", "");
            string[] p = raw.Split(',');
            for (int i = 0; i < p.Length; i++) if (p[i].Length > 0) l.Add(p[i]);
            return l;
        }

        private static void SetHidden(string id, bool hide)
        {
            string was = BtcMenuState.Get("stats:hiddenprof", "");
            List<string> l = HiddenProfiles();
            l.Remove(id);
            if (hide) l.Add(id);
            BtcMenuState.Set("stats:hiddenprof", string.Join(",", l.ToArray()));
            BtcUndo.Push(Scope, hide ? Tt("hide preset", "скрытие набора") : Tt("show preset", "показ набора"), delegate { BtcMenuState.Set("stats:hiddenprof", was); });
        }

        /// <summary>The profile (stat set) chips shared by the Table and Graph pages; the built-in sets can be hidden, user sets saved from the favourites.</summary>
        private float DrawProfiles(Rect area, float y, int fs, out string profOut)
        {
            Event e = Event.current;
            float pad = fs;
            string prof = BtcMenuState.Get("stats:profile", "all");
            List<string> hid = HiddenProfiles();
            if (!_editProf && hid.Contains(prof)) prof = "all";
            float cx = area.x + pad, ch = fs * 1.7f;
            List<string> ids = new List<string> { "all", "fav", "eco", "army", "act" };
            List<string> names = new List<string> { Tt("All", "Все"), Tt("Favourites", "Избранное"), Tt("Economy", "Экономика"), Tt("Army", "Армия"), Tt("Activity", "Активность") };
            List<KeyValuePair<string, string>> ups = Profiles();
            for (int i = 0; i < ups.Count; i++) { ids.Add("u:" + ups[i].Key); names.Add(ups[i].Key); }
            float edge = area.xMax - pad - fs * 6f;
            for (int i = 0; i < ids.Count; i++)
            {
                bool builtin = i > 0 && i < 5;
                bool hidden = builtin && hid.Contains(ids[i]);
                if (hidden && !_editProf) continue;
                float cw = _chip.CalcSize(new GUIContent(names[i])).x + fs * 1.4f + (_editProf && builtin ? fs * 1.6f : 0f);
                if (cx + cw > edge && cx > area.x + pad) break;
                Rect r = new Rect(cx, y, cw, ch);
                bool sel = prof == ids[i] && !hidden, hov = r.Contains(e.mousePosition);
                BtcReplayUI.Fill(r, sel ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.2f) : new Color(1f, 1f, 1f, hov ? 0.09f : 0.04f));
                if (sel) BtcReplayUI.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), BtcMenu.Accent);
                float tw = cw - (_editProf && builtin ? fs * 1.6f : 0f);
                Txt(new Rect(r.x, r.y, tw, r.height), names[i], _chip, hidden ? new Color(1f, 1f, 1f, 0.3f) : (sel ? Color.white : new Color(1f, 1f, 1f, 0.7f)));
                if (!hidden && BtcSfx.Btn(new Rect(r.x, r.y, tw, r.height), "", BtcReplayUI.sBtn) && !sel) { SetStrU("stats:profile", ids[i], Tt("profile", "набор")); prof = ids[i]; }
                if (_editProf && builtin)
                {
                    Rect xr = new Rect(r.xMax - fs * 1.6f, r.y, fs * 1.6f, r.height);
                    if (BtcReplayIcons.Plain(xr, hidden ? BtcReplayIcons.Plus : BtcReplayIcons.Cross, hidden ? BtcMenu.Accent : new Color(1f, 0.5f, 0.45f, 1f), hidden ? Tt("Show this preset", "Показать этот набор") : Tt("Hide this preset", "Скрыть этот набор")))
                    {
                        SetHidden(ids[i], !hidden);
                        if (!hidden && prof == ids[i]) { SetStrU("stats:profile", "all", Tt("profile", "набор")); prof = "all"; }
                    }
                }
                cx += cw + fs * 0.3f;
            }
            // save the favourites as a profile / delete the selected user profile / edit the visible presets
            Rect plus = new Rect(cx, y, ch, ch);
            if (BtcReplayIcons.Plain(plus, BtcReplayIcons.Plus, Color.white, Tt("Save the favourites (stars) as a new profile", "Сохранить избранное (звёзды) как новый набор"))) { _naming = !_naming; _newName = ""; }
            cx = plus.xMax + fs * 0.3f;
            if (prof.StartsWith("u:") && BtcReplayIcons.Plain(new Rect(cx, y, ch, ch), BtcReplayIcons.Trash, new Color(1f, 0.5f, 0.45f, 1f), Tt("Delete this profile", "Удалить этот набор")))
            {
                string nm = prof.Substring(2);
                List<KeyValuePair<string, string>> before = Profiles();
                List<KeyValuePair<string, string>> after = new List<KeyValuePair<string, string>>();
                for (int i = 0; i < before.Count; i++) if (before[i].Key != nm) after.Add(before[i]);
                SaveProfiles(after);
                string was = prof;
                BtcMenuState.Set("stats:profile", "all");
                prof = "all";
                BtcUndo.Push(Scope, Tt("delete profile", "удаление набора"), delegate { SaveProfiles(before); BtcMenuState.Set("stats:profile", was); });
            }
            if (prof.StartsWith("u:")) cx += ch + fs * 0.3f;
            if (BtcReplayIcons.Plain(new Rect(cx, y, ch, ch), BtcReplayIcons.Eye, _editProf ? BtcMenu.Accent : Color.white, _editProf ? Tt("Done editing the presets", "Готово: закончить правку наборов") : Tt("Hide or show the built-in presets", "Скрыть или показать встроенные наборы"))) _editProf = !_editProf;
            y += ch + fs * 0.35f;
            if (_naming)
            {
                Rect nr = new Rect(area.x + pad, y, Mathf.Min(area.width * 0.4f, fs * 18f), fs * 1.9f);
                BtcReplayUI.Fill(nr, new Color(1f, 1f, 1f, 0.07f));
                BtcUi.Field(nr);
                GUI.SetNextControlName("btcst_name");
                _newName = GUI.TextField(new Rect(nr.x + 2f, nr.y, nr.width - fs * 4f, nr.height), _newName, 14, _tf);
                if (_newName.Length == 0 && GUI.GetNameOfFocusedControl() != "btcst_name") Txt(new Rect(nr.x + 6f, nr.y, nr.width, nr.height), Tt("Profile name", "Имя набора"), _dim, Color.white);
                Rect okr = new Rect(nr.xMax - fs * 3.8f, nr.y, fs * 1.9f, nr.height), cr = new Rect(nr.xMax - fs * 1.9f, nr.y, fs * 1.9f, nr.height);
                bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "btcst_name";
                if (BtcReplayIcons.Plain(okr, BtcReplayIcons.Check, BtcMenu.Accent, Tt("Save", "Сохранить")) || enter)
                {
                    string nm = CleanName(_newName);
                    if (nm.Length > 0 && Favs.Count > 0)
                    {
                        List<KeyValuePair<string, string>> before = Profiles();
                        List<KeyValuePair<string, string>> after = new List<KeyValuePair<string, string>>();
                        for (int i = 0; i < before.Count; i++) if (before[i].Key != nm) after.Add(before[i]);
                        after.Add(new KeyValuePair<string, string>(nm, FavRaw(Favs)));
                        SaveProfiles(after);
                        string was = BtcMenuState.Get("stats:profile", "all");
                        BtcMenuState.Set("stats:profile", "u:" + nm);
                        prof = "u:" + nm;
                        BtcUndo.Push(Scope, Tt("new profile", "новый набор"), delegate { SaveProfiles(before); BtcMenuState.Set("stats:profile", was); });
                        _naming = false;
                        GUIUtility.keyboardControl = 0;
                        BtcSfx.Ok();
                    }
                    else { BtcSfx.Warn(); BtcMenu.Toast(Favs.Count == 0 ? Tt("Star some stats first", "Сначала отметьте показатели звёздочкой") : Tt("Type a name", "Введите имя"), BtcReplayIcons.Star); }
                    if (enter) e.Use();
                }
                if (BtcReplayIcons.Plain(cr, BtcReplayIcons.Cross, Color.white, Tt("Cancel", "Отмена"))) _naming = false;
                y += fs * 2.3f;
            }
            profOut = prof;
            return y;
        }
        private void PageTable(Rect area, int fs)
        {
            Event e = Event.current;
            float pad = fs;
            List<BtcMsFaction> vis = BtcMatchStatsUI.Vis;
            int n = vis.Count;
            float y = area.y;

            string prof;
            y = DrawProfiles(area, y, fs, out prof);
            float ch = fs * 1.7f;

            // compare modes
            int me = MeIdx();
            int mode = BtcMenuState.Int("stats:cmp", 0);
            int refId = BtcMenuState.Int("stats:cmpid", -1);
            if (mode == 4) { bool found = false; for (int i = 0; i < n; i++) if (vis[i].Id == refId) found = true; if (!found) mode = 0; }
            if ((mode >= 1 && mode <= 3) && me < 0) mode = 0;
            if (n > 1)
            {
                Txt(new Rect(area.x + pad, y, fs * 6f, ch), Tt("Compare with", "Сравнение с"), _dim, Color.white);
                float mx = area.x + pad + fs * 6.2f;
                List<int> mm = new List<int> { 0 };
                List<string> mn = new List<string> { Tt("others", "остальными") };
                if (me >= 0) { mm.Add(1); mn.Add(Tt("me", "мной")); mm.Add(2); mn.Add(Tt("allies", "союзниками")); mm.Add(3); mn.Add(Tt("enemies", "врагами")); }
                for (int i = 0; i < mm.Count; i++)
                {
                    float cw = _chip.CalcSize(new GUIContent(mn[i])).x + fs * 1.4f;
                    Rect r = new Rect(mx, y, cw, ch);
                    bool sel = mode == mm[i], hov = r.Contains(e.mousePosition);
                    BtcReplayUI.Fill(r, sel ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.2f) : new Color(1f, 1f, 1f, hov ? 0.09f : 0.04f));
                    Txt(r, mn[i], _chip, sel ? Color.white : new Color(1f, 1f, 1f, 0.7f));
                    if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn) && !sel) { SetIntU("stats:cmp", mm[i], Tt("compare", "сравнение")); mode = mm[i]; }
                    mx += cw + fs * 0.3f;
                }
                if (mode == 4) Txt(new Rect(mx, y, fs * 12f, ch), Tt("vs ", "с ") + NameOfId(refId), _dim, BtcMenu.Accent);
                else Txt(new Rect(mx, y, area.xMax - mx - pad, ch), Tt("click a player name in the table to compare with that player", "клик по имени игрока в таблице сравнивает с этим игроком"), _dim, Color.white);
                y += ch + fs * 0.4f;
            }

            if (n == 0)
            {
                BtcUi.Note(ref y, area.width, fs, Tt("No data yet.", "Данных пока нет."));
                return;
            }

            // column geometry
            float nameW = fs * 15f;
            float availW = area.width - pad * 2f - nameW - 14f;
            float colW = Mathf.Clamp(availW / n, fs * 7f, fs * 13f);

            // sticky header row
            float hh = fs * 2.3f;
            Rect hrow = new Rect(area.x + pad, y, nameW + colW * n, hh);
            BtcReplayUI.Fill(hrow, new Color(1f, 1f, 1f, 0.04f));
            for (int i = 0; i < n; i++)
            {
                Rect hc = new Rect(hrow.x + nameW + colW * i, y, colW, hh);
                Color fc = BtcMatchStatsUI.FCol[i];
                BtcReplayUI.Fill(new Rect(hc.x + 2f, hc.y + hh * 0.15f, 3f, hh * 0.7f), fc);
                float fx = hc.x + fs * 0.6f;
                if (BtcMatchStatsUI.FFlag[i] != null) { DrawSprite(new Rect(fx, hc.y + hh * 0.15f, hh * 0.7f, hh * 0.7f), BtcMatchStatsUI.FFlag[i]); fx += hh * 0.8f; }
                bool isRef = mode == 4 && vis[i].Id == refId;
                Color nc = new Color(fc.r * 0.5f + 0.5f, fc.g * 0.5f + 0.5f, fc.b * 0.5f + 0.5f, 1f);
                Txt(new Rect(fx, hc.y, hc.xMax - fx - (isRef ? fs * 1.3f : 2f), hh), BtcMatchStatsUI.FName[i], _bold, nc);
                if (isRef) BtcReplayIcons.Glyph(BtcReplayIcons.Target, new Rect(hc.xMax - fs * 1.3f, hc.y + (hh - fs) * 0.5f, fs, fs), BtcMenu.Accent);
                if (n > 1)
                {
                    BtcReplayIcons.Hint(hc, isRef ? Tt("Comparing with this player (click to reset)", "Сравнение с этим игроком (клик - сбросить)") : Tt("Compare everyone with this player", "Сравнить всех с этим игроком"));
                    if (BtcSfx.Btn(hc, "", BtcReplayUI.sBtn))
                    {
                        if (isRef) SetIntU("stats:cmp", 0, Tt("compare", "сравнение"));
                        else { SetIntU("stats:cmpid", vis[i].Id, Tt("compare", "сравнение")); SetIntU("stats:cmp", 4, Tt("compare", "сравнение")); }
                    }
                }
            }
            y += hh + fs * 0.15f;

            // rows
            Rect view = new Rect(area.x, y, area.width, area.yMax - y);
            float vw = view.width - (_h > view.height ? 14f : 0f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, vw, _h));
            float cy = 0f;
            string q = Q.Trim().ToLowerInvariant();
            bool searching = q.Length > 0;
            int ns = BtcMatchStatsUI.NS;
            string[] secNames = SecName();
            List<int> rows = new List<int>();
            int shown = 0;
            for (int sec = 0; sec < SecKey.Length; sec++)
            {
                rows.Clear();
                for (int k = 0; k < ns; k++)
                {
                    if (SecOf(k) != sec || !InProfile(prof, k)) continue;
                    BtcMatchStatsUI.DStat st = BtcMatchStatsUI.Stats[k];
                    if (searching && st.En.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0 && st.Ru.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0 && st.Tag.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0) continue;
                    rows.Add(k);
                }
                if (rows.Count == 0) continue;
                shown += rows.Count;
                if (!searching)
                {
                    if (!BtcUi.Fold(ref cy, vw, fs, "stats:sec:" + SecKey[sec], secNames[sec], rows.Count)) continue;
                }
                for (int r = 0; r < rows.Count; r++) cy = StatRow(cy, vw, fs, rows[r], nameW, colW, mode, refId, vis, n, area.x, view);
                cy += fs * 0.4f;
            }
            if (shown == 0) BtcUi.Note(ref cy, vw, fs, prof == "fav" ? Tt("No favourites yet: click the star next to a stat.", "Избранного пока нет: нажмите звёздочку у показателя.") : Tt("Nothing matches.", "Ничего не найдено."));
            GUI.EndScrollView();
            _h = cy + fs * 2f;
        }

        private static string NameOfId(int id)
        {
            for (int i = 0; i < BtcMatchStatsUI.Vis.Count; i++) if (BtcMatchStatsUI.Vis[i].Id == id) return BtcMatchStatsUI.FName[i];
            return "?";
        }

        private float StatRow(float y, float w, int fs, int k, float nameW, float colW, int mode, int refId, List<BtcMsFaction> vis, int n, float areaX, Rect view)
        {
            Event e = Event.current;
            BtcMatchStatsUI.DStat st = BtcMatchStatsUI.Stats[k];
            float pad = fs, rh = fs * 2.0f;
            Rect row = new Rect(pad, y, nameW + colW * n, rh);
            bool hov = row.Contains(e.mousePosition);
            if (hov) BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.05f));
            // star
            bool fav = Favs.Contains(k);
            Rect star = new Rect(row.x + 2f, y + (rh - fs * 1.2f) * 0.5f, fs * 1.2f, fs * 1.2f);
            BtcReplayIcons.Glyph(BtcReplayIcons.Star, star, fav ? new Color(1f, 0.82f, 0.3f, 1f) : new Color(1f, 1f, 1f, hov ? 0.35f : 0.12f));
            Rect starHit = new Rect(star.x - 3f, y, star.width + 6f, rh);
            BtcReplayIcons.Hint(starHit, new Rect(areaX + starHit.x, view.y + y - _scroll.y, starHit.width, starHit.height), fav ? Tt("Remove from favourites", "Убрать из избранного") : Tt("Add to favourites", "В избранное"));
            if (BtcSfx.Btn(starHit, "", BtcReplayUI.sBtn)) ToggleFav(k);
            // name + detail chevron + graph icon
            float nx = row.x + fs * 1.8f;
            bool hasDet = st.Det >= 0;
            string ek = "stats:exp:" + st.Tag;
            bool exp = hasDet && BtcMenuState.Bool(ek, false);
            if (hasDet)
            {
                BtcReplayIcons.Glyph(exp ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, new Rect(nx, y + (rh - fs) * 0.5f, fs * 1.2f, fs), new Color(1f, 1f, 1f, 0.6f));
                nx += fs * 1.4f;
            }
            Rect nameR = new Rect(nx, y, row.x + nameW - nx - fs * 1.6f, rh);
            Txt(nameR, st.Name, _lbl, new Color(1f, 1f, 1f, 0.92f));
            Rect gr = new Rect(row.x + nameW - fs * 1.6f, y + (rh - fs * 1.1f) * 0.5f, fs * 1.1f, fs * 1.1f);
            if (hov) BtcReplayIcons.Glyph(BtcReplayIcons.Chart, gr, new Color(1f, 1f, 1f, 0.55f));
            Rect grHit = new Rect(gr.x - 3f, y, gr.width + 6f, rh);
            BtcReplayIcons.Hint(grHit, new Rect(areaX + grHit.x, view.y + y - _scroll.y, grHit.width, grHit.height), Tt("Show on the graph", "Показать на графике"));
            if (BtcSfx.Btn(grHit, "", BtcReplayUI.sBtn)) { SetIntU("stats:graph", k, Tt("graph", "график")); SetIntU("stats:page", 1, Tt("page", "страница")); _next = 0f; }
            if (hasDet)
            {
                Rect nr = new Rect(nx, y, nameR.width, rh);
                if (BtcSfx.Btn(nr, "", BtcReplayUI.sBtn)) { bool was = exp; BtcMenuState.SetBool(ek, !exp); BtcUndo.Push(Scope, Tt("details", "подробности"), delegate { BtcMenuState.SetBool(ek, was); }); }
            }
            // cells
            double[] vals = new double[n];
            bool[] ok = new bool[n];
            for (int i = 0; i < n; i++) { ok[i] = BtcMatchStatsUI.Avail(st, vis[i].V); vals[i] = ok[i] ? BtcMatchStatsUI.Value(st, vis[i].V) : 0; }
            int best = BestIdx(st, vals, ok);
            for (int i = 0; i < n; i++)
            {
                Rect c = new Rect(row.x + nameW + colW * i, y, colW, rh);
                if (!ok[i]) { Txt(c, "n/a", _lbl, new Color(1f, 1f, 1f, 0.3f)); continue; }
                bool has;
                double d = RefDiff(i, vals, ok, mode, refId, out has);
                if (n == 1) { d = BtcMatchStatsUI.DiffOf(st, 0); has = Math.Abs(d) > 0.0005; }
                Color vc = st.Kind == 1 && vals[i] >= 99.9995 ? PerfectBlue : Color.white;
                string vt = BtcMatchStatsUI.Fmt(st, vals[i], false);
                float vwid = _bold.CalcSize(new GUIContent(vt)).x;
                Txt(new Rect(c.x + fs * 0.6f, y, colW - fs * 0.6f, rh), vt, _bold, vc);
                if (has && Math.Abs(d) > 0.0005)
                    Txt(new Rect(c.x + fs * 0.6f + vwid + fs * 0.4f, y, colW - vwid - fs, rh), BtcMatchStatsUI.Fmt(st, d, true), _dim, BtcMatchStatsUI.Good(st, d));
                if (i == best) BtcReplayUI.Fill(new Rect(c.x + fs * 0.6f, y + rh - 3f, Mathf.Max(fs, vwid), 2f), new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.8f));
            }
            y += rh;
            if (hasDet && exp) y = DetailRows(y, fs, st, nameW, colW, mode, refId, vis, n, row.x);
            return y;
        }

        private static int BestIdx(BtcMatchStatsUI.DStat st, double[] vals, bool[] ok)
        {
            int n = vals.Length;
            if (n < 2) return -1;
            int best = -1; bool tie = false;
            for (int i = 0; i < n; i++)
            {
                if (!ok[i]) continue;
                if (best < 0) { best = i; continue; }
                double a = vals[i], b = vals[best];
                if (Math.Abs(a - b) < 0.0005) tie = true;
                else if (st.LowerBetter ? a < b : a > b) { best = i; tie = false; }
            }
            return tie ? -1 : best;
        }

        private float DetailRows(float y, int fs, BtcMatchStatsUI.DStat st, float nameW, float colW, int mode, int refId, List<BtcMsFaction> vis, int n, float x0)
        {
            List<string> keys = new List<string>();
            for (int i = 0; i < n; i++)
                foreach (KeyValuePair<string, int> kv in vis[i].Det[st.Det])
                    if (!keys.Contains(kv.Key)) keys.Add(kv.Key);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            float rh = fs * 1.7f;
            double[] vals = new double[n];
            bool[] ok = new bool[n];
            for (int r = 0; r < keys.Count; r++)
            {
                for (int i = 0; i < n; i++) { int v; vis[i].Det[st.Det].TryGetValue(keys[r], out v); vals[i] = v; ok[i] = true; }
                Rect row = new Rect(x0, y, nameW + colW * n, rh);
                if ((r & 1) == 0) BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.02f));
                Txt(new Rect(x0 + fs * 3.4f, y, nameW - fs * 3.4f, rh), keys[r], _dim, new Color(1f, 1f, 1f, 0.85f));
                for (int i = 0; i < n; i++)
                {
                    Rect c = new Rect(x0 + nameW + colW * i, y, colW, rh);
                    bool has;
                    double d = RefDiff(i, vals, ok, mode, refId, out has);
                    string vt = ((int)vals[i]).ToString();
                    float vwid = _lbl.CalcSize(new GUIContent(vt)).x;
                    Txt(new Rect(c.x + fs * 0.6f, y, colW - fs * 0.6f, rh), vt, _lbl, Color.white);
                    if (has && Math.Abs(d) > 0.0005) Txt(new Rect(c.x + fs * 0.6f + vwid + fs * 0.4f, y, colW - vwid - fs, rh), BtcMatchStatsUI.Fmt(st, d, true), _dim, BtcMatchStatsUI.Good(st, d));
                }
                y += rh;
            }
            if (keys.Count == 0) { Txt(new Rect(x0 + fs * 3.4f, y, nameW, rh), Tt("nothing yet", "пока ничего"), _dim, Color.white); y += rh; }
            return y + fs * 0.2f;
        }

        // ------------------------------------------------------------------ graph page
        private readonly float[][] _gy = new float[8][];
        private readonly float[] _gx = new float[0];
        private readonly int[] _gn = new int[8];
        private float _gMin, _gMax, _gT;

        private int GraphStat
        {
            get { int k = BtcMenuState.Int("stats:graph", 0); return k < 0 || k >= BtcMatchStatsUI.NS ? 0 : k; }
        }

        private void BuildGraph()
        {
            if (!BtcMatchStats.Active) return;
            List<BtcMsFaction> vis = BtcMatchStatsUI.Vis;
            BtcMatchStatsUI.DStat s = BtcMatchStatsUI.Stats[GraphStat];
            bool diff = BtcMenuState.Bool("stats:gdiff", false) && vis.Count > 1;
            _gMin = float.MaxValue; _gMax = float.MinValue; _gT = Mathf.Max(1f, BtcMatchStats.Current.T);
            for (int i = 0; i < vis.Count && i < _gy.Length; i++)
            {
                BtcMsFaction f = vis[i];
                int cnt = f.Series.Count;
                if (BtcReplay.Mode == BtcReplay.State.Playing) while (cnt > 0 && f.Series[cnt - 1].T > BtcMatchStats.Current.T + 0.5f) cnt--;
                if (_gy[i] == null || _gy[i].Length < cnt + 1) _gy[i] = new float[Mathf.NextPowerOfTwo(cnt + 1)];
                float[] y = _gy[i];
                for (int k = 0; k < cnt; k++) y[k] = BtcMatchStatsUI.SampleValue(s, f, k, diff);
                y[cnt] = diff ? (float)BtcMatchStatsUI.DiffOf(s, i) : (float)BtcMatchStatsUI.Value(s, f.V);
                _gn[i] = cnt + 1;
                for (int k = 0; k <= cnt; k++) { if (y[k] < _gMin) _gMin = y[k]; if (y[k] > _gMax) _gMax = y[k]; }
            }
            if (_gMin > _gMax) { _gMin = 0f; _gMax = 1f; }
            if (diff) { float m = Mathf.Max(Mathf.Abs(_gMin), Mathf.Abs(_gMax)); _gMin = -m; _gMax = m; }
            if (_gMax - _gMin < 1e-3f) _gMax = _gMin + 1f;
        }

        private void PageGraph(Rect area, int fs)
        {
            Event e = Event.current;
            float pad = fs;
            List<BtcMsFaction> vis = BtcMatchStatsUI.Vis;
            int n = Mathf.Min(vis.Count, _gy.Length);
            int gk = GraphStat;
            BtcMatchStatsUI.DStat st = BtcMatchStatsUI.Stats[gk];
            string prof;
            float py0 = DrawProfiles(area, area.y, fs, out prof);   // the same stat sets as on the Table page

            // left: stat list
            float lw = Mathf.Min(area.width * 0.3f, fs * 17f);
            Rect lv = new Rect(area.x + pad, py0, lw, area.yMax - py0);
            string q = Q.Trim().ToLowerInvariant();
            List<int> list = new List<int>();
            for (int k = 0; k < BtcMatchStatsUI.NS; k++)
            {
                BtcMatchStatsUI.DStat s = BtcMatchStatsUI.Stats[k];
                if (q.Length > 0 && s.En.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0 && s.Ru.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0 && s.Tag.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0) continue;
                if (!InProfile(prof, k) && k != gk) continue;
                list.Add(k);
            }
            float rh = fs * 1.9f;
            Rect content = new Rect(0f, 0f, lv.width - (list.Count * rh > lv.height ? 14f : 0f), list.Count * rh);
            _listScroll = GUI.BeginScrollView(lv, _listScroll, content);
            for (int i = 0; i < list.Count; i++)
            {
                int k = list[i];
                Rect r = new Rect(0f, i * rh, content.width, rh);
                bool sel = k == gk, hov = r.Contains(e.mousePosition);
                BtcReplayUI.Fill(r, sel ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.18f) : new Color(1f, 1f, 1f, hov ? 0.07f : 0f));
                if (sel) BtcReplayUI.Fill(new Rect(r.x, r.y + rh * 0.15f, 3f, rh * 0.7f), BtcMenu.Accent);
                Txt(new Rect(r.x + fs * 0.8f, r.y, r.width - fs, rh), BtcMatchStatsUI.Stats[k].Name, _lbl, sel ? Color.white : new Color(1f, 1f, 1f, 0.75f));
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn) && !sel) { SetIntU("stats:graph", k, Tt("graph", "график")); _next = 0f; }
            }
            GUI.EndScrollView();
            if (list.Count == 0) Txt(new Rect(lv.x, lv.y, lv.width, rh), Tt("Nothing matches.", "Ничего не найдено."), _dim, Color.white);

            // right: graph
            float gx = lv.xMax + pad;
            float gw = area.xMax - pad - gx;
            if (gw < fs * 8f) return;
            float top = py0;
            Txt(new Rect(gx, top, gw - fs * 2.4f, fs * 2f), st.Name, _ttl, Color.white);
            bool diff = BtcMenuState.Bool("stats:gdiff", false);
            if (vis.Count > 1)
            {
                Rect dr = new Rect(area.xMax - pad - fs * 1.9f, top, fs * 1.9f, fs * 1.9f);
                if (BtcReplayIcons.Plain(dr, BtcReplayIcons.Sort, diff ? BtcMenu.Accent : Color.white, diff ? Tt("Showing the difference from the others (click for real values)", "Показана разница с остальными (клик - реальные значения)") : Tt("Show the difference from the others", "Показать разницу с остальными")))
                {
                    bool was = diff;
                    BtcMenuState.SetBool("stats:gdiff", !diff);
                    BtcUndo.Push(Scope, Tt("graph mode", "режим графика"), delegate { BtcMenuState.SetBool("stats:gdiff", was); });
                    _next = 0f;
                }
            }
            // legend
            float lx = gx;
            float ly = top + fs * 2.2f;
            for (int i = 0; i < n; i++)
            {
                float cw = _lbl.CalcSize(new GUIContent(BtcMatchStatsUI.FName[i])).x + fs * 1.8f;
                if (lx + cw > gx + gw) break;
                BtcReplayUI.Fill(new Rect(lx, ly + fs * 0.45f, fs * 0.9f, fs * 0.5f), BtcMatchStatsUI.FCol[i]);
                Txt(new Rect(lx + fs * 1.1f, ly, cw, fs * 1.4f), BtcMatchStatsUI.FName[i], _lbl, new Color(1f, 1f, 1f, 0.85f));
                lx += cw + fs * 0.4f;
            }
            Rect g = new Rect(gx + fs * 3f, ly + fs * 1.8f, gw - fs * 3f, area.yMax - (ly + fs * 1.8f) - fs * 2.2f);
            if (g.height < fs * 4f) return;
            bool dd = diff && vis.Count > 1;
            BtcReplayUI.Fill(g, new Color(0f, 0f, 0f, 0.3f));
            float span = _gMax - _gMin;
            for (int i = 0; i <= 4; i++)
            {
                float yy = g.y + g.height * i / 4f;
                BtcReplayUI.Fill(new Rect(g.x, Mathf.Min(yy, g.yMax - 1f), g.width, 1f), new Color(1f, 1f, 1f, i == 0 || i == 4 ? 0.12f : 0.05f));
                float val = _gMax - span * i / 4f;
                Txt(new Rect(gx, yy - fs * 0.7f, fs * 2.9f, fs * 1.4f), BtcMatchStatsUI.Fmt(st, val, dd), _dim, Color.white);
            }
            if (dd && _gMin < 0f && _gMax > 0f) BtcReplayUI.Fill(new Rect(g.x, g.yMax - (0f - _gMin) / span * g.height, g.width, 1f), new Color(1f, 1f, 1f, 0.3f));
            if (Event.current.type == EventType.Repaint)
            {
                for (int i = 0; i < n; i++)
                {
                    int cnt = _gn[i];
                    float[] y = _gy[i];
                    if (y == null || cnt <= 0) continue;
                    Color c = BtcMatchStatsUI.FCol[i];
                    float prev = float.NaN;
                    int cols = Mathf.Max(2, (int)(g.width / 2f));
                    for (int x = 0; x < cols; x++)
                    {
                        int k = cnt == 1 ? 0 : Mathf.RoundToInt((float)x / (cols - 1) * (cnt - 1));
                        float py = g.yMax - (y[k] - _gMin) / span * g.height;
                        float a = float.IsNaN(prev) ? py : prev;
                        float lo = Mathf.Min(a, py), hi = Mathf.Max(a, py);
                        BtcReplayUI.Fill(new Rect(g.x + x * 2f, lo - 1f, 2f, hi - lo + 2f), c);
                        prev = py;
                    }
                }
            }
            Txt(new Rect(g.x, g.yMax + 2f, g.width * 0.5f, fs * 1.4f), "0s", _dim, Color.white);
            Txt(new Rect(g.x + g.width * 0.5f, g.yMax + 2f, g.width * 0.5f, fs * 1.4f), BtcMatchStatsUI.FmtTime(_gT), _right, new Color(1f, 1f, 1f, 0.55f));
            // hover: vertical cursor with the values at that time
            if (g.Contains(e.mousePosition) && n > 0)
            {
                float fx = (e.mousePosition.x - g.x) / g.width;
                BtcReplayUI.Fill(new Rect(e.mousePosition.x, g.y, 1f, g.height), new Color(1f, 1f, 1f, 0.35f));
                float t = fx * _gT;
                StringBuilder sb = new StringBuilder();
                sb.Append(BtcMatchStatsUI.FmtTime(t));
                int lines = 1;
                float tw = _lbl.CalcSize(new GUIContent(sb.ToString())).x;
                for (int i = 0; i < n; i++)
                {
                    int cnt = _gn[i];
                    if (cnt <= 0) continue;
                    int k = cnt == 1 ? 0 : Mathf.Clamp(Mathf.RoundToInt(fx * (cnt - 1)), 0, cnt - 1);
                    string line = BtcMatchStatsUI.FName[i] + ": " + BtcMatchStatsUI.Fmt(st, _gy[i][k], dd);
                    tw = Mathf.Max(tw, _lbl.CalcSize(new GUIContent(line)).x);
                    lines++;
                }
                float bw = tw + fs * 2.4f, bh = lines * fs * 1.4f + fs * 0.6f;
                Rect tip = new Rect(Mathf.Min(e.mousePosition.x + fs, g.xMax - bw), g.y + fs * 0.4f, bw, bh);
                BtcReplayUI.Fill(tip, new Color(0.02f, 0.025f, 0.03f, 0.95f));
                BtcReplayUI.Fill(new Rect(tip.x, tip.y, 3f, tip.height), BtcMenu.Accent);
                float ty = tip.y + fs * 0.3f;
                Txt(new Rect(tip.x + fs * 0.8f, ty, bw, fs * 1.4f), BtcMatchStatsUI.FmtTime(t), _bold, BtcMenu.Accent);
                ty += fs * 1.4f;
                for (int i = 0; i < n; i++)
                {
                    int cnt = _gn[i];
                    if (cnt <= 0) continue;
                    int k = cnt == 1 ? 0 : Mathf.Clamp(Mathf.RoundToInt(fx * (cnt - 1)), 0, cnt - 1);
                    BtcReplayUI.Fill(new Rect(tip.x + fs * 0.8f, ty + fs * 0.45f, fs * 0.8f, fs * 0.5f), BtcMatchStatsUI.FCol[i]);
                    Txt(new Rect(tip.x + fs * 1.9f, ty, bw, fs * 1.4f), BtcMatchStatsUI.FName[i] + ": " + BtcMatchStatsUI.Fmt(st, _gy[i][k], dd), _lbl, Color.white);
                    ty += fs * 1.4f;
                }
            }
        }

        // ------------------------------------------------------------------ display page (bar and panel)
        private void PageDisplay(Rect area, int fs)
        {
            Rect view = area;
            float vw = view.width - (_h > view.height ? 14f : 0f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, vw, _h));
            float y = fs * 0.3f;
            BtcMatchStatsUI.SyncMasks();

            if (BtcUi.Fold(ref y, vw, fs, "stats:disp:bar", Tt("Bar at the edge of the screen", "Полоса у края экрана"), 0))
            {
                bool bar = BtcConfig.StatsBar != null && BtcConfig.StatsBar.Value;
                if (BtcConfig.StatsBar != null && BtcUi.Toggle(ref y, vw, fs, Tt("Show the bar", "Показывать полосу"), bar, Tt("A thin strip with the main numbers of every player", "Тонкая полоса с главными цифрами каждого игрока"))) SetBoolCfg(BtcConfig.StatsBar, !bar, Tt("bar", "полоса"));
                bool bot = BtcConfig.StatsBarBottom != null && BtcConfig.StatsBarBottom.Value;
                if (BtcConfig.StatsBarBottom != null && BtcUi.Toggle(ref y, vw, fs, Tt("At the bottom", "Внизу экрана"), bot, Tt("Off: at the top", "Выкл: вверху"))) SetBoolCfg(BtcConfig.StatsBarBottom, !bot, Tt("bar position", "положение полосы"));
                StepRow(ref y, vw, fs, Tt("Offset from the edge", "Отступ от края"), BtcConfig.StatsBarOffset, 10f, 0f, 300f, "0", Tt("bar offset", "отступ полосы"));
                StepRow(ref y, vw, fs, Tt("Scale", "Масштаб"), BtcConfig.StatsBarScale, 0.05f, 0.5f, 2f, "0.00", Tt("bar scale", "масштаб полосы"));
                StepRow(ref y, vw, fs, Tt("Opacity", "Непрозрачность"), BtcConfig.StatsBarOpacity, 0.05f, 0.1f, 1f, "0.00", Tt("bar opacity", "прозрачность полосы"));
            }
            if (BtcUi.Fold(ref y, vw, fs, "stats:disp:barlist", Tt("What the bar shows", "Что показывает полоса"), 0))
                y = StatChips(y, vw, fs, BtcMatchStatsUI.InBar, true);
            BtcUi.Note(ref y, vw, fs, Tt("The statistics hotkey (" + BtcMatchStatsUI.HotkeyText() + ", change it in Advanced Hotkeys > Match statistics) opens this tab.", "Клавиша статистики (" + BtcMatchStatsUI.HotkeyText() + ", меняется в Advanced Hotkeys > Match statistics) открывает эту вкладку."));
            GUI.EndScrollView();
            _h = y + fs * 2f;
        }

        private void StepRow(ref float y, float w, int fs, string label, BepInEx.Configuration.ConfigEntry<float> e, float step, float lo, float hi, string fmt, string undoLabel)
        {
            if (e == null) return;
            int d = BtcUi.Stepper(ref y, w, fs, label, e.Value.ToString(fmt));
            if (d != 0) SetFloatCfg(e, e.Value + d * step, lo, hi, undoLabel);
        }

        private float StatChips(float y, float w, int fs, bool[] mask, bool bar)
        {
            Event e = Event.current;
            float pad = fs, x = pad, ch = fs * 1.8f;
            int ns = BtcMatchStatsUI.NS;
            for (int k = 0; k < ns; k++)
            {
                string name = BtcMatchStatsUI.Stats[k].Name;
                float cw = _chip.CalcSize(new GUIContent(name)).x + fs * 2.6f;
                if (x + cw > w - pad && x > pad) { x = pad; y += ch + fs * 0.3f; }
                Rect r = new Rect(x, y, cw, ch);
                bool on = mask[k], hov = r.Contains(e.mousePosition);
                BtcReplayUI.Fill(r, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.2f) : new Color(1f, 1f, 1f, hov ? 0.09f : 0.04f));
                if (on) BtcReplayUI.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), BtcMenu.Accent);
                BtcReplayIcons.Glyph(on ? BtcReplayIcons.Check : BtcReplayIcons.Ring, new Rect(r.x + fs * 0.4f, r.y + (ch - fs) * 0.5f, fs, fs), on ? BtcMenu.Accent : new Color(1f, 1f, 1f, 0.35f));
                Txt(new Rect(r.x + fs * 1.7f, r.y, r.width - fs * 1.8f, r.height), name, _chip, on ? Color.white : new Color(1f, 1f, 1f, 0.7f));
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn))
                {
                    int kk = k; bool was = on;
                    mask[k] = !on;
                    if (bar) BtcMatchStatsUI.SaveBarMask(); else BtcMatchStatsUI.SavePanelMask();
                    BtcUndo.Push(Scope, bar ? Tt("bar stats", "показатели полосы") : Tt("panel stats", "показатели панели"), delegate { mask[kk] = was; if (bar) BtcMatchStatsUI.SaveBarMask(); else BtcMatchStatsUI.SavePanelMask(); });
                }
                x += cw + fs * 0.3f;
            }
            return y + ch + fs * 0.6f;
        }
    }
}
