using System;
using System.Collections.Generic;
using Brix.Game;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Match statistics on the wire (BetterConnection ON only; OFF = no handler registered, nothing sent, the handler ignores messages).
    /// Msg 101 host->client, kinds: 0 live values (1 Hz), 1 time-series sample (every 5 s; history streamed from index 0 through a
    /// per-connection cursor, so late joiners get the whole match). Sent only to connections that said BTC hello (mod clients).
    /// Role filter at the host: spectators (BtcSpectate.IsSpec or spectator faction) get every faction, a player only the own faction.
    /// Payload: byte kind, int matchId, packed idx, float t, byte nFac, per faction: packed id+1, packed nv, nv packed values.
    /// Kind 2 (per-item breakdown, sent on change): same header with nFac 1, then packed id+1, byte category, byte n, n x (string key, packed count).
    /// </summary>
    internal static class BtcMatchStatsNet
    {
        internal const short Msg = 101;
        private const int SamplesPerPump = 4;

        private sealed class Cur
        {
            internal int Next;
            internal int Role;
            internal int Fac;
            internal bool Seen;
            internal readonly Dictionary<int, int> DetSent = new Dictionary<int, int>();
        }

        private static readonly Dictionary<int, Cur> Cursors = new Dictionary<int, Cur>();
        private static readonly Dictionary<int, User> UserByConn = new Dictionary<int, User>();
        private static readonly List<int> Drop = new List<int>();
        private static readonly StatsMsg Out = new StatsMsg();
        private static float _next, _nextLive;
        private static int _sent;

        internal static void OnMatchReset()
        {
            Cursors.Clear();
            _sent = 0;
        }

        // ------------------------------------------------------------------ client
        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                UNetManager m = NetworkManager.singleton as UNetManager;
                NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
                if (c == null) return;
                c.UnregisterHandler(Msg);
                c.RegisterHandler(Msg, OnMsgAtClient);
            }
            catch (Exception e) { BtcLog.Error("MatchStats", "RegisterHandlers: " + e); }
        }

        private static void OnMsgAtClient(NetworkMessage nm)
        {
            try
            {
                ApplyAtClient(nm.ReadMessage<StatsMsg>());
            }
            catch (Exception e) { BtcLog.Error("MatchStats", "OnMsgAtClient: " + e); }
        }

        private static int _legacyMatch;

        internal static void ResetLegacy() { _legacyMatch = 0; }

        /// <summary>
        /// Replays recorded before 2026-11-05 20:00 carry the stats stream as msg 84 (id moved to 101 because of the BtcRecipes collision).
        /// True when this msg 84 of such a replay is a stats message (parsed + same match id as the first one), so the feeder can retag it.
        /// </summary>
        internal static bool LegacyStats(byte[] raw, int len)
        {
            try
            {
                if (BtcReplay.Mode != BtcReplay.State.Playing || len < 12) return false;
                string f = BtcReplay.FeedFile;
                if (string.IsNullOrEmpty(f)) return false;
                string n = System.IO.Path.GetFileName(f);
                // yyyy-MM-dd_HH-mm-ss_...: the file name sorts like time
                if (n.Length < 19 || string.CompareOrdinal(n.Substring(0, 19), "2026-11-05_20-00-00") >= 0) return false;
                NetworkReader rd = new NetworkReader(raw);
                rd.ReadUInt16();
                rd.ReadUInt16();
                StatsMsg m = new StatsMsg();
                m.Deserialize(rd);
                if (m.Kind > 2 || m.MatchId == 0 || m.N > 32 || rd.Position > len) return false;
                if (_legacyMatch == 0) _legacyMatch = m.MatchId;
                return m.MatchId == _legacyMatch;
            }
            catch (Exception) { return false; }
        }
        /// <summary>Replay seek: re-apply a recorded raw stats message (header + payload as stored in the stream).</summary>
        internal static void ApplyRaw(byte[] raw, int len)
        {
            try
            {
                NetworkReader rd = new NetworkReader(raw);
                rd.ReadUInt16();
                rd.ReadUInt16();
                StatsMsg m = new StatsMsg();
                m.Deserialize(rd);
                ApplyAtClient(m);
            }
            catch (Exception e) { BtcLog.Error("MatchStats", "ApplyRaw: " + e.Message); }
        }

        private static void ApplyAtClient(StatsMsg m)
        {
            try
            {
                bool replay = BtcReplay.Mode == BtcReplay.State.Playing;
                if ((!BtcNetworkGate.Enabled && !replay) || (NetworkServer.active && !replay) || m == null || m.MatchId == 0) return;
                BtcMsStore r = BtcMatchStats.Remote;
                if (r.MatchId != m.MatchId)
                {
                    r.Clear(m.MatchId);
                    BtcLog.Info("MatchStats", "host stats stream match=" + m.MatchId);
                }
                if (m.Kind == 2)
                {
                    if (m.N < 1 || m.Cat < 0 || m.Cat >= BtcMatchStats.ND) return;
                    BtcMsFaction df = r.Get(m.Ids[0], true);
                    df.Det[m.Cat].Clear();
                    for (int k = 0; k < m.DN; k++) if (!string.IsNullOrEmpty(m.DKeys[k])) df.Det[m.Cat][m.DKeys[k]] = m.DVals[k];
                    df.DetVer[m.Cat]++;
                    r.Version++;
                    return;
                }
                for (int i = 0; i < m.N; i++)
                {
                    BtcMsFaction f = r.Get(m.Ids[i], true);
                    int[] row = m.Rows[i];
                    if (m.Kind == 0)
                    {
                        Array.Clear(f.V, 0, f.V.Length);
                        Array.Copy(row, f.V, Mathf.Min(row.Length, f.V.Length));
                    }
                    else
                    {
                        int[] v = new int[BtcMatchStats.NV];
                        Array.Copy(row, v, Mathf.Min(row.Length, v.Length));
                        f.AddSample(m.Idx, m.T, v);
                    }
                }
                if (m.Kind == 0) { r.T = m.T; BtcMatchStats.LastRemote = Time.unscaledTime; }
                r.Version++;
            }
            catch (Exception e) { BtcLog.Error("MatchStats", "OnMsgAtClient: " + e); }
        }

        // ------------------------------------------------------------------ replay (file only, independent of BetterConnection)
        private static readonly Cur RecCur = new Cur { Role = 2, Fac = int.MinValue };
        private static NetworkConnection _recConn;
        private static float _recNext, _recLive, _recDetFull;

        /// <summary>Playback: the replay's fake client gets the stats handler (data comes from the file, not from the wire).</summary>
        internal static void RegisterReplayHandler(NetworkClient c)
        {
            try
            {
                if (c == null) return;
                c.UnregisterHandler(Msg);
                c.RegisterHandler(Msg, OnMsgAtClient);
            }
            catch (Exception e) { BtcLog.Error("MatchStats", "RegisterReplayHandler: " + e); }
        }

        /// <summary>Recording host: write the full stats stream (all factions, as for a spectator) into the replay through its recording connection.</summary>
        private static void RecTick()
        {
            NetworkConnection c = BtcReplay.RecMainConn;
            if (c == null || !NetworkServer.active || !BtcMatchStats.Active || !BtcMatchStats.Authoritative) { _recConn = null; return; }
            float now = Time.unscaledTime;
            if (now < _recNext) return;
            _recNext = now + 0.1f;
            try
            {
                if (!ReferenceEquals(c, _recConn)) { _recConn = c; RecCur.Next = 0; RecCur.DetSent.Clear(); _recLive = 0f; _recDetFull = now + 10f; }
                if (now >= _recLive)
                {
                    _recLive = now + 1f;
                    // breakdown is re-sent in full every 10 s so a rewind lands on a recent complete state
                    if (now >= _recDetFull) { _recDetFull = now + 10f; RecCur.DetSent.Clear(); }
                    if (Build(0, 0, BtcMatchStats.Local.T, 2, int.MinValue)) c.Send(Msg, Out);
                    SendDetails(c, RecCur, 2, int.MinValue);
                }
                int sent = 0;
                while (RecCur.Next < BtcMatchStats.SampleCount && sent < SamplesPerPump)
                {
                    float t;
                    if (BuildSample(RecCur.Next, 2, int.MinValue, out t)) { c.Send(Msg, Out); sent++; }
                    RecCur.Next++;
                }
            }
            catch (Exception e) { BtcLog.Error("MatchStats", "rec tick: " + e); _recNext = now + 5f; }
        }

        // ------------------------------------------------------------------ host
        /// <summary>Called every frame from BtcMatchStats.Tick (after the collector).</summary>
        internal static void Tick()
        {
            RecTick();
            if (!BtcNetworkGate.Enabled || !NetworkServer.active || !BtcMatchStats.Active || !BtcMatchStats.Authoritative) return;
            float now = Time.unscaledTime;
            if (now < _next) return;
            _next = now + 0.1f;
            try
            {
                bool live = now >= _nextLive;
                if (live)
                {
                    _nextLive = now + 1f;
                    RefreshUsers();
                }
                foreach (KeyValuePair<int, Cur> kv in Cursors) kv.Value.Seen = false;
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    if (c == null || !c.isConnected || BtcNetManager.IsLocalConnection(c) || !BtcSpectate.SaidHello(c)) continue;
                    int fac;
                    int role = Role(c, out fac);
                    if (role == 0) continue;
                    Cur cur;
                    if (!Cursors.TryGetValue(c.connectionId, out cur)) { cur = new Cur { Role = role, Fac = fac }; Cursors[c.connectionId] = cur; }
                    if (cur.Role != role || cur.Fac != fac) { cur.Role = role; cur.Fac = fac; cur.Next = 0; cur.DetSent.Clear(); }
                    cur.Seen = true;
                    if (live && Build(0, 0, BtcMatchStats.Local.T, role, fac)) Send(c);
                    if (live) SendDetails(c, cur, role, fac);
                    int sent = 0;
                    while (cur.Next < BtcMatchStats.SampleCount && sent < SamplesPerPump)
                    {
                        float t;
                        if (BuildSample(cur.Next, role, fac, out t)) { Send(c); sent++; }
                        cur.Next++;
                    }
                }
                Drop.Clear();
                foreach (KeyValuePair<int, Cur> kv in Cursors) if (!kv.Value.Seen) Drop.Add(kv.Key);
                for (int i = 0; i < Drop.Count; i++) Cursors.Remove(Drop[i]);
            }
            catch (Exception e) { BtcLog.Error("MatchStats", "net tick: " + e); _next = now + 5f; }
        }

        /// <summary>Kind 2: per-item breakdown of one faction and one category, sent only when that category changed (max 30 entries).</summary>
        private static void SendDetails(NetworkConnection c, Cur cur, int role, int fac)
        {
            List<BtcMsFaction> fs = BtcMatchStats.Local.Factions;
            for (int i = 0; i < fs.Count; i++)
            {
                BtcMsFaction f = fs[i];
                if (!Visible(f, f.V, role, fac)) continue;
                for (int cat = 0; cat < BtcMatchStats.ND; cat++)
                {
                    int key = f.Id * 8 + cat, ver;
                    if (f.DetVer[cat] == 0 || (cur.DetSent.TryGetValue(key, out ver) && ver == f.DetVer[cat])) continue;
                    cur.DetSent[key] = f.DetVer[cat];
                    Prepare(2, 0, BtcMatchStats.Local.T, 1);
                    Out.N = 1;
                    Out.Ids[0] = f.Id;
                    Out.Cat = cat;
                    Out.DN = 0;
                    foreach (KeyValuePair<string, int> kv in f.Det[cat])
                    {
                        if (Out.DN >= 30) break; // keeps a message under ~1 KB
                        Out.DKeys[Out.DN] = kv.Key;
                        Out.DVals[Out.DN] = kv.Value;
                        Out.DN++;
                    }
                    Send(c);
                }
            }
        }

        private static void Send(NetworkConnection c)
        {
            c.Send(Msg, Out);
            if (_sent++ == 0) BtcLog.Info("MatchStats", "first stats message to conn " + c.connectionId);
        }

        private static void RefreshUsers()
        {
            UserByConn.Clear();
            User[] us = UnityEngine.Object.FindObjectsOfType<User>();
            for (int i = 0; i < us.Length; i++)
            {
                User u = us[i];
                if (u == null) continue;
                NetworkConnection c = u.connectionToClient;
                if (c != null) UserByConn[c.connectionId] = u;
            }
        }

        /// <summary>0 = nothing, 1 = player (own faction only), 2 = spectator (all).</summary>
        private static int Role(NetworkConnection c, out int fac)
        {
            fac = int.MinValue;
            if (BtcSpectate.IsSpec(c)) return 2;
            User u;
            if (!UserByConn.TryGetValue(c.connectionId, out u) || u == null) return 0;
            Faction f = u.faction;
            if (f == null || f.IsSpectator) return 2;
            if (!BtcMatchStats.Tracks(f)) return 0;
            fac = f.ID;
            return 1;
        }

        private static bool Visible(BtcMsFaction f, int[] v, int role, int fac)
        {
            // empty factions (never had units or crystals) are not sent: keeps a message well under the ~1.2 KB reliable packet
            if (v[BtcMatchStats.V_OWNED] == 0 && v[BtcMatchStats.V_ALIVE] == 0 && v[BtcMatchStats.V_CRYSEC] == 0 && v[BtcMatchStats.V_CRYS] == 0) return false;
            return role == 2 || (role == 1 && f.Id == fac);
        }

        private static void Prepare(int kind, int idx, float t, int cap)
        {
            Out.Kind = kind;
            Out.MatchId = BtcMatchStats.Local.MatchId;
            Out.Idx = idx;
            Out.T = t;
            Out.N = 0;
            if (Out.Ids.Length < cap) { Out.Ids = new int[cap]; Out.Rows = new int[cap][]; }
        }

        private static bool Build(int kind, int idx, float t, int role, int fac)
        {
            List<BtcMsFaction> fs = BtcMatchStats.Local.Factions;
            Prepare(kind, idx, t, fs.Count);
            for (int i = 0; i < fs.Count && Out.N < 10; i++)
            {
                if (!Visible(fs[i], fs[i].V, role, fac)) continue;
                Out.Ids[Out.N] = fs[i].Id;
                Out.Rows[Out.N] = fs[i].V;
                Out.N++;
            }
            return Out.N > 0;
        }

        private static bool BuildSample(int idx, int role, int fac, out float t)
        {
            t = 0f;
            List<BtcMsFaction> fs = BtcMatchStats.Local.Factions;
            Prepare(1, idx, 0f, fs.Count);
            for (int i = 0; i < fs.Count && Out.N < 10; i++)
            {
                int k = fs[i].Find(idx);
                if (k < 0 || !Visible(fs[i], fs[i].Series[k].V, role, fac)) continue;
                BtcMsSample s = fs[i].Series[k];
                t = s.T;
                Out.Ids[Out.N] = fs[i].Id;
                Out.Rows[Out.N] = s.V;
                Out.N++;
            }
            Out.T = t;
            return Out.N > 0;
        }

        internal sealed class StatsMsg : MessageBase
        {
            internal int Kind;
            internal int MatchId;
            internal int Idx;
            internal float T;
            internal int N;
            internal int[] Ids = new int[8];
            internal int[][] Rows = new int[8][];
            // kind 2 (per-item breakdown): one faction (Ids[0]), one category
            internal int Cat, DN;
            internal string[] DKeys = new string[40];
            internal int[] DVals = new int[40];

            public override void Serialize(NetworkWriter w)
            {
                w.Write((byte)Kind);
                w.Write(MatchId);
                w.WritePackedUInt32((uint)Mathf.Max(0, Idx));
                w.Write(T);
                w.Write((byte)N);
                if (Kind == 2)
                {
                    w.WritePackedUInt32((uint)(Ids[0] + 1));
                    w.Write((byte)Cat);
                    w.Write((byte)DN);
                    for (int k = 0; k < DN; k++) { w.Write(DKeys[k] ?? string.Empty); w.WritePackedUInt32((uint)Mathf.Max(0, DVals[k])); }
                    return;
                }
                for (int i = 0; i < N; i++)
                {
                    // faction ids may be -1 (unassigned): shift by one
                    w.WritePackedUInt32((uint)(Ids[i] + 1));
                    int[] row = Rows[i];
                    int nv = row != null ? row.Length : 0;
                    w.WritePackedUInt32((uint)nv);
                    for (int k = 0; k < nv; k++) w.WritePackedUInt32((uint)Mathf.Max(0, row[k]));
                }
            }

            public override void Deserialize(NetworkReader r)
            {
                Kind = r.ReadByte();
                MatchId = r.ReadInt32();
                Idx = (int)r.ReadPackedUInt32();
                T = r.ReadSingle();
                N = r.ReadByte();
                if (N > 32) { N = 0; return; }
                if (Kind == 2)
                {
                    Ids = new int[1];
                    Ids[0] = (int)r.ReadPackedUInt32() - 1;
                    Cat = r.ReadByte();
                    DN = r.ReadByte();
                    if (DN > 40) { N = 0; DN = 0; return; }
                    DKeys = new string[DN];
                    DVals = new int[DN];
                    for (int k = 0; k < DN; k++) { DKeys[k] = r.ReadString(); DVals[k] = (int)r.ReadPackedUInt32(); }
                    return;
                }
                Ids = new int[N];
                Rows = new int[N][];
                for (int i = 0; i < N; i++)
                {
                    Ids[i] = (int)r.ReadPackedUInt32() - 1;
                    int nv = (int)r.ReadPackedUInt32();
                    if (nv > 256) { N = i; return; }
                    int[] row = new int[nv];
                    for (int k = 0; k < nv; k++) row[k] = (int)r.ReadPackedUInt32();
                    Rows[i] = row;
                }
            }
        }
    }
}
