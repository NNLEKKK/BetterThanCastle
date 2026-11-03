using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Engine;
using Brix.UI.Builder.Menu.Component;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle.Patches
{
    /// <summary>Shared IL/iterator helpers for the G3 loading patches (public: called from patched game code).</summary>
    public static class G3Il
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static BtcLoadBoost.Slicer _slicer;
        private static int _prog;

        /// <summary>MoveNext of the compiler generated iterator of outer.name (mcs "&lt;Name&gt;c__IteratorN" or roslyn "&lt;Name&gt;d__N").</summary>
        public static MethodBase MoveNextOf(Type outer, string name)
        {
            foreach (Type t in outer.GetNestedTypes(All))
            {
                if (!t.Name.StartsWith("<" + name + ">")) continue;
                MethodBase m = t.GetMethod("MoveNext", All);
                if (m != null) return m;
            }
            BtcLog.Error("Hook", "iterator MoveNext not found: " + outer.Name + "." + name);
            return null;
        }

        /// <summary>yield return new WaitForSeconds(1f) -> yield return BtcLoadBoost.Poll() (gate: optional replacement for sites after a HostLoaded check).</summary>
        public static List<CodeInstruction> PollSwap(IEnumerable<CodeInstruction> src, string tag, MethodInfo gate)
        {
            List<CodeInstruction> ins = new List<CodeInstruction>(src);
            MethodInfo poll = AccessTools.Method(typeof(BtcLoadBoost), "Poll");
            int n = 0;
            int lastSite = -1;
            for (int i = 0; i + 1 < ins.Count; i++)
            {
                if (ins[i].opcode != OpCodes.Ldc_R4 || !(ins[i].operand is float) || (float)ins[i].operand != 1f) continue;
                ConstructorInfo c = ins[i + 1].operand as ConstructorInfo;
                if (ins[i + 1].opcode != OpCodes.Newobj || c == null || c.DeclaringType != typeof(WaitForSeconds)) continue;
                MethodInfo target = poll;
                if (gate != null)
                {
                    for (int k = i - 1; k >= 0 && k > i - 30 && k > lastSite; k--)
                    {
                        FieldInfo f = ins[k].operand as FieldInfo;
                        if (f != null && f.Name == "HostLoaded") { target = gate; break; }
                    }
                }
                ins[i].opcode = OpCodes.Nop;
                ins[i].operand = null;
                ins[i + 1].opcode = OpCodes.Call;
                ins[i + 1].operand = target;
                lastSite = i;
                n++;
            }
            if (n == 0) BtcLog.Error("Hook", tag + ": WaitForSeconds(1f) sites not found");
            return ins;
        }

        /// <summary>Time slicing: replaces "if (++steps == 10000) { Invoke(LoadingProgress); yield; steps = 0; }" condition by SliceHit().</summary>
        public static IEnumerable<CodeInstruction> SliceSwap(IEnumerable<CodeInstruction> src, string tag)
        {
            List<CodeInstruction> ins = new List<CodeInstruction>(src);
            int at = -1, cnt = 0;
            for (int i = 0; i < ins.Count; i++)
            {
                if (ins[i].opcode == OpCodes.Ldc_I4 && ins[i].operand is int && (int)ins[i].operand == 10000) { at = i; cnt++; }
            }
            if (cnt != 1) { BtcLog.Error("Hook", tag + ": expected one 10000 compare, found " + cnt); return src; }
            int sig = -1;
            for (int k = at + 1; k < ins.Count && k < at + 12; k++)
            {
                FieldInfo f = ins[k].operand as FieldInfo;
                if (f != null && f.Name == "LoadingProgress" && k + 1 < ins.Count && ins[k + 1].opcode == OpCodes.Call) { sig = k + 1; break; }
            }
            if (sig < 0) { BtcLog.Error("Hook", tag + ": LoadingProgress invoke not found"); return src; }
            // [steps] ldc 10000  ->  [steps] pop; call SliceHit; ldc.i4.1  (the original compare/branch then tests the bool)
            ins[at].opcode = OpCodes.Pop;
            ins[at].operand = null;
            // progress signal is raised by SliceHit itself every 10000 steps (legacy: progress and yield are decoupled)
            ins[sig].opcode = OpCodes.Pop;
            ins[sig].operand = null;
            ins.Insert(at + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(G3Il), "SliceHit")));
            ins.Insert(at + 2, new CodeInstruction(OpCodes.Ldc_I4_1));
            return ins;
        }

        public static bool SliceHit()
        {
            if (++_prog >= 10000) { _prog = 0; GameSignals.Invoke(GameSignals.LoadingProgress); }
            if (_slicer == null) _slicer = new BtcLoadBoost.Slicer();
            if (!_slicer.Due()) return false;
            _slicer.Restart();
            return true;
        }

        /// <summary>Runs inner, then (optionally) Begin before / Mark after.</summary>
        public static IEnumerator Wrap(IEnumerator inner, bool begin, string endMark)
        {
            if (begin) BtcLoadBoost.Begin();
            while (inner.MoveNext()) yield return inner.Current;
            if (endMark != null) BtcLoadBoost.Mark(endMark);
        }

        /// <summary>Insert instrs before index i, moving labels/blocks of the old instruction to the first new one.</summary>
        public static void InsertBefore(List<CodeInstruction> ins, int i, params CodeInstruction[] add)
        {
            add[0].labels.AddRange(ins[i].labels);
            ins[i].labels.Clear();
            add[0].blocks.AddRange(ins[i].blocks);
            ins[i].blocks.Clear();
            ins.InsertRange(i, add);
        }
    }

    [HarmonyPatch(typeof(GameLoader), "LoadNewGameFromGameParams")]
    internal static class GameLoaderLoadNewGamePatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            try
            {
                GameLoaderEarlyUi.Reset();
                BtcMapScale.Prepare();
                BtcSymmetry.Prepare();
            }
            catch (Exception e) { BtcLog.Error("Hook", "GameLoader.LoadNewGameFromGameParams: " + e); }
        }
    }

    /// <summary>Load(): BtcLoadBoost.Begin() at start (first MoveNext).</summary>
    [HarmonyPatch(typeof(GameLoader), "Load")]
    internal static class GameLoaderLoadPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref IEnumerator __result)
        {
            try { __result = G3Il.Wrap(__result, true, null); }
            catch (Exception e) { BtcLog.Error("Hook", "GameLoader.Load: " + e); }
        }
    }

    [HarmonyPatch(typeof(GameLoader), "AwakeStep")]
    internal static class GameLoaderAwakeStepPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref IEnumerator __result)
        {
            try { __result = G3Il.Wrap(__result, false, "AwakeStep done"); }
            catch (Exception e) { BtcLog.Error("Hook", "GameLoader.AwakeStep: " + e); }
        }
    }

    [HarmonyPatch(typeof(GameLoader), "BuildStep")]
    internal static class GameLoaderBuildStepPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref IEnumerator __result)
        {
            try { __result = G3Il.Wrap(__result, false, "BuildStep done"); }
            catch (Exception e) { BtcLog.Error("Hook", "GameLoader.BuildStep: " + e); }
        }
    }

    /// <summary>BuildStep body: Poll + OnBeforeGamemode(0/2) + early client UI gate.</summary>
    [HarmonyPatch]
    internal static class GameLoaderBuildStepMoveNextPatch
    {
        private static MethodBase TargetMethod() { return G3Il.MoveNextOf(typeof(GameLoader), "BuildStep"); }

        private static bool Prepare() { return TargetMethod() != null; }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> src)
        {
            List<CodeInstruction> ins;
            try
            {
                ins = G3Il.PollSwap(src, "GameLoader.BuildStep", AccessTools.Method(typeof(GameLoaderEarlyUi), "GateWait"));
                MethodInfo before = AccessTools.Method(typeof(BtcReplay), "OnBeforeGamemode");
                // vanilla: this.StartLua();  (ldarg.0; ldfld this; call StartLua) -> BtcReplay.OnBeforeGamemode(0) first
                int a = ins.FindIndex(x => (x.opcode == OpCodes.Call || x.opcode == OpCodes.Callvirt) && x.operand is MethodInfo && ((MethodInfo)x.operand).Name == "StartLua");
                if (a >= 2) G3Il.InsertBefore(ins, a - 2, new CodeInstruction(OpCodes.Ldc_I4_0), new CodeInstruction(OpCodes.Call, before));
                else BtcLog.Error("Hook", "GameLoader.BuildStep: StartLua call not found");
                // vanilla: if (NetworkServer.active) { yield return this.LoadGameObjects(originOffset); } -> OnBeforeGamemode(2) right after the branch
                int b = ins.FindIndex(x => (x.opcode == OpCodes.Call || x.opcode == OpCodes.Callvirt) && x.operand is MethodInfo && ((MethodInfo)x.operand).Name == "LoadGameObjects");
                int br = -1;
                for (int k = b - 1; k >= 0 && b >= 0; k--)
                {
                    if (ins[k].opcode == OpCodes.Brfalse || ins[k].opcode == OpCodes.Brfalse_S || ins[k].opcode == OpCodes.Brtrue || ins[k].opcode == OpCodes.Brtrue_S) { br = k; break; }
                }
                if (br >= 0) ins.InsertRange(br + 1, new[] { new CodeInstruction(OpCodes.Ldc_I4_2), new CodeInstruction(OpCodes.Call, before) });
                else BtcLog.Error("Hook", "GameLoader.BuildStep: LoadGameObjects branch not found");
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "GameLoader.BuildStep transpiler: " + e);
                return src;
            }
            return ins;
        }
    }

    [HarmonyPatch(typeof(GameLoader), "StartStep")]
    internal static class GameLoaderStartStepPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref IEnumerator __result)
        {
            try { __result = G3Il.Wrap(__result, false, "StartStep done"); }
            catch (Exception e) { BtcLog.Error("Hook", "GameLoader.StartStep: " + e); }
        }
    }

    [HarmonyPatch]
    internal static class GameLoaderStartStepMoveNextPatch
    {
        private static MethodBase TargetMethod() { return G3Il.MoveNextOf(typeof(GameLoader), "StartStep"); }

        private static bool Prepare() { return TargetMethod() != null; }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> src)
        {
            List<CodeInstruction> ins;
            try
            {
                ins = G3Il.PollSwap(src, "GameLoader.StartStep", null);
                MethodInfo before = AccessTools.Method(typeof(BtcReplay), "OnBeforeGamemode");
                // GameSignals.Invoke(GameSignals.LoadUI) -> skipped when the client already loaded the UI early
                int u = ins.FindIndex(x => x.operand is FieldInfo && ((FieldInfo)x.operand).Name == "LoadUI");
                if (u >= 0 && u + 1 < ins.Count && ins[u + 1].opcode == OpCodes.Call)
                    ins[u + 1].operand = AccessTools.Method(typeof(GameLoaderEarlyUi), "InvokeLoadUi");
                else BtcLog.Error("Hook", "GameLoader.StartStep: LoadUI invoke not found");
                // if (!Neo.IsMultiplayer) StatsTracker.StartRecording(); -> unconditional (legacy)
                int s = ins.FindIndex(x => x.operand is MethodInfo && ((MethodInfo)x.operand).Name == "StartRecording");
                int mp = -1;
                for (int k = s - 1; k >= 0 && s >= 0 && k > s - 5; k--)
                {
                    MethodInfo mi = ins[k].operand as MethodInfo;
                    if (mi != null && mi.Name == "get_IsMultiplayer") { mp = k; break; }
                }
                if (mp >= 0) { ins[mp].opcode = OpCodes.Ldc_I4_0; ins[mp].operand = null; }
                else BtcLog.Error("Hook", "GameLoader.StartStep: IsMultiplayer check before StartRecording not found");
                // LoadGamemode / LevelFirstReady signals
                int g = ins.FindIndex(x => x.operand is FieldInfo && ((FieldInfo)x.operand).Name == "LoadGamemode");
                if (g >= 0) G3Il.InsertBefore(ins, g, new CodeInstruction(OpCodes.Ldc_I4_3), new CodeInstruction(OpCodes.Call, before));
                else BtcLog.Error("Hook", "GameLoader.StartStep: LoadGamemode not found");
                int f = ins.FindIndex(x => x.operand is FieldInfo && ((FieldInfo)x.operand).Name == "LevelFirstReady");
                if (f >= 0) G3Il.InsertBefore(ins, f, new CodeInstruction(OpCodes.Ldc_I4_4), new CodeInstruction(OpCodes.Call, before));
                else BtcLog.Error("Hook", "GameLoader.StartStep: LevelFirstReady not found");
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "GameLoader.StartStep transpiler: " + e);
                return src;
            }
            return ins;
        }
    }

    /// <summary>Time-sliced terrain gravity loop (BtcLoadBoost.Slicer).</summary>
    [HarmonyPatch]
    internal static class GameLoaderInitTerrainGravityPatch
    {
        private static MethodBase TargetMethod() { return G3Il.MoveNextOf(typeof(GameLoader), "InitTerrainGravity"); }

        private static bool Prepare() { return TargetMethod() != null; }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> src)
        {
            try { return G3Il.SliceSwap(src, "GameLoader.InitTerrainGravity"); }
            catch (Exception e) { BtcLog.Error("Hook", "GameLoader.InitTerrainGravity transpiler: " + e); return src; }
        }
    }

    /// <summary>Time-sliced tree loading loop (BtcLoadBoost.Slicer).</summary>
    [HarmonyPatch]
    internal static class GameLoaderLoadTreesPatch
    {
        private static MethodBase TargetMethod() { return G3Il.MoveNextOf(typeof(GameLoader), "LoadTrees"); }

        private static bool Prepare() { return TargetMethod() != null; }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> src)
        {
            try { return G3Il.SliceSwap(src, "GameLoader.LoadTrees"); }
            catch (Exception e) { BtcLog.Error("Hook", "GameLoader.LoadTrees transpiler: " + e); return src; }
        }
    }

    /// <summary>Client loads the UI while waiting for the host (legacy "early client UI"). Called from the BuildStep/StartStep transpilers.</summary>
    public static class GameLoaderEarlyUi
    {
        private static bool _uiPreloaded;
        private static bool _done;

        public static void Reset() { _uiPreloaded = false; _done = false; }

        /// <summary>Replaces the 1s poll of the "wait for HostLoaded" loop.</summary>
        public static object GateWait()
        {
            try
            {
                if (!_done && !NetworkServer.active && Neo.IsMultiplayer && !GameParam.instance.HostLoaded)
                {
                    _done = true;
                    return EarlyUi();
                }
            }
            catch (Exception e) { BtcLog.Error("Hook", "GameLoader.GateWait: " + e); }
            return BtcLoadBoost.Poll();
        }

        private static IEnumerator EarlyUi()
        {
            if (GameLoader._instance != null && GameLoader._instance.loadingScreen != null)
                GameLoader._instance.loadingScreen.SetLoadingTitle("##loading");
            BtcLoadBoost.Note("early client UI: host not loaded yet, loading UI while waiting");
            yield return new WaitForSeconds(0.3f);
            BtcLoadBoost.Note("early client UI: network flushed, starting UI");
            SafeInvoke(GameSignals.LoadUI);
            int frames = 0;
            while (LuaMenuComponent.CoroutineCount != 0)
            {
                frames++;
                yield return new WaitForEndOfFrame();
            }
            _uiPreloaded = true;
            BtcLoadBoost.Mark("early client UI loaded, frames=" + frames);
        }

        /// <summary>Replaces GameSignals.Invoke(GameSignals.LoadUI) in StartStep.</summary>
        public static void InvokeLoadUi(GameSignals.GameSignal gs)
        {
            if (_uiPreloaded)
            {
                BtcLoadBoost.Mark("LoadUI skipped (client loaded it early)");
                return;
            }
            SafeInvoke(gs);
        }

        /// <summary>Replay playback has no firefly nest, so Lua UI loading may throw; swallow only then, else vanilla behaviour.</summary>
        private static void SafeInvoke(GameSignals.GameSignal gs)
        {
            try { GameSignals.Invoke(gs); }
            catch (Exception e)
            {
                if (!BtcReplay.Loading && !BtcReplay.Running) throw;
                BtcLog.Error("Loader", "LoadUI threw during replay (swallowed): " + e.Message);
            }
        }
    }
}
