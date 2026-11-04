using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Engine;
using Brix.Game.AI;
using Brix.Game.AI.Nodes;
using Brix.Game.Components.Volumes;
using HarmonyLib;
using Motus.Behavior;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Group jump-off: vanilla FindBestObstacle is a BFS from the unit's voxel that cannot pass through other characters and
    // stops once it reaches the target, so a crowd finds the same edge column and lands in one block whatever target each unit
    // got. While mov.jumpLine is on (host side, where orders execute) every jump-off landing column has one owner:
    // - the vanilla column if it is free, else the nearest free column along the same edge (+-1, +-2 ... across the jump
    //   direction, checked with vanilla FindObstacleInColumn, never a column without ground below);
    // - a column frees as soon as its owner has dropped below the edge (or after a timeout), and nobody jumps onto a unit
    //   standing on the landing block;
    // - if every column nearby is busy, the unit waits and takes the first column that frees (re-checked every tick).
    // Crowded units whose own search fails (the BFS cannot pass through the others) reuse an edge a neighbour found.
    // Applies to every jump-off search (orders and jumps that navigation decides on its own).
    internal static class BtcJumpOffReserve
    {
        private const float Hold = 1.6f;      // owner timeout if it never drops (walk to edge + jump)
        private const int MaxSide = 20;
        private const float MaxWait = 6f;
        private struct Res { public int Owner; public Labor Who; public int EdgeY; public float Until; }
        private struct Recent { public NavigationNodes.ObstacleSearchResult R; public float T; }
        private static readonly Dictionary<XYZ, Res> ByCell = new Dictionary<XYZ, Res>();
        private static readonly Dictionary<int, XYZ> ByLabor = new Dictionary<int, XYZ>();
        private static readonly List<Recent> Recents = new List<Recent>();
        private static readonly object[] Args = new object[4];
        private static MethodInfo _column;

        private static bool Free(XYZ prospect, int self, float now)
        {
            if (VolumeRegistry.HasVolumesWithPropertyAt(prospect + XYZ.up, VProperty.character)) return false;
            Res r;
            if (!ByCell.TryGetValue(prospect, out r) || r.Owner == self || r.Until <= now) return true;
            if (r.Who == null) return true;
            // the owner has jumped and is clearly below: two blocks under the edge it jumped from
            return r.Who.presence.CurrentVoxel.y <= r.EdgeY - 2;
        }

        private static void Own(NavigationNodes.ObstacleSearchResult pick, Labor labor, int id, float now)
        {
            XYZ old;
            Res o;
            if (ByLabor.TryGetValue(id, out old) && !(old == pick.Prospect) && ByCell.TryGetValue(old, out o) && o.Owner == id) ByCell.Remove(old);
            ByLabor[id] = pick.Prospect;
            ByCell[pick.Prospect] = new Res { Owner = id, Who = labor, EdgeY = pick.Direction.y, Until = now + Hold };
            Recents.Add(new Recent { R = pick, T = now });
            if (ByCell.Count > 256 || Recents.Count > 64) Prune(now);
        }

        private static void Prune(float now)
        {
            List<XYZ> dead = new List<XYZ>();
            foreach (KeyValuePair<XYZ, Res> kv in ByCell) if (kv.Value.Until + 5f < now) dead.Add(kv.Key);
            for (int i = 0; i < dead.Count; i++) ByCell.Remove(dead[i]);
            if (ByLabor.Count > 256) ByLabor.Clear();
            for (int i = Recents.Count - 1; i >= 0; i--) if (Recents[i].T + 6f < now) Recents.RemoveAt(i);
            if (Recents.Count > 64) Recents.RemoveRange(0, Recents.Count - 64);
        }

        private static bool FromNeighbour(Labor labor, out NavigationNodes.ObstacleSearchResult b)
        {
            b = default(NavigationNodes.ObstacleSearchResult);
            float now = Time.time;
            XYZ me = labor.presence.CurrentVoxel;
            int bestD = int.MaxValue;
            for (int i = Recents.Count - 1; i >= 0; i--)
            {
                if (Recents[i].T + 6f < now) continue;
                XYZ d = Recents[i].R.Direction;
                int dist = Math.Abs(d.x - me.x) + Math.Abs(d.z - me.z) + Math.Abs(d.y - me.y) * 2;
                if (dist <= 10 && dist < bestD) { bestD = dist; b = Recents[i].R; }
            }
            return bestD != int.MaxValue;
        }

        private static bool Standable(XYZ p)
        {
            return Voxel.IsEmpty(p) && Voxel.IsEmpty(p + XYZ.up) && Voxel.IsNonEmpty(p - XYZ.up);
        }

        // Vanilla result first, then jump-off columns along its edge (nearest first) that land on ground.
        private static List<NavigationNodes.ObstacleSearchResult> Collect(NavigationNodes.ObstacleSearchResult b, Labor labor, NavigationNodes.FindObstacleMode mode)
        {
            List<NavigationNodes.ObstacleSearchResult> list = new List<NavigationNodes.ObstacleSearchResult>();
            list.Add(b);
            if (_column == null) _column = AccessTools.Method(typeof(NavigationNodes), "FindObstacleInColumn");
            int dx = Math.Sign(b.Prospect.x - b.Direction.x), dz = Math.Sign(b.Prospect.z - b.Direction.z);
            if (dx != 0 && dz != 0) dz = 0;
            if (dx == 0 && dz == 0) return list;
            XYZ dir = new XYZ(dx, 0, dz);
            for (int k = 1; k <= MaxSide * 2; k++)
            {
                int off = (k + 1) / 2 * ((k & 1) == 1 ? 1 : -1);
                XYZ basePos = b.Direction + new XYZ(dz * off, 0, dx * off);
                for (int dy = 0; dy < 3; dy++)
                {
                    XYZ from = basePos + new XYZ(0, dy == 0 ? 0 : (dy == 1 ? 1 : -1), 0);
                    if (!Standable(from)) continue;
                    Args[0] = from + dir; Args[1] = labor; Args[2] = mode; Args[3] = from;
                    NavigationNodes.ObstacleSearchResult r = (NavigationNodes.ObstacleSearchResult)_column.Invoke(null, Args);
                    if (r.SearchMode != NavigationNodes.FindObstacleMode.JumpOff || !Voxel.IsNonEmpty(r.Prospect)) continue;
                    list.Add(r);
                    break;
                }
            }
            Args[1] = null;
            return list;
        }

        internal static IEnumerator<ComputationProgress<NavigationNodes.ObstacleSearchResult>> Wrap(
            IEnumerator<ComputationProgress<NavigationNodes.ObstacleSearchResult>> e, Labor labor, NavigationNodes.FindObstacleMode mode)
        {
            while (e.MoveNext())
            {
                ComputationProgress<NavigationNodes.ObstacleSearchResult> cur = e.Current;
                ComputationProgress<NavigationNodes.ObstacleSearchResult>.Result res = cur as ComputationProgress<NavigationNodes.ObstacleSearchResult>.Result;
                bool fromNb = false;
                if (res == null && labor != null && cur is ComputationProgress<NavigationNodes.ObstacleSearchResult>.Failed)
                {
                    NavigationNodes.ObstacleSearchResult nb;
                    try { fromNb = FromNeighbour(labor, out nb); }
                    catch (Exception ex) { nb = default(NavigationNodes.ObstacleSearchResult); BtcLog.Error("Hook", "JumpOff FromNeighbour: " + ex); }
                    if (fromNb) cur = res = new ComputationProgress<NavigationNodes.ObstacleSearchResult>.Result(nb);
                }
                if (res != null && res.Value.SearchMode == NavigationNodes.FindObstacleMode.JumpOff && labor != null)
                {
                    int id = labor.GetInstanceID();
                    NavigationNodes.ObstacleSearchResult pick = res.Value;
                    float t0 = Time.time;
                    string how = fromNb ? "neighbour" : "vanilla";
                    List<NavigationNodes.ObstacleSearchResult> cands = null;
                    bool got = false;
                    while (true)
                    {
                        float now = Time.time;
                        try
                        {
                            // a neighbour's column is owned by the neighbour: always look along the edge
                            if (!fromNb && Free(pick.Prospect, id, now)) got = true;
                            else
                            {
                                if (cands == null) cands = Collect(res.Value, labor, mode);
                                for (int i = fromNb ? 0 : 1; i < cands.Count; i++)
                                    if (Free(cands[i].Prospect, id, now)) { pick = cands[i]; got = true; if (i > 0) how = (fromNb ? "neighbour " : string.Empty) + "edge"; break; }
                            }
                        }
                        catch (Exception ex) { BtcLog.Error("Hook", "JumpOff pick: " + ex); got = true; }
                        if (got || now - t0 > MaxWait) break;
                        yield return ComputationProgress<NavigationNodes.ObstacleSearchResult>.Wait();
                    }
                    Own(pick, labor, id, Time.time);
                    float waited = Time.time - t0;
                    BtcLog.Info("JumpLine", "jump-off " + id + " from " + pick.Direction + " to " + pick.Prospect + " " + how + (waited > 0.05f ? " waited " + waited.ToString("0.00") + "s" : string.Empty) + (got ? string.Empty : " TIMEOUT"));
                    if (!pick.Equals(res.Value)) cur = new ComputationProgress<NavigationNodes.ObstacleSearchResult>.Result(pick);
                }
                yield return cur;
                if (res != null) yield break;
            }
        }
    }

    [HarmonyPatch(typeof(NavigationNodes), "FindBestObstacle")]
    internal static class NavigationNodesFindBestObstaclePatch
    {
        private static void Postfix(Labor labor, NavigationNodes.FindObstacleMode _mode,
            ref IEnumerator<ComputationProgress<NavigationNodes.ObstacleSearchResult>> __result)
        {
            try
            {
                if (labor == null || __result == null || !BtcJumpLine.On) return;
                // Auto mode (None) is resolved inside vanilla; the edge search only needs the jump-off flag then.
                NavigationNodes.FindObstacleMode m = _mode == NavigationNodes.FindObstacleMode.None ? NavigationNodes.FindObstacleMode.JumpOff : _mode;
                if ((m & NavigationNodes.FindObstacleMode.JumpOff) == 0) return;
                __result = BtcJumpOffReserve.Wrap(__result, labor, m);
            }
            catch (Exception ex) { BtcLog.Error("Hook", "JumpOff FindBestObstacle: " + ex); }
        }
    }
}
