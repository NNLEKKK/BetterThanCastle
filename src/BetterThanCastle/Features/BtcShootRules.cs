using UnityEngine;

namespace BetterThanCastle
{
	// Gameplay > Shooting range (BC ON, host):
	//   F1 rng.groundFix (default ON): the fire-at point is never an exact .5 height. The game rounds the target with Mathf.RoundToInt
	//      (half to even), so a top face at voxelY + 0.5 rounded down on even Y and up on odd Y (+0 / +1 by parity).
	//   rng.hitKeep (default ON, vanilla bug fix): a manual "fire at a block / point" order is not cancelled by a hit. Vanilla
	//      Profession.ShootAt sets a "damaged" flag on any damage and its per-frame When(damaged) branch drops the shot ("cannot aim").
	//      Stuns, smashes and death still stop it (they leave the archery stance). Fire at an object (unit, building) stays vanilla.
	// Patches/ShootRulesPatches.cs. (D2 rng.holdFire removed 2026-11-23: it skipped the flinch only, the real cause is the flag.)
	public static class BtcShootRules
	{
		private static int _iFix = -2, _iKeep = -2;
		private static int _logged;

		public static void AddRows()
		{
			BtcGameplay.Add("rng.hitKeep", "rng", "Manual fire at a block: hits do not cancel it", "Ручная стрельба по блоку: урон её не сбивает", 1f, 0f, 1f, 1f, true, "Vanilla bug: any hit made a ranged unit drop a manual 'fire at this block' order without shooting. With this rule it keeps shooting; stuns, smashes and death still stop it.", "Ванильный баг: любой урон заставлял стрелка бросить ручной приказ «стрелять по блоку» без выстрела. С этим правилом он продолжает стрелять; станы, смэши и смерть по-прежнему останавливают.");
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
		internal static bool HitKeep { get { return Rule(ref _iKeep, "rng.hitKeep"); } }

		// Profession.ShootAt "damaged = true" lambda prefix: true = ignore this hit for the shot at a block / point
		internal static bool KeepOnHit(Brix.Game.AI.Location target)
		{
			if (target == null || target is Brix.Game.AI.ObjectLocation || !HitKeep) return false;
			if (_logged < 3) { _logged++; BtcLog.Info("ShootKeep", "hit ignored, fire at " + target.Position); }
			return true;
		}

		// UNetContextualCmd.CmdFireAt prefix (host)
		internal static void FixPoint(ref Vector3 p)
		{
			if (!GroundFix) return;
			float f = p.y - Mathf.Floor(p.y);
			if (Mathf.Abs(f - 0.5f) < 0.01f) p.y -= 0.02f;   // ties always resolve to the lower cell
		}

	}
}
