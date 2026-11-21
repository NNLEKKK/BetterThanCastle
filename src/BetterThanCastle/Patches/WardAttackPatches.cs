using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game;
using Brix.Game.AI.Damage;
using Brix.Game.Components.Wards;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(WardAttack), "AttackRange", MethodType.Getter)]
    internal static class WardAttackAttackRangePatch
    {
        private static void Postfix(ref float __result)
        {
            try { __result = BtcBalanceExtra.SentinelRange(__result); }
            catch (Exception e) { BtcLog.Error("Hook", "WardAttack.AttackRange: " + e); }
        }
    }

    [HarmonyPatch(typeof(WardAttack), "UnitsRefresh")]
    internal static class WardAttackUnitsRefreshPatch
    {
        private static bool Prefix(WardAttack __instance, HashSet<Trackable> units)
        {
            try { return BtcFocusMark.RefreshWard(__instance, units); }
            catch (Exception e) { BtcLog.Error("Hook", "WardAttack.UnitsRefresh: " + e); return true; }
        }
    }

    /// <summary>A Sentinel Ward standing inside its own (or an allied) base shield fires at full range through it: when vanilla refuses a target,
    /// the range, line of sight and enemy checks are redone without the faction-vision gate (BtcShield.InOwnCover is host-only and false while no barrier is up).</summary>
    [HarmonyPatch(typeof(WardAttack), "CanFireAt")]
    internal static class WardAttackCanFireAtPatch
    {
        private delegate bool LosFn(WardAttack w, int layerMask, GameObject target);
        private static LosFn _los;

        private static void Postfix(WardAttack __instance, GameObject unit, ref bool __result)
        {
            if (__result || !BtcShield.Any || unit == null) return;
            try
            {
                if (!BtcShield.InOwnCover(__instance.faction, __instance.transform.position)) return;
                DamageReceiver dr = unit.GetComponent<DamageReceiver>();
                if (dr == null || dr.IsInvincible || !Affiliation.IsEnemy(__instance.gameObject, unit)) return;
                if (Vector3.Distance(unit.transform.position, __instance.transform.position) > __instance.AttackRange) return;
                if (_los == null)
                    _los = (LosFn)Delegate.CreateDelegate(typeof(LosFn), AccessTools.Method(typeof(WardAttack), "CheckLinesOfSight"));
                __result = _los(__instance, __instance.ProjectileLayerMask, unit);
            }
            catch (Exception e) { BtcLog.Error("Hook", "WardAttack.CanFireAt: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(WardAttack), "FixedUpdate")]
    internal static class WardAttackFixedUpdatePatch
    {
        // vanilla: base.Fire(..., 1f, 0f, 60f) -> last argument BtcBalanceExtra.SentinelDamage(60f)
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            CodeMatcher cm = new CodeMatcher(ins);
            cm.MatchForward(false,
                new CodeMatch(i => i.opcode == OpCodes.Ldc_R4 && i.operand is float f && f == 60f),
                new CodeMatch(i => (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt) && i.operand is MethodInfo m && m.Name == "Fire"));
            if (cm.IsInvalid)
            {
                BtcLog.Error("Hook", "WardAttack.FixedUpdate: 60f + Fire pattern not found");
                return ins;
            }
            cm.Advance(1).Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BtcBalanceExtra), "SentinelDamage")));
            return cm.InstructionEnumeration();
        }
    }
}
