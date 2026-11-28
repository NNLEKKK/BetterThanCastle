using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.AI.Nodes;
using HarmonyLib;
using Motus.Behavior;
using UnityEngine;

namespace BetterThanCastle
{
    // Rule "instant orders" (host): a busy unit takes a new order at once. Vanilla ends every task with "Delay on Finished"
    // (Task.DelayOnFinished = 10 ticks = 0.2 s, also after a cancel), so a redirect waited 0.2 s before the new task even started
    // (lab orderlag: do -> cleanup 10 ticks, then 3 ticks to start + 4 ticks path search = ~0.35 s). With the rule the delay ends
    // as soon as a new task is pending; a task that just finishes on its own keeps the vanilla pause.
    // Strike skips stay vanilla (user report 2026-11-23): players cancel a melee recovery with a quick attack-move / focus
    // and their timing counts on the vanilla pause. A combat order (attack-move, focus, shoot) keeps the vanilla pause and
    // never breaks off a stroke; a plain move stays instant. A plain move within one strike cycle (1.5 s) after a melee strike
    // remembers when it would have started in vanilla, and a combat order sent before that moment waits the missing time,
    // so "strike -> move -> attack" is never faster than in vanilla.
    internal static class BtcInstantOrders
    {
        private sealed class Unit { public float Strike = -99f; public float VanillaEnd = -99f; }
        private sealed class Wait { public Task T; public float Base; public float Start = -1f; }

        private static int _idx = -2;
        private static readonly Func<Wait, bool> DoneFn = Done;
        private const float StrikeWindow = 1.5f;
        private static readonly Dictionary<int, Unit> Units = new Dictionary<int, Unit>();

        public static void AddRows()
        {
            BtcGameplay.Add("mov.instant", "mov", "Instant orders", "Мгновенные приказы", 1f, 0f, 1f, 1f, true, "A busy unit starts a new order at once instead of finishing a 0.2 s pause of its old task first (redirecting a running unit took ~0.35 s in vanilla). Plain moves stay instant; attack-move and focus keep vanilla timing, so strike skips work as in vanilla. A new order also breaks a chain of 1-block jumps on a stair of full blocks: the unit turns after the current jump. A run order also resets every action animation (shooting, fighting, working, taking...) so the unit runs at once; falling, ladders, traps and stun are not touched.", "Занятый юнит начинает новый приказ сразу, а не досиживает 0,2 с паузы старой задачи (перенаправление бегущего юнита в ванили занимало ~0,35 с). Обычное движение остаётся мгновенным, а мув-атака и фокус идут с ванильной задержкой, поэтому скипы ударов работают как в ванили. Новый приказ также прерывает цепочку прыжков по лестнице из полных блоков: юнит разворачивается после текущего прыжка. Приказ бега также сбрасывает любую анимацию действия (стрельба, бой, работа, подбор...), и юнит сразу бежит; падение, лестницы, ловушки и стан не трогаются.");
        }

        public static bool On
        {
            get
            {
                if (_idx == -2) _idx = BtcGameplay.Idx("mov.instant");
                float[] e = BtcGameplay.E;
                return _idx >= 0 && e != null && _idx < e.Length && e[_idx] > 0.5f;
            }
        }

        /// <summary>A plain move the player ordered (not an attack-move).</summary>
        internal static bool UserMove(Task t)
        {
            if (t == null || t.InitialPackage == null || t.InitialPackage.DecisionSource != Labor.DecisionSource.User) return false;
            string d = t.Description;
            return d == "RunToward" || d == "WalkToward";
        }

        /// <summary>Attack-move, focus, hold-and-attack or shoot order.</summary>
        internal static bool Combat(Task t)
        {
            if (t == null) return false;
            string d = t.Description;
            return d != null && (d.IndexOf("Attack", StringComparison.Ordinal) >= 0 || d == "ShootAt");
        }

