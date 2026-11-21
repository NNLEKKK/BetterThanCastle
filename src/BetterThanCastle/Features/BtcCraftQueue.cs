using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Brix.Game.Components;
using Brix.Network;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
	// Rule craft.max: more than 8 queued orders per crafting station (BC ON only).
	// Vanilla packs the queue into one ulong (CraftingState.recipes = 8 byte slots, a SyncVar), so only the first 8 orders travel the vanilla way.
	// The tail (orders 9..N) lives in Tails (key = CraftingState netId): the host fills it in CraftingState.SetRecipes, sends it to clients with
	// message 107 (station netId ':' recipe ids), CraftingLabor.QueuedRecipes appends it. BC OFF: limit stays 8, nothing is sent or registered.
	internal static class BtcCraftQueue
	{
		internal const short MsgTail = 107;
		private const int Vanilla = 8;

		private static int _idx = -2;
		private static readonly Dictionary<uint, List<int>> Tails = new Dictionary<uint, List<int>>();
		private static readonly HashSet<uint> Dirty = new HashSet<uint>();      // client: tail changed, rebuild the list; host: tail changed, send
		private static readonly FieldInfo StateField = AccessTools.Field(typeof(CraftingLabor), "state");
		private static float _nextResend;

		public static void AddRows()
		{
			BtcGameplay.Add("craft.max", "eco", "Workbench/forge: max queued orders", "Верстаки/печки: макс. число заказов в очереди", 8f, 8f, 40f, 1f, false, "Vanilla is 8. Orders above 8 are synced by an extra mod message.", "В ваниле 8. Заказы сверх 8 синхронизируются дополнительным сообщением мода.");
		}

		public static int Max()
		{
			if (!BtcNetworkGate.Enabled) return Vanilla;
			if (_idx == -2) _idx = BtcGameplay.Idx("craft.max");
			float[] e = BtcGameplay.E;
			if (_idx < 0 || e == null || _idx >= e.Length) return Vanilla;
			return Mathf.Max(Vanilla, Mathf.RoundToInt(e[_idx]));
		}

		private static uint KeyOf(CraftingLabor l)
		{
			CraftingState s = StateField.GetValue(l) as CraftingState;
			return s == null ? 0u : s.netId.Value;
		}

		// host: CraftingState.SetRecipes(list) postfix
		public static void OnSetRecipes(CraftingState s, List<RecipeInfo> recipes)
		{
			if (!NetworkServer.active || !BtcNetworkGate.Enabled) return;
			uint key = s.netId.Value;
			List<int> old;
			bool had = Tails.TryGetValue(key, out old);
			if (recipes.Count <= Vanilla)
			{
				if (had) { Tails.Remove(key); Dirty.Add(key); }
				return;
			}
			List<int> tail = had ? old : new List<int>();
			tail.Clear();
			for (int i = Vanilla; i < recipes.Count; i++) tail.Add(recipes[i].id);
			Tails[key] = tail;
			Dirty.Add(key);
		}

		// CraftingLabor.QueuedRecipes postfix: put the tail behind the 8 vanilla slots
		public static void OnQueued(CraftingLabor l, List<RecipeInfo> list)
		{
			if (!BtcNetworkGate.Enabled) return;
			uint key = KeyOf(l);
			List<int> tail;
			Tails.TryGetValue(key, out tail);
			int tailCount = tail == null ? 0 : tail.Count;
			if (NetworkServer.active)
			{
				// host: a mutated list (> 8) is trusted, a list fresh from the ulong (== 8) gets the tail back
				if (list.Count == Vanilla && tailCount > 0) Append(list, tail);
				return;
			}
			bool dirty = Dirty.Remove(key);
			if (!dirty && list.Count == Vanilla + tailCount) return;
			if (list.Count > Vanilla) list.RemoveRange(Vanilla, list.Count - Vanilla);
			if (list.Count == Vanilla && tailCount > 0) Append(list, tail);
		}

		private static void Append(List<RecipeInfo> list, List<int> tail)
		{
			for (int i = 0; i < tail.Count; i++)
			{
				try { list.Add(CookBook.GetById(tail[i])); } catch { }
			}
		}

		internal static void RegisterHandlers(NetworkClient clientOverride)
		{
			if (!BtcNetworkGate.Enabled) return;
			try
			{
				UNetManager m = NetworkManager.singleton as UNetManager;
				NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
				if (c != null)
				{
					c.UnregisterHandler(MsgTail);
					c.RegisterHandler(MsgTail, OnTailAtClient);
				}
			}
			catch (Exception e) { BtcLog.Error("CraftQueue", "RegisterHandlers: " + e); }
		}

		// every frame (BtcTimer.Tick): the host sends changed tails at once and everything every 3 s (late joiners)
		internal static void Tick()
		{
			if (!NetworkServer.active || !BtcNetworkGate.Enabled) return;
			bool all = Time.unscaledTime >= _nextResend;
			if (Dirty.Count == 0 && !all) return;
			try
			{
				StringBuilder sb = new StringBuilder();
				if (all)
				{
					_nextResend = Time.unscaledTime + 3f;
					foreach (KeyValuePair<uint, List<int>> kv in Tails) Encode(sb, kv.Key, kv.Value);
				}
				foreach (uint k in Dirty)
				{
					if (all && Tails.ContainsKey(k)) continue;
					List<int> t;
					Tails.TryGetValue(k, out t);
					Encode(sb, k, t);
				}
				Dirty.Clear();
				if (sb.Length == 0) return;
				StringMessage msg = new StringMessage(sb.ToString());
				for (int i = 0; i < NetworkServer.connections.Count; i++)
				{
					NetworkConnection c = NetworkServer.connections[i];
					if (c != null && c.isConnected && !BtcNetManager.IsLocalConnection(c)) c.Send(MsgTail, msg);
				}
			}
			catch (Exception e) { BtcLog.Error("CraftQueue", "Tick: " + e); }
		}

		private static void Encode(StringBuilder sb, uint key, List<int> tail)
		{
			sb.Append(key).Append(':');
			if (tail != null) for (int i = 0; i < tail.Count; i++) { if (i > 0) sb.Append(','); sb.Append(tail[i]); }
			sb.Append(';');
		}

		private static void OnTailAtClient(NetworkMessage nm)
		{
			try
			{
				if (NetworkServer.active || !BtcNetworkGate.Enabled) return;
				string s = nm.ReadMessage<StringMessage>().value;
				string[] recs = s.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				for (int r = 0; r < recs.Length; r++)
				{
					int c = recs[r].IndexOf(':');
					if (c <= 0) continue;
					uint key;
					if (!uint.TryParse(recs[r].Substring(0, c), out key)) continue;
					string body = recs[r].Substring(c + 1);
					List<int> cur;
					bool had = Tails.TryGetValue(key, out cur);
					List<int> tail = new List<int>();
					if (body.Length > 0)
					{
						string[] ids = body.Split(',');
						for (int i = 0; i < ids.Length; i++) { int id; if (int.TryParse(ids[i], out id)) tail.Add(id); }
					}
					bool same = had ? SameList(cur, tail) : tail.Count == 0;
					if (same) continue;
					if (tail.Count == 0) Tails.Remove(key); else Tails[key] = tail;
					Dirty.Add(key);
				}
			}
			catch (Exception e) { BtcLog.Error("CraftQueue", "OnTailAtClient: " + e); }
		}

		private static bool SameList(List<int> a, List<int> b)
		{
			if (a.Count != b.Count) return false;
			for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
			return true;
		}
	}
}
