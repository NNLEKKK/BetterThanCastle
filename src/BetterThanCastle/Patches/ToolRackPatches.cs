using System;
using Brix.Game.Semantique;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Rack kit filter (BtcRackFilter, BC ON only): the chosen kit replaces the vanilla "first item decides" exclusivity.
    [HarmonyPatch(typeof(ToolRack), "RefreshExclusivity")]
    internal static class ToolRackRefreshExclusivityPatch
    {
        private static bool Prefix(ToolRack __instance)
        {
            try { return BtcRackFilter.RefreshPrefix(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "ToolRack.RefreshExclusivity: " + e); return true; }
        }
    }

    [HarmonyPatch(typeof(ToolRack), "Start")]
    internal static class ToolRackStartPatch
    {
        private static void Postfix(ToolRack __instance)
        {
            try { BtcRackFilter.OnRackStart(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "ToolRack.Start: " + e); }
        }
    }
}
