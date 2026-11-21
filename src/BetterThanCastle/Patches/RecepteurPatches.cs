using System;
using System.Reflection;
using Brix.Game.Semantique;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay fast feed only: an item a unit lets go of (placed ward, dropped kit) is queued in Recepteur.toScatter and positioned in the
    /// next FixedUpdate near the unit (ClosestAvailableDropPosition, terrain + occupied cells). Fast feeds run many records before that
    /// step, so the unit had moved on and pending digs were not applied: keyframes got wards metres away / floating. Place it right away.
    /// </summary>
    [HarmonyPatch(typeof(Recepteur), "ObjectUntracked")]
    internal static class RecepteurFastScatterPatch
    {
        private static readonly MethodInfo Scatter = typeof(Recepteur).GetMethod("ProcessParentlessObjets", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static void Postfix(Recepteur __instance, GameObject itemGO)
        {
            try
            {
                if (Scatter == null || !BtcReplay.Running || !BtcReplaySeek.FastFeed) return;
                BtcReplaySeek.FlushVoxelQueue();
                Scatter.Invoke(__instance, null);
            }
            catch (Exception e) { BtcLog.Error("Hook", "RecepteurFastScatter: " + (e.InnerException ?? e).Message); }
        }
    }
}
