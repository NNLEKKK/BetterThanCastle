using System;
using System.Collections.Generic;
using Brix.Game.AI;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // mov.noAutoPick: harvest/repair projects keep cleanup and litter goals only for fresh items (what the project itself
    // dropped); loose items that were lying around earlier get no goal there. A Cleanup project keeps everything.
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
                if (own ? cp == null : !BtcAutoPick.On) return;
                HashSet<CleanUpGoal> goals = __instance.Goals();
                Tmp.Clear();
                foreach (CleanUpGoal g in goals) if (g != null && (own ? BtcCleanFilter.Blocked(cp, g.gameObject) : BtcAutoPick.IsOld(g.gameObject))) Tmp.Add(g);
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
                if (own ? cp == null : !BtcAutoPick.On) return;
                List<LitterGoal> goals = __instance.Goals();
                Tmp.Clear();
                for (int i = 0; i < goals.Count; i++)
                {
                    LitterGoal g = goals[i];
                    if (g == null || g.Garnottes == null) continue;
                    bool fresh = false;
                    foreach (Transform t in g.Garnottes)
                    {
                        if (t != null && !(own ? BtcCleanFilter.Blocked(cp, t.gameObject) : BtcAutoPick.IsOld(t.gameObject))) { fresh = true; break; }
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
