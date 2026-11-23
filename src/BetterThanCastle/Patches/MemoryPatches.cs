using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Engine;
using Brix.External.Signals;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Lua;
using Brix.Network;
using Brix.UI.Builder.Lui;
using Brix.UI.Builder.Menu;
using Brix.UI.Builder.Menu.Component;
using Brix.UI.GameSelector;
using Brix.Lifecycle.Pooling;
using Brix.Utils;
using Brix.Utils.Collections;
using HarmonyLib;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace BetterThanCastle.Patches
{
    // Memory fixes, ported from Castle Story Plus by Tricky12321 (GPLv3, CastleStoryPlus/Memory). Every class applies only
    // when Performance/MemoryFixes was on at start (Prepare). None changes what is sent on the wire.

    // ---------------------------------------------------------------- MoonSharp
    /// Lua value heap starts small (BtcMemFix.Init); grow it before an Allocate would run past the end.
    [HarmonyPatch(typeof(HeapAllocatedDynValue), "Allocate", new Type[0])]
    internal static class MemLuaHeapAllocatePatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static void Prefix(int ____size, DynValue[] ____heapAllocatedDynValue, Stack<int> ____emptySlots)
        {
            if (____emptySlots.Count == 0 && ____size >= ____heapAllocatedDynValue.Length) BtcMemFix.EnsureLuaHeap(1, ____emptySlots);
        }
    }

    [HarmonyPatch(typeof(HeapAllocatedDynValue), "Allocate", new Type[] { typeof(int[]) })]
    internal static class MemLuaHeapAllocateManyPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static void Prefix(int[] array, int ____size, DynValue[] ____heapAllocatedDynValue, Stack<int> ____emptySlots)
        {
            if (array != null && ____size + Math.Max(0, array.Length - ____emptySlots.Count) > ____heapAllocatedDynValue.Length)
                BtcMemFix.EnsureLuaHeap(array.Length, ____emptySlots);
        }
    }

    /// Every MoonSharp Processor (one per script and per Lua coroutine) made 131,072 call frames (15 MB) + 131,072 values
    /// (3 MB) at once. Now 8,192 frames + 32,768 values (< 2 MB): far deeper than the game's Lua goes.
    [HarmonyPatch]
    internal static class MemLuaStackPatch
    {
        const int GameSlots = 131072;
        static bool Prepare() { return BtcMemFix.On; }
        static IEnumerable<MethodBase> TargetMethods()
        {
            Type p = typeof(Script).Assembly.GetType("MoonSharp.Interpreter.Execution.VM.Processor");
            return p.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Cast<MethodBase>();
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            int changed = 0;
            for (int i = 0; i < code.Count - 1; i++)
            {
                ConstructorInfo ctor = code[i + 1].operand as ConstructorInfo;
                if (!code[i].LoadsConstant(GameSlots) || code[i + 1].opcode != OpCodes.Newobj || ctor == null) continue;
                Type st = ctor.DeclaringType;
                if (!st.IsGenericType || !st.Name.StartsWith("FastStack")) continue;
                string arg = st.GetGenericArguments()[0].Name;
                if (arg == "CallStackItem") { code[i] = new CodeInstruction(OpCodes.Ldc_I4, 8192).MoveLabelsFrom(code[i]); changed++; }
                else if (arg == "DynValue") { code[i] = new CodeInstruction(OpCodes.Ldc_I4, 32768).MoveLabelsFrom(code[i]); changed++; }
            }
            if (changed != 2) BtcLog.Error("Hook", "MemLuaStack: " + changed + " of 2 stacks found in " + __originalMethod);
            return code;
        }
    }

    /// A Lua menu component destroyed with its scene only forgot its owned script; it stayed alive with all its tables,
    /// closures and panels, and its session-wide hooks kept running. Close it the way Unload does.
    [HarmonyPatch(typeof(LuaMenuComponent), "Release")]
    internal static class MemLuaMenuReleasePatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static void Prefix(LuaMenuComponent __instance, out Script __state)
        {
            __state = __instance._isOwnerScriptOwner ? __instance._ownerScript : null;
        }
        static void Postfix(LuaMenuComponent __instance, Script __state)
        {
            Script script = __state;
            if (script == null || !script.IsAlive) return;
            try
            {
                script.OnScriptKilled -= __instance._OnScriptKilled;
                LuaRegistry.DishookScript(script);
                LuiBuilder.MapHelper.RemoveAllAtScript(script);
                Script.Kill(ref script);
            }
            catch (Exception e) { BtcLog.Warn("MemFix", "close Lua script of '" + __instance.Path + "': " + e.Message); }
        }
    }

    /// Closures of scripts without hk_OnScriptKilled stay connected to C# hooks after their script dies, and the first
    /// dead one throws and ends the whole hook call (GameMenu's listener after it never runs). Disconnect them on kill.
    [HarmonyPatch(typeof(LuaHookBase), "HookDisconnectWhen")]
    internal static class MemLuaHookKillPatch
    {
        struct Conn { public LuaHookBase Hook; public Closure Act; }
        static readonly Dictionary<Script, List<Conn>> Conns = new Dictionary<Script, List<Conn>>();
        static bool Prepare() { return BtcMemFix.On; }
        static void Postfix(LuaHookBase hook, Closure act)
        {
            try
            {
                Script script = act != null ? act.OwnerScript : null;
                if (script == null || hook == null) return;
                if (script.Globals.Get("hk_OnScriptKilled").ToObject<LuaHook>() != null) return;
                List<Conn> list;
                if (!Conns.TryGetValue(script, out list))
                {
                    list = new List<Conn>();
                    Conns[script] = list;
                    script.OnScriptKilled += OnKilled;
                }
                list.Add(new Conn { Hook = hook, Act = act });
            }
            catch (Exception e) { BtcLog.Error("MemFix", "hook track: " + e.Message); }
        }
        internal static void Forget(LuaHookBase hook, Closure act)
        {
            Script script = act != null ? act.OwnerScript : null;
            List<Conn> list;
            if (script == null || !Conns.TryGetValue(script, out list)) return;
            for (int i = list.Count - 1; i >= 0; i--)
                if (ReferenceEquals(list[i].Hook, hook) && ReferenceEquals(list[i].Act, act)) list.RemoveAt(i);
        }
        static void OnKilled(Script script)
        {
            script.OnScriptKilled -= OnKilled;
            List<Conn> list;
            if (!Conns.TryGetValue(script, out list)) return;
            Conns.Remove(script);
            foreach (Conn c in list)
            {
                try { LuaHookBase.DisconnectFromCLR(c.Hook, c.Act); } catch (Exception e) { BtcLog.Warn("MemFix", "hook disconnect: " + e.Message); }
            }
        }
    }

    [HarmonyPatch(typeof(LuaHookBase), "DisconnectFromCLR")]
    internal static class MemLuaHookDisconnectPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static void Postfix(LuaHookBase hook, Closure act) { MemLuaHookKillPatch.Forget(hook, act); }
    }

    /// UnregisterMenu removed the newest id instead of the menu's own: every closed Lua menu stayed in the id map.
    [HarmonyPatch(typeof(LuaIdUtility), "UnregisterMenu")]
    internal static class MemLuaMenuIdPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static bool Prefix(IMenu menu)
        {
            try
            {
                if (menu.LuaId == 0) return false;
                LuaIdUtility.EntryMap.Remove(menu.LuaId);
                menu.LuaId = 0;
                BaseMenu bm = menu as BaseMenu;
                if (bm != null && bm.Panels != null)
                    foreach (IPanel p in bm.Panels)
                        if (p != null && p.LuaId != 0) { LuaIdUtility.EntryMap.Remove(p.LuaId); p.LuaId = 0; }
                return false;
            }
            catch (Exception e) { BtcLog.Error("MemFix", "UnregisterMenu: " + e.Message); return true; }
        }
    }

    // ---------------------------------------------------------------- game leaks
    /// Analytics server is gone: every 10 s the failed queue was re-sent and every other try made ~21 MB of garbage.
    [HarmonyPatch(typeof(GA_Queue), "AddItem")]
    internal static class MemAnalyticsAddPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static bool Prefix() { return false; }
    }

    [HarmonyPatch(typeof(GA_Queue), "ForceSubmit")]
    internal static class MemAnalyticsSubmitPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static bool Prefix() { return false; }
    }

    /// Rejected goals filled a never-read error store; keep only the visible effect (temporary thought of direct errors).
    [HarmonyPatch(typeof(AIErrorsStore), "AddError")]
    internal static class MemAiErrorsPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static bool Prefix(AIErrorsStore __instance, AIError error, Labor labor)
        {
            try
            {
                if (__instance.DirectErrors.Contains(error)) labor.SetTemporaryThought(2f, error.thought);
                return false;
            }
            catch (Exception e) { BtcLog.Error("MemFix", "AddError: " + e.Message); return true; }
        }
    }

    /// Every removed voxel went into a never-read "whole game" list, and a client's delta list is never sent (only the
    /// server coroutine drains it). Same bytes on the wire: the server still queues the delta.
    [HarmonyPatch(typeof(UnetDeblocMessageHandler), "AddDeblocToSendQueue")]
    internal static class MemDeblocListPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static bool Prefix(UnetDeblocMessageHandler __instance, XYZ position, int id)
        {
            if (NetworkServer.active) __instance.DeblocDataListDelta.Add(new UnetDeblocMessageHandler.DeblocData(position, (short)id));
            return false;
        }
    }

    /// A pooled highlight built a new combined mesh on every reuse and never destroyed the old one.
    [HarmonyPatch(typeof(Highlight), "CombinedMeshFromChildren")]
    internal static class MemHighlightMeshPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static void Prefix(MeshFilter filter)
        {
            Mesh prev = filter != null ? filter.sharedMesh : null;
            // runtime-made meshes have negative instance ids (5.6); never destroy an asset
            if (prev != null && prev.GetInstanceID() < 0) UnityEngine.Object.Destroy(prev);
        }
    }

    /// Pie menu segments got a new material on every build; replaced and destroyed ones were never destroyed.
    [HarmonyPatch]
    internal static class MemRadialMaterialPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (Type t in typeof(RadialLayoutElement).Assembly.GetTypes())
            {
                if (!typeof(RadialLayoutElement).IsAssignableFrom(t)) continue;
                MethodInfo m = t.GetMethod("InitBgRawImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (m != null) yield return m;
            }
        }
        static void Postfix(RadialLayoutElement __instance)
        {
            try
            {
                BtcRuntimeMaterials mats = __instance.GetComponent<BtcRuntimeMaterials>();
                if (mats == null) mats = __instance.gameObject.AddComponent<BtcRuntimeMaterials>();
                mats.Track(__instance.bgRawImage);
                RadialLayoutElementCore core = __instance as RadialLayoutElementCore;
                if (core != null) { mats.Track(core.bgMidRawImage); mats.Track(core.bgTopRawImage); }
            }
            catch (Exception e) { BtcLog.Error("MemFix", "radial mat: " + e.Message); }
        }
    }

    /// Destroys runtime materials of a few UI images when replaced or when the owner goes.
    internal class BtcRuntimeMaterials : MonoBehaviour
    {
        private readonly Dictionary<Graphic, Material> _tracked = new Dictionary<Graphic, Material>();

        internal void Track(Graphic g)
        {
            if (g == null) return;
            Material cur = g.material;
            // the shared default UI material must never be tracked: destroying it breaks every UI drawn with it
            if (IsDefault(cur)) return;
            Material prev;
            if (_tracked.TryGetValue(g, out prev) && prev != cur) Kill(prev);
            _tracked[g] = cur;
        }

        private void OnDestroy()
        {
            foreach (Material m in _tracked.Values) Kill(m);
            _tracked.Clear();
        }

        private static void Kill(Material m)
        {
            if (m != null && m.GetInstanceID() < 0 && !IsDefault(m)) Destroy(m);
        }

        private static bool IsDefault(Material m)
        {
            return m == null || m == Graphic.defaultGraphicMaterial || m == Canvas.GetDefaultCanvasMaterial();
        }
    }

    /// Leaving a game kept every faction as a key of ProjectDatabase.All and pending switch-task cooldowns.
    [HarmonyPatch(typeof(ProjectDatabase), "ClearAll")]
    internal static class MemLeaveStaticsPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static void Postfix()
        {
            try { ProjectDatabase.All.Clear(); ConflictResolver.SwitchTaskCooldowns.Clear(); }
            catch (Exception e) { BtcLog.Error("MemFix", "ClearAll: " + e.Message); }
        }
    }

    // ---------------------------------------------------------------- signals kept forever
    /// Replaces the discarded result of every 'signal.Connect(...)' (call + pop) with a call that keeps it.
    internal static class MemKeptConnections
    {
        internal static IEnumerable<CodeInstruction> KeepDiscarded(IEnumerable<CodeInstruction> instructions, MethodInfo keep, bool passThis, string who)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            int n = 0;
            for (int i = 0; i < code.Count - 1; i++)
            {
                MethodInfo m = code[i].operand as MethodInfo;
                if (m == null || m.ReturnType != typeof(SignalConnection) || IsGameSignals(m.DeclaringType) || code[i + 1].opcode != OpCodes.Pop) continue;
                List<CodeInstruction> rep = new List<CodeInstruction>();
                if (passThis) rep.Add(new CodeInstruction(OpCodes.Ldarg_0));
                rep.Add(new CodeInstruction(OpCodes.Call, keep));
                rep[0].labels.AddRange(code[i + 1].labels);
                rep[0].blocks.AddRange(code[i + 1].blocks);
                code.RemoveAt(i + 1);
                code.InsertRange(i + 1, rep);
                n++;
            }
            if (n == 0) BtcLog.Error("Hook", "MemKeptConnections: no discarded Connect found in " + who);
            return code;
        }

        // GameSignals are cleared with every scene already
        private static bool IsGameSignals(Type type)
        {
            for (Type t = type; t != null; t = t.DeclaringType) if (t == typeof(GameSignals)) return true;
            return false;
        }
    }

    /// UIGameObserver's world editor objects, drag handles and dynamic object lists connect to static signals and never
    /// disconnect: keep the connections and drop them when the observer is destroyed with its scene.
    [HarmonyPatch]
    internal static class MemObserverSignalsPatch
    {
        static readonly List<SignalConnection> Conns = new List<SignalConnection>();
        static bool Prepare() { return BtcMemFix.On; }
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(UIGameObserver.WorldEditorObjects), "ConnectToSignals2");
            yield return AccessTools.Method(typeof(UIGameObserver.Handles), "ConnectBaseSignals");
            yield return AccessTools.Method(typeof(UIGameObserver.DynamicObjects), "ConnectBaseSignals");
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            return MemKeptConnections.KeepDiscarded(instructions, AccessTools.Method(typeof(MemObserverSignalsPatch), "Keep"), false, __originalMethod.DeclaringType.Name + "." + __originalMethod.Name);
        }
        static void Keep(SignalConnection c) { Conns.Add(c); }
        internal static void DisconnectAll()
        {
            foreach (SignalConnection c in Conns) { try { c.Disconnect(); } catch { } }
            Conns.Clear();
        }
    }

    [HarmonyPatch(typeof(UIGameObserver), "OnDestroy")]
    internal static class MemObserverDestroyPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static void Postfix() { MemObserverSignalsPatch.DisconnectAll(); }
    }

    /// Each game's overhead parenter connected to the static ParentMe signal and never disconnected.
    [HarmonyPatch(typeof(OverheadParenter), "Awake")]
    internal static class MemOverheadParenterPatch
    {
        static readonly Dictionary<OverheadParenter, SignalConnection> Conns = new Dictionary<OverheadParenter, SignalConnection>();
        static readonly List<OverheadParenter> Gone = new List<OverheadParenter>();
        static bool Prepare() { return BtcMemFix.On; }
        static bool Prefix(OverheadParenter __instance)
        {
            try
            {
                Gone.Clear();
                foreach (KeyValuePair<OverheadParenter, SignalConnection> p in Conns)
                    if (p.Key == null) { p.Value.Disconnect(); Gone.Add(p.Key); }
                for (int i = 0; i < Gone.Count; i++) Conns.Remove(Gone[i]);
                Gone.Clear();
                Conns[__instance] = OverheadDisplayDriver.ParentMe.Connect(__instance.ParentOverhead);
                return false;
            }
            catch (Exception e) { BtcLog.Error("MemFix", "OverheadParenter: " + e.Message); return true; }
        }
    }

    /// Highlighter: every hover connected to the block's "released" signal and kept it; per-position entries stayed for
    /// every voxel ever highlighted (all dig zones).
    [HarmonyPatch(typeof(Highlighter), "HighlightAt")]
    internal static class HighlighterKeep
    {
        internal static readonly Dictionary<int, SignalConnection> ByRequest = new Dictionary<int, SignalConnection>();
        static bool Prepare() { return BtcMemFix.On; }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return MemKeptConnections.KeepDiscarded(instructions, AccessTools.Method(typeof(HighlighterKeep), "Keep"), true, "Highlighter.HighlightAt");
        }
        static void Keep(SignalConnection c, Highlighter h) { ByRequest[h.currentID] = c; }
    }

    [HarmonyPatch(typeof(Highlighter), "Request")]
    internal static class MemHighlighterRequestPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static void Prefix(int i)
        {
            SignalConnection c;
            if (HighlighterKeep.ByRequest.TryGetValue(i, out c)) { HighlighterKeep.ByRequest.Remove(i); try { c.Disconnect(); } catch { } }
        }
    }

    [HarmonyPatch(typeof(Highlighter), "_RemoveHighlight")]
    internal static class MemHighlighterPosPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static void Prefix(Highlight highlight, out XYZ __state) { __state = highlight != null ? highlight.TargetPosition : default(XYZ); }
        static void Postfix(Highlighter __instance, Highlight highlight, XYZ __state)
        {
            if (highlight == null) return;
            try
            {
                List<GameObject> list;
                if (__instance.HighlightsByPos.TryGetValue(__state, out list) && list.Count == 0)
                {
                    __instance.HighlightsByPos.Remove(__state);
                    list.Dispose<GameObject>();
                }
            }
            catch (Exception e) { BtcLog.Error("MemFix", "highlight pos: " + e.Message); }
        }
    }

    // ---------------------------------------------------------------- adjective sets, selectables, litter, LOD, loggers
    /// Every DicoAdjectif made two dictionaries (field initializer + constructor) and a StringWriter used only for Unity
    /// serialization; ~70 000 alive. Keep only the constructor's dictionary; the writer is made on first serialize.
    [HarmonyPatch]
    internal static class MemAdjectiveSlimPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Constructor(typeof(DicoAdjectif), Type.EmptyTypes);
            yield return AccessTools.Constructor(typeof(DicoAdjectif), new[] { typeof(int) });
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            ConstructorInfo dict = AccessTools.Constructor(typeof(Dictionary<Type, Adjectif>), Type.EmptyTypes);
            ConstructorInfo writer = AccessTools.Constructor(typeof(StringWriter), Type.EmptyTypes);
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            bool d = false, w = false;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].opcode != OpCodes.Newobj) continue;
                if (!d && Equals(code[i].operand, dict)) { d = true; code[i] = new CodeInstruction(OpCodes.Ldnull).MoveLabelsFrom(code[i]); }
                else if (!w && Equals(code[i].operand, writer)) { w = true; code[i] = new CodeInstruction(OpCodes.Ldnull).MoveLabelsFrom(code[i]); }
            }
            // both found or nothing changed: a half change (dictionary nulled without a constructor dictionary) would break
            if (!d || !w)
            {
                BtcLog.Error("Hook", "MemAdjectiveSlim: field initializers not found in " + __originalMethod);
                return instructions;
            }
            return code;
        }
    }

    [HarmonyPatch(typeof(DicoAdjectif), "OnBeforeSerialize")]
    internal static class MemAdjectiveWriterPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static void Prefix(DicoAdjectif __instance) { if (__instance.stringWriter == null) __instance.stringWriter = new StringWriter(); }
    }

    /// Each Unity clone of a DicoAdjectif kept its own ~2 KB copy of the same JSON text (22 MB); share the cached one.
    [HarmonyPatch(typeof(DicoAdjectif), "OnAfterDeserialize")]
    internal static class MemAdjectiveTextPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static void Postfix(DicoAdjectif __instance)
        {
            string md5 = __instance.md5;
            DicoAdjectif cached;
            if (md5 == null || DicoAdjectif.deserializeCache == null || !DicoAdjectif.deserializeCache.TryGetValue(md5, out cached)) return;
            if (cached == null || ReferenceEquals(cached, __instance) || ReferenceEquals(cached.adjectives, __instance.adjectives)) return;
            if (cached.adjectives == __instance.adjectives) { __instance.adjectives = cached.adjectives; __instance.md5 = cached.md5; }
        }
    }

    /// UIGameSelector.Unregister had its test inverted and never removed anything: the list kept every selectable of the
    /// session (destroyed ones too, ~66 MB) and each registration searched all of it.
    [HarmonyPatch(typeof(UIGameSelector), "Unregister")]
    internal static class MemSelectableUnregisterPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static bool Prefix(UISelectable selectable)
        {
            if (UIGameSelector.instance == null) return false; // scene teardown: nothing left to unregister from
            try
            {
                UIGameSelector.Unhover(selectable);
                UIGameSelector.Unselect(selectable);
                List<UISelectable> list = UIGameSelector.instance.RegisteredList;
                for (int i = list.Count - 1; i >= 0; i--)
                    if (ReferenceEquals(list[i], selectable)) { list.RemoveAt(i); break; }
                return false;
            }
            catch (Exception e) { BtcLog.Info("MemFix", "Unregister at teardown: " + e.Message); return false; }
        }
    }

    /// Destroyed (not pooled) selectables never unregister: prune them each time the list has doubled.
    [HarmonyPatch(typeof(UIGameSelector), "Register")]
    internal static class MemSelectableRegisterPatch
    {
        const int MinCheck = 1024;
        static readonly Predicate<UISelectable> Destroyed = s => s == null;
        static int _checkAt = MinCheck;
        static bool Prepare() { return BtcMemFix.On; }
        static void Postfix()
        {
            UIGameSelector sel = UIGameSelector.instance;
            if (ReferenceEquals(sel, null)) return;
            List<UISelectable> list = sel.RegisteredList;
            if (list.Count * 4 < _checkAt) _checkAt = Math.Max(MinCheck, list.Count * 2);
            if (list.Count < _checkAt) return;
            list.RemoveAll(Destroyed);
            _checkAt = Math.Max(MinCheck, list.Count * 2);
        }
    }

    [HarmonyPatch(typeof(Architecte), "LeaveGame")]
    internal static class MemSelectableLeavePatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static void Postfix()
        {
            UIGameSelector sel = UIGameSelector.instance;
            if (!ReferenceEquals(sel, null)) { sel.RegisteredList.Clear(); sel.RegisteredList.TrimExcess(); }
        }
    }

    /// Cleanup task: RemoveOldGoals never emptied its _toRemoveGO list (grew with every litter ever picked up).
    [HarmonyPatch(typeof(LitterGoalProvider), "RemoveOldGoals")]
    internal static class MemLitterGoalPatch
    {
        static bool Prepare() { return BtcMemFix.On; }
        static void Postfix(LitterGoalProvider __instance) { __instance._toRemoveGO.Clear(); }
    }

    /// LOD array pool keeps the first full-map build (~54 MB): trim after a large build (Brix.Core, reflection).
    [HarmonyPatch]
    internal static class MemLodPoolPatch
    {
        const int LargeBuild = 64;
        static int _dirtyBefore;
        static IEnumerator _started;
        static bool _startedLarge;
        static bool Prepare() { return BtcMemFix.On && Target() != null; }
        static MethodBase TargetMethod() { return Target(); }
        static MethodBase Target()
        {
            Type t = AccessTools.TypeByName("Brix.Engine.FlatWorld.LodUpdater");
            if (t == null) return null;
            BtcMemFix.LodWork = AccessTools.Field(t, "_work");
            BtcMemFix.LodDirty = AccessTools.Field(t, "_dirtyMvs");
            return BtcMemFix.LodWork != null && BtcMemFix.LodDirty != null ? AccessTools.Method(t, "Update") : null;
        }
        static void Prefix(object __instance, out IEnumerator __state)
        {
            __state = BtcMemFix.LodWork.GetValue(__instance) as IEnumerator;
            ICollection dirty = BtcMemFix.LodDirty.GetValue(__instance) as ICollection;
            _dirtyBefore = dirty != null ? dirty.Count : 0;
        }
        static void Postfix(object __instance, IEnumerator __state)
        {
            try
            {
                IEnumerator work = BtcMemFix.LodWork.GetValue(__instance) as IEnumerator;
                if (__state == null && work != null) { _started = work; _startedLarge = _dirtyBefore >= LargeBuild; return; }
                if (__state != null && work == null)
                {
                    bool large = !ReferenceEquals(__state, _started) || _startedLarge;
                    _started = null;
                    if (large) BtcMemFix.TrimLodPool();
                }
            }
            catch (Exception e) { BtcLog.Error("MemFix", "LOD pool: " + e.Message); }
        }
    }

    /// LodUpdater.Pool.ClearAll emptied the static pools and then forgot them: from the 2nd world on they were never
    /// emptied again (previous world's LOD arrays kept). Empty them and keep them registered.
    [HarmonyPatch]
    internal static class MemLodClearAllPatch
    {
        static FieldInfo _pools;
        static MethodInfo _clear;
        static bool Prepare() { return BtcMemFix.On && TargetMethod() != null; }
        static MethodBase TargetMethod()
        {
            Type t = AccessTools.TypeByName("Brix.Engine.FlatWorld.LodUpdater+Pool");
            if (t == null) return null;
            _pools = AccessTools.Field(t, "_pools");
            _clear = AccessTools.Method(t, "Clear", Type.EmptyTypes);
            return _pools != null && _clear != null ? AccessTools.Method(t, "ClearAll") : null;
        }
        static bool Prefix()
        {
            try
            {
                IList pools = _pools.GetValue(null) as IList;
                if (pools == null) return true;
                for (int i = 0; i < pools.Count; i++) _clear.Invoke(pools[i], null);
                return false;
            }
            catch (Exception e) { BtcLog.Error("MemFix", "LOD ClearAll: " + e.Message); return true; }
        }
    }

    /// Each generic class made a BrixLogger per closed type named with assembly-qualified type arguments (13,574 loggers,
    /// 21.5 MB of names kept by log4net). Cut the type arguments: levels are set by namespace, so logging is unchanged.
    [HarmonyPatch]
    internal static class MemLoggerNamePatch
    {
        static bool Prepare() { return BtcMemFix.On && TargetMethod() != null; }
        static MethodBase TargetMethod()
        {
            Type t = AccessTools.TypeByName("Brix.Utils.Logging.BrixLogger");
            return t != null ? AccessTools.Constructor(t, new[] { typeof(string) }) : null;
        }
        static void Prefix(ref string name)
        {
            if (name == null) return;
            int a = name.IndexOf('[');
            if (a > 0) name = name.Substring(0, a);
        }
    }
}
