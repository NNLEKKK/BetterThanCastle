using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Brix.Game.AI;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.Components;
using Brix.Legacy;

namespace BetterThanCastle
{
	// "Do not pick up lying items automatically": harvest and repair projects own a CleanUpGoalProvider, so a worker on an
	// iron-farming task grabs every loose stone in the project radius. While the rule is on, those providers keep no goals
	// (host side, where the AI runs); only a Cleanup project and manual pie-menu orders pick items up.
	public static class BtcAutoPick
	{
		private static int _idx = -2;

		private const float Fresh = 2f;
		private const float FreshMine = 30f;
		private static readonly HashSet<int> _manual = new HashSet<int>();
		private static readonly Dictionary<int, float> _born = new Dictionary<int, float>();
		private static readonly List<GameObject> _drop = new List<GameObject>();
		private static float _nextPrune;

		public static void NoteBorn(Garnotte g)
		{
			if (g == null) return;
			float now = Time.time;
			float dropT;
			if (_dropped.TryGetValue(g.gameObject.GetInstanceID(), out dropT))
			{
				// re-enabled right after a unit dropped it = still mess; later (pool reuse for a new drop) = a new item
				if (now - dropT < 0.5f) { _born.Remove(g.gameObject.GetInstanceID()); return; }
				_dropped.Remove(g.gameObject.GetInstanceID());
			}
			_born[g.gameObject.GetInstanceID()] = now;
			if (_bornLogged < 3) { _bornLogged++; BtcLog.Info("AutoPick", "garnotte born tracked: " + g.name + " t=" + now.ToString("0.0")); }
			if (now >= _nextPrune && _born.Count > 2000)
			{
				_nextPrune = now + 30f;
				List<int> old = new List<int>();
				foreach (KeyValuePair<int, float> kv in _born) if (now - kv.Value > 60f) old.Add(kv.Key);
				for (int i = 0; i < old.Count; i++) _born.Remove(old[i]);
			}
		}

		// removes loose garnottes that are not fresh (unknown = was lying there before) from the eater's candidates
		public static bool IsOld(GameObject go)
		{
			if (go == null || go.GetComponent<Garnotte>() == null) return false;
			if (_dropped.ContainsKey(go.GetInstanceID())) return true;
			float t;
			return !_born.TryGetValue(go.GetInstanceID(), out t) || Time.time - t > Fresh;
		}

		public static void SetManual(GarnotteEater e, bool on)
		{
			if (on) _manual.Add(e.GetInstanceID()); else _manual.Remove(e.GetInstanceID());
		}



		private static bool IsMiner(GarnotteEater eater)
		{
			Labor labor = eater.GetComponent<Labor>();
			return labor != null && labor.Project is MineProject;
		}
		// A miner only takes what its quarry is after: brimstone task = brimstone, never the stone lying next to it.
		private static bool WrongForMiner(GarnotteEater eater, GameObject go)
		{
			Labor labor = eater.GetComponent<Labor>();
			MineProject mine = labor != null ? labor.Project as MineProject : null;
			if (mine == null) return false;
			GroundResource types = mine.TypesToHarvest;
			if (types == GroundResource.None) return false;
			GameComponent gc = go.GetComponent<GameComponent>();
			if (gc == null) return false;
			Factory.AssetKey k = gc.AssetKey;
			if (k.Equals(RawResources.RawStone)) return (types & GroundResource.Stone) == 0;
			if (k.Equals(RawResources.RawIron)) return (types & GroundResource.Iron) == 0;
			if (k.Equals(RawResources.RawOrangeCrystal)) return (types & GroundResource.Brimstone) == 0;
			return false;
		}
		public static void DropOld(GarnotteEater eater, HashSet<GameObject> gos)
		{
			float now = Time.time;
			_drop.Clear();
			Labor owner = eater.GetComponent<Labor>();
			CleanupProject cleanProj = owner != null ? owner.Project as CleanupProject : null;
			if (cleanProj != null)
			{
				foreach (GameObject go in gos) if (BtcCleanFilter.Blocked(cleanProj, go)) _drop.Add(go);
				for (int i = 0; i < _drop.Count; i++) gos.Remove(_drop[i]);
				_drop.Clear();
				return;
			}
			if (!On) return;
			if (_manual.Contains(eater.GetInstanceID())) return;
			foreach (GameObject go in gos)
			{
				if (go == null || go.GetComponent<Garnotte>() == null) continue;
				float t;
				bool known = _born.TryGetValue(go.GetInstanceID(), out t) && !_dropped.ContainsKey(go.GetInstanceID());
				bool wrong = WrongForMiner(eater, go);
				bool old = !known || now - t > (IsMiner(eater) ? FreshMine : Fresh);
				if (_diag < 40) { _diag++; BtcLog.Info("AutoPick", "eater cand " + go.name + " known=" + known + " age=" + (known ? (now - t).ToString("0.0") : "-") + " wrongForMiner=" + wrong); }
				if (old || wrong) _drop.Add(go);
			}
			for (int i = 0; i < _drop.Count; i++) gos.Remove(_drop[i]);
			if (_drop.Count > 0 && !_logged)
			{
				_logged = true;
				BtcLog.Info("AutoPick", "eater: skipped " + _drop.Count + " old loose item(s)");
			}
			_drop.Clear();
		}

