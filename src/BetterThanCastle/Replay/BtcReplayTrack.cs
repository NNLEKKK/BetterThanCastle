using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// The replay progress line in the new look: a thin line that swells under the pointer, time ticks under it, the played part in the accent
    /// colour (amber while seeking) and a slim playhead with a soft halo. The old 22 px slab is only the hit area now; the marks (BtcAutoMarks,
    /// BtcTimer) attach to Line().
    /// </summary>
    internal static class BtcReplayTrack
    {
        private static float _th = 3f;
        private static readonly float[] Steps = { 5f, 10f, 30f, 60f, 120f, 300f, 600f, 1800f, 3600f };

        /// <summary>The thin line inside the hit area r (what the marks sit on).</summary>
        internal static Rect Line(Rect r) { return new Rect(r.x, r.center.y - 2f, r.width, 4f); }

        internal static void Draw(Rect r, float frac, bool seeking, bool hot, float total, int fs)
        {
            if (Event.current.type != EventType.Repaint) return;
            float target = hot ? 7f : 3f;
            _th += (target - _th) * Mathf.Clamp01(Time.unscaledDeltaTime * 14f);
            float yc = r.center.y, th = _th;
            Color acc = seeking ? new Color(1f, 0.75f, 0.2f, 1f) : BtcMenu.Accent;
            // time ticks under the line: the smallest step that keeps them about 3 font sizes apart
            float minGap = fs * 3.2f, step = Steps[Steps.Length - 1];
            for (int i = 0; i < Steps.Length; i++) if (r.width * Steps[i] / Mathf.Max(1f, total) >= minGap) { step = Steps[i]; break; }
            for (float t = step; t < total; t += step)
            {
                float x = r.x + r.width * t / total;
                bool major = ((int)(t / step) % 5) == 0;
                BtcReplayUI.Fill(new Rect(x - 0.5f, yc + th * 0.5f + 3f, 1f, major ? 6f : 3f), new Color(1f, 1f, 1f, major ? 0.3f : 0.18f));
            }
            BtcReplayUI.Fill(new Rect(r.x, yc - th * 0.5f, r.width, th), new Color(1f, 1f, 1f, hot ? 0.2f : 0.14f));
            float px = r.x + r.width * frac;
            BtcReplayUI.Fill(new Rect(r.x, yc - th * 0.5f, r.width * frac, th), new Color(acc.r, acc.g, acc.b, 0.95f));
            // playhead: halo + slim bar
            BtcReplayUI.Fill(new Rect(px - 7f, yc - 11f, 14f, 22f), new Color(acc.r, acc.g, acc.b, hot ? 0.22f : 0.12f));
            BtcReplayUI.Fill(new Rect(px - 1.5f, yc - 9f, 3f, 18f), Color.white);
        }
    }
}
