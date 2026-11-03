using System;
using Brix.Assets;
using HarmonyLib;
using MoonSharp.Interpreter;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(Asset_Gamemode), "ApplyPresetSettingsToScript")]
    internal static class AssetGamemodeApplyPresetSettingsToScriptPatch
    {
        // mod: BtcBalanceExtra.ApplyStart(sv_Settings) before each preset setting is written (only runs when the preset has settings).
        // Disk_ReadFactionPrototypes -> BtcLobby.ExtendProtos: see AssetGamemodeDiskReadFactionPrototypesPatch (network wave).
        private static void Prefix(Script script, Asset_GamemodePreset preset)
        {
            try
            {
                Table table = script.Globals["sv_Settings"] as Table;
                if (table != null && preset != null && preset.settings != null && preset.settings.Count > 0)
                    BtcBalanceExtra.ApplyStart(table);
            }
            catch (Exception e) { BtcLog.Error("Hook", "Asset_Gamemode.ApplyPresetSettingsToScript: " + e); }
        }
    }

    /// <summary>
    /// Disk_LoadGamemode: after the server script ran, install the lobby spawn selection wrappers
    /// (BtcSpawnPick: original conquest/sandbox only, host + BC ON + non-empty table; local server Lua, no wire change).
    /// </summary>
    [HarmonyPatch(typeof(Asset_Gamemode), "Disk_LoadGamemode")]
    internal static class AssetGamemodeDiskLoadGamemodePatch
    {
        private static void Postfix(Asset_Gamemode __instance, ref Script outscript)
        {
            if (!BtcNetworkGate.Enabled) return;
            try { BtcSpawnPick.OnGamemodeLoaded(__instance, outscript); }
            catch (Exception e) { BtcLog.Error("Hook", "Asset_Gamemode.Disk_LoadGamemode: " + e); }
        }
    }

    /// <summary>
    /// Disk_ReadFactionPrototypes: legacy `return BtcLobby.ExtendProtos(array)` (extra player slots / colours + spectator
    /// slot). Changes the faction list both peers build, so BC ON only (ExtendProtos also requires BtcLobby.Active).
    /// </summary>
    [HarmonyPatch(typeof(Asset_Gamemode), "Disk_ReadFactionPrototypes")]
    internal static class AssetGamemodeDiskReadFactionPrototypesPatch
    {
        private static void Postfix(ref Asset_Gamemode.FactionPrototype[] __result)
        {
            if (!BtcNetworkGate.Enabled) return;
            try { __result = BtcLobby.ExtendProtos(__result); }
            catch (Exception e) { BtcLog.Error("Hook", "Asset_Gamemode.Disk_ReadFactionPrototypes: " + e); }
        }
    }
}
