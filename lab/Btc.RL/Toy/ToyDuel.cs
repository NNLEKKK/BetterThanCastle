namespace Btc.RL.Toy;

/// <summary>
/// Deliberately simple melee micro-duel used to prove the learner before the real simulator exists.
/// Box 100 x 20 (x = length). Zones along x: safe0 [0,15) neutral [15,35) mid [35,65] neutral (65,85] safe1 (85,100].
/// Fixed 20 ms tick, one decision every 5 ticks (100 ms). Side 1 sees a mirrored (x -> 100-x) world, so both
/// sides share one egocentric observation/action layout.
/// </summary>
public sealed class ToyDuelGame
{
    public const float Width = 100f, Height = 20f, SafeLen = 15f, MidLo = 35f, MidHi = 65f;
    public const float MaxHp = 500f, Damage = 32f, Speed = 6f, Reach = 2.6f, LandRange = 3.5f, Windup = 0.47f, Cooldown = 1.5f;
    public const float Dt = 0.02f, Timeout = 120f, ControlWin = 10f;
    public const int DecisionTicks = 5;

    // actions (single head)
    public const int ActContinue = 0, ActStop = 1, ActMove0 = 2, NumDirs = 16, ActAttack0 = 18, MaxK = 3, ActRetreat = 21, NumActions = 22;
    public const int ObsSize = 46, CentralSize = 8;
    public const string Schema = "toyduel/v1|obs46|central8|heads22|allies2x4|enemies3x8";

    enum Mode : byte { Idle, Move, Attack, Retreat }

    public readonly int N; // units per side
    public readonly float[] X, Y, Hp, Cd, StrikeT;
    public readonly int[] StrikeTarget, Target, Dir;
    readonly Mode[] _mode;
    public readonly bool[] Alive;
    public float Time;
    public readonly float[] Ctrl = new float[2];
    public readonly int[] Kills = new int[2];
    public readonly float[] DmgDealt = new float[2]; // during the last Step
    public bool Done; public int Outcome; // +1 side 0 won, -1 side 1 won, 0 draw
    public float ShapeDamage = 0.5f;      // reward shaping: (dealt - taken) / MaxHp * ShapeDamage
    readonly bool[] _lose = new bool[2];
    readonly Rng _rng = new Rng(1);

    static readonly float[] DirX = new float[NumDirs], DirY = new float[NumDirs];
    static ToyDuelGame()
    {
        for (int k = 0; k < NumDirs; k++) { double a = 2 * Math.PI * k / NumDirs; DirX[k] = (float)Math.Cos(a); DirY[k] = (float)Math.Sin(a); }
    }

    public ToyDuelGame(int unitsPerSide)
    {
        N = unitsPerSide;
        int n = 2 * N;
        X = new float[n]; Y = new float[n]; Hp = new float[n]; Cd = new float[n]; StrikeT = new float[n];
        StrikeTarget = new int[n]; Target = new int[n]; Dir = new int[n]; _mode = new Mode[n]; Alive = new bool[n];
    }

    public static int SideOf(int u, int n) => u < n ? 0 : 1;
    public int Side(int u) => u < N ? 0 : 1;
    /// <summary>x in the side's own frame (0 = own back wall).</summary>
    public float EgoX(int side, float x) => side == 0 ? x : Width - x;
    public bool InOwnSafe(int u) => EgoX(Side(u), X[u]) < SafeLen;
    public bool InEnemySafe(int u) => EgoX(Side(u), X[u]) > Width - SafeLen;
    public bool InMid(int u) => X[u] >= MidLo && X[u] <= MidHi;

    public void Reset(ulong seed)
    {
        _rng.State = seed;
        for (int u = 0; u < 2 * N; u++)
        {
            float ex = 5f + 7f * _rng.NextFloat();
            X[u] = Side(u) == 0 ? ex : Width - ex;
            Y[u] = 2f + 16f * _rng.NextFloat();
            Hp[u] = MaxHp; Cd[u] = 0; StrikeT[u] = -1; StrikeTarget[u] = -1; Target[u] = -1; Dir[u] = 0;
            _mode[u] = Mode.Idle; Alive[u] = true;
        }
        Time = 0; Ctrl[0] = Ctrl[1] = 0; Kills[0] = Kills[1] = 0; Done = false; Outcome = 0;
    }

    float Dist(int a, int b) { float dx = X[a] - X[b], dy = Y[a] - Y[b]; return MathF.Sqrt(dx * dx + dy * dy); }

