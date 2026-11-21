using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.Engine.Nature;
using Brix.Game;
using Brix.Legacy;
using Brix.Lua;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Ctrl+Z in the world editor. One group per mouse stroke (or Delete press). Records the state BEFORE a change:
    /// terrain columns (all brushes, Carve, symmetry mirrors), trees, iron/pyrite, objects spawned from the side panel
    /// (undo removes them). Not covered: removed objects/plants (cannot be re-created exactly), placed blueprints/buildings.
    /// </summary>
    internal static class BtcEdUndo
    {
        private class Group { public readonly List<Action> Batch = new List<Action>(); public readonly List<Action> Other = new List<Action>(); }

        private static readonly List<Group> _groups = new List<Group>();
        private static Group _cur;
        private static readonly HashSet<long> _seen = new HashSet<long>();
        private static int _gid, _curGid = -1, _frame = -1;
        internal static bool Restoring;

        internal static bool Recording()
        {
            if (Restoring) return false;
            try { return BtcLiveSym.Editing || BtcLiveSym.Busy; }
            catch (Exception) { return false; }
        }

        private static void Push(long key, bool batch, Action undo)
        {
            Edge();
            if (_cur == null || _curGid != _gid)
            {
                Flush();
                _cur = new Group(); _seen.Clear(); _curGid = _gid;
            }
            if (key != 0 && !_seen.Add(key)) return;
            (batch ? _cur.Batch : _cur.Other).Add(undo);
        }

        // A new stroke starts on every mouse-down (or Delete) frame. Checked once per frame from Tick (every frame) and from Push,
        // so a stroke whose first change lands a frame after the press still gets its own group.
        private static void Edge()
        {
            if (Time.frameCount == _frame) return;
            _frame = Time.frameCount;
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) _gid++;
        }

        private static void Flush()
        {
            if (_cur != null && (_cur.Batch.Count > 0 || _cur.Other.Count > 0))
            {
                _groups.Add(_cur);
                if (_groups.Count > 200) _groups.RemoveAt(0);
            }
            _cur = null;
        }

        internal static void Tick()
        {
            Edge();
            if (!(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) || !Input.GetKeyDown(KeyCode.Z)) return;
            if (Input.GetMouseButton(0)) return;
            Flush();
            if (_groups.Count == 0) return;
            Group g = _groups[_groups.Count - 1];
            _groups.RemoveAt(_groups.Count - 1);
            Restoring = true;
            try
            {
                for (int i = g.Other.Count - 1; i >= 0; i--) { try { g.Other[i](); } catch (Exception e) { BtcLog.Error("Undo", e.ToString()); } }
                if (g.Batch.Count > 0)
                {
                    VoxelEngine.BeginChanges();
                    for (int i = g.Batch.Count - 1; i >= 0; i--) { try { g.Batch[i](); } catch (Exception e) { BtcLog.Error("Undo", e.ToString()); } }
                    VoxelEngine.ApplyChanges(false);
                    GameSignals.Invoke(GameSignals.OnTerrainEdited);
                }
                BtcLog.Info("Undo", "editor undo: " + (g.Batch.Count + g.Other.Count) + " changes");
            }
            finally { Restoring = false; }
        }

        /// <summary>Own undo entry (Carve): runs outside the column batch.</summary>
        internal static void PushOther(Action undo) { if (Recording()) Push(0, false, undo); }

        // ---- terrain column, captured before an engine write
        internal static void Column(int x, int z)
        {
            if (!Recording()) return;
            int bottom, top; VoxelTerrainType type; float crust;
            VoxelEngine.GetColumnData(x, z, out bottom, out top, out type, out crust);
            Push(0x4000000000000000L | ((long)x << 20) | (uint)z, true, () =>
            {
                if (bottom >= top) { VoxelEngine.RemoveColumn(x, z); return; }
                VoxelEngine.RemoveColumn(x, z);
                VoxelEngine.SetColumnBottom(x, z, bottom);
                VoxelEngine.SetColumnTopVoxel(x, z, top);
                VoxelEngine.SetColumnType(x, z, type);
                VoxelEngine.SetColumnCrustVolume(x, z, crust);
            });
        }

        internal static void Tree(XYZ p)
        {
            if (!Recording()) return;
            int info = TreeRegistry.Instance.GetTree(p);
            Push(0x2000000000000000L | ((long)p.x << 36) | ((long)p.y << 20) | (uint)p.z, true, () =>
            {
                if (info == 0) { if (TreeRegistry.Instance.GetTree(p) != 0) Brix.Input.WorldEditor.BrushUtils.RemoveTree(p); return; }
                if (TreeRegistry.Instance.GetTree(p) == 0) TreeRegistry.Instance.Add(p, info & 15);
                TreeRegistry.Instance.ModifyTree(p, info);
            });
        }

        /// <summary>Iron / pyrite presence before an add/remove call (kind: 0 iron, 1 pyrite).</summary>
        internal static void Mineral(int kind, XYZ p)
        {
            if (!Recording()) return;
            bool had = kind == 0 ? IronRegistry.HasMinerals(p) : PyriteRegistry.HasMinerals(p);
            Push(0x1000000000000000L | ((long)kind << 58) | ((long)p.x << 36) | ((long)p.y << 20) | (uint)p.z, true, () =>
            {
                if (kind == 0) { if (had) IronRegistry.AddOrGrow(p, 1f); else IronRegistry.Remove(p); }
                else { if (had) PyriteRegistry.AddOrGrow(p, 1f); else PyriteRegistry.Remove(p); }
            });
        }

        /// <summary>Plant added by a call: undo removes it (a removed plant cannot be restored, its type is unknown).</summary>
        internal static void PlantAdd(XYZ p)
        {
            if (!Recording() || PlantRegistry.IsPlant(p)) return;
            Push(0x0800000000000000L | ((long)p.x << 36) | ((long)p.y << 20) | (uint)p.z, true, () => PlantRegistry.Remove(p));
        }

        internal static void Spawned(LuaDriver d)
        {
            if (d == null || !Recording()) return;
            Push(0, false, () => { if (d != null) d.Remove(); });
        }
    }
}
