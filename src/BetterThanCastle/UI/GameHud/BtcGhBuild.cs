using System.Collections.Generic;
using Brix.External.Factories;
using Brix.Input;
using UnityEngine;

namespace BetterThanCastle
{
    /// Build > Blocks / Structures sidebars (GameMenu.lua 2196-2597 over LUI/Meta/Meta_StoneBlock.lua and Meta_Structure.lua).
    /// Button: icon 64, white, hover label = name, hotkey = Projects binding, click = Picking.SetHeldBlueprint, highlight Bottom
    /// while that blueprint is held. Wormhole ward button after the wards group (BtcWormWardBuild, the Lua injection's twin).
    internal static class BtcGhBuild
    {
        private sealed class Item
        {
            internal int Group;
            internal string Bp, Loc, Icon, Hotkey;
            internal Factory.AssetKey Key;
        }

        // Meta_StoneBlock.lua order: bp, name key, icon key, hotkey action, groupId
        private static readonly string[] BlockMeta =
        {
            "Stone_DoubleBrick", "brick", "_Brick", "project_StandardBrick", "1",
            "Stone_Brick", "halfbrick", "_Block", "project_HalfBrick", "1",
            "Stone_Staircase", "stair", "_Stair", "project_StairBrick", "1",
            "Stone_StairSingle", "stairsingle", "_Half_Stair", "project_HalfStair", "1",
            "Stone_StairSpiralLeft", "stairspiralleft", "_Spiral_Stair_A", "project_StairSpiralLeft", "1",
            "Stone_StairSpiralRight", "stairspiralright", "_Spiral_Stair_B", "project_StairSpiralRight", "1",
            "Stone_StairCornerLeft", "staircornerleft", "_Corner_Stair_A", "project_StairCornerLeft", "1",
            "Stone_StairCornerRight", "staircornerright", "_Corner_Stair_B", "project_StairCornerRight", "1",
            "Stone_StairCornerSingle", "staircornersingle", "_Half_Corner_Stair", "project_StairCornerSingle", "1",
            "Stone_InvertStairCornerSingle", "invertstaircornersingle", "_Half_Inverted_Corner_Stair", "", "1",
            "Stone_Column_Base", "stonecolumn_basic", "_Stone_Column_Base", "project_Pillar", "1",
            "Stone_Column2_Base", "stonecolumn_fluted", "_Stone_Column_Fluted", "", "1",
            "Stone_Column3_Base", "stonecolumn_pillar", "_Stone_Column_Pillar", "", "1",
            "Stone_Column4_Base", "stonecolumn_round", "_Stone_Column_Round", "", "1",
            "Stone_Column5_Base", "stonecolumn_square", "_Stone_Column_Square", "", "1",
            "Stone_BeamPlug_OneSide", "plug", "_Plug_Block", "project_PlugBrick", "2",
            "Stone_BeamPlug_TwoSides", "plugh", "_Plug_H", "project_PlugH", "2",
            "Stone_BeamPlug_Corner", "plugl", "_Plug_L", "project_PlugL", "2",
            "Stone_BeamPlug_ThreeSides", "plugt", "_Plug_T", "project_PlugT", "2",
            "Stone_BeamPlug_AllSides", "plugx", "_Plug_X", "project_PlugX", "2",
            "Stone_DoubleBrick_RoundR", "curvedright", "_Round_Right", "project_RoundRight", "3",
            "Stone_DoubleBrick_RoundL", "curvedleft", "_Round_Left", "project_RoundLeft", "3",
            "Stone_DoubleBrick_RoundLR", "curvedbrick", "_Round_Half", "project_RoundHalf", "3",
            "Stone_Brick_Round", "roundbrick", "_Round_Quarter", "project_RoundBrick", "3",
            "Stone_Arch", "arch", "_Arch", "project_ArchBrick", "4",
            "Stone_Brace1", "corbel1", "_Corbel_1", "project_Corbel", "4",
            "Stone_Brace2", "corbel2", "_Corbel_2", "project_Corbel2", "4",
            "Stone_Merlon", "merlon", "_Merlon", "project_Merlon", "4",
            "Stone_MerlonCorner", "merloncorner", "_Merlon_Corner", "project_MerlonCorner", "4",
            "Stone_MerlonDouble", "merlondouble", "_Merlon_Double", "project_MerlonDouble", "4",
            "Stone_MerlonInverseCorner", "merlonconcave", "_Merlon_Concave", "project_MerlonConcave", "4",
            "Stone_Parapet1", "parapet1", "_Parapet_1", "project_Parapet", "4",
            "Stone_Parapet2", "parapet2", "_Parapet_2", "project_Parapet2", "4",
            "Stone_Parapet3", "parapet3", "_Parapet_3", "project_Parapet3", "4",
            "Wood_SingleLog", "log", "_Log", "project_Log", "5",
            "Wood_Beam", "beam", "_Beam", "project_WoodBeam", "5",
            "Wood_VerticalBeam", "woodcolumn", "_Vertical_Beam", "project_WoodColumn", "5",
            "Wood_Brace", "brace", "_Brace", "project_Brace", "5",
            "Wood_SinglePlank", "plank", "_Plank", "project_Plank", "5",
            "Wood_Ramp_TopA", "ramp", "_Ramp", "project_WoodenRamp", "5",
            "Wood_Ramp_JunctionA", "rampjunction", "_Ramp_Junction", "project_RampJunction", "5",
        };

