using System;
using System.IO;
using System.IO.Compression;

namespace BetterThanCastle
{
public static class BtcPng
{
	private static uint[] MakeCrcTable()
	{
		uint[] array = new uint[256];
		for (uint num = 0U; num < 256U; num += 1U)
		{
			uint num2 = num;
			for (int i = 0; i < 8; i++)
			{
				num2 = (((num2 & 1U) != 0U) ? (3988292384U ^ num2 >> 1) : (num2 >> 1));
			}
			array[(int)num] = num2;
		}
		return array;
	}

	private static uint Crc(byte[] buf, int off, int len)
	{
		uint num = uint.MaxValue;
		for (int i = 0; i < len; i++)
		{
			num = (BtcPng.crcTable[(int)((num ^ (uint)buf[off + i]) & 255U)] ^ num >> 8);
		}
		return num ^ uint.MaxValue;
	}

	private static uint Be32(byte[] b, int o)
	{
		return (uint)((int)b[o] << 24 | (int)b[o + 1] << 16 | (int)b[o + 2] << 8 | (int)b[o + 3]);
	}

	private static void PutBe32(Stream s, uint v)
	{
		s.WriteByte((byte)(v >> 24));
		s.WriteByte((byte)(v >> 16));
		s.WriteByte((byte)(v >> 8));
		s.WriteByte((byte)v);
	}

	private static int Paeth(int a, int b, int c)
	{
		int num = a + b - c;
		int num2 = Math.Abs(num - a);
		int num3 = Math.Abs(num - b);
		int num4 = Math.Abs(num - c);
		bool flag = num2 <= num3 && num2 <= num4;
		int result;
		if (flag)
		{
			result = a;
		}
		else
		{
			bool flag2 = num3 <= num4;
			if (flag2)
			{
				result = b;
			}
			else
			{
				result = c;
			}
		}
		return result;
	}

	public static bool IsPng(byte[] b)
	{
		bool flag = b == null || b.Length < 33;
		bool result;
		if (flag)
		{
			result = false;
		}
		else
		{
			for (int i = 0; i < 8; i++)
			{
				bool flag2 = b[i] != BtcPng.Sig[i];
				if (flag2)
				{
					return false;
				}
			}
			result = true;
		}
		return result;
	}

