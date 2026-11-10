using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// In-game HUD pieces in the menu style (flat dark, accent stripe, icon + short text): top-centre cards (click = details, X = dismiss),
    /// short notices, left-column plates and hover-expanding chips. Immediate mode: callers submit every frame.
    /// Every element has an id with a <see cref="Slot"/> (enabled/scale/alpha/offset); the values are defaults for now,
    /// the future Visuals UI editor will fill them (nothing reads a config yet).
    /// </summary>
    internal static class BtcHud
    {
        internal static readonly Color Ok = new Color(0.55f, 0.85f, 0.55f, 1f);
        internal static readonly Color Warn = new Color(0.95f, 0.78f, 0.25f, 1f);
        internal static readonly Color Danger = new Color(0.92f, 0.34f, 0.32f, 1f);
        internal static readonly Color Panel = new Color(0.03f, 0.036f, 0.045f, 0.93f);

        /// <summary>Per-element customisation (prepared for the UI editor, defaults only).</summary>
        internal sealed class Slot
        {
            internal bool Enabled = true;
            internal float Scale = 1f, Alpha = 1f;
            /// <summary>Position offset as a fraction of the screen height (resolution independent); the UI editor writes it.</summary>
            internal Vector2 Norm;
            internal Vector2 Offset { get { return Norm * Screen.height; } }
        }

        private static readonly Dictionary<string, Slot> _slots = new Dictionary<string, Slot>();

        internal static Slot SlotOf(string id)
        {
            Slot s;
            if (!_slots.TryGetValue(id, out s))
            {
                s = new Slot();
                if (id == "ping" || id == "clientpause" || id == "pauseplate") s.Scale = 0.7f;
                else if (id == "groups") s.Scale = 0.85f;
                _slots[id] = s;
            }
            return s;
        }

        internal static int Fs { get { return Mathf.Max(8, Mathf.RoundToInt(Mathf.Max(13, Screen.height / 45) * BtcUiEdit.HudScale)); } }

        // ---- notification history (menu tab 13): every notice and every card that appeared this session
        internal sealed class HistEntry { internal string Text; internal int Icon; internal Color Col; internal string Clock; }
        internal static readonly List<HistEntry> History = new List<HistEntry>();
        internal static int Unseen;

        private static void Hist(string text, int icon, Color col)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (History.Count > 0 && History[History.Count - 1].Text == text && Time.unscaledTime - _histAt < 2f) return;   // the same message twice in a row
            _histAt = Time.unscaledTime;
            History.Add(new HistEntry { Text = text, Icon = icon, Col = col, Clock = System.DateTime.Now.ToString("HH:mm:ss") });
            if (History.Count > 150) History.RemoveAt(0);
            Unseen++;
        }

        private static float _histAt;

        private sealed class CardState { internal float Born, Seen; internal bool Open; internal Vector2 Scroll; }
        /// <summary>Expands a card (a clickable hint opens at once, so its button is one click away).</summary>
        /// <summary>Adds a line to the notification history without showing anything (cards drawn by other code).</summary>
        internal static void AddHistory(string text, int icon, Color col) { Hist(text, icon, col); }

        internal static void ForceOpen(string id) { CardState c; if (_cards.TryGetValue(id, out c)) c.Open = true; }
        private static readonly Dictionary<string, CardState> _cards = new Dictionary<string, CardState>();

        private sealed class Note { internal string Text; internal int Icon; internal Color Col; internal float At, Until; }
        private static readonly List<Note> _notes = new List<Note>();

        private static GUIStyle _note;
        private static int _noteFs;
        private static int _stackKey = -1;
        private static float _top, _left;

        private static void Frame(int fs)
        {
            BtcReplayUI.Ensure(fs);
            int key = Time.frameCount * 8 + (int)Event.current.type;
            if (key != _stackKey) { _stackKey = key; _top = fs * 0.5f; _left = fs * 0.6f; }
        }

        private static float Fade(CardState c, Slot sl)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - c.Born) / 0.18f);
            return sl.Alpha * t;
        }

        private static Color A(Color c, float a) { return new Color(c.r, c.g, c.b, c.a * a); }

        private static void Label(Rect r, string text, GUIStyle st, Color col, float a)
        {
            Color old = GUI.contentColor;
            GUI.contentColor = A(col, a);
            GUI.Label(r, text, st);
            GUI.contentColor = old;
        }

        /// <summary>A top-centre card. Collapsed: icon, title, chevron, X. Click = expanded text + detail lines. Returns true when X was pressed.</summary>
        internal static bool Card(string id, int icon, Color accent, string title, string text, List<string> details, bool dismissable)
        {
            return Card(id, icon, accent, title, text, details, dismissable, null, null);
        }

        /// <summary>Card with an action button at the bottom of the expanded view (e.g. "Open in the Gameplay tab").</summary>
        internal static bool Card(string id, int icon, Color accent, string title, string text, List<string> details, bool dismissable, string actionLabel, System.Action action)
        {
            return Card(id, icon, accent, title, text, details, dismissable, actionLabel, action, -1);
        }

        /// <summary>actionIcon >= 0: the action is a small icon button at the bottom right (no wide text button).</summary>
        internal static bool Card(string id, int icon, Color accent, string title, string text, List<string> details, bool dismissable, string actionLabel, System.Action action, int actionIcon)
        {
            Slot sl = SlotOf("cards");
            if (!sl.Enabled) return false;
            int fs = Mathf.RoundToInt(Fs * sl.Scale);
            Frame(fs);
            Event e = Event.current;
            CardState c;
            if (!_cards.TryGetValue(id, out c)) { c = new CardState(); _cards[id] = c; c.Born = Time.unscaledTime; }
            if (Time.unscaledTime - c.Seen > 0.5f) { c.Born = Time.unscaledTime; Hist(string.IsNullOrEmpty(text) ? title : title + ": " + text, icon, accent); }
            c.Seen = Time.unscaledTime;
            float a = Fade(c, sl);
            GUI.depth = -1100;
            float w = Mathf.Min(Screen.width * 0.55f, fs * 34f);
            float head = fs * 2.3f;
            float inner = w - fs * 1.6f;
            if (_note == null || _noteFs != fs)
            {
                _noteFs = fs;
                _note = new GUIStyle(BtcReplayUI.sLabel);
                _note.fontSize = Mathf.Max(10, Mathf.RoundToInt(fs * 0.9f));
                _note.wordWrap = true;
                _note.alignment = TextAnchor.UpperLeft;
                _note.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
            }
            GUIStyle note = _note, lab = BtcReplayUI.sLabel, bold = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            float textH = 0f, listH = 0f;
            int lines = details == null ? 0 : details.Count;
            if (c.Open)
            {
                textH = string.IsNullOrEmpty(text) ? 0f : note.CalcHeight(new GUIContent(text), inner) + fs * 0.4f;
                listH = lines == 0 ? 0f : Mathf.Min(lines, 9) * fs * 1.3f + fs * 0.4f;
            }
            float actH = (c.Open && action != null) ? (actionIcon >= 0 ? fs * 2.6f : fs * 2.4f) : 0f;
            Rect r = new Rect((Screen.width - w) * 0.5f + sl.Offset.x, _top + sl.Offset.y, w, head + textH + listH + actH);
            BtcReplayUI.Fill(r, A(Panel, a));
            BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, r.height), A(accent, a));
            Rect hr = new Rect(r.x, r.y, r.width, head);
            bool hov = hr.Contains(e.mousePosition);
            if (hov) BtcReplayUI.Fill(hr, new Color(1f, 1f, 1f, 0.05f * a));
            float g = fs * 1.4f;
            BtcReplayIcons.Glyph(icon, new Rect(r.x + fs * 0.6f, r.y + (head - g) * 0.5f, g, g), A(accent, a));
            float xw = dismissable ? head : 0f;
            Rect close = new Rect(r.xMax - xw, r.y, xw, head);
            bool pressed = false;
            bool prev = GUI.enabled;
            if (dismissable)
            {
                bool ch = close.Contains(e.mousePosition);
                BtcReplayIcons.Glyph(BtcReplayIcons.Cross, new Rect(close.center.x - g * 0.5f, close.center.y - g * 0.5f, g, g), new Color(1f, 1f, 1f, (ch ? 0.95f : 0.45f) * a));
                BtcReplayIcons.Hint(close, BtcReplayUI.T("Dismiss", "Закрыть"));
                if (BtcSfx.Btn(close, "", BtcReplayUI.sBtn)) pressed = true;
            }
            Rect tr = new Rect(r.x + fs * 0.6f + g + fs * 0.6f, r.y, r.width - g - fs * 1.6f - xw - g, head);
            Label(tr, title, bold, Color.white, a);
            if (details != null || !string.IsNullOrEmpty(text))
            {
                float cg = fs * 1.0f;
                Rect cr = new Rect(r.xMax - xw - cg - fs * 0.5f, r.y + (head - cg) * 0.5f, cg, cg);
                BtcReplayIcons.Glyph(c.Open ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, cr, new Color(1f, 1f, 1f, (hov ? 0.9f : 0.5f) * a));
                Rect hit = new Rect(hr.x, hr.y, hr.width - xw, hr.height);
                BtcReplayIcons.Hint(hit, c.Open ? BtcReplayUI.T("Hide details", "Скрыть подробности") : BtcReplayUI.T("What changed?", "Что изменено?"));
                if (BtcSfx.Btn(hit, "", BtcReplayUI.sBtn)) c.Open = !c.Open;
            }
            if (c.Open)
            {
                float y = r.y + head;
                if (textH > 0f)
                {
                    GUI.Label(new Rect(r.x + fs * 0.8f, y, inner, textH), text, note);
                    y += textH;
                }
                if (lines > 0)
                {
                    Rect view = new Rect(r.x + fs * 0.8f, y, inner, listH - fs * 0.4f);
                    Rect content = new Rect(0f, 0f, inner - fs, lines * fs * 1.3f);
                    c.Scroll = GUI.BeginScrollView(view, c.Scroll, content);
                    for (int i = 0; i < lines; i++) GUI.Label(new Rect(0f, i * fs * 1.3f, content.width, fs * 1.3f), details[i], lab);
                    GUI.EndScrollView();
                    y += listH;
                }
                if (actH > 0f && actionIcon >= 0)
                {
                    float bs = fs * 2f;
                    Rect ib = new Rect(r.xMax - fs * 0.8f - bs, y + fs * 0.2f, bs, bs);
                    bool ihv = ib.Contains(e.mousePosition);
                    BtcReplayUI.Fill(ib, new Color(accent.r, accent.g, accent.b, (ihv ? 0.35f : 0.18f) * a));
                    BtcReplayIcons.Glyph(actionIcon, new Rect(ib.x + bs * 0.2f, ib.y + bs * 0.2f, bs * 0.6f, bs * 0.6f), A(Color.white, a * (ihv ? 1f : 0.85f)));
                    if (BtcSfx.Btn(ib, "", BtcReplayUI.sBtn)) action();
                }
                else
                if (actH > 0f)
                {
                    Rect ab = new Rect(r.x + fs * 0.8f, y + fs * 0.2f, inner, fs * 1.9f);
                    bool ah = ab.Contains(e.mousePosition);
                    BtcReplayUI.Fill(ab, new Color(accent.r, accent.g, accent.b, (ah ? 0.35f : 0.2f) * a));
                    Label(ab, actionLabel, bold, Color.white, a);
                    if (BtcSfx.Btn(ab, "", BtcReplayUI.sBtn)) action();
                }
            }
            GUI.enabled = prev;
            _top = r.yMax - sl.Offset.y + fs * 0.3f;
            return pressed;
        }

        /// <summary>One-line message in the top stack for a few seconds (replaces the old bottom toast).</summary>
        internal static void Notice(string text, int icon, Color accent)
        {
            if (string.IsNullOrEmpty(text)) return;
            Hist(text, icon, accent);
            if (_notes.Count >= 4) _notes.RemoveAt(0);
            _notes.Add(new Note { Text = text, Icon = icon, Col = accent, At = Time.unscaledTime, Until = Time.unscaledTime + 6f });
        }

        internal static void Notice(string text) { Notice(text, BtcReplayIcons.Check, Ok); }

        /// <summary>Draws the queued notices under the cards. Call once per OnGUI after the cards.</summary>
        internal static void DrawNotices()
        {
            if (_notes.Count == 0 || Event.current == null) return;
            Slot sl = SlotOf("notice");
            int fs = Mathf.RoundToInt(Fs * sl.Scale);
            Frame(fs);
            Event e = Event.current;
            GUI.depth = -1100;
            GUIStyle st = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            for (int i = _notes.Count - 1; i >= 0; i--) if (Time.unscaledTime > _notes[i].Until) _notes.RemoveAt(i);
            if (!sl.Enabled) return;
            for (int i = 0; i < _notes.Count; i++)
            {
                Note n = _notes[i];
                float a = sl.Alpha * Mathf.Clamp01((n.Until - Time.unscaledTime) / 0.4f) * Mathf.Clamp01((Time.unscaledTime - n.At) / 0.12f + 0.2f);
                float g = fs * 1.3f, h = fs * 2.0f;
                float w = Mathf.Min(Screen.width * 0.6f, n.Text.Length * fs * 0.62f + fs * 3.2f);
                Rect r = new Rect((Screen.width - w) * 0.5f + sl.Offset.x, _top + sl.Offset.y, w, h);
                BtcReplayUI.Fill(r, A(Panel, a));
                BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, r.height), A(n.Col, a));
                BtcReplayIcons.Glyph(n.Icon, new Rect(r.x + fs * 0.6f, r.y + (h - g) * 0.5f, g, g), A(n.Col, a));
                Label(new Rect(r.x + fs * 0.6f + g + fs * 0.5f, r.y, r.width - g - fs * 1.4f, h), n.Text, st, Color.white, a);
                _top = r.yMax - sl.Offset.y + fs * 0.25f;
            }
        }

        /// <summary>Plate in the top-left column (pause state etc.): icon + text on a dark strip.</summary>
        internal static void Plate(string id, int icon, Color accent, string text)
        {
            Slot sl = SlotOf(id);
            if (!sl.Enabled) return;
            int fs = Mathf.RoundToInt(Fs * sl.Scale);
            Frame(fs);
            GUI.depth = -1100;
            GUIStyle st = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            float g = fs * 1.2f, h = fs * 1.9f, w = text.Length * fs * 0.62f + g + fs * 2.2f;
            Rect r = new Rect(_left + sl.Offset.x, _leftY + sl.Offset.y, w, h);
            BtcReplayUI.Fill(r, A(Panel, sl.Alpha));
            BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, r.height), A(accent, sl.Alpha));
            BtcReplayIcons.Glyph(icon, new Rect(r.x + fs * 0.6f, r.y + (h - g) * 0.5f, g, g), A(accent, sl.Alpha));
            Label(new Rect(r.x + fs * 0.6f + g + fs * 0.5f, r.y, w - g - fs * 1.2f, h), text, st, Color.white, sl.Alpha);
            _leftY = r.yMax - sl.Offset.y + fs * 0.25f;
        }

        private static float _leftY = 14f;
        private static int _leftKey = -1;

        /// <summary>Start of the left column for this frame (call before the first Plate/Chip of a frame).</summary>
        internal static void BeginLeft()
        {
            int key = Time.frameCount * 8 + (int)Event.current.type;
            if (key == _leftKey) return;
            _leftKey = key;
            _leftY = Fs * 0.6f + BtcMenuLauncher.ColumnShift();
        }

        /// <summary>Compact chip (icon + short value); hovering it expands a list of rows below. dots: colour per row (a.a = 0 means none).</summary>
        internal static void Chip(string id, int icon, Color accent, string value, string[] rows, Color[] dots)
        {
            Slot sl = SlotOf(id);
            if (!sl.Enabled) return;
            int fs = Mathf.RoundToInt(Fs * sl.Scale);
            Frame(fs);
            Event e = Event.current;
            GUI.depth = -1100;
            GUIStyle st = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel, lab = BtcReplayUI.sLabel;
            float g = fs * 1.2f, h = fs * 1.9f, w = value.Length * fs * 0.62f + g + fs * 2.2f;
            Rect r = new Rect(_left + sl.Offset.x, _leftY + sl.Offset.y, w, h);
            int n = rows == null ? 0 : rows.Length;
            float rowH = fs * 1.5f, rw = w;
            for (int i = 0; i < n; i++) rw = Mathf.Max(rw, rows[i].Length * fs * 0.6f + fs * 3f);
            Rect full = new Rect(r.x, r.y, rw, h + (n > 0 ? n * rowH + fs * 0.3f : 0f));
            bool open = n > 0 && (r.Contains(e.mousePosition) || (CardOpen(id) && full.Contains(e.mousePosition)));
            SetOpen(id, open);
            Rect box = open ? full : r;
            BtcReplayUI.Fill(box, A(Panel, sl.Alpha));
            BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, box.height), A(accent, sl.Alpha));
            BtcReplayIcons.Glyph(icon, new Rect(r.x + fs * 0.6f, r.y + (h - g) * 0.5f, g, g), A(accent, sl.Alpha));
            Label(new Rect(r.x + fs * 0.6f + g + fs * 0.5f, r.y, w - g - fs * 1.2f, h), value, st, Color.white, sl.Alpha);
            if (open)
            {
                for (int i = 0; i < n; i++)
                {
                    float y = r.y + h + fs * 0.1f + i * rowH;
                    float x = r.x + fs * 0.7f;
                    if (dots != null && i < dots.Length && dots[i].a > 0f)
                    {
                        float d = fs * 0.55f;
                        BtcReplayUI.Fill(new Rect(x, y + (rowH - d) * 0.5f, d, d), A(dots[i], sl.Alpha));
                    }
                    Label(new Rect(x + fs * 1.0f, y, rw - fs * 1.7f, rowH), rows[i], lab, Color.white, sl.Alpha * 0.9f);
                }
            }
            _leftY = r.yMax - sl.Offset.y + fs * 0.25f;
        }

        private static readonly Dictionary<string, bool> _chipOpen = new Dictionary<string, bool>();
        private static bool CardOpen(string id) { bool b; return _chipOpen.TryGetValue(id, out b) && b; }
        private static void SetOpen(string id, bool v) { _chipOpen[id] = v; }
    }
}
