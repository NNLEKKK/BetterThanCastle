using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Game.Network;
using HarmonyLib;
using UnityEngine.Networking;

namespace BetterThanCastle.Patches
{
    /// <summary>Holder links whose holder is not spawned yet are kept and applied later (BtcTrackFix).</summary>
    [HarmonyPatch(typeof(GenericParentTracker), "OnDeserialize")]
    internal static class GenericParentTrackerOnDeserializePatch
    {
        private static void Prefix(GenericParentTracker __instance) { BtcTrackFix.Current = __instance; }

        private static void Finalizer() { BtcTrackFix.Current = null; }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            MethodInfo mine = AccessTools.Method(typeof(BtcTrackFix), "Read");
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                MethodInfo m = list[i].operand as MethodInfo;
                if (m == null || m.Name != "_ReadTrackingPair_None") continue;
                list[i].opcode = System.Reflection.Emit.OpCodes.Call;
                list[i].operand = mine;
                n++;
            }
            if (n != 2) BtcLog.Error("Hook", "GenericParentTracker.OnDeserialize: expected 2 TrackingPair reads, patched " + n);
            return list;
        }
    }
}
