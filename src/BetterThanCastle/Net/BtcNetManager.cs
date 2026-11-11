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
    /// Message ids: 64 heartbeat (BCv2 payload), 70/71 ping req/resp, 72/73/74 pause req/state(voluntary + host link pause)/perm,
    /// 110 session end, 111 rejoin. Link monitor and pause policy: Net/BtcNetLink.cs.
    /// </summary>
    internal static partial class BtcNetManager
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
        private const float PingInterval = 10f;
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
            internal bool connectionDegraded;        // legacy field (read by dev tools); BCv2 keeps it false
            internal bool recoveryDrainScheduled;    // legacy field; BCv2 keeps it false
            internal bool hardPauseActive;           // link pause in effect locally (host: decided here; client: from the host or own reconnect)
            internal float preHardPauseTimeScale = 1f;
            internal bool heartbeatHandlerRegistered;
            internal float lastHeartbeatSendTime;
            internal readonly List<User> pendingReconnectUsers = new List<User>();
            // BCv2 link monitor (Net/BtcNetLink.cs)
            internal readonly Dictionary<int, Peer> peers = new Dictionary<int, Peer>();   // host: by connection id
            internal readonly Peer hostPeer = new Peer();                                  // client: the host
            internal readonly Dictionary<int, float> awaitingRejoin = new Dictionary<int, float>();   // host: new conn id -> connect time
            internal readonly HashSet<string> continueWithout = new HashSet<string>();     // host: names it chose not to wait for
            internal float lastTickRt = -1f;
            internal float frameMax;
            internal float ownStallAt = -100f;
            internal float ownStallLen;
            internal string hardText;                // reason lines of the link pause (host text, shown everywhere)
            internal float hardSince;
            internal int pauseCount;
            internal float pausedTotal;
            internal float lastPauseBroadcast;
            internal bool hostHard;                  // client: the host says the game is link-paused
            internal bool clientLinkWarn;
            internal bool hostEnded;                 // client: the host left on purpose (msg 110), no reconnect
            internal bool rejoinPending;             // client: send our name after the next connect
            internal string myName;
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
            internal float levelLiveAt;          // time the level became playable for everyone (GameSignals.LevelReady); MaxValue while loading
            internal bool wasLive = true;
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
            s.peers.Clear();
            s.awaitingRejoin.Clear();
            if (clearClientHeartbeat) ResetPeer(s.hostPeer);
            s.connectionDegraded = false;
            s.recoveryDrainScheduled = false;
            if (clearPending) { s.reconnectRecoveryInProgress = false; s.pendingReconnectUsers.Clear(); s.continueWithout.Clear(); }
            s.hostHard = false;
            s.hardText = null;
            ReleaseHard(s);
            s.clientsCanVoluntaryPause = true;
            if (s.voluntaryPauseActive) SetVoluntaryPause(m, s, false, NoVoluntaryPauseOwner, null);
        }

        /// <summary>UNetManager.Disconnect prefix.</summary>
        internal static void OnDisconnect(UNetManager m)
        {
            State s = Peek(m);
            if (s == null) return;
            if (s.reconnectRecoveryInProgress) BtcLog.Warn("Net", "Disconnect() during a reconnect, caller: " + Caller());
            AnnounceSessionEnd(m);
            if (s.clientReconnectCoroutine != null)
            {
                m.StopCoroutine(s.clientReconnectCoroutine);
                s.clientReconnectCoroutine = null;
            }
            s.wasConnected = false;
            s.rejoinPending = false;
            ResetSession(m, s, true, true);
        }

        internal static void OnLobbyStopHost(UNetManager m)
        {
            State s = Peek(m);
            if (s == null) return;
            AnnounceSessionEnd(m);
            ResetSession(m, s, true, false);
        }

        internal static string CallerTrace() { return Caller(); }

        private static string Caller()
        {
            string t = System.Environment.StackTrace.Replace(System.Environment.NewLine, " | ");
            return t.Length > 1200 ? t.Substring(0, 1200) : t;
        }

        private static void ResetPeer(Peer p)
        {
            p.silence = 0f; p.hear = 0f; p.stall = 0f; p.loading = false; p.seen = false; p.hold = false; p.badFor = 0f; p.healthy = 0f; p.deafFor = 0f; p.why = null;
        }

        /// <summary>Local release of the link pause (session start/end, load start).</summary>
        private static void ReleaseHard(State s)
        {
            if (!s.hardPauseActive) return;
            s.hardPauseActive = false;
            RecomputeEffectivePause(s);
        }

        internal static void OnStopClient(UNetManager m)
        {
            State s = Peek(m);
            if (s == null) return;
            if (s.reconnectRecoveryInProgress) BtcLog.Warn("Net", "StopClient during a reconnect, caller: " + Caller());
            // keep bcSession/pending users while a reconnect loop is running: the next StartClient re-registers.
            ResetSession(m, s, false, true);
        }

        /// <summary>UNetManager.OnStartServer postfix (BC ON).</summary>
        internal static void OnStartServer(UNetManager m)
        {
            State s = Get(m);
            BtcNetId.OnServerStart();
            s.peers.Clear();
            s.awaitingRejoin.Clear();
            s.continueWithout.Clear();
            s.connectionDegraded = false;
            s.recoveryDrainScheduled = false;
            s.pendingReconnectUsers.Clear();
            s.pauseCount = 0;
            s.pausedTotal = 0f;
            RegisterHeartbeatHandler(m);
            s.heartbeatHandlerRegistered = true;
            RegisterPingHandler(m);
            RegisterVoluntaryPauseHandlers(m, null);
            RegisterServerHandler(MsgRejoin, delegate(NetworkMessage msg) { OnRejoinReceived(m, msg); });
            s.bcSession = true;
            s.clientsCanVoluntaryPause = true;
            if (s.voluntaryPauseActive) SetVoluntaryPause(m, s, false, NoVoluntaryPauseOwner, null);
            s.hardText = null;
            ReleaseHard(s);
            BtcNetCheck.OnServerStarted(m);
            BtcLog.Info("Net", "BetterConnection v2 server session started (handlers 64,70-74,111 registered)");
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
            {
                client.RegisterHandler(MsgUnetDisconnect, delegate(NetworkMessage netMsg) { m.OnClientDisconnect(netMsg.conn); });
                RegisterClientHandler(client, MsgSessionEnd, delegate(NetworkMessage netMsg) { OnSessionEndReceived(m); });
            }
            s.bcSession = true;
        }

        /// <summary>UNetManager.OnClientConnect postfix (BC ON).</summary>
        internal static void OnClientConnect(UNetManager m, NetworkConnection conn)
        {
            State s = Get(m);
            ResetPeer(s.hostPeer);
            RegisterHeartbeatHandler(m);
            s.heartbeatHandlerRegistered = true;
            RegisterPingHandler(m);
            RegisterVoluntaryPauseHandlers(m, null);
            if (m.LocalNetworkClient != null) RegisterClientHandler(m.LocalNetworkClient, MsgSessionEnd, delegate(NetworkMessage netMsg) { OnSessionEndReceived(m); });
            s.bcSession = true;
            s.wasConnected = true;
            s.hostEnded = false;
            RaiseChannelBuffers(m, conn, "client");
            s.connectionDegraded = false;
            s.recoveryDrainScheduled = false;
            bool rejoin = s.rejoinPending || s.reconnectRecoveryInProgress;
            s.reconnectRecoveryInProgress = false;
            s.rejoinPending = false;
            if (rejoin) SendRejoin(m, s);
            ApplyClientPause(m, s);
            BtcNetCheck.OnClientConnected(m);
            BtcLog.Info("Net", "BetterConnection v2 client connected" + (rejoin ? " (reconnect)" : "") + " (handlers 64,70-77,110 registered)");
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
            StartClientReconnectLoop(m, s);
        }

        /// <summary>UNetManager.OnServerDisconnect (BC ON). Returns true when the player is kept alive for a reconnect.</summary>
        private static User _dropUser;
        private static int _dropConnId = -1;

        /// <summary>OnServerDisconnect, BEFORE the base call (which may clear the player of the connection): remember its User.</summary>
        internal static void BeforeServerDisconnect(UNetManager m, NetworkConnection conn)
        {
            _dropUser = conn != null ? SafeUser(m, conn) : null;
            _dropConnId = conn != null ? conn.connectionId : -1;
        }

        internal static bool TryKeepDroppedPlayer(UNetManager m, NetworkConnection conn)
        {
            State s = Get(m);
            if (!s.bcSession) { BtcLog.Warn("Net", "Server: connection " + conn.connectionId + " dropped outside a BC session"); return false; }
            User user = SafeUser(m, conn);
            if (user == null)
            {
                // the User may not own this connection (connectionToClient unset): match the cached lobby name instead
                string cn;
                if (s.cachedPlayerNameByConnectionId.TryGetValue(conn.connectionId, out cn) && !string.IsNullOrEmpty(cn) && m.UserList != null)
                    user = m.UserList.Find(x => x != null && UserName(x) == cn);
            }
            if (user == null && _dropUser != null && _dropConnId == conn.connectionId) user = _dropUser;
            _dropUser = null;
            if (user == null || BtcSpectate.IsSpec(conn))
            {
                string users = "";
                try { if (m.UserList != null) foreach (User x in m.UserList) users += (x == null ? "null" : UserName(x) + "@" + (x.connectionToClient != null ? x.connectionToClient.connectionId.ToString() : "-")) + " "; } catch (Exception) { }
                string cn; s.cachedPlayerNameByConnectionId.TryGetValue(conn.connectionId, out cn);
                BtcLog.Warn("Net", "Server: connection " + conn.connectionId + " dropped, not kept (" + (user == null ? "no user" : "spectator") + ") users=[" + users + "] cachedName=" + cn);
                return false;
            }
            BtcLog.Warn("Net", "Server: connection " + conn.connectionId + " dropped, keeping player " + user.name + " alive and waiting for reconnect.");
            user.NetworkReady = false;
            string name = UserName(user);
            BtcHud.Notice(BtcReplayUI.T(name + " lost connection", name + " потерял связь"), BtcReplayIcons.Cross, BtcHud.Warn);
            if (!s.pendingReconnectUsers.Contains(user)) s.pendingReconnectUsers.Add(user);
            s.peers.Remove(conn.connectionId);
            s.awaitingRejoin.Remove(conn.connectionId);
            return true;
        }

        /// <summary>UNetManager.OnServerConnect postfix part (BC ON). connState = BtcSpectate.PreConnect() result.</summary>
        internal static void OnServerConnect(UNetManager m, NetworkConnection conn, BtcSpectate.ConnState connState)
        {
            State s = Get(m);
            ResetPeer(PeerOf(s, conn.connectionId));
            RaiseChannelBuffers(m, conn, "server");
            // a returning player says its name (msg 111); FallbackRejoins hands out the oldest dropped player if it never does
            if (s.pendingReconnectUsers.Count > 0 && connState == null)
            {
                s.awaitingRejoin[conn.connectionId] = Time.realtimeSinceStartup;
                BtcLog.Info("Net", "Server: connection " + conn.connectionId + " may be a returning player, waiting for its name");
            }
        }

        /// <summary>
        /// A client that was really connected (not a failed first connect) enters the reconnect loop instead of the menu,
        /// also while a map loads; not in the lobby and not when the host ended the session on purpose (msg 110).
        /// </summary>
        internal static bool CanReconnect(UNetManager m)
        {
            State s = Peek(m);
            return s != null && (s.wasConnected || s.reconnectRecoveryInProgress) && !NetworkServer.active && !InLobby(m) && !s.hostEnded;
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
            BtcNetCheck.OnClientError(true);
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
            StartClientReconnectLoop(m, Get(m));
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
            if (s.myName == null && !NetworkServer.active && User.LocalUser != null)
            {
                try { if (!string.IsNullOrEmpty(User.LocalUser.ScreenName)) s.myName = User.LocalUser.ScreenName; } catch (Exception) { }
            }
            BtcNetCheck.Tick(m);
            bool live = NetLive(m, s);
            if (live && !s.wasLive)
            {
                // the load is over: silence during it was not a fault, start the link monitor from a clean slate
                BtcNetCheck.HeartbeatFrom(-1);
                foreach (KeyValuePair<int, Peer> kv in s.peers) { kv.Value.silence = 0f; kv.Value.hold = false; }
                s.hostPeer.silence = 0f;
            }
            s.wasLive = live;
            LinkTick(m, s);
            if (Time.unscaledTime >= s.lastPingSendTime + PingInterval)
            {
                s.lastPingSendTime = Time.unscaledTime;
                SendPings(m, s);
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

        // ------------------------------------------------------------------ pause
        private const float LiveGrace = 5f;
        private const float LiveFallback = 600f;

        /// <summary>
        /// False while a map is loading: from the load start until vanilla says the level is ready for every player
        /// (GameSignals.LevelReady) plus a short grace. Packet loss during the load is normal, so no hard pause,
        /// silence detection or reconnect runs until then. In the lobby it is always true.
        /// </summary>
        internal static bool NetLive(UNetManager m, State s)
        {
            if (s == null) return true;
            if (m != null && m._currentSceneName == m.lobbyScene) return true;
            float now = Time.unscaledTime;
            if (s.levelLiveAt != float.MaxValue && now >= s.levelLiveAt + LiveGrace) return true;
            return s.isLoadingLevel == false && s.loadingLevelStartTime > 0f && now >= s.loadingLevelStartTime + LiveFallback;
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
                    BroadcastPauseState(s);
                }
            }
            else
            {
                bool allowed = requesterIsHost || s.clientsCanVoluntaryPause;
                DiagLog("ServerHandleVoluntaryPauseToggle - engage attempt by " + requesterName + " (conn " + requesterConnectionId + ", isHost=" + requesterIsHost + "), clientsCanVoluntaryPause=" + s.clientsCanVoluntaryPause + ", allowed=" + allowed);
                if (allowed)
                {
                    SetVoluntaryPause(m, s, true, requesterConnectionId, requesterName);
                    BroadcastPauseState(s);
                }
            }
        }

        /// <summary>Host: the whole pause state (voluntary + link pause with its reason) to every client; on change and every second while paused.</summary>
        private static void BroadcastPauseState(State s)
        {
            if (!NetworkServer.active || !s.bcSession) return;
            s.lastPauseBroadcast = Time.realtimeSinceStartup;
            VoluntaryPauseStateMessage msg = new VoluntaryPauseStateMessage
            {
                paused = s.voluntaryPauseActive,
                ownerConnectionId = s.voluntaryPauseOwnerConnectionId,
                ownerName = s.voluntaryPauseOwnerName ?? string.Empty,
                hard = s.hardPauseActive,
                hardText = s.hardText ?? string.Empty
            };
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                NetworkConnection c = NetworkServer.connections[i];
                if (c != null && !IsLocalConnection(c)) c.Send(MsgPauseState, msg);
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
            State s = Get(m);
            if (msg.hard != s.hostHard) BtcLog.Info("Net", "host link pause " + (msg.hard ? "ON: " + msg.hardText.Replace('\n', ';') : "OFF"));
            s.hostHard = msg.hard;
            s.hardText = msg.hard ? msg.hardText : null;
            if (msg.paused != s.voluntaryPauseActive)
                SetVoluntaryPause(m, s, msg.paused, msg.ownerConnectionId, string.IsNullOrEmpty(msg.ownerName) ? null : msg.ownerName);
            ApplyClientPause(m, s);
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
                s.levelLiveAt = float.MaxValue;
                s.wasLive = false;
                GameSignals.Once(GameSignals.LevelReady, delegate { s.levelLiveAt = Time.unscaledTime; }, null);
                if (s.voluntaryPauseActive)
                {
                    SetVoluntaryPause(m, s, false, NoVoluntaryPauseOwner, null);
                    if (NetworkServer.active) BroadcastPauseState(s);
                }
                s.hostHard = false;
                s.hardText = null;
                ReleaseHard(s);
                if (NetworkServer.active) BroadcastPauseState(s);
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
            s.rejoinPending = true;
            ApplyClientPause(m, s);
            if (s.clientReconnectCoroutine == null) s.clientReconnectCoroutine = m.StartCoroutine(ClientReconnectLoop(m, s));
        }

        /// <summary>
        /// BCv2: runs until OnClientConnect clears reconnectRecoveryInProgress (legacy looped on !NetworkClient.active, which stays
        /// true while the dead client object exists, so the loop ended at once and the client kept a hard pause forever).
        /// Stops on a user Disconnect() or when the host ended the session.
        /// </summary>
        private static IEnumerator ClientReconnectLoop(UNetManager m, State s)
        {
            string address = m.networkAddress;
            int port = m.networkPort;
            BtcLog.Info("Net", "Client reconnect loop started, target " + address + ":" + port + ".");
            int attempt = 0;
            float started = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(1f);
            while (s.reconnectRecoveryInProgress && !m.calledDisconnect && !s.hostEnded)
            {
                attempt++;
                BtcLog.Info("Net", "Client reconnect attempt " + attempt + " to " + address + ":" + port + ".");
                AttemptClientReconnect(m, address, port);
                float until = Time.realtimeSinceStartup + 6f;
                while (Time.realtimeSinceStartup < until && s.reconnectRecoveryInProgress && !m.calledDisconnect) yield return null;
                if (!s.reconnectRecoveryInProgress) break;
                yield return new WaitForSecondsRealtime(Mathf.Min(1f + attempt * 0.5f, 4f));
            }
            BtcLog.Info("Net", !s.reconnectRecoveryInProgress ? "Client reconnect succeeded after " + (Time.realtimeSinceStartup - started).ToString("0.0") + " s." : "Client reconnect loop stopped (" + (s.hostEnded ? "host ended the session" : "manual disconnect") + ").");
            s.clientReconnectCoroutine = null;
            if (s.reconnectRecoveryInProgress && s.hostEnded && !m.calledDisconnect)
            {
                s.reconnectRecoveryInProgress = false;
                ApplyClientPause(m, s);
                try { m.Disconnect(); } catch (Exception e) { BtcLog.Warn("Net", "leave after session end: " + e.Message); }
            }
        }

        /// <summary>Reconnect card button: give up and go back to the menu.</summary>
        internal static void LeaveToMenu()
        {
            UNetManager m = Current;
            State s = Peek(m);
            if (m == null) return;
            BtcLog.Info("Net", "player left while reconnecting");
            if (s != null) { s.reconnectRecoveryInProgress = false; ApplyClientPause(m, s); }
            try { m.Disconnect(); } catch (Exception e) { BtcLog.Warn("Net", "leave: " + e.Message); }
        }

        internal static bool Reconnecting
        {
            get { State s = Peek(Current); return BtcNetworkGate.Enabled && s != null && s.reconnectRecoveryInProgress; }
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
            public float Ms;
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
                    if (ping == "...") ms = -1f;
                    bool owner = s.voluntaryPauseActive && s.voluntaryPauseOwnerConnectionId == c.connectionId;
                    list.Add(new PingRowInfo
                    {
                        Label = string.Format("{0}: {1}", ResolvePlayerName(m, s, c), ping),
                        Ms = ms,
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
                    Ms = s.pingMsToHost,
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
            if (s.reconnectRecoveryInProgress && !NetworkServer.active) return "RECONNECTING...";
            if (s.hardPauseActive) return "LINK PAUSE: " + (s.hardText ?? "").Replace('\n', ';');
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
            BtcHud.BeginLeft();
            bool recovering = s.reconnectRecoveryInProgress && !NetworkServer.active;
            bool hard = s.hardPauseActive && !recovering;
            bool voluntary = s.voluntaryPauseActive && !hard && !recovering;
            bool inGame = m._currentSceneName != m.lobbyScene;
            string owner = string.IsNullOrEmpty(s.voluntaryPauseOwnerName) ? "?" : s.voluntaryPauseOwnerName;
            if (inGame && BtcReplay.Mode != BtcReplay.State.Playing)
            {
                int st = hard ? 1 : (recovering ? 2 : (voluntary ? 3 : 0));
                if (st != _noticeState && Event.current.type == EventType.Repaint)
                {
                    if (st == 3 || st == 0) { if (st == 0 && _noticeState > 0 && _noticeState != 3) BtcHud.Notice(BtcReplayUI.T("The game continues", "Игра продолжается"), BtcReplayIcons.Play, BtcHud.Ok); }
                    _noticeState = st;
                }
            }
            else _noticeState = -1;
            // the link pause and the reconnect card are always shown (they explain a frozen game); the label setting hides the rest
            if (inGame && BtcReplay.Mode != BtcReplay.State.Playing && (hard || recovering))
            {
                if (recovering)
                {
                    BtcHud.Card("netpause", BtcReplayIcons.Refresh, BtcHud.Warn, BtcReplayUI.T("Reconnecting to the host...", "Переподключение к хосту..."),
                        BtcReplayUI.T("The game waits for you. Your units are safe.", "Игра ждёт тебя, твои юниты на месте."), null, false,
                        BtcReplayUI.T("Leave to menu", "Выйти в меню"), LeaveToMenu);
                }
                else
                {
                    List<string> lines = string.IsNullOrEmpty(s.hardText) ? null : new List<string>(s.hardText.Split('\n'));
                    string title = BtcReplayUI.T("Waiting for a player", "Ждём игрока");
                    if (NetworkServer.active)
                        BtcHud.Card("netpause", BtcReplayIcons.Pause, BtcHud.Warn, title, BtcReplayUI.T("Connection problem, the game is paused for everyone.", "Проблема со связью, игра на паузе у всех."), lines, false,
                            BtcReplayUI.T("Continue without them", "Продолжить без них"), ContinueWithoutBlocking);
                    else
                        BtcHud.Card("netpause", BtcReplayIcons.Pause, BtcHud.Warn, title, BtcReplayUI.T("Connection problem, the host paused the game for everyone.", "Проблема со связью, хост поставил игру на паузу у всех."), lines, false);
                }
            }
            else if (!NetworkServer.active && s.clientLinkWarn && inGame && BtcReplay.Mode != BtcReplay.State.Playing)
            {
                BtcHud.Plate("netwarn", BtcReplayIcons.Wave, BtcHud.Warn, BtcReplayUI.T("Host is not responding...", "Хост не отвечает..."));
            }
            if (inGame && voluntary && BtcConfig.ShowPauseLabel.Value)
            {
                // stays on screen until the pause is released
                BtcHud.Card("pause", BtcReplayIcons.Pause, BtcHud.Ok, BtcReplayUI.T("Paused by " + owner, "Пауза: " + owner), null, null, false);
            }
            if (BtcReplay.Mode != BtcReplay.State.Playing && NetworkServer.active && BtcConfig.ShowPauseLabel.Value)
            {
                bool can = s.clientsCanVoluntaryPause;
                BtcHud.Plate("clientpause", can ? BtcReplayIcons.Check : BtcReplayIcons.Cross, can ? BtcHud.Ok : BtcHud.Danger, can ? BtcReplayUI.T("Client pause: allowed", "Пауза клиентов: разрешена") : BtcReplayUI.T("Client pause: DISABLED", "Пауза клиентов: ЗАПРЕЩЕНА"));
            }
            if (BtcReplay.Mode != BtcReplay.State.Playing && BtcConfig.ShowPing.Value && (NetworkServer.active || (m.LocalNetworkClient != null && m.LocalNetworkClient.isConnected)))
            {
                List<PingRowInfo> rows = BuildPingRows(m, s);
                if (rows.Count == 0) return;
                string[] labels = new string[rows.Count];
                Color[] dots = new Color[rows.Count];
                float worst = -1f;
                for (int i = 0; i < rows.Count; i++)
                {
                    labels[i] = rows[i].Label;
                    dots[i] = rows[i].ShowVoluntaryPauseBadge ? BtcHud.Ok : (rows[i].ShowAwaitingRecoveryBadge ? BtcHud.Danger : new Color(0f, 0f, 0f, 0f));
                    if (rows[i].Ms > worst) worst = rows[i].Ms;
                }
                Color pc = worst < 0f ? new Color(1f, 1f, 1f, 0.6f) : (worst < 90f ? BtcHud.Ok : (worst < 170f ? BtcHud.Warn : BtcHud.Danger));
                BtcHud.Chip("ping", BtcReplayIcons.Wave, pc, worst < 0f ? "..." : string.Format("{0:0} ms", worst), labels, dots);
            }
        }

        private static int _noticeState = -1;

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

        private const long DiagLogMaxBytes = 20L * 1024 * 1024;   // legacy logs grew to 3 GB and cost frame time

        internal static void DiagLog(string message)
        {
            if (!BtcNetworkGate.Enabled || !BtcConfig.DiagLog.Value) return;
            try
            {
                if (_diagLogWriter != null && _diagLogWriter.BaseStream.Length > DiagLogMaxBytes) return;
                if (_diagLogWriter == null)
                {
                    string side = NetworkServer.active ? "host" : (NetworkClient.active ? "client" : "unknown");
                    string path = BtcPaths.DebugFile("unet_netdiag_" + side + ".log");
                    bool append = !(File.Exists(path) && new FileInfo(path).Length > DiagLogMaxBytes);
                    _diagLogWriter = new StreamWriter(path, append);
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
            public bool hard;            // BCv2: host link pause
            public string hardText;      // BCv2: its reason lines

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(paused);
                writer.Write(ownerConnectionId);
                writer.Write(ownerName ?? string.Empty);
                writer.Write(hard);
                writer.Write(hardText ?? string.Empty);
            }

            public override void Deserialize(NetworkReader reader)
            {
                paused = reader.ReadBoolean();
                ownerConnectionId = reader.ReadInt32();
                ownerName = reader.ReadString();
                hard = reader.ReadBoolean();
                hardText = reader.ReadString();
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

        private void OnApplicationQuit()
        {
            // the host closing the game is a deliberate end too: clients go to the menu instead of reconnecting forever
            try { if (_mgr != null) BtcNetManager.AnnounceSessionEnd(_mgr); } catch (Exception) { }
        }

        private void OnDestroy()
        {
            if (_mgr != null) BtcNetManager.Forget(_mgr.GetInstanceID());
        }
    }
}
