#if DEV
using System;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Components.Volumes;
using Brix.Game.Factories;
using Brix.Engine;
using Brix.Utils;
using Brix.Transactions;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>DEV scenario helper: leaves a few blueprints of the local faction standing (replay blueprint recording test).</summary>
    internal static class BtcDevBp
    {
        internal static void Spawn(int n)
        {
            try
            {
                Faction f = User.LocalUser != null ? User.LocalUser.faction : null;
                Occupation[] occ = UnityEngine.Object.FindObjectsOfType<Occupation>();
                if (f == null || occ.Length == 0) { BtcLog.Warn("DevBp", "no faction/unit"); return; }
                Vector3 basep = occ[0].transform.position;
                int ok = 0;
                for (int i = 0; i < n; i++)
                {
                    Vector3 pos = new Vector3(Mathf.Round(basep.x) + 4 + i * 4, Mathf.Round(basep.y), Mathf.Round(basep.z) + 4);
                    GameObject go = Transactor.SpawnUNet(Blueprints.ChoppingBlock, f, pos, PreprocessedRotations.DiscreteRotationForIndexes[0], null, true);
                    if (go != null) ok++;
                }
                BtcLog.Info("DevBp", "spawned " + ok + "/" + n + " blueprints for " + f.name);
            }
            catch (Exception e) { BtcLog.Warn("DevBp", "spawn: " + e.Message); }
        }
    }
}
#endif
