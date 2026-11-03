using System.Runtime.CompilerServices;

namespace Btc.Sim;

// Engine core: state, Reset, Order queue, the Unity-like clock (fixed AI ticks + render frames), match rules, hashing.
// Mechanics: DuelSim.Grid.cs (occupancy, searches, paths), DuelSim.Loco.cs (per render frame), DuelSim.Ai.cs (per fixed tick).
public sealed partial class DuelSim
{
    // ---- internal enums (bytes for cache) ----
    private const byte TkNone = 0, TkMove = 1, TkAttack = 2, TkMoveAttack = 3, TkAttackPrio = 4, TkHold = 5;
    private const byte MoNone = 0, MoPath = 1, MoSlide = 2, MoStrike = 3;
    private const byte StNone = 0, StRaising = 1, StReady = 2, StStriking = 3, StReact = 4, StLowering = 5, StRecover = 6;
    private const byte NvIdle = 0, NvSearch = 1, NvFollow = 2, NvDone = 3, NvFailed = 4, NvGaveUp = 5;
    private const byte AiStart = 0, AiCombat = 1, AiRun = 2, AiWaitArrive = 3, AiWaitForever = 4, AiIdle = 5;

    public const float RulesStep = 0.1f;   // BtcDuelMatch.RulesStep

    // ---- extra public state ----
    public int N0, N1;
    public double RenderTime;              // time of the last simulated render frame (Unity Time.time inside Update)
    public float FightTime;                // BtcDuelMatch.FightTime (sum of render dt while the fight runs)
    public bool MechanicsEnabled = true;   // false: only clocks + rules run (log replay drives positions/hp)
    public bool RulesEnabled = true;
    public bool ControllersActive => FightTime > Rules.SettleTime;
    public int[] Cell = new int[0];        // occupied cell index (i + j*Len) per unit, -1 when dead
    public int RenderFrames;               // render frames simulated since Reset

    // ---- grid ----
    private int _w, _h, _cells, _ox, _oz;
    private float _off;
    private int[] _occHead = new int[0];   // unit+1 of the first occupant
    private int[] _occNext = new int[0];   // per unit: next occupant unit+1
    private ulong[] _acc = new ulong[0];   // per cell: units that can be attacked from here (bit = unit)
    private int[] _accStamp = new int[0];
    private int _accGen;
    private ulong _sideMask0, _sideMask1, _aliveMask;

    // ---- per unit: task / AttackInfo ----
    private byte[] _task = new byte[0], _ai = new byte[0];
    private int[] _aiFrame = new int[0], _nextMelee = new int[0], _noActionEnd = new int[0], _prep = new int[0];
    private int[] _meleeTarget = new int[0], _wsFrom = new int[0], _wsTo = new int[0], _destTarget = new int[0];
    private int[] _searchR = new int[0], _searchL = new int[0], _aiWait = new int[0];
    private bool[] _wantStrike = new bool[0], _wantStep = new bool[0], _canMove = new bool[0], _must = new bool[0], _prio = new bool[0];
    private int[] _step = new int[0], _stepLen = new int[0];
    private int[] _lastOrderFrame = new int[0];
    // busy-unit order model (Order): last receipt, its busy flag, the pending order
    private int[] _recvFrame = new int[0], _pendFrame = new int[0], _pendTarget = new int[0];
    private double[] _recvTime = new double[0];
    private bool[] _recvBusy = new bool[0], _wasMoving = new bool[0];
    private float[] _stopTime = new float[0];
    private SimOrder[] _pendKind = new SimOrder[0];
    private float[] _pendX = new float[0], _pendZ = new float[0];

    // ---- per unit: navigation ----
    private byte[] _nv = new byte[0];
    private int[] _navAt = new int[0], _navTries = new int[0], _navGoal = new int[0], _navTarget = new int[0];
    private bool[] _navAggr = new bool[0], _navFail = new bool[0];
    private float[] _navTPX = new float[0], _navTPZ = new float[0];

