using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Engine;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Map editor live symmetry: objects, trees, nature props and voxel columns edited on one side are mirrored at once,
    /// using the symmetry mode/axis already set in Gameplay settings (BtcSymmetry). Local to the editing host, no wire traffic.
    /// </summary>
    internal static class BtcLiveSym
    {
        private static bool _busy;
        internal static bool Busy { get { return _busy; } set { _busy = value; } }
        internal static bool Editing { get { int m, sx, sz; Active(out m, out sx, out sz); return _edit; } }

        private static readonly float[] _ox = new float[3], _oz = new float[3];
        private static readonly bool[] _fx = new bool[3], _fz = new bool[3];

        private static int _frame = -1;
        private static bool _on;
        private static int _m, _sx, _sz;

        /// <summary>True while a user is editing in the world editor with a symmetry mode on (cached per frame).</summary>
        internal static bool Active(out int m, out int sx, out int sz)
        {
            m = sx = sz = 0;
            if (_busy) return false;
            try
            {
                int f = Time.frameCount;
                if (f != _frame)
                {
                    _frame = f;
                    _on = Compute(out _m, out _sx, out _sz);
                }
            }
            catch (Exception) { return false; }   // not the main thread
            m = _m; sx = _sx; sz = _sz;
            return _on;
        }

        private static bool _edit;
        private static float _lastInp = -10f;

        private static bool Compute(out int m, out int sx, out int sz)
        {
            m = sx = sz = 0;
            _edit = false;
            if (!BtcNetworkGate.Enabled) return false;
            if (Brix.Input.InputModeController.DefaultMode != Brix.Input.InputMode.worldEditor) return false;
            // Editor load restores objects through the same calls: only react to a user action after the scene settled.
            if (Time.timeSinceLevelLoad < 10f || BtcLiveSymUI.Over) return false;
            bool inp = Input.GetMouseButton(0) || Input.GetMouseButtonUp(0) || Input.GetKey(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace);
            if (inp) _lastInp = Time.unscaledTime;
            if (!inp && Time.unscaledTime - _lastInp > 0.3f) return false;   // brushes may commit a few frames after the release
            _edit = true;
            if (BtcConfig.LiveSymmetry == null || !BtcConfig.LiveSymmetry.Value) return false;
            int keep;
            return BtcSymmetry.LiveAxes(out m, out keep, out sx, out sz);
        }

        /// <summary>Calls place(x, z, flipX, flipZ) for every mirror image of (x, z), guarded against re-entry.</summary>
        internal static void ForEachImage(int m, int sx, int sz, float x, float z, Action<float, float, bool, bool> place)
        {
            int n = BtcSymmetry.Images(m, sx, sz, x, z, _ox, _oz, _fx, _fz);
            if (n == 0) return;
            float[] px = new float[n], pz = new float[n];
            bool[] ffx = new bool[n], ffz = new bool[n];
            Array.Copy(_ox, px, n); Array.Copy(_oz, pz, n); Array.Copy(_fx, ffx, n); Array.Copy(_fz, ffz, n);
            _busy = true;
            try
            {
                for (int i = 0; i < n; i++)
                {
                    try { place(px[i], pz[i], ffx[i], ffz[i]); }
                    catch (Exception e) { BtcLog.Error("LiveSym", e.ToString()); }
                }
            }
            finally { _busy = false; }
        }

        /// <summary>Yaw-only rotation of the mirrored object (flip across X: -yaw, across Z: 180 - yaw).</summary>
        internal static Quaternion Rot(Quaternion q, bool fx, bool fz)
        {
            float yaw = q.eulerAngles.y;
            if (fx) yaw = -yaw;
            if (fz) yaw = 180f - yaw;
            return Quaternion.Euler(0f, yaw, 0f);
        }

        // ---- generic mirroring of calls that take XYZ / Vector3 / List<XYZ> (nature registries, tree removal)
        private static XYZ MirXyz(XYZ p, bool fx, bool fz, int sx, int sz)
        {
            return new XYZ(fx ? sx - p.x : p.x, p.y, fz ? sz - p.z : p.z);
        }

        internal static void MirrorCall(MethodBase mb, object[] args)
        {
            int m, sx, sz;
            if (!Active(out m, out sx, out sz)) return;
            float rx = 0, rz = 0;
            bool found = false;
            for (int i = 0; i < args.Length && !found; i++)
            {
                if (args[i] is XYZ) { XYZ p = (XYZ)args[i]; rx = p.x; rz = p.z; found = true; }
                else if (args[i] is Vector3) { Vector3 v = (Vector3)args[i]; rx = v.x; rz = v.z; found = true; }
                else if (args[i] is List<XYZ> && ((List<XYZ>)args[i]).Count > 0) { XYZ p = ((List<XYZ>)args[i])[0]; rx = p.x; rz = p.z; found = true; }
            }
            if (!found) return;
            ForEachImage(m, sx, sz, rx, rz, (x, z, fx, fz) =>
            {
                object[] a = new object[args.Length];
                bool pos = true;
                for (int i = 0; i < args.Length; i++)
                {
                    object o = args[i];
                    if (o is XYZ) o = MirXyz((XYZ)o, fx, fz, sx, sz);
                    else if (o is Vector3)
                    {
                        Vector3 v = (Vector3)o;
                        if (pos) { pos = false; o = new Vector3(fx ? sx - v.x : v.x, v.y, fz ? sz - v.z : v.z); }
                        else o = new Vector3(fx ? -v.x : v.x, v.y, fz ? -v.z : v.z);   // offset direction
                    }
                    else if (o is List<XYZ>)
                    {
                        List<XYZ> src = (List<XYZ>)o, dst = new List<XYZ>(src.Count);
                        for (int k = 0; k < src.Count; k++) dst.Add(MirXyz(src[k], fx, fz, sx, sz));
                        o = dst;
                    }
                    a[i] = o;
                }
                mb.Invoke(null, a);
            });
        }

        // ---- hanging props: after terrain edits, nature/trees that no longer sit on the column top are removed
        private static readonly HashSet<long> _dirty = new HashSet<long>();
        private static readonly List<long> _batch = new List<long>();

        private static void MarkDirty(int x, int z)
        {
            if (BtcConfig.CleanHanging == null || !BtcConfig.CleanHanging.Value) return;
            lock (_dirty) _dirty.Add(((long)x << 32) | (uint)z);
        }

        /// <summary>Main thread, once per frame: checks a slice of the edited columns.</summary>
        internal static void Tick()
        {
            _batch.Clear();
            lock (_dirty)
            {
                if (_dirty.Count == 0) return;
                if (Input.GetMouseButton(0)) return;   // wait until the stroke ends
                foreach (long k in _dirty) { _batch.Add(k); if (_batch.Count >= 120) break; }
                for (int i = 0; i < _batch.Count; i++) _dirty.Remove(_batch[i]);
            }
            int removed = 0;
            for (int i = 0; i < _batch.Count; i++)
            {
                int x = (int)(_batch[i] >> 32), z = (int)(uint)_batch[i];
                int top = VoxelEngine.GetColumnTop(x, z);
                bool empty = VoxelEngine.IsEmptyColumn(x, z);
                for (int y = 0; y < 256; y++)
                {
                    if (!empty && y == top - 1) continue;   // props live on the top voxel
                    XYZ p = new XYZ(x, y, z);
                    try
                    {
                        // boulders span several voxels of a slope: only drop them when the whole column is gone
                        bool hang = Brix.Engine.Nature.PlantRegistry.IsPlant(p) || Brix.Engine.Nature.IronRegistry.HasMinerals(p)
                            || Brix.Engine.Nature.PyriteRegistry.HasMinerals(p) || (empty && Brix.Engine.Nature.BoulderRegistry.HasBoulders(p));
                        if (hang) { Brix.Engine.Nature.NatureRegistry.RemoveNatureAt(p); removed++; }
                        if (Brix.Legacy.TreeRegistry.Instance.GetTree(p) != 0) { Brix.Input.WorldEditor.BrushUtils.RemoveTree(p); removed++; }
                    }
                    catch (Exception) { }
                }
            }
            if (removed > 0) BtcLog.Info("LiveSym", "removed " + removed + " hanging props");
        }

        // ---- voxel columns: patched on the engine implementation (VoxelEngine wrappers are tiny and get inlined by Mono)
        private static bool _implDone;

        internal static void EnsureImplPatched()
        {
            if (_implDone) return;
            try
            {
                if (VoxelEngine.Impl == null) return;
                _implDone = true;
                Type t = VoxelEngine.Impl.GetType();
                HarmonyMethod post = new HarmonyMethod(typeof(BtcLiveSym), "ImplPostfix");
                HarmonyMethod pre = new HarmonyMethod(typeof(BtcLiveSym), "ImplPrefix");
                int n = 0;
                foreach (MethodInfo mi in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    ParameterInfo[] ps = mi.GetParameters();
                    bool write = mi.Name.StartsWith("SetColumn") || mi.Name.StartsWith("RemoveColumn");
                    BtcLog.Info("LiveSym", "impl " + t.Name + "." + mi.Name + "(" + ps.Length + ")" + (write ? " PATCH" : ""));
                    if (!write || ps.Length < 2 || ps[0].ParameterType != typeof(int) || ps[1].ParameterType != typeof(int)) continue;
                    BtcPlugin.Harmony.Patch(mi, prefix: pre, postfix: post);
                    n++;
                }
                BtcLog.Info("LiveSym", "voxel engine " + t.Name + ": " + n + " write methods patched");
            }
            catch (Exception e) { BtcLog.Error("LiveSym", "impl patch: " + e); }
        }

        private static void ImplPrefix(object[] __args)
        {
            try { BtcEdUndo.Column((int)__args[0], (int)__args[1]); }
            catch (Exception e) { BtcLog.Error("Undo", "column capture: " + e); }
        }

        private static void ImplPostfix(MethodBase __originalMethod, object __instance, object[] __args)
        {
            try
            {
                if (BtcEdUndo.Restoring) return;
                int m, sx, sz;
                bool act = Active(out m, out sx, out sz);
                int x = (int)__args[0], z = (int)__args[1];
                if (_edit || _busy) MarkDirty(x, z);
                if (!act) return;
                ForEachImage(m, sx, sz, x, z, (fx0, fz0, fx, fz) =>
                {
                    object[] a = (object[])__args.Clone();
                    a[0] = fx ? sx - x : x;
                    a[1] = fz ? sz - z : z;
                    __originalMethod.Invoke(__instance, a);
                });
            }
            catch (Exception e) { BtcLog.Error("LiveSym", "column: " + e); }
        }
    }
}
