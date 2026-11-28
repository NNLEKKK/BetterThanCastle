#if DEV
using System.Text;

namespace BetterThanCastle
{
    /// <summary>DEV cmd "chain": dump | play [g] | seek g | state. Lab for matches split over saves (BtcReplayChain / BtcReplayChainPlay).</summary>
    internal static class BtcDevChain
    {
        private static void Lab(string s) { BtcLog.Info("Lab", "chain " + s); }

        internal static bool Cmd(string[] p)
        {
            string sub = p.Length > 1 ? p[1] : "dump";
            switch (sub)
            {
                case "dump":
                    {
                        BtcReplayUI.Refresh();
                        int n = 0;
                        foreach (BtcReplay.Data d in BtcReplayUI.list)
                        {
                            BtcReplayChain.Match m = BtcReplayChain.Of(d);
                            if (m == null || m.Root.D != d) continue;
                            n++;
                            StringBuilder sb = new StringBuilder();
                            sb.Append("match ").Append(m.Root.Id).Append(" sessions ").Append(m.Sessions).Append(" lines ").Append(m.Lines.Count).Append(" main ").Append(m.Main.Total.ToString("0.0")).Append(" s:");
                            for (int i = 0; i < m.Main.S.Count; i++) sb.Append(' ').Append(m.Main.S[i].N.D.FileName).Append('@').Append(m.Main.S[i].End.ToString("0.0"));
                            Lab(sb.ToString());
                        }
                        Lab("dump: " + n + " match(es), " + BtcReplayUI.list.Count + " replays");
                        return true;
                    }
                case "play":
                    {
                        BtcReplayUI.Refresh();
                        BtcReplayChain.Match best = null;
                        foreach (BtcReplay.Data d in BtcReplayUI.list)
                        {
                            BtcReplayChain.Match m = BtcReplayChain.Of(d);
                            if (m != null && (best == null || m.Root.D.Ticks > best.Root.D.Ticks)) best = m;
                        }
                        if (best == null) { Lab("play: no match"); return true; }
                        float g = 0f;
                        if (p.Length > 2) float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out g);
                        Lab("play " + best.Root.Id + " from " + g);
                        BtcReplayChainPlay.Start(best, null, g);
                        return true;
                    }
                case "seek":
                    {
                        float g = 0f;
                        if (p.Length > 2) float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out g);
                        Lab("seek " + g);
                        BtcReplayChainPlay.Seek(g);
                        return true;
                    }
                case "clip": Lab("clip " + BtcReplayDrop.PasteNow()); return true;
                case "stop": Lab("stop"); BtcReplayChainPlay.Stop(); return true;
                case "seamless": BtcReplayChainPlay.SeamlessOn = p.Length < 3 || p[2] != "0"; Lab("seamless " + BtcReplayChainPlay.SeamlessOn); return true;
                case "compare":
                    {
                        // the newest match: every joint of its main line, A at the joint vs B at its first keyframe
                        BtcReplayUI.Refresh();
                        BtcReplayChain.Match best = null;
                        foreach (BtcReplay.Data d in BtcReplayUI.list) { BtcReplayChain.Match m = BtcReplayChain.Of(d); if (m != null && (best == null || m.Root.D.Ticks > best.Root.D.Ticks)) best = m; }
                        if (best == null) { Lab("compare: no match"); return true; }
                        for (int i = 0; i + 1 < best.Main.S.Count; i++)
                            Lab("compare joint " + (i + 1) + ": " + BtcReplaySeek.CompareSessions(best.Main.S[i].N.D.FilePath, (long)(best.Main.S[i].End * 1000f), best.Main.S[i + 1].N.D.FilePath));
                        return true;
                    }
                case "state":
                    Lab("state active=" + BtcReplayChainPlay.Active + " step=" + BtcReplayChainPlay.Step + " clock=" + BtcReplayChainPlay.Clock.ToString("0.0") + " total=" + BtcReplayChainPlay.Total.ToString("0.0")
                        + " preparing=" + BtcReplayChainPlay.Preparing + " switching=" + BtcReplayChainPlay.Switching + " file=" + BtcReplay.FeedFile + " local=" + BtcReplay.PlayClock.ToString("0.0") + " mode=" + BtcReplay.Mode);
                    {
                        BtcMsStore r = BtcMatchStats.Remote;
                        StringBuilder sb = new StringBuilder("stats T=" + r.T.ToString("0.0") + " missing=" + BtcChainStats.Missing);
                        for (int i = 0; i < r.Factions.Count; i++)
                        {
                            BtcMsFaction f = r.Factions[i];
                            int res = 0;
                            for (int k = 0; k < BtcMatchStats.NRES; k++) res += f.V[BtcMatchStats.V_RES0 + k];
                            sb.Append(" | fac ").Append(f.Id).Append(" owned ").Append(f.V[BtcMatchStats.V_OWNED]).Append(" alive ").Append(f.V[BtcMatchStats.V_ALIVE]).Append(" loss ").Append(f.V[BtcMatchStats.V_LOSS])
                              .Append(" res ").Append(res).Append(" score ").Append(f.V[BtcMatchStats.V_SCORE]).Append(" actT ").Append(f.V[BtcMatchStats.V_ACTT])
                              .Append(" samples ").Append(f.Series.Count).Append(f.Series.Count > 0 ? " " + f.Series[0].T.ToString("0") + ".." + f.Series[f.Series.Count - 1].T.ToString("0") : "");
                        }
                        Lab(sb.ToString());
                    }
                    return true;
            }
            return false;
        }
    }
}
#endif
