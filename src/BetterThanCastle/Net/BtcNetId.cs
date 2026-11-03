using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
public static class BtcNetId
{
	public static void OnServerStart()
	{
		try
		{
			BtcNetId.LoadCfg();
			BtcNetId.sceneKey = BtcNetId.GetSceneKey();
			BtcNetId.wasActive = true;
			BtcNetId.nextDiag = Time.unscaledTime + 10f;
			BtcNetId.Raise("server start");
		}
		catch (Exception arg)
		{
			BtcNetId.Log("OnServerStart error: " + arg);
		}
	}

	public static void Tick()
	{
		try
		{
			bool flag = !NetworkServer.active;
			if (flag)
			{
				BtcNetId.wasActive = false;
			}
			else
			{
				bool flag2 = !BtcNetId.cfgLoaded;
				if (flag2)
				{
					BtcNetId.LoadCfg();
				}
				string a = BtcNetId.GetSceneKey();
				bool flag3 = !BtcNetId.wasActive;
				if (flag3)
				{
					BtcNetId.wasActive = true;
					BtcNetId.sceneKey = a;
					BtcNetId.Raise("server active (tick)");
				}
				else
				{
					bool flag4 = a != BtcNetId.sceneKey;
					if (flag4)
					{
						BtcNetId.sceneKey = a;
						BtcNetId.Raise("scene change");
					}
				}
				bool flag5 = BtcNetId.Diag && Time.unscaledTime >= BtcNetId.nextDiag;
				if (flag5)
				{
					BtcNetId.nextDiag = Time.unscaledTime + BtcNetId.DiagSec;
					BtcNetId.Scan();
				}
			}
		}
		catch (Exception arg)
		{
			BtcNetId.errors++;
			bool flag6 = BtcNetId.errors <= 3;
			if (flag6)
			{
				BtcNetId.Log("Tick error: " + arg);
			}
		}
	}

	private static string GetSceneKey()
	{
		Scene activeScene = SceneManager.GetActiveScene();
		return activeScene.buildIndex + ":" + activeScene.name;
	}

	private static void Raise(string why)
	{
		bool flag = !BtcNetId.Enabled;
		if (flag)
		{
			BtcNetId.Log("Raise skipped (Enabled=0) [" + why + "]");
		}
		else
		{
			FieldInfo fieldInfo = BtcNetId.FindCounterField();
			bool flag2 = fieldInfo == null;
			if (flag2)
			{
				BtcNetId.Log("Raise FAILED [" + why + "]: counter field not found in NetworkIdentity");
			}
			else
			{
				uint num = 0U;
				int num2 = 0;
				NetworkIdentity[] array = Resources.FindObjectsOfTypeAll<NetworkIdentity>();
				for (int i = 0; i < array.Length; i++)
				{
					bool flag3 = array[i] != null;
					if (flag3)
					{
						uint value = array[i].netId.Value;
						bool flag4 = value > 0U;
						if (flag4)
						{
							num2++;
							bool flag5 = value > num;
							if (flag5)
							{
								num = value;
							}
						}
					}
				}
				uint num3 = (uint)fieldInfo.GetValue(null);
				uint num4 = num + 1U + (uint)BtcNetId.Margin;
				bool flag6 = num4 > num3;
				if (flag6)
				{
					fieldInfo.SetValue(null, num4);
					uint num5 = (uint)fieldInfo.GetValue(null);
					BtcNetId.Log(string.Concat(new object[]
					{
						"Raise [",
						why,
						"]: counter ",
						num3,
						" -> ",
						num5,
						" (max netId in memory ",
						num,
						", identities with id ",
						num2,
						", total ",
						array.Length,
						")"
					}));
				}
				else
				{
					BtcNetId.Log(string.Concat(new object[]
					{
						"Raise [",
						why,
						"]: counter ",
						num3,
						" is already above max ",
						num,
						" (identities with id ",
						num2,
						", total ",
						array.Length,
						")"
					}));
				}
			}
		}
	}

