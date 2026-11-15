// Ethereal Hell map generator v2. Compiled by scripts/maps/ethereal-hell.ps1 (Add-Type, C# 5).
// Four equal layers (0 circles y40, 1 hexagons y60, 2 galaxy y80, 3 triangles y104) live in 8 spiral sectors (layer = sector mod 4),
// 180 degree point symmetry (sector s <-> s+4). Almost everything is Heightmap.png (one slab per column); added voxels only where a
// galaxy arm crosses OVER another layer (a "bridge over"). Ramps between neighbouring layers are heightmap columns too.
// Grid: u,v in 0..N-1, world x = X0+u, z = Z0+v, mirror (u,v) -> (N-1-u, N-1-v) = world (1023-x, 1023-z).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

public static class EtherealHell
{
    const int N = 240, X0 = 392, Z0 = 392;
    const double C = (N - 1) / 2.0;
    const int LC = 0, LH = 1, LG = 2, LT = 3, NL = 4;       // circles, hexagons, galaxy, triangles
    const int Y1 = 40, Y2 = 60, Y3 = 80, Y4 = 104;
    const double RZ = 56;                                    // sectors start here; inside: central hexagon + galaxy ring
    const double KTW = 0.03, GAP = 3.5, SLAB = 6;
    static int[][] lt = new int[NL][];                       // candidate surface top per layer shape (unclipped), -1 none
    static byte[][] lty = new byte[NL][];
    static int[] top = new int[N * N], bot = new int[N * N], lay = new int[N * N];   // composite heightmap; lay -1 empty, 4 ramp
    static byte[] ty = new byte[N * N];
    static List<int[]> bridge = new List<int[]>();           // voxel columns: u,v,top (arm over another layer)
    static StringBuilder log = new StringBuilder();
    static Random rnd; static int seed;
    static List<double[]> spawns = new List<double[]>();     // dx,dz,layer

    static int I(int u, int v) { return v * N + u; }
    static bool In(int u, int v) { return u >= 0 && v >= 0 && u < N && v < N; }
    static uint Hash(int a, int b, int s)
    {
        uint h = (uint)(a * 374761393 + b * 668265263 + s * 2147483647 + seed * 1274126177);
        h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16; h *= 2246822519u; h ^= h >> 15; return h;
    }
    static double H01(int a, int b, int s) { return (Hash(a, b, s) & 0xFFFFFF) / (double)0x1000000; }
    static void Say(string s) { log.AppendLine(s); }
    static int Rnd(double x) { return (int)Math.Round(x); }

    // sector of a point: returns sector id 0..7 (or -1 inside RZ), layer = id%4, margin = distance to the nearest sector boundary
    static int Sector(double dx, double dz, out double margin)
    {
        double r = Math.Sqrt(dx * dx + dz * dz); margin = 0;
        if (r < RZ) return -1;
        double th = Math.Atan2(dz, dx);
        double idx = (2 * th + KTW * r) / (Math.PI / 2);
        double fl = Math.Floor(idx), f = idx - fl;
        double G = Math.Sqrt((2 / r) * (2 / r) + KTW * KTW) / (Math.PI / 2);
        margin = Math.Min(f, 1 - f) / G;
        return (((int)fl % 8) + 8) % 8;
    }

    public static string Run(string outDir, int seedIn)
    {
        seed = seedIn; rnd = new Random(seedIn);
        for (int l = 0; l < NL; l++) { lt[l] = new int[N * N]; lty[l] = new byte[N * N]; for (int i = 0; i < N * N; i++) lt[l][i] = -1; }
        for (int i = 0; i < N * N; i++) { top[i] = -1; bot[i] = -1; lay[i] = -1; }
        FindSpawns();
        BuildHex(); BuildCircles(); BuildGalaxy(); BuildTriangles();
        Compose();
        MakeLinks();
        Report();
        WriteMap(outDir);
        return log.ToString();
    }

    // best (max margin) point of a layer's sector, canonical half (dz < 0)
    static double[] BestSpot(int layer, double rmin, double rmax)
    {
        double bm = -1; double[] best = null;
        for (double r = rmin; r <= rmax; r += 2)
            for (double th = -Math.PI; th < 0; th += 0.01)
            {
                double dx = Math.Cos(th) * r, dz = Math.Sin(th) * r; double m; int s = Sector(dx, dz, out m);
                if (s < 0 || s % 4 != layer) continue;
                if (m > bm) { bm = m; best = new double[] { dx, dz, m, s }; }
            }
        return best;
    }
    static void FindSpawns()
    {
        double[] h = BestSpot(LH, 78, 100), c = BestSpot(LC, 78, 100);
        spawns.Add(new double[] { h[0], h[1], LH }); spawns.Add(new double[] { -h[0], -h[1], LH });
        spawns.Add(new double[] { c[0], c[1], LC }); spawns.Add(new double[] { -c[0], -c[1], LC });
        Say("spawn hex  at (" + Rnd(h[0]) + "," + Rnd(h[1]) + ") sector " + (int)h[3] + " margin " + Rnd(h[2]));
        Say("spawn circ at (" + Rnd(c[0]) + "," + Rnd(c[1]) + ") sector " + (int)c[3] + " margin " + Rnd(c[2]));
    }

