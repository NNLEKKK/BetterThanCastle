using System;
using System.Collections.Generic;
using Brix.Game.AI;
using Brix.Game.Network;
using Brix.Network;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// Smooth unit movement (BC ON, new-with-new): vanilla UNetTransform samples units at 6 Hz and the client chases the last sample
    /// with an exponential lerp (~200-300 ms behind the host on top of the ping, steps visible) and a lost last packet of a unit that
    /// stopped is never repeated. Here the host sends the pose of every moving unit at 20 Hz with its own clock (unreliable, only to
    /// connections that see the unit), repeats the final pose after a stop and refreshes everybody every second; the client maps the
    /// host clock to its own (windowed minimum), interpolates behind by a delay that adapts to the jitter and fades corrections.
    /// Vanilla UNetTransform keeps running underneath (same wire); while our samples arrive its client lerp is skipped.
    /// </summary>
    internal static class BtcPosNet
    {
        internal const short MsgBatch = 126, MsgHello = 127;
        private const int Proto = 2;
        private const float Interval = 0.05f, Settle = 0.3f, Refresh = 1f, Alive = 2.5f;
        private const float Snap = 6f;   // farther than this = put the model there at once
        private const int FlushAt = 1100;
        private const float Q = 64f;   // position quantum 1/64 m, range +-512 m

        internal static bool On { get { return BtcNetworkGate.Enabled && BtcConfig.PosSync != null && BtcConfig.PosSync.Value; } }

        private static AccessTools.FieldRef<NetworkIdentity, List<NetworkConnection>> _observers;
        private static bool _init, _initOk;

        private static bool Init()
        {
            if (_init) return _initOk;
            _init = true;
            try { _observers = AccessTools.FieldRefAccess<NetworkIdentity, List<NetworkConnection>>("m_Observers"); _initOk = true; }
            catch (Exception e) { BtcLog.Error("PosNet", "init: " + e.Message); }
            return _initOk;
        }

        // ------------------------------------------------------------------ registry (UNetTransform on units)
        private sealed class Entry
        {
            internal UNetTransform Ut;
            internal Transform T;
            internal NetworkIdentity Ni;
            // host
            internal Vector3 SentPos;
            internal Quaternion SentRot;
            internal float MovedAt = -100f, RefreshAt;
            internal bool Sent;
            // client: ring of samples (host time)
            internal readonly float[] St = new float[8];
            internal readonly Vector3[] Sp = new Vector3[8];
            internal readonly Quaternion[] Sr = new Quaternion[8];
            internal int N, Head;
            internal float LastAt;
            internal Vector3 Corr, PrevRaw, PrevShown;
            internal bool Shown, Snap;
        }

        private static readonly List<Entry> All = new List<Entry>();
        private static readonly Dictionary<int, Entry> ByInst = new Dictionary<int, Entry>();
        private static readonly Dictionary<uint, Entry> ByNet = new Dictionary<uint, Entry>();

        private static Entry Get(UNetTransform ut)
        {
            Entry e;
            if (ByInst.TryGetValue(ut.GetInstanceID(), out e)) return e;
            if (ut.GetComponent<Locomotion4>() == null) { ByInst[ut.GetInstanceID()] = null; return null; }
            e = new Entry { Ut = ut, T = ut.transform, Ni = ut.GetComponent<NetworkIdentity>() };
            ByInst[ut.GetInstanceID()] = e;
            All.Add(e);
            return e;
        }

        private static void Prune()
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i].Ut == null) All.RemoveAt(i);
            List<int> dead = null;
            foreach (KeyValuePair<int, Entry> kv in ByInst) if (kv.Value != null && kv.Value.Ut == null) (dead ?? (dead = new List<int>())).Add(kv.Key);
            if (dead != null) for (int i = 0; i < dead.Count; i++) ByInst.Remove(dead[i]);
        }

        // ------------------------------------------------------------------ session wiring (BtcNetManager, BC ON paths only)
        private sealed class Peer
        {
            internal NetworkConnection Conn;
            internal readonly NetworkWriter W = new NetworkWriter();
            internal bool Open;
        }
        private static readonly Dictionary<int, Peer> Peers = new Dictionary<int, Peer>();
        private static readonly List<Peer> PeerList = new List<Peer>();

        internal static void OnServerStarted(UNetManager m)
        {
            Peers.Clear(); PeerList.Clear();
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                if (NetworkServer.handlers.ContainsKey(MsgHello)) NetworkServer.UnregisterHandler(MsgHello);
                NetworkServer.RegisterHandler(MsgHello, OnHello);
                Runner(m);
            }
            catch (Exception e) { BtcLog.Error("PosNet", "server: " + e.Message); }
        }

        internal static void OnClientConnected(UNetManager m)
        {
            if (NetworkServer.active || !BtcNetworkGate.Enabled) return;
            try
            {
                NetworkClient c = m.LocalNetworkClient;
                if (c == null) return;
                if (c.handlers.ContainsKey(MsgBatch)) c.UnregisterHandler(MsgBatch);
                c.RegisterHandler(MsgBatch, OnBatch);
                Runner(m);
                ResetClient();
                if (On && c.isConnected) c.Send(MsgHello, new IntegerMessage(Proto));
                BtcLog.Info("PosNet", "client ready, smooth movement " + (On ? "requested" : "off (config)"));
            }
            catch (Exception e) { BtcLog.Error("PosNet", "client: " + e.Message); }
        }

        private static void Runner(UNetManager m)
        {
            if (m != null && m.GetComponent<BtcPosNetRunner>() == null) m.gameObject.AddComponent<BtcPosNetRunner>();
        }

        private static void OnHello(NetworkMessage nm)
        {
            try
            {
                IntegerMessage im = nm.ReadMessage<IntegerMessage>();
                if (nm.conn == null || im == null || im.value < Proto) return;
                Peer p;
                if (!Peers.TryGetValue(nm.conn.connectionId, out p)) { p = new Peer(); Peers[nm.conn.connectionId] = p; PeerList.Add(p); }
                p.Conn = nm.conn; p.Open = false;
                BtcLog.Info("PosNet", "host: conn " + nm.conn.connectionId + " gets smooth movement");
            }
            catch (Exception e) { BtcLog.Error("PosNet", "hello: " + e.Message); }
        }

        // ------------------------------------------------------------------ host
        private static float _nextSend, _statAt;
        private static int _hRecords, _hBytes, _hPackets;

        internal static void HostTick()
        {
            if (PeerList.Count == 0 || !NetworkServer.active || !On || !Init()) return;
            float now = Time.unscaledTime;
            if (now < _nextSend) return;
            _nextSend = now + Interval;
            for (int i = PeerList.Count - 1; i >= 0; i--)
            {
                Peer p = PeerList[i];
                if (p.Conn == null || !p.Conn.isConnected || !NetworkServer.connections.Contains(p.Conn))
                {
                    PeerList.RemoveAt(i);
                    if (p.Conn != null) Peers.Remove(p.Conn.connectionId);
                }
            }
            if (PeerList.Count == 0) return;
            bool prune = false;
            for (int i = 0; i < All.Count; i++)
            {
                Entry e = All[i];
                if (e.Ut == null) { prune = true; continue; }
                if (e.Ni == null || !e.T.gameObject.activeInHierarchy) continue;
                uint nid = e.Ni.netId.Value;
                if (nid == 0u) continue;
                Vector3 pos = e.T.position;
                Quaternion rot = e.T.rotation;
                bool moved = !e.Sent || (pos - e.SentPos).sqrMagnitude > 0.0001f || Quaternion.Angle(rot, e.SentRot) > 0.5f;
                if (moved) e.MovedAt = now;
                bool full = now >= e.RefreshAt;
                if (!moved && now - e.MovedAt > Settle && !full) continue;
                List<NetworkConnection> obs = _observers(e.Ni);
                if (obs == null || obs.Count == 0) continue;
                bool tp = e.Sent && (pos - e.SentPos).sqrMagnitude > 16f;   // >4 m in one 50 ms step: pushed back, wormhole, respawn
                e.SentPos = pos; e.SentRot = rot; e.Sent = true;
                e.RefreshAt = now + Refresh + (nid % 16u) * 0.03f;
                for (int c = 0; c < PeerList.Count; c++)
                {
                    Peer p = PeerList[c];
                    if (!obs.Contains(p.Conn)) continue;
                    NetworkWriter w = p.W;
                    if (!p.Open) { w.StartMessage(MsgBatch); w.Write(now); p.Open = true; }
                    Write(w, nid, pos, rot, tp);
                    _hRecords++;
                    if (w.Position >= FlushAt) Flush(p);
                }
            }
            // every peer gets a batch every interval, empty when nothing moved (~10 B): the client clock and its underrun check need it
            for (int c = 0; c < PeerList.Count; c++)
            {
                Peer p = PeerList[c];
                if (!p.Open) { p.W.StartMessage(MsgBatch); p.W.Write(now); p.Open = true; }
                Flush(p);
            }
            if (prune) Prune();
            if (now >= _statAt)
            {
                if (_statAt > 0f && _hRecords > 0)
                    BtcLog.Info("PosNet", "host 30 s: peers=" + PeerList.Count + " units=" + All.Count + " records=" + _hRecords + " packets=" + _hPackets + " (" + (_hBytes / 30 / 1024f).ToString("0.0") + " KB/s)");
                _statAt = now + 30f; _hRecords = 0; _hBytes = 0; _hPackets = 0;
            }
        }

        // record: packed netId, flags (1 = upright yaw only, 2 = full float position, 4 = teleport), position, rotation
        private static void Write(NetworkWriter w, uint nid, Vector3 pos, Quaternion rot, bool tp)
        {
            w.WritePackedUInt32(nid);
            Vector3 e = rot.eulerAngles;
            bool upright = Mathf.Abs(Mathf.DeltaAngle(e.x, 0f)) < 0.5f && Mathf.Abs(Mathf.DeltaAngle(e.z, 0f)) < 0.5f;
            bool small = Mathf.Abs(pos.x) < 511f && Mathf.Abs(pos.y) < 511f && Mathf.Abs(pos.z) < 511f;
            w.Write((byte)((upright ? 1 : 0) | (small ? 0 : 2) | (tp ? 4 : 0)));
            if (small) { w.Write((short)Mathf.RoundToInt(pos.x * Q)); w.Write((short)Mathf.RoundToInt(pos.y * Q)); w.Write((short)Mathf.RoundToInt(pos.z * Q)); }
            else w.Write(pos);
            if (upright) w.Write((ushort)Mathf.RoundToInt(Mathf.Repeat(e.y, 360f) / 360f * 65535f));
            else w.Write(rot);
        }

        private static void Flush(Peer p)
        {
            NetworkWriter w = p.W;
            try
            {
                w.WritePackedUInt32(0u);
                w.FinishMessage();
                _hBytes += w.Position; _hPackets++;
                p.Conn.SendWriter(w, Channels.DefaultUnreliable);
            }
            catch (Exception e) { BtcLog.Error("PosNet", "send: " + e.Message); }
            p.Open = false;
        }

        // ------------------------------------------------------------------ client
        // v2 (after the first user test: units jumped while walking): the v1 clock offset dropped at once on every fast packet, so the
        // render time stepped back and forth by the network jitter; a late packet made units run 150 ms ahead on extrapolation and get
        // pulled back. Now: offset = minimum over a 2-4 s window, delay adapts to the measured jitter (and grows after underruns),
        // the render clock never jumps (it is steered by +-10% speed), no extrapolation beyond 50 ms, and any jump of the raw pose is
        // absorbed into a correction offset that fades in ~0.1 s instead of moving the model at once.
        private const float Window = 2f, MinDelay = 0.08f, MaxDelay = 0.4f, MaxExtrapolate = 0.05f, CorrFade = 0.08f;
        private static float _baseCur = float.MaxValue, _basePrev = float.MaxValue, _bucketAt;   // local unscaled time - host time
        private static float _sendDt = 0.05f, _jit, _under, _delay = 0.12f, _rt, _newest = float.MinValue, _underAt;
        private static bool _rtInit;
        private static float _lastPacketAt = -100f;
        private static int _cPackets, _cRecords, _cExtra, _cSnap, _cJump, _cUnder;
        private static float _cStatAt, _cDelaySum;
        private static int _cDelayN, _cLate;
        private static float _cLateMax, _cGapMax;

        private static void ResetClient()
        {
            _baseCur = _basePrev = float.MaxValue; _bucketAt = 0f; _sendDt = Interval; _jit = 0f; _under = 0f; _delay = 0.12f;
            _rtInit = false; _newest = float.MinValue; ByNet.Clear();
        }

        private static float Base { get { return Mathf.Min(_baseCur, _basePrev); } }

        private static void OnBatch(NetworkMessage nm)
        {
            try
            {
                if (NetworkServer.active || !Init()) return;
                NetworkReader r = nm.reader;
                float hostNow = r.ReadSingle();
                float local = Time.unscaledTime;
                float off = local - hostNow;
                if (local >= _bucketAt) { _basePrev = _baseCur; _baseCur = float.MaxValue; _bucketAt = local + Window; }
                if (off < _baseCur) _baseCur = off;
                float j = off - Base;
                if (j < 0.5f) _jit += (j - _jit) * 0.05f;
                if (j > _cLateMax) _cLateMax = j;
                if (j > _delay) _cLate++;
                float gap = local - _lastPacketAt;
                if (_lastPacketAt > 0f && gap > _cGapMax && gap < 5f) _cGapMax = gap;
                if (hostNow > _newest) { if (_newest > float.MinValue && hostNow - _newest < 0.5f) _sendDt += (hostNow - _newest - _sendDt) * 0.05f; _newest = hostNow; }
                _lastPacketAt = local;
                _cPackets++;
                while (true)
                {
                    uint nid = r.ReadPackedUInt32();
                    if (nid == 0u) break;
                    byte f = r.ReadByte();
                    Vector3 pos;
                    if ((f & 2) == 0) pos = new Vector3(r.ReadInt16() / Q, r.ReadInt16() / Q, r.ReadInt16() / Q);
                    else pos = r.ReadVector3();
                    Quaternion rot = (f & 1) != 0 ? Quaternion.Euler(0f, r.ReadUInt16() / 65535f * 360f, 0f) : r.ReadQuaternion();
                    _cRecords++;
                    Entry e = Find(nid);
                    if (e == null) continue;
                    if (e.N > 0 && hostNow <= e.St[e.Head]) continue;   // old or duplicate
                    if ((f & 4) != 0) { e.N = 0; e.Snap = true; }       // host teleport: forget the path before it
                    e.Head = (e.Head + 1) & 7;
                    e.St[e.Head] = hostNow; e.Sp[e.Head] = pos; e.Sr[e.Head] = rot;
                    if (e.N < 8) e.N++;
                    e.LastAt = local;
                }
            }
            catch (Exception e) { BtcLog.Error("PosNet", "batch: " + e.Message); }
        }

        private static Entry Find(uint nid)
        {
            Entry e;
            if (ByNet.TryGetValue(nid, out e) && e.Ut != null && e.Ni != null && e.Ni.netId.Value == nid) return e;
            GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(nid));
            if (go == null) return null;
            UNetTransform ut = go.GetComponent<UNetTransform>();
            if (ut == null) return null;
            e = Get(ut);
            if (e == null) return null;
            e.N = 0; e.Shown = false;
            ByNet[nid] = e;
            return e;
        }

        /// <summary>Client: vanilla UNetTransform lerp is skipped for units we drive.</summary>
        internal static bool Drives(UNetTransform ut)
        {
            if (NetworkServer.active || !On) return false;
            Entry e;
            return ByInst.TryGetValue(ut.GetInstanceID(), out e) && e != null && e.N > 0 && Time.unscaledTime - e.LastAt < Alive;
        }

        internal static void ClientTick()
        {
            if (NetworkServer.active || ByNet.Count == 0 || !On || (_baseCur == float.MaxValue && _basePrev == float.MaxValue)) return;
            float local = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            // delay: one measured send interval + 2x mean jitter + margin, +10 ms per underrun (max 100 ms, fades 5 ms/s); moves slowly.
            // A rarer longer gap (host frame hitch) just holds the units and the correction fade glides them on: no ever-growing delay.
            if (_under > 0f) _under = Mathf.Max(0f, _under - 0.005f * dt);
            float want = Mathf.Clamp(_sendDt + 2f * _jit + 0.02f + _under, MinDelay, MaxDelay);
            _delay += (want - _delay) * Mathf.Min(1f, dt * 0.5f);
            float target = local - Base - _delay;
            if (!_rtInit || Mathf.Abs(target - _rt) > 0.5f) { _rt = target; _rtInit = true; }
            else _rt += dt * (1f + Mathf.Clamp((target - _rt) * 2f, -0.1f, 0.1f));
            float rt = _rt;
            if (rt > _newest + 0.01f && local - _lastPacketAt < 0.5f && local >= _underAt)
            {
                _under = Mathf.Min(_under + 0.01f, 0.1f); _underAt = local + 0.5f; _cUnder++;
            }
            _cDelaySum += _delay; _cDelayN++;
            float fade = Mathf.Exp(-dt / CorrFade);
            float jumpAt = 0.3f + 15f * dt;
            foreach (KeyValuePair<uint, Entry> kv in ByNet)
            {
                Entry e = kv.Value;
                if (e.N == 0 || e.Ut == null || local - e.LastAt >= Alive) { e.Shown = false; continue; }
                Transform t = e.T;
                int h = e.Head;
                Vector3 p; Quaternion q;
                if (rt >= e.St[h])
                {
                    // newest sample is in the past: hold, or extrapolate a moving unit a very little
                    int pv = (h + 7) & 7;
                    float sdt = e.St[h] - e.St[pv];
                    float ahead = Mathf.Min(rt - e.St[h], MaxExtrapolate);
                    if (e.N > 1 && sdt > 0.001f && sdt < 0.3f && ahead > 0f)
                    {
                        Vector3 v = (e.Sp[h] - e.Sp[pv]) / sdt;
                        p = e.Sp[h] + v * ahead;
                        if (v.sqrMagnitude > 0.01f) _cExtra++;
                    }
                    else p = e.Sp[h];
                    q = e.Sr[h];
                }
                else
                {
                    int i = h, k = 0;
                    while (k < e.N - 1 && e.St[(i + 7) & 7] > rt) { i = (i + 7) & 7; k++; }
                    int a = (i + 7) & 7;
                    if (k >= e.N - 1) { p = e.Sp[i]; q = e.Sr[i]; }   // older than the buffer: oldest sample
                    else
                    {
                        float span = e.St[i] - e.St[a];
                        float u = span > 0.0001f ? Mathf.Clamp01((rt - e.St[a]) / span) : 1f;
                        p = Vector3.LerpUnclamped(e.Sp[a], e.Sp[i], u);
                        q = Quaternion.Slerp(e.Sr[a], e.Sr[i], u);
                    }
                }
                if (e.Snap) { e.Snap = false; e.Corr = Vector3.zero; _cSnap++; }
                else if (!e.Shown)
                {
                    // hand-over from the vanilla lerp: start from where the model is
                    Vector3 c = t.position - p;
                    e.Corr = c.sqrMagnitude < Snap * Snap ? c : Vector3.zero;
                }
                else if ((p - e.PrevRaw).sqrMagnitude > jumpAt * jumpAt)
                {
                    Vector3 c = e.PrevShown - p;
                    if (c.sqrMagnitude < Snap * Snap) { e.Corr = c; _cJump++; }
                    else { e.Corr = Vector3.zero; _cSnap++; }
                }
                else e.Corr *= fade;
                if (e.Corr.sqrMagnitude < 0.000001f) e.Corr = Vector3.zero;
                Vector3 shown = p + e.Corr;
                e.PrevRaw = p; e.PrevShown = shown; e.Shown = true;
                t.position = shown;
                t.rotation = q;
            }
            if (local >= _cStatAt)
            {
                if (_cStatAt > 0f && _cPackets > 0)
                    BtcLog.Info("PosNet", "client 30 s: packets=" + _cPackets + " records=" + _cRecords + " delay=" + (_cDelayN > 0 ? _cDelaySum / _cDelayN * 1000f : 0f).ToString("0") + "ms jitter=" + (_jit * 1000f).ToString("0") + "ms underruns=" + _cUnder + " extrapolated=" + _cExtra + " jumps=" + _cJump + " snaps=" + _cSnap + " units=" + ByNet.Count + " lateMax=" + (_cLateMax * 1000f).ToString("0") + "ms late=" + _cLate + " gapMax=" + (_cGapMax * 1000f).ToString("0") + "ms fps=" + (_cDelayN / 30) + " hostDt=" + (_sendDt * 1000f).ToString("0") + "ms");
                _cStatAt = local + 30f; _cPackets = 0; _cRecords = 0; _cExtra = 0; _cSnap = 0; _cJump = 0; _cUnder = 0; _cDelaySum = 0f; _cDelayN = 0; _cLate = 0; _cLateMax = 0f; _cGapMax = 0f;
                Sweep();
            }
        }

        private static readonly List<uint> Dead = new List<uint>();
        private static void Sweep()
        {
            Dead.Clear();
            foreach (KeyValuePair<uint, Entry> kv in ByNet) if (kv.Value.Ut == null || kv.Value.Ni == null || kv.Value.Ni.netId.Value != kv.Key) Dead.Add(kv.Key);
            for (int i = 0; i < Dead.Count; i++) ByNet.Remove(Dead[i]);
            Prune();
        }

        /// <summary>Host: every unit transform becomes known when it starts (UNetTransform awake).</summary>
        internal static void OnAwake(UNetTransform ut) { if (ut != null) Get(ut); }
    }

    /// <summary>Host sends after the physics step of the frame; client places units before rendering.</summary>
    internal sealed class BtcPosNetRunner : MonoBehaviour
    {
        private void LateUpdate()
        {
            try
            {
                if (NetworkServer.active) BtcPosNet.HostTick();
                else BtcPosNet.ClientTick();
            }
            catch (Exception e) { BtcLog.Error("PosNet", "tick: " + e.Message); }
        }
    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(UNetTransform), "BrixUpdate")]
    internal static class UNetTransformBrixUpdatePatch
    {
        private static bool Prefix(UNetTransform __instance) { return !BtcPosNet.Drives(__instance); }
    }

    [HarmonyPatch(typeof(UNetTransform), "Start")]
    internal static class UNetTransformStartPatch
    {
        private static void Postfix(UNetTransform __instance) { try { BtcPosNet.OnAwake(__instance); } catch (Exception) { } }
    }
}
