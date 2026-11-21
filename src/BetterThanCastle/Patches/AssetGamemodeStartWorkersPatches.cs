using System;
using Brix.Assets;
using HarmonyLib;
using MoonSharp.Interpreter;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// Disk_LoadGamemode (server script just ran): install the per-player "starting workers" handicap wrapper
    /// (BtcHandicap.InstallStartWorkers; host + BC ON + new game, original conquest/sandbox only, local server Lua, no wire change).
    /// </summary>
    [HarmonyPatch(typeof(Asset_Gamemode), "Disk_LoadGamemode")]
    internal static class AssetGamemodeDiskLoadGamemodeStartWorkersPatch
    {
        private static void Postfix(Asset_Gamemode __instance, ref Script outscript)
        {
            if (!BtcNetworkGate.Enabled) return;
            try { BtcHandicap.InstallStartWorkers(__instance, outscript); }
            catch (Exception e) { BtcLog.Error("Hook", "Asset_Gamemode.Disk_LoadGamemode(handicap): " + e); }
        }
    }
}
