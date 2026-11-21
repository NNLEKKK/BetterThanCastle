using System;
using System.Collections.Generic;
using Brix.Input;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // "Focus without auto-run": the vanilla right-click order on an enemy is split, ranged units go to BtcFocusHold (hold position and shoot).
    [HarmonyPatch(typeof(PickerOrderGiver), "DirectOrder", new Type[] { typeof(List<GameObject>), typeof(GameObject) })]
    internal static class FocusHoldDirectOrderPatch
    {
        private static bool Prefix(List<GameObject> listGO, GameObject target)
        {
            return BtcFocusHold.OnVanillaOrder(listGO, target);
        }
    }

    // Vanilla mouse binds (LMB/RMB/... with an optional modifier) edited in Advanced Hotkeys: vanilla only maps keyboard entries into Rewired.
    [HarmonyPatch(typeof(BindingsProvider), "Reload")]
    internal static class VanillaMouseBindsReloadPatch
    {
        private static void Postfix()
        {
            BtcVanillaBinds.ApplyMouse();
        }
    }
}
