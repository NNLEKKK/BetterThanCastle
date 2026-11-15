using System;
using Brix.Game.AI;
using Brix.Input;
using Brix.UI.GameSelector;
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

    // Advanced Hotkeys > Orders > "no_double_click_select" (default off): a double-click on a unit is treated
    // as a single click (only that unit stays selected). The batch-select modifier still selects coworkers.
    [HarmonyPatch(typeof(UnitActionPicker), "OnFirstActionCharacter")]
    internal static class UnitActionPickerDoubleClickPatch
    {
        private static bool Prefix(Labor clickedCharacter, ClickInfo clickInfo)
        {
            try
            {
                if (clickedCharacter == null || clickInfo == null || clickInfo.actionName != "cam_action1") return true;
                if (!BtcHotkeys.Enabled || !BtcHotkeys.GetBool("no_double_click_select", false)) return true;
                if (PickingUtility.isInViewMode() || clickInfo.clickType != ClickType.doubleClick) return true;
                if (PickingUtility.Player.GetButton("cam_mouse_sec")) return true;
                // The first click of the pair already selected / toggled the unit; toggling again would undo it.
                if (!PickingUtility.isInToggleSelectMode()) UIGameSelector.Select(clickedCharacter.gameObject);
                return false;
            }
            catch (Exception ex)
            {
                BtcLog.Error("Hook", "UnitActionPicker.OnFirstActionCharacter: " + ex.Message);
                return true;
            }
        }
    }
}
