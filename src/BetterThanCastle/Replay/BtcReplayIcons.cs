using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Tiny icons for the replay bar, drawn from filled rects (no textures, no font glyphs) plus a hover tooltip.</summary>
    internal static class BtcReplayIcons
    {
        internal const int Monitor = 0, Camera = 1, Cursor = 2, Blueprint = 3, View = 4, Layout = 5, Play = 6, Pause = 7, Slow = 8, Fast = 9, Back10 = 10, Fwd10 = 11, Stop = 12, Prev = 13, Next = 14;

        private static string _tip;
        private static Rect _tipAnchor;

        private static void R(float x, float y, float w, float h, Color c)
        {
            BtcReplayUI.Fill(new Rect(x, y, w, h), c);
        }

        private static void Frame(float x, float y, float w, float h, float t, Color c)
        {
            R(x, y, w, t, c); R(x, y + h - t, w, t, c); R(x, y, t, h, c); R(x + w - t, y, t, h, c);
        }

        /// <summary>Triangle 3.6p wide, 8p tall at (x,y); right = apex on the right. Columns do not overlap (no alpha stripes).</summary>
        private static void Tri(float x, float y, float p, bool right, Color c)
        {
            const int n = 14;
            float cw = 3.6f * p / n;
            for (int i = 0; i < n; i++)
            {
                float h = 8f * p * (1f - (i + 0.5f) / n);
                R(x + (right ? i : n - 1 - i) * cw, y + 4f * p - h * 0.5f, cw + 0.02f, h, c);
            }
        }


        private static void Draw(int kind, Rect r, Color c)
        {
            float s = Mathf.Min(r.width, r.height) * 0.62f;
            float p = s / 8f;
            float x = r.center.x - s * 0.5f, y = r.center.y - s * 0.5f;
            switch (kind)
            {
                case Monitor:
                    Frame(x, y + p, 8f * p, 5f * p, p * 0.8f, c);
                    R(x + 3.5f * p, y + 6f * p, p, p, c);
                    R(x + 2f * p, y + 7f * p, 4f * p, p * 0.8f, c);
                    break;
                case Camera:
                    R(x + 2.5f * p, y + p, 3f * p, p, c);
                    R(x, y + 2f * p, 8f * p, 5f * p, c);
                    R(x + 3f * p, y + 3.2f * p, 2f * p, 2.6f * p, new Color(0.08f, 0.08f, 0.1f, 1f));
                    break;
                case Cursor:
                    for (int i = 0; i < 6; i++) R(x + p, y + (i + 0.5f) * p, (1 + i) * 0.9f * p, p, c);
                    R(x + 2.2f * p, y + 6f * p, 1.6f * p, 2f * p, c);
                    break;
                case Blueprint:
                    Frame(x, y, 8f * p, 8f * p, p * 0.8f, c);
                    R(x + 2.6f * p, y, p * 0.8f, 8f * p, c);
                    R(x + 5f * p, y, p * 0.8f, 8f * p, c);
                    R(x, y + 2.6f * p, 8f * p, p * 0.8f, c);
                    R(x, y + 5f * p, 8f * p, p * 0.8f, c);
                    break;
                case View:
                    for (int i = 0; i < 8; i++) R(x + (3.5f - i * 0.5f) * p, y + i * p, (1 + i) * p, p, new Color(c.r, c.g, c.b, c.a * (0.5f + i * 0.06f)));
                    R(x + 3.2f * p, y, 1.6f * p, 1.6f * p, c);
                    break;
                case Play: Tri(x + 2.2f * p, y, p, true, c); break;
                case Pause: R(x + 1.5f * p, y + p, 2f * p, 6f * p, c); R(x + 4.5f * p, y + p, 2f * p, 6f * p, c); break;
                case Slow: Tri(x + 0.2f * p, y, p, false, c); Tri(x + 4.2f * p, y, p, false, c); break;
                case Fast: Tri(x + 0.2f * p, y, p, true, c); Tri(x + 4.2f * p, y, p, true, c); break;
                case Back10: R(x + 0.8f * p, y + p, 1.1f * p, 6f * p, c); Tri(x + 3f * p, y, p, false, c); break;
                case Fwd10: R(x + 6.1f * p, y + p, 1.1f * p, 6f * p, c); Tri(x + 1.4f * p, y, p, true, c); break;
                case Stop: R(x + p, y + p, 6f * p, 6f * p, c); break;
                case Prev: Tri(x + 2.2f * p, y, p, false, c); break;
                case Next: Tri(x + 2.2f * p, y, p, true, c); break;
                case Layout:
                    R(x, y, 5f * p, 4f * p, c);
                    R(x + 5.7f * p, y, 2.3f * p, 4f * p, c);
                    R(x, y + 4.7f * p, 8f * p, 3.3f * p, c);
                    break;
            }
        }

        /// <summary>Square icon toggle; tip is shown above the bar while hovered. Returns true on click.</summary>
        internal static bool Button(Rect r, int kind, bool on, string tip)
        {
            Event e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            BtcReplayUI.Fill(r, on ? new Color(0.5f, 1f, 0.55f, 0.18f) : new Color(1f, 1f, 1f, hover ? 0.14f : 0.07f));
            Draw(kind, r, on ? new Color(0.5f, 1f, 0.55f, 0.95f) : new Color(1f, 1f, 1f, hover ? 0.9f : 0.55f));
            if (hover && e.type == EventType.Repaint) { _tip = tip; _tipAnchor = r; }
            return GUI.Button(r, "", BtcReplayUI.sBtn);
        }

        /// <summary>Icon button in a fixed colour (transport controls).</summary>
        internal static bool Plain(Rect r, int kind, Color c, string tip)
        {
            Event e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, hover ? 0.14f : 0.07f));
            Draw(kind, r, hover ? new Color(c.r, c.g, c.b, 1f) : new Color(c.r, c.g, c.b, c.a * 0.85f));
            if (hover && e.type == EventType.Repaint) { _tip = tip; _tipAnchor = r; }
            return GUI.Button(r, "", BtcReplayUI.sBtn);
        }

        /// <summary>Call once after the bar is drawn.</summary>
        internal static void DrawTip(int fs)
        {
            if (_tip == null || Event.current.type != EventType.Repaint) { if (Event.current.type == EventType.Repaint) _tip = null; return; }
            float w = _tip.Length * fs * 0.62f + fs * 1.2f;
            float h = fs * 1.5f;
            Rect t = new Rect(Mathf.Clamp(_tipAnchor.center.x - w * 0.5f, 2f, Screen.width - w - 2f), _tipAnchor.y - h - 4f, w, h);
            BtcReplayUI.Fill(t, new Color(0f, 0f, 0f, 0.88f));
            GUI.Label(new Rect(t.x + fs * 0.6f, t.y, t.width, t.height), _tip, BtcReplayUI.sBarLabel);
            _tip = null;
        }
    }
}
