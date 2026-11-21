using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Brix.Engine;
using Brix.Game;
using Brix.Input;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
public static class BtcDiscord
{
	public static bool Enabled
	{
		get
		{
			return BtcDiscord.enabled;
		}
	}

	public static string ClientId
	{
		get
		{
			BtcDiscord.LoadConfig();
			return BtcDiscord.clientId;
		}
	}

	public static void SetEnabled(bool on)
	{
		bool flag = on == BtcDiscord.enabled;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcDiscord.enabled = on;
			if (on)
			{
				BtcDiscord.LoadConfig();
				BtcDiscord.Start();
			}
			else
			{
				BtcDiscord.Stop();
			}
		}
	}

	private static void LoadConfig()
	{
		bool flag = !BtcDiscord.cfgLoaded;
		if (flag)
		{
			BtcDiscord.cfgLoaded = true;
			BtcDiscord.clientId = "1557630400908632104";
			BtcDiscord.largeImage = "logo";
			BtcDiscord.largeText = "Better Than Castle";
			BtcDiscord.showMap = true;
			BtcDiscord.showPlayers = true;
			BtcDiscord.showCrystals = true;
			BtcDiscord.showTimer = true;
			BtcDiscord.hideMap = false;
			BtcDiscord.hideNick = false;
			BtcDiscord.mapUpload = true;
			BtcDiscord.showLobbyNames = true;
			BtcDiscord.tplDetails = "{mode} • {map}";
			BtcDiscord.tplState = "{fp} • Crystals {crystals}";
			BtcDiscord.aboutLabel = "About Mod";
			BtcDiscord.aboutUrl = "https://github.com/NNLEKKK/BetterThanCastle";
		}
	}

	private static void Log(string msg)
	{
		try
		{
			File.AppendAllText(BtcPaths.DebugFile("discord_log.txt"), DateTime.Now.ToString("HH:mm:ss") + "  " + msg + "\r\n");
		}
		catch (Exception)
		{
		}
	}

	public static void Poll()
	{
		bool flag = !BtcDiscord.enabled;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			Scene activeScene = SceneManager.GetActiveScene();
			BtcDiscord.EnsureHooks(activeScene);
			BtcDiscord.UpdateUpload();
			bool flag6 = Time.unscaledTime < BtcDiscord.nextPoll;
			bool flag7 = !flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			if (flag10)
			{
				BtcDiscord.nextPoll = Time.unscaledTime + 2f;
				try
				{
					BtcDiscord.Activity activity = new BtcDiscord.Activity();
					UNetManager instance = UNetManager.instance;
					string text = activeScene.name ?? string.Empty;
					bool multiplayer = Neo.multiplayer;
					List<LobbyPlayer> list = (multiplayer && instance != null) ? instance.LobbyPlayerList : null;
					string text2 = null;
					string text3 = null;
					string mapPath = null;
					string text4 = null;
					try
					{
						bool flag11 = GameParam.instance != null;
						bool flag12 = flag11;
						bool flag13 = flag12;
						bool flag14 = flag13;
						bool flag15 = flag14;
						if (flag15)
						{
							bool flag16 = GameParam.Map != null;
							bool flag17 = flag16;
							bool flag18 = flag17;
							bool flag19 = flag18;
							bool flag20 = flag19;
							if (flag20)
							{
								text2 = GameParam.Map.displayName;
								text3 = GameParam.Map.codename;
								mapPath = GameParam.Map.path;
							}
							bool flag21 = GameParam.Gamemode != null;
							bool flag22 = flag21;
							bool flag23 = flag22;
							bool flag24 = flag23;
							bool flag25 = flag24;
							if (flag25)
							{
								text4 = GameParam.Gamemode.displayName;
							}
						}
					}
					catch (Exception)
					{
					}
					bool flag26 = activeScene.buildIndex == 2;
					LobbyPlayer lobbyPlayer = null;
					List<BtcDiscord.PlayerInfo> list2 = new List<BtcDiscord.PlayerInfo>();
					int num = 0;
					bool flag27 = multiplayer && instance != null;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					if (flag30)
					{
						num = instance.maxConnections;
						bool flag31 = list != null;
						bool flag32 = flag31;
						bool flag33 = flag32;
						bool flag34 = flag33;
						if (flag34)
						{
							for (int i = 0; i < list.Count; i++)
							{
								LobbyPlayer lobbyPlayer2 = list[i];
								bool flag35 = lobbyPlayer2 == null;
								bool flag36 = !flag35;
								bool flag37 = flag36;
								bool flag38 = flag37;
								if (flag38)
								{
									list2.Add(new BtcDiscord.PlayerInfo
									{
										name = BtcNick.Plain(lobbyPlayer2.NetworkplayerName),
										team = lobbyPlayer2.NetworkTeamNumber,
										color = BtcDiscord.ColorFromFactionNumber(lobbyPlayer2.NetworkfactionNumber),
										local = lobbyPlayer2.isLocalPlayer
									});
									bool isLocalPlayer = lobbyPlayer2.isLocalPlayer;
									bool flag39 = isLocalPlayer;
									bool flag40 = flag39;
									bool flag41 = flag40;
									if (flag41)
									{
										lobbyPlayer = lobbyPlayer2;
									}
								}
							}
						}
						bool flag42 = list2.Count > 0;
						bool flag43 = flag42;
						bool flag44 = flag43;
						bool flag45 = flag44;
						if (flag45)
						{
							BtcDiscord.snapPlayers = list2;
							BtcDiscord.snapMax = num;
						}
						else
						{
							bool flag46 = flag26;
							bool flag47 = flag46;
							bool flag48 = flag47;
							bool flag49 = flag48;
							if (flag49)
							{
								list2 = BtcDiscord.ScanUsers();
								bool flag50 = list2.Count == 0 && BtcDiscord.snapPlayers != null;
								bool flag51 = flag50;
								bool flag52 = flag51;
								bool flag53 = flag52;
								if (flag53)
								{
									list2 = BtcDiscord.snapPlayers;
								}
								bool flag54 = num <= 0;
								bool flag55 = flag54;
								bool flag56 = flag55;
								bool flag57 = flag56;
								if (flag57)
								{
									num = BtcDiscord.snapMax;
								}
							}
						}
					}
					else
					{
						BtcDiscord.snapPlayers = null;
						BtcDiscord.snapMax = 0;
					}
					bool flag58 = !flag26;
					bool flag59 = flag58;
					bool flag60 = flag59;
					bool flag61 = flag60;
					if (flag61)
					{
						BtcDiscord.usersCache = null;
					}
					int count = list2.Count;
					BtcDiscord.PlayerInfo playerInfo = null;
					for (int j = 0; j < list2.Count; j++)
					{
						bool local = list2[j].local;
						bool flag62 = local;
						bool flag63 = flag62;
						bool flag64 = flag63;
						if (flag64)
						{
							playerInfo = list2[j];
						}
					}
					List<int> order = BtcDiscord.BuildTeamOrder(list2);
					string text5 = BtcDiscord.TeamNames(list2, order);
					bool flag65 = text5.Length > 90;
					bool flag66 = flag65;
					bool flag67 = flag66;
					bool flag68 = flag67;
					if (flag68)
					{
						text5 = text5.Substring(0, 87) + "...";
					}
					List<BtcDiscord.PlayerInfo> list3 = list2;
					bool flag69 = BtcReplayBoost.IsReplay();
					bool flag70 = flag69;
					bool flag71 = flag70;
					if (flag71)
					{
						list3 = new List<BtcDiscord.PlayerInfo>();
						for (int k = 0; k < list2.Count; k++)
						{
							bool flag72 = !list2[k].spec;
							bool flag73 = flag72;
							bool flag74 = flag73;
							if (flag74)
							{
								list3.Add(list2[k]);
							}
						}
					}
					string text6 = BtcDiscord.TeamFormat(list3);
					string text7 = multiplayer ? (((lobbyPlayer != null && lobbyPlayer.NetworkisHost) || NetworkServer.active) ? "Host" : "Client") : "Singleplayer";
					string text8 = (lobbyPlayer != null && !lobbyPlayer.NetworkisHost && !NetworkServer.active) ? (lobbyPlayer.Networkping.ToString() + " ms") : string.Empty;
					string value = (playerInfo != null) ? playerInfo.name : string.Empty;
					bool flag75 = flag26 && BtcDiscord.IsLevelReady();
					bool flag76 = (flag26 && !flag75) || text.ToLowerInvariant().Contains("load");
					bool flag77 = !flag76 && !flag26 && multiplayer && instance != null;
					bool flag78 = flag77;
					bool flag79 = flag78;
					bool flag80 = flag79;
					bool flag81 = flag80;
					if (flag81)
					{
						flag76 = BtcBc.IsLoadingLevel(instance);
					}
					bool flag82 = false;
					bool flag83 = false;
					bool flag84 = false;
					try
					{
						Faction faction = (User.LocalUser != null) ? User.LocalUser.faction : null;
						flag84 = (faction != null && faction.IsSpectator);
					}
					catch (Exception)
					{
					}
					bool flag85 = flag26 && !flag76;
					bool flag86 = flag85;
					bool flag87 = flag86;
					bool flag88 = flag87;
					bool flag89 = flag88;
					if (flag89)
					{
						try
						{
							flag82 = (Time.timeScale <= 0.0001f);
							bool flag90 = flag82 != BtcDiscord.lastPaused;
							bool flag91 = flag90;
							bool flag92 = flag91;
							bool flag93 = flag92;
							if (flag93)
							{
								BtcDiscord.lastPaused = flag82;
								BtcDiscord.Log(string.Concat(new string[]
								{
									"Pause=",
									flag82.ToString(),
									" (timeScale=",
									Time.timeScale.ToString("0.###"),
									", Journee.CurrentPaused=",
									Journee.CurrentPaused.ToString(),
									")"
								}));
							}
						}
						catch (Exception)
						{
						}
						try
						{
							flag83 = (InputModeController.DefaultMode == InputMode.worldEditor);
						}
						catch (Exception)
						{
						}
					}
					string value2 = string.Empty;
					bool flag94 = flag26 && !flag76 && BtcDiscord.showCrystals;
					bool flag95 = flag94;
					bool flag96 = flag95;
					bool flag97 = flag96;
					bool flag98 = flag97;
					if (flag98)
					{
						value2 = BtcDiscord.CrystalScore(order);
					}
					bool flag99 = BtcDiscord.hideMap;
					bool flag100 = flag99;
					bool flag101 = flag100;
					bool flag102 = flag101;
					bool flag103 = flag102;
					if (flag103)
					{
						text2 = null;
						text3 = null;
						mapPath = null;
					}
					bool flag104 = BtcDiscord.hideNick;
					bool flag105 = flag104;
					bool flag106 = flag105;
					bool flag107 = flag106;
					bool flag108 = flag107;
					if (flag108)
					{
						value = string.Empty;
						text5 = string.Empty;
					}
					float num2 = Time.timeSinceLevelLoad;
					bool flag109 = BtcDiscord.readyAt >= 0f;
					bool flag110 = flag109;
					bool flag111 = flag110;
					bool flag112 = flag111;
					bool flag113 = flag112;
					if (flag113)
					{
						num2 = Math.Max(0f, Time.timeSinceLevelLoad - BtcDiscord.readyAt);
					}
					int num3 = (int)num2;
					string phaseText = null;
					bool hideClock = false;
					float advElapsed;
					if (BtcTimer.DiscordInfo(out advElapsed, out phaseText))
					{
						if (advElapsed >= 0f) num3 = (int)advElapsed;
						else hideClock = true;
					}
					int num4 = BtcDiscord.LoadPercent();
					Dictionary<string, string> dictionary = new Dictionary<string, string>();
					dictionary["map"] = (text2 ?? string.Empty);
					dictionary["mode"] = (text4 ?? string.Empty);
					dictionary["format"] = text6;
					dictionary["players"] = count.ToString();
					dictionary["max"] = Math.Max(count, num).ToString();
					dictionary["nick"] = value;
					dictionary["names"] = text5;
					dictionary["ping"] = text8;
					dictionary["role"] = text7;
					dictionary["crystals"] = value2;
					dictionary["time"] = BtcDiscord.Fmt(num3);
					dictionary["percent"] = ((num4 >= 0) ? (num4.ToString() + "%") : string.Empty);
					string value3 = text6;
					bool flag114 = multiplayer && BtcDiscord.showPlayers && count > 0;
					bool flag115 = flag114;
					bool flag116 = flag115;
					bool flag117 = flag116;
					if (flag117)
					{
						value3 = ((text6.Length > 0) ? (text6 + " ") : string.Empty) + count.ToString() + "/" + Math.Max(count, num).ToString();
					}
					dictionary["fp"] = value3;
					string text9 = BtcDiscord.BcLabel();
					dictionary["bc"] = text9;
					string text10 = (text5.Length > 0) ? ("Players: " + text5) : string.Empty;
					bool flag118 = flag76;
					bool flag119 = flag118;
					bool flag120 = flag119;
					bool flag121 = flag120;
					bool flag122 = flag121;
					string text11;
					if (flag122)
					{
						text11 = "loading";
						activity.details = (string.IsNullOrEmpty(text2) ? "Loading..." : ("Loading " + text2));
						string text12 = (num4 < 0) ? "Please wait" : ((num4 >= 100) ? "Entering world" : (num4.ToString() + "%"));
						activity.state = (string.IsNullOrEmpty(text4) ? text12 : (text12 + " • " + text4));
						activity.smallImage = "loading";
						activity.smallText = ((num4 >= 0) ? ("Loading " + num4.ToString() + "%") : "Loading");
						activity.fast = true;
					}
					else
					{
						bool flag123 = flag26 && flag83;
						bool flag124 = flag123;
						bool flag125 = flag124;
						bool flag126 = flag125;
						bool flag127 = flag126;
						if (flag127)
						{
							text11 = "editor";
							activity.details = "In the map editor";
							activity.state = (string.IsNullOrEmpty(text2) ? "Building" : text2);
							activity.smallImage = "editor";
							activity.smallText = "Editor";
						}
						else
						{
							bool flag128 = flag26;
							bool flag129 = flag128;
							bool flag130 = flag129;
							bool flag131 = flag130;
							bool flag132 = flag131;
							if (flag132)
							{
								bool flag133 = BtcDiscord.result != 0;
								bool flag134 = flag133;
								bool flag135 = flag134;
								bool flag136 = flag135;
								bool flag137 = flag136;
								string tpl;
								string text13;
								if (flag137)
								{
									text11 = ((BtcDiscord.result > 0) ? "victory" : "defeat");
									activity.smallImage = text11;
									activity.smallText = ((BtcDiscord.result > 0) ? "Victory" : "Defeat");
									tpl = ((BtcDiscord.result > 0) ? "Victory • {mode} • {map}" : "Defeat • {mode} • {map}");
									bool flag138 = BtcDiscord.result > 0;
									bool flag139 = flag138;
									bool flag140 = flag139;
									bool flag141 = flag140;
									if (flag141)
									{
										text13 = "{fp} • Won the game";
									}
									else
									{
										text13 = (flag84 ? "{fp} • Lost the game • Spectating" : "{fp} • Lost the game");
									}
								}
								else
								{
									bool flag142 = flag82;
									bool flag143 = flag142;
									bool flag144 = flag143;
									bool flag145 = flag144;
									bool flag146 = flag145;
									if (flag146)
									{
										text11 = "paused";
										activity.smallImage = "pause";
										activity.smallText = "Paused";
										tpl = BtcDiscord.tplDetails;
										text13 = "Paused • {fp}";
									}
									else
									{
										text11 = (multiplayer ? "game_mp" : "game_sp");
										string text14 = flag84 ? "spectator" : BtcDiscord.LocalColorKey();
										bool flag147 = text14 == null && playerInfo != null;
										bool flag148 = flag147;
										bool flag149 = flag148;
										bool flag150 = flag149;
										if (flag150)
										{
											text14 = playerInfo.color;
										}
										activity.smallImage = ((text14 != null) ? text14 : (multiplayer ? "multiplayer" : "singleplayer"));
										string text15 = (text10.Length > 0) ? text10 : (multiplayer ? text7 : "Singleplayer");
										bool flag151 = text8.Length > 0;
										bool flag152 = flag151;
										bool flag153 = flag152;
										bool flag154 = flag153;
										bool flag155 = flag154;
										if (flag155)
										{
											text15 = text15 + " • " + text8;
										}
										activity.smallText = text15;
										tpl = BtcDiscord.tplDetails;
										text13 = BtcDiscord.tplState;
										bool flag156 = multiplayer && text13.IndexOf("{players}", StringComparison.Ordinal) < 0;
										bool flag157 = flag156;
										bool flag158 = flag157;
										bool flag159 = flag158;
										if (flag159)
										{
											text13 = text13.Replace("{format}", "{fp}");
										}
										bool flag160 = flag84;
										bool flag161 = flag160;
										bool flag162 = flag161;
										bool flag163 = flag162;
										if (flag163)
										{
											text13 = "Spectating • " + text13;
										}
									}
								}
								activity.details = BtcDiscord.Fill(tpl, dictionary).Replace("•  •", "•");
								activity.state = BtcDiscord.Fill(text13, dictionary);
								if (phaseText != null && BtcDiscord.result == 0)
								{
									activity.state = phaseText + " • " + activity.state;
								}
								bool flag164 = BtcReplayBoost.IsReplay();
								bool flag165 = flag164;
								bool flag166 = flag165;
								if (flag166)
								{
									string details;
									string state;
									string smallImage;
									string smallText;
									BtcReplayBoost.DiscordTexts(text2, text6, out details, out state, out smallImage, out smallText);
									activity.details = details;
									activity.state = state;
									activity.smallImage = smallImage;
									activity.smallText = smallText;
								}
							}
							else
							{
								BtcDiscord.result = 0;
								bool flag167 = instance != null && multiplayer && text == instance.lobbyScene;
								bool flag168 = flag167;
								bool flag169 = flag168;
								bool flag170 = flag169;
								bool flag171 = flag170;
								bool flag172 = flag171;
								if (flag172)
								{
									text11 = "lobby";
									activity.details = ((text7 == "Host") ? "Hosting Lobby" : "In Lobby") + " • " + text9;
									activity.state = BtcDiscord.Fill(BtcDiscord.showLobbyNames ? "{fp} • {names}" : "{fp}", dictionary);
									string text16 = (playerInfo != null) ? playerInfo.color : null;
									activity.smallImage = ((text16 != null) ? text16 : "lobby");
									activity.smallText = ((text10.Length > 0) ? text10 : text7);
								}
								else
								{
									text11 = "menu";
									activity.details = "In the main menu";
									activity.state = "Better Than Castle";
								}
							}
						}
					}
					BtcDiscord.lastStateId = text11;
					bool flag173 = text11 == "game_mp" || text11 == "game_sp" || text11 == "editor";
					bool flag174 = flag173;
					bool flag175 = flag174;
					bool flag176 = flag175;
					bool flag177 = flag176;
					bool flag178 = flag177;
					if (flag178)
					{
						long num5 = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
						long num6 = num5 - (long)num3;
						bool flag179 = BtcDiscord.timerAnchor == 0L || Math.Abs(BtcDiscord.timerAnchor - num6) > 3L;
						bool flag180 = flag179;
						bool flag181 = flag180;
						bool flag182 = flag181;
						bool flag183 = flag182;
						if (flag183)
						{
							BtcDiscord.timerAnchor = num6;
						}
						activity.start = ((BtcDiscord.showTimer && !hideClock) ? BtcDiscord.timerAnchor : 0L);
					}
					else
					{
						BtcDiscord.timerAnchor = 0L;
						bool flag184 = text11 == "loading" || text11 == "lobby";
						bool flag185 = flag184;
						bool flag186 = flag185;
						bool flag187 = flag186;
						bool flag188 = flag187;
						if (flag188)
						{
							bool flag189 = BtcDiscord.stateStartId != text11;
							bool flag190 = flag189;
							bool flag191 = flag190;
							bool flag192 = flag191;
							bool flag193 = flag192;
							if (flag193)
							{
								BtcDiscord.stateStartId = text11;
								BtcDiscord.stateStart = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
							}
							activity.start = BtcDiscord.stateStart;
						}
						else
						{
							activity.start = 0L;
						}
					}
					bool flag194 = text11 == "menu";
					bool flag195 = flag194;
					bool flag196 = flag195;
					bool flag197 = flag196;
					bool flag198 = flag197;
					if (flag198)
					{
						BtcDiscord.stateStartId = null;
					}
					activity.largeImage = ((text11 != "menu" && !string.IsNullOrEmpty(text3)) ? BtcDiscord.MapKey(text3, mapPath) : BtcDiscord.largeImage);
					activity.largeText = ((text11 != "menu" && !string.IsNullOrEmpty(text2)) ? text2 : BtcDiscord.largeText);
					bool flag199 = text11 == "lobby" && !string.IsNullOrEmpty(text2) && !string.IsNullOrEmpty(text4);
					bool flag200 = flag199;
					bool flag201 = flag200;
					bool flag202 = flag201;
					if (flag202)
					{
						activity.largeText = text2 + " • " + text4;
					}
					bool flag203 = string.IsNullOrEmpty(activity.largeText);
					bool flag204 = flag203;
					bool flag205 = flag204;
					bool flag206 = flag205;
					bool flag207 = flag206;
					if (flag207)
					{
						activity.largeText = BtcDiscord.largeText;
					}
					object obj = BtcDiscord.gate;
					object obj2 = obj;
					object obj3 = obj2;
					object obj4 = obj3;
					object obj5 = obj4;
					lock (obj5)
					{
						BtcDiscord.pending = activity;
						BtcDiscord.pendingKey = activity.Key();
					}
				}
				catch (Exception ex)
				{
					BtcDiscord.Log("Poll failed: " + ex.Message);
				}
			}
		}
	}

	private static void Start()
	{
		bool flag = BtcDiscord.running;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			bool flag8 = string.IsNullOrEmpty(BtcDiscord.clientId);
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				BtcDiscord.Log("ClientId is empty in configs/discord.txt - Rich Presence will not connect.");
			}
			BtcDiscord.running = true;
			BtcDiscord.sentKey = null;
			BtcDiscord.nextPoll = 0f;
			BtcDiscord.worker = new Thread(new ThreadStart(BtcDiscord.WorkerLoop));
			BtcDiscord.worker.IsBackground = true;
			BtcDiscord.worker.Name = "BTC-Discord";
			BtcDiscord.worker.Start();
		}
	}

	private static void Stop()
	{
		BtcDiscord.running = false;
	}

	private static void WorkerLoop()
	{
		IntPtr intPtr = BtcDiscord.INVALID;
		DateTime t = DateTime.MinValue;
		DateTime dateTime = DateTime.MinValue;
		try
		{
			while (BtcDiscord.running)
			{
				bool flag = intPtr == BtcDiscord.INVALID;
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				bool flag7 = flag6;
				if (flag7)
				{
					bool flag8 = !string.IsNullOrEmpty(BtcDiscord.clientId) && DateTime.UtcNow >= t;
					bool flag9 = flag8;
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					if (flag14)
					{
						t = DateTime.UtcNow.AddSeconds(15.0);
						intPtr = BtcDiscord.Connect();
						BtcDiscord.sentKey = null;
					}
				}
				else
				{
					bool flag15 = !BtcDiscord.Drain(intPtr);
					bool flag16 = flag15;
					bool flag17 = flag16;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					if (flag21)
					{
						BtcDiscord.Close(ref intPtr);
						BtcDiscord.sentKey = null;
					}
					else
					{
						object obj = BtcDiscord.gate;
						object obj2 = obj;
						object obj3 = obj2;
						object obj4 = obj3;
						object obj5 = obj4;
						object obj6 = obj5;
						object obj7 = obj6;
						BtcDiscord.Activity activity;
						string text;
						lock (obj7)
						{
							activity = BtcDiscord.pending;
							text = BtcDiscord.pendingKey;
						}
						bool flag22 = activity != null && text != BtcDiscord.sentKey && DateTime.UtcNow >= dateTime.AddSeconds(activity.fast ? 4.0 : 15.0);
						bool flag23 = flag22;
						bool flag24 = flag23;
						bool flag25 = flag24;
						bool flag26 = flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						if (flag28)
						{
							bool flag29 = BtcDiscord.SetActivity(intPtr, activity);
							bool flag30 = flag29;
							bool flag31 = flag30;
							bool flag32 = flag31;
							bool flag33 = flag32;
							bool flag34 = flag33;
							bool flag35 = flag34;
							if (flag35)
							{
								BtcDiscord.sentKey = text;
								dateTime = DateTime.UtcNow;
								BtcDiscord.Log("Sent: " + text);
							}
							else
							{
								BtcDiscord.Close(ref intPtr);
								BtcDiscord.sentKey = null;
							}
						}
					}
				}
				Thread.Sleep(500);
			}
			bool flag36 = intPtr != BtcDiscord.INVALID;
			bool flag37 = flag36;
			bool flag38 = flag37;
			bool flag39 = flag38;
			bool flag40 = flag39;
			bool flag41 = flag40;
			bool flag42 = flag41;
			if (flag42)
			{
				BtcDiscord.SetActivity(intPtr, null);
				Thread.Sleep(150);
			}
		}
		catch (DllNotFoundException)
		{
			BtcDiscord.Log("kernel32 not available (non-Windows?) - Discord Rich Presence disabled.");
		}
		catch (EntryPointNotFoundException)
		{
			BtcDiscord.Log("kernel32 not available (non-Windows?) - Discord Rich Presence disabled.");
		}
		catch (Exception ex)
		{
			BtcDiscord.Log("Worker crashed: " + ex.Message);
		}
		finally
		{
			BtcDiscord.Close(ref intPtr);
			BtcDiscord.running = false;
		}
	}

	private static IntPtr Connect()
	{
		for (int i = 0; i < 10; i++)
		{
			IntPtr intPtr = BtcDiscord.CreateFile("\\\\.\\pipe\\discord-ipc-" + i, 3221225472U, 0U, IntPtr.Zero, 3U, 0U, IntPtr.Zero);
			bool flag = intPtr == BtcDiscord.INVALID;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				bool flag8 = !BtcDiscord.WriteFrame(intPtr, 0, "{\"v\":1,\"client_id\":\"" + BtcDiscord.Esc(BtcDiscord.clientId) + "\"}");
				bool flag9 = flag8;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				if (flag14)
				{
					BtcDiscord.CloseHandle(intPtr);
				}
				else
				{
					Thread.Sleep(300);
					bool flag15 = !BtcDiscord.Drain(intPtr);
					bool flag16 = !flag15;
					bool flag17 = flag16;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					if (flag21)
					{
						BtcDiscord.Log("Connected to Discord (pipe " + i + ").");
						return intPtr;
					}
					BtcDiscord.CloseHandle(intPtr);
				}
			}
		}
		return BtcDiscord.INVALID;
	}

	private static void Close(ref IntPtr h)
	{
		bool flag = h != BtcDiscord.INVALID;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			try
			{
				BtcDiscord.CloseHandle(h);
			}
			catch (Exception)
			{
			}
			h = BtcDiscord.INVALID;
		}
	}

	private static bool SetActivity(IntPtr h, BtcDiscord.Activity a)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("{\"cmd\":\"SET_ACTIVITY\",\"nonce\":\"").Append(Guid.NewGuid().ToString("N")).Append("\",\"args\":{\"pid\":").Append(Process.GetCurrentProcess().Id);
		bool flag = a != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			stringBuilder.Append(",\"activity\":{");
			bool flag6 = true;
			flag6 = BtcDiscord.AddText(stringBuilder, "details", a.details, flag6, 2, 128);
			flag6 = BtcDiscord.AddText(stringBuilder, "state", a.state, flag6, 2, 128);
			bool flag7 = a.start > 0L;
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			if (flag11)
			{
				bool flag12 = !flag6;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					stringBuilder.Append(',');
				}
				flag6 = false;
				stringBuilder.Append("\"timestamps\":{\"start\":").Append(a.start).Append("}");
			}
			StringBuilder stringBuilder2 = new StringBuilder();
			bool flag17 = true;
			bool flag18 = !string.IsNullOrEmpty(a.largeImage);
			bool flag19 = !string.IsNullOrEmpty(a.smallImage);
			flag17 = BtcDiscord.AddText(stringBuilder2, "large_image", a.largeImage, flag17, 1, 256);
			bool flag20 = flag18;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				flag17 = BtcDiscord.AddText(stringBuilder2, "large_text", a.largeText, flag17, 2, 128);
			}
			flag17 = BtcDiscord.AddText(stringBuilder2, "small_image", a.smallImage, flag17, 1, 256);
			bool flag25 = flag19;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			if (flag29)
			{
				flag17 = BtcDiscord.AddText(stringBuilder2, "small_text", a.smallText, flag17, 2, 128);
			}
			bool flag30 = !flag17;
			bool flag31 = flag30;
			bool flag32 = flag31;
			bool flag33 = flag32;
			bool flag34 = flag33;
			if (flag34)
			{
				bool flag35 = !flag6;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				if (flag39)
				{
					stringBuilder.Append(',');
				}
				flag6 = false;
				stringBuilder.Append("\"assets\":{").Append(stringBuilder2.ToString()).Append("}");
			}
			string text = BtcDiscord.aboutUrl;
			bool flag40 = !string.IsNullOrEmpty(text) && (text.StartsWith("https://") || text.StartsWith("http://")) && text.Length <= 512;
			bool flag41 = flag40;
			bool flag42 = flag41;
			bool flag43 = flag42;
			if (flag43)
			{
				string text2 = string.IsNullOrEmpty(BtcDiscord.aboutLabel) ? "About Mod" : BtcDiscord.aboutLabel;
				bool flag44 = text2.Length > 32;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				if (flag47)
				{
					text2 = text2.Substring(0, 32);
				}
				bool flag48 = !flag6;
				bool flag49 = flag48;
				bool flag50 = flag49;
				bool flag51 = flag50;
				if (flag51)
				{
					stringBuilder.Append(',');
				}
				flag6 = false;
				stringBuilder.Append("\"buttons\":[{\"label\":\"").Append(BtcDiscord.Esc(text2)).Append("\",\"url\":\"").Append(BtcDiscord.Esc(text)).Append("\"}]");
			}
			bool flag52 = a.partySize > 0;
			bool flag53 = flag52;
			bool flag54 = flag53;
			bool flag55 = flag54;
			bool flag56 = flag55;
			if (flag56)
			{
				bool flag57 = !flag6;
				bool flag58 = flag57;
				bool flag59 = flag58;
				bool flag60 = flag59;
				bool flag61 = flag60;
				if (flag61)
				{
					stringBuilder.Append(',');
				}
				stringBuilder.Append("\"party\":{\"id\":\"btc-party\",\"size\":[").Append(a.partySize).Append(",").Append(a.partyMax).Append("]}");
			}
			stringBuilder.Append("}");
		}
		stringBuilder.Append("}}");
		return BtcDiscord.WriteFrame(h, 1, stringBuilder.ToString());
	}

	private static bool WriteFrame(IntPtr h, int opcode, string json)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		byte[] array = new byte[8 + bytes.Length];
		BitConverter.GetBytes(opcode).CopyTo(array, 0);
		BitConverter.GetBytes(bytes.Length).CopyTo(array, 4);
		Array.Copy(bytes, 0, array, 8, bytes.Length);
		uint num;
		return BtcDiscord.WriteFile(h, array, (uint)array.Length, out num, IntPtr.Zero) && num == (uint)array.Length;
	}

	private static bool Drain(IntPtr h)
	{
		byte[] array = new byte[4096];
		int i = 0;
		while (i < 32)
		{
			uint num;
			uint num2;
			uint num3;
			bool flag = !BtcDiscord.PeekNamedPipe(h, null, 0U, out num, out num2, out num3);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6;
			if (flag5)
			{
				flag6 = false;
			}
			else
			{
				bool flag7 = num2 == 0U;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				bool flag11 = flag10;
				if (flag11)
				{
					flag6 = true;
				}
				else
				{
					uint count = Math.Min(num2, (uint)array.Length);
					bool flag12 = !BtcDiscord.ReadFile(h, array, count, out num, IntPtr.Zero);
					bool flag13 = !flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						try
						{
							bool flag17 = num > 8U;
							bool flag18 = flag17;
							bool flag19 = flag18;
							bool flag20 = flag19;
							bool flag21 = flag20;
							if (flag21)
							{
								string @string = Encoding.UTF8.GetString(array, 0, (int)num);
								bool flag22 = @string.IndexOf("\"evt\":\"ERROR\"", StringComparison.Ordinal) >= 0 || (@string.IndexOf("\"code\":", StringComparison.Ordinal) >= 0 && @string.IndexOf("\"message\"", StringComparison.Ordinal) >= 0);
								bool flag23 = flag22;
								bool flag24 = flag23;
								bool flag25 = flag24;
								bool flag26 = flag25;
								if (flag26)
								{
									BtcDiscord.Log("Discord replied: " + @string.Substring(Math.Min(8, @string.Length)));
								}
							}
						}
						catch (Exception)
						{
						}
						i++;
						continue;
					}
					flag6 = false;
				}
			}
			return flag6;
		}
		return true;
	}

	private static string Esc(string s)
	{
		bool flag = string.IsNullOrEmpty(s);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		string text;
		if (flag7)
		{
			text = string.Empty;
		}
		else
		{
			StringBuilder stringBuilder = new StringBuilder(s.Length + 8);
			int i = 0;
			while (i < s.Length)
			{
				char c = s[i];
				char c2 = c;
				switch (c2)
				{
				case '\t':
					stringBuilder.Append("\\t");
					break;
				case '\n':
					stringBuilder.Append("\\n");
					break;
				case '\v':
				case '\f':
					goto IL_123;
				case '\r':
					stringBuilder.Append("\\r");
					break;
				default:
				{
					bool flag8 = c2 != '"';
					bool flag9 = flag8;
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					if (flag13)
					{
						bool flag14 = c2 != '\\';
						bool flag15 = flag14;
						bool flag16 = flag15;
						bool flag17 = flag16;
						bool flag18 = flag17;
						bool flag19 = flag18;
						if (flag19)
						{
							goto IL_123;
						}
						stringBuilder.Append("\\\\");
					}
					else
					{
						stringBuilder.Append("\\\"");
					}
					break;
				}
				}
				IL_11A:
				i++;
				continue;
				IL_123:
				bool flag20 = c < ' ';
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				bool flag25 = flag24;
				bool flag26 = flag25;
				if (flag26)
				{
					StringBuilder stringBuilder2 = stringBuilder.Append("\\u");
					int num = (int)c;
					stringBuilder2.Append(num.ToString("x4"));
				}
				else
				{
					stringBuilder.Append(c);
				}
				goto IL_11A;
			}
			text = stringBuilder.ToString();
		}
		return text;
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool WriteFile(IntPtr h, byte[] buf, uint count, out uint written, IntPtr overlapped);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool ReadFile(IntPtr h, byte[] buf, uint count, out uint read, IntPtr overlapped);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool PeekNamedPipe(IntPtr h, byte[] buf, uint size, out uint read, out uint avail, out uint left);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(IntPtr h);

	static BtcDiscord()
	{
		BtcDiscord.hookedScene = -1;
		BtcDiscord.hookedAtLevelTime = float.MaxValue;
		BtcDiscord.readyAt = -1f;
		BtcDiscord.crystalCache = string.Empty;
		BtcDiscord.urlCache = new Dictionary<string, string>();
		BtcDiscord.uploadFails = new Dictionary<string, int>();
		BtcDiscord.lastStateId = string.Empty;
		BtcDiscord.INVALID = new IntPtr(-1);
	}

	public static void MarkResult(bool victory)
	{
		BtcDiscord.result = (victory ? 1 : -1);
	}

	private static string Fill(string tpl, Dictionary<string, string> v)
	{
		foreach (KeyValuePair<string, string> keyValuePair in v)
		{
			tpl = tpl.Replace("{" + keyValuePair.Key + "}", keyValuePair.Value ?? string.Empty);
		}
		return tpl.Trim().Trim(new char[]
		{
			' ',
			'•'
		});
	}

	private static string Fmt(int t)
	{
		return (t / 60).ToString() + ":" + (t % 60).ToString("00");
	}

	private static bool AddText(StringBuilder sb, string key, string val, bool first, int minLen, int maxLen)
	{
		bool flag = string.IsNullOrEmpty(val) || val.Length < minLen;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6;
		if (flag5)
		{
			flag6 = first;
		}
		else
		{
			bool flag7 = val.Length > maxLen;
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			if (flag11)
			{
				bool flag12 = maxLen > 128;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					return first;
				}
				val = val.Substring(0, maxLen - 3) + "...";
			}
			bool flag17 = !first;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			if (flag21)
			{
				sb.Append(',');
			}
			sb.Append('"').Append(key).Append("\":\"").Append(BtcDiscord.Esc(val)).Append('"');
			flag6 = false;
		}
		return flag6;
	}

	private static string MapKey(string codename, string mapPath)
	{
		bool flag = string.IsNullOrEmpty(codename);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		string text;
		if (flag5)
		{
			text = BtcDiscord.largeImage;
		}
		else
		{
			string text2 = BtcPaths.ReadKey("discord_maps.txt", codename, string.Empty);
			bool flag6 = !string.IsNullOrEmpty(text2);
			bool flag7 = flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			if (flag10)
			{
				text = text2;
			}
			else
			{
				bool flag11 = !BtcDiscord.mapUpload || string.IsNullOrEmpty(mapPath);
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				if (flag15)
				{
					text = BtcDiscord.largeImage;
				}
				else
				{
					try
					{
						string text3 = BtcDiscord.FindMapImage(mapPath);
						bool flag16 = text3 == null;
						bool flag17 = flag16;
						bool flag18 = flag17;
						bool flag19 = flag18;
						bool flag20 = flag19;
						if (flag20)
						{
							return BtcDiscord.largeImage;
						}
						FileInfo fileInfo = new FileInfo(text3);
						string text4 = string.Concat(new string[]
						{
							codename,
							"@",
							fileInfo.Length.ToString(),
							"@",
							(fileInfo.LastWriteTimeUtc.Ticks / 10000000L).ToString(),
							"@c1"
						});
						BtcDiscord.LoadUrlCache();
						string text5;
						bool flag21 = BtcDiscord.urlCache.TryGetValue(text4, out text5);
						bool flag22 = flag21;
						bool flag23 = flag22;
						bool flag24 = flag23;
						bool flag25 = flag24;
						if (flag25)
						{
							return text5;
						}
						BtcDiscord.StartUpload(text4, text3);
					}
					catch (Exception ex)
					{
						BtcDiscord.Log("MapKey failed: " + ex.Message);
					}
					text = BtcDiscord.largeImage;
				}
			}
		}
		return text;
	}

	private static string FindMapImage(string mapPath)
	{
		string[] array = new string[]
		{
			"Heightmap.png",
			"thumbnail.png"
		};
		for (int i = 0; i < array.Length; i++)
		{
			string text = Path.Combine(mapPath, array[i]);
			bool flag = File.Exists(text) && new FileInfo(text).Length <= 5000000L;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				return text;
			}
		}
		return null;
	}

	private static void LoadUrlCache()
	{
		bool flag = BtcDiscord.urlCacheLoaded;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			BtcDiscord.urlCacheLoaded = true;
			try
			{
				string path = BtcPaths.Config("discord_map_urls.txt");
				bool flag6 = File.Exists(path);
				bool flag7 = flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					string[] array = File.ReadAllLines(path);
					for (int i = 0; i < array.Length; i++)
					{
						int num = array[i].IndexOf('=');
						bool flag11 = num > 0;
						bool flag12 = flag11;
						bool flag13 = flag12;
						bool flag14 = flag13;
						bool flag15 = flag14;
						if (flag15)
						{
							BtcDiscord.urlCache[array[i].Substring(0, num)] = array[i].Substring(num + 1).Trim();
						}
					}
				}
			}
			catch (Exception)
			{
			}
		}
	}

	private static void StartUpload(string id, string file)
	{
		bool flag = BtcDiscord.uploadWww != null || Time.unscaledTime < BtcDiscord.nextUploadTry;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			int num;
			BtcDiscord.uploadFails.TryGetValue(id, out num);
			bool flag6 = num >= 4;
			bool flag7 = !flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			if (flag10)
			{
				try
				{
					byte[] contents = BtcDiscord.CropMapImage(File.ReadAllBytes(file));
					WWWForm wwwform = new WWWForm();
					wwwform.AddField("reqtype", "fileupload");
					bool flag11 = num < 2;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					string url;
					if (flag15)
					{
						url = "https://catbox.moe/user/api.php";
					}
					else
					{
						wwwform.AddField("time", "72h");
						url = "https://litterbox.catbox.moe/resources/internals/api.php";
					}
					wwwform.AddBinaryData("fileToUpload", contents, "map.png", "image/png");
					BtcDiscord.uploadId = id;
					BtcDiscord.uploadWww = new WWW(url, wwwform);
					BtcDiscord.Log("Uploading map image (attempt " + (num + 1).ToString() + "): " + id);
				}
				catch (Exception ex)
				{
					BtcDiscord.uploadWww = null;
					BtcDiscord.uploadFails[id] = num + 1;
					BtcDiscord.nextUploadTry = Time.unscaledTime + 20f;
					BtcDiscord.Log("Upload start failed: " + ex.Message);
				}
			}
		}
	}

	private static void UpdateUpload()
	{
		bool flag = BtcDiscord.uploadWww == null || !BtcDiscord.uploadWww.isDone;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			string text = BtcDiscord.uploadId;
			string text2 = null;
			string text3 = null;
			try
			{
				text3 = BtcDiscord.uploadWww.error;
				bool flag6 = string.IsNullOrEmpty(text3);
				bool flag7 = flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					string text4 = (BtcDiscord.uploadWww.text ?? string.Empty).Trim();
					bool flag11 = text4.StartsWith("https://");
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					if (flag15)
					{
						text2 = text4;
					}
					else
					{
						text3 = "bad response: " + ((text4.Length > 80) ? text4.Substring(0, 80) : text4);
					}
				}
			}
			catch (Exception ex)
			{
				text3 = ex.Message;
			}
			try
			{
				BtcDiscord.uploadWww.Dispose();
			}
			catch (Exception)
			{
			}
			BtcDiscord.uploadWww = null;
			bool flag16 = text2 != null;
			bool flag17 = flag16;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			if (flag20)
			{
				BtcDiscord.urlCache[text] = text2;
				bool flag21 = text2.Contains("litterbox");
				bool flag22 = !flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				bool flag25 = flag24;
				bool flag26 = flag25;
				if (flag26)
				{
					try
					{
						File.AppendAllText(BtcPaths.Config("discord_map_urls.txt"), text + "=" + text2 + "\r\n");
					}
					catch (Exception)
					{
					}
				}
				BtcDiscord.Log("Map image uploaded: " + text2);
			}
			else
			{
				int num;
				BtcDiscord.uploadFails.TryGetValue(text, out num);
				BtcDiscord.uploadFails[text] = num + 1;
				BtcDiscord.nextUploadTry = Time.unscaledTime + 20f;
				BtcDiscord.Log("Map image upload failed: " + text3);
			}
		}
	}

	private static void EnsureHooks(Scene scene)
	{
		bool flag = scene.buildIndex != 2;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			BtcDiscord.hookedScene = -1;
			BtcDiscord.hookedAtLevelTime = float.MaxValue;
			BtcDiscord.hooksOk = false;
			BtcDiscord.levelReadySeen = false;
			BtcDiscord.readyAt = -1f;
			BtcDiscord.loadCur = 0;
			BtcDiscord.loadMax = 0;
		}
		else
		{
			bool flag6 = BtcDiscord.hookedScene == scene.buildIndex && Time.timeSinceLevelLoad >= BtcDiscord.hookedAtLevelTime;
			BtcDiscord.hookedAtLevelTime = Time.timeSinceLevelLoad;
			bool flag7 = flag6;
			bool flag8 = !flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			if (flag11)
			{
				BtcDiscord.hookedScene = scene.buildIndex;
				BtcDiscord.levelReadySeen = false;
				BtcDiscord.readyAt = -1f;
				BtcDiscord.loadCur = 0;
				BtcDiscord.loadMax = 0;
				BtcDiscord.hooksOk = false;
				try
				{
					GameSignals.Connect<int>(GameSignals.MaxLoadingProgress, new Action<int>(BtcDiscord.OnMaxProgress), null);
					GameSignals.Connect(GameSignals.LoadingProgress, new Action(BtcDiscord.OnProgress), null);
					GameSignals.Connect<int>(GameSignals.LoadingProgressLeap, new Action<int>(BtcDiscord.OnProgressLeap), null);
					GameSignals.LateConnectOnce(GameSignals.LevelReady, new Action(BtcDiscord.OnLevelReadyHook), null);
					BtcDiscord.hooksOk = true;
					BtcDiscord.Log("Loading hooks connected for game scene.");
				}
				catch (Exception ex)
				{
					BtcDiscord.Log("Loading hooks failed: " + ex.Message);
				}
			}
		}
	}

	private static void OnMaxProgress(int max)
	{
		BtcDiscord.loadCur = 0;
		BtcDiscord.loadMax = max;
	}

	private static void OnProgress()
	{
		BtcDiscord.loadCur++;
	}

	private static void OnProgressLeap(int leap)
	{
		BtcDiscord.loadCur += leap;
	}

	private static void OnLevelReadyHook()
	{
		BtcDiscord.levelReadySeen = true;
		bool flag = BtcDiscord.readyAt < 0f;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			BtcDiscord.readyAt = Time.timeSinceLevelLoad;
		}
		BtcDiscord.Log(string.Concat(new string[]
		{
			"Level ready at ",
			BtcDiscord.readyAt.ToString("0.0"),
			"s, progress ",
			BtcDiscord.loadCur.ToString(),
			"/",
			BtcDiscord.loadMax.ToString()
		}));
	}

	private static bool IsLevelReady()
	{
		bool flag = BtcDiscord.hooksOk;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6;
		if (flag5)
		{
			flag6 = BtcDiscord.levelReadySeen;
		}
		else
		{
			flag6 = (GameParam.instance != null && GameParam.Map != null && Time.timeSinceLevelLoad >= 1.5f);
		}
		return flag6;
	}

	private static int LoadPercent()
	{
		bool flag = !BtcDiscord.hooksOk || BtcDiscord.loadMax <= 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		int num;
		if (flag5)
		{
			num = -1;
		}
		else
		{
			long val = (long)BtcDiscord.loadCur * 100L / (long)BtcDiscord.loadMax;
			num = (int)Math.Max(0L, Math.Min(100L, val));
		}
		return num;
	}

	private static string LocalColorKey()
	{
		try
		{
			User localUser = User.LocalUser;
			Faction faction = (localUser != null) ? localUser.faction : null;
			bool flag = faction != null && !string.IsNullOrEmpty(faction.name);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				return BtcDiscord.ValidColor(faction.name);
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private static string TeamFormat(List<BtcDiscord.PlayerInfo> list)
	{
		bool flag = list == null || list.Count == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		string text;
		if (flag6)
		{
			text = string.Empty;
		}
		else
		{
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			for (int i = 0; i < list.Count; i++)
			{
				int team = list[i].team;
				int num;
				dictionary.TryGetValue(team, out num);
				dictionary[team] = num + 1;
			}
			bool flag7 = list.Count == 1;
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				text = "Solo";
			}
			else
			{
				bool flag13 = dictionary.Count == 1;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				if (flag18)
				{
					text = "Co-op";
				}
				else
				{
					List<int> list2 = new List<int>(dictionary.Values);
					list2.Sort();
					list2.Reverse();
					bool flag19 = true;
					for (int j = 0; j < list2.Count; j++)
					{
						bool flag20 = list2[j] != 1;
						bool flag21 = flag20;
						bool flag22 = flag21;
						bool flag23 = flag22;
						bool flag24 = flag23;
						bool flag25 = flag24;
						if (flag25)
						{
							flag19 = false;
						}
					}
					bool flag26 = flag19 && list2.Count > 2;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					if (flag31)
					{
						text = "FFA";
					}
					else
					{
						string[] array = new string[list2.Count];
						for (int k = 0; k < list2.Count; k++)
						{
							array[k] = list2[k].ToString();
						}
						text = string.Join("v", array);
					}
				}
			}
		}
		return text;
	}

	private static byte[] CropMapImage(byte[] png)
	{
		Texture2D texture2D = null;
		Texture2D texture2D2 = null;
		byte[] array;
		try
		{
			texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			bool flag = !texture2D.LoadImage(png);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				array = png;
			}
			else
			{
				int width = texture2D.width;
				int height = texture2D.height;
				Color32[] pixels = texture2D.GetPixels32();
				int num = width;
				int num2 = height;
				int num3 = -1;
				int num4 = -1;
				for (int i = 0; i < height; i++)
				{
					int num5 = i * width;
					for (int j = 0; j < width; j++)
					{
						bool flag5 = pixels[num5 + j].a > 12;
						bool flag6 = flag5;
						bool flag7 = flag6;
						bool flag8 = flag7;
						if (flag8)
						{
							bool flag9 = j < num;
							bool flag10 = flag9;
							bool flag11 = flag10;
							bool flag12 = flag11;
							if (flag12)
							{
								num = j;
							}
							bool flag13 = j > num3;
							bool flag14 = flag13;
							bool flag15 = flag14;
							bool flag16 = flag15;
							if (flag16)
							{
								num3 = j;
							}
							bool flag17 = i < num2;
							bool flag18 = flag17;
							bool flag19 = flag18;
							bool flag20 = flag19;
							if (flag20)
							{
								num2 = i;
							}
							bool flag21 = i > num4;
							bool flag22 = flag21;
							bool flag23 = flag22;
							bool flag24 = flag23;
							if (flag24)
							{
								num4 = i;
							}
						}
					}
				}
				bool flag25 = num3 < num || num4 < num2;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				if (flag28)
				{
					array = png;
				}
				else
				{
					int num6 = 2;
					num = Math.Max(0, num - num6);
					num2 = Math.Max(0, num2 - num6);
					num3 = Math.Min(width - 1, num3 + num6);
					num4 = Math.Min(height - 1, num4 + num6);
					int num7 = num3 - num + 1;
					int num8 = num4 - num2 + 1;
					bool flag29 = num7 >= width - 4 && num8 >= height - 4;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						array = png;
					}
					else
					{
						int num9 = Math.Max(num7, num8);
						Color32[] array2 = new Color32[num9 * num9];
						int num10 = (num9 - num7) / 2;
						int num11 = (num9 - num8) / 2;
						for (int k = 0; k < num8; k++)
						{
							Array.Copy(pixels, (num2 + k) * width + num, array2, (num11 + k) * num9 + num10, num7);
						}
						texture2D2 = new Texture2D(num9, num9, TextureFormat.RGBA32, false);
						texture2D2.SetPixels32(array2);
						texture2D2.Apply();
						byte[] array3 = texture2D2.EncodeToPNG();
						BtcDiscord.Log(string.Concat(new string[]
						{
							"Map image cropped ",
							width.ToString(),
							"x",
							height.ToString(),
							" -> ",
							num9.ToString(),
							"x",
							num9.ToString()
						}));
						array = ((array3 != null && array3.Length != 0) ? array3 : png);
					}
				}
			}
		}
		catch (Exception ex)
		{
			BtcDiscord.Log("Map crop failed: " + ex.Message);
			array = png;
		}
		finally
		{
			bool flag33 = texture2D != null;
			bool flag34 = flag33;
			bool flag35 = flag34;
			bool flag36 = flag35;
			if (flag36)
			{
				UnityEngine.Object.Destroy(texture2D);
			}
			bool flag37 = texture2D2 != null;
			bool flag38 = flag37;
			bool flag39 = flag38;
			bool flag40 = flag39;
			if (flag40)
			{
				UnityEngine.Object.Destroy(texture2D2);
			}
		}
		return array;
	}

	private static string ValidColor(string name)
	{
		bool flag = string.IsNullOrEmpty(name);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		string text;
		if (flag4)
		{
			text = null;
		}
		else
		{
			string text2 = name.ToLowerInvariant();
			string[] array = new string[]
			{
				"blue",
				"green",
				"orange",
				"purple",
				"red",
				"yellow"
			};
			for (int i = 0; i < array.Length; i++)
			{
				bool flag5 = text2 == array[i];
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					return array[i];
				}
			}
			for (int j = 0; j < array.Length; j++)
			{
				bool flag9 = text2.Contains(array[j]);
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					return array[j];
				}
			}
			text = null;
		}
		return text;
	}

	private static string ColorFromFactionNumber(int factionNumber)
	{
		try
		{
			GameParam.FactionInfo[] currentFactionInfos = GameParam.CurrentFactionInfos;
			bool flag = currentFactionInfos != null && factionNumber >= 0 && factionNumber < currentFactionInfos.Length && currentFactionInfos[factionNumber] != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				return BtcDiscord.ValidColor(currentFactionInfos[factionNumber].Name);
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private static List<BtcDiscord.PlayerInfo> ScanUsers()
	{
		bool flag = BtcDiscord.usersCache != null && Time.unscaledTime < BtcDiscord.usersNext;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		List<BtcDiscord.PlayerInfo> list;
		if (flag4)
		{
			list = BtcDiscord.usersCache;
		}
		else
		{
			BtcDiscord.usersNext = Time.unscaledTime + 5f;
			List<BtcDiscord.PlayerInfo> list2 = new List<BtcDiscord.PlayerInfo>();
			try
			{
				foreach (User user in UnityEngine.Object.FindObjectsOfType<User>())
				{
					bool flag5 = user == null || BtcDiscord.IsRecorder(user);
					bool flag6 = !flag5;
					bool flag7 = flag6;
					bool flag8 = flag7;
					if (flag8)
					{
						Faction faction = user.faction;
						BtcDiscord.PlayerInfo playerInfo = new BtcDiscord.PlayerInfo();
						playerInfo.name = BtcNick.Plain(user.ScreenName);
						playerInfo.team = user.NetworkTeamNumber;
						playerInfo.spec = (faction != null && faction.IsSpectator);
						playerInfo.color = ((faction != null && !playerInfo.spec) ? BtcDiscord.ValidColor(faction.name) : null);
						playerInfo.local = (user == User.LocalUser);
						list2.Add(playerInfo);
					}
				}
			}
			catch (Exception ex)
			{
				BtcDiscord.Log("ScanUsers failed: " + ex.Message);
			}
			BtcDiscord.usersCache = list2;
			list = list2;
		}
		return list;
	}

	/// <summary>The replay recorder joins as a hidden spectator user ("REC", shown as "Player"): never list it.</summary>
	private static bool IsRecorder(User u)
	{
		try
		{
			return BtcReplay.IsRec(u.connectionToClient) || u.Network_screenName == "REC";
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static List<int> BuildTeamOrder(List<BtcDiscord.PlayerInfo> players)
	{
		List<int> list = new List<int>();
		int item = int.MinValue;
		for (int i = 0; i < players.Count; i++)
		{
			bool local = players[i].local;
			bool flag = local;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				item = players[i].team;
			}
			bool flag4 = !list.Contains(players[i].team);
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				list.Add(players[i].team);
			}
		}
		list.Sort();
		bool flag8 = list.Remove(item);
		bool flag9 = flag8;
		bool flag10 = flag9;
		bool flag11 = flag10;
		if (flag11)
		{
			list.Insert(0, item);
		}
		return list;
	}

	private static string TeamNames(List<BtcDiscord.PlayerInfo> players, List<int> order)
	{
		List<string> list = new List<string>();
		for (int i = 0; i < order.Count; i++)
		{
			List<string> list2 = new List<string>();
			for (int j = 0; j < players.Count; j++)
			{
				bool flag = players[j].team == order[i];
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				if (flag4)
				{
					list2.Add(players[j].name);
				}
			}
			list.Add(string.Join("+", list2.ToArray()));
		}
		return string.Join(" : ", list.ToArray());
	}

	private static string CrystalScore(List<int> order)
	{
		bool flag = order == null || order.Count == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		string text;
		if (flag4)
		{
			text = BtcDiscord.LegacyCrystalScore();
		}
		else
		{
			bool flag5 = Time.unscaledTime < BtcDiscord.crystalNext;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				text = BtcDiscord.crystalCache;
			}
			else
			{
				BtcDiscord.crystalNext = Time.unscaledTime + 6f;
				try
				{
					Dictionary<Faction, int> dictionary = new Dictionary<Faction, int>();
					User[] array = UnityEngine.Object.FindObjectsOfType<User>();
					for (int i = 0; i < array.Length; i++)
					{
						Faction faction = (array[i] != null) ? array[i].faction : null;
						bool flag9 = faction != null && !faction.IsSpectator;
						bool flag10 = flag9;
						bool flag11 = flag10;
						bool flag12 = flag11;
						if (flag12)
						{
							dictionary[faction] = array[i].NetworkTeamNumber;
						}
					}
					Dictionary<int, int> dictionary2 = new Dictionary<int, int>();
					for (int j = 0; j < order.Count; j++)
					{
						dictionary2[order[j]] = 0;
					}
					IsCapturable[] array2 = UnityEngine.Object.FindObjectsOfType<IsCapturable>();
					for (int k = 0; k < array2.Length; k++)
					{
						bool flag13 = array2[k] == null || !array2[k].gameObject.activeInHierarchy;
						bool flag14 = !flag13;
						bool flag15 = flag14;
						bool flag16 = flag15;
						if (flag16)
						{
							Faction faction2 = Faction.GetFaction(array2[k].gameObject);
							int num = 0;
							bool flag17 = faction2 != null && faction2 != Faction.DefaultNeutral && dictionary.TryGetValue(faction2, out num) && dictionary2.ContainsKey(num);
							bool flag18 = flag17;
							bool flag19 = flag18;
							bool flag20 = flag19;
							if (flag20)
							{
								Dictionary<int, int> dictionary3 = dictionary2;
								int key = num;
								int num2 = dictionary3[key];
								dictionary3[key] = num2 + 1;
							}
						}
					}
					string[] array3 = new string[order.Count];
					for (int l = 0; l < order.Count; l++)
					{
						array3[l] = Math.Max(0, dictionary2[order[l]] - 1).ToString();
					}
					BtcDiscord.crystalCache = string.Join(" : ", array3);
				}
				catch (Exception)
				{
				}
				text = BtcDiscord.crystalCache;
			}
		}
		return text;
	}

	private static string LegacyCrystalScore()
	{
		bool flag = Time.unscaledTime < BtcDiscord.crystalNext;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		string text;
		if (flag5)
		{
			text = BtcDiscord.crystalCache;
		}
		else
		{
			BtcDiscord.crystalNext = Time.unscaledTime + 6f;
			try
			{
				User localUser = User.LocalUser;
				Faction faction = (localUser != null) ? localUser.faction : null;
				bool flag6 = faction != null;
				bool flag7 = flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					int num = 0;
					int num2 = 0;
					IsCapturable[] array = UnityEngine.Object.FindObjectsOfType<IsCapturable>();
					for (int i = 0; i < array.Length; i++)
					{
						bool flag11 = !(array[i] == null) && array[i].gameObject.activeInHierarchy;
						bool flag12 = flag11;
						bool flag13 = flag12;
						bool flag14 = flag13;
						bool flag15 = flag14;
						if (flag15)
						{
							Faction faction2 = Faction.GetFaction(array[i].gameObject);
							bool flag16 = !(faction2 == null) && !(faction2 == Faction.DefaultNeutral);
							bool flag17 = flag16;
							bool flag18 = flag17;
							bool flag19 = flag18;
							bool flag20 = flag19;
							if (flag20)
							{
								bool flag21 = Affiliation.GetRelation(faction, faction2) == Affiliation.Relation.Allied;
								bool flag22 = flag21;
								bool flag23 = flag22;
								bool flag24 = flag23;
								bool flag25 = flag24;
								if (flag25)
								{
									num++;
								}
								else
								{
									num2++;
								}
							}
						}
					}
					num = Math.Max(0, num - 1);
					num2 = Math.Max(0, num2 - 1);
					BtcDiscord.crystalCache = num.ToString() + " : " + num2.ToString();
				}
			}
			catch (Exception)
			{
			}
			text = BtcDiscord.crystalCache;
		}
		return text;
	}

	private static string BcLabel()
	{
		string text;
		try
		{
			text = (BtcBc.BcEnabled ? "BetterConnection" : "Vanilla");
		}
		catch (Exception)
		{
			text = "Vanilla";
		}
		return text;
	}

	private static bool enabled;

	private static Thread worker;

	private static volatile bool running;

	private static readonly object gate = new object();

	private static BtcDiscord.Activity pending;

	private static string pendingKey = string.Empty;

	private static string sentKey = null;

	private static string clientId = string.Empty;

	private static string largeImage = "logo";

	private static string largeText = "Better Than Castle";

	private static bool showMap = true;

	private static bool showPlayers = true;

	private static bool cfgLoaded;

	private static float nextPoll;

	private static string lastStateId;

	private static long stateStart;

	private static readonly IntPtr INVALID;

	private static bool showCrystals = true;

	private static bool showTimer = true;

	private static bool hideMap;

	private static bool hideNick;

	private static string tplDetails = "{mode} • {map}";

	private static string tplState = "{fp} • Crystals {crystals}";

	private static int result;

	private static string stateStartId;

	private static bool mapUpload = true;

	private static long timerAnchor;

	private static int hookedScene;

	private static float hookedAtLevelTime;

	private static bool hooksOk;

	private static bool levelReadySeen;

	private static float readyAt;

	private static int loadCur;

	private static int loadMax;

	private static float crystalNext;

	private static string crystalCache;

	private static readonly Dictionary<string, string> urlCache;

	private static bool urlCacheLoaded;

	private static readonly Dictionary<string, int> uploadFails;

	private static WWW uploadWww;

	private static string uploadId;

	private static float nextUploadTry;

	private static bool showLobbyNames = true;

	private static bool lastPaused;

	private static List<BtcDiscord.PlayerInfo> snapPlayers;

	private static int snapMax;

	private static List<BtcDiscord.PlayerInfo> usersCache;

	private static float usersNext;

	private static string aboutLabel = "About Mod";

	private static string aboutUrl = "https://github.com/NNLEKKK/BetterThanCastle";

	private class Activity
	{
		public string Key()
		{
			return string.Concat(new object[]
			{
				this.details,
				"|",
				this.state,
				"|",
				this.start,
				"|",
				this.smallImage,
				"|",
				this.smallText,
				"|",
				this.partySize,
				"/",
				this.partyMax,
				"|",
				this.largeImage,
				"|",
				this.largeText
			});
		}

		public string details;

		public string state;

		public long start;

		public string largeImage;

		public string largeText;

		public string smallImage;

		public string smallText;

		public int partySize;

		public int partyMax;

		public bool fast;
	}

	private class PlayerInfo
	{
		public string name;

		public int team;

		public string color;

		public bool local;

		public bool spec;
	}
}
}
