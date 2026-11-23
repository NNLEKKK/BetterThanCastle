using System;
using Brix.External.Factories;
using Brix.Input;
using Brix.UI.Builder.Menu;
using Brix.Utils.UI;

namespace BetterThanCastle
{
    /// GameMenu.lua input modes (lines 30-320): which key actions are registered while the menu is in the projects, tasks, bricks,
    /// structures or blueprints mode. Same register/unregister lists as the Lua (including its quirks: deactivating a mode
    /// unregisters a shared name even if another mode registered it), same key binding category flips.
    /// Popup actions (queue slots, priority, task options, delete) are filled by the popup region when it exists.
    internal static class BtcGhModes
    {
        internal static Action CallToArms, BackToWork, BlocksMenu, StructuresMenu, NewTask;
        internal static Action RaisePriority, LowerPriority, DeleteTask, ToggleLoop, ClearQueue, BlueprintMgmt;
        internal static Action<int> QueueSlot, TaskActionSlot;
        internal static Action[] NewTaskButtons = new Action[12]; // order of the project_Create* list below

        private static readonly string[] TaskActions =
        {
            "project_CreateHarvestTask", "project_CreateWoodTask", "project_CreateBoulderTask", "project_CreateIronTask",
            "project_CreateBrimstoneTask", "project_CreatePlantsTask", "project_CreateRepairTask", "project_CreateQuarryTask",
            "project_CreateCleaningTask", "project_CreateLandscapeTask", "project_CreateTunnelTask", "project_CreateBuildTask",
        };

        private static readonly string[] BrickActions =
        {
            "project_StandardBrick", "Stone_DoubleBrick", "project_StairBrick", "Stone_Staircase", "project_HalfStair", "Stone_StairSingle",
            "project_StairSpiralLeft", "Stone_StairSpiralLeft", "project_StairSpiralRight", "Stone_StairSpiralRight",
            "project_StairCornerLeft", "Stone_StairCornerLeft", "project_StairCornerRight", "Stone_StairCornerRight",
            "project_StairCornerSingle", "Stone_StairCornerSingle", "project_PlugBrick", "Stone_BeamPlug_OneSide",
            "project_PlugH", "Stone_BeamPlug_TwoSides", "project_PlugL", "Stone_BeamPlug_Corner", "project_PlugT", "Stone_BeamPlug_ThreeSides",
            "project_PlugX", "Stone_BeamPlug_AllSides", "project_ArchBrick", "Stone_Arch", "project_Pillar", "Stone_Column_Base",
            "project_RoundBrick", "Stone_Brick_Round", "project_RoundRight", "Stone_DoubleBrick_RoundR", "project_RoundLeft", "Stone_DoubleBrick_RoundL",
            "project_RoundHalf", "Stone_DoubleBrick_RoundLR", "project_Parapet", "Stone_Parapet1", "project_Parapet2", "Stone_Parapet2",
            "project_Parapet3", "Stone_Parapet3", "project_HalfBrick", "Stone_Brick", "project_Plank", "Wood_SinglePlank",
            "project_Corbel", "Stone_Brace1", "project_Corbel2", "Stone_Brace2", "project_WoodenRamp", "Wood_Ramp_TopA",
            "project_RampJunction", "Wood_Ramp_JunctionA", "project_WoodColumn", "Wood_VerticalBeam", "project_WoodBeam", "Wood_Beam",
            "project_Log", "Wood_SingleLog", "project_Brace", "Wood_Brace", "project_Merlon", "Stone_Merlon",
            "project_MerlonDouble", "Stone_MerlonDouble", "project_MerlonCorner", "Stone_MerlonCorner", "project_MerlonConcave", "Stone_MerlonInverseCorner",
        };

        private static readonly string[] StructureActions =
        {
            "project_Stockpile", "Pallet", "project_WeaponRack", "Toolrack", "project_Workbench", "Workbench", "project_Loom", "Loom",
            "project_ChoppingBlock", "ChoppingBlock", "project_Forge", "Forge", "project_MachineShop", "MachineShop", "project_Altar", "Altar",
            "project_Laboratory", "Lab", "project_RopeBridge", "RopeBridgePlacement", "project_Pylon", "PylonGround", "project_Furnace", "Furnace",
            "project_Catapult", "Catapult", "project_Door", "Door", "project_BearTrap", "BearTrap", "project_ExplosiveBarrel", "Barrel",
            "project_Lantern", "LanternWard", "project_Sentinel", "SentinelWard", "project_Healing", "HealingWard", "project_Capture", "CaptureWard",
        };

        // built once: one delegate per key action (no allocation when a mode flips)
        private static Action[] _brickRuns, _structRuns;

