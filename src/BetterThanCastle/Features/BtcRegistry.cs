using System;
using System.Collections.Generic;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Semantique;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Object registries instead of FindObjectsOfType scene walks (units, souls, stockpiles). The match stats used to walk the whole scene every second
    /// (souls), 4 s (units) and 5 s (stockpiles) which showed up as hitches. Objects register themselves once (Harmony postfix on their
    /// Initialize / first store); readers skip destroyed and inactive ones. Fallback: if a registry is still empty during a match, one scene walk every 10-30 s.
    /// </summary>
    internal static class BtcRegistry
    {
        internal static readonly List<Labor> Labors = new List<Labor>();
        internal static readonly List<Firefly> Fireflies = new List<Firefly>();
        internal static readonly List<Recepteur> Pallets = new List<Recepteur>();
        private static readonly HashSet<int> LaborIds = new HashSet<int>(), FireflyIds = new HashSet<int>(), PalletIds = new HashSet<int>();
        private static float _nextWalkL, _nextWalkF, _nextWalkP;

        internal static void Add(Labor l) { if (l != null && LaborIds.Add(l.GetInstanceID())) Labors.Add(l); }
        internal static void Add(Firefly f) { if (f != null && FireflyIds.Add(f.GetInstanceID())) Fireflies.Add(f); }

        internal static void AddPallet(Recepteur r)
        {
            if (r == null || PalletIds.Contains(r.GetInstanceID())) return;
            try
            {
                if (r.AssetKey != Brix.External.Factories.ObjetsDynamiques.Palette) { PalletIds.Add(r.GetInstanceID()); return; }
            }
            catch { return; }
            PalletIds.Add(r.GetInstanceID());
            Pallets.Add(r);
        }

        private static void Prune<T>(List<T> l) where T : UnityEngine.Object
        {
            for (int i = l.Count - 1; i >= 0; i--) if (l[i] == null) l.RemoveAt(i);
        }

        /// <summary>Live units (destroyed ones are dropped, inactive ones skipped).</summary>
        internal static void CollectLabors(List<Labor> into)
        {
            Prune(Labors);
            if (Labors.Count == 0 && Time.unscaledTime >= _nextWalkL)
            {
                _nextWalkL = Time.unscaledTime + 10f;
                Labor[] all = UnityEngine.Object.FindObjectsOfType<Labor>();
                for (int i = 0; i < all.Length; i++) Add(all[i]);
            }
            for (int i = 0; i < Labors.Count; i++) if (Labors[i].gameObject.activeInHierarchy) into.Add(Labors[i]);
        }

        internal static void CollectFireflies(List<Firefly> into)
        {
            Prune(Fireflies);
            if (Fireflies.Count == 0 && Time.unscaledTime >= _nextWalkF)
            {
                _nextWalkF = Time.unscaledTime + 10f;
                Firefly[] all = UnityEngine.Object.FindObjectsOfType<Firefly>();
                for (int i = 0; i < all.Length; i++) Add(all[i]);
            }
            for (int i = 0; i < Fireflies.Count; i++) if (Fireflies[i].gameObject.activeInHierarchy) into.Add(Fireflies[i]);
        }

        internal static void CollectPallets(List<Recepteur> into)
        {
            Prune(Pallets);
            if (Pallets.Count == 0 && Time.unscaledTime >= _nextWalkP)
            {
                _nextWalkP = Time.unscaledTime + 30f;
                Recepteur[] all = UnityEngine.Object.FindObjectsOfType<Recepteur>();
                for (int i = 0; i < all.Length; i++) AddPallet(all[i]);
            }
            for (int i = 0; i < Pallets.Count; i++) if (Pallets[i].gameObject.activeInHierarchy) into.Add(Pallets[i]);
        }
    }

    [HarmonyPatch(typeof(Labor), "Initialize")]
    internal static class RegistryLaborPatch
    {
        private static void Postfix(Labor __instance)
        {
            try { BtcRegistry.Add(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "RegistryLabor: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Firefly), "Initialize")]
    internal static class RegistryFireflyPatch
    {
        private static void Postfix(Firefly __instance)
        {
            try { BtcRegistry.Add(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "RegistryFirefly: " + e.Message); }
        }
    }
}
