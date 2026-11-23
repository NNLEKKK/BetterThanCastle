using System;
using System.Collections.Generic;
using Brix.Network;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// Own reliable transport (BC ON + Network/OwnTransport on both sides, negotiated per connection).
    /// Why: the UNET reliable channel gives up after ~5-10 s without a single ack (100% loss) and the host drops the link with
    /// BadMessage, while 99% loss lives forever; and everything reliable shares one ordered pipe, so one lost packet holds HP,
    /// deaths, orders, terrain, files and chat alike until the LLAPI resend.
    /// How: every packet HLAPI hands to NetworkConnection.TransportSend for channel 0 (ReliableSequenced) is taken over: its
    /// messages are split into lanes (0 world, 1 terrain deblocs, 2 bulk files) and each lane is an ordered byte stream sent in
    /// chunks over the UNET Unreliable channel as mod message 128 frames with our own sequence numbers, cumulative + selective acks,
    /// RTT-based resends and an unbounded send queue. Nothing ever gives up: while the peer is silent only a probe goes out per
    /// second (and the unreliable channel is muted, so LLAPI sees almost nothing), and when it is heard again the queues drain.
    /// Switch-over: client hello 129 -> host sends marker 130 on the old channel and from then sends through lanes; the client,
    /// having read 130 (everything older has been delivered), answers with its own 130 and switches too. Lane data is only
    /// delivered after the peer's 130 was read.
    /// </summary>
    internal static class BtcOwnTransport
    {
        internal const short MsgFrame = 128, MsgHello = 129, MsgSwitch = 130;
        private const int Proto = 1;
        private const int Lanes = 3;
        private const int ChunkMax = 1100;
        private const int Window = 512;           // chunks in flight per lane
        private const float SilentAfter = 1f;     // nothing heard from the peer -> probe mode
        private const float ProbeEvery = 1f;
        private const int RtoMinMs = 40, RtoMaxMs = 1000;
        private const int ResendsPerTick = 64;
        private const int Reliable = 0, Unreliable = 1;

        internal static bool Wanted
        {
            get
            {
#if DEV
                if (Environment.GetEnvironmentVariable("BTC_OWNNET") == "1") return BtcNetworkGate.Enabled;   // lab override
#endif
                return BtcNetworkGate.Enabled && BtcConfig.OwnTransport != null && BtcConfig.OwnTransport.Value;
            }
        }

        // ------------------------------------------------------------------ per connection state
        private sealed class Chunk
        {
            internal uint Seq;
            internal byte[] Data;
            internal int Len;
            internal int SentAt;   // ms
            internal int Sends;
            internal bool Acked;
        }

        private sealed class OutLane
        {
            internal byte[] Buf = new byte[4096];
            internal int Len;                       // bytes not yet chunked
            internal uint NextSeq;
            internal readonly List<Chunk> Flight = new List<Chunk>();   // in seq order
        }

        private sealed class InLane
        {
            internal uint Next;                     // next expected seq
            internal readonly Dictionary<uint, byte[]> Ooo = new Dictionary<uint, byte[]>();
            internal byte[] Buf = new byte[4096];
            internal int Len;                       // stream bytes waiting for a complete message (or for the switch)
            internal byte[] Frag = new byte[4096];  // HLAPI fragments joined until the end fragment
            internal int FragLen;
        }

        private sealed class Link
        {
            internal NetworkConnection Conn;
            internal bool SendOn, RecvOn;           // sending through lanes / peer switched (deliver lane data)
            internal bool SwitchPending;            // send marker 130 when channel 0 is idle, then switch
            internal bool MarkerSent;
            internal readonly OutLane[] Out = new OutLane[Lanes];
            internal readonly InLane[] In = new InLane[Lanes];
            internal float LastHeard;
            internal float NextProbe;
            internal bool AckDue;
            internal int EchoTs, EchoAt;            // last frame stamp from the peer and when it arrived
            internal int Srtt = 100, Rttvar = 50;
            internal long BytesOut, BytesIn, Resent, Frames;
            internal Link() { for (int i = 0; i < Lanes; i++) { Out[i] = new OutLane(); In[i] = new InLane(); } }
            internal bool Silent { get { return Time.realtimeSinceStartup - LastHeard > SilentAfter; } }
            internal int Rto { get { int r = Srtt + 4 * Rttvar; return r < RtoMinMs ? RtoMinMs : (r > RtoMaxMs ? RtoMaxMs : r); } }
        }

        private static readonly Dictionary<NetworkConnection, Link> Links = new Dictionary<NetworkConnection, Link>();
        private static readonly List<Link> LinkList = new List<Link>();
        private static readonly byte[] Frame = new byte[ChunkMax + 128];
        private static byte[] _deliver = new byte[4096];
        private static int _deliverErrs;
        private static System.Reflection.FieldInfo _channels, _pending;
        private static bool _init, _initOk;

        private static bool Init()
        {
            if (_init) return _initOk;
            _init = true;
            try
            {
                _channels = AccessTools.Field(typeof(NetworkConnection), "m_Channels");
                _pending = AccessTools.Field(AccessTools.TypeByName("UnityEngine.Networking.ChannelBuffer"), "m_PendingPackets");
                if (_channels == null || _pending == null) throw new Exception("HLAPI channel fields not found");
                _initOk = true;
            }
            catch (Exception e) { BtcLog.Error("OwnNet", "init: " + e.Message); }
            return _initOk;
        }

        private static int Ms { get { return Environment.TickCount; } }

        /// <summary>The connection runs on our transport (both directions switched).</summary>
        internal static bool ActiveFor(NetworkConnection c)
        {
            Link l;
            return c != null && Links.TryGetValue(c, out l) && l.SendOn && l.RecvOn;
        }

        // ------------------------------------------------------------------ session wiring (BtcNetManager, BC ON only)
        internal static void OnServerStarted(UNetManager m)
        {
            Reset();
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                Reg(MsgHello, OnHello, true);
                Reg(MsgSwitch, OnSwitch, true);
                Reg(MsgFrame, OnFrame, true);
                Runner(m);
            }
            catch (Exception e) { BtcLog.Error("OwnNet", "server: " + e.Message); }
        }

        internal static void OnClientConnected(UNetManager m)
        {
            if (NetworkServer.active || !BtcNetworkGate.Enabled) return;
            try
            {
                NetworkClient c = m.LocalNetworkClient;
                if (c == null || c.connection == null) return;
                Reset();
                Reg(MsgSwitch, OnSwitch, false);
                Reg(MsgFrame, OnFrame, false);
                Runner(m);
                if (!Wanted || !Init()) { BtcLog.Info("OwnNet", "client: own transport off (config)"); return; }
                Link l = Add(c.connection);
                l.LastHeard = Time.realtimeSinceStartup;
                c.Send(MsgHello, new IntegerMessage(Proto));
                BtcLog.Info("OwnNet", "client: own transport requested");
            }
            catch (Exception e) { BtcLog.Error("OwnNet", "client: " + e.Message); }
        }

        private static void Reg(short id, NetworkMessageDelegate h, bool server)
        {
            if (server)
            {
                if (NetworkServer.handlers.ContainsKey(id)) NetworkServer.UnregisterHandler(id);
                NetworkServer.RegisterHandler(id, h);
            }
            else
            {
                NetworkClient c = UNetManager.instance != null ? UNetManager.instance.LocalNetworkClient : null;
                if (c == null) return;
                if (c.handlers.ContainsKey(id)) c.UnregisterHandler(id);
                c.RegisterHandler(id, h);
            }
        }

        private static void Runner(UNetManager m)
        {
            if (m != null && m.GetComponent<BtcOwnTransportRunner>() == null) m.gameObject.AddComponent<BtcOwnTransportRunner>();
        }

        private static void Reset()
        {
            Links.Clear();
            LinkList.Clear();
        }

        private static Link Add(NetworkConnection c)
        {
            Link l;
            if (Links.TryGetValue(c, out l)) return l;
            l = new Link { Conn = c, LastHeard = Time.realtimeSinceStartup };
            Links[c] = l;
            LinkList.Add(l);
            return l;
        }

        private static void OnHello(NetworkMessage nm)
        {
            try
            {
                IntegerMessage im = nm.ReadMessage<IntegerMessage>();
                if (nm.conn == null || im == null || im.value < Proto) return;
                if (!Wanted || !Init()) { BtcLog.Info("OwnNet", "host: conn " + nm.conn.connectionId + " asked for the own transport, off here (config)"); return; }
                if (Links.ContainsKey(nm.conn)) return;
                Link l = Add(nm.conn);
                l.SwitchPending = true;
                BtcLog.Info("OwnNet", "host: conn " + nm.conn.connectionId + " switches to the own transport");
            }
            catch (Exception e) { BtcLog.Error("OwnNet", "hello: " + e.Message); }
        }

        /// <summary>The peer's marker arrived on the old channel: everything it sent before is delivered, lane data may flow now.</summary>
        private static void OnSwitch(NetworkMessage nm)
        {
            try
            {
                Link l;
                if (nm.conn == null || !Links.TryGetValue(nm.conn, out l)) return;
                l.RecvOn = true;
                for (int i = 0; i < Lanes; i++) Deliver(l, i);
                if (!NetworkServer.active && !l.SendOn && !l.SwitchPending) l.SwitchPending = true;   // client answers and switches
                BtcLog.Info("OwnNet", (NetworkServer.active ? "host: conn " + nm.conn.connectionId : "client") + " peer switched");
            }
            catch (Exception e) { BtcLog.Error("OwnNet", "switch: " + e.Message); }
        }

        /// <summary>Send our marker on the old channel as soon as it has nothing pending, then route channel 0 through lanes.</summary>
        private static void TrySwitch(Link l)
        {
            Array ch = _channels.GetValue(l.Conn) as Array;
            if (ch == null || ch.Length < 2) { l.SwitchPending = false; return; }
            if (!l.MarkerSent)
            {
                if (Pending(ch) > 0) return;
                l.Conn.Send(MsgSwitch, new EmptyMessage());
                l.Conn.FlushChannels();
                l.MarkerSent = true;
            }
            if (Pending(ch) > 0) return;   // the marker is still in the old pipe: everything sent from now must come after it
            l.SwitchPending = false;
            l.SendOn = true;
            BtcLog.Info("OwnNet", (NetworkServer.active ? "host: conn " + l.Conn.connectionId : "client") + " sends through the own transport");
        }

        private static int Pending(Array ch)
        {
            System.Collections.ICollection q = _pending.GetValue(ch.GetValue(Reliable)) as System.Collections.ICollection;
            return q != null ? q.Count : 0;
        }

        // ------------------------------------------------------------------ send side (NetworkConnection.TransportSend prefix)
        /// <summary>true = handled here (the packet is queued or dropped), false = vanilla send.</summary>
        internal static bool TakeSend(NetworkConnection c, byte[] bytes, int n, int channelId)
        {
            if (LinkList.Count == 0) return false;
            Link l;
            if (!Links.TryGetValue(c, out l)) return false;
            if (channelId == Unreliable) return l.SendOn && l.Silent;   // nothing unreliable into a dead link
            if (channelId != Reliable || !l.SendOn) return false;
            int p = 0;
            while (p + 4 <= n)
            {
                int size = bytes[p] | (bytes[p + 1] << 8);
                short type = (short)(bytes[p + 2] | (bytes[p + 3] << 8));
                int total = 4 + size;
                if (p + total > n) total = n - p;
                Append(l.Out[LaneOf(type)], bytes, p, total);
                p += total;
            }
            Pump(l);
            return true;
        }

        private static int LaneOf(short type)
        {
            switch (type)
            {
                case 51: case 52: case 53: return 1;                                  // terrain deblocs
                case 48: case 49: case 50: case 63:                                   // file transfer (maps, snapshots)
                case 68: case 69: case 95: case 96: case 112: case 113: return 2;     // replay chunks, plate images, terraform shapes
                default: return 0;
            }
        }

        private static void Append(OutLane o, byte[] src, int at, int n)
        {
            if (o.Len + n > o.Buf.Length)
            {
                int cap = o.Buf.Length;
                while (cap < o.Len + n) cap *= 2;
                Array.Resize(ref o.Buf, cap);
            }
            Buffer.BlockCopy(src, at, o.Buf, o.Len, n);
            o.Len += n;
        }

        /// <summary>Chunk queued bytes and send what the window allows; resend overdue chunks.</summary>
        private static void Pump(Link l)
        {
            bool silent = l.Silent;
            int now = Ms;
            int budget = ResendsPerTick;
            for (int li = 0; li < Lanes; li++)
            {
                OutLane o = l.Out[li];
                if (!silent)
                {
                    int rto = l.Rto;
                    for (int i = 0; i < o.Flight.Count && budget > 0; i++)
                    {
                        Chunk k = o.Flight[i];
                        if (k.Acked || now - k.SentAt < rto * (k.Sends > 3 ? 2 : 1)) continue;
                        if (SendChunk(l, li, k, now)) { l.Resent++; budget--; } else return;
                    }
                }
                int off = 0;
                while (o.Len - off > 0 && o.Flight.Count < Window && !silent)
                {
                    int n = Math.Min(ChunkMax, o.Len - off);
                    Chunk k = new Chunk { Seq = o.NextSeq++, Data = new byte[n], Len = n };
                    Buffer.BlockCopy(o.Buf, off, k.Data, 0, n);
                    off += n;
                    o.Flight.Add(k);
                    if (!SendChunk(l, li, k, now)) { k.SentAt = now - RtoMaxMs; break; }   // transport full: the resend loop retries
                }
                if (off > 0)
                {
                    Buffer.BlockCopy(o.Buf, off, o.Buf, 0, o.Len - off);
                    o.Len -= off;
                }
            }
            if (l.AckDue) SendAck(l);
        }

        private static bool SendChunk(Link l, int lane, Chunk k, int now)
        {
            int p = Header(l, 1);
            Frame[p++] = (byte)lane;
            W32(Frame, ref p, k.Seq);
            Frame[p++] = (byte)k.Len; Frame[p++] = (byte)(k.Len >> 8);
            Buffer.BlockCopy(k.Data, 0, Frame, p, k.Len);
            p += k.Len;
            if (!Put(l, p)) return false;
            k.SentAt = now;
            k.Sends++;
            return true;
        }

        private static void SendAck(Link l)
        {
            int p = Header(l, 2);
            Put(l, p);
        }

        /// <summary>HLAPI message header (len, type 128) + kind, stamp, echo, acks of every lane.</summary>
        private static int Header(Link l, byte kind)
        {
            int p = 4;
            Frame[p++] = kind;
            W32(Frame, ref p, (uint)Ms);
            W32(Frame, ref p, (uint)l.EchoTs);
            int hold = l.EchoTs == 0 ? 0 : Mathf.Clamp(Ms - l.EchoAt, 0, 65535);
            Frame[p++] = (byte)hold; Frame[p++] = (byte)(hold >> 8);
            for (int i = 0; i < Lanes; i++)
            {
                InLane il = l.In[i];
                W32(Frame, ref p, il.Next);
                uint mask = 0u;
                if (il.Ooo.Count > 0)
                    for (int b = 0; b < 32; b++) if (il.Ooo.ContainsKey(il.Next + 1u + (uint)b)) mask |= 1u << b;
                W32(Frame, ref p, mask);
            }
            l.AckDue = false;
            return p;
        }

        private static bool Put(Link l, int len)
        {
            int size = len - 4;
            Frame[0] = (byte)size; Frame[1] = (byte)(size >> 8);
            Frame[2] = (byte)MsgFrame; Frame[3] = (byte)(MsgFrame >> 8);
            byte err;
            bool ok = NetworkTransport.Send(l.Conn.hostId, l.Conn.connectionId, Unreliable, Frame, len, out err);
            if (!ok || err != 0) return false;
            l.BytesOut += len;
            l.Frames++;
            return true;
        }

        private static void W32(byte[] b, ref int p, uint v) { b[p++] = (byte)v; b[p++] = (byte)(v >> 8); b[p++] = (byte)(v >> 16); b[p++] = (byte)(v >> 24); }
        private static uint R32(NetworkReader r) { return r.ReadUInt32(); }

        // ------------------------------------------------------------------ receive side
        /// <summary>NetworkConnection.TransportReceive prefix: anything from the peer counts as "heard".</summary>
        internal static void Heard(NetworkConnection c)
        {
            if (LinkList.Count == 0) return;
            Link l;
            if (Links.TryGetValue(c, out l)) l.LastHeard = Time.realtimeSinceStartup;
        }

        private static void OnFrame(NetworkMessage nm)
        {
            try
            {
                Link l;
                if (nm.conn == null || !Links.TryGetValue(nm.conn, out l)) return;
                NetworkReader r = nm.reader;
                byte kind = r.ReadByte();
                int stamp = (int)R32(r);
                int echo = (int)R32(r);
                int hold = r.ReadUInt16();
                if (echo != 0)
                {
                    int rtt = Ms - echo - hold;
                    if (rtt >= 0 && rtt < 10000)
                    {
                        int d = rtt - l.Srtt;
                        l.Srtt += d / 8;
                        l.Rttvar += ((d < 0 ? -d : d) - l.Rttvar) / 4;
                    }
                }
                l.EchoTs = stamp == 0 ? 1 : stamp;
                l.EchoAt = Ms;
                for (int i = 0; i < Lanes; i++) Acked(l.Out[i], R32(r), R32(r));
                if (kind == 1)
                {
                    int lane = r.ReadByte();
                    uint seq = R32(r);
                    int len = r.ReadUInt16();
                    byte[] data = r.ReadBytes(len);
                    l.BytesIn += len;
                    if (lane < Lanes) Received(l, lane, seq, data);
                    l.AckDue = true;
                }
            }
            catch (Exception e) { BtcLog.Error("OwnNet", "frame: " + e.Message); }
        }

        private static void Acked(OutLane o, uint next, uint mask)
        {
            List<Chunk> f = o.Flight;
            int drop = 0;
            while (drop < f.Count && (int)(f[drop].Seq - next) < 0) drop++;
            if (drop > 0) f.RemoveRange(0, drop);
            if (mask == 0u) return;
            for (int i = 0; i < f.Count; i++)
            {
                int b = (int)(f[i].Seq - next) - 1;
                if (b < 0) continue;
                if (b >= 32) break;
                if ((mask & (1u << b)) != 0) f[i].Acked = true;
            }
        }

        private static void Received(Link l, int lane, uint seq, byte[] data)
        {
            InLane il = l.In[lane];
            int ahead = (int)(seq - il.Next);
            if (ahead < 0) return;                                   // duplicate
            if (ahead > 0) { if (ahead < Window * 2 && !il.Ooo.ContainsKey(seq)) il.Ooo[seq] = data; return; }
            Take(il, data);
            il.Next++;
            byte[] more;
            while (il.Ooo.TryGetValue(il.Next, out more)) { il.Ooo.Remove(il.Next); Take(il, more); il.Next++; }
            Deliver(l, lane);
        }

        private static void Take(InLane il, byte[] d)
        {
            if (il.Len + d.Length > il.Buf.Length)
            {
                int cap = il.Buf.Length;
                while (cap < il.Len + d.Length) cap *= 2;
                Array.Resize(ref il.Buf, cap);
            }
            Buffer.BlockCopy(d, 0, il.Buf, il.Len, d.Length);
            il.Len += d.Length;
        }

        /// <summary>Hand every complete message of a lane to HLAPI as if it came on the reliable channel.</summary>
        private static void Deliver(Link l, int lane)
        {
            if (!l.RecvOn) return;
            InLane il = l.In[lane];
            int p = 0;
            while (il.Len - p >= 4)
            {
                int total = 4 + (il.Buf[p] | (il.Buf[p + 1] << 8));
                if (il.Len - p < total) break;
                if (_deliver.Length < total) _deliver = new byte[Math.Max(total, _deliver.Length * 2)];
                Buffer.BlockCopy(il.Buf, p, _deliver, 0, total);
                p += total;
                // HLAPI fragments (msg 17: state byte, then ushort length + bytes; state 1 = end) of a message bigger than one packet:
                // channel 0's own fragment buffer throws when fed from here, so the lane joins them and hands over the whole message
                if (total >= 5 && (_deliver[2] | (_deliver[3] << 8)) == 17)
                {
                    if (_deliver[4] == 0)
                    {
                        int fl = total >= 7 ? Math.Min(_deliver[5] | (_deliver[6] << 8), total - 7) : 0;
                        if (il.Frag.Length < il.FragLen + fl) Array.Resize(ref il.Frag, Math.Max(il.FragLen + fl, il.Frag.Length * 2));
                        Buffer.BlockCopy(_deliver, 7, il.Frag, il.FragLen, fl);
                        il.FragLen += fl;
                        continue;
                    }
                    int whole = il.FragLen;
                    il.FragLen = 0;
                    if (whole == 0) continue;
                    try { l.Conn.TransportReceive(il.Frag, whole, Reliable); }
                    catch (Exception e) { BtcLog.Error("OwnNet", "deliver lane " + lane + " fragmented: " + e.Message); }
                    if (!Links.ContainsKey(l.Conn)) return;
                    continue;
                }
                try { l.Conn.TransportReceive(_deliver, total, Reliable); }
                catch (Exception e)
                {
                    MoonSharp.Interpreter.InterpreterException ie = e as MoonSharp.Interpreter.InterpreterException;
                    string lua = ie != null && ie.DecoratedMessage != null ? " lua: " + ie.DecoratedMessage : "";
                    BtcLog.Error("OwnNet", "deliver lane " + lane + " msg " + (_deliver[2] | (_deliver[3] << 8)) + lua + ": " + (_deliverErrs++ < 3 ? e.ToString() : e.Message));
                }
                if (!Links.ContainsKey(l.Conn)) return;              // the message closed the connection
            }
            if (p > 0)
            {
                Buffer.BlockCopy(il.Buf, p, il.Buf, 0, il.Len - p);
                il.Len -= p;
            }
        }

        // ------------------------------------------------------------------ tick (after HLAPI flushed this frame)
        private static float _statAt;

        internal static void Tick()
        {
            if (LinkList.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            for (int i = LinkList.Count - 1; i >= 0; i--)
            {
                Link l = LinkList[i];
                if (l.Conn == null || !l.Conn.isConnected || (NetworkServer.active && !NetworkServer.connections.Contains(l.Conn)))
                {
                    BtcLog.Info("OwnNet", "link closed (" + (l.Conn != null ? "conn " + l.Conn.connectionId : "null") + ")");
                    LinkList.RemoveAt(i);
                    if (l.Conn != null) Links.Remove(l.Conn);
                    continue;
                }
                try
                {
                    if (l.SwitchPending) TrySwitch(l);
                    if (!l.SendOn) continue;
                    if (l.Silent)
                    {
                        if (now >= l.NextProbe) { l.NextProbe = now + ProbeEvery; Probe(l); }
                    }
                    else Pump(l);
                }
                catch (Exception e) { BtcLog.Error("OwnNet", "tick: " + e.Message); }
            }
            if (now >= _statAt)
            {
                if (_statAt > 0f)
                    for (int i = 0; i < LinkList.Count; i++)
                    {
                        Link l = LinkList[i];
                        if (!l.SendOn) continue;
                        int q = 0, f = 0;
                        for (int k = 0; k < Lanes; k++) { q += l.Out[k].Len; f += l.Out[k].Flight.Count; }
                        BtcLog.Info("OwnNet", "30 s conn " + l.Conn.connectionId + ": out=" + (l.BytesOut / 1024) + " KB in=" + (l.BytesIn / 1024) + " KB frames=" + l.Frames + " resent=" + l.Resent
                            + " rtt=" + l.Srtt + "+-" + l.Rttvar + " ms flight=" + f + " queued=" + q + (l.Silent ? " SILENT" : ""));
                        l.BytesOut = 0; l.BytesIn = 0; l.Frames = 0; l.Resent = 0;
                    }
                _statAt = now + 30f;
            }
        }

        /// <summary>Silent peer: one frame per second (the oldest unacked chunk, or acks only) to find out when it is back.</summary>
        private static void Probe(Link l)
        {
            int now = Ms;
            for (int li = 0; li < Lanes; li++)
            {
                List<Chunk> f = l.Out[li].Flight;
                for (int i = 0; i < f.Count; i++)
                    if (!f[i].Acked) { SendChunk(l, li, f[i], now); return; }
            }
            SendAck(l);
        }
    }

    internal sealed class BtcOwnTransportRunner : MonoBehaviour
    {
        private void LateUpdate()
        {
            try { BtcOwnTransport.Tick(); }
            catch (Exception e) { BtcLog.Error("OwnNet", "runner: " + e.Message); }
        }
    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(NetworkConnection), "TransportSend")]
    internal static class NetworkConnectionTransportSendPatch
    {
        private static bool Prefix(NetworkConnection __instance, byte[] bytes, int numBytes, int channelId, ref byte error, ref bool __result)
        {
            try
            {
                if (!BtcOwnTransport.TakeSend(__instance, bytes, numBytes, channelId)) return true;
                error = 0;
                __result = true;
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "NetworkConnection.TransportSend: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(NetworkConnection), "TransportReceive")]
    internal static class NetworkConnectionTransportReceivePatch
    {
        private static void Prefix(NetworkConnection __instance) { BtcOwnTransport.Heard(__instance); }
    }
}
