using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Brix.Engine;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
public static class BtcAvatar
{
	private static string Dir
	{
		get
		{
			return Path.Combine(BtcPaths.Root, "Avatars");
		}
	}

	private static string CfgPath
	{
		get
		{
			return BtcPaths.Config("btc_avatar.txt");
		}
	}

	public static Texture For(LobbyPlayer p)
	{
		bool flag = p == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		Texture result;
		if (flag3)
		{
			result = null;
		}
		else
		{
			bool flag4 = p.isLocalPlayer && BtcAvatar._local != null;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				result = BtcAvatar._local.tex;
			}
			else
			{
				BtcAvatar.Rx rx;
				bool flag7 = BtcAvatar._view.TryGetValue(p.netId.Value, out rx) && rx.anim != null;
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					result = rx.anim.tex;
				}
				else
				{
					result = null;
				}
			}
		}
		return result;
	}

	internal static void Bump()
	{
		BtcAvatar._ver++;
	}

	internal static string[] FilesList
	{
		get
		{
			BtcAvatar.EnsureFiles(false);
			return BtcAvatar._files;
		}
	}

	internal static string CurrentFile
	{
		get
		{
			BtcAvatar.LoadCfg();
			return BtcAvatar._file;
		}
	}

	internal static string StatusText
	{
		get
		{
			return BtcAvatar._status;
		}
	}

	internal static string FolderPath
	{
		get
		{
			return BtcAvatar.Dir;
		}
	}

	internal static void Choose(string name)
	{
		BtcAvatar.Select(name);
	}

	internal static void Rescan()
	{
		BtcAvatar.EnsureFiles(true);
	}

	internal static void UiSeen()
	{
		BtcAvatar._uiSeen = Time.unscaledTime;
		BtcAvatar.LoadCfg();
		BtcAvatar.EnsureFiles(false);
	}

	internal static Texture2D LocalTex
	{
		get
		{
			return (BtcAvatar._local != null) ? BtcAvatar._local.tex : null;
		}
	}

	public static void Tick()
	{
		bool flag = Time.frameCount == BtcAvatar._lastFrame;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcAvatar._lastFrame = Time.frameCount;
			try
			{
				BtcAvatar.LoadCfg();
				bool pendReady = BtcAvatar._pendReady;
				bool flag4 = pendReady;
				bool flag5 = flag4;
				if (flag5)
				{
					BtcAvatar.FinishLocal();
				}
				bool flag6 = NetworkServer.active || NetworkClient.active;
				bool flag7 = !flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					bool hadNet = BtcAvatar._hadNet;
					bool flag10 = hadNet;
					bool flag11 = flag10;
					if (flag11)
					{
						BtcAvatar.ResetNet();
					}
					BtcAvatar.AnimateOne(BtcAvatar._local, Time.unscaledTime - BtcAvatar._uiSeen < 0.5f);
				}
				else
				{
					BtcAvatar._hadNet = true;
					UNetManager instance = UNetManager.instance;
					bool flag12 = instance == null;
					bool flag13 = !flag12;
					bool flag14 = flag13;
					if (flag14)
					{
						BtcAvatar.Register();
						LobbyPlayer localLobbyPlayer = instance.LocalLobbyPlayer;
						bool active = NetworkServer.active;
						bool flag15 = active;
						bool flag16 = flag15;
						if (flag16)
						{
							BtcAvatar.ServerTick(localLobbyPlayer);
						}
						else
						{
							BtcAvatar.ClientTick(localLobbyPlayer);
						}
						BtcAvatar.Pump();
						bool flag17 = Time.unscaledTime >= BtcAvatar._pruneAt;
						bool flag18 = flag17;
						bool flag19 = flag18;
						if (flag19)
						{
							BtcAvatar._pruneAt = Time.unscaledTime + 2f;
							BtcAvatar.Prune(instance);
						}
						bool flag20 = BtcGameplay.InLobby || Time.unscaledTime - BtcAvatar._uiSeen < 0.5f;
						BtcAvatar.AnimateOne(BtcAvatar._local, flag20);
						bool flag21 = flag20;
						bool flag22 = flag21;
						bool flag23 = flag22;
						if (flag23)
						{
							foreach (KeyValuePair<uint, BtcAvatar.Rx> keyValuePair in BtcAvatar._view)
							{
								BtcAvatar.AnimateOne(keyValuePair.Value.anim, true);
							}
						}
					}
				}
			}
			catch (Exception)
			{
			}
		}
	}

	private static void LoadCfg()
	{
		bool cfgLoaded = BtcAvatar._cfgLoaded;
		bool flag = !cfgLoaded;
		bool flag2 = flag;
		if (flag2)
		{
			BtcAvatar._cfgLoaded = true;
			try
			{
				BtcPaths.Init();
				Directory.CreateDirectory(BtcAvatar.Dir);
				BtcAvatar._file = BtcPaths.ReadKey("btc_avatar.txt", "file", string.Empty);
				bool flag3 = BtcAvatar._file == null;
				bool flag4 = flag3;
				bool flag5 = flag4;
				if (flag5)
				{
					BtcAvatar._file = string.Empty;
				}
			}
			catch (Exception)
			{
			}
			BtcAvatar.StartLoad();
		}
	}

	private static void SaveCfg()
	{
		try
		{
			File.WriteAllText(BtcAvatar.CfgPath, "file=" + BtcAvatar._file + "\r\n");
		}
		catch (Exception)
		{
		}
	}

	private static void Select(string name)
	{
		BtcAvatar._file = (name ?? string.Empty);
		BtcAvatar.SaveCfg();
		BtcAvatar.StartLoad();
	}

	private static void StartLoad()
	{
		BtcAvatar._loadToken++;
		BtcAvatar._pendReady = false;
		bool flag = string.IsNullOrEmpty(BtcAvatar._file);
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcAvatar.SetLocal(null, null, null);
			BtcAvatar._status = BtcLang.Tr("No custom avatar.");
		}
		else
		{
			string path = Path.Combine(BtcAvatar.Dir, BtcAvatar._file);
			bool flag4 = !File.Exists(path);
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				BtcAvatar.SetLocal(null, null, null);
				BtcAvatar._status = BtcLang.Tr("File not found: ") + BtcAvatar._file;
			}
			else
			{
				string a = Path.GetExtension(path).ToLowerInvariant();
				BtcAvatar._status = BtcLang.Tr("Loading...");
				bool flag7 = a == ".gif";
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					int token = BtcAvatar._loadToken;
					string name = BtcAvatar._file;
					ThreadPool.QueueUserWorkItem(delegate(object o)
					{
						List<Color32[]> list3 = null;
						List<int> list4 = null;
						byte[] pendPacked = null;
						string pendErr = null;
						try
						{
							FileInfo fileInfo2 = new FileInfo(path);
							bool flag16 = fileInfo2.Length > 41943040L;
							bool flag17 = flag16;
							bool flag18 = flag17;
							if (flag18)
							{
								throw new Exception("file too big");
							}
							byte[] d = File.ReadAllBytes(path);
							bool flag19 = !BtcAvatar.DecodeGif(d, out list3, out list4);
							bool flag20 = flag19;
							bool flag21 = flag20;
							if (flag21)
							{
								throw new Exception("bad gif");
							}
							pendPacked = BtcAvatar.Pack(list3, list4);
						}
						catch (Exception ex2)
						{
							pendErr = ex2.Message;
						}
						bool flag22 = token != BtcAvatar._loadToken;
						bool flag23 = !flag22;
						bool flag24 = flag23;
						if (flag24)
						{
							BtcAvatar._pendFrames = list3;
							BtcAvatar._pendDelays = list4;
							BtcAvatar._pendPacked = pendPacked;
							BtcAvatar._pendErr = pendErr;
							BtcAvatar._pendName = name;
							BtcAvatar._pendReady = true;
						}
					});
				}
				else
				{
					try
					{
						FileInfo fileInfo = new FileInfo(path);
						bool flag10 = fileInfo.Length > 41943040L;
						bool flag11 = flag10;
						bool flag12 = flag11;
						if (flag12)
						{
							throw new Exception("file too big");
						}
						byte[] data = File.ReadAllBytes(path);
						Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, false);
						bool flag13 = !texture2D.LoadImage(data) || (long)texture2D.width * (long)texture2D.height > 16000000L;
						bool flag14 = flag13;
						bool flag15 = flag14;
						if (flag15)
						{
							UnityEngine.Object.Destroy(texture2D);
							throw new Exception("bad image");
						}
						Color32[] pixels = texture2D.GetPixels32();
						int width = texture2D.width;
						int height = texture2D.height;
						UnityEngine.Object.Destroy(texture2D);
						List<Color32[]> list = new List<Color32[]>();
						list.Add(BtcAvatar.Down(pixels, width, height, false));
						List<int> list2 = new List<int>();
						list2.Add(1000);
						BtcAvatar._pendFrames = list;
						BtcAvatar._pendDelays = list2;
						BtcAvatar._pendPacked = BtcAvatar.Pack(list, list2);
						BtcAvatar._pendErr = null;
						BtcAvatar._pendName = BtcAvatar._file;
						BtcAvatar._pendReady = true;
					}
					catch (Exception ex)
					{
						BtcAvatar.SetLocal(null, null, null);
						BtcAvatar._status = BtcLang.Tr("Error: ") + ex.Message;
					}
				}
			}
		}
	}

	private static void FinishLocal()
	{
		BtcAvatar._pendReady = false;
		bool flag = BtcAvatar._pendName != BtcAvatar._file;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			bool flag4 = BtcAvatar._pendErr != null || BtcAvatar._pendFrames == null || BtcAvatar._pendPacked == null;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				BtcAvatar.SetLocal(null, null, null);
				BtcAvatar._status = BtcLang.Tr("Error: ") + (BtcAvatar._pendErr ?? "?");
			}
			else
			{
				BtcAvatar.SetLocal(BtcAvatar._pendFrames, BtcAvatar._pendDelays, BtcAvatar._pendPacked);
				BtcAvatar._status = string.Concat(new object[]
				{
					BtcAvatar._file,
					"\n",
					BtcAvatar._pendFrames.Count,
					" ",
					BtcLang.Tr("frames"),
					", ",
					BtcAvatar._pendPacked.Length / 1024 + 1,
					" KB"
				});
				BtcAvatar._pendFrames = null;
				BtcAvatar._pendDelays = null;
				BtcAvatar._pendPacked = null;
			}
		}
	}

	private static void SetLocal(List<Color32[]> frames, List<int> delays, byte[] packed)
	{
		bool flag = BtcAvatar._local != null && BtcAvatar._local.tex != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			UnityEngine.Object.Destroy(BtcAvatar._local.tex);
		}
		BtcAvatar._local = null;
		BtcAvatar._packed = packed;
		bool flag4 = frames != null;
		bool flag5 = flag4;
		bool flag6 = flag5;
		if (flag6)
		{
			BtcAvatar._local = BtcAvatar.MakeAnim(frames.ToArray(), delays.ToArray());
		}
		BtcAvatar._ver++;
		BtcAvatar.RefreshList();
	}

	private static BtcAvatar.Anim MakeAnim(Color32[][] frames, int[] delays)
	{
		BtcAvatar.Anim anim = new BtcAvatar.Anim();
		anim.frames = frames;
		anim.delayMs = delays;
		anim.tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
		anim.tex.filterMode = FilterMode.Bilinear;
		anim.tex.wrapMode = TextureWrapMode.Clamp;
		anim.tex.SetPixels32(frames[0]);
		anim.tex.Apply(false);
		anim.cur = 0;
		anim.next = Time.unscaledTime + (float)Mathf.Max(20, delays[0]) / 1000f;
		return anim;
	}

	private static void AnimateOne(BtcAvatar.Anim a, bool run)
	{
		bool flag = !run || a == null || a.frames == null || a.frames.Length < 2 || a.tex == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			float unscaledTime = Time.unscaledTime;
			bool flag4 = unscaledTime < a.next;
			bool flag5 = !flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				a.cur = (a.cur + 1) % a.frames.Length;
				a.tex.SetPixels32(a.frames[a.cur]);
				a.tex.Apply(false);
				float num = (float)Mathf.Max(20, a.delayMs[a.cur]) / 1000f;
				a.next = Mathf.Max(a.next + num, unscaledTime);
			}
		}
	}

	private static Color32[] Down(Color32[] s, int w, int h, bool flipY)
	{
		Color32[] array = new Color32[4096];
		int num = Math.Min(w, h);
		int num2 = (w - num) / 2;
		int num3 = (h - num) / 2;
		float num4 = (float)num / 64f;
		for (int i = 0; i < 64; i++)
		{
			int num5 = num3 + (int)((float)i * num4);
			int num6 = num3 + Math.Max((int)((float)(i + 1) * num4), (int)((float)i * num4) + 1);
			int num7 = Math.Max(1, (num6 - num5) / 6);
			int num8 = flipY ? (63 - i) : i;
			for (int j = 0; j < 64; j++)
			{
				int num9 = num2 + (int)((float)j * num4);
				int num10 = num2 + Math.Max((int)((float)(j + 1) * num4), (int)((float)j * num4) + 1);
				int num11 = Math.Max(1, (num10 - num9) / 6);
				long num12 = 0L;
				long num13 = 0L;
				long num14 = 0L;
				long num15 = 0L;
				int num16 = 0;
				int num17 = num5;
				while (num17 < num6 && num17 < h)
				{
					int num18 = num17 * w;
					int num19 = num9;
					while (num19 < num10 && num19 < w)
					{
						Color32 color = s[num18 + num19];
						num12 += (long)(color.r * color.a);
						num13 += (long)(color.g * color.a);
						num14 += (long)(color.b * color.a);
						num15 += (long)((ulong)color.a);
						num16++;
						num19 += num11;
					}
					num17 += num7;
				}
				Color32 color2 = default(Color32);
				bool flag = num16 > 0 && num15 > 0L;
				bool flag2 = flag;
				bool flag3 = flag2;
				if (flag3)
				{
					color2.r = (byte)(num12 / num15);
					color2.g = (byte)(num13 / num15);
					color2.b = (byte)(num14 / num15);
					color2.a = (byte)(num15 / (long)num16);
				}
				array[num8 * 64 + j] = color2;
			}
		}
		return array;
	}

	private static int U16(byte[] d, int p)
	{
		return (int)d[p] | (int)d[p + 1] << 8;
	}

	private static bool DecodeGif(byte[] d, out List<Color32[]> outFrames, out List<int> outDelays)
	{
		outFrames = null;
		outDelays = null;
		bool flag = d.Length < 13 || d[0] != 71 || d[1] != 73 || d[2] != 70;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool result;
		if (flag3)
		{
			result = false;
		}
		else
		{
			int num = BtcAvatar.U16(d, 6);
			int num2 = BtcAvatar.U16(d, 8);
			bool flag4 = num <= 0 || num2 <= 0 || num > 4096 || num2 > 4096 || (long)num * (long)num2 > 16000000L;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				result = false;
			}
			else
			{
				int num3 = (int)d[10];
				int i = 13;
				Color32[] array = null;
				bool flag7 = (num3 & 128) != 0;
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					int n = 1 << (num3 & 7) + 1;
					array = BtcAvatar.ReadPal(d, ref i, n);
				}
				Color32[] array2 = new Color32[num * num2];
				Color32[] array3 = null;
				List<Color32[]> list = new List<Color32[]>();
				List<int> list2 = new List<int>();
				int num4 = 10;
				int num5 = -1;
				int num6 = 0;
				int num7 = 0;
				int x = 0;
				int y = 0;
				int rw = 0;
				int rh = 0;
				while (i < d.Length && list.Count < 600)
				{
					int num8 = (int)d[i++];
					bool flag10 = num8 == 59;
					bool flag11 = flag10;
					bool flag12 = flag11;
					if (flag12)
					{
						break;
					}
					bool flag13 = num8 == 33;
					bool flag14 = flag13;
					bool flag15 = flag14;
					if (flag15)
					{
						int num9 = (int)d[i++];
						int num10 = (int)d[i++];
						bool flag16 = num9 == 249 && num10 >= 4;
						bool flag17 = flag16;
						bool flag18 = flag17;
						if (flag18)
						{
							int num11 = (int)d[i];
							num6 = (num11 >> 2 & 7);
							num4 = BtcAvatar.U16(d, i + 1);
							num5 = (((num11 & 1) != 0) ? ((int)d[i + 3]) : -1);
						}
						for (i += num10; i < d.Length; i += num10)
						{
							num10 = (int)d[i++];
							bool flag19 = num10 == 0;
							bool flag20 = flag19;
							bool flag21 = flag20;
							if (flag21)
							{
								break;
							}
						}
					}
					else
					{
						bool flag22 = num8 != 44;
						bool flag23 = flag22;
						bool flag24 = flag23;
						if (flag24)
						{
							break;
						}
						int num12 = BtcAvatar.U16(d, i);
						int num13 = BtcAvatar.U16(d, i + 2);
						int num14 = BtcAvatar.U16(d, i + 4);
						int num15 = BtcAvatar.U16(d, i + 6);
						int num16 = (int)d[i + 8];
						i += 9;
						Color32[] array4 = array;
						bool flag25 = (num16 & 128) != 0;
						bool flag26 = flag25;
						bool flag27 = flag26;
						if (flag27)
						{
							int n2 = 1 << (num16 & 7) + 1;
							array4 = BtcAvatar.ReadPal(d, ref i, n2);
						}
						bool inter = (num16 & 64) != 0;
						int num17 = (int)d[i++];
						MemoryStream memoryStream = new MemoryStream();
						while (i < d.Length)
						{
							int num18 = (int)d[i++];
							bool flag28 = num18 == 0;
							bool flag29 = flag28;
							bool flag30 = flag29;
							if (flag30)
							{
								break;
							}
							bool flag31 = i + num18 > d.Length;
							bool flag32 = flag31;
							bool flag33 = flag32;
							if (flag33)
							{
								num18 = d.Length - i;
							}
							memoryStream.Write(d, i, num18);
							i += num18;
						}
						bool flag34 = array4 == null || num14 <= 0 || num15 <= 0 || num17 < 1 || num17 > 11;
						bool flag35 = !flag34;
						bool flag36 = flag35;
						if (flag36)
						{
							byte[] array5 = BtcAvatar.Lzw(memoryStream.ToArray(), num17, num14 * num15);
							bool flag37 = num7 == 2;
							bool flag38 = flag37;
							bool flag39 = flag38;
							if (flag39)
							{
								BtcAvatar.ClearRect(array2, num, num2, x, y, rw, rh);
							}
							else
							{
								bool flag40 = num7 == 3 && array3 != null;
								bool flag41 = flag40;
								bool flag42 = flag41;
								if (flag42)
								{
									Array.Copy(array3, array2, array2.Length);
								}
							}
							bool flag43 = num6 == 3;
							bool flag44 = flag43;
							bool flag45 = flag44;
							if (flag45)
							{
								bool flag46 = array3 == null;
								bool flag47 = flag46;
								bool flag48 = flag47;
								if (flag48)
								{
									array3 = new Color32[array2.Length];
								}
								Array.Copy(array2, array3, array2.Length);
							}
							int[] array6 = BtcAvatar.RowOrder(num15, inter);
							for (int j = 0; j < num15; j++)
							{
								int num19 = num13 + array6[j];
								bool flag49 = num19 < 0 || num19 >= num2;
								bool flag50 = !flag49;
								bool flag51 = flag50;
								if (flag51)
								{
									for (int k = 0; k < num14; k++)
									{
										int num20 = num12 + k;
										bool flag52 = num20 < 0 || num20 >= num;
										bool flag53 = !flag52;
										bool flag54 = flag53;
										if (flag54)
										{
											int num21 = (int)array5[j * num14 + k];
											bool flag55 = num21 == num5 || num21 >= array4.Length;
											bool flag56 = !flag55;
											bool flag57 = flag56;
											if (flag57)
											{
												array2[num19 * num + num20] = array4[num21];
											}
										}
									}
								}
							}
							list.Add(BtcAvatar.Down(array2, num, num2, true));
							int num22 = num4 * 10;
							bool flag58 = num22 <= 10;
							bool flag59 = flag58;
							bool flag60 = flag59;
							if (flag60)
							{
								num22 = 100;
							}
							list2.Add(num22);
							num7 = num6;
							x = num12;
							y = num13;
							rw = num14;
							rh = num15;
							num4 = 10;
							num5 = -1;
							num6 = 0;
						}
					}
				}
				bool flag61 = list.Count == 0;
				bool flag62 = flag61;
				bool flag63 = flag62;
				if (flag63)
				{
					result = false;
				}
				else
				{
					bool flag64 = list.Count > 32;
					bool flag65 = flag64;
					bool flag66 = flag65;
					if (flag66)
					{
						List<Color32[]> list3 = new List<Color32[]>();
						List<int> list4 = new List<int>();
						int count = list.Count;
						for (int l = 0; l < 32; l++)
						{
							int num23 = (int)((long)l * (long)count / 32L);
							int num24 = (int)((long)(l + 1) * (long)count / 32L);
							int num25 = 0;
							for (int m = num23; m < num24; m++)
							{
								num25 += list2[m];
							}
							list3.Add(list[num23]);
							list4.Add(Math.Max(20, num25));
						}
						list = list3;
						list2 = list4;
					}
					outFrames = list;
					outDelays = list2;
					result = true;
				}
			}
		}
		return result;
	}

	private static Color32[] ReadPal(byte[] d, ref int pos, int n)
	{
		Color32[] array = new Color32[n];
		for (int i = 0; i < n; i++)
		{
			bool flag = pos + 2 >= d.Length;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				break;
			}
			array[i] = new Color32(d[pos], d[pos + 1], d[pos + 2], byte.MaxValue);
			pos += 3;
		}
		return array;
	}

	private static void ClearRect(Color32[] c, int w, int h, int x, int y, int rw, int rh)
	{
		int num = y;
		while (num < y + rh && num < h)
		{
			bool flag = num < 0;
			bool flag2 = !flag;
			bool flag3 = flag2;
			if (flag3)
			{
				int num2 = x;
				while (num2 < x + rw && num2 < w)
				{
					bool flag4 = num2 >= 0;
					bool flag5 = flag4;
					bool flag6 = flag5;
					if (flag6)
					{
						c[num * w + num2] = default(Color32);
					}
					num2++;
				}
			}
			num++;
		}
	}

	private static int[] RowOrder(int ih, bool inter)
	{
		int[] array = new int[ih];
		bool flag = !inter;
		bool flag2 = flag;
		bool flag3 = flag2;
		int[] result;
		if (flag3)
		{
			for (int i = 0; i < ih; i++)
			{
				array[i] = i;
			}
			result = array;
		}
		else
		{
			int num = 0;
			int[] array2 = new int[]
			{
				0,
				4,
				2,
				1
			};
			int[] array3 = new int[]
			{
				8,
				8,
				4,
				2
			};
			for (int j = 0; j < 4; j++)
			{
				for (int k = array2[j]; k < ih; k += array3[j])
				{
					array[num++] = k;
				}
			}
			result = array;
		}
		return result;
	}

	private static byte[] Lzw(byte[] data, int minCode, int npix)
	{
		int num = 1 << minCode;
		int num2 = num + 1;
		int num3 = num2 + 1;
		int num4 = minCode + 1;
		int num5 = (1 << num4) - 1;
		short[] array = new short[4096];
		byte[] array2 = new byte[4096];
		byte[] array3 = new byte[4097];
		byte[] array4 = new byte[npix];
		for (int i = 0; i < num; i++)
		{
			array2[i] = (byte)i;
		}
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		int j = 0;
		int num9 = -1;
		int num10 = 0;
		int num11 = 0;
		while (j < npix)
		{
			bool flag = num11 == 0;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				bool flag4 = num6 < num4;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					bool flag7 = num8 >= data.Length;
					bool flag8 = flag7;
					bool flag9 = flag8;
					if (flag9)
					{
						break;
					}
					num7 += (int)data[num8++] << num6;
					num6 += 8;
					continue;
				}
				else
				{
					int k = num7 & num5;
					num7 >>= num4;
					num6 -= num4;
					bool flag10 = k > num3 || k == num2;
					bool flag11 = flag10;
					bool flag12 = flag11;
					if (flag12)
					{
						break;
					}
					bool flag13 = k == num;
					bool flag14 = flag13;
					bool flag15 = flag14;
					if (flag15)
					{
						num4 = minCode + 1;
						num5 = (1 << num4) - 1;
						num3 = num2 + 1;
						num9 = -1;
						continue;
					}
					bool flag16 = num9 == -1;
					bool flag17 = flag16;
					bool flag18 = flag17;
					if (flag18)
					{
						array3[num11++] = array2[k];
						num9 = k;
						num10 = k;
						continue;
					}
					int num12 = k;
					bool flag19 = k == num3;
					bool flag20 = flag19;
					bool flag21 = flag20;
					if (flag21)
					{
						array3[num11++] = (byte)num10;
						k = num9;
					}
					while (k >= num)
					{
						array3[num11++] = array2[k];
						k = (int)array[k];
					}
					num10 = (int)array2[k];
					array3[num11++] = (byte)num10;
					bool flag22 = num3 < 4096;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						array[num3] = (short)num9;
						array2[num3] = (byte)num10;
						num3++;
						bool flag25 = (num3 & num5) == 0 && num3 < 4096;
						bool flag26 = flag25;
						bool flag27 = flag26;
						if (flag27)
						{
							num4++;
							num5 = (1 << num4) - 1;
						}
					}
					num9 = num12;
				}
			}
			num11--;
			array4[j++] = array3[num11];
		}
		return array4;
	}

	private static byte[] Pack(List<Color32[]> fr, List<int> dl)
	{
		int count = fr.Count;
		int num = 4096;
		int[] hist = new int[32768];
		for (int i = 0; i < count; i++)
		{
			Color32[] array = fr[i];
			for (int j = 0; j < num; j++)
			{
				bool flag = array[j].a >= 128;
				bool flag2 = flag;
				bool flag3 = flag2;
				if (flag3)
				{
					hist[array[j].r >> 3 << 10 | array[j].g >> 3 << 5 | array[j].b >> 3]++;
				}
			}
		}
		List<int> list = new List<int>();
		for (int k = 0; k < 32768; k++)
		{
			bool flag4 = hist[k] > 0;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				list.Add(k);
			}
		}
		list.Sort((int x, int y) => hist[y].CompareTo(hist[x]));
		int num2 = Math.Min(255, list.Count);
		int[] array2 = new int[num2];
		int[] array3 = new int[num2];
		int[] array4 = new int[num2];
		byte[] array5 = new byte[765];
		for (int l = 0; l < num2; l++)
		{
			int num3 = list[l];
			array2[l] = ((num3 >> 10 & 31) << 3 | 4);
			array3[l] = ((num3 >> 5 & 31) << 3 | 4);
			array4[l] = ((num3 & 31) << 3 | 4);
			array5[l * 3] = (byte)array2[l];
			array5[l * 3 + 1] = (byte)array3[l];
			array5[l * 3 + 2] = (byte)array4[l];
		}
		byte[] array6 = new byte[32768];
		for (int m = 0; m < list.Count; m++)
		{
			int num4 = list[m];
			bool flag7 = m < num2;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				array6[num4] = (byte)(m + 1);
			}
			else
			{
				int num5 = (num4 >> 10 & 31) << 3 | 4;
				int num6 = (num4 >> 5 & 31) << 3 | 4;
				int num7 = (num4 & 31) << 3 | 4;
				int num8 = 0;
				int num9 = int.MaxValue;
				for (int n = 0; n < num2; n++)
				{
					int num10 = num5 - array2[n];
					int num11 = num6 - array3[n];
					int num12 = num7 - array4[n];
					int num13 = num10 * num10 + num11 * num11 + num12 * num12;
					bool flag10 = num13 < num9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					if (flag12)
					{
						num9 = num13;
						num8 = n;
					}
				}
				array6[num4] = (byte)(num8 + 1);
			}
		}
		MemoryStream memoryStream = new MemoryStream();
		memoryStream.WriteByte((byte)count);
		for (int num14 = 0; num14 < count; num14++)
		{
			int num15 = Mathf.Clamp(dl[num14] / 10, 2, 65535);
			memoryStream.WriteByte((byte)(num15 & 255));
			memoryStream.WriteByte((byte)(num15 >> 8));
		}
		memoryStream.Write(array5, 0, 765);
		byte[] array7 = new byte[num];
		byte[] array8 = new byte[num];
		for (int num16 = 0; num16 < count; num16++)
		{
			Color32[] array9 = fr[num16];
			for (int num17 = 0; num17 < num; num17++)
			{
				byte b = 0;
				bool flag13 = array9[num17].a >= 128;
				bool flag14 = flag13;
				bool flag15 = flag14;
				if (flag15)
				{
					b = array6[array9[num17].r >> 3 << 10 | array9[num17].g >> 3 << 5 | array9[num17].b >> 3];
				}
				array8[num17] = b;
				memoryStream.WriteByte((byte)(b ^ array7[num17]));
			}
			byte[] array10 = array7;
			array7 = array8;
			array8 = array10;
		}
		byte[] array11 = memoryStream.ToArray();
		MemoryStream memoryStream2 = new MemoryStream();
		using (DeflateStream deflateStream = new DeflateStream(memoryStream2, CompressionMode.Compress, true))
		{
			deflateStream.Write(array11, 0, array11.Length);
		}
		return memoryStream2.ToArray();
	}

	private static BtcAvatar.Anim Unpack(byte[] z)
	{
		int num = 4096;
		int num2 = 830 + num * 32;
		byte[] array = new byte[num2 + 1];
		int i = 0;
		using (DeflateStream deflateStream = new DeflateStream(new MemoryStream(z), CompressionMode.Decompress))
		{
			while (i < array.Length)
			{
				int num3 = deflateStream.Read(array, i, array.Length - i);
				bool flag = num3 <= 0;
				bool flag2 = flag;
				bool flag3 = flag2;
				if (flag3)
				{
					break;
				}
				i += num3;
			}
		}
		bool flag4 = i < 1 || i > num2;
		bool flag5 = flag4;
		bool flag6 = flag5;
		BtcAvatar.Anim result;
		if (flag6)
		{
			result = null;
		}
		else
		{
			int num4 = (int)array[0];
			bool flag7 = num4 < 1 || num4 > 32 || i != 1 + 2 * num4 + 765 + num * num4;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				result = null;
			}
			else
			{
				int[] array2 = new int[num4];
				for (int j = 0; j < num4; j++)
				{
					array2[j] = ((int)array[1 + j * 2] | (int)array[2 + j * 2] << 8) * 10;
				}
				int num5 = 1 + 2 * num4;
				Color32[] array3 = new Color32[256];
				for (int k = 0; k < 255; k++)
				{
					array3[k + 1] = new Color32(array[num5 + k * 3], array[num5 + k * 3 + 1], array[num5 + k * 3 + 2], byte.MaxValue);
				}
				int num6 = num5 + 765;
				Color32[][] array4 = new Color32[num4][];
				byte[] array5 = new byte[num];
				for (int l = 0; l < num4; l++)
				{
					Color32[] array6 = new Color32[num];
					for (int m = 0; m < num; m++)
					{
						byte b = (byte)(array[num6 + l * num + m] ^ array5[m]);
						array5[m] = b;
						array6[m] = array3[(int)b];
					}
					array4[l] = array6;
				}
				result = BtcAvatar.MakeAnim(array4, array2);
			}
		}
		return result;
	}

	private static void Register()
	{
		bool active = NetworkServer.active;
		bool flag = active;
		bool flag2 = flag;
		if (flag2)
		{
			bool flag3 = !BtcAvatar._srvReg;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				BtcAvatar._srvReg = true;
				NetworkServer.RegisterHandler(92, new NetworkMessageDelegate(BtcAvatar.OnUp));
			}
		}
		else
		{
			BtcAvatar._srvReg = false;
		}
		NetworkClient networkClient = (NetworkClient.allClients.Count > 0) ? NetworkClient.allClients[0] : null;
		bool flag6 = networkClient != BtcAvatar._cl;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcAvatar._cl = networkClient;
			BtcAvatar._annVer = -1;
			bool flag9 = networkClient != null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			if (flag11)
			{
				networkClient.RegisterHandler(91, new NetworkMessageDelegate(BtcAvatar.OnDown));
			}
		}
	}

	private static List<BtcAvatar.AvaMsg> MakeMsgs(byte[] packed, int ver, uint nid)
	{
		List<BtcAvatar.AvaMsg> list = new List<BtcAvatar.AvaMsg>();
		bool flag = packed == null || packed.Length == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		List<BtcAvatar.AvaMsg> result;
		if (flag3)
		{
			list.Add(new BtcAvatar.AvaMsg
			{
				nid = nid,
				ver = ver,
				idx = 0,
				count = 0
			});
			result = list;
		}
		else
		{
			int num = (packed.Length + 1000 - 1) / 1000;
			bool flag4 = num > 300;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				list.Add(new BtcAvatar.AvaMsg
				{
					nid = nid,
					ver = ver
				});
				result = list;
			}
			else
			{
				for (int i = 0; i < num; i++)
				{
					int num2 = i * 1000;
					int num3 = Math.Min(1000, packed.Length - num2);
					byte[] array = new byte[num3];
					Array.Copy(packed, num2, array, 0, num3);
					list.Add(new BtcAvatar.AvaMsg
					{
						nid = nid,
						ver = ver,
						idx = (short)i,
						count = (short)num,
						data = array
					});
				}
				result = list;
			}
		}
		return result;
	}

	private static void ClientTick(LobbyPlayer me)
	{
		bool flag = BtcAvatar._cl == null || !BtcAvatar._cl.isConnected || me == null || BtcAvatar._annVer == BtcAvatar._ver;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcAvatar._annVer = BtcAvatar._ver;
			BtcAvatar._cOut.Clear();
			List<BtcAvatar.AvaMsg> list = BtcAvatar.MakeMsgs(BtcNick.StyleOn ? BtcAvatar._packed : null, BtcAvatar._ver, 0U);
			for (int i = 0; i < list.Count; i++)
			{
				BtcAvatar._cOut.Enqueue(list[i]);
			}
		}
	}

	private static void ServerTick(LobbyPlayer me)
	{
		bool flag = me == null || BtcAvatar._hostVer == BtcAvatar._ver;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcAvatar._hostVer = BtcAvatar._ver;
			uint value = me.netId.Value;
			BtcAvatar.Rx srv = BtcAvatar.GetSrv(value);
			List<BtcAvatar.AvaMsg> list = BtcAvatar.MakeMsgs(BtcNick.StyleOn ? BtcAvatar._packed : null, BtcAvatar._ver, value);
			BtcAvatar.Reset(srv, BtcAvatar._ver, (list.Count == 1 && list[0].count == 0) ? 0 : list.Count);
			for (int i = 0; i < list.Count; i++)
			{
				bool flag4 = list[i].count > 0;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					srv.parts[i] = list[i].data;
					srv.got++;
				}
				for (int j = 0; j < BtcAvatar._mods.Count; j++)
				{
					BtcAvatar._sOut.Enqueue(new KeyValuePair<int, BtcAvatar.AvaMsg>(BtcAvatar._mods[j], list[i]));
				}
			}
		}
	}

	private static BtcAvatar.Rx GetSrv(uint owner)
	{
		BtcAvatar.Rx rx;
		bool flag = !BtcAvatar._srv.TryGetValue(owner, out rx);
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			rx = new BtcAvatar.Rx();
			BtcAvatar._srv[owner] = rx;
		}
		return rx;
	}

	private static void Reset(BtcAvatar.Rx rx, int ver, int count)
	{
		rx.ver = ver;
		rx.count = count;
		rx.got = 0;
		rx.parts = ((count > 0) ? new byte[count][] : null);
	}

	private static bool Accept(BtcAvatar.Rx rx, BtcAvatar.AvaMsg m)
	{
		bool flag = m.count < 0 || m.count > 300 || (m.count > 0 && (m.idx < 0 || m.idx >= m.count));
		bool flag2 = flag;
		bool flag3 = flag2;
		bool result;
		if (flag3)
		{
			result = false;
		}
		else
		{
			bool flag4 = m.data != null && m.data.Length > 1064;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				result = false;
			}
			else
			{
				bool flag7 = rx.ver != m.ver || rx.count != (int)m.count;
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					BtcAvatar.Reset(rx, m.ver, (int)m.count);
				}
				bool flag10 = m.count > 0 && rx.parts[(int)m.idx] == null;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					rx.parts[(int)m.idx] = m.data;
					rx.got++;
				}
				result = true;
			}
		}
		return result;
	}

	private static void BuildView(uint owner, BtcAvatar.Rx src)
	{
		BtcAvatar.Rx rx;
		bool flag = !BtcAvatar._view.TryGetValue(owner, out rx);
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			rx = new BtcAvatar.Rx();
			BtcAvatar._view[owner] = rx;
		}
		bool flag4 = rx.anim != null && rx.anim.tex != null;
		bool flag5 = flag4;
		bool flag6 = flag5;
		if (flag6)
		{
			UnityEngine.Object.Destroy(rx.anim.tex);
		}
		rx.anim = null;
		bool flag7 = src.count <= 0 || src.got < src.count;
		bool flag8 = !flag7;
		bool flag9 = flag8;
		if (flag9)
		{
			MemoryStream memoryStream = new MemoryStream();
			for (int i = 0; i < src.count; i++)
			{
				memoryStream.Write(src.parts[i], 0, src.parts[i].Length);
			}
			try
			{
				rx.anim = BtcAvatar.Unpack(memoryStream.ToArray());
			}
			catch (Exception)
			{
				rx.anim = null;
			}
		}
		BtcAvatar.RefreshList();
	}

	private static uint OwnerOf(int connId)
	{
		UNetManager instance = UNetManager.instance;
		bool flag = instance == null || instance.lobbySlots == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		uint result;
		if (flag3)
		{
			result = 0U;
		}
		else
		{
			for (int i = 0; i < instance.lobbySlots.Length; i++)
			{
				LobbyPlayer lobbyPlayer = instance.lobbySlots[i] as LobbyPlayer;
				bool flag4 = lobbyPlayer != null && lobbyPlayer.connectionToClient != null && lobbyPlayer.connectionToClient.connectionId == connId;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					return lobbyPlayer.netId.Value;
				}
			}
			result = 0U;
		}
		return result;
	}

	private static void OnUp(NetworkMessage netMsg)
	{
		try
		{
			BtcAvatar.AvaMsg avaMsg = netMsg.ReadMessage<BtcAvatar.AvaMsg>();
			int connectionId = netMsg.conn.connectionId;
			uint num = BtcAvatar.OwnerOf(connectionId);
			bool flag = num == 0U;
			bool flag2 = !flag;
			bool flag3 = flag2;
			if (flag3)
			{
				bool flag4 = !BtcAvatar._mods.Contains(connectionId);
				bool flag5 = flag4;
				bool flag6 = flag5;
				bool flag7 = flag6;
				if (flag7)
				{
					BtcAvatar._mods.Add(connectionId);
				}
				BtcAvatar.Rx srv = BtcAvatar.GetSrv(num);
				bool flag8 = BtcAvatar.Accept(srv, avaMsg);
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					BtcAvatar.AvaMsg avaMsg2 = new BtcAvatar.AvaMsg();
					avaMsg2.nid = num;
					avaMsg2.ver = avaMsg.ver;
					avaMsg2.idx = avaMsg.idx;
					avaMsg2.count = avaMsg.count;
					avaMsg2.data = avaMsg.data;
					for (int i = 0; i < BtcAvatar._mods.Count; i++)
					{
						bool flag11 = BtcAvatar._mods[i] != connectionId;
						bool flag12 = flag11;
						bool flag13 = flag12;
						if (flag13)
						{
							BtcAvatar._sOut.Enqueue(new KeyValuePair<int, BtcAvatar.AvaMsg>(BtcAvatar._mods[i], avaMsg2));
						}
					}
					bool flag14 = srv.count == 0 || srv.got >= srv.count;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						BtcAvatar.BuildView(num, srv);
					}
				}
				bool flag17 = flag4;
				bool flag18 = flag17;
				bool flag19 = flag18;
				if (flag19)
				{
					foreach (KeyValuePair<uint, BtcAvatar.Rx> keyValuePair in BtcAvatar._srv)
					{
						bool flag20 = keyValuePair.Key == num;
						bool flag21 = !flag20;
						bool flag22 = flag21;
						if (flag22)
						{
							BtcAvatar.Rx value = keyValuePair.Value;
							bool flag23 = value.count <= 0 || value.got < value.count;
							bool flag24 = !flag23;
							bool flag25 = flag24;
							if (flag25)
							{
								for (int j = 0; j < value.count; j++)
								{
									BtcAvatar.AvaMsg avaMsg3 = new BtcAvatar.AvaMsg();
									avaMsg3.nid = keyValuePair.Key;
									avaMsg3.ver = value.ver;
									avaMsg3.idx = (short)j;
									avaMsg3.count = (short)value.count;
									avaMsg3.data = value.parts[j];
									BtcAvatar._sOut.Enqueue(new KeyValuePair<int, BtcAvatar.AvaMsg>(connectionId, avaMsg3));
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

	private static void OnDown(NetworkMessage netMsg)
	{
		try
		{
			bool active = NetworkServer.active;
			bool flag = !active;
			bool flag2 = flag;
			if (flag2)
			{
				BtcAvatar.AvaMsg avaMsg = netMsg.ReadMessage<BtcAvatar.AvaMsg>();
				BtcAvatar.Rx rx;
				bool flag3 = !BtcAvatar._view.TryGetValue(avaMsg.nid, out rx);
				bool flag4 = flag3;
				bool flag5 = flag4;
				if (flag5)
				{
					rx = new BtcAvatar.Rx();
					BtcAvatar._view[avaMsg.nid] = rx;
				}
				bool flag6 = BtcAvatar.Accept(rx, avaMsg) && (rx.count == 0 || rx.got >= rx.count);
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					BtcAvatar.BuildView(avaMsg.nid, rx);
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void Pump()
	{
		int num = 0;
		while (BtcAvatar._sOut.Count > 0 && num < 6)
		{
			KeyValuePair<int, BtcAvatar.AvaMsg> keyValuePair = BtcAvatar._sOut.Dequeue();
			num++;
			try
			{
				NetworkServer.SendToClient(keyValuePair.Key, 91, keyValuePair.Value);
			}
			catch (Exception)
			{
			}
		}
		num = 0;
		while (BtcAvatar._cOut.Count > 0 && num < 6 && BtcAvatar._cl != null && BtcAvatar._cl.isConnected)
		{
			BtcAvatar.AvaMsg msg = BtcAvatar._cOut.Dequeue();
			num++;
			try
			{
				BtcAvatar._cl.Send(92, msg);
			}
			catch (Exception)
			{
			}
		}
	}

	private static void Prune(UNetManager um)
	{
		HashSet<uint> hashSet = new HashSet<uint>();
		bool flag = um.lobbySlots != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			for (int i = 0; i < um.lobbySlots.Length; i++)
			{
				LobbyPlayer lobbyPlayer = um.lobbySlots[i] as LobbyPlayer;
				bool flag4 = lobbyPlayer != null;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					hashSet.Add(lobbyPlayer.netId.Value);
				}
			}
		}
		HashSet<uint> hashSet2 = new HashSet<uint>();
		foreach (uint item in BtcAvatar._view.Keys)
		{
			bool flag7 = !hashSet.Contains(item);
			if (flag7)
			{
				hashSet2.Add(item);
			}
		}
		foreach (uint item2 in BtcAvatar._srv.Keys)
		{
			bool flag8 = !hashSet.Contains(item2);
			if (flag8)
			{
				hashSet2.Add(item2);
			}
		}
		List<uint> list = new List<uint>();
		foreach (uint item3 in hashSet2)
		{
			bool flag9 = BtcAvatar._deadPrev.Contains(item3);
			if (flag9)
			{
				list.Add(item3);
			}
		}
		BtcAvatar._deadPrev = hashSet2;
		for (int j = 0; j < list.Count; j++)
		{
			BtcAvatar.Rx rx;
			bool flag10 = BtcAvatar._view.TryGetValue(list[j], out rx);
			if (flag10)
			{
				bool flag11 = rx.anim != null && rx.anim.tex != null;
				if (flag11)
				{
					UnityEngine.Object.Destroy(rx.anim.tex);
				}
				BtcAvatar._view.Remove(list[j]);
			}
			BtcAvatar._srv.Remove(list[j]);
		}
		bool active = NetworkServer.active;
		bool flag12 = active;
		bool flag13 = flag12;
		if (flag13)
		{
			for (int k = BtcAvatar._mods.Count - 1; k >= 0; k--)
			{
				bool flag14 = false;
				for (int l = 0; l < NetworkServer.connections.Count; l++)
				{
					NetworkConnection networkConnection = NetworkServer.connections[l];
					bool flag15 = networkConnection != null && networkConnection.connectionId == BtcAvatar._mods[k];
					bool flag16 = flag15;
					bool flag17 = flag16;
					if (flag17)
					{
						flag14 = true;
						break;
					}
				}
				bool flag18 = !flag14;
				bool flag19 = flag18;
				bool flag20 = flag19;
				if (flag20)
				{
					BtcAvatar._mods.RemoveAt(k);
				}
			}
		}
	}

	private static void ResetNet()
	{
		BtcAvatar._hadNet = false;
		foreach (KeyValuePair<uint, BtcAvatar.Rx> keyValuePair in BtcAvatar._view)
		{
			bool flag = keyValuePair.Value.anim != null && keyValuePair.Value.anim.tex != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				UnityEngine.Object.Destroy(keyValuePair.Value.anim.tex);
			}
		}
		BtcAvatar._view.Clear();
		BtcAvatar._srv.Clear();
		BtcAvatar._mods.Clear();
		BtcAvatar._deadPrev.Clear();
		BtcAvatar._sOut.Clear();
		BtcAvatar._cOut.Clear();
		BtcAvatar._srvReg = false;
		BtcAvatar._cl = null;
		BtcAvatar._annVer = -1;
		BtcAvatar._hostVer = -1;
	}

	private static void EnsureFiles(bool force)
	{
		bool flag = !force && Time.unscaledTime - BtcAvatar._filesAt < 5f;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcAvatar._filesAt = Time.unscaledTime;
			try
			{
				Directory.CreateDirectory(BtcAvatar.Dir);
				List<string> list = new List<string>();
				string[] files = Directory.GetFiles(BtcAvatar.Dir);
				for (int i = 0; i < files.Length; i++)
				{
					string a = Path.GetExtension(files[i]).ToLowerInvariant();
					bool flag4 = a == ".png" || a == ".jpg" || a == ".jpeg" || a == ".gif";
					bool flag5 = flag4;
					bool flag6 = flag5;
					if (flag6)
					{
						list.Add(Path.GetFileName(files[i]));
					}
				}
				list.Sort(StringComparer.OrdinalIgnoreCase);
				BtcAvatar._files = list.ToArray();
			}
			catch (Exception)
			{
				BtcAvatar._files = new string[0];
			}
		}
	}

	public static void DrawUI(float x, float y, float w, float h, float row, float pad, GUIStyle btn, GUIStyle label, GUIStyle tog, Texture2D px)
	{
		BtcAvatar._uiSeen = Time.unscaledTime;
		BtcAvatar.LoadCfg();
		BtcAvatar.EnsureFiles(false);
		bool flag = BtcAvatar._wrap == null || BtcAvatar._wrapFs != label.fontSize;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcAvatar._wrapFs = label.fontSize;
			BtcAvatar._wrap = new GUIStyle(label);
			BtcAvatar._wrap.wordWrap = true;
			BtcAvatar._wrap.alignment = TextAnchor.UpperLeft;
		}
		float num = y;
		GUI.Label(new Rect(x, num, w, row), BtcLang.Tr("Folder: BetterThanCastle/Avatars (png, jpg, gif)"), label);
		num += row + pad * 0.5f;
		float num2 = (w - pad) * 0.5f;
		bool flag5 = GUI.Button(new Rect(x, num, num2, row), BtcLang.Tr("Open folder"), btn);
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			try
			{
				Directory.CreateDirectory(BtcAvatar.Dir);
				Application.OpenURL("file:///" + BtcAvatar.Dir.Replace('\\', '/'));
			}
			catch (Exception)
			{
			}
		}
		bool flag9 = GUI.Button(new Rect(x + num2 + pad, num, num2, row), BtcLang.Tr("Refresh"), btn);
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		if (flag12)
		{
			BtcAvatar.EnsureFiles(true);
		}
		num += row + pad * 0.5f;
		float num3 = row * 4f;
		Color color = GUI.color;
		GUI.color = new Color(0f, 0f, 0f, 0.6f);
		GUI.DrawTexture(new Rect(x, num, num3, num3), px);
		GUI.color = Color.white;
		bool flag13 = BtcAvatar._local != null && BtcAvatar._local.tex != null;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			GUI.DrawTexture(new Rect(x + 2f, num + 2f, num3 - 4f, num3 - 4f), BtcAvatar._local.tex, ScaleMode.ScaleToFit, true);
		}
		GUI.Label(new Rect(x + num3 + pad, num, w - num3 - pad, num3), BtcAvatar._status, BtcAvatar._wrap);
		num += num3 + pad * 0.5f;
		bool flag17 = GUI.Button(new Rect(x, num, w, row), BtcLang.Tr("No custom avatar (Steam)"), btn);
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		if (flag20)
		{
			BtcAvatar.Select(string.Empty);
		}
		num += row + pad * 0.5f;
		float num4 = y + h - num;
		bool flag21 = num4 > row;
		bool flag22 = flag21;
		bool flag23 = flag22;
		bool flag24 = flag23;
		if (flag24)
		{
			float num5 = row + 2f;
			BtcAvatar._scroll = GUI.BeginScrollView(new Rect(x, num, w, num4), BtcAvatar._scroll, new Rect(0f, 0f, w - 18f, (float)BtcAvatar._files.Length * num5), false, true);
			for (int i = 0; i < BtcAvatar._files.Length; i++)
			{
				bool flag25 = BtcAvatar._files[i] == BtcAvatar._file;
				GUI.color = (flag25 ? new Color(0.6f, 1f, 0.6f, 1f) : Color.white);
				bool flag26 = GUI.Button(new Rect(0f, (float)i * num5, w - 18f, row), BtcAvatar._files[i], btn) && !flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				if (flag29)
				{
					BtcAvatar.Select(BtcAvatar._files[i]);
				}
			}
			GUI.color = Color.white;
			GUI.EndScrollView();
		}
		GUI.color = color;
	}

	// Note: this type is marked as 'beforefieldinit'.
	static BtcAvatar()
	{
		BtcAvatar._lastFrame = -1;
		BtcAvatar._files = new string[0];
		BtcAvatar._filesAt = -100f;
	}

	private static void RefreshList()
	{
		try
		{
			GameSignals.Invoke(UNetManager.PlayerListChanged);
		}
		catch (Exception)
		{
		}
	}

	public const int Size = 64;

	public const int MaxFrames = 32;

	private const short MsgUp = 92;

	private const short MsgDown = 91;

	private const int ChunkBytes = 1000;

	private const int MaxChunks = 300;

	private const int PumpPerTick = 6;

	private const int MaxFileBytes = 41943040;

	private static bool _cfgLoaded;

	private static string _file = string.Empty;

	private static BtcAvatar.Anim _local;

	private static byte[] _packed;

	private static int _ver;

	private static string _status = string.Empty;

	private static float _uiSeen = -10f;

	private static volatile bool _pendReady;

	private static List<Color32[]> _pendFrames;

	private static List<int> _pendDelays;

	private static byte[] _pendPacked;

	private static string _pendErr;

	private static string _pendName;

	private static int _loadToken;

	private static readonly Dictionary<uint, BtcAvatar.Rx> _srv = new Dictionary<uint, BtcAvatar.Rx>();

	private static readonly Dictionary<uint, BtcAvatar.Rx> _view = new Dictionary<uint, BtcAvatar.Rx>();

	private static readonly List<int> _mods = new List<int>();

	private static readonly Queue<KeyValuePair<int, BtcAvatar.AvaMsg>> _sOut = new Queue<KeyValuePair<int, BtcAvatar.AvaMsg>>();

	private static readonly Queue<BtcAvatar.AvaMsg> _cOut = new Queue<BtcAvatar.AvaMsg>();

	private static bool _srvReg;

	private static NetworkClient _cl;

	private static int _annVer = -1;

	private static int _hostVer = -1;

	private static bool _hadNet;

	private static float _pruneAt;

	private static int _lastFrame;

	private static string[] _files;

	private static float _filesAt;

	private static Vector2 _scroll;

	private static GUIStyle _wrap;

	private static int _wrapFs;

	private static HashSet<uint> _deadPrev = new HashSet<uint>();

	public sealed class Anim
	{
		public Texture2D tex;

		public Color32[][] frames;

		public int[] delayMs;

		public int cur;

		public float next;
	}

	private sealed class Rx
	{
		public int ver = -1;

		public int count;

		public int got;

		public byte[][] parts;

		public BtcAvatar.Anim anim;
	}

	public class AvaMsg : MessageBase
	{
		public override void Serialize(NetworkWriter w)
		{
			w.WritePackedUInt32(this.nid);
			w.Write(this.ver);
			w.Write(this.idx);
			w.Write(this.count);
			w.WriteBytesFull(this.data ?? new byte[0]);
		}

		public override void Deserialize(NetworkReader r)
		{
			this.nid = r.ReadPackedUInt32();
			this.ver = r.ReadInt32();
			this.idx = r.ReadInt16();
			this.count = r.ReadInt16();
			this.data = r.ReadBytesAndSize();
		}

		public uint nid;

		public int ver;

		public short idx;

		public short count;

		public byte[] data;
	}
}
}
