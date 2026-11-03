using System;

namespace Btc.EcoSim
{
    /// <summary>Carried / stored item kinds of the economy sim (maps to Brix ResourceType / adjectif keys, see EcoDefs.ItemName).</summary>
    public enum EcoItem : byte { None = 0, Log, Plank, Stones, StoneBlock, RawIron, OrangeCrystal, IronIngot, Fiber, Poleaxe, KettleHat, CaptureWard, Count }

    public enum EcoBuilding : byte { None = 0, Stockpile, ChoppingBlock, Furnace, WeaponRack, Workbench, Altar, Count }

    public enum EcoKind : byte { Free = 0, Tree, Nature, Item, Container, Station, Rack, Blueprint, Crystal }

    /// <summary>Internal order op of a unit.</summary>
    public enum EcoOp : byte { None = 0, Move, Chop, Dig, Harvest, Pickup, Take, Drop, Store, Transform, Work, AddRes, Deliver, Build, Equip, UnequipRack, UnequipDrop, Wait }

    public static class EcoDefs
    {
        public const int NI = (int)EcoItem.Count;

        public static readonly string[] ItemName = { "none", "WoodBlock", "PlankBlock", "Stones", "StoneBlock", "RawIron", "OrangeCrystal", "Iron", "Plant", "Poleaxe", "KettleHat", "CaptureWard" };

        /// <summary>Per-bricktron carry capacity per item. [code] dump cap|Test.Bricktron (WoodBlock*1, PlankBlock*3, Stones*20, StoneBlock*1, Iron*3, OrangeCrystal*20, RawIron*20);
        /// Plant/tools/ward [guess]. A unit carries ONE item type at a time [guess].</summary>
        public static readonly int[] CarryCap = { 0, 1, 3, 20, 1, 20, 20, 3, 20, 1, 1, 1 };

        /// <summary>Stockpile capacity per item. [guess] (ResourceConverter fills when Stones are full).</summary>
        public static readonly int[] PileCap = { 0, 20, 40, 80, 20, 100, 100, 20, 40, 8, 8, 8 };

        public static bool IsTool(int item) { return item == (int)EcoItem.Poleaxe || item == (int)EcoItem.KettleHat; }

        // ---- recipes. [code] work/heat/ingredients from CookBook (dump recipe| lines). Station assignment [guess] except ChoppingBlock/Furnace/Altar.
        public const int RPlanks = 0, RIronIngot = 1, RPoleaxe = 2, RKettleHat = 3, RCaptureWard = 4, RCount = 5;
        public static readonly string[] RecipeName = { "Planks", "IronIngot", "Poleaxe", "KettleHat", "CaptureWard" };
        public static readonly int[] RWork = { 3, 0, 10, 10, 10 };
        public static readonly int[] RHeat = { 0, 2, 0, 0, 0 };
        public static readonly int[] RIn0 = { (int)EcoItem.Log, (int)EcoItem.RawIron, (int)EcoItem.Plank, (int)EcoItem.IronIngot, (int)EcoItem.OrangeCrystal };
        public static readonly int[] RQ0 = { 1, 10, 1, 1, 40 };
        public static readonly int[] RIn1 = { 0, 0, 0, 0, (int)EcoItem.IronIngot };
        public static readonly int[] RQ1 = { 0, 0, 0, 0, 1 };
        public static readonly int[] ROut = { (int)EcoItem.Plank, (int)EcoItem.IronIngot, (int)EcoItem.Poleaxe, (int)EcoItem.KettleHat, (int)EcoItem.CaptureWard };
        public static readonly int[] ROutQ = { 3, 1, 1, 1, 1 };
        public static readonly int[] RStation = { (int)EcoBuilding.ChoppingBlock, (int)EcoBuilding.Furnace, (int)EcoBuilding.Workbench, (int)EcoBuilding.Workbench, (int)EcoBuilding.Altar };   // Workbench for tools = [guess]

