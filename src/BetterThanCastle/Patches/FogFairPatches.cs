using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.Game;
using Brix.Game.Components;
using Brix.Game.Components.Volumes;
using Brix.Legacy;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // A4: VUtil.IsObstructed (both overloads) = vanilla logic + hidden enemy traps never obstruct (BtcFogFair.IgnoredObstacle).
    [HarmonyPatch(typeof(VUtil), "IsObstructed", new Type[] { typeof(XYZ), typeof(VPropertyMask), typeof(Faction), typeof(IVolume) })]
    internal static class FogFairObstructedPatch
    {
        private static bool Prefix(XYZ i, VPropertyMask obstructions, Faction faction, IVolume excludedVolume, ref bool __result)
        {
            if (!BtcFogFair.Leak) return true;
            try
            {
                using (IVoxel voxel = Voxel.GetVoxel(i))
                {
                    if ((voxel.IsNull || voxel.IsCrust || !voxel.IsTerrain) && !TreeRegistry.Instance.IsNearATree(i))
                    {
                        bool any = false;
                        foreach (IVolume v in VolumeRegistry.VolumesWithPropertyAtExcluding(i, obstructions, excludedVolume))
                            if ((!(v is BlueprintVolume) || v.Faction == faction) && !BtcFogFair.IgnoredObstacle(v, faction)) { any = true; break; }
                        __result = any;
                    }
                    else __result = true;
                }
                return false;
            }
            catch (Exception e) { BtcLog.Error("FogFair", "IsObstructed: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(VUtil), "IsObstructed", new Type[] { typeof(XYZ), typeof(VPropertyMask), typeof(Faction), typeof(List<IVolume>) })]
    internal static class FogFairObstructedListPatch
    {
        private static bool Prefix(XYZ i, VPropertyMask obstructions, Faction faction, List<IVolume> excludedVolumes, ref bool __result)
        {
            if (!BtcFogFair.Leak) return true;
            try
            {
                using (IVoxel voxel = Voxel.GetVoxel(i))
                {
                    if ((voxel.IsNull || voxel.IsCrust || !voxel.IsTerrain) && !TreeRegistry.Instance.IsNearATree(i))
                    {
                        bool any = false;
                        foreach (IVolume v in VolumeRegistry.VolumesWithPropertyAt(i, obstructions))
                            if ((excludedVolumes == null || !excludedVolumes.Contains(v)) && (!(v is BlueprintVolume) || v.Faction == faction) && !BtcFogFair.IgnoredObstacle(v, faction)) { any = true; break; }
                        __result = any;
                    }
                    else __result = true;
                }
                return false;
            }
            catch (Exception e) { BtcLog.Error("FogFair", "IsObstructed(list): " + e.Message); return true; }
        }
    }

    // A7: hover / click on an unseen object does nothing.
    [HarmonyPatch]
    internal static class FogFairHoverPatch
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            Type t = AccessTools.TypeByName("Brix.Input.ObjectActionPicker");
            if (t == null) { BtcLog.Error("Hook", "ObjectActionPicker not found"); yield break; }
            yield return AccessTools.Method(t, "OnEnterGameObject");
            yield return AccessTools.Method(t, "OnFirstActionGameObject");
        }

        private static bool Prefix(object[] __args)
        {
            if (!BtcFogFair.Fog || __args == null || __args.Length == 0) return true;
            try { return !BtcFogFair.Hidden(Brix.Input.PickingUtility.FindSignificantParent(__args[0] as GameObject)); }
            catch (Exception) { return true; }
        }
    }

    // A7: no pie menu on an unseen object.
    [HarmonyPatch]
    internal static class FogFairPieTargetPatch
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            Type t = AccessTools.TypeByName("Brix.Input.ObjectPMPicker");
            return t == null ? null : AccessTools.Method(t, "GetPieMenuTarget");
        }

        private static void Postfix(ref object __result)
        {
            if (!BtcFogFair.Fog) return;
            try { if (BtcFogFair.Hidden(__result as GameObject)) __result = null; }
            catch (Exception) { }
        }
    }

    // A7: no right-click order on an unseen object.
    [HarmonyPatch(typeof(Brix.Input.PickerOrderGiver), "DirectOrder", new Type[] { typeof(List<GameObject>), typeof(GameObject) })]
    internal static class FogFairDirectOrderPatch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(GameObject target)
        {
            if (!BtcFogFair.Fog) return true;
            try { return !BtcFogFair.Hidden(target); }
            catch (Exception) { return true; }
        }
    }

    // Alt scan: holding "show overheads" does not reveal objects outside vision.
    [HarmonyPatch(typeof(OverheadDisplayDriver), "ShowOverHeadsButtonDown")]
    internal static class FogFairOverheadsPatch
    {
        private static bool Prefix(OverheadDisplayDriver __instance)
        {
            if (!BtcFogFair.Fog) return true;
            try { return !(__instance.vision != null && !__instance.vision.IsVisibleForMe()); }
            catch (Exception) { return true; }
        }
    }

    // Hover on an unseen object (enemy blueprints included) does nothing at all: no tooltip, no highlight, as if nothing is there.
    [HarmonyPatch(typeof(Brix.UI.GameSelector.UISelectable), "OnHover")]
    internal static class FogFairSelectableHoverPatch
    {
        private static bool Prefix(Brix.UI.GameSelector.UISelectable __instance)
        {
            if (!BtcFogFair.Fog) return true;
            try { return !BtcFogFair.Hidden(__instance.gameObject); }
            catch (Exception) { return true; }
        }
    }

    // Placement mode: the picking ray passes through enemy blueprints (they neither hover nor get in the way), so they cannot block placing.
    [HarmonyPatch(typeof(Brix.Input.VoxelRaycastHit), "PlaceAtRaycast")]
    internal static class FogFairRaycastThroughPatch
    {
        private static readonly System.Reflection.FieldInfo Target = AccessTools.Field(typeof(Brix.Input.VoxelRaycastHit), "_target");

        private static void Postfix(Brix.Input.VoxelRaycastHit __instance, Ray ray, float distance, Brix.Input.LayerBundle layers, bool useDefaultPlane, ref bool __result)
        {
            if (useDefaultPlane || Target == null || !BtcFogFair.Fog) return;
            try
            {
                if (!BtcFogFair.IsForeignPlan(__instance.HitGameObject)) return;
                float d = ((RaycastHit)Target.GetValue(__instance)).distance + 0.05f;
                if (d >= distance) return;
                __result = __instance.PlaceAtRaycast(new Ray(ray.GetPoint(d), ray.direction), distance - d, layers, false, true);
            }
            catch (Exception e) { BtcLog.Error("FogFair", "raycast through: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Brix.UI.GameSelector.UISelectable), "IsHoverable")]
    internal static class FogFairSelectableIsHoverablePatch
    {
        private static void Postfix(Brix.UI.GameSelector.UISelectable __instance, ref bool __result)
        {
            if (!__result || !BtcFogFair.Fog) return;
            try { if (BtcFogFair.Hidden(__instance.gameObject)) __result = false; }
            catch (Exception) { }
        }
    }
}
