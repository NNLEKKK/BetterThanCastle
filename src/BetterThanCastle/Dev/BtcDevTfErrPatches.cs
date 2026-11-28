#if DEV
using System;
using Brix.Game.AI;
using Brix.Game;
using BetterThanCastle.Patches;
using Brix.Lifecycle.Pooling;
using HarmonyLib;

namespace BetterThanCastle
{
    // DEV: why a terraform worker fails his goal (work / path exceptions), rate-limited
    [HarmonyPatch(typeof(Labor), "HandleWorkException")]
    internal static class BtcDevTfWorkErrPatch
    {
        internal static int N;
        private static void Prefix(Labor __instance, Exceptions.WorkFailedException exception)
        {
            try
            {
                if (BtcTerraform.ByObj.Count == 0 || N > 400) return;
                if (__instance.Goal == null)
                {
                    // the fill work has no goal (BtcTerraformFill)
                    if (__instance.Project != null && BtcTerraform.Of(__instance.Project) != null)
                    { N++; BtcLog.Info("TfErr", __instance.name + " fill work " + (exception == null ? "null" : exception.GetType().Name)); }
                    return;
                }
                if (TerraformDigSetting.Get(__instance.Goal as DigGoal) == null && !(__instance.Goal is BuildStairsGoal)) return;
                N++;
                BtcLog.Info("TfErr", __instance.name + " work " + (exception == null ? "null" : exception.GetType().Name) + " goal@" + __instance.Goal.voxel.position);
            }
            catch (Exception) { }
        }
    }

    [HarmonyPatch(typeof(Labor), "HandlePathFail")]
    internal static class BtcDevTfPathErrPatch
    {
        private static void Prefix(Labor __instance, Exceptions.PathException e)
        {
            try
            {
                if (BtcTerraform.ByObj.Count == 0 || BtcDevTfWorkErrPatch.N > 400 || __instance.Goal == null) return;
                if (TerraformDigSetting.Get(__instance.Goal as DigGoal) == null) return;
                BtcDevTfWorkErrPatch.N++;
                BtcLog.Info("TfErr", __instance.name + " path " + (e == null ? "null" : e.GetType().Name) + " goal@" + __instance.Goal.voxel.position);
            }
            catch (Exception) { }
        }
    }
}
#endif
