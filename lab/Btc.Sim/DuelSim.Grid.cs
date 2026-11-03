using System.Numerics;
using System.Runtime.CompilerServices;

namespace Btc.Sim;

// Voxel grid: occupancy (VoxelPresence), attack-access masks, melee BFS (AttackInfo.FindBestMeleeTarget), path planning
// (straight staircase fast path + bounded A*), local detour (DestinationResolver.RepathMoveNext). All buffers preallocated.
public sealed partial class DuelSim
{
    // reach offsets with flat d2 in {1,2,4} (CharacterLocation.TargetReached) = cells from which a unit can be struck
    private static readonly int[] ReachDx = { 1, -1, 0, 0, 1, 1, -1, -1, 2, -2, 0, 0 };
    private static readonly int[] ReachDz = { 0, 0, 1, -1, 1, -1, 1, -1, 0, 0, 2, -2 };
    private static readonly int[] NDx = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] NDz = { 0, 0, 1, -1, 1, -1, 1, -1 };

    private int[] _bq = new int[0], _bpar = new int[0], _bstamp = new int[0];
    private int _bgen;
    private int[] _ag = new int[0], _apar = new int[0], _astamp = new int[0], _aclosed = new int[0];
    private int _agen;
    private long[] _hk = new long[0]; private int[] _hv = new int[0]; private int _hn;
    private int[] _tmpPath = new int[0], _tmpPath2 = new int[0];
    /// <summary>Diagnostics: search calls / visited nodes since Reset (perf tuning).</summary>
    public long StatBfs, StatBfsVisits, StatAStar, StatAStarExp, StatRepath;

    private void AllocSearch(int cells)
    {
        _bq = new int[cells]; _bpar = new int[cells]; _bstamp = new int[cells]; _bgen = 0;
        _ag = new int[cells]; _apar = new int[cells]; _astamp = new int[cells]; _aclosed = new int[cells]; _agen = 0;
        _hk = new long[cells * 8 + 16]; _hv = new int[cells * 8 + 16];
        _tmpPath = new int[cells + 8]; _tmpPath2 = new int[cells + 8];
    }

    // ---------------- cell math ----------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RoundX(float x) { int i = (int)MathF.Round(x - _off + _ox) - _ox; return i < 0 ? 0 : (i >= _w ? _w - 1 : i); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RoundZ(float z) { int j = (int)MathF.Round(z - _off + _oz) - _oz; return j < 0 ? 0 : (j >= _h ? _h - 1 : j); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CellCX(int i) => i + _off;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CellCZ(int j) => j + _off;

    /// <summary>Voxel (cell index i + j*Len) of an arena-local position (XYZ.FromVector3: banker's rounding).</summary>
    public int CellOf(float x, float z) => RoundX(x) + RoundZ(z) * _w;
    public float CellX(int c) => c % _w + _off;
    public float CellZ(int c) => c / _w + _off;

    // ---------------- occupancy ----------------
    private void OccAdd(int u, int c)
    {
        Cell[u] = c;
        _occNext[u] = _occHead[c];
        _occHead[c] = u + 1;
    }

    private void OccRemove(int u)
    {
        int c = Cell[u];
        if (c < 0) return;
        int prev = 0, cur = _occHead[c];
        while (cur != 0)
        {
            if (cur == u + 1) { if (prev == 0) _occHead[c] = _occNext[u]; else _occNext[prev - 1] = _occNext[u]; break; }
            prev = cur; cur = _occNext[cur - 1];
        }
        _occNext[u] = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateCell(int u)
    {
        int c = CellOf(X[u], Z[u]);
        if (c == Cell[u]) return;
        OccRemove(u); OccAdd(u, c);
    }

    /// <summary>True if a unit other than self stands in cell c (VoxelPresence).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool OccupiedByOther(int c, int self)
    {
        int h = _occHead[c];
        if (h == 0) return false;
        if (h != self + 1) return true;
        return _occNext[self] != 0;
    }

    /// <summary>First unit other than self in cell c (GetPresenceAt(...).FirstOrDefault), -1 none.</summary>
    private int OtherAt(int c, int self)
    {
        int cur = _occHead[c];
        while (cur != 0) { if (cur != self + 1) return cur - 1; cur = _occNext[cur - 1]; }
        return -1;
    }

    /// <summary>Unit standing in cell c (any), -1 none.</summary>
    public int UnitAt(int c) => (uint)c < (uint)_cells ? _occHead[c] - 1 : -1;

    /// <summary>Planner presence test: blocked unless free, self, the ignored unit, or an enemy when aggressive.</summary>
    private bool PlanBlocked(int c, int self, int ignore, bool aggressive)
    {
        int cur = _occHead[c];
        while (cur != 0)
        {
            int v = cur - 1;
            if (v != self && v != ignore && !(aggressive && Side[v] != Side[self])) return true;
            cur = _occNext[v];
        }
        return false;
    }

    // ---------------- reach ----------------
    /// <summary>CharacterLocation.TargetReachedOrAcceptable on flat ground: Manhattan <= 1, or flat d2 in {1,2,4} (clear line for d2 >= 2).</summary>
    private bool Reach(int a, int b)
    {
        if (a < 0 || b < 0) return false;
        int ax = a % _w, az = a / _w, bx = b % _w, bz = b / _w;
        int dx = ax - bx, dz = az - bz;
        int adx = dx < 0 ? -dx : dx, adz = dz < 0 ? -dz : dz;
        if (adx + adz <= 1) return true;
        int d2 = dx * dx + dz * dz;
        if (d2 != 2 && d2 != 4) return false;
        if (!P.StrikeBlockedByUnits) return true;
        if (d2 == 4) return _occHead[(ax + bx) / 2 + (az + bz) / 2 * _w] == 0;
        return _occHead[bx + az * _w] == 0 || _occHead[ax + bz * _w] == 0;
    }

    /// <summary>Public reach test between two units (occupied voxels).</summary>
    public bool InReach(int a, int b) => !Dead[a] && !Dead[b] && Reach(Cell[a], Cell[b]);

    // ---------------- access masks (VProperty.characterAttackAccess) ----------------
    private void BuildAccess()
    {
        int gen = ++_accGen;
        for (int u = 0; u < Count; u++)
        {
            if (Dead[u]) continue;
            int c = Cell[u], cx = c % _w, cz = c / _w;
            ulong bit = 1UL << u;
            for (int k = 0; k < 12; k++)
            {
                int nx = cx + ReachDx[k], nz = cz + ReachDz[k];
                if ((uint)nx >= (uint)_w || (uint)nz >= (uint)_h) continue;
                int n = nx + nz * _w;
                if (_accStamp[n] != gen) { _accStamp[n] = gen; _acc[n] = 0; }
                _acc[n] |= bit;
            }
        }
    }

    // ---------------- melee target BFS ----------------
    /// <summary>AttackInfo.FindBestMeleeTarget: BFS from the own voxel over free voxels (any character blocks), radius R, visit limit L.
    /// The first visited voxel with attack access to an accepted enemy wins; the path to it gives up to 4 combat-step nodes.</summary>
    private void FindBestMeleeTarget(int u)
    {
        _meleeTarget[u] = -1; _stepLen[u] = 0;
        int start = Cell[u];
        if (start < 0) return;
        int R = _searchR[u], R2 = R * R, limit = _searchL[u];
        int sx = start % _w, sz = start / _w;
        ulong enemies = (Side[u] == 0 ? _sideMask1 : _sideMask0) & _aliveMask;
        // fast reject: no enemy voxel within radius + 2 (access ring) of the start -> the BFS cannot find anyone
        {
            ulong m = enemies; int lim = (R + 2) * (R + 2); bool any = false;
            while (m != 0)
            {
                int e = BitOperations.TrailingZeroCount(m); m &= m - 1;
                int ec = Cell[e], dx = ec % _w - sx, dz = ec / _w - sz;
                if (dx * dx + dz * dz <= lim) { any = true; break; }
            }
            if (!any) return;
        }
        int gen = ++_bgen, head = 0, tail = 0;
        _bq[tail++] = start; _bstamp[start] = gen; _bpar[start] = -1;
        StatBfs++;
        int found = -1, foundEnemy = -1, remCell = -1, remEnemy = -1;
        int dest = _destTarget[u];
        bool must = _must[u], prioDest = _prio[u] && dest >= 0;
        int nn = P.Diagonal8 ? 8 : 4;
        for (int it = 0; it < limit && head < tail; it++)
        {
            int c = _bq[head++];
            if (_accStamp[c] == _accGen)
            {
                ulong m = _acc[c] & enemies;
                while (m != 0)
                {
                    int e = BitOperations.TrailingZeroCount(m); m &= m - 1;
                    if (e == dest) { found = c; foundEnemy = e; goto Done; }
                    if (must) { remCell = -1; remEnemy = -1; continue; }
                    if (P.AttackOnSight) { found = c; foundEnemy = e; goto Done; }
                    if (prioDest) continue;
                    if (remEnemy < 0) { remEnemy = e; remCell = c; }
                }
            }
            int cx = c % _w, cz = c / _w;
            for (int k = 0; k < nn; k++)
            {
                int nx = cx + NDx[k], nz = cz + NDz[k];
                if ((uint)nx >= (uint)_w || (uint)nz >= (uint)_h) continue;
                int ddx = nx - sx, ddz = nz - sz;
                if (ddx * ddx + ddz * ddz > R2) continue;
                int n = nx + nz * _w;
                if (_bstamp[n] == gen) continue;
                if (_occHead[n] != 0 && OccupiedByOther(n, u)) continue;
                if (k >= 4 && (OccupiedByOther(cx + NDx[k] + cz * _w, u) || OccupiedByOther(cx + (cz + NDz[k]) * _w, u))) continue;
                _bstamp[n] = gen; _bpar[n] = c; _bq[tail++] = n;
            }
        }
        StatBfsVisits += head;
        if (remEnemy >= 0) { found = remCell; foundEnemy = remEnemy; }
        if (found < 0) return;
    Done:
        if (!_canMove[u] && !Reach(EnteringOrOccupied(u), EnteringOrOccupied(foundEnemy)) && !HoldClose(u, foundEnemy)) return;   // Hold: only units already in reach
        _meleeTarget[u] = foundEnemy;
        // path start -> found (excluding the start): keep the first 4 nodes
        int len = 0;
        for (int c = found; c != start && c >= 0; c = _bpar[c]) _tmpPath[len++] = c;
        int take = len < 4 ? len : 4;
        for (int k = 0; k < take; k++) _step[u * 4 + k] = _tmpPath[len - 1 - k];
        _stepLen[u] = take;
    }

    // ---------------- path planning ----------------
    /// <summary>Plans start -> goal into _tmpPath (excluding start). Straight staircase when clear, else bounded A*. Returns node count or -1.</summary>
    private int PlanPath(int u, int goal, bool aggressive, int ignore)
    {
        int start = Cell[u];
        if (start == goal) return 0;
        int n = P.Diagonal8 ? Line8(start, goal, _tmpPath) : Staircase(start, goal, _tmpPath);
        if (!P.PlanAroundUnits) return n;
        bool clear = true;
        for (int k = 0; k < n; k++) if (_occHead[_tmpPath[k]] != 0 && PlanBlocked(_tmpPath[k], u, ignore, aggressive) && _tmpPath[k] != goal) { clear = false; break; }
        if (clear) return n;
        int r = AStar(u, start, goal, aggressive, ignore, P.PathSearchLimit);
        return r >= 0 ? r : -1;
    }

    /// <summary>4-connected straight staircase (evenly interleaved x/z steps) from a to b, excluding a.</summary>
    private int Staircase(int a, int b, int[] outp)
    {
        int x0 = a % _w, z0 = a / _w, x1 = b % _w, z1 = b / _w;
        int nx = Math.Abs(x1 - x0), nz = Math.Abs(z1 - z0), sx = Math.Sign(x1 - x0), sz = Math.Sign(z1 - z0);
        int ix = 0, iz = 0, x = x0, z = z0, n = 0;
        while (ix < nx || iz < nz)
        {
            if ((2 * ix + 1) * nz < (2 * iz + 1) * nx) { x += sx; ix++; } else { z += sz; iz++; }
            outp[n++] = x + z * _w;
        }
        return n;
    }

    private int Line8(int a, int b, int[] outp)
    {
        int x0 = a % _w, z0 = a / _w, x1 = b % _w, z1 = b / _w;
        int nx = Math.Abs(x1 - x0), nz = Math.Abs(z1 - z0), sx = Math.Sign(x1 - x0), sz = Math.Sign(z1 - z0);
        // measured: octile path runs straight along the major axis first, then the 45-degree diagonal
        int diag = Math.Min(nx, nz), straight = Math.Max(nx, nz) - diag, n = 0, x = x0, z = z0;
        bool xMajor = nx >= nz;
        for (int k = 0; k < straight; k++) { if (xMajor) x += sx; else z += sz; outp[n++] = x + z * _w; }
        for (int k = 0; k < diag; k++) { x += sx; z += sz; outp[n++] = x + z * _w; }
        return n;
    }

    private int AStar(int u, int start, int goal, bool aggressive, int ignore, int limit)
    {
        int gen = ++_agen;
        _hn = 0;
        int gx = goal % _w, gz = goal / _w;
        _astamp[start] = gen; _ag[start] = 0; _apar[start] = -1;
        HeapPush(H(start, gx, gz), start);
        int nn = P.Diagonal8 ? 8 : 4, expanded = 0;
        while (_hn > 0 && expanded < limit)
        {
            int c = HeapPop();
            if (_aclosed[c] == gen) continue;
            _aclosed[c] = gen; expanded++;
            if (c == goal) break;
            int cx = c % _w, cz = c / _w, g = _ag[c];
            for (int k = 0; k < nn; k++)
            {
                int nx = cx + NDx[k], nz = cz + NDz[k];
                if ((uint)nx >= (uint)_w || (uint)nz >= (uint)_h) continue;
                int n = nx + nz * _w;
                if (_aclosed[n] == gen) continue;
                if (n != goal && _occHead[n] != 0 && PlanBlocked(n, u, ignore, aggressive)) continue;
                if (k >= 4 && (PlanBlocked(nx + cz * _w, u, ignore, aggressive) || PlanBlocked(cx + nz * _w, u, ignore, aggressive))) continue;
                int ng = g + (k >= 4 ? 14 : 10);
                if (_astamp[n] == gen && _ag[n] <= ng) continue;
                _astamp[n] = gen; _ag[n] = ng; _apar[n] = c;
                if (_hn < _hk.Length) HeapPush((long)(ng + Heur(nx, nz, gx, gz)) << 24 | (uint)(0xFFFFFF - Math.Min(ng, 0xFFFFFF)), n);
            }
        }
        StatAStar++; StatAStarExp += expanded;
        if (_aclosed[goal] != gen) return -1;
        int len = 0;
        for (int c = goal; c != start; c = _apar[c]) _tmpPath2[len++] = c;
        for (int k = 0; k < len; k++) _tmpPath[k] = _tmpPath2[len - 1 - k];
        return len;
    }

    private long H(int c, int gx, int gz) => (long)Heur(c % _w, c / _w, gx, gz) << 24 | 0xFFFFFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Heur(int x, int z, int gx, int gz)
    {
        int dx = Math.Abs(x - gx), dz = Math.Abs(z - gz);
        if (!P.Diagonal8) return 10 * (dx + dz);
        int mn = Math.Min(dx, dz);
        return 14 * mn + 10 * (dx + dz - 2 * mn);
    }

    private void HeapPush(long key, int v)
    {
        int i = _hn++;
        while (i > 0)
        {
            int p = (i - 1) >> 1;
            if (_hk[p] <= key) break;
            _hk[i] = _hk[p]; _hv[i] = _hv[p]; i = p;
        }
        _hk[i] = key; _hv[i] = v;
    }

    private int HeapPop()
    {
        int top = _hv[0];
        long key = _hk[--_hn]; int v = _hv[_hn];
        int i = 0;
        while (true)
        {
            int l = 2 * i + 1;
            if (l >= _hn) break;
            int r = l + 1, m = (r < _hn && _hk[r] < _hk[l]) ? r : l;
            if (_hk[m] >= key) break;
            _hk[i] = _hk[m]; _hv[i] = _hv[m]; i = m;
        }
        _hk[i] = key; _hv[i] = v;
        return top;
    }

    /// <summary>DestinationResolver.RepathMoveNext: detour from the own voxel to node Current+3 (or the last), every other unit blocks. Splices on success.</summary>
    private bool Repath(int u)
    {
        int len = _pathLen[u], idx = _pathIdx[u], bas = u * _maxPath;
        if (idx >= len) return false;
        int gi = Math.Min(idx + P.RepathAhead, len - 1);
        int goal = _path[bas + gi], start = Cell[u];
        if (goal == start || OccupiedByOther(goal, u)) return false;
        StatRepath++;
        int gen = ++_bgen, head = 0, tail = 0;
        _bq[tail++] = start; _bstamp[start] = gen; _bpar[start] = -1;
        bool ok = false;
        int nn = P.Diagonal8 ? 8 : 4;
        for (int it = 0; it < P.RepathLimit && head < tail; it++)
        {
            int c = _bq[head++];
            if (c == goal) { ok = true; break; }
            int cx = c % _w, cz = c / _w;
            for (int k = 0; k < nn; k++)
            {
                int nx = cx + NDx[k], nz = cz + NDz[k];
                if ((uint)nx >= (uint)_w || (uint)nz >= (uint)_h) continue;
                int n = nx + nz * _w;
                if (_bstamp[n] == gen || OccupiedByOther(n, u)) continue;
                if (k >= 4 && (OccupiedByOther(nx + cz * _w, u) || OccupiedByOther(cx + nz * _w, u))) continue;
                _bstamp[n] = gen; _bpar[n] = c; _bq[tail++] = n;
            }
        }
        if (!ok && !(_bstamp[goal] == gen)) return false;
        int dl = 0;
        for (int c = goal; c != start; c = _bpar[c]) _tmpPath2[dl++] = c;
        int nlen = 0;
        for (int k = dl - 1; k >= 0 && nlen < _maxPath; k--) _tmpPath[nlen++] = _tmpPath2[k];
        for (int k = gi + 1; k < len && nlen < _maxPath; k++) _tmpPath[nlen++] = _path[bas + k];
        Array.Copy(_tmpPath, 0, _path, bas, nlen);
        _pathLen[u] = nlen; _pathIdx[u] = 0;
        return true;
    }

    /// <summary>Nearest voxel to c not occupied by another unit (destination resolution for point orders).</summary>
    private int FreeCellNear(int u, int c)
    {
        if (!OccupiedByOther(c, u)) return c;
        int gen = ++_bgen, head = 0, tail = 0;
        _bq[tail++] = c; _bstamp[c] = gen;
        for (int it = 0; it < 400 && head < tail; it++)
        {
            int cur = _bq[head++];
            if (!OccupiedByOther(cur, u)) return cur;
            int cx = cur % _w, cz = cur / _w;
            for (int k = 0; k < 4; k++)
            {
                int nx = cx + NDx[k], nz = cz + NDz[k];
                if ((uint)nx >= (uint)_w || (uint)nz >= (uint)_h) continue;
                int n = nx + nz * _w;
                if (_bstamp[n] == gen) continue;
                _bstamp[n] = gen; _bq[tail++] = n;
            }
        }
        return c;
    }
}
