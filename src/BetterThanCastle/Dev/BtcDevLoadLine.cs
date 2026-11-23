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
            }
            catch (Exception e) { BtcLog.Error("LoadLine", e.ToString()); }
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
            Sb.Length = 0; Seen.Clear();
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