	private static FieldInfo FindCounterField()
	{
		bool flag = BtcNetId.counterField != null || BtcNetId.fieldSearched;
		FieldInfo result;
		if (flag)
		{
			result = BtcNetId.counterField;
		}
		else
		{
			BtcNetId.fieldSearched = true;
			BindingFlags bindingAttr = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			string[] array = new string[]
			{
				"s_NextNetworkId",
				"s_nextNetworkId",
				"nextNetworkId",
				"m_NextNetworkId"
			};
			Type typeFromHandle = typeof(NetworkIdentity);
			for (int i = 0; i < array.Length; i++)
			{
				FieldInfo field = typeFromHandle.GetField(array[i], bindingAttr);
				bool flag2 = field != null && field.FieldType == typeof(uint);
				if (flag2)
				{
					BtcNetId.counterField = field;
					return field;
				}
			}
			FieldInfo[] fields = typeFromHandle.GetFields(bindingAttr);
			for (int j = 0; j < fields.Length; j++)
			{
				bool flag3 = fields[j].FieldType == typeof(uint) && fields[j].Name.ToLowerInvariant().Contains("next");
				if (flag3)
				{
					BtcNetId.counterField = fields[j];
					return fields[j];
				}
			}
			result = null;
		}
		return result;
	}

	private static void Scan()
	{
		NetworkIdentity[] array = UnityEngine.Object.FindObjectsOfType<NetworkIdentity>();
		Dictionary<uint, NetworkIdentity> dictionary = new Dictionary<uint, NetworkIdentity>(array.Length);
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		foreach (NetworkIdentity networkIdentity in array)
		{
			bool flag = !(networkIdentity == null) && networkIdentity.isServer;
			if (flag)
			{
				uint value = networkIdentity.netId.Value;
				bool flag2 = value > 0U;
				if (flag2)
				{
					num3++;
					NetworkIdentity networkIdentity2;
					bool flag3 = dictionary.TryGetValue(value, out networkIdentity2);
					if (flag3)
					{
						num++;
						bool flag4 = BtcNetId.logged < 40;
						if (flag4)
						{
							BtcNetId.logged++;
							BtcNetId.Log(string.Concat(new object[]
							{
								"COLLISION netId=",
								value,
								": '",
								networkIdentity2.name,
								"' and '",
								networkIdentity.name,
								"'"
							}));
						}
					}
					else
					{
						dictionary[value] = networkIdentity;
					}
					GameObject gameObject = NetworkServer.FindLocalObject(networkIdentity.netId);
					bool flag5 = gameObject != networkIdentity.gameObject;
					if (flag5)
					{
						num2++;
						bool flag6 = BtcNetId.logged < 40;
						if (flag6)
						{
							BtcNetId.logged++;
							BtcNetId.Log(string.Concat(new object[]
							{
								"ORPHAN netId=",
								value,
								": '",
								networkIdentity.name,
								"' is not the registered object (",
								(gameObject != null) ? gameObject.name : "null",
								")"
							}));
						}
					}
				}
			}
		}
		bool flag7 = num > 0 || num2 > 0 || !BtcNetId.lastClean;
		if (flag7)
		{
			BtcNetId.Log(string.Concat(new object[]
			{
				"Scan: server identities ",
				num3,
				", collisions ",
				num,
				", orphans ",
				num2
			}));
		}
		BtcNetId.lastClean = (num == 0 && num2 == 0);
	}

	private static void LoadCfg()
	{
		BtcNetId.cfgLoaded = true;
		BtcPaths.Init();
		BtcNetId.Enabled = (BtcPaths.ReadKey("netid.txt", "Enabled", "1") != "0");
		BtcNetId.Diag = (BtcPaths.ReadKey("netid.txt", "Diag", "1") != "0");
		int b;
		bool flag = !int.TryParse(BtcPaths.ReadKey("netid.txt", "DiagSec", "30"), out b);
		if (flag)
		{
			b = 30;
		}
		BtcNetId.DiagSec = (float)Mathf.Max(5, b);
		int value;
		bool flag2 = !int.TryParse(BtcPaths.ReadKey("netid.txt", "Margin", "1000"), out value);
		if (flag2)
		{
			value = 1000;
		}
		BtcNetId.Margin = Mathf.Clamp(value, 0, 100000);
	}

	private static void Log(string msg)
	{
		try
		{
			string text = BtcPaths.DebugFile("btc_netid.txt");
			FileInfo fileInfo = new FileInfo(text);
			bool flag = fileInfo.Exists && fileInfo.Length > 2097152L;
			if (!flag)
			{
				File.AppendAllText(text, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + msg + "\r\n");
			}
		}
		catch (Exception)
		{
		}
	}

	public static bool Enabled = true;

	public static bool Diag = true;

	public static float DiagSec = 30f;

	public static int Margin = 1000;

	private static bool cfgLoaded;

	private static bool wasActive;

	private static string sceneKey = "";

	private static float nextDiag;

	private static int errors;

	private static int logged;

	private static bool lastClean = true;

	private static FieldInfo counterField;

	private static bool fieldSearched;
}
}
