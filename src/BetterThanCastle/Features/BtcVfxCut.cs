using System;
using System.Collections.Generic;
using Brix.Game.Components;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Brix.Engine;

namespace BetterThanCastle
{
    /// <summary>
    /// Performance > Visuals: night lights that cost GPU and CPU but change nothing in the game (measured 2026-11-21, scenario night-ff:
    /// night render 1.8 -> 3.7 ms of CPU at 640x360, ~125 extra point lights: one lantern per unit + one per firefly; crystal lights cast
    /// point light shadows day and night). The lights register themselves when their object is taken from the pool, so no scene walks;
    /// the switches are enforced in LateUpdate (after animators) and undone when switched back on. Also the shader warm-up: the first
    /// night / fireflies froze a match up to 0.5 s compiling shaders; Shader.WarmupAllShaders once in the main menu (~1.8 s) instead.
    /// Local only, nothing on the wire.
    /// </summary>
    internal static class BtcVfxCut
    {
        private static readonly List<Light> Unit = new List<Light>(), Fly = new List<Light>(), Crystal = new List<Light>();
        private static readonly HashSet<int> UnitIds = new HashSet<int>(), FlyIds = new HashSet<int>(), CrystalIds = new HashSet<int>();
        private static readonly HashSet<int> OffByUs = new HashSet<int>();
        private static readonly Dictionary<int, LightShadows> Shadow = new Dictionary<int, LightShadows>();
        private static bool _unitOn = true, _flyOn = true, _shadowOn = true, _crysOn = true;


        internal static void AddUnit(Light l) { Add(Unit, UnitIds, l); }
        internal static void AddFirefly(Component c) { AddAll(Fly, FlyIds, c, false); }
        // the crystal lights sit on the "Crystal (n)" object above CrystalVisuals; only the shadow casters (its fireflies are children too)
        internal static void AddCrystal(Component c)
        {
            if (c == null) return;
            Transform t = c.transform;
            for (int k = 0; k < 4 && t.parent != null && !t.name.StartsWith("Crystal", StringComparison.Ordinal); k++) t = t.parent;
            AddAll(Crystal, CrystalIds, t, true);
        }

        private static readonly List<Light> Tmp = new List<Light>();
        private static void AddAll(List<Light> into, HashSet<int> ids, Component c, bool shadowOnly)
        {
            if (c == null) return;
            c.GetComponentsInChildren(true, Tmp);
            for (int i = 0; i < Tmp.Count; i++) if (!shadowOnly || Tmp[i].shadows != LightShadows.None) Add(into, ids, Tmp[i]);
            Tmp.Clear();
        }

        private static void Add(List<Light> into, HashSet<int> ids, Light l)
        {
            if (l == null || l.type == LightType.Directional || !ids.Add(l.GetInstanceID())) return;
            into.Add(l);
        }

        /// <summary>LateUpdate: keep the switched-off lights off (vanilla turns unit lanterns on every second, animators drive the rest).</summary>
        internal static void Tick()
        {
            try
            {
                bool unit = BtcConfig.UnitNightLights == null || BtcConfig.UnitNightLights.Value;
                bool fly = BtcConfig.FireflyLights == null || BtcConfig.FireflyLights.Value;
                bool sh = BtcConfig.CrystalShadows == null || BtcConfig.CrystalShadows.Value;
                bool cl = BtcConfig.CrystalLights == null || BtcConfig.CrystalLights.Value;
                bool simple = BtcConfig.SimpleFireflies != null && BtcConfig.SimpleFireflies.Value;
                if (!cl || cl != _crysOn) Enforce(Crystal, cl, cl != _crysOn);
                if (simple != _simpleOn) { _simpleOn = simple; SimpleAll(simple); }
                _crysOn = cl;
                if (!unit || unit != _unitOn) Enforce(Unit, unit, unit != _unitOn);
                if (!fly || fly != _flyOn) Enforce(Fly, fly, fly != _flyOn);
                if (sh != _shadowOn || !sh) Shadows(sh);
                _unitOn = unit; _flyOn = fly; _shadowOn = sh;
                WarmTick();
                DayTick();
            }
            catch (Exception e) { BtcLog.Error("VfxCut", "tick: " + e.Message); }
        }

        private static void Enforce(List<Light> l, bool on, bool changed)
        {
            for (int i = l.Count - 1; i >= 0; i--)
            {
                Light x = l[i];
                if (x == null) { l.RemoveAt(i); continue; }
                int id = x.GetInstanceID();
                if (!on) { if (x.enabled) { x.enabled = false; OffByUs.Add(id); } }
                else if (changed && OffByUs.Remove(id)) x.enabled = true;
            }
        }

