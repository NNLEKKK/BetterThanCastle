using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game.AI;
using Brix.Game;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // mov.noAutoPick: while a worker digs/harvests, its GarnotteEater (radius 4, no filter) absorbs every loose item around.
    // With the rule on, it absorbs only the items its task needs (BtcAutoPick.Wanted).
    [HarmonyPatch(typeof(GarnotteEater), "AbsorbGarnottes", new Type[] { typeof(HashSet<GameObject>) })]
    internal static class GarnotteEaterAbsorbPatch
    {
        private static void Prefix(GarnotteEater __instance, HashSet<GameObject> gos)
        {
            try
            {
                if (gos.Count > 0) BtcAutoPick.DropOld(__instance, gos);
            }
            catch (Exception ex)
            {
                BtcLog.Error("AutoPick", ex.Message);
            }
        }
    }

    // A manual cleanup order (pie menu) or a Cleanup-project litter goal sets a filter on the eater: it must take
    // everything in reach. Starve ends it.
    [HarmonyPatch(typeof(GarnotteEater), "EatOnly")]
    internal static class GarnotteEaterEatOnlyPatch
    {
        private static void Prefix(GarnotteEater __instance) { BtcAutoPick.SetManual(__instance, true); }
    }

    [HarmonyPatch(typeof(GarnotteEater), "EatOnlySimilarsTo")]
    internal static class GarnotteEaterEatOnlySimilarsPatch
    {
        private static void Prefix(GarnotteEater __instance) { BtcAutoPick.SetManual(__instance, true); }
    }

    [HarmonyPatch(typeof(GarnotteEater), "Starve")]
    internal static class GarnotteEaterStarvePatch
    {
        private static void Postfix(GarnotteEater __instance)
        {
            BtcAutoPick.SetManual(__instance, false);
            try { BtcAutoPick.OnStarve(__instance); }
            catch (Exception ex) { BtcLog.Error("AutoPick", ex.Message); }
        }
    }

    // mov.noAutoPick: vanilla DigGoal turns the eater on only while digging a voxel whose resource is kept
    // (!MustOverkill), so a tunnel with stone off never picks up the brimstone/iron lying next to the stone it digs.
    // With the rule on and any ore kept, the eater also runs on overkilled voxels; BtcAutoPick.Wanted filters it.
    [HarmonyPatch(typeof(DigGoal), "SetupBuilder")]
    internal static class DigGoalEatOverkillPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            CodeMatcher cm = new CodeMatcher(ins);
            cm.MatchForward(false, new CodeMatch(i => (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt)
                && i.operand is MethodInfo m && m.Name == "MustOverkill" && m.GetParameters().Length == 0));
            if (cm.IsInvalid)
            {
                BtcLog.Error("Hook", "DigGoal.SetupBuilder: MustOverkill call not found");
                return ins;
            }
            cm.Advance(1).Insert(new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BtcAutoPick), "OverkillNoEat")));
            return cm.InstructionEnumeration();
        }
    }
}
