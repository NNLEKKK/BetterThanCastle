using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Brix.Engine;
using Brix.Game.AI;
using Brix.Game.Components.Volumes;
using Brix.Game.Locomotion;
using Brix.Legacy;
using Brix.Pathfinding;
using Brix.Pathfinding.Test;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
    // Rule mov.straight (host), after Castle Story Plus (Tricky12321, GPLv3: StraightPaths). The route graph links a voxel only
    // to its 4 sides, so units walked open ground as a staircase, often an L where a straight line was free.
    // Route: after RemoveInvalidNodes, runs of plain flat steps are pulled tight: from a kept voxel, the furthest later voxel a
    // straight line reaches replaces the voxels in between by the voxels that line passes (4-connected supercover). Only when every
    // voxel is a flat route node at the same height and surface, each step is a plain walk branch, none holds a door/obstacle or a
    // unit the route's presence test refuses, and the voxels the body brushes beside the line are flat open ground too. Climbs,
    // drops, ladders, forests, doors and the end stay as found.
    // Walk: the route still holds every voxel walked through, so the game's per-voxel checks (blockers, doors, repath) are vanilla;
    // only the aim on a pulled run differs: a point a little ahead on the line, kept inside the next route voxel.
    internal static class BtcStraightPaths
    {
        private const int MaxRun = 24;
        private const float BodyRadius = 0.3f;
        private const float Lookahead = 1f;
        private const float Margin = 0.1f;
        private const int MaxNodes = 50000;
        private static int _idx = -2;

        public static void AddRows()
        {
            BtcGameplay.Add("mov.straight", "mov", "Straight walking", "Прямая ходьба", 1f, 0f, 1f, 1f, true, "Units walk straight lines across flat open ground instead of the vanilla voxel-by-voxel staircase (often an L where a straight line was free).", "Юниты идут по прямой по ровной открытой земле, а не лесенкой по вокселям, как в ванили (часто буквой Г, где прямая была свободна).");
        }

        public static bool On
        {
            get
            {
                if (_idx == -2) _idx = BtcGameplay.Idx("mov.straight");
                float[] e = BtcGameplay.E;
                return _idx >= 0 && e != null && _idx < e.Length && e[_idx] > 0.5f;
            }
        }

        internal struct Line
        {
            public Vector3 Start;
            public Vector3 Direction;
            public float Length;
        }

        private sealed class ByReference : IEqualityComparer<WalkNode>
        {
            public bool Equals(WalkNode a, WalkNode b) { return ReferenceEquals(a, b); }
            public int GetHashCode(WalkNode node) { return RuntimeHelpers.GetHashCode(node); }
        }

        private static readonly Dictionary<WalkNode, Line> Lines = new Dictionary<WalkNode, Line>(new ByReference());
        internal static readonly List<PathBranch> Pulled = new List<PathBranch>();
        private static readonly List<Line?> PulledLines = new List<Line?>();
        private static readonly List<XYZ> Cells = new List<XYZ>();
        internal static int Routes, RoutesPulled, Steered;
        private static GameObject _walker;

        private static readonly Func<IVolume, bool> BlocksLine = volume =>
        {
            IPassThrough pass = volume.PassThrough;
            return pass == null || pass is Porte || _walker == null || !pass.CanSearchThrough(_walker);
        };

        internal static bool IsOnLine(WalkNode node) { return Lines.Count > 0 && Lines.ContainsKey(node); }

        /// <summary>Pulls the route tight into Pulled/PulledLines; false when nothing changed.</summary>
        internal static bool Pull(List<PathBranch> branches, TestTerrainLod lod, Func<XYZ, bool> presenceTest, GameObject walker)
        {
            _walker = walker;
            Pulled.Clear();
            PulledLines.Clear();
            int count = branches.Count;
            if (count < 3 || lod == null) return false;
            bool changed = false;
            Pulled.Add(branches[0]);
            PulledLines.Add(null);
            int i = 0;
            while (i < count - 1)
            {
                int best = i + 1;
                TerrainPathNode start = branches[i].Node as TerrainPathNode;
                if (start != null && !start.IsEmpty)
                {
                    XYZ from = start.RepresentativePosition;
                    float surface = start.GetSurfaceHeightDelta(0f, 0f);
                    for (int j = i + 1; j < count && j - i <= MaxRun; j++)
                    {
                        if (!IsPlainStep(branches[j], from.y, surface)) break;
                        if (j - i < 2) continue;
                        if (!IsLineClear(lod, from, branches[j].Node.RepresentativePosition, surface, presenceTest)) break;
                        best = j;
                    }
                }
                if (best >= i + 2)
                {
                    EmitLine(lod, branches, i, best);
                    changed = true;
                }
                else
                {
                    Pulled.Add(branches[i + 1]);
                    PulledLines.Add(null);
                    best = i + 1;
                }
                i = best;
            }
            return changed;
        }

        private static void EmitLine(TestTerrainLod lod, List<PathBranch> branches, int i, int j)
        {
            XYZ from = branches[i].Node.RepresentativePosition;
            XYZ to = branches[j].Node.RepresentativePosition;
            Supercover(from, to, Cells);
            Vector3 a = from.ToVector3();
            Vector3 d = Flat(to.ToVector3() - a);
            Line line = new Line { Start = a, Direction = d.normalized, Length = d.magnitude };
            for (int k = 0; k < Cells.Count; k++)
            {
                if (k == Cells.Count - 1) Pulled.Add(branches[j]);
                else Pulled.Add(new PathBranch(lod.GetPathNode(Cells[k]), PathBranch.BranchType.Full, branches[i + 1].weight));
                PulledLines.Add(line);
            }
        }

        private static bool IsPlainStep(PathBranch branch, int y, float surface)
        {
            if (branch.Type != PathBranch.BranchType.Full) return false;
            return IsFlatGround(branch.Node as TerrainPathNode, y, surface);
        }

        private static bool IsLineClear(TestTerrainLod lod, XYZ from, XYZ to, float surface, Func<XYZ, bool> presenceTest)
        {
            Supercover(from, to, Cells);
            TerrainPathNode previous = lod.GetPathNode(from) as TerrainPathNode;
            for (int k = 0; k < Cells.Count; k++)
            {
                XYZ cell = Cells[k];
                TerrainPathNode node = lod.GetPathNode(cell) as TerrainPathNode;
                if (!IsFlatGround(node, from.y, surface) || !HasPlainStep(previous, node)) return false;
                if (k < Cells.Count - 1 && (HasObstacle(cell) || (presenceTest != null && !presenceTest(cell)))) return false;
                previous = node;
            }
            // The voxels beside the line the body brushes: flat open ground, so the line neither cuts a wall corner nor runs along a drop.
            Vector3 a = from.ToVector3();
            Vector3 b = to.ToVector3();
            Vector3 d = Flat(b - a).normalized;
            Vector3 side = new Vector3(-d.z, 0f, d.x) * BodyRadius;
            return IsSideClear(lod, a + side, b + side, from.y, surface) && IsSideClear(lod, a - side, b - side, from.y, surface);
        }

        private static bool IsSideClear(TestTerrainLod lod, Vector3 a, Vector3 b, int y, float surface)
        {
            int x = Mathf.RoundToInt(a.x);
            int z = Mathf.RoundToInt(a.z);
            int endX = Mathf.RoundToInt(b.x);
            int endZ = Mathf.RoundToInt(b.z);
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            int stepX = dx > 0f ? 1 : -1;
            int stepZ = dz > 0f ? 1 : -1;
            float deltaX = Mathf.Abs(dx) > 1e-6f ? Mathf.Abs(1f / dx) : float.MaxValue;
            float deltaZ = Mathf.Abs(dz) > 1e-6f ? Mathf.Abs(1f / dz) : float.MaxValue;
            float nextX = Mathf.Abs(dx) > 1e-6f ? (x + 0.5f * stepX - a.x) / dx : float.MaxValue;
            float nextZ = Mathf.Abs(dz) > 1e-6f ? (z + 0.5f * stepZ - a.z) / dz : float.MaxValue;
            for (int guard = 0; guard < 4 * MaxRun + 8; guard++)
            {
                XYZ cell = new XYZ(x, y, z);
                if (!IsFlatGround(lod.GetPathNode(cell) as TerrainPathNode, y, surface) || HasObstacle(cell)) return false;
                if ((x == endX && z == endZ) || (nextX > 1f && nextZ > 1f)) return true;
                if (Mathf.Abs(nextX - nextZ) < 1e-4f)
                {
                    XYZ corner = new XYZ(x + stepX, y, z);
                    if (!IsFlatGround(lod.GetPathNode(corner) as TerrainPathNode, y, surface) || HasObstacle(corner)) return false;
                    z += stepZ;
                    nextZ += deltaZ;
                }
                else if (nextX < nextZ) { x += stepX; nextX += deltaX; }
                else { z += stepZ; nextZ += deltaZ; }
            }
            return false;
        }

        private static bool HasObstacle(XYZ cell) { return VolumeRegistry.HasVolumesWithPropertyAt(cell, VProperty.obstacle, BlocksLine); }

        private static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }

        private static bool IsFlatGround(TerrainPathNode node, int y, float surface)
        {
            return node != null && !node.IsEmpty && node.RepresentativePosition.y == y && Mathf.Abs(node.GetSurfaceHeightDelta(0f, 0f) - surface) < 0.01f;
        }

        private static bool HasPlainStep(TerrainPathNode from, TerrainPathNode to)
        {
            if (from == null || to == null || from.siblings == null) return false;
            PathBranch[] s = from.siblings;
            for (int k = 0; k < s.Length; k++)
                if (s[k].Node == to) return s[k].Type == PathBranch.BranchType.Full;
            return false;
        }

        /// <summary>Voxels a line between two voxel centres passes, each a side neighbour of the one before; start left out, end included.</summary>
        internal static void Supercover(XYZ from, XYZ to, List<XYZ> cells)
        {
            cells.Clear();
            int dx = to.x - from.x, dz = to.z - from.z;
            int nx = Math.Abs(dx), nz = Math.Abs(dz);
            int sx = Math.Sign(dx), sz = Math.Sign(dz);
            int x = from.x, z = from.z, ix = 0, iz = 0;
            while (ix < nx || iz < nz)
            {
                long compare = (long)(1 + 2 * ix) * nz - (long)(1 + 2 * iz) * nx;
                if (iz >= nz || (ix < nx && compare <= 0)) { x += sx; ix++; }
                else { z += sz; iz++; }
                cells.Add(new XYZ(x, from.y, z));
            }
        }

        internal static void Attach(List<WalkNode> nodes, int first)
        {
            if (Lines.Count > MaxNodes) Lines.Clear();
            for (int k = first; k < nodes.Count && k - first < PulledLines.Count; k++)
            {
                Line? line = PulledLines[k - first];
                if (line.HasValue) Lines[nodes[k]] = line.Value;
            }
        }

        internal static void Forget(WalkNode node) { if (Lines.Count > 0) Lines.Remove(node); }

        internal static void Copy(WalkNode node, WalkNode other)
        {
            Line line;
            if (Lines.Count > 0 && other != null && Lines.TryGetValue(other, out line)) Lines[node] = line;
        }

        /// <summary>The aim of a unit walking a pulled run, or false to leave it to the game.</summary>
        internal static bool Steer(LocomotionWalkableCycleState state)
        {
            if (Lines.Count == 0 || !On) return false;
            WalkNode current = state.PathIterator.CurrentActive;
            Line line;
            if (current == null || !Lines.TryGetValue(current, out line)) return false;
            Transform trf = state.trf;
            Vector3 position = trf.position;
            XYZ standing = XYZ.FromVector3(position);
            XYZ next = current.Voxel;
            if (Math.Abs(standing.x - next.x) + Math.Abs(standing.z - next.z) != 1) return false;
            float along = Vector3.Dot(Flat(position - line.Start), line.Direction);
            Vector3 aim = line.Start + line.Direction * Mathf.Clamp(along + Lookahead, 0f, line.Length);
            aim.x = Mathf.Clamp(aim.x, next.x - 0.5f + Margin, next.x + 0.5f - Margin);
            aim.z = Mathf.Clamp(aim.z, next.z - 0.5f + Margin, next.z + 0.5f - Margin);
            aim.y = next.y;
            Quaternion facing = Quaternion.LookRotation(line.Direction);
            float time = (aim - position).magnitude / state.SpeedMperS;
            float rotationTime = Mathf.Min(time, Quaternion.Angle(trf.rotation, facing) / (state.TurningTopSpeed * 360f));
            state.OverrideMatchTarget(time, rotationTime, aim, facing);
            if (++Steered == 1) BtcLog.Info("Straight", "first steer on a line");
            return true;
        }
    }
}

