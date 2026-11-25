using System;
using Brix.External.Factories;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle
{
    /// New task sidebar (GameMenu.lua 1628-2019): 4 px spacer, "too many tasks" alert, Maintenance (repair), Harvest (trees, plants,
    /// cleaning, iron, brimstone, boulders, all), Mining (tunnel, landscape, quarry), Construction (build). Icon white under the
    /// task cap, grey at the cap; click/hotkey only under the cap; highlight Bottom while that task is being placed.
    internal static class BtcGhNewTask
    {
        private delegate DynValue LuaFn(ScriptExecutionContext c, CallbackArguments a);

        private sealed class Task
        {
            internal string Icon, Loc, Hotkey;
            internal bool FgLabel = true;
            internal LuaFn Run, Placing;
            internal bool Terraform; // BTC task: own icon / label, started by BtcTerraformTool
        }

        private static readonly Task Terra = new Task { Terraform = true, Loc = "Terraforming", Hotkey = BtcTerraformTool.HotkeyId };

        // BtcGhModes.NewTaskButtons order: Harvest(all), Wood, Boulder, Iron, Brimstone, Plants, Repair, Quarry, Cleaning, Landscape, Tunnel, Build
        private static readonly Task All = new Task { Icon = "_Harvest", Loc = "##gamemenu_taskbar_harvest", Hotkey = "project_CreateHarvestTask", Run = UIGameObserver.lua_NewHarvestTask_All, Placing = UIGameObserver.lua_IsPlacingNewHarvestTask_All };
        private static readonly Task Wood = new Task { Icon = "_Cut_Tree", Loc = "##gamemenu_taskbar_trees", Hotkey = "project_CreateWoodTask", Run = UIGameObserver.lua_NewHarvestTask_Trees, Placing = UIGameObserver.lua_IsPlacingNewHarvestTask_Trees };
        private static readonly Task Boulder = new Task { Icon = "_Harvest_Stone", Loc = "##gamemenu_taskbar_boulders", Hotkey = "project_CreateBoulderTask", Run = UIGameObserver.lua_NewHarvestTask_Boulders, Placing = UIGameObserver.lua_IsPlacingIsPlacingNewHarvestTask_Boulders };
        private static readonly Task Iron = new Task { Icon = "_Harvest_Iron", Loc = "##gamemenu_taskbar_iron", Hotkey = "project_CreateIronTask", Run = UIGameObserver.lua_NewHarvestTask_Iron, Placing = UIGameObserver.lua_IsPlacingNewHarvestTask_Iron };
        private static readonly Task Brim = new Task { Icon = "_Harvest_Orange", Loc = "##gamemenu_taskbar_brimstone", Hotkey = "project_CreateBrimstoneTask", Run = UIGameObserver.lua_NewHarvestTask_Brimstone, Placing = UIGameObserver.lua_IsPlacingNewHarvestTask_Brimstone };
        private static readonly Task Plants = new Task { Icon = "_Harvest_Plant", Loc = "##gamemenu_taskbar_plants", Hotkey = "project_CreatePlantsTask", Run = UIGameObserver.lua_NewHarvestTask_Plants, Placing = UIGameObserver.lua_IsPlacingNewHarvestTask_Plants };
        private static readonly Task Repair = new Task { Icon = "_Repair_Brick", Loc = "##gamemenu_taskbar_repair", Hotkey = "project_CreateRepairTask", Run = UIGameObserver.lua_NewRepairTask, Placing = UIGameObserver.lua_IsPlacingNewRepairTask };
        private static readonly Task Quarry = new Task { Icon = "_Tool_Shovel", Loc = "##gamemenu_taskbar_quarry", Hotkey = "project_CreateQuarryTask", FgLabel = false, Run = UIGameObserver.lua_NewQuarryTask, Placing = UIGameObserver.lua_IsPlacingNewQuarryTask };
        private static readonly Task Clean = new Task { Icon = "_Cleanup", Loc = "##gamemenu_taskbar_cleaning", Hotkey = "project_CreateCleaningTask", Run = UIGameObserver.lua_NewCleanupTask, Placing = UIGameObserver.lua_IsPlacingNewCleanupTask };
        private static readonly Task Land = new Task { Icon = "_Break_Voxel", Loc = "##gamemenu_taskbar_landscape", Hotkey = "project_CreateLandscapeTask", Run = UIGameObserver.lua_NewLandscapeTask, Placing = UIGameObserver.lua_IsPlacingNewLandscapeTask };
        private static readonly Task Tunnel = new Task { Icon = "_Tool_Pickaxe", Loc = "##gamemenu_taskbar_tunnel", Hotkey = "project_CreateTunnelTask", Run = UIGameObserver.lua_NewTunnelTask, Placing = UIGameObserver.lua_IsPlacingNewTunnelTask };
        private static readonly Task Build = new Task { Icon = "_Build_Brick", Loc = "##gamemenu_taskbar_build", Hotkey = "project_CreateBuildTask", Run = UIGameObserver.lua_NewBuildTask };

        private static readonly Task[] KeyOrder = { All, Wood, Boulder, Iron, Brim, Plants, Repair, Quarry, Clean, Land, Tunnel, Build };

        private static void Start(Task t)
        {
            if (BtcGhLeft.Paused || !BtcGhLeft.UnderCap()) return;
            if (t.Terraform)
            {
                BtcGhLeft.SetOpen(BtcGhLeft.Side.None);
                BtcTerraformTool.OpenNew();
                return;
            }
            t.Run(default(ScriptExecutionContext), default(CallbackArguments));
        }

        /// Key actions of the tasks input mode (registered by BtcGhModes.ActivateTasks).
        internal static void BindKeys()
        {
            for (int i = 0; i < KeyOrder.Length; i++)
            {
                Task t = KeyOrder[i];
                BtcGhModes.NewTaskButtons[i] = delegate { Start(t); };
            }
        }

        internal static void Fill(BtcGhSidebar bar)
        {
            BtcGhKit.Spacer(bar.Body, 4f, -1f, BtcGhKit.RailBg, true);
            Alert(bar);
            Grid(bar, "##gamemenu_taskbar_maintenancetasks", Repair);
            Grid(bar, "##gamemenu_taskbar_harvesttasks", Wood, Plants, Clean, Iron, Brim, Boulder, All);
            Grid(bar, "##gamemenu_taskbar_miningtasks", Tunnel, Land, Quarry, Terra);
            Grid(bar, "##gamemenu_taskbar_constructiontasks", Build);
        }

        private static void Grid(BtcGhSidebar bar, string header, params Task[] tasks)
        {
            RectTransform grid = bar.Section(BtcGameHud.Loc(header));
            for (int i = 0; i < tasks.Length; i++)
            {
                Task t = tasks[i];
                if (t.Terraform)
                {
                    BtcGhButton tb = BtcGhKit.GridButton(grid, BtcTerraformIcon.Get(), Color.white, 48f, BtcLang.Tr(t.Loc), true, "", true);
                    tb.HotkeyFn = delegate { return BtcHotkeys.GetKeys(t.Hotkey, null).Length > 0 ? BtcHotkeys.ComboText(t.Hotkey) : ""; };
                    tb.IconColor = delegate { return BtcGhLeft.UnderCap() && BtcTerraform.On ? Color.white : BtcGhKit.Gray; };
                    tb.OnAction = delegate { Start(t); };
                    tb.Highlight = delegate { return BtcTerraformTool.Placing ? 1 : 0; };
                    tb.Tip = delegate { return BtcTerraform.On ? BtcLang.Tr("Dig any shape, tunnel and quarry ladders, landings") : BtcLang.Tr("Terraforming needs BetterConnection (BC tab)"); };
                    bar.Add(tb);
                    continue;
                }
                BtcGhButton b = BtcGhKit.GridButton(grid, BtcGhKit.Icon(t.Icon), Color.white, 64f, BtcGameHud.Loc(t.Loc), t.FgLabel, "", true);
                b.HotkeyFn = delegate { return BtcGameHud.Hotkey("Projects", t.Hotkey); };
                b.IconColor = delegate { return BtcGhLeft.UnderCap() ? Color.white : BtcGhKit.Gray; };
                b.OnAction = delegate { Start(t); };
                if (t.Placing != null) b.Highlight = delegate { return t.Placing(default(ScriptExecutionContext), default(CallbackArguments)).Boolean ? 1 : 0; };
                bar.Add(b);
            }
        }

        /// AlertPanel (SidebarFloatingMenu AddAlertHandle): 256x56, warning icon at (100,0), grey text, frame highlight All, no hover.
        private static void Alert(BtcGhSidebar bar)
        {
            RectTransform p = BtcGhKit.Make(Layouts.Horizontal, bar.Body, "AlertPanel @ _p");
            BtcGhKit.Rect(p, new Vector2(0f, 1f), new Vector2(0f, 1f), BtcGhKit.Mid, Vector2.zero, new Vector2(256f, 56f));
            BtcGhKit.Img(p, null, BtcGhKit.Clear, true);
            BtcGhKit.Le(p, 256f, 56f, 256f, 56f, -1f, -1f, false);
            BtcGhKit.Group(p, 0, 0, 0, 0, 2f, TextAnchor.MiddleLeft);
            RectTransform bg = BtcGhKit.Make(Images.Default, p, "AlertPanel @ _p.bgImg");
            BtcGhKit.Rect(bg, BtcGhKit.Mid, BtcGhKit.Mid, BtcGhKit.Mid, new Vector2(100f, 0f), new Vector2(32f, 32f));
            BtcGhKit.Img(bg, BtcGhKit.Icon("_UI_Warning"), BtcGhKit.Red, true);
            BtcGhKit.Le(bg, 64f, 64f, -1f, -1f, -1f, -1f, true);
            RectTransform lbl = BtcGhKit.Make(Texts.Default, p, "AlertPanel @ _p.lbl_fg");
            BtcGhKit.Rect(lbl, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), BtcGhKit.Mid, Vector2.zero, Vector2.zero);
            Text t = BtcGhKit.Txt(lbl, BtcGameHud.Loc("##gamemenu_help_popup_toomanytasks"), 10, new Color(0.6f, 0.6f, 0.6f, 1f), TextAnchor.MiddleLeft);
            if (t != null) t.lineSpacing = 1.2f;
            BtcGhKit.Le(lbl, 64f, 64f, -1f, -1f, -1f, -1f, true);
            RectTransform btn = BtcGhKit.Make(Buttons.Standard, p, "AlertPanel @ _p.btn");
            BtcGhKit.Stretch(btn);
            BtcGhKit.Le(btn, 64f, 64f, 64f, 64f, -1f, -1f, true);
            BtcGhButton b = btn.gameObject.AddComponent<BtcGhButton>();
            b.Frame = btn.GetComponent<Brix.UI.Builder.Widget.FrameAnimatorWidgetComponent>() ?? btn.GetComponentInChildren<Brix.UI.Builder.Widget.FrameAnimatorWidgetComponent>(true);
            b.Silent = true;
            b.Panel = p.gameObject;
            b.Visible = delegate { return !BtcGhLeft.UnderCap(); };
            b.Highlight = delegate { return 15; };
            bar.Add(b);
        }
    }
}
