using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Engine;
using Brix.Network;
using Brix.NewUI.UserSettings_;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
public static partial class BtcNick
{
	private static string CfgPath
	{
		get
		{
			return BtcPaths.Config("btc_nick.txt");
		}
	}

	private static void Load()
	{
		BtcNick._loaded = true;
		BtcNick._styleOn = BtcMenuState.Bool("profile:on", true);
		BtcNick._enforceUntil = Time.unscaledTime + 20f;
		try
		{
			string cfgPath = BtcNick.CfgPath;
			bool flag = !File.Exists(cfgPath);
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				string[] array = File.ReadAllLines(cfgPath);
				for (int i = 0; i < array.Length; i++)
				{
					string text = array[i].Trim();
					int num = text.IndexOf('=');
					bool flag5 = num <= 0;
					bool flag6 = !flag5;
					bool flag7 = flag6;
					bool flag8 = flag7;
					if (flag8)
					{
						string a = text.Substring(0, num).Trim();
						string text2 = text.Substring(num + 1).Trim();
						bool flag9 = a == "snick";
						bool flag10 = flag9;
						bool flag11 = flag10;
						if (flag11)
						{
							BtcNick._nick[1] = BtcNick.Clean(text2, 30);
						}
						else
						{
							bool flag12 = a == "nick";
							bool flag13 = flag12;
							bool flag14 = flag13;
							bool flag15 = flag14;
							if (flag15)
							{
								BtcNick._nick[0] = BtcNick.Clean(text2, 999);
							}
							else
							{
								bool flag16 = a == "save";
								bool flag17 = flag16;
								bool flag18 = flag17;
								bool flag19 = flag18;
								if (flag19)
								{
									BtcNick._save = (text2 == "1");
								}
								else
								{
									bool flag20 = a == "auto";
									bool flag21 = flag20;
									bool flag22 = flag21;
									bool flag23 = flag22;
									if (flag23)
									{
										BtcNick._auto = (text2 != "0");
									}
									else
									{
										bool flag24 = a == "hist";
										bool flag25 = flag24;
										bool flag26 = flag25;
										bool flag27 = flag26;
										if (flag27)
										{
											bool flag28 = text2.Length > 0 && BtcNick._hist.Count < 8 && !BtcNick._hist.Contains(text2);
											bool flag29 = flag28;
											bool flag30 = flag29;
											bool flag31 = flag30;
											if (flag31)
											{
												BtcNick._hist.Add(text2);
											}
										}
										else
										{
											bool flag32 = a == "anim";
											bool flag33 = flag32;
											bool flag34 = flag33;
											bool flag35 = flag34;
											if (flag35)
											{
												BtcNick._anim = (text2 == "1");
											}
											else
											{
												bool flag36 = a == "aspeed";
												bool flag37 = flag36;
												bool flag38 = flag37;
												bool flag39 = flag38;
												if (flag39)
												{
													BtcNick._aSpeed = Mathf.Clamp(BtcNick.F(text2, 0.3f), 0.05f, 2f);
												}
												else
												{
													bool flag40 = a == "arate";
													bool flag41 = flag40;
													bool flag42 = flag41;
													bool flag43 = flag42;
													if (flag43)
													{
														BtcNick._aRate = Mathf.Clamp(BtcNick.F(text2, 5f), 1f, 10f);
													}
													else
													{
														bool flag44 = a == "spread";
														bool flag45 = flag44;
														bool flag46 = flag45;
														bool flag47 = flag46;
														if (flag47)
														{
															BtcNick._spread = Mathf.Clamp(BtcNick.F(text2, 1f), 1f, 6f);
														}
														else
														{
															bool flag48 = a == "rev";
															bool flag49 = flag48;
															bool flag50 = flag49;
															bool flag51 = flag50;
															if (flag51)
															{
																BtcNick._rev = (text2 == "1");
															}
															else
															{
																bool flag52 = a == "grad";
																bool flag53 = flag52;
																bool flag54 = flag53;
																bool flag55 = flag54;
																if (flag55)
																{
																	BtcNick._gradSel = -1;
																	for (int j = 0; j < BtcNick.PName.Length; j++)
																	{
																		bool flag56 = BtcNick.PName[j] == text2;
																		bool flag57 = flag56;
																		bool flag58 = flag57;
																		bool flag59 = flag58;
																		if (flag59)
																		{
																			BtcNick._gradSel = j;
																		}
																	}
																}
																else
																{
																	bool flag60 = a == "custom";
																	bool flag61 = flag60;
																	bool flag62 = flag61;
																	bool flag63 = flag62;
																	if (flag63)
																	{
																		BtcNick._cust.Clear();
																		string[] array2 = text2.Split(new char[]
																		{
																			','
																		});
																		int num2 = 0;
																		while (num2 < array2.Length && BtcNick._cust.Count < 6)
																		{
																			int item;
																			bool flag64 = BtcNick.TryHex(array2[num2].Trim(), out item);
																			bool flag65 = flag64;
																			bool flag66 = flag65;
																			bool flag67 = flag66;
																			if (flag67)
																			{
																				BtcNick._cust.Add(item);
																			}
																			num2++;
																		}
																		BtcNick._custArr = null;
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
						}
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void SaveCfg()
	{
		try
		{
			List<string> list = new List<string>();
			list.Add("save=" + (BtcNick._save ? "1" : "0"));
			list.Add("auto=" + (BtcNick._auto ? "1" : "0"));
			bool save = BtcNick._save;
			bool flag = save;
			bool flag2 = flag;
			if (flag2)
			{
				bool flag3 = BtcNick._nick[0].Length > 0;
				bool flag4 = flag3;
				bool flag5 = flag4;
				if (flag5)
				{
					list.Add("nick=" + BtcNick._nick[0]);
				}
				bool flag6 = BtcNick._nick[1].Length > 0;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					list.Add("snick=" + BtcNick._nick[1]);
				}
			}
			list.Add("anim=" + (BtcNick._anim ? "1" : "0"));
			list.Add("aspeed=" + BtcNick.FS(BtcNick._aSpeed));
			list.Add("arate=" + BtcNick.FS(BtcNick._aRate));
			list.Add("spread=" + BtcNick.FS(BtcNick._spread));
			list.Add("rev=" + (BtcNick._rev ? "1" : "0"));
			list.Add("grad=" + ((BtcNick._gradSel >= 0 && BtcNick._gradSel < BtcNick.PName.Length) ? BtcNick.PName[BtcNick._gradSel] : "custom"));
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < BtcNick._cust.Count; i++)
			{
				bool flag9 = i > 0;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					stringBuilder.Append(',');
				}
				stringBuilder.Append(BtcNick._cust[i].ToString("x6"));
			}
			list.Add("custom=" + stringBuilder);
			for (int j = 0; j < BtcNick._hist.Count; j++)
			{
				list.Add("hist=" + BtcNick._hist[j]);
			}
			File.WriteAllLines(BtcNick.CfgPath, list.ToArray());
		}
		catch (Exception)
		{
		}
	}

	private static string Current()
	{
		return string.IsNullOrEmpty(Neo.multiplayerName) ? string.Empty : Neo.multiplayerName;
	}

	private static void Apply()
	{
		string text = BtcNick.Clean(BtcNick._text, BtcNick.Lim);
		bool flag = BtcNick.Plain(text).Length == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcNick.Say(BtcNick.T("The nick cannot be empty."));
		}
		else
		{
			try
			{
				BtcNick._text = text;
				BtcNick.SyncFromText();
				BtcNick._nick[BtcNick._slot] = text;
				bool flag4 = BtcNick._slot == BtcNick.Active();
				bool flag5 = flag4;
				bool flag6 = flag5;
				bool flag7 = flag6;
				if (flag7)
				{
					BtcNick.SetName(BtcNick.NameFor(BtcNick._slot));
				}
				bool save = BtcNick._save;
				bool flag8 = save;
				bool flag9 = flag8;
				if (flag9)
				{
					UserSettingsReader.Save(UserSettingManager.GetSettingsFromGame());
				}
				BtcNick._hist.Remove(text);
				BtcNick._hist.Insert(0, text);
				while (BtcNick._hist.Count > 8)
				{
					BtcNick._hist.RemoveAt(BtcNick._hist.Count - 1);
				}
				BtcNick.SaveCfg();
				bool flag10 = flag4;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					BtcNick.Say(BtcNick.T("Nick changed: ") + BtcNick.Plain(text) + (BtcNick._save ? BtcNick.T(" (saved)") : BtcNick.T(" (this run only)")));
				}
				else
				{
					BtcNick.Say(BtcNick.T("Nick saved. It will be used when the other mode is active."));
				}
			}
			catch (Exception ex)
			{
				BtcNick.Say(BtcNick.T("Error: ") + ex.Message);
			}
		}
	}

	private static void Say(string m)
	{
		BtcNick._msg = m;
		BtcNick._msgUntil = Time.unscaledTime + 4f;
	}

	private static void Styles(int fs)
	{
		bool flag = BtcNick._btn != null && BtcNick._styleFs == fs;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcNick._styleFs = fs;
			BtcNick._px = (BtcNick._px ?? new Texture2D(1, 1));
			BtcNick._px.SetPixel(0, 0, Color.white);
			BtcNick._px.Apply();
			BtcNick._btn = BtcUiStyle.Button(fs);
			BtcNick._field = BtcUiStyle.Field(fs);
			BtcNick._label = new GUIStyle(GUI.skin.label);
			BtcNick._label.fontSize = fs;
			BtcNick._label.alignment = TextAnchor.MiddleLeft;
			BtcNick._label.normal.textColor = Color.white;
			BtcNick._label.richText = true;
			BtcNick._wrap = new GUIStyle(BtcNick._label);
			BtcNick._wrap.wordWrap = true;
			BtcNick._wrap.alignment = TextAnchor.UpperLeft;
			BtcNick._prev = new GUIStyle(BtcNick._wrap);
			BtcNick._area = BtcUiStyle.Area(fs);
			BtcNick._tog = BtcUiStyle.Toggle(fs);
		}
	}

	public static void OnGUI()
	{
		try
		{
			bool flag = SceneManager.GetActiveScene().buildIndex == 2;
			bool flag2 = NetworkServer.active || NetworkClient.active;
			bool flag3 = flag2 && BtcGameplay.InLobby;
			bool flag4 = flag || (flag2 && !flag3);
			bool flag5 = !flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				bool flag8 = !BtcNick._loaded;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					BtcNick.Load();
				}
				BtcAvatar.Tick();
				bool flag11 = BtcNick._nick[0].Length == 0 && BtcNick.Current().Length > 0;
				bool flag12 = flag11;
				bool flag13 = flag12;
				if (flag13)
				{
					BtcNick._nick[0] = BtcNick.Clean(BtcNick.Current(), 999);
				}
				bool flag14 = !BtcNick._init && BtcNick._plain.Length == 0 && BtcNick.SlotText(BtcNick._slot).Length > 0;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					BtcNick._text = BtcNick.SlotText(BtcNick._slot);
					BtcNick.SyncFromText();
				}
				int num = BtcNick.Active();
				bool flag17 = BtcNick._lastActive != num;
				bool flag18 = flag17;
				bool flag19 = flag18;
				if (flag19)
				{
					bool flag20 = BtcNick._lastActive < 0;
					BtcNick._lastActive = num;
					string text = BtcNick.NameFor(num);
					bool flag21 = !flag20 && text.Length > 0;
					bool flag22 = flag21;
					bool flag23 = flag22;
					if (flag23)
					{
						BtcNick.SetName(text);
					}
				}
				string text2 = BtcNick._save ? BtcNick.NameFor(num) : string.Empty;
				bool flag24 = !flag2 && text2.Length > 0 && Time.unscaledTime < BtcNick._enforceUntil && Neo.multiplayerName != text2;
				bool flag25 = flag24;
				bool flag26 = flag25;
				bool flag27 = flag26;
				if (flag27)
				{
					BtcNick.SetName(text2);
				}
				BtcNick.AnimTick(flag3);
			}
		}
		catch (Exception)
		{
		}
	}

	static BtcNick()
	{
		BtcNick.PName = new string[]
		{
			"Rainbow",
			"Fire",
			"Lava",
			"Ocean",
			"Sunset",
			"Aurora",
			"Candy",
			"Pastel",
			"Neon",
			"Gold",
			"Silver",
			"Ice",
			"Forest",
			"Toxic",
			"Vampire",
			"Cotton",
			"Galaxy",
			"Sakura",
			"Cyber",
			"Autumn",
			"Mint",
			"Royal",
			"Peach",
			"Berry"
		};
		BtcNick.PHex = new string[]
		{
			"ff0000,ff9900,ffee00,33ff33,00ddff,3355ff,cc33ff",
			"ffee00,ff8800,ff2200,990000",
			"ff0000,ff6600,ffcc00",
			"00ffee,0088ff,0022aa",
			"ffcc00,ff5500,ff0066,7700cc",
			"00ff87,00b7ff,8a2be2",
			"ff77ff,77ffff,ffff77",
			"ffb3ba,ffdfba,ffffba,baffc9,bae1ff",
			"39ff14,00ffff,ff00ff",
			"fff6a0,ffd700,b8860b",
			"ffffff,c0c0c0,808080",
			"ffffff,99e6ff,3399ff",
			"b6ff6a,2e8b57,0b5d1e",
			"ccff00,00ff66",
			"ff5050,aa0000",
			"ff9ad5,ffffff,9ae0ff",
			"00d4ff,6a00ff,ff00aa",
			"ffe0ea,ff8fb1,ff4d88",
			"ff00ff,00ffff",
			"ffcc33,ff6600,993300",
			"b8ffdd,33cc99",
			"7b68ee,ffd700",
			"ffe5b4,ff9e7a,ff5e78",
			"ff4d9a,9b30ff,3d2cff"
		};
		BtcNick.PLoop = new bool[]
		{
			true,
			false,
			false,
			false,
			false,
			false,
			true,
			true,
			true,
			false,
			false,
			false,
			false,
			false,
			false,
			false,
			false,
			false,
			true,
			false,
			false,
			false,
			false,
			false
		};
		BtcNick.Palette = new string[]
		{
			"ff0000",
			"ff6600",
			"ffaa00",
			"ffee00",
			"aaff00",
			"33ff33",
			"00ff99",
			"00ffee",
			"00aaff",
			"3366ff",
			"7733ff",
			"cc33ff",
			"ff33cc",
			"ff3377",
			"ffffff",
			"cccccc",
			"888888",
			"444444",
			"8b4513",
			"ffb6c1",
			"90ee90",
			"87cefa",
			"ffd700",
			"000000"
		};
		BtcNick._text = string.Empty;
		BtcNick._plain = string.Empty;
		BtcNick._cols = new int[0];
		BtcNick._hist = new List<string>();
		BtcNick._msg = string.Empty;
		BtcNick._pr = 1f;
		BtcNick._pg = 0.2f;
		BtcNick._pb = 0.2f;
		BtcNick._hex = "FF3333";
		BtcNick._lastPick = -1;
		BtcNick._cust = new List<int>();
		BtcNick._spread = 1f;
		BtcNick._aSpeed = 0.3f;
		BtcNick._aRate = 5f;
		BtcNick._animCur = string.Empty;
		BtcNick._animK = 1;
		BtcNick._animKFor = -1;
		BtcNick._animCols = new int[0];
	}

	private static void SetName(string n)
	{
		Neo.multiplayerName = n;
		UNetManager instance = UNetManager.instance;
		bool flag = instance != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			instance.LocalPlayerName = n;
			bool flag5 = NetworkClient.active && instance.LocalLobbyPlayer != null;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				instance.LocalLobbyPlayer.CallCmdSetPlayerName(n);
			}
		}
		BtcNick._animCur = string.Empty;
	}

	private static string T(string en)
	{
		return BtcLang.Tr(en);
	}

	private static float F(string s, float d)
	{
		float num;
		return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out num) ? num : d;
	}

	private static string FS(float f)
	{
		return f.ToString("0.###", CultureInfo.InvariantCulture);
	}

	private static bool TryHex(string s, out int c)
	{
		c = 0;
		bool flag = s.StartsWith("#");
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			s = s.Substring(1);
		}
		bool flag5 = s.Length == 3;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			s = new string(new char[]
			{
				s[0],
				s[0],
				s[1],
				s[1],
				s[2],
				s[2]
			});
		}
		bool flag9 = s.Length == 8;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		if (flag12)
		{
			s = s.Substring(0, 6);
		}
		return s.Length == 6 && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out c);
	}

