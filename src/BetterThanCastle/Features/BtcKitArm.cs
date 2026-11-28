using System;
using System.Collections.Generic;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.KnowledgeSpace;
using Brix.Game.Components;
using Brix.Game.Rules;
using Brix.Game.Semantique;
using Brix.Input;
using Brix.Network;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// Arm by kit: buttons above Back to work / Call to arms in the C# game menu right bar, one per kit class that lies on the
    /// player's own weapon racks (icon = vanilla kit icon, number = whole kits on racks). A press sends the nearest workers
    /// (as many as there are kits of that class) to take exactly that kit; everyone else keeps working. Two presses (archers, then
    /// arbalists) never pick the same worker or the same kits: sent workers and their racks are booked until they equip
    /// (or get another task, or 30 s pass).
    /// Counting is event driven: a rack content change (ToolRack.RefreshExclusivity) wakes a one-shot LateUpdate, no polling.
    /// Wire: host / single player act directly (vanilla orders, nothing extra on the wire). A client sends 145 "class index"
    /// to the host (BC ON only; with BC OFF the client buttons are hidden). Binds Orders "arm_kit_*", unassigned.
    /// UI editor element gh.kits hides the column.
    /// </summary>
    internal static class BtcKitArm
    {
        internal const short Msg = 145;
        internal const int N = 6;
        private const float BookSeconds = 30f;

        internal static readonly string[] Icons = { "_Knight_Kit", "_Halberdier_Kit", "_Archer_Kit", "_Arbalist_Kit", "_Artificer_Kit", "_Alchemist_Kit" };
        internal static readonly string[] LocKeys = { "##recipe_knightkit", "##recipe_halberdierkit", "##recipe_archerkit", "##recipe_arbalistkit", "##recipe_artificerkit", "##recipe_alchemistkit" };
        internal static readonly string[] Binds = { "arm_kit_knight", "arm_kit_halberdier", "arm_kit_archer", "arm_kit_arbalist", "arm_kit_artificer", "arm_kit_alchemist" };
        private static List<Ressource>[] _kits;

        private static List<Ressource> Kit(int c)
        {
            if (_kits == null) _kits = new[] { ToolRack.Knight, ToolRack.Halbardier, ToolRack.Archer, ToolRack.Arbalist, ToolRack.Artificer, ToolRack.Alchemist };
            return _kits[c];
        }

        internal static void AddKeys(string group)
        {
            string[] en = { "Knights", "Halberdiers", "Archers", "Arbalists", "Artificers", "Alchemists" };
            for (int c = 0; c < N; c++)
                BtcHotkeys.AddKey(group, Binds[c], "Arm by kit: the nearest workers take the " + en[c] + " kits from your racks (as many as there are kits; the kit buttons above Call to arms)", 0, string.Empty, false);
        }

        /// <summary>The buttons work here: own player in a match, and on a client only with BetterConnection (the host does the work).</summary>
        internal static bool Usable
        {
            get
            {
                if (BtcReplay.Active || User.LocalUser == null) return false;
                return NetworkServer.active || BtcNetworkGate.Enabled;
            }
        }

        // ------------------------------------------------------------------ kit counts (local player, for the buttons)
        internal static readonly int[] Count = new int[N];

        /// Whole kits of a class on a rack (or the most of one piece when partial kits are allowed), as BtcKitReserve counts them.
        private static int KitsOn(ToolRack rack, List<Ressource> kit)
        {
            Recepteur rec = rack.recepteur;
            if (rec == null) return 0;
            int full = int.MaxValue, most = 0;
            for (int i = 0; i < kit.Count; i++)
            {
                int v = rec.ContentDescription.Value(kit[i]);
                if (v < full) full = v;
                if (v > most) most = v;
            }
            return GeneralRules.AllowPartialToolsets ? most : full;
        }

        internal static void Recount()
        {
            Array.Clear(Count, 0, N);
            User me = User.LocalUser;
            if (me != null)
            {
                ToolRack[] racks = BtcRegistry.RacksArray();
                for (int r = 0; r < racks.Length; r++)
                {
                    ToolRack rack = racks[r];
                    if (rack == null || !Affiliation.IsOwnedBy(rack.gameObject, me)) continue;
                    for (int c = 0; c < N; c++) Count[c] += KitsOn(rack, Kit(c));
                }
            }
            if (Changed != null) Changed();
        }

        /// The HUD column redraws itself (set by BtcGhKitColumn while it exists).
        internal static Action Changed;
        internal static BtcKitArmTick Waker;

        /// ToolRack.RefreshExclusivity: rack content changed; recount once at the end of this frame (only while the column exists).
        internal static void OnRackChanged()
        {
            if (Waker != null && !Waker.enabled) Waker.enabled = true;
        }

        // ------------------------------------------------------------------ press
        internal static void Press(int c)
        {
            if (c < 0 || c >= N || !Usable) return;
            if (InputModeController.IsPaused()) return;
            try
            {
                Brix.Audio.SoundEngine.Play("Call_To_Arm");
                if (NetworkServer.active) { Arm(User.LocalUser, c); return; }
                UNetManager m = NetworkManager.singleton as UNetManager;
                if (m == null || m.LocalNetworkClient == null || !m.LocalNetworkClient.isConnected) return;
                m.LocalNetworkClient.Send(Msg, new IntegerMessage(c));
                BtcLog.Info("KitArm", "sent class " + c);
            }
            catch (Exception e) { BtcLog.Error("KitArm", "press: " + e); }
        }

        // ------------------------------------------------------------------ host
        private sealed class Book
        {
            public ToolRack Rack;
            public float At;
            public object Task;
            public bool TaskSet;
        }

        private static readonly Dictionary<Labor, Book> Booked = new Dictionary<Labor, Book>();
        private static readonly List<Labor> Tmp = new List<Labor>();
        private static readonly List<Labor> Free = new List<Labor>();

        private static bool Valid(Labor l, Book b, float now)
        {
            if (l == null || l.IsNullOrReleased() || b.Rack == null || now - b.At > BookSeconds) return false;
            if (l.Profession == null || l.Profession.OccupationType != Occupation.Type.Builder) return false; // equipped: done
            if (!b.TaskSet) { if (now - b.At > 0.5f) { b.Task = l.CurrentTask; b.TaskSet = true; } return true; }
            return ReferenceEquals(l.CurrentTask, b.Task);
        }

        private static void Prune(float now)
        {
            Tmp.Clear();
            foreach (KeyValuePair<Labor, Book> kv in Booked) if (!Valid(kv.Key, kv.Value, now)) Tmp.Add(kv.Key);
            for (int i = 0; i < Tmp.Count; i++) Booked.Remove(Tmp[i]);
            Tmp.Clear();
        }

        /// <summary>Host: send the nearest free workers of the user to the kits of class c on the user's racks.</summary>
        internal static void Arm(User user, int c)
        {
            if (user == null || c < 0 || c >= N) return;
            float now = Time.time;
            Prune(now);
            List<Ressource> kit = Kit(c);

            ToolRack[] all = BtcRegistry.RacksArray();
            List<ToolRack> racks = new List<ToolRack>();
            List<int> left = new List<int>();
            int total = 0;
            for (int r = 0; r < all.Length; r++)
            {
                ToolRack rack = all[r];
                if (rack == null || !Affiliation.IsOwnedBy(rack.gameObject, user)) continue;
                int n = KitsOn(rack, kit);
                if (n <= 0) continue;
                foreach (KeyValuePair<Labor, Book> kv in Booked) if (kv.Value.Rack == rack) n--;
                if (n <= 0) continue;
                racks.Add(rack);
                left.Add(n);
                total += n;
            }
            if (total == 0) { BtcLog.Info("KitArm", "class " + c + ": no free kits"); return; }

            Free.Clear();
            Tmp.Clear();
            BtcRegistry.CollectLabors(Tmp);
            for (int i = 0; i < Tmp.Count; i++)
            {
                Labor l = Tmp[i];
                if (l == null || l.IsNullOrReleased() || Booked.ContainsKey(l)) continue;
                if (l.Profession == null || l.Profession.OccupationType != Occupation.Type.Builder) continue;
                if (!Affiliation.IsOwnedBy(l.gameObject, user)) continue;
                Free.Add(l);
            }
            Tmp.Clear();

            // greedy: the closest (worker, rack with kits left) pair first
            int sent = 0;
            while (total > 0 && Free.Count > 0)
            {
                int bw = -1, br = -1;
                float bd = float.MaxValue;
                for (int r = 0; r < racks.Count; r++)
                {
                    if (left[r] <= 0) continue;
                    Vector3 rp = racks[r].transform.position;
                    for (int w = 0; w < Free.Count; w++)
                    {
                        float d = (Free[w].transform.position - rp).sqrMagnitude;
                        if (d < bd) { bd = d; bw = w; br = r; }
                    }
                }
                if (bw < 0) break;
                Labor lab = Free[bw];
                ToolRack rk = racks[br];
                Free.RemoveAt(bw);
                left[br]--;
                total--;
                try
                {
                    Order.AboutTools.GoEquipRackContent.Send(lab, rk.gameObject, Labor.DecisionSource.User);
                    Booked[lab] = new Book { Rack = rk, At = now };
                    if (BtcKitReserve.On) BtcKitReserve.Reserve(lab, rk.gameObject, false);
                    sent++;
                }
                catch (Exception e) { BtcLog.Error("KitArm", "order: " + e.Message); }
            }
            Free.Clear();
            BtcLog.Info("KitArm", "class " + c + " for " + (user.name ?? "?") + ": " + sent + " sent, " + total + " kits left without a worker");
        }

        // ------------------------------------------------------------------ net
        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            Booked.Clear();
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                if (NetworkServer.active)
                {
                    NetworkServer.UnregisterHandler(Msg);
                    NetworkServer.RegisterHandler(Msg, OnArmAtHost);
                }
            }
            catch (Exception e) { BtcLog.Error("KitArm", "RegisterHandlers: " + e); }
        }

        private static void OnArmAtHost(NetworkMessage nm)
        {
            try
            {
                if (!BtcNetworkGate.Enabled || nm.conn == null) return;
                int c = nm.ReadMessage<IntegerMessage>().value;
                User u = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(nm.conn) : null;
                if (u == null) return;
                Arm(u, c);
            }
            catch (Exception e) { BtcLog.Error("KitArm", "OnArmAtHost: " + e); }
        }
    }

    /// One-shot recount at the end of a frame in which rack content changed; disabled the rest of the time.
    internal class BtcKitArmTick : MonoBehaviour
    {
        private void Awake() { BtcKitArm.Waker = this; enabled = false; }
        private void OnDestroy() { if (BtcKitArm.Waker == this) BtcKitArm.Waker = null; }
        private void LateUpdate()
        {
            enabled = false;
            try { BtcKitArm.Recount(); } catch (Exception e) { BtcLog.Error("KitArm", "recount: " + e.Message); }
        }
    }
}

namespace BetterThanCastle.Patches
{
    // Rack content changed (kit taken / stored): the kit buttons recount (Postfix: runs also when the rack filter prefix skips vanilla).
    [HarmonyPatch(typeof(ToolRack), "RefreshExclusivity")]
    internal static class KitArmRackChangedPatch
    {
        private static void Postfix()
        {
            try { BtcKitArm.OnRackChanged(); }
            catch (Exception e) { BtcLog.Error("Hook", "KitArmRackChanged: " + e.Message); }
        }
    }
}
