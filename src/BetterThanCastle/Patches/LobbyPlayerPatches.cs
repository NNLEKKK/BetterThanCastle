using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.Network;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Legacy LobbyPlayer.cs: 21 hunks, only 2 real (OnNameChange, CmdSetPlayerName); the rest is decompiler noise
    // (weaver UNetVersion/OnSerialize/OnDeserialize/InvokeCmd* are byte-identical in behaviour -> not touched).

    /// <summary>
    /// Parity item (b): no "X is now called Y" chat line (and no LobbyPlayerJoined re-signal) when only the colour tags of
    /// a nick changed (BtcNick Rainbow tab / animated rainbow -> BtcNick.SameNick compares the plain text). BC ON only.
    /// Runs on the host (the chat RPC is server-side); clients just skip the duplicate join signal.
    /// </summary>
    [HarmonyPatch(typeof(LobbyPlayer), "OnNameChange")]
    internal static class LobbyPlayerOnNameChangePatch
    {
        private static bool Prefix(LobbyPlayer __instance, string dstName)
        {
            if (!BtcNetworkGate.Enabled) return true;
            string text;
            try
            {
                text = __instance.playerName;
                if (string.IsNullOrEmpty(text) || !BtcNick.SameNick(text, dstName)) return true;   // real rename / first name: vanilla
            }
            catch (Exception e) { BtcLog.Error("Hook", "LobbyPlayer.OnNameChange: " + e); return true; }
            // same plain nick, only colours differ: vanilla body minus LobbyPlayerJoined and minus the rename chat message
            __instance.NetworkplayerName = dstName;
            __instance.gameObject.name = dstName;
            GameSignals.Invoke(UNetManager.PlayerListChanged);
            return false;
        }
    }

    /// <summary>Parity item (a): server-side nick cap 30 -> BtcNetManager.NickLimit() (999 when the host runs BC ON).</summary>
    [HarmonyPatch(typeof(LobbyPlayer), "CmdSetPlayerName")]
    internal static class LobbyPlayerCmdSetPlayerNamePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            return NickLimitIl.Replace30(ins, "LobbyPlayer.CmdSetPlayerName");
        }
    }
}