    // ---- per unit: locomotion ----
    private byte[] _mo = new byte[0];
    private int _maxPath;
    private int[] _path = new int[0], _pathLen = new int[0], _pathIdx = new int[0];
    private bool[] _strikePath = new bool[0], _walk = new bool[0];
    private float[] _slideX = new float[0], _slideZ = new float[0], _slideV = new float[0];
    private double[] _blockedUntil = new double[0];
    private int[] _blockedCount = new int[0];
    private float[] _yaw = new float[0], _wantYaw = new float[0];
    private bool[] _turnWanted = new bool[0];

    // ---- per unit: stance / strike ----
    private byte[] _st = new byte[0];
    private double[] _stEnd = new double[0], _dmgAt = new double[0];
    private int[] _strikeAbs = new int[0];     // Frame of the last accepted strike (skip-off guard), int.MinValue/2 none
    private int[] _prevStrikeAbs = new int[0];
    private bool[] _skipFlag = new bool[0], _fresh = new bool[0];
    private double[] _animStart = new double[0];   // AnimatorStrike: render time the crank started (-1 = strike started, first render frame pending)
    private float[] _animLen = new float[0];       // AnimatorStrike: start -> StartReacting (damage) length of the running strike
    private double[] _skipReadyAt = new double[0];

    // ---- per unit: hp ----
    private float[] _lastDamageT = new float[0], _regenT = new float[0];
    private double[] _lastHitAt = new double[0];

    // ---- match ----
    private float _rulesAcc, _healAcc;
    private string[] _reasonEnter = new string[0], _reasonSafeHit = new string[0];

    // ---- order queue ----
    private struct PendingOrder { public int Frame, Issue, Unit, Target; public SimOrder Kind; public float X, Z; public double AnimT; }
    private int _orderIssue;   // frame the order being applied was issued (Order() call), for the cancel window / restrike timings
    private double _orderAnimT; // AnimatorStrike: render time of the last animator update completed before the order was issued
    private PendingOrder[] _q = new PendingOrder[256];
    private int _qn;

    // ---- rng / clock ----
    private ulong _rng = 1;
    private double _nextFrameT, _prevRenderTime;
    private int[] _hitA = new int[256], _hitV = new int[256];   // damage applied in the last render frame, delivered at the next step (DeferDamage)
    private int _hitN;
    private double _baseDt;

