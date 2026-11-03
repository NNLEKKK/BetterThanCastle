using System;

namespace BetterThanCastle
{
public static class BtcLzma
{
	public static byte PropsByte(int lc, int lp, int pb)
	{
		return (byte)((pb * 5 + lp) * 9 + lc);
	}

	public static void ParseProps(byte b, out int lc, out int lp, out int pb)
	{
		lc = (int)(b % 9);
		int num = (int)(b / 9);
		lp = num % 5;
		pb = num / 5;
	}

	private static int StLit(int s)
	{
		return (s < 4) ? 0 : ((s < 10) ? (s - 3) : (s - 6));
	}

	private static int StMatch(int s)
	{
		return (s < 7) ? 7 : 10;
	}

	private static int StRep(int s)
	{
		return (s < 7) ? 8 : 11;
	}

	private static int StShort(int s)
	{
		return (s < 7) ? 9 : 11;
	}

	private static int PosSlotOf(uint dist)
	{
		bool flag = dist < 4U;
		int result;
		if (flag)
		{
			result = (int)dist;
		}
		else
		{
			int num = 31;
			while ((dist >> num & 1U) == 0U)
			{
				num--;
			}
			result = (num << 1 | (int)(dist >> num - 1 & 1U));
		}
		return result;
	}

	private static void TreeEnc(BtcLzma.Rc rc, ushort[] probs, int bas, int nbits, int sym)
	{
		int num = 1;
		for (int i = nbits - 1; i >= 0; i--)
		{
			int num2 = sym >> i & 1;
			rc.Bit(probs, bas + num, num2);
			num = (num << 1 | num2);
		}
	}

	private static void RTreeEnc(BtcLzma.Rc rc, ushort[] probs, int bas, int nbits, uint sym)
	{
		int num = 1;
		for (int i = 0; i < nbits; i++)
		{
			int num2 = (int)(sym & 1U);
			rc.Bit(probs, bas + num, num2);
			num = (num << 1 | num2);
			sym >>= 1;
		}
	}

	private static void LenEnc(BtcLzma.Rc rc, ushort[] choice, ushort[] low, ushort[] mid, ushort[] high, int l, int posState)
	{
		l -= 2;
		bool flag = l < 8;
		if (flag)
		{
			rc.Bit(choice, 0, 0);
			BtcLzma.TreeEnc(rc, low, posState << 3, 3, l);
		}
		else
		{
			rc.Bit(choice, 0, 1);
			l -= 8;
			bool flag2 = l < 8;
			if (flag2)
			{
				rc.Bit(choice, 1, 0);
				BtcLzma.TreeEnc(rc, mid, posState << 3, 3, l);
			}
			else
			{
				rc.Bit(choice, 1, 1);
				BtcLzma.TreeEnc(rc, high, 0, 8, l - 8);
			}
		}
	}

