#if DEV
using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Text;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV only. Two-instance multiplayer automation + network measurement. Roles via env BTC_ROLE=host|client.
    /// Commands (see BtcDevCmd): host, join, waitclients, waitlobby, ready, startmatch, waitfile.
    /// Sampler (1 Hz): mod ping (msg 70/71), transport RTT, heartbeat age, connection transitions, "Unknown message ID" log counter.
    /// Net line (prof.txt / engine.txt / agent.log): per measure window. Sync line: every 10 s of WALL clock (both instances share the PC clock,
    /// so t=&lt;unixsec/10&gt; is the same instant) -> agent.log "Sync t= units= hash= h1= c=" for scripts/mp-compare.ps1.
    /// </summary>
    internal sealed class BtcDevNet : MonoBehaviour
    {
        private struct Samp { public float T; public float Mod; public float Tr; public float Hb; }
        private sealed class Base { public float T; public int InM, InB, OutM, OutB, Unknown, Disc, Hard, Start; public int N; }

        private static BtcDevNet _i;
        private static readonly List<Samp> Ring = new List<Samp>();
        private static readonly Dictionary<string, Base> Bases = new Dictionary<string, Base>();
        private static int _unknown, _disc, _hard, _conns;
        private static bool _lastConn, _lastHard;
        private static int _hostStage;       // 0 none, 1 start, 2 wait lobby player, 3 published
        private static string _hMap, _hMode, _hPreset;
        private static float _hStageT;
        private static bool _hostReadySent;
        private static long _lastBucket;
        private float _next;

        internal static string Role { get { return Environment.GetEnvironmentVariable("BTC_ROLE") ?? string.Empty; } }
        internal static bool HostPublished { get { return _hostStage == 3; } }

        internal static void Init()
        {
            if (_i != null) return;
            // env BTC_BC=0: run this instance with BetterConnection OFF without touching the user config file (DEV A/B vs vanilla wire).
            if (Environment.GetEnvironmentVariable("BTC_BC") == "0" && BtcConfig.BetterConnection != null) { BtcConfig.BetterConnection.ConfigFile.SaveOnConfigSet = false; BtcConfig.BetterConnection.Value = false; }
            GameObject go = new GameObject("BtcDevNet");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _i = go.AddComponent<BtcDevNet>();
            Application.logMessageReceived += OnLog;
            BtcLog.Info("MP", "armed role=" + (Role.Length == 0 ? "-" : Role) + " logdir=" + BtcDevPaths.LogDir);
        }

        private static void OnLog(string cond, string stack, LogType t)
        {
            if (cond != null && cond.IndexOf("Unknown message ID", StringComparison.Ordinal) >= 0) _unknown++;
        }

        // ---------------- helpers ----------------
        internal static string RepoLogRoot()
        {
            DirectoryInfo d = new DirectoryInfo(BtcDevPaths.LogDir);
            while (d != null && !string.Equals(d.Name, "BTClogs", StringComparison.OrdinalIgnoreCase)) d = d.Parent;
            return d != null ? d.FullName : BtcDevPaths.LogDir;
        }

        internal static string ResolvePath(string p)
        {
            if (Path.IsPathRooted(p)) return p;
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(RepoLogRoot()) ?? string.Empty, p));
        }

        private static int Port() { int p = 0; try { p = UNetManager.HostingPort; } catch (Exception) { } return p > 0 ? p : 7777; }

        // ---------------- host ----------------
        internal static void Host(string map, string mode, string preset)
        {
            _hMap = map; _hMode = mode; _hPreset = preset; _hostStage = 1; _hStageT = Time.realtimeSinceStartup;
            try { File.Delete(Path.Combine(BtcDevPaths.LogDir, "host-ready.txt")); } catch (Exception) { }
        }

        private void HostTick()
        {
            if (_hostStage == 1)
            {
                UNetManager m = UNetManager.instance;
                int port = Port();
                try { UNetManager.SetHostingPort(port); } catch (Exception) { }
                m.LocalPlayerName = "BtcHost";
                m.maxConnections = UNetManager.HostingMaxConnections;
                NetworkClient c = m.StartHostMultiplayer(UNetManager.HostingIP, port);
                BtcLog.Info("MP", "host started client=" + (c != null) + " server=" + NetworkServer.active + " port=" + port + " bc=" + BtcNetworkGate.Enabled);
                _hostStage = 2; _hStageT = Time.realtimeSinceStartup;
            }
            else if (_hostStage == 2)
            {
                UNetManager m = UNetManager.instance;
                if (m.LocalLobbyPlayer == null)
                {
                    if (Time.realtimeSinceStartup - _hStageT > 60f) { BtcLog.Error("MP", "host: no local lobby player after 60 s"); _hostStage = 0; }
                    return;
                }
                if (!BtcDevMatch.Pick(_hMap, _hMode, _hPreset, Brix.Assets.AssetPlayableContext.Multiplayer)) { _hostStage = 0; return; }
                try { GameParam.UpdateParameters(); } catch (Exception e) { BtcLog.Error("MP", "UpdateParameters: " + e.Message); }
                File.WriteAllText(Path.Combine(BtcDevPaths.LogDir, "host-ready.txt"), "127.0.0.1 " + Port());
                BtcLog.Info("MP", "host lobby listening, marker written; faction=" + m.LocalLobbyPlayer.factionNumber);
                _hostStage = 3;
            }
        }

        internal static int RemoteConns()
        {
            int n = 0;
            try
            {
                if (!NetworkServer.active) return 0;
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    if (c != null && c.isConnected && c.hostId >= 0 && c.address != "btc-replay" && !BtcNetManager.IsLocalConnection(c)) n++;
                }
            }
            catch (Exception) { }
            return n;
        }

        internal static bool ClientsReady(int n)
        {
            if (RemoteConns() < n) return false;
            UNetManager m = UNetManager.instance;
            return m != null && m.LobbyPlayerList.Count >= n + 1;
        }

        // ---------------- client ----------------
        internal static void Join(string ip, string port)
        {
            UNetManager m = UNetManager.instance;
            int p = Port();
            int pp; if (!string.IsNullOrEmpty(port) && int.TryParse(port, out pp)) p = pp;
            m.LocalPlayerName = "BtcClient";
            NetworkClient c = m.ConnectToServer(string.IsNullOrEmpty(ip) ? "127.0.0.1" : ip, p);
            BtcLog.Info("MP", "join " + ip + ":" + p + " client=" + (c != null) + " bc=" + BtcNetworkGate.Enabled);
        }

        internal static bool LobbyReady()
        {
            UNetManager m = UNetManager.instance;
            return m != null && m.LocalLobbyPlayer != null && GameParam.Map != null && GameParam.Gamemode != null;
        }

        /// <summary>Client: send ready once the host marked the lobby ready (and the map is present).</summary>
        internal static bool ClientReady()
        {
            UNetManager m = UNetManager.instance;
            if (m == null || m.LocalLobbyPlayer == null || GameParam.Map == null) return false;
            bool hostReady = false;
            foreach (LobbyPlayer p in m.LobbyPlayerList) if (p != null && p.isHost && p.IsHostReady) hostReady = true;
            if (!hostReady || m.LocalLobbyPlayer.downloadProgression != -1f) return false;
            m.LocalLobbyPlayer.SendReadyToBeginMessage();
            BtcLog.Info("MP", "client ready sent");
            return true;
        }

        /// <summary>Host: mark host ready (lobby locked, like the ready button), wait for all clients, then press start.</summary>
        internal static bool StartMatch()
        {
            UNetManager m = UNetManager.instance;
            if (m == null || m.LocalLobbyPlayer == null) return false;
            LobbyPlayer me = m.LocalLobbyPlayer;
            if (!_hostReadySent)
            {
                _hostReadySent = true;
                me.NetworkIsHostReady = true;
                m.AbortUploads();
                m.SendMapToClients();
                BtcLog.Info("MP", "host ready set");
            }
            int others = 0, ready = 0;
            foreach (LobbyPlayer p in m.LobbyPlayerList) { if (p == null || p.isHost) continue; others++; if (p.readyToBegin) ready++; }
            if (others == 0 || ready < others) return false;
            me.SendReadyToBeginMessage();
            BtcLog.Info("MP", "host start pressed clients=" + others + " factions=" + FactionsText(m));
            return true;
        }

        private static string FactionsText(UNetManager m)
        {
            StringBuilder sb = new StringBuilder();
            foreach (LobbyPlayer p in m.LobbyPlayerList) if (p != null) sb.Append(p.playerName).Append('=').Append(p.factionNumber).Append(' ');
            return sb.ToString().Trim();
        }

        // ---------------- sampler / sync ----------------
        private void Update()
        {
            try { if (_hostStage == 1 || _hostStage == 2) HostTick(); } catch (Exception e) { BtcLog.Error("MP", "host: " + e.Message); _hostStage = 0; }
            float now = Time.realtimeSinceStartup;
            if (now >= _next) { _next = now + 1f; try { Sample(now); } catch (Exception e) { BtcLog.Warn("MP", "sample: " + e.Message); } }
            long bucket = (long)Math.Floor((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds / 10.0);
            if (bucket != _lastBucket)
            {
                _lastBucket = bucket;
                try { SyncLine(bucket); } catch (Exception e) { BtcLog.Warn("MP", "sync: " + e.Message); }
            }
        }

        private static void Sample(float now)
        {
            UNetManager m = UNetManager.instance;
            if (m == null) return;
            BtcNetManager.State s = BtcNetManager.Peek(m);
            Samp sm; sm.T = now; sm.Mod = -1f; sm.Tr = -1f; sm.Hb = -1f;
            bool conn = false;
            if (NetworkServer.active)
            {
                int prevC = _conns; _conns = RemoteConns(); conn = _conns > 0;
                if (_conns != prevC) { StringBuilder cs = new StringBuilder(); for (int i = 0; i < NetworkServer.connections.Count; i++) { NetworkConnection c = NetworkServer.connections[i]; if (c != null) cs.Append(c.GetType().Name).Append("#").Append(c.connectionId).Append("/h").Append(c.hostId).Append("/").Append(c.address).Append(" "); } BtcLog.Info("MP", "remote conns " + prevC + "->" + _conns + " : " + cs); }
                float sum = 0f; int n = 0, cnt = 0; float trMax = -1f;
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    if (c == null || c.hostId < 0 || c.address == "btc-replay" || BtcNetManager.IsLocalConnection(c)) continue;
                    float v;
                    if (s != null && s.pingMsByConnection.TryGetValue(c.connectionId, out v)) { sum += v; n++; }
                    trMax = Math.Max(trMax, Rtt(c));
                    BtcNetManager.Peer pr;
                    if (s != null && s.peers.TryGetValue(c.connectionId, out pr)) sm.Hb = Math.Max(sm.Hb, pr.silence);
                    cnt++;
                }
                if (n > 0) sm.Mod = sum / n;
                sm.Tr = trMax;
            }
            else if (m.LocalNetworkClient != null)
            {
                conn = m.LocalNetworkClient.isConnected;
                if (s != null && s.pingMsToHost >= 0f) sm.Mod = s.pingMsToHost;
                sm.Tr = Rtt(m.LocalNetworkClient.connection);
                if (s != null && s.hostPeer.seen) sm.Hb = s.hostPeer.silence;
            }
            if (_lastConn && !conn) { _disc++; BtcLog.Warn("MP", "connection lost (disc#" + _disc + ")"); }
            if (!_lastConn && conn) BtcLog.Info("MP", "connection up");
            _lastConn = conn;
            bool hard = s != null && (s.hardPauseActive || s.connectionDegraded);
            if (hard && !_lastHard) { _hard++; BtcLog.Warn("MP", "hard pause / degraded #" + _hard); }
            _lastHard = hard;
            if (conn) { Ring.Add(sm); if (Ring.Count > 7200) Ring.RemoveRange(0, 1200); }
        }

        /// <summary>Scenario "netdrop": cut the transport connection (client: to the host; host: every remote client) like a real drop.</summary>
        internal static void Drop()
        {
            try
            {
                byte err;
                if (NetworkServer.active)
                {
                    for (int i = 0; i < NetworkServer.connections.Count; i++)
                    {
                        NetworkConnection c = NetworkServer.connections[i];
                        if (c == null || c.hostId < 0 || BtcNetManager.IsLocalConnection(c)) continue;
                        NetworkTransport.Disconnect(c.hostId, c.connectionId, out err);
                        BtcLog.Info("MP", "netdrop conn " + c.connectionId + " err=" + err);
                    }
                }
                else
                {
                    UNetManager m = UNetManager.instance;
                    NetworkConnection c = m != null && m.LocalNetworkClient != null ? m.LocalNetworkClient.connection : null;
                    if (c == null) { BtcLog.Warn("MP", "netdrop: no client connection"); return; }
                    NetworkTransport.Disconnect(c.hostId, c.connectionId, out err);
                    BtcLog.Info("MP", "netdrop client err=" + err);
                    // a local Disconnect raises no event here: deliver the disconnect like a real drop (UNET MsgType.Disconnect)
                    c.InvokeHandlerNoData(MsgType.Disconnect);
                }
            }
            catch (Exception e) { BtcLog.Warn("MP", "netdrop: " + e.Message); }
        }

        private static float Rtt(NetworkConnection c)
        {
            try
            {
                if (c == null) return -1f;
                byte err;
                int r = NetworkTransport.GetCurrentRTT(c.hostId, c.connectionId, out err);
                return err == 0 ? r : -1f;
            }
            catch (Exception) { return -1f; }
        }

        // Transport counters (cumulative, all hosts of this process): in = packets, out = messages/bytes(full: user+system+headers).
        private static void Stats(out int inM, out int inB, out int outM, out int outB)
        {
            inM = inB = outM = outB = 0;
            try
            {
                inM = NetworkTransport.GetIncomingPacketCountForAllHosts(); outM = NetworkTransport.GetOutgoingMessageCount(); outB = NetworkTransport.GetOutgoingFullBytesCount(); inB = NetworkTransport.GetOutgoingPacketCount();
            }
            catch (Exception) { }
        }

        private static string Pct(List<float> a)
        {
            if (a.Count == 0) return "-";
            a.Sort();
            return a[a.Count / 2].ToString("0") + "/" + a[Math.Min(a.Count - 1, (int)(a.Count * 0.99))].ToString("0") + "/" + a[a.Count - 1].ToString("0");
        }

        /// <summary>One "Net ..." line for the window since the last call with the same key; logs it too. Empty if the DEV net sampler is not armed.</summary>
        internal static string Line(string key)
        {
            if (_i == null) return string.Empty;
            float now = Time.realtimeSinceStartup;
            Base b;
            if (!Bases.TryGetValue(key, out b)) { b = new Base(); b.T = BtcDevProf.StartReal; Bases[key] = b; }
            int inM, inB, outM, outB; Stats(out inM, out inB, out outM, out outB);
            double dt = Math.Max(0.5, now - b.T);
            List<float> mod = new List<float>(), tr = new List<float>();
            float hbMax = -1f;
            for (int i = 0; i < Ring.Count; i++)
            {
                if (Ring[i].T < b.T) continue;
                if (Ring[i].Mod >= 0f) mod.Add(Ring[i].Mod);
                if (Ring[i].Tr >= 0f) tr.Add(Ring[i].Tr);
                if (Ring[i].Hb > hbMax) hbMax = Ring[i].Hb;
            }
            bool neg = inM < b.InM || outM < b.OutM;
            string s = "Net role=" + (Role.Length == 0 ? "-" : Role) + " win=" + dt.ToString("0") + "s conns=" + _conns + " rtt_mod(p50/p99/max)=" + Pct(mod) + " rtt_tr=" + Pct(tr)
                + " pkt/s in=" + ((inM - (neg ? 0 : b.InM)) / dt).ToString("0") + " out=" + ((inB - (neg ? 0 : b.InB)) / dt).ToString("0")
                + " out=" + ((outM - (neg ? 0 : b.OutM)) / dt).ToString("0") + "msg/s," + ((outB - (neg ? 0 : b.OutB)) / dt / 1024.0).ToString("0.0") + "KB/s(full)"
                + " unknownMsg=" + (_unknown - b.Unknown) + " disc=" + (_disc - b.Disc) + " hardpause=" + (_hard - b.Hard) + " hbAgeMax=" + hbMax.ToString("0.0") + "s bc=" + BtcNetworkGate.Enabled;
            b.T = now; b.InM = inM; b.InB = inB; b.OutM = outM; b.OutB = outB; b.Unknown = _unknown; b.Disc = _disc; b.Hard = _hard;
            BtcLog.Info("Net", s);
            return s;
        }

        private static void SyncLine(long bucket)
        {
            if (!Architecte.commence) return;
            Labor[] ls = FindObjectsOfType<Labor>();
            uint h = 0, h1 = 0; int n = 0; double cx = 0, cz = 0;
            List<Faction> facs = Faction.GetAllFactions(); int[] fc = new int[(facs != null ? facs.Count : 0) + 1];
            unchecked
            {
                for (int i = 0; i < ls.Length; i++)
                {
                    if (ls[i] == null) continue;
                    Vector3 p = ls[i].transform.position;
                    n++; cx += p.x; cz += p.z;
                    Faction fo = Faction.GetFaction(ls[i].gameObject); int fi = fo != null && facs != null ? facs.IndexOf(fo) : -1; fc[fi < 0 ? fc.Length - 1 : fi]++;
                    h += Mix((int)Math.Round(p.x * 10.0), (int)Math.Round(p.y * 10.0), (int)Math.Round(p.z * 10.0));
                    h1 += Mix((int)Math.Round(p.x), (int)Math.Round(p.y), (int)Math.Round(p.z));
                }
            }
            if (n == 0) return;
            int allL = 0; try { Labor[] al = Resources.FindObjectsOfTypeAll<Labor>(); for (int i = 0; i < al.Length; i++) if (al[i] != null && al[i].gameObject.scene.IsValid()) allL++; } catch (Exception) { }
            int nid = 0; try { nid = NetworkServer.active ? NetworkServer.objects.Count : ClientScene.objects.Count; } catch (Exception) { }
            StringBuilder fs = new StringBuilder();
            for (int i = 0; i < fc.Length; i++) if (fc[i] > 0) fs.Append(fs.Length > 0 ? "," : string.Empty).Append(i == fc.Length - 1 ? "x" : i.ToString()).Append(":").Append(fc[i]);
            BtcLog.Info("Sync", "t=" + bucket + " units=" + n + " hash=" + h.ToString("x8") + " h1=" + h1.ToString("x8")
                + " all=" + allL + " nid=" + nid + " fac=" + fs + " c=" + (cx / n).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "," + (cz / n).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static uint Mix(int x, int y, int z)
        {
            unchecked
            {
                uint a = (uint)x * 73856093u ^ (uint)y * 19349663u ^ (uint)z * 83492791u;
                a ^= a >> 15; a *= 2246822519u; a ^= a >> 13;
                return a;
            }
        }
    }
}
#endif
