using System;
using Brix.Engine;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Micro-duel arena: a 100 x 20 flat plateau raised above an existing map (columns rewritten through VoxelEngine, same API as
    /// BtcReplayAnchor.ApplyTerrain), walled all around. Zones run along +X: safe A 15 | neutral A 20 | mid 30 | neutral B 20 | safe B 15.
    /// Zone colours = terrain types. Side A is the human (west), side B the bot (east).
    /// </summary>
    internal static partial class BtcDuelMap
    {

        private static VoxelTerrainType TypeOf(BtcDuelZone z)
        {
            switch (z)
            {
                case BtcDuelZone.SafeA: return VoxelTerrainType.BlueCrystal;
                case BtcDuelZone.SafeB: return VoxelTerrainType.OrangeCrystal;
                case BtcDuelZone.Mid: return VoxelTerrainType.Stone;
                case BtcDuelZone.NeutralA:
                case BtcDuelZone.NeutralB: return VoxelTerrainType.Sand;
                default: return VoxelTerrainType.Earth;
            }
        }


        /// <summary>Rewrites the arena columns around the anchor. Returns false (and logs) if the voxel engine is not ready.</summary>
        internal static bool Build(Vector3 anchor)
        {
            try
            {
                if (!VoxelEngine.Ready) { BtcLog.Warn("Duel", "voxel engine not ready"); return false; }
                bool rebuild = Built;
                if (!rebuild) { _cx = anchor.x; _cz = anchor.z; _pX0 = _pZ0 = _pLen = _pWid = -1; }
                X0 = Mathf.RoundToInt(_cx) - Len / 2;
                Z0 = Mathf.RoundToInt(_cz) - Wid / 2;
                int maxTop = 0, bad = 0;
                int[] oldTop = new int[(Len + 2) * (Wid + 2)];
                for (int x = X0 - 1; x <= X0 + Len; x++)
                    for (int z = Z0 - 1; z <= Z0 + Wid; z++)
                    {
                        if (VoxelEngine.IsEmptyColumn(x, z)) { bad++; continue; }
                        int t = VoxelEngine.GetColumnTop(x, z);
                        if (rebuild && x >= _pX0 - 1 && x <= _pX0 + _pLen && z >= _pZ0 - 1 && z <= _pZ0 + _pWid) t = 0;   // our own old plateau/walls do not count
                        oldTop[(x - X0 + 1) * (Wid + 2) + (z - Z0 + 1)] = t;
                        if (t > maxTop) maxTop = t;
                    }
                Top = Mathf.Min(maxTop + 12, 240);
                if (rebuild) Top = Mathf.Max(Top, _pTop);
                BtcLog.Info("Duel", "arena X0=" + X0 + " Z0=" + Z0 + " maxTop=" + maxTop + " Top=" + Top + " emptyCols=" + bad);
                VoxelEngine.BeginChanges();
                for (int x = X0 - 1; x <= X0 + Len; x++)
                    for (int z = Z0 - 1; z <= Z0 + Wid; z++)
                    {
                        bool rim = x < X0 || x >= X0 + Len || z < Z0 || z >= Z0 + Wid;
                        BtcDuelZone zn = ZoneAt(x + 0.5f, z + 0.5f);
                        // 1-wide border lines between zones
                        VoxelTerrainType type = TypeOf(zn);
                        if (!rim)
                        {
                            int rx = x - X0;
                            if (rx == SafeLen || rx == SafeLen - 1 || rx == Len - SafeLen || rx == Len - SafeLen - 1 ||
                                rx == SafeLen + NeutralLen || rx == SafeLen + NeutralLen - 1 || rx == Len - SafeLen - NeutralLen || rx == Len - SafeLen - NeutralLen - 1)
                                type = VoxelTerrainType.Pavement;
                        }
                        int top = rim ? Top + 5 : Top;
                        VoxelEngine.SetColumnTopVoxel(x, z, top);
                        VoxelEngine.SetColumnType(x, z, rim ? VoxelTerrainType.Brick : type);
                        VoxelEngine.SetColumnCrustVolume(x, z, 1f);
                    }
                VoxelEngine.ApplyChanges(false);
                // tell navigation / physics / vision that the terrain changed (same signals BtcTerrainUndo fires)
                int fired = 0;
                for (int x = X0 - 1; x <= X0 + Len; x++)
                    for (int z = Z0 - 1; z <= Z0 + Wid; z++)
                    {
                        int ot = oldTop[(x - X0 + 1) * (Wid + 2) + (z - Z0 + 1)];
                        for (int dy = 0; dy <= 1; dy++) { FireAt(x, Top + dy, z); fired++; }
                        if (rebuild && Top + 5 != ot) { FireAt(x, Top + 5, z); fired++; }
                    }
                BtcLog.Info("Duel", "terrain change signals: " + fired);
                Built = true;
                return true;
            }
            catch (Exception e)
            {
                BtcLog.Error("Duel", "arena build failed", e);
                return false;
            }
        }

        private static void FireAt(int x, int y, int z)
        {
            if (y < 0 || y > 254) return;
            XYZ p = new XYZ(x, y, z);
            GameSignals.Invoke<XYZ>(GameSignals.VoxelChanged, p);
            GameSignals.Invoke<XYZ>(GameSignals.TerrainChanged, p);
            Architecte.Changement(p);
        }

        /// <summary>Verification sample logged after Build: surface heights at the corners and centre.</summary>
        internal static string Describe()
        {
            try
            {
                return "top(c)=" + VoxelEngine.GetColumnTop(X0 + Len / 2, Z0 + Wid / 2) + " top(sw)=" + VoxelEngine.GetColumnTop(X0, Z0) +
                       " top(ne)=" + VoxelEngine.GetColumnTop(X0 + Len - 1, Z0 + Wid - 1) + " rim=" + VoxelEngine.GetColumnTop(X0 - 1, Z0);
            }
            catch (Exception e) { return "describe failed: " + e.Message; }
        }
    }
}
