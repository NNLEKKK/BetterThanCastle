using System;
using System.Collections.Generic;
using Brix.Game.AI;
using Brix.Game.AI.KnowledgeSpace;
using Brix.Game.Components;
using Brix.Game.Rules;
using Brix.Game.Semantique;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    // Rule mov.kitReserve (host), after Castle Story Plus (Tricky12321, GPLv3: Army/KitReservation.cs, bug #59).
    // Vanilla picks a weapon rack by what lies on it, not by who is already running there: at a call to arms all bricktrons
    // run to the nearest rack, the first ones take its kits and the rest find it empty and search again from there (same
    // for putting gear back: several run to the last free place). Now a bricktron reserves a kit (or a place) on the rack it
    // is sent to, and the search skips racks whose kits / places are all reserved: 3 kits on a rack = 3 bricktrons go there.
    // A reservation ends when the kit is taken / stored, the bricktron starts another task, is gone, or after 90 s.
    // Player orders on a rack are not limited (they do not go through the search).
    internal static class BtcKitReserve
    {
        private static int _idx = -2;
        private const float TimeoutSeconds = 90f;

        private sealed class Res
        {
            public Labor Labor;
            public object Task;
            public GameObject Rack;
            public bool Store;
            public float Until;
        }

        private static readonly Dictionary<Labor, Res> ByLabor = new Dictionary<Labor, Res>();
        private static readonly Dictionary<GameObject, List<Res>> ByRack = new Dictionary<GameObject, List<Res>>();

        public static void AddRows()
        {
            BtcGameplay.Add("mov.kitReserve", "mov", "Reserve kits on weapon racks", "Бронь китов на стойках", 1f, 0f, 1f, 1f, true,
                "A bricktron sent for a kit (call to arms, equip) books it before running, so a rack with 3 kits gets exactly 3 bricktrons and the rest go to another rack at once. Same for putting gear back: free places are booked. Host only.",
                "Бриктрон, которого послали за китом (призыв к оружию, экипировка), бронирует его до того, как побежать: к стойке с 3 китами побегут ровно 3 бриктрона, остальные сразу идут к другой стойке. То же при сдаче снаряжения: свободные места бронируются. Только у хоста.");
        }

        public static bool On
        {
            get
            {
                if (_idx == -2) _idx = BtcGameplay.Idx("mov.kitReserve");
                float[] e = BtcGameplay.E;
                return _idx >= 0 && e != null && _idx < e.Length && e[_idx] > 0.5f && NetworkServer.active;
            }
        }

        internal static void Reserve(Labor labor, GameObject rack, bool store)
        {
            if (labor == null || rack == null) return;
            Release(labor);
            Res r = new Res { Labor = labor, Task = labor.CurrentTask, Rack = rack, Store = store, Until = Time.time + TimeoutSeconds };
            ByLabor[labor] = r;
            List<Res> list;
            if (!ByRack.TryGetValue(rack, out list))
            {
                list = new List<Res>();
                ByRack[rack] = list;
            }
            list.Add(r);
            BtcLog.Info("KitRes", (store ? "place " : "kit ") + labor.GetInstanceID() + " -> rack " + rack.GetInstanceID() + " (" + list.Count + " booked)");
        }

        internal static void Release(Labor labor)
        {
            Res r;
            if (labor == null || !ByLabor.TryGetValue(labor, out r)) return;
            ByLabor.Remove(labor);
            List<Res> list;
            if (r.Rack != null && ByRack.TryGetValue(r.Rack, out list))
            {
                list.Remove(r);
                if (list.Count == 0) ByRack.Remove(r.Rack);
            }
        }

        private static bool Valid(Res r)
        {
            if (Time.time > r.Until || r.Labor == null || r.Rack == null || r.Labor.IsNullOrReleased() || r.Rack.IsNullOrReleased()) return false;
            return ReferenceEquals(r.Labor.CurrentTask, r.Task);
        }

        /// someone runs to this rack to take a kit (BtcKitAssemble leaves such racks alone)
        internal static bool AnyPick(GameObject rack) { return On && Booked(rack, null, false) > 0; }

        // Reservations of others on the rack (kits to take or places to fill); stale ones are dropped on the way.
        private static int Booked(GameObject rack, Labor except, bool store)
        {
            List<Res> list;
            if (rack == null || !ByRack.TryGetValue(rack, out list)) return 0;
            int n = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Res r = list[i];
                if (!Valid(r))
                {
                    Release(r.Labor);
                    if (!ByRack.ContainsKey(rack)) return n;
                    continue;
                }
                if (r.Labor != except && r.Store == store) n++;
            }
            return n;
        }

        // Kits a bricktron can take from the rack: whole kits of its class (vanilla racks hold one class),
        // or the most of any one piece when partial kits are allowed.
        internal static int Kits(ToolRack rack)
        {
            Recepteur rec = rack.recepteur;
            if (rec == null) return 0;
            int best = 0;
            List<List<Ressource>> classes = ToolRack.AllClasses;
            for (int c = 0; c < classes.Count; c++)
            {
                List<Ressource> kit = classes[c];
                int full = int.MaxValue, most = 0;
                for (int i = 0; i < kit.Count; i++)
                {
                    int v = rec.ContentDescription.Value(kit[i]);
                    if (v < full) full = v;
                    if (v > most) most = v;
                }
                int n = GeneralRules.AllowPartialToolsets ? most : full;
                if (n > best) best = n;
            }
            return best;
        }

        // Kits of the bricktron's class the rack still takes (the room left for each piece of its kit).
        internal static int Room(ToolRack rack, Labor labor)
        {
            List<Ressource> kit = KitOf(labor);
            Recepteur rec = rack.recepteur;
            if (kit == null || rec == null) return 1;
            int room = int.MaxValue;
            for (int i = 0; i < kit.Count; i++)
            {
                int v = rec.CurrentCapacity.Value(kit[i]);
                if (v < room) room = v;
            }
            return room;
        }

        private static List<Ressource> KitOf(Labor labor)
        {
            if (labor == null || labor.Occupation == null) return null;
            switch (labor.Occupation.CurrentJob)
            {
                case Occupation.Job.Knight: return ToolRack.Knight;
                case Occupation.Job.Halberdier: return ToolRack.Halbardier;
                case Occupation.Job.Archer: return ToolRack.Archer;
                case Occupation.Job.Arbalist: return ToolRack.Arbalist;
                case Occupation.Job.Alchemist: return ToolRack.Alchemist;
                case Occupation.Job.Artificer: return ToolRack.Artificer;
                default: return null;
            }
        }

        internal static bool KitsLeft(ToolRack rack, Labor user)
        {
            return Kits(rack) - Booked(rack.gameObject, user, false) > 0;
        }

        internal static bool PlacesLeft(ToolRack rack, Labor user)
        {
            return Room(rack, user) - Booked(rack.gameObject, user, true) > 0;
        }
    }
}

