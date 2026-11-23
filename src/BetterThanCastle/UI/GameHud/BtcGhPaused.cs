using System;
using Brix.External.Factories;
using Brix.External.Signals;
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle
{
    /// LUI/Menus/Game/IsPausedMenu.lua: "Game Paused" plate (256x60, (25,25,25), 100 px below the top centre, Korolev 24 + 16,
    /// yellow) shown while UIGame.IsPaused(). Refreshed by UIGame.hk_OnSetIsPaused (Journee.OnSetIsPaused), no poll.
    internal static class BtcGhPaused
    {
        private static GameObject _lyt;
        private static SignalConnection _conn; // struct
        private static bool _connected;

        internal static void Build(RectTransform root)
        {
            RectTransform menu = BtcGhKit.Box(Layouts.Vertical, root, "", 0, 0, 0, 0, 0f, TextAnchor.UpperLeft);
            BtcGhKit.Le(menu, -1f, -1f, -1f, -1f, -1f, -1f, true);
            RectTransform lyt = BtcGhKit.Box(Layouts.Horizontal, menu, "", 0, 0, 0, 0, 0f, TextAnchor.MiddleCenter);
            BtcGhKit.Le(lyt, -1f, -1f, -1f, -1f, -1f, -1f, true);
            RectTransform plate = BtcGhKit.Make(Layouts.Horizontal, lyt, "");
            BtcGhKit.Rect(plate, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), BtcGhKit.Mid, new Vector2(0f, -100f), new Vector2(256f, 60f));
            BtcGhKit.Img(plate, null, BtcGhKit.Selected, true);
            BtcGhKit.Le(plate, -1f, -1f, -1f, -1f, -1f, -1f, true);
            BtcGhKit.Group(plate, 0, 0, 0, 0, 0f, TextAnchor.MiddleCenter);
            RectTransform col = BtcGhKit.Box(Layouts.Vertical, plate, "", 0, 0, 0, 0, 0f, TextAnchor.UpperLeft);
            BtcGhKit.Le(col, -1f, -1f, -1f, -1f, 1f, -1f, false);
            Label(col, "##gamemenu_paused", 24);
            Label(col, "##gamemenu_paused_text", 16);
            _lyt = lyt.gameObject;
            BtcHudSlots.Register("gh.paused", plate);
            if (UIGameObserver.lua_OnSetIsPaused != null) { _conn = UIGameObserver.lua_OnSetIsPaused._signal.Connect(new Action(Refresh)); _connected = true; }
            Refresh();
        }

        private static void Label(RectTransform parent, string key, int size)
        {
            RectTransform t = BtcGhKit.Make(Texts.Default, parent, "");
            BtcGhKit.Rect(t, Vector2.zero, Vector2.zero, BtcGhKit.Mid, Vector2.zero, new Vector2(64f, 64f));
            Text tx = BtcGhKit.Txt(t, BtcGameHud.Loc(key), size, BtcGhKit.Yellow, TextAnchor.MiddleCenter);
            if (tx != null && BtcGhKit.Korolev != null) tx.font = BtcGhKit.Korolev;
            BtcGhKit.Le(t, -1f, -1f, -1f, size, 1f, -1f, false);
        }

        private static void Refresh()
        {
            try { if (_lyt != null) _lyt.SetActive(ClockConfig.Paused); }
            catch (Exception e) { BtcLog.Error("GameHud", "paused: " + e.Message); }
        }

        internal static void Close()
        {
            if (_connected) { try { _conn.Disconnect(); } catch { } }
            _connected = false; _lyt = null;
        }
    }
}