namespace BetterThanCastle.Patches
{
    /// <summary>Pulls the route tight while its walk nodes are made, then gives the nodes of each pulled run their line.</summary>
    [HarmonyPatch(typeof(PathUnroll), "ConvertVoxelListToPath")]
    internal static class StraightPathsConvertPatch
    {
        private static void Prefix(PathUnroll __instance, ref List<PathBranch> branches, List<WalkNode> outLocations, out int __state)
        {
            __state = -1;
            try
            {
                if (branches == null || outLocations == null || !BtcStraightPaths.On) return;
                BtcStraightPaths.Routes++;
                GameObject walker = __instance.transform != null ? __instance.transform.gameObject : null;
                if (!BtcStraightPaths.Pull(branches, __instance.terrainLod, __instance.presenceTest, walker)) return;
                if (++BtcStraightPaths.RoutesPulled == 1) BtcLog.Info("Straight", "first route pulled: " + branches.Count + " -> " + BtcStraightPaths.Pulled.Count + " voxels");
                branches = BtcStraightPaths.Pulled;
                __state = outLocations.Count;
            }
            catch (Exception e) { BtcLog.Error("Hook", "PathUnroll.ConvertVoxelListToPath straight: " + e.Message); }
        }

        private static void Postfix(List<WalkNode> outLocations, int __state)
        {
            if (__state >= 0) BtcStraightPaths.Attach(outLocations, __state);
        }
    }

