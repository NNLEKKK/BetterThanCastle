using System;
using Brix.Legacy;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Client-side body of the RPC (local physics fix of the fallen log); no networking is added.
    // Legacy called LogFix inside "if (Log) { ... }" right after setting the velocity.
    [HarmonyPatch(typeof(DamagedTree), "RpcFallingLogsPostprocess")]
    internal static class DamagedTreeRpcFallingLogsPostprocessPatch
    {
        private static void Postfix(GameObject Log)
        {
            try { if (Log) BtcSimplePhys.LogFix(Log); }
            catch (Exception e) { BtcLog.Error("Hook", "DamagedTree.RpcFallingLogsPostprocess: " + e); }
        }
    }
}
