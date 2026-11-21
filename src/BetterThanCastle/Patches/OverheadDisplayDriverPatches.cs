using System;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Unit name plate / mode icon scale (local visual). BrixUpdate is declared in OverheadDisplayDriver;
    // BricktronOverheadDisplayDriver does not override it, so filter on the instance type.
    [HarmonyPatch(typeof(OverheadDisplayDriver), "BrixUpdate")]
    internal static class OverheadDisplayDriverBrixUpdatePatch
    {
        private static void Postfix(OverheadDisplayDriver __instance)
        {
            try
            {
                if (BtcUnitPlates.IsDefaultFast && !BtcUnitPlates.AnyApplied) return;
                if (!(__instance is BricktronOverheadDisplayDriver)) return;
                BtcUnitPlates.Apply(__instance.overheadDisplay);
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "OverheadDisplayDriver.BrixUpdate: " + e);
            }
        }
    }
}