    // ======================================================================================
    public partial void Reset(SimRules rules, ulong seed)
    {
        Rules = rules ?? new SimRules();
        P ??= SimParams.Default();
        int sc = Math.Max(1, Rules.ArenaScale);
        Len = 100 * sc; Wid = 20 * sc; SafeLen = 15 * sc; NeutralLen = 20 * sc; MidLen = 30 * sc;
        N0 = Math.Max(0, Rules.UnitsPerSide);
        N1 = Rules.UnitsSide1 < 0 ? N0 : Rules.UnitsSide1;
        int n = N0 + N1;
        if (n > 64) throw new ArgumentException("DuelSim supports at most 64 units");
        Alloc(n, (int)Len, (int)Wid);
        _off = P.CellCentreOffset; _ox = Rules.OriginX; _oz = Rules.OriginZ;

        _rng = (seed + 0x9E3779B97F4A7C15UL) * 0xBF58476D1CE4E5B9UL; if (_rng == 0) _rng = 1;
        Frame = 0; Time = 0f; RenderTime = 0; FightTime = 0f; RenderFrames = 0;
        float fps = Math.Max(1f, P.RenderFps);
        if (P.RenderFpsSpread > 0f) fps *= 1f + P.RenderFpsSpread * (NextU() * 2f - 1f);
        _baseDt = 1.0 / fps; _prevRenderTime = 0;
        RenderDt = SampleDt();
        _nextFrameT = RenderDt;
        Winner = -2; Reason = null;
        Escaping[0] = Escaping[1] = false; Ctrl[0] = Ctrl[1] = 0f;
        Kills[0] = Kills[1] = Hits[0] = Hits[1] = SkipHits[0] = SkipHits[1] = 0;
        _rulesAcc = _healAcc = 0f; _qn = 0; _accGen = 0; _hitN = 0;
        StatBfs = StatBfsVisits = StatAStar = StatAStarExp = StatRepath = 0;
        Array.Clear(_occHead); Array.Clear(_accStamp);
        _sideMask0 = _sideMask1 = _aliveMask = 0;

        for (int u = 0; u < n; u++)
        {
            int side = u < N0 ? 0 : 1, idx = side == 0 ? u : u - N0, cnt = side == 0 ? N0 : N1;
            Side[u] = side;
            SpawnPoint(side, idx, cnt, out float x, out float z);
            if (P.SpawnSnap) { x = CellCX(RoundX(x)); z = CellCZ(RoundZ(z)); }
            X[u] = x; Z[u] = z; VX[u] = VZ[u] = 0f;
            _yaw[u] = side == 0 ? MathF.PI * 0.5f : -MathF.PI * 0.5f;
            FX[u] = side == 0 ? 1f : -1f; FZ[u] = 0f; _turnWanted[u] = false;
            MaxHp[u] = P.MaxHp; Hp[u] = P.MaxHp; Dead[u] = false;
            Ord[u] = SimOrder.None; OrdX[u] = x; OrdZ[u] = z; OrdTarget[u] = -1; OrdAt[u] = 0f;
            AtkTarget[u] = -1; NextAttackFrame[u] = 0; StrikeFrame[u] = -1; StrikeTarget[u] = -1; StrikeAt[u] = -1000f; LastStrikeAt[u] = -1000f;
            _task[u] = TkNone; _ai[u] = AiIdle; _meleeTarget[u] = -1; _destTarget[u] = -1; _stepLen[u] = 0;
            _wantStrike[u] = _wantStep[u] = false; _prep[u] = 0; _lastOrderFrame[u] = -1000;
            _recvFrame[u] = -1000; _recvTime[u] = -1000.0; _recvBusy[u] = false; _pendFrame[u] = -1; _wasMoving[u] = false; _stopTime[u] = -1000f;
            _nv[u] = NvIdle; _navFail[u] = false;
            _mo[u] = MoNone; _pathLen[u] = 0; _pathIdx[u] = 0; _strikePath[u] = false; _walk[u] = false;
            _blockedUntil[u] = 0; _blockedCount[u] = 0;
            _st[u] = StNone; _stEnd[u] = 0; _dmgAt[u] = 0; _strikeAbs[u] = int.MinValue / 2; _prevStrikeAbs[u] = int.MinValue / 2; _skipFlag[u] = false; _fresh[u] = true; _skipReadyAt[u] = 0; _animStart[u] = 0; _animLen[u] = 0f;
            _lastDamageT[u] = -1000f; _regenT[u] = 0f; _lastHitAt[u] = 0;
            _occNext[u] = 0; Cell[u] = -1;
            OccAdd(u, CellOf(x, z));
            ulong bit = 1UL << u;
            _aliveMask |= bit;
            if (side == 0) _sideMask0 |= bit; else _sideMask1 |= bit;
            _reasonEnter[u] = "enemy unit u" + u + " entered the safe zone";
            _reasonSafeHit[u] = "unit u" + u + " attacked a unit in the safe zone";
        }
    }

    /// <summary>BtcDuelMap.SpawnPoint in arena-local coordinates (before the landing snap).</summary>
    public void SpawnPoint(int side, int i, int n, out float x, out float z)
    {
        int rows = Math.Max(1, Math.Min(n, (int)Wid - 4));
        int col = i / rows, row = i % rows;
        int inCol = Math.Min(rows, n - col * rows);
        x = side == 0 ? 2.5f + col * 1.5f : Len - 2.5f - col * 1.5f;
        z = Wid * 0.5f + (row - (inCol - 1) * 0.5f) * 1.2f;
    }

