using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Network;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Phantoms (BtcPhantom). Every hook exits at once while no phantom lives on this peer (rule off, BetterConnection OFF, lobby),
    /// so the vanilla path is untouched then.</summary>
    [HarmonyPatch(typeof(Faction), "AnyUnitChangedFaction")]
    internal static class PhantomUnitCountPatch
    {
        private static bool Prefix(Faction __instance, GameObject unit, Faction oldFaction, Faction newFaction)
        {
            if (!BtcPhantom.Track && !BtcPhantom.Spawning) return true;
            try { return BtcPhantom.OnFactionChange(__instance, unit, oldFaction, newFaction); }
            catch (Exception e) { BtcLog.Error("Hook", "Phantom AnyUnitChangedFaction: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(Faction), "RemoveUnit")]
    internal static class PhantomRemoveUnitPatch
    {
        private static bool Prefix(Labor labor)
        {
            try { return BtcPhantom.OnLaborDeleted(labor); }
            catch (Exception e) { BtcLog.Error("Hook", "Phantom RemoveUnit: " + e.Message); return true; }
        }
    }

    /// <summary>No corpse, no death animation, no respawn firefly: the phantom just vanishes with what it carried.</summary>
    [HarmonyPatch(typeof(Locomotion4), "RpcDie")]
    internal static class PhantomDiePatch
    {
        private static bool Prefix(Locomotion4 __instance)
        {
            if (BtcPhantom.LiveCount == 0) return true;
            try { return !BtcPhantom.OnDie(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "Phantom RpcDie: " + e); return true; }
        }
    }

    /// <summary>Phantom attacks: fists do nothing to units, enemy objects and blocks in enemy territory take no damage.</summary>
    [HarmonyPatch]
    internal static class PhantomReceiveAttackPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] types = { typeof(BricktronDamageReceiver), typeof(ObjectDamageReceiver), typeof(PlacedBlockDamageReceiver), typeof(SimpleDamageReceiver),
                             typeof(MultiBlockDamageReceiver) };
            for (int i = 0; i < types.Length; i++)
            {
                MethodInfo m = AccessTools.DeclaredMethod(types[i], "ReceiveAttack");
                if (m != null) yield return m;
                else BtcLog.Error("Hook", "PhantomReceiveAttack: no ReceiveAttack on " + types[i].Name);
            }
        }

        private static bool Prefix(object __instance, Vector3 contact, GameObject attacker, ref DamageReaction __result)
        {
            if (BtcPhantom.LiveCount == 0 || attacker == null) return true;
            try
            {
                if (!BtcPhantom.VetoAttack(__instance, attacker, contact)) return true;
                __result = DamageReaction.Ignored;
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomReceiveAttack: " + e.Message); return true; }
        }
    }

    /// <summary>Direct block breaking (HitBrick, deconstruction hits): same rule for blocks and multi blocks.</summary>
    [HarmonyPatch]
    internal static class PhantomApplyDamagePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] types = { typeof(PlacedBlockDamageReceiver), typeof(MultiBlockDamageReceiver) };
            for (int i = 0; i < types.Length; i++)
            {
                MethodInfo m = AccessTools.DeclaredMethod(types[i], "ApplyDamage", new[] { typeof(float), typeof(GameObject) });
                if (m != null) yield return m;
                else BtcLog.Error("Hook", "PhantomApplyDamage: no ApplyDamage(float, GameObject) on " + types[i].Name);
            }
        }

        private static bool Prefix(object __instance, GameObject attacker)
        {
            if (BtcPhantom.LiveCount == 0 || attacker == null) return true;
            try { return !BtcPhantom.VetoAttack(__instance, attacker, Vector3.zero); }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomApplyDamage: " + e.Message); return true; }
        }
    }

    /// <summary>Phantoms are left out of every capture zone: they neither capture, stop a capture nor hold a crystal.</summary>
    [HarmonyPatch(typeof(CaptureClock), "SetUnitsInCaptureZone")]
    internal static class PhantomCapturePatch
    {
        private static void Prefix(HashSet<GameObject> unitSet)
        {
            if (BtcPhantom.LiveCount == 0 || unitSet == null || unitSet.Count == 0) return;
            try { BtcPhantom.FilterCapture(unitSet); }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomCapture: " + e.Message); }
        }
    }

    /// <summary>Call to arms never sends a phantom for a weapon.</summary>
    [HarmonyPatch(typeof(UNetProjectCmd), "CmdAddCallToArmsGoal")]
    internal static class PhantomCallToArmsPatch
    {
        private static bool Prefix(GameObject laborGO)
        {
            if (BtcPhantom.LiveCount == 0) return true;
            try { return !BtcPhantom.SkipCallToArms(laborGO); }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomCallToArms: " + e.Message); return true; }
        }
    }

    /// <summary>Kits never raise a phantom's HP or stamina (runs after the balance prefixes of ProfessionPatches).</summary>
    [HarmonyPatch(typeof(Profession), "SetHpBonus")]
    internal static class PhantomHpPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Profession __instance)
        {
            if (BtcPhantom.LiveCount == 0) return;
            try { BtcPhantom.Enforce(__instance.gameObject); }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomHp: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Profession), "SetStaminaBonus")]
    internal static class PhantomStaminaPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Profession __instance)
        {
            if (BtcPhantom.LiveCount == 0) return;
            try { BtcPhantom.Enforce(__instance.gameObject); }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomStamina: " + e.Message); }
        }
    }
}
