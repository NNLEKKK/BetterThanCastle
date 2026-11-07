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
	// (btc-infheal.txt: one "x y z" line per unit) and after the load the nearest unit to every line gets a dummy zero-regen aura again.
	// The healing itself runs on the host (HP is a SyncVar), so nothing extra is sent to clients. Host only; the file is ignored by vanilla.
	internal static class BtcInfHeal
	{
		internal const string FileName = "btc-infheal.txt";
		private static List<Vector4> _pending;
		private static float _applyAt;

		private static readonly System.Reflection.FieldInfo TrackedField = HarmonyLib.AccessTools.Field(typeof(RegenAura), "tracked");
		private static readonly System.Reflection.FieldInfo RegenField = HarmonyLib.AccessTools.Field(typeof(RegenAura), "_currentRegen");
		private static readonly Dictionary<int, RegenAura> Dummies = new Dictionary<int, RegenAura>();

		private static bool IsDummy(RegenAura a) { return a != null && a.gameObject.name == "BtcInfHealAura"; }

		// stuck = the unit keeps an external regenerator that no longer serves it: switched off (regen 0), the unit left its list,
		// the aura is carried / inactive, or the unit is far away from it (a lifted ward never sends the exit event)
		private static bool Stuck(DamageReceiver dr)
		{
			RegenAura a = dr.ExternalRegenerator;
			if (a == null) return false;
			if (IsDummy(a)) return true;
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
				  .Append(p.z.ToString("F2", CultureInfo.InvariantCulture)).Append(' ').Append(dr.ExternalRegenerator.RegenPerSecond).Append('\n');
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
			List<Vector4> list = new List<Vector4>();
			string[] lines = File.ReadAllLines(file);
			for (int i = 0; i < lines.Length; i++)
			{
				string[] t = lines[i].Split(' ');
				float x, y, z;
				float rg = -1f;
				if (t.Length >= 3 && (t.Length < 4 || float.TryParse(t[3], NumberStyles.Float, CultureInfo.InvariantCulture, out rg)) && float.TryParse(t[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
					&& float.TryParse(t[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
					&& float.TryParse(t[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
					list.Add(new Vector4(x, y, z, rg));
			}
			if (list.Count == 0) return;
			_pending = list;
			_applyAt = Time.unscaledTime + 6f;
			BtcLog.Info("InfHeal", "save has " + list.Count + " stuck-heal unit(s), applying after the load");
		}

		// every frame (BtcTimer.Tick)
		internal static void Tick()
		{
			if (_pending == null || Time.unscaledTime < _applyAt) return;
			if (!NetworkServer.active) { _pending = null; return; }
			try
			{
				DamageReceiver[] all = UnityEngine.Object.FindObjectsOfType<DamageReceiver>();
				if (all.Length == 0) { _applyAt = Time.unscaledTime + 2f; return; }
				
				List<Vector4> todo = _pending;
				_pending = null;
				bool[] used = new bool[all.Length];
				int done = 0;
				for (int k = 0; k < todo.Count; k++)
				{
					int best = -1;
					float bestD = 2.5f * 2.5f;
					for (int i = 0; i < all.Length; i++)
					{
						DamageReceiver dr = all[i];
						if (used[i] || dr == null || !(dr is BricktronDamageReceiver)) continue;
						float d = (dr.transform.position - (Vector3)todo[k]).sqrMagnitude;
						if (d < bestD) { bestD = d; best = i; }
					}
					if (best < 0) continue;
					used[best] = true;
					if (all[best].ExternalRegenerator == null) all[best].ExternalRegenerator = Dummy(todo[k].w >= 0f ? Mathf.RoundToInt(todo[k].w) : LegacyRegen());
					done++;
				}
				BtcLog.Info("InfHeal", "restored " + done + "/" + todo.Count);
			}
			catch (Exception e) { _pending = null; BtcLog.Error("InfHeal", "Tick: " + e); }
		}

		// files written before the regen column existed: the strongest live aura, else 10
		private static int LegacyRegen()
		{
			int best = 10;
			RegenAura[] auras = UnityEngine.Object.FindObjectsOfType<RegenAura>();
			for (int i = 0; i < auras.Length; i++) if (!IsDummy(auras[i]) && auras[i].RegenPerSecond > best) best = auras[i].RegenPerSecond;
			return best;
		}

		private static RegenAura Dummy(int regen)
		{
			RegenAura a;
			if (Dummies.TryGetValue(regen, out a) && a != null) return a;
			GameObject go = new GameObject("BtcInfHealAura");
			UnityEngine.Object.DontDestroyOnLoad(go);
			a = go.AddComponent<RegenAura>();
			if (RegenField != null) RegenField.SetValue(a, regen);
			Dummies[regen] = a;
			return a;
		}
	}
}
