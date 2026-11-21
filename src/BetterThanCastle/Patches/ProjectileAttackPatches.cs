using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(ProjectileAttack), "Apply", new Type[] { typeof(IDamageReceiver) })]
    internal static class ProjectileAttackApplyPatch
    {
        internal static float Dmg(float value, ProjectileAttack self, IDamageReceiver receiver)
        {
            try
            {
                float n = value;
                if (self.attack != AttackTemplate.ThrownUnit) n = BtcGameplay.ProjDmg(BtcGameplay.JobOf(self.origin), n);
                return n * BtcGameplay.TargetMul(receiver);
            }
            catch (Exception e) { BtcLog.Error("Hook", "ProjectileAttack.Apply: " + e); return value; }
        }

        // vanilla line 66: FloorToInt(this.DamageValue * ...)  ->  DamageValue scaled by job + target multiplier
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            CodeMatcher cm = new CodeMatcher(ins);
            cm.MatchForward(false, new CodeMatch(i => (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt)
                && i.operand is MethodInfo m && m.Name == "get_DamageValue"));
            if (cm.IsInvalid)
            {
                BtcLog.Error("Hook", "ProjectileAttack.Apply: get_DamageValue not found");
                return ins;
            }
            cm.Advance(1).Insert(new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ProjectileAttackApplyPatch), "Dmg")));
            return cm.InstructionEnumeration();
        }
    }
}
