using UnityEngine;

namespace BetterThanCastle
{
	// Gameplay > Shooting range (BC ON, host):
	//   F1 rng.groundFix (default ON): the fire-at point is never an exact .5 height. The game rounds the target with Mathf.RoundToInt
	//      (half to even), so a top face at voxelY + 0.5 rounded down on even Y and up on odd Y (+0 / +1 by parity).
	// Patches/ShootRulesPatches.cs. (D2 rng.holdFire removed 2026-11-23: did not survive "cannot aim target", see decisions.)
	public static class BtcShootRules
	{
		private static int _iFix = -2;

		public static void AddRows()
		{
			BtcGameplay.Add("rng.groundFix", "rng", "Fire at the ground: same height on every block", "Стрельба по земле: одинаковая высота на любом блоке", 1f, 0f, 1f, 1f, true, "Vanilla rounds the aim point of a shot at the ground differently on even and odd block heights (+0 or +1). This rule makes it the same everywhere.", "Ваниль по-разному округляет точку прицела выстрела по земле на чётной и нечётной высоте (+0 или +1). Это правило делает её одинаковой везде.");
		}

		private static bool Rule(ref int idx, string id)
		{
			if (!BtcNetworkGate.Enabled) return false;
			if (idx == -2) idx = BtcGameplay.Idx(id);
			float[] a = BtcGameplay.Active;
			return idx >= 0 && a != null && idx < a.Length && a[idx] > 0.5f;
		}

		internal static bool GroundFix { get { return Rule(ref _iFix, "rng.groundFix"); } }

		// UNetContextualCmd.CmdFireAt prefix (host)
		internal static void FixPoint(ref Vector3 p)
		{
			if (!GroundFix) return;
			float f = p.y - Mathf.Floor(p.y);
			if (Mathf.Abs(f - 0.5f) < 0.01f) p.y -= 0.02f;   // ties always resolve to the lower cell
		}

	}
}
