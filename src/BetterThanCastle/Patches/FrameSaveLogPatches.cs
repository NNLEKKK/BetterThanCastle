using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Game.AI;
using Brix.Utils.Locomotion;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
    /// The units' locomotion writes a log line whenever a floor snap is refused (SnapToFloor would teleport, floor occupied,
    /// several voxels at once, soft halt twice...), which happens many times a second in a base, and formats both positions
    /// with Vector3.ToPreciseString (three floats to text, a string concat) for every line, also when the logger drops it:
    /// ~60 KB/s of garbage in a 60-worker base (BTClogs/alloc.txt). The positions are no longer formatted (the message keeps
    /// its unit name and text). Found with Dev/BtcDevAlloc.cs; part of Performance/FrameSavers.
    [HarmonyPatch]
    internal static class FrameSaveLocoLogText
    {
        private static readonly MethodInfo Precise = AccessTools.Method(typeof(Vector3LocoExtension), "ToPreciseString", new[] { typeof(Vector3) });
        private static readonly MethodInfo Cheap = AccessTools.Method(typeof(FrameSaveLocoLogText), "NoText");

        static bool Prepare() { return BtcFrameSave.On && Precise != null; }

        static IEnumerable<MethodBase> TargetMethods()
        {
            string[] loco = { "SnapToFloor", "CheckSnapToFloor", "PopOutOfGround", "SetPosition" };
            for (int i = 0; i < loco.Length; i++)
            {
                MethodInfo m = loco[i] == "SetPosition" ? AccessTools.Method(typeof(Locomotion4), "SetPosition", new[] { typeof(Vector3), typeof(bool) }) : AccessTools.Method(typeof(Locomotion4), loco[i]);
                if (m != null) yield return m;
            }
            string[] halt = { "DoSoftHalt", "ValidateDestination" };
            Type h = AccessTools.TypeByName("Brix.Game.Locomotion.Halting");
            for (int i = 0; h != null && i < halt.Length; i++)
            {
                MethodInfo m = AccessTools.Method(h, halt[i]);
                if (m != null) yield return m;
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(Precise)) { list[i].operand = Cheap; n++; }
            }
            if (n == 0) BtcLog.Error("Hook", "FrameSaveLocoLogText: no ToPreciseString in " + __originalMethod.Name);
            return list;
        }

        private static string NoText(Vector3 v) { return string.Empty; }
    }

    /// Voxel.GetSurfaceName (Enum.GetName: a boxed value and a search of the names) is asked by every walking unit when it
    /// steps onto another surface (footstep sound switch). The names are cached per value; same strings as the game's.
    [HarmonyPatch(typeof(Brix.Engine.Voxel), "GetSurfaceName", new[] { typeof(Brix.Engine.VoxelSurface) })]
    internal static class FrameSaveSurfaceName
    {
        private static readonly Dictionary<int, string> Names = new Dictionary<int, string>();

        static bool Prepare() { return BtcFrameSave.On; }

        static bool Prefix(Brix.Engine.VoxelSurface surface, ref string __result)
        {
            int k = (int)surface;
            if (Names.TryGetValue(k, out __result)) return false;
            try { __result = Enum.GetName(typeof(Brix.Engine.VoxelSurface), surface); Names[k] = __result; return false; }
            catch (Exception e) { BtcLog.Error("FrameSave", "surface name: " + e.Message); return true; }
        }

        internal static string Of(Brix.Engine.VoxelSurface surface)
        {
            string s = null; Prefix(surface, ref s); return s;
        }
    }

    /// The overload by position (the one Locomotion4.SetSurfaceSwitch calls): same lookup through the cache above.
    [HarmonyPatch(typeof(Brix.Engine.Voxel), "GetSurfaceName", new[] { typeof(Brix.Engine.XYZ) })]
    internal static class FrameSaveSurfaceNameAt
    {
        static bool Prepare() { return BtcFrameSave.On; }

        static bool Prefix(Brix.Engine.XYZ position, ref string __result)
        {
            try { __result = FrameSaveSurfaceName.Of(Brix.Engine.Voxel.GetSurfaceType(position)); return __result == null; }
            catch (Exception e) { BtcLog.Error("FrameSave", "surface name at: " + e.Message); return true; }
        }
    }
}
