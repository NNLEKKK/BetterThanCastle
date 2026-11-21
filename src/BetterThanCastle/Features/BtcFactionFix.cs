using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Lua;
using Brix.Network;
using Brix.UI;
using Brix.UI.Builder.Menu;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
public static class BtcFactionFix
{
	public static void AddRows()
	{
		BtcGameplay.Add("spec.facfix", "gen", "Replay missed faction events after coming back", "Повторить пропущенные события фракций после возвращения", 1f, 0f, 1f, 1f, true, "Client side. A late join misses the 'faction changed' events, so idle project, shared vision, fog and the HUD lists stay empty. Replays them once (3 passes in the first 15 s). Fixes HUD / minimap / task list errors and vanishing units.", "На стороне клиента. При позднем входе пропускаются события «смена фракции»: проект безделья, общее зрение, туман и списки интерфейса остаются пустыми. Один раз повторяет их (3 прохода за первые 15 с). Лечит ошибки интерфейса / миникарты / списка задач и пропадающих юнитов.");
		BtcGameplay.Add("spec.facidle", "gen", "Register the idle project by hand if still missing", "Регистрировать проект безделья вручную, если его нет", 1f, 0f, 1f, 1f, true, null, null);
		BtcGameplay.Add("spec.facsel", "gen", "Refresh the task panel selection after the replay", "Обновить выбор в панели заданий после повтора", 1f, 0f, 1f, 1f, true, "Client side. Once the idle project exists, the Lua selection cache (title of the task bar) is refreshed one time. If the idle task popup opens by itself after coming back, switch this off.", "На стороне клиента. Когда проект безделья появился, один раз обновляется Lua-кэш выбора (заголовок панели заданий). Если после возвращения сама открывается панель безделья, выключите.");
		BtcGameplay.Add("spec.facspec", "gen", "Spectator: show health of all player factions", "Зритель: показывать здоровье всех фракций игроков", 1f, 0f, 1f, 1f, true, "Client side. A spectator has no relation to the players, so their units never enter the enemy / allied lists the health bars and numbers are built from. This marks every non-spectator faction as 'enemy' in the spectator's own table (local only, the host is not touched).", "На стороне клиента. У зрителя нет отношений с игроками, поэтому их юниты не попадают в списки врагов / союзников, из которых строятся полоски и цифры здоровья. Все не-зрительские фракции помечаются «врагом» в локальной таблице зрителя (только у него, хост не затрагивается).");
		BtcGameplay.Add("spec.faclua", "gen", "Conquest: tell the late client that the host is ready (builds the score panel)", "Conquest: сообщить вошедшему позже клиенту, что хост готов (строит панель score)", 1f, 0f, 1f, 1f, true, "Client side. The mode's clientside.lua builds the score panel (timer, minimap, storages) only after the host's 'GameTime' message, which a late joiner never gets. The same message is delivered locally once the level is ready.", "На стороне клиента. clientside.lua режима строит панель score (таймер, миникарта, склады) только после сообщения хоста «GameTime», которое вошедший позже не получает. То же сообщение один раз доставляется локально, когда уровень готов.");
		BtcFactionFix.iOn = BtcGameplay.Idx("spec.facfix");
		BtcFactionFix.iLua = BtcGameplay.Idx("spec.faclua");
		BtcFactionFix.iSpec = BtcGameplay.Idx("spec.facspec");
		BtcFactionFix.iSel = BtcGameplay.Idx("spec.facsel");
		BtcFactionFix.iIdle = BtcGameplay.Idx("spec.facidle");
	}

	private static bool On(int i)
	{
		float[] e = BtcGameplay.E;
		return e == null || i < 0 || i >= e.Length || e[i] > 0.5f;
	}

	public static void Reset()
	{
		BtcFactionFix._step = 0;
		BtcFactionFix._armed = false;
		BtcFactionFix._selDone = false;
		BtcFactionFix._luaDone = false;
		BtcFactionFix._dumpDone = false;
		BtcFactionFix._dumpArmed = false;
		BtcFactionFix._dumpN = 0;
	}

