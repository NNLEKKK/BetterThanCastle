using System;
using Brix.Input;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Advanced Hotkeys > Orders > "order_any_distance": vanilla raycasts every picker with
    // Picking.MaxDistance, so with the camera zoomed far out the click hits nothing and the order is lost.
    // On: the ray length is raised before each picking update; Off (or master switch off): the original value is restored.
    [HarmonyPatch(typeof(Picking), "OnUpdate")]
    internal static class PickingOnUpdatePatch
    {
        private const float AnyDistance = 100000f;
        private static float _vanilla = -1f;

        private static void Prefix(Picking __instance)
        {
            try
            {
                if (_vanilla < 0f && __instance.MaxDistance < AnyDistance)
                {
                    _vanilla = __instance.MaxDistance;
                    BtcLog.Info("Hotkeys", "Picking.MaxDistance vanilla=" + _vanilla);
                }
                bool on = BtcHotkeys.Enabled && BtcHotkeys.GetBool("order_any_distance", true);
                float want = on ? AnyDistance : (_vanilla > 0f ? _vanilla : __instance.MaxDistance);
                if (__instance.MaxDistance != want) __instance.MaxDistance = want;
            }
            catch (Exception ex)
            {
                BtcLog.Error("Hook", "Picking.OnUpdate: " + ex.Message);
            }
        }
    }
}
