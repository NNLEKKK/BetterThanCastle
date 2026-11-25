using System.Collections.Generic;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using UnityEngine;

namespace BetterThanCastle
{
	// Rule "arr.accuracy" (ported from Castle Story Plus ArcherAccuracy): a bricktron arrow fired with line of sight to its target has
	// no inaccuracy, steers onto the target and ignores everything else, so it always hits. No bricktron arrow damages allies.
	internal static class BtcArcherAccuracy
	{
		private static int _idx = -2;
		// Per shot; projectiles are pooled, so the entry is replaced on every FireAt.
		public static readonly Dictionary<KinematicProjectile, Location> Targets = new Dictionary<KinematicProjectile, Location>();

		public static void AddRows()
		{
			BtcGameplay.Add("arr.accuracy", "dmg", "Archers always hit", "Лучники всегда попадают", 0f, 0f, 1f, 1f, true, "Bricktron arrows fired with a clear line of sight always hit their target and never hurt allies. Everywhere, not only in duels.", "Стрелы бриктронов с чистой линией прицела всегда попадают в цель и не ранят союзников. Везде, не только в дуэлях.");
		}

		public static bool On
		{
			get
			{
				if (_idx == -2) _idx = BtcGameplay.Idx("arr.accuracy");
				float[] e = BtcGameplay.E;
				return _idx >= 0 && e != null && _idx < e.Length && e[_idx] > 0.5f;
			}
		}

		public static bool IsBricktronArrow(KinematicProjectile projectile, GameObject attacker)
		{
			return projectile.Archetype == KinematicProjectile.AttackArchetype.Arrow && attacker != null && attacker.GetComponent<Labor>() != null;
		}

		public static bool HasLineOfSight(KinematicProjectile projectile, GameObject attacker, Vector3 eye, Location target)
		{
			if (target == null || target.Object.IsNullOrReleased()) return false;
			Vector3 delta = target.RangeHitPosition - eye;
			RaycastHit[] hits = Physics.RaycastAll(eye, delta.normalized, delta.magnitude + 0.5f, projectile.LayerCollisionMask, QueryTriggerInteraction.Ignore);
			System.Array.Sort(hits, (RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance));
			foreach (RaycastHit hit in hits)
			{
				if (hit.transform.IsChildOf(attacker.transform)) continue;
				GameObject hitObject = ProjectileAiming.GetSignificantParent(hit.collider).gameObject;
				if (hitObject == target.Object) return true;
				if (Affiliation.IsAllied(attacker, hitObject)) continue;
				return false;
			}
			// Nothing in between: the target's own collider may not be on the collision layers.
			return true;
		}

		public static bool IsHoming(KinematicProjectile projectile, out Location target)
		{
			return Targets.TryGetValue(projectile, out target) && target != null && !projectile.Target.IsNullOrReleased();
		}
	}
}