    private void Alloc(int n, int w, int h)
    {
        int cells = w * h;
        if (_cells != cells || _w != w)
        {
            _w = w; _h = h; _cells = cells;
            _occHead = new int[cells]; _acc = new ulong[cells]; _accStamp = new int[cells];
            AllocSearch(cells);
        }
        _maxPath = w + h + 160;
        if (Count == n && _path.Length == n * _maxPath) return;
        Count = n;
        Side = new int[n]; X = new float[n]; Z = new float[n]; VX = new float[n]; VZ = new float[n]; FX = new float[n]; FZ = new float[n];
        Hp = new float[n]; MaxHp = new float[n]; Dead = new bool[n];
        Ord = new SimOrder[n]; OrdX = new float[n]; OrdZ = new float[n]; OrdTarget = new int[n]; OrdAt = new float[n];
        AtkTarget = new int[n]; NextAttackFrame = new int[n]; StrikeFrame = new int[n]; StrikeTarget = new int[n]; StrikeAt = new float[n]; LastStrikeAt = new float[n];
        Cell = new int[n];
        _occNext = new int[n];
        _task = new byte[n]; _ai = new byte[n];
        _aiFrame = new int[n]; _nextMelee = new int[n]; _noActionEnd = new int[n]; _prep = new int[n];
        _meleeTarget = new int[n]; _wsFrom = new int[n]; _wsTo = new int[n]; _destTarget = new int[n];
        _searchR = new int[n]; _searchL = new int[n]; _aiWait = new int[n];
        _wantStrike = new bool[n]; _wantStep = new bool[n]; _canMove = new bool[n]; _must = new bool[n]; _prio = new bool[n];
        _step = new int[n * 4]; _stepLen = new int[n]; _lastOrderFrame = new int[n];
        _recvFrame = new int[n]; _pendFrame = new int[n]; _pendTarget = new int[n]; _recvTime = new double[n]; _recvBusy = new bool[n];
        _pendKind = new SimOrder[n]; _pendX = new float[n]; _pendZ = new float[n];
        _wasMoving = new bool[n]; _stopTime = new float[n];
        _nv = new byte[n]; _navAt = new int[n]; _navTries = new int[n]; _navGoal = new int[n]; _navTarget = new int[n];
        _navAggr = new bool[n]; _navFail = new bool[n]; _navTPX = new float[n]; _navTPZ = new float[n];
        _mo = new byte[n]; _path = new int[n * _maxPath]; _pathLen = new int[n]; _pathIdx = new int[n];
        _strikePath = new bool[n]; _walk = new bool[n];
        _slideX = new float[n]; _slideZ = new float[n]; _slideV = new float[n];
        _blockedUntil = new double[n]; _blockedCount = new int[n];
        _yaw = new float[n]; _wantYaw = new float[n]; _turnWanted = new bool[n];
        _st = new byte[n]; _stEnd = new double[n]; _dmgAt = new double[n]; _strikeAbs = new int[n]; _prevStrikeAbs = new int[n]; _skipFlag = new bool[n]; _fresh = new bool[n]; _skipReadyAt = new double[n]; _animStart = new double[n]; _animLen = new float[n];
        _lastDamageT = new float[n]; _regenT = new float[n]; _lastHitAt = new double[n];
        _reasonEnter = new string[n]; _reasonSafeHit = new string[n];
    }

    // ======================================================================================
    // orders
    public partial void Order(int unit, SimOrder kind, float x, float z, int target)
    {
        if ((uint)unit >= (uint)Count || Dead[unit]) return;
        // Busy units (running, or stopped from a Move less than BusyWindowSec ago) take a new order only after BusyOrderSec, a newer one replaces
        // the pending one with a fresh timer (latest wins): an order stream faster than ~0.38 s never changes what a unit does.
        // Measured: tests/scenarios/lab-reorder*.txt (docs/sim/measured.md "order streams"). Same-frame orders share one decision.
        if (P.BusyOrders)
        {
            bool sameFrame = _recvFrame[unit] == Frame;
            bool busy = sameFrame ? _recvBusy[unit]
                : _mo[unit] == MoPath || _pendFrame[unit] >= 0 || (Ord[unit] == SimOrder.Move && Time - _stopTime[unit] < P.BusyWindowSec);
            _recvFrame[unit] = Frame; _recvTime[unit] = Time; _recvBusy[unit] = busy;
            if (busy)
            {
                _pendFrame[unit] = Frame + Math.Max(1, (int)MathF.Round(P.BusyOrderSec / FixedDt));
                _pendKind[unit] = kind; _pendX[unit] = x; _pendZ[unit] = z; _pendTarget[unit] = target;
                return;
            }
        }
        int lat = P.OrderLatencyFrames;
        if (P.OrderLatencyJitter > 0) lat += (int)(NextU() * (P.OrderLatencyJitter + 1));
        if (_qn == _q.Length) Array.Resize(ref _q, _q.Length * 2);   // rare growth (not in steady state)
        ref PendingOrder o = ref _q[_qn++];
        o.Frame = Frame + lat; o.Issue = Frame; o.Unit = unit; o.Kind = kind; o.X = x; o.Z = z; o.Target = target;
        o.AnimT = _prevRenderTime;   // issued in Update of the last render frame: the animator has completed the updates of the frames before it
    }

