using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// "Jump to this setting" for the global search and the clickable links of the menu: the caller opens the tab / page / fold and calls
    /// <see cref="Target"/>; the row with that key reports where it is drawn (<see cref="Row"/>), the tab scrolls it into view once
    /// (<see cref="Scroll"/>, before its BeginScrollView) and an accent frame pulses around the row for a short while.
    /// Never writes anything into the search fields of the tabs.
    /// </summary>
    internal static class BtcJump
    {
        private const float PulseTime = 2.5f, PinTime = 30f;
        private static string _scope, _key;   // scope = tab family ("vis", "gp"), key = setting id; kept apart so rows compare without concatenating
        private static float _until, _pinUntil, _rowY;
        private static int _state;   // 0 done, 1 waiting for the row, 2 row y known (scroll on the next pass)

        internal static void Target(string scope, string key)
        {
            _scope = scope;
            _key = key;
            _until = Time.unscaledTime + PulseTime;
            _pinUntil = Time.unscaledTime + PinTime;
            _rowY = 0f;
            _state = 1;
        }

        /// <summary>True for a short while after the jump: the row is shown even if Simple mode or a parent switch would hide it.</summary>
        internal static bool Pinned(string scope, string key) { return Is(scope, key) && Time.unscaledTime < _pinUntil; }

        private static bool Is(string scope, string key) { return _key != null && string.Equals(key, _key) && string.Equals(scope, _scope); }

        /// <summary>Rows call this with their rect (scroll content space): reports the position once and draws the pulse.</summary>
        internal static void Row(string scope, string key, Rect r)
        {
            if (!Is(scope, key)) return;
            if (_state == 1 && Event.current.type == EventType.Repaint) { _rowY = r.y; _state = 2; }
            Pulse(r, _until);
        }

        /// <summary>Tabs call this right before their BeginScrollView: brings the row reported last pass into view (once).</summary>
        internal static void Scroll(ref Vector2 scroll, float viewH)
        {
            if (_state != 2) return;
            scroll.y = Mathf.Max(0f, _rowY - viewH * 0.35f);
            _state = 0;
            _until = Time.unscaledTime + PulseTime;   // the pulse starts when the row is actually on screen
        }

        /// <summary>Pulsing accent frame that fades out at <paramref name="until"/>.</summary>
        internal static void Pulse(Rect r, float until)
        {
            if (Event.current.type != EventType.Repaint) return;
            float left = until - Time.unscaledTime;
            if (left <= 0f) return;
            float fade = Mathf.Clamp01(left / 0.6f);
            float p = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 9f);
            Color a = BtcMenu.Accent;
            Color c = new Color(a.r, a.g, a.b, fade * p);
            BtcReplayUI.Fill(r, new Color(a.r, a.g, a.b, 0.1f * fade * p));
            BtcReplayUI.Fill(new Rect(r.x, r.y, r.width, 2f), c);
            BtcReplayUI.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), c);
            BtcReplayUI.Fill(new Rect(r.x, r.y, 2f, r.height), c);
            BtcReplayUI.Fill(new Rect(r.xMax - 2f, r.y, 2f, r.height), c);
        }
    }
}
