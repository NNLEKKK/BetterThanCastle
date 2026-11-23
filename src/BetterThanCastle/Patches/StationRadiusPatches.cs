using System;
using Brix.Game.AI;
using Brix.Game.AI.KnowledgeSpace;
using Brix.Game.Components;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // N13: floor pickup radius (BtcStationRadius). The search sets the context, the per-candidate test rejects far floor items.
    [HarmonyPatch(typeof(Knowledge), "ClosestObjectToPickup")]
    internal static class StationRadiusSearchPatch
    {
        private static void Prefix(Labor source)
        {
            try { BtcStationRadius.Begin(source); }
            catch (Exception e) { BtcStationRadius.End(); BtcLog.Error("StationR", "Begin: " + e.Message); }
        }

        private static Exception Finalizer(Exception __exception)
        {
            BtcStationRadius.End();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Knowledge), "_ClosestObjectToPickup_Test")]
    internal static class StationRadiusTestPatch
    {
        private static void Postfix(IGameComponent gc, ref bool __result)
        {
            if (__result && BtcStationRadius.Reject(gc)) __result = false;
        }
    }

    // A free worker decides on its own to work at a chopping block (not hired yet): the block is the fetch context.
    [HarmonyPatch(typeof(CraftingStation), "CanFetch")]
    internal static class StationRadiusCanFetchPatch
    {
        private static void Prefix(CraftingStation __instance)
        {
            try { BtcStationRadius.BeginDecide(__instance); }
            catch (Exception e) { BtcStationRadius.EndDecide(); BtcLog.Error("StationR", "BeginDecide: " + e.Message); }
        }

        private static void Postfix(CraftingStation __instance, Labor labor, ref bool __result)
        {
            try { if (__result && BtcStationRadius.FetchTooFar(__instance, labor)) __result = false; }
            catch (Exception e) { BtcLog.Error("StationR", "FetchTooFar: " + e.Message); }
        }

        private static Exception Finalizer(Exception __exception)
        {
            BtcStationRadius.EndDecide();
            return __exception;
        }
    }
}
