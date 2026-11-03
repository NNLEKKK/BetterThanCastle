using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Engine.Loop;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    /// <summary>WaitForPhysicsReady: yield return new WaitForSeconds(1f) -> BtcLoadBoost.Poll().</summary>
    [HarmonyPatch]
    internal static class PhysicsLoopWaitForPhysicsReadyPatch
    {
        private static MethodBase TargetMethod() { return G3Il.MoveNextOf(typeof(PhysicsLoop), "WaitForPhysicsReady"); }

        private static bool Prepare() { return TargetMethod() != null; }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> src)
        {
            try { return G3Il.PollSwap(src, "PhysicsLoop.WaitForPhysicsReady", null); }
            catch (Exception e) { BtcLog.Error("Hook", "PhysicsLoop.WaitForPhysicsReady transpiler: " + e); return src; }
        }
    }
}
