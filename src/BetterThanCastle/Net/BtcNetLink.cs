using System;
using System.Collections.Generic;
using System.Text;
using Brix.Game;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// BetterConnection v2 link monitor (BC ON only; nothing here runs, sends or registers with BC OFF).
    /// - Heartbeat 64 every 0.25 s carries: loading flag, how long the sender has not heard the other side, its longest own frame.
    /// - Silence is counted per peer with stall credit: one own frame adds at most 0.25 s, so a local freeze never looks like a dead link.
    /// - ONLY THE HOST decides a link pause (peer silent or deaf > 4 s, never while anyone loads) and sends it to everybody in the
    ///   pause state message 73 (with the reason). Clients never pause on their own, except while their own transport is reconnecting.
    /// - Players that dropped (pending reconnect) block the game until they return or the host presses "continue without".
    /// - 110: host leaves on purpose (clients go to the menu instead of reconnecting forever). 111: client tells its name after a reconnect.
    /// </summary>
    internal static partial class BtcNetManager
    {
        internal const short MsgSessionEnd = 110;
        internal const short MsgRejoin = 111;

        private const float LinkSendInterval = 0.25f;
        private const float LinkPauseAfter = 4f;
        private const float LinkStallCredit = 0.25f;
        private const float LinkDeadAfter = 20f;
        private const float LinkOwnStallIgnore = 3f;
        private const float PauseRebroadcast = 1f;
        private const float RejoinFallback = 4f;

        internal const byte HbLoading = 1;

        internal sealed class Peer
        {
            internal float silence;      // seconds without a heartbeat from this peer (own stalls credited at most LinkStallCredit per frame)
            internal float hear;         // the peer's report: seconds it has not heard us
            internal float stall;        // the peer's report: longest own frame since its previous heartbeat
            internal bool loading;       // the peer's report: it is loading a map
            internal bool seen;          // at least one heartbeat arrived
            internal bool hold;          // counts as a reason to pause (bad, or not healthy long enough yet)
            internal float badFor;       // length of the current outage
            internal float healthy;      // seconds fine since the outage ended
            internal float deafFor;      // seconds the peer's reports keep saying it does not hear us
            internal string why;         // "silent" / "deaf"
        }

        internal class HeartbeatMessage : MessageBase
        {
            public byte flags;
            public ushort hearDs;        // tenths of a second
            public ushort stallMs;
            public override void Serialize(NetworkWriter writer) { writer.Write(flags); writer.Write(hearDs); writer.Write(stallMs); }
            public override void Deserialize(NetworkReader reader) { flags = reader.ReadByte(); hearDs = reader.ReadUInt16(); stallMs = reader.ReadUInt16(); }
        }

        internal class RejoinMessage : MessageBase
        {
            public string name = "";
            public override void Serialize(NetworkWriter writer) { writer.Write(name ?? ""); }
            public override void Deserialize(NetworkReader reader) { name = reader.ReadString(); }
        }

        internal class EmptyMessage : MessageBase { }

        private static readonly HeartbeatMessage _hb = new HeartbeatMessage();
        private static readonly List<int> _tmpIds = new List<int>();

        private static Peer PeerOf(State s, int connId)
        {
            Peer p;
            if (!s.peers.TryGetValue(connId, out p)) { p = new Peer(); s.peers[connId] = p; }
            return p;
        }

        private static ushort Clamp16(float v) { return (ushort)Mathf.Clamp(Mathf.RoundToInt(v), 0, 65535); }

        /// <summary>Own loading state as the peers should see it: from the load start until the level is live.</summary>
        private static bool SelfLoading(UNetManager m, State s)
        {
            return !InLobby(m) && !NetLive(m, s);
        }

        private static bool InLobby(UNetManager m)
        {
            return m == null || m._currentSceneName == null || m._currentSceneName == m.lobbyScene;
        }

        /// <summary>Every frame of a BC session (replaces the legacy heartbeat send + silence monitor).</summary>
        private static void LinkTick(UNetManager m, State s)
        {
            float rt = Time.realtimeSinceStartup;
            float dt = s.lastTickRt < 0f ? 0f : rt - s.lastTickRt;
            s.lastTickRt = rt;
            if (dt > s.frameMax) s.frameMax = dt;
            if (dt > 1f) { s.ownStallAt = rt; s.ownStallLen = dt; }
            float credit = Mathf.Min(dt, LinkStallCredit);
            // the game must keep running unfocused in MP (vanilla only sets this from the settings at boot, so an SP "pause on focus
            // lost" setting froze the whole match whenever that player alt-tabbed)
            if (!Application.runInBackground) { Application.runInBackground = true; BtcLog.Info("Net", "runInBackground forced on for the MP session"); }

            if (NetworkServer.active)
            {
                foreach (KeyValuePair<int, Peer> kv in s.peers) kv.Value.silence += credit;
            }
            else s.hostPeer.silence += credit;

            if (rt < s.lastHeartbeatSendTime + LinkSendInterval) return;
            float step = s.lastHeartbeatSendTime > 0f ? Mathf.Min(rt - s.lastHeartbeatSendTime, 1f) : LinkSendInterval;
            s.lastHeartbeatSendTime = rt;
            SendHeartbeat(m, s);
            s.frameMax = 0f;
            if (NetworkServer.active) HostPolicy(m, s, rt, step);
            else ClientWatch(m, s, rt);
        }

#if DEV
        internal static float DevMuteUntil;   // scenario "hbmute N": stop sending heartbeats (one-way loss test)
#endif

        private static void SendHeartbeat(UNetManager m, State s)
        {
#if DEV
            if (Time.realtimeSinceStartup < DevMuteUntil) return;
#endif
            _hb.flags = SelfLoading(m, s) ? HbLoading : (byte)0;
            _hb.stallMs = Clamp16(s.frameMax * 1000f);
            if (NetworkServer.active)
            {
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    if (c == null || IsLocalConnection(c)) continue;
                    Peer p;
                    _hb.hearDs = Clamp16((s.peers.TryGetValue(c.connectionId, out p) ? p.silence : 0f) * 10f);
                    c.SendUnreliable(MsgHeartbeat, _hb);
                }
            }
            else if (m.LocalNetworkClient != null && m.LocalNetworkClient.isConnected)
            {
                _hb.hearDs = Clamp16(s.hostPeer.silence * 10f);
                m.LocalNetworkClient.SendUnreliable(MsgHeartbeat, _hb);
            }
        }

        private static void OnHeartbeatReceived(UNetManager m, NetworkMessage message)
        {
            State s = Get(m);
            HeartbeatMessage hb = null;
            try { hb = message.ReadMessage<HeartbeatMessage>(); } catch (Exception) { }
            Peer p;
            if (NetworkServer.active)
            {
                if (message.conn == null) return;
                p = PeerOf(s, message.conn.connectionId);
                BtcNetCheck.HeartbeatFrom(message.conn.connectionId);
            }
            else
            {
                p = s.hostPeer;
                BtcNetCheck.HeartbeatFrom(-1);
            }
            p.silence = 0f;
            p.seen = true;
            if (hb != null)
            {
                p.loading = (hb.flags & HbLoading) != 0;
                p.hear = hb.hearDs * 0.1f;
                p.stall = hb.stallMs * 0.001f;
                if (p.stall > 1.5f) BtcLog.Info("Net", "peer " + (message.conn != null ? message.conn.connectionId : -1) + " reported an own frame of " + p.stall.ToString("0.0") + " s");
            }
        }

        // ------------------------------------------------------------------ host: who blocks the game
        private static void HostPolicy(UNetManager m, State s, float rt, float step)
        {
            if (InLobby(m) || BtcReplay.Mode == BtcReplay.State.Playing)
            {
                if (s.hardPauseActive) SetHard(m, s, false, null);
                return;
            }
            bool selfLoading = SelfLoading(m, s);
            bool ownStall = rt - s.ownStallAt < LinkOwnStallIgnore;
            StringBuilder sb = null;
            int blocking = 0;
            // prune peers of closed connections
            _tmpIds.Clear();
            foreach (KeyValuePair<int, Peer> kv in s.peers)
            {
                NetworkConnection c = ConnById(kv.Key);
                if (c == null || !c.isConnected) _tmpIds.Add(kv.Key);
            }
            for (int i = 0; i < _tmpIds.Count; i++) s.peers.Remove(_tmpIds[i]);

            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                NetworkConnection c = NetworkServer.connections[i];
                if (c == null || IsLocalConnection(c) || BtcSpectate.IsSpec(c)) continue;
                if (IsPendingRejoin(s, c.connectionId)) continue;
                Peer p = PeerOf(s, c.connectionId);
                bool silent = p.silence > LinkPauseAfter;
                // one stale report (sent before the peer processed our queued heartbeats after its own freeze) must not count:
                // deaf only if the reports say so for a whole second
                if (p.seen && p.hear > LinkPauseAfter && !ownStall) p.deafFor += step; else p.deafFor = 0f;
                bool deaf = p.deafFor >= 1f;
                bool bad = !selfLoading && !p.loading && (silent || deaf);
                string name = ResolvePlayerName(m, s, c);
                if (bad)
                {
                    if (!p.hold)
                    {
                        p.badFor = 0f;
                        p.why = silent ? "silent" : "deaf";
                        BtcLog.Warn("Net", "link: " + name + " " + p.why + " (silence " + p.silence.ToString("0.0") + " s, hears host " + p.hear.ToString("0.0") + " s ago, last frame " + p.stall.ToString("0.00") + " s)");
                    }
                    p.hold = true;
                    p.badFor += step;
                    p.healthy = 0f;
                }
                else if (p.hold)
                {
                    p.healthy += step;
                    // short drain after the link is back so the backlog arrives before the game runs again
                    float need = Mathf.Clamp(0.75f + p.badFor * 0.1f, 0.75f, 4f);
                    if (p.healthy >= need)
                    {
                        p.hold = false;
                        s.continueWithout.Remove(name);
                        BtcLog.Info("Net", "link: " + name + " back after " + p.badFor.ToString("0.0") + " s");
                    }
                }
                if (!p.hold || s.continueWithout.Contains(name)) continue;
                blocking++;
                if (sb == null) sb = new StringBuilder();
                else sb.Append('\n');
                bool fine = !bad;
                sb.Append(fine ? T2(name + ": link is back, resuming", name + ": связь вернулась, продолжаем")
                    : (p.why == "deaf" ? T2(name + " does not receive data", name + " не получает данные")
                    : T2(name + " is not responding", name + " не отвечает")));
            }
            // dropped players waiting for a reconnect
            for (int i = 0; i < s.pendingReconnectUsers.Count; i++)
            {
                User u = s.pendingReconnectUsers[i];
                string name = UserName(u);
                if (s.continueWithout.Contains(name)) continue;
                blocking++;
                if (sb == null) sb = new StringBuilder();
                else sb.Append('\n');
                sb.Append(T2(name + " disconnected, waiting", name + " отключился, ждём"));
            }
            if (BtcSpectate.StashedCount > 0 && s.continueWithout.Count == 0)
            {
                blocking++;
                if (sb == null) sb = new StringBuilder();
                else sb.Append('\n');
                sb.Append(T2("A player is reconnecting", "Игрок переподключается"));
            }
            FallbackRejoins(m, s, rt);
            bool want = blocking > 0 && !selfLoading;
            string text = want ? sb.ToString() : null;
            if (want != s.hardPauseActive || text != s.hardText) SetHard(m, s, want, text);
            if ((s.hardPauseActive || s.voluntaryPauseActive) && rt >= s.lastPauseBroadcast + PauseRebroadcast) BroadcastPauseState(s);
        }

        private static string T2(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static string UserName(User u)
        {
            if (u == null) return "?";
            try { if (!string.IsNullOrEmpty(u.ScreenName)) return u.ScreenName; } catch (Exception) { }
            return u.name;
        }

        private static NetworkConnection ConnById(int id)
        {
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                NetworkConnection c = NetworkServer.connections[i];
                if (c != null && c.connectionId == id) return c;
            }
            return null;
        }

        /// <summary>Host: change the link pause (applied locally, sent to everybody).</summary>
        private static void SetHard(UNetManager m, State s, bool on, string text)
        {
            bool changed = on != s.hardPauseActive;
            s.hardText = on ? text : null;
            if (changed)
            {
                float rt = Time.realtimeSinceStartup;
                if (on) { s.hardSince = rt; s.pauseCount++; BtcLog.Warn("Net", "link pause ON: " + (text ?? "").Replace('\n', ';')); }
                else
                {
                    if (s.hardSince > 0f) s.pausedTotal += rt - s.hardSince;
                    BtcLog.Info("Net", "link pause OFF after " + (rt - s.hardSince).ToString("0.0") + " s (session: " + s.pauseCount + " pauses, " + s.pausedTotal.ToString("0") + " s)");
                }
                s.hardPauseActive = on;
                RecomputeEffectivePause(s);
            }
            if (NetworkServer.active) BroadcastPauseState(s);
        }

        /// <summary>Host button: stop waiting for everybody who blocks the game right now (they can still come back later).</summary>
        internal static void ContinueWithoutBlocking()
        {
            UNetManager m = Current;
            State s = Peek(m);
            if (s == null || !NetworkServer.active) return;
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                NetworkConnection c = NetworkServer.connections[i];
                Peer p;
                if (c != null && s.peers.TryGetValue(c.connectionId, out p) && p.hold) s.continueWithout.Add(ResolvePlayerName(m, s, c));
            }
            for (int i = 0; i < s.pendingReconnectUsers.Count; i++) s.continueWithout.Add(UserName(s.pendingReconnectUsers[i]));
            BtcLog.Info("Net", "host continues without: " + string.Join(", ", new List<string>(s.continueWithout).ToArray()));
            try { BtcSpectate.CancelWaitingFromHost(); } catch (Exception e) { BtcLog.Warn("Net", "spectate cancel: " + e.Message); }
            SetHard(m, s, false, null);
        }

        internal static bool LinkPauseActive
        {
            get { State s = Peek(Current); return BtcNetworkGate.Enabled && s != null && s.hardPauseActive; }
        }

        /// <summary>BtcSpectate (legacy reflection target "connectionDegraded"): any peer currently holds the game.</summary>
        internal static bool LinkDegraded
        {
            get
            {
                State s = Peek(Current);
                if (s == null) return false;
                foreach (KeyValuePair<int, Peer> kv in s.peers) if (kv.Value.hold) return true;
                return false;
            }
        }

        /// <summary>BtcSpectate (legacy reflection target "pendingReconnectUsers").</summary>
        internal static List<User> PendingUsers
        {
            get { State s = Peek(Current); return s != null ? s.pendingReconnectUsers : null; }
        }

        // ------------------------------------------------------------------ client
        private static void ClientWatch(UNetManager m, State s, float rt)
        {
            Peer h = s.hostPeer;
            bool warn = h.seen && h.silence > LinkPauseAfter && !InLobby(m);
            if (warn != s.clientLinkWarn)
            {
                s.clientLinkWarn = warn;
                if (warn) BtcLog.Warn("Net", "link: host silent " + h.silence.ToString("0.0") + " s (no local pause, the host decides)");
                else BtcLog.Info("Net", "link: host heard again");
            }
            if (h.seen && h.silence > LinkDeadAfter && !s.reconnectRecoveryInProgress && !InLobby(m) && NetLive(m, s) && !m.calledDisconnect)
            {
                BtcLog.Warn("Net", "link: host silent " + h.silence.ToString("0") + " s, reconnecting");
                h.silence = 0f;
                h.seen = false;
                StartClientReconnectLoop(m, s);
            }
            ApplyClientPause(m, s);
        }

        /// <summary>Client: the host's pause state, plus a local freeze only while our own transport is reconnecting.</summary>
        private static void ApplyClientPause(UNetManager m, State s)
        {
            bool live = NetLive(m, s) && !InLobby(m);
            bool hard = live && (s.hostHard || s.reconnectRecoveryInProgress);
            if (hard == s.hardPauseActive) return;
            s.hardPauseActive = hard;
            RecomputeEffectivePause(s);
        }

        // ------------------------------------------------------------------ rejoin (re-attach by name)
        private static bool IsPendingRejoin(State s, int connId)
        {
            return s.awaitingRejoin.ContainsKey(connId);
        }

        private static void OnRejoinReceived(UNetManager m, NetworkMessage message)
        {
            if (!NetworkServer.active || message.conn == null) return;
            State s = Get(m);
            RejoinMessage rm = message.ReadMessage<RejoinMessage>();
            int id = message.conn.connectionId;
            if (!s.awaitingRejoin.Remove(id)) { BtcLog.Info("Net", "rejoin from conn " + id + " '" + rm.name + "' but nobody waits for it"); return; }
            User user = null;
            for (int i = 0; i < s.pendingReconnectUsers.Count; i++)
                if (UserName(s.pendingReconnectUsers[i]) == rm.name) { user = s.pendingReconnectUsers[i]; break; }
            if (user == null && s.pendingReconnectUsers.Count == 1) user = s.pendingReconnectUsers[0];
            if (user == null) { BtcLog.Warn("Net", "rejoin '" + rm.name + "': no matching dropped player"); return; }
            Reattach(m, s, message.conn, user, "by name");
        }

        /// <summary>A connection that never says its name (or the message got lost) gets the oldest dropped player after a few seconds.</summary>
        private static void FallbackRejoins(UNetManager m, State s, float rt)
        {
            if (s.awaitingRejoin.Count == 0) return;
            _tmpIds.Clear();
            foreach (KeyValuePair<int, float> kv in s.awaitingRejoin) if (rt - kv.Value > RejoinFallback) _tmpIds.Add(kv.Key);
            for (int i = 0; i < _tmpIds.Count; i++)
            {
                s.awaitingRejoin.Remove(_tmpIds[i]);
                NetworkConnection c = ConnById(_tmpIds[i]);
                if (c == null || !c.isConnected || s.pendingReconnectUsers.Count == 0) continue;
                Reattach(m, s, c, s.pendingReconnectUsers[0], "fallback (no name)");
            }
        }

        private static void Reattach(UNetManager m, State s, NetworkConnection conn, User user, string how)
        {
            s.pendingReconnectUsers.Remove(user);
            string name = UserName(user);
            s.continueWithout.Remove(name);
            try
            {
                // the old connection of this player may still be open (1 h transport timeout): close it
                NetworkConnection old = user.connectionToClient;
                if (old != null && old != conn && old.connectionId != conn.connectionId && old.isConnected) { try { old.Disconnect(); } catch (Exception) { } }
            }
            catch (Exception) { }
            BtcLog.Info("Net", "Server: re-attaching returning player " + name + " to connection " + conn.connectionId + " (" + how + ").");
            BtcHud.Notice(BtcReplayUI.T(name + " reconnected", name + " вернулся"), BtcReplayIcons.Check, BtcHud.Ok);
            NetworkServer.ReplacePlayerForConnection(conn, user.gameObject, 0);
            NetworkServer.SetClientReady(conn);
            user.NetworkReady = true;
            Peer p = PeerOf(s, conn.connectionId);
            p.silence = 0f;
            p.hold = false;
        }

        private static void SendRejoin(UNetManager m, State s)
        {
            try
            {
                if (m.LocalNetworkClient == null || !m.LocalNetworkClient.isConnected) return;
                string name = !string.IsNullOrEmpty(s.myName) ? s.myName : m.LocalPlayerName;
                m.LocalNetworkClient.Send(MsgRejoin, new RejoinMessage { name = name ?? "" });
                BtcLog.Info("Net", "rejoin sent as '" + name + "'");
            }
            catch (Exception e) { BtcLog.Warn("Net", "rejoin send: " + e.Message); }
        }

        // ------------------------------------------------------------------ host leaves on purpose
        internal static void AnnounceSessionEnd(UNetManager m)
        {
            State s = Peek(m);
            if (s == null || !s.bcSession || !NetworkServer.active || !BtcNetworkGate.Enabled) return;
            try
            {
                EmptyMessage e = new EmptyMessage();
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    if (c != null && !IsLocalConnection(c)) { c.Send(MsgSessionEnd, e); c.FlushChannels(); }
                }
                BtcLog.Info("Net", "session end sent to clients");
            }
            catch (Exception e) { BtcLog.Warn("Net", "session end: " + e.Message); }
        }

        private static void OnSessionEndReceived(UNetManager m)
        {
            State s = Get(m);
            s.hostEnded = true;
            BtcLog.Info("Net", "host ended the session: no reconnect");
        }

        internal static bool HostEnded(UNetManager m)
        {
            State s = Peek(m);
            return s != null && s.hostEnded;
        }
    }
}
