using System;
using Brix.Game.AI;
using Brix.Game.AI.Nodes;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Rule "mov.noskip" (BC ON only through BtcGameplay.E). The duel mode has its own guard in DuelAttackInfoStrikePatch.
    [HarmonyPatch(typeof(AttackInfo), "Strike")]
    internal static class NoSkipAttackInfoStrikePatch
    {
        private static bool Prefix(AttackInfo __instance, out bool __state)
        {
            __state = false;
            try
            {
                if (BtcDuel.Active || !BtcNoSkip.On) return true;
                if (BtcNoSkip.Blocked(__instance.Attacker)) return false;
                __state = true;
            }
            catch (Exception e) { BtcLog.Error("Hook", "NoSkipStrike: " + e); }
            return true;
        }

        private static void Postfix(AttackInfo __instance, bool __state)
        {
            if (!__state) return;
            try { BtcNoSkip.Record(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "NoSkipStrikeRecord: " + e); }
        }
    }
}
