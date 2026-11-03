using System;
using System.Collections.Generic;

namespace Btc.EcoSim
{
    public struct EcoTree { public int X, Y, Z, Type; }
    public struct EcoNatureObj { public int Type, X, Y, Z, Charges; }

    public sealed class EcoEntityRec
    {
        public int NetId; public string Kind, Key; public float Px, Py, Pz, RotY; public string Props;
    }

    public sealed class EcoBlueprintDef
    {
        public string Key, Factory, Cost, Footprint, Props;
    }

    public sealed class EcoRecipeRec
    {
        public int Id; public string Name, Inputs, Creates; public int Work, Heat, Qty;
    }

    /// <summary>Dense voxel grid: terrain u8 + block u16, index = (x*SizeZ + z)*SizeY + y (column-major, Y up).</summary>
    public sealed class EcoWorld
    {
        public int OriginX, OriginY, OriginZ;
        public int SizeX, SizeY, SizeZ;
        public byte[] Terrain;
        public ushort[] Block;
        public int[] ColTop;                 // per column (x*SizeZ+z): highest y with terrain or block, -1 if empty
        public string Gameplay = "";
        public int OreVoxelsConverted, WorkersAdded;
        public List<string> BlockKeys = new List<string>();     // 1-based in Block[]; entry 0 unused
        public List<string> BlockResource = new List<string>();
        public List<EcoTree> Trees = new List<EcoTree>();
        public List<EcoNatureObj> Nature = new List<EcoNatureObj>();
        public List<EcoEntityRec> Entities = new List<EcoEntityRec>();
        public List<EcoBlueprintDef> Blueprints = new List<EcoBlueprintDef>();
        public List<EcoRecipeRec> Recipes = new List<EcoRecipeRec>();
        public Dictionary<string, int> Capacity = new Dictionary<string, int>();
        public Dictionary<string, string> Param = new Dictionary<string, string>();

        public EcoWorld(int ox, int oy, int oz, int sx, int sy, int sz)
        {
            OriginX = ox; OriginY = oy; OriginZ = oz; SizeX = sx; SizeY = sy; SizeZ = sz;
            long n = (long)sx * sy * sz;
            if (n <= 0 || n > int.MaxValue) throw new ArgumentException("bad grid size");
            Terrain = new byte[n];
            Block = new ushort[n];
            ColTop = new int[sx * sz];
            for (int i = 0; i < ColTop.Length; i++) ColTop[i] = -1;
        }

        public int Index(int x, int y, int z) { return (x * SizeZ + z) * SizeY + y; }
        public bool InBounds(int x, int y, int z) { return (uint)x < (uint)SizeX && (uint)y < (uint)SizeY && (uint)z < (uint)SizeZ; }
        public byte GetTerrain(int x, int y, int z) { return InBounds(x, y, z) ? Terrain[Index(x, y, z)] : (byte)0; }
        public ushort GetBlock(int x, int y, int z) { return InBounds(x, y, z) ? Block[Index(x, y, z)] : (ushort)0; }
        public bool Solid(int x, int y, int z) { int i = Index(x, y, z); return Terrain[i] != 0 || Block[i] != 0; }
        public int Top(int x, int z) { return ColTop[x * SizeZ + z]; }

        public void SetTerrain(int x, int y, int z, byte t) { Terrain[Index(x, y, z)] = t; RefreshColumn(x, z); }
        public void SetBlock(int x, int y, int z, ushort b) { Block[Index(x, y, z)] = b; RefreshColumn(x, z); }

        public void RefreshColumn(int x, int z)
        {
            int b = (x * SizeZ + z) * SizeY;
            int top = -1;
            for (int y = SizeY - 1; y >= 0; y--) if (Terrain[b + y] != 0 || Block[b + y] != 0) { top = y; break; }
            ColTop[x * SizeZ + z] = top;
        }

        public void RefreshAllColumns() { for (int x = 0; x < SizeX; x++) for (int z = 0; z < SizeZ; z++) RefreshColumn(x, z); }

        public int[] TerrainHistogram() { var h = new int[256]; for (int i = 0; i < Terrain.Length; i++) h[Terrain[i]]++; return h; }

        public ulong Hash()
        {
            ulong h = 1469598103934665603UL;
            for (int i = 0; i < Terrain.Length; i++) h = (h ^ Terrain[i]) * 1099511628211UL;
            for (int i = 0; i < Block.Length; i++) h = (h ^ Block[i]) * 1099511628211UL;
            return h;
        }
    }
}
