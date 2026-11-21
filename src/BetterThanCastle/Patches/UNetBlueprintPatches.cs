using System.Collections.Generic;
using System.Reflection;
using Brix.Game.Network;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Legacy replay hook at the top of every server-side [Command] body (see UNetCmdReplay in UNetProjectCmdPatches.cs).
    [HarmonyPatch]
    internal static class UNetBlueprintReplayPatch
    {
        private static readonly string[] Names =
        {
            "CmdPlaceBlueprint", "CmdSpawnPlacedBlueprint", "CmdSpawnPlacedRopeBridge", "CmdDestroyBluePrint", "CmdPlaceAnti", "CmdSendBlueprintsToNewProject"
        };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            return UNetCmdReplay.Targets(typeof(UNetBlueprint), Names);
        }

        private static bool Prefix(UNetBlueprint __instance, object[] __args, MethodBase __originalMethod)
        {
            return UNetCmdReplay.Run(__instance, __args, __originalMethod);
        }
    }
}