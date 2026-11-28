using System;
using Brix.Game.AI;
using Brix.Game.AI.Nodes;
using Brix.Game.Components;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Rule trap.fix (BtcTrapFix): the trap stun task is not overwritten by other orders; a re-armed trap forgets its last prisoner.
    [HarmonyPatch(typeof(Labor), "SendOrder")]
    internal static class TrapFixSendOrderPatch
    {
        private static bool Prefix(Labor __instance, Task task, Labor.DecisionSource source)
        {
            try { return BtcTrapFix.BeforeSend(__instance, task, source); }
            catch (Exception e) { BtcLog.Error("TrapFix", "SendOrder: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(ObjectActivator), "Active", MethodType.Setter)]
    internal static class TrapFixActivePatch
    {
        private static void Postfix(ObjectActivator __instance, bool value)
        {
            try { BtcTrapFix.OnActive(__instance, value); }
            catch (Exception e) { BtcLog.Error("TrapFix", "Active: " + e.Message); }
        }
    }
}
