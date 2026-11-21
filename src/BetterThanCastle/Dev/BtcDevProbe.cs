#if DEV
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV only. Puts a transparent Finalizer on every patched game method to count calls and catch exceptions thrown
    /// inside patched methods, then writes BTClogs/hits.txt + last-run.json every 10 s (the smoke test kills the game,
    /// so there is no reliable quit callback). Lets agents see which hooks ran, never ran or threw without a human.
    /// Disable with env BTC_NOPROBE=1. F9 or env BTC_SHOT_AT=<seconds> writes BTClogs/shot.png.
    /// </summary>
    internal static class BtcDevProbe
    {
        private sealed class Counter
        {
            public string Name;
            public int Calls;
            public int Exceptions;
            public string FirstEx;
        }

        private static readonly Dictionary<MethodBase, Counter> Map = new Dictionary<MethodBase, Counter>();
        internal static int Patched;
        internal static int ProbeFailed;

        internal static void Init()
        {
            if (Environment.GetEnvironmentVariable("BTC_NOPROBE") == "1") return;
            try
            {
                Harmony probe = new Harmony(BtcPlugin.Guid + ".probe");
                HarmonyMethod fin = new HarmonyMethod(typeof(BtcDevProbe).GetMethod("Finalizer", BindingFlags.NonPublic | BindingFlags.Static));
                List<MethodBase> methods = new List<MethodBase>(Harmony.GetAllPatchedMethods());
                foreach (MethodBase m in methods)
                {
                    try
                    {
                        Counter c = new Counter();
                        c.Name = (m.DeclaringType != null ? m.DeclaringType.Name + "." : string.Empty) + m.Name;
                        Map[m] = c;
                        probe.Patch(m, finalizer: fin);
                        Patched++;
                    }
                    catch (Exception)
                    {
                        Map.Remove(m);
                        ProbeFailed++;
                    }
                }
                GameObject go = new GameObject("BtcDevProbe");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<BtcDevProbeBehaviour>();
                BtcLog.Info("Probe", "counting " + Patched + " patched methods (probe failed on " + ProbeFailed + ")");
            }
            catch (Exception e)
            {
                BtcLog.Error("Probe", "init failed: " + e);
            }
        }

        // Transparent: counts and rethrows. __originalMethod is the patched game method.
        private static Exception Finalizer(MethodBase __originalMethod, Exception __exception)
        {
            Counter c;
            if (Map.TryGetValue(__originalMethod, out c))
            {
                c.Calls++;
                if (__exception != null)
                {
                    c.Exceptions++;
                    if (c.FirstEx == null) c.FirstEx = __exception.GetType().Name + ": " + __exception.Message;
                }
            }
            return __exception;
        }

        internal static void Flush()
        {
            try
            {
                int called = 0, exc = 0;
                List<Counter> all = new List<Counter>(Map.Values);
                foreach (Counter c in all) { if (c.Calls > 0) called++; exc += c.Exceptions; }
                all.Sort(delegate (Counter a, Counter b) { return b.Calls.CompareTo(a.Calls); });
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# patched=" + Map.Count + " called=" + called + " never=" + (Map.Count - called) + " exceptions=" + exc);
                sb.AppendLine("# calls\texceptions\tmethod\tfirst exception");
                foreach (Counter c in all) if (c.Exceptions > 0) sb.AppendLine(c.Calls + "\t" + c.Exceptions + "\t" + c.Name + "\t" + c.FirstEx);
                sb.AppendLine("# --- called without exceptions");
                foreach (Counter c in all) if (c.Calls > 0 && c.Exceptions == 0) sb.AppendLine(c.Calls + "\t0\t" + c.Name);
                sb.AppendLine("# --- never called");
                foreach (Counter c in all) if (c.Calls == 0) sb.AppendLine("0\t0\t" + c.Name);
                File.WriteAllText(Path.Combine(BtcDevPaths.LogDir, "hits.txt"), sb.ToString());
                File.WriteAllText(Path.Combine(BtcDevPaths.LogDir, "last-run.json"),
                    "{\"time\":\"" + DateTime.Now.ToString("s") + "\",\"patchOk\":" + BtcPatcher.Ok + ",\"patchFail\":" + BtcPatcher.Failed
                    + ",\"patchedMethods\":" + Map.Count + ",\"called\":" + called + ",\"never\":" + (Map.Count - called)
                    + ",\"hookExceptions\":" + exc + "}");
            }
            catch (Exception) { }
        }
    }

    internal sealed class BtcDevProbeBehaviour : MonoBehaviour
    {
        private float _next = 10f;
        private float _shotAt = -1f;
        private bool _shotDone;

        private void Awake()
        {
            string s = Environment.GetEnvironmentVariable("BTC_SHOT_AT");
            float v;
            if (!string.IsNullOrEmpty(s) && float.TryParse(s, out v)) _shotAt = v;
        }

        private void Update()
        {
            float t = Time.realtimeSinceStartup;
            if (t >= _next) { _next = t + 10f; BtcDevProbe.Flush(); }
            if ((Input.GetKeyDown(KeyCode.F9)) || (_shotAt > 0f && !_shotDone && t >= _shotAt))
            {
                _shotDone = true;
                try { Application.CaptureScreenshot(Path.Combine(BtcDevPaths.LogDir, "shot.png")); }
                catch (Exception) { }
            }
        }

        private void OnApplicationQuit()
        {
            BtcDevProbe.Flush();
        }
    }
}
#endif
