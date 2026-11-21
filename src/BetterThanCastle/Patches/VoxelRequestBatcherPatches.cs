using System;
using System.Collections;
using System.Reflection;
using Brix.Engine;
using Brix.Game.Batchers;
using HarmonyLib;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay playback only: a terrain debloc is applied by the client only when the voxel's block id matches the server's
    /// (TerrainBlockData.DeBlockClient). Removals stay pending in the voxel engine until ApplyChanges at the end of the batcher's
    /// FixedUpdate, so when several deblocs of one dig arrive in the same step (fast seek, instant seek preprocess) the cell below
    /// still looks internal, its id differs and the debloc is silently dropped: fast-fed worlds dug less than normal viewing.
    /// Pending changes are applied before each debloc while a replay plays (no effect on live games or the wire).
    /// </summary>
    [HarmonyPatch(typeof(VoxelRequestBatcher), "ProcessDebloc")]
    internal static class VoxelRequestBatcherDeblocOrderPatch
    {
        private static Type _engType;
        private static FieldInfo _pending;

        private static void Prefix()
        {
            try
            {
                if (!BtcReplay.Running) return;
                object eng = Voxel.Engine;
                if (eng == null) return;
                if (eng.GetType() != _engType)
                {
                    _engType = eng.GetType();
                    _pending = _engType.GetField("p2i", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                ICollection p = _pending != null ? _pending.GetValue(eng) as ICollection : null;
                if (p == null || p.Count == 0) return;
                Voxel.Engine.ApplyChanges(false);
                Voxel.Engine.BeginChanges();
            }
            catch (Exception e) { BtcLog.Error("Hook", "VoxelRequestBatcherDeblocOrder: " + e.Message); }
        }
    }
}
