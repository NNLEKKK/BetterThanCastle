using UnityEngine;

namespace BetterThanCastle
{
	// list.md F2: vanilla checks the MIN range of ranged units on the flat (XZ) distance, so a target right below the shooter (a ledge, a wall
	// edge) counts as "too close". With the rule on, a target below the shooter uses the full 3D distance for the min range, so shooting
	// straight down works. BC ON only (BtcGameplay.E is the vanilla array otherwise).
	internal static class BtcShootDown
	{
		private static int _idx = -2;

		public static void AddRows()
		{
			BtcGameplay.Add("mov.shootdown", "mov", "Shoot straight down (any angle up to 90)", "Стрельба вниз под любым углом (до 90)", 0f, 0f, 1f, 1f, true, "Ranged units can shoot at targets right below them: the minimum range is measured in 3D when the target is lower than the shooter, not only on the flat distance.", "Дальнобойные могут стрелять по целям прямо под собой: минимальная дальность считается в 3D, если цель ниже стрелка, а не только по плоскому расстоянию.");
		}

		public static bool On
		{
			get
			{
				if (_idx == -2) _idx = BtcGameplay.Idx("mov.shootdown");
				float[] e = BtcGameplay.E;
				return _idx >= 0 && e != null && _idx < e.Length && e[_idx] > 0.5f;
			}
		}

		// a = weapon position - target
		public static float MinDistance(Vector3 a, float flat)
		{
			return (On && a.y > 0f) ? a.magnitude : flat;
		}
	}
}