	public static byte[] Encode(byte[] data, int lc, int lp, int pb, int depth)
	{
		int num = data.Length;
		BtcLzma.Model model = new BtcLzma.Model(lc, lp);
		BtcLzma.Rc rc = new BtcLzma.Rc();
		int num2 = (1 << pb) - 1;
		int lpMask = (1 << lp) - 1;
		int num3 = 0;
		uint num4 = 0U;
		int[] array = new int[65536];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = -1;
		}
		int[] prev = new int[(num > 0) ? num : 1];
		int num13;
		for (int j = 0; j < num; j += num13)
		{
			int num5 = j & num2;
			int num6 = 0;
			bool flag = j > 0 && (long)j - (long)((ulong)num4) - 1L >= 0L;
			if (flag)
			{
				int num7 = (int)((long)j - (long)((ulong)num4) - 1L);
				int num8 = Math.Min(273, num - j);
				while (num6 < num8 && data[num7 + num6] == data[j + num6])
				{
					num6++;
				}
			}
			int num9 = 0;
			uint num10 = 0U;
			BtcLzma.FindMatch(data, num, array, prev, j, depth, out num9, out num10);
			bool flag2 = num6 >= 2 && num6 + 1 >= num9;
			bool flag3 = num9 < 3 && (num9 != 2 || num10 >= 128U);
			if (flag3)
			{
				num9 = 0;
			}
			bool flag4 = !flag2 && num9 >= 3 && j + 1 < num;
			if (flag4)
			{
				int num11;
				uint num12;
				BtcLzma.FindMatch(data, num, array, prev, j + 1, depth, out num11, out num12);
				bool flag5 = num11 > num9 + 1 && num11 >= 3;
				if (flag5)
				{
					num9 = 0;
				}
			}
			bool flag6 = flag2;
			if (flag6)
			{
				num13 = num6;
				rc.Bit(model.isMatch, (num3 << 4) + num5, 1);
				rc.Bit(model.isRep, num3, 1);
				rc.Bit(model.isRepG0, num3, 0);
				rc.Bit(model.isRep0Long, (num3 << 4) + num5, 1);
				BtcLzma.LenEnc(rc, model.rlenChoice, model.rlenLow, model.rlenMid, model.rlenHigh, num13, num5);
				num3 = BtcLzma.StRep(num3);
			}
			else
			{
				bool flag7 = num9 > 0;
				if (flag7)
				{
					num13 = num9;
					rc.Bit(model.isMatch, (num3 << 4) + num5, 1);
					rc.Bit(model.isRep, num3, 0);
					BtcLzma.LenEnc(rc, model.lenChoice, model.lenLow, model.lenMid, model.lenHigh, num13, num5);
					BtcLzma.EncDist(rc, model, num10, num13);
					num4 = num10;
					num3 = BtcLzma.StMatch(num3);
				}
				else
				{
					BtcLzma.EncLiteral(rc, model, data, j, num3, num4, lc, lp, lpMask, num2);
					num3 = BtcLzma.StLit(num3);
					num13 = 1;
				}
			}
			for (int k = 0; k < num13; k++)
			{
				BtcLzma.InsertPos(data, num, array, prev, j + k);
			}
		}
		return rc.Flush();
	}

	private static int Hash(byte[] d, int i)
	{
		return ((int)d[i] << 8 ^ (int)d[i + 1] << 4 ^ (int)d[i + 2] * 2533 ^ (int)d[i + 2] << 11) & 65535;
	}

	private static void InsertPos(byte[] d, int n, int[] head, int[] prev, int i)
	{
		bool flag = i + 2 < n;
		if (flag)
		{
			int num = BtcLzma.Hash(d, i);
			prev[i] = head[num];
			head[num] = i;
		}
	}

	private static void FindMatch(byte[] d, int n, int[] head, int[] prev, int i, int depth, out int bestLen, out uint bestDist)
	{
		bestLen = 0;
		bestDist = 0U;
		bool flag = i + 2 >= n;
		if (!flag)
		{
			int num = Math.Min(273, n - i);
			int num2 = head[BtcLzma.Hash(d, i)];
			int num3 = depth;
			while (num2 >= 0 && num3 > 0)
			{
				bool flag2 = bestLen < num && d[num2 + bestLen] == d[i + bestLen];
				if (flag2)
				{
					int num4 = 0;
					while (num4 < num && d[num2 + num4] == d[i + num4])
					{
						num4++;
					}
					bool flag3 = num4 > bestLen;
					if (flag3)
					{
						bestLen = num4;
						bestDist = (uint)(i - num2 - 1);
						bool flag4 = num4 >= num;
						if (flag4)
						{
							break;
						}
					}
				}
				num2 = prev[num2];
				num3--;
			}
		}
	}

	private static void EncLiteral(BtcLzma.Rc rc, BtcLzma.Model P, byte[] data, int i, int state, uint rep0, int lc, int lp, int lpMask, int pbMask)
	{
		int num = i & pbMask;
		rc.Bit(P.isMatch, (state << 4) + num, 0);
		int num2 = (int)((i > 0) ? data[i - 1] : 0);
		int num3 = 768 * (((i & lpMask) << lc) + (num2 >> 8 - lc));
		int num4 = (int)data[i];
		bool flag = state < 7;
		if (flag)
		{
			BtcLzma.TreeEnc(rc, P.literal, num3, 8, num4);
		}
		else
		{
			int num5 = (int)data[(int)((long)i - (long)((ulong)rep0) - 1L)];
			int num6 = 1;
			bool flag2 = true;
			for (int j = 7; j >= 0; j--)
			{
				int num7 = num4 >> j & 1;
				int num8 = num6;
				bool flag3 = flag2;
				if (flag3)
				{
					int num9 = num5 >> j & 1;
					num8 += 1 + num9 << 8;
					flag2 = (num9 == num7);
				}
				rc.Bit(P.literal, num3 + num8, num7);
				num6 = (num6 << 1 | num7);
			}
		}
	}

