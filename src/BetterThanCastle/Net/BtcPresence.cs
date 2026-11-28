using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Brix.Engine;
using Brix.Network;
using Brix.UI.Builder.Menu;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
    /// <summary>
    /// Online presence of mod players (Online tab, corner tab 16): every running game with the feature ON sends a small UDP beacon
    /// (port 47781, its own socket, NOT the UNET game connection) to the directed broadcast address of every network adapter
    /// (Radmin 26.255.255.255, Hamachi 25.255.255.255, LAN x.y.z.255) and by unicast to everyone it already knows. Vanilla LAN discovery
    /// broadcasts to 255.255.255.255 only, which Windows sends out of the primary adapter: VPN-LAN hosts go missing, and its entries
    /// expire after 5 s. Here a peer stays online 15 s after its last beacon and answers new peers directly, so one way through is enough.
    /// Same channel: invites (host -> player, HUD card with a Join button), chat (all online + direct messages, acked + resent),
    /// avatars (64 px PNG in chunks on request, cached on disk). History: everyone met in a lobby or match (configs/online-peers.txt).
    /// BetterConnection does not matter: nothing here touches the game connection; Join = vanilla UNetManager.ConnectToServer by IP.
    /// </summary>
    internal static partial class BtcPresence
    {
        internal const int Proto = 1;
        private const string Magic = "BTCP";
        private const float OnlineFor = 15f, BeaconEvery = 3f, UnicastEvery = 6f, HistoryEvery = 20f;
        private const int MaxPacket = 1400, AvSize = 64, AvChunk = 900;

        internal sealed class Peer
        {
            internal string Id = "", Nick = "", Ver = "", Map = "", Mode = "", Detail = "", Versus = "", ShownIp = "", Av = "";
            internal BtcActivity.Kind Kind;
            internal bool Host, Join, JoinMatch, Bc, NoMod, Met;
            internal int Players, Max, Secs = -1, Port = 3000, PlateA = -1, PlateG = -1, Grad;
            internal IPEndPoint Src;            // where its beacon came from (the path that works): join + unicast use it
            internal string LastIp = "";        // history: last address
            internal float Seen = -1000f;       // Time.unscaledTime of the last beacon (this session)
            internal long LastUtc;              // history: last time seen online (UTC ticks)
            internal float SecsAt;              // when Secs was received (the clock keeps running locally)
            internal bool Online { get { return Time.unscaledTime - Seen < OnlineFor; } }
        }

        internal sealed class Msg
        {
            internal string Mid, From, Nick, Ch, Text;   // Ch = "all" or the id of the other side of a direct chat
            internal long Utc;
            internal bool Mine;
        }

        private sealed class Pending { internal string Mid, To; internal byte[] Data; internal int Tries; internal float Next; }

        internal sealed class Invite { internal string From, Nick, Map, Mode; internal IPEndPoint Src; internal int Port; internal float At; internal bool Gone; }

        // ---- state (main thread only, except _inbox)
        internal static readonly Dictionary<string, Peer> Peers = new Dictionary<string, Peer>();
        internal static readonly List<Msg> Chat = new List<Msg>();
        internal static readonly Dictionary<string, int> Unread = new Dictionary<string, int>();
        internal static readonly List<Invite> Invites = new List<Invite>();
        private static readonly HashSet<string> _seenMids = new HashSet<string>();
        private static readonly List<Pending> _pending = new List<Pending>();
        private static readonly Queue<KeyValuePair<IPEndPoint, byte[]>> _inbox = new Queue<KeyValuePair<IPEndPoint, byte[]>>();
        private static readonly object _lock = new object();

        private static Socket _sock;
        private static Thread _rx;
        private static volatile bool _run;
        private static int _port;
        private static float _nextBeacon, _nextUni, _nextHist, _nextMet, _nextAddr, _nextSig, _saveAt = -1f, _chatSaveAt = -1f, _retryOpen;
        private static string _lastSig = "";
        private static readonly List<IPAddress> _bcast = new List<IPAddress>();
        private static readonly List<IPAddress> _local = new List<IPAddress>();
        private static string _myId;
        private static string _shownIp = "";
        private static bool _loaded;

        // join that waits for the old connection to close
        private static string _joinIp; private static int _joinPort; private static float _joinAt, _joinUntil;

        internal static int OnlineCount
        {
            get { int n = 0; foreach (Peer p in Peers.Values) if (p.Online && !p.NoMod) n++; return n; }
        }

        internal static string MyId { get { EnsureId(); return _myId; } }
        internal static string MyIp { get { return _shownIp; } }
        internal static bool Running { get { return _sock != null; } }
        internal static string Status = "";

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        // ================================================================== config
        internal static bool On { get { return BtcConfig.OnlineOn == null || BtcConfig.OnlineOn.Value; } }

        private static void EnsureId()
        {
            if (!string.IsNullOrEmpty(_myId)) return;
            string v = BtcConfig.OnlineId != null ? BtcConfig.OnlineId.Value : "";
            if (string.IsNullOrEmpty(v) || v.Length < 8) { v = Guid.NewGuid().ToString("N").Substring(0, 16); if (BtcConfig.OnlineId != null) BtcConfig.OnlineId.Value = v; }
            _myId = v;
        }

        // ================================================================== lifecycle
        /// <summary>ResourcePackManager.Update, every frame.</summary>
        internal static void Tick()
        {
            float now = Time.unscaledTime;
            try { BtcDuelPvp.Tick(now); } catch (Exception e) { BtcLog.Warn("DuelPvp", "tick: " + e.Message); }
            try
            {
                if (!_loaded) { _loaded = true; LoadHistory(); LoadChat(); }
                if (!On) { if (_sock != null) Close(true); TickJoin(now); return; }
                if (_sock == null && now >= _retryOpen) Open();
                if (_sock == null) return;
                Drain(now);
                if (now >= _nextAddr) { _nextAddr = now + 30f; ScanAsync(); }
                TakeScanned();
                // the signature is a dozen string concatenations: 4 times a second is enough (was every frame)
                bool changed = false;
                if (now >= _nextSig)
                {
                    _nextSig = now + 0.25f;
                    string sig = Signature();
                    changed = sig != _lastSig;
                    if (changed) _lastSig = sig;
                }
                if (changed || now >= _nextBeacon) { _nextBeacon = now + BeaconEvery; Broadcast(); }
                if (changed || now >= _nextUni) { _nextUni = now + UnicastEvery; foreach (Peer p in Peers.Values) if (p.Online && p.Src != null && !p.NoMod) Send(p.Src, Beacon(true)); }
                if (now >= _nextHist) { _nextHist = now + HistoryEvery; PingHistory(); }
                if (now >= _nextMet) { _nextMet = now + 5f; MarkMet(); }
                Resend(now);
                TickAvatar(now);
                TickJoin(now);
                if (_saveAt > 0f && now >= _saveAt) { _saveAt = -1f; SaveHistory(); }
                if (_chatSaveAt > 0f && now >= _chatSaveAt) { _chatSaveAt = -1f; SaveChat(); }
            }
            catch (Exception e) { BtcLog.Error("Presence", "tick: " + e.Message); _retryOpen = now + 30f; Close(false); }
        }

        private static void Open()
        {
            _retryOpen = Time.unscaledTime + 30f;
            _base = BtcConfig.OnlinePort != null ? BtcConfig.OnlinePort.Value : 47781;
            string err = "";
            // a second copy of the game on this PC takes the next port (exclusive: unicast replies must reach the right copy)
            for (int k = 0; k < PortSpan && _sock == null; k++)
            {
                Socket s = null;
                try
                {
                    s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    s.ExclusiveAddressUse = true;
                    s.EnableBroadcast = true;
                    try { s.IOControl(-1744830452, new byte[] { 0 }, null); } catch (Exception) { }   // SIO_UDP_CONNRESET off: an ICMP "port unreachable" must not kill Receive
                    s.Bind(new IPEndPoint(IPAddress.Any, _base + k));
                    _sock = s; _port = _base + k;
                }
                catch (Exception e) { err = e.Message; try { if (s != null) s.Close(); } catch (Exception) { } }
            }
            if (_sock == null)
            {
                Status = Tt("Cannot open UDP port ", "Не удалось открыть UDP порт ") + _base + ": " + err;
                BtcLog.Warn("Presence", "open: " + err);
                return;
            }
            // a second copy on this PC shares the config (same id): give it its own id, else both drop each other as "self"
            EnsureId();
            if (_port != _base && _myId.IndexOf('-') < 0) _myId = _myId + "-" + (_port - _base);
            _run = true;
            _rx = new Thread(RxLoop); _rx.IsBackground = true; _rx.Name = "BtcPresence"; _rx.Start();
            Addresses();
            _nextBeacon = 0f; _nextUni = 0f; _nextHist = Time.unscaledTime + 2f;
            Status = "";
            BtcLog.Info("Presence", "open port " + _port + " id " + MyId + " bcast " + _bcast.Count + " shown " + _shownIp);
        }

        private const int PortSpan = 4;
        private static int _base = 47781;

        /// <summary>Broadcast / unknown address: every port a copy of the game may sit on.</summary>
        private static void SendAll(IPAddress a, byte[] b) { for (int k = 0; k < PortSpan; k++) if (_base + k != _port || !IsLocal(a)) Send(new IPEndPoint(a, _base + k), b); }

        private static bool IsLocal(IPAddress a) { if (IPAddress.IsLoopback(a)) return true; for (int i = 0; i < _local.Count; i++) if (_local[i].Equals(a)) return true; return false; }

        /// <summary>The presence endpoint of the sender of the packet being handled (its port is in every packet).</summary>
        private static IPEndPoint From(IPEndPoint src) { return new IPEndPoint(src.Address, I("pp", _base)); }

        /// <summary>Quit / switch off: tells the others at once (else they keep us for 15 s).</summary>
        internal static void Close(bool sayBye)
        {
            if (sayBye && _sock != null)
            {
                try { byte[] b = Pack("bye", "id", MyId); foreach (IPAddress a in _bcast) SendAll(a, b); foreach (Peer p in Peers.Values) if (p.Online && p.Src != null) Send(p.Src, b); }
                catch (Exception) { }
            }
            _run = false;
            try { if (_sock != null) _sock.Close(); } catch (Exception) { }
            _sock = null;
            if (_saveAt > 0f) SaveHistory();
            if (_chatSaveAt > 0f) SaveChat();
        }

        private static void RxLoop()
        {
            byte[] buf = new byte[2048];
            while (_run)
            {
                try
                {
                    Socket s = _sock;
                    if (s == null) break;
                    EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    int n = s.ReceiveFrom(buf, ref ep);
                    if (n < 6) continue;
                    byte[] copy = new byte[n];
                    Buffer.BlockCopy(buf, 0, copy, 0, n);
                    lock (_lock) { if (_inbox.Count < 512) _inbox.Enqueue(new KeyValuePair<IPEndPoint, byte[]>((IPEndPoint)ep, copy)); }
                }
                catch (SocketException) { if (!_run) break; Thread.Sleep(20); }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { Thread.Sleep(50); }
            }
        }

        // ================================================================== addresses
        private static bool IsVpn(IPAddress a) { byte[] b = a.GetAddressBytes(); return b[0] == 26 || b[0] == 25; }
        private static bool IsPrivate(IPAddress a) { byte[] b = a.GetAddressBytes(); return b[0] == 10 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] >= 16 && b[1] < 32); }

        private static void Addresses() { ApplyAddresses(RawAddresses()); }

        // the adapter walk (GetAllNetworkInterfaces + GetIPProperties) takes tens of ms with VPN adapters: off the main thread every 30 s
        private static List<KeyValuePair<IPAddress, IPAddress>> _scanned;
        private static volatile bool _scanBusy;

        private static void ScanAsync()
        {
            if (_scanBusy) return;
            _scanBusy = true;
            try
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    List<KeyValuePair<IPAddress, IPAddress>> r = null;
                    try { r = RawAddresses(); } catch (Exception) { }
                    lock (_lock) _scanned = r;
                    _scanBusy = false;
                });
            }
            catch (Exception) { _scanBusy = false; }
        }

        private static void TakeScanned()
        {
            List<KeyValuePair<IPAddress, IPAddress>> r;
            lock (_lock) { r = _scanned; _scanned = null; }
            if (r != null) ApplyAddresses(r);
        }

        /// (address, mask) of every IPv4 adapter that is up; mask null = unknown. Any thread.
        private static List<KeyValuePair<IPAddress, IPAddress>> RawAddresses()
        {
            List<KeyValuePair<IPAddress, IPAddress>> r = new List<KeyValuePair<IPAddress, IPAddress>>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    try
                    {
                        if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                        foreach (UnicastIPAddressInformation u in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (u.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(u.Address)) continue;
                            IPAddress mask = null;
                            try { mask = u.IPv4Mask; } catch (Exception) { }
                            r.Add(new KeyValuePair<IPAddress, IPAddress>(u.Address, mask));
                        }
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
            if (r.Count == 0)
            {
                try { foreach (IPAddress a in Dns.GetHostAddresses(Dns.GetHostName())) if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)) r.Add(new KeyValuePair<IPAddress, IPAddress>(a, null)); }
                catch (Exception) { }
            }
            return r;
        }

        private static void ApplyAddresses(List<KeyValuePair<IPAddress, IPAddress>> raw)
        {
            _bcast.Clear(); _local.Clear();
            for (int i = 0; i < raw.Count; i++) { AddLocal(raw[i].Key); AddBcast(raw[i].Key, raw[i].Value); }
            AddUnique(_bcast, IPAddress.Broadcast);
            AddUnique(_bcast, IPAddress.Loopback);   // other games on this PC (labs, two copies)
            // the address others should type: VPN LAN (Radmin / Hamachi) first, then a private LAN address
            _shownIp = "";
            foreach (IPAddress a in _local) if (IsVpn(a)) { _shownIp = a.ToString(); break; }
            if (_shownIp.Length == 0) foreach (IPAddress a in _local) if (IsPrivate(a)) { _shownIp = a.ToString(); break; }
            if (_shownIp.Length == 0 && _local.Count > 0) _shownIp = _local[0].ToString();
            string manual = BtcConfig.OnlineMyIp != null ? BtcConfig.OnlineMyIp.Value.Trim() : "";
            if (manual.Length > 0) _shownIp = manual;
        }

        private static void AddLocal(IPAddress a) { AddUnique(_local, a); }

        private static void AddBcast(IPAddress a, IPAddress mask)
        {
            byte[] b = a.GetAddressBytes();
            byte[] m = mask != null ? mask.GetAddressBytes() : null;
            if (m == null || m.Length != 4 || (m[0] == 0 && m[1] == 0 && m[2] == 0 && m[3] == 0))
                m = (b[0] == 26 || b[0] == 25 || b[0] == 10) ? new byte[] { 255, 0, 0, 0 } : new byte[] { 255, 255, 255, 0 };
            byte[] r = new byte[4];
            for (int i = 0; i < 4; i++) r[i] = (byte)(b[i] | ~m[i]);
            AddUnique(_bcast, new IPAddress(r));
        }

        private static void AddUnique(List<IPAddress> l, IPAddress a) { for (int i = 0; i < l.Count; i++) if (l[i].Equals(a)) return; l.Add(a); }

        // ================================================================== packets
        private static string Esc(string s) { return s == null ? "" : s.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", ""); }
        private static string Unesc(string s) { return s.Replace("\\n", "\n").Replace("\\\\", "\\"); }

        private static byte[] Pack(string type, params string[] kv)
        {
            StringBuilder sb = new StringBuilder(256);
            sb.Append(Magic).Append(Proto).Append(' ').Append(type).Append('\n');
            sb.Append("pp=").Append(_port).Append('\n');   // own presence port: replies come back to this copy of the game
            for (int i = 0; i + 1 < kv.Length; i += 2) sb.Append(kv[i]).Append('=').Append(Esc(kv[i + 1])).Append('\n');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static string Parse(byte[] data, Dictionary<string, string> into)
        {
            into.Clear();
            string s;
            try { s = Encoding.UTF8.GetString(data); } catch (Exception) { return null; }
            if (!s.StartsWith(Magic)) return null;
            int nl = s.IndexOf('\n');
            if (nl < 0) return null;
            string head = s.Substring(0, nl);
            int sp = head.IndexOf(' ');
            if (sp < 0) return null;
            int proto;
            if (!int.TryParse(head.Substring(4, sp - 4), out proto) || proto < 1) return null;
            string[] lines = s.Substring(nl + 1).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int eq = lines[i].IndexOf('=');
                if (eq > 0) into[lines[i].Substring(0, eq)] = Unesc(lines[i].Substring(eq + 1));
            }
            return head.Substring(sp + 1);
        }

        private static void Send(IPEndPoint to, byte[] data)
        {
            Socket s = _sock;
            if (s == null || to == null || data == null || data.Length > MaxPacket) return;
            try { s.SendTo(data, to); } catch (Exception) { }
        }

        private static string Signature()
        {
            BtcActivity.Snap a = BtcActivity.Now;
            return (int)a.Kind + "|" + a.Map + "|" + a.Mode + "|" + a.Detail + "|" + a.Players + "|" + a.Versus + "|" + Joinable() + "|" + JoinableMatch() + "|" + NickPlain() + "|" + _avHash + "|" + ShowIp;
        }

        private static bool ShowIp { get { return BtcConfig.OnlineShowIp == null || BtcConfig.OnlineShowIp.Value; } }

        private static string NickPlain()
        {
            try { UNetManager m = UNetManager.instance; string n = m != null ? m.LocalPlayerName : null; if (string.IsNullOrEmpty(n)) n = Neo.multiplayerName; return BtcNick.Plain(n ?? "Player"); }
            catch (Exception) { return "Player"; }
        }

        internal static string MyNick { get { return NickPlain(); } }

        /// <summary>The local game takes players right now: hosting, in the lobby, free slots, the switch on.</summary>
        internal static bool Joinable()
        {
            if (BtcConfig.OnlineAllowJoin != null && !BtcConfig.OnlineAllowJoin.Value) return false;
            if (BtcDuelPvp.HostingLobby) return false;   // a duel lobby takes only the invited opponent (spectators join the running duel)
            BtcActivity.Snap a = BtcActivity.Now;
            return a.Host && a.Kind == BtcActivity.Kind.Lobby && (a.Max <= 0 || a.Players < a.Max);
        }

        /// <summary>Hosting a running match with BetterConnection (it takes late joiners as spectators / returning players).</summary>
        internal static bool JoinableMatch()
        {
            if (BtcConfig.OnlineAllowJoin != null && !BtcConfig.OnlineAllowJoin.Value) return false;
            BtcActivity.Snap a = BtcActivity.Now;
            return a.Host && BtcNetworkGate.Enabled && (a.Kind == BtcActivity.Kind.Match || a.Kind == BtcActivity.Kind.Paused || (a.Kind == BtcActivity.Kind.Duel && BtcDuel.Pvp));
        }

        private static int GamePort()
        {
            // UNetManager hides its own networkPort field; the port the host listens on is the base NetworkManager one
            try { if (NetworkServer.active && NetworkServer.listenPort > 0) return NetworkServer.listenPort; } catch (Exception) { }
            try { NetworkManager nm = UNetManager.instance; if (nm != null && nm.networkPort > 0) return nm.networkPort; } catch (Exception) { }
            try { if (UNetManager.HostingPort > 0) return UNetManager.HostingPort; } catch (Exception) { }
            return 3000;
        }

        private static byte[] Beacon(bool reply)
        {
            BtcActivity.Snap a = BtcActivity.Now;
            int pa = -1, pg = -1, grad = 0;
            try { pa = BtcPlate.Get("a"); pg = BtcPlate.Get("g"); grad = BtcPlate.Get("grad"); } catch (Exception) { }
            string det = a.Detail ?? "";
            if (det.Length > 120) det = det.Substring(0, 120);
            return Pack("hi", "id", MyId, "rep", reply ? "1" : "0", "n", Cut(NickPlain(), 64), "v", BtcPlugin.Version,
                "k", ((int)a.Kind).ToString(), "map", Cut(a.Map, 64), "mode", Cut(a.Mode, 40), "det", det, "vs", Cut(a.Versus, 120),
                "h", a.Host ? "1" : "0", "pl", a.Players.ToString(), "mx", a.Max.ToString(), "s", a.Seconds.ToString(),
                "j", Joinable() ? "1" : "0", "jm", JoinableMatch() ? "1" : "0", "bc", BtcNetworkGate.Enabled ? "1" : "0",
                "port", GamePort().ToString(), "ip", ShowIp ? _shownIp : "", "av", _avHash,
                "pa", pa.ToString(), "pg", pg.ToString(), "gr", grad.ToString());
        }

        private static string Cut(string s, int n) { if (s == null) return ""; return s.Length > n ? s.Substring(0, n) : s; }

        private static void Broadcast()
        {
            byte[] b = Beacon(false);
            for (int i = 0; i < _bcast.Count; i++) SendAll(_bcast[i], b);
            string manual = BtcConfig.OnlineManual != null ? BtcConfig.OnlineManual.Value : "";
            if (!string.IsNullOrEmpty(manual))
            {
                string[] parts = manual.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++) { IPAddress ip; if (IPAddress.TryParse(parts[i], out ip)) SendAll(ip, b); }
            }
        }

        /// <summary>Offline people from the history: a direct beacon to their last address (they may sit behind a network that drops broadcasts).</summary>
        private static void PingHistory()
        {
            byte[] b = null;
            int n = 0;
            foreach (Peer p in Peers.Values)
            {
                if (p.Online || p.NoMod || string.IsNullOrEmpty(p.LastIp) || n >= 64) continue;
                IPAddress ip;
                if (!IPAddress.TryParse(p.LastIp, out ip)) continue;
                if (b == null) b = Beacon(false);
                SendAll(ip, b);
                n++;
            }
        }

        // ================================================================== receive
        private static readonly Dictionary<string, string> _kv = new Dictionary<string, string>();

        private static string V(string k) { string v; return _kv.TryGetValue(k, out v) ? v : ""; }
        private static int I(string k, int def) { int v; return int.TryParse(V(k), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : def; }

        private static void Drain(float now)
        {
            for (int guard = 0; guard < 128; guard++)
            {
                KeyValuePair<IPEndPoint, byte[]> it;
                lock (_lock) { if (_inbox.Count == 0) return; it = _inbox.Dequeue(); }
                string type = Parse(it.Value, _kv);
                if (type == null) continue;
                string id = V("id");
                if (id.Length == 0 || id == MyId) continue;
                try { Handle(type, id, it.Key, now); }
                catch (Exception e) { BtcLog.Warn("Presence", type + ": " + e.Message); }
            }
        }

        private static Peer GetPeer(string id, bool create)
        {
            Peer p;
            if (Peers.TryGetValue(id, out p)) return p;
            if (!create) return null;
            p = new Peer(); p.Id = id;
            Peers[id] = p;
            return p;
        }

        private static void Handle(string type, string id, IPEndPoint src, float now)
        {
            switch (type)
            {
                case "hi": OnBeacon(id, src, now); break;
                case "bye": { Peer p = GetPeer(id, false); if (p != null) p.Seen = -1000f; break; }
                case "inv": OnInvite(id, src, now); break;
                case "chat": OnChat(id, src); break;
                case "ack": { string mid = V("mid"); _pending.RemoveAll(x => x.Mid == mid && x.To == id); break; }
                case "avq": OnAvatarRequest(src); break;
                case "avd": OnAvatarData(); break;
                default: HandleDuel(type, id, src, now); break;
            }
        }

        private static void OnBeacon(string id, IPEndPoint src, float now)
        {
            Peer p = GetPeer(id, true);
            bool wasOnline = p.Online;
            p.Nick = V("n"); p.Ver = V("v");
            p.Kind = (BtcActivity.Kind)Mathf.Clamp(I("k", 0), 0, (int)BtcActivity.Kind.Afk);
            p.Map = V("map"); p.Mode = V("mode"); p.Detail = V("det"); p.Versus = V("vs");
            p.Host = V("h") == "1"; p.Join = V("j") == "1"; p.JoinMatch = V("jm") == "1"; p.Bc = V("bc") == "1";
            p.Players = I("pl", 0); p.Max = I("mx", 0); p.Secs = I("s", -1); p.SecsAt = now;
            p.Port = I("port", 3000); p.ShownIp = V("ip"); p.Av = V("av");
            p.PlateA = I("pa", -1); p.PlateG = I("pg", -1); p.Grad = I("gr", 0);
            p.NoMod = false;
            p.Src = From(src);   // its presence port (in every packet), not an ephemeral one
            p.Seen = now;
            p.LastUtc = DateTime.UtcNow.Ticks;
            string ip = src.Address.ToString();
            if (!IPAddress.IsLoopback(src.Address) && p.LastIp != ip) { p.LastIp = ip; if (p.Met) QueueSave(); }
            if (p.Met) QueueSave();
            if (!wasOnline && V("rep") != "1") Send(p.Src, Beacon(true));   // a new peer hears us at once, even if our broadcast never reaches it
            if (!wasOnline) BtcLog.Info("Presence", "online: " + p.Nick + " " + ip + ":" + p.Src.Port + (p.Met ? " (met)" : ""));
        }

        // ================================================================== history (met players)
        private static string HistFile { get { return BtcPaths.Config("online-peers.txt"); } }
        private static void QueueSave() { if (_saveAt < 0f) _saveAt = Time.unscaledTime + 5f; }

        private static readonly List<string> _names = new List<string>();

        /// <summary>Everyone in the current lobby / match becomes "met" (offline list). Players without the mod are kept by nick.</summary>
        private static void MarkMet()
        {
            BtcActivity.CollectNames(_names);
            for (int i = 0; i < _names.Count; i++)
            {
                string nm = _names[i];
                if (string.IsNullOrEmpty(nm)) continue;
                Peer found = null;
                foreach (Peer p in Peers.Values) if (!p.NoMod && p.Online && string.Equals(p.Nick, nm, StringComparison.OrdinalIgnoreCase)) { found = p; break; }
                if (found == null)
                {
                    foreach (Peer p in Peers.Values) if (string.Equals(p.Nick, nm, StringComparison.OrdinalIgnoreCase)) { found = p; break; }
                    if (found == null) { found = GetPeer("nick:" + nm.ToLowerInvariant(), true); found.Nick = nm; found.NoMod = true; }
                }
                found.LastUtc = DateTime.UtcNow.Ticks;
                if (!found.Met) { found.Met = true; BtcLog.Info("Presence", "met " + nm + (found.NoMod ? " (no mod)" : "")); }
                // a mod peer with the same nick supersedes its old no-mod entry
                if (!found.NoMod) { string key = "nick:" + nm.ToLowerInvariant(); if (Peers.ContainsKey(key)) Peers.Remove(key); }
                QueueSave();
            }
        }

        internal static void Forget(string id)
        {
            Peer p = GetPeer(id, false);
            if (p == null) return;
            p.Met = false;
            if (!p.Online) Peers.Remove(id);
            QueueSave();
        }

        /// <summary>Undo of Forget: puts the entry back as it was.</summary>
        internal static void Restore(Peer p)
        {
            if (p == null) return;
            p.Met = true;
            Peers[p.Id] = p;
            QueueSave();
        }

        private static void LoadHistory()
        {
            try
            {
                if (!File.Exists(HistFile)) return;
                string[] lines = File.ReadAllLines(HistFile, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] f = lines[i].Split('\t');
                    if (f.Length < 7 || f[0].Length == 0) continue;
                    Peer p = GetPeer(f[0], true);
                    p.Nick = Unesc(f[1]); p.LastIp = f[2];
                    int port; if (int.TryParse(f[3], out port)) p.Port = port;
                    long t; if (long.TryParse(f[4], out t)) p.LastUtc = t;
                    p.Av = f[5]; p.NoMod = f[6] == "1"; p.Met = true;
                    if (f.Length >= 9) { int a, g; if (int.TryParse(f[7], out a)) p.PlateA = a; if (int.TryParse(f[8], out g)) p.PlateG = g; }
                    if (f.Length >= 10) p.Ver = f[9];
                }
            }
            catch (Exception e) { BtcLog.Warn("Presence", "history load: " + e.Message); }
        }

        private static void SaveHistory()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (Peer p in Peers.Values)
                {
                    if (!p.Met) continue;
                    sb.Append(p.Id).Append('\t').Append(Esc(p.Nick).Replace("\t", " ")).Append('\t').Append(p.LastIp).Append('\t').Append(p.Port).Append('\t')
                      .Append(p.LastUtc).Append('\t').Append(p.Av).Append('\t').Append(p.NoMod ? "1" : "0").Append('\t').Append(p.PlateA).Append('\t').Append(p.PlateG)
                      .Append('\t').Append(p.Ver).Append("\r\n");
                }
                File.WriteAllText(HistFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { BtcLog.Warn("Presence", "history save: " + e.Message); }
        }

        // ================================================================== join + invites
        /// <summary>Connects to a game by address (vanilla direct join). From a lobby the old connection is closed first.</summary>
        internal static bool JoinAddress(string ip, int port, out string why)
        {
            why = null;
            if (SceneManager.GetActiveScene().buildIndex == 2) { why = Tt("Leave your match first", "Сначала выйдите из своего матча"); return false; }
            if (BtcActivity.Now.Kind == BtcActivity.Kind.Loading) { why = Tt("Wait until the load is over", "Дождитесь конца загрузки"); return false; }
            IPAddress a;
            if (!IPAddress.TryParse(ip ?? "", out a)) { why = Tt("No address", "Нет адреса"); return false; }
            _joinIp = ip; _joinPort = port > 0 ? port : 3000;
            _joinUntil = Time.unscaledTime + 10f;
            _joinAt = Time.unscaledTime;
            if (NetworkServer.active || NetworkClient.active)
            {
                try { UNetManager.instance.Disconnect(); } catch (Exception e) { BtcLog.Warn("Presence", "leave lobby: " + e.Message); }
                _joinAt = Time.unscaledTime + 1.5f;
            }
            BtcLog.Info("Presence", "join " + ip + ":" + _joinPort);
            return true;
        }

        internal static bool JoinPeer(Peer p, out string why)
        {
            why = null;
            if (p == null || p.Src == null) { why = Tt("The player is offline", "Игрок не в сети"); return false; }
            return JoinAddress(p.Src.Address.ToString(), p.Port, out why);
        }

        private static void TickJoin(float now)
        {
            if (_joinIp == null || now < _joinAt) return;
            if (now > _joinUntil) { BtcLog.Warn("Presence", "join: the old connection did not close"); _joinIp = null; return; }
            if (NetworkServer.active || NetworkClient.active) return;
            string ip = _joinIp; int port = _joinPort;
            _joinIp = null;
            try
            {
                UNetManager m = UNetManager.instance;
                if (m == null) return;
                NetworkClient c = m.ConnectToServer(ip, port);
                if (c != null) { try { NetworkWaitingModalMenu.OpenAsClientSignal.Invoke(); } catch (Exception) { } }
            }
            catch (Exception e) { BtcLog.Error("Presence", "join: " + e.Message); }
        }

        internal static void SendInvite(Peer p)
        {
            if (p == null || p.Src == null) return;
            BtcActivity.Snap a = BtcActivity.Now;
            byte[] b = Pack("inv", "id", MyId, "n", Cut(NickPlain(), 64), "port", GamePort().ToString(), "map", Cut(a.Map, 64), "mode", Cut(a.Mode, 40), "iid", Guid.NewGuid().ToString("N").Substring(0, 8));
            for (int i = 0; i < 3; i++) Send(p.Src, b);   // the receiver drops the copies (same sender, 10 s)
            BtcLog.Info("Presence", "invite -> " + p.Nick);
        }

        private static void OnInvite(string id, IPEndPoint src, float now)
        {
            if (BtcConfig.OnlineInvites != null && !BtcConfig.OnlineInvites.Value) return;
            for (int i = 0; i < Invites.Count; i++) if (Invites[i].From == id && now - Invites[i].At < 10f) return;
            Invites.RemoveAll(x => x.From == id);
            Invite inv = new Invite { From = id, Nick = V("n"), Map = V("map"), Mode = V("mode"), Port = I("port", 3000), Src = From(src), At = now };
            Invites.Add(inv);
            BtcSfx.Open();
            BtcHud.Notice(Tt(inv.Nick + " invites you to the game", inv.Nick + " приглашает вас в игру"), BtcReplayIcons.Group, BtcHud.Ok);
            BtcLog.Info("Presence", "invite from " + inv.Nick);
        }

        /// <summary>OnGUI (every scene): invite cards at the top with a Join button. 60 s each.</summary>
        internal static void DrawHud()
        {
            DrawDuelHud();
            if (Invites.Count == 0 || Event.current == null) return;
            float now = Time.unscaledTime;
            bool inMatch = SceneManager.GetActiveScene().buildIndex == 2;   // nothing to join from a match: the notice + the Online tab list are enough
            for (int i = Invites.Count - 1; i >= 0; i--)
            {
                Invite inv = Invites[i];
                if (inv.Gone || now - inv.At > 60f) { Invites.RemoveAt(i); continue; }
                if (inMatch) continue;
                string id = "btcinv:" + inv.From;
                string where = (inv.Mode ?? "") + (string.IsNullOrEmpty(inv.Map) ? "" : (string.IsNullOrEmpty(inv.Mode) ? "" : " · ") + inv.Map);
                Invite cur = inv;
                bool x = BtcHud.Card(id, BtcReplayIcons.Group, BtcHud.Ok, Tt(inv.Nick + " invites you", inv.Nick + " зовёт вас в игру"),
                    (where.Length > 0 ? where + ". " : "") + Tt("Join the lobby now? (Online tab: decline)", "Зайти в лобби сейчас? (вкладка Онлайн: отклонить)"), null, false, Tt("Join", "Зайти"),
                    delegate
                    {
                        string why;
                        if (JoinAddress(cur.Src.Address.ToString(), cur.Port, out why)) { cur.Gone = true; BtcSfx.Ok(); }
                        else { BtcSfx.Warn(); BtcHud.Notice(why, BtcReplayIcons.Cross, BtcHud.Warn); }
                    });
                if (!x) BtcHud.ForceOpen(id);   // not dismissable (the 5 s auto-close must not drop an invite): it lives 60 s, declined in the Online tab
            }
        }

        // ================================================================== chat
        private static string ChatFile { get { return BtcPaths.Config("online-chat.txt"); } }
        private static float _lastSend;
        internal const int MaxChat = 300;

        /// <summary>Sends a chat line to everyone online (ch "all") or to one player (ch = its id). False when throttled / nobody to send to.</summary>
        internal static bool SendChat(string ch, string text, out string why)
        {
            why = null;
            text = (text ?? "").Replace("\n", " ").Replace("\r", " ").Trim();
            if (text.Length == 0) return false;
            if (text.Length > MaxChat) text = text.Substring(0, MaxChat);
            float now = Time.unscaledTime;
            if (now - _lastSend < 0.4f) { why = Tt("Too fast", "Слишком быстро"); return false; }
            if (_sock == null) { why = Tt("Online is off", "Онлайн выключен"); return false; }
            _lastSend = now;
            string mid = Guid.NewGuid().ToString("N").Substring(0, 12);
            Msg m = new Msg { Mid = mid, From = MyId, Nick = NickPlain(), Ch = ch, Text = text, Utc = DateTime.UtcNow.Ticks, Mine = true };
            AddMsg(m);
            byte[] b = Pack("chat", "id", MyId, "n", Cut(NickPlain(), 64), "mid", mid, "ch", ch == "all" ? "all" : "dm", "t", text);
            foreach (Peer p in Peers.Values)
            {
                if (!p.Online || p.NoMod || p.Src == null) continue;
                if (ch != "all" && p.Id != ch) continue;
                _pending.Add(new Pending { Mid = mid, To = p.Id, Data = b, Tries = 0, Next = 0f });
            }
            if (ch != "all" && _pending.Count == 0) why = Tt("The player is offline: the message waits in your history only", "Игрок не в сети: сообщение осталось только у вас");
            return true;
        }

        private static void Resend(float now)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                Pending pd = _pending[i];
                if (now < pd.Next) continue;
                Peer p = GetPeer(pd.To, false);
                if (p == null || p.Src == null || pd.Tries >= 6) { _pending.RemoveAt(i); continue; }
                Send(p.Src, pd.Data);
                pd.Tries++;
                pd.Next = now + 1.2f;
            }
        }

        private static void OnChat(string id, IPEndPoint src)
        {
            string mid = V("mid");
            if (mid.Length == 0) return;
            Send(From(src), Pack("ack", "id", MyId, "mid", mid));
            if (!_seenMids.Add(mid)) return;
            if (BtcConfig.OnlineChat != null && !BtcConfig.OnlineChat.Value) return;
            string text = V("t");
            if (text.Length > MaxChat) text = text.Substring(0, MaxChat);
            string ch = V("ch") == "dm" ? id : "all";
            Msg m = new Msg { Mid = mid, From = id, Nick = V("n"), Ch = ch, Text = text, Utc = DateTime.UtcNow.Ticks, Mine = false };
            AddMsg(m);
            bool looking = BtcOnlineTab.Watching(ch);
            if (!looking)
            {
                int n; Unread.TryGetValue(ch, out n); Unread[ch] = n + 1;
                if (ch != "all" || (BtcConfig.OnlineChatNotify != null && BtcConfig.OnlineChatNotify.Value))
                {
                    BtcSfx.Tick();
                    BtcHud.Notice(m.Nick + ": " + (text.Length > 70 ? text.Substring(0, 70) + "..." : text), BtcReplayIcons.Chat, BtcHud.Ok);
                }
            }
        }

        /// <summary>One line per known player (DEV "online dump", bug reports).</summary>
        internal static string DumpText()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("me=").Append(MyNick).Append(" port=").Append(_port).Append(" kind=").Append(BtcActivity.Now.Kind).Append(" join=").Append(Joinable() ? 1 : 0)
              .Append(" av=").Append(_avHash).Append(" chat=").Append(Chat.Count).Append(" unread=").Append(UnreadTotal).Append(" invites=").Append(Invites.Count).Append(" pending=").Append(_pending.Count);
            foreach (Peer p in Peers.Values)
                sb.Append(" | ").Append(p.Nick).Append(p.Online ? " ON " : " off ").Append(p.Kind).Append(" j=").Append(p.Join ? 1 : 0).Append(p.JoinMatch ? "m" : "")
                  .Append(" src=").Append(p.Src != null ? p.Src.ToString() : p.LastIp).Append(" met=").Append(p.Met ? 1 : 0).Append(p.NoMod ? " nomod" : "")
                  .Append(" av=").Append(string.IsNullOrEmpty(p.Av) ? "-" : (AvatarOf(p.Av) != null ? "ok" : "wait"));
            return sb.ToString();
        }

        /// <summary>DEV: join the first online peer that takes players.</summary>
        internal static bool JoinFirst()
        {
            foreach (Peer p in Peers.Values) if (p.Online && (p.Join || p.JoinMatch)) { string why; return JoinPeer(p, out why); }
            return false;
        }

        /// <summary>DEV: invite every online peer.</summary>
        internal static int InviteAll() { int n = 0; foreach (Peer p in Peers.Values) if (p.Online && !p.NoMod) { SendInvite(p); n++; } return n; }

        /// <summary>DEV: press Join on the newest invite card.</summary>
        internal static bool AcceptInvite()
        {
            if (Invites.Count == 0) return false;
            Invite inv = Invites[Invites.Count - 1];
            string why;
            bool ok = JoinAddress(inv.Src.Address.ToString(), inv.Port, out why);
            if (ok) inv.Gone = true;
            return ok;
        }

        internal static int UnreadTotal { get { int n = 0; foreach (int v in Unread.Values) n += v; return n; } }

        private static void AddMsg(Msg m)
        {
            _seenMids.Add(m.Mid);
            Chat.Add(m);
            if (Chat.Count > 600) Chat.RemoveRange(0, Chat.Count - 500);
            if (_chatSaveAt < 0f) _chatSaveAt = Time.unscaledTime + 3f;
        }

        private static void LoadChat()
        {
            try
            {
                if (!File.Exists(ChatFile)) return;
                string[] lines = File.ReadAllLines(ChatFile, Encoding.UTF8);
                for (int i = Math.Max(0, lines.Length - 500); i < lines.Length; i++)
                {
                    string[] f = lines[i].Split('\t');
                    if (f.Length < 7) continue;
                    long t; long.TryParse(f[5], out t);
                    Msg m = new Msg { Mid = f[0], From = f[1], Nick = Unesc(f[2]), Ch = f[3], Text = Unesc(f[4]), Utc = t, Mine = f[6] == "1" };
                    Chat.Add(m); _seenMids.Add(m.Mid);
                }
            }
            catch (Exception e) { BtcLog.Warn("Presence", "chat load: " + e.Message); }
        }

        private static void SaveChat()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                for (int i = Math.Max(0, Chat.Count - 500); i < Chat.Count; i++)
                {
                    Msg m = Chat[i];
                    sb.Append(m.Mid).Append('\t').Append(m.From).Append('\t').Append(Esc(m.Nick).Replace("\t", " ")).Append('\t').Append(m.Ch).Append('\t')
                      .Append(Esc(m.Text).Replace("\t", " ")).Append('\t').Append(m.Utc).Append('\t').Append(m.Mine ? "1" : "0").Append("\r\n");
                }
                File.WriteAllText(ChatFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { BtcLog.Warn("Presence", "chat save: " + e.Message); }
        }

        /// <summary>Removes the messages of one channel (undo puts them back).</summary>
        internal static List<Msg> ClearChannel(string ch)
        {
            List<Msg> gone = Chat.FindAll(x => x.Ch == ch);
            Chat.RemoveAll(x => x.Ch == ch);
            Unread.Remove(ch);
            if (_chatSaveAt < 0f) _chatSaveAt = Time.unscaledTime + 1f;
            return gone;
        }

        internal static void RestoreChannel(List<Msg> msgs)
        {
            if (msgs == null) return;
            Chat.AddRange(msgs);
            Chat.Sort((a, b) => a.Utc.CompareTo(b.Utc));
            if (_chatSaveAt < 0f) _chatSaveAt = Time.unscaledTime + 1f;
        }

        // ================================================================== avatars
        private static string _avHash = "";
        private static byte[] _avPng;
        private static Texture _avSrc;
        private static float _nextAvCheck;
        private static readonly Dictionary<string, Texture2D> _avCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, byte[][]> _avParts = new Dictionary<string, byte[][]>();
        private static readonly Dictionary<string, float> _avAsked = new Dictionary<string, float>();
        private static readonly Dictionary<string, int> _avTries = new Dictionary<string, int>();
        private static string AvDir { get { return Path.Combine(BtcPaths.Root, Path.Combine("Cache", "online-avatars")); } }

        private static void TickAvatar(float now)
        {
            if (now < _nextAvCheck) return;
            _nextAvCheck = now + 5f;
            Texture2D t = null;
            try { BtcAvatar.Tick(); t = BtcAvatar.LocalTex; } catch (Exception) { }
            if (t != _avSrc)
            {
                _avSrc = t;
                _avPng = t != null ? Shrink(t) : null;
                _avHash = _avPng != null ? Hash(_avPng) : "";
                if (_avPng != null) _avCache[_avHash] = Decode(_avPng);
            }
            // ask for missing avatars of online peers (each hash: 6 tries, 5 s apart)
            foreach (Peer p in Peers.Values)
            {
                if (string.IsNullOrEmpty(p.Av) || !p.Online || p.Src == null || _avCache.ContainsKey(p.Av)) continue;
                if (LoadCached(p.Av)) continue;
                int tries; _avTries.TryGetValue(p.Av, out tries);
                if (tries >= 6) continue;
                float at; if (_avAsked.TryGetValue(p.Av, out at) && now - at < 5f) continue;
                _avAsked[p.Av] = now; _avTries[p.Av] = tries + 1;
                Send(p.Src, Pack("avq", "id", MyId, "av", p.Av));
            }
        }

        /// <summary>Avatar texture of a peer (own avatar for MyId), or null while it is not here yet.</summary>
        internal static Texture2D AvatarOf(string hash)
        {
            if (string.IsNullOrEmpty(hash)) return null;
            Texture2D t;
            if (_avCache.TryGetValue(hash, out t)) return t;
            return LoadCached(hash) ? _avCache[hash] : null;
        }

        internal static string MyAvatar { get { return _avHash; } }

        private static readonly HashSet<string> _noDisk = new HashSet<string>();

        private static bool LoadCached(string hash)
        {
            if (_avCache.ContainsKey(hash)) return true;
            if (_noDisk.Contains(hash) || hash.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            try
            {
                string f = Path.Combine(AvDir, hash + ".png");
                if (!File.Exists(f)) { _noDisk.Add(hash); return false; }
                Texture2D t = Decode(File.ReadAllBytes(f));
                if (t == null) { _noDisk.Add(hash); return false; }
                _avCache[hash] = t;
                return true;
            }
            catch (Exception) { _noDisk.Add(hash); return false; }
        }

        private static Texture2D Decode(byte[] png)
        {
            Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.hideFlags = HideFlags.HideAndDontSave;
            if (!t.LoadImage(png)) { UnityEngine.Object.Destroy(t); return null; }
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        private static byte[] Shrink(Texture src)
        {
            RenderTexture rt = null, old = RenderTexture.active;
            try
            {
                rt = RenderTexture.GetTemporary(AvSize, AvSize, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                Texture2D t = new Texture2D(AvSize, AvSize, TextureFormat.RGBA32, false);
                t.ReadPixels(new Rect(0, 0, AvSize, AvSize), 0, 0);
                t.Apply();
                byte[] png = t.EncodeToPNG();
                UnityEngine.Object.Destroy(t);
                return png != null && png.Length < AvChunk * 30 ? png : null;
            }
            catch (Exception e) { BtcLog.Warn("Presence", "avatar shrink: " + e.Message); return null; }
            finally { RenderTexture.active = old; if (rt != null) RenderTexture.ReleaseTemporary(rt); }
        }

        private static string Hash(byte[] b)
        {
            uint h = 2166136261;
            for (int i = 0; i < b.Length; i++) { h ^= b[i]; h *= 16777619; }
            return h.ToString("x8") + b.Length.ToString("x");
        }

        private static void OnAvatarRequest(IPEndPoint src)
        {
            if (_avPng == null || V("av") != _avHash) return;
            IPEndPoint to = From(src);
            int count = (_avPng.Length + AvChunk - 1) / AvChunk;
            for (int i = 0; i < count; i++)
            {
                int len = Math.Min(AvChunk, _avPng.Length - i * AvChunk);
                string d = Convert.ToBase64String(_avPng, i * AvChunk, len);
                Send(to, Pack("avd", "id", MyId, "av", _avHash, "i", i.ToString(), "c", count.ToString(), "d", d));
            }
        }

        private static void OnAvatarData()
        {
            string hash = V("av");
            int i = I("i", -1), c = I("c", 0);
            if (hash.Length == 0 || c <= 0 || c > 30 || i < 0 || i >= c || _avCache.ContainsKey(hash)) return;
            byte[][] parts;
            if (!_avParts.TryGetValue(hash, out parts) || parts.Length != c) { parts = new byte[c][]; _avParts[hash] = parts; }
            try { parts[i] = Convert.FromBase64String(V("d")); } catch (Exception) { return; }
            for (int k = 0; k < c; k++) if (parts[k] == null) return;
            MemoryStream ms = new MemoryStream();
            for (int k = 0; k < c; k++) ms.Write(parts[k], 0, parts[k].Length);
            byte[] png = ms.ToArray();
            _avParts.Remove(hash);
            if (Hash(png) != hash) return;
            Texture2D t = Decode(png);
            if (t == null) return;
            _avCache[hash] = t;
            _noDisk.Remove(hash);
            try { Directory.CreateDirectory(AvDir); File.WriteAllBytes(Path.Combine(AvDir, hash + ".png"), png); } catch (Exception) { }
        }
    }
}
