#if DEV
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV only, enabled by the file BtcMethodProf.flag next to the game exe: patches every method of the types in the given
    /// namespace prefixes (Prefix + Finalizer) and, while BtcLoadBoost.Loading, accumulates self and inclusive time per method.
    /// Dump() is called at the end of the load. Slow (hook per call): use only to find hot spots, not for absolute times.
    /// </summary>
    internal static class BtcDevMethodProf
    {
        private static readonly Dictionary<MethodBase, int> Ids = new Dictionary<MethodBase, int>();
        private static readonly List<string> Names = new List<string>();
        private static long[] _self = new long[8192];
        private static long[] _incl = new long[8192];
        private static long[] _calls = new long[8192];
        [ThreadStatic] private static long[] _child;
        [ThreadStatic] private static int _d;
        internal static bool On;

        internal static void Init()
        {
            try
            {
                string flag = Path.Combine(Path.GetDirectoryName(Application.dataPath), "BtcMethodProf.flag");
                if (!File.Exists(flag)) return;
                string[] prefixes = File.ReadAllText(flag).Split(new[] { '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (prefixes.Length == 0) prefixes = new[] { "Brix.UI.Builder" };
                Harmony h = new Harmony("com.btc.methodprof");
                HarmonyMethod pre = new HarmonyMethod(typeof(BtcDevMethodProf).GetMethod("Pre", BindingFlags.Static | BindingFlags.NonPublic));
                HarmonyMethod fin = new HarmonyMethod(typeof(BtcDevMethodProf).GetMethod("Fin", BindingFlags.Static | BindingFlags.NonPublic));
                BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                int n = 0, fail = 0;
                foreach (Type t in typeof(Brix.UI.Builder.Menu.BaseMenu).Assembly.GetTypes())
                {
                    if (t.Namespace == null || t.IsGenericTypeDefinition || t.ContainsGenericParameters) continue;
                    bool ok = false;
                    foreach (string p in prefixes) { if (p.EndsWith("*") ? t.Namespace.StartsWith(p.Substring(0, p.Length - 1)) : (t.FullName == p || (t.IsNested && t.FullName.StartsWith(p + "+")))) ok = true; }
                    if (!ok) continue;
                    foreach (MethodBase m in t.GetMethods(all))
                    {
                        if (m.IsAbstract || m.IsGenericMethodDefinition || m.GetMethodBody() == null) continue;
                        if (m.Name.StartsWith("get_") || m.Name.StartsWith("set_")) continue;
                        try
                        {
                            int id = Names.Count;
                            if (id >= _self.Length) break;
                            Ids[m] = id;
                            Names.Add(t.Name + "." + m.Name);
                            h.Patch(m, prefix: pre, finalizer: fin);
                            n++;
                        }
                        catch (Exception) { fail++; }
                    }
                }
                On = true;
                BtcLog.Info("MethodProf", "patched " + n + " methods (" + fail + " failed) for " + string.Join(",", prefixes));
            }
            catch (Exception e) { BtcLog.Error("MethodProf", e.ToString()); }
        }

        private static void Pre(MethodBase __originalMethod, ref long __state)
        {
            __state = 0;
            if (!BtcLoadBoost.Loading) return;
            if (_child == null) _child = new long[512];
            if (_d >= 500) return;
            _d++;
            _child[_d] = 0;
            __state = Stopwatch.GetTimestamp();
        }

        private static Exception Fin(MethodBase __originalMethod, long __state, Exception __exception)
        {
            if (__state == 0) return __exception;
            long el = Stopwatch.GetTimestamp() - __state;
            int id;
            if (Ids.TryGetValue(__originalMethod, out id))
            {
                _self[id] += el - _child[_d];
                _incl[id] += el;
                _calls[id]++;
            }
            _d--;
            _child[_d] += el;
            return __exception;
        }

        internal static void Dump()
        {
            if (!On) return;
            On = false;
            int n = Names.Count;
            List<int> idx = new List<int>();
            for (int i = 0; i < n; i++) if (_calls[i] > 0) idx.Add(i);
            double f = 1000.0 / Stopwatch.Frequency;
            idx.Sort((a, b) => _self[b].CompareTo(_self[a]));
            BtcLoadBoost.Note("methodprof: " + idx.Count + " methods hit; top by SELF time");
            for (int i = 0; i < idx.Count && i < 40; i++) BtcLoadBoost.Note("    self " + (_self[idx[i]] * f).ToString("F0") + " ms  incl " + (_incl[idx[i]] * f).ToString("F0") + "  n=" + _calls[idx[i]] + "  " + Names[idx[i]]);
            idx.Sort((a, b) => _incl[b].CompareTo(_incl[a]));
            BtcLoadBoost.Note("methodprof: top by INCLUSIVE time");
            for (int i = 0; i < idx.Count && i < 30; i++) BtcLoadBoost.Note("    incl " + (_incl[idx[i]] * f).ToString("F0") + " ms  self " + (_self[idx[i]] * f).ToString("F0") + "  n=" + _calls[idx[i]] + "  " + Names[idx[i]]);
        }
    }
}
#endif
