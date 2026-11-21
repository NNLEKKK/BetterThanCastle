using System;
using System.Collections.Generic;
using System.IO;

namespace BetterThanCastle
{
    /// <summary>GIF89a writer (no engine types, so it can be tested stand-alone): fixed r*g*b palette, optional ordered dither, optional "unchanged pixel = transparent" frames, LZW.</summary>
    internal static class BtcGifEncoder
    {
        /// <summary>Palette levels per channel (r,g,b); index = (r * lg + g) * lb + b, always below 255 so 255 can be the transparent entry.</summary>
        internal static readonly int[][] Levels = { new[] { 6, 7, 6 }, new[] { 5, 5, 5 }, new[] { 4, 4, 4 }, new[] { 3, 3, 3 } };
        internal static readonly int[] Colors = { 252, 125, 64, 27 };
        private static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        internal static byte Index(int r, int g, int b, int x, int y, int level, bool dither)
        {
            int[] lv = Levels[level];
            float d = dither ? (Bayer[(y & 3) * 4 + (x & 3)] - 7.5f) / 16f : 0f;
            return (byte)((Q(r, lv[0] - 1, d) * lv[1] + Q(g, lv[1] - 1, d)) * lv[2] + Q(b, lv[2] - 1, d));
        }

        private static int Q(int v, int max, float dither)
        {
            int q = (int)(v / 255f * max + dither + 0.5f);
            return q < 0 ? 0 : (q > max ? max : q);
        }

        /// <summary>frames = palette indices, top-down, w*h each.</summary>
        internal static void Write(Stream s, List<byte[]> frames, int w, int h, int fps, int level, bool diff)
        {
            int[] lv = Levels[level];
            s.Write(new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' }, 0, 6);
            s.Write(new byte[] { (byte)w, (byte)(w >> 8), (byte)h, (byte)(h >> 8), 0xF7, 0, 0 }, 0, 7);   // global palette, 256 entries
            byte[] pal = new byte[768];
            for (int r = 0; r < lv[0]; r++) for (int g = 0; g < lv[1]; g++) for (int b = 0; b < lv[2]; b++)
                    {
                        int i = ((r * lv[1] + g) * lv[2] + b) * 3;
                        pal[i] = (byte)(r * 255 / (lv[0] - 1)); pal[i + 1] = (byte)(g * 255 / (lv[1] - 1)); pal[i + 2] = (byte)(b * 255 / (lv[2] - 1));
                    }
            s.Write(pal, 0, 768);
            s.Write(new byte[] { 0x21, 0xFF, 0x0B, (byte)'N', (byte)'E', (byte)'T', (byte)'S', (byte)'C', (byte)'A', (byte)'P', (byte)'E', (byte)'2', (byte)'.', (byte)'0', 3, 1, 0, 0, 0 }, 0, 19);
            int delay = (int)(100f / fps + 0.5f);
            byte[] cur = new byte[w * h];
            for (int f = 0; f < frames.Count; f++)
            {
                byte[] src = frames[f];
                if (diff && f > 0)
                {
                    byte[] prev = frames[f - 1];
                    for (int i = 0; i < cur.Length; i++) cur[i] = src[i] == prev[i] ? (byte)255 : src[i];
                }
                else Buffer.BlockCopy(src, 0, cur, 0, cur.Length);
                s.Write(new byte[] { 0x21, 0xF9, 4, (byte)(diff ? 0x05 : 0x04), (byte)delay, (byte)(delay >> 8), 255, 0 }, 0, 8);   // do not dispose; transparent index 255
                s.Write(new byte[] { 0x2C, 0, 0, 0, 0, (byte)w, (byte)(w >> 8), (byte)h, (byte)(h >> 8), 0 }, 0, 10);
                Lzw(s, cur);
            }
            s.WriteByte(0x3B);
        }

        // GIF LZW, min code size 8, variable width up to 12 bits, table reset when full
        private static void Lzw(Stream s, byte[] data)
        {
            const int Clear = 256, Eoi = 257;
            byte[] block = new byte[255];
            int bl = 0;
            uint acc = 0; int nacc = 0;
            int bits = 9, maxcode = (1 << 9) - 1, free = Clear + 2;
            Dictionary<int, int> dict = new Dictionary<int, int>();
            s.WriteByte(8);
            Action<int> put = delegate (int code)
            {
                acc |= (uint)code << nacc; nacc += bits;
                while (nacc >= 8)
                {
                    block[bl++] = (byte)(acc & 0xFF); acc >>= 8; nacc -= 8;
                    if (bl == 255) { s.WriteByte(255); s.Write(block, 0, 255); bl = 0; }
                }
            };
            put(Clear);
            int ent = data[0];
            for (int i = 1; i < data.Length; i++)
            {
                int c = data[i], key = (ent << 8) | c, code;
                if (dict.TryGetValue(key, out code)) { ent = code; continue; }
                put(ent);
                if (free > maxcode && bits < 12) { bits++; maxcode = bits == 12 ? 4095 : (1 << bits) - 1; }
                if (free < 4096) dict[key] = free++;
                else
                {
                    put(Clear);
                    dict.Clear(); free = Clear + 2; bits = 9; maxcode = (1 << 9) - 1;
                }
                ent = c;
            }
            put(ent);
            if (free > maxcode && bits < 12) { bits++; }
            put(Eoi);
            if (nacc > 0) block[bl++] = (byte)(acc & 0xFF);
            if (bl > 0) { s.WriteByte((byte)bl); s.Write(block, 0, bl); }
            s.WriteByte(0);
        }
    }
}
