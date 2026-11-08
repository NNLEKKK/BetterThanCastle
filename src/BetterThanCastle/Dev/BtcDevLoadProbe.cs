#if DEV
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using MoonSharp.Interpreter;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV only: while BtcLoadBoost.Loading, times (a) every CLR callback called from Lua (outermost per name, inclusive) and
    /// (b) Lua compile (Script.LoadString/LoadFile). Dumped by BtcLoadBoost.DumpMenus via Dump().
    /// </summary>
    internal static class BtcDevLoadProbe
    {
        private sealed class Acc { public long Calls; public double Ms; public double Max; }

        private static readonly Dictionary<string, Acc> Cb = new Dictionary<string, Acc>();
        private static readonly Dictionary<string, Acc> Comp = new Dictionary<string, Acc>();
        [ThreadStatic] private static int _depth;
        [ThreadStatic] private static long _t0;
        [ThreadStatic] private static long _c0;

        internal static void Init()
        {
            try
            {
                Harmony h = new Harmony("com.btc.loadprobe");
                int ok = 0;
                BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                MethodInfo inv = typeof(CallbackFunction).GetMethod("Invoke", all);
                if (inv != null)
                {
                    h.Patch(inv, new HarmonyMethod(typeof(BtcDevLoadProbe).GetMethod("PreCb", all)), new HarmonyMethod(typeof(BtcDevLoadProbe).GetMethod("PostCb", all)));
                    ok++;
                }
                foreach (MethodInfo m in typeof(Script).GetMethods(all))
                {
                    if (m.DeclaringType != typeof(Script) || (m.Name != "LoadString" && m.Name != "LoadFile")) continue;
                    h.Patch(m, new HarmonyMethod(typeof(BtcDevLoadProbe).GetMethod("PreComp", all)), new HarmonyMethod(typeof(BtcDevLoadProbe).GetMethod("PostComp", all)));
                    ok++;
                }
                MethodInfo rel = typeof(Brix.UI.Builder.Panel.BasePanel).GetMethod("Release", all);
                if (rel != null) { h.Patch(rel, new HarmonyMethod(typeof(BtcDevLoadProbe).GetMethod("PreRelease", all))); ok++; }
                BtcLog.Info("LoadProbe", "patched " + ok);
            }
            catch (Exception e) { BtcLog.Error("LoadProbe", e.ToString()); }
        }

        private static long _rel, _relReal;
        private static readonly Dictionary<string, int> RelBy = new Dictionary<string, int>();
        private static readonly Dictionary<object, int> RelObj = new Dictionary<object, int>(new RefCmp());

        private static void PreRelease(Brix.UI.Builder.Panel.BasePanel __instance)
        {
            if (!BtcLoadBoost.Loading) return;
            _rel++;
            if (__instance.HasInstance)
            {
                _relReal++;
                string k = (__instance.ParentMenu != null ? __instance.ParentMenu.GetType().Name + ":" + __instance.ParentMenu.GetHashCode() : "nomenu") + " " + (__instance.AssetKey != null ? __instance.AssetKey.Name : "?");
                int c; RelBy.TryGetValue(k, out c); RelBy[k] = c + 1;
                int d; RelObj.TryGetValue(__instance, out d); RelObj[__instance] = d + 1;
            }
        }

        private static void PreCb()
        {
            if (!BtcLoadBoost.Loading) return;
            if (_depth++ == 0) _t0 = Stopwatch.GetTimestamp();
        }

        private static void PostCb(CallbackFunction __instance)
        {
            if (_depth == 0) return;
            if (--_depth != 0) return;
            Add(Cb, NameOf(__instance), _t0);
        }

        private static readonly Dictionary<object, string> Names = new Dictionary<object, string>(new RefCmp());

        private sealed class RefCmp : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) { return ReferenceEquals(a, b); }
            public int GetHashCode(object o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }

        private static string NameOf(CallbackFunction cf)
        {
            if (!string.IsNullOrEmpty(cf.Name)) return cf.Name;
            lock (Names)
            {
                string s;
                if (Names.TryGetValue(cf, out s)) return s;
                s = "?";
                try
                {
                    BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    foreach (FieldInfo f in typeof(CallbackFunction).GetFields(all))
                    {
                        Delegate d = f.GetValue(cf) as Delegate;
                        if (d == null) continue;
                        string tn = d.Target == null ? "static" : d.Target.GetType().Name;
                        string extra = "";
                        if (d.Target != null)
                        {
                            foreach (FieldInfo tf in d.Target.GetType().GetFields(all))
                            {
                                if (tf.FieldType == typeof(string) && tf.GetValue(d.Target) != null) { extra += "/" + tf.Name + "=" + tf.GetValue(d.Target); }
                                else if (tf.Name.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0 || tf.FieldType.Name.EndsWith("Descriptor")) extra += "/" + tf.Name + "=" + tf.GetValue(d.Target);
                                if (extra.Length > 120) break;
                            }
                        }
                        s = "?" + d.Method.DeclaringType.Name + "." + d.Method.Name + "[" + tn + extra + "]";
                        break;
                    }
                }
                catch (Exception) { }
                Names[cf] = s;
                return s;
            }
        }

        private static void PreComp(MethodBase __originalMethod)
        {
            if (BtcLoadBoost.Loading) _c0 = Stopwatch.GetTimestamp();
        }

        private static void PostComp(MethodBase __originalMethod, object[] __args)
        {
            if (!BtcLoadBoost.Loading || _c0 == 0) return;
            string n = "?";
            if (__args != null && __args.Length > 0 && __args[0] != null)
            {
                n = __args[0].ToString();
                if (__originalMethod.Name == "LoadString") n = "(string " + n.Length + ")";
            }
            Add(Comp, n, _c0);
            _c0 = 0;
        }

        private static void Add(Dictionary<string, Acc> d, string name, long t0)
        {
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            lock (d)
            {
                if (name == null) name = "?";
                Acc a;
                if (!d.TryGetValue(name, out a)) { a = new Acc(); d[name] = a; }
                a.Calls++; a.Ms += ms; if (ms > a.Max) a.Max = ms;
            }
        }

        private static void DumpRel()
        {
            int multi = 0, maxN = 0;
            foreach (KeyValuePair<object, int> kv in RelObj) { if (kv.Value > 1) multi++; if (kv.Value > maxN) maxN = kv.Value; }
            BtcLoadBoost.Note("  released-with-instance: distinct panels=" + RelObj.Count + " destroyed more than once=" + multi + " max times=" + maxN);
            List<KeyValuePair<string, int>> l = new List<KeyValuePair<string, int>>(RelBy);
            l.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < l.Count && i < 12; i++) BtcLoadBoost.Note("    " + l[i].Value + "  " + l[i].Key);
            RelBy.Clear(); RelObj.Clear();
        }

        internal static void Dump()
        {
            BtcLoadBoost.Note("BasePanel.Release calls=" + _rel + " with instance=" + _relReal); DumpRel(); _rel = 0; _relReal = 0;
            Dump("lua clr callbacks", Cb, 20);
            Dump("lua compile", Comp, 15);
        }

        private static void Dump(string title, Dictionary<string, Acc> d, int top)
        {
            List<KeyValuePair<string, Acc>> l;
            lock (d) { l = new List<KeyValuePair<string, Acc>>(d); d.Clear(); }
            l.Sort((a, b) => b.Value.Ms.CompareTo(a.Value.Ms));
            double sum = 0;
            for (int i = 0; i < l.Count; i++) sum += l[i].Value.Ms;
            BtcLoadBoost.Note(title + ": total " + sum.ToString("F0") + " ms, " + l.Count + " names");
            for (int i = 0; i < l.Count && i < top; i++)
                BtcLoadBoost.Note("    " + l[i].Key + " n=" + l[i].Value.Calls + " " + l[i].Value.Ms.ToString("F0") + " ms max " + l[i].Value.Max.ToString("F0"));
        }
    }
}
#endif
