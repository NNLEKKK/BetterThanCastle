using System;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Legacy: ResourcePackManager.Boot() first thing in Awake (idempotent).
    [HarmonyPatch(typeof(AkInitializer), "Awake")]
    internal static class AkInitializerAwakePatch
    {
        private static void Prefix()
        {
            try { ResourcePackManager.Boot(); }
            catch (Exception e) { BtcLog.Error("Hook", "AkInitializer.Awake: " + e); }
        }
    }
}
