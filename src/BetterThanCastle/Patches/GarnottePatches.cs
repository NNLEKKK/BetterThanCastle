using System;
using Brix.Legacy;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(Garnotte), "Initialize")]
    internal static class GarnotteInitializePatch
    {
        private static void Postfix(Garnotte __instance)
        {
            try { BtcSimplePhys.Item(__instance.gameObject); }
            catch (Exception e) { BtcLog.Error("Hook", "Garnotte.Initialize: " + e); }
        }
    }
}
