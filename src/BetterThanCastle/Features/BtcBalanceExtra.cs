using System;
using System.Collections.Generic;
using Brix.Game.AI;
using Brix.Game.Components.Wards;
using Brix.Game.Rules;
using MoonSharp.Interpreter;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcBalanceExtra
{
	private static void AddAbs(string id, string group, string en, string ru, float val, float min, float max, float step, float numDefault, float absMax, float absStep, string unitEn, string unitRu, string hintEn, string hintRu)
	{
		BtcGameplay.Add(id, group, en, ru, val, min, max, step, false, hintEn, hintRu);
		BtcGameplay.Def def = BtcGameplay.Defs[BtcGameplay.Idx(id)];
		def.Abs = true;
		def.NumDefault = numDefault;
		def.UnitEn = unitEn;
		def.UnitRu = unitRu;
		def.ModeIdx = BtcGameplay.Defs.Count;
		BtcGameplay.Add(id + "#m", group, def.En + " [number mode]", def.Ru + " [режим числа]", 0f, 0f, 1f, 1f, true, null, null);
		BtcGameplay.Defs[def.ModeIdx].Hidden = true;
		def.NumIdx = BtcGameplay.Defs.Count;
		BtcGameplay.Add(id + "#n", group, def.En + " [number]", def.Ru + " [число]", numDefault, 0f, absMax, absStep, false, null, null);
		BtcGameplay.Defs[def.NumIdx].Hidden = true;
	}

	public static void AddRows()
	{
		string hintEn = "Applies to objects created after the match starts. x = multiplier of the normal vision radius; number mode = fixed radius.";
		string hintRu = "Действует на объекты, созданные после старта матча. x = множитель обычного радиуса; режим числа = фиксированный радиус.";
		BtcBalanceExtra.AddAbs("vis.unit", "vis", "Vision: units", "Обзор: юниты", 1f, 0f, 5f, 0.05f, 20f, 200f, 1f, "blocks", "блоков", hintEn, hintRu);
		BtcBalanceExtra.AddAbs("vis.crystal", "vis", "Vision: crystals", "Обзор: кристаллы", 1f, 0f, 5f, 0.05f, 20f, 200f, 1f, "blocks", "блоков", hintEn, hintRu);
		BtcBalanceExtra.AddAbs("vis.ward", "vis", "Vision: wards (all except Lantern)", "Обзор: варды (кроме Lantern)", 1f, 0f, 5f, 0.05f, 20f, 200f, 1f, "blocks", "блоков", hintEn, hintRu);
		BtcBalanceExtra.AddAbs("vis.lantern", "vis", "Vision: Lantern Ward", "Обзор: Lantern Ward", 1f, 0f, 5f, 0.05f, 20f, 200f, 1f, "blocks", "блоков", hintEn, hintRu);
		BtcBalanceExtra.AddAbs("sent.range", "rng", "Sentinel Ward: range x", "Sentinel Ward: дальность x", 1f, 0.1f, 5f, 0.05f, 15f, 100f, 0.5f, "blocks", "блоков", "Targeting range. Applies to wards placed after the match starts.", "Дальность поиска целей. Действует на варды, поставленные после старта матча.");
		BtcBalanceExtra.AddAbs("sent.dmg", "dmg", "Sentinel Ward: damage x", "Sentinel Ward: урон x", 1f, 0f, 10f, 0.05f, 60f, 1000f, 1f, "damage", "урона", "Damage of one shot (vanilla 60).", "Урон одного выстрела (ваниль 60).");
		string str = "Nodes the pathfinder may expand per update for this kind of search. Higher = long paths are found in fewer frames and units react faster, but it costs CPU every frame (FPS/tick drops with many units). Lower = lighter, but units wait longer for a route. -1 = vanilla value.";
		string str2 = "Сколько узлов поиск может разобрать за один апдейт для этого вида поиска. Больше = длинные пути находятся за меньше кадров, юниты реагируют быстрее, но это нагрузка на CPU каждый кадр (при куче юнитов падает FPS/тик). Меньше = легче, но юниты дольше ждут маршрут. -1 = ваниль.";
		BtcGameplay.Add("path.default", "path", "Units: search budget (default, vanilla 100)", "Юниты: бюджет поиска пути (обычный, ваниль 100)", -1f, -1f, 2000f, 10f, false, "Regular bricktron navigation (build, mine, carry, move orders). " + str, "Обычная навигация бриктронов (стройка, добыча, перенос, приказы движения). " + str2);
		BtcGameplay.Add("path.free", "path", "Units: search budget (free agents, vanilla 40)", "Юниты: бюджет поиска пути (свободные, ваниль 40)", -1f, -1f, 2000f, 5f, false, "Units that act on their own without orders (idle/free agents). " + str, "Юниты, которые действуют сами без приказов (свободные агенты). " + str2);
		BtcGameplay.Add("path.corr", "path", "Corruptrons: search budget (vanilla 20)", "Коррутроны: бюджет поиска пути (ваниль 20)", -1f, -1f, 2000f, 5f, false, "Enemy AI units (faction is AI). " + str, "Вражеские ИИ-юниты (фракция ИИ). " + str2);
		BtcGameplay.Add("path.build", "path", "Build projects: search budget (vanilla 40)", "Проекты стройки: бюджет поиска (ваниль 40)", -1f, -1f, 2000f, 5f, false, "How many build goals are checked per update when assigning construction work. " + str, "Сколько целей стройки проверяется за апдейт при раздаче работы строителям. " + str2);
		BtcGameplay.Add("path.kupd", "path", "Knowledge: region updater budget (vanilla 100)", "Знание карты: бюджет обновления регионов (ваниль 100)", -1f, -1f, 2000f, 10f, false, "Background flood-fill that tracks which areas are connected (used to decide what is reachable). " + str, "Фоновая заливка, которая следит, какие зоны связаны (решает, что достижимо). " + str2);
		BtcGameplay.Add("path.kexc", "path", "Knowledge: access excluder budget (vanilla 40)", "Знание карты: бюджет исключения доступа (ваниль 40)", -1f, -1f, 2000f, 5f, false, "Background check that removes unreachable places from the knowledge. " + str, "Фоновая проверка, которая убирает недостижимые места из знания карты. " + str2);
		BtcGameplay.Add("start.workers", "eco", "Starting bricktrons: workers", "Стартовые бриктроны: рабочие", -1f, -1f, 100f, 1f, false, "-1 = take the value from the gamemode preset (vanilla Versus: 6). Applies at match start.", "-1 = значение из пресета режима (ваниль Versus: 6). Действует на старте матча.");
		BtcGameplay.Add("start.knight", "eco", "Starting bricktrons: knights", "Стартовые бриктроны: рыцари", -1f, -1f, 100f, 1f, false, "-1 = preset value (vanilla 0).", "-1 = значение из пресета (ваниль 0).");
		BtcGameplay.Add("start.archer", "eco", "Starting bricktrons: archers", "Стартовые бриктроны: лучники", -1f, -1f, 100f, 1f, false, "-1 = preset value (vanilla 0).", "-1 = значение из пресета (ваниль 0).");
		BtcQol.AddRows();
		BtcFocusMark.AddRows();
	}

	private static void Init()
	{
		bool init = BtcBalanceExtra._init;
		if (!init)
		{
			BtcBalanceExtra._init = true;
			BtcBalanceExtra._vis = new int[]
			{
				BtcGameplay.Idx("vis.unit"),
				BtcGameplay.Idx("vis.crystal"),
				BtcGameplay.Idx("vis.ward"),
				BtcGameplay.Idx("vis.lantern")
			};
			BtcBalanceExtra._iSR = BtcGameplay.Idx("sent.range");
			BtcBalanceExtra._iSD = BtcGameplay.Idx("sent.dmg");
		}
	}

	private static float Resolve(float[] e, int i, float baseValue)
	{
		BtcGameplay.Def def = BtcGameplay.Defs[i];
		bool flag = def.Abs && def.ModeIdx >= 0 && e[def.ModeIdx] > 0.5f;
		float result;
		if (flag)
		{
			result = e[def.NumIdx];
		}
		else
		{
			result = baseValue * e[i];
		}
		return result;
	}

	private static bool Neutral(float[] e, int i)
	{
		BtcGameplay.Def def = BtcGameplay.Defs[i];
		return Mathf.Abs(e[i] - 1f) < 0.0001f && (def.ModeIdx < 0 || e[def.ModeIdx] <= 0.5f);
	}

	public static int Vision(GameObject go, int baseRange)
	{
		BtcBalanceExtra.Init();
		float[] e = BtcGameplay.E;
		bool flag = e == null || BtcBalanceExtra._vis[0] < 0;
		int result;
		if (flag)
		{
			result = baseRange;
		}
		else
		{
			bool flag2 = BtcConfig.LogVis.Value;
			bool flag3 = !flag2;
			if (flag3)
			{
				bool flag4 = true;
				for (int i = 0; i < 4; i++)
				{
					bool flag5 = !BtcBalanceExtra.Neutral(e, BtcBalanceExtra._vis[i]);
					if (flag5)
					{
						flag4 = false;
						break;
					}
				}
				bool flag6 = flag4;
				if (flag6)
				{
					return baseRange;
				}
			}
			string name = go.name;
			bool flag7 = name.IndexOf("lantern", StringComparison.OrdinalIgnoreCase) >= 0;
			int num;
			if (flag7)
			{
				num = 3;
			}
			else
			{
				bool flag8 = go.GetComponent<WardController>() != null;
				if (flag8)
				{
					num = 2;
				}
				else
				{
					bool flag9 = name.IndexOf("crystal", StringComparison.OrdinalIgnoreCase) >= 0;
					if (flag9)
					{
						num = 1;
					}
					else
					{
						bool flag10 = go.GetComponent<Profession>() != null;
						if (flag10)
						{
							num = 0;
						}
						else
						{
							num = -1;
						}
					}
				}
			}
			bool flag11 = flag2 && BtcBalanceExtra._seen.Add(name);
			if (flag11)
			{
				Debug.Log(string.Concat(new object[]
				{
					"[BTC vis] ",
					name,
					" cat=",
					num,
					" range=",
					baseRange
				}));
			}
			bool flag12 = num < 0;
			if (flag12)
			{
				result = baseRange;
			}
			else
			{
				result = Mathf.Max(0, Mathf.RoundToInt(BtcBalanceExtra.Resolve(e, BtcBalanceExtra._vis[num], (float)baseRange)));
			}
		}
		return result;
	}

	public static float SentinelRange(float baseRange)
	{
		BtcBalanceExtra.Init();
		float[] e = BtcGameplay.E;
		bool flag = e == null || BtcBalanceExtra._iSR < 0;
		float result;
		if (flag)
		{
			result = baseRange;
		}
		else
		{
			result = Mathf.Max(0.5f, BtcBalanceExtra.Resolve(e, BtcBalanceExtra._iSR, baseRange));
		}
		return result;
	}

	public static float SentinelDamage(float baseDamage)
	{
		BtcBalanceExtra.Init();
		float[] e = BtcGameplay.E;
		bool flag = e == null || BtcBalanceExtra._iSD < 0;
		float result;
		if (flag)
		{
			result = baseDamage;
		}
		else
		{
			result = Mathf.Max(0f, BtcBalanceExtra.Resolve(e, BtcBalanceExtra._iSD, baseDamage));
		}
		return result;
	}

	public static void ApplyStart(Table t)
	{
		float[] e = BtcGameplay.E;
		bool flag = e == null || t == null;
		if (!flag)
		{
			string[] array = new string[]
			{
				"startingWorkersCount",
				"startingKnightCount",
				"startingArcherCount"
			};
			string[] array2 = new string[]
			{
				"start.workers",
				"start.knight",
				"start.archer"
			};
			for (int i = 0; i < 3; i++)
			{
				int num = BtcGameplay.Idx(array2[i]);
				bool flag2 = num >= 0 && e[num] >= 0f;
				if (flag2)
				{
					t[array[i]] = (long)Mathf.RoundToInt(e[num]);
				}
			}
		}
	}

	public static float WardRange(Component c, float baseRange)
	{
		BtcBalanceExtra.Init();
		float[] e = BtcGameplay.E;
		bool flag = e == null || BtcBalanceExtra._iSR < 0 || BtcBalanceExtra.Neutral(e, BtcBalanceExtra._iSR) || c == null;
		float result;
		if (flag)
		{
			result = baseRange;
		}
		else
		{
			bool flag2 = c.GetComponent<WardAttack>() == null;
			if (flag2)
			{
				result = baseRange;
			}
			else
			{
				result = BtcBalanceExtra.SentinelRange(baseRange);
			}
		}
		return result;
	}

	private static int PathVal(string id, int original)
	{
		float[] e = BtcGameplay.E;
		bool flag = e == null;
		int result;
		if (flag)
		{
			result = original;
		}
		else
		{
			int num = BtcGameplay.Idx(id);
			result = ((num >= 0 && e[num] >= 0f) ? Mathf.Max(1, Mathf.RoundToInt(e[num])) : original);
		}
		return result;
	}

	public static int NavBudget(bool ai, int original)
	{
		int result;
		if (ai)
		{
			result = BtcBalanceExtra.PathVal("path.corr", original);
		}
		else
		{
			result = ((original == GeneralRules.FreeAgentSearchPathBudget) ? BtcBalanceExtra.PathVal("path.free", original) : BtcBalanceExtra.PathVal("path.default", original));
		}
		return result;
	}

	public static int KnowledgeUpdaterBudget(int original)
	{
		return BtcBalanceExtra.PathVal("path.kupd", original);
	}

	public static int KnowledgeExcluderBudget(int original)
	{
		return BtcBalanceExtra.PathVal("path.kexc", original);
	}

	public static int BuildBudget(int original)
	{
		return BtcBalanceExtra.PathVal("path.build", original);
	}

	private static bool _init;

	private static int[] _vis;

	private static int _iLog;

	private static int _iSR;

	private static int _iSD;

	private static readonly HashSet<string> _seen = new HashSet<string>();
}
}
