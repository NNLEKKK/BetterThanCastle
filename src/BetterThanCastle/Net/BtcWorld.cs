using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Brix.Engine;
using Brix.Game.AI;
using Brix.Utils.Locomotion;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcWorld
{
	public static float OreThr(VoxelTerrainType type, float threshold)
	{
		BtcWorld.EnsureCal();
		bool flag = !BtcWorld._useCal;
		bool flag2 = flag;
		float result;
		if (flag2)
		{
			result = threshold;
		}
		else
		{
			bool flag3 = type == VoxelTerrainType.Stone;
			bool flag4 = flag3;
			if (flag4)
			{
				result = (float)BtcWorld._thrStone;
			}
			else
			{
				bool flag5 = type == VoxelTerrainType.Iron;
				bool flag6 = flag5;
				if (flag6)
				{
					result = (float)BtcWorld._thrIron;
				}
				else
				{
					bool flag7 = type == VoxelTerrainType.OrangeCrystal;
					bool flag8 = flag7;
					if (flag8)
					{
						result = (float)BtcWorld._thrBrim;
					}
					else
					{
						result = threshold;
					}
				}
			}
		}
		return result;
	}

	public static VoxelTerrainType DeepFix(VoxelTerrainType result, Vector3 pos)
	{
		BtcWorld.CountReal(result, pos);
		return result;
	}

	private static float Hash(int x, int y, int z)
	{
		uint num = (uint)(x * 73856093 ^ y * 19349663 ^ z * 83492791);
		num ^= num >> 13;
		num *= 1274126177U;
		num ^= num >> 16;
		return (num & 16777215U) / 16777215f;
	}

	public static float Range(float vanilla, GameObject shooter, bool max)
	{
		float[] e = BtcGameplay.E;
		int num = max ? BtcGameplay.iRngMaxAll : BtcGameplay.iRngMinAll;
		BtcGameplay.Seen[num] = vanilla;
		int num2 = BtcGameplay.JobOf(shooter);
		int[] array = max ? BtcGameplay.iRngMaxJ : BtcGameplay.iRngMinJ;
		int num3 = (num2 >= 0 && num2 < array.Length) ? array[num2] : -1;
		bool flag = num3 >= 0;
		bool flag2 = flag;
		if (flag2)
		{
			BtcGameplay.Seen[num3] = vanilla;
			bool flag3 = BtcGameplay.AbsOn(e, num3);
			bool flag4 = flag3;
			if (flag4)
			{
				return e[BtcGameplay.Defs[num3].NumIdx];
			}
		}
		bool flag5 = BtcGameplay.AbsOn(e, num);
		bool flag6 = flag5;
		float result;
		if (flag6)
		{
			result = e[BtcGameplay.Defs[num].NumIdx];
		}
		else
		{
			float num4 = vanilla * e[num];
			bool flag7 = num3 >= 0;
			bool flag8 = flag7;
			if (flag8)
			{
				num4 *= e[num3];
			}
			result = num4;
		}
		return result;
	}

	public static bool InRange(ProjectileWeapon weapon, GameObject shooter, Vector3 target)
	{
		Vector3 a = weapon.transform.position - target;
		float magnitude = a.Flatten().magnitude;
		return magnitude >= BtcWorld.Range(weapon.MinRange, shooter, false) && magnitude <= BtcWorld.Range(weapon.MaxRange, shooter, true);
	}

	public static bool FarEnough(ProjectileWeapon weapon, GameObject shooter, Vector3 target)
	{
		float num = BtcWorld.Range(weapon.MinRange, shooter, false);
		return (target - weapon.transform.position).sqrMagnitude >= num * num;
	}

	private static void CountReal(VoxelTerrainType result, Vector3 pos)
	{
		int tickCount = Environment.TickCount;
		int globalSeed = VoxelNoiseConfigurator.GetGlobalSeed();
		bool flag = BtcWorld._sessId == 0 || globalSeed != BtcWorld._sessSeed || tickCount - BtcWorld._lastTick > 30000;
		if (flag)
		{
			BtcWorld.NewSession(globalSeed, tickCount);
		}
		BtcWorld._lastTick = tickCount;
		int num = (result == VoxelTerrainType.Stone) ? 1 : ((result == VoxelTerrainType.Iron) ? 2 : ((result == VoxelTerrainType.OrangeCrystal) ? 3 : ((result == VoxelTerrainType.Earth) ? 0 : 4)));
		int num2 = Mathf.Clamp(Mathf.FloorToInt(pos.y / 4f), 0, 127);
		Interlocked.Increment(ref BtcWorld._hist[num2, num]);
		int num3 = Mathf.Clamp(Mathf.FloorToInt(pos.x / 32f), 0, 63);
		int num4 = Mathf.Clamp(Mathf.FloorToInt(pos.z / 32f), 0, 63);
		Interlocked.Increment(ref BtcWorld._cell[num3, num4, num]);
		bool flag2 = pos.x < BtcWorld._minX;
		if (flag2)
		{
			BtcWorld._minX = pos.x;
		}
		bool flag3 = pos.x > BtcWorld._maxX;
		if (flag3)
		{
			BtcWorld._maxX = pos.x;
		}
		bool flag4 = pos.y < BtcWorld._minY;
		if (flag4)
		{
			BtcWorld._minY = pos.y;
		}
		bool flag5 = pos.y > BtcWorld._maxY;
		if (flag5)
		{
			BtcWorld._maxY = pos.y;
		}
		bool flag6 = pos.z < BtcWorld._minZ;
		if (flag6)
		{
			BtcWorld._minZ = pos.z;
		}
		bool flag7 = pos.z > BtcWorld._maxZ;
		if (flag7)
		{
			BtcWorld._maxZ = pos.z;
		}
		Interlocked.Increment(ref BtcWorld._cnt);
	}

	private static void EnsureCal()
	{
		float[] e = BtcGameplay.E;
		float num = e[BtcGameplay.iOreStone];
		float num2 = e[BtcGameplay.iOreIron];
		float num3 = e[BtcGameplay.iOreBrim];
		bool flag = BtcWorld._calValid && num == BtcWorld._calS && num2 == BtcWorld._calI && num3 == BtcWorld._calB && VoxelNoiseConfigurator.GetGlobalSeed() == BtcWorld._calSeed;
		bool flag2 = !flag;
		if (flag2)
		{
			object @lock = BtcWorld._lock;
			object obj = @lock;
			object obj2 = obj;
			lock (obj2)
			{
				bool flag3 = BtcWorld._calValid && num == BtcWorld._calS && num2 == BtcWorld._calI && num3 == BtcWorld._calB && VoxelNoiseConfigurator.GetGlobalSeed() == BtcWorld._calSeed;
				bool flag4 = !flag3;
				if (flag4)
				{
					bool flag5 = !VoxelNoiseConfigurator.IsInitialized();
					bool flag6 = !flag5;
					if (flag6)
					{
						bool flag7 = num == BtcGameplay.Defs[BtcGameplay.iOreStone].Val && num2 == BtcGameplay.Defs[BtcGameplay.iOreIron].Val && num3 == BtcGameplay.Defs[BtcGameplay.iOreBrim].Val;
						bool useCal = false;
						bool flag8 = !flag7;
						bool flag9 = flag8;
						if (flag9)
						{
							try
							{
								useCal = BtcWorld.Calibrate(num, num2, num3);
							}
							catch (Exception arg)
							{
								Debug.LogWarning("BtcWorld calibration failed: " + arg);
								useCal = false;
							}
						}
						BtcWorld._useCal = useCal;
						BtcWorld._calSeed = VoxelNoiseConfigurator.GetGlobalSeed();
						BtcWorld._calS = num;
						BtcWorld._calI = num2;
						BtcWorld._calB = num3;
						BtcWorld._calValid = true;
					}
				}
			}
		}
	}

	private static bool InRange(VoxelNoiseConfigurator.NoiseConfig c, int depth)
	{
		return !c.UseRange || (depth >= c.Start && depth <= c.End);
	}

	static BtcWorld()
	{
		BtcWorld._hist = new int[128, 5];
		BtcWorld._cell = new int[64, 64, 5];
		BtcWorld._minX = float.MaxValue;
		BtcWorld._maxX = float.MinValue;
		BtcWorld._minY = float.MaxValue;
		BtcWorld._maxY = float.MinValue;
		BtcWorld._minZ = float.MaxValue;
		BtcWorld._maxZ = float.MinValue;
	}

	public static void CachePaths()
	{
		try
		{
			BtcWorld._dir = BtcPaths.Debug;
			BtcWorld.EnsureTimer();
		}
		catch (Exception)
		{
		}
	}

	private static void WriteDbg(string name, string text)
	{
		try
		{
			bool flag = BtcWorld._dir == null;
			bool flag2 = flag;
			if (flag2)
			{
				Debug.LogWarning("BtcWorld: debug folder is not cached yet, cannot write " + name);
			}
			else
			{
				File.WriteAllText(System.IO.Path.Combine(BtcWorld._dir, name), text);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("BtcWorld: cannot write " + name + ": " + ex.Message);
		}
	}

	private static bool Calibrate(float pStone, float pIron, float pBrim)
	{
		List<VoxelNoiseConfigurator.NoiseConfig> allNoiseTypes = VoxelNoiseConfigurator.GetAllNoiseTypes();
		List<VoxelNoiseConfigurator.NoiseConfig> list = new List<VoxelNoiseConfigurator.NoiseConfig>();
		for (int i = 0; i < allNoiseTypes.Count; i++)
		{
			VoxelNoiseConfigurator.NoiseConfig noiseConfig = allNoiseTypes[i];
			bool flag = noiseConfig.Enabled && noiseConfig.NoiseComponent != null;
			bool flag2 = flag;
			if (flag2)
			{
				list.Add(noiseConfig);
			}
		}
		int count = list.Count;
		bool flag3 = count == 0;
		bool flag4 = flag3;
		bool result;
		if (flag4)
		{
			result = false;
		}
		else
		{
			int num = 131072;
			double[][] array = new double[count][];
			for (int j = 0; j < count; j++)
			{
				array[j] = new double[num];
			}
			int[] array2 = new int[num];
			int num2 = 0;
			for (int k = 0; k < 512; k += 8)
			{
				for (int l = 0; l < 512; l += 8)
				{
					for (int m = 0; m < 128; m += 4)
					{
						Vector3 positon = new Vector3((float)k, (float)m, (float)l);
						array2[num2] = (int)(BtcWorld.Hash(k, m, l) * 255.99f);
						for (int n = 0; n < count; n++)
						{
							array[n][num2] = list[n].NoiseComponent.GetValue(positon, 0);
						}
						num2++;
					}
				}
			}
			double[] array3 = new double[count];
			int[] array4 = new int[count + 1];
			for (int num3 = 0; num3 < num; num3++)
			{
				int num4 = -1;
				for (int num5 = 0; num5 < count; num5++)
				{
					bool flag5 = BtcWorld.InRange(list[num5], array2[num3]) && array[num5][num3] > (double)list[num5].Threshold;
					bool flag6 = flag5;
					if (flag6)
					{
						num4 = num5;
					}
				}
				array4[num4 + 1]++;
			}
			for (int num6 = 0; num6 < count; num6++)
			{
				int type = list[num6].Type;
				double num7 = (double)array4[num6 + 1] / (double)num;
				bool flag7 = type == 8;
				bool flag8 = type == 54;
				bool flag9 = type == 44;
				array3[num6] = -1.0;
				bool flag10 = flag7;
				bool flag11 = flag10;
				if (flag11)
				{
					array3[num6] = ((pStone < 0f) ? num7 : ((double)(pStone / 100f)));
				}
				bool flag12 = flag8;
				bool flag13 = flag12;
				if (flag13)
				{
					array3[num6] = ((pIron < 0f) ? num7 : ((double)(pIron / 100f)));
				}
				bool flag14 = flag9;
				bool flag15 = flag14;
				if (flag15)
				{
					array3[num6] = ((pBrim < 0f) ? num7 : ((double)(pBrim / 100f)));
				}
			}
			double num8 = 0.0;
			for (int num9 = 0; num9 < count; num9++)
			{
				bool flag16 = array3[num9] > 0.0;
				bool flag17 = flag16;
				if (flag17)
				{
					num8 += array3[num9];
				}
			}
			bool flag18 = num8 > 1.0;
			bool flag19 = flag18;
			if (flag19)
			{
				for (int num10 = 0; num10 < count; num10++)
				{
					bool flag20 = array3[num10] > 0.0;
					bool flag21 = flag20;
					if (flag21)
					{
						array3[num10] /= num8;
					}
				}
			}
			bool[] array5 = new bool[num];
			double[] array6 = new double[count];
			int[] array7 = new int[count];
			for (int num11 = count - 1; num11 >= 0; num11--)
			{
				VoxelNoiseConfigurator.NoiseConfig noiseConfig2 = list[num11];
				double[] array8 = array[num11];
				double num12 = (double)noiseConfig2.Threshold;
				bool flag22 = noiseConfig2.Type == 8 || noiseConfig2.Type == 54 || noiseConfig2.Type == 44;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					List<double> list2 = new List<double>(num);
					for (int num13 = 0; num13 < num; num13++)
					{
						bool flag25 = !array5[num13] && BtcWorld.InRange(noiseConfig2, array2[num13]);
						bool flag26 = flag25;
						if (flag26)
						{
							list2.Add(array8[num13]);
						}
					}
					int num14 = (int)Math.Round(array3[num11] * (double)num);
					bool flag27 = num14 <= 0;
					bool flag28 = flag27;
					if (flag28)
					{
						num12 = 1000.0;
					}
					else
					{
						bool flag29 = num14 >= list2.Count;
						bool flag30 = flag29;
						if (flag30)
						{
							num12 = -1000.0;
						}
						else
						{
							list2.Sort();
							double num15 = list2[list2.Count - num14];
							double num16 = list2[list2.Count - num14 - 1];
							num12 = ((num15 > num16) ? ((num15 + num16) * 0.5) : num16);
						}
					}
				}
				array6[num11] = num12;
				for (int num17 = 0; num17 < num; num17++)
				{
					bool flag31 = !array5[num17] && BtcWorld.InRange(noiseConfig2, array2[num17]) && array8[num17] > num12;
					bool flag32 = flag31;
					if (flag32)
					{
						array5[num17] = true;
						array7[num11]++;
					}
				}
			}
			for (int num18 = 0; num18 < count; num18++)
			{
				int type2 = list[num18].Type;
				bool flag33 = type2 == 8;
				bool flag34 = flag33;
				if (flag34)
				{
					BtcWorld._thrStone = array6[num18];
				}
				bool flag35 = type2 == 54;
				bool flag36 = flag35;
				if (flag36)
				{
					BtcWorld._thrIron = array6[num18];
				}
				bool flag37 = type2 == 44;
				bool flag38 = flag37;
				if (flag38)
				{
					BtcWorld._thrBrim = array6[num18];
				}
			}
			try
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine(string.Concat(new object[]
				{
					"BtcWorld calibration ",
					DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
					" seed=",
					BtcWorld._calSeed,
					" samples=",
					num
				}));
				for (int num19 = 0; num19 < count; num19++)
				{
					StringBuilder stringBuilder2 = stringBuilder;
					object[] array9 = new object[10];
					int num20 = 0;
					VoxelTerrainType type3 = (VoxelTerrainType)list[num19].Type;
					array9[num20] = type3.ToString();
					array9[1] = " vanillaThr=";
					array9[2] = list[num19].Threshold.ToString("R", CultureInfo.InvariantCulture);
					array9[3] = " newThr=";
					array9[4] = array6[num19].ToString("F4", CultureInfo.InvariantCulture);
					array9[5] = " target=";
					array9[6] = (array3[num19] * 100.0).ToString("F2", CultureInfo.InvariantCulture);
					array9[7] = "% achieved=";
					array9[8] = (100.0 * (double)array7[num19] / (double)num).ToString("F2", CultureInfo.InvariantCulture);
					array9[9] = "%";
					stringBuilder2.AppendLine(string.Concat(array9));
				}
				BtcWorld.WriteDbg("btc_worldcal.txt", stringBuilder.ToString());
			}
			catch (Exception)
			{
			}
			result = true;
		}
		return result;
	}

	private static void NewSession(int seed, int tick)
	{
		object rlock = BtcWorld._rlock;
		lock (rlock)
		{
			bool flag = BtcWorld._sessId != 0 && seed == BtcWorld._sessSeed && tick - BtcWorld._lastTick <= 30000;
			if (flag)
			{
				return;
			}
			Array.Clear(BtcWorld._hist, 0, BtcWorld._hist.Length);
			Array.Clear(BtcWorld._cell, 0, BtcWorld._cell.Length);
			BtcWorld._cnt = 0;
			BtcWorld._minX = float.MaxValue;
			BtcWorld._maxX = float.MinValue;
			BtcWorld._minY = float.MaxValue;
			BtcWorld._maxY = float.MinValue;
			BtcWorld._minZ = float.MaxValue;
			BtcWorld._maxZ = float.MinValue;
			BtcWorld._sessSeed = seed;
			BtcWorld._sessStart = DateTime.Now;
			BtcWorld._lastTick = tick;
			BtcWorld._wCnt = -1;
			BtcWorld._wFin = false;
			BtcWorld._wTick = tick;
			BtcWorld._sessId++;
		}
		BtcWorld.EnsureTimer();
	}

	private static void EnsureTimer()
	{
		bool flag = BtcWorld._timer != null;
		if (!flag)
		{
			object rlock = BtcWorld._rlock;
			lock (rlock)
			{
				bool flag2 = BtcWorld._timer == null;
				if (flag2)
				{
					BtcWorld._timer = new Timer(new TimerCallback(BtcWorld.Tick), null, 1000, 1000);
				}
			}
		}
	}

	private static void Tick(object state)
	{
		try
		{
			int cnt = BtcWorld._cnt;
			bool flag = BtcWorld._sessId == 0 || cnt == 0;
			if (!flag)
			{
				int tickCount = Environment.TickCount;
				bool flag2 = tickCount - BtcWorld._lastTick > 1500;
				bool flag3 = cnt != BtcWorld._wCnt || (flag2 && !BtcWorld._wFin);
				bool flag4 = flag3 && (flag2 || tickCount - BtcWorld._wTick > 4000);
				if (flag4)
				{
					BtcWorld.WriteReal(flag2);
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void WriteReal(bool fin)
	{
		try
		{
			object wlock = BtcWorld._wlock;
			lock (wlock)
			{
				string format = "yyyy-MM-dd HH:mm:ss";
				CultureInfo invariantCulture = CultureInfo.InvariantCulture;
				int tickCount = Environment.TickCount;
				int num = tickCount - BtcWorld._lastTick;
				int cnt = BtcWorld._cnt;
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine("BtcWorld real deep layer census (every deep voxel generated in this map session)");
				stringBuilder.AppendLine(string.Concat(new object[]
				{
					"session #",
					BtcWorld._sessId,
					"  seed=",
					BtcWorld._sessSeed,
					"  started ",
					BtcWorld._sessStart.ToString(format, invariantCulture),
					"  last voxel ",
					DateTime.Now.AddMilliseconds(-(double)num).ToString(format, invariantCulture)
				}));
				stringBuilder.AppendLine("written " + DateTime.Now.ToString(format, invariantCulture) + (fin ? ("  state=FINAL (no new voxels for " + ((float)num / 1000f).ToString("F1", invariantCulture) + " s)") : "  state=still generating (not final)"));
				stringBuilder.AppendLine("voxels=" + cnt);
				float[] e = BtcGameplay.E;
				string text = BtcWorld._useCal ? string.Concat(new string[]
				{
					"on thr stone/iron/brim=",
					BtcWorld._thrStone.ToString("F4", invariantCulture),
					"/",
					BtcWorld._thrIron.ToString("F4", invariantCulture),
					"/",
					BtcWorld._thrBrim.ToString("F4", invariantCulture)
				}) : "off (vanilla thresholds)";
				stringBuilder.AppendLine(string.Concat(new string[]
				{
					"rows stone/iron/brim=",
					e[BtcGameplay.iOreStone].ToString("R", invariantCulture),
					"/",
					e[BtcGameplay.iOreIron].ToString("R", invariantCulture),
					"/",
					e[BtcGameplay.iOreBrim].ToString("R", invariantCulture),
					" (-1 = map)  calibration ",
					text
				}));
				stringBuilder.AppendLine(string.Concat(new object[]
				{
					"extent x ",
					BtcWorld._minX,
					"..",
					BtcWorld._maxX,
					"  y ",
					BtcWorld._minY,
					"..",
					BtcWorld._maxY,
					"  z ",
					BtcWorld._minZ,
					"..",
					BtcWorld._maxZ
				}));
				stringBuilder.AppendLine("row(y/4): n  earth%  stone%  iron%  brimstone%  other%");
				long[] array = new long[5];
				for (int i = 0; i < 128; i++)
				{
					long num2 = 0L;
					for (int j = 0; j < 5; j++)
					{
						num2 += (long)BtcWorld._hist[i, j];
						array[j] += (long)BtcWorld._hist[i, j];
					}
					bool flag = num2 != 0L;
					if (flag)
					{
						StringBuilder stringBuilder2 = new StringBuilder();
						for (int k = 0; k < 5; k++)
						{
							stringBuilder2.Append(((double)BtcWorld._hist[i, k] * 100.0 / (double)num2).ToString("F2", invariantCulture)).Append("  ");
						}
						stringBuilder.AppendLine(string.Concat(new object[]
						{
							"y ",
							i * 4,
							"-",
							i * 4 + 3,
							": ",
							num2,
							"  ",
							stringBuilder2.ToString()
						}));
					}
				}
				long num3 = 0L;
				for (int l = 0; l < 5; l++)
				{
					num3 += array[l];
				}
				StringBuilder stringBuilder3 = new StringBuilder();
				for (int m = 0; m < 5; m++)
				{
					stringBuilder3.Append(((double)array[m] * 100.0 / (double)Math.Max(1L, num3)).ToString("F2", invariantCulture)).Append("  ");
				}
				stringBuilder.AppendLine(string.Concat(new object[]
				{
					"ALL: ",
					num3,
					"  ",
					stringBuilder3.ToString(),
					"  (histogram total vs voxels counter: ",
					num3,
					" / ",
					cnt,
					")"
				}));
				stringBuilder.AppendLine("xz cells of 32 blocks: x z : n  earth%  stone%  iron%  brimstone%  other%");
				for (int n = 0; n < 64; n++)
				{
					for (int num4 = 0; num4 < 64; num4++)
					{
						long num5 = 0L;
						for (int num6 = 0; num6 < 5; num6++)
						{
							num5 += (long)BtcWorld._cell[n, num4, num6];
						}
						bool flag2 = num5 != 0L;
						if (flag2)
						{
							StringBuilder stringBuilder4 = new StringBuilder();
							for (int num7 = 0; num7 < 5; num7++)
							{
								stringBuilder4.Append(((double)BtcWorld._cell[n, num4, num7] * 100.0 / (double)num5).ToString("F1", invariantCulture)).Append("  ");
							}
							stringBuilder.AppendLine(string.Concat(new object[]
							{
								n * 32,
								" ",
								num4 * 32,
								" : ",
								num5,
								"  ",
								stringBuilder4.ToString()
							}));
						}
					}
				}
				string text2 = stringBuilder.ToString();
				bool flag3 = cnt >= 1000;
				if (flag3)
				{
					BtcWorld.WriteDbg("btc_worldreal_s" + BtcWorld._sessId + ".txt", text2);
				}
				bool flag4 = cnt >= BtcWorld._bestCnt;
				if (flag4)
				{
					BtcWorld._bestCnt = cnt;
					BtcWorld.WriteDbg("btc_worldreal.txt", text2);
				}
				BtcWorld._wCnt = cnt;
				BtcWorld._wFin = fin;
				BtcWorld._wTick = tickCount;
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("BtcWorld: census write failed: " + ex.Message);
		}
	}

	private static bool _useCal;

	private static bool _calValid;

	private static int _calSeed;

	private static float _calS;

	private static float _calI;

	private static float _calB;

	private static double _thrStone;

	private static double _thrIron;

	private static double _thrBrim;

	private static readonly int[,] _hist;

	private static int _cnt;

	private static float _minX;

	private static float _maxX;

	private static float _minY;

	private static float _maxY;

	private static float _minZ;

	private static float _maxZ;

	private static readonly object _lock = new object();

	private static string _dir;

	private static readonly int[,,] _cell;

	private static readonly object _rlock = new object();

	private static readonly object _wlock = new object();

	private static Timer _timer;

	private static int _sessId;

	private static int _sessSeed;

	private static DateTime _sessStart;

	private static int _lastTick;

	private static int _wCnt;

	private static bool _wFin;

	private static int _wTick;

	private static int _bestCnt;
}
}
