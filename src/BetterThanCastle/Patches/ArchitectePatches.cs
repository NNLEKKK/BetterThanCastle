using System;
using Brix.Engine;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(Architecte), "OnLoadError")]
    internal static class ArchitecteOnLoadErrorPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Exception e)
        {
            try { BtcSpectate.OnLoadError(e); }
            catch (Exception ex) { BtcLog.Error("Hook", "Architecte.OnLoadError: " + ex); }
        }
    }
}