	private static bool TryColor(string v, out int c)
	{
		c = 0;
		v = v.Trim().Trim(new char[]
		{
			'"'
		});
		bool flag = v.StartsWith("#");
		bool result;
		if (flag)
		{
			result = BtcNick.TryHex(v, out c);
		}
		else
		{
			string text = v;
			uint num = BtcStr.ComputeStringHash(text);
			if (num <= 1169454059U)
			{
				if (num <= 132336572U)
				{
					if (num != 18738364U)
					{
						if (num != 96429129U)
						{
							if (num == 132336572U)
							{
								if (text == "lime")
								{
									c = 65280;
									return true;
								}
							}
						}
						else if (text == "yellow")
						{
							c = 16771844;
							return true;
						}
					}
					else if (text == "green")
					{
						c = 32768;
						return true;
					}
				}
				else if (num <= 1089765596U)
				{
					if (num != 817772335U)
					{
						if (num == 1089765596U)
						{
							if (text == "red")
							{
								c = 16711680;
								return true;
							}
						}
					}
					else if (text == "brown")
					{
						c = 10824234;
						return true;
					}
				}
				else if (num != 1110921109U)
				{
					if (num == 1169454059U)
					{
						if (text == "orange")
						{
							c = 16753920;
							return true;
						}
					}
				}
				else if (text == "aqua")
				{
					c = 65535;
					return true;
				}
			}
			else if (num <= 2197550541U)
			{
				if (num <= 1452231588U)
				{
					if (num != 1231115066U)
					{
						if (num == 1452231588U)
						{
							if (text == "black")
							{
								c = 0;
								return true;
							}
						}
					}
					else if (text == "cyan")
					{
						c = 65535;
						return true;
					}
				}
				else if (num != 1676028392U)
				{
					if (num == 2197550541U)
					{
						if (text == "blue")
						{
							c = 255;
							return true;
						}
					}
				}
				else if (text == "magenta")
				{
					c = 16711935;
					return true;
				}
			}
			else if (num <= 2995788198U)
			{
				if (num != 2590900991U)
				{
					if (num == 2995788198U)
					{
						if (text == "grey")
						{
							c = 8421504;
							return true;
						}
					}
				}
				else if (text == "purple")
				{
					c = 8388736;
					return true;
				}
			}
			else if (num != 3042244896U)
			{
				if (num == 3724674918U)
				{
					if (text == "white")
					{
						c = 16777215;
						return true;
					}
				}
			}
			else if (text == "silver")
			{
				c = 12632256;
				return true;
			}
			result = false;
		}
		return result;
	}

