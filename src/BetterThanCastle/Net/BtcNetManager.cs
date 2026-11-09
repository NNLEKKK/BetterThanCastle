using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Brix.Assets;
using Brix.Components;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Game;
using Brix.Input;
using Brix.Lifecycle.Pooling;
using Brix.Network;
using Smooth.Slinq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
    /// <summary>
    /// BetterConnection network core. Owns everything the legacy mod added INSIDE vanilla UNetManager
    /// (fields + methods: heartbeat / silence hard-pause / auto-reconnect / ping / voluntary pause / transport buffers).
    /// Vanilla touch-points are the small hooks in Patches/UNetManagerPatches.cs.
    /// State is per UNetManager instance (Dictionary by instanceID, dropped by BtcNetOverlay.OnDestroy).
    /// Wire rule: nothing here sends, registers or changes anything unless BtcNetworkGate.Enabled is true at call time
    /// AND the session was started with BetterConnection ON (State.bcSession).
    /// Message ids (unchanged from the legacy mod): 64 heartbeat, 70/71 ping req/resp, 72/73/74 voluntary pause req/state/perm.
    /// </summary>
    internal static class BtcNetManager
    {
        internal const short MsgHeartbeat = 64;
        internal const short MsgPingRequest = 70;
        internal const short MsgPingResponse = 71;
        internal const short MsgPauseRequest = 72;
        internal const short MsgPauseState = 73;
        internal const short MsgPausePermission = 74;
        internal const short MsgUnetDisconnect = 33;   // UNET MsgType.Disconnect, intercepted on the client (legacy)

        private const int HostOwnerId = -1;
        private const int NoVoluntaryPauseOwner = -2;
        private const float HeartbeatInterval = 0.5f;
        private const float PingInterval = 10f;
        private const float ConnectionPollInterval = 0.2f;
        private const float ConnectionSilenceThreshold = 2f;
        private const float DegradedGiveUpThreshold = 300f;
        private const float LoadingLevelSafetyTimeout = 6f;
        private const uint BcDisconnectTimeout = 3600000U;
        private const int BcMaxBufferedPackets = 511;
        private const int VanillaNickLimit = 30;
        private const int BcNickLimit = 999;

        // ------------------------------------------------------------------ state
        internal sealed class State
        {
            internal bool attached;
            internal bool bcSession;                 // handlers registered with BC ON for the current session
            internal bool wasConnected;              // client reached OnClientConnect at least once (reconnect only then)
            internal bool connectionDegraded;
            internal float lastConnectionPollTime;
            internal float degradedSinceTime;
            internal bool recoveryDrainScheduled;
            internal float recoveryDrainDeadline;
            internal bool hardPauseActive;
            internal float preHardPauseTimeScale = 1f;
            internal bool heartbeatHandlerRegistered;
            internal float lastHeartbeatSendTime;
            internal readonly Dictionary<int, float> lastHeartbeatUnscaledTimeByConnection = new Dictionary<int, float>();
            internal float lastHeartbeatUnscaledTimeClient;
            internal readonly List<User> pendingReconnectUsers = new List<User>();
            internal Coroutine clientReconnectCoroutine;
            internal float lastPingSendTime;
            internal readonly Dictionary<int, float> pingMsByConnection = new Dictionary<int, float>();
            internal float pingMsToHost = -1f;
            internal bool reconnectRecoveryInProgress;
            internal bool effectivePauseApplied;
            internal bool voluntaryPauseActive;
            internal int voluntaryPauseOwnerConnectionId = NoVoluntaryPauseOwner;
            internal string voluntaryPauseOwnerName;
            internal bool clientsCanVoluntaryPause = true;
            internal readonly Dictionary<int, string> cachedPlayerNameByConnectionId = new Dictionary<int, string>();
            internal string cachedHostName;
            internal bool isLoadingLevel;
            internal float loadingLevelStartTime;
            internal Texture2D connectionOverlayTexture;
            internal GUIStyle connectionOverlayLabelStyle;
            internal Action<GameSignals.loadingContext> loadingHandler;
            internal Application.LogCallback logHandler;
        }

        private static readonly Dictionary<int, State> _states = new Dictionary<int, State>();

        internal static UNetManager Current { get { return NetworkManager.singleton as UNetManager; } }

        internal static State Get(UNetManager m)
        {
            if (m == null) return null;
            int id = m.GetInstanceID();
            State s;
            if (!_states.TryGetValue(id, out s))
            {
                s = new State();
                _states[id] = s;
            }
            return s;
        }

        internal static State Peek(UNetManager m)
        {
            if (m == null) return null;
            State s;
            return _states.TryGetValue(m.GetInstanceID(), out s) ? s : null;
        }

        /// <summary>Called by BtcNetOverlay.OnDestroy.</summary>
        internal static void Forget(int instanceId)
        {
            State s;
            if (!_states.TryGetValue(instanceId, out s)) return;
            // LoadingGame handler stays connected (UNetManager lives for the whole process); it no-ops once m is destroyed.
            try
            {
                if (s.logHandler != null) Application.logMessageReceived -= s.logHandler;
            }
            catch (Exception) { }
            if (s.connectionOverlayTexture != null) UnityEngine.Object.Destroy(s.connectionOverlayTexture);
            _states.Remove(instanceId);
        }

        // ------------------------------------------------------------------ nick limit (parity item a)
        /// <summary>Vanilla caps nicks at 30 chars (UNetManager.LocalPlayerName setter + LobbyPlayer.CmdSetPlayerName). BC ON: 999.</summary>
        internal static int NickLimit()
        {
            return BtcNetworkGate.Enabled ? BcNickLimit : VanillaNickLimit;
        }

        // ------------------------------------------------------------------ lifecycle hooks
        /// <summary>UNetManager.Start postfix (also lazily from Tick if Start ran before the patch).</summary>
        internal static void OnStart(UNetManager m)
        {
            State s = Get(m);
            if (s == null || s.attached) return;
            s.attached = true;
            try
            {
                if (m.GetComponent<BtcNetOverlay>() == null) m.gameObject.AddComponent<BtcNetOverlay>();
                s.loadingHandler = delegate(GameSignals.loadingContext ctx) { OnLoadingGameStarted(m, ctx); };
                GameSignals.Connect<GameSignals.loadingContext>(GameSignals.LoadingGame, s.loadingHandler, null);
                s.logHandler = OnUnityLogMessageForDiag;
                Application.logMessageReceived += s.logHandler;
            }
            catch (Exception e) { BtcLog.Error("Net", "BtcNetManager.OnStart: " + e); }
        }

        /// <summary>UNetManager.Initialize prefix (legacy: BcEnsureLoaded/ApplyBcTransportConfig/ApplyMaxBufferedPackets + name caches).</summary>
        internal static void OnInitialize(UNetManager m)
        {
            State s = Get(m);
            s.cachedPlayerNameByConnectionId.Clear();
            s.cachedHostName = null;
            ApplyTransportConfig(m);
            ApplyMaxBufferedPackets(m);
        }

        /// <summary>UNetManager.ManuallyInitTransport postfix: after vanilla set Acks64/2048 and the channels.</summary>
        internal static void OnTransportInit(UNetManager m)
        {
            ApplyTransportConfig(m);
            if (BtcNetworkGate.Enabled) LogTransportConfig(m);
        }

        /// <summary>Called by BtcBc.BcSetEnabled (legacy BcSetEnabled re-applied the transport immediately).</summary>
        internal static void OnBcToggled()
        {
            UNetManager m = Current;
            if (m == null) return;
            ApplyTransportConfig(m);
            ApplyMaxBufferedPackets(m);
        }

        internal static void ResetCaches(UNetManager m)
        {
            State s = Peek(m);
            if (s == null) return;
            s.cachedPlayerNameByConnectionId.Clear();
            s.cachedHostName = null;
        }

        /// <summary>Common teardown used by Disconnect / OnLobbyStopHost / OnStopClient (legacy copies of the same block).</summary>
        private static void ResetSession(UNetManager m, State s, bool clearPending, bool clearClientHeartbeat)
        {
            s.heartbeatHandlerRegistered = false;
            s.bcSession = false;
            s.lastHeartbeatUnscaledTimeByConnection.Clear();
            if (clearClientHeartbeat) s.lastHeartbeatUnscaledTimeClient = 0f;
            s.connectionDegraded = false;
            s.recoveryDrainScheduled = false;
            s.reconnectRecoveryInProgress = false;
            if (clearPending) s.pendingReconnectUsers.Clear();
            if (s.hardPauseActive) ApplyHardPause(m, s, false);
            s.clientsCanVoluntaryPause = true;
            if (s.voluntaryPauseActive) SetVoluntaryPause(m, s, false, NoVoluntaryPauseOwner, null);
        }

        /// <summary>UNetManager.Disconnect prefix.</summary>
        internal static void OnDisconnect(UNetManager m)
        {
            State s = Peek(m);
            if (s == null) return;
            if (s.clientReconnectCoroutine != null)
            {
                m.StopCoroutine(s.clientReconnectCoroutine);
                s.clientReconnectCoroutine = null;
            }
            s.wasConnected = false;
            ResetSession(m, s, true, true);
        }

        internal static void OnLobbyStopHost(UNetManager m)
        {
            State s = Peek(m);
            if (s != null) ResetSession(m, s, true, false);
        }

        internal static void OnStopClient(UNetManager m)
        {
            State s = Peek(m);
            if (s == null) return;
            // keep bcSession/pending users while a reconnect loop is running: the next StartClient re-registers.
            ResetSession(m, s, false, true);
        }

        /// <summary>UNetManager.OnStartServer postfix (BC ON).</summary>
        internal static void OnStartServer(UNetManager m)
        {
            State s = Get(m);
            BtcNetId.OnServerStart();
            s.lastHeartbeatUnscaledTimeByConnection.Clear();
            s.connectionDegraded = false;
            s.recoveryDrainScheduled = false;
            s.pendingReconnectUsers.Clear();
            RegisterHeartbeatHandler(m);
            s.heartbeatHandlerRegistered = true;
            RegisterPingHandler(m);
            RegisterVoluntaryPauseHandlers(m, null);
            s.bcSession = true;
            s.clientsCanVoluntaryPause = true;
            if (s.voluntaryPauseActive) SetVoluntaryPause(m, s, false, NoVoluntaryPauseOwner, null);
            if (s.hardPauseActive) ApplyHardPause(m, s, false);
            BtcLog.Info("Net", "BetterConnection server session started (handlers 64,70-74 registered)");
        }

        /// <summary>NetworkLobbyManager.OnStartClient postfix for a UNetManager (BC ON).</summary>
        internal static void OnStartClient(UNetManager m, NetworkClient client)
        {
            State s = Get(m);
            RegisterHeartbeatHandler(m);
            s.heartbeatHandlerRegistered = true;
            RegisterPingHandler(m);
            RegisterVoluntaryPauseHandlers(m, client);
            if (client != null)
                client.RegisterHandler(MsgUnetDisconnect, delegate(NetworkMessage netMsg) { m.OnClientDisconnect(netMsg.conn); });
            s.bcSession = true;
        }

        /// <summary>UNetManager.OnClientConnect postfix (BC ON).</summary>
        internal static void OnClientConnect(UNetManager m, NetworkConnection conn)
        {
            State s = Get(m);
            s.lastHeartbeatUnscaledTimeClient = Time.unscaledTime;
            RegisterHeartbeatHandler(m);
            s.heartbeatHandlerRegistered = true;
            RegisterPingHandler(m);
            RegisterVoluntaryPauseHandlers(m, null);
            s.bcSession = true;
            s.wasConnected = true;
            RaiseChannelBuffers(m, conn, "client");
            DiagLog("OnClientConnect - explicit pause release on confirmed connect");
            s.connectionDegraded = false;
            s.recoveryDrainScheduled = false;
            s.reconnectRecoveryInProgress = false;
            if (s.hardPauseActive) ApplyHardPause(m, s, false);
            BtcLog.Info("Net", "BetterConnection client connected (handlers 64,70-77 registered)");
        }

        private static void RaiseChannelBuffers(UNetManager m, NetworkConnection conn, string side)
        {
            try
            {
                int count = m.connectionConfig.Channels.Count;
                for (int i = 0; i < count; i++)
                {
                    bool ok = conn.SetChannelOption(i, ChannelOption.MaxPendingBuffers, BcMaxBufferedPackets);
                    DiagLog(string.Format("SetChannelOption(MaxPendingBuffers=511) on {0} channel {1} -> success={2}", side, i, ok));
                }
            }
            catch (Exception ex)
            {
                BtcLog.Warn("Net", "Failed to raise " + side + "-side channel buffer: " + ex.Message);
            }
        }

        /// <summary>
        /// UNetManager.OnClientDisconnect (BC ON, not a user Disconnect()): keep the game, hard-pause and reconnect
        /// instead of SetNetworkError -> main menu. Base call is done by the hook.
        /// </summary>
        internal static void OnClientHardDisconnect(UNetManager m, NetworkConnection conn)
        {
            State s = Get(m);
            BtcLog.Warn("Net", "Client hard-disconnected (lastError=" + conn.lastError + "). Entering reconnect loop instead of kicking to menu.");
            DiagLog("HARD DISCONNECT, lastError=" + conn.lastError);
            ApplyHardPause(m, s, true);
            StartClientReconnectLoop(m, s);
        }

        /// <summary>UNetManager.OnServerDisconnect (BC ON). Returns true when the player is kept alive for a reconnect.</summary>
        internal static bool TryKeepDroppedPlayer(UNetManager m, NetworkConnection conn)
        {
            State s = Get(m);
            if (!s.bcSession) return false;
            User user = SafeUser(m, conn);
            if (user == null || BtcSpectate.IsSpec(conn)) return false;
            BtcLog.Warn("Net", "Server: connection " + conn.connectionId + " dropped, keeping player " + user.name + " alive and waiting for reconnect.");
            user.NetworkReady = false;
            s.pendingReconnectUsers.Add(user);
            ApplyHardPause(m, s, true);
            return true;
        }

        /// <summary>UNetManager.OnServerConnect postfix part (BC ON). connState = BtcSpectate.PreConnect() result.</summary>
        internal static void OnServerConnect(UNetManager m, NetworkConnection conn, BtcSpectate.ConnState connState)
        {
            State s = Get(m);
            s.lastHeartbeatUnscaledTimeByConnection[conn.connectionId] = Time.unscaledTime;
            RaiseChannelBuffers(m, conn, "server");
            if (s.pendingReconnectUsers.Count > 0 && connState == null)
            {
                User user = s.pendingReconnectUsers[0];
                s.pendingReconnectUsers.RemoveAt(0);
                if (user != null)
                {
                    BtcLog.Info("Net", "Server: re-attaching returning player " + user.name + " to connection " + conn.connectionId + ".");
                    NetworkServer.ReplacePlayerForConnection(conn, user.gameObject, 0);
                    NetworkServer.SetClientReady(conn);
                    user.NetworkReady = true;
                }
                if (s.pendingReconnectUsers.Count == 0 && !s.connectionDegraded) ApplyHardPause(m, s, false);
            }
        }

        /// <summary>Only a client that was really connected (not a failed first connect) enters the reconnect loop.</summary>
        internal static bool CanReconnect(UNetManager m)
        {
            State s = Peek(m);
            return s != null && (s.wasConnected || s.reconnectRecoveryInProgress) && !NetworkServer.active;
        }

        internal static bool ReconnectRecoveryInProgress(UNetManager m)
        {
            State s = Peek(m);
            return s != null && s.reconnectRecoveryInProgress;
        }

        /// <summary>OnServerError / OnClientError (BC ON): non-fatal errors are ignored instead of kicking to the menu.</summary>
        internal static bool IsFatalBrixNetworkError(UNetManager.BrixNetworkError error)
        {
            return error - UNetManager.BrixNetworkError.BadMessage > 1 && error != UNetManager.BrixNetworkError.NoResources && error - UNetManager.BrixNetworkError.WrongChannel > 1;
        }

        internal static void OnServerError(UNetManager m, NetworkConnection conn, int errorCode)
        {
            int num = (conn == null) ? -1 : conn.connectionId;
            string name = Enum.GetName(typeof(NetworkError), errorCode);
            BcFileLog("OnServerError code=" + errorCode + " (" + name + "), connId=" + num + ", BetterConnection ON");
            DiagLog(string.Format("OnServerError code={0} ({1}) connId={2}", errorCode, name, num));
            UNetManager.BrixNetworkError error;
            if (!UNetManager.ToBrixNetworkErrorMap.TryGetValue((NetworkError)errorCode, out error)) error = UNetManager.BrixNetworkError.UsageError;
            if (!IsFatalBrixNetworkError(error))
            {
                BtcLog.Warn("Net", "Ignored non-fatal server error code(" + name + ") for connectionId: " + num);
                return;
            }
            BtcLog.Error("Net", "Server error code(" + name + ") for connectionId: " + num);
            if (conn != null) m.SafeDisconnectConnection(conn);
            else UNetManager.SetNetworkError((NetworkError)errorCode, true);
        }

        internal static void OnClientError(UNetManager m, NetworkConnection conn, int errorCode)
        {
            string name = Enum.GetName(typeof(NetworkError), errorCode);
            BcFileLog("OnClientError code=" + errorCode + " (" + name + "), BetterConnection ON");
            UNetManager.BrixNetworkError error;
            if (!UNetManager.ToBrixNetworkErrorMap.TryGetValue((NetworkError)errorCode, out error)) error = UNetManager.BrixNetworkError.UsageError;
            if (!IsFatalBrixNetworkError(error))
            {
                BtcLog.Warn("Net", "Ignored non-fatal client error code : " + name + "(" + errorCode + ")");
                return;
            }
            BtcLog.Error("Net", "Client error code : " + name + "(" + errorCode + ")");
            if (m.calledDisconnect || !CanReconnect(m))
            {
                UNetManager.SetNetworkError((NetworkError)errorCode, true);
                return;
            }
            BtcLog.Warn("Net", "Fatal client error (" + name + "/" + errorCode + ") while connected - pausing and attempting reconnect instead of kicking to menu.");
            DiagLog(string.Format("FATAL OnClientError code={0} ({1}) - pausing and reconnecting", errorCode, name));
            State s = Get(m);
            ApplyHardPause(m, s, true);
            StartClientReconnectLoop(m, s);
        }

        // ------------------------------------------------------------------ Update
        /// <summary>UNetManager.Update postfix.</summary>
        internal static void Tick(UNetManager m)
        {
            State s = Get(m);
            if (!s.attached) OnStart(m);
            if (!BtcNetworkGate.Enabled)
            {
                s.heartbeatHandlerRegistered = false;
                return;
            }
            if (s.isLoadingLevel && Time.unscaledTime >= s.loadingLevelStartTime + LoadingLevelSafetyTimeout)
            {
                DiagLog("isLoadingLevel SAFETY TIMEOUT - forcing isLoadingLevel=false after " + LoadingLevelSafetyTimeout + "s, SoundEngineReady never arrived for this load");
                s.isLoadingLevel = false;
            }
            if (!UNetManager.IsConnected)
            {
                s.heartbeatHandlerRegistered = false;
                return;
            }
            if (!s.bcSession) return;
            CachePlayerNames(m, s);
            if (Time.unscaledTime >= s.lastHeartbeatSendTime + HeartbeatInterval)
            {
                s.lastHeartbeatSendTime = Time.unscaledTime;
                SendHeartbeat(m);
            }
            if (m._currentSceneName != null && m._currentSceneName != m.lobbyScene && !s.isLoadingLevel)
                MonitorConnectionQuality(m, s);
            if (Time.unscaledTime >= s.lastPingSendTime + PingInterval)
            {
                s.lastPingSendTime = Time.unscaledTime;
                SendPings(m, s);
            }
        }

        private static void MonitorConnectionQuality(UNetManager m, State s)
        {
            if (s.reconnectRecoveryInProgress) return;
            if (Time.unscaledTime < s.lastConnectionPollTime + ConnectionPollInterval) return;
            s.lastConnectionPollTime = Time.unscaledTime;
            if (s.connectionDegraded) LogConnectionBufferStats(m);
            bool healthy = IsConnectionLinkHealthy(m, s);
            if (!healthy && !s.connectionDegraded)
            {
                s.connectionDegraded = true;
                s.degradedSinceTime = Time.unscaledTime;
                s.recoveryDrainScheduled = false;
                BtcLog.Warn("Net", "Connection silence detected, pausing.");
                DiagLog("SILENCE DETECTED - hard pause engaged");
                ApplyHardPause(m, s, true);
            }
            else if (healthy && s.connectionDegraded)
            {
                if (!s.recoveryDrainScheduled)
                {
                    float outage = Time.unscaledTime - s.degradedSinceTime;
                    float drain = Mathf.Clamp(outage * 0.1f, 0f, 5f);
                    s.recoveryDrainDeadline = Time.unscaledTime + drain;
                    s.recoveryDrainScheduled = true;
                    BtcLog.Info("Net", string.Format("Link back, draining backlog for {0:0.0}s before resuming (outage was {1:0.0}s).", drain, outage));
                }
                if (Time.unscaledTime >= s.recoveryDrainDeadline)
                {
                    s.connectionDegraded = false;
                    s.recoveryDrainScheduled = false;
                    BtcLog.Info("Net", "Connection recovered, resuming.");
                    DiagLog("RECOVERED - hard pause lifted");
                    if (s.pendingReconnectUsers.Count == 0) ApplyHardPause(m, s, false);
                }
            }
            if (s.connectionDegraded && Time.unscaledTime - s.degradedSinceTime > DegradedGiveUpThreshold && !NetworkServer.active)
            {
                BtcLog.Warn("Net", "Client link degraded too long (5 min), forcing a clean reconnect as a last resort.");
                DiagLog("GIVE-UP WATCHDOG FIRED - forcing clean reconnect");
                s.connectionDegraded = false;
                s.recoveryDrainScheduled = false;
                StartClientReconnectLoop(m, s);
            }
        }

        internal static bool IsLocalConnection(NetworkConnection c)
        {
            foreach (NetworkConnection lc in NetworkServer.localConnections)
            {
                if (lc != null && lc.connectionId == c.connectionId) return true;
            }
            return false;
        }

        private static bool IsConnectionLinkHealthy(UNetManager m, State s)
        {
            float now = Time.unscaledTime;
            if (NetworkServer.active)
            {
                foreach (NetworkConnection c in NetworkServer.connections)
                {
                    if (c == null || IsLocalConnection(c) || BtcSpectate.IsSpec(c)) continue;
                    float last;
                    float t = s.lastHeartbeatUnscaledTimeByConnection.TryGetValue(c.connectionId, out last) ? last : now;
                    if (now - t > ConnectionSilenceThreshold) return false;
                }
                return true;
            }
            NetworkConnection conn = (m.LocalNetworkClient != null) ? m.LocalNetworkClient.connection : null;
            return conn == null || now - s.lastHeartbeatUnscaledTimeClient <= ConnectionSilenceThreshold;
        }

        // ------------------------------------------------------------------ pause
        private static void ApplyHardPause(UNetManager m, State s, bool pause)
        {
            if (pause && s.isLoadingLevel)
            {
                DiagLog("ApplyHardPause - BLOCKED engage attempt while isLoadingLevel=true");
                return;
            }
            s.hardPauseActive = pause;
            RecomputeEffectivePause(s);
        }

        private static void SetVoluntaryPause(UNetManager m, State s, bool pause, int ownerConnectionId, string ownerName)
        {
            s.voluntaryPauseActive = pause;
            s.voluntaryPauseOwnerConnectionId = pause ? ownerConnectionId : NoVoluntaryPauseOwner;
            s.voluntaryPauseOwnerName = pause ? ownerName : null;
            RecomputeEffectivePause(s);
        }

        private static void RecomputeEffectivePause(State s)
        {
            bool effective = s.hardPauseActive || s.voluntaryPauseActive;
            if (effective == s.effectivePauseApplied) return;
            DiagLog("RecomputeEffectivePause - transition to effective=" + effective + " (hardPauseActive=" + s.hardPauseActive + ", voluntaryPauseActive=" + s.voluntaryPauseActive + ")");
            s.effectivePauseApplied = effective;
            if (effective)
            {
                s.preHardPauseTimeScale = Time.timeScale;
                if (s.preHardPauseTimeScale <= 0f) s.preHardPauseTimeScale = 1f;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = s.preHardPauseTimeScale;
            }
            if (NetworkServer.active)
            {
                Journee.CurrentPaused = effective;
            }
            else if (Journee.instance != null)
            {
                Journee.instance.IsPaused = effective;
            }
            BtcLog.Info("Net", "pause " + (effective ? "ON" : "OFF") + " (hard=" + s.hardPauseActive + ", voluntary=" + s.voluntaryPauseActive + ")");
        }

        internal static void RequestVoluntaryPauseToggle(UNetManager m)
        {
            State s = Get(m);
            DiagLog("RequestVoluntaryPauseToggle - requested. isLoadingLevel=" + s.isLoadingLevel + ", NetworkServer.active=" + NetworkServer.active
                + ", LocalNetworkClient=" + ((m.LocalNetworkClient != null) ? "set" : "null")
                + ", isConnected=" + (m.LocalNetworkClient != null && m.LocalNetworkClient.isConnected));
            if (s.isLoadingLevel || !s.bcSession) return;
            if (NetworkServer.active)
            {
                ServerHandleVoluntaryPauseToggle(m, s, HostOwnerId, m.LocalPlayerName, true);
            }
            else if (m.LocalNetworkClient != null && m.LocalNetworkClient.isConnected)
            {
                m.LocalNetworkClient.Send(MsgPauseRequest, new VoluntaryPauseRequestMessage());
            }
            else
            {
                DiagLog("RequestVoluntaryPauseToggle - client request DROPPED, LocalNetworkClient not usable.");
            }
        }

        internal static void RequestToggleClientPausePermission(UNetManager m)
        {
            State s = Get(m);
            if (!NetworkServer.active || !s.bcSession) return;
            s.clientsCanVoluntaryPause = !s.clientsCanVoluntaryPause;
            BtcLog.Info("Net", "Clients allowed to request voluntary pause: " + s.clientsCanVoluntaryPause + ".");
            BroadcastVoluntaryPausePermission(s);
        }

        private static void OnVoluntaryPauseRequestReceived(UNetManager m, NetworkMessage message)
        {
            if (!NetworkServer.active || message.conn == null) return;
            string requesterName = ResolvePlayerName(m, Get(m), message.conn);
            ServerHandleVoluntaryPauseToggle(m, Get(m), message.conn.connectionId, requesterName, false);
        }

        private static void ServerHandleVoluntaryPauseToggle(UNetManager m, State s, int requesterConnectionId, string requesterName, bool requesterIsHost)
        {
            if (s.isLoadingLevel)
            {
                DiagLog("ServerHandleVoluntaryPauseToggle - rejected, level is loading. requester=" + requesterName + " (conn " + requesterConnectionId + ", isHost=" + requesterIsHost + ")");
                return;
            }
            if (s.voluntaryPauseActive)
            {
                bool canRelease = s.voluntaryPauseOwnerConnectionId == requesterConnectionId || requesterIsHost;
                DiagLog("ServerHandleVoluntaryPauseToggle - release attempt by " + requesterName + " (conn " + requesterConnectionId + ", isHost=" + requesterIsHost + "), owner=" + s.voluntaryPauseOwnerConnectionId + ", canRelease=" + canRelease);
                if (canRelease)
                {
                    SetVoluntaryPause(m, s, false, NoVoluntaryPauseOwner, null);
                    BroadcastVoluntaryPauseState(s);
                }
            }
            else
            {
                bool allowed = requesterIsHost || s.clientsCanVoluntaryPause;
                DiagLog("ServerHandleVoluntaryPauseToggle - engage attempt by " + requesterName + " (conn " + requesterConnectionId + ", isHost=" + requesterIsHost + "), clientsCanVoluntaryPause=" + s.clientsCanVoluntaryPause + ", allowed=" + allowed);
                if (allowed)
                {
                    SetVoluntaryPause(m, s, true, requesterConnectionId, requesterName);
                    BroadcastVoluntaryPauseState(s);
                }
            }
        }

        private static void BroadcastVoluntaryPauseState(State s)
        {
            if (!NetworkServer.active || !s.bcSession) return;
            VoluntaryPauseStateMessage msg = new VoluntaryPauseStateMessage
            {
                paused = s.voluntaryPauseActive,
                ownerConnectionId = s.voluntaryPauseOwnerConnectionId,
                ownerName = s.voluntaryPauseOwnerName ?? string.Empty
            };
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                NetworkConnection c = NetworkServer.connections[i];
                if (c != null) c.Send(MsgPauseState, msg);
            }
        }

        private static void BroadcastVoluntaryPausePermission(State s)
        {
            if (!NetworkServer.active || !s.bcSession) return;
            VoluntaryPausePermissionMessage msg = new VoluntaryPausePermissionMessage { clientsCanPause = s.clientsCanVoluntaryPause };
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                NetworkConnection c = NetworkServer.connections[i];
                if (c != null) c.Send(MsgPausePermission, msg);
            }
        }

        private static void OnVoluntaryPauseStateReceived(UNetManager m, NetworkMessage message)
        {
            if (NetworkServer.active) return;
            VoluntaryPauseStateMessage msg = message.ReadMessage<VoluntaryPauseStateMessage>();
            DiagLog("OnVoluntaryPauseStateReceived - paused=" + msg.paused + ", owner=" + msg.ownerConnectionId + " (" + msg.ownerName + ")");
            SetVoluntaryPause(m, Get(m), msg.paused, msg.ownerConnectionId, string.IsNullOrEmpty(msg.ownerName) ? null : msg.ownerName);
        }

        private static void OnVoluntaryPausePermissionReceived(UNetManager m, NetworkMessage message)
        {
            if (NetworkServer.active) return;
            VoluntaryPausePermissionMessage msg = message.ReadMessage<VoluntaryPausePermissionMessage>();
            Get(m).clientsCanVoluntaryPause = msg.clientsCanPause;
        }

        private static void OnLoadingGameStarted(UNetManager m, GameSignals.loadingContext context)
        {
            try
            {
                if (!BtcNetworkGate.Enabled || m == null || Peek(m) == null) return;
                State s = Get(m);
                s.isLoadingLevel = true;
                s.loadingLevelStartTime = Time.unscaledTime;
                if (s.voluntaryPauseActive)
                {
                    SetVoluntaryPause(m, s, false, NoVoluntaryPauseOwner, null);
                    if (NetworkServer.active) BroadcastVoluntaryPauseState(s);
                }
                if (s.hardPauseActive) ApplyHardPause(m, s, false);
                GameSignals.Once(GameSignals.SoundEngineReady, delegate { s.isLoadingLevel = false; }, null);
            }
            catch (Exception e) { BtcLog.Error("Net", "OnLoadingGameStarted: " + e); }
        }

        // ------------------------------------------------------------------ handlers
        private static void RegisterServerHandler(short id, NetworkMessageDelegate h)
        {
            if (NetworkServer.handlers.ContainsKey(id)) NetworkServer.UnregisterHandler(id);
            NetworkServer.RegisterHandler(id, h);
        }

        private static void RegisterClientHandler(NetworkClient c, short id, NetworkMessageDelegate h)
        {
            if (c.handlers.ContainsKey(id)) c.UnregisterHandler(id);
            c.RegisterHandler(id, h);
        }

        private static void RegisterHeartbeatHandler(UNetManager m)
        {
            NetworkMessageDelegate h = delegate(NetworkMessage msg) { OnHeartbeatReceived(m, msg); };
            if (NetworkServer.active) RegisterServerHandler(MsgHeartbeat, h);
            else if (m.LocalNetworkClient != null) RegisterClientHandler(m.LocalNetworkClient, MsgHeartbeat, h);
        }

        private static void OnHeartbeatReceived(UNetManager m, NetworkMessage message)
        {
            State s = Get(m);
            float now = Time.unscaledTime;
            if (NetworkServer.active)
            {
                if (message.conn != null) s.lastHeartbeatUnscaledTimeByConnection[message.conn.connectionId] = now;
            }
            else
            {
                s.lastHeartbeatUnscaledTimeClient = now;
            }
        }

        private static void SendHeartbeat(UNetManager m)
        {
            HeartbeatMessage msg = new HeartbeatMessage();
            if (NetworkServer.active)
            {
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    // legacy also sent to the host's own local connection (no handler there -> "Unknown message ID 64"); skip it.
                    if (c != null && !IsLocalConnection(c)) c.SendUnreliable(MsgHeartbeat, msg);
                }
            }
            else if (m.LocalNetworkClient != null && m.LocalNetworkClient.isConnected)
            {
                m.LocalNetworkClient.SendUnreliable(MsgHeartbeat, msg);
            }
        }

        private static void RegisterPingHandler(UNetManager m)
        {
            NetworkMessageDelegate req = delegate(NetworkMessage msg) { OnPingRequestReceived(msg); };
            NetworkMessageDelegate resp = delegate(NetworkMessage msg) { OnPingResponseReceived(m, msg); };
            if (NetworkServer.active)
            {
                RegisterServerHandler(MsgPingRequest, req);
                RegisterServerHandler(MsgPingResponse, resp);
            }
            else if (m.LocalNetworkClient != null)
            {
                RegisterClientHandler(m.LocalNetworkClient, MsgPingRequest, req);
                RegisterClientHandler(m.LocalNetworkClient, MsgPingResponse, resp);
            }
        }

        private static void OnPingRequestReceived(NetworkMessage message)
        {
            PingMessage req = message.ReadMessage<PingMessage>();
            if (message.conn != null) message.conn.SendUnreliable(MsgPingResponse, new PingMessage { sentTime = req.sentTime });
        }

        private static void OnPingResponseReceived(UNetManager m, NetworkMessage message)
        {
            PingMessage msg = message.ReadMessage<PingMessage>();
            float value = Mathf.Max(0f, (Time.unscaledTime - msg.sentTime) * 1000f) / 2f;
            State s = Get(m);
            if (NetworkServer.active)
            {
                if (message.conn != null) s.pingMsByConnection[message.conn.connectionId] = value;
            }
            else
            {
                s.pingMsToHost = value;
            }
        }

        private static void SendPings(UNetManager m, State s)
        {
            PingMessage msg = new PingMessage { sentTime = Time.unscaledTime };
            if (NetworkServer.active)
            {
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    if (c != null && !IsLocalConnection(c)) c.SendUnreliable(MsgPingRequest, msg);
                }
            }
            else if (m.LocalNetworkClient != null && m.LocalNetworkClient.isConnected)
            {
                m.LocalNetworkClient.SendUnreliable(MsgPingRequest, msg);
            }
        }

        private static void RegisterVoluntaryPauseHandlers(UNetManager m, NetworkClient clientOverride)
        {
            if (NetworkServer.active)
                RegisterServerHandler(MsgPauseRequest, delegate(NetworkMessage msg) { OnVoluntaryPauseRequestReceived(m, msg); });
            NetworkClient c = clientOverride ?? m.LocalNetworkClient;
            if (c != null)
            {
                RegisterClientHandler(c, MsgPauseState, delegate(NetworkMessage msg) { OnVoluntaryPauseStateReceived(m, msg); });
                RegisterClientHandler(c, MsgPausePermission, delegate(NetworkMessage msg) { OnVoluntaryPausePermissionReceived(m, msg); });
            }
            // gameplay/spectate/recipes/symmetry/mapscale/handicap handlers (75-77 + their own ids)
            BtcGameplay.RegisterHandlers(clientOverride);
        }

        // ------------------------------------------------------------------ reconnect
        private static void StartClientReconnectLoop(UNetManager m, State s)
        {
            s.reconnectRecoveryInProgress = true;
            if (s.clientReconnectCoroutine == null) s.clientReconnectCoroutine = m.StartCoroutine(ClientReconnectLoop(m, s));
        }

        private static IEnumerator ClientReconnectLoop(UNetManager m, State s)
        {
            string address = m.networkAddress;
            int port = m.networkPort;
            BtcLog.Info("Net", "Client reconnect loop started, target " + address + ":" + port + ".");
            DiagLog(string.Format("RECONNECT LOOP START target={0}:{1}", address, port));
            while (!m.calledDisconnect && !NetworkClient.active)
            {
                yield return new WaitForSecondsRealtime(3f);
                if (m.calledDisconnect) break;
                BtcLog.Info("Net", "Client reconnect attempt to " + address + ":" + port + ".");
                DiagLog(string.Format("RECONNECT ATTEMPT target={0}:{1}", address, port));
                AttemptClientReconnect(m, address, port);
                yield return new WaitForSecondsRealtime(2f);
            }
            BtcLog.Info("Net", NetworkClient.active ? "Client reconnect succeeded." : "Client reconnect loop stopped (manual disconnect).");
            s.clientReconnectCoroutine = null;
        }

        private static void AttemptClientReconnect(UNetManager m, string address, int port)
        {
            try
            {
                if (m.LocalNetworkClient != null)
                {
                    DiagLog("AttemptClientReconnect - tearing down old NetworkClient before reconnecting");
                    try { m.LocalNetworkClient.Disconnect(); }
                    catch (Exception ex) { DiagLog("AttemptClientReconnect - old client Disconnect() threw: " + ex.Message); }
                    try { m.LocalNetworkClient.Shutdown(); }
                    catch (Exception ex2) { DiagLog("AttemptClientReconnect - old client Shutdown() threw: " + ex2.Message); }
                    m.client = null;
                    m.LocalNetworkClient = null;
                }
                SetMaxBufferedPackets(m, BcMaxBufferedPackets);
                m.ManuallyInitTransport();
                m.networkAddress = address;
                m.networkPort = port;
                Neo.multiplayer = true;
                m.LocalNetworkClient = m.StartClient();
                DiagLog("AttemptClientReconnect - fresh NetworkClient created, Connect() issued");
            }
            catch (Exception ex3)
            {
                BtcLog.Warn("Net", "Client reconnect attempt threw: " + ex3.Message);
                DiagLog("AttemptClientReconnect - threw: " + ex3.Message);
            }
        }

        // ------------------------------------------------------------------ transport
        private static bool _disconnectTimeoutCaptured;
        private static uint _origDisconnectTimeout;
        private static FieldInfo _maxBufferedPacketsField;
        private static bool _origBufCaptured;
        private static int _origBuf = 16;

        /// <summary>
        /// Legacy ApplyBcTransportConfig. Vanilla already sets AcksType=Acks64 / MaxSentMessageQueueSize=2048 in
        /// ManuallyInitTransport; BC ON additionally sets DisconnectTimeout to 1h (the heartbeat replaces the UNET timeout).
        /// OFF restores the captured vanilla value (only if we ever changed it).
        /// </summary>
        private static void ApplyTransportConfig(UNetManager m)
        {
            try
            {
                ConnectionConfig cfg = m.connectionConfig;
                if (cfg == null) return;
                if (BtcNetworkGate.Enabled)
                {
                    if (!_disconnectTimeoutCaptured)
                    {
                        _origDisconnectTimeout = cfg.DisconnectTimeout;
                        _disconnectTimeoutCaptured = true;
                    }
                    cfg.AcksType = ConnectionAcksType.Acks64;
                    cfg.MaxSentMessageQueueSize = 2048;
                    cfg.DisconnectTimeout = BcDisconnectTimeout;
                }
                else if (_disconnectTimeoutCaptured && cfg.DisconnectTimeout == BcDisconnectTimeout)
                {
                    cfg.DisconnectTimeout = _origDisconnectTimeout;
                }
            }
            catch (Exception e) { BtcLog.Error("Net", "ApplyTransportConfig: " + e.Message); }
        }

        private static FieldInfo MaxBufferedField()
        {
            if (_maxBufferedPacketsField == null)
                _maxBufferedPacketsField = typeof(NetworkManager).GetField("m_MaxBufferedPackets", BindingFlags.Instance | BindingFlags.NonPublic);
            return _maxBufferedPacketsField;
        }

        /// <summary>Legacy ApplyMaxBufferedPackets: 511 when BC ON, the captured engine value when OFF.</summary>
        private static void ApplyMaxBufferedPackets(UNetManager m)
        {
            try
            {
                FieldInfo f = MaxBufferedField();
                if (f == null)
                {
                    BtcLog.Warn("Net", "NetworkManager.m_MaxBufferedPackets not found (Unity version mismatch?) - staying at engine default.");
                    return;
                }
                if (!_origBufCaptured)
                {
                    if (!BtcNetworkGate.Enabled) return;   // never touched: stay vanilla
                    int cur = (int)f.GetValue(m);
                    _origBuf = (cur == BcMaxBufferedPackets) ? 16 : cur;
                    _origBufCaptured = true;
                }
                int v = BtcNetworkGate.Enabled ? BcMaxBufferedPackets : _origBuf;
                f.SetValue(m, v);
                BtcLog.Info("Net", "NetworkManager.m_MaxBufferedPackets set to " + v + ".");
            }
            catch (Exception ex) { BtcLog.Warn("Net", "Failed to set m_MaxBufferedPackets: " + ex.Message); }
        }

        private static void SetMaxBufferedPackets(UNetManager m, int value)
        {
            try
            {
                FieldInfo f = MaxBufferedField();
                if (f == null) return;
                if (!_origBufCaptured)
                {
                    int cur = (int)f.GetValue(m);
                    _origBuf = (cur == BcMaxBufferedPackets) ? 16 : cur;
                    _origBufCaptured = true;
                }
                f.SetValue(m, Mathf.Clamp(value, 0, BcMaxBufferedPackets));
            }
            catch (Exception ex) { BtcLog.Warn("Net", "Failed to raise m_MaxBufferedPackets: " + ex.Message); }
        }

        private static void LogTransportConfig(UNetManager m)
        {
            try
            {
                ConnectionConfig cfg = m.connectionConfig;
                List<string> ch = new List<string>();
                for (int j = 0; j < cfg.ChannelCount; j++) ch.Add(cfg.GetChannel((byte)j).ToString());
                FieldInfo f = MaxBufferedField();
                string msg = "transport config (BetterConnection ON): AcksType=" + cfg.AcksType + ", MaxSentMessageQueueSize=" + cfg.MaxSentMessageQueueSize
                    + ", DisconnectTimeout=" + cfg.DisconnectTimeout + ", MaxBufferedPackets=" + (f != null ? f.GetValue(m) : "?")
                    + " | channels: " + string.Join(", ", ch.ToArray());
                BtcLog.Info("Net", msg);
                BcFileLog(msg);
            }
            catch (Exception ex) { BtcLog.Warn("Net", "could not log transport config: " + ex.Message); }
        }

        // ------------------------------------------------------------------ names / ping rows
        private static User SafeUser(UNetManager m, NetworkConnection conn)
        {
            try
            {
                if (m.debugLoader) return User.LocalUser;
                return m.UserList.Find(x => x != null && x.connectionToClient != null && x.connectionToClient.connectionId == conn.connectionId);
            }
            catch (Exception) { return null; }
        }

        /// <summary>BC ON replacement of UNetManager.GetLobbyPlayerNameFromConnection (null-safe + spectator names).</summary>
        internal static string LobbyPlayerNameFromConnection(UNetManager m, NetworkConnection connection)
        {
            if (m.LobbyPlayerList == null || m.LobbyPlayerList.Count == 0) return "Player";
            LobbyPlayer lp = m.LobbyPlayerList.Find(x => x != null && x.connectionToClient != null && connection != null && x.connectionToClient.connectionId == connection.connectionId);
            return (lp != null) ? lp.playerName : BtcSpectate.NameOf(connection);
        }

        private static bool IsConnectionAwaitingRecovery(UNetManager m, State s, NetworkConnection connection)
        {
            User u = SafeUser(m, connection);
            return u != null && s.pendingReconnectUsers.Contains(u);
        }

        private static string ResolvePlayerName(UNetManager m, State s, NetworkConnection connection)
        {
            string text;
            if (s.cachedPlayerNameByConnectionId.TryGetValue(connection.connectionId, out text) && !string.IsNullOrEmpty(text)) return text;
            string n = null;
            try { n = m.GetLobbyPlayerNameFromConnection(connection); }
            catch (Exception) { }
            if (!string.IsNullOrEmpty(n) && n != "Player") return n;
            return "Player " + connection.connectionId;
        }

        private static void CachePlayerNames(UNetManager m, State s)
        {
            if (m.LobbyPlayerList == null) return;
            for (int i = 0; i < m.LobbyPlayerList.Count; i++)
            {
                LobbyPlayer lp = m.LobbyPlayerList[i];
                if (lp == null || string.IsNullOrEmpty(lp.playerName)) continue;
                if (NetworkServer.active && lp.connectionToClient != null)
                    s.cachedPlayerNameByConnectionId[lp.connectionToClient.connectionId] = lp.playerName;
                if (lp.isHost) s.cachedHostName = lp.playerName;
            }
        }

        private struct PingRowInfo
        {
            public string Label;
            public bool ShowVoluntaryPauseBadge;
            public bool ShowAwaitingRecoveryBadge;
        }

        private static List<PingRowInfo> BuildPingRows(UNetManager m, State s)
        {
            List<PingRowInfo> list = new List<PingRowInfo>();
            if (NetworkServer.active)
            {
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    if (c == null || IsLocalConnection(c)) continue;
                    float ms;
                    string ping = s.pingMsByConnection.TryGetValue(c.connectionId, out ms) ? string.Format("{0:0}ms", ms) : "...";
                    bool owner = s.voluntaryPauseActive && s.voluntaryPauseOwnerConnectionId == c.connectionId;
                    list.Add(new PingRowInfo
                    {
                        Label = string.Format("{0}: {1}", ResolvePlayerName(m, s, c), ping),
                        ShowVoluntaryPauseBadge = owner,
                        ShowAwaitingRecoveryBadge = !owner && IsConnectionAwaitingRecovery(m, s, c)
                    });
                }
            }
            else
            {
                string ping = (s.pingMsToHost >= 0f) ? string.Format("{0:0}ms", s.pingMsToHost) : "...";
                string host;
                if (!string.IsNullOrEmpty(s.cachedHostName)) host = s.cachedHostName;
                else
                {
                    LobbyPlayer lp = (m.LobbyPlayerList != null) ? m.LobbyPlayerList.Find(p => p != null && p.isHost) : null;
                    host = (lp != null) ? lp.playerName : "Host";
                }
                list.Add(new PingRowInfo
                {
                    Label = host + ": " + ping,
                    ShowVoluntaryPauseBadge = s.voluntaryPauseActive && s.voluntaryPauseOwnerConnectionId == HostOwnerId,
                    ShowAwaitingRecoveryBadge = false
                });
            }
            return list;
        }

        // ------------------------------------------------------------------ public surface for BtcBc (legacy UNetManager.Bc*)
        internal static bool InSession
        {
            get
            {
                UNetManager m = Current;
                return BtcNetworkGate.Enabled && m != null && (NetworkServer.active || (m.LocalNetworkClient != null && m.LocalNetworkClient.isConnected));
            }
        }

        internal static bool VoluntaryPauseActive
        {
            get
            {
                State s = Peek(Current);
                return BtcNetworkGate.Enabled && s != null && s.voluntaryPauseActive;
            }
        }

        internal static string VoluntaryPauseOwner
        {
            get
            {
                State s = Peek(Current);
                return (s != null) ? s.voluntaryPauseOwnerName : null;
            }
        }

        internal static bool ClientsCanPause
        {
            get
            {
                State s = Peek(Current);
                return s == null || s.clientsCanVoluntaryPause;
            }
        }

        internal static bool IsLoadingLevel(UNetManager m)
        {
            State s = Peek(m);
            return s != null && s.isLoadingLevel;
        }

        internal static void TogglePause()
        {
            UNetManager m = Current;
            if (BtcNetworkGate.Enabled && m != null) RequestVoluntaryPauseToggle(m);
        }

        internal static void ToggleClientPausePermission()
        {
            UNetManager m = Current;
            if (BtcNetworkGate.Enabled && m != null) RequestToggleClientPausePermission(m);
        }

        internal static string StatusText()
        {
            if (!BtcNetworkGate.Enabled) return "BetterConnection is off (vanilla networking)";
            UNetManager m = Current;
            State s = Peek(m);
            if (m == null || !InSession || s == null) return "Not in a network session";
            if (s.connectionDegraded && s.recoveryDrainScheduled) return "RECOVERING...";
            if (s.connectionDegraded || s.hardPauseActive) return "HARDPAUSED (connection lost)";
            if (s.voluntaryPauseActive) return "PAUSED BY " + (string.IsNullOrEmpty(s.voluntaryPauseOwnerName) ? "?" : s.voluntaryPauseOwnerName);
            return "Connection OK";
        }

        internal static List<string> PingLines()
        {
            List<string> list = new List<string>();
            UNetManager m = Current;
            State s = Peek(m);
            if (m == null || s == null || !InSession) return list;
            try
            {
                List<PingRowInfo> rows = BuildPingRows(m, s);
                for (int i = 0; i < rows.Count; i++)
                {
                    string badge = rows[i].ShowVoluntaryPauseBadge ? "[P] " : (rows[i].ShowAwaitingRecoveryBadge ? "[!] " : string.Empty);
                    list.Add(badge + rows[i].Label);
                }
            }
            catch (Exception) { }
            return list;
        }

        // ------------------------------------------------------------------ overlay (legacy UNetManager.OnGUI)
        internal static void DrawOverlay(UNetManager m)
        {
            if (!BtcNetworkGate.Enabled || m == null) return;
            State s = Peek(m);
            if (s == null || !s.bcSession) return;
            bool hard = (s.connectionDegraded && !s.recoveryDrainScheduled) || (s.hardPauseActive && !s.connectionDegraded);
            bool recovering = s.connectionDegraded && s.recoveryDrainScheduled;
            bool voluntary = s.voluntaryPauseActive && !hard && !recovering;
            if (m._currentSceneName != m.lobbyScene && (hard || recovering || voluntary))
            {
                EnsureConnectionOverlayTexture(s);
                EnsureConnectionOverlayLabelStyle(s);
                Color old = GUI.color;
                float b = 1.5f;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(14f - b, 14f - b, 22f + b * 2f, 22f + b * 2f), s.connectionOverlayTexture, ScaleMode.StretchToFill, true);
                GUI.color = voluntary ? new Color(0.2f, 0.75f, 0.25f, 1f) : (hard ? new Color(0.85f, 0.16f, 0.16f, 1f) : new Color(0.95f, 0.78f, 0.1f, 1f));
                GUI.DrawTexture(new Rect(14f, 14f, 22f, 22f), s.connectionOverlayTexture, ScaleMode.StretchToFill, true);
                GUI.color = old;
                string owner = string.IsNullOrEmpty(s.voluntaryPauseOwnerName) ? "?" : s.voluntaryPauseOwnerName;
                string text = voluntary ? ("PAUSED BY " + owner) : (hard ? "HARDPAUSED" : "RECOVERING...");
                GUI.color = Color.white;
                GUI.Label(new Rect(42f, 14f, 220f, 22f), text, s.connectionOverlayLabelStyle);
                GUI.color = old;
            }
            if (BtcReplay.Mode != BtcReplay.State.Playing && NetworkServer.active && BtcConfig.ShowPauseLabel.Value)
            {
                EnsureConnectionOverlayLabelStyle(s);
                Color old2 = GUI.color;
                GUI.color = s.clientsCanVoluntaryPause ? Color.white : new Color(0.85f, 0.16f, 0.16f, 1f);
                GUI.Label(new Rect(14f, 42f, 260f, 18f), s.clientsCanVoluntaryPause ? "Client pause: allowed" : "Client pause: DISABLED", s.connectionOverlayLabelStyle);
                GUI.color = old2;
            }
            if (BtcReplay.Mode != BtcReplay.State.Playing && BtcConfig.ShowPing.Value && (NetworkServer.active || (m.LocalNetworkClient != null && m.LocalNetworkClient.isConnected)))
            {
                EnsureConnectionOverlayLabelStyle(s);
                EnsureConnectionOverlayTexture(s);
                List<PingRowInfo> rows = BuildPingRows(m, s);
                if (rows.Count == 0) return;
                Color old3 = GUI.color;
                float rowH = 18f, top = 60f, badge = 14f;
                for (int i = 0; i < rows.Count; i++)
                {
                    PingRowInfo r = rows[i];
                    float y = top + i * rowH;
                    if (r.ShowVoluntaryPauseBadge || r.ShowAwaitingRecoveryBadge)
                    {
                        GUI.color = r.ShowVoluntaryPauseBadge ? new Color(0.2f, 0.75f, 0.25f, 1f) : new Color(0.85f, 0.16f, 0.16f, 1f);
                        GUI.DrawTexture(new Rect(10f, y + 1f, badge, badge), s.connectionOverlayTexture, ScaleMode.StretchToFill, true);
                        GUI.color = old3;
                        if (r.ShowVoluntaryPauseBadge)
                        {
                            GUI.color = Color.white;
                            GUI.Label(new Rect(10f, y - 1f, badge, badge), "P", s.connectionOverlayLabelStyle);
                            GUI.color = old3;
                        }
                    }
                    GUI.color = Color.white;
                    GUI.Label(new Rect(10f + badge + 4f, y, 220f, rowH), r.Label, s.connectionOverlayLabelStyle);
                    GUI.color = old3;
                }
            }
        }

        private static void EnsureConnectionOverlayTexture(State s)
        {
            if (s.connectionOverlayTexture != null) return;
            int n = 32;
            Texture2D tex = new Texture2D(n, n, TextureFormat.ARGB32, false);
            Vector2 c = new Vector2(n * 0.5f, n * 0.5f);
            float r = n * 0.5f - 1f;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float d = Vector2.Distance(new Vector2(j + 0.5f, i + 0.5f), c);
                    tex.SetPixel(j, i, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 1f)));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            s.connectionOverlayTexture = tex;
        }

        private static void EnsureConnectionOverlayLabelStyle(State s)
        {
            if (s.connectionOverlayLabelStyle != null) return;
            s.connectionOverlayLabelStyle = new GUIStyle(GUI.skin.label);
            s.connectionOverlayLabelStyle.fontStyle = FontStyle.Bold;
            s.connectionOverlayLabelStyle.normal.textColor = Color.white;
        }

        // ------------------------------------------------------------------ diagnostics (files under BtcPaths.DebugFile)
        private static StreamWriter _diagLogWriter;

        internal static void DiagLog(string message)
        {
            if (!BtcNetworkGate.Enabled || !BtcConfig.DiagLog.Value) return;
            try
            {
                if (_diagLogWriter == null)
                {
                    string side = NetworkServer.active ? "host" : (NetworkClient.active ? "client" : "unknown");
                    _diagLogWriter = new StreamWriter(BtcPaths.DebugFile("unet_netdiag_" + side + ".log"), true);
                    _diagLogWriter.AutoFlush = true;
                }
                _diagLogWriter.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " " + message);
            }
            catch (Exception) { }
        }

        internal static void BcFileLog(string message)
        {
            if (!BtcConfig.DiagLog.Value) return;
            try
            {
                File.AppendAllText(BtcPaths.DebugFile("betterconnection_netlog.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + "\r\n");
            }
            catch (Exception) { }
        }

        private static void LogConnectionBufferStats(UNetManager m)
        {
            if (!BtcConfig.DiagLog.Value) return;
            try
            {
                int a, msgs, bytes, d;
                if (NetworkServer.active)
                {
                    for (int i = 0; i < NetworkServer.connections.Count; i++)
                    {
                        NetworkConnection c = NetworkServer.connections[i];
                        if (c == null) continue;
                        c.GetStatsOut(out a, out msgs, out bytes, out d);
                        DiagLog(string.Format("[diag] server conn {0} buffered msgs={1} bytes={2}", c.connectionId, msgs, bytes));
                    }
                }
                else
                {
                    NetworkConnection c = (m.LocalNetworkClient != null) ? m.LocalNetworkClient.connection : null;
                    if (c != null)
                    {
                        c.GetStatsOut(out a, out msgs, out bytes, out d);
                        DiagLog(string.Format("[diag] client conn buffered msgs={0} bytes={1}", msgs, bytes));
                    }
                }
            }
            catch (Exception ex) { DiagLog("[diag] LogConnectionBufferStats failed: " + ex.Message); }
        }

        private static void OnUnityLogMessageForDiag(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error) DiagLog("UNITY LOG [" + type + "]: " + condition + "\n" + stackTrace);
        }

        // ------------------------------------------------------------------ SimpleHostScript.Run (legacy body, BC ON / replay proxies)
        /// <summary>
        /// Legacy SimpleHostScript.Run: same as vanilla plus spectator slots (BtcLobby.MakeSpectator), replay proxy users
        /// (BtcReplay.ProxyUsers), index guards and a loop instead of Slinq.First (no exception on a missing uid).
        /// </summary>
        internal static void HostRun()
        {
            Asset_Gamemode.FactionPrototype[] array = GameParam.Gamemode.Disk_ReadFactionPrototypes();
            List<Faction> list = new List<Faction>(array.Length);
            for (int num = 0; num != array.Length; num++) list.Add(array[num].CreateFaction());
            if (list.Count == 0) return;
            List<Faction> list2 = new List<Faction>();
            if (GameParam.Map.IsNewGame())
            {
                list2 = list;
            }
            else
            {
                Faction.FactionMeta[] array2 = GameParam.Map.Disk_ReadFactionMeta();
                if (array2 == null) list2 = list;
                else
                {
                    for (int i = 0; i < array2.Length; i++)
                    {
                        if (array2[i].assigned) list2.Add(list[i]);
                    }
                }
            }
            for (int j = 0; j < list.Count; j++) NetworkServer.Spawn(list[j].gameObject, list[j].AssetKey.AssetId);
            for (int num2 = 0; num2 != array.Length; num2++)
            {
                Faction fac = null;
                for (int k = 0; k < list.Count; k++)
                {
                    if (list[k].ID == array[num2].uid)
                    {
                        fac = list[k];
                        break;
                    }
                }
                array[num2].PopulateFaction(fac);
            }
            if (UNetManager.instance.debugLoader)
            {
                list[0].AddOwner(User.LocalUser);
            }
            else
            {
                if (!Neo.IsMultiplayer && !GameParam.Map.IsNewGame()) User.LocalUser.factionNumber = Faction.GetFirstAvalaibleFaction();
                List<User> list3 = new List<User>(UNetManager.instance.UserList);
                if (BtcReplay.ProxyUsers != null) list3.AddRange(BtcReplay.ProxyUsers);
                for (int l = 0; l < list3.Count; l++)
                {
                    User user = list3[l];
                    if (user != null && BtcLobby.IsSpecNum(user.factionNumber))
                    {
                        BtcLobby.MakeSpectator((user.connectionToClient != null) ? UNetManager.instance.GetUserFromConnection(user.connectionToClient) : user);
                        continue;
                    }
                    if (user == null || user.factionNumber < 0 || user.factionNumber >= list2.Count) continue;
                    Faction faction = list2[user.factionNumber];
                    User owner = (user.connectionToClient != null) ? UNetManager.instance.GetUserFromConnection(user.connectionToClient) : user;
                    faction.AddOwner(owner);
                    for (int n = 0; n < list3.Count; n++)
                    {
                        User other = list3[n];
                        if (other == null || other.factionNumber < 0 || other.factionNumber >= list2.Count) continue;
                        faction.SetFactionRelation(list2[other.factionNumber], other.TeamNumber == user.TeamNumber ? Affiliation.Relation.Allied : Affiliation.Relation.Enemy);
                    }
                    faction.SetFactionRelation(Faction.DefaultEnemy, Affiliation.Relation.Enemy);
                    faction.SetFactionRelation(Faction.DefaultNeutral, Affiliation.Relation.Neutral);
                    Faction.DefaultEnemy.SetFactionRelation(faction, Affiliation.Relation.Enemy);
                    Faction.DefaultNeutral.SetFactionRelation(faction, Affiliation.Relation.Neutral);
                }
                Faction.DefaultEnemy.SetFactionRelation(Faction.DefaultNeutral, Affiliation.Relation.Neutral);
                Faction.DefaultNeutral.SetFactionRelation(Faction.DefaultEnemy, Affiliation.Relation.Neutral);
            }
            BrixSingleton<AutoList>.Instance.GetInstances(Groupes.Idle).Slinq<GameObject>().ForEach(delegate(GameObject g)
            {
                g.GetComponent<NetworkIdentity>().RebuildObservers(false);
            });
            GameParam.instance.NetworkHostLoadedAllFactions = true;
        }

        // ------------------------------------------------------------------ messages (wire format = legacy)
        internal class HeartbeatMessage : MessageBase { }

        internal class PingMessage : MessageBase
        {
            public float sentTime;
            public override void Serialize(NetworkWriter writer) { writer.Write(sentTime); }
            public override void Deserialize(NetworkReader reader) { sentTime = reader.ReadSingle(); }
        }

        internal class VoluntaryPauseRequestMessage : MessageBase { }

        internal class VoluntaryPauseStateMessage : MessageBase
        {
            public bool paused;
            public int ownerConnectionId;
            public string ownerName;

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(paused);
                writer.Write(ownerConnectionId);
                writer.Write(ownerName ?? string.Empty);
            }

            public override void Deserialize(NetworkReader reader)
            {
                paused = reader.ReadBoolean();
                ownerConnectionId = reader.ReadInt32();
                ownerName = reader.ReadString();
            }
        }

        internal class VoluntaryPausePermissionMessage : MessageBase
        {
            public bool clientsCanPause;
            public override void Serialize(NetworkWriter writer) { writer.Write(clientsCanPause); }
            public override void Deserialize(NetworkReader reader) { clientsCanPause = reader.ReadBoolean(); }
        }
    }

    /// <summary>
    /// Plugin-owned component added to the UNetManager GameObject: draws the legacy connection overlay
    /// (vanilla UNetManager has no OnGUI) and drops the per-instance state when the manager is destroyed.
    /// </summary>
    internal sealed class BtcNetOverlay : MonoBehaviour
    {
        private UNetManager _mgr;

        private void Awake() { _mgr = GetComponent<UNetManager>(); useGUILayout = false; }

        private void OnGUI()
        {
            try { BtcNetManager.DrawOverlay(_mgr); }
            catch (Exception e) { BtcLog.Error("Net", "overlay: " + e.Message); enabled = false; }
        }

        private void OnDestroy()
        {
            if (_mgr != null) BtcNetManager.Forget(_mgr.GetInstanceID());
        }
    }
}