    /// <summary>Alive enemies of unit u sorted by distance (ties by index). Returns count; buf length >= N.</summary>
    public int SortedEnemies(int u, int[] buf)
    {
        int side = Side(u), e0 = side == 0 ? N : 0, c = 0;
        for (int e = e0; e < e0 + N; e++) if (Alive[e]) buf[c++] = e;
        for (int i = 1; i < c; i++)
        {
            int v = buf[i]; float dv = Dist(u, v); int j = i - 1;
            while (j >= 0 && Dist(u, buf[j]) > dv) { buf[j + 1] = buf[j]; j--; }
            buf[j + 1] = v;
        }
        return c;
    }

    readonly int[] _tmp = new int[8];

    /// <summary>Applies one action of unit u given in the side's egocentric frame.</summary>
    public void Order(int u, int action)
    {
        if (!Alive[u]) return;
        int side = Side(u);
        if (action == ActContinue) return;
        if (action == ActStop) { _mode[u] = Mode.Idle; return; }
        if (action >= ActMove0 && action < ActMove0 + NumDirs) { _mode[u] = Mode.Move; Dir[u] = action - ActMove0; return; }
        if (action >= ActAttack0 && action < ActAttack0 + MaxK)
        {
            int c = SortedEnemies(u, _tmp), k = action - ActAttack0;
            if (k < c) { _mode[u] = Mode.Attack; Target[u] = _tmp[k]; }
            return;
        }
        if (action == ActRetreat) _mode[u] = Mode.Retreat;
        _ = side;
    }

    /// <summary>One decision step (5 ticks). actions are per unit of each side, egocentric.</summary>
    public void Step(int[] a0, int[] a1)
    {
        for (int i = 0; i < N; i++) { Order(i, a0[i]); Order(N + i, a1 != null ? a1[i] : ActStop); }
        DmgDealt[0] = DmgDealt[1] = 0;
        for (int k = 0; k < DecisionTicks && !Done; k++) Tick();
    }

    void MoveToward(int u, float tx, float ty, float stopDist)
    {
        float dx = tx - X[u], dy = ty - Y[u];
        float d = MathF.Sqrt(dx * dx + dy * dy);
        if (d <= stopDist || d < 1e-6f) return;
        float step = MathF.Min(Speed * Dt, d - stopDist);
        X[u] += dx / d * step; Y[u] += dy / d * step;
    }

    void Tick()
    {
        int n = 2 * N;
        _lose[0] = _lose[1] = false;
        for (int u = 0; u < n; u++)
        {
            if (!Alive[u]) continue;
            if (Cd[u] > 0) Cd[u] -= Dt;
            if (StrikeT[u] >= 0)
            {
                StrikeT[u] -= Dt;
                if (StrikeT[u] <= 0)
                {
                    StrikeT[u] = -1;
                    int t = StrikeTarget[u];
                    if (t >= 0 && Alive[t] && Dist(u, t) <= LandRange)
                    {
                        if (InOwnSafe(t)) _lose[Side(u)] = true;
                        Hp[t] -= Damage; DmgDealt[Side(u)] += Damage;
                        if (Hp[t] <= 0) { Hp[t] = 0; Alive[t] = false; StrikeT[t] = -1; Kills[Side(u)]++; }
                    }
                }
                continue; // rooted while striking
            }
            int side = Side(u);
            switch (_mode[u])
            {
                case Mode.Move:
                    {
                        float dx = DirX[Dir[u]] * (side == 0 ? 1f : -1f);
                        X[u] += dx * Speed * Dt; Y[u] += DirY[Dir[u]] * Speed * Dt;
                        break;
                    }
                case Mode.Attack:
                    {
                        int t = Target[u];
                        if (t < 0 || !Alive[t]) { _mode[u] = Mode.Idle; break; }
                        float d = Dist(u, t);
                        if (d > Reach) MoveToward(u, X[t], Y[t], Reach * 0.9f);
                        else if (Cd[u] <= 0) { StrikeT[u] = Windup; StrikeTarget[u] = t; Cd[u] = Cooldown; }
                        break;
                    }
                case Mode.Retreat:
                    {
                        float tx = side == 0 ? SafeLen * 0.5f : Width - SafeLen * 0.5f;
                        if (InOwnSafe(u) && MathF.Abs(X[u] - tx) < SafeLen * 0.35f) { _mode[u] = Mode.Idle; break; }
                        MoveToward(u, tx, Y[u], 0f);
                        break;
                    }
            }
            X[u] = Math.Clamp(X[u], 0.3f, Width - 0.3f); Y[u] = Math.Clamp(Y[u], 0.3f, Height - 0.3f);
        }
        Time += Dt;
        // rules
        int alive0 = 0, alive1 = 0; bool mid0 = false, mid1 = false, home0 = true, home1 = true;
        for (int u = 0; u < n; u++)
        {
            if (!Alive[u]) continue;
            int s = Side(u);
            if (InEnemySafe(u)) _lose[s] = true;
            if (s == 0) { alive0++; mid0 |= InMid(u); home0 &= InOwnSafe(u); }
            else { alive1++; mid1 |= InMid(u); home1 &= InOwnSafe(u); }
        }
        if (_lose[0] || _lose[1]) { Finish(_lose[0] && _lose[1] ? 0 : _lose[0] ? -1 : 1); return; }
        if (alive0 == 0 || alive1 == 0) { Finish(alive0 == alive1 ? 0 : alive0 > 0 ? 1 : -1); return; }
        if (Kills[0] > Kills[1] && home0) { Finish(1); return; }
        if (Kills[1] > Kills[0] && home1) { Finish(-1); return; }
        Ctrl[0] = mid0 && !mid1 ? Ctrl[0] + Dt : 0f;
        Ctrl[1] = mid1 && !mid0 ? Ctrl[1] + Dt : 0f;
        if (Ctrl[0] >= ControlWin) { Finish(1); return; }
        if (Ctrl[1] >= ControlWin) { Finish(-1); return; }
        if (Time >= Timeout) Finish(0);
    }

