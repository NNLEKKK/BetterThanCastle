using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Engine;
using Brix.Engine.FlatWorld.Layers;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(DeepLayer), "GetDeepType")]
    internal static class DeepLayerGetDeepTypePatch
    {
        internal static float OreThr(float threshold, VoxelTerrainType key)
        {
            try { return BtcWorld.OreThr(key, threshold); }
            catch (Exception e) { BtcLog.Error("Hook", "DeepLayer.OreThr: " + e); return threshold; }
        }

        // mod: voxelPos = BtcSymmetry.FoldPos(voxelPos) at method start (Postfix below sees the folded arg too)
        private static void Prefix(ref Vector3 voxelPos)
        {
            try { voxelPos = BtcSymmetry.FoldPos(voxelPos); }
            catch (Exception e) { BtcLog.Error("Hook", "DeepLayer.GetDeepType(prefix): " + e); }
        }

        // mod: return BtcWorld.DeepFix(result, voxelPos)
        private static void Postfix(Vector3 voxelPos, ref VoxelTerrainType __result)
        {
            try { __result = BtcWorld.DeepFix(__result, voxelPos); }
            catch (Exception e) { BtcLog.Error("Hook", "DeepLayer.GetDeepType(postfix): " + e); }
        }

        // vanilla: value2 > (double)value.Threshold  ->  (double)BtcWorld.OreThr(key, value.Threshold); key = the foreach KeyValuePair.Key local
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(ins);
            CodeInstruction keySt = null;
            for (int i = 0; i + 1 < list.Count; i++)
            {
                MethodInfo m = list[i].operand as MethodInfo;
                if (m != null && (list[i].opcode == OpCodes.Call || list[i].opcode == OpCodes.Callvirt) && m.Name == "get_Key"
                    && m.ReturnType == typeof(VoxelTerrainType) && list[i + 1].IsStloc())
                {
                    keySt = list[i + 1];
                    break;
                }
            }
            int thr = -1;
            for (int i = 0; i < list.Count; i++)
            {
                FieldInfo f = list[i].operand as FieldInfo;
                if (list[i].opcode == OpCodes.Ldfld && f != null && f.Name == "Threshold") { thr = i; break; }
            }
            if (keySt == null || thr < 0)
            {
                BtcLog.Error("Hook", "DeepLayer.GetDeepType: key local / Threshold not found");
                return list;
            }
            list.InsertRange(thr + 1, new[]
            {
                LdlocFrom(keySt),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DeepLayerGetDeepTypePatch), "OreThr"))
            });
            return list;
        }
        private static CodeInstruction LdlocFrom(CodeInstruction st)
        {
            if (st.opcode == OpCodes.Stloc_0) return new CodeInstruction(OpCodes.Ldloc_0);
            if (st.opcode == OpCodes.Stloc_1) return new CodeInstruction(OpCodes.Ldloc_1);
            if (st.opcode == OpCodes.Stloc_2) return new CodeInstruction(OpCodes.Ldloc_2);
            if (st.opcode == OpCodes.Stloc_3) return new CodeInstruction(OpCodes.Ldloc_3);
            return new CodeInstruction(st.opcode == OpCodes.Stloc_S ? OpCodes.Ldloc_S : OpCodes.Ldloc, st.operand);
        }
    }
}
