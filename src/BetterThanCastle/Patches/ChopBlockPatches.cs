using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Game.Components.Volumes;
using Brix.Game.Factories;
using Brix.Utils;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // VolumePreprocessor serves every volume footprint (FreeVolume.Make -> blueprint ghost, validator, host registry).
    // For the chopping block blueprint the result is cut down to the built block's 1x1 cells; the blueprint keeps its
    // own vanilla properties on them (so it stays a passable marker that does not carve terrain).
    internal static class ChopBlockHook
    {
        internal static void Settings(Factory.AssetKey key, int rot, int config, ref Dictionary<XYZ, VPropertyMask> result)
        {
            try { if (BtcChopBlock1x1.Applies(key)) result = BtcChopBlock1x1.FilterSettings(result, rot, config); }
            catch (Exception ex) { BtcLog.Error("Hook", "ChopBlock1x1: " + ex); }
        }

        internal static void Indices(Factory.AssetKey key, int rot, int config, ref List<XYZ>[] result)
        {
            try { if (BtcChopBlock1x1.Applies(key)) result = BtcChopBlock1x1.FilterIndices(result, rot, config); }
            catch (Exception ex) { BtcLog.Error("Hook", "ChopBlock1x1: " + ex); }
        }
    }

    [HarmonyPatch(typeof(VolumePreprocessor), "GetIndexedVSettings", new Type[] { typeof(Factory.AssetKey), typeof(int), typeof(int) })]
    internal static class ChopBlockVSettingsIntPatch
    {
        [HarmonyPostfix] private static void Postfix(Factory.AssetKey assetKey, int rotationIndex, int configId, ref Dictionary<XYZ, VPropertyMask> __result) { ChopBlockHook.Settings(assetKey, rotationIndex, configId, ref __result); }
    }

    [HarmonyPatch(typeof(VolumePreprocessor), "GetIndexedVSettings", new Type[] { typeof(Factory.AssetKey), typeof(Quaternion), typeof(int) })]
    internal static class ChopBlockVSettingsQuatPatch
    {
        [HarmonyPostfix] private static void Postfix(Factory.AssetKey assetKey, Quaternion q, int configId, ref Dictionary<XYZ, VPropertyMask> __result) { ChopBlockHook.Settings(assetKey, PreprocessedRotations.RotationToDiscreteIndex(q), configId, ref __result); }
    }

    [HarmonyPatch(typeof(VolumePreprocessor), "GetVSettingsIndices", new Type[] { typeof(Factory.AssetKey), typeof(int), typeof(int) })]
    internal static class ChopBlockIndicesIntPatch
    {
        [HarmonyPostfix] private static void Postfix(Factory.AssetKey assetKey, int rotationIndex, int configId, ref List<XYZ>[] __result) { ChopBlockHook.Indices(assetKey, rotationIndex, configId, ref __result); }
    }

    [HarmonyPatch(typeof(VolumePreprocessor), "GetVSettingsIndices", new Type[] { typeof(Factory.AssetKey), typeof(Quaternion), typeof(int) })]
    internal static class ChopBlockIndicesQuatPatch
    {
        [HarmonyPostfix] private static void Postfix(Factory.AssetKey assetKey, Quaternion q, int configId, ref List<XYZ>[] __result) { ChopBlockHook.Indices(assetKey, PreprocessedRotations.RotationToDiscreteIndex(q), configId, ref __result); }
    }
}
