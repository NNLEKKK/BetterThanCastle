using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Game.Components.Volumes;
using Brix.Game.Factories;

namespace BetterThanCastle
{
	// Chopping block blueprint footprint. Vanilla data (see Dev chop-dump): the blueprint is a 2x2 of `fantome` cells with
	// 4 mainSupportReq cells below and an access/operation ring around the 2x2; the built block is one `obstacle|occupy|debloc`
	// cell with its own access/operation ring and a single mainSupportReq below. While "build.chop1x1" is on, the blueprint
	// volume is the BUILT block's cell set where the solid core (obstacle/occupy/debloc) becomes the blueprint marker `fantome`.
	// Rings, support cell and rotation come from the same rotation of the built block, so they stay consistent. Config 1
	// (anti volume) keeps only access|fantome|meshSwapTrigger, exactly as BlueprintVolumeFactoryPostProcessor does.
	public static class BtcChopBlock1x1
	{
		private static int idx = -2;
		private const bool Wip = false;
		internal static int Force = -1; // DEV tests: 0 off, 1 on, -1 normal

		private const long CoreBits = (long)(VPropertyMask.obstacle | VPropertyMask.occupy | VPropertyMask.debloc);
		private const long AntiKeep = (long)(VPropertyMask.access | VPropertyMask.fantome | VPropertyMask.meshSwapTrigger);

		private sealed class Entry
		{
			public Dictionary<XYZ, VPropertyMask> Settings;
			public List<XYZ>[] Indices;
		}

		private static readonly Dictionary<int, Entry> Cache = new Dictionary<int, Entry>();

		public static void AddRows()
		{
			BtcGameplay.Add("build.chop1x1", "build", "Chopping block 1x1, stations block only themselves", "Пенёк 1x1, станки занимают только себя", 1f, 0f, 1f, 1f, true, "The chopping block blueprint takes the same single block as the built chopping block: it snaps to the cursor block and rotation does not shift it. It stays a plain blueprint (does not block paths or dig terrain). Built crafting stations (workbench, forge, loom, lab, machine shop, altar) no longer forbid building on the 2 cells in front of them: only the station's own blocks are taken.", "Чертёж пенька занимает тот же один блок, что и построенный пенёк: встаёт в блок под курсором, поворот его не смещает. Это по-прежнему обычный чертёж (не блочит проход и не ломает землю). Построенные крафтильни (верстак, кузня, ткацкий станок, лаборатория, мастерская, алтарь) больше не запрещают строить на 2 клетках перед собой: заняты только блоки самого станка.");
		}

		public static bool Enabled
		{
			get
			{
				if (Wip) return false;
				if (Force >= 0) return Force == 1;
				if (!BtcNetworkGate.Enabled) return false;
				if (idx == -2) idx = BtcGameplay.Idx("build.chop1x1");
				float[] a = BtcGameplay.Active;
				return idx >= 0 && a != null && idx < a.Length && a[idx] > 0.5f;
			}
		}


		public static bool Applies(Factory.AssetKey key)
		{
			return key == Blueprints.ChoppingBlock && Enabled;
		}

		// Built crafting stations (station-dump): the two cells in front where the crafter stands are access|occupy(|operation).
		// `occupy` is a blueprint obstruction (BlueprintValidator.Obstructions), so nothing can be planned there. Under the same
		// rule those cells keep access/operation and lose occupy; the station's own cells (obstacle/debloc) stay untouched.
		private static readonly Factory.AssetKey[] StationKeys = { ObjetsDynamiques.Workbench, ObjetsDynamiques.Forge, ObjetsDynamiques.Loom,
			ObjetsDynamiques.Lab, ObjetsDynamiques.MachineShop, ObjetsDynamiques.Altar };
		private const long SolidBits = (long)(VPropertyMask.obstacle | VPropertyMask.debloc);
		private static readonly Dictionary<string, Entry> StationCache = new Dictionary<string, Entry>();

		public static bool AppliesStation(Factory.AssetKey key)
		{
			for (int i = 0; i < StationKeys.Length; i++) if (key == StationKeys[i]) return Enabled;
			return false;
		}

