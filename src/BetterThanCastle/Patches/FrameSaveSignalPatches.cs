using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.External.Signals;
using Brix.Utils.Collections;
using HarmonyLib;

namespace BetterThanCastle
{
    /// What the game did: every signal keeps its connections in a StructLinkedList whose constructor makes two List&lt;int&gt;
    /// (_dataIndices, _freeIndices). Most signals are never connected: ~700k empty lists stood in a running base, a ninth of
    /// all managed objects, each one scanned by Mono's GC.
    /// What the saver does: after the constructor both fields point to ONE shared empty list; Alloc (the only code that
    /// ever writes to them first) gives the list its own two before the first node is added. Before that the lists are only
    /// read (Count, indexer: an empty list throws exactly as before) and Clear() on an empty list changes nothing. A
    /// list still sharing is told by both fields being the same object. Alloc is patched BEFORE the constructor and only
    /// the types whose Alloc patch succeeded get the constructor postfix.
    /// Ported from Castle Story Plus (Performance/LazySignalLists.cs); the constructor is patched with a Postfix instead
    /// of a transpiler (generic shared code on Mono 5.6: no IL rewriting of closed types).
    [HarmonyPatch]
    internal static class FrameSaveLazySignalLists
    {
        private static readonly string[] Assemblies = { "Brix.Core", "Assembly-CSharp-firstpass", "Assembly-CSharp" };

        /// Never written to.
        private static readonly List<int> Shared = new List<int>();

        private static List<MethodBase> _ctors;

        static bool Prepare()
        {
            if (!BtcFrameSave.On) return false;
            if (_ctors != null) return _ctors.Count > 0;
            _ctors = new List<MethodBase>();
            try
            {
                Harmony allocHarmony = new Harmony("com.btc.betterthancastle.framesave.lazylists");
                HarmonyMethod prefix = new HarmonyMethod(typeof(FrameSaveLazySignalLists).GetMethod("AllocPrefix", BindingFlags.Static | BindingFlags.NonPublic));
                int failed = 0;
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                List<Type> types = ListTypes();
                long scanMs = sw.ElapsedMilliseconds;
                foreach (Type list in types)
                {
                    try
                    {
                        MethodInfo alloc = AccessTools.Method(list, "Alloc");
                        ConstructorInfo ctor = list.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                        if (alloc == null || ctor == null) throw new MissingMethodException(list.FullName, alloc == null ? "Alloc" : ".ctor");
                        allocHarmony.Patch(alloc, prefix: prefix);
                        _ctors.Add(ctor);
                    }
                    catch (Exception e)
                    {
                        failed++;
                        BtcLog.Warn("FrameSave", "LazySignalLists: " + list.FullName + " skipped: " + e.Message);
                    }
                }
                BtcLog.Info("FrameSave", "LazySignalLists: " + _ctors.Count + " linked list types start with a shared empty list" + (failed > 0 ? ", " + failed + " failed" : "") + " (scan " + scanMs + " ms, alloc patches " + (sw.ElapsedMilliseconds - scanMs) + " ms)");
            }
            catch (Exception e)
            {
                BtcLog.Error("FrameSave", "LazySignalLists Prepare: " + e);
            }
            return _ctors.Count > 0;
        }

        static IEnumerable<MethodBase> TargetMethods()
        {
            return _ctors;
        }

        static void Postfix(ref List<int> ____dataIndices, ref List<int> ____freeIndices)
        {
            ____dataIndices = Shared;
            ____freeIndices = Shared;
        }

        /// Before the first node: the list gets two lists of its own.
        static void AllocPrefix(ref List<int> ____dataIndices, ref List<int> ____freeIndices)
        {
            if (ReferenceEquals(____dataIndices, ____freeIndices))
            {
                ____dataIndices = new List<int>();
                ____freeIndices = new List<int>();
            }
        }

        /// Each closed StructLinkedList the game types name: a signal's _actions and fields of a linked list type.
        private static List<Type> ListTypes()
        {
            List<Type> lists = new List<Type>();
            HashSet<Type> seen = new HashSet<Type>();
            foreach (string name in Assemblies)
            {
                Assembly assembly = Find(name);
                if (assembly == null) { BtcLog.Warn("FrameSave", "LazySignalLists: assembly " + name + " not found"); continue; }
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                foreach (Type type in types)
                {
                    if (type == null) continue;
                    try
                    {
                        Collect(type, lists, seen);
                        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                            Collect(field.FieldType, lists, seen);
                    }
                    catch (Exception) { }
                }
            }
            return lists;
        }

        private static void Collect(Type type, List<Type> lists, HashSet<Type> seen)
        {
            if (type == null || type.IsGenericParameter || !seen.Add(type)) return;
            if (type.IsArray || type.IsByRef || type.IsPointer) { Collect(type.GetElementType(), lists, seen); return; }
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                Type definition = type.GetGenericTypeDefinition();
                if (!type.ContainsGenericParameters)
                {
                    if (definition == typeof(StructLinkedList<>)) lists.Add(type);
                    else if (definition == typeof(SignalBase<>))
                    {
                        FieldInfo actions = AccessTools.Field(type, "_actions");
                        if (actions != null) Collect(actions.FieldType, lists, seen);
                    }
                }
                foreach (Type argument in type.GetGenericArguments()) Collect(argument, lists, seen);
            }
            Collect(type.BaseType, lists, seen);
        }

        private static Assembly Find(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == name) return assembly;
            try { return Assembly.Load(name); }
            catch (Exception) { return null; }
        }
    }
}
