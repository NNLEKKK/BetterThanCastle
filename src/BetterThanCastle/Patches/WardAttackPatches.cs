using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game.Components.Wards;
using HarmonyLib;

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
