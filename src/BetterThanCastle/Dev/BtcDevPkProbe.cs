#if DEV
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine.Networking;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV only. Feasibility probe for replay playback keyframes (replay v4 phase 0): on the replay client, serialize every spawned
    /// NetworkBehaviour with OnSerialize(initialState) and read it back with OnDeserialize, then serialize again and compare bytes.
    /// agent.log "PkProbe": object/behaviour counts, bytes, serialize/deserialize ms, failing types. Scenario command: pkprobe [rt=1].
    /// </summary>
    internal static class BtcDevPkProbe
    {
        internal static void Run(bool roundTrip)
        {
            int objs = 0, behs = 0, serErr = 0, deErr = 0, diff = 0;
            long bytes = 0;
            Dictionary<string, int> bad = new Dictionary<string, int>();
            Dictionary<string, long> byType = new Dictionary<string, long>();
            List<KeyValuePair<NetworkBehaviour, byte[]>> snap = new List<KeyValuePair<NetworkBehaviour, byte[]>>(4096);
            Stopwatch sw = Stopwatch.StartNew();
            foreach (NetworkIdentity id in ClientScene.objects.Values)
            {
                if (id == null) continue;
                objs++;
                NetworkBehaviour[] nbs = id.GetComponents<NetworkBehaviour>();
                for (int i = 0; i < nbs.Length; i++)
                {
                    NetworkBehaviour nb = nbs[i];
                    behs++;
                    try
                    {
                        NetworkWriter w = new NetworkWriter();
                        nb.OnSerialize(w, true);
                        byte[] b = w.ToArray();
                        bytes += b.Length;
                        string tn = nb.GetType().Name;
                        long acc; byType.TryGetValue(tn, out acc); byType[tn] = acc + b.Length;
                        snap.Add(new KeyValuePair<NetworkBehaviour, byte[]>(nb, b));
                    }
                    catch (Exception e) { serErr++; Bad(bad, "ser " + nb.GetType().Name + ": " + e.GetType().Name); }
                }
            }
            double serMs = sw.Elapsed.TotalMilliseconds;
            double deMs = 0;
            if (roundTrip)
            {
                sw = Stopwatch.StartNew();
                for (int i = 0; i < snap.Count; i++)
                {
                    try { snap[i].Key.OnDeserialize(new NetworkReader(snap[i].Value), true); }
                    catch (Exception e) { deErr++; Bad(bad, "de " + snap[i].Key.GetType().Name + ": " + e.GetType().Name); }
                }
                deMs = sw.Elapsed.TotalMilliseconds;
                for (int i = 0; i < snap.Count; i++)
                {
                    try
                    {
                        NetworkWriter w = new NetworkWriter();
                        snap[i].Key.OnSerialize(w, true);
                        if (!Same(w.ToArray(), snap[i].Value)) { diff++; Bad(bad, "diff " + snap[i].Key.GetType().Name); }
                    }
                    catch (Exception e) { Bad(bad, "reser " + snap[i].Key.GetType().Name + ": " + e.GetType().Name); }
                }
            }
            BtcLog.Info("PkProbe", "objs=" + objs + " behs=" + behs + " bytes=" + bytes + " serMs=" + serMs.ToString("0.0") + " deMs=" + deMs.ToString("0.0")
                + " serErr=" + serErr + " deErr=" + deErr + " diff=" + diff + " t=" + BtcReplay.PlayClock.ToString("0.0"));
            BtcLog.Info("PkProbe", "bytes by type: " + Top(byType, 12));
            if (bad.Count > 0) BtcLog.Info("PkProbe", "problems: " + TopI(bad, 20));
        }

        private static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static void Bad(Dictionary<string, int> d, string k)
        {
            int n; d.TryGetValue(k, out n); d[k] = n + 1;
        }

        private static string Top(Dictionary<string, long> d, int n)
        {
            List<KeyValuePair<string, long>> l = new List<KeyValuePair<string, long>>(d);
            l.Sort((x, y) => y.Value.CompareTo(x.Value));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < l.Count && i < n; i++) sb.Append(l[i].Key).Append('=').Append(l[i].Value).Append(' ');
            return sb.ToString();
        }

        private static string TopI(Dictionary<string, int> d, int n)
        {
            List<KeyValuePair<string, int>> l = new List<KeyValuePair<string, int>>(d);
            l.Sort((x, y) => y.Value.CompareTo(x.Value));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < l.Count && i < n; i++) sb.Append(l[i].Key).Append(" x").Append(l[i].Value).Append("; ");
            return sb.ToString();
        }
    }
}
#endif
