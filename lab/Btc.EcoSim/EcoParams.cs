// Every constant of the economy sim, tagged like Btc.Sim SimParams: [code] read from ref/vanilla,
// [measured] from docs/eco/measured.md (none yet), [guess] placeholder until measured.
// SCOPE (user, 2026-11-02): NO automatic worker AI. Units only do what the model orders; ProjectPriority scoring
// and Labor.AssignToIdle are NOT modelled (reference weights are kept as a comment at the bottom).
using System;
using System.Collections.Generic;

namespace Btc.EcoSim
{
    /// <summary>Brix.Engine.ResourceType flags. [code] Brix/Engine/ResourceType.cs</summary>
    [Flags] public enum EcoResource { None = 0, StoneBlock = 1, WoodenBlock = 2, Stones = 4, RawIron = 8, BlueCrystal = 16, PersistentObject = 32, TronStone = 64, OrangeCrystal = 128 }

    /// <summary>GroundResource flags. [code] GroundResource.cs</summary>
    [Flags] public enum EcoGroundResource { None = 0, Stone = 1, Brimstone = 2, Iron = 4, All = 7 }

    /// <summary>NatureType flags. [code] Brix/Engine/Nature/NatureType.cs</summary>
    [Flags] public enum EcoNature { None = 0, Plant = 1, Boulder = 2, Iron = 4, Pyrite = 8, Tree = 16, Everything = 31 }

    /// <summary>AutonomyStatus.Ids. [code] AutonomyStatus.cs. Sim rule: FreeAgent behaves exactly like WaitForOrder (no auto job search).</summary>
    public enum EcoAutonomy { WaitForOrder = 0, WaitForOrderDefensive = 5, HoldingPosition = 10, Harpooner = 15, InstinctOnly = 20, LockedToOperable = 30, FreeAgent = 40 }

    /// <summary>ProjectPriority.Bias. [code] ProjectPriority.cs (command CmdRaisePriority/CmdLowerPriority still exist; no effect on assignment).</summary>
    public enum EcoBias { Low = 0, Medium = 4, High = 8, Locked = -10 }

    /// <summary>Terrain byte values (VoxelTerrainType). [code] values from DESIGN.md (non-exhaustive).</summary>
    public static class EcoTerrain
    {
        public const byte Empty = 0, Earth = 6, Stone = 8, BlueCrystal = 38, OrangeCrystal = 44, Iron = 54, Crust1 = 55, Crust2 = 57, Crust3 = 59;
    }

    public struct EcoRecipe
    {
        public string Name;
        public int Work, Heat;            // [code] CookBook
        public EcoResource[] InRes;       // ingredient resource flag (None = crafted item)
        public string[] InKey;            // ingredient adjectif keys
        public int[] InQty;
        public string Creates;
        public int QtyCreated;
    }

    public static class EcoParams
    {
        public const int TicksPerSecond = 30;                 // [code] fixed 30 Hz logic tick
        public const float DtTick = 1f / TicksPerSecond;

        // Task delays [code] Brix/Game/AI/Task.cs:264/267 (ticks). Mod bai.delayOn=1 with delay 0 zeroes DelayOnFinished.
        public const int TaskDelayOnFinishedVanilla = 10;
        public const int TaskDelayOnFailVanilla = 30;
        public const int ProjectDelayOnFailure = 1;           // [code] Project.cs:1754
        public const int TaskDelayOnFinishedMod = 0;          // [code] gameplay.txt bai.delayOn=1, delay 0
        public const float HoldSecondsMod = 5f;               // [code] gameplay.txt bai.hold=5

        // Worker on a project (after explicit CmdChangeProject): takes the nearest open goal. [guess] to be measured vs real game.
        public const bool ProjectGoalRuleNearest = true;      // [guess]

