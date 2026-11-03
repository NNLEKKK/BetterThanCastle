using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Network;
using Brix.UI.Icons;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
public static class BtcRecipes
{
	private static void Init()
	{
		bool init = BtcRecipes._init;
		if (!init)
		{
			BtcRecipes._init = true;
			BtcRecipes._rec = new RecipeInfo[]
			{
				CookBook.Sword,
				CookBook.Shield,
				CookBook.Helmet,
				CookBook.Bow,
				CookBook.Quiver,
				CookBook.Cap,
				CookBook.CrossbowWeapon,
				CookBook.CrossbowPouch,
				CookBook.CrossbowMorion,
				CookBook.AlchemistPouch,
				CookBook.AlchemistHat,
				CookBook.Staff,
				CookBook.Sachel,
				CookBook.ArtificerHat,
				CookBook.Poleaxe,
				CookBook.KettleHat,
				CookBook.RopeBallot,
				CookBook.FabricPile,
				CookBook.Planks,
				CookBook.IronIngot,
				CookBook.GlassIngot,
				CookBook.Cog,
				CookBook.Bomb,
				CookBook.BearTrap,
				CookBook.ExplosiveBarrel,
				CookBook.SentinelWard,
				CookBook.HealingWard,
				CookBook.CaptureWard,
				CookBook.LanternWard,
				CookBook.PylonWard,
				CookBook.FireFly
			};
			BtcRecipes._en = new string[]
			{
				"Sword",
				"Shield",
				"Helmet",
				"Bow",
				"Quiver",
				"Cap",
				"Crossbow",
				"Crossbow pouch",
				"Morion",
				"Alchemist pouch",
				"Alchemist hat",
				"Staff",
				"Satchel",
				"Artificer hat",
				"Poleaxe",
				"Kettle hat",
				"Rope",
				"Fabric",
				"Planks",
				"Iron ingot",
				"Glass ingot",
				"Cog",
				"Bomb",
				"Bear trap",
				"Explosive barrel",
				"Sentinel Ward",
				"Healing Ward",
				"Capture Ward",
				"Lantern Ward",
				"Pylon Ward",
				"Firefly"
			};
			BtcRecipes._ru = new string[]
			{
				"Меч",
				"Щит",
				"Шлем",
				"Лук",
				"Колчан",
				"Шапка",
				"Арбалет",
				"Подсумок арбалетчика",
				"Морион",
				"Сумка алхимика",
				"Шляпа алхимика",
				"Посох",
				"Сумка артифисера",
				"Шляпа артифисера",
				"Алебарда",
				"Каска",
				"Верёвка",
				"Ткань",
				"Доски",
				"Железный слиток",
				"Стеклянный слиток",
				"Шестерня",
				"Бомба",
				"Капкан",
				"Взрывная бочка",
				"Страж-вард",
				"Лечащий вард",
				"Захватывающий вард",
				"Фонарь-вард",
				"Пилон-вард",
				"Светлячок"
			};
			BtcRecipes._res = new Ressource[]
			{
				Adjectif.iron,
				Adjectif.plankBlock,
				Adjectif.woodBlock,
				Adjectif.stones,
				Adjectif.rawIron,
				Adjectif.rope,
				Adjectif.fabric,
				Adjectif.glass,
				Adjectif.cog,
				Adjectif.plant,
				Adjectif.blueCrystal,
				Adjectif.orangeCrystal
			};
			BtcRecipes._resEn = new string[]
			{
				"Iron",
				"Planks",
				"Wood",
				"Stone",
				"Raw iron",
				"Rope",
				"Fabric",
				"Glass",
				"Cog",
				"Plants",
				"Blue crystal",
				"Orange crystal"
			};
			BtcRecipes._resRu = new string[]
			{
				"Железо",
				"Доски",
				"Дерево",
				"Камень",
				"Руда",
				"Верёвка",
				"Ткань",
				"Стекло",
				"Шестерня",
				"Растения",
				"Синий кристалл",
				"Оранжевый кристалл"
			};
			BtcRecipes._resIcon = new IconKey[]
			{
				IconKeys._Iron,
				IconKeys._Plank,
				IconKeys.Wood_SingleLog,
				IconKeys._Raw_Stone,
				IconKeys._Raw_Iron,
				IconKeys._Rope,
				IconKeys._Fabric,
				IconKeys._Glass_Ingot,
				IconKeys._Cog,
				IconKeys._Plant,
				IconKeys._Raw_Blue_Crystal,
				IconKeys._Raw_Orange_Crystal
			};
			BtcRecipes._resType = new Type[12];
			for (int i = 0; i < 12; i++)
			{
				BtcRecipes._resType[i] = BtcRecipes._res[i].GetType();
			}
			BtcRecipes._groups = new BtcRecipes.Group[]
			{
				BtcRecipes.G("Altar (wards)", "Алтарь (варды)", IconKeys._Altar, new int[]
				{
					25,
					26,
					27,
					28,
					29
				}),
				BtcRecipes.G("Knight kit", "Набор рыцаря", IconKeys._Sword, new int[]
				{
					0,
					1,
					2
				}),
				BtcRecipes.G("Archer kit", "Набор лучника", IconKeys._Bow, new int[]
				{
					3,
					4,
					5
				}),
				BtcRecipes.G("Arbalist kit", "Набор арбалетчика", IconKeys._Crossbow, new int[]
				{
					6,
					7,
					8
				}),
				BtcRecipes.G("Artificer kit", "Набор артифисера", IconKeys._Staff, new int[]
				{
					11,
					12,
					13
				}),
				BtcRecipes.G("Alchemist kit", "Набор алхимика", IconKeys.AlchemistHat, new int[]
				{
					9,
					10
				}),
				BtcRecipes.G("Halberdier kit", "Набор алебардщика", IconKeys._Poleaxe, new int[]
				{
					14,
					15
				}),
				BtcRecipes.G("Materials", "Материалы", IconKeys._Furnace, new int[]
				{
					16,
					17,
					18,
					19,
					20,
					21
				}),
				BtcRecipes.G("Traps & explosives", "Ловушки и взрывчатка", IconKeys._Beartrap, new int[]
				{
					22,
					23,
					24
				}),
				BtcRecipes.G("Other", "Прочее", IconKeys.FireflyYellow, new int[]
				{
					30
				})
			};
			BtcRecipes._fIng = typeof(RecipeInfo).GetField("ingredients", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			BtcRecipes._fWork = typeof(RecipeInfo).GetField("work", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			BtcRecipes._fHeat = typeof(RecipeInfo).GetField("heat", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			BtcRecipes._fQty = typeof(RecipeInfo).GetField("QtyCreated", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			int num = BtcRecipes._rec.Length * 15;
			BtcRecipes._van = new int[num];
			for (int j = 0; j < BtcRecipes._rec.Length; j++)
			{
				BtcRecipes.ReadRecipe(j, BtcRecipes._van);
			}
			BtcRecipes._cur = (int[])BtcRecipes._van.Clone();
			BtcRecipes._prop = (int[])BtcRecipes._van.Clone();
			BtcRecipes._app = (int[])BtcRecipes._van.Clone();
			try
			{
				BtcRecipes.Decode(PlayerPrefs.GetString("BtcRecipes", string.Empty), BtcRecipes._cur);
				Array.Copy(BtcRecipes._cur, BtcRecipes._prop, num);
			}
			catch (Exception)
			{
			}
		}
	}

	private static BtcRecipes.Group G(string en, string ru, IconKey icon, params int[] idx)
	{
		return new BtcRecipes.Group
		{
			En = en,
			Ru = ru,
			Icon = icon,
			Idx = idx
		};
	}

	private static Description Ing(RecipeInfo r)
	{
		return (Description)BtcRecipes._fIng.GetValue(r);
	}

	private static void ReadRecipe(int k, int[] dst)
	{
		RecipeInfo recipeInfo = BtcRecipes._rec[k];
		int num = k * 15;
		dst[num] = recipeInfo.work;
		dst[num + 1] = recipeInfo.heat;
		dst[num + 2] = recipeInfo.QtyCreated;
		DicoAdjectif dicoAdjectif = BtcRecipes.Ing(recipeInfo).DicoAdjectif;
		for (int i = 0; i < 12; i++)
		{
			Adjectif adjectif;
			dst[num + 3 + i] = (dicoAdjectif.TryGetValue(BtcRecipes._resType[i], out adjectif) ? adjectif.quantifiable.valeur : 0);
		}
	}

	private static void Apply(int[] src)
	{
		BtcRecipes.Init();
		for (int i = 0; i < BtcRecipes._rec.Length; i++)
		{
			int num = i * 15;
			bool flag = false;
			for (int j = 0; j < 15; j++)
			{
				bool flag2 = src[num + j] != BtcRecipes._app[num + j];
				if (flag2)
				{
					flag = true;
					break;
				}
			}
			bool flag3 = !flag;
			if (!flag3)
			{
				RecipeInfo recipeInfo = BtcRecipes._rec[i];
				BtcRecipes._fWork.SetValue(recipeInfo, src[num]);
				BtcRecipes._fHeat.SetValue(recipeInfo, src[num + 1]);
				BtcRecipes._fQty.SetValue(recipeInfo, src[num + 2]);
				DicoAdjectif dicoAdjectif = BtcRecipes.Ing(recipeInfo).DicoAdjectif;
				for (int k = 0; k < 12; k++)
				{
					int num2 = src[num + 3 + k];
					Type key = BtcRecipes._resType[k];
					bool flag4 = num2 <= 0;
					if (flag4)
					{
						dicoAdjectif.Remove(key);
					}
					else
					{
						Adjectif adjectif;
						bool flag5 = dicoAdjectif.TryGetValue(key, out adjectif);
						if (flag5)
						{
							adjectif.quantifiable.valeur = num2;
						}
						else
						{
							Adjectif adjectif2 = BtcRecipes._res[k].Clone();
							adjectif2.quantifiable.valeur = num2;
							dicoAdjectif.Add(adjectif2);
						}
					}
					BtcRecipes._app[num + 3 + k] = num2;
				}
				BtcRecipes._app[num] = src[num];
				BtcRecipes._app[num + 1] = src[num + 1];
				BtcRecipes._app[num + 2] = src[num + 2];
			}
		}
	}

	private static bool Differs(int[] a, int k)
	{
		int num = k * 15;
		for (int i = 0; i < 15; i++)
		{
			bool flag = a[num + i] != BtcRecipes._van[num + i];
			if (flag)
			{
				return true;
			}
		}
		return false;
	}

	// Adds one line per recipe that differs from vanilla (current synced state). Used by the rules-changed banner.
	public static void AppendDiffLines(List<string> list)
	{
		try
		{
			if (BtcRecipes._cur == null || BtcRecipes._van == null || BtcRecipes._rec == null || !BtcBc.BcEnabled) return;
			for (int i = 0; i < BtcRecipes._rec.Length; i++)
			{
				if (BtcRecipes.Differs(BtcRecipes._cur, i)) list.Add(BtcRecipes.T("Recipe: ", "Рецепт: ") + BtcRecipes.T(BtcRecipes._en[i], BtcRecipes._ru[i]));
			}
		}
		catch (Exception)
		{
		}
	}

	private static bool _stateSeen;

	private static string Encode(int[] a)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < BtcRecipes._rec.Length; i++)
		{
			bool flag = BtcRecipes.Differs(a, i);
			if (flag)
			{
				stringBuilder.Append(i).Append(':');
				for (int j = 0; j < 15; j++)
				{
					bool flag2 = j > 0;
					if (flag2)
					{
						stringBuilder.Append(',');
					}
					stringBuilder.Append(a[i * 15 + j]);
				}
				stringBuilder.Append(';');
			}
		}
		return stringBuilder.ToString();
	}

	private static bool Decode(string s, int[] dst)
	{
		Array.Copy(BtcRecipes._van, dst, BtcRecipes._van.Length);
		bool flag = string.IsNullOrEmpty(s);
		bool result;
		if (flag)
		{
			result = true;
		}
		else
		{
			try
			{
				string[] array = s.Split(new char[]
				{
					';'
				}, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < array.Length; i++)
				{
					string[] array2 = array[i].Split(new char[]
					{
						':'
					});
					int num = int.Parse(array2[0]);
					string[] array3 = array2[1].Split(new char[]
					{
						','
					});
					bool flag2 = num < 0 || num >= BtcRecipes._rec.Length || array3.Length != 15;
					if (flag2)
					{
						throw new FormatException();
					}
					for (int j = 0; j < 15; j++)
					{
						int value = int.Parse(array3[j]);
						int min = (j == 2) ? 1 : 0;
						int max = (j == 0) ? 999 : ((j == 1) ? 99 : 99);
						dst[num * 15 + j] = Mathf.Clamp(value, min, max);
					}
				}
				result = true;
			}
			catch (Exception)
			{
				Array.Copy(BtcRecipes._van, dst, BtcRecipes._van.Length);
				result = false;
			}
		}
		return result;
	}

	private static int ChangedCount(int[] a)
	{
		int num = 0;
		for (int i = 0; i < BtcRecipes._rec.Length; i++)
		{
			bool flag = BtcRecipes.Differs(a, i);
			if (flag)
			{
				num++;
			}
		}
		return num;
	}

	public static void RegisterHandlers(NetworkClient clientOverride)
	{
		BtcRecipes.Init();
		bool active = NetworkServer.active;
		if (active)
		{
			NetworkServer.UnregisterHandler(85);
			NetworkServer.RegisterHandler(85, new NetworkMessageDelegate(BtcRecipes.OnRequestAtHost));
			BtcRecipes.Apply(BtcBc.BcEnabled ? BtcRecipes._cur : BtcRecipes._van);
		}
		UNetManager unetManager = NetworkManager.singleton as UNetManager;
		NetworkClient networkClient = clientOverride ?? ((unetManager != null) ? unetManager.LocalNetworkClient : null);
		bool flag = networkClient != null;
		if (flag)
		{
			networkClient.UnregisterHandler(84);
			networkClient.UnregisterHandler(86);
			networkClient.RegisterHandler(84, new NetworkMessageDelegate(BtcRecipes.OnStateAtClient));
			networkClient.RegisterHandler(86, new NetworkMessageDelegate(BtcRecipes.OnResultAtClient));
			BtcRecipes._stateSeen = false;
			bool flag2 = !NetworkServer.active;
			if (flag2)
			{
				Array.Copy(BtcRecipes._van, BtcRecipes._cur, BtcRecipes._van.Length);
				BtcRecipes.Apply(BtcRecipes._cur);
			}
		}
	}

	private static BtcRecipes.Msg StateMsg()
	{
		return new BtcRecipes.Msg
		{
			kind = 0,
			data = BtcRecipes.Encode(BtcRecipes._cur)
		};
	}

	public static void SendState(NetworkConnection conn)
	{
		BtcRecipes.Init();
		bool flag = conn != null && BtcBc.BcEnabled;
		if (flag)
		{
			conn.Send(84, BtcRecipes.StateMsg());
		}
	}

	private static void Broadcast()
	{
		bool flag = NetworkServer.active && BtcBc.BcEnabled;
		if (flag)
		{
			NetworkServer.SendToAll(84, BtcRecipes.StateMsg());
		}
	}

	private static void OnStateAtClient(NetworkMessage netMsg)
	{
		try
		{
			BtcRecipes.Msg msg = netMsg.ReadMessage<BtcRecipes.Msg>();
			bool active = NetworkServer.active;
			if (!active)
			{
				BtcRecipes.Init();
				int[] before = (int[])BtcRecipes._cur.Clone();
				BtcRecipes.Decode(msg.data, BtcRecipes._cur);
				bool changedNow = BtcRecipes.ChangedCount(BtcRecipes._cur) > 0;
				bool differsFromBefore = false;
				for (int q = 0; q < before.Length && q < BtcRecipes._cur.Length; q++) { if (before[q] != BtcRecipes._cur[q]) { differsFromBefore = true; break; } }
				if (!BtcRecipes._stateSeen) { BtcRecipes._stateSeen = true; if (changedNow) BtcGameplay.RaiseWarn(1); }
				else if (differsFromBefore) BtcGameplay.RaiseWarn(2);
				BtcRecipes.Apply(BtcRecipes._cur);
				bool flag = !BtcRecipes._propDirty;
				if (flag)
				{
					Array.Copy(BtcRecipes._cur, BtcRecipes._prop, BtcRecipes._cur.Length);
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void OnRequestAtHost(NetworkMessage netMsg)
	{
		try
		{
			BtcRecipes.Msg msg = netMsg.ReadMessage<BtcRecipes.Msg>();
			bool flag = !NetworkServer.active || !BtcBc.BcEnabled;
			if (!flag)
			{
				BtcRecipes.Init();
				int connectionId = netMsg.conn.connectionId;
				for (int i = BtcRecipes._pending.Count - 1; i >= 0; i--)
				{
					bool flag2 = BtcRecipes._pending[i].ConnId == connectionId;
					if (flag2)
					{
						BtcRecipes._pending.RemoveAt(i);
					}
				}
				bool flag3 = BtcRecipes._pending.Count >= 16;
				if (!flag3)
				{
					int[] array = new int[BtcRecipes._van.Length];
					bool flag4 = !BtcRecipes.Decode(msg.data, array);
					if (!flag4)
					{
						BtcRecipes._pending.Add(new BtcRecipes.Req
						{
							ConnId = connectionId,
							Rid = msg.rid,
							Name = (string.IsNullOrEmpty(msg.name) ? ("#" + connectionId) : msg.name),
							Vals = array
						});
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void OnResultAtClient(NetworkMessage netMsg)
	{
		try
		{
			BtcRecipes.Msg msg = netMsg.ReadMessage<BtcRecipes.Msg>();
			bool flag = msg.rid == BtcRecipes._myRid;
			if (flag)
			{
				BtcRecipes._myState = ((msg.data == "ok") ? 2 : 3);
			}
		}
		catch (Exception)
		{
		}
	}

	private static void SendRequest()
	{
		UNetManager unetManager = NetworkManager.singleton as UNetManager;
		bool flag = unetManager == null || unetManager.LocalNetworkClient == null || NetworkServer.active;
		if (!flag)
		{
			string name = (unetManager.LocalLobbyPlayer != null) ? unetManager.LocalLobbyPlayer.playerName : "?";
			BtcRecipes._myRid = ++BtcRecipes._ridCounter;
			BtcRecipes._myState = 1;
			BtcRecipes._propDirty = false;
			unetManager.LocalNetworkClient.Send(85, new BtcRecipes.Msg
			{
				kind = 1,
				rid = BtcRecipes._myRid,
				name = name,
				data = BtcRecipes.Encode(BtcRecipes._prop)
			});
		}
	}

	private static void Resolve(BtcRecipes.Req r, bool approve)
	{
		BtcRecipes._pending.Remove(r);
		bool flag = approve && BtcGameplay.CanEdit;
		bool flag2 = flag;
		if (flag2)
		{
			Array.Copy(r.Vals, BtcRecipes._cur, BtcRecipes._cur.Length);
			BtcRecipes.HostChanged();
		}
		for (int i = 0; i < NetworkServer.connections.Count; i++)
		{
			NetworkConnection networkConnection = NetworkServer.connections[i];
			bool flag3 = networkConnection != null && networkConnection.connectionId == r.ConnId;
			if (flag3)
			{
				networkConnection.Send(86, new BtcRecipes.Msg
				{
					kind = 2,
					rid = r.Rid,
					data = (flag ? "ok" : "no")
				});
				break;
			}
		}
	}

	private static void HostChanged()
	{
		bool bcEnabled = BtcBc.BcEnabled;
		if (bcEnabled)
		{
			BtcRecipes.Apply(BtcRecipes._cur);
		}
		PlayerPrefs.SetString("BtcRecipes", BtcRecipes.Encode(BtcRecipes._cur));
		BtcRecipes.Broadcast();
	}

	private static void Styles(int fs)
	{
		bool flag = BtcRecipes._label != null && BtcRecipes._styleFs == fs;
		if (!flag)
		{
			BtcRecipes._styleFs = fs;
			BtcRecipes._label = BtcRecipes.Make(GUI.skin.label, fs, TextAnchor.MiddleLeft, FontStyle.Normal, Color.white);
			BtcRecipes._note = BtcRecipes.Make(GUI.skin.label, Mathf.Max(10, (int)((float)fs * 0.8f)), TextAnchor.UpperLeft, FontStyle.Normal, new Color(1f, 1f, 1f, 0.55f));
			BtcRecipes._note.wordWrap = true;
			BtcRecipes._sub = BtcRecipes.Make(GUI.skin.label, fs, TextAnchor.MiddleLeft, FontStyle.Bold, BtcRecipes.Accent);
			BtcRecipes._sub.padding = new RectOffset(fs, 0, 0, 0);
			BtcRecipes._btn = BtcRecipes.Make(GUI.skin.label, Mathf.Max(10, (int)((float)fs * 0.9f)), TextAnchor.MiddleCenter, FontStyle.Normal, Color.white);
			BtcRecipes._small = BtcRecipes.Make(GUI.skin.label, Mathf.Max(9, (int)((float)fs * 0.7f)), TextAnchor.MiddleCenter, FontStyle.Normal, Color.white);
		}
	}

	private static GUIStyle Make(GUIStyle basis, int size, TextAnchor anchor, FontStyle fontStyle, Color color)
	{
		return new GUIStyle(basis)
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

	private static Texture2D White()
	{
		bool flag = BtcRecipes._white == null;
		if (flag)
		{
			BtcRecipes._white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			BtcRecipes._white.SetPixel(0, 0, Color.white);
			BtcRecipes._white.Apply();
			BtcRecipes._white.hideFlags = HideFlags.HideAndDontSave;
		}
		return BtcRecipes._white;
	}

	private static string T(string en, string ru)
	{
		return BtcGameplay.T(en, ru);
	}

	private static void Note(ref float y, float w, int fs, string text)
	{
		float num = BtcRecipes._note.CalcHeight(new GUIContent(text), w - (float)fs * 2f);
		GUI.Label(new Rect((float)fs, y + 2f, w - (float)fs * 2f, num), text, BtcRecipes._note);
		y += num + (float)fs * 0.5f;
	}

	private static bool Button(Event e, Rect r, string text, Color color, Color old)
	{
		bool flag = e.type == EventType.Repaint;
		if (flag)
		{
			GUI.color = (r.Contains(e.mousePosition) ? new Color(1f, 1f, 1f, 0.16f) : new Color(1f, 1f, 1f, 0.07f));
			GUI.DrawTexture(r, BtcRecipes.White());
			GUI.color = old;
		}
		Color textColor = BtcRecipes._btn.normal.textColor;
		BtcRecipes._btn.normal.textColor = color;
		bool flag2 = GUI.Button(r, text, BtcRecipes._btn);
		BtcRecipes._btn.normal.textColor = textColor;
		return flag2 && !BtcHotkeys.UiClickSwallowed;
	}

	private static void DrawIcon(Rect r, IconKey key)
	{
		bool flag = key == null || Event.current.type != EventType.Repaint;
		if (!flag)
		{
			Sprite sprite;
			bool flag2 = !BtcRecipes._sprites.TryGetValue(key, out sprite);
			if (flag2)
			{
				sprite = key.Get64();
				BtcRecipes._sprites[key] = sprite;
			}
			bool flag3 = sprite == null || sprite.texture == null;
			if (!flag3)
			{
				Texture2D texture = sprite.texture;
				Rect textureRect = sprite.textureRect;
				GUI.DrawTextureWithTexCoords(r, texture, new Rect(textureRect.x / (float)texture.width, textureRect.y / (float)texture.height, textureRect.width / (float)texture.width, textureRect.height / (float)texture.height));
			}
		}
	}

	private static bool Square(Event e, Rect r, IconKey icon, string caption, bool selected, bool changed, Color old)
	{
		float num = (float)BtcRecipes._small.fontSize + 4f;
		bool flag = e.type == EventType.Repaint;
		if (flag)
		{
			GUI.color = (r.Contains(e.mousePosition) ? new Color(1f, 1f, 1f, 0.16f) : new Color(1f, 1f, 1f, 0.07f));
			GUI.DrawTexture(r, BtcRecipes.White());
			if (selected)
			{
				GUI.color = BtcRecipes.Accent;
				GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), BtcRecipes.White());
				GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), BtcRecipes.White());
				GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), BtcRecipes.White());
				GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), BtcRecipes.White());
			}
			GUI.color = old;
		}
		BtcRecipes.DrawIcon(new Rect(r.x + 4f, r.y + 3f, r.width - 8f, r.height - num - 4f), icon);
		bool flag2 = e.type == EventType.Repaint;
		if (flag2)
		{
			if (changed)
			{
				GUI.color = BtcRecipes.ChangedC;
				GUI.DrawTexture(new Rect(r.xMax - 9f, r.y + 3f, 6f, 6f), BtcRecipes.White());
				GUI.color = old;
			}
			GUI.Label(new Rect(r.x, r.yMax - num, r.width, num), caption, BtcRecipes._small);
		}
		return GUI.Button(r, GUIContent.none, GUIStyle.none) && !BtcHotkeys.UiClickSwallowed;
	}

	private static int Stepper(Event e, ref float y, float w, float rowH, int fs, Color old, string label, int v, int vanilla, int min, int max, int step, bool edit)
	{
		Rect rect = new Rect(0f, y, w, rowH);
		y += rowH;
		GUI.Label(new Rect((float)fs, rect.y, w * 0.5f, rowH), label, BtcRecipes._label);
		float num = rowH * 0.9f;
		float num2 = w - (float)fs - num;
		Rect r = new Rect(num2, rect.y + rowH * 0.05f, num, rowH * 0.9f);
		Rect position = new Rect(num2 - num * 1.8f, rect.y + rowH * 0.05f, num * 1.8f, rowH * 0.9f);
		Rect r2 = new Rect(position.x - num, rect.y + rowH * 0.05f, num, rowH * 0.9f);
		Color color = (v != vanilla) ? BtcRecipes.ChangedC : Color.white;
		GUI.Label(position, v.ToString(), BtcRecipes._btn);
		bool flag = v != vanilla;
		if (flag)
		{
			GUI.Label(new Rect(r2.x - num * 2.2f, rect.y, num * 2.2f, rowH), "(" + vanilla + ")", BtcRecipes._small);
		}
		bool flag2 = BtcRecipes.Button(e, r2, "-", edit ? color : BtcRecipes.Dim, old);
		bool flag3 = BtcRecipes.Button(e, r, "+", edit ? color : BtcRecipes.Dim, old);
		bool flag4 = edit && flag2;
		if (flag4)
		{
			v = Mathf.Max(min, v - step);
		}
		bool flag5 = edit && flag3;
		if (flag5)
		{
			v = Mathf.Min(max, v + step);
		}
		return v;
	}

	public static void Draw(Event e, ref float y, float w, float rowH, int fs, Color old, bool interact, bool isHost, bool inSession)
	{
		BtcRecipes.Init();
		BtcRecipes.Styles(fs);
		int[] array = isHost ? BtcRecipes._cur : BtcRecipes._prop;
		int num = BtcRecipes.ChangedCount(array);
		Rect position = new Rect(0f, y, w, rowH);
		y += rowH;
		bool flag = e.type == EventType.Repaint && position.Contains(e.mousePosition);
		if (flag)
		{
			GUI.color = new Color(1f, 1f, 1f, 0.05f);
			GUI.DrawTexture(position, BtcRecipes.White());
			GUI.color = old;
		}
		GUI.Label(position, (BtcRecipes._open ? "v  " : ">  ") + BtcRecipes.T("Recipes", "Рецепты") + ((num > 0) ? ("   (" + num + ")") : string.Empty), BtcRecipes._sub);
		bool flag2 = GUI.Button(position, GUIContent.none, GUIStyle.none) && !BtcHotkeys.UiClickSwallowed;
		if (flag2)
		{
			BtcRecipes._open = !BtcRecipes._open;
		}
		bool flag3 = !BtcRecipes._open;
		if (!flag3)
		{
			bool flag4 = false;
			bool flag5 = !isHost && inSession;
			if (flag5)
			{
				BtcRecipes.Note(ref y, w, fs, BtcRecipes.T("Request mode: edit recipes below, then send them to the host.", "Режим запроса: правьте рецепты ниже и отправьте хосту."));
			}
			else
			{
				bool flag6 = !interact;
				if (flag6)
				{
					BtcRecipes.Note(ref y, w, fs, BtcRecipes.T("Recipes can be edited by the host in the lobby.", "Рецепты правит хост в лобби."));
				}
			}
			float num2 = rowH * 1.9f;
			float num3 = 6f;
			int num4 = Mathf.Max(1, (int)((w - (float)fs * 2f + num3) / (num2 + num3)));
			for (int i = 0; i < BtcRecipes._groups.Length; i++)
			{
				BtcRecipes.Group group = BtcRecipes._groups[i];
				float x = (float)fs + (float)(i % num4) * (num2 + num3);
				float y2 = y + (float)(i / num4) * (num2 + num3);
				bool flag7 = false;
				for (int j = 0; j < group.Idx.Length; j++)
				{
					flag7 |= BtcRecipes.Differs(array, group.Idx[j]);
				}
				bool flag8 = BtcRecipes.Square(e, new Rect(x, y2, num2, num2), group.Icon, BtcRecipes.T(group.En, group.Ru), BtcRecipes._grp == i, flag7, old);
				if (flag8)
				{
					BtcRecipes._grp = ((BtcRecipes._grp == i) ? -1 : i);
					BtcRecipes._sel = -1;
					BtcRecipes._rs = -1;
					BtcRecipes._adding = false;
				}
			}
			y += (float)((BtcRecipes._groups.Length + num4 - 1) / num4) * (num2 + num3) + 2f;
			bool flag9 = BtcRecipes._grp < 0;
			if (!flag9)
			{
				BtcRecipes.Group group2 = BtcRecipes._groups[BtcRecipes._grp];
				GUI.Label(new Rect((float)fs, y, w, rowH * 0.8f), BtcRecipes.T(group2.En, group2.Ru), BtcRecipes._label);
				y += rowH * 0.85f;
				for (int k = 0; k < group2.Idx.Length; k++)
				{
					int num5 = group2.Idx[k];
					float x2 = (float)fs + (float)(k % num4) * (num2 + num3);
					float y3 = y + (float)(k / num4) * (num2 + num3);
					bool flag10 = BtcRecipes.Square(e, new Rect(x2, y3, num2, num2), BtcRecipes._rec[num5].icon, BtcRecipes.T(BtcRecipes._en[num5], BtcRecipes._ru[num5]), BtcRecipes._sel == num5, BtcRecipes.Differs(array, num5), old);
					if (flag10)
					{
						BtcRecipes._sel = ((BtcRecipes._sel == num5) ? -1 : num5);
						BtcRecipes._rs = -1;
						BtcRecipes._adding = false;
					}
				}
				y += (float)((group2.Idx.Length + num4 - 1) / num4) * (num2 + num3) + 2f;
				bool flag11 = BtcRecipes._sel < 0;
				if (!flag11)
				{
					int sel = BtcRecipes._sel;
					int num6 = sel * 15;
					GUI.Label(new Rect((float)fs, y, w, rowH), BtcRecipes.T(BtcRecipes._en[sel], BtcRecipes._ru[sel]), BtcRecipes._sub);
					y += rowH;
					int num7 = array[num6];
					int num8 = array[num6 + 1];
					int num9 = array[num6 + 2];
					num7 = BtcRecipes.Stepper(e, ref y, w, rowH, fs, old, BtcRecipes.T("Work", "Работа"), num7, BtcRecipes._van[num6], 0, 999, 1, interact);
					num8 = BtcRecipes.Stepper(e, ref y, w, rowH, fs, old, BtcRecipes.T("Heat", "Жар"), num8, BtcRecipes._van[num6 + 1], 0, 99, 1, interact);
					num9 = BtcRecipes.Stepper(e, ref y, w, rowH, fs, old, BtcRecipes.T("Output quantity", "Количество на выходе"), num9, BtcRecipes._van[num6 + 2], 1, 99, 1, interact);
					flag4 |= (num7 != array[num6] || num8 != array[num6 + 1] || num9 != array[num6 + 2]);
					array[num6] = num7;
					array[num6 + 1] = num8;
					array[num6 + 2] = num9;
					GUI.Label(new Rect((float)fs, y, w, rowH * 0.8f), BtcRecipes.T("Ingredients", "Ингредиенты"), BtcRecipes._label);
					y += rowH * 0.85f;
					float num10 = rowH * 1.6f;
					int num11 = Mathf.Max(1, (int)((w - (float)fs * 2f + num3) / (num10 + num3)));
					int num12 = 0;
					for (int l = 0; l < 12; l++)
					{
						bool flag12 = array[num6 + 3 + l] <= 0;
						if (!flag12)
						{
							float x3 = (float)fs + (float)(num12 % num11) * (num10 + num3);
							float y4 = y + (float)(num12 / num11) * (num10 + num3);
							bool flag13 = BtcRecipes.Square(e, new Rect(x3, y4, num10, num10), BtcRecipes._resIcon[l], "x" + array[num6 + 3 + l], BtcRecipes._rs == l, array[num6 + 3 + l] != BtcRecipes._van[num6 + 3 + l], old);
							if (flag13)
							{
								BtcRecipes._rs = ((BtcRecipes._rs == l) ? -1 : l);
								BtcRecipes._adding = false;
							}
							num12++;
						}
					}
					if (interact)
					{
						float x4 = (float)fs + (float)(num12 % num11) * (num10 + num3);
						float y5 = y + (float)(num12 / num11) * (num10 + num3);
						bool flag14 = BtcRecipes.Square(e, new Rect(x4, y5, num10, num10), null, "+", BtcRecipes._adding, false, old);
						if (flag14)
						{
							BtcRecipes._adding = !BtcRecipes._adding;
							BtcRecipes._rs = -1;
						}
						num12++;
					}
					y += (float)((num12 + num11 - 1) / num11) * (num10 + num3) + 2f;
					bool flag15 = BtcRecipes._rs >= 0 && array[num6 + 3 + BtcRecipes._rs] > 0;
					if (flag15)
					{
						int rs = BtcRecipes._rs;
						int num13 = array[num6 + 3 + rs];
						num13 = BtcRecipes.Stepper(e, ref y, w, rowH, fs, old, BtcRecipes.T(BtcRecipes._resEn[rs], BtcRecipes._resRu[rs]), num13, BtcRecipes._van[num6 + 3 + rs], 1, 99, 1, interact);
						flag4 |= (num13 != array[num6 + 3 + rs]);
						array[num6 + 3 + rs] = num13;
						bool flag16 = interact && BtcRecipes.Button(e, new Rect((float)fs, y + rowH * 0.05f, w * 0.4f, rowH * 0.9f), BtcRecipes.T("Remove from recipe", "Убрать из рецепта"), BtcRecipes.Warn, old);
						if (flag16)
						{
							array[num6 + 3 + rs] = 0;
							BtcRecipes._rs = -1;
							flag4 = true;
						}
						y += rowH;
					}
					bool flag17 = BtcRecipes._adding && interact;
					if (flag17)
					{
						GUI.Label(new Rect((float)fs, y, w, rowH * 0.8f), BtcRecipes.T("Add a resource:", "Добавить ресурс:"), BtcRecipes._note);
						y += rowH * 0.7f;
						int num14 = 0;
						for (int m = 0; m < 12; m++)
						{
							bool flag18 = array[num6 + 3 + m] > 0;
							if (!flag18)
							{
								float x5 = (float)fs + (float)(num14 % num11) * (num10 + num3);
								float y6 = y + (float)(num14 / num11) * (num10 + num3);
								bool flag19 = BtcRecipes.Square(e, new Rect(x5, y6, num10, num10), BtcRecipes._resIcon[m], BtcRecipes.T(BtcRecipes._resEn[m], BtcRecipes._resRu[m]), false, false, old);
								if (flag19)
								{
									array[num6 + 3 + m] = 1;
									BtcRecipes._rs = m;
									BtcRecipes._adding = false;
									flag4 = true;
								}
								num14++;
							}
						}
						y += (float)((num14 + num11 - 1) / num11) * (num10 + num3) + 2f;
					}
					if (interact)
					{
						float num15 = (w - (float)fs * 2f - 6f) / 2f;
						bool flag20 = BtcRecipes.Button(e, new Rect((float)fs, y + rowH * 0.05f, num15, rowH * 0.9f), BtcRecipes.T("Reset this recipe", "Сбросить рецепт"), Color.white, old);
						if (flag20)
						{
							Array.Copy(BtcRecipes._van, num6, array, num6, 15);
							BtcRecipes._rs = -1;
							flag4 = true;
						}
						bool flag21 = BtcRecipes.Button(e, new Rect((float)fs + num15 + 6f, y + rowH * 0.05f, num15, rowH * 0.9f), BtcRecipes.T("Reset all recipes", "Сбросить все рецепты"), BtcRecipes.Warn, old);
						if (flag21)
						{
							Array.Copy(BtcRecipes._van, array, array.Length);
							BtcRecipes._rs = -1;
							flag4 = true;
						}
						y += rowH;
					}
					bool flag22 = flag4;
					if (flag22)
					{
						if (isHost)
						{
							BtcRecipes.HostChanged();
						}
						else
						{
							BtcRecipes._propDirty = true;
							BtcRecipes._myState = 0;
						}
					}
					bool flag23 = !isHost && inSession && interact;
					if (flag23)
					{
						string text = BtcRecipes.T("Send recipes to host", "Отправить рецепты хосту");
						bool flag24 = BtcRecipes._myState == 1;
						if (flag24)
						{
							text = BtcRecipes.T("Waiting for the host...", "Ждём решения хоста...");
						}
						else
						{
							bool flag25 = BtcRecipes._myState == 2;
							if (flag25)
							{
								text = BtcRecipes.T("Approved! Send again", "Одобрено! Отправить снова");
							}
							else
							{
								bool flag26 = BtcRecipes._myState == 3;
								if (flag26)
								{
									text = BtcRecipes.T("Rejected. Send again", "Отклонено. Отправить снова");
								}
							}
						}
						bool flag27 = BtcRecipes.Button(e, new Rect((float)fs, y + rowH * 0.05f, w - (float)fs * 2f, rowH * 0.9f), text, BtcRecipes.Accent, old);
						if (flag27)
						{
							BtcRecipes.SendRequest();
						}
						y += rowH;
					}
					bool flag28 = isHost && BtcRecipes._pending.Count > 0;
					if (flag28)
					{
						for (int n = BtcRecipes._pending.Count - 1; n >= 0; n--)
						{
							BtcRecipes.Req req = BtcRecipes._pending[n];
							GUI.Label(new Rect((float)fs, y, w * 0.5f, rowH), string.Concat(new object[]
							{
								req.Name,
								": ",
								BtcRecipes.ChangedCount(req.Vals),
								" ",
								BtcRecipes.T("recipes", "рецептов")
							}), BtcRecipes._label);
							float num16 = w * 0.22f;
							bool flag29 = BtcRecipes.Button(e, new Rect(w - (float)fs - num16 * 2f - 6f, y + rowH * 0.05f, num16, rowH * 0.9f), BtcRecipes.T("Accept", "Принять"), BtcRecipes.Accent, old);
							bool flag30 = BtcRecipes.Button(e, new Rect(w - (float)fs - num16, y + rowH * 0.05f, num16, rowH * 0.9f), BtcRecipes.T("Reject", "Отклонить"), BtcRecipes.Warn, old);
							y += rowH;
							bool flag31 = (flag29 && interact) || flag30;
							if (flag31)
							{
								BtcRecipes.Resolve(req, flag29 && interact);
							}
						}
					}
					y += rowH * 0.2f;
				}
			}
		}
	}

	private const int NRes = 12;

	private const int Stride = 15;

	private static bool _init;

	private static RecipeInfo[] _rec;

	private static string[] _en;

	private static string[] _ru;

	private static Ressource[] _res;

	private static Type[] _resType;

	private static string[] _resEn;

	private static string[] _resRu;

	private static IconKey[] _resIcon;

	private static BtcRecipes.Group[] _groups;

	private static FieldInfo _fIng;

	private static FieldInfo _fWork;

	private static FieldInfo _fHeat;

	private static FieldInfo _fQty;

	private static int[] _van;

	private static int[] _cur;

	private static int[] _prop;

	private static int[] _app;

	private static bool _propDirty;

	private static readonly List<BtcRecipes.Req> _pending = new List<BtcRecipes.Req>();

	private static int _ridCounter;

	private static int _myRid;

	private static int _myState;

	private static bool _open;

	private static int _grp = -1;

	private static int _sel = -1;

	private static int _rs = -1;

	private static bool _adding;

	private static readonly Dictionary<IconKey, Sprite> _sprites = new Dictionary<IconKey, Sprite>();

	private static Texture2D _white;

	private static int _styleFs;

	private static GUIStyle _label;

	private static GUIStyle _note;

	private static GUIStyle _sub;

	private static GUIStyle _btn;

	private static GUIStyle _small;

	private static readonly Color Accent = new Color(0.55f, 0.85f, 0.55f, 1f);

	private static readonly Color Warn = new Color(1f, 0.45f, 0.45f, 1f);

	private static readonly Color ChangedC = new Color(1f, 0.85f, 0.4f, 1f);

	private static readonly Color Dim = new Color(1f, 1f, 1f, 0.35f);

	public sealed class Msg : MessageBase
	{
		public override void Serialize(NetworkWriter w)
		{
			w.Write(this.kind);
			w.Write(this.rid);
			w.Write(this.name ?? string.Empty);
			w.Write(this.data ?? string.Empty);
		}

		public override void Deserialize(NetworkReader r)
		{
			this.kind = r.ReadByte();
			this.rid = r.ReadInt32();
			this.name = r.ReadString();
			this.data = r.ReadString();
		}

		public byte kind;

		public int rid;

		public string name = string.Empty;

		public string data = string.Empty;
	}

	private sealed class Group
	{
		public string En;

		public string Ru;

		public IconKey Icon;

		public int[] Idx;
	}

	private sealed class Req
	{
		public int ConnId;

		public int Rid;

		public string Name;

		public int[] Vals;
	}
}
}