	private static void EncDist(BtcLzma.Rc rc, BtcLzma.Model P, uint dist, int ln)
	{
		int num = BtcLzma.PosSlotOf(dist);
		int num2 = Math.Min(ln - 2, 3);
		BtcLzma.TreeEnc(rc, P.posSlot, num2 << 6, 6, num);
		bool flag = num >= 4;
		if (flag)
		{
			int num3 = (num >> 1) - 1;
			uint num4 = (uint)((uint)(2 | (num & 1)) << num3);
			uint num5 = dist - num4;
			bool flag2 = num < 14;
			if (flag2)
			{
				BtcLzma.RTreeEnc(rc, P.posSpecial, (int)(num4 - (uint)num - 1U), num3, num5);
			}
			else
			{
				rc.Direct(num5 >> 4, num3 - 4);
				BtcLzma.RTreeEnc(rc, P.align, 0, 4, num5 & 15U);
			}
		}
	}

	private static int TreeDec(BtcLzma.Rd rc, ushort[] probs, int bas, int nbits)
	{
		int num = 1;
		for (int i = 0; i < nbits; i++)
		{
			num = (num << 1 | rc.Bit(probs, bas + num));
		}
		return num - (1 << nbits);
	}

	private static uint RTreeDec(BtcLzma.Rd rc, ushort[] probs, int bas, int nbits)
	{
		int num = 1;
		uint num2 = 0U;
		for (int i = 0; i < nbits; i++)
		{
			int num3 = rc.Bit(probs, bas + num);
			num = (num << 1 | num3);
			num2 |= (uint)((uint)num3 << i);
		}
		return num2;
	}

	private static int LenDec(BtcLzma.Rd rc, ushort[] choice, ushort[] low, ushort[] mid, ushort[] high, int posState)
	{
		bool flag = rc.Bit(choice, 0) == 0;
		int result;
		if (flag)
		{
			result = 2 + BtcLzma.TreeDec(rc, low, posState << 3, 3);
		}
		else
		{
			bool flag2 = rc.Bit(choice, 1) == 0;
			if (flag2)
			{
				result = 10 + BtcLzma.TreeDec(rc, mid, posState << 3, 3);
			}
			else
			{
				result = 18 + BtcLzma.TreeDec(rc, high, 0, 8);
			}
		}
		return result;
	}

