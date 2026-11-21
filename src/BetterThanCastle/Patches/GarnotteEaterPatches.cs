using System;
using System.Collections.Generic;
using Brix.Game;
using Brix.Legacy;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // mov.noAutoPick: while a worker digs/harvests, its GarnotteEater (radius 4, no filter) absorbs every loose item around.
    // The ore the dig itself drops is fresh; everything older was lying there before. With the rule on, only garnottes
    // that appeared in the last few seconds are absorbed.
    [HarmonyPatch(typeof(Garnotte), "Initialize")]
    internal static class GarnotteBornInitPatch
    {
        private static void Postfix(Garnotte __instance) { BtcAutoPick.NoteBorn(__instance); }
    }

    [HarmonyPatch(typeof(Garnotte), "ResetComponent")]
    internal static class GarnotteBornResetPatch
    {
        private static void Postfix(Garnotte __instance) { BtcAutoPick.NoteBorn(__instance); }
    }

    [HarmonyPatch(typeof(GarnotteEater), "AbsorbGarnottes", new Type[] { typeof(HashSet<GameObject>) })]
    internal static class GarnotteEaterAbsorbPatch
    {
        private static void Prefix(GarnotteEater __instance, HashSet<GameObject> gos)
        {
            try
            {
                if (gos.Count > 0) BtcAutoPick.DropOld(__instance, gos);
            }
            catch (Exception ex)
            {
                BtcLog.Error("AutoPick", ex.Message);
            }
        }
    }

    // A manual cleanup order (pie menu) or a Cleanup-project litter goal sets a filter on the eater: it must take
    // everything in reach, old or not. Starve ends it.
    [HarmonyPatch(typeof(GarnotteEater), "EatOnly")]
    internal static class GarnotteEaterEatOnlyPatch
    {
        private static void Prefix(GarnotteEater __instance) { BtcAutoPick.SetManual(__instance, true); }
    }

    [HarmonyPatch(typeof(GarnotteEater), "EatOnlySimilarsTo")]
    internal static class GarnotteEaterEatOnlySimilarsPatch
    {
        private static void Prefix(GarnotteEater __instance) { BtcAutoPick.SetManual(__instance, true); }
    }

    [HarmonyPatch(typeof(GarnotteEater), "Starve")]
    internal static class GarnotteEaterStarvePatch
    {
        private static void Postfix(GarnotteEater __instance) { BtcAutoPick.SetManual(__instance, false); }
    }

    // Pooled garnottes are re-activated without Initialize/ResetComponent: OnEnable is the one hook every spawn goes through.
    [HarmonyPatch(typeof(Garnotte), "OnEnable")]
    internal static class GarnotteBornEnablePatch
    {
        private static void Postfix(Garnotte __instance) { BtcAutoPick.NoteBorn(__instance); }
    }

    // A unit dropping its bag: those items are mess (not the project's fresh output).
    [HarmonyPatch(typeof(Brix.Game.Semantique.Recepteur), "DropAll", new Type[] { typeof(List<GameObject>) })]
    internal static class RecepteurDropAllPatch
    {
        private static void Prefix(List<GameObject> objects)
        {
            try { BtcAutoPick.NoteDropped(objects); }
            catch (Exception ex) { BtcLog.Error("AutoPick", ex.Message); }
        }
    }
}
