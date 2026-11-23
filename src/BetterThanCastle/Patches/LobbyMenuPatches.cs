using System;
using Brix.UI.Builder.Menu;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // New lobby: the host starts on the map and mode chosen in Gameplay > Map > Lobby defaults (BtcLobbyDefaults)
    // instead of vanilla's best-rated official map. Local selection only; vanilla syncs it to clients.
    [HarmonyPatch(typeof(LobbyMenu), "OpenSelf")]
    internal static class LobbyMenuOpenSelfPatch
    {
        private static void Postfix()
        {
            try { BtcLobbyDefaults.OnLobbyOpen(); }
            catch (Exception ex) { BtcLog.Error("LobbyDef", "OpenSelf: " + ex); }
        }
    }
}
