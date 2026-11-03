using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game.Components;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    /// <summary>Per-explosion unit/block/radius scales (legacy mod kept them as instance fields _uScale/_bScale/_rScale).</summary>
    internal static class ExplosionScales
    {
        private static readonly Dictionary<ExplosionGameplay, float[]> Map = new Dictionary<ExplosionGameplay, float[]>();
        private static ExplosionGameplay _last;
        private static float[] _lastVal;
        private static readonly float[] One = { 1f, 1f, 1f };

        internal static void Set(ExplosionGameplay e, float u, float b, float r)
        {
            if (Map.Count > 256) Prune();
            float[] v;
            if (!Map.TryGetValue(e, out v)) { v = new float[3]; Map[e] = v; }
            v[0] = u; v[1] = b; v[2] = r;
            _last = e; _lastVal = v;
        }

        private static void Prune()
        {
            List<ExplosionGameplay> dead = new List<ExplosionGameplay>();
            foreach (KeyValuePair<ExplosionGameplay, float[]> kv in Map) if (kv.Key == null) dead.Add(kv.Key);
            for (int i = 0; i < dead.Count; i++) Map.Remove(dead[i]);
            _last = null; _lastVal = null;
        }

        internal static float[] Get(ExplosionGameplay e)
        {
            if (object.ReferenceEquals(e, _last) && _lastVal != null) return _lastVal;
            float[] v;
            if (e != null && Map.TryGetValue(e, out v)) { _last = e; _lastVal = v; return v; }
            return One;
        }

        internal static float R(float value, ExplosionGameplay e) { return value * Get(e)[2]; }
        internal static float DivR(float value, ExplosionGameplay e) { return value / Get(e)[2]; }

        internal static IEnumerable<CodeInstruction> InsertAfter(IEnumerable<CodeInstruction> ins, Func<CodeInstruction, bool> match,
            string helper, string where)
        {
            CodeMatcher cm = new CodeMatcher(ins);
            cm.MatchForward(false, new CodeMatch(match));
            if (cm.IsInvalid)
            {
                BtcLog.Error("Hook", where + ": anchor not found");
                return ins;
            }
            cm.Advance(1).Insert(new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ExplosionScales), helper)));
            return cm.InstructionEnumeration();
        }

        internal static bool IsFalloffMaxRange(CodeInstruction i)
        {
            FieldInfo f = i.operand as FieldInfo;
            return i.opcode == OpCodes.Ldfld && f != null && f.Name == "maxRange";
        }
    }

    [HarmonyPatch(typeof(ExplosionGameplay), "Explosion")]
    internal static class ExplosionGameplayExplosionPatch
    {
        // mod: BtcGameplay.ExplScales(pos, unitDamageCurve.intensityFactor, unitDamageCurve.maxRange, out u, out b, out r) at start
        private static void Prefix(ExplosionGameplay __instance)
        {
            try
            {
                float u, b, r;
                BtcGameplay.ExplScales(__instance.transform.position, __instance.unitDamageCurve.intensityFactor,
                    __instance.unitDamageCurve.maxRange, out u, out b, out r);
                ExplosionScales.Set(__instance, u, b, r);
            }
            catch (Exception e) { BtcLog.Error("Hook", "ExplosionGameplay.Explosion: " + e); }
        }
    }

    [HarmonyPatch(typeof(ExplosionGameplay), "GetIntensity")]
    internal static class ExplosionGameplayGetIntensityPatch
    {
        // mod: curve.Evaluate(dist / _rScale); callers multiply by _uScale (unit damage) / _bScale (terrain, brick, wood damage)
        private static bool Prefix(ExplosionGameplay __instance, UnityEngine.Vector3 position, UnityEngine.Vector3 explosionCenter,
            ExplosionGameplay.FalloffCurve curve, ref float __result)
        {
            try
            {
                float[] s = ExplosionScales.Get(__instance);
                float v = curve.Evaluate((position - explosionCenter).magnitude / s[2]);
                if (object.ReferenceEquals(curve, __instance.unitDamageCurve)) v *= s[0];
                else if (object.ReferenceEquals(curve, __instance.terrainDamageCurve)
                    || object.ReferenceEquals(curve, __instance.brickDamageCurve)
                    || object.ReferenceEquals(curve, __instance.woodDamageCurve)) v *= s[1];
                __result = v;
                return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "ExplosionGameplay.GetIntensity: " + e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(ExplosionGameplay), "ApplyTerrainDammage")]
    internal static class ExplosionGameplayApplyTerrainDammagePatch
    {
        // mod: num = Max(terrain,brick,wood maxRange) * _rScale;  blockAnnihilationRange * _rScale
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            IEnumerable<CodeInstruction> a = ExplosionScales.InsertAfter(ins,
                i => (i.opcode == OpCodes.Call) && i.operand is MethodInfo m && m.Name == "Max" && m.DeclaringType == typeof(UnityEngine.Mathf),
                "R", "ExplosionGameplay.ApplyTerrainDammage(Max)");
            return ExplosionScales.InsertAfter(a,
                i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo f && f.Name == "blockAnnihilationRange",
                "R", "ExplosionGameplay.ApplyTerrainDammage(annihilation)");
        }
    }

    [HarmonyPatch(typeof(ExplosionGameplay), "ApplyUnitDammage")]
    internal static class ExplosionGameplayApplyUnitDammagePatch
    {
        // mod: radius = unitImpulseCurve.maxRange * _rScale
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            return ExplosionScales.InsertAfter(ins, ExplosionScales.IsFalloffMaxRange, "R", "ExplosionGameplay.ApplyUnitDammage");
        }
    }

    [HarmonyPatch(typeof(ExplosionGameplay), "ApplyObjectExplosionForce")]
    internal static class ExplosionGameplayApplyObjectExplosionForcePatch
    {
        // mod: radius = objectImpulseCurve.maxRange * _rScale
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            return ExplosionScales.InsertAfter(ins, ExplosionScales.IsFalloffMaxRange, "R", "ExplosionGameplay.ApplyObjectExplosionForce");
        }
    }

    [HarmonyPatch(typeof(ExplosionGameplay), "ComputeUnitThrust")]
    internal static class ExplosionGameplayComputeUnitThrustPatch
    {
        // mod: unitImpulseCurve.Evaluate(distance / _rScale)
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            CodeMatcher cm = new CodeMatcher(ins);
            cm.MatchForward(false, new CodeMatch(i => (i.opcode == OpCodes.Callvirt || i.opcode == OpCodes.Call)
                && i.operand is MethodInfo m && m.Name == "Evaluate"));
            if (cm.IsInvalid)
            {
                BtcLog.Error("Hook", "ExplosionGameplay.ComputeUnitThrust: Evaluate not found");
                return ins;
            }
            cm.Insert(new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ExplosionScales), "DivR")));
            return cm.InstructionEnumeration();
        }
    }
}
