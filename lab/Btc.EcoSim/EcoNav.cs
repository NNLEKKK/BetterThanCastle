using System;

namespace Btc.EcoSim
{
    /// <summary>
    /// Voxel walk graph for bricktrons. A node is a stand cell (x,y,z): solid voxel at y-1, two free voxels (y, y+1), no obstacle.
    /// Moves: 8 neighbours (corner cutting forbidden), step up 1 voxel, drop up to MaxDrop. Pathfinding is A* from the unit to a goal
    /// region (allocation-free: preallocated stamped arrays + binary heap) with a direct-mapped (start,goal)->cost cache that is
    /// invalidated by a global version bump on every walkability-changing terrain edit.
    /// Deviation from DESIGN.md (BFS distance field per target): a field flood costs ~10 ms on the 64x100x60 band, a bounded A* ~0.1 ms,
    /// and orders are mostly distinct targets, so per-order A* + result cache is faster. Local invalidation: edits outside the band are ignored.
    /// </summary>
    public sealed class EcoNav
    {
        public readonly int SX, SY, SZ, Y0, BH;
        public readonly byte[] T; public readonly ushort[] B; public readonly byte[] Ob;
        readonly int[] g; readonly int[] stamp; int gen;
        readonly long[] heap; int hn;
        readonly int[] parent;
        public int Version;
        public long Expanded;    // statistics

        // cost units: 10 = one orthogonal voxel step
        public const int CostFlat = 10, CostDiag = 14;
        public static int CostUp = 18;        // [guess] jump up one voxel
        public static int CostDownPer = 8;    // [guess] per voxel dropped (added to the horizontal step)
        public static int MaxDrop = 4;        // [guess] bricktron can step down this many voxels without hurting itself

        struct CacheEntry { public int Version, Sx, Sy, Sz, Tx, Ty, Tz, R, Dmin, Dmax, Cost, End; }
        readonly CacheEntry[] cache = new CacheEntry[8192];

        public EcoNav(EcoWorld w, byte[] t, ushort[] b, byte[] ob)
        {
            SX = w.SizeX; SY = w.SizeY; SZ = w.SizeZ; T = t; B = b; Ob = ob;
            int minTop = int.MaxValue, maxTop = 0;
            for (int i = 0; i < w.ColTop.Length; i++) { int c = w.ColTop[i]; if (c < 0) continue; if (c < minTop) minTop = c; if (c > maxTop) maxTop = c; }
            if (minTop == int.MaxValue) minTop = 0;
            Y0 = Math.Max(0, minTop - 10);
            int y1 = Math.Min(SY - 1, maxTop + 8);
            BH = y1 - Y0 + 1;
            int n = SX * SZ * BH;
            g = new int[n]; stamp = new int[n]; parent = new int[n];
            heap = new long[Math.Max(1 << 16, n * 4)];
        }

        public int Idx(int x, int y, int z) { return (x * SZ + z) * SY + y; }

        public bool Solid(int x, int y, int z)
        {
            if ((uint)x >= (uint)SX || (uint)z >= (uint)SZ) return true;   // map border is a wall
            if (y < 0) return true;
            if (y >= SY) return false;
            int i = (x * SZ + z) * SY + y;
            return T[i] != 0 || B[i] != 0;
        }

        bool Blocked(int x, int y, int z)
        {
            if ((uint)x >= (uint)SX || (uint)z >= (uint)SZ) return true;
            if (y < 0) return true;
            if (y >= SY) return false;
            int i = (x * SZ + z) * SY + y;
            return T[i] != 0 || B[i] != 0 || Ob[i] != 0;
        }

        public bool Standable(int x, int y, int z)
        {
            if ((uint)x >= (uint)SX || (uint)z >= (uint)SZ || y < Y0 + 1 || y >= Y0 + BH - 2) return false;
            int i = (x * SZ + z) * SY + y;
            if (T[i - 1] == 0 && B[i - 1] == 0) return false;
            return !(T[i] != 0 || B[i] != 0 || Ob[i] != 0 || T[i + 1] != 0 || B[i + 1] != 0 || Ob[i + 1] != 0);
        }