    // ---------------- hexagons (pointy-top, circumradius S) ----------------
    const double S = 12.0, SQ3 = 1.7320508075688772;
    static void Axial(double x, double z, out int q, out int r)
    {
        double fq = (SQ3 / 3 * x - z / 3) / S, fr = (2.0 / 3 * z) / S;
        double fx = fq, fz = fr, fy = -fx - fz;
        double rx = Math.Round(fx), ry = Math.Round(fy), rz = Math.Round(fz);
        double dx = Math.Abs(rx - fx), dy = Math.Abs(ry - fy), dz = Math.Abs(rz - fz);
        if (dx > dy && dx > dz) rx = -ry - rz; else if (dy > dz) ry = -rx - rz; else rz = -rx - ry;
        q = (int)rx; r = (int)rz;
    }
    static void CellCenter(int q, int r, out double cx, out double cz) { cx = S * SQ3 * (q + r / 2.0); cz = S * 1.5 * r; }
    static void Canon(ref int q, ref int r) { if (q < -q || (q == -q && r < -r)) return; q = -q; r = -r; }
    static bool SpawnCell(int q, int r, int rad)
    {
        for (int k = 0; k < spawns.Count; k++)
        {
            if (spawns[k][2] != LH) continue;
            int sq, sr; Axial(spawns[k][0], spawns[k][1], out sq, out sr);
            int dq = q - sq, dr = r - sr;
            if (Math.Max(Math.Abs(dq), Math.Max(Math.Abs(dr), Math.Abs(dq + dr))) <= rad) return true;
        }
        return false;
    }
    static bool HexCell(int q, int r)
    {
        if (SpawnCell(q, r, 2)) return true;
        int cq = q, cr = r; Canon(ref cq, ref cr);
        return H01(cq, cr, 11) > 0.13;
    }
    static Dictionary<long, int> hexOv = new Dictionary<long, int>();
    static long CK(int q, int r) { return (long)(q + 512) * 1024 + (r + 512); }
    static int HexOffBase(int q, int r)
    {
        if (SpawnCell(q, r, 2)) return 0;
        int cq = q, cr = r; Canon(ref cq, ref cr);
        int ov; if (hexOv.TryGetValue(CK(cq, cr), out ov)) return ov;
        double p = H01(cq, cr, 12);
        if (p < 0.50) return 0; if (p < 0.64) return 1; if (p < 0.74) return 2; if (p < 0.88) return -1; return -2;
    }
    // make every hexagon tile that touches another tile reachable from its sector's spawn tile (adjacent tiles differ by at most 1)
    static void FixHex()
    {
        int[][] nb = { new int[] { 1, 0 }, new int[] { -1, 0 }, new int[] { 0, 1 }, new int[] { 0, -1 }, new int[] { 1, -1 }, new int[] { -1, 1 } };
        List<int[]> cells = new List<int[]>(); Dictionary<long, int> idx = new Dictionary<long, int>();
        for (int q = -14; q <= 14; q++) for (int r = -14; r <= 14; r++)
            {
                double cx, cz; CellCenter(q, r, out cx, out cz); double m; int s = Sector(cx, cz, out m);
                if (s < 0 || s % 4 != LH || !HexCell(q, r)) continue;
                idx[CK(q, r)] = cells.Count; cells.Add(new int[] { q, r });
            }
        int[] spawnIdx = new int[2];
        for (int k = 0; k < 2; k++) { int sq, sr; Axial(spawns[k][0], spawns[k][1], out sq, out sr); spawnIdx[k] = idx[CK(sq, sr)]; }
        int changes = 0;
        for (int it = 0; it < 400; it++)
        {
            int[] par = new int[cells.Count]; for (int i = 0; i < par.Length; i++) par[i] = i;
            for (int a = 0; a < cells.Count; a++) for (int d = 0; d < 6; d++)
                {
                    int b; if (!idx.TryGetValue(CK(cells[a][0] + nb[d][0], cells[a][1] + nb[d][1]), out b)) continue;
                    if (Math.Abs(HexOffBase(cells[a][0], cells[a][1]) - HexOffBase(cells[b][0], cells[b][1])) <= 1) Union(par, a, b);
                }
            int r0 = Find(par, spawnIdx[0]), r1 = Find(par, spawnIdx[1]);
            int bestA = -1, bestB = -1, bestD = 99;
            for (int a = 0; a < cells.Count; a++)
            {
                int ra = Find(par, a); if (ra == r0 || ra == r1) continue;
                for (int d = 0; d < 6; d++)
                {
                    int b; if (!idx.TryGetValue(CK(cells[a][0] + nb[d][0], cells[a][1] + nb[d][1]), out b)) continue;
                    int dd = Math.Abs(HexOffBase(cells[a][0], cells[a][1]) - HexOffBase(cells[b][0], cells[b][1]));
                    if (dd < bestD && !SpawnCell(cells[a][0], cells[a][1], 2)) { bestD = dd; bestA = a; bestB = b; }
                }
            }
            if (bestA < 0) break;
            int ob = HexOffBase(cells[bestB][0], cells[bestB][1]), oa = HexOffBase(cells[bestA][0], cells[bestA][1]);
            int nv = oa + Math.Sign(ob - oa) * (Math.Abs(ob - oa) - 1);   // step towards b until within 1
            int cq = cells[bestA][0], cr = cells[bestA][1]; Canon(ref cq, ref cr); hexOv[CK(cq, cr)] = nv; changes++;
        }
        Say("hex tiles: " + cells.Count + ", flattened " + changes + " to keep them connected");
    }
    static int HexOff(int q, int r) { return HexOffBase(q, r); }
    static void BuildHex()
    {
        FixHex();
        double R0 = 28, a0 = R0 * SQ3 / 2;
        for (int v = 0; v < N; v++) for (int u = 0; u < N; u++)
            {
                double dx = u - C, dz = v - C; int i = I(u, v);
                if (Math.Sqrt(dx * dx + dz * dz) > 118) continue;
                if (Math.Abs(dx) <= a0 && Math.Abs(dz) + Math.Abs(dx) / SQ3 <= R0)
                {
                    lt[LH][i] = Y2; lty[LH][i] = (byte)(Math.Abs(dz) + Math.Abs(dx) / SQ3 > R0 - 1.2 || Math.Abs(dx) > a0 - 1.0 ? 8 : 3); continue;
                }
                if (Math.Sqrt(dx * dx + dz * dz) < RZ) continue;   // inside RZ only the central hexagon exists
                int q, r; Axial(dx, dz, out q, out r);
                if (!HexCell(q, r)) continue;
                double cx, cz; CellCenter(q, r, out cx, out cz);
                double px = dx - cx, pz = dz - cz;
                double m = Math.Max(Math.Abs(px), Math.Max(Math.Abs(px * 0.5 + pz * SQ3 / 2), Math.Abs(px * 0.5 - pz * SQ3 / 2)));
                if (m > S * SQ3 / 2 + 0.01) continue;
                lt[LH][i] = Y2 + HexOff(q, r); lty[LH][i] = (byte)(m > S * SQ3 / 2 - 1.0 ? 8 : 3);
            }
    }