		private static Entry Station(Factory.AssetKey key, int rot, int config, Dictionary<XYZ, VPropertyMask> vanilla)
		{
			string k = key + "|" + rot + "|" + config;
			Entry e;
			if (StationCache.TryGetValue(k, out e)) return e;
			if (vanilla == null) return null;
			Dictionary<XYZ, VPropertyMask> res = new Dictionary<XYZ, VPropertyMask>();
			foreach (KeyValuePair<XYZ, VPropertyMask> kv in vanilla)
			{
				long m = (long)kv.Value;
				if ((m & SolidBits) == 0) m &= ~(long)VPropertyMask.occupy;
				if (m != 0) res[kv.Key] = (VPropertyMask)m;
			}
			List<XYZ>[] ix = new List<XYZ>[64];
			foreach (KeyValuePair<XYZ, VPropertyMask> kv in res)
				for (int i = 0; i < 64; i++)
					if (((long)kv.Value & (1L << i)) != 0)
					{
						if (ix[i] == null) ix[i] = new List<XYZ>();
						ix[i].Add(kv.Key);
					}
			for (int i = 0; i < 64; i++) if (ix[i] != null) ix[i].Sort();
			e = new Entry { Settings = res, Indices = ix };
			StationCache[k] = e;
			BtcLog.Info("ChopBlock", "station volume " + key + " rot=" + rot + " cfg=" + config + " cells=" + res.Count);
			return e;
		}

		public static Dictionary<XYZ, VPropertyMask> FilterStationSettings(Factory.AssetKey key, Dictionary<XYZ, VPropertyMask> vanilla, int rot, int config)
		{
			Entry e = Station(key, rot, config, vanilla);
			return e != null ? e.Settings : vanilla;
		}

		public static List<XYZ>[] FilterStationIndices(Factory.AssetKey key, List<XYZ>[] vanilla, int rot, int config)
		{
			if (vanilla == null) return null;
			Entry e = Station(key, rot, config, null);
			if (e == null)
			{
				VolumePreprocessor.GetIndexedVSettings(key, rot, config); // fills the cache through the settings postfix
				e = Station(key, rot, config, null);
			}
			return e != null ? e.Indices : vanilla;
		}

		private static Entry Get(int rot, int config)
		{
			int k = rot * 64 + config;
			Entry e;
			if (Cache.TryGetValue(k, out e)) return e;
			Dictionary<XYZ, VPropertyMask> built = VolumePreprocessor.GetIndexedVSettings(ObjetsDynamiques.ChoppingBlock, rot, 0);
			if (built == null || built.Count == 0) return null;
			Dictionary<XYZ, VPropertyMask> res = new Dictionary<XYZ, VPropertyMask>();
			foreach (KeyValuePair<XYZ, VPropertyMask> kv in built)
			{
				long m = (long)kv.Value;
				if ((m & CoreBits) != 0) m = (m & ~CoreBits) | (long)VPropertyMask.fantome;
				m &= ~(long)VPropertyMask.breakableAccess;
				if (config == 1) m &= AntiKeep;
				if (m != 0) res[kv.Key] = (VPropertyMask)m;
			}
			List<XYZ>[] ix = new List<XYZ>[64];
			foreach (KeyValuePair<XYZ, VPropertyMask> kv in res)
				for (int i = 0; i < 64; i++)
					if (((long)kv.Value & (1L << i)) != 0)
					{
						if (ix[i] == null) ix[i] = new List<XYZ>();
						ix[i].Add(kv.Key);
					}
			for (int i = 0; i < 64; i++) if (ix[i] != null) ix[i].Sort();
			e = new Entry { Settings = res, Indices = ix };
			Cache[k] = e;
			BtcLog.Info("ChopBlock", "1x1 volume rot=" + rot + " cfg=" + config + " cells=" + res.Count);
			return e;
		}

		public static Dictionary<XYZ, VPropertyMask> FilterSettings(Dictionary<XYZ, VPropertyMask> bp, int rot, int config)
		{
			if (bp == null || config > 1) return bp;
			Entry e = Get(rot, config);
			return e != null ? e.Settings : bp;
		}

		public static List<XYZ>[] FilterIndices(List<XYZ>[] bp, int rot, int config)
		{
			if (bp == null || config > 1) return bp;
			Entry e = Get(rot, config);
			return e != null ? e.Indices : bp;
		}
	}
}
