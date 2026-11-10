using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// What's new tab (rail icon: news). Releases newest first, every change as a row with a kind icon (new / improved / fixed / removed), an area tag
    /// and, when the change has a place in the menu, a link chip that jumps straight there. Filters: kind, area, search.
    /// </summary>
    internal sealed class BtcChangelogTab : BtcMenuTab
    {
        internal static bool TabActive;
        internal static readonly BtcChangelogTab Instance = new BtcChangelogTab();

        private const string Scope = "changelog";
        private Vector2 _scroll;
        private float _h = 100f;
        private string _q = "";
        private string _kinds = "+~*=-";      // enabled kinds
        private string _area = "";           // "" = all
        private readonly Dictionary<string, bool> _open = new Dictionary<string, bool>();
        private GUIStyle _lbl, _dim, _wrap, _chip, _tf, _big;
        private int _fs;

        internal override int Id { get { return 10; } }
        internal override bool IsActive { get { return TabActive; } }
        internal override string UndoScope { get { return Scope; } }

        internal static void OnOpen() { BtcChangelog.MarkSeen(); }

        private static string T(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static Color KindColor(char k)
        {
            switch (k)
            {
                case '+': return new Color(0.55f, 0.85f, 0.55f, 1f);
                case '~': return new Color(0.75f, 0.6f, 1f, 1f);
                case '*': return new Color(0.98f, 0.8f, 0.4f, 1f);
                case '=': return new Color(0.45f, 0.9f, 0.85f, 1f);
                default: return new Color(1f, 0.5f, 0.45f, 1f);
            }
        }

        private static int KindIcon(char k)
        {
            switch (k) { case '+': return BtcReplayIcons.Plus; case '~': return BtcReplayIcons.Refresh; case '*': return BtcReplayIcons.Bolt; case '=': return BtcReplayIcons.Bug; default: return BtcReplayIcons.Cross; }
        }

        private static string KindName(char k)
        {
            switch (k) { case '+': return T("New", "Новое"); case '~': return T("Reworked", "Переработано"); case '*': return T("Improved", "Улучшено"); case '=': return T("Issue fixed", "Проблема исправлена"); default: return T("Removed", "Убрано"); }
        }

        private static string AreaName(string key)
        {
            for (int i = 0; i < BtcReportData.AreaKeys.Length; i++) if (BtcReportData.AreaKeys[i] == key) return T(BtcReportData.AreaEn[i], BtcReportData.AreaRu[i]);
            return key;
        }

        private void Styles(int fs)
        {
            if (_lbl != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _lbl = new GUIStyle(b); _lbl.fontSize = Mathf.RoundToInt(fs * 0.95f); _lbl.alignment = TextAnchor.MiddleLeft; _lbl.normal.textColor = Color.white; _lbl.clipping = TextClipping.Clip;
            _dim = new GUIStyle(_lbl); _dim.fontSize = Mathf.RoundToInt(fs * 0.75f); _dim.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
            _wrap = new GUIStyle(_lbl); _wrap.wordWrap = true; _wrap.alignment = TextAnchor.UpperLeft; _wrap.clipping = TextClipping.Overflow;
            _chip = new GUIStyle(_lbl); _chip.alignment = TextAnchor.MiddleCenter; _chip.fontSize = Mathf.RoundToInt(fs * 0.8f);
            _big = new GUIStyle(_lbl); _big.fontSize = Mathf.RoundToInt(fs * 1.35f); _big.fontStyle = FontStyle.Bold;
            _tf = new GUIStyle(GUI.skin.textField); _tf.font = b.font; _tf.fontSize = Mathf.RoundToInt(fs * 0.9f); _tf.alignment = TextAnchor.MiddleLeft;
            _tf.normal.background = null; _tf.focused.background = null; _tf.hover.background = null; _tf.active.background = null;
            _tf.normal.textColor = Color.white; _tf.focused.textColor = Color.white; _tf.hover.textColor = Color.white; _tf.padding = new RectOffset(4, 4, 0, 0);
        }

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
            BtcSfx.Typing(e);
            Rect view = new Rect(0f, 0f, area.width - (_h > area.height ? 14f : 0f), _h);
            _scroll = GUI.BeginScrollView(area, _scroll, view);
            BtcUi.ClickAway(e);
            float w = view.width, y = fs * 0.5f;
            try { Body(ref y, w, fs); }
            catch (Exception ex) { if (e.type == EventType.Repaint) BtcLog.Error("Changelog", "draw: " + ex); }
            BtcReplayIcons.DrawTip(fs, w, BtcReplayIcons.TipAnchor.y - _scroll.y < fs * 3f); // below the icon near the top edge, else it hides under the view edge
            GUI.EndScrollView();
            _h = y + fs * 2f;
        }

        private bool Matches(BtcChangelog.Entry en)
        {
            if (_kinds.IndexOf(en.Kind) < 0) return false;
            if (_area.Length > 0 && en.Area != _area) return false;
            if (_q.Length > 0)
            {
                string q = _q.ToLowerInvariant();
                if (en.En.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0 && en.Ru.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0
                    && AreaName(en.Area).ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0) return false;
            }
            return true;
        }

        private void Body(ref float y, float w, int fs)
        {
            Event e = Event.current;
            List<BtcChangelog.Release> rel = BtcChangelog.Releases;
            float pad = fs;

            // toolbar: search + kind chips
            Rect sr = new Rect(pad, y, Mathf.Min(w * 0.38f, fs * 22f), fs * 2f);
            BtcReplayUI.Fill(sr, new Color(1f, 1f, 1f, 0.06f));
            BtcReplayIcons.Glyph(BtcReplayIcons.Search, new Rect(sr.x + fs * 0.3f, sr.y + fs * 0.3f, fs * 1.4f, fs * 1.4f), new Color(1f, 1f, 1f, 0.5f));
            Rect tr = new Rect(sr.x + fs * 1.8f, sr.y, sr.width - fs * 1.8f, sr.height);
            BtcUi.Field(sr);
            GUI.SetNextControlName("btccl_q");
            string nq = GUI.TextField(tr, _q, 40, _tf);
            if (nq != _q) { _q = nq; _scroll.y = 0f; }
            if (_q.Length == 0 && GUI.GetNameOfFocusedControl() != "btccl_q") GUI.Label(tr, T("Search changes", "Поиск по изменениям"), _dim);
            float x = sr.xMax + fs * 0.8f;
            string kinds = "+~*=-";
            for (int i = 0; i < kinds.Length; i++)
            {
                char k = kinds[i];
                bool on = _kinds.IndexOf(k) >= 0;
                Rect r = new Rect(x, y, fs * 2f, fs * 2f);
                bool hov = r.Contains(e.mousePosition);
                Color kc = KindColor(k);
                BtcReplayUI.Fill(r, on ? new Color(kc.r, kc.g, kc.b, 0.18f) : new Color(1f, 1f, 1f, hov ? 0.1f : 0.04f));
                BtcReplayIcons.Glyph(KindIcon(k), new Rect(r.x + fs * 0.35f, r.y + fs * 0.35f, fs * 1.3f, fs * 1.3f), on ? kc : new Color(1f, 1f, 1f, 0.3f));
                BtcReplayIcons.Hint(r, (on ? T("Hide: ", "Скрыть: ") : T("Show: ", "Показать: ")) + KindName(k));
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn))
                {
                    string old = _kinds;
                    _kinds = on ? _kinds.Replace(k.ToString(), "") : _kinds + k;
                    if (_kinds.Length == 0) _kinds = old;
                    string prev = old;
                    BtcUndo.Push(Scope, T("filter", "фильтр"), delegate { _kinds = prev; });
                }
                x += r.width + fs * 0.35f;
            }
            // grouping: by type of change (then place) or by place (then type)
            x += fs * 0.6f;
            for (int g = 0; g < 2; g++)
            {
                Rect gr = new Rect(x, y, fs * 2f, fs * 2f);
                bool gon = GroupMode == g, ghov = gr.Contains(e.mousePosition);
                BtcReplayUI.Fill(gr, gon ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.22f) : new Color(1f, 1f, 1f, ghov ? 0.1f : 0.04f));
                BtcReplayIcons.Glyph(g == 0 ? BtcReplayIcons.List : BtcReplayIcons.Grid, new Rect(gr.x + fs * 0.35f, gr.y + fs * 0.35f, fs * 1.3f, fs * 1.3f), gon ? BtcMenu.Accent : new Color(1f, 1f, 1f, ghov ? 0.9f : 0.5f));
                BtcReplayIcons.Hint(gr, g == 0 ? T("Group by type of change, then by place", "Группировать по типу изменения, затем по месту") : T("Group by place (replays, settings...), then by type", "Группировать по месту (реплеи, настройки...), затем по типу"));
                if (BtcSfx.Btn(gr, "", BtcReplayUI.sBtn) && !gon) { int pg = GroupMode; GroupMode = g; BtcUndo.Push(Scope, T("grouping", "группировка"), delegate { GroupMode = pg; }); }
                x += gr.width + fs * 0.35f;
            }
            y += fs * 2.5f;

            // area chips (only areas that exist in the log)
            List<string> areas = new List<string>();
            for (int i = 0; i < rel.Count; i++) for (int j = 0; j < rel[i].Entries.Count; j++) if (!areas.Contains(rel[i].Entries[j].Area)) areas.Add(rel[i].Entries[j].Area);
            float cx = pad, ch = fs * 1.7f;
            for (int i = -1; i < areas.Count; i++)
            {
                string key = i < 0 ? "" : areas[i];
                string name = i < 0 ? T("All", "Все") : AreaName(key);
                float cw = _chip.CalcSize(new GUIContent(name)).x + fs * 1.3f;
                if (cx + cw > w - pad && cx > pad) { cx = pad; y += ch + fs * 0.3f; }
                Rect r = new Rect(cx, y, cw, ch);
                bool on = _area == key, hov = r.Contains(e.mousePosition);
                BtcReplayUI.Fill(r, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.2f) : new Color(1f, 1f, 1f, hov ? 0.09f : 0.04f));
                if (on) BtcReplayUI.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), BtcMenu.Accent);
                Color oc = GUI.contentColor; GUI.contentColor = on ? Color.white : new Color(1f, 1f, 1f, 0.7f);
                GUI.Label(r, name, _chip);
                GUI.contentColor = oc;
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn) && !on) { string prev = _area; _area = key; BtcUndo.Push(Scope, T("filter", "фильтр"), delegate { _area = prev; }); }
                cx += cw + fs * 0.3f;
            }
            y += ch + fs * 0.9f;

            if (rel.Count == 0) { BtcUi.Note(ref y, w, fs, T("The changelog is empty.", "Список изменений пуст.")); return; }

            int shown = 0;
            for (int ri = 0; ri < rel.Count; ri++)
            {
                BtcChangelog.Release r = rel[ri];
                List<BtcChangelog.Entry> list = new List<BtcChangelog.Entry>();
                for (int i = 0; i < r.Entries.Count; i++) if (Matches(r.Entries[i])) list.Add(r.Entries[i]);
                if (list.Count == 0) continue;
                shown += list.Count;
                bool filtering = _q.Length > 0 || _area.Length > 0 || _kinds.Length < 5;
                bool open;
                if (!_open.TryGetValue(r.Version, out open)) open = ri < 2;
                if (filtering) open = true;
                if (ReleaseHeader(ref y, w, fs, r, list, ref open, filtering)) _open[r.Version] = open;
                if (!open) continue;
                DrawGroups(ref y, w, fs, list);
                y += fs * 0.8f;
            }
            if (shown == 0) BtcUi.Note(ref y, w, fs, T("Nothing matches the filters.", "По фильтрам ничего не найдено."));
        }

        private static int GroupMode { get { return BtcMenuState.Int("changelog:group", 0); } set { BtcMenuState.SetInt("changelog:group", value); } }

        private static readonly string KindOrder = "+~*=-";

        private static int AreaIcon(string key)
        {
            switch (key)
            {
                case "replay": return BtcReplayIcons.Play;
                case "hotkeys": return BtcReplayIcons.Keys;
                case "visuals": return BtcReplayIcons.Eye;
                case "gameplay": return BtcReplayIcons.Swords;
                case "multiplayer": return BtcReplayIcons.Group;
                case "duel": return BtcReplayIcons.Target;
                case "packs": return BtcReplayIcons.Blueprint;
                case "menu": return BtcReplayIcons.Layout;
            }
            return BtcReplayIcons.Gear;
        }

        /// <summary>Entries of one release in two levels: type of change then place (or place then type), each level with a header, icon and count.</summary>
        private void DrawGroups(ref float y, float w, int fs, List<BtcChangelog.Entry> list)
        {
            bool byKind = GroupMode == 0;
            List<string> prim = new List<string>();
            if (byKind) { for (int i = 0; i < KindOrder.Length; i++) prim.Add(KindOrder[i].ToString()); }
            else { for (int i = 0; i < BtcReportData.AreaKeys.Length; i++) prim.Add(BtcReportData.AreaKeys[i]); }
            for (int p = 0; p < prim.Count; p++)
            {
                List<BtcChangelog.Entry> sub = new List<BtcChangelog.Entry>();
                for (int i = 0; i < list.Count; i++) if ((byKind ? list[i].Kind.ToString() : list[i].Area) == prim[p]) sub.Add(list[i]);
                if (sub.Count == 0) continue;
                char pk = byKind ? prim[p][0] : '\0';
                Color pc = byKind ? KindColor(pk) : BtcMenu.Accent;
                Header(ref y, w, fs, byKind ? KindIcon(pk) : AreaIcon(prim[p]), byKind ? KindName(pk) : AreaName(prim[p]), sub.Count, pc, 0);
                List<string> sec = new List<string>();
                if (byKind) { for (int i = 0; i < BtcReportData.AreaKeys.Length; i++) sec.Add(BtcReportData.AreaKeys[i]); }
                else { for (int i = 0; i < KindOrder.Length; i++) sec.Add(KindOrder[i].ToString()); }
                for (int s = 0; s < sec.Count; s++)
                {
                    List<BtcChangelog.Entry> rows = new List<BtcChangelog.Entry>();
                    for (int i = 0; i < sub.Count; i++) if ((byKind ? sub[i].Area : sub[i].Kind.ToString()) == sec[s]) rows.Add(sub[i]);
                    if (rows.Count == 0) continue;
                    if (byKind) Header(ref y, w, fs, AreaIcon(sec[s]), AreaName(sec[s]), rows.Count, new Color(1f, 1f, 1f, 0.7f), 1);
                    else Header(ref y, w, fs, KindIcon(sec[s][0]), KindName(sec[s][0]), rows.Count, KindColor(sec[s][0]), 1);
                    for (int i = 0; i < rows.Count; i++) Row(ref y, w, fs, rows[i]);
                }
                y += fs * 0.4f;
            }
        }

        private void Header(ref float y, float w, int fs, int icon, string name, int count, Color c, int level)
        {
            float pad = fs + level * fs * 1.2f;
            float h = level == 0 ? fs * 2.1f : fs * 1.6f;
            Rect r = new Rect(pad, y + (level == 0 ? fs * 0.2f : 0f), w - pad - fs, h);
            if (level == 0) BtcReplayUI.Fill(r, new Color(c.r, c.g, c.b, 0.1f));
            float g = level == 0 ? fs * 1.3f : fs * 1.0f;
            BtcReplayIcons.Glyph(icon, new Rect(r.x + fs * 0.5f, r.center.y - g * 0.5f, g, g), c);
            Color oc = GUI.contentColor;
            GUI.contentColor = level == 0 ? Color.white : new Color(1f, 1f, 1f, 0.7f);
            GUIStyle st = level == 0 ? _lbl : _dim;
            GUI.Label(new Rect(r.x + fs * 0.5f + g + fs * 0.6f, r.y, r.width * 0.6f, h), level == 0 ? name.ToUpperInvariant() : name, st);
            GUI.contentColor = new Color(1f, 1f, 1f, 0.5f);
            GUIStyle rs = new GUIStyle(_dim); rs.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(r.x, r.y, r.width - fs * 0.6f, h), count.ToString(), rs);
            GUI.contentColor = oc;
            y += h + (level == 0 ? fs * 0.5f : fs * 0.15f);
        }

        private bool ReleaseHeader(ref float y, float w, int fs, BtcChangelog.Release r, List<BtcChangelog.Entry> list, ref bool open, bool locked)
        {
            Event e = Event.current;
            float pad = fs;
            Rect row = new Rect(pad, y, w - pad * 2f, fs * 2.8f);
            bool hov = row.Contains(e.mousePosition);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, hov ? 0.07f : 0.035f));
            BtcReplayIcons.Glyph(open ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, new Rect(row.x + fs * 0.4f, row.center.y - fs * 0.5f, fs * 1.4f, fs), BtcMenu.Accent);
            string ver = "v" + r.Version + (string.IsNullOrEmpty(r.Title) ? "" : "  " + r.Title);
            float vw = _big.CalcSize(new GUIContent(ver)).x;
            GUI.Label(new Rect(row.x + fs * 2.2f, row.y, vw + 4f, row.height), ver, _big);
            float lx = row.x + fs * 2.2f + vw + fs * 0.8f;
            if (r.Label.Length > 0)
            {
                float lw = _chip.CalcSize(new GUIContent(r.Label)).x + fs * 1.0f;
                Rect lr = new Rect(lx, row.center.y - fs * 0.7f, lw, fs * 1.4f);
                BtcReplayUI.Fill(lr, new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.22f));
                Color oc = GUI.contentColor; GUI.contentColor = BtcMenu.Accent;
                GUI.Label(lr, r.Label, _chip);
                GUI.contentColor = oc;
                lx += lw + fs * 0.7f;
            }
            if (r.Date.Length > 0) { GUI.Label(new Rect(lx, row.y, fs * 8f, row.height), r.Date, _dim); }
            // counters per kind, right side
            float rx = row.xMax - fs * 0.6f;
            string kinds = "-=*~+";
            for (int i = 0; i < kinds.Length; i++)
            {
                int n = 0;
                for (int j = 0; j < list.Count; j++) if (list[j].Kind == kinds[i]) n++;
                if (n == 0) continue;
                float cw = fs * 3.2f;
                rx -= cw;
                Color kc = KindColor(kinds[i]);
                BtcReplayIcons.Glyph(KindIcon(kinds[i]), new Rect(rx, row.center.y - fs * 0.5f, fs, fs), kc);
                Color oc = GUI.contentColor; GUI.contentColor = new Color(1f, 1f, 1f, 0.75f);
                GUI.Label(new Rect(rx + fs * 1.2f, row.y, fs * 2f, row.height), n.ToString(), _dim);
                GUI.contentColor = oc;
            }
            y += row.height + fs * 0.3f;
            if (locked) return false;
            if (BtcSfx.Btn(row, "", BtcReplayUI.sBtn)) { open = !open; BtcSfx.Switch(open); return true; }
            return false;
        }

        private void Row(ref float y, float w, int fs, BtcChangelog.Entry en)
        {
            Event e = Event.current;
            float pad = fs;
            string text = T(en.En, en.Ru);
            string dest = BtcChangelog.TargetName(en.Target);
            bool link = dest != null;
            float tagW = 0f, chipW = link ? Mathf.Min(fs * 12f, _chip.CalcSize(new GUIContent(dest)).x + fs * 3f) : 0f;
            float textX = pad + fs * 2.6f, textW = w - pad * 2f - fs * 2.6f - chipW - fs * 0.8f;
            float th = Mathf.Max(fs * 1.6f, _wrap.CalcHeight(new GUIContent(text), textW));
            float rowH = th + fs * 0.9f;
            Rect row = new Rect(pad, y, w - pad * 2f, rowH);
            bool hov = row.Contains(e.mousePosition);
            if (hov && link) BtcReplayUI.Fill(row, new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.1f));
            else if (hov) BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.04f));
            Color kc = KindColor(en.Kind);
            BtcReplayUI.Fill(new Rect(row.x + fs * 1.2f, row.y + fs * 0.9f, fs * 0.4f, fs * 0.4f), new Color(kc.r, kc.g, kc.b, 0.9f));
            Color oc = GUI.contentColor;
            GUI.contentColor = new Color(1f, 1f, 1f, 0.45f);
            GUI.contentColor = Color.white;
            GUI.Label(new Rect(textX, row.y + fs * 0.45f, textW, th), text, _wrap);
            GUI.contentColor = oc;
            if (link)
            {
                Rect cr = new Rect(row.xMax - chipW - fs * 0.4f, row.y + (rowH - fs * 1.7f) * 0.5f, chipW, fs * 1.7f);
                BtcReplayUI.Fill(cr, new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, hov ? 0.32f : 0.16f));
                Color c2 = GUI.contentColor; GUI.contentColor = BtcMenu.Accent;
                GUI.Label(new Rect(cr.x, cr.y, cr.width - fs * 1.2f, cr.height), dest, _chip);
                GUI.contentColor = c2;
                BtcReplayIcons.Glyph(BtcReplayIcons.ChevR, new Rect(cr.xMax - fs * 1.4f, cr.center.y - fs * 0.4f, fs * 0.9f, fs * 0.8f), BtcMenu.Accent);
                BtcReplayIcons.Hint(row, T("Click to open: ", "Клик, чтобы открыть: ") + dest);
                if (BtcSfx.Btn(row, "", BtcReplayUI.sBtn))
                {
                    string target = en.Target;
                    if (BtcChangelog.Go(target) && !target.StartsWith("url:", StringComparison.Ordinal)) BtcMenu.Toast(T("Opened: ", "Открыто: ") + dest, BtcReplayIcons.News);
                }
            }
            y += rowH + 2f;
        }
    }
}
