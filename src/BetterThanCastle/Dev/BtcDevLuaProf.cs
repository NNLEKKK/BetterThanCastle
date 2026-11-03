#if DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using MoonSharp.Interpreter;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV only: Lua (MoonSharp) cost per Lua function, part of BtcDevProf (prof.txt section LUA).
    /// Hooks (Harmony id com.btc.prof): Processor.Call(DynValue, IList) and the 6-arg metamethod Call (covers Script.Call*,
    /// Closure.Call*, Script.DoString/DoFile via the loaded chunk closure) and Processor.Coroutine_Resume (coroutine steps, named
    /// via a Coroutine_Create postfix). Only the OUTERMOST entry per thread is counted (depth counter), so nested calls and
    /// Lua-side coroutine.resume are folded into their caller (no double counting; callee cost is attributed to the outer function).
    /// Names: "<source>:<line> <fn>" resolved once per (script,entry pc) from MoonSharp bytecode (Meta/BeginFn name + SourceRef).
    /// Alloc: GC.GetTotalMemory delta on every 8th outermost call, extrapolated per entry (approximate, can undercount after a GC).
    /// Limits: CLR callbacks invoked from C# directly (CallbackFunction.Invoke) and Script.LoadString compile time are not measured.
    /// </summary>
    internal static class BtcDevLuaProf
    {
        private const int Max = 512;
        private const int SampleMask = 7;

        private struct St { public long T; public long Mem; public int Id; public bool Inc; }

        private sealed class RefCmp : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) { return ReferenceEquals(a, b); }
            public int GetHashCode(object o) { return RuntimeHelpers.GetHashCode(o); }
        }

        private static readonly string[] Names = new string[Max];
        private static readonly long[] Calls = new long[Max];
        private static readonly long[] Total = new long[Max];
        private static readonly long[] MaxT = new long[Max];
        private static readonly long[] AllocSum = new long[Max];
        private static readonly long[] Samples = new long[Max];
        private static readonly bool[] IsCo = new bool[Max];
        private static int _count; private static int _tail = Max - 65; private static bool _ovLogged;
        private static readonly Dictionary<long, int> Keys = new Dictionary<long, int>();
        private static readonly Dictionary<object, int> CoIds = new Dictionary<object, int>(new RefCmp());
        private static readonly object Lock = new object();
        private static long _outer, _coSteps;
        private static int _sampleCtr;

        [ThreadStatic] private static int _depth;

        private static FieldInfo _fByteCode, _fCode, _fOpCode, _fName, _fSrcRef, _fSrcIdx, _fFromLine, _fCoProc;
        private static bool _reflOk;
        private static bool _patched;

        internal static void Reset()
        {
            lock (Lock)
            {
                for (int i = 0; i < Max; i++) { Calls[i] = 0; Total[i] = 0; MaxT[i] = 0; AllocSum[i] = 0; Samples[i] = 0; }
                _outer = 0; _coSteps = 0;
            }
        }

        internal static void Patch()
        {
            if (_patched) return;
            _patched = true;
            try
            {
                Assembly asm = typeof(Script).Assembly;
                Type proc = asm.GetType("MoonSharp.Interpreter.Execution.VM.Processor");
                if (proc == null) { BtcLog.Warn("Prof", "lua: Processor type not found"); return; }
                BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Type ins = asm.GetType("MoonSharp.Interpreter.Execution.VM.Instruction");
                Type bc = asm.GetType("MoonSharp.Interpreter.Execution.VM.ByteCode");
                Type sref = asm.GetType("MoonSharp.Interpreter.Debugging.SourceRef");
                _fByteCode = typeof(Script).GetField("m_ByteCode", bf);
                _fCode = bc.GetField("Code", bf);
                _fOpCode = ins.GetField("OpCode", bf);
                _fName = ins.GetField("Name", bf);
                _fSrcRef = ins.GetField("SourceCodeRef", bf);
                _fSrcIdx = sref.GetField("<SourceIdx>k__BackingField", bf) ?? sref.GetField("SourceIdx", bf);
                _fFromLine = sref.GetField("<FromLine>k__BackingField", bf) ?? sref.GetField("FromLine", bf);
                _fCoProc = typeof(Coroutine).GetField("m_Processor", bf);
                _reflOk = _fByteCode != null && _fCode != null && _fOpCode != null && _fName != null && _fSrcRef != null && _fSrcIdx != null && _fFromLine != null;

                Harmony h = new Harmony("com.btc.prof");
                BindingFlags sf = BindingFlags.NonPublic | BindingFlags.Static;
                HarmonyMethod post = new HarmonyMethod(typeof(BtcDevLuaProf).GetMethod("Post", sf));
                int ok = 0;
                MethodInfo[] ms = proc.GetMethods(bf);
                for (int i = 0; i < ms.Length; i++)
                {
                    MethodInfo m = ms[i];
                    if (m.DeclaringType != proc) continue;
                    try
                    {
                        if (m.Name == "Call")
                        {
                            h.Patch(m, new HarmonyMethod(typeof(BtcDevLuaProf).GetMethod("PreCall", sf)), post); ok++;
                        }
                        else if (m.Name == "Coroutine_Resume")
                        {
                            h.Patch(m, new HarmonyMethod(typeof(BtcDevLuaProf).GetMethod("PreResume", sf)), post); ok++;
                        }
                        else if (m.Name == "Coroutine_Create")
                        {
                            h.Patch(m, null, new HarmonyMethod(typeof(BtcDevLuaProf).GetMethod("PostCreate", sf))); ok++;
                        }
                    }
                    catch (Exception e) { BtcLog.Warn("Prof", "lua patch " + m.Name + ": " + e.Message); }
                }
                BtcLog.Info("Prof", "lua hooks=" + ok + " names=" + (_reflOk ? "bytecode" : "fallback"));
            }
            catch (Exception e) { BtcLog.Warn("Prof", "lua patch failed: " + e.Message); }
        }

        // ---------- hooks ----------
        private static void PreCall(DynValue function, out St __state)
        {
            __state.T = 0; __state.Mem = 0; __state.Id = -1; __state.Inc = false;
            if (!BtcDevProf.Running) return;
            __state.Inc = true;
            if (_depth++ != 0) return;
            int id = -1;
            try { if (!ReferenceEquals(function, null) && function.Type == DataType.Function) id = Resolve(function.Function); } catch (Exception) { }
            __state.Id = id < 0 ? Intern("<lua fn?>", false) : id;
            Begin(ref __state);
        }

        private static void PreResume(object __instance, out St __state)
        {
            __state.T = 0; __state.Mem = 0; __state.Id = -1; __state.Inc = false;
            if (!BtcDevProf.Running) return;
            __state.Inc = true;
            if (_depth++ != 0) return;
            int id;
            lock (Lock) { if (!CoIds.TryGetValue(__instance, out id)) id = -1; }
            __state.Id = id < 0 ? Intern("co:<unknown>", true) : id;
            Begin(ref __state);
        }

        private static void Begin(ref St s)
        {
            if ((Interlocked_Inc() & SampleMask) == 0) s.Mem = GC.GetTotalMemory(false); else s.Mem = -1;
            s.T = Stopwatch.GetTimestamp();
        }

        private static int Interlocked_Inc() { return System.Threading.Interlocked.Increment(ref _sampleCtr); }

        private static void Post(St __state)
        {
            // Id == -1 and depth>0 means nested/ignored or not running; decrement only when this call incremented depth.
            if (__state.Inc && _depth > 0) _depth--;
            if (__state.T == 0) return;
            long dt = Stopwatch.GetTimestamp() - __state.T;
            long mem = __state.Mem;
            long d = mem >= 0 ? GC.GetTotalMemory(false) - mem : 0;
            int id = __state.Id;
            Calls[id]++; Total[id] += dt; if (dt > MaxT[id]) MaxT[id] = dt;
            if (mem >= 0) { Samples[id]++; if (d > 0) AllocSum[id] += d; }
            _outer++;
            if (IsCo[id]) _coSteps++;
        }

        private static void PostCreate(object __instance, DynValue __result, Closure closure)
        {
            try
            {
                if (ReferenceEquals(__result, null) || ReferenceEquals(__result.Coroutine, null) || _fCoProc == null) return;
                object p = _fCoProc.GetValue(__result.Coroutine);
                if (p == null) return;
                int fid = Resolve(closure);
                string nm = "co:" + Names[fid];
                int id = Intern(nm, true);
                lock (Lock)
                {
                    if (CoIds.Count > 20000) CoIds.Clear();
                    CoIds[p] = id;
                }
            }
            catch (Exception) { }
        }

        // ---------- names ----------
        private static int Intern(string name, bool co)
        {
            lock (Lock)
            {
                for (int i = 0; i < _count; i++) if (Names[i] == name) return i;
                if (_count >= Max - 65)
                {
                    // Too many distinct names: bucket by source file ("<src> <other>") in the reserved tail, final fallback <overflow>.
                    if (!_ovLogged) { _ovLogged = true; BtcLog.Warn("Prof", "lua name table full; samples: " + Names[0] + " | " + Names[1] + " | " + Names[50] + " | " + Names[200] + " | new=" + name); }
                    int cut = name.IndexOfAny(new char[] { ':', ' ' });
                    string bucket = (cut > 0 ? name.Substring(0, cut) : name) + " <other>";
                    for (int i = Max - 65; i < _tail; i++) if (Names[i] == bucket) return i;
                    if (_tail < Max - 1) { Names[_tail] = bucket; IsCo[_tail] = co; return _tail++; }
                    Names[Max - 1] = "<overflow>"; return Max - 1;
                }
                Names[_count] = name; IsCo[_count] = co;
                return _count++;
            }
        }
        private static int Resolve(Closure c)
        {
            Script s = c.OwnerScript;
            int pc = c.EntryPointByteCodeLocation;
            long key = ((long)RuntimeHelpers.GetHashCode(s) << 32) | (uint)pc;
            int id;
            lock (Lock) { if (Keys.TryGetValue(key, out id)) return id; }
            id = Intern(BuildName(s, pc), false);
            lock (Lock) { Keys[key] = id; }
            return id;
        }

        private static string BuildName(Script s, int pc)
        {
            string fn = null, src = null; int line = -1;
            try
            {
                if (_reflOk)
                {
                    IList code = (IList)_fCode.GetValue(_fByteCode.GetValue(s));
                    for (int i = pc - 1; i <= pc + 2; i++)
                    {
                        if (i < 0 || i >= code.Count) continue;
                        object ins = code[i];
                        string nm = (string)_fName.GetValue(ins);
                        string op = _fOpCode.GetValue(ins).ToString();
                        if (fn == null && nm != null && nm.Length > 0 && (op == "Meta" || op == "BeginFn")) fn = nm;
                        if (line < 0)
                        {
                            object sr = _fSrcRef.GetValue(ins);
                            if (sr != null && i >= pc)
                            {
                                int si = (int)_fSrcIdx.GetValue(sr);
                                if (si >= 0 && si < s.SourceCodeCount)
                                {
                                    src = s.GetSourceCode(si).Name;
                                    line = (int)_fFromLine.GetValue(sr);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
            if (src == null) src = s.FriendlyName;
            if (src == null) src = "script#";
            int sl = src.LastIndexOfAny(new char[] { '/', '\\' });
            if (sl >= 0 && sl < src.Length - 1) src = src.Substring(sl + 1);
            if (src.Length > 40) src = src.Substring(src.Length - 40);
            StringBuilder sb = new StringBuilder();
            sb.Append(src);
            if (line >= 0) sb.Append(':').Append(line);
            sb.Append(' ').Append(fn ?? ("pc" + pc));
            return sb.ToString();
        }

        // ---------- dump ----------
        internal static void Append(StringBuilder sb, long frames, double sumDtSec)
        {
            long[] calls = new long[Max], tot = new long[Max], mx = new long[Max], al = new long[Max], sm = new long[Max];
            string[] nm = new string[Max];
            int n;
            long outer, co;
            lock (Lock)
            {
                n = Max;
                Array.Copy(Calls, calls, Max); Array.Copy(Total, tot, Max); Array.Copy(MaxT, mx, Max);
                Array.Copy(AllocSum, al, Max); Array.Copy(Samples, sm, Max); Array.Copy(Names, nm, Max);
                outer = _outer; co = _coSteps;
            }
            List<int> l = new List<int>();
            long sumTicks = 0;
            for (int i = 0; i < n; i++) { if (calls[i] > 0) { l.Add(i); sumTicks += tot[i]; } }
            l.Sort(delegate (int a, int b) { return tot[b].CompareTo(tot[a]); });
            double f = 1000.0 / Stopwatch.Frequency;
            sb.AppendLine("LUA (ms_total calls avg max KB_alloc_est name; outermost calls only, alloc sampled 1/8, co: = coroutine step)");
            for (int k = 0; k < 15 && k < l.Count; k++)
            {
                int i = l[k];
                double ms = tot[i] * f;
                double kb = sm[i] > 0 ? (double)al[i] / sm[i] * calls[i] / 1024.0 : 0;
                sb.Append(ms.ToString("0.0")).Append(' ').Append(calls[i]).Append(' ').Append((ms / calls[i]).ToString("0.000")).Append(' ')
                  .Append((mx[i] * f).ToString("0.00")).Append(' ').Append(kb.ToString("0")).Append(' ').AppendLine(nm[i]);
            }
            double totMs = sumTicks * f;
            double frameMs = sumDtSec * 1000.0;
            sb.Append("lua_total_ms=").Append(totMs.ToString("0.0")).Append(" pct_of_frame=").Append((frameMs > 0 ? totMs * 100.0 / frameMs : 0).ToString("0.0"))
              .Append("% lua_calls/frame=").Append((frames > 0 ? (double)outer / frames : 0).ToString("0.00"))
              .Append(" coroutine_steps=").Append(co).Append(" fns=").Append(l.Count).AppendLine();
        }
    }
}
#endif
