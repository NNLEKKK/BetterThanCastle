using System;
using Brix.Lifecycle.Pooling;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    /// <summary>Reused pool instances get the signal connections their one-time Start/Initialize made (BtcPoolRevive).</summary>
    [HarmonyPatch(typeof(AbstractGameObjectPool), "OnGameObjectRequested")]
    internal static class PoolRequestedRevivePatch
    {
        private static void Postfix(GameObject __0)
        {
            try { BtcPoolRevive.OnRequested(__0); }
            catch (Exception e) { BtcLog.Error("PoolRevive", e.Message); }
        }
    }

    /// <summary>Pure client: still-listed children of a released holder are parked, not pooled with it (BtcTrackFix.BeforeRelease).</summary>
    [HarmonyPatch(typeof(ObjectPoolSingleton), "ResetGameObject")]
    internal static class PoolResetParkPatch
    {
        private static void Prefix(GameObject __0)
        {
            try { BtcTrackFix.BeforeRelease(__0); }
            catch (Exception e) { BtcLog.Error("TrackFix", "park: " + e.Message); }
        }
    }
}
