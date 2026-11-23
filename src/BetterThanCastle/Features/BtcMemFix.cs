using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Brix.External.Signals;
using Brix.Game.AI.KnowledgeSpace;
using Brix.Engine;
using Brix.Game.Components;
using Brix.Lifecycle.Pooling;
using Brix.Utils;
using HarmonyLib;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.SceneManagement;
using Tuple2 = Smooth.Algebraics.Tuple<UnityEngine.GameObject, UnityEngine.GameObject>;

namespace BetterThanCastle
{
    /// Memory fixes (fewer and shorter GC freezes). Ported from Castle Story Plus by Tricky12321 (GPLv3, CastleStoryPlus/Memory).
    /// Unity 5.6 Boehm stops the game and walks the whole live heap on every collection, so the pause grows with what is
    /// reachable: these fixes shrink what the collector walks (Lua value heap 280 MB, Lua stacks 18 MB each) and stop leaks.
    /// Local only: nothing here touches the wire. Config Performance/MemoryFixes, read once at start (patches use Prepare).
    internal static class BtcMemFix
    {
        private static bool? _on;

        /// Snapshot at plugin start: patches are applied only if it was on then (changing it needs a restart).
        internal static bool On
        {
            get
            {
                if (!_on.HasValue) _on = BtcConfig.MemoryFixes == null || BtcConfig.MemoryFixes.Value;
                return _on.Value;
            }
        }

        internal static void Init()
        {
            if (!On) return;
            try { ShrinkLuaHeap(); } catch (Exception e) { BtcLog.Error("MemFix", "lua heap: " + e); }
            try { if (BtcPlugin.Inst != null) BtcPlugin.Inst.gameObject.AddComponent<BtcMemSweeper>(); } catch (Exception e) { BtcLog.Error("MemFix", "sweeper: " + e); }
            SceneManager.activeSceneChanged += delegate { Patches.HighlighterKeep.ByRequest.Clear(); };
            BtcLog.Info("MemFix", "on");
        }

        // ------------------------------------------------------------------ Lua value heap (HeapAllocatedDynValue)
        // MoonSharp keeps every value a Lua closure captures in one static array of 10,485,760 slots (240 MB of references
        // + 40 MB of counts) that every collection reads. Start at 1M slots and double when they run out, as a List does.
        private const int StartSlots = 1 << 20;
        private static readonly FieldInfo HValues = AccessTools.Field(typeof(HeapAllocatedDynValue), "_heapAllocatedDynValue");
        private static readonly FieldInfo HCounts = AccessTools.Field(typeof(HeapAllocatedDynValue), "_refCount");
        private static readonly FieldInfo HSize = AccessTools.Field(typeof(HeapAllocatedDynValue), "_size");
        private static readonly FieldInfo HEmpty = AccessTools.Field(typeof(HeapAllocatedDynValue), "_emptySlots");

        private static void ShrinkLuaHeap()
        {
            Stack<int> empty = (Stack<int>)HEmpty.GetValue(null);
            lock (empty)
            {
                DynValue[] values = (DynValue[])HValues.GetValue(null);
                int used = (int)HSize.GetValue(null);
                int slots = StartSlots;
                while (slots < used * 2) slots *= 2;
                if (slots >= values.Length) return;
                HValues.SetValue(null, Copy(values, slots, used));
                HCounts.SetValue(null, Copy((int[])HCounts.GetValue(null), slots, used));
                BtcLog.Info("MemFix", "Lua value heap " + values.Length + " -> " + slots + " slots (" + used + " used)");
            }
        }

        /// Room for count more slots past the end (Allocate takes a freed slot first, else the next one past _size).
        internal static void EnsureLuaHeap(int count, Stack<int> empty)
        {
            lock (empty)
            {
                DynValue[] values = (DynValue[])HValues.GetValue(null);
                int used = (int)HSize.GetValue(null);
                int needed = used + Math.Max(0, count - empty.Count);
                if (needed <= values.Length) return;
                int slots = values.Length;
                while (slots < needed) slots *= 2;
                HValues.SetValue(null, Copy(values, slots, used));
                HCounts.SetValue(null, Copy((int[])HCounts.GetValue(null), slots, used));
                BtcLog.Info("MemFix", "Lua value heap grown to " + slots + " slots");
            }
        }

