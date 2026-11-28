using System;
using System.Collections.Generic;
using Brix.Game.AI;
using Brix.Game.AI.KnowledgeSpace;
using Brix.Game.Components;
using Brix.Game.Semantique;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
	// Rule craft.kits "Better Workbenches: assemble full kits on racks" (default ON, needs craft.better, host AI).
	// Vanilla stores a kit part on the nearest rack with room (a small bonus for a missing part), so hats and poleaxes end up on different
	// racks and nobody can take a full kit. With the rule:
	// 1) storing a kit part (Knowledge.ClosestStorageWithRoomFor) picks, at any distance: a rack where the part is missing from a started kit
	//    of its class (the fullest first, then the nearest), else a rack that already holds the same part (stacks: quivers on the quiver
	//    rack), else an empty rack, else the vanilla answer;
	// 2) an idle worker (IdleProject.PackageFor: about to wander or sit) moves a part from the least full rack of a class to the fullest rack
	//    of that class that misses it, until no two started kits of one class can be merged. After the pickup the normal store goes to the
	//    fullest rack by 1), the source is never fuller than the target, so parts never travel back. One worker per part, racks somebody is
	//    running to for a kit (mov.kitReserve) are left alone.
	internal static class BtcKitAssemble
	{
		private static int _idx = -2;

		public static void AddRows()
		{
			BtcGameplay.Add("craft.kits", "eco", "Better Workbenches: assemble full kits on racks", "Улучшенные верстаки: собирать полные наборы на стойках", 1f, 0f, 1f, 1f, true,
				"Workers put each kit part on the rack where it completes a started kit (at any distance), and idle workers move parts between racks until the kits are full. Off: vanilla (the nearest rack).",
				"Рабочие кладут каждую часть набора на стойку, где она дополняет начатый набор (на любом расстоянии), а свободные рабочие перекладывают части между стойками, пока наборы не станут полными. Выкл: как в ваниле (ближайшая стойка).");
		}

		internal static bool On
		{
			get
			{
				if (!BtcWorkbench.On || !NetworkServer.active) return false;
				if (_idx == -2) _idx = BtcGameplay.Idx("craft.kits");
				float[] a = BtcGameplay.Active;
				return _idx >= 0 && a != null && _idx < a.Length && a[_idx] > 0.5f;
			}
		}

		private static readonly List<List<Ressource>> Classes = AccessTools.StaticFieldRefAccess<ToolRack, List<List<Ressource>>>("AllClasses");

		// ------------------------------------------------------------------ rack state
		/// class of the kit on the rack (null = empty), number of its parts (one per kind), full kit
		private static List<Ressource> ClassOf(Recepteur r, out int parts, out bool full)
		{
			parts = 0; full = false;
			Description c = r.ContentDescription;
			for (int k = 0; k < Classes.Count; k++)
			{
				List<Ressource> cl = Classes[k];
				int n = 0;
				for (int i = 0; i < cl.Count; i++) if (c.Value(cl[i].GetType()) > 0) n++;
				if (n == 0) continue;
				parts = n; full = n == cl.Count;
				return cl;
			}
			return null;
		}

		private static List<Ressource> ClassOf(Ressource part)
		{
			if (part == null) return null;
			Type t = part.GetType();
			for (int k = 0; k < Classes.Count; k++)
				for (int i = 0; i < Classes[k].Count; i++) if (Classes[k][i].GetType() == t) return Classes[k];
			return null;
		}

		private static bool Usable(ToolRack rack, Brix.Game.Faction faction)
		{
			return rack != null && !rack.IsNullOrReleased() && rack.recepteur != null && ReferenceEquals(rack.faction, faction);
		}

		// ------------------------------------------------------------------ 1) where to store a part
		/// Knowledge.ClosestStorageWithRoomFor postfix: a better rack for a kit part, or null = keep the vanilla answer
		internal static IGameComponent Choose(Labor l, IGameComponent item, Brix.Game.Faction faction)
		{
			if (!On || l == null || item == null || item.IsNullOrReleased() || item.apparence == null) return null;
			Ressource part = item.apparence.Principale;
			List<Ressource> cls = ClassOf(part);
			GameObject go = item.GameObject;
			if (cls == null || go == null) return null;
			Type kind = part.GetType();
			ToolRack[] racks = BtcWorkbench.Racks();
			if (racks == null) return null;
			ToolRack best = null, same = null, empty = null;
			int bestN = 0, sameN = 0;
			float bestD = float.MaxValue, sameD = float.MaxValue, emptyD = float.MaxValue;
			Vector3 p = l.transform.position;
			for (int i = 0; i < racks.Length; i++)
			{
				ToolRack rack = racks[i];
				if (!Usable(rack, faction)) continue;
				Recepteur r = rack.recepteur;
				if (r.Carries(go)) continue;   // the rack it lies on
				int n; bool full;
				List<Ressource> c = ClassOf(r, out n, out full);
				if (c != null && c != cls) continue;
				float d = (rack.transform.position - p).sqrMagnitude;
				int have = c != null ? r.ContentDescription.Value(kind) : 0;
				if (have > 0)
				{
					// the same part already lies there: stack it (the most of it first, then the nearest)
					if ((have > sameN || (have == sameN && d < sameD)) && r.HasRoomFor(go) && Knowledge.Instance.CanReach(l, rack, -1)) { same = rack; sameN = have; sameD = d; }
				}
				else if (c == null)
				{
					if (d < emptyD && r.HasRoomFor(go) && Knowledge.Instance.CanReach(l, rack, -1)) { empty = rack; emptyD = d; }
				}
				else if (!full && (n > bestN || (n == bestN && d < bestD)) && r.HasRoomFor(go) && Knowledge.Instance.CanReach(l, rack, -1))
				{
					best = rack; bestN = n; bestD = d;
				}
			}
			ToolRack pick = best ?? same ?? empty;
			return pick != null ? (IGameComponent)pick : null;
		}

		// ------------------------------------------------------------------ 2) idle workers merge started kits
		private sealed class Move { internal Labor L; internal float Until; }
		private static readonly Dictionary<GameObject, Move> Moving = new Dictionary<GameObject, Move>();
		private const float MoveSlot = 45f;
		private static float _nothingUntil;
		private static readonly System.Reflection.MethodInfo PrepareM = AccessTools.Method(typeof(IdleProject), "Prepare");

		/// IdleProject.PackageFor postfix (action WalkInPoint / Sit): a part to move, or the vanilla package
		internal static void OnIdle(IdleProject project, Labor l, ref OrderPackage<Labor> result)
		{
			if (!On || l == null || l.IsNullOrReleased() || l.recepteur == null || !l.recepteur.IsEmpty()) return;
			if (l.Profession == null || l.Profession.OccupationType != Occupation.Type.Builder) return;
			if (Time.time < _nothingUntil) return;
			GameObject part = FindMove(l);
			if (part == null) { _nothingUntil = Time.time + 2f; return; }
			Moving[part] = new Move { L = l, Until = Time.time + MoveSlot };
			OrderPackage<Labor> pkg = Order.Package.OfGoIntTuple<Labor>(Order.Transfer.GoPickUp, Smooth.Algebraics.Tuple.Create<GameObject, int>(part, -1), Labor.DecisionSource.Project);
			IdleProject ip = project;
			pkg.SetPrepare(delegate(BaseLabor w)
			{
				Labor lw = w as Labor;
				if (lw == null || PrepareM == null) return true;
				return (bool)PrepareM.Invoke(ip, new object[] { lw, true, Activity.Idle });
			});
			result = pkg;
#if DEV
			BtcLog.Info("KitMerge", l.name + " moves " + part.name);
#endif
		}

		private static bool Taken(GameObject part, Labor l)
		{
			Move m;
			if (!Moving.TryGetValue(part, out m)) return false;
			if (m.L == l) return false;
			bool alive = m.L != null && !m.L.IsNullOrReleased() && Time.time < m.Until;
			if (!alive) { Moving.Remove(part); return false; }
			return true;
		}

		/// the part to move: per class, from the least full started kit to the fullest one that misses it
		private static GameObject FindMove(Labor l)
		{
			ToolRack[] racks = BtcWorkbench.Racks();
			if (racks == null || racks.Length < 2) return null;
			for (int i = 0; i < racks.Length; i++)
			{
				ToolRack src = racks[i];
				if (!Usable(src, l.faction) || BtcKitReserve.AnyPick(src.gameObject)) continue;
				int sn; bool sfull;
				List<Ressource> cls = ClassOf(src.recepteur, out sn, out sfull);
				if (cls == null || sfull) continue;
				// a fuller (or equal, ordered by index) started kit of the class that misses one of src's parts
				for (int j = 0; j < racks.Length; j++)
				{
					ToolRack dst = racks[j];
					if (j == i || !Usable(dst, l.faction)) continue;
					int dn; bool dfull;
					List<Ressource> dc = ClassOf(dst.recepteur, out dn, out dfull);
					if (dc != cls || dfull || dn < sn || (dn == sn && j < i)) continue;
					foreach (IDescriptor d in src.recepteur.StoredItems)
					{
						GameObject go = d != null ? d.GameObject : null;
						Ressource pr = d != null ? d.Principale : null;
						if (go == null || pr == null || Taken(go, l)) continue;
						if (dst.recepteur.ContentDescription.Value(pr.GetType()) > 0 || !dst.recepteur.HasRoomFor(go)) continue;
						if (!Knowledge.Instance.CanReach(l, src, -1) || !Knowledge.Instance.CanReach(l, dst, -1)) continue;
						return go;
					}
				}
			}
			return null;
		}

		internal static void Clear() { Moving.Clear(); _nothingUntil = 0f; }
	}
}
