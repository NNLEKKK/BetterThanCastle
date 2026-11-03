using System;
using Brix.External.Factories;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Rules;
using HarmonyLib;
using System.Reflection.Emit;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    internal static class ProfessionHook
    {
        // Legacy private property Profession.BtcJob.
        internal static int Job(Profession p)
        {
            if (p is Builder && p.labor != null && p.labor.Occupation != null)
                return (int)p.labor.Occupation.CurrentJob;
            return -1;
        }
    }

    // Vanilla CharacterLevel setter sets Walk/RunSpeedMperS; legacy routes it through BtcGameplay.ApplySpeed instead.
    [HarmonyPatch(typeof(Profession), nameof(Profession.CharacterLevel), MethodType.Setter)]
    internal static class ProfessionSetCharacterLevelPatch
    {
        private static void Postfix(Profession __instance)
        {
            try
            {
                Locomotion4 proto = Factory.Peek(__instance.AssetKey).GetComponent<Locomotion4>();
                Locomotion4 cur = __instance.GetComponent<Locomotion4>();
                BtcGameplay.ApplySpeed(cur, proto, __instance.MovementSpeedLevelMultiplier, ProfessionHook.Job(__instance));
            }
            catch (Exception e) { BtcLog.Error("Hook", "Profession.CharacterLevel: " + e); }
        }
    }

    [HarmonyPatch(typeof(Profession), "CanPassThrough")]
    internal static class ProfessionCanPassThroughPatch
    {
        private static void Postfix(ref bool __result)
        {
            try { if (!__result && BtcQol.AlliesPass) __result = true; }
            catch (Exception e) { BtcLog.Error("Hook", "Profession.CanPassThrough: " + e); }
        }
    }

    [HarmonyPatch(typeof(Profession), "SetHpBonus")]
    internal static class ProfessionSetHpBonusPatch
    {
        private static bool Prefix(Profession __instance, int bonus)
        {
            try
            {
                BricktronDamageReceiver proto = Factory.Peek(__instance.GetComponent<AssetKeyHolder>().AssetKey).GetComponent<BricktronDamageReceiver>();
                int job = ProfessionHook.Job(__instance);
                if (!BtcGameplay.ClientKeepsSyncedHp())
                {
                    __instance.GetComponent<BricktronDamageReceiver>().MaxHP = BtcHandicap.Hp(__instance.faction,
                        BtcGameplay.HpFinal(job, ((float)bonus + proto.MaxHP) * (float)__instance.CharacterLevel));
                }
                BtcGameplay.ApplySpeed(__instance.GetComponent<Locomotion4>(), Factory.Peek(__instance.AssetKey).GetComponent<Locomotion4>(),
                    __instance.MovementSpeedLevelMultiplier, job);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Profession.SetHpBonus: " + e); }
            return true;
        }
    }

    [HarmonyPatch(typeof(Profession), "SetStaminaBonus")]
    internal static class ProfessionSetStaminaBonusPatch
    {
        private static bool Prefix(Profession __instance, int bonus)
        {
            try
            {
                BricktronDamageReceiver proto = Factory.Peek(__instance.GetComponent<AssetKeyHolder>().AssetKey).GetComponent<BricktronDamageReceiver>();
                __instance.GetComponent<BricktronDamageReceiver>().MaxStamina = BtcGameplay.StamFinal(((float)bonus + proto.MaxStamina) * (float)__instance.CharacterLevel);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Profession.SetStaminaBonus: " + e); }
            return true;
        }
    }

    // Legacy: floor of 12 for every job (vanilla used 18 for Arbalist) - BtcGameplay.MeleeDmg gets the job.
    [HarmonyPatch(typeof(Profession), nameof(Profession.MeleeDamage), MethodType.Getter)]
    internal static class ProfessionMeleeDamagePatch
    {
        private static bool Prefix(Profession __instance, ref int __result)
        {
            try
            {
                float v = (float)Mathf.Max(__instance.GetStatistic(Adjectif.meleeDamage), 12) * CombatRules.UnitToUnitDamageMultiplier * __instance.MeleeDamageLevelMultiplier;
                __result = Mathf.Max(1, Mathf.RoundToInt(BtcGameplay.MeleeDmg(ProfessionHook.Job(__instance), v) * BtcHandicap.Dmg(__instance.faction)));
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Profession.MeleeDamage: " + e); }
            return true;
        }
    }

    [HarmonyPatch(typeof(Profession), nameof(Profession.RangeDamage), MethodType.Getter)]
    internal static class ProfessionRangeDamagePatch
    {
        private static bool Prefix(Profession __instance, ref int __result)
        {
            try
            {
                __result = Mathf.RoundToInt((float)__instance.GetStatistic(Adjectif.rangeDamage) * CombatRules.UnitToUnitDamageMultiplier
                    * BtcGameplay.DmgMul(ProfessionHook.Job(__instance)) * BtcHandicap.Dmg(__instance.faction));
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Profession.RangeDamage: " + e); }
            return true;
        }
    }

}
