using System;
using Brix.Game.AI;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Legacy balance: the legacy mod added `Corruptron.CanMakeWay() => true` (Corruptrons break through walls).
    // Minitron declares CanMakeWay (returns false) and Corruptron inherits it, so the hook sits on Minitron and filters by type.
    // NOTE: the game DLL used during development contained another mod's wall-climbing for Corruptron/Builder/Magitron;
    // that is not part of clean vanilla and must NOT be touched or referenced here.
    [HarmonyPatch(typeof(Minitron), "CanMakeWay")]
    internal static class MinitronCanMakeWayPatch
    {
        private static void Postfix(Minitron __instance, ref bool __result)
        {
            try { if (__instance is Corruptron) __result = true; }
            catch (Exception e) { BtcLog.Error("Hook", "Minitron.CanMakeWay: " + e); }
        }
    }
}
