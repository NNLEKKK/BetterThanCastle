using System;
using Brix.External.Factories;
using Brix.Game.Factories;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // VolumePreprocessor serves every volume footprint (FreeVolume.Make -> blueprint ghost, validator, host registry).
    // Redirecting the blueprint key to the built object's key makes the blueprint 1x1 everywhere, same on host and client.
    internal static class ChopBlockHook
    {
        internal static void Run(ref Factory.AssetKey key)
        {
            try { BtcChopBlock1x1.Redirect(ref key); }
            catch (Exception ex) { BtcLog.Error("Hook", "ChopBlock1x1: " + ex); }
        }
    }

    [HarmonyPatch(typeof(VolumePreprocessor), "GetIndexedVSettings", new Type[] { typeof(Factory.AssetKey), typeof(int), typeof(int) })]
    internal static class ChopBlockVSettingsIntPatch
    {
        [HarmonyPrefix] private static void Prefix(ref Factory.AssetKey assetKey) { ChopBlockHook.Run(ref assetKey); }
    }

    [HarmonyPatch(typeof(VolumePreprocessor), "GetIndexedVSettings", new Type[] { typeof(Factory.AssetKey), typeof(Quaternion), typeof(int) })]
    internal static class ChopBlockVSettingsQuatPatch
    {
        [HarmonyPrefix] private static void Prefix(ref Factory.AssetKey assetKey) { ChopBlockHook.Run(ref assetKey); }
    }

    [HarmonyPatch(typeof(VolumePreprocessor), "GetVSettingsIndices", new Type[] { typeof(Factory.AssetKey), typeof(int), typeof(int) })]
    internal static class ChopBlockIndicesIntPatch
    {
        [HarmonyPrefix] private static void Prefix(ref Factory.AssetKey assetKey) { ChopBlockHook.Run(ref assetKey); }
    }

    [HarmonyPatch(typeof(VolumePreprocessor), "GetVSettingsIndices", new Type[] { typeof(Factory.AssetKey), typeof(Quaternion), typeof(int) })]
    internal static class ChopBlockIndicesQuatPatch
    {
        [HarmonyPrefix] private static void Prefix(ref Factory.AssetKey assetKey) { ChopBlockHook.Run(ref assetKey); }
    }
}
