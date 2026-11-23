using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game.AI;
using HarmonyLib;

namespace BetterThanCastle
{
    // Rule mov.betterPaths (host), two vanilla route-search bugs fixed after Castle Story Plus (Tricky12321, GPLv3):
    // a route is found over coarse terrain blocks, then refined near the start; the refinement stopped after 100 nodes (so a
    // unit walked to far stairs while stairs next to it led down) and WrapUpSearch called InitImprove() a second time, which
    // reset the route to the coarse result and threw the refined one away.
    internal static class BtcBetterPaths
    {
        private static int _idx = -2;

        public static void AddRows()
        {
            BtcGameplay.Add("mov.betterPaths", "mov", "Better routes", "Точнее пути", 1f, 0f, 1f, 1f, true, "Two vanilla route bugs fixed: the near part of a route is refined properly (vanilla gave up after 100 steps and units walked to far stairs) and the refined route is kept (vanilla threw it away).", "Исправлены два бага путей ванили: ближняя часть пути нормально уточняется (ваниль сдавалась через 100 шагов, и юниты шли к дальней лестнице) и уточнённый путь сохраняется (ваниль его выбрасывала).");
        }

        public static bool On
        {
            get
            {
                if (_idx == -2) _idx = BtcGameplay.Idx("mov.betterPaths");
                float[] e = BtcGameplay.E;
                return _idx >= 0 && e != null && _idx < e.Length && e[_idx] > 0.5f;
            }
        }

        internal static int ImproveLimit(int vanilla) { return On ? 1000 : vanilla; }
        internal static bool ReInit() { return !On; }
    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(SearchPathRequest), "InitSearch")]
    internal static class SearchPathRequestImproveLimitPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            MethodInfo lim = AccessTools.Method(typeof(BtcBetterPaths), "ImproveLimit");
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].LoadsConstant(100)) continue;
                list.Insert(i + 1, new CodeInstruction(OpCodes.Call, lim));
                n++;
                break;
            }
            if (n == 0) BtcLog.Error("Hook", "SearchPathRequest.InitSearch: improve limit 100 not found");
            return list;
        }
    }

    [HarmonyPatch(typeof(SearchPathRequest), "WrapUpSearch")]
    internal static class SearchPathRequestWrapUpPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator gen)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            MethodInfo init = AccessTools.Method(typeof(SearchPathRequest), "InitImprove");
            for (int i = 0; i < list.Count - 1; i++)
            {
                if (!list[i].IsLdarg(0) || !list[i + 1].Calls(init)) continue;
                // if (BtcBetterPaths.ReInit()) this.InitImprove();
                Label skip = gen.DefineLabel();
                list.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BtcBetterPaths), "ReInit")));
                list.Insert(i + 1, new CodeInstruction(OpCodes.Brfalse, skip));
                if (i + 4 < list.Count) list[i + 4].labels.Add(skip);
                else { BtcLog.Error("Hook", "SearchPathRequest.WrapUpSearch: no instruction after InitImprove"); return instructions; }
                // move labels of the original first instruction (ldarg.0) to our new first one
                list[i].labels.AddRange(list[i + 2].labels);
                list[i + 2].labels.Clear();
                return list;
            }
            BtcLog.Error("Hook", "SearchPathRequest.WrapUpSearch: InitImprove call not found");
            return list;
        }
    }
}
