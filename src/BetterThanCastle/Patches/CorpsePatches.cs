using System;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(Corpse), "GetRefundEnergy")]
    internal static class CorpseGetRefundEnergyPatch
    {
        // Server-side energy refund calculation (not in an OnSerialize/SyncVar path).
        private static bool Prefix(Corpse __instance, ref int __result)
        {
            try
            {
                __result = BtcHandicap.Refund(__instance.originalFaction, BtcGameplay.Refund(__instance.originalFaction));
                return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "Corpse.GetRefundEnergy: " + e);
                return true;
            }
        }
    }
}