    void Finish(int outcome) { Done = true; Outcome = outcome; }

    /// <summary>Egocentric observation of unit u (ObsSize floats); zeros if dead.</summary>
    public void Observe(int u, float[] o, int off)
    {
        Array.Clear(o, off, ObsSize);
        if (!Alive[u]) return;
        int side = Side(u), other = 1 - side;
        float sx = side == 0 ? 1f : -1f;
        int i = off;
        o[i++] = EgoX(side, X[u]) / Width; o[i++] = Y[u] / Height; o[i++] = Hp[u] / MaxHp;
        o[i++] = MathF.Max(Cd[u], 0) / Cooldown; o[i++] = StrikeT[u] > 0 ? StrikeT[u] / Windup : 0f;
        o[i++] = InOwnSafe(u) ? 1 : 0; o[i++] = InMid(u) ? 1 : 0; o[i++] = (Width - SafeLen - EgoX(side, X[u])) / Width;
        int aliveOwn = 0, aliveEn = 0;
        for (int v = 0; v < 2 * N; v++) if (Alive[v]) { if (Side(v) == side) aliveOwn++; else aliveEn++; }
        o[i++] = Time / Timeout; o[i++] = Ctrl[side] / ControlWin; o[i++] = Ctrl[other] / ControlWin;
        o[i++] = (Kills[side] - Kills[other]) / 3f; o[i++] = aliveOwn / 3f; o[i++] = aliveEn / 3f;
        // allies (2 slots)
        int slot = 0, a0 = side == 0 ? 0 : N;
        for (int v = a0; v < a0 + N && slot < 2; v++)
        {
            if (v == u) continue;
            if (Alive[v]) { o[i] = (X[v] - X[u]) * sx / 20f; o[i + 1] = (Y[v] - Y[u]) / 20f; o[i + 2] = Hp[v] / MaxHp; o[i + 3] = 1; }
            i += 4; slot++;
        }
        i = off + 14 + 8;
        int c = SortedEnemies(u, _tmp);
        for (int k = 0; k < 3; k++)
        {
            if (k < c)
            {
                int e = _tmp[k];
                o[i] = (X[e] - X[u]) * sx / 20f; o[i + 1] = (Y[e] - Y[u]) / 20f; o[i + 2] = Dist(u, e) / 20f;
                o[i + 3] = Hp[e] / MaxHp; o[i + 4] = MathF.Max(Cd[e], 0) / Cooldown; o[i + 5] = StrikeT[e] > 0 ? StrikeT[e] / Windup : 0f;
                o[i + 6] = InOwnSafe(e) ? 1 : 0; o[i + 7] = 1;
            }
            i += 8;
        }
    }

    public void Mask(int u, byte[] m, int off)
    {
        for (int j = 0; j < NumActions; j++) m[off + j] = 1;
        int c = Alive[u] ? SortedEnemies(u, _tmp) : 0;
        for (int k = c; k < MaxK; k++) m[off + ActAttack0 + k] = 0;
    }

