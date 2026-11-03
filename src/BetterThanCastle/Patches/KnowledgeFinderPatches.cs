using System;
using Brix.Game.AI.KnowledgeSpace;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(KnowledgeDisjointRegionsFinder), MethodType.Constructor, new Type[0])]
    internal static class KnowledgeDisjointRegionsFinderCtorPatch
    {
        private static void Postfix(KnowledgeDisjointRegionsFinder __instance)
        {
            try { AccessTools.Field(typeof(KnowledgeDisjointRegionsFinder), "SearchPerFrame").SetValue(__instance, BtcBalanceExtra.KnowledgeUpdaterBudget(__instance.SearchPerFrame)); }
            catch (Exception e) { BtcLog.Error("Hook", "KnowledgeDisjointRegionsFinder..ctor: " + e); }
        }
    }

    [HarmonyPatch(typeof(KnowledgeConnectedAccessFinder), MethodType.Constructor, new Type[0])]
    internal static class KnowledgeConnectedAccessFinderCtorPatch
    {
        private static void Postfix(KnowledgeConnectedAccessFinder __instance)
        {
            try { AccessTools.Field(typeof(KnowledgeConnectedAccessFinder), "SearchPerFrame").SetValue(__instance, BtcBalanceExtra.KnowledgeExcluderBudget(__instance.SearchPerFrame)); }
            catch (Exception e) { BtcLog.Error("Hook", "KnowledgeConnectedAccessFinder..ctor: " + e); }
        }
    }
}
