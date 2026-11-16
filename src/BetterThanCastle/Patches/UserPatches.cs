using System;
using Brix.Game;
using HarmonyLib;
using UnityEngine.Networking;

namespace BetterThanCastle.Patches
{
    // User.Start (server) names the user from the lobby list. A User the host creates for a returning player or a spectator in a
    // running game has no lobby row, so it was renamed "Player" one frame after BtcSpectate had set the real nick: the next rejoin
    // of that player then found no user by name and was refused (BC ON only; BtcSpectate jobs only exist with BC ON).
    [HarmonyPatch(typeof(User), "Start")]
    internal static class UserStartNamePatch
    {
        private static void Postfix(User __instance)
        {
            try
            {
                if (!BtcNetworkGate.Enabled || !NetworkServer.active || __instance == null) return;
                string name = BtcSpectate.NameOf(__instance.connectionToClient);
                if (string.IsNullOrEmpty(name) || __instance.ScreenName == name) return;
                BtcLog.Info("Net", "User of conn " + __instance.connectionToClient.connectionId + " named '" + name + "' (was '" + __instance.ScreenName + "')");
                __instance.Network_screenName = name;
            }
            catch (Exception e) { BtcLog.Error("Hook", "User.Start name: " + e.Message); }
        }
    }
}
