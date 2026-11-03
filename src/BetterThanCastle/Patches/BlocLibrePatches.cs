using System;
using Brix.Legacy;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Legacy: BtcQol.NoteItem(gameObject) at end of Initialize.
    // NOTE: the legacy "BlocLibre : IPassThrough" interface cannot be added by Harmony; the replacement is
    // BtcQol.ItemPass(Volume) which belongs in a Volume.Initialize postfix (PassThrough = ItemPass(this)).
    [HarmonyPatch(typeof(BlocLibre), "Initialize")]
    internal static class BlocLibreInitializePatch
    {
        private static void Postfix(BlocLibre __instance)
        {
            try { BtcQol.NoteItem(__instance.gameObject); }
            catch (Exception e) { BtcLog.Error("Hook", "BlocLibre.Initialize: " + e); }
        }
    }
}