        private static void Reg(string name, Action run)
        {
            KeyBindingsUtility.RegisterAction(name, delegate
            {
                if (run == null) return;
                try { run(); } catch (Exception e) { BtcLog.Error("GameHud", name + ": " + e.Message); }
            });
        }

        private static void Unreg(string name) { try { KeyBindingsUtility.UnregisterAction(name); } catch { } } // input system may be gone at quit

        private static Action Hold(string bp)
        {
            Factory.AssetKey key = new Factory.AssetKey("Blueprints", bp);
            return delegate { Picking.SetBlueprintInPicker(key); };
        }

        private static void Init()
        {
            if (_brickRuns != null) return;
            _brickRuns = new Action[BrickActions.Length / 2];
            for (int i = 0; i < _brickRuns.Length; i++) _brickRuns[i] = Hold(BrickActions[i * 2 + 1]);
            _structRuns = new Action[StructureActions.Length / 2];
            for (int i = 0; i < _structRuns.Length; i++) _structRuns[i] = Hold(StructureActions[i * 2 + 1]);
        }

        // shared lambdas (late-bound so the popup can fill the slots after the modes are active)
        private static readonly Action RunCallToArms = delegate { if (CallToArms != null) CallToArms(); };
        private static readonly Action RunBackToWork = delegate { if (BackToWork != null) BackToWork(); };
        private static readonly Action RunBlocks = delegate { if (BlocksMenu != null) BlocksMenu(); };
        private static readonly Action RunStructures = delegate { if (StructuresMenu != null) StructuresMenu(); };
        private static readonly Action RunNewTask = delegate { if (NewTask != null) NewTask(); };
        private static readonly Action RunRaise = delegate { if (RaisePriority != null) RaisePriority(); };
        private static readonly Action RunLower = delegate { if (LowerPriority != null) LowerPriority(); };
        private static readonly Action RunDelete = delegate { if (DeleteTask != null) DeleteTask(); };
        private static readonly Action RunLoop = delegate { if (ToggleLoop != null) ToggleLoop(); };
        private static readonly Action RunClear = delegate { if (ClearQueue != null) ClearQueue(); };
        private static readonly Action RunBpMgmt = delegate { if (BlueprintMgmt != null) BlueprintMgmt(); };
        private static readonly Action RunXRay = delegate { if (XRayImpl.instance != null) XRayImpl.instance.enabled = !XRayImpl.instance.enabled; };
        private static readonly Action[] RunQueue = MakeSlots(5, true);
        private static readonly Action[] RunTaskSlot = MakeSlots(6, false);

        private static Action[] MakeSlots(int n, bool queue)
        {
            Action[] a = new Action[n];
            for (int i = 0; i < n; i++)
            {
                int slot = i + 1;
                a[i] = queue ? (Action)delegate { if (QueueSlot != null) QueueSlot(slot); } : delegate { if (TaskActionSlot != null) TaskActionSlot(slot); };
            }
            return a;
        }

        internal static void ActivateProjects()
        {
            Reg("command_CallToArms", RunCallToArms);
            Reg("command_BackToWork", RunBackToWork);
            Reg("command_BlocksMenu", RunBlocks);
            Reg("command_StructuresMenu", RunStructures);
            Reg("command_CreateProject", RunNewTask);
            for (int i = 0; i < 5; i++) Reg("project_QueueSlot" + (i + 1), RunQueue[i]);
            Reg("project_ToggleLoop", RunLoop);
            Reg("project_ClearQueue", RunClear);
            for (int i = 0; i < 6; i++) Reg("project_TaskActionSlot" + (i + 1), RunTaskSlot[i]);
            Reg("project_RaisePriority", RunRaise);
            Reg("project_LowerPriority", RunLower);
            Reg("project_DeleteTask", RunDelete);
            Reg("project_OpenBlueprintManagement", RunBpMgmt);
            Reg("project_ToggleXRay", RunXRay);
            CombatHotkeys.hotkeysActivated = true;
            LuaMenuUtility.SetKeyBindingsTo(KeyBindingsUtility.Category.General, false);
            LuaMenuUtility.SetKeyBindingsTo(KeyBindingsUtility.Category.Projects, true);
        }

        internal static void DeactivateProjects()
        {
            Unreg("command_CallToArms"); Unreg("command_BackToWork"); Unreg("command_BlocksMenu"); Unreg("command_StructuresMenu"); Unreg("command_CreateProject");
            for (int i = 1; i <= 5; i++) Unreg("project_QueueSlot" + i);
            Unreg("project_ToggleLoop"); Unreg("project_ClearQueue");
            for (int i = 1; i <= 6; i++) Unreg("project_TaskActionSlot" + i);
            Unreg("project_RaisePriority"); Unreg("project_LowerPriority"); Unreg("project_DeleteTask"); Unreg("project_ToggleXRay");
            // vanilla quirk kept: project_OpenBlueprintManagement stays registered
            CombatHotkeys.hotkeysActivated = false;
            LuaMenuUtility.SetKeyBindingsTo(KeyBindingsUtility.Category.General, true);
            LuaMenuUtility.SetKeyBindingsTo(KeyBindingsUtility.Category.Projects, false);
        }

