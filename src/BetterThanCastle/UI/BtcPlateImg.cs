using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
public static class BtcPlateImg
{
	private static string Dir
	{
		get
		{
			return Path.Combine(BtcPaths.Root, "Plates");
		}
	}

	private static string CfgPath
	{
		get
		{
			return BtcPaths.Config("btc_plateimg.txt");
		}
	}

	private static string T(string en, string ru)
	{
		return BtcLang.Russian ? ru : en;
	}

	public static string SelectedFile
	{
		get
		{
			BtcPlateImg.LoadCfg();
			return BtcPlateImg._file;
		}
	}

	public static BtcPlateImg.Anim Local
	{
		get
		{
			return BtcPlateImg._local;
		}
	}

	public static bool TakeSelected()
	{
		bool justSelected = BtcPlateImg._justSelected;
		BtcPlateImg._justSelected = false;
		return justSelected;
	}

	public static BtcPlateImg.Anim For(uint nid, bool local)
	{
		BtcPlateImg.Anim anim = null;
		if (local)
		{
			anim = BtcPlateImg._local;
		}
		else
		{
			BtcPlateImg.Rx rx;
			bool flag = BtcPlateImg._view.TryGetValue(nid, out rx);
			if (flag)
			{
				anim = rx.anim;
			}
		}
		bool flag2 = anim != null;
		if (flag2)
		{
			anim.used = Time.unscaledTime;
		}
		return anim;
	}

	public static void Tick()
	{
		bool flag = Time.frameCount == BtcPlateImg._lastFrame;
		if (!flag)
		{
			BtcPlateImg._lastFrame = Time.frameCount;
			try
			{
				BtcPlateImg.LoadCfg();
				bool pendReady = BtcPlateImg._pendReady;
				if (pendReady)
				{
					BtcPlateImg.FinishLocal();
				}
				bool flag2 = NetworkServer.active || NetworkClient.active;
				bool flag3 = !flag2;
				if (flag3)
				{
					bool hadNet = BtcPlateImg._hadNet;
					if (hadNet)
					{
						BtcPlateImg.ResetNet();
					}
				}
				else
				{
					BtcPlateImg._hadNet = true;
					UNetManager instance = UNetManager.instance;
					bool flag4 = instance != null;
					if (flag4)
					{
						BtcPlateImg.Register();
						LobbyPlayer localLobbyPlayer = instance.LocalLobbyPlayer;
						bool active = NetworkServer.active;
						if (active)
						{
							BtcPlateImg.ServerTick(localLobbyPlayer);
						}
						else
						{
							BtcPlateImg.ClientTick(localLobbyPlayer);
						}
						BtcPlateImg.Pump();
						bool flag5 = Time.unscaledTime >= BtcPlateImg._pruneAt;
						if (flag5)
						{
							BtcPlateImg._pruneAt = Time.unscaledTime + 2f;
							BtcPlateImg.Prune(instance);
						}
					}
				}
				float unscaledTime = Time.unscaledTime;
				BtcPlateImg.AnimateOne(BtcPlateImg._local, unscaledTime);
				foreach (KeyValuePair<uint, BtcPlateImg.Rx> keyValuePair in BtcPlateImg._view)
				{
					BtcPlateImg.AnimateOne(keyValuePair.Value.anim, unscaledTime);
				}
			}
			catch (Exception)
			{
			}
		}
	}

	private static void LoadCfg()
	{
		bool cfgLoaded = BtcPlateImg._cfgLoaded;
		if (!cfgLoaded)
		{
			BtcPlateImg._cfgLoaded = true;
			try
			{
				BtcPaths.Init();
				Directory.CreateDirectory(BtcPlateImg.Dir);
				BtcPlateImg._file = BtcPaths.ReadKey("btc_plateimg.txt", "file", string.Empty);
				bool flag = BtcPlateImg._file == null;
				if (flag)
				{
					BtcPlateImg._file = string.Empty;
				}
			}
			catch (Exception)
			{
			}
			BtcPlateImg.StartLoad();
		}
	}

	private static void SaveCfg()
	{
		try
		{
			File.WriteAllText(BtcPlateImg.CfgPath, "file=" + BtcPlateImg._file + "\r\n");
		}
		catch (Exception)
		{
		}
	}

	private static void Select(string name)
	{
		BtcPlateImg._file = (name ?? string.Empty);
		BtcPlateImg._justSelected = (BtcPlateImg._file.Length > 0);
		BtcPlateImg.SaveCfg();
		BtcPlateImg.StartLoad();
	}

