using System;
using Brix.Engine;
using Brix.Network;
using Brix.UI.Builder.Menu;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
    /// <summary>
    /// "Host game" button on the first screen of the main menu: opens a multiplayer lobby in one click, by the same vanilla call as the
    /// Host button of the multiplayer screen (NetworkWaitingModalMenu.OpenAsHostSignal -> StartHostMultiplayer -> LobbyMenu). Local UI only,
    /// nothing on the wire beyond what vanilla hosting sends. UI editor element "quickhost" (move/scale/hide).
    /// </summary>
    internal static class BtcQuickHost
    {
        private static GUIStyle _st;
        private static int _fs;
        private static float _busyUntil;

        /// <summary>Main menu scene, no network session, no mod menu over it, and the vanilla root screen is the one shown.</summary>
        internal static bool Visible
        {
            get
            {
                if (SceneManager.GetActiveScene().buildIndex != 1) return false;
                if (NetworkServer.active || NetworkClient.active || BtcNetManager.Rejoining) return false;
                return BtcMainMenuScreen.IsRoot;
            }
        }

        internal static void Host()
        {
            try
            {
                UNetManager m = UNetManager.instance;
                if (m == null || NetworkServer.active || NetworkClient.active) return;
                _busyUntil = Time.unscaledTime + 3f;
                m.LocalPlayerName = !string.IsNullOrEmpty(Neo.multiplayerName) ? Neo.multiplayerName : "Player";
                BtcLog.Info("QuickHost", "host from the main menu, port " + UNetManager.HostingPort);
                NetworkWaitingModalMenu.OpenAsHostSignal.Invoke();
            }
            catch (Exception e) { BtcLog.Error("QuickHost", "host: " + e); }
        }

        internal static void OnGUI()
        {
            bool live = ResourcePackManager.MenuIsOpen && BtcUiEditorTab.Live;
            if (ResourcePackManager.MenuIsOpen && !live) return;
            if (!live && (!Visible || Time.unscaledTime < _busyUntil)) return;
            BtcHud.Slot sl = BtcHud.SlotOf("quickhost");
            if (!sl.Enabled) return;
            int fs = Mathf.RoundToInt(BtcHud.Fs * sl.Scale);
            BtcReplayUI.Ensure(fs);
            if (_st == null || _fs != fs)
            {
                _fs = fs;
                _st = new GUIStyle(BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel);
                _st.fontSize = Mathf.RoundToInt(fs * 1.1f); _st.fontStyle = FontStyle.Bold; _st.alignment = TextAnchor.MiddleLeft; _st.wordWrap = false;
            }
            Event e = Event.current;
            GUI.depth = -1200;
            float w = fs * 12f, h = fs * 2.6f;
            Rect r = BtcHud.Place("quickhost", new Rect(Screen.width - w - fs * 1.2f, fs * 0.8f, w, h));
            bool hover = r.Contains(e.mousePosition);
            float a = sl.Alpha * (hover ? 1f : 0.85f);
            Color ac = BtcMenu.Accent;
            BtcReplayUI.Fill(r, new Color(BtcHud.Panel.r, BtcHud.Panel.g, BtcHud.Panel.b, BtcHud.Panel.a * a));
            BtcReplayUI.Fill(new Rect(r.x, r.y + r.height * 0.14f, 3f, r.height * 0.72f), new Color(ac.r, ac.g, ac.b, a));
            if (hover) BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, 0.06f));
            float ic = fs * 1.2f;
            BtcReplayIcons.Glyph(BtcReplayIcons.Globe, new Rect(r.x + fs * 0.8f, r.center.y - ic * 0.5f, ic, ic), new Color(ac.r, ac.g, ac.b, a));
            Color old = GUI.contentColor;
            GUI.contentColor = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(r.x + fs * 2.6f, r.y, r.width - fs * 2.8f, r.height), BtcReplayUI.T("Host game", "Создать игру"), _st);
            GUI.contentColor = old;
            if (live) return;
            if (hover) BtcReplayIcons.Hint(r, BtcReplayUI.T("Open a multiplayer lobby right away (port ", "Сразу открыть лобби мультиплеера (порт ") + UNetManager.HostingPort + ")");
            if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) Host();
        }
    }
}
