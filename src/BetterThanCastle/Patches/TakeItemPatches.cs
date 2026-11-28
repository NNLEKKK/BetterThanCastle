using System;
using System.Collections;
using Brix.Legacy;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    /// <summary>PieContenu.GetButtonData(bouton): the "Take item..." slots BtcTake0..15 get the live name, icon and count of
    /// the stored item kind they stand for (BtcTakeItem). Local UI only.</summary>
    [HarmonyPatch(typeof(PieContenu), "GetButtonData")]
    internal static class PieGetButtonDataTakePatch
    {
        private static void Postfix(PieContenu.Bouton bouton, Hashtable __result)
        {
            try { if (bouton != null) BtcTakeItem.FillButton(bouton.nom, __result); }
            catch (Exception e) { BtcLog.Error("TakeItem", "button data: " + e.Message); }
        }
    }
}
