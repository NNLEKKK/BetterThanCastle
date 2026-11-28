using System;
using System.Reflection;
using Brix.Assets;
using Brix.Game.Components;
using Brix.Game.Components.Validators.Graphs;
using Brix.Game.Compounders;
using Brix.Game.Network;
using Brix.Legacy;
using Brix.UI.GameSelector;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Advanced blueprints (Features/BtcBpPreset.cs): presets on blue marks, applied when built, carried by compound blueprints.

    [HarmonyPatch]
    internal static class BpPresetTestHoverPatch
    {
        private static FieldInfo _objet;

        private static MethodBase TargetMethod()
        {
            Type t = AccessTools.Inner(typeof(PieContenu), "Menu");
            _objet = AccessTools.Field(t, "objet");
            return AccessTools.Method(t, "TestHover", new[] { typeof(string) });
        }

        private static void Postfix(object __instance, string tag, ref bool __result)
        {
            if (__result || tag == null || tag.Length < 5 || tag[0] != 'B' || !tag.StartsWith("BtcBp", StringComparison.Ordinal)) return;
            try { __result = BtcBpPreset.TestHover(_objet != null ? _objet.GetValue(__instance) : null, tag); }
            catch (Exception e) { BtcLog.Error("Hook", "PieContenu.Menu.TestHover (bp preset): " + e); }
        }
    }

    [HarmonyPatch(typeof(UISelectable), "OnHover")]
    internal static class BpPresetHoverPatch
    {
        private static void Postfix(UISelectable __instance)
        {
            try { BtcBpPreset.OnHover(__instance, true); }
            catch (Exception e) { BtcLog.Error("Hook", "UISelectable.OnHover (bp preset): " + e); }
        }
    }

    [HarmonyPatch(typeof(UISelectable), "OnUnhover")]
    internal static class BpPresetUnhoverPatch
    {
        private static void Postfix(UISelectable __instance)
        {
            try { BtcBpPreset.OnHover(__instance, false); }
            catch (Exception e) { BtcLog.Error("Hook", "UISelectable.OnUnhover (bp preset): " + e); }
        }
    }

    [HarmonyPatch(typeof(Blueprint), "MorphToObject")]
    internal static class BpPresetMorphPatch
    {
        private static void Prefix(Blueprint __instance, out string __state)
        {
            __state = null;
            try { __state = BtcBpPreset.BeforeMorph(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "Blueprint.MorphToObject (bp preset): " + e); }
        }

        private static void Postfix(Blueprint __instance, string __state, GameObject __result)
        {
            if (__state == null) return;
            try { BtcBpPreset.AfterMorph(__instance, __state, __result); }
            catch (Exception e) { BtcLog.Error("Hook", "Blueprint.MorphToObject post (bp preset): " + e); }
        }
    }

    [HarmonyPatch(typeof(UNetBlueprint), "_FinishSpawnBlueprint")]
    internal static class BpPresetFinishSpawnPatch
    {
        private static void Postfix(Blueprint blueprint, bool instant)
        {
            if (instant) return;
            try { BtcBpPreset.OnBlueprintSpawned(blueprint); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetBlueprint._FinishSpawnBlueprint (bp preset): " + e); }
        }
    }

    [HarmonyPatch(typeof(Blueprint), "OnStartPureClient")]
    internal static class BpPresetClientStartPatch
    {
        private static void Postfix(Blueprint __instance)
        {
            try { BtcBpPreset.OnBlueprintSpawned(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "Blueprint.OnStartPureClient (bp preset): " + e); }
        }
    }

    [HarmonyPatch(typeof(BlueprintCompounder), "GenerateBlueprintGraph")]
    internal static class BpPresetGraphPatch
    {
        private static void Postfix(BlueprintGraph __result)
        {
            try { BtcBpPreset.OnGraph(__result); }
            catch (Exception e) { BtcLog.Error("Hook", "BlueprintCompounder.GenerateBlueprintGraph (bp preset): " + e); }
        }
    }

    [HarmonyPatch(typeof(BlueprintGraph), "SpawnBlueprintsUnder")]
    internal static class BpPresetSpawnUnderPatch
    {
        private static void Postfix(BlueprintGraph __instance)
        {
            try { BtcBpPreset.OnSpawnedUnder(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "BlueprintGraph.SpawnBlueprintsUnder (bp preset): " + e); }
        }
    }

    [HarmonyPatch(typeof(BlueprintCompoundComponent), "StartPlaceBlueprints")]
    internal static class BpPresetStartPlacePatch
    {
        private static void Prefix(BlueprintCompoundComponent __instance)
        {
            try { BtcBpPreset.OnStartPlace(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "BlueprintCompoundComponent.StartPlaceBlueprints (bp preset): " + e); }
        }
    }

    [HarmonyPatch(typeof(BlueprintCompounder), "SaveSelected")]
    internal static class BpPresetSavePatch
    {
        private static void Prefix(out Asset_Blueprints __state) { __state = BlueprintCompounder.current; }

        private static void Postfix(Asset_Blueprints __state)
        {
            try { BtcBpPreset.OnCompoundSaved(__state); }
            catch (Exception e) { BtcLog.Error("Hook", "BlueprintCompounder.SaveSelected (bp preset): " + e); }
        }
    }

    [HarmonyPatch(typeof(BlueprintCompounder), "LoadCompoundBlueprints")]
    internal static class BpPresetLoadPatch
    {
        private static void Prefix(Asset_Blueprints compound)
        {
            try { BtcBpPreset.OnCompoundLoad(compound); }
            catch (Exception e) { BtcLog.Error("Hook", "BlueprintCompounder.LoadCompoundBlueprints (bp preset): " + e); }
        }
    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyLib.HarmonyPatch(typeof(Brix.UI.GameSelector.UISelectable), "OnSelect")]
    internal static class BpPresetSelectPatch
    {
        private static void Postfix(Brix.UI.GameSelector.UISelectable __instance)
        {
            try { BtcBpPreset.OnSelect(__instance, true); }
            catch (System.Exception e) { BtcLog.Error("Hook", "UISelectable.OnSelect (bp preset): " + e); }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Brix.UI.GameSelector.UISelectable), "OnUnselect")]
    internal static class BpPresetUnselectPatch
    {
        private static void Postfix(Brix.UI.GameSelector.UISelectable __instance)
        {
            try { BtcBpPreset.OnSelect(__instance, false); }
            catch (System.Exception e) { BtcLog.Error("Hook", "UISelectable.OnUnselect (bp preset): " + e); }
        }
    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyLib.HarmonyPatch(typeof(Brix.Game.Compounders.BlueprintCompounder), "LoadCompoundBlueprints")]
    internal static class BpAnchorLoadPatch
    {
        private static void Postfix(Brix.Assets.Asset_Blueprints compound, Brix.Game.Components.BlueprintCompoundComponent __result)
        {
            try { BtcBpPreset.OnCompoundLoaded(compound, __result); }
            catch (System.Exception e) { BtcLog.Error("Hook", "BlueprintCompounder.LoadCompoundBlueprints post (bp anchor): " + e); }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Brix.Game.Components.BlueprintCompoundComponent), "MoveAt")]
    internal static class BpAnchorMoveAtPatch
    {
        private static void Prefix(Brix.Game.Components.BlueprintCompoundComponent __instance, ref UnityEngine.Vector3 newPosition)
        {
            try { BtcBpPreset.BeforeMoveAt(__instance, ref newPosition); }
            catch (System.Exception e) { BtcLog.Error("Hook", "BlueprintCompoundComponent.MoveAt (bp anchor): " + e); }
        }
    }

    // putting the originals of a cancelled move back uses the original centre, never the anchor
    [HarmonyLib.HarmonyPatch(typeof(Brix.Game.Components.BlueprintCompoundComponent), "PlaceBackOriginalBlueprints")]
    internal static class BpAnchorPlaceBackPatch
    {
        private static void Prefix() { BtcBpPreset.NoShift(true); }
        private static void Finalizer() { BtcBpPreset.NoShift(false); }
    }
}