    private void ApplyQueuedOrders()
    {
        if (P.BusyOrders)
            for (int u = 0; u < Count; u++)
            {
                bool mv = _mo[u] == MoPath;
                if (_wasMoving[u] && !mv) _stopTime[u] = Time;
                _wasMoving[u] = mv;
                if (_pendFrame[u] < 0 || Frame < _pendFrame[u]) continue;
                _pendFrame[u] = -1;
                if (Dead[u]) continue;
                _orderIssue = Frame; _orderAnimT = RenderTime;
                ApplyOrder(u, _pendKind[u], _pendX[u], _pendZ[u], _pendTarget[u]);
            }
        if (_qn == 0) return;
        int k = 0;
        for (int i = 0; i < _qn; i++)
        {
            PendingOrder o = _q[i];
            if (o.Frame < Frame) { _orderIssue = o.Issue; _orderAnimT = o.Frame > o.Issue ? RenderTime : o.AnimT; ApplyOrder(o.Unit, o.Kind, o.X, o.Z, o.Target); }
            else _q[k++] = o;
        }
        _qn = k;
    }

    private void ApplyOrder(int u, SimOrder kind, float x, float z, int t)
    {
        if (Dead[u]) return;
        if (kind == SimOrder.Attack && ((uint)t >= (uint)Count || Dead[t] || t == u)) return;   // invalid target: the order is never sent
        InterruptByOrder(u, kind);
        EndTask(u);
        _lastOrderFrame[u] = Frame;
        Ord[u] = kind; OrdX[u] = x; OrdZ[u] = z; OrdTarget[u] = kind == SimOrder.Attack ? t : -1; OrdAt[u] = Time;
        switch (kind)
        {
            case SimOrder.Move:
                _task[u] = TkMove;
                StartNav(u, FreeCellNear(u, CellOf(x, z)), -1, false);
                break;
            case SimOrder.Attack:
                _task[u] = TkAttack;
                InitAi(u, true, false, true, P.AttackSearchRadius, P.AttackSearchLimit, t);
                break;
            case SimOrder.MoveAttack:
                _task[u] = TkMoveAttack;
                InitAi(u, false, false, true, P.MoveAttackSearchRadius, P.MoveAttackSearchLimit, -1);
                _navGoal[u] = FreeCellNear(u, CellOf(x, z));
                break;
            default:   // Stop / None: halt
                _task[u] = TkNone; Ord[u] = SimOrder.Stop;
                ClearPath(u);
                break;
        }
    }

