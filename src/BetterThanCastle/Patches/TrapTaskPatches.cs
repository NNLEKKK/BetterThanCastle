using System;
using Brix.Game.AI;
using Brix.Game.Components;
using HarmonyLib;
using Motus.Behavior;

namespace BetterThanCastle.Patches
{
    // Bear traps task (BtcTrapTask): a vanilla Repair task tagged as a trap task does trap work instead of repair goals.
    // RepairProject overrides none of these, so the Project methods are patched and filtered by the tag.

    [HarmonyPatch(typeof(Project), "HasWorkFor")]
    internal static class TrapTaskHasWorkPatch
    {
        private static bool Prefix(Project __instance, Labor labor, ref bool __result)
        {
            BtcTrapTask.Info e = BtcTrapTask.Of(__instance);
            if (e == null) return true;
            try { __result = BtcTrapTask.HasWorkFor(e, __instance, labor); }
            catch (Exception ex) { BtcLog.Error("Hook", "TrapTask.HasWorkFor: " + ex.Message); __result = false; }
            return false;
        }
    }

    [HarmonyPatch(typeof(Project), "GetInstruction")]
    internal static class TrapTaskInstructionPatch
    {
        private static bool Prefix(Project __instance, Labor labor, Var<OrderPackage<Labor>> result, ref Node __result)
        {
            BtcTrapTask.Info e = BtcTrapTask.Of(__instance);
            if (e == null) return true;
            try { __result = BtcTrapTask.GetInstruction(e, __instance, labor, result); }
            catch (Exception ex) { BtcLog.Error("Hook", "TrapTask.GetInstruction: " + ex); __result = Node.Fail(FailException.Generic); }
            return false;
        }
    }

    [HarmonyPatch(typeof(Project), "VerifyWorkFor")]
    internal static class TrapTaskVerifyWorkPatch
    {
        // the storage check of the task is ours (HandsOk / store first), the repair task's own check would ask for a stockpile
        private static bool Prefix(Project __instance, ref Node __result)
        {
            if (BtcTrapTask.Of(__instance) == null) return true;
            __result = Node.Empty;
            return false;
        }
    }

    [HarmonyPatch(typeof(Project), "GoalCounts")]
    internal static class TrapTaskGoalCountsPatch
    {
        // goals = fired traps of the task (priority input, goal counter of the task popup)
        private static bool Prefix(Project __instance, ref int __result)
        {
            BtcTrapTask.Info e = BtcTrapTask.Of(__instance);
            if (e == null) return true;
            try { __result = BtcTrapTask.GoalCount(e); }
            catch (Exception ex) { BtcLog.Error("Hook", "TrapTask.GoalCounts: " + ex.Message); __result = 0; }
            return false;
        }
    }

    [HarmonyPatch(typeof(RepairGoalProvider), "VoxelNeedsGoal")]
    internal static class TrapTaskRepairGoalPatch
    {
        // a trap task never repairs blocks
        private static bool Prefix(RepairGoalProvider __instance, ref SpawnOrder __result)
        {
            if (BtcTrapTask.ByObj.Count == 0 || BtcTrapTask.Of(__instance) == null) return true;
            __result = SpawnOrder.None;
            return false;
        }
    }

    [HarmonyPatch(typeof(CleanUpGoalProvider), "RefreshCleanupGoals")]
    internal static class TrapTaskCleanupGoalPatch
    {
        // nor picks up items around its beacon
        private static void Postfix(CleanUpGoalProvider __instance)
        {
            if (BtcTrapTask.ByObj.Count == 0) return;
            try
            {
                Project p = __instance.GetComponentInParent<Project>();
                if (p != null && BtcTrapTask.Of(p) != null) __instance.Goals().Clear();
            }
            catch (Exception ex) { BtcLog.Error("Hook", "TrapTask.Cleanup: " + ex.Message); }
        }
    }
}
