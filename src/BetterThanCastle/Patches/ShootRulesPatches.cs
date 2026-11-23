using System;
using Brix.Game.Network;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // F1 (BtcShootRules).
    [HarmonyPatch(typeof(UNetContextualCmd), "CmdFireAt")]
    internal static class ShootRulesFireAtPatch
    {
        private static void Prefix(ref Vector3 location)
        {
            try { BtcShootRules.FixPoint(ref location); }
            catch (Exception e) { BtcLog.Error("ShootRules", "FixPoint: " + e.Message); }
        }
    }
}
