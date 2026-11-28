using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Match statistics over a whole match of several sessions (BtcReplayChainPlay). Every session records its own stats stream from zero (msg 101,
    /// a new match id per load). While session N of a line is open, the end state of sessions 0..N-1 (at their joints, read from their keyframe
    /// caches) is laid under it: counters (losses, trades, gathered, crafted, built, score, actions...) are added, the peak APM is the max, the
    /// live state (alive units, energy, crystals, jobs) stays the open session's; their time series come first with shifted time and sample
    /// index, the per-item breakdowns are summed. Applied inside BtcMatchStatsNet.ApplyAtClient during replay playback only.
    /// </summary>
    internal static class BtcChainStats
    {
        private struct Rec { internal long Ms; internal byte[] Raw; }

        private sealed class End
        {
            internal readonly Dictionary<int, int[]> V = new Dictionary<int, int[]>();
            internal readonly Dictionary<int, List<BtcMsSample>> S = new Dictionary<int, List<BtcMsSample>>();
            internal readonly Dictionary<int, Dictionary<string, int>[]> Det = new Dictionary<int, Dictionary<string, int>[]>();
            internal int MaxIdx = -1;
        }

        private static readonly Dictionary<string, List<KeyValuePair<long, byte[]>>> Recs = new Dictionary<string, List<KeyValuePair<long, byte[]>>>();

        private static string _key;
        private static bool _on;
        private static float _off;
        private static int _idxBase;
        private static int _missing;
        private static readonly Dictionary<int, int[]> CarryV = new Dictionary<int, int[]>();
        private static readonly Dictionary<int, int[]> LastV = new Dictionary<int, int[]>();
        private static readonly Dictionary<int, Dictionary<string, int>[]> CarryDet = new Dictionary<int, Dictionary<string, int>[]>();
        private static readonly Dictionary<int, List<BtcMsSample>> Seeds = new Dictionary<int, List<BtcMsSample>>();

        /// <summary>Sessions before the open one whose stats could not be read (no keyframe cache yet).</summary>
        internal static int Missing { get { return _missing; } }

        private static bool Live(int k)
        {
            return k == BtcMatchStats.V_ALIVE || k == BtcMatchStats.V_ENERGY || k == BtcMatchStats.V_CRYS || k == BtcMatchStats.V_AUTH
                || (k >= BtcMatchStats.V_PROF0 && k < BtcMatchStats.V_PROF0 + BtcMatchStats.NPROF);
        }

        private static bool Playing { get { return BtcReplay.Mode == BtcReplay.State.Playing; } }

        /// <summary>The open session changed (BtcReplayChainPlay): rebuild what lies under it.</summary>
        internal static void OnStep()
        {
            _on = false; _key = null; _missing = 0;
            CarryV.Clear(); LastV.Clear(); CarryDet.Clear(); Seeds.Clear();
            if (!BtcReplayChainPlay.Active) return;
            BtcReplayChain.Line l = BtcReplayChainPlay.L;
            int i = BtcReplayChainPlay.Step;
            if (i <= 0 || i >= l.S.Count) return;
            int idxBase = 0;
            for (int j = 0; j < i; j++)
            {
                List<KeyValuePair<long, byte[]>> recs = RecsOf(l.S[j].N.D);
                if (recs == null) { _missing++; continue; }
                End e = Decode(recs, (long)(l.S[j].End * 1000f) + 600L);
                float off = l.Off(j);
                foreach (KeyValuePair<int, List<BtcMsSample>> kv in e.S)
                {
                    List<BtcMsSample> into;
                    if (!Seeds.TryGetValue(kv.Key, out into)) { into = new List<BtcMsSample>(); Seeds[kv.Key] = into; }
                    for (int k = 0; k < kv.Value.Count; k++)
                    {
                        BtcMsSample s = kv.Value[k];
                        into.Add(new BtcMsSample { Idx = s.Idx + idxBase, T = s.T + off, V = AddCarry(kv.Key, s.V) });
                    }
                }
                foreach (KeyValuePair<int, int[]> kv in e.V)
                {
                    int[] c;
                    if (!CarryV.TryGetValue(kv.Key, out c)) { c = new int[BtcMatchStats.NV]; CarryV[kv.Key] = c; }
                    int[] v = kv.Value;
                    for (int k = 0; k < BtcMatchStats.NV && k < v.Length; k++)
                    {
                        if (Live(k)) continue;
                        if (k == BtcMatchStats.V_APMPEAK) c[k] = Mathf.Max(c[k], v[k]);
                        else if (k == BtcMatchStats.V_OWNED) c[k] += Mathf.Max(0, v[k] - v[BtcMatchStats.V_ALIVE]);   // units alive at the save are counted again by the next session
                        else c[k] += v[k];
                    }
                    LastV[kv.Key] = (int[])v.Clone();
                }
                foreach (KeyValuePair<int, Dictionary<string, int>[]> kv in e.Det)
                {
                    Dictionary<string, int>[] c;
                    if (!CarryDet.TryGetValue(kv.Key, out c)) { c = NewDet(); CarryDet[kv.Key] = c; }
                    for (int cat = 0; cat < c.Length && cat < kv.Value.Length; cat++)
                        foreach (KeyValuePair<string, int> d in kv.Value[cat]) { int had; c[cat].TryGetValue(d.Key, out had); c[cat][d.Key] = had + d.Value; }
                }
                idxBase += e.MaxIdx + 1;
            }
            _off = l.Off(i);
            _idxBase = idxBase;
            _on = true;
            _key = l.Key + "|" + i;
            BtcLog.Info("ReplayChain", "stats of sessions 1.." + i + " laid under session " + (i + 1) + ": " + CarryV.Count + " factions" + (_missing > 0 ? ", " + _missing + " session(s) without a cache" : ""));
            // the store already holds part of this session's stream: rebuild it with the earlier sessions under it
            BtcReplaySeek.RestoreStatsNow();
        }

        private static Dictionary<string, int>[] NewDet()
        {
            Dictionary<string, int>[] d = new Dictionary<string, int>[BtcMatchStats.ND];
            for (int i = 0; i < d.Length; i++) d[i] = new Dictionary<string, int>();
            return d;
        }

        private static List<KeyValuePair<long, byte[]>> RecsOf(BtcReplay.Data d)
        {
            List<KeyValuePair<long, byte[]>> r;
            if (Recs.TryGetValue(d.FilePath, out r) && r != null) return r;
            r = BtcReplaySeek.ReadCachedStats(d.FilePath);
            if (r != null) Recs[d.FilePath] = r;
            return r;
        }

        private static End Decode(List<KeyValuePair<long, byte[]>> recs, long endMs)
        {
            End e = new End();
            for (int i = 0; i < recs.Count; i++)
            {
                if (recs[i].Key > endMs) break;
                BtcMatchStatsNet.StatsMsg m = new BtcMatchStatsNet.StatsMsg();
                try
                {
                    NetworkReader rd = new NetworkReader(recs[i].Value);
                    rd.ReadUInt16();
                    rd.ReadUInt16();
                    m.Deserialize(rd);
                }
                catch (Exception) { continue; }
                if (m.Kind == 2)
                {
                    if (m.N < 1 || m.Cat < 0 || m.Cat >= BtcMatchStats.ND) continue;
                    Dictionary<string, int>[] d;
                    if (!e.Det.TryGetValue(m.Ids[0], out d)) { d = NewDet(); e.Det[m.Ids[0]] = d; }
                    d[m.Cat].Clear();
                    for (int k = 0; k < m.DN; k++) if (!string.IsNullOrEmpty(m.DKeys[k])) d[m.Cat][m.DKeys[k]] = m.DVals[k];
                    continue;
                }
                for (int f = 0; f < m.N; f++)
                {
                    int[] v = new int[BtcMatchStats.NV];
                    Array.Copy(m.Rows[f], v, Mathf.Min(m.Rows[f].Length, v.Length));
                    if (m.Kind == 0) e.V[m.Ids[f]] = v;
                    else
                    {
                        List<BtcMsSample> s;
                        if (!e.S.TryGetValue(m.Ids[f], out s)) { s = new List<BtcMsSample>(); e.S[m.Ids[f]] = s; }
                        s.Add(new BtcMsSample { Idx = m.Idx, T = m.T, V = v });
                        if (m.Idx > e.MaxIdx) e.MaxIdx = m.Idx;
                    }
                }
            }
            return e;
        }

        private static int[] AddCarry(int fac, int[] v)
        {
            int[] c;
            if (!CarryV.TryGetValue(fac, out c)) return v;
            int[] r = (int[])v.Clone();
            for (int k = 0; k < r.Length && k < c.Length; k++)
            {
                if (Live(k)) continue;
                r[k] = k == BtcMatchStats.V_APMPEAK ? Mathf.Max(r[k], c[k]) : r[k] + c[k];
            }
            return r;
        }

        // ------------------------------------------------------------------ hooks (BtcMatchStatsNet.ApplyAtClient, replay playback only)
        private static bool On { get { return _on && Playing && BtcReplayChainPlay.Active && _key == BtcReplayChainPlay.L.Key + "|" + BtcReplayChainPlay.Step; } }

        /// <summary>The store was cleared for this session's stream: put the earlier sessions under it.</summary>
        internal static void AfterClear(BtcMsStore r)
        {
            if (!On) return;
            foreach (KeyValuePair<int, int[]> kv in LastV)
            {
                BtcMsFaction f = r.Get(kv.Key, true);
                Array.Copy(kv.Value, f.V, Mathf.Min(kv.Value.Length, f.V.Length));   // a faction gone in this session keeps its last values
            }
            foreach (KeyValuePair<int, List<BtcMsSample>> kv in Seeds)
            {
                BtcMsFaction f = r.Get(kv.Key, true);
                for (int k = 0; k < kv.Value.Count; k++) f.AddSample(kv.Value[k].Idx, kv.Value[k].T, kv.Value[k].V);
            }
            foreach (KeyValuePair<int, Dictionary<string, int>[]> kv in CarryDet)
            {
                BtcMsFaction f = r.Get(kv.Key, true);
                for (int cat = 0; cat < f.Det.Length && cat < kv.Value.Length; cat++)
                {
                    foreach (KeyValuePair<string, int> d in kv.Value[cat]) f.Det[cat][d.Key] = d.Value;
                    f.DetVer[cat]++;
                }
            }
        }

        internal static void FixLive(int fac, int[] v)
        {
            if (!On) return;
            int[] c;
            if (!CarryV.TryGetValue(fac, out c)) return;
            for (int k = 0; k < v.Length && k < c.Length; k++)
            {
                if (Live(k)) continue;
                v[k] = k == BtcMatchStats.V_APMPEAK ? Mathf.Max(v[k], c[k]) : v[k] + c[k];
            }
        }

        internal static int[] FixSample(int fac, int[] v) { return On ? AddCarry(fac, v) : v; }

        internal static float T(float t) { return On ? t + _off : t; }

        internal static int Idx(int i) { return On ? i + _idxBase : i; }

        internal static void FixDet(BtcMsFaction f, int cat)
        {
            if (!On) return;
            Dictionary<string, int>[] c;
            if (!CarryDet.TryGetValue(f.Id, out c) || cat >= c.Length) return;
            foreach (KeyValuePair<string, int> d in c[cat]) { int had; f.Det[cat].TryGetValue(d.Key, out had); f.Det[cat][d.Key] = had + d.Value; }
        }
    }
}
