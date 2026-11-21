using Brix.Game;
using Brix.Game.AI.Pathfinding;
using Brix.Pathfinding.Test;
using System;
using System.Linq;
using Brix.Engine;
using Brix.Game.AI;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Legacy: LogWhy/LogBlocked at start of PathBlockedAt.
    [HarmonyPatch(typeof(Locomotion4), "PathBlockedAt")]
    internal static class Locomotion4PathBlockedAtPatch
    {
        private static void Prefix(Locomotion4 __instance)
        {
            try
            {
                BtcQol.LogWhy(__instance);
                BtcQol.LogBlocked(__instance.name, __instance.transform.position);
            }
            catch (Exception e) { BtcLog.Error("Hook", "Locomotion4.PathBlockedAt: " + e); }
        }
    }

    // Legacy: heal a stale (emptied) current node, and let allies pass, before the vanilla repath.
    [HarmonyPatch(typeof(Locomotion4), "RepathMoveNext", new Type[0])]
    internal static class Locomotion4RepathMoveNextPatch
    {
        private static bool Prefix(Locomotion4 __instance, ref bool __result)
        {
            try
            {
                Locomotion4 self = __instance;
                if (self.PathIterator.HasCurrent)
                {
                    WalkNode walkNode = self.PathIterator.Current;
                    WalkablePathNode pathNode = walkNode.PathNode;
                    if (pathNode != null && pathNode.IsEmpty && !walkNode.IsLadder() && !walkNode.MustClimbUp()
                        && !walkNode.MustStepDown() && !walkNode.IsBlockBreaking() && !walkNode.IsForest())
                    {
                        WalkablePathNode fresh = PathNodeRegistry.WalkableAt(XYZ.FromVector3(walkNode.Position));
                        if (fresh != null && !fresh.IsEmpty)
                        {
                            bool hasOrientation = walkNode.HasSpecificOrientation;
                            Quaternion orientation = walkNode.Orientation;
                            walkNode.Reset(fresh);
                            if (hasOrientation) walkNode.Orientation = orientation;
                            BtcQol.LogNote("healed stale node " + self.name + " at " + walkNode.Position);
                            __result = true;
                            return false;
                        }
                    }
                }
                if (BtcQol.AlliesPass)
                {
                    XYZ entering = self.PositionEntering;
                    if (entering != XYZ.zero)
                    {
                        VoxelPresence other = VoxelPresence.GetPresenceAt(entering).FirstOrDefault();
                        if (other != null && other != self.presence && Affiliation.IsAllied(self.faction, other.faction))
                        {
                            __result = true;
                            return false;
                        }
                    }
                }
            }
            catch (Exception e) { BtcLog.Error("Hook", "Locomotion4.RepathMoveNext: " + e); }
            return true;
        }
    }
}

namespace BetterThanCastle.Patches
{
    // Local visual optimization (config Performance/AnimatorCulling + BC ON): off-screen unit animators use CullUpdateTransforms.
    [HarmonyPatch(typeof(Locomotion4), "InitAnimator")]
    internal static class Locomotion4InitAnimatorCullPatch
    {
        private static void Postfix(Locomotion4 __instance)
        {
            try { BtcAnimCull.Apply(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "Locomotion4.InitAnimator cull: " + e.Message); }
        }
    }
}