        // Recipes [code] Brix/Game/Components/CookBook.cs (critical path only)
        public static readonly EcoRecipe Planks = R("Planks", 3, 0, "Wood_DoublePlank_AA", 3, "woodBlock", EcoResource.WoodenBlock, 1);
        public static readonly EcoRecipe IronIngot = R("IronIngot", 0, 2, "IronIngot", 1, "rawIron", EcoResource.RawIron, 10);
        public static readonly EcoRecipe Poleaxe = R("Poleaxe", 10, 0, "Poleaxe", 1, "plankBlock", EcoResource.None, 1);   // the halberd weapon
        public static readonly EcoRecipe KettleHat = R("KettleHat", 10, 0, "KettleHat", 1, "iron", EcoResource.None, 1);
        public static readonly EcoRecipe CaptureWard = new EcoRecipe
        {
            Name = "CaptureWard", Work = 10, Heat = 0, Creates = "CaptureWard", QtyCreated = 1,
            InKey = new string[] { "orangeCrystal", "iron" }, InRes = new EcoResource[] { EcoResource.OrangeCrystal, EcoResource.None }, InQty = new int[] { 40, 1 }
        };

        static EcoRecipe R(string name, int work, int heat, string creates, int qty, string inKey, EcoResource inRes, int inQty)
        {
            return new EcoRecipe { Name = name, Work = work, Heat = heat, Creates = creates, QtyCreated = qty,
                InKey = new string[] { inKey }, InRes = new EcoResource[] { inRes }, InQty = new int[] { inQty } };
        }

        public static EcoRecipe[] AllRecipes() { return new EcoRecipe[] { Planks, IronIngot, Poleaxe, KettleHat, CaptureWard }; }

        // Movement / action timings: none measured yet. [guess] until docs/eco/measured.md exists.
        public const float WalkSpeedGuess = 3.0f;      // [guess] voxels/s
        public const float DigSecondsGuess = 1.0f;     // [guess] per voxel
        public const float ChopSecondsGuess = 4.0f;    // [guess] per tree
        public const int CarryCapacityGuess = 1;       // [guess] items per bricktron
        public const int UnitCount = 6;                // [code] user decision: strictly 6 workers
        public const float InvalidBlueprintPenalty = -0.02f;  // [user/guess] one-shot reward penalty per red (invalid) blueprint placement
        public const float StallSeconds = 60f;         // [code] user decision: stall = 60 s without a new maximum

        // Reference only (NOT modelled): ProjectPriority score = sum(input*weight). Build: crewSize -1, globalCrewSize -0.5,
        // isCurrent 2, distance -1, manualBias 1; Harvest/CleanUp add hasCleanup 3; Idle bias -10000. Bias Low0 Med4 High8 Locked-10.
    }

    /// <summary>User's mod settings relevant to the sim. The sim refuses to run on a different snapshot.</summary>
    public static class GameplaySnapshot
    {
        // [code] game/csmod/BetterThanCastle/configs/gameplay.txt (2026-11-02). Keys outside this list are irrelevant to the economy.
        public const string Expected = "eco.stable=1;mov.items=1;mov.retry=1;mov.cancel=1;mov.simple=1;bai.delayOn=1;bai.wait=1;bai.build=1;bai.hold=5";

        public static Dictionary<string, string> Parse(string s)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            if (s == null) return d;
            foreach (string kv in s.Split(';'))
            {
                int i = kv.IndexOf('=');
                if (i > 0) d[kv.Substring(0, i).Trim()] = kv.Substring(i + 1).Trim();
            }
            return d;
        }

        /// <summary>Returns null when the snapshot matches, else a message naming the first differing key.</summary>
        public static string Check(string gp)
        {
            var have = Parse(gp);
            var want = Parse(Expected);
            foreach (var kv in want)
            {
                string v;
                if (!have.TryGetValue(kv.Key, out v)) return "missing setting " + kv.Key + " (expected " + kv.Value + ")";
                if (v != kv.Value) return "setting " + kv.Key + "=" + v + " (expected " + kv.Value + ")";
            }
            return null;
        }
    }
}
