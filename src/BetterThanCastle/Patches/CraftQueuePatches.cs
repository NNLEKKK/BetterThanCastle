using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Brix.Game.Components;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // QueueRecipe: the literal limit 8 becomes BtcCraftQueue.Max() (8 when BetterConnection is OFF)
    [HarmonyPatch(typeof(CraftingLabor), "QueueRecipe")]
    internal static class CraftQueueLimitPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(ins);
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].opcode == OpCodes.Ldc_I4_8)
                {
                    list[i] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BtcCraftQueue), "Max"));
                    n++;
                }
            }
            if (n != 1) { BtcLog.Error("Hook", "CraftingLabor.QueueRecipe: expected one ldc.i4.8, found " + n); return ins; }
            return list;
        }
    }

    [HarmonyPatch(typeof(CraftingState), "SetRecipes")]
    internal static class CraftQueueSetRecipesPatch
    {
        private static void Postfix(CraftingState __instance, List<RecipeInfo> recipes)
        {
            try { BtcCraftQueue.OnSetRecipes(__instance, recipes); }
            catch (Exception e) { BtcLog.Error("Hook", "CraftQueue.SetRecipes: " + e); }
        }
    }

    [HarmonyPatch(typeof(CraftingLabor), "get_QueuedRecipes")]
    internal static class CraftQueueGetterPatch
    {
        private static void Postfix(CraftingLabor __instance, List<RecipeInfo> __result)
        {
            try { BtcCraftQueue.OnQueued(__instance, __result); }
            catch (Exception e) { BtcLog.Error("Hook", "CraftQueue.QueuedRecipes: " + e); }
        }
    }
}
