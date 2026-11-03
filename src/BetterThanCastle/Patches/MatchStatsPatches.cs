using System;
using System.Collections.Generic;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using Brix.Game.Semantique;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Match statistics event hooks (Features/BtcMatchStats.cs). Local bookkeeping only: nothing is sent, no vanilla behaviour changes.
    // All of these run where the simulation runs (host / single player); pure clients use the poll path of the collector.

    /// <summary>Unit death with the attacker (private _Kill is the single funnel of ApplyDamage-kills and Kill()).</summary>
    [HarmonyPatch(typeof(BricktronDamageReceiver), "_Kill")]
    internal static class MatchStatsKillPatch
    {
        private static void Postfix(BricktronDamageReceiver __instance, GameObject attacker)
        {
            if (!BtcMatchStats.Active) return;
            try { BtcMatchStats.OnKill(__instance, attacker); }
            catch (Exception e) { BtcLog.Error("Hook", "MatchStatsKill: " + e); }
        }
    }

    /// <summary>Damage involvement (who hit whom, for the trade rule).</summary>
    [HarmonyPatch(typeof(BricktronDamageReceiver), "ApplyDamage",
        new Type[] { typeof(float), typeof(DamageType), typeof(DamageReaction), typeof(Vector3), typeof(Vector3), typeof(float), typeof(GameObject) })]
    internal static class MatchStatsApplyDamagePatch
    {
        private static void Postfix(BricktronDamageReceiver __instance, GameObject attacker)
        {
            if (!BtcMatchStats.Active || attacker == null) return;
            try { BtcMatchStats.OnHit(__instance, attacker); }
            catch (Exception e) { BtcLog.Error("Hook", "MatchStatsApplyDamage: " + e); }
        }
    }

    /// <summary>Structure finished (blueprint morphs into the real object).</summary>
    [HarmonyPatch(typeof(Blueprint), "MorphToObject")]
    internal static class MatchStatsMorphPatch
    {
        private static void Postfix(Blueprint __instance, GameObject __result)
        {
            if (!BtcMatchStats.Active || __result == null) return;
            try { BtcMatchStats.OnBuilt(__instance, __result); }
            catch (Exception e) { BtcLog.Error("Hook", "MatchStatsMorph: " + e); }
        }
    }

    /// <summary>Recipe finished at a crafting station (CraftingStation.Finish is too small to patch safely: Mono may inline it).</summary>
    [HarmonyPatch(typeof(CraftingLabor), "FinishRecipe")]
    internal static class MatchStatsFinishRecipePatch
    {
        private static void Prefix(CraftingLabor __instance, Recipe recipe)
        {
            if (!BtcMatchStats.Active) return;
            try { BtcMatchStats.OnCraftBegin(__instance, recipe); }
            catch (Exception e) { BtcLog.Error("Hook", "MatchStatsFinishRecipe: " + e); }
        }

        private static void Postfix()
        {
            BtcMatchStats.OnCraftEnd();
        }
    }

    /// <summary>Objects produced by the recipe that is finishing (only recorded inside the FinishRecipe window above).</summary>
    [HarmonyPatch(typeof(Recipe), "SpawnAll")]
    internal static class MatchStatsSpawnAllPatch
    {
        private static void Postfix(ref List<GameObject> gos)
        {
            if (!BtcMatchStats.CraftTrack) return;
            try { BtcMatchStats.OnCraftSpawned(gos); }
            catch (Exception e) { BtcLog.Error("Hook", "MatchStatsSpawnAll: " + e); }
        }
    }

    /// <summary>Item enters a recepteur (bricktron hands, stockpile): authoritative "gathered" count (first entry per item life).</summary>
    [HarmonyPatch(typeof(Recepteur), "AddObject", new Type[] { typeof(GameObject), typeof(bool) })]
    internal static class MatchStatsRecepteurAddPatch
    {
        private static void Postfix(Recepteur __instance, GameObject item)
        {
            if (!BtcMatchStats.Active) return;
            try { BtcMatchStats.OnStored(__instance, item); }
            catch (Exception e) { BtcLog.Error("Hook", "MatchStatsRecepteurAdd: " + e); }
        }
    }

    /// <summary>Explosive object blows up (barrels are filtered by asset key in BtcMatchStats.OnExplode).</summary>
    [HarmonyPatch(typeof(ExplodeOnDeath), "Explode")]
    internal static class MatchStatsExplodePatch
    {
        private static void Prefix(ExplodeOnDeath __instance)
        {
            if (!BtcMatchStats.Active || __instance == null) return;
            try { BtcMatchStats.OnExplode(__instance.gameObject); }
            catch (Exception e) { BtcLog.Error("Hook", "MatchStatsExplode: " + e); }
        }
    }
}
