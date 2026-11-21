using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
	// list.md G5: "infinity heal" survives a save. The bug: a unit leaves a lifted (unpowered) healing ward without the exit event, so
	// DamageReceiver.ExternalRegenerator stays set to an aura whose RegenPerSecond is 0; vanilla then heals the unit everywhere, ignoring the hurt
	// delay. The field is runtime only, so a save/load drops it. Here the units in that state are written next to the vanilla save files
	// (btc-infheal.txt: one "x y z" line per unit) and after the load the nearest unit to every line gets its real source ward (RegenAura, found by saved position, also disabled ones) as ExternalRegenerator again, so it ends like vanilla when the ward is destroyed.
	// The healing itself runs on the host (HP is a SyncVar), so nothing extra is sent to clients. Host only; the file is ignored by vanilla.
	internal static class BtcInfHeal
	{
		internal const string FileName = "btc-infheal.txt";
		private sealed class Pend { public Vector4 P; public bool HasSrc; public Vector3 Src; }
		private static List<Pend> _pending;
		private static readonly List<RegenAura> Restored = new List<RegenAura>();
		private static float _nextWatch;
		private static float _giveUpAt;
		private static float _applyAt;

		private static readonly System.Reflection.FieldInfo TrackedField = HarmonyLib.AccessTools.Field(typeof(RegenAura), "tracked");

		// stuck = the unit keeps an external regenerator that no longer serves it: switched off (regen 0), the unit left its list,
		// the aura is carried / inactive, or the unit is far away from it (a lifted ward never sends the exit event)
		private static bool Stuck(DamageReceiver dr)
		{
			RegenAura a = dr.ExternalRegenerator;
			if (a == null) return false;
			if (a.RegenPerSecond <= 0) return true;
			if (!a.gameObject.activeInHierarchy) return true;
			if (a.GetComponentInParent<Brix.Game.AI.Labor>() != null) return true;
			List<DamageReceiver> tr = TrackedField.GetValue(a) as List<DamageReceiver>;
			if (tr == null || !tr.Contains(dr)) return true;
			Vector3 d = dr.transform.position - a.transform.position;
			return d.sqrMagnitude > 25f * 25f;
		}

		// Disk_Save postfix: the folder is the staging copy, ApplyStaging moves it into place afterwards
		internal static void OnSave(string folder)
		{
			if (!NetworkServer.active || !BtcNetworkGate.Enabled || string.IsNullOrEmpty(folder)) return;
			string file = Path.Combine(folder, FileName);
			StringBuilder sb = new StringBuilder();
			DamageReceiver[] all = UnityEngine.Object.FindObjectsOfType<DamageReceiver>();
			int n = 0, ext = 0;
			for (int i = 0; i < all.Length; i++)
			{
				DamageReceiver dr = all[i];
				if (dr != null && dr.ExternalRegenerator != null) ext++;
				if (dr == null || !Stuck(dr)) continue;
				Vector3 p = dr.transform.position;
				sb.Append(p.x.ToString("F2", CultureInfo.InvariantCulture)).Append(' ')
				  .Append(p.y.ToString("F2", CultureInfo.InvariantCulture)).Append(' ')
				  .Append(p.z.ToString("F2", CultureInfo.InvariantCulture)).Append(' ').Append(dr.ExternalRegenerator.RegenPerSecond);
					RegenAura src = SourceOf(dr);
					if (src != null) BtcLog.Info("InfHeal", "source ward " + src.transform.root.name + " active=" + src.gameObject.activeInHierarchy + " regen=" + src.RegenPerSecond + " pos=" + src.transform.position);
					if (src != null)
					{
						Vector3 sp = src.transform.position;
						sb.Append(' ').Append(sp.x.ToString("F2", CultureInfo.InvariantCulture)).Append(' ').Append(sp.y.ToString("F2", CultureInfo.InvariantCulture)).Append(' ').Append(sp.z.ToString("F2", CultureInfo.InvariantCulture));
					}
					sb.Append('\n');
				n++;
			}
			BtcLog.Info("InfHeal", "save scan: " + ext + " unit(s) with an external regenerator, " + n + " stuck");
			if (n == 0) { if (File.Exists(file)) File.Delete(file); return; }
			File.WriteAllText(file, sb.ToString());
			BtcLog.Info("InfHeal", "saved " + n + " unit(s) with a stuck heal aura");
		}

		// Asset_Map.Disk_ReadGameObjects prefix: the save being loaded
		internal static void OnLoad(string folder)
		{
			_pending = null;
			if (!NetworkServer.active || !BtcNetworkGate.Enabled || string.IsNullOrEmpty(folder)) return;
			string file = Path.Combine(folder, FileName);
			if (!File.Exists(file)) return;
			List<Pend> list = new List<Pend>();
			string[] lines = File.ReadAllLines(file);
			for (int i = 0; i < lines.Length; i++)
			{
				string[] t = lines[i].Split(' ');
				float x, y, z;
				float rg = -1f;
				if (t.Length >= 3 && (t.Length < 4 || float.TryParse(t[3], NumberStyles.Float, CultureInfo.InvariantCulture, out rg)) && float.TryParse(t[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
					&& float.TryParse(t[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
					&& float.TryParse(t[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
					{
						Pend pe = new Pend(); pe.P = new Vector4(x, y, z, rg);
						float sx, sy, sz;
						if (t.Length >= 7 && float.TryParse(t[4], NumberStyles.Float, CultureInfo.InvariantCulture, out sx) && float.TryParse(t[5], NumberStyles.Float, CultureInfo.InvariantCulture, out sy)
							&& float.TryParse(t[6], NumberStyles.Float, CultureInfo.InvariantCulture, out sz)) { pe.HasSrc = true; pe.Src = new Vector3(sx, sy, sz); }
						list.Add(pe);
					}
			}
			if (list.Count == 0) return;
			_pending = list;
			_applyAt = Time.unscaledTime + 6f;
			_giveUpAt = Time.unscaledTime + 40f;
			BtcLog.Info("InfHeal", "save has " + list.Count + " stuck-heal unit(s), applying after the load");
		}

		// the ward behind a stuck unit is its real RegenAura (vanilla keeps exactly that reference: a lifted / carried / stored ward
		// sends no exit event); when the ward object is destroyed the reference turns null and the heal ends by itself
		private static RegenAura SourceOf(DamageReceiver dr)
		{
			RegenAura a = dr.ExternalRegenerator;
			return a;
		}

		// includes inactive objects (a ward in a warehouse / workbench is disabled); scene objects only
		private static RegenAura FindWard(Vector3 pos, RegenAura[] auras, int regen)
		{
			RegenAura best = null;
			float bestD = 3f * 3f;
			for (int i = 0; i < auras.Length; i++)
			{
				RegenAura a = auras[i];
				if (a == null || !a.gameObject.scene.IsValid()) continue;
				float d = (a.transform.position - pos).sqrMagnitude;
				if (d < bestD) { bestD = d; best = a; }
			}
			return best;
		}

		// ArtificerStaff.ToHealing postfix: powering the ward drops the heal of every unit that was bound to it
		internal static void OnWardOn(RegenAura aura)
		{
			if (aura == null || !NetworkServer.active || !BtcNetworkGate.Enabled) return;
			DamageReceiver[] all = UnityEngine.Object.FindObjectsOfType<DamageReceiver>();
			int n = 0;
			for (int i = 0; i < all.Length; i++)
				if (all[i] != null && all[i].ExternalRegenerator == aura) { all[i].ExternalRegenerator = null; n++; }
			if (n > 0) BtcLog.Info("InfHeal", "ward powered, released " + n + " bound unit(s)");
		}
		// a restored ward that starts working (out of storage / the hands, active, regen > 0) releases its bound units, like powering it in vanilla
		private static void WatchWards()
		{
			for (int i = Restored.Count - 1; i >= 0; i--)
			{
				RegenAura a = Restored[i];
				if (a == null) { Restored.RemoveAt(i); continue; }
				if (!a.gameObject.activeInHierarchy || a.RegenPerSecond <= 0 || a.GetComponentInParent<Brix.Game.AI.Labor>() != null) continue;
				Restored.RemoveAt(i);
				OnWardOn(a);
			}
		}
		// every frame (BtcTimer.Tick)
		internal static void Tick()
		{
			if (Restored.Count > 0 && Time.unscaledTime >= _nextWatch) { _nextWatch = Time.unscaledTime + 0.5f; WatchWards(); }
			if (_pending == null || Time.unscaledTime < _applyAt) return;
			if (!NetworkServer.active) { _pending = null; return; }
			try
			{
				DamageReceiver[] all = UnityEngine.Object.FindObjectsOfType<DamageReceiver>();
				if (all.Length == 0) { _applyAt = Time.unscaledTime + 2f; return; }
				RegenAura[] auras = Resources.FindObjectsOfTypeAll<RegenAura>();

				List<Pend> todo = _pending;
				List<Pend> retry = new List<Pend>();
				bool[] used = new bool[all.Length];
				int done = 0, noWard = 0;
				for (int k = 0; k < todo.Count; k++)
				{
					Vector4 pk = todo[k].P;
					RegenAura src = todo[k].HasSrc ? FindWard(todo[k].Src, auras, Mathf.RoundToInt(pk.w)) : null;
					if (src == null)
					{
						if (todo[k].HasSrc && Time.unscaledTime < _giveUpAt) retry.Add(todo[k]); else noWard++;
						continue;
					}
					int best = -1;
					float bestD = 2.5f * 2.5f;
					for (int i = 0; i < all.Length; i++)
					{
						DamageReceiver dr = all[i];
						if (used[i] || dr == null || !(dr is BricktronDamageReceiver)) continue;
						float d = (dr.transform.position - (Vector3)pk).sqrMagnitude;
						if (d < bestD) { bestD = d; best = i; }
					}
					if (best < 0) continue;
					used[best] = true;
					if (all[best].ExternalRegenerator == null) { all[best].ExternalRegenerator = src; if (!Restored.Contains(src)) Restored.Add(src); }
					done++;
				}
				BtcLog.Info("InfHeal", "restored " + done + "/" + todo.Count + ", ward not found " + noWard + ", waiting " + retry.Count);
				if (retry.Count > 0) { _pending = retry; _applyAt = Time.unscaledTime + 3f; } else _pending = null;
			}
			catch (Exception e) { _pending = null; BtcLog.Error("InfHeal", "Tick: " + e); }
		}
	}
}