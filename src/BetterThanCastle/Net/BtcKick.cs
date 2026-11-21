using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
public static class BtcKick
{
	public static void Tick()
	{
		try
		{
			bool active = NetworkServer.active;
			bool flag = !active;
			if (flag)
			{
				bool srv = BtcKick._srv;
				bool flag2 = srv;
				if (flag2)
				{
					BtcKick._srv = false;
					BtcKick._banIp.Clear();
					BtcKick._banName.Clear();
					BtcKick._handled.Clear();
					BtcKick._pending.Clear();
				}
			}
			else
			{
				BtcKick._srv = true;
				bool flag3 = BtcKick._banIp.Count == 0 && BtcKick._banName.Count == 0 && BtcKick._pending.Count == 0;
				bool flag4 = !flag3;
				if (flag4)
				{
					float unscaledTime = Time.unscaledTime;
					bool flag5 = unscaledTime >= BtcKick._next;
					bool flag6 = flag5;
					if (flag6)
					{
						BtcKick._next = unscaledTime + 0.5f;
						BtcKick.Remote();
						for (int i = 0; i < BtcKick._tmp.Count; i++)
						{
							NetworkConnection networkConnection = BtcKick._tmp[i];
							bool flag7 = BtcKick._handled.Contains(networkConnection.connectionId);
							bool flag8 = !flag7;
							if (flag8)
							{
								string text = BtcSpectate.NameOf(networkConnection);
								bool flag9 = BtcKick._banIp.Contains(networkConnection.address ?? string.Empty) || (text != null && BtcKick._banName.Contains(text));
								bool flag10 = flag9;
								if (flag10)
								{
									BtcKick._handled.Add(networkConnection.connectionId);
									BtcKick.Drop(networkConnection);
								}
							}
						}
						for (int j = BtcKick._pending.Count - 1; j >= 0; j--)
						{
							bool flag11 = unscaledTime < BtcKick._pending[j].Value;
							bool flag12 = !flag11;
							if (flag12)
							{
								int key = BtcKick._pending[j].Key;
								BtcKick._pending.RemoveAt(j);
								for (int k = 0; k < BtcKick._tmp.Count; k++)
								{
									bool flag13 = BtcKick._tmp[k].connectionId == key && BtcKick._tmp[k].isConnected && UNetManager.instance != null;
									bool flag14 = flag13;
									if (flag14)
									{
										UNetManager.instance.SafeDisconnectConnection(BtcKick._tmp[k]);
									}
								}
							}
						}
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	public static void PingRows(Event e, ref float y, float w, float rowH, int fs, Color old, GUIStyle label, List<string> lines)
	{
		BtcKick.Remote();
		bool flag = NetworkServer.active && BtcKick._tmp.Count == lines.Count;
		float num = Mathf.Max((float)fs * 5f, w * 0.16f);
		bool flag2 = BtcKick._btn == null || BtcKick._fs != fs;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcKick._fs = fs;
			BtcKick._btn = new GUIStyle(GUI.skin.button);
			BtcKick._btn.fontSize = fs;
			BtcKick._btn.alignment = TextAnchor.MiddleCenter;
		}
		for (int i = 0; i < lines.Count; i++)
		{
			float width = w - (float)fs * 2f - (flag ? (num + 6f) : 0f);
			GUI.Label(new Rect((float)fs, y, width, rowH), lines[i] + BtcHandicap.Label(BtcKick.NameFromLine(lines[i])), label);
			bool flag5 = flag;
			bool flag6 = flag5;
			if (flag6)
			{
				NetworkConnection networkConnection = BtcKick._tmp[i];
				bool flag7 = BtcKick._confirmId == networkConnection.connectionId && Time.unscaledTime < BtcKick._confirmUntil;
				GUI.color = (flag7 ? new Color(1f, 0.45f, 0.45f, 1f) : Color.white);
				bool flag8 = GUI.Button(new Rect(w - (float)fs - num, y + rowH * 0.08f, num, rowH * 0.84f), flag7 ? BtcGameplay.T("Sure?", "Точно?") : BtcGameplay.T("Kick", "Кик"), BtcKick._btn);
				GUI.color = old;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					bool flag11 = flag7;
					bool flag12 = flag11;
					if (flag12)
					{
						BtcKick._confirmId = -1;
						BtcKick.Kick(networkConnection, BtcKick.NameFromLine(lines[i]));
						break;
					}
					BtcKick._confirmId = networkConnection.connectionId;
					BtcKick._confirmUntil = Time.unscaledTime + 3f;
				}
			}
			y += rowH;
		}
	}

	public static void Kick(NetworkConnection c, string name)
	{
		try
		{
			bool flag = c == null;
			bool flag2 = !flag;
			if (flag2)
			{
				BtcKick.Reflect();
				string text = c.address ?? string.Empty;
				bool flag3 = text.Length > 0 && !BtcKick.IsPrivate(text);
				bool flag4 = flag3;
				if (flag4)
				{
					BtcKick._banIp.Add(text);
				}
				string text2 = BtcSpectate.NameOf(c);
				bool flag5 = !string.IsNullOrEmpty(name);
				bool flag6 = flag5;
				if (flag6)
				{
					BtcKick._banName.Add(name);
					BtcKick.RemoveKey(BtcKick._fSlots, name);
					BtcKick.RemoveKey(BtcKick._fKnown, name);
					BtcKick.RemoveKey(BtcKick._fKeys, name);
				}
				bool flag7 = !string.IsNullOrEmpty(text2);
				bool flag8 = flag7;
				if (flag8)
				{
					BtcKick._banName.Add(text2);
					BtcKick.RemoveKey(BtcKick._fSlots, text2);
					BtcKick.RemoveKey(BtcKick._fKnown, text2);
					BtcKick.RemoveKey(BtcKick._fKeys, text2);
				}
				BtcKick.RemoveId(BtcKick._fRet, c.connectionId);
				BtcKick.RemoveId(BtcKick._fSpec, c.connectionId);
				BtcKick._handled.Add(c.connectionId);
				BtcKick.Drop(c);
				BtcHud.Notice(BtcReplayUI.T("Kicked: " + name, "Исключён: " + name), BtcReplayIcons.Cross, BtcHud.Danger);
				Debug.Log(string.Concat(new object[]
				{
					"BtcKick: kicked '",
					name,
					"' conn ",
					c.connectionId,
					" ip ",
					text
				}));
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("BtcKick.Kick: " + ex.Message);
		}
	}

	private static void Drop(NetworkConnection c)
	{
		bool flag = BtcBc.BcEnabled && BtcKick._deny != null;
		bool flag2 = flag;
		if (flag2)
		{
			try
			{
				BtcKick._deny.Invoke(null, new object[]
				{
					c,
					BtcGameplay.T("You were kicked by the host.", "Хост исключил вас из игры.")
				});
				BtcKick._pending.Add(new KeyValuePair<int, float>(c.connectionId, Time.unscaledTime + 2f));
				return;
			}
			catch (Exception)
			{
			}
		}
		bool flag3 = UNetManager.instance != null;
		bool flag4 = flag3;
		if (flag4)
		{
			UNetManager.instance.SafeDisconnectConnection(c);
		}
	}

	private static void Remote()
	{
		BtcKick._tmp.Clear();
		for (int i = 0; i < NetworkServer.connections.Count; i++)
		{
			NetworkConnection networkConnection = NetworkServer.connections[i];
			bool flag = networkConnection == null;
			bool flag2 = !flag;
			if (flag2)
			{
				bool flag3 = false;
				foreach (NetworkConnection networkConnection2 in NetworkServer.localConnections)
				{
					bool flag4 = networkConnection2 != null && networkConnection2.connectionId == networkConnection.connectionId;
					bool flag5 = flag4;
					if (flag5)
					{
						flag3 = true;
						break;
					}
				}
				bool flag6 = !flag3;
				if (flag6)
				{
					BtcKick._tmp.Add(networkConnection);
				}
			}
		}
	}

	/// <summary>Remote connections of the host in the same order as BtcNetManager.PingLines (menu rows; empty for clients).</summary>
	internal static NetworkConnection[] RemoteConns()
	{
		if (!NetworkServer.active) return new NetworkConnection[0];
		BtcKick.Remote();
		return BtcKick._tmp.ToArray();
	}

	internal static string PlayerOfLine(string line) { return BtcKick.NameFromLine(line); }

	private static string NameFromLine(string line)
	{
		string text = line ?? string.Empty;
		bool flag = text.StartsWith("[P] ") || text.StartsWith("[!] ");
		bool flag2 = flag;
		if (flag2)
		{
			text = text.Substring(4);
		}
		int num = text.LastIndexOf(": ");
		return (num > 0) ? text.Substring(0, num) : text;
	}

	private static bool IsPrivate(string ip)
	{
		string text = ip.StartsWith("::ffff:") ? ip.Substring(7) : ip;
		bool flag = text.StartsWith("10.") || text.StartsWith("192.168.") || text.StartsWith("127.") || text.StartsWith("169.254.") || text == "::1" || text == "localClient";
		bool flag2 = flag;
		bool result;
		if (flag2)
		{
			result = true;
		}
		else
		{
			bool flag3 = text.StartsWith("172.");
			bool flag4 = flag3;
			if (flag4)
			{
				int num = text.IndexOf('.', 4);
				int num2;
				bool flag5 = num > 4 && int.TryParse(text.Substring(4, num - 4), out num2) && num2 >= 16 && num2 <= 31;
				bool flag6 = flag5;
				if (flag6)
				{
					return true;
				}
			}
			result = false;
		}
		return result;
	}

	private static void Reflect()
	{
		bool refl = BtcKick._refl;
		bool flag = !refl;
		if (flag)
		{
			BtcKick._refl = true;
			try
			{
				Type typeFromHandle = typeof(BtcSpectate);
				BindingFlags bindingAttr = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
				BtcKick._deny = typeFromHandle.GetMethod("Deny", bindingAttr);
				BtcKick._fSlots = typeFromHandle.GetField("_slots", bindingAttr);
				BtcKick._fKnown = typeFromHandle.GetField("_known", bindingAttr);
				BtcKick._fKeys = typeFromHandle.GetField("_keys", bindingAttr);
				BtcKick._fRet = typeFromHandle.GetField("_ret", bindingAttr);
				BtcKick._fSpec = typeFromHandle.GetField("_spec", bindingAttr);
			}
			catch (Exception)
			{
			}
		}
	}

	private static void RemoveKey(FieldInfo f, string key)
	{
		bool flag = f == null || string.IsNullOrEmpty(key);
		bool flag2 = !flag;
		if (flag2)
		{
			IDictionary dictionary = f.GetValue(null) as IDictionary;
			bool flag3 = dictionary != null;
			bool flag4 = flag3;
			if (flag4)
			{
				dictionary.Remove(key);
			}
		}
	}

	private static void RemoveId(FieldInfo f, int id)
	{
		bool flag = f == null;
		bool flag2 = !flag;
		if (flag2)
		{
			HashSet<int> hashSet = f.GetValue(null) as HashSet<int>;
			bool flag3 = hashSet != null;
			bool flag4 = flag3;
			if (flag4)
			{
				hashSet.Remove(id);
			}
		}
	}

	private static readonly HashSet<string> _banIp = new HashSet<string>();

	private static readonly HashSet<string> _banName = new HashSet<string>();

	private static readonly HashSet<int> _handled = new HashSet<int>();

	private static readonly List<KeyValuePair<int, float>> _pending = new List<KeyValuePair<int, float>>();

	private static readonly List<NetworkConnection> _tmp = new List<NetworkConnection>();

	private static bool _srv;

	private static bool _refl;

	private static float _next;

	private static int _confirmId = -1;

	private static float _confirmUntil;

	private static int _fs;

	private static GUIStyle _btn;

	private static MethodInfo _deny;

	private static FieldInfo _fSlots;

	private static FieldInfo _fKnown;

	private static FieldInfo _fKeys;

	private static FieldInfo _fRet;

	private static FieldInfo _fSpec;
}
}
