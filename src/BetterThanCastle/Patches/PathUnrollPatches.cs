using System;
using System.Collections.Generic;
using Brix.Game.AI;
using Brix.Pathfinding;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Legacy only added diagnostic logging (qol_blocked.log) to RemoveInvalidNodes; the per-node "cut" lines are not ported.
    [HarmonyPatch(typeof(PathUnroll), "RemoveInvalidNodes")]
    internal static class PathUnrollRemoveInvalidNodesPatch
    {
        private static void Prefix(PathUnroll __instance, List<PathBranch> branches)
        {
            if (!BtcQol.LogOn) return; // avoid per-path string building when logging is off
            try
            {
                int emptyAt = -1;
                for (int i = 0; i < branches.Count; i++)
                {
                    if (branches[i].Type.CompareTo(PathBranch.BranchType.Empty) <= 0) { emptyAt = i; break; }
                }
                BtcQol.LogNote("PathUnroll " + __instance.transform.name + " n=" + branches.Count + " emptyAt=" + emptyAt);
            }
            catch (Exception e) { BtcLog.Error("Hook", "PathUnroll.RemoveInvalidNodes: " + e); }
        }
    }
}
