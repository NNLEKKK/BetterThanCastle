using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Assets;
using Brix.Engine;
using Brix.Game;
using Brix.Game.Network;
using Brix.Network;
using Brix.UI.Icons;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
public static class BtcLobby
{
	public static void AddRows()
	{
		BtcGameplay.Add("gen.maxp", "gen", "Max players", "Макс. игроков", 4f, 2f, 8f, 1f, false, "2-8. Vanilla is 4. Applies at once in the lobby (nobody is kicked if you lower it below the current count, new players just cannot join). Needs a map with enough 'Player Starting Point' markers for that many colours.", "2-8. В ванили 4. Применяется сразу в лобби (если уменьшить ниже текущего числа, никого не кикает, просто новые не зайдут). Нужна карта с достаточным числом маркеров 'Player Starting Point'.");
		BtcLobby.iMaxP = BtcGameplay.Idx("gen.maxp");
		BtcGameplay.Add("gen.editor", "gen", "Enable editor panel", "Включить эдитор-панель", 0f, 0f, 1f, 1f, true, "Lets players use the in-game editor panel. This can make the game unfair, so everyone gets a red warning when it is on.", "Разрешает использовать эдитор-панель в игре. Это может сделать игру нечестной, поэтому при включении все получают красное предупреждение.");
		string[] array = new string[]
		{
			"red",
			"green",
			"blue"
		};
		string[] array2 = new string[]
		{
			"красный",
			"зелёный",
			"синий"
		};
		for (int i = 0; i < BtcLobby.ColName.Length; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				BtcGameplay.Add(string.Concat(new object[]
				{
					"gcol.",
					i,
					".",
					j
				}), "gcol", BtcLobby.ColEn[i] + ": " + array[j] + " (0-255)", BtcLobby.ColRu[i] + ": " + array2[j] + " (0-255)", (float)BtcLobby.DefRgb[i * 3 + j], 0f, 255f, 1f, false, null, null);
			}
		}
		BtcLobby.iCol0 = BtcGameplay.Idx("gcol.0.0");
		BtcSpectate.AddRows();
	}

	private static float Val(int i, float d)
	{
		float[] e = BtcGameplay.E;
		return (e != null && i >= 0 && i < e.Length) ? e[i] : d;
	}

	public static int AdjCap(int n)
	{
		bool flag = !BtcLobby.Active;
		int result;
		if (flag)
		{
			result = n;
		}
		else
		{
			int maxPlayers = BtcLobby.MaxPlayers;
			result = ((n > 0 && n < maxPlayers) ? maxPlayers : n);
		}
		return result;
	}

	public static Color ExtraColorFrom(float[] a, int k)
	{
		int num = BtcLobby.iCol0 + k * 3;
		bool flag = a == null || BtcLobby.iCol0 < 0 || num + 2 >= a.Length;
		Color result;
		if (flag)
		{
			result = new Color((float)BtcLobby.DefRgb[k * 3] / 255f, (float)BtcLobby.DefRgb[k * 3 + 1] / 255f, (float)BtcLobby.DefRgb[k * 3 + 2] / 255f, 1f);
		}
		else
		{
			result = new Color(a[num] / 255f, a[num + 1] / 255f, a[num + 2] / 255f, 1f);
		}
		// Rainbow (k 3, formerly Black) must stay bright on the minimap, scarves and capture bar: a dark saved value (old Black
		// default 28,28,32 in host presets) falls back to the bright default.
		if (k == 3 && Mathf.Max(result.r, Mathf.Max(result.g, result.b)) < 0.35f)
		{
			result = new Color((float)BtcLobby.DefRgb[9] / 255f, (float)BtcLobby.DefRgb[10] / 255f, (float)BtcLobby.DefRgb[11] / 255f, 1f);
		}
		return result;
	}

	private static Color ExtraColor(int k)
	{
		return BtcLobby.ExtraColorFrom(BtcGameplay.E, k);
	}

	private static uint ToUint(Color c)
	{
		uint num = (uint)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
		uint num2 = (uint)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
		uint num3 = (uint)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
		return num << 24 | num2 << 16 | num3 << 8 | 255U;
	}

	private static Color FromUint(uint i)
	{
		return new Color((i >> 24 & 255U) / 255f, (i >> 16 & 255U) / 255f, (i >> 8 & 255U) / 255f, 1f);
	}

	public static Asset_Gamemode.FactionPrototype[] ExtendProtos(Asset_Gamemode.FactionPrototype[] src)
	{
		Asset_Gamemode.FactionPrototype[] result;
		try
		{
			bool flag = src == null || src.Length == 0 || !BtcLobby.Active;
			if (flag)
			{
				result = src;
			}
			else
			{
				int num = -1;
				int num2 = 0;
				int num3 = 0;
				List<string> list = new List<string>();
				for (int i = 0; i < src.Length; i++)
				{
					bool flag2 = src[i].uid > num2;
					if (flag2)
					{
						num2 = src[i].uid;
					}
					list.Add(src[i].name);
					bool flag3 = !src[i].isAI && !src[i].isDefault && !src[i].isSpectator;
					if (flag3)
					{
						num = i;
						num3++;
					}
				}
				BtcLobby.PlayableColors.Clear();
				for (int j = 0; j < src.Length; j++)
				{
					bool flag4 = !src[j].isAI && !src[j].isDefault && !src[j].isSpectator;
					if (flag4)
					{
						BtcLobby.PlayableColors.Add(BtcLobby.FromUint(src[j].colour));
					}
				}
				bool flag5 = num < 0 || num3 >= 8 || !BtcLobby.ShouldExtend(src.Length);
				if (flag5)
				{
					result = src;
				}
				else
				{
					List<Asset_Gamemode.FactionPrototype> list2 = new List<Asset_Gamemode.FactionPrototype>(src.Length + BtcLobby.ColName.Length);
					for (int k = 0; k <= num; k++)
					{
						list2.Add(src[k]);
					}
					int num4 = num3;
					for (int l = 0; l < BtcLobby.ColName.Length; l++)
					{
						bool flag6 = num4 >= 8 || list.Contains(BtcLobby.ColName[l]);
						if (!flag6)
						{
							Color color = BtcLobby.ExtraColor(l);
							Asset_Gamemode.FactionPrototype item = src[num];
							// Stable uid per colour NAME (max gamemode uid + 1 + table index), not "next free": identical on every
							// machine even if a name is skipped (gamemode already has it) or the 8-cap cuts the list. Conquest/sandbox:
							// Purple 7, Pink 8, Cyan 9, Rainbow 10 (ids >= 8 need TrackingRegistryPatches + BtcFactionCap).
							item.uid = num2 + 1 + l;
							item.name = BtcLobby.ColName[l];
							item.colour = BtcLobby.ToUint(color);
							item.flagIcon = BtcLobby.Flag(l, color);
							item.crystalColor = BtcLobby.ColCrystal[l];
							list2.Add(item);
							BtcLobby.PlayableColors.Add(color);
							num4++;
						}
					}
					for (int m = num + 1; m < src.Length; m++)
					{
						list2.Add(src[m]);
					}
					result = list2.ToArray();
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("BtcLobby.ExtendProtos: " + ex.Message);
			result = src;
		}
		return result;
	}

	private static bool ShouldExtend(int srcLen)
	{
		bool result;
		try
		{
			Asset_Map map = GameParam.Map;
			bool flag = map == null || map.IsNewGame();
			if (flag)
			{
				result = true;
			}
			else
			{
				Faction.FactionMeta[] array = map.Disk_ReadFactionMeta();
				result = (array != null && array.Length > srcLen);
			}
		}
		catch (Exception)
		{
			result = false;
		}
		return result;
	}

	public static void OnFactionsRefreshed()
	{
		try
		{
			bool flag = !BtcLobby.Active;
			if (!flag)
			{
				GameParam.FactionInfo[] currentFactionInfos = GameParam.CurrentFactionInfos;
				bool flag2 = currentFactionInfos == null || currentFactionInfos.Length == 0;
				if (!flag2)
				{
					bool flag3 = BtcLobby._specInfo != null && currentFactionInfos[currentFactionInfos.Length - 1] == BtcLobby._specInfo;
					if (!flag3)
					{
						BtcLobby._specInfo = new GameParam.FactionInfo(BtcLobby.SpecSprite(), BtcGameplay.T("Spectator", "Наблюдатель"));
						GameParam.FactionInfo[] array = new GameParam.FactionInfo[currentFactionInfos.Length + 1];
						Array.Copy(currentFactionInfos, array, currentFactionInfos.Length);
						array[currentFactionInfos.Length] = BtcLobby._specInfo;
						GameParam.CurrentFactionInfos = array;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("BtcLobby.OnFactionsRefreshed: " + ex.Message);
		}
	}

	public static bool IsSpecNum(int n)
	{
		bool flag = BtcLobby._specInfo == null;
		bool result;
		if (flag)
		{
			result = false;
		}
		else
		{
			GameParam.FactionInfo[] currentFactionInfos = GameParam.CurrentFactionInfos;
			result = (currentFactionInfos != null && currentFactionInfos.Length != 0 && n == currentFactionInfos.Length - 1 && currentFactionInfos[currentFactionInfos.Length - 1] == BtcLobby._specInfo);
		}
		return result;
	}

	public static int SpecNum()
	{
		GameParam.FactionInfo[] currentFactionInfos = GameParam.CurrentFactionInfos;
		bool flag = BtcLobby._specInfo == null || currentFactionInfos == null || currentFactionInfos.Length == 0 || currentFactionInfos[currentFactionInfos.Length - 1] != BtcLobby._specInfo;
		int result;
		if (flag)
		{
			result = -1;
		}
		else
		{
			result = currentFactionInfos.Length - 1;
		}
		return result;
	}

	public static int ColorCount()
	{
		GameParam.FactionInfo[] currentFactionInfos = GameParam.CurrentFactionInfos;
		bool flag = currentFactionInfos == null;
		int result;
		if (flag)
		{
			result = 0;
		}
		else
		{
			int value = currentFactionInfos.Length - ((BtcLobby.SpecNum() >= 0) ? 1 : 0);
			result = Mathf.Clamp(value, 0, 8);
		}
		return result;
	}

	public static Color ColorOf(int n)
	{
		bool flag = BtcLobby.IsSpecNum(n);
		Color result;
		if (flag)
		{
			result = BtcLobby.SpecGrey;
		}
		else
		{
			bool flag2 = n >= 0 && n < BtcLobby.PlayableColors.Count;
			if (flag2)
			{
				result = BtcLobby.PlayableColors[n];
			}
			else
			{
				bool flag3 = n >= 0 && n < BtcLobby.VanillaPal.Length;
				if (flag3)
				{
					result = BtcLobby.VanillaPal[n];
				}
				else
				{
					bool flag4 = n >= BtcLobby.VanillaPal.Length && n < 8;
					if (flag4)
					{
						result = BtcLobby.ExtraColor(n - BtcLobby.VanillaPal.Length);
					}
					else
					{
						result = Color.white;
					}
				}
			}
		}
		return result;
	}

	public static List<int> AssignedFactions(List<LobbyPlayer> players)
	{
		List<int> list = new List<int>(8);
		for (int i = 0; i < players.Count; i++)
		{
			int factionNumber = players[i].factionNumber;
			bool flag = !BtcLobby.IsSpecNum(factionNumber) && !list.Contains(factionNumber);
			if (flag)
			{
				list.Add(factionNumber);
			}
		}
		return list;
	}

	public static List<int> AssignedTeams(List<LobbyPlayer> players)
	{
		List<int> list = new List<int>(8);
		for (int i = 0; i < players.Count; i++)
		{
			bool flag = !BtcLobby.IsSpecNum(players[i].factionNumber);
			if (flag)
			{
				int teamNumber = players[i].TeamNumber;
				bool flag2 = !list.Contains(teamNumber);
				if (flag2)
				{
					list.Add(teamNumber);
				}
			}
		}
		return list;
	}

	public static void MakeSpectator(User u)
	{
		try
		{
			Faction spectator = Faction.Spectator;
			bool flag = spectator != null && u != null;
			if (flag)
			{
				u.faction = spectator;
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("BtcLobby.MakeSpectator: " + ex.Message);
		}
	}

	/// <summary>Local-only visuals (not synced by vanilla) of an extended colour by faction name; false for vanilla names.</summary>
	public static bool ExtraVisuals(string name, out CrystalVisuals.CrystalColorEnum crystal, out Sprite flag)
	{
		crystal = CrystalVisuals.CrystalColorEnum.Neutral;
		flag = null;
		for (int i = 0; i < BtcLobby.ColName.Length; i++)
		{
			if (name != BtcLobby.ColName[i]) continue;
			crystal = BtcLobby.ColCrystal[i];
			try { flag = BtcLobby.Flag(i, BtcLobby.ExtraColor(i)); } catch (Exception) { flag = null; }
			return true;
		}
		return false;
	}

	public static Color ScarfColor(string name)
	{
		for (int i = 0; i < BtcLobby.ColName.Length; i++)
		{
			bool flag = name == BtcLobby.ColName[i];
			if (flag)
			{
				return BtcLobby.ExtraColor(i);
			}
		}
		return new Color(0f, 0f, 0f, 255f);
	}

	/// <summary>Team table seen by lobby code. Vanilla field is static readonly (4 entries) and cannot be swapped reliably
	/// (JIT may bake it); consumers are transpiled (GameParamTeamInfosPatch) to call this instead. BC ON + multiplayer: 8 entries.</summary>
	public static GameParam.TeamInfo[] TeamInfos()
	{
		GameParam.TeamInfo[] vanilla = GameParam.CurrentTeamInfos;
		try
		{
			if (!Active) return vanilla;
			if (_t8 == null || _t4 != vanilla)
			{
				_t4 = vanilla;
				List<GameParam.TeamInfo> list = new List<GameParam.TeamInfo>(vanilla);
				for (int i = list.Count; i < 8; i++)
					list.Add(new GameParam.TeamInfo(null, "Team " + ((char)(65 + i)).ToString()));
				_t8 = list.ToArray();
			}
			return _t8;
		}
		catch (Exception ex)
		{
			BtcLog.Error("Lobby", "TeamInfos: " + ex);
			return vanilla;
		}
	}

	public static int NetTeams() { return BtcNetworkGate.Enabled ? 8 : 4; }

	public static int NetSlots() { return BtcNetworkGate.Enabled ? 8 : 4; }

	public static bool TeamsOn()
	{
		bool result;
		try
		{
			result = (GameParam.Preset != null && GameParam.Preset.settings.ContainsKey("playerRelations") && (long)GameParam.Preset.settings["playerRelations"] == 2L);
		}
		catch (Exception)
		{
			result = false;
		}
		return result;
	}

	public static string TeamLetter(int t)
	{
		return ((char)(65 + Mathf.Clamp(t, 0, 25))).ToString();
	}

	public static bool PanelVisible()
	{
		bool flag = !BtcLobby.Active || !BtcGameplay.InLobby;
		bool result;
		if (flag)
		{
			result = false;
		}
		else
		{
			UNetManager instance = UNetManager.instance;
			result = (instance != null && instance.LocalLobbyPlayer != null && instance.LobbyPlayerList != null && instance.LobbyPlayerList.Count > 0);
		}
		return result;
	}

	public static void Tick()
	{
		float unscaledTime = Time.unscaledTime;
		bool flag = unscaledTime < BtcLobby._nextTick;
		if (!flag)
		{
			BtcLobby._nextTick = unscaledTime + 0.2f;
			bool flag2 = !BtcLobby._iconsReg;
			if (flag2)
			{
				BtcLobby._iconsReg = true;
				BtcLobby.RegisterIconKeys();
			}
			bool active = BtcLobby.Active;
			bool flag3 = !active;
			if (!flag3)
			{
				UNetManager unetManager = NetworkManager.singleton as UNetManager;
				bool flag4 = unetManager == null;
				if (!flag4)
				{
					bool inLobby = BtcGameplay.InLobby;
					bool flag5 = NetworkServer.active && inLobby;
					if (flag5)
					{
						int maxPlayers = BtcLobby.MaxPlayers;
						bool flag6 = unetManager.maxConnections != maxPlayers;
						if (flag6)
						{
							unetManager.maxConnections = maxPlayers;
						}
						bool flag7 = unetManager.maxPlayers != maxPlayers;
						if (flag7)
						{
							unetManager.maxPlayers = maxPlayers;
						}
						BtcLobby.SteamLimit(maxPlayers);
					}
					int num = BtcLobby.ColorHash();
					bool flag8 = num != BtcLobby._colHash;
					if (flag8)
					{
						BtcLobby._colHash = num;
						bool flag9 = inLobby;
						if (flag9)
						{
							try
							{
								GameParam.RefreshFactionsAndTeams();
							}
							catch (Exception)
							{
							}
						}
					}
				}
			}
		}
	}

	private static int ColorHash()
	{
		int num = 17;
		for (int i = 0; i < 12; i++)
		{
			num = num * 31 + Mathf.RoundToInt(BtcLobby.Val(BtcLobby.iCol0 + i, 0f));
		}
		return num;
	}

	private static void SteamLimit(int cap)
	{
		try
		{
			bool flag = !SteamManager.Initialized;
			if (!flag)
			{
				CSteamID lobbyId = SteamManager.Instance.LobbyId;
				bool flag2 = !lobbyId.IsValid();
				if (!flag2)
				{
					bool flag3 = lobbyId.m_SteamID != BtcLobby._lastLobby;
					if (flag3)
					{
						BtcLobby._lastLobby = lobbyId.m_SteamID;
						BtcLobby._lastSteamCap = -1;
					}
					bool flag4 = cap != BtcLobby._lastSteamCap && SteamMatchmaking.SetLobbyMemberLimit(lobbyId, cap);
					if (flag4)
					{
						BtcLobby._lastSteamCap = cap;
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static Sprite Flag(int k, Color c)
	{
		uint num = BtcLobby.ToUint(c);
		Sprite sprite;
		uint num2;
		bool flag = BtcLobby._flags.TryGetValue(k, out sprite) && sprite != null && BtcLobby._flagCol.TryGetValue(k, out num2) && num2 == num;
		Sprite result;
		if (flag)
		{
			result = sprite;
		}
		else
		{
			bool flag2 = sprite != null;
			if (flag2)
			{
				UnityEngine.Object.Destroy(sprite.texture);
				UnityEngine.Object.Destroy(sprite);
			}
			sprite = BtcLobby.MakeFlag(c, BtcLobby.ColName[k] == "Rainbow");
			BtcLobby._flags[k] = sprite;
			BtcLobby._flagCol[k] = num;
			result = sprite;
		}
		return result;
	}

	private static Sprite SpecSprite()
	{
		bool flag = BtcLobby._specSprite == null;
		if (flag)
		{
			BtcLobby._specSprite = BtcLobby.MakeFlag(new Color(0.6f, 0.6f, 0.64f, 1f));
		}
		return BtcLobby._specSprite;
	}

	private static Sprite MakeFlag(Color target)
	{
		return BtcLobby.MakeFlag(target, false);
	}

	// rainbow: coloured pixels of the vanilla flag get a bright diagonal rainbow instead of the target hue.
	private static Sprite MakeFlag(Color target, bool rainbow)
	{
		try
		{
			Sprite sprite = Faction.IconKeyMap["Blue"].Get64();
			bool flag = sprite != null && sprite.texture != null;
			if (flag)
			{
				Texture2D texture = sprite.texture;
				Rect textureRect = sprite.textureRect;
				int num = (int)textureRect.width;
				int num2 = (int)textureRect.height;
				RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0);
				Graphics.Blit(texture, temporary);
				RenderTexture active = RenderTexture.active;
				RenderTexture.active = temporary;
				Texture2D texture2D = new Texture2D(num, num2, TextureFormat.RGBA32, false);
				texture2D.ReadPixels(new Rect(textureRect.x, textureRect.y, (float)num, (float)num2), 0, 0);
				RenderTexture.active = active;
				RenderTexture.ReleaseTemporary(temporary);
				float h;
				float num3;
				float num4;
				Color.RGBToHSV(target, out h, out num3, out num4);
				Color[] pixels = texture2D.GetPixels();
				for (int i = 0; i < pixels.Length; i++)
				{
					float num5;
					float num6;
					float num7;
					Color.RGBToHSV(pixels[i], out num5, out num6, out num7);
					bool flag2 = num6 > 0.2f;
					if (flag2)
					{
						float a = pixels[i].a;
						Color color = Color.HSVToRGB(h, Mathf.Clamp01(num6 * (num3 / 0.7f)), Mathf.Clamp01(num7 * (num4 / 0.9f)));
						if (rainbow)
						{
							float rh = ((float)(i % num) + (float)(i / num) * 0.6f) / ((float)num * 1.6f);
							color = Color.HSVToRGB(rh - Mathf.Floor(rh), 1f, Mathf.Max(num7, 0.85f));
						}
						color.a = a;
						pixels[i] = color;
					}
				}
				texture2D.SetPixels(pixels);
				texture2D.Apply();
				texture2D.hideFlags = HideFlags.HideAndDontSave;
				return Sprite.Create(texture2D, new Rect(0f, 0f, (float)num, (float)num2), new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit);
			}
		}
		catch (Exception)
		{
		}
		return BtcLobby.MakeDisc(rainbow ? new Color(1f, 0.2f, 0.6f, 1f) : target);
	}

	private static Sprite MakeDisc(Color c)
	{
		Texture2D texture2D = new Texture2D(32, 32, TextureFormat.RGBA32, false);
		for (int i = 0; i < 32; i++)
		{
			for (int j = 0; j < 32; j++)
			{
				float num = (float)(i - 16) + 0.5f;
				float num2 = (float)(j - 16) + 0.5f;
				bool flag = num * num + num2 * num2 <= 225f;
				texture2D.SetPixel(i, j, flag ? c : new Color(0f, 0f, 0f, 0f));
			}
		}
		texture2D.Apply();
		texture2D.hideFlags = HideFlags.HideAndDontSave;
		return Sprite.Create(texture2D, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
	}

	public static void RegisterIconKeys()
	{
		try
		{
			string[] array = new string[]
			{
				"Pink",
				"Cyan",
				"Rainbow"
			};
			IconKey[] array2 = new IconKey[]
			{
				IconKeys.FlagRed,
				IconKeys.FlagBlue,
				IconKeys.FlagGreen
			};
			for (int i = 0; i < array.Length; i++)
			{
				bool flag = !Faction.IconKeyMap.ContainsKey(array[i]);
				if (flag)
				{
					Faction.IconKeyMap[array[i]] = array2[i];
				}
			}
		}
		catch (Exception)
		{
		}
	}

	// Note: this type is marked as 'beforefieldinit'.
	static BtcLobby()
	{
		CrystalVisuals.CrystalColorEnum[] array = new CrystalVisuals.CrystalColorEnum[4];
		array[0] = CrystalVisuals.CrystalColorEnum.Blue;
		array[1] = CrystalVisuals.CrystalColorEnum.Red;
		array[2] = CrystalVisuals.CrystalColorEnum.Green;
		array[3] = CrystalVisuals.CrystalColorEnum.Corrupted;
		BtcLobby.ColCrystal = array;
		BtcLobby.VanillaPal = new Color[]
		{
			new Color(0f, 0.53f, 1f, 1f),
			new Color(0f, 0.8f, 0.2f, 1f),
			new Color(1f, 0.78f, 0.31f, 1f),
			new Color(0.855f, 0f, 0.216f, 1f)
		};
		BtcLobby.SpecGrey = new Color(0.62f, 0.62f, 0.66f, 1f);
		BtcLobby.iMaxP = -1;
		BtcLobby.iCol0 = -1;
		BtcLobby.PlayableColors = new List<Color>();
		BtcLobby._lastSteamCap = -1;
		BtcLobby._flags = new Dictionary<int, Sprite>();
		BtcLobby._flagCol = new Dictionary<int, uint>();
	}

	public static bool Active
	{
		get
		{
			return BtcBc.BcEnabled && Neo.multiplayer;
		}
	}

	public static int MaxPlayers
	{
		get
		{
			bool flag = !BtcLobby.Active || BtcLobby.iMaxP < 0;
			int result;
			if (flag)
			{
				result = 4;
			}
			else
			{
				result = Mathf.Clamp(Mathf.RoundToInt(BtcLobby.Val(BtcLobby.iMaxP, 4f)), 2, 8);
			}
			return result;
		}
	}

	public static int HostCap
	{
		get
		{
			return BtcLobby.Active ? 16 : 4;
		}
	}

	public const int MaxSlots = 8;

	public const int HostSlots = 16;

	private static readonly string[] ColName = new string[]
	{
		"Purple",
		"Pink",
		"Cyan",
		"Rainbow"
	};

	public static readonly string[] ColRu = new string[]
	{
		"Фиолетовый",
		"Розовый",
		"Бирюзовый",
		"Радужный"
	};

	public static readonly string[] ColEn = BtcLobby.ColName;

	private static readonly int[] DefRgb = new int[]
	{
		138,
		43,
		226,
		255,
		105,
		180,
		0,
		229,
		238,
		255,
		255,
		255
	};

	private static readonly CrystalVisuals.CrystalColorEnum[] ColCrystal;

	public static readonly Color[] VanillaPal;

	private static readonly Color SpecGrey;

	private static int iMaxP;

	private static int iCol0;

	private static readonly List<Color> PlayableColors;

	private static GameParam.FactionInfo _specInfo;

	private static GameParam.TeamInfo[] _t4;

	private static GameParam.TeamInfo[] _t8;

	private static FieldInfo _tf;

	private static bool _teamsFailed;

	private static float _nextTick;

	private static int _colHash;

	private static int _lastSteamCap;

	private static ulong _lastLobby;

	private static bool _iconsReg;

	private static readonly Dictionary<int, Sprite> _flags;

	private static readonly Dictionary<int, uint> _flagCol;

	private static Sprite _specSprite;
}
}
