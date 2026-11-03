using System;
using Brix.External.Factories;

namespace BetterThanCastle
{
	// Chopping block blueprint footprint: while "build.chop1x1" is on, the blueprint's volume
	// (placement ghost, validation, host-side obstruction check) is the built object's 1x1 volume.
	public static class BtcChopBlock1x1
	{
		private static int idx = -2;

		public static void AddRows()
		{
			BtcGameplay.Add("build.chop1x1", "build", "Chopping block blueprint is 1x1", "Чертёж пенька 1x1", 1f, 0f, 1f, 1f, true, "The chopping block blueprint uses the same 1x1 footprint as the built block: it snaps to the cursor block, rotation does not shift it and only one block is checked for obstruction.", "Чертёж пенька использует тот же размер 1x1, что и построенный пенёк: он встаёт в блок под курсором, поворот его не смещает, а на препятствия проверяется только один блок.");
		}

		public static bool Enabled
		{
			get
			{
				if (!BtcNetworkGate.Enabled) return false;
				if (idx == -2) idx = BtcGameplay.Idx("build.chop1x1");
				float[] a = BtcGameplay.Active;
				return idx >= 0 && a != null && idx < a.Length && a[idx] > 0.5f;
			}
		}

		// Returns the key whose volume data must be used for the requested key.
		public static void Redirect(ref Factory.AssetKey key)
		{
			if (key == Blueprints.ChoppingBlock && Enabled) key = ObjetsDynamiques.ChoppingBlock;
		}
	}
}
