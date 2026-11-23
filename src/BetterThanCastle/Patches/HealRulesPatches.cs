using System;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // G4: custom healing (BtcHealRules).
    [HarmonyPatch(typeof(RegenAura), "HealTick")]
    internal static class HealRulesAuraTickPatch
    {
        private static void Prefix(RegenAura __instance)
        {
            try { BtcHealRules.OnAuraTick(__instance); }
            catch (Exception e) { BtcLog.Error("HealRules", "aura: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(BricktronDamageReceiver), "BrixFixedUpdate")]
    internal static class HealRulesBricktronTickPatch
    {
        private static void Prefix(BricktronDamageReceiver __instance)
        {
            try { BtcHealRules.OnBricktronTick(__instance); }
            catch (Exception) { }
        }
    }
}
