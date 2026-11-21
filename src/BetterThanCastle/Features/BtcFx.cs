using System;
using System.IO;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcFx
{
	public static int Mode
	{
		get
		{
			BtcFx.EnsureLoaded();
			return BtcFx._mode;
		}
		set
		{
			BtcFx.EnsureLoaded();
			int num = Mathf.Clamp(value, 0, 4);
			bool flag = num != BtcFx._mode;
			if (flag)
			{
				BtcFx._mode = num;
				BtcFx.Save();
			}
		}
	}

	private static void EnsureLoaded()
	{
		bool loaded = BtcFx._loaded;
		if (!loaded)
		{
			BtcFx._loaded = true;
			try
			{
				BtcPaths.Init();
				int value;
				bool flag = int.TryParse(BtcPaths.ReadKey("btc_fx.txt", "mode", "0"), out value);
				if (flag)
				{
					BtcFx._mode = Mathf.Clamp(value, 0, 4);
				}
			}
			catch (Exception)
			{
			}
		}
	}

	private static void Save()
	{
		try
		{
			File.WriteAllText(BtcPaths.Config("btc_fx.txt"), "mode=" + BtcFx._mode + "\r\n");
		}
		catch (Exception)
		{
		}
	}

	public static void SetBase(int[] cols)
	{
		BtcFx._base = ((cols != null) ? cols : new int[0]);
	}

	private static int Mix(int a, int b, float t)
	{
		t = Mathf.Clamp01(t);
		int num = Mathf.RoundToInt(Mathf.Lerp((float)(a >> 16 & 255), (float)(b >> 16 & 255), t));
		int num2 = Mathf.RoundToInt(Mathf.Lerp((float)(a >> 8 & 255), (float)(b >> 8 & 255), t));
		int num3 = Mathf.RoundToInt(Mathf.Lerp((float)(a & 255), (float)(b & 255), t));
		return num << 16 | num2 << 8 | num3;
	}

	private static float Hash01(long slot)
	{
		uint num = (uint)(slot ^ slot >> 32);
		num *= 2654435761U;
		num ^= num >> 15;
		num *= 2246822519U;
		num ^= num >> 13;
		return (num & 65535U) / 65535f;
	}

	private static int HueShift(int c, float d)
	{
		float num;
		float num2;
		float num3;
		Color.RGBToHSV(new Color((float)(c >> 16 & 255) / 255f, (float)(c >> 8 & 255) / 255f, (float)(c & 255) / 255f), out num, out num2, out num3);
		num = (num + d) % 1f;
		bool flag = num2 < 0.35f;
		if (flag)
		{
			num2 = 0.85f;
		}
		bool flag2 = num3 < 0.6f;
		if (flag2)
		{
			num3 = 0.9f;
		}
		Color color = Color.HSVToRGB(num, num2, num3);
		return Mathf.RoundToInt(color.r * 255f) << 16 | Mathf.RoundToInt(color.g * 255f) << 8 | Mathf.RoundToInt(color.b * 255f);
	}

	public static void Frame(int len, int k, float phase, float spread, int[] outCols)
	{
		int num = Mathf.Min(len, outCols.Length);
		bool flag = num <= 0;
		if (!flag)
		{
			bool flag2 = k < 1;
			if (flag2)
			{
				k = 1;
			}
			int[] @base = BtcFx._base;
			int mode = BtcFx.Mode;
			int num2 = 16777215;
			int num3 = 16777215;
			for (int i = 0; i < @base.Length; i++)
			{
				bool flag3 = @base[i] >= 0;
				if (flag3)
				{
					num2 = @base[i];
					break;
				}
			}
			for (int j = @base.Length - 1; j >= 0; j--)
			{
				bool flag4 = @base[j] >= 0;
				if (flag4)
				{
					num3 = @base[j];
					break;
				}
			}
			bool flag5 = num2 == num3;
			if (flag5)
			{
				num3 = BtcFx.HueShift(num2, 0.33f);
			}
			float t = 0f;
			bool flag6 = mode == 3;
			if (flag6)
			{
				float num4 = BtcFx.Hash01((long)Mathf.Floor(Time.unscaledTime * 9f));
				t = ((num4 < 0.1f) ? 0.7f : ((num4 < 0.22f) ? 0.4f : 0f));
			}
			float t2 = 0.5f - 0.5f * Mathf.Cos(phase * 6.2831855f);
			float num5 = Mathf.Max(1f, spread);
			for (int l = 0; l < num; l++)
			{
				int num6 = l / k;
				int num7 = Mathf.Min(num6 * k, (@base.Length != 0) ? (@base.Length - 1) : 0);
				int num8 = (@base.Length != 0 && @base[num7] >= 0) ? @base[num7] : 16777215;
				float num9 = Mathf.Clamp01(((float)num6 + 0.5f) * (float)k / (float)num);
				int num10 = num8;
				bool flag7 = mode == 1;
				if (flag7)
				{
					float num11 = Mathf.Sin((num9 * num5 - phase) * 6.2831855f);
					num10 = ((num11 >= 0f) ? BtcFx.Mix(num8, 16777215, num11 * 0.6f) : BtcFx.Mix(num8, 0, -num11 * 0.5f));
				}
				else
				{
					bool flag8 = mode == 2;
					if (flag8)
					{
						float num12 = phase / 0.55f;
						bool flag9 = num12 <= 1f;
						if (flag9)
						{
							float num13 = Mathf.Lerp(-0.25f, 1.25f, num12);
							float num14 = Mathf.Clamp01(1f - Mathf.Abs(num9 - num13) / 0.2f);
							num10 = BtcFx.Mix(num8, 16777215, num14 * num14 * 0.9f);
						}
					}
					else
					{
						bool flag10 = mode == 3;
						if (flag10)
						{
							num10 = BtcFx.Mix(num8, 0, t);
						}
						else
						{
							bool flag11 = mode == 4;
							if (flag11)
							{
								num10 = BtcFx.Mix(num2, num3, t2);
							}
						}
					}
				}
				outCols[l] = num10;
			}
		}
	}

	public static float DrawModes(float x, float y, float w, float row, float gap, GUIStyle btn, GUIStyle label)
	{
		GUI.Label(new Rect(x, y, w, row), BtcLang.Tr("Effect:"), label);
		y += row;
		string[] texts = new string[]
		{
			BtcLang.Tr("Gradient scroll"),
			BtcLang.Tr("Wave"),
			BtcLang.Tr("Shine"),
			BtcLang.Tr("Flicker"),
			BtcLang.Tr("Crossfade")
		};
		int mode = BtcFx.Mode;
		int num = GUI.SelectionGrid(new Rect(x, y, w, row * 2f + gap), mode, texts, 3, btn);
		bool flag = num != mode;
		if (flag)
		{
			BtcFx.Mode = num;
		}
		y += row * 2f + gap * 2f;
		bool flag2 = BtcFx.Mode != 0;
		if (flag2)
		{
			GUI.Label(new Rect(x, y, w, row * 2f), BtcLang.Tr("Effects use the colors of the applied nick (Colors / Gradients tabs). Speed, Updates/sec and Repeat apply to all of them."), label);
			y += row * 2f + gap;
		}
		return y;
	}

	private static bool _loaded;

	private static int _mode;

	private static int[] _base = new int[0];
}
}
