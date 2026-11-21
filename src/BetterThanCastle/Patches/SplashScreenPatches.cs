using System;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Skip intro: SplashScreen.Start connects a callback that calls LoadNextScene() when the public field `skip` is set.
    [HarmonyPatch(typeof(Brix.UI.SplashScreen), "Start")]
    internal static class SplashScreenStartPatch
    {
        private static void Prefix(Brix.UI.SplashScreen __instance)
        {
            try
            {
                if (BtcConfig.SkipIntro != null && BtcConfig.SkipIntro.Value && !__instance.skip)
                {
                    __instance.skip = true;
                    BtcLog.Info("Display", "intro skipped");
                }
            }
            catch (Exception e) { BtcLog.Error("Hook", "SplashScreen.Start: " + e); }
        }
    }
}
