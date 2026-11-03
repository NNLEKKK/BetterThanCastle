using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Brix.Game.AI;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Vanilla: return Node.Sequence.Do(l => l.GetReadyToMove(), component)...; legacy prepends Do(BtcQol.OnRunOrder(this)).
    [HarmonyPatch(typeof(Labor), "GetReadyForNextTask")]
    internal static class LaborGetReadyForNextTaskPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SeqHook.InsertOnRun(instructions, "get_Sequence", OpCodes.Ldarg_0, "Labor.GetReadyForNextTask");
        }
    }

}
