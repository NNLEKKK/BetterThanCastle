using System;
using Brix.Game.Components.Volumes;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Legacy: BlocLibre implemented IPassThrough, so Volume.Initialize's GetComponent<IPassThrough>() found it.
    // Harmony cannot add an interface to BlocLibre; instead, after Initialize, install BtcQol.ItemPass(this) for volumes
    // that carry a BlocLibre (ItemPass returns PassThroughObstacle.Null for everything else, which is what vanilla assigned).
    [HarmonyPatch(typeof(Volume), "Initialize")]
    internal static class VolumeInitializePatch
    {
        private static void Postfix(Volume __instance)
        {
            try
            {
                if (__instance.GetComponent<Brix.Legacy.BlocLibre>() != null)
                {
                    __instance.PassThrough = BtcQol.ItemPass(__instance);
                }
            }
            catch (Exception e) { BtcLog.Error("Hook", "Volume.Initialize: " + e); }
        }
    }
}
