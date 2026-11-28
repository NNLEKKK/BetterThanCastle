using System;
using System.Reflection;
using Brix.Game.AI;
using Brix.Game.Network;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // F1 (BtcShootRules).
    [HarmonyPatch(typeof(UNetContextualCmd), "CmdFireAt")]
    internal static class ShootRulesFireAtPatch
    {
        private static void Prefix(ref Vector3 location)
        {
            try { BtcShootRules.FixPoint(ref location); }
            catch (Exception e) { BtcLog.Error("ShootRules", "FixPoint: " + e.Message); }
        }
    }

    /// <summary>rng.hitKeep: the "damaged = true" lambda of Profession.ShootAt (closure class with the fields damaged + location).
    /// Its When(() => damaged) branch is evaluated every frame, so any hit switched the shot sequence to the empty "damaged" branch
    /// and the order failed with CombatTargetIsTooFarAway ("cannot aim").</summary>
    [HarmonyPatch]
    internal static class ShootRulesDamagedFlagPatch
    {
        private static FieldInfo _loc;

        private static bool Prepare() { return TargetMethod() != null; }

        private static MethodBase TargetMethod()
        {
            foreach (Type t in typeof(Profession).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            {
                FieldInfo dmg = t.GetField("damaged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                FieldInfo loc = t.GetField("location", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (dmg == null || dmg.FieldType != typeof(bool) || loc == null || loc.FieldType != typeof(Location)) continue;
                foreach (MethodInfo m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (m.ReturnType != typeof(void) || m.GetParameters().Length != 0) continue;
                    MethodBody body = m.GetMethodBody();
                    byte[] il = body != null ? body.GetILAsByteArray() : null;
                    // ldarg.0, ldc.i4.1, stfld <damaged>, ret
                    if (il == null || il.Length != 8 || il[0] != 0x02 || il[1] != 0x17 || il[2] != 0x7D || il[7] != 0x2A) continue;
                    try { if (m.Module.ResolveField(BitConverter.ToInt32(il, 3)) != dmg) continue; } catch { continue; }
                    _loc = loc;
                    return m;
                }
            }
            BtcLog.Error("Hook", "ShootRules: Profession.ShootAt damaged lambda not found");
            return null;
        }

        private static bool Prefix(object __instance)
        {
            try { return !BtcShootRules.KeepOnHit(_loc != null ? _loc.GetValue(__instance) as Location : null); }
            catch (Exception e) { BtcLog.Error("ShootRules", "KeepOnHit: " + e.Message); return true; }
        }
    }
}
