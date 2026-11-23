using System;
using HarmonyLib;

namespace BetterThanCastle
{
    /// <summary>
    /// Applies every [HarmonyPatch] class separately so one failing hook cannot take the others down.
    /// Logs "[Patch] OK|FAIL <class>" per class: grep "[Patch] FAIL" after a test run.
    /// </summary>
    internal static class BtcPatcher
    {
        internal static int Ok;
        internal static int Failed;

        internal static void Apply(Harmony harmony, System.Reflection.Assembly asm)
        {
            // GetTypesFromAssembly skips types that fail to load instead of throwing ReflectionTypeLoadException:
            // a game update breaking one class must not stop every patch and the rest of Awake (updater included)
            System.Diagnostics.Stopwatch all = System.Diagnostics.Stopwatch.StartNew();
            System.Collections.Generic.List<PatchTime> slow = new System.Collections.Generic.List<PatchTime>();
            long logTicks = 0;
            foreach (Type t in AccessTools.GetTypesFromAssembly(asm))
            {
                if (!t.IsDefined(typeof(HarmonyPatch), true)) continue;
                long t0 = all.ElapsedTicks;
                try
                {
                    harmony.CreateClassProcessor(t).Patch();
                    Ok++;
                    long l0 = all.ElapsedTicks; BtcLog.Info("Patch", "OK " + t.Name); logTicks += all.ElapsedTicks - l0;
                    slow.Add(new PatchTime(t.Name, (all.ElapsedTicks - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency));
                }
                catch (Exception e)
                {
                    Failed++;
                    BtcLog.Error("Patch", "FAIL " + t.Name + ": " + e.Message + (e.InnerException != null ? " <- " + e.InnerException.GetType().Name + ": " + e.InnerException.Message : ""));
                }
            }
            slow.Sort((a, b) => b.Ms.CompareTo(a.Ms));
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < slow.Count && i < 12; i++) sb.Append(' ').Append(slow[i].Name).Append('=').Append(slow[i].Ms.ToString("0"));
            BtcLog.Info("Patch", "summary ok=" + Ok + " fail=" + Failed + " in " + all.ElapsedMilliseconds + " ms (log lines " + (logTicks * 1000 / System.Diagnostics.Stopwatch.Frequency) + " ms); slowest:" + sb);
        }

        private struct PatchTime
        {
            public readonly string Name; public readonly double Ms;
            public PatchTime(string name, double ms) { Name = name; Ms = ms; }
        }
    }
}
