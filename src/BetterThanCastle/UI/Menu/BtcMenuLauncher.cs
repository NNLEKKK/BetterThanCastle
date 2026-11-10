using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Small green "BTC" button on screen (in game, lobby and the main menu) that opens the mod menu, plus a one-time hint for new players
    /// ("mod menu is here" + the bound key). Slot id "launcher" in <see cref="BtcHud"/> (position/scale/alpha/enabled; the UI editor moves it).
    /// </summary>
    internal static class BtcMenuLauncher
    {
        private const string HintKey = "launcher:hintSeen";
        private static GUIStyle _st, _small;
        private static int _fs;

        internal static bool HintPending { get { return !BtcMenuState.Bool(HintKey, false); } }
        internal static void DismissHint() { if (HintPending) BtcMenuState.SetBool(HintKey, true); }

        /// <summary>Default rectangle (before the slot offset): top-left corner; the left column of plates starts under it.</summary>
        internal static Rect Bounds(int fs, BtcHud.Slot sl)
        {
            float w = fs * 3.6f, h = fs * 2.0f;
            return new Rect(fs * 0.6f + sl.Offset.x, fs * 0.5f + sl.Offset.y, w, h);
        }

        /// <summary>Height the left HUD column must skip so the plates start under the launcher (0 when it is hidden or moved away from the column).</summary>
        internal static float ColumnShift()
        {
            BtcHud.Slot sl = BtcHud.SlotOf("launcher");
            if (!sl.Enabled || Mathf.Abs(sl.Norm.x) > 0.05f || Mathf.Abs(sl.Norm.y) > 0.05f) return 0f;
            return BtcHud.Fs * sl.Scale * 2.7f;
        }

        internal static void OnGUI()
        {
            if (ResourcePackManager.MenuIsOpen) { DismissHint(); return; }
            BtcHud.Slot sl = BtcHud.SlotOf("launcher");
            if (!sl.Enabled) return;
            int fs = Mathf.RoundToInt(BtcHud.Fs * sl.Scale);
            BtcReplayUI.Ensure(fs);
            if (_st == null || _fs != fs)
            {
                _fs = fs;
                GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
                _st = new GUIStyle(b); _st.fontSize = Mathf.RoundToInt(fs * 1.0f); _st.fontStyle = FontStyle.Bold; _st.alignment = TextAnchor.MiddleCenter;
                _small = new GUIStyle(BtcReplayUI.sLabel); _small.fontSize = Mathf.RoundToInt(fs * 0.85f); _small.alignment = TextAnchor.MiddleLeft; _small.wordWrap = false;
            }
            Event e = Event.current;
            GUI.depth = -1200;
            Rect r = Bounds(fs, sl);
            bool hover = r.Contains(e.mousePosition);
            float a = sl.Alpha * (hover ? 1f : 0.8f);
            Color ac = BtcMenu.Accent;
            BtcReplayUI.Fill(r, new Color(BtcHud.Panel.r, BtcHud.Panel.g, BtcHud.Panel.b, BtcHud.Panel.a * a));
            BtcReplayUI.Fill(new Rect(r.x, r.y + r.height * 0.14f, 3f, r.height * 0.72f), new Color(ac.r, ac.g, ac.b, a));
            if (hover) BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, 0.06f));
            Color old = GUI.contentColor;
            GUI.contentColor = new Color(ac.r, ac.g, ac.b, a);
            GUI.Label(r, "BTC", _st);
            GUI.contentColor = old;
            if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) { DismissHint(); ResourcePackManager.OpenMenu(); return; }
            if (HintPending) DrawHint(r, fs, sl);
        }

        private static void DrawHint(Rect btn, int fs, BtcHud.Slot sl)
        {
            Event e = Event.current;
            string l1 = BtcReplayUI.T("Mod menu is here", "Мод-меню здесь");
            string l2 = BtcReplayUI.T("or press ", "или нажмите ") + BtcHotkeys.ComboText("menu_toggle");
            float w = fs * 13f, h = fs * 4.2f;
            float x = Mathf.Clamp(btn.x, fs * 0.4f, Screen.width - w - fs * 0.4f);
            bool above = btn.yMax + fs * 0.5f + h > Screen.height;
            Rect r = new Rect(x, above ? btn.y - fs * 0.5f - h : btn.yMax + fs * 0.5f, w, h);
            Color ac = BtcMenu.Accent;
            BtcReplayUI.Fill(r, new Color(BtcHud.Panel.r, BtcHud.Panel.g, BtcHud.Panel.b, BtcHud.Panel.a * sl.Alpha));
            BtcReplayUI.Fill(new Rect(r.x, above ? r.yMax - 2f : r.y, r.width, 2f), ac);
            BtcReplayUI.Fill(new Rect(Mathf.Clamp(btn.center.x, x + fs, x + w - fs) - fs * 0.4f, (above ? btn.y - fs * 0.5f : btn.yMax + fs * 0.1f), fs * 0.8f, fs * 0.4f), ac);   // pointer notch
            Color old = GUI.contentColor;
            GUI.contentColor = Color.white;
            GUI.Label(new Rect(r.x + fs * 0.8f, r.y + fs * 0.4f, r.width - fs * 3f, fs * 1.6f), l1, _small);
            GUI.contentColor = new Color(1f, 1f, 1f, 0.65f);
            GUI.Label(new Rect(r.x + fs * 0.8f, r.y + fs * 2.1f, r.width - fs * 3f, fs * 1.6f), l2, _small);
            GUI.contentColor = old;
            Rect x1 = new Rect(r.xMax - fs * 2f, r.y + fs * 0.3f, fs * 1.6f, fs * 1.6f);
            bool hv = x1.Contains(e.mousePosition);
            BtcReplayIcons.Glyph(BtcReplayIcons.Cross, new Rect(x1.x + fs * 0.3f, x1.y + fs * 0.3f, fs, fs), new Color(1f, 1f, 1f, hv ? 0.9f : 0.4f));
            if (BtcSfx.Btn(x1, "", BtcReplayUI.sBtn)) DismissHint();
        }
    }
}
