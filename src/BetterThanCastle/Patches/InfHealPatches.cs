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

    // a ward switched to healing (powered) releases every unit bound to it by the stuck-heal bug (restored from a save)
    [HarmonyPatch(typeof(Brix.Game.Components.ArtificerStaff), "ToHealing")]
    internal static class InfHealWardOnPatch
    {
        private static readonly System.Reflection.FieldInfo AuraField = AccessTools.Field(typeof(Brix.Game.Components.ArtificerStaff), "aura");

        private static void Postfix(Brix.Game.Components.ArtificerStaff __instance)
        {
            try { BtcInfHeal.OnWardOn(AuraField.GetValue(__instance) as Brix.Game.Components.RegenAura); }
            catch (Exception e) { BtcLog.Error("Hook", "InfHeal.WardOn: " + e); }
        }
    }
}
