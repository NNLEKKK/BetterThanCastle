#if DEV
using System;
using Brix.Game.AI;
using HarmonyLib;
using Motus.Behavior;

namespace BetterThanCastle
{
    // DEV trace of vanilla BuildStairsGoal (terraform ladders): match result, work node, spawn; throttled by a counter.
    [HarmonyPatch(typeof(BuildStairsGoal), "IsMatchFor")]
    internal static class BtcDevStairsMatchTrace
    {
        private static int _n;
        private static void Postfix(BuildStairsGoal __instance, bool __result)
        {
            if (BtcTerraform.ByObj.Count == 0 || _n++ > 40) return;
            BtcLog.Info("DevStairs", "match " + __instance.voxel.position + " -> " + __result);
        }

        private static Exception Finalizer(Exception __exception)
        {
            if (__exception != null && _n < 60) BtcLog.Warn("DevStairs", "match threw " + __exception);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(BuildStairsGoal), "GetWorkNode")]
    internal static class BtcDevStairsWorkTrace
    {
        private static int _n;
        private static void Postfix(BuildStairsGoal __instance, Node __result)
        {
            if (BtcTerraform.ByObj.Count == 0 || _n++ > 40) return;
            BtcLog.Info("DevStairs", "work " + __instance.voxel.position + " node=" + (__result == null ? "null" : __result.GetType().Name));
        }
    }

    [HarmonyPatch(typeof(BuildStairsGoal), "SpawnStairs")]
    internal static class BtcDevStairsSpawnTrace
    {
        private static void Postfix(BuildStairsGoal __instance)
        {
            BtcLog.Info("DevStairs", "spawn " + __instance.voxel.position + " fwd=" + __instance.transform.forward);
        }
    }

    [HarmonyPatch(typeof(BuildStairsGoal), "PriorityFor")]
    internal static class BtcDevStairsPrioTrace
    {
        private static int _n;
        private static Exception Finalizer(BuildStairsGoal __instance, Exception __exception)
        {
            if (__exception != null && _n++ < 10) BtcLog.Warn("DevStairs", "prio threw " + __exception.GetType().Name + ": " + __exception.Message);
            return __exception;
        }
    }
}
#endif
