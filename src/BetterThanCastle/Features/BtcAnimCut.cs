using System;
using System.Collections.Generic;
using Brix.Game.Components;
using Brix.Input;
using Brix.NewUI.OverheadDisplay;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
    /// <summary>
    /// Performance > Visuals: decoration animators that the game evaluates every frame for nothing (measured 2026-11-21, scenario
    /// anim-dump, 60 workers: 383 animators, only 75 are unit bodies). Per unit an overhead (OverheadAnimationController) plus its
    /// name tag and thought bubble lines (NameTag controller), all AlwaysAnimate and mostly hidden (alpha 0: shown 23 of 151).
    /// PauseHidden: an overhead animator runs only while its content / line is shown (hover, selection, fog, distance all go through
    /// OverheadDisplay.Show / ShowLine); crystal shard animators (colour, light, breathing) stop off screen (CullCompletely keeps their
    /// state); beacon drag handles (Hovered/Normal/Pressed states, clips of 0.1 s) run only for a moment after a state change.
    /// Unity 5.6 resets an animator when it is enabled again, so bool parameters are saved and put back. HideNameTags: name tag lines
    /// are never shown (max performance). Local only, nothing on the wire.
    /// </summary>
    internal static class BtcAnimCut
    {
        private sealed class Paused { internal Animator a; internal string[] names; internal bool[] vals; }

        private static readonly Dictionary<int, Paused> Off = new Dictionary<int, Paused>();
        private static readonly List<Animator> Tmp = new List<Animator>();
        private static readonly List<Animator> Shards = new List<Animator>();
        private static readonly HashSet<int> ShardIds = new HashSet<int>();
        private static readonly List<Animator> Handles = new List<Animator>();
        private static readonly List<float> HandleSleep = new List<float>();
        private static readonly Dictionary<int, Animator> HandleOf = new Dictionary<int, Animator>();
        private static bool _pauseOn = true;
        private static int _scene = -1;
        private const float HandleAwake = 0.6f;

        internal static bool PauseOn { get { return BtcConfig.PauseHiddenAnims == null || BtcConfig.PauseHiddenAnims.Value; } }
        internal static bool HideTags { get { return BtcConfig.HideNameTags != null && BtcConfig.HideNameTags.Value; } }

        // ---------------------------------------------------------------- overheads
        internal static void OnShow(OverheadDisplay d, bool shown, Transform content)
        {
            if (!PauseOn || d == null) return;
            // the display root animator (OverheadAnimationController: show/hide of the whole icon) is never paused: pausing it
            // resets it on wake and the unit mode icon stayed invisible (user report 2026-11-22); only the line animators sleep
            Animator own = d.GetComponent<Animator>();
            if (own != null) Set(own, true);
            if (content == null) return;
            content.GetComponentsInChildren(true, Tmp);
            for (int i = 0; i < Tmp.Count; i++) Set(Tmp[i], shown && LineShown(Tmp[i].transform, content));
            Tmp.Clear();
        }

        internal static void OnLine(Overhead line, bool shownDisplay)
        {
            if (!PauseOn || line == null) return;
            line.GetComponentsInChildren(true, Tmp);
            bool on = shownDisplay && LineShown(line.transform, null);
            for (int i = 0; i < Tmp.Count; i++) Set(Tmp[i], on && LineShown(Tmp[i].transform, line.transform.parent));
            Tmp.Clear();
        }


        // every display, swept a few per frame: catches displays whose Show never ran since spawn and the moment the switch goes on
        private static readonly List<OverheadDisplay> Displays = new List<OverheadDisplay>();
        private static readonly HashSet<int> DisplayIds = new HashSet<int>();
        private static readonly AccessTools.FieldRef<OverheadDisplay, Transform> ContentOf = AccessTools.FieldRefAccess<OverheadDisplay, Transform>("Content");
        private static int _sweep;

        internal static void AddDisplay(OverheadDisplay d)
        {
            if (d != null && DisplayIds.Add(d.GetInstanceID())) Displays.Add(d);
        }

        private static void Sweep()
        {
            for (int n = 0; n < 6 && Displays.Count > 0; n++)
            {
                if (_sweep >= Displays.Count) _sweep = 0;
                OverheadDisplay d = Displays[_sweep];
                if (d == null) { Displays.RemoveAt(_sweep); continue; }
                _sweep++;
                Transform c = ContentOf(d);
                CanvasGroup g = c != null ? c.GetComponent<CanvasGroup>() : null;
                OnShow(d, g == null || g.alpha > 0f, c);
            }
        }
        // every canvas group between the animator and the stop transform must be visible
        private static bool LineShown(Transform t, Transform stop)
        {
            for (; t != null && t != stop; t = t.parent)
            {
                CanvasGroup g = t.GetComponent<CanvasGroup>();
                if (g != null && g.alpha <= 0f) return false;
                if (t.GetComponent<OverheadDisplay>() != null) break;
            }
            return true;
        }

        private static void Set(Animator a, bool on)
        {
            if (a == null) return;
            int id = a.GetInstanceID();
            if (!on)
            {
                if (!a.enabled || Off.ContainsKey(id)) return;
                Paused p = new Paused { a = a };
                Save(p);
                a.enabled = false;
                Off[id] = p;
                return;
            }
            Paused q;
            if (!Off.TryGetValue(id, out q)) return;
            Off.Remove(id);
            Wake(q);
        }

        private static void Save(Paused p)
        {
            if (!p.a.isActiveAndEnabled || p.a.runtimeAnimatorController == null) return;
            AnimatorControllerParameter[] ps = p.a.parameters;
            int n = 0;
            for (int i = 0; i < ps.Length; i++) if (ps[i].type == AnimatorControllerParameterType.Bool) n++;
            if (n == 0) return;
            p.names = new string[n]; p.vals = new bool[n];
            for (int i = 0, k = 0; i < ps.Length; i++)
                if (ps[i].type == AnimatorControllerParameterType.Bool) { p.names[k] = ps[i].name; p.vals[k] = p.a.GetBool(ps[i].name); k++; }
        }

        private static void Wake(Paused p)
        {
            if (p.a == null) return;
            p.a.enabled = true;
            if (p.names == null || !p.a.isActiveAndEnabled) return;
            for (int i = 0; i < p.names.Length; i++) p.a.SetBool(p.names[i], p.vals[i]);
        }

        private static void WakeAll()
        {
            foreach (Paused p in Off.Values) Wake(p);
            Off.Clear();
        }

        // ---------------------------------------------------------------- crystal shards (only with a renderer: CullCompletely without one never animates)
        internal static void AddCrystal(Component c)
        {
            if (c == null) return;
            Animator a = c.GetComponent<Animator>();
            if (a == null || !ShardIds.Add(a.GetInstanceID())) return;
            AddStatic(a);
            if (a.GetComponentInChildren<Renderer>(true) == null) return;
            Shards.Add(a);
            if (PauseOn) a.cullingMode = AnimatorCullingMode.CullCompletely;
        }

        private static void ShardMode(bool cull)
        {
            for (int i = Shards.Count - 1; i >= 0; i--)
            {
                if (Shards[i] == null) { Shards.RemoveAt(i); continue; }
                Shards[i].cullingMode = cull ? AnimatorCullingMode.CullCompletely : AnimatorCullingMode.AlwaysAnimate;
            }
        }

        // ---------------------------------------------------------------- static crystals: the animator sleeps, woken only to repaint on a faction change
        private sealed class Crys { internal Animator a; internal float until; internal string[] n; internal AnimatorControllerParameterType[] t; internal float[] v; }
        private static readonly List<Crys> Crystals = new List<Crys>();
        private static readonly Dictionary<int, Crys> CrysOf = new Dictionary<int, Crys>();
        private static bool _staticOn;
        private const float CrysAwake = 2f;

        internal static bool StaticCrystals { get { return BtcConfig.StaticCrystals != null && BtcConfig.StaticCrystals.Value; } }

        private static void AddStatic(Animator a)
        {
            if (CrysOf.ContainsKey(a.GetInstanceID())) return;
            Crys c = new Crys { a = a };
            CrysOf[a.GetInstanceID()] = c;
            Crystals.Add(c);
            CrysWake(c, 3f);
        }

        /// <summary>Before the game repaints a crystal (spawn, faction change, end of a capture): run its animator for a moment.</summary>
        internal static void CrystalWake(Component cv)
        {
            if (cv == null) return;
            Animator a = cv.GetComponent<Animator>();
            Crys c;
            if (a != null && CrysOf.TryGetValue(a.GetInstanceID(), out c)) CrysWake(c, CrysAwake);
        }

        private static void CrysWake(Crys c, float dur)
        {
            if (c.a == null) return;
            if (!c.a.enabled)
            {
                c.a.enabled = true;
                if (c.n != null && c.a.isActiveAndEnabled)
                    for (int i = 0; i < c.n.Length; i++)
                    {
                        if (c.t[i] == AnimatorControllerParameterType.Bool) c.a.SetBool(c.n[i], c.v[i] > 0.5f);
                        else if (c.t[i] == AnimatorControllerParameterType.Int) c.a.SetInteger(c.n[i], (int)c.v[i]);
                        else if (c.t[i] == AnimatorControllerParameterType.Float) c.a.SetFloat(c.n[i], c.v[i]);
                    }
            }
            c.until = Mathf.Max(c.until, Time.unscaledTime + dur);
        }

        private static void CrysSleep(Crys c)
        {
            Animator a = c.a;
            if (a.isActiveAndEnabled && a.runtimeAnimatorController != null)
            {
                AnimatorControllerParameter[] ps = a.parameters;
                c.n = new string[ps.Length]; c.t = new AnimatorControllerParameterType[ps.Length]; c.v = new float[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    c.n[i] = ps[i].name; c.t[i] = ps[i].type;
                    if (ps[i].type == AnimatorControllerParameterType.Bool) c.v[i] = a.GetBool(ps[i].name) ? 1f : 0f;
                    else if (ps[i].type == AnimatorControllerParameterType.Int) c.v[i] = a.GetInteger(ps[i].name);
                    else if (ps[i].type == AnimatorControllerParameterType.Float) c.v[i] = a.GetFloat(ps[i].name);
                }
            }
            a.enabled = false;
        }

        private static void CrysTick(bool on)
        {
            float now = Time.unscaledTime;
            for (int i = Crystals.Count - 1; i >= 0; i--)
            {
                Crys c = Crystals[i];
                if (c.a == null) { Crystals.RemoveAt(i); continue; }
                if (!on) { if (!c.a.enabled) CrysWake(c, 0f); continue; }
                if (c.a.enabled && now > c.until) CrysSleep(c);
            }
        }

        // ---------------------------------------------------------------- spawn point handles: world editor only, switched off in a normal game
        private static readonly List<GameObject> SpawnHandles = new List<GameObject>();
        private static readonly List<GameObject> SpawnOff = new List<GameObject>();

        private static bool InEditor()
        {
            try { return InputModeController.DefaultMode == InputMode.worldEditor || InputModeController.GetCurrentMode() == InputMode.worldEditor; }
            catch (Exception) { return false; }
        }

        private static void AddSpawnHandle(DragHandle h)
        {
            GameObject g = h.gameObject;
            if (!SpawnHandles.Contains(g) && !SpawnOff.Contains(g)) SpawnHandles.Add(g);
        }

        private static void SpawnTick(bool pause)
        {
            bool off = pause && !BtcLoadBoost.Loading && !InEditor();
            if (off)
            {
                for (int i = SpawnHandles.Count - 1; i >= 0; i--)
                {
                    GameObject g = SpawnHandles[i];
                    SpawnHandles.RemoveAt(i);
                    if (g == null || !g.activeSelf || g.GetComponentInParent<BaliseStandaloneOverheadDisplayDriver>() == null) continue;
                    g.SetActive(false);
                    SpawnOff.Add(g);
                }
            }
            else if (SpawnOff.Count > 0 && !BtcLoadBoost.Loading)
            {
                for (int i = 0; i < SpawnOff.Count; i++) if (SpawnOff[i] != null) SpawnOff[i].SetActive(true);
                SpawnOff.Clear();
            }
        }

        // ---------------------------------------------------------------- beacon drag handles: awake for a moment after a state change
        internal static void HandleWake(DragHandle h, Animator a)
        {
            _wakes++;
            if (h == null) return;
            if (!HandleOf.ContainsKey(h.GetInstanceID())) AddSpawnHandle(h);
            int id = h.GetInstanceID();
            Animator known;
            if (HandleOf.TryGetValue(id, out known)) a = known;
            else { if (a == null) return; HandleOf[id] = a; }
            if (a == null) return;
            int at = Handles.IndexOf(a);
            if (at < 0) { Handles.Add(a); HandleSleep.Add(0f); at = Handles.Count - 1; }
            HandleSleep[at] = Time.unscaledTime + HandleAwake;
            if (!a.enabled) a.enabled = true;
        }

        private static void HandleTick(bool pause)
        {
            float now = Time.unscaledTime;
            for (int i = Handles.Count - 1; i >= 0; i--)
            {
                Animator a = Handles[i];
                if (a == null) { Handles.RemoveAt(i); HandleSleep.RemoveAt(i); continue; }
                if (!pause) { if (!a.enabled) a.enabled = true; continue; }
                if (a.enabled && now > HandleSleep[i]) a.enabled = false;
            }
        }

        // scene change: drop what was destroyed (lists are kept: objects made during the load live on in the new scene)
        private static readonly List<int> Dead = new List<int>();
        private static void Prune()
        {
            foreach (KeyValuePair<int, Paused> kv in Off) if (kv.Value.a == null) Dead.Add(kv.Key);
            for (int i = 0; i < Dead.Count; i++) Off.Remove(Dead[i]);
            Dead.Clear();
            Displays.RemoveAll(d => d == null); DisplayIds.Clear(); foreach (OverheadDisplay d in Displays) DisplayIds.Add(d.GetInstanceID());
            Shards.RemoveAll(a => a == null);
            Crystals.RemoveAll(c => c.a == null); CrysOf.Clear(); foreach (Crys c in Crystals) CrysOf[c.a.GetInstanceID()] = c;
            SpawnHandles.RemoveAll(g => g == null); SpawnOff.RemoveAll(g => g == null);
            foreach (KeyValuePair<int, Animator> kv in HandleOf) if (kv.Value == null) Dead.Add(kv.Key);
            for (int i = 0; i < Dead.Count; i++) HandleOf.Remove(Dead[i]);
            Dead.Clear();
        }

        internal static string Stats() { return "animcut paused=" + Off.Count + " displays=" + Displays.Count + " shards=" + Shards.Count + " crystals=" + Crystals.Count + " spawnOff=" + SpawnOff.Count + " handles=" + Handles.Count + " handleOf=" + HandleOf.Count + " wakes=" + _wakes; }
        private static int _wakes;

        /// <summary>LateUpdate (from BtcVfxCut.Tick).</summary>
        internal static void Tick()
        {
            int s = SceneManager.GetActiveScene().GetHashCode();
            if (s != _scene)
            {
                _scene = s;
                Prune();
            }
            bool pause = PauseOn;
            if (pause != _pauseOn)
            {
                _pauseOn = pause;
                if (!pause) WakeAll();
                ShardMode(pause);
            }
            if (Handles.Count > 0) HandleTick(pause);
            if (pause) Sweep();
            bool st = StaticCrystals;
            if (st || st != _staticOn) CrysTick(st);
            _staticOn = st;
            if (SpawnHandles.Count > 0 || SpawnOff.Count > 0) SpawnTick(pause);
        }
    }

    // static crystals: no per-frame lerps (creep fill, time of day glow), the animator is woken right before a repaint
    [HarmonyPatch(typeof(CrystalVisuals), "Update")]
    internal static class AnimCutCrystalUpdate
    {
        private static bool Prefix() { return !BtcAnimCut.StaticCrystals; }
    }

    [HarmonyPatch(typeof(CrystalVisuals), "FactionChanged")]
    internal static class AnimCutCrystalFaction
    {
        private static void Prefix(CrystalVisuals __instance)
        {
            try { BtcAnimCut.CrystalWake(__instance); }
            catch (Exception e) { BtcLog.Error("AnimCut", "crystal faction: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(CrystalVisuals), "EndTransition")]
    internal static class AnimCutCrystalEnd
    {
        private static void Prefix(CrystalVisuals __instance)
        {
            try { BtcAnimCut.CrystalWake(__instance); }
            catch (Exception e) { BtcLog.Error("AnimCut", "crystal end: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(CrystalDriver), "Start")]
    internal static class AnimCutCrystalDriverStart
    {
        private static void Prefix(CrystalVisuals ___crystal)
        {
            try { BtcAnimCut.CrystalWake(___crystal); }
            catch (Exception e) { BtcLog.Error("AnimCut", "crystal start: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(OverheadDisplay), "Init")]
    internal static class AnimCutOverheadInit
    {
        private static void Postfix(OverheadDisplay __instance)
        {
            try { BtcAnimCut.AddDisplay(__instance); }
            catch (Exception e) { BtcLog.Error("AnimCut", "init: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(OverheadDisplay), "Show")]
    internal static class AnimCutOverheadShow
    {
        private static void Postfix(OverheadDisplay __instance, bool value, Transform ___Content)
        {
            try { BtcAnimCut.OnShow(__instance, value, ___Content); }
            catch (Exception e) { BtcLog.Error("AnimCut", "overhead: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(OverheadDisplay), "ShowLine")]
    internal static class AnimCutOverheadLine
    {
        private static readonly AccessTools.FieldRef<NameTag, UnityEngine.UI.Text> NameOf = AccessTools.FieldRefAccess<NameTag, UnityEngine.UI.Text>("Name");

        private static void Postfix(OverheadDisplay __instance, Overhead line, Transform ___Content)
        {
            try
            {
                // Hide name tags: only the name text goes, the line stays (it also holds the unit mode icon, NameTag.Badge)
                NameTag tag = line as NameTag;
                if (tag != null)
                {
                    UnityEngine.UI.Text n = NameOf(tag);
                    bool show = !BtcAnimCut.HideTags;
                    if (n != null && n.gameObject.activeSelf != show) { n.gameObject.SetActive(show); tag.RefreshUI(); }
                }
                CanvasGroup g = ___Content != null ? ___Content.GetComponent<CanvasGroup>() : null;
                BtcAnimCut.OnLine(line, g == null || g.alpha > 0f);
            }
            catch (Exception e) { BtcLog.Error("AnimCut", "line: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(DragHandle), "UpdateAnimState")]
    internal static class AnimCutHandleState
    {
        private static readonly AccessTools.FieldRef<DragHandle, DragHandle.Dependencies> Deps = AccessTools.FieldRefAccess<DragHandle, DragHandle.Dependencies>("dependencies");

        internal static Animator Of(DragHandle h)
        {
            DragHandle.Dependencies d = Deps(h);
            return d != null ? d.animator : null;
        }

        // before the game sets the state parameters: a woken animator restarts from its default state, then takes the new state
        private static void Prefix(DragHandle __instance)
        {
            try { BtcAnimCut.HandleWake(__instance, Of(__instance)); }
            catch (Exception e) { BtcLog.Error("AnimCut", "handle: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(DragHandle), "OnEnable")]
    internal static class AnimCutHandleEnable
    {
        private static void Prefix(DragHandle __instance)
        {
            try { BtcAnimCut.HandleWake(__instance, AnimCutHandleState.Of(__instance)); }
            catch (Exception e) { BtcLog.Error("AnimCut", "handle enable: " + e.Message); }
        }
    }
}