	public static bool Decode(byte[] png, out int w, out int h, out byte[] rgba)
	{
		w = 0;
		h = 0;
		rgba = null;
		bool result;
		try
		{
			bool flag = !BtcPng.IsPng(png);
			if (flag)
			{
				result = false;
			}
			else
			{
				int num = 8;
				MemoryStream memoryStream = new MemoryStream();
				byte[] array = null;
				byte[] array2 = null;
				int num2 = 0;
				int num3 = 0;
				int num4 = 0;
				while (num + 8 <= png.Length)
				{
					int num5 = (int)BtcPng.Be32(png, num);
					bool flag2 = num5 < 0 || num + 12 + num5 > png.Length;
					if (flag2)
					{
						return false;
					}
					char c = (char)png[num + 4];
					string str = c.ToString();
					c = (char)png[num + 5];
					string str2 = c.ToString();
					c = (char)png[num + 6];
					string str3 = c.ToString();
					c = (char)png[num + 7];
					string a = str + str2 + str3 + c.ToString();
					int num6 = num + 8;
					num += 12 + num5;
					bool flag3 = a == "IHDR";
					if (flag3)
					{
						bool flag4 = num5 < 13;
						if (flag4)
						{
							return false;
						}
						w = (int)BtcPng.Be32(png, num6);
						h = (int)BtcPng.Be32(png, num6 + 4);
						num2 = (int)png[num6 + 8];
						num3 = (int)png[num6 + 9];
						num4 = (int)png[num6 + 12];
					}
					else
					{
						bool flag5 = a == "PLTE";
						if (flag5)
						{
							array = new byte[num5];
							Buffer.BlockCopy(png, num6, array, 0, num5);
						}
						else
						{
							bool flag6 = a == "tRNS";
							if (flag6)
							{
								array2 = new byte[num5];
								Buffer.BlockCopy(png, num6, array2, 0, num5);
							}
							else
							{
								bool flag7 = a == "IDAT";
								if (flag7)
								{
									memoryStream.Write(png, num6, num5);
								}
								else
								{
									bool flag8 = a == "IEND";
									if (flag8)
									{
										break;
									}
								}
							}
						}
					}
				}
				bool flag9 = num2 != 8 || num4 != 0 || w <= 0 || h <= 0 || w > 16384 || h > 16384;
				if (flag9)
				{
					result = false;
				}
				else
				{
					bool flag10 = num3 != 0 && num3 != 2 && num3 != 3 && num3 != 4 && num3 != 6;
					if (flag10)
					{
						result = false;
					}
					else
					{
						bool flag11 = num3 == 3 && array == null;
						if (flag11)
						{
							result = false;
						}
						else
						{
							int num7 = (num3 == 0 || num3 == 3) ? 1 : ((num3 == 4) ? 2 : ((num3 == 2) ? 3 : 4));
							int num8 = w * num7;
							int num9 = (num8 + 1) * h;
							byte[] array3 = memoryStream.ToArray();
							bool flag12 = array3.Length < 3;
							if (flag12)
							{
								result = false;
							}
							else
							{
								byte[] array4 = new byte[num9];
								using (MemoryStream memoryStream2 = new MemoryStream(array3, 2, array3.Length - 2))
								{
									using (DeflateStream deflateStream = new DeflateStream(memoryStream2, CompressionMode.Decompress))
									{
										int i;
										int num10;
										for (i = 0; i < num9; i += num10)
										{
											num10 = deflateStream.Read(array4, i, num9 - i);
											bool flag13 = num10 <= 0;
											if (flag13)
											{
												break;
											}
										}
										bool flag14 = i < num9;
										if (flag14)
										{
											return false;
										}
									}
								}
								rgba = new byte[w * h * 4];
								byte[] array5 = new byte[num8];
								byte[] array6 = new byte[num8];
								int num11 = 0;
								for (int j = 0; j < h; j++)
								{
									int num12 = (int)array4[num11];
									num11++;
									Buffer.BlockCopy(array4, num11, array6, 0, num8);
									num11 += num8;
									bool flag15 = num12 > 4;
									if (flag15)
									{
										rgba = null;
										return false;
									}
									bool flag16 = num12 != 0;
									if (flag16)
									{
										for (int k = 0; k < num8; k++)
										{
											int num13 = (int)((k >= num7) ? array6[k - num7] : 0);
											int num14 = (int)array5[k];
											int c2 = (int)((k >= num7) ? array5[k - num7] : 0);
											bool flag17 = num12 == 1;
											int num15;
											if (flag17)
											{
												num15 = num13;
											}
											else
											{
												bool flag18 = num12 == 2;
												if (flag18)
												{
													num15 = num14;
												}
												else
												{
													bool flag19 = num12 == 3;
													if (flag19)
													{
														num15 = num13 + num14 >> 1;
													}
													else
													{
														num15 = BtcPng.Paeth(num13, num14, c2);
													}
												}
											}
											array6[k] = (byte)((int)array6[k] + num15);
										}
									}
									for (int l = 0; l < w; l++)
									{
										int num16 = (j * w + l) * 4;
										bool flag20 = num3 == 6;
										if (flag20)
										{
											rgba[num16] = array6[l * 4];
											rgba[num16 + 1] = array6[l * 4 + 1];
											rgba[num16 + 2] = array6[l * 4 + 2];
											rgba[num16 + 3] = array6[l * 4 + 3];
										}
										else
										{
											bool flag21 = num3 == 2;
											if (flag21)
											{
												rgba[num16] = array6[l * 3];
												rgba[num16 + 1] = array6[l * 3 + 1];
												rgba[num16 + 2] = array6[l * 3 + 2];
												rgba[num16 + 3] = byte.MaxValue;
												bool flag22 = array2 != null && array2.Length >= 6 && array6[l * 3] == array2[1] && array6[l * 3 + 1] == array2[3] && array6[l * 3 + 2] == array2[5];
												if (flag22)
												{
													rgba[num16 + 3] = 0;
												}
											}
											else
											{
												bool flag23 = num3 == 0;
												if (flag23)
												{
													byte b = array6[l];
													rgba[num16] = b;
													rgba[num16 + 1] = b;
													rgba[num16 + 2] = b;
													rgba[num16 + 3] = byte.MaxValue;
													bool flag24 = array2 != null && array2.Length >= 2 && b == array2[1];
													if (flag24)
													{
														rgba[num16 + 3] = 0;
													}
												}
												else
												{
													bool flag25 = num3 == 4;
													if (flag25)
													{
														byte b2 = array6[l * 2];
														rgba[num16] = b2;
														rgba[num16 + 1] = b2;
														rgba[num16 + 2] = b2;
														rgba[num16 + 3] = array6[l * 2 + 1];
													}
													else
													{
														int num17 = (int)array6[l];
														bool flag26 = num17 * 3 + 2 >= array.Length;
														if (flag26)
														{
															rgba = null;
															return false;
														}
														rgba[num16] = array[num17 * 3];
														rgba[num16 + 1] = array[num17 * 3 + 1];
														rgba[num16 + 2] = array[num17 * 3 + 2];
														rgba[num16 + 3] = ((array2 != null && num17 < array2.Length) ? array2[num17] : byte.MaxValue);
													}
												}
											}
										}
									}
									byte[] array7 = array5;
									array5 = array6;
									array6 = array7;
								}
								result = true;
							}
						}
					}
				}
			}
		}
		catch (Exception)
		{
			rgba = null;
			result = false;
		}
		return result;
	}