	public static void Tick(float now)
	{
		bool flag = !BtcFactionFix._luaDone;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcFactionFix.LuaHostReady();
		}
		bool flag4 = BtcFactionFix._step >= BtcFactionFix.Delays.Length;
		bool flag5 = !flag4;
		bool flag6 = flag5;
		if (flag6)
		{
			bool flag7 = !BtcFactionFix._armed;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				BtcFactionFix._armed = true;
				BtcFactionFix._t0 = now;
			}
			bool flag10 = now - BtcFactionFix._t0 < BtcFactionFix.Delays[BtcFactionFix._step];
			bool flag11 = !flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				BtcFactionFix._step++;
				bool flag13 = BtcFactionFix.On(BtcFactionFix.iOn);
				bool flag14 = flag13;
				bool flag15 = flag14;
				if (flag15)
				{
					BtcFactionFix.Run(BtcFactionFix._step);
				}
			}
		}
	}

	private static void Init()
	{
		bool init = BtcFactionFix._init;
		bool flag = !init;
		bool flag2 = flag;
		if (flag2)
		{
			BtcFactionFix._init = true;
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			BtcFactionFix._miChanged = typeof(AffiliationComponent).GetMethod("FactionChangedActions", bindingAttr);
			BtcFactionFix._fiOld = typeof(AffiliationComponent).GetField("oldFactionGO", bindingAttr);
			BtcFactionFix._fiNetId = typeof(AffiliationComponent).GetField("___factionGONetId", bindingAttr);
			BtcFactionFix._fiIdle = typeof(IdleProject).GetField("_faction2Projects", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			BtcSpectate.Log(string.Concat(new string[]
			{
				"facfix: reflection changed=",
				(BtcFactionFix._miChanged != null).ToString(),
				" old=",
				(BtcFactionFix._fiOld != null).ToString(),
				" netId=",
				(BtcFactionFix._fiNetId != null).ToString(),
				" idleMap=",
				(BtcFactionFix._fiIdle != null).ToString()
			}));
		}
	}

	private static void Run(int pass)
	{
		try
		{
			BtcFactionFix.Init();
			bool flag = BtcFactionFix._miChanged == null || BtcFactionFix._fiOld == null;
			bool flag2 = !flag;
			bool flag3 = flag2;
			if (flag3)
			{
				bool flag4 = BtcConfig.LogFaction.Value;
				Faction defaultNeutral = Faction.DefaultNeutral;
				AffiliationComponent[] array = UnityEngine.Object.FindObjectsOfType<AffiliationComponent>();
				int num = 0;
				int num2 = 0;
				int num3 = 0;
				int num4 = 0;
				foreach (AffiliationComponent affiliationComponent in array)
				{
					bool flag5 = affiliationComponent == null;
					bool flag6 = !flag5;
					bool flag7 = flag6;
					if (flag7)
					{
						bool flag8 = affiliationComponent.factionGO == null && BtcFactionFix._fiNetId != null;
						bool flag9 = flag8;
						bool flag10 = flag9;
						if (flag10)
						{
							NetworkInstanceId netId = (NetworkInstanceId)BtcFactionFix._fiNetId.GetValue(affiliationComponent);
							bool flag11 = !netId.IsEmpty();
							bool flag12 = flag11;
							bool flag13 = flag12;
							if (flag13)
							{
								GameObject gameObject = ClientScene.FindLocalObject(netId);
								bool flag14 = gameObject != null;
								bool flag15 = flag14;
								bool flag16 = flag15;
								if (flag16)
								{
									affiliationComponent.factionGO = gameObject;
									num++;
								}
							}
						}
						bool flag17 = affiliationComponent.factionGO == null || BtcFactionFix._fiOld.GetValue(affiliationComponent) == affiliationComponent.factionGO;
						bool flag18 = !flag17;
						bool flag19 = flag18;
						if (flag19)
						{
							try
							{
								bool flag20 = affiliationComponent.GetComponent<Labor>() != null;
								bool flag21 = flag20;
								bool flag22 = flag21;
								if (flag22)
								{
									Faction component = affiliationComponent.factionGO.GetComponent<Faction>();
									BtcFactionFix._fiOld.SetValue(affiliationComponent, affiliationComponent.factionGO);
									GameSignals.Invoke<GameObject, Faction, Faction>(AffiliationComponent.AnyObjectChangedFaction, affiliationComponent.gameObject, defaultNeutral, component);
									num3++;
								}
								else
								{
									BtcFactionFix._miChanged.Invoke(affiliationComponent, null);
									num2++;
								}
							}
							catch (Exception ex)
							{
								num4++;
								bool flag23 = num4 <= 4;
								bool flag24 = flag23;
								bool flag25 = flag24;
								if (flag25)
								{
									BtcSpectate.Log("facfix: " + affiliationComponent.name + ": " + ((ex.InnerException != null) ? ex.InnerException.Message : ex.Message));
								}
							}
						}
					}
				}
				int num5 = 0;
				bool flag26 = BtcFactionFix.On(BtcFactionFix.iSpec);
				bool flag27 = flag26;
				bool flag28 = flag27;
				if (flag28)
				{
					num5 = BtcFactionFix.SpectatorEnemies();
				}
				int num6 = 0;
				bool flag29 = BtcFactionFix.On(BtcFactionFix.iIdle);
				bool flag30 = flag29;
				bool flag31 = flag30;
				if (flag31)
				{
					num6 = BtcFactionFix.RegisterIdle();
				}
				IdleProject alliedProject = IdleProject.GetAlliedProject();
				bool flag32 = pass >= 2 && alliedProject != null && !BtcFactionFix._selDone && BtcFactionFix.On(BtcFactionFix.iSel);
				bool flag33 = flag32;
				bool flag34 = flag33;
				if (flag34)
				{
					BtcFactionFix._selDone = true;
					BtcFactionFix.RefreshSelection(alliedProject);
				}
				BtcSpectate.Log(string.Concat(new object[]
				{
					"facfix: pass ",
					pass,
					": ",
					array.Length,
					" components, resolved ",
					num,
					", events replayed ",
					num2,
					", units (signal only) ",
					num3,
					", errors ",
					num4,
					", spectator enemy marks ",
					num5,
					", idle project registered by hand ",
					num6,
					", GetAlliedProject=",
					(alliedProject != null) ? "ok" : "NULL",
					flag4 ? (" localFaction=" + BtcFactionFix.FactionName()) : string.Empty
				}));
			}
		}
		catch (Exception arg)
		{
			BtcSpectate.Log("facfix Run: " + arg);
		}
	}

	private static void RefreshSelection(IdleProject idle)
	{
		try
		{
			bool flag = BtcFactionFix._fiSel == null;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				BtcFactionFix._fiSel = typeof(Project).GetField("lua_Selected", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			}
			LuaHook<Project> luaHook = (BtcFactionFix._fiSel != null) ? (BtcFactionFix._fiSel.GetValue(null) as LuaHook<Project>) : null;
			bool flag4 = luaHook == null;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				BtcSpectate.Log("facfix: selected hook not found");
			}
			else
			{
				Project project = (UIGameObserver.projects != null) ? UIGameObserver.projects.CurrentSelected : null;
				LuaHookBase.InvokeFromCLR<Project>(luaHook, (project != null) ? project : idle);
				BtcSpectate.Log("facfix: Lua selection refreshed (" + ((project != null) ? "selected project" : "idle project") + ")");
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("facfix RefreshSelection: " + ex.Message);
		}
	}

	private static string FactionName()
	{
		string result;
		try
		{
			Faction faction = (User.LocalUser != null) ? User.LocalUser.faction : null;
			result = ((faction != null) ? faction.name : "null");
		}
		catch (Exception)
		{
			result = "?";
		}
		return result;
	}

	private static int RegisterIdle()
	{
		int num = 0;
		try
		{
			bool flag = BtcFactionFix._fiIdle == null;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				return 0;
			}
			Dictionary<Faction, IdleProject> dictionary = BtcFactionFix._fiIdle.GetValue(null) as Dictionary<Faction, IdleProject>;
			bool flag4 = dictionary == null;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				return 0;
			}
			foreach (IdleProject idleProject in UnityEngine.Object.FindObjectsOfType<IdleProject>())
			{
				Faction faction = (idleProject != null) ? idleProject.faction : null;
				bool flag7 = faction != null && faction != Faction.DefaultNeutral && !dictionary.ContainsKey(faction);
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					dictionary[faction] = idleProject;
					num++;
				}
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("facfix RegisterIdle: " + ex.Message);
		}
		return num;
	}

	private static int SpectatorEnemies()
	{
		int num = 0;
		try
		{
			Faction faction = (User.LocalUser != null) ? User.LocalUser.faction : null;
			bool flag = faction == null || !faction.IsSpectator;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				return 0;
			}
			Faction defaultNeutral = Faction.DefaultNeutral;
			foreach (Faction faction2 in UnityEngine.Object.FindObjectsOfType<Faction>())
			{
				bool flag4 = faction2 == null || faction2 == faction || faction2 == defaultNeutral || faction2.IsSpectator;
				bool flag5 = !flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					Affiliation.Relation relation;
					bool flag7 = !faction.AffiliationMap.TryGetValue(faction2, out relation) || relation != Affiliation.Relation.Enemy;
					bool flag8 = flag7;
					bool flag9 = flag8;
					if (flag9)
					{
						faction.AffiliationMap[faction2] = Affiliation.Relation.Enemy;
						num++;
					}
				}
			}
			bool flag10 = num > 0;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				bool flag13 = BtcFactionFix._miRebuild == null;
				bool flag14 = flag13;
				bool flag15 = flag14;
				if (flag15)
				{
					BtcFactionFix._miRebuild = typeof(BtcSpectate).GetMethod("RebuildFactionLists", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				}
				bool flag16 = BtcFactionFix._miRebuild != null;
				bool flag17 = flag16;
				bool flag18 = flag17;
				if (flag18)
				{
					BtcFactionFix._miRebuild.Invoke(null, new object[]
					{
						faction
					});
				}
				else
				{
					BtcSpectate.Log("facfix: RebuildFactionLists not found");
				}
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("facfix SpectatorEnemies: " + ((ex.InnerException != null) ? ex.InnerException.Message : ex.Message));
		}
		return num;
	}

	public static void Frame(float now)
	{
		bool flag = SceneManager.GetActiveScene().buildIndex != 2;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcFactionFix._dumpArmed = false;
			BtcFactionFix._dumpDone = false;
			BtcFactionFix._dumpN = 0;
		}
		else
		{
			bool flag4 = BtcFactionFix._dumpDone || !BtcConfig.LogFactionDump.Value;
			bool flag5 = !flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				bool flag7 = !BtcFactionFix._dumpArmed;
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					BtcFactionFix._dumpArmed = true;
					BtcFactionFix._dumpAt = now + 20f;
				}
				else
				{
					bool flag10 = now >= BtcFactionFix._dumpAt;
					bool flag11 = flag10;
					bool flag12 = flag11;
					if (flag12)
					{
						bool flag13 = ++BtcFactionFix._dumpN >= 2;
						bool flag14 = flag13;
						bool flag15 = flag14;
						if (flag15)
						{
							BtcFactionFix._dumpDone = true;
						}
						BtcFactionFix._dumpAt = now + 40f;
						BtcFactionFix.Dump();
					}
				}
			}
		}
	}

	private static string PathOf(Transform t)
	{
		StringBuilder stringBuilder = new StringBuilder();
		Transform transform = t;
		while (transform != null)
		{
			stringBuilder.Insert(0, "/" + transform.name + (transform.gameObject.activeSelf ? string.Empty : "(off)"));
			transform = transform.parent;
		}
		return stringBuilder.ToString();
	}

	private static void Dump()
	{
		try
		{
			string text = NetworkServer.active ? "host" : (NetworkClient.active ? "client" : "none");
			MiniMap[] array = Resources.FindObjectsOfTypeAll<MiniMap>();
			int num = 0;
			for (int i = 0; i < array.Length; i++)
			{
				bool flag = array[i] == null || !array[i].gameObject.scene.IsValid();
				bool flag2 = !flag;
				bool flag3 = flag2;
				if (flag3)
				{
					num++;
					RectTransform component = array[i].GetComponent<RectTransform>();
					BtcSpectate.Log(string.Concat(new string[]
					{
						"dump(",
						text,
						"): MiniMap ",
						BtcFactionFix.PathOf(array[i].transform),
						" activeInHierarchy=",
						array[i].gameObject.activeInHierarchy.ToString(),
						" enabled=",
						array[i].enabled.ToString(),
						" heightmap=",
						(array[i].heightmap != null).ToString(),
						" rect=",
						(component != null) ? (component.rect.width.ToString("0") + "x" + component.rect.height.ToString("0")) : "-"
					}));
				}
			}
			int num2 = 0;
			Transform[] array2 = Resources.FindObjectsOfTypeAll<Transform>();
			int num3 = 0;
			while (num3 < array2.Length && num2 < 60)
			{
				Transform transform = array2[num3];
				bool flag4 = transform == null || !transform.gameObject.scene.IsValid() || !BtcFactionFix.HudName(transform.name);
				bool flag5 = !flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					num2++;
					BtcSpectate.Log(string.Concat(new string[]
					{
						"dump(",
						text,
						"): named '",
						transform.name,
						"' ",
						BtcFactionFix.PathOf(transform),
						" activeInHierarchy=",
						transform.gameObject.activeInHierarchy.ToString()
					}));
				}
				num3++;
			}
			foreach (Transform transform2 in array2)
			{
				bool flag7 = transform2 == null || !transform2.gameObject.scene.IsValid() || transform2.parent == null || transform2.parent.name != "GameScreen" || transform2.name.IndexOf("(Lua)", StringComparison.Ordinal) < 0;
				bool flag8 = !flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					BtcSpectate.Log(string.Concat(new object[]
					{
						"dump(",
						text,
						"): luaMenu '",
						transform2.name,
						"' active=",
						transform2.gameObject.activeInHierarchy.ToString(),
						" children=",
						transform2.childCount,
						" descendants=",
						transform2.GetComponentsInChildren<Transform>(true).Length
					}));
					bool flag10 = transform2.name.IndexOf("Scoreboard", StringComparison.OrdinalIgnoreCase) >= 0;
					bool flag11 = flag10;
					bool flag12 = flag11;
					if (flag12)
					{
						int num4 = 90;
						BtcFactionFix.Tree(transform2, 0, text, ref num4);
					}
				}
			}
			BtcSpectate.Log(string.Concat(new object[]
			{
				"dump(",
				text,
				"): MiniMap components ",
				num,
				", objects with 'minimap' in the name ",
				num2,
				", showsMinimap=",
				UIGameObserver.ShowsMinimap.ToString()
			}));
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("facfix Dump: " + ex.Message);
		}
	}

	private static bool HudName(string n)
	{
		return n.IndexOf("inimap", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("score", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("timer", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("storage", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("SidebarMap", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static void LuaHostReady()
	{
		bool flag = !BtcFactionFix.On(BtcFactionFix.iLua) || !GameSignals.Received<Action>(GameSignals.LevelReady);
		bool flag2 = !flag;
		if (flag2)
		{
			BtcFactionFix._luaDone = true;
			BtcSpectate.Log("facfix: level ready, delivering lua server messages, gameTime=" + GameClock.TimeSinceGameStart.ToString("0.0"));
			BtcFactionFix.CleanStaleLuaUsers();
			BtcFactionFix.FixUserFactions();
			bool flag3 = BtcFactionFix.TryLua("{0}", 0, false, 0f);
			bool flag4 = BtcFactionFix.TryLua("{1}", 1, false, 0f);
			User localUser = User.LocalUser;
			Faction faction = null;
			bool flag5 = false;
			try
			{
				bool flag6 = localUser != null && localUser.faction != null && localUser.faction.IsSpectator;
				bool flag7 = flag6;
				if (flag7)
				{
					Faction faction2 = BtcFactionFix.PickPlayerFaction(localUser);
					bool flag8 = faction2 != null;
					bool flag9 = flag8;
					if (flag9)
					{
						faction = localUser.faction;
						localUser.faction = faction2;
						flag5 = true;
						BtcSpectate.Log("facfix: spectator, building the score panel as faction '" + faction2.name + "'");
					}
				}
			}
			catch (Exception ex)
			{
				BtcSpectate.Log("facfix spectator swap: " + ex.Message);
			}
			bool flag10 = BtcFactionFix.TryLua("{3}", 3, true, GameClock.TimeSinceGameStart);
			bool flag11 = flag5;
			bool flag12 = flag11;
			if (flag12)
			{
				try
				{
					localUser.faction = faction;
					BtcSpectate.Log("facfix: local faction restored to '" + faction.name + "'");
				}
				catch (Exception ex2)
				{
					BtcSpectate.Log("facfix spectator restore: " + ex2.Message);
				}
			}
			BtcSpectate.Log(string.Concat(new string[]
			{
				"facfix: lua messages delivered: {0}=",
				flag3 ? "ok" : "FAILED",
				" {1}=",
				flag4 ? "ok" : "FAILED",
				" {3}=",
				flag10 ? "ok" : "FAILED"
			}));
			bool flag13 = !flag10;
			bool flag14 = flag13;
			if (flag14)
			{
				BuilderUtility.QueueRefreshOptimization = false;
				BuilderUtility.QueueTreeRefreshOptimization = false;
				BtcSpectate.Log("facfix: score panel failed, UI refresh optimisation flags restored");
			}
		}
	}

	private static void SendLua(int type, bool withTime, float t)
	{
		Dictionary<object, object> dictionary = new Dictionary<object, object>();
		dictionary[1.0] = (double)type;
		if (withTime)
		{
			dictionary[2.0] = (double)t;
		}
		bool flag = type == 0;
		bool flag2 = flag;
		if (flag2)
		{
			dictionary[2.0] = new LuaTimer(0f, false);
		}
		Table a = LuaLibs.DeserialiseTable(dictionary, null);
		LuaRegistry.sig_serverMessage.Invoke(a);
	}

	private static void Tree(Transform t, int depth, string role, ref int budget)
	{
		bool flag = budget <= 0 || depth > 12;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			budget--;
			string text = t.name.Replace("{LuaMenu}", "{L}").Trim();
			BtcSpectate.Log(string.Concat(new object[]
			{
				"dump(",
				role,
				"): tree ",
				new string(' ', depth),
				text,
				t.gameObject.activeSelf ? string.Empty : " (off)",
				" [",
				t.childCount,
				"]"
			}));
			for (int i = 0; i < t.childCount; i++)
			{
				BtcFactionFix.Tree(t.GetChild(i), depth + 1, role, ref budget);
			}
		}
	}

	private static bool TryLua(string tag, int type, bool withTime, float t)
	{
		bool result;
		try
		{
			BtcFactionFix.SendLua(type, withTime, t);
			result = true;
		}
		catch (Exception ex)
		{
			string decoratedMessage = null;
			try
			{
				InterpreterException ex2 = ex as InterpreterException;
				bool flag = ex2 == null && ex.InnerException != null;
				bool flag2 = flag;
				if (flag2)
				{
					ex2 = (ex.InnerException as InterpreterException);
				}
				bool flag3 = ex2 != null;
				bool flag4 = flag3;
				if (flag4)
				{
					decoratedMessage = ex2.DecoratedMessage;
					BtcSpectate.Log("facfix lua " + tag + " ERROR: " + ex2.DecoratedMessage);
					bool flag5 = ex2.CallStack != null;
					bool flag6 = flag5;
					if (flag6)
					{
						for (int i = 0; i < ex2.CallStack.Count; i++)
						{
							BtcSpectate.Log(string.Concat(new object[]
							{
								"facfix lua ",
								tag,
								"   stack[",
								i,
								"] ",
								ex2.CallStack[i]
							}));
						}
					}
				}
				else
				{
					BtcSpectate.Log(string.Concat(new object[]
					{
						"facfix lua ",
						tag,
						" exception: ",
						ex
					}));
				}
			}
			catch (Exception ex3)
			{
				BtcSpectate.Log(string.Concat(new string[]
				{
					"facfix lua ",
					tag,
					" (error while reporting): ",
					ex3.Message,
					" | original: ",
					ex.Message
				}));
			}
			BtcFactionFix.DumpLuaSources(decoratedMessage);
			result = false;
		}
		return result;
	}

	private static void DumpLuaSources(string decoratedMessage)
	{
		try
		{
			string text = null;
			int num = -1;
			bool flag = !string.IsNullOrEmpty(decoratedMessage);
			bool flag2 = flag;
			if (flag2)
			{
				Match match = Regex.Match(decoratedMessage, "^(.*?):\\((\\d+),");
				bool success = match.Success;
				bool flag3 = success;
				if (flag3)
				{
					text = match.Groups[1].Value.Replace('\\', '/');
					num = int.Parse(match.Groups[2].Value);
				}
			}
			FieldInfo field = typeof(BrixScriptLoader).GetField("_fileCache", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			IDictionary dictionary = (field != null) ? (field.GetValue(null) as IDictionary) : null;
			bool flag4 = dictionary == null;
			bool flag5 = flag4;
			if (flag5)
			{
				BtcSpectate.Log("facfix lua dump: BrixScriptLoader._fileCache not found");
			}
			else
			{
				StringBuilder stringBuilder = new StringBuilder();
				StringBuilder stringBuilder2 = new StringBuilder();
				int num2 = 0;
				foreach (object obj in dictionary)
				{
					DictionaryEntry dictionaryEntry = (DictionaryEntry)obj;
					string text2 = ((string)dictionaryEntry.Key).Replace('\\', '/');
					string text3 = null;
					try
					{
						FieldInfo field2 = dictionaryEntry.Value.GetType().GetField("code", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						bool flag6 = field2 != null;
						bool flag7 = flag6;
						if (flag7)
						{
							text3 = (field2.GetValue(dictionaryEntry.Value) as string);
						}
					}
					catch (Exception)
					{
					}
					num2++;
					stringBuilder.Append(text2).Append(' ').Append((text3 != null) ? text3.Length : -1).Append("; ");
					string text4 = text2.ToLowerInvariant();
					bool flag8 = text != null && (text2 == text || text2.EndsWith(text) || text.EndsWith(text2));
					bool flag9 = flag8 || text4.EndsWith("/ui.lua") || text4.EndsWith("/config.lua") || text4.EndsWith("/clientside.lua") || text4 == "ui.lua" || text4 == "config.lua" || text4 == "clientside.lua";
					bool flag10 = text3 != null && flag9;
					bool flag11 = flag10;
					if (flag11)
					{
						stringBuilder2.Append("\r\n===== ").Append(text2).Append(" (").Append(text3.Length).Append(" chars) =====\r\n").Append(text3).Append("\r\n");
					}
					bool flag12 = text3 != null && flag8 && num > 0;
					bool flag13 = flag12;
					if (flag13)
					{
						string[] array = text3.Replace("\r\n", "\n").Split(new char[]
						{
							'\n'
						});
						for (int i = Math.Max(1, num - 12); i <= Math.Min(array.Length, num + 4); i++)
						{
							BtcSpectate.Log(string.Concat(new object[]
							{
								"facfix lua src ",
								text2,
								":",
								i,
								(i == num) ? " >>> " : "     ",
								array[i - 1]
							}));
						}
					}
				}
				bool flag14 = !BtcFactionFix._luaDumped;
				bool flag15 = flag14;
				if (flag15)
				{
					BtcSpectate.Log(string.Concat(new object[]
					{
						"facfix lua dump: ",
						num2,
						" cached lua files: ",
						stringBuilder
					}));
				}
				BtcFactionFix._luaDumped = true;
				string text5 = BtcPaths.DebugFile("btc_lua_dump.txt");
				File.WriteAllText(text5, "failing location: " + (decoratedMessage ?? "(none)") + "\r\n" + stringBuilder2.ToString());
				BtcSpectate.Log("facfix lua dump: written to " + text5);
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("facfix lua dump failed: " + ex.Message);
		}
	}

	private static void FixUserFactions()
	{
		try
		{
			FieldInfo field = typeof(User).GetField("___DefaultAlliedFactionGONetId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			List<User> userList = UNetManager.instance.UserList;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			for (int i = 0; i < userList.Count; i++)
			{
				User user = userList[i];
				bool flag = user == null;
				bool flag2 = !flag;
				if (flag2)
				{
					num++;
					bool flag3 = user.faction != null;
					bool flag4 = !flag3;
					if (flag4)
					{
						Faction faction = null;
						bool flag5 = field != null;
						bool flag6 = flag5;
						if (flag6)
						{
							NetworkInstanceId netId = (NetworkInstanceId)field.GetValue(user);
							bool flag7 = !netId.IsEmpty();
							bool flag8 = flag7;
							if (flag8)
							{
								GameObject gameObject = ClientScene.FindLocalObject(netId);
								bool flag9 = gameObject != null;
								bool flag10 = flag9;
								if (flag10)
								{
									faction = gameObject.GetComponent<Faction>();
								}
							}
						}
						bool flag11 = faction != null;
						bool flag12 = flag11;
						if (flag12)
						{
							num2++;
						}
						else
						{
							faction = Faction.Spectator;
							num3++;
						}
						bool flag13 = faction != null;
						bool flag14 = flag13;
						if (flag14)
						{
							user.faction = faction;
						}
						BtcSpectate.Log(string.Concat(new string[]
						{
							"facfix: user '",
							user.name,
							"' had no faction, set to '",
							(faction != null) ? faction.name : "null",
							"' (",
							(num2 > 0 && faction != Faction.Spectator) ? "by net id" : "fallback",
							")"
						}));
					}
				}
			}
			BtcSpectate.Log(string.Concat(new object[]
			{
				"facfix: users ",
				num,
				", faction resolved by net id ",
				num2,
				", set to spectator ",
				num3
			}));
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("facfix FixUserFactions: " + ex.Message);
		}
	}

	private static void CleanStaleLuaUsers()
	{
		try
		{
			LuaStaticDriver staticDriver = LuaDriverHQ.GetStaticDriver("fy_User");
			bool flag = staticDriver == null;
			bool flag2 = flag;
			if (flag2)
			{
				BtcSpectate.Log("facfix: fy_User static driver not found");
			}
			else
			{
				List<LuaDriver> all = staticDriver.GetAll();
				int count = all.Count;
				for (int i = all.Count - 1; i >= 0; i--)
				{
					LuaDriver luaDriver = all[i];
					bool flag3 = luaDriver == null || luaDriver.TargetIsNullOrReleased();
					bool flag4 = flag3;
					if (flag4)
					{
						all.RemoveAt(i);
					}
				}
				BtcSpectate.Log(string.Concat(new object[]
				{
					"facfix: fy_User list ",
					count,
					" -> ",
					all.Count,
					" (stale entries removed)"
				}));
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("facfix CleanStaleLuaUsers: " + ex.Message);
		}
	}

	private static Faction PickPlayerFaction(User local)
	{
		List<User> userList = UNetManager.instance.UserList;
		Faction faction = null;
		for (int i = 0; i < userList.Count; i++)
		{
			User user = userList[i];
			bool flag = user == null || user == local;
			bool flag2 = !flag;
			if (flag2)
			{
				Faction faction2 = user.faction;
				bool flag3 = faction2 == null || faction2.IsSpectator;
				bool flag4 = !flag3;
				if (flag4)
				{
					bool flag5 = faction == null;
					bool flag6 = flag5;
					if (flag6)
					{
						faction = faction2;
					}
					foreach (Faction x in Faction.GetAllFactions())
					{
						bool flag7 = x == faction2;
						bool flag8 = flag7;
						if (flag8)
						{
							return faction2;
						}
					}
				}
			}
		}
		return faction;
	}

	private static readonly float[] Delays = new float[]
	{
		0.3f,
		3.5f,
		13f
	};

	private static int _step;

	private static bool _armed;

	private static float _t0;

	private static bool _init;

	private static bool _selDone;

	private static MethodInfo _miChanged;

	private static FieldInfo _fiOld;

	private static FieldInfo _fiNetId;

	private static FieldInfo _fiIdle;

	private static int iOn = -1;

	private static int iIdle = -1;

	private static int iLog = -1;

	private static int iSel = -1;

	private static FieldInfo _fiSel;

	private static int iSpec = -1;

	private static int iDump = -1;

	private static MethodInfo _miRebuild;

	private static bool _dumpArmed;

	private static bool _dumpDone;

	private static float _dumpAt;

	private static int _dumpN;

	private static int iLua = -1;

	private static bool _luaDone;

	private static bool _luaDumped;
}
}
