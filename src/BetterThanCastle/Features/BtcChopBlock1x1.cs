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
			BtcGameplay.Add("build.chop1x1", "build", "Chopping block blueprint is 1x1", "Чертёж пенька 1x1", 1f, 0f, 1f, 1f, true, "The chopping block blueprint takes the same single block as the built chopping block: it snaps to the cursor block and rotation does not shift it. It stays a plain blueprint (does not block paths or dig terrain).", "Чертёж пенька занимает тот же один блок, что и построенный пенёк: встаёт в блок под курсором, поворот его не смещает. Это по-прежнему обычный чертёж (не блочит проход и не ломает землю).");
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
