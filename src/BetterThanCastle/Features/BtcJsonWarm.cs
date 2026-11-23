using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Brix.Components;
using Brix.Engine;
using Brix.IO.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// The first game-object save of a session (the replay snapshot at match start, a quicksave) froze the game ~1.6 s, a second one
    /// takes 15 ms: the cost is warm-up (JIT of the Newtonsoft writer and the game converters, contracts of every component type,
    /// property getters). It is done here on a background thread while the world loads (terrain is frame bound, a core is free):
    /// at load begin the writer code is JIT compiled, after the game objects are loaded the main thread lists the component types
    /// of the save queue (a few ms) and the thread resolves their contracts with the game's own "save" resolver (Newtonsoft's
    /// contract cache is copy-on-write and lock protected) and prepares their getters. Same JSON, nothing on the wire.
    /// Part of Performance/FastLoad.
    /// </summary>
    internal static class BtcJsonWarm
    {
        private static readonly object Lock = new object();
        private static readonly Queue<Type> Pending = new Queue<Type>();
        private static readonly HashSet<Type> Seen = new HashSet<Type>();
        private static IContractResolver _resolver;
        private static Thread _thread;
        private static bool _jitDone;
        private static int _contracts, _methods;
        private static long _ms;

        internal static bool On { get { return BtcConfig.FastLoad == null || BtcConfig.FastLoad.Value; } }

        /// <summary>Load begin: start the worker (JIT of the writer code once per session) and listen for the loaded objects.</summary>
        internal static void OnLoadBegin()
        {
            if (!On) return;
            try
            {
                GameSignals.Once(GameSignals.GameObjectsLoaded, delegate { Collect("objects"); }, null);
                GameSignals.Once(GameSignals.GamemodeLoaded, delegate { Collect("gamemode"); }, null);
                Kick();
            }
            catch (Exception e) { BtcLog.Error("JsonWarm", "begin: " + e.Message); }
        }

        private static void Kick()
        {
            lock (Lock)
            {
                if (_thread != null) return;
                _thread = new Thread(Work);
                _thread.IsBackground = true;
                _thread.Priority = System.Threading.ThreadPriority.BelowNormal;
                _thread.Name = "BtcJsonWarm";
                _thread.Start();
            }
        }

        /// <summary>Main thread: component types of everything in the save queue.</summary>
        private static void Collect(string when)
        {
            try
            {
                GameObjectSerializer gos = BrixSingleton<GameObjectSerializer>.Instance;
                if (gos == null) return;
                if (_resolver == null) _resolver = Io.GetSaveSerializerSettings("save").ContractResolver;   // game's cached settings (not thread safe: main thread only)
                if (_queueField == null) _queueField = typeof(GameObjectSerializer).GetField("_saveQueue", BindingFlags.Instance | BindingFlags.NonPublic);
                LinkedList<SerializationComponent> q = _queueField == null ? null : _queueField.GetValue(gos) as LinkedList<SerializationComponent>;
                if (q == null) return;
                int added = 0;
                lock (Lock)
                {
                    foreach (SerializationComponent sc in q)
                    {
                        if (sc == null) continue;
                        sc.GetComponentsInChildren(true, Comps);
                        for (int i = 0; i < Comps.Count; i++)
                        {
                            if (Comps[i] == null) continue;
                            Type t = Comps[i].GetType();
                            if (Seen.Add(t)) { Pending.Enqueue(t); added++; }
                        }
                        Comps.Clear();
                    }
                    if (Seen.Add(typeof(GameObject))) Pending.Enqueue(typeof(GameObject));
                }
                BtcLog.Info("JsonWarm", when + ": " + q.Count + " saved objects, " + added + " new component types queued");
                lock (Lock) { if (_thread == null) Kick(); else Monitor.Pulse(Lock); }
            }
            catch (Exception e) { BtcLog.Error("JsonWarm", "collect: " + e.Message); }
        }

        private static FieldInfo _queueField;
        private static readonly List<Component> Comps = new List<Component>();

        private static void Work()
        {
            try
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                if (!_jitDone) { _jitDone = true; JitWriter(); }
                _ms += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000L / System.Diagnostics.Stopwatch.Frequency;
                BtcLog.Info("JsonWarm", "writer code prepared: " + _methods + " methods, " + _ms + " ms (background)");
                while (true)
                {
                    Type t;
                    IContractResolver r;
                    lock (Lock)
                    {
                        while (Pending.Count == 0 && !Monitor.Wait(Lock, 20000)) { }
                        if (Pending.Count == 0) { _thread = null; return; }   // idle 20 s: stop, a later load starts a new one
                        t = Pending.Dequeue();
                        r = _resolver;
                    }
                    if (r == null) continue;
                    long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                    Warm(r, t, 0);
                    _ms += (System.Diagnostics.Stopwatch.GetTimestamp() - t1) * 1000L / System.Diagnostics.Stopwatch.Frequency;
                    bool idle; lock (Lock) idle = Pending.Count == 0;
                    if (idle) BtcLog.Info("JsonWarm", "contracts warm: " + _contracts + " contracts, " + _methods + " methods, " + _ms + " ms total (background)");
                }
            }
            catch (Exception e)
            {
                BtcLog.Error("JsonWarm", "worker: " + e.Message);
                lock (Lock) _thread = null;
            }
        }

        // contract of t and of everything reachable from its serialized members (the writer resolves them lazily on the main thread otherwise)
        private static void Warm(IContractResolver r, Type t, int depth)
        {
            if (t == null || depth > 6 || t.IsGenericTypeDefinition || t.IsPointer || t.IsByRef) return;
            JsonContract c;
            try { c = r.ResolveContract(t); _contracts++; }
            catch (Exception) { return; }
            JsonObjectContract oc = c as JsonObjectContract;
            if (oc != null)
            {
                for (int i = 0; i < oc.Properties.Count; i++)
                {
                    JsonProperty p = oc.Properties[i];
                    if (p.Ignored) continue;
                    PrepareGetter(p, t);
                    Next(r, p.PropertyType, depth);
                }
                return;
            }
            JsonArrayContract ac = c as JsonArrayContract;
            if (ac != null) { Next(r, ac.CollectionItemType, depth); return; }
            JsonDictionaryContract dc = c as JsonDictionaryContract;
            if (dc != null) { Next(r, dc.DictionaryKeyType, depth); Next(r, dc.DictionaryValueType, depth); }
        }

        private static void Next(IContractResolver r, Type t, int depth)
        {
            if (t == null || t == typeof(object) || t.IsPrimitive || t == typeof(string)) return;
            lock (Lock) { if (!Seen.Add(t)) return; }
            Warm(r, t, depth + 1);
        }

        private static void PrepareGetter(JsonProperty p, Type owner)
        {
            try
            {
                string n = p.UnderlyingName;
                if (string.IsNullOrEmpty(n)) return;
                PropertyInfo pi = (p.DeclaringType ?? owner).GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                MethodInfo g = pi == null ? null : pi.GetGetMethod(true);
                if (g != null && !g.IsAbstract && !g.ContainsGenericParameters) { RuntimeHelpers.PrepareMethod(g.MethodHandle); _methods++; }
            }
            catch (Exception) { }
        }

        // JIT the serializer write path (Newtonsoft writer + contracts + the game's Brix.IO.Serialization converters), once per session
        private static void JitWriter()
        {
            PrepareAssembly(typeof(JsonSerializer).Assembly, delegate (Type t)
            {
                string ns = t.Namespace ?? "";
                if (ns != "Newtonsoft.Json" && ns != "Newtonsoft.Json.Serialization" && ns != "Newtonsoft.Json.Utilities") return false;
                string n = t.Name;
                return n.IndexOf("Reader", StringComparison.Ordinal) < 0 && n.IndexOf("Bson", StringComparison.Ordinal) < 0 && n.IndexOf("Xml", StringComparison.Ordinal) < 0;
            });
            PrepareAssembly(typeof(GameObjectSerializer).Assembly, delegate (Type t)
            {
                string ns = t.Namespace ?? "";
                return ns.StartsWith("Brix.IO.Serialization", StringComparison.Ordinal);
            });
        }

        private static void PrepareAssembly(Assembly a, Func<Type, bool> want)
        {
            Type[] types;
            try { types = a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }
            BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (int i = 0; i < types.Length; i++)
            {
                Type t = types[i];
                if (t == null || t.ContainsGenericParameters || t.IsInterface || !want(t)) continue;
                MethodInfo[] ms;
                try { ms = t.GetMethods(all); } catch (Exception) { continue; }
                for (int k = 0; k < ms.Length; k++)
                {
                    MethodInfo m = ms[k];
                    if (m.IsAbstract || m.ContainsGenericParameters || (m.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) != 0) continue;
                    try { RuntimeHelpers.PrepareMethod(m.MethodHandle); _methods++; } catch (Exception) { }
                }
            }
        }
    }
}
