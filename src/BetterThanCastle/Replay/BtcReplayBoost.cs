using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Brix.Components;
using Brix.Engine;
using Brix.Engine.Nature;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcReplayBoost
{
	private static string T(string en, string ru)
	{
		return BtcLang.Russian ? ru : en;
	}

	private static void EnsureCfg()
	{
		bool cfg = BtcReplayBoost._cfg;
		bool flag = !cfg;
		if (flag)
		{
			BtcReplayBoost._cfg = true;
			try
			{
				BtcPaths.Init();
				BtcReplayBoost._fastNature = (BtcPaths.ReadKey("replay_rewind.txt", "FastNature", "1") != "0");
				BtcReplayBoost._fastWaits = (BtcPaths.ReadKey("replay_rewind.txt", "FastWaits", "1") != "0");
				int num;
				bool flag2 = int.TryParse(BtcPaths.ReadKey("replay_rewind.txt", "BudgetMs", "100"), NumberStyles.Integer, CultureInfo.InvariantCulture, out num) && num >= 20 && num <= 1000;
				bool flag3 = flag2;
				if (flag3)
				{
					BtcReplayBoost._budgetMs = num;
				}
			}
			catch (Exception)
			{
			}
		}
	}

	private static void SaveCfg()
	{
		try
		{
			BtcPaths.Init();
			File.WriteAllText(BtcPaths.Config("replay_rewind.txt"), string.Concat(new string[]
			{
				"# BtcReplayBoost settings (managed from the Replay tab). 1 = on, 0 = off.\r\n",
				"# FastNature: rewind restores grass/rocks/ore as a diff against the start of the match instead of reloading them.\r\n",
				"FastNature=",
				BtcReplayBoost._fastNature ? "1" : "0",
				"\r\n# FastWaits: short pauses between the rewind steps (0.05/0.1/0.1 s instead of 0.15/0.4/0.3 s).\r\n",
				"FastWaits=",
				BtcReplayBoost._fastWaits ? "1" : "0",
				"\r\n# BudgetMs: milliseconds of stream feeding per frame while rewinding (forward seek always uses 40).\r\n",
				"BudgetMs=",
				BtcReplayBoost._budgetMs.ToString(CultureInfo.InvariantCulture),
				"\r\n"
			}));
		}
		catch (Exception)
		{
		}
	}

	public static bool GetFastNature()
	{
		BtcReplayBoost.EnsureCfg();
		return BtcReplayBoost._fastNature;
	}

	public static void SetFastNature(bool v)
	{
		BtcReplayBoost.EnsureCfg();
		BtcReplayBoost._fastNature = v;
		BtcReplayBoost.SaveCfg();
	}

	public static bool GetFastWaits()
	{
		BtcReplayBoost.EnsureCfg();
		return BtcReplayBoost._fastWaits;
	}

	public static void SetFastWaits(bool v)
	{
		BtcReplayBoost.EnsureCfg();
		BtcReplayBoost._fastWaits = v;
		BtcReplayBoost.SaveCfg();
	}

	public static int GetBudgetMs()
	{
		BtcReplayBoost.EnsureCfg();
		return BtcReplayBoost._budgetMs;
	}

	public static void StepBudget(int dir)
	{
		BtcReplayBoost.EnsureCfg();
		int num = 0;
		for (int i = 0; i < BtcReplayBoost.BudgetSteps.Length; i++)
		{
			bool flag = BtcReplayBoost.BudgetSteps[i] <= BtcReplayBoost._budgetMs;
			bool flag2 = flag;
			if (flag2)
			{
				num = i;
			}
		}
		num = Mathf.Clamp(num + dir, 0, BtcReplayBoost.BudgetSteps.Length - 1);
		BtcReplayBoost._budgetMs = BtcReplayBoost.BudgetSteps[num];
		BtcReplayBoost.SaveCfg();
	}

	public static float Wait0
	{
		get
		{
			BtcReplayBoost.EnsureCfg();
			return BtcReplayBoost._fastWaits ? 0.05f : 0.15f;
		}
	}

	public static float Wait1
	{
		get
		{
			BtcReplayBoost.EnsureCfg();
			return BtcReplayBoost._fastWaits ? 0.1f : 0.4f;
		}
	}

	public static float Wait3
	{
		get
		{
			BtcReplayBoost.EnsureCfg();
			return BtcReplayBoost._fastWaits ? 0.1f : 0.3f;
		}
	}

	public static long Budget(bool rewinding)
	{
		bool flag = !rewinding;
		bool flag2 = flag;
		long result;
		if (flag2)
		{
			result = 40L;
		}
		else
		{
			BtcReplayBoost.EnsureCfg();
			result = (long)BtcReplayBoost._budgetMs;
		}
		return result;
	}

	private static void Log(string text)
	{
		try
		{
			UnityEngine.Debug.Log("[BtcReplayBoost] " + text);
			BtcPaths.Init();
			File.AppendAllText(BtcPaths.DebugFile("replay_log.txt"), DateTime.Now.ToString("HH:mm:ss") + " [boost] " + text + "\r\n");
		}
		catch (Exception)
		{
		}
	}

	public static void LogRewindTotal(float seconds)
	{
		BtcReplayBoost.Log("rewind total " + seconds.ToString("0.00", CultureInfo.InvariantCulture) + " s");
	}

	private static Dictionary<XYZ, BtcReplayBoost.PlantV> ReadPlants()
	{
		PlantSerializedData[] serializedData = BrixSingleton<PlantRegistry>.Instance.SerializedData;
		Dictionary<XYZ, BtcReplayBoost.PlantV> dictionary = new Dictionary<XYZ, BtcReplayBoost.PlantV>(serializedData.Length);
		for (int i = 0; i < serializedData.Length; i++)
		{
			BtcReplayBoost.PlantV value = default(BtcReplayBoost.PlantV);
			value.t = serializedData[i].t;
			value.s = serializedData[i].s;
			dictionary[new XYZ(serializedData[i].x, serializedData[i].y, serializedData[i].z)] = value;
		}
		return dictionary;
	}

	private static Dictionary<BtcReplayBoost.BKey, BtcReplayBoost.BVal> ReadBoulders()
	{
		Dictionary<BtcReplayBoost.BKey, BtcReplayBoost.BVal> dictionary = new Dictionary<BtcReplayBoost.BKey, BtcReplayBoost.BVal>();
		List<XYZ> list = new List<XYZ>();
		BoulderRegistry.AddAllBoulderPositionsToList(ref list);
		HashSet<BoulderData> hashSet = new HashSet<BoulderData>();
		for (int i = 0; i < list.Count; i++)
		{
			BoulderData boulder = BoulderRegistry.GetBoulder(list[i]);
			bool flag = boulder == null || !hashSet.Add(boulder);
			bool flag2 = !flag;
			if (flag2)
			{
				BtcReplayBoost.BKey key = default(BtcReplayBoost.BKey);
				Vector3 centerPos = boulder.CenterPos;
				Vector3 offset = boulder.Offset;
				key.cx = centerPos.x;
				key.cy = centerPos.y;
				key.cz = centerPos.z;
				key.ox = offset.x;
				key.oy = offset.y;
				key.oz = offset.z;
				key.sz = (int)boulder.BoulderSize;
				BtcReplayBoost.BVal value = new BtcReplayBoost.BVal
				{
					growth = boulder.Growth,
					root = boulder.RootPos,
					damage = BoulderRegistry.GetDamage(boulder.RootPos)
				};
				dictionary[key] = value;
			}
		}
		return dictionary;
	}

	private static Dictionary<XYZ, BtcReplayBoost.MV> ReadMin(MineralSerializedData[] a, bool iron)
	{
		Dictionary<XYZ, BtcReplayBoost.MV> dictionary = new Dictionary<XYZ, BtcReplayBoost.MV>(a.Length);
		for (int i = 0; i < a.Length; i++)
		{
			BtcReplayBoost.MV value = default(BtcReplayBoost.MV);
			value.g = a[i].growth;
			value.v = a[i].variation;
			value.d = (iron ? IronRegistry.GetDamage(a[i].position) : PyriteRegistry.GetDamage(a[i].position));
			dictionary[a[i].position] = value;
		}
		return dictionary;
	}

	private static bool MinDiffers(Dictionary<XYZ, BtcReplayBoost.MV> cur, Dictionary<XYZ, BtcReplayBoost.MV> snap)
	{
		bool flag = cur.Count != snap.Count;
		bool flag2 = flag;
		bool result;
		if (flag2)
		{
			result = true;
		}
		else
		{
			foreach (KeyValuePair<XYZ, BtcReplayBoost.MV> keyValuePair in cur)
			{
				BtcReplayBoost.MV mv;
				bool flag3 = !snap.TryGetValue(keyValuePair.Key, out mv);
				bool flag4 = flag3;
				if (flag4)
				{
					return true;
				}
				bool flag5 = mv.v != keyValuePair.Value.v || Math.Abs(mv.g - keyValuePair.Value.g) > 0.0001f || Math.Abs(mv.d - keyValuePair.Value.d) > 0.0001f;
				bool flag6 = flag5;
				if (flag6)
				{
					return true;
				}
			}
			result = false;
		}
		return result;
	}

	public static void Capture()
	{
		BtcReplayBoost.EnsureCfg();
		BtcReplayBoost._has = false;
		bool flag = !BtcReplayBoost._fastNature;
		bool flag2 = !flag;
		if (flag2)
		{
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				BtcReplayBoost._plants = BtcReplayBoost.ReadPlants();
				BtcReplayBoost._boulders = BtcReplayBoost.ReadBoulders();
				BtcReplayBoost._ironArr = IronRegistry.GetSerializedMinerals();
				BtcReplayBoost._pyrArr = PyriteRegistry.GetSerializedMinerals();
				BtcReplayBoost._iron = BtcReplayBoost.ReadMin(BtcReplayBoost._ironArr, true);
				BtcReplayBoost._pyr = BtcReplayBoost.ReadMin(BtcReplayBoost._pyrArr, false);
				BtcReplayBoost._has = true;
				BtcReplayBoost.Log(string.Concat(new object[]
				{
					"capture: plants ",
					BtcReplayBoost._plants.Count,
					", boulders ",
					BtcReplayBoost._boulders.Count,
					", iron ",
					BtcReplayBoost._iron.Count,
					", pyrite ",
					BtcReplayBoost._pyr.Count,
					", ",
					stopwatch.ElapsedMilliseconds,
					" ms"
				}));
			}
			catch (Exception ex)
			{
				BtcReplayBoost._has = false;
				BtcReplayBoost.Log("capture failed: " + ex.Message);
			}
		}
	}

	public static void Clear()
	{
		BtcReplayBoost._has = false;
		BtcReplayBoost._plants = null;
		BtcReplayBoost._boulders = null;
		BtcReplayBoost._ironArr = null;
		BtcReplayBoost._pyrArr = null;
		BtcReplayBoost._iron = null;
		BtcReplayBoost._pyr = null;
	}

	public static bool RestoreNature()
	{
		BtcReplayBoost.EnsureCfg();
		bool flag = !BtcReplayBoost._fastNature || !BtcReplayBoost._has;
		bool flag2 = flag;
		bool result;
		if (flag2)
		{
			BtcReplayBoost.Log("restore: " + (BtcReplayBoost._fastNature ? "no baseline" : "disabled") + ", slow path");
			result = false;
		}
		else
		{
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				Dictionary<XYZ, BtcReplayBoost.PlantV> dictionary = BtcReplayBoost.ReadPlants();
				Dictionary<BtcReplayBoost.BKey, BtcReplayBoost.BVal> dictionary2 = BtcReplayBoost.ReadBoulders();
				Dictionary<XYZ, BtcReplayBoost.MV> dictionary3 = BtcReplayBoost.ReadMin(IronRegistry.GetSerializedMinerals(), true);
				Dictionary<XYZ, BtcReplayBoost.MV> dictionary4 = BtcReplayBoost.ReadMin(PyriteRegistry.GetSerializedMinerals(), false);
				List<XYZ> list = new List<XYZ>();
				List<KeyValuePair<XYZ, BtcReplayBoost.PlantV>> list2 = new List<KeyValuePair<XYZ, BtcReplayBoost.PlantV>>();
				foreach (KeyValuePair<XYZ, BtcReplayBoost.PlantV> keyValuePair in dictionary)
				{
					BtcReplayBoost.PlantV plantV;
					bool flag3 = !BtcReplayBoost._plants.TryGetValue(keyValuePair.Key, out plantV);
					bool flag4 = flag3;
					if (flag4)
					{
						list.Add(keyValuePair.Key);
					}
					else
					{
						bool flag5 = plantV.t != keyValuePair.Value.t || Math.Abs(plantV.s - keyValuePair.Value.s) > 0.0001f;
						bool flag6 = flag5;
						if (flag6)
						{
							list.Add(keyValuePair.Key);
							list2.Add(new KeyValuePair<XYZ, BtcReplayBoost.PlantV>(keyValuePair.Key, plantV));
						}
					}
				}
				foreach (KeyValuePair<XYZ, BtcReplayBoost.PlantV> item in BtcReplayBoost._plants)
				{
					bool flag7 = !dictionary.ContainsKey(item.Key);
					bool flag8 = flag7;
					if (flag8)
					{
						list2.Add(item);
					}
				}
				List<XYZ> list3 = new List<XYZ>();
				List<BtcReplayBoost.BKey> list4 = new List<BtcReplayBoost.BKey>();
				foreach (KeyValuePair<BtcReplayBoost.BKey, BtcReplayBoost.BVal> keyValuePair2 in dictionary2)
				{
					BtcReplayBoost.BVal bval;
					bool flag9 = !BtcReplayBoost._boulders.TryGetValue(keyValuePair2.Key, out bval);
					bool flag10 = flag9;
					if (flag10)
					{
						list3.Add(keyValuePair2.Value.root);
					}
					else
					{
						bool flag11 = Math.Abs(bval.growth - keyValuePair2.Value.growth) > 0.0001f || Math.Abs(bval.damage - keyValuePair2.Value.damage) > 0.0001f;
						bool flag12 = flag11;
						if (flag12)
						{
							list3.Add(keyValuePair2.Value.root);
							list4.Add(keyValuePair2.Key);
						}
					}
				}
				foreach (KeyValuePair<BtcReplayBoost.BKey, BtcReplayBoost.BVal> keyValuePair3 in BtcReplayBoost._boulders)
				{
					bool flag13 = !dictionary2.ContainsKey(keyValuePair3.Key);
					bool flag14 = flag13;
					if (flag14)
					{
						list4.Add(keyValuePair3.Key);
					}
				}
				bool flag15 = BtcReplayBoost.MinDiffers(dictionary3, BtcReplayBoost._iron);
				bool flag16 = BtcReplayBoost.MinDiffers(dictionary4, BtcReplayBoost._pyr);
				long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
				for (int i = 0; i < list.Count; i++)
				{
					PlantRegistry.Remove(list[i]);
				}
				for (int j = 0; j < list3.Count; j++)
				{
					BoulderRegistry.Remove(list3[j]);
				}
				bool flag17 = flag15;
				bool flag18 = flag17;
				if (flag18)
				{
					foreach (XYZ position in dictionary3.Keys)
					{
						IronRegistry.Remove(position);
					}
				}
				bool flag19 = flag16;
				bool flag20 = flag19;
				if (flag20)
				{
					foreach (XYZ position2 in dictionary4.Keys)
					{
						PyriteRegistry.Remove(position2);
					}
				}
				bool flag21 = BtcTerrainUndo.RestoreTrees();
				bool flag22 = !flag21;
				bool flag23 = flag22;
				if (flag23)
				{
					BtcReplayBoost.Log("restore: trees failed, slow path");
					result = false;
				}
				else
				{
					int num = 0;
					for (int k = 0; k < list4.Count; k++)
					{
						BtcReplayBoost.BKey bkey = list4[k];
						BtcReplayBoost.BVal bval2 = BtcReplayBoost._boulders[bkey];
						BoulderRegistry.Add(new Vector3(bkey.cx, bkey.cy, bkey.cz), bval2.growth, new Vector3(bkey.ox, bkey.oy, bkey.oz), (BoulderSize)bkey.sz);
						bool flag24 = !BoulderRegistry.HasBoulders(bval2.root);
						bool flag25 = flag24;
						if (flag25)
						{
							num++;
						}
					}
					bool flag26 = flag15;
					bool flag27 = flag26;
					if (flag27)
					{
						IronRegistry.BuildMineralsFromSerialized(BtcReplayBoost._ironArr);
					}
					bool flag28 = flag16;
					bool flag29 = flag28;
					if (flag29)
					{
						PyriteRegistry.BuildMineralsFromSerialized(BtcReplayBoost._pyrArr);
					}
					int num2 = 0;
					for (int l = 0; l < list2.Count; l++)
					{
						PlantRegistry.Add(list2[l].Key, list2[l].Value.t, list2[l].Value.s);
						bool flag30 = !PlantRegistry.IsPlant(list2[l].Key);
						bool flag31 = flag30;
						if (flag31)
						{
							num2++;
						}
					}
					BtcReplayBoost.Log(string.Concat(new object[]
					{
						"restore: plants -",
						list.Count,
						" +",
						list2.Count,
						" (skipped ",
						num2,
						"), boulders -",
						list3.Count,
						" +",
						list4.Count,
						" (skipped ",
						num,
						"), iron ",
						flag15 ? "rebuilt" : "same",
						", pyrite ",
						flag16 ? "rebuilt" : "same",
						", scan ",
						elapsedMilliseconds,
						" ms, total ",
						stopwatch.ElapsedMilliseconds,
						" ms"
					}));
					result = true;
				}
			}
			catch (Exception arg)
			{
				BtcReplayBoost.Log("restore failed, slow path: " + arg);
				result = false;
			}
		}
		return result;
	}

	public static bool IsReplay()
	{
		bool result;
		try
		{
			result = BtcReplay.Active;
		}
		catch (Exception)
		{
			result = false;
		}
		return result;
	}

	private static string Clock(float sec)
	{
		int num = Mathf.Max(0, Mathf.RoundToInt(sec));
		bool flag = num >= 3600;
		bool flag2 = flag;
		string result;
		if (flag2)
		{
			result = string.Concat(new string[]
			{
				(num / 3600).ToString(),
				":",
				(num / 60 % 60).ToString("00"),
				":",
				(num % 60).ToString("00")
			});
		}
		else
		{
			result = (num / 60).ToString("00") + ":" + (num % 60).ToString("00");
		}
		return result;
	}

	public static void DiscordTexts(string map, string format, out string details, out string state, out string smallImage, out string smallText)
	{
		bool flag = false;
		string text = "00:00";
		try
		{
			flag = BtcReplay.PlayPaused;
			text = BtcReplayBoost.Clock(BtcReplay.PlayClock);
			bool flag2 = BtcReplay.PlayTotal > 0.5f;
			bool flag3 = flag2;
			if (flag3)
			{
				text = text + " / " + BtcReplayBoost.Clock(BtcReplay.PlayTotal);
			}
		}
		catch (Exception)
		{
		}
		details = (string.IsNullOrEmpty(map) ? "Watching replay" : ("Watching replay • " + map));
		string text2 = string.IsNullOrEmpty(format) ? text : (format + " • " + text);
		state = (flag ? ("Paused • " + text2) : text2);
		smallImage = (flag ? "pause" : "spectator");
		smallText = (flag ? "Replay (paused)" : "Replay");
	}

	public static string UiFastNature()
	{
		return BtcReplayBoost.T("Fast rewind: restore grass, rocks and ore as a diff", "Быстрая перемотка назад: трава, камни и руда откатываются диффом");
	}

	public static string UiFastWaits()
	{
		return BtcReplayBoost.T("Short pauses between rewind steps", "Короткие паузы между шагами перемотки");
	}

	public static string UiBudget()
	{
		return BtcReplayBoost.T("Rewind speed: ms of work per frame", "Скорость перемотки назад: мс работы за кадр");
	}

	public static string UiNote()
	{
		return BtcReplayBoost.T("Higher values make rewinding faster but the picture stutters while it runs. If something looks wrong after a rewind, turn the diff off: the slow full reload is used again. The timing of every rewind is written to replay_log.txt.", "Чем больше значение, тем быстрее перемотка, но картинка дёргается, пока она идёт. Если после перемотки что-то выглядит неправильно, выключите дифф: вернётся медленная полная перезагрузка. Время каждой перемотки пишется в replay_log.txt.");
	}

	private static bool _cfg;

	private static bool _fastNature = true;

	private static bool _fastWaits = true;

	private static int _budgetMs = 100;

	private static readonly int[] BudgetSteps = new int[]
	{
		40,
		60,
		100,
		150,
		250,
		400
	};

	private static bool _has;

	private static Dictionary<XYZ, BtcReplayBoost.PlantV> _plants;

	private static Dictionary<BtcReplayBoost.BKey, BtcReplayBoost.BVal> _boulders;

	private static MineralSerializedData[] _ironArr;

	private static MineralSerializedData[] _pyrArr;

	private static Dictionary<XYZ, BtcReplayBoost.MV> _iron;

	private static Dictionary<XYZ, BtcReplayBoost.MV> _pyr;

	private struct PlantV
	{
		public int t;

		public float s;
	}

	private struct BKey : IEquatable<BtcReplayBoost.BKey>
	{
		public bool Equals(BtcReplayBoost.BKey o)
		{
			return this.cx == o.cx && this.cy == o.cy && this.cz == o.cz && this.ox == o.ox && this.oy == o.oy && this.oz == o.oz && this.sz == o.sz;
		}

		public override bool Equals(object obj)
		{
			return obj is BtcReplayBoost.BKey && this.Equals((BtcReplayBoost.BKey)obj);
		}

		public override int GetHashCode()
		{
			int num = this.cx.GetHashCode();
			num = num * 31 + this.cy.GetHashCode();
			num = num * 31 + this.cz.GetHashCode();
			num = num * 31 + this.ox.GetHashCode();
			num = num * 31 + this.oy.GetHashCode();
			num = num * 31 + this.oz.GetHashCode();
			return num * 31 + this.sz;
		}

		public float cx;

		public float cy;

		public float cz;

		public float ox;

		public float oy;

		public float oz;

		public int sz;
	}

	private struct BVal
	{
		public float growth;

		public float damage;

		public XYZ root;
	}

	private struct MV
	{
		public float g;

		public int v;

		public float d;
	}
}
}
