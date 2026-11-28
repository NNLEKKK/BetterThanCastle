using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using Brix.Game.Components.CatapultSpace;
using Brix.Engine;
using Brix.Engine.Abstract;
using Brix.Lifecycle.Pooling;
using Brix.Util;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>Base shields (BtcShield): host-side vetoes. Every prefix exits at once while no barrier is up (BtcShield.Any is false on clients,
    /// with the rule off and with BetterConnection OFF), so the vanilla path is untouched then.</summary>
    internal static class ShieldHookUtil
    {
        private static readonly Dictionary<Type, FieldInfo> PosField = new Dictionary<Type, FieldInfo>();

        /// <summary>World position of a damage receiver: its transform, or its voxel (pooled terrain/block receivers keep an XYZ _position).</summary>
        internal static Vector3 PosOf(object recv, Vector3 contact)
        {
            Component c = recv as Component;
            if (c != null) return c.transform.position;
            Type t = recv.GetType();
            FieldInfo fi;
            if (!PosField.TryGetValue(t, out fi)) { fi = AccessTools.Field(t, "_position"); if (fi != null && fi.FieldType != typeof(XYZ)) fi = null; PosField[t] = fi; }
            if (fi != null) return ((XYZ)fi.GetValue(recv)).ToVector3();
            return contact;
        }
    }

    /// <summary>ReceiveAttack of every concrete receiver (units, objects, placed blocks, terrain, multi blocks, nature): melee, arrows, digging,
    /// siege, explosions and catapults all end here with the attacker GameObject.</summary>
    [HarmonyPatch]
    internal static class ShieldReceiveAttackPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] types = { typeof(BricktronDamageReceiver), typeof(ObjectDamageReceiver), typeof(PlacedBlockDamageReceiver), typeof(SimpleDamageReceiver),
                             typeof(TerrainDamageReceiver), typeof(MultiBlockDamageReceiver), typeof(NatureDamageReceiver) };
            for (int i = 0; i < types.Length; i++)
            {
                MethodInfo m = AccessTools.DeclaredMethod(types[i], "ReceiveAttack");
                if (m != null) yield return m;
                else BtcLog.Error("Hook", "ShieldReceiveAttack: no ReceiveAttack on " + types[i].Name);
            }
        }

        private static bool Prefix(object __instance, Vector3 contact, GameObject attacker, ref DamageReaction __result)
        {
            if (!BtcShield.Any || attacker == null) return true;
            try
            {
                if (!BtcShield.Veto(attacker, ShieldHookUtil.PosOf(__instance, contact))) return true;
                __result = DamageReaction.Ignored;
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "ShieldReceiveAttack: " + e.Message); return true; }
        }
    }

    /// <summary>Direct ApplyDamage(value, attacker) on blocks/terrain/nature (HitBrick block breaking, harvesting): same veto.</summary>
    [HarmonyPatch]
    internal static class ShieldApplyDamagePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] types = { typeof(PlacedBlockDamageReceiver), typeof(TerrainDamageReceiver), typeof(MultiBlockDamageReceiver), typeof(NatureDamageReceiver) };
            for (int i = 0; i < types.Length; i++)
            {
                MethodInfo m = AccessTools.DeclaredMethod(types[i], "ApplyDamage", new[] { typeof(float), typeof(GameObject) });
                if (m != null) yield return m;
                else BtcLog.Error("Hook", "ShieldApplyDamage: no ApplyDamage(float, GameObject) on " + types[i].Name);
            }
        }

        private static bool Prefix(object __instance, GameObject attacker)
        {
            if (!BtcShield.Any || attacker == null) return true;
            try { return !BtcShield.Veto(attacker, ShieldHookUtil.PosOf(__instance, Vector3.zero)); }
            catch (Exception e) { BtcLog.Error("Hook", "ShieldApplyDamage: " + e.Message); return true; }
        }
    }

    /// <summary>No capture and no decapture by a faction the crystal's barrier blocks: its units are left out of the capture zone set.</summary>
    [HarmonyPatch(typeof(CaptureClock), "SetUnitsInCaptureZone")]
    internal static class ShieldCapturePatch
    {
        private static readonly HashSet<GameObject> Tmp = new HashSet<GameObject>();

        private static void Prefix(CaptureClock __instance, ref HashSet<GameObject> unitSet)
        {
            if (!BtcShield.Any || unitSet == null || unitSet.Count == 0) return;
            try
            {
                Tmp.Clear();
                foreach (GameObject go in unitSet) Tmp.Add(go);
                int before = Tmp.Count;
                BtcShield.FilterCapture(__instance, Tmp);
                if (Tmp.Count != before) unitSet = Tmp;
            }
            catch (Exception e) { BtcLog.Error("Hook", "ShieldCapture: " + e.Message); }
        }
    }

    /// <summary>Catapult shots are tracked on the host so a barrier can bounce them off its surface.</summary>
    [HarmonyPatch(typeof(CatapultProjectile), "Start")]
    internal static class ShieldCatapultShotPatch
    {
        private static void Postfix(CatapultProjectile __instance)
        {
            if (!NetworkServer.active || !BtcShield.RuleOn) return;
            try { BtcShield.AddCatapultShot(__instance, __instance.sourceCatapult); }
            catch (Exception e) { BtcLog.Error("Hook", "ShieldCatapultShot: " + e.Message); }
        }
    }

    /// <summary>Arrows and bolts that fly into a barrier which blocks their shooter are removed at the edge (like vanilla's out-of-bounds delete).</summary>
    [HarmonyPatch(typeof(KinematicProjectile), "Update")]
    internal static class ShieldArrowPatch
    {
        private static bool Prefix(KinematicProjectile __instance)
        {
            if (!BtcShield.Any) return true;
            bool gone = false;   // once unspawned, vanilla Update must not run on the dead arrow
            try
            {
                if (!BtcShield.StopArrow(__instance, __instance.Attacker)) return true;
                GameObject go = __instance.gameObject;
                gone = true;
                NetworkServer.UnSpawn(go);
                ObjectPoolSingleton.Release(go);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "ShieldArrow: " + e.Message); return !gone; }
        }
    }
}

