using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Brix.Assets;
using Brix.Game;
using Brix.Game.Components;
using MoonSharp.Interpreter;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
public static class BtcHandicap
{
	private static void Clamp(int[] a)
	{
		for (int i = 0; i < 6; i++)
		{
			a[i] = Mathf.Clamp(a[i], BtcHandicap.Lo[i], BtcHandicap.Hi[i]);
		}
		bool flag = a[4] == 0;
		if (flag)
		{
			a[4] = 1;
		}
	}

	private static bool IsDefault(int[] a)
	{
		for (int i = 0; i < 6; i++)
		{
			bool flag = a[i] != BtcHandicap.Def[i];
			if (flag)
			{
				return false;
			}
		}
		return true;
	}

	private static string EncodeOne(int[] a)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < 6; i++)
		{
			bool flag = i > 0;
			if (flag)
			{
				stringBuilder.Append(',');
			}
			stringBuilder.Append(a[i]);
		}
		return stringBuilder.ToString();
	}

	private static bool DecodeOne(string s, int[] dst)
	{
		bool result;
		try
		{
			string[] array = s.Split(new char[]
			{
				','
			});
			bool flag = array.Length != 6;
			if (flag)
			{
				result = false;
			}
			else
			{
				for (int i = 0; i < 6; i++)
				{
					dst[i] = int.Parse(array[i]);
				}
				BtcHandicap.Clamp(dst);
				result = true;
			}
		}
		catch (Exception)
		{
			result = false;
		}
		return result;
	}

	private static string EncodeTable()
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<string, int[]> keyValuePair in BtcHandicap._tab)
		{
			stringBuilder.Append(keyValuePair.Key).Append('\u001f').Append(BtcHandicap.EncodeOne(keyValuePair.Value)).Append('\u001e');
		}
		return stringBuilder.ToString();
	}

	private static void DecodeTable(string s)
	{
		BtcHandicap._tab.Clear();
		bool flag = !string.IsNullOrEmpty(s);
		if (flag)
		{
			string[] array = s.Split(new char[]
			{
				'\u001e'
			}, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				int num = array[i].IndexOf('\u001f');
				bool flag2 = num <= 0;
				if (!flag2)
				{
					int[] array2 = new int[6];
					bool flag3 = BtcHandicap.DecodeOne(array[i].Substring(num + 1), array2) && !BtcHandicap.IsDefault(array2);
					if (flag3)
					{
						BtcHandicap._tab[array[i].Substring(0, num)] = array2;
					}
				}
			}
		}
		BtcHandicap._ver++;
	}

	private static string Key(string name)
	{
		if (string.IsNullOrEmpty(name)) return string.Empty;
		try { return (BtcNick.Plain(name) ?? string.Empty).Trim(); } catch (Exception) { return name.Trim(); }
	}

	private static bool Same(int[] a, int[] b)
	{
		for (int i = 0; i < 6; i++) { if (a[i] != b[i]) return false; }
		return true;
	}

	// Lazy sync: the lobby player may not exist yet when handlers register, and names can change.
	private static void Tick()
	{
		float t = Time.unscaledTime;
		if (t < BtcHandicap._tickAt) return;
		BtcHandicap._tickAt = t + 2f;
		if (!BtcBc.BcEnabled) return;
		try
		{
			BtcHandicap.LoadMine();
			string k = BtcHandicap.Key(BtcHandicap.MyName());
			if (k.Length == 0) return;
			if (NetworkServer.active)
			{
				int[] cur;
				bool has = BtcHandicap._tab.TryGetValue(k, out cur);
				bool want = !BtcHandicap.IsDefault(BtcHandicap._mine);
				if (want ? (!has || !BtcHandicap.Same(cur, BtcHandicap._mine)) : has)
				{
					BtcHandicap.Put(k, BtcHandicap._mine);
					BtcHandicap.Broadcast();
				}
			}
			else if (NetworkClient.active && BtcHandicap._sentOnce && k != BtcHandicap._sentKey)
			{
				if (!BtcHandicap.IsDefault(BtcHandicap._mine)) BtcHandicap.SendMine();
				BtcHandicap._sentKey = k;
			}
		}
		catch (Exception) { }
	}

	private static void Put(string name, int[] a)
	{
		name = BtcHandicap.Key(name);
		bool flag = string.IsNullOrEmpty(name);
		if (!flag)
		{
			bool flag2 = BtcHandicap.IsDefault(a);
			if (flag2)
			{
				BtcHandicap._tab.Remove(name);
			}
			else
			{
				BtcHandicap._tab[name] = (int[])a.Clone();
			}
			BtcHandicap._ver++;
		}
	}

	private static string MyName()
	{
		UNetManager instance = UNetManager.instance;
		return (instance != null && instance.LocalLobbyPlayer != null) ? instance.LocalLobbyPlayer.playerName : string.Empty;
	}

	private static void LoadMine()
	{
		bool loaded = BtcHandicap._loaded;
		if (!loaded)
		{
			BtcHandicap._loaded = true;
			try
			{
				int[] array = new int[6];
				bool flag = BtcHandicap.DecodeOne(PlayerPrefs.GetString("BtcHandicap", string.Empty), array);
				if (flag)
				{
					BtcHandicap._mine = array;
				}
			}
			catch (Exception)
			{
			}
		}
	}

	private static void MineChanged()
	{
		BtcHandicap.Clamp(BtcHandicap._mine);
		try
		{
			PlayerPrefs.SetString("BtcHandicap", BtcHandicap.EncodeOne(BtcHandicap._mine));
		}
		catch (Exception)
		{
		}
		bool active = NetworkServer.active;
		if (active)
		{
			BtcHandicap.PutHost();
			BtcHandicap.Broadcast();
		}
		else
		{
			BtcHandicap.SendMine();
		}
	}

	private static void PutHost()
	{
		BtcHandicap.LoadMine();
		string text = BtcHandicap.MyName();
		bool flag = !string.IsNullOrEmpty(text);
		if (flag)
		{
			BtcHandicap.Put(text, BtcHandicap._mine);
		}
	}

	public static void RegisterHandlers(NetworkClient clientOverride)
	{
		BtcHandicap.LoadMine();
		BtcHandicap._sentOnce = false;
		BtcHandicap._sentKey = null;
		bool active = NetworkServer.active;
		if (active)
		{
			NetworkServer.UnregisterHandler(88);
			NetworkServer.RegisterHandler(88, new NetworkMessageDelegate(BtcHandicap.OnSubmitAtHost));
			BtcHandicap._tab.Clear();
			BtcHandicap._ver++;
			BtcHandicap.PutHost();
		}
		UNetManager unetManager = NetworkManager.singleton as UNetManager;
		NetworkClient networkClient = clientOverride ?? ((unetManager != null) ? unetManager.LocalNetworkClient : null);
		bool flag = networkClient != null;
		if (flag)
		{
			networkClient.UnregisterHandler(87);
			networkClient.RegisterHandler(87, new NetworkMessageDelegate(BtcHandicap.OnStateAtClient));
			bool flag2 = !NetworkServer.active;
			if (flag2)
			{
				BtcHandicap._tab.Clear();
				BtcHandicap._ver++;
			}
		}
	}

	private static BtcHandicap.Msg StateMsg()
	{
		return new BtcHandicap.Msg
		{
			kind = 0,
			data = BtcHandicap.EncodeTable()
		};
	}

	public static void SendState(NetworkConnection conn)
	{
		bool flag = conn != null && BtcBc.BcEnabled;
		if (flag)
		{
			BtcHandicap.PutHost();
			conn.Send(87, BtcHandicap.StateMsg());
		}
	}

	private static void Broadcast()
	{
		bool flag = NetworkServer.active && BtcBc.BcEnabled;
		if (flag)
		{
			NetworkServer.SendToAll(87, BtcHandicap.StateMsg());
		}
	}

	private static void OnStateAtClient(NetworkMessage netMsg)
	{
		try
		{
			BtcHandicap.Msg msg = netMsg.ReadMessage<BtcHandicap.Msg>();
			bool active = NetworkServer.active;
			if (!active)
			{
				BtcHandicap.DecodeTable(msg.data);
				bool flag = !BtcHandicap._sentOnce;
				if (flag)
				{
					BtcHandicap._sentOnce = true;
					BtcHandicap.LoadMine();
					bool flag2 = !BtcHandicap.IsDefault(BtcHandicap._mine);
					if (flag2)
					{
						BtcHandicap.SendMine();
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void SendMine()
	{
		UNetManager unetManager = NetworkManager.singleton as UNetManager;
		bool flag = unetManager == null || unetManager.LocalNetworkClient == null || NetworkServer.active || !BtcBc.BcEnabled;
		if (!flag)
		{
			BtcHandicap._sentKey = BtcHandicap.Key(BtcHandicap.MyName());
			unetManager.LocalNetworkClient.Send(88, new BtcHandicap.Msg
			{
				kind = 1,
				name = BtcHandicap.MyName(),
				data = BtcHandicap.EncodeOne(BtcHandicap._mine)
			});
		}
	}

	private static void OnSubmitAtHost(NetworkMessage netMsg)
	{
		try
		{
			BtcHandicap.Msg msg = netMsg.ReadMessage<BtcHandicap.Msg>();
			bool flag = !NetworkServer.active || !BtcBc.BcEnabled;
			if (!flag)
			{
				string text = (UNetManager.instance != null) ? UNetManager.instance.GetLobbyPlayerNameFromConnection(netMsg.conn) : null;
				bool flag2 = string.IsNullOrEmpty(text);
				if (flag2)
				{
					text = msg.name;
				}
				int[] array = new int[6];
				bool flag3 = string.IsNullOrEmpty(text) || !BtcHandicap.DecodeOne(msg.data, array);
				if (!flag3)
				{
					BtcHandicap.Put(text, array);
					BtcHandicap.Broadcast();
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static int[] For(Faction f)
	{
		BtcHandicap.Tick();
		bool flag = f == null || BtcHandicap._tab.Count == 0 || !BtcBc.BcEnabled || BtcDuel.Active;
		int[] result;
		if (flag)
		{
			result = null;
		}
		else
		{
			float unscaledTime = Time.unscaledTime;
			bool flag2 = BtcHandicap._cacheVer != BtcHandicap._ver || unscaledTime > BtcHandicap._cacheUntil;
			if (flag2)
			{
				BtcHandicap._cache.Clear();
				BtcHandicap._cacheVer = BtcHandicap._ver;
				BtcHandicap._cacheUntil = unscaledTime + 3f;
			}
			int instanceID = f.GetInstanceID();
			int[] array;
			bool flag3 = BtcHandicap._cache.TryGetValue(instanceID, out array);
			if (flag3)
			{
				result = array;
			}
			else
			{
				array = null;
				List<User> allOwners = f.AllOwners;
				for (int i = 0; i < allOwners.Count; i++)
				{
					User user = allOwners[i];
					int[] array2 = default(int[]);
					bool flag4 = user != null && !string.IsNullOrEmpty(user.ScreenName) && BtcHandicap._tab.TryGetValue(BtcHandicap.Key(user.ScreenName), out array2);
					if (flag4)
					{
						array = ((array == null) ? ((int[])array2.Clone()) : BtcHandicap.Worst(array, array2));
					}
				}
				if (allOwners.Count > 0) BtcHandicap._cache[instanceID] = array;
				result = array;
			}
		}
		return result;
	}

	private static int[] Worst(int[] a, int[] b)
	{
		return new int[]
		{
			Mathf.Min(a[0], b[0]),
			Mathf.Min(a[1], b[1]),
			Mathf.Min(a[2], b[2]),
			Mathf.Max(a[3], b[3]),
			(a[4] < 0) ? b[4] : ((b[4] < 0) ? a[4] : Mathf.Min(a[4], b[4])),
			(a[5] < 0) ? b[5] : ((b[5] < 0) ? a[5] : Mathf.Min(a[5], b[5]))
		};
	}

	public static float Dmg(Faction f)
	{
		int[] array = BtcHandicap.For(f);
		return (array == null) ? 1f : ((float)array[0] / 100f);
	}

	public static float Hp(Faction f, float value)
	{
		int[] array = BtcHandicap.For(f);
		return (array == null) ? value : (value * (float)array[1] / 100f);
	}

	public static int Income(Faction f, int value)
	{
		int[] array = BtcHandicap.For(f);
		return (array == null) ? value : Mathf.RoundToInt((float)value * (float)array[2] / 100f);
	}

	public static int Refund(Faction f, int value)
	{
		int[] array = BtcHandicap.For(f);
		return (array == null || array[3] <= 0) ? value : Mathf.Max(0, value - array[3]);
	}

	public static int Cap(Faction f, int cap)
	{
		int[] array = BtcHandicap.For(f);
		return (array == null || array[4] < 0) ? cap : Mathf.Min(cap, array[4]);
	}

	public static int StartWorkers(Faction f, int count)
	{
		int[] array = BtcHandicap.For(f);
		return (array == null || array[5] < 0) ? count : Mathf.Min(count, array[5]);
	}

	private const string LuaStartWorkers =
		"if SetAsHomeCrystal ~= nil and sv_Settings ~= nil and __BtcHcStart ~= nil and Markers ~= nil then\n" +
		" local __h_base = nil\n" +
		" local __h_sh = SetAsHomeCrystal\n" +
		" SetAsHomeCrystal = function(c)\n" +
		"  __h_sh(c)\n" +
		"  pcall(function()\n" +
		"   if __h_base == nil then __h_base = sv_Settings.startingWorkersCount end\n" +
		"   local n = __BtcHcStart(c, __h_base)\n" +
		"   if n ~= nil then sv_Settings.startingWorkersCount = n end\n" +
		"  end)\n" +
		" end\n" +
		" local __h_gg = Markers.GetGroup\n" +
		" Markers.GetGroup = function(name)\n" +
		"  if name == 'Crystal' and __h_base ~= nil then\n" +
		"   sv_Settings.startingWorkersCount = __h_base\n" +
		"   __h_base = nil\n" +
		"  end\n" +
		"  return __h_gg(name)\n" +
		" end\n" +
		"end\n";

	private static readonly HashSet<int> _startLogged = new HashSet<int>();

	/// <summary>Asset_Gamemode.Disk_LoadGamemode Postfix: host + BC ON + new game, original conquest/sandbox only.</summary>
	internal static void InstallStartWorkers(Asset_Gamemode gm, Script script)
	{
		if (script == null || gm == null || !NetworkServer.active || !BtcBc.BcEnabled) return;
		Asset_Map map = GameParam.Map;
		if (map == null || !map.IsNewGame()) return;
		string folder = Path.GetFileName((gm.path ?? string.Empty).TrimEnd('/', '\\'));
		if (folder != "conquest" && folder != "sandbox") return;
		_startLogged.Clear();
		script.Globals["__BtcHcStart"] = DynValue.NewCallback(LuaStart, null);
		script.DoString(LuaStartWorkers, null, "btc_handicap_start");
	}

	// Lua: __BtcHcStart(crystal, vanillaCount) -> new count (nil = unchanged). Called right before each faction's start-workers loop.
	private static DynValue LuaStart(ScriptExecutionContext ctx, CallbackArguments args)
	{
		try
		{
			Component c = BtcSpawnPick.Obj(args, 0) as Component;
			if (c == null || args.Count < 2 || args[1].Type != DataType.Number) return DynValue.Nil;
			int vanilla = (int)args[1].Number;
			AffiliationComponent aff = c.GetComponent<AffiliationComponent>();
			Faction f = aff != null ? aff.faction : null;
			if (f == null || !f.HasOwner()) return DynValue.NewNumber(vanilla);
			int target = StartWorkers(f, vanilla);
			if (target == vanilla) return DynValue.NewNumber(vanilla);
			if (_startLogged.Add(f.GetInstanceID()))
				BtcLog.Info("Handicap", "start workers faction=" + f.name + " vanilla=" + vanilla + " target=" + target + " spawned=0 skipped=" + (vanilla - target));
			return DynValue.NewNumber(target);
		}
		catch (Exception e) { BtcLog.Error("Handicap", "LuaStart: " + e); return DynValue.Nil; }
	}

	public static string Label(string name)
	{
		int[] h = default(int[]);
		bool flag = BtcHandicap._tab.Count == 0 || string.IsNullOrEmpty(name) || !BtcBc.BcEnabled || !BtcHandicap._tab.TryGetValue(BtcHandicap.Key(name), out h);
		string result;
		if (flag)
		{
			result = string.Empty;
		}
		else
		{
			result = "   " + BtcHandicap.Summary(h);
		}
		return result;
	}

	private static string Summary(int[] h)
	{
		StringBuilder stringBuilder = new StringBuilder("[");
		bool flag = true;
		BtcHandicap.Part(stringBuilder, ref flag, h[0] != 100, BtcGameplay.T("dmg ", "урон ") + h[0] + "%");
		BtcHandicap.Part(stringBuilder, ref flag, h[1] != 100, BtcGameplay.T("HP ", "ХП ") + h[1] + "%");
		BtcHandicap.Part(stringBuilder, ref flag, h[2] != 100, BtcGameplay.T("income ", "доход ") + h[2] + "%");
		BtcHandicap.Part(stringBuilder, ref flag, h[3] > 0, BtcGameplay.T("death -", "смерть -") + h[3] + BtcGameplay.T(" energy", " энергии"));
		BtcHandicap.Part(stringBuilder, ref flag, h[4] >= 0, BtcGameplay.T("cap ", "кап ") + h[4]);
		BtcHandicap.Part(stringBuilder, ref flag, h[5] >= 0, BtcGameplay.T("start ", "старт ") + h[5]);
		return stringBuilder.Append("]").ToString();
	}

	private static void Part(StringBuilder sb, ref bool first, bool on, string text)
	{
		bool flag = !on;
		if (!flag)
		{
			bool flag2 = !first;
			if (flag2)
			{
				sb.Append(", ");
			}
			first = false;
			sb.Append(text);
		}
	}

	private static GUIStyle Make(int size, TextAnchor anchor, FontStyle fontStyle, Color color)
	{
		return new GUIStyle(GUI.skin.label)
		{
			fontSize = size,
			alignment = anchor,
			fontStyle = fontStyle,
			normal =
			{
				textColor = color
			},
			hover =
			{
				textColor = color
			},
			active =
			{
				textColor = color
			},
			focused =
			{
				textColor = color
			}
		};
	}

	private static void Styles(int fs)
	{
		bool flag = BtcHandicap._label != null && BtcHandicap._styleFs == fs;
		if (!flag)
		{
			BtcHandicap._styleFs = fs;
			BtcHandicap._label = BtcHandicap.Make(fs, TextAnchor.MiddleLeft, FontStyle.Normal, Color.white);
			BtcHandicap._note = BtcHandicap.Make(Mathf.Max(10, (int)((float)fs * 0.8f)), TextAnchor.UpperLeft, FontStyle.Normal, new Color(1f, 1f, 1f, 0.55f));
			BtcHandicap._note.wordWrap = true;
			BtcHandicap._sub = BtcHandicap.Make(fs, TextAnchor.MiddleLeft, FontStyle.Bold, BtcHandicap.Accent);
			BtcHandicap._btn = BtcHandicap.Make(Mathf.Max(10, (int)((float)fs * 0.9f)), TextAnchor.MiddleCenter, FontStyle.Normal, Color.white);
			BtcHandicap._small = BtcHandicap.Make(Mathf.Max(9, (int)((float)fs * 0.7f)), TextAnchor.MiddleCenter, FontStyle.Normal, Color.white);
		}
	}

	private static Texture2D White()
	{
		bool flag = BtcHandicap._white == null;
		if (flag)
		{
			BtcHandicap._white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			BtcHandicap._white.SetPixel(0, 0, Color.white);
			BtcHandicap._white.Apply();
			BtcHandicap._white.hideFlags = HideFlags.HideAndDontSave;
		}
		return BtcHandicap._white;
	}

	private static void Note(ref float y, float w, int fs, string text)
	{
		float num = BtcHandicap._note.CalcHeight(new GUIContent(text), w - (float)fs * 2f);
		GUI.Label(new Rect((float)fs, y + 2f, w - (float)fs * 2f, num), text, BtcHandicap._note);
		y += num + (float)fs * 0.5f;
	}

	private static bool Button(Event e, Rect r, string text, Color color, Color old)
	{
		bool flag = e.type == EventType.Repaint;
		if (flag)
		{
			GUI.color = (r.Contains(e.mousePosition) ? new Color(1f, 1f, 1f, 0.16f) : new Color(1f, 1f, 1f, 0.07f));
			GUI.DrawTexture(r, BtcHandicap.White());
			GUI.color = old;
		}
		Color textColor = BtcHandicap._btn.normal.textColor;
		BtcHandicap._btn.normal.textColor = color;
		bool flag2 = GUI.Button(r, text, BtcHandicap._btn);
		BtcHandicap._btn.normal.textColor = textColor;
		return flag2 && !BtcHotkeys.UiClickSwallowed;
	}

	private static int Stepper(Event e, ref float y, float w, float rowH, int fs, Color old, string label, int v, int def, int min, int max, int step, int offValue)
	{
		float num = y;
		y += rowH;
		GUI.Label(new Rect((float)fs, num, w * 0.5f, rowH), label, BtcHandicap._label);
		float num2 = rowH * 0.9f;
		Rect r = new Rect(w - (float)fs - num2, num + rowH * 0.05f, num2, rowH * 0.9f);
		Rect position = new Rect(r.x - num2 * 1.8f, num + rowH * 0.05f, num2 * 1.8f, rowH * 0.9f);
		Rect r2 = new Rect(position.x - num2, num + rowH * 0.05f, num2, rowH * 0.9f);
		Color color = (v != def) ? BtcHandicap.ChangedC : Color.white;
		GUI.Label(position, (v == offValue) ? BtcGameplay.T("off", "выкл") : v.ToString(), BtcHandicap._btn);
		bool flag = BtcHandicap.Button(e, r2, "-", color, old);
		if (flag)
		{
			v = Mathf.Max(min, v - step);
		}
		bool flag2 = BtcHandicap.Button(e, r, "+", color, old);
		if (flag2)
		{
			v = Mathf.Min(max, v + step);
		}
		return v;
	}

	private static int StepperOpt(Event e, ref float y, float w, float rowH, int fs, Color old, string label, int v, int firstOn, int max)
	{
		int v2 = (v < 0) ? (firstOn - 1) : v;
		int num = BtcHandicap.Stepper(e, ref y, w, rowH, fs, old, label, v2, firstOn - 1, firstOn - 1, max, 1, firstOn - 1);
		return (num < firstOn) ? -1 : num;
	}

	public static void Draw(Event e, ref float y, float w, float rowH, int fs, Color old)
	{
		BtcHandicap.LoadMine();
		BtcHandicap.Styles(fs);
		BtcHandicap.Note(ref y, w, fs, BtcGameplay.T("Personal handicap: you can only make the game harder for YOURSELF, never better than the host's rules. Everybody sees it in the pause menu (BetterConnection) next to your name. Works only with BetterConnection.", "Личная фора: можно только усложнить игру СЕБЕ, лучше правил хоста не сделать. Все видят её в меню паузы (BetterConnection) рядом с ником. Работает только с BetterConnection."));
		int[] mine = BtcHandicap._mine;
		int[] array = (int[])mine.Clone();
		GUI.Label(new Rect((float)fs, y, w, rowH), BtcGameplay.T("Your handicap", "Твоя фора"), BtcHandicap._sub);
		y += rowH;
		mine[0] = BtcHandicap.Stepper(e, ref y, w, rowH, fs, old, BtcGameplay.T("Damage of your units, %", "Урон твоих юнитов, %"), mine[0], 100, 10, 100, 5, -999);
		mine[1] = BtcHandicap.Stepper(e, ref y, w, rowH, fs, old, BtcGameplay.T("HP of your units, %", "ХП твоих юнитов, %"), mine[1], 100, 10, 100, 5, -999);
		mine[2] = BtcHandicap.Stepper(e, ref y, w, rowH, fs, old, BtcGameplay.T("Crystal energy income, %", "Доход энергии с кристаллов, %"), mine[2], 100, 10, 100, 5, -999);
		mine[3] = BtcHandicap.Stepper(e, ref y, w, rowH, fs, old, BtcGameplay.T("Extra energy lost per death (flat)", "Доп. потеря энергии за смерть (штучно)"), mine[3], 0, 0, 100, 5, -999);
		mine[4] = BtcHandicap.StepperOpt(e, ref y, w, rowH, fs, old, BtcGameplay.T("Own bricktron cap (max)", "Свой кап бриктронов (максимум)"), mine[4], 1, 100);
		mine[5] = BtcHandicap.StepperOpt(e, ref y, w, rowH, fs, old, BtcGameplay.T("Starting workers (max)", "Стартовые рабочие (максимум)"), mine[5], 0, 100);
		Rect r = new Rect((float)fs, y + rowH * 0.05f, w * 0.4f, rowH * 0.9f);
		bool flag = BtcHandicap.Button(e, r, BtcGameplay.T("Reset handicap", "Сбросить фору"), BtcHandicap.Warn, old);
		if (flag)
		{
			Array.Copy(BtcHandicap.Def, mine, 6);
		}
		y += rowH;
		bool flag2 = false;
		for (int i = 0; i < 6; i++)
		{
			flag2 |= (mine[i] != array[i]);
		}
		bool flag3 = flag2;
		if (flag3)
		{
			BtcHandicap.MineChanged();
		}
		bool flag4 = !BtcBc.BcEnabled;
		if (flag4)
		{
			BtcHandicap.Note(ref y, w, fs, BtcGameplay.T("BetterConnection is off: the handicap is saved but not applied.", "BetterConnection выключен: фора сохранена, но не применяется."));
		}
		BtcHandicap.Note(ref y, w, fs, BtcGameplay.T("Bricktron cap: can only LOWER the mode's cap (it limits new bricktrons; existing ones stay). Starting workers: applied at match start on the host (original conquest and sandbox only); can only lower the mode's count.", "Кап бриктронов: может только СНИЗИТЬ кап режима (ограничивает новых, существующие остаются). Стартовые рабочие: применяются при старте матча на хосте (только оригинальные conquest и sandbox); можно только уменьшить число режима."));
		GUI.Label(new Rect((float)fs, y, w, rowH), BtcGameplay.T("Players with a handicap", "Игроки с форой"), BtcHandicap._sub);
		y += rowH;
		bool flag5 = BtcHandicap._tab.Count == 0;
		if (flag5)
		{
			GUI.Label(new Rect((float)fs, y, w - (float)fs * 2f, rowH), BtcGameplay.T("Nobody", "Нет таких"), BtcHandicap._label);
			y += rowH;
		}
		foreach (KeyValuePair<string, int[]> keyValuePair in BtcHandicap._tab)
		{
			GUI.Label(new Rect((float)fs, y, w - (float)fs * 2f, rowH), keyValuePair.Key + "  " + BtcHandicap.Summary(keyValuePair.Value), BtcHandicap._label);
			y += rowH;
		}
		y += rowH * 0.2f;
	}

	private const int N = 6;

	private const char SepEntry = '\u001e';

	private const char SepName = '\u001f';

	private static readonly int[] Def = new int[]
	{
		100,
		100,
		100,
		0,
		-1,
		-1
	};

	private static readonly int[] Lo = new int[]
	{
		10,
		10,
		10,
		0,
		-1,
		-1
	};

	private static readonly int[] Hi = new int[]
	{
		100,
		100,
		100,
		100,
		100,
		100
	};

	private static readonly Dictionary<string, int[]> _tab = new Dictionary<string, int[]>();

	private static int[] _mine = (int[])BtcHandicap.Def.Clone();

	private static bool _loaded;

	private static int _ver;

	private static bool _sentOnce;

	private static string _sentKey;

	private static float _tickAt;

	private static readonly Dictionary<int, int[]> _cache = new Dictionary<int, int[]>();

	private static int _cacheVer = -1;

	private static float _cacheUntil;

	private static int _styleFs;

	private static GUIStyle _label;

	private static GUIStyle _note;

	private static GUIStyle _sub;

	private static GUIStyle _btn;

	private static GUIStyle _small;

	private static Texture2D _white;

	private static readonly Color Accent = new Color(0.55f, 0.85f, 0.55f, 1f);

	private static readonly Color Warn = new Color(1f, 0.45f, 0.45f, 1f);

	private static readonly Color ChangedC = new Color(1f, 0.85f, 0.4f, 1f);

	public sealed class Msg : MessageBase
	{
		public override void Serialize(NetworkWriter w)
		{
			w.Write(this.kind);
			w.Write(this.name ?? string.Empty);
			w.Write(this.data ?? string.Empty);
		}

		public override void Deserialize(NetworkReader r)
		{
			this.kind = r.ReadByte();
			this.name = r.ReadString();
			this.data = r.ReadString();
		}

		public byte kind;

		public string name = string.Empty;

		public string data = string.Empty;
	}
}
}