        // ---- buildings. Costs [measured] (real game, 2026-11-02): ChoppingBlock WoodBlock*1, Furnace StoneBlock*4, Workbench PlankBlock*6, Toolrack PlankBlock*3,
        // Altar StoneBlock*1 + Iron*2 + OrangeCrystal*40 + PlankBlock*3. Build hits [guess]. Stockpile is NOT a blueprint: it is a zone container made by CmdAccepterPosition.
        public static readonly string[] BuildingName = { "none", "Stockpile(zone)", "ChoppingBlock", "Furnace", "WeaponRack(Toolrack)", "Workbench", "Altar" };
        public const int CostSlots = 4;
        static int[] Z(params int[] a) { var r = new int[CostSlots]; for (int i = 0; i < a.Length; i++) r[i] = a[i]; return r; }
        const int Lg = (int)EcoItem.Log, Pl = (int)EcoItem.Plank, Sb = (int)EcoItem.StoneBlock, Ig = (int)EcoItem.IronIngot, Oc = (int)EcoItem.OrangeCrystal;
        public static readonly int[][] BCostItem = { Z(), Z(), Z(Lg), Z(Sb), Z(Pl), Z(Pl), Z(Sb, Ig, Oc, Pl) };
        public static readonly int[][] BCostQty = { Z(), Z(), Z(1), Z(4), Z(3), Z(6), Z(1, 2, 40, 3) };
        public static readonly int[] BHits = { 0, 0, 3, 8, 4, 6, 10 };
        /// <summary>Footprint cell offsets (dx,dy,dz) from the placement point. ChoppingBlock, Workbench, Altar [measured]; Furnace 2x2 and Toolrack 1x2 [guess].</summary>
        public static readonly int[][] FootX = { Z(), Z(), Z(0, -1, -1, 0), Z(0, 1, 0, 1), Z(0, 0), Z(0, 0, 0, 0), Z(0, -1) };
        public static readonly int[][] FootY = { Z(), Z(), Z(0, 0, 0, 0), Z(0, 0, 0, 0), Z(0, 0), Z(0, 0, 1, 1), Z(0, 0) };
        public static readonly int[][] FootZ = { Z(), Z(), Z(0, 0, 1, 1), Z(0, 0, 1, 1), Z(0, 1), Z(-1, 0, 0, -1), Z(0, 0) };
        public static readonly int[] FootN = { 0, 0, 4, 4, 2, 4, 2 };

        public const int RackSlots = 8;          // [guess] tools per weapon rack
        public const int StationCap = 100;       // [guess] ingredient storage per station

        public static EcoKind KindOfBuilding(int b)
        {
            switch ((EcoBuilding)b)
            {
                case EcoBuilding.Stockpile: return EcoKind.Container;
                case EcoBuilding.WeaponRack: return EcoKind.Rack;
                default: return EcoKind.Station;
            }
        }
    }

    /// <summary>Durations and yields. Every value is [guess] unless tagged. To be replaced from docs/eco/measured.md.</summary>
    public static class EcoTune
    {
        public static float WalkSpeed = 3.0f;          // [code] Walking.WalkSpeedMperS = 3 (voxel = 1 m [guess])
        public static float ChopHitSec = 1.0f;         // [guess]
        public static int ChopHits = 5;                // [guess] tree hp in hits
        public static int LogsPerTree = 3;             // [guess] DamagedTree.spawnedQty
        public static float DigSecEarth = 1.0f;        // [guess]
        public static float DigSecStone = 2.0f;        // [guess]
        public static int DigYield = 1;                // [guess] Stones per terrain voxel (GetTerrainResources amount unknown)
        public static float HarvestSec = 3.0f;         // [guess]
        public static int HarvestYield = 1;            // [code] NatureData.ExpectedQte: iron 1, pyrite 1, plant 2, boulder 4
        public static int IronCharges = 5;             // [guess] harvests per iron node before it is gone
        public static int PyriteCharges = 5;           // [guess]
        public static float PickupSec = 0.5f;          // [guess]
        public static float DropSec = 0.3f;            // [guess]
        public static float StoreSec = 0.5f;           // [guess]
        public static float TakeSec = 0.5f;            // [guess]
        public static float DeliverSec = 0.5f;         // [guess]
        public static float BuildHitSec = 1.0f;        // [guess]
        public static float WorkHitSec = 1.0f;         // [guess] CraftingStation work hit
        public static float HeatSecPerUnit = 3.0f;     // [guess] seconds per heat unit (1 orange crystal = 1 heat [guess])
        public static float EquipSec = 0.5f;           // [guess]
        public static int StonesPerBlock = 10;         // [guess] ResourceConverter: full Stones pile -> StoneBlock*(cap/StonesPerBlock)
        public static int BuildReach = 1;              // [guess] horizontal reach to a 1-cell building
        public static int OrderLatencyTicks = 0;       // [guess] 0: sim orders take effect at once

        public static int Ticks(float seconds) { int t = (int)Math.Ceiling(seconds * EcoParams.TicksPerSecond - 1e-4); return t < 1 ? 1 : t; }
    }
}
