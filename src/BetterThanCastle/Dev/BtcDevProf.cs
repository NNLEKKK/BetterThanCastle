#if DEV
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Brix.Game.AI;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV only profiler. Enabled by env BTC_PROF=1. Frame stats (ring buffer), data-driven method timing
    /// (docs/perf/targets/*.txt, lines "Type:Method  # why"), spike attribution, cheap counters, idle-worker detector.
    /// Output: BTClogs/prof.txt (fixed format, &lt;=60 lines) on Dump(tag).
    /// Env: BTC_PROF=1, BTC_PROF_DUMP_AT=&lt;sec since start&gt; (then every 60 s), BTC_PROF_SECONDS=&lt;sec&gt; (final dump + Stop), BTC_REPO=&lt;repo root&gt;.
    /// Limits: nested/recursive timed calls double count in totals; per-frame attribution is approximate (frame = interval
    /// between two Updates of the profiler behaviour); worker-thread calls are timed but attributed to the current frame bucket.
    /// </summary>
    internal static class BtcDevProf
    {
        private const int Ring = 4096;
        private const int MaxIds = 1024;
        private const float SpikeMs = 50f;

        private sealed class Spike { public float Ms; public int Frame; public float Time; public string Culprits; }

        // ---- timing tables (index = id) ----
        private static readonly Dictionary<MethodBase, int> Ids = new Dictionary<MethodBase, int>();
        private static readonly string[] Names = new string[MaxIds];
        private static readonly long[] Calls = new long[MaxIds];
        private static readonly long[] Total = new long[MaxIds];
        private static readonly long[] Max = new long[MaxIds];
        private static readonly long[] FrameTicks = new long[MaxIds];
        private static readonly int[] FrameFlag = new int[MaxIds];
        private static readonly int[] Touched = new int[MaxIds];
        private static int _touchedCount;
        private static int _idCount;

        // ---- frame stats ----
        private static readonly float[] Frames = new float[Ring];
        private static int _frameIdx;
        private static long _frames;
        private static double _sumDt;
        private static float _maxDt;
        private static long _over33, _over100;
        private static double _namedMs; // approx: per frame min(frame ms, sum of timed-method ms); nesting overcounts
        private static readonly Spike[] Spikes = new Spike[10];

        // ---- state ----
        internal static volatile bool Running;
        private static bool _inited;
        private static float _startReal;
        private static int _gcStart;
        private static long _swStart;
        private static double _overheadTicksPerPair;
        private static int _targetsOk, _targetsFail;
        private static string _unresolved = string.Empty;
        private static int _units, _idleN;
        // GC attribution + counters
        private static int _gc0Prev, _gc1Prev, _gc2Prev, _gc0Start, _gc1Start, _gc2Start;
        private static long _memPrev, _allocBytes;
        private static long _gcFrames; private static double _gcFrameMs;
        private static int _procs, _particleSystems, _particlesAlive, _rigidActive, _rigidTotal, _structs;
        private static string _structDetail = string.Empty;
        private static readonly Dictionary<string, int> IdleReasons = new Dictionary<string, int>();
        private static readonly Dictionary<int, float> IdleSince = new Dictionary<int, float>();
        private static bool _patched;
        private static GameObject _go;

        internal static float StartReal { get { return _startReal; } }

        // ================= public API =================
        internal static void Init()
        {
            if (Environment.GetEnvironmentVariable("BTC_PROF") != "1") return;
            BtcDevEngine.Init();
            if (_inited) return;
            _inited = true;
            try
            {
                _go = new GameObject("BtcDevProf");
                UnityEngine.Object.DontDestroyOnLoad(_go);
                _go.hideFlags = HideFlags.HideAndDontSave;
                _go.AddComponent<BtcDevProfBehaviour>();
                Start();
                BtcLog.Info("Prof", "armed (targets patched after 10 s)");
            }
            catch (Exception e) { BtcLog.Error("Prof", "init failed: " + e.Message); }
        }

        internal static void Start() { Reset(); Running = true; }
        internal static void Stop() { Running = false; }

        internal static void Reset()
        {
            BtcDevLuaProf.Reset();
            BtcDevEngine.ResetProf();
            for (int i = 0; i < MaxIds; i++) { Calls[i] = 0; Total[i] = 0; Max[i] = 0; FrameTicks[i] = 0; FrameFlag[i] = 0; }
            _touchedCount = 0;
            Array.Clear(Frames, 0, Ring);
            _frameIdx = 0; _frames = 0; _sumDt = 0; _maxDt = 0; _over33 = 0; _over100 = 0; _namedMs = 0;
            for (int i = 0; i < Spikes.Length; i++) Spikes[i] = null;
            _startReal = Time.realtimeSinceStartup;
            _gcStart = GC.CollectionCount(0);
            _gc0Start = _gc0Prev = GC.CollectionCount(0); _gc1Start = _gc1Prev = GC.CollectionCount(1); _gc2Start = _gc2Prev = GC.CollectionCount(2);
            _memPrev = GC.GetTotalMemory(false); _allocBytes = 0; _gcFrames = 0; _gcFrameMs = 0;
            _swStart = Stopwatch.GetTimestamp();
        }

        // ================= patching =================
        internal static void PatchTargets()
        {
            if (_patched) return;
            _patched = true;
            try
            {
                Calibrate();
                BtcDevLuaProf.Patch();
                string root = FindRepo();
                if (root == null) { BtcLog.Warn("Prof", "no docs/perf/targets found (set BTC_REPO)"); return; }
                string dir = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(root, "docs"), "perf"), "targets");
                if (!Directory.Exists(dir)) { BtcLog.Warn("Prof", "no targets dir " + dir); return; }

                Dictionary<string, Type> types = BuildTypeMap();
                Harmony h = new Harmony("com.btc.prof");
                HarmonyMethod pre = new HarmonyMethod(typeof(BtcDevProf).GetMethod("Pre", BindingFlags.NonPublic | BindingFlags.Static));
                HarmonyMethod post = new HarmonyMethod(typeof(BtcDevProf).GetMethod("Post", BindingFlags.NonPublic | BindingFlags.Static));
                HashSet<string> seen = new HashSet<string>();
                List<string> bad = new List<string>();

                string[] files = Directory.GetFiles(dir, "*.txt");
                Array.Sort(files);
                for (int f = 0; f < files.Length; f++)
                {
                    string[] lines = File.ReadAllLines(files[f]);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        int hash = line.IndexOf('#');
                        if (hash >= 0) line = line.Substring(0, hash);
                        line = line.Trim();
                        if (line.Length == 0) continue;
                        if (!seen.Add(line)) continue;
                        int colon = line.LastIndexOf(':');
                        if (colon <= 0 || colon >= line.Length - 1) { bad.Add(line); _targetsFail++; continue; }
                        string tn = line.Substring(0, colon).Trim();
                        string mn = line.Substring(colon + 1).Trim();
                        int n = 0;
                        try { n = PatchOne(h, pre, post, types, tn, mn); }
                        catch (Exception e) { BtcLog.Warn("Prof", "patch " + line + ": " + e.Message); }
                        if (n > 0) _targetsOk++; else { _targetsFail++; bad.Add(line); }
                    }
                }
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < bad.Count && i < 10; i++) { if (i > 0) sb.Append(", "); sb.Append(bad[i]); }
                _unresolved = sb.ToString();
                BtcLog.Info("Prof", "targets ok=" + _targetsOk + " unresolved=" + _targetsFail + " methods=" + _idCount + (bad.Count > 0 ? " first: " + _unresolved : string.Empty));
            }
            catch (Exception e) { BtcLog.Error("Prof", "PatchTargets failed: " + e); }
        }

        private static string FindRepo()
        {
            string env = Environment.GetEnvironmentVariable("BTC_REPO");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(env, "docs"), "perf"), "targets"))) return env;
            string d = BtcDevPaths.LogDir;
            for (int i = 0; i < 6 && !string.IsNullOrEmpty(d); i++)
            {
                if (Directory.Exists(System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(d, "docs"), "perf"), "targets"))) return d;
                d = System.IO.Path.GetDirectoryName(d);
            }
            return null;
        }

        private static Dictionary<string, Type> BuildTypeMap()
        {
            Dictionary<string, Type> map = new Dictionary<string, Type>();
            Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < asms.Length; a++)
            {
                string an = asms[a].GetName().Name;
                if (an != "Assembly-CSharp-firstpass" && an != "Assembly-CSharp") continue;
                Type[] ts;
                try { ts = asms[a].GetTypes(); }
                catch (ReflectionTypeLoadException e) { ts = e.Types; }
                for (int i = 0; i < ts.Length; i++)
                {
                    Type t = ts[i];
                    if (t == null || t.IsNested) continue;
                    if (!map.ContainsKey(t.Name)) map[t.Name] = t;
                    if (t.FullName != null && !map.ContainsKey(t.FullName)) map[t.FullName] = t;
                }
            }
            return map;
        }

        private static int PatchOne(Harmony h, HarmonyMethod pre, HarmonyMethod post, Dictionary<string, Type> types, string tn, string mn)
        {
            Type t;
            if (!types.TryGetValue(tn, out t)) t = AccessTools.TypeByName(tn);
            if (t == null) return 0;
            BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            List<MethodBase> list = new List<MethodBase>();
            if (mn == "ctor" || mn == ".ctor")
            {
                ConstructorInfo[] cs = t.GetConstructors(bf);
                for (int i = 0; i < cs.Length; i++) list.Add(cs[i]);
            }
            else
            {
                MethodInfo[] ms = t.GetMethods(bf);
                for (int i = 0; i < ms.Length; i++)
                {
                    MethodInfo m = ms[i];
                    if (m.Name != mn || m.IsAbstract) continue;
                    Type rt = m.ReturnType;
                    bool enumerator = rt == typeof(System.Collections.IEnumerator) || rt == typeof(System.Collections.IEnumerable)
                        || (rt.IsGenericType && (rt.GetGenericTypeDefinition() == typeof(IEnumerator<>) || rt.GetGenericTypeDefinition() == typeof(IEnumerable<>)));
                    if (enumerator)
                    {
                        int before = list.Count;
                        Type[] nested = t.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public);
                        for (int k = 0; k < nested.Length; k++)
                        {
                            if (!nested[k].Name.StartsWith("<" + mn + ">c__Iterator")) continue;
                            MethodInfo mv = nested[k].GetMethod("MoveNext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                            if (mv != null) list.Add(mv);
                        }
                        if (list.Count > before) continue; // timed via MoveNext; the stub only allocates the iterator
                    }
                    list.Add(m);
                }
            }
            int ok = 0;
            for (int i = 0; i < list.Count; i++)
            {
                MethodBase mb = list[i];
                if (mb.ContainsGenericParameters || mb.IsAbstract || Ids.ContainsKey(mb) || _idCount >= MaxIds) continue;
                string name;
                if (mb.DeclaringType != null && mb.DeclaringType.IsNested && mb.DeclaringType.Name.StartsWith("<"))
                    name = t.Name + "." + mn + "+MoveNext";
                else name = t.Name + "." + mb.Name;
                int id = _idCount;
                try
                {
                    h.Patch(mb, prefix: pre, postfix: post);
                    Names[id] = name;
                    Ids[mb] = id;
                    _idCount++;
                    ok++;
                }
                catch (Exception e) { BtcLog.Warn("Prof", "cannot patch " + name + ": " + e.Message); }
            }
            return ok;
        }

        // ================= hot path =================
        private static void Pre(out long __state)
        {
            __state = Running ? Stopwatch.GetTimestamp() : 0L;
        }

        private static void Post(MethodBase __originalMethod, long __state)
        {
            if (__state == 0L) return;
            long d = Stopwatch.GetTimestamp() - __state;
            int id;
            if (!Ids.TryGetValue(__originalMethod, out id)) return;
            Interlocked.Increment(ref Calls[id]);
            Interlocked.Add(ref Total[id], d);
            if (d > Max[id]) Max[id] = d;
            Interlocked.Add(ref FrameTicks[id], d);
            if (Interlocked.CompareExchange(ref FrameFlag[id], 1, 0) == 0)
            {
                int slot = Interlocked.Increment(ref _touchedCount) - 1;
                if (slot < MaxIds) Touched[slot] = id;
            }
        }

        private static void Calibrate()
        {
            try
            {
                MethodBase dummy = typeof(BtcDevProf).GetMethod("Calibrate", BindingFlags.NonPublic | BindingFlags.Static);
                int id = _idCount; // temporary slot, never used for output
                Ids[dummy] = id;
                bool was = Running; Running = true;
                const int N = 50000;
                long t0 = Stopwatch.GetTimestamp();
                for (int i = 0; i < N; i++) { long s; Pre(out s); Post(dummy, s); }
                long t1 = Stopwatch.GetTimestamp();
                Running = was;
                _overheadTicksPerPair = (double)(t1 - t0) / N;
                Ids.Remove(dummy);
                Calls[id] = 0; Total[id] = 0; Max[id] = 0; FrameTicks[id] = 0; FrameFlag[id] = 0; _touchedCount = 0;
            }
            catch (Exception e) { BtcLog.Warn("Prof", "calibrate: " + e.Message); }
        }

        // ================= per frame (called from behaviour.Update) =================
        internal static void OnFrame(float dtSec, float now)
        {
            if (!Running) return;
            float ms = dtSec * 1000f;
            Frames[_frameIdx] = ms;
            _frameIdx = (_frameIdx + 1) % Ring;
            _frames++;
            _sumDt += dtSec;
            if (ms > _maxDt) _maxDt = ms;
            if (ms > 33f) _over33++;
            if (ms > 100f) _over100++;

            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            string gcTag = null;
            if (g0 != _gc0Prev || g1 != _gc1Prev || g2 != _gc2Prev)
            {
                gcTag = g2 != _gc2Prev ? "GC(gen2)" : (g1 != _gc1Prev ? "GC(gen1)" : "GC(gen0)");
                _gcFrames++; _gcFrameMs += ms;
                _gc0Prev = g0; _gc1Prev = g1; _gc2Prev = g2;
            }
            long mem = GC.GetTotalMemory(false);
            if (mem > _memPrev) _allocBytes += mem - _memPrev;
            _memPrev = mem;

            int cnt = _touchedCount; if (cnt > MaxIds) cnt = MaxIds;
            if (ms > SpikeMs && (cnt > 0 || gcTag != null)) RecordSpike(ms, now, cnt, gcTag);
            double namedFrame = 0;
            for (int i = 0; i < cnt; i++) namedFrame += Ms(FrameTicks[Touched[i]]);
            _namedMs += namedFrame < ms ? namedFrame : ms;
            for (int i = 0; i < cnt; i++)
            {
                int id = Touched[i];
                FrameTicks[id] = 0;
                FrameFlag[id] = 0;
            }
            _touchedCount = 0;
        }

        private static void RecordSpike(float ms, float now, int cnt, string gcTag)
        {
            // find slot: empty or smaller than ms
            int slot = -1; float minMs = float.MaxValue;
            for (int i = 0; i < Spikes.Length; i++)
            {
                if (Spikes[i] == null) { slot = i; minMs = -1f; break; }
                if (Spikes[i].Ms < minMs) { minMs = Spikes[i].Ms; slot = i; }
            }
            if (slot < 0 || ms <= minMs) return;
            int a = -1, b = -1, c = -1;
            long ta = 0, tb = 0, tc = 0;
            for (int i = 0; i < cnt; i++)
            {
                int id = Touched[i];
                long v = FrameTicks[id];
                if (v > ta) { c = b; tc = tb; b = a; tb = ta; a = id; ta = v; }
                else if (v > tb) { c = b; tc = tb; b = id; tb = v; }
                else if (v > tc) { c = id; tc = v; }
            }
            StringBuilder sb = new StringBuilder();
            if (gcTag != null) sb.Append(gcTag);
            AppendCulprit(sb, a, ta); AppendCulprit(sb, b, tb); AppendCulprit(sb, c, tc);
            Spike s = new Spike();
            s.Ms = ms; s.Frame = (int)_frames; s.Time = now - _startReal; s.Culprits = sb.Length == 0 ? "-" : sb.ToString();
            Spikes[slot] = s;
        }

        private static void AppendCulprit(StringBuilder sb, int id, long ticks)
        {
            if (id < 0 || ticks <= 0) return;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(Names[id]).Append('=').Append(Ms(ticks).ToString("0.#"));
        }

        private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }

        // ================= counters / idle (every 5 s) =================
        internal static void SlowTick(float now)
        {
            if (BtcDevEngine.NoCounters) return;
            try
            {
                Labor[] ls = UnityEngine.Object.FindObjectsOfType<Labor>();
                _units = ls.Length;
                _procs = UnityEngine.Object.FindObjectsOfType<Profession>().Length;
                ParticleSystem[] pss = UnityEngine.Object.FindObjectsOfType<ParticleSystem>();
                int pAlive = 0;
                for (int i = 0; i < pss.Length; i++) if (pss[i] != null) pAlive += pss[i].particleCount;
                _particleSystems = pss.Length; _particlesAlive = pAlive;
                Rigidbody[] rbs = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
                int act = 0;
                for (int i = 0; i < rbs.Length; i++) if (rbs[i] != null && !rbs[i].isKinematic && !rbs[i].IsSleeping()) act++;
                _rigidTotal = rbs.Length; _rigidActive = act;
                UnityEngine.Networking.NetworkIdentity[] nis = UnityEngine.Object.FindObjectsOfType<UnityEngine.Networking.NetworkIdentity>();
                int st = 0; Dictionary<string, int> tally = new Dictionary<string, int>();
                for (int i = 0; i < nis.Length; i++)
                {
                    if (nis[i] == null) continue;
                    string nm = nis[i].gameObject.name;
                    int cl = nm.IndexOf(" (Clone)"); if (cl > 0) nm = nm.Substring(0, cl);
                    if (nm != "Palette" && nm != "Chest" && nm != "Toolrack" && nm != "SingleToolrack" && nm != "Workbench" && nm != "Furnace" && nm != "Forge" && nm != "Loom" && nm != "Catapulte") continue;
                    st++; int c; tally.TryGetValue(nm, out c); tally[nm] = c + 1;
                }
                _structs = st;
                StringBuilder tb = new StringBuilder();
                foreach (KeyValuePair<string, int> kv in tally) { if (tb.Length > 0) tb.Append(','); tb.Append(kv.Key).Append('=').Append(kv.Value); if (tb.Length > 160) break; }
                _structDetail = tb.ToString();
                IdleReasons.Clear();
                int idle = 0;
                HashSet<int> alive = new HashSet<int>();
                for (int i = 0; i < ls.Length; i++)
                {
                    Labor l = ls[i];
                    if (l == null) continue;
                    int iid = l.GetInstanceID();
                    alive.Add(iid);
                    bool noTask;
                    try { noTask = !l.IsPhasedOut && l.GetCurrentOrPendingTask() == null; }
                    catch (Exception) { noTask = false; }
                    if (!noTask) { IdleSince.Remove(iid); continue; }
                    float since;
                    if (!IdleSince.TryGetValue(iid, out since)) { IdleSince[iid] = now; continue; }
                    if (now - since < 5f) continue;
                    // Limitation: "no current/pending task" also holds for units waiting on player orders (Autonomy-driven). Reason tags only.
                    string r = l.HasGoal() ? "goal-no-task" : (l.Project != null ? "project-no-task" : "no-job");
                    int n; IdleReasons.TryGetValue(r, out n); IdleReasons[r] = n + 1;
                    idle++;
                }
                _idleN = idle;
                List<int> stale = null;
                foreach (KeyValuePair<int, float> kv in IdleSince)
                    if (!alive.Contains(kv.Key)) { if (stale == null) stale = new List<int>(); stale.Add(kv.Key); }
                if (stale != null) for (int i = 0; i < stale.Count; i++) IdleSince.Remove(stale[i]);
            }
            catch (Exception e) { BtcLog.Warn("Prof", "slow tick: " + e.Message); }
        }

        // ================= dump =================
        internal static void Dump(string tag)
        {
            try
            {
                float now = Time.realtimeSinceStartup;
                double t = now - _startReal; if (t < 0.001) t = 0.001;
                int n = (int)Math.Min(_frames, Ring);
                float[] copy = new float[n];
                Array.Copy(Frames, copy, n);
                Array.Sort(copy);
                double p50 = n > 0 ? copy[n / 2] : 0, p99 = n > 0 ? copy[Math.Min(n - 1, (int)(n * 0.99))] : 0;
                double fps = _sumDt > 0 ? _frames / _sumDt : 0;
                double gcs = (GC.CollectionCount(0) - _gcStart) / t;
                double mem = GC.GetTotalMemory(false) / 1048576.0;
                long pairs = 0;
                for (int i = 0; i < _idCount; i++) pairs += Calls[i];
                double wallTicks = Stopwatch.GetTimestamp() - _swStart; if (wallTicks < 1) wallTicks = 1;
                double ovh = pairs * _overheadTicksPerPair * 100.0 / wallTicks;

                StringBuilder sb = new StringBuilder(4096);
                sb.Append("PROF ").Append(tag).Append(" t=").Append(t.ToString("0")).Append(" frames=").Append(_frames)
                  .Append(" fps=").Append(fps.ToString("0.0")).Append(" p50=").Append(p50.ToString("0.0")).Append(" p99=").Append(p99.ToString("0.0"))
                  .Append(" max=").Append(_maxDt.ToString("0.0")).Append(" >33ms=").Append(_over33).Append(" >100ms=").Append(_over100)
                  .Append(" gc0/s=").Append(gcs.ToString("0.00")).Append(" gc0/1/2=").Append(GC.CollectionCount(0) - _gc0Start).Append('/').Append(GC.CollectionCount(1) - _gc1Start).Append('/').Append(GC.CollectionCount(2) - _gc2Start)
                  .Append(" gcframes=").Append(_gcFrames).Append('/').Append(_gcFrameMs.ToString("0")).Append("ms alloc_KB/s=").Append((_allocBytes / 1024.0 / t).ToString("0")).Append(" mem=").Append(mem.ToString("0")).Append(" overhead=").Append(ovh.ToString("0.0")).Append(" named=").Append((_sumDt > 0 ? _namedMs * 100.0 / (_sumDt * 1000.0) : 0).ToString("0")).Append("%").AppendLine();

                BtcDevEngine.Append(sb);
                string netLine = BtcDevNet.Line("prof"); if (netLine.Length > 0) sb.AppendLine(netLine);
                int[] order = SortedIds(false);
                sb.AppendLine("TOP total  (ms_total calls avg_ms max_ms name)");
                for (int i = 0; i < 20; i++) AppendRow(sb, i < order.Length ? order[i] : -1);
                order = SortedIds(true);
                sb.AppendLine("TOP max");
                for (int i = 0; i < 10; i++) AppendRow(sb, i < order.Length ? order[i] : -1);

                sb.AppendLine("SPIKES  (frame_ms frame# t_sec culprits Type.Method=ms)");
                Spike[] sp = (Spike[])Spikes.Clone();
                Array.Sort(sp, delegate (Spike x, Spike y) { float a = x == null ? -1f : x.Ms, b = y == null ? -1f : y.Ms; return b.CompareTo(a); });
                for (int i = 0; i < sp.Length; i++)
                {
                    if (sp[i] == null) continue;
                    sb.Append(sp[i].Ms.ToString("0.0")).Append(' ').Append(sp[i].Frame).Append(' ').Append(sp[i].Time.ToString("0")).Append(' ').Append(sp[i].Culprits).AppendLine();
                }

                sb.AppendLine("COUNTERS");
                sb.Append("units=").Append(_units).Append(" professions=").Append(_procs).Append(" particleSystems=").Append(_particleSystems).Append(" particles=").Append(_particlesAlive).Append(" rigid_active=").Append(_rigidActive).Append("/").Append(_rigidTotal).Append(" structs=").Append(_structs).Append(" [").Append(_structDetail).Append("] targets_ok=").Append(_targetsOk).Append(" targets_unresolved=").Append(_targetsFail).Append(" timed_methods=").Append(_idCount).AppendLine();
                if (_unresolved.Length > 0) sb.Append("unresolved: ").AppendLine(_unresolved.Length > 300 ? _unresolved.Substring(0, 300) : _unresolved);
                sb.Append("path:");
                bool any = false;
                for (int i = 0; i < _idCount; i++)
                {
                    string nm = Names[i];
                    if (nm == null || Calls[i] == 0) continue;
                    if (nm.IndexOf("Path", StringComparison.Ordinal) < 0 && nm.IndexOf("Navigation", StringComparison.Ordinal) < 0 && nm.IndexOf("Resolver", StringComparison.Ordinal) < 0) continue;
                    sb.Append(' ').Append(nm).Append('=').Append(Calls[i]).Append('/').Append(Ms(Total[i]).ToString("0")).Append("ms");
                    any = true;
                }
                if (!any) sb.Append(" -");
                sb.AppendLine();

                sb.AppendLine("IDLE  (units without task >5 s, by reason)");
                sb.Append("idle=").Append(_idleN).Append('/').Append(_units);
                foreach (KeyValuePair<string, int> kv in IdleReasons) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
                sb.AppendLine();

                BtcDevLuaProf.Append(sb, _frames, _sumDt);
                Directory.CreateDirectory(BtcDevPaths.LogDir);
                File.WriteAllText(System.IO.Path.Combine(BtcDevPaths.LogDir, "prof.txt"), sb.ToString());
                if (!string.IsNullOrEmpty(tag) && tag != "auto" && tag != "quit") File.WriteAllText(System.IO.Path.Combine(BtcDevPaths.LogDir, "prof-" + tag + ".txt"), sb.ToString());
                BtcLog.Info("Prof", "dumped " + tag + " fps=" + fps.ToString("0.0") + " p99=" + p99.ToString("0.0"));
            }
            catch (Exception e) { BtcLog.Error("Prof", "dump failed: " + e.Message); }
        }

        private static int[] SortedIds(bool byMax)
        {
            List<int> l = new List<int>();
            for (int i = 0; i < _idCount; i++) if (Calls[i] > 0) l.Add(i);
            long[] key = byMax ? Max : Total;
            l.Sort(delegate (int a, int b) { return key[b].CompareTo(key[a]); });
            return l.ToArray();
        }

        private static void AppendRow(StringBuilder sb, int id)
        {
            if (id < 0) return;
            long c = Calls[id];
            double tot = Ms(Total[id]);
            sb.Append(tot.ToString("0.0")).Append(' ').Append(c).Append(' ').Append((c > 0 ? tot / c : 0).ToString("0.000")).Append(' ')
              .Append(Ms(Max[id]).ToString("0.00")).Append(' ').AppendLine(Names[id]);
        }
    }

    internal sealed class BtcDevProfBehaviour : MonoBehaviour
    {
        private float _nextSlow, _nextDump = -1f;
        private bool _patchDone, _finalDone;
        private float _dumpAt = -1f, _limit = -1f;

        private void Awake()
        {
            float v;
            if (float.TryParse(Environment.GetEnvironmentVariable("BTC_PROF_DUMP_AT") ?? string.Empty, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) _dumpAt = v;
            if (float.TryParse(Environment.GetEnvironmentVariable("BTC_PROF_SECONDS") ?? string.Empty, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) _limit = v;
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (!_patchDone && now > 10f) { _patchDone = true; BtcDevProf.PatchTargets(); }
            BtcDevProf.OnFrame(Time.unscaledDeltaTime, now);
            if (!BtcDevProf.Running) return;
            float el = now - BtcDevProf.StartReal;
            if (now >= _nextSlow) { _nextSlow = now + 5f; BtcDevProf.SlowTick(now); }
            if (_dumpAt >= 0f)
            {
                if (_nextDump < 0f) _nextDump = _dumpAt;
                if (el >= _nextDump) { _nextDump = el + 60f; BtcDevProf.Dump("auto"); }
            }
            if (_limit >= 0f && !_finalDone && el >= _limit)
            {
                _finalDone = true;
                BtcDevProf.Dump("final");
                BtcDevProf.Stop();
            }
        }

        private void OnApplicationQuit()
        {
            if (BtcDevProf.Running) BtcDevProf.Dump("quit");
        }
    }
}
#endif