    /// <summary>A walk node taken from the pool for another route loses its line; one copied (a repath keeps the rest of the route) keeps it.</summary>
    [HarmonyPatch(typeof(WalkNode), "Reset", new Type[] { typeof(PathBranch) })]
    internal static class StraightPathsResetBranchPatch
    {
        private static void Prefix(WalkNode __instance) { BtcStraightPaths.Forget(__instance); }
    }

    [HarmonyPatch(typeof(WalkNode), "Reset", new Type[] { typeof(WalkablePathNode) })]
    internal static class StraightPathsResetNodePatch
    {
        private static void Prefix(WalkNode __instance) { BtcStraightPaths.Forget(__instance); }
    }

    [HarmonyPatch(typeof(WalkNode), "Reset", new Type[] { typeof(WalkNode) })]
    internal static class StraightPathsResetCopyPatch
    {
        private static void Prefix(WalkNode __instance, WalkNode other)
        {
            BtcStraightPaths.Forget(__instance);
            BtcStraightPaths.Copy(__instance, other);
        }
    }

    /// <summary>Walking and running aim along the line on a pulled run.</summary>
    [HarmonyPatch(typeof(LocomotionWalkableCycleState), "MatchCurrentTarget")]
    internal static class StraightPathsSteerPatch
    {
        private static bool Prefix(LocomotionWalkableCycleState __instance)
        {
            try { return !BtcStraightPaths.Steer(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "MatchCurrentTarget straight: " + e.Message); return true; }
        }
    }
}
