using System;
using System.Collections.Generic;
using System.Threading;
using HarmonyLib;

namespace BetterThanCastle
{
    /// <summary>
    /// Applies every [HarmonyPatch] class separately so one failing hook cannot take the others down.
    /// Logs "[Patch] FAIL <class>" per failed class (DEV also "OK"): grep "[Patch] FAIL" after a test run.
    /// Boot speed (cfg Performance/BackgroundPatching): classes listed in BtcPatchLate (targets never called before the
    /// first world load in the labs) are applied on a background thread while the game loads its factories and the menu;
    /// WaitLate() blocks until they are all in (world load begin, DEV probe).
    /// </summary>
    internal static class BtcPatcher
    {
        internal static int Ok;
        internal static int Failed;
        private static readonly object CountLock = new object();
        private static volatile bool _lateDone = true;
        private static readonly ManualResetEvent LateEvent = new ManualResetEvent(true);
        private static double _lateMs;
        private static int _lateN;
#if DEV
        internal static readonly List<string> Applied = new List<string>();
#endif

        internal static bool LateDone { get { return _lateDone; } }

        internal static void Apply(Harmony harmony, System.Reflection.Assembly asm)
        {
            // GetTypesFromAssembly skips types that fail to load instead of throwing ReflectionTypeLoadException:
            // a game update breaking one class must not stop every patch and the rest of Awake (updater included)
            System.Diagnostics.Stopwatch all = System.Diagnostics.Stopwatch.StartNew();
            List<PatchTime> slow = new List<PatchTime>();
            List<Type> late = new List<Type>();
            bool bg = BtcConfig.BackgroundPatching == null || BtcConfig.BackgroundPatching.Value;
            HashSet<string> lateNames = bg ? new HashSet<string>(BtcPatchLate.Classes) : null;
            foreach (Type t in AccessTools.GetTypesFromAssembly(asm))
            {
                if (!t.IsDefined(typeof(HarmonyPatch), true)) continue;
                if (lateNames != null && lateNames.Contains(t.Name)) { late.Add(t); continue; }
                PatchOne(harmony, t, all, slow);
            }
            slow.Sort((a, b) => b.Ms.CompareTo(a.Ms));
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < slow.Count && i < 12; i++) sb.Append(' ').Append(slow[i].Name).Append('=').Append(slow[i].Ms.ToString("0"));
            BtcLog.Info("Patch", "summary ok=" + Ok + " fail=" + Failed + " in " + all.ElapsedMilliseconds + " ms, " + late.Count + " classes to the background; slowest:" + sb);
            if (late.Count == 0) return;
            _lateDone = false; LateEvent.Reset(); _lateN = late.Count;
            Thread th = new Thread(() => RunLate(harmony, late));
            th.IsBackground = true;
            th.Name = "BtcLatePatches";
            th.Start();
        }

        private static void RunLate(Harmony harmony, List<Type> late)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            List<PatchTime> slow = new List<PatchTime>();
            try { foreach (Type t in late) PatchOne(harmony, t, sw, slow); }
            catch (Exception e) { BtcLog.Error("Patch", "background: " + e); }
            _lateMs = sw.Elapsed.TotalMilliseconds;
            _lateDone = true;
            LateEvent.Set();
            BtcLog.Info("Patch", "background done: " + _lateN + " classes in " + _lateMs.ToString("0") + " ms, total ok=" + Ok + " fail=" + Failed);
        }

        /// <summary>Blocks until the background patches are in. Cheap once done.</summary>
        internal static void WaitLate(string why)
        {
            if (_lateDone) return;
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            LateEvent.WaitOne();
            BtcLog.Info("Patch", "waited " + sw.ElapsedMilliseconds + " ms for the background patches (" + why + ")");
        }

        private static void PatchOne(Harmony harmony, Type t, System.Diagnostics.Stopwatch clock, List<PatchTime> slow)
        {
            long t0 = clock.ElapsedTicks;
            try
            {
                harmony.CreateClassProcessor(t).Patch();
                lock (CountLock) Ok++;
#if DEV
                BtcLog.Info("Patch", "OK " + t.Name);
                lock (Applied) Applied.Add(t.Name);
#endif
                slow.Add(new PatchTime(t.Name, (clock.ElapsedTicks - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency));
            }
            catch (Exception e)
            {
                lock (CountLock) Failed++;
                BtcLog.Error("Patch", "FAIL " + t.Name + ": " + e.Message + (e.InnerException != null ? " <- " + e.InnerException.GetType().Name + ": " + e.InnerException.Message : ""));
            }
        }

        private struct PatchTime
        {
            public readonly string Name; public readonly double Ms;
            public PatchTime(string name, double ms) { Name = name; Ms = ms; }
        }
    }
}
