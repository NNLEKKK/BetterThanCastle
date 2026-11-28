using System;
using UnityEngine;

namespace BetterThanCastle
{
    /// BTC Terraforming task sections of the popup (shown while a terraform task is selected, or while the tool places a new one):
    /// mode slots like the tunnel directions (dig, fill, tunnel ladder, quarry ladder, landing; task slots 1-5) + "dig room for ladders",
    /// kept resources like the tunnel (stone 6, iron, brimstone) + haul to stockpile + brush size - / + buttons. Priority / actions are the vanilla
    /// sections (a terraform task is a Flatten). Refreshed by BtcTerraformTool through RefreshTerraform (no polling).
    internal static partial class BtcGhPopup
    {
        private static Sec TerraModeSec()
        {
            return new Sec(M_SEL, TerraShown, "Terraform", delegate(Sec s)
            {
                s.Add(TerraMode("UI_TerrainHug", 32f, BtcGhKit.Yellow, 0, "Dig: hold LMB and drag a straight line (on a wall: level, along it or into the hill, Alt: up / down; on top: Alt digs down)"));
                s.Add(TerraMode("UI_RaiseBrush", 32f, BtcGhKit.Yellow, 1, "Fill: hold LMB and drag (Alt: out of the face). Workers bring stones, 10 per block, and fill from the bottom up"));
                s.Add(TerraMode("_UI_Tunnel", 40f, Color.white, 2, "Tunnel ladder: click a wall (it stands with its back to it), drag up or down the slope for a chain. RMB / R rotate"));
                s.Add(TerraMode("_UI_Quarry", 40f, Color.white, 3, "Quarry ladder: replaces a full block, drag for a staircase. RMB / R rotate"));
                s.Add(TerraMode("UI_PlatformBrush", 32f, BtcGhKit.Yellow, 4, "Landing: a flat block to stand on, drag for a row"));
                s.Add(new Btn
                {
                    Visible = TerraLadderMode, IconName = "_Tool_Shovel", IconColor = Color.white, IconSize = 48f, OnAction = ActTerraAutoDig,
                    Highlight = delegate { return BtcTerraformTool.AutoDig ? 15 : 0; },
                    Tip = delegate { return BtcLang.TrG("Dig room for ladders") + ": " + BtcLang.TrG("Marks the block above a ladder (and a tunnel ladder's own block) for digging"); },
                });
                s.Add(TerraSizeBtn("UI_Minus", -1, 0, BtcTerraformTool.WMinusKey));
                s.Add(TerraSizeBtn("UI_Plus", 1, 0, BtcTerraformTool.WPlusKey));
                s.Add(TerraSizeBtn("UI_Minus", 0, -1, BtcTerraformTool.HMinusKey));
                s.Add(TerraSizeBtn("UI_Plus", 0, 1, BtcTerraformTool.HPlusKey));
            });
        }

        /// Brush size step button: the count shows the letter (W / H) and the current value, the corner its own bind.
        private static Btn TerraSizeBtn(string icon, int dw, int dh, string bind)
        {
            bool w = dw != 0;
            return new Btn
            {
                Visible = TerraShown, IconName = icon, IconSize = 32f, IconColor = Color.white,
                OnAction = delegate { if (TerraShown()) BtcTerraformTool.Resize(dw, dh); },
                HasHotkey = true, Hotkey = delegate { return BtcHotkeys.GetKeys(bind, NoTfKeys).Length == 0 ? "" : BtcHotkeys.ComboText(bind); },
                CountFn = delegate { return TerraSizeText(w); }, CountSize = 12, CountColor = BtcGhKit.Yellow,
                Tip = TerraSizeTip,
            };
        }

        private static readonly KeyCode[] NoTfKeys = new KeyCode[0];
        private static readonly string[] _sizeText = new string[4];
        private static readonly int[] _sizeVal = { -1, -1, -1, -1 };

