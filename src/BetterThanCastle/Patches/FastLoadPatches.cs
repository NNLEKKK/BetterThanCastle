using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Engine;
using Brix.Game;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Performance/FastLoad (read once at start): load waits that cost frames for nothing (ideas from Castle Story Plus, Tricky12321, GPLv3).
    // The waits themselves are BtcLoadBoost.Poll (null = every frame with FastLoad); the JSON warm-up is Features/BtcJsonWarm.cs.

    /// <summary>
    /// GameLoader.LoadingStepsCheckpoint fed the loading bar at most 100 steps per frame (Mathf.Min(100, missing)) and waited a frame
    /// each time, so a checkpoint far behind burned frames only to move the bar. Now one leap: same final progress, no frames lost.
    /// </summary>
    [HarmonyPatch]
    internal static class FastLoadProgressLeapPatch
    {
        private static bool Prepare() { return BtcConfig.FastLoad == null || BtcConfig.FastLoad.Value; }

        private static MethodBase TargetMethod() { return G3Il.MoveNextOf(typeof(GameLoader), "LoadingStepsCheckpoint"); }

        public static int Second(int a, int b) { return b; }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> src)
        {
            MethodInfo min = AccessTools.Method(typeof(Mathf), "Min", new[] { typeof(int), typeof(int) });
            List<CodeInstruction> ins = new List<CodeInstruction>(src);
            int n = 0;
            for (int i = 0; i < ins.Count; i++)
                if (ins[i].Calls(min)) { ins[i].operand = AccessTools.Method(typeof(FastLoadProgressLeapPatch), "Second"); n++; }
            if (n == 0) BtcLog.Error("Hook", "FastLoadProgressLeap: Mathf.Min not found");
            return ins;
        }
    }
    /// <summary>
    /// The terrain of a loading world is drawn by TestWorldConfig3's quick-draw enumerator, stepped ONCE per frame from its Update
    /// (UpdateQuickDraw): ~350 frames for a normal map, most of each frame spent drawing the loading screen. The yields wait for nothing
    /// (it is stepped by hand, yielded objects are ignored), so while a world loads it is stepped until done or 100 ms are used.
    /// </summary>
    [HarmonyPatch]
    internal static class FastLoadQuickDrawPatch
    {
        private const double BudgetMs = 100.0;
        private static FieldInfo _en;
        internal static int Steps, Frames;

        private static bool Prepare() { return (BtcConfig.FastLoad == null || BtcConfig.FastLoad.Value) && TargetMethod() != null; }

        private static MethodBase TargetMethod()
        {
            Type t = AccessTools.TypeByName("Brix.Engine.FlatWorld.Layers.TestWorldConfig3");
            return t == null ? null : AccessTools.Method(t, "UpdateQuickDraw");
        }

        private static bool Prefix(object __instance)
        {
            if (!BtcLoadBoost.Loading) return true;
            try
            {
                if (_en == null) _en = AccessTools.Field(__instance.GetType(), "_quickDrawEnumerator");
                System.Collections.IEnumerator en = _en.GetValue(__instance) as System.Collections.IEnumerator;
                if (en == null) return false;
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                long budget = (long)(BudgetMs * 0.001 * System.Diagnostics.Stopwatch.Frequency);
                if (Frames == 0) _start = t0;
                Frames++;
                do
                {
                    Steps++;
                    if (!en.MoveNext())
                    {
                        _en.SetValue(__instance, null);
                        long ms = (System.Diagnostics.Stopwatch.GetTimestamp() - _start) * 1000 / System.Diagnostics.Stopwatch.Frequency;
                        BtcLoadBoost.Note("quick draw done: " + Steps + " steps in " + Frames + " frames, " + ms + " ms" + BtcTerrainThreads.Stats());
                        Steps = Frames = 0;
                        return false;
                    }
                }
                while (System.Diagnostics.Stopwatch.GetTimestamp() - t0 < budget);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "FastLoadQuickDraw: " + e.Message); return true; }
        }

        private static long _start;
    }
    /// <summary>
    /// The loading-screen curtain (LoadingScreenSlide) eases in with a long exponential tail: a single player start waits for it to close
    /// (~0.9 s) before the host even starts (GameParam.StartGame), and it opens the same way when the game begins. 2.5x faster steps.
    /// </summary>
    [HarmonyPatch(typeof(LoadingScreenSlide), "ComputeIncrement")]
    internal static class FastLoadCurtainPatch
    {
        private static bool Prepare() { return BtcConfig.FastLoad == null || BtcConfig.FastLoad.Value; }

        private static void Postfix(LoadingScreenSlide __instance, ref float __result)
        {
            if (__result == 0f || __instance.Bot == null) return;
            float y = __instance.Bot.anchorMax.y, room = __result > 0f ? 0.5f - y : -y;   // never past the end (a long frame has a big delta time)
            __result *= 2.5f;
            if (Mathf.Abs(__result) > Mathf.Abs(room)) __result = room;
        }
    }

    /// <summary>
    /// Visuals > No loading curtain: the two black halves of LoadingScreenSlide are hidden, the slide itself still runs
    /// (the driver waits for its AnimStart/AnimEnd signals), so the world shows while it loads. Read at each call: switch applies at once.
    /// </summary>
    [HarmonyPatch(typeof(LoadingScreenSlide), "ComputeIncrement")]
    internal static class NoLoadCurtainPatch
    {
        private static void Postfix(LoadingScreenSlide __instance) { Apply(__instance); }

        // the instant calls and the open/close starts also cover the frames before the first animation step
        [HarmonyPatch(typeof(LoadingScreenSlide), "InstantClose")]
        internal static class AtInstantClose { private static void Postfix(LoadingScreenSlide __instance) { Apply(__instance); } }
        [HarmonyPatch(typeof(LoadingScreenSlide), "InstantOpen")]
        internal static class AtInstantOpen { private static void Postfix(LoadingScreenSlide __instance) { Apply(__instance); } }
        [HarmonyPatch(typeof(LoadingScreenSlide), "Close")]
        internal static class AtClose { private static void Postfix(LoadingScreenSlide __instance) { Apply(__instance); } }
        [HarmonyPatch(typeof(LoadingScreenSlide), "Open")]
        internal static class AtOpen { private static void Postfix(LoadingScreenSlide __instance) { Apply(__instance); } }

        internal static void Apply(LoadingScreenSlide s)
        {
            try
            {
                bool show = BtcConfig.NoLoadCurtain == null || !BtcConfig.NoLoadCurtain.Value;
                if (s.Top != null && s.Top.gameObject.activeSelf != show) s.Top.gameObject.SetActive(show);
                if (s.Bot != null && s.Bot.gameObject.activeSelf != show) s.Bot.gameObject.SetActive(show);
            }
            catch (Exception e) { BtcLog.Error("Hook", "NoLoadCurtain: " + e.Message); }
        }
    }
}