    /// <summary>A new task reaches a striking unit: early = strike cancelled (no damage, no refund); later = the follow-through ends early (skip).</summary>
    private void InterruptByOrder(int u, SimOrder kind)
    {
        _fresh[u] = true;   // the next strike uses the 23-tick "first strike" timing
        if (_st[u] == StStriking)
        {
            bool cancel = P.AnimatorStrike
                ? _animStart[u] < 0 || _orderAnimT - _animStart[u] < (kind == SimOrder.Move ? P.CancelCommitMoveSec : P.CancelCommitSec) - 1e-6
                : _orderIssue - StrikeFrame[u] < P.CancelWindowFrames;
            if (cancel)
            {
                AbortStrike(u);
                _stEnd[u] = Time + (P.CancelRecoverFrames - 1 - (Frame - _orderIssue)) * (double)FixedDt;   // next strike N ticks after the order was issued
            }
            else { _skipFlag[u] = true; _skipReadyAt[u] = Time + SkipRestrike() * (double)FixedDt; }
        }
        else if (_st[u] == StReact)
        {
            double e = Math.Max(RenderTime, Time + SkipRestrike() * (double)FixedDt);
            if (e < _stEnd[u]) _stEnd[u] = e;
        }
    }

    /// <summary>Ticks after the order to the next strike when an order hits a strike at K >= 9 (bimodal, measured).</summary>
    private int SkipRestrike() => (NextU() < P.SkipSlowProb ? P.SkipRestrikeSlowFrames : P.SkipRestrikeFrames) - 1 - (Frame - _orderIssue);

    private void EndTask(int u)
    {
        _task[u] = TkNone; _ai[u] = AiIdle;
        _meleeTarget[u] = -1; AtkTarget[u] = -1; _destTarget[u] = -1;
        _wantStrike[u] = _wantStep[u] = false; _stepLen[u] = 0;
        _nv[u] = NvIdle; _navFail[u] = false;
    }

    private void TaskFinished(int u)
    {
        EndTask(u);
        Ord[u] = SimOrder.None; OrdTarget[u] = -1;
    }

    // ======================================================================================
    // clock
    public partial void Step()
    {
        Frame++;
        Time = Frame * FixedDt;
        if (_hitN > 0) FlushHits();
        ApplyQueuedOrders();
        if (MechanicsEnabled) FixedTick();
        double end = (Frame + 1) * (double)FixedDt - 1e-9;
        while (_nextFrameT < end)
        {
            float dt = RenderDt;
            _prevRenderTime = RenderTime;
            RenderTime = _nextFrameT;
            RenderFrame(dt);
            RenderDt = SampleDt();             // the next frame: SimBotHost reads RenderDt for its own render step (same frame times)
            _nextFrameT += RenderDt;
        }
    }

    /// <summary>Next render frame length: per-episode base dt, uniform jitter, hitches; never below RenderMinDt. float-exact (hosts sum the same floats).</summary>
    private float SampleDt()
    {
        double dt = _baseDt;
        if (P.RenderJitter > 0f) dt *= 1.0 + P.RenderJitter * (NextU() * 2f - 1f);
        if (P.HitchProb > 0f && NextU() < P.HitchProb) dt = P.HitchMinSec + (P.HitchMaxSec - P.HitchMinSec) * NextU();
        double min = Math.Max(0.001, P.RenderMinDt);
        return (float)(dt < min ? min : dt);
    }

    private void RenderFrame(float dt)
    {
        RenderFrames++;
        if (MechanicsEnabled)
            for (int u = 0; u < Count; u++) if (!Dead[u]) Locomote(u, dt);
        if (Winner != -2 || !RulesEnabled) return;
        FightTime += dt;
        _rulesAcc += dt; _healAcc += dt;
        if (_healAcc >= P.HealStep) { if (Rules.InfiniteHeal) HealTick(); _healAcc -= P.HealStep; }
        if (_rulesAcc >= RulesStep) { RulesTick(_rulesAcc); _rulesAcc = 0f; }
        if (Winner == -2 && Rules.TimeLimit > 0f && FightTime >= Rules.TimeLimit) { if (Rules.Attacker >= 0) Finish(1 - Rules.Attacker, "time limit, defender holds"); else Finish(-1, "time limit"); }
    }

    // ======================================================================================
    // rules (BtcDuelMatch port)
    private void HealTick()
    {
        float amount = P.HealPerStep;
        for (int u = 0; u < Count; u++)
        {
            if (Dead[u]) continue;
            float room = MaxHp[u] - Hp[u];
            if (room > 0.01f) Hp[u] += Math.Min(amount, room);
        }
    }

