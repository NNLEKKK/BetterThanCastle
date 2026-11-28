using System;
using System.Reflection;
using Brix.Game.AI;
using Brix.Game.Components;
using HarmonyLib;
using Motus.Behavior;

namespace BetterThanCastle.Patches
{
    // An exception while a unit's Behavior ticks goes only to the game's own logger, and StopOnError (default) then disables the
    // component for good: the unit freezes in its last state, orders pile up in PendingTask and nothing (not even Stop) moves it
    // (terraform fill hang 2026-11-28; rare random freezes in vanilla games too). For units (BaseLabor) the error is logged into
    // our log and the unit's brain is rebuilt instead: the broken task is dropped, a fresh tree is spawned, the queued order (or
    // the unit's own work search) starts at once. Other behaviours keep vanilla. Host only (behaviours tick on the server).
    [HarmonyPatch(typeof(Behavior), "OnFixedUpdateError")]
    internal static class BehaviorErrorLogPatch
    {
        private static int _logged;
        private static readonly FieldInfo TreeF = AccessTools.Field(typeof(Behavior), "Tree");
        private static readonly FieldInfo RunF = AccessTools.Field(typeof(Behavior), "TreeRun");
        private static readonly MethodInfo CreateTreeM = AccessTools.Method(typeof(Behavior), "CreateTree");
        private static readonly MethodInfo CleanUpM = AccessTools.Method(typeof(Task), "CleanUp");

        private static bool Prefix(Behavior __instance, Exception e)
        {
            BaseLabor l = __instance as BaseLabor;
            bool log = _logged++ < 50;
            try
            {
                if (log) BtcLog.Error("Behavior", (__instance != null ? __instance.name : "null") + " error" + (l != null ? " (brain rebuilt)" : "") + ": " + e);
                if (l == null || l.IsNullOrReleased() || TreeF == null || RunF == null || CreateTreeM == null) return true;
                Recover(l);
                return false;
            }
            catch (Exception ex)
            {
                BtcLog.Error("Behavior", "rebuild failed for " + (__instance != null ? __instance.name : "null") + ": " + ex.Message);
                return true;
            }
        }

        private static void Recover(BaseLabor l)
        {
            Task cur = l.CurrentTask;
            NodeRun old = RunF.GetValue(l) as NodeRun;
            try { if (old != null) old.Halt(); }   // finalizers of the running nodes (stop work animation, release reservations)
            catch (Exception ex) { BtcLog.Warn("Behavior", l.name + " old tree halt: " + ex.Message); }
            if (l.CurrentTask == cur) l.CurrentTask = null;
            try { if (cur != null && CleanUpM != null) CleanUpM.Invoke(cur, null); }   // disconnect the dropped task's signals
            catch (Exception) { }
            Node tree = (Node)CreateTreeM.Invoke(l, null);
            TreeF.SetValue(l, tree);
            RunF.SetValue(l, tree.Spawn());
            l.ErrorHappened = false;
            if (!l.enabled) l.enabled = true;
        }
    }
}
