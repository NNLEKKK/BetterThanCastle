using System;
using System.Collections.Generic;
using Brix.Game.AI;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // mov.noAutoPick: harvest/repair projects keep cleanup and litter goals only for items the task needs
    // (BtcAutoPick.Wanted); other loose items get no goal there. A Cleanup project keeps everything its filter allows.
    [HarmonyPatch(typeof(CleanUpGoalProvider), "RefreshCleanupGoals")]
    internal static class CleanUpGoalProviderRefreshPatch
    {
        private static readonly List<CleanUpGoal> Tmp = new List<CleanUpGoal>();

        private static void Postfix(CleanUpGoalProvider __instance)
        {
            try
            {
                bool own = BtcAutoPick.IsCleanupOwned(__instance);
                CleanupProject cp = own ? BtcAutoPick.OwnerProject(__instance) : null;
                bool noStump = own && BtcChopRules.NoStump;
                bool noLogs = !own && BtcChopRules.IsHarvestOwned(__instance) && BtcChopRules.NoLogs(__instance.gameObject);
                Project task = !own && BtcAutoPick.On ? BtcAutoPick.TaskOf(__instance) : null;
                bool autoPick = task != null;
                if ((own ? cp == null : !autoPick) && !noLogs) return;
                HashSet<CleanUpGoal> goals = __instance.Goals();
                Tmp.Clear();
                foreach (CleanUpGoal g in goals)
                {
                    if (g == null) continue;
                    GameObject go = g.gameObject;
                    bool drop = own ? (noStump && BtcChopRules.IsStump(go)) || (cp != null && BtcCleanFilter.Blocked(cp, go))
                                    : (noLogs && BtcChopRules.IsLogOrStump(go)) || (autoPick && !BtcAutoPick.Wanted(task, go));
                    if (drop) Tmp.Add(g);
                }
                for (int i = 0; i < Tmp.Count; i++) goals.Remove(Tmp[i]);
                Tmp.Clear();
            }
            catch (Exception ex)
            {
                BtcLog.Error("AutoPick", ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(LitterGoalProvider), "Refresh")]
    internal static class LitterGoalProviderRefreshPatch
    {
        private static readonly List<LitterGoal> Tmp = new List<LitterGoal>();

        private static void Postfix(LitterGoalProvider __instance)
        {
            try
            {
                bool own = BtcAutoPick.IsCleanupOwned(__instance);
                CleanupProject cp = own ? BtcAutoPick.OwnerProject(__instance) : null;
                Project task = !own && BtcAutoPick.On ? BtcAutoPick.TaskOf(__instance) : null;
                if (own ? cp == null : task == null) return;
                List<LitterGoal> goals = __instance.Goals();
                Tmp.Clear();
                for (int i = 0; i < goals.Count; i++)
                {
                    LitterGoal g = goals[i];
                    if (g == null || g.Garnottes == null) continue;
                    bool fresh = false;
                    foreach (Transform t in g.Garnottes)
                    {
                        if (t != null && !(own ? BtcCleanFilter.Blocked(cp, t.gameObject) : !BtcAutoPick.Wanted(task, t.gameObject))) { fresh = true; break; }
                    }
                    if (!fresh) Tmp.Add(g);
                }
                for (int i = 0; i < Tmp.Count; i++) goals.Remove(Tmp[i]);
                Tmp.Clear();
            }
            catch (Exception ex)
            {
                BtcLog.Error("AutoPick", ex.Message);
            }
        }
    }
}
