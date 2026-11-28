using Brix.Engine;
using Brix.Utils;
using UnityEngine;

namespace BetterThanCastle
{
	// Gameplay > Shooting range, rule rng.noWallShot (default ON, vanilla bug fix, host): no shots through solid ground.
	// Vanilla tests line of fire and arrow flight only with physics rays against the terrain mesh colliders. At some exact
	// angles (target flush against a cliff, shooter behind the hill) a ray slips through a seam of the surface mesh; inside the
	// hill every face is a back face, so the arrow crosses the whole hill and hits. This rule walks the voxels themselves:
	//  - aiming (ProjectileAiming.IsProjectileObstructed postfix): an arc that runs through solid terrain = obstructed, the unit
	//    does not take that shot (ends near the shooter and the target are skipped: they may stand against the wall);
	//  - flight (KinematicProjectile.CheckCollisions postfix): a projectile that found no collision but whose step runs through
	//    the INSIDE of the ground (full terrain voxel) slipped through the mesh: it is removed like an out-of-bounds projectile.
	// Patches/WallShotPatches.cs.
	public static class BtcWallShot
	{
		private static int _idx = -2;
		private static int _logged;

		public static void AddRows()
		{
			BtcGameplay.Add("rng.noWallShot", "rng", "No shots through hills", "Без стрельбы сквозь горы", 1f, 0f, 1f, 1f, true,
				"Vanilla bug: at some exact angles ranged units shot through a whole hill (for example at units or buildings standing right against a cliff). With this rule arrows and bolts never fly through solid ground.",
				"Ванильный баг: под некоторыми точными углами стрелки простреливали насквозь целую гору (например, по юнитам или зданиям вплотную к скале). С этим правилом стрелы и болты никогда не пролетают сквозь сплошную землю.");
		}

		internal static bool On
		{
			get
			{
				if (!BtcNetworkGate.Enabled) return false;
				if (_idx == -2) _idx = BtcGameplay.Idx("rng.noWallShot");
				float[] a = BtcGameplay.Active;
				return _idx >= 0 && a != null && _idx < a.Length && a[_idx] > 0.5f;
			}
		}

		/// <summary>Point inside terrain: a full terrain voxel, or a crust voxel below its surface (deep = full voxels only).</summary>
		private static bool Solid(Vector3 p, bool deep)
		{
			XYZ c = XYZ.FromVector3(p);
			if (Voxel.IsEmpty(c) || !Voxel.IsTerrain(c)) return false;
			if (!Voxel.IsCrust(c)) return true;
			if (deep) return false;
			float h = Voxel.ComputeSurfaceHeightFromNiveaux(Voxel.GetNiveaux(c), p.x - c.x, p.z - c.z);
			return p.y < h - 0.05f;
		}

		private const float EndSkip = 0.75f, Step = 0.3f;

		/// <summary>Aiming: the ballistic arc from origin to target runs through the ground (ends skipped).</summary>
		internal static bool ArcBlocked(Vector3 origin, Vector3 target, float power)
		{
			Vector3 delta = target - origin;
			float dist = delta.magnitude;
			if (dist < EndSkip * 2f + Step) return false;
			Vector3 v = ProjectilePhysics.ComputeFiringVelocity(power, delta, false);
			float speed = v.magnitude;
			if (speed < 0.01f) return false;
			float dur = ProjectilePhysics.ComputeFlightDuration(v, delta);
			if (float.IsNaN(dur) || dur <= 0f || dur > 30f) dur = dist / speed;
			int n = Mathf.Clamp(Mathf.CeilToInt(dur * speed / Step), 2, 400);
			float dt = dur / n;
			Vector3 p = origin, vel = v;
			for (int i = 1; i < n; i++)
			{
				p = ProjectilePhysics.ComputeNextPosition(p, ref vel, dt);
				if ((p - origin).sqrMagnitude < EndSkip * EndSkip || (p - target).sqrMagnitude < EndSkip * EndSkip) continue;
				if (Solid(p, false))
				{
					if (_logged < 5) { _logged++; BtcLog.Info("WallShot", "shot refused: arc through the ground at " + p); }
					return true;
				}
			}
			return false;
		}

		/// <summary>Flight: the step from a to b (no collision found) passes through the inside of the ground.</summary>
		internal static bool StepInsideGround(Vector3 a, Vector3 b)
		{
			Vector3 d = b - a;
			float len = d.magnitude;
			if (len < 0.01f || Solid(a, true)) return false;   // stuck arrows sit inside a wall voxel: only an ENTRY without collision counts
			int n = Mathf.Clamp(Mathf.CeilToInt(len / Step), 1, 64);
			for (int i = 1; i <= n; i++) if (Solid(a + d * ((float)i / n), true)) return true;
			return false;
		}

		internal static void LogRemoved(Vector3 p)
		{
			if (_logged < 10) { _logged++; BtcLog.Info("WallShot", "projectile inside the ground removed at " + p); }
		}
	}
}