        private static readonly string[] BlockGroups = { "##gamemenu_taskbar_stoneblocks", "##gamemenu_taskbar_plugs", "##gamemenu_taskbar_roundbricks", "##gamemenu_taskbar_crenelations", "##gamemenu_taskbar_woodblocks" };

        // Meta_Structure.lua order
        private static readonly string[] StructMeta =
        {
            "Pallet", "stockpile", "_Stockpiled_Brick", "project_Stockpile", "1",
            "Toolrack", "toolrack", "_Tool_Rack", "project_WeaponRack", "1",
            "ChoppingBlock", "choppingblock", "_Chopping_Block", "project_ChoppingBlock", "2",
            "Furnace", "furnace", "_Furnace", "project_Furnace", "2",
            "Loom", "loom", "_Loom", "project_Loom", "2",
            "Workbench", "workbench", "_Workbench", "project_Workbench", "3",
            "Forge", "forge", "_Forge", "project_Forge", "3",
            "Lab", "laboratory", "_Lab", "project_Laboratory", "3",
            "Altar", "altar", "_Altar", "project_Altar", "3",
            "MachineShop", "machineshop", "_Machine_Shop", "project_MachineShop", "3",
            "RopeBridgePlacement", "ropebridge", "_Rope_Bridge", "project_RopeBridge", "4",
            "ShakyDoor", "shakydoor", "_Shabby_Door", "", "4",
            "Door", "door", "_Door", "project_Door", "4",
            "HeavyDoor", "heavydoor", "_Heavy_Door", "", "4",
            "Catapult", "catapult", "_Catapult", "project_Catapult", "4",
            "Barrel", "explosivebarrel", "_Explosive_Barrel", "project_ExplosiveBarrel", "4",
            "BearTrap", "beartrap", "_Beartrap", "project_BearTrap", "4",
            "PylonGround", "pylonward", "_Pylon", "project_Pylon", "5",
            "LanternWard", "lanternward", "_Lantern", "project_Lantern", "5",
            "SentinelWard", "sentinelward", "_Sentinel", "project_Sentinel", "5",
            "HealingWard", "healingward", "_Healing_Ward", "project_Healing", "5",
            "CaptureWard", "captureward", "_Capture_Stone", "project_Capture", "5",
            "Window", "window", "_Window", "", "6",
            "DoubleWindow", "doublewindow", "_Double_Window", "", "6",
            "KnightStatue", "knightstatue", "_Knight_Statue", "", "6",
            "PaintingSmall", "paintingsmall", "_Painting_Small", "", "6",
            "PaintingBig", "paintingbig", "_Painting_Big", "", "6",
            "PaintingWide", "paintingwide", "_Painting_Wide", "", "6",
            "PaintingTall", "paintingtall", "_Painting_Tall", "", "6",
        };

