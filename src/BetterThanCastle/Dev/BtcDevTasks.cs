#if DEV
using System;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Network;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>DEV scenario helper: leaves a landscape (flatten), a quarry and a harvest project of the local faction standing (replay task recording test).</summary>
    internal static class BtcDevTasks
    {
        private static Vector3 Base()
        {
            Occupation[] occ = UnityEngine.Object.FindObjectsOfType<Occupation>();
            return occ.Length > 0 ? occ[0].transform.position : Vector3.zero;
        }

        internal static void Spawn()
        {
            try
            {
                UNetGroupe g = User.LocalUser != null ? User.LocalUser.GetComponent<UNetGroupe>() : null;
                if (g == null) { BtcLog.Warn("DevTasks", "no UNetGroupe"); return; }
                Vector3 b = Base();
                g.CallCmdSpawnLandscapeProject(b + new Vector3(10, 0, 0), Quaternion.identity, false);
                g.CallCmdSpawnMineProject(b + new Vector3(-12, 0, 0), Quaternion.identity, false);
                g.CallCmdSpawnHarvestProject(b + new Vector3(0, 0, 14), Quaternion.identity, false, -1);
                BtcLog.Info("DevTasks", "spawn commands sent at " + b);
            }
            catch (Exception e) { BtcLog.Warn("DevTasks", "spawn: " + e.Message); }
        }

        internal static void SetBounds()
        {
            try
            {
                UNetGroupe g = User.LocalUser.GetComponent<UNetGroupe>();
                int n = 0;
                foreach (LandscapeProject p in UnityEngine.Object.FindObjectsOfType<LandscapeProject>())
                {
                    Vector3 c = p.transform.position;
                    g.CallCmdSetLandscape(p.gameObject, new Vector3(c.x - 5, c.y - 3, c.z - 5), new Vector3(c.x + 5, c.y + 3, c.z + 5), Mathf.RoundToInt(c.y));
                    n++;
                }
                foreach (MineProject p in UnityEngine.Object.FindObjectsOfType<MineProject>())
                {
                    Vector3 c = p.transform.position;
                    g.CallCmdSetMine(p.gameObject, new Vector3(c.x - 3, c.y - 6, c.z - 3), new Vector3(c.x + 3, c.y, c.z + 3), new Vector3(c.x - 3, c.y, c.z - 3), new Vector3(1, 0, 0), true);
                    n++;
                }
                BtcLog.Info("DevTasks", "bounds set on " + n + " project(s); landscape=" + UnityEngine.Object.FindObjectsOfType<LandscapeProject>().Length
                    + " mine=" + UnityEngine.Object.FindObjectsOfType<MineProject>().Length + " harvest=" + UnityEngine.Object.FindObjectsOfType<HarvestProject>().Length);
            }
            catch (Exception e) { BtcLog.Warn("DevTasks", "bounds: " + e.Message); }
        }

        internal static void Dump(string tag)
        {
            BtcLog.Info("DevTasks", tag + ": " + BtcReplayTasks.Status() + " projects=" + UnityEngine.Object.FindObjectsOfType<Project>().Length);
        }
    }
}
#endif