        /// <summary>Nearest standable cell to (x,y,z): same column first (searching down then up), then radius 1..3.</summary>
        public bool Snap(int x, int y, int z, out int sx, out int sy, out int sz)
        {
            for (int r = 0; r <= 3; r++)
            {
                int best = int.MaxValue; sx = sy = sz = 0; bool found = false;
                for (int dx = -r; dx <= r; dx++)
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                        for (int yy = Math.Max(Y0 + 1, y - 6); yy <= Math.Min(Y0 + BH - 3, y + 6); yy++)
                        {
                            if (!Standable(x + dx, yy, z + dz)) continue;
                            int d = Math.Abs(yy - y) * 4 + r * 3;
                            if (d < best) { best = d; sx = x + dx; sy = yy; sz = z + dz; found = true; }
                        }
                    }
                if (found) return true;
            }
            sx = sy = sz = 0; return false;
        }

        /// <summary>Lowest stand cell below (x,y,z) in the same column (item / unit fall), or -1.</summary>
        public int FallTo(int x, int y, int z)
        {
            if ((uint)x >= (uint)SX || (uint)z >= (uint)SZ) return -1;
            if (y >= SY) y = SY - 1;
            for (int yy = y; yy >= 1; yy--)
            {
                int i = (x * SZ + z) * SY + yy;
                if ((T[i - 1] != 0 || B[i - 1] != 0) && !(T[i] != 0 || B[i] != 0)) return yy;
            }
            return -1;
        }

        public bool InReach(int x, int y, int z, int tx, int ty, int tz, int r, int dmin, int dmax)
        {
            int dx = x - tx; if (dx < 0) dx = -dx; int dz = z - tz; if (dz < 0) dz = -dz;
            if (dx > r || dz > r) return false;
            int dy = ty - y;
            return dy >= dmin && dy <= dmax;
        }

        public void Invalidate(int x, int y, int z)
        {
            if (y < Y0 - 1 || y > Y0 + BH) return;
            Version++;
        }

        /// <summary>Path cost (10 per orthogonal step) from the stand cell to any cell within reach of the target; -1 = unreachable.
        /// End cell is returned in ex,ey,ez.</summary>
        public int Path(int sx, int sy, int sz, int tx, int ty, int tz, int r, int dmin, int dmax, out int ex, out int ey, out int ez)
        {
            ex = sx; ey = sy; ez = sz;
            if (InReach(sx, sy, sz, tx, ty, tz, r, dmin, dmax)) return 0;
            int h = (int)((uint)(sx * 73856093) ^ (uint)(sz * 19349663) ^ (uint)(sy * 83492791) ^ (uint)(tx * 2654435761u) ^ (uint)(tz * 40503) ^ (uint)(ty * 97) ^ (uint)(r * 131 + dmin * 7 + dmax));
            int slot = (h & 0x7fffffff) % cache.Length;
            CacheEntry ce = cache[slot];
            if (ce.Version == Version + 1 && ce.Sx == sx && ce.Sy == sy && ce.Sz == sz && ce.Tx == tx && ce.Ty == ty && ce.Tz == tz && ce.R == r && ce.Dmin == dmin && ce.Dmax == dmax)
            {
                if (ce.Cost < 0) return -1;
                ex = ceX(ce); ey = ceY(ce); ez = ceZ(ce);
                return ce.Cost;
            }
            int endNode; int cost = Search(sx, sy, sz, tx, ty, tz, r, dmin, dmax, out endNode);
            ce = new CacheEntry { Version = Version + 1, Sx = sx, Sy = sy, Sz = sz, Tx = tx, Ty = ty, Tz = tz, R = r, Dmin = dmin, Dmax = dmax, Cost = cost, End = endNode };
            cache[slot] = ce;
            if (cost < 0) return -1;
            ex = ceX(ce); ey = ceY(ce); ez = ceZ(ce);
            return cost;
        }
        int ceX(CacheEntry c) { return c.End / (SZ * BH); }
        int ceZ(CacheEntry c) { return (c.End / BH) % SZ; }
        int ceY(CacheEntry c) { return c.End % BH + Y0; }

