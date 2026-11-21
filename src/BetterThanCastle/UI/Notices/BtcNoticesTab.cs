using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Notification centre (menu tab 13, chat icon in the top corner): every short message and card the HUD showed this session, newest first, so a
    /// notice you missed (a kick, a reconnect, a rule change) can be read again. Cleared by the trash button (Ctrl+Z brings it back).
    /// </summary>
    internal sealed class BtcNoticesTab : BtcMenuTab
    {
        internal static readonly BtcNoticesTab Instance = new BtcNoticesTab();
        private const string Scope = "notices";
        private Vector2 _scroll;
        private float _h = 100f;

        internal override int Id { get { return 13; } }
        internal override bool IsActive { get { return BtcMenu.CurrentId == 13; } }
        internal override string UndoScope { get { return Scope; } }

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        internal override void Draw(Rect area, int fs)
        {
            BtcVisualRows.Styles(fs);
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == BtcMenu.MenuKeyMain)
            {
                BtcMenu.PgDownFrame = Time.frameCount;
                ResourcePackManager.CloseMenu();
                e.Use();
                return;
            }
            BtcHud.Unseen = 0;
            float pad = fs, ib = fs * 1.9f, y0 = area.y + fs * 0.4f;
            GUIStyle ttl = new GUIStyle(BtcVisualRows.Lbl); ttl.fontSize = Mathf.RoundToInt(fs * 1.15f); ttl.fontStyle = FontStyle.Bold;
            BtcVisualRows.Txt(new Rect(area.x + pad, y0, area.width * 0.5f, ib), Tt("Notifications", "Уведомления") + "  (" + BtcHud.History.Count + ")", ttl, Color.white);
            if (BtcHud.History.Count > 0 && BtcReplayIcons.Plain(new Rect(area.xMax - pad - ib, y0, ib, ib), BtcReplayIcons.Trash, Color.white, Tt("Clear the list", "Очистить список")))
            {
                List<BtcHud.HistEntry> old = new List<BtcHud.HistEntry>(BtcHud.History);
                BtcHud.History.Clear();
                BtcSfx.Delete();
                BtcUndo.Push(Scope, Tt("Notifications cleared", "Уведомления очищены"), delegate { BtcHud.History.InsertRange(0, old); });
            }
            float y = y0 + ib + fs * 0.5f;
            BtcReplayUI.Fill(new Rect(area.x + pad, y - fs * 0.25f, area.width - pad * 2f, 1f), BtcMenu.Line);
            Rect view = new Rect(area.x, y, area.width, area.yMax - y);
            float vw = view.width - (_h > view.height ? 14f : 0f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, vw, _h));
            float cy = fs * 0.3f;
            if (BtcHud.History.Count == 0)
                BtcUi.Note(ref cy, vw, fs, Tt("Nothing yet. Messages about rule changes, reconnects, kicks and version warnings will be listed here.", "Пока ничего нет. Здесь будут сообщения об изменении правил, переподключениях, киках и предупреждения о версиях."));
            GUIStyle clock = new GUIStyle(BtcVisualRows.Dim); clock.alignment = TextAnchor.MiddleRight;
            for (int i = BtcHud.History.Count - 1; i >= 0; i--)
            {
                BtcHud.HistEntry h = BtcHud.History[i];
                Rect r = new Rect(fs, cy, vw - fs * 2f, fs * 2.1f);
                BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, 0.03f));
                BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, r.height), h.Col);
                float g = fs * 1.2f;
                BtcReplayIcons.Glyph(h.Icon, new Rect(r.x + fs * 0.7f, r.center.y - g * 0.5f, g, g), h.Col);
                BtcVisualRows.Txt(new Rect(r.x + fs * 2.4f, r.y, r.width - fs * 8f, r.height), h.Text, BtcVisualRows.Lbl, Color.white);
                BtcVisualRows.Txt(new Rect(r.xMax - fs * 5.5f, r.y, fs * 5f, r.height), h.Clock, clock, new Color(1f, 1f, 1f, 0.5f));
                if (r.Contains(e.mousePosition)) BtcReplayIcons.Hint(r, h.Text);
                cy += r.height + 3f;
            }
            _h = cy + fs;
            GUI.EndScrollView();
            BtcReplayIcons.DrawTip(fs, area.xMax, false);
        }
    }
}
