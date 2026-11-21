using System;
using Brix.Game.AI;
using HarmonyLib;
using Motus.Behavior;

namespace BetterThanCastle.Patches
{
    // Vanilla: user-ordered task => "return Node.Empty;". Legacy: try BtcQol.PathRetry first.
    [HarmonyPatch(typeof(Task), "OnPathFailed")]
    internal static class TaskOnPathFailedPatch
    {
        private static void Postfix(Task __instance, Labor worker, ref Node __result)
        {
            try
            {
                if (__instance.InitialPackage.DecisionSource != Labor.DecisionSource.User) return;
                Node retry = BtcQol.PathRetry(__instance, worker);
                if (retry != null) __result = retry;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Task.OnPathFailed: " + e); }
        }
    }
}
