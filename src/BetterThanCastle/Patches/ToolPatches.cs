using System;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Legacy calls BtcSimplePhys.Drop right after the position is set, before AddForce; a postfix is equivalent
    // (it only adjusts the transform position).
    [HarmonyPatch(typeof(Tool), "DropFrom")]
    internal static class ToolDropFromPatch
    {
        private static void Postfix(Tool __instance, Transform user)
        {
            try { BtcSimplePhys.Drop(__instance.gameObject, user); }
            catch (Exception e) { BtcLog.Error("Hook", "Tool.DropFrom: " + e); }
        }
    }
}
