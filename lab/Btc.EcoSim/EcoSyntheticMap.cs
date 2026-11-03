using System;
using System.Collections.Generic;

namespace Btc.EcoSim
{
    /// <summary>Tiny deterministic fake map in eco-map.bin form, for selftest until a real ecodump exists.</summary>
    public static class EcoSyntheticMap
    {
        public static void Write(string path, int seed)
        {
            const int sx = 32, sy = 24, sz = 32;
            var terrain = new byte[sx * sy * sz];
            var block = new ushort[sx * sy * sz];
            var rnd = new Random(seed);
            for (int x = 0; x < sx; x++)
                for (int z = 0; z < sz; z++)
                {
                    int h = 8 + (int)(2 * Math.Sin(x * 0.3) + 2 * Math.Cos(z * 0.25));
                    for (int y = 0; y < h; y++)
                    {
                        byte t = y < h - 3 ? EcoTerrain.Stone : EcoTerrain.Earth;
                        if (y < h - 3 && rnd.Next(40) == 0) t = EcoTerrain.Iron;
                        else if (y < h - 3 && rnd.Next(60) == 0) t = EcoTerrain.OrangeCrystal;
                        terrain[(x * sz + z) * sy + y] = t;
                    }
                }
            block[(5 * sz + 5) * sy + 7] = 1;
            var meta = new List<string>();
            meta.Add("gp|" + GameplaySnapshot.Expected);
            meta.Add("blk|1|Wood_DoublePlank_AA|WoodenBlock");
            for (int i = 0; i < 12; i++)
            {
                int x = 2 + rnd.Next(sx - 4), z = 2 + rnd.Next(sz - 4);
                int y = 0; for (int yy = sy - 1; yy >= 0; yy--) if (terrain[(x * sz + z) * sy + yy] != 0) { y = yy + 1; break; }
                meta.Add("tree|" + (x + 100) + "|" + (y - 1 - 4) + "|" + (z + 200) + "|" + (i % 3));
            }
            meta.Add("nature|iron|108|6|214|1|40|var=0;max=40");
            for (int i = 0; i < 6; i++)
            {
                int ux = 14 + i, uz = 14, ut = 0;
                for (int yy = sy - 1; yy >= 0; yy--) if (terrain[(ux * sz + uz) * sy + yy] != 0) { ut = yy; break; }
                meta.Add("ent|" + (100 + i) + "|unit|Bricktron|" + (100 + ux) + "|" + (ut - 4) + "|" + (200 + uz) + "|0|");
            }
            meta.Add("ent|200|item|Log|16|10.5|16|0|qty=1");
            meta.Add("bp|Stockpile|Stockpile|WoodenBlock*4|0,0,0,1,0,0|");
            meta.Add("recipe|1|Planks|woodBlock*1|3|0|Wood_DoublePlank_AA|3");
            meta.Add("cap|Stockpile|40");
            meta.Add("param|gravity|9.81");
            EcoMapLoader.Write(path, 100, -4, 200, sx, sy, sz, terrain, block, meta.ToArray());
        }
    }
}

namespace Btc.EcoSim
{
    public static class EcoSyntheticNature
    {
        /// <summary>Adds fake iron / pyrite nodes (world coords on the ground) around the first worker until real nodes are in the dump.</summary>
        public static int Inject(EcoWorld w, int iron, int pyrite, int seed, int charges)
        {
            double cx = 0, cz = 0; int n = 0;
            foreach (var e in w.Entities) if (e.Kind == "unit") { cx += e.Px; cz += e.Pz; n++; }
            if (n == 0) return 0;
            cx = cx / n - w.OriginX; cz = cz / n - w.OriginZ;
            var rnd = new Random(seed);
            var taken = new System.Collections.Generic.HashSet<int>();
            foreach (var t in w.Trees) taken.Add((t.X - w.OriginX) * w.SizeZ + (t.Z - w.OriginZ));
            int added = 0;
            for (int k = 0; k < iron + pyrite; k++)
            {
                for (int tries = 0; tries < 200; tries++)
                {
                    double ang = rnd.NextDouble() * Math.PI * 2, rad = 10 + rnd.NextDouble() * 18;
                    int x = (int)(cx + Math.Cos(ang) * rad), z = (int)(cz + Math.Sin(ang) * rad);
                    if (x < 2 || z < 2 || x >= w.SizeX - 2 || z >= w.SizeZ - 2) continue;
                    int col = x * w.SizeZ + z;
                    if (taken.Contains(col)) continue;
                    int top = w.Top(x, z); if (top < 1 || w.GetTerrain(x, top + 1, z) != 0 || w.GetTerrain(x, top + 2, z) != 0) continue;
                    taken.Add(col);
                    w.Nature.Add(new EcoNatureObj { Type = k < iron ? 4 : 8, X = x + w.OriginX, Y = top + 1 + w.OriginY, Z = z + w.OriginZ, Charges = charges });
                    added++;
                    break;
                }
            }
            return added;
        }
    }
}
