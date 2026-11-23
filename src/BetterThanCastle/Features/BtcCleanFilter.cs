using System;
using System.Collections.Generic;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Lua;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
	// Resource filter for the Cleanup task (BC ON): toggle buttons (one per kind of loose item) in the task's bottom menu,
	// injected into the game's GameMenu.lua at load (Patches/CleanFilterPatches.cs). A disabled kind is ignored by that
	// Cleanup project: no goals for it and its workers' eaters skip it. All on by default. The host holds the state and the AI;
	// clients ask the host (Net/BtcCleanFilterNet.cs, msgs 98 request / 99 state).
	public static class BtcCleanFilter
	{
		// bit -> (en, ru, icon); the order is the button order; blue crystal is deliberately absent (always collected)
		internal static readonly int[] Bits = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024 };
		internal static readonly string[] En = { "Stone", "Brimstone", "Iron ore", "Grass", "Logs", "Planks", "Bricks", "Ingots", "Gears", "Fabric", "Rope" };
		internal static readonly string[] Ru = { "Камни", "Сера", "Железная руда", "Трава", "Брёвна", "Доски", "Кирпичи", "Слитки", "Шестерёнки", "Ткань", "Верёвки" };
		internal static readonly string[] Icons = { "_Raw_Stone", "_Raw_Orange_Crystal", "_Raw_Iron", "_Grass", "_Log", "_Plank", "_Brick", "_Iron", "_Cog", "_Fabric", "_Rope" };
		public const int All = 2047;

		internal static readonly Dictionary<long, int> Map = new Dictionary<long, int>();

		internal static long KeyOf(CleanupProject p)
		{
			NetworkIdentity ni = p.GetComponent<NetworkIdentity>();
			if (ni != null && ni.netId.Value != 0u) return (long)ni.netId.Value;
			return -(long)p.gameObject.GetInstanceID();
		}

		public static int Mask(CleanupProject p)
		{
			int m;
			return p != null && Map.TryGetValue(KeyOf(p), out m) ? m : All;
		}

		internal static void Clear() { Map.Clear(); }

		internal static void ApplyLocal(CleanupProject p, int bit)
		{
			if (p == null || bit == 0) return;
			Map[KeyOf(p)] = (Mask(p) ^ bit) & All;
		}

		// kind of a loose item (0 = not filterable, always collected)
		internal static int BitOf(GameObject item)
		{
			GameComponent gc = item.GetComponent<GameComponent>();
			string n = gc != null && gc.AssetKey != null ? gc.AssetKey.Name : null;
			if (string.IsNullOrEmpty(n)) return 0;
			if (n.IndexOf("BlueCrystal", StringComparison.OrdinalIgnoreCase) >= 0) return 0;
			if (n == "RawStone") return 1;
			if (n == "RawOrangeCrystal") return 2;
			if (n == "RawIron") return 4;
			if (n == "RawFiber" || n.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0) return 8;
			if (n.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0) return 16;
			if (n.IndexOf("Plank", StringComparison.OrdinalIgnoreCase) >= 0) return 32;
			if (n.IndexOf("Brick", StringComparison.OrdinalIgnoreCase) >= 0 || n == "RawClay" || n == "Terracotta") return 64;
			if (n.IndexOf("Ingot", StringComparison.OrdinalIgnoreCase) >= 0 || n == "RawGlass") return 128;
			if (n == "Cog") return 256;
			if (n == "Fabric") return 512;
			if (n == "Rope") return 1024;
			return 0;
		}

		public static bool Blocked(CleanupProject p, GameObject item)
		{
			if (p == null || item == null || !BtcNetworkGate.Enabled) return false;
			int mask = Mask(p);
			if (mask == All) return false;
			int bit = BitOf(item);
			return bit != 0 && (mask & bit) == 0;
		}

		// ---- Lua bridge (globals installed into every menu script) ----
		public static void Install(Script script)
		{
			if (script == null) return;
			script.Globals["__BtcCleanToggle"] = DynValue.NewCallback(LuaToggle, null);
			script.Globals["__BtcCleanGet"] = DynValue.NewCallback(LuaGet, null);
			script.Globals["__BtcCleanLabel"] = DynValue.NewCallback(LuaLabel, null);
		}

		private static CleanupProject Proj(CallbackArguments args)
		{
			try { return args.AsComponent<CleanupProject>(0, "BtcClean", true); }
			catch (Exception) { return null; }
		}

		private static DynValue LuaToggle(ScriptExecutionContext ctx, CallbackArguments args)
		{
			try
			{
				CleanupProject p = Proj(args);
				int bit = args.Count > 1 ? (int)args[1].Number : 0;
				if (p == null || bit == 0 || !BtcNetworkGate.Enabled) return DynValue.Void;
				if (NetworkServer.active)
				{
					ApplyLocal(p, bit);
					BtcCleanFilterNet.Broadcast();
				}
				else BtcCleanFilterNet.SendRequest(p, bit);
			}
			catch (Exception ex) { BtcLog.Error("CleanFilter", ex.Message); }
			return DynValue.Void;
		}

		private static DynValue LuaGet(ScriptExecutionContext ctx, CallbackArguments args)
		{
			try
			{
				CleanupProject p = Proj(args);
				int bit = args.Count > 1 ? (int)args[1].Number : 0;
				return DynValue.NewBoolean(p != null && (Mask(p) & bit) != 0);
			}
			catch (Exception) { return DynValue.False; }
		}

		private static DynValue LuaLabel(ScriptExecutionContext ctx, CallbackArguments args)
		{
			int bit = args.Count > 0 ? (int)args[0].Number : 0;
			for (int i = 0; i < Bits.Length; i++) if (Bits[i] == bit) return DynValue.NewString(BtcLang.Russian ? Ru[i] : En[i]);
			return DynValue.NewString("?");
		}

		// ---- GameMenu.lua injection ----
		private const string Marker = "--BTC cleanup filters";

		private const string LuaBlocks =
			"--BTC cleanup filters\n" +
			"do\n" +
			"local h = DividerHandle.New()\n" +
			"h.Visible = ||Data.Project:IsSelectedOfType(fy_CleanupProject)\n" +
			"h.ev_refresh = Event.New()\n" +
			"_m.onSetSelectedProject:AddListener(||h.ev_refresh:Invoke())\n" +
			"_m.dh.btcClean = h\n" +
			"end\n" +
			"do\n" +
			"local h = SectionHandle.New()\n" +
			"h.Visible = ||Data.Project:IsSelectedOfType(fy_CleanupProject)\n" +
			"h.Label = ||\"\"\n" +
			"h.LabelColor = ||CastleRed\n" +
			"h.hideHeader = true\n" +
			"h.ev_refresh = Event.New()\n" +
			"h.ev_onLoad = Event.New()\n" +
			"_m.onSetSelectedProject:AddListener(||h.ev_refresh:Invoke())\n" +
			"_m.sh.btcClean = h\n" +
			"end\n" +
			"local function btcCleanSel() return Data.Project:IsSelectedOfType(fy_CleanupProject) end\n" +
			"local function btcCleanBtn(bit, icon, key)\n" +
			"local h = MenuHandle.New()\n" +
			"h.Visible = ||btcCleanSel()\n" +
			"h.Icon = ||icon:Get64()\n" +
			"h.IconColor = ||White\n" +
			"h.IconSize = ||64\n" +
			"h.Label = ||__BtcCleanLabel(bit)\n" +
			"h.onRefreshHighlight = Event.New()\n" +
			"h.OnAction = function() if not UIGame.IsPaused() and btcCleanSel() then __BtcCleanToggle(Data.Project:GetSelected(), bit) h.onRefreshHighlight:Invoke() end end\n" +
			"h.Highlight = function() if btcCleanSel() and __BtcCleanGet(Data.Project:GetSelected(), bit) then return HighlightMode.All end return HighlightMode.None end\n" +
			"_m.sh.btcClean.ev_onLoad:AddListener(function(m, sh) m.AddMenuHandleToggle(h, sh) end)\n" +
			"_m.mg.projectContext:AddChild(h)\n" +
			"_m.mh[key] = h\n" +
			"end\n";

		public static string Inject(string code)
		{
			if (string.IsNullOrEmpty(code) || code.Contains(Marker)) return code;
			int a = code.IndexOf("local toCoroutine = function()", StringComparison.Ordinal);
			int b = code.IndexOf(".AddSection(_m.sh.harvest)", StringComparison.Ordinal);
			if (a < 0 || b < 0)
			{
				BtcLog.Error("CleanFilter", "GameMenu.lua anchors not found (toCoroutine=" + a + ", harvest section=" + b + ")");
				return code;
			}
			string tail = ".AddSection(_m.sh.harvest)\n\t\t----\n\t\t.AddDivider(_m.dh.btcClean)\n\t\t.AddSection(_m.sh.btcClean)";
			// apply the later edit first so the earlier index stays valid
			string r = code.Substring(0, b) + tail + code.Substring(b + ".AddSection(_m.sh.harvest)".Length);
			System.Text.StringBuilder sb = new System.Text.StringBuilder(LuaBlocks);
			for (int i = 0; i < Bits.Length; i++) sb.Append("btcCleanBtn(").Append(Bits[i]).Append(", IconKeys.").Append(Icons[i]).Append(", \"btcClean").Append(Bits[i]).Append("\")\n");
			sb.Append("--BTC cleanup filters end\n\n");
			r = r.Substring(0, a) + sb.ToString() + r.Substring(a);
			BtcLog.Info("CleanFilter", "cleanup filter buttons injected into GameMenu.lua");
			return r;
		}

		// ---- C9: filters survive save/load (btc-cleanfilter.txt in the save folder, "x y z mask" per Cleanup project; host only, BC ON) ----
		internal const string FileName = "btc-cleanfilter.txt";
		private static List<Vector4> _pending;
		private static float _applyAt, _deadline;

		internal static void OnSave(string folder)
		{
			if (!NetworkServer.active || !BtcNetworkGate.Enabled || string.IsNullOrEmpty(folder)) return;
			string file = System.IO.Path.Combine(folder, FileName);
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			CleanupProject[] all = UnityEngine.Object.FindObjectsOfType<CleanupProject>();
			System.Globalization.CultureInfo c = System.Globalization.CultureInfo.InvariantCulture;
			int n = 0;
			for (int i = 0; i < all.Length; i++)
			{
				int m = Mask(all[i]);
				if (m == All) continue;
				Vector3 p = all[i].transform.position;
				sb.Append(p.x.ToString("F2", c)).Append(' ').Append(p.y.ToString("F2", c)).Append(' ').Append(p.z.ToString("F2", c)).Append(' ').Append(m).Append('\n');
				n++;
			}
			if (n == 0) { if (System.IO.File.Exists(file)) System.IO.File.Delete(file); return; }
			System.IO.File.WriteAllText(file, sb.ToString());
			BtcLog.Info("CleanFilter", "saved " + n + " cleanup filter(s)");
		}

		internal static void OnLoad(string folder)
		{
			_pending = null;
			if (!NetworkServer.active || !BtcNetworkGate.Enabled || string.IsNullOrEmpty(folder)) return;
			string file = System.IO.Path.Combine(folder, FileName);
			if (!System.IO.File.Exists(file)) return;
			System.Globalization.CultureInfo c = System.Globalization.CultureInfo.InvariantCulture;
			System.Globalization.NumberStyles st = System.Globalization.NumberStyles.Float;
			List<Vector4> list = new List<Vector4>();
			string[] lines = System.IO.File.ReadAllLines(file);
			for (int i = 0; i < lines.Length; i++)
			{
				string[] t = lines[i].Split(' ');
				float x, y, z; int m;
				if (t.Length == 4 && float.TryParse(t[0], st, c, out x) && float.TryParse(t[1], st, c, out y) && float.TryParse(t[2], st, c, out z) && int.TryParse(t[3], out m))
					list.Add(new Vector4(x, y, z, m & All));
			}
			if (list.Count == 0) return;
			_pending = list;
			_applyAt = Time.unscaledTime + 2f;
			_deadline = Time.unscaledTime + 30f;
			BtcLog.Info("CleanFilter", "save has " + list.Count + " cleanup filter(s), restoring after the load");
		}

		// every frame (BtcTimer.Tick): match saved filters to the nearest Cleanup project (within 2 blocks), retry for 30 s
		internal static void Tick()
		{
			if (_pending == null || Time.unscaledTime < _applyAt) return;
			if (!NetworkServer.active) { _pending = null; return; }
			try
			{
				_applyAt = Time.unscaledTime + 1f;
				CleanupProject[] all = UnityEngine.Object.FindObjectsOfType<CleanupProject>();
				List<Vector4> left = new List<Vector4>();
				int done = 0;
				for (int k = 0; k < _pending.Count; k++)
				{
					CleanupProject best = null;
					float bestD = 2f * 2f;
					for (int i = 0; i < all.Length; i++)
					{
						if (all[i] == null) continue;
						float d = (all[i].transform.position - (Vector3)_pending[k]).sqrMagnitude;
						if (d < bestD) { bestD = d; best = all[i]; }
					}
					if (best == null) { left.Add(_pending[k]); continue; }
					Map[KeyOf(best)] = Mathf.RoundToInt(_pending[k].w) & All;
					done++;
				}
				if (done > 0) BtcCleanFilterNet.Broadcast();
				_pending = left.Count > 0 && Time.unscaledTime < _deadline ? left : null;
				if (_pending == null) BtcLog.Info("CleanFilter", "restore finished, " + done + " this pass, " + left.Count + " not found");
			}
			catch (Exception e) { _pending = null; BtcLog.Error("CleanFilter", "Tick: " + e); }
		}
	}
}
