using System;
using System.Collections.Generic;
using Brix.Game.AI;
using Brix.Input.ContextualOrders;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Vanilla Event_JumpOff gives every unit Formation.Spot(i) (all the same block when the target has no walkable node).
    // Per-unit prefix: replaces the position with a block of a line through the target. Client-side; vanilla CmdGoAndJumpTo bytes.
    [HarmonyPatch(typeof(DirectOrderProject), "GoAndJumpTo")]
    internal static class DirectOrderProjectGoAndJumpToPatch
    {
        private static void Prefix(Labor labor, ref Vector3 position)
        {
            try { if (BtcJumpLine.On) BtcJumpLine.Adjust(labor, ref position); }
            catch (Exception e) { BtcLog.Error("JumpLine", e.ToString()); }
        }
    }

    // Records the raw click target (Formation.Init is called right before the GoAndJumpTo loop).
    [HarmonyPatch(typeof(Formation), "Init", new Type[] { typeof(Vector3), typeof(List<GameObject>) })]
    internal static class FormationInitJumpLinePatch
    {
        private static void Postfix(Vector3 targetPos)
        {
            try { BtcJumpLine.NoteTarget(targetPos); }
            catch (Exception e) { BtcLog.Error("JumpLine", e.ToString()); }
        }
    }
}
