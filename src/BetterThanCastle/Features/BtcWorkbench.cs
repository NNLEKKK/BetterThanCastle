using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Semantique;
using Brix.Network;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
	// Rule craft.better "Better Workbenches" (default ON, host rule, BC ON only):
	//  1. queue limit craft.queue (8..40, default 40) instead of the vanilla 8 at every crafting station (CraftingLabor.QueueRecipe literal).
	//  2. every crafting station takes any number of the items its type accepts: the Recepteur current capacity gets a large bonus, so leftovers,
	//     finished kit parts or extra resources never block a recipe (NeedsWork / CookRecipeNode check HasRoomFor(recipe.Creates)). Fetch amounts stay
	//     vanilla (CraftingInventory caps them at the BASE capacity), hauling leftovers to stockpiles stays vanilla.
	//  3. kit part buttons in the C# station popup (UI/GameHud/BtcGhPopupOp.cs).
	// Sync: vanilla packs only the first 8 orders into CraftingState.recipes (ulong SyncVar, also what the save keeps). The host keeps the WHOLE list of
	// every queue longer than 8 in Full (key = CraftingState netId) and sends whole lists with msg 139 on change (empty = vanilla again) and every 3 s.
	// Clients show exactly that list and fire the vanilla CraftingState signals (inventory, recipe display, Lua/C# queue UI). This replaces craft.max
	// (msg 107, tail only): its client glued a tail onto stale ulong slots, never fired the queue signals and kept tails across matches (netId reuse).
	// Saves: btc-craftqueue.txt "x y z id,id,..." in the save folder for queues longer than 8 (host).
	internal static class BtcWorkbench
	{
		internal const short MsgQueue = 139;
		internal const string FileName = "btc-craftqueue.txt";
		private const int Vanilla = 8;
		internal const int Limit = 40;
		private const int Bonus = 100000;

		private static int _iOn = -2, _iMax = -2;
		private static readonly Dictionary<uint, List<int>> Full = new Dictionary<uint, List<int>>();
		private static readonly HashSet<uint> Dirty = new HashSet<uint>();      // host: changed, send
		private static float _nextResend;
		private static bool _sceneHooked;

		private static readonly AccessTools.FieldRef<CraftingLabor, CraftingState> LaborState = AccessTools.FieldRefAccess<CraftingLabor, CraftingState>("state");
		private static readonly AccessTools.FieldRef<CraftingLabor, ulong> LaborHash = AccessTools.FieldRefAccess<CraftingLabor, ulong>("_recipesHash");
		private static readonly AccessTools.FieldRef<CraftingState, bool> StateQueueChanged = AccessTools.FieldRefAccess<CraftingState, bool>("queueChanged");
		private static readonly AccessTools.FieldRef<CraftingStation, CraftingLabor> StationLabor = AccessTools.FieldRefAccess<CraftingStation, CraftingLabor>("craftingLabor");

		public static void AddRows()
		{
			BtcGameplay.Add("craft.better", "eco", "Better Workbenches", "Улучшенные верстаки", 1f, 0f, 1f, 1f, true,
				"Workbenches, forges, the lab and every other crafting station: a longer order queue (limit below), no limit on how many items fit inside, and leftovers or finished parts never stop the workers from crafting the next order. Kit part buttons in the station panel queue a single part.",
				"Верстаки, кузницы, лаборатория и любые другие крафтильни: длинная очередь заказов (лимит ниже), нет лимита на число предметов внутри, а лишние предметы и готовые части не мешают рабочим крафтить следующий заказ. Кнопки частей набора в панели станции ставят в очередь одну часть.");
			BtcGameplay.Add("craft.queue", "eco", "Better Workbenches: max queued orders", "Улучшенные верстаки: макс. заказов в очереди", 40f, 8f, 40f, 1f, false, "Vanilla is 8.", "В ваниле 8.");
			BtcGameplay.Add("craft.chopInf", "eco", "Better Workbenches: unlimited chopping block", "Улучшенные верстаки: безлимитный пенёк", 1f, 0f, 1f, 1f, true,
				"On: the chopping block holds any number of logs and planks like the other stations. Off: exactly the vanilla chopping block capacity.",
				"Вкл: пенёк вмещает сколько угодно брёвен и досок, как остальные станции. Выкл: вместимость пенька точно как в ваниле.");
			if (!_sceneHooked)
			{
				_sceneHooked = true;
				SceneManager.sceneLoaded += delegate { Clear(); };
			}
		}

		private static float Val(ref int idx, string id, float def)
		{
			if (idx == -2) idx = BtcGameplay.Idx(id);
			float[] a = BtcGameplay.Active;
			return idx >= 0 && a != null && idx < a.Length ? a[idx] : def;
		}

		internal static bool On { get { return BtcNetworkGate.Enabled && Val(ref _iOn, "craft.better", 0f) > 0.5f; } }
		private static int _iChop = -2;
		private static bool ChopUnlimited { get { return Val(ref _iChop, "craft.chopInf", 1f) > 0.5f; } }

		/// furnace and chopping block never get a helper (user rule)
		internal static bool HelperStation(CraftingStation st)
		{
			Brix.Game.Components.GameComponent gc = st != null ? st.GetComponent<Brix.Game.Components.GameComponent>() : null;
			if (gc == null) return false;
			return gc.AssetKey != Brix.External.Factories.ObjetsDynamiques.Furnace && gc.AssetKey != Brix.External.Factories.ObjetsDynamiques.ChoppingBlock;
		}

		/// CraftingLabor.QueueRecipe: replaces the literal 8 (host).
		public static int Max()
		{
			if (!On) return Vanilla;
			return Mathf.Clamp(Mathf.RoundToInt(Val(ref _iMax, "craft.queue", Vanilla)), Vanilla, Limit);
		}

		private static void Clear() { Full.Clear(); Dirty.Clear(); Keep.Clear(); Modes.Clear(); HelpModes.Clear(); FinishedAt.Clear(); Helpers.Clear(); _racks = null; _labors = null; _pending = null; BtcKitAssemble.Clear(); }

		// ------------------------------------------------------------------ queue
		// host: CraftingState.SetRecipes(list) postfix
		public static void OnSetRecipes(CraftingState s, List<RecipeInfo> recipes)
		{
			if (s == null || !NetworkServer.active || !BtcNetworkGate.Enabled) return;
			uint key = s.netId.Value;
			if (key == 0u) return;
			List<int> cur;
			bool had = Full.TryGetValue(key, out cur);
			if (recipes == null || recipes.Count <= Vanilla)
			{
				if (had) { Full.Remove(key); Dirty.Add(key); }
				return;
			}
			if (had && Same(cur, recipes)) return;
			if (!had) cur = new List<int>(recipes.Count);
			cur.Clear();
			for (int i = 0; i < recipes.Count; i++) cur.Add(recipes[i] == null ? 0 : recipes[i].id);
			Full[key] = cur;
			Dirty.Add(key);
		}

		/// CraftingLabor.QueuedRecipes prefix: true when the vanilla getter is about to refill its list from the 8 ulong slots.
		internal static bool WillRefill(CraftingLabor l)
		{
			if (Full.Count == 0) return false;
			CraftingState s = LaborState(l);
			return s != null && LaborHash(l) != s.Hash;
		}

		/// CraftingLabor.QueuedRecipes postfix.
		public static void OnQueued(CraftingLabor l, List<RecipeInfo> list, bool refilled)
		{
			if (list == null || (Full.Count == 0 && list.Count <= Vanilla)) return;
			CraftingState s = LaborState(l);
			if (s == null) return;
			List<int> full;
			Full.TryGetValue(s.netId.Value, out full);
			if (NetworkServer.active)
			{
				// host: the list is the truth (vanilla mutates it in place), only a refill from the ulong dropped the orders past 8
				if (refilled && full != null) Fill(list, full);
				return;
			}
			// client: exactly the host's list while there is one, else the vanilla 8 slots
			if (full != null) { if (!Same(full, list)) Fill(list, full); }
			else if (list.Count > Vanilla) s.GetRecipe(list);
		}

		private static void Fill(List<RecipeInfo> list, List<int> ids)
		{
			list.Clear();
			for (int i = 0; i < ids.Count; i++)
			{
				RecipeInfo r = null;
				try { r = CookBook.GetById(ids[i]); } catch { }
				if (r != null) list.Add(r);
			}
		}

		private static bool Same(List<int> a, List<RecipeInfo> b)
		{
			if (a.Count != b.Count) return false;
			for (int i = 0; i < a.Count; i++) if (b[i] == null || b[i].id != a[i]) return false;
			return true;
		}

		private static bool Same(List<int> a, List<int> b)
		{
			if (a.Count != b.Count) return false;
			for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
			return true;
		}

		// ------------------------------------------------------------------ net
		internal static void RegisterHandlers(NetworkClient clientOverride)
		{
			if (!NetworkServer.active) { Full.Clear(); Modes.Clear(); HelpModes.Clear(); }   // a fresh client connection never keeps lists of another game
			if (!BtcNetworkGate.Enabled) return;
			try
			{
				UNetManager m = NetworkManager.singleton as UNetManager;
				NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
				if (c != null)
				{
					c.UnregisterHandler(MsgQueue);
					c.RegisterHandler(MsgQueue, OnQueueAtClient);
					c.UnregisterHandler(MsgModes);
					c.RegisterHandler(MsgModes, OnModesAtClient);
				}
				if (NetworkServer.active)
				{
					NetworkServer.UnregisterHandler(MsgModeReq);
					NetworkServer.RegisterHandler(MsgModeReq, OnModeReqAtHost);
				}
			}
			catch (Exception e) { BtcLog.Error("Workbench", "RegisterHandlers: " + e); }
		}

		// every frame (BtcTimer.Tick): the host sends changed lists at once and all lists every 3 s (late joiners, rejoin)
		internal static void Tick()
		{
			LoadTick();
			if (!NetworkServer.active || !BtcNetworkGate.Enabled) return;
			try { SendModes(); } catch (Exception e) { BtcLog.Error("Workbench", "SendModes: " + e); }
			bool all = Full.Count > 0 && Time.unscaledTime >= _nextResend;
			if (Dirty.Count == 0 && !all) return;
			try
			{
				StringBuilder sb = new StringBuilder();
				if (all)
				{
					_nextResend = Time.unscaledTime + 3f;
					foreach (KeyValuePair<uint, List<int>> kv in Full) Encode(sb, kv.Key, kv.Value);
				}
				foreach (uint k in Dirty)
				{
					if (all && Full.ContainsKey(k)) continue;
					List<int> t;
					Full.TryGetValue(k, out t);
					Encode(sb, k, t);
				}
				Dirty.Clear();
				if (sb.Length == 0) return;
				StringMessage msg = new StringMessage(sb.ToString());
				for (int i = 0; i < NetworkServer.connections.Count; i++)
				{
					NetworkConnection c = NetworkServer.connections[i];
					if (c != null && c.isConnected && !BtcNetManager.IsLocalConnection(c)) c.Send(MsgQueue, msg);
				}
			}
			catch (Exception e) { BtcLog.Error("Workbench", "Tick: " + e); }
		}

		private static void Encode(StringBuilder sb, uint key, List<int> ids)
		{
			sb.Append(key).Append(':');
			if (ids != null) for (int i = 0; i < ids.Count; i++) { if (i > 0) sb.Append(','); sb.Append(ids[i]); }
			sb.Append(';');
		}

		private static void OnQueueAtClient(NetworkMessage nm)
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
					List<int> ids = Parse(recs[r].Substring(c + 1));
					List<int> cur;
					bool had = Full.TryGetValue(key, out cur);
					if (had ? Same(cur, ids) : ids.Count == 0) continue;
					if (ids.Count == 0) Full.Remove(key); else Full[key] = ids;
					Poke(key);
				}
			}
			catch (Exception e) { BtcLog.Error("Workbench", "OnQueueAtClient: " + e); }
		}

		private static List<int> Parse(string body)
		{
			List<int> ids = new List<int>();
			if (body.Length == 0) return ids;
			string[] t = body.Split(',');
			for (int i = 0; i < t.Length; i++) { int id; if (int.TryParse(t[i], out id) && id != 0) ids.Add(id); }
			return ids;
		}

		/// client: the list changed without a SyncVar change; CraftingState.Update fires Updated + QueueChanged next frame, as RefreshRecipes does
		private static void Poke(uint key)
		{
			GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(key));
			if (go == null) return;
			CraftingState s = go.GetComponent<CraftingState>() ?? go.GetComponentInChildren<CraftingState>(true);
			if (s == null) return;
			StateQueueChanged(s) = true;
			s.enabled = true;
		}

		// ------------------------------------------------------------------ capacity
		/// Recepteur.RegenerateDescriptionsWithExclusions / Recepteur.RemoveFrom postfix: a crafting station's free room = base + Bonus - content,
		/// only for the kinds its base capacity accepts (exclusions and the single-resource rule stay vanilla). Both sides, same rule.
		internal static void Boost(Recepteur r)
		{
			if (r == null || !On || r.ExcludeAll) return;
			if (r.GetComponent<CraftingStation>() == null) return;
			Brix.Game.Components.GameComponent gc = r.GetComponent<Brix.Game.Components.GameComponent>();
			if (gc != null && gc.AssetKey == Brix.External.Factories.ObjetsDynamiques.Furnace) return;   // user rule: the furnace keeps its vanilla limit (8 ingots)
			if (gc != null && gc.AssetKey == Brix.External.Factories.ObjetsDynamiques.ChoppingBlock && !ChopUnlimited) return;   // rule craft.chopInf off: vanilla
			Description b = r.BaseCapacityWithExclusions, cur = r.CurrentCapacity, content = r.ContentDescription;
			bool single = !r.AcceptMultipleResourceType && !r.IsEmpty();
			foreach (KeyValuePair<Type, Adjectif> kv in b.DicoAdjectif)
			{
				Quantite bq = kv.Value as Quantite;
				if (bq == null || bq.valeur <= 0) continue;
				Adjectif a;
				if (!cur.DicoAdjectif.TryGetValue(kv.Key, out a)) continue;
				Quantite q = a as Quantite;
				if (q == null) continue;
				int have = content.Value(kv.Key);
				if (single && have == 0) continue;
				q.valeur = bq.valeur + Bonus - have;
			}
		}

		// ------------------------------------------------------------------ leftovers never block auto-store (host AI)
		// Vanilla CraftingInventory.ItemToPickup returns the FIRST stored item of a storable kind; when nothing accepts that one (junk without a
		// stockpile) CanStore fails and finished kit parts behind it never go to their racks. While a worker asks (CanPickUpProduction, or the
		// GetPackage(Pickup) right after its DecideWhatToDo), the pick is the first item this worker can hold AND store somewhere.
		private static Labor _ctx, _lastLabor;
		private static CraftingStation _lastStation;
		private static readonly AccessTools.FieldRef<CraftingInventory, Recepteur> InvRecepteur = AccessTools.FieldRefAccess<CraftingInventory, Recepteur>("recepteur");
		private static readonly AccessTools.FieldRef<CraftingInventory, HashSet<Brix.External.Factories.Factory.AssetKey>> InvStorable =
			AccessTools.FieldRefAccess<CraftingInventory, HashSet<Brix.External.Factories.Factory.AssetKey>>("uniqueStorable");

		internal static void AskBegin(Labor l) { _ctx = l; }
		internal static void AskEnd() { _ctx = null; }

		/// CraftingStation.DecideWhatToDo postfix: remember who gets the Pickup package next; a helper (station run by another worker) only
		/// ever gets GoFetch for the later orders or Nothing (vanilla would hand it Work/Pickup and its Hire would fire the operator)
		internal static void Decided(CraftingStation s, Labor l, ref CraftingStation.Actions a)
		{
			_helperStation = null; _helperLabor = null; _helperDesc = null;
			if (IsHelper(s, l))
			{
				Description d;
				if (HelperCan(s, l, out d)) { a = CraftingStation.Actions.GoFetch; _helperStation = s; _helperLabor = l; _helperDesc = d; }
				else a = CraftingStation.Actions.Nothing;
			}
			_lastStation = a == CraftingStation.Actions.Pickup ? s : null;
			_lastLabor = a == CraftingStation.Actions.Pickup ? l : null;
		}

		/// CraftingStation.GetPackage prefix
		internal static void PackageBegin(CraftingStation s, CraftingStation.Actions a)
		{
			if (a == CraftingStation.Actions.Pickup && s != null && s == _lastStation) _ctx = _lastLabor;
		}

		internal static void PackageEnd() { _ctx = null; _lastStation = null; _lastLabor = null; }

		/// CraftingInventory.ItemToPickup prefix: true = handled (go is the answer)
		internal static bool PickFor(CraftingInventory inv, out GameObject go)
		{
			go = null;
			Labor l = _ctx;
			if (l == null || l.IsNullOrReleased() || !NetworkServer.active || !On) return false;
			Recepteur r = InvRecepteur(inv);
			HashSet<Brix.External.Factories.Factory.AssetKey> keys = InvStorable(inv);
			if (r == null || keys == null || keys.Count == 0) return false;
			GameObject first = null;
			foreach (IDescriptor d in r.StoredItems)
			{
				GameObject o = d != null ? d.GameObject : null;
				if (o == null) continue;
				GameComponent gc = o.GetComponent<GameComponent>();
				if (gc == null || !keys.Contains(gc.AssetKey)) continue;
				if (first == null) first = o;
				if (l.recepteur.HasRoomFor(o) && l.BestRecepteurToStore(o) != null) { go = o; return true; }
			}
			go = first;   // nothing can be stored: the vanilla answer
			return true;
		}

		// ------------------------------------------------------------------ items a later order needs stay in the station
		// Non-stacking stations (workbench, lab...) only count the FIRST order as needed: everything else is "to store" and gets hauled out, even the
		// rope the next bow of a looped queue needs. With the rule, the kinds any later queued order uses are never "to store" (nor "resources to
		// remove"); what workers fetch still follows the first order only (totalRequiredIngredients is untouched).
		private static readonly Dictionary<CraftingInventory, Description> Keep = new Dictionary<CraftingInventory, Description>();
		private static readonly AccessTools.FieldRef<CraftingStation, CraftingInventory> StationInv = AccessTools.FieldRefAccess<CraftingStation, CraftingInventory>("inventory");
		private static readonly AccessTools.FieldRef<CraftingInventory, Description> InvToStore = AccessTools.FieldRefAccess<CraftingInventory, Description>("toStore");
		private static readonly AccessTools.FieldRef<CraftingInventory, Description> InvResources = AccessTools.FieldRefAccess<CraftingInventory, Description>("resources");
		private static readonly AccessTools.FieldRef<CraftingInventory, HashSet<Brix.External.Factories.Factory.AssetKey>> InvUniqueRes =
			AccessTools.FieldRefAccess<CraftingInventory, HashSet<Brix.External.Factories.Factory.AssetKey>>("uniqueResources");
		private static readonly System.Reflection.MethodInfo RefreshAmountsM = AccessTools.Method(typeof(CraftingInventory), "RefreshAmounts");

		/// CraftingStation.RefeshInventory postfix (queue changed): ingredients of orders 2..N
		internal static void OnStationInventory(CraftingStation st)
		{
			CraftingInventory inv = st != null ? StationInv(st) : null;
			if (inv == null) return;
			Description d = null;
			if (On)
			{
				List<RecipeInfo> q = st.QueuedRecipes;
				RecipeInfo active = q != null && q.Count > 0 ? q[0] : null;
				// host: the rack limit may craft a later order first; fetching follows that order (non-stacking stations count one order)
				if (NetworkServer.active && active != null && !Stacks(st))
				{
					RecipeInfo a = ActiveOf(st);
					if (a != active)
					{
						active = a;
						if (a == null) inv.Clear(); else inv.SetRecipe(a);
					}
				}
				if (q != null && q.Count > 1)
				{
					d = new Description();
					bool skipped = false;
					for (int i = 0; i < q.Count; i++)
					{
						if (q[i] == null) continue;
						if (!skipped && q[i] == active) { skipped = true; continue; }
						q[i].AddTo(d);
					}
					if (!d.Any()) d = null;
				}
			}
			if (d == null) { if (!Keep.Remove(inv)) return; }
			else Keep[inv] = d;
			RefreshAmountsM.Invoke(inv, null);
		}

		/// CraftingInventory.RefreshAmounts postfix: same operations as vanilla, with the later orders' kinds taken out of "to store"
		internal static void AfterAmounts(CraftingInventory inv)
		{
			if (Keep.Count == 0) return;
			Description keep;
			if (!Keep.TryGetValue(inv, out keep)) return;
			Recepteur r = InvRecepteur(inv);
			Description toStore = InvToStore(inv), res = InvResources(inv);
			if (r == null || toStore == null || res == null) return;
			toStore.ExceptWith(keep).RemoveZeros();
			res.Clear();
			res.AddAllFrom(r.ContentDescription);
			res.ExceptWith(toStore).RemoveZeros().RessourceOnly();
			HashSet<Brix.External.Factories.Factory.AssetKey> us = InvStorable(inv), ur = InvUniqueRes(inv);
			us.Clear(); toStore.UniqueRepresentatives(ref us); InvStorable(inv) = us;
			ur.Clear(); res.UniqueRepresentatives(ref ur); InvUniqueRes(inv) = ur;
		}

		// ------------------------------------------------------------------ builders take products straight from stations (host AI)
		// An empty-handed builder is offered only blueprints whose material is in its region list (BuildGoalProvider.FulfillableEnumerator ->
		// KnowledgeRegion.UniqueResources). That list opens stockpiles only: a furnace counts as "Furnace", never as its ingots, so without a
		// stockpile builders never fetched ingots, although Labor.Find already accepts station products (crafting does it). With the rule the
		// products a station would haul out (uniqueStorable it really holds, the same test as Knowledge.NotInCraftingInventory) join the list.
		private static readonly AccessTools.FieldRef<Brix.Game.AI.KnowledgeSpace.KnowledgeRegion, Dictionary<Brix.External.Factories.Factory.AssetKey, List<Brix.Game.Components.Volumes.IVolume>>> RegionByKey =
			AccessTools.FieldRefAccess<Brix.Game.AI.KnowledgeSpace.KnowledgeRegion, Dictionary<Brix.External.Factories.Factory.AssetKey, List<Brix.Game.Components.Volumes.IVolume>>>("_resourcesByAssetKey");
		private static readonly Dictionary<Brix.External.Factories.Factory.AssetKey, bool> StationKeys = new Dictionary<Brix.External.Factories.Factory.AssetKey, bool>();
		private static readonly HashSet<Brix.External.Factories.Factory.AssetKey> HeldKeys = new HashSet<Brix.External.Factories.Factory.AssetKey>();

		/// KnowledgeRegion.UniqueResources postfix
		internal static void AfterRegionResources(Brix.Game.AI.KnowledgeSpace.KnowledgeRegion region, HashSet<Brix.External.Factories.Factory.AssetKey> items)
		{
			if (items == null || !On) return;
			Dictionary<Brix.External.Factories.Factory.AssetKey, List<Brix.Game.Components.Volumes.IVolume>> byKey = RegionByKey(region);
			if (byKey == null) return;
			foreach (KeyValuePair<Brix.External.Factories.Factory.AssetKey, List<Brix.Game.Components.Volumes.IVolume>> kv in byKey)
			{
				if (!IsStationKey(kv.Key) || kv.Value == null) continue;
				for (int i = 0; i < kv.Value.Count; i++)
				{
					IGameComponent gc = Brix.Game.Components.Volumes.IVolumeExtender.AsComponent(kv.Value[i]);
					if (gc == null || gc.IsNullOrReleased() || gc.recepteur == null) continue;
					CraftingStation st = gc.GameObject.GetComponent<CraftingStation>();
					CraftingInventory inv = st != null ? StationInv(st) : null;
					HashSet<Brix.External.Factories.Factory.AssetKey> storable = inv != null ? InvStorable(inv) : null;
					if (storable == null || storable.Count == 0) continue;
					HeldKeys.Clear();
					gc.recepteur.UniqueStoredItemsKeys(HeldKeys);
					foreach (Brix.External.Factories.Factory.AssetKey k in HeldKeys) if (storable.Contains(k)) items.Add(k);
				}
			}
			HeldKeys.Clear();
		}

		// ------------------------------------------------------------------ every station is a pickup source (host AI)
		// Knowledge.ClosestObjectToPickup only looks at objects whose key is in Knowledge.InterestingResourceObject: stockpiles, racks, furnace and
		// chopping block. A rope made in the loom was invisible to the workbench that needs it. Station keys join the set when a region indexes
		// them (KnowledgeRegion.AddUniqueResource prefix); with the rule off the added keys are refused again in the pickup test.
		private static readonly HashSet<Brix.External.Factories.Factory.AssetKey> AddedKeys = new HashSet<Brix.External.Factories.Factory.AssetKey>();

		/// KnowledgeRegion.AddUniqueResource prefix
		internal static void BeforeRegionAdd(Brix.Game.Components.Volumes.IVolume v)
		{
			Brix.External.Factories.Factory.AssetKey k = v != null ? v.AssetKey : null;
			if (k == null || !IsStationKey(k) || Brix.Game.AI.KnowledgeSpace.Knowledge.InterestingResourceObject.Contains(k)) return;
			Brix.Game.AI.KnowledgeSpace.Knowledge.InterestingResourceObject.Add(k);
			AddedKeys.Add(k);
		}

		/// Knowledge._ClosestObjectToPickup_Test postfix: false = refuse
		internal static bool PickupAllowed(IGameComponent gc)
		{
			if (AddedKeys.Count == 0 || On) return true;
			return gc == null || gc.AssetKey == null || !AddedKeys.Contains(gc.AssetKey);
		}

		// ------------------------------------------------------------------ no stockpile: leftovers go to a station (host AI)
		// Vanilla stores only in stockpiles (Knowledge.ClosestStorageWithRoomFor). With the rule and no stockpile an item a worker carries (leftover
		// ingots of a blueprint) goes to the nearest station whose queue needs it, else back to a station that makes or holds that kind (the
		// furnace). Items lying in a station are NOT pushed out (that sent every free worker to empty one station, user report): the operator and
		// helper of the station that needs them pull them like from a stockpile (station keys are pickup sources). Never used for prefab checks.
		internal static IGameComponent StoreFallback(Labor l, IGameComponent item, Brix.Game.Faction faction)
		{
			if (!On || l == null || item == null || item.IsNullOrReleased() || item.apparence == null) return null;
			Ressource res = item.apparence.Principale;
			GameObject go = item.GameObject;
			if (res == null || go == null) return null;
			if (l.recepteur == null || !l.recepteur.Carries(go)) return null;   // only what the worker holds
			Brix.External.Factories.Factory.AssetKey key = Description.RepresentativeOf(item) ?? item.AssetKey;
			Type kind = res.GetType();
			Lists();
			if (_stations == null) return null;
			CraftingStation need = null, make = null;
			float needD = float.MaxValue, makeD = float.MaxValue;
			Vector3 p = l.transform.position;
			for (int i = 0; i < _stations.Length; i++)
			{
				CraftingStation st = _stations[i];
				if (st == null || st.IsNullOrReleased() || st.recepteur == null || !ReferenceEquals(st.faction, faction)) continue;
				CraftingInventory inv = StationInv(st);
				if (inv == null) continue;
				float d = (st.transform.position - p).sqrMagnitude;
				Description keep;
				bool needs = inv.StuffToFecth().Value(kind) > 0 || (Keep.TryGetValue(inv, out keep) && keep.Value(kind) > 0 && st.recepteur.ContentDescription.Value(kind) < keep.Value(kind));
				if (needs)
				{
					if (d < needD && st.recepteur.HasRoomFor(go) && Brix.Game.AI.KnowledgeSpace.Knowledge.Instance.CanReach(l, st, -1)) { need = st; needD = d; }
				}
				else if (d < makeD && Makes(st, inv, key) && st.recepteur.HasRoomFor(go) && Brix.Game.AI.KnowledgeSpace.Knowledge.Instance.CanReach(l, st, -1)) { make = st; makeD = d; }
			}
			return need ?? make;
		}

		private static bool Makes(CraftingStation st, CraftingInventory inv, Brix.External.Factories.Factory.AssetKey key)
		{
			if (key == null) return false;
			if (inv.HasProductsOfType(key)) return true;
			List<RecipeInfo> q = st.QueuedRecipes;
			if (q != null) for (int i = 0; i < q.Count; i++) if (q[i] != null && q[i].Creates == key) return true;
			return false;
		}

		private static bool IsStationKey(Brix.External.Factories.Factory.AssetKey key)
		{
			bool v;
			if (key == null) return false;
			if (StationKeys.TryGetValue(key, out v)) return v;
			GameObject p = Brix.External.Factories.Factory.Peek(key);
			v = p != null && p.GetComponent<CraftingStation>() != null;
			StationKeys[key] = v;
			return v;
		}

		// ------------------------------------------------------------------ rack limit (per station mode, host AI)
		// With the limit, a station does not START a kit part when its faction's racks (rack filters included: they are vanilla exclusions, so they
		// show in the rack's current capacity) have no free slot for it after counting the same finished parts still lying in its crafting stations
		// and the same part being crafted right now elsewhere. Such orders keep their place in the queue; the first order that can be crafted goes
		// first (two lambdas of CraftingLabor.ProcessQueue: the "anything to pop" condition and the pop itself). An order already started finishes.
		// Mode: explicit per station (button in the station panel, msg 140 client->host "key=1", msg 141 host->clients "key:1;..."), otherwise the
		// owner's Orders default craft_rack_limit (BtcPlayerPrefs "rack", default off = vanilla: craft without limit).
		internal const short MsgModeReq = 140, MsgModes = 141;
		private static readonly Dictionary<uint, bool> Modes = new Dictionary<uint, bool>();       // rack limit (explicit)
		private static readonly Dictionary<uint, bool> HelpModes = new Dictionary<uint, bool>();   // helper (explicit), wire prefix "h"
		private static bool _modesDirty;
		private static float _nextModes;
		private static ToolRack[] _racks;
		internal static ToolRack[] Racks() { Lists(); return _racks; }
		private static CraftingStation[] _stations;
		private static float _listsAt;
		private static readonly Dictionary<RecipeInfo, Ressource> PartAdj = new Dictionary<RecipeInfo, Ressource>();
		private static readonly System.Reflection.MethodInfo StacksGetter = AccessTools.PropertyGetter(typeof(CraftingStation), "CanStackOrders");
		private static readonly System.Reflection.MethodInfo CurrentSetter = AccessTools.PropertySetter(typeof(CraftingLabor), "CurrentRecipe");

		private static bool Stacks(CraftingStation st)
		{
			try { return StacksGetter != null && (bool)StacksGetter.Invoke(st, null); } catch { return false; }
		}

		internal static uint KeyOf(CraftingStation st)
		{
			CraftingLabor l = st != null ? StationLabor(st) : null;
			CraftingState s = l != null ? LaborState(l) : null;
			return s != null ? s.netId.Value : 0u;
		}

		/// effective mode of a station (host: owner's default when unset; client: own default, used only for the button)
		internal static bool LimitOn(CraftingStation st)
		{
			if (st == null || !On) return false;
			bool v;
			if (Modes.TryGetValue(KeyOf(st), out v)) return v;
			return NetworkServer.active ? BtcPlayerPrefs.For(st.faction).RackLimit : BtcPlayerPrefs.LocalRackLimit;
		}

		/// helper mode of a station (one extra worker brings what later orders need), same rules as LimitOn; owner default craft_helpers (on)
		internal static bool HelpOn(CraftingStation st)
		{
			if (st == null || !On) return false;
			bool v;
			if (HelpModes.TryGetValue(KeyOf(st), out v)) return v;
			return NetworkServer.active ? BtcPlayerPrefs.For(st.faction).Helpers : BtcPlayerPrefs.LocalHelpers;
		}

		/// station panel buttons
		internal static void ToggleLimit(CraftingStation st) { Toggle(st, false); }
		internal static void ToggleHelp(CraftingStation st) { if (HelperStation(st)) Toggle(st, true); }

		private static void Toggle(CraftingStation st, bool help)
		{
			uint key = KeyOf(st);
			if (key == 0u || !On) return;
			bool v = help ? !HelpOn(st) : !LimitOn(st);
			if (NetworkServer.active) { SetMode(key, v, help); return; }
			(help ? HelpModes : Modes)[key] = v;   // shown at once, the host's state follows
			try
			{
				UNetManager m = NetworkManager.singleton as UNetManager;
				if (m != null && m.LocalNetworkClient != null) m.LocalNetworkClient.Send(MsgModeReq, new StringMessage((help ? "h" : "") + key + "=" + (v ? 1 : 0)));
			}
			catch (Exception e) { BtcLog.Error("Workbench", "Toggle: " + e.Message); }
		}

		private static void SetMode(uint key, bool v) { SetMode(key, v, false); }

		/// host: explicit mode of a station (blueprint presets, BtcBpPreset)
		internal static void SetModeHost(CraftingStation st, bool help, bool v)
		{
			uint key = KeyOf(st);
			if (key != 0u && On && NetworkServer.active) SetMode(key, v, help);
		}

		private static void SetMode(uint key, bool v, bool help)
		{
			Dictionary<uint, bool> map = help ? HelpModes : Modes;
			bool old;
			if (map.TryGetValue(key, out old) && old == v) return;
			map[key] = v;
			_modesDirty = true;
			BtcLog.Info("Workbench", "station " + key + (help ? " helper " : " rack limit ") + (v ? "on" : "off"));
			GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(key));
			CraftingStation st = go != null ? go.GetComponent<CraftingStation>() : null;
			if (st != null) OnStationInventory(st);
		}

		private static void OnModeReqAtHost(NetworkMessage nm)
		{
			try
			{
				if (!BtcNetworkGate.Enabled || nm.conn == null) return;
				string s = nm.ReadMessage<StringMessage>().value ?? "";
				bool help = s.StartsWith("h");
				string[] kv = (help ? s.Substring(1) : s).Split('=');
				uint key; int v;
				if (kv.Length != 2 || !uint.TryParse(kv[0], out key) || !int.TryParse(kv[1], out v)) return;
				NetworkIdentity ni;
				if (!NetworkServer.objects.TryGetValue(new NetworkInstanceId(key), out ni) || ni == null) return;
				Brix.Game.User u = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(nm.conn) : null;
				if (u == null || !ReferenceEquals(BtcPlayerPrefs.FactionOf(ni.gameObject), u.faction)) return;   // own stations only
				SetMode(key, v != 0, help);
			}
			catch (Exception e) { BtcLog.Error("Workbench", "OnModeReqAtHost: " + e); }
		}

		private static void OnModesAtClient(NetworkMessage nm)
		{
			try
			{
				if (NetworkServer.active || !BtcNetworkGate.Enabled) return;
				string[] recs = (nm.ReadMessage<StringMessage>().value ?? "").Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				Modes.Clear();
				HelpModes.Clear();
				for (int i = 0; i < recs.Length; i++)
				{
					bool help = recs[i].StartsWith("h");
					string[] kv = (help ? recs[i].Substring(1) : recs[i]).Split(':');
					uint key; int v;
					if (kv.Length == 2 && uint.TryParse(kv[0], out key) && int.TryParse(kv[1], out v)) (help ? HelpModes : Modes)[key] = v != 0;
				}
			}
			catch (Exception e) { BtcLog.Error("Workbench", "OnModesAtClient: " + e); }
		}

		private static void SendModes()
		{
			bool due = Modes.Count + HelpModes.Count > 0 && Time.unscaledTime >= _nextModes;
			if (!_modesDirty && !due) return;
			_modesDirty = false;
			_nextModes = Time.unscaledTime + 3f;
			StringBuilder sb = new StringBuilder();
			foreach (KeyValuePair<uint, bool> kv in Modes) sb.Append(kv.Key).Append(':').Append(kv.Value ? 1 : 0).Append(';');
			foreach (KeyValuePair<uint, bool> kv in HelpModes) sb.Append('h').Append(kv.Key).Append(':').Append(kv.Value ? 1 : 0).Append(';');
			StringMessage msg = new StringMessage(sb.ToString());
			for (int i = 0; i < NetworkServer.connections.Count; i++)
			{
				NetworkConnection c = NetworkServer.connections[i];
				if (c != null && c.isConnected && !BtcNetManager.IsLocalConnection(c)) c.Send(MsgModes, msg);
			}
		}

		/// rack item made by a recipe, or null (not a kit part)
		private static Ressource AdjOf(RecipeInfo r)
		{
			if (r == null) return null;
			Ressource a;
			if (PartAdj.TryGetValue(r, out a)) return a;
			try
			{
				a = Apparence.PrincipaleOf(Brix.External.Factories.Factory.Peek(r.Creates));
				if (a != null && !ToolRack.Accepts(a)) a = null;
			}
			catch { a = null; }
			PartAdj[r] = a;
			return a;
		}

		private static void Lists()
		{
			if (_racks != null && Time.time < _listsAt) return;
			// registries, not scene walks: three FindObjectsOfType every 2 s were frame spikes in big matches
			_racks = BtcRegistry.RacksArray();
			_stations = BtcRegistry.StationsArray();
			_labors = BtcRegistry.LaborsArray();
			_listsAt = Time.time + 2f;
		}

		private static Labor[] _labors;
		private static readonly Dictionary<CraftingStation, float> FinishedAt = new Dictionary<CraftingStation, float>();
		private const float Settle = 1.5f;

		/// CraftingLabor.FinishRecipe postfix: the made item reaches the station's content a moment later (spawn + parent tracking)
		internal static void OnFinished(CraftingLabor l)
		{
			if (!NetworkServer.active || !On) return;
			CraftingStation st = StationOf(l);
			if (st != null) FinishedAt[st] = Time.time;
		}

		/// a kit part with no free rack slot of its faction left: rack room - same parts in crafting stations - same parts carried by own units
		/// (picked up on the way to a rack) - the same part being crafted elsewhere; right after a finish the station waits until the item landed
		private static bool Blocked(CraftingStation st, RecipeInfo r)
		{
			Ressource a = AdjOf(r);
			if (a == null) return false;
			float fin;
			if (FinishedAt.TryGetValue(st, out fin) && Time.time - fin < Settle) return true;
			Lists();
			object f = st.faction;
			int room = 0, inStations = 0, crafting = 0, carried = 0;
			for (int i = 0; i < _racks.Length; i++)
			{
				ToolRack k = _racks[i];
				if (k == null || k.IsNullOrReleased() || !ReferenceEquals(k.faction, f) || k.recepteur == null) continue;
				room += Mathf.Max(0, k.recepteur.CurrentCapacity.Value(a));
			}
			for (int i = 0; i < _stations.Length; i++)
			{
				CraftingStation s = _stations[i];
				if (s == null || s.IsNullOrReleased() || !ReferenceEquals(s.faction, f)) continue;
				if (s.recepteur != null) inStations += s.recepteur.ContentDescription.Value(a);
				Recipe cur = s != st ? s.CurrentRecipe : null;
				if (cur != null && cur.Info == r) crafting++;
			}
			for (int i = 0; i < _labors.Length; i++)
			{
				Labor u = _labors[i];
				if (u == null || u.IsNullOrReleased() || !ReferenceEquals(u.faction, f) || u.recepteur == null) continue;
				carried += u.recepteur.ContentDescription.Value(a);
			}
			bool blocked = room - inStations - crafting - carried <= 0;
			long lk = ((long)st.GetInstanceID() << 16) ^ r.id;
			bool was;
			if (!LastBlocked.TryGetValue(lk, out was) || was != blocked)
			{
				LastBlocked[lk] = blocked;
				BtcLog.Info("Workbench", "rack limit " + (blocked ? "blocks" : "allows") + " recipe " + r.id + " at station " + KeyOf(st)
					+ ": room " + room + ", in stations " + inStations + ", crafting elsewhere " + crafting + ", carried " + carried);
			}
			return blocked;
		}

		private static readonly Dictionary<long, bool> LastBlocked = new Dictionary<long, bool>();

		/// the order this station should craft now: the current one, else the first one not blocked by the rack limit
		private static RecipeInfo ActiveOf(CraftingStation st)
		{
			CraftingLabor l = StationLabor(st);
			if (l == null) return null;
			Recipe cur = l.CurrentRecipe;
			if (cur != null) return cur.Info;
			return Choose(st, l);
		}

		private static RecipeInfo Choose(CraftingStation st, CraftingLabor l)
		{
			List<RecipeInfo> q = l.QueuedRecipes;
			if (q.Count == 0) return null;
			if (!LimitOn(st)) return q[0];
			for (int i = 0; i < q.Count; i++) if (q[i] != null && !Blocked(st, q[i])) return q[i];
			return null;
		}

		private static CraftingStation StationOf(CraftingLabor l)
		{
			return l == null ? null : (l.GetComponent<CraftingStation>() ?? l.GetComponentInParent<CraftingStation>());
		}

		/// ProcessQueue condition lambda postfix: nothing to pop when every queued order is blocked
		internal static bool CanPop(CraftingLabor l)
		{
			if (!NetworkServer.active || !On) return true;
			CraftingStation st = StationOf(l);
			return st == null || !LimitOn(st) || Choose(st, l) != null;
		}

		/// ProcessQueue pop lambda prefix: true = handled (a later order became the current recipe)
		internal static bool Pop(CraftingLabor l)
		{
			if (!NetworkServer.active || !On || CurrentSetter == null) return false;
			CraftingStation st = StationOf(l);
			if (st == null || !LimitOn(st)) return false;
			RecipeInfo c = Choose(st, l);
			List<RecipeInfo> q = l.QueuedRecipes;
			if (c == null || q.Count == 0 || c == q[0]) return false;
			CurrentSetter.Invoke(l, new object[] { c.GetRecipe() });
			OnStationInventory(st);
			return true;
		}

		// ------------------------------------------------------------------ helpers bring what later orders need (host AI)
		// A station has one operator (Hire fires the previous one). While it works, ONE other worker at a time may bring what the whole queue still
		// needs (ingredients of every queued order, capped per resource like vanilla fetching, minus what is inside), e.g. the rope for the next bow
		// of a looped bow + quiver while the quiver is made. The helper's package has no Hire, so the operator stays. The operator itself still
		// fetches for its current order only. Only ONE helper per station (user rule) and it is the SAME worker for the whole job (user rule
		// 2026-11-28: no taking turns): the slot belongs to that helper across deliveries and is freed only when it is gone, 90 s passed since its
		// last fetch, or it has delivered and taken no new fetch for HelperIdle s (nothing left to bring, or it went to other work).
		private sealed class Helper { internal Labor L; internal float Until; internal bool Carried; internal float EmptyAt; }

		private static bool Busy(Helper h)
		{
			if (h == null || h.L == null || h.L.IsNullOrReleased() || Time.time >= h.Until) return false;
			bool carrying = h.L.recepteur != null && !h.L.recepteur.IsEmpty();
			if (carrying) { h.Carried = true; h.EmptyAt = -1f; return true; }
			if (!h.Carried) return true;   // still walking to its pick-up
			if (h.EmptyAt < 0f) h.EmptyAt = Time.time;
			return Time.time - h.EmptyAt < HelperIdle;
		}
		private const float HelperIdle = 10f;
		private static readonly Dictionary<CraftingStation, Helper> Helpers = new Dictionary<CraftingStation, Helper>();
		private static CraftingStation _helperStation;
		private static Labor _helperLabor;
		private static Description _helperDesc;
		private const float HelperSlot = 90f;
		private static readonly AccessTools.FieldRef<CraftingInventory, int> InvCap = AccessTools.FieldRefAccess<CraftingInventory, int>("_resourceCap");
		private static readonly AccessTools.FieldRef<CraftingInstructions, Brix.Game.AI.InstructionType.LaborInstruction<Description>> FindInstr =
			AccessTools.FieldRefAccess<CraftingInstructions, Brix.Game.AI.InstructionType.LaborInstruction<Description>>("FindIngredients");

		private static bool IsHelper(CraftingStation st, Labor l)
		{
			if (st == null || l == null || !NetworkServer.active || !On) return false;
			Labor op = st.Operator;
			return op != null && !op.IsNullOrReleased() && op != l && HelperStation(st) && HelpOn(st);
		}

		/// what the whole queue still needs inside the station, capped per resource like vanilla, or null
		private static Description PrefetchFor(CraftingStation st)
		{
			CraftingInventory inv = StationInv(st);
			List<RecipeInfo> q = st.QueuedRecipes;
			if (inv == null || q == null || q.Count == 0 || st.recepteur == null) return null;
			Description all = new Description();
			for (int i = 0; i < q.Count; i++) if (q[i] != null) q[i].AddTo(all);
			Description need = all.CapAt(InvCap(inv)) - st.recepteur.ContentDescription;
			if (need == null) return null;
			need.RemoveZeros();
			return need.Any() ? need : null;
		}

		private static bool HelperCan(CraftingStation st, Labor l, out Description d)
		{
			d = null;
			if (!IsHelper(st, l)) return false;
			Helper h;
			if (Helpers.TryGetValue(st, out h) && h.L != l && Busy(h)) return false;   // only one helper per station
			// HasWorkFor runs on every decision of every crew worker: a failed reach/world search is not repeated for a while (frame spikes)
			long key = ((long)st.GetInstanceID() << 32) ^ (uint)l.GetInstanceID();
			float until, now = Time.time;
			if (Misses.TryGetValue(key, out until) && now < until) return false;
			if (Misses.Count > 512) Misses.Clear();
			if (!Brix.Game.AI.KnowledgeSpace.Knowledge.Instance.CanReach(l, st, -1)) { Misses[key] = now + MissHold; return false; }
			d = PrefetchFor(st);
			if (d == null) { Misses[key] = now + MissHold; return false; }
			GameObject o = l.Find(d);
			if (o == null && now >= _helperNoneLogAt)
			{
				_helperNoneLogAt = now + 10f;
				BtcLog.Info("Workbench", "helper finds nothing for " + st.name + " needs " + d);
			}
			bool ok = o != null && l.recepteur.HasRoomFor(o);
			if (!ok) Misses[key] = now + MissHold;
			return ok;
		}
		private static float _helperNoneLogAt;
		private const float MissHold = 2f;
		private static readonly Dictionary<long, float> Misses = new Dictionary<long, float>();

		/// CraftingStation.HasWorkFor postfix: false only because another worker operates the station -> a helper may bring later ingredients
		internal static bool HelperHasWork(CraftingStation st, Labor l)
		{
			Description d;
			return HelperCan(st, l, out d);
		}

		/// CraftingStation.GetPackage prefix: the helper's fetch package (no Hire); null = vanilla
		internal static OrderPackage<Labor> HelperPackage(CraftingStation st, CraftingStation.Actions a)
		{
			if (a != CraftingStation.Actions.GoFetch || st == null || st != _helperStation || _helperLabor == null || _helperDesc == null) return null;
			CraftingLabor cl = StationLabor(st);
			Brix.Game.AI.InstructionType.LaborInstruction<Description> fi = cl != null && cl.instructions != null ? FindInstr(cl.instructions) : null;
			if (fi == null) return null;
			Helpers[st] = new Helper { L = _helperLabor, Until = Time.time + HelperSlot, EmptyAt = -1f };
#if DEV
			BtcLog.Info("Workbench", "helper " + _helperLabor.name + " fetches " + _helperDesc + " for " + st.name);
#endif
			OrderPackage<Labor> p = Order.Package.OfDescription<Labor>(fi, _helperDesc, Labor.DecisionSource.Project);
			_helperStation = null; _helperLabor = null; _helperDesc = null;
			return p;
		}

		// ------------------------------------------------------------------ saves (host)
		private sealed class Pend { internal Vector3 P; internal List<int> Ids; internal int Mode = -1; internal bool Help; }
		private static List<Pend> _pending;
		private static float _applyAt, _deadline;

		internal static void OnSave(string folder)
		{
			if (!NetworkServer.active || !BtcNetworkGate.Enabled || string.IsNullOrEmpty(folder)) return;
			string file = System.IO.Path.Combine(folder, FileName);
			CultureInfo c = CultureInfo.InvariantCulture;
			StringBuilder sb = new StringBuilder();
			int n = 0;
			foreach (KeyValuePair<uint, List<int>> kv in Full)
			{
				NetworkIdentity ni;
				if (!NetworkServer.objects.TryGetValue(new NetworkInstanceId(kv.Key), out ni) || ni == null) continue;
				Vector3 p = ni.transform.position;
				sb.Append(p.x.ToString("F2", c)).Append(' ').Append(p.y.ToString("F2", c)).Append(' ').Append(p.z.ToString("F2", c)).Append(' ');
				for (int i = 0; i < kv.Value.Count; i++) { if (i > 0) sb.Append(','); sb.Append(kv.Value[i]); }
				sb.Append('\n');
				n++;
			}
			int m = 0;
			for (int pass = 0; pass < 2; pass++)
			foreach (KeyValuePair<uint, bool> kv in pass == 0 ? Modes : HelpModes)   // "R x y z 1" rack limit / "H x y z 1" helper, explicit modes
			{
				NetworkIdentity ni;
				if (!NetworkServer.objects.TryGetValue(new NetworkInstanceId(kv.Key), out ni) || ni == null) continue;
				Vector3 p = ni.transform.position;
				sb.Append(pass == 0 ? "R " : "H ").Append(p.x.ToString("F2", c)).Append(' ').Append(p.y.ToString("F2", c)).Append(' ').Append(p.z.ToString("F2", c)).Append(' ').Append(kv.Value ? 1 : 0).Append('\n');
				m++;
			}
			if (n + m == 0) { if (File.Exists(file)) File.Delete(file); return; }
			File.WriteAllText(file, sb.ToString());
			BtcLog.Info("Workbench", "saved " + n + " long queue(s), " + m + " station mode(s)");
		}

		internal static void OnLoad(string folder)
		{
			_pending = null;
			if (!NetworkServer.active || !BtcNetworkGate.Enabled || string.IsNullOrEmpty(folder)) return;
			string file = System.IO.Path.Combine(folder, FileName);
			if (!File.Exists(file)) return;
			CultureInfo c = CultureInfo.InvariantCulture;
			List<Pend> list = new List<Pend>();
			string[] lines = File.ReadAllLines(file);
			for (int i = 0; i < lines.Length; i++)
			{
				string[] t = lines[i].Split(' ');
				float x, y, z;
				int v;
				if (t.Length == 5 && (t[0] == "R" || t[0] == "H") && float.TryParse(t[1], NumberStyles.Float, c, out x) && float.TryParse(t[2], NumberStyles.Float, c, out y)
					&& float.TryParse(t[3], NumberStyles.Float, c, out z) && int.TryParse(t[4], out v))
					list.Add(new Pend { P = new Vector3(x, y, z), Mode = v != 0 ? 1 : 0, Help = t[0] == "H" });
				else if (t.Length == 4 && float.TryParse(t[0], NumberStyles.Float, c, out x) && float.TryParse(t[1], NumberStyles.Float, c, out y)
					&& float.TryParse(t[2], NumberStyles.Float, c, out z))
				{
					List<int> ids = Parse(t[3]);
					if (ids.Count > Vanilla) list.Add(new Pend { P = new Vector3(x, y, z), Ids = ids });
				}
			}
			if (list.Count == 0) return;
			_pending = list;
			_applyAt = Time.unscaledTime + 2f;
			_deadline = Time.unscaledTime + 30f;
			BtcLog.Info("Workbench", "save has " + list.Count + " long queue(s), restoring after the load");
		}

		/// match saved queues to the nearest station (within 1.5 blocks) whose first 8 orders still equal the saved ones, retry for 30 s
		private static void LoadTick()
		{
			if (_pending == null || Time.unscaledTime < _applyAt) return;
			if (!NetworkServer.active || !On) { _pending = null; return; }
			try
			{
				_applyAt = Time.unscaledTime + 1f;
				CraftingStation[] all = BtcRegistry.StationsArray();
				List<Pend> left = new List<Pend>();
				int done = 0;
				for (int k = 0; k < _pending.Count; k++)
				{
					CraftingStation best = null;
					float bestD = 1.5f * 1.5f;
					for (int i = 0; i < all.Length; i++)
					{
						float d = (all[i].transform.position - _pending[k].P).sqrMagnitude;
						if (d < bestD) { bestD = d; best = all[i]; }
					}
					CraftingLabor l = best != null ? StationLabor(best) : null;
					CraftingState s = l != null ? LaborState(l) : null;
					if (s == null || s.netId.Value == 0u) { left.Add(_pending[k]); continue; }
					if (_pending[k].Mode >= 0) { SetMode(s.netId.Value, _pending[k].Mode == 1, _pending[k].Help); done++; continue; }
					List<RecipeInfo> q = l.QueuedRecipes;
					List<int> ids = _pending[k].Ids;
					bool match = q.Count == Vanilla;
					for (int i = 0; match && i < Vanilla; i++) match = q[i] != null && q[i].id == ids[i];
					if (!match) continue;   // the player changed this queue already
					Fill(q, ids);
					s.SetRecipes(q);
					done++;
				}
				_pending = left.Count > 0 && Time.unscaledTime < _deadline ? left : null;
				if (_pending == null) BtcLog.Info("Workbench", "restore finished, " + done + " this pass, " + left.Count + " not found");
			}
			catch (Exception e) { _pending = null; BtcLog.Error("Workbench", "LoadTick: " + e); }
		}
	}
}
