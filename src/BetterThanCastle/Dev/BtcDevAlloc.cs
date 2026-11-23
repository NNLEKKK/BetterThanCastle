#if DEV
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace BetterThanCastle.Dev
{
    /// DEV only allocation profiler: every managed allocation through Mono's own profiler interface
    /// (mono_profiler_install_allocation), bytes per type on every thread, and on the main thread a call stack sampled every
    /// SampleBytes allocated (weighted by the bytes it stands for). Answers what fills the heap between two collections.
    /// Windows x64 port of Castle Story Plus Bootstrap/AllocProfiler.cs (Tricky12321, GPLv3), with the same workarounds:
    /// - libmono's "profile_allocs" flag (arrays/strings) is cleared for good the first time a method is compiled while no
    ///   profiler wants allocations: found as the dword that mono_object_new_specific's code writes 0 to AND that
    ///   mono_string_new_size reads, and set to 1 again on Start;
    /// - objects of methods compiled before Install use allocators without the profiler call: install as early as possible
    ///   (plugin load, env BTC_ALLOC=1);
    /// - a native thread allocates its Thread object while attaching to Mono: a few bytes of native code call the managed
    ///   callback only when mono_thread_current() is set.
    /// Counting runs while BtcDevProf runs (Reset/Start with it); Dump writes BTClogs/alloc.txt. Costs one native-to-managed
    /// call per allocation while installed.
    internal static class BtcDevAlloc
    {
        private const string Mono = "mono.dll";
        private const int ProfileAllocations = 1 << 7;
        private const int TypeSlots = 4096;
        private const int StackSlots = 4096;
        private const int MaxFrames = 16;
        private const int WalkFrames = 64;
        private const long SampleBytes = 16 * 1024;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AllocCallback(IntPtr profiler, IntPtr obj, IntPtr klass);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int FrameCallback(IntPtr method, int nativeOffset, int ilOffset, int managed, IntPtr data);

        internal static bool Installed;
        internal static volatile bool Running;
        internal static string Error = string.Empty;
        private static bool _flagFound;
        private static int _flagValue = 1; // what "on" looks like: 1 (Linux build) or -1 (Windows branchless mask)

        private static AllocCallback _allocCallback; // kept alive for as long as Mono may call it
        private static FrameCallback _frameCallback;
        private static IntPtr _framePointer, _profileFlag, _selfMethod;
        private static Thread _mainThread;
        private static bool _busy;
        private static int _sampleLock;
        private static long _otherUntil = SampleBytes;

        private static readonly IntPtr[] TypeKeys = new IntPtr[TypeSlots];
        private static readonly long[] TypeCounts = new long[TypeSlots];
        private static readonly long[] TypeBytes = new long[TypeSlots];
        private static readonly long[] TypeMainBytes = new long[TypeSlots];
        // other threads: bytes per managed thread (slot = first free), the Thread kept to print its name
        private static readonly Thread[] Threads = new Thread[512];
        private static readonly long[] ThreadBytes = new long[512];
        private static readonly IntPtr[] StackClasses = new IntPtr[StackSlots];
        private static readonly int[] StackDepths = new int[StackSlots];
        private static readonly IntPtr[] StackFrames = new IntPtr[StackSlots * MaxFrames];
        private static readonly long[] StackSamples = new long[StackSlots];
        private static readonly long[] StackBytes = new long[StackSlots];
        private static readonly IntPtr[] Walk = new IntPtr[WalkFrames];
        private static readonly Dictionary<IntPtr, string> Names = new Dictionary<IntPtr, string>();
        private static int _walkDepth, _stacksUsed;
        private static long _objects, _bytes, _mainBytes, _overflowBytes, _untilSample, _startedAt;

        /// From the plugin's Awake (main thread), when env BTC_ALLOC=1.
        internal static void Install()
        {
            if (Installed || Environment.GetEnvironmentVariable("BTC_ALLOC") != "1") return;
            try
            {
                if (IntPtr.Size != 8) { Error = "x64 only"; return; }
                IntPtr mono = GetModuleHandle(Mono);
                if (mono == IntPtr.Zero) { Error = "mono.dll not loaded"; return; }
                IntPtr threadCurrent = GetProcAddress(mono, "mono_thread_current");
                IntPtr newSpecific = GetProcAddress(mono, "mono_object_new_specific");
                IntPtr stringNew = GetProcAddress(mono, "mono_string_new_size");
                if (threadCurrent == IntPtr.Zero || newSpecific == IntPtr.Zero) { Error = "mono exports missing"; return; }
                _profileFlag = FindProfileFlag(newSpecific, stringNew);
                _flagFound = _profileFlag != IntPtr.Zero;
                _mainThread = Thread.CurrentThread;
                _selfMethod = typeof(BtcDevAlloc).GetMethod("OnAlloc", BindingFlags.Static | BindingFlags.NonPublic).MethodHandle.Value;
                _allocCallback = OnAlloc;
                _frameCallback = OnFrame;
                IntPtr managed = Marshal.GetFunctionPointerForDelegate(_allocCallback); // wrappers compiled here, not inside an allocation
                _framePointer = Marshal.GetFunctionPointerForDelegate(_frameCallback);
                string mode = Environment.GetEnvironmentVariable("BTC_ALLOC_MODE"); // diagnostics: nop = native ret only, direct = no gate
                _countOnly = mode == "count";
                IntPtr gate = mode == "direct" ? managed : NativeGate(threadCurrent, managed);
                if (mode == "nop" && gate != IntPtr.Zero) { Marshal.WriteByte(gate, 0xC3); BtcLog.Info("Alloc", "mode nop"); }
                if (mode == "direct") BtcLog.Info("Alloc", "mode direct (no gate)");
                if (gate == IntPtr.Zero) { Error = "no executable memory"; return; }
                mono_profiler_install(Marshal.AllocHGlobal(16), IntPtr.Zero);
                mono_profiler_install_allocation(gate);
                // The callback's own methods are compiled now, not inside an allocation (a compile that allocates would re-enter).
                string[] warm = { "OnAlloc", "CountType", "Sample", "SameStack", "OnFrame", "Hash" };
                for (int i = 0; i < warm.Length; i++)
                    System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(typeof(BtcDevAlloc).GetMethod(warm[i], BindingFlags.Static | BindingFlags.NonPublic).MethodHandle);
                // The P/Invoke used on every thread is bound now: binding allocates, and other threads have no re-entry guard.
                WarmSize();
                // Wanted from now on, so no later compile clears the flag again; the flag itself is set on Reset (start of counting).
                mono_profiler_set_events(ProfileAllocations);
                Installed = true;
                BtcLog.Info("Alloc", "installed (arrays/strings " + (_flagFound ? "on" : "NOT seen: flag not found") + ")");
            }
            catch (Exception e) { Error = e.GetType().Name + ": " + e.Message; }
            if (!Installed) BtcLog.Warn("Alloc", "not installed: " + Error);
        }

        private static unsafe void WarmSize()
        {
            object o = new object();
            TypedReference tr = __makeref(o);
            IntPtr p = **(IntPtr**)&tr; // the MonoObject* the reference holds
            uint size = mono_object_get_size(p);
            GC.KeepAlive(o);
            if (size == 0 || size > 64) BtcLog.Warn("Alloc", "object size probe odd: " + size);
        }

        internal static void Reset()
        {
            if (!Installed) return;
            _busy = true;
            try
            {
                Array.Clear(TypeKeys, 0, TypeSlots); Array.Clear(TypeCounts, 0, TypeSlots); Array.Clear(TypeBytes, 0, TypeSlots); Array.Clear(TypeMainBytes, 0, TypeSlots); Array.Clear(ThreadBytes, 0, ThreadBytes.Length); Array.Clear(Threads, 0, Threads.Length);
                Array.Clear(StackClasses, 0, StackSlots); Array.Clear(StackDepths, 0, StackSlots); Array.Clear(StackFrames, 0, StackFrames.Length);
                Array.Clear(StackSamples, 0, StackSlots); Array.Clear(StackBytes, 0, StackSlots);
                _stacksUsed = 0; _objects = 0; _bytes = 0; _mainBytes = 0; _overflowBytes = 0; _untilSample = SampleBytes;
                _startedAt = Stopwatch.GetTimestamp();
                if (_flagFound) Marshal.WriteInt32(_profileFlag, _flagValue);
            }
            finally { _busy = false; }
            Running = true;
        }

        internal static void Stop() { Running = false; }

        private static long _calls; private static string _firstError; private static bool _countOnly;

        private static void OnAlloc(IntPtr profiler, IntPtr obj, IntPtr klass)
        {
            Interlocked.Increment(ref _calls);
            if (_countOnly) return;
            if (!Running || obj == IntPtr.Zero) return;
            bool main = Thread.CurrentThread == _mainThread;
            if (main) { if (_busy) return; _busy = true; }

            try
            {
                long size = mono_object_get_size(obj);
                Interlocked.Increment(ref _objects);
                Interlocked.Add(ref _bytes, size);
                CountType(klass, size, main);
                if (!main) CountThread(size);
                if (main)
                {
                    _mainBytes += size;
                    _untilSample -= size;
                    if (_untilSample <= 0)
                    {
                        long crossed = 1 + (-_untilSample) / SampleBytes;
                        _untilSample += crossed * SampleBytes;
                        if (Interlocked.CompareExchange(ref _sampleLock, 1, 0) == 0) { try { Sample(klass, crossed * SampleBytes); } finally { _sampleLock = 0; } }
                    }
                }
                else
                {
                    // unnamed (foreign) threads: sampled too, so their stacks show who they are (shared tables, one sampler at a time)
                    long u = Interlocked.Add(ref _otherUntil, -size);
                    if (u <= 0 && Thread.CurrentThread.Name == null && Interlocked.CompareExchange(ref _sampleLock, 1, 0) == 0)
                    {
                        try { long crossed = 1 + (-u) / SampleBytes; Interlocked.Add(ref _otherUntil, crossed * SampleBytes); Sample(klass, crossed * SampleBytes); }
                        finally { _sampleLock = 0; }
                    }
                }
            }
            catch (Exception e) { if (_firstError == null) _firstError = e.GetType().Name + ": " + e.Message; }
            finally { if (main) _busy = false; }
        }

        private static void CountThread(long size)
        {
            Thread t = Thread.CurrentThread;
            for (int i = 0; i < Threads.Length; i++)
            {
                Thread k = Threads[i];
                if (k == null) { k = Interlocked.CompareExchange(ref Threads[i], t, null); if (k == null) k = t; }
                if (ReferenceEquals(k, t)) { Interlocked.Add(ref ThreadBytes[i], size); return; }
            }
        }

        private static void CountType(IntPtr klass, long size, bool main)
        {
            if (klass == IntPtr.Zero) return;
            int slot = Hash(klass) & (TypeSlots - 1);
            for (int i = 0; i < TypeSlots; i++)
            {
                IntPtr key = TypeKeys[slot];
                if (key == IntPtr.Zero)
                {
                    key = Interlocked.CompareExchange(ref TypeKeys[slot], klass, IntPtr.Zero);
                    if (key == IntPtr.Zero) key = klass;
                }
                if (key == klass) { Interlocked.Increment(ref TypeCounts[slot]); Interlocked.Add(ref TypeBytes[slot], size); if (main) TypeMainBytes[slot] += size; return; }
                slot = (slot + 1) & (TypeSlots - 1);
            }
            Interlocked.Add(ref _overflowBytes, size);
        }

        // Main thread only: the managed stack beyond OnAlloc, counted with the bytes it stands for.
        private static void Sample(IntPtr klass, long weight)
        {
            _walkDepth = 0;
            mono_stack_walk_no_il(_framePointer, IntPtr.Zero);
            int start = 0;
            for (int i = 0; i < _walkDepth; i++) if (Walk[i] == _selfMethod) { start = i + 1; break; }
            int depth = Math.Min(_walkDepth - start, MaxFrames);
            int hash = Hash(klass);
            for (int i = 0; i < depth; i++) hash = hash * 31 + Hash(Walk[start + i]);
            int slot = hash & (StackSlots - 1);
            for (int n = 0; n < StackSlots; n++)
            {
                if (StackSamples[slot] == 0)
                {
                    if (_stacksUsed >= StackSlots * 3 / 4) break;
                    StackClasses[slot] = klass; StackDepths[slot] = depth;
                    Array.Copy(Walk, start, StackFrames, slot * MaxFrames, depth);
                    StackSamples[slot] = 1; StackBytes[slot] = weight; _stacksUsed++;
                    return;
                }
                if (SameStack(slot, klass, start, depth)) { StackSamples[slot]++; StackBytes[slot] += weight; return; }
                slot = (slot + 1) & (StackSlots - 1);
            }
            _overflowBytes += weight;
        }

        private static bool SameStack(int slot, IntPtr klass, int start, int depth)
        {
            if (StackClasses[slot] != klass || StackDepths[slot] != depth) return false;
            int at = slot * MaxFrames;
            for (int i = 0; i < depth; i++) if (StackFrames[at + i] != Walk[start + i]) return false;
            return true;
        }

        private static int OnFrame(IntPtr method, int nativeOffset, int ilOffset, int managed, IntPtr data)
        {
            if (method == IntPtr.Zero) return 0;
            Walk[_walkDepth++] = method;
            return _walkDepth >= WalkFrames ? 1 : 0;
        }

        private static int Hash(IntPtr p)
        {
            long v = p.ToInt64();
            v ^= v >> 29; v *= unchecked((long)0xBF58476D1CE4E5B9UL); v ^= v >> 32;
            return (int)v & int.MaxValue;
        }

        // ================= dump =================
        internal static void Dump(string tag)
        {
            if (!Installed) return;
            _busy = true;
            try
            {
                double sec = (Stopwatch.GetTimestamp() - _startedAt) / (double)Stopwatch.Frequency; if (sec < 0.001) sec = 0.001;
                StringBuilder sb = new StringBuilder(16384);
                sb.Append("ALLOC ").Append(tag).Append(" t=").Append(sec.ToString("0")).Append(" objects=").Append(_objects)
                  .Append(" MB=").Append((_bytes / 1048576.0).ToString("0.0")).Append(" KB/s=").Append((_bytes / 1024.0 / sec).ToString("0"))
                  .Append(" main KB/s=").Append((_mainBytes / 1024.0 / sec).ToString("0")).Append(" overflowKB=").Append(_overflowBytes / 1024)
                  .Append(" arrays/strings=").Append(_flagFound ? "yes" : "NO").Append(" calls=").Append(_calls).Append(" err=").Append(_firstError ?? "-").AppendLine();
                List<int> rows = new List<int>();
                for (int i = 0; i < TypeSlots; i++) if (TypeKeys[i] != IntPtr.Zero && TypeCounts[i] > 0) rows.Add(i);
                rows.Sort(delegate (int a, int b) { return TypeBytes[b].CompareTo(TypeBytes[a]); });
                sb.Append("-- types (KB/s, objects/s, main-thread KB/s)").AppendLine();
                for (int i = 0; i < rows.Count && i < 40; i++)
                {
                    int s = rows[i];
                    sb.Append((TypeBytes[s] / 1024.0 / sec).ToString("0.0")).Append(' ').Append((TypeCounts[s] / sec).ToString("0")).Append(" main=").Append((TypeMainBytes[s] / 1024.0 / sec).ToString("0.0")).Append(' ').Append(ClassName(TypeKeys[s])).AppendLine();
                }
                sb.Append("-- other threads (KB/s name id)").AppendLine();
                int nThreads = 0; double unnamed = 0; for (int i = 0; i < Threads.Length; i++) if (Threads[i] != null) { nThreads++; if (Threads[i].Name == null) unnamed += ThreadBytes[i]; }
                sb.Append("threads seen=").Append(nThreads).Append(" unnamed KB/s=").Append((unnamed / 1024.0 / sec).ToString("0.0")).AppendLine();
                for (int i = 0; i < Threads.Length; i++) if (Threads[i] != null && ThreadBytes[i] > 50000) sb.Append((ThreadBytes[i] / 1024.0 / sec).ToString("0.0")).Append(' ').Append(Threads[i].Name ?? "?").Append(' ').Append(Threads[i].ManagedThreadId).AppendLine();
                List<int> st = new List<int>();
                for (int i = 0; i < StackSlots; i++) if (StackSamples[i] > 0) st.Add(i);
                st.Sort(delegate (int a, int b) { return StackBytes[b].CompareTo(StackBytes[a]); });
                sb.Append("-- main-thread stacks (KB/s type <- innermost frames)").AppendLine();
                for (int i = 0; i < st.Count && i < 60; i++)
                {
                    int s = st[i];
                    sb.Append((StackBytes[s] / 1024.0 / sec).ToString("0.0")).Append(' ').Append(ClassName(StackClasses[s]));
                    for (int f = 0; f < StackDepths[s]; f++) sb.Append(" <- ").Append(MethodName(StackFrames[s * MaxFrames + f]));
                    sb.AppendLine();
                }
                File.WriteAllText(Path.Combine(BtcDevPaths.LogDir, "alloc.txt"), sb.ToString());
                BtcLog.Info("Alloc", "dump " + tag + ": " + (_bytes / 1024.0 / sec).ToString("0") + " KB/s");
            }
            catch (Exception e) { BtcLog.Warn("Alloc", "dump: " + e.Message); }
            finally { _busy = false; }
        }

        private static string ClassName(IntPtr klass)
        {
            if (klass == IntPtr.Zero) return "?";
            string name;
            if (Names.TryGetValue(klass, out name)) return name;
            IntPtr text = mono_type_get_name(mono_class_get_type(klass));
            name = text != IntPtr.Zero ? Marshal.PtrToStringAnsi(text) : "?";
            // the name string is not freed: one per class, cached for the session (g_free may not be exported)
            Names[klass] = name;
            return name;
        }

        private static string MethodName(IntPtr method)
        {
            string name;
            if (Names.TryGetValue(method, out name)) return name;
            IntPtr text = mono_method_get_name(method);
            string cls = ClassName(mono_method_get_class(method));
            int dot = cls.LastIndexOf('.');
            if (dot >= 0 && cls.IndexOf('<') < 0) cls = cls.Substring(dot + 1); // short type names keep the dump narrow
            name = cls + "." + (text != IntPtr.Zero ? Marshal.PtrToStringAnsi(text) : "?");
            Names[method] = name;
            return name;
        }

        // ================= native helpers =================
        // The flag: a dword that the code after mono_object_new_specific sets to 0 ("mov dword [rip+d], 0" = C7 05 d32 0)
        // and that mono_string_new_size reads (any rip-relative reference to the same address). Holds 0 or 1.
        private static IntPtr FindProfileFlag(IntPtr writer, IntPtr reader)
        {
            if (reader == IntPtr.Zero) return IntPtr.Zero;
            List<long> cands = new List<long>();
            for (int i = 0; i < 0x800; i++)
            {
                if (Marshal.ReadByte(writer, i) != 0xC7 || Marshal.ReadByte(writer, i + 1) != 0x05 || Marshal.ReadInt32(writer, i + 6) != 0) continue;
                long a = writer.ToInt64() + i + 10 + Marshal.ReadInt32(writer, i + 2);
                int v = Marshal.ReadInt32(new IntPtr(a));
                if (v == 0 || v == 1) cands.Add(a);
            }
            // Windows MSVC build (Unity 5.6 mono.dll): branchless "call get_events; and al,0x80; neg al; sbb eax,eax;
            // and eax,[rip+d]; mov [rip+d],eax" (flag &= wanted ? -1 : 0): 23 05 d32 followed by 89 05 d32, same address.
            for (int i = 0; i < 0x800; i++)
            {
                if (Marshal.ReadByte(writer, i) != 0x23 || Marshal.ReadByte(writer, i + 1) != 0x05) continue;
                if (Marshal.ReadByte(writer, i + 6) != 0x89 || Marshal.ReadByte(writer, i + 7) != 0x05) continue;
                long a = writer.ToInt64() + i + 6 + Marshal.ReadInt32(writer, i + 2);
                long b = writer.ToInt64() + i + 12 + Marshal.ReadInt32(writer, i + 8);
                if (a == b) { _flagValue = -1; return new IntPtr(a); }
            }
            for (int c = 0; c < cands.Count; c++)
            {
                for (int i = 0; i < 0x400; i++)
                {
                    long end = reader.ToInt64() + i + 4;
                    long d = Marshal.ReadInt32(reader, i);
                    // disp32 followed by nothing (mov/test reg) or an imm8 (cmp [rip+d], imm8)
                    if (end + d == cands[c] || end + 1 + d == cands[c]) return new IntPtr(cands[c]);
                }
            }
            BtcLog.Warn("Alloc", "flag candidates=" + cands.Count + ", none read by mono_string_new_size");
            return IntPtr.Zero;
        }

        // Win64: calls the managed callback (args rcx, rdx, r8) only when mono_thread_current() is set and the thread is not
        // already inside it. The re-entry flag is a Win32 TLS slot read straight from the TEB (gs:[0x1480 + 8 * index]):
        // the native-to-managed wrapper itself allocates, so a guard in managed code comes too late (endless recursion).
        //   mov rax, gs:[slot]; test rax,rax; jnz out
        //   push rcx; push rdx; push r8; sub rsp,32; mov rax, mono_thread_current; call rax; test rax,rax; jz restore
        //   mov qword gs:[slot],1; reload rcx/rdx/r8 from the stack; mov rax, managed; call rax; mov qword gs:[slot],0
        //   restore: add rsp,32; pop r8; pop rdx; pop rcx; out: ret
        private static IntPtr NativeGate(IntPtr threadCurrent, IntPtr managed)
        {
            uint tls = TlsAlloc();
            if (tls >= 64) { BtcLog.Warn("Alloc", "TLS index " + tls + " beyond the TEB slots"); return IntPtr.Zero; }
            int slot = 0x1480 + 8 * (int)tls;
            byte[] code =
            {
                0x65, 0x48, 0x8B, 0x04, 0x25, 0, 0, 0, 0,        // 0  mov rax, gs:[slot]
                0x48, 0x85, 0xC0,                                // 9  test rax, rax
                0x75, 0x56,                                      // 12 jnz out (100)
                0x51, 0x52, 0x41, 0x50,                          // 14 push rcx, rdx, r8
                0x48, 0x83, 0xEC, 0x20,                          // 18 sub rsp, 32
                0x48, 0xB8, 0, 0, 0, 0, 0, 0, 0, 0,              // 22 mov rax, mono_thread_current
                0xFF, 0xD0,                                      // 32 call rax
                0x48, 0x85, 0xC0,                                // 34 test rax, rax
                0x74, 0x35,                                      // 37 jz restore (92)
                0x65, 0x48, 0xC7, 0x04, 0x25, 0, 0, 0, 0, 1, 0, 0, 0, // 39 mov qword gs:[slot], 1
                0x48, 0x8B, 0x4C, 0x24, 0x30,                    // 52 mov rcx, [rsp+48]
                0x48, 0x8B, 0x54, 0x24, 0x28,                    // 57 mov rdx, [rsp+40]
                0x4C, 0x8B, 0x44, 0x24, 0x20,                    // 62 mov r8, [rsp+32]
                0x48, 0xB8, 0, 0, 0, 0, 0, 0, 0, 0,              // 67 mov rax, managed
                0xFF, 0xD0,                                      // 77 call rax
                0x65, 0x48, 0xC7, 0x04, 0x25, 0, 0, 0, 0, 0, 0, 0, 0, // 79 mov qword gs:[slot], 0
                0x48, 0x83, 0xC4, 0x20,                          // 92 restore: add rsp, 32
                0x41, 0x58, 0x5A, 0x59,                          // 96 pop r8, rdx, rcx
                0xC3                                             // 100 out: ret
            };
            byte[] s = BitConverter.GetBytes(slot);
            Array.Copy(s, 0, code, 5, 4);
            Array.Copy(s, 0, code, 44, 4);
            Array.Copy(s, 0, code, 84, 4);
            Array.Copy(BitConverter.GetBytes(threadCurrent.ToInt64()), 0, code, 24, 8);
            Array.Copy(BitConverter.GetBytes(managed.ToInt64()), 0, code, 69, 8);
            IntPtr mem = VirtualAlloc(IntPtr.Zero, new UIntPtr(4096), 0x3000, 0x40); // MEM_COMMIT|MEM_RESERVE, PAGE_EXECUTE_READWRITE
            if (mem == IntPtr.Zero) return IntPtr.Zero;
            Marshal.Copy(code, 0, mem, code.Length);
            return mem;
        }

        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll")] private static extern uint TlsAlloc();
        [DllImport("kernel32.dll")] private static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32.dll")] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);
        [DllImport(Mono, CallingConvention = CallingConvention.Cdecl)] private static extern void mono_profiler_install(IntPtr profiler, IntPtr shutdown);
        [DllImport(Mono, CallingConvention = CallingConvention.Cdecl)] private static extern void mono_profiler_install_allocation(IntPtr callback);
        [DllImport(Mono, CallingConvention = CallingConvention.Cdecl)] private static extern void mono_profiler_set_events(int events);
        [DllImport(Mono, CallingConvention = CallingConvention.Cdecl)] private static extern uint mono_object_get_size(IntPtr obj);
        [DllImport(Mono, CallingConvention = CallingConvention.Cdecl)] private static extern void mono_stack_walk_no_il(IntPtr callback, IntPtr data);
        [DllImport(Mono, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr mono_class_get_type(IntPtr klass);
        [DllImport(Mono, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr mono_type_get_name(IntPtr type);
        [DllImport(Mono, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr mono_method_get_name(IntPtr method);
        [DllImport(Mono, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr mono_method_get_class(IntPtr method);
    }
}
#endif
