using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Tiny icons for the replay bar, drawn from filled rects (no textures, no font glyphs) plus a hover tooltip.</summary>
    internal static class BtcReplayIcons
    {
        internal const int Monitor = 0, Camera = 1, Cursor = 2, Blueprint = 3, View = 4, Layout = 5, Play = 6, Pause = 7, Slow = 8, Fast = 9, Back10 = 10, Fwd10 = 11, Stop = 12, Prev = 13, Next = 14, Map = 15, Wave = 16, Keys = 17, Snow = 18, Fire = 19, PeakMark = 20, ClipIn = 21, ClipOut = 22, Gif = 23, Cross = 24, Check = 25, Star = 26, Folder = 27, Trash = 28, Share = 29, Copy = 30, Gear = 31, Sort = 32, Refresh = 33, Search = 34, Grid = 35, List = 36, Lines = 37, ChevR = 38, ChevD = 39, Group = 40, Swords = 41, Lock = 42, Plus = 43, Bolt = 44, Speaker = 45, Undo = 46, Eye = 47, Ring = 48, Palette = 49, Num = 50, Target = 51, PathI = 52, Bomb = 53, Sun = 54, Chat = 55, Chart = 56, Plate = 57, Import = 58, Export = 59, Diamond = 60, Bug = 61, News = 62, Pumpkin = 63, Image = 64, Globe = 65, Shield = 66;

        private static string _tip;
        internal static Rect TipAnchor { get { return _tipAnchor; } }

        /// <summary>Hover tooltip for a custom-drawn control. The tip is shown next to the mouse cursor (screen space), so it lands right
        /// even inside scroll views and groups.</summary>
        internal static void Hint(Rect r, string tip) { if (r.Contains(Event.current.mousePosition) && Event.current.type == EventType.Repaint) { _tip = tip; _tipAnchor = r; _tipMouse = GUIUtility.GUIToScreenPoint(Event.current.mousePosition); } }
        private static Rect _tipAnchor;
        private static Vector2 _tipMouse;
        private static string _pend;
        private static GUIStyle _tipSt;
        private static int _tipFs;
        private static readonly GUIContent TipContent = new GUIContent();

        /// <summary>Hint for a control inside a scroll view: hit = local rect (mouse is local there), anchor = the same rect in window space.</summary>
        internal static void Hint(Rect hit, Rect anchor, string tip) { if (hit.Contains(Event.current.mousePosition) && Event.current.type == EventType.Repaint) { _tip = tip; _tipAnchor = anchor; _tipMouse = GUIUtility.GUIToScreenPoint(Event.current.mousePosition); } }

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
            // stepped pixel triangle: 4 columns of 8, 6, 4, 2 units, no overlaps
            const int n = 4;
            float cw = 3.6f * p / n;
            for (int i = 0; i < n; i++)
            {
                float h = (8 - 2 * i) * p;
                R(x + (right ? i : n - 1 - i) * cw, y + 4f * p - h * 0.5f, cw, h, c);
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
                    for (int i = 0; i < 6; i++) R(x + p, y + i * 1.1f * p, (i + 1) * 1.1f * p, 1.1f * p, c);
                    R(x + 2.2f * p, y + 6.6f * p, 1.8f * p, 1.4f * p, c);
                    break;
                case Swords:
                    for (int i = 0; i < 8; i++) { R(x + i * p, y + i * p, p, p, c); R(x + (7 - i) * p, y + i * p, p, p, c); }
                    R(x + 6f * p, y + 5f * p, p, p, c); R(x + 5f * p, y + 6f * p, p, p, c); R(x + p, y + 5f * p, p, p, c); R(x + 2f * p, y + 6f * p, p, p, c);
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
                case Keys:
                    Frame(x, y + 1.5f * p, 8f * p, 5f * p, p * 0.8f, c);
                    for (int i = 0; i < 3; i++) R(x + (1.2f + i * 2.2f) * p, y + 2.6f * p, 1.2f * p, 1.2f * p, c);
                    R(x + 2f * p, y + 4.6f * p, 4f * p, 0.9f * p, c);
                    break;
                case Snow:
                    R(x + 3.5f * p, y, p, 8f * p, c);
                    R(x, y + 3.5f * p, 8f * p, p, c);
                    R(x + 1.2f * p, y + 1.2f * p, p, p, c); R(x + 5.8f * p, y + 1.2f * p, p, p, c);
                    R(x + 1.2f * p, y + 5.8f * p, p, p, c); R(x + 5.8f * p, y + 5.8f * p, p, p, c);
                    R(x + 2.3f * p, y + 2.3f * p, 3.4f * p, 3.4f * p, new Color(c.r, c.g, c.b, c.a * 0.55f));
                    break;
                case Fire:
                    R(x + 1.5f * p, y + 5.6f * p, 5f * p, 2.4f * p, c);
                    R(x + 1f * p, y + 3.6f * p, 4f * p, 2.2f * p, c);
                    R(x + 2.6f * p, y + 1.6f * p, 3.2f * p, 2.2f * p, c);
                    R(x + 3.4f * p, y, 1.6f * p, 1.8f * p, c);
                    R(x + 5f * p, y + 3.2f * p, 1.8f * p, 2.6f * p, c);
                    break;
                case PeakMark:
                    R(x + 3.5f * p, y, p, p, c);
                    R(x + 2.5f * p, y + p, 3f * p, p, c);
                    R(x + 1.5f * p, y + 2f * p, 5f * p, p, c);
                    R(x + 0.5f * p, y + 3f * p, 7f * p, p, c);
                    R(x + 2.5f * p, y + 4f * p, 3f * p, 3.8f * p, c);
                    break;
                case Wave:
                {
                    float[] hs = { 5f, 2.5f, 4f, 1f, 3.5f, 6f, 3f };
                    for (int i = 0; i < hs.Length; i++) R(x + i * 1.15f * p, y + (7f - hs[i]) * p, p * 0.9f, (hs[i] + 1f) * p, c);
                    break;
                }
                case Map:
                    Frame(x, y, 8f * p, 8f * p, p * 0.8f, c);
                    R(x + 1.6f * p, y + 4.6f * p, 2.2f * p, 1.8f * p, c);
                    R(x + 4.6f * p, y + 1.6f * p, 1.8f * p, 2.2f * p, c);
                    break;
                case ClipIn:
                    R(x + 1.5f * p, y, p, 8f * p, c); R(x + 1.5f * p, y, 3.5f * p, p, c); R(x + 1.5f * p, y + 7f * p, 3.5f * p, p, c);
                    Tri(x + 4.2f * p, y, p, true, c);
                    break;
                case ClipOut:
                    R(x + 5.5f * p, y, p, 8f * p, c); R(x + 2f * p, y, 4.5f * p, p, c); R(x + 2f * p, y + 7f * p, 4.5f * p, p, c);
                    Tri(x - 0.2f * p, y, p, true, c);
                    break;
                case Gif:
                    Frame(x, y + p, 8f * p, 6f * p, p * 0.8f, c);
                    for (int i = 0; i < 3; i++) { R(x + (1f + i * 2.4f) * p, y + p, 0.9f * p, 0.9f * p, c); R(x + (1f + i * 2.4f) * p, y + 6.1f * p, 0.9f * p, 0.9f * p, c); }
                    R(x + 3.4f * p, y + 3f * p, 1.4f * p, 2f * p, c);
                    break;
                case Star:
                    R(x + 3.5f * p, y, p, p, c); R(x + 2.5f * p, y + p, 3f * p, p, c); R(x, y + 2f * p, 8f * p, 1.3f * p, c);
                    R(x + p, y + 3.3f * p, 6f * p, p, c); R(x + 1.5f * p, y + 4.3f * p, 5f * p, p, c);
                    R(x + p, y + 5.3f * p, 2.2f * p, 2.4f * p, c); R(x + 4.8f * p, y + 5.3f * p, 2.2f * p, 2.4f * p, c);
                    break;
                case Folder:
                    R(x, y + 1.2f * p, 3.5f * p, 1.2f * p, c); R(x, y + 2.4f * p, 8f * p, 5f * p, c);
                    break;
                case Trash:
                    R(x + 3f * p, y, 2f * p, p, c); R(x + p, y + p, 6f * p, p, c);
                    Frame(x + 1.6f * p, y + 2.2f * p, 4.8f * p, 5.8f * p, p * 0.8f, c);
                    R(x + 3.6f * p, y + 3.4f * p, 0.8f * p, 3.4f * p, c);
                    break;
                case Share:
                    R(x + 3.5f * p, y, p, p, c); R(x + 2.5f * p, y + p, 3f * p, p, c); R(x + 1.5f * p, y + 2f * p, 5f * p, p, c);
                    R(x + 3.5f * p, y + 3f * p, p, 3.4f * p, c); R(x + 0.5f * p, y + 7f * p, 7f * p, p, c);
                    break;
                case Copy:
                    Frame(x, y, 5f * p, 5.5f * p, p * 0.8f, c); Frame(x + 3f * p, y + 2.5f * p, 5f * p, 5.5f * p, p * 0.8f, c);
                    break;
                case Gear:
                    Frame(x + 1.2f * p, y + 1.2f * p, 5.6f * p, 5.6f * p, p * 1.6f, c);
                    R(x + 3.4f * p, y, 1.2f * p, 1.2f * p, c); R(x + 3.4f * p, y + 6.8f * p, 1.2f * p, 1.2f * p, c);
                    R(x, y + 3.4f * p, 1.2f * p, 1.2f * p, c); R(x + 6.8f * p, y + 3.4f * p, 1.2f * p, 1.2f * p, c);
                    break;
                case Sort:
                    R(x, y + p, 8f * p, p, c); R(x, y + 3.5f * p, 5.5f * p, p, c); R(x, y + 6f * p, 3f * p, p, c);
                    break;
                case Refresh:
                    Frame(x + p, y + p, 6f * p, 6f * p, p * 0.9f, c);
                    R(x + 4f * p, y, 4f * p, 2f * p, new Color(0.08f, 0.08f, 0.1f, 1f)); R(x + 4.5f * p, y, 3.5f * p, p, c); R(x + 7f * p, y, p, 3f * p, c);
                    break;
                case Search:
                    Frame(x, y, 5.5f * p, 5.5f * p, p * 0.9f, c); R(x + 5f * p, y + 5f * p, 1.4f * p, 1.4f * p, c); R(x + 6.2f * p, y + 6.2f * p, 1.8f * p, 1.8f * p, c);
                    break;
                case Grid:
                    R(x, y, 3.6f * p, 3.6f * p, c); R(x + 4.4f * p, y, 3.6f * p, 3.6f * p, c); R(x, y + 4.4f * p, 3.6f * p, 3.6f * p, c); R(x + 4.4f * p, y + 4.4f * p, 3.6f * p, 3.6f * p, c);
                    break;
                case List:
                    for (int i = 0; i < 3; i++) { R(x, y + (0.2f + i * 2.8f) * p, 2.2f * p, 2.2f * p, c); R(x + 3.2f * p, y + (0.7f + i * 2.8f) * p, 4.8f * p, 1.2f * p, c); }
                    break;
                case Lines:
                    R(x, y + 1f * p, 8f * p, p, c); R(x, y + 3.5f * p, 8f * p, p, c); R(x, y + 6f * p, 8f * p, p, c);
                    break;
                case ChevR:
                    Tri(x + 2.4f * p, y, p, true, c);
                    break;
                case ChevD:
                    for (int i = 0; i < 4; i++) R(x + i * p, y + (1.8f + i) * p, (8f - 2f * i) * p, p, c);
                    break;
                case Group:
                    R(x, y + p, 8f * p, p, c); R(x + 2f * p, y + 3.5f * p, 6f * p, p, c); R(x + 2f * p, y + 6f * p, 6f * p, p, c); R(x, y + 2f * p, p, 4.5f * p, c);
                    break;
                case Check:
                    for (int i = 0; i < 3; i++) R(x + (0.5f + i) * p, y + (3.5f + i) * p, p * 1.3f, p * 1.3f, c);
                    for (int i = 0; i < 5; i++) R(x + (2.8f + i) * p, y + (5.7f - i) * p, p * 1.3f, p * 1.3f, c);
                    break;
                case Cross:
                    for (int i = 0; i < 8; i++) { R(x + i * p, y + i * p, p, p, c); R(x + (7 - i) * p, y + i * p, p, p, c); }
                    break;
                case Speaker:
                    R(x, y + 2.5f * p, 2f * p, 3f * p, c); R(x + 2f * p, y + 1.5f * p, 1.5f * p, 5f * p, c); R(x + 3.5f * p, y + 0.5f * p, p, 7f * p, c);
                    R(x + 5.2f * p, y + 2.5f * p, p, 3f * p, c); R(x + 6.8f * p, y + 1.5f * p, p, 5f * p, c);
                    break;
                case Undo:
                    R(x + 1.2f * p, y + 1.2f * p, p, 3f * p, c); R(x + 0.2f * p, y + 2.2f * p, 3f * p, p, c);
                    R(x + 2.4f * p, y + 1.2f * p, 4.2f * p, p, c); R(x + 6f * p, y + 2.2f * p, p, 3f * p, c);
                    R(x + 3f * p, y + 5.2f * p, 4f * p, p, c);
                    break;
                case Lock:
                    R(x + 2f * p, y, 4f * p, p, c); R(x + 1.2f * p, y + p, p, 2.4f * p, c); R(x + 5.8f * p, y + p, p, 2.4f * p, c);
                    R(x, y + 3.6f * p, 8f * p, 4.4f * p, c);
                    break;
                case Plus:
                    R(x + 3.3f * p, y, 1.4f * p, 8f * p, c); R(x, y + 3.3f * p, 8f * p, 1.4f * p, c);
                    break;
                case Bolt:
                    R(x + 4f * p, y, 2f * p, p, c); R(x + 3f * p, y + p, 2f * p, p, c); R(x + 2f * p, y + 2f * p, 2f * p, p, c);
                    R(x + 1f * p, y + 3f * p, 5f * p, p, c); R(x + 3f * p, y + 4f * p, 2f * p, p, c); R(x + 2f * p, y + 5f * p, 2f * p, p, c); R(x + p, y + 6f * p, 2f * p, p, c);
                    break;
                case Eye:
                    R(x + 2f * p, y + 1.5f * p, 4f * p, p, c); R(x + p, y + 2.5f * p, p, p, c); R(x + 6f * p, y + 2.5f * p, p, p, c); R(x, y + 3.5f * p, p, p, c); R(x + 7f * p, y + 3.5f * p, p, p, c);
                    R(x + p, y + 4.5f * p, p, p, c); R(x + 6f * p, y + 4.5f * p, p, p, c); R(x + 2f * p, y + 5.5f * p, 4f * p, p, c); R(x + 3f * p, y + 3f * p, 2f * p, 2f * p, c);
                    break;
                case Ring:
                    R(x + 2f * p, y, 4f * p, p, c); R(x + 2f * p, y + 7f * p, 4f * p, p, c); R(x, y + 2f * p, p, 4f * p, c); R(x + 7f * p, y + 2f * p, p, 4f * p, c);
                    R(x + p, y + p, p, p, c); R(x + 6f * p, y + p, p, p, c); R(x + p, y + 6f * p, p, p, c); R(x + 6f * p, y + 6f * p, p, p, c);
                    break;
                case Palette:
                    R(x, y, 3.5f * p, 3.5f * p, c); R(x + 4.5f * p, y, 3.5f * p, 3.5f * p, new Color(c.r, c.g, c.b, c.a * 0.7f));
                    R(x, y + 4.5f * p, 3.5f * p, 3.5f * p, new Color(c.r, c.g, c.b, c.a * 0.5f)); R(x + 4.5f * p, y + 4.5f * p, 3.5f * p, 3.5f * p, new Color(c.r, c.g, c.b, c.a * 0.3f));
                    break;
                case Num:
                    R(x + 2.5f * p, y, p, 8f * p, c); R(x + 5f * p, y, p, 8f * p, c); R(x, y + 2.5f * p, 8f * p, p, c); R(x, y + 5f * p, 8f * p, p, c);
                    break;
                case Target:
                    R(x + 2f * p, y + p, 4f * p, p, c); R(x + 2f * p, y + 6f * p, 4f * p, p, c); R(x + p, y + 2f * p, p, 4f * p, c); R(x + 6f * p, y + 2f * p, p, 4f * p, c);
                    R(x + 3.3f * p, y + 3.3f * p, 1.4f * p, 1.4f * p, c); R(x + 3.5f * p, y, p, p, c); R(x + 3.5f * p, y + 7f * p, p, p, c); R(x, y + 3.5f * p, p, p, c); R(x + 7f * p, y + 3.5f * p, p, p, c);
                    break;
                case PathI:
                    R(x, y + 6f * p, 1.6f * p, 1.6f * p, c); R(x + 2.4f * p, y + 4.4f * p, 1.4f * p, 1.4f * p, c); R(x + 4.4f * p, y + 3.4f * p, 1.4f * p, 1.4f * p, c); R(x + 6.2f * p, y, 1.8f * p, 1.8f * p, c);
                    R(x + 5.4f * p, y + 1.8f * p, p, 1.2f * p, c);
                    break;
                case Bomb:
                    R(x + 2f * p, y + 3f * p, 4f * p, p, c); R(x + p, y + 4f * p, 6f * p, 3f * p, c); R(x + 2f * p, y + 7f * p, 4f * p, p, c);
                    R(x + 5f * p, y + 2f * p, p, p, c); R(x + 6f * p, y + p, p, p, c); R(x + 7f * p, y, p, p, c);
                    break;
                case Sun:
                    R(x + 2.5f * p, y + 2.5f * p, 3f * p, 3f * p, c); R(x + 3.5f * p, y, p, 1.5f * p, c); R(x + 3.5f * p, y + 6.5f * p, p, 1.5f * p, c);
                    R(x, y + 3.5f * p, 1.5f * p, p, c); R(x + 6.5f * p, y + 3.5f * p, 1.5f * p, p, c);
                    break;
                case Chat:
                    R(x, y + p, 8f * p, 5f * p, c); R(x + p, y + 6f * p, 2f * p, p, c); R(x + p, y + 7f * p, p, p, c);
                    R(x + 1.5f * p, y + 2.4f * p, 5f * p, 0.8f * p, new Color(0.08f, 0.08f, 0.1f, 1f)); R(x + 1.5f * p, y + 4f * p, 3f * p, 0.8f * p, new Color(0.08f, 0.08f, 0.1f, 1f));
                    break;
                case Chart:
                    R(x, y + 5f * p, 2f * p, 3f * p, c); R(x + 3f * p, y + 2f * p, 2f * p, 6f * p, c); R(x + 6f * p, y + 4f * p, 2f * p, 4f * p, c);
                    break;
                case Plate:
                    Frame(x, y + 2f * p, 8f * p, 4f * p, p * 0.8f, c); R(x + 1.5f * p, y + 3.5f * p, 3.5f * p, p, c);
                    break;
                case Import:
                    R(x + 3.5f * p, y, p, 4f * p, c); R(x + 2f * p, y + 3f * p, 4f * p, p, c); R(x + 3f * p, y + 4f * p, 2f * p, p, c);
                    R(x, y + 6.5f * p, 8f * p, p, c); R(x, y + 5.5f * p, p, p, c); R(x + 7f * p, y + 5.5f * p, p, p, c);
                    break;
                case Export:
                    R(x + 3.5f * p, y + 2f * p, p, 4f * p, c); R(x + 3.5f * p, y, p, p, c); R(x + 2.5f * p, y + p, 3f * p, p, c); R(x + 1.5f * p, y + 2f * p, 5f * p, p, c);
                    R(x, y + 6.5f * p, 8f * p, p, c); R(x, y + 5.5f * p, p, p, c); R(x + 7f * p, y + 5.5f * p, p, p, c);
                    break;
                case Diamond:
                    R(x + 3f * p, y, 2f * p, p, c); R(x + 2f * p, y + p, 4f * p, p, c); R(x + p, y + 2f * p, 6f * p, p, c); R(x, y + 3f * p, 8f * p, p, c);
                    R(x + p, y + 4f * p, 6f * p, p, c); R(x + 2f * p, y + 5f * p, 4f * p, p, c); R(x + 3f * p, y + 6f * p, 2f * p, p, c);
                    break;
                case Bug:
                    R(x + p, y, p, p, c); R(x + 6f * p, y, p, p, c); R(x + 2f * p, y + p, 4f * p, p, c);
                    R(x + 2f * p, y + 2f * p, 4f * p, 5f * p, c); R(x + 3f * p, y + 7f * p, 2f * p, p, c);
                    R(x, y + 3f * p, 2f * p, p, c); R(x, y + 5f * p, 2f * p, p, c); R(x + 6f * p, y + 3f * p, 2f * p, p, c); R(x + 6f * p, y + 5f * p, 2f * p, p, c);
                    break;
                case News:
                    Frame(x, y, 7f * p, 8f * p, p, c);
                    R(x + 2f * p, y + 2f * p, 3f * p, p, c); R(x + 2f * p, y + 4f * p, 3f * p, p, c); R(x + 2f * p, y + 6f * p, 1.5f * p, p, c);
                    break;
                case Pumpkin:
                    R(x + 3.2f * p, y, 1.6f * p, 1.2f * p, c);
                    R(x + p, y + 1.2f * p, 6f * p, 1.2f * p, c);
                    R(x, y + 2.4f * p, 2f * p, 1.2f * p, c); R(x + 3.4f * p, y + 2.4f * p, 1.2f * p, 1.2f * p, c); R(x + 6f * p, y + 2.4f * p, 2f * p, 1.2f * p, c);
                    R(x, y + 3.6f * p, 8f * p, 1.2f * p, c);
                    R(x, y + 4.8f * p, 1.6f * p, 1.2f * p, c); R(x + 2.8f * p, y + 4.8f * p, 1.4f * p, 1.2f * p, c); R(x + 5.4f * p, y + 4.8f * p, 2.6f * p, 1.2f * p, c);
                    R(x + p, y + 6f * p, 6f * p, 1.2f * p, c);
                    R(x + 2f * p, y + 7.2f * p, 4f * p, 0.8f * p, c);
                    break;
                case Image:
                    Frame(x, y + p, 8f * p, 6f * p, p * 0.8f, c);
                    R(x + 5.4f * p, y + 2.2f * p, 1.2f * p, 1.2f * p, c);
                    R(x + 2.6f * p, y + 3.8f * p, 1.2f * p, 0.9f * p, c);
                    R(x + 1.7f * p, y + 4.7f * p, 3f * p, 1.5f * p, c);
                    break;
                case Globe:
                    R(x + 2f * p, y, 4f * p, p, c); R(x + 2f * p, y + 7f * p, 4f * p, p, c); R(x, y + 2f * p, p, 4f * p, c); R(x + 7f * p, y + 2f * p, p, 4f * p, c);
                    R(x + p, y + p, p, p, c); R(x + 6f * p, y + p, p, p, c); R(x + p, y + 6f * p, p, p, c); R(x + 6f * p, y + 6f * p, p, p, c);
                    R(x + 3.5f * p, y + p, p, 6f * p, c); R(x + p, y + 3.5f * p, 6f * p, p, c); R(x + 2.1f * p, y + 1.8f * p, 0.8f * p, 4.4f * p, c); R(x + 5.1f * p, y + 1.8f * p, 0.8f * p, 4.4f * p, c);
                    break;
                case Shield:
                    R(x, y, 8f * p, p, c); R(x, y + p, p, 3.5f * p, c); R(x + 7f * p, y + p, p, 3.5f * p, c);
                    R(x + p, y + 4.5f * p, p, 1.2f * p, c); R(x + 6f * p, y + 4.5f * p, p, 1.2f * p, c);
                    R(x + 2f * p, y + 5.7f * p, p, p, c); R(x + 5f * p, y + 5.7f * p, p, p, c); R(x + 3f * p, y + 6.7f * p, 2f * p, 1.3f * p, c);
                    R(x + 3.5f * p, y + 2f * p, p, 3.5f * p, c); R(x + 2.2f * p, y + 3.2f * p, 3.6f * p, p, c);
                    break;
                case Layout:
                    R(x, y, 5f * p, 4f * p, c);
                    R(x + 5.7f * p, y, 2.3f * p, 4f * p, c);
                    R(x, y + 4.7f * p, 8f * p, 3.3f * p, c);
                    break;
            }
        }

        /// <summary>A bare glyph (no button background), e.g. the marks over the APM wave.</summary>
        internal static void Glyph(int kind, Rect r, Color c) { Draw(kind, r, c); }

        /// <summary>Square icon toggle; tip is shown above the bar while hovered. Returns true on click.</summary>
        internal static bool Button(Rect r, int kind, bool on, string tip)
        {
            Event e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            BtcReplayUI.Fill(r, on ? BtcMenu.AccentA(0.18f) : new Color(1f, 1f, 1f, hover ? 0.14f : 0.07f));
            Draw(kind, r, on ? BtcMenu.AccentA(0.95f) : new Color(1f, 1f, 1f, hover ? 0.9f : 0.55f));
            if (hover && e.type == EventType.Repaint) { _tip = tip; _tipAnchor = r; _tipMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y); }
            bool clicked = BtcSfx.Btn(r, "", BtcReplayUI.sBtn);
            if (clicked) BtcSfx.Switch(!on);
            return clicked;
        }

        /// <summary>Icon button in a fixed colour (transport controls).</summary>
        internal static bool Plain(Rect r, int kind, Color c, string tip)
        {
            Event e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, hover ? 0.14f : 0.07f));
            Draw(kind, r, hover ? new Color(c.r, c.g, c.b, 1f) : new Color(c.r, c.g, c.b, c.a * 0.85f));
            if (hover && e.type == EventType.Repaint) { _tip = tip; _tipAnchor = r; _tipMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y); }
            return BtcSfx.Btn(r, "", BtcReplayUI.sBtn);
        }

        /// <summary>Call once after the bar is drawn (top level, outside the mod menu): draws the tip right away.</summary>
        internal static void DrawTip(int fs)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (_tip != null) Paint(_tip, fs);
            _tip = null;
        }

        /// <summary>Menu tabs: hands the tip over to the menu, which paints it last on top of everything (<see cref="FlushTip"/>), so a
        /// scroll view or a group never clips it. maxX / below are kept for old callers; the tip follows the cursor now.</summary>
        internal static void DrawTip(int fs, float maxX, bool below)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (_tip != null) _pend = _tip;
            _tip = null;
        }

        /// <summary>Paints the pending tip of the menu (call once at top level after the whole menu).</summary>
        internal static void FlushTip(int fs)
        {
            if (Event.current.type != EventType.Repaint) return;
            string t = _pend ?? _tip;
            _pend = null; _tip = null;
            if (t != null) Paint(t, fs);
        }

        // next to the cursor (right-below, flipped near the screen edges), wrapped to a readable width
        private static void Paint(string tip, int fs)
        {
            if (string.IsNullOrEmpty(tip)) return;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            if (b == null) return;
            if (_tipSt == null || _tipFs != fs)
            {
                _tipFs = fs;
                _tipSt = new GUIStyle(b);
                _tipSt.fontSize = Mathf.RoundToInt(fs * 0.9f);
                _tipSt.wordWrap = true;
                _tipSt.clipping = TextClipping.Overflow;
                _tipSt.alignment = TextAnchor.UpperLeft;
                _tipSt.padding = new RectOffset(0, 0, 0, 0);
                _tipSt.normal.textColor = Color.white;
            }
            TipContent.text = tip;
            float padX = fs * 0.6f, padY = fs * 0.3f;
            float maxW = Mathf.Min(fs * 22f, Screen.width * 0.45f);
            _tipSt.wordWrap = false;
            float tw = Mathf.Min(_tipSt.CalcSize(TipContent).x + 2f, maxW);
            _tipSt.wordWrap = true;
            float th = _tipSt.CalcHeight(TipContent, tw);
            float w = tw + padX * 2f, h = th + padY * 2f;
            Vector2 m = GUIUtility.ScreenToGUIPoint(_tipMouse);
            float x = m.x + fs * 0.9f, y = m.y + fs * 1.1f;
            if (x + w > Screen.width - 4f) x = m.x - w - fs * 0.4f;
            if (y + h > Screen.height - 4f) y = m.y - h - fs * 0.4f;
            x = Mathf.Clamp(x, 4f, Mathf.Max(4f, Screen.width - w - 4f));
            y = Mathf.Clamp(y, 4f, Mathf.Max(4f, Screen.height - h - 4f));
            Rect t = new Rect(x, y, w, h);
            BtcReplayUI.Fill(t, new Color(0f, 0f, 0f, 0.92f));
            BtcReplayUI.Fill(new Rect(t.x, t.y, 2f, t.height), BtcMenu.Accent);
            GUI.Label(new Rect(t.x + padX, t.y + padY, tw, th), TipContent, _tipSt);
        }
    }
}
