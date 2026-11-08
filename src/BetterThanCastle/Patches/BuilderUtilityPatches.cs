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

    /// <summary>
    /// Loading: the in-game Lua menus are opened/closed repeatedly while the world loads and every Open destroys and re-creates
    /// all panels (2668 panels were built 11805 times, up to 6x each). Vanilla already has the per-menu flag InstantiateOnlyIfMissing
    /// (reuse a panel that still has its instance; ParentTree/ResetTree/Refresh run afterwards as always); force it while loading.
    /// Config Loading/ReuseMenuPanels.
    /// </summary>
    [HarmonyPatch(typeof(BuilderUtility), "InstantiateTree")]
    internal static class BuilderUtilityInstantiateTreePatch
    {
        internal static long Forced;

        [HarmonyPrefix]
        private static void Prefix(ref bool skipIfInstanced)
        {
            try
            {
                if (skipIfInstanced || !BtcLoadBoost.Loading || !BtcLoadBoost.Enabled || !BtcConfig.ReuseMenuPanels.Value) return;
                skipIfInstanced = true;
                Forced++;
            }
            catch (Exception e) { BtcLog.Error("Hook", "BuilderUtility.InstantiateTree: " + e); }
        }
    }
}
