using System;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // "No pie menu" hotkey (Safety group): while held, RMB-hold never starts the pie menu creation timer.
    // creationTime stays -1, so IsCreationWaiting/IsCreationDue are false and the drag path cannot open it either.
    // Plain RMB orders do not go through this class. Local input only, no network.
    [HarmonyPatch(typeof(RadialLayoutGroup), "SetCreationTime")]
    internal static class RadialLayoutGroupSetCreationTimePatch
    {
        private static bool Prefix()
        {
            try
            {
                return !BtcHotkeys.PieMenuBlocked();
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "RadialLayoutGroup.SetCreationTime: " + e.Message);
                return true;
            }
        }
    }
}
