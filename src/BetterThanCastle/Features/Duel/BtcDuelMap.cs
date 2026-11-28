using System;
using Brix.Engine;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Micro-duel arena. Normal path: the own empty map (BtcDuelVoid, stock Void + 2 spawn markers) and a 1-voxel slab hanging in the
    /// void at VoidTop (columns with bottom = top, like the aim trainer floor): no walls, a unit that walks off the edge falls and dies.
    /// Fallback (empty map missing): the old 100 x 20 plateau raised above an existing map, walled all around.
    /// Zones run along +X: safe A 15 | neutral A 20 | mid 30 | neutral B 20 | safe B 15. Zone colours = terrain types. Side A west, side B east.
    /// The void slab is deterministic from (centre, scale): PvP clients rebuild the same slab locally from the host state message.
    /// </summary>
    internal static partial class BtcDuelMap
    {
        internal const int VoidTop = 235;
        /// <summary>True when the arena is the slab over the void (falling off = death).</summary>
        internal static bool OnVoid;
        internal static int CenterXi { get { return Mathf.RoundToInt(_cx); } }
        internal static int CenterZi { get { return Mathf.RoundToInt(_cz); } }

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

        private static VoxelTerrainType CellType(int x, int z)
        {
            VoxelTerrainType type = TypeOf(ZoneAt(x + 0.5f, z + 0.5f));
            int rx = x - X0;
            // 1-wide border lines between zones
            if (rx == SafeLen || rx == SafeLen - 1 || rx == Len - SafeLen || rx == Len - SafeLen - 1 ||
                rx == SafeLen + NeutralLen || rx == SafeLen + NeutralLen - 1 || rx == Len - SafeLen - NeutralLen || rx == Len - SafeLen - NeutralLen - 1)
                type = VoxelTerrainType.Pavement;
            return type;
        }

        // ---------------- void slab ----------------
        /// <summary>Builds (or moves / resizes) the slab around the centre. Call SetScale first. Old slab columns outside the new one are removed.</summary>
        internal static bool BuildVoid(int cx, int cz)
        {
            try
            {
                if (!VoxelEngine.Ready) { BtcLog.Warn("Duel", "voxel engine not ready"); return false; }
                bool had = Built && OnVoid;
                _cx = cx; _cz = cz;
                X0 = cx - Len / 2; Z0 = cz - Wid / 2; Top = VoidTop;
                OnVoid = true;
                VoxelEngine.BeginChanges();
                int removed = 0;
                if (had && _pLen > 0)
                    for (int x = _pX0; x < _pX0 + _pLen; x++)
                        for (int z = _pZ0; z < _pZ0 + _pWid; z++)
                            if (x < X0 || x >= X0 + Len || z < Z0 || z >= Z0 + Wid) { VoxelEngine.RemoveColumn(x, z); removed++; }
                for (int x = X0; x < X0 + Len; x++)
                    for (int z = Z0; z < Z0 + Wid; z++)
                    {
                        VoxelEngine.SetColumnTopVoxel(x, z, VoidTop);
                        VoxelEngine.SetColumnBottom(x, z, VoidTop);
                        VoxelEngine.SetColumnType(x, z, CellType(x, z));
                        VoxelEngine.SetColumnCrustVolume(x, z, 1f);
                    }
                VoxelEngine.ApplyChanges(false);
                int b, t;
                VoxelEngine.GetColumnData(X0 + Len / 2, Z0 + Wid / 2, out b, out t);
                if (b >= t)
                {
                    // the engine treats bottom as exclusive: one voxel lower (same fix as the aim floor)
                    VoxelEngine.BeginChanges();
                    for (int x = X0; x < X0 + Len; x++) for (int z = Z0; z < Z0 + Wid; z++) VoxelEngine.SetColumnBottom(x, z, VoidTop - 1);
                    VoxelEngine.ApplyChanges(false);
                    VoxelEngine.GetColumnData(X0 + Len / 2, Z0 + Wid / 2, out b, out t);
                }
                bool ok = b < t && t >= VoidTop;
                for (int x = X0; x < X0 + Len; x++)
                    for (int z = Z0; z < Z0 + Wid; z++)
                        for (int dy = 0; dy <= 1; dy++) FireAt(x, VoidTop + dy, z);
                if (had && _pLen > 0)
                    for (int x = _pX0; x < _pX0 + _pLen; x++)
                        for (int z = _pZ0; z < _pZ0 + _pWid; z++)
                            if (x < X0 || x >= X0 + Len || z < Z0 || z >= Z0 + Wid) FireAt(x, VoidTop, z);
                BtcLog.Info("Duel", "void slab " + Len + "x" + Wid + " X0=" + X0 + " Z0=" + Z0 + " col(b,t)=" + b + "," + t + " removed=" + removed + (ok ? "" : " FAILED"));
                Built = ok;
                return ok;
            }
            catch (Exception e)
            {
                BtcLog.Error("Duel", "void slab build failed", e);
                return false;
            }
        }

        // ---------------- fallback plateau over a real map ----------------
        /// <summary>Rewrites the arena columns around the anchor. Returns false (and logs) if the voxel engine is not ready.</summary>
        internal static bool Build(Vector3 anchor)
        {
            try
            {
                if (!VoxelEngine.Ready) { BtcLog.Warn("Duel", "voxel engine not ready"); return false; }
                OnVoid = false;
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
                        int top = rim ? Top + 5 : Top;
                        VoxelEngine.SetColumnTopVoxel(x, z, top);
                        VoxelEngine.SetColumnType(x, z, rim ? VoxelTerrainType.Brick : CellType(x, z));
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

        /// <summary>Forget the arena (a new game loads a new world).</summary>
        internal static void Reset() { Built = false; OnVoid = false; _pX0 = _pZ0 = _pLen = _pWid = -1; }

        /// <summary>Verification sample logged after Build: surface heights at the corners and centre.</summary>
        internal static string Describe()
        {
            try
            {
                return "top(c)=" + VoxelEngine.GetColumnTop(X0 + Len / 2, Z0 + Wid / 2) + " top(sw)=" + VoxelEngine.GetColumnTop(X0, Z0) +
                       " top(ne)=" + VoxelEngine.GetColumnTop(X0 + Len - 1, Z0 + Wid - 1) + " rim=" + VoxelEngine.GetColumnTop(X0 - 1, Z0) + " void=" + OnVoid;
            }
            catch (Exception e) { return "describe failed: " + e.Message; }
        }
    }

    /// <summary>The duel's own empty map: stock Void + 2 spawn markers, single player and multiplayer (PvP duels host it in a lobby).</summary>
    internal static class BtcDuelVoid
    {
        internal const string Codename = "BtcDuelArena_6e2f4a10-3c1b-4d8e-9b7a-duel00000001";
        private static readonly BtcVoidMap.Spec _spec = new BtcVoidMap.Spec
        {
            Folder = "BtcDuelArena", Codename = Codename, Version = 1, Tag = "Duel", Display = "BTC Micro duel",
            Description = "Empty map used by the Better Than Castle micro duel.", Context = 3,
            // home crystals far from the arena (the arena goes to the middle of the world)
            Marks = new[] { new Vector3(120f, 120f, 120f), new Vector3(140f, 120f, 120f) }, IdBase = "go:6e2f4a10-0000-4d8e-9b7a-d0e1000000",
        };

        internal static bool Ensure() { return BtcVoidMap.Ensure(_spec); }
    }
}
