#if DEV
using System;
using Brix.Engine;
using Brix.Game.Batchers;
using HarmonyLib;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV: replay deblocs the client processes vs drops (block id mismatch -> NullBlockData), split by seeking/preprocess;
    /// the first drops are logged with the voxel the client has there. Summary printed by worldsig.</summary>
    [HarmonyPatch(typeof(VoxelRequestBatcher), "ProcessDebloc")]
    internal static class BtcDevDeblocProbe
    {
        private static int _ok, _drop, _okFast, _dropFast, _logged;

        private static void Prefix(XYZ position, int blockID)
        {
            try
            {
                if (!BtcReplay.Running) return;
                bool fast = BtcReplaySeek.Preprocessing || BtcReplay.Seeking;
                IVoxel v = Voxel.GetVoxelDataWithId(position, blockID);
                bool drop = v is NullBlockData;
                if (drop) { if (fast) _dropFast++; else _drop++; } else { if (fast) _okFast++; else _ok++; }
                if (_logged < 80)
                {
                    _logged++;
                    VoxelHandle h = Voxel.Engine.GetVoxelHandle(position);
                    BtcLog.Info("DeblocProbe", (drop ? "drop " : "ok ") + position + " id " + blockID + " client type " + Voxel.Engine.GetVoxelType(h) + " vol " + Voxel.Engine.GetVoxelVolume(h)
                        + " nonEmpty " + Voxel.Engine.IsVoxelNonEmpty(h) + (fast ? " fast" : "") + " t=" + BtcReplay.PlayClock.ToString("0.0"));
                }
                v.Dispose();
            }
            catch (Exception e) { BtcLog.Error("Hook", "DeblocProbe: " + e.Message); }
        }

        private static System.Reflection.FieldInfo _p2i;

        private static void Postfix(XYZ position)
        {
            try
            {
                if (!BtcReplay.Running || _logged >= 80) return;
                object eng = Voxel.Engine;
                if (_p2i == null) _p2i = eng.GetType().GetField("p2i", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                System.Collections.ICollection pend = _p2i != null ? _p2i.GetValue(eng) as System.Collections.ICollection : null;
                BtcLog.Info("DeblocProbe", "  after " + position + " pending " + (pend != null ? pend.Count : -1) + " layer " + BtcTerrainLayers.Has(BtcTerrainLayers.Snap(), position)
                    + " batcher enabled " + (VoxelRequestBatcher.Instance != null && VoxelRequestBatcher.Instance.enabled));
            }
            catch (Exception e) { BtcLog.Error("Hook", "DeblocProbe post: " + e.Message); }
        }

        internal static string Summary()
        {
            return "deblocs normal ok " + _ok + " drop " + _drop + ", fast ok " + _okFast + " drop " + _dropFast;
        }
    }
}
#endif
