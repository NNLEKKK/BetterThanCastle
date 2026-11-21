using System;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.AI.Nodes;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// Micro-duel hooks. Every body starts with BtcDuel.Active (false outside the duel mode = vanilla behaviour, nothing on the wire).
    /// 1) BricktronDamageReceiver.ApplyDamage: hit/kill events for the rules and the match log.
    /// 2) AttackInfo.ResetObject: with "skip animations" OFF a new order keeps the remaining strike cooldown of duel units (blocks the move-attack skip).
    /// </summary>
    [HarmonyPatch(typeof(BricktronDamageReceiver), "ApplyDamage",
        new Type[] { typeof(float), typeof(DamageType), typeof(DamageReaction), typeof(Vector3), typeof(Vector3), typeof(float), typeof(GameObject) })]
    internal static class DuelApplyDamagePatch
    {
        private static void Prefix(BricktronDamageReceiver __instance, out float __state)
        {
            __state = -1f;
            if (!BtcDuel.Active) return;
            BtcDuelUnitRef u;
            if (BtcDuel.TryGet(__instance.gameObject, out u)) __state = __instance.CurrentHP;
        }

        private static void Postfix(BricktronDamageReceiver __instance, float __state, GameObject attacker)
        {
            if (__state < 0f) return;
            try
            {
                BtcDuelUnitRef u;
                if (BtcDuel.Match != null && BtcDuel.TryGet(__instance.gameObject, out u))
                    BtcDuel.Match.OnDamage(u, attacker, __state - __instance.CurrentHP, __instance.CurrentHP);
            }
            catch (Exception e) { BtcLog.Error("Hook", "DuelApplyDamage: " + e); }
        }
    }

    // One patch for AttackInfo.Strike (private): Prefix = per-unit cooldown guard when skips are OFF (a fresh AttackInfo per order forgets the vanilla
    // cooldown, so the full cycle of 75 frames = 1.5 s is enforced here), Postfix = record the strike (only when the original really ran).
    [HarmonyPatch(typeof(AttackInfo), "Strike")]
    internal static class DuelAttackInfoStrikePatch
    {
        private static bool Prefix(AttackInfo __instance, out bool __state)
        {
            __state = false;
            if (!BtcDuel.Active) return true;
            try
            {
                Labor a = __instance.Attacker;
                BtcDuelUnitRef u;
                if (a == null || !BtcDuel.TryGet(a.gameObject, out u)) return true;
                if (!BtcDuel.SkipAnims && Time.time - u.StrikeAt < BtcDuel.VanillaCycleMs * 0.001f - 0.015f) return false;
                __state = true;
                return true;
            }
            catch (Exception e) { BtcLog.Error("Hook", "DuelStrikeGuard: " + e); return true; }
        }

        private static void Postfix(AttackInfo __instance, bool __state)
        {
            if (!__state || BtcDuel.Match == null) return;
            try
            {
                Labor a = __instance.Attacker;
                BtcDuelUnitRef u;
                if (a != null && BtcDuel.TryGet(a.gameObject, out u)) { u.StrikeFrame = __instance.CurrentFrame; u.StrikeAI = __instance; BtcDuel.Match.OnStrike(u, __instance.MeleeTarget); }
            }
            catch (Exception e) { BtcLog.Error("Hook", "DuelAttackInfoStrike: " + e); }
        }
    }

    /// <summary>Logs the human side's orders (ORD lines) so a lost match can be replayed from the log. Bot orders are logged by the bot itself.</summary>
    [HarmonyPatch(typeof(Labor), "SendOrder")]
    internal static class DuelSendOrderPatch
    {
        private static void Prefix(Labor __instance, Task task, Labor.DecisionSource source)
        {
            if (!BtcDuel.Active || BtcDuel.Match == null) return;
            try { BtcDuel.Match.OnOrder(__instance.gameObject, task, source); }
            catch (Exception e) { BtcLog.Error("Hook", "DuelSendOrder: " + e); }
        }
    }

    /// <summary>Simulation-tick hook: a skip is due exactly K simulation frames after the strike started (render-time timers jitter against the 20 ms sim step and miss the 1-2 tick window).</summary>
    [HarmonyPatch(typeof(AttackInfo), "NextFrame")]
    internal static class DuelAttackInfoNextFramePatch
    {
        private static void Postfix(AttackInfo __instance)
        {
            if (!BtcDuel.Active) return;
            try
            {
                Labor a = __instance.Attacker;
                BtcDuelUnitRef u;
                if (a == null || !BtcDuel.TryGet(a.gameObject, out u)) return;
#if DEV
                if (BetterThanCastle.Dev.BtcDevDuelTrace.On) BetterThanCastle.Dev.BtcDevDuelTrace.Note(a.gameObject.GetInstanceID(), __instance);
#endif
                if (u.SkipFrameWait <= 0 || (object)__instance != u.StrikeAI) return;
                if (__instance.CurrentFrame - u.StrikeFrame >= u.SkipFrameWait) { u.SkipFrameWait = 0; u.SkipAt = Time.time; }
            }
            catch (Exception e) { BtcLog.Error("Hook", "DuelNextFrame: " + e); }
        }
    }
}