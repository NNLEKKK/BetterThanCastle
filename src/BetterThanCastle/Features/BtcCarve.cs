using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Engine;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Map editor Carve tool: break or add single voxels or NxNxN blocks anywhere in the 3D voxel engine (caves, multi-level
    /// passages: the Island brush only edits one top/bottom span per column). LMB (hold to drag), Ctrl+Z undoes a stroke.
    /// Mirrors through live symmetry when it is on. Editor only; local to the editing host.
    /// </summary>
    internal static class BtcCarve
    {
        internal static bool On;
        internal static bool AddMode;
        internal static int Size = 1;
        internal static int TypeIdx;
        internal static readonly VoxelTerrainType[] Types =
        {
            VoxelTerrainType.Stone, VoxelTerrainType.Earth, VoxelTerrainType.Grass,
            VoxelTerrainType.Sand, VoxelTerrainType.Snow, VoxelTerrainType.Pavement
        };

        private struct Cell { public XYZ P; public int Type; public int Vol; }

        private static bool _has, _hasPrev, _stroke;
        private static XYZ _hit, _prev, _last;
        private static bool _hasLast;
        private static readonly List<XYZ> _preview = new List<XYZ>();
        private static readonly List<List<Cell>> _undo = new List<List<Cell>>();
        private static List<Cell> _group;
        private static MethodInfo _setData;

        private static bool Same(XYZ a, XYZ b) { return a.x == b.x && a.y == b.y && a.z == b.z; }
        private static long Key(XYZ p) { return ((long)p.x << 40) ^ ((long)p.y << 20) ^ (long)p.z; }

        internal static void Tick()
        {
            if (!On) { _has = false; _preview.Clear(); EndStroke(); return; }
            try
            {
                if (BtcLiveSymUI.Over) { _has = false; _preview.Clear(); return; }
                Raycast();
                _preview.Clear();
                if (!_has) { if (Input.GetMouseButtonUp(0)) EndStroke(); return; }
                XYZ anchor;
                if (AddMode) { if (!_hasPrev) return; anchor = _prev; } else anchor = _hit;
                Expand(anchor, _preview);
                if (Input.GetMouseButtonDown(0)) { _stroke = true; _group = new List<Cell>(); _hasLast = false; }
                // one voxel block per click; dragging carves continuously only while Alt is held
                bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                if (_stroke && Input.GetMouseButton(0) && (!_hasLast || (alt && !Same(anchor, _last))))
                {
                    _last = anchor; _hasLast = true;
                    Apply(anchor);
                }
                if (Input.GetMouseButtonUp(0)) EndStroke();
            }
            catch (Exception e) { BtcLog.Error("Carve", e.ToString()); EndStroke(); }
        }

        private static void EndStroke()
        {
            if (_stroke && _group != null && _group.Count > 0)
            {
                _undo.Add(_group);
                if (_undo.Count > 200) _undo.RemoveAt(0);
            }
            _stroke = false; _group = null; _hasLast = false;
        }

        private static void Raycast()
        {
            _has = _hasPrev = false;
            Camera cam = Camera.main;
            if (cam == null) return;
            Ray r = cam.ScreenPointToRay(Input.mousePosition);
            XYZ prev = default(XYZ), lastC = default(XYZ);
            bool havePrev = false, haveLast = false;
            for (float t = 0f; t < 500f; t += 0.3f)
            {
                Vector3 p = r.origin + r.direction * t;
                XYZ c = XYZ.FromVector3(p);
                if (haveLast && Same(c, lastC)) continue;
                lastC = c; haveLast = true;
                if (c.y < 0 || c.y > 255) { if (havePrev) prev = c; continue; }
                if (Voxel.IsNonEmpty(c))
                {
                    if (!Voxel.IsTerrain(c)) return;   // a building is in the way
                    _hit = c; _prev = prev; _hasPrev = havePrev; _has = true;
                    return;
                }
                prev = c; havePrev = true;
            }
        }

        private static void Expand(XYZ a, List<XYZ> into)
        {
            int lo = -(Size - 1) / 2, hi = Size / 2;
            for (int dx = lo; dx <= hi; dx++)
                for (int dy = lo; dy <= hi; dy++)
                    for (int dz = lo; dz <= hi; dz++)
                        into.Add(new XYZ(a.x + dx, a.y + dy, a.z + dz));
        }

        private static void Apply(XYZ anchor)
        {
            List<XYZ> cells = new List<XYZ>();
            Expand(anchor, cells);
            int m, sx, sz;
            if (BtcLiveSym.Active(out m, out sx, out sz))
            {
                HashSet<long> seen = new HashSet<long>();
                for (int i = 0; i < cells.Count; i++) seen.Add(Key(cells[i]));
                int n0 = cells.Count;
                for (int i = 0; i < n0; i++)
                {
                    XYZ c = cells[i];
                    List<XYZ> extra = new List<XYZ>();
                    BtcLiveSym.ForEachImage(m, sx, sz, c.x, c.z, (x, z, fx, fz) => extra.Add(new XYZ(Mathf.RoundToInt(x), c.y, Mathf.RoundToInt(z))));
                    for (int k = 0; k < extra.Count; k++) if (seen.Add(Key(extra[k]))) cells.Add(extra[k]);
                }
            }
            List<Cell> target = new List<Cell>();
            for (int i = 0; i < cells.Count; i++)
            {
                XYZ c = cells[i];
                if (c.y < 0 || c.y > 255) continue;
                Cell cell = new Cell { P = c };
                if (AddMode)
                {
                    if (Voxel.IsNonEmpty(c)) continue;
                    cell.Type = (int)Types[Mathf.Clamp(TypeIdx, 0, Types.Length - 1)];
                    cell.Vol = 255;
                }
                else
                {
                    if (!Voxel.IsTerrain(c) || Voxel.IsInvincible(c)) continue;
                }
                target.Add(cell);
            }
            if (target.Count == 0) return;
            List<Cell> before = Snapshot(target);
            SetCells(target);
            BtcEdUndo.PushOther(() => SetCells(before));
            if (!AddMode)
                for (int i = 0; i < target.Count; i++)
                {
                    XYZ p = target[i].P;
                    try
                    {
                        Brix.Engine.Nature.NatureRegistry.RemoveNatureAt(p);   // props standing on a broken voxel go with it
                        if (Brix.Legacy.TreeRegistry.Instance.GetTree(p) != 0) Brix.Input.WorldEditor.BrushUtils.RemoveTree(p);
                    }
                    catch (Exception) { }
                }
        }

        private static List<Cell> Snapshot(List<Cell> target)
        {
            List<Cell> res = new List<Cell>(target.Count);
            for (int i = 0; i < target.Count; i++)
            {
                VoxelHandle vh = Voxel.Engine.GetVoxelHandle(target[i].P);
                Cell c = new Cell { P = target[i].P };
                if (Voxel.Engine.IsVoxelNonEmpty(vh)) { c.Type = Voxel.Engine.GetVoxelType(vh); c.Vol = Voxel.Engine.GetVoxelVolume(vh); }
                res.Add(c);
            }
            return res;
        }

        // Makes every cell match (Type 0 = empty). Proven sequence from the replay seek: remove, ApplyChanges, SetVoxelData, ApplyChanges.
        private static void SetCells(List<Cell> target)
        {
            object eng = Voxel.Engine;
            if (_setData == null) _setData = eng.GetType().GetMethod("SetVoxelData", new Type[] { typeof(VoxelHandle), typeof(int), typeof(int), typeof(int) });
            VoxelEngine.BeginChanges();
            for (int i = 0; i < target.Count; i++)
            {
                VoxelHandle vh = Voxel.Engine.GetVoxelHandle(target[i].P);
                if (Voxel.Engine.IsVoxelNonEmpty(vh)) Voxel.Engine.RemoveVoxel(vh);
            }
            VoxelEngine.ApplyChanges(false);
            bool any = false;
            for (int i = 0; i < target.Count; i++) if (target[i].Type != 0) { any = true; break; }
            if (!any) return;
            VoxelEngine.BeginChanges();
            object[] args = new object[4];
            for (int i = 0; i < target.Count; i++)
            {
                if (target[i].Type == 0) continue;
                args[0] = Voxel.Engine.GetVoxelHandle(target[i].P);
                args[1] = target[i].Type; args[2] = target[i].Vol; args[3] = 0;
                _setData.Invoke(eng, args);
            }
            VoxelEngine.ApplyChanges(false);
            // fallback for cells SetVoxelData did not fill: the game's own re-add path
            VoxelTerrainResources res = new VoxelTerrainResources();
            bool began = false;
            for (int i = 0; i < target.Count; i++)
            {
                if (target[i].Type == 0) continue;
                VoxelHandle vh = Voxel.Engine.GetVoxelHandle(target[i].P);
                if (Voxel.Engine.GetVoxelType(vh) == target[i].Type) continue;
                if (!began) { VoxelEngine.BeginChanges(); began = true; }
                res.Clear();
                res.Add(target[i].Type, target[i].Vol >= 255 ? 10 : Mathf.Clamp(target[i].Vol * 2, 1, 10));
                Voxel.Engine.AddTerrainResources(vh, res);
            }
            if (began) VoxelEngine.ApplyChanges(false);
        }

        private static void Undo()
        {
            List<Cell> g = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            SetCells(g);
        }

        /// <summary>Wireframe of the brush under the cursor (green = break, cyan = add). Called inside Camera.onPostRender.</summary>
        internal static void Draw()
        {
            if (!On || _preview.Count == 0) return;
            GL.Begin(GL.LINES);
            GL.Color(AddMode ? new Color(0.3f, 0.9f, 1f, 0.9f) : new Color(1f, 0.4f, 0.3f, 0.9f));
            for (int i = 0; i < _preview.Count; i++)
            {
                Vector3 c = _preview[i].ToVector3();
                Vector3 a = c - new Vector3(0.5f, 0.5f, 0.5f), b = c + new Vector3(0.5f, 0.5f, 0.5f);
                Edge(a.x, a.y, a.z, b.x, a.y, a.z); Edge(a.x, b.y, a.z, b.x, b.y, a.z); Edge(a.x, a.y, b.z, b.x, a.y, b.z); Edge(a.x, b.y, b.z, b.x, b.y, b.z);
                Edge(a.x, a.y, a.z, a.x, b.y, a.z); Edge(b.x, a.y, a.z, b.x, b.y, a.z); Edge(a.x, a.y, b.z, a.x, b.y, b.z); Edge(b.x, a.y, b.z, b.x, b.y, b.z);
                Edge(a.x, a.y, a.z, a.x, a.y, b.z); Edge(b.x, a.y, a.z, b.x, a.y, b.z); Edge(a.x, b.y, a.z, a.x, b.y, b.z); Edge(b.x, b.y, a.z, b.x, b.y, b.z);
            }
            GL.End();
        }

        private static void Edge(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            GL.Vertex3(x0, y0, z0); GL.Vertex3(x1, y1, z1);
        }
    }
}
