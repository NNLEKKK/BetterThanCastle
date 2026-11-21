using System.Collections.Generic;
using System.Reflection;
using Brix.Game.Network;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Legacy replay hook at the top of every server-side [Command] body (see UNetCmdReplay in UNetProjectCmdPatches.cs).
    [HarmonyPatch]
    internal static class UNetCatapultReplayPatch
    {
        private static readonly string[] Names =
        {
            "CmdSetTarget", "CmdUnloadFromCatapult", "CmdLoadIntoCatapult", "CmdWorkOnCatapult", "CmdShootCatapult", "CmdAssignWorker", "CmdClearTarget", "CmdRequestHoldFire", "CmdRequestFireOnce", "CmdRequestFireAtWill"
        };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            return UNetCmdReplay.Targets(typeof(UNetCatapult), Names);
        }

        private static bool Prefix(UNetCatapult __instance, object[] __args, MethodBase __originalMethod)
        {
            return UNetCmdReplay.Run(__instance, __args, __originalMethod);
        }
    }
}