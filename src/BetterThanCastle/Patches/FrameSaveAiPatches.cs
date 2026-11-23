using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using Brix.Components;
using Brix.Engine;
using Brix.Engine.FlatWorld;
using Brix.Engine.Nature;
using Brix.External.Factories;
using Brix.Game.AI;
using Brix.Game.Components;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
    // Frame savers for the AI: pure caches that give the same answers as the game. None changes timing, decisions or
    // anything sent on the wire. Each class applies only when Performance/FrameSavers was on at start.

    /// What the game did: a unit's AnimatorTracker asks the animator for its current state once per tracked state
    /// (~66 per bricktron, twice for a running one) every fixed update: ~30k Unity calls a frame with 100 units.
    /// What the saver does: each layer's state and transition flag is read once per update; every tracked state compares
    /// against it and moves on exactly as before (started, finished, looped, transiting). Runs every fixed update.
    /// Ported from Castle Story Plus (Performance/AnimatorTrackerCache.cs, without AiTickCap).
    [HarmonyPatch(typeof(AnimatorTracker), "BrixFixedUpdate")]
    internal static class FrameSaveAnimatorTracker
    {
        static bool Prepare() { return BtcFrameSave.On; }

        private const int MostLayers = 8;
        private static readonly AnimatorStateInfo[] Infos = new AnimatorStateInfo[MostLayers];
        private static readonly bool[] Read = new bool[MostLayers];
        private static readonly int[] Transition = new int[MostLayers];

        // Private fields of AnimatorStateProgress through DynamicMethod (no ref returns on net35).
        private static readonly Func<AnimatorStateProgress, Animator> GetAnimator = Getter<Animator>("animator");
        private static readonly Func<AnimatorStateProgress, int> GetLayer = Getter<int>("layer");
        private static readonly Func<AnimatorStateProgress, float> GetTime = Getter<float>("time");
        private static readonly Action<AnimatorStateProgress, float> SetTime = Setter("time");

        private static Func<AnimatorStateProgress, T> Getter<T>(string field)
        {
            FieldInfo f = typeof(AnimatorStateProgress).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            DynamicMethod dm = new DynamicMethod("btc_get_" + field, typeof(T), new Type[] { typeof(AnimatorStateProgress) }, typeof(AnimatorStateProgress), true);
            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, f);
            il.Emit(OpCodes.Ret);
            return (Func<AnimatorStateProgress, T>)dm.CreateDelegate(typeof(Func<AnimatorStateProgress, T>));
        }

        private static Action<AnimatorStateProgress, float> Setter(string field)
        {
            FieldInfo f = typeof(AnimatorStateProgress).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            DynamicMethod dm = new DynamicMethod("btc_set_" + field, typeof(void), new Type[] { typeof(AnimatorStateProgress), typeof(float) }, typeof(AnimatorStateProgress), true);
            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, f);
            il.Emit(OpCodes.Ret);
            return (Action<AnimatorStateProgress, float>)dm.CreateDelegate(typeof(Action<AnimatorStateProgress, float>));
        }

        static bool Prefix(List<AnimatorStateProgress> ___States)
        {
            try
            {
                List<AnimatorStateProgress> states = ___States;
                if (states == null) return true; // vanilla throws
                if (states.Count == 0) return false;
                Animator animator = GetAnimator(states[0]);
                for (int i = 0; i < MostLayers; i++) { Read[i] = false; Transition[i] = -1; }
                for (int i = 0; i < states.Count; i++)
                {
                    AnimatorStateProgress state = states[i];
                    int layer = GetLayer(state);
                    if (!ReferenceEquals(GetAnimator(state), animator) || layer < 0 || layer >= MostLayers) { state.Update(); continue; }
                    Step(state, animator, layer);
                }
                return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("FrameSave", "AnimatorTracker: " + e);
                return true;
            }
        }

        /// AnimatorStateProgress.Update with the layer's state read once.
        private static void Step(AnimatorStateProgress state, Animator animator, int layer)
        {
            if (!Read[layer]) { Infos[layer] = animator.GetCurrentAnimatorStateInfo(layer); Read[layer] = true; }
            AnimatorStateInfo info = Infos[layer];
            bool active = info.fullPathHash == state.StateHash;
            state.Started = false;
            state.Finished = false;
            state.Looped = false;
            state.Transiting = false;
            if (state.Running)
            {
                if (!active) { state.Running = false; state.Finished = true; }
                else
                {
                    if (Transition[layer] < 0) Transition[layer] = animator.IsInTransition(layer) ? 1 : 0;
                    if (Transition[layer] == 1) state.Transiting = true;
                    else if (GetTime(state) > Fraction(info.normalizedTime)) state.Looped = true;
                }
            }
            else if (active)
            {
                state.Started = true;
                state.Running = true;
                SetTime(state, Fraction(info.normalizedTime));
            }
        }

        private static float Fraction(float t) { return t - Mathf.Floor(t); }
    }

    /// What the game did: Interaction.CarriedType (hands / back / bag) looked up the factory template by name, its
    /// GameObject and a GetComponent&lt;Grip&gt; on every call; walking units ask it every frame (CanRun, CanClimb, CanStrike).
    /// What the saver does: the answer is remembered per unit together with the prop it was worked out for and worked out
    /// again as soon as the unit carries another prop (reference compare). Carrying nothing answers None as before.
    /// Ported from Castle Story Plus (Performance/CarriedTypeCache.cs).
    [HarmonyPatch(typeof(Interaction), "CarriedType", MethodType.Getter)]
    internal static class FrameSaveCarriedType
    {
        static bool Prepare() { return BtcFrameSave.On; }

        private struct Carried { internal Factory.AssetKey Prop; internal TakeType Type; }

        private const int MostUnits = 4096;
        private static readonly Dictionary<int, Carried> Known = new Dictionary<int, Carried>();

        static bool Prefix(Interaction __instance, Factory.AssetKey ___CarriedProp, ref TakeType __result)
        {
            try
            {
                Factory.AssetKey prop = ___CarriedProp;
                if (prop == null) { __result = TakeType.None; return false; }
                int id = __instance.GetInstanceID();
                Carried carried;
                if (Known.TryGetValue(id, out carried) && ReferenceEquals(carried.Prop, prop)) { __result = carried.Type; return false; }
                TakeType type = Factory.Peek(prop).GetComponent<Grip>().TakeType; // as the game (throws as the game does)
                if (Known.Count >= MostUnits) Known.Clear();
                Known[id] = new Carried { Prop = prop, Type = type };
                __result = type;
                return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("FrameSave", "CarriedType: " + e.Message);
                return true;
            }
        }
    }

    /// What the game did: GameComponentExtender.IsNullOrReleased(GameObject) did a GetComponent&lt;IBrixComponent&gt; (Unity checks
    /// the object's components one by one) on every ask; the AI asks all the time (every working worker's task, every
    /// resource/storage/goal a task search weighs).
    /// What the saver does: the first IBrixComponent is remembered per object, only its Released/Destroyed flags are read
    /// each time. A remembered component that was destroyed is looked up again; a component added later comes after it,
    /// so the first one does not change. Other threads get the game's own check.
    /// Ported from Castle Story Plus (Performance/WorkerTickSavers.cs).
    [HarmonyPatch(typeof(GameComponentExtender), "IsNullOrReleased", new Type[] { typeof(GameObject) })]
    internal static class FrameSaveReleasedCheck
    {
        static bool Prepare()
        {
            if (!BtcFrameSave.On) return false;
            _mainThread = Thread.CurrentThread.ManagedThreadId; // plugin Awake runs on Unity's main thread
            return true;
        }

        private const int MostObjects = 8192;
        private static readonly Dictionary<GameObject, IBrixComponent> First = new Dictionary<GameObject, IBrixComponent>(new SameObject());
        private static int _mainThread = -1;

        static bool Prefix(GameObject go, ref bool __result)
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThread) return true;
            try
            {
                if (go == null) { __result = true; return false; }
                IBrixComponent component;
                if (!First.TryGetValue(go, out component) || (component as UnityEngine.Object) == null)
                {
                    component = go.GetComponent<IBrixComponent>();
                    if (component == null) { First.Remove(go); __result = false; return false; }
                    if (First.Count >= MostObjects) First.Clear();
                    First[go] = component;
                }
                __result = component.Released || component.Destroyed;
                return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("FrameSave", "ReleasedCheck: " + e.Message);
                return true;
            }
        }

        /// By reference: Unity's own comparison asks the engine whether the object still lives.
        private sealed class SameObject : IEqualityComparer<GameObject>
        {
            public bool Equals(GameObject a, GameObject b) { return ReferenceEquals(a, b); }
            public int GetHashCode(GameObject go) { return RuntimeHelpers.GetHashCode(go); }
        }
    }

    /// What the game did: HarvestGoalProvider.GoalCountOfType walked every goal through a Slinq pipeline with two
    /// delegate calls per goal; asked for every task each time idle workers sort tasks and by HasWorkFor.
    /// What the saver does: counts with a plain loop and the same flag test ((type &amp; kind) == kind = Enum.HasFlag): same count.
    /// Ported from Castle Story Plus (Performance/LaborTickSavers.cs, HarvestGoalCount).
    [HarmonyPatch(typeof(HarvestGoalProvider), "GoalCountOfType")]
    internal static class FrameSaveHarvestGoalCount
    {
        static bool Prepare() { return BtcFrameSave.On; }

        static bool Prefix(Dictionary<XYZ, NatureHarvestGoal> ____goals, NatureType type, ref int __result)
        {
            try
            {
                if (____goals == null) return true;
                int accepted = (int)type;
                int count = 0;
                foreach (KeyValuePair<XYZ, NatureHarvestGoal> pair in ____goals)
                {
                    int kind = (int)pair.Value.NatureType;
                    if ((accepted & kind) == kind) count++;
                }
                __result = count;
                return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("FrameSave", "HarvestGoalCount: " + e.Message);
                return true;
            }
        }
    }

    /// What the game did: the distant terrain (LOD) is redrawn in batches: a worker thread works out each section's data
    /// (LodDrawer.InitRender), then the main thread turns every section into a mesh (LodDrawer.Render). The main thread
    /// only waits for the thread to end, not to succeed: when it dies part way, the sections it did not reach hold pooled
    /// arrays of another size and Render runs off their end (index out of range), stopping the whole redraw.
    /// What the saver does: a section is drawn only once its InitRender finished; the others keep the mesh they had until
    /// the next redraw, the rest of the batch is drawn as before. Purely visual, no simulation state.
    /// Ported from Castle Story Plus (Core/TerrainLodFix.cs). The LodUpdater types are internal: found by name.
    [HarmonyPatch]
    internal static class FrameSaveTerrainLod
    {
        private const string Drawer = "Brix.Engine.FlatWorld.LodUpdater+LodDrawer";
        private const string Batch = "Brix.Engine.FlatWorld.LodUpdater+LodBatchDrawer";

        private static MethodBase _initRender, _render, _renderLod;
        private static FieldInfo _lowWorld;

        // Sections whose render data is complete: filled on the worker thread, read on the main thread.
        private static readonly Dictionary<object, bool> Ready = new Dictionary<object, bool>(new SameRef());
        private static bool _warned;

        static bool Prepare()
        {
            if (!BtcFrameSave.On) return false;
            try
            {
                Type drawer = AccessTools.TypeByName(Drawer);
                Type batch = AccessTools.TypeByName(Batch);
                if (drawer == null || batch == null) return false;
                _initRender = AccessTools.Method(drawer, "InitRender");
                _render = AccessTools.Method(drawer, "Render");
                _renderLod = AccessTools.Method(batch, "RenderLod");
                _lowWorld = AccessTools.Field(drawer, "LowWorld");
                if (_initRender == null || _render == null || _renderLod == null || _lowWorld == null)
                {
                    BtcLog.Warn("FrameSave", "TerrainLod: member missing, skipped");
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                BtcLog.Error("FrameSave", "TerrainLod Prepare: " + e.Message);
                return false;
            }
        }

        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return _initRender;
            yield return _render;
            yield return _renderLod;
        }

        // One prefix/postfix/finalizer set applies to all three targets: tell them apart by the method name.
        static bool Prefix(MethodBase __originalMethod, object __instance)
        {
            try
            {
                if (__originalMethod != _render) return true;
                object world = _lowWorld.GetValue(__instance);
                bool ready;
                lock (Ready) ready = world != null && Ready.ContainsKey(world);
                if (!ready && !_warned)
                {
                    _warned = true;
                    BtcLog.Warn("FrameSave", "Terrain LOD: a section was not finished by the worker thread and was not redrawn");
                }
                return ready;
            }
            catch (Exception e)
            {
                BtcLog.Error("FrameSave", "TerrainLod Render: " + e.Message);
                return true;
            }
        }

        // Only reached when the method returned normally (InitRender).
        static void Postfix(MethodBase __originalMethod, object __instance)
        {
            try
            {
                if (__originalMethod != _initRender) return;
                object world = _lowWorld.GetValue(__instance);
                if (world == null) return;
                lock (Ready)
                {
                    if (Ready.Count > 20000) Ready.Clear(); // leak guard
                    Ready[world] = true;
                }
            }
            catch (Exception e)
            {
                BtcLog.Error("FrameSave", "TerrainLod InitRender: " + e.Message);
            }
        }

        // A finalizer, so the batch is forgotten even when drawing it threw. RenderLod's dictionary of drawn sections.
        static Exception Finalizer(MethodBase __originalMethod, object[] __args, Exception __exception)
        {
            try
            {
                if (__originalMethod == _renderLod && __args != null)
                {
                    for (int i = 0; i < __args.Length; i++)
                    {
                        Dictionary<XYZ, FlatWorldColors> worlds = __args[i] as Dictionary<XYZ, FlatWorldColors>;
                        if (worlds == null) continue;
                        lock (Ready)
                            foreach (FlatWorldColors w in worlds.Values) Ready.Remove(w);
                    }
                }
            }
            catch (Exception e)
            {
                BtcLog.Error("FrameSave", "TerrainLod Forget: " + e.Message);
            }
            return __exception;
        }

        private sealed class SameRef : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) { return ReferenceEquals(a, b); }
            public int GetHashCode(object o) { return RuntimeHelpers.GetHashCode(o); }
        }
    }
}
