// EcoCommands: one enum + struct for every player command (1:1 with the UNet Cmd* methods hooked in
// src/BetterThanCastle/Patches/UNet{Project,Contextual,Groupe,Blueprint,Recepteur,Catapult}Cmd*Patches.cs).
// net35-compatible: no Span, LINQ, tuples, System.Numerics. Link into the mod with
//   <Compile Include="..\..\lab\Btc.EcoShared\*.cs" Link="EcoShared\%(Filename)%(Extension)" />
// Trace line (after "t|CMD|"): Name|actor|target|x,y,z|p0,p1,p2,p3|f0,f1   (trailing fields optional)
//   actor/target = netId (0 = none), pN = ints (resource flags, enum values, counts, bool 0/1), fN = floats (3 decimals).
using System;
using System.Globalization;
using System.Text;

namespace Btc.EcoShared
{
    public enum EcoCmd
    {
        None = 0,
        AccepterPosition = 1,
        AccepterRayon = 2,
        AddBackToWorkGoal = 3,
        AddBreakVoxelGoal = 4,
        AddCallToArmsGoal = 5,
        AddCleanGoal = 6,
        AddEmergencyRecallGoal = 7,
        AddExclusion = 8,
        AddMoveToGoal = 9,
        AddMoveToVolumeGoal = 10,
        AddNatureHarvestGoal = 11,
        AddSmartTransferGoal = 12,
        AddTreeGoal = 13,
        ArmBearTrap = 14,
        Attack = 15,
        BatchedContextualActionGameObject = 16,
        BatchedContextualActionVector = 17,
        BreakObject = 18,
        ChangeProject = 19,
        CraftingAddResources = 20,
        CraftingAssignWorker = 21,
        CraftingRemoveProduction = 22,
        CraftingRemoveResources = 23,
        CraftingWorkAt = 24,
        EquipTool = 25,
        EquipToolRackContent = 26,
        FireAt = 27,
        FireAtObject = 28,
        GoAndJumpTo = 29,
        GoDetonate = 30,
        GoDropAt = 31,
        GoStoreInto = 32,
        HarvestSetResource = 33,
        HarvestToggleResource = 34,
        InstantiateTunnelUnit = 35,
        JumpOff = 36,
        LockDoor = 37,
        ManualConversion = 38,
        MoveAttack = 39,
        Pickup = 40,
        QuarryToggleResource = 41,
        RemoveAllLabor = 42,
        RemoveExclusion = 43,
        RemovePlant = 44,
        RemoveTunnelUnit = 45,
        Repair = 46,
        SetAutoConvert = 47,
        SetAutonomy = 48,
        Spawn = 49,
        SpawnAlchemist = 50,
        SpawnCrossbowTron = 51,
        SpawnFireflyNest = 52,
        SpawnGeomancer = 53,
        SpawnHalbardier = 54,
        SpawnWithLevel = 55,
        StartBarrelFuse = 56,
        StockpileAcceptAll = 57,
        StockpileAcceptOnly = 58,
        StockpileKeepEmpty = 59,
        Stop = 60,
        TransformLog = 61,
        TunnelToggleResource = 62,
        TurnTo = 63,
        UnequipAllTools = 64,
        UnequipIntoRack = 65,
        CraftingDoNothing = 66,
        CraftingLoop = 67,
        CraftingProduce = 68,
        CraftingQueueRecipe = 69,
        CraftingRemoveAt = 70,
        AddProjectCapacity = 71,
        AssignWorker = 72,
        CancelCommit = 73,
        ClearTarget = 74,
        Commit = 75,
        Create = 76,
        Delete = 77,
        DestroyBluePrint = 78,
        LoadIntoCatapult = 79,
        LowerPriority = 80,
        PlaceAnti = 81,
        PlaceBlueprint = 82,
        RaisePriority = 83,
        RemoveProjectCapacity = 84,
        RequestFireAtWill = 85,
        RequestFireOnce = 86,
        RequestHoldFire = 87,
        SendBlueprintsToNewProject = 88,
        SetLandscape = 89,
        SetMine = 90,
        SetTarget = 91,
        ShootCatapult = 92,
        SpawnGoupe = 93,
        SpawnHarvestProject = 94,
        SpawnLandscapeProject = 95,
        SpawnMineProject = 96,
        SpawnPlacedBlueprint = 97,
        SpawnPlacedRopeBridge = 98,
        SpawnRaidGoupeOccupy = 99,
        UnloadFromCatapult = 100,
        WorkOnCatapult = 101,
        /// <summary>LAB-ONLY (not a UNet Cmd): placement phase before t=0. Actor = unit index+1, X,Y,Z = world voxel (standing cell). Never appears in a game trace.</summary>
        PlaceWorker = 102,
        Count = 103
    }