	private static void WriteChunk(Stream s, string type, byte[] data, int len)
	{
		BtcPng.PutBe32(s, (uint)len);
		byte[] array = new byte[4 + len];
		array[0] = (byte)type[0];
		array[1] = (byte)type[1];
		array[2] = (byte)type[2];
		array[3] = (byte)type[3];
		bool flag = len > 0;
		if (flag)
		{
			Buffer.BlockCopy(data, 0, array, 4, len);
		}
		s.Write(array, 0, array.Length);
		BtcPng.PutBe32(s, BtcPng.Crc(array, 0, array.Length));
	}

	private static uint Adler32(byte[] b, int len)
	{
		uint num = 1U;
		uint num2 = 0U;
		for (int i = 0; i < len; i++)
		{
			num = (num + (uint)b[i]) % 65521U;
			num2 = (num2 + num) % 65521U;
		}
		return num2 << 16 | num;
	}

	public static byte[] Encode(int w, int h, byte[] rgba, bool hasAlpha)
	{
		int num = hasAlpha ? 4 : 3;
		int num2 = w * num;
		byte[] array = new byte[(num2 + 1) * h];
		byte[] array2 = new byte[num2];
		byte[] array3 = new byte[num2];
		byte[][] array4 = new byte[5][];
		for (int i = 0; i < 5; i++)
		{
			array4[i] = new byte[num2];
		}
		int num3 = 0;
		for (int j = 0; j < h; j++)
		{
			for (int k = 0; k < w; k++)
			{
				int num4 = (j * w + k) * 4;
				for (int l = 0; l < num; l++)
				{
					array3[k * num + l] = rgba[num4 + l];
				}
			}
			int num5 = 0;
			long num6 = long.MaxValue;
			for (int m = 0; m < 5; m++)
			{
				byte[] array5 = array4[m];
				long num7 = 0L;
				for (int n = 0; n < num2; n++)
				{
					int num8 = (int)((n >= num) ? array3[n - num] : 0);
					int num9 = (int)array2[n];
					int c = (int)((n >= num) ? array2[n - num] : 0);
					int num10 = 0;
					bool flag = m == 1;
					if (flag)
					{
						num10 = num8;
					}
					else
					{
						bool flag2 = m == 2;
						if (flag2)
						{
							num10 = num9;
						}
						else
						{
							bool flag3 = m == 3;
							if (flag3)
							{
								num10 = num8 + num9 >> 1;
							}
							else
							{
								bool flag4 = m == 4;
								if (flag4)
								{
									num10 = BtcPng.Paeth(num8, num9, c);
								}
							}
						}
					}
					byte b = (byte)((int)array3[n] - num10);
					array5[n] = b;
					num7 += ((b < 128) ? ((long)b) : (256L - (long)b));
				}
				bool flag5 = num7 < num6;
				if (flag5)
				{
					num6 = num7;
					num5 = m;
				}
			}
			array[num3++] = (byte)num5;
			Buffer.BlockCopy(array4[num5], 0, array, num3, num2);
			num3 += num2;
			byte[] array6 = array2;
			array2 = array3;
			array3 = array6;
		}
		MemoryStream memoryStream = new MemoryStream();
		memoryStream.WriteByte(120);
		memoryStream.WriteByte(218);
		using (DeflateStream deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress, true))
		{
			deflateStream.Write(array, 0, array.Length);
		}
		BtcPng.PutBe32(memoryStream, BtcPng.Adler32(array, array.Length));
		byte[] array7 = memoryStream.ToArray();
		MemoryStream memoryStream2 = new MemoryStream();
		memoryStream2.Write(BtcPng.Sig, 0, 8);
		byte[] array8 = new byte[13];
		array8[0] = (byte)(w >> 24);
		array8[1] = (byte)(w >> 16);
		array8[2] = (byte)(w >> 8);
		array8[3] = (byte)w;
		array8[4] = (byte)(h >> 24);
		array8[5] = (byte)(h >> 16);
		array8[6] = (byte)(h >> 8);
		array8[7] = (byte)h;
		array8[8] = 8;
		array8[9] = (byte)(hasAlpha ? 6 : 2);
		BtcPng.WriteChunk(memoryStream2, "IHDR", array8, 13);
		BtcPng.WriteChunk(memoryStream2, "IDAT", array7, array7.Length);
		BtcPng.WriteChunk(memoryStream2, "IEND", null, 0);
		return memoryStream2.ToArray();
	}

	private static readonly uint[] crcTable = BtcPng.MakeCrcTable();

	private static readonly byte[] Sig = new byte[]
	{
		137,
		80,
		78,
		71,
		13,
		10,
		26,
		10
	};
}
}