        private static Unit Get(BaseLabor l, bool make)
        {
            Unit u;
            int id = l.GetInstanceID();
            if (Units.TryGetValue(id, out u) || !make) return u;
            if (Units.Count > 1024) Prune();
            u = new Unit();
            Units[id] = u;
            return u;
        }

        private static void Prune()
        {
            float now = Time.time;
            List<int> old = new List<int>();
            foreach (KeyValuePair<int, Unit> kv in Units)
                if (now - kv.Value.Strike >= StrikeWindow && now >= kv.Value.VanillaEnd) old.Add(kv.Key);
            for (int i = 0; i < old.Count; i++) Units.Remove(old[i]);
        }

        internal static void NoteStrike(Labor l)
        {
            if (l == null || !On) return;
            Get(l, true).Strike = Time.time;
        }

        private static bool StruckRecently(BaseLabor l, out Unit u)
        {
            u = l != null ? Get(l, false) : null;
            return u != null && Time.time - u.Strike < StrikeWindow;
        }

        /// <summary>A plain move breaks off the unit's own stroke: a combat order inside the strike cycle waits for the cycle end, as the stroke would have.</summary>
        internal static void OnStrokeAbort(BaseLabor l)
        {
            Unit u;
            if (StruckRecently(l, out u)) u.VanillaEnd = Mathf.Max(u.VanillaEnd, u.Strike + StrikeWindow);
        }

        private static bool Done(Wait w)
        {
            float now = Time.time;
            if (w.Start < 0f) w.Start = now;
            float el = now - w.Start;
            BaseLabor lab = w.T.Worker;
            Task next = lab != null ? lab.PendingTask : null;
            if (next == null || !On) return el >= w.Base;
            Unit u;
            bool struck = StruckRecently(lab, out u);
            if (!Combat(next))
            {
                if (struck) u.VanillaEnd = Mathf.Max(u.VanillaEnd, now + Mathf.Max(0f, w.Base - el));
                return true;
            }
            float owed = u != null ? Mathf.Max(0f, u.VanillaEnd - w.Start) : 0f;
            if (el < w.Base + owed) return false;
            if (u != null) u.VanillaEnd = -99f;
            return true;
        }

        /// <summary>Replaces Node.WaitForTicks(DelayOnFinished) in Task.CoreNode: same wait, cut short by a pending plain order (see Done).</summary>
        internal static StaticNode FinishWait(int ticks, Task task)
        {
            if (ticks <= 0) return Node.WaitForTicks(ticks);
            Wait w = new Wait { T = task, Base = ticks * Time.fixedDeltaTime };
            return UtilityNodes.WaitForTimeOrCondition(DoneFn, w, 2f * w.Base + StrikeWindow, false);
        }
    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(Task), "CoreNode")]
    internal static class TaskCoreNodePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            MethodInfo wait = AccessTools.Method(typeof(Node), "WaitForTicks", new Type[] { typeof(int) });
            MethodInfo mine = AccessTools.Method(typeof(BtcInstantOrders), "FinishWait");
            FieldInfo delay = AccessTools.Field(typeof(Task), "DelayOnFinished");
            for (int i = 1; i < list.Count; i++)
            {
                if (!(list[i].operand is MethodInfo) || (MethodInfo)list[i].operand != wait) continue;
                if (!(list[i - 1].operand is FieldInfo) || (FieldInfo)list[i - 1].operand != delay) continue;
                list[i].operand = mine;
                list.Insert(i, new CodeInstruction(OpCodes.Ldarg_0));
                return list;
            }
            BtcLog.Error("Hook", "Task.CoreNode: WaitForTicks(DelayOnFinished) not found, instant orders off");
            return list;
        }
    }
}

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// Rule mov.instant: a blocked walk (an ally or a new obstacle in the way) retried its jump/make-way after 50 ticks (1 s) each
    /// time, up to 3 times, so a unit stood "thinking" for seconds; the retry now waits at most 10 ticks (0.2 s).
    /// </summary>
    [HarmonyPatch(typeof(NavigationNodes.Tries), "CountFailureAndWait")]
    internal static class TriesCountFailureAndWaitPatch
    {
        private static void Prefix(ref int ticks)
        {
            if (ticks > 10 && BtcInstantOrders.On) ticks = 10;
        }
    }
}

