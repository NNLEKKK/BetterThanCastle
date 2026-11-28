#if DEV
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV only engine-time attribution: A/B subsystem toggles (engine particles|animators|render|physics|shadows|lights|vsync|quality),
    /// measure windows (engine.txt) and phase timers (Update / animation gap / LateUpdate / render / fixed steps).
    /// Phases: Early component (order -32000) and Late component (order +32000) bracket Update and LateUpdate; the gap between
    /// them is Animator + coroutines. Render = Camera.onPreCull -> onPostRender summed. other = the rest (physics, vsync wait, GPU present).
    /// </summary>
    internal static class BtcDevEngine
    {
        internal sealed class Acc
        {
            public long N; public double Frame, Upd, Anim, Late, Rend, Fixed;
            public void Clear() { N = 0; Frame = Upd = Anim = Late = Rend = Fixed = 0; }
        }

        private const int Ring = 4096;
        private static readonly float[] Frames = new float[Ring];
        private static int _ringN;
        private static readonly Acc Meas = new Acc();
        private static readonly Acc ProfAcc = new Acc();
        private static bool _inited;
        private static readonly double TickMs = 1000.0 / Stopwatch.Frequency;

        // stamps
        private static long _u0, _u1, _l0, _l1, _camStart;
        private static double _rendAcc;
        private static int _fixedCount;

        // measure
        private static string _mTag; private static float _mEnd; private static bool _measuring;
        // counters
        private static string _counters = "-";
        internal static bool NoCounters = Environment.GetEnvironmentVariable("BTC_NOCOUNTERS") == "1";
        private static readonly List<string> Spikes = new List<string>();
        private static readonly List<float> SpikeT = new List<float>();
        private static int _lastGc;
        private static float _nextCount;

        // toggle state
        private static bool _partOff, _animOff, _lightOff, _animOther;   // _animOther: only animators that are not on a unit
        private static readonly Dictionary<int, bool> PsPlaying = new Dictionary<int, bool>();
        private static readonly Dictionary<int, bool> AnEnabled = new Dictionary<int, bool>();
        private static readonly Dictionary<int, bool> LiEnabled = new Dictionary<int, bool>();
        private static readonly Dictionary<int, int> CamMask = new Dictionary<int, int>();
        private static bool _renderOff;
        private static bool _physOff; private static float _origFixed, _origMaxDt;
        private static ShadowQuality _origShadows; private static bool _shadowsSaved;
        private static int _origVsync = -1;
        private static int _origQuality = -1;
        private static float _nextSweep;

        internal static void Init()
        {
            if (_inited) return;
            _inited = true;
            try
            {
                Directory.CreateDirectory(BtcDevPaths.LogDir);
                File.WriteAllText(Path.Combine(BtcDevPaths.LogDir, "engine.txt"), string.Empty);
                GameObject a = new GameObject("BtcDevEngEarly");
                UnityEngine.Object.DontDestroyOnLoad(a); a.hideFlags = HideFlags.HideAndDontSave; a.AddComponent<BtcDevEngEarly>();
                GameObject b = new GameObject("BtcDevEngLate");
                UnityEngine.Object.DontDestroyOnLoad(b); b.hideFlags = HideFlags.HideAndDontSave; b.AddComponent<BtcDevEngLate>();
                Camera.onPreCull += OnPreCull;
                Camera.onPostRender += OnPostRender;
                BtcLog.Info("Engine", "armed");
            }
            catch (Exception e) { BtcLog.Error("Engine", "init: " + e.Message); }
        }

        internal static void ResetProf() { ProfAcc.Clear(); PhSpikes.Clear(); }
        private static readonly System.Collections.Generic.List<string> PhSpikes = new System.Collections.Generic.List<string>();

        // ---------- phase stamps ----------
        private static void OnPreCull(Camera c) { _camStart = Stopwatch.GetTimestamp(); }
        private static void OnPostRender(Camera c) { if (_camStart != 0) _rendAcc += (Stopwatch.GetTimestamp() - _camStart) * TickMs; _camStart = 0; }
        internal static void MarkFixed() { _fixedCount++; }
        internal static void MarkLate0() { _l0 = Stopwatch.GetTimestamp(); }
        internal static void MarkUpd1() { _u1 = Stopwatch.GetTimestamp(); }
        internal static void MarkLate1() { _l1 = Stopwatch.GetTimestamp(); }

        internal static void MarkUpd0()
        {
            long now = Stopwatch.GetTimestamp();
            float dt = Time.unscaledDeltaTime * 1000f;
            if (_u0 != 0 && _l1 != 0 && _u1 >= _u0 && _l0 >= _u1 && _l1 >= _l0)
            {
                double upd = (_u1 - _u0) * TickMs, anim = (_l0 - _u1) * TickMs, late = (_l1 - _l0) * TickMs;
                Add(Meas, dt, upd, anim, late);
                Add(ProfAcc, dt, upd, anim, late);
                if (dt > 25f && PhSpikes.Count < 300)
                    PhSpikes.Add(Time.realtimeSinceStartup.ToString("0.00") + " dt=" + dt.ToString("0") + " upd=" + upd.ToString("0.0") + " anim=" + anim.ToString("0.0") + " late=" + late.ToString("0.0")
                        + " rend=" + _rendAcc.ToString("0.0") + " fixed=" + _fixedCount + " other=" + Math.Max(0.0, dt - upd - anim - late - _rendAcc).ToString("0.0"));
                Frames[_ringN % Ring] = dt; _ringN++;
                if (_measuring && dt > 100f && Spikes.Count < 40)
                {
                    int gc = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
                    SpikeT.Add(Time.realtimeSinceStartup);
                    Spikes.Add("f" + _ringN + " t=" + Time.realtimeSinceStartup.ToString("0.0") + " dt=" + dt.ToString("0") + " upd=" + upd.ToString("0") + " anim=" + anim.ToString("0") + " late=" + late.ToString("0") + " rend=" + _rendAcc.ToString("0") + " fixed=" + _fixedCount + " gc+" + (gc - _lastGc));
                }
                _lastGc = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
            }
            _fixedCount = 0; _rendAcc = 0; _u0 = now; _l1 = 0;
            Tick();
        }

        private static void Add(Acc a, double dt, double upd, double anim, double late)
        {
            a.N++; a.Frame += dt; a.Upd += upd; a.Anim += anim; a.Late += late; a.Rend += _rendAcc; a.Fixed += _fixedCount;
        }

        // ---------- per frame tick ----------
        private static void Tick()
        {
            float now = Time.realtimeSinceStartup;
            if (_measuring && now >= _mEnd) EndMeasure();
            if ((_partOff || _animOff || _lightOff) && now >= _nextSweep) { _nextSweep = now + 2f; Sweep(); }
            if (!_measuring && !NoCounters && now >= _nextCount) { _nextCount = now + 5f; SampleCounters(); BtcDevProf.SkipFrames = 2; } // scene walk: frame left out of the stats
        }

        // ---------- measure ----------
        internal static void BeginMeasure(string tag, float sec)
        {
            Spikes.Clear(); SpikeT.Clear();
            Meas.Clear(); _ringN = 0; Array.Clear(Frames, 0, Ring);
            _mTag = tag; _mEnd = Time.realtimeSinceStartup + sec; _measuring = true;
        }

        internal static bool Measuring { get { return _measuring; } }

        internal static void EndMeasure()
        {
            if (!_measuring) return;
            _measuring = false;
            int n = Math.Min(_ringN, Ring);
            float[] c = new float[n]; Array.Copy(Frames, c, n); Array.Sort(c);
            double p50 = n > 0 ? c[n / 2] : 0, p99 = n > 0 ? c[Math.Min(n - 1, (int)(n * 0.99))] : 0, max = n > 0 ? c[n - 1] : 0;
            string line = "Engine " + _mTag + " p50=" + p50.ToString("0.0") + " p99=" + p99.ToString("0.0") + " max=" + max.ToString("0.0") + " n=" + _ringN
                + " | " + PhaseText(Meas);
            BtcLog.Info("Engine", line);
            StringBuilder sp = new StringBuilder();
            sp.Append("  spikes>100ms=").Append(Spikes.Count).Append(" counters=").Append(NoCounters ? "off" : "on");
            if (SpikeT.Count > 1) { sp.Append(" period="); for (int i = 1; i < SpikeT.Count; i++) sp.Append((SpikeT[i] - SpikeT[i - 1]).ToString("0.0")).Append(i + 1 < SpikeT.Count ? "," : string.Empty); }
            for (int i = 0; i < Spikes.Count && i < 6; i++) sp.AppendLine().Append("    ").Append(Spikes[i]);
            line += Environment.NewLine + sp;
            string netLine = BtcDevNet.Line("m"); if (netLine.Length > 0) line += Environment.NewLine + "  " + netLine;
            BtcLog.Info("Engine", sp.ToString());
            try { File.AppendAllText(Path.Combine(BtcDevPaths.LogDir, "engine.txt"), line + Environment.NewLine); } catch { }
        }

        private static string PhaseText(Acc a)
        {
            if (a.N == 0) return "phases: -";
            double n = a.N, fr = a.Frame / n, u = a.Upd / n, an = a.Anim / n, l = a.Late / n, r = a.Rend / n;
            double other = fr - u - an - l - r; if (other < 0) other = 0;
            return "phases: update=" + u.ToString("0.0") + " anim=" + an.ToString("0.0") + " late=" + l.ToString("0.0") + " render=" + r.ToString("0.0")
                + " fixed_steps=" + (a.Fixed / n).ToString("0.00") + " other=" + other.ToString("0.0") + " frame=" + fr.ToString("0.0");
        }

        /// <summary>Two lines for prof.txt: phases (avg per frame since prof reset) + engine counters (sampled every 5 s).</summary>
        internal static void Append(StringBuilder sb)
        {
            sb.AppendLine(PhaseText(ProfAcc));
            sb.Append("eng: ").AppendLine(_counters);
            if (PhSpikes.Count > 0)
            {
                sb.AppendLine("PHASE SPIKES (realtime dt>25ms, first 300; other = between LateUpdate end and next Update minus camera render: physics, canvases, present)");
                for (int i = 0; i < PhSpikes.Count && i < 60; i++) sb.AppendLine(PhSpikes[i]);
            }
        }

        internal static void SampleCounters()
        {
            try
            {
                Animator[] an = UnityEngine.Object.FindObjectsOfType<Animator>();
                int vis = 0, en = 0;
                for (int i = 0; i < an.Length; i++)
                {
                    if (an[i] == null) continue;
                    if (an[i].enabled) en++;
                    Renderer r = an[i].GetComponentInChildren<Renderer>();
                    if (r != null && r.isVisible) vis++;
                }
                int rend = UnityEngine.Object.FindObjectsOfType<Renderer>().Length;
                int skin = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>().Length;
                Light[] li = UnityEngine.Object.FindObjectsOfType<Light>();
                int lon = 0; for (int i = 0; i < li.Length; i++) if (li[i] != null && li[i].enabled) lon++;
                ParticleSystem[] ps = UnityEngine.Object.FindObjectsOfType<ParticleSystem>();
                int pl = 0; for (int i = 0; i < ps.Length; i++) if (ps[i] != null && ps[i].isPlaying) pl++;
                Rigidbody[] rb = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
                int ra = 0; for (int i = 0; i < rb.Length; i++) if (rb[i] != null && !rb[i].isKinematic && !rb[i].IsSleeping()) ra++;
                int col = UnityEngine.Object.FindObjectsOfType<Collider>().Length;
                _counters = "animators=" + an.Length + " enabled=" + en + " visible=" + vis + " skinned=" + skin + " renderers=" + rend + " lights=" + li.Length + "/on" + lon
                    + " ps=" + ps.Length + "/playing" + pl + " rb_active=" + ra + "/" + rb.Length + " colliders=" + col;
            }
            catch (Exception e) { _counters = "err " + e.Message; }
        }

        // ---------- toggles ----------
        internal static void Command(string[] p)
        {
            string what = p.Length > 1 ? p[1].ToLowerInvariant() : string.Empty;
            string arg = p.Length > 2 ? p[2].ToLowerInvariant() : string.Empty;
            bool off = arg == "off";
            if (what != "quality" && arg != "on" && arg != "off") { BtcLog.Warn("Engine", "usage: engine <what> on|off"); return; }
            switch (what)
            {
                case "counters": NoCounters = off; break;
                case "culling": SetCulling(false, !off); break;
                case "cullall": SetCulling(true, !off); break;
                case "cullfull": SetCulling(false, !off, true); break;
                case "particles": _partOff = off; if (!off) RestoreParticles(); else Sweep(); break;
                case "animators": _animOff = off; _animOther = false; if (!off) RestoreAnimators(); else Sweep(); break;
                case "animothers": _animOff = off; _animOther = off; if (!off) RestoreAnimators(); else Sweep(); break;
                case "lights": _lightOff = off; if (!off) RestoreLights(); else Sweep(); break;
                case "render": SetRender(off); break;
                case "physics": SetPhysics(off); break;
                case "shadows": SetShadows(off); break;
                case "vsync":
                    if (off) { if (_origVsync < 0) _origVsync = QualitySettings.vSyncCount; QualitySettings.vSyncCount = 0; }
                    else if (_origVsync >= 0) { QualitySettings.vSyncCount = _origVsync; _origVsync = -1; }
                    break;
                case "quality":
                    {
                        int q;
                        if (arg == "reset" || arg == "restore") { if (_origQuality >= 0) QualitySettings.SetQualityLevel(_origQuality, true); _origQuality = -1; }
                        else if (int.TryParse(arg, out q)) { if (_origQuality < 0) _origQuality = QualitySettings.GetQualityLevel(); QualitySettings.SetQualityLevel(q, true); }
                        else BtcLog.Warn("Engine", "usage: engine quality <n|reset>");
                        break;
                    }
                default: BtcLog.Warn("Engine", "unknown engine target: " + what); return;
            }
            BtcLog.Info("Engine", what + " " + arg + " vsync=" + QualitySettings.vSyncCount + " quality=" + QualitySettings.GetQualityLevel() + " shadows=" + QualitySettings.shadows);
        }

        // engine culling on|off: unit animators (Locomotion4) CullUpdateTransforms|AlwaysAnimate; cullall: every Animator.
        private static void SetCulling(bool all, bool cull, bool full = false)
        {
            AnimatorCullingMode m = cull ? (full ? AnimatorCullingMode.CullCompletely : AnimatorCullingMode.CullUpdateTransforms) : AnimatorCullingMode.AlwaysAnimate;
            int n = 0;
            if (all) { Animator[] an = UnityEngine.Object.FindObjectsOfType<Animator>(); for (int i = 0; i < an.Length; i++) if (an[i] != null) { an[i].cullingMode = m; n++; } }
            else { Brix.Game.AI.Locomotion4[] ls = UnityEngine.Object.FindObjectsOfType<Brix.Game.AI.Locomotion4>(); for (int i = 0; i < ls.Length; i++) { Animator a = ls[i] != null ? ls[i].GetComponent<Animator>() : null; if (a != null) { a.cullingMode = m; n++; } } }
            BtcLog.Info("Engine", "culling " + (all ? "all" : "units") + " -> " + m + " animators=" + n);
        }


        // vfxdump [tag]: what lights the scene and what emits particles, grouped by object name (night vs day cost hunt). BTClogs/vfx-<tag>.txt
        internal static void VfxDump(string tag)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                Dictionary<string, int[]> g = new Dictionary<string, int[]>();
                Light[] li = UnityEngine.Object.FindObjectsOfType<Light>();
                foreach (Light l in li)
                {
                    if (l == null) continue;
                    Transform p = l.transform.parent;
                    string k = l.type + " " + l.name + " < " + (p != null ? p.name : "-") + " shadows=" + l.shadows + " mode=" + l.renderMode + " range=" + l.range.ToString("F0");
                    int[] v; if (!g.TryGetValue(k, out v)) g[k] = v = new int[2];
                    v[0]++; if (l.enabled && l.gameObject.activeInHierarchy && l.intensity > 0f) v[1]++;
                }
                sb.AppendLine("LIGHTS total=" + li.Length + " pixelLightCount=" + QualitySettings.pixelLightCount + " shadows=" + QualitySettings.shadows + " shadowDist=" + QualitySettings.shadowDistance);
                Emit(sb, g);
                g.Clear();
                ParticleSystem[] ps = UnityEngine.Object.FindObjectsOfType<ParticleSystem>();
                foreach (ParticleSystem s in ps)
                {
                    if (s == null) continue;
                    Transform p = s.transform.parent;
                    string k = s.name + " < " + (p != null ? p.name : "-") + " max=" + s.main.maxParticles;
                    int[] v; if (!g.TryGetValue(k, out v)) g[k] = v = new int[3];
                    v[0]++; if (s.isPlaying) v[1]++; v[2] += s.particleCount;
                }
                sb.AppendLine("PARTICLES total=" + ps.Length + "  (count playing particles)");
                Emit(sb, g);
                g.Clear();
                foreach (MonoBehaviour m in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
                {
                    if (m == null || !m.isActiveAndEnabled) continue;
                    string n = m.GetType().Name;
                    if (n.IndexOf("Firefly", StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Light", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("Flicker", StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Fx", StringComparison.Ordinal) < 0
                        && n.IndexOf("Night", StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("Effect", StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Particle", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    int[] v; if (!g.TryGetValue(n, out v)) g[n] = v = new int[1];
                    v[0]++;
                }
                Dictionary<string, int[]> ga = new Dictionary<string, int[]>();
                Animator[] ans = UnityEngine.Object.FindObjectsOfType<Animator>();
                foreach (Animator a in ans)
                {
                    if (a == null) continue;
                    Transform p = a.transform.parent;
                    string nm = a.name; int c = nm.IndexOf(" (", StringComparison.Ordinal); if (c > 0) nm = nm.Substring(0, c);
                    string pn = p != null ? p.name : "-"; c = pn.IndexOf(" (", StringComparison.Ordinal); if (c > 0) pn = pn.Substring(0, c);
                    bool unit = a.GetComponentInParent<Brix.Game.AI.Locomotion4>() != null;
                    Renderer r = a.GetComponentInChildren<Renderer>();
                    string k = (unit ? "UNIT " : "") + nm + " < " + pn + " ctrl=" + (a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "-")
                        + " cull=" + a.cullingMode + " rootMotion=" + a.applyRootMotion + " rend=" + (r == null ? "none" : r.GetType().Name);
                    int[] v; if (!ga.TryGetValue(k, out v)) ga[k] = v = new int[3];
                    v[0]++; if (a.isActiveAndEnabled) v[1]++; if (a.isActiveAndEnabled && r != null && r.isVisible) v[2]++;
                }
                sb.AppendLine("ANIMATORS total=" + ans.Length + "  (count on=active+enabled visible=)");
                Emit(sb, ga);
                sb.AppendLine(BetterThanCastle.BtcAnimCut.Stats());
                sb.AppendLine("CLIPS (controller: clip/length, loop; shown = parent CanvasGroup alpha > 0)");
                Dictionary<string, string> clips = new Dictionary<string, string>();
                Dictionary<string, int[]> shown = new Dictionary<string, int[]>();
                foreach (Animator a in ans)
                {
                    if (a == null || a.runtimeAnimatorController == null) continue;
                    string cn = a.runtimeAnimatorController.name;
                    if (!clips.ContainsKey(cn))
                    {
                        StringBuilder cs = new StringBuilder();
                        foreach (AnimationClip ac in a.runtimeAnimatorController.animationClips)
                            if (ac != null) cs.Append(' ').Append(ac.name).Append('/').Append(ac.length.ToString("0.##")).Append(ac.isLooping ? "L" : "");
                        AnimatorControllerParameter[] aps = a.isActiveAndEnabled ? a.parameters : new AnimatorControllerParameter[0];
                        cs.Append("  params:");
                        foreach (AnimatorControllerParameter pp in aps) cs.Append(' ').Append(pp.name);
                        cs.Append("  comps:");
                        for (Transform ct = a.transform; ct != null && ct != a.transform.root; ct = ct.parent)
                            foreach (MonoBehaviour mb in ct.GetComponents<MonoBehaviour>()) if (mb != null) cs.Append(' ').Append(ct.name).Append(':').Append(mb.GetType().Name);
                        clips[cn] = cs.ToString();
                    }
                    CanvasGroup cg = a.GetComponentInParent<CanvasGroup>();
                    int[] sv; if (!shown.TryGetValue(cn, out sv)) shown[cn] = sv = new int[2];
                    sv[0]++; if (cg == null || cg.alpha > 0f) sv[1]++;
                }
                foreach (KeyValuePair<string, string> kv in clips) sb.AppendLine("  " + kv.Key + " n=" + shown[kv.Key][0] + " shown=" + shown[kv.Key][1] + " :" + kv.Value);
                sb.AppendLine("SCRIPTS (light/firefly/fx-like, active)");
                Emit(sb, g);
                foreach (Camera c in Camera.allCameras)
                {
                    sb.Append("CAMERA ").Append(c.name).Append(" path=").Append(c.actualRenderingPath).Append(" hdr=").Append(c.allowHDR).Append(" far=").Append(c.farClipPlane.ToString("F0")).Append(" fx:");
                    foreach (MonoBehaviour m in c.GetComponents<MonoBehaviour>()) if (m != null) sb.Append(' ').Append(m.GetType().Name).Append(m.enabled ? "" : "(off)");
                    sb.AppendLine();
                }
                File.WriteAllText(Path.Combine(BtcDevPaths.LogDir, "vfx-" + tag + ".txt"), sb.ToString());
                BtcLog.Info("Engine", "vfxdump " + tag + " lights=" + li.Length + " ps=" + ps.Length);
            }
            catch (Exception e) { BtcLog.Error("Engine", "vfxdump: " + e); }
        }

        private static void Emit(StringBuilder sb, Dictionary<string, int[]> g)
        {
            List<KeyValuePair<string, int[]>> l = new List<KeyValuePair<string, int[]>>(g);
            l.Sort((a, b) => b.Value[b.Value.Length > 1 ? 1 : 0].CompareTo(a.Value[a.Value.Length > 1 ? 1 : 0]));
            foreach (KeyValuePair<string, int[]> p in l)
            {
                sb.Append("  ");
                for (int i = 0; i < p.Value.Length; i++) sb.Append(p.Value[i]).Append(i == 0 ? (p.Value.Length > 1 ? " on=" : "") : i == 1 && p.Value.Length > 2 ? " particles=" : "").Append(' ');
                sb.Append(p.Key).AppendLine();
            }
        }
        private static void Sweep()
        {
            if (_partOff)
            {
                ParticleSystem[] ps = UnityEngine.Object.FindObjectsOfType<ParticleSystem>();
                for (int i = 0; i < ps.Length; i++)
                {
                    ParticleSystem s = ps[i]; if (s == null) continue;
                    int id = s.GetInstanceID();
                    if (!PsPlaying.ContainsKey(id)) PsPlaying[id] = s.isPlaying;
                    if (s.isPlaying || s.particleCount > 0) { s.Stop(); s.Clear(); }
                    Renderer r = s.GetComponent<Renderer>(); if (r != null) r.enabled = false;
                }
            }
            if (_animOff)
            {
                Animator[] an = UnityEngine.Object.FindObjectsOfType<Animator>();
                for (int i = 0; i < an.Length; i++)
                {
                    if (an[i] == null || (_animOther && an[i].GetComponentInParent<Brix.Game.AI.Locomotion4>() != null)) continue;
                    int id = an[i].GetInstanceID();
                    if (!AnEnabled.ContainsKey(id)) AnEnabled[id] = an[i].enabled;
                    an[i].enabled = false;
                }
            }
            if (_lightOff)
            {
                Light[] li = UnityEngine.Object.FindObjectsOfType<Light>();
                for (int i = 0; i < li.Length; i++)
                {
                    if (li[i] == null) continue;
                    int id = li[i].GetInstanceID();
                    if (!LiEnabled.ContainsKey(id)) LiEnabled[id] = li[i].enabled;
                    li[i].enabled = false;
                }
            }
        }

        private static void RestoreParticles()
        {
            ParticleSystem[] ps = UnityEngine.Object.FindObjectsOfType<ParticleSystem>();
            for (int i = 0; i < ps.Length; i++)
            {
                ParticleSystem s = ps[i]; if (s == null) continue;
                Renderer r = s.GetComponent<Renderer>(); if (r != null) r.enabled = true;
                bool was; if (PsPlaying.TryGetValue(s.GetInstanceID(), out was) && was) s.Play();
            }
            PsPlaying.Clear();
        }

        private static void RestoreAnimators()
        {
            Animator[] an = UnityEngine.Object.FindObjectsOfType<Animator>();
            for (int i = 0; i < an.Length; i++)
            {
                if (an[i] == null) continue;
                bool was; if (AnEnabled.TryGetValue(an[i].GetInstanceID(), out was)) an[i].enabled = was;
            }
            AnEnabled.Clear();
        }

        private static void RestoreLights()
        {
            Light[] li = UnityEngine.Object.FindObjectsOfType<Light>();
            for (int i = 0; i < li.Length; i++)
            {
                if (li[i] == null) continue;
                bool was; if (LiEnabled.TryGetValue(li[i].GetInstanceID(), out was)) li[i].enabled = was;
            }
            LiEnabled.Clear();
        }

        private static void SetRender(bool off)
        {
            if (off == _renderOff) return;
            _renderOff = off;
            Camera[] cs = Camera.allCameras;
            for (int i = 0; i < cs.Length; i++)
            {
                if (cs[i] == null) continue;
                int id = cs[i].GetInstanceID();
                if (off) { CamMask[id] = cs[i].cullingMask; cs[i].cullingMask = 0; }
                else { int m; if (CamMask.TryGetValue(id, out m)) cs[i].cullingMask = m; }
            }
            if (!off) CamMask.Clear();
        }

        // Physics.autoSimulation does not exist in 5.6: starve the fixed step instead (fixedDeltaTime large, no catch-up).
        private static void SetPhysics(bool off)
        {
            if (off == _physOff) return;
            _physOff = off;
            if (off) { _origFixed = Time.fixedDeltaTime; _origMaxDt = Time.maximumDeltaTime; Time.maximumDeltaTime = 0.05f; Time.fixedDeltaTime = 10f; }
            else { Time.fixedDeltaTime = _origFixed; Time.maximumDeltaTime = _origMaxDt; }
        }

        private static void SetShadows(bool off)
        {
            if (off) { if (!_shadowsSaved) { _origShadows = QualitySettings.shadows; _shadowsSaved = true; } QualitySettings.shadows = ShadowQuality.Disable; }
            else if (_shadowsSaved) { QualitySettings.shadows = _origShadows; _shadowsSaved = false; }
        }
    }

    [DefaultExecutionOrder(-32000)]
    internal sealed class BtcDevEngEarly : MonoBehaviour
    {
        private void FixedUpdate() { BtcDevEngine.MarkFixed(); }
        private void Update() { BtcDevEngine.MarkUpd0(); }
        private void LateUpdate() { BtcDevEngine.MarkLate0(); }
    }

    [DefaultExecutionOrder(32000)]
    internal sealed class BtcDevEngLate : MonoBehaviour
    {
        private void Update() { BtcDevEngine.MarkUpd1(); }
        private void LateUpdate() { BtcDevEngine.MarkLate1(); }
    }
}
#endif
