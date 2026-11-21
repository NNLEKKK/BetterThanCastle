using System;
using Brix.Game.Components.Tracking;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// TrackingRegistry.TrackingGroup.FactionEntryCount is `new int[8]` indexed by Faction.ID. Extended colours
    /// (BtcLobby.ExtendProtos) get uids 7..10 (Pink 8, Cyan 9, Black 10): AddEntry threw IndexOutOfRange, the unit was
    /// never tracked, enemy vision never saw it -> invisible for other players (and host observers never added it).
    /// Legacy mod had `new int[16]` + full-length clear; the port lost that hunk. Local capacity only (no wire change),
    /// inert for vanilla ids &lt; 8, so not gated (also needed when a BC-ON replay is played back with BC OFF).
    /// </summary>
    [HarmonyPatch(typeof(TrackingRegistry.TrackingGroup), MethodType.Constructor)]
    internal static class TrackingGroupCtorPatch
    {
        private static void Postfix(TrackingRegistry.TrackingGroup __instance)
        {
            try
            {
                int[] a = __instance.FactionEntryCount;
                if (a == null || a.Length < BtcColors.TrackCap)
                {
                    int[] b = new int[BtcColors.TrackCap];
                    if (a != null) Array.Copy(a, b, a.Length);
                    __instance.FactionEntryCount = b;
                }
                BtcColors.TrackPatched = true;
            }
            catch (Exception e) { BtcLog.Error("Hook", "TrackingGroup.ctor: " + e); }
        }
    }

    /// <summary>Vanilla ResetObject clears only FactionEntryCount[0..7]: clear the extended tail too (pooled groups).</summary>
    [HarmonyPatch(typeof(TrackingRegistry.TrackingGroup), "ResetObject")]
    internal static class TrackingGroupResetObjectPatch
    {
        private static void Postfix(TrackingRegistry.TrackingGroup __instance)
        {
            try
            {
                int[] a = __instance.FactionEntryCount;
                if (a != null && a.Length > 8) Array.Clear(a, 8, a.Length - 8);
            }
            catch (Exception e) { BtcLog.Error("Hook", "TrackingGroup.ResetObject: " + e); }
        }
    }
}
