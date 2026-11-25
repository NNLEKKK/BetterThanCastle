using System;
using UnityEngine;

namespace BetterThanCastle
{
    /// BTC Terraforming task sections of the popup (shown while a terraform task is selected, or while the tool places a new one):
    /// mode slots like the tunnel directions (dig, tunnel ladder, quarry ladder, landing; task slots 1-4) + "dig room for ladders",
    /// kept resources like the tunnel (stone 5, iron 6, brimstone) + haul to stockpile + brush size. Priority / actions are the vanilla
    /// sections (a terraform task is a Flatten). Refreshed by BtcTerraformTool through RefreshTerraform (no polling).
    internal static partial class BtcGhPopup
    {
        private static Sec TerraModeSec()
        {
            return new Sec(M_SEL, TerraShown, "Terraforming", delegate(Sec s)
            {
                s.Add(TerraMode("UI_TerrainHug", 32f, BtcGhKit.Yellow, 0, "Dig: hold LMB and drag a straight line (on a wall: level, along it or into the hill, Alt: up / down; on top: Alt digs down)"));
                s.Add(TerraMode("_UI_Tunnel", 40f, Color.white, 1, "Tunnel ladder: click a wall (it stands with its back to it), drag up or down the slope for a chain. RMB / R rotate"));
                s.Add(TerraMode("_UI_Quarry", 40f, Color.white, 2, "Quarry ladder: replaces a full block, drag for a staircase. RMB / R rotate"));
                s.Add(TerraMode("UI_PlatformBrush", 32f, BtcGhKit.Yellow, 3, "Landing: a flat block to stand on, drag for a row"));
                s.Add(new Btn
                {
                    Visible = TerraLadderMode, IconName = "_Tool_Shovel", IconColor = Color.white, IconSize = 48f, OnAction = ActTerraAutoDig,
                    Highlight = delegate { return BtcTerraformTool.AutoDig ? 15 : 0; },
                    Tip = delegate { return BtcLang.Tr("Dig room for ladders") + ": " + BtcLang.Tr("Marks the block above a ladder (and a tunnel ladder's own block) for digging"); },
                });
                s.Add(new Btn
                {
                    Visible = TerraShown, IconName = null, IconSize = 32f, OnAction = NoOp, NoHover = false,
                    CountFn = TerraSize, CountSize = 14, CountAlign = TextAnchor.MiddleCenter, CountColor = BtcGhKit.Yellow,
                    Tip = TerraSizeTip,
                });
            });
        }

        private static Sec TerraKeepSec()
        {
            return new Sec(M_SEL, TerraShown, "Keep dug resources", delegate(Sec s)
            {
                s.Add(TerraKeep("_Boulder", FlagStone, 5, "Keep stone"));
                s.Add(TerraKeep("_Raw_Iron", FlagIron, 6, "Keep iron"));
                s.Add(TerraKeep("_Raw_Orange_Crystal", FlagBrimstone, 0, "Keep brimstone"));
                s.Add(new Btn
                {
                    Visible = TerraShown, IconName = "UI_Stockpile", IconSize = 32f, OnAction = ActTerraHaul,
                    IconColorFn = delegate { return BtcTerraformTool.Harvest != 0 ? BtcGhKit.Yellow : BtcGhKit.Gray; },
                    Highlight = delegate { return BtcTerraformTool.StoreFull && BtcTerraformTool.Harvest != 0 ? 15 : 0; },
                    Tip = delegate
                    {
                        return BtcTerraformTool.Harvest == 0 ? BtcLang.Tr("Nothing drops: no stockpile needed")
                            : BtcLang.Tr("Haul to stockpile when the bag is full") + ": " + BtcLang.Tr("A worker whose bag is full carries the stone to a stockpile at once, then returns to the task");
                    },
                });
            });
        }

        private static Btn TerraMode(string icon, float size, Color col, int mode, string tip)
        {
            Btn b = new Btn
            {
                Visible = TerraShown, IconName = icon, IconSize = size, IconColor = col, OnAction = delegate { ActTerraMode(mode); },
                HasHotkey = true, Hotkey = Key("Projects", "project_TaskActionSlot" + (mode + 1)),
                Highlight = delegate { return BtcTerraformTool.TfMode == mode ? 15 : 0; },
                Tip = delegate { return BtcLang.Tr(tip); },
            };
            return b;
        }

        private static Btn TerraKeep(string icon, int flag, int slot, string tip)
        {
            Btn b = new Btn
            {
                Visible = TerraShown, IconName = icon, IconColor = Color.white, IconSize = 64f, OnAction = delegate { ActTerraKeep(flag); },
                Highlight = delegate { return (BtcTerraformTool.Harvest & flag) != 0 ? 15 : 0; },
                Tip = delegate { return BtcLang.Tr(tip) + " (" + BtcLang.Tr("off: the block is destroyed, nothing drops") + ")"; },
            };
            if (slot > 0) { b.HasHotkey = true; b.Hotkey = Key("Projects", "project_TaskActionSlot" + slot); }
            return b;
        }

        private static bool TerraLadderMode() { int m = BtcTerraformTool.TfMode; return TerraShown() && (m == 1 || m == 2); }

        private static string _sizeText;
        private static int _sizeW, _sizeH;

        private static string TerraSize()
        {
            int w = BtcTerraformTool.W, h = BtcTerraformTool.H;
            if (_sizeText == null || w != _sizeW || h != _sizeH) { _sizeW = w; _sizeH = h; _sizeText = Num(w) + "x" + Num(h); }
            return _sizeText;
        }

        private static string TerraSizeTip()
        {
            return string.Format(BtcLang.Tr("Brush {0} wide, {1} high. Hold {2} / {3} and turn the mouse wheel to change it"),
                BtcTerraformTool.W, BtcTerraformTool.H, BtcHotkeys.ComboText(BtcTerraformTool.WidthKey), BtcHotkeys.ComboText(BtcTerraformTool.HeightKey));
        }

        private static void ActTerraMode(int m) { if (TerraShown()) BtcTerraformTool.SetMode(m); }
        private static void ActTerraAutoDig() { if (TerraShown()) BtcTerraformTool.ToggleAutoDig(); }
        private static void ActTerraKeep(int flag) { if (!Paused() && TerraShown()) BtcTerraformTool.ToggleKeep(flag); }
        private static void ActTerraHaul() { if (!Paused() && TerraShown() && BtcTerraformTool.Harvest != 0) BtcTerraformTool.ToggleStoreFull(); }

        /// Task action slots of a terraform task: 1-4 modes, 5 keep stone, 6 keep iron.
        private static void TerraSlot(int v)
        {
            if (!TerraShown()) return;
            if (v >= 1 && v <= 4) ActTerraMode(v - 1);
            else if (v == 5) ActTerraKeep(FlagStone);
            else if (v == 6) ActTerraKeep(FlagIron);
        }
    }
}
