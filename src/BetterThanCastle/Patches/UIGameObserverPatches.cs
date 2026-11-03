using System;
using Brix.Input;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(UIGameObserver), "lua_SetModeVictoryTextureAsync")]
    internal static class UIGameObserverVictoryTexturePatch
    {
        private static void Prefix()
        {
            try { BtcDiscord.MarkResult(true); }
            catch (Exception e) { BtcLog.Error("Hook", "UIGameObserver.lua_SetModeVictoryTextureAsync: " + e); }
        }
    }

    [HarmonyPatch(typeof(UIGameObserver), "lua_SetModeDefeatTextureAsync")]
    internal static class UIGameObserverDefeatTexturePatch
    {
        private static void Prefix()
        {
            try { BtcDiscord.MarkResult(false); }
            catch (Exception e) { BtcLog.Error("Hook", "UIGameObserver.lua_SetModeDefeatTextureAsync: " + e); }
        }
    }

    // Legacy: Start() ends by adding the hotkey components.
    [HarmonyPatch(typeof(UIGameObserver), "Start")]
    internal static class UIGameObserverStartPatch
    {
        private static void Postfix(UIGameObserver __instance)
        {
            try
            {
                if (__instance.GetComponent<JobSelectHotkeys>() == null) __instance.gameObject.AddComponent<JobSelectHotkeys>();
                if (__instance.GetComponent<CameraBookmarkHotkey>() == null) __instance.gameObject.AddComponent<CameraBookmarkHotkey>();
            }
            catch (Exception e) { BtcLog.Error("Hook", "UIGameObserver.Start: " + e); }
        }
    }
}
