using System;
using Brix.Game.AI.Damage;
using Brix.Engine;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
    // Automatic replay marks (Replay/BtcAutoMarks.cs): local bookkeeping only, nothing is sent.

    [HarmonyPatch(typeof(BricktronDamageReceiver), "_Kill")]
    internal static class AutoMarkKillPatch
    {
        private static void Postfix(BricktronDamageReceiver __instance)
        {
            try { BtcAutoMarks.OnKill(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "AutoMarkKill: " + e); }
        }
    }

    [HarmonyPatch(typeof(BricktronDamageReceiver), "ApplyDamage",
        new Type[] { typeof(float), typeof(DamageType), typeof(DamageReaction), typeof(Vector3), typeof(Vector3), typeof(float), typeof(GameObject) })]
    internal static class AutoMarkHitPatch
    {
        private static void Postfix(BricktronDamageReceiver __instance, GameObject attacker)
        {
            try { BtcAutoMarks.OnHit(__instance, attacker); }
            catch (Exception e) { BtcLog.Error("Hook", "AutoMarkHit: " + e); }
        }
    }

    /// <summary>Every explosion (fuse, hit, "Break object", projectile) goes through Architecte.ExplosionSimple(GameObject source, ...): the funnel for barrel stats and the replay mark.</summary>
    [HarmonyPatch(typeof(Architecte), "ExplosionSimple", new Type[] { typeof(GameObject), typeof(Vector3), typeof(float), typeof(string) })]
    internal static class AutoMarkBarrelPatch
    {
        private static void Prefix(GameObject source)
        {
            if (source == null) return;
            try
            {
                BtcAutoMarks.OnBarrel(source);
                if (BtcMatchStats.Active) BtcMatchStats.OnExplode(source);
            }
            catch (Exception e) { BtcLog.Error("Hook", "AutoMarkBarrel: " + e); }
        }
    }
}