        // "W 2" / "Ш 2", cached per (axis, language)
        private static string TerraSizeText(bool w)
        {
            bool ru = BtcLang.GameRussian;
            int i = (w ? 0 : 2) + (ru ? 1 : 0), v = w ? BtcTerraformTool.W : BtcTerraformTool.H;
            if (_sizeText[i] == null || _sizeVal[i] != v)
            {
                _sizeVal[i] = v;
                _sizeText[i] = (w ? (ru ? "Ш " : "W ") : (ru ? "В " : "H ")) + Num(v);
            }
            return _sizeText[i];
        }

        private static Sec TerraKeepSec()
        {
            return new Sec(M_SEL, TerraShown, "Keep dug resources", delegate(Sec s)
            {
                s.Add(TerraKeep("_Boulder", FlagStone, 6, "Keep stone"));
                s.Add(TerraKeep("_Raw_Iron", FlagIron, 0, "Keep iron"));
                s.Add(TerraKeep("_Raw_Orange_Crystal", FlagBrimstone, 0, "Keep brimstone"));
                s.Add(new Btn
                {
                    Visible = TerraShown, IconName = "UI_Stockpile", IconSize = 32f, OnAction = ActTerraHaul,
                    IconColorFn = delegate { return BtcTerraformTool.Harvest != 0 ? BtcGhKit.Yellow : BtcGhKit.Gray; },
                    Highlight = delegate { return BtcTerraformTool.StoreFull && BtcTerraformTool.Harvest != 0 ? 15 : 0; },
                    Tip = delegate
                    {
                        return BtcTerraformTool.Harvest == 0 ? BtcLang.TrG("Nothing drops: no stockpile needed")
                            : BtcLang.TrG("Haul to stockpile when the bag is full") + ": " + BtcLang.TrG("A worker whose bag is full carries the stone to a stockpile at once, then returns to the task");
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
                Tip = delegate { return BtcLang.TrG(tip); },
            };
            return b;
        }

        private static Btn TerraKeep(string icon, int flag, int slot, string tip)
        {
            Btn b = new Btn
            {
                Visible = TerraShown, IconName = icon, IconColor = Color.white, IconSize = 64f, OnAction = delegate { ActTerraKeep(flag); },
                Highlight = delegate { return (BtcTerraformTool.Harvest & flag) != 0 ? 15 : 0; },
                Tip = delegate { return BtcLang.TrG(tip) + " (" + BtcLang.TrG("off: the block is destroyed, nothing drops") + ")"; },
            };
            if (slot > 0) { b.HasHotkey = true; b.Hotkey = Key("Projects", "project_TaskActionSlot" + slot); }
            return b;
        }

        private static bool TerraLadderMode() { int m = BtcTerraformTool.TfMode; return TerraShown() && (m == 2 || m == 3); }

        private static string TerraSizeTip()
        {
            return string.Format(BtcLang.TrG("Brush {0} wide, {1} high. Click - / +, or hold {2} / {3} and turn the mouse wheel"),
                BtcTerraformTool.W, BtcTerraformTool.H, BtcHotkeys.ComboText(BtcTerraformTool.WidthKey), BtcHotkeys.ComboText(BtcTerraformTool.HeightKey));
        }

        private static void ActTerraMode(int m) { if (TerraShown()) BtcTerraformTool.SetMode(m); }
        private static void ActTerraAutoDig() { if (TerraShown()) BtcTerraformTool.ToggleAutoDig(); }
        private static void ActTerraKeep(int flag) { if (!Paused() && TerraShown()) BtcTerraformTool.ToggleKeep(flag); }
        private static void ActTerraHaul() { if (!Paused() && TerraShown() && BtcTerraformTool.Harvest != 0) BtcTerraformTool.ToggleStoreFull(); }

        /// Task action slots of a terraform task: 1-5 modes, 6 keep stone.
        private static void TerraSlot(int v)
        {
            if (!TerraShown()) return;
            if (v >= 1 && v <= 5) ActTerraMode(v - 1);
            else if (v == 6) ActTerraKeep(FlagStone);
        }
    }
}
