using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game.AI;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(Navigation.NavigateOrderData), "InitSearch")]
    internal static class NavigateOrderDataInitSearchPatch
    {
        internal static int Budget(int original, Navigation.NavigateOrderData self)
        {
            try
            {
                int b = BtcBalanceExtra.NavBudget(self.navigation.faction.isAI, original);
                // rule mov.instant: a player's order searches 600 nodes per update (only while that search runs), so a squad starts ticks earlier
                Labor l = self.navigation.Labor;
                if (b < 600 && !self.navigation.faction.isAI && BtcInstantOrders.On && l != null && l.CurrentTask != null && l.CurrentTask.InitialPackage != null && l.CurrentTask.InitialPackage.DecisionSource == Labor.DecisionSource.User) b = 600;
                return b;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Navigation.InitSearch: " + e); return original; }
        }

        // vanilla line 567: _searchRequest.SearchPerUpdate = (ai ? corruptronBudget : autonomyBudget) -> BtcBalanceExtra.NavBudget(ai, ...)
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            CodeMatcher cm = new CodeMatcher(ins);
            cm.MatchForward(false, new CodeMatch(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.Name == "SearchPerUpdate"));
            if (cm.IsInvalid)
            {
                BtcLog.Error("Hook", "Navigation.NavigateOrderData.InitSearch: SearchPerUpdate store not found");
                return ins;
            }
            cm.Insert(new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NavigateOrderDataInitSearchPatch), "Budget")));
            return cm.InstructionEnumeration();
        }
    }
}