namespace BetterThanCastle.Patches
{
    // Search for a rack to take a kit from: skip racks whose kits are all booked by others.
    [HarmonyPatch(typeof(Knowledge), "_ClosestToolFrom_Test")]
    internal static class KitReservePickTestPatch
    {
        private static void Postfix(IGameComponent gc, Labor futureUser, ref bool __result)
        {
            if (!__result || !BtcKitReserve.On) return;
            try
            {
                if (gc == null || gc.GameObject == null) return;
                ToolRack rack = gc.GameObject.GetComponent<ToolRack>();
                if (rack != null && !BtcKitReserve.KitsLeft(rack, futureUser)) __result = false;
            }
            catch (Exception ex) { BtcLog.Error("KitRes", "pick test: " + ex.Message); }
        }
    }

    // Search for a rack to put the gear back on: skip racks whose free places are all booked.
    [HarmonyPatch(typeof(Knowledge), "_CanStoreToolOnRack_Test")]
    internal static class KitReserveStoreTestPatch
    {
        private static void Postfix(IGameComponent gc, Labor futureUser, ref bool __result)
        {
            if (!__result || !BtcKitReserve.On) return;
            try
            {
                if (gc == null || gc.GameObject == null || futureUser.IsNullOrReleased()) return;
                ToolRack rack = gc.GameObject.GetComponent<ToolRack>();
                if (rack != null && !BtcKitReserve.PlacesLeft(rack, futureUser)) __result = false;
            }
            catch (Exception ex) { BtcLog.Error("KitRes", "store test: " + ex.Message); }
        }
    }

    // The rack chosen to take a kit from: book one. Patched on Knowledge (Labor.BestToolToPickup is tiny, Mono may inline it).
    [HarmonyPatch(typeof(Knowledge), "ClosestToolFrom")]
    internal static class KitReservePickPatch
    {
        private static void Postfix(Labor futureUser, IGameComponent __result)
        {
            if (__result == null || !BtcKitReserve.On) return;
            try
            {
                GameObject go = __result.GameObject;
                if (go != null && go.GetComponent<ToolRack>() != null) BtcKitReserve.Reserve(futureUser, go, false);
            }
            catch (Exception ex) { BtcLog.Error("KitRes", "pick: " + ex.Message); }
        }
    }

    // The rack chosen to put the gear back on: book a place.
    [HarmonyPatch(typeof(Knowledge), "ClosestToolRackWithSpace")]
    internal static class KitReserveStorePatch
    {
        private static void Postfix(Labor futureUser, IGameComponent __result)
        {
            if (__result == null || !BtcKitReserve.On) return;
            try
            {
                GameObject go = __result.GameObject;
                if (go != null && go.GetComponent<ToolRack>() != null) BtcKitReserve.Reserve(futureUser, go, true);
            }
            catch (Exception ex) { BtcLog.Error("KitRes", "store: " + ex.Message); }
        }
    }

    // Kit taken (or none found) at the rack: the booking is done.
    [HarmonyPatch(typeof(Brix.Game.AI.Nodes.ToolNode), "EquipRackContent")]
    internal static class KitReserveEquipDonePatch
    {
        private static void Finalizer(Labor labor) { BtcKitReserve.Release(labor); }
    }

    [HarmonyPatch(typeof(Brix.Game.AI.Nodes.ToolNode), "StoreToolsIn")]
    internal static class KitReserveStoreDonePatch
    {
        private static void Finalizer(Labor labor) { BtcKitReserve.Release(labor); }
    }

    // A pooled bricktron that is reused starts without a booking.
    [HarmonyPatch(typeof(Labor), "OnRequested")]
    internal static class KitReservePoolPatch
    {
        private static void Prefix(Labor __instance) { BtcKitReserve.Release(__instance); }
    }
}
