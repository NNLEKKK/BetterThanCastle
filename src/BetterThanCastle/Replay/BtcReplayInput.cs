using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay: what the watched player presses. Codes: KeyCode for keys, 320 left / 321 right / 322 middle / 323-324 side mouse buttons, and for the
    /// modifiers (Shift 303/304, Ctrl 305/306, Alt 307/308) the press and, as code + 1000, the release. Held camera keys are not recorded.
    /// Mode (bar icon cycles it): 0 hidden, 1 feed (newest pill on top, fades after ~1.6 s), 2 mini keyboard in the style of the bar (flat tiles on one dark
    /// backing, pressed keys take the player's colour, modifiers stay lit while held). Placed right above the bar, F7 module "Pressed keys".
    /// </summary>
    internal static class BtcReplayInput
    {
        private const float Life = 1.6f, KeyLife = 0.4f;
        private static GUIStyle _st, _stL;
        private static int _hCode;
        private static BtcPovRec.Track _hTrack;
        private static string _hName;
        private static readonly Dictionary<int, string> Names = new Dictionary<int, string>();

        // keyboard layout, units of one key step: code, label, x, row, width
        private struct K { internal int Code; internal string Lbl; internal float X; internal int Row; internal float W; internal K(int code, string lbl, float x, int row, float w) { Code = code; Lbl = lbl; X = x; Row = row; W = w; } }

        private static readonly List<K> Keys = BuildKeys();

        private static List<K> BuildKeys()
        {
            List<K> l = new List<K>();
            l.Add(new K(27, "Esc", 0f, 0, 1.5f));
            int[] dg = { 49, 50, 51, 52, 53, 54, 55, 56, 57, 48 };
            for (int i = 0; i < dg.Length; i++) l.Add(new K(dg[i], Name(dg[i]), 1.5f + i, 0, 1f));
            l.Add(new K(9, "Tab", 0f, 1, 1.5f));
            int[] q = { 113, 119, 101, 114, 116, 121, 117, 105, 111, 112 };
            for (int i = 0; i < q.Length; i++) l.Add(new K(q[i], Name(q[i]), 1.5f + i, 1, 1f));
            int[] a = { 97, 115, 100, 102, 103, 104, 106, 107, 108 };
            for (int i = 0; i < a.Length; i++) l.Add(new K(a[i], Name(a[i]), 1.75f + i, 2, 1f));
            l.Add(new K(301, "Caps", 0f, 2, 1.75f));
            l.Add(new K(304, "Shift", 0f, 3, 2.25f));
            int[] z = { 122, 120, 99, 118, 98, 110, 109 };
            for (int i = 0; i < z.Length; i++) l.Add(new K(z[i], Name(z[i]), 2.25f + i, 3, 1f));
            l.Add(new K(306, "Ctrl", 0f, 4, 1.5f));
            l.Add(new K(308, "Alt", 1.5f, 4, 1.5f));
            l.Add(new K(32, "Space", 3f, 4, 8.5f));
            return l;
        }

        internal static string Name(int code)
        {
            string s;
            if (Names.TryGetValue(code, out s)) return s;
            if (code == 320) s = "LMB";
            else if (code == 321) s = "RMB";
            else if (code == 322) s = "MMB";
            else if (code == 323) s = "M4";
            else if (code == 324) s = "M5";
            else if (code == 301) s = "Caps";
            else if (code == 303 || code == 304) s = "Shift";
            else if (code == 305 || code == 306) s = "Ctrl";
            else if (code == 307 || code == 308) s = "Alt";
            else if (code >= 97 && code <= 122) s = ((char)(code - 32)).ToString();
            else if (code >= 48 && code <= 57) s = ((char)code).ToString();
            else if (code >= 282 && code <= 293) s = "F" + (code - 281);
            else if (code == 32) s = "Space";
            else if (code == 9) s = "Tab";
            else if (code == 27) s = "Esc";
            else if (code == 13) s = "Enter";
            else if (code == 8) s = "Bksp";
            else s = ((KeyCode)code).ToString();
            Names[code] = s;
            return s;
        }

        private static void Style()
        {
            if (_st == null) { _st = new GUIStyle(BtcReplayUI.sBarLabel); _st.alignment = TextAnchor.MiddleCenter; _st.clipping = TextClipping.Overflow; }
            _st.font = BtcReplayUI.sBarLabel.font;
            _st.fontSize = BtcReplayUI.sBarLabel.fontSize;
            if (_stL == null) { _stL = new GUIStyle(_st); _stL.alignment = TextAnchor.MiddleLeft; }
            _stL.font = _st.font; _stL.fontSize = _st.fontSize;
        }

        private static void Hover(Rect r, int code, string name)
        {
            if (code < 300 && Event.current.type == EventType.Repaint && r.Contains(Event.current.mousePosition)) { _hCode = code; _hName = name; }
        }

        private static string NoAction() { return BtcReplayUI.T("No action bound", "Нет назначенного действия"); }

        /// <summary>Tooltip above the module: the key and what it is bound to.</summary>
        private static void DrawTip(Rect b, float fs)
        {
            if (_hCode < 0) return;
            List<string> acts = BtcApm.DescribeFor(_hTrack, _hCode);
            bool own = _hTrack != null && _hTrack.Bind != null;
            int n = acts == null ? 1 : Mathf.Min(acts.Count, 5);
            int rows = n + 1 + (own ? 0 : 1);
            float rowH = fs * 1.4f, w = fs * 15f;
            Rect r = new Rect(b.xMax - w, b.y - rowH * rows - 6f, w, rowH * rows);
            BtcReplayUI.Fill(r, new Color(0f, 0f, 0f, 0.88f));
            GUI.Label(new Rect(r.x + 8f, r.y, w, rowH), _hName, _stL);
            Color old = GUI.contentColor;
            GUI.contentColor = new Color(1f, 1f, 1f, 0.8f);
            for (int i = 0; i < n; i++)
            {
                string s = acts == null ? NoAction() : acts[i];
                if (s.Length > 30) s = s.Substring(0, 29) + "..";
                GUI.Label(new Rect(r.x + 8f, r.y + rowH * (i + 1), w, rowH), s, _stL);
            }
            if (!own)
            {
                GUI.contentColor = new Color(1f, 1f, 1f, 0.4f);
                GUI.Label(new Rect(r.x + 8f, r.y + rowH * (n + 1), w, rowH), BtcReplayUI.T("(your bindings: not recorded)", "(ваши привязки: у игрока не записаны)"), _stL);
            }
            GUI.contentColor = old;
        }

        /// <summary>First index with T greater than ms (events are sorted by time).</summary>
        private static int Upper(List<int> kt, int ms)
        {
            int lo = 0, hi = kt.Count;
            while (lo < hi) { int mid = (lo + hi) >> 1; if (kt[mid] <= ms) lo = mid + 1; else hi = mid; }
            return lo;
        }

        /// <summary>Call after the bar is drawn; bottomY = top edge of what sits above the bar.</summary>
        internal static void Draw(float fs, float bottomY)
        {
            int mode = BtcConfig.ReplayKeysMode.Value;
            if (mode == 0 || !BtcReplayLayout.Visible(BtcReplayLayout.Keys)) return;
            BtcPovRec.Track t = BtcPov.Active ? BtcPovRec.TrackOf(BtcPov.PovFaction.ID) : null;
            bool edit = BtcReplayLayout.Edit;
            if (t == null && !edit) return;
            Style();
            _hCode = -1;
            _hTrack = t;
            float c = fs * 1.25f, g = fs * 0.12f, u = c + g, pad = fs * 0.4f;
            float w = mode == 1 ? fs * 6.5f : 15f * u - g + pad * 2f;
            float h = mode == 1 ? fs * 1.45f * 6f : 5f * u - g + pad * 2f;
            Rect b = new Rect(Screen.width - w - fs, bottomY - h, w, h);
            BtcReplayLayout.Begin(BtcReplayLayout.Keys, b);
            Color pc = Color.white;
            if (t != null) { Color tc; if (ColorUtility.TryParseHtmlString("#" + t.Col.TrimStart('#'), out tc)) pc = tc; }
            int nowMs = Mathf.RoundToInt(BtcReplay.PlayClock * 1000f);
            if (mode == 1) DrawFeed(b, fs, t, nowMs, pc, edit);
            else DrawKeyboard(b, c, g, pad, t, nowMs, pc);
            DrawTip(b, fs);
            BtcReplayLayout.End();
        }

        private static void Pill(Rect r, string text, float a, Color accent)
        {
            BtcReplayUI.Fill(r, new Color(0f, 0f, 0f, 0.62f * a));
            BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, r.height), new Color(accent.r, accent.g, accent.b, a));
            Color old = GUI.contentColor;
            GUI.contentColor = new Color(1f, 1f, 1f, a);
            GUI.Label(r, text, _st);
            GUI.contentColor = old;
        }

        private static void DrawFeed(Rect b, float fs, BtcPovRec.Track t, int nowMs, Color pc, bool edit)
        {
            if (Event.current.type != EventType.Repaint) return;
            float rowH = fs * 1.45f;
            int row = 0;
            if (t != null)
            {
                int i = Upper(t.KT, nowMs) - 1;
                for (; i >= 0 && row < 6; i--)
                {
                    float age = (nowMs - t.KT[i]) / 1000f;
                    if (age > Life) break;
                    if (t.KC[i] >= 1000) continue;   // modifier releases only drive the keyboard
                    string s = Name(t.KC[i]);
                    float pw = Mathf.Max(fs * 2.2f, fs * (0.9f + 0.62f * s.Length));
                    float a = Mathf.Clamp01((Life - age) / 0.6f);
                    Rect pr = new Rect(b.xMax - pw, b.y + row * rowH, pw, rowH * 0.88f);
                    Pill(pr, s, a, pc);
                    Hover(pr, t.KC[i], s);
                    row++;
                }
            }
            if (edit && row == 0)
            {
                Pill(new Rect(b.xMax - fs * 2.6f, b.y, fs * 2.6f, rowH * 0.88f), "LMB", 0.6f, pc);
                Pill(new Rect(b.xMax - fs * 2.2f, b.y + rowH, fs * 2.2f, rowH * 0.88f), "Q", 0.6f, pc);
            }
        }

        /// <summary>0..1 light of a key: a short pulse after a press; a modifier stays lit from its press until its release.</summary>
        private static float Level(BtcPovRec.Track t, int nowMs, int code)
        {
            if (t == null) return 0f;
            bool mod = code == 301 || (code >= 303 && code <= 308);
            int other = (code >= 303 && code <= 308) ? ((code & 1) == 1 ? code + 1 : code - 1) : -1;
            int scanned = 0;
            for (int i = Upper(t.KT, nowMs) - 1; i >= 0 && scanned < 400; i--, scanned++)
            {
                float age = (nowMs - t.KT[i]) / 1000f;
                int kc = t.KC[i];
                if (mod)
                {
                    if (age > 120f) break;
                    if (kc == code || kc == other) return 1f;                    // last press of this modifier (either side)
                    if (kc == code + 1000 || kc == other + 1000) return 0f;      // released
                }
                else
                {
                    if (age > KeyLife) break;
                    if (kc == code) return 1f - age / KeyLife;
                }
            }
            return 0f;
        }

        private static void Tile(Rect r, string label, float k, Color pc)
        {
            BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, 0.07f));
            if (k > 0f)
            {
                BtcReplayUI.Fill(r, new Color(pc.r, pc.g, pc.b, 0.28f * k));
                BtcReplayUI.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), new Color(pc.r, pc.g, pc.b, 0.95f * k));
            }
            Color old = GUI.contentColor;
            GUI.contentColor = k > 0f ? new Color(Mathf.Lerp(1f, pc.r, 0.35f * k), Mathf.Lerp(1f, pc.g, 0.35f * k), Mathf.Lerp(1f, pc.b, 0.35f * k), 1f) : new Color(1f, 1f, 1f, 0.5f);
            GUI.Label(r, label, _st);
            GUI.contentColor = old;
        }

        private static void DrawKeyboard(Rect b, float c, float g, float pad, BtcPovRec.Track t, int nowMs, Color pc)
        {
            if (Event.current.type != EventType.Repaint) return;
            BtcReplayUI.Fill(b, new Color(0f, 0f, 0f, 0.62f));
            float u = c + g, x0 = b.x + pad, y0 = b.y + pad;
            for (int i = 0; i < Keys.Count; i++)
            {
                K k = Keys[i];
                Rect kr = new Rect(x0 + k.X * u, y0 + k.Row * u, k.W * u - g, c);
                Tile(kr, k.Lbl, Level(t, nowMs, k.Code), pc);
                Hover(kr, k.Code, k.Lbl);
            }
            // mouse: three buttons on top, the body fills the rest of the height (side buttons on its left edge)
            float mx = x0 + 12f * u, mw = 3f * u - g, topH = 2f * u - g;
            Tile(new Rect(mx, y0, 1.3f * u, topH), "LMB", Level(t, nowMs, 320), pc);
            Tile(new Rect(mx + 1.3f * u, y0, 0.4f * u - g, topH), "", Level(t, nowMs, 322), pc);
            Tile(new Rect(mx + 1.7f * u, y0, mw - 1.7f * u, topH), "RMB", Level(t, nowMs, 321), pc);
            float bodyY = y0 + 2f * u;
            Rect body = new Rect(mx, bodyY, mw, 3f * u - g);
            BtcReplayUI.Fill(body, new Color(1f, 1f, 1f, 0.04f));
            Tile(new Rect(mx + g, bodyY + g, 1.3f * u, c), "M4", Level(t, nowMs, 323), pc);
            Tile(new Rect(mx + g, bodyY + u + g * 2f, 1.3f * u, c), "M5", Level(t, nowMs, 324), pc);
        }
    }
}
