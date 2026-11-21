using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game.AI;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(BuildGoalProvider.FulfillableEnumerator), "MoveNext")]
    internal static class BuildGoalProviderFulfillableEnumeratorMoveNextPatch
    {
        // vanilla line 424: this.searched >= GeneralRules.BuildProjectSearchPathBudget -> BtcBalanceExtra.BuildBudget(...)
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            CodeMatcher cm = new CodeMatcher(ins);
            cm.MatchForward(false, new CodeMatch(i => (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt)
                && i.operand is MethodInfo m && m.Name == "get_BuildProjectSearchPathBudget"));
            if (cm.IsInvalid)
            {
                BtcLog.Error("Hook", "BuildGoalProvider.FulfillableEnumerator.MoveNext: BuildProjectSearchPathBudget not found");
                return ins;
            }
            cm.Advance(1).Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BtcBalanceExtra), "BuildBudget")));
            return cm.InstructionEnumeration();
        }
    }
}
