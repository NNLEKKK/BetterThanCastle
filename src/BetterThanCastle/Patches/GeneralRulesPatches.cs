using System;
using Brix.Game.Rules;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(GeneralRules), "bricktronCap", MethodType.Getter)]
    internal static class GeneralRulesBricktronCapPatch
    {
        private static void Postfix(ref int __result)
        {
            try { __result = BtcGameplay.Cap(__result); }
            catch (Exception e) { BtcLog.Error("Hook", "GeneralRules.bricktronCap: " + e); }
        }
    }

    [HarmonyPatch(typeof(GeneralRules), "GetHomeCrystalSpawnRequiredEnergy")]
    internal static class GeneralRulesGetHomeCrystalSpawnRequiredEnergyPatch
    {
        private static bool Prefix(GeneralRules __instance, int activeCharacters, ref int __result)
        {
            try
            {
                __result = BtcGameplay.SpawnEnergy(activeCharacters, __instance.homeCrystalSpawnRequiredEnergyBase,
                    __instance.homeCrystalSpawnRequiredEnergyFactor, __instance.homeCrystalSpawnRequiredEnergyBeginCount);
                return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "GeneralRules.GetHomeCrystalSpawnRequiredEnergy: " + e);
                return true;
            }
        }
    }
}
