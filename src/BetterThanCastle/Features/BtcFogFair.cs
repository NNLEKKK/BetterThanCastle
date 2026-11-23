using Brix.Game;
using Brix.Game.Components;
using Brix.Game.Components.Volumes;
using UnityEngine;

namespace BetterThanCastle
{
	// Fair fog (Gameplay > Vision, BC ON, both default ON):
	//   fair.fog (A7 + Alt scan): nobody (the host included, who holds every object) can hover, click, pie-menu or right-click
	//            order an object its faction cannot see, and holding the "show overheads" key (Alt) no longer shows HP bars and icons
	//            of objects outside vision (vanilla: InFog was only refreshed on a vision change, so never-seen objects showed up).
	//   trap.bpLeak (A4): an enemy trap you cannot see no longer blocks your blueprints, so placing blueprints cannot locate traps.
	// Patches/FogFairPatches.cs.
	public static class BtcFogFair
	{
		private static int _iFog = -2, _iLeak = -2;

		public static void AddRows()
		{
			BtcGameplay.Add("fair.fog", "vis", "Strict fog of war: no clicks, pie menu or Alt info on unseen objects", "Честный туман: нельзя кликать, открывать пай-меню и смотреть по Alt то, чего не видишь", 1f, 0f, 1f, 1f, true, "Objects your team cannot see (enemy traps, buildings in the fog) cannot be hovered, selected, used from the pie menu or ordered at, and holding Alt does not show their HP bars and icons. Vanilla let the host do this. Host setting.", "Объекты, которых твоя команда не видит (вражеские трапы, постройки в тумане), нельзя навести, выделить, использовать из пай-меню или отдать на них приказ, а зажатый Alt не показывает их HP-бары и иконки. В ваниле хост так мог. Настройка хоста.");
			BtcGameplay.Add("trap.bpLeak", "vis", "Hidden enemy traps do not block blueprints", "Невидимые вражеские трапы не мешают блюпринтам", 1f, 0f, 1f, 1f, true, "In vanilla a blueprint could not be placed where an invisible enemy trap lies, so traps could be found by placing blueprints. Host setting.", "В ваниле блюпринт нельзя было поставить туда, где лежит невидимая вражеская трапа, и трапы вычислялись блюпринтами. Настройка хоста.");
		}

		private static bool Rule(ref int idx, string id)
		{
			if (!BtcNetworkGate.Enabled) return false;
			if (idx == -2) idx = BtcGameplay.Idx(id);
			float[] a = BtcGameplay.Active;
			return idx >= 0 && a != null && idx < a.Length && a[idx] > 0.5f;
		}

		internal static bool Fog { get { return Rule(ref _iFog, "fair.fog"); } }
		internal static bool Leak { get { return Rule(ref _iLeak, "trap.bpLeak"); } }

		/// <summary>true = an object with vision that the local faction cannot see (terrain and plain objects pass).</summary>
		internal static bool Hidden(GameObject go)
		{
			if (go == null || User.LocalUser == null || User.LocalUser.faction == null) return false;
			GameComponent gc = go.GetComponent<GameComponent>();
			if (gc == null) gc = go.GetComponentInParent<GameComponent>();
			LocalFactionVision v = gc != null ? gc.vision : null;
			return v != null && !v.IsVisibleForMe();
		}

		/// <summary>VUtil.IsObstructed replacement filter: a hidden enemy trap volume does not obstruct the placing faction.</summary>
		internal static bool IgnoredObstacle(IVolume v, Faction faction)
		{
			if (faction == null) return false;
			GameObject go = v.GameObject;
			if (go == null) return false;
			BearTrapTrigger t = go.GetComponentInParent<BearTrapTrigger>();
			if (t == null) t = go.GetComponentInChildren<BearTrapTrigger>();
			if (t == null) return false;
			LocalFactionVision vis = t.GetComponentInParent<LocalFactionVision>();
			return vis != null && !vis.IsVisibleForFaction(faction, true);
		}
	}
}
