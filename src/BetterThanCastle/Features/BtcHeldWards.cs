using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Transactions;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
	// list.md: wards carried by workers vanish on save/load in vanilla. Here every worker that holds a ward is written next to the save
	// (btc-heldwards.txt: "x y z wardIndex") and after the load the nearest worker without a full bag gets a fresh ward of that kind in the hands.
	// Host only, BC ON; the file is ignored by vanilla. Log tag HeldWard.
	internal static class BtcHeldWards
	{
		internal const string FileName = "btc-heldwards.txt";
		private static readonly Factory.AssetKey[] Keys = new Factory.AssetKey[]
		{
			ObjetsDynamiques.HealingWard, ObjetsDynamiques.SentinelWard, ObjetsDynamiques.CaptureWard, ObjetsDynamiques.LanternWard, ObjetsDynamiques.PylonGround
		};
		private static List<Vector4> _pending;
		private static float _applyAt;

		internal static void OnSave(string folder)
		{
			if (!NetworkServer.active || !BtcNetworkGate.Enabled || string.IsNullOrEmpty(folder)) return;
			string file = System.IO.Path.Combine(folder, FileName);
			StringBuilder sb = new StringBuilder();
			Labor[] all = UnityEngine.Object.FindObjectsOfType<Labor>();
			int n = 0;
			for (int i = 0; i < all.Length; i++)
			{
				Labor l = all[i];
				if (l == null || l.recepteur == null) continue;
				for (int k = 0; k < Keys.Length; k++)
				{
					GameObject go;
					if (!l.recepteur.FirstObjectOf(Keys[k], out go)) continue;
					Vector3 p = l.transform.position;
					sb.Append(p.x.ToString("F2", CultureInfo.InvariantCulture)).Append(' ')
					  .Append(p.y.ToString("F2", CultureInfo.InvariantCulture)).Append(' ')
					  .Append(p.z.ToString("F2", CultureInfo.InvariantCulture)).Append(' ').Append(k).Append('\n');
					n++;
					break;
				}
			}
			if (n == 0) { if (File.Exists(file)) File.Delete(file); return; }
			File.WriteAllText(file, sb.ToString());
			BtcLog.Info("HeldWard", "saved " + n + " carried ward(s)");
		}

		internal static void OnLoad(string folder)
		{
			_pending = null;
			if (!NetworkServer.active || !BtcNetworkGate.Enabled || string.IsNullOrEmpty(folder)) return;
			string file = System.IO.Path.Combine(folder, FileName);
			if (!File.Exists(file)) return;
			List<Vector4> list = new List<Vector4>();
			string[] lines = File.ReadAllLines(file);
			for (int i = 0; i < lines.Length; i++)
			{
				string[] t = lines[i].Split(' ');
				float x, y, z, w;
				if (t.Length == 4 && float.TryParse(t[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
					&& float.TryParse(t[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
					&& float.TryParse(t[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)
					&& float.TryParse(t[3], NumberStyles.Float, CultureInfo.InvariantCulture, out w))
					list.Add(new Vector4(x, y, z, w));
			}
			if (list.Count == 0) return;
			_pending = list;
			_applyAt = Time.unscaledTime + 2f;
			_deadline = Time.unscaledTime + 30f;
			_tries = 0;
			BtcLog.Info("HeldWard", "save has " + list.Count + " carried ward(s), restoring after the load");
		}

		// every frame (BtcTimer.Tick)
		private static float _deadline;
		private static int _tries;

		internal static void Tick()
		{
			if (_pending == null || Time.unscaledTime < _applyAt) return;
			if (!NetworkServer.active) { _pending = null; return; }
			try
			{
				Labor[] all = UnityEngine.Object.FindObjectsOfType<Labor>();
				_applyAt = Time.unscaledTime + 1f;
				_tries++;
				List<Vector4> left = new List<Vector4>();
				bool[] used = new bool[all.Length];
				int done = 0;
				for (int k = 0; k < _pending.Count; k++)
				{
					int ki = Mathf.Clamp(Mathf.RoundToInt(_pending[k].w), 0, Keys.Length - 1);
					int best = -1;
					float bestD = 6f * 6f;
					string why = "no worker within 6 blocks (" + all.Length + " workers)";
					for (int i = 0; i < all.Length; i++)
					{
						Labor l = all[i];
						if (used[i] || l == null || l.recepteur == null) continue;
						float d = (l.transform.position - (Vector3)_pending[k]).sqrMagnitude;
						if (d >= bestD) continue;
						if (l.recepteur.CarriesSomething()) { why = "nearest worker already carries something"; continue; }
						if (!l.recepteur.HasRoomFor(Keys[ki])) { why = "nearest worker has no room for the ward"; continue; }
						bestD = d; best = i;
					}
					if (best < 0)
					{
						left.Add(_pending[k]);
						if (_tries == 1 || Time.unscaledTime >= _deadline) BtcLog.Info("HeldWard", "ward " + ki + " not placed yet: " + why);
						continue;
					}
					used[best] = true;
					Labor w = all[best];
					Faction f = (w.affiliation != null) ? w.affiliation.faction : null;
					GameObject ward = Transactor.SpawnUNet(Keys[ki], f, w.transform.position, Quaternion.identity, null, true);
					if (ward == null) { left.Add(_pending[k]); BtcLog.Warn("HeldWard", "SpawnUNet returned null"); continue; }
					w.recepteur.AddObject(ward);
					BtcLog.Info("HeldWard", "gave ward " + ki + " to a worker " + Mathf.Sqrt(bestD).ToString("F1") + " blocks from the saved spot, carries now: " + w.recepteur.CarriesSomething());
					done++;
				}
				_pending = left.Count > 0 && Time.unscaledTime < _deadline ? left : null;
				if (_pending == null) BtcLog.Info("HeldWard", "finished, placed " + done + " this pass, " + left.Count + " left");
			}
			catch (Exception e) { _pending = null; BtcLog.Error("HeldWard", "Tick: " + e); }
		}
	}
}
