using System;
using Brix.Game.AI;
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
}
