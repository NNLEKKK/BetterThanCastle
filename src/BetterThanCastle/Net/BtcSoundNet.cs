using System;
using System.Collections.Generic;
using Brix.Audio;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Network;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// Unit sounds like the host (BC ON, new-with-new). Lab snd-mp measured three vanilla gaps on a client:
    /// 1) the sound set of a worker comes from Occupation.CurrentJob, which is never sent: the client guesses it from its own toolbag,
    ///    which lags behind kit changes (a worker that dropped its sword kept the knight voice); the Wwise "Profession" switch is set
    ///    once in Occupation.Start and never follows a kit change, on the host too;
    /// 2) many sounds are played only by host-side code (aiming, spotting, arrow hits, kills, thoughts, rack/workbench queue sounds);
    /// 3) the host hears everything, also units in the fog of war.
    /// Fixes: the host sends job + occupation per unit (on change and every 2 s) and the sounds of host-only code (event name ids,
    /// only to connections that observe the emitter, unreliable, batched per frame); the client applies the host job, matches relayed
    /// sounds 1:1 against the same sounds it played itself (no doubles), and everyone mutes sounds of objects their faction cannot see.
    /// </summary>
    internal static class BtcSoundNet
    {
        internal const short MsgBatch = 131, MsgHello = 132;
        private const int Proto = 1;
        private const float JobRefresh = 2f, Match = 1f;
        private const int FlushAt = 1100;

        internal static bool On { get { return BtcNetworkGate.Enabled && BtcConfig.SoundSync != null && BtcConfig.SoundSync.Value; } }
        internal static bool FogOn { get { return BtcNetworkGate.Enabled && BtcConfig.FogSounds != null && BtcConfig.FogSounds.Value; } }

        /// <summary>Host: set every frame, read by the relay scope prefixes (hot: SoundController queues).</summary>
        internal static bool HostRelay;
        /// <summary>Host: &gt;0 while host-only code that plays unit sounds runs.</summary>
        internal static int Relay;
        private static bool _relaying;   // client: playing a relayed sound (skip the local matching)
        internal static bool DevRelaying { get { return _relaying; } }
        private static bool _fog;        // cached FogOn for the play prefix, refreshed once per frame
        private static int _fogFrame = -1;

        private static AccessTools.FieldRef<NetworkIdentity, List<NetworkConnection>> _observers;
        private static AccessTools.FieldRef<Occupation, Occupation.Type> _occField;
        private static bool _init, _initOk;

        private static bool Init()
        {
            if (_init) return _initOk;
            _init = true;
            try
            {
                _observers = AccessTools.FieldRefAccess<NetworkIdentity, List<NetworkConnection>>("m_Observers");
                _occField = AccessTools.FieldRefAccess<Occupation, Occupation.Type>("_currentOccupation");
                _initOk = true;
            }
            catch (Exception e) { BtcLog.Error("SoundNet", "init: " + e.Message); }
            return _initOk;
        }

        // ------------------------------------------------------------------ string table (event names, switch groups/values)
        private static readonly Dictionary<string, int> Ids = new Dictionary<string, int>();
        private static readonly List<string> Names = new List<string>();

        private static int IdOf(string s)
        {
            int id;
            if (Ids.TryGetValue(s, out id)) return id;
            id = Names.Count;
            Names.Add(s);
            Ids[s] = id;
            return id;
        }

        // ------------------------------------------------------------------ session wiring (BtcNetManager, BC ON paths only)
        private sealed class Peer
        {
            internal NetworkConnection Conn;
            internal readonly NetworkWriter S = new NetworkWriter();   // sound records of this frame (no header)
            internal readonly NetworkWriter J = new NetworkWriter();   // job records of this frame (no header)
            internal readonly NetworkWriter W = new NetworkWriter();
            internal readonly List<byte> DefSent = new List<byte>();
            internal readonly List<ushort> Uses = new List<ushort>();
            internal int SN, JN;
            internal float FullAt;
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
            catch (Exception e) { BtcLog.Error("SoundNet", "server: " + e.Message); }
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
                BtcLog.Info("SoundNet", "client ready, unit sounds like the host " + (On ? "requested" : "off (config)"));
            }
            catch (Exception e) { BtcLog.Error("SoundNet", "client: " + e.Message); }
        }

        private static void Runner(UNetManager m)
        {
            if (m != null && m.GetComponent<BtcSoundNetRunner>() == null) m.gameObject.AddComponent<BtcSoundNetRunner>();
        }

        private static void OnHello(NetworkMessage nm)
        {
            try
            {
                IntegerMessage im = nm.ReadMessage<IntegerMessage>();
                if (nm.conn == null || im == null || im.value < Proto) return;
                Peer p;
                if (!Peers.TryGetValue(nm.conn.connectionId, out p)) { p = new Peer(); Peers[nm.conn.connectionId] = p; PeerList.Add(p); }
                p.Conn = nm.conn; p.S.SeekZero(); p.J.SeekZero(); p.SN = 0; p.JN = 0; p.FullAt = 0f;
                p.DefSent.Clear(); p.Uses.Clear();
                BtcLog.Info("SoundNet", "host: conn " + nm.conn.connectionId + " gets unit sounds like the host");
            }
            catch (Exception e) { BtcLog.Error("SoundNet", "hello: " + e.Message); }
        }

        // ------------------------------------------------------------------ units (Occupation registry, both sides)
        private static readonly List<Occupation> Units = new List<Occupation>();
        private static readonly HashSet<int> UnitIds = new HashSet<int>();
        private static readonly List<Occupation> Dirty = new List<Occupation>();
        private static readonly Dictionary<int, int> SwitchSet = new Dictionary<int, int>();   // instance id -> occupation type set as the Wwise switch

        internal static void OnOccupationStart(Occupation o)
        {
            if (o == null) return;
            HealEmitter(o.gameObject);
            if (!UnitIds.Add(o.GetInstanceID())) return;
            Units.Add(o);
            if (NetworkServer.active) { if (On) SyncSwitch(o, o.CurrentOccupation); }
            else ApplyKnown(o);
        }

        /// <summary>Host: a kit or occupation change (SetOccupation / UpdateOccupationTool postfix).</summary>
        internal static void OnHostOccupationChanged(Occupation o)
        {
            if (o == null || !NetworkServer.active || !On) return;
            SyncSwitch(o, o.CurrentOccupation);
            if (PeerList.Count > 0 && !Dirty.Contains(o)) Dirty.Add(o);
        }

        /// <summary>The Wwise "Profession" switch follows the occupation (vanilla sets it once in Occupation.Start).</summary>
        private static void SyncSwitch(Occupation o, Occupation.Type t)
        {
            // Wwise auto-registers an object on its first call; an inactive one (a unit spawned or respawned, SetOccupation runs before
            // it is activated) gets a temporary AkAutoObject instead of an AkGameObj, so its position stays at 0,0,0 for good and the unit
            // is silent (lab snd-fog). Occupation.Start syncs the switch once the unit is active.
            if (!o.gameObject.activeInHierarchy) return;
            int id = o.GetInstanceID(), was;
            if (SwitchSet.TryGetValue(id, out was) && was == (int)t) return;
            SwitchSet[id] = (int)t;
            if (!SoundEngine.EngineIsReady) return;
            SoundEngine.SetSwitch("Profession", Enum.GetName(typeof(Occupation.Type), t), o.gameObject);
        }

        /// <summary>A unit Wwise knows without an AkGameObj (registered while inactive) plays every sound at 0,0,0: give it the component.</summary>
        private static void HealEmitter(GameObject go)
        {
            if (!SoundEngine.EngineIsReady || !go.activeInHierarchy || go.GetComponent<AkGameObj>() != null) return;
            if (!AkSoundEngine.IsAkGameObjectRegistered(go)) return;   // the first Wwise call adds the component by itself
            go.AddComponent<AkGameObj>();
            _healed++;
            BtcLog.Info("SoundNet", "unit " + go.name + " had no sound position (registered while inactive), fixed; total " + _healed);
        }
        private static int _healed;

        private static void PruneUnits()
        {
            for (int i = Units.Count - 1; i >= 0; i--)
                if (Units[i] == null) Units.RemoveAt(i);
            if (UnitIds.Count > Units.Count + 64)
            {
                UnitIds.Clear(); SwitchSet.Clear();
                for (int i = 0; i < Units.Count; i++) UnitIds.Add(Units[i].GetInstanceID());
            }
        }

        // ------------------------------------------------------------------ host
        private static float _statAt, _pruneAt;
        private static int _hSounds, _hJobs, _hBytes, _hPackets;
        private static string _swGroup, _swData;
        private static GameObject _swEmitter;

        /// <summary>Host, inside a relay scope: a SoundController queue sets a switch right before its play on the same emitter.</summary>
        internal static void OnHostSwitch(string group, string data, GameObject emitter)
        {
            if (Relay <= 0 || !HostRelay) return;
            _swGroup = group; _swData = data; _swEmitter = emitter;
        }

        private static void HostCapture(string name, GameObject emitter)
        {
            string g = null, d = null;
            if (_swEmitter != null && _swEmitter == emitter) { g = _swGroup; d = _swData; }
            _swEmitter = null;
            if (emitter == null || !Init()) return;
            NetworkIdentity ni = emitter.GetComponent<NetworkIdentity>();
            if (ni == null) ni = emitter.GetComponentInParent<NetworkIdentity>();
            if (ni == null) return;
            uint nid = ni.netId.Value;
            List<NetworkConnection> obs = nid != 0u ? _observers(ni) : null;
            if (obs == null || obs.Count == 0)
            {
                // spawn and arrow-shot sounds play before UNET has built the observers of the new object: retry for 0.5 s
                if (Pending.Count < 256) Pending.Add(new PendingSound { Ni = ni, Name = name, G = g, D = d, At = Time.unscaledTime });
                return;
            }
            Emit(nid, obs, name, g, d);
        }

        private struct PendingSound { internal NetworkIdentity Ni; internal string Name, G, D; internal float At; }
        private static readonly List<PendingSound> Pending = new List<PendingSound>();

        private static void RetryPending(float now)
        {
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                PendingSound s = Pending[i];
                List<NetworkConnection> obs = s.Ni != null && s.Ni.netId.Value != 0u ? _observers(s.Ni) : null;
                if (obs != null && obs.Count > 0) { Emit(s.Ni.netId.Value, obs, s.Name, s.G, s.D); Pending.RemoveAt(i); }
                else if (s.Ni == null || now - s.At > 0.5f) Pending.RemoveAt(i);
            }
        }

        private static void Emit(uint nid, List<NetworkConnection> obs, string name, string g, string d)
        {
            for (int c = 0; c < PeerList.Count; c++)
            {
                Peer p = PeerList[c];
                if (p.Conn == null || !obs.Contains(p.Conn)) continue;
                NetworkWriter w = p.S;
                w.WritePackedUInt32(nid);
                w.WritePackedUInt32(g != null ? 1u : 0u);
                Str(p, w, name);
                if (g != null) { Str(p, w, g); Str(p, w, d ?? string.Empty); }
                p.SN++;
                _hSounds++;
                if (w.Position + p.J.Position >= FlushAt) Flush(p);
            }
        }

        // string ref: packed (id << 1 | hasText) [+ text]; the text rides along the first 3 uses per peer and every 128th after that
        // (unreliable channel: a client that lost all of them drops that sound until the next repeat)
        private static void Str(Peer p, NetworkWriter w, string s)
        {
            int id = IdOf(s);
            while (p.DefSent.Count <= id) { p.DefSent.Add(0); p.Uses.Add(0); }
            bool def = p.DefSent[id] < 3 || (++p.Uses[id] & 127) == 0;
            if (def && p.DefSent[id] < 3) p.DefSent[id]++;
            w.WritePackedUInt32(((uint)id << 1) | (def ? 1u : 0u));
            if (def) w.Write(s);
        }

        private static void WriteJob(Peer p, uint nid, Occupation o)
        {
            p.J.WritePackedUInt32(nid);
            p.J.Write((byte)o.CurrentJob);
            p.J.Write((byte)o.CurrentOccupation);
            p.JN++;
            _hJobs++;
            if (p.S.Position + p.J.Position >= FlushAt) Flush(p);
        }

        internal static void HostTick()
        {
            HostRelay = PeerList.Count > 0 && On;
            if (!NetworkServer.active) { HostRelay = false; return; }
            float now = Time.unscaledTime;
            if (now >= _pruneAt) { _pruneAt = now + 10f; PruneUnits(); }
            if (PeerList.Count == 0 || !On || !Init()) { Dirty.Clear(); Pending.Clear(); return; }
            for (int i = PeerList.Count - 1; i >= 0; i--)
            {
                Peer p = PeerList[i];
                if (p.Conn == null || !p.Conn.isConnected || !NetworkServer.connections.Contains(p.Conn))
                {
                    PeerList.RemoveAt(i);
                    if (p.Conn != null) Peers.Remove(p.Conn.connectionId);
                }
            }
            if (Pending.Count > 0) RetryPending(now);
            for (int c = 0; c < PeerList.Count; c++)
            {
                Peer p = PeerList[c];
                bool full = now >= p.FullAt;
                if (full) p.FullAt = now + JobRefresh;
                List<Occupation> list = full ? Units : Dirty;
                for (int i = 0; i < list.Count; i++)
                {
                    Occupation o = list[i];
                    if (o == null) continue;
                    NetworkIdentity ni = o.GetComponent<NetworkIdentity>();
                    if (ni == null || ni.netId.Value == 0u) continue;
                    List<NetworkConnection> obs = _observers(ni);
                    if (obs == null || !obs.Contains(p.Conn)) continue;
                    WriteJob(p, ni.netId.Value, o);
                }
                if (p.SN > 0 || p.JN > 0) Flush(p);
            }
            Dirty.Clear();
            if (now >= _statAt)
            {
                if (_statAt > 0f && _hPackets > 0)
                    BtcLog.Info("SoundNet", "host 30 s: peers=" + PeerList.Count + " sounds=" + _hSounds + " jobs=" + _hJobs + " packets=" + _hPackets + " (" + (_hBytes / 30 / 1024f).ToString("0.00") + " KB/s)");
                _statAt = now + 30f; _hSounds = 0; _hJobs = 0; _hBytes = 0; _hPackets = 0;
            }
        }

        // message: sound records (packed nid, 0 = end), then job records (packed nid, 0 = end)
        private static void Flush(Peer p)
        {
            NetworkWriter w = p.W;
            try
            {
                w.StartMessage(MsgBatch);
                if (p.S.Position > 0) w.Write(p.S.AsArray(), p.S.Position);
                w.WritePackedUInt32(0u);
                if (p.J.Position > 0) w.Write(p.J.AsArray(), p.J.Position);
                w.WritePackedUInt32(0u);
                w.FinishMessage();
                _hBytes += w.Position; _hPackets++;
                p.Conn.SendWriter(w, Channels.DefaultUnreliable);
            }
            catch (Exception e) { BtcLog.Error("SoundNet", "send: " + e.Message); }
            p.S.SeekZero(); p.J.SeekZero(); p.SN = 0; p.JN = 0;
        }

        // ------------------------------------------------------------------ client
        private static readonly Dictionary<int, string> CNames = new Dictionary<int, string>();
        private static readonly Dictionary<uint, ushort> HostJobs = new Dictionary<uint, ushort>();
        private struct MatchRec { internal float LocalAt, RelayAt; internal int LocalN, RelayN; }
        private static readonly Dictionary<long, MatchRec> Recent = new Dictionary<long, MatchRec>();
        private static bool _clientActive;
        private static float _cStatAt;
        private static int _cPlayed, _cMatchedRelay, _cMatchedLocal, _cUnknown, _cNoObj, _cJobFix;

        private static void ResetClient()
        {
            CNames.Clear(); HostJobs.Clear(); Recent.Clear();
            _clientActive = false;
        }

        private static string ReadStr(NetworkReader r)
        {
            uint v = r.ReadPackedUInt32();
            int id = (int)(v >> 1);
            if ((v & 1u) != 0u) { string s = r.ReadString(); CNames[id] = s; return s; }
            string k;
            return CNames.TryGetValue(id, out k) ? k : null;
        }

        private static void OnBatch(NetworkMessage nm)
        {
            if (NetworkServer.active) return;
            try
            {
                NetworkReader r = nm.reader;
                _clientActive = true;
                float now = Time.unscaledTime;
                for (;;)
                {
                    uint nid = r.ReadPackedUInt32();
                    if (nid == 0u) break;
                    uint flags = r.ReadPackedUInt32();
                    string name = ReadStr(r), g = null, d = null;
                    if ((flags & 1u) != 0u) { g = ReadStr(r); d = ReadStr(r); }
                    if (name == null || ((flags & 1u) != 0u && (g == null || d == null))) { _cUnknown++; continue; }
                    if (!On) continue;
                    GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(nid));
                    if (go == null || !go.activeInHierarchy) { _cNoObj++; continue; }   // inactive: Wwise would register it without a position
                    if (MatchedLocal(nid, name, now)) { _cMatchedLocal++; continue; }
                    _relaying = true;
                    try
                    {
                        if (g != null) SoundEngine.SetSwitch(g, d, go);
                        SoundEngine.Play(name, go);
                    }
                    finally { _relaying = false; }
                    _cPlayed++;
                }
                for (;;)
                {
                    uint nid = r.ReadPackedUInt32();
                    if (nid == 0u) break;
                    byte job = r.ReadByte(), occ = r.ReadByte();
                    HostJobs[nid] = (ushort)(job << 8 | occ);
                    if (!On) continue;
                    GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(nid));
                    Occupation o = go != null ? go.GetComponent<Occupation>() : null;
                    if (o != null) Apply(o, (Occupation.Job)job, (Occupation.Type)occ);
                }
                if (now >= _cStatAt)
                {
                    if (_cStatAt > 0f)
                        BtcLog.Info("SoundNet", "client 30 s: played=" + _cPlayed + " doubles skipped=" + _cMatchedLocal + "+" + _cMatchedRelay + " unknown=" + _cUnknown + " noObj=" + _cNoObj + " jobFix=" + _cJobFix);
                    _cStatAt = now + 30f; _cPlayed = 0; _cMatchedLocal = 0; _cMatchedRelay = 0; _cUnknown = 0; _cNoObj = 0; _cJobFix = 0;
                    if (Recent.Count > 4096) Recent.Clear();
                }
            }
            catch (Exception e) { BtcLog.Error("SoundNet", "batch: " + e.Message); }
        }

        private static long Key(uint nid, string name) { return ((long)nid << 32) | (uint)name.GetHashCode(); }

        // 1:1 matching: a relayed sound and the same sound the client played itself on the same object within 1 s are one sound
        // (0.6 s let an equip sound of a freshly spawned unit through twice in lab snd-mp)
        private static bool MatchedLocal(uint nid, string name, float now)
        {
            long k = Key(nid, name);
            MatchRec m;
            Recent.TryGetValue(k, out m);
            if (m.LocalN > 0 && now - m.LocalAt < Match) { m.LocalN--; Recent[k] = m; return true; }
            if (now - m.RelayAt >= Match) m.RelayN = 0;
            m.RelayN++; m.RelayAt = now; m.LocalN = 0;
            Recent[k] = m;
            return false;
        }

        private static bool MatchedRelay(uint nid, string name, float now)
        {
            long k = Key(nid, name);
            MatchRec m;
            Recent.TryGetValue(k, out m);
            if (m.RelayN > 0 && now - m.RelayAt < Match) { m.RelayN--; Recent[k] = m; return true; }
            if (now - m.LocalAt >= Match) m.LocalN = 0;
            m.LocalN++; m.LocalAt = now;
            Recent[k] = m;
            return false;
        }

        private static void ApplyKnown(Occupation o)
        {
            if (!On || !_clientActive) return;
            NetworkIdentity ni = o.GetComponent<NetworkIdentity>();
            ushort v;
            if (ni != null && HostJobs.TryGetValue(ni.netId.Value, out v)) Apply(o, (Occupation.Job)(v >> 8), (Occupation.Type)(v & 0xff));
        }

        private static void Apply(Occupation o, Occupation.Job job, Occupation.Type occ)
        {
            if (!Init()) return;
            bool changed = false;
            if (o.CurrentJob != job) { o.CurrentJob = job; changed = true; }
            if (_occField(o) != occ) { o.CurrentOccupation = occ; changed = true; }
            else if (changed) o.OccupationChanged.Invoke(occ);
            if (changed) _cJobFix++;
            SyncSwitch(o, occ);
        }

        /// <summary>Client: Occupation.UpdateOccupationTool prefix. false = the host value is known, the local toolbag guess is skipped.</summary>
        internal static bool ClientToolChanged(Occupation o)
        {
            if (NetworkServer.active || !_clientActive || !On || o == null) return true;
            NetworkIdentity ni = o.GetComponent<NetworkIdentity>();
            ushort v;
            if (ni == null || !HostJobs.TryGetValue(ni.netId.Value, out v)) return true;
            Apply(o, (Occupation.Job)(v >> 8), (Occupation.Type)(v & 0xff));
            return false;
        }

        // ------------------------------------------------------------------ every play (SoundEngine.Play(string, GameObject) prefix)
        /// <summary>false = do not play here (fog of war, or the host already sent this sound).</summary>
        internal static bool OnPlay(string name, GameObject emitter)
        {
            if (emitter == null || string.IsNullOrEmpty(name)) return true;
            if (Relay > 0 && HostRelay) HostCapture(name, emitter);
            int fr = Time.frameCount;
            if (fr != _fogFrame) { _fogFrame = fr; _fog = FogOn; }   // also single player (no session runner there)
            if (_fog && !Audible(emitter)) return false;
            if (_relaying || !_clientActive || NetworkServer.active) return true;   // every own sound is noted: the first relay of a name can come after the local one
            NetworkIdentity ni = emitter.GetComponent<NetworkIdentity>();
            if (ni == null) ni = emitter.GetComponentInParent<NetworkIdentity>();
            if (ni == null) return true;
            if (MatchedRelay(ni.netId.Value, name, Time.unscaledTime)) { _cMatchedRelay++; return false; }
            return true;
        }

        /// <summary>Fog of war: an object the local faction cannot see makes no sound (replays and spectators hear everything).</summary>
        internal static bool Audible(GameObject emitter)
        {
            if (emitter == null || BtcReplay.Active) return true;
            User u = User.LocalUser;
            Faction f = u != null ? u.faction : null;
            if (f == null) return true;
            GameComponent gc = emitter.GetComponent<GameComponent>();
            if (gc == null) gc = emitter.GetComponentInParent<GameComponent>();
            if (gc == null) return true;
            LocalFactionVision v = gc.vision;
            if (v == null) return true;
            return v.IsVisibleForFaction(f, true);
        }

        internal static void FrameTick()
        {
            if (!NetworkServer.active) HostRelay = false;
        }
    }

    internal sealed class BtcSoundNetRunner : MonoBehaviour
    {
        private void LateUpdate()
        {
            try
            {
                BtcSoundNet.FrameTick();
                if (NetworkServer.active) BtcSoundNet.HostTick();
            }
            catch (Exception e) { BtcLog.Error("SoundNet", "tick: " + e.Message); }
        }
    }
}
