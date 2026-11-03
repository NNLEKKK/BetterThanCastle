using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game.AI;
using Brix.Game.AI.Nodes;
using HarmonyLib;
using Motus.Behavior;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// Shared by NavigationNodes/Labor patches: inserts ".Do(l => BtcQol.OnRunOrder(l), labor)" into a
    /// Node.Sequence chain right after the first call whose name is afterCall.
    /// </summary>
    internal static class SeqHook
    {
        internal static void OnRun(Labor l)
        {
            try { BtcQol.OnRunOrder(l); }
            catch (Exception e) { BtcLog.Error("Hook", "OnRunOrder: " + e); }
        }

        internal static IEnumerable<CodeInstruction> InsertOnRun(IEnumerable<CodeInstruction> instructions, string afterCall, OpCode loadLabor, string where)
        {
            var list = new List<CodeInstruction>(instructions);
            MethodInfo doDef = null;
            foreach (MethodInfo m in typeof(SequenceBuilderExtender).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != "Do" || !m.IsGenericMethodDefinition || m.GetGenericArguments().Length != 2) continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != 3) continue;
                Type p1 = ps[1].ParameterType;
                if (p1.IsGenericType && p1.GetGenericTypeDefinition() == typeof(Action<>)
                    && ps[2].ParameterType == m.GetGenericArguments()[1])
                {
                    doDef = m;
                    break;
                }
            }
            MethodInfo handler = AccessTools.Method(typeof(SeqHook), "OnRun");
            ConstructorInfo ctor = typeof(Action<Labor>).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) });
            if (doDef != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    MethodInfo mi = list[i].operand as MethodInfo;
                    if (mi == null || mi.Name != afterCall) continue;
                    MethodInfo doM = doDef.MakeGenericMethod(mi.ReturnType, typeof(Labor));
                    var ins = new List<CodeInstruction>
                    {
                        new CodeInstruction(OpCodes.Ldnull),
                        new CodeInstruction(OpCodes.Ldftn, handler),
                        new CodeInstruction(OpCodes.Newobj, ctor),
                        new CodeInstruction(loadLabor),
                        new CodeInstruction(OpCodes.Call, doM)
                    };
                    list.InsertRange(i + 1, ins);
                    return list;
                }
            }
            BtcLog.Error("Hook", where + ": anchor " + afterCall + " / Do overload not found");
            return list;
        }
    }

    // Vanilla: Node.Sequence.Label("RunToExactly Sequence").Do(...). Legacy inserts the OnRunOrder step first.
    [HarmonyPatch(typeof(NavigationNodes), "RunToExactly", new Type[] { typeof(Labor), typeof(Location) })]
    internal static class NavigationNodesRunToExactlyPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SeqHook.InsertOnRun(instructions, "Label", OpCodes.Ldarg_0, "NavigationNodes.RunToExactly");
        }
    }

    // Vanilla: Node.Sequence.Label("RunToward Sequence").Do(...)
    [HarmonyPatch(typeof(NavigationNodes), "RunToward", new Type[] { typeof(Labor), typeof(Location), typeof(bool), typeof(bool) })]
    internal static class NavigationNodesRunTowardPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SeqHook.InsertOnRun(instructions, "Label", OpCodes.Ldarg_0, "NavigationNodes.RunToward");
        }
    }
}