namespace BetterThanCastle
{
    /// <summary>Barrel user tracking (BtcShield.NoteBarrelUser): the unit or catapult a barrel was just attached to is its user.</summary>
    [HarmonyPatch(typeof(Brix.Legacy.Explosifs), "OnTransformParentChanged")]
    internal static class ShieldBarrelParentPatch
    {
        private static void Postfix(Brix.Legacy.Explosifs __instance)
        {
            if (!NetworkServer.active || !BtcShield.RuleOn || __instance == null) return;
            try
            {
                Transform p = __instance.transform.parent;
                if (p == null) return;
                Labor l = p.GetComponentInParent<Labor>();
                if (l != null) { BtcShield.NoteBarrelUser(__instance.gameObject, l.faction); return; }
                Catapult c = p.GetComponentInParent<Catapult>();
                if (c != null) BtcShield.NoteBarrelUser(__instance.gameObject, BtcShield.FacOf(c.gameObject));
            }
            catch (Exception e) { BtcLog.Error("Hook", "ShieldBarrelParent: " + e.Message); }
        }
    }

    /// <summary>A player lit a barrel by hand: that player is its user.</summary>
    [HarmonyPatch(typeof(Brix.Game.Network.UNetContextualCmd), "CmdStartBarrelFuse")]
    internal static class ShieldBarrelFusePatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Brix.Game.Network.UNetContextualCmd __instance, GameObject instance)
        {
            if (!NetworkServer.active || !BtcShield.RuleOn || instance == null || __instance == null) return;
            try { BtcShield.NoteBarrelUser(instance, BtcShield.FacOf(__instance.gameObject)); }
            catch (Exception e) { BtcLog.Error("Hook", "ShieldBarrelFuse: " + e.Message); }
        }
    }

    /// <summary>Every barrel / bomb explosion: remember who used it, so the damage vetoes judge it by its user.</summary>
    [HarmonyPatch(typeof(Brix.Engine.Architecte), "ExplosionSimple", new[] { typeof(GameObject), typeof(Vector3), typeof(float), typeof(string) })]
    internal static class ShieldExplosionPatch
    {
        private static void Prefix(GameObject source, Vector3 pos)
        {
            if (!NetworkServer.active || !BtcShield.RuleOn) return;
            try { BtcShield.OnExplosion(source, pos); }
            catch (Exception e) { BtcLog.Error("Hook", "ShieldExplosion: " + e.Message); }
        }
    }
}

namespace BetterThanCastle.Patches
{
    /// <summary>Rule shield.avoid: a unit's route searches see the voxels of barriers that are up against it as blocked, so routes go around
    /// them (BtcShield.PathBlocked). Wrapped only while the shield rule is on; one cached wrapper per inner test (no garbage per search).</summary>
    [HarmonyPatch(typeof(VoxelPresence), "BuildPresenceTest")]
    internal static class ShieldPresenceTestPatch
    {
        private static readonly Dictionary<Func<XYZ, bool>, Func<XYZ, bool>> Wrapped = new Dictionary<Func<XYZ, bool>, Func<XYZ, bool>>();

