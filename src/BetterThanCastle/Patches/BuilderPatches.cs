using System;
using Brix.Game.AI;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Legacy: "return BtcQol.AlliesPass || (vanilla expression)".
    [HarmonyPatch(typeof(Builder), "CanPassThrough")]
    internal static class BuilderCanPassThroughPatch
    {
        private static void Postfix(ref bool __result)
        {
            try { if (!__result && BtcQol.AlliesPass) __result = true; }
            catch (Exception e) { BtcLog.Error("Hook", "Builder.CanPassThrough: " + e); }
        }
    }
}
