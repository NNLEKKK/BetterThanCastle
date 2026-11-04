using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Brix.Assets;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Legacy;
using Brix.Network;
using Brix.NewUI.LoadingScreen;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
public static class BtcReplay
{
	public static BtcReplay.State Mode
	{
		get
		{
			return BtcReplay.mode;
		}
	}

	public static string ReplaysDir
	{
		get
		{
			return System.IO.Path.Combine(BtcPaths.Root, "Replays");
		}
	}

	public static float RecSeconds
	{
		get
		{
			return (BtcReplay.mode == BtcReplay.State.Recording) ? BtcReplay.RecClock() : 0f;
		}
	}

	public static string RecFile
	{
		get
		{
			return string.IsNullOrEmpty(BtcReplay.curPath) ? "" : System.IO.Path.GetFileName(BtcReplay.curPath);
		}
	}

	public static string T(string en, string ru)
	{
		return BtcLang.Russian ? ru : en;
	}

	public static void Say(string text)
	{
		BtcReplay.Message = text;
		BtcReplay.MessageUntil = Time.unscaledTime + 8f;
		BtcReplay.Log(text);
	}

	private static void Log(string text)
	{
		try
		{
			UnityEngine.Debug.Log("[BtcReplay] " + text);
			bool flag = BtcReplay.logLines < 1500;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				BtcReplay.logLines++;
				BtcPaths.Init();
				File.AppendAllText(BtcPaths.DebugFile("replay_log.txt"), DateTime.Now.ToString("HH:mm:ss", BtcReplay.Inv) + " " + text + "\r\n");
			}
		}
		catch (Exception)
		{
		}
	}

	private static void EnsureCfg()
	{
		bool flag = BtcReplay.cfgLoaded;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcReplay.cfgLoaded = true;
			try
			{
				BtcPaths.Init();
				BtcReplay.RecordEnabled = (BtcPaths.ReadKey("replay.txt", "Record", "1") != "0");
				BtcReplay.RecordSingleplayer = (BtcPaths.ReadKey("replay.txt", "RecordSingleplayer", "0") == "1");
				BtcReplay.ShowIndicator = (BtcPaths.ReadKey("replay.txt", "ShowIndicator", "1") != "0");
				BtcReplay.RecSpectator = (BtcPaths.ReadKey("replay.txt", "SpectatorUser", "1") != "0");
				int num;
				bool flag8 = int.TryParse(BtcPaths.ReadKey("replay.txt", "KeyMinutes", "5"), NumberStyles.Integer, BtcReplay.Inv, out num) && num >= 0 && num <= 120;
				bool flag9 = flag8;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				if (flag14)
				{
					BtcReplay.KeyMinutes = num;
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
			File.WriteAllText(BtcPaths.Config("replay.txt"), string.Concat(new string[]
			{
				"# BtcReplay settings (managed from the Replay tab). 1 = on, 0 = off.\r\nRecord=",
				BtcReplay.RecordEnabled ? "1" : "0",
				"\r\nRecordSingleplayer=",
				BtcReplay.RecordSingleplayer ? "1" : "0",
				"\r\nShowIndicator=",
				BtcReplay.ShowIndicator ? "1" : "0",
				"\r\n# Hidden spectator user on the host: gives the replay a full spectator view. 0 = raw stream only.\r\nSpectatorUser=",
				BtcReplay.RecSpectator ? "1" : "0",
				"\r\n# Keyframe (full world respawn) every KeyMinutes minutes, 0 = never.\r\nKeyMinutes=",
				BtcReplay.KeyMinutes.ToString(BtcReplay.Inv),
				"\r\n"
			}));
		}
		catch (Exception)
		{
		}
	}

	public static void SetRecordEnabled(bool v)
	{
		BtcReplay.EnsureCfg();
		BtcReplay.RecordEnabled = v;
		BtcReplay.SaveCfg();
	}

	public static void SetRecordSingleplayer(bool v)
	{
		BtcReplay.EnsureCfg();
		BtcReplay.RecordSingleplayer = v;
		BtcReplay.SaveCfg();
	}

	public static void SetShowIndicator(bool v)
	{
		BtcReplay.EnsureCfg();
		BtcReplay.ShowIndicator = v;
		BtcReplay.SaveCfg();
	}

	private static bool MatchLive()
	{
		bool result;
		try
		{
			result = (NetworkServer.active && UNetManager.instance != null && GameParam.instance != null && GameParam.Map != null && GameParam.instance.HostLoadedAllFactions && User.LocalUser != null && SceneManager.GetActiveScene().buildIndex == 2 && GameSignals.Received<Action>(GameSignals.LevelReady));
		}
		catch (Exception)
		{
			result = false;
		}
		return result;
	}

	public static void Tick()
	{
		BtcReplayFix.Tick();
		BtcReplay.EnsureCfg();
		float unscaledTime = Time.unscaledTime;
		bool flag = BtcReplay.mode == BtcReplay.State.Playing;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcReplay.PlayTick(unscaledTime);
		}
		else
		{
			bool flag9 = BtcReplay.mode == BtcReplay.State.Idle;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				bool flag17 = BtcReplay.restartData != null;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				if (flag23)
				{
					BtcReplay.RestartTick(unscaledTime);
				}
				else
				{
					bool flag24 = !BtcReplay.RecordEnabled || unscaledTime < BtcReplay.nextCheck;
					bool flag25 = !flag24;
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					if (flag31)
					{
						BtcReplay.nextCheck = unscaledTime + 0.5f;
						try
						{
							bool flag32 = !BtcReplay.MatchLive();
							bool flag33 = flag32;
							bool flag34 = flag33;
							bool flag35 = flag34;
							bool flag36 = flag35;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							if (flag39)
							{
								BtcReplay.attempts = 0;
								BtcReplay.recordedThisMatch = false;
							}
							else
							{
								bool flag40 = (Neo.multiplayer || BtcReplay.RecordSingleplayer) && unscaledTime >= BtcReplay.recRetryAt && BtcReplay.attempts < 3 && !BtcReplay.recordedThisMatch;
								bool flag41 = flag40;
								bool flag42 = flag41;
								bool flag43 = flag42;
								bool flag44 = flag43;
								bool flag45 = flag44;
								bool flag46 = flag45;
								bool flag47 = flag46;
								if (flag47)
								{
									BtcReplay.BeginRecording();
								}
							}
						}
						catch (Exception arg)
						{
							BtcReplay.Log("Tick error: " + arg);
						}
					}
				}
			}
			else
			{
				bool flag48 = BtcReplay.mode != BtcReplay.State.Recording || unscaledTime < BtcReplay.nextTick;
				bool flag49 = !flag48;
				bool flag50 = flag49;
				bool flag51 = flag50;
				bool flag52 = flag51;
				bool flag53 = flag52;
				bool flag54 = flag53;
				bool flag55 = flag54;
				if (flag55)
				{
					BtcReplay.nextTick = unscaledTime + 0.5f;
					try
					{
						BtcReplay.FlushBuf();
						BtcReplay.PollSnapshot();
						bool flag56 = !BtcReplay.MatchLive();
						bool flag57 = flag56;
						bool flag58 = flag57;
						bool flag59 = flag58;
						bool flag60 = flag59;
						bool flag61 = flag60;
						bool flag62 = flag61;
						bool flag63 = flag62;
						if (flag63)
						{
							BtcReplay.EndRecording();
							BtcReplay.recordedThisMatch = false;
						}
						else
						{
							BtcReplay.MarkIgnored(BtcReplay.ConnId(0));
							BtcReplay.RecSpectatorTick(unscaledTime);
							bool flag64 = BtcReplay.kfConn != null;
							bool flag65 = flag64;
							bool flag66 = flag65;
							bool flag67 = flag66;
							bool flag68 = flag67;
							bool flag69 = flag68;
							bool flag70 = flag69;
							bool flag71 = flag70;
							if (flag71)
							{
								bool flag72 = (BtcReplay.kfSpawnDone && unscaledTime - BtcReplay.kfDoneAt > 1.5f) || unscaledTime - BtcReplay.kfStart > 30f;
								bool flag73 = flag72;
								bool flag74 = flag73;
								bool flag75 = flag74;
								bool flag76 = flag75;
								bool flag77 = flag76;
								bool flag78 = flag77;
								bool flag79 = flag78;
								if (flag79)
								{
									BtcReplay.EndKeyframe();
								}
								else
								{
									BtcReplay.MarkIgnored(BtcReplay.ConnId(1));
								}
							}
							else
							{
								bool flag80 = BtcReplay.KeyMinutes > 0 && unscaledTime >= BtcReplay.nextKf;
								bool flag81 = flag80;
								bool flag82 = flag81;
								bool flag83 = flag82;
								bool flag84 = flag83;
								bool flag85 = flag84;
								bool flag86 = flag85;
								bool flag87 = flag86;
								if (flag87)
								{
									BtcReplay.StartKeyframe();
								}
							}
						}
					}
					catch (Exception arg2)
					{
						BtcReplay.Log("RecTick error: " + arg2);
					}
				}
			}
		}
	}

	public static void Shutdown()
	{
		try
		{
			bool flag = BtcReplay.mode == BtcReplay.State.Recording;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				BtcReplay.EndRecording();
			}
			Thread thread = BtcReplay.wThread;
			bool flag8 = thread != null && thread.IsAlive;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				thread.Join(3000);
			}
		}
		catch (Exception)
		{
		}
	}

	private static string Esc(string s)
	{
		return (s == null) ? "" : s.Replace("\r", " ").Replace("\n", " ").Replace("|", "/");
	}

	private static string SafeName(string s)
	{
		bool flag = string.IsNullOrEmpty(s);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		string result;
		if (flag7)
		{
			result = "map";
		}
		else
		{
			char[] invalidFileNameChars = System.IO.Path.GetInvalidFileNameChars();
			StringBuilder stringBuilder = new StringBuilder();
			int num = 0;
			while (num < s.Length && stringBuilder.Length < 40)
			{
				char c = s[num];
				bool flag8 = char.IsWhiteSpace(c) || Array.IndexOf<char>(invalidFileNameChars, c) >= 0;
				stringBuilder.Append(flag8 ? '_' : c);
				num++;
			}
			result = stringBuilder.ToString();
		}
		return result;
	}

	private static void BeginRecording()
	{
		try
		{
			bool flag = BtcReplay.wThread != null && BtcReplay.wThread.IsAlive;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcReplay.recRetryAt = Time.unscaledTime + 1f;
			}
			else
			{
				bool flag9 = NetworkServer.hostTopology == null;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					BtcReplay.recRetryAt = Time.unscaledTime + 2f;
				}
				else
				{
					BtcReplay.attempts++;
					Directory.CreateDirectory(BtcReplay.ReplaysDir);
					string s = (GameParam.Map != null) ? GameParam.Map.displayName : "map";
					BtcReplay.curPath = System.IO.Path.Combine(BtcReplay.ReplaysDir, string.Concat(new string[]
					{
						DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", BtcReplay.Inv),
						"_",
						BtcReplay.SafeName(s),
						Neo.multiplayer ? "_mp" : "_sp",
						".btcr"
					}));
					StringBuilder stringBuilder = new StringBuilder();
					stringBuilder.Append("map=").Append(BtcReplay.Esc(GameParam.Map.codename)).Append('\n');
					stringBuilder.Append("mapName=").Append(BtcReplay.Esc(s)).Append('\n');
					stringBuilder.Append("mapModified=").Append(GameParam.Map.ModifiedTime.ToString(BtcReplay.Inv)).Append('\n');
					stringBuilder.Append("newGame=").Append(GameParam.Map.IsNewGame() ? "1" : "0").Append('\n');
					stringBuilder.Append("mode=").Append((GameParam.Gamemode != null) ? BtcReplay.Esc(GameParam.Gamemode.codename) : "").Append('\n');
					stringBuilder.Append("preset=").Append((GameParam.Preset != null) ? BtcReplay.Esc(GameParam.Preset.codename) : "").Append('\n');
					stringBuilder.Append("mp=").Append(Neo.multiplayer ? "1" : "0").Append('\n');
					stringBuilder.Append("clock=").Append(Neo.multiplayer ? "wall" : "game").Append('\n');
					stringBuilder.Append("ticks=").Append(DateTime.Now.Ticks.ToString(BtcReplay.Inv)).Append('\n');
					stringBuilder.Append("scene=").Append(BtcReplay.Esc(NetworkManager.networkSceneName)).Append('\n');
					stringBuilder.Append("keyMin=").Append(BtcReplay.KeyMinutes.ToString(BtcReplay.Inv)).Append('\n');
					stringBuilder.Append(BtcReplayFix.GpsLine());
					StringBuilder stringBuilder2 = new StringBuilder();
					List<User> userList = UNetManager.instance.UserList;
					for (int i = 0; i < userList.Count; i++)
					{
						User user = userList[i];
						bool flag17 = user != null;
						bool flag18 = flag17;
						bool flag19 = flag18;
						bool flag20 = flag19;
						bool flag21 = flag20;
						bool flag22 = flag21;
						bool flag23 = flag22;
						bool flag24 = flag23;
						if (flag24)
						{
							bool flag25 = stringBuilder2.Length > 0;
							bool flag26 = flag25;
							bool flag27 = flag26;
							bool flag28 = flag27;
							bool flag29 = flag28;
							bool flag30 = flag29;
							bool flag31 = flag30;
							bool flag32 = flag31;
							if (flag32)
							{
								stringBuilder2.Append('|');
							}
							stringBuilder2.Append(BtcReplay.Esc(user.ScreenName));
						}
					}
					stringBuilder.Append("players=").Append(stringBuilder2.ToString()).Append('\n');
					stringBuilder.Append(BtcReplayNames.HeaderLine());
					stringBuilder.Append(BtcTimer.HeaderLine());
					byte[] bytes = Encoding.UTF8.GetBytes(stringBuilder.ToString());
					BtcReplay.tlOff = 9L + (long)bytes.Length - (long)BtcTimer.PadLen - 1L;
					BtcReplay.fs = new FileStream(BtcReplay.curPath, FileMode.Create, FileAccess.Write, FileShare.Read);
					BtcReplay.fs.Write(new byte[]
					{
						66,
						84,
						67,
						82,
						3
					}, 0, 5);
					BtcReplay.fs.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
					BtcReplay.fs.Write(bytes, 0, bytes.Length);
					BtcReplay.ds = new DeflateStream(BtcReplay.fs, CompressionMode.Compress, true);
					BtcReplay.q.Clear();
					BtcReplay.wClosing = false;
					BtcReplay.wAbort = false;
					BtcReplay.wPath = BtcReplay.curPath;
					BtcReplay.outBytes = 0L;
					BtcReplay.bufLen = 0;
					BtcReplay.lastMs = 0U;
					BtcReplay.packets = 0;
					BtcReplay.rawBytes = 0L;
					BtcReplay.kfCount = 0;
					BtcReplay.kfId = 0;
					BtcReplay.kfConn = null;
					BtcReplay.recT0 = Time.unscaledTime;
					BtcReplay.recGameClock = !Neo.multiplayer;
					BtcReplay.recG0 = Time.time;
					BtcReplay.mode = BtcReplay.State.Recording;
					BtcTimer.OnRecordStart();
					BtcReplay.wThread = new Thread(new ThreadStart(BtcReplay.WriterLoop));
					BtcReplay.wThread.IsBackground = true;
					BtcReplay.wThread.Start();
					BtcReplay.StartSnapshot();
					BtcReplay.mainConn = BtcReplay.AddRecConn(0);
					bool flag33 = BtcReplay.mainConn == null;
					bool flag34 = flag33;
					bool flag35 = flag34;
					bool flag36 = flag35;
					bool flag37 = flag36;
					bool flag38 = flag37;
					bool flag39 = flag38;
					bool flag40 = flag39;
					if (flag40)
					{
						BtcReplay.Log("cannot add recorder connection");
						BtcReplay.AbortRecording();
						BtcReplay.recRetryAt = Time.unscaledTime + 10f;
					}
					else
					{
						BtcReplay.SetupRecSpectator(BtcReplay.mainConn);
						BtcReplay.recordedThisMatch = true;
						BtcReplay.nextTick = Time.unscaledTime + 0.5f;
						BtcReplay.nextKf = ((BtcReplay.KeyMinutes > 0) ? (Time.unscaledTime + (float)BtcReplay.KeyMinutes * 60f) : float.MaxValue);
						BtcReplay.Log("Recording started: " + BtcReplay.curPath);
					}
				}
			}
		}
		catch (Exception arg)
		{
			BtcReplay.Log("Cannot start recording: " + arg);
			BtcReplay.AbortRecording();
			BtcReplay.recRetryAt = Time.unscaledTime + 10f;
		}
	}

	private static void EndRecording()
	{
		bool flag = BtcReplay.mode != BtcReplay.State.Recording;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			try
			{
				bool flag8 = BtcReplay.kfConn != null;
				bool flag9 = flag8;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				if (flag14)
				{
					BtcReplay.EndKeyframe();
				}
				BtcReplay.FlushBuf();
				Thread thread = BtcReplay.snapThread;
				bool flag15 = thread != null && thread.IsAlive;
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				if (flag21)
				{
					thread.Join(15000);
				}
				BtcReplay.PollSnapshot();
				BtcReplay.footDur = BtcReplay.RecClock();
				BtcReplay.footPackets = BtcReplay.packets;
				BtcReplay.footRaw = BtcReplay.rawBytes;
				BtcReplay.footKf = BtcReplay.kfCount;
				BtcReplay.footSnap = (BtcReplay.snapOk ? BtcReplay.snapLen : 0);
				BtcReplay.mode = BtcReplay.State.Idle;
				BtcReplay.RemoveRecConn(BtcReplay.mainConn);
				BtcReplay.mainConn = null;
				object obj = BtcReplay.qLock;
				object obj2 = obj;
				object obj3 = obj2;
				object obj4 = obj3;
				object obj5 = obj4;
				object obj6 = obj5;
				object obj7 = obj6;
				lock (obj7)
				{
					BtcReplay.wClosing = true;
					Monitor.PulseAll(BtcReplay.qLock);
				}
				BtcReplay.Log(string.Concat(new object[]
				{
					"Recording finished: ",
					BtcReplay.footDur.ToString("F1", BtcReplay.Inv),
					" s, ",
					BtcReplay.footPackets,
					" packets, ",
					BtcReplay.footRaw,
					" raw bytes, ",
					BtcReplay.footKf,
					" keyframes, snapshot ",
					BtcReplay.footSnap,
					" bytes, ",
					BtcReplay.curPath
				}));
				BtcReplayAuto.OnRecordingEnded(BtcReplay.curPath);
			}
			catch (Exception arg)
			{
				BtcReplay.Log("EndRecording: " + arg);
				BtcReplay.mode = BtcReplay.State.Idle;
			}
		}
	}

	/// <summary>True while the writer thread still flushes/closes the current .btcr (BtcReplayAuto waits for it).</summary>
	internal static bool WriterBusy
	{
		get
		{
			Thread t = BtcReplay.wThread;
			return t != null && t.IsAlive;
		}
	}

	public static bool Intercept(NetworkBehaviour self, string method, object[] args)
	{
		return BtcReplay.Active;
	}

	public static List<BtcReplay.Data> ListReplays()
	{
		List<BtcReplay.Data> list = new List<BtcReplay.Data>();
		try
		{
			BtcReplayShare.ImportZips();
			string replaysDir = BtcReplay.ReplaysDir;
			bool flag = !Directory.Exists(replaysDir);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				return list;
			}
			string[] files = Directory.GetFiles(replaysDir, "*.btcr");
			for (int i = 0; i < files.Length; i++)
			{
				list.Add(BtcReplay.ReadHeader(files[i]));
			}
			list.Sort((BtcReplay.Data a, BtcReplay.Data b) => b.Ticks.CompareTo(a.Ticks));
		}
		catch (Exception)
		{
		}
		return list;
	}

	public static bool Delete(string path)
	{
		try
		{
			bool flag = !string.IsNullOrEmpty(path) && File.Exists(path) && path != BtcReplay.curPath;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				File.Delete(path);
				return true;
			}
			bool flag8 = !string.IsNullOrEmpty(path) && path == BtcReplay.curPath && BtcReplay.mode == BtcReplay.State.Idle && File.Exists(path);
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				File.Delete(path);
				return true;
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	public static bool CanStartReplay()
	{
		bool result;
		try
		{
			result = (BtcReplay.mode <= BtcReplay.State.Idle && !NetworkServer.active && !NetworkClient.active && SceneManager.GetActiveScene().buildIndex != 2 && UNetManager.instance != null);
		}
		catch (Exception)
		{
			result = false;
		}
		return result;
	}

	static BtcReplay()
	{
		BtcReplay.RecordEnabled = true;
		BtcReplay.ShowIndicator = true;
		BtcReplay.RecSpectator = true;
		BtcReplay.ProxyUsers = new List<User>();
		BtcReplay.FeatureOn = true;
		BtcReplay.KeyMinutes = 5;
		BtcReplay.Inv = CultureInfo.InvariantCulture;
		BtcReplay.nextKf = float.MaxValue;
		BtcReplay.buf = new byte[524288];
		BtcReplay.qLock = new object();
		BtcReplay.q = new Queue<byte[]>();
		BtcReplay.playSceneOk = -1f;
		BtcReplay.playLeftAt = -1f;
		BtcReplay.playSpeed = 1f;
		BtcReplay.feedErrKeys = new HashSet<string>();
	}

	public static void OnBeforeGamemode(int phase)
	{
	}

	public static bool IsRec(NetworkConnection c)
	{
		return c is BtcReplay.RecConn;
	}

	public static bool GetRecordEnabled()
	{
		BtcReplay.EnsureCfg();
		return BtcReplay.RecordEnabled;
	}

	public static bool GetRecordSingleplayer()
	{
		BtcReplay.EnsureCfg();
		return BtcReplay.RecordSingleplayer;
	}

	public static bool GetShowIndicator()
	{
		BtcReplay.EnsureCfg();
		return BtcReplay.ShowIndicator;
	}

	public static int GetKeyMinutes()
	{
		BtcReplay.EnsureCfg();
		return BtcReplay.KeyMinutes;
	}

	public static void SetKeyMinutes(int v)
	{
		BtcReplay.EnsureCfg();
		BtcReplay.KeyMinutes = Mathf.Clamp(v, 0, 120);
		BtcReplay.SaveCfg();
		bool flag = BtcReplay.mode == BtcReplay.State.Recording && BtcReplay.kfConn == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcReplay.nextKf = ((BtcReplay.KeyMinutes > 0) ? (Time.unscaledTime + (float)BtcReplay.KeyMinutes * 60f) : float.MaxValue);
		}
	}

	private static int ConnId(int tag)
	{
		return NetworkServer.hostTopology.MaxDefaultConnections + 30 + tag;
	}

	private static void Noop(NetworkMessage m)
	{
	}

	private static void MarkIgnored(int id)
	{
		try
		{
			bool flag = !BtcReplay.ignoreTried;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				BtcReplay.ignoreTried = true;
				FieldInfo field = typeof(BtcSpectate).GetField("_ignore", BindingFlags.Static | BindingFlags.NonPublic);
				bool flag8 = field != null;
				bool flag9 = flag8;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				if (flag14)
				{
					BtcReplay.ignoreSet = (field.GetValue(null) as HashSet<int>);
				}
			}
			bool flag15 = BtcReplay.ignoreSet != null && !BtcReplay.ignoreSet.Contains(id);
			bool flag16 = flag15;
			bool flag17 = flag16;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			if (flag21)
			{
				BtcReplay.ignoreSet.Add(id);
			}
		}
		catch (Exception)
		{
		}
	}

	/// <summary>Blueprint.OnRebuildObservers postfix: the recording connections observe every blueprint (local fake clients, nothing goes to real players).</summary>
	internal static void AddRecObservers(HashSet<NetworkConnection> observers)
	{
		if (BtcReplay.mode != BtcReplay.State.Recording) return;
		if (BtcReplay.mainConn != null && BtcReplay.mainConn.HasUser) observers.Add(BtcReplay.mainConn);
		if (BtcReplay.kfConn != null) observers.Add(BtcReplay.kfConn);
	}

	private static BtcReplay.RecConn AddRecConn(int tag)
	{
		int num = BtcReplay.ConnId(tag);
		BtcReplay.RecConn recConn = new BtcReplay.RecConn();
		recConn.Tag = tag;
		recConn.Initialize("btc-replay", 0, num, NetworkServer.hostTopology);
		BtcReplay.MarkIgnored(num);
		Dictionary<short, NetworkMessageDelegate> handlers = NetworkServer.handlers;
		NetworkMessageDelegate value = null;
		bool flag = handlers.TryGetValue(32, out value);
		handlers[32] = new NetworkMessageDelegate(BtcReplay.Noop);
		bool flag2;
		try
		{
			flag2 = NetworkServer.AddExternalConnection(recConn);
		}
		finally
		{
			bool flag3 = flag;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				handlers[32] = value;
			}
			else
			{
				handlers.Remove(32);
			}
		}
		bool flag10 = !flag2;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		BtcReplay.RecConn result;
		if (flag16)
		{
			result = null;
		}
		else
		{
			NetworkServer.SetClientReady(recConn);
			result = recConn;
		}
		return result;
	}

	private static void RemoveRecConn(BtcReplay.RecConn c)
	{
		bool flag = c == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			try
			{
				c.isReady = false;
				bool hasUser = c.HasUser;
				bool flag8 = hasUser;
				bool flag9 = flag8;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				if (flag13)
				{
					try
					{
						NetworkServer.DestroyPlayersForConnection(c);
					}
					catch (Exception)
					{
					}
					BtcReplay.UnmarkSpec(c.connectionId);
					c.HasUser = false;
				}
				Dictionary<short, NetworkMessageDelegate> handlers = NetworkServer.handlers;
				NetworkMessageDelegate value = null;
				bool flag14 = handlers.TryGetValue(33, out value);
				handlers[33] = new NetworkMessageDelegate(BtcReplay.Noop);
				try
				{
					NetworkServer.RemoveExternalConnection(c.connectionId);
				}
				finally
				{
					bool flag15 = flag14;
					bool flag16 = flag15;
					bool flag17 = flag16;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					if (flag21)
					{
						handlers[33] = value;
					}
					else
					{
						handlers.Remove(33);
					}
				}
			}
			catch (Exception ex)
			{
				BtcReplay.Log("RemoveRecConn: " + ex.Message);
			}
		}
	}

	private static void AbortRecording()
	{
		try
		{
			BtcReplay.mode = BtcReplay.State.Idle;
			BtcReplay.RemoveRecConn(BtcReplay.kfConn);
			BtcReplay.RemoveRecConn(BtcReplay.mainConn);
			BtcReplay.kfConn = null;
			BtcReplay.mainConn = null;
			BtcReplay.bufLen = 0;
			BtcReplay.CancelShell();
			BtcReplay.DropSnapshotFile();
			bool flag = BtcReplay.wThread != null && BtcReplay.wThread.IsAlive;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				BtcReplay.wAbort = true;
				object obj = BtcReplay.qLock;
				object obj2 = obj;
				object obj3 = obj2;
				object obj4 = obj3;
				object obj5 = obj4;
				object obj6 = obj5;
				object obj7 = obj6;
				lock (obj7)
				{
					BtcReplay.wClosing = true;
					Monitor.PulseAll(BtcReplay.qLock);
				}
			}
			else
			{
				BtcReplay.CloseWriterNow(true);
			}
		}
		catch (Exception)
		{
		}
	}

	private static void CloseWriterNow(bool delete)
	{
		try
		{
			bool flag = BtcReplay.ds != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				BtcReplay.ds.Close();
			}
		}
		catch (Exception)
		{
		}
		try
		{
			bool flag8 = BtcReplay.fs != null;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				BtcReplay.fs.Close();
			}
		}
		catch (Exception)
		{
		}
		BtcReplay.ds = null;
		BtcReplay.fs = null;
		if (delete)
		{
			try
			{
				bool flag15 = !string.IsNullOrEmpty(BtcReplay.wPath) && File.Exists(BtcReplay.wPath);
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				if (flag21)
				{
					File.Delete(BtcReplay.wPath);
				}
			}
			catch (Exception)
			{
			}
		}
	}

	private static void StartKeyframe()
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		BtcReplay.kfId++;
		BtcReplay.PutMarker(1, BtcReplay.kfId);
		BtcReplay.kfStart = Time.unscaledTime;
		BtcReplay.kfSpawnDone = false;
		BtcReplay.kfConn = BtcReplay.AddRecConn(1);
		stopwatch.Stop();
		bool flag = BtcReplay.kfConn == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcReplay.PutMarker(3, BtcReplay.kfId);
			BtcReplay.nextKf = Time.unscaledTime + 60f;
			BtcReplay.Log("keyframe " + BtcReplay.kfId + ": cannot add connection");
		}
		else
		{
			BtcReplay.kfCount++;
			BtcReplay.nextKf = ((BtcReplay.KeyMinutes > 0) ? ((float)BtcReplay.KeyMinutes * 60f) : 300f) + Time.unscaledTime;
			BtcReplay.Log(string.Concat(new object[]
			{
				"keyframe ",
				BtcReplay.kfId,
				" started, host stall ",
				stopwatch.ElapsedMilliseconds,
				" ms"
			}));
		}
	}

	private static void EndKeyframe()
	{
		BtcReplay.PutMarker(3, BtcReplay.kfId);
		BtcReplay.RemoveRecConn(BtcReplay.kfConn);
		BtcReplay.kfConn = null;
		BtcReplay.Log("keyframe " + BtcReplay.kfId + " closed");
	}

	private static void Ensure(int n)
	{
		bool flag = BtcReplay.bufLen + n > BtcReplay.buf.Length;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			Array.Resize<byte>(ref BtcReplay.buf, Mathf.Max(BtcReplay.buf.Length * 2, BtcReplay.bufLen + n + 1024));
		}
	}

	private static void Put7(uint v)
	{
		BtcReplay.Ensure(5);
		while (v >= 128U)
		{
			BtcReplay.buf[BtcReplay.bufLen++] = (byte)(v | 128U);
			v >>= 7;
		}
		BtcReplay.buf[BtcReplay.bufLen++] = (byte)v;
	}

	/// <summary>Recording timeline in seconds. MP: wall clock (vanilla MP cannot pause or change speed, so wall time == game time).
	/// SP: game time (Time.time), so the SP clock (pause, x1.5/x2/x5, focus-loss pause) does not stretch or squeeze the replay:
	/// the stream then plays back at the speed the world really moved, like an MP replay.</summary>
	/// <summary>Recording timeline position (seconds), -1 when not recording (BtcTimer timeline events).</summary>
	internal static float RecTime
	{
		get { return (BtcReplay.mode == BtcReplay.State.Recording) ? BtcReplay.RecClock() : -1f; }
	}

	private static long tlOff;

	/// <summary>Overwrites the reserved "tl=" header region with the timeline events (same length, header size unchanged).</summary>
	private static void PatchTimeline()
	{
		try
		{
			byte[] b = Encoding.ASCII.GetBytes(BtcTimer.TimelinePayload());
			if (BtcReplay.tlOff < 9L || b.Length == 0 || b.Length > BtcTimer.PadLen) return;
			long p = BtcReplay.fs.Position;
			BtcReplay.fs.Flush();
			BtcReplay.fs.Seek(BtcReplay.tlOff, SeekOrigin.Begin);
			BtcReplay.fs.Write(b, 0, b.Length);
			BtcReplay.fs.Seek(p, SeekOrigin.Begin);
		}
		catch (Exception ex) { BtcReplay.Log("PatchTimeline: " + ex.Message); }
	}

	private static float RecClock()
	{
		return BtcReplay.recGameClock ? (Time.time - BtcReplay.recG0) : (Time.unscaledTime - BtcReplay.recT0);
	}

	private static uint Dt()
	{
		float t = BtcReplay.RecClock();
		uint num = (t > 0f) ? (uint)(t * 1000f) : 0U;
		if (num < BtcReplay.lastMs)
		{
			num = BtcReplay.lastMs;
		}
		uint result = num - BtcReplay.lastMs;
		BtcReplay.lastMs = num;
		return result;
	}

	private static void PutMarker(byte type, int id)
	{
		BtcReplay.Ensure(12);
		BtcReplay.buf[BtcReplay.bufLen++] = type;
		BtcReplay.Put7(BtcReplay.Dt());
		BtcReplay.Put7((uint)id);
	}

	public static void OnPacket(int tag, byte[] bytes, int n, int ch)
	{
		bool flag = BtcReplay.mode != BtcReplay.State.Recording || n <= 0;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcReplay.Ensure(n + 16);
			bool flag8 = tag == 0;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				BtcReplay.buf[BtcReplay.bufLen++] = 0;
				BtcReplay.Put7(BtcReplay.Dt());
			}
			else
			{
				BtcReplay.buf[BtcReplay.bufLen++] = 2;
				BtcReplay.Put7(BtcReplay.Dt());
				BtcReplay.Put7((uint)BtcReplay.kfId);
				BtcReplay.ScanSpawnDone(bytes, n);
			}
			BtcReplay.buf[BtcReplay.bufLen++] = (byte)ch;
			BtcReplay.Put7((uint)n);
			BtcReplay.Ensure(n);
			Buffer.BlockCopy(bytes, 0, BtcReplay.buf, BtcReplay.bufLen, n);
			BtcReplay.bufLen += n;
			BtcReplay.packets++;
			BtcReplay.rawBytes += (long)n;
			bool flag15 = BtcReplay.bufLen >= 262144;
			bool flag16 = flag15;
			bool flag17 = flag16;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			if (flag21)
			{
				BtcReplay.FlushBuf();
			}
		}
	}

	private static void FlushBuf()
	{
		bool flag = BtcReplay.bufLen == 0;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			byte[] array = new byte[BtcReplay.bufLen];
			Buffer.BlockCopy(BtcReplay.buf, 0, array, 0, BtcReplay.bufLen);
			BtcReplay.bufLen = 0;
			object obj = BtcReplay.qLock;
			object obj2 = obj;
			object obj3 = obj2;
			object obj4 = obj3;
			object obj5 = obj4;
			object obj6 = obj5;
			object obj7 = obj6;
			lock (obj7)
			{
				BtcReplay.q.Enqueue(array);
				Monitor.Pulse(BtcReplay.qLock);
			}
		}
	}

	private static void WriterLoop()
	{
		for (;;)
		{
			byte[] array = null;
			object obj = BtcReplay.qLock;
			object obj2 = obj;
			object obj3 = obj2;
			object obj4 = obj3;
			object obj5 = obj4;
			object obj6 = obj5;
			object obj7 = obj6;
			lock (obj7)
			{
				while (BtcReplay.q.Count == 0 && !BtcReplay.wClosing)
				{
					Monitor.Wait(BtcReplay.qLock);
				}
				bool flag = BtcReplay.q.Count == 0;
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				bool flag7 = flag6;
				if (flag7)
				{
					break;
				}
				array = BtcReplay.q.Dequeue();
			}
			bool flag8 = !BtcReplay.wAbort;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				try
				{
					BtcReplay.ds.Write(array, 0, array.Length);
					BtcReplay.outBytes = BtcReplay.fs.Position;
				}
				catch (Exception)
				{
				}
			}
		}
		try
		{
			bool flag15 = BtcReplay.wAbort;
			bool flag16 = flag15;
			bool flag17 = flag16;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			if (flag21)
			{
				BtcReplay.CloseWriterNow(true);
			}
			else
			{
				BtcReplay.ds.Close();
				BtcReplay.ds = null;
				try
				{
					byte[] povBlock = BtcPovRec.BuildBlob();
					if (povBlock != null && povBlock.Length > 0)
					{
						BtcReplay.fs.Write(povBlock, 0, povBlock.Length);
					}
				}
				catch (Exception)
				{
				}
				int value = 0;
				bool flag22 = BtcReplay.footSnap > 0 && BtcReplay.snapZip != null && File.Exists(BtcReplay.snapZip);
				bool flag23 = flag22;
				bool flag24 = flag23;
				bool flag25 = flag24;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				if (flag28)
				{
					byte[] array2 = File.ReadAllBytes(BtcReplay.snapZip);
					BtcReplay.fs.Write(array2, 0, array2.Length);
					value = array2.Length;
					try
					{
						File.Delete(BtcReplay.snapZip);
					}
					catch (Exception)
					{
					}
				}
				BtcReplay.fs.Write(Encoding.ASCII.GetBytes("BTCE"), 0, 4);
				BtcReplay.fs.Write(BitConverter.GetBytes(BtcReplay.footDur), 0, 4);
				BtcReplay.fs.Write(BitConverter.GetBytes(BtcReplay.footPackets), 0, 4);
				BtcReplay.fs.Write(BitConverter.GetBytes(BtcReplay.footRaw), 0, 8);
				BtcReplay.fs.Write(BitConverter.GetBytes(BtcReplay.footKf), 0, 4);
				BtcReplay.fs.Write(BitConverter.GetBytes(value), 0, 4);
				BtcReplay.PatchTimeline();
				BtcReplay.fs.Close();
				BtcReplay.fs = null;
			}
		}
		catch (Exception)
		{
			BtcReplay.CloseWriterNow(false);
		}
	}

	public static BtcReplay.Data ReadHeader(string path)
	{
		BtcReplay.Data data = new BtcReplay.Data();
		data.FilePath = path;
		data.FileName = System.IO.Path.GetFileName(path);
		try
		{
			FileInfo fileInfo = new FileInfo(path);
			data.FileBytes = fileInfo.Length;
			data.Ticks = fileInfo.LastWriteTime.Ticks;
			using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				byte[] array = new byte[9];
				bool flag = fileStream.Read(array, 0, 9) < 9 || array[0] != 66 || array[1] != 84 || array[2] != 67 || array[3] != 82 || array[4] != 3;
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				bool flag7 = flag6;
				if (flag7)
				{
					data.Error = BtcReplay.T("old format", "старый формат");
					return data;
				}
				int num = BitConverter.ToInt32(array, 5);
				bool flag8 = num < 0 || num > 65536 || (long)(9 + num) > fileStream.Length;
				bool flag9 = flag8;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				if (flag14)
				{
					data.Error = BtcReplay.T("damaged", "повреждён");
					return data;
				}
				byte[] array2 = new byte[num];
				fileStream.Read(array2, 0, num);
				data.HeaderEnd = 9 + num;
				string[] array3 = Encoding.UTF8.GetString(array2).Split(new char[]
				{
					'\n'
				});
				for (int i = 0; i < array3.Length; i++)
				{
					int num2 = array3[i].IndexOf('=');
					bool flag15 = num2 > 0;
					bool flag16 = flag15;
					bool flag17 = flag16;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					if (flag21)
					{
						string a = array3[i].Substring(0, num2);
						string text = array3[i].Substring(num2 + 1);
						bool flag22 = a == "map";
						bool flag23 = flag22;
						bool flag24 = flag23;
						bool flag25 = flag24;
						bool flag26 = flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						if (flag28)
						{
							data.Map = text;
						}
						else
						{
							bool flag29 = a == "mapName";
							bool flag30 = flag29;
							bool flag31 = flag30;
							bool flag32 = flag31;
							bool flag33 = flag32;
							bool flag34 = flag33;
							bool flag35 = flag34;
							if (flag35)
							{
								data.MapName = text;
							}
							else
							{
								bool flag36 = a == "mode";
								bool flag37 = flag36;
								bool flag38 = flag37;
								bool flag39 = flag38;
								bool flag40 = flag39;
								bool flag41 = flag40;
								bool flag42 = flag41;
								if (flag42)
								{
									data.Mode = text;
								}
								else
								{
									bool flag43 = a == "scene";
									bool flag44 = flag43;
									bool flag45 = flag44;
									bool flag46 = flag45;
									bool flag47 = flag46;
									bool flag48 = flag47;
									bool flag49 = flag48;
									if (flag49)
									{
										data.Scene = text;
									}
									else
									{
										bool flag50 = a == "mp";
										bool flag51 = flag50;
										bool flag52 = flag51;
										bool flag53 = flag52;
										bool flag54 = flag53;
										bool flag55 = flag54;
										bool flag56 = flag55;
										if (flag56)
										{
											data.Multiplayer = (text == "1");
										}
										else
										{
											bool flag57 = a == "newGame";
											bool flag58 = flag57;
											bool flag59 = flag58;
											bool flag60 = flag59;
											bool flag61 = flag60;
											bool flag62 = flag61;
											bool flag63 = flag62;
											if (flag63)
											{
												data.NewGame = (text == "1");
											}
											else
											{
												bool flag64 = a == "ticks";
												bool flag65 = flag64;
												bool flag66 = flag65;
												bool flag67 = flag66;
												bool flag68 = flag67;
												bool flag69 = flag68;
												bool flag70 = flag69;
												if (flag70)
												{
													long.TryParse(text, NumberStyles.Integer, BtcReplay.Inv, out data.Ticks);
												}
												else
												{
													if (a == "pl2") data.Pl2 = text;
													if (a == "tl") data.Tl = text;
													bool flag71 = a == "players" && text.Length > 0;
													bool flag72 = flag71;
													bool flag73 = flag72;
													bool flag74 = flag73;
													bool flag75 = flag74;
													bool flag76 = flag75;
													bool flag77 = flag76;
													if (flag77)
													{
														string[] array4 = text.Split(new char[]
														{
															'|'
														});
														for (int j = 0; j < array4.Length; j++)
														{
															string text2 = Regex.Replace(array4[j], "<[^>]*>?", "").Trim();
															data.Players.Add((text2.Length > 0) ? text2 : "?");
														}
													}
												}
											}
										}
									}
								}
							}
						}
					}
				}
				bool flag78 = fileStream.Length >= (long)(data.HeaderEnd + 28);
				bool flag79 = flag78;
				bool flag80 = flag79;
				bool flag81 = flag80;
				bool flag82 = flag81;
				bool flag83 = flag82;
				bool flag84 = flag83;
				if (flag84)
				{
					fileStream.Seek(-28L, SeekOrigin.End);
					byte[] array5 = new byte[28];
					fileStream.Read(array5, 0, 28);
					bool flag85 = array5[0] == 66 && array5[1] == 84 && array5[2] == 67 && array5[3] == 69;
					bool flag86 = flag85;
					bool flag87 = flag86;
					bool flag88 = flag87;
					bool flag89 = flag88;
					bool flag90 = flag89;
					bool flag91 = flag90;
					if (flag91)
					{
						data.Complete = true;
						data.Duration = BitConverter.ToSingle(array5, 4);
						data.Packets = BitConverter.ToInt32(array5, 8);
						data.RawBytes = BitConverter.ToInt64(array5, 12);
						data.Keyframes = BitConverter.ToInt32(array5, 20);
						int num3 = BitConverter.ToInt32(array5, 24);
						bool flag92 = num3 > 0 && (long)num3 <= fileStream.Length - (long)data.HeaderEnd - 28L;
						bool flag93 = flag92;
						bool flag94 = flag93;
						bool flag95 = flag94;
						bool flag96 = flag95;
						bool flag97 = flag96;
						bool flag98 = flag97;
						if (flag98)
						{
							data.SnapBytes = num3;
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			data.Error = ex.Message;
		}
		return data;
	}

	public static long RecBytes
	{
		get
		{
			return BtcReplay.outBytes + (long)BtcReplay.bufLen;
		}
	}

	public static int RecKeyframes
	{
		get
		{
			return BtcReplay.kfCount;
		}
	}

	public static int RecPackets
	{
		get
		{
			return BtcReplay.packets;
		}
	}

	private static FieldInfo FindField(Type t, string name)
	{
		while (t != null)
		{
			FieldInfo field = t.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			bool flag = field != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				return field;
			}
			t = t.BaseType;
		}
		return null;
	}

	private static bool EnsureSpectatorFaction()
	{
		bool result;
		try
		{
			MethodInfo method = typeof(BtcSpectate).GetMethod("EnsureSpectatorFaction", BindingFlags.Static | BindingFlags.NonPublic);
			bool flag = method == null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				result = (Faction.Spectator != null);
			}
			else
			{
				result = (bool)method.Invoke(null, null);
			}
		}
		catch (Exception arg)
		{
			BtcReplay.Log("EnsureSpectatorFaction: " + arg);
			result = false;
		}
		return result;
	}

	private static void MarkSpec(int connId)
	{
		try
		{
			FieldInfo field = typeof(BtcSpectate).GetField("_spec", BindingFlags.Static | BindingFlags.NonPublic);
			HashSet<int> hashSet = (field != null) ? (field.GetValue(null) as HashSet<int>) : null;
			bool flag = hashSet != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				hashSet.Add(connId);
			}
			FieldInfo field2 = typeof(BtcSpectate).GetField("_names", BindingFlags.Static | BindingFlags.NonPublic);
			Dictionary<int, string> dictionary = (field2 != null) ? (field2.GetValue(null) as Dictionary<int, string>) : null;
			bool flag8 = dictionary != null;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				dictionary[connId] = "REC";
			}
		}
		catch (Exception)
		{
		}
	}

	private static void UnmarkSpec(int connId)
	{
		try
		{
			FieldInfo field = typeof(BtcSpectate).GetField("_spec", BindingFlags.Static | BindingFlags.NonPublic);
			HashSet<int> hashSet = (field != null) ? (field.GetValue(null) as HashSet<int>) : null;
			bool flag = hashSet != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				hashSet.Remove(connId);
			}
			FieldInfo field2 = typeof(BtcSpectate).GetField("_names", BindingFlags.Static | BindingFlags.NonPublic);
			Dictionary<int, string> dictionary = (field2 != null) ? (field2.GetValue(null) as Dictionary<int, string>) : null;
			bool flag8 = dictionary != null;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				dictionary.Remove(connId);
			}
		}
		catch (Exception)
		{
		}
	}

	private static bool SetupRecSpectator(BtcReplay.RecConn c)
	{
		bool flag = !BtcReplay.RecSpectator;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool result;
		if (flag7)
		{
			result = false;
		}
		else
		{
			try
			{
				bool flag8 = !BtcReplay.EnsureSpectatorFaction();
				bool flag9 = flag8;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				if (flag14)
				{
					BtcReplay.Log("no spectator faction, recording the raw stream only");
					result = false;
				}
				else
				{
					GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(UNetManager.instance.playerPrefab);
					NetworkServer.ReplacePlayerForConnection(c, gameObject, 0);
					NetworkServer.SpawnWithClientAuthority(gameObject, c);
					NetworkServer.SendToClient(c.connectionId, 54, new UnetUserMessageHandler.AssignUserObjectMessage(gameObject));
					User component = gameObject.GetComponent<User>();
					component.Network_screenName = "REC";
					BtcLobby.MakeSpectator(component);
					component.NetworkReady = true;
					c.HasUser = true;
					BtcReplay.MarkSpec(c.connectionId);
					BtcReplay.relSent = 0;
					BtcReplay.relT0 = Time.unscaledTime;
					BtcReplay.Log("recorder spectator user created (conn " + c.connectionId + ")");
					result = true;
				}
			}
			catch (Exception arg)
			{
				BtcReplay.Log("SetupRecSpectator: " + arg);
				result = false;
			}
		}
		return result;
	}

	private static void RecSpectatorTick(float now)
	{
		bool flag = BtcReplay.mainConn == null || !BtcReplay.mainConn.HasUser || BtcReplay.relSent >= 3;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			float[] array = new float[]
			{
				3f,
				8f,
				14f
			};
			bool flag8 = now - BtcReplay.relT0 < array[BtcReplay.relSent];
			bool flag9 = !flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				BtcReplay.relSent++;
				try
				{
					MethodInfo method = typeof(BtcSpectate).GetMethod("SendRelations", BindingFlags.Static | BindingFlags.NonPublic);
					bool flag15 = method != null;
					bool flag16 = flag15;
					bool flag17 = flag16;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					if (flag21)
					{
						method.Invoke(null, new object[]
						{
							BtcReplay.mainConn.connectionId
						});
					}
				}
				catch (Exception arg)
				{
					BtcReplay.Log("SendRelations: " + arg);
				}
			}
		}
	}

	private static string BuildSnapshot()
	{
		Asset_Map asset_Map = Asset_Map.CreateShellFrom(GameParam.Map, Asset_Map.MapType.Save);
		FieldInfo fieldInfo = BtcReplay.FindField(typeof(Asset_Map), "thumbnail");
		bool flag = fieldInfo != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			fieldInfo.SetValue(asset_Map, null);
		}
		asset_Map.MoveToStaging();
		BtcReplay.snapShell = asset_Map;
		MethodInfo method = typeof(Asset_Map).GetMethod("Disk_Save", BindingFlags.Instance | BindingFlags.NonPublic);
		bool flag8 = method == null;
		bool flag9 = flag8;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		if (flag14)
		{
			throw new Exception("Asset_Map.Disk_Save not found");
		}
		method.Invoke(asset_Map, null);
		FieldInfo fieldInfo2 = BtcReplay.FindField(asset_Map.GetType(), "_stagePath");
		string text = (fieldInfo2 != null) ? ((string)fieldInfo2.GetValue(asset_Map)) : null;
		bool flag15 = string.IsNullOrEmpty(text) || !Directory.Exists(text);
		bool flag16 = flag15;
		bool flag17 = flag16;
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		if (flag21)
		{
			throw new Exception("staging folder not found");
		}
		try
		{
			MethodInfo method2 = typeof(BtcSpectate).GetMethod("CopyHeightmap", BindingFlags.Static | BindingFlags.NonPublic);
			bool flag22 = method2 != null;
			bool flag23 = flag22;
			bool flag24 = flag23;
			bool flag25 = flag24;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			if (flag28)
			{
				method2.Invoke(null, new object[]
				{
					text
				});
			}
		}
		catch (Exception ex)
		{
			BtcReplay.Log("CopyHeightmap: " + ex.Message);
		}
		return text;
	}

	private static void StartSnapshot()
	{
		BtcReplay.snapLen = 0;
		BtcReplay.snapDone = false;
		BtcReplay.snapOk = false;
		BtcReplay.snapZip = null;
		try
		{
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			string src = BtcReplay.BuildSnapshot();
			BtcReplay.snapZip = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "btcrec_" + DateTime.Now.Ticks.ToString(BtcReplay.Inv) + ".zip");
			string dst = BtcReplay.snapZip;
			BtcReplay.Log("snapshot staged in " + (Time.realtimeSinceStartup - realtimeSinceStartup).ToString("0.00", BtcReplay.Inv) + " s: " + src);
			BtcReplay.snapThread = new Thread(delegate()
			{
				try
				{
					lzip.compressDir(src, 10, dst, false, null, false);
					BtcReplay.snapOk = File.Exists(dst);
				}
				catch (Exception)
				{
					BtcReplay.snapOk = false;
				}
				BtcReplay.snapDone = true;
			});
			BtcReplay.snapThread.IsBackground = true;
			BtcReplay.snapThread.Start();
		}
		catch (Exception arg)
		{
			BtcReplay.Log("StartSnapshot: " + arg);
			BtcReplay.snapDone = true;
			BtcReplay.snapOk = false;
			BtcReplay.CancelShell();
		}
	}

	private static void CancelShell()
	{
		try
		{
			bool flag = BtcReplay.snapShell != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				BtcReplay.snapShell.CancelStaging();
			}
		}
		catch (Exception)
		{
		}
		BtcReplay.snapShell = null;
	}

	private static void PollSnapshot()
	{
		bool flag = BtcReplay.snapDone && BtcReplay.snapShell != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcReplay.CancelShell();
			bool flag8 = BtcReplay.snapOk;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				try
				{
					BtcReplay.snapLen = (int)new FileInfo(BtcReplay.snapZip).Length;
				}
				catch (Exception)
				{
					BtcReplay.snapLen = 0;
				}
			}
			BtcReplay.Log("snapshot ready: " + BtcReplay.snapLen + " bytes");
		}
	}

	private static void DropSnapshotFile()
	{
		try
		{
			bool flag = !string.IsNullOrEmpty(BtcReplay.snapZip) && File.Exists(BtcReplay.snapZip);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				File.Delete(BtcReplay.snapZip);
			}
		}
		catch (Exception)
		{
		}
		BtcReplay.snapZip = null;
		BtcReplay.snapLen = 0;
	}

	private static void ScanSpawnDone(byte[] b, int n)
	{
		int num = 0;
		while (num + 4 <= n)
		{
			int num2 = (int)b[num] | (int)b[num + 1] << 8;
			int num3 = (int)b[num + 2] | (int)b[num + 3] << 8;
			bool flag = num3 == 12 && num2 >= 1 && num + 4 < n && b[num + 4] == 1;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				BtcReplay.kfSpawnDone = true;
				BtcReplay.kfDoneAt = Time.unscaledTime;
			}
			num += 4 + num2;
		}
	}

	public static void StartReplay(BtcReplay.Data d)
	{
		try
		{
			bool flag = d == null || d.Error != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcReplay.Say(BtcReplay.T("This file cannot be played.", "Этот файл нельзя проиграть."));
			}
			else
			{
				bool flag9 = !d.Complete;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					BtcReplay.Say(BtcReplay.T("The replay is incomplete (the game did not finish writing it).", "Реплей не завершён (игра не дописала файл)."));
				}
				else
				{
					bool flag17 = d.SnapBytes <= 0;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						BtcReplay.Say(BtcReplay.T("This replay has no world snapshot (recorded by an older version).", "В этом реплее нет снимка мира (записан старой версией)."));
					}
					else
					{
						bool flag25 = !BtcReplay.CanStartReplay();
						bool flag26 = flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						if (flag32)
						{
							BtcReplay.Say(BtcReplay.T("Leave the current game first.", "Сначала выйдите из текущей игры."));
						}
						else
						{
							BtcReplay.playData = d;
							BtcReplayNames.Load(d.Pl2);
							BtcTimer.LoadTl(d.Tl);
							BtcReplay.playTotal = d.Duration;
							BtcReplay.playClock = 0f;
							BtcReplay.playSpeed = 1f;
							BtcReplay.playPaused = false;
							BtcReplay.playEnded = false;
							BtcReplay.playBuf = null;
							BtcReplay.playPos = 0;
							BtcReplay.playMsAcc = 0L;
							BtcReplay.playHavePend = false;
							BtcReplay.playMap = null;
							BtcReplay.playMapSet = false;
							BtcReplay.playErrors = 0;
							BtcReplay.playFed = 0;
							BtcReplay.playSceneOk = -1f;
							BtcReplay.playSeenGame = false;
							BtcReplay.playT0 = Time.unscaledTime;
							BtcReplay.playThread = 0;
							BtcReplay.playErr = null;
							BtcReplay.playGo = false;
							BtcReplay.pendingUserSet = false;
							BtcReplay.feedErrKeys.Clear();
							BtcReplay.sceneMsgLogged = 0;
							BtcReplay.seekAfterLoad = -1f;
							BtcReplay.playSeeking = false;
							BtcReplay.displacedId = null;
							BtcReplay.playSid = 700000 + (int)(DateTime.Now.Ticks % 90000L);
							BtcReplay.sigHooked = false;
							BtcReplay.terrainDirty = false;
							BtcReplay.rwActive = false;
							BtcReplay.playStage = 1;
							BtcReplay.mode = BtcReplay.State.Playing;
							BtcReplay.Active = true;
							BtcReplayFix.OnStart(d.FilePath);
							BtcReplay.playStatus = BtcReplay.T("Unpacking the replay...", "Распаковка реплея...");
							BtcReplay.Log(string.Concat(new object[]
							{
								"StartReplay: ",
								d.FilePath,
								" snapshot ",
								d.SnapBytes,
								" bytes, sid ",
								BtcReplay.playSid
							}));
							string zip = "Info/Downloads/Maps/btcspec_" + BtcReplay.playSid + ".zip";
							string dir = "Info/Downloads/Maps/btcspec_" + BtcReplay.playSid + "/";
							BtcReplay.playDir = dir;
							Directory.CreateDirectory("Info/Downloads/Maps/");
							BtcReplay.Data dd = d;
							new Thread(delegate()
							{
								try
								{
									byte[] buffer;
									byte[] bytes;
									using (FileStream fileStream = new FileStream(dd.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
									{
										long num = fileStream.Length - 28L - (long)dd.SnapBytes;
										int num2 = (int)(num - (long)dd.HeaderEnd);
										buffer = new byte[num2];
										fileStream.Seek((long)dd.HeaderEnd, SeekOrigin.Begin);
										BtcReplay.ReadFully(fileStream, buffer, num2);
										bytes = new byte[dd.SnapBytes];
										BtcReplay.ReadFully(fileStream, bytes, dd.SnapBytes);
									}
									File.WriteAllBytes(zip, bytes);
									Directory.CreateDirectory(dir);
									int[] progress = new int[]
									{
										10
									};
									lzip.decompress_File(zip, dir, progress, null, null, null);
									File.Delete(zip);
									int povLen = BtcPovRec.BlobLenFromTl(dd.Tl);
									int streamLen = buffer.Length;
									BtcPovRec.SetBlob(null);
									if (povLen > 0 && povLen < buffer.Length)
									{
										byte[] povBytes = new byte[povLen];
										Buffer.BlockCopy(buffer, buffer.Length - povLen, povBytes, 0, povLen);
										streamLen = buffer.Length - povLen;
										BtcPovRec.SetBlob(povBytes);
									}
									MemoryStream compressedStream = new MemoryStream(buffer, 0, streamLen);
									DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
									MemoryStream memoryStream = new MemoryStream();
									byte[] array = new byte[65536];
									int count;
									while ((count = deflateStream.Read(array, 0, array.Length)) > 0)
									{
										memoryStream.Write(array, 0, count);
									}
									BtcReplay.playBuf = memoryStream.ToArray();
									BtcReplay.playThread = 1;
								}
								catch (Exception ex2)
								{
									BtcReplay.playErr = ex2.Message;
									BtcReplay.playThread = 2;
								}
							})
							{
								IsBackground = true
							}.Start();
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			BtcReplay.Log("StartReplay: " + ex);
			BtcReplay.PlayFail(ex.Message);
		}
	}

	private static void ReadFully(Stream s, byte[] buf, int count)
	{
		int num;
		for (int i = 0; i < count; i += num)
		{
			num = s.Read(buf, i, count - i);
			bool flag = num <= 0;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				throw new EndOfStreamException();
			}
		}
	}

	public static void TogglePause()
	{
		bool flag = BtcReplay.mode == BtcReplay.State.Playing;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcReplay.playPaused = !BtcReplay.playPaused;
		}
	}

	public static void SpeedStep(int dir)
	{
		float[] array = new float[]
		{
			0.25f,
			0.5f,
			1f,
			2f,
			4f,
			8f
		};
		int num = 2;
		for (int i = 0; i < array.Length; i++)
		{
			bool flag = Mathf.Abs(array[i] - BtcReplay.playSpeed) < 0.01f;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				num = i;
			}
		}
		num = Mathf.Clamp(num + dir, 0, array.Length - 1);
		BtcReplay.playSpeed = array[num];
	}

	public static void StopReplay()
	{
		BtcReplay.EndPlayback(true);
	}

	private static void PlayFail(string why)
	{
		BtcReplay.Log("Playback failed: " + why);
		BtcReplay.Say(BtcReplay.T("Replay failed: ", "Ошибка реплея: ") + why);
		BtcReplay.EndPlayback(true);
	}

	private static void EndPlayback(bool disconnect)
	{
		bool flag = BtcReplay.mode != BtcReplay.State.Playing;
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
			BtcReplay.mode = BtcReplay.State.Idle;
			BtcReplay.Active = false;
			BtcReplay.playStage = 0;
			BtcReplay.playSeeking = false;
			BtcReplay.rwHold = false;
			BtcReplay.rwActive = false;
			BtcReplay.rwStep = 0;
			BtcReplay.rwCo.Clear();
			BtcSpectate.RewindKeep = false;
			BtcReplay.playBuf = null;
			BtcReplayAnchor.Clear();
			BtcReplay.playEnded = false;
			BtcReplay.playStatus = null;
			try
			{
				BtcReplay.CallSpec("StopHold", new object[]
				{
					false,
					"replay ended"
				});
				BtcReplay.SetSpec("_cState", 0);
				BtcReplay.SetSpec("_cSpectator", false);
			}
			catch (Exception)
			{
			}
			try
			{
				bool flag11 = disconnect && UNetManager.instance != null && BtcReplay.rclient != null;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				if (flag20)
				{
					UNetManager.instance.Disconnect();
				}
			}
			catch (Exception ex)
			{
				BtcReplay.Log("Disconnect: " + ex.Message);
			}
			BtcReplay.rclient = null;
			BtcReplay.rconn = null;
			string dir = BtcReplay.playDir;
			BtcReplay.playDir = null;
			bool flag21 = !string.IsNullOrEmpty(dir);
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			bool flag25 = flag24;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			if (flag30)
			{
				new Thread(delegate()
				{
					try
					{
						Thread.Sleep(3000);
						bool flag31 = Directory.Exists(dir);
						bool flag32 = flag31;
						bool flag33 = flag32;
						bool flag34 = flag33;
						bool flag35 = flag34;
						bool flag36 = flag35;
						bool flag37 = flag36;
						bool flag38 = flag37;
						bool flag39 = flag38;
						bool flag40 = flag39;
						if (flag40)
						{
							Directory.Delete(dir, true);
						}
					}
					catch (Exception)
					{
					}
				})
				{
					IsBackground = true
				}.Start();
			}
			BtcPov.Shutdown();
			BtcTerrainUndo.Clear();
			BtcReplayBoost.Clear();
			BtcReplay.Log("Playback ended");
			BtcReplayFix.OnEnd();
		}
	}

	private static void SetSpec(string name, object value)
	{
		FieldInfo field = typeof(BtcSpectate).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
		bool flag = field != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			field.SetValue(null, value);
		}
	}

	private static object GetSpec(string name)
	{
		FieldInfo field = typeof(BtcSpectate).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
		return (field != null) ? field.GetValue(null) : null;
	}

	private static void ApplyMap()
	{
		bool flag = !BtcReplay.playMapSet && BtcReplay.playMap != null && GameParam.instance != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			GameParam.Map = BtcReplay.playMap;
			BtcReplay.playMapSet = true;
			BtcReplay.Log("GameParam.Map set to the snapshot");
		}
	}

	private static void OnAssignUser(NetworkMessage m)
	{
		NetworkInstanceId networkInstanceId = default(NetworkInstanceId);
		try
		{
			m.reader.SeekZero();
			networkInstanceId = m.reader.ReadNetworkId();
			m.reader.SeekZero();
		}
		catch (Exception)
		{
		}
		UnetUserMessageHandler.AssignUserObjectMessage assignUserObjectMessage = m.ReadMessage<UnetUserMessageHandler.AssignUserObjectMessage>();
		bool flag = assignUserObjectMessage != null && assignUserObjectMessage.obj != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			User.LocalUser = assignUserObjectMessage.obj.GetComponent<User>();
			BtcReplay.Log("LocalUser assigned from the stream");
		}
		else
		{
			BtcReplay.pendingUser = networkInstanceId;
			BtcReplay.pendingUserSet = true;
			BtcReplay.Log("AssignUser: object " + networkInstanceId.Value + " is not spawned yet, will retry");
		}
	}

	private static bool ConnectFake()
	{
		UNetManager instance = UNetManager.instance;
		bool flag = instance == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool result;
		if (flag7)
		{
			BtcReplay.PlayFail("no UNetManager");
			result = false;
		}
		else
		{
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			instance.ResetPersistantNetworkSceneObjects();
			MethodInfo method = typeof(UNetManager).GetMethod("ManuallyInitTransport", BindingFlags.Instance | BindingFlags.NonPublic);
			bool flag8 = method != null;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				method.Invoke(instance, null);
			}
			instance.Initialize();
			FieldInfo field = typeof(UNetManager).GetField("mode", bindingAttr);
			bool flag15 = field != null && field.FieldType.IsEnum;
			bool flag16 = flag15;
			bool flag17 = flag16;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			if (flag21)
			{
				field.SetValue(instance, Enum.Parse(field.FieldType, "Client"));
			}
			Neo.multiplayer = true;
			HostTopology hostTopology = new HostTopology(instance.connectionConfig, 8);
			BtcReplay.ReplayConn replayConn = new BtcReplay.ReplayConn();
			replayConn.Initialize("btc-replay", 0, 1, hostTopology);
			NetworkClient networkClient = new NetworkClient(replayConn);
			BtcReplay.rconn = replayConn;
			BtcReplay.rclient = networkClient;
			BtcReplay.Log(string.Concat(new object[]
			{
				"fake client created, handlers ",
				networkClient.handlers.Count,
				", connected ",
				networkClient.isConnected.ToString()
			}));
			PropertyInfo property = typeof(UNetManager).GetProperty("LocalNetworkClient", bindingAttr);
			bool flag22 = property != null;
			bool flag23 = flag22;
			bool flag24 = flag23;
			bool flag25 = flag24;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			if (flag28)
			{
				property.SetValue(instance, networkClient, null);
			}
			else
			{
				FieldInfo fieldInfo = BtcReplay.FindField(typeof(UNetManager), "LocalNetworkClient");
				bool flag29 = fieldInfo != null;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				bool flag33 = flag32;
				bool flag34 = flag33;
				bool flag35 = flag34;
				if (flag35)
				{
					fieldInfo.SetValue(instance, networkClient);
				}
			}
			FieldInfo[] fields = typeof(NetworkManager).GetFields(bindingAttr);
			for (int i = 0; i < fields.Length; i++)
			{
				bool flag36 = fields[i].FieldType == typeof(NetworkClient) && !fields[i].IsStatic;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				bool flag41 = flag40;
				bool flag42 = flag41;
				if (flag42)
				{
					fields[i].SetValue(instance, networkClient);
				}
			}
			try
			{
				PropertyInfo property2 = typeof(NetworkManager).GetProperty("isNetworkActive", bindingAttr);
				bool flag43 = property2 != null && property2.CanWrite;
				bool flag44 = flag43;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				bool flag48 = flag47;
				bool flag49 = flag48;
				if (flag49)
				{
					property2.SetValue(property2.GetSetMethod(true).IsStatic ? null : instance, true, null);
				}
				else
				{
					FieldInfo fieldInfo2 = BtcReplay.FindField(typeof(NetworkManager), "isNetworkActive");
					bool flag50 = fieldInfo2 != null;
					bool flag51 = flag50;
					bool flag52 = flag51;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					if (flag56)
					{
						fieldInfo2.SetValue(fieldInfo2.IsStatic ? null : instance, true);
					}
				}
			}
			catch (Exception ex)
			{
				BtcReplay.Log("isNetworkActive: " + ex.Message);
			}
			bool flag57 = networkClient.handlers.Count == 0;
			bool flag58 = flag57;
			bool flag59 = flag58;
			bool flag60 = flag59;
			bool flag61 = flag60;
			bool flag62 = flag61;
			bool flag63 = flag62;
			if (flag63)
			{
				MethodInfo method2 = typeof(NetworkClient).GetMethod("RegisterSystemHandlers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				bool flag64 = method2 != null;
				bool flag65 = flag64;
				bool flag66 = flag65;
				bool flag67 = flag66;
				bool flag68 = flag67;
				bool flag69 = flag68;
				bool flag70 = flag69;
				if (flag70)
				{
					method2.Invoke(networkClient, new object[]
					{
						false
					});
					BtcReplay.Log("system handlers registered by hand, now " + networkClient.handlers.Count);
				}
			}
			MethodInfo method3 = typeof(NetworkManager).GetMethod("RegisterClientMessages", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			bool flag71 = method3 != null;
			bool flag72 = flag71;
			bool flag73 = flag72;
			bool flag74 = flag73;
			bool flag75 = flag74;
			bool flag76 = flag75;
			bool flag77 = flag76;
			if (flag77)
			{
				method3.Invoke(instance, new object[]
				{
					networkClient
				});
			}
			else
			{
				BtcReplay.Log("RegisterClientMessages not found");
			}
			try
			{
				instance.OnStartClient(networkClient);
			}
			catch (Exception ex2)
			{
				BtcReplay.Log("OnStartClient: " + ex2.Message);
			}
			try
			{
				BtcSpectate.RegisterHandlers(networkClient);
				NetworkMessageDelegate networkMessageDelegate = BtcReplay.GetSpec("_orig") as NetworkMessageDelegate;
				NetworkMessageDelegate networkMessageDelegate2;
				bool flag78 = networkMessageDelegate != null && networkClient.handlers.TryGetValue(39, out networkMessageDelegate2) && networkMessageDelegate2 != null && networkMessageDelegate2.Method.DeclaringType == typeof(BtcSpectate);
				bool flag79 = flag78;
				bool flag80 = flag79;
				bool flag81 = flag80;
				bool flag82 = flag81;
				bool flag83 = flag82;
				bool flag84 = flag83;
				if (flag84)
				{
					networkClient.handlers[39] = networkMessageDelegate;
					BtcReplay.Log("Scene handler restored to the original");
				}
			}
			catch (Exception ex3)
			{
				BtcReplay.Log("BtcSpectate.RegisterHandlers: " + ex3.Message);
			}
			networkClient.RegisterHandler(54, new NetworkMessageDelegate(BtcReplay.OnAssignUser));
			BtcMatchStatsNet.RegisterReplayHandler(networkClient);
			try
			{
				GameSignals.Invoke(UNetManager.ClientConnectedSelf);
			}
			catch (Exception ex4)
			{
				BtcReplay.Log("ClientConnectedSelf: " + ex4.Message);
			}
			BtcReplay.SetSpec("_cSid", BtcReplay.playSid);
			MethodInfo method4 = typeof(BtcSpectate).GetMethod("FindSnapshot", BindingFlags.Static | BindingFlags.NonPublic);
			BtcReplay.playMap = ((method4 != null) ? (method4.Invoke(null, null) as Asset_Map) : null);
			bool flag85 = BtcReplay.playMap == null;
			bool flag86 = flag85;
			bool flag87 = flag86;
			bool flag88 = flag87;
			bool flag89 = flag88;
			bool flag90 = flag89;
			bool flag91 = flag90;
			if (flag91)
			{
				BtcReplay.PlayFail(BtcReplay.T("The snapshot map was not found after unpacking.", "Карта из снимка не найдена после распаковки."));
				result = false;
			}
			else
			{
				BtcReplay.Log("snapshot map: " + BtcReplay.playMap.path);
				BtcReplay.SetSpec("_pendingMap", BtcReplay.playMap);
				BtcReplay.SetSpec("_mapApplied", false);
				BtcReplay.SetSpec("_cSpectator", true);
				BtcReplay.SetSpec("_cReturn", false);
				BtcReplay.SetSpec("_cT0", Time.unscaledTime);
				BtcReplay.SetSpec("_cState", 5);
				BtcReplay.ApplyMap();
				BtcReplay.CallSpec("ResetClientReady", new object[0]);
				BtcReplay.CallSpec("StartHold", new object[0]);
				replayConn.InvokeHandlerNoData(32);
				string text = string.IsNullOrEmpty(BtcReplay.playData.Scene) ? "ScenePrincipale" : BtcReplay.playData.Scene;
				NetworkWriter networkWriter = new NetworkWriter();
				new BtcSpectate.SceneStrMsg(text).Serialize(networkWriter);
				NetworkMessage networkMessage = new NetworkMessage();
				networkMessage.msgType = 39;
				networkMessage.conn = replayConn;
				networkMessage.reader = new NetworkReader(networkWriter.ToArray());
				networkMessage.channelId = 0;
				NetworkMessageDelegate networkMessageDelegate3 = BtcReplay.GetSpec("_orig") as NetworkMessageDelegate;
				bool flag92 = networkMessageDelegate3 == null;
				bool flag93 = flag92;
				bool flag94 = flag93;
				bool flag95 = flag94;
				bool flag96 = flag95;
				bool flag97 = flag96;
				bool flag98 = flag97;
				if (flag98)
				{
					networkClient.handlers.TryGetValue(39, out networkMessageDelegate3);
				}
				bool flag99 = networkMessageDelegate3 != null;
				bool flag100 = flag99;
				bool flag101 = flag100;
				bool flag102 = flag101;
				bool flag103 = flag102;
				bool flag104 = flag103;
				bool flag105 = flag104;
				if (flag105)
				{
					networkMessageDelegate3(networkMessage);
					BtcReplay.Log("Scene '" + text + "' sent to the loader");
					BtcReplay.playStage = 2;
					BtcReplay.playT0 = Time.unscaledTime;
					BtcReplay.playStatus = BtcReplay.T("Loading the world...", "Загрузка мира...");
					result = true;
				}
				else
				{
					BtcReplay.PlayFail("no Scene handler on the fake client");
					result = false;
				}
			}
		}
		return result;
	}

	private static void PlayTick(float now)
	{
		BtcPov.Tick(now);
		BtcReplayAnchor.Tick(now, BtcReplay.playGo, BtcReplay.playSeeking, BtcReplay.rwActive);
		try
		{
			switch (BtcReplay.playStage)
			{
			case 1:
			{
				bool flag = BtcReplay.playThread == 2;
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					BtcReplay.PlayFail(BtcReplay.playErr ?? "unpack failed");
				}
				else
				{
					bool flag9 = BtcReplay.playThread == 1;
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						BtcReplay.ConnectFake();
					}
				}
				break;
			}
			case 2:
			{
				BtcReplay.ApplyMap();
				bool flag17 = now - BtcReplay.playT0 > 150f;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					BtcReplay.PlayFail(BtcReplay.T("The world did not load in time.", "Мир не загрузился вовремя."));
				}
				else
				{
					bool flag25 = SceneManager.GetActiveScene().buildIndex == 2 && GameParam.instance != null;
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						bool flag33 = BtcReplay.playSceneOk < 0f;
						bool flag34 = flag33;
						bool flag35 = flag34;
						bool flag36 = flag35;
						bool flag37 = flag36;
						bool flag38 = flag37;
						bool flag39 = flag38;
						bool flag40 = flag39;
						if (flag40)
						{
							BtcReplay.playSceneOk = now;
						}
						else
						{
							bool flag41 = now - BtcReplay.playSceneOk > 1.5f;
							bool flag42 = flag41;
							bool flag43 = flag42;
							bool flag44 = flag43;
							bool flag45 = flag44;
							bool flag46 = flag45;
							bool flag47 = flag46;
							bool flag48 = flag47;
							if (flag48)
							{
								BtcReplay.BeginFeed(now);
							}
						}
					}
					else
					{
						BtcReplay.playSceneOk = -1f;
					}
				}
				break;
			}
			case 3:
				BtcReplay.ApplyMap();
				BtcReplay.FeedTick(now);
				break;
			case 4:
				BtcReplay.RewindTick(now);
				break;
			}
		}
		catch (Exception ex)
		{
			BtcReplay.Log("PlayTick: " + ex);
			BtcReplay.PlayFail(ex.Message);
		}
	}

	private static void BeginFeed(float now)
	{
		BtcReplay.PrepareSceneObjects();
		BtcReplay.playStage = 3;
		BtcReplay.playClock = 0f;
		BtcReplay.playLastHb = now;
		BtcReplay.playGo = false;
		BtcReplay.playFeedT0 = now;
		BtcReplay.playStatus = BtcReplay.T("Loading the world...", "Загрузка мира...");
		BtcReplay.SetSpec("_hudAt", now + 3f);
		BtcReplay.SetSpec("_hudPass", 0);
		BtcReplay.SetSpec("_probeAt", now + 6f);
		BtcReplay.SetSpec("_fixNext", 0f);
		BtcReplay.Log("scene is up, feeding the recorded stream");
	}

	private static uint ReadV7(ref int p)
	{
		uint num = 0U;
		int num2 = 0;
		while (p < BtcReplay.playBuf.Length)
		{
			byte[] array = BtcReplay.playBuf;
			int num3 = p;
			p = num3 + 1;
			byte b = array[num3];
			num |= (uint)((uint)(b & 127) << num2);
			bool flag = b < 128;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				break;
			}
			num2 += 7;
		}
		return num;
	}

	private static bool Peek()
	{
		bool flag = BtcReplay.playHavePend;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool result;
		if (flag7)
		{
			result = true;
		}
		else
		{
			bool flag8 = BtcReplay.playBuf == null || BtcReplay.playPos >= BtcReplay.playBuf.Length;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				result = false;
			}
			else
			{
				int num = BtcReplay.playPos;
				BtcReplay.pendType = (int)BtcReplay.playBuf[num++];
				uint num2 = BtcReplay.ReadV7(ref num);
				BtcReplay.pendAbs = BtcReplay.playMsAcc + (long)((ulong)num2);
				BtcReplay.pendBody = num;
				BtcReplay.playHavePend = true;
				result = true;
			}
		}
		return result;
	}

	private static void Consume()
	{
		int num = BtcReplay.pendBody;
		int num2 = BtcReplay.pendType;
		BtcReplay.playMsAcc = BtcReplay.pendAbs;
		BtcReplay.playHavePend = false;
		int num3 = BtcReplayAnchor.OnRecord(num2, BtcReplay.playBuf, num);
		bool flag = num2 == 0;
		if (flag)
		{
			int ch = (int)BtcReplay.playBuf[num++];
			int num4 = (int)BtcReplay.ReadV7(ref num);
			byte[] array = new byte[num4];
			Buffer.BlockCopy(BtcReplay.playBuf, num, array, 0, num4);
			num += num4;
			BtcReplay.playPos = num;
			bool flag2 = num3 != 0;
			if (flag2)
			{
				BtcReplay.FeedPacket(array, num4, ch);
			}
		}
		else
		{
			bool flag3 = num2 == 2;
			if (flag3)
			{
				BtcReplay.ReadV7(ref num);
				int ch2 = (int)BtcReplay.playBuf[num++];
				int num5 = (int)BtcReplay.ReadV7(ref num);
				bool flag4 = num3 != 0;
				if (flag4)
				{
					byte[] array2 = new byte[num5];
					Buffer.BlockCopy(BtcReplay.playBuf, num, array2, 0, num5);
					BtcReplay.FeedPacket(array2, num5, ch2);
				}
				BtcReplay.playPos = num + num5;
			}
			else
			{
				BtcReplay.ReadV7(ref num);
				BtcReplay.playPos = num;
			}
		}
	}

	private static bool HasMsgType(byte[] b, int n, int type)
	{
		int num = 0;
		while (num + 4 <= n)
		{
			int num2 = (int)b[num] | (int)b[num + 1] << 8;
			int num3 = (int)b[num + 2] | (int)b[num + 3] << 8;
			bool flag = num3 == type;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				return true;
			}
			num += 4 + num2;
		}
		return false;
	}

	private static void FeedPacket(byte[] data, int len, int ch)
	{
		bool flag = BtcReplay.HasMsgType(data, len, 64);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcReplay.playLastHb = Time.unscaledTime;
		}
		int num = 0;
		while (num + 4 <= len)
		{
			int num2 = (int)data[num] | (int)data[num + 1] << 8;
			int num3 = (int)data[num + 2] | (int)data[num + 3] << 8;
			int num4 = 4 + num2;
			bool flag8 = num + num4 > len;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				num4 = len - num;
			}
			byte[] array = new byte[num4];
			Buffer.BlockCopy(data, num, array, 0, num4);
			bool flag15 = num3 == 10;
			bool flag16 = flag15;
			bool flag17 = flag16;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			if (flag20)
			{
				BtcReplay.FixSceneSpawn(array, num4);
			}
			try
			{
				BtcReplay.rconn.TransportReceive(array, num4, ch);
				BtcReplay.playFed++;
			}
			catch (Exception ex)
			{
				BtcReplay.playErrors++;
				BtcReplay.LogFeedError(num3, num2, ex);
			}
			num += num4;
		}
	}

	private static void FeedTick(float now)
	{
		int buildIndex = SceneManager.GetActiveScene().buildIndex;
		bool flag = buildIndex == 2;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcReplay.playSeenGame = true;
			BtcReplay.playLeftAt = -1f;
		}
		else
		{
			bool flag9 = BtcReplay.playSeenGame;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				bool flag17 = BtcReplay.playLeftAt < 0f;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					BtcReplay.playLeftAt = now;
				}
				else
				{
					bool flag25 = now - BtcReplay.playLeftAt > 3f;
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						BtcReplay.EndPlayback(false);
						return;
					}
				}
			}
		}
		bool flag33 = !BtcReplay.playGo;
		bool flag34 = flag33;
		bool flag35 = flag34;
		bool flag36 = flag35;
		bool flag37 = flag36;
		bool flag38 = flag37;
		bool flag39 = flag38;
		bool flag40 = flag39;
		if (flag40)
		{
			bool flag41 = false;
			try
			{
				flag41 = (GameSignals.Received<Action>(GameSignals.LevelReady) && User.LocalUser != null);
			}
			catch (Exception)
			{
			}
			bool flag42 = !BtcReplay.rwHold && (flag41 || now - BtcReplay.playFeedT0 > 150f);
			bool flag43 = flag42;
			bool flag44 = flag43;
			bool flag45 = flag44;
			bool flag46 = flag45;
			bool flag47 = flag46;
			bool flag48 = flag47;
			bool flag49 = flag48;
			if (flag49)
			{
				BtcReplay.playGo = true;
				BtcReplay.playStatus = null;
				BtcReplay.HookSignals();
				BtcReplay.terrainDirty = false;
				BtcReplay.terrainBaseHash = BtcReplay.TerrainFingerprint("baseline");
				BtcReplayAnchor.OnBaseline(BtcReplay.playBuf, (BtcReplay.playData != null) ? BtcReplay.playData.FilePath : null);
				BtcReplay.Log(string.Concat(new object[]
				{
					"the loader finished (",
					flag41 ? "level ready" : "timeout",
					"), the clock starts; fed ",
					BtcReplay.playFed,
					", errors ",
					BtcReplay.playErrors,
					", LocalUser ",
					(User.LocalUser != null).ToString()
				}));
			}
		}
		BtcReplay.RetryUser();
		bool flag50 = BtcReplay.rwActive;
		bool flag51 = flag50;
		bool flag52 = flag51;
		bool flag53 = flag52;
		bool flag54 = flag53;
		if (flag54)
		{
			BtcReplay.RewindGuard(now);
		}
		bool flag55 = BtcReplay.rwHold;
		bool flag56 = flag55;
		bool flag57 = flag56;
		bool flag58 = flag57;
		bool flag59 = flag58;
		bool flag60 = flag59;
		if (flag60)
		{
			BtcReplay.RewindHoldTick(now);
		}
		bool flag61 = BtcReplay.camRestoreAt > 0f && now >= BtcReplay.camRestoreAt;
		bool flag62 = flag61;
		bool flag63 = flag62;
		bool flag64 = flag63;
		bool flag65 = flag64;
		bool flag66 = flag65;
		bool flag67 = flag66;
		if (flag67)
		{
			BtcReplay.camRestoreAt = 0f;
			BtcReplay.RestoreCamera();
			BtcReplay.camChain = null;
		}
		bool flag68 = !BtcReplay.playGo;
		bool flag69 = flag68;
		bool flag70 = flag69;
		bool flag71 = flag70;
		bool flag72 = flag71;
		bool flag73 = flag72;
		bool flag74 = flag73;
		if (flag74)
		{
			BtcReplay.ForceHostFlags();
		}
		bool flag75 = !BtcReplay.playGo && now >= BtcReplay.playDiagNext;
		bool flag76 = flag75;
		bool flag77 = flag76;
		bool flag78 = flag77;
		bool flag79 = flag78;
		bool flag80 = flag79;
		bool flag81 = flag80;
		bool flag82 = flag81;
		if (flag82)
		{
			BtcReplay.playDiagNext = now + 3f;
			BtcReplay.LoadDiag();
		}
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		bool flag83 = BtcReplay.playGo && BtcReplay.seekAfterLoad >= 0f;
		bool flag84 = flag83;
		bool flag85 = flag84;
		bool flag86 = flag85;
		bool flag87 = flag86;
		bool flag88 = flag87;
		bool flag89 = flag88;
		if (flag89)
		{
			float t = BtcReplay.seekAfterLoad;
			BtcReplay.seekAfterLoad = -1f;
			BtcReplay.StartSeekForward(t);
		}
		bool flag90 = BtcReplay.playSeeking && BtcReplay.playGo;
		bool flag91 = !flag90 && !BtcReplay.playPaused && !BtcReplay.playEnded;
		bool flag92 = flag91;
		bool flag93 = flag92;
		bool flag94 = flag93;
		bool flag95 = flag94;
		bool flag96 = flag95;
		bool flag97 = flag96;
		bool flag98 = flag97;
		if (flag98)
		{
			BtcReplay.playClock += Mathf.Min(unscaledDeltaTime, 0.25f) * BtcReplay.playSpeed;
			bool flag99 = !BtcReplay.playGo && BtcReplay.playClock > 5f;
			bool flag100 = flag99;
			bool flag101 = flag100;
			bool flag102 = flag101;
			bool flag103 = flag102;
			bool flag104 = flag103;
			bool flag105 = flag104;
			bool flag106 = flag105;
			if (flag106)
			{
				BtcReplay.playClock = 5f;
			}
		}
		bool flag107 = flag90;
		bool flag108 = flag107;
		bool flag109 = flag108;
		bool flag110 = flag109;
		bool flag111 = flag110;
		bool flag112 = flag111;
		bool flag113 = flag112;
		if (flag113)
		{
			BtcReplay.SeekStep();
		}
		bool flag114 = !flag90 && !BtcReplay.playPaused;
		bool flag115 = flag114;
		bool flag116 = flag115;
		bool flag117 = flag116;
		bool flag118 = flag117;
		bool flag119 = flag118;
		bool flag120 = flag119;
		bool flag121 = flag120;
		if (flag121)
		{
			long num = (long)(BtcReplay.playClock * 1000f);
			int num2 = 0;
			while (num2++ < 6000 && BtcReplay.Peek() && BtcReplay.pendAbs <= num)
			{
				BtcReplay.Consume();
			}
			bool flag122 = !BtcReplay.playEnded && !BtcReplay.Peek();
			bool flag123 = flag122;
			bool flag124 = flag123;
			bool flag125 = flag124;
			bool flag126 = flag125;
			bool flag127 = flag126;
			bool flag128 = flag127;
			bool flag129 = flag128;
			if (flag129)
			{
				BtcReplay.playEnded = true;
				BtcReplay.Log(string.Concat(new object[]
				{
					"end of the recording reached, ",
					BtcReplay.playFed,
					" packets, ",
					BtcReplay.playErrors,
					" errors"
				}));
			}
		}
		bool flag130 = now - BtcReplay.playLastHb > 0.8f;
		bool flag131 = flag130;
		bool flag132 = flag131;
		bool flag133 = flag132;
		bool flag134 = flag133;
		bool flag135 = flag134;
		bool flag136 = flag135;
		bool flag137 = flag136;
		if (flag137)
		{
			BtcReplay.playLastHb = now;
			try
			{
				NetworkConnection networkConnection = BtcReplay.rconn;
				byte[] array = new byte[4];
				array[2] = 64;
				networkConnection.TransportReceive(array, 4, 0);
			}
			catch (Exception)
			{
			}
		}
	}

	public static bool Loading
	{
		get
		{
			return BtcReplay.mode == BtcReplay.State.Playing && (BtcReplay.playStage < 3 || BtcReplay.playStage == 4 || !BtcReplay.playGo);
		}
	}

	public static bool Running
	{
		get
		{
			return BtcReplay.mode == BtcReplay.State.Playing && BtcReplay.playStage == 3 && BtcReplay.playGo;
		}
	}

	public static bool Finished
	{
		get
		{
			return BtcReplay.playEnded;
		}
	}

	public static float PlayClock
	{
		get
		{
			return BtcReplay.playClock;
		}
	}

	public static float PlayTotal
	{
		get
		{
			return BtcReplay.playTotal;
		}
	}

	public static float PlaySpeed
	{
		get
		{
			return BtcReplay.playSpeed;
		}
	}

	public static bool PlayPaused
	{
		get
		{
			return BtcReplay.playPaused;
		}
	}

	public static string PlayStatus
	{
		get
		{
			return BtcReplay.playStatus;
		}
	}

	private static void LogFeedError(int type, int size, Exception ex)
	{
		string text = ex.StackTrace ?? "";
		int num = text.IndexOf('\n');
		string item = string.Concat(new object[]
		{
			type,
			"|",
			ex.GetType().Name,
			"|",
			(num > 0) ? text.Substring(0, num) : text
		});
		bool flag = BtcReplay.feedErrKeys.Count >= 20 || !BtcReplay.feedErrKeys.Add(item);
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcReplay.Log(string.Concat(new object[]
			{
				"feed error: msg type ",
				type,
				" size ",
				size,
				" (after ",
				BtcReplay.playFed,
				" ok): ",
				ex
			}));
		}
	}

	private static void CallSpec(string name, params object[] args)
	{
		try
		{
			MethodInfo method = typeof(BtcSpectate).GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			bool flag = method == null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				BtcReplay.Log("BtcSpectate." + name + " not found");
			}
			else
			{
				method.Invoke(null, args);
			}
		}
		catch (Exception ex)
		{
			BtcReplay.Log("BtcSpectate." + name + ": " + ((ex.InnerException != null) ? ex.InnerException.Message : ex.Message));
		}
	}

	private static void LoadDiag()
	{
		try
		{
			GameParam instance = GameParam.instance;
			BtcReplay.Log(string.Concat(new object[]
			{
				"loading: LocalUser=",
				User.LocalUser != null,
				" gameParam=",
				instance != null,
				" allFactions=",
				instance != null && instance.HostLoadedAllFactions,
				" hostLoaded=",
				instance != null && instance.HostLoaded,
				" ready=",
				ClientScene.ready,
				" objectsLoaded=",
				GameSignals.Received<Action>(GameSignals.GameObjectsLoaded),
				" levelReady=",
				GameSignals.Received<Action>(GameSignals.LevelReady),
				" scene=",
				SceneManager.GetActiveScene().buildIndex,
				" fed=",
				BtcReplay.playFed,
				" errors=",
				BtcReplay.playErrors,
				" clock=",
				BtcReplay.playClock.ToString("0.0", BtcReplay.Inv)
			}));
		}
		catch (Exception ex)
		{
			BtcReplay.Log("LoadDiag: " + ex.Message);
		}
	}

	private static void RetryUser()
	{
		bool flag = !BtcReplay.pendingUserSet || User.LocalUser != null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			GameObject gameObject = ClientScene.FindLocalObject(BtcReplay.pendingUser);
			bool flag8 = gameObject != null;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				User component = gameObject.GetComponent<User>();
				bool flag15 = component != null;
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				if (flag21)
				{
					User.LocalUser = component;
					BtcReplay.pendingUserSet = false;
					BtcReplay.Log("LocalUser assigned on retry");
				}
			}
		}
	}

	private static string SpawnableInfo()
	{
		string result;
		try
		{
			FieldInfo fieldInfo = BtcReplay.FindField(typeof(ClientScene), "s_SpawnableObjects");
			IDictionary dictionary = (fieldInfo != null) ? (fieldInfo.GetValue(null) as IDictionary) : null;
			bool flag = dictionary == null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				result = "null";
			}
			else
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append(dictionary.Count).Append(" [");
				int num = 0;
				foreach (object obj in dictionary)
				{
					DictionaryEntry dictionaryEntry = (DictionaryEntry)obj;
					bool flag8 = num++ >= 12;
					bool flag9 = flag8;
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					if (flag14)
					{
						break;
					}
					UnityEngine.Object @object = dictionaryEntry.Value as UnityEngine.Object;
					stringBuilder.Append(dictionaryEntry.Key).Append('=').Append((@object != null) ? @object.name : "null").Append(' ');
				}
				result = stringBuilder.Append(']').ToString();
			}
		}
		catch (Exception ex)
		{
			result = "err " + ex.Message;
		}
		return result;
	}

	private static void PrepareSceneObjects()
	{
		BtcReplay.Log("scene objects before: " + BtcReplay.SpawnableInfo());
		try
		{
			MethodInfo method = typeof(ClientScene).GetMethod("PrepareToSpawnSceneObjects", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			bool flag = method != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				method.Invoke(null, null);
			}
			else
			{
				BtcReplay.Log("PrepareToSpawnSceneObjects not found");
			}
		}
		catch (Exception ex)
		{
			BtcReplay.Log("PrepareToSpawnSceneObjects: " + ((ex.InnerException != null) ? ex.InnerException.Message : ex.Message));
		}
		BtcReplay.Log("scene objects after: " + BtcReplay.SpawnableInfo());
		BtcReplay.IdentityInfo();
	}

	private static void IdentityInfo()
	{
		try
		{
			NetworkIdentity[] array = Resources.FindObjectsOfTypeAll<NetworkIdentity>();
			int num = 0;
			int num2 = 0;
			while (num2 < array.Length && num < 40)
			{
				NetworkIdentity networkIdentity = array[num2];
				bool flag = networkIdentity == null || networkIdentity.sceneId.IsEmpty();
				bool flag2 = !flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					num++;
					BtcReplay.Log(string.Concat(new object[]
					{
						"identity: ",
						networkIdentity.name,
						" sceneId=",
						networkIdentity.sceneId.Value,
						" active=",
						networkIdentity.gameObject.activeSelf.ToString(),
						" isClient=",
						networkIdentity.isClient.ToString(),
						" scene=",
						networkIdentity.gameObject.scene.name
					}));
				}
				num2++;
			}
			GameParam instance = GameParam.instance;
			NetworkIdentity networkIdentity2 = (instance != null) ? instance.GetComponent<NetworkIdentity>() : null;
			BtcReplay.Log("GameParam identity: " + ((networkIdentity2 == null) ? "none" : string.Concat(new object[]
			{
				"sceneId=",
				networkIdentity2.sceneId.Value,
				" active=",
				networkIdentity2.gameObject.activeSelf.ToString(),
				" isClient=",
				networkIdentity2.isClient.ToString(),
				" netId=",
				networkIdentity2.netId.Value
			})));
		}
		catch (Exception ex)
		{
			BtcReplay.Log("IdentityInfo: " + ex.Message);
		}
	}

	private static void FixSceneSpawn(byte[] one, int total)
	{
		try
		{
			bool flag = total <= 4;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				byte[] array = new byte[total - 4];
				Buffer.BlockCopy(one, 4, array, 0, total - 4);
				NetworkReader networkReader = new NetworkReader(array);
				NetworkInstanceId networkInstanceId = networkReader.ReadNetworkId();
				NetworkSceneId networkSceneId = networkReader.ReadSceneId();
				FieldInfo fieldInfo = BtcReplay.FindField(typeof(ClientScene), "s_SpawnableObjects");
				IDictionary dictionary = (fieldInfo != null) ? (fieldInfo.GetValue(null) as IDictionary) : null;
				bool flag7 = dictionary == null;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					BtcReplay.Log("scene spawn sid=" + networkSceneId.Value + ": no spawnable list");
				}
				else
				{
					bool flag13 = dictionary.Contains(networkSceneId);
					string str = "";
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					bool flag17 = flag16;
					bool flag18 = flag17;
					bool flag19 = flag18;
					if (flag19)
					{
						UnityEngine.Object @object = dictionary[networkSceneId] as UnityEngine.Object;
						str = ((@object != null) ? @object.name : "null");
					}
					GameParam instance = GameParam.instance;
					NetworkIdentity networkIdentity = (instance != null) ? instance.GetComponent<NetworkIdentity>() : null;
					bool flag20 = networkIdentity != null && (!networkIdentity.gameObject.activeSelf || networkIdentity.netId.IsEmpty()) && (networkIdentity.sceneId.Equals(networkSceneId) || !flag13);
					bool flag21 = false;
					bool flag22 = flag20;
					bool flag23 = flag22;
					bool flag24 = flag23;
					bool flag25 = flag24;
					bool flag26 = flag25;
					bool flag27 = flag26;
					if (flag27)
					{
						bool flag28 = flag13;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						bool flag33 = flag32;
						if (flag33)
						{
							BtcReplay.displacedId = (dictionary[networkSceneId] as NetworkIdentity);
							BtcReplay.displacedSid = networkSceneId.Value;
						}
						dictionary[networkSceneId] = networkIdentity;
					}
					else
					{
						bool flag34 = !flag13 && BtcReplay.displacedId != null && BtcReplay.displacedSid == networkSceneId.Value;
						bool flag35 = flag34;
						bool flag36 = flag35;
						bool flag37 = flag36;
						bool flag38 = flag37;
						bool flag39 = flag38;
						if (flag39)
						{
							dictionary[networkSceneId] = BtcReplay.displacedId;
							BtcReplay.displacedId = null;
							flag21 = true;
						}
					}
					bool flag40 = BtcReplay.sceneMsgLogged++ < 12;
					bool flag41 = flag40;
					bool flag42 = flag41;
					bool flag43 = flag42;
					bool flag44 = flag43;
					bool flag45 = flag44;
					if (flag45)
					{
						BtcReplay.Log(string.Concat(new object[]
						{
							"scene spawn: netId=",
							networkInstanceId.Value,
							" sceneId=",
							networkSceneId.Value,
							" size=",
							total,
							" inList=",
							flag13.ToString(),
							flag13 ? ("(" + str + ")") : "",
							" -> ",
							flag20 ? "bound to GameParam" : (flag21 ? "restored displaced object" : "as is")
						}));
					}
				}
			}
		}
		catch (Exception ex)
		{
			BtcReplay.Log("FixSceneSpawn: " + ex.Message);
		}
	}

	private static void ForceHostFlags()
	{
		try
		{
			GameParam instance = GameParam.instance;
			bool flag = instance == null || !instance.HostLoadedAllFactions;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				bool flag7 = !instance.HostLoaded && GameSignals.Received<Action>(GameSignals.GameObjectsLoaded);
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					instance.HostLoaded = true;
					BtcReplay.Log("HostLoaded forced (the recorded state had it false)");
				}
				bool flag13 = instance.HostLoaded && !instance.HostReady && GameSignals.Received<Action>(GameSignals.LevelReady);
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				if (flag18)
				{
					instance.HostReady = true;
					BtcReplay.Log("HostReady forced");
				}
			}
		}
		catch (Exception ex)
		{
			BtcReplay.Log("ForceHostFlags: " + ex.Message);
		}
	}

	public static void SeekTo(float seconds)
	{
		bool flag = BtcReplay.mode != BtcReplay.State.Playing || !BtcReplay.playGo || BtcReplay.playData == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		if (flag6)
		{
			seconds = Mathf.Clamp(seconds, 0f, BtcReplay.playTotal);
			bool flag7 = seconds >= BtcReplay.playClock - 0.001f;
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				bool flag13 = !BtcReplay.playEnded;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				if (flag18)
				{
					BtcReplay.StartSeekForward(seconds);
				}
			}
			else
			{
				BtcReplay.Log(string.Concat(new string[]
				{
					"seek back to ",
					seconds.ToString("0.0", BtcReplay.Inv),
					" s from ",
					BtcReplay.playClock.ToString("0.0", BtcReplay.Inv),
					" s"
				}));
				bool flag19 = BtcReplay.InSceneRewind && BtcReplay.playMap != null;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					BtcReplay.BeginRewind(seconds);
				}
				else
				{
					BtcReplay.RestartViaMenu(seconds);
				}
			}
		}
	}

	private static void StartSeekForward(float t)
	{
		BtcReplay.playSeekTarget = Mathf.Max(t, BtcReplay.playClock);
		BtcReplay.playSeeking = true;
		BtcReplay.playSeekT0 = Time.realtimeSinceStartup;
		BtcReplay.Log(string.Concat(new string[]
		{
			"seek forward ",
			BtcReplay.playClock.ToString("0.0", BtcReplay.Inv),
			" -> ",
			BtcReplay.playSeekTarget.ToString("0.0", BtcReplay.Inv),
			" s"
		}));
	}

	private static void SeekStep()
	{
		long num = (long)(BtcReplay.playSeekTarget * 1000f);
		Stopwatch stopwatch = Stopwatch.StartNew();
		int num2 = 0;
		while (BtcReplay.Peek() && BtcReplay.pendAbs <= num)
		{
			BtcReplay.Consume();
			bool flag = (++num2 & 15) == 0 && stopwatch.ElapsedMilliseconds >= BtcReplayBoost.Budget(BtcReplay.rwActive);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				break;
			}
		}
		bool flag7 = BtcReplay.Peek();
		bool flag8 = flag7 && BtcReplay.pendAbs <= num;
		bool flag9 = flag8;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		if (flag13)
		{
			BtcReplay.playClock = Mathf.Max(BtcReplay.playClock, (float)BtcReplay.playMsAcc / 1000f);
		}
		else
		{
			BtcReplay.playClock = BtcReplay.playSeekTarget;
			BtcReplay.playSeeking = false;
			bool seekWasForward = !BtcReplay.rwActive;
			bool flag14 = BtcReplay.rwActive;
			bool flag15 = flag14;
			bool flag16 = flag15;
			bool flag17 = flag16;
			if (flag17)
			{
				BtcReplay.RewindFinish();
			}
			bool flag18 = BtcReplay.camChain != null;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			if (flag23)
			{
				BtcReplay.RestoreCamera();
				BtcReplay.camRestoreAt = Time.unscaledTime + 0.7f;
			}
			bool flag24 = !flag7;
			bool flag25 = flag24;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			if (flag29)
			{
				BtcReplay.playEnded = true;
			}
			if (seekWasForward)
			{
				try
				{
					BtcReplayAnchor.AfterSeekForward();
				}
				catch (Exception ex)
				{
					BtcLog.Error("Replay", "AfterSeekForward: " + ex.Message);
				}
			}
			BtcReplay.Log(string.Concat(new object[]
			{
				"seek done at ",
				BtcReplay.playClock.ToString("0.0", BtcReplay.Inv),
				" s in ",
				(Time.realtimeSinceStartup - BtcReplay.playSeekT0).ToString("0.0", BtcReplay.Inv),
				" s; fed ",
				BtcReplay.playFed,
				", errors ",
				BtcReplay.playErrors
			}));
		}
	}

	private static void RestartTick(float now)
	{
		bool flag = now - BtcReplay.restartAt > 60f;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		if (flag6)
		{
			BtcReplay.restartData = null;
			BtcReplay.Say(BtcReplay.T("Could not restart the replay for rewinding.", "Не удалось перезапустить реплей для перемотки."));
		}
		else
		{
			bool flag7 = now - BtcReplay.restartAt < 1f || !BtcReplay.CanStartReplay();
			bool flag8 = !flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				BtcReplay.Data d = BtcReplay.restartData;
				float num = BtcReplay.restartSeek;
				BtcReplay.restartData = null;
				BtcReplay.StartReplay(d);
				bool flag13 = BtcReplay.mode == BtcReplay.State.Playing;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				if (flag18)
				{
					BtcReplay.seekAfterLoad = num;
				}
			}
		}
	}

	public static bool Seeking
	{
		get
		{
			return BtcReplay.playSeeking;
		}
	}

	public static float SeekTarget
	{
		get
		{
			return BtcReplay.playSeekTarget;
		}
	}

	private static void RestartViaMenu(float seconds)
	{
		BtcReplay.restartData = BtcReplay.playData;
		BtcReplay.restartSeek = seconds;
		BtcReplay.restartAt = Time.unscaledTime;
		BtcReplay.Log("rewind through the menu: restarting from the snapshot");
		BtcReplay.EndPlayback(true);
	}

	private static void SaveCamera()
	{
		BtcReplay.camChain = null;
		try
		{
			Camera main = Camera.main;
			bool flag = main == null;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				List<Transform> list = new List<Transform>();
				Transform transform = main.transform;
				while (transform != null)
				{
					list.Add(transform);
					transform = transform.parent;
				}
				BtcReplay.camChain = list.ToArray();
				BtcReplay.camPos = new Vector3[list.Count];
				BtcReplay.camRot = new Quaternion[list.Count];
				for (int i = 0; i < list.Count; i++)
				{
					BtcReplay.camPos[i] = list[i].localPosition;
					BtcReplay.camRot[i] = list[i].localRotation;
				}
			}
		}
		catch (Exception ex)
		{
			BtcReplay.Log("SaveCamera: " + ex.Message);
			BtcReplay.camChain = null;
		}
	}

	private static void RestoreCamera()
	{
		try
		{
			bool flag = BtcReplay.camChain == null;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				for (int i = 0; i < BtcReplay.camChain.Length; i++)
				{
					bool flag7 = BtcReplay.camChain[i] != null;
					bool flag8 = flag7;
					bool flag9 = flag8;
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					if (flag12)
					{
						BtcReplay.camChain[i].localPosition = BtcReplay.camPos[i];
						BtcReplay.camChain[i].localRotation = BtcReplay.camRot[i];
					}
				}
			}
		}
		catch (Exception ex)
		{
			BtcReplay.Log("RestoreCamera: " + ex.Message);
		}
	}

	private static void BeginRewind(float target)
	{
		long num = BtcReplay.TerrainFingerprint("rewind");
		bool flag = BtcReplay.terrainBaseHash >= 0L && num >= 0L && num != BtcReplay.terrainBaseHash;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			BtcReplay.Log(string.Concat(new object[]
			{
				"rewind: the terrain was changed (hash ",
				num,
				" vs ",
				BtcReplay.terrainBaseHash,
				"), an in-scene rewind cannot restore it, restarting through the menu"
			}));
			BtcReplay.RestartViaMenu(target);
		}
		else
		{
			BtcReplayAnchor.Select(target);
			BtcReplay.rwTarget = target;
			BtcReplay.rwStep = 0;
			BtcReplay.rwT0 = Time.unscaledTime;
			BtcReplay.rwStart = Time.unscaledTime;
			BtcReplay.rwHold = false;
			BtcReplay.rwCo.Clear();
			BtcReplay.playSeeking = false;
			BtcReplay.playGo = false;
			BtcReplay.rwActive = true;
			BtcReplay.rwGuardHits = 0;
			BtcReplay.rwGuardAt = 0f;
			BtcReplay.HookSignals();
			BtcReplay.SaveCamera();
			BtcReplay.playStage = 4;
			BtcReplay.playStatus = BtcReplay.T("Rewinding...", "Перемотка...");
		}
	}

	private static void RewindTick(float now)
	{
		try
		{
			BtcReplay.RewindBeat(now);
			bool flag = now - BtcReplay.rwStart > 240f;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				throw new Exception("rewind timeout");
			}
			switch (BtcReplay.rwStep)
			{
			case 0:
			{
				bool flag7 = now - BtcReplay.rwT0 >= BtcReplayBoost.Wait0;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					BtcReplay.RewindResetWorld();
					BtcReplay.rwStep = 1;
					BtcReplay.rwT0 = now;
				}
				break;
			}
			case 1:
			{
				bool flag13 = now - BtcReplay.rwT0 >= BtcReplayBoost.Wait1;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				if (flag18)
				{
					bool flag19 = GameParam.instance == null;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						throw new Exception("GameParam was destroyed by the reset");
					}
					BtcReplay.PurgeObserverMaps();
					BtcReplay.rwCo.Clear();
					BtcReplay.rwCo.Push(BtcReplay.RewindLoadWorld());
					BtcReplay.rwStep = 2;
					BtcReplay.Log("rewind: reloading the world the way the loader does");
				}
				break;
			}
			case 2:
			{
				bool flag25 = !BtcReplay.PumpCo();
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				if (flag30)
				{
					BtcReplay.rwStep = 3;
					BtcReplay.rwT0 = now;
				}
				break;
			}
			case 3:
			{
				bool flag31 = now - BtcReplay.rwT0 >= BtcReplayBoost.Wait3;
				bool flag32 = flag31;
				bool flag33 = flag32;
				bool flag34 = flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				if (flag36)
				{
					BtcReplay.rwStep = 4;
					BtcReplay.RewindBeginFeed(now);
				}
				break;
			}
			}
		}
		catch (Exception arg)
		{
			BtcReplayAnchor.OnRewindFailed();
			BtcReplay.Log("RewindTick: " + arg);
			BtcReplay.RewindFallback();
		}
	}

	private static void RewindBeat(float now)
	{
		BtcReplay.RewindGuard(now);
		bool flag = now - BtcReplay.playLastHb > 0.8f && BtcReplay.rconn != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		if (flag6)
		{
			BtcReplay.playLastHb = now;
			try
			{
				byte[] array = new byte[4];
				array[2] = 64;
				BtcReplay.rconn.TransportReceive(array, 4, 0);
			}
			catch (Exception)
			{
			}
		}
	}

	private static bool PumpCo()
	{
		int num = 0;
		while (BtcReplay.rwCo.Count > 0 && num++ < 200000)
		{
			IEnumerator enumerator = BtcReplay.rwCo.Peek();
			bool flag = !enumerator.MoveNext();
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				BtcReplay.rwCo.Pop();
			}
			else
			{
				IEnumerator enumerator2 = enumerator.Current as IEnumerator;
				bool flag6 = enumerator2 == null;
				bool flag7 = flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					return true;
				}
				BtcReplay.rwCo.Push(enumerator2);
			}
		}
		return BtcReplay.rwCo.Count > 0;
	}

	private static IEnumerator LoaderStep(object loader, string name)
	{
		MethodInfo method = typeof(GameLoader).GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		bool flag = method == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			throw new Exception("GameLoader." + name + " not found");
		}
		IEnumerator enumerator = method.Invoke(method.IsStatic ? null : loader, null) as IEnumerator;
		bool flag6 = enumerator == null;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		bool flag10 = flag9;
		if (flag10)
		{
			throw new Exception("GameLoader." + name + " returned no enumerator");
		}
		return enumerator;
	}

	private static void ClearTrees()
	{
		try
		{
			int count = DamagedTree.AllDamagedTrees.Count;
			DamagedTree.AllDamagedTrees.Clear();
			BtcReplay.Log("rewind: dropped " + count + " stale damaged-tree entries");
			Type type = typeof(GameLoader).Assembly.GetType("Brix.Legacy.TreeRegistry");
			object value = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
			FieldInfo field = type.GetField("_treeData", BindingFlags.Instance | BindingFlags.NonPublic);
			IDictionary dictionary = (field != null && value != null) ? (field.GetValue(value) as IDictionary) : null;
			bool flag = dictionary == null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				BtcReplay.Log("rewind: tree registry not cleared (field not found)");
			}
			else
			{
				MethodInfo method = type.GetMethod("Remove", BindingFlags.Instance | BindingFlags.Public);
				List<object> list = new List<object>();
				foreach (object item in dictionary.Keys)
				{
					list.Add(item);
				}
				int num = 0;
				bool flag5 = method != null;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					foreach (object obj in list)
					{
						try
						{
							method.Invoke(value, new object[]
							{
								obj
							});
							num++;
						}
						catch (Exception)
						{
						}
					}
				}
				dictionary.Clear();
				BtcReplay.Log(string.Concat(new object[]
				{
					"rewind: tree registry cleared, ",
					num,
					" of ",
					list.Count,
					" trees removed through TreeChanged"
				}));
			}
		}
		catch (Exception ex)
		{
			BtcReplay.Log("ClearTrees: " + ex.Message);
		}
	}

	private static IEnumerator RewindLoadWorld()
	{
		GameLoader loader = GameLoader._instance;
		bool flag = loader == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag16 = flag7;
		if (flag16)
		{
			throw new Exception("GameLoader instance is gone");
		}
		FieldInfo mapF = typeof(GameLoader).GetField("_mapToLoad", BindingFlags.Instance | BindingFlags.NonPublic);
		bool flag8 = mapF != null && BtcReplay.playMap != null;
		bool flag9 = flag8;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag17 = flag14;
		if (flag17)
		{
			mapF.SetValue(loader, BtcReplay.playMap);
		}
		BtcReplay.Log("rewind: the terrain is unchanged (checked by content), keeping it");
		bool flag15 = !BtcReplayBoost.RestoreNature();
		bool flag18 = flag15;
		if (flag18)
		{
			BtcReplay.ClearTrees();
			BtcReplay.ClearTrees();
			BtcReplay.LoadNatureTolerant(loader);
			BtcTerrainUndo.RestoreTrees();
		}
		BtcTerrainUndo.Dump("rewind: world reloaded");
		BtcReplay.Log("rewind: nature registries rebuilt");
		BtcReplay.terrainDirty = false;
		yield break;
	}

	private static void RewindResetWorld()
	{
		BtcPov.OnRewind();
		BtcMatchStats.ResetForRewind();
		try
		{
			ProjectDatabase.ClearAll();
		}
		catch (Exception)
		{
		}
		GameParam instance = GameParam.instance;
		NetworkIdentity networkIdentity = (instance != null) ? instance.GetComponent<NetworkIdentity>() : null;
		BtcReplay.Log("rewind: GameParam sceneId=" + ((networkIdentity != null) ? networkIdentity.sceneId.Value.ToString() : "none") + " active=" + ((networkIdentity != null) ? networkIdentity.gameObject.activeSelf.ToString() : "-"));
		BtcReplay.ExitObserverCreations();
		BtcReplay.ReleaseTrackedObjects();
		ClientScene.DestroyAllClientObjects();
		User.LocalUser = null;
		BtcReplay.pendingUserSet = false;
		Faction.DefaultNeutral = null;
		Faction.DefaultEnemy = null;
		Faction.Spectator = null;
		BtcFactionFix.Reset();
		BtcReplay.SetSpec("_hudAt", 0f);
		BtcReplay.SetSpec("_hudPass", 0);
		BtcReplay.SetSpec("_probeAt", 0f);
		BtcReplay.SetSpec("_probed2", false);
		BtcReplay.SetSpec("_refireAt", 0f);
		BtcReplay.SetSpec("_fixNext", 0f);
		BtcReplay.Log("rewind: client objects destroyed");
	}

	private static void RewindBeginFeed(float now)
	{
		BtcReplay.playPos = BtcReplayAnchor.StartPos;
		BtcReplay.playMsAcc = BtcReplayAnchor.StartMs;
		BtcReplayAnchor.Begin();
		BtcReplay.playHavePend = false;
		BtcReplay.playClock = 0f;
		BtcReplay.playEnded = false;
		BtcReplay.pendingUserSet = false;
		BtcReplay.feedErrKeys.Clear();
		BtcReplay.sceneMsgLogged = 0;
		BtcReplay.displacedId = null;
		BtcReplay.playMapSet = false;
		BtcReplay.ApplyMap();
		BtcReplay.PrepareSceneObjects();
		BtcReplay.playLastHb = now;
		BtcReplay.playFeedT0 = now;
		BtcReplay.playDiagNext = now;
		BtcReplay.SetSpec("_hudAt", now + 3f);
		BtcReplay.SetSpec("_hudPass", 0);
		BtcReplay.SetSpec("_probeAt", now + 6f);
		BtcReplay.SetSpec("_fixNext", 0f);
		BtcSpectate.RewindKeep = true;
		BtcReplay.CallSpec("StartHold", new object[0]);
		BtcReplay.rwHold = true;
		BtcReplay.rwPopulated = false;
		BtcReplay.rwReleased = false;
		BtcReplay.rwT0 = now;
		BtcReplay.playStage = 3;
		BtcReplay.playGo = false;
		BtcReplay.playStatus = BtcReplay.T("Rewinding...", "Перемотка...");
		BtcReplay.Log("rewind: world ready, feeding the stream; objects are held until the factions are filled in");
	}

	private static bool SpecBool(string name)
	{
		object spec = BtcReplay.GetSpec(name);
		return spec is bool && (bool)spec;
	}

	private static void RewindHoldTick(float now)
	{
		try
		{
			GameParam instance = GameParam.instance;
			bool flag = !BtcReplay.rwPopulated;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				bool flag6 = instance != null && instance.HostLoadedAllFactions && User.LocalUser != null && BtcReplay.SpecBool("_tier2Open") && Faction.GetAllFactions().Count > 0;
				bool flag7 = flag6 && now - BtcReplay.rwT0 > 0.5f;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				bool flag11 = flag10;
				if (flag11)
				{
					try
					{
						SimpleHostScript.PopulateFactionsLocally();
						BtcReplay.Log("rewind: PopulateFactionsLocally done, factions " + Faction.GetAllFactions().Count);
					}
					catch (Exception arg)
					{
						BtcReplay.Log("rewind: PopulateFactionsLocally: " + arg);
					}
					BtcReplay.rwPopulated = true;
					BtcReplay.rwT0 = now;
				}
				else
				{
					bool flag12 = now - BtcReplay.rwT0 > 45f;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						throw new Exception(string.Concat(new string[]
						{
							"factions did not appear in time (allFactions=",
							(instance != null && instance.HostLoadedAllFactions).ToString(),
							", LocalUser=",
							(User.LocalUser != null).ToString(),
							")"
						}));
					}
				}
			}
			else
			{
				bool flag17 = !BtcReplay.rwReleased;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				if (flag21)
				{
					bool flag22 = now - BtcReplay.rwT0 > 0.5f;
					bool flag23 = flag22;
					bool flag24 = flag23;
					bool flag25 = flag24;
					bool flag26 = flag25;
					if (flag26)
					{
						BtcSpectate.RewindKeep = false;
						BtcReplay.rwReleased = true;
						BtcReplay.rwT0 = now;
						BtcReplay.Log("rewind: releasing the held objects");
					}
				}
				else
				{
					bool flag27 = BtcReplay.SpecBool("_hold");
					bool flag28 = now - BtcReplay.rwT0 > 60f;
					bool flag29 = (!flag27 && now - BtcReplay.rwT0 > 0.3f) || flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					bool flag33 = flag32;
					if (flag33)
					{
						BtcReplay.rwHold = false;
						BtcSpectate.RewindKeep = false;
						BtcReplay.playGo = true;
						BtcReplay.playStatus = null;
						BtcReplay.Log(string.Concat(new string[]
						{
							"rewind: objects spawned",
							flag28 ? " (hold timeout)" : "",
							", fast-forwarding to ",
							BtcReplay.rwTarget.ToString("0.0", BtcReplay.Inv),
							" s"
						}));
						BtcReplay.StartSeekForward(BtcReplay.rwTarget);
					}
				}
			}
		}
		catch (Exception arg2)
		{
			BtcReplay.Log("RewindHoldTick: " + arg2);
			BtcReplay.RewindFallback();
		}
	}

	private static void RewindFallback()
	{
		float seconds = BtcReplay.rwTarget;
		BtcReplay.rwHold = false;
		BtcReplay.rwActive = false;
		BtcSpectate.RewindKeep = false;
		BtcReplay.Log("falling back to the menu restart");
		BtcReplay.playStage = 3;
		BtcReplay.RestartViaMenu(seconds);
	}

	private static void HookSignals()
	{
		bool flag = BtcReplay.sigHooked;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcReplay.sigHooked = true;
			try
			{
				GameSignals.Connect<GameSignals.loadingContext>(GameSignals.LoadingGame, delegate(GameSignals.loadingContext ctx)
				{
					BtcReplay.OnLoadingGameSignal(ctx.ToString());
				}, null);
				GameSignals.Connect<XYZ>(GameSignals.VoxelChanged, delegate(XYZ p)
				{
					BtcReplay.terrainDirty = true;
				}, null);
				GameSignals.Connect<XYZ>(GameSignals.TerrainChanged, delegate(XYZ p)
				{
					BtcReplay.terrainDirty = true;
				}, null);
			}
			catch (Exception ex)
			{
				BtcReplay.Log("HookSignals: " + ex.Message);
			}
		}
	}

	private static void OnLoadingGameSignal(string ctx)
	{
		bool flag = BtcReplay.mode != BtcReplay.State.Playing;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			bool flag5 = !BtcReplay.rwActive;
			bool flag6 = !flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcReplay.Log("rewind: LoadingGame signal (" + ctx + ") raised during the rewind, stack: " + new StackTrace(1, false).ToString().Replace("\r", "").Replace("\n", " | "));
				BtcReplay.HideVanillaLoading("LoadingGame " + ctx);
			}
		}
	}

	private static void HideVanillaLoading(string why)
	{
		try
		{
			int num = 0;
			foreach (LoadingScreen loadingScreen in Resources.FindObjectsOfTypeAll<LoadingScreen>())
			{
				bool flag = loadingScreen != null && loadingScreen.gameObject.scene.IsValid();
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				if (flag4)
				{
					loadingScreen.InstantClose();
					num++;
				}
			}
			bool flag5 = BtcReplay.rwGuardHits++ < 8;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcReplay.Log(string.Concat(new object[]
				{
					"rewind: vanilla loading screen closed (",
					why,
					", ",
					num,
					" screens)"
				}));
			}
		}
		catch (Exception ex)
		{
			BtcReplay.Log("HideVanillaLoading: " + ex.Message);
		}
	}

	private static void RewindGuard(float now)
	{
		bool flag = now < BtcReplay.rwGuardAt;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcReplay.rwGuardAt = now + 0.25f;
			BtcReplay.HideVanillaLoading("guard");
		}
	}

	private static void RewindFinish()
	{
		BtcReplay.rwActive = false;
		float unscaledTime = Time.unscaledTime;
		BtcReplay.SetSpec("_hudAt", unscaledTime + 1.5f);
		BtcReplay.SetSpec("_hudPass", 0);
		BtcReplay.SetSpec("_probeAt", unscaledTime + 4f);
		BtcReplay.SetSpec("_fixNext", 0f);
		BtcReplay.Log("rewind: finished at " + BtcReplay.playClock.ToString("0.0", BtcReplay.Inv) + " s, minimap and panel fixes re-armed");
	}

	private static long TerrainFingerprint(string why)
	{
		long result;
		try
		{
			bool flag = why == "baseline";
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				BtcTerrainUndo.Capture();
				BtcReplayBoost.Capture();
			}
			else
			{
				bool flag5 = why == "rewind";
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					bool flag9 = !BtcTerrainUndo.Restore();
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					if (flag12)
					{
						return long.MaxValue;
					}
				}
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			Bounds bounds = Voxel.Engine.ComputeOccupiedBounds();
			int num = Mathf.FloorToInt(bounds.min.x);
			int num2 = Mathf.CeilToInt(bounds.max.x) - 1;
			int num3 = Mathf.FloorToInt(bounds.min.z);
			int num4 = Mathf.CeilToInt(bounds.max.z) - 1;
			ulong num5 = 14695981039346656037UL;
			int num6 = 0;
			for (int i = num; i <= num2; i++)
			{
				for (int j = num3; j <= num4; j++)
				{
					int num7;
					int num8;
					VoxelEngine.GetColumnData(i, j, out num7, out num8);
					num5 = (num5 ^ (ulong)((long)(num7 * 7919 + num8))) * 1099511628211UL;
					num6++;
				}
			}
			long num9 = (long)(num5 & 9223372036854775807UL);
			BtcReplay.Log(string.Concat(new object[]
			{
				"terrain fingerprint (",
				why,
				"): ",
				num9,
				", ",
				num6,
				" columns, ",
				stopwatch.ElapsedMilliseconds,
				" ms"
			}));
			result = num9;
		}
		catch (Exception ex)
		{
			BtcReplay.Log("TerrainFingerprint (" + why + "): " + ex.Message);
			result = -1L;
		}
		return result;
	}

	private static FieldInfo FindFieldUp(Type t, string name)
	{
		while (t != null)
		{
			FieldInfo field = t.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			bool flag = field != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				return field;
			}
			t = t.BaseType;
		}
		return null;
	}

	private static MethodInfo FindMethodUp(Type t, string name)
	{
		while (t != null)
		{
			MethodInfo method = t.GetMethod(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			bool flag = method != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				return method;
			}
			t = t.BaseType;
		}
		return null;
	}

	private static void ExitObserverCreations()
	{
		int num = 0;
		int num2 = 0;
		try
		{
			foreach (FieldInfo fieldInfo in typeof(UIGameObserver).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
			{
				object value = fieldInfo.GetValue(null);
				bool flag = value == null || value.GetType().DeclaringType != typeof(UIGameObserver);
				bool flag2 = !flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				if (flag4)
				{
					FieldInfo fieldInfo2 = BtcReplay.FindFieldUp(value.GetType(), "creationMap");
					MethodInfo methodInfo = BtcReplay.FindMethodUp(value.GetType(), "OnExitCreation");
					IEnumerable enumerable = (fieldInfo2 != null) ? (fieldInfo2.GetValue(value) as IEnumerable) : null;
					bool flag5 = methodInfo == null || enumerable == null;
					bool flag6 = !flag5;
					bool flag7 = flag6;
					bool flag8 = flag7;
					if (flag8)
					{
						List<object> list = new List<object>();
						foreach (object item in enumerable)
						{
							list.Add(item);
						}
						foreach (object obj in list)
						{
							try
							{
								methodInfo.Invoke(value, new object[]
								{
									obj
								});
								num++;
							}
							catch (Exception)
							{
								num2++;
							}
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			BtcReplay.Log("ExitObserverCreations: " + ex.Message);
		}
		BtcReplay.Log(string.Concat(new object[]
		{
			"rewind: told the UI observers about ",
			num,
			" exits, ",
			num2,
			" failed"
		}));
	}

	private static void PurgeObserverMaps()
	{
		int num = 0;
		try
		{
			foreach (FieldInfo fieldInfo in typeof(UIGameObserver).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
			{
				object value = fieldInfo.GetValue(null);
				bool flag = value == null || value.GetType().DeclaringType != typeof(UIGameObserver);
				bool flag2 = !flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				if (flag4)
				{
					FieldInfo fieldInfo2 = BtcReplay.FindFieldUp(value.GetType(), "creationMap");
					object obj = (fieldInfo2 != null) ? fieldInfo2.GetValue(value) : null;
					IEnumerable enumerable = obj as IEnumerable;
					MethodInfo methodInfo = (obj != null) ? obj.GetType().GetMethod("Remove") : null;
					bool flag5 = enumerable == null || methodInfo == null;
					bool flag6 = !flag5;
					bool flag7 = flag6;
					bool flag8 = flag7;
					if (flag8)
					{
						List<object> list = new List<object>();
						foreach (object obj2 in enumerable)
						{
							UnityEngine.Object x = obj2 as UnityEngine.Object;
							bool flag9 = x == null;
							bool flag10 = flag9;
							bool flag11 = flag10;
							bool flag12 = flag11;
							if (flag12)
							{
								list.Add(obj2);
							}
						}
						foreach (object obj3 in list)
						{
							try
							{
								methodInfo.Invoke(obj, new object[]
								{
									obj3
								});
								num++;
							}
							catch (Exception)
							{
							}
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			BtcReplay.Log("PurgeObserverMaps: " + ex.Message);
		}
		BtcReplay.Log("rewind: purged " + num + " destroyed units from the UI observers");
	}

	private static void LoadNatureTolerant(GameLoader loader)
	{
		Asset_Map asset_Map = BtcReplay.playMap;
		Type type = typeof(GameLoader).Assembly.GetType("Brix.Legacy.TreeRegistry");
		object value = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
		MethodInfo methodInfo = null;
		foreach (MethodInfo methodInfo2 in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
		{
			bool flag = methodInfo2.Name == "Add" && methodInfo2.GetParameters().Length == 2;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				methodInfo = methodInfo2;
			}
		}
		MethodInfo method = type.GetMethod("GetPseudoRandomArbreInfo", BindingFlags.Static | BindingFlags.Public);
		Type parameterType = methodInfo.GetParameters()[0].ParameterType;
		ConstructorInfo constructor = parameterType.GetConstructor(new Type[]
		{
			typeof(int),
			typeof(int),
			typeof(int)
		});
		MethodInfo method2 = parameterType.GetMethod("ToScalaire", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
		bool flag5 = constructor == null || method2 == null || method == null;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			throw new Exception("tree loading: XYZ members not found");
		}
		Asset_Map.TreeInfo[] array = asset_Map.Disk_ReadTrees();
		int num = 0;
		int num2 = 0;
		string text = null;
		foreach (Asset_Map.TreeInfo treeInfo in array)
		{
			try
			{
				object obj = constructor.Invoke(new object[]
				{
					treeInfo.x,
					treeInfo.y,
					treeInfo.z
				});
				int num3 = (int)method.Invoke(null, new object[]
				{
					method2.Invoke(obj, null)
				});
				num3 = ((num3 & 15) | Mathf.Clamp(treeInfo.s, 0, 15) << 4);
				bool flag9 = (bool)methodInfo.Invoke(value, new object[]
				{
					obj,
					num3
				});
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					num++;
				}
			}
			catch (Exception ex)
			{
				num2++;
				bool flag13 = text == null;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					text = ((ex.InnerException != null) ? (ex.InnerException.GetType().Name + ": " + ex.InnerException.Message) : ex.Message);
				}
			}
		}
		BtcReplay.Log(string.Concat(new object[]
		{
			"rewind: trees loaded ",
			num,
			" of ",
			array.Length,
			", failed ",
			num2,
			(text != null) ? (" (" + text + ")") : ""
		}));
		bool flag17 = array.Length != 0 && num == 0;
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		if (flag20)
		{
			throw new Exception("no tree could be loaded");
		}
		string[] array3 = new string[]
		{
			"LoadPlants",
			"LoadBoulders",
			"LoadIrons",
			"LoadPyrites"
		};
		foreach (string text2 in array3)
		{
			try
			{
				MethodInfo method3 = typeof(GameLoader).GetMethod(text2, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				method3.Invoke(loader, null);
			}
			catch (Exception ex2)
			{
				BtcReplay.Log("rewind: " + text2 + ": " + ((ex2.InnerException != null) ? ex2.InnerException.Message : ex2.Message));
			}
		}
	}

	private static Type FindType(string name)
	{
		foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			try
			{
				Type type = assembly.GetType(name);
				bool flag = type != null;
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				if (flag4)
				{
					return type;
				}
			}
			catch (Exception)
			{
			}
		}
		return null;
	}

	private static object GetMember(object o, string name)
	{
		BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		Type type = (o is Type) ? ((Type)o) : o.GetType();
		object obj = (o is Type) ? null : o;
		PropertyInfo property = type.GetProperty(name, bindingAttr);
		bool flag = property != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		object result;
		if (flag4)
		{
			result = property.GetValue(obj, null);
		}
		else
		{
			FieldInfo field = type.GetField(name, bindingAttr);
			result = ((field != null) ? field.GetValue(obj) : null);
		}
		return result;
	}

	private static void ReleaseTrackedObjects()
	{
		try
		{
			Type type = BtcReplay.FindType("Brix.Lifecycle.Pooling.ObjectPoolSingleton");
			object obj = (type != null) ? BtcReplay.GetMember(type, "Instance") : null;
			object obj2 = (obj != null) ? BtcReplay.GetMember(obj, "GameObjectReleasedSignal") : null;
			MethodInfo methodInfo = null;
			bool flag = obj2 != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				foreach (MethodInfo methodInfo2 in obj2.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
				{
					bool flag5 = methodInfo2.Name == "Invoke" && methodInfo2.GetParameters().Length == 2;
					bool flag6 = flag5;
					bool flag7 = flag6;
					bool flag8 = flag7;
					if (flag8)
					{
						methodInfo = methodInfo2;
					}
				}
			}
			bool flag9 = methodInfo == null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				BtcReplay.Log("rewind: pool release signal not found, UI icons will not be cleaned");
			}
			else
			{
				List<NetworkIdentity> list = new List<NetworkIdentity>(ClientScene.objects.Values);
				int num = 0;
				int num2 = 0;
				foreach (NetworkIdentity networkIdentity in list)
				{
					bool flag13 = networkIdentity == null;
					bool flag14 = !flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						GameObject gameObject = networkIdentity.gameObject;
						Component component = null;
						foreach (Component component2 in gameObject.GetComponents<Component>())
						{
							bool flag17 = component2 != null && component2.GetType().Name == "FactoryImprint";
							bool flag18 = flag17;
							bool flag19 = flag18;
							bool flag20 = flag19;
							if (flag20)
							{
								component = component2;
								break;
							}
						}
						bool flag21 = component == null;
						bool flag22 = !flag21;
						bool flag23 = flag22;
						bool flag24 = flag23;
						if (flag24)
						{
							try
							{
								object member = BtcReplay.GetMember(component, "AssetKey");
								methodInfo.Invoke(obj2, new object[]
								{
									member,
									gameObject
								});
								num++;
							}
							catch (Exception)
							{
								num2++;
							}
						}
					}
				}
				BtcReplay.Log(string.Concat(new object[]
				{
					"rewind: announced the release of ",
					num,
					" objects to the UI trackers, ",
					num2,
					" failed"
				}));
			}
		}
		catch (Exception ex)
		{
			BtcReplay.Log("ReleaseTrackedObjects: " + ex.Message);
		}
	}

	public static bool Active;

	public static bool RecordEnabled;

	public static bool RecordSingleplayer;

	public static bool ShowIndicator;

	public static string Message;

	public static float MessageUntil;

	public static List<User> ProxyUsers;

	private static readonly CultureInfo Inv;

	private static BtcReplay.State mode;

	private static bool cfgLoaded;

	private static string curPath;

	private static float recT0;

	private static float recG0;

	private static bool recGameClock;

	private static int logLines;

	private static int kfCount;

	private static float recRetryAt;

	private static float nextKf;

	public static bool FeatureOn;

	public static int KeyMinutes;

	private static bool recordedThisMatch;

	private static int attempts;

	private static float nextCheck;

	private static float nextTick;

	private static string wPath;

	private static byte[] buf;

	private static int bufLen;

	private static uint lastMs;

	private static int packets;

	private static long rawBytes;

	private static BtcReplay.RecConn mainConn;

	/// <summary>Main recording connection while recording (BtcMatchStatsNet writes the stats stream into the replay through it), else null.</summary>
	internal static NetworkConnection RecMainConn
	{
		get { return (BtcReplay.mode == BtcReplay.State.Recording) ? BtcReplay.mainConn : null; }
	}

	private static BtcReplay.RecConn kfConn;

	private static int kfId;

	private static float kfStart;

	private static FileStream fs;

	private static DeflateStream ds;

	private static Thread wThread;

	private static readonly object qLock;

	private static readonly Queue<byte[]> q;

	private static bool wClosing;

	private static bool wAbort;

	private static long outBytes;

	private static float footDur;

	private static int footPackets;

	private static long footRaw;

	private static int footKf;

	private static bool ignoreTried;

	private static HashSet<int> ignoreSet;

	public static bool RecSpectator;

	private static bool kfSpawnDone;

	private static float kfDoneAt;

	private static int footSnap;

	private static int relSent;

	private static float relT0;

	private static Asset_Map snapShell;

	private static Thread snapThread;

	private static volatile bool snapDone;

	private static volatile bool snapOk;

	private static string snapZip;

	private static int snapLen;

	private static BtcReplay.Data playData;

	private static int playStage;

	private static volatile int playThread;

	private static string playErr;

	private static string playStatus;

	private static string playDir;

	private static int playSid;

	private static Asset_Map playMap;

	private static bool playMapSet;

	private static float playT0;

	private static float playSceneOk;

	private static bool playSeenGame;

	private static float playLeftAt;

	private static byte[] playBuf;

	private static int playPos;

	private static long playMsAcc;

	private static bool playHavePend;

	private static int pendType;

	private static long pendAbs;

	private static int pendBody;

	private static float playClock;

	private static float playTotal;

	private static float playSpeed;

	private static bool playPaused;

	private static bool playEnded;

	private static float playLastHb;

	private static int playFed;

	private static int playErrors;

	private static BtcReplay.ReplayConn rconn;

	private static NetworkClient rclient;

	private static bool playGo;

	private static float playFeedT0;

	private static readonly HashSet<string> feedErrKeys;

	private static float playDiagNext;

	private static NetworkInstanceId pendingUser;

	private static bool pendingUserSet;

	private static int sceneMsgLogged;

	private static NetworkIdentity displacedId;

	private static uint displacedSid;

	private const int SeekBudgetMs = 40;

	private static bool playSeeking;

	private static float playSeekTarget;

	private static float seekAfterLoad = -1f;

	private static BtcReplay.Data restartData;

	private static float restartSeek;

	private static float restartAt;

	private static float playSeekT0;

	public static bool InSceneRewind = true;

	private static int rwStep;

	private static float rwT0;

	private static float rwTarget;

	private static Transform[] camChain;

	private static Vector3[] camPos;

	private static Quaternion[] camRot;

	private static float camRestoreAt;

	private static float rwStart;

	private static bool rwHold;

	private static bool rwPopulated;

	private static bool rwReleased;

	private static readonly Stack<IEnumerator> rwCo = new Stack<IEnumerator>();

	private static bool rwActive;

	private static bool sigHooked;

	private static bool terrainDirty;

	private static float rwGuardAt;

	private static int rwGuardHits;

	private static long terrainBaseHash = -1L;

	public enum State
	{
		Idle,
		Recording,
		Playing
	}

	public sealed class Data
	{
		public string FilePath;

		public string FileName;

		public string Map;

		public string MapName;

		public bool NewGame;

		public bool Multiplayer;

		public long Ticks;

		public float Duration;

		public bool Complete;

		public string Error;

		public List<string> Players = new List<string>();

		/// <summary>Header "pl2": faction id, colour and rich-text nick per player (BtcReplayNames); null in old files.</summary>
		public string Pl2;

		public string Tl;

		public string Mode;

		public string Scene;

		public long FileBytes;

		public long RawBytes;

		public int Packets;

		public int Keyframes;

		public int HeaderEnd;

		public int SnapBytes;
	}

	public sealed class RecConn : NetworkConnection
	{
		public override bool TransportSend(byte[] bytes, int numBytes, int channelId, out byte error)
		{
			error = 0;
			BtcReplay.OnPacket(this.Tag, bytes, numBytes, channelId);
			return true;
		}

		public int Tag;

		public bool HasUser;
	}

	public sealed class ReplayConn : NetworkConnection
	{
		public override bool TransportSend(byte[] bytes, int numBytes, int channelId, out byte error)
		{
			error = 0;
			return true;
		}
	}
}
}
