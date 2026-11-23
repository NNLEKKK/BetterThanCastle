using System;
using System.Collections.Generic;
using Brix.Game.AI;
using Brix.Game.Locomotion;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
    // Rule mov.instant: a new order breaks a chain of 1-block jumps. Vanilla hands a new path to the locomotion only while
    // CanMove() (InPlace/Walking/Running/Halting/TurnOnSpot); ClimbUp and StepDown are not in that list, and between two jumps
    // InPlace.Update starts the next jump of the OLD path in the same frame it is entered. On a stair of full blocks the AI
    // (FollowPathRequest, NavigateOrderData.Validate) almost never caught that frame, so the unit kept jumping along the old
    // route and the order could even fail (Validate gives up after 1 s). After a player's order the unit now stays InPlace
    // instead of starting the next jump of the old path until the new path arrives (Locomotion4.SetPath) or 1.5 s pass.
    // The jump in progress still ends (no mid-air cut), so the turn comes after at most one jump.
    internal static class BtcClimbBreak
    {
        private const float Hold = 1.5f;
        private static readonly Dictionary<int, float> OrderAt = new Dictionary<int, float>();
        internal static int Held;
#if DEV
        private static int Logged;
        private static readonly bool DevOff = Environment.GetEnvironmentVariable("BTC_NOCLIMBBREAK") == "1";
#endif

        internal static void NoteOrder(Labor l)
        {
            Locomotion4 loc = l.navigation != null ? l.navigation.Locomotion : null;
            if (loc == null) return;
            if (OrderAt.Count > 512) OrderAt.Clear();
            OrderAt[loc.GetInstanceID()] = Time.time;
        }

        internal static void OnPath(Locomotion4 loc)
        {
            if (OrderAt.Count == 0) return;
#if DEV
            float at;
            if (OrderAt.TryGetValue(loc.GetInstanceID(), out at) && Logged++ < 200)
                BtcLog.Info("ClimbBreak", loc.name + " path after " + ((Time.time - at) * 1000f).ToString("0") + " ms held=" + Held + " state=" + (loc.IsClimbing() ? "jump" : "ground"));
#endif
            OrderAt.Remove(loc.GetInstanceID());
        }

        /// <summary>True when InPlace must not start the next jump of the old path this frame.</summary>
        internal static bool Holds(InPlace s)
        {
            if (OrderAt.Count == 0) return false;
#if DEV
            if (DevOff) return false;
#endif
            Locomotion4 loc = s.Locomotion;
            if (loc == null) return false;
            int id = loc.GetInstanceID();
            float at;
            if (!OrderAt.TryGetValue(id, out at)) return false;
            if (Time.time - at > Hold || !BtcInstantOrders.On) { OrderAt.Remove(id); return false; }
            DestinationIterator it = loc.PathIterator;
            if (it == null || !it.HasCurrent) return false;
            if (!IsJump(it.Current) && !IsJump(it.Peek)) return false;
            Held++;
            return true;
        }

        private static bool IsJump(WalkNode n) { return n != null && (n.MustClimbUp() || n.MustStepDown()); }
    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(InPlace), "Update")]
    internal static class ClimbBreakInPlacePatch
    {
        private static bool Prefix(InPlace __instance)
        {
            try { return !BtcClimbBreak.Holds(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "InPlace.Update climb break: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(Locomotion4), "SetPath")]
    internal static class ClimbBreakSetPathPatch
    {
        private static void Postfix(Locomotion4 __instance)
        {
            try { BtcClimbBreak.OnPath(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "Locomotion4.SetPath climb break: " + e.Message); }
        }
    }
}
