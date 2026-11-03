using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Btc.EcoSim
{
    /// <summary>Reads / writes eco-map.bin (format: docs/eco/DESIGN.md "File formats").</summary>
    public static class EcoMapLoader
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static EcoWorld Load(string path) { return Load(path, true); }

        public static EcoWorld Load(string path, bool oreToStone)
        {
            using (var fs = File.OpenRead(path)) return Load(fs, oreToStone);
        }

        public static EcoWorld Load(Stream s) { return Load(s, true); }

        public static EcoWorld Load(Stream s, bool oreToStone)
        {
            var r = new BinaryReader(s, Encoding.UTF8);
            byte[] magic = r.ReadBytes(4);
            if (magic.Length != 4 || magic[0] != 'B' || magic[1] != 'T' || magic[2] != 'C' || magic[3] != 'E') throw new InvalidDataException("bad magic (expected BTCE)");
            int ver = r.ReadUInt16();
            if (ver != 1) throw new InvalidDataException("unsupported eco-map version " + ver);
            int ox = r.ReadInt32(), oy = r.ReadInt32(), oz = r.ReadInt32();
            int sx = r.ReadUInt16(), sy = r.ReadUInt16(), sz = r.ReadUInt16();
            var w = new EcoWorld(ox, oy, oz, sx, sy, sz);
            int n = w.Terrain.Length;
            byte[] tb = r.ReadBytes(n);
            if (tb.Length != n) throw new EndOfStreamException("terrain truncated");
            Buffer.BlockCopy(tb, 0, w.Terrain, 0, n);
            byte[] bb = r.ReadBytes(n * 2);
            if (bb.Length != n * 2) throw new EndOfStreamException("block truncated");
            Buffer.BlockCopy(bb, 0, w.Block, 0, bb.Length);   // little-endian host assumed
            int metaLen = r.ReadInt32();
            if (metaLen < 0 || metaLen > (1 << 28)) throw new InvalidDataException("bad meta length");
            byte[] mb = r.ReadBytes(metaLen);
            if (mb.Length != metaLen) throw new EndOfStreamException("meta truncated");
            w.BlockKeys.Add(""); w.BlockResource.Add("");
            ParseMeta(w, Encoding.UTF8.GetString(mb));
            if (oreToStone) w.OreVoxelsConverted = ConvertOres(w);
            EnsureWorkers(w, EcoParams.UnitCount);
            w.RefreshAllColumns();
            return w;
        }

        /// <summary>Dump line: nature|type|x|y|z|growth|max|props. Harvestable amount = growth*max resource units; one hit yields plant 2 / boulder 4 / iron,pyrite 1 ([code] NatureData.ExpectedQte). 0 = unknown (sim default).</summary>
        static int NatureCharges(int type, string[] f)
        {
            if (f.Length < 7) return 0;
            float g = F(f[5]); int max = I(f[6]);
            if (max <= 0 || g <= 0) return 0;
            int y = type == 1 ? 2 : type == 2 ? 4 : 1;
            int c = Math.Max(1, (int)Math.Round(g * max / y)); return (type == 4 || type == 8) ? Math.Min(c, 40) : c;   // [user] iron/pyrite node: 40 resources max, 1 per hit
        }

        static int Fl(string s) { return (int)Math.Floor(F(s) + 0.5); }

        /// <summary>NatureType: numeric flag (plant 1, boulder 2, iron 4, pyrite 8) or a name.</summary>
        static int NatureType(string s)
        {
            int v;
            if (int.TryParse(s, NumberStyles.Integer, Inv, out v)) return v;
            string l = s.ToLowerInvariant();
            if (l.Contains("iron")) return 4;
            if (l.Contains("pyrite")) return 8;
            if (l.Contains("boulder")) return 2;
            if (l.Contains("plant")) return 1;
            return 0;
        }

        /// <summary>User decision 2026-11-02: no ore from walls. Iron 54, OrangeCrystal 44, BlueCrystal 38, crusts 55/57/59 become Stone 8.</summary>
        static int ConvertOres(EcoWorld w)
        {
            int n = 0;
            byte[] t = w.Terrain;
            for (int i = 0; i < t.Length; i++)
            {
                byte b = t[i];
                if (b == 54 || b == 44 || b == 38 || b == 55 || b == 57 || b == 59) { t[i] = EcoTerrain.Stone; n++; }
            }
            return n;
        }

        /// <summary>Sandbox gives 5 bricktrons; the 6th is cloned next to the last one (the sim snaps it to a walkable cell).</summary>
        static void EnsureWorkers(EcoWorld w, int want)
        {
            int units = 0, maxId = 0; EcoEntityRec last = null;
            foreach (var e in w.Entities) { if (e.NetId > maxId) maxId = e.NetId; if (e.Kind == "unit") { units++; last = e; } }
            if (last == null) return;
            while (units < want)
            {
                w.Entities.Add(new EcoEntityRec { NetId = ++maxId, Kind = "unit", Key = last.Key, Px = last.Px + 1f, Py = last.Py, Pz = last.Pz, RotY = last.RotY, Props = "synthetic6=1" });
                units++; w.WorkersAdded++;
            }
        }

        static float F(string s) { float v; float.TryParse(s, NumberStyles.Float, Inv, out v); return v; }
        static int I(string s) { int v; int.TryParse(s, NumberStyles.Integer, Inv, out v); return v; }

        static void ParseMeta(EcoWorld w, string text)
        {
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                string[] f = line.Split('|');
                switch (f[0])
                {
                    case "gp": w.Gameplay = line.Length > 3 ? line.Substring(3) : ""; break;
                    case "blk":
                        {
                            int i = I(f[1]);
                            while (w.BlockKeys.Count <= i) { w.BlockKeys.Add(""); w.BlockResource.Add(""); }
                            w.BlockKeys[i] = f.Length > 2 ? f[2] : "";
                            w.BlockResource[i] = f.Length > 3 ? f[3] : "";
                        }
                        break;
                    case "tree": if (f.Length >= 5) w.Trees.Add(new EcoTree { X = I(f[1]), Y = I(f[2]), Z = I(f[3]), Type = I(f[4]) }); break;
                    case "nature":
                        if (f.Length >= 5)
                            w.Nature.Add(new EcoNatureObj { Type = NatureType(f[1]), X = Fl(f[2]), Y = Fl(f[3]), Z = Fl(f[4]), Charges = NatureCharges(NatureType(f[1]), f) });
                        break;
                    case "ent":
                        if (f.Length >= 8)
                            w.Entities.Add(new EcoEntityRec { NetId = I(f[1]), Kind = f[2], Key = f[3], Px = F(f[4]), Py = F(f[5]), Pz = F(f[6]), RotY = F(f[7]), Props = f.Length > 8 ? f[8] : "" });
                        break;
                    case "bp":
                        if (f.Length >= 5)
                            w.Blueprints.Add(new EcoBlueprintDef { Key = f[1], Factory = f[2], Cost = f[3], Footprint = f[4], Props = f.Length > 5 ? f[5] : "" });
                        break;
                    case "recipe":
                        if (f.Length >= 8)
                            w.Recipes.Add(new EcoRecipeRec { Id = I(f[1]), Name = f[2], Inputs = f[3], Work = I(f[4]), Heat = I(f[5]), Creates = f[6], Qty = I(f[7]) });
                        break;
                    case "cap": if (f.Length >= 3) w.Capacity[f[1]] = I(f[2]); break;
                    case "param": if (f.Length >= 3) w.Param[f[1]] = f[2]; break;
                    default: break;   // unknown record kinds ignored (forward compatible)
                }
            }
        }

        public static void Write(string path, int ox, int oy, int oz, int sx, int sy, int sz, byte[] terrain, ushort[] block, string[] metaLines)
        {
            using (var fs = File.Create(path)) Write(fs, ox, oy, oz, sx, sy, sz, terrain, block, metaLines);
        }

        public static void Write(Stream s, int ox, int oy, int oz, int sx, int sy, int sz, byte[] terrain, ushort[] block, string[] metaLines)
        {
            var wr = new BinaryWriter(s, Encoding.UTF8);
            wr.Write(new byte[] { (byte)'B', (byte)'T', (byte)'C', (byte)'E' });
            wr.Write((ushort)1);
            wr.Write(ox); wr.Write(oy); wr.Write(oz);
            wr.Write((ushort)sx); wr.Write((ushort)sy); wr.Write((ushort)sz);
            wr.Write(terrain, 0, terrain.Length);
            byte[] bb = new byte[block.Length * 2];
            Buffer.BlockCopy(block, 0, bb, 0, bb.Length);
            wr.Write(bb);
            byte[] meta = Encoding.UTF8.GetBytes(string.Join("\n", metaLines) + "\n");
            wr.Write(meta.Length);
            wr.Write(meta);
            wr.Flush();
        }
    }
}
