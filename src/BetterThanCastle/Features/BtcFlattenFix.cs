using System.Collections.Generic;
using Brix.Engine;
using Brix.Game.AI;
using Brix.Game.AI.KnowledgeSpace;
using Brix.Game.Components;
using HarmonyLib;
using Motus.Behavior;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
	// Rule "work.flatFix" (host rule): vanilla Flatten fills in layers, so workers stop walling themselves in.
	// Why vanilla does it: a fill goal sits on the lowest open cell of each column and its priority prefers low cells, but
	// LandscapeProject picks with GoodEnoughFromSample (best of the first ~10 goals in dictionary order), so a worker standing in a
	// deep column often fills the shallower columns around him first; once they are 2 higher than his floor he cannot climb out.
	// The goal list is also re-walked only every 15 s, and nothing stops a fill into a cell a bricktron stands in.
	// Fix: compare all goals (global lowest first), a fill goal waits while a neighbouring column still has a lower open fill
	// goal (columns never differ by more than 1), no fill into an occupied cell, re-walk every 3 s. Terraform tasks are not touched.
	internal static class BtcFlattenFix
	{
		private const float RefreshDelay = 3f;
		private const int Depth = 8;
		private static int _idx = -2;

		internal static void AddRows()
		{
			BtcGameplay.Add("work.flatFix", "eco", "Flatten: fill in layers", "Выравнивание: насыпать слоями", 1f, 0f, 1f, 1f, true,
				"Workers of the vanilla Flatten task always fill the lowest cells first, layer by layer, and never fill a cell someone stands in. Vanilla often filled the higher cells around a worker first and walled him into a pit.",
				"Рабочие ванильного задания Выровнять всегда засыпают сначала самые нижние клетки, слой за слоем, и не засыпают клетку, где кто-то стоит. В ваниле они часто засыпали более высокие клетки вокруг себя и замуровывали себя в яме.");
		}

		internal static bool On
		{
			get
			{
				if (_idx == -2) _idx = BtcGameplay.Idx("work.flatFix");
				float[] a = BtcGameplay.Active;
				return _idx >= 0 && a != null && _idx < a.Length && a[_idx] > 0.5f;
			}
		}

		internal static void Setup(LandscapeProject lp)
		{
			if (lp == null || !NetworkServer.active || !On) return;
			if (BtcTerraform.Of(lp) != null) return;
			lp.SetGoalSelectionModel(GoalSelector.Type.CompareAll, false);
			LandscapeGoalProvider p = lp.goalProvider;
			if (p != null && p.AutoRefreshDelay > RefreshDelay) p.AutoRefreshDelay = RefreshDelay;
		}

		private static bool NeedsFill(XYZ p) { return Voxel.IsEmpty(p) || Voxel.IsCrust(p); }

		/// <summary>A neighbouring column (8 around) still has a live fill goal below this one that this worker can reach.</summary>
		internal static bool LowerNeighbour(LandscapeGoalProvider p, XYZ v, Labor labor)
		{
			Dictionary<XYZ, SpawnOrder> map = p.spawnPerVoxel;
			if (map == null || map.Count == 0) return false;
			for (int dx = -1; dx <= 1; dx++)
				for (int dz = -1; dz <= 1; dz++)
				{
					if (dx == 0 && dz == 0) continue;
					for (int dy = 1; dy <= Depth; dy++)
					{
						XYZ w = new XYZ(v.x + dx, v.y - dy, v.z + dz);
						SpawnOrder o;
						if (!map.TryGetValue(w, out o)) continue;
						LandscapeGoal g = o.instance as LandscapeGoal;
						if (g == null || g.IsNullOrReleased() || !g.IsFill || !NeedsFill(w)) continue;
						// a lower cell nobody can get to must not hold the whole task
						if (!Knowledge.Instance.CanReach(labor, g, -1)) continue;
						return true;
					}
				}
			return false;
		}
	}

	[HarmonyPatch(typeof(LandscapeProject), "Awake")]
	internal static class FlattenFixAwakePatch
	{
		private static void Postfix(LandscapeProject __instance)
		{
			try { BtcFlattenFix.Setup(__instance); }
			catch (System.Exception e) { BtcLog.Error("Hook", "FlattenFix.Awake: " + e.Message); }
		}
	}

	[HarmonyPatch(typeof(LandscapeGoal), "IsMatchFor")]
	internal static class FlattenFixIsMatchPatch
	{
		private static void Postfix(LandscapeGoal __instance, Labor labor, AIErrorsStore errorDestination, ref bool __result)
		{
			if (!__result || labor == null || !__instance.IsFill || !BtcFlattenFix.On) return;
			try
			{
				LandscapeProject lp = __instance._landscapeProject;
				if (lp == null || BtcTerraform.Of(lp) != null) return;
				XYZ v = __instance.voxel.position;
				if (BtcTerraformFill.Occupied(v, labor))
				{
					errorDestination.AddDecisionError(SharedWorkException<Exceptions.GoalTooCrowded>.Get.Problem, labor);
					__result = false;
					return;
				}
				if (lp.goalProvider != null && BtcFlattenFix.LowerNeighbour(lp.goalProvider, v, labor)) __result = false;
			}
			catch (System.Exception e) { BtcLog.Error("Hook", "FlattenFix.IsMatch: " + e.Message); }
		}
	}
}
