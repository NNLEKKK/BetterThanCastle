using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Short message at the top (menu-style notice, see BtcHud), drawn from ResourcePackManager.OnGUI via BtcSessionTick.</summary>
    internal static class BtcToast
    {
        internal static void Show(string text)
        {
            BtcHud.Notice(text);
        }

        internal static void OnGUI()
        {
            BtcHud.DrawNotices();
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
