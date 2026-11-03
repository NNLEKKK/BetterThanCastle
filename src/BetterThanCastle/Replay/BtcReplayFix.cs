using System;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Game;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
public static class BtcReplayFix
{
	private static void Log(string s)
	{
		Debug.Log("[BtcReplayFix] " + s);
	}

	public static string GpsLine()
	{
		string result;
		try
		{
			float[] array = BtcGameplay.E ?? BtcGameplay.Active;
			result = "gps=" + ((array != null) ? BtcGameplay.Encode(array) : "") + "\n";
		}
		catch (Exception ex)
		{
			BtcReplayFix.Log("GpsLine: " + ex.Message);
			result = "";
		}
		return result;
	}

	private static string ReadGps(string path)
	{
		try
		{
			using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				byte[] array = new byte[9];
				bool flag = fileStream.Read(array, 0, 9) < 9;
				if (flag)
				{
					return null;
				}
				int num = BitConverter.ToInt32(array, 5);
				bool flag2 = num < 0 || num > 65536 || 9L + (long)num > fileStream.Length;
				if (flag2)
				{
					return null;
				}
				byte[] array2 = new byte[num];
				fileStream.Read(array2, 0, num);
				string[] array3 = Encoding.UTF8.GetString(array2).Split(new char[]
				{
					'\n'
				});
				for (int i = 0; i < array3.Length; i++)
				{
					bool flag3 = array3[i].StartsWith("gps=");
					if (flag3)
					{
						return array3[i].Substring(4).Trim();
					}
				}
			}
		}
		catch (Exception ex)
		{
			BtcReplayFix.Log("ReadGps: " + ex.Message);
		}
		return null;
	}

	private static void SetV(float[] v)
	{
		bool flag = BtcReplayFix._fiV == null;
		if (flag)
		{
			BtcReplayFix._fiV = typeof(BtcGameplay).GetField("V", BindingFlags.Static | BindingFlags.NonPublic);
		}
		bool flag2 = BtcReplayFix._fiV != null;
		if (flag2)
		{
			BtcReplayFix._fiV.SetValue(null, v);
		}
		BtcGameplay.Refresh();
	}

	public static void OnStart(string replayPath)
	{
		try
		{
			BtcReplayFix.ResetRuntime();
			string text = BtcReplayFix.ReadGps(replayPath);
			bool flag = !BtcReplayFix._applied;
			if (flag)
			{
				BtcReplayFix._backup = BtcGameplay.Active;
				BtcReplayFix._applied = true;
			}
			BtcReplayFix.SetV(BtcGameplay.Decode(text));
			BtcReplayFix.Log("gameplay settings from the replay: " + ((text == null) ? "none (defaults)" : ((text.Length == 0) ? "vanilla" : text)));
		}
		catch (Exception arg)
		{
			BtcReplayFix.Log("OnStart: " + arg);
		}
	}

	private static void CallSpec(string name)
	{
		MethodInfo method = typeof(BtcSpectate).GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		bool flag = method != null;
		if (flag)
		{
			method.Invoke(null, null);
		}
	}

	private static void SetSpec(string name, object v)
	{
		FieldInfo field = typeof(BtcSpectate).GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		bool flag = field != null;
		if (flag)
		{
			field.SetValue(null, v);
		}
	}

	public static void OnEnd()
	{
		try
		{
			try
			{
				BtcReplayFix.CallSpec("ClientReset");
			}
			catch (Exception ex)
			{
				BtcReplayFix.Log("ClientReset: " + ((ex.InnerException != null) ? ex.InnerException.Message : ex.Message));
			}
			BtcReplayFix.SetSpec("_pendingMap", null);
			BtcReplayFix.SetSpec("_mapApplied", false);
			BtcReplayFix.SetSpec("_cReturn", false);
			BtcReplayFix.SetSpec("_cSpectator", false);
			BtcSpectate.RewindKeep = false;
			BtcFactionFix.Reset();
			bool applied = BtcReplayFix._applied;
			if (applied)
			{
				BtcReplayFix._applied = false;
				bool flag = BtcReplayFix._backup != null;
				if (flag)
				{
					BtcReplayFix.SetV(BtcReplayFix._backup);
				}
				BtcReplayFix._backup = null;
				BtcReplayFix.Log("gameplay settings restored");
			}
			BtcReplayFix.ResetRuntime();
			BtcReplayFix.Log("replay state reset");
		}
		catch (Exception arg)
		{
			BtcReplayFix.Log("OnEnd: " + arg);
		}
	}

	public static bool IsStalePending()
	{
		return BtcReplay.Mode != BtcReplay.State.Playing;
	}

	private static void ResetRuntime()
	{
		BtcReplayFix._wasRunning = false;
		BtcReplayFix._relAt1 = 0f;
		BtcReplayFix._relAt2 = 0f;
		BtcReplayFix._relAt3 = 0f;
	}

	private static void Noop(NetworkMessage m)
	{
	}

	public static void Tick()
	{
		bool flag = BtcReplay.Mode != BtcReplay.State.Playing;
		if (!flag)
		{
			try
			{
				UNetManager instance = UNetManager.instance;
				NetworkClient networkClient = (instance != null) ? instance.LocalNetworkClient : null;
				bool flag2 = networkClient != null && networkClient.handlers != null;
				if (flag2)
				{
					bool flag3 = BtcReplayFix._noop == null;
					if (flag3)
					{
						BtcReplayFix._noop = new NetworkMessageDelegate(BtcReplayFix.Noop);
					}
					NetworkMessageDelegate d;
					bool flag4 = networkClient.handlers.TryGetValue(73, out d) && d != BtcReplayFix._noop;
					if (flag4)
					{
						networkClient.handlers[73] = BtcReplayFix._noop;
					}
					bool flag5 = networkClient.handlers.TryGetValue(74, out d) && d != BtcReplayFix._noop;
					if (flag5)
					{
						networkClient.handlers[74] = BtcReplayFix._noop;
					}
				}
				Journee instance2 = Journee.instance;
				bool flag6 = instance2 != null && instance2.IsPaused;
				if (flag6)
				{
					instance2.IsPaused = false;
				}
				bool running = BtcReplay.Running;
				float unscaledTime = Time.unscaledTime;
				bool flag7 = running && !BtcReplayFix._wasRunning;
				if (flag7)
				{
					BtcReplayFix.PruneUsers();
					BtcReplayFix._relAt1 = unscaledTime + 1f;
					BtcReplayFix._relAt2 = unscaledTime + 3f;
					BtcReplayFix._relAt3 = unscaledTime + 8f;
				}
				BtcReplayFix._wasRunning = running;
				bool flag8 = running;
				if (flag8)
				{
					bool flag9 = BtcReplayFix._relAt1 > 0f && unscaledTime >= BtcReplayFix._relAt1;
					if (flag9)
					{
						BtcReplayFix._relAt1 = 0f;
						BtcReplayFix.ApplyRel("pass 1");
					}
					bool flag10 = BtcReplayFix._relAt2 > 0f && unscaledTime >= BtcReplayFix._relAt2;
					if (flag10)
					{
						BtcReplayFix._relAt2 = 0f;
						BtcReplayFix.PruneUsers();
						BtcReplayFix.ApplyRel("pass 2");
					}
					bool flag11 = BtcReplayFix._relAt3 > 0f && unscaledTime >= BtcReplayFix._relAt3;
					if (flag11)
					{
						BtcReplayFix._relAt3 = 0f;
						BtcReplayFix.ApplyRel("pass 3");
					}
				}
			}
			catch (Exception ex)
			{
				BtcReplayFix.Log("Tick: " + ex.Message);
			}
		}
	}

	private static void PruneUsers()
	{
		UNetManager instance = UNetManager.instance;
		bool flag = instance == null || instance.UserList == null;
		if (!flag)
		{
			int num = instance.UserList.RemoveAll((User u) => u == null);
			bool flag2 = num > 0;
			if (flag2)
			{
				BtcReplayFix.Log("removed " + num + " destroyed users from UserList");
			}
		}
	}

	private static void ApplyRel(string why)
	{
		try
		{
			bool flag = BtcReplayFix._miApplyRel == null;
			if (flag)
			{
				BtcReplayFix._miApplyRel = typeof(BtcSpectate).GetMethod("ApplyRelations", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			}
			bool flag2 = BtcReplayFix._miApplyRel == null;
			if (flag2)
			{
				BtcReplayFix.Log("ApplyRelations not found");
			}
			else
			{
				object obj = BtcReplayFix._miApplyRel.Invoke(null, new object[]
				{
					"replay " + why
				});
				BtcReplayFix.Log(string.Concat(new object[]
				{
					"relations re-applied (",
					why,
					"): ",
					obj,
					" pairs"
				}));
			}
		}
		catch (Exception ex)
		{
			BtcReplayFix.Log("ApplyRel: " + ((ex.InnerException != null) ? ex.InnerException.Message : ex.Message));
		}
	}

	private static FieldInfo _fiV;

	private static float[] _backup;

	private static bool _applied;

	private static bool _wasRunning;

	private static float _relAt1;

	private static float _relAt2;

	private static float _relAt3;

	private static MethodInfo _miApplyRel;

	private static NetworkMessageDelegate _noop;
}
}
