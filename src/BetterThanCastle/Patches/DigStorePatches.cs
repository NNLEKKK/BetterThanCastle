using System;
using Brix.Game.AI;
using HarmonyLib;
using Motus.Behavior;

namespace BetterThanCastle.Patches
{
    // Rule work.digStore (BtcDigStore): quarry / tunnel workers with a big item in the hands store it before digging.
    // MineProject and TunnelProject do not override GetInstruction, so the Project method is patched and filtered by type.
    [HarmonyPatch(typeof(Project), "GetInstruction")]
    internal static class DigStoreInstructionPatch
    {
        private static bool Prefix(Project __instance, Labor labor, Var<OrderPackage<Labor>> result, ref Node __result)
        {
            try
            {
                if (__instance is LandscapeProject && BtcTerraform.Of(__instance) != null) return true;   // TerraformInstructionPatch
                if (!BtcDigStore.TryStoreFirst(__instance, labor, result)) return true;
                __result = Node.Empty;
                return false;
            }
            catch (Exception ex) { BtcLog.Error("Hook", "DigStore.GetInstruction: " + ex.Message); return true; }
        }
    }
}
