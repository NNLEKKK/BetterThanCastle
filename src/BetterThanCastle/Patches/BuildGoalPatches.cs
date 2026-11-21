using Motus.Behavior;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Semantique;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(BuildGoal), "OnBlueprintStateChanged")]
    internal static class BuildGoalOnBlueprintStateChangedPatch
    {
        private static void Prefix(BuildGoal __instance)
        {
            try { BtcBuildAI.Stamp(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "BuildGoal.OnBlueprintStateChanged: " + e); }
        }
    }

    [HarmonyPatch(typeof(BuildGoal), "PriorityFor")]
    internal static class BuildGoalPriorityForPatch
    {
        private static void Postfix(BuildGoal __instance, Labor labor, ref int __result)
        {
            try { __result += BtcBuildAI.Prio(__instance, labor); }
            catch (Exception e) { BtcLog.Error("Hook", "BuildGoal.PriorityFor: " + e); }
        }
    }

    [HarmonyPatch(typeof(BuildGoal), "IsMatchFor")]
    internal static class BuildGoalIsMatchForPatch
    {
        internal static bool TrustOr(BuildGoal g, Labor labor)
        {
            try { return BtcBuildAI.Trust(g, labor); }
            catch (Exception e) { BtcLog.Error("Hook", "BuildGoal.Trust: " + e); return false; }
        }

        internal static bool CrowdedErr(BuildGoal g, Labor labor, AIErrorsStore store)
        {
            try
            {
                if (!BtcBuildAI.Crowded(g, labor)) return false;
                store.AddDecisionError(SharedWorkException<Exceptions.GoalTooCrowded>.Get.Problem, labor);
                return true;
            }
            catch (Exception e) { BtcLog.Error("Hook", "BuildGoal.Crowded: " + e); return false; }
        }

        internal static bool OrCarries(bool hasRoom, Labor labor, GameObject o)
        {
            if (hasRoom) return true;
            try { return BtcBuildAI.CarriesOk(labor, o); }
            catch (Exception e) { BtcLog.Error("Hook", "BuildGoal.CarriesOk: " + e); return false; }
        }

        private static bool IsCall(CodeInstruction i, string name)
        {
            return (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt) && i.operand is MethodInfo m && m.Name == name;
        }

        // Targets vanilla IsMatchFor lines 271 (reach), 291 (find) and 292 (HasRoomFor).
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins, ILGenerator gen)
        {
            CodeMatcher cm = new CodeMatcher(ins, gen);

            // (1) if (!CanReachWithKnowledge(labor)) -> if (!CanReach && !Trust(this, labor))   [brtrue skip; + ldarg.0 ldarg.1 call Trust brtrue skip]
            cm.MatchForward(false, new CodeMatch(i => IsCall(i, "CanReachWithKnowledge")));
            if (cm.IsInvalid || (cm.Pos + 1) >= cm.Length || (cm.InstructionAt(1).opcode != OpCodes.Brtrue && cm.InstructionAt(1).opcode != OpCodes.Brtrue_S))
            {
                BtcLog.Error("Hook", "BuildGoal.IsMatchFor: CanReachWithKnowledge branch pattern not found");
                return ins;
            }
            object skip = cm.InstructionAt(1).operand;
            cm.Advance(2).Insert(new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BuildGoalIsMatchForPatch), "TrustOr")),
                new CodeInstruction(OpCodes.Brtrue, skip));

            // (2) after "GameObject o = labor.Find(...)": if (Crowded(this, labor)) { error; return false; }
            cm.Start();
            cm.MatchForward(false, new CodeMatch(i => IsCall(i, "Find") && ((MethodInfo)i.operand).DeclaringType == typeof(Labor)));
            if (cm.IsInvalid || (cm.Pos + 1) >= cm.Length || !cm.InstructionAt(1).IsStloc())
            {
                BtcLog.Error("Hook", "BuildGoal.IsMatchFor: labor.Find pattern not found");
                return ins;
            }
            Label cont = gen.DefineLabel();
            cm.Advance(2).Insert(
                new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BuildGoalIsMatchForPatch), "CrowdedErr")),
                new CodeInstruction(OpCodes.Brfalse, cont),
                new CodeInstruction(OpCodes.Ldc_I4_0),
                new CodeInstruction(OpCodes.Ret));
            cm.Advance(7);
            cm.Instruction.labels.Add(cont);

            // (3) labor.recepteur.HasRoomFor(o) -> HasRoomFor(o) || BtcBuildAI.CarriesOk(labor, o)
            cm.Start();
            cm.MatchForward(false, new CodeMatch(i => IsCall(i, "HasRoomFor")));
            if (cm.IsInvalid || cm.Pos < 1 || !cm.InstructionAt(-1).IsLdloc())
            {
                BtcLog.Error("Hook", "BuildGoal.IsMatchFor: HasRoomFor pattern not found");
                return ins;
            }
            CodeInstruction ldO = new CodeInstruction(cm.InstructionAt(-1));
            ldO.labels.Clear();
            cm.Advance(1).Insert(new CodeInstruction(OpCodes.Ldarg_1), ldO,
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BuildGoalIsMatchForPatch), "OrCarries")));
            return cm.InstructionEnumeration();
        }
    }
}
