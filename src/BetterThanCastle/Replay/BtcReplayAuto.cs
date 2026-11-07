using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Auto replay delivery (BetterConnection ON + host cfg Replay/AutoShare): when the host's multiplayer recording
    /// ends, the .btcr is streamed to every connected client. Header first (idx -1); a client that answers the header
    /// (mod installed, BC ON) gets 1200-byte chunks on the reliable channel, window 48, max 12 chunks per frame per client.
    /// Client acks every 16 chunks; 25 s without progress or a disconnect aborts that client only.
    /// Wire (mod ids, BC ON only): 68 chunk host->client, 69 ack client->host.
    /// </summary>
    internal static class BtcReplayAuto
    {
        internal const short MsgChunk = 68;
        internal const short MsgAck = 69;
        private const int ChunkSize = 1200;
        private const int Window = 48;
        private const int PerFrame = 12;
        private const int AckEvery = 16;
        private const int MaxBytes = 96 * 1024 * 1024;
        private const float Timeout = 25f;

        private sealed class Job
        {
            internal NetworkConnection conn;
            internal int sid;
            internal int stage; // 0 offered, 1 streaming
            internal int sent;
            internal int acked;
            internal float lastProgress;
        }

        // host
        private static string _waitPath;
        private static float _waitSince;
        private static byte[] _data;
        private static string _name;
        private static int _chunks;
        private static int _sidCounter;
        private static readonly List<Job> _jobs = new List<Job>();

        // client
        private static int _rxSid = -1;
        private static string _rxName;
        private static byte[] _rxBuf;
        private static int _rxGot;
        private static int _rxChunks;
        private static string _rxHost;
        private static float _rxLast;

        // deferred Disconnect (leaving the match while a transfer is running keeps the connection alive until it ends)
        private const float DeferCap = 90f;
        private static Brix.Network.UNetManager _pending;
        private static float _pendingSince;
        private static bool _bypass;

        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                if (NetworkServer.active)
                {
                    NetworkServer.UnregisterHandler(MsgAck);
                    NetworkServer.RegisterHandler(MsgAck, OnAckAtHost);
                }
                _rxSid = -1;
                _rxBuf = null;
                Brix.Network.UNetManager m = NetworkManager.singleton as Brix.Network.UNetManager;
                NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
                if (c != null && !NetworkServer.active)
                {
                    c.UnregisterHandler(MsgChunk);
                    c.RegisterHandler(MsgChunk, OnChunkAtClient);
                }
            }
            catch (Exception e) { BtcLog.Error("ReplayShare", "RegisterHandlers: " + e); }
        }

        /// <summary>Called at the end of BtcReplay.EndRecording (host side, any mode); filters itself.</summary>
        internal static void OnRecordingEnded(string path)
        {
            try
            {
                if (!NetworkServer.active || !BtcNetworkGate.Enabled) return;
                if (BtcConfig.ReplayAutoShare == null || !BtcConfig.ReplayAutoShare.Value) return;
                if (string.IsNullOrEmpty(path) || !path.EndsWith("_mp.btcr", StringComparison.OrdinalIgnoreCase)) return;
                _waitPath = path;
                _waitSince = Time.unscaledTime;
                BtcLog.Info("ReplayShare", "queued " + Path.GetFileName(path) + " (waiting for the writer)");
            }
            catch (Exception e) { BtcLog.Error("ReplayShare", "OnRecordingEnded: " + e); }
        }

        private static bool Busy()
        {
            if (_waitPath != null || _jobs.Count > 0) return true;
            return _rxBuf != null && Time.unscaledTime - _rxLast < Timeout;
        }

        /// <summary>UNetManager.Disconnect Prefix: true = postpone the real disconnect until the replay transfer is done (max 90 s).</summary>
        internal static bool DeferDisconnect(Brix.Network.UNetManager m)
        {
            try
            {
                if (_bypass || !BtcNetworkGate.Enabled || m == null) return false;
                if (_pending != null) return true;
                if (NetworkServer.active) BtcReplay.FinishForDisconnect();
                if (!Busy()) return false;
                _pending = m;
                _pendingSince = Time.unscaledTime;
                BtcLog.Info("ReplayShare", "Disconnect postponed until the replay transfer ends");
                BtcToast.Show(BtcReplay.T("Finishing the replay transfer before leaving...", "Дозагружаем реплей перед выходом..."));
                return true;
            }
            catch (Exception e) { BtcLog.Error("ReplayShare", "DeferDisconnect: " + e); return false; }
        }

        private static void RunPendingDisconnect()
        {
            if (_pending == null) return;
            if (Busy() && Time.unscaledTime - _pendingSince < DeferCap) return;
            Brix.Network.UNetManager m = _pending;
            _pending = null;
            BtcLog.Info("ReplayShare", "postponed Disconnect now runs");
            _bypass = true;
            try { m.Disconnect(); }
            catch (Exception e) { BtcLog.Error("ReplayShare", "pending Disconnect: " + e); }
            finally { _bypass = false; }
        }

        /// <summary>Once per frame (RPM OnGUI repaint).</summary>
        internal static void Tick()
        {
            RunPendingDisconnect();
            if (_waitPath == null && _jobs.Count == 0) return;
            try
            {
                if (!NetworkServer.active || !BtcNetworkGate.Enabled)
                {
                    Reset("host stopped or BetterConnection off");
                    return;
                }
                float now = Time.unscaledTime;
                if (_waitPath != null && (!BtcReplay.WriterBusy || now - _waitSince > 30f)) Start(now);
                for (int i = _jobs.Count - 1; i >= 0; i--)
                {
                    Job j = _jobs[i];
                    if (j.conn == null || !j.conn.isConnected || !Contains(j.conn))
                    {
                        BtcLog.Info("ReplayShare", "client " + (j.conn != null ? j.conn.connectionId : -1) + " gone, transfer dropped");
                        _jobs.RemoveAt(i);
                        continue;
                    }
                    if (now - j.lastProgress > Timeout)
                    {
                        BtcLog.Warn("ReplayShare", "client " + j.conn.connectionId + " timeout (" + j.acked + "/" + _chunks + ")");
                        _jobs.RemoveAt(i);
                        continue;
                    }
                    if (j.stage != 1) continue;
                    int n = 0;
                    while (j.sent < _chunks && j.sent - j.acked < Window && n < PerFrame)
                    {
                        int off = j.sent * ChunkSize;
                        int len = Mathf.Min(ChunkSize, _data.Length - off);
                        byte[] part = new byte[len];
                        Buffer.BlockCopy(_data, off, part, 0, len);
                        j.conn.Send(MsgChunk, new ChunkMsg { sid = j.sid, idx = j.sent, total = _data.Length, name = string.Empty, data = part });
                        j.sent++;
                        n++;
                    }
                }
                if (_waitPath == null && _jobs.Count == 0 && _data != null)
                {
                    _data = null;
                    BtcLog.Info("ReplayShare", "all transfers finished");
                }
            }
            catch (Exception e)
            {
                BtcLog.Error("ReplayShare", "Tick: " + e);
                Reset("error");
            }
        }

        private static void Reset(string why)
        {
            if (_jobs.Count > 0 || _waitPath != null) BtcLog.Info("ReplayShare", "reset: " + why);
            _jobs.Clear();
            _waitPath = null;
            _data = null;
        }

        private static bool Contains(NetworkConnection c)
        {
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                if (ReferenceEquals(NetworkServer.connections[i], c)) return true;
            }
            return false;
        }

        private static void Start(float now)
        {
            string path = _waitPath;
            _waitPath = null;
            if (!File.Exists(path)) { BtcLog.Warn("ReplayShare", "file missing: " + path); return; }
            long len = new FileInfo(path).Length;
            if (len <= 0 || len > MaxBytes) { BtcLog.Warn("ReplayShare", "not sent, size " + len + " bytes (cap " + MaxBytes + ")"); return; }
            _jobs.Clear();
            _data = File.ReadAllBytes(path);
            _name = Path.GetFileName(path);
            _chunks = (_data.Length + ChunkSize - 1) / ChunkSize;
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                NetworkConnection c = NetworkServer.connections[i];
                if (c == null || !c.isConnected || BtcNetManager.IsLocalConnection(c)) continue;
                Job j = new Job { conn = c, sid = ++_sidCounter, lastProgress = now };
                _jobs.Add(j);
                c.Send(MsgChunk, new ChunkMsg { sid = j.sid, idx = -1, total = _data.Length, name = _name, data = System.Text.Encoding.UTF8.GetBytes(HostName()) });
            }
            BtcLog.Info("ReplayShare", "offering " + _name + " (" + _data.Length + " bytes, " + _chunks + " chunks) to " + _jobs.Count + " client(s)");
            if (_jobs.Count == 0) _data = null;
        }

        private static void OnAckAtHost(NetworkMessage nm)
        {
            try
            {
                AckMsg a = nm.ReadMessage<AckMsg>();
                for (int i = 0; i < _jobs.Count; i++)
                {
                    Job j = _jobs[i];
                    if (j.sid != a.sid || j.conn == null || nm.conn == null || j.conn.connectionId != nm.conn.connectionId) continue;
                    j.lastProgress = Time.unscaledTime;
                    if (a.status == 1 || a.status == 2 || a.status == 3)
                    {
                        BtcLog.Info("ReplayShare", "client " + j.conn.connectionId + (a.status == 3 ? " received the replay" : a.status == 1 ? " skipped (already has it)" : " failed"));
                        _jobs.RemoveAt(i);
                    }
                    else
                    {
                        j.stage = 1;
                        if (a.next > j.acked) j.acked = Mathf.Min(a.next, _chunks);
                    }
                    return;
                }
            }
            catch (Exception e) { BtcLog.Error("ReplayShare", "OnAckAtHost: " + e); }
        }

        // ------------------------------------------------------------------ client
        private static void Ack(NetworkConnection conn, int sid, int next, int status)
        {
            if (conn != null) conn.Send(MsgAck, new AckMsg { sid = sid, next = next, status = status });
        }

        private static void OnChunkAtClient(NetworkMessage nm)
        {
            ChunkMsg m;
            try { m = nm.ReadMessage<ChunkMsg>(); }
            catch (Exception e) { BtcLog.Error("ReplayShare", "bad chunk: " + e); return; }
            if (NetworkServer.active || !BtcNetworkGate.Enabled) return;
            try
            {
                if (m.idx < 0)
                {
                    string name = SafeName(m.name);
                    if (name == null || m.total <= 0 || m.total > MaxBytes)
                    {
                        Ack(nm.conn, m.sid, 0, 2);
                        return;
                    }
                    if (BtcConfig.ReplayDownloadFromHost != null && !BtcConfig.ReplayDownloadFromHost.Value)
                    {
                        Ack(nm.conn, m.sid, 0, 1);
                        return;
                    }
                    _rxHost = CleanHost(m.data != null && m.data.Length > 0 ? System.Text.Encoding.UTF8.GetString(m.data) : null);
                    string existing = Path.Combine(BtcReplay.ReplaysDir, name);
                    if (File.Exists(existing) && new FileInfo(existing).Length == m.total)
                    {
                        Ack(nm.conn, m.sid, 0, 1);
                        return;
                    }
                    _rxSid = m.sid;
                    _rxName = name;
                    _rxLast = Time.unscaledTime;
                    _rxBuf = new byte[m.total];
                    _rxGot = 0;
                    _rxChunks = (m.total + ChunkSize - 1) / ChunkSize;
                    BtcLog.Info("ReplayShare", "receiving " + name + " (" + m.total + " bytes)");
                    Ack(nm.conn, m.sid, 0, 0);
                    return;
                }
                if (_rxBuf == null || m.sid != _rxSid || m.idx != _rxGot || m.data == null) return;
                int off = m.idx * ChunkSize;
                if (off + m.data.Length > _rxBuf.Length) { Fail(nm.conn, "chunk overflow"); return; }
                Buffer.BlockCopy(m.data, 0, _rxBuf, off, m.data.Length);
                _rxGot++;
                _rxLast = Time.unscaledTime;
                if (_rxGot >= _rxChunks)
                {
                    Directory.CreateDirectory(BtcReplay.ReplaysDir);
                    string path = Unique(BtcReplay.ReplaysDir, Path.GetFileNameWithoutExtension(_rxName) + (string.IsNullOrEmpty(_rxHost) ? string.Empty : "_host-" + _rxHost));
                    File.WriteAllBytes(path, _rxBuf);
                    BtcLog.Info("ReplayShare", "saved " + path);
                    Ack(nm.conn, _rxSid, _rxGot, 3);
                    BtcToast.Show(BtcReplay.T("Replay from the host saved: ", "Реплей от хоста сохранён: ") + Path.GetFileName(path));
                    _rxBuf = null;
                    _rxSid = -1;
                }
                else if (_rxGot % AckEvery == 0)
                {
                    Ack(nm.conn, _rxSid, _rxGot, 0);
                }
            }
            catch (Exception e)
            {
                BtcLog.Error("ReplayShare", "OnChunkAtClient: " + e);
                Fail(nm.conn, "error");
            }
        }

        private static void Fail(NetworkConnection conn, string why)
        {
            BtcLog.Warn("ReplayShare", "receive failed: " + why);
            try { Ack(conn, _rxSid, _rxGot, 2); } catch (Exception) { }
            _rxBuf = null;
            _rxSid = -1;
        }

        private static string HostName()
        {
            Brix.Network.UNetManager m = NetworkManager.singleton as Brix.Network.UNetManager;
            return m != null && m.LocalPlayerName != null ? m.LocalPlayerName : string.Empty;
        }

        private static string CleanHost(string n)
        {
            if (string.IsNullOrEmpty(n)) return null;
            n = BtcStr.Plain(n);
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            n = n.Trim();
            if (n.Length > 24) n = n.Substring(0, 24);
            return n.Length == 0 ? null : n;
        }

        private static string SafeName(string n)
        {
            if (string.IsNullOrEmpty(n)) return null;
            n = Path.GetFileName(n);
            if (n.Length > 120 || !n.EndsWith(".btcr", StringComparison.OrdinalIgnoreCase)) return null;
            if (n.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
            return n;
        }

        private static string Unique(string dir, string baseName)
        {
            string p = Path.Combine(dir, baseName + ".btcr");
            int k = 1;
            while (File.Exists(p))
            {
                p = Path.Combine(dir, baseName + "_" + k + ".btcr");
                k++;
            }
            return p;
        }

        internal sealed class ChunkMsg : MessageBase
        {
            internal int sid;
            internal int idx;
            internal int total;
            internal string name;
            internal byte[] data;

            public override void Serialize(NetworkWriter w)
            {
                w.Write(sid);
                w.Write(idx);
                w.Write(total);
                w.Write(name ?? string.Empty);
                w.WriteBytesFull(data ?? new byte[0]);
            }

            public override void Deserialize(NetworkReader r)
            {
                sid = r.ReadInt32();
                idx = r.ReadInt32();
                total = r.ReadInt32();
                name = r.ReadString();
                data = r.ReadBytesAndSize();
            }
        }

        internal sealed class AckMsg : MessageBase
        {
            internal int sid;
            internal int next;
            internal int status; // 0 progress, 1 skipped, 2 failed, 3 done

            public override void Serialize(NetworkWriter w)
            {
                w.Write(sid);
                w.Write(next);
                w.Write((byte)status);
            }

            public override void Deserialize(NetworkReader r)
            {
                sid = r.ReadInt32();
                next = r.ReadInt32();
                status = r.ReadByte();
            }
        }
    }
}