        private static void Postfix(VoxelPresence __instance, ref Func<XYZ, bool> __result)
        {
            try
            {
                if (__result == null || !BtcShield.RuleOn || !NetworkServer.active) return;
                Func<XYZ, bool> w;
                if (!Wrapped.TryGetValue(__result, out w))
                {
                    if (Wrapped.Count > 8000) Wrapped.Clear();
                    Func<XYZ, bool> inner = __result;
                    GameObject go = __instance.gameObject;
                    w = p => inner(p) && !BtcShield.PathBlocked(go, p);
                    Wrapped[inner] = w;
                }
                __result = w;
            }
            catch (Exception e) { BtcLog.Error("Hook", "VoxelPresence.BuildPresenceTest shield: " + e.Message); }
        }
    }
}

namespace BetterThanCastle.Patches
{
    /// <summary>Rule shield.avoid: route search scopes (the barriers closed to the searching unit are collected once per call).</summary>
    [HarmonyPatch(typeof(SearchPathRequest), "Work")]
    internal static class ShieldSearchWorkScopePatch
    {
        private static void Prefix(SearchPathRequest __instance, out GameObject __state)
        {
            __state = null;
            try { __state = BtcShield.BeginPath(__instance.Presence != null ? __instance.Presence.gameObject : null); }
            catch (Exception e) { BtcLog.Error("Hook", "SearchPathRequest.Work shield: " + e.Message); }
        }
        private static Exception Finalizer(Exception __exception, GameObject __state) { BtcShield.EndPath(__state); return __exception; }
    }

    [HarmonyPatch(typeof(SearchPathRequest), "LoopImprovePath")]
    internal static class ShieldSearchImproveScopePatch
    {
        private static void Prefix(SearchPathRequest __instance, out GameObject __state)
        {
            __state = null;
            try { __state = BtcShield.BeginPath(__instance.Presence != null ? __instance.Presence.gameObject : null); }
            catch (Exception e) { BtcLog.Error("Hook", "SearchPathRequest.LoopImprovePath shield: " + e.Message); }
        }
        private static Exception Finalizer(Exception __exception, GameObject __state) { BtcShield.EndPath(__state); return __exception; }
    }

    [HarmonyPatch(typeof(DestinationRepathSearch), "Search")]
    internal static class ShieldRepathScopePatch
    {
        private static void Prefix(GameObject ____gameObject, out GameObject __state)
        {
            __state = null;
            try { __state = BtcShield.BeginPath(____gameObject); }
            catch (Exception e) { BtcLog.Error("Hook", "DestinationRepathSearch.Search shield: " + e.Message); }
        }
        private static Exception Finalizer(Exception __exception, GameObject __state) { BtcShield.EndPath(__state); return __exception; }
    }

    internal static class ShieldPathNode
    {
        /// <summary>The cell of a path node (2^lod voxels per axis) touches a barrier closed to the current searcher.</summary>
        internal static bool Blocked(Brix.Pathfinding.PathNode node)
        {
            if (node == null) return false;
            int lod = node.Lod, size = 1 << lod;
            XYZ lo = node.LowResPosition;
            return BtcShield.PathBoxBlocked(lo.x * size, lo.y * size, lo.z * size, size);
        }
    }

    /// <summary>A node touching the barrier is not crossed as a whole: the search goes down to its finer children and walks round.</summary>
    [HarmonyPatch(typeof(SearchPathRequest.BranchEvaluator), "CanCross")]
    internal static class ShieldBranchCanCrossPatch
    {
        private static void Postfix(Brix.Pathfinding.PathBranch branch, ref bool __result)
        {
            if (!__result) return;
            try { if (ShieldPathNode.Blocked(branch.Node)) __result = false; }
            catch (Exception e) { BtcLog.Error("Hook", "BranchEvaluator.CanCross shield: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(SearchPathRequest.OptimisticBranchEvaluator), "CanCross")]
    internal static class ShieldOptimisticCanCrossPatch
    {
        private static void Postfix(Brix.Pathfinding.PathBranch branch, ref bool __result)
        {
            if (!__result) return;
            try { if (ShieldPathNode.Blocked(branch.Node)) __result = false; }
            catch (Exception e) { BtcLog.Error("Hook", "OptimisticBranchEvaluator.CanCross shield: " + e.Message); }
        }
    }

    /// <summary>The search must not climb into a full parent node that holds part of the barrier (it would walk straight through it).</summary>
    [HarmonyPatch(typeof(Brix.Pathfinding.PathLodBuilder), "IsSuitableParent")]
    internal static class ShieldSuitableParentPatch
    {
        private static void Postfix(Brix.Pathfinding.PathNode parentNode, ref bool __result)
        {
            if (!__result) return;
            try { if (ShieldPathNode.Blocked(parentNode)) __result = false; }
            catch (Exception e) { BtcLog.Error("Hook", "PathLodBuilder.IsSuitableParent shield: " + e.Message); }
        }
    }
}
