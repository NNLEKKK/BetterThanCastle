using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Components.CatapultSpace;
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

    /// <summary>Arrows, bolts, magic shots (host collision test): an enemy phantom is not a hit. It dies and the shot flies on.</summary>
    [HarmonyPatch(typeof(KinematicProjectile), "AcceptCollision")]
    internal static class PhantomArrowThroughPatch
    {
        private static void Postfix(KinematicProjectile __instance, RaycastHit hit, ref bool __result)
        {
            if (!__result || BtcPhantom.LiveCount == 0) return;
            try { if (BtcPhantom.PassThrough(hit.collider, __instance.GetAttack().Attacker, null)) __result = false; }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomArrowThrough: " + e.Message); }
        }
    }

    /// <summary>Rigidbody shots (catapult boulders, thrown things): their velocity is tracked so a pass-through keeps it.</summary>
    [HarmonyPatch(typeof(PhysicalizedProjectile), "Thrust")]
    internal static class PhantomThrustPatch
    {
        private static void Postfix(PhysicalizedProjectile __instance)
        {
            if (BtcPhantom.LiveCount == 0) return;
            try { BtcPhantom.NoteThrown(__instance.GetComponent<Rigidbody>()); }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomThrust: " + e.Message); }
        }
    }

    /// <summary>A thrown thing touching an enemy phantom: the phantom dies, no hit, the thing flies on.</summary>
    [HarmonyPatch(typeof(PhysicalizedProjectile), "OnCollisionEnter")]
    internal static class PhantomThrownThroughPatch
    {
        private static bool Prefix(PhysicalizedProjectile __instance, Collision collision)
        {
            if (BtcPhantom.LiveCount == 0 || collision == null) return true;
            try
            {
                if (!BtcPhantom.PassThrough(collision.collider, __instance.GetAttack().Attacker, null)) return true;
                BtcPhantom.ThrownThrough(__instance, collision);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomThrownThrough: " + e.Message); return true; }
        }
    }

    /// <summary>A catapult boulder touching an enemy phantom does not explode there: the phantom dies, the boulder flies on.</summary>
    [HarmonyPatch(typeof(CatapultProjectile), "OnCollisionEnter")]
    internal static class PhantomCatapultThroughPatch
    {
        private static bool Prefix(CatapultProjectile __instance, Collision collision)
        {
            if (BtcPhantom.LiveCount == 0 || collision == null) return true;
            try
            {
                Faction f = __instance.sourceCatapult != null ? Faction.GetFaction(__instance.sourceCatapult.gameObject) : null;
                if (!BtcPhantom.PassThrough(collision.collider, null, f)) return true;
                BtcPhantom.ThrownThrough(__instance, collision);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomCatapultThrough: " + e.Message); return true; }
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

    /// <summary>Flying phantoms (phantom.fly, host): every move/task trip becomes a straight flight (BtcPhantomFly), no path search.</summary>
    [HarmonyPatch(typeof(Navigation), "NavigateToOrAway")]
    internal static class PhantomFlyNavigatePatch
    {
        private static bool Prefix(Navigation __instance, Location location, bool away, ref Motus.Behavior.Node __result)
        {
            if (!BtcPhantom.Fly || BtcPhantom.LiveCount == 0) return true;
            try
            {
                if (!BtcPhantom.IsPhantom(__instance.gameObject)) return true;
                __result = BtcPhantomFly.Make(__instance, location, away);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "PhantomFly NavigateToOrAway: " + e.Message); return true; }
        }
    }

    /// <summary>No animator root motion while a phantom flies (the flight sets the position itself).</summary>
    [HarmonyPatch(typeof(Locomotion4), "ApplyAnimatorMotion")]
    internal static class PhantomFlyRootMotionPatch
    {
        private static bool Prefix(Locomotion4 __instance)
        {
            if (!BtcPhantom.Fly || BtcPhantom.LiveCount == 0) return true;
            return !BtcPhantomFly.InFlight(__instance.gameObject);
        }
    }

    /// <summary>A flying phantom keeps its height: no snap to the floor, no pop out of the ground.</summary>
    [HarmonyPatch(typeof(Locomotion4), "CheckSnapToFloor")]
    internal static class PhantomFlySnapPatch
    {
        private static bool Prefix(Locomotion4 __instance)
        {
            if (!BtcPhantom.Fly || BtcPhantom.LiveCount == 0) return true;
            return !BtcPhantom.IsPhantom(__instance.gameObject);
        }
    }

    /// <summary>No pop out of obstacles / ground for a flying phantom (vanilla PopOuterOfObstacle teleported it back to the nearest free
    /// cell while it flew through a building or a block, user 2026-11-16).</summary>
    [HarmonyPatch(typeof(PopOuterOfObstacle), "CheckAndPop")]
    internal static class PhantomFlyPopOutPatch
    {
        private static bool Prefix(PopOuterOfObstacle __instance)
        {
            if (!BtcPhantom.Fly || BtcPhantom.LiveCount == 0) return true;
            return !BtcPhantom.IsPhantom(__instance.gameObject);
        }
    }

    /// <summary>No unstack step (out of another unit or out of a forest) for a flying phantom: it interrupted the flight.</summary>
    [HarmonyPatch]
    internal static class PhantomFlyUnstackPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            string[] names = { "Unstack", "UnstackFromNature" };
            for (int i = 0; i < names.Length; i++)
            {
                MethodInfo m = AccessTools.DeclaredMethod(typeof(Labor), names[i]);
                if (m != null) yield return m;
                else BtcLog.Error("Hook", "PhantomFlyUnstack: no Labor." + names[i]);
            }
        }

        private static bool Prefix(Labor __instance)
        {
            if (!BtcPhantom.Fly || BtcPhantom.LiveCount == 0) return true;
            return !BtcPhantom.IsPhantom(__instance.gameObject);
        }
    }

    /// <summary>Ghost phantoms (phantom.ghost, BtcPhantomGhost): the path search ignores obstacles that are not made of blocks.</summary>
    [HarmonyPatch(typeof(SearchPathRequest), "HasBlockerAt")]
    internal static class PhantomGhostSearchPatch
    {
        private static bool Prefix(GameObject gameObject, Brix.Engine.XYZ index, ref bool __result)
        {
            if (!BtcPhantom.Ghost || BtcPhantom.LiveCount == 0 || !BtcPhantomGhost.On(gameObject)) return true;
            __result = BtcPhantomGhost.SearchBlocked(gameObject, index);
            return false;
        }
    }

    /// <summary>Ghost phantoms: the repath search (path blocked on the way) ignores the same obstacles.</summary>
    [HarmonyPatch(typeof(DestinationRepathSearch), "CanCross")]
    internal static class PhantomGhostRepathPatch
    {
        private static readonly AccessTools.FieldRef<DestinationRepathSearch, GameObject> FGo = AccessTools.FieldRefAccess<DestinationRepathSearch, GameObject>("_gameObject");
        private static readonly AccessTools.FieldRef<DestinationRepathSearch, Func<Brix.Engine.XYZ, bool>> FTest =
            AccessTools.FieldRefAccess<DestinationRepathSearch, Func<Brix.Engine.XYZ, bool>>("_presenceTest");

        private static bool Prefix(DestinationRepathSearch __instance, Brix.Engine.XYZ p, ref bool __result)
        {
            if (!BtcPhantom.Ghost || BtcPhantom.LiveCount == 0) return true;
            GameObject go = FGo(__instance);
            if (!BtcPhantomGhost.On(go)) return true;
            Func<Brix.Engine.XYZ, bool> t = FTest(__instance);
            __result = !BtcPhantomGhost.PassBlocked(go, p) && (t == null || t(p));
            return false;
        }
    }

    /// <summary>Ghost phantoms: walking inside a building is not "occupied" (no pop out of the obstacle, no blocked step).</summary>
    [HarmonyPatch(typeof(FloorPicker), "IsOccupied")]
    internal static class PhantomGhostOccupiedPatch
    {
        private static bool Prefix(GameObject exceptThis, Brix.Engine.XYZ position, bool avoidCharacterCollisions, ref bool __result)
        {
            if (!BtcPhantom.Ghost || BtcPhantom.LiveCount == 0 || !BtcPhantomGhost.On(exceptThis)) return true;
            __result = BtcPhantomGhost.Occupied(exceptThis, position, avoidCharacterCollisions);
            return false;
        }
    }

    /// <summary>Ghost phantoms: enemy bricktrons do not block them (the vanilla aggressive presence test), own ones still do.</summary>
    [HarmonyPatch(typeof(VoxelPresence), "BuildPresenceTest")]
    internal static class PhantomGhostPresencePatch
    {
        private static void Prefix(VoxelPresence __instance, ref bool aggressive)
        {
            if (!BtcPhantom.Ghost || BtcPhantom.LiveCount == 0 || aggressive) return;
            if (BtcPhantomGhost.On(__instance.gameObject)) aggressive = true;
        }
    }

    /// <summary>Ghost phantoms walk in the forest (vanilla path branch type Forest).</summary>
    [HarmonyPatch(typeof(Profession), "CanWalkInForest")]
    internal static class PhantomGhostForestPatch
    {
        private static void Postfix(Profession __instance, ref bool __result)
        {
            if (__result || !BtcPhantom.Ghost || BtcPhantom.LiveCount == 0) return;
            if (BtcPhantomGhost.On(__instance.gameObject)) __result = true;
        }
    }

    /// <summary>A flying phantom never counts as ungrounded, so it hovers instead of falling.</summary>
    [HarmonyPatch(typeof(Brix.Game.Locomotion.Grounded), "Update")]
    internal static class PhantomFlyGroundedPatch
    {
        private static readonly AccessTools.FieldRef<Brix.Game.Locomotion.LocomotionStateUpdater, Locomotion4> FLoco =
            AccessTools.FieldRefAccess<Brix.Game.Locomotion.LocomotionStateUpdater, Locomotion4>("Locomotion");

        private static bool Prefix(Brix.Game.Locomotion.Grounded __instance)
        {
            if (!BtcPhantom.Fly || BtcPhantom.LiveCount == 0) return true;
            Locomotion4 l = FLoco(__instance);
            if (l == null || !BtcPhantom.IsPhantom(l.gameObject)) return true;
            __instance.GroundedLastTime = Time.time;
            return false;
        }
    }
}