        internal static void ActivateTasks()
        {
            for (int i = 0; i < TaskActions.Length; i++) Reg(TaskActions[i], NewTaskButtons[i]);
            Reg("command_CallToArms", RunCallToArms);
            Reg("command_BlocksMenu", RunBlocks);
            Reg("command_StructuresMenu", RunStructures);
        }

        internal static void DeactivateTasks()
        {
            for (int i = 0; i < TaskActions.Length; i++) Unreg(TaskActions[i]);
            Unreg("command_CallToArms"); Unreg("command_BlocksMenu"); Unreg("command_StructuresMenu");
        }

        internal static void ActivateBricks()
        {
            Init();
            Reg("command_CallToArms", RunCallToArms);
            Reg("command_StructuresMenu", RunStructures);
            Reg("command_CreateProject", RunNewTask);
            for (int i = 0; i < _brickRuns.Length; i++) Reg(BrickActions[i * 2], _brickRuns[i]);
            Reg("project_RaisePriority", RunRaise);
            Reg("project_LowerPriority", RunLower);
            Reg("project_DeleteTask", RunDelete);
        }

        internal static void DeactivateBricks()
        {
            Unreg("command_CallToArms"); Unreg("command_StructuresMenu"); Unreg("command_CreateProject");
            for (int i = 0; i < BrickActions.Length; i += 2) Unreg(BrickActions[i]);
            Unreg("project_RaisePriority"); Unreg("project_LowerPriority"); Unreg("project_DeleteTask");
        }

        internal static void ActivateStructures()
        {
            Init();
            Reg("command_CallToArms", RunCallToArms);
            Reg("command_BlocksMenu", RunBlocks);
            Reg("command_CreateProject", RunNewTask);
            for (int i = 0; i < _structRuns.Length; i++) Reg(StructureActions[i * 2], _structRuns[i]);
            Reg("project_RaisePriority", RunRaise);
            Reg("project_LowerPriority", RunLower);
            Reg("project_DeleteTask", RunDelete);
        }

        internal static void DeactivateStructures()
        {
            Unreg("command_CallToArms"); Unreg("command_BlocksMenu"); Unreg("command_CreateProject");
            for (int i = 0; i < StructureActions.Length; i += 2) Unreg(StructureActions[i]);
            Unreg("project_RaisePriority"); Unreg("project_LowerPriority"); Unreg("project_DeleteTask");
        }

        private static readonly Action BpDuplicate = delegate { Brix.Game.Compounders.BlueprintCompounder.Duplicate(); };
        private static readonly Action BpDelete = delegate { Brix.UI.UIGameActor.DeleteSelectedBlueprints(); };
        private static readonly Action BpExpand = delegate { Brix.Game.Compounders.BlueprintCompounder.Expand(); };
        private static readonly Action BpCancel = delegate { Brix.Game.Compounders.BlueprintCompounder.Cancel(); };

        internal static void ActivateBlueprints()
        {
            Reg("tb_constr_blueprints_duplicate", BpDuplicate);
            Reg("tb_constr_blueprints_delete", BpDelete);
            Reg("tb_constr_blueprints_expand", BpExpand);
            Reg("tb_constr_blueprints_clear", BpCancel);
            LuaMenuUtility.SetKeyBindingsTo(KeyBindingsUtility.Category.General, false);
            LuaMenuUtility.SetKeyBindingsTo(KeyBindingsUtility.Category.Blueprints, true);
        }

        internal static void DeactivateBlueprints()
        {
            Unreg("tb_constr_blueprints_duplicate"); Unreg("tb_constr_blueprints_delete"); Unreg("tb_constr_blueprints_expand"); Unreg("tb_constr_blueprints_clear");
            LuaMenuUtility.SetKeyBindingsTo(KeyBindingsUtility.Category.Blueprints, false);
            LuaMenuUtility.SetKeyBindingsTo(KeyBindingsUtility.Category.General, true);
        }

        /// Menu close: drop every name any mode may have registered.
        internal static void UnregisterAll()
        {
            DeactivateTasks(); DeactivateBricks(); DeactivateStructures();
            Unreg("tb_constr_blueprints_duplicate"); Unreg("tb_constr_blueprints_delete"); Unreg("tb_constr_blueprints_expand"); Unreg("tb_constr_blueprints_clear");
            DeactivateProjects();
            Unreg("project_OpenBlueprintManagement");
        }
    }
}
