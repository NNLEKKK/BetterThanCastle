using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Local unit-aware path planner for the duel bot (the game's voxel pathfinder ignores bodies, but bricktrons cannot walk through each other).
    /// 1 m grid over a window around start/goal: enemy bodies are walls, cells within strike reach of an enemy are expensive (free hits),
    /// friends are a small cost, the enemy safe zone (+3 m) and the arena edge are walls. A* (8-neighbour) with fixed arrays, no allocations.
    /// </summary>
    internal static class BtcDuelPath
    {
        private const int N = 64, MaxExpand = 2600;
        private const float Danger = 5f, Reach = 3.4f;
#if SIM
        // lab simulator: per-thread scratch (parallel matches); same names, lazily allocated
        [System.ThreadStatic] private static float[] _tCost, _tG, _tHeapF;
        [System.ThreadStatic] private static int[] _tParent, _tSeen, _tDone, _tRoute, _tHeap;
        private static float[] Cost { get { return _tCost ?? (_tCost = new float[N * N]); } }
        private static float[] G { get { return _tG ?? (_tG = new float[N * N]); } }
        private static int[] Parent { get { return _tParent ?? (_tParent = new int[N * N]); } }
        private static int[] Seen { get { return _tSeen ?? (_tSeen = new int[N * N]); } }
        private static int[] Done { get { return _tDone ?? (_tDone = new int[N * N]); } }
        private static int[] Route { get { return _tRoute ?? (_tRoute = new int[N * N]); } }
        private static int[] Heap { get { return _tHeap ?? (_tHeap = new int[N * N * 4]); } }
        private static float[] HeapF { get { return _tHeapF ?? (_tHeapF = new float[N * N * 4]); } }
#else
        private static readonly float[] Cost = new float[N * N], G = new float[N * N];
        private static readonly int[] Parent = new int[N * N], Seen = new int[N * N], Done = new int[N * N], Route = new int[N * N];
        private static readonly int[] Heap = new int[N * N * 4];
        private static readonly float[] HeapF = new float[N * N * 4];      // key per heap entry (a cell can be pushed again with a better key)
#endif
#if SIM
        [System.ThreadStatic]
#endif
        private static int _stamp, _heapN;
#if SIM
        [System.ThreadStatic]
#endif
        private static float _ox, _oz;
#if SIM
        [System.ThreadStatic]
#endif
        private static int _w, _h;
        private static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 }, DZ = { 0, 0, 1, -1, 1, -1, 1, -1 };
        private static readonly float[] DL = { 1f, 1f, 1f, 1f, 1.4142f, 1.4142f, 1.4142f, 1.4142f };
#if SIM
        [System.ThreadStatic]
#endif
        internal static int Plans;   // per-second counter for the log

        /// <summary>Plans from -> to. Returns the path length in metres (-1 = no path found within the window/expansion limit),
        /// <paramref name="danger"/> = metres of the path inside enemy reach, <paramref name="waypoint"/> = point to steer to now (furthest clear point on the path).</summary>
        internal static float Plan(Vector3 from, Vector3 to, BtcDuelUnitRef self, BtcDuelUnitRef target, List<BtcDuelUnitRef> my, List<BtcDuelUnitRef> foe,
                                   BtcDuelZone enemySafe, float forbidX, int dir, out Vector3 waypoint, out float danger)
        {
            Plans++;
            waypoint = to; danger = 0f;
            Vector3 goal = to;
            float span = N - 14f;
            Vector3 d0 = goal - from; d0.y = 0f;
            if (d0.magnitude > span) goal = from + d0.normalized * span;      // far goal: plan the first stretch
            _ox = Mathf.Floor(Mathf.Min(from.x, goal.x)) - 7f; _oz = Mathf.Floor(Mathf.Min(from.z, goal.z)) - 7f;
            _w = Mathf.Min(N, (int)(Mathf.Abs(goal.x - from.x)) + 15); _h = Mathf.Min(N, (int)(Mathf.Abs(goal.z - from.z)) + 15);
            float ax0 = BtcDuelMap.X0 + 0.5f, ax1 = BtcDuelMap.X0 + BtcDuelMap.Len - 0.5f, az0 = BtcDuelMap.Z0 + 0.5f, az1 = BtcDuelMap.Z0 + BtcDuelMap.Wid - 0.5f;
            for (int z = 0; z < _h; z++)
                for (int x = 0; x < _w; x++)
                {
                    float wx = _ox + x + 0.5f, wz = _oz + z + 0.5f;
                    bool wall = wx < ax0 || wx > ax1 || wz < az0 || wz > az1 || (dir > 0 ? wx > forbidX : wx < forbidX);
                    Cost[z * N + x] = wall ? -1f : 0f;
                }
            for (int j = 0; j < foe.Count; j++)
            {
                BtcDuelUnitRef e = foe[j];
                if (e == target || e.Zone == enemySafe) continue;
                Vector3 p = e.Tf.position;
                int cx = (int)(p.x - _ox), cz = (int)(p.z - _oz);
                int r = 4;
                for (int z = cz - r; z <= cz + r; z++)
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        if (x < 0 || z < 0 || x >= _w || z >= _h) continue;
                        int k = z * N + x;
                        if (Cost[k] < 0f) continue;
                        float dx = _ox + x + 0.5f - p.x, dz = _oz + z + 0.5f - p.z, dd = dx * dx + dz * dz;
                        if (dd < 0.56f) Cost[k] = -1f;                 // the body itself
                        else if (dd < Reach * Reach) Cost[k] += Danger;
                    }
            }
            for (int i = 0; i < my.Count; i++)
            {
                BtcDuelUnitRef u = my[i];
                if (u == self) continue;
                int cx = (int)(u.Tf.position.x - _ox), cz = (int)(u.Tf.position.z - _oz);
                if (cx < 0 || cz < 0 || cx >= _w || cz >= _h) continue;
                int k = cz * N + cx;
                if (Cost[k] >= 0f) Cost[k] += 1.5f;
            }
            int sx = Mathf.Clamp((int)(from.x - _ox), 0, _w - 1), sz = Mathf.Clamp((int)(from.z - _oz), 0, _h - 1);
            int gx = Mathf.Clamp((int)(goal.x - _ox), 0, _w - 1), gz = Mathf.Clamp((int)(goal.z - _oz), 0, _h - 1);
            int start = sz * N + sx, gk = gz * N + gx;
            if (Cost[start] < 0f) Cost[start] = 0f;
            if (Cost[gk] < 0f) Cost[gk] = 0f;
            // A*
            _stamp++; _heapN = 0;
            G[start] = 0f; Parent[start] = -1; Seen[start] = _stamp; Push(start, H(sx, sz, gx, gz));
            int expanded = 0; bool found = false;
            while (_heapN > 0 && expanded < MaxExpand)
            {
                int c = Pop();
                if (Done[c] == _stamp) continue;
                Done[c] = _stamp; expanded++;
                if (c == gk) { found = true; break; }
                int cx = c % N, cz = c / N;
                for (int q = 0; q < 8; q++)
                {
                    int nx = cx + DX[q], nz = cz + DZ[q];
                    if (nx < 0 || nz < 0 || nx >= _w || nz >= _h) continue;
                    int nk = nz * N + nx;
                    if (Cost[nk] < 0f || Done[nk] == _stamp) continue;
                    if (q >= 4 && (Cost[cz * N + nx] < 0f || Cost[nz * N + cx] < 0f)) continue;     // no corner cutting between bodies
                    float ng = G[c] + DL[q] * (1f + Cost[nk]);
                    if (Seen[nk] == _stamp && ng >= G[nk]) continue;
                    Seen[nk] = _stamp; G[nk] = ng; Parent[nk] = c;
                    Push(nk, ng + H(nx, nz, gx, gz));
                }
            }
            if (!found) return -1f;
            // route (start..goal), geometric length, metres in danger
            int n = 0;
            for (int c = gk; c != -1 && n < Route.Length; c = Parent[c]) Route[n++] = c;
            float len = 0f;
            for (int i = n - 1; i > 0; i--)
            {
                int a = Route[i], b = Route[i - 1];
                float l = (a % N != b % N && a / N != b / N) ? 1.4142f : 1f;
                len += l;
                if (Cost[b] >= Danger) danger += l;
            }
            // steer point: furthest route cell (<= 18 m) reachable in a straight line without touching walls or more danger than the route itself.
            // Never the start cell (a unit ordered onto its own position stands AFK); at least ~3 m along the route.
            float routeMax = 0f; int best = n - 1, minIdx = Mathf.Max(0, n - 4);
            for (int i = n - 2; i >= 0; i--)
            {
                int c = Route[i];
                routeMax = Mathf.Max(routeMax, Cost[c]);
                Vector3 pc = Centre(c);
                if (Mathf.Abs(pc.x - from.x) + Mathf.Abs(pc.z - from.z) > 18f) break;
                if (i < minIdx && !Clear(from, pc, routeMax + 0.5f)) break;
                best = i;
            }
            if (best >= n - 1) best = Mathf.Max(0, n - 2);
            waypoint = best == 0 && (goal - to).sqrMagnitude < 0.01f ? to : Centre(Route[best]);
            if ((waypoint - from).sqrMagnitude < 1f) waypoint = to;
            waypoint.y = to.y;
            return len + Mathf.Max(0f, Vector3.Distance(goal, to));
        }

        private static Vector3 Centre(int c) { return new Vector3(_ox + c % N + 0.5f, 0f, _oz + c / N + 0.5f); }

        private static bool Clear(Vector3 a, Vector3 b, float maxCost)
        {
            float dx = b.x - a.x, dz = b.z - a.z, d = Mathf.Sqrt(dx * dx + dz * dz);
            int steps = Mathf.Max(1, (int)(d * 2f));
            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps;
                if (d * t < 0.8f) continue;
                int x = (int)(a.x + dx * t - _ox), z = (int)(a.z + dz * t - _oz);
                if (x < 0 || z < 0 || x >= _w || z >= _h) return false;
                float c = Cost[z * N + x];
                if (c < 0f || c > maxCost) return false;
            }
            return true;
        }

        private static float H(int x, int z, int gx, int gz)
        {
            int dx = Mathf.Abs(x - gx), dz = Mathf.Abs(z - gz);
            return (dx + dz) + (1.4142f - 2f) * Mathf.Min(dx, dz);
        }

        private static void Push(int k, float f)
        {
            if (_heapN >= Heap.Length) return;
            int i = _heapN++;
            Heap[i] = k; HeapF[i] = f;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (HeapF[p] <= HeapF[i]) break;
                int t = Heap[p]; Heap[p] = Heap[i]; Heap[i] = t;
                float tf = HeapF[p]; HeapF[p] = HeapF[i]; HeapF[i] = tf;
                i = p;
            }
        }

        private static int Pop()
        {
            int top = Heap[0];
            _heapN--;
            Heap[0] = Heap[_heapN]; HeapF[0] = HeapF[_heapN];
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < _heapN && HeapF[l] < HeapF[m]) m = l;
                if (r < _heapN && HeapF[r] < HeapF[m]) m = r;
                if (m == i) break;
                int t = Heap[m]; Heap[m] = Heap[i]; Heap[i] = t;
                float tf = HeapF[m]; HeapF[m] = HeapF[i]; HeapF[i] = tf;
                i = m;
            }
            return top;
        }
    }
}