        static int Octile(int dx, int dz) { if (dx < 0) dx = -dx; if (dz < 0) dz = -dz; return dx > dz ? 10 * dx + 4 * dz : 10 * dz + 4 * dx; }

        int Search(int sx, int sy, int sz, int tx, int ty, int tz, int r, int dmin, int dmax, out int endNode)
        {
            endNode = -1;
            gen++;
            if (gen == int.MaxValue) { Array.Clear(stamp, 0, stamp.Length); gen = 1; }
            hn = 0;
            int s = (sx * SZ + sz) * BH + (sy - Y0);
            g[s] = 0; stamp[s] = gen; parent[s] = -1;
            Push(Math.Max(0, Octile(sx - tx, sz - tz) - r * 10), s);
            int budget = 400000;
            while (hn > 0 && budget-- > 0)
            {
                long top = Pop();
                int node = (int)(top & 0x3ffffff);
                int f = (int)(top >> 26);
                int cy = node % BH + Y0; int rest = node / BH; int cz = rest % SZ; int cx = rest / SZ;
                int gc = g[node];
                int hh = Math.Max(0, Octile(cx - tx, cz - tz) - r * 10);
                if (f > gc + hh) continue;   // stale
                Expanded++;
                if (InReach(cx, cy, cz, tx, ty, tz, r, dmin, dmax)) { endNode = node; return gc; }
                for (int d = 0; d < 8; d++)
                {
                    int dx = DX[d], dz = DZ[d];
                    int nx = cx + dx, nz = cz + dz;
                    if ((uint)nx >= (uint)SX || (uint)nz >= (uint)SZ) continue;
                    bool diag = dx != 0 && dz != 0;
                    if (diag)
                    {
                        if (Solid(cx + dx, cy, cz) || Solid(cx + dx, cy + 1, cz) || Solid(cx, cy, cz + dz) || Solid(cx, cy + 1, cz + dz)) continue;   // trunks (Ob) do not block diagonals: dense forests stay passable
                    }
                    int step = diag ? CostDiag : CostFlat;
                    // flat
                    if (Standable(nx, cy, nz)) Relax(node, gc, step, nx, cy, nz, tx, ty, tz, r);
                    else
                    {
                        // up one voxel: needs head room above the current cell
                        if (!Blocked(cx, cy + 2, cz) && Standable(nx, cy + 1, nz)) Relax(node, gc, step + CostUp, nx, cy + 1, nz, tx, ty, tz, r);
                        else if (!Blocked(nx, cy, nz) && !Blocked(nx, cy + 1, nz))
                        {
                            // drop: shaft cells free from cy+1 down to landing+2
                            for (int k = 1; k <= MaxDrop; k++)
                            {
                                int ny = cy - k;
                                if (Standable(nx, ny, nz)) { Relax(node, gc, step + CostDownPer * k, nx, ny, nz, tx, ty, tz, r); break; }
                                if (Blocked(nx, ny, nz)) break;
                            }
                        }
                    }
                }
            }
            return -1;
        }

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

        void Relax(int from, int gc, int cost, int nx, int ny, int nz, int tx, int ty, int tz, int r)
        {
            int ni = (nx * SZ + nz) * BH + (ny - Y0);
            int ng = gc + cost;
            if (stamp[ni] == gen && g[ni] <= ng) return;
            stamp[ni] = gen; g[ni] = ng; parent[ni] = from;
            Push(ng + Math.Max(0, Octile(nx - tx, nz - tz) - r * 10), ni);
        }

        void Push(int f, int node)
        {
            if (hn >= heap.Length) return;
            long v = ((long)f << 26) | (long)node;
            int i = hn++;
            while (i > 0) { int p = (i - 1) >> 1; if (heap[p] <= v) break; heap[i] = heap[p]; i = p; }
            heap[i] = v;
        }

        long Pop()
        {
            long res = heap[0];
            long v = heap[--hn];
            int i = 0;
            while (true)
            {
                int c = 2 * i + 1; if (c >= hn) break;
                if (c + 1 < hn && heap[c + 1] < heap[c]) c++;
                if (heap[c] >= v) break;
                heap[i] = heap[c]; i = c;
            }
            if (hn > 0) heap[i] = v;
            return res;
        }
    }
}
