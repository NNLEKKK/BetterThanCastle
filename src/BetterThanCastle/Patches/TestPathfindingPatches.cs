using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Pathfinding.Test;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    /// <summary>Time-sliced node read loop (BtcLoadBoost.Slicer replaces the fixed 10000-step yield).</summary>
    [HarmonyPatch]
    internal static class TestPathfindingReadAllPathNodesPatch
    {
        private static MethodBase TargetMethod() { return G3Il.MoveNextOf(typeof(TestPathfinding), "ReadAllPathNodes"); }

        private static bool Prepare() { return TargetMethod() != null; }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> src)
        {
            try { return G3Il.SliceSwap(src, "TestPathfinding.ReadAllPathNodes"); }
            catch (Exception e) { BtcLog.Error("Hook", "TestPathfinding.ReadAllPathNodes transpiler: " + e); return src; }
        }
    }

    /// <summary>Time-sliced lod0 rebuild loop.</summary>
    [HarmonyPatch]
    internal static class TestPathfindingInitPathfindingPatch
    {
        private static MethodBase TargetMethod() { return G3Il.MoveNextOf(typeof(TestPathfinding), "InitPathfinding"); }

        private static bool Prepare() { return TargetMethod() != null; }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> src)
        {
            try { return G3Il.SliceSwap(src, "TestPathfinding.InitPathfinding"); }
            catch (Exception e) { BtcLog.Error("Hook", "TestPathfinding.InitPathfinding transpiler: " + e); return src; }
        }
    }
}
