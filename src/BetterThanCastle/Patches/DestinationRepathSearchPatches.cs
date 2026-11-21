using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game.AI;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Vanilla: for (int i = 0; i < 100; i++) { if (!_pathSearch.MoveNext()) break; }  -> 100 becomes BtcQol.RepathSteps
    [HarmonyPatch(typeof(DestinationRepathSearch), "Search")]
    internal static class DestinationRepathSearchSearchPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            MethodInfo getter = AccessTools.PropertyGetter(typeof(BtcQol), "RepathSteps");
            for (int i = 0; i < list.Count; i++)
            {
                CodeInstruction c = list[i];
                bool is100 = (c.opcode == OpCodes.Ldc_I4_S || c.opcode == OpCodes.Ldc_I4) && Convert.ToInt32(c.operand) == 100;
                if (is100)
                {
                    var repl = new CodeInstruction(OpCodes.Call, getter);
                    repl.MoveLabelsFrom(c);
                    list[i] = repl;
                    return list;
                }
            }
            BtcLog.Error("Hook", "DestinationRepathSearch.Search: constant 100 not found");
            return list;
        }
    }
}
