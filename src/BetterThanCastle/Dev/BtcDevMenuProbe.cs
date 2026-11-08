#if DEV
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Brix.UI.Builder.Menu;
using HarmonyLib;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV only: self/inclusive time of BaseMenu.Load and BaseMenu.Open per menu (Lua path) while loading.</summary>
    internal static class BtcDevMenuProbe
    {
        private sealed class Acc { public double Self; public double Incl; public int N; }
        private static readonly Dictionary<string, Acc> Load = new Dictionary<string, Acc>();
        private static readonly Dictionary<string, Acc> Open = new Dictionary<string, Acc>();
        [ThreadStatic] private static double[] _child;
        [ThreadStatic] private static int _d;

        internal static void Init()
        {
            try
            {
                Harmony h = new Harmony("com.btc.menuprobe");
                BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                MethodInfo l = typeof(BaseMenu).GetMethod("Load", all);
                MethodInfo o = typeof(BaseMenu).GetMethod("Open", all, null, Type.EmptyTypes, null);
                h.Patch(l, prefix: new HarmonyMethod(typeof(BtcDevMenuProbe).GetMethod("PreL", all | BindingFlags.Static)), finalizer: new HarmonyMethod(typeof(BtcDevMenuProbe).GetMethod("FinL", all | BindingFlags.Static)));
                h.Patch(o, prefix: new HarmonyMethod(typeof(BtcDevMenuProbe).GetMethod("PreO", all | BindingFlags.Static)), finalizer: new HarmonyMethod(typeof(BtcDevMenuProbe).GetMethod("FinO", all | BindingFlags.Static)));
            }
            catch (Exception e) { BtcLog.Error("MenuProbe", e.ToString()); }
        }

        private static string NameOf(BaseMenu m)
        {
            try { string p = m.LuaPath; if (!string.IsNullOrEmpty(p)) return p; } catch (Exception) { }
            return m.GetType().Name + "#" + m.GetHashCode();
        }

        private static void Pre(ref long st)
        {
            st = 0;
            if (!BtcLoadBoost.Loading) return;
            if (_child == null) _child = new double[256];
            if (_d >= 250) return;
            _d++; _child[_d] = 0; st = Stopwatch.GetTimestamp();
        }

        private static void Fin(Dictionary<string, Acc> d, BaseMenu m, long st)
        {
            if (st == 0) return;
            double ms = (Stopwatch.GetTimestamp() - st) * 1000.0 / Stopwatch.Frequency;
            double self = ms - _child[_d];
            _d--; _child[_d] += ms;
            string k = NameOf(m);
            Acc a; if (!d.TryGetValue(k, out a)) { a = new Acc(); d[k] = a; }
            a.Self += self; a.Incl += ms; a.N++;
        }

        private static void PreL(ref long __state) { Pre(ref __state); }
        private static void PreO(ref long __state) { Pre(ref __state); }
        private static Exception FinL(BaseMenu __instance, long __state, Exception __exception) { Fin(Load, __instance, __state); return __exception; }
        private static Exception FinO(BaseMenu __instance, long __state, Exception __exception) { Fin(Open, __instance, __state); return __exception; }

        internal static void Dump()
        {
            Print("Load", Load); Print("Open", Open);
        }

        private static void Print(string t, Dictionary<string, Acc> d)
        {
            List<KeyValuePair<string, Acc>> l = new List<KeyValuePair<string, Acc>>(d);
            d.Clear();
            l.Sort((a, b) => b.Value.Self.CompareTo(a.Value.Self));
            double sum = 0; foreach (KeyValuePair<string, Acc> kv in l) sum += kv.Value.Self;
            BtcLoadBoost.Note("menuprobe " + t + ": " + l.Count + " menus, self sum " + sum.ToString("F0") + " ms");
            for (int i = 0; i < l.Count && i < 25; i++)
                BtcLoadBoost.Note("    " + t + " self " + l[i].Value.Self.ToString("F0") + " incl " + l[i].Value.Incl.ToString("F0") + " n=" + l[i].Value.N + "  " + l[i].Key);
        }
    }
}
#endif
