using System.Collections.Generic;
using Brix.Game.AI;
using Brix.Game.AI.Nodes;
using Brix.Game.Components;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
	// Rule trap.fix (default ON, BC ON, host): two vanilla bear trap flaws (BearTrapTrigger), Patches/TrapFixPatches.cs.
	// 1) Damage without stun: ApplyTrap puts its "BearTrap freeze" task into Labor.PendingTask, a single slot. Any order sent before the
	//    next AI tick (player click, instinct attack, project job) overwrites it: 150 damage, no stun. A non-interruptible current task
	//    makes the window longer. Fix: while the freeze task is pending, other orders wait and are sent right after it started.
	// 2) Trap stuck shut: a unit that dies inside the trap is pooled without OnTriggerExit, so _isTrapped/_trappedLabor stay set; after
	//    the re-arm the next enemy leaving the trigger (or the old prisoner after a manual re-arm) sets the look to shut while the trap is
	//    armed: nobody re-arms it (HasWorkFor = false), it looks jammed until rebuilt. Fix: re-arming clears the stale prisoner state.
	public static class BtcTrapFix
	{
		internal const string FreezeDesc = "BearTrap freeze";
		private static int _iOn = -2;

		private sealed class Deferred { internal Labor L; internal Task T; internal Labor.DecisionSource S; internal float Since; }
		private static readonly Dictionary<int, Deferred> Wait = new Dictionary<int, Deferred>();
		private static readonly List<int> Keys = new List<int>();

		private static readonly AccessTools.FieldRef<BearTrapTrigger, bool> IsTrapped = AccessTools.FieldRefAccess<BearTrapTrigger, bool>("_isTrapped");
		private static readonly AccessTools.FieldRef<BearTrapTrigger, Labor> TrappedLabor = AccessTools.FieldRefAccess<BearTrapTrigger, Labor>("_trappedLabor");
		private static readonly AccessTools.FieldRef<BearTrapTrigger, Collider> TrappedCol = AccessTools.FieldRefAccess<BearTrapTrigger, Collider>("_trappedLaborCollider");

		public static void AddRows()
		{
			BtcGameplay.Add("trap.fix", "eco", "Bear trap fixes", "Исправления капканов", 1f, 0f, 1f, 1f, true,
				"Fixes two vanilla trap bugs: a unit could take the trap damage but not get stuck (an order given at that moment cancelled the stun), and a trap could look shut forever after a unit died in it, so nobody re-armed it.",
				"Исправляет два ванильных бага капканов: юнит мог получить урон капкана, но не застрять (приказ в этот момент отменял стан), и капкан мог навсегда выглядеть закрытым после смерти юнита в нём, поэтому его никто не перезаряжал.");
		}

		internal static bool On
		{
			get
			{
				if (!BtcNetworkGate.Enabled) return false;
				if (_iOn == -2) _iOn = BtcGameplay.Idx("trap.fix");
				float[] a = BtcGameplay.Active;
				return _iOn >= 0 && a != null && _iOn < a.Length && a[_iOn] > 0.5f;
			}
		}

		private static bool IsFreeze(Task t) { return t != null && t.Description == FreezeDesc; }

		// Labor.SendOrder prefix: false = the order waits until the pending freeze task has started
		internal static bool BeforeSend(Labor l, Task task, Labor.DecisionSource source)
		{
			if (l == null || IsFreeze(task) || !IsFreeze(l.PendingTask) || !On) return true;
			Deferred d;
			int id = l.GetInstanceID();
			if (!Wait.TryGetValue(id, out d)) { d = new Deferred(); d.Since = Time.time; Wait[id] = d; }
			d.L = l; d.T = task; d.S = source;   // newest order wins, as in vanilla
			BtcLog.Info("TrapFix", "order held during trap stun: " + l.name);
			return false;
		}

		// every frame (ResourcePackManager Update); nothing to do almost always
		internal static void Tick()
		{
			if (Wait.Count == 0) return;
			Keys.Clear();
			foreach (KeyValuePair<int, Deferred> kv in Wait) Keys.Add(kv.Key);
			for (int i = 0; i < Keys.Count; i++)
			{
				Deferred d = Wait[Keys[i]];
				if (d.L.IsNullOrReleased()) { Wait.Remove(Keys[i]); continue; }
				if (IsFreeze(d.L.PendingTask) && Time.time - d.Since < 10f) continue;
				Wait.Remove(Keys[i]);
				d.L.SendOrder(d.T, d.S);
			}
		}

		// ObjectActivator.Active setter postfix: a re-armed trap forgets its last prisoner
		internal static void OnActive(ObjectActivator act, bool value)
		{
			if (!value || !On) return;
			BearTrapTrigger t = act.GetComponent<BearTrapTrigger>();
			if (t == null) return;
			if (IsTrapped(t) || TrappedLabor(t) != null) BtcLog.Info("TrapFix", "re-arm cleared stale prisoner at " + t.transform.position);
			IsTrapped(t) = false;
			TrappedLabor(t) = null;
			TrappedCol(t) = null;
		}
	}
}
