using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Engine;
using Brix.Game;
using Brix.Legacy;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
public static class BtcTerrainUndo
{
	private static void Log(string text)
	{
		try
		{
			UnityEngine.Debug.Log("[BtcTerrainUndo] " + text);
			BtcPaths.Init();
			File.AppendAllText(BtcPaths.DebugFile("replay_log.txt"), DateTime.Now.ToString("HH:mm:ss") + " [terrain] " + text + "\r\n");
		}
		catch (Exception)
		{
		}
	}

	/// <summary>Replay seek (BtcReplayAnchor.ApplyTerrainBlob): journal a column/voxel it wrote directly.</summary>
	internal static void MarkAt(XYZ p)
	{
		BtcTerrainUndo.Mark(p);
	}

	private static void Mark(XYZ p)
	{
		bool flag = !BtcTerrainUndo._live;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			int num = p.x - BtcTerrainUndo._x0;
			int num2 = p.z - BtcTerrainUndo._z0;
			bool flag4 = num >= 0 && num2 >= 0 && num < BtcTerrainUndo._w && num2 < BtcTerrainUndo._h;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				BtcTerrainUndo._dirty.Add(num * BtcTerrainUndo._h + num2);
				bool flag8 = p.y >= 0 && p.y <= 255;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					int key = num * BtcTerrainUndo._h + num2;
					int num3 = p.y;
					int num4 = p.y;
					int num5;
					bool flag11 = BtcTerrainUndo._vrange.TryGetValue(key, out num5);
					bool flag12 = flag11;
					if (flag12)
					{
						num3 = Math.Min(num3, num5 & 255);
						num4 = Math.Max(num4, num5 >> 8);
					}
					BtcTerrainUndo._vrange[key] = (num3 | num4 << 8);
				}
			}
			else
			{
				BtcTerrainUndo._dropped++;
				bool flag13 = BtcTerrainUndo._sigLog < 25;
				bool flag14 = flag13;
				bool flag15 = flag14;
				if (flag15)
				{
					BtcTerrainUndo.Log(string.Concat(new object[]
					{
						"signal outside the snapshot at ",
						p.x,
						",",
						p.y,
						",",
						p.z
					}));
					BtcTerrainUndo._sigLog++;
				}
			}
		}
	}

	private static void Hook()
	{
		bool hooked = BtcTerrainUndo._hooked;
		bool flag = !hooked;
		bool flag2 = flag;
		if (flag2)
		{
			BtcTerrainUndo._hooked = true;
			GameSignals.Connect<XYZ>(GameSignals.VoxelChanged, delegate(XYZ p)
			{
				BtcTerrainUndo.Mark(p);
			}, null);
			GameSignals.Connect<XYZ>(GameSignals.TerrainChanged, delegate(XYZ p)
			{
				BtcTerrainUndo.Mark(p);
			}, null);
			GameSignals.Connect<XYZ>(GameSignals.TerrainRemoved, delegate(XYZ p)
			{
				BtcTerrainUndo.Mark(p);
			}, null);
		}
	}

	public static void Capture()
	{
		try
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			Bounds bounds = Voxel.Engine.ComputeOccupiedBounds();
			int num = Mathf.FloorToInt(bounds.min.x) - BtcTerrainUndo.Margin;
			int num2 = Mathf.CeilToInt(bounds.max.x) - 1 + BtcTerrainUndo.Margin;
			int num3 = Mathf.FloorToInt(bounds.min.z) - BtcTerrainUndo.Margin;
			int num4 = Mathf.CeilToInt(bounds.max.z) - 1 + BtcTerrainUndo.Margin;
			int num5 = num2 - num + 1;
			int num6 = num4 - num3 + 1;
			bool flag = num5 <= 0 || num6 <= 0 || (long)num5 * (long)num6 > 4000000L;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				BtcTerrainUndo.Log(string.Concat(new object[]
				{
					"capture: bad bounds ",
					num5,
					"x",
					num6,
					", terrain undo disabled"
				}));
				BtcTerrainUndo._has = false;
				BtcTerrainUndo._live = false;
			}
			else
			{
				BtcTerrainUndo._x0 = num;
				BtcTerrainUndo._z0 = num3;
				BtcTerrainUndo._w = num5;
				BtcTerrainUndo._h = num6;
				int num7 = num5 * num6;
				BtcTerrainUndo._bot = new short[num7];
				BtcTerrainUndo._top = new short[num7];
				BtcTerrainUndo._typ = new byte[num7];
				BtcTerrainUndo._cru = new byte[num7];
				BtcTerrainUndo._crf = new float[num7];
				for (int i = num; i <= num2; i++)
				{
					for (int j = num3; j <= num4; j++)
					{
						int num8;
						int num9;
						VoxelTerrainType voxelTerrainType;
						float num10;
						VoxelEngine.GetColumnData(i, j, out num8, out num9, out voxelTerrainType, out num10);
						int num11 = (i - num) * num6 + (j - num3);
						BtcTerrainUndo._bot[num11] = (short)num8;
						BtcTerrainUndo._top[num11] = (short)num9;
						BtcTerrainUndo._typ[num11] = (byte)voxelTerrainType;
						BtcTerrainUndo._cru[num11] = (byte)Mathf.RoundToInt(num10 * 3f);
						BtcTerrainUndo._crf[num11] = num10;
					}
				}
				BtcTerrainUndo._dirty.Clear();
				BtcTerrainUndo._vrange.Clear();
				BtcTerrainUndo._sigLog = 0;
				BtcTerrainUndo._dropped = 0;
				BtcTerrainUndo._has = true;
				BtcTerrainUndo.CaptureVoxels();
				BtcTerrainUndo._live = true;
				BtcTerrainUndo.Hook();
				BtcTerrainUndo.CaptureTrees();
				BtcTerrainUndo.DiagVoxels("baseline", true);
				BtcTerrainUndo.Log(string.Concat(new object[]
				{
					"capture: ",
					num5,
					"x",
					num6,
					" columns, total ",
					stopwatch.ElapsedMilliseconds,
					" ms, trees ",
					(BtcTerrainUndo._trees != null) ? BtcTerrainUndo._trees.Count : -1,
					", voxels ",
					BtcTerrainUndo._hasVox.ToString()
				}));
				BtcTerrainUndo.Dump("baseline");
			}
		}
		catch (Exception arg)
		{
			BtcTerrainUndo._has = false;
			BtcTerrainUndo._live = false;
			BtcTerrainUndo.Log("capture failed: " + arg);
		}
	}

	private static bool Fix(int i, List<int> idx, List<int> oldTop)
	{
		int x = BtcTerrainUndo._x0 + i / BtcTerrainUndo._h;
		int z = BtcTerrainUndo._z0 + i % BtcTerrainUndo._h;
		int num;
		int num2;
		VoxelTerrainType voxelTerrainType;
		float num3;
		VoxelEngine.GetColumnData(x, z, out num, out num2, out voxelTerrainType, out num3);
		bool flag = num == (int)BtcTerrainUndo._bot[i] && num2 == (int)BtcTerrainUndo._top[i] && (byte)voxelTerrainType == BtcTerrainUndo._typ[i] && Mathf.Abs(num3 - BtcTerrainUndo._crf[i]) < 0.0001f;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool result;
		if (flag3)
		{
			result = false;
		}
		else
		{
			VoxelEngine.SetColumnBottom(x, z, (int)BtcTerrainUndo._bot[i]);
			VoxelEngine.SetColumnTopVoxel(x, z, (int)BtcTerrainUndo._top[i]);
			VoxelEngine.SetColumnType(x, z, (VoxelTerrainType)BtcTerrainUndo._typ[i]);
			VoxelEngine.SetColumnCrustVolume(x, z, BtcTerrainUndo._crf[i]);
			bool flag4 = num == (int)BtcTerrainUndo._bot[i] && num2 == (int)BtcTerrainUndo._top[i] && (byte)voxelTerrainType == BtcTerrainUndo._typ[i];
			bool flag5 = flag4;
			if (flag5)
			{
				BtcTerrainUndo._crustOnly++;
			}
			BtcTerrainUndo._fixBot.Add(num);
			idx.Add(i);
			oldTop.Add(num2);
			result = true;
		}
		return result;
	}

	private static void FireAt(int x, int y, int z)
	{
		bool flag = y < 0 || y > 254;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			XYZ xyz = new XYZ(x, y, z);
			GameSignals.Invoke<XYZ>(GameSignals.VoxelChanged, xyz);
			GameSignals.Invoke<XYZ>(GameSignals.VoxelChanged, new XYZ(x, y + 1, z));
			GameSignals.Invoke<XYZ>(GameSignals.VoxelChanged, new XYZ(x + 1, y, z));
			GameSignals.Invoke<XYZ>(GameSignals.VoxelChanged, new XYZ(x - 1, y, z));
			GameSignals.Invoke<XYZ>(GameSignals.VoxelChanged, new XYZ(x, y, z + 1));
			GameSignals.Invoke<XYZ>(GameSignals.VoxelChanged, new XYZ(x, y, z - 1));
			GameSignals.Invoke<XYZ>(GameSignals.TerrainChanged, xyz);
			Architecte.Changement(xyz);
			Architecte.Changement(new XYZ(x, y + 1, z));
			Architecte.Changement(new XYZ(x, y - 1, z));
		}
	}

	private static bool BaseVox(int c, int y, out int t, out int v)
	{
		t = 0;
		v = 0;
		int num = BtcTerrainUndo._vstart[c];
		bool flag = num < 0;
		bool flag2 = flag;
		bool result;
		if (flag2)
		{
			result = false;
		}
		else
		{
			int num2 = Mathf.Max((int)(BtcTerrainUndo._bot[c] - 1), 0);
			int num3 = Mathf.Min((int)(BtcTerrainUndo._top[c] + 1), 254);
			bool flag3 = y < num2 || y > num3;
			bool flag4 = flag3;
			if (flag4)
			{
				result = false;
			}
			else
			{
				t = (int)BtcTerrainUndo._vt[num + (y - num2)];
				v = (int)BtcTerrainUndo._vv[num + (y - num2)];
				result = true;
			}
		}
		return result;
	}

	private static int ScanCol(int c, int lo, int hi, List<int> vx, List<int> vy, List<int> vz)
	{
		int num = BtcTerrainUndo._x0 + c / BtcTerrainUndo._h;
		int num2 = BtcTerrainUndo._z0 + c % BtcTerrainUndo._h;
		int num3 = 0;
		lo = Mathf.Max(lo, 0);
		hi = Mathf.Min(hi, 254);
		for (int i = lo; i <= hi; i++)
		{
			int bt;
			int bv;
			BtcTerrainUndo.BaseVox(c, i, out bt, out bv);
			bool flag = BtcTerrainUndo.FixVoxel(num, i, num2, bt, bv);
			bool flag2 = flag;
			if (flag2)
			{
				vx.Add(num);
				vy.Add(i);
				vz.Add(num2);
				num3++;
			}
		}
		return num3;
	}

	private static void Residual(Dictionary<int, int> area)
	{
		int num = 0;
		int num2 = 0;
		foreach (KeyValuePair<int, int> keyValuePair in area)
		{
			int key = keyValuePair.Key;
			int num3 = BtcTerrainUndo._x0 + key / BtcTerrainUndo._h;
			int num4 = BtcTerrainUndo._z0 + key % BtcTerrainUndo._h;
			int num5 = Mathf.Min(keyValuePair.Value >> 8, 254);
			for (int i = Mathf.Max(keyValuePair.Value & 255, 0); i <= num5; i++)
			{
				int num6;
				int num7;
				BtcTerrainUndo.BaseVox(key, i, out num6, out num7);
				bool flag = num6 != 0;
				bool flag2 = !flag;
				if (flag2)
				{
					VoxelHandle voxelHandle = Voxel.Engine.GetVoxelHandle(new XYZ(num3, i, num4));
					int voxelType = Voxel.Engine.GetVoxelType(voxelHandle);
					bool flag3 = Voxel.Engine.IsVoxelNonEmpty(voxelHandle);
					bool flag4 = voxelType != 0 || flag3;
					bool flag5 = flag4;
					if (flag5)
					{
						num++;
						bool flag6 = num2 < 10;
						bool flag7 = flag6;
						if (flag7)
						{
							num2++;
							BtcTerrainUndo.Log(string.Concat(new object[]
							{
								"residual at ",
								num3,
								",",
								i,
								",",
								num4,
								": type ",
								voxelType,
								", volume ",
								Voxel.Engine.GetVoxelVolume(voxelHandle),
								", nonEmpty ",
								flag3.ToString(),
								", resources ",
								Voxel.Engine.GetTerrainResources(voxelHandle).TotalAmount,
								", baseline empty"
							}));
						}
					}
				}
			}
		}
		BtcTerrainUndo.Log("restore: residual extra voxels in the journal area: " + num);
	}

	private static bool Solid(XYZ p)
	{
		return Voxel.Engine.IsVoxelNonEmpty(Voxel.Engine.GetVoxelHandle(p));
	}

	private static string ClearVoxel(XYZ p)
	{
		Voxel.Engine.RemoveVoxel(Voxel.Engine.GetVoxelHandle(p));
		return BtcTerrainUndo.Solid(p) ? "stuck" : "RemoveVoxel";
	}

	private static int Nudge(ICollection<int> cols)
	{
		List<int> list = new List<int>();
		List<XYZ> list2 = new List<XYZ>();
		List<int> list3 = new List<int>();
		VoxelEngine.BeginChanges();
		foreach (int num in cols)
		{
			bool flag = BtcTerrainUndo._bot[num] >= BtcTerrainUndo._top[num];
			bool flag2 = !flag;
			if (flag2)
			{
				bool flag3 = BtcTerrainUndo._vstart[num] >= 0;
				bool flag4 = flag3;
				if (flag4)
				{
					int x = BtcTerrainUndo._x0 + num / BtcTerrainUndo._h;
					int z = BtcTerrainUndo._z0 + num % BtcTerrainUndo._h;
					int num2 = Mathf.Max((int)(BtcTerrainUndo._top[num] - 2), 0);
					int num3 = Mathf.Min((int)(BtcTerrainUndo._top[num] + 1), 254);
					for (int i = num2; i <= num3; i++)
					{
						int num4;
						int num5;
						BtcTerrainUndo.BaseVox(num, i, out num4, out num5);
						bool flag5 = num4 == 0;
						bool flag6 = !flag5;
						if (flag6)
						{
							XYZ xyz = new XYZ(x, i, z);
							VoxelHandle voxelHandle = Voxel.Engine.GetVoxelHandle(xyz);
							bool flag7 = Mathf.Clamp(Voxel.Engine.GetVoxelVolume(voxelHandle), 0, 255) != num5;
							bool flag8 = !flag7;
							if (flag8)
							{
								Voxel.Engine.SetVoxelVolume(voxelHandle, (num5 > 1) ? (num5 - 1) : (num5 + 1));
								list2.Add(xyz);
								list3.Add(num5);
							}
						}
					}
				}
				float num6 = BtcTerrainUndo._crf[num];
				float crustVolume = (num6 < 0.5f) ? (num6 + 0.33333334f) : (num6 - 0.33333334f);
				VoxelEngine.SetColumnCrustVolume(BtcTerrainUndo._x0 + num / BtcTerrainUndo._h, BtcTerrainUndo._z0 + num % BtcTerrainUndo._h, crustVolume);
				list.Add(num);
			}
		}
		VoxelEngine.ApplyChanges(false);
		VoxelEngine.BeginChanges();
		for (int j = 0; j < list.Count; j++)
		{
			int num7 = list[j];
			VoxelEngine.SetColumnCrustVolume(BtcTerrainUndo._x0 + num7 / BtcTerrainUndo._h, BtcTerrainUndo._z0 + num7 % BtcTerrainUndo._h, BtcTerrainUndo._crf[num7]);
		}
		for (int k = 0; k < list2.Count; k++)
		{
			Voxel.Engine.SetVoxelVolume(Voxel.Engine.GetVoxelHandle(list2[k]), list3[k]);
		}
		VoxelEngine.ApplyChanges(false);
		return list.Count;
	}

	private static void LogOnce(string t)
	{
		bool flag = BtcTerrainUndo._once < 8;
		bool flag2 = flag;
		if (flag2)
		{
			BtcTerrainUndo._once++;
			BtcTerrainUndo.Log("clear: " + t);
		}
	}

	private static void LogClear(string[] how)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		for (int i = 0; i < how.Length; i++)
		{
			int num;
			dictionary.TryGetValue(how[i], out num);
			dictionary[how[i]] = num + 1;
		}
		StringBuilder stringBuilder = new StringBuilder("restore: voxel clearing results:");
		foreach (KeyValuePair<string, int> keyValuePair in dictionary)
		{
			stringBuilder.Append(' ').Append(keyValuePair.Key).Append('=').Append(keyValuePair.Value);
		}
		BtcTerrainUndo.Log(stringBuilder.ToString());
	}

	private static void GlobalResidual(Dictionary<int, int> area)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		int num = BtcTerrainUndo._w * BtcTerrainUndo._h;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		for (int i = 0; i < num; i++)
		{
			int num5 = (BtcTerrainUndo._vstart[i] >= 0) ? Mathf.Min((int)(BtcTerrainUndo._top[i] + 2), 254) : 0;
			bool flag = BtcTerrainUndo._vstart[i] < 0;
			bool flag2 = !flag;
			if (flag2)
			{
				int num6 = BtcTerrainUndo._x0 + i / BtcTerrainUndo._h;
				int num7 = BtcTerrainUndo._z0 + i % BtcTerrainUndo._h;
				int num8 = Mathf.Min(num5 + 6, 254);
				for (int j = num5; j <= num8; j++)
				{
					bool flag3 = Voxel.Engine.IsVoxelNonEmpty(new XYZ(num6, j, num7));
					bool flag4 = flag3;
					if (flag4)
					{
						num2++;
						bool flag5 = area.ContainsKey(i);
						bool flag6 = flag5;
						if (flag6)
						{
							num3++;
						}
						bool flag7 = num4 < 10;
						bool flag8 = flag7;
						if (flag8)
						{
							num4++;
							BtcTerrainUndo.Log(string.Concat(new object[]
							{
								"global residual at ",
								num6,
								",",
								j,
								",",
								num7,
								area.ContainsKey(i) ? " (in journal area)" : " (OUTSIDE journal area)"
							}));
						}
					}
				}
			}
		}
		BtcTerrainUndo.Log(string.Concat(new object[]
		{
			"restore: solid voxels above baseline top+1..+7: ",
			num2,
			" (in journal area ",
			num3,
			"), ",
			stopwatch.ElapsedMilliseconds,
			" ms"
		}));
	}

	public static bool Restore()
	{
		bool flag = !BtcTerrainUndo._has || BtcTerrainUndo._bot == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool result;
		if (flag3)
		{
			BtcTerrainUndo.Log("restore: no baseline, nothing to restore");
			result = false;
		}
		else
		{
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				int num = BtcTerrainUndo._w * BtcTerrainUndo._h;
				int count = BtcTerrainUndo._vrange.Count;
				BtcTerrainUndo.DiagVoxels("rewind", false);
				int num2 = Mathf.Max(BtcTerrainUndo.Radius, 0);
				Dictionary<int, int> dictionary = new Dictionary<int, int>();
				foreach (KeyValuePair<int, int> keyValuePair in BtcTerrainUndo._vrange)
				{
					int num3 = keyValuePair.Key / BtcTerrainUndo._h;
					int num4 = keyValuePair.Key % BtcTerrainUndo._h;
					int num5 = Mathf.Max((keyValuePair.Value & 255) - num2 - 1, 0);
					int num6 = Mathf.Min((keyValuePair.Value >> 8) + num2 + 1, 254);
					for (int i = -num2; i <= num2; i++)
					{
						int num7 = num3 + i;
						bool flag4 = num7 < 0 || num7 >= BtcTerrainUndo._w;
						bool flag5 = !flag4;
						if (flag5)
						{
							for (int j = -num2; j <= num2; j++)
							{
								int num8 = num4 + j;
								bool flag6 = num8 < 0 || num8 >= BtcTerrainUndo._h;
								bool flag7 = !flag6;
								if (flag7)
								{
									int key = num7 * BtcTerrainUndo._h + num8;
									int num9;
									bool flag8 = dictionary.TryGetValue(key, out num9);
									bool flag9 = flag8;
									if (flag9)
									{
										dictionary[key] = (Math.Min(num5, num9 & 255) | Math.Max(num6, num9 >> 8) << 8);
									}
									else
									{
										dictionary[key] = (num5 | num6 << 8);
									}
								}
							}
						}
					}
				}
				bool flag10 = BtcTerrainUndo.FullScan || dictionary.Count > num / 4;
				BtcTerrainUndo._fixBot.Clear();
				BtcTerrainUndo._rem.Clear();
				BtcTerrainUndo._crustOnly = 0;
				List<int> list = new List<int>();
				List<int> list2 = new List<int>();
				List<int> list3 = new List<int>();
				List<int> list4 = new List<int>();
				List<int> list5 = new List<int>();
				int num10 = 0;
				BtcTerrainUndo._live = false;
				VoxelEngine.BeginChanges();
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					for (int k = 0; k < num; k++)
					{
						BtcTerrainUndo.Fix(k, list, list2);
					}
				}
				else
				{
					// compare EVERY column (cheap, no voxel access) so crust/top changes that raised no signal are still found
					for (int k2 = 0; k2 < num; k2++)
					{
						BtcTerrainUndo.Fix(k2, list, list2);
					}
				}
				VoxelEngine.ApplyChanges(false);
				VoxelEngine.BeginChanges();
				bool hasVox = BtcTerrainUndo._hasVox;
				bool flag13 = hasVox;
				if (flag13)
				{
					bool flag14 = flag10;
					bool flag15 = flag14;
					if (flag15)
					{
						for (int l = 0; l < num; l++)
						{
							int num11 = 1;
							int num12 = 0;
							bool flag16 = BtcTerrainUndo._vstart[l] >= 0;
							bool flag17 = flag16;
							if (flag17)
							{
								num11 = (int)(BtcTerrainUndo._bot[l] - 1);
								num12 = (int)(BtcTerrainUndo._top[l] + 1);
							}
							int num13;
							bool flag18 = dictionary.TryGetValue(l, out num13);
							bool flag19 = flag18;
							bool flag20 = flag19;
							if (flag20)
							{
								bool flag21 = num11 > num12;
								num11 = (flag21 ? (num13 & 255) : Math.Min(num11, num13 & 255));
								num12 = (flag21 ? (num13 >> 8) : Math.Max(num12, num13 >> 8));
							}
							bool flag22 = num11 <= num12;
							bool flag23 = flag22;
							if (flag23)
							{
								int num14 = BtcTerrainUndo.ScanCol(l, num11, num12, list3, list4, list5);
								bool flag24 = !flag18;
								bool flag25 = flag24;
								if (flag25)
								{
									num10 += num14;
								}
							}
						}
					}
					else
					{
						foreach (KeyValuePair<int, int> keyValuePair2 in dictionary)
						{
							BtcTerrainUndo.ScanCol(keyValuePair2.Key, keyValuePair2.Value & 255, keyValuePair2.Value >> 8, list3, list4, list5);
						}
						// columns whose top/bottom/crust differed (found by the full column compare): scan the union of the live and baseline voxel span
						for (int cc = 0; cc < list.Count; cc++)
						{
							int ci = list[cc];
							if (dictionary.ContainsKey(ci)) continue;
							int lo = Math.Min((int)BtcTerrainUndo._bot[ci], BtcTerrainUndo._fixBot[cc]) - 1;
							int hi = Math.Max((int)BtcTerrainUndo._top[ci], list2[cc]) + 1;
							int sc = BtcTerrainUndo.ScanCol(ci, lo, hi, list3, list4, list5);
							if (sc > 0) num10 += sc;
						}
					}
				}
				VoxelEngine.ApplyChanges(false);
				bool removeUnbatched = BtcTerrainUndo.RemoveUnbatched;
				bool flag26 = removeUnbatched;
				if (flag26)
				{
					string[] array = new string[BtcTerrainUndo._rem.Count];
					List<XYZ> list6 = new List<XYZ>();
					for (int m = 0; m < BtcTerrainUndo._rem.Count; m++)
					{
						array[m] = BtcTerrainUndo.ClearVoxel(BtcTerrainUndo._rem[m]);
						bool flag27 = array[m] != "RemoveVoxel";
						bool flag28 = flag27;
						if (flag28)
						{
							list6.Add(BtcTerrainUndo._rem[m]);
						}
					}
					BtcTerrainUndo.LogClear(array);
				}
				int num15 = 0;
				bool flag29 = BtcTerrainUndo.Nudging && (list.Count > 0 || list3.Count > 0 || BtcTerrainUndo._rem.Count > 0);
				bool flag30 = flag29;
				if (flag30)
				{
					try
					{
						bool flag31 = !flag10;
						ICollection<int> cols;
						if (flag31)
						{
							HashSet<int> keys = new HashSet<int>(dictionary.Keys);
							for (int cc2 = 0; cc2 < list.Count; cc2++) keys.Add(list[cc2]);
							cols = keys;
						}
						else
						{
							ICollection<int> keys2 = BtcTerrainUndo._vrange.Keys;
							cols = keys2;
						}
						num15 = BtcTerrainUndo.Nudge(cols);
					}
					catch (Exception ex)
					{
						BtcTerrainUndo.Log("nudge: " + ex.GetBaseException().Message);
					}
				}
				bool flag32 = BtcTerrainUndo.FillDiag && !flag10;
				bool flag33 = flag32;
				if (flag33)
				{
					try
					{
						BtcTerrainUndo.Residual(dictionary);
					}
					catch (Exception ex2)
					{
						BtcTerrainUndo.Log("residual: " + ex2.Message);
					}
				}
				for (int n = 0; n < list.Count; n++)
				{
					int num16 = list[n];
					int x = BtcTerrainUndo._x0 + num16 / BtcTerrainUndo._h;
					int z = BtcTerrainUndo._z0 + num16 % BtcTerrainUndo._h;
					BtcTerrainUndo.FireAt(x, (int)BtcTerrainUndo._top[num16], z);
					bool flag34 = list2[n] != (int)BtcTerrainUndo._top[num16];
					bool flag35 = flag34;
					if (flag35)
					{
						BtcTerrainUndo.FireAt(x, list2[n], z);
					}
				}
				for (int num17 = 0; num17 < list3.Count; num17++)
				{
					BtcTerrainUndo.FireAt(list3[num17], list4[num17], list5[num17]);
				}
				for (int num18 = 0; num18 < BtcTerrainUndo._rem.Count; num18++)
				{
					XYZ xyz = BtcTerrainUndo._rem[num18];
					bool flag36 = TerrainDamageTracker.Instance != null;
					bool flag37 = flag36;
					if (flag37)
					{
						TerrainDamageTracker.Instance.ClearDamageAt(xyz);
					}
					GameSignals.Invoke<XYZ>(GameSignals.TerrainRemoved, xyz);
					GameSignals.Invoke<Vector3>(GameSignals.TerrainRemovedLua, xyz.ToVector3());
					BtcTerrainUndo.FireAt(xyz.x, xyz.y - 1, xyz.z);
					BtcTerrainUndo.FireAt(xyz.x + 1, xyz.y, xyz.z);
					BtcTerrainUndo.FireAt(xyz.x - 1, xyz.y, xyz.z);
					BtcTerrainUndo.FireAt(xyz.x, xyz.y, xyz.z + 1);
					BtcTerrainUndo.FireAt(xyz.x, xyz.y, xyz.z - 1);
				}
				bool fillDiag = BtcTerrainUndo.FillDiag;
				bool flag38 = fillDiag;
				if (flag38)
				{
					string text = (BtcTerrainUndo._rem.Count > 0) ? string.Concat(new object[]
					{
						BtcTerrainUndo._rem[0].x,
						",",
						BtcTerrainUndo._rem[0].y,
						",",
						BtcTerrainUndo._rem[0].z
					}) : "-";
					BtcTerrainUndo.Log(string.Concat(new object[]
					{
						"restore: removed ",
						BtcTerrainUndo._rem.Count,
						" voxels that were absent in the baseline (first ",
						text,
						")"
					}));
					try
					{
						BtcTerrainUndo.GlobalResidual(dictionary);
					}
					catch (Exception ex3)
					{
						BtcTerrainUndo.Log("global residual: " + ex3.Message);
					}
				}
				try
				{
					Architecte.AppliquerChangements();
				}
				catch (Exception ex4)
				{
					BtcTerrainUndo.Log("restore: AppliquerChangements: " + ex4.Message);
				}
				BtcTerrainUndo.Log("restore: signals dropped outside the capture area since last capture: " + BtcTerrainUndo._dropped);
				BtcTerrainUndo._dirty.Clear();
				BtcTerrainUndo._vrange.Clear();
				BtcTerrainUndo._live = true;
				BtcTerrainUndo.Log(string.Concat(new object[]
				{
					"restore: ",
					flag10 ? "FULL scan" : "journal area",
					", journal ",
					count,
					" columns, checked ",
					flag10 ? num : dictionary.Count,
					" columns, changed ",
					list.Count,
					" columns (crust only ",
					BtcTerrainUndo._crustOnly,
					") / ",
					list3.Count,
					" voxels",
					BtcTerrainUndo.FullScan ? (", outside the journal area " + num10) : "",
					", nudged ",
					num15,
					", ",
					stopwatch.ElapsedMilliseconds,
					" ms"
				}));
				bool voxelDiag = BtcTerrainUndo.VoxelDiag;
				bool flag39 = voxelDiag;
				if (flag39)
				{
					BtcTerrainUndo.Log(BtcTerrainUndo.DiagVoxels("after restore", false) ? "restore: voxels match the baseline" : "restore: voxels STILL differ from the baseline");
				}
				result = true;
			}
			catch (Exception arg)
			{
				BtcTerrainUndo._live = true;
				BtcTerrainUndo.Log("restore failed: " + arg);
				result = false;
			}
		}
		return result;
	}

	private static IDictionary TreeDict(out object inst, out Type t)
	{
		inst = null;
		t = typeof(GameLoader).Assembly.GetType("Brix.Legacy.TreeRegistry");
		bool flag = t == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		IDictionary result;
		if (flag3)
		{
			result = null;
		}
		else
		{
			PropertyInfo property = t.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
			inst = ((property != null) ? property.GetValue(null, null) : null);
			FieldInfo field = t.GetField("_treeData", BindingFlags.Instance | BindingFlags.NonPublic);
			bool flag4 = field == null || inst == null;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				result = null;
			}
			else
			{
				result = (field.GetValue(inst) as IDictionary);
			}
		}
		return result;
	}

	private static void CaptureTrees()
	{
		try
		{
			object obj;
			Type type;
			IDictionary dictionary = BtcTerrainUndo.TreeDict(out obj, out type);
			bool flag = dictionary == null;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				BtcTerrainUndo._trees = null;
			}
			else
			{
				BtcTerrainUndo._trees = new Dictionary<object, int>();
				foreach (object obj2 in dictionary)
				{
					DictionaryEntry dictionaryEntry = (DictionaryEntry)obj2;
					BtcTerrainUndo._trees[dictionaryEntry.Key] = (int)dictionaryEntry.Value;
				}
			}
		}
		catch (Exception ex)
		{
			BtcTerrainUndo._trees = null;
			BtcTerrainUndo.Log("CaptureTrees: " + ex.Message);
		}
	}

	public static bool RestoreTrees()
	{
		bool flag = BtcTerrainUndo._trees == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool result;
		if (flag3)
		{
			BtcTerrainUndo.Log("trees: no baseline");
			result = false;
		}
		else
		{
			try
			{
				object obj;
				Type type;
				IDictionary dictionary = BtcTerrainUndo.TreeDict(out obj, out type);
				bool flag4 = dictionary == null;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					BtcTerrainUndo.Log("trees: registry not found");
					result = false;
				}
				else
				{
					MethodInfo method = type.GetMethod("Remove", BindingFlags.Instance | BindingFlags.Public);
					MethodInfo methodInfo = null;
					foreach (MethodInfo methodInfo2 in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
					{
						bool flag7 = methodInfo2.Name == "Add" && methodInfo2.GetParameters().Length == 2;
						bool flag8 = flag7;
						bool flag9 = flag8;
						if (flag9)
						{
							methodInfo = methodInfo2;
						}
					}
					bool flag10 = method == null || methodInfo == null;
					bool flag11 = flag10;
					bool flag12 = flag11;
					if (flag12)
					{
						BtcTerrainUndo.Log("trees: Add/Remove not found");
						result = false;
					}
					else
					{
						int count = dictionary.Count;
						List<object> list = new List<object>();
						List<KeyValuePair<object, int>> list2 = new List<KeyValuePair<object, int>>();
						foreach (object obj2 in dictionary)
						{
							DictionaryEntry dictionaryEntry = (DictionaryEntry)obj2;
							int num;
							bool flag13 = !BtcTerrainUndo._trees.TryGetValue(dictionaryEntry.Key, out num);
							bool flag14 = flag13;
							bool flag15 = flag14;
							if (flag15)
							{
								list.Add(dictionaryEntry.Key);
							}
							else
							{
								bool flag16 = num != (int)dictionaryEntry.Value;
								bool flag17 = flag16;
								bool flag18 = flag17;
								if (flag18)
								{
									list.Add(dictionaryEntry.Key);
									list2.Add(new KeyValuePair<object, int>(dictionaryEntry.Key, num));
								}
							}
						}
						foreach (KeyValuePair<object, int> item in BtcTerrainUndo._trees)
						{
							bool flag19 = !dictionary.Contains(item.Key);
							bool flag20 = flag19;
							bool flag21 = flag20;
							if (flag21)
							{
								list2.Add(item);
							}
						}
						for (int j = 0; j < list.Count; j++)
						{
							method.Invoke(obj, new object[]
							{
								list[j]
							});
						}
						for (int k = 0; k < list2.Count; k++)
						{
							methodInfo.Invoke(obj, new object[]
							{
								list2[k].Key,
								list2[k].Value
							});
						}
						DamagedTree.AllDamagedTrees.Clear();
						try
						{
							Architecte.AppliquerChangements();
						}
						catch (Exception)
						{
						}
						BtcTerrainUndo.Log(string.Concat(new object[]
						{
							"trees: registry had ",
							count,
							", baseline ",
							BtcTerrainUndo._trees.Count,
							", removed ",
							list.Count,
							", added ",
							list2.Count,
							", now ",
							dictionary.Count
						}));
						result = true;
					}
				}
			}
			catch (Exception arg)
			{
				BtcTerrainUndo.Log("RestoreTrees: " + arg);
				result = false;
			}
		}
		return result;
	}

	public static void Clear()
	{
		BtcTerrainUndo._live = false;
		BtcTerrainUndo._has = false;
		BtcTerrainUndo._hasVox = false;
		BtcTerrainUndo._bot = null;
		BtcTerrainUndo._top = null;
		BtcTerrainUndo._typ = null;
		BtcTerrainUndo._cru = null;
		BtcTerrainUndo._crf = null;
		BtcTerrainUndo._vstart = null;
		BtcTerrainUndo._vt = null;
		BtcTerrainUndo._vv = null;
		BtcTerrainUndo._trees = null;
		BtcTerrainUndo._dirty.Clear();
		BtcTerrainUndo._vrange.Clear();
		BtcTerrainUndo.Dump("playback ended");
	}

	private static FieldInfo FieldUp(Type t, string name)
	{
		while (t != null)
		{
			FieldInfo field = t.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			bool flag = field != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				return field;
			}
			t = t.BaseType;
		}
		return null;
	}

	public static void Dump(string why)
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("state [").Append(why).Append("] ");
			stringBuilder.Append("LocalUser=").Append(User.LocalUser != null);
			stringBuilder.Append(", Spectator=").Append(Faction.Spectator != null);
			stringBuilder.Append(", GameParam=").Append(GameParam.instance != null);
			stringBuilder.Append(", clientObjects=").Append(ClientScene.objects.Count);
			stringBuilder.Append(", ClientActive=").Append(NetworkClient.active);
			stringBuilder.Append(", ServerActive=").Append(NetworkServer.active);
			stringBuilder.Append(", damagedTrees=").Append(DamagedTree.AllDamagedTrees.Count);
			object obj;
			Type type;
			IDictionary dictionary = BtcTerrainUndo.TreeDict(out obj, out type);
			stringBuilder.Append(", trees=").Append((dictionary != null) ? dictionary.Count : -1);
			foreach (FieldInfo fieldInfo in typeof(UIGameObserver).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
			{
				object value = fieldInfo.GetValue(null);
				bool flag = value == null;
				bool flag2 = !flag;
				bool flag3 = flag2;
				if (flag3)
				{
					FieldInfo fieldInfo2 = BtcTerrainUndo.FieldUp(value.GetType(), "creationMap");
					IEnumerable enumerable = (fieldInfo2 != null) ? (fieldInfo2.GetValue(value) as IEnumerable) : null;
					bool flag4 = enumerable == null;
					bool flag5 = !flag4;
					bool flag6 = flag5;
					if (flag6)
					{
						int num = 0;
						int num2 = 0;
						foreach (object obj2 in enumerable)
						{
							num++;
							UnityEngine.Object x = obj2 as UnityEngine.Object;
							bool flag7 = x != null && x == null;
							bool flag8 = flag7;
							bool flag9 = flag8;
							if (flag9)
							{
								num2++;
							}
						}
						stringBuilder.Append(", obs ").Append(fieldInfo.Name).Append("=").Append(num).Append("/dead ").Append(num2);
					}
				}
			}
			BtcTerrainUndo.Log(stringBuilder.ToString());
		}
		catch (Exception ex)
		{
			BtcTerrainUndo.Log("Dump: " + ex.Message);
		}
	}

	static BtcTerrainUndo()
	{
		BtcTerrainUndo._dirty = new HashSet<int>();
		BtcTerrainUndo._vrange = new Dictionary<int, int>();
	}

	private static ulong VoxelHash(out int solid, out long ms)
	{
		solid = 0;
		Stopwatch stopwatch = Stopwatch.StartNew();
		ulong num = 14695981039346656037UL;
		for (int i = 0; i < BtcTerrainUndo._w * BtcTerrainUndo._h; i++)
		{
			int num2 = (int)BtcTerrainUndo._bot[i];
			int num3 = (int)BtcTerrainUndo._top[i];
			bool flag = num2 >= num3;
			bool flag2 = !flag;
			bool flag3 = flag2;
			if (flag3)
			{
				int x = BtcTerrainUndo._x0 + i / BtcTerrainUndo._h;
				int z = BtcTerrainUndo._z0 + i % BtcTerrainUndo._h;
				int num4 = Mathf.Max(num2, 0);
				int num5 = Mathf.Min(num3, 254);
				for (int j = num4; j <= num5; j++)
				{
					bool flag4 = Voxel.Engine.IsVoxelNonEmpty(new XYZ(x, j, z));
					bool flag5 = flag4;
					bool flag6 = flag5;
					if (flag6)
					{
						solid++;
						num = (num ^ (ulong)((long)i << 8 | (long)j)) * 1099511628211UL;
					}
				}
			}
		}
		ms = stopwatch.ElapsedMilliseconds;
		return num;
	}

	private static bool DiagVoxels(string why, bool baseline)
	{
		bool flag = !BtcTerrainUndo.VoxelDiag;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool result;
		if (flag3)
		{
			result = true;
		}
		else
		{
			try
			{
				int num2;
				long num3;
				ulong num = BtcTerrainUndo.VoxelHash(out num2, out num3);
				if (baseline)
				{
					BtcTerrainUndo._vbase = num;
				}
				bool flag4 = num == BtcTerrainUndo._vbase;
				BtcTerrainUndo.Log(string.Concat(new object[]
				{
					"voxel hash (",
					why,
					"): ",
					num,
					", solid voxels ",
					num2,
					", ",
					num3,
					" ms",
					baseline ? "" : (flag4 ? " = SAME as baseline" : " = DIFFERENT from baseline")
				}));
				result = flag4;
			}
			catch (Exception ex)
			{
				BtcTerrainUndo.Log("voxel hash: " + ex.Message);
				result = true;
			}
		}
		return result;
	}

	private static void CaptureVoxels()
	{
		BtcTerrainUndo._hasVox = false;
		BtcTerrainUndo._vstart = null;
		BtcTerrainUndo._vt = null;
		BtcTerrainUndo._vv = null;
		try
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			int num = BtcTerrainUndo._w * BtcTerrainUndo._h;
			int[] array = new int[num];
			long num2 = 0L;
			for (int i = 0; i < num; i++)
			{
				int num3 = (int)BtcTerrainUndo._bot[i];
				int num4 = (int)BtcTerrainUndo._top[i];
				bool flag = num3 >= num4 || num3 > 254;
				bool flag2 = flag;
				bool flag3 = flag2;
				if (flag3)
				{
					array[i] = -1;
				}
				else
				{
					int num5 = Mathf.Max(num3 - 1, 0);
					int num6 = Mathf.Min(num4 + 1, 254);
					array[i] = (int)num2;
					num2 += (long)(num6 - num5 + 1);
					bool flag4 = num2 > 12000000L;
					bool flag5 = flag4;
					bool flag6 = flag5;
					if (flag6)
					{
						BtcTerrainUndo.Log("voxel snapshot too big (" + num2 + "+), voxel undo disabled");
						return;
					}
				}
			}
			byte[] array2 = new byte[num2];
			byte[] array3 = new byte[num2];
			for (int j = 0; j < num; j++)
			{
				int num7 = array[j];
				bool flag7 = num7 < 0;
				bool flag8 = !flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					int x = BtcTerrainUndo._x0 + j / BtcTerrainUndo._h;
					int z = BtcTerrainUndo._z0 + j % BtcTerrainUndo._h;
					int num8 = Mathf.Max((int)(BtcTerrainUndo._bot[j] - 1), 0);
					int num9 = Mathf.Min((int)(BtcTerrainUndo._top[j] + 1), 254);
					int num10 = num7;
					for (int k = num8; k <= num9; k++)
					{
						VoxelHandle voxelHandle = Voxel.Engine.GetVoxelHandle(new XYZ(x, k, z));
						array2[num10] = (byte)Mathf.Clamp(Voxel.Engine.GetVoxelType(voxelHandle), 0, 255);
						array3[num10] = (byte)Mathf.Clamp(Voxel.Engine.GetVoxelVolume(voxelHandle), 0, 255);
						num10++;
					}
				}
			}
			BtcTerrainUndo._vstart = array;
			BtcTerrainUndo._vt = array2;
			BtcTerrainUndo._vv = array3;
			BtcTerrainUndo._hasVox = true;
			BtcTerrainUndo.Log(string.Concat(new object[]
			{
				"voxel snapshot: ",
				num2,
				" voxels, ",
				stopwatch.ElapsedMilliseconds,
				" ms"
			}));
		}
		catch (Exception arg)
		{
			BtcTerrainUndo._hasVox = false;
			BtcTerrainUndo.Log("CaptureVoxels: " + arg);
		}
	}

	private static bool FixVoxel(int x, int y, int z, int bt, int bv)
	{
		VoxelHandle voxelHandle = Voxel.Engine.GetVoxelHandle(new XYZ(x, y, z));
		int voxelType = Voxel.Engine.GetVoxelType(voxelHandle);
		bool flag = bt == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool result;
		if (flag3)
		{
			bool flag4 = Voxel.Engine.IsVoxelNonEmpty(voxelHandle);
			bool flag5 = voxelType == 0 && !flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				result = false;
			}
			else
			{
				bool flag8 = voxelType == 0;
				bool flag9 = flag8 && BtcTerrainUndo._hid < 12;
				bool flag10 = flag9;
				if (flag10)
				{
					BtcTerrainUndo._hid++;
					BtcTerrainUndo.Log(string.Concat(new object[]
					{
						"hidden solid voxel at ",
						x,
						",",
						y,
						",",
						z,
						": type 0, non-empty, resources ",
						Voxel.Engine.GetTerrainResources(voxelHandle).TotalAmount
					}));
				}
				bool flag11 = !BtcTerrainUndo.RemoveUnbatched;
				bool flag12 = flag11;
				if (flag12)
				{
					Voxel.Engine.RemoveVoxel(voxelHandle);
				}
				BtcTerrainUndo._rem.Add(new XYZ(x, y, z));
				result = true;
			}
		}
		else
		{
			int num = Mathf.Clamp(Voxel.Engine.GetVoxelVolume(voxelHandle), 0, 255);
			bool flag13 = voxelType == bt && num == bv;
			bool flag14 = flag13;
			bool flag15 = flag14;
			if (flag15)
			{
				result = false;
			}
			else
			{
				bool flag16 = voxelType != bt;
				bool flag17 = flag16;
				bool flag18 = flag17;
				if (flag18)
				{
					Voxel.Engine.SetVoxelType(voxelHandle, bt);
				}
				Voxel.Engine.SetVoxelVolume(voxelHandle, bv);
				result = true;
			}
		}
		return result;
	}

	private static int _once;

	public static int Radius = 3;

	public static int Margin = 12;

	private static readonly List<int> _fixBot = new List<int>();

	private static int _dropped;

	public static bool FullScan = false;

	public static bool FillDiag = true;

	public static bool RemoveUnbatched = true;

	public static bool Nudging = true;

	private static int _hid;

	private static readonly List<XYZ> _rem = new List<XYZ>();

	private static bool _has;

	private static bool _live;

	private static bool _hooked;

	private static int _x0;

	private static int _z0;

	private static int _w;

	private static int _h;

	private static short[] _bot;

	private static short[] _top;

	private static byte[] _typ;

	private static byte[] _cru;

	private static float[] _crf;

	private static int _crustOnly;

	private static readonly HashSet<int> _dirty;

	private static Dictionary<object, int> _trees;

	private static int _sigLog;

	public static bool VoxelDiag = false;

	private static ulong _vbase;

	private static bool _hasVox;

	private static int[] _vstart;

	private static byte[] _vt;

	private static byte[] _vv;

	private static readonly Dictionary<int, int> _vrange;
}
}