	public static byte[] Decode(byte[] src, int srcOffset, int outSize, int lc, int lp, int pb)
	{
		byte[] result;
		try
		{
			BtcLzma.Model model = new BtcLzma.Model(lc, lp);
			BtcLzma.Rd rd = new BtcLzma.Rd(src, srcOffset);
			byte[] array = new byte[outSize];
			int num = (1 << pb) - 1;
			int num2 = (1 << lp) - 1;
			int num3 = 0;
			uint num4 = 0U;
			uint num5 = 0U;
			uint num6 = 0U;
			uint num7 = 0U;
			int i = 0;
			while (i < outSize)
			{
				int num8 = i & num;
				bool flag = rd.Bit(model.isMatch, (num3 << 4) + num8) == 0;
				if (flag)
				{
					int num9 = (int)((i > 0) ? array[i - 1] : 0);
					int num10 = 768 * (((i & num2) << lc) + (num9 >> 8 - lc));
					int j = 1;
					bool flag2 = num3 >= 7;
					if (flag2)
					{
						long num11 = (long)i - (long)((ulong)num4) - 1L;
						bool flag3 = num11 < 0L;
						if (flag3)
						{
							return null;
						}
						int num12 = (int)array[(int)num11];
						while (j < 256)
						{
							int num13 = num12 >> 7 & 1;
							num12 = (num12 << 1 & 255);
							int num14 = rd.Bit(model.literal, num10 + (1 + num13 << 8) + j);
							j = (j << 1 | num14);
							bool flag4 = num13 != num14;
							if (flag4)
							{
								break;
							}
						}
					}
					while (j < 256)
					{
						j = (j << 1 | rd.Bit(model.literal, num10 + j));
					}
					array[i++] = (byte)j;
					num3 = BtcLzma.StLit(num3);
				}
				else
				{
					bool flag5 = rd.Bit(model.isRep, num3) != 0;
					int num17;
					if (flag5)
					{
						bool flag6 = rd.Bit(model.isRepG0, num3) == 0;
						if (flag6)
						{
							bool flag7 = rd.Bit(model.isRep0Long, (num3 << 4) + num8) == 0;
							if (flag7)
							{
								long num15 = (long)i - (long)((ulong)num4) - 1L;
								bool flag8 = num15 < 0L;
								if (flag8)
								{
									return null;
								}
								array[i] = array[(int)num15];
								i++;
								num3 = BtcLzma.StShort(num3);
								continue;
							}
						}
						else
						{
							bool flag9 = rd.Bit(model.isRepG1, num3) == 0;
							uint num16;
							if (flag9)
							{
								num16 = num5;
							}
							else
							{
								bool flag10 = rd.Bit(model.isRepG2, num3) == 0;
								if (flag10)
								{
									num16 = num6;
								}
								else
								{
									num16 = num7;
									num7 = num6;
								}
								num6 = num5;
							}
							num5 = num4;
							num4 = num16;
						}
						num17 = BtcLzma.LenDec(rd, model.rlenChoice, model.rlenLow, model.rlenMid, model.rlenHigh, num8);
						num3 = BtcLzma.StRep(num3);
					}
					else
					{
						num7 = num6;
						num6 = num5;
						num5 = num4;
						num17 = BtcLzma.LenDec(rd, model.lenChoice, model.lenLow, model.lenMid, model.lenHigh, num8);
						num3 = BtcLzma.StMatch(num3);
						int num18 = Math.Min(num17 - 2, 3);
						int num19 = BtcLzma.TreeDec(rd, model.posSlot, num18 << 6, 6);
						bool flag11 = num19 < 4;
						uint num20;
						if (flag11)
						{
							num20 = (uint)num19;
						}
						else
						{
							int num21 = (num19 >> 1) - 1;
							num20 = (uint)((uint)(2 | (num19 & 1)) << num21);
							bool flag12 = num19 < 14;
							if (flag12)
							{
								num20 += BtcLzma.RTreeDec(rd, model.posSpecial, (int)(num20 - (uint)num19 - 1U), num21);
							}
							else
							{
								num20 += rd.Direct(num21 - 4) << 4;
								num20 += BtcLzma.RTreeDec(rd, model.align, 0, 4);
							}
						}
						num4 = num20;
					}
					long num22 = (long)i - (long)((ulong)num4) - 1L;
					bool flag13 = num22 < 0L;
					if (flag13)
					{
						return null;
					}
					int num23 = 0;
					while (num23 < num17 && i < outSize)
					{
						array[i] = array[(int)(num22 + (long)num23)];
						i++;
						num23++;
					}
				}
			}
			result = array;
		}
		catch (Exception)
		{
			result = null;
		}
		return result;
	}

	private const int NumStates = 12;

	private const uint Top = 16777216U;

	private const int MatchMin = 2;

	private const int MatchMax = 273;

	private sealed class Model
	{
		public Model(int lc, int lp)
		{
			this.lc = lc;
			this.lp = lp;
			this.isMatch = BtcLzma.Model.New(192);
			this.isRep = BtcLzma.Model.New(12);
			this.isRepG0 = BtcLzma.Model.New(12);
			this.isRepG1 = BtcLzma.Model.New(12);
			this.isRepG2 = BtcLzma.Model.New(12);
			this.isRep0Long = BtcLzma.Model.New(192);
			this.posSlot = BtcLzma.Model.New(256);
			this.posSpecial = BtcLzma.Model.New(115);
			this.align = BtcLzma.Model.New(16);
			this.literal = BtcLzma.Model.New(768 << lc + lp);
			this.lenChoice = BtcLzma.Model.New(2);
			this.lenLow = BtcLzma.Model.New(128);
			this.lenMid = BtcLzma.Model.New(128);
			this.lenHigh = BtcLzma.Model.New(256);
			this.rlenChoice = BtcLzma.Model.New(2);
			this.rlenLow = BtcLzma.Model.New(128);
			this.rlenMid = BtcLzma.Model.New(128);
			this.rlenHigh = BtcLzma.Model.New(256);
		}

		private static ushort[] New(int n)
		{
			ushort[] array = new ushort[n];
			for (int i = 0; i < n; i++)
			{
				array[i] = 1024;
			}
			return array;
		}

		public int lc;

		public int lp;

		public ushort[] isMatch;

		public ushort[] isRep;

		public ushort[] isRepG0;

		public ushort[] isRepG1;

		public ushort[] isRepG2;

		public ushort[] isRep0Long;

		public ushort[] posSlot;

		public ushort[] posSpecial;

		public ushort[] align;

		public ushort[] literal;

