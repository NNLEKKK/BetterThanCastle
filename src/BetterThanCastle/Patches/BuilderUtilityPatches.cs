using System;
using System.Diagnostics;
using Brix.UI.Builder.Menu;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    /// <summary>RefreshTree: count calls / time while loading (stats shown by BtcLoadBoost).</summary>
    [HarmonyPatch(typeof(BuilderUtility), "RefreshTree", new Type[] { typeof(IMenu), typeof(bool) })]
    internal static class BuilderUtilityRefreshTreePatch
    {
        [HarmonyPrefix]
        private static void Prefix(out long __state)
        {
            __state = BtcLoadBoost.Loading ? Stopwatch.GetTimestamp() : 0L;
        }

        [HarmonyPostfix]
        private static void Postfix(long __state)
        {
            if (__state == 0L) return;
            try
            {
                BtcLoadBoost.RefreshTreeCalls++;
                BtcLoadBoost.RefreshTreeMs += BtcLoadBoost.ElapsedMs(__state);
            }
            catch (Exception e) { BtcLog.Error("Hook", "BuilderUtility.RefreshTree: " + e); }
        }
    }
}
