using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Short bottom-centre message (IMGUI), drawn from ResourcePackManager.OnGUI via BtcSessionTick.</summary>
    internal static class BtcToast
    {
        private static string _text;
        private static float _until;
        private static GUIStyle _style;
        private static int _fs;

        internal static void Show(string text)
        {
            _text = text;
            _until = Time.unscaledTime + 7f;
        }

        internal static void OnGUI()
        {
            if (_text == null) return;
            if (Time.unscaledTime > _until)
            {
                _text = null;
                return;
            }
            Event e = Event.current;
            if (e == null || e.type != EventType.Repaint) return;
            int fs = Mathf.Max(13, Screen.height / 45);
            if (_style == null || _fs != fs)
            {
                _fs = fs;
                _style = new GUIStyle(GUI.skin.label);
                _style.fontSize = fs;
                _style.alignment = TextAnchor.MiddleCenter;
                _style.wordWrap = true;
                _style.normal.textColor = Color.white;
            }
            float w = Mathf.Min(Screen.width * 0.6f, fs * 40f);
            float h = fs * 2.6f;
            Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height - h - fs * 4f, w, h);
            Color old = GUI.color;
            GUI.color = new Color(0.08f, 0.2f, 0.1f, 0.92f);
            GUI.DrawTexture(r, BtcSymmetry.White());
            GUI.color = old;
            GUI.Label(r, _text, _style);
        }
    }

    /// <summary>Per-frame driver for the session features (editor share, spawn pick, replay delivery) + toast.</summary>
    internal static class BtcSessionTick
    {
        private static int _frame = -1;

        internal static void OnGUI()
        {
            Event e = Event.current;
            if (e != null && e.type == EventType.Repaint && _frame != Time.frameCount)
            {
                _frame = Time.frameCount;
                try
                {
                    BtcEditorShare.Tick();
                    BtcSpawnPick.Tick();
                    BtcReplayAuto.Tick();
                    BtcGameChatFade.Tick();
                }
                catch (System.Exception ex)
                {
                    BtcLog.Error("Session", "tick: " + ex);
                }
            }
            BtcGameChatFade.OnGUI(e);
            BtcToast.OnGUI();
        }
    }
}