	private static void Parse(string raw, out string plain, out int[] cols)
	{
		StringBuilder stringBuilder = new StringBuilder();
		List<int> list = new List<int>();
		int item = -1;
		int i = 0;
		bool flag = raw == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			raw = string.Empty;
		}
		while (i < raw.Length)
		{
			char c = raw[i];
			bool flag5 = c == '<';
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				int num = raw.IndexOf('>', i);
				bool flag9 = num > i && num - i <= 24;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					string text = raw.Substring(i + 1, num - i - 1).ToLowerInvariant();
					int num2 = 0;
					bool flag13 = text.StartsWith("color=") && BtcNick.TryColor(text.Substring(6), out num2);
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						item = num2;
						i = num + 1;
						continue;
					}
					bool flag17 = text == "/color";
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					if (flag20)
					{
						item = -1;
						i = num + 1;
						continue;
					}
				}
			}
			i++;
			bool flag21 = char.IsControl(c);
			bool flag22 = !flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				stringBuilder.Append(c);
				list.Add(item);
			}
		}
		int num3 = 0;
		int num4 = stringBuilder.Length;
		while (num3 < num4 && char.IsWhiteSpace(stringBuilder[num3]))
		{
			num3++;
		}
		while (num4 > num3 && char.IsWhiteSpace(stringBuilder[num4 - 1]))
		{
			num4--;
		}
		plain = stringBuilder.ToString(num3, num4 - num3);
		cols = new int[plain.Length];
		for (int j = 0; j < cols.Length; j++)
		{
			cols[j] = list[num3 + j];
		}
	}

	public static string Plain(string v)
	{
		string result;
		int[] array;
		BtcNick.Parse(v, out result, out array);
		return result;
	}

	private static void SyncFromText()
	{
		BtcNick.Parse(BtcNick._text, out BtcNick._plain, out BtcNick._cols);
		BtcNick.ClampRange();
		BtcNick._animKFor = -1;
	}

	private static void SyncToText()
	{
		BtcNick._text = BtcNick.Compose(BtcNick._plain, BtcNick._cols, BtcNick.Lim);
		BtcNick._animKFor = -1;
	}

	private static void ClampRange()
	{
		int max = Mathf.Max(0, BtcNick._plain.Length - 1);
		BtcNick._from = Mathf.Clamp(BtcNick._from, 0, max);
		BtcNick._to = Mathf.Clamp(BtcNick._to, 0, max);
		bool flag = BtcNick._to < BtcNick._from;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcNick._to = BtcNick._from;
		}
	}

	private static int[][] PStops()
	{
		bool flag = BtcNick._pStops == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcNick._pStops = new int[BtcNick.PHex.Length][];
			for (int i = 0; i < BtcNick.PHex.Length; i++)
			{
				string[] array = BtcNick.PHex[i].Split(new char[]
				{
					','
				});
				int[] array2 = new int[array.Length];
				for (int j = 0; j < array.Length; j++)
				{
					BtcNick.TryHex(array[j], out array2[j]);
				}
				BtcNick._pStops[i] = array2;
			}
		}
		return BtcNick._pStops;
	}

	private static void CurStops()
	{
		bool flag = BtcNick._gradSel >= 0 && BtcNick._gradSel < BtcNick.PName.Length;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcNick._stops = BtcNick.PStops()[BtcNick._gradSel];
			BtcNick._loop = BtcNick.PLoop[BtcNick._gradSel];
		}
		else
		{
			bool flag5 = BtcNick._custArr == null;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				bool flag9 = BtcNick._cust.Count >= 2;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					BtcNick._custArr = BtcNick._cust.ToArray();
				}
				else
				{
					bool flag13 = BtcNick._cust.Count == 1;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						BtcNick._custArr = new int[]
						{
							BtcNick._cust[0],
							BtcNick._cust[0]
						};
					}
					else
					{
						BtcNick._custArr = new int[]
						{
							16711680,
							255
						};
					}
				}
			}
			BtcNick._stops = BtcNick._custArr;
			BtcNick._loop = false;
		}
	}

	private static int Mix(int a, int b, float t)
	{
		int num = Mathf.RoundToInt(Mathf.Lerp((float)(a >> 16 & 255), (float)(b >> 16 & 255), t));
		int num2 = Mathf.RoundToInt(Mathf.Lerp((float)(a >> 8 & 255), (float)(b >> 8 & 255), t));
		int num3 = Mathf.RoundToInt(Mathf.Lerp((float)(a & 255), (float)(b & 255), t));
		return num << 16 | num2 << 8 | num3;
	}

	private static int Lin(int[] st, float t)
	{
		bool flag = st.Length == 1;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		int result;
		if (flag4)
		{
			result = st[0];
		}
		else
		{
			t = Mathf.Clamp01(t);
			float num = t * (float)(st.Length - 1);
			int num2 = Mathf.Min((int)num, st.Length - 2);
			result = BtcNick.Mix(st[num2], st[num2 + 1], num - (float)num2);
		}
		return result;
	}

	private static int LoopSample(int[] st, float u)
	{
		u -= Mathf.Floor(u);
		float num = u * (float)st.Length;
		int num2 = (int)num;
		bool flag = num2 >= st.Length;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			num2 = st.Length - 1;
		}
		return BtcNick.Mix(st[num2], st[(num2 + 1) % st.Length], num - (float)num2);
	}

	private static int Sample(float t)
	{
		bool rev = BtcNick._rev;
		bool flag = rev;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			t = 1f - t;
		}
		float num = t * BtcNick._spread;
		bool flag4 = BtcNick._spread <= 1.001f;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		int result;
		if (flag7)
		{
			result = BtcNick.Lin(BtcNick._stops, num);
		}
		else
		{
			result = (BtcNick._loop ? BtcNick.LoopSample(BtcNick._stops, num) : BtcNick.Lin(BtcNick._stops, Mathf.PingPong(num, 1f)));
		}
		return result;
	}

	private static int SampleAnim(float p)
	{
		return BtcNick._loop ? BtcNick.LoopSample(BtcNick._stops, p) : BtcNick.Lin(BtcNick._stops, Mathf.PingPong(p * 2f, 1f));
	}

	private static void ApplyGradient(int from, int to)
	{
		bool flag = BtcNick._plain.Length == 0;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcNick.CurStops();
			from = Mathf.Clamp(from, 0, BtcNick._plain.Length - 1);
			to = Mathf.Clamp(to, from, BtcNick._plain.Length - 1);
			int num = to - from + 1;
			int[] cols = (int[])BtcNick._cols.Clone();
			for (int i = 1; i <= num; i++)
			{
				int num2 = (num + i - 1) / i;
				for (int j = 0; j < num; j++)
				{
					int num3 = j / i;
					float t = (num2 <= 1) ? 0f : ((float)num3 / (float)(num2 - 1));
					BtcNick._cols[from + j] = BtcNick.Sample(t);
				}
				string text = BtcNick.Compose(BtcNick._plain, BtcNick._cols, BtcNick.Lim);
				bool flag5 = BtcNick._used >= BtcNick._plain.Length;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					BtcNick._text = text;
					BtcNick._pend = true;
					BtcNick._animKFor = -1;
					bool flag9 = i > 1;
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					if (flag12)
					{
						BtcNick.Say(BtcNick.T("Gradient was coarsened to fit the length limit."));
					}
					return;
				}
			}
			BtcNick._cols = cols;
			BtcNick.Say(BtcNick.T("Does not fit the length limit."));
		}
	}

	private static void PaintRange(int from, int to, int color)
	{
		bool flag = BtcNick._plain.Length == 0;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			from = Mathf.Clamp(from, 0, BtcNick._plain.Length - 1);
			to = Mathf.Clamp(to, from, BtcNick._plain.Length - 1);
			int[] cols = (int[])BtcNick._cols.Clone();
			for (int i = from; i <= to; i++)
			{
				BtcNick._cols[i] = color;
			}
			string text = BtcNick.Compose(BtcNick._plain, BtcNick._cols, BtcNick.Lim);
			bool flag5 = BtcNick._used < BtcNick._plain.Length;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcNick._cols = cols;
				BtcNick.Say(BtcNick.T("Does not fit the length limit."));
			}
			else
			{
				BtcNick._text = text;
				BtcNick._pend = true;
				BtcNick._animKFor = -1;
			}
		}
	}

	private static void RecalcK()
	{
		int num = 0;
		for (int i = 0; i < BtcNick._aPlain.Length; i++)
		{
			bool flag = !char.IsWhiteSpace(BtcNick._aPlain[i]);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				num++;
			}
		}
		int num2 = 1;
		while (num2 < 64 && BtcNick._aPlain.Length + (num + num2 - 1) / num2 * 23 > 999)
		{
			num2++;
		}
		BtcNick._animK = num2;
		BtcNick._animKFor = BtcNick._aPlain.Length;
		bool flag5 = BtcNick._animCols.Length != BtcNick._aPlain.Length;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcNick._animCols = new int[BtcNick._aPlain.Length];
		}
	}

	private static float EffRate()
	{
		float num = (float)Mathf.Max(60, (BtcNick._animCur.Length > 0) ? BtcNick._animCur.Length : BtcNick._nick[0].Length);
		return Mathf.Max(0.5f, Mathf.Min(BtcNick._aRate, 5000f / num));
	}

	private static string AnimFrame()
	{
		bool flag = BtcNick._aPlain.Length == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		string result;
		if (flag4)
		{
			result = string.Empty;
		}
		else
		{
			bool flag5 = BtcNick._animKFor != BtcNick._aPlain.Length;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcNick.RecalcK();
			}
			BtcNick.CurStops();
			int length = BtcNick._aPlain.Length;
			bool flag9 = BtcFx.Mode != 0;
			if (flag9)
			{
				BtcFx.Frame(length, BtcNick._animK, BtcNick._phase, BtcNick._spread, BtcNick._animCols);
				return BtcNick.Compose(BtcNick._aPlain, BtcNick._animCols, 999);
			}
			int num = (length + BtcNick._animK - 1) / BtcNick._animK;
			for (int i = 0; i < length; i++)
			{
				int num2 = i / BtcNick._animK;
				float num3 = (num <= 1) ? 0f : ((float)num2 / (float)num * BtcNick._spread);
				bool rev = BtcNick._rev;
				bool flag10 = rev;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					num3 = -num3;
				}
				float num4 = num3 + BtcNick._phase;
				num4 -= Mathf.Floor(num4);
				BtcNick._animCols[i] = BtcNick.SampleAnim(num4);
			}
			result = BtcNick.Compose(BtcNick._aPlain, BtcNick._animCols, 999);
		}
		return result;
	}

	private static void AnimTick(bool inLobby)
	{
		BtcPlate.Tick();
		bool flag = BtcNick._aSrc != BtcNick._nick[0];
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcNick._aSrc = BtcNick._nick[0];
			int[] @base;
			BtcNick.Parse(BtcNick._aSrc, out BtcNick._aPlain, out @base);
			BtcFx.SetBase(@base);
			BtcNick._animKFor = -1;
		}
		bool flag4 = !BtcNick._styleOn || !BtcNick._anim || !inLobby || BtcNick._aPlain.Length == 0 || BtcNick.Active() != 0;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcNick._lastT = Time.unscaledTime;
		}
		else
		{
			float unscaledTime = Time.unscaledTime;
			float num = Mathf.Min(0.25f, unscaledTime - BtcNick._lastT);
			BtcNick._lastT = unscaledTime;
			BtcNick._phase += num * BtcNick._aSpeed;
			BtcNick._phase -= Mathf.Floor(BtcNick._phase);
			bool flag8 = unscaledTime < BtcNick._animNext;
			bool flag9 = !flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			if (flag11)
			{
				BtcNick._animNext = unscaledTime + 1f / BtcNick.EffRate();
				string text = BtcNick.AnimFrame();
				bool flag12 = text.Length > 0 && text != BtcNick._animCur;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				if (flag15)
				{
					BtcNick._animCur = text;
					BtcNick.PushLobby(text);
				}
			}
		}
	}

	private static void PushLobby(string name)
	{
		UNetManager instance = UNetManager.instance;
		bool flag = instance == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			instance.LocalPlayerName = name;
			bool flag5 = NetworkClient.active && instance.LocalLobbyPlayer != null;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				instance.LocalLobbyPlayer.CallCmdSetPlayerName(name);
			}
		}
	}

	public static int LobbyLimit(int lim)
	{
		return (lim >= 15 && lim <= 40) ? 999 : lim;
	}

	private static void Fill(Rect r, Color c)
	{
		Color color = GUI.color;
		GUI.color = c;
		GUI.DrawTexture(r, BtcNick._px);
		GUI.color = color;
	}

	private static Color ToColor(int c)
	{
		return new Color((float)(c >> 16 & 255) / 255f, (float)(c >> 8 & 255) / 255f, (float)(c & 255) / 255f, 1f);
	}

	private static float SliderRow(float x, float y, float w, float h, string lab, float v, float a, float b, string val)
	{
		GUI.Label(new Rect(x, y, w * 0.34f, h), lab, BtcNick._label);
		float result = GUI.HorizontalSlider(new Rect(x + w * 0.35f, y + h * 0.3f, w * 0.48f, h), v, a, b);
		GUI.Label(new Rect(x + w * 0.85f, y, w * 0.15f, h), val, BtcNick._label);
		return result;
	}

	private static void RangeRows(float x, ref float y, float w, float rh, float gap)
	{
		int length = BtcNick._plain.Length;
		int num = Mathf.Max(1, length - 1);
		bool flag = length > 1;
		GUI.enabled = flag;
		int from = Mathf.RoundToInt(BtcNick.SliderRow(x, y, w, rh, BtcNick.T("From char"), (float)BtcNick._from, 0f, (float)num, (length > 0) ? (BtcNick._from + 1).ToString() : "-"));
		y += rh + gap;
		int to = Mathf.RoundToInt(BtcNick.SliderRow(x, y, w, rh, BtcNick.T("To char"), (float)BtcNick._to, 0f, (float)num, (length > 0) ? (BtcNick._to + 1).ToString() : "-"));
		y += rh + gap;
		GUI.enabled = true;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			BtcNick._from = from;
			BtcNick._to = to;
		}
		BtcNick.ClampRange();
	}

	private static void SetPick(int c)
	{
		BtcNick._pr = (float)(c >> 16 & 255) / 255f;
		BtcNick._pg = (float)(c >> 8 & 255) / 255f;
		BtcNick._pb = (float)(c & 255) / 255f;
		BtcNick._hex = c.ToString("X6");
	}

	private static int PickInt()
	{
		return Mathf.RoundToInt(BtcNick._pr * 255f) << 16 | Mathf.RoundToInt(BtcNick._pg * 255f) << 8 | Mathf.RoundToInt(BtcNick._pb * 255f);
	}

	private static int HsvInt(float h)
	{
		Color color = Color.HSVToRGB(h, 0.85f, 1f);
		return Mathf.RoundToInt(color.r * 255f) << 16 | Mathf.RoundToInt(color.g * 255f) << 8 | Mathf.RoundToInt(color.b * 255f);
	}

	private static string Clean(string v, int lim)
	{
		string plain;
		int[] cols;
		BtcNick.Parse(v, out plain, out cols);
		return BtcNick.Compose(plain, cols, lim);
	}

	private static int Active()
	{
		return BtcBc.BcEnabled ? 0 : 1;
	}

	private static string NameFor(int a)
	{
		bool flag = a == 0 || BtcNick._nick[1].Length > 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		string result;
		if (flag3)
		{
			result = (BtcNick._styleOn || BtcNick._nick[a].Length == 0) ? BtcNick._nick[a] : BtcNick.Plain(BtcNick._nick[a]);
		}
		else
		{
			string text = BtcNick.Plain(BtcNick._nick[0]);
			bool flag4 = text.Length > 30;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				text = text.Substring(0, 30);
			}
			result = text;
		}
		return result;
	}

	private static string SlotText(int n)
	{
		bool flag = BtcNick._nick[n].Length > 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		string result;
		if (flag3)
		{
			result = BtcNick._nick[n];
		}
		else
		{
			result = ((n == 1) ? BtcNick.NameFor(1) : BtcNick.Clean(BtcNick.Current(), 999));
		}
		return result;
	}

	private static void SwitchSlot(int n)
	{
		BtcNick._slot = n;
		BtcNick._text = BtcNick.SlotText(n);
		BtcNick.SyncFromText();
	}

	public static void OnMenuOpen()
	{
		BtcNick._init = false;
	}

	public static bool SameNick(string a, string b)
	{
		return BtcNick.Plain(a) == BtcNick.Plain(b);
	}

	private static bool CanEdit(out bool inLobby)
	{
		bool flag = SceneManager.GetActiveScene().buildIndex == 2;
		bool flag2 = NetworkServer.active || NetworkClient.active;
		inLobby = (flag2 && BtcGameplay.InLobby);
		return !flag && (!flag2 | inLobby);
	}

	public static void Draw(Event ev, float w, float rowH, int fs, Color old)
	{
		try
		{
			bool flag = !BtcNick._loaded;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				BtcNick.Load();
			}
			BtcNick.Styles(fs);
			bool flag5;
			bool flag4 = BtcNick.CanEdit(out flag5);
			bool flag6 = !BtcNick._init;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcNick._init = true;
				BtcNick._text = BtcNick.SlotText(BtcNick._slot);
				BtcNick.SyncFromText();
				BtcNick._focus = false;
			}
			float num = (float)fs / 15f;
			float num2 = 26f * num;
			float num3 = 4f * num;
			float num4 = 6f * num;
			float num5 = num4;
			float num6 = w - num4 * 2f;
			float num7 = num4;
			BtcNick._pend = false;
			bool flag9 = !flag4;
			bool flag10 = flag9;
			bool flag11 = flag10;
			if (flag11)
			{
				GUI.Label(new Rect(num5, num7, num6, num2 * 2f), BtcNick.T("The nick can be changed only in the main menu or in the lobby."), BtcNick._wrap);
				num7 += num2 * 2f + num3;
			}
			string text = (BtcNick._anim && flag5 && BtcNick._slot == 0 && BtcNick.Active() == 0 && BtcNick._animCur.Length > 0) ? BtcNick._animCur : BtcNick._text;
			BtcNick.Fill(new Rect(num5, num7, num6, num2 * 2f + num3), new Color(0.1f, 0.1f, 0.1f, 1f));
			GUI.Label(new Rect(num5 + 3f, num7, num6 - 6f, num2 * 2f + num3), text, BtcNick._prev);
			num7 += num2 * 2f + num3 * 2f;
			bool enabled = GUI.enabled;
			GUI.enabled = flag4;
			bool flag12 = GUI.Button(new Rect(num5, num7, num6 * 0.6f - 2f, num2), BtcNick.T("Apply"), BtcNick._btn);
			GUI.enabled = enabled;
			bool flag13 = GUI.Button(new Rect(num5 + num6 * 0.6f + 2f, num7, num6 * 0.4f - 2f, num2), BtcNick.T("Revert"), BtcNick._btn);
			num7 += num2 + num3;
			bool flag14 = GUI.Toggle(new Rect(num5, num7, num6, num2), BtcNick._auto, " " + BtcNick.T("Auto-apply on color changes"), BtcNick._tog);
			bool flag15 = flag14 != BtcNick._auto;
			bool flag16 = flag15;
			bool flag17 = flag16;
			if (flag17)
			{
				BtcNick._auto = flag14;
				BtcNick.SaveCfg();
			}
			num7 += num2 + num3;
			bool flag18 = BtcNick._msg.Length > 0 && Time.unscaledTime < BtcNick._msgUntil;
			bool flag19 = flag18;
			bool flag20 = flag19;
			if (flag20)
			{
				GUI.Label(new Rect(num5, num7, num6, num2), BtcNick._msg, BtcNick._label);
				num7 += num2 + num3;
			}
			string[] texts = new string[]
			{
				BtcNick.T("Nick"),
				BtcNick.T("Colors"),
				BtcNick.T("Gradients"),
				BtcNick.T("Rainbow"),
				BtcNick.T("Avatar"),
				BtcNick.T("Plate")
			};
			int num8 = GUI.SelectionGrid(new Rect(num5, num7, num6, num2 * 2f + num3), BtcNick._tab, texts, 3, BtcNick._btn);
			bool flag21 = num8 != BtcNick._tab;
			bool flag22 = flag21;
			bool flag23 = flag22;
			if (flag23)
			{
				BtcNick._tab = num8;
			}
			num7 += num2 * 2f + num3 * 3f;
			bool flag24 = BtcNick._tab == 1 || BtcNick._tab == 2;
			bool flag25 = flag24 && BtcNick._anim;
			bool flag26 = flag25;
			bool flag27 = flag26;
			if (flag27)
			{
				GUI.Label(new Rect(num5, num7, num6, num2 * 2f), BtcNick.T("Rainbow is on: in the lobby it overrides these colors. Turn it off in the Rainbow tab."), BtcNick._wrap);
				num7 += num2 * 2f + num3;
			}
			bool flag28 = BtcNick._tab == 3 && BtcNick.Active() != 0;
			bool flag29 = flag28;
			bool flag30 = flag29;
			if (flag30)
			{
				GUI.Label(new Rect(num5, num7, num6, num2 * 2f), BtcNick.T("Rainbow works only with the long nick (BetterConnection on)."), BtcNick._wrap);
				num7 += num2 * 2f + num3;
			}
			bool flag31 = BtcNick._tab == 0;
			bool flag32 = flag31;
			bool flag33 = flag32;
			if (flag33)
			{
				num7 = BtcNick.DrawNickTab(num5, num7, num6, num2, num3, ev, flag4);
			}
			else
			{
				bool flag34 = BtcNick._tab == 1;
				bool flag35 = flag34;
				bool flag36 = flag35;
				if (flag36)
				{
					num7 = BtcNick.DrawColorsTab(num5, num7, num6, num2, num3);
				}
				else
				{
					bool flag37 = BtcNick._tab == 2;
					bool flag38 = flag37;
					bool flag39 = flag38;
					if (flag39)
					{
						num7 = BtcNick.DrawGradTab(num5, num7, num6, num2, num3);
					}
					else
					{
						bool flag40 = BtcNick._tab == 3;
						bool flag41 = flag40;
						bool flag42 = flag41;
						if (flag42)
						{
							num7 = BtcNick.DrawAnimTab(num5, num7, num6, num2, num3, flag5);
						}
						else
						{
							bool flag43 = BtcNick._tab == 4;
							if (flag43)
							{
								float num9 = num2 * 17f;
								BtcAvatar.DrawUI(num5, num7, num6, num9, num2, num3, BtcNick._btn, BtcNick._label, BtcNick._tog, BtcNick._px);
								num7 += num9;
							}
							else
							{
								num7 = BtcPlate.DrawUI(num5, num7, num6, num2, num3, BtcNick._btn, BtcNick._wrap, BtcNick._tog, BtcNick._px);
							}
						}
					}
				}
			}
			bool flag44 = flag13;
			bool flag45 = flag44;
			bool flag46 = flag45;
			if (flag46)
			{
				BtcNick._text = BtcNick.SlotText(BtcNick._slot);
				BtcNick.SyncFromText();
			}
			bool flag47 = flag12 || (BtcNick._pend && BtcNick._auto && flag4);
			bool flag48 = flag47;
			bool flag49 = flag48;
			if (flag49)
			{
				BtcNick.Apply();
			}
			BtcNick.ContentH = num7 + num2;
			GUI.color = old;
		}
		catch (Exception)
		{
		}
	}

	private static string Compose(string plain, int[] cols, int lim)
	{
		StringBuilder stringBuilder = new StringBuilder(plain.Length * 2 + 16);
		int num = -1;
		int num2 = 0;
		for (int i = 0; i < plain.Length; i++)
		{
			char c = plain[i];
			int num3 = cols[i];
			bool flag = !char.IsWhiteSpace(c) && num3 != num;
			int num4 = 1;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				num4 += ((num != -1) ? 8 : 0) + ((num3 == -1) ? 0 : 15);
			}
			int num5 = flag ? num3 : num;
			int num6 = (num5 != -1) ? 8 : 0;
			bool flag6 = stringBuilder.Length + num4 + num6 > lim;
			bool flag7 = flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				break;
			}
			bool flag10 = flag;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			if (flag13)
			{
				bool flag14 = num != -1;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					stringBuilder.Append("</color>");
				}
				bool flag17 = num3 != -1;
				bool flag18 = flag17;
				bool flag19 = flag18;
				if (flag19)
				{
					stringBuilder.Append("<color=#");
					stringBuilder.Append(num3.ToString("X6"));
					stringBuilder.Append('>');
				}
				num = num3;
			}
			stringBuilder.Append(c);
			num2++;
		}
		bool flag20 = num != -1;
		bool flag21 = flag20;
		bool flag22 = flag21;
		bool flag23 = flag22;
		if (flag23)
		{
			stringBuilder.Append("</color>");
		}
		BtcNick._used = num2;
		return stringBuilder.ToString();
	}

	private static float DrawNickTab(float x, float y, float w, float row, float gap, Event ev, bool can)
	{
		bool flag = ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "btcnick";
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcNick._pend = true;
			ev.Use();
		}
		string[] texts = new string[]
		{
			BtcNick.T("Long nick (BetterConnection)"),
			BtcNick.T("Short nick (vanilla lobby)")
		};
		int num = GUI.Toolbar(new Rect(x, y, w, row), BtcNick._slot, texts, BtcNick._btn);
		bool flag5 = num != BtcNick._slot;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcNick.SwitchSlot(num);
		}
		y += row + gap;
		GUI.Label(new Rect(x, y, w, row * 2f), BtcNick.T("In use now: ") + ((BtcNick.Active() == 0) ? BtcNick.T("long") : BtcNick.T("short")) + BtcNick.T(" (chosen by BetterConnection). Short limit is 30 characters including color tags."), BtcNick._wrap);
		y += row * 2f + gap;
		GUI.SetNextControlName("btcnick");
		string text = GUI.TextArea(new Rect(x, y, w, row * 4f + gap * 3f), BtcNick._text ?? string.Empty, BtcNick.Lim, BtcNick._area);
		bool flag8 = text != BtcNick._text;
		bool flag9 = flag8;
		bool flag10 = flag9;
		if (flag10)
		{
			BtcNick._text = text;
			BtcNick.SyncFromText();
		}
		y += row * 4f + gap * 4f;
		GUI.Label(new Rect(x, y, w * 0.6f, row), string.Concat(new object[]
		{
			BtcNick.T("Length: "),
			BtcNick._text.Length,
			"/",
			BtcNick.Lim,
			"   ",
			BtcNick.T("Visible: "),
			BtcNick._plain.Length
		}), BtcNick._label);
		bool flag11 = GUI.Button(new Rect(x + w * 0.62f, y, w * 0.38f, row), BtcNick.T("Strip colors"), BtcNick._btn);
		bool flag12 = flag11;
		bool flag13 = flag12;
		if (flag13)
		{
			for (int i = 0; i < BtcNick._cols.Length; i++)
			{
				BtcNick._cols[i] = -1;
			}
			BtcNick.SyncToText();
			BtcNick._pend = true;
		}
		y += row + gap;
		bool flag14 = GUI.Toggle(new Rect(x, y, w, row), BtcNick._save, " " + BtcNick.T("Keep this nick for good"), BtcNick._tog);
		bool flag15 = flag14 != BtcNick._save;
		bool flag16 = flag15;
		bool flag17 = flag16;
		if (flag17)
		{
			BtcNick._save = flag14;
			BtcNick.SaveCfg();
		}
		y += row + gap;
		float num2 = row * 3f + gap * 2f;
		float num3 = row + 2f;
		BtcNick._histScroll = GUI.BeginScrollView(new Rect(x, y, w, num2), BtcNick._histScroll, new Rect(0f, 0f, w - 18f, (float)BtcNick._hist.Count * num3), false, true);
		for (int j = 0; j < BtcNick._hist.Count; j++)
		{
			bool flag18 = GUI.Button(new Rect(0f, (float)j * num3, w - 18f, row), BtcNick._hist[j], BtcNick._btn);
			bool flag19 = flag18;
			bool flag20 = flag19;
			if (flag20)
			{
				BtcNick._text = BtcNick._hist[j];
				BtcNick.SyncFromText();
			}
		}
		GUI.EndScrollView();
		return y + num2;
	}

	private static float DrawColorsTab(float x, float y, float w, float row, float gap)
	{
		int num = 12;
		float num2 = (w - gap * (float)(num - 1)) / (float)num;
		for (int i = 0; i < BtcNick.Palette.Length; i++)
		{
			int num3 = i % num;
			int num4 = i / num;
			Rect rect = new Rect(x + (float)num3 * (num2 + gap), y + (float)num4 * (row * 0.8f + gap), num2, row * 0.8f);
			int num5;
			BtcNick.TryHex(BtcNick.Palette[i], out num5);
			BtcNick.Fill(rect, BtcNick.ToColor(num5));
			bool flag = GUI.Button(rect, GUIContent.none, GUIStyle.none);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				BtcNick.SetPick(num5);
			}
		}
		y += 2f * (row * 0.8f + gap);
		BtcNick._pr = BtcNick.SliderRow(x, y, w, row * 0.9f, "R", BtcNick._pr, 0f, 1f, Mathf.RoundToInt(BtcNick._pr * 255f).ToString());
		y += row * 0.9f + gap * 0.5f;
		BtcNick._pg = BtcNick.SliderRow(x, y, w, row * 0.9f, "G", BtcNick._pg, 0f, 1f, Mathf.RoundToInt(BtcNick._pg * 255f).ToString());
		y += row * 0.9f + gap * 0.5f;
		BtcNick._pb = BtcNick.SliderRow(x, y, w, row * 0.9f, "B", BtcNick._pb, 0f, 1f, Mathf.RoundToInt(BtcNick._pb * 255f).ToString());
		y += row * 0.9f + gap;
		int num6 = BtcNick.PickInt();
		bool flag5 = num6 != BtcNick._lastPick;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcNick._lastPick = num6;
			BtcNick._hex = num6.ToString("X6");
		}
		BtcNick.Fill(new Rect(x, y, row * 1.6f, row), BtcNick.ToColor(num6));
		GUI.Label(new Rect(x + row * 1.7f, y, row * 0.6f, row), "#", BtcNick._label);
		string text = GUI.TextField(new Rect(x + row * 2.2f, y, w * 0.3f, row), BtcNick._hex, 6, BtcNick._field);
		bool flag9 = text != BtcNick._hex;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		if (flag12)
		{
			BtcNick._hex = text;
			int num7 = 0;
			bool flag13 = text.Length == 6 && BtcNick.TryHex(text, out num7);
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				BtcNick._pr = (float)(num7 >> 16 & 255) / 255f;
				BtcNick._pg = (float)(num7 >> 8 & 255) / 255f;
				BtcNick._pb = (float)(num7 & 255) / 255f;
			}
		}
		y += row + gap;
		BtcNick.RangeRows(x, ref y, w, row * 0.9f, gap * 0.5f);
		float width = w * 0.5f - 2f;
		bool flag17 = GUI.Button(new Rect(x, y, width, row), BtcNick.T("Paint range"), BtcNick._btn);
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		if (flag20)
		{
			BtcNick.PaintRange(BtcNick._from, BtcNick._to, num6);
		}
		bool flag21 = GUI.Button(new Rect(x + w * 0.5f + 2f, y, width, row), BtcNick.T("Paint all"), BtcNick._btn);
		bool flag22 = flag21;
		bool flag23 = flag22;
		bool flag24 = flag23;
		if (flag24)
		{
			BtcNick.PaintRange(0, BtcNick._plain.Length - 1, num6);
		}
		y += row + gap;
		float num8 = w / 3f - 3f;
		bool flag25 = GUI.Button(new Rect(x, y, num8, row), BtcNick.T("Clear range"), BtcNick._btn);
		bool flag26 = flag25;
		bool flag27 = flag26;
		bool flag28 = flag27;
		if (flag28)
		{
			BtcNick.PaintRange(BtcNick._from, BtcNick._to, -1);
		}
		bool flag29 = GUI.Button(new Rect(x + num8 + 4f, y, num8, row), BtcNick.T("Clear all"), BtcNick._btn);
		bool flag30 = flag29;
		bool flag31 = flag30;
		bool flag32 = flag31;
		if (flag32)
		{
			BtcNick.PaintRange(0, BtcNick._plain.Length - 1, -1);
		}
		bool flag33 = GUI.Button(new Rect(x + (num8 + 4f) * 2f, y, num8, row), BtcNick.T("Random"), BtcNick._btn);
		bool flag34 = flag33;
		bool flag35 = flag34;
		bool flag36 = flag35;
		if (flag36)
		{
			int[] cols = (int[])BtcNick._cols.Clone();
			int num9 = BtcNick._from;
			while (num9 <= BtcNick._to && num9 < BtcNick._cols.Length)
			{
				BtcNick._cols[num9] = BtcNick.HsvInt(UnityEngine.Random.value);
				num9++;
			}
			string text2 = BtcNick.Compose(BtcNick._plain, BtcNick._cols, BtcNick.Lim);
			bool flag37 = BtcNick._used < BtcNick._plain.Length;
			bool flag38 = flag37;
			bool flag39 = flag38;
			bool flag40 = flag39;
			if (flag40)
			{
				BtcNick._cols = cols;
				BtcNick.Say(BtcNick.T("Does not fit the length limit."));
			}
			else
			{
				BtcNick._text = text2;
				BtcNick._pend = true;
				BtcNick._animKFor = -1;
			}
		}
		return y + row;
	}

	private static float DrawGradTab(float x, float y, float w, float row, float gap)
	{
		int num = 3;
		float num2 = (w - 18f - gap * (float)(num - 1)) / (float)num;
		int num3 = (BtcNick.PName.Length + num - 1) / num;
		float num4 = row + 2f;
		float num5 = row * 6f + gap * 5f;
		BtcNick._presetScroll = GUI.BeginScrollView(new Rect(x, y, w, num5), BtcNick._presetScroll, new Rect(0f, 0f, w - 18f, (float)num3 * num4), false, true);
		int[][] array = BtcNick.PStops();
		for (int i = 0; i < BtcNick.PName.Length; i++)
		{
			float num6 = (float)(i % num) * (num2 + gap);
			float num7 = (float)(i / num) * num4;
			Rect position = new Rect(num6, num7, num2, row);
			Color color = GUI.color;
			bool flag = i == BtcNick._gradSel;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				GUI.color = new Color(0.6f, 1f, 0.6f, 1f);
			}
			bool flag5 = GUI.Button(position, string.Empty, BtcNick._btn);
			GUI.color = color;
			int num8 = 10;
			float num9 = num2 * 0.42f / (float)num8;
			for (int j = 0; j < num8; j++)
			{
				int c = BtcNick.Lin(array[i], (float)j / (float)(num8 - 1));
				BtcNick.Fill(new Rect(num6 + 4f + (float)j * num9, num7 + 4f, num9 + 0.5f, row - 8f), BtcNick.ToColor(c));
			}
			GUI.Label(new Rect(num6 + num2 * 0.46f, num7, num2 * 0.54f, row), BtcNick.T(BtcNick.PName[i]), BtcNick._label);
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				BtcNick._gradSel = i;
				BtcNick.SaveCfg();
			}
		}
		GUI.EndScrollView();
		y += num5 + gap;
		bool flag10 = GUI.Toggle(new Rect(x, y, w * 0.3f, row), BtcNick._rev, " " + BtcNick.T("Reverse"), BtcNick._tog);
		bool flag11 = flag10 != BtcNick._rev;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		if (flag14)
		{
			BtcNick._rev = flag10;
			BtcNick.SaveCfg();
		}
		float num10 = BtcNick.SliderRow(x + w * 0.31f, y, w * 0.69f, row, BtcNick.T("Repeat"), BtcNick._spread, 1f, 6f, BtcNick._spread.ToString("0.0"));
		bool flag15 = Mathf.Abs(num10 - BtcNick._spread) > 0.001f;
		bool flag16 = flag15;
		bool flag17 = flag16;
		bool flag18 = flag17;
		if (flag18)
		{
			BtcNick._spread = Mathf.Round(num10 * 2f) / 2f;
			BtcNick.SaveCfg();
		}
		y += row + gap;
		float num11 = row * 0.9f;
		for (int k = 0; k < BtcNick._cust.Count; k++)
		{
			Rect rect = new Rect(x + (float)k * (num11 + gap), y + (row - num11) * 0.5f, num11, num11);
			BtcNick.Fill(rect, BtcNick.ToColor(BtcNick._cust[k]));
			bool flag19 = GUI.Button(rect, GUIContent.none, GUIStyle.none);
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			if (flag22)
			{
				BtcNick._cust.RemoveAt(k);
				BtcNick._custArr = null;
				BtcNick.SaveCfg();
				break;
			}
		}
		float num12 = x + (float)Mathf.Max(BtcNick._cust.Count, 0) * (num11 + gap) + gap;
		float num13 = x + w - num12;
		float num14 = num13 * 0.5f - 2f;
		GUI.enabled = (BtcNick._cust.Count < 6);
		bool flag23 = GUI.Button(new Rect(num12, y, num14, row), "+ " + BtcNick.T("Add stop"), BtcNick._btn);
		bool flag24 = flag23;
		bool flag25 = flag24;
		bool flag26 = flag25;
		if (flag26)
		{
			BtcNick._cust.Add(BtcNick.PickInt());
			BtcNick._custArr = null;
			BtcNick.SaveCfg();
		}
		GUI.enabled = true;
		bool flag27 = BtcNick._gradSel < 0;
		Color color2 = GUI.color;
		bool flag28 = flag27;
		bool flag29 = flag28;
		bool flag30 = flag29;
		bool flag31 = flag30;
		if (flag31)
		{
			GUI.color = new Color(0.6f, 1f, 0.6f, 1f);
		}
		bool flag32 = GUI.Button(new Rect(num12 + num14 + 4f, y, num14, row), BtcNick.T("Custom"), BtcNick._btn);
		bool flag33 = flag32;
		bool flag34 = flag33;
		bool flag35 = flag34;
		if (flag35)
		{
			BtcNick._gradSel = -1;
			BtcNick.SaveCfg();
		}
		GUI.color = color2;
		y += row + gap;
		BtcNick.RangeRows(x, ref y, w, row * 0.9f, gap * 0.5f);
		float width = w * 0.5f - 2f;
		bool flag36 = GUI.Button(new Rect(x, y, width, row), BtcNick.T("Gradient: range"), BtcNick._btn);
		bool flag37 = flag36;
		bool flag38 = flag37;
		bool flag39 = flag38;
		if (flag39)
		{
			BtcNick.ApplyGradient(BtcNick._from, BtcNick._to);
		}
		bool flag40 = GUI.Button(new Rect(x + w * 0.5f + 2f, y, width, row), BtcNick.T("Gradient: all"), BtcNick._btn);
		bool flag41 = flag40;
		bool flag42 = flag41;
		bool flag43 = flag42;
		if (flag43)
		{
			BtcNick.ApplyGradient(0, BtcNick._plain.Length - 1);
		}
		return y + row;
	}

	private static float DrawAnimTab(float x, float y, float w, float row, float gap, bool inLobby)
	{
		bool flag = GUI.Toggle(new Rect(x, y, w, row), BtcNick._anim, " " + BtcNick.T("Animate the nick in the lobby"), BtcNick._tog);
		bool flag2 = flag != BtcNick._anim;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			BtcNick._anim = flag;
			BtcNick._animCur = string.Empty;
			BtcNick._animKFor = -1;
			BtcNick.SaveCfg();
			bool flag6 = !BtcNick._anim && inLobby;
			bool flag7 = flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				string text = BtcNick.NameFor(BtcNick.Active());
				bool flag10 = text.Length > 0;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				if (flag13)
				{
					BtcNick.PushLobby(text);
				}
			}
		}
		y += row + gap;
		y = BtcFx.DrawModes(x, y, w, row, gap, BtcNick._btn, BtcNick._wrap);
		BtcNick.CurStops();
		string text2 = (BtcNick._gradSel >= 0 && BtcNick._gradSel < BtcNick.PName.Length) ? BtcNick.T(BtcNick.PName[BtcNick._gradSel]) : BtcNick.T("Custom");
		GUI.Label(new Rect(x, y, w, row), string.Concat(new string[]
		{
			BtcNick.T("Gradient: "),
			text2,
			"  (",
			BtcNick.T("Gradients tab"),
			")"
		}), BtcNick._label);
		y += row + gap;
		float num = BtcNick.SliderRow(x, y, w, row, BtcNick.T("Speed"), BtcNick._aSpeed, 0.05f, 2f, BtcNick._aSpeed.ToString("0.00"));
		bool flag14 = Mathf.Abs(num - BtcNick._aSpeed) > 0.0005f;
		bool flag15 = flag14;
		bool flag16 = flag15;
		bool flag17 = flag16;
		if (flag17)
		{
			BtcNick._aSpeed = num;
			BtcNick.SaveCfg();
		}
		y += row + gap;
		float f = BtcNick.SliderRow(x, y, w, row, BtcNick.T("Updates/sec"), BtcNick._aRate, 1f, 10f, Mathf.RoundToInt(BtcNick._aRate).ToString());
		bool flag18 = Mathf.RoundToInt(f) != Mathf.RoundToInt(BtcNick._aRate);
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		if (flag21)
		{
			BtcNick._aRate = (float)Mathf.RoundToInt(f);
			BtcNick.SaveCfg();
		}
		y += row + gap;
		float num2 = BtcNick.SliderRow(x, y, w, row, BtcNick.T("Repeat"), BtcNick._spread, 1f, 6f, BtcNick._spread.ToString("0.0"));
		bool flag22 = Mathf.Abs(num2 - BtcNick._spread) > 0.001f;
		bool flag23 = flag22;
		bool flag24 = flag23;
		bool flag25 = flag24;
		if (flag25)
		{
			BtcNick._spread = Mathf.Round(num2 * 2f) / 2f;
			BtcNick.SaveCfg();
		}
		y += row + gap;
		GUI.Label(new Rect(x, y, w, row), BtcNick.T("Effective rate: ") + BtcNick.EffRate().ToString("0.0") + "/s", BtcNick._label);
		y += row + gap;
		GUI.Label(new Rect(x, y, w, row * 3f), BtcNick.T("Works only in the lobby, never in the game. The nick is frozen on the last frame when the match starts. Long nicks are updated less often to spare the network."), BtcNick._wrap);
		y += row * 3f + gap;
		BtcNick.Fill(new Rect(x, y, w, row * 2f + gap), new Color(0.1f, 0.1f, 0.1f, 1f));
		string text3 = (BtcNick._anim && BtcNick._animCur.Length > 0) ? BtcNick._animCur : BtcNick._text;
		GUI.Label(new Rect(x + 3f, y, w - 6f, row * 2f + gap), text3, BtcNick._prev);
		return y + row * 2f + gap;
	}

	private static int Lim
	{
		get
		{
			return (BtcNick._slot == 1) ? 30 : 999;
		}
	}

	private const int MaxHistory = 8;

	public const int MaxLen = 999;

	private static bool _loaded;

	private static bool _save;

	private static string _text;

	private static readonly List<string> _hist;

	private static string _msg;

	private static float _msgUntil;

	private static bool _focus;

	private static GUIStyle _btn;

	private static GUIStyle _field;

	private static GUIStyle _label;

	private static Texture2D _px;

	private static int _styleFs;

	private static GUIStyle _tog;

	private static float _enforceUntil;

	private static readonly string[] PName;

	private static readonly string[] PHex;

	private static readonly bool[] PLoop;

	private static readonly string[] Palette;

	private static int[][] _pStops;

	private static int _tab;

	private static string _plain;

	private static int[] _cols;

	private static float _pr;

	private static float _pg;

	private static float _pb;

	private static string _hex;

	private static int _lastPick;

	private static int _from;

	private static int _to;

	private static int _gradSel;

	private static readonly List<int> _cust;

	private static int[] _custArr;

	private static bool _rev;

	private static float _spread;

	private static int[] _stops;

	private static bool _loop;

	private static bool _anim;

	private static float _aSpeed;

	private static float _aRate;

	private static float _phase;

	private static float _animNext;

	private static float _lastT;

	private static string _animCur;

	private static int _animK;

	private static int _animKFor;

	private static int[] _animCols;

	private static Vector2 _histScroll;

	private static Vector2 _presetScroll;

	private static GUIStyle _area;

	private static GUIStyle _prev;

	private static GUIStyle _wrap;

	private static int _used;

	public static bool TabActive;

	public static float ContentH = 600f;

	private static bool _init;

	private static bool _pend;

	private static bool _auto = true;

	private static readonly string[] _nick = new string[]
	{
		string.Empty,
		string.Empty
	};

	private static int _slot;

	private static int _lastActive = -1;

	private static string _aPlain = string.Empty;

	private static string _aSrc;
}
}