        private static readonly string[] StructGroups = { "##gamemenu_taskbar_storage", "##gamemenu_taskbar_production", "##gamemenu_taskbar_crafting", "##gamemenu_taskbar_structures", "##gamemenu_taskbar_wards", "##gamemenu_taskbar_decorations" };

        private static Item[] _blocks, _structs;

        private static Item[] Parse(string[] m)
        {
            Item[] r = new Item[m.Length / 5];
            for (int i = 0; i < r.Length; i++)
            {
                int o = i * 5;
                r[i] = new Item { Bp = m[o], Loc = "##gamemenu_constructionwheel_" + m[o + 1], Icon = m[o + 2], Hotkey = m[o + 3], Group = m[o + 4][0] - '0', Key = new Factory.AssetKey("Blueprints", m[o]) };
            }
            return r;
        }

        private static void Ensure()
        {
            if (_blocks == null) { _blocks = Parse(BlockMeta); _structs = Parse(StructMeta); }
        }

        internal static bool IsBlock(Factory.AssetKey k) { Ensure(); return Find(_blocks, k); }
        internal static bool IsStructure(Factory.AssetKey k) { Ensure(); return Find(_structs, k); }

        private static bool Find(Item[] items, Factory.AssetKey k)
        {
            if (k == null) return false;
            for (int i = 0; i < items.Length; i++) if (items[i].Key == k) return true;
            return false;
        }

        internal static void FillBlocks(BtcGhSidebar bar) { Ensure(); Fill(bar, _blocks, BlockGroups, -1); }
        internal static void FillStructures(BtcGhSidebar bar) { Ensure(); Fill(bar, _structs, StructGroups, 5); }

        private static void Fill(BtcGhSidebar bar, Item[] items, string[] groups, int wardGroup)
        {
            for (int g = 0; g < groups.Length; g++)
            {
                RectTransform grid = bar.Section(BtcGameHud.Loc(groups[g]));
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i].Group != g + 1) continue;
                    Item it = items[i];
                    BtcGhButton b = BtcGhKit.GridButton(grid, BtcGhKit.Icon(it.Icon), Color.white, 64f, BtcGameHud.Loc(it.Loc), true, "", true);
                    b.HotkeyFn = delegate { return BtcGameHud.Hotkey("Projects", it.Hotkey); };
                    b.OnAction = delegate { if (!BtcGhLeft.Paused) Picking.SetBlueprintInPicker(it.Key); };
                    b.Highlight = delegate { return Picking.BlueprintBeingPlaced() == it.Key && !BtcWormWardBuild.Placing ? 1 : 0; };
                    bar.Add(b);
                }
                if (g + 1 == wardGroup && BtcWormWard.RuleOn) AddWormWard(bar, grid);
            }
        }

        /// Twin of the BtcWormWardBuild.PatchGameMenu Lua button (rule on at menu build).
        private static void AddWormWard(BtcGhSidebar bar, RectTransform grid)
        {
            Sprite icon = BtcWormWardLook.Icon64() ?? BtcGhKit.Icon("_Sentinel");
            BtcGhButton b = BtcGhKit.GridButton(grid, icon, Color.white, 64f, BtcLang.Russian ? "Вард-червоточина" : "Wormhole Ward", true, null, false);
            b.OnAction = delegate { if (!BtcGhLeft.Paused) BtcWormWardBuild.BeginPlacing(); };
            b.Highlight = delegate { return BtcWormWardBuild.Placing ? 1 : 0; };
            bar.Add(b);
        }
    }
}
