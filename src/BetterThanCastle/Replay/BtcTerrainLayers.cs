using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Brix.Engine;
using Brix.Engine.FlatWorld.Layers;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay v4 terrain keyframes, exact: the voxel state above the heightmap lives in two layers of the (internal) LayerVoxelEngine:
    /// NegativeLayer.removedSet (dug cells) and TerrainSurfaceLayer.addedSet + p2resources (added sand/crust with their resources).
    /// SetVoxelData/AddTerrainResources cannot reproduce them (SetVoxelData only toggles presence, re-adding resources spills over),
    /// so a keyframe stores both sets and a jump writes them back through the sets' own Add/Remove, marks the cells dirty and redraws.
    /// Heightmap columns stay in the column part of the terrain blob (BtcReplayAnchor.TerrainBlob).
    /// </summary>
    internal static class BtcTerrainLayers
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static object _test;
        private static MethodInfo _getNeg, _getSurf, _draw;
        private static Action<XYZ> _setDirty;
        private static NegativeLayer _neg;
        private static TerrainSurfaceLayer _surf;
        private static bool _broken;

        internal static string LastInfo = "";

        private static bool Resolve()
        {
            if (_broken) return false;
            try
            {
                object eng = Voxel.Engine;
                if (eng == null) return false;
                FieldInfo ft = eng.GetType().GetField("test", Any);
                object test = ft != null ? ft.GetValue(eng) : null;
                if (test == null) return false;
                if (!ReferenceEquals(test, _test))
                {
                    Type t = test.GetType();
                    _getNeg = t.GetMethod("GetNegativeLayer", Any, null, Type.EmptyTypes, null);
                    _getSurf = t.GetMethod("GetTerrainSurfaceLayer", Any, null, Type.EmptyTypes, null);
                    _draw = t.GetMethod("DrawDirtyMegaVoxels", Any, null, Type.EmptyTypes, null);
                    MethodInfo sd = t.GetMethod("SetDirty", Any, null, new Type[] { typeof(XYZ) }, null);
                    if (_getNeg == null || _getSurf == null || _draw == null || sd == null)
                    {
                        _broken = true;
                        BtcLog.Error("ReplaySeek", "terrain layers: engine members not found, layer keyframes off");
                        return false;
                    }
                    _setDirty = (Action<XYZ>)Delegate.CreateDelegate(typeof(Action<XYZ>), test, sd);
                    _test = test;
                }
                // layers are re-created with the map: resolve them every time (two cheap calls)
                _neg = _getNeg.Invoke(_test, null) as NegativeLayer;
                _surf = _getSurf.Invoke(_test, null) as TerrainSurfaceLayer;
                return _neg != null && _surf != null;
            }
            catch (Exception e)
            {
                _broken = true;
                BtcLog.Error("ReplaySeek", "terrain layers: " + e.Message);
                return false;
            }
        }

        // x 12 bits, z 12 bits, y 8 bits (maps are <= 1024 wide)
        private static bool Key(XYZ p, out uint k)
        {
            k = 0u;
            if (p.x < 0 || p.x > 4095 || p.z < 0 || p.z > 4095 || p.y < 0 || p.y > 255) return false;
            k = (uint)p.x << 20 | (uint)p.z << 8 | (uint)p.y;
            return true;
        }

        private static XYZ Pos(uint k)
        {
            return new XYZ((int)(k >> 20), (int)(k & 255u), (int)(k >> 8 & 4095u));
        }

        private static List<uint> Keys(ColumnPointSet set)
        {
            List<uint> l = new List<uint>();
            foreach (XYZ p in set.GetPoints())
            {
                uint k;
                if (!Key(p, out k)) throw new Exception("voxel out of key range " + p);
                l.Add(k);
            }
            l.Sort();
            return l;
        }

        /// <summary>Both layer sets of the live world (sorted, so equal terrain gives equal bytes); null when unavailable.</summary>
        internal static byte[] Snap()
        {
            if (!Resolve()) return null;
            List<uint> neg = Keys(_neg.removedSet);
            List<uint> add = Keys(_surf.addedSet);
            MemoryStream ms = new MemoryStream(16 + neg.Count * 4 + add.Count * 12);
            BinaryWriter w = new BinaryWriter(ms);
            w.Write(neg.Count);
            for (int i = 0; i < neg.Count; i++) w.Write(neg[i]);
            w.Write(add.Count);
            for (int i = 0; i < add.Count; i++)
            {
                w.Write(add[i]);
                VoxelTerrainResources r;
                if (!_surf.p2resources.TryGetValue(Pos(add[i]), out r) || r == null || r.types == null) { w.Write((byte)0); continue; }
                int n = Math.Min(r.types.Length, 255);
                w.Write((byte)n);
                for (int j = 0; j < n; j++) { w.Write((short)r.types[j]); w.Write((short)(j < r.amounts.Length ? r.amounts[j] : 0)); }
            }
            return ms.ToArray();
        }

        /// <summary>Make the live layer sets equal to the snapshot; returns the number of changed cells (-1 = not possible).</summary>
        internal static int Apply(byte[] target)
        {
            if (target == null || !Resolve()) return -1;
            HashSet<uint> tNeg = new HashSet<uint>();
            Dictionary<uint, VoxelTerrainResources> tAdd = new Dictionary<uint, VoxelTerrainResources>();
            BinaryReader r = new BinaryReader(new MemoryStream(target));
            int nn = r.ReadInt32();
            for (int i = 0; i < nn; i++) tNeg.Add(r.ReadUInt32());
            int na = r.ReadInt32();
            for (int i = 0; i < na; i++)
            {
                uint k = r.ReadUInt32();
                int n = r.ReadByte();
                VoxelTerrainResources res = new VoxelTerrainResources();
                res.types = new int[n];
                res.amounts = new int[n];
                for (int j = 0; j < n; j++) { res.types[j] = r.ReadInt16(); res.amounts[j] = r.ReadInt16(); }
                tAdd[k] = res;
            }
            HashSet<uint> cNeg = new HashSet<uint>(Keys(_neg.removedSet));
            HashSet<uint> cAdd = new HashSet<uint>(Keys(_surf.addedSet));
            List<XYZ> changed = new List<XYZ>();
            int dug = 0, filled = 0, added = 0, removed = 0, res2 = 0;
            foreach (uint k in cNeg) if (!tNeg.Contains(k)) { XYZ p = Pos(k); _neg.removedSet.Remove(p); changed.Add(p); filled++; }
            foreach (uint k in tNeg) if (!cNeg.Contains(k)) { XYZ p = Pos(k); _neg.removedSet.Add(p); changed.Add(p); dug++; }
            foreach (uint k in cAdd) if (!tAdd.ContainsKey(k)) { XYZ p = Pos(k); _surf.addedSet.Remove(p); changed.Add(p); removed++; }
            foreach (KeyValuePair<uint, VoxelTerrainResources> kv in tAdd)
            {
                XYZ p = Pos(kv.Key);
                bool had = cAdd.Contains(kv.Key);
                if (!had) { _surf.addedSet.Add(p); added++; }
                VoxelTerrainResources live;
                if (had && _surf.p2resources.TryGetValue(p, out live) && SameRes(live, kv.Value)) continue;
                _surf.p2resources[p] = kv.Value;
                if (had) res2++;
                changed.Add(p);
            }
            for (int i = 0; i < changed.Count; i++)
            {
                _setDirty(changed[i]);
                BtcReplayAnchor.FireVoxel(changed[i]);
                BtcTerrainUndo.MarkAt(changed[i]);
            }
            if (changed.Count > 0) _draw.Invoke(_test, null);
            LastInfo = "layers: dug " + dug + ", refilled " + filled + ", added " + added + ", removed " + removed + ", resources " + res2;
            return changed.Count;
        }

        /// <summary>DEV: membership of p in a layer blob: N = dug (negative), A = added (with resources), - = neither.</summary>
        internal static string Has(byte[] lay, XYZ p)
        {
            uint key;
            if (lay == null || !Key(p, out key)) return "?";
            BinaryReader r = new BinaryReader(new MemoryStream(lay));
            string o = "";
            int nn = r.ReadInt32();
            for (int i = 0; i < nn; i++) if (r.ReadUInt32() == key) o += "N";
            int na = r.ReadInt32();
            for (int i = 0; i < na; i++)
            {
                uint k = r.ReadUInt32();
                int n = r.ReadByte();
                if (k == key) o += "A" + n;
                r.ReadBytes(n * 4);
            }
            return o.Length > 0 ? o : "-";
        }

        private static bool SameRes(VoxelTerrainResources a, VoxelTerrainResources b)
        {
            if (a == null || a.types == null || a.amounts == null) return false;
            if (a.types.Length != b.types.Length || a.amounts.Length != b.amounts.Length) return false;
            for (int i = 0; i < a.types.Length; i++) if (a.types[i] != b.types[i] || a.amounts[i] != b.amounts[i]) return false;
            return true;
        }
    }
}
