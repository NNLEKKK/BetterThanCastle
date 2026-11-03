using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Game;
using Brix.Game.Network;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
public static class BtcNetDiag
{
	public static void Tick()
	{
		BtcNetId.Tick();
		bool flag = !BtcNetDiag.Enabled || BtcNetDiag.failed;
		bool flag2 = !flag;
		if (flag2)
		{
			try
			{
				bool flag3 = !BtcNetDiag.inited;
				bool flag4 = flag3;
				if (flag4)
				{
					BtcNetDiag.Init();
				}
				float unscaledTime = Time.unscaledTime;
				bool flag5 = unscaledTime < BtcNetDiag.nextSnap;
				bool flag6 = !flag5;
				if (flag6)
				{
					BtcNetDiag.nextSnap = unscaledTime + 1f;
					bool flag7 = unscaledTime >= BtcNetDiag.nextBeat;
					bool flag8 = flag7;
					bool flag9 = flag8;
					if (flag9)
					{
						BtcNetDiag.nextBeat = unscaledTime + 15f;
					}
					bool active = NetworkClient.active;
					bool flag10 = active;
					if (flag10)
					{
						string text = BtcNetDiag.ClientSnapshot();
						bool flag11 = flag7 || text != BtcNetDiag.lastClient;
						bool flag12 = flag11;
						if (flag12)
						{
							BtcNetDiag.lastClient = text;
							BtcNetDiag.Add("CLIENT " + text);
						}
					}
					else
					{
						BtcNetDiag.lastClient = null;
					}
					bool active2 = NetworkServer.active;
					bool flag13 = active2;
					if (flag13)
					{
						string text2 = BtcNetDiag.HostSnapshot();
						bool flag14 = flag7 || text2 != BtcNetDiag.lastHost;
						bool flag15 = flag14;
						if (flag15)
						{
							BtcNetDiag.lastHost = text2;
							BtcNetDiag.Add("HOST   " + text2);
						}
					}
					else
					{
						BtcNetDiag.lastHost = null;
					}
					BtcNetDiag.Flush();
				}
			}
			catch (Exception ex)
			{
				BtcNetDiag.failed = true;
				try
				{
					Application.logMessageReceivedThreaded -= BtcNetDiag.OnLog;
				}
				catch (Exception)
				{
				}
				Debug.LogWarning("BtcNetDiag disabled: " + ex.Message);
			}
		}
	}

	private static void Init()
	{
		BtcNetDiag.inited = true;
		BtcPaths.Init();
		BtcNetDiag.path = BtcPaths.DebugFile("btc_netdiag.txt");
		Type typeFromHandle = typeof(UNetManager);
		BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		BtcNetDiag.fVol = typeFromHandle.GetField("voluntaryPauseActive", bindingAttr);
		BtcNetDiag.fHard = typeFromHandle.GetField("hardPauseActive", bindingAttr);
		BtcNetDiag.fDegraded = typeFromHandle.GetField("connectionDegraded", bindingAttr);
		BtcNetDiag.fLoading = typeFromHandle.GetField("isLoadingLevel", bindingAttr);
		Application.logMessageReceivedThreaded += BtcNetDiag.OnLog;
		BtcNetDiag.Add(string.Concat(new string[]
		{
			"=== BtcNetDiag start ",
			DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
			" | unity ",
			Application.unityVersion,
			" | BC=",
			BtcBc.BcEnabled ? "ON" : "OFF",
			" ==="
		}));
	}

	private static void OnLog(string condition, string stackTrace, LogType type)
	{
		bool flag = !BtcNetDiag.Enabled;
		if (!flag)
		{
			bool flag2 = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
			bool flag3 = !flag2 && type == LogType.Warning && condition != null;
			if (flag3)
			{
				string text = condition.ToLowerInvariant();
				flag2 = (text.Contains("command") || text.Contains("authority") || text.Contains("not ready") || text.Contains("unet") || text.Contains("network") || text.Contains("send ") || text.Contains("connection"));
			}
			bool flag4 = !flag2;
			if (!flag4)
			{
				string text2 = string.Concat(new object[]
				{
					"LOG[",
					type,
					"] ",
					condition
				});
				bool flag5 = type == LogType.Exception || type == LogType.Error;
				if (flag5)
				{
					string text3 = stackTrace;
					bool flag6 = text3 != null && text3.Length > 600;
					if (flag6)
					{
						text3 = text3.Substring(0, 600);
					}
					text2 = text2 + " || " + ((text3 == null) ? "" : text3.Replace("\n", " | "));
				}
				BtcNetDiag.Add(text2);
			}
		}
	}