    private void RulesTick(float dt)
    {
        int mid0 = 0, mid1 = 0, alive0 = 0, alive1 = 0, safe0 = 0, safe1 = 0;
        for (int u = 0; u < Count; u++)
        {
            if (Dead[u]) continue;
            SimZone z = ZoneAt(X[u], Z[u]);
            int s = Side[u];
            if (s == 0) { alive0++; if (z == SimZone.Mid) mid0++; if (z == SimZone.SafeA) safe0++; if (z == SimZone.SafeB) { Finish(1, _reasonEnter[u]); return; } }
            else { alive1++; if (z == SimZone.Mid) mid1++; if (z == SimZone.SafeB) safe1++; if (z == SimZone.SafeA) { Finish(0, _reasonEnter[u]); return; } }
        }
        if (alive0 == 0 && alive1 == 0) { Finish(-1, "draw, everyone dead"); return; }
        if (alive0 == 0) { Finish(1, "all enemy units dead"); return; }
        if (alive1 == 0) { Finish(0, "all enemy units dead"); return; }
        if (Escaping[0] && safe0 == alive0) { Finish(0, "kill and escape"); return; }
        if (Escaping[1] && safe1 == alive1) { Finish(1, "kill and escape"); return; }
        if (!Escaping[0] && !Escaping[1])
        {
            for (int s = 0; s < 2; s++)
            {
                bool holds = s == 0 ? (mid0 > 0 && mid1 == 0) : (mid1 > 0 && mid0 == 0);
                Ctrl[s] = holds ? Ctrl[s] + dt : 0f;
                if (Ctrl[s] >= Rules.SpaceControlSec) { Finish(s, "space control"); return; }
            }
        }
        else { Ctrl[0] = Ctrl[1] = 0f; }
    }

    /// <summary>BtcDuelMatch.OnDamage (rules part). att may be -1 (unknown attacker).</summary>
    private void OnDamageRules(int att, int victim, float dealt, float hpAfter)
    {
        if (Winner != -2 || Dead[victim]) return;
        if (dealt > 0.5f) _lastDamageT[victim] = Time;
        if (att >= 0 && Side[att] != Side[victim])
        {
            double now = RenderTime;
            double gap = _lastHitAt[att] > 0 ? (now - _lastHitAt[att]) * 1000.0 : 0.0;
            bool skip = _lastHitAt[att] > 0 && gap < 1500.0 * 0.9;
            _lastHitAt[att] = now;
            Hits[Side[att]]++; if (skip) SkipHits[Side[att]]++;
            Listener?.OnHit(att, victim, dealt, hpAfter);
            SimZone vz = ZoneAt(X[victim], Z[victim]);
            if (vz == (Side[victim] == 0 ? SimZone.SafeA : SimZone.SafeB)) { Finish(Side[victim], _reasonSafeHit[att]); return; }
        }
        if (hpAfter <= 0f) MarkDead(victim, att);
    }

    private void MarkDead(int victim, int killer)
    {
        if (Dead[victim]) return;
        KillUnit(victim);
        int ks = killer >= 0 ? Side[killer] : 1 - Side[victim];
        Kills[ks]++;
        Listener?.OnKill(victim, killer);
        if (Winner != -2) return;
        bool e0 = Kills[0] > Kills[1], e1 = Kills[1] > Kills[0];
        Escaping[0] = e0; Escaping[1] = e1;
    }

    private void Finish(int winner, string why)
    {
        if (Winner != -2) return;
        Winner = winner; Reason = why;
        Listener?.OnEnd(winner, why);
    }

    /// <summary>Mechanical death: the unit is released (frees its voxel, drops its strike and task).</summary>
    private void KillUnit(int u)
    {
        if (Dead[u]) return;
        Dead[u] = true; Hp[u] = 0f;
        OccRemove(u); Cell[u] = -1;
        _aliveMask &= ~(1UL << u);
        EndTask(u);
        _mo[u] = MoNone; _pathLen[u] = 0; _st[u] = StNone; StrikeFrame[u] = -1; StrikeTarget[u] = -1;
        VX[u] = VZ[u] = 0f;
    }