        private static void Shadows(bool on)
        {
            for (int i = Crystal.Count - 1; i >= 0; i--)
            {
                Light x = Crystal[i];
                if (x == null) { Crystal.RemoveAt(i); continue; }
                int id = x.GetInstanceID();
                LightShadows s;
                if (!on) { if (x.shadows != LightShadows.None) { Shadow[id] = x.shadows; x.shadows = LightShadows.None; } }
                else if (Shadow.TryGetValue(id, out s)) { x.shadows = s; Shadow.Remove(id); }
            }
        }

        // ---------------------------------------------------------------- day: sun shadow distance, ambient occlusion, bloom (on top of the game's own graphics options)
        private static float _shadowDistOrig = -1f;
        private static bool _shortOn, _noAoOn, _noBloomOn, _noSunOn;
        private static ShadowQuality _shadowsOrig = ShadowQuality.All;
        private static int _camId;
        private static AmplifyOcclusionEffect _ao;
        private static SENaturalBloomAndDirtyLens _bloom;
        internal const float ShortShadowDistance = 120f;

        private static void DayTick()
        {
            bool shortSh = BtcConfig.ShortShadows != null && BtcConfig.ShortShadows.Value;
            bool noAo = BtcConfig.NoAmbientOcclusion != null && BtcConfig.NoAmbientOcclusion.Value;
            bool noBloom = BtcConfig.NoBloom != null && BtcConfig.NoBloom.Value;
            if (shortSh)
            {
                if (QualitySettings.shadowDistance != ShortShadowDistance)
                {
                    if (_shadowDistOrig < 0f || QualitySettings.shadowDistance > ShortShadowDistance) _shadowDistOrig = QualitySettings.shadowDistance;
                    QualitySettings.shadowDistance = ShortShadowDistance;
                }
            }
            else if (_shortOn && _shadowDistOrig > 0f) QualitySettings.shadowDistance = _shadowDistOrig;
            _shortOn = shortSh;
            bool noSun = BtcConfig.NoSunShadows != null && BtcConfig.NoSunShadows.Value;
            if (noSun) { if (QualitySettings.shadows != ShadowQuality.Disable) { _shadowsOrig = QualitySettings.shadows; QualitySettings.shadows = ShadowQuality.Disable; } }
            else if (_noSunOn) QualitySettings.shadows = _shadowsOrig;
            _noSunOn = noSun;
            if (!noAo && !_noAoOn && !noBloom && !_noBloomOn) return;
            Camera c = Camera.main;
            if (c == null) return;
            if (c.GetInstanceID() != _camId) { _camId = c.GetInstanceID(); _ao = c.GetComponent<AmplifyOcclusionEffect>(); _bloom = c.GetComponent<SENaturalBloomAndDirtyLens>(); }
            if (_ao != null)
            {
                if (noAo) { if (_ao.enabled) _ao.enabled = false; }
                else if (_noAoOn) _ao.enabled = Brix.Rendering.CameraEffectsManager.SSAOQuality != Brix.NewUI.UserSettings_.UserSettings.SSAOQualityLevels.off;
            }
            if (_bloom != null)
            {
                if (noBloom) { if (_bloom.enabled) _bloom.enabled = false; }
                else if (_noBloomOn) _bloom.enabled = Brix.Rendering.CameraEffectsManager.BloomEnabled;
            }
            _noAoOn = noAo; _noBloomOn = noBloom;
        }

        // ---------------------------------------------------------------- simple fireflies: particles and trail off, the glowing ball stays
        private static readonly List<Firefly> Flies = new List<Firefly>();
        private static readonly HashSet<int> FlyObjIds = new HashSet<int>();
        private static bool _simpleOn;

        internal static void AddFly(Firefly f)
        {
            if (f == null) return;
            if (FlyObjIds.Add(f.GetInstanceID())) Flies.Add(f);
            if (_simpleOn) Simple(f, true);
        }

        private static void Simple(Firefly f, bool on)
        {
            ParticleSystem ps = f.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ParticleSystem.EmissionModule em = ps.emission;
                em.enabled = !on;
                if (on) ps.Clear(true);
            }
            ParticleSystemRenderer pr = f.GetComponent<ParticleSystemRenderer>();
            if (pr != null) pr.enabled = !on;
            TrailRenderer tr = f.GetComponent<TrailRenderer>();
            if (tr != null) { tr.enabled = !on; if (on) tr.Clear(); }
        }

