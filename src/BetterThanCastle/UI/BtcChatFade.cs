using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle
{
    /// <summary>
    /// In-game chat box (ChatBoxDriver, matches only; the lobby chat is untouched): messages are shown for ChatFadeSeconds,
    /// then hidden. While the chat is open (driver active) the history (100 lines) is visible in a 10-line window that can be
    /// scrolled with the mouse wheel or the scrollbar left of the chat. The vanilla output Text is rewritten from our own
    /// timestamped copy. Local UI only, no network traffic.
    /// </summary>
    internal static class BtcGameChatFade
    {
        private const int MaxLines = 100;
        private const int Window = 10;
        private static readonly List<string> _lines = new List<string>();
        private static readonly List<float> _times = new List<float>();
        private static readonly StringBuilder _sb = new StringBuilder(512);
        private static readonly System.Reflection.FieldInfo _fActive = AccessTools.Field(typeof(ChatBoxDriver), "_isActive");
        private static readonly System.Reflection.FieldInfo _fInit = AccessTools.Field(typeof(ChatBoxDriver), "initialized");
        private static readonly System.Reflection.MethodInfo _setActive = AccessTools.PropertySetter(typeof(ChatBoxDriver), "isActive");
        private static int _key = -1;
        private static string _shown;
        private static int _scroll; // lines above the newest one
        private static bool _open;
        private static bool _drag;
        private static bool _reopen;
        private static float _reopenUntil;
        private static Rect _bar;
        private static bool _haveBar;

        private static bool On { get { return BtcConfig.ChatFade != null && BtcConfig.ChatFade.Value; } }

        private static Text OutText(ChatBoxDriver d)
        {
            if (d == null || d.chatPanel == null) return null;
            return d.chatPanel.GetPlugComponent<Text>("Output Text");
        }

        internal static void Reset(ChatBoxDriver d)
        {
            _lines.Clear();
            _times.Clear();
            _key = -1;
            _shown = null;
            _scroll = 0;
            Text t = OutText(d);
            if (t != null && !string.IsNullOrEmpty(t.text))
            {
                _lines.Add(t.text.TrimEnd('\r', '\n'));
                _times.Add(Time.unscaledTime);
            }
        }

        internal static void Add(ChatBoxDriver d, string msg)
        {
            _lines.Add(msg);
            _times.Add(Time.unscaledTime);
            if (_lines.Count > MaxLines) { _lines.RemoveAt(0); _times.RemoveAt(0); if (_scroll > 0) _scroll--; }
            if (_scroll > 0) _scroll++; // keep the lines being read in place
            ClampScroll();
            _key = -1;
        }

        private static void ClampScroll()
        {
            int max = Mathf.Max(0, _lines.Count - Window);
            if (_scroll > max) _scroll = max;
            if (_scroll < 0) _scroll = 0;
        }

        /// <summary>Once per frame (BtcSessionTick).</summary>
        internal static void Tick()
        {
            _haveBar = false;
            if (!On || _lines.Count == 0) return;
            ChatBoxDriver d = ChatBoxDriver.Instance;
            if (d == null || _fInit == null || !(bool)_fInit.GetValue(d)) return;
            bool open = _fActive != null && (bool)_fActive.GetValue(d);
            float now = Time.unscaledTime;
            // a click on the scrollbar makes the uGUI input field lose focus, which closes the chat: open it again
            if (!open && _reopen && now < _reopenUntil && _setActive != null)
            {
                _setActive.Invoke(d, new object[] { true });
                open = true;
            }
            if (!open) { _scroll = 0; _drag = false; _reopen = false; }
            _open = open;
            Text t = OutText(d);
            if (t == null) return;
            if (open)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (wheel != 0f) { _scroll += wheel > 0f ? 2 : -2; ClampScroll(); }
                if (_lines.Count > Window) _haveBar = BarRect(t, out _bar);
            }
            float life = BtcConfig.ChatFadeSeconds.Value;
            int end = _lines.Count - _scroll, start = Mathf.Max(0, end - Window);
            int visible = 0;
            if (!open) for (int i = start; i < end; i++) if (now - _times[i] <= life) visible++;
            int key = (open ? 1 : 0) | (visible << 1) | (_lines.Count << 8) | (_scroll << 18);
            if (key == _key && t.text == _shown) return;
            _key = key;
            _sb.Length = 0;
            for (int i = start; i < end; i++)
            {
                if (!open && now - _times[i] > life) continue;
                _sb.Append(_lines[i]).Append(System.Environment.NewLine);
            }
            _shown = _sb.ToString();
            t.text = _shown;
        }

        /// <summary>Scrollbar rect (IMGUI space) just left of the chat text.</summary>
        private static bool BarRect(Text t, out Rect r)
        {
            r = default(Rect);
            RectTransform rt = t.rectTransform;
            Canvas c = t.canvas;
            if (rt == null || c == null) return false;
            Vector3[] w = new Vector3[4];
            rt.GetWorldCorners(w);
            Camera cam = c.renderMode == RenderMode.ScreenSpaceOverlay ? null : (c.worldCamera != null ? c.worldCamera : Camera.main);
            Vector2 bl = cam != null ? (Vector2)cam.WorldToScreenPoint(w[0]) : (Vector2)w[0];
            Vector2 tr = cam != null ? (Vector2)cam.WorldToScreenPoint(w[2]) : (Vector2)w[2];
            float h = tr.y - bl.y;
            if (h < 20f) return false;
            r = new Rect(bl.x - 18f, Screen.height - tr.y, 16f, h);
            return true;
        }

        internal static void OnGUI(Event e)
        {
            if (!_haveBar || !_open || e == null) return;
            if (e.type == EventType.MouseDown && _bar.Contains(e.mousePosition)) { _drag = true; _reopen = true; _reopenUntil = Time.unscaledTime + 0.5f; }
            else if (e.type == EventType.MouseUp && _drag) { _drag = false; _reopen = true; _reopenUntil = Time.unscaledTime + 0.5f; }
            else if (_drag) _reopenUntil = Time.unscaledTime + 0.5f;
            int max = Mathf.Max(0, _lines.Count - Window);
            float pos = max - _scroll;
            float nv = GUI.VerticalScrollbar(_bar, pos, Window, 0f, _lines.Count);
            int ns = max - Mathf.Clamp(Mathf.RoundToInt(nv), 0, max);
            if (ns != _scroll) { _scroll = ns; _key = -1; }
        }

        internal static void DrawSettings(System.Func<string, bool, bool> check, BtcInterface.SliderFn slider)
        {
            bool ru = BtcLang.Russian;
            if (BtcConfig.ChatFade == null) return;
            if (check(ru ? "Скрывать сообщения игрового чата через время (открой чат: история, прокрутка колесом/полосой слева)" : "Hide in-game chat messages after a while (open the chat: history, scroll with the wheel or the bar on the left)", BtcConfig.ChatFade.Value))
                BtcConfig.ChatFade.Value = !BtcConfig.ChatFade.Value;
            float v = slider(ru ? "Сколько секунд висит сообщение" : "Seconds a message stays visible", 9710, BtcConfig.ChatFadeSeconds.Value, 2f, 60f, 1f, "0", string.Empty);
            if (Mathf.Abs(v - BtcConfig.ChatFadeSeconds.Value) > 0.01f) { BtcConfig.ChatFadeSeconds.Value = v; _key = -1; }
        }
    }
}
