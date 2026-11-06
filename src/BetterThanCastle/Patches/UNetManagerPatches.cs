using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Engine;
using Brix.Network;
using HarmonyLib;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BetterThanCastle.Patches
{
    // Network core hooks for vanilla Brix.Network.UNetManager (legacy: 35 hunks inside the class).
    // All logic/state lives in Net/BtcNetManager.cs. OFF (BtcNetworkGate.Enabled == false) => every hook falls through to vanilla.

    /// <summary>Non-virtual calls to the NetworkLobbyManager base implementations (what `base.X(conn)` did in the legacy code).</summary>
    internal static class UNetBase
    {
        private static Action<NetworkLobbyManager, NetworkConnection> _onClientDisconnect;
        private static Action<NetworkLobbyManager, NetworkConnection> _onServerDisconnect;
        private static Action<NetworkLobbyManager, NetworkConnection> _onClientSceneChanged;

        private static Action<NetworkLobbyManager, NetworkConnection> Make(string name)
        {
            MethodInfo mi = AccessTools.Method(typeof(NetworkLobbyManager), name, new[] { typeof(NetworkConnection) });
            return AccessTools.MethodDelegate<Action<NetworkLobbyManager, NetworkConnection>>(mi, null, false);
        }

        internal static void OnClientDisconnect(UNetManager m, NetworkConnection c)
        {
            if (_onClientDisconnect == null) _onClientDisconnect = Make("OnClientDisconnect");
            _onClientDisconnect(m, c);
        }

        internal static void OnServerDisconnect(UNetManager m, NetworkConnection c)
        {
            if (_onServerDisconnect == null) _onServerDisconnect = Make("OnServerDisconnect");
            _onServerDisconnect(m, c);
        }

        internal static void OnClientSceneChanged(UNetManager m, NetworkConnection c)
        {
            if (_onClientSceneChanged == null) _onClientSceneChanged = Make("OnClientSceneChanged");
            _onClientSceneChanged(m, c);
        }
    }

    /// <summary>Initialize: transport config (DisconnectTimeout), m_MaxBufferedPackets 511, name caches. Legacy hunk 3.</summary>
    [HarmonyPatch(typeof(UNetManager), "Initialize")]
    internal static class UNetManagerInitializePatch
    {
        private static void Prefix(UNetManager __instance)
        {
            try { BtcNetManager.OnInitialize(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.Initialize: " + e); }
        }
    }

    /// <summary>ManuallyInitTransport: BC transport config after vanilla Acks64/2048/channels. Legacy hunks 4-5.</summary>
    [HarmonyPatch(typeof(UNetManager), "ManuallyInitTransport")]
    internal static class UNetManagerManuallyInitTransportPatch
    {
        private static void Postfix(UNetManager __instance)
        {
            try { BtcNetManager.OnTransportInit(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.ManuallyInitTransport: " + e); }
        }
    }

    /// <summary>Start: overlay component, LoadingGame signal, diag log hook. Legacy hunk 5 (Start part).</summary>
    [HarmonyPatch(typeof(UNetManager), "Start")]
    internal static class UNetManagerStartPatch
    {
        private static void Postfix(UNetManager __instance)
        {
            try { BtcNetManager.OnStart(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.Start: " + e); }
        }
    }

    /// <summary>Update: heartbeat / silence monitor / pings / loading timeout. Legacy hunk 6.</summary>
    [HarmonyPatch(typeof(UNetManager), "Update")]
    internal static class UNetManagerUpdatePatch
    {
        private static bool _failed;

        private static void Postfix(UNetManager __instance)
        {
            if (_failed) return;
            try { BtcNetManager.Tick(__instance); }
            catch (Exception e) { _failed = true; BtcLog.Error("Hook", "UNetManager.Update (tick disabled): " + e); }
        }
    }

    /// <summary>LocalPlayerName setter: 30-char cap -> BtcNetManager.NickLimit() (999 with BC ON). Legacy hunk 7.</summary>
    [HarmonyPatch(typeof(UNetManager), "LocalPlayerName", MethodType.Setter)]
    internal static class UNetManagerLocalPlayerNamePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            return NickLimitIl.Replace30(ins, "UNetManager.LocalPlayerName.set");
        }
    }

    /// <summary>Shared transpiler body: every `ldc.i4 30` -> call BtcNetManager.NickLimit().</summary>
    internal static class NickLimitIl
    {
        internal static IEnumerable<CodeInstruction> Replace30(IEnumerable<CodeInstruction> ins, string where)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(ins);
            MethodInfo limit = AccessTools.Method(typeof(BtcNetManager), "NickLimit");
            int n = 0;
            foreach (CodeInstruction ci in list)
            {
                if (ci.LoadsConstant(30L))
                {
                    ci.opcode = OpCodes.Call;
                    ci.operand = limit;
                    n++;
                }
            }
            if (n != 2) BtcLog.Error("Hook", where + ": expected 2 nick-limit constants (30), replaced " + n);
            return list;
        }
    }

    /// <summary>Disconnect: stop the reconnect loop, clear pause/heartbeat state (then vanilla runs). Legacy hunk 17.</summary>
    [HarmonyPatch(typeof(UNetManager), "Disconnect")]
    internal static class UNetManagerDisconnectPatch
    {
        private static bool Prefix(UNetManager __instance)
        {
            if (BtcReplayAuto.DeferDisconnect(__instance)) return false;
            try { BtcNetManager.OnDisconnect(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.Disconnect: " + e); }
            return true;
        }
    }

    /// <summary>ResetToDefault: clear cached player names. Legacy hunk 19.</summary>
    [HarmonyPatch(typeof(UNetManager), "ResetToDefault")]
    internal static class UNetManagerResetToDefaultPatch
    {
        private static void Postfix(UNetManager __instance)
        {
            try { BtcNetManager.ResetCaches(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.ResetToDefault: " + e); }
        }
    }

    /// <summary>Lobby_AssignedFaction: spectator slot excluded (BtcLobby). Legacy hunk 24.</summary>
    [HarmonyPatch(typeof(UNetManager), "Lobby_AssignedFaction")]
    internal static class UNetManagerLobbyAssignedFactionPatch
    {
        private static bool Prefix(UNetManager __instance, ref List<int> __result)
        {
            if (!BtcNetworkGate.Enabled) return true;
            try
            {
                __result = BtcLobby.AssignedFactions(__instance.LobbyPlayerList);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.Lobby_AssignedFaction: " + e); return true; }
        }
    }

    /// <summary>Lobby_AssignedTeam: spectator slot excluded (BtcLobby). Legacy hunk 24.</summary>
    [HarmonyPatch(typeof(UNetManager), "Lobby_AssignedTeam")]
    internal static class UNetManagerLobbyAssignedTeamPatch
    {
        private static bool Prefix(UNetManager __instance, ref List<int> __result)
        {
            if (!BtcNetworkGate.Enabled) return true;
            try
            {
                __result = BtcLobby.AssignedTeams(__instance.LobbyPlayerList);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.Lobby_AssignedTeam: " + e); return true; }
        }
    }

    /// <summary>OnLobbyStopHost: reset BC session state. Legacy hunk 26.</summary>
    [HarmonyPatch(typeof(UNetManager), "OnLobbyStopHost")]
    internal static class UNetManagerOnLobbyStopHostPatch
    {
        private static void Postfix(UNetManager __instance)
        {
            try { BtcNetManager.OnLobbyStopHost(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnLobbyStopHost: " + e); }
        }
    }

    /// <summary>
    /// OnClientDisconnect (BC ON, not a user Disconnect()): base call, then hard pause + reconnect loop instead of
    /// SetNetworkError -> main menu. Legacy hunk 26 (second half).
    /// </summary>
    [HarmonyPatch(typeof(UNetManager), "OnClientDisconnect")]
    internal static class UNetManagerOnClientDisconnectPatch
    {
        private static bool Prefix(UNetManager __instance, NetworkConnection conn)
        {
            if (!BtcNetworkGate.Enabled) return true;
            try
            {
                BtcNetManager.BcFileLog("OnClientDisconnect lastError=" + conn.lastError + ", calledDisconnect=" + __instance.calledDisconnect + ", BetterConnection ON");
                if (__instance.calledDisconnect) return true;   // vanilla: SetNetworkError(lastError)
                if (!BtcNetManager.CanReconnect(__instance)) return true;   // failed first connect / host: vanilla error path
            }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnClientDisconnect: " + e); return true; }
            try { UNetBase.OnClientDisconnect(__instance, conn); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnClientDisconnect base: " + e); }
            try { BtcNetManager.OnClientHardDisconnect(__instance, conn); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnClientDisconnect reconnect: " + e); }
            return false;
        }
    }

    /// <summary>OnClientConnect: register BC handlers, raise channel buffers, release hard pause. Legacy (OnClientConnect).</summary>
    [HarmonyPatch(typeof(UNetManager), "OnClientConnect")]
    internal static class UNetManagerOnClientConnectPatch
    {
        private static void Postfix(UNetManager __instance, NetworkConnection conn)
        {
            if (!BtcNetworkGate.Enabled) return;
            try { BtcNetManager.OnClientConnect(__instance, conn); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnClientConnect: " + e); }
        }
    }

    /// <summary>OnServerDisconnect (BC ON): keep the dropped player's User alive and hard-pause until it reconnects.</summary>
    [HarmonyPatch(typeof(UNetManager), "OnServerDisconnect")]
    internal static class UNetManagerOnServerDisconnectPatch
    {
        private static bool Prefix(UNetManager __instance, NetworkConnection conn)
        {
            if (!BtcNetworkGate.Enabled) return true;
            try { UNetBase.OnServerDisconnect(__instance, conn); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnServerDisconnect base: " + e); }
            bool kept = false;
            try { kept = BtcNetManager.TryKeepDroppedPlayer(__instance, conn); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnServerDisconnect: " + e); }
            if (!kept) __instance.SafeDisconnectConnection(conn);
            return false;
        }
    }

    /// <summary>
    /// OnServerConnect: BtcSpectate.PreConnect (mid-game spectator join skips the lobby base call), PostConnect,
    /// then BC heartbeat seed / channel buffers / re-attach a returning player.
    /// </summary>
    [HarmonyPatch(typeof(UNetManager), "OnServerConnect")]
    internal static class UNetManagerOnServerConnectPatch
    {
        private static bool Prefix(out BtcSpectate.ConnState __state)
        {
            __state = null;
            if (!BtcNetworkGate.Enabled) return true;
            try { __state = BtcSpectate.PreConnect(); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnServerConnect pre: " + e); }
            return __state == null;
        }

        private static void Postfix(UNetManager __instance, NetworkConnection conn, BtcSpectate.ConnState __state)
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                BtcSpectate.PostConnect(__state);
                BtcNetManager.OnServerConnect(__instance, conn, __state);
            }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnServerConnect: " + e); }
        }
    }

    /// <summary>OnStopClient: reset BC client state. Legacy hunk 27.</summary>
    [HarmonyPatch(typeof(UNetManager), "OnStopClient")]
    internal static class UNetManagerOnStopClientPatch
    {
        private static void Postfix(UNetManager __instance)
        {
            try { BtcNetManager.OnStopClient(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnStopClient: " + e); }
        }
    }

    /// <summary>OnClientSceneChanged: no auto-Disconnect() on the offline scene while a BC reconnect is in progress.</summary>
    [HarmonyPatch(typeof(UNetManager), "OnClientSceneChanged")]
    internal static class UNetManagerOnClientSceneChangedPatch
    {
        private static bool Prefix(UNetManager __instance, NetworkConnection conn)
        {
            try
            {
                if (!BtcNetworkGate.Enabled || SceneManager.GetActiveScene().buildIndex != 1 || !BtcNetManager.ReconnectRecoveryInProgress(__instance)) return true;
                if (!Neo.multiplayer) UNetBase.OnClientSceneChanged(__instance, conn);
                BtcNetManager.DiagLog("OnClientSceneChanged - suppressed auto-Disconnect (buildIndex==1) while reconnect recovery in progress");
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnClientSceneChanged: " + e); return true; }
        }
    }

    /// <summary>OnStartServer: BtcNetId counter raise + BC server handlers (64,70-74 + gameplay ids). Legacy hunk 30.</summary>
    [HarmonyPatch(typeof(UNetManager), "OnStartServer")]
    internal static class UNetManagerOnStartServerPatch
    {
        private static void Postfix(UNetManager __instance)
        {
            if (!BtcNetworkGate.Enabled) return;
            try { BtcNetManager.OnStartServer(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnStartServer: " + e); }
        }
    }

    /// <summary>
    /// OnStartClient is not overridden by UNetManager: patch NetworkLobbyManager.OnStartClient and filter on UNetManager.
    /// BC ON: client handlers (64,70-77 ...) + Disconnect(33) intercept (no offline-scene change on a drop).
    /// </summary>
    [HarmonyPatch(typeof(NetworkLobbyManager), "OnStartClient", new[] { typeof(NetworkClient) })]
    internal static class UNetManagerOnStartClientPatch
    {
        private static void Postfix(NetworkLobbyManager __instance, NetworkClient __0)
        {
            UNetManager m = __instance as UNetManager;
            if (m == null || !BtcNetworkGate.Enabled) return;
            try { BtcNetManager.OnStartClient(m, __0); }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnStartClient: " + e); }
        }
    }

    /// <summary>OnServerReady: BtcGameplay state push + BtcSpectate user creation (BC ON). Legacy hunk 31.</summary>
    [HarmonyPatch(typeof(UNetManager), "OnServerReady")]
    internal static class UNetManagerOnServerReadyPatch
    {
        private static void Postfix(NetworkConnection conn)
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                BtcGameplay.OnClientReady(conn);
                BtcSpectate.OnServerReady(conn);
            }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnServerReady: " + e); }
        }
    }

    /// <summary>OnServerError (BC ON): ignore non-fatal errors, drop only the faulty connection. Legacy hunk 31.</summary>
    [HarmonyPatch(typeof(UNetManager), "OnServerError")]
    internal static class UNetManagerOnServerErrorPatch
    {
        private static bool Prefix(UNetManager __instance, NetworkConnection conn, int errorCode)
        {
            if (!BtcNetworkGate.Enabled) return true;
            try { BtcNetManager.OnServerError(__instance, conn, errorCode); return false; }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnServerError: " + e); return true; }
        }
    }

    /// <summary>OnClientError (BC ON): ignore non-fatal errors, fatal ones -> hard pause + reconnect.</summary>
    [HarmonyPatch(typeof(UNetManager), "OnClientError")]
    internal static class UNetManagerOnClientErrorPatch
    {
        private static bool Prefix(UNetManager __instance, NetworkConnection conn, int errorCode)
        {
            if (!BtcNetworkGate.Enabled) return true;
            try { BtcNetManager.OnClientError(__instance, conn, errorCode); return false; }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.OnClientError: " + e); return true; }
        }
    }

    /// <summary>GetLobbyPlayerNameFromConnection (BC ON): null-safe + spectator names (vanilla NREs on unknown conns). Legacy hunk 32.</summary>
    [HarmonyPatch(typeof(UNetManager), "GetLobbyPlayerNameFromConnection")]
    internal static class UNetManagerGetLobbyPlayerNameFromConnectionPatch
    {
        private static bool Prefix(UNetManager __instance, NetworkConnection connection, ref string __result)
        {
            if (!BtcNetworkGate.Enabled) return true;
            try
            {
                __result = BtcNetManager.LobbyPlayerNameFromConnection(__instance, connection);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "UNetManager.GetLobbyPlayerNameFromConnection: " + e); return true; }
        }
    }
}
