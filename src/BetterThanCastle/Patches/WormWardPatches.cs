using System;
using Brix.Game.Components.Wards;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    /// <summary>Wormhole wards (BtcWormWard) are sentinel ward objects that never shoot: no target is ever acceptable.</summary>
    [HarmonyPatch(typeof(WardAttack), "CanFireAt")]
    internal static class WormWardCanFirePatch
    {
        private static void Postfix(WardAttack __instance, ref bool __result)
        {
            if (!__result || BtcWormWard.Count == 0) return;
            try { if (BtcWormWard.IsWorm(__instance.gameObject)) __result = false; }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard CanFireAt: " + e.Message); }
        }
    }

#if DEV
    [HarmonyPatch(typeof(WardController), "OnRequested")]
    internal static class WormWardTraceRequestedPatch
    {
        private static void Postfix(WardController __instance)
        {
            BtcLog.Info("WormTrace", "OnRequested " + __instance.name + " id=" + __instance.gameObject.GetInstanceID() + " worm=" + BtcWormWard.IsWorm(__instance.gameObject) + " at=" + __instance.transform.position);
        }
    }
#endif

    /// <summary>A ward object going back to the pool loses its wormhole mark (the pool reuses it as a plain sentinel ward).</summary>
    [HarmonyPatch(typeof(WardController), "ResetComponent")]
    internal static class WormWardResetPatch
    {
        private static void Postfix(WardController __instance)
        {
#if DEV
            BtcLog.Info("WormTrace", "ResetComponent " + __instance.name + " id=" + __instance.gameObject.GetInstanceID() + " worm=" + BtcWormWard.IsWorm(__instance.gameObject) + " act=" + __instance.gameObject.activeInHierarchy + " parent=" + (__instance.transform.parent != null ? __instance.transform.parent.name : "-"));
#endif
            if (BtcWormWard.Count == 0) return;
            try { BtcWormWard.Forget(__instance.gameObject); }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard ResetComponent: " + e.Message); }
        }
    }
}
