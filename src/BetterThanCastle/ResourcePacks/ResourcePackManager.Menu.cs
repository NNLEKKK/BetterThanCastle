using System;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Tab selection of the main mod menu (the rail calls it). Migrated tabs live in UI/Menu/BtcMenu.cs + their own BtcMenuTab class.</summary>
    public partial class ResourcePackManager
    {
        internal static readonly Func<int, bool> TabEnabledFn = delegate (int id) { return (id != 4 || BtcBc.BcEnabled) && (id != 11 || BtcStatsTab.Available); };

        /// <summary>Opens a tab by id from outside the menu code (changelog links). False when the menu is not alive.</summary>
        internal static bool GoTab(int id)
        {
            if (instance == null) return false;
            instance.menuOpen = true;
            instance.SelectTab(id);
            return true;
        }

        private void SelectTab(int id)
        {
            if (id == 9) BtcReportTab.OnOpen(BtcMenu.CurrentId);
            if (id == 10) BtcChangelogTab.OnOpen();
            BtcMenu.CurrentId = id;
            if (id != 4) BtcGameplayTab.UseLegacy = false;
            BtcMenuState.SetInt("tab", id);   // the menu reopens on this tab next time (every tab)
            BtcReportTab.TabActive = (id == 9);
            BtcChangelogTab.TabActive = (id == 10);
            this.menuHp = (id == 1);
            this.menuBc = (id == 2);
            BtcHotkeysUI.TabActive = (id == 3);
            BtcGameplayUI.TabActive = (id == 4);
            BtcShareUI.TabActive = (id == 5);
            BtcNick.TabActive = (id == 6);
            BtcReplayUI.TabActive = (id == 7);
            BtcDuelUI.TabActive = (id == 8);
            if (id == 6) BtcNick.OnMenuOpen();
            BtcHotkeys.CancelCapture();
            this.menuScroll = Vector2.zero;
            this.pickStop = null;
            this.pickOv = null;
            this.pickNum = false;
            this.pickExtra = 0;
        }
    }
}
