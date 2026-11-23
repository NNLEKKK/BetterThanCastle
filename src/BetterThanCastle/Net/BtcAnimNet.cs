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
    /// Smooth unit animations for clients (BetterConnection ON, both sides new).
    /// Vanilla UNetAnimator samples ~38 animator bools into one SyncVar at 10 Hz on the Unreliable channel, sends some floats and
    /// triggers as separate unreliable messages, never sends a dozen params (AttackVariation, StrikeDistance, step directions ...)
    /// and queues triggers until a transition ends. Clients then miss short pulses (strike start/abort, hurt), stay stuck after a
    /// lost packet and play the wrong strike variation.
    /// Here the host sends, per changed animator, in the frame of the change: the full bool state, the missing params and the
    /// state every layer is in (or heading to) with its normalized time, stamped with the LLAPI network timestamp. Each change is
    /// sent again 0.1 s later and every animator is refreshed every 1.5 s, all unreliable: a lost packet never blocks anything.
    /// The client applies it at once (no buffer) and, one frame later, moves any layer that ended in another state to the host's
    /// state, fast-forwarded by the one-way delay, so it shows what the host shows now. While packets arrive for an animator the
    /// vanilla bool SyncVar and the trigger/play messages are ignored for it.
    /// Wire: msg 125 client -> host "I can" (hello), msg 124 host -> client batches (channel 1, only to hello'd observers).
    /// BC OFF: nothing registered, nothing sent, vanilla behaviour.
    /// </summary>
    internal static class BtcAnimNet
    {
        internal const short MsgBatch = 124, MsgHello = 125;
        private const int Proto = 2;   // 2: Archetype (bit 12) added, peers on 1 get no batches
        private const int MaxLayers = 6;
        private const float Resend = 0.1f, Refresh = 1.5f, Alive = 3f;
        private const int FlushAt = 1000;

        /// <summary>Params vanilla never sends (sendOnNetwork false), plus Archetype. Index = wire bit; bools are ExtraBoolFrom..ExtraBoolTo-1.</summary>
        private static readonly string[] ExtraNames =
        {
            "CloseStepStartDirection", "CombatStepDirection", "ArrivalDirection", "FacingUp", "WalkVariation", "RunVariation",
            "AttackVariation", "StrikeDistance", "StunDirection", "StepWithLeftFoot", "AttackWillHit", "AttackIsBlocked",
            "Archetype"   // vanilla sends it once, only to units' observers at that moment: an enemy armed in our fog kept the default stance
        };
        private const int ExtraBoolFrom = 9, ExtraBoolTo = 12;
        private static bool IsBool(int k) { return k >= ExtraBoolFrom && k < ExtraBoolTo; }
        private static readonly int[] ExtraHash = new int[ExtraNames.Length];

        internal static bool On { get { return BtcNetworkGate.Enabled && BtcConfig.AnimSync != null && BtcConfig.AnimSync.Value; } }
        /// <summary>DEV A/B: the client measures but neither corrects nor blocks vanilla.</summary>
        internal static bool ObserveOnly;

        private static Action<UNetAnimator, ulong> _stateChanged;
        private static AccessTools.FieldRef<UNetAnimator, Queue<int>> _triggerQueue;
        private static AccessTools.FieldRef<NetworkIdentity, List<NetworkConnection>> _observers;
        private static bool _init, _initOk;

        private static bool Init()
        {
            if (_init) return _initOk;
            _init = true;
            try
            {
                for (int i = 0; i < ExtraNames.Length; i++) ExtraHash[i] = Animator.StringToHash(ExtraNames[i]);
                _stateChanged = (Action<UNetAnimator, ulong>)Delegate.CreateDelegate(typeof(Action<UNetAnimator, ulong>), AccessTools.Method(typeof(UNetAnimator), "stateChanged"));
                _triggerQueue = AccessTools.FieldRefAccess<UNetAnimator, Queue<int>>("triggerQueue");
                _observers = AccessTools.FieldRefAccess<NetworkIdentity, List<NetworkConnection>>("m_Observers");
                _initOk = true;
            }
            catch (Exception e) { BtcLog.Error("AnimNet", "init: " + e.Message); }
            return _initOk;
        }

        // ------------------------------------------------------------------ registry (both sides, from UNetAnimator.Awake)
        private sealed class Entry
        {
            internal int Id;
            internal UNetAnimator Ua;
            internal Animator An;
            internal NetworkIdentity Ni;
            internal int Extras = -1;              // bit mask of ExtraNames the controller has (-1 = not checked)
            internal RuntimeAnimatorController Ctrl;
            internal int Side;                     // DEV counters: 0 unknown, 1 local player's unit, 2 other
            // host
            internal bool Sent;
            internal ulong State;
            internal readonly int[] Hash = new int[MaxLayers];
            internal readonly float[] Xv = new float[16];
            internal readonly float[] Xat = new float[16];
            internal float ResendAt, RefreshAt;
            internal int ObsN;                     // observer count last tick: a new observer gets a full record at once
            // client
            internal uint Seq;
            internal float RecvAt, RecvGameTime, Delay;
            internal int RecvFrame;
            internal bool Pending;
            internal int Layers;
            internal readonly byte[] PBits = new byte[MaxLayers];
            internal readonly int[] PHash = new int[MaxLayers];
            internal readonly float[] PNt = new float[MaxLayers];
            internal readonly float[] PLen = new float[MaxLayers];
        }

        private static readonly List<Entry> All = new List<Entry>();
        private static readonly Dictionary<int, Entry> ByInst = new Dictionary<int, Entry>();
        private static readonly Dictionary<uint, Entry> ByNet = new Dictionary<uint, Entry>();

        internal static void OnAwake(UNetAnimator ua)
        {
            if (ua == null) return;
            int id = ua.GetInstanceID();
            if (ByInst.ContainsKey(id)) return;
            Entry e = new Entry { Id = id, Ua = ua, An = ua.animator, Ni = ua.GetComponent<NetworkIdentity>() };
            ByInst[id] = e;
            All.Add(e);
        }

        private static void Prune()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                Entry e = All[i];
                if (e.Ua != null) continue;
                All.RemoveAt(i);
                ByInst.Remove(e.Id);
            }
        }

        private static int ExtrasOf(Entry e)
        {
            RuntimeAnimatorController c = e.An != null ? e.An.runtimeAnimatorController : null;
            if (e.Extras >= 0 && c == e.Ctrl) return e.Extras;
            e.Ctrl = c;
            int mask = 0;
            if (c != null)
            {
                int key = c.GetInstanceID();
                if (!CtrlMask.TryGetValue(key, out mask))
                {
                    AnimatorControllerParameter[] ps = e.An.parameters;
                    for (int k = 0; k < ExtraNames.Length; k++)
                        for (int j = 0; j < ps.Length; j++)
                            if (ps[j].nameHash == ExtraHash[k]
                                && (IsBool(k) ? ps[j].type == AnimatorControllerParameterType.Bool : ps[j].type == AnimatorControllerParameterType.Float))
                            { mask |= 1 << k; break; }
                    CtrlMask[key] = mask;
                }
            }
            e.Extras = mask;
            return mask;
        }
        private static readonly Dictionary<int, int> CtrlMask = new Dictionary<int, int>();

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
            catch (Exception e) { BtcLog.Error("AnimNet", "server: " + e.Message); }
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
                if (On && c.isConnected) c.Send(MsgHello, new IntegerMessage(Proto));
                BtcLog.Info("AnimNet", "client ready, smooth animations " + (On ? "requested" : "off (config)"));
            }
            catch (Exception e) { BtcLog.Error("AnimNet", "client: " + e.Message); }
        }

        private static void Runner(UNetManager m)
        {
            if (m != null && m.GetComponent<BtcAnimNetRunner>() == null) m.gameObject.AddComponent<BtcAnimNetRunner>();
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
                BtcLog.Info("AnimNet", "host: conn " + nm.conn.connectionId + " gets smooth animations");
            }
            catch (Exception e) { BtcLog.Error("AnimNet", "hello: " + e.Message); }
        }

        // ------------------------------------------------------------------ host
        private static uint _seq;
        private static int _hRecords, _hBytes, _hPackets;
        private static float _statAt;

        internal static void HostTick()
        {
            if (PeerList.Count == 0 || !NetworkServer.active) return;
            if (!On || !Init()) return;
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
            float now = Time.unscaledTime;
            int ts = NetworkTransport.GetNetworkTimestamp();
            _seq++;
            bool prune = false;
            for (int i = 0; i < All.Count; i++)
            {
                Entry e = All[i];
                UNetAnimator ua = e.Ua;
                if (ua == null) { prune = true; continue; }
                Animator an = e.An;
                if (an == null || !an.isActiveAndEnabled || e.Ni == null) continue;
                uint nid = ua.netId.Value;
                if (nid == 0u) continue;
                List<NetworkConnection> obs = _observers(e.Ni);
                if (obs == null || obs.Count == 0) { e.ObsN = 0; continue; }
                int xm = ExtrasOf(e);
                bool changed = !e.Sent || ua.state != e.State;
                int layers = Mathf.Min(an.layerCount, MaxLayers);
                for (int l = 0; l < layers; l++)
                {
                    int h = Target(an, l);
                    if (h != e.Hash[l]) { e.Hash[l] = h; changed = true; }
                }
                for (int k = 0; k < ExtraNames.Length; k++)
                {
                    if ((xm & (1 << k)) == 0) continue;
                    float v = IsBool(k) ? (an.GetBool(ExtraHash[k]) ? 1f : 0f) : an.GetFloat(ExtraHash[k]);
                    if (Mathf.Abs(v - e.Xv[k]) > 0.002f || !e.Sent) { e.Xv[k] = v; e.Xat[k] = now; changed = true; }
                }
                bool full = now >= e.RefreshAt || obs.Count > e.ObsN;   // a unit leaving the fog gets every param at once
                e.ObsN = obs.Count;
                if (changed) e.ResendAt = now + Resend;
                else if (e.ResendAt > 0f && now >= e.ResendAt) e.ResendAt = 0f;
                else if (!full) continue;
                e.State = ua.state; e.Sent = true; e.RefreshAt = now + Refresh + (nid % 16u) * 0.02f;
                for (int c = 0; c < PeerList.Count; c++)
                {
                    Peer p = PeerList[c];
                    if (!obs.Contains(p.Conn)) { _hObsMiss[SideOf(e) - 1]++; continue; }
                    NetworkWriter w = p.W;
                    if (!p.Open) { w.StartMessage(MsgBatch); w.Write(_seq); w.Write(ts); p.Open = true; }
                    WriteRecord(w, e, nid, an, layers, xm, full, now);
                    _hSide[SideOf(e) - 1]++;
                    _hRecords++;
                    if (w.Position >= FlushAt) Flush(p);
                }
            }
            for (int c = 0; c < PeerList.Count; c++) if (PeerList[c].Open) Flush(PeerList[c]);
            if (prune) Prune();
            if (now >= _statAt)
            {
                if (_statAt > 0f && _hRecords > 0)
                    BtcLog.Info("AnimNet", "host 30 s: peers=" + PeerList.Count + " animators=" + All.Count + " records=" + _hRecords + " packets=" + _hPackets + " bytes=" + _hBytes + " (" + (_hBytes / 30 / 1024f).ToString("0.0") + " KB/s)");
                _statAt = now + 30f; _hRecords = 0; _hBytes = 0; _hPackets = 0;
            }
        }

        /// <summary>The state a layer is in, or heading to while in a transition.</summary>
        private static int Target(Animator an, int l)
        {
            return an.IsInTransition(l) ? an.GetNextAnimatorStateInfo(l).fullPathHash : an.GetCurrentAnimatorStateInfo(l).fullPathHash;
        }

        private static void WriteRecord(NetworkWriter w, Entry e, uint nid, Animator an, int layers, int xm, bool full, float now)
        {
            w.WritePackedUInt32(nid);
            w.WritePackedUInt64(e.Ua.state);
            w.Write((byte)layers);
            for (int l = 0; l < layers; l++)
            {
                if (l > 0 && an.GetLayerWeight(l) < 0.01f) { w.Write((byte)0); continue; }
                AnimatorStateInfo si = an.IsInTransition(l) ? an.GetNextAnimatorStateInfo(l) : an.GetCurrentAnimatorStateInfo(l);
                float nt = si.normalizedTime;
                if (si.loop) nt -= Mathf.Floor(nt);
                w.Write((byte)(si.loop ? 3 : 1));
                w.Write(si.fullPathHash);
                w.Write((ushort)Mathf.Clamp(nt * 32768f, 0f, 65535f));
                w.Write((ushort)Mathf.Clamp(si.length * 1000f, 1f, 65535f));
            }
            int send = 0;
            for (int k = 0; k < ExtraNames.Length; k++)
                if ((xm & (1 << k)) != 0 && (full || now - e.Xat[k] < 0.5f)) send |= 1 << k;
            w.Write((ushort)send);
            for (int k = 0; k < ExtraNames.Length; k++)
            {
                if ((send & (1 << k)) == 0) continue;
                if (IsBool(k)) w.Write((byte)(e.Xv[k] > 0.5f ? 1 : 0));
                else w.Write(e.Xv[k]);
            }
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
            catch (Exception e) { BtcLog.Error("AnimNet", "send: " + e.Message); }
            p.Open = false;
        }

        // ------------------------------------------------------------------ client
        private static int _cPackets, _cRecords, _cChecks, _cMismatch, _cFixed, _cDrift, _cDelaySum, _cStale;
        private static float _cStatAt;

        private static void OnBatch(NetworkMessage nm)
        {
            try
            {
                if (NetworkServer.active || !Init()) return;
                NetworkReader r = nm.reader;
                uint seq = r.ReadUInt32();
                int ts = r.ReadInt32();
                byte err = 0;
                int ms = nm.conn != null ? NetworkTransport.GetRemoteDelayTimeMS(nm.conn.hostId, nm.conn.connectionId, ts, out err) : 0;
                if (nm.conn == null || err != 0 || ms < 0) ms = 50;
                if (ms > 400) ms = 400;
                _cPackets++; _cDelaySum += ms;
                _lastPacketAt = Time.unscaledTime;
                float delay = ms * 0.001f * Time.timeScale;
                bool on = On;
                for (int guard = 0; guard < 256; guard++)
                {
                    uint nid = r.ReadPackedUInt32();
                    if (nid == 0u) break;
                    ulong state = r.ReadPackedUInt64();
                    int layers = r.ReadByte();
                    Entry e = Find(nid);
                    bool fresh = e != null && ((int)(seq - e.Seq) > 0 || Time.unscaledTime - e.RecvAt > Alive);
                    for (int l = 0; l < layers; l++)
                    {
                        byte b = r.ReadByte();
                        if (b == 0) { if (fresh && l < MaxLayers) e.PBits[l] = 0; continue; }
                        int h = r.ReadInt32();
                        float nt = r.ReadUInt16() / 32768f;
                        float len = r.ReadUInt16() * 0.001f;
                        if (fresh && l < MaxLayers) { e.PBits[l] = b; e.PHash[l] = h; e.PNt[l] = nt; e.PLen[l] = len; }
                    }
                    int send = r.ReadUInt16();
                    for (int k = 0; k < 16; k++)
                    {
                        if ((send & (1 << k)) == 0) continue;
                        float v = IsBool(k) ? r.ReadByte() : r.ReadSingle();
                        if (!fresh || !on || ObserveOnly || k >= ExtraNames.Length || e.An == null || (ExtrasOf(e) & (1 << k)) == 0) continue;
                        _cX[SideOf(e) - 1]++;
                        if (IsBool(k)) e.An.SetBool(ExtraHash[k], v > 0.5f);
                        else e.An.SetFloat(ExtraHash[k], v);
                    }
                    _cRecords++;
                    if (!fresh) { if (e != null) _cStale++; continue; }
                    _cSide[SideOf(e) - 1]++;
                    e.Seq = seq;
                    e.RecvAt = Time.unscaledTime; e.RecvGameTime = Time.time; e.RecvFrame = Time.frameCount; e.Delay = delay;
                    e.Layers = Mathf.Min(layers, MaxLayers);
                    e.Pending = true;
                    if (on && !ObserveOnly && e.Ua != null)
                    {
                        e.Ua.state = state;
                        _applying = true;
                        try { _stateChanged(e.Ua, state); }
                        finally { _applying = false; }
                    }
                }
            }
            catch (Exception ex) { BtcLog.Error("AnimNet", "batch: " + ex.Message); }
        }

        private static bool _applying;
        private static float _lastPacketAt = -100f;

        /// <summary>Client gets our batches (feature on, not observing): cheap gate before any per-animator lookup.</summary>
        internal static bool Receiving
        {
            get { return !ObserveOnly && Time.unscaledTime - _lastPacketAt < Alive && !NetworkServer.active && On; }
        }

        private static Entry Find(uint nid)
        {
            Entry e;
            if (ByNet.TryGetValue(nid, out e) && e.Ua != null && e.Ua.netId.Value == nid) return e;
            GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(nid));
            if (go == null) return null;
            UNetAnimator ua = go.GetComponent<UNetAnimator>();
            if (ua == null) return null;
            OnAwake(ua);
            if (!ByInst.TryGetValue(ua.GetInstanceID(), out e)) return null;
            if (e.An == null) e.An = ua.animator;
            ByNet[nid] = e;
            return e;
        }

        /// <summary>Client LateUpdate: one frame after a record, move layers that ended elsewhere to the host's state "now".</summary>
        internal static void ClientTick()
        {
            if (NetworkServer.active || ByNet.Count == 0) return;
            bool fix = On && !ObserveOnly;
            float ts = Time.timeScale;
            foreach (KeyValuePair<uint, Entry> kv in ByNet)
            {
                Entry e = kv.Value;
                if (!e.Pending || e.RecvFrame >= Time.frameCount) continue;
                e.Pending = false;
                Animator an = e.An;
                if (an == null || !an.isActiveAndEnabled || ts <= 0f || an.runtimeAnimatorController == null) continue;
                float ahead = e.Delay + (Time.time - e.RecvGameTime);
                int layers = Mathf.Min(e.Layers, an.layerCount);
                for (int l = 0; l < layers; l++)
                {
                    if (e.PBits[l] == 0) continue;
                    bool loop = (e.PBits[l] & 2) != 0;
                    float len = Mathf.Max(e.PLen[l], 0.05f);
                    float nt = e.PNt[l] + ahead / len;
                    if (!loop && nt >= 0.95f) continue;   // the host is about to leave it by exit time: the client follows on its own
                    if (loop) nt -= Mathf.Floor(nt);
                    bool tr = an.IsInTransition(l);
                    AnimatorStateInfo cur = an.GetCurrentAnimatorStateInfo(l);
                    AnimatorStateInfo tgt = tr ? an.GetNextAnimatorStateInfo(l) : cur;
                    _cChecks++;
                    if (tgt.fullPathHash == e.PHash[l])
                    {
                        if (tr || loop) continue;
                        float diff = (nt - cur.normalizedTime) * len;
                        if (diff < 0.25f && diff > -0.25f) continue;
                        _cDrift++;
                        if (fix && an.HasState(l, e.PHash[l])) an.Play(e.PHash[l], l, nt);
                        continue;
                    }
                    if (tr && cur.fullPathHash == e.PHash[l]) continue;   // already leaving it a little early: tolerated
                    _cMismatch++;
                    if (!fix || !an.HasState(l, e.PHash[l])) continue;
                    an.CrossFade(e.PHash[l], Mathf.Clamp(0.1f / Mathf.Max(cur.length, 0.05f), 0.02f, 0.5f), l, nt);
                    _cFixed++;
                }
            }
            float now = Time.unscaledTime;
            if (now >= _cStatAt)
            {
                if (_cStatAt > 0f && _cPackets > 0)
                    BtcLog.Info("AnimNet", "client 30 s" + (ObserveOnly ? " (observe)" : "") + ": packets=" + _cPackets + " records=" + _cRecords + " stale=" + _cStale
                        + " delay=" + (_cDelaySum / _cPackets) + " ms checks=" + _cChecks + " mismatch=" + _cMismatch + " (" + (_cChecks > 0 ? (100f * _cMismatch / _cChecks).ToString("0.0") : "0") + "%) fixed=" + _cFixed + " drift=" + _cDrift);
                DevLast = "packets=" + _cPackets + " checks=" + _cChecks + " mismatch=" + _cMismatch + " fixed=" + _cFixed + " drift=" + _cDrift + " stale=" + _cStale + " delay=" + (_cPackets > 0 ? _cDelaySum / _cPackets : 0);
                _cStatAt = now + 30f; _cPackets = 0; _cRecords = 0; _cChecks = 0; _cMismatch = 0; _cFixed = 0; _cDrift = 0; _cDelaySum = 0; _cStale = 0;
                if (ByNet.Count > 0) Sweep();
            }
        }

        /// <summary>Last finished 30 s client window (DEV lab reads it).</summary>
        internal static string DevLast = "";
        internal static string DevNow()
        {
            return "packets=" + _cPackets + " checks=" + _cChecks + " mismatch=" + _cMismatch + " fixed=" + _cFixed + " drift=" + _cDrift + " stale=" + _cStale + " delay=" + (_cPackets > 0 ? _cDelaySum / _cPackets : 0)
                + " | host records=" + _hRecords + " bytes=" + _hBytes + " | " + DevSides();
        }
        // per side (index 0 = local player's units, 1 = others): host records / observer misses, client records / params applied
        private static readonly int[] _hSide = new int[2], _hObsMiss = new int[2], _cSide = new int[2], _cX = new int[2];
        private static int SideOf(Entry e)
        {
            if (e.Side == 0)
            {
                Brix.Game.User me = Brix.Game.User.LocalUser;
                if (me == null || e.Ua == null) return 2;
                e.Side = Brix.Game.Affiliation.IsOwnedBy(e.Ua.gameObject, me) ? 1 : 2;
            }
            return e.Side;
        }
        internal static string DevSides()
        {
            return "own/other host rec=" + _hSide[0] + "/" + _hSide[1] + " obsMiss=" + _hObsMiss[0] + "/" + _hObsMiss[1]
                + " client rec=" + _cSide[0] + "/" + _cSide[1] + " params=" + _cX[0] + "/" + _cX[1];
        }
        internal static void DevReset() { for (int i = 0; i < 2; i++) { _hSide[i] = 0; _hObsMiss[i] = 0; _cSide[i] = 0; _cX[i] = 0; } _cPackets = 0; _cRecords = 0; _cChecks = 0; _cMismatch = 0; _cFixed = 0; _cDrift = 0; _cDelaySum = 0; _cStale = 0; _hRecords = 0; _hBytes = 0; _hPackets = 0; }

        private static readonly List<uint> Dead = new List<uint>();
        private static void Sweep()
        {
            Dead.Clear();
            foreach (KeyValuePair<uint, Entry> kv in ByNet) if (kv.Value.Ua == null) Dead.Add(kv.Key);
            for (int i = 0; i < Dead.Count; i++) ByNet.Remove(Dead[i]);
            Prune();
        }

        // ------------------------------------------------------------------ client: vanilla paths muted while our packets arrive
        /// <summary>True when vanilla must not touch this animator (client, feature on, a record within the last 3 s).</summary>
        internal static bool Owns(UNetAnimator ua)
        {
            if (_applying || ua == null || NetworkServer.active || ObserveOnly) return false;
            Entry e;
            if (!ByInst.TryGetValue(ua.GetInstanceID(), out e) || e.Seq == 0u) return false;
            return Time.unscaledTime - e.RecvAt < Alive && On;
        }

        /// <summary>Vanilla path for a trigger/reset message the patch already read (same as the vanilla callback).</summary>
        internal static void VanillaQueue(UNetAnimator ua, int code)
        {
            if (Init()) _triggerQueue(ua).Enqueue(code);
        }
    }

    /// <summary>Ticks BtcAnimNet after every animator has been evaluated this frame.</summary>
    internal sealed class BtcAnimNetRunner : MonoBehaviour
    {
        private void LateUpdate()
        {
            try
            {
                if (NetworkServer.active) BtcAnimNet.HostTick();
                else BtcAnimNet.ClientTick();
            }
            catch (Exception e) { BtcLog.Error("AnimNet", "tick: " + e.Message); }
        }
    }
}
