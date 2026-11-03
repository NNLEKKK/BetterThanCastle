using System;
using Brix.Game.AI;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(IdleProject), "DecideWhatToDo")]
    internal static class IdleProjectDecideWhatToDoPatch
    {
        private static bool Prefix(Labor labor, ref IdleProject.IdleActions __result)
        {
            try
            {
                if (BtcBuildAI.HoldCarry(labor)) { __result = IdleProject.IdleActions.None; return false; }
            }
            catch (Exception e) { BtcLog.Error("Hook", "IdleProject.DecideWhatToDo: " + e); }
            return true;
        }
    }
}