    // ---------------- circles + 4-5 wide corridors ----------------
    class Disc { public double x, z, r; }
    static int L1Top(double dx, double dz) { return Y1 + Rnd(2.5 * Math.Cos(dx / 23.0) * Math.Cos(dz / 19.0)); }
    static void BuildCircles()
    {
        List<Disc> d0 = new List<Disc>();   // sector 0 (mirrored into sector 4)
        Disc sp = new Disc(); sp.x = spawns[2][0]; sp.z = spawns[2][1]; sp.r = 16; d0.Add(sp);
        int tries = 0;
        while (d0.Count < 9 && tries++ < 20000)
        {
            Disc d = new Disc(); double ang = rnd.NextDouble() * Math.PI * 2, rad = RZ + 8 + rnd.NextDouble() * 55;
            d.x = Math.Cos(ang) * rad; d.z = Math.Sin(ang) * rad; d.r = 7 + rnd.Next(0, 5);
            double m; int s = Sector(d.x, d.z, out m);
            if (s != Sector(sp.x, sp.z, out m) || m < d.r + GAP + 1) continue;
            if (Math.Sqrt(d.x * d.x + d.z * d.z) + d.r > 112) continue;
            bool ok = true;
            for (int k = 0; k < d0.Count && ok; k++) { double ex = d.x - d0[k].x, ez = d.z - d0[k].z; if (Math.Sqrt(ex * ex + ez * ez) < d.r + d0[k].r + 10) ok = false; }
            if (ok) d0.Add(d);
        }
        // edges: MST (Prim) + one extra loop edge inside sector 0
        List<int[]> edges = new List<int[]>(); bool[] inT = new bool[d0.Count]; inT[0] = true;
        for (int n = 1; n < d0.Count; n++)
        {
            double bd = 1e9; int ba = -1, bb = -1;
            for (int a = 0; a < d0.Count; a++) if (inT[a]) for (int b = 0; b < d0.Count; b++) if (!inT[b]) { double e = D(d0[a], d0[b]); if (e < bd) { bd = e; ba = a; bb = b; } }
            inT[bb] = true; edges.Add(new int[] { ba, bb });
        }
        double bl = 1e9; int la = -1, lb = -1;
        for (int a = 0; a < d0.Count; a++) for (int b = a + 1; b < d0.Count; b++)
            {
                bool has = false; for (int e = 0; e < edges.Count; e++) if ((edges[e][0] == a && edges[e][1] == b) || (edges[e][0] == b && edges[e][1] == a)) has = true;
                if (!has && D(d0[a], d0[b]) < bl) { bl = D(d0[a], d0[b]); la = a; lb = b; }
            }
        if (la >= 0) edges.Add(new int[] { la, lb });
        Say("circles: " + d0.Count + " discs per sector, " + edges.Count + " corridors; first " + Rnd(d0[0].x) + "," + Rnd(d0[0].z) + " r" + d0[0].r + " others " + (d0.Count > 1 ? Rnd(d0[1].x) + "," + Rnd(d0[1].z) + " r" + d0[1].r : ""));
        for (int copy = 0; copy < 2; copy++)
        {
            double sg = copy == 0 ? 1 : -1;
            for (int e = 0; e < edges.Count; e++)
            {
                Disc a = d0[edges[e][0]], b = d0[edges[e][1]];
                double w = H01(edges[e][0], edges[e][1], 21) < 0.55 ? 4.5 : 5.5;
                Corridor(sg * a.x, sg * a.z, sg * b.x, sg * b.z, w);
            }
            for (int k = 0; k < d0.Count; k++) DiscDraw(sg * d0[k].x, sg * d0[k].z, d0[k].r);
            int cnt0 = 0; for (int q = 0; q < N * N; q++) if (lt[LC][q] >= 0) cnt0++; Say("LC cells after copy " + copy + ": " + cnt0);
        }
    }
    static double D(Disc a, Disc b) { double x = a.x - b.x, z = a.z - b.z; return Math.Sqrt(x * x + z * z); }
    static void PutC(int u, int v)
    {
        if (!In(u, v)) return;
        lt[LC][I(u, v)] = L1Top(u - C, v - C); lty[LC][I(u, v)] = 3;
    }
    // shapes are rasterised per grid column (px = u - C) so the C2 mirror u -> N-1-u is exact
    static void DiscDraw(double x, double z, double rad)
    {
        int u0 = (int)Math.Floor(x + C - rad) - 1, u1 = (int)Math.Ceiling(x + C + rad) + 1, v0 = (int)Math.Floor(z + C - rad) - 1, v1 = (int)Math.Ceiling(z + C + rad) + 1;
        for (int v = v0; v <= v1; v++) for (int u = u0; u <= u1; u++)
            {
                double ex = u - C - x, ez = v - C - z;
                if (ex * ex + ez * ez <= rad * rad) PutC(u, v);
            }
    }
    static void Corridor(double ax, double az, double bx, double bz, double w)
    {
        double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az)); int steps = (int)(len * 2);
        double dx = (bx - ax) / len, dz = (bz - az) / len; int rr = (int)Math.Ceiling(w / 2) + 2;
        for (int s = 0; s <= steps; s++)
        {
            double t = s / (double)steps, cx = ax + (bx - ax) * t, cz = az + (bz - az) * t;
            int uc = (int)Math.Floor(cx + C), vc = (int)Math.Floor(cz + C);
            for (int v = vc - rr; v <= vc + rr; v++) for (int u = uc - rr; u <= uc + rr; u++)
                {
                    double px = u - C, pz = v - C;
                    double perp = Math.Abs((px - cx) * (-dz) + (pz - cz) * dx);
                    if (perp <= w / 2 && (px - cx) * (px - cx) + (pz - cz) * (pz - cz) <= (w / 2 + 1) * (w / 2 + 1)) PutC(u, v);
                }
        }
    }

    // ---------------- galaxy: ring around the centre + 8 spiral arms that continue across the other layers ----------------
    const double RA = 46, RB = 36, ROT = 0.44;
    static double GalH(int arm, double tn, double th)
    {
        double[] end = { +9, -7, +4, -19 };
        return Y3 + end[arm] * tn + 2.0 * Math.Sin(th * 3.0 + arm);
    }
    static void GBlob(double x, double z, double rad, int t)
    {
        int u0 = (int)Math.Floor(x + C - rad) - 1, u1 = (int)Math.Ceiling(x + C + rad) + 1, v0 = (int)Math.Floor(z + C - rad) - 1, v1 = (int)Math.Ceiling(z + C + rad) + 1;
        for (int v = v0; v <= v1; v++) for (int u = u0; u <= u1; u++)
            {
                if (!In(u, v)) continue;
                double ex = u - C - x, ez = v - C - z;
                if (ex * ex + ez * ez > rad * rad) continue;
                int i = I(u, v);
                if (t > lt[LG][i]) { lt[LG][i] = t; lty[LG][i] = 3; }
            }
    }
    static void BuildGalaxy()
    {
        for (double th = 0; th < Math.PI * 2; th += 0.003)
        {
            double ex = RA * Math.Cos(th), ez = RB * Math.Sin(th);
            GBlob(ex * Math.Cos(ROT) - ez * Math.Sin(ROT), ex * Math.Sin(ROT) + ez * Math.Cos(ROT), 2.7, Y3 + Rnd(3 * Math.Sin(2 * th + 0.7)));
        }
        double[] phi = { 0.2, 1.05, 1.9, 2.75 };
        double[] kcur = { 1.2, -1.0, 1.5, -1.3 };
        double[] len = { 90, 84, 80, 96 };
        for (int arm = 0; arm < 4; arm++)
            for (int copy = 0; copy < 2; copy++)
            {
                double sg = copy == 0 ? 1 : -1;
                for (double tt = 0; tt <= len[arm]; tt += 0.4)
                {
                    double tn = tt / len[arm], rad = 44 + tt * 0.8;
                    double ang = phi[arm] + kcur[arm] * Math.Log(1 + tt / 14.0) * 1.1;
                    double x = Math.Cos(ang) * rad * (1 + 0.1 * Math.Cos(2 * ang)), z = Math.Sin(ang) * rad * (1 - 0.1 * Math.Cos(2 * ang));
                    if (Math.Sqrt(x * x + z * z) > 112) break;
                    GBlob(sg * x, sg * z, 3.4 - 1.0 * tn + 0.5 * Math.Sin(tt * 0.2 + arm), Rnd(GalH(arm, tn, ang)));
                }
            }
    }

    // ---------------- triangles + pyramids ----------------
    const double TA = 13.0, TH = TA * 0.8660254037844386;
    static void TriCell(double dx, double dz, out int ci, out int cj, out bool up, out double lam)
    {
        double A = dz / TH, B = dx / TA - A / 2.0;
        ci = (int)Math.Floor(B); cj = (int)Math.Floor(A);
        double fb = B - ci, fa = A - cj; up = fb + fa < 1.0;
        lam = up ? Math.Min(Math.Min(fb, fa), 1 - fb - fa) : Math.Min(Math.Min(1 - fb, 1 - fa), fb + fa - 1);
    }
    static void TriCanon(ref int i, ref int j, ref bool up)
    {
        int pi = -i - 1, pj = -j - 1; bool pu = !up;
        if (pj < j || (pj == j && (pi < i || (pi == i && (pu ? 1 : 0) < (up ? 1 : 0))))) { i = pi; j = pj; up = pu; }
    }
    static bool TriIncluded(int ci, int cj, bool up)
    {
        int a = ci, b = cj; bool u = up; TriCanon(ref a, ref b, ref u);
        double x = TA * (ci + cj / 2.0 + (up ? 0.5 : 1.0)), z = TH * (cj + (up ? 1.0 / 3 : 2.0 / 3));
        double ang = Math.Atan2(z, x);
        return H01(a * 2 + (u ? 1 : 0), b, 31) < 0.60 + 0.15 * Math.Cos(ang * 2);
    }
    static long TriKey(int i, int j, bool up) { return ((long)(i + 4096) << 24) | ((long)(j + 4096) << 4) | (up ? 1L : 0L); }
    static int[][] TriNb(int i, int j, bool up)
    {
        if (up) return new int[][] { new int[] { i, j, 0 }, new int[] { i - 1, j, 0 }, new int[] { i, j - 1, 0 } };
        return new int[][] { new int[] { i, j, 1 }, new int[] { i + 1, j, 1 }, new int[] { i, j + 1, 1 } };
    }
    static void BuildTriangles()
    {
        for (int v = 0; v < N; v++) for (int u = 0; u < N; u++)
            {
                double dx = u - C, dz = v - C; int ci, cj; bool up; double lam; TriCell(dx, dz, out ci, out cj, out up, out lam);
                if (!TriIncluded(ci, cj, up)) continue;
                int a = ci, b = cj; bool uu = up; TriCanon(ref a, ref b, ref uu);
                double plane = 0.5 * Math.Abs(dx * 0.05) + 0.5 * Math.Abs(dz * 0.04) - 3;
                int t = Y4 + Rnd(plane);
                if (H01(a * 2 + (uu ? 1 : 0), b, 41) < 0.30) t += Math.Min((int)(lam * TH / 1.6), 5);
                int i = I(u, v); lt[LT][i] = t; lty[LT][i] = 3;
            }
    }

    // ---------------- composition: one slab per column; galaxy arms cross over other layers ----------------
    static void Compose()
    {
        int bridges = 0, merged = 0, over = 0;
        for (int v = 0; v < N; v++) for (int u = 0; u < N; u++)
            {
                double dx = u - C, dz = v - C; int i = I(u, v); double m; int s = Sector(dx, dz, out m);
                int l = -1;
                if (s < 0) { if (lt[LH][i] >= 0) l = LH; else if (lt[LG][i] >= 0) l = LG; }
                else if (m >= GAP && lt[s % 4][i] >= 0) l = s % 4;
                if (l >= 0) { top[i] = lt[l][i]; bot[i] = top[i] - (int)SLAB; lay[i] = l; ty[i] = lty[l][i]; }
            }
        for (int i = 0; i < N * N; i++)
        {
            if (lt[LG][i] < 0 || lay[i] == LG) continue;
            int at = lt[LG][i];
            if (lay[i] < 0) { top[i] = at; bot[i] = at - (int)SLAB; lay[i] = LG; ty[i] = 3; continue; }
            int ct = top[i];
            if (at - 2 >= ct + 7) { bridge.Add(new int[] { i % N, i / N, at }); bridges++; }
            else if (at <= ct - 6 - 4) { bridge.Add(new int[] { i % N, i / N, at }); bridges++; }
            else if (at > ct) { top[i] = at; merged++; }
            else over++;
        }
        Say("composition: " + bridges + " bridge-over columns (voxels), " + merged + " raised columns, " + over + " skipped");
    }

    // ---------------- ramps between neighbouring layers (heightmap columns, max composite) ----------------
    static void MakeLinks()
    {
        TryLink(LC, LH, 2, 0.45, 1.0);
        TryLink(LH, LG, 2, 0.35, 0.95);
        TryLink(LG, LT, 1, 0.30, 0.85);
    }
    static void TryLink(int low, int high, int count, double smin, double smax)
    {
        List<int> cand = new List<int>();
        for (int i = 0; i < N * N; i++) if (lay[i] == low) { double m; double dx = i % N - C, dz = i / N - C; Sector(dx, dz, out m); if (m < 22 && dz < 0) cand.Add(i); }
        int made = 0, guard = 0; List<int[]> done = new List<int[]>();
        while (made < count && guard++ < 80000 && cand.Count > 0)
        {
            int ci = cand[rnd.Next(cand.Count)]; int u0 = ci % N, v0 = ci / N; bool close = false;
            for (int k = 0; k < done.Count; k++) { double ex = u0 - done[k][0], ez = v0 - done[k][1]; if (ex * ex + ez * ez < 50 * 50) close = true; }
            if (close) continue;
            double ang = rnd.NextDouble() * Math.PI * 2, dx2 = Math.Cos(ang), dz2 = Math.Sin(ang); int lowTop = top[ci];
            for (int t = 8; t <= 60; t++)
            {
                int u = Rnd(u0 + dx2 * t), v = Rnd(v0 + dz2 * t); if (!In(u, v)) break;
                if (lay[I(u, v)] != high) continue;
                int hi = top[I(u, v)]; double slope = (hi - lowTop) / (double)t;
                if (slope < smin || slope > smax) break;
                if (RampClear(u0, v0, dx2, dz2, t, lowTop, slope, low, high)) { ApplyRamp(u0, v0, dx2, dz2, t, lowTop, slope, false); ApplyRamp(u0, v0, dx2, dz2, t, lowTop, slope, true); done.Add(new int[] { u0, v0 }); made++; Say("ramp " + low + "->" + high + " at u=" + u0 + " v=" + v0 + " len " + t + " rise " + (hi - lowTop)); }
                break;
            }
        }
        if (made < count) Say("WARN only " + made + "/" + count + " ramps " + low + "->" + high);
    }
    static bool RampClear(int u0, int v0, double dx, double dz, int D, int lowTop, double slope, int low, int high)
    {
        for (int t = 2; t < D - 1; t++)
            for (int k = -3; k <= 3; k++)
            {
                int u = Rnd(u0 + dx * t - dz * k), v = Rnd(v0 + dz * t + dx * k);
                if (!In(u, v) || !In(N - 1 - u, N - 1 - v)) return false;
                int l1 = lay[I(u, v)], l2 = lay[I(N - 1 - u, N - 1 - v)];
                if ((l1 >= 0 && l1 != low && l1 != high && l1 != 4) || (l2 >= 0 && l2 != low && l2 != high && l2 != 4)) return false;
            }
        return true;
    }
    static void ApplyRamp(int u0, int v0, double dx, double dz, int D, int lowTop, double slope, bool mirror)
    {
        for (int t = 0; t <= D; t++)
        {
            int rt = lowTop + Rnd(slope * t);
            for (int k = -2; k <= 2; k++)
            {
                int u = Rnd(u0 + dx * t - dz * k), v = Rnd(v0 + dz * t + dx * k);
                if (mirror) { u = N - 1 - u; v = N - 1 - v; }
                if (!In(u, v)) continue; int i = I(u, v);
                if (top[i] >= rt) continue;
                top[i] = rt; bot[i] = rt - (int)SLAB; lay[i] = 4; ty[i] = 3;
            }
        }
    }

    // ---------------- verification ----------------
    static void Report()
    {
        for (int s = 0; s < spawns.Count; s++)
        {
            int cu = Rnd(spawns[s][0] + C), cv = Rnd(spawns[s][1] + C), nl = 0, nc = 0, tot2 = 0; double mm; int sc = Sector(spawns[s][0], spawns[s][1], out mm);
            for (int dv = -10; dv <= 10; dv++) for (int du = -10; du <= 10; du++) { int uu = cu + du, vv = cv + dv; if (!In(uu, vv)) continue; tot2++; if (lt[(int)spawns[s][2]][I(uu, vv)] >= 0) nl++; if (lay[I(uu, vv)] == (int)spawns[s][2]) nc++; }
            Say("dbg spawn " + s + " u=" + cu + " v=" + cv + " sector " + sc + " margin " + Rnd(mm) + " shape " + nl + "/" + tot2 + " composed " + nc);
        }
        Dictionary<long, int> id = new Dictionary<long, int>(); List<int[]> nodes = new List<int[]>();
        for (int i = 0; i < N * N; i++) if (top[i] >= 0) { id[(long)i * 4096L + (long)top[i]] = nodes.Count; nodes.Add(new int[] { i % N, i / N, top[i], lay[i] }); }
        for (int k = 0; k < bridge.Count; k++) { int i = I(bridge[k][0], bridge[k][1]); long key = (long)i * 4096L + (long)bridge[k][2]; if (!id.ContainsKey(key)) { id[key] = nodes.Count; nodes.Add(new int[] { bridge[k][0], bridge[k][1], bridge[k][2], 5 }); } }
        int[] par = new int[nodes.Count]; for (int i = 0; i < par.Length; i++) par[i] = i;
        int[] dxs = { 1, 0 }, dzs = { 0, 1 };
        for (int n = 0; n < nodes.Count; n++)
            for (int d = 0; d < 2; d++)
            {
                int u = nodes[n][0] + dxs[d], v = nodes[n][1] + dzs[d]; if (!In(u, v)) continue; int i = I(u, v);
                for (int dh = -1; dh <= 1; dh++) { int m; if (id.TryGetValue((long)i * 4096L + (long)(nodes[n][2] + dh), out m)) Union(par, n, m); }
            }
        Dictionary<int, int> size = new Dictionary<int, int>(); for (int n = 0; n < nodes.Count; n++) { int r = Find(par, n); int c; size.TryGetValue(r, out c); size[r] = c + 1; }
        for (int s = 0; s < spawns.Count; s++)
        {
            int u = Rnd(spawns[s][0] + C), v = Rnd(spawns[s][1] + C); int i = I(u, v); int m;
            if (top[i] < 0 || !id.TryGetValue((long)i * 4096L + (long)top[i], out m)) { Say("spawn " + s + " has NO surface at u=" + u + " v=" + v); continue; }
            Say("spawn " + s + " layer " + (int)spawns[s][2] + " component " + size[Find(par, m)]);
        }
        int mainRoot = -1, mainSize = 0; foreach (KeyValuePair<int, int> kv in size) if (kv.Value > mainSize) { mainSize = kv.Value; mainRoot = kv.Key; }
        int[] tot = new int[6], inMain = new int[6];
        for (int n = 0; n < nodes.Count; n++) { tot[nodes[n][3]]++; if (Find(par, n) == mainRoot) inMain[nodes[n][3]]++; }
        string[] nm = { "circles", "hexagons", "galaxy", "triangles", "ramps", "bridge voxels" };
        Say("walkable components: " + size.Count + ", largest " + mainSize + " of " + nodes.Count + " surface columns");
        for (int l = 0; l < 6; l++) Say("  " + nm[l] + ": " + tot[l] + " columns, " + inMain[l] + " in the largest component");
        int asym = 0; for (int i = 0; i < N * N; i++) if (top[i] != top[N * N - 1 - i]) asym++;
        Say("C2 asymmetric columns: " + asym);
    }
    static void Ascii(string path)
    {
        StringBuilder a = new StringBuilder();
        for (int v = 0; v < N; v += 3)
        {
            for (int u = 0; u < N; u += 2)
            {
                int i = I(u, v); char ch = ' ';
                if (lay[i] >= 0) ch = "chgtr"[lay[i]];
                for (int k = 0; k < bridge.Count && ch != '+'; k++) { }
                a.Append(ch);
            }
            a.AppendLine();
        }
        File.WriteAllText(path, a.ToString());
    }
    static int Find(int[] p, int x) { while (p[x] != x) { p[x] = p[p[x]]; x = p[x]; } return x; }
    static void Union(int[] p, int a, int b) { a = Find(p, a); b = Find(p, b); if (a != b) p[a] = b; }

    // ---------------- output ----------------
    static void WriteMap(string dir)
    {
        Bitmap bm = new Bitmap(1024, 1024, PixelFormat.Format32bppArgb);
        for (int y = 0; y < 1024; y++) for (int x = 0; x < 1024; x++) bm.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0));
        int cols = 0;
        for (int v = 0; v < N; v++) for (int u = 0; u < N; u++)
            {
                int i = I(u, v); if (top[i] < 0) continue; cols++;
                bm.SetPixel(X0 + u, 1023 - (Z0 + v), Color.FromArgb(254, Math.Max(1, bot[i]), top[i], ty[i] == 0 ? 3 : ty[i]));
            }
        bm.Save(Path.Combine(dir, "Heightmap.png"), ImageFormat.Png); bm.Dispose();
        List<int[]> vox = new List<int[]>();
        for (int k = 0; k < bridge.Count; k++)
        {
            vox.Add(new int[] { X0 + bridge[k][0], bridge[k][2] - 1, Z0 + bridge[k][1], 3 });
            vox.Add(new int[] { X0 + bridge[k][0], bridge[k][2] - 2, Z0 + bridge[k][1], 8 });
        }
        Say("heightmap columns: " + cols + ", added voxels: " + vox.Count);
        StringBuilder sb = new StringBuilder("{\"addedSet\":{\"points\":[");
        for (int k = 0; k < vox.Count; k++) { if (k > 0) sb.Append(','); sb.Append("{\"x\":").Append(vox[k][0]).Append(",\"y\":").Append(vox[k][1]).Append(",\"z\":").Append(vox[k][2]).Append('}'); }
        sb.Append("]},\"pResourcesArray\":[");
        for (int k = 0; k < vox.Count; k++)
        {
            if (k > 0) sb.Append(','); int t = vox[k][3];
            sb.Append("{\"Key\":{\"x\":").Append(vox[k][0]).Append(",\"y\":").Append(vox[k][1]).Append(",\"z\":").Append(vox[k][2]).Append("},\"Value\":{\"types\":[").Append(t).Append("],\"amounts\":[10],\"MostCommonType\":").Append(t).Append(",\"TotalAmount\":10,\"TotalVolume\":4,\"All\":[{\"Key\":").Append(t).Append(",\"Value\":10}]}}");
        }
        sb.Append("]}");
        File.WriteAllText(Path.Combine(dir, "TerrainSurfaceLayer.json"), sb.ToString(), new UTF8Encoding(false));
        StringBuilder mk = new StringBuilder("[");
        for (int s = 0; s < spawns.Count; s++)
        {
            int u = Rnd(spawns[s][0] + C), v = Rnd(spawns[s][1] + C); int y = top[I(u, v)] - 1; if (s > 0) mk.Append(",");
            mk.Append(Marker("Player Starting Point", X0 + u, y, Z0 + v));
        }
        mk.Append("]");
        File.WriteAllText(Path.Combine(dir, "LuaMarkersCount.json"), mk.ToString(), new UTF8Encoding(false));
        WriteThumb(Path.Combine(dir, "thumbnail.png")); Ascii(Path.Combine(dir, "layout.txt"));
    }
    static string Marker(string group, double x, double y, double z)
    {
        System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
        string pos = "{\"$type\":\"UnityEngine.Vector3, UnityEngine\",\"x\":" + x.ToString("0.0", ci) + ",\"y\":" + y.ToString("0.0", ci) + ",\"z\":" + z.ToString("0.0", ci) + "}";
        string rot = "{\"$type\":\"UnityEngine.Quaternion, UnityEngine\",\"w\":1.0}";
        return "{\"markerTag\":\"unset\",\"markerGroups\":{\"$type\":\"System.Collections.Generic.List`1[[System.String, mscorlib]], mscorlib\",\"$values\":[\"" + group + "\"]},\"position\":" + pos + ",\"localPosition\":" + pos + ",\"rotation\":" + rot + ",\"localRotation\":" + rot + "}";
    }
    static void WriteThumb(string path)
    {
        Bitmap bm = new Bitmap(240, 128, PixelFormat.Format24bppRgb);
        Graphics g = Graphics.FromImage(bm); g.Clear(Color.FromArgb(20, 28, 44)); g.Dispose();
        Color[] cols = { Color.FromArgb(240, 200, 120), Color.FromArgb(120, 170, 220), Color.FromArgb(150, 215, 235), Color.FromArgb(235, 245, 255), Color.FromArgb(200, 120, 220) };
        for (int y = 0; y < 128; y++) for (int x = 0; x < 240; x++)
            {
                int u = (int)((x - 56) * N / 128.0), v = (int)(y * N / 128.0); if (u < 0 || v < 0 || u >= N || v >= N) continue;
                int l = lay[I(u, v)]; if (l >= 0) bm.SetPixel(x, y, cols[l]);
            }
        bm.Save(path, ImageFormat.Png); bm.Dispose();
    }
}
