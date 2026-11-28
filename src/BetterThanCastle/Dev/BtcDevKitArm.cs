#if DEV
using System;
using System.IO;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Semantique;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV arm-by-kit lab -> BTClogs/kitarm.txt. "kitfill Archer|Knight N": fill the first N own racks without kits;
    /// "kitarm c": press kit button c (BtcKitArm order); "kitdump label": button counts + jobs of own units.</summary>
    internal static class BtcDevKitArm
    {
        private static void L(string s)
        {
            try { File.AppendAllText(System.IO.Path.Combine(BtcDevPaths.LogDir, "kitarm.txt"), s + "\n"); } catch { }
            BtcLog.Info("KitLab", s);
        }

        internal static void Fill(string kit, int n)
        {
            User me = User.LocalUser;
            int done = 0;
            foreach (ToolRack r in BtcRegistry.RacksArray())
            {
                if (done >= n) break;
                if (r == null || me == null || !Affiliation.IsOwnedBy(r.gameObject, me) || !r.recepteur.IsEmpty()) continue;
                if (kit == "Knight") r.FillWithKnightKit(); else r.FillWithArcherKit();
                done++;
            }
            L("fill " + kit + " x" + done);
        }

        internal static void Dump(string label)
        {
            BtcKitArm.Recount();
            string counts = "";
            for (int c = 0; c < BtcKitArm.N; c++) counts += BtcKitArm.Count[c] + " ";
            User me = User.LocalUser;
            int[] jobs = new int[16];
            foreach (Labor l in BtcRegistry.LaborsArray())
            {
                if (l == null || me == null || !Affiliation.IsOwnedBy(l.gameObject, me) || l.Occupation == null) continue;
                int j = (int)l.Occupation.CurrentJob;
                if (j >= 0 && j < jobs.Length) jobs[j]++;
            }
            ToolRack[] racks = BtcRegistry.RacksArray();
            int k = 0;
            foreach (Labor l in BtcRegistry.LaborsArray())
            {
                if (l == null || k++ >= 6) continue;
                float best = float.MaxValue;
                for (int r = 0; r < racks.Length; r++) best = Mathf.Min(best, Vector3.Distance(racks[r].transform.position, l.transform.position));
                L("  unit " + l.name + " task=" + (l.CurrentTask != null ? l.CurrentTask.ToString() : "-") + " rackDist=" + best.ToString("0.0") + " pos=" + l.transform.position);
            }
            for (int r = 0; r < racks.Length; r++) L("  rack " + racks[r].GetInstanceID() + " pos=" + racks[r].transform.position + " active=" + racks[r].gameObject.activeInHierarchy);
            string js = "";
            for (int j = 0; j < jobs.Length; j++) if (jobs[j] > 0) js += (Occupation.Job)j + "=" + jobs[j] + " ";
            L(label + ": counts " + counts + "| jobs " + js + "| usable " + BtcKitArm.Usable);
        }
    }
}
#endif
