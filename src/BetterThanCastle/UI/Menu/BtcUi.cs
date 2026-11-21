using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Shared widgets of the new menu look (flat dark rows, accent switches, thin lines): used by every migrated tab instead of the old checkbox
    /// rows. All of them advance a y cursor and take the row width, so a tab is a column of calls. Font = the replay bar font.
    /// </summary>
    internal static class BtcUi
    {
        private static GUIStyle _label, _dim, _wrap, _head;
        private static int _fs;

        private static void Styles(int fs)
        {
            if (_label != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _label = new GUIStyle(b); _label.fontSize = Mathf.RoundToInt(fs * 0.95f); _label.alignment = TextAnchor.MiddleLeft; _label.normal.textColor = Color.white; _label.clipping = TextClipping.Clip;
            _dim = new GUIStyle(_label); _dim.fontSize = Mathf.RoundToInt(fs * 0.78f); _dim.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            _wrap = new GUIStyle(_dim); _wrap.wordWrap = true; _wrap.alignment = TextAnchor.UpperLeft;
            _head = new GUIStyle(_label); _head.fontSize = Mathf.RoundToInt(fs * 0.8f); _head.fontStyle = FontStyle.Bold; _head.normal.textColor = BtcMenu.Accent;
        }

        private static void Txt(Rect r, string s, GUIStyle st, Color c)
        {
            Color old = GUI.contentColor;
            GUI.contentColor = c;
            GUI.Label(r, s, st);
            GUI.contentColor = old;
        }

        /// <summary>Small accent caption with a line under it.</summary>
        internal static void Section(ref float y, float w, int fs, string text)
        {
            Styles(fs);
            float pad = fs;
            Txt(new Rect(pad, y, w - pad * 2f, fs * 1.6f), text.ToUpperInvariant(), _head, BtcMenu.Accent);
            BtcReplayUI.Fill(new Rect(pad, y + fs * 1.6f, w - pad * 2f, 1f), BtcMenu.Line);
            y += fs * 1.9f;
        }

        // ---- text fields: a click outside the focused field (or Esc) must release the keyboard (so hotkeys and the key finder work again)
        private static readonly List<Rect> FieldRects = new List<Rect>();
        private static int _fieldFrame = -1;

        /// <summary>Register the rect of a text field drawn this frame (call right before GUI.TextField).</summary>
        internal static void Field(Rect r)
        {
            if (Time.frameCount != _fieldFrame) { _fieldFrame = Time.frameCount; FieldRects.Clear(); }
            FieldRects.Add(r);
        }

        /// <summary>Call at the start of a tab's Draw (same coordinate space as the Field rects).</summary>
        internal static void ClickAway(Event e)
        {
            BtcSfx.Typing(e);
            ClickAwayCore(e);
        }

        private static void ClickAwayCore(Event e)
        {
            if (GUIUtility.keyboardControl == 0) return;
            bool esc = e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape;
            bool away = false;
            if (e.type == EventType.MouseDown)
            {
                away = true;
                for (int i = 0; i < FieldRects.Count; i++) if (FieldRects[i].Contains(e.mousePosition)) { away = false; break; }
            }
            if (!away && !esc) return;
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl("");
            if (esc) e.Use();
        }

        // ---- foldable sections: the collapsed keys are remembered in menu-ui.txt (everything is open by default)
        private static readonly HashSet<string> Closed = new HashSet<string>();
        private static bool _foldLoaded;
        private static string FoldFile { get { return BtcPaths.Config("menu-ui.txt"); } }

        private static void LoadFold()
        {
            if (_foldLoaded) return;
            _foldLoaded = true;
            try
            {
                if (!System.IO.File.Exists(FoldFile)) return;
                string[] l = System.IO.File.ReadAllLines(FoldFile, System.Text.Encoding.UTF8);
                for (int i = 0; i < l.Length; i++) if (l[i].StartsWith("closed=")) Closed.Add(l[i].Substring(7));
            }
            catch (Exception) { }
        }

        private static void SaveFold()
        {
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (string k in Closed) sb.Append("closed=").Append(k).Append("\r\n");
                System.IO.File.WriteAllText(FoldFile, sb.ToString(), new System.Text.UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        internal static bool IsOpen(string key) { LoadFold(); return !Closed.Contains(key); }

        internal static void SetOpen(string key, bool open)
        {
            LoadFold();
            if (open ? Closed.Remove(key) : Closed.Add(key)) SaveFold();
        }

        /// <summary>Sets many sections at once (expand / collapse all), one save.</summary>
        internal static void SetOpen(IList<string> keys, bool open)
        {
            LoadFold();
            bool ch = false;
            for (int i = 0; i < keys.Count; i++) ch |= open ? Closed.Remove(keys[i]) : Closed.Add(keys[i]);
            if (ch) SaveFold();
        }

        /// <summary>A section header that folds: chevron, accent caption, optional count, line. The whole row is the button. Returns true when the section is open (draw its content only then).</summary>
        internal static bool Fold(ref float y, float w, int fs, string key, string text, int count)
        {
            Styles(fs);
            bool open = IsOpen(key);
            float pad = fs;
            Rect r = new Rect(pad, y, w - pad * 2f, fs * 1.8f);
            bool hover = r.Contains(Event.current.mousePosition);
            if (hover) BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, 0.04f));
            float g = fs * 1.0f;
            BtcReplayIcons.Glyph(open ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, new Rect(r.x, r.center.y - g * 0.5f, g * 1.4f, g), hover ? Color.white : BtcMenu.Accent);
            Txt(new Rect(r.x + fs * 1.6f, r.y, r.width - fs * 1.6f, r.height), text.ToUpperInvariant() + (count > 0 ? "  " + count : ""), _head, BtcMenu.Accent);
            BtcReplayUI.Fill(new Rect(r.x, r.yMax, r.width, 1f), BtcMenu.Line);
            y += fs * 2.1f;
            if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) { SetOpen(key, !open); open = !open; BtcSfx.Switch(open); }
            return open;
        }

        /// <summary>The master switch of a tab: a small switch in the page header (right side, left of the Simple/Expert icons). Returns true when clicked.</summary>
        internal static bool HeaderSwitch(Rect sw, bool on, string tip)
        {
            BtcReplayUI.Fill(sw, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.9f) : new Color(1f, 1f, 1f, 0.16f));
            float kk = sw.height - 4f;
            BtcReplayUI.Fill(new Rect(on ? sw.xMax - kk - 2f : sw.x + 2f, sw.y + 2f, kk, kk), on ? new Color(0.03f, 0.05f, 0.04f, 1f) : new Color(1f, 1f, 1f, 0.75f));
            BtcReplayIcons.Hint(sw, tip);
            return BtcSfx.Btn(sw, "", BtcReplayUI.sBtn);
        }

        /// <summary>Icon + label row used as an action button (whole row clickable). Returns true when clicked.</summary>
        internal static bool Action(ref float y, float w, int fs, int icon, string label, string hint)
        {
            Styles(fs);
            float pad = fs, h = fs * 2.1f + (string.IsNullOrEmpty(hint) ? 0f : fs * 1.1f);
            Rect row = new Rect(pad, y, w - pad * 2f, h);
            bool hover = row.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, hover ? 0.07f : 0.03f));
            float g = fs * 1.2f;
            BtcReplayIcons.Glyph(icon, new Rect(row.x + fs * 0.7f, row.y + (fs * 2.1f - g) * 0.5f, g, g), hover ? Color.white : BtcMenu.Accent);
            Txt(new Rect(row.x + fs * 2.6f, row.y, row.width - fs * 3f, fs * 2.1f), label, _label, Color.white);
            if (!string.IsNullOrEmpty(hint)) Txt(new Rect(row.x + fs * 2.6f, row.y + fs * 1.65f, row.width - fs * 3f, fs * 1.1f), hint, _dim, new Color(1f, 1f, 1f, 0.5f));
            y += h + 3f;
            return BtcSfx.Btn(row, "", BtcReplayUI.sBtn);
        }

        /// <summary>Wrapped dim paragraph.</summary>
        internal static void Note(ref float y, float w, int fs, string text)
        {
            Styles(fs);
            float pad = fs, ww = w - pad * 2f;
            float h = _wrap.CalcHeight(new GUIContent(text), ww);
            GUI.Label(new Rect(pad, y, ww, h), text, _wrap);
            y += h + fs * 0.4f;
        }

        /// <summary>Row with an accent switch; the whole row is clickable. Returns true when it was clicked (the caller flips its value).</summary>
        internal static bool Toggle(ref float y, float w, int fs, string label, bool value, string hint)
        {
            Styles(fs);
            Event e = Event.current;
            float pad = fs, h = fs * 2.1f + (string.IsNullOrEmpty(hint) ? 0f : fs * 1.1f);
            Rect row = new Rect(pad, y, w - pad * 2f, h);
            bool hover = row.Contains(e.mousePosition);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, hover ? 0.07f : 0.03f));
            float sw = fs * 2.5f, sh = fs * 1.3f;
            Rect tr = new Rect(row.x + fs * 0.6f, row.y + (fs * 2.1f - sh) * 0.5f, sw, sh);
            BtcReplayUI.Fill(tr, value ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.9f) : new Color(1f, 1f, 1f, 0.16f));
            float k = sh - 4f;
            BtcReplayUI.Fill(new Rect(value ? tr.xMax - k - 2f : tr.x + 2f, tr.y + 2f, k, k), value ? new Color(0.03f, 0.05f, 0.04f, 1f) : new Color(1f, 1f, 1f, 0.75f));
            Txt(new Rect(tr.xMax + fs * 0.8f, row.y, row.width - sw - fs * 2.2f, fs * 2.1f), label, _label, value ? Color.white : new Color(1f, 1f, 1f, 0.78f));
            if (!string.IsNullOrEmpty(hint)) Txt(new Rect(tr.xMax + fs * 0.8f, row.y + fs * 1.65f, row.width - sw - fs * 2.2f, fs * 1.1f), hint, _dim, new Color(1f, 1f, 1f, 0.5f));
            y += h + 3f;
            bool clicked = BtcSfx.Btn(row, "", BtcReplayUI.sBtn);
            if (clicked) BtcSfx.Switch(!value);
            return clicked;
        }

        /// <summary>A switch row that sets its value through a setter and records an undo step in the given scope (the standard way to add a switch to a tab).</summary>
        internal static void ToggleUndo(ref float y, float w, int fs, string scope, string label, bool value, string hint, Action<bool> set)
        {
            if (!Toggle(ref y, w, fs, label, value, hint)) return;
            set(!value);
            BtcUndo.Push(scope, label, delegate { set(value); });
        }

        /// <summary>Row with a value and minus / plus buttons. Returns -1, 0 or +1.</summary>
        internal static int Stepper(ref float y, float w, int fs, string label, string value)
        {
            Styles(fs);
            Event e = Event.current;
            float pad = fs, h = fs * 2.1f;
            Rect row = new Rect(pad, y, w - pad * 2f, h);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.03f));
            Txt(new Rect(row.x + fs * 0.6f, row.y, row.width - fs * 12f, h), label, _label, new Color(1f, 1f, 1f, 0.85f));
            float b = h * 0.8f, bx = row.xMax - fs * 0.6f - b * 2f - fs * 5f, by = row.y + h * 0.1f;
            int dir = 0;
            Rect m = new Rect(bx, by, b, b), p = new Rect(row.xMax - fs * 0.6f - b, by, b, b);
            Rect v = new Rect(bx + b, by, fs * 5f, b);
            BtcReplayUI.Fill(m, new Color(1f, 1f, 1f, m.Contains(e.mousePosition) ? 0.18f : 0.09f));
            BtcReplayUI.Fill(p, new Color(1f, 1f, 1f, p.Contains(e.mousePosition) ? 0.18f : 0.09f));
            BtcReplayUI.Fill(new Rect(m.x + b * 0.28f, m.center.y - 1f, b * 0.44f, 2f), Color.white);
            BtcReplayUI.Fill(new Rect(p.x + b * 0.28f, p.center.y - 1f, b * 0.44f, 2f), Color.white);
            BtcReplayUI.Fill(new Rect(p.center.x - 1f, p.y + b * 0.28f, 2f, b * 0.44f), Color.white);
            GUIStyle c = new GUIStyle(_label); c.alignment = TextAnchor.MiddleCenter;
            GUI.Label(v, value, c);
            if (BtcSfx.Btn(m, "", BtcReplayUI.sBtn)) dir = -1;
            if (BtcSfx.Btn(p, "", BtcReplayUI.sBtn)) dir = 1;
            y += h + 3f;
            return dir;
        }

        /// <summary>One coloured status line.</summary>
        internal static void Status(ref float y, float w, int fs, string text, Color c)
        {
            Styles(fs);
            Txt(new Rect(fs, y, w - fs * 2f, fs * 1.8f), text, _label, c);
            y += fs * 2f;
        }
    }
}
