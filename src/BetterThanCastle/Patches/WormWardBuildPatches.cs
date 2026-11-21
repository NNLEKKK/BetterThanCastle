using System;
using Brix.Game.AI;
using Brix.Game.AI.KnowledgeSpace;
using Brix.Game.Components;
using Brix.Game.Network;
using Brix.Game.Semantique;
using Brix.Input;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    /// <summary>Any blueprint choice other than our own call ends the wormhole build mode (BtcWormWardBuild).</summary>
    [HarmonyPatch(typeof(InputModeController), "PlaceBlueprint")]
    internal static class WormWardPlaceBlueprintPatch
    {
        private static void Prefix()
        {
            try { BtcWormWardBuild.OnPlaceBlueprint(); }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard PlaceBlueprint: " + e.Message); }
        }
    }

    /// <summary>A sentinel blueprint placed in wormhole mode: its position goes to the host as a tag.</summary>
    [HarmonyPatch(typeof(BlueprintPlacementPicker), "PlaceBlueprint", new[] { typeof(Slave) })]
    internal static class WormWardPickerPlacePatch
    {
        private static void Prefix(Slave s)
        {
            if (!BtcWormWardBuild.Placing || s == null) return;
            try { BtcWormWardBuild.OnPlaced(s.GetHeldComponent<Blueprint>()); }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard picker PlaceBlueprint: " + e.Message); }
        }
    }

    /// <summary>Host: a blueprint spawned from a player command (matched with the pending tags).</summary>
    [HarmonyPatch(typeof(UNetBlueprint), "_FinishSpawnBlueprint")]
    internal static class WormWardFinishSpawnPatch
    {
        private static void Prefix(Blueprint blueprint, bool instant)
        {
            if (instant) return;
            try { BtcWormWardBuild.OnSpawned(blueprint); }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard _FinishSpawnBlueprint: " + e.Message); }
        }
    }

    /// <summary>A builder looks for an item for a target: the target decides which ward kind is wanted.</summary>
    [HarmonyPatch(typeof(Labor), "Find", new[] { typeof(Recepteur) })]
    internal static class WormWardLaborFindPatch
    {
        private static void Prefix(Recepteur target)
        {
            try { BtcWormWardBuild.BeginFind(target); }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard Labor.Find: " + e.Message); BtcWormWardBuild.Want = 0; }
        }

        private static Exception Finalizer(Exception __exception)
        {
            BtcWormWardBuild.Want = 0;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Knowledge), "_ClosestObjectToPickup_Test")]
    internal static class WormWardPickupTestPatch
    {
        private static void Postfix(IGameComponent gc, ref bool __result)
        {
            if (!__result || BtcWormWardBuild.Want == 0) return;
            try { __result = BtcWormWardBuild.Accept(gc.GameObject, gc.recepteur); }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard pickup test: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Recepteur), "FirstObjectOf", new[] { typeof(Ressource) })]
    internal static class WormWardFirstObjectPatch
    {
        private static void Postfix(Recepteur __instance, ref GameObject __result)
        {
            if (BtcWormWardBuild.Want == 0 || __result == null) return;
            try { __result = BtcWormWardBuild.Pick(__instance, __result); }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard FirstObjectOf: " + e.Message); }
        }
    }

    /// <summary>A sentinel blueprint turns into a new ward object: it is a wormhole ward when a wormhole ward was delivered (the item decides).
    /// A tagged blueprint refuses a plain ward: the item is dropped next to it and the blueprint stays.</summary>
    [HarmonyPatch(typeof(Blueprint), "MorphToObject")]
    internal static class WormWardMorphPatch
    {
        private static bool Prefix(Blueprint __instance, out GameObject __state, ref GameObject __result)
        {
            __state = null;
            if (BtcWormWard.Count == 0 && BtcWormWardBuild.TaggedCount == 0) return true;
            try
            {
                int verdict = BtcWormWardBuild.BeforeMorph(__instance);
                if (verdict < 0) { __result = null; return false; }
                if (verdict > 0) __state = __instance.gameObject;
            }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard MorphToObject prefix: " + e.Message); }
            return true;
        }

        private static void Postfix(GameObject __state, GameObject __result)
        {
            if (__state == null) return;
            try { BtcWormWardBuild.OnMorphed(__state, __result); }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard MorphToObject: " + e.Message); }
        }
    }

    /// <summary>Moving a sentinel ward moves exactly that object (vanilla takes "any item with the same asset key" from a stockpile or the
    /// ground, which could swap a wormhole ward for a plain one).</summary>
    [HarmonyPatch(typeof(Transaction), "StoreEverything")]
    internal static class WormWardStorePatch
    {
        private static bool Prefix(GameObject from, GameObject into)
        {
            if (BtcWormWard.Count == 0 || from == null || into == null) return true;
            try { return !BtcWormWardBuild.StoreExact(from, into); }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard StoreEverything: " + e.Message); return true; }
        }
    }
}

namespace BetterThanCastle.Patches
{
    /// <summary>A wormhole blueprint never takes a plain sentinel ward in: the add is skipped, the ward stays with the worker (no loss).</summary>
    [HarmonyPatch(typeof(Recepteur), "AddObject", new[] { typeof(GameObject), typeof(bool) })]
    internal static class WormWardAddObjectPatch
    {
        private static bool Prefix(Recepteur __instance, GameObject item)
        {
            if (BtcWormWardBuild.TaggedCount == 0 || item == null) return true;
            try { return !BtcWormWardBuild.RefuseAdd(__instance, item); }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard AddObject: " + e.Message); return true; }
        }
    }

    /// <summary>After Labor.Find: a wrong-kind ward for a sentinel blueprint is swapped for the nearest ward of the right kind.</summary>
    [HarmonyPatch(typeof(Labor), "Find", new[] { typeof(Recepteur) })]
    internal static class WormWardLaborFindResultPatch
    {
        [HarmonyPriority(Priority.High)]
        private static void Postfix(Labor __instance, Recepteur target, ref GameObject __result)
        {
            if (BtcWormWard.Count == 0 && BtcWormWardBuild.TaggedCount == 0) return;
            try { __result = BtcWormWardBuild.FixFind(__instance, target, __result); }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard Labor.Find result: " + e.Message); }
        }
    }
}
