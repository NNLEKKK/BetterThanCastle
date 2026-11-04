using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using Brix.Game.Rules;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
public static class BtcGameplay
{
	public static void Add(string id, string group, string en, string ru, float val, float min, float max, float step, bool isBool = false, string hintEn = null, string hintRu = null)
	{
		BtcGameplay.IndexById[id] = BtcGameplay.Defs.Count;
		BtcGameplay.Defs.Add(new BtcGameplay.Def
		{
			Id = id,
			Group = group,
			En = en,
			Ru = ru,
			Val = val,
			Min = min,
			Max = max,
			Step = step,
			IsBool = isBool,
			HintEn = hintEn,
			HintRu = hintRu
		});
	}

	static BtcGameplay()
	{
		BtcGameplay._lastScene = string.Empty;
		BtcGameplay.Presets = new List<BtcGameplay.Preset>();
		BtcGameplay.Pending = new List<BtcGameplay.PendingReq>();
		BtcGameplay.Snaps = new Dictionary<Faction, BtcGameplay.RefundSnap>();
		BtcGameplay.AddDmgRows();
		BtcGameplay.AddJobs("hp", "hp", "HP x", "ХП x", 10f, 0.05f, 350f, new float[]
		{
			350f,
			500f,
			700f,
			350f,
			500f,
			350f,
			350f
		}, 5000f, 1f, "hp", "хп");
		BtcGameplay.Add("stam.all", "hp", "Stamina x (Knight etc.)", "Стамина x (рыцарь и др.)", 1f, 0f, 10f, 0.05f, false, null, null);
		BtcGameplay.Absify("stam.all", 300f, 5000f, 1f, "stamina", "стамина");
		BtcGameplay.AddJobs("spd", "spd", "Speed x", "Скорость x", 3f, 0.05f, 5.16f, new float[]
		{
			5.16f,
			6f,
			4.3f,
			6f,
			4.3f,
			5.16f,
			4.3f
		}, 30f, 0.01f, "blocks/s", "блоков/с");
		BtcGameplay.Add("eco.income", "eco", "Crystal energy income x", "Доход энергии с кристаллов x", 1f, 0f, 10f, 0.05f, false, null, null);
		BtcGameplay.Add("eco.stable", "eco", "Fix random refund (stable unit count)", "Починить рандом штрафа (стабильный счёт юнитов)", 0f, 0f, 1f, 1f, true, "Vanilla prices a death by the army size that still includes the dead unit, so every death refunds one price step x share too much and simultaneous deaths jump around. On = each death is priced at the real respawn cost, the sum never depends on how many died at once.", "В ваниле штраф считается по армии вместе с погибшим, из-за этого каждая смерть возвращает на шаг цены x долю больше, а одновременные смерти прыгают. Вкл = смерть оценивается по реальной цене респавна, сумма не зависит от числа одновременных смертей.");
		BtcGameplay.Add("eco.penFixed", "eco", "Death penalty: fixed energy lost", "Штраф за смерть: фиксированная потеря энергии", -1f, -1f, 300f, 1f, false, "-1 = off. Otherwise refund = spawn cost - this value, always (competitive style).", "-1 = выкл. Иначе возврат = цена спавна - это значение, всегда.");
		BtcGameplay.Add("eco.penProp", "eco", "Death penalty: refund share of spawn cost", "Штраф за смерть: доля возврата от цены", -1f, -1f, 1f, 0.05f, false, "-1 = map value. 0.7 = 70% of the spawn cost comes back. Ignored when the fixed penalty is set.", "-1 = как на карте. 0.7 = возвращается 70% цены. Игнорируется при фикс. штрафе.");
		BtcGameplay.Add("eco.base", "eco", "Spawn energy: base cost", "Энергия на спавн: базовая цена", -1f, -1f, 1000f, 5f, false, "-1 = map value (Lua is overridden otherwise).", "-1 = как на карте (иначе Lua перекрывается).");
		BtcGameplay.Add("eco.factor", "eco", "Spawn energy: +cost per extra bricktron", "Энергия на спавн: прибавка за каждого сверх порога", -1f, -1f, 500f, 5f, false, null, null);
		BtcGameplay.Add("eco.begin", "eco", "Spawn energy: bricktrons before the cost grows", "Энергия на спавн: бриктронов до роста цены", -1f, -1f, 50f, 1f, false, null, null);
		BtcGameplay.Add("eco.cap", "eco", "Bricktron cap", "Максимум бриктронов", -1f, -1f, 100f, 1f, false, null, null);
		BtcGameplay.AddWorldRows();
		BtcLobby.AddRows();
		BtcBalanceExtra.AddRows();
		BtcGameplay.V = BtcGameplay.NewDefaults();
		BtcGameplay.Own = BtcGameplay.NewDefaults();
		BtcGameplay.Proposal = BtcGameplay.NewDefaults();
		BtcGameplay.VanillaArr = BtcGameplay.NewDefaults();
		BtcGameplay.E = BtcGameplay.VanillaArr;
		BtcGameplay.iDmgAll = BtcGameplay.Idx("dmg.all");
		BtcGameplay.iHpAll = BtcGameplay.Idx("hp.all");
		BtcGameplay.iSpdAll = BtcGameplay.Idx("spd.all");
		BtcGameplay.iDmgJ0 = BtcGameplay.Idx("dmg.j0");
		BtcGameplay.iHpJ0 = BtcGameplay.Idx("hp.j0");
		BtcGameplay.iSpdJ0 = BtcGameplay.Idx("spd.j0");
		BtcGameplay.iDmgBld = BtcGameplay.Idx("dmg.bld");
		BtcGameplay.iDmgAdv = BtcGameplay.Idx("dmg.adv");
		BtcGameplay.iDmgM0 = BtcGameplay.Idx("dmg.m.j0");
		BtcGameplay.iDmgP0 = BtcGameplay.Idx("dmg.p.j0");
		BtcGameplay.iBomb = BtcGameplay.Idx("dmg.bomb");
		BtcGameplay.iBarUnit = BtcGameplay.Idx("dmg.bar.unit");
		BtcGameplay.iBarBlk = BtcGameplay.Idx("dmg.bar.blk");
		BtcGameplay.iBarRad = BtcGameplay.Idx("dmg.bar.rad");
		BtcGameplay.Seen = new float[BtcGameplay.Defs.Count];
		BtcGameplay.iStam = BtcGameplay.Idx("stam.all");
		BtcGameplay.iIncome = BtcGameplay.Idx("eco.income");
		BtcGameplay.iStable = BtcGameplay.Idx("eco.stable");
		BtcGameplay.iPenFixed = BtcGameplay.Idx("eco.penFixed");
		BtcGameplay.iPenProp = BtcGameplay.Idx("eco.penProp");
		BtcGameplay.iBase = BtcGameplay.Idx("eco.base");
		BtcGameplay.iFactor = BtcGameplay.Idx("eco.factor");
		BtcGameplay.iBegin = BtcGameplay.Idx("eco.begin");
		BtcGameplay.iCap = BtcGameplay.Idx("eco.cap");
	}

	public static int Idx(string id)
	{
		int num;
		return BtcGameplay.IndexById.TryGetValue(id, out num) ? num : -1;
	}

	public static float[] NewDefaults()
	{
		float[] array = new float[BtcGameplay.Defs.Count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = BtcGameplay.Defs[i].Val;
		}
		return array;
	}

	public static float[] Active
	{
		get
		{
			return BtcGameplay.V;
		}
	}

	public static float[] Edit
	{
		get
		{
			return BtcGameplay.IsHost ? BtcGameplay.V : BtcGameplay.Proposal;
		}
	}

	public static string T(string en, string ru)
	{
		return BtcLang.Russian ? ru : en;
	}

	public static bool Enabled
	{
		get
		{
			return BtcBc.BcEnabled;
		}
	}

	public static bool IsHost
	{
		get
		{
			return BtcBc.BcEnabled && NetworkServer.active;
		}
	}

	public static bool InSession
	{
		get
		{
			return BtcBc.BcInSession;
		}
	}

