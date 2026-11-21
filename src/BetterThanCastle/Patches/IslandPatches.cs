using System;
using Brix.Input.WorldEditor;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    /// <summary>Square brush: replaces the round range test of every editor brush when the option is on (box when the brush is height-limited).</summary>
    [HarmonyPatch(typeof(BrushUtils), "IsInsideRange")]
    internal static class IslandSquareRangePatch
    {
        private static void Postfix(Vector3 position, Vector3 rangeCenter, int areaSize, bool unlimitedInY, ref bool __result)
        {
            try
            {
                if (!BtcIsland.Square) return;
                float r = areaSize / 2f;
                __result = unlimitedInY ? BrushUtils.IsInsideSquare(rangeCenter, position, r) : BrushUtils.IsInsideBox(rangeCenter, position, r);
            }
            catch (Exception e) { BtcLog.Error("Hook", "IslandSquare: " + e); }
        }
    }

    /// <summary>Sloped Island: the target height follows the distance from the stroke start (add mode only).</summary>
    [HarmonyPatch(typeof(BrushComponent), "CreateOrRemoveIsland")]
    internal static class IslandSlopePatch
    {
        private static void Prefix(ref Vector3 hitVoxel, bool removeMode)
        {
            try
            {
                if (removeMode) return;
                hitVoxel = BtcIsland.Sloped(hitVoxel);
            }
            catch (Exception e) { BtcLog.Error("Hook", "IslandSlope: " + e); }
        }
    }
}
