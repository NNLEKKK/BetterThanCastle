#if DEV
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using MoonSharp.Interpreter;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV only: self/inclusive time per Lua file run through dofile/require/loadfile while loading (nested files subtracted from self).</summary>
    internal static class BtcDevLuaFileProbe
    {
        private sealed class Acc { public double Self; public double Incl; public int N; }
        private sealed class Fr { public long T; public double Child; public string Key; }
        private static readonly Dictionary<string, Acc> D = new Dictionary<string, Acc>();
        [ThreadStatic] private static List<Fr> _st;

        internal static void Init()
        {
            try
            {
                Harmony h = new Harmony("com.btc.luafileprobe");
                BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                MethodInfo inv = typeof(CallbackFunction).GetMethod("Invoke", all);
                h.Patch(inv, prefix: new HarmonyMethod(typeof(BtcDevLuaFileProbe).GetMethod("Pre", all)), finalizer: new HarmonyMethod(typeof(BtcDevLuaFileProbe).GetMethod("Fin", all)));
            }
            catch (Exception e) { BtcLog.Error("LuaFileProbe", e.ToString()); }
        }

        private static void Pre(CallbackFunction __instance, IList<DynValue> args, ref bool __state)
        {
            __state = false;
            if (!BtcLoadBoost.Loading) return;
            string n = __instance.Name;
            if (n != "dofile" && n != "require" && n != "loadfile") return;
            string a = "?";
            try { if (args != null && args.Count > 0 && args[0].Type == DataType.String) a = args[0].String; } catch (Exception) { }
            if (_st == null) _st = new List<Fr>();
            Fr f = new Fr(); f.T = Stopwatch.GetTimestamp(); f.Key = n + " " + a;
            _st.Add(f);
            __state = true;
        }

        private static Exception Fin(bool __state, Exception __exception)
        {
            if (!__state || _st == null || _st.Count == 0) return __exception;
            Fr f = _st[_st.Count - 1];
            _st.RemoveAt(_st.Count - 1);
            double ms = (Stopwatch.GetTimestamp() - f.T) * 1000.0 / Stopwatch.Frequency;
            if (_st.Count > 0) _st[_st.Count - 1].Child += ms;
            Acc c; if (!D.TryGetValue(f.Key, out c)) { c = new Acc(); D[f.Key] = c; }
            c.Self += ms - f.Child; c.Incl += ms; c.N++;
            return __exception;
        }

        internal static void Dump()
        {
            List<KeyValuePair<string, Acc>> l = new List<KeyValuePair<string, Acc>>(D);
            D.Clear();
            l.Sort((a, b) => b.Value.Self.CompareTo(a.Value.Self));
            double sum = 0; foreach (KeyValuePair<string, Acc> kv in l) sum += kv.Value.Self;
            BtcLoadBoost.Note("luafiles: " + l.Count + " files, self sum " + sum.ToString("F0") + " ms (includes compile + run, excludes nested files)");
            for (int i = 0; i < l.Count && i < 30; i++)
                BtcLoadBoost.Note("    self " + l[i].Value.Self.ToString("F0") + " incl " + l[i].Value.Incl.ToString("F0") + " n=" + l[i].Value.N + "  " + l[i].Key);
        }
    }
}
#endif