namespace BetterThanCastle.Patches
{
    // Rule mov.instant, "player orders first" after Castle Story Plus (Tricky12321, GPLv3: PlayerOrdersFirst). A unit holds one
    // pending task and every order replaced it: when a move cancelled an attack the unit was "idle" for a tick, HasToFight saw the
    // enemy and the instinct sent HoldPositionAndAttackWithPriority, which replaced the move (part of a retreating squad stayed
    // shooting, lab grouplag); in a fight the instinct also cut a running player order off. While a player's order waits or runs:
    // the instinct does not send the unit to fight, the unit's own or its project's orders do not replace it, and an own task
    // that may not be interrupted (a stroke, a shot) is aborted so the order starts at once. AI factions keep vanilla.
    internal static class PlayerOrdersFirst
    {
        internal static bool Applies(Labor l) { return l != null && l.faction != null && !l.faction.isAI && BtcInstantOrders.On; }

        internal static bool Has(Labor l) { return IsPlayerTask(l.CurrentTask) || IsPlayerTask(l.PendingTask); }

        internal static bool IsPlayerTask(Task t)
        {
            return t != null && t.InitialPackage != null && t.InitialPackage.DecisionSource == Labor.DecisionSource.User
                && (t.Status == TaskProgression.NotStarted || t.Status == TaskProgression.Running);
        }
    }

    [HarmonyPatch(typeof(Labor), "HasToFight")]
    internal static class PlayerOrdersFirstFightPatch
    {
        private static bool Prefix(Labor __instance, ref bool __result)
        {
            try
            {
                if (!PlayerOrdersFirst.Applies(__instance) || !PlayerOrdersFirst.Has(__instance)) return true;
                __result = false;
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Labor.HasToFight: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(Labor), "SendOrder")]
    internal static class PlayerOrdersFirstSendPatch
    {
        private static bool Prefix(Labor __instance, Labor.DecisionSource source)
        {
            try
            {
                if ((source != Labor.DecisionSource.Character && source != Labor.DecisionSource.Project) || !PlayerOrdersFirst.Applies(__instance)) return true;
                return !PlayerOrdersFirst.Has(__instance);
            }
            catch (Exception e) { BtcLog.Error("Hook", "Labor.SendOrder first: " + e.Message); return true; }
        }

        private static void Postfix(Labor __instance, Labor.DecisionSource source)
        {
            try
            {
                if (source != Labor.DecisionSource.User || __instance.faction == null || __instance.faction.isAI) return;
                BtcClimbBreak.NoteOrder(__instance);
                if (!BtcInstantOrders.On) return;
                Task cur = __instance.CurrentTask;
                if (cur != null && cur != __instance.PendingTask && !cur.CanInterrupt && cur.Status == TaskProgression.Running && !PlayerOrdersFirst.IsPlayerTask(cur)
                    && !BtcInstantOrders.Combat(__instance.PendingTask))
                {
                    BtcInstantOrders.OnStrokeAbort(__instance);
                    __instance.AbortWorkSignal.Invoke(TaskResult.ReceivedNewInstruction);
                }
            }
            catch (Exception e) { BtcLog.Error("Hook", "Labor.SendOrder abort: " + e.Message); }
        }
    }

    /// <summary>Rule mov.instant: remembers when a unit starts a melee strike, so orders around a strike keep vanilla skip timing.</summary>
    [HarmonyPatch(typeof(AttackInfo), "Strike")]
    internal static class InstantOrdersStrikePatch
    {
        private static void Postfix(AttackInfo __instance, bool __runOriginal)
        {
            try { if (__runOriginal) BtcInstantOrders.NoteStrike(__instance.Attacker); }
            catch (Exception e) { BtcLog.Error("Hook", "AttackInfo.Strike instant: " + e.Message); }
        }
    }
}