	private static string ClientSnapshot()
	{
		BtcNetDiag.sb.Length = 0;
		BtcNetDiag.sb.Append("t=").Append(Time.unscaledTime.ToString("F1"));
		BtcNetDiag.sb.Append(" scale=").Append(Time.timeScale.ToString("F2"));
		BtcNetDiag.sb.Append(" connected=").Append(BtcNetDiag.ClientConnected());
		BtcNetDiag.sb.Append(" ready=").Append(ClientScene.ready);
		BtcNetDiag.sb.Append(" readyConn=").Append(ClientScene.readyConnection != null);
		BtcNetDiag.sb.Append(" localPlayers=").Append((ClientScene.localPlayers != null) ? ClientScene.localPlayers.Count : -1);
		BtcNetDiag.AppendPause(BtcNetDiag.sb);
		BtcNetDiag.sb.Append(" Received=").Append(BtcGameplay.Received);
		BtcNetDiag.sb.Append(" replayActive=").Append(BtcReplay.Active);
		User localUser = User.LocalUser;
		BtcNetDiag.sb.Append(" | LocalUser=").Append(localUser != null);
		bool flag = localUser != null;
		if (flag)
		{
			BtcNetDiag.sb.Append(" fac#=").Append(localUser.factionNumber);
			NetworkIdentity component = localUser.GetComponent<NetworkIdentity>();
			bool flag2 = component != null;
			if (flag2)
			{
				BtcNetDiag.sb.Append(" netId=").Append(component.netId.Value);
				BtcNetDiag.sb.Append(" isLocalPlayer=").Append(component.isLocalPlayer);
				BtcNetDiag.sb.Append(" hasAuthority=").Append(component.hasAuthority);
			}
			UNetContextualCmd component2 = localUser.GetComponent<UNetContextualCmd>();
			BtcNetDiag.sb.Append(" cmdComp=").Append(component2 != null);
			bool flag3 = component2 != null;
			if (flag3)
			{
				BtcNetDiag.sb.Append(" cmdLocal=").Append(component2.isLocalPlayer);
				BtcNetDiag.sb.Append(" cmdAuth=").Append(component2.hasAuthority);
			}
			BtcNetDiag.AppendOwner(BtcNetDiag.sb, localUser);
		}
		return BtcNetDiag.sb.ToString();
	}

	private static string HostSnapshot()
	{
		BtcNetDiag.sb.Length = 0;
		BtcNetDiag.sb.Append("t=").Append(Time.unscaledTime.ToString("F1"));
		BtcNetDiag.sb.Append(" scale=").Append(Time.timeScale.ToString("F2"));
		BtcNetDiag.AppendPause(BtcNetDiag.sb);
		BtcNetDiag.sb.Append(" replayActive=").Append(BtcReplay.Active);
		UNetManager instance = UNetManager.instance;
		ReadOnlyCollection<NetworkConnection> connections = NetworkServer.connections;
		int num = 0;
		for (int i = 0; i < connections.Count; i++)
		{
			NetworkConnection networkConnection = connections[i];
			bool flag = networkConnection == null;
			if (!flag)
			{
				num++;
				BtcNetDiag.sb.Append(" | conn").Append(networkConnection.connectionId);
				BtcNetDiag.sb.Append(" ready=").Append(networkConnection.isReady);
				BtcNetDiag.sb.Append(" players=").Append((networkConnection.playerControllers != null) ? networkConnection.playerControllers.Count : -1);
				User user = (instance != null) ? instance.GetUserFromConnection(networkConnection) : null;
				BtcNetDiag.sb.Append(" user=").Append(user != null);
				bool flag2 = user != null;
				if (flag2)
				{
					BtcNetDiag.sb.Append(" fac#=").Append(user.factionNumber);
					BtcNetDiag.sb.Append(" netReady=").Append(user.NetworkReady);
					UNetContextualCmd component = user.GetComponent<UNetContextualCmd>();
					BtcNetDiag.sb.Append(" cmdComp=").Append(component != null);
					BtcNetDiag.AppendOwner(BtcNetDiag.sb, user);
				}
			}
		}
		BtcNetDiag.sb.Append(" | conns=").Append(num);
		return BtcNetDiag.sb.ToString();
	}

