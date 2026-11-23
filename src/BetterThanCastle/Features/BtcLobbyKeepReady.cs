using System;
using Brix.Network;
using Brix.UI.Builder.Menu;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
    // Lobby "Ready" is never cleared by itself. Vanilla LobbyMenu.SetEveryoneNotReady (host only) runs on every player
    // join/leave, every rename (LobbyPlayer.OnNameChange raises LobbyPlayerJoined, so animated nicks cleared it again and
    // again), and every map/mode/preset/selected-map change. It sets readyToBegin = false on the host's LobbyPlayer copies
    // only (no message), so clients still saw themselves as ready while the host waited for them.
    // Now: clients keep their ready until they untick it themselves. The host's own ready is dropped only when the map,
    // mode or preset really changed since the host pressed ready (the host has to press it again to send the new map;
    // clients stay ready, the start waits for their download via IsAnyPlayerDownloading as in vanilla).
    // Host-local state only, nothing sent: BetterConnection OFF/ON make no difference on the wire.
    internal static class BtcLobbyKeepReady
    {
        private static object _map, _mode, _preset;
        private static float _nextLog;

        internal static void HostReady(bool value)
        {
            if (!value) return;
            _map = GameParam.Map; _mode = GameParam.Gamemode; _preset = GameParam.Preset;
        }

        internal static bool SetupChanged
        {
            get
            {
                return !ReferenceEquals(_map, GameParam.Map) || !ReferenceEquals(_mode, GameParam.Gamemode)
                    || !ReferenceEquals(_preset, GameParam.Preset);
            }
        }

        internal static void Log(string msg)
        {
            if (Time.realtimeSinceStartup < _nextLog) return;
            _nextLog = Time.realtimeSinceStartup + 10f;
            BtcLog.Info("KeepReady", msg);
        }
    }

    [HarmonyPatch(typeof(LobbyMenu), "SetEveryoneNotReady")]
    internal static class BtcLobbyKeepReadyPatch
    {
        private static bool Prefix()
        {
            try
            {
                if (!LobbyMenu.IsHost) return true;
                LobbyPlayer host = LobbyMenu.Player;
                if (host != null && host.IsHostReady && BtcLobbyKeepReady.SetupChanged)
                {
                    BtcLobbyKeepReady.Log("map/mode/preset changed: host ready cleared, players stay ready");
                    LobbyMenu.SetIsReady(false, null);
                }
                else BtcLobbyKeepReady.Log("lobby event: everyone stays ready");
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "KeepReady: " + e); return true; }
        }
    }

    [HarmonyPatch(typeof(LobbyMenu), "SetIsReady")]
    internal static class BtcLobbyKeepReadySetPatch
    {
        private static void Postfix(bool value, LobbyPlayer player)
        {
            try
            {
                LobbyPlayer p = player ?? LobbyMenu.Player;
                if (p != null && p.isHost) BtcLobbyKeepReady.HostReady(value);
            }
            catch (Exception e) { BtcLog.Error("Hook", "KeepReady.Set: " + e); }
        }
    }
}
