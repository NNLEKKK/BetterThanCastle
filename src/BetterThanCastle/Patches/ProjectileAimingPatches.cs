using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game.AI;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    internal static class ProjectileAimingHelpers
    {
        internal static bool InRange(ProjectileWeapon w, Vector3 target, ProjectileAiming self)
        {
            try { return BtcWorld.InRange(w, self._parent.gameObject, target); }
            catch (Exception e) { BtcLog.Error("Hook", "ProjectileAiming.InRange: " + e); return w.IsInRangeToShoot(target); }
        }

        internal static bool FarEnough(ProjectileWeapon w, Vector3 target, ProjectileAiming self)
        {
            try { return BtcWorld.FarEnough(w, self._parent.gameObject, target); }
            catch (Exception e) { BtcLog.Error("Hook", "ProjectileAiming.FarEnough: " + e); return w.IsFarEnough(target); }
        }

        internal static IEnumerable<CodeInstruction> Redirect(IEnumerable<CodeInstruction> ins, string name, string helper, string where)
        {
            CodeMatcher cm = new CodeMatcher(ins);
            cm.MatchForward(false, new CodeMatch(i => (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt)
                && i.operand is MethodInfo m && m.Name == name));
            if (cm.IsInvalid)
            {
                BtcLog.Error("Hook", where + ": call " + name + " not found");
                return ins;
            }
            // stack: weapon, target  ->  weapon, target, this  -> helper(weapon, target, this)
            cm.Insert(new CodeInstruction(OpCodes.Ldarg_0));
            cm.Advance(1).Set(OpCodes.Call, AccessTools.Method(typeof(ProjectileAimingHelpers), helper));
            return cm.InstructionEnumeration();
        }
    }

    [HarmonyPatch(typeof(ProjectileAiming), "CanReachWithProjectile")]
    internal static class ProjectileAimingCanReachWithProjectilePatch
    {
        // vanilla: !_weapon.IsInRangeToShoot(Target.Position)  ->  BtcWorld.InRange(_weapon, _parent.gameObject, pos)
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            return ProjectileAimingHelpers.Redirect(ins, "IsInRangeToShoot", "InRange", "ProjectileAiming.CanReachWithProjectile");
        }
    }

    [HarmonyPatch(typeof(ProjectileAiming), "IsArcheryTargetFarEnough")]
    internal static class ProjectileAimingIsArcheryTargetFarEnoughPatch
    {
        // vanilla: _weapon.IsFarEnough(_target.Position)  ->  BtcWorld.FarEnough(_weapon, _parent.gameObject, pos)
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            return ProjectileAimingHelpers.Redirect(ins, "IsFarEnough", "FarEnough", "ProjectileAiming.IsArcheryTargetFarEnough");
        }
    }
}
