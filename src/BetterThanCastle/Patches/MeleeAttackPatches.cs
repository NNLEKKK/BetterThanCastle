using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(MeleeAttack), "Apply", new Type[] { typeof(IDamageReceiver) })]
    internal static class MeleeAttackApplyPatch
    {
        internal static float Dmg(float damage, MeleeAttack self)
        {
            try { return damage * BtcGameplay.TargetMul(self.Target); }
            catch (Exception e) { BtcLog.Error("Hook", "MeleeAttack.Apply: " + e); return damage; }
        }

        // vanilla: Target.ReceiveAttack(Attack.damage, ...)  ->  Attack.damage * BtcGameplay.TargetMul(Target)
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            FieldInfo dmg = AccessTools.Field(typeof(AttackTemplate), "damage");
            CodeMatcher cm = new CodeMatcher(ins);
            cm.MatchForward(false, new CodeMatch(i => i.opcode == OpCodes.Ldfld && object.Equals(i.operand, dmg)));
            if (cm.IsInvalid)
            {
                BtcLog.Error("Hook", "MeleeAttack.Apply: Attack.damage load not found");
                return ins;
            }
            cm.Advance(1).Insert(new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MeleeAttackApplyPatch), "Dmg")));

            return cm.InstructionEnumeration();
        }
    }
}
