using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Engine;
using Brix.Game.AI;
using Brix.Game.Locomotion;
using Brix.Pathfinding;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
    // ------------------------------------------------------------------ phantom bodies (BtcPhantomBody): phantoms and normal units never touch
    /// <summary>Every vanilla presence test (paths, steps, repath) ends here: a phantom and a normal unit never prevent each other's movement.</summary>
    [HarmonyPatch(typeof(VoxelPresence), "PreventsMovement")]
    internal static class PhantomBodyPreventsPatch
    {
        private static bool Prefix(VoxelPresence __instance, VoxelPresence presence, ref bool __result)
        {
            if (!BtcPhantomBody.On || presence == null) return true;
            if (!BtcPhantomBody.Ignores(__instance.gameObject, presence.gameObject)) return true;
            __result = false;
            return false;
        }
    }

    internal static class PhantomBodyUtil
    {
        internal static readonly AccessTools.FieldRef<LocomotionStateUpdater, Locomotion4> FLoco =
            AccessTools.FieldRefAccess<LocomotionStateUpdater, Locomotion4>("Locomotion");
    }

    /// <summary>The unit standing in the cell the walker enters (step blocking, waiting, transfer): an ignored pair is no blocker.</summary>
    [HarmonyPatch(typeof(LocomotionMovementState), "GetBlocker")]
    internal static class PhantomBodyBlockerPatch
    {
        private static void Postfix(LocomotionMovementState __instance, ref VoxelPresence __result)
        {
            if (__result == null || !BtcPhantomBody.On) return;
            try
            {
                Locomotion4 l = PhantomBodyUtil.FLoco(__instance);
                if (l == null || !BtcPhantomBody.Ignores(l.gameObject, __result.gameObject)) return;
                __result = BtcPhantomBody.FirstTouching(l.gameObject, l.PositionEntering);
            }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomBodyBlocker: " + e.Message); }
        }
    }

    /// <summary>Labor.Unstack (step aside when two units share a cell): an ignored pair does not count as stacked.</summary>
    [HarmonyPatch(typeof(VoxelPresence), "GetStackedPresence")]
    internal static class PhantomBodyStackPatch
    {
        private static void Postfix(VoxelPresence __instance, ref VoxelPresence __result)
        {
            if (__result == null || !BtcPhantomBody.On) return;
            try
            {
                if (!BtcPhantomBody.Ignores(__instance.gameObject, __result.gameObject)) return;
                __result = BtcPhantomBody.FirstTouching(__instance.gameObject, __instance.CurrentVoxel);
            }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomBodyStack: " + e.Message); }
        }
    }

    /// <summary>Landing of a jump on someone's head: no stomp and no flail when everyone below is an ignored pair.</summary>
    [HarmonyPatch(typeof(JumpingOff), "MakeStompedEnemiesFlail")]
    internal static class PhantomBodyStompPatch
    {
        private static bool Prefix(JumpingOff __instance)
        {
            if (!BtcPhantomBody.On) return true;
            try
            {
                Locomotion4 l = PhantomBodyUtil.FLoco(__instance);
                if (l == null) return true;
                return BtcPhantomBody.AnyTouching(l.gameObject, l.PositionOccupied - XYZ.up);
            }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomBodyStomp: " + e.Message); return true; }
        }
    }

    /// <summary>Falling/flailing onto a unit ("body surfing" = stabilise on top of it): ignored pairs are not under each other.</summary>
    [HarmonyPatch(typeof(Flailing), "IsAboveCharacter")]
    internal static class PhantomBodySurfPatch
    {
        private static bool Prefix(Flailing __instance, ref bool __result)
        {
            if (!BtcPhantomBody.On) return true;
            try
            {
                Locomotion4 l = PhantomBodyUtil.FLoco(__instance);
                if (l == null) return true;
                XYZ p = l.PositionOccupied;
                __result = BtcPhantomBody.AnyTouching(l.gameObject, p) || BtcPhantomBody.AnyTouching(l.gameObject, p - XYZ.up);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomBodySurf: " + e.Message); return true; }
        }
    }

    /// <summary>The step check of a walking unit (LocomotionMovementState.IsBlockedByDynamicObstacle, both overloads) used the vanilla
    /// obstacle rule: a ghost phantom stepping into a building or object cell was "blocked", stopped and repathed on every such step.
    /// For ghost phantoms only block-made obstacles count, like their path search (BtcPhantomGhost.PassBlocked).</summary>
    [HarmonyPatch]
    internal static class PhantomGhostStepPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodInfo m in typeof(LocomotionMovementState).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                if (m.Name == "IsBlockedByDynamicObstacle") yield return m;
        }

        private static void Postfix(LocomotionMovementState __instance, ref bool __result)
        {
            if (!__result || !BtcPhantom.Ghost || BtcPhantom.LiveCount == 0) return;
            try
            {
                Locomotion4 l = PhantomBodyUtil.FLoco(__instance);
                if (l == null || !BtcPhantomGhost.On(l.gameObject)) return;
                __result = BtcPhantomGhost.PassBlocked(l.gameObject, l.PositionEntering);
            }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomGhostStep: " + e.Message); }
        }
    }

    // ------------------------------------------------------------------ ghost phantoms walk through foliage (BtcPhantomBody.ForestEval)
    [HarmonyPatch(typeof(SearchPathRequest.BranchEvaluator), MethodType.Constructor, new[] { typeof(Func<XYZ, bool>), typeof(bool), typeof(bool), typeof(bool) })]
    internal static class PhantomForestEvalCtorPatch
    {
        private static void Postfix(SearchPathRequest.BranchEvaluator __instance, Func<XYZ, bool> presenceTest, bool canClimb, bool canBreakVoxels)
        {
            try { BtcPhantomBody.OnEvaluatorInit(__instance, presenceTest, canClimb, canBreakVoxels); }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomForestEvalCtor: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(SearchPathRequest.BranchEvaluator), "Reset")]
    internal static class PhantomForestEvalResetPatch
    {
        private static void Postfix(SearchPathRequest.BranchEvaluator __instance, Func<XYZ, bool> presenceTest, bool canClimb, bool canBreakVoxels)
        {
            try { BtcPhantomBody.OnEvaluatorInit(__instance, presenceTest, canClimb, canBreakVoxels); }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomForestEvalReset: " + e.Message); }
        }
    }

    /// <summary>The node a path builder is expanding (PushOuterPaths/GetOuterPaths of the link it extends): CanCross only sees the target
    /// node, but a forest branch must be judged by its height step.</summary>
    [HarmonyPatch]
    internal static class PhantomForestSourcePatch
    {
        private static readonly AccessTools.FieldRef<PathLink, PathBranch> FHead = AccessTools.FieldRefAccess<PathLink, PathBranch>("head");
        [ThreadStatic] internal static PathNode Src;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] types = { typeof(PathLodBuilder), typeof(PathDirectBuilder) };
            string[] names = { "PushOuterPaths", "GetOuterPaths" };
            for (int i = 0; i < types.Length; i++)
                for (int k = 0; k < names.Length; k++)
                {
                    int found = 0;
                    foreach (MethodInfo m in AccessTools.GetDeclaredMethods(types[i]))   // overloads: only the ones that take the PathLink first (Prefix __0)
                    {
                        if (m.Name != names[k]) continue;
                        ParameterInfo[] ps = m.GetParameters();
                        if (ps.Length == 0 || ps[0].ParameterType != typeof(PathLink)) continue;
                        found++;
                        yield return m;
                    }
                    if (found == 0) BtcLog.Error("Hook", "PhantomForestSource: no " + names[k] + "(PathLink, ...) on " + types[i].Name);
                }
        }

        private static void Prefix(PathLink __0)
        {
            if (BtcPhantomBody.ForestEval.Count == 0) { Src = null; return; }
            Src = __0 != null ? FHead(__0).Node : null;
        }
    }

    /// <summary>A Forest branch is accepted by a phantom evaluator like a walk branch (full node, or the presence test on a fine node), but
    /// only on the same height: the forest branch carries no climb/step down (vanilla builds it without the height check), so a forest step
    /// up or down a block would be walked into the wall (phantom stood AFK); those cells are reached through normal climb/step branches.</summary>
    [HarmonyPatch(typeof(SearchPathRequest.BranchEvaluator), "CanCross")]
    internal static class PhantomForestCrossPatch
    {
        private static bool Prefix(SearchPathRequest.BranchEvaluator __instance, PathBranch branch, ref bool __result)
        {
            if (BtcPhantomBody.ForestEval.Count == 0 || branch.Type != PathBranch.BranchType.Forest || !BtcPhantomBody.ForestEval.Contains(__instance)) return true;
            PathNode n = branch.Node, s = PhantomForestSourcePatch.Src;
            if (n.Lod == 0 && s != null && s.Lod == 0 && s.RepresentativePosition.y != n.RepresentativePosition.y) return true;   // vanilla: rejected (Forest < Climbing)
            __result = n.IsFull || (n.Lod == 0 && __instance.PresenceTest(n.RepresentativePosition));
            return false;
        }
    }

    /// <summary>Idle ghost phantoms stay in the foliage (vanilla steps idle units out of the forest).</summary>
    [HarmonyPatch(typeof(Labor), "UnstackFromNature")]
    internal static class PhantomForestUnstackPatch
    {
        private static bool Prefix(Labor __instance)
        {
            return !BtcPhantom.Ghost || BtcPhantom.LiveCount == 0 || !BtcPhantomGhost.On(__instance.gameObject);
        }
    }
}
