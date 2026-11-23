using System;
using System.Collections.Generic;
using Brix.Game.AI;
using Brix.Game.Components;
using HarmonyLib;
using Motus.Behavior;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    // Rule mov.legsFix (host), after Castle Story Plus (Tricky12321, GPLv3: WorkerAI/LocomotionRecovery.cs + RecoveryRules, bug #156).
    // A bricktron's legs (Locomotion4) can stay in an airborne/stun state while the animation stands in place (a missed stand-up
    // entry after a blow): it looks idle, cannot move, and every task waits for good in Labor.GetReadyForNextTask.
    // Fix, players' units on the host only: (1) once a second legs out of step with the animation for 2 checks in a row are told the
    // animation stands; (2) the task's wait for the legs is time limited (2 s for a player's order, 8 s other), after which the
    // pose flags are cleared and the animator put in its standing state. Not for dead/held/phased out/climbing/falling units.
    // The wait replaces the vanilla node but keeps the BTC OnRunOrder hook (SeqHook.OnRun) in front, as LaborPatches does.
    internal sealed class BtcLegsRecovery : MonoBehaviour
    {
        private static int _idx = -2;
        private static GameObject _go;
        private const int OutOfStepChecks = 2;
        private const float CheckSeconds = 1f;
        private const float FallingSpeed = 0.5f;

        private static readonly Dictionary<Labor, int> OutOfStep = new Dictionary<Labor, int>();
        private static readonly List<Labor> Gone = new List<Labor>();
        private float _next;

        public static void AddRows()
        {
            BtcGameplay.Add("mov.legsFix", "mov", "Legs recovery", "Починка ног юнитов", 1f, 0f, 1f, 1f, true,
                "A worker whose legs got stuck in a stun or air state while it stands still (it ignores every order and task) is put back on its feet after 2 s (8 s for its own tasks). Host only, your units only.",
                "Если у рабочего ноги застряли в состоянии оглушения или полёта, пока он просто стоит (не выполняет ни приказы, ни задачи), через 2 с (8 с для его собственных задач) он ставится обратно на ноги. Только у хоста, только ваши юниты.");
        }

        public static bool On
        {
            get
            {
                if (_idx == -2) _idx = BtcGameplay.Idx("mov.legsFix");
                float[] e = BtcGameplay.E;
                return _idx >= 0 && e != null && _idx < e.Length && e[_idx] > 0.5f;
            }
        }

        internal static void EnsureAttached()
        {
            if (_go != null) return;
            _go = new GameObject("BtcLegsRecovery");
            UnityEngine.Object.DontDestroyOnLoad(_go);
            _go.AddComponent<BtcLegsRecovery>();
        }

        private static Locomotion4 LegsOf(Labor l)
        {
            return l == null ? null : l.GetComponent<Locomotion4>();
        }

        internal static bool Watched(Labor l)
        {
            return NetworkServer.active && l != null && l.faction != null && !l.faction.isAI;
        }

        private sealed class Wait
        {
            private float _since;
            private bool _player;

            public void Begin(Labor l)
            {
                _since = Time.time;
                _player = Patches.PlayerOrdersFirst.IsPlayerTask(l.CurrentTask);
                Locomotion4 legs = LegsOf(l);
                if (legs != null) legs.GetReadyToMove();
            }

            public bool Ready(Labor l)
            {
                Locomotion4 legs = LegsOf(l);
                if (legs == null || legs.CanMove()) return true;
                float waited = Time.time - _since;
                if (waited < (_player ? 2f : 8f)) return false;
                _since = Time.time;
                try { return ResetLegs(l, "task waited " + Mathf.RoundToInt(waited) + " s for its legs") && legs.CanMove(); }
                catch (Exception e) { BtcLog.Error("LegsFix", l.name + ": " + e.Message); return false; }
            }
        }

        internal static Node ReadyForNextTask(Labor labor)
        {
            Wait wait = new Wait();
            return Node.Sequence
                .Do(Patches.SeqHook.OnRun, labor)
                .Do((Labor self) => wait.Begin(self), labor)
                .Do(Node.WaitForCondition<Labor>((Labor self) => wait.Ready(self), labor)).ToNode;
        }

        private static bool MayReset(Labor l, Locomotion4 legs)
        {
            Locomotion4.State s = legs.state;
            bool climbing = legs.IsUsingLadder || s == Locomotion4.State.Ladder || s == Locomotion4.State.ClimbUp || s == Locomotion4.State.StepDown
                || s == Locomotion4.State.JumpingOff || s == Locomotion4.State.JumpingOffJumping || s == Locomotion4.State.JumpingOffFalling || s == Locomotion4.State.JumpingOffLanding;
            bool flailing = false;
            float speed = 0f;
            Rigidbody body = legs.FlailingColliderRigidbody;
            if (legs.IsAirborne && body != null && !body.isKinematic) { flailing = true; speed = body.velocity.magnitude; }
            if (legs.IsDead || legs.IsGrabbed || l.IsPhasedOut || climbing) return false;
            return !flailing || speed < FallingSpeed;
        }

        internal static bool ResetLegs(Labor l, string why)
        {
            if (!NetworkServer.active || l == null) return false;
            Locomotion4 legs = LegsOf(l);
            if (legs == null || legs.CanMove() || !MayReset(l, legs)) return false;
            GridLocomotionControllerInterface c = legs.controller;
            if (c == null || c.InPlace == null) return false;
            Locomotion4.State before = legs.state;
            BoolParameter[] poses =
            {
                c.IsFlailing, c.IsStabilizing, c.IsStunned, c.IsHardHurt, c.IsSoftHurt, c.IsTakingCombatStance, c.IsAttacking,
                c.IsTakingArcheryStance, c.IsAiming, c.IsShooting, c.IsShieldBlocking, c.IsCombatStepping, c.IsRunStriking, c.IsSitting,
                c.IsTaking, c.IsUnburrowing, c.IsGettingTrapped, c.IsLeavingTrap, c.IsBeingGrabbed, c.IsJumpingOff
            };
            for (int i = 0; i < poses.Length; i++) if (poses[i] != null) poses[i].Set(false);
            legs.Common.Trapped.TimeToLeaveTrap = 0f;
            AnimatorStateInfo info = default(AnimatorStateInfo);
            if (legs.animator != null)
            {
                legs.animator.Play(c.InPlace.StateHash, 0, 0f);
                info = legs.animator.GetCurrentAnimatorStateInfo(0);
            }
            c.InPlace.EnterState(info);
            if (!legs.CanMove()) c.InPlace.EnterState(info);
            OutOfStep.Remove(l);
            BtcLog.Info("LegsFix", l.name + " (" + why + ", legs " + before + (legs.IsAirborne ? ", airborne" : "") + "): put back on its feet" + (legs.CanMove() ? "" : ", still cannot move"));
            return true;
        }

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + CheckSeconds;
            if (!NetworkServer.active || !On) return;
            // The observer sets are split by the local (host) player: mine, allied, enemy. Clients' units are in the last two.
            CheckAll(UIGameObserver.bricktrons);
            CheckAll(UIGameObserver.alliedBricktrons);
            CheckAll(UIGameObserver.enemyBricktrons);
            Prune();
        }

        private static void CheckAll(UIGameObserver.Bricktron set)
        {
            if (set == null) return;
            foreach (Labor l in set.CheckCreation())
            {
                try { Check(l); }
                catch (Exception e) { BtcLog.Error("LegsFix", e.Message); OutOfStep.Remove(l); }
            }
        }

        private static void Check(Labor l)
        {
            if (!Watched(l) || l.IsPhasedOut) { OutOfStep.Remove(l); return; }
            Locomotion4 legs = LegsOf(l);
            GridLocomotionControllerInterface c = legs == null ? null : legs.controller;
            if (c == null || c.InPlace == null || legs.animator == null || legs.IsDead || legs.IsGrabbed) { OutOfStep.Remove(l); return; }
            bool outOfStep = !legs.CanMove() && c.InPlace.IsActive() && !legs.animator.IsInTransition(0);
            if (!outOfStep) { OutOfStep.Remove(l); return; }
            int checks;
            OutOfStep.TryGetValue(l, out checks);
            checks++;
            if (checks < OutOfStepChecks) { OutOfStep[l] = checks; return; }
            OutOfStep.Remove(l);
            Locomotion4.State before = legs.state;
            c.InPlace.EnterState(legs.animator.GetCurrentAnimatorStateInfo(0));
            BtcLog.Info("LegsFix", l.name + "'s legs waited (" + before + ") while it stood in place: back in step" + (legs.CanMove() ? "" : ", still cannot move"));
        }

        private static void Prune()
        {
            if (OutOfStep.Count == 0) return;
            Gone.Clear();
            foreach (Labor l in OutOfStep.Keys) if (l == null || !l.gameObject.activeInHierarchy) Gone.Add(l);
            for (int i = 0; i < Gone.Count; i++) OutOfStep.Remove(Gone[i]);
            Gone.Clear();
        }
    }

    [HarmonyPatch(typeof(Labor), "GetReadyForNextTask")]
    internal static class BtcLegsRecoveryReadyPatch
    {
        private static void Postfix(Labor __instance, ref Node __result)
        {
            try
            {
                if (!BtcLegsRecovery.On || !BtcLegsRecovery.Watched(__instance)) return;
                BtcLegsRecovery.EnsureAttached();
                __result = BtcLegsRecovery.ReadyForNextTask(__instance);
            }
            catch (Exception e) { BtcLog.Error("LegsFix", "GetReadyForNextTask: " + e.Message); }
        }
    }
}
