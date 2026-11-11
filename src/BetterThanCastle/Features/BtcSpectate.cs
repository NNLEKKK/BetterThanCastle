using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Brix.Assets;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using Brix.Legacy;
using Brix.Network;
using Brix.UI;
using MoonSharp.Interpreter;
using Smooth.Slinq;
using Smooth.Slinq.Context;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BetterThanCastle
{
public static class BtcSpectate
{
	public static void AddRows()
	{
		BtcGameplay.Add("spec.allow", "gen", "Allow spectators into a running game", "Разрешить зрителей в идущей игре", 1f, 0f, 1f, 1f, true, "Spectators connect by direct IP while the game is running. Off = nobody can join after the start.", "Зрители заходят по прямому IP во время игры. Выкл = после старта никто не зайдёт.");
		BtcGameplay.Add("spec.max", "gen", "Max spectators (0 = no limit)", "Макс. зрителей (0 = без лимита)", 0f, 0f, 16f, 1f, false, null, null);
		BtcGameplay.Add("spec.pause", "gen", "Pause the game while a spectator joins", "Пауза игры, пока зритель заходит", 1f, 0f, 1f, 1f, true, "Recommended. The world is frozen from the snapshot until the spectator is in, so nothing changes in between (no desync). Off = the game keeps running and the spectator may see slightly outdated terrain.", "Рекомендуется. Мир заморожен от снимка до входа зрителя, ничего не меняется между ними (нет десинка). Выкл = игра идёт, зритель может видеть чуть устаревший ландшафт.");
		BtcGameplay.Add("spec.ask", "gen", "Ask me before each spectator joins", "Спрашивать разрешение на каждого зрителя", 0f, 0f, 1f, 1f, true, null, null);
		BtcGameplay.Add("spec.ret", "gen", "Let dropped players come back by IP", "Разрешить вернуться выпавшим игрокам по IP", 1f, 0f, 1f, 1f, true, "A player who left or crashed connects again by direct IP, downloads the world and continues on his own faction and color. The game stays hard-paused until he is back (or you press 'Resume without them').", "Игрок, который вылетел или вышел, снова заходит по прямому IP, скачивает мир и продолжает на своей фракции и цвете. Игра на хард-паузе, пока он не вернётся (или пока вы не нажмёте «Продолжить без них»).");
		BtcGameplay.Add("spec.retip", "gen", "Returning player must have the same IP", "Вернувшийся игрок должен быть с того же IP", 1f, 0f, 1f, 1f, true, "The player is recognised by nickname + the IP he had at the start of the game.", "Игрок опознаётся по нику и по IP, с которого он заходил в начале игры.");
		BtcGameplay.Add("spec.hudfix", "gen", "Rebuild the HUD after coming back", "Пересобрать интерфейс после возвращения", 1f, 0f, 1f, 1f, true, "Client side. A few seconds after the world is in (and once more 10 s later) the panels get the units, crystals and projects they missed and all layouts are recalculated. Fixes empty energy / select-all panels and a shifted HUD.", "На стороне клиента. Через несколько секунд после загрузки мира (и ещё раз через 10 с) панели получают пропущенных юнитов, кристаллы и проекты, а все раскладки пересчитываются. Лечит пустые плашки энергии / «выделить всех» и съехавший интерфейс.");
		BtcGameplay.Add("spec.mapfix", "gen", "Wake up the game minimap after coming back", "Включить игровую миникарту после возвращения", 1f, 0f, 1f, 1f, true, "Client side. If the real minimap panel is switched off after the late join, it is switched on again.", "На стороне клиента. Если настоящая панель миникарты после позднего входа выключена, она включается снова.");
		BtcSpectate.iAllow = BtcGameplay.Idx("spec.allow");
		BtcSpectate.iMax = BtcGameplay.Idx("spec.max");
		BtcSpectate.iPause = BtcGameplay.Idx("spec.pause");
		BtcSpectate.iAsk = BtcGameplay.Idx("spec.ask");
		BtcSpectate.iRet = BtcGameplay.Idx("spec.ret");
		BtcSpectate.iRetIp = BtcGameplay.Idx("spec.retip");
		BtcSpectate.iHud = BtcGameplay.Idx("spec.hudfix");
		BtcSpectate.iMap = BtcGameplay.Idx("spec.mapfix");
		BtcFactionFix.AddRows();
	}

	public static void RegisterHandlers(NetworkClient clientOverride)
	{
		try
		{
			BtcSpectate.Log(string.Concat(new string[]
			{
				"RegisterHandlers: server=",
				NetworkServer.active.ToString(),
				" client=",
				(clientOverride != null).ToString(),
				" bc=",
				BtcBc.BcEnabled.ToString()
			}));
			bool active = NetworkServer.active;
			bool flag = active;
			if (flag)
			{
				NetworkServer.UnregisterHandler(78);
				NetworkServer.UnregisterHandler(81);
				NetworkServer.RegisterHandler(78, new NetworkMessageDelegate(BtcSpectate.OnHello));
				NetworkServer.RegisterHandler(81, new NetworkMessageDelegate(BtcSpectate.OnAck));
			}
			UNetManager unetManager = NetworkManager.singleton as UNetManager;
			NetworkClient networkClient = clientOverride ?? ((unetManager != null) ? unetManager.LocalNetworkClient : null);
			bool flag2 = networkClient == null;
			bool flag3 = !flag2;
			if (flag3)
			{
				BtcSpectate._client = networkClient;
				bool flag4 = !NetworkServer.active;
				bool flag5 = flag4;
				if (flag5)
				{
					BtcSpectate.OpenDiag();
				}
				networkClient.UnregisterHandler(79);
				networkClient.UnregisterHandler(80);
				networkClient.UnregisterHandler(82);
				networkClient.UnregisterHandler(83);
				networkClient.RegisterHandler(79, new NetworkMessageDelegate(BtcSpectate.OnReply));
				networkClient.RegisterHandler(80, new NetworkMessageDelegate(BtcSpectate.OnChunk));
				networkClient.RegisterHandler(82, new NetworkMessageDelegate(BtcSpectate.OnBanner));
				networkClient.RegisterHandler(83, new NetworkMessageDelegate(BtcSpectate.OnRel));
				bool flag6 = !NetworkServer.active && BtcBc.BcEnabled && BtcLobby.Active;
				bool flag7 = flag6;
				if (flag7)
				{
					NetworkMessageDelegate networkMessageDelegate;
					bool flag8 = networkClient.handlers.TryGetValue(39, out networkMessageDelegate) && networkMessageDelegate != null && networkMessageDelegate.Method.DeclaringType != typeof(BtcSpectate);
					bool flag9 = flag8;
					if (flag9)
					{
						BtcSpectate._orig = networkMessageDelegate;
					}
					bool flag10 = BtcSpectate._orig != null;
					bool flag11 = flag10;
					if (flag11)
					{
						networkClient.UnregisterHandler(39);
						networkClient.RegisterHandler(39, new NetworkMessageDelegate(BtcSpectate.OnScene));
						BtcSpectate.Log("client: Scene handler intercepted");
					}
					else
					{
						BtcSpectate.Log("client: NO original Scene handler found, spectate join will not work");
					}
				}
			}
		}
		catch (Exception arg)
		{
			BtcSpectate.Log("RegisterHandlers: " + arg);
		}
	}

	public static void OnGUI()
	{
		try
		{
			float unscaledTime = Time.unscaledTime;
			bool flag = BtcSpectate._lastFrame != Time.frameCount;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				BtcSpectate._lastFrame = Time.frameCount;
				BtcSpectate.ServerTick(unscaledTime);
				BtcSpectate.ClientTick(unscaledTime);
				BtcFactionFix.Frame(unscaledTime);
				bool flag4 = unscaledTime < BtcSpectate._watchUntil && unscaledTime >= BtcSpectate._watchNext;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					BtcSpectate._watchNext = unscaledTime + 0.5f;
					BtcSpectate.DumpConns("server: watch");
				}
				bool flag7 = BtcSpectate._bannerOn && unscaledTime - BtcSpectate._bannerSince > 180f;
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					BtcSpectate._bannerOn = false;
				}
			}
			BtcSpectate.Job job = BtcSpectate._job;
			bool flag10 = job != null && job.stage == 0 && !job.askDone;
			bool flag11 = BtcSpectate._bannerOn && BtcSpectate.NetOk();
			bool flag12 = (BtcSpectate._cState >= 2 && BtcSpectate._cState <= 5) || (BtcSpectate._cState == 7 && unscaledTime < BtcSpectate._cMsgUntil);
			bool flag13 = NetworkServer.active && BtcSpectate._waitCount > 0 && BtcSpectate.Val(BtcSpectate.iRet, 1f) > 0.5f && !BtcNetManager.LinkPauseActive; // BCv2: the HUD card shows the same with its own button
			bool flag14 = !flag10 && !flag11 && !flag12 && !flag13;
			bool flag15 = !flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				BtcSpectate.EnsureGui();
				Color color = GUI.color;
				bool flag17 = flag11 && Event.current.type == EventType.Repaint;
				bool flag18 = flag17;
				bool flag19 = flag18;
				if (flag19)
				{
					GUI.color = Color.black;
					GUI.DrawTexture(new Rect(12.5f, 12.5f, 25f, 25f), BtcSpectate._dot);
					GUI.color = new Color(0.62f, 0.62f, 0.66f, 1f);
					GUI.DrawTexture(new Rect(14f, 14f, 22f, 22f), BtcSpectate._dot);
					GUI.color = Color.white;
					GUI.Label(new Rect(42f, 14f, 460f, 22f), ((BtcSpectate._bannerKind == 2) ? BtcGameplay.T("PLAYER RETURNING: ", "ИГРОК ВОЗВРАЩАЕТСЯ: ") : BtcGameplay.T("SPECTATOR JOINING: ", "ПОДКЛЮЧАЕТСЯ ЗРИТЕЛЬ: ")) + BtcSpectate._bannerName + BtcSpectate._bannerExtra, BtcSpectate._st);
				}
				bool flag20 = flag10;
				bool flag21 = flag20;
				bool flag22 = flag21;
				if (flag22)
				{
					Rect position = new Rect((float)Screen.width * 0.5f - 230f, 70f, 460f, 84f);
					GUI.color = new Color(0f, 0f, 0f, 0.8f);
					GUI.DrawTexture(position, BtcSpectate._px);
					GUI.color = Color.white;
					GUI.Label(new Rect(position.x + 12f, position.y + 6f, position.width - 24f, 30f), BtcGameplay.T("Spectator wants to join: ", "Зритель хочет зайти: ") + job.name, BtcSpectate._st);
					bool flag23 = GUI.Button(new Rect(position.x + 12f, position.y + 42f, 200f, 30f), BtcGameplay.T("Allow", "Разрешить"));
					bool flag24 = flag23;
					bool flag25 = flag24;
					if (flag25)
					{
						job.askYes = true;
						job.askDone = true;
					}
					bool flag26 = GUI.Button(new Rect(position.x + 248f, position.y + 42f, 200f, 30f), BtcGameplay.T("Deny", "Отклонить"));
					bool flag27 = flag26;
					bool flag28 = flag27;
					if (flag28)
					{
						job.askYes = false;
						job.askDone = true;
					}
				}
				bool flag29 = flag13;
				bool flag30 = flag29;
				bool flag31 = flag30;
				if (flag31)
				{
					string waitText = BtcSpectate._waitText;
					Rect position2 = new Rect(12f, 80f, 330f, 76f);
					GUI.color = new Color(0f, 0f, 0f, 0.75f);
					GUI.DrawTexture(position2, BtcSpectate._px);
					GUI.color = Color.white;
					GUI.Label(new Rect(position2.x + 8f, position2.y + 4f, position2.width - 16f, 28f), BtcGameplay.T("Waiting for: ", "Ждём: ") + waitText, BtcSpectate._st);
					bool flag32 = GUI.Button(new Rect(position2.x + 8f, position2.y + 36f, position2.width - 16f, 30f), BtcGameplay.T("Resume without them", "Продолжить без них"));
					bool flag33 = flag32;
					bool flag34 = flag33;
					if (flag34)
					{
						BtcSpectate.CancelWaiting();
					}
				}
				bool flag35 = flag12 && Event.current.type == EventType.Repaint;
				bool flag36 = flag35;
				bool flag37 = flag36;
				if (flag37)
				{
					bool flag38 = BtcSpectate._cState == 1;
					bool flag39 = flag38;
					bool flag40 = flag39;
					string text;
					if (flag40)
					{
						text = BtcGameplay.T("Connecting...", "Подключение...");
					}
					else
					{
						bool flag41 = BtcSpectate._cState == 2;
						bool flag42 = flag41;
						bool flag43 = flag42;
						if (flag43)
						{
							text = BtcGameplay.T("Waiting for the host...", "Ждём хоста...");
						}
						else
						{
							bool flag44 = BtcSpectate._cState == 3;
							bool flag45 = flag44;
							bool flag46 = flag45;
							if (flag46)
							{
								text = string.Concat(new object[]
								{
									BtcSpectate._cReturn ? BtcGameplay.T("Reconnecting: downloading the world ", "Возвращение: загрузка мира ") : BtcGameplay.T("Spectator: downloading the world ", "Зритель: загрузка мира "),
									Mathf.RoundToInt(100f * (float)BtcSpectate._cGot / (float)Mathf.Max(1, BtcSpectate._cChunks)),
									"% (",
									((float)(BtcSpectate._cGot * 1200) / 1048576f).ToString("0.0"),
									" MB)"
								});
							}
							else
							{
								bool flag47 = BtcSpectate._cState == 4;
								bool flag48 = flag47;
								bool flag49 = flag48;
								if (flag49)
								{
									text = (BtcSpectate._cReturn ? BtcGameplay.T("Reconnecting: unpacking the world...", "Возвращение: распаковка мира...") : BtcGameplay.T("Spectator: unpacking the world...", "Зритель: распаковка мира..."));
								}
								else
								{
									bool flag50 = BtcSpectate._cState == 5;
									bool flag51 = flag50;
									bool flag52 = flag51;
									if (flag52)
									{
										text = (BtcSpectate._cReturn ? BtcGameplay.T("Reconnecting: loading the game...", "Возвращение: загрузка игры...") : BtcGameplay.T("Spectator: loading the game...", "Зритель: загрузка игры..."));
									}
									else
									{
										text = BtcSpectate._cMsg;
									}
								}
							}
						}
					}
					GUI.color = new Color(0f, 0f, 0f, 0.7f);
					GUI.DrawTexture(new Rect((float)Screen.width * 0.5f - 260f, 14f, 520f, 30f), BtcSpectate._px);
					GUI.color = Color.white;
					GUI.Label(new Rect((float)Screen.width * 0.5f - 248f, 14f, 500f, 30f), text, BtcSpectate._st);
				}
				GUI.color = color;
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("OnGUI: " + ex.Message);
		}
	}

	private static float Val(int i, float d)
	{
		float[] e = BtcGameplay.E;
		return (e != null && i >= 0 && i < e.Length) ? e[i] : d;
	}

	public static void Log(string s)
	{
		try
		{
			bool flag = BtcSpectate._logPath == null;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				BtcPaths.Init();
				BtcSpectate._pid = Process.GetCurrentProcess().Id;
				BtcSpectate._logPath = BtcPaths.DebugFile("btc_spectate.txt");
			}
			string contents = string.Concat(new object[]
			{
				DateTime.Now.ToString("HH:mm:ss.fff"),
				" [",
				BtcSpectate.Role(),
				" pid",
				BtcSpectate._pid,
				"] ",
				s,
				"\r\n"
			});
			for (int i = 0; i < 5; i++)
			{
				try
				{
					File.AppendAllText(BtcSpectate._logPath, contents);
					break;
				}
				catch (IOException)
				{
					Thread.Sleep(5);
				}
			}
		}
		catch (Exception)
		{
		}
	}

	public static BtcSpectate.ConnState PreConnect()
	{
		BtcSpectate.ConnState result;
		try
		{
			bool active = NetworkServer.active;
			bool flag = active;
			if (flag)
			{
				BtcSpectate.OpenDiag();
				BtcSpectate.Log(string.Concat(new object[]
				{
					"server: PreConnect bc=",
					BtcBc.BcEnabled.ToString(),
					" lobbyActive=",
					BtcLobby.Active.ToString(),
					" inGame=",
					BtcSpectate.InGame().ToString(),
					" scene=",
					NetworkManager.networkSceneName,
					" conns=",
					NetworkServer.connections.Count
				}));
			}
			bool flag2 = !NetworkServer.active || !BtcBc.BcEnabled || !BtcLobby.Active || !BtcSpectate.InGame();
			bool flag3 = flag2;
			if (flag3)
			{
				result = null;
			}
			else
			{
				BtcSpectate.StashPending();
				UNetManager instance = UNetManager.instance;
				BtcSpectate.ConnState connState = new BtcSpectate.ConnState();
				connState.mc = instance.maxConnections;
				connState.mp = instance.maxPlayers;
				connState.scene = NetworkManager.networkSceneName;
				instance.maxConnections = 64;
				instance.maxPlayers = 64;
				NetworkManager.networkSceneName = instance.lobbyScene;
				result = connState;
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("PreConnect: " + ex.Message);
			result = null;
		}
		return result;
	}

	public static void PostConnect(BtcSpectate.ConnState s)
	{
		bool flag = s == null;
		bool flag2 = !flag;
		if (flag2)
		{
			try
			{
				BtcSpectate.DumpConns("server: PostConnect");
				BtcSpectate._watchUntil = Time.unscaledTime + 10f;
				BtcSpectate._watchNext = 0f;
				UNetManager instance = UNetManager.instance;
				instance.maxConnections = s.mc;
				instance.maxPlayers = s.mp;
				NetworkManager.networkSceneName = s.scene;
			}
			catch (Exception ex)
			{
				BtcSpectate.Log("PostConnect: " + ex.Message);
			}
		}
	}

	public static void OnServerReady(NetworkConnection conn)
	{
		try
		{
			bool flag = conn == null || (!BtcSpectate._spec.Contains(conn.connectionId) && !BtcSpectate._ret.Contains(conn.connectionId));
			bool flag2 = !flag;
			if (flag2)
			{
				bool flag3 = BtcSpectate._ret.Contains(conn.connectionId);
				BtcSpectate.Job job = BtcSpectate._job;
				BtcSpectate.Log(string.Concat(new object[]
				{
					"server: OnServerReady ",
					flag3 ? "returning player" : "spectator",
					" conn ",
					conn.connectionId,
					" stage ",
					(job != null) ? job.stage : -1,
					" players ",
					conn.playerControllers.Count
				}));
				bool flag4 = job != null && job.conn == conn;
				bool flag5 = flag4;
				if (flag5)
				{
					bool flag6 = job.stage < 5;
					bool flag7 = flag6;
					if (flag7)
					{
						return;
					}
					job.ready = true;
				}
				bool flag8 = conn.playerControllers.Count == 0;
				bool flag9 = flag8;
				if (flag9)
				{
					GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(UNetManager.instance.playerPrefab);
					bool flag10 = flag3 && job != null && job.conn == conn && job.slot != null && job.slot.faction != null;
					bool flag11 = flag10;
					if (flag11)
					{
						User component = gameObject.GetComponent<User>();
						component.factionNumber = job.slot.fnum;
						component.NetworkTeamNumber = job.slot.team;
						component.faction = job.slot.faction;
					}
					NetworkServer.ReplacePlayerForConnection(conn, gameObject, 0);
					NetworkServer.SpawnWithClientAuthority(gameObject, conn);
					BtcSpectate.Log(string.Concat(new object[]
					{
						"server: User created for ",
						flag3 ? "returning player" : "spectator",
						" conn ",
						conn.connectionId,
						" (netId ",
						gameObject.GetComponent<NetworkIdentity>().netId.Value,
						", authority ",
						gameObject.GetComponent<NetworkIdentity>().clientAuthorityOwner,
						")"
					}));
					try
					{
						NetworkServer.SendToClient(conn.connectionId, 54, new UnetUserMessageHandler.AssignUserObjectMessage(gameObject));
					}
					catch (Exception ex)
					{
						BtcSpectate.Log("AssignUserObject send: " + ex.Message);
					}
				}
			}
		}
		catch (Exception arg)
		{
			BtcSpectate.Log("OnServerReady: " + arg);
		}
	}

	public static bool IsSpec(NetworkConnection conn)
	{
		return conn != null && (BtcSpectate._spec.Contains(conn.connectionId) || BtcSpectate._ignore.Contains(conn.connectionId) || (BtcSpectate._ret.Contains(conn.connectionId) && conn.playerControllers.Count == 0));
	}

	/// <summary>True for connections that sent the BTC hello (BetterConnection ON clients), or mod spectators. Used to never send mod messages to vanilla clients.</summary>
	internal static bool SaidHello(NetworkConnection conn)
	{
		return conn != null && (BtcSpectate._hello.Contains(conn.connectionId) || BtcSpectate._spec.Contains(conn.connectionId));
	}

	public static string NameOf(NetworkConnection conn)
	{
		string text = null;
		bool flag = conn != null && BtcSpectate._names.TryGetValue(conn.connectionId, out text);
		bool flag2 = flag;
		string result;
		if (flag2)
		{
			result = text;
		}
		else
		{
			result = null;
		}
		return result;
	}

	public static bool KeepMap(Asset_Map value)
	{
		return BtcSpectate._pendingMap != null && value != BtcSpectate._pendingMap;
	}

	private static bool InGame()
	{
		return NetworkServer.active && SceneManager.GetActiveScene().buildIndex == 2 && !BtcGameplay.InLobby && GameSignals.Received<Action>(GameSignals.LevelReady);
	}

	private static void Reply(NetworkConnection c, int code, int sid, int size, string text)
	{
		try
		{
			BtcSpectate.ReplyMsg replyMsg = new BtcSpectate.ReplyMsg();
			replyMsg.code = code;
			replyMsg.sid = sid;
			replyMsg.size = size;
			replyMsg.text = text;
			NetworkServer.SendToClient(c.connectionId, 79, replyMsg);
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("Reply: " + ex.Message);
		}
	}

	// legacy read UNetManager.pendingReconnectUsers by reflection (the field lives in BtcNetManager since the port, so it was always null)
	private static IList Pending()
	{
		return BtcNetManager.PendingUsers;
	}

	private static void OnHello(NetworkMessage nm)
	{
		try
		{
			BtcSpectate.HelloMsg helloMsg = nm.ReadMessage<BtcSpectate.HelloMsg>();
			NetworkConnection conn = nm.conn;
			string text = BtcSpectate.K(helloMsg.name) ?? "Spectator";
			string text2 = BtcSpectate.Ip(conn);
			string text3 = helloMsg.key ?? string.Empty;
			BtcSpectate._hello.Add(conn.connectionId);
			BtcSpectate.Log(string.Concat(new object[]
			{
				"server: hello from conn ",
				conn.connectionId,
				" '",
				text,
				"' ip ",
				text2,
				" ver ",
				helloMsg.ver,
				" ing ",
				helloMsg.ing,
				" inGame=",
				BtcSpectate.InGame().ToString()
			}));
			bool flag = !BtcBc.BcEnabled || !BtcLobby.Active || helloMsg.ver != 2;
			bool flag2 = flag;
			if (flag2)
			{
				BtcSpectate.Reply(conn, 0, 0, 0, string.Empty);
			}
			else
			{
				bool flag3 = !BtcSpectate.InGame();
				bool flag4 = flag3;
				if (flag4)
				{
					BtcSpectate._known[text] = text2;
					bool flag5 = text3.Length > 0;
					bool flag6 = flag5;
					if (flag6)
					{
						BtcSpectate._keys[text] = text3;
					}
					BtcSpectate.Reply(conn, 0, 0, 0, string.Empty);
				}
				else
				{
					bool flag7 = BtcSpectate.JobOf(conn) != null || BtcSpectate._spec.Contains(conn.connectionId) || BtcSpectate._ret.Contains(conn.connectionId);
					bool flag8 = flag7;
					if (flag8)
					{
						BtcSpectate.Log("server: repeated hello from conn " + conn.connectionId + ", answer repeated");
						BtcSpectate.Reply(conn, 3, 0, 0, BtcSpectate._ret.Contains(conn.connectionId) ? BtcGameplay.T("Reconnecting...", "Возвращаемся в игру...") : BtcGameplay.T("Waiting for the host...", "Ждём хоста..."));
					}
					else
					{
						BtcSpectate.RefreshSlots();
						BtcSpectate.Slot slot = null;
						bool flag9 = BtcSpectate.Val(BtcSpectate.iRet, 1f) > 0.5f;
						bool flag10 = flag9;
						if (flag10)
						{
							BtcSpectate._slots.TryGetValue(text, out slot);
						}
						bool flag11 = slot != null;
						bool flag12 = flag11;
						if (flag12)
						{
							bool flag13 = !BtcSpectate.Identity(text, text2, text3);
							bool flag14 = flag13;
							if (flag14)
							{
								BtcSpectate.Deny(conn, BtcGameplay.T("This player slot belongs to another computer.", "Это место игрока закреплено за другим компьютером."));
							}
							else
							{
								User user = BtcSpectate.UserByName(text, conn);
								NetworkConnection networkConnection = (user != null) ? user.connectionToClient : null;
								bool flag15 = networkConnection != null && BtcSpectate.IsLocal(networkConnection);
								bool flag16 = flag15;
								if (flag16)
								{
									try
									{
										string dbg = "";
										foreach (User du in UNetManager.instance.UserList) if (du != null) dbg += "'" + BtcSpectate.K(du.ScreenName) + "' away=" + BtcSpectate.IsAway(du) + " conn=" + (du.connectionToClient != null ? du.connectionToClient.connectionId.ToString() : "-") + "; ";
										List<User> pl = BtcNetManager.PendingUsers;
										BtcSpectate.Log("server: host-nick deny for '" + text + "', users: " + dbg + " pending=" + (pl != null ? pl.Count : -1));
									}
									catch (Exception) { }
									BtcSpectate.Deny(conn, BtcGameplay.T("This nickname belongs to the host. Change your nickname.", "Этот ник принадлежит хосту. Смените ник."));
								}
								else
								{
									bool flag17 = helloMsg.ing != 0 && user != null && BtcSpectate.IsAway(user);
									bool flag18 = flag17;
									if (flag18)
									{
										BtcSpectate._known[text] = text2;
										bool flag19 = text3.Length > 0;
										bool flag20 = flag19;
										if (flag20)
										{
											BtcSpectate._keys[text] = text3;
										}
										BtcSpectate.ReattachNow(conn, user, "quick return");
										BtcSpectate.Reply(conn, 0, 0, 0, string.Empty);
									}
									else
									{
										bool flag21 = BtcSpectate.SlotBusy(slot);
										bool flag22 = flag21;
										if (flag22)
										{
											BtcSpectate.Deny(conn, BtcGameplay.T("This player is already reconnecting.", "Этот игрок уже возвращается."));
										}
										else
										{
											BtcSpectate._known[text] = text2;
											bool flag23 = text3.Length > 0;
											bool flag24 = flag23;
											if (flag24)
											{
												BtcSpectate._keys[text] = text3;
											}
											BtcSpectate._ret.Add(conn.connectionId);
											BtcSpectate._names[conn.connectionId] = text;
											BtcSpectate.Job job = new BtcSpectate.Job();
											job.conn = conn;
											job.name = text;
											job.sid = ++BtcSpectate._sid;
											job.ret = true;
											job.slot = slot;
											job.oldConn = networkConnection;
											BtcSpectate._queue.Enqueue(job);
											BtcSpectate.Log(string.Concat(new string[]
											{
												"server: '",
												text,
												"' is a returning player (faction '",
												(slot.faction != null) ? slot.faction.name : "?",
												"', old connection ",
												(networkConnection != null) ? networkConnection.connectionId.ToString() : "none",
												BtcSpectate.Stale(networkConnection) ? ", silent" : ", still talking",
												"), queued"
											}));
											BtcSpectate.Reply(conn, 3, 0, 0, BtcGameplay.T("Reconnecting...", "Возвращаемся в игру..."));
										}
									}
								}
							}
						}
						else
						{
							bool flag25 = BtcSpectate.Val(BtcSpectate.iAllow, 1f) < 0.5f;
							bool flag26 = flag25;
							if (flag26)
							{
								BtcSpectate.Deny(conn, BtcGameplay.T("The host does not allow spectators in a running game.", "Хост не разрешает зрителей в идущей игре."));
							}
							else
							{
								int num = Mathf.RoundToInt(BtcSpectate.Val(BtcSpectate.iMax, 0f));
								bool flag27 = num > 0 && BtcSpectate._spec.Count + BtcSpectate._queue.Count + ((BtcSpectate._job != null) ? 1 : 0) >= num;
								bool flag28 = flag27;
								if (flag28)
								{
									BtcSpectate.Deny(conn, BtcGameplay.T("The spectator limit is reached.", "Лимит зрителей достигнут."));
								}
								else
								{
									bool flag29 = !BtcSpectate.EnsureSpectatorFaction();
									bool flag30 = flag29;
									if (flag30)
									{
										BtcSpectate.Deny(conn, BtcGameplay.T("This game mode has no spectator faction.", "В этом режиме нет фракции зрителя."));
									}
									else
									{
										bool flag31 = !BtcSpectate._known.ContainsKey(text);
										bool flag32 = flag31;
										if (flag32)
										{
											BtcSpectate._known[text] = text2;
										}
										BtcSpectate._spec.Add(conn.connectionId);
										BtcSpectate._names[conn.connectionId] = text;
										BtcSpectate.Job job2 = new BtcSpectate.Job();
										job2.conn = conn;
										job2.name = text;
										job2.sid = ++BtcSpectate._sid;
										BtcSpectate._queue.Enqueue(job2);
										BtcSpectate.Reply(conn, 3, 0, 0, BtcGameplay.T("Waiting for the host...", "Ждём хоста..."));
									}
								}
							}
						}
					}
				}
			}
		}
		catch (Exception arg)
		{
			BtcSpectate.Log("OnHello: " + arg);
		}
	}

	private static void OnAck(NetworkMessage nm)
	{
		BtcSpectate.AckMsg ackMsg = nm.ReadMessage<BtcSpectate.AckMsg>();
		BtcSpectate.Job job = BtcSpectate._job;
		bool flag = job != null && job.sid == ackMsg.sid && job.conn == nm.conn && ackMsg.next > job.acked;
		bool flag2 = flag;
		if (flag2)
		{
			job.acked = ackMsg.next;
		}
	}

	private static void Deny(NetworkConnection c, string text)
	{
		BtcSpectate.Log(string.Concat(new object[]
		{
			"server: deny conn ",
			c.connectionId,
			": ",
			text
		}));
		BtcSpectate.Reply(c, 2, 0, 0, text);
		BtcSpectate._kick.Add(new KeyValuePair<int, float>(c.connectionId, Time.unscaledTime + 1f));
	}

	private static bool EnsureSpectatorFaction()
	{
		bool result;
		try
		{
			bool flag = Faction.Spectator != null;
			bool flag2 = flag;
			if (flag2)
			{
				result = true;
			}
			else
			{
				Asset_Gamemode gamemode = GameParam.Gamemode;
				bool flag3 = gamemode == null;
				bool flag4 = flag3;
				if (flag4)
				{
					result = false;
				}
				else
				{
					Asset_Gamemode.FactionPrototype[] array = gamemode.Disk_ReadFactionPrototypes();
					bool flag5 = array == null || array.Length == 0;
					bool flag6 = flag5;
					if (flag6)
					{
						result = false;
					}
					else
					{
						int num = -1;
						int num2 = 0;
						for (int i = 0; i < array.Length; i++)
						{
							bool flag7 = array[i].uid > num2;
							bool flag8 = flag7;
							if (flag8)
							{
								num2 = array[i].uid;
							}
							bool isSpectator = array[i].isSpectator;
							bool flag9 = isSpectator;
							if (flag9)
							{
								num = i;
							}
						}
						Asset_Gamemode.FactionPrototype factionPrototype = (num >= 0) ? array[num] : array[0];
						bool flag10 = num < 0;
						bool flag11 = flag10;
						if (flag11)
						{
							factionPrototype.uid = num2 + 1;
							factionPrototype.name = "Spectator";
							factionPrototype.isSpectator = true;
							factionPrototype.isPlayable = false;
							factionPrototype.isAI = false;
						}
						Faction faction = factionPrototype.CreateFaction();
						factionPrototype.PopulateFaction(faction);
						Faction.Spectator = faction;
						BtcSpectate.Log(string.Concat(new object[]
						{
							"server: spectator faction created on the fly (uid ",
							factionPrototype.uid,
							", from proto ",
							(num >= 0) ? "spectator" : "first",
							")"
						}));
						result = (Faction.Spectator != null);
					}
				}
			}
		}
		catch (Exception arg)
		{
			BtcSpectate.Log("EnsureSpectatorFaction: " + arg);
			result = false;
		}
		return result;
	}

	private static void Freeze(BtcSpectate.Job j)
	{
		BtcSpectate._bannerOn = true;
		BtcSpectate._bannerName = j.name;
		BtcSpectate._bannerSince = Time.unscaledTime;
		BtcSpectate._bannerKind = (j.ret ? 2 : 1);
		BtcSpectate.SendBanner(BtcSpectate._bannerKind, j.name);
		bool flag = BtcSpectate.Val(BtcSpectate.iPause, 1f) > 0.5f && !BtcSpectate._frozen && Time.timeScale > 0f && BtcSpectate.NetOk();
		bool flag2 = flag;
		if (flag2)
		{
			BtcSpectate._savedScale = Time.timeScale;
			Time.timeScale = 0f;
			BtcSpectate._frozen = true;
			BtcSpectate.Log("server: game frozen");
		}
	}

	private static void Unfreeze()
	{
		bool frozen = BtcSpectate._frozen;
		bool flag = frozen;
		if (flag)
		{
			BtcSpectate._frozen = false;
			bool flag2 = Time.timeScale <= 0f && BtcSpectate.NetOk();
			bool flag3 = flag2;
			if (flag3)
			{
				Time.timeScale = ((BtcSpectate._savedScale > 0f) ? BtcSpectate._savedScale : 1f);
			}
			BtcSpectate.Log("server: game unfrozen");
		}
		BtcSpectate._bannerOn = false;
		BtcSpectate.SendBanner(0, string.Empty);
	}

	private static bool NetOk()
	{
		bool result;
		try
		{
			result = (BtcBc.BcStatusText() == "Connection OK");
		}
		catch (Exception)
		{
			result = true;
		}
		return result;
	}

	private static bool Alive(NetworkConnection c)
	{
		bool flag = c == null;
		bool flag2 = flag;
		bool result;
		if (flag2)
		{
			result = false;
		}
		else
		{
			for (int i = 0; i < NetworkServer.connections.Count; i++)
			{
				NetworkConnection networkConnection = NetworkServer.connections[i];
				bool flag3 = networkConnection != null && networkConnection.connectionId == c.connectionId;
				bool flag4 = flag3;
				if (flag4)
				{
					return networkConnection.isConnected;
				}
			}
			result = false;
		}
		return result;
	}

	private static void Abort(BtcSpectate.Job j, string why)
	{
		BtcSpectate.Log(string.Concat(new object[]
		{
			"server: abort job sid ",
			j.sid,
			": ",
			why
		}));
		try
		{
			bool flag = j.shell != null;
			bool flag2 = flag;
			if (flag2)
			{
				j.shell.CancelStaging();
				j.shell = null;
			}
			bool flag3 = !string.IsNullOrEmpty(j.zipPath) && File.Exists(j.zipPath);
			bool flag4 = flag3;
			if (flag4)
			{
				File.Delete(j.zipPath);
			}
		}
		catch (Exception)
		{
		}
		bool flag5 = j.conn != null && BtcSpectate.Alive(j.conn);
		bool flag6 = flag5;
		if (flag6)
		{
			BtcSpectate.Deny(j.conn, BtcGameplay.T("Joining failed: ", "Не удалось зайти: ") + why);
		}
		BtcSpectate._spec.Remove((j.conn != null) ? j.conn.connectionId : -1);
		BtcSpectate._ret.Remove((j.conn != null) ? j.conn.connectionId : -1);
		BtcSpectate.Unfreeze();
		BtcSpectate._job = null;
	}

	private static FieldInfo FindField(Type t, string name)
	{
		while (t != null)
		{
			FieldInfo field = t.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			bool flag = field != null;
			bool flag2 = flag;
			if (flag2)
			{
				return field;
			}
			t = t.BaseType;
		}
		return null;
	}

	private static string BuildSnapshot(BtcSpectate.Job j)
	{
		Asset_Map asset_Map = Asset_Map.CreateShellFrom(GameParam.Map, Asset_Map.MapType.Save);
		FieldInfo fieldInfo = BtcSpectate.FindField(typeof(Asset_Map), "thumbnail");
		bool flag = fieldInfo != null;
		bool flag2 = flag;
		if (flag2)
		{
			fieldInfo.SetValue(asset_Map, null);
		}
		asset_Map.MoveToStaging();
		j.shell = asset_Map;
		MethodInfo method = typeof(Asset_Map).GetMethod("Disk_Save", BindingFlags.Instance | BindingFlags.NonPublic);
		bool flag3 = method == null;
		bool flag4 = flag3;
		if (flag4)
		{
			throw new Exception("Asset_Map.Disk_Save not found");
		}
		method.Invoke(asset_Map, null);
		FieldInfo fieldInfo2 = BtcSpectate.FindField(asset_Map.GetType(), "_stagePath");
		string text = (fieldInfo2 != null) ? ((string)fieldInfo2.GetValue(asset_Map)) : null;
		bool flag5 = string.IsNullOrEmpty(text) || !Directory.Exists(text);
		bool flag6 = flag5;
		if (flag6)
		{
			throw new Exception("staging folder not found");
		}
		BtcSpectate.CopyHeightmap(text);
		return text;
	}

	private static void ServerTick(float now)
	{
		bool flag = !NetworkServer.active;
		bool flag2 = flag;
		if (flag2)
		{
			bool flag3 = BtcSpectate._job != null || BtcSpectate._queue.Count > 0 || BtcSpectate._spec.Count > 0 || BtcSpectate._frozen;
			bool flag4 = flag3;
			if (flag4)
			{
				BtcSpectate._queue.Clear();
				BtcSpectate._spec.Clear();
				BtcSpectate._names.Clear();
				BtcSpectate._job = null;
				BtcSpectate.Unfreeze();
			}
			bool flag5 = BtcSpectate._ret.Count > 0 || BtcSpectate._known.Count > 0 || BtcSpectate._slots.Count > 0 || BtcSpectate._hello.Count > 0 || BtcSpectate._ignore.Count > 0 || BtcSpectate._stash.Count > 0;
			bool flag6 = flag5;
			if (flag6)
			{
				BtcSpectate._ret.Clear();
				BtcSpectate._known.Clear();
				BtcSpectate._keys.Clear();
				BtcSpectate._slots.Clear();
				BtcSpectate._userName.Clear();
				BtcSpectate._ignore.Clear();
				BtcSpectate._stash.Clear();
				BtcSpectate._hello.Clear();
				BtcSpectate._seen.Clear();
				BtcSpectate._waitCount = 0;
				BtcSpectate._waitText = string.Empty;
			}
			BtcSpectate._pauseOrig = null;
		}
		else
		{
			BtcSpectate.GuardPause();
			BtcSpectate.RestorePending();
			BtcSpectate.RelationsTick(now);
			bool flag7 = now >= BtcSpectate._nextCapture;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcSpectate._nextCapture = now + 0.5f;
				BtcSpectate.RefreshSlots();
				BtcSpectate.BuildWaiting();
				BtcSpectate.LegacyReattach(now);
			}
			for (int i = BtcSpectate._kick.Count - 1; i >= 0; i--)
			{
				bool flag9 = now >= BtcSpectate._kick[i].Value;
				bool flag10 = flag9;
				if (flag10)
				{
					NetworkConnection networkConnection = null;
					for (int j = 0; j < NetworkServer.connections.Count; j++)
					{
						bool flag11 = NetworkServer.connections[j] != null && NetworkServer.connections[j].connectionId == BtcSpectate._kick[i].Key;
						bool flag12 = flag11;
						if (flag12)
						{
							networkConnection = NetworkServer.connections[j];
						}
					}
					BtcSpectate._kick.RemoveAt(i);
					BtcSpectate._spec.Remove((networkConnection != null) ? networkConnection.connectionId : -1);
					bool flag13 = networkConnection != null;
					bool flag14 = flag13;
					if (flag14)
					{
						UNetManager.instance.SafeDisconnectConnection(networkConnection);
					}
				}
			}
			bool flag15 = now >= BtcSpectate._nextPrune;
			bool flag16 = flag15;
			if (flag16)
			{
				BtcSpectate._nextPrune = now + 2f;
				List<int> list = null;
				foreach (int num in BtcSpectate._spec)
				{
					bool flag17 = false;
					for (int k = 0; k < NetworkServer.connections.Count; k++)
					{
						bool flag18 = NetworkServer.connections[k] != null && NetworkServer.connections[k].connectionId == num;
						bool flag19 = flag18;
						if (flag19)
						{
							flag17 = true;
							break;
						}
					}
					bool flag20 = !flag17 && (BtcSpectate._job == null || BtcSpectate._job.conn == null || BtcSpectate._job.conn.connectionId != num);
					bool flag21 = flag20;
					if (flag21)
					{
						bool flag22 = list == null;
						bool flag23 = flag22;
						if (flag23)
						{
							list = new List<int>();
						}
						list.Add(num);
					}
				}
				bool flag24 = list != null;
				bool flag25 = flag24;
				if (flag25)
				{
					for (int l = 0; l < list.Count; l++)
					{
						BtcSpectate._spec.Remove(list[l]);
						BtcSpectate._names.Remove(list[l]);
					}
				}
				BtcSpectate.PruneIds(BtcSpectate._ret);
				BtcSpectate.PruneIds(BtcSpectate._hello);
			}
			bool flag26 = BtcSpectate._job == null && BtcSpectate._queue.Count > 0;
			bool flag27 = flag26;
			if (flag27)
			{
				BtcSpectate._job = BtcSpectate._queue.Dequeue();
				BtcSpectate._job.t0 = now;
				BtcSpectate._job.stage = ((!BtcSpectate._job.ret && BtcSpectate.Val(BtcSpectate.iAsk, 0f) > 0.5f) ? 0 : 1);
				bool flag28 = BtcSpectate._job.ret && BtcSpectate._job.oldConn != null && !BtcSpectate.Stale(BtcSpectate._job.oldConn);
				bool flag29 = flag28;
				if (flag29)
				{
					BtcSpectate._job.stage = 7;
				}
				BtcSpectate.Log(string.Concat(new object[]
				{
					"server: job ",
					BtcSpectate._job.sid,
					" start for '",
					BtcSpectate._job.name,
					"'",
					BtcSpectate._job.ret ? " (returning player)" : string.Empty,
					", stage ",
					BtcSpectate._job.stage
				}));
			}
			BtcSpectate.Job job = BtcSpectate._job;
			bool flag30 = job == null;
			bool flag31 = !flag30;
			if (flag31)
			{
				bool flag32 = !BtcSpectate.Alive(job.conn);
				bool flag33 = flag32;
				if (flag33)
				{
					BtcSpectate.Abort(job, "spectator disconnected");
				}
				else
				{
					bool flag34 = job.stage > 0 && now - job.t0 > 150f;
					bool flag35 = flag34;
					if (flag35)
					{
						BtcSpectate.Abort(job, "timeout");
					}
					else
					{
						try
						{
							switch (job.stage)
							{
							case 0:
							{
								bool askDone = job.askDone;
								bool flag36 = askDone;
								if (flag36)
								{
									bool askYes = job.askYes;
									bool flag37 = askYes;
									if (flag37)
									{
										job.stage = 1;
										job.t0 = now;
									}
									else
									{
										BtcSpectate.Abort(job, BtcGameplay.T("the host declined", "хост отклонил"));
									}
								}
								else
								{
									bool flag38 = now - job.t0 > 60f;
									bool flag39 = flag38;
									if (flag39)
									{
										BtcSpectate.Abort(job, BtcGameplay.T("the host did not answer", "хост не ответил"));
									}
								}
								break;
							}
							case 1:
							{
								bool flag40 = job.frames == 0;
								bool flag41 = flag40;
								if (flag41)
								{
									BtcSpectate.Freeze(job);
								}
								BtcSpectate.Job job2 = job;
								int num2 = job2.frames + 1;
								job2.frames = num2;
								bool flag42 = num2 >= 3;
								bool flag43 = flag42;
								if (flag43)
								{
									job.stage = 2;
								}
								break;
							}
							case 2:
							{
								float realtimeSinceStartup = Time.realtimeSinceStartup;
								string text = BtcSpectate.BuildSnapshot(job);
								job.zipPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "btcspec_" + job.sid + ".zip");
								BtcSpectate.Log("server: snapshot written in " + (Time.realtimeSinceStartup - realtimeSinceStartup).ToString("0.00") + " s: " + text);
								BtcSpectate._thread = 1;
								string src = text;
								string dst = job.zipPath;
								new Thread(delegate()
								{
									try
									{
										lzip.compressDir(src, 10, dst, false, null, false);
										BtcSpectate._thread = (File.Exists(dst) ? 2 : 3);
									}
									catch (Exception)
									{
										BtcSpectate._thread = 3;
									}
								})
								{
									IsBackground = true
								}.Start();
								job.stage = 3;
								break;
							}
							case 3:
							{
								bool flag44 = BtcSpectate._thread == 2;
								bool flag45 = flag44;
								if (flag45)
								{
									job.data = File.ReadAllBytes(job.zipPath);
									File.Delete(job.zipPath);
									job.shell.CancelStaging();
									job.shell = null;
									job.chunks = (job.data.Length + 1200 - 1) / 1200;
									BtcSpectate.Log(string.Concat(new object[]
									{
										"server: zip ready, ",
										job.data.Length,
										" bytes, ",
										job.chunks,
										" chunks"
									}));
									BtcSpectate.Reply(job.conn, job.ret ? 5 : 1, job.sid, job.data.Length, string.Empty);
									job.stage = 4;
									job.t0 = now;
								}
								else
								{
									bool flag46 = BtcSpectate._thread == 3;
									bool flag47 = flag46;
									if (flag47)
									{
										BtcSpectate.Abort(job, "compress failed");
									}
								}
								break;
							}
							case 4:
							{
								while (job.sent < job.chunks && job.sent - job.acked < 96)
								{
									int num3 = job.sent * 1200;
									int num4 = Mathf.Min(1200, job.data.Length - num3);
									byte[] array = new byte[num4];
									Buffer.BlockCopy(job.data, num3, array, 0, num4);
									BtcSpectate.ChunkMsg chunkMsg = new BtcSpectate.ChunkMsg();
									chunkMsg.sid = job.sid;
									chunkMsg.idx = job.sent;
									chunkMsg.data = array;
									NetworkServer.SendToClient(job.conn.connectionId, 80, chunkMsg);
									job.sent++;
								}
								BtcSpectate._bannerExtra = " " + Mathf.RoundToInt(100f * (float)job.acked / (float)Mathf.Max(1, job.chunks)) + "%";
								bool flag48 = job.acked >= job.chunks;
								bool flag49 = flag48;
								if (flag49)
								{
									BtcSpectate.Log("server: all chunks acked in " + (now - job.t0).ToString("0.0") + " s");
									job.data = null;
									job.stage = 5;
									job.ready = false;
									NetworkServer.SetClientNotReady(job.conn);
									BtcSpectate.Log("server: spectator conn " + job.conn.connectionId + " set not ready, waiting for Ready after its scene load");
									job.t0 = now;
									BtcSpectate._bannerExtra = string.Empty;
								}
								break;
							}
							case 5:
							{
								User user = job.ready ? UNetManager.instance.GetUserFromConnection(job.conn) : null;
								bool flag50 = user != null;
								bool flag51 = flag50;
								if (flag51)
								{
									bool ret = job.ret;
									bool flag52 = ret;
									if (flag52)
									{
										BtcSpectate.Slot slot = job.slot;
										user.Network_screenName = job.name;
										bool flag53 = slot != null && slot.faction != null;
										bool flag54 = flag53;
										if (flag54)
										{
											slot.faction.AddOwner(user);
											slot.user = user;
										}
										user.NetworkReady = true;
										bool flag55 = slot != null;
										bool flag56 = flag55;
										if (flag56)
										{
											BtcSpectate.DropOld(slot, job.conn);
											BtcSpectate._known[job.name] = BtcSpectate.Ip(job.conn);
										}
										BtcSpectate.FinishReturn(job.name);
										BtcSpectate.Log("server: player '" + job.name + "' is back");
										BtcSpectate.ScheduleRelations(job.conn);
									}
									else
									{
										user.Network_screenName = job.name;
										BtcLobby.MakeSpectator(user);
										user.NetworkReady = true;
										BtcSpectate.Log("server: spectator '" + job.name + "' is in");
										BtcSpectate.ScheduleRelations(job.conn);
									}
									job.stage = 6;
								}
								else
								{
									bool flag57 = now - job.t0 > 90f;
									bool flag58 = flag57;
									if (flag58)
									{
										BtcSpectate.Abort(job, "client did not finish loading");
									}
								}
								break;
							}
							case 6:
								BtcSpectate.Unfreeze();
								BtcSpectate._job = null;
								break;
							case 7:
							{
								bool flag59 = job.oldConn == null || BtcSpectate.Stale(job.oldConn);
								bool flag60 = flag59;
								if (flag60)
								{
									BtcSpectate.Log("server: old connection of '" + job.name + "' is silent now, going on");
									job.stage = 1;
									job.t0 = now;
								}
								else
								{
									bool flag61 = now - job.t0 > 20f;
									bool flag62 = flag61;
									if (flag62)
									{
										BtcSpectate.Abort(job, BtcGameplay.T("this player is still connected from another client", "этот игрок ещё подключён с другого клиента"));
									}
								}
								break;
							}
							}
						}
						catch (Exception ex)
						{
							BtcSpectate.Log(string.Concat(new object[]
							{
								"ServerTick stage ",
								job.stage,
								": ",
								ex
							}));
							BtcSpectate.Abort(job, ex.Message);
						}
					}
				}
			}
		}
	}

	private static void OnScene(NetworkMessage nm)
	{
		try
		{
			BtcSpectate._heldScene = nm.ReadMessage<BtcSpectate.SceneStrMsg>().value;
			string text = (!string.IsNullOrEmpty(Neo.multiplayerName)) ? Neo.multiplayerName : "Spectator";
			BtcSpectate.HelloMsg helloMsg = new BtcSpectate.HelloMsg();
			helloMsg.ver = 2;
			helloMsg.name = text;
			helloMsg.ing = ((SceneManager.GetActiveScene().buildIndex == 2) ? 1 : 0);
			helloMsg.key = BtcSpectate.MyKey();
			BtcSpectate._helloMsg = helloMsg;
			BtcSpectate._cState = 1;
			BtcSpectate._cT0 = Time.unscaledTime;
			BtcSpectate._helloNext = BtcSpectate._cT0 + 1.5f;
			BtcSpectate._helloTries = 1;
			BtcSpectate._cMsg = string.Empty;
			BtcSpectate.StartPreHold();
			BtcSpectate._client.Send(78, helloMsg);
			BtcSpectate.Log(string.Concat(new string[]
			{
				"client: Scene '",
				BtcSpectate._heldScene,
				"' held, hello sent as '",
				text,
				"'"
			}));
		}
		catch (Exception arg)
		{
			BtcSpectate.Log("OnScene: " + arg);
			BtcSpectate.Passthrough("scene error");
		}
	}

	private static void Passthrough(string why)
	{
		BtcSpectate.Log("client: passthrough (" + why + ")");
		BtcSpectate._cState = 0;
		bool flag = why == "snapshot ready";
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcSpectate.StopPreHold(false);
			BtcSpectate.ResetClientReady();
			BtcSpectate.StartHold();
		}
		bool flag4 = BtcSpectate._heldScene == null || BtcSpectate._orig == null;
		bool flag5 = flag4;
		if (flag5)
		{
			bool flag6 = !flag;
			bool flag7 = flag6;
			if (flag7)
			{
				BtcSpectate.StopPreHold(true);
			}
		}
		else
		{
			try
			{
				NetworkWriter networkWriter = new NetworkWriter();
				new BtcSpectate.SceneStrMsg(BtcSpectate._heldScene).Serialize(networkWriter);
				NetworkMessage networkMessage = new NetworkMessage();
				networkMessage.msgType = 39;
				networkMessage.conn = BtcSpectate._client.connection;
				networkMessage.reader = new NetworkReader(networkWriter.ToArray());
				networkMessage.channelId = 0;
				BtcSpectate._heldScene = null;
				BtcSpectate._orig(networkMessage);
			}
			catch (Exception arg)
			{
				BtcSpectate.Log("Passthrough: " + arg);
			}
			bool flag8 = !flag;
			bool flag9 = flag8;
			if (flag9)
			{
				BtcSpectate.StopPreHold(true);
			}
		}
	}

	private static void Fail(string text)
	{
		BtcSpectate.Log("client: fail: " + text);
		BtcSpectate._cState = 7;
		BtcSpectate.StopPreHold(false);
		BtcSpectate.StopHold(false, "fail");
		BtcSpectate._cMsg = text;
		BtcSpectate._cMsgUntil = Time.unscaledTime + 8f;
		BtcSpectate.CloseFile();
		BtcSpectate._heldScene = null;
		try
		{
			UNetManager.instance.Disconnect();
		}
		catch (Exception)
		{
		}
	}

	private static void CloseFile()
	{
		try
		{
			bool flag = BtcSpectate._fs != null;
			bool flag2 = flag;
			if (flag2)
			{
				BtcSpectate._fs.Close();
				BtcSpectate._fs = null;
			}
		}
		catch (Exception)
		{
		}
	}

	private static void OnReply(NetworkMessage nm)
	{
		try
		{
			BtcSpectate.ReplyMsg replyMsg = nm.ReadMessage<BtcSpectate.ReplyMsg>();
			BtcSpectate.Log(string.Concat(new object[]
			{
				"client: reply code ",
				replyMsg.code,
				" sid ",
				replyMsg.sid,
				" size ",
				replyMsg.size,
				" '",
				replyMsg.text,
				"'"
			}));
			bool flag = BtcSpectate._cState == 1 || BtcSpectate._cState == 2;
			bool flag2 = replyMsg.code == 0;
			bool flag3 = flag2;
			if (flag3)
			{
				bool flag4 = flag;
				bool flag5 = flag4;
				if (flag5)
				{
					BtcSpectate.Passthrough("host says normal join");
				}
			}
			else
			{
				bool flag6 = !flag && replyMsg.code != 2;
				bool flag7 = flag6;
				if (flag7)
				{
					bool flag8 = replyMsg.code == 1 || replyMsg.code == 5;
					bool flag9 = flag8;
					if (flag9)
					{
						BtcSpectate.Fail(BtcGameplay.T("The host answered too late. Please connect again.", "Хост ответил слишком поздно. Зайдите ещё раз."));
					}
				}
				else
				{
					bool flag10 = replyMsg.code == 3;
					bool flag11 = flag10;
					if (flag11)
					{
						BtcSpectate.DiscardPre();
						BtcSpectate._cState = 2;
						BtcSpectate._cT0 = Time.unscaledTime;
						BtcSpectate._cMsg = replyMsg.text;
						BtcSpectate._cMsgUntil = float.MaxValue;
					}
					else
					{
						bool flag12 = replyMsg.code == 2;
						bool flag13 = flag12;
						if (flag13)
						{
							BtcSpectate.Fail(replyMsg.text);
						}
						else
						{
							bool flag14 = replyMsg.code == 1 || replyMsg.code == 5;
							bool flag15 = flag14;
							if (flag15)
							{
								BtcSpectate.DiscardPre();
								BtcSpectate._cReturn = (replyMsg.code == 5);
								BtcSpectate._cSpectator = (replyMsg.code == 1);
								BtcSpectate._cSid = replyMsg.sid;
								BtcSpectate._cTotal = replyMsg.size;
								BtcSpectate._cGot = 0;
								BtcSpectate._cChunks = (replyMsg.size + 1200 - 1) / 1200;
								string text = "Info/Downloads/Maps/";
								Directory.CreateDirectory(text);
								BtcSpectate._cZip = string.Concat(new object[]
								{
									text,
									"btcspec_",
									replyMsg.sid,
									".zip"
								});
								BtcSpectate._cDir = string.Concat(new object[]
								{
									text,
									"btcspec_",
									replyMsg.sid,
									"/"
								});
								BtcSpectate._fs = new FileStream(BtcSpectate._cZip, FileMode.Create, FileAccess.Write, FileShare.Read);
								BtcSpectate._cState = 3;
								BtcSpectate._cT0 = Time.unscaledTime;
								BtcSpectate._cMsg = string.Empty;
							}
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("OnReply: " + ex);
			BtcSpectate.Fail(ex.Message);
		}
	}

	private static void OnChunk(NetworkMessage nm)
	{
		try
		{
			BtcSpectate.ChunkMsg chunkMsg = nm.ReadMessage<BtcSpectate.ChunkMsg>();
			bool flag = BtcSpectate._cState != 3 || chunkMsg.sid != BtcSpectate._cSid || BtcSpectate._fs == null;
			bool flag2 = !flag;
			if (flag2)
			{
				BtcSpectate._fs.Write(chunkMsg.data, 0, chunkMsg.data.Length);
				BtcSpectate._cGot++;
				bool flag3 = BtcSpectate._cGot % 16 == 0 || BtcSpectate._cGot >= BtcSpectate._cChunks;
				bool flag4 = flag3;
				if (flag4)
				{
					BtcSpectate.AckMsg ackMsg = new BtcSpectate.AckMsg();
					ackMsg.sid = BtcSpectate._cSid;
					ackMsg.next = BtcSpectate._cGot;
					BtcSpectate._client.Send(81, ackMsg);
				}
				bool flag5 = BtcSpectate._cGot >= BtcSpectate._cChunks;
				bool flag6 = flag5;
				if (flag6)
				{
					BtcSpectate.CloseFile();
					BtcSpectate.Log("client: download complete, unzipping");
					BtcSpectate._cState = 4;
					BtcSpectate._thread = 1;
					string z = BtcSpectate._cZip;
					string d = BtcSpectate._cDir;
					new Thread(delegate()
					{
						try
						{
							Directory.CreateDirectory(d);
							int[] progress = new int[]
							{
								10
							};
							lzip.decompress_File(z, d, progress, null, null, null);
							File.Delete(z);
							BtcSpectate._thread = 2;
						}
						catch (Exception)
						{
							BtcSpectate._thread = 3;
						}
					})
					{
						IsBackground = true
					}.Start();
				}
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("OnChunk: " + ex);
			BtcSpectate.Fail(ex.Message);
		}
	}

	private static void OnBanner(NetworkMessage nm)
	{
		BtcSpectate.BannerMsg bannerMsg = nm.ReadMessage<BtcSpectate.BannerMsg>();
		BtcSpectate._bannerOn = (bannerMsg.on != 0);
		BtcSpectate._bannerKind = bannerMsg.on;
		BtcSpectate._bannerName = bannerMsg.name;
		BtcSpectate._bannerSince = Time.unscaledTime;
		BtcSpectate._bannerExtra = string.Empty;
	}

	private static Asset_Map FindSnapshot()
	{
		string parameter = "btcspec_" + BtcSpectate._cSid;
		AssetQueryParameters qparams = AssetQueryParameters.Official | AssetQueryParameters.Multiplayer | AssetQueryParameters.UGC | AssetQueryParameters.Workshop | AssetQueryParameters.RefreshCache;
		Asset_Map asset_Map = AssetDispensary.saves.SendQuery(qparams, AssetPlayableContext.Any, false).Slinq<Asset_Map>().Where((Asset_Map s, string k) => s.path != null && s.path.Replace('\\', '/').Contains(k), parameter).FirstOrDefault();
		bool flag = asset_Map == null;
		bool flag2 = flag;
		if (flag2)
		{
			asset_Map = AssetDispensary.maps.SendQuery(qparams, AssetPlayableContext.Any, false).Slinq<Asset_Map>().Where((Asset_Map s, string k) => s.path != null && s.path.Replace('\\', '/').Contains(k), parameter).FirstOrDefault();
		}
		return asset_Map;
	}

	private static void ClientReset()
	{
		BtcFactionFix.Reset();
		BtcSpectate._cState = 0;
		BtcSpectate.StopPreHold(false);
		BtcSpectate.StopHold(false, "reset");
		BtcSpectate._hudAt = 0f;
		BtcSpectate._hudPass = 0;
		BtcSpectate._relA = new uint[0];
		BtcSpectate._relB = new uint[0];
		BtcSpectate._relR = new byte[0];
		BtcSpectate._pendingMap = null;
		BtcSpectate._mapApplied = false;
		BtcSpectate._cReturn = false;
		BtcSpectate._refireAt = 0f;
		BtcSpectate._probed2 = false;
		BtcSpectate._cSpectator = false;
		BtcSpectate._probeAt = 0f;
		BtcSpectate._heldScene = null;
		BtcSpectate.CloseFile();
		try
		{
			bool flag = !string.IsNullOrEmpty(BtcSpectate._cDir) && Directory.Exists(BtcSpectate._cDir);
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				Directory.Delete(BtcSpectate._cDir, true);
			}
		}
		catch (Exception)
		{
		}
		BtcSpectate._cDir = null;
	}

	private static void ClientTick(float now)
	{
		switch (BtcSpectate._cState)
		{
		case 1:
		{
			bool flag = now - BtcSpectate._cT0 > 8f;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				BtcSpectate.Passthrough("no reply from host (vanilla host?)");
			}
			else
			{
				bool flag4 = now >= BtcSpectate._helloNext && BtcSpectate._helloMsg != null && BtcSpectate._client != null;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					BtcSpectate._helloNext = now + 1.5f;
					BtcSpectate._helloTries++;
					BtcSpectate._client.Send(78, BtcSpectate._helloMsg);
					BtcSpectate.Log("client: no answer yet, hello sent again (" + BtcSpectate._helloTries + ")");
				}
			}
			break;
		}
		case 2:
		{
			bool flag7 = now - BtcSpectate._cT0 > 200f;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				BtcSpectate.Fail(BtcGameplay.T("The host did not let you in in time.", "Хост не пустил вовремя."));
			}
			break;
		}
		case 3:
		{
			bool flag10 = now - BtcSpectate._cT0 > 200f;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				BtcSpectate.Fail(BtcGameplay.T("Download timed out.", "Таймаут загрузки."));
			}
			break;
		}
		case 4:
		{
			bool flag13 = BtcSpectate._thread == 2;
			bool flag14 = flag13;
			bool flag15 = flag14;
			if (flag15)
			{
				Asset_Map asset_Map = BtcSpectate.FindSnapshot();
				bool flag16 = asset_Map == null;
				bool flag17 = flag16;
				bool flag18 = flag17;
				if (flag18)
				{
					BtcSpectate.Fail(BtcGameplay.T("The downloaded world was not found.", "Скачанный мир не найден."));
				}
				else
				{
					BtcSpectate._pendingMap = asset_Map;
					BtcSpectate._mapApplied = false;
					BtcSpectate._cState = 5;
					BtcSpectate._cT0 = now;
					BtcSpectate.Log(string.Concat(new string[]
					{
						"client: snapshot found at ",
						asset_Map.path,
						", Heightmap.png ",
						File.Exists(System.IO.Path.Combine(asset_Map.path, "Heightmap.png")) ? "present" : "MISSING",
						", releasing the Scene message"
					}));
					BtcSpectate.Passthrough("snapshot ready");
					BtcSpectate._cState = 5;
				}
			}
			else
			{
				bool flag19 = BtcSpectate._thread == 3;
				bool flag20 = flag19;
				bool flag21 = flag20;
				if (flag21)
				{
					BtcSpectate.Fail(BtcGameplay.T("Unpacking the world failed.", "Не удалось распаковать мир."));
				}
			}
			break;
		}
		case 5:
		case 6:
		{
			BtcSpectate.HoldTick(now);
			bool flag22 = !BtcSpectate._mapApplied && BtcSpectate._pendingMap != null && GameParam.instance != null;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				GameParam.Map = BtcSpectate._pendingMap;
				BtcSpectate._mapApplied = true;
				BtcSpectate.Log("client: GameParam.Map set to the snapshot after " + (now - BtcSpectate._cT0).ToString("0.0") + " s");
			}
			bool flag25 = BtcSpectate._cState == 5 && SceneManager.GetActiveScene().buildIndex == 2;
			bool flag26 = flag25;
			bool flag27 = flag26;
			if (flag27)
			{
				BtcSpectate._cState = 6;
			}
			bool flag28 = BtcSpectate._cState == 6 && !BtcSpectate._hold;
			bool flag29 = flag28;
			bool flag30 = flag29;
			if (flag30)
			{
				BtcFactionFix.Tick(now);
				bool flag31 = now >= BtcSpectate._fixNext;
				bool flag32 = flag31;
				bool flag33 = flag32;
				if (flag33)
				{
					BtcSpectate._fixNext = now + 2f;
					BtcSpectate.FixLocalFaction();
				}
				bool flag34 = BtcSpectate._refireAt > 0f && now >= BtcSpectate._refireAt;
				bool flag35 = flag34;
				bool flag36 = flag35;
				if (flag36)
				{
					BtcSpectate._refireAt = 0f;
					BtcSpectate.RefireProjects();
				}
				bool flag37 = BtcSpectate._hudAt > 0f && now >= BtcSpectate._hudAt;
				bool flag38 = flag37;
				bool flag39 = flag38;
				if (flag39)
				{
					BtcSpectate._hudAt = ((BtcSpectate._hudPass == 0) ? (now + 10f) : 0f);
					BtcSpectate.HudFix(BtcSpectate._hudPass++);
				}
				bool flag40 = BtcSpectate._probeAt > 0f && now >= BtcSpectate._probeAt;
				bool flag41 = flag40;
				bool flag42 = flag41;
				if (flag42)
				{
					BtcSpectate._probeAt = 0f;
					BtcSpectate.Probe();
					bool flag43 = !BtcSpectate._probed2;
					bool flag44 = flag43;
					bool flag45 = flag44;
					if (flag45)
					{
						BtcSpectate._probed2 = true;
						BtcSpectate._probeAt = now + 25f;
					}
				}
			}
			bool flag46 = BtcSpectate._cState == 6;
			bool flag47 = flag46;
			bool flag48 = flag47;
			if (flag48)
			{
				UNetManager instance = UNetManager.instance;
				bool flag49 = instance == null || instance.LocalNetworkClient == null || !instance.LocalNetworkClient.isConnected;
				bool flag50 = flag49;
				bool flag51 = flag50;
				if (flag51)
				{
					BtcSpectate.Log("client: left the game");
					BtcSpectate.ClientReset();
				}
			}
			else
			{
				bool flag52 = now - BtcSpectate._cT0 > 120f;
				bool flag53 = flag52;
				bool flag54 = flag53;
				if (flag54)
				{
					BtcSpectate.Log("client: load timeout");
					BtcSpectate.ClientReset();
				}
			}
			break;
		}
		case 7:
		{
			bool flag55 = now > BtcSpectate._cMsgUntil;
			bool flag56 = flag55;
			bool flag57 = flag56;
			if (flag57)
			{
				BtcSpectate.ClientReset();
			}
			break;
		}
		}
	}

	private static void EnsureGui()
	{
		bool flag = BtcSpectate._st != null;
		bool flag2 = !flag;
		if (flag2)
		{
			BtcSpectate._st = new GUIStyle(GUI.skin.label);
			BtcSpectate._st.fontSize = 14;
			BtcSpectate._st.fontStyle = FontStyle.Bold;
			BtcSpectate._st.alignment = TextAnchor.MiddleLeft;
			BtcSpectate._st.normal.textColor = Color.white;
			BtcSpectate._px = new Texture2D(1, 1);
			BtcSpectate._px.SetPixel(0, 0, Color.white);
			BtcSpectate._px.Apply();
			BtcSpectate._dot = new Texture2D(32, 32, TextureFormat.ARGB32, false);
			for (int i = 0; i < 32; i++)
			{
				for (int j = 0; j < 32; j++)
				{
					float num = Mathf.Sqrt((float)((j - 15) * (j - 15) + (i - 15) * (i - 15)));
					BtcSpectate._dot.SetPixel(j, i, new Color(1f, 1f, 1f, Mathf.Clamp01(16f - num)));
				}
			}
			BtcSpectate._dot.Apply();
		}
	}

	static BtcSpectate()
	{
		BtcSpectate._queue = new Queue<BtcSpectate.Job>();
		BtcSpectate._spec = new HashSet<int>();
		BtcSpectate._names = new Dictionary<int, string>();
		BtcSpectate._ret = new HashSet<int>();
		BtcSpectate._known = new Dictionary<string, string>();
		BtcSpectate._keys = new Dictionary<string, string>();
		BtcSpectate._slots = new Dictionary<string, BtcSpectate.Slot>();
		BtcSpectate._userName = new Dictionary<int, string>();
		BtcSpectate._ignore = new HashSet<int>();
		BtcSpectate._stash = new List<object>();
		BtcSpectate._waitText = string.Empty;
		BtcSpectate._hello = new HashSet<int>();
		BtcSpectate._seen = new Dictionary<int, float>();
		BtcSpectate._kick = new List<KeyValuePair<int, float>>();
		BtcSpectate._lastFrame = -1;
		BtcSpectate._savedScale = 1f;
		BtcSpectate._bannerName = string.Empty;
		BtcSpectate._bannerExtra = string.Empty;
		BtcSpectate._bannerKind = 1;
		BtcSpectate._relA = new uint[0];
		BtcSpectate._relB = new uint[0];
		BtcSpectate._relR = new byte[0];
		BtcSpectate._cMsg = string.Empty;
		BtcSpectate._preQ = new List<BtcSpectate.HeldMsg>();
		BtcSpectate.PreTypes = new short[]
		{
			3
		};
		BtcSpectate._held = new List<BtcSpectate.HeldMsg>();
		BtcSpectate._heldIds = new HashSet<uint>();
		BtcSpectate._hOrig = new Dictionary<short, NetworkMessageDelegate>();
		BtcSpectate.HoldTypes = new short[]
		{
			1,
			2,
			3,
			4,
			7,
			8,
			9,
			13,
			15,
			16,
			40,
			41,
			42
		};
		BtcSpectate._held2 = new List<BtcSpectate.HeldMsg>();
		BtcSpectate._heldIds2 = new HashSet<uint>();
		BtcSpectate._relAt = new List<KeyValuePair<float, int>>();
	}

	private static string Role()
	{
		bool active = NetworkServer.active;
		bool flag = active;
		string result;
		if (flag)
		{
			result = "HOST";
		}
		else
		{
			bool active2 = NetworkClient.active;
			bool flag2 = active2;
			if (flag2)
			{
				result = "CLIENT";
			}
			else
			{
				result = "----";
			}
		}
		return result;
	}

	private static void OpenDiag()
	{
		BtcSpectate._diagUntil = DateTime.UtcNow.AddSeconds(60.0).Ticks;
		BtcSpectate._noTarget = 0;
		bool flag = !BtcSpectate._logHook;
		bool flag2 = flag;
		if (flag2)
		{
			BtcSpectate._logHook = true;
			Application.logMessageReceived += BtcSpectate.OnUnityLog;
		}
	}

	private static void OnUnityLog(string cond, string stack, LogType t)
	{
		try
		{
			bool flag = cond == null || DateTime.UtcNow.Ticks > BtcSpectate._diagUntil;
			bool flag2 = !flag;
			if (flag2)
			{
				bool flag3 = cond.IndexOf("referenced script", StringComparison.Ordinal) >= 0 || cond.IndexOf("RearViewMirror", StringComparison.Ordinal) >= 0 || cond.IndexOf("GA Error", StringComparison.Ordinal) >= 0 || cond.IndexOf("CRC ", StringComparison.Ordinal) >= 0;
				bool flag4 = !flag3;
				if (flag4)
				{
					bool flag5 = cond.IndexOf("Did not find target for sync message", StringComparison.Ordinal) >= 0 && ++BtcSpectate._noTarget > 8;
					bool flag6 = !flag5;
					if (flag6)
					{
						bool flag7 = t == LogType.Log && cond.IndexOf("onnect", StringComparison.Ordinal) < 0 && cond.IndexOf("Network", StringComparison.Ordinal) < 0 && cond.IndexOf("Scene", StringComparison.Ordinal) < 0;
						bool flag8 = !flag7;
						if (flag8)
						{
							string text = (cond.Length > 400) ? cond.Substring(0, 400) : cond;
							bool flag9 = (t == LogType.Exception || t == LogType.Error) && !string.IsNullOrEmpty(stack);
							bool flag10 = flag9;
							if (flag10)
							{
								string[] array = stack.Split(new char[]
								{
									'\n'
								});
								int num = Math.Min(4, array.Length);
								for (int i = 0; i < num; i++)
								{
									text = text + " | " + array[i].Trim();
								}
							}
							BtcSpectate.Log(string.Concat(new object[]
							{
								"unity ",
								t,
								": ",
								text
							}));
						}
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void DumpConns(string tag)
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder(tag);
			foreach (NetworkConnection networkConnection in NetworkServer.connections)
			{
				bool flag = networkConnection != null;
				bool flag2 = flag;
				if (flag2)
				{
					stringBuilder.Append(" [id=").Append(networkConnection.connectionId).Append(" connected=").Append(networkConnection.isConnected).Append(" ready=").Append(networkConnection.isReady).Append(" players=").Append(networkConnection.playerControllers.Count).Append("]");
				}
			}
			BtcSpectate.Log(stringBuilder.ToString());
		}
		catch (Exception)
		{
		}
	}

	private static void ResetClientReady()
	{
		try
		{
			BindingFlags bindingAttr = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			MethodInfo method = typeof(ClientScene).GetMethod("SetNotReady", bindingAttr);
			bool flag = method != null;
			bool flag2 = flag;
			if (flag2)
			{
				method.Invoke(null, null);
				BtcSpectate.Log("client: ClientScene.SetNotReady called");
			}
			else
			{
				FieldInfo field = typeof(ClientScene).GetField("s_IsReady", bindingAttr);
				bool flag3 = field != null;
				bool flag4 = flag3;
				if (flag4)
				{
					field.SetValue(null, false);
					BtcSpectate.Log("client: ClientScene.s_IsReady reset");
				}
				else
				{
					BtcSpectate.Log("client: no way found to reset the Ready flag");
				}
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("ResetClientReady: " + ex.Message);
		}
	}

	private static void StartHold()
	{
		try
		{
			bool flag = BtcSpectate._client == null;
			bool flag2 = !flag;
			if (flag2)
			{
				BtcSpectate._held.Clear();
				BtcSpectate._heldIds.Clear();
				BtcSpectate._held2.Clear();
				BtcSpectate._heldIds2.Clear();
				BtcSpectate._tier2Open = false;
				BtcSpectate._hOrig.Clear();
				for (int i = 0; i < BtcSpectate.HoldTypes.Length; i++)
				{
					short key = BtcSpectate.HoldTypes[i];
					NetworkMessageDelegate networkMessageDelegate;
					bool flag3 = BtcSpectate._client.handlers.TryGetValue(key, out networkMessageDelegate) && networkMessageDelegate != null && networkMessageDelegate.Method.DeclaringType != typeof(BtcSpectate);
					bool flag4 = flag3;
					if (flag4)
					{
						BtcSpectate._hOrig[key] = networkMessageDelegate;
						BtcSpectate._client.handlers[key] = new NetworkMessageDelegate(BtcSpectate.OnHeld);
					}
				}
				BtcSpectate._hold = true;
				BtcSpectate._holdSince = Time.unscaledTime;
				BtcSpectate._diagNext = 0f;
				BtcSpectate.Log("client: holding world object messages, wrapped " + BtcSpectate._hOrig.Count + " handlers");
			}
		}
		catch (Exception arg)
		{
			BtcSpectate.Log("StartHold: " + arg);
		}
	}

	private static void OnHeld(NetworkMessage nm)
	{
		NetworkMessageDelegate networkMessageDelegate;
		bool flag = !BtcSpectate._hOrig.TryGetValue(nm.msgType, out networkMessageDelegate) || networkMessageDelegate == null;
		bool flag2 = !flag;
		if (flag2)
		{
			bool flag3 = false;
			try
			{
				bool hold = BtcSpectate._hold;
				bool flag4 = hold;
				if (flag4)
				{
					nm.reader.SeekZero();
					int num = 0;
					bool flag5 = nm.msgType == 3;
					bool flag6 = flag5;
					if (flag6)
					{
						NetworkInstanceId networkInstanceId = nm.reader.ReadNetworkId();
						NetworkHash128 a = nm.reader.ReadNetworkHash128();
						nm.reader.SeekZero();
						num = BtcSpectate.TierOf(a);
						bool flag7 = num == 2 && !BtcSpectate._tier2Open;
						bool flag8 = flag7;
						if (flag8)
						{
							BtcSpectate._heldIds2.Add(networkInstanceId.Value);
						}
						else
						{
							bool flag9 = num == 2;
							bool flag10 = flag9;
							if (flag10)
							{
								num = 0;
							}
							else
							{
								bool flag11 = num == 3;
								bool flag12 = flag11;
								if (flag12)
								{
									BtcSpectate._heldIds.Add(networkInstanceId.Value);
								}
							}
						}
					}
					else
					{
						NetworkInstanceId networkInstanceId2 = nm.reader.ReadNetworkId();
						nm.reader.SeekZero();
						bool flag13 = BtcSpectate._heldIds.Contains(networkInstanceId2.Value);
						bool flag14 = flag13;
						if (flag14)
						{
							num = 3;
						}
						else
						{
							bool flag15 = !BtcSpectate._tier2Open && BtcSpectate._heldIds2.Contains(networkInstanceId2.Value);
							bool flag16 = flag15;
							if (flag16)
							{
								num = 2;
							}
						}
					}
					bool flag17 = num != 0;
					bool flag18 = flag17;
					if (flag18)
					{
						flag3 = true;
						BtcSpectate.HeldMsg heldMsg = new BtcSpectate.HeldMsg();
						heldMsg.t = nm.msgType;
						heldMsg.ch = nm.channelId;
						heldMsg.raw = nm.reader.ReadBytes(nm.reader.Length);
						bool flag19 = num == 2;
						bool flag20 = flag19;
						if (flag20)
						{
							BtcSpectate._held2.Add(heldMsg);
						}
						else
						{
							BtcSpectate._held.Add(heldMsg);
						}
					}
					else
					{
						nm.reader.SeekZero();
					}
				}
			}
			catch (Exception ex)
			{
				BtcSpectate.Log(string.Concat(new object[]
				{
					"OnHeld type ",
					nm.msgType,
					": ",
					ex.Message
				}));
				flag3 = false;
				try
				{
					nm.reader.SeekZero();
				}
				catch (Exception)
				{
				}
			}
			bool flag21 = !flag3;
			bool flag22 = flag21;
			if (flag22)
			{
				networkMessageDelegate(nm);
			}
		}
	}

	private static void StopHold(bool replay, string why)
	{
		bool flag = !BtcSpectate._hold && BtcSpectate._held.Count == 0 && BtcSpectate._held2.Count == 0 && BtcSpectate._hOrig.Count == 0;
		bool flag2 = !flag;
		if (flag2)
		{
			BtcSpectate._hold = false;
			int count = BtcSpectate._held.Count;
			int num = 0;
			bool flag3 = replay && BtcSpectate._client != null;
			bool flag4 = flag3;
			if (flag4)
			{
				BtcSpectate.OpenTier2("hold end");
				num = BtcSpectate.Replay(BtcSpectate._held);
			}
			BtcSpectate._held2.Clear();
			BtcSpectate._heldIds2.Clear();
			BtcSpectate._tier2Open = true;
			try
			{
				bool flag5 = BtcSpectate._client != null;
				bool flag6 = flag5;
				if (flag6)
				{
					foreach (KeyValuePair<short, NetworkMessageDelegate> keyValuePair in BtcSpectate._hOrig)
					{
						BtcSpectate._client.handlers[keyValuePair.Key] = keyValuePair.Value;
					}
				}
			}
			catch (Exception ex)
			{
				BtcSpectate.Log("restore handlers: " + ex.Message);
			}
			BtcSpectate._hOrig.Clear();
			BtcSpectate._held.Clear();
			BtcSpectate._heldIds.Clear();
			BtcSpectate.Log(string.Concat(new object[]
			{
				"client: hold released (",
				why,
				"), ",
				count,
				" messages ",
				replay ? "replayed" : "dropped",
				", ",
				num,
				" errors"
			}));
			if (replay)
			{
				BtcSpectate._probeAt = Time.unscaledTime + 6f;
				bool cReturn = BtcSpectate._cReturn;
				bool flag7 = cReturn;
				if (flag7)
				{
					BtcSpectate._refireAt = Time.unscaledTime + 2.5f;
				}
				BtcSpectate._fixNext = 0f;
				bool flag8 = BtcSpectate._cReturn || BtcSpectate._cSpectator;
				bool flag9 = flag8;
				if (flag9)
				{
					BtcSpectate._hudAt = Time.unscaledTime + 3f;
					BtcSpectate._hudPass = 0;
				}
			}
		}
	}

	private static void HoldTick(float now)
	{
		try
		{
			bool flag = !BtcSpectate._hold;
			bool flag2 = !flag;
			if (flag2)
			{
				bool flag3 = !BtcSpectate._tier2Open && User.LocalUser != null;
				bool flag4 = flag3;
				if (flag4)
				{
					BtcSpectate.OpenTier2("LocalUser exists");
				}
				bool tier2Open = BtcSpectate._tier2Open;
				bool flag5 = tier2Open;
				if (flag5)
				{
					BtcSpectate.FixLocalFaction();
				}
				bool flag6 = GameSignals.Received<Action>(GameSignals.GameObjectsLoaded) && !BtcSpectate.RewindKeep;
				bool flag7 = now >= BtcSpectate._diagNext;
				bool flag8 = flag7;
				if (flag8)
				{
					BtcSpectate._diagNext = now + 2f;
					GameParam instance = GameParam.instance;
					BtcSpectate.Log(string.Concat(new object[]
					{
						"client: loader state: LocalUser=",
						(User.LocalUser != null).ToString(),
						" isInit=",
						(instance != null && instance._isInitialized).ToString(),
						" allFactions=",
						(instance != null && instance.HostLoadedAllFactions).ToString(),
						" hostLoaded=",
						(instance != null && instance.HostLoaded).ToString(),
						" ready=",
						ClientScene.ready.ToString(),
						" objectsLoadedSignal=",
						flag6.ToString(),
						" faction=",
						BtcSpectate.FactionInfo(),
						" held=",
						BtcSpectate._held.Count,
						" heldFactions=",
						BtcSpectate._held2.Count,
						" scene=",
						SceneManager.GetActiveScene().buildIndex
					}));
				}
				bool flag9 = flag6;
				bool flag10 = flag9;
				if (flag10)
				{
					BtcSpectate.StopHold(true, "GameObjectsLoaded");
				}
				else
				{
					bool flag11 = now - BtcSpectate._holdSince > 150f;
					bool flag12 = flag11;
					if (flag12)
					{
						BtcSpectate.StopHold(true, "timeout, loader never reached the object step");
					}
				}
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("HoldTick: " + ex.Message);
			BtcSpectate.StopHold(true, "error");
		}
	}

	public static void OnLoadError(Exception e)
	{
		try
		{
			string text = (e != null) ? e.ToString() : "null";
			bool flag = e is InterpreterException;
			bool flag2 = flag;
			if (flag2)
			{
				text = text + " | lua: " + ((InterpreterException)e).DecoratedMessage;
			}
			bool flag3 = text.Length > 4000;
			bool flag4 = flag3;
			if (flag4)
			{
				text = text.Substring(0, 4000);
			}
			BtcSpectate.Log("LOAD ERROR (" + BtcSpectate.Role() + "): " + text.Replace("\r", " ").Replace("\n", " | "));
		}
		catch (Exception)
		{
		}
	}

	private static int TierOf(NetworkHash128 a)
	{
		int result;
		try
		{
			bool flag = BtcSpectate._userAsset == null;
			bool flag2 = flag;
			if (flag2)
			{
				UNetManager instance = UNetManager.instance;
				BtcSpectate._userAsset = instance.playerPrefab.GetComponent<NetworkIdentity>().assetId.ToString();
				BtcSpectate._noiseAsset = Noise.NOISE_CONFIG.AssetId.ToString();
				BtcSpectate._factionAsset = Core.Faction.AssetId.ToString();
			}
			string a2 = a.ToString();
			bool flag3 = a2 == BtcSpectate._userAsset || a2 == BtcSpectate._noiseAsset;
			bool flag4 = flag3;
			if (flag4)
			{
				result = 0;
			}
			else
			{
				result = ((a2 == BtcSpectate._factionAsset) ? 2 : 3);
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("TierOf: " + ex.Message);
			result = 0;
		}
		return result;
	}

	private static int Replay(List<BtcSpectate.HeldMsg> list)
	{
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			BtcSpectate.HeldMsg heldMsg = list[i];
			try
			{
				NetworkMessageDelegate networkMessageDelegate;
				bool flag = BtcSpectate._hOrig.TryGetValue(heldMsg.t, out networkMessageDelegate) && networkMessageDelegate != null;
				bool flag2 = flag;
				if (flag2)
				{
					NetworkMessage networkMessage = new NetworkMessage();
					networkMessage.msgType = heldMsg.t;
					networkMessage.conn = BtcSpectate._client.connection;
					networkMessage.channelId = heldMsg.ch;
					networkMessage.reader = new NetworkReader(heldMsg.raw);
					networkMessageDelegate(networkMessage);
				}
			}
			catch (Exception ex)
			{
				num++;
				bool flag3 = num <= 5;
				bool flag4 = flag3;
				if (flag4)
				{
					BtcSpectate.Log(string.Concat(new object[]
					{
						"replay type ",
						heldMsg.t,
						": ",
						ex.Message
					}));
				}
			}
		}
		return num;
	}

	private static void OpenTier2(string why)
	{
		bool tier2Open = BtcSpectate._tier2Open;
		bool flag = !tier2Open;
		if (flag)
		{
			BtcSpectate._tier2Open = true;
			int count = BtcSpectate._held2.Count;
			int num = (BtcSpectate._client != null) ? BtcSpectate.Replay(BtcSpectate._held2) : 0;
			BtcSpectate._held2.Clear();
			BtcSpectate.Log(string.Concat(new object[]
			{
				"client: factions released (",
				why,
				"), ",
				count,
				" messages, ",
				num,
				" errors"
			}));
			BtcSpectate.FixLocalFaction();
		}
	}

	private static void FixLocalFaction()
	{
		try
		{
			User localUser = User.LocalUser;
			bool flag = localUser == null;
			bool flag2 = !flag;
			if (flag2)
			{
				bool flag3 = localUser.faction == null;
				bool flag4 = flag3;
				if (flag4)
				{
					Faction faction = null;
					FieldInfo field = typeof(User).GetField("___DefaultAlliedFactionGONetId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					bool flag5 = field != null;
					bool flag6 = flag5;
					if (flag6)
					{
						NetworkInstanceId netId = (NetworkInstanceId)field.GetValue(localUser);
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
					bool flag11 = faction == null && BtcSpectate._cSpectator;
					bool flag12 = flag11;
					if (flag12)
					{
						faction = Faction.Spectator;
					}
					bool flag13 = faction != null;
					bool flag14 = flag13;
					if (flag14)
					{
						localUser.faction = faction;
						BtcSpectate.Log("client: LocalUser.faction was empty, set to '" + faction.name + "'");
					}
				}
				bool cSpectator = BtcSpectate._cSpectator;
				bool flag15 = cSpectator;
				if (flag15)
				{
					BtcSpectate.SpectatorVision(localUser.faction);
				}
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("FixLocalFaction: " + ex.Message);
		}
	}

	private static string FactionInfo()
	{
		string result;
		try
		{
			User localUser = User.LocalUser;
			bool flag = localUser == null;
			bool flag2 = flag;
			if (flag2)
			{
				result = "noUser";
			}
			else
			{
				Faction faction = localUser.faction;
				bool flag3 = faction == null;
				bool flag4 = flag3;
				if (flag4)
				{
					result = "null";
				}
				else
				{
					result = string.Concat(new string[]
					{
						faction.name,
						"/spec=",
						faction.IsSpectator.ToString(),
						"/storage=",
						(faction.Storage != null).ToString()
					});
				}
			}
		}
		catch (Exception ex)
		{
			result = "err:" + ex.Message;
		}
		return result;
	}

	private static void SendBanner(int kind, string name)
	{
		try
		{
			NetworkServer.SendToAll(82, new BtcSpectate.BannerMsg
			{
				on = kind,
				name = name
			});
		}
		catch (Exception)
		{
		}
	}

	private static bool IsAway(User u)
	{
		// BCv2: a dropped player waiting for a reconnect is away even if its old connection id was reused by the new connection
		List<User> pending = BtcNetManager.PendingUsers;
		return u != null && ((pending != null && pending.Contains(u)) || u.connectionToClient == null || !BtcSpectate.Alive(u.connectionToClient));
	}

	private static string Ip(NetworkConnection c)
	{
		string text = (c != null && c.address != null) ? c.address : string.Empty;
		bool flag = text.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase);
		bool flag2 = flag;
		if (flag2)
		{
			text = text.Substring(7);
		}
		return text;
	}

	/// <summary>BCv2: dropped players set aside by StashPending while a new connection is handled (they still block the game).</summary>
	internal static int StashedCount { get { return BtcSpectate._stash != null ? BtcSpectate._stash.Count : 0; } }

	// legacy reflected on UNetManager fields/methods that only exist in BtcNetManager since the port (always false / not found)
	private static bool GetBool(string field)
	{
		return field == "connectionDegraded" && BtcNetManager.LinkDegraded;
	}

	// BCv2: the host link pause follows the waiting list by itself (BtcNetManager.HostPolicy); nothing to force here
	private static void SetHardPause(bool on)
	{
		BtcSpectate.Log("server: hard pause " + (on ? "on" : "off") + " requested (BCv2 policy decides)");
	}

	private static void ReattachNow(NetworkConnection conn, User u, string why)
	{
		try
		{
			NetworkServer.ReplacePlayerForConnection(conn, u.gameObject, 0);
			NetworkServer.SetClientReady(conn);
			u.NetworkReady = true;
			BtcSpectate.Log(string.Concat(new object[]
			{
				"server: ",
				why,
				": '",
				u.ScreenName,
				"' re-attached to conn ",
				conn.connectionId
			}));
			BtcSpectate.FinishReturn(u.ScreenName);
		}
		catch (Exception arg)
		{
			BtcSpectate.Log("ReattachNow: " + arg);
		}
	}

	/// <summary>BCv2 host card "continue without": stop waiting for stale slots, keep the dropped players re-attachable.</summary>
	internal static void CancelWaitingFromHost() { BtcSpectate.CancelWaiting(false); }

	private static void CancelWaiting() { BtcSpectate.CancelWaiting(true); }

	private static void CancelWaiting(bool clearPending)
	{
		try
		{
			IList list = BtcSpectate.Pending();
			bool flag = list != null && clearPending;
			bool flag2 = flag;
			if (flag2)
			{
				list.Clear();
			}
			foreach (KeyValuePair<string, BtcSpectate.Slot> keyValuePair in BtcSpectate._slots)
			{
				User user = keyValuePair.Value.user;
				NetworkConnection networkConnection = (user != null) ? user.connectionToClient : null;
				bool flag3 = networkConnection != null && BtcSpectate.Stale(networkConnection);
				bool flag4 = flag3;
				if (flag4)
				{
					BtcSpectate._ignore.Add(networkConnection.connectionId);
				}
			}
			BtcSpectate.Log("server: host stopped waiting for: " + BtcSpectate._waitText);
			BtcSpectate._waitCount = 0;
			BtcSpectate._waitText = string.Empty;
			bool flag5 = !BtcSpectate.GetBool("connectionDegraded");
			bool flag6 = flag5;
			if (flag6)
			{
				BtcSpectate.SetHardPause(false);
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("CancelWaiting: " + ex.Message);
		}
	}

	private static void GuardPause()
	{
		try
		{
			NetworkMessageDelegate networkMessageDelegate;
			bool flag = NetworkServer.handlers.TryGetValue(72, out networkMessageDelegate) && networkMessageDelegate != null && networkMessageDelegate.Method.DeclaringType != typeof(BtcSpectate);
			bool flag2 = flag;
			if (flag2)
			{
				BtcSpectate._pauseOrig = networkMessageDelegate;
				NetworkServer.handlers[72] = new NetworkMessageDelegate(BtcSpectate.OnPauseRequest);
				BtcSpectate.Log("server: pause request handler guarded");
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("GuardPause: " + ex.Message);
		}
	}

	private static void OnPauseRequest(NetworkMessage nm)
	{
		bool flag = nm.conn != null && BtcSpectate._spec.Contains(nm.conn.connectionId);
		bool flag2 = flag;
		if (flag2)
		{
			BtcSpectate.Log("server: pause request from spectator conn " + nm.conn.connectionId + " ignored");
		}
		else
		{
			bool flag3 = BtcSpectate._pauseOrig != null;
			bool flag4 = flag3;
			if (flag4)
			{
				BtcSpectate._pauseOrig(nm);
			}
		}
	}

	private static void CopyHeightmap(string stage)
	{
		try
		{
			string text = System.IO.Path.Combine(stage, "Heightmap.png");
			bool flag = File.Exists(text);
			bool flag2 = flag;
			if (flag2)
			{
				BtcSpectate.Log("server: snapshot already has Heightmap.png (" + new FileInfo(text).Length + " bytes)");
			}
			else
			{
				Asset_Map map = GameParam.Map;
				string text2 = (map != null && map.path != null) ? System.IO.Path.Combine(map.path, "Heightmap.png") : null;
				bool flag3 = text2 != null && File.Exists(text2);
				bool flag4 = flag3;
				if (flag4)
				{
					File.Copy(text2, text, true);
					BtcSpectate.Log(string.Concat(new object[]
					{
						"server: Heightmap.png added to the snapshot (",
						new FileInfo(text).Length,
						" bytes) from ",
						text2
					}));
				}
				else
				{
					BtcSpectate.Log("server: NO Heightmap.png found for the snapshot, source: " + text2);
				}
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("CopyHeightmap: " + ex.Message);
		}
	}

	private static void PruneIds(HashSet<int> set)
	{
		bool flag = set.Count == 0;
		bool flag2 = !flag;
		if (flag2)
		{
			List<int> list = null;
			foreach (int num in set)
			{
				bool flag3 = false;
				for (int i = 0; i < NetworkServer.connections.Count; i++)
				{
					bool flag4 = NetworkServer.connections[i] != null && NetworkServer.connections[i].connectionId == num;
					bool flag5 = flag4;
					if (flag5)
					{
						flag3 = true;
						break;
					}
				}
				bool flag6 = !flag3 && (BtcSpectate._job == null || BtcSpectate._job.conn == null || BtcSpectate._job.conn.connectionId != num);
				bool flag7 = flag6;
				if (flag7)
				{
					bool flag8 = list == null;
					bool flag9 = flag8;
					if (flag9)
					{
						list = new List<int>();
					}
					list.Add(num);
				}
			}
			bool flag10 = list != null;
			bool flag11 = flag10;
			if (flag11)
			{
				for (int j = 0; j < list.Count; j++)
				{
					set.Remove(list[j]);
					BtcSpectate._seen.Remove(list[j]);
				}
			}
		}
	}

	private static void LegacyReattach(float now)
	{
		IList list = BtcSpectate.Pending();
		bool flag = list == null || list.Count == 0;
		bool flag2 = !flag;
		if (flag2)
		{
			for (int i = 0; i < NetworkServer.connections.Count; i++)
			{
				NetworkConnection networkConnection = NetworkServer.connections[i];
				bool flag3 = networkConnection == null || !networkConnection.isConnected || networkConnection.playerControllers.Count > 0 || BtcSpectate._hello.Contains(networkConnection.connectionId) || BtcSpectate._spec.Contains(networkConnection.connectionId) || BtcSpectate._ret.Contains(networkConnection.connectionId);
				bool flag4 = !flag3;
				if (flag4)
				{
					float num = 0f;
					bool flag5 = !BtcSpectate._seen.TryGetValue(networkConnection.connectionId, out num);
					bool flag6 = flag5;
					if (flag6)
					{
						BtcSpectate._seen[networkConnection.connectionId] = now;
					}
					else
					{
						bool flag7 = now - num > 8f && list.Count > 0;
						bool flag8 = flag7;
						if (flag8)
						{
							User user = list[0] as User;
							bool flag9 = user != null;
							bool flag10 = flag9;
							if (flag10)
							{
								BtcSpectate.ReattachNow(networkConnection, user, "client without hello");
							}
							else
							{
								list.RemoveAt(0);
							}
						}
					}
				}
			}
		}
	}

	private static void SpectatorVision(Faction f)
	{
		try
		{
			bool flag = f != null && !f.HasGlobalFogVision;
			bool flag2 = flag;
			if (flag2)
			{
				f.HasGlobalFogVision = true;
				BtcSpectate.Log("client: spectator faction got global fog vision");
			}
			bool flag3 = !LocalFactionVision.AllVisible;
			bool flag4 = flag3;
			if (flag4)
			{
				LocalFactionVision.ToggleFogSystem(false);
				BtcSpectate.Log("client: fog of war switched off for the spectator");
			}
		}
		catch (Exception ex)
		{
			bool flag5 = Time.unscaledTime >= BtcSpectate._visErrNext;
			bool flag6 = flag5;
			if (flag6)
			{
				BtcSpectate._visErrNext = Time.unscaledTime + 5f;
				BtcSpectate.Log("SpectatorVision: " + ex.Message);
			}
		}
	}

	private static void Probe()
	{
		try
		{
			MiniMap miniMap = UnityEngine.Object.FindObjectOfType<MiniMap>();
			int num = 0;
			foreach (MiniMap miniMap2 in Resources.FindObjectsOfTypeAll<MiniMap>())
			{
				bool flag = miniMap2 != null && miniMap2.gameObject.scene.IsValid();
				bool flag2 = flag;
				if (flag2)
				{
					num++;
				}
			}
			string text = string.Concat(new object[]
			{
				"minimap=",
				(miniMap != null).ToString(),
				" (incl. inactive: ",
				num,
				") showsMinimap=",
				UIGameObserver.ShowsMinimap.ToString(),
				" heightmap=",
				(miniMap != null && miniMap.heightmap != null).ToString()
			});
			int num2 = 0;
			int num3 = 0;
			string text2 = string.Empty;
			DamageReceiver[] array2 = UnityEngine.Object.FindObjectsOfType<DamageReceiver>();
			for (int j = 0; j < array2.Length; j++)
			{
				num2++;
				bool flag3 = array2[j].CurrentHP < array2[j].MaxHP;
				bool flag4 = flag3;
				if (flag4)
				{
					num3++;
					bool flag5 = num3 <= 6;
					bool flag6 = flag5;
					if (flag6)
					{
						text2 = string.Concat(new string[]
						{
							text2,
							" [",
							array2[j].name,
							" ",
							array2[j].CurrentHP.ToString("0.#"),
							"/",
							array2[j].MaxHP.ToString("0.#"),
							"]"
						});
					}
				}
			}
			BtcSpectate.Log(string.Concat(new object[]
			{
				"client: ui: timeScale=",
				Time.timeScale,
				" screen=",
				Screen.width,
				"x",
				Screen.height,
				" ",
				BtcSpectate.UiInfo()
			}));
			BtcSpectate.Log("client: projects: " + BtcSpectate.ProjectInfo());
			BtcSpectate.Log(string.Concat(new object[]
			{
				"client: probe: ",
				text,
				" allVisible=",
				LocalFactionVision.AllVisible.ToString(),
				" faction=",
				BtcSpectate.FactionInfo(),
				" damageReceivers=",
				num2,
				" hurt=",
				num3,
				text2
			}));
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("Probe: " + ex.Message);
		}
	}

	private static bool IsLocal(NetworkConnection c)
	{
		bool flag = c == null;
		bool flag2 = flag;
		bool result;
		if (flag2)
		{
			result = false;
		}
		else
		{
			for (int i = 0; i < NetworkServer.localConnections.Count; i++)
			{
				NetworkConnection networkConnection = NetworkServer.localConnections[i];
				bool flag3 = networkConnection != null && networkConnection.connectionId == c.connectionId;
				bool flag4 = flag3;
				if (flag4)
				{
					return true;
				}
			}
			result = false;
		}
		return result;
	}

	private static bool Stale(NetworkConnection c)
	{
		bool flag = c == null || BtcSpectate.IsLocal(c);
		bool flag2 = flag;
		bool result;
		if (flag2)
		{
			result = false;
		}
		else
		{
			bool flag3 = !BtcSpectate.Alive(c);
			bool flag4 = flag3;
			if (flag4)
			{
				result = true;
			}
			else
			{
				try
				{
					bool flag5 = BtcSpectate._hbField == null;
					bool flag6 = flag5;
					if (flag6)
					{
						BtcSpectate._hbField = typeof(UNetManager).GetField("lastHeartbeatUnscaledTimeByConnection", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					}
					Dictionary<int, float> dictionary = (BtcSpectate._hbField != null) ? (BtcSpectate._hbField.GetValue(UNetManager.instance) as Dictionary<int, float>) : null;
					float num = 0f;
					bool flag7 = dictionary != null && dictionary.TryGetValue(c.connectionId, out num);
					bool flag8 = flag7;
					if (flag8)
					{
						return Time.unscaledTime - num > 3f;
					}
				}
				catch (Exception)
				{
				}
				result = false;
			}
		}
		return result;
	}

	private static void RefreshSlots()
	{
		try
		{
			UNetManager instance = UNetManager.instance;
			bool flag = instance == null || instance.UserList == null;
			bool flag2 = !flag;
			if (flag2)
			{
				for (int i = 0; i < instance.UserList.Count; i++)
				{
					User user = instance.UserList[i];
					bool flag3 = user == null;
					bool flag4 = !flag3;
					if (flag4)
					{
						string screenName = BtcSpectate.K(user.ScreenName);
						bool flag5 = string.IsNullOrEmpty(screenName);
						bool flag6 = !flag5;
						if (flag6)
						{
							NetworkConnection connectionToClient = user.connectionToClient;
							string a;
							bool flag7 = connectionToClient != null && BtcSpectate._names.TryGetValue(connectionToClient.connectionId, out a) && a != screenName;
							bool flag8 = !flag7;
							if (flag8)
							{
								BtcSpectate._userName[user.GetInstanceID()] = screenName;
								Faction faction = user.faction;
								bool flag9 = faction == null || faction.IsSpectator;
								bool flag10 = !flag9;
								if (flag10)
								{
									BtcSpectate.Slot slot;
									bool flag11 = !BtcSpectate._slots.TryGetValue(screenName, out slot);
									bool flag12 = flag11;
									if (flag12)
									{
										slot = new BtcSpectate.Slot();
										slot.name = screenName;
										BtcSpectate._slots[screenName] = slot;
										BtcSpectate.Log(string.Concat(new string[]
										{
											"server: player '",
											screenName,
											"' registered on faction '",
											faction.name,
											"'"
										}));
									}
									slot.faction = faction;
									slot.fnum = user.factionNumber;
									slot.team = user.TeamNumber;
									slot.user = user;
								}
							}
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("RefreshSlots: " + ex.Message);
		}
	}

	private static User UserByName(string name, NetworkConnection except)
	{
		try
		{
			UNetManager instance = UNetManager.instance;
			// BCv2: two players may share a nick (same Steam name): a dropped (away) one wins, so a return never hits the host's user
			// (the host's own local connection never counts as "connected", so pending users are checked first and local ones skipped)
			List<User> pending = BtcNetManager.PendingUsers;
			if (pending != null)
				for (int j = 0; j < pending.Count; j++)
					if (pending[j] != null && BtcSpectate.K(pending[j].ScreenName) == BtcSpectate.K(name)) return pending[j];
			for (int j = 0; j < instance.UserList.Count; j++)
			{
				User away = instance.UserList[j];
				if (away != null && BtcSpectate.K(away.ScreenName) == BtcSpectate.K(name) && BtcSpectate.IsAway(away)
					&& (away.connectionToClient == null || !BtcSpectate.IsLocal(away.connectionToClient))) return away;
			}
			for (int i = 0; i < instance.UserList.Count; i++)
			{
				User user = instance.UserList[i];
				bool flag = user == null || BtcSpectate.K(user.ScreenName) != BtcSpectate.K(name);
				bool flag2 = !flag;
				if (flag2)
				{
					NetworkConnection connectionToClient = user.connectionToClient;
					bool flag3 = except != null && connectionToClient != null && connectionToClient.connectionId == except.connectionId;
					bool flag4 = !flag3;
					if (flag4)
					{
						return user;
					}
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private static string PendName(object e)
	{
		User user = e as User;
		bool flag = user == null;
		bool flag2 = flag;
		string result;
		if (flag2)
		{
			result = null;
		}
		else
		{
			bool flag3 = user != null;
			bool flag4 = flag3;
			if (flag4)
			{
				result = BtcSpectate.K(user.ScreenName);
			}
			else
			{
				string text = null;
				result = (BtcSpectate._userName.TryGetValue(user.GetInstanceID(), out text) ? text : null);
			}
		}
		return result;
	}

	private static void StashPending()
	{
		try
		{
			IList list = BtcSpectate.Pending();
			bool flag = list == null || list.Count == 0;
			bool flag2 = !flag;
			if (flag2)
			{
				BtcSpectate._stash.Clear();
				for (int i = 0; i < list.Count; i++)
				{
					BtcSpectate._stash.Add(list[i]);
				}
				list.Clear();
				BtcSpectate.Log("server: " + BtcSpectate._stash.Count + " waiting player(s) set aside while the new connection is handled");
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("StashPending: " + ex.Message);
		}
	}

	private static void RestorePending()
	{
		bool flag = BtcSpectate._stash.Count == 0;
		bool flag2 = !flag;
		if (flag2)
		{
			try
			{
				IList list = BtcSpectate.Pending();
				bool flag3 = list != null;
				bool flag4 = flag3;
				if (flag4)
				{
					for (int i = 0; i < BtcSpectate._stash.Count; i++)
					{
						bool flag5 = !list.Contains(BtcSpectate._stash[i]);
						bool flag6 = flag5;
						if (flag6)
						{
							list.Add(BtcSpectate._stash[i]);
						}
					}
				}
			}
			catch (Exception ex)
			{
				BtcSpectate.Log("RestorePending: " + ex.Message);
			}
			BtcSpectate._stash.Clear();
		}
	}

	private static void BuildWaiting()
	{
		try
		{
			List<string> list = new List<string>();
			IList list2 = BtcSpectate.Pending();
			bool flag = list2 != null;
			bool flag2 = flag;
			if (flag2)
			{
				for (int i = 0; i < list2.Count; i++)
				{
					string text = BtcSpectate.PendName(list2[i]);
					bool flag3 = !string.IsNullOrEmpty(text) && !list.Contains(text);
					bool flag4 = flag3;
					if (flag4)
					{
						list.Add(text);
					}
				}
			}
			foreach (KeyValuePair<string, BtcSpectate.Slot> keyValuePair in BtcSpectate._slots)
			{
				User user = keyValuePair.Value.user;
				bool flag5 = user == null;
				bool flag6 = !flag5;
				if (flag6)
				{
					NetworkConnection connectionToClient = user.connectionToClient;
					bool flag7 = connectionToClient != null && !BtcSpectate._ignore.Contains(connectionToClient.connectionId) && BtcSpectate.Stale(connectionToClient) && !list.Contains(keyValuePair.Key);
					bool flag8 = flag7;
					if (flag8)
					{
						list.Add(keyValuePair.Key);
					}
				}
			}
			BtcSpectate._waitCount = list.Count;
			BtcSpectate._waitText = string.Join(", ", list.ToArray());
		}
		catch (Exception)
		{
			BtcSpectate._waitCount = 0;
			BtcSpectate._waitText = string.Empty;
		}
	}

	private static bool SlotBusy(BtcSpectate.Slot s)
	{
		bool flag = BtcSpectate._job != null && BtcSpectate._job.slot == s && BtcSpectate.Alive(BtcSpectate._job.conn);
		bool flag2 = flag;
		bool result;
		if (flag2)
		{
			result = true;
		}
		else
		{
			foreach (BtcSpectate.Job job in BtcSpectate._queue)
			{
				bool flag3 = job.slot == s && BtcSpectate.Alive(job.conn);
				bool flag4 = flag3;
				if (flag4)
				{
					return true;
				}
			}
			result = false;
		}
		return result;
	}

	private static BtcSpectate.Job JobOf(NetworkConnection c)
	{
		bool flag = BtcSpectate._job != null && BtcSpectate._job.conn == c;
		bool flag2 = flag;
		BtcSpectate.Job result;
		if (flag2)
		{
			result = BtcSpectate._job;
		}
		else
		{
			foreach (BtcSpectate.Job job in BtcSpectate._queue)
			{
				bool flag3 = job.conn == c;
				bool flag4 = flag3;
				if (flag4)
				{
					return job;
				}
			}
			result = null;
		}
		return result;
	}

	/// <summary>Identity key of a player: nick without colour/rich-text tags (rainbow nicks change every frame, identity must not). Null when empty.</summary>
	private static string K(string name)
	{
		try
		{
			string p = BtcNick.Plain(name);
			if (string.IsNullOrEmpty(p)) return null;
			return p.Trim();
		}
		catch (Exception e)
		{
			BtcLog.Error("Spectate", "K(): " + e.Message);
			return string.IsNullOrEmpty(name) ? null : name;
		}
	}

	private static bool Identity(string name, string ip, string key)
	{
		bool flag = BtcSpectate.Val(BtcSpectate.iRetIp, 1f) < 0.5f;
		bool flag2 = flag;
		bool result;
		if (flag2)
		{
			result = true;
		}
		else
		{
			string a;
			bool flag3 = key.Length > 0 && BtcSpectate._keys.TryGetValue(name, out a) && string.Equals(a, key, StringComparison.Ordinal);
			bool flag4 = flag3;
			string a2;
			result = (flag4 || !BtcSpectate._known.TryGetValue(name, out a2) || string.Equals(a2, ip, StringComparison.Ordinal));
		}
		return result;
	}

	private static void DropOld(BtcSpectate.Slot s, NetworkConnection newConn)
	{
		try
		{
			User user = BtcSpectate.UserByName(s.name, newConn);
			NetworkConnection networkConnection = (user != null) ? user.connectionToClient : null;
			bool flag = user != null && s.faction != null;
			bool flag2 = flag;
			if (flag2)
			{
				s.faction.RemoveOwner(user);
			}
			BtcSpectate.PurgeOwners(s.faction);
			bool flag3 = networkConnection != null && (newConn == null || networkConnection.connectionId != newConn.connectionId);
			bool flag4 = flag3;
			if (flag4)
			{
				BtcSpectate._ignore.Add(networkConnection.connectionId);
				BtcSpectate.Log(string.Concat(new object[]
				{
					"server: old connection ",
					networkConnection.connectionId,
					" of '",
					s.name,
					"' is closed"
				}));
				try
				{
					UNetManager.instance.SafeDisconnectConnection(networkConnection);
				}
				catch (Exception ex)
				{
					BtcSpectate.Log("SafeDisconnect old: " + ex.Message);
				}
			}
			bool flag5 = user != null;
			bool flag6 = flag5;
			if (flag6)
			{
				NetworkServer.Destroy(user.gameObject);
			}
		}
		catch (Exception ex2)
		{
			BtcSpectate.Log("DropOld: " + ex2.Message);
		}
	}

	private static void PurgeOwners(Faction f)
	{
		try
		{
			bool flag = f == null;
			bool flag2 = !flag;
			if (flag2)
			{
				for (int i = f.OwnerSyncList.Count - 1; i >= 0; i--)
				{
					GameObject x = f.OwnerSyncList[i];
					bool flag3 = x == null;
					bool flag4 = flag3;
					if (flag4)
					{
						f.OwnerSyncList.RemoveAt(i);
					}
				}
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("PurgeOwners: " + ex.Message);
		}
	}

	private static void FinishReturn(string name)
	{
		try
		{
			IList list = BtcSpectate.Pending();
			bool flag = list != null;
			bool flag2 = flag;
			if (flag2)
			{
				for (int i = list.Count - 1; i >= 0; i--)
				{
					string text = BtcSpectate.PendName(list[i]);
					User x = list[i] as User;
					bool flag3 = x != null && x == null;
					bool flag4 = text == BtcSpectate.K(name) || (flag3 && text == null);
					bool flag5 = flag4;
					if (flag5)
					{
						list.RemoveAt(i);
					}
				}
			}
			bool flag6 = (list == null || list.Count == 0) && !BtcSpectate.GetBool("connectionDegraded");
			bool flag7 = flag6;
			if (flag7)
			{
				BtcSpectate.SetHardPause(false);
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("FinishReturn: " + ex.Message);
		}
	}

	private static string MyKey()
	{
		string result;
		try
		{
			string text = PlayerPrefs.GetString("btc_spec_key", string.Empty);
			bool flag = string.IsNullOrEmpty(text);
			bool flag2 = flag;
			if (flag2)
			{
				text = Guid.NewGuid().ToString("N");
				PlayerPrefs.SetString("btc_spec_key", text);
				PlayerPrefs.Save();
			}
			result = text;
		}
		catch (Exception)
		{
			result = string.Empty;
		}
		return result;
	}

	private static void StartPreHold()
	{
		try
		{
			bool flag = BtcSpectate._pre || BtcSpectate._client == null;
			bool flag2 = !flag;
			if (flag2)
			{
				BtcSpectate._preQ.Clear();
				BtcSpectate._hOrig.Clear();
				for (int i = 0; i < BtcSpectate.PreTypes.Length; i++)
				{
					short key = BtcSpectate.PreTypes[i];
					NetworkMessageDelegate networkMessageDelegate;
					bool flag3 = BtcSpectate._client.handlers.TryGetValue(key, out networkMessageDelegate) && networkMessageDelegate != null && networkMessageDelegate.Method.DeclaringType != typeof(BtcSpectate);
					bool flag4 = flag3;
					if (flag4)
					{
						BtcSpectate._hOrig[key] = networkMessageDelegate;
						BtcSpectate._client.handlers[key] = new NetworkMessageDelegate(BtcSpectate.OnPre);
					}
				}
				BtcSpectate._pre = true;
				BtcSpectate._preKeep = true;
				BtcSpectate.Log("client: world messages are queued until the host answers (" + BtcSpectate._hOrig.Count + " handlers)");
			}
		}
		catch (Exception arg)
		{
			BtcSpectate.Log("StartPreHold: " + arg);
		}
	}

	private static void OnPre(NetworkMessage nm)
	{
		bool flag = !BtcSpectate._preKeep;
		bool flag2 = !flag;
		if (flag2)
		{
			try
			{
				bool flag3 = BtcSpectate._preQ.Count < 4000;
				bool flag4 = flag3;
				if (flag4)
				{
					nm.reader.SeekZero();
					BtcSpectate.HeldMsg heldMsg = new BtcSpectate.HeldMsg();
					heldMsg.t = nm.msgType;
					heldMsg.ch = nm.channelId;
					heldMsg.raw = nm.reader.ReadBytes(nm.reader.Length);
					BtcSpectate._preQ.Add(heldMsg);
				}
			}
			catch (Exception ex)
			{
				BtcSpectate.Log(string.Concat(new object[]
				{
					"OnPre type ",
					nm.msgType,
					": ",
					ex.Message
				}));
			}
		}
	}

	private static void DiscardPre()
	{
		BtcSpectate._preKeep = false;
		BtcSpectate._preQ.Clear();
	}

	private static void StopPreHold(bool replay)
	{
		bool flag = !BtcSpectate._pre;
		bool flag2 = !flag;
		if (flag2)
		{
			BtcSpectate._pre = false;
			int count = BtcSpectate._preQ.Count;
			int num = 0;
			try
			{
				bool flag3 = BtcSpectate._client != null;
				bool flag4 = flag3;
				if (flag4)
				{
					foreach (KeyValuePair<short, NetworkMessageDelegate> keyValuePair in BtcSpectate._hOrig)
					{
						BtcSpectate._client.handlers[keyValuePair.Key] = keyValuePair.Value;
					}
				}
			}
			catch (Exception ex)
			{
				BtcSpectate.Log("restore pre handlers: " + ex.Message);
			}
			bool flag5 = replay && BtcSpectate._preKeep && BtcSpectate._client != null;
			bool flag6 = flag5;
			if (flag6)
			{
				num = BtcSpectate.Replay(BtcSpectate._preQ);
			}
			BtcSpectate.Log(string.Concat(new object[]
			{
				"client: queued world messages ",
				(replay && BtcSpectate._preKeep) ? "replayed" : "dropped",
				": ",
				count,
				", errors ",
				num
			}));
			BtcSpectate._preQ.Clear();
			BtcSpectate._preKeep = false;
			BtcSpectate._hOrig.Clear();
		}
	}

	private static string UiInfo()
	{
		string result;
		try
		{
			string text = string.Empty;
			Canvas[] array = UnityEngine.Object.FindObjectsOfType<Canvas>();
			int num = 0;
			int num2 = 0;
			while (num2 < array.Length && num < 8)
			{
				bool flag = array[num2] == null || !array[num2].isRootCanvas;
				bool flag2 = !flag;
				if (flag2)
				{
					num++;
					CanvasScaler component = array[num2].GetComponent<CanvasScaler>();
					RectTransform rectTransform = (RectTransform)array[num2].transform;
					text = string.Concat(new object[]
					{
						text,
						" [",
						array[num2].name,
						" mode=",
						array[num2].renderMode,
						" scale=",
						array[num2].scaleFactor.ToString("0.##"),
						" rect=",
						rectTransform.rect.width.ToString("0"),
						"x",
						rectTransform.rect.height.ToString("0"),
						(component != null) ? string.Concat(new object[]
						{
							" ref=",
							component.referenceResolution.x,
							"x",
							component.referenceResolution.y
						}) : string.Empty,
						"]"
					});
				}
				num2++;
			}
			result = string.Concat(new object[]
			{
				"canvases(",
				array.Length,
				"):",
				text
			});
		}
		catch (Exception ex)
		{
			result = "err:" + ex.Message;
		}
		return result;
	}

	private static string ProjectInfo()
	{
		string result;
		try
		{
			Project[] array = UnityEngine.Object.FindObjectsOfType<Project>();
			Faction faction = (User.LocalUser != null) ? User.LocalUser.faction : null;
			int num = 0;
			for (int i = 0; i < array.Length; i++)
			{
				bool flag = array[i] != null && faction != null && array[i].faction == faction;
				bool flag2 = flag;
				if (flag2)
				{
					num++;
				}
			}
			result = string.Concat(new object[]
			{
				"inScene=",
				array.Length,
				" ofMyFaction=",
				num
			});
		}
		catch (Exception ex)
		{
			result = "err:" + ex.Message;
		}
		return result;
	}

	private static void RefireProjects()
	{
		try
		{
			Faction faction = (User.LocalUser != null) ? User.LocalUser.faction : null;
			bool flag = faction == null;
			bool flag2 = !flag;
			if (flag2)
			{
				int num = 0;
				foreach (Project project in UnityEngine.Object.FindObjectsOfType<Project>())
				{
					bool flag3 = project != null && project.faction == faction;
					bool flag4 = flag3;
					if (flag4)
					{
						GameSignals.Invoke<Project>(Project.ProjectStarted, project);
						num++;
					}
				}
				BtcSpectate.Log("client: ProjectStarted resent for " + num + " projects");
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("RefireProjects: " + ex.Message);
		}
	}

	private static void RelationsTick(float now)
	{
		bool flag = BtcSpectate._relAt.Count == 0;
		bool flag2 = !flag;
		if (flag2)
		{
			for (int i = 0; i < BtcSpectate._relAt.Count; i++)
			{
				bool flag3 = now < BtcSpectate._relAt[i].Key;
				bool flag4 = !flag3;
				if (flag4)
				{
					int value = BtcSpectate._relAt[i].Value;
					BtcSpectate._relAt.RemoveAt(i);
					i--;
					BtcSpectate.SendRelations(value);
				}
			}
		}
	}

	private static void ScheduleRelations(NetworkConnection conn)
	{
		bool flag = conn == null;
		bool flag2 = !flag;
		if (flag2)
		{
			float unscaledTime = Time.unscaledTime;
			BtcSpectate._relAt.Add(new KeyValuePair<float, int>(unscaledTime + 3f, conn.connectionId));
			BtcSpectate._relAt.Add(new KeyValuePair<float, int>(unscaledTime + 8f, conn.connectionId));
			BtcSpectate._relAt.Add(new KeyValuePair<float, int>(unscaledTime + 14f, conn.connectionId));
		}
	}

	private static void SendRelations(int connId)
	{
		try
		{
			NetworkConnection networkConnection = null;
			foreach (NetworkConnection networkConnection2 in NetworkServer.connections)
			{
				bool flag = networkConnection2 != null && networkConnection2.connectionId == connId;
				bool flag2 = flag;
				if (flag2)
				{
					networkConnection = networkConnection2;
					break;
				}
			}
			bool flag3 = networkConnection == null || !networkConnection.isConnected;
			bool flag4 = flag3;
			if (flag4)
			{
				BtcSpectate.Log("server: relations: connection " + connId + " is gone");
			}
			else
			{
				List<uint> list = new List<uint>();
				List<uint> list2 = new List<uint>();
				List<byte> list3 = new List<byte>();
				Faction[] array = UnityEngine.Object.FindObjectsOfType<Faction>();
				for (int i = 0; i < array.Length; i++)
				{
					NetworkIdentity component = array[i].GetComponent<NetworkIdentity>();
					bool flag5 = component == null;
					bool flag6 = !flag5;
					if (flag6)
					{
						foreach (KeyValuePair<Faction, Affiliation.Relation> keyValuePair in array[i].AffiliationMap)
						{
							bool flag7 = keyValuePair.Key == null || keyValuePair.Value == Affiliation.Relation.Undefined;
							bool flag8 = !flag7;
							if (flag8)
							{
								NetworkIdentity component2 = keyValuePair.Key.GetComponent<NetworkIdentity>();
								bool flag9 = component2 == null;
								bool flag10 = !flag9;
								if (flag10)
								{
									list.Add(component.netId.Value);
									list2.Add(component2.netId.Value);
									list3.Add((byte)keyValuePair.Value);
								}
							}
						}
					}
				}
				NetworkServer.SendToClient(connId, 83, new BtcSpectate.RelMsg
				{
					a = list.ToArray(),
					b = list2.ToArray(),
					r = list3.ToArray()
				});
				BtcSpectate.Log(string.Concat(new object[]
				{
					"server: faction relations sent to conn ",
					connId,
					" (",
					list.Count,
					" pairs)"
				}));
			}
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("SendRelations: " + ex.Message);
		}
	}

	private static void OnRel(NetworkMessage nm)
	{
		try
		{
			BtcSpectate.RelMsg relMsg = nm.ReadMessage<BtcSpectate.RelMsg>();
			BtcSpectate._relA = relMsg.a;
			BtcSpectate._relB = relMsg.b;
			BtcSpectate._relR = relMsg.r;
			BtcSpectate.ApplyRelations("msg");
		}
		catch (Exception ex)
		{
			BtcSpectate.Log("OnRel: " + ex.Message);
		}
	}

	private static int ApplyRelations(string why)
	{
		bool flag = NetworkServer.active || BtcSpectate._relA.Length == 0;
		bool flag2 = flag;
		int result;
		if (flag2)
		{
			result = 0;
		}
		else
		{
			int num = 0;
			int num2 = 0;
			try
			{
				HashSet<Faction> hashSet = new HashSet<Faction>();
				for (int i = 0; i < BtcSpectate._relA.Length; i++)
				{
					GameObject gameObject = ClientScene.FindLocalObject(new NetworkInstanceId(BtcSpectate._relA[i]));
					GameObject gameObject2 = ClientScene.FindLocalObject(new NetworkInstanceId(BtcSpectate._relB[i]));
					Faction faction = (gameObject != null) ? gameObject.GetComponent<Faction>() : null;
					Faction faction2 = (gameObject2 != null) ? gameObject2.GetComponent<Faction>() : null;
					bool flag3 = faction == null || faction2 == null;
					bool flag4 = flag3;
					if (flag4)
					{
						num2++;
					}
					else
					{
						faction.AffiliationMap[faction2] = (Affiliation.Relation)BtcSpectate._relR[i];
						hashSet.Add(faction);
						num++;
					}
				}
				foreach (Faction f in hashSet)
				{
					BtcSpectate.RebuildFactionLists(f);
				}
			}
			catch (Exception ex)
			{
				BtcSpectate.Log("ApplyRelations: " + ex.Message);
			}
			BtcSpectate.Log(string.Concat(new object[]
			{
				"client: relations applied (",
				why,
				"): ",
				num,
				" pairs, ",
				num2,
				" not spawned yet"
			}));
			result = num;
		}
		return result;
	}

	private static void RebuildFactionLists(Faction f)
	{
		List<Faction> list = new List<Faction>();
		List<Faction> list2 = new List<Faction>();
		List<Faction> list3 = new List<Faction>();
		foreach (KeyValuePair<Faction, Affiliation.Relation> keyValuePair in f.AffiliationMap)
		{
			bool flag = keyValuePair.Key == null;
			bool flag2 = !flag;
			if (flag2)
			{
				bool flag3 = keyValuePair.Value == Affiliation.Relation.Enemy;
				bool flag4 = flag3;
				if (flag4)
				{
					list.Add(keyValuePair.Key);
				}
				else
				{
					bool flag5 = keyValuePair.Value == Affiliation.Relation.Allied;
					bool flag6 = flag5;
					if (flag6)
					{
						list2.Add(keyValuePair.Key);
					}
					else
					{
						bool flag7 = keyValuePair.Value == Affiliation.Relation.Neutral;
						bool flag8 = flag7;
						if (flag8)
						{
							list3.Add(keyValuePair.Key);
						}
					}
				}
			}
		}
		f.EnemyFactions = list;
		f.AllyFactions = list2;
		f.NeutralFactions = list3;
		List<Faction> list4 = new List<Faction>(list);
		list4.AddRange(list3);
		f.EnemyOrNeutralFactions = list4;
		bool flag9 = f.SharedVision != null;
		bool flag10 = flag9;
		if (flag10)
		{
			bool flag11 = BtcSpectate._miRefresh == null;
			bool flag12 = flag11;
			if (flag12)
			{
				BtcSpectate._miRefresh = typeof(SharedFactionVision).GetMethod("RefreshAlliances", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			bool flag13 = BtcSpectate._miRefresh != null;
			bool flag14 = flag13;
			if (flag14)
			{
				BtcSpectate._miRefresh.Invoke(f.SharedVision, null);
			}
		}
	}

	private static int ResyncOne(object obs, IEnumerable items)
	{
		bool flag = obs == null;
		bool flag2 = flag;
		int result;
		if (flag2)
		{
			result = -1;
		}
		else
		{
			Type type = obs.GetType();
			MethodInfo method = type.GetMethod("OnEnterCreation", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			FieldInfo fieldInfo = null;
			Type type2 = type;
			while (type2 != null && fieldInfo == null)
			{
				fieldInfo = type2.GetField("condition", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				type2 = type2.BaseType;
			}
			bool flag3 = method == null || fieldInfo == null;
			bool flag4 = flag3;
			if (flag4)
			{
				result = -1;
			}
			else
			{
				Delegate @delegate = fieldInfo.GetValue(obs) as Delegate;
				int num = 0;
				object[] array = new object[1];
				foreach (object obj in items)
				{
					UnityEngine.Object x = obj as UnityEngine.Object;
					bool flag5 = x == null;
					bool flag6 = !flag5;
					if (flag6)
					{
						bool flag7 = @delegate != null && !(bool)@delegate.DynamicInvoke(new object[]
						{
							obj
						});
						bool flag8 = !flag7;
						if (flag8)
						{
							array[0] = obj;
							method.Invoke(obs, array);
							num++;
						}
					}
				}
				result = num;
			}
		}
		return result;
	}

	private static void HudFix(int pass)
	{
		try
		{
			BtcSpectate.ApplyRelations("hud pass " + pass);
			bool flag = BtcSpectate.Val(BtcSpectate.iHud, 1f) > 0.5f;
			bool flag2 = flag;
			if (flag2)
			{
				Labor[] array = UnityEngine.Object.FindObjectsOfType<Labor>();
				SpawnerCristal[] array2 = UnityEngine.Object.FindObjectsOfType<SpawnerCristal>();
				int num = BtcSpectate.ResyncOne(UIGameObserver.bricktrons, array);
				int num2 = BtcSpectate.ResyncOne(UIGameObserver.enemyBricktrons, array);
				int num3 = BtcSpectate.ResyncOne(UIGameObserver.alliedBricktrons, array);
				int num4 = BtcSpectate.ResyncOne(UIGameObserver.crystals, array2);
				BtcSpectate.Log(string.Concat(new object[]
				{
					"client: observers resynced (pass ",
					pass,
					"): bricktrons own ",
					num,
					" enemy ",
					num2,
					" allied ",
					num3,
					", crystals ",
					num4,
					" (of ",
					array.Length,
					" labors, ",
					array2.Length,
					" crystals)"
				}));
				Canvas.ForceUpdateCanvases();
				Canvas[] array3 = UnityEngine.Object.FindObjectsOfType<Canvas>();
				for (int i = 0; i < array3.Length; i++)
				{
					bool flag3 = array3[i] != null && array3[i].isRootCanvas && array3[i].renderMode == RenderMode.ScreenSpaceOverlay;
					bool flag4 = flag3;
					if (flag4)
					{
						LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)array3[i].transform);
					}
				}
			}
			bool flag5 = BtcSpectate.Val(BtcSpectate.iMap, 1f) > 0.5f;
			bool flag6 = flag5;
			if (flag6)
			{
				BtcSpectate.FixMiniMap();
			}
		}
		catch (Exception arg)
		{
			BtcSpectate.Log("HudFix: " + arg);
		}
	}

	private static void FixMiniMap()
	{
		foreach (MiniMap miniMap in Resources.FindObjectsOfTypeAll<MiniMap>())
		{
			bool flag = miniMap == null || !miniMap.gameObject.scene.IsValid();
			bool flag2 = !flag;
			if (flag2)
			{
				List<Transform> list = new List<Transform>();
				StringBuilder stringBuilder = new StringBuilder();
				Transform transform = miniMap.transform;
				while (transform != null)
				{
					bool flag3 = !transform.gameObject.activeSelf;
					bool flag4 = flag3;
					if (flag4)
					{
						list.Add(transform);
					}
					stringBuilder.Insert(0, "/" + transform.name + (transform.gameObject.activeSelf ? string.Empty : "(off)"));
					transform = transform.parent;
				}
				BtcSpectate.Log(string.Concat(new object[]
				{
					"client: minimap ",
					stringBuilder,
					" activeInHierarchy=",
					miniMap.gameObject.activeInHierarchy.ToString(),
					" heightmap=",
					(miniMap.heightmap != null).ToString(),
					" enabled=",
					miniMap.enabled.ToString()
				}));
				bool flag5 = list.Count > 0 && list.Count <= 6;
				bool flag6 = flag5;
				if (flag6)
				{
					for (int j = list.Count - 1; j >= 0; j--)
					{
						list[j].gameObject.SetActive(true);
					}
					BtcSpectate.Log("client: minimap switched on (" + list.Count + " objects)");
				}
				bool flag7 = miniMap.gameObject.activeInHierarchy && !miniMap.enabled;
				bool flag8 = flag7;
				if (flag8)
				{
					miniMap.enabled = true;
				}
				bool flag9 = miniMap.gameObject.activeInHierarchy && miniMap.heightmap == null && GameSignals.Received<Action>(GameSignals.LevelReady);
				bool flag10 = flag9;
				if (flag10)
				{
					MethodInfo method = typeof(MiniMap).GetMethod("Init", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					bool flag11 = method != null;
					bool flag12 = flag11;
					if (flag12)
					{
						method.Invoke(miniMap, null);
						BtcSpectate.Log("client: minimap Init called by hand");
					}
				}
			}
		}
	}

	public const short MsgHello = 78;

	public const short MsgReply = 79;

	public const short MsgChunk = 80;

	public const short MsgAck = 81;

	public const short MsgBanner = 82;

	private const int ChunkSize = 1200;

	private const int Window = 96;

	private const int AckEvery = 16;

	private const int Version = 2;

	private const float HelloTimeout = 8f;

	private const float WaitTimeout = 200f;

	private const float JobTimeout = 150f;

	private const string NetOkText = "Connection OK";

	private static int iAllow = -1;

	private static int iMax = -1;

	private static int iPause = -1;

	private static int iAsk = -1;

	private static BtcSpectate.Job _job;

	private static readonly Queue<BtcSpectate.Job> _queue;

	private static readonly HashSet<int> _spec;

	private static readonly Dictionary<int, string> _names;

	private static readonly List<KeyValuePair<int, float>> _kick;

	private static int _sid;

	private static int _lastFrame;

	private static float _nextPrune;

	private static bool _frozen;

	private static float _savedScale;

	private static volatile int _thread;

	private static bool _bannerOn;

	private static string _bannerName;

	private static float _bannerSince;

	private static string _bannerExtra;

	private static NetworkClient _client;

	private static NetworkMessageDelegate _orig;

	private static string _heldScene;

	private static int _cState;

	private static float _cT0;

	private static int _cSid;

	private static int _cTotal;

	private static int _cGot;

	private static int _cChunks;

	private static FileStream _fs;

	private static string _cZip;

	private static string _cDir;

	private static Asset_Map _pendingMap;

	private static bool _mapApplied;

	private static string _cMsg;

	private static float _cMsgUntil;

	private static GUIStyle _st;

	private static Texture2D _dot;

	private static Texture2D _px;

	private static int _pid;

	private static string _logPath;

	private static long _diagUntil;

	private static bool _logHook;

	private static float _watchUntil;

	private static float _watchNext;

	private static int _noTarget;

	private static bool _hold;

	private static float _holdSince;

	private static float _diagNext;

	private static readonly List<BtcSpectate.HeldMsg> _held;

	private static readonly HashSet<uint> _heldIds;

	private static readonly Dictionary<short, NetworkMessageDelegate> _hOrig;

	private static readonly short[] HoldTypes;

	private static string _userAsset;

	private static string _noiseAsset;

	private static string _factionAsset;

	private static bool _tier2Open;

	private static readonly List<BtcSpectate.HeldMsg> _held2;

	private static readonly HashSet<uint> _heldIds2;

	private static int iRet = -1;

	private static int iRetIp = -1;

	private static readonly HashSet<int> _ret;

	private static readonly Dictionary<string, string> _known;

	private static readonly HashSet<int> _hello;

	private static readonly Dictionary<int, float> _seen;

	private static float _nextCapture;

	private static NetworkMessageDelegate _pauseOrig;

	private static int _bannerKind;

	private static bool _cReturn;

	private static bool _cSpectator;

	private static float _fixNext;

	private static float _probeAt;

	private static float _visErrNext;

	private const float GhostWait = 20f;

	private const float StaleAfter = 3f;

	private static readonly Dictionary<string, string> _keys;

	private static readonly Dictionary<string, BtcSpectate.Slot> _slots;

	private static readonly Dictionary<int, string> _userName;

	private static readonly HashSet<int> _ignore;

	private static readonly List<object> _stash;

	private static float _waitNext;

	private static string _waitText;

	private static int _waitCount;

	private static FieldInfo _hbField;

	private static BtcSpectate.HelloMsg _helloMsg;

	private static float _helloNext;

	private static int _helloTries;

	private static bool _pre;

	private static bool _preKeep;

	private static readonly List<BtcSpectate.HeldMsg> _preQ;

	private static readonly short[] PreTypes;

	private static float _refireAt;

	private static bool _probed2;

	public const short MsgRel = 83;

	private static int iHud = -1;

	private static int iMap = -1;

	private static float _hudAt;

	private static int _hudPass;

	private static uint[] _relA;

	private static uint[] _relB;

	private static byte[] _relR;

	private static MethodInfo _miRefresh;

	private static readonly List<KeyValuePair<float, int>> _relAt;

	public static bool RewindKeep;

	public class SceneStrMsg : MessageBase
	{
		public SceneStrMsg()
		{
		}

		public SceneStrMsg(string v)
		{
			this.value = v;
		}

		public override void Serialize(NetworkWriter w)
		{
			w.Write(this.value ?? string.Empty);
		}

		public override void Deserialize(NetworkReader r)
		{
			this.value = r.ReadString();
		}

		public string value;
	}

	public class HelloMsg : MessageBase
	{
		public override void Serialize(NetworkWriter w)
		{
			w.Write(this.ver);
			w.Write(this.name ?? string.Empty);
			w.Write(this.ing);
			w.Write(this.key ?? string.Empty);
		}

		public override void Deserialize(NetworkReader r)
		{
			this.ver = r.ReadInt32();
			this.name = r.ReadString();
			this.ing = ((r.Position < (uint)r.Length) ? r.ReadInt32() : 0);
			this.key = ((r.Position < (uint)r.Length) ? r.ReadString() : string.Empty);
		}

		public int ver;

		public string name;

		public int ing;

		public string key;
	}

	public class ReplyMsg : MessageBase
	{
		public override void Serialize(NetworkWriter w)
		{
			w.Write(this.code);
			w.Write(this.sid);
			w.Write(this.size);
			w.Write(this.text ?? string.Empty);
		}

		public override void Deserialize(NetworkReader r)
		{
			this.code = r.ReadInt32();
			this.sid = r.ReadInt32();
			this.size = r.ReadInt32();
			this.text = r.ReadString();
		}

		public int code;

		public int sid;

		public int size;

		public string text;
	}

	public class ChunkMsg : MessageBase
	{
		public override void Serialize(NetworkWriter w)
		{
			w.Write(this.sid);
			w.Write(this.idx);
			w.WriteBytesFull(this.data);
		}

		public override void Deserialize(NetworkReader r)
		{
			this.sid = r.ReadInt32();
			this.idx = r.ReadInt32();
			this.data = r.ReadBytesAndSize();
		}

		public int sid;

		public int idx;

		public byte[] data;
	}

	public class AckMsg : MessageBase
	{
		public override void Serialize(NetworkWriter w)
		{
			w.Write(this.sid);
			w.Write(this.next);
		}

		public override void Deserialize(NetworkReader r)
		{
			this.sid = r.ReadInt32();
			this.next = r.ReadInt32();
		}

		public int sid;

		public int next;
	}

	public class BannerMsg : MessageBase
	{
		public override void Serialize(NetworkWriter w)
		{
			w.Write(this.on);
			w.Write(this.name ?? string.Empty);
		}

		public override void Deserialize(NetworkReader r)
		{
			this.on = r.ReadInt32();
			this.name = r.ReadString();
		}

		public int on;

		public string name;
	}

	private class Job
	{
		public NetworkConnection conn;

		public string name;

		public int sid;

		public int stage;

		public float t0;

		public int frames;

		public byte[] data;

		public int chunks;

		public int sent;

		public int acked;

		public bool ready;

		public bool askYes;

		public bool askDone;

		public Asset_Map shell;

		public string zipPath;

		public bool ret;

		public BtcSpectate.Slot slot;

		public NetworkConnection oldConn;
	}

	public class ConnState
	{
		public int mc;

		public int mp;

		public string scene;
	}

	private class HeldMsg
	{
		public short t;

		public byte[] raw;

		public int ch;
	}

	private class Slot
	{
		public string name;

		public Faction faction;

		public int fnum;

		public int team;

		public User user;
	}

	public class RelMsg : MessageBase
	{
		public override void Serialize(NetworkWriter w)
		{
			w.WritePackedUInt32((uint)this.a.Length);
			for (int i = 0; i < this.a.Length; i++)
			{
				w.WritePackedUInt32(this.a[i]);
				w.WritePackedUInt32(this.b[i]);
				w.Write(this.r[i]);
			}
		}

		public override void Deserialize(NetworkReader rd)
		{
			int num = (int)rd.ReadPackedUInt32();
			this.a = new uint[num];
			this.b = new uint[num];
			this.r = new byte[num];
			for (int i = 0; i < num; i++)
			{
				this.a[i] = rd.ReadPackedUInt32();
				this.b[i] = rd.ReadPackedUInt32();
				this.r[i] = rd.ReadByte();
			}
		}

		public uint[] a = new uint[0];

		public uint[] b = new uint[0];

		public byte[] r = new byte[0];
	}
}
}
