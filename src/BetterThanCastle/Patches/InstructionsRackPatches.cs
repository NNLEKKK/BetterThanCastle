using System;
using Brix.Legacy;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Pie menu events/conditions for the rack kit filter (Info/Livre text is injected by LivrePatches.cs).
    [HarmonyPatch(typeof(Instructions), "Batire")]
    internal static class InstructionsBatireRackPatch
    {
        private static void Postfix()
        {
            try { BtcRackFilter.Install(); }
            catch (Exception e) { BtcLog.Error("Hook", "Instructions.Batire (rack filter): " + e); }
        }
    }
}