		private static bool _logged;
		private static int _bornLogged;
		private static int _diag;
		private static readonly Dictionary<int, float> _dropped = new Dictionary<int, float>();

		// items a unit threw out of its bag are mess for every task (only Cleanup or a manual order takes them)
		public static void NoteDropped(List<GameObject> objs)
		{
			float now = Time.time;
			if (_dropped.Count > 3000)
			{
				List<int> old = new List<int>();
				foreach (KeyValuePair<int, float> kv in _dropped) if (now - kv.Value > 600f) old.Add(kv.Key);
				for (int i = 0; i < old.Count; i++) _dropped.Remove(old[i]);
			}
			for (int i = 0; i < objs.Count; i++) if (objs[i] != null) _dropped[objs[i].GetInstanceID()] = now;
		}
		private static readonly Dictionary<int, bool> _cleanupOwner = new Dictionary<int, bool>();
		private static readonly Dictionary<int, CleanupProject> _ownerProj = new Dictionary<int, CleanupProject>();

		public static CleanupProject OwnerProject(Component p)
		{
			CleanupProject cp;
			return _ownerProj.TryGetValue(p.GetInstanceID(), out cp) ? cp : null;
		}

		public static void AddRows()
		{
			BtcGameplay.Add("mov.noAutoPick", "mov", "Do not pick up lying items automatically", "Не собирать валяющиеся предметы автоматически", 1f, 0f, 1f, 1f, true, "Workers on a task (mining, harvesting, repair) skip loose items on their way instead of carrying them to storage. Items are picked up only by the Cleanup task or by a manual pie-menu order. Host setting.", "Рабочие на задании (добыча, сбор, ремонт) пропускают валяющиеся по пути предметы и не относят их на склад. Предметы подбираются только заданием «Клининг» или ручным приказом из пай-меню. Настройка хоста.");
		}

		public static bool On
		{
			get
			{
				if (!BtcNetworkGate.Enabled) return false;
				if (_idx == -2) _idx = BtcGameplay.Idx("mov.noAutoPick");
				float[] a = BtcGameplay.Active;
				return _idx >= 0 && a != null && _idx < a.Length && a[_idx] > 0.5f;
			}
		}

		// true when the provider belongs to a Cleanup project (cached per provider, owners never change)
		public static bool IsCleanupOwned(Component p)
		{
			return IsCleanupOwned(p, p is CleanUpGoalProvider ? "CleanupGoals" : "LitterGoals");
		}

		private static bool IsCleanupOwned(Component p, string field)
		{
			int id = p.GetInstanceID();
			bool v;
			if (!_cleanupOwner.TryGetValue(id, out v))
			{
				// the provider is a Cleanup project's own only if that project's field points at this very instance
				// (project prefabs may share a parent, so a plain parent lookup is not enough)
				v = false;
				CleanupProject[] owners = p.GetComponentsInParent<CleanupProject>(true);
				FieldInfo f = typeof(CleanupProject).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (f == null) BtcLog.Error("AutoPick", "CleanupProject." + field + " field not found");
				for (int i = 0; f != null && i < owners.Length; i++)
				{
					if (object.ReferenceEquals(f.GetValue(owners[i]), p)) { v = true; _ownerProj[id] = owners[i]; }
				}
				_cleanupOwner[id] = v;
				BtcLog.Info("AutoPick", "provider " + p.name + ": cleanupOwned=" + v + " on=" + On + " cleanupProjectsAbove=" + owners.Length);
			}
			return v;
		}
	}
}
