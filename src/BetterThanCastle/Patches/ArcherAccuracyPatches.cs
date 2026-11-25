using System;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Lifecycle.Pooling;
using Brix.Utils;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Rule "arr.accuracy" (BC ON only through BtcGameplay.E). Vanilla FireAt stays when the rule is off.
    [HarmonyPatch(typeof(KinematicProjectile), "FireAt")]
    internal static class ArcherAccuracyFireAtPatch
    {
        private static bool Prefix(Factory.AssetKey ammoAssetKey, GameObject attacker, Vector3 origin, Vector3 aimOrigin, Location target, Vector3 anticipatedTargetPosition, float power, float inaccuracy, int hitPointDamage, ref KinematicProjectile __result)
        {
            if (!BtcArcherAccuracy.On) return true;
            try
            {
                Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, attacker.transform.forward);
                ObjectPoolSingleton.RequestComponent<KinematicProjectile>(out KinematicProjectile t, ammoAssetKey, origin, rotation);
                Vector3 vector = ProjectilePhysics.ComputeFiringVelocity(power, anticipatedTargetPosition - origin);
                if (BtcArcherAccuracy.Targets.Count > 4000) BtcArcherAccuracy.Targets.Clear();
                if (BtcArcherAccuracy.IsBricktronArrow(t, attacker) && BtcArcherAccuracy.HasLineOfSight(t, attacker, aimOrigin, target))
                {
                    BtcArcherAccuracy.Targets[t] = target;
                    inaccuracy = 0f;
                }
                else
                {
                    BtcArcherAccuracy.Targets.Remove(t);
                }
                Vector3 inaccurateVelocity = Projectile.GetInaccurateVelocity(vector, inaccuracy);
                t.InitBeforeSpawn(origin, aimOrigin, inaccurateVelocity, attacker, target.Object, anticipatedTargetPosition, hitPointDamage);
                t.enabled = true;
                __result = t;
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "ArcherAccuracyFireAt: " + e); return true; }
        }
    }

    // Bricktron arrows never hit allies, and a homing arrow only hits its own target.
    [HarmonyPatch(typeof(KinematicProjectile), "AcceptCollision")]
    internal static class ArcherAccuracyAcceptCollisionPatch
    {
        private static bool Prefix(KinematicProjectile __instance, RaycastHit hit, ref bool __result)
        {
            if (!BtcArcherAccuracy.On) return true;
            try
            {
                GameObject hitObject = ProjectileAiming.GetSignificantParent(hit.collider).gameObject;
                if (BtcArcherAccuracy.IsHoming(__instance, out _))
                {
                    __result = hitObject == __instance.Target;
                    return false;
                }
                if (__instance.Archetype == KinematicProjectile.AttackArchetype.Arrow && __instance.Attacker != null && hitObject != __instance.Target && Affiliation.IsAllied(__instance.Attacker, hitObject))
                {
                    __result = false;
                    return false;
                }
            }
            catch (Exception e) { BtcLog.Error("Hook", "ArcherAccuracyAccept: " + e); }
            return true;
        }
    }

    // Restarts the ballistic arc every frame, aimed at where the target is now.
    [HarmonyPatch(typeof(KinematicProjectile), "Move")]
    internal static class ArcherAccuracySteerPatch
    {
        private static void Prefix(KinematicProjectile __instance)
        {
            if (!BtcArcherAccuracy.On || __instance == null || ((bool)__instance.magic && __instance.UseMagicProjectileComponent) || !BtcArcherAccuracy.IsHoming(__instance, out Location target)) return;
            try
            {
                Vector3 toTarget = target.RangeHitPosition - __instance.transform.position;
                float speed = __instance.FiredVelocity.magnitude;
                Vector3 velocity = ProjectilePhysics.ComputeFiringVelocity(speed, toTarget);
                if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || velocity.sqrMagnitude < 0.01f) velocity = toTarget.normalized * speed;
                __instance.FiredVelocity = velocity;
                __instance.FiredPosition = __instance.transform.position;
            }
            catch (Exception e) { BtcLog.Error("Hook", "ArcherAccuracySteer: " + e); }
        }
    }
}
