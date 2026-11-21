using System;
using Brix.Game;
using Brix.Game.Components;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Extended player colours (Purple/Pink/Cyan/Black) map to a vanilla CrystalColorEnum for the animator; BtcCrystalTint then
    // recolours the animated material properties in LateUpdate. Local visuals only, no network.
    [HarmonyPatch(typeof(CrystalVisuals), "FactionChanged")]
    internal static class CrystalVisualsFactionChangedPatch
    {
        private static void Postfix(CrystalVisuals __instance, Faction newFaction)
        {
            try { BtcCrystalTint.SetOwner(__instance, newFaction); }
            catch (Exception e) { BtcLog.Error("Hook", "CrystalVisuals.FactionChanged: " + e); }
        }
    }

    [HarmonyPatch(typeof(CrystalVisuals), "StartTransition")]
    internal static class CrystalVisualsStartTransitionPatch
    {
        private static void Postfix(CrystalVisuals __instance, Faction capturer)
        {
            try { BtcCrystalTint.SetCapturer(__instance, capturer); }
            catch (Exception e) { BtcLog.Error("Hook", "CrystalVisuals.StartTransition: " + e); }
        }
    }

    [HarmonyPatch(typeof(CrystalVisuals), "EndTransition")]
    internal static class CrystalVisualsEndTransitionPatch
    {
        private static void Postfix(CrystalVisuals __instance)
        {
            try { BtcCrystalTint.SetCapturer(__instance, null); }
            catch (Exception e) { BtcLog.Error("Hook", "CrystalVisuals.EndTransition: " + e); }
        }
    }

    // CrystalDriver.Start sets the initial colour directly (not via FactionChanged).
    [HarmonyPatch(typeof(CrystalDriver), "Start")]
    internal static class CrystalDriverStartPatch
    {
        private static void Postfix(CrystalVisuals ___crystal, AffiliationComponent ___affiliation)
        {
            try { if (___crystal != null && ___affiliation != null) BtcCrystalTint.SetOwner(___crystal, ___affiliation.faction); }
            catch (Exception e) { BtcLog.Error("Hook", "CrystalDriver.Start: " + e); }
        }
    }
}
