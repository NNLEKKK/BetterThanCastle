using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Brix.Game.Components;
using Brix.Game.AI;
using Brix.Game.Semantique;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Better Workbenches (Features/BtcWorkbench.cs). QueueRecipe: the literal limit 8 becomes BtcWorkbench.Max() (8 when the rule or BetterConnection is off)
    [HarmonyPatch(typeof(CraftingLabor), "QueueRecipe")]
    internal static class WorkbenchQueueLimitPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(ins);
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].opcode == OpCodes.Ldc_I4_8)
                {
                    list[i].opcode = OpCodes.Call;   // in place: keeps labels
                    list[i].operand = AccessTools.Method(typeof(BtcWorkbench), "Max");
                    n++;
                }
            }
            if (n != 1) { BtcLog.Error("Hook", "CraftingLabor.QueueRecipe: expected one ldc.i4.8, found " + n); return ins; }
            return list;
        }
    }

    [HarmonyPatch(typeof(CraftingState), "SetRecipes")]
    internal static class WorkbenchSetRecipesPatch
    {
        private static void Postfix(CraftingState __instance, List<RecipeInfo> recipes)
        {
            try { BtcWorkbench.OnSetRecipes(__instance, recipes); }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.SetRecipes: " + e); }
        }
    }

    [HarmonyPatch(typeof(CraftingLabor), "get_QueuedRecipes")]
    internal static class WorkbenchQueuedPatch
    {
        private static void Prefix(CraftingLabor __instance, out bool __state)
        {
            __state = false;
            try { __state = BtcWorkbench.WillRefill(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.QueuedRecipes prefix: " + e); }
        }

        private static void Postfix(CraftingLabor __instance, List<RecipeInfo> __result, bool __state)
        {
            try { BtcWorkbench.OnQueued(__instance, __result, __state); }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.QueuedRecipes: " + e); }
        }
    }

    // leftovers never block auto-store: the worker context for CraftingInventory.ItemToPickup (BtcWorkbench.PickFor)
    [HarmonyPatch(typeof(CraftingStation), "CanPickUpProduction")]
    internal static class WorkbenchCanPickPatch
    {
        private static void Prefix(Labor labor) { BtcWorkbench.AskBegin(labor); }
        private static void Finalizer() { BtcWorkbench.AskEnd(); }
    }

    [HarmonyPatch(typeof(CraftingStation), "DecideWhatToDo")]
    internal static class WorkbenchDecidePatch
    {
        private static void Postfix(CraftingStation __instance, Labor labor, ref CraftingStation.Actions __result)
        {
            try { BtcWorkbench.Decided(__instance, labor, ref __result); }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.DecideWhatToDo: " + e); }
        }
    }

    [HarmonyPatch(typeof(CraftingStation), "HasWorkFor")]
    internal static class WorkbenchHasWorkPatch
    {
        private static void Postfix(CraftingStation __instance, Labor labor, ref bool __result)
        {
            try { if (!__result && BtcWorkbench.HelperHasWork(__instance, labor)) __result = true; }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.HasWorkFor: " + e); }
        }
    }

    [HarmonyPatch(typeof(CraftingStation), "GetPackage")]
    internal static class WorkbenchPackagePatch
    {
        private static bool Prefix(CraftingStation __instance, CraftingStation.Actions action, ref OrderPackage<Labor> __result)
        {
            try
            {
                OrderPackage<Labor> h = BtcWorkbench.HelperPackage(__instance, action);
                if (h != null) { __result = h; return false; }   // helper: no SetPrepare(Hire), the operator stays
            }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.HelperPackage: " + e); }
            BtcWorkbench.PackageBegin(__instance, action);
            return true;
        }

        private static void Finalizer() { BtcWorkbench.PackageEnd(); }
    }

    [HarmonyPatch(typeof(CraftingInventory), "ItemToPickup")]
    internal static class WorkbenchItemToPickupPatch
    {
        private static bool Prefix(CraftingInventory __instance, ref GameObject go, ref bool __result)
        {
            try
            {
                GameObject o;
                if (!BtcWorkbench.PickFor(__instance, out o)) return true;
                go = o;
                __result = o != null;
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.ItemToPickup: " + e); return true; }
        }
    }

    // rack limit: the compiler-generated lambdas of CraftingLabor.ProcessQueue, found by the calls in their IL (PopRecipe / get_FirstRecipe)
    internal static class WorkbenchLambdas
    {
        private static bool _done;
        internal static System.Reflection.MethodInfo Cond, Pop;

        internal static void Find()
        {
            if (_done) return;
            _done = true;
            try
            {
                int popTok = AccessTools.Method(typeof(CraftingLabor), "PopRecipe").MetadataToken;
                int firstTok = AccessTools.PropertyGetter(typeof(CraftingLabor), "FirstRecipe").MetadataToken;
                List<Type> types = new List<Type> { typeof(CraftingLabor) };
                types.AddRange(typeof(CraftingLabor).GetNestedTypes(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public));
                foreach (Type t in types)
                    foreach (System.Reflection.MethodInfo m in t.GetMethods(System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Static
                        | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
                    {
                        if (m.Name.IndexOf("ProcessQueue", StringComparison.Ordinal) < 0 || m.Name == "ProcessQueue") continue;
                        System.Reflection.MethodBody body = m.GetMethodBody();
                        byte[] il = body != null ? body.GetILAsByteArray() : null;
                        if (il == null) continue;
                        if (m.ReturnType == typeof(bool) && Calls(il, firstTok)) Cond = m;
                        else if (m.ReturnType == typeof(void) && Calls(il, popTok)) Pop = m;
                    }
                if (Cond == null || Pop == null) BtcLog.Error("Hook", "CraftingLabor.ProcessQueue lambdas not found (cond " + (Cond != null) + ", pop " + (Pop != null) + "): rack limit off");
            }
            catch (Exception e) { BtcLog.Error("Hook", "CraftingLabor.ProcessQueue lambdas: " + e); }
        }

        private static bool Calls(byte[] il, int tok)
        {
            for (int i = 0; i + 4 < il.Length; i++)
                if ((il[i] == 0x28 || il[i] == 0x6F) && BitConverter.ToInt32(il, i + 1) == tok) return true;
            return false;
        }

        internal static CraftingLabor LaborArg(object[] args)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length; i++) { CraftingLabor l = args[i] as CraftingLabor; if (l != null) return l; }
            return null;
        }
    }

    [HarmonyPatch]
    internal static class WorkbenchPopCondPatch
    {
        private static bool Prepare() { WorkbenchLambdas.Find(); return WorkbenchLambdas.Cond != null && WorkbenchLambdas.Pop != null; }
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods() { yield return WorkbenchLambdas.Cond; }

        private static void Postfix(object[] __args, ref bool __result)
        {
            try { if (__result && !BtcWorkbench.CanPop(WorkbenchLambdas.LaborArg(__args))) __result = false; }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.PopCond: " + e); }
        }
    }

    [HarmonyPatch]
    internal static class WorkbenchPopPatch
    {
        private static bool Prepare() { WorkbenchLambdas.Find(); return WorkbenchLambdas.Cond != null && WorkbenchLambdas.Pop != null; }
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods() { yield return WorkbenchLambdas.Pop; }

        private static bool Prefix(object[] __args)
        {
            try { return !BtcWorkbench.Pop(WorkbenchLambdas.LaborArg(__args)); }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.Pop: " + e); return true; }
        }
    }

    [HarmonyPatch(typeof(CraftingLabor), "FinishRecipe")]
    internal static class WorkbenchFinishPatch
    {
        private static void Postfix(CraftingLabor __instance)
        {
            try { BtcWorkbench.OnFinished(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.FinishRecipe: " + e); }
        }
    }

    // items a later queued order needs are not hauled out
    [HarmonyPatch(typeof(CraftingStation), "RefeshInventory")]
    internal static class WorkbenchRefreshInventoryPatch
    {
        private static void Postfix(CraftingStation __instance)
        {
            try { BtcWorkbench.OnStationInventory(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.RefeshInventory: " + e); }
        }
    }

    [HarmonyPatch(typeof(CraftingInventory), "RefreshAmounts")]
    internal static class WorkbenchRefreshAmountsPatch
    {
        private static void Postfix(CraftingInventory __instance)
        {
            try { BtcWorkbench.AfterAmounts(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.RefreshAmounts: " + e); }
        }
    }

    // builders without a stockpile take ingots etc. straight from stations (region material list)
    [HarmonyPatch(typeof(Brix.Game.AI.KnowledgeSpace.KnowledgeRegion), "UniqueResources")]
    internal static class WorkbenchRegionResourcesPatch
    {
        private static void Postfix(Brix.Game.AI.KnowledgeSpace.KnowledgeRegion __instance, HashSet<Brix.External.Factories.Factory.AssetKey> uniqueItems)
        {
            try { BtcWorkbench.AfterRegionResources(__instance, uniqueItems); }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.UniqueResources: " + e.Message); }
        }
    }

    // every crafting station is a pickup source (a loom's rope for the workbench)
    [HarmonyPatch(typeof(Brix.Game.AI.KnowledgeSpace.KnowledgeRegion), "AddUniqueResource")]
    internal static class WorkbenchRegionAddPatch
    {
        private static void Prefix(Brix.Game.Components.Volumes.IVolume v)
        {
            try { BtcWorkbench.BeforeRegionAdd(v); }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.AddUniqueResource: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Brix.Game.AI.KnowledgeSpace.Knowledge), "_ClosestObjectToPickup_Test")]
    internal static class WorkbenchPickupTestPatch
    {
        private static void Postfix(IGameComponent gc, ref bool __result)
        {
            if (__result && !BtcWorkbench.PickupAllowed(gc)) __result = false;
        }
    }

    // no stockpile: leftovers and products go to the station that needs them (or back to the furnace)
    [HarmonyPatch(typeof(Brix.Game.AI.KnowledgeSpace.Knowledge), "ClosestStorageWithRoomFor")]
    internal static class WorkbenchStoreFallbackPatch
    {
        private static void Postfix(Labor source, IGameComponent itemToStore, Brix.Game.Faction faction, ref IGameComponent __result)
        {
            try
            {
                IGameComponent rack = BtcKitAssemble.Choose(source, itemToStore, faction);   // kit parts: the rack that completes a kit
                if (rack != null) { __result = rack; return; }
                if (__result == null) __result = BtcWorkbench.StoreFallback(source, itemToStore, faction);
            }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.StoreFallback: " + e.Message); }
        }
    }

    // idle workers merge started kits on racks (BtcKitAssemble)
    [HarmonyPatch(typeof(IdleProject), "PackageFor")]
    internal static class KitAssembleIdlePatch
    {
        private static void Postfix(IdleProject __instance, object action, Labor labor, ref OrderPackage<Labor> __result)
        {
            try
            {
                int a = Convert.ToInt32(action);
                if (a == 1 || a == 2) BtcKitAssemble.OnIdle(__instance, labor, ref __result);   // WalkInPoint, Sit
            }
            catch (Exception e) { BtcLog.Error("Hook", "KitAssemble.OnIdle: " + e.Message); }
        }
    }

    // free room of crafting stations (both recompute paths of Recepteur._currentCapacity)
    [HarmonyPatch(typeof(Recepteur), "RegenerateDescriptionsWithExclusions")]
    internal static class WorkbenchCapacityPatch
    {
        private static void Postfix(Recepteur __instance)
        {
            try { BtcWorkbench.Boost(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.Regenerate: " + e); }
        }
    }

    [HarmonyPatch(typeof(Recepteur), "RemoveFrom")]
    internal static class WorkbenchCapacityEmptyPatch
    {
        private static void Postfix(IRecepteur recept)
        {
            try { BtcWorkbench.Boost(recept as Recepteur); }
            catch (Exception e) { BtcLog.Error("Hook", "Workbench.RemoveFrom: " + e); }
        }
    }
}
