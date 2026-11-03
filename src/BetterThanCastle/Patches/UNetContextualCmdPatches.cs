using System.Collections.Generic;
using System.Reflection;
using Brix.Game.Network;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Legacy replay hook at the top of every server-side [Command] body (see UNetCmdReplay in UNetProjectCmdPatches.cs).
    [HarmonyPatch]
    internal static class UNetContextualCmdReplayPatch
    {
        private static readonly string[] Names =
        {
            "CmdBatchedContextualActionVector", "CmdBatchedContextualActionGameObject", "CmdRemovePlant", "CmdLockDoor", "CmdJumpOff", "CmdGoAndJumpTo", "CmdFireAtObject", "CmdFireAt", "CmdTurnTo", "CmdStop", "CmdStartBarrelFuse", "CmdEquipTool", "CmdUnequipIntoRack", "CmdUnequipAllTools", "CmdEquipToolRackContent"
        };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            return UNetCmdReplay.Targets(typeof(UNetContextualCmd), Names);
        }

        private static bool Prefix(UNetContextualCmd __instance, object[] __args, MethodBase __originalMethod)
        {
            return UNetCmdReplay.Run(__instance, __args, __originalMethod);
        }
    }
}