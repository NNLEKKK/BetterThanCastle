using System.Collections.Generic;
using System.Reflection;
using Brix.Game.Network;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Legacy replay hook at the top of every server-side [Command] body (see UNetCmdReplay in UNetProjectCmdPatches.cs).
    // CmdSpawnGoupe: only the 5-arg [Command] overload is hooked (the 3-arg one is a client helper calling CallCmdSpawnGoupe).
    [HarmonyPatch]
    internal static class UNetGroupeReplayPatch
    {
        private static readonly string[] Names =
        {
            "CmdSpawnGoupe", "CmdSpawnHarvestProject", "CmdSpawnMineProject", "CmdSpawnLandscapeProject", "CmdSpawnRaidGoupeOccupy", "CmdCreate", "CmdCommit", "CmdCancelCommit", "CmdSetMine", "CmdSetLandscape", "CmdDelete", "CmdAddProjectCapacity", "CmdRemoveProjectCapacity", "CmdRaisePriority", "CmdLowerPriority"
        };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            return UNetCmdReplay.Targets(typeof(UNetGroupe), Names);
        }

        private static bool Prefix(UNetGroupe __instance, object[] __args, MethodBase __originalMethod)
        {
            return UNetCmdReplay.Run(__instance, __args, __originalMethod);
        }
    }
}