	public static bool InLobby
	{
		get
		{
			UNetManager unetManager = NetworkManager.singleton as UNetManager;
			return unetManager != null && SceneManager.GetActiveScene().name == unetManager.lobbyScene;
		}
	}

	public static bool CanEdit
	{
		get
		{
			return BtcGameplay.IsHost && BtcGameplay.InLobby;
		}
	}

	public static bool IsVanilla(float[] a)
	{
		for (int i = 0; i < a.Length; i++)
		{
			bool flag = BtcGameplay.IsDev(i, a[i]);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				return false;
			}
		}
		return true;
	}

	public static void Init()
	{
		bool inited = BtcGameplay._inited;
		bool flag = !inited;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcGameplay._inited = true;
			BtcWorld.CachePaths();
			BtcGameplay.LoadOwn();
			BtcGameplay.LoadPresets();
			SceneManager.sceneLoaded += BtcGameplay.OnSceneLoaded;
			BtcGameplay.Refresh();
		}
	}

	private static void OnSceneLoaded(Scene s, LoadSceneMode mode)
	{
		BtcGameplay.Refresh();
		bool isHost = BtcGameplay.IsHost;
		bool flag = isHost;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcGameplay.Broadcast();
		}
	}

	public static void Refresh()
	{
		BtcGameplay.E = (BtcBc.BcEnabled ? BtcGameplay.V : BtcGameplay.VanillaArr);
	}

	public static void Tick()
	{
		BtcGameplay.Init();
		BtcGameplay.Refresh();
		bool isHost = BtcGameplay.IsHost;
		bool inSession = BtcGameplay.InSession;
		bool flag = !inSession && BtcGameplay.Received;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcGameplay.Received = false;
			BtcGameplay.V = (float[])BtcGameplay.Own.Clone();
			BtcGameplay.Pending.Clear();
			BtcGameplay.MyReq = BtcGameplay.ReqState.None;
		}
		bool flag9 = !inSession && !isHost && BtcGameplay.V != BtcGameplay.Own;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			BtcGameplay.V = BtcGameplay.Own;
		}
		bool flag17 = isHost && BtcGameplay.Dirty && Time.unscaledTime - BtcGameplay._dirtySince > 0.2f;
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		bool flag22 = flag21;
		bool flag23 = flag22;
		bool flag24 = flag23;
		if (flag24)
		{
			BtcGameplay.Dirty = false;
			BtcGameplay.Broadcast();
		}
		bool flag25 = BtcGameplay._saveAt > 0f && Time.unscaledTime > BtcGameplay._saveAt;
		bool flag26 = flag25;
		bool flag27 = flag26;
		bool flag28 = flag27;
		bool flag29 = flag28;
		bool flag30 = flag29;
		bool flag31 = flag30;
		bool flag32 = flag31;
		if (flag32)
		{
			BtcGameplay._saveAt = 0f;
			BtcGameplay.SaveOwn();
		}
		BtcGameplay.SampleNest();
	}

	public static float Snap(int i, float v)
	{
		BtcGameplay.Def def = BtcGameplay.Defs[i];
		bool isBool = def.IsBool;
		bool flag = isBool;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			v = ((v > 0.5f) ? 1f : 0f);
		}
		else
		{
			bool flag6 = def.Step >= 1f;
			bool flag7 = flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			if (flag11)
			{
				v = Mathf.Round(v);
			}
		}
		bool flag12 = def.Group == "ore";
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		if (flag15)
		{
			v = Mathf.Clamp(v, def.Min, def.Max);
		}
		else
		{
			bool flag16 = float.IsNaN(v) || float.IsInfinity(v);
			bool flag17 = flag16;
			bool flag18 = flag17;
			bool flag19 = flag18;
			if (flag19)
			{
				v = def.Val;
			}
			v = Mathf.Clamp(v, -1000000f, 1000000f);
		}
		return Mathf.Round(v * 10000f) / 10000f;
	}

	public static void Set(int i, float v)
	{
		v = BtcGameplay.Snap(i, v);
		BtcGameplay.SetRaw(i, v);
		bool flag = i == BtcGameplay.iOreStone || i == BtcGameplay.iOreIron || i == BtcGameplay.iOreBrim;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			BtcGameplay.LinkOre(i, v);
		}
	}

	public static void SetAll(float[] src)
	{
		for (int i = 0; i < BtcGameplay.Defs.Count; i++)
		{
			BtcGameplay.SetRaw(i, src[i]);
		}
	}

	private static void MarkDirty()
	{
		BtcGameplay.Dirty = true;
		BtcGameplay._dirtySince = Time.unscaledTime;
		BtcGameplay._saveAt = Time.unscaledTime + 1.5f;
	}

	public static string Encode(float[] a)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < a.Length; i++)
		{
			bool flag = Mathf.Abs(a[i] - BtcGameplay.Defs[i].Val) > 0.0001f;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				stringBuilder.Append(BtcGameplay.Defs[i].Id).Append('=').Append(a[i].ToString("R", CultureInfo.InvariantCulture)).Append(';');
			}
		}
		return stringBuilder.ToString();
	}

	public static float[] Decode(string s)
	{
		float[] array = BtcGameplay.NewDefaults();
		bool flag = string.IsNullOrEmpty(s);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		float[] result;
		if (flag8)
		{
			result = array;
		}
		else
		{
			string[] array2 = s.Split(new char[]
			{
				';'
			}, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array2.Length; i++)
			{
				int num = array2[i].IndexOf('=');
				bool flag9 = num <= 0;
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					int num2 = BtcGameplay.Idx(array2[i].Substring(0, num));
					float v = 0f;
					bool flag17 = num2 >= 0 && float.TryParse(array2[i].Substring(num + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						array[num2] = BtcGameplay.Snap(num2, v);
					}
				}
			}
			result = array;
		}
		return result;
	}

	public static List<string> DiffLines(float[] a)
	{
		List<string> list = new List<string>();
		for (int i = 0; i < a.Length; i++)
		{
			BtcGameplay.Def def = BtcGameplay.Defs[i];
			bool flag = BtcGameplay.IsDev(i, a[i]);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				list.Add(string.Concat(new string[]
				{
					BtcGameplay.T(def.En, def.Ru),
					":  ",
					BtcGameplay.Fmt(i, def.Val),
					"  ->  ",
					BtcGameplay.Fmt(i, a[i])
				}));
			}
		}
		BtcRecipes.AppendDiffLines(list);
		return list;
	}

	public static string Fmt(int i, float v)
	{
		BtcGameplay.Def def = BtcGameplay.Defs[i];
		bool isBool = def.IsBool;
		bool flag = isBool;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		string result;
		if (flag7)
		{
			result = ((v > 0.5f) ? BtcGameplay.T("on", "вкл") : BtcGameplay.T("off", "выкл"));
		}
		else
		{
			bool flag8 = def.HasMapDefault && v < 0f;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			if (flag15)
			{
				result = BtcGameplay.T("map", "карта");
			}
			else
			{
				result = v.ToString((def.Step >= 1f) ? "0" : "0.##", CultureInfo.InvariantCulture);
			}
		}
		return result;
	}

	private static string OwnPath
	{
		get
		{
			return BtcPaths.Config("gameplay.txt");
		}
	}

	private static string PresetPath
	{
		get
		{
			return BtcPaths.Config("gameplay_presets.txt");
		}
	}

	private static void LoadOwn()
	{
		try
		{
			bool flag = File.Exists(BtcGameplay.OwnPath);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcGameplay.Own = BtcGameplay.Decode(File.ReadAllText(BtcGameplay.OwnPath).Trim());
			}
		}
		catch (Exception)
		{
		}
		BtcGameplay.V = BtcGameplay.Own;
	}

	private static void SaveOwn()
	{
		try
		{
			BtcPaths.Init();
			File.WriteAllText(BtcGameplay.OwnPath, BtcGameplay.Encode(BtcGameplay.Own));
		}
		catch (Exception)
		{
		}
	}

	private static void LoadPresets()
	{
		BtcGameplay.Presets.Clear();
		BtcGameplay.Presets.Add(new BtcGameplay.Preset
		{
			Name = "Vanilla",
			Data = string.Empty,
			BuiltIn = true
		});
		BtcGameplay.Presets.Add(new BtcGameplay.Preset
		{
			Name = "Competitive",
			Data = "eco.stable=1;eco.penFixed=30;mov.items=1;mov.allies=1;mov.retry=1;mov.cancel=1;",
			BuiltIn = true
		});
		try
		{
			bool flag = File.Exists(BtcGameplay.PresetPath);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				string[] array = File.ReadAllLines(BtcGameplay.PresetPath);
				for (int i = 0; i < array.Length; i++)
				{
					int num = array[i].IndexOf('\t');
					bool flag10 = num > 0;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					bool flag17 = flag16;
					bool flag18 = flag17;
					if (flag18)
					{
						BtcGameplay.Presets.Add(new BtcGameplay.Preset
						{
							Name = array[i].Substring(0, num),
							Data = array[i].Substring(num + 1)
						});
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void SavePresets()
	{
		try
		{
			BtcPaths.Init();
			List<string> list = new List<string>();
			for (int i = 0; i < BtcGameplay.Presets.Count; i++)
			{
				bool flag = !BtcGameplay.Presets[i].BuiltIn;
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					list.Add(BtcGameplay.Presets[i].Name + "\t" + BtcGameplay.Presets[i].Data);
				}
			}
			File.WriteAllLines(BtcGameplay.PresetPath, list.ToArray());
		}
		catch (Exception)
		{
		}
	}

	public static void SavePreset(string name, float[] a)
	{
		name = (name ?? string.Empty).Replace('\t', ' ').Replace('\n', ' ').Trim();
		bool flag = name.Length == 0;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			int i = 0;
			while (i < BtcGameplay.Presets.Count)
			{
				bool flag9 = BtcGameplay.Presets[i].Name == name;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					bool builtIn = BtcGameplay.Presets[i].BuiltIn;
					bool flag17 = builtIn;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					if (flag23)
					{
						return;
					}
					BtcGameplay.Presets[i].Data = BtcGameplay.Encode(a);
					BtcGameplay.SavePresets();
					return;
				}
				else
				{
					i++;
				}
			}
			BtcGameplay.Presets.Add(new BtcGameplay.Preset
			{
				Name = name,
				Data = BtcGameplay.Encode(a)
			});
			BtcGameplay.SavePresets();
		}
	}

	public static void DeletePreset(int i)
	{
		bool flag = i >= 0 && i < BtcGameplay.Presets.Count && !BtcGameplay.Presets[i].BuiltIn;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcGameplay.Presets.RemoveAt(i);
			BtcGameplay.SavePresets();
		}
	}

	public static void RegisterHandlers(NetworkClient clientOverride)
	{
		BtcSpectate.RegisterHandlers(clientOverride);
		BtcRecipes.RegisterHandlers(clientOverride);
		BtcSymmetry.RegisterHandlers(clientOverride);
		BtcMapScale.RegisterHandlers(clientOverride);
		BtcHandicap.RegisterHandlers(clientOverride);
		BtcSpawnPick.RegisterHandlers(clientOverride);
		BtcEditorShare.RegisterHandlers();
		BtcReplayAuto.RegisterHandlers(clientOverride);
		BtcPovRec.RegisterHandlers();
		BtcMatchStatsNet.RegisterHandlers(clientOverride);
		BtcRackFilterNet.RegisterHandlers(clientOverride);
		BtcCleanFilterNet.RegisterHandlers(clientOverride);
		BtcTimer.RegisterHandlers(clientOverride);
		bool active = NetworkServer.active;
		bool flag = active;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		bool flag10 = flag9;
		bool flag11 = flag10;
		if (flag11)
		{
			NetworkServer.UnregisterHandler(76);
			NetworkServer.RegisterHandler(76, new NetworkMessageDelegate(BtcGameplay.OnRequestAtHost));
		}
		UNetManager unetManager = NetworkManager.singleton as UNetManager;
		NetworkClient networkClient = clientOverride ?? ((unetManager != null) ? unetManager.LocalNetworkClient : null);
		bool flag12 = networkClient != null;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		bool flag17 = flag16;
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		bool flag22 = flag21;
		bool flag23 = flag22;
		if (flag23)
		{
			networkClient.UnregisterHandler(75);
			networkClient.UnregisterHandler(77);
			networkClient.RegisterHandler(75, new NetworkMessageDelegate(BtcGameplay.OnStateAtClient));
			networkClient.RegisterHandler(77, new NetworkMessageDelegate(BtcGameplay.OnResultAtClient));
		}
	}

	public static void OnClientReady(NetworkConnection conn)
	{
		bool flag = !BtcBc.BcEnabled || conn == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		bool flag10 = flag9;
		if (flag10)
		{
			BtcGameplay.SendState(conn);
			BtcRecipes.SendState(conn);
			BtcHandicap.SendState(conn);
			BtcSpawnPick.SendState(conn);
			BtcRackFilterNet.SendState(conn);
			BtcCleanFilterNet.SendState(conn);
		}
	}

	private static BtcGameplay.GsMsg StateMsg()
	{
		return new BtcGameplay.GsMsg
		{
			kind = 0,
			data = BtcGameplay.Encode(BtcGameplay.V),
			rid = (BtcGameplay.InLobby ? 1 : 0)
		};
	}

	private static void SendState(NetworkConnection conn)
	{
		conn.Send(75, BtcGameplay.StateMsg());
		BtcSymmetry.SendState(conn);
		BtcMapScale.SendState(conn);
	}

	public static void Broadcast()
	{
		bool flag = !NetworkServer.active || !BtcBc.BcEnabled;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			NetworkServer.SendToAll(75, BtcGameplay.StateMsg());
		}
	}

	private static void OnStateAtClient(NetworkMessage netMsg)
	{
		BtcGameplay.GsMsg gsMsg = netMsg.ReadMessage<BtcGameplay.GsMsg>();
		bool active = NetworkServer.active;
		bool flag = !active;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			float[] array = BtcGameplay.Decode(gsMsg.data);
			bool received = BtcGameplay.Received;
			bool flag8 = !BtcGameplay.SameArr(array, BtcGameplay.V);
			BtcGameplay.V = array;
			BtcGameplay.Received = true;
			BtcGameplay.Refresh();
			bool flag9 = !received;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				BtcGameplay.Proposal = (float[])array.Clone();
				bool flag17 = !BtcGameplay.IsVanilla(array);
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					BtcGameplay.Warn = 1;
					BtcGameplay.WarnData = array;
				}
			}
			else
			{
				bool flag25 = flag8;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					BtcGameplay.Warn = 2;
					BtcGameplay.WarnData = array;
				}
			}
		}
	}

	private static bool SameArr(float[] a, float[] b)
	{
		bool flag = a.Length != b.Length;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool result;
		if (flag8)
		{
			result = false;
		}
		else
		{
			for (int i = 0; i < a.Length; i++)
			{
				bool flag9 = BtcGameplay.IsRule(i) && Mathf.Abs(a[i] - b[i]) > 0.0001f;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					return false;
				}
			}
			result = true;
		}
		return result;
	}

	public static void SendRequest(float[] draft)
	{
		UNetManager unetManager = NetworkManager.singleton as UNetManager;
		bool flag = unetManager == null || unetManager.LocalNetworkClient == null || !unetManager.LocalNetworkClient.isConnected || NetworkServer.active;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			string name = (unetManager.LocalLobbyPlayer != null) ? unetManager.LocalLobbyPlayer.playerName : "?";
			BtcGameplay._myRid = ++BtcGameplay._ridCounter;
			BtcGameplay.MyReq = BtcGameplay.ReqState.Waiting;
			unetManager.LocalNetworkClient.Send(76, new BtcGameplay.GsMsg
			{
				kind = 1,
				rid = BtcGameplay._myRid,
				name = name,
				data = BtcGameplay.Encode(draft)
			});
		}
	}

	private static void OnRequestAtHost(NetworkMessage netMsg)
	{
		bool flag = !BtcBc.BcEnabled;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcGameplay.GsMsg gsMsg = netMsg.ReadMessage<BtcGameplay.GsMsg>();
			int connectionId = netMsg.conn.connectionId;
			for (int i = BtcGameplay.Pending.Count - 1; i >= 0; i--)
			{
				bool flag9 = BtcGameplay.Pending[i].ConnId == connectionId;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					BtcGameplay.Pending.RemoveAt(i);
				}
			}
			bool flag17 = BtcGameplay.Pending.Count >= 16;
			bool flag18 = !flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				BtcGameplay.Pending.Add(new BtcGameplay.PendingReq
				{
					ConnId = connectionId,
					Rid = gsMsg.rid,
					Name = (string.IsNullOrEmpty(gsMsg.name) ? ("#" + connectionId) : gsMsg.name),
					Data = gsMsg.data,
					Vals = BtcGameplay.Decode(gsMsg.data)
				});
			}
		}
	}

	public static void Resolve(BtcGameplay.PendingReq r, bool approve)
	{
		BtcGameplay.Pending.Remove(r);
		bool flag = approve && BtcGameplay.CanEdit;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			for (int i = 0; i < BtcGameplay.Defs.Count; i++)
			{
				BtcGameplay.SetRaw(i, r.Vals[i]);
			}
			BtcGameplay.Dirty = true;
		}
		NetworkConnection networkConnection = null;
		for (int j = 0; j < NetworkServer.connections.Count; j++)
		{
			NetworkConnection networkConnection2 = NetworkServer.connections[j];
			bool flag9 = networkConnection2 != null && networkConnection2.connectionId == r.ConnId;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				networkConnection = networkConnection2;
				break;
			}
		}
		bool flag17 = networkConnection != null;
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		bool flag22 = flag21;
		bool flag23 = flag22;
		bool flag24 = flag23;
		if (flag24)
		{
			networkConnection.Send(77, new BtcGameplay.GsMsg
			{
				kind = 2,
				rid = r.Rid,
				data = ((approve && BtcGameplay.CanEdit) ? "ok" : "no")
			});
		}
	}

	private static void OnResultAtClient(NetworkMessage netMsg)
	{
		BtcGameplay.GsMsg gsMsg = netMsg.ReadMessage<BtcGameplay.GsMsg>();
		bool flag = gsMsg.rid == BtcGameplay._myRid;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcGameplay.MyReq = ((gsMsg.data == "ok") ? BtcGameplay.ReqState.Approved : BtcGameplay.ReqState.Rejected);
		}
	}

	public static float DmgMul(int job)
	{
		float[] e = BtcGameplay.E;
		float num = e[BtcGameplay.iDmgAll];
		return (job >= 0) ? (num * e[BtcGameplay.iDmgJ0 + job]) : num;
	}

	public static float HpMul(int job)
	{
		float[] e = BtcGameplay.E;
		float num = e[BtcGameplay.iHpAll];
		return (job >= 0) ? (num * e[BtcGameplay.iHpJ0 + job]) : num;
	}

	public static float StamMul()
	{
		return BtcGameplay.E[BtcGameplay.iStam];
	}

	public static float SpdMul(int job)
	{
		float[] e = BtcGameplay.E;
		float num = e[BtcGameplay.iSpdAll];
		return (job >= 0) ? (num * e[BtcGameplay.iSpdJ0 + job]) : num;
	}

	public static float BldMul()
	{
		return BtcGameplay.E[BtcGameplay.iDmgBld];
	}

	public static void ApplySpeed(Locomotion4 cur, Locomotion4 proto, float levelMul, int job)
	{
		bool flag = cur == null || proto == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		if (flag6)
		{
			float runSpeedMperS = proto.RunSpeedMperS;
			float num = runSpeedMperS * levelMul;
			float[] e = BtcGameplay.E;
			int num2 = BtcGameplay.iSpdAll;
			bool flag7 = job >= 0 && BtcGameplay.AbsOn(e, BtcGameplay.iSpdJ0 + job);
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			float num3;
			if (flag12)
			{
				BtcGameplay.Seen[BtcGameplay.iSpdJ0 + job] = num;
				num3 = e[BtcGameplay.Defs[BtcGameplay.iSpdJ0 + job].NumIdx];
			}
			else
			{
				bool flag13 = BtcGameplay.AbsOn(e, num2);
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				if (flag18)
				{
					BtcGameplay.Seen[num2] = num;
					num3 = e[BtcGameplay.Defs[num2].NumIdx];
				}
				else
				{
					num3 = num * BtcGameplay.SpdMul(job);
				}
			}
			float num4 = (runSpeedMperS > 0.0001f) ? (num3 / runSpeedMperS) : 1f;
			cur.WalkSpeedMperS = proto.WalkSpeedMperS * num4;
			cur.RunSpeedMperS = num3;
		}
	}

	public static int Cap(int mapValue)
	{
		float num = BtcGameplay.E[BtcGameplay.iCap];
		return (num >= 0f) ? Mathf.RoundToInt(num) : mapValue;
	}

	public static int SpawnEnergy(int activeCharacters, int mapBase, int mapFactor, int mapBegin)
	{
		float[] e = BtcGameplay.E;
		int num = (e[BtcGameplay.iBase] >= 0f) ? Mathf.RoundToInt(e[BtcGameplay.iBase]) : mapBase;
		int num2 = (e[BtcGameplay.iFactor] >= 0f) ? Mathf.RoundToInt(e[BtcGameplay.iFactor]) : mapFactor;
		int num3 = (e[BtcGameplay.iBegin] >= 0f) ? Mathf.RoundToInt(e[BtcGameplay.iBegin]) : mapBegin;
		return Mathf.Max(1, num + num2 * Mathf.Max(activeCharacters - num3, 0));
	}

	public static int Income(int fireflyFilling)
	{
		return Mathf.RoundToInt((float)fireflyFilling * BtcGameplay.E[BtcGameplay.iIncome]);
	}

	public static int Refund(Faction f)
	{
		GeneralRules main = GeneralRules.Main;
		float[] e = BtcGameplay.E;
		int unitCount = f.UnitCount;
		int num = Firefly.SpawnableFirefliesForFaction(f);
		int num2 = Firefly.RespawningFirefliesForFaction(f);
		bool flag = e[BtcGameplay.iStable] > 0.5f;
		int num3 = unitCount + num;
		int num4 = 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		if (flag9)
		{
			num3 = BtcGameplay.StableCount(f, out num4);
		}
		int homeCrystalSpawnRequiredEnergy = main.GetHomeCrystalSpawnRequiredEnergy(num3);
		bool flag10 = BtcGameplay.LogEnabled();
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		bool flag17 = flag16;
		if (flag17)
		{
			BtcGameplay._trackFaction = f;
			BtcGameplay._trackUntil = Time.unscaledTime + 120f;
		}
		float num5 = e[BtcGameplay.iPenFixed];
		float num6 = e[BtcGameplay.iPenProp];
		bool flag18 = num5 >= 0f;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		bool flag22 = flag21;
		bool flag23 = flag22;
		bool flag24 = flag23;
		bool flag25 = flag24;
		int num7;
		string text;
		if (flag25)
		{
			num7 = Mathf.Max(0, homeCrystalSpawnRequiredEnergy - Mathf.RoundToInt(num5));
			text = "fixed-" + num5;
		}
		else
		{
			bool flag26 = num6 >= 0f;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			bool flag32 = flag31;
			bool flag33 = flag32;
			if (flag33)
			{
				num7 = Mathf.RoundToInt((float)homeCrystalSpawnRequiredEnergy * num6);
				text = "prop-" + num6;
			}
			else
			{
				bool flag34 = main.proportionalRefund > 0f;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				bool flag41 = flag40;
				if (flag41)
				{
					num7 = Mathf.RoundToInt((float)homeCrystalSpawnRequiredEnergy * main.proportionalRefund);
					text = "vanilla-prop-" + main.proportionalRefund;
				}
				else
				{
					num7 = main.bricktronRefund;
					text = "vanilla-flat";
				}
			}
		}
		BtcGameplay.RefundLog(string.Concat(new object[]
		{
			f.name,
			" refund=",
			num7,
			" cost=",
			homeCrystalSpawnRequiredEnergy,
			" count=",
			num3,
			" (units=",
			unitCount,
			" waiting=",
			num,
			" respawning=",
			num2,
			") stable=",
			flag.ToString(),
			" burst#=",
			num4,
			" mode=",
			text
		}));
		return num7;
	}

	public static float TargetMul(IDamageReceiver target)
	{
		return (target is BricktronDamageReceiver) ? 1f : BtcGameplay.E[BtcGameplay.iDmgBld];
	}

	private static void RefundLog(string msg)
	{
		bool flag = BtcGameplay.LogEnabled();
		bool flag2 = !flag;
		bool flag3 = !flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			try
			{
				BtcPaths.Init();
				File.AppendAllText(BtcPaths.DebugFile("gameplay_refund_log.txt"), string.Concat(new object[]
				{
					DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
					" f",
					Time.frameCount,
					" ",
					msg,
					"\r\n"
				}));
			}
			catch (Exception)
			{
			}
		}
	}

	private static int StableCount(Faction f, out int idx)
	{
		int @base = f.UnitCount + Firefly.SpawnableFirefliesForFaction(f) + BtcGameplay.SpawningOld(f);
		BtcGameplay.RefundSnap refundSnap = null;
		bool flag = !BtcGameplay.Snaps.TryGetValue(f, out refundSnap);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			refundSnap = new BtcGameplay.RefundSnap();
			BtcGameplay.Snaps[f] = refundSnap;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		bool flag9 = refundSnap.Idx <= 0 || realtimeSinceStartup - refundSnap.At > 0.3f;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			refundSnap.Base = @base;
			refundSnap.Idx = 0;
		}
		refundSnap.Idx++;
		refundSnap.At = realtimeSinceStartup;
		idx = refundSnap.Idx;
		return Mathf.Max(refundSnap.Base - refundSnap.Idx, 0);
	}

	private static void SampleNest()
	{
		float unscaledTime = Time.unscaledTime;
		bool flag = unscaledTime >= BtcGameplay._trackUntil || unscaledTime < BtcGameplay._nextSample || BtcGameplay._trackFaction == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcGameplay._nextSample = unscaledTime + 1f;
			FireflyNest[] array = UnityEngine.Object.FindObjectsOfType<FireflyNest>();
			int num = -1;
			for (int i = 0; i < array.Length; i++)
			{
				bool flag9 = array[i] != null && array[i].isHome && array[i].GetComponent<AffiliationComponent>().faction == BtcGameplay._trackFaction;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					num = array[i].GetAvailablePurifiedEnergy();
					break;
				}
			}
			Faction trackFaction = BtcGameplay._trackFaction;
			BtcGameplay.RefundLog(string.Concat(new object[]
			{
				trackFaction.name,
				" NEST energy=",
				num,
				" units=",
				trackFaction.UnitCount,
				" waiting=",
				Firefly.SpawnableFirefliesForFaction(trackFaction),
				" respawning=",
				Firefly.RespawningFirefliesForFaction(trackFaction)
			}));
		}
	}

	private static int SpawningOld(Faction f)
	{
		bool flag = BtcGameplay._ffField == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcGameplay._ffField = typeof(Firefly).GetField("_allFireflies", BindingFlags.Static | BindingFlags.NonPublic);
		}
		List<Firefly> list = (BtcGameplay._ffField != null) ? (BtcGameplay._ffField.GetValue(null) as List<Firefly>) : null;
		int num = 0;
		bool flag9 = list != null;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			for (int i = 0; i < list.Count; i++)
			{
				Firefly firefly = list[i];
				bool flag17 = firefly != null && firefly.IsSpawning && firefly.IsNamed && firefly.isOldUnit && firefly.GetComponent<AffiliationComponent>().faction == f;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					num++;
				}
			}
		}
		return num;
	}

	public static int RespawnBase(Faction f, int vanilla)
	{
		return (f == null || f.isAI || BtcGameplay.E[BtcGameplay.iStable] <= 0.5f) ? vanilla : (vanilla + BtcGameplay.SpawningOld(f));
	}

	public static bool HoldNewSpawns(Faction f)
	{
		bool flag = f != null && !f.isAI && BtcGameplay.E[BtcGameplay.iStable] > 0.5f;
		return flag && Firefly.RespawningFirefliesForFaction(f) - BtcGameplay.SpawningOld(f) > 0;
	}

	private static bool LogEnabled()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		bool flag = realtimeSinceStartup >= BtcGameplay._logCheckAt;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcGameplay._logCheckAt = realtimeSinceStartup + 10f;
			try
			{
				BtcPaths.Init();
				BtcGameplay._logOn = File.Exists(BtcPaths.DebugFile("gameplay_log_on.txt"));
			}
			catch (Exception)
			{
				BtcGameplay._logOn = false;
			}
		}
		return BtcGameplay._logOn;
	}

	private static void Absify(string id, float numDefault, float absMax, float absStep, string unitEn, string unitRu)
	{
		int index = BtcGameplay.IndexById[id];
		BtcGameplay.Def def = BtcGameplay.Defs[index];
		def.Abs = true;
		def.NumDefault = numDefault;
		def.UnitEn = unitEn;
		def.UnitRu = unitRu;
		def.ModeIdx = BtcGameplay.Defs.Count;
		BtcGameplay.Add(id + "#m", def.Group, def.En + " [number mode]", def.Ru + " [режим числа]", 0f, 0f, 1f, 1f, true, null, null);
		BtcGameplay.Defs[def.ModeIdx].Hidden = true;
		def.NumIdx = BtcGameplay.Defs.Count;
		BtcGameplay.Add(id + "#n", def.Group, def.En + " [number]", def.Ru + " [число]", numDefault, 0f, absMax, absStep, false, null, null);
		BtcGameplay.Defs[def.NumIdx].Hidden = true;
	}

	private static void SetAdv(string id, int adv)
	{
		BtcGameplay.Defs[BtcGameplay.IndexById[id]].Adv = adv;
	}

	private static void AddDmgRows()
	{
		BtcGameplay.Add("dmg.adv", "dmg", "Advanced: melee and ranged separately", "Расширенно: мили и дальний бой отдельно", 0f, 0f, 1f, 1f, true, "Off: one row per unit (melee units = melee hit, archers/arbalists/artificers = projectile, alchemist = direct bomb hit). On: every unit gets its own melee and projectile rows.", "Выкл: одна строка на юнита (мили-юниты = удар, лучник/арбалетчик/артефактор = снаряд, алхимик = тычка бомбой). Вкл: у каждого юнита отдельно мили и снаряд.");
		BtcGameplay.Add("dmg.all", "dmg", "Damage x: all units", "Урон x: все юниты", 1f, 0f, 10f, 0.05f, false, "Melee hits, projectiles and alchemist bombs of every unit.", "Мили-удары, снаряды и бомбы алхимика у всех юнитов.");
		float[] array = new float[]
		{
			12f,
			32f,
			2f,
			20f,
			45f,
			20f,
			30f
		};
		float[] array2 = new float[]
		{
			12f,
			32f,
			2f,
			12f,
			12f,
			12f,
			12f
		};
		float[] array3 = new float[]
		{
			20f,
			20f,
			20f,
			20f,
			45f,
			20f,
			30f
		};
		string[] array4 = new string[]
		{
			"melee",
			"melee",
			"melee",
			"projectile",
			"projectile",
			"direct bomb hit",
			"projectile"
		};
		string[] array5 = new string[]
		{
			"мили",
			"мили",
			"мили",
			"снаряд",
			"снаряд",
			"тычка бомбой",
			"снаряд"
		};
		for (int i = 0; i < BtcGameplay.JobEn.Length; i++)
		{
			string id = "dmg.j" + i;
			BtcGameplay.Add(id, "dmg", string.Concat(new string[]
			{
				"Damage x: ",
				BtcGameplay.JobEn[i],
				" (",
				array4[i],
				")"
			}), string.Concat(new string[]
			{
				"Урон x: ",
				BtcGameplay.JobRu[i],
				" (",
				array5[i],
				")"
			}), 1f, 0f, 10f, 0.05f, false, null, null);
			BtcGameplay.SetAdv(id, 1);
		}
		for (int j = 0; j < BtcGameplay.JobEn.Length; j++)
		{
			BtcGameplay.Absify("dmg.j" + j, array[j], 1000f, 1f, "dmg", "урон");
		}
		BtcGameplay.Add("dmg.bomb", "dmg", "Alchemist bomb explosion x", "Взрыв бомбы алхимика x", 1f, 0f, 10f, 0.05f, false, "Number mode = damage to units at the centre of the blast.", "Режим числа = урон по юнитам в центре взрыва.");
		BtcGameplay.Absify("dmg.bomb", 100f, 5000f, 1f, "dmg", "урон");
		for (int k = 0; k < BtcGameplay.JobEn.Length; k++)
		{
			string id2 = "dmg.m.j" + k;
			BtcGameplay.Add(id2, "dmg", "Melee x: " + BtcGameplay.JobEn[k], "Мили x: " + BtcGameplay.JobRu[k], 1f, 0f, 10f, 0.05f, false, null, null);
			BtcGameplay.SetAdv(id2, 2);
		}
		for (int l = 0; l < BtcGameplay.JobEn.Length; l++)
		{
			BtcGameplay.Absify("dmg.m.j" + l, array2[l], 1000f, 1f, "dmg", "урон");
		}
		for (int m = 0; m < BtcGameplay.JobEn.Length; m++)
		{
			string id3 = "dmg.p.j" + m;
			BtcGameplay.Add(id3, "dmg", "Projectile x: " + BtcGameplay.JobEn[m], "Снаряд x: " + BtcGameplay.JobRu[m], 1f, 0f, 10f, 0.05f, false, (m == 5) ? "Alchemist: direct bomb hit (the blast has its own row)." : null, (m == 5) ? "Алхимик: тычка бомбой (у взрыва своя строка)." : null);
			BtcGameplay.SetAdv(id3, 2);
		}
		for (int n = 0; n < BtcGameplay.JobEn.Length; n++)
		{
			BtcGameplay.Absify("dmg.p.j" + n, array3[n], 1000f, 1f, "dmg", "урон");
		}
		BtcGameplay.Add("dmg.bld", "dmg", "Damage to buildings / objects x", "Урон по зданиям / объектам x", 1f, 0f, 10f, 0.05f, false, "Melee and projectile hits on every non-unit target (blocks, barrels, wards...) and bomb blast on blocks.", "Мили и снаряды по любым не-юнитам (блоки, бочки, стражи...) и взрыв бомбы по блокам.");
		BtcGameplay.Add("dmg.bar.unit", "dmg", "Barrel explosion: damage to units x", "Взрыв бочки: урон по юнитам x", 1f, 0f, 10f, 0.05f, false, "Number mode = damage at the centre of the blast.", "Режим числа = урон в центре взрыва.");
		BtcGameplay.Absify("dmg.bar.unit", 600f, 5000f, 1f, "dmg", "урон");
		BtcGameplay.Add("dmg.bar.blk", "dmg", "Barrel explosion: damage to blocks x", "Взрыв бочки: урон по блокам x", 1f, 0f, 10f, 0.05f, false, null, null);
		BtcGameplay.Add("dmg.bar.rad", "dmg", "Barrel explosion: radius x", "Взрыв бочки: радиус x", 1f, 0.2f, 4f, 0.05f, false, "Number mode = blast radius in blocks.", "Режим числа = радиус взрыва в блоках.");
		BtcGameplay.Absify("dmg.bar.rad", 5f, 40f, 0.5f, "blocks", "блоков");
	}

	public static bool AbsOn(float[] e, int row)
	{
		int modeIdx = BtcGameplay.Defs[row].ModeIdx;
		return modeIdx >= 0 && e[modeIdx] > 0.5f;
	}

	public static float SeenNum(int row)
	{
		float num = BtcGameplay.Seen[row];
		return (num > 0f) ? num : BtcGameplay.Defs[row].NumDefault;
	}

	public static int JobOf(GameObject go)
	{
		bool flag = go == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		int result;
		if (flag6)
		{
			result = -1;
		}
		else
		{
			Labor component = go.GetComponent<Labor>();
			bool flag7 = component == null || component.Occupation == null || !(component.Profession is Builder);
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				result = -1;
			}
			else
			{
				result = (int)component.Occupation.CurrentJob;
			}
		}
		return result;
	}

	public static float MeleeDmg(int job, float vanilla)
	{
		float[] e = BtcGameplay.E;
		float num = e[BtcGameplay.iDmgAll];
		bool flag = job >= 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		if (flag6)
		{
			int num2 = (e[BtcGameplay.iDmgAdv] > 0.5f) ? (BtcGameplay.iDmgM0 + job) : ((job <= 2) ? (BtcGameplay.iDmgJ0 + job) : -1);
			bool flag7 = num2 >= 0;
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				BtcGameplay.Seen[num2] = vanilla;
				bool flag13 = BtcGameplay.AbsOn(e, num2);
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				if (flag18)
				{
					return e[BtcGameplay.Defs[num2].NumIdx];
				}
				return vanilla * num * e[num2];
			}
		}
		return vanilla * num;
	}

	public static float ProjDmg(int job, float vanilla)
	{
		float[] e = BtcGameplay.E;
		float num = e[BtcGameplay.iDmgAll];
		bool flag = job >= 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		if (flag6)
		{
			int num2 = (e[BtcGameplay.iDmgAdv] > 0.5f) ? (BtcGameplay.iDmgP0 + job) : ((job >= 3) ? (BtcGameplay.iDmgJ0 + job) : -1);
			bool flag7 = num2 >= 0;
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				BtcGameplay.Seen[num2] = vanilla;
				bool flag13 = BtcGameplay.AbsOn(e, num2);
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				if (flag18)
				{
					return e[BtcGameplay.Defs[num2].NumIdx];
				}
				return vanilla * num * e[num2];
			}
		}
		return vanilla * num;
	}

	public static float HpFinal(int job, float vanilla)
	{
		float[] e = BtcGameplay.E;
		bool flag = job >= 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		if (flag6)
		{
			int num = BtcGameplay.iHpJ0 + job;
			BtcGameplay.Seen[num] = vanilla;
			bool flag7 = BtcGameplay.AbsOn(e, num);
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				return e[BtcGameplay.Defs[num].NumIdx];
			}
		}
		BtcGameplay.Seen[BtcGameplay.iHpAll] = vanilla;
		bool flag13 = BtcGameplay.AbsOn(e, BtcGameplay.iHpAll);
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		bool flag17 = flag16;
		bool flag18 = flag17;
		float result;
		if (flag18)
		{
			result = e[BtcGameplay.Defs[BtcGameplay.iHpAll].NumIdx];
		}
		else
		{
			result = vanilla * BtcGameplay.HpMul(job);
		}
		return result;
	}

	public static float StamFinal(float vanilla)
	{
		float[] e = BtcGameplay.E;
		BtcGameplay.Seen[BtcGameplay.iStam] = vanilla;
		bool flag = BtcGameplay.AbsOn(e, BtcGameplay.iStam);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		float result;
		if (flag6)
		{
			result = e[BtcGameplay.Defs[BtcGameplay.iStam].NumIdx];
		}
		else
		{
			result = vanilla * e[BtcGameplay.iStam];
		}
		return result;
	}

	public static bool ClientKeepsSyncedHp()
	{
		return !NetworkServer.active && BtcGameplay.Received;
	}

	public static void NoteExpl(Vector3 pos, string template)
	{
		int exIdx = BtcGameplay._exIdx;
		BtcGameplay._exIdx = (exIdx + 1 & 3);
		BtcGameplay._exPos[exIdx] = pos;
		BtcGameplay._exBomb[exIdx] = (template == "ExplosionAlchemistPotion");
		BtcGameplay._exUsed[exIdx] = false;
	}

	private static bool TakeBomb(Vector3 pos)
	{
		for (int i = 0; i < 4; i++)
		{
			bool flag = !BtcGameplay._exUsed[i] && (BtcGameplay._exPos[i] - pos).sqrMagnitude < 0.25f;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				BtcGameplay._exUsed[i] = true;
				return BtcGameplay._exBomb[i];
			}
		}
		return false;
	}

	public static void ExplScales(Vector3 pos, float unitPeak, float maxRange, out float unit, out float blk, out float rad)
	{
		float[] e = BtcGameplay.E;
		unit = 1f;
		blk = 1f;
		rad = 1f;
		bool flag = BtcGameplay.TakeBomb(pos);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		if (flag6)
		{
			BtcGameplay.Seen[BtcGameplay.iBomb] = unitPeak;
			bool flag7 = BtcGameplay.AbsOn(e, BtcGameplay.iBomb);
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				unit = ((unitPeak > 0.01f) ? (e[BtcGameplay.Defs[BtcGameplay.iBomb].NumIdx] / unitPeak) : 1f);
			}
			else
			{
				unit = e[BtcGameplay.iDmgAll] * e[BtcGameplay.iBomb];
			}
			blk = e[BtcGameplay.iDmgBld];
		}
		else
		{
			BtcGameplay.Seen[BtcGameplay.iBarUnit] = unitPeak;
			BtcGameplay.Seen[BtcGameplay.iBarRad] = maxRange;
			bool flag13 = BtcGameplay.AbsOn(e, BtcGameplay.iBarUnit);
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			bool flag17 = flag16;
			bool flag18 = flag17;
			if (flag18)
			{
				unit = ((unitPeak > 0.01f) ? (e[BtcGameplay.Defs[BtcGameplay.iBarUnit].NumIdx] / unitPeak) : 1f);
			}
			else
			{
				unit = e[BtcGameplay.iBarUnit];
			}
			blk = e[BtcGameplay.iBarBlk];
			bool flag19 = BtcGameplay.AbsOn(e, BtcGameplay.iBarRad);
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				rad = ((maxRange > 0.01f) ? (e[BtcGameplay.Defs[BtcGameplay.iBarRad].NumIdx] / maxRange) : 1f);
			}
			else
			{
				rad = e[BtcGameplay.iBarRad];
			}
			bool flag25 = rad < 0.05f;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			if (flag30)
			{
				rad = 0.05f;
			}
		}
	}

	private static void AddJobs(string key, string group, string en, string ru, float max, float step, float absDefAll, float[] absDefJobs, float absMax, float absStep, string unitEn, string unitRu)
	{
		BtcGameplay.Add(key + ".all", group, en + ": all units", ru + ": все юниты", 1f, 0f, max, step, false, null, null);
		for (int i = 0; i < BtcGameplay.JobEn.Length; i++)
		{
			BtcGameplay.Add(key + ".j" + i, group, en + ": " + BtcGameplay.JobEn[i], ru + ": " + BtcGameplay.JobRu[i], 1f, 0f, max, step, false, null, null);
		}
		BtcGameplay.Absify(key + ".all", absDefAll, absMax, absStep, unitEn, unitRu);
		for (int j = 0; j < BtcGameplay.JobEn.Length; j++)
		{
			BtcGameplay.Absify(key + ".j" + j, absDefJobs[j], absMax, absStep, unitEn, unitRu);
		}
	}

	private static void AddWorldRows()
	{
		BtcGameplay.iRngMaxJ = new int[]
		{
			-1,
			-1,
			-1,
			-1,
			-1,
			-1,
			-1
		};
		BtcGameplay.iRngMinJ = new int[]
		{
			-1,
			-1,
			-1,
			-1,
			-1,
			-1,
			-1
		};
		BtcGameplay.Add("rng.max.all", "rng", "Max shooting range x: all ranged units", "Макс. дальность выстрела x: все стрелки", 1f, 0f, 5f, 0.05f, false, "Archers, arbalists, alchemists, artificers (and wards/turrets). Number mode = range in blocks. Shots farther than the arrow can fly may fall short.", "Лучники, арбалетчики, алхимики, артефакторы (и стражи/турели). Режим числа = дальность в блоках. Цели дальше полёта стрелы могут недолетать.");
		BtcGameplay.Absify("rng.max.all", 80f, 400f, 1f, "blocks", "блоков");
		BtcGameplay.iRngMaxAll = BtcGameplay.Idx("rng.max.all");
		for (int i = 3; i < 7; i++)
		{
			BtcGameplay.Add("rng.max.j" + i, "rng", "Max range x: " + BtcGameplay.JobEn[i], "Макс. дальность x: " + BtcGameplay.JobRu[i], 1f, 0f, 5f, 0.05f, false, null, null);
			BtcGameplay.Absify("rng.max.j" + i, 80f, 400f, 1f, "blocks", "блоков");
			BtcGameplay.iRngMaxJ[i] = BtcGameplay.Idx("rng.max.j" + i);
		}
		BtcGameplay.Add("rng.min.all", "rng", "Min shooting range x: all ranged units", "Мин. дальность выстрела x: все стрелки", 1f, 0f, 10f, 0.05f, false, "Closer targets are not shot at. 0 = shoot point blank. Number mode = range in blocks.", "Ближе этой дальности не стреляют. 0 = стрелять в упор. Режим числа = дальность в блоках.");
		BtcGameplay.Absify("rng.min.all", 0.5f, 100f, 0.5f, "blocks", "блоков");
		BtcGameplay.iRngMinAll = BtcGameplay.Idx("rng.min.all");
		for (int j = 3; j < 7; j++)
		{
			BtcGameplay.Add("rng.min.j" + j, "rng", "Min range x: " + BtcGameplay.JobEn[j], "Мин. дальность x: " + BtcGameplay.JobRu[j], 1f, 0f, 10f, 0.05f, false, null, null);
			BtcGameplay.Absify("rng.min.j" + j, 0.5f, 100f, 0.5f, "blocks", "блоков");
			BtcGameplay.iRngMinJ[j] = BtcGameplay.Idx("rng.min.j" + j);
		}
		BtcGameplay.Add("ore.stone", "ore", "Deep hard stone, % of the deep layer", "Твёрдый дипкамень, % глубокого слоя", 51f, -1f, 100f, 0.1f, false, "Share of the deep layer volume. Default 51 = vanilla (measured on a real map, differs by about 1 between seeds). Soft earth = 100 - stone - iron - brimstone. The three rows are linked: raising one lowers the others proportionally (100 = the others become 0). -1 = vanilla of the current seed. Applies when the terrain is generated: host sets it in the lobby, clients get it before loading. 0 = no hard stone.", "Доля объёма глубокого слоя. По умолчанию 51 = ваниль (замерено на реальной карте, между сидами отличается примерно на 1). Мягкая земля = 100 - камень - железо - бримстоун. Три строки связаны: если поднять одну, остальные уменьшаются пропорционально (100 = остальные станут 0). -1 = ваниль текущего сида. Работает при генерации земли: хост ставит в лобби, клиенты получают до загрузки. 0 = твёрдого камня нет.");
		BtcGameplay.iOreStone = BtcGameplay.Idx("ore.stone");
		BtcGameplay.Add("ore.iron", "ore", "Iron ore, % of the deep layer", "Железная руда, % глубокого слоя", 3.2f, -1f, 100f, 0.1f, false, "Final share of the deep layer volume (after hard stone is placed). Default 3.2 = vanilla (measured on a real map). -1 = vanilla of the current seed. 0 = no iron.", "Итоговая доля объёма глубокого слоя (после твёрдого камня). По умолчанию 3.2 = ваниль (замерено на реальной карте). -1 = ваниль текущего сида. 0 = железа нет.");
		BtcGameplay.iOreIron = BtcGameplay.Idx("ore.iron");
		BtcGameplay.Add("ore.brim", "ore", "Brimstone (orange crystal), % of the deep layer", "Бримстоун (оранжевый кристалл), % глубокого слоя", 6.9f, -1f, 100f, 0.1f, false, "Final share of the deep layer volume. Default 6.9 = vanilla (measured on a real map). -1 = vanilla of the current seed. 0 = no brimstone. The three ore rows are linked, see hard stone.", "Итоговая доля объёма глубокого слоя. По умолчанию 6.9 = ваниль (замерено на реальной карте). -1 = ваниль текущего сида. 0 = бримстоуна нет. Три строки руды связаны, см. твёрдый камень.");
		BtcGameplay.iOreBrim = BtcGameplay.Idx("ore.brim");
	}

	private static void LinkOre(int i, float v)
	{
		bool flag = v < 0f;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			float[] edit = BtcGameplay.Edit;
			int[] array = new int[]
			{
				BtcGameplay.iOreStone,
				BtcGameplay.iOreIron,
				BtcGameplay.iOreBrim
			};
			float num = Mathf.Max(0f, 100f - v);
			float num2 = 0f;
			for (int j = 0; j < 3; j++)
			{
				bool flag5 = array[j] != i && edit[array[j]] > 0f;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					num2 += edit[array[j]];
				}
			}
			bool flag9 = num2 > num + 0.0001f;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				float num3 = num / num2;
				for (int k = 0; k < 3; k++)
				{
					bool flag13 = array[k] != i && edit[array[k]] > 0f;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						BtcGameplay.SetRaw(array[k], Mathf.Floor(edit[array[k]] * num3 * 10f) / 10f);
					}
				}
			}
			bool flag17 = num <= 0f;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			if (flag20)
			{
				for (int l = 0; l < 3; l++)
				{
					bool flag21 = array[l] != i && edit[array[l]] < 0f;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						BtcGameplay.SetRaw(array[l], 0f);
					}
				}
			}
		}
	}

	private static void SetRaw(int i, float v)
	{
		v = BtcGameplay.Snap(i, v);
		bool isHost = BtcGameplay.IsHost;
		bool flag = isHost;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			bool flag8 = !BtcGameplay.CanEdit;
			bool flag9 = !flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			if (flag15)
			{
				bool flag16 = BtcGameplay.V[i] == v;
				bool flag17 = !flag16;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				if (flag23)
				{
					BtcGameplay.V[i] = v;
					BtcGameplay.Own[i] = v;
					BtcGameplay.MarkDirty();
				}
			}
		}
		else
		{
			BtcGameplay.Proposal[i] = v;
			BtcGameplay.MyReq = BtcGameplay.ReqState.None;
		}
	}

	// Rows that never count as a rules change (client-only repair toggles, UX).
	private static readonly string[] NonRuleIds = new string[] { "spec.facfix", "spec.facidle", "spec.facsel", "spec.facspec", "spec.faclua", "spec.hudfix", "spec.mapfix", "spec.pause", "spec.ask" };

	public static bool IsRule(int i)
	{
		if (i < 0 || i >= BtcGameplay.Defs.Count) return false;
		string id = BtcGameplay.Defs[i].Id;
		for (int k = 0; k < BtcGameplay.NonRuleIds.Length; k++)
		{
			if (BtcGameplay.NonRuleIds[k] == id) return false;
		}
		return true;
	}

	public static bool IsDev(int i, float v)
	{
		BtcGameplay.Def def = BtcGameplay.Defs[i];
		return BtcGameplay.IsRule(i) && Mathf.Abs(v - def.Val) > 0.0001f;
	}

	public static readonly string[] JobEn = new string[]
	{
		"Builder",
		"Halberdier",
		"Knight",
		"Archer",
		"Arbalist",
		"Alchemist",
		"Artificer"
	};

	public static readonly string[] JobRu = new string[]
	{
		"Строитель",
		"Алебардщик",
		"Рыцарь",
		"Лучник",
		"Арбалетчик",
		"Алхимик",
		"Артефактор"
	};

	public static readonly List<BtcGameplay.Def> Defs = new List<BtcGameplay.Def>();

	private static readonly Dictionary<string, int> IndexById = new Dictionary<string, int>();

	public const string GDmg = "dmg";

	public const string GHp = "hp";

	public const string GSpd = "spd";

	public const string GEco = "eco";

	private static float[] V;

	private static float[] Own;

	public static float[] Proposal;

	private static float[] VanillaArr;

	public static float[] E;

	private static int iDmgAll;

	private static int iHpAll;

	private static int iSpdAll;

	private static int iDmgJ0;

	private static int iHpJ0;

	private static int iSpdJ0;

	private static int iDmgBld;

	private static int iStam;

	private static int iIncome;

	private static int iStable;

	private static int iPenFixed;

	private static int iPenProp;

	private static int iBase;

	private static int iFactor;

	private static int iBegin;

	private static int iCap;

	public static bool Received;

	public static bool Dirty;

	private static float _dirtySince;

	private static float _saveAt;

	private static bool _inited;

	private static string _lastScene;

	public static readonly List<BtcGameplay.Preset> Presets;

	public const short MsgState = 75;

	public const short MsgRequest = 76;

	public const short MsgResult = 77;

	public static BtcGameplay.ReqState MyReq;

	private static int _ridCounter;

	private static int _myRid;

	public static readonly List<BtcGameplay.PendingReq> Pending;

	public static int Warn;

	// Red banner: host enabled the editor panel (set by net code).
	public static bool EditorWarn;

	private static int _iEditor = -1;

	public static bool EditorPanelOn
	{
		get
		{
			if (BtcGameplay._iEditor < 0) BtcGameplay._iEditor = BtcGameplay.Idx("gen.editor");
			float[] e = BtcGameplay.E;
			return BtcGameplay._iEditor >= 0 && e != null && BtcGameplay._iEditor < e.Length && e[BtcGameplay._iEditor] > 0.5f;
		}
	}

	// Raise the yellow rules warning from another system (e.g. recipes). Never lowers an active warning.
	public static void RaiseWarn(int level)
	{
		if (BtcGameplay.Warn < level) BtcGameplay.Warn = level;
	}

	public static float[] WarnData;

	private static readonly Dictionary<Faction, BtcGameplay.RefundSnap> Snaps;

	private static Faction _trackFaction;

	private static float _trackUntil;

	private static float _nextSample;

	private static FieldInfo _ffField;

	private static bool _logOn;

	private static float _logCheckAt;

	private static int iDmgAdv;

	private static int iDmgM0;

	private static int iDmgP0;

	private static int iBomb;

	private static int iBarUnit;

	private static int iBarBlk;

	private static int iBarRad;

	public static float[] Seen;

	private static readonly Vector3[] _exPos = new Vector3[4];

	private static readonly bool[] _exBomb = new bool[4];

	private static readonly bool[] _exUsed = new bool[4];

	private static int _exIdx;

	public static int iRngMaxAll;

	public static int iRngMinAll;

	public static int[] iRngMaxJ;

	public static int[] iRngMinJ;

	public static int iOreIron;

	public static int iOreBrim;

	public static int iOreStone;

	public sealed class Def
	{
		public bool HasMapDefault
		{
			get
			{
				return this.Min < 0f;
			}
		}

		public string Id;

		public string Group;

		public string En;

		public string Ru;

		public string HintEn;

		public string HintRu;

		public float Val;

		public float Min;

		public float Max;

		public float Step;

		public bool IsBool;

		public bool Abs;

		public int ModeIdx = -1;

		public int NumIdx = -1;

		public float NumDefault;

		public int Adv;

		public string UnitEn = string.Empty;

		public string UnitRu = string.Empty;

		public bool Hidden;
	}

	public sealed class Preset
	{
		public string Name;

		public string Data;

		public bool BuiltIn;
	}

	public class GsMsg : MessageBase
	{
		public override void Serialize(NetworkWriter w)
		{
			w.Write(this.kind);
			w.Write(this.rid);
			w.Write(this.name ?? string.Empty);
			w.Write(this.data ?? string.Empty);
		}

		public override void Deserialize(NetworkReader r)
		{
			this.kind = r.ReadByte();
			this.rid = r.ReadInt32();
			this.name = r.ReadString();
			this.data = r.ReadString();
		}

		public byte kind;

		public int rid;

		public string name = string.Empty;

		public string data = string.Empty;
	}

	public enum ReqState
	{
		None,
		Waiting,
		Approved,
		Rejected
	}

	public sealed class PendingReq
	{
		public int ConnId;

		public int Rid;

		public string Name;

		public string Data;

		public float[] Vals;
	}

	private sealed class RefundSnap
	{
		public float At;

		public int Base;

		public int Idx;
	}
}
}
