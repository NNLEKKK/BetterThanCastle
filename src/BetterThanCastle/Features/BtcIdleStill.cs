using System;
using HarmonyLib;

namespace BetterThanCastle
{
    // Rule mov.idleStill (Gameplay > Movement and worker AI, BC ON, host AI, default ON): idle workers do not wander.
    // Vanilla IdleProject.DecideWhatToDo picks WalkInPoint (a walk to a random spot round the idle point) 70% of the time and
    // Sit 30%. A crowd of idle workers round the crystal then keeps walking into each other and re-searching detours every
    // update: the regular frame hitches with ~50 workers (lab idle-units). With the rule a walk becomes the vanilla Sit
    // (the idle squat in place); storing carried items and gathering from far away stay vanilla.
    internal static class BtcIdleStill
    {
        private static int _idx = -2;

        public static void AddRows()
        {
            BtcGameplay.Add("mov.idleStill", "mov", "Idle workers stay in place", "Бездельники стоят на месте", 1f, 0f, 1f, 1f, true,
                "Workers without work sit in place (the idle squat) instead of walking round the idle point; fewer frame hitches with many workers. Host setting.",
                "Рабочие без дела сидят на месте (поза безделия), а не бродят вокруг точки отдыха; меньше подфризов при большом числе рабочих. Настройка хоста.");
        }

        internal static bool On
        {
            get
            {
                if (!BtcNetworkGate.Enabled) return false;
                if (_idx == -2) _idx = BtcGameplay.Idx("mov.idleStill");
                float[] a = BtcGameplay.Active;
                return _idx >= 0 && a != null && _idx < a.Length && a[_idx] > 0.5f;
            }
        }
    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(IdleProject), "DecideWhatToDo")]
    internal static class IdleStillDecidePatch
    {
        private static void Postfix(ref IdleProject.IdleActions __result)
        {
            try { if (__result == IdleProject.IdleActions.WalkInPoint && BtcIdleStill.On) __result = IdleProject.IdleActions.Sit; }
            catch (Exception e) { BtcLog.Error("Hook", "IdleStill: " + e.Message); }
        }
    }
}