namespace BetterThanCastle.Patches
{
    // build mode: a click on an own station mark opens its preset panel instead of placing a block
    [HarmonyLib.HarmonyPatch(typeof(Brix.Input.BlueprintPlacementPicker), "PlaceBlueprint", new[] { typeof(Brix.Input.Slave) })]
    internal static class BpPresetClickPlacePatch
    {
        private static bool Prefix()
        {
            try { return BtcBpPreset.ClickPlace(); }
            catch (System.Exception e) { BtcLog.Error("Hook", "BlueprintPlacementPicker.PlaceBlueprint (bp panel): " + e.Message); return true; }
        }
    }

    // holding bp_select_all: own marks of every build task are selectable (vanilla: only the edited task's marks)
    [HarmonyLib.HarmonyPatch(typeof(Brix.UI.GameSelector.SelectableBlueprintBehaviour), "IsSelectable")]
    internal static class BpSelectAllPatch
    {
        private static readonly HarmonyLib.AccessTools.FieldRef<Brix.UI.GameSelector.SelectableBlueprintBehaviour, Blueprint> Bp =
            HarmonyLib.AccessTools.FieldRefAccess<Brix.UI.GameSelector.SelectableBlueprintBehaviour, Blueprint>("blueprint");

        private static void Postfix(Brix.UI.GameSelector.SelectableBlueprintBehaviour __instance, ref bool __result)
        {
            if (__result) return;
            try { if (BtcBpPreset.SelectAllHeld()) __result = BtcBpPreset.SelectableAcrossTasks(Bp(__instance)); }
            catch (System.Exception e) { BtcLog.Error("Hook", "SelectableBlueprintBehaviour.IsSelectable (bp select all): " + e.Message); }
        }
    }
}