        private static void SimpleAll(bool on)
        {
            for (int i = Flies.Count - 1; i >= 0; i--)
            {
                if (Flies[i] == null) { Flies.RemoveAt(i); continue; }
                Simple(Flies[i], on);
            }
        }

        // ---------------------------------------------------------------- shader warm-up (once per session, main menu)
        private static bool _warmed;
        private static int _menuFrames;


        // first world load of the session: the game's shaders are loaded only now (the menu warm-up covers the menu ones), warm them under the loading screen
        private static bool _warmedGame;

        internal static void OnLoadBegin()
        {
            if (_warmedGame || (BtcConfig.WarmShaders != null && !BtcConfig.WarmShaders.Value)) return;
            try { GameSignals.Once(GameSignals.GameObjectsLoaded, delegate { WarmGame(); }, null); }
            catch (Exception e) { BtcLog.Error("VfxCut", "load hook: " + e.Message); }
        }

        private static void WarmGame()
        {
            if (_warmedGame) return;
            _warmedGame = true;
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            Shader.WarmupAllShaders();
            BtcLoadBoost.Note("shaders warmed in " + sw.ElapsedMilliseconds + " ms (first world load)");
        }
        private static void WarmTick()
        {
            if (_warmed) return;
            if (BtcConfig.WarmShaders != null && !BtcConfig.WarmShaders.Value) { _warmed = true; return; }
            Scene s = SceneManager.GetActiveScene();
            if (s.name != "SceneMenu" || BtcLoadBoost.Loading) { _menuFrames = 0; return; }
            if (++_menuFrames < 60) return;   // let the menu show first
            _warmed = true;
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            Shader.WarmupAllShaders();
            BtcLog.Info("VfxCut", "shaders warmed in " + sw.ElapsedMilliseconds + " ms (main menu, 60 menu frames at " + Time.realtimeSinceStartup.ToString("0.00") + " s since start)");
        }
    }

    [HarmonyPatch(typeof(ActivateAtNight), "Awake")]
    internal static class VfxUnitLightPatch
    {
        private static void Postfix(ActivateAtNight __instance)
        {
            try { if (__instance != null) BtcVfxCut.AddUnit(__instance.Light); }
            catch (Exception e) { BtcLog.Error("VfxCut", "unit light: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Firefly), "OnRequested")]
    internal static class VfxFireflyLightPatch
    {
        private static void Postfix(Firefly __instance)
        {
            try { BtcVfxCut.AddFirefly(__instance); BtcVfxCut.AddFly(__instance); }
            catch (Exception e) { BtcLog.Error("VfxCut", "firefly light: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(CrystalVisuals), "OnRequested")]
    internal static class VfxCrystalLightPatch
    {
        private static void Postfix(CrystalVisuals __instance)
        {
            try { BtcVfxCut.AddCrystal(__instance); BtcAnimCut.AddCrystal(__instance); }
            catch (Exception e) { BtcLog.Error("VfxCut", "crystal light: " + e.Message); }
        }
    }

    // Firefly.WanderAround: every wandering firefly sent its Wwise parameter (string RTPC + emitter lookup) every frame (~70 at a crystal,
    // the most expensive script of a frame). Saver: each firefly sends it every 4th frame (staggered); the wander sound pitch moves in steps
    // of 4 frames, which is not audible. Local, sound only.
    [HarmonyPatch(typeof(Firefly), "WanderAround")]
    internal static class FrameSaveFireflySound
    {
        static bool Prepare() { return BtcFrameSave.On; }

        internal static void SetParam(string name, float value, GameObject emitter)
        {
            if (emitter != null && ((Time.frameCount + emitter.GetInstanceID()) & 3) != 0) return;
            Brix.Audio.SoundEngine.SetParam(name, value, emitter);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            System.Reflection.MethodInfo from = AccessTools.Method(typeof(Brix.Audio.SoundEngine), "SetParam", new[] { typeof(string), typeof(float), typeof(GameObject) });
            System.Reflection.MethodInfo to = AccessTools.Method(typeof(FrameSaveFireflySound), "SetParam");
            List<CodeInstruction> l = new List<CodeInstruction>(ins);
            int n = 0;
            for (int i = 0; i < l.Count; i++)
                if (from != null && l[i].Calls(from)) { l[i].operand = to; n++; }
            if (n == 0) BtcLog.Error("Hook", "FrameSaveFireflySound: SoundEngine.SetParam not found");
            return l;
        }
    }
}
