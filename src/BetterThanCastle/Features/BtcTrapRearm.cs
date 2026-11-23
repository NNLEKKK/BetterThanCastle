using System;
using Brix.Game;
using Brix.Game.Components;
using Brix.Legacy;
using Brix.Network;
using UnityEngine;

namespace BetterThanCastle
{
	// A8 (BC ON, per player): automatic re-arming of fired bear traps. Every player switches it for ALL own traps from a trap's pie menu
	// (builder pie on the trap, or hover with nothing selected); the start value is the Orders setting "trap_auto_rearm" (default on =
	// vanilla). Host AI: ObjectActivator.HasWorkFor -> false for traps of a player with it off (BtcPlayerPrefs.For(trap).TrapAuto).
	// The manual re-arm (builder secondary click on the trap) sends the Activate instruction directly and still works.
	// Pie buttons/menus: Patches/LivrePatches.cs (TrapButtons, PatchTrapMenus).
	public static class BtcTrapRearm
	{
		internal static bool BlockAuto(ObjectActivator act)
		{
			if (act == null || !BtcNetworkGate.Enabled || act.GetComponent<BearTrapTrigger>() == null) return false;
			return !BtcPlayerPrefs.For(act.gameObject).TrapAuto;
		}

		// ------------------------------------------------------------------ pie menu (local player)
		private static bool OwnTrap()
		{
			GameObject go = Instructions.objet as GameObject;
			if (go == null || !BtcNetworkGate.Enabled) return false;
			if (go.GetComponentInParent<BearTrapTrigger>() == null && go.GetComponentInChildren<BearTrapTrigger>() == null) return false;
			User u = User.LocalUser;
			Faction f = BtcPlayerPrefs.FactionOf(go);
			return u != null && f != null && ReferenceEquals(u.faction, f);
		}

		internal static void Install()
		{
			Instructions.conditions["Condition_Btc_Trap_Auto_On"] = () => OwnTrap() && BtcPlayerPrefs.LocalTrapAuto;
			Instructions.conditions["Condition_Btc_Trap_Auto_Off"] = () => OwnTrap() && !BtcPlayerPrefs.LocalTrapAuto;
			Instructions.commendes["Event_Btc_Trap_Auto_Toggle"] = () =>
			{
				try { BtcPlayerPrefs.LocalTrapAuto = !BtcPlayerPrefs.LocalTrapAuto; }
				catch (Exception e) { BtcLog.Error("TrapRearm", "toggle: " + e.Message); }
			};
		}
	}
}
