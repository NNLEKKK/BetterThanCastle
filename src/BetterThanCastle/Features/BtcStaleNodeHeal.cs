using Brix.Engine;
using Brix.Pathfinding.Test;
using HarmonyLib;

namespace BetterThanCastle
{
    // After Castle Story Plus (Tricky12321, GPLv3: Pathfinding/StaleNodeHeal.cs). The route graph is rebuilt only round voxels the
    // terrain signals as changed, so a node could stay in a voxel that is solid now (seen at a quarry rim): routes led into it,
    // PopOuterOfObstacle pushed the unit out and the next route led straight back. When a unit is pushed out of a voxel that still
    // holds a (non-empty) node while the voxel is not open, the voxel is handed to the graph as dirty and rebuilt.
    // Always on (bug fix); runs wherever the unit's PopOut runs (the graph lives wherever the sim runs).
    [HarmonyPatch(typeof(PopOuterOfObstacle), "PopOut")]
    internal static class BtcStaleNodeHeal
    {
        private static void Prefix(PopOuterOfObstacle __instance)
        {
            try
            {
                TestPathfinding pf = TestPathfinding.Instance;
                if (pf == null || __instance == null || __instance.locomotion == null) return;
                XYZ cell = XYZ.FromVector3(__instance.locomotion.transform.position);
                TerrainPathNode node;
                if (!pf.TerrainNodes.TryGetValue(cell, out node) || node == null || node.IsEmpty || Voxel.IsOpen(cell)) return;
                pf.DirtyVoxels.Add(cell);
                BtcLog.Info("StaleNode", "route node at " + cell + " is inside solid ground (" + __instance.name + " pushed out); rebuilding it");
            }
            catch (System.Exception e) { BtcLog.Error("StaleNode", e.Message); }
        }
    }
}