	private static void AppendOwner(StringBuilder b, User u)
	{
		Faction faction = u.faction;
		b.Append(" faction=").Append(faction != null);
		bool flag = faction == null;
		if (!flag)
		{
			int num = 0;
			bool value = false;
			foreach (GameObject x in faction.OwnerSyncList)
			{
				num++;
				bool flag2 = x == u.gameObject;
				if (flag2)
				{
					value = true;
				}
			}
			b.Append(" owners=").Append(num);
			b.Append(" isOwner=").Append(value);
		}
	}

	private static void AppendPause(StringBuilder b)
	{
		UNetManager instance = UNetManager.instance;
		bool flag = instance == null;
		if (flag)
		{
			b.Append(" mgr=null");
		}
		else
		{
			b.Append(" vol=").Append(BtcNetDiag.Read(BtcNetDiag.fVol, instance));
			b.Append(" hard=").Append(BtcNetDiag.Read(BtcNetDiag.fHard, instance));
			b.Append(" degraded=").Append(BtcNetDiag.Read(BtcNetDiag.fDegraded, instance));
			b.Append(" loading=").Append(BtcNetDiag.Read(BtcNetDiag.fLoading, instance));
		}
	}

	private static string Read(FieldInfo f, object o)
	{
		bool flag = f == null;
		string result;
		if (flag)
		{
			result = "?";
		}
		else
		{
			object value = f.GetValue(o);
			result = ((value == null) ? "null" : value.ToString());
		}
		return result;
	}

	private static bool ClientConnected()
	{
		UNetManager instance = UNetManager.instance;
		NetworkClient networkClient = (instance != null) ? instance.LocalNetworkClient : null;
		return networkClient != null && networkClient.isConnected;
	}

	private static void Add(string msg)
	{
		string item = DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg;
		object obj = BtcNetDiag.gate;
		lock (obj)
		{
			bool flag = BtcNetDiag.pending.Count < 2000;
			if (flag)
			{
				BtcNetDiag.pending.Add(item);
			}
		}
	}

	private static void Flush()
	{
		// never blocks the main thread: a background writer drains the queue
		try
		{
			if (BtcNetDiag.writer == null)
			{
				BtcNetDiag.writer = new System.Threading.Thread(BtcNetDiag.WriterLoop);
				BtcNetDiag.writer.IsBackground = true;
				BtcNetDiag.writer.Name = "BtcNetDiagWriter";
				AppDomain.CurrentDomain.ProcessExit += delegate(object s, EventArgs a)
				{
					BtcNetDiag.WriteNow();
				};
				BtcNetDiag.writer.Start();
			}
			BtcNetDiag.wake.Set();
		}
		catch (Exception)
		{
		}
	}

	private static void WriterLoop()
	{
		for (;;)
		{
			BtcNetDiag.wake.WaitOne(2000);
			BtcNetDiag.WriteNow();
		}
	}

	private static void WriteNow()
	{
		string[] value = null;
		object obj = BtcNetDiag.gate;
		lock (obj)
		{
			if (BtcNetDiag.pending.Count == 0)
			{
				return;
			}
			value = BtcNetDiag.pending.ToArray();
			BtcNetDiag.pending.Clear();
		}
		lock (BtcNetDiag.fileGate)
		{
			try
			{
				string p = BtcNetDiag.path;
				if (p == null)
				{
					return;
				}
				FileInfo fileInfo = new FileInfo(p);
				if (fileInfo.Exists && fileInfo.Length > 524288L)
				{
					string old = p + ".old";
					if (File.Exists(old))
					{
						File.Delete(old);
					}
					File.Move(p, old);
				}
				File.AppendAllText(p, string.Join("\r\n", value) + "\r\n");
			}
			catch (Exception)
			{
			}
		}
	}

	private static System.Threading.Thread writer = null;

	private static readonly System.Threading.AutoResetEvent wake = new System.Threading.AutoResetEvent(false);

	private static readonly object fileGate = new object();
	public static bool Enabled = true;

	private const int MaxFileBytes = 524288;

	private const float SnapshotInterval = 1f;

	private const float HeartbeatInterval = 15f;

	private static bool inited = false;

	private static string path = null;

	private static float nextSnap = 0f;

	private static float nextBeat = 0f;

	private static string lastClient = null;

	private static string lastHost = null;

	private static bool failed = false;

	private static readonly object gate = new object();

	private static readonly List<string> pending = new List<string>();

	private static readonly StringBuilder sb = new StringBuilder(512);

	private static FieldInfo fVol = null;

	private static FieldInfo fHard = null;

	private static FieldInfo fDegraded = null;

	private static FieldInfo fLoading = null;
}
}