    public void Central(int side, float[] o, int off)
    {
        float hpOwn = 0, hpEn = 0, xOwn = 0, xEn = 0; int nOwn = 0, nEn = 0;
        for (int v = 0; v < 2 * N; v++)
        {
            if (!Alive[v]) continue;
            if (Side(v) == side) { hpOwn += Hp[v]; xOwn += EgoX(side, X[v]); nOwn++; }
            else { hpEn += Hp[v]; xEn += EgoX(side, X[v]); nEn++; }
        }
        o[off] = hpOwn / (3 * MaxHp); o[off + 1] = hpEn / (3 * MaxHp);
        o[off + 2] = nOwn > 0 ? xOwn / nOwn / Width : 0; o[off + 3] = nEn > 0 ? xEn / nEn / Width : 0;
        o[off + 4] = nOwn / 3f; o[off + 5] = nEn / 3f;
        o[off + 6] = Ctrl[side] / ControlWin; o[off + 7] = Ctrl[1 - side] / ControlWin;
    }

    /// <summary>Order-sensitive hash of the full state (determinism tests).</summary>
    public ulong StateHash()
    {
        ulong h = 1469598103934665603UL;
        void Mix(float f) { h ^= (uint)BitConverter.SingleToInt32Bits(f); h *= 1099511628211UL; }
        for (int u = 0; u < 2 * N; u++) { Mix(X[u]); Mix(Y[u]); Mix(Hp[u]); Mix(Cd[u]); Mix(StrikeT[u]); }
        Mix(Time); Mix(Ctrl[0]); Mix(Ctrl[1]); Mix(Kills[0]); Mix(Kills[1]); Mix(Outcome);
        return h;
    }

    /// <summary>Egocentric action that moves unit u toward world point (tx, ty).</summary>
    public int MoveActionToward(int u, float tx, float ty)
    {
        float dx = (tx - X[u]) * (Side(u) == 0 ? 1f : -1f), dy = ty - Y[u];
        double ang = Math.Atan2(dy, dx);
        int k = (int)Math.Round(ang / (2 * Math.PI) * NumDirs);
        return ActMove0 + ((k % NumDirs) + NumDirs) % NumDirs;
    }
}

/// <summary>IEnvBatch over N independent ToyDuelGames. Reward: terminal +1/-1 plus damage shaping, shared by the team.</summary>
public sealed class ToyDuelBatch : IEnvBatch
{
    readonly ToyDuelGame[] _g;
    readonly int _units;
    readonly int[][] _a0, _a1;

    public ToyDuelBatch(int numEnvs, int unitsPerSide, float shapeDamage = 0.5f)
    {
        _units = unitsPerSide;
        _g = new ToyDuelGame[numEnvs]; _a0 = new int[numEnvs][]; _a1 = new int[numEnvs][];
        for (int i = 0; i < numEnvs; i++)
        {
            _g[i] = new ToyDuelGame(unitsPerSide) { ShapeDamage = shapeDamage };
            _a0[i] = new int[unitsPerSide]; _a1[i] = new int[unitsPerSide];
        }
    }

    public ToyDuelGame Game(int e) => _g[e];
    public int NumEnvs => _g.Length;
    public int MaxAgents => _units;
    public int ObsSize => ToyDuelGame.ObsSize;
    public int CentralSize => ToyDuelGame.CentralSize;
    public int[] HeadSizes { get; } = { ToyDuelGame.NumActions };
    public bool HasOpponent => true;
    public string Schema => ToyDuelGame.Schema;

    public void Reset(int env, ulong seed) => _g[env].Reset(seed);

    public void Observe(int env, int side, float[] obs, int obsOff, byte[] mask, int maskOff, float[] central, int centralOff, bool[] active, int activeOff)
    {
        var g = _g[env];
        int u0 = side == 0 ? 0 : _units;
        for (int a = 0; a < _units; a++)
        {
            int u = u0 + a;
            g.Observe(u, obs, obsOff + a * ToyDuelGame.ObsSize);
            g.Mask(u, mask, maskOff + a * ToyDuelGame.NumActions);
            if (central != null && central.Length >= centralOff + (a + 1) * ToyDuelGame.CentralSize)
                g.Central(side, central, centralOff + a * ToyDuelGame.CentralSize);
            active[activeOff + a] = g.Alive[u] && !g.Done;
        }
    }

    public void Step(int env, int[] actions0, int[] actions1, float[] rewards0, int rewOff, out bool done, out int outcome)
    {
        var g = _g[env];
        g.Step(actions0, actions1);
        float r = g.ShapeDamage * (g.DmgDealt[0] - g.DmgDealt[1]) / ToyDuelGame.MaxHp;
        if (g.Done) r += g.Outcome;
        for (int a = 0; a < _units; a++) rewards0[rewOff + a] = r;
        done = g.Done; outcome = g.Outcome;
    }
}
