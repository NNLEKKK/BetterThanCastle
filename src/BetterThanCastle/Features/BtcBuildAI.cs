using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Semantique;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcBuildAI
{
	public static void AddRows()
	{
		BtcGameplay.Add("bai.delayOn", "mov", "Bricktrons: shorter hourglass after a manual order", "Бриктроны: короче песочные часы после ручного приказа", 0f, 0f, 1f, 1f, true, "Vanilla: after your move order a builder waits 3 s (hourglass icon) before taking automatic work again. On = the pause from the slider below. Host setting.", "В ванили после твоего приказа строитель ждёт 3 с (иконка песочных часов), прежде чем снова брать работу сам. Вкл = пауза из слайдера ниже. Настройка хоста.");
		BtcGameplay.Add("bai.delay", "mov", "Hourglass pause (s), vanilla 3", "Пауза песочных часов (с), ваниль 3", 0f, 0f, 3f, 0.1f, false, "0 = takes automatic work at once. Used only when the checkbox above is on.", "0 = сразу берётся за автоматическую работу. Работает только при включённом чекбоксе выше.");
		BtcGameplay.Add("bai.wait", "mov", "Work search pause (ticks), vanilla 10", "Пауза поиска работы (тики), ваниль 10", 10f, 1f, 10f, 1f, false, "How long an idle bricktron waits between two checks for work. Lower = faster reaction but more CPU with many bricktrons. 10 = vanilla.", "Сколько свободный бриктрон ждёт между двумя проверками работы. Меньше = быстрее реакция, но больше нагрузка на CPU при куче бриктронов. 10 = ваниль.");
		BtcGameplay.Add("bai.build", "mov", "Sane build assignment", "Адекватное задание постройки", 0f, 0f, 1f, 1f, true, "Fixes for the build AI (host): 1) a goal gets only as many bricktrons as its missing resource needs (1 brick = 1 bricktron); 2) goals that accept the carried resource come first; a bricktron carrying the needed resource with full hands is accepted; 3) a goal that just became buildable (second part of a beam) is trusted even if the map knowledge has not caught up; 4) a bricktron keeps its resource for a while instead of running to storage or dropping it.", "Фиксы ИИ стройки (хост): 1) на цель идёт столько бриктронов, сколько нужно ресурса (1 кирпич = 1 бриктрон); 2) цели под несомый ресурс идут первыми, бриктрон с нужным ресурсом и полными руками принимается; 3) цель, которая только что стала доступной (вторая часть балки), берётся, даже если знание карты ещё не обновилось; 4) бриктрон некоторое время держит ресурс вместо бега на склад или сброса на пол.");
		BtcGameplay.Add("bai.hold", "mov", "Keep carried resource for (s)", "Держать ресурс в руках (с)", 10f, 0f, 60f, 1f, false, "Sane build assignment: how long a bricktron with no reachable goal keeps its resource and re-checks before going to storage or dropping it. 0 = vanilla behaviour.", "Адекватная постройка: сколько бриктрон без доступной цели держит ресурс и перепроверяет цели, прежде чем идти на склад или бросать. 0 = как в ванили.");
		BtcGameplay.Add("bai.trust", "mov", "Trust fresh goals for (s)", "Доверять свежим целям (с)", 20f, 0f, 120f, 1f, false, "Sane build assignment: for this long after a build goal changes state, the path finder result is trusted over the slow map knowledge.", "Адекватная постройка: столько секунд после смены состояния цели результату поиска пути доверяем больше, чем медленному знанию карты.");
	}

	private static bool On(ref int idx, string id)
	{
		bool flag = idx == -2;
		if (flag)
		{
			idx = BtcGameplay.Idx(id);
		}
		float[] e = BtcGameplay.E;
		return idx >= 0 && e != null && idx < e.Length && e[idx] > 0.5f;
	}

	private static float Val(ref int idx, string id, float def)
	{
		bool flag = idx == -2;
		if (flag)
		{
			idx = BtcGameplay.Idx(id);
		}
		float[] e = BtcGameplay.E;
		return (idx >= 0 && e != null && idx < e.Length) ? e[idx] : def;
	}

	public static bool Build
	{
		get
		{
			return BtcBuildAI.On(ref BtcBuildAI.iBuild, "bai.build");
		}
	}

	public static void Sync()
	{
		float num = 3f;
		bool flag = BtcBuildAI.On(ref BtcBuildAI.iDelayOn, "bai.delayOn");
		if (flag)
		{
			num = Mathf.Clamp(BtcBuildAI.Val(ref BtcBuildAI.iDelay, "bai.delay", 0f), 0f, 3f);
		}
		bool flag2 = num == BtcBuildAI._appliedDelay;
		if (!flag2)
		{
			BtcBuildAI._appliedDelay = num;
			try
			{
				bool flag3 = BtcBuildAI._expField == null;
				if (flag3)
				{
					BtcBuildAI._expField = typeof(AutonomyStatus).GetField("expirationTime", BindingFlags.Instance | BindingFlags.NonPublic);
				}
				bool flag4 = BtcBuildAI._expField != null;
				if (flag4)
				{
					BtcBuildAI._expField.SetValue(AutonomyStatus.InstinctOnly, num);
				}
			}
			catch (Exception)
			{
			}
		}
	}

	public static int WaitTicks()
	{
		BtcBuildAI.Sync();
		int num = (int)BtcBuildAI.Val(ref BtcBuildAI.iWait, "bai.wait", 10f);
		return (num < 1) ? 1 : ((num > 10) ? 10 : num);
	}

	public static bool Crowded(BuildGoal g, Labor labor)
	{
		bool flag = !BtcBuildAI.Build || g == null || labor == null;
		bool result;
		if (flag)
		{
			result = false;
		}
		else
		{
			bool flag2 = g.AssignedWorkerCount() == 0;
			if (flag2)
			{
				result = false;
			}
			else
			{
				bool flag3 = BtcBuildAI._workersField == null;
				if (flag3)
				{
					BtcBuildAI._workersField = typeof(Goal).GetField("workers", BindingFlags.Instance | BindingFlags.NonPublic);
					bool flag4 = BtcBuildAI._workersField == null;
					if (flag4)
					{
						return false;
					}
				}
				List<Labor> list = BtcBuildAI._workersField.GetValue(g) as List<Labor>;
				Recepteur recepteur = g.recepteur;
				bool flag5 = list == null || recepteur == null;
				if (flag5)
				{
					result = false;
				}
				else
				{
					Ressource ressource = recepteur.CurrentCapacity.GetRessources().FirstOrDefault();
					bool flag6 = ressource == null;
					if (flag6)
					{
						result = false;
					}
					else
					{
						int num = recepteur.CurrentCapacity.Value(ressource);
						bool flag7 = num <= 0;
						if (flag7)
						{
							result = false;
						}
						else
						{
							int num2 = 0;
							for (int i = 0; i < list.Count; i++)
							{
								Labor labor2 = list[i];
								bool flag8 = labor2 == null || labor2 == labor || labor2.recepteur == null;
								if (!flag8)
								{
									int num3 = labor2.recepteur.ContentDescription.Value(ressource);
									num2 += ((num3 > 1) ? num3 : 1);
									bool flag9 = num2 >= num;
									if (flag9)
									{
										BtcBuildAI.Log(string.Concat(new object[]
										{
											"crowded ",
											labor.name,
											" goal=",
											g.GetInstanceID(),
											" need=",
											num,
											" assigned=",
											list.Count
										}), labor.GetInstanceID() * 31 + g.GetInstanceID());
										return true;
									}
								}
							}
							result = false;
						}
					}
				}
			}
		}
		return result;
	}

	public static int Prio(BuildGoal g, Labor labor)
	{
		BtcBuildAI.Sync();
		bool flag = !BtcBuildAI.Build || g == null || labor == null;
		int result;
		if (flag)
		{
			result = 0;
		}
		else
		{
			Recepteur recepteur = labor.recepteur;
			Recepteur recepteur2 = g.recepteur;
			bool flag2 = recepteur == null || recepteur2 == null || !recepteur.CarriesSomething();
			if (flag2)
			{
				result = 0;
			}
			else
			{
				result = ((recepteur.FirstThatFitsIn(recepteur2) != null) ? 1000 : 0);
			}
		}
		return result;
	}

	public static bool CarriesOk(Labor labor, GameObject o)
	{
		bool flag = !BtcBuildAI.Build || labor == null || o == null || labor.recepteur == null;
		bool result;
		if (flag)
		{
			result = false;
		}
		else
		{
			bool flag2 = labor.recepteur.Carries(o);
			bool flag3 = flag2;
			if (flag3)
			{
				BtcBuildAI.Log("carried item accepted " + labor.name, labor.GetInstanceID());
			}
			result = flag2;
		}
		return result;
	}

	public static void Stamp(BuildGoal g)
	{
		bool flag = g == null;
		if (!flag)
		{
			bool flag2 = BtcBuildAI._stamp.Count > 4000;
			if (flag2)
			{
				BtcBuildAI._stamp.Clear();
			}
			BtcBuildAI._stamp[g.GetInstanceID()] = Time.time;
		}
	}

	public static bool Trust(BuildGoal g, Labor labor)
	{
		bool flag = !BtcBuildAI.Build || g == null;
		bool result;
		if (flag)
		{
			result = false;
		}
		else
		{
			float num;
			bool flag2 = !BtcBuildAI._stamp.TryGetValue(g.GetInstanceID(), out num);
			if (flag2)
			{
				result = false;
			}
			else
			{
				bool flag3 = Time.time - num > BtcBuildAI.Val(ref BtcBuildAI.iTrust, "bai.trust", 20f);
				if (flag3)
				{
					result = false;
				}
				else
				{
					BtcBuildAI.Log(string.Concat(new object[]
					{
						"trust fresh goal ",
						g.GetInstanceID(),
						" for ",
						(labor != null) ? labor.name : "?"
					}), g.GetInstanceID());
					result = true;
				}
			}
		}
		return result;
	}

	public static bool HoldCarry(Labor labor)
	{
		BtcBuildAI.Sync();
		bool flag = !BtcBuildAI.Build || labor == null;
		bool result;
		if (flag)
		{
			result = false;
		}
		else
		{
			Recepteur recepteur = labor.recepteur;
			int instanceID = labor.GetInstanceID();
			bool flag2 = recepteur == null || recepteur.IsEmpty();
			if (flag2)
			{
				bool flag3 = BtcBuildAI._hold.Count > 0;
				if (flag3)
				{
					BtcBuildAI._hold.Remove(instanceID);
				}
				result = false;
			}
			else
			{
				float num = BtcBuildAI.Val(ref BtcBuildAI.iHold, "bai.hold", 10f);
				bool flag4 = num <= 0f;
				if (flag4)
				{
					result = false;
				}
				else
				{
					BuildProject buildProject = labor.Project as BuildProject;
					bool flag5 = buildProject == null;
					if (flag5)
					{
						result = false;
					}
					else
					{
						float time = Time.time;
						Vector2 vector;
						bool flag6 = !BtcBuildAI._hold.TryGetValue(instanceID, out vector) || time - vector.y > 5f;
						if (flag6)
						{
							bool flag7 = !BtcBuildAI.HasMatchingGoal(buildProject, recepteur);
							if (flag7)
							{
								return false;
							}
							vector = new Vector2(time, time);
							bool flag8 = BtcBuildAI._hold.Count > 256;
							if (flag8)
							{
								BtcBuildAI._hold.Clear();
							}
							BtcBuildAI.Log("hold resource " + labor.name, instanceID);
						}
						bool flag9 = time - vector.x > num;
						if (flag9)
						{
							vector.y = time;
							BtcBuildAI._hold[instanceID] = vector;
							BtcBuildAI.Log("hold over, store " + labor.name, instanceID + 7);
							result = false;
						}
						else
						{
							vector.y = time;
							BtcBuildAI._hold[instanceID] = vector;
							result = true;
						}
					}
				}
			}
		}
		return result;
	}

	private static bool HasMatchingGoal(BuildProject bp, Recepteur carried)
	{
		bool flag = BtcBuildAI._goalsField == null;
		if (flag)
		{
			BtcBuildAI._goalsField = typeof(BuildProject).GetField("goals", BindingFlags.Instance | BindingFlags.NonPublic);
			bool flag2 = BtcBuildAI._goalsField == null;
			if (flag2)
			{
				return false;
			}
		}
		BuildGoalProvider buildGoalProvider = BtcBuildAI._goalsField.GetValue(bp) as BuildGoalProvider;
		bool flag3 = buildGoalProvider == null || buildGoalProvider.TrackedBlueprints.Count == 0;
		bool result;
		if (flag3)
		{
			result = false;
		}
		else
		{
			int num = 0;
			foreach (Blueprint blueprint in buildGoalProvider.TrackedBlueprints)
			{
				bool flag4 = blueprint == null;
				if (!flag4)
				{
					num++;
					bool flag5 = num > 64;
					if (flag5)
					{
						return true;
					}
					BuildGoal component = blueprint.GetComponent<BuildGoal>();
					bool flag6 = component != null && !component.IsAnti && component.recepteur != null && carried.FirstThatFitsIn(component.recepteur) != null;
					if (flag6)
					{
						return true;
					}
				}
			}
			result = false;
		}
		return result;
	}

	private static void Log(string msg, int key)
	{
		bool flag = !BtcConfig.LogBuildAi.Value;
		if (!flag)
		{
			float unscaledTime = Time.unscaledTime;
			float num;
			bool flag2 = BtcBuildAI._logNext.TryGetValue(key, out num) && unscaledTime < num;
			if (!flag2)
			{
				bool flag3 = BtcBuildAI._logNext.Count > 512;
				if (flag3)
				{
					BtcBuildAI._logNext.Clear();
				}
				BtcBuildAI._logNext[key] = unscaledTime + 2f;
				try
				{
					bool flag4 = BtcBuildAI._path == null;
					if (flag4)
					{
						BtcBuildAI._path = BtcPaths.DebugFile("btc_netdiag.txt");
					}
					File.AppendAllText(BtcBuildAI._path, string.Concat(new string[]
					{
						"[BuildAI ",
						unscaledTime.ToString("F1"),
						"] ",
						msg,
						"\r\n"
					}));
				}
				catch (Exception)
				{
				}
			}
		}
	}

	private static int iDelayOn = -2;

	private static int iDelay = -2;

	private static int iWait = -2;

	private static int iBuild = -2;

	private static int iHold = -2;

	private static int iTrust = -2;

	private static int iLog = -2;

	private static float _appliedDelay = -1f;

	private static FieldInfo _expField;

	private static FieldInfo _workersField;

	private static readonly Dictionary<int, float> _stamp = new Dictionary<int, float>();

	private static FieldInfo _goalsField;

	private static readonly Dictionary<int, Vector2> _hold = new Dictionary<int, Vector2>();

	private static string _path;

	private static readonly Dictionary<int, float> _logNext = new Dictionary<int, float>();
}
}
