using System;
using System.Collections.Generic;
using Brix.Game.AI;
using Brix.Pathfinding;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Legacy only added diagnostic logging (qol_blocked.log) to RemoveInvalidNodes; the per-node "cut" lines are not ported.
    [HarmonyPatch(typeof(PathUnroll), "RemoveInvalidNodes")]
    internal static class PathUnrollRemoveInvalidNodesPatch
    {
        private static void Prefix(PathUnroll __instance, List<PathBranch> branches)
        {
            if (!BtcQol.LogOn) return; // avoid per-path string building when logging is off
            try
            {
                int emptyAt = -1;
                for (int i = 0; i < branches.Count; i++)
                {
                    if (branches[i].Type.CompareTo(PathBranch.BranchType.Empty) <= 0) { emptyAt = i; break; }
                }
                BtcQol.LogNote("PathUnroll " + __instance.transform.name + " n=" + branches.Count + " emptyAt=" + emptyAt);
            }
            catch (Exception e) { BtcLog.Error("Hook", "PathUnroll.RemoveInvalidNodes: " + e); }
        }
    }

    // Game: the first path unroll of a match looked up its two pathfinding singletons with FindObjectOfType (a whole-scene walk each,
    // 72 ms in one frame in a 60-worker test). Saver: fill the same two statics from TestPathfinding.Instance (set in its Awake) and the
    // component of the field's type on that object or its parent/children; falls back to the game's own search if not found. Same objects,
    // no change to paths, so no BetterConnection gate.
    [HarmonyPatch(typeof(PathUnroll), "PreProcessNodes")]
    internal static class PathUnrollSingletonsPatch
    {
        private static System.Reflection.FieldInfo _pf, _tp;
        private static bool _logged;

        static bool Prepare() { return BtcFrameSave.On; }

        private static void Prefix()
        {
            try
            {
                if (_tp == null) { _tp = HarmonyLib.AccessTools.Field(typeof(PathUnroll), "TestPathfinding"); _pf = HarmonyLib.AccessTools.Field(typeof(PathUnroll), "Pathfinding"); }
                Brix.Pathfinding.Test.TestPathfinding t = Brix.Pathfinding.Test.TestPathfinding.Instance;
                if (t == null || _tp == null || _pf == null) return;
                UnityEngine.Object cur = _tp.GetValue(null) as UnityEngine.Object;
                if (cur == null) _tp.SetValue(null, t);
                UnityEngine.Object pf = _pf.GetValue(null) as UnityEngine.Object;
                if (pf != null) return;
                UnityEngine.Component c = t.GetComponent(_pf.FieldType) ?? t.GetComponentInParent(_pf.FieldType) ?? t.GetComponentInChildren(_pf.FieldType, true);
                if (c != null) _pf.SetValue(null, c);
                if (!_logged) { _logged = true; BtcLog.Info("FrameSave", "PathUnroll singletons: " + (c != null ? "found on TestPathfinding" : "not near TestPathfinding, game search")); }
            }
            catch (Exception e) { BtcLog.Error("FrameSave", "PathUnroll singletons: " + e.Message); }
        }
    }
}
