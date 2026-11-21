using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Practice tab (menu tab 8, ex Duel): page rail with the duel against a bot (BtcDuelTab) and aim training (BtcAimTab).
    /// Page kept in BtcMenuState "practice:page"; undo scope follows the page.
    /// </summary>
    internal sealed class BtcPracticeTab : BtcMenuTab
    {
        internal static readonly BtcPracticeTab Instance = new BtcPracticeTab();

        internal override int Id { get { return 8; } }
        internal override bool IsActive { get { return BtcMenu.CurrentId == 8; } }
        internal override string UndoScope { get { return Page == PAim ? BtcAimTab.Scope : BtcDuelTab.Scope; } }

        internal const int PDuel = 0, PAim = 1;
        private static readonly int[] Pages = { PDuel, PAim };

        internal static int Page
        {
            get { return Mathf.Clamp(BtcMenuState.Int("practice:page", 0), 0, Pages.Length - 1); }
            set { BtcMenuState.SetInt("practice:page", value); }
        }

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static string PageName(int p) { return p == PAim ? Tt("Aim training", "Тренировка аима") : Tt("Duel vs bot", "Дуэль с ботом"); }
        private static int PageIcon(int p) { return p == PAim ? BtcReplayIcons.Target : BtcReplayIcons.Swords; }

        internal override void Draw(Rect area, int fs)
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == BtcMenu.MenuKeyMain)
            {
                GUIUtility.keyboardControl = 0;
                BtcMenu.PgDownFrame = Time.frameCount;
                ResourcePackManager.CloseMenu();
                e.Use();
                return;
            }
            float railW = BtcMenu.SubRailWidth(fs);
            DrawRail(new Rect(area.x, area.y, railW, area.height), fs);
            Rect content = new Rect(area.x + railW, area.y, area.width - railW, area.height);
            BtcReplayUI.Fill(new Rect(content.x - 1f, content.y, 1f, content.height), BtcMenu.Line);
            if (Page == PAim) BtcAimTab.Instance.Draw(content, fs);
            else BtcDuelTab.Instance.Draw(content, fs);
            BtcReplayIcons.DrawTip(fs, area.xMax, false);
        }

        private static void DrawRail(Rect r, int fs)
        {
            Event e = Event.current;
            float item = r.width * 0.82f, x = r.x + (r.width - item) * 0.5f, y = r.y + fs * 0.6f, itemW = item;
            BtcMenu.RailGeometry(r, fs, ref item, ref itemW, ref x);
            for (int i = 0; i < Pages.Length; i++)
            {
                int p = Pages[i];
                Rect ir = new Rect(x, y, itemW, item);
                bool on = p == Page, hover = ir.Contains(e.mousePosition);
                if (on) { BtcReplayUI.Fill(ir, BtcMenu.AccentA(0.14f)); BtcReplayUI.Fill(new Rect(r.x, y + item * 0.15f, 3f, item * 0.7f), BtcMenu.Accent); }
                else if (hover) BtcReplayUI.Fill(ir, new Color(1f, 1f, 1f, 0.07f));
                BtcMenu.RailGlyph(ir, fs, PageIcon(p), PageName(p), on, hover);
                BtcReplayIcons.Hint(ir, PageName(p));
                if (BtcSfx.Btn(ir, "", BtcReplayUI.sBtn)) Page = p;
                y += item + fs * 0.3f;
            }
        }
    }
}
