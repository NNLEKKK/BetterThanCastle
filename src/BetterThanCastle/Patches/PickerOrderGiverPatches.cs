using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Input;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    internal static class PickerOrderGiverHook
    {
        // Inserts "BtcWaypoints.OnClientOrder(listGO);" right before the first User.LocalUser read
        // (first statement inside "if (AcceptNewOrder(...)) { ... }" in both DirectOrder overloads).
        internal static IEnumerable<CodeInstruction> Insert(IEnumerable<CodeInstruction> instructions, string where)
        {
            var list = new List<CodeInstruction>(instructions);
            MethodInfo hook = AccessTools.Method(typeof(BtcWaypoints), "OnClientOrder");
            for (int i = 0; i < list.Count; i++)
            {
                FieldInfo mi = list[i].operand as FieldInfo;
                if (mi != null && list[i].opcode == OpCodes.Ldsfld && mi.Name == "LocalUser")
                {
                    var ldarg = new CodeInstruction(OpCodes.Ldarg_1);
                    ldarg.MoveLabelsFrom(list[i]);
                    list.Insert(i, ldarg);
                    list.Insert(i + 1, new CodeInstruction(OpCodes.Call, hook));
                    return list;
                }
            }
            BtcLog.Error("Hook", where + ": ldsfld User.LocalUser anchor not found");
            return list;
        }
    }

    [HarmonyPatch(typeof(PickerOrderGiver), "DirectOrder", new Type[] { typeof(List<GameObject>), typeof(Vector3) })]
    internal static class PickerOrderGiverDirectOrderVectorPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return PickerOrderGiverHook.Insert(instructions, "PickerOrderGiver.DirectOrder(Vector3)");
        }
    }

    [HarmonyPatch(typeof(PickerOrderGiver), "DirectOrder", new Type[] { typeof(List<GameObject>), typeof(GameObject) })]
    internal static class PickerOrderGiverDirectOrderObjectPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return PickerOrderGiverHook.Insert(instructions, "PickerOrderGiver.DirectOrder(GameObject)");
        }
    }
}
