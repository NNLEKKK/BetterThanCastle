using System;
using Brix.Game.AI;
using Brix.Lifecycle.Pooling;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle.Patches
{
    /// <summary>rng.noWallShot (BtcWallShot): a line of fire through solid ground counts as obstructed (host AI).</summary>
    [HarmonyPatch(typeof(ProjectileAiming), "IsProjectileObstructed")]
    internal static class ProjectileAimingWallShotPatch
    {
        private static void Postfix(ProjectileAiming __instance, Vector3 projectileOrigin, ref bool __result)
        {
            if (__result) return;
            try
            {
                if (!(__instance.Target is ObjectLocation) || __instance.Weapon == null || !BtcWallShot.On) return;
                if (BtcWallShot.ArcBlocked(projectileOrigin, __instance.GetAnticipatedTargetPosition(), __instance.Power)) __result = true;
            }
            catch (Exception e) { BtcLog.Error("WallShot", "aim: " + e.Message); }
        }
    }

    /// <summary>rng.noWallShot: a projectile that slipped into the inside of the ground without a collision is removed.</summary>
    [HarmonyPatch(typeof(KinematicProjectile), "CheckCollisions")]
    internal static class KinematicProjectileWallShotPatch
    {
        private static void Postfix(KinematicProjectile __instance, float deltaTime, ref bool __result)
        {
            if (__result || !NetworkServer.active) return;
            try
            {
                if (!BtcWallShot.On) return;
                Vector3 a = __instance.transform.position;
                Vector3 b = a + __instance.GetVelocity() * deltaTime;
                if (!BtcWallShot.StepInsideGround(a, b)) return;
                BtcWallShot.LogRemoved(a);
                NetworkServer.UnSpawn(__instance.gameObject);
                ObjectPoolSingleton.Release(__instance.gameObject);
                __result = true;   // vanilla Update skips Move for this frame
            }
            catch (Exception e) { BtcLog.Error("WallShot", "flight: " + e.Message); }
        }
    }
}