	private static void StartLoad()
	{
		BtcPlateImg._loadToken++;
		BtcPlateImg._pendReady = false;
		bool flag = string.IsNullOrEmpty(BtcPlateImg._file);
		if (flag)
		{
			BtcPlateImg.SetLocal(null);
			BtcPlateImg._status = BtcPlateImg.T("No plate image.", "Картинка плашки не выбрана.");
		}
		else
		{
			string path = Path.Combine(BtcPlateImg.Dir, BtcPlateImg._file);
			bool flag2 = !File.Exists(path);
			if (flag2)
			{
				BtcPlateImg.SetLocal(null);
				BtcPlateImg._status = BtcPlateImg.T("File not found: ", "Файл не найден: ") + BtcPlateImg._file;
			}
			else
			{
				string a = Path.GetExtension(path).ToLowerInvariant();
				BtcPlateImg._status = BtcPlateImg.T("Loading...", "Загрузка...");
				bool flag3 = a == ".gif";
				if (flag3)
				{
					int token = BtcPlateImg._loadToken;
					string name = BtcPlateImg._file;
					ThreadPool.QueueUserWorkItem(delegate(object o)
					{
						byte[] pendPacked = null;
						string pendErr = null;
						try
						{
							FileInfo fileInfo2 = new FileInfo(path);
							bool flag6 = fileInfo2.Length > 41943040L;
							if (flag6)
							{
								throw new Exception("file too big");
							}
							byte[] d = File.ReadAllBytes(path);
							List<Color32[]> fr;
							List<int> dl;
							int w;
							int h;
							bool flag7 = !BtcPlateImg.DecodeGif(d, out fr, out dl, out w, out h);
							if (flag7)
							{
								throw new Exception("bad gif");
							}
							pendPacked = BtcPlateImg.Pack(fr, dl, w, h);
						}
						catch (Exception ex2)
						{
							pendErr = ex2.Message;
						}
						bool flag8 = token == BtcPlateImg._loadToken;
						if (flag8)
						{
							BtcPlateImg._pendPacked = pendPacked;
							BtcPlateImg._pendErr = pendErr;
							BtcPlateImg._pendName = name;
							BtcPlateImg._pendReady = true;
						}
					});
				}
				else
				{
					try
					{
						FileInfo fileInfo = new FileInfo(path);
						bool flag4 = fileInfo.Length > 41943040L;
						if (flag4)
						{
							throw new Exception("file too big");
						}
						byte[] data = File.ReadAllBytes(path);
						Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, false);
						bool flag5 = !texture2D.LoadImage(data) || (long)texture2D.width * (long)texture2D.height > 16000000L;
						if (flag5)
						{
							UnityEngine.Object.Destroy(texture2D);
							throw new Exception("bad image");
						}
						Color32[] pixels = texture2D.GetPixels32();
						int width = texture2D.width;
						int height = texture2D.height;
						UnityEngine.Object.Destroy(texture2D);
						int num;
						int num2;
						BtcPlateImg.Dims(width, height, out num, out num2);
						BtcPlateImg._pendPacked = BtcPlateImg.Pack(new List<Color32[]>
						{
							BtcPlateImg.Down(pixels, width, height, num, num2, false)
						}, new List<int>
						{
							1000
						}, num, num2);
						BtcPlateImg._pendErr = null;
						BtcPlateImg._pendName = BtcPlateImg._file;
						BtcPlateImg._pendReady = true;
					}
					catch (Exception ex)
					{
						BtcPlateImg.SetLocal(null);
						BtcPlateImg._status = BtcPlateImg.T("Error: ", "Ошибка: ") + ex.Message;
					}
				}
			}
		}
	}

	private static void FinishLocal()
	{
		BtcPlateImg._pendReady = false;
		bool flag = BtcPlateImg._pendName != BtcPlateImg._file;
		if (!flag)
		{
			bool flag2 = BtcPlateImg._pendErr != null || BtcPlateImg._pendPacked == null;
			if (flag2)
			{
				BtcPlateImg.SetLocal(null);
				BtcPlateImg._status = BtcPlateImg.T("Error: ", "Ошибка: ") + (BtcPlateImg._pendErr ?? "?");
			}
			else
			{
				byte[] pendPacked = BtcPlateImg._pendPacked;
				BtcPlateImg._pendPacked = null;
				bool flag3 = pendPacked.Length > 400000;
				if (flag3)
				{
					BtcPlateImg.SetLocal(null);
					BtcPlateImg._status = string.Concat(new object[]
					{
						BtcPlateImg.T("Too heavy to send: ", "Слишком тяжёлая для передачи: "),
						pendPacked.Length / 1024 + 1,
						" KB (max ",
						390,
						" KB). ",
						BtcPlateImg.T("Use fewer frames or a smaller picture.", "Уменьшите число кадров или размер.")
					});
				}
				else
				{
					BtcPlateImg.SetLocal(pendPacked);
					bool flag4 = BtcPlateImg._local != null;
					if (flag4)
					{
						BtcPlateImg._status = string.Concat(new object[]
						{
							BtcPlateImg._file,
							"\n",
							BtcPlateImg._local.w,
							"x",
							BtcPlateImg._local.h,
							", ",
							BtcPlateImg._local.idx.Length,
							" ",
							BtcPlateImg.T("frames", "кадров"),
							", ",
							pendPacked.Length / 1024 + 1,
							" KB"
						});
					}
					else
					{
						BtcPlateImg._status = BtcPlateImg.T("Error: ", "Ошибка: ") + "unpack";
					}
				}
			}
		}
	}

	private static void SetLocal(byte[] packed)
	{
		bool flag = BtcPlateImg._local != null && BtcPlateImg._local.tex != null;
		if (flag)
		{
			UnityEngine.Object.Destroy(BtcPlateImg._local.tex);
		}
		BtcPlateImg._local = null;
		BtcPlateImg._packed = packed;
		bool flag2 = packed != null;
		if (flag2)
		{
			try
			{
				BtcPlateImg._local = BtcPlateImg.Unpack(packed);
			}
			catch (Exception)
			{
				BtcPlateImg._local = null;
			}
			bool flag3 = BtcPlateImg._local == null;
			if (flag3)
			{
				BtcPlateImg._packed = null;
			}
		}
		BtcPlateImg._ver++;
	}

	private static BtcPlateImg.Anim MakeAnim(int w, int h, byte[][] idx, Color32[] pal, int[] delays)
	{
		BtcPlateImg.Anim anim = new BtcPlateImg.Anim();
		anim.w = w;
		anim.h = h;
		anim.idx = idx;
		anim.pal = pal;
		anim.delayMs = delays;
		anim.buf = new Color32[w * h];
		anim.tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
		anim.tex.filterMode = FilterMode.Bilinear;
		anim.tex.wrapMode = TextureWrapMode.Clamp;
		BtcPlateImg.Paint(anim, 0);
		anim.cur = 0;
		anim.next = Time.unscaledTime + (float)Math.Max(20, delays[0]) / 1000f;
		anim.used = -10f;
		return anim;
	}

	private static void Paint(BtcPlateImg.Anim a, int f)
	{
		byte[] array = a.idx[f];
		Color32[] buf = a.buf;
		Color32[] pal = a.pal;
		for (int i = 0; i < buf.Length; i++)
		{
			buf[i] = pal[(int)array[i]];
		}
		a.tex.SetPixels32(buf);
		a.tex.Apply(false);
	}

	private static void AnimateOne(BtcPlateImg.Anim a, float now)
	{
		bool flag = a == null || a.idx == null || a.idx.Length < 2 || a.tex == null || now - a.used > 0.5f;
		if (!flag)
		{
			bool flag2 = now < a.next;
			if (!flag2)
			{
				a.cur = (a.cur + 1) % a.idx.Length;
				BtcPlateImg.Paint(a, a.cur);
				float num = (float)Math.Max(20, a.delayMs[a.cur]) / 1000f;
				a.next = Math.Max(a.next + num, now);
			}
		}
	}

	private static void Dims(int sw, int sh, out int ow, out int oh)
	{
		float num = Math.Min(1f, Math.Min(256f / (float)sw, 128f / (float)sh));
		ow = Math.Max(4, Math.Min(256, (int)Math.Round((double)((float)sw * num))));
		oh = Math.Max(4, Math.Min(128, (int)Math.Round((double)((float)sh * num))));
	}

	private static Color32[] Down(Color32[] s, int sw, int sh, int ow, int oh, bool flipY)
	{
		Color32[] array = new Color32[ow * oh];
		float num = (float)sw / (float)ow;
		float num2 = (float)sh / (float)oh;
		for (int i = 0; i < oh; i++)
		{
			int num3 = (int)((float)i * num2);
			int num4 = Math.Max((int)((float)(i + 1) * num2), num3 + 1);
			int num5 = Math.Max(1, (num4 - num3) / 6);
			int num6 = flipY ? (oh - 1 - i) : i;
			for (int j = 0; j < ow; j++)
			{
				int num7 = (int)((float)j * num);
				int num8 = Math.Max((int)((float)(j + 1) * num), num7 + 1);
				int num9 = Math.Max(1, (num8 - num7) / 6);
				long num10 = 0L;
				long num11 = 0L;
				long num12 = 0L;
				long num13 = 0L;
				int num14 = 0;
				int num15 = num3;
				while (num15 < num4 && num15 < sh)
				{
					int num16 = num15 * sw;
					int num17 = num7;
					while (num17 < num8 && num17 < sw)
					{
						Color32 color = s[num16 + num17];
						num10 += (long)(color.r * color.a);
						num11 += (long)(color.g * color.a);
						num12 += (long)(color.b * color.a);
						num13 += (long)((ulong)color.a);
						num14++;
						num17 += num9;
					}
					num15 += num5;
				}
				Color32 color2 = default(Color32);
				bool flag = num14 > 0 && num13 > 0L;
				if (flag)
				{
					color2.r = (byte)(num10 / num13);
					color2.g = (byte)(num11 / num13);
					color2.b = (byte)(num12 / num13);
					color2.a = (byte)(num13 / (long)num14);
				}
				array[num6 * ow + j] = color2;
			}
		}
		return array;
	}

	private static int U16(byte[] d, int p)
	{
		return (int)d[p] | (int)d[p + 1] << 8;
	}

	private static bool DecodeGif(byte[] d, out List<Color32[]> outFrames, out List<int> outDelays, out int ow, out int oh)
	{
		outFrames = null;
		outDelays = null;
		ow = 0;
		oh = 0;
		bool flag = d.Length < 13 || d[0] != 71 || d[1] != 73 || d[2] != 70;
		bool result;
		if (flag)
		{
			result = false;
		}
		else
		{
			int num = BtcPlateImg.U16(d, 6);
			int num2 = BtcPlateImg.U16(d, 8);
			bool flag2 = num <= 0 || num2 <= 0 || num > 4096 || num2 > 4096 || (long)num * (long)num2 > 16000000L;
			if (flag2)
			{
				result = false;
			}
			else
			{
				BtcPlateImg.Dims(num, num2, out ow, out oh);
				int num3 = (int)d[10];
				int i = 13;
				Color32[] array = null;
				bool flag3 = (num3 & 128) != 0;
				if (flag3)
				{
					int n = 1 << (num3 & 7) + 1;
					array = BtcPlateImg.ReadPal(d, ref i, n);
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
				while (i < d.Length && list.Count < 300)
				{
					int num8 = (int)d[i++];
					bool flag4 = num8 == 59;
					if (flag4)
					{
						break;
					}
					bool flag5 = num8 == 33;
					if (flag5)
					{
						int num9 = (int)d[i++];
						int num10 = (int)d[i++];
						bool flag6 = num9 == 249 && num10 >= 4;
						if (flag6)
						{
							int num11 = (int)d[i];
							num6 = (num11 >> 2 & 7);
							num4 = BtcPlateImg.U16(d, i + 1);
							num5 = (((num11 & 1) != 0) ? ((int)d[i + 3]) : -1);
						}
						for (i += num10; i < d.Length; i += num10)
						{
							num10 = (int)d[i++];
							bool flag7 = num10 == 0;
							if (flag7)
							{
								break;
							}
						}
					}
					else
					{
						bool flag8 = num8 != 44;
						if (flag8)
						{
							break;
						}
						int num12 = BtcPlateImg.U16(d, i);
						int num13 = BtcPlateImg.U16(d, i + 2);
						int num14 = BtcPlateImg.U16(d, i + 4);
						int num15 = BtcPlateImg.U16(d, i + 6);
						int num16 = (int)d[i + 8];
						i += 9;
						Color32[] array4 = array;
						bool flag9 = (num16 & 128) != 0;
						if (flag9)
						{
							int n2 = 1 << (num16 & 7) + 1;
							array4 = BtcPlateImg.ReadPal(d, ref i, n2);
						}
						bool inter = (num16 & 64) != 0;
						int num17 = (int)d[i++];
						MemoryStream memoryStream = new MemoryStream();
						while (i < d.Length)
						{
							int num18 = (int)d[i++];
							bool flag10 = num18 == 0;
							if (flag10)
							{
								break;
							}
							bool flag11 = i + num18 > d.Length;
							if (flag11)
							{
								num18 = d.Length - i;
							}
							memoryStream.Write(d, i, num18);
							i += num18;
						}
						bool flag12 = array4 == null || num14 <= 0 || num15 <= 0 || num17 < 1 || num17 > 11;
						if (!flag12)
						{
							byte[] array5 = BtcPlateImg.Lzw(memoryStream.ToArray(), num17, num14 * num15);
							bool flag13 = num7 == 2;
							if (flag13)
							{
								BtcPlateImg.ClearRect(array2, num, num2, x, y, rw, rh);
							}
							else
							{
								bool flag14 = num7 == 3 && array3 != null;
								if (flag14)
								{
									Array.Copy(array3, array2, array2.Length);
								}
							}
							bool flag15 = num6 == 3;
							if (flag15)
							{
								bool flag16 = array3 == null;
								if (flag16)
								{
									array3 = new Color32[array2.Length];
								}
								Array.Copy(array2, array3, array2.Length);
							}
							int[] array6 = BtcPlateImg.RowOrder(num15, inter);
							for (int j = 0; j < num15; j++)
							{
								int num19 = num13 + array6[j];
								bool flag17 = num19 < 0 || num19 >= num2;
								if (!flag17)
								{
									for (int k = 0; k < num14; k++)
									{
										int num20 = num12 + k;
										bool flag18 = num20 < 0 || num20 >= num;
										if (!flag18)
										{
											int num21 = (int)array5[j * num14 + k];
											bool flag19 = num21 == num5 || num21 >= array4.Length;
											if (!flag19)
											{
												array2[num19 * num + num20] = array4[num21];
											}
										}
									}
								}
							}
							list.Add(BtcPlateImg.Down(array2, num, num2, ow, oh, true));
							int num22 = num4 * 10;
							bool flag20 = num22 <= 10;
							if (flag20)
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
				bool flag21 = list.Count == 0;
				if (flag21)
				{
					result = false;
				}
				else
				{
					bool flag22 = list.Count > 32;
					if (flag22)
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
			if (flag)
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
			if (!flag)
			{
				int num2 = x;
				while (num2 < x + rw && num2 < w)
				{
					bool flag2 = num2 >= 0;
					if (flag2)
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
		int[] result;
		if (flag)
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
			if (flag)
			{
				bool flag2 = num6 < num4;
				if (flag2)
				{
					bool flag3 = num8 >= data.Length;
					if (flag3)
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
					bool flag4 = k > num3 || k == num2;
					if (flag4)
					{
						break;
					}
					bool flag5 = k == num;
					if (flag5)
					{
						num4 = minCode + 1;
						num5 = (1 << num4) - 1;
						num3 = num2 + 1;
						num9 = -1;
						continue;
					}
					bool flag6 = num9 == -1;
					if (flag6)
					{
						array3[num11++] = array2[k];
						num9 = k;
						num10 = k;
						continue;
					}
					int num12 = k;
					bool flag7 = k == num3;
					if (flag7)
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
					bool flag8 = num3 < 4096;
					if (flag8)
					{
						array[num3] = (short)num9;
						array2[num3] = (byte)num10;
						num3++;
						bool flag9 = (num3 & num5) == 0 && num3 < 4096;
						if (flag9)
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

	private static byte[] Pack(List<Color32[]> fr, List<int> dl, int w, int h)
	{
		int count = fr.Count;
		int num = w * h;
		int[] hist = new int[32768];
		for (int i = 0; i < count; i++)
		{
			Color32[] array = fr[i];
			for (int j = 0; j < num; j++)
			{
				bool flag = array[j].a >= 128;
				if (flag)
				{
					hist[array[j].r >> 3 << 10 | array[j].g >> 3 << 5 | array[j].b >> 3]++;
				}
			}
		}
		List<int> list = new List<int>();
		for (int k = 0; k < 32768; k++)
		{
			bool flag2 = hist[k] > 0;
			if (flag2)
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
			bool flag3 = m < num2;
			if (flag3)
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
					bool flag4 = num13 < num9;
					if (flag4)
					{
						num9 = num13;
						num8 = n;
					}
				}
				array6[num4] = (byte)(num8 + 1);
			}
		}
		MemoryStream memoryStream = new MemoryStream();
		memoryStream.WriteByte((byte)(w & 255));
		memoryStream.WriteByte((byte)(w >> 8));
		memoryStream.WriteByte((byte)(h & 255));
		memoryStream.WriteByte((byte)(h >> 8));
		memoryStream.WriteByte((byte)count);
		for (int num14 = 0; num14 < count; num14++)
		{
			int num15 = Math.Max(2, Math.Min(65535, dl[num14] / 10));
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
				bool flag5 = array9[num17].a >= 128;
				if (flag5)
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

	private static BtcPlateImg.Anim Unpack(byte[] z)
	{
		int num = 1049410;
		byte[] array = new byte[num + 1];
		int i = 0;
		using (DeflateStream deflateStream = new DeflateStream(new MemoryStream(z), CompressionMode.Decompress))
		{
			while (i < array.Length)
			{
				int num2 = deflateStream.Read(array, i, array.Length - i);
				bool flag = num2 <= 0;
				if (flag)
				{
					break;
				}
				i += num2;
			}
		}
		bool flag2 = i < 5 || i > num;
		BtcPlateImg.Anim result;
		if (flag2)
		{
			result = null;
		}
		else
		{
			int num3 = (int)array[0] | (int)array[1] << 8;
			int num4 = (int)array[2] | (int)array[3] << 8;
			int num5 = (int)array[4];
			bool flag3 = num3 < 1 || num4 < 1 || num3 > 256 || num4 > 128 || num5 < 1 || num5 > 32;
			if (flag3)
			{
				result = null;
			}
			else
			{
				int num6 = num3 * num4;
				bool flag4 = i != 5 + 2 * num5 + 765 + num6 * num5;
				if (flag4)
				{
					result = null;
				}
				else
				{
					int[] array2 = new int[num5];
					for (int j = 0; j < num5; j++)
					{
						array2[j] = ((int)array[5 + j * 2] | (int)array[6 + j * 2] << 8) * 10;
					}
					int num7 = 5 + 2 * num5;
					Color32[] array3 = new Color32[256];
					for (int k = 0; k < 255; k++)
					{
						array3[k + 1] = new Color32(array[num7 + k * 3], array[num7 + k * 3 + 1], array[num7 + k * 3 + 2], byte.MaxValue);
					}
					int num8 = num7 + 765;
					byte[][] array4 = new byte[num5][];
					byte[] array5 = new byte[num6];
					for (int l = 0; l < num5; l++)
					{
						byte[] array6 = new byte[num6];
						for (int m = 0; m < num6; m++)
						{
							byte b = (byte)(array[num8 + l * num6 + m] ^ array5[m]);
							array5[m] = b;
							array6[m] = b;
						}
						array4[l] = array6;
					}
					result = BtcPlateImg.MakeAnim(num3, num4, array4, array3, array2);
				}
			}
		}
		return result;
	}

	private static void Register()
	{
		bool active = NetworkServer.active;
		if (active)
		{
			bool flag = !BtcPlateImg._srvReg;
			if (flag)
			{
				BtcPlateImg._srvReg = true;
				NetworkServer.RegisterHandler(95, new NetworkMessageDelegate(BtcPlateImg.OnUp));
			}
		}
		else
		{
			BtcPlateImg._srvReg = false;
		}
		NetworkClient networkClient = (NetworkClient.allClients.Count > 0) ? NetworkClient.allClients[0] : null;
		bool flag2 = networkClient != BtcPlateImg._cl;
		if (flag2)
		{
			BtcPlateImg._cl = networkClient;
			BtcPlateImg._annVer = -1;
			bool flag3 = networkClient != null;
			if (flag3)
			{
				networkClient.RegisterHandler(96, new NetworkMessageDelegate(BtcPlateImg.OnDown));
			}
		}
	}

	private static List<BtcPlateImg.ImgMsg> MakeMsgs(byte[] packed, int ver, uint nid)
	{
		List<BtcPlateImg.ImgMsg> list = new List<BtcPlateImg.ImgMsg>();
		bool flag = packed == null || packed.Length == 0;
		List<BtcPlateImg.ImgMsg> result;
		if (flag)
		{
			list.Add(new BtcPlateImg.ImgMsg
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
			bool flag2 = num > 400;
			if (flag2)
			{
				list.Add(new BtcPlateImg.ImgMsg
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
				for (int i = 0; i < num; i++)
				{
					int num2 = i * 1000;
					int num3 = Math.Min(1000, packed.Length - num2);
					byte[] array = new byte[num3];
					Array.Copy(packed, num2, array, 0, num3);
					list.Add(new BtcPlateImg.ImgMsg
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
		bool flag = BtcPlateImg._cl == null || !BtcPlateImg._cl.isConnected || me == null || BtcPlateImg._annVer == BtcPlateImg._ver;
		if (!flag)
		{
			BtcPlateImg._annVer = BtcPlateImg._ver;
			BtcPlateImg._cOut.Clear();
			List<BtcPlateImg.ImgMsg> list = BtcPlateImg.MakeMsgs(BtcPlateImg._packed, BtcPlateImg._ver, 0U);
			for (int i = 0; i < list.Count; i++)
			{
				BtcPlateImg._cOut.Enqueue(list[i]);
			}
		}
	}

	private static void ServerTick(LobbyPlayer me)
	{
		bool flag = me == null || BtcPlateImg._hostVer == BtcPlateImg._ver;
		if (!flag)
		{
			BtcPlateImg._hostVer = BtcPlateImg._ver;
			uint value = me.netId.Value;
			BtcPlateImg.Rx srv = BtcPlateImg.GetSrv(value);
			List<BtcPlateImg.ImgMsg> list = BtcPlateImg.MakeMsgs(BtcPlateImg._packed, BtcPlateImg._ver, value);
			BtcPlateImg.Reset(srv, BtcPlateImg._ver, (list.Count == 1 && list[0].count == 0) ? 0 : list.Count);
			for (int i = 0; i < list.Count; i++)
			{
				bool flag2 = list[i].count > 0;
				if (flag2)
				{
					srv.parts[i] = list[i].data;
					srv.got++;
				}
				for (int j = 0; j < BtcPlateImg._mods.Count; j++)
				{
					BtcPlateImg._sOut.Enqueue(new KeyValuePair<int, BtcPlateImg.ImgMsg>(BtcPlateImg._mods[j], list[i]));
				}
			}
		}
	}

	private static BtcPlateImg.Rx GetSrv(uint owner)
	{
		BtcPlateImg.Rx rx;
		bool flag = !BtcPlateImg._srv.TryGetValue(owner, out rx);
		if (flag)
		{
			rx = new BtcPlateImg.Rx();
			BtcPlateImg._srv[owner] = rx;
		}
		return rx;
	}

	private static void Reset(BtcPlateImg.Rx rx, int ver, int count)
	{
		rx.ver = ver;
		rx.count = count;
		rx.got = 0;
		rx.parts = ((count > 0) ? new byte[count][] : null);
	}

	private static bool Accept(BtcPlateImg.Rx rx, BtcPlateImg.ImgMsg m)
	{
		bool flag = m.count < 0 || m.count > 400 || (m.count > 0 && (m.idx < 0 || m.idx >= m.count));
		bool result;
		if (flag)
		{
			result = false;
		}
		else
		{
			bool flag2 = m.data != null && m.data.Length > 1064;
			if (flag2)
			{
				result = false;
			}
			else
			{
				bool flag3 = rx.ver != m.ver || rx.count != (int)m.count;
				if (flag3)
				{
					BtcPlateImg.Reset(rx, m.ver, (int)m.count);
				}
				bool flag4 = m.count > 0 && rx.parts[(int)m.idx] == null;
				if (flag4)
				{
					rx.parts[(int)m.idx] = m.data;
					rx.got++;
				}
				result = true;
			}
		}
		return result;
	}

	private static void BuildView(uint owner, BtcPlateImg.Rx src)
	{
		BtcPlateImg.Rx rx;
		bool flag = !BtcPlateImg._view.TryGetValue(owner, out rx);
		if (flag)
		{
			rx = new BtcPlateImg.Rx();
			BtcPlateImg._view[owner] = rx;
		}
		bool flag2 = rx.anim != null && rx.anim.tex != null;
		if (flag2)
		{
			UnityEngine.Object.Destroy(rx.anim.tex);
		}
		rx.anim = null;
		bool flag3 = src.count <= 0 || src.got < src.count;
		if (!flag3)
		{
			MemoryStream memoryStream = new MemoryStream();
			for (int i = 0; i < src.count; i++)
			{
				memoryStream.Write(src.parts[i], 0, src.parts[i].Length);
			}
			try
			{
				rx.anim = BtcPlateImg.Unpack(memoryStream.ToArray());
			}
			catch (Exception)
			{
				rx.anim = null;
			}
		}
	}

	private static uint OwnerOf(int connId)
	{
		UNetManager instance = UNetManager.instance;
		bool flag = instance == null || instance.lobbySlots == null;
		uint result;
		if (flag)
		{
			result = 0U;
		}
		else
		{
			for (int i = 0; i < instance.lobbySlots.Length; i++)
			{
				LobbyPlayer lobbyPlayer = instance.lobbySlots[i] as LobbyPlayer;
				bool flag2 = lobbyPlayer != null && lobbyPlayer.connectionToClient != null && lobbyPlayer.connectionToClient.connectionId == connId;
				if (flag2)
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
			BtcPlateImg.ImgMsg imgMsg = netMsg.ReadMessage<BtcPlateImg.ImgMsg>();
			int connectionId = netMsg.conn.connectionId;
			uint num = BtcPlateImg.OwnerOf(connectionId);
			bool flag = num == 0U;
			if (!flag)
			{
				bool flag2 = !BtcPlateImg._mods.Contains(connectionId);
				bool flag3 = flag2;
				if (flag3)
				{
					BtcPlateImg._mods.Add(connectionId);
				}
				BtcPlateImg.Rx srv = BtcPlateImg.GetSrv(num);
				bool flag4 = BtcPlateImg.Accept(srv, imgMsg);
				if (flag4)
				{
					BtcPlateImg.ImgMsg imgMsg2 = new BtcPlateImg.ImgMsg();
					imgMsg2.nid = num;
					imgMsg2.ver = imgMsg.ver;
					imgMsg2.idx = imgMsg.idx;
					imgMsg2.count = imgMsg.count;
					imgMsg2.data = imgMsg.data;
					for (int i = 0; i < BtcPlateImg._mods.Count; i++)
					{
						bool flag5 = BtcPlateImg._mods[i] != connectionId;
						if (flag5)
						{
							BtcPlateImg._sOut.Enqueue(new KeyValuePair<int, BtcPlateImg.ImgMsg>(BtcPlateImg._mods[i], imgMsg2));
						}
					}
					bool flag6 = srv.count == 0 || srv.got >= srv.count;
					if (flag6)
					{
						BtcPlateImg.BuildView(num, srv);
					}
				}
				bool flag7 = flag2;
				if (flag7)
				{
					foreach (KeyValuePair<uint, BtcPlateImg.Rx> keyValuePair in BtcPlateImg._srv)
					{
						bool flag8 = keyValuePair.Key == num;
						if (!flag8)
						{
							BtcPlateImg.Rx value = keyValuePair.Value;
							bool flag9 = value.count <= 0 || value.got < value.count;
							if (!flag9)
							{
								for (int j = 0; j < value.count; j++)
								{
									BtcPlateImg.ImgMsg imgMsg3 = new BtcPlateImg.ImgMsg();
									imgMsg3.nid = keyValuePair.Key;
									imgMsg3.ver = value.ver;
									imgMsg3.idx = (short)j;
									imgMsg3.count = (short)value.count;
									imgMsg3.data = value.parts[j];
									BtcPlateImg._sOut.Enqueue(new KeyValuePair<int, BtcPlateImg.ImgMsg>(connectionId, imgMsg3));
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
			if (!active)
			{
				BtcPlateImg.ImgMsg imgMsg = netMsg.ReadMessage<BtcPlateImg.ImgMsg>();
				BtcPlateImg.Rx rx;
				bool flag = !BtcPlateImg._view.TryGetValue(imgMsg.nid, out rx);
				if (flag)
				{
					rx = new BtcPlateImg.Rx();
					BtcPlateImg._view[imgMsg.nid] = rx;
				}
				bool flag2 = BtcPlateImg.Accept(rx, imgMsg) && (rx.count == 0 || rx.got >= rx.count);
				if (flag2)
				{
					BtcPlateImg.BuildView(imgMsg.nid, rx);
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
		while (BtcPlateImg._sOut.Count > 0 && num < 6)
		{
			KeyValuePair<int, BtcPlateImg.ImgMsg> keyValuePair = BtcPlateImg._sOut.Dequeue();
			num++;
			try
			{
				NetworkServer.SendToClient(keyValuePair.Key, 96, keyValuePair.Value);
			}
			catch (Exception)
			{
			}
		}
		num = 0;
		while (BtcPlateImg._cOut.Count > 0 && num < 6 && BtcPlateImg._cl != null && BtcPlateImg._cl.isConnected)
		{
			BtcPlateImg.ImgMsg msg = BtcPlateImg._cOut.Dequeue();
			num++;
			try
			{
				BtcPlateImg._cl.Send(95, msg);
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
		if (flag)
		{
			for (int i = 0; i < um.lobbySlots.Length; i++)
			{
				LobbyPlayer lobbyPlayer = um.lobbySlots[i] as LobbyPlayer;
				bool flag2 = lobbyPlayer != null;
				if (flag2)
				{
					hashSet.Add(lobbyPlayer.netId.Value);
				}
			}
		}
		HashSet<uint> hashSet2 = new HashSet<uint>();
		foreach (uint item in BtcPlateImg._view.Keys)
		{
			bool flag3 = !hashSet.Contains(item);
			if (flag3)
			{
				hashSet2.Add(item);
			}
		}
		foreach (uint item2 in BtcPlateImg._srv.Keys)
		{
			bool flag4 = !hashSet.Contains(item2);
			if (flag4)
			{
				hashSet2.Add(item2);
			}
		}
		List<uint> list = new List<uint>();
		foreach (uint item3 in hashSet2)
		{
			bool flag5 = BtcPlateImg._deadPrev.Contains(item3);
			if (flag5)
			{
				list.Add(item3);
			}
		}
		BtcPlateImg._deadPrev = hashSet2;
		for (int j = 0; j < list.Count; j++)
		{
			BtcPlateImg.Rx rx;
			bool flag6 = BtcPlateImg._view.TryGetValue(list[j], out rx);
			if (flag6)
			{
				bool flag7 = rx.anim != null && rx.anim.tex != null;
				if (flag7)
				{
					UnityEngine.Object.Destroy(rx.anim.tex);
				}
				BtcPlateImg._view.Remove(list[j]);
			}
			BtcPlateImg._srv.Remove(list[j]);
		}
		bool active = NetworkServer.active;
		if (active)
		{
			for (int k = BtcPlateImg._mods.Count - 1; k >= 0; k--)
			{
				bool flag8 = false;
				for (int l = 0; l < NetworkServer.connections.Count; l++)
				{
					NetworkConnection networkConnection = NetworkServer.connections[l];
					bool flag9 = networkConnection != null && networkConnection.connectionId == BtcPlateImg._mods[k];
					if (flag9)
					{
						flag8 = true;
						break;
					}
				}
				bool flag10 = !flag8;
				if (flag10)
				{
					BtcPlateImg._mods.RemoveAt(k);
				}
			}
		}
	}

	private static void ResetNet()
	{
		BtcPlateImg._hadNet = false;
		foreach (KeyValuePair<uint, BtcPlateImg.Rx> keyValuePair in BtcPlateImg._view)
		{
			bool flag = keyValuePair.Value.anim != null && keyValuePair.Value.anim.tex != null;
			if (flag)
			{
				UnityEngine.Object.Destroy(keyValuePair.Value.anim.tex);
			}
		}
		BtcPlateImg._view.Clear();
		BtcPlateImg._srv.Clear();
		BtcPlateImg._mods.Clear();
		BtcPlateImg._deadPrev.Clear();
		BtcPlateImg._sOut.Clear();
		BtcPlateImg._cOut.Clear();
		BtcPlateImg._srvReg = false;
		BtcPlateImg._cl = null;
		BtcPlateImg._annVer = -1;
		BtcPlateImg._hostVer = -1;
	}

	private static void EnsureFiles(bool force)
	{
		bool flag = !force && Time.unscaledTime - BtcPlateImg._filesAt < 5f;
		if (!flag)
		{
			BtcPlateImg._filesAt = Time.unscaledTime;
			try
			{
				Directory.CreateDirectory(BtcPlateImg.Dir);
				List<string> list = new List<string>();
				string[] files = Directory.GetFiles(BtcPlateImg.Dir);
				for (int i = 0; i < files.Length; i++)
				{
					string a = Path.GetExtension(files[i]).ToLowerInvariant();
					bool flag2 = a == ".png" || a == ".jpg" || a == ".jpeg" || a == ".gif";
					if (flag2)
					{
						list.Add(Path.GetFileName(files[i]));
					}
				}
				list.Sort(StringComparer.OrdinalIgnoreCase);
				BtcPlateImg._files = list.ToArray();
			}
			catch (Exception)
			{
				BtcPlateImg._files = new string[0];
			}
		}
	}

	public static float DrawUI(float x, float y, float w, float row, float gap, GUIStyle btn, GUIStyle label, Texture2D px)
	{
		BtcPlateImg.LoadCfg();
		BtcPlateImg.EnsureFiles(false);
		GUI.Label(new Rect(x, y, w, row * 2f), BtcPlateImg.T("Folder: BetterThanCastle/Plates (png, jpg, gif)", "Папка: BetterThanCastle/Plates (png, jpg, gif)"), label);
		y += row * 2f + gap * 0.5f;
		float num = (w - gap) * 0.5f;
		bool flag = GUI.Button(new Rect(x, y, num, row), BtcPlateImg.T("Open folder", "Открыть папку"), btn);
		if (flag)
		{
			try
			{
				Directory.CreateDirectory(BtcPlateImg.Dir);
				Application.OpenURL("file:///" + BtcPlateImg.Dir.Replace('\\', '/'));
			}
			catch (Exception)
			{
			}
		}
		bool flag2 = GUI.Button(new Rect(x + num + gap, y, num, row), BtcPlateImg.T("Refresh", "Обновить"), btn);
		if (flag2)
		{
			BtcPlateImg.EnsureFiles(true);
		}
		y += row + gap * 0.5f;
		float num2 = row * 3f;
		Color color = GUI.color;
		GUI.color = new Color(0f, 0f, 0f, 0.6f);
		GUI.DrawTexture(new Rect(x, y, w, num2), px);
		GUI.color = Color.white;
		bool flag3 = BtcPlateImg._local != null && BtcPlateImg._local.tex != null;
		if (flag3)
		{
			BtcPlateImg._local.used = Time.unscaledTime;
			GUI.DrawTexture(new Rect(x + 2f, y + 2f, w - 4f, num2 - 4f), BtcPlateImg._local.tex, ScaleMode.ScaleToFit, true);
		}
		y += num2 + gap * 0.5f;
		GUI.Label(new Rect(x, y, w, row * 2f), BtcPlateImg._status, label);
		y += row * 2f + gap * 0.5f;
		bool flag4 = GUI.Button(new Rect(x, y, w, row), BtcPlateImg.T("No image", "Без картинки"), btn);
		if (flag4)
		{
			BtcPlateImg.Select(string.Empty);
		}
		y += row + gap * 0.5f;
		float num3 = row + 2f;
		float num4 = row * 5f;
		BtcPlateImg._scroll = GUI.BeginScrollView(new Rect(x, y, w, num4), BtcPlateImg._scroll, new Rect(0f, 0f, w - 18f, (float)BtcPlateImg._files.Length * num3), false, true);
		for (int i = 0; i < BtcPlateImg._files.Length; i++)
		{
			bool flag5 = BtcPlateImg._files[i] == BtcPlateImg._file;
			GUI.color = (flag5 ? new Color(0.6f, 1f, 0.6f, 1f) : Color.white);
			bool flag6 = GUI.Button(new Rect(0f, (float)i * num3, w - 18f, row), BtcPlateImg._files[i], btn) && !flag5;
			if (flag6)
			{
				BtcPlateImg.Select(BtcPlateImg._files[i]);
			}
		}
		GUI.color = Color.white;
		GUI.EndScrollView();
		GUI.color = color;
		y += num4 + gap;
		return y;
	}

	public const int MaxW = 256;

	public const int MaxH = 128;

	public const int MaxFrames = 32;

	private const short MsgUp = 95;

	private const short MsgDown = 96;

	private const int ChunkBytes = 1000;

	private const int MaxChunks = 400;

	private const int PumpPerTick = 6;

	private const int MaxFileBytes = 41943040;

	private const int MaxDecodeFrames = 300;

	private static bool _cfgLoaded;

	private static string _file = string.Empty;

	private static BtcPlateImg.Anim _local;

	private static byte[] _packed;

	private static int _ver;

	private static string _status = string.Empty;

	private static bool _justSelected;

	private static volatile bool _pendReady;

	private static byte[] _pendPacked;

	private static string _pendErr;

	private static string _pendName;

	private static int _loadToken;

	private static readonly Dictionary<uint, BtcPlateImg.Rx> _srv = new Dictionary<uint, BtcPlateImg.Rx>();

	private static readonly Dictionary<uint, BtcPlateImg.Rx> _view = new Dictionary<uint, BtcPlateImg.Rx>();

	private static readonly List<int> _mods = new List<int>();

	private static readonly Queue<KeyValuePair<int, BtcPlateImg.ImgMsg>> _sOut = new Queue<KeyValuePair<int, BtcPlateImg.ImgMsg>>();

	private static readonly Queue<BtcPlateImg.ImgMsg> _cOut = new Queue<BtcPlateImg.ImgMsg>();

	private static bool _srvReg;

	private static NetworkClient _cl;

	private static int _annVer = -1;

	private static int _hostVer = -1;

	private static bool _hadNet;

	private static float _pruneAt;

	private static int _lastFrame = -1;

	private static string[] _files = new string[0];

	private static float _filesAt = -100f;

	private static Vector2 _scroll;

	private static HashSet<uint> _deadPrev = new HashSet<uint>();

	public sealed class Anim
	{
		public Texture2D tex;

		public int w;

		public int h;

		public byte[][] idx;

		public Color32[] pal;

		public int[] delayMs;

		public Color32[] buf;

		public int cur;

		public float next;

		public float used;
	}

	private sealed class Rx
	{
		public int ver = -1;

		public int count;

		public int got;

		public byte[][] parts;

		public BtcPlateImg.Anim anim;
	}

	public class ImgMsg : MessageBase
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
