using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Game.AI;
using Brix.Game.Components;
using UnityEngine;

namespace BetterThanCastle
{
	// C7 / C8 (Gameplay > Movement and worker AI, BC ON, host AI, both default OFF):
	//   clean.noStump: Cleanup never carries stumps (the Wood_Stump block left by a felled tree; it is not one of the cleanup filter kinds).
	//   keep logs (per player, Orders setting "chop_keep_logs" via BtcPlayerPrefs, default off): that player's tree-cutting tasks do not pick up
	//                  the logs and stumps they fell; they stay on the ground for Cleanup or a manual order.
	// Applied in the CleanUpGoalProvider.RefreshCleanupGoals postfix (Patches/CleanUpGoalProviderPatches.cs).
	public static class BtcChopRules
	{
		private static int _iStump = -2;
		private static readonly Dictionary<int, bool> _harvestOwner = new Dictionary<int, bool>();
		private static FieldInfo _harvestField;

		public static void AddRows()
		{
			BtcGameplay.Add("clean.noStump", "mov", "Cleanup: do not carry stumps", "Клининг: не носить пни", 0f, 0f, 1f, 1f, true, "The Cleanup task leaves stumps of felled trees where they lie. Host setting.", "Задание «Клининг» оставляет пни срубленных деревьев там, где они лежат. Настройка хоста.");
		}

		private static bool Rule(ref int idx, string id)
		{
			if (!BtcNetworkGate.Enabled) return false;
			if (idx == -2) idx = BtcGameplay.Idx(id);
			float[] a = BtcGameplay.Active;
			return idx >= 0 && a != null && idx < a.Length && a[idx] > 0.5f;
		}

		public static bool NoStump { get { return Rule(ref _iStump, "clean.noStump"); } }
		public static bool NoLogs(GameObject provider) { return BtcNetworkGate.Enabled && BtcPlayerPrefs.For(provider).KeepLogs; }

		internal static bool IsStump(GameObject go)
		{
			GameComponent gc = go != null ? go.GetComponent<GameComponent>() : null;
			return gc != null && gc.AssetKey != null && gc.AssetKey.Name == "Wood_Stump";
		}

		internal static bool IsLogOrStump(GameObject go)
		{
			if (IsStump(go)) return true;
			GameComponent gc = go != null ? go.GetComponent<GameComponent>() : null;
			string n = gc != null && gc.AssetKey != null ? gc.AssetKey.Name : null;
			return n != null && n.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		// true when this provider is a HarvestProject's own CleanUpGoals (cached per provider)
		internal static bool IsHarvestOwned(CleanUpGoalProvider p)
		{
			int id = p.GetInstanceID();
			bool v;
			if (_harvestOwner.TryGetValue(id, out v)) return v;
			v = false;
			if (_harvestField == null) _harvestField = typeof(HarvestProject).GetField("CleanUpGoals", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (_harvestField == null) BtcLog.Error("ChopRules", "HarvestProject.CleanUpGoals field not found");
			HarvestProject[] owners = p.GetComponentsInParent<HarvestProject>(true);
			for (int i = 0; _harvestField != null && i < owners.Length && !v; i++) v = ReferenceEquals(_harvestField.GetValue(owners[i]), p);
			_harvestOwner[id] = v;
			return v;
		}
	}
}
