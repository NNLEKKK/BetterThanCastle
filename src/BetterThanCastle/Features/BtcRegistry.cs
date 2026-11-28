using System;
using System.Collections.Generic;
using Brix.Game;
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

        // true once per loaded scene (the scene handle changes on every load)
        private static bool FirstWalk(ref int scene)
        {
            int s = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetHashCode();
            if (s == scene) return false;
            scene = s;
            return true;
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

        // Capturables (crystals, shrines): registered while requested from the pool (IsCapturable.OnRequested / ResetComponent).
        private static readonly List<IsCapturable> Capturables = new List<IsCapturable>();
        private static readonly HashSet<int> CapturableIds = new HashSet<int>();
        private static int _walkSceneC = -1;   // fallback walk once per scene: an empty list is normal (sandbox, no units), a walk every 30 s was a 16-39 ms hitch

        internal static void AddCapturable(IsCapturable c) { if (c != null && CapturableIds.Add(c.GetInstanceID())) Capturables.Add(c); }

        internal static void RemoveCapturable(IsCapturable c)
        {
            if (c == null || !CapturableIds.Remove(c.GetInstanceID())) return;
            Capturables.Remove(c);
        }

        /// What FindObjectsOfType&lt;IsCapturable&gt;() gave (active ones), without walking the scene (that took ~33 ms in a base).
        internal static IsCapturable[] CapturablesArray()
        {
            Prune(Capturables);
            if (Capturables.Count == 0 && FirstWalk(ref _walkSceneC))
            {
                IsCapturable[] all = UnityEngine.Object.FindObjectsOfType<IsCapturable>();
                for (int i = 0; i < all.Length; i++) AddCapturable(all[i]);
                return all;
            }
            List<IsCapturable> live = new List<IsCapturable>(Capturables.Count);
            for (int i = 0; i < Capturables.Count; i++) if (Capturables[i].gameObject.activeInHierarchy) live.Add(Capturables[i]);
            return live.ToArray();
        }

        // Selection circles (HP rings): same pattern, registered while requested from the pool.
        private static readonly List<SelectionCircle> Circles = new List<SelectionCircle>();
        private static readonly HashSet<int> CircleIds = new HashSet<int>();
        private static int _walkSceneS = -1;

        internal static void AddCircle(SelectionCircle c) { if (c != null && CircleIds.Add(c.GetInstanceID())) Circles.Add(c); }

        internal static void RemoveCircle(SelectionCircle c)
        {
            if (c == null || !CircleIds.Remove(c.GetInstanceID())) return;
            Circles.Remove(c);
        }

        /// The circles in use, instead of Resources.FindObjectsOfTypeAll (every object and asset of the game: ~38 ms in a base).
        internal static SelectionCircle[] CirclesArray()
        {
            Prune(Circles);
            if (Circles.Count == 0 && FirstWalk(ref _walkSceneS))
            {
                SelectionCircle[] all = Resources.FindObjectsOfTypeAll<SelectionCircle>();
                for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].gameObject.scene.IsValid()) AddCircle(all[i]);
                return all;
            }
            return Circles.ToArray();
        }

        // Users (one per player connection): registered at User.Start, destroyed ones dropped when read.
        private static readonly List<User> Users = new List<User>();
        private static readonly HashSet<int> UserIds = new HashSet<int>();
        private static float _nextWalkU;

        internal static void AddUser(User u) { if (u != null && UserIds.Add(u.GetInstanceID())) Users.Add(u); }

        /// What FindObjectsOfType&lt;User&gt;() gave, without the scene walk (it ran every second for the match stats: ~40 ms).
        internal static User[] UsersArray()
        {
            for (int i = Users.Count - 1; i >= 0; i--) if (Users[i] == null) { Users.RemoveAt(i); }
            if (Users.Count == 0 && Time.unscaledTime >= _nextWalkU)
            {
                _nextWalkU = Time.unscaledTime + 10f;
                User[] all = UnityEngine.Object.FindObjectsOfType<User>();
                for (int i = 0; i < all.Length; i++) AddUser(all[i]);
                return all;
            }
            if (Users.Count != UserIds.Count) { UserIds.Clear(); for (int i = 0; i < Users.Count; i++) UserIds.Add(Users[i].GetInstanceID()); }
            List<User> live = new List<User>(Users.Count);
            for (int i = 0; i < Users.Count; i++) if (Users[i].gameObject.activeInHierarchy) live.Add(Users[i]);
            return live.ToArray();
        }

        // Tool racks, crafting stations, bear traps (Better Workbenches, kit assembly, traps task walked the scene every 1-2 s: 3-4 walks).
        // Registered at Start (no subclass overrides it); pooled objects keep their instance, so readers only skip inactive ones.
        private static readonly List<ToolRack> Racks = new List<ToolRack>();
        private static readonly List<CraftingStation> Stations = new List<CraftingStation>();
        private static readonly List<BearTrapTrigger> Traps = new List<BearTrapTrigger>();
        private static readonly HashSet<int> RackIds = new HashSet<int>(), StationIds = new HashSet<int>(), TrapIds = new HashSet<int>();
        private static int _walkSceneR = -1, _walkSceneT = -1, _walkSceneB = -1;

        internal static void Add(ToolRack r) { if (r != null && RackIds.Add(r.GetInstanceID())) Racks.Add(r); }
        internal static void Add(CraftingStation s) { if (s != null && StationIds.Add(s.GetInstanceID())) Stations.Add(s); }
        internal static void Add(BearTrapTrigger t) { if (t != null && TrapIds.Add(t.GetInstanceID())) Traps.Add(t); }

        private static T[] Live<T>(List<T> l, HashSet<int> ids, ref int scene) where T : Component
        {
            for (int i = l.Count - 1; i >= 0; i--) if (l[i] == null) { l.RemoveAt(i); }
            if (l.Count != ids.Count) { ids.Clear(); for (int i = 0; i < l.Count; i++) ids.Add(l[i].GetInstanceID()); }
            if (l.Count == 0 && FirstWalk(ref scene))
            {
                // objects started before the patches (should not happen): one walk per scene
                T[] all = UnityEngine.Object.FindObjectsOfType<T>();
                for (int i = 0; i < all.Length; i++) if (all[i] != null && ids.Add(all[i].GetInstanceID())) l.Add(all[i]);
            }
            int n = 0;
            for (int i = 0; i < l.Count; i++) if (l[i].gameObject.activeInHierarchy) n++;
            T[] r = new T[n];
            n = 0;
            for (int i = 0; i < l.Count; i++) if (l[i].gameObject.activeInHierarchy) r[n++] = l[i];
            return r;
        }

        /// What FindObjectsOfType gave (active objects), without the scene walk.
        internal static ToolRack[] RacksArray() { return Live(Racks, RackIds, ref _walkSceneR); }
        internal static CraftingStation[] StationsArray() { return Live(Stations, StationIds, ref _walkSceneT); }
        internal static BearTrapTrigger[] TrapsArray() { return Live(Traps, TrapIds, ref _walkSceneB); }

        internal static Labor[] LaborsArray()
        {
            LabTmp.Clear();
            CollectLabors(LabTmp);
            return LabTmp.ToArray();
        }
        private static readonly List<Labor> LabTmp = new List<Labor>();

        internal static void CollectPallets(List<Recepteur> into)
        {
            Prune(Pallets);
            if (Pallets.Count == 0 && Time.unscaledTime >= _nextWalkP)
            {
                _nextWalkP = Time.unscaledTime + 300f; // a pallet with content registers itself on its first store; empty ones count 0 (walk ~34 ms)
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

    [HarmonyPatch(typeof(IsCapturable), "OnRequested")]
    internal static class RegistryCapturableAddPatch
    {
        private static void Postfix(IsCapturable __instance)
        {
            try { BtcRegistry.AddCapturable(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "RegistryCapturable: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(IsCapturable), "ResetComponent")]
    internal static class RegistryCapturableRemovePatch
    {
        private static void Postfix(IsCapturable __instance)
        {
            try { BtcRegistry.RemoveCapturable(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "RegistryCapturable: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(SelectionCircle), "OnRequested")]
    internal static class RegistryCircleAddPatch
    {
        private static void Postfix(SelectionCircle __instance)
        {
            try { BtcRegistry.AddCircle(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "RegistryCircle: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(SelectionCircle), "ResetComponent")]
    internal static class RegistryCircleRemovePatch
    {
        private static void Postfix(SelectionCircle __instance)
        {
            try { BtcRegistry.RemoveCircle(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "RegistryCircle: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(User), "Start")]
    internal static class RegistryUserPatch
    {
        private static void Postfix(User __instance)
        {
            try { BtcRegistry.AddUser(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "RegistryUser: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(ToolRack), "Start")]
    internal static class RegistryRackPatch
    {
        private static void Postfix(ToolRack __instance)
        {
            try { BtcRegistry.Add(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "RegistryRack: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(CraftingStation), "Start")]
    internal static class RegistryStationPatch
    {
        private static void Postfix(CraftingStation __instance)
        {
            try { BtcRegistry.Add(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "RegistryStation: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(BearTrapTrigger), "Start")]
    internal static class RegistryTrapPatch
    {
        private static void Postfix(BearTrapTrigger __instance)
        {
            try { BtcRegistry.Add(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "RegistryTrap: " + e.Message); }
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
