using System;
using Brix.Engine;
using Brix.Engine.FlatWorld;
using Brix.Engine.FlatWorld.SoftAlgos;
using Brix.Engine.TerrainAtlas;
using Brix.Utils.Collections;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Performance/ThreadedTerrain: the two heaviest whole-world passes of the terrain build (LevelConfigNormalUvs.LevelConfig and
    // NormalUvs, ~1/3 of the terrain load) run on several threads. Each voxel offset i reads only shared input arrays and writes
    // only its own output element i; the game's instance fields are per-voxel scratch, so every worker gets its own scratch and the
    // result is bit-identical (DEV check: BTC_TTERRAIN_VERIFY=1 runs the original afterwards and compares). The loop bodies below
    // are the vanilla loops with an index range. Small passes (terrain edits) and calls off the main thread stay vanilla.

    internal static class BtcTerrainThreads
    {
        internal const int MinCount = 8192;
        [ThreadStatic] internal static bool Bypass;
        internal static int Calls, Voxels;
        internal static long Ticks;

        internal static bool On
        {
            get
            {
#if DEV
                string ab = Environment.GetEnvironmentVariable("BTC_TTERRAIN"); // A/B runs: 0 = off, 1 = on
                if (ab == "0") return false; if (ab == "1") return true;
#endif
                return BtcConfig.ThreadedTerrain != null && BtcConfig.ThreadedTerrain.Value;
            }
        }

        /// <summary>" | threaded passes ..." summary for the load note since the last call (empty when nothing ran threaded).</summary>
        internal static string Stats()
        {
            string s = Calls == 0 ? (On ? " | threaded passes: none" : "") : " | threaded passes: " + Calls + " (" + Voxels + " voxels, " + BtcParallel.Workers + " threads) "
                + (Ticks * 1000 / System.Diagnostics.Stopwatch.Frequency) + " ms";
#if DEV
            if (Verify) s += " verify " + Checked + " outputs, mismatches " + Mismatch;
#endif
            Calls = Voxels = 0; Ticks = 0;
            return s;
        }

        private static readonly int[] IConfig = { 14, 16, 10, 22, 4, 12 };
        private static readonly int[] ILevel = { 7, 16, 25, 4, 13, 22, 1, 10, 19 };
        private static readonly int[] ITrans = { 14, 14, 14, 15, 16, 17, 9, 10, 11, 21, 22, 23, 3, 4, 5, 12, 12, 12, 12 };

        private sealed class Scratch
        {
            internal readonly int[] Rubik = new int[27], Volumes = new int[27], Types = new int[27], Lvs = new int[9];
            internal readonly Color32[] LvColors = new Color32[27];
            internal readonly Vector3[] Normals = new Vector3[9];
        }
        private static Scratch[] _scratch;

        private static Scratch[] Scratches()
        {
            int n = BtcParallel.PlannedWorkers;
            if (_scratch == null || _scratch.Length < n)
            {
                _scratch = new Scratch[n];
                for (int i = 0; i < n; i++) _scratch[i] = new Scratch();
            }
            return _scratch;
        }

        internal static void LevelConfig(ITexColors levelTex, ITexColors configTex, ITexColors columnTex, ITexColors xzTex, ITexColors offsetTex, ITexColors volumeTex, ITexColors smoothTex, int count)
        {
            Color32[] column = columnTex.GetPixels32(), xz = xzTex.GetPixels32(), offset = offsetTex.GetPixels32(), volume = volumeTex.GetPixels32(), smooth = smoothTex.GetPixels32();
            Color32[] level = Soft.GetOrAllocPixels32(levelTex, offsetTex, offset);
            Color32[] config = Soft.GetOrAllocPixels32(configTex, offsetTex, offset);
            int w = columnTex.Width, h = columnTex.Height;
            Scratch[] sc = Scratches();
            Run(count, (from, to, worker) =>
            {
                Scratch s = sc[worker];
                int[] rubik = s.Rubik, volumes = s.Volumes, lvs = s.Lvs;
                for (int i = from; i < to; i++)
                {
                    if (volume[i].r == 0) { level[i] = new Color32(0, 0, 0, 0); config[i] = new Color32(0, 0, 0, 0); continue; }
                    XYZ p = Soft.DecodeXYZ(Soft.DecodeInt(offset[i]));
                    Soft.GetOffsetRubik(rubik, p, xz, column, w, h);
                    for (int j = 0; j < 27; j++) volumes[j] = Soft.GetOffsetDataR(rubik[j], volume, 0, 5);
                    for (int k = 0; k < 9; k++)
                    {
                        int c = 1 + k % 3 * 3 + k / 3 * 9;
                        if (volumes[c] > 0 && volumes[c - 1] > 0 && volumes[c + 1] > 0) volumes[c] = 5;
                    }
                    if (smooth[i].r == 0) volumes[13] = 5;
                    int bits = 0;
                    for (int l = 0; l < IConfig.Length; l++) if (volumes[IConfig[l]] == 0) bits |= 1 << l;
                    bool empty14 = volumes[14] == 0;
                    bool empty12 = !empty14 && volumes[12] == 0;
                    for (int m = 1; m < 5; m++)
                    {
                        if (volumes[IConfig[m]] == 0) continue;
                        bool up = volumes[IConfig[m] + 1] == 0;
                        bool down = !up && volumes[IConfig[m] - 1] == 0;
                        if ((empty14 & down) || (empty12 & up)) bits |= 1 << m;
                    }
                    for (int n = 0; n < 9; n++) lvs[n] = volumes[ILevel[n]];
                    level[i] = new Color32(LevelConfigNormalUvs.ComputeAverageLevel(lvs[0], lvs[1], lvs[3], lvs[4]), LevelConfigNormalUvs.ComputeAverageLevel(lvs[1], lvs[2], lvs[4], lvs[5]),
                        LevelConfigNormalUvs.ComputeAverageLevel(lvs[3], lvs[4], lvs[6], lvs[7]), LevelConfigNormalUvs.ComputeAverageLevel(lvs[4], lvs[5], lvs[7], lvs[8]));
                    config[i] = new Color32((byte)bits, 0, 0, 0);
                }
            });
            if (levelTex != null) levelTex.SetPixels32(level, offsetTex.Width, offsetTex.Height);
            if (configTex != null) configTex.SetPixels32(config, offsetTex.Width, offsetTex.Height);
        }

        internal static void NormalUvs(ITexColors normalTex, ITexColors uvBaseTex, ITexColors uvTransTex, ITexColors uvSpecialTex, ITexColors columnTex, ITexColors xzTex, ITexColors offsetTex, ITexColors typeTex, ITexColors volumeTex, ITexColors levelTex, ITexColors noiseTex, XYZ worldStart, int count)
        {
            Color32[] column = columnTex.GetPixels32(), xz = xzTex.GetPixels32(), offset = offsetTex.GetPixels32(), type = typeTex.GetPixels32(),
                volume = volumeTex.GetPixels32(), levels = levelTex.GetPixels32(), noise = noiseTex.GetPixels32();
            Color32[] normal = Soft.GetOrAllocPixels32(normalTex, offsetTex, offset);
            Color32[] uvBase = Soft.GetOrAllocPixels32(uvBaseTex, offsetTex, offset);
            Color32[] uvTrans = Soft.GetOrAllocPixels32(uvTransTex, offsetTex, offset);
            Color32[] uvSpecial = Soft.GetOrAllocPixels32(uvSpecialTex, offsetTex, offset);
            MondeAtlas atlas = MondeAtlas.GetAtlas("Terrain_LOD0");
            atlas.GetUv(0);   // lazy Init() on the main thread, workers then only read
            int w = columnTex.Width, h = columnTex.Height, nw = noiseTex.Width, nh = noiseTex.Height;
            Scratch[] sc = Scratches();
            Color32 none = new Color32(0, 0, 0, 0), full = new Color32(255, 255, 255, 255);
            Run(count, (from, to, worker) =>
            {
                Scratch s = sc[worker];
                int[] rubik = s.Rubik, types = s.Types;
                Color32[] lvColors = s.LvColors;
                Vector3[] normals = s.Normals;
                for (int i = from; i < to; i++)
                {
                    XYZ p = Soft.DecodeXYZ(Soft.DecodeInt(offset[i]));
                    Soft.GetOffsetRubik(rubik, p, xz, column, w, h);
                    for (int j = 0; j < 27; j++) types[j] = Soft.GetOffsetDataR(rubik[j], type, 0, 255);
                    Color32 vol = volume[i];
                    if (vol.r > 0 && vol.r <= 4)
                    {
                        for (int k = 0; k < 9; k++)
                        {
                            int idx = ILevel[k];
                            int v = Soft.GetOffsetDataR(rubik[idx], volume, 0, 5);
                            int dy = v >= 5 ? 1 : (v == 0 ? -1 : 0);
                            Color32 lv = Soft.GetOffsetData(rubik[idx + dy], levels, none, full);
                            normals[k] = new Vector3(lv.b - lv.a - (lv.g - lv.r), 510f, lv.b - lv.r - (lv.g - lv.a));
                            int add = (dy + 1) * 3;
                            lvColors[k] = new Color32((byte)((lv.r + 42) / 85 + add), (byte)((lv.g + 42) / 85 + add), (byte)((lv.b + 42) / 85 + add), (byte)((lv.a + 42) / 85 + add));
                        }
                        normal[i] = new Color32(Soft.EncodeNormal(Avg(lvColors[4].r, lvColors[0], lvColors[1], lvColors[3], lvColors[4], normals[0], normals[1], normals[3], normals[4]).normalized),
                            Soft.EncodeNormal(Avg(lvColors[4].g, lvColors[1], lvColors[2], lvColors[4], lvColors[5], normals[1], normals[2], normals[4], normals[5]).normalized),
                            Soft.EncodeNormal(Avg(lvColors[4].b, lvColors[3], lvColors[4], lvColors[6], lvColors[7], normals[3], normals[4], normals[6], normals[7]).normalized),
                            Soft.EncodeNormal(Avg(lvColors[4].a, lvColors[4], lvColors[5], lvColors[7], lvColors[8], normals[4], normals[5], normals[7], normals[8]).normalized));
                    }
                    else normal[i] = new Color32(119, 119, 119, 119);
                    byte r = type[i].r;
                    Vector2 uv = atlas.GetUv(r);
                    XYZ q = p + worldStart;
                    int ni = Soft.RepeatIndex(q.x + 7 * q.y, nw) + Soft.RepeatIndex(q.z + 17 * q.y, nh) * nw;
                    int variant = !atlas.UsesWeightedNoise(r) ? noise[ni].r * 16 / 255 % 16 : NoiseVariant(noise[ni].r, atlas.GetNoiseRowWeights(r));
                    uvBase[i] = new Color32((byte)Mathf.RoundToInt(uv.x * 85f), (byte)Mathf.RoundToInt(uv.y * 85f), (byte)(variant % 4 * 85), (byte)(variant / 4 * 85));
                    if (atlas.HasUvSpecial(r))
                    {
                        Vector2 sp = atlas.GetUvSpecial(r);
                        uvTrans[i] = full;
                        uvSpecial[i] = new Color32((byte)Mathf.RoundToInt(sp.x * 85f), (byte)Mathf.RoundToInt(sp.y * 85f), (byte)(variant % 4 * 85), (byte)(variant / 4 * 85));
                        continue;
                    }
                    int layer = atlas.GetLayer(r);
                    SmallOrderedSubset_8from255 list = default(SmallOrderedSubset_8from255);
                    int bits = 0;
                    bool crust = atlas.IsCroute(r);
                    for (int l = 0; l < ITrans.Length; l++)
                    {
                        if (!crust)
                        {
                            int m3 = ITrans[l] % 3;
                            if (m3 == 0 || (m3 == 2 && !atlas.IsCroute(types[ITrans[l]]))) continue;
                        }
                        else if (!GoodBranching(lvColors, ITrans[l] / 9 - 1, ITrans[l] % 9 / 3 - 1)) continue;
                        int t = types[ITrans[l]];
                        if (t != r && atlas.HasUvTransition(t) && atlas.GetLayer(t) >= layer)
                        {
                            int at = list.GetOrAddIndexOf(t);
                            bits |= 1 << at * 8 + l / 3;
                        }
                    }
                    uvTrans[i] = TransColor(bits, list, atlas);
                    uvSpecial[i] = full;
                }
            });
            if (normalTex != null) normalTex.SetPixels32(normal, offsetTex.Width, offsetTex.Height);
            if (uvBaseTex != null) uvBaseTex.SetPixels32(uvBase, offsetTex.Width, offsetTex.Height);
            if (uvTransTex != null) uvTransTex.SetPixels32(uvTrans, offsetTex.Width, offsetTex.Height);
            if (uvSpecialTex != null) uvSpecialTex.SetPixels32(uvSpecial, offsetTex.Width, offsetTex.Height);
        }

        private static void Run(int count, BtcParallel.RangeBody body)
        {
            if (!BtcParallel.For(count, body)) body(0, count, 0);
        }

        private static Vector3 Avg(int y, Color32 l0, Color32 l1, Color32 l2, Color32 l3, Vector3 n0, Vector3 n1, Vector3 n2, Vector3 n3)
        {
            Vector3 v = Vector3.zero;
            if (l0.a == y) v += n0;
            if (l1.b == y) v += n1;
            if (l2.g == y) v += n2;
            if (l3.r == y) v += n3;
            return v;
        }

        private static int NoiseVariant(int noise, WeightedNoiseData w)
        {
            int sum = w.row0 + w.row1 + w.row2 + w.row3;
            int x = sum * (noise & 0x3F) / 64, row;
            if (x < w.row3) row = 3;
            else if ((x -= w.row3) < w.row2) row = 2;
            else if ((x -= w.row2) < w.row1) row = 1;
            else row = 0;
            return row * 4 + noise % 4;
        }

        // vanilla IsGoodBranching with d = (dx, ?, dz); only x/z are tested
        private static bool GoodBranching(Color32[] lvs, int dx, int dz)
        {
            Color32 c = lvs[4];
            if (dx == 1 && dz == 0) return c.g == lvs[5].r && c.a == lvs[5].b;
            if (dx == -1 && dz == 0) return c.r == lvs[3].g && c.b == lvs[3].a;
            if (dx == 0 && dz == 1) return c.r == lvs[1].b && c.g == lvs[1].a;
            if (dx == 0 && dz == -1) return c.b == lvs[7].r && c.a == lvs[7].g;
            return true;
        }

        private static Color32 TransColor(int bits, SmallOrderedSubset_8from255 list, MondeAtlas atlas)
        {
            Color32 c = new Color32(255, 255, 255, 255);
            if (bits == 0) return c;
            c.r = TransByte(bits, atlas.GetUvTransition(list[0])); bits >>= 8; if (bits == 0) return c;
            c.g = TransByte(bits, atlas.GetUvTransition(list[1])); bits >>= 8; if (bits == 0) return c;
            c.b = TransByte(bits, atlas.GetUvTransition(list[2])); bits >>= 8; if (bits == 0) return c;
            c.a = TransByte(bits, atlas.GetUvTransition(list[3]));
            return c;
        }

        private static byte TransByte(int bits, Vector2 uv)
        {
            int a = 1 - ((bits >> 1) & 1), b = 1 - ((bits >> 2) & 1), c = 1 - ((bits >> 3) & 1), d = 1 - ((bits >> 4) & 1);
            return (byte)(a | (c << 1) | (Mathf.RoundToInt(uv.x) << 2) | (b << 4) | (d << 5) | (Mathf.RoundToInt(uv.y) << 6));
        }

#if DEV
        // BTC_TTERRAIN_VERIFY=1: run the vanilla pass after ours and compare every output element
        internal static readonly bool Verify = Environment.GetEnvironmentVariable("BTC_TTERRAIN_VERIFY") == "1";
        internal static int Checked, Mismatch;

        internal static Color32[] Snap(ITexColors t) { Color32[] a = t == null ? null : t.GetPixels32(); return a == null ? null : (Color32[])a.Clone(); }

        internal static void Compare(string what, Color32[] mine, ITexColors t, int count)
        {
            if (mine == null || t == null) return;
            Color32[] van = t.GetPixels32();
            Checked++;
            for (int i = 0; i < count; i++)
            {
                Color32 a = mine[i], b = van[i];
                if (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a)
                {
                    if (Mismatch++ < 10) BtcLog.Warn("TerrainThreads", "MISMATCH " + what + " at " + i + "/" + count + ": " + a + " vs " + b);
                    return;
                }
            }
        }
#endif
    }

    [HarmonyPatch(typeof(LevelConfigNormalUvs), "LevelConfig")]
    internal static class TerrainThreadsLevelConfigPatch
    {
        private static bool Prefix(LevelConfigNormalUvs __instance, ITexColors levelTex, ITexColors configTex, ITexColors columnTex, ITexColors xzTex, ITexColors offsetTex, ITexColors volumeTex, ITexColors smoothTex, int count)
        {
            if (BtcTerrainThreads.Bypass || count < BtcTerrainThreads.MinCount || !BtcTerrainThreads.On) return true;
            try
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                BtcTerrainThreads.LevelConfig(levelTex, configTex, columnTex, xzTex, offsetTex, volumeTex, smoothTex, count);
                BtcTerrainThreads.Ticks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; BtcTerrainThreads.Calls++; BtcTerrainThreads.Voxels += count;
#if DEV
                if (BtcTerrainThreads.Verify)
                {
                    Color32[] a = BtcTerrainThreads.Snap(levelTex), b = BtcTerrainThreads.Snap(configTex);
                    BtcTerrainThreads.Bypass = true;
                    try { __instance.LevelConfig(levelTex, configTex, columnTex, xzTex, offsetTex, volumeTex, smoothTex, count); } finally { BtcTerrainThreads.Bypass = false; }
                    BtcTerrainThreads.Compare("level", a, levelTex, count); BtcTerrainThreads.Compare("config", b, configTex, count);
                }
#endif
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "TerrainThreads LevelConfig: " + e); return true; }
        }
    }

    [HarmonyPatch(typeof(LevelConfigNormalUvs), "NormalUvs")]
    internal static class TerrainThreadsNormalUvsPatch
    {
        private static bool Prefix(LevelConfigNormalUvs __instance, ITexColors normalTex, ITexColors uvBaseTex, ITexColors uvTransTex, ITexColors uvSpecialTex, ITexColors columnTex, ITexColors xzTex, ITexColors offsetTex, ITexColors typeTex, ITexColors volumeTex, ITexColors levelTex, ITexColors noiseTex, XYZ worldStart, int count)
        {
            if (BtcTerrainThreads.Bypass || count < BtcTerrainThreads.MinCount || !BtcTerrainThreads.On) return true;
            try
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                BtcTerrainThreads.NormalUvs(normalTex, uvBaseTex, uvTransTex, uvSpecialTex, columnTex, xzTex, offsetTex, typeTex, volumeTex, levelTex, noiseTex, worldStart, count);
                BtcTerrainThreads.Ticks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; BtcTerrainThreads.Calls++; BtcTerrainThreads.Voxels += count;
#if DEV
                if (BtcTerrainThreads.Verify)
                {
                    Color32[] a = BtcTerrainThreads.Snap(normalTex), b = BtcTerrainThreads.Snap(uvBaseTex), c = BtcTerrainThreads.Snap(uvTransTex), d = BtcTerrainThreads.Snap(uvSpecialTex);
                    BtcTerrainThreads.Bypass = true;
                    try { __instance.NormalUvs(normalTex, uvBaseTex, uvTransTex, uvSpecialTex, columnTex, xzTex, offsetTex, typeTex, volumeTex, levelTex, noiseTex, worldStart, count); } finally { BtcTerrainThreads.Bypass = false; }
                    BtcTerrainThreads.Compare("normal", a, normalTex, count); BtcTerrainThreads.Compare("uvBase", b, uvBaseTex, count);
                    BtcTerrainThreads.Compare("uvTrans", c, uvTransTex, count); BtcTerrainThreads.Compare("uvSpecial", d, uvSpecialTex, count);
                }
#endif
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "TerrainThreads NormalUvs: " + e); return true; }
        }
    }
}
