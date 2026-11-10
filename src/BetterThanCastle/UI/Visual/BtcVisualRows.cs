using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Row widgets of the Visuals tab. Two ways to use them: (1) directly with a BtcVisualSetting (registry settings of the colored-HP config),
    /// (2) through the legacy callbacks (check / step / slider / colour) that the older feature classes (BtcOverlays, BtcBlockMarks,
    /// BtcMatchStatsUI, BtcUnitPlates, BtcInterface, BtcGameChatFade) still use to draw their own settings: the callbacks below render those
    /// rows in the new style, so the features keep their code and cfg keys. Undo for callback rows replays a click / the old value on the next draw.
    /// </summary>
    internal static class BtcVisualRows
    {
        internal const string Scope = "visual";

        /// <summary>Expert mode shows sliders, colours and every sub-option; Simple keeps switches and choices only.</summary>
        internal static bool Expert;
        /// <summary>Unique prefix of the callback rows being drawn (feature + page), used for undo keys.</summary>
        internal static string Prefix = "";
        /// <summary>Page index the current rows belong to (undo of a callback row switches back to it).</summary>
        internal static int PageId;
        internal static Action<int> GoPage;
        internal static bool IgnoreVis;
        internal static Func<int, Color> ColorGet;
        internal static Action<int, Color> ColorSet;

        private static GUIStyle _lbl, _dim, _head, _chip;
        private static int _fs;
        private static string _dragId;

        // ---- pending "replayed" undo of callback rows
        private static readonly Dictionary<string, bool> ForceClick = new Dictionary<string, bool>();
        private static readonly Dictionary<string, float> ForceVal = new Dictionary<string, float>();
        private static readonly Dictionary<string, int> ForceAt = new Dictionary<string, int>();

        internal static void Styles(int fs)
        {
            BtcReplayUI.Ensure(fs);
            if (_head != null) _head.normal.textColor = BtcMenu.Accent;
            if (_lbl != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _lbl = new GUIStyle(b); _lbl.fontSize = Mathf.RoundToInt(fs * 0.95f); _lbl.alignment = TextAnchor.MiddleLeft; _lbl.clipping = TextClipping.Clip; _lbl.normal.textColor = Color.white;
            _dim = new GUIStyle(_lbl); _dim.fontSize = Mathf.RoundToInt(fs * 0.78f); _dim.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            _head = new GUIStyle(_lbl); _head.fontSize = Mathf.RoundToInt(fs * 0.8f); _head.fontStyle = FontStyle.Bold; _head.normal.textColor = BtcMenu.Accent;
            _chip = new GUIStyle(_lbl); _chip.fontSize = Mathf.RoundToInt(fs * 0.85f); _chip.alignment = TextAnchor.MiddleCenter;
        }

        internal static GUIStyle Lbl { get { return _lbl; } }
        internal static GUIStyle Dim { get { return _dim; } }

        internal static void Txt(Rect r, string s, GUIStyle st, Color c)
        {
            Color old = GUI.contentColor;
            GUI.contentColor = c;
            GUI.Label(r, s, st);
            GUI.contentColor = old;
        }

        private static string Tr(string s) { return BtcLang.Tr(s); }

        private static void Depth(string label, out int level, out string text)
        {
            int n = 0;
            while (n < label.Length && label[n] == ' ') n++;
            level = Mathf.Min(3, n / 3);
            text = label.Substring(n);
        }

        internal static void Gap(ref float y, int fs) { y += fs * 0.5f; }

        // ------------------------------------------------------------------ favourites (stars)
        private static readonly List<string> FavList = new List<string>();
        private static bool _favLoaded;
        private static string FavFile { get { return BtcPaths.Config("visual-ui.txt"); } }

        /// <summary>Starred keys in the order they were added: registry setting keys, "pal:&lt;key&gt;" palettes, "vis:&lt;page&gt;:&lt;section&gt;" folds.</summary>
        internal static IList<string> Favs { get { LoadFavs(); return FavList; } }

        private static void LoadFavs()
        {
            if (_favLoaded) return;
            _favLoaded = true;
            try
            {
                if (!System.IO.File.Exists(FavFile)) return;
                string[] l = System.IO.File.ReadAllLines(FavFile, System.Text.Encoding.UTF8);
                for (int i = 0; i < l.Length; i++) if (l[i].StartsWith("fav=") && !FavList.Contains(l[i].Substring(4))) FavList.Add(l[i].Substring(4));
            }
            catch (Exception) { }
        }

        private static void SaveFavs()
        {
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < FavList.Count; i++) sb.Append("fav=").Append(FavList[i]).Append("\r\n");
                System.IO.File.WriteAllText(FavFile, sb.ToString(), new System.Text.UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        internal static bool IsFav(string key) { LoadFavs(); return FavList.Contains(key); }

        internal static void SetFav(string key, bool on)
        {
            LoadFavs();
            if (on) { if (!FavList.Contains(key)) FavList.Add(key); } else FavList.Remove(key);
            SaveFavs();
        }

        /// <summary>Star button (a faint star, accent when starred). Draw it BEFORE the row it belongs to (the earlier control gets the click). Records undo.</summary>
        internal static void Star(Rect r, string key, string label)
        {
            bool fav = IsFav(key);
            bool hover = r.Contains(Event.current.mousePosition);
            Color c = fav ? new Color(1f, 0.85f, 0.4f, 1f) : new Color(1f, 1f, 1f, hover ? 0.7f : 0.22f);
            BtcReplayIcons.Glyph(BtcReplayIcons.Star, r, c);
            BtcReplayIcons.Hint(r, fav ? BtcReplayUI.T("Remove from favourites", "Убрать из избранного") : BtcReplayUI.T("Add to favourites", "В избранное"));
            if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn))
            {
                SetFav(key, !fav);
                BtcSfx.Switch(!fav);
                bool was = fav;
                BtcUndo.Push(Scope, (was ? BtcReplayUI.T("Favourite removed: ", "Убрано из избранного: ") : BtcReplayUI.T("Favourite added: ", "В избранное: ")) + label, delegate { SetFav(key, was); });
            }
        }

        /// <summary>Gutter rect for the star of a row that starts at y (the rows are drawn narrower by GutterW).</summary>
        internal static float GutterW(int fs) { return fs * 2.2f; }

        internal static Rect StarRect(float y, float w, int fs)
        {
            float sz = fs * 1.5f;
            return new Rect(w - fs * 2.6f, y + (fs * 2.1f - sz) * 0.5f, sz, sz);
        }

        // ------------------------------------------------------------------ folds (with an icon)
        /// <summary>Foldable section header with a pixel icon. Returns true when open.</summary>
        internal static bool Fold(ref float y, float w, int fs, string key, int icon, string text, string right)
        {
            Styles(fs);
            bool open = BtcUi.IsOpen(key);
            float pad = fs;
            Rect r = new Rect(pad, y, w - pad * 2f, fs * 1.9f);
            bool hover = r.Contains(Event.current.mousePosition);
            if (hover) BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, 0.04f));
            float g = fs * 1.0f;
            BtcReplayIcons.Glyph(open ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, new Rect(r.x, r.center.y - g * 0.5f, g * 1.4f, g), hover ? Color.white : BtcMenu.Accent);
            float ig = fs * 1.35f;
            BtcReplayIcons.Glyph(icon, new Rect(r.x + fs * 1.6f, r.center.y - ig * 0.5f, ig, ig), hover ? Color.white : BtcMenu.Accent);
            Txt(new Rect(r.x + fs * 3.4f, r.y, r.width - fs * 3.4f, r.height), text.ToUpperInvariant(), _head, BtcMenu.Accent);
            if (!string.IsNullOrEmpty(right))
            {
                GUIStyle rs = new GUIStyle(_dim); rs.alignment = TextAnchor.MiddleRight;
                Txt(new Rect(r.x, r.y, r.width - fs * 2.6f, r.height), right, rs, new Color(1f, 1f, 1f, 0.45f));
            }
            BtcReplayUI.Fill(new Rect(r.x, r.yMax, r.width, 1f), BtcMenu.Line);
            y += fs * 2.2f;
            Star(new Rect(r.xMax - fs * 1.9f, r.center.y - fs * 0.75f, fs * 1.5f, fs * 1.5f), key, text);
            if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) { BtcUi.SetOpen(key, !open); open = !open; BtcSfx.Switch(open); }
            return open;
        }

        // ------------------------------------------------------------------ switch
        /// <summary>Switch row. indent = nesting level (0..3). Returns true when clicked.</summary>
        internal static bool Switch(ref float y, float w, int fs, int indent, string label, bool on, string hint)
        {
            Styles(fs);
            Event e = Event.current;
            float pad = fs + indent * fs * 1.2f;
            float h = fs * 2.1f + (string.IsNullOrEmpty(hint) ? 0f : fs * 1.1f);
            Rect row = new Rect(pad, y, w - pad - fs, h);
            bool hover = row.Contains(e.mousePosition);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, hover ? 0.07f : 0.03f));
            if (indent > 0) BtcReplayUI.Fill(new Rect(row.x - fs * 0.6f, row.y, 1f, row.height), BtcMenu.Line);
            float sw = fs * 2.5f, sh = fs * 1.3f;
            Rect tr = new Rect(row.x + fs * 0.6f, row.y + (fs * 2.1f - sh) * 0.5f, sw, sh);
            BtcReplayUI.Fill(tr, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.9f) : new Color(1f, 1f, 1f, 0.16f));
            float k = sh - 4f;
            BtcReplayUI.Fill(new Rect(on ? tr.xMax - k - 2f : tr.x + 2f, tr.y + 2f, k, k), on ? new Color(0.03f, 0.05f, 0.04f, 1f) : new Color(1f, 1f, 1f, 0.75f));
            Txt(new Rect(tr.xMax + fs * 0.8f, row.y, row.width - sw - fs * 2.2f, fs * 2.1f), label, _lbl, on ? Color.white : new Color(1f, 1f, 1f, 0.78f));
            if (!string.IsNullOrEmpty(hint)) Txt(new Rect(tr.xMax + fs * 0.8f, row.y + fs * 1.65f, row.width - sw - fs * 2.2f, fs * 1.1f), hint, _dim, new Color(1f, 1f, 1f, 0.5f));
            y += h + 3f;
            bool clicked = BtcSfx.Btn(row, "", BtcReplayUI.sBtn);
            if (clicked) BtcSfx.Switch(!on);
            return clicked;
        }

        // ------------------------------------------------------------------ slider
        private static bool StepBtn(Rect r, bool plus)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, hover ? 0.18f : 0.08f));
            Color c = hover ? Color.white : new Color(1f, 1f, 1f, 0.7f);
            BtcReplayUI.Fill(new Rect(r.x + r.width * 0.28f, r.center.y - 1f, r.width * 0.44f, 2f), c);
            if (plus) BtcReplayUI.Fill(new Rect(r.center.x - 1f, r.y + r.height * 0.28f, 2f, r.height * 0.44f), c);
            return BtcSfx.Btn(r, "", BtcReplayUI.sBtn);
        }

        private static float Snap(float v, float step, float min) { return step > 0f ? min + Mathf.Round((v - min) / step) * step : v; }

        /// <summary>Slider row: label, [-] track [+] value. Returns the new value (== value when unchanged).</summary>
        internal static float Slider(ref float y, float w, int fs, int indent, string id, string label, float value, float min, float max, float step, string fmt, string suffix)
        {
            Styles(fs);
            Event e = Event.current;
            float pad = fs + indent * fs * 1.2f;
            float h = fs * 2.1f;
            Rect row = new Rect(pad, y, w - pad - fs, h);
            bool hover = row.Contains(e.mousePosition);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, hover || _dragId == id ? 0.07f : 0.03f));
            if (indent > 0) BtcReplayUI.Fill(new Rect(row.x - fs * 0.6f, row.y, 1f, row.height), BtcMenu.Line);
            Txt(new Rect(row.x + fs * 0.6f, row.y, row.width * 0.38f, h), label, _lbl, new Color(1f, 1f, 1f, 0.85f));
            float b = h * 0.62f, by = row.y + (h - b) * 0.5f;
            float tx = row.x + row.width * 0.42f + b + 4f, vx = row.xMax - fs * 6f;
            Rect minus = new Rect(tx - b - 4f, by, b, b), plus = new Rect(vx - b - 2f, by, b, b);
            Rect track = new Rect(tx + 2f, row.center.y - 2f, Mathf.Max(20f, plus.x - tx - 8f), 4f);
            float nv = value;
            if (StepBtn(minus, false)) nv = Mathf.Clamp(Snap(value - (step > 0f ? step : (max - min) / 20f), step, min), min, max);
            if (StepBtn(plus, true)) nv = Mathf.Clamp(Snap(value + (step > 0f ? step : (max - min) / 20f), step, min), min, max);
            Rect hit = new Rect(track.x - 6f, row.y, track.width + 12f, h);
            bool hov = hit.Contains(e.mousePosition) || _dragId == id;
            BtcReplayUI.Fill(track, new Color(1f, 1f, 1f, 0.12f));
            float f = Mathf.Clamp01((value - min) / Mathf.Max(0.0001f, max - min));
            BtcReplayUI.Fill(new Rect(track.x, track.y, track.width * f, track.height), new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, hov ? 1f : 0.8f));
            float kw = fs * 0.55f, kh = fs * 1.3f, kx = Mathf.Clamp(track.x + track.width * f - kw * 0.5f, track.x, track.xMax - kw);
            if (hov) BtcReplayUI.Fill(new Rect(kx - 2f, track.center.y - kh * 0.5f - 2f, kw + 4f, kh + 4f), new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.25f));
            BtcReplayUI.Fill(new Rect(kx, track.center.y - kh * 0.5f, kw, kh), hov ? Color.white : new Color(1f, 1f, 1f, 0.85f));
            if (e.rawType == EventType.MouseUp) _dragId = null;
            if (e.type == EventType.MouseDown && hit.Contains(e.mousePosition) && e.button == 0) _dragId = id;
            if (_dragId == id && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                float t = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                float v = min + t * (max - min);
                if (step > 0f) v = Mathf.Round((v - min) / step) * step + min;
                nv = Mathf.Clamp(v, min, max);
                e.Use();
            }
            GUIStyle vs = new GUIStyle(_lbl); vs.alignment = TextAnchor.MiddleRight;
            Txt(new Rect(vx, row.y, fs * 5.6f, h), nv.ToString(fmt, CultureInfo.InvariantCulture) + suffix, vs, Color.white);
            y += h + 3f;
            return nv;
        }

        // ------------------------------------------------------------------ choice (icon buttons or text chips)
        /// <summary>Row with a label and N options on the right (icons when icons != null, else text chips). Returns the clicked index or -1.</summary>
        internal static int Choice(ref float y, float w, int fs, int indent, string label, int sel, string[] texts, int[] icons, string[] tips)
        {
            Styles(fs);
            Event e = Event.current;
            float pad = fs + indent * fs * 1.2f;
            float h = fs * 2.1f;
            Rect row = new Rect(pad, y, w - pad - fs, h);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.03f));
            Txt(new Rect(row.x + fs * 0.6f, row.y, row.width * 0.45f, h), label, _lbl, new Color(1f, 1f, 1f, 0.85f));
            int n = icons != null ? icons.Length : texts.Length;
            float bw = icons != null ? h * 0.8f : Mathf.Min(fs * 7f, (row.width * 0.5f) / n), x = row.xMax - fs * 0.5f - bw * n - 3f * (n - 1);
            int clicked = -1;
            for (int i = 0; i < n; i++)
            {
                Rect r = new Rect(x + i * (bw + 3f), row.y + h * 0.1f, bw, h * 0.8f);
                bool on = i == sel, hv = r.Contains(e.mousePosition);
                BtcReplayUI.Fill(r, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.22f) : new Color(1f, 1f, 1f, hv ? 0.14f : 0.07f));
                if (icons != null) BtcReplayIcons.Glyph(icons[i], r, on ? BtcMenu.Accent : new Color(1f, 1f, 1f, hv ? 0.95f : 0.6f));
                else Txt(r, texts[i], _chip, on ? BtcMenu.Accent : new Color(1f, 1f, 1f, hv ? 0.95f : 0.7f));
                if (tips != null && i < tips.Length) BtcReplayIcons.Hint(r, tips[i]);
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) clicked = i;
            }
            y += h + 3f;
            return clicked;
        }

        // ------------------------------------------------------------------ registry settings
        /// <summary>One registry setting (switch / slider / colour) with undo. Returns false when it is hidden in the current mode.</summary>
        internal static bool Setting(ref float y, float w, int fs, BtcVisualSetting s)
        {
            if (!IgnoreVis && s.Vis != null && !s.Vis()) return false;
            if (s.Expert && !Expert) return false;
            string label = s.Label;
            Star(StarRect(y, w, fs), s.Key, label);
            w -= GutterW(fs);
            switch (s.Kind)
            {
                case VKind.Bool:
                    {
                        bool v = s.GetB();
                        if (Switch(ref y, w, fs, s.Indent, label, v, null))
                        {
                            s.SetB(!v);
                            Action<bool> set = s.SetB;
                            BtcUndo.Push(Scope, label, delegate { set(v); });
                        }
                        break;
                    }
                case VKind.Float:
                    {
                        float v = s.GetF();
                        float nv = Slider(ref y, w, fs, s.Indent, s.Key, label, v, s.Min, s.Max, s.Step, s.Fmt, s.Suf);
                        if (!Mathf.Approximately(nv, v))
                        {
                            s.SetF(nv);
                            Action<float> set = s.SetF;
                            BtcUndo.Push(Scope, label, delegate { set(v); }, s.Key);
                        }
                        break;
                    }
                case VKind.Color:
                    BtcVisualColor.Row(ref y, w, fs, s.Key, label, s.Indent, s.GetC(), s.SetC, null);
                    break;
                case VKind.Choice:
                    {
                        int v = s.GetI();
                        int c = Choice(ref y, w, fs, s.Indent, label, v, s.Choices, null, null);
                        if (c >= 0 && c != v)
                        {
                            s.SetI(c);
                            Action<int> set = s.SetI;
                            BtcUndo.Push(Scope, label, delegate { set(v); });
                        }
                        break;
                    }
            }
            return true;
        }

        // ------------------------------------------------------------------ legacy callbacks (new look)
        private static bool Forced(string key)
        {
            bool v;
            if (ForceClick.TryGetValue(key, out v)) { ForceClick.Remove(key); ForceAt.Remove(key); return true; }
            return false;
        }

        /// <summary>Called every frame: drops replayed undo requests that never found their row (hidden by a parent switch).</summary>
        internal static void ExpireForced()
        {
            if (ForceAt.Count == 0) return;
            List<string> dead = null;
            foreach (KeyValuePair<string, int> kv in ForceAt) if (Time.frameCount - kv.Value > 6) { if (dead == null) dead = new List<string>(); dead.Add(kv.Key); }
            if (dead == null) return;
            for (int i = 0; i < dead.Count; i++) { ForceClick.Remove(dead[i]); ForceVal.Remove(dead[i]); ForceAt.Remove(dead[i]); }
            BtcMenu.Toast(BtcReplayUI.T("That step could not be undone (the option is hidden now)", "Шаг не удалось отменить (опция сейчас скрыта)"), -1);
        }

        private static void Request(string key, int page)
        {
            ForceClick[key] = true; ForceAt[key] = Time.frameCount;
            if (GoPage != null) GoPage(page);
        }

        /// <summary>Callback "check": switch row, true = the feature should flip its value.</summary>
        internal static bool Check(ref float y, float w, int fs, string label, bool on)
        {
            int lvl; string text;
            Depth(Tr(label), out lvl, out text);
            string key = Prefix + "|c|" + label;
            bool forced = Forced(key);
            bool clicked = Switch(ref y, w, fs, lvl, text, on, null);
            if (forced) return true;
            if (clicked)
            {
                int page = PageId;
                BtcUndo.Push(Scope, text, delegate { Request(key, page); });
            }
            return clicked;
        }

        /// <summary>Callback "step": a cycling button, shown as label + current value chip. label is "name: value".</summary>
        internal static bool Step(ref float y, float w, int fs, string label)
        {
            Styles(fs);
            int lvl; string text;
            Depth(Tr(label), out lvl, out text);
            string name = text, val = "";
            int ix = text.LastIndexOf(": ", StringComparison.Ordinal);
            if (ix > 0) { name = text.Substring(0, ix); val = text.Substring(ix + 2); }
            Event e = Event.current;
            float pad = fs + lvl * fs * 1.2f;
            float h = fs * 2.1f;
            Rect row = new Rect(pad, y, w - pad - fs, h);
            bool hover = row.Contains(e.mousePosition);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, hover ? 0.07f : 0.03f));
            if (lvl > 0) BtcReplayUI.Fill(new Rect(row.x - fs * 0.6f, row.y, 1f, row.height), BtcMenu.Line);
            Txt(new Rect(row.x + fs * 0.6f, row.y, row.width * 0.6f, h), name, _lbl, new Color(1f, 1f, 1f, 0.85f));
            float cw = Mathf.Min(fs * 10f, row.width * 0.35f);
            Rect chip = new Rect(row.xMax - cw - fs * 0.5f, row.y + h * 0.12f, cw, h * 0.76f);
            BtcReplayUI.Fill(chip, new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, hover ? 0.28f : 0.18f));
            Txt(chip, val, _chip, BtcMenu.Accent);
            BtcReplayIcons.Hint(row, BtcReplayUI.T("Click to change", "Клик - сменить"));
            y += h + 3f;
            // no undo step: a cycling button (n states) cannot be reverted by replaying one click
            return BtcSfx.Btn(row, "", BtcReplayUI.sBtn);
        }

        /// <summary>Callback "slider".</summary>
        internal static float SliderCb(ref float y, float w, int fs, string label, int id, float v, float mn, float mx, float st, string fmt, string suf)
        {
            if (!Expert) return v;
            int lvl; string text;
            Depth(Tr(label), out lvl, out text);
            string key = Prefix + "|f|" + label;
            float fv;
            if (ForceVal.TryGetValue(key, out fv)) { ForceVal.Remove(key); ForceAt.Remove(key); y += fs * 2.1f + 3f; return fv; }
            float nv = Slider(ref y, w, fs, lvl, Prefix + id, text, v, mn, mx, st, fmt, suf);
            if (!Mathf.Approximately(nv, v))
            {
                int page = PageId;
                float old = v;
                BtcUndo.Push(Scope, text, delegate { ForceVal[key] = old; ForceAt[key] = Time.frameCount; if (GoPage != null) GoPage(page); }, key);
            }
            return nv;
        }

        /// <summary>Callback "colour".</summary>
        internal static void ColorCb(ref float y, float w, int fs, string label, int id)
        {
            if (!Expert || ColorGet == null) return;
            int lvl; string text;
            Depth(Tr(label), out lvl, out text);
            int cid = id;
            BtcVisualColor.Row(ref y, w, fs, "cb" + id, text, lvl, ColorGet(id), delegate (Color c) { ColorSet(cid, c); }, null);
        }
    }
}