    // ======================================================================================
    // external drive (log replay, tests, bots)
    /// <summary>Teleport a unit (log replay): position, hp, alive. Clears its locomotion, stance and task.</summary>
    public void SetUnitState(int u, float x, float z, float hp, bool dead)
    {
        if (dead) { if (!Dead[u]) KillUnit(u); return; }
        if (Dead[u])
        {
            Dead[u] = false; Cell[u] = -1; _aliveMask |= 1UL << u;
        }
        X[u] = x; Z[u] = z; Hp[u] = hp; VX[u] = VZ[u] = 0f;
        _mo[u] = MoNone; _pathLen[u] = 0; _pathIdx[u] = 0; _st[u] = StNone; StrikeFrame[u] = -1;
        EndTask(u); Ord[u] = SimOrder.None;
        int c = CellOf(x, z);
        if (c != Cell[u]) { if (Cell[u] >= 0) OccRemove(u); OccAdd(u, c); }
    }

    /// <summary>Moves a unit without touching its task (log replay with MechanicsEnabled = false).</summary>
    public void SetPosition(int u, float x, float z)
    {
        if (Dead[u]) return;
        X[u] = x; Z[u] = z;
        UpdateCell(u);
    }

    /// <summary>External hit (log replay): hp already known; runs the rules exactly like the ApplyDamage hook.</summary>
    public void ExternalHit(int att, int victim, float dealt, float hpAfter)
    {
        if (Dead[victim]) return;
        Hp[victim] = Math.Max(0f, hpAfter);
        OnDamageRules(att, victim, dealt, hpAfter);
        if (Hp[victim] <= 0f && !Dead[victim]) KillUnit(victim);
    }

    public void ExternalKill(int victim, int killer)
    {
        if (Dead[victim]) return;
        if (Winner == -2) MarkDead(victim, killer); else KillUnit(victim);
    }

    // ======================================================================================
    // rng
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float NextU()
    {
        ulong x = _rng; x ^= x << 13; x ^= x >> 7; x ^= x << 17; _rng = x;
        return (x >> 40) * (1f / 16777216f);
    }

    private int RandRange(int lo, int hiExclusive) => hiExclusive <= lo ? lo : lo + (int)(NextU() * (hiExclusive - lo));

    public partial ulong StateHash()
    {
        ulong h = 1469598103934665603UL;
        h = Mix(h, (ulong)Frame); h = Mix(h, (ulong)BitConverter.DoubleToInt64Bits(RenderTime)); h = Mix(h, _rng);
        h = Mix(h, (ulong)(Winner + 3)); h = Mix(h, (ulong)Kills[0]); h = Mix(h, (ulong)Kills[1]);
        h = Mix(h, BitConverter.SingleToUInt32Bits(Ctrl[0])); h = Mix(h, BitConverter.SingleToUInt32Bits(Ctrl[1]));
        for (int u = 0; u < Count; u++)
        {
            h = Mix(h, BitConverter.SingleToUInt32Bits(X[u])); h = Mix(h, BitConverter.SingleToUInt32Bits(Z[u]));
            h = Mix(h, BitConverter.SingleToUInt32Bits(Hp[u])); h = Mix(h, BitConverter.SingleToUInt32Bits(_yaw[u]));
            h = Mix(h, (Dead[u] ? 1UL : 0UL) | ((ulong)_st[u] << 8) | ((ulong)_mo[u] << 16) | ((ulong)_task[u] << 24) | ((ulong)_nv[u] << 32) | ((ulong)_ai[u] << 40));
            h = Mix(h, (ulong)(uint)NextAttackFrame[u]); h = Mix(h, (ulong)(uint)_meleeTarget[u]); h = Mix(h, (ulong)(uint)_aiFrame[u]);
            h = Mix(h, (ulong)(uint)_pathIdx[u] | ((ulong)(uint)_pathLen[u] << 32));
        }
        return h;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mix(ulong h, ulong v) { h ^= v; h *= 1099511628211UL; h ^= h >> 29; return h; }
}