		public ushort[] lenChoice;

		public ushort[] lenLow;

		public ushort[] lenMid;

		public ushort[] lenHigh;

		public ushort[] rlenChoice;

		public ushort[] rlenLow;

		public ushort[] rlenMid;

		public ushort[] rlenHigh;
	}

	private sealed class Rc
	{
		private void Put(byte b)
		{
			bool flag = this.len == this.buf.Length;
			if (flag)
			{
				Array.Resize<byte>(ref this.buf, this.buf.Length * 2);
			}
			byte[] array = this.buf;
			int num = this.len;
			this.len = num + 1;
			array[num] = b;
		}

		private void ShiftLow()
		{
			bool flag = this.low < unchecked((ulong)-16777216) || this.low >= 4294967296UL;
			if (flag)
			{
				byte b = (byte)(this.low >> 32);
				byte maxValue = this.cache;
				long num;
				do
				{
					this.Put((byte)(maxValue + b));
					maxValue = byte.MaxValue;
					num = this.cacheSize - 1L;
					this.cacheSize = num;
				}
				while (num != 0L);
				this.cache = (byte)(this.low >> 24 & 255UL);
			}
			this.cacheSize += 1L;
			this.low = (this.low & 16777215UL) << 8;
		}

		public void Bit(ushort[] probs, int idx, int b)
		{
			uint num = (uint)probs[idx];
			uint num2 = (this.range >> 11) * num;
			bool flag = b == 0;
			if (flag)
			{
				this.range = num2;
				probs[idx] = (ushort)(num + (2048U - num >> 5));
			}
			else
			{
				this.low += (ulong)num2;
				this.range -= num2;
				probs[idx] = (ushort)(num - (num >> 5));
			}
			while (this.range < 16777216U)
			{
				this.range <<= 8;
				this.ShiftLow();
			}
		}

		public void Direct(uint value, int nbits)
		{
			for (int i = nbits - 1; i >= 0; i--)
			{
				this.range >>= 1;
				bool flag = (value >> i & 1U) > 0U;
				if (flag)
				{
					this.low += (ulong)this.range;
				}
				while (this.range < 16777216U)
				{
					this.range <<= 8;
					this.ShiftLow();
				}
			}
		}

		public byte[] Flush()
		{
			for (int i = 0; i < 5; i++)
			{
				this.ShiftLow();
			}
			byte[] array = new byte[this.len];
			Buffer.BlockCopy(this.buf, 0, array, 0, this.len);
			return array;
		}

		public ulong low;

		public uint range = uint.MaxValue;

		public byte cache;

		public long cacheSize = 1L;

		public byte[] buf = new byte[65536];

		public int len;
	}

	private sealed class Rd
	{
		public Rd(byte[] b, int p)
		{
			this.buf = b;
			this.pos = p;
			for (int i = 0; i < 5; i++)
			{
				this.code = (this.code << 8 | this.Next());
			}
		}

		private uint Next()
		{
			uint result = (uint)((this.pos < this.buf.Length) ? this.buf[this.pos] : 0);
			this.pos++;
			return result;
		}

		public int Bit(ushort[] probs, int idx)
		{
			uint num = (uint)probs[idx];
			uint num2 = (this.range >> 11) * num;
			bool flag = this.code < num2;
			int result;
			if (flag)
			{
				this.range = num2;
				probs[idx] = (ushort)(num + (2048U - num >> 5));
				result = 0;
			}
			else
			{
				this.code -= num2;
				this.range -= num2;
				probs[idx] = (ushort)(num - (num >> 5));
				result = 1;
			}
			bool flag2 = this.range < 16777216U;
			if (flag2)
			{
				this.range <<= 8;
				this.code = (this.code << 8 | this.Next());
			}
			return result;
		}

		public uint Direct(int nbits)
		{
			uint num = 0U;
			for (int i = 0; i < nbits; i++)
			{
				this.range >>= 1;
				bool flag = this.code >= this.range;
				uint num2;
				if (flag)
				{
					this.code -= this.range;
					num2 = 1U;
				}
				else
				{
					num2 = 0U;
				}
				num = (num << 1 | num2);
				bool flag2 = this.range < 16777216U;
				if (flag2)
				{
					this.range <<= 8;
					this.code = (this.code << 8 | this.Next());
				}
			}
			return num;
		}

		public byte[] buf;

		public int pos;

		public uint range = uint.MaxValue;

		public uint code;
	}
}
}
