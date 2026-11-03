namespace Btc.Shared
{
    /// <summary>
    /// Egocentric per-unit observation for the duel policy (LuminaZero v2). Shared by the lab (training) and the mod (inference):
    /// both fill a DuelView and call Encode, so the features are identical in both worlds.
    /// Mirrored for side 1: every side sees itself on the west (x' = Len - x), so one policy plays both sides.
    /// Layout (Size floats): self block, KEnemy enemy slots (nearest first), KAlly ally slots (nearest first), globals.
    /// Every unit (self, enemy, ally) carries: order kind, order destination, order target info, time since its last order,
    /// strike in progress, hp, velocity, attackers on it, unit type one-hot (all-unit training later).
    /// Changing anything here changes Schema -> old .btcpol files are rejected (schema hash).
    /// </summary>
    public static class DuelObs
    {
        public const int KEnemy = 8, KAlly = 6;
        public const int SelfSize = 43, EnemySize = 36, AllySize = 32, GlobalSize = 27;
        public const int Size = SelfSize + KEnemy * EnemySize + KAlly * AllySize + GlobalSize;
        /// <summary>Largest army per side the encoders and the central critic support.</summary>
        public const int MaxPerSide = 18;
        public const int CentralUnit = 15, CentralGlobal = 13;
        public const int CentralSize = 2 * MaxPerSide * CentralUnit + CentralGlobal;
        /// <summary>slotOut layout: [0..KEnemy) enemy slots, [KEnemy] team focus target, [KEnemy+1] enemy attacking me (-1 = none).</summary>
        public const int SlotCount = KEnemy + 2, FocusSlot = KEnemy, AttackerSlot = KEnemy + 1;
        public const string Schema = "duelobs-v3|self43|e8x36|a6x32|g27role|c553|act69";

        const float StrikeWin = 2f;     // seconds window for "time since strike" features

        [System.ThreadStatic] static int[] _atk;      // units attacking x (opposite side)
        [System.ThreadStatic] static int[] _foc;      // own-side units whose attack/order target is x

        /// <summary>Mirrored x for side s.</summary>
        static float MX(DuelView v, int side, float x) { return side == 0 ? x : v.Len - x; }

        static float Recency(float t, float win) { return t < win ? (t < 0f ? 1f : 1f - t / win) : 0f; }
        static float OrdDecay(float t) { return t > 30f ? 0f : (t < 0f ? 1f : 1f / (1f + t)); }

        /// <summary>Current attack target of unit j: strike target / attack-order target; -1 none.</summary>
        static int Tgt(DuelView v, int j)
        {
            int t = v.TargetOf[j];
            if (t < 0 && v.OrdKind[j] == 2) t = v.OrdTarget[j];
            return t >= 0 && t < v.Count && !v.Dead[t] ? t : -1;
        }

        /// <summary>
        /// Fill obs[off .. off+Size) for unit u. idxBuf (>= DuelView.MaxUnits ints) and dBuf (>= MaxUnits floats) are caller scratch.
        /// slotOut (>= SlotCount ints): enemy slots nearest first (-1 = empty), then team focus target and the enemy attacking me:
        /// DuelActions uses them for "attack slot k / focus / attacker".
        /// </summary>
        public static void Encode(DuelView v, int u, float[] obs, int off, int[] slotOut, int[] idxBuf, float[] dBuf)
        {
            int side = v.Side[u], foe = 1 - side;
            float sgn = side == 0 ? 1f : -1f;
            float ux = MX(v, side, v.X[u]), uz = v.Z[u];
            float L = v.Len, W = v.Wid;
            int o = off;
            int n = v.Count;
            if (_atk == null) { _atk = new int[DuelView.MaxUnits]; _foc = new int[DuelView.MaxUnits]; }
            int[] atk = _atk, foc = _foc;
            for (int j = 0; j < n; j++) { atk[j] = 0; foc[j] = 0; }
            for (int j = 0; j < n; j++)
            {
                if (v.Dead[j]) continue;
                int t = Tgt(v, j);
                if (t < 0 || v.Side[t] == v.Side[j]) continue;
                atk[t]++;
                if (v.Side[j] == side) foc[t]++;
            }

            // ---------- self (43) ----------
            float hpf = v.MaxHp[u] > 0f ? v.Hp[u] / v.MaxHp[u] : 0f;
            obs[o++] = hpf;
            obs[o++] = ux / L;
            obs[o++] = uz / W;
            obs[o++] = Clamp((ux - v.SafeLen) / 20f, -1f, 3f);                 // distance past own safe edge
            obs[o++] = Clamp((L - v.SafeLen - ux) / 20f, -1f, 3f);             // distance to enemy safe edge
            obs[o++] = Clamp((v.MidX0 - ux) / 20f, -3f, 3f);                   // to mid start (mirrored geometry is symmetric)
            obs[o++] = Clamp((ux - v.MidX1) / 20f, -3f, 3f);
            obs[o++] = Clamp(uz / 10f, 0f, 6f);                               // walls
            obs[o++] = Clamp((W - uz) / 10f, 0f, 6f);
            obs[o++] = sgn * v.VX[u] / 6f;
            obs[o++] = v.VZ[u] / 6f;
            obs[o++] = sgn * v.FX[u];
            obs[o++] = v.FZ[u];
            float ts = v.Time - v.StrikeAt[u];
            obs[o++] = Recency(ts, StrikeWin);
            obs[o++] = ts < 0.5f ? 1f : 0f;                                    // strike in progress (damage lands ~0.47 s)
            obs[o++] = Recency(v.Time - v.HurtAt[u], StrikeWin);
            int ok = v.OrdKind[u];
            obs[o++] = ok == 1 ? 1f : 0f; obs[o++] = ok == 2 ? 1f : 0f; obs[o++] = ok == 3 ? 1f : 0f;
            obs[o++] = v.TargetOf[u] >= 0 ? 1f : 0f;
            int z = v.ZoneAt(v.X[u], v.Z[u]);
            int zr = side == 0 ? z : (z == 0 ? 0 : 6 - z);                    // mirrored zone: 1 own safe .. 5 enemy safe
            obs[o++] = zr == 1 ? 1f : 0f; obs[o++] = zr == 2 ? 1f : 0f; obs[o++] = zr == 3 ? 1f : 0f; obs[o++] = zr == 4 ? 1f : 0f; obs[o++] = zr == 5 ? 1f : 0f;
            obs[o++] = atk[u] / 4f;
            o = OrderDest(v, u, side, ux, uz, obs, o);                         // own order destination (4)
            o = OrderTarget(v, u, u, side, obs, o, false);                     // own order target (4: has, dx, dz, dist)
            obs[o++] = OrdDecay(v.Time - v.OrdAt[u]);
            o = TypeOneHot(v, u, obs, o);

            // ---------- enemies (nearest first) ----------
            int ne = SortByDistance(v, u, foe, idxBuf, dBuf);
            // team focus: enemy most targeted by our units' strikes/orders (ties: nearest), protected enemies skipped; fallback nearest
            int focus = -1, best = -1, attacker = -1;
            for (int k = 0; k < ne; k++)
            {
                int e = idxBuf[k];
                if (v.ZoneAt(v.X[e], v.Z[e]) == DuelView.OwnSafe(foe)) continue;
                if (foc[e] > best) { best = foc[e]; focus = e; }
                if (attacker < 0 && v.TargetOf[e] == u) attacker = e;
            }
            slotOut[FocusSlot] = focus; slotOut[AttackerSlot] = attacker;
            for (int k = 0; k < KEnemy; k++)
            {
                if (k >= ne) { slotOut[k] = -1; for (int q = 0; q < EnemySize; q++) obs[o++] = 0f; continue; }
                int e = idxBuf[k]; slotOut[k] = e;
                float ex = MX(v, side, v.X[e]), ez = v.Z[e];
                float dx = ex - ux, dz = ez - uz, d = dBuf[k];
                obs[o++] = 1f;
                obs[o++] = Clamp(dx / 20f, -5f, 5f);
                obs[o++] = Clamp(dz / 20f, -5f, 5f);
                obs[o++] = Clamp(d / 20f, 0f, 5f);
                obs[o++] = d < 3.5f ? 1f : 0f;                                 // in strike reach band
                obs[o++] = sgn * v.VX[e] / 6f;
                obs[o++] = v.VZ[e] / 6f;
                obs[o++] = v.MaxHp[e] > 0f ? v.Hp[e] / v.MaxHp[e] : 0f;
                float es = v.Time - v.StrikeAt[e];
                obs[o++] = Recency(es, StrikeWin);
                obs[o++] = v.TargetOf[e] == u ? 1f : 0f;                       // it is attacking me
                int ez0 = v.ZoneAt(v.X[e], v.Z[e]);
                obs[o++] = ez0 == DuelView.OwnSafe(foe) ? 1f : 0f;             // protected: hitting it = loss
                int eo = v.OrdKind[e];
                obs[o++] = eo == 1 ? 1f : 0f; obs[o++] = eo == 2 ? 1f : 0f; obs[o++] = eo == 3 ? 1f : 0f;
                obs[o++] = foc[e] / 4f;                                        // our attackers on it
                obs[o++] = Recency(v.Time - v.HurtAt[e], StrikeWin);
                obs[o++] = es < 0.5f ? 1f : 0f;                                // its strike in progress
                o = OrderDest(v, e, side, ux, uz, obs, o);
                o = OrderTarget(v, e, u, side, obs, o, true);
                obs[o++] = OrdDecay(v.Time - v.OrdAt[e]);
                obs[o++] = e == focus ? 1f : 0f;
                o = TypeOneHot(v, e, obs, o);
            }

            // ---------- allies (nearest first, excluding self) ----------
            int na = SortByDistance(v, u, side, idxBuf, dBuf);
            int k2 = 0;
            for (int k = 0; k < na && k2 < KAlly; k++)
            {
                int a = idxBuf[k];
                if (a == u) continue;
                float ax = MX(v, side, v.X[a]);
                obs[o++] = 1f;
                obs[o++] = Clamp((ax - ux) / 20f, -5f, 5f);
                obs[o++] = Clamp((v.Z[a] - uz) / 20f, -5f, 5f);
                obs[o++] = Clamp(dBuf[k] / 20f, 0f, 5f);
                obs[o++] = v.MaxHp[a] > 0f ? v.Hp[a] / v.MaxHp[a] : 0f;
                float as0 = v.Time - v.StrikeAt[a];
                obs[o++] = Recency(as0, StrikeWin);
                obs[o++] = atk[a] / 4f;
                obs[o++] = sgn * v.VX[a] / 6f;
                obs[o++] = v.VZ[a] / 6f;
                int ao = v.OrdKind[a];
                obs[o++] = ao == 1 ? 1f : 0f; obs[o++] = ao == 2 ? 1f : 0f; obs[o++] = ao == 3 ? 1f : 0f;
                obs[o++] = as0 < 0.5f ? 1f : 0f;
                obs[o++] = Recency(v.Time - v.HurtAt[a], StrikeWin);
                o = OrderDest(v, a, side, ux, uz, obs, o);
                o = OrderTarget(v, a, u, side, obs, o, true);
                obs[o++] = OrdDecay(v.Time - v.OrdAt[a]);
                o = TypeOneHot(v, a, obs, o);
                k2++;
            }
            for (; k2 < KAlly; k2++) for (int q = 0; q < AllySize; q++) obs[o++] = 0f;

            // ---------- globals (27) ----------
            int aliveS = 0, aliveF = 0, midS = 0, midF = 0, safeS = 0;
            float hpS = 0f, hpF = 0f, cSx = 0f, cSz = 0f, cFx = 0f, cFz = 0f;
            for (int j = 0; j < n; j++)
            {
                if (v.Dead[j]) continue;
                int zj = v.ZoneAt(v.X[j], v.Z[j]);
                float h = v.MaxHp[j] > 0f ? v.Hp[j] / v.MaxHp[j] : 0f;
                if (v.Side[j] == side) { aliveS++; hpS += h; cSx += MX(v, side, v.X[j]); cSz += v.Z[j]; if (zj == 3) midS++; if (zj == DuelView.OwnSafe(side)) safeS++; }
                else { aliveF++; hpF += h; cFx += MX(v, side, v.X[j]); cFz += v.Z[j]; if (zj == 3) midF++; }
            }
            float nS = Max1(v.StartUnits[side]), nF = Max1(v.StartUnits[foe]);
            obs[o++] = aliveS / nS;
            obs[o++] = aliveF / nF;
            obs[o++] = hpS / nS;
            obs[o++] = hpF / nF;
            obs[o++] = v.Kills[side] / nF;
            obs[o++] = v.Kills[foe] / nS;
            obs[o++] = v.Escaping[side] ? 1f : 0f;
            obs[o++] = v.Escaping[foe] ? 1f : 0f;
            obs[o++] = v.Ctrl[side] / v.SpaceControlSec;
            obs[o++] = v.Ctrl[foe] / v.SpaceControlSec;
            obs[o++] = midS / nS;
            obs[o++] = midF / nF;
            obs[o++] = aliveS > 0 ? safeS / (float)aliveS : 0f;               // escaping progress
            obs[o++] = Clamp(v.Time / 120f, 0f, 3f);
            obs[o++] = v.Skips ? 1f : 0f;
            obs[o++] = v.Len > 150f ? 1f : 0f;                                 // x3 arena
            obs[o++] = v.InfiniteHeal ? 1f : 0f;
            obs[o++] = v.StartUnits[side] / (float)MaxPerSide;
            obs[o++] = v.StartUnits[foe] / (float)MaxPerSide;
            obs[o++] = (v.StartUnits[side] - v.StartUnits[foe]) / (float)MaxPerSide;   // handicap (negative = we are outnumbered)
            obs[o++] = aliveS / (float)MaxPerSide;
            obs[o++] = aliveF / (float)MaxPerSide;
            obs[o++] = aliveS > 0 ? Clamp((cSx / aliveS - ux) / 20f, -5f, 5f) : 0f;
            obs[o++] = aliveS > 0 ? Clamp((cSz / aliveS - uz) / 20f, -5f, 5f) : 0f;
            obs[o++] = aliveF > 0 ? Clamp((cFx / aliveF - ux) / 20f, -5f, 5f) : 0f;
            obs[o++] = aliveF > 0 ? Clamp((cFz / aliveF - uz) / 20f, -5f, 5f) : 0f;
            obs[o++] = v.Attacker == side ? 1f : (v.Attacker == foe ? -1f : 0f);       // role: +1 attacker (must win before the time limit), -1 defender
        }

        /// <summary>4 floats: has destination (Move / MoveAttack on the ground), destination relative to the observer /20, distance of the destination from unit w /20.</summary>
        static int OrderDest(DuelView v, int w, int side, float ux, float uz, float[] obs, int o)
        {
            int k = v.OrdKind[w];
            if (!v.OrdHasDest[w] || (k != 1 && k != 3)) { obs[o++] = 0f; obs[o++] = 0f; obs[o++] = 0f; obs[o++] = 0f; return o; }
            float dx = MX(v, side, v.OrdX[w]), dz = v.OrdZ[w];
            float ddx = dx - MX(v, side, v.X[w]), ddz = dz - v.Z[w];
            obs[o++] = 1f;
            obs[o++] = Clamp((dx - ux) / 20f, -5f, 5f);
            obs[o++] = Clamp((dz - uz) / 20f, -5f, 5f);
            obs[o++] = Clamp((float)System.Math.Sqrt(ddx * ddx + ddz * ddz) / 20f, 0f, 5f);
            return o;
        }

        /// <summary>
        /// Order-target info of unit w. others=true (5 floats): targets me / targets an ally of mine / targets an enemy of mine, target position
        /// relative to w /20. others=false (self, 4 floats): has target, position relative to self /20, distance /20.
        /// </summary>
        static int OrderTarget(DuelView v, int w, int u, int side, float[] obs, int o, bool others)
        {
            int t = v.OrdKind[w] == 2 ? v.OrdTarget[w] : -1;
            if (t < 0 || t >= v.Count || v.Dead[t])
            {
                obs[o++] = 0f; obs[o++] = 0f; obs[o++] = 0f; obs[o++] = 0f;
                if (others) obs[o++] = 0f;
                return o;
            }
            float tx = MX(v, side, v.X[t]) - MX(v, side, v.X[w]), tz = v.Z[t] - v.Z[w];
            if (others)
            {
                obs[o++] = t == u ? 1f : 0f;
                obs[o++] = (t != u && v.Side[t] == side) ? 1f : 0f;
                obs[o++] = v.Side[t] != side ? 1f : 0f;
                obs[o++] = Clamp(tx / 20f, -5f, 5f);
                obs[o++] = Clamp(tz / 20f, -5f, 5f);
            }
            else
            {
                obs[o++] = 1f;
                obs[o++] = Clamp(tx / 20f, -5f, 5f);
                obs[o++] = Clamp(tz / 20f, -5f, 5f);
                obs[o++] = Clamp((float)System.Math.Sqrt(tx * tx + tz * tz) / 20f, 0f, 5f);
            }
            return o;
        }

        static int TypeOneHot(DuelView v, int w, float[] obs, int o)
        {
            int t = v.Type[w];
            for (int q = 0; q < DuelView.TypeCount; q++) obs[o++] = q == t ? 1f : 0f;
            return o;
        }

        /// <summary>
        /// Centralised critic features for `side` (mirrored): every unit of both sides in a fixed order (own units, then enemies, view order,
        /// padded to MaxPerSide each; CentralUnit floats per unit) then CentralGlobal team-level globals. c[off .. off+CentralSize).
        /// </summary>
        public static void EncodeCentral(DuelView v, int side, float[] c, int off)
        {
            int foe = 1 - side;
            int o = off;
            for (int pass = 0; pass < 2; pass++)
            {
                int sd = pass == 0 ? side : foe, placed = 0;
                for (int j = 0; j < v.Count && placed < MaxPerSide; j++)
                {
                    if (v.Side[j] != sd) continue;
                    placed++;
                    if (v.Dead[j]) { for (int q = 0; q < CentralUnit; q++) c[o++] = 0f; continue; }
                    c[o++] = 1f;
                    c[o++] = MX(v, side, v.X[j]) / v.Len;
                    c[o++] = v.Z[j] / v.Wid;
                    c[o++] = (side == 0 ? 1f : -1f) * v.VX[j] / 6f;
                    c[o++] = v.VZ[j] / 6f;
                    c[o++] = v.MaxHp[j] > 0f ? v.Hp[j] / v.MaxHp[j] : 0f;
                    int k = v.OrdKind[j];
                    c[o++] = k == 1 ? 1f : 0f; c[o++] = k == 2 ? 1f : 0f; c[o++] = k == 3 ? 1f : 0f;
                    bool hd = v.OrdHasDest[j] && (k == 1 || k == 3);
                    c[o++] = hd ? 1f : 0f;
                    c[o++] = hd ? MX(v, side, v.OrdX[j]) / v.Len : 0f;
                    c[o++] = hd ? v.OrdZ[j] / v.Wid : 0f;
                    c[o++] = Tgt(v, j) >= 0 ? 1f : 0f;
                    c[o++] = Recency(v.Time - v.StrikeAt[j], StrikeWin);
                    c[o++] = Recency(v.Time - v.HurtAt[j], StrikeWin);
                }
                for (; placed < MaxPerSide; placed++) for (int q = 0; q < CentralUnit; q++) c[o++] = 0f;
            }
            float hpS = 0f, hpF = 0f, xS = 0f, xF = 0f; int aS = 0, aF = 0, midS = 0, midF = 0;
            for (int j = 0; j < v.Count; j++)
            {
                if (v.Dead[j]) continue;
                float h = v.MaxHp[j] > 0f ? v.Hp[j] / v.MaxHp[j] : 0f;
                float x = MX(v, side, v.X[j]) / v.Len;
                bool mid = v.ZoneAt(v.X[j], v.Z[j]) == 3;
                if (v.Side[j] == side) { aS++; hpS += h; xS += x; if (mid) midS++; }
                else { aF++; hpF += h; xF += x; if (mid) midF++; }
            }
            float nS = Max1(v.StartUnits[side]), nF = Max1(v.StartUnits[foe]);
            c[o++] = aS / nS; c[o++] = aF / nF; c[o++] = hpS / nS; c[o++] = hpF / nF;
            c[o++] = aS > 0 ? xS / aS : 0f; c[o++] = aF > 0 ? xF / aF : 0f;
            c[o++] = midS / nS; c[o++] = midF / nF;
            c[o++] = v.Escaping[side] ? 1f : 0f; c[o++] = v.Escaping[foe] ? 1f : 0f;
            c[o++] = (v.Ctrl[side] - v.Ctrl[foe]) / v.SpaceControlSec;
            c[o++] = (v.Kills[side] - v.Kills[foe]) / Max1(v.StartUnits[side] + v.StartUnits[foe]) * 2f;
            c[o++] = v.Attacker == side ? 1f : (v.Attacker == foe ? -1f : 0f);
        }

        /// <summary>Living units of `side` sorted by distance to u (insertion sort; n <= 64). Returns count.</summary>
        static int SortByDistance(DuelView v, int u, int side, int[] idx, float[] d)
        {
            int n = 0;
            float ux = v.X[u], uz = v.Z[u];
            for (int j = 0; j < v.Count; j++)
            {
                if (v.Dead[j] || v.Side[j] != side) continue;
                float dx = v.X[j] - ux, dz = v.Z[j] - uz;
                float dj = (float)System.Math.Sqrt(dx * dx + dz * dz);
                int p = n++;
                while (p > 0 && (d[p - 1] > dj || (d[p - 1] == dj && idx[p - 1] > j))) { d[p] = d[p - 1]; idx[p] = idx[p - 1]; p--; }
                d[p] = dj; idx[p] = j;
            }
            return n;
        }

        static float Clamp(float x, float lo, float hi) { return x < lo ? lo : (x > hi ? hi : x); }
        static float Max1(int n) { return n < 1 ? 1f : n; }
    }
}
