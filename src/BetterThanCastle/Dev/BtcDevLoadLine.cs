#if DEV
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Engine;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV only: the whole load timeline from the Start click (GameParam.StartGame) to the first frame the game really runs (loading screen
    /// slid away, time scale above 0). Every game signal of the loader is a mark; per segment: wall time, frames, slowest frame, GC count.
    /// Frame-bound segments (many short frames) and CPU-bound ones (few long frames) need different fixes. Writes BTClogs/loadline.txt.
    /// </summary>
    internal static class BtcDevLoadLine
    {
        private sealed class Driver : MonoBehaviour
        {
            private void Update() { Frame(); }
        }

        private static long _t0, _segT0;
        private static int _frames, _gc0;
        private static double _maxFrame;
        private static long _lastFrame;
        private static bool _on, _animEnd;
        /// <summary>Load probes record while the world loads and until the game is playable (the gamemode Lua menus are built after StartStep).</summary>
        internal static bool Window { get { return BtcLoadBoost.Loading || (_on && !_animEnd); } }
        private static float _endAt;
        private static readonly StringBuilder Sb = new StringBuilder();
        private static readonly HashSet<string> Seen = new HashSet<string>();
        private const string Ready = "LevelReady (loading screen starts to slide)";

        internal static void Init()
        {
            try
            {
                GameObject go = new GameObject("BtcDevLoadLine");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.AddComponent<Driver>();
                Harmony h = new Harmony("com.btc.loadline");
                MethodInfo sg = AccessTools.Method(typeof(GameParam), "StartGame");
                if (sg != null) h.Patch(sg, new HarmonyMethod(typeof(BtcDevLoadLine).GetMethod("PreStart", BindingFlags.Static | BindingFlags.NonPublic)));
                // first step of each terrain sub-loop: where the frame-bound step2 time goes
                foreach (string it in new[] { "LoadBasicTerrain", "InitTerrainGravity", "LoadNature", "BuildTerrainMeshes", "LoadGameObjects", "LoadGridGuide", "InitPathfinding" })
                {
                    MethodBase mn = Patches.G3Il.MoveNextOf(typeof(GameLoader), it);
                    if (mn != null) h.Patch(mn, new HarmonyMethod(typeof(BtcDevLoadLine).GetMethod("Pre" + (it == "InitTerrainGravity" || it == "InitPathfinding" ? "Static" : "Iter"), BindingFlags.Static | BindingFlags.NonPublic)));
                }
                // replay snapshot parts (Asset_Map.Disk_Write*: the start-of-match snapshot froze the game ~1.9 s)
                HarmonyMethod tp = new HarmonyMethod(typeof(BtcDevLoadLine).GetMethod("PreTime", BindingFlags.Static | BindingFlags.NonPublic));
                HarmonyMethod tf = new HarmonyMethod(typeof(BtcDevLoadLine).GetMethod("PostTime", BindingFlags.Static | BindingFlags.NonPublic));
                foreach (MethodInfo m in typeof(Brix.Assets.Asset_Map).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    if (m.Name.StartsWith("Disk_Write") && !m.IsGenericMethod) { try { h.Patch(m, tp, tf); } catch (Exception) { } }
                Type gst = AccessTools.TypeByName("GameObjectSerializer"); MethodInfo gs = gst == null ? null : AccessTools.Method(gst, "GetJsonGameState");
                if (gs != null) h.Patch(gs, tp, tf);
                Type sct = AccessTools.TypeByName("SerializationComponent");
                MethodInfo sv = sct == null ? null : AccessTools.Method(sct, "Save");
                if (sv != null) h.Patch(sv, new HarmonyMethod(typeof(BtcDevLoadLine).GetMethod("PreTime", BindingFlags.Static | BindingFlags.NonPublic)), new HarmonyMethod(typeof(BtcDevLoadLine).GetMethod("PostSave", BindingFlags.Static | BindingFlags.NonPublic)));
                else BtcLog.Warn("LoadLine", "SerializationComponent.Save not found");
                if (Environment.GetEnvironmentVariable("BTC_LOADPROF") == "1") HookCosts(h);
            }
            catch (Exception e) { BtcLog.Error("LoadLine", e.ToString()); }
        }

        // BTC_LOADPROF=1: every game-signal handler (GameSignals.SafeInvoke) and every Unity start-up message of the game's components,
        // summed per timeline segment and listed under it (>= 5 ms). Nested calls are listed as "in ..." (already inside an outer one).
        private sealed class Cost { public int N; public long T, Self; }
        private static readonly Dictionary<string, Cost> Costs = new Dictionary<string, Cost>();
        private static readonly Dictionary<MethodBase, string> CostKeys = new Dictionary<MethodBase, string>();
        private static int _depth;

        private static void HookCosts(Harmony h)
        {
            BindingFlags sf = BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo si = AccessTools.Method(typeof(GameSignals), "SafeInvoke", new[] { typeof(Action), typeof(Action<Exception>) });
            if (si != null) h.Patch(si, prefix: new HarmonyMethod(typeof(BtcDevLoadLine).GetMethod("PreCost", sf)), finalizer: new HarmonyMethod(typeof(BtcDevLoadLine).GetMethod("FinSig", sf)));
            else BtcLog.Warn("LoadLine", "GameSignals.SafeInvoke not found");
            // one-argument handlers (LateConnectOnce<T>(signal, f, self): factions, capturables): reference type instances share the code
            foreach (MethodInfo g in typeof(GameSignals).GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
                if (g.Name == "SafeInvoke" && g.IsGenericMethodDefinition && g.GetGenericArguments().Length == 1)
                {
                    try { h.Patch(g.MakeGenericMethod(typeof(object)), prefix: new HarmonyMethod(typeof(BtcDevLoadLine).GetMethod("PreCost", sf)), finalizer: new HarmonyMethod(typeof(BtcDevLoadLine).GetMethod("FinSig1", sf))); }
                    catch (Exception e) { BtcLog.Warn("LoadLine", "SafeInvoke<T1>: " + e.Message); }
                }
            // whole signal (all its handlers, also those connected straight to the Signal, e.g. from Lua)
            Type sig = AccessTools.TypeByName("Brix.External.Signals.Signal");
            MethodInfo inv = sig == null ? null : AccessTools.Method(sig, "Invoke", Type.EmptyTypes);
            if (inv != null) h.Patch(inv, prefix: new HarmonyMethod(typeof(BtcDevLoadLine).GetMethod("PreCost", sf)), finalizer: new HarmonyMethod(typeof(BtcDevLoadLine).GetMethod("FinSignal", sf)));
            // LevelLoaded / LevelReady: run the vanilla invoke list by hand (same enumerator) to time each handler, also the direct ones
            if (inv != null)
            {
                Type bt = sig.BaseType;
                Type et = bt == null ? null : bt.GetGenericTypeDefinition().GetNestedType("SignalInvokeListEnumerator");
                if (et != null)
                {
                    _enumT = et.MakeGenericType(bt.GetGenericArguments());
                    _enumCtor = _enumT.GetConstructors()[0];
                    _enumNext = _enumT.GetMethod("MoveNext");
                    _enumCur = _enumT.GetProperty("Current");
                    _enumDispose = _enumT.GetMethod("Dispose");
                    h.Patch(inv, prefix: new HarmonyMethod(typeof(BtcDevLoadLine).GetMethod("PreOwnInvoke", sf)) { priority = Priority.Last });
                }
            }
            HarmonyMethod pre = new HarmonyMethod(typeof(BtcDevLoadLine).GetMethod("PreCost", sf));
            HarmonyMethod fin = new HarmonyMethod(typeof(BtcDevLoadLine).GetMethod("FinCost", sf));
            List<Type> types = new List<Type>(typeof(GameSignals).Assembly.GetTypes());
            Type tw = AccessTools.TypeByName("Brix.Engine.FlatWorld.Layers.TestWorldConfig3");
            if (tw != null && tw.Assembly != typeof(GameSignals).Assembly) types.AddRange(tw.Assembly.GetTypes());
            string[] msgs = { "Awake", "Start", "OnEnable", "OnStartClient", "OnStartServer", "OnStartLocalPlayer", "OnStartAuthority" };
            int n = 0, fail = 0;
            foreach (Type t in types)
            {
                if (t.IsGenericTypeDefinition || t.ContainsGenericParameters || !typeof(MonoBehaviour).IsAssignableFrom(t)) continue;
                foreach (string msg in msgs)
                {
                    MethodInfo m = t.GetMethod(msg, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                    if (m == null || m.GetMethodBody() == null) continue;
                    try { CostKeys[m] = msg + " " + t.Name; h.Patch(m, prefix: pre, finalizer: fin); n++; }
                    catch (Exception) { fail++; }
                }
            }
            BtcLog.Info("LoadLine", "cost probes: " + n + " start-up messages (" + fail + " failed) + signal handlers");
        }

        private static void PreCost(out long __state)
        {
            __state = 0;
            if (!Window) return;
            _depth++;
            if (_depth < Child.Length) Child[_depth] = 0;
            __state = Stopwatch.GetTimestamp();
        }

        private static Exception FinCost(MethodBase __originalMethod, long __state, Exception __exception)
        {
            if (__state != 0) { string k; CostKeys.TryGetValue(__originalMethod, out k); AddCost(k ?? __originalMethod.Name, __state); }
            return __exception;
        }

        private static Exception FinSig(Action a, long __state, Exception __exception)
        {
            if (__state != 0) AddCost("sig " + DelName(a), __state);
            return __exception;
        }

        private static Exception FinSig1(object[] __args, long __state, Exception __exception)
        {
            if (__state != 0) AddCost("sig1 " + DelName(__args != null && __args.Length > 1 ? __args[1] as Delegate : null), __state);
            return __exception;
        }

        private static Type _enumT;
        private static ConstructorInfo _enumCtor;
        private static MethodInfo _enumNext, _enumDispose;
        private static PropertyInfo _enumCur;

        private static bool PreOwnInvoke(object __instance)
        {
            if (!Window || __instance == null) return true;
            string n = SigName(__instance);
            if (n != "LevelReady" && n != "LevelLoaded") return true;
            object en = _enumCtor.Invoke(new[] { __instance });   // boxed: MoveNext mutates this copy
            try
            {
                while ((bool)_enumNext.Invoke(en, null))
                {
                    object cur = _enumCur.GetValue(en, null);
                    object act = Iface(cur, "IClosure`1").GetProperty("Delegate").GetValue(cur, null);
                    MethodInfo call = Iface(act, "IAction").GetMethod("Invoke", Type.EmptyTypes);
                    string k = "h " + Describe(cur, 0);
                    long t0 = 0;
                    PreCost(out t0);
                    try { call.Invoke(act, null); }
                    catch (TargetInvocationException e) { BtcLog.Warn("LoadLine", "handler " + k + ": " + (e.InnerException ?? e).Message); }
                    finally { if (t0 != 0) AddCost(k, t0); }
                }
            }
            finally { _enumDispose.Invoke(en, null); }
            return false;
        }

        private static Type Iface(object o, string name)
        {
            foreach (Type i in o.GetType().GetInterfaces()) if (i.Name == name) return i;
            throw new InvalidOperationException(o.GetType().Name + " has no " + name);
        }

        private static string Describe(object o, int depth)
        {
            if (o == null) return "null";
            StringBuilder s = new StringBuilder();
            foreach (FieldInfo fi in o.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                object v; try { v = fi.GetValue(o); } catch (Exception) { continue; }
                if (v == null || v is string || v.GetType().IsPrimitive || v is System.Collections.IEnumerable) continue;
                Delegate d = v as Delegate;
                if (d != null)
                {
                    Type t = d.Method.DeclaringType;
                    while (t != null && t.IsNested && t.Name.StartsWith("<", StringComparison.Ordinal)) t = t.DeclaringType;
                    if (t == typeof(GameSignals) && d.Method.Name.Contains("SafeInvoke")) continue;
                    s.Append(t == null ? "?" : t.Name).Append('.').Append(d.Method.Name).Append(' ');
                    if (d.Target != null && !(d.Target is UnityEngine.Object) && depth < 5) s.Append(Describe(d.Target, depth + 1));
                    else if (d.Target is UnityEngine.Object) s.Append('[').Append(d.Target.GetType().Name).Append("] ");
                }
                else if (v is UnityEngine.Object) s.Append('[').Append(v.GetType().Name).Append("] ");
                else if (depth < 5 && !v.GetType().IsEnum) s.Append(Describe(v, depth + 1));   // closures are nested structs (Up, V, A)
                if (s.Length > 140) break;
            }
            return s.ToString();
        }

        private static string SigName(object s)
        {
            if (_sigNames == null)
            {
                _sigNames = new Dictionary<object, string>();
                foreach (FieldInfo fi in typeof(GameSignals).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    try
                    {
                        object gs = fi.GetValue(null); if (gs == null) continue;
                        FieldInfo sf = AccessTools.Field(gs.GetType(), "_signal"); object sv = sf == null ? null : sf.GetValue(gs);
                        if (sv != null && !_sigNames.ContainsKey(sv)) _sigNames[sv] = fi.Name;
                    }
                    catch (Exception) { }
                }
            }
            string n; return _sigNames.TryGetValue(s, out n) ? n : "other";
        }

        private static Dictionary<object, string> _sigNames;
        private static Exception FinSignal(object __instance, long __state, Exception __exception)
        {
            if (__state == 0) return __exception;
            string n = __instance == null ? "other" : SigName(__instance);
            AddCost("SIGNAL " + n, __state);
            return __exception;
        }

        private static string DelName(Delegate a)
        {
            if (a == null) return "?";
            Type t = a.Method.DeclaringType;
            while (t != null && t.IsNested && t.Name.StartsWith("<", StringComparison.Ordinal)) t = t.DeclaringType;
            return (t == null ? "?" : t.Name) + "." + a.Method.Name;
        }

        private static readonly long[] Child = new long[256];

        private static void AddCost(string k, long t0)
        {
            long el = Stopwatch.GetTimestamp() - t0;
            long self = _depth < Child.Length ? el - Child[_depth] : el;
            if (_depth < Child.Length) Child[_depth] = 0;
            _depth--;
            if (_depth > 0) { k = "in " + k; if (_depth < Child.Length) Child[_depth] += el; }
            Cost c; if (!Costs.TryGetValue(k, out c)) Costs[k] = c = new Cost();
            c.N++; c.T += el; c.Self += self;
        }

        private static void FlushCosts()
        {
            if (Costs.Count == 0) return;
            List<KeyValuePair<string, Cost>> l = new List<KeyValuePair<string, Cost>>(Costs);
            Costs.Clear();
            l.Sort((a, b) => b.Value.T.CompareTo(a.Value.T));
            double f = 1000.0 / Stopwatch.Frequency;
            for (int i = 0; i < l.Count && i < 30; i++)
            {
                double ms = l[i].Value.T * f;
                if (ms < 2.0) break;
                Sb.AppendFormat("{0,26}{1,7:F0} ms self {2,5:F0} x{3,-5} {4}", "", ms, l[i].Value.Self * f, l[i].Value.N, l[i].Key).AppendLine();
            }
        }

        private static void PreStart() { Begin("start click"); }

        private static void PreIter(MethodBase __originalMethod) { Mark("begin " + __originalMethod.DeclaringType.Name); }
        private static void PreStatic(MethodBase __originalMethod) { Mark("begin " + __originalMethod.DeclaringType.Name); }

        private sealed class Kind { public int N; public double Ms; }
        private static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>();

        private static void PostSave(long __state, Component __instance)
        {
            double ms = (Stopwatch.GetTimestamp() - __state) * 1000.0 / Stopwatch.Frequency;
            string k = __instance == null ? "?" : __instance.gameObject.name;
            int c = k.IndexOf("(Clone)", StringComparison.Ordinal); if (c > 0) k = k.Substring(0, c);
            Kind v; if (!Kinds.TryGetValue(k, out v)) Kinds[k] = v = new Kind();
            v.N++; v.Ms += ms;
        }

        private static void DumpKinds()
        {
            if (Kinds.Count == 0) return;
            List<KeyValuePair<string, Kind>> l = new List<KeyValuePair<string, Kind>>(Kinds);
            l.Sort((a, b) => b.Value.Ms.CompareTo(a.Value.Ms));
            double sum = 0; int n = 0; foreach (var p in l) { sum += p.Value.Ms; n += p.Value.N; }
            BtcLog.Info("LoadLine", "save objects " + n + " total " + sum.ToString("F0") + " ms");
            for (int i = 0; i < l.Count && i < 12; i++) BtcLog.Info("LoadLine", "  save " + l[i].Key + " x" + l[i].Value.N + " " + l[i].Value.Ms.ToString("F0") + " ms");
            Kinds.Clear();
        }

        private static void PreTime(out long __state) { __state = Stopwatch.GetTimestamp(); }

        private static void PostTime(long __state, MethodBase __originalMethod)
        {
            double ms = (Stopwatch.GetTimestamp() - __state) * 1000.0 / Stopwatch.Frequency;
            if (ms >= 5.0) BtcLog.Info("LoadLine", "time " + __originalMethod.Name + " " + ms.ToString("F0") + " ms");
            if (__originalMethod.Name == "GetJsonGameState") DumpKinds();
        }

        internal static void Begin(string what)
        {
            _t0 = _segT0 = _lastFrame = Stopwatch.GetTimestamp();
            _frames = 0; _maxFrame = 0; _gc0 = GC.CollectionCount(0);
            _on = true; _animEnd = false; _endAt = 0f;
            Sb.Length = 0; Seen.Clear(); Costs.Clear();
            Sb.AppendLine("     t(s)    seg(s) frames  max(ms)  gc  mark");
            Line(what);
            Hook();
        }

        // signals are connected again at the load begin too (the game scene may clear connections made in the menu); Seen drops repeats
        internal static void Hook()
        {
            if (!_on) return;
            try
            {
                C(GameSignals.LoadingStarted, "LoadingStarted");
                C(GameSignals.BeforeTerrainLoaded, "BeforeTerrainLoaded");
                C(GameSignals.TerrainLoaded, "TerrainLoaded (terrain + nature)");
                C(GameSignals.GameObjectsLoaded, "GameObjectsLoaded (meshes + saved objects)");
                C(GameSignals.LevelLoaded, "LevelLoaded (BuildStep end)");
                C(GameSignals.TerrainReady, "TerrainReady (pathfinding)");
                C(GameSignals.LoadUI, "LoadUI (game menu build starts)");
                C(GameSignals.LoadGamemode, "LoadGamemode (Lua menus done)");
                C(GameSignals.GamemodeLoaded, "GamemodeLoaded");
                C(GameSignals.LevelReady, Ready);
                C(GameSignals.PhysicsReady, "PhysicsReady");
                C(GameSignals.SoundEngineReady, "SoundEngineReady");
                GameSignals.Connect(GameSignals.LoadingScreenAnimStart, delegate { Mark(Seen.Contains(Ready) ? "LoadingScreenAnimStart (slide out)" : "menu screen anim start"); }, null);
                GameSignals.Connect(GameSignals.LoadingScreenAnimEnd, delegate { if (!Seen.Contains(Ready)) Mark("menu screen anim end"); else if (Mark("LoadingScreenAnimEnd (slide done)")) _animEnd = true; }, null);
            }
            catch (Exception e) { BtcLog.Error("LoadLine", "hook: " + e.Message); }
        }

        private static void C(GameSignals.GameSignal s, string name) { GameSignals.Connect(s, delegate { Mark(name); }, null); }

        internal static bool Mark(string name)
        {
            if (!_on || !Seen.Add(name)) return false;
            Line(name);
            return true;
        }

        private static void Line(string name)
        {
            long now = Stopwatch.GetTimestamp();
            double f = Stopwatch.Frequency;
            FlushCosts();   // costs of the segment that ends here, listed above its line
            Sb.AppendFormat("{0,9:F2} {1,9:F2} {2,6} {3,8:F0} {4,3}  {5}", (now - _t0) / f, (now - _segT0) / f, _frames, _maxFrame, GC.CollectionCount(0) - _gc0, name).AppendLine();
            _segT0 = now; _frames = 0; _maxFrame = 0;
        }

        private static void Frame()
        {
            if (!_on) return;
            long now = Stopwatch.GetTimestamp();
            double ms = (now - _lastFrame) * 1000.0 / Stopwatch.Frequency;
            _lastFrame = now;
            _frames++;
            if (ms > _maxFrame) _maxFrame = ms;
            if (_animEnd && _endAt == 0f && Time.timeScale > 0f) { Mark("PLAYABLE (screen gone, time runs)"); _endAt = Time.realtimeSinceStartup + 3f; BtcLoadBoost.Note("---- probes from StartStep done to playable:"); BtcDevLoadProbe.Dump(); BtcDevMenuProbe.Dump(); BtcDevLuaFileProbe.Dump(); BtcDevMethodProf.Dump(); }
            if (_endAt > 0f && Time.realtimeSinceStartup >= _endAt) { Line("+3 s after playable"); Write(); }
            if ((now - _t0) / (double)Stopwatch.Frequency > 300.0) { Line("TIMEOUT 300 s"); Write(); }
        }

        private static void Write()
        {
            _on = false;
            try
            {
                string txt = Sb.ToString();
                File.WriteAllText(Path.Combine(BtcDevPaths.LogDir, "loadline.txt"), txt);
                BtcLog.Info("LoadLine", "written loadline.txt");
            }
            catch (Exception e) { BtcLog.Error("LoadLine", e.Message); }
        }
    }
}
#endif
