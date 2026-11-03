using System;
using Brix.Network;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// SimpleHostScript.Run (host faction setup at game start). Legacy rewrote it: spectator slot users become
    /// Faction.Spectator (BtcLobby.MakeSpectator), replay proxy users get factions too (BtcReplay.ProxyUsers), index guards.
    /// The vanilla body is all closures (no stable IL anchor), so BC ON / replay proxies run the legacy body
    /// (BtcNetManager.HostRun, ~70 lines); BC OFF without proxies runs vanilla untouched.
    /// </summary>
    [HarmonyPatch(typeof(SimpleHostScript), "Run")]
    internal static class SimpleHostScriptRunPatch
    {
        private static bool Prefix()
        {
            bool proxies = BtcReplay.ProxyUsers != null && BtcReplay.ProxyUsers.Count > 0;
            if (!BtcNetworkGate.Enabled && !proxies) return true;
            try
            {
                BtcNetManager.HostRun();
                return false;
            }
            catch (Exception e)
            {
                // HostRun may have spawned factions already: running vanilla again would double-spawn. Log loudly.
                BtcLog.Error("Hook", "SimpleHostScript.Run (BTC body): " + e);
                return false;
            }
        }
    }
}
