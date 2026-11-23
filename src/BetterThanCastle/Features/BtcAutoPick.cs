using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Brix.Engine.Nature;
using Brix.Game.AI;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.Components;
using Brix.Legacy;

namespace BetterThanCastle
{
	// "Pick up only what the task needs": a worker on a task (its GarnotteEater while digging/harvesting, and the cleanup/litter
	// goals of harvest and repair projects) takes only the items that task is for; everything else stays on the ground for a
	// Cleanup project or a manual pie-menu order. Host side, where the AI runs.
	//   quarry / tunnel: the ores switched on in it (vanilla TypesToHarvest; stone only if stone is on)
	//   flatten: stone only; terraforming (a flatten too): the ores kept in its panel
	//   a unit taken off its task (move order) keeps that task's filter while its eater still runs
	//   harvest: what its accepted types give (boulder stone, iron, pyrite brimstone, plant fiber, tree logs + stumps)
	//   repair: the materials the repair needs
	//   any other task or no task: vanilla
	public static class BtcAutoPick
	{
		private static int _idx = -2;

		private static readonly HashSet<int> _manual = new HashSet<int>();
		private static readonly List<GameObject> _drop = new List<GameObject>();

		public static void SetManual(GarnotteEater e, bool on)
		{
			if (on) _manual.Add(e.GetInstanceID()); else _manual.Remove(e.GetInstanceID());
		}

		// keys of pooled/spawned items are not always the same instance as the RawResources statics: compare by name too
		private static bool Same(Factory.AssetKey k, Factory.AssetKey r)
		{
			return k == r || (k.Name != null && k.Name == r.Name);
		}

		// the tasks this rule filters; any other project (IdleProject after a move order, build, ...) is not one
		private static bool IsTask(Project p)
		{
			return p is MineProject || p is TunnelProject || p is LandscapeProject || p is HarvestProject || p is RepairProject;
		}

		// Tunnel and quarry workers pick up the kept ores all the time while on the task, also on the way (vanilla: only
		// while digging a kept voxel + 3 s). Re-armed for 1 s on every Starve, so it stops by itself once the unit
		// leaves the task. Not for flatten/terraform: a no-storage terraform drops its full bag next to the task.
		public static void OnStarve(GarnotteEater e)
		{
			if (!On) return;
			Labor l = e.GetComponent<Labor>();
			Project p = l != null ? l.Project : null;
			GroundResource types = GroundResource.None;
			MineProject mine = p as MineProject;
			if (mine != null) types = mine.TypesToHarvest;
			TunnelProject tunnel = p as TunnelProject;
			if (tunnel != null) types = tunnel.TypesToHarvest;
			if (types == GroundResource.None) return;
			e.Eat(1f);
		}

		// DigGoal.SetupBuilder: "no eater while digging this voxel" (vanilla = its resource is not kept). With the rule on
		// the eater runs whenever the dig keeps any ore, so loose kept ores next to an overkilled voxel are picked up too.
		public static bool OverkillNoEat(bool overkill, DigGoal g)
		{
			if (!overkill || !On) return overkill;
			IDigGoalResourceSetting rs = g.ResourceSetting;
			return rs == null || rs.TypesToHarvest == GroundResource.None;
		}

		private static bool Ore(GroundResource types, Factory.AssetKey k)
		{
			if (Same(k, RawResources.RawStone)) return (types & GroundResource.Stone) != 0;
			if (Same(k, RawResources.RawIron)) return (types & GroundResource.Iron) != 0;
			if (Same(k, RawResources.RawOrangeCrystal)) return (types & GroundResource.Brimstone) != 0;
			return false;
		}

		// true = the task needs this item (vanilla pick-up), false = leave it lying
		public static bool Wanted(Project p, GameObject go)
		{
			if (p == null || go == null) return true;
			GameComponent gc = go.GetComponent<GameComponent>();
			Factory.AssetKey k = gc != null ? gc.AssetKey : null;
			if (k == null) return true;
			MineProject mine = p as MineProject;
			if (mine != null) return Ore(mine.TypesToHarvest, k);
			TunnelProject tunnel = p as TunnelProject;
			if (tunnel != null) return Ore(tunnel.TypesToHarvest, k);
			if (p is LandscapeProject)
			{
				// terraforming: the ores kept in its panel; plain flatten: stone only (brimstone and iron stay lying)
				BtcTerraform.Shape s = BtcTerraform.Of(p);
				return Ore(s != null ? (GroundResource)s.Harvest : GroundResource.Stone, k);
			}
			HarvestProject harvest = p as HarvestProject;
			if (harvest != null)
			{
				NatureType t = harvest.AcceptedTypes;
				if (Same(k, RawResources.RawStone)) return (t & NatureType.boulder) != 0;
				if (Same(k, RawResources.RawIron)) return (t & NatureType.iron) != 0;
				if (Same(k, RawResources.RawOrangeCrystal)) return (t & NatureType.pyrite) != 0;
				if (Same(k, RawResources.RawFiber)) return (t & NatureType.plant) != 0;
				if (BtcChopRules.IsLogOrStump(go)) return (t & NatureType.tree) != 0;
				return false;
			}
			if (p is RepairProject) return p.UniqueItemsNeeded().Contains(k);
			return true;
		}