    public static class EcoCmdNames
    {
        /// <summary>Exact UNet method name per EcoCmd value (index = (int)EcoCmd).</summary>
        public static readonly string[] Names = new string[] {
            "",
            "CmdAccepterPosition",
            "CmdAccepterRayon",
            "CmdAddBackToWorkGoal",
            "CmdAddBreakVoxelGoal",
            "CmdAddCallToArmsGoal",
            "CmdAddCleanGoal",
            "CmdAddEmergencyRecallGoal",
            "CmdAddExclusion",
            "CmdAddMoveToGoal",
            "CmdAddMoveToVolumeGoal",
            "CmdAddNatureHarvestGoal",
            "CmdAddSmartTransferGoal",
            "CmdAddTreeGoal",
            "CmdArmBearTrap",
            "CmdAttack",
            "CmdBatchedContextualActionGameObject",
            "CmdBatchedContextualActionVector",
            "CmdBreakObject",
            "CmdChangeProject",
            "CmdCraftingAddResources",
            "CmdCraftingAssignWorker",
            "CmdCraftingRemoveProduction",
            "CmdCraftingRemoveResources",
            "CmdCraftingWorkAt",
            "CmdEquipTool",
            "CmdEquipToolRackContent",
            "CmdFireAt",
            "CmdFireAtObject",
            "CmdGoAndJumpTo",
            "CmdGoDetonate",
            "CmdGoDropAt",
            "CmdGoStoreInto",
            "CmdHarvestSetResource",
            "CmdHarvestToggleResource",
            "CmdInstantiateTunnelUnit",
            "CmdJumpOff",
            "CmdLockDoor",
            "CmdManualConversion",
            "CmdMoveAttack",
            "CmdPickup",
            "CmdQuarryToggleResource",
            "CmdRemoveAllLabor",
            "CmdRemoveExclusion",
            "CmdRemovePlant",
            "CmdRemoveTunnelUnit",
            "CmdRepair",
            "CmdSetAutoConvert",
            "CmdSetAutonomy",
            "CmdSpawn",
            "CmdSpawnAlchemist",
            "CmdSpawnCrossbowTron",
            "CmdSpawnFireflyNest",
            "CmdSpawnGeomancer",
            "CmdSpawnHalbardier",
            "CmdSpawnWithLevel",
            "CmdStartBarrelFuse",
            "CmdStockpileAcceptAll",
            "CmdStockpileAcceptOnly",
            "CmdStockpileKeepEmpty",
            "CmdStop",
            "CmdTransformLog",
            "CmdTunnelToggleResource",
            "CmdTurnTo",
            "CmdUnequipAllTools",
            "CmdUnequipIntoRack",
            "CmdcraftingDoNothing",
            "CmdcraftingLoop",
            "CmdcraftingProduce",
            "CmdcraftingQueueRecipe",
            "CmdcraftingRemoveAt",
            "CmdAddProjectCapacity",
            "CmdAssignWorker",
            "CmdCancelCommit",
            "CmdClearTarget",
            "CmdCommit",
            "CmdCreate",
            "CmdDelete",
            "CmdDestroyBluePrint",
            "CmdLoadIntoCatapult",
            "CmdLowerPriority",
            "CmdPlaceAnti",
            "CmdPlaceBlueprint",
            "CmdRaisePriority",
            "CmdRemoveProjectCapacity",
            "CmdRequestFireAtWill",
            "CmdRequestFireOnce",
            "CmdRequestHoldFire",
            "CmdSendBlueprintsToNewProject",
            "CmdSetLandscape",
            "CmdSetMine",
            "CmdSetTarget",
            "CmdShootCatapult",
            "CmdSpawnGoupe",
            "CmdSpawnHarvestProject",
            "CmdSpawnLandscapeProject",
            "CmdSpawnMineProject",
            "CmdSpawnPlacedBlueprint",
            "CmdSpawnPlacedRopeBridge",
            "CmdSpawnRaidGoupeOccupy",
            "CmdUnloadFromCatapult",
            "CmdWorkOnCatapult",
            "LabPlaceWorker",
        };
        /// <summary>True for lab-only commands that have no UNet counterpart.</summary>
        public static bool IsLabOnly(EcoCmd c) { return c == EcoCmd.PlaceWorker; }
        /// <summary>Spawn* commands: masked off for the economy agent (strictly 6 workers).</summary>
        public static bool IsSpawn(EcoCmd c)
        {
            string n = Names[(int)c];
            return n.StartsWith("CmdSpawn", StringComparison.Ordinal);
        }
        /// <summary>Combat-only commands that the economy agent never issues.</summary>
        public static bool IsCombat(EcoCmd c)
        {
            switch (c)
            {
                case EcoCmd.Attack:
                case EcoCmd.MoveAttack:
                case EcoCmd.FireAt:
                case EcoCmd.FireAtObject:
                case EcoCmd.GoDetonate:
                case EcoCmd.StartBarrelFuse:
                case EcoCmd.ArmBearTrap:
                case EcoCmd.AddCallToArmsGoal:
                case EcoCmd.AddEmergencyRecallGoal:
                case EcoCmd.RequestFireAtWill:
                case EcoCmd.RequestFireOnce:
                case EcoCmd.RequestHoldFire:
                case EcoCmd.ShootCatapult:
                case EcoCmd.LoadIntoCatapult:
                case EcoCmd.UnloadFromCatapult:
                case EcoCmd.WorkOnCatapult:
                    return true;
                default: return false;
            }
        }
        static System.Collections.Generic.Dictionary<string, EcoCmd> byName;
        public static bool TryParse(string name, out EcoCmd cmd)
        {
            if (byName == null)
            {
                var d = new System.Collections.Generic.Dictionary<string, EcoCmd>(StringComparer.Ordinal);
                for (int i = 1; i < (int)EcoCmd.Count; i++) d[Names[i]] = (EcoCmd)i;
                byName = d;
            }
            return byName.TryGetValue(name, out cmd);
        }
    }
}
