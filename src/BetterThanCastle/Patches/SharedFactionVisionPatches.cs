using System.Collections.Generic;
using Brix.Game;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(SharedFactionVision), "AddToRegularTracking")]
    internal static class SharedFactionVisionAddToRegularTrackingPatch
    {
        // vanilla: v.FactionsSeeingMe.Add(Faction.ID)  ->  BtcFactionCap.SeeAdd(v, Faction.ID)
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            return G2Util.RedirectSubsetCall(ins, "Add", AccessTools.Method(typeof(BtcFactionCap), "SeeAdd"),
                "SharedFactionVision.AddToRegularTracking");
        }
    }

    [HarmonyPatch(typeof(SharedFactionVision), "RemoveFromRegularTracking")]
    internal static class SharedFactionVisionRemoveFromRegularTrackingPatch
    {
        // vanilla: v.FactionsSeeingMe.Remove(Faction.ID)  ->  BtcFactionCap.SeeRemove(v, Faction.ID)
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            return G2Util.RedirectSubsetCall(ins, "Remove", AccessTools.Method(typeof(BtcFactionCap), "SeeRemove"),
                "SharedFactionVision.RemoveFromRegularTracking");
        }
    }
}