		public static void DropOld(GarnotteEater eater, HashSet<GameObject> gos)
		{
			_drop.Clear();
			Labor owner = eater.GetComponent<Labor>();
			Project proj = owner != null ? owner.Project : null;
			CleanupProject cleanProj = proj as CleanupProject;
			if (cleanProj != null)
			{
				_lastTask.Remove(eater.GetInstanceID());
				foreach (GameObject go in gos) if (BtcCleanFilter.Blocked(cleanProj, go)) _drop.Add(go);
				for (int i = 0; i < _drop.Count; i++) gos.Remove(_drop[i]);
				_drop.Clear();
				return;
			}
			if (!On) return;
			if (_manual.Contains(eater.GetInstanceID())) return;
			// the eater keeps eating ~3 s after the work stops (vanilla StarveIn): a unit taken off its task by a move
			// order sits in the faction IdleProject by then and still filters by its last task, else it would sweep up
			// everything it skipped
			int eid = eater.GetInstanceID();
			Project last;
			if (IsTask(proj)) _lastTask[eid] = proj;
			else if (_lastTask.TryGetValue(eid, out last))
			{
				if (last == null) _lastTask.Remove(eid);
				else proj = last;
			}
			if (!IsTask(proj)) return;
			foreach (GameObject go in gos)
			{
				if (go == null || go.GetComponent<Garnotte>() == null) continue;
				bool want = Wanted(proj, go);
				if (!want) _drop.Add(go);
				Diag(proj, go, want);
			}
			for (int i = 0; i < _drop.Count; i++) gos.Remove(_drop[i]);
			_drop.Clear();
		}

		// one log line per (task kind, item, verdict) so a user log shows what each task keeps and skips
		private static readonly HashSet<string> _seen = new HashSet<string>();
		private static void Diag(Project p, GameObject go, bool want)
		{
			if (_seen.Count > 60) return;
			GameComponent gc = go.GetComponent<GameComponent>();
			string key = p.GetType().Name + " " + (gc != null && gc.AssetKey != null ? gc.AssetKey.Name : go.name) + (want ? " keep" : " skip");
			if (!_seen.Add(key)) return;
			string extra = "";
			MineProject m = p as MineProject; if (m != null) extra = " types=" + m.TypesToHarvest;
			TunnelProject t = p as TunnelProject; if (t != null) extra = " types=" + t.TypesToHarvest;
			HarvestProject h = p as HarvestProject; if (h != null) extra = " accepted=" + h.AcceptedTypes;
			BtcLog.Info("AutoPick", "eater " + key + extra);
		}

		private static readonly Dictionary<int, Project> _lastTask = new Dictionary<int, Project>();
		private static readonly Dictionary<int, bool> _cleanupOwner = new Dictionary<int, bool>();
		private static readonly Dictionary<int, CleanupProject> _ownerProj = new Dictionary<int, CleanupProject>();
		private static readonly Dictionary<int, Project> _taskOwner = new Dictionary<int, Project>();
		private static FieldInfo _providersField;

		public static CleanupProject OwnerProject(Component p)
		{
			CleanupProject cp;
			return _ownerProj.TryGetValue(p.GetInstanceID(), out cp) ? cp : null;
		}

		// the project whose provider list holds this very provider (cached; project prefabs may share a parent)
		public static Project TaskOf(Component p)
		{
			int id = p.GetInstanceID();
			Project v;
			if (_taskOwner.TryGetValue(id, out v)) return v;
			v = null;
			if (_providersField == null) _providersField = typeof(Project).GetField("GoalProviders", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (_providersField == null) { BtcLog.Error("AutoPick", "Project.GoalProviders field not found"); return null; }
			Project[] owners = p.GetComponentsInParent<Project>(true);
			for (int i = 0; i < owners.Length && v == null; i++)
			{
				List<IGoalProvider> list = _providersField.GetValue(owners[i]) as List<IGoalProvider>;
				if (list == null) continue;
				for (int j = 0; j < list.Count; j++) if (object.ReferenceEquals(list[j], p)) { v = owners[i]; break; }
			}
			_taskOwner[id] = v;
			return v;
		}

		public static void AddRows()
		{
			BtcGameplay.Add("mov.noAutoPick", "mov", "Pick up only what the task needs", "Подбирать только нужное заданию", 1f, 0f, 1f, 1f, true, "Workers on a task pick up only the loose items that task is for: a quarry or tunnel only the ores switched on in it (stone only if stone is on), a harvest only what is chosen in it (pyrite harvest leaves stone and iron), flattening only stone, terraforming the resources kept in its panel, repair only the materials it needs. Other items stay for the Cleanup task or a manual pie-menu order. Host setting.", "Рабочие на задании подбирают только те валяющиеся предметы, ради которых это задание: карьер и туннель только включённые в них руды (камень только если включён камень), сбор только выбранное в нём (сбор пирита не трогает камень и железо), выравнивание только камень, терраформинг ресурсы, отмеченные в его панели, ремонт только нужные ему материалы. Остальное остаётся для задания «Клининг» или ручного приказа из пай-меню. Настройка хоста.");
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
