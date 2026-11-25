using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game.AI;
using Brix.Utils.Locomotion;
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

    [HarmonyPatch(typeof(ProjectileAiming), "CanReachWithProjectile")]
    internal static class ProjectileAimingCanReachRealPatch
    {
        // vanilla approach check (not in stance yet) measures from the weapon, which moves when the unit raises it: at the stance the
        // target was up to ~1.7 blocks beyond MaxRange and the attack died (lab rangedstop). Stop only with a 1 block margin.
        private const float Margin = 1f;

        private static void Postfix(ProjectileAiming __instance, bool roundPosition, ref bool __result)
        {
            try
            {
                if (!__result || !roundPosition || !BtcNetworkGate.Enabled) return;
                ProjectileWeapon w = __instance.Weapon;
                if (w == null || __instance.Target == null) return;
                float flat = (w.transform.position - __instance.Target.Position).Flatten().magnitude;
                if (flat > BtcWorld.Range(w.MaxRange, __instance._parent.gameObject, true) - Margin) __result = false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "ProjectileAiming.CanReachReal: " + e.Message); }
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
