using System;
using Brix.Assets;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // G5: the stuck-heal list is written into the save folder and read back on load (BtcInfHeal). Host only, BC ON.
    [HarmonyPatch(typeof(Asset_Map), "Disk_Save")]
    internal static class InfHealDiskSavePatch
    {
        private static void Postfix(Asset_Map __instance)
        {
            try { BtcInfHeal.OnSave(__instance.path); BtcHeldWards.OnSave(__instance.path); }
            catch (Exception e) { BtcLog.Error("Hook", "InfHeal.Save: " + e); }
        }
    }

    [HarmonyPatch(typeof(Asset_Map), "Disk_ReadGameObjects")]
    internal static class InfHealDiskReadPatch
    {
        private static void Prefix(Asset_Map __instance)
        {
            try { BtcInfHeal.OnLoad(__instance.path); BtcHeldWards.OnLoad(__instance.path); }
            catch (Exception e) { BtcLog.Error("Hook", "InfHeal.Load: " + e); }
        }
    }
}
