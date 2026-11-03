using System;
using HarmonyLib;

namespace BetterThanCastle
{
    /// <summary>
    /// Applies every [HarmonyPatch] class separately so one failing hook cannot take the others down.
    /// Logs "[Patch] OK|FAIL <class>" per class: grep "[Patch] FAIL" after a test run.
    /// </summary>
    internal static class BtcPatcher
    {
        internal static int Ok;
        internal static int Failed;

        internal static void Apply(Harmony harmony, System.Reflection.Assembly asm)
        {
            foreach (Type t in asm.GetTypes())
            {
                if (!t.IsDefined(typeof(HarmonyPatch), true)) continue;
                try
                {
                    harmony.CreateClassProcessor(t).Patch();
                    Ok++;
                    BtcLog.Info("Patch", "OK " + t.Name);
                }
                catch (Exception e)
                {
                    Failed++;
                    BtcLog.Error("Patch", "FAIL " + t.Name + ": " + e.Message);
                }
            }
            BtcLog.Info("Patch", "summary ok=" + Ok + " fail=" + Failed);
        }
    }
}