        private static T[] Copy<T>(T[] from, int slots, int used)
        {
            T[] to = new T[slots];
            Array.Copy(from, to, used);
            return to;
        }

        /// DEV/diagnostics: current Lua heap slot count.
        internal static int LuaHeapSlots()
        {
            try { DynValue[] v = (DynValue[])HValues.GetValue(null); return v != null ? v.Length : -1; } catch { return -1; }
        }

        // ------------------------------------------------------------------ LOD array pool (Brix.Core, private members)
        // The first LOD build after a load makes every LOD of the map at once and the static pool keeps that peak (~54 MB)
        // for the rest of the game; later updates need a small part. After a large build trim it to 8 MB, keeping the mix.
        private const long LodBudget = 8L * 1024 * 1024;
        internal static FieldInfo LodWork, LodDirty;
        private static FieldInfo _lodPools, _lodQueue;

        internal static void TrimLodPool()
        {
            if (_lodPools == null)
            {
                Type ap = AccessTools.TypeByName("Brix.Engine.FlatWorld.LodUpdater+ArrayPool`1");
                if (ap == null) return;
                _lodPools = AccessTools.Field(ap.MakeGenericType(typeof(Color32)), "_pools");
                if (_lodPools == null) return;
            }
            IDictionary pools = _lodPools.GetValue(null) as IDictionary;
            if (pools == null) return;
            long total = 0;
            foreach (DictionaryEntry e in pools)
            {
                Queue<Color32[]> q = Queue(e.Value);
                if (q != null) total += (long)(int)e.Key * 4 * q.Count;
            }
            if (total <= LodBudget) return;
            double keepShare = (double)LodBudget / total;
            long freed = 0;
            foreach (DictionaryEntry e in pools)
            {
                Queue<Color32[]> q = Queue(e.Value);
                if (q == null) continue;
                int drop = q.Count - (int)(q.Count * keepShare);
                if (drop <= 0) continue;
                for (int i = 0; i < drop; i++) q.Dequeue();
                q.TrimExcess();
                freed += (long)(int)e.Key * 4 * drop;
            }
            BtcLog.Info("MemFix", "LOD array pool trimmed by " + (freed >> 20) + " MB");
        }

        private static Queue<Color32[]> Queue(object pool)
        {
            if (pool == null) return null;
            if (_lodQueue == null) _lodQueue = AccessTools.Field(pool.GetType(), "_queue");
            return _lodQueue != null ? _lodQueue.GetValue(pool) as Queue<Color32[]> : null;
        }
    }

    /// Every 30 s drops entries the game never removes during a match: "A cannot reach B" exclusions of destroyed or pooled
    /// objects and fired reservation connections (one per pick-up or harvest).
    internal class BtcMemSweeper : MonoBehaviour
    {
        private float _next;
        private static readonly List<Tuple2> Dead = new List<Tuple2>();

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 30f;
            Knowledge k = Knowledge.Instance;
            if (k == null) return;
            try
            {
                KnowledgeExcluder ex = k.knowledgeExcluder;
                if (ex != null && ex.Exclusions2.Count > 0)
                {
                    Dead.Clear();
                    foreach (Tuple2 pair in ex.Exclusions2.Keys)
                        if (pair._1.IsNullOrReleased() || pair._2.IsNullOrReleased()) Dead.Add(pair);
                    for (int i = 0; i < Dead.Count; i++) ex.Exclusions2.Remove(Dead[i]);
                    Dead.Clear();
                }
                KnowledgeReserver res = k.knowledgeReserver;
                if (res != null)
                {
                    res.connections.RemoveAll((SignalConnection c) => !c.IsConnected);
                    res.Reservations.RemoveWhere((GameObject go) => go.IsNullOrReleased());
                }
            }
            catch (Exception e) { BtcLog.Warn("MemFix", "sweep: " + e.Message); }
        }
    }
}
