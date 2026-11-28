using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Engine;
using Brix.Game.AI;
using Brix.Game.AI.Pathfinding;
using Brix.Game.Components.Volumes;
using Brix.Game.Locomotion;
using Brix.Pathfinding;
using Brix.Utils;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    // Rule mov.lookahead "Early detour" (host). Vanilla checks only the next voxel of a route: a wall, building or dug hole that
    // appeared after the route was found is noticed when the unit stands against it; then a local repath to the node 3 ahead
    // runs (100 search steps) and, if the obstacle is longer than that, fails, so the unit waits 20 ticks and searches the
    // whole route again while standing. With the rule a walking unit looks up to 8 voxels ahead once per voxel step (cheap:
    // graph links + obstacle volumes, units ignored); when a step ahead became impossible it detours at once, still walking,
    // to the route node just past the blocked part (same vanilla merge as the contact repath, up to 400 search steps). A failed
    // detour changes nothing: the vanilla contact repath still runs.
    internal static class BtcEarlyDetour
    {
        private const int Ahead = 8;
        private const float Recheck = 0.3f;
        private const int DetourLimit = 1500;
        private static int _idx = -2;
        private static bool _big;
        internal static int Checks, Found, Detoured, Failed;
        private const int PerFrame = 3;
        private static int _frame, _inFrame;

        private sealed class Seen
        {
            public int Index = -1;
            public object Path;
            public float Next;
            public XYZ Tried;
        }

        private static readonly Dictionary<int, Seen> State = new Dictionary<int, Seen>();

        private static readonly Func<IVolume, GameObject, bool> Blocks = (v, go) => v.PassThrough != null && !v.PassThrough.CanPassThrough(go);

        public static void AddRows()
        {
            BtcGameplay.Add("mov.lookahead", "mov", "Early detour", "Ранний обход", 1f, 0f, 1f, 1f, true, "A walking unit looks 8 blocks ahead on its route: if a wall, building or hole appeared there after the route was found, it turns off at once instead of walking into it and standing while the whole route is searched again.", "Идущий юнит смотрит на 8 блоков вперёд по пути: если там после постройки пути появились стена, здание или яма, он сразу сворачивает, а не упирается и не стоит, пока путь ищется заново.");
        }

        public static bool On
        {
            get
            {
                if (_idx == -2) _idx = BtcGameplay.Idx("mov.lookahead");
                float[] e = BtcGameplay.E;
                return _idx >= 0 && e != null && _idx < e.Length && e[_idx] > 0.5f;
            }
        }

        internal static int SearchLimit(int vanilla) { return _big ? DetourLimit : vanilla; }

        internal static void Tick(LocomotionWalkableCycleState state)
        {
            if (!On || !NetworkServer.active || state.Time < state.BlockedTimeout) return;
            Locomotion4 loco = state.Locomotion;
            if (loco == null || loco.presence == null || loco.PathResolver == null || BtcPhantom.IsPhantom(loco.gameObject)) return;
            DestinationIterator it = loco.PathIterator;
            if (it == null || !it.HasCurrent) return;
            Seen s;
            int id = loco.GetInstanceID();
            if (!State.TryGetValue(id, out s))
            {
                if (State.Count > 4000) State.Clear();
                s = new Seen();
                State[id] = s;
            }
            int index = it.CurrentIndex;
            object path = it.Path;
            float now = Time.time;
            if (index == s.Index && ReferenceEquals(path, s.Path) && now < s.Next) return;
            s.Index = index;
            s.Path = path;
            s.Next = now + Recheck;
            Checks++;

            WalkNode prev = it.GetNth(index);
            if (prev == null) return;
            for (int n = index + 1; n <= index + Ahead; n++)
            {
                WalkNode node = it.GetNth(n);
                if (node == null) return;
                if (IsBlocked(prev, node, loco.gameObject))
                {
                    if (node.Voxel == s.Tried) return;
                    // at most PerFrame detour searches a frame (a crowd meeting a new wall): the rest look again next frame
                    if (Time.frameCount != _frame) { _frame = Time.frameCount; _inFrame = 0; }
                    if (_inFrame >= PerFrame) { s.Index = -1; return; }
                    _inFrame++;
                    s.Tried = node.Voxel;
                    Found++;
                    WalkNode target = it.GetNth(n + 1);
                    if (target == null) return; // the destination itself is blocked: the vanilla failure handles it
                    WalkNode further = it.GetNth(n + 2);
                    if (further != null && IsBlocked(target, further, loco.gameObject)) target = further;
                    bool ok = Detour(loco, target.Voxel);
                    if (ok) Detoured++;
                    else Failed++;
#if DEV
                    if (Found <= 40) BtcLog.Info("Detour", loco.name + " blocked " + (n - index) + " ahead at " + node.Voxel + " -> " + (ok ? "detour" : "no detour"));
#endif
                    return;
                }
                prev = node;
            }
        }

        /// <summary>A plain walk step of the route that the graph or an obstacle no longer allows.</summary>
        private static bool IsBlocked(WalkNode from, WalkNode to, GameObject go)
        {
            if (to._branchType != PathBranch.BranchType.Full) return false;
            XYZ a = from.Voxel, b = to.Voxel;
            int d = Math.Abs(a.x - b.x) + Math.Abs(a.z - b.z);
            if (d != 1 || Math.Abs(a.y - b.y) > 1) return false;
            if (PathNodeRegistry.PermissionVers(a, b) == PathBranch.BranchType.Empty) return true;
            return VolumeRegistry.HasVolumesWithPropertyAt(b, VProperty.obstacle, Blocks, go);
        }

        /// <summary>DestinationResolver.RepathMoveNext with a farther destination and a bigger search.</summary>
        private static bool Detour(Locomotion4 loco, XYZ destination)
        {
            DestinationResolver res = loco.PathResolver;
            DestinationRepathSearch search = ObjectPool<DestinationRepathSearch>.Reserve().Init(loco, destination, false, true);
            List<PathBranch> branches = CollectionPool.RequestList<PathBranch>();
            Dictionary<XYZ, int> positions = CollectionPool.RequestDictionary<XYZ, int>();
            List<WalkNode> locations = CollectionPool.RequestList<WalkNode>();
            bool ok;
            _big = true;
            try { ok = res.MergePaths(search, branches, positions, locations); }
            finally
            {
                _big = false;
                ObjectPool<DestinationRepathSearch>.Release(search);
                branches.Dispose<PathBranch>();
                positions.Dispose<XYZ, int>();
                locations.Dispose<WalkNode>();
            }
            if (!ok) return false;
            loco.PathIterator.Replace(res.Result);
            if (loco.PathIterator.HasCurrent && XYZ.FromVector3(loco.trf.position) == XYZ.FromVector3(loco.PathIterator.Current.Position)) loco.PathIterator.MoveNext(1);
            return true;
        }
    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(LocomotionWalkableCycleState), "Move")]
    internal static class EarlyDetourMovePatch
    {
        private static void Prefix(LocomotionWalkableCycleState __instance)
        {
            try { BtcEarlyDetour.Tick(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "LocomotionWalkableCycleState.Move detour: " + e.Message); }
        }
    }

    /// <summary>The repath search step limit (100) is raised only for an early detour.</summary>
    [HarmonyPatch(typeof(DestinationRepathSearch), "Search")]
    internal static class EarlyDetourSearchLimitPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            MethodInfo limit = AccessTools.Method(typeof(BtcEarlyDetour), "SearchLimit");
            for (int i = 0; i < list.Count; i++)
            {
                if ((list[i].opcode != OpCodes.Ldc_I4_S && list[i].opcode != OpCodes.Ldc_I4) || list[i].operand == null || Convert.ToInt32(list[i].operand) != 100) continue;
                list.Insert(i + 1, new CodeInstruction(OpCodes.Call, limit));
                return list;
            }
            BtcLog.Error("Hook", "DestinationRepathSearch.Search: limit 100 not found, early detour keeps 100 steps");
            return list;
        }
    }
}
