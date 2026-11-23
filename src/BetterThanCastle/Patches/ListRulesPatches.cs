using System;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Network;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // A8: no automatic trap re-arming (BtcTrapRearm). The manual re-arm sends the Activate instruction directly and skips HasWorkFor.
    [HarmonyPatch(typeof(ObjectActivator), "HasWorkFor")]
    internal static class TrapRearmHasWorkForPatch
    {
        private static void Postfix(ObjectActivator __instance, ref bool __result)
        {
            try { if (__result && BtcTrapRearm.BlockAuto(__instance)) __result = false; }
            catch (Exception e) { BtcLog.Error("TrapRearm", e.Message); }
        }
    }

    // N19: an Artificer ordered onto an enemy marks it for the faction's Sentinel Wards (host, BtcFocusMark.OnHostOrder).
    [HarmonyPatch(typeof(UNetContextualCmd), "CmdBatchedContextualActionGameObject")]
    internal static class FocusMarkAutoPatch
    {
        private static void Prefix(UNetContextualCmd __instance, GameObject[] arrayGO, GameObject target)
        {
            try { BtcFocusMark.OnHostOrder(__instance.gameObject, arrayGO, target); }
            catch (Exception e) { BtcLog.Error("FocusMark", "OnHostOrder: " + e.Message); }
        }
    }
}
