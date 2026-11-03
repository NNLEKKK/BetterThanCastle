namespace Btc.Sim;

// Per fixed tick (20 ms): AttackInfo behaviour tree (ActionLoop then ObservationLoop), navigation requests, LaborInstinct, regen.
public sealed partial class DuelSim
{
    private void FixedTick()
    {
        BuildAccess();
        for (int u = 0; u < Count; u++)
        {
            if (Dead[u]) continue;
            switch (_task[u])
            {
                case TkMove:
                    NavTick(u);
                    if (_nv[u] == NvDone || _nv[u] == NvGaveUp) TaskFinished(u);
                    break;
                case TkAttack: case TkMoveAttack: case TkAttackPrio: case TkHold:
                    AiTick(u);
                    break;
            }
            if (P.InstinctEnabled && (Frame + u) % P.InstinctEveryFrames == 0 &&
                (_task[u] == TkNone || (P.InstinctPreemptsOrders && _task[u] == TkMove)))
                Instinct(u);
            if (P.RegenPerSec > 0f && Hp[u] < MaxHp[u] && Time - _lastDamageT[u] >= P.HurtDelay - 1e-4f && Time - _regenT[u] >= 1f - 1e-4f)
            {
                _regenT[u] = Time;
                Hp[u] = Math.Min(MaxHp[u], Hp[u] + P.RegenPerSec);
            }
        }
    }

    // ---------------- AttackInfo ----------------
    private void InitAi(int u, bool must, bool prio, bool canMove, int r, int limit, int destTarget)
    {
        _ai[u] = AiStart; _aiFrame[u] = 0; NextAttackFrame[u] = 0; _nextMelee[u] = 0; _noActionEnd[u] = 0; _prep[u] = 0;
        _meleeTarget[u] = -1; _wantStrike[u] = _wantStep[u] = false; _stepLen[u] = 0;
        _must[u] = must; _prio[u] = prio; _canMove[u] = canMove; _searchR[u] = r; _searchL[u] = limit; _destTarget[u] = destTarget;
        _nv[u] = NvIdle;
    }

    private void AiTick(int u)
    {
        if (_meleeTarget[u] >= 0 && Dead[_meleeTarget[u]]) _meleeTarget[u] = -1;   // released GameObject == null
        if (_destTarget[u] >= 0 && Dead[_destTarget[u]]) _destTarget[u] = -2;      // destination object gone
        if (_ai[u] == AiStart) { FindBestTarget(u, true); _ai[u] = AiIdle; }
        // ---- ActionLoop (selector, re-evaluated every tick) ----
        if (HasCombatAction(u))
        {
            if (_ai[u] != AiCombat) { _ai[u] = AiCombat; _nv[u] = NvIdle; }
            FindBestMeleeAction(u);
            DoMeleeAction(u);
        }
        else if (HasRunDestination(u))
        {
            if (_ai[u] != AiRun && _ai[u] != AiWaitArrive && _ai[u] != AiWaitForever)
            {
                _ai[u] = AiRun;
                if (_destTarget[u] >= 0) StartNav(u, Cell[_destTarget[u]], _destTarget[u], !_must[u]);
                else StartNav(u, _navGoal[u], -1, true);
            }
            if (_ai[u] == AiRun)
            {
                NavTick(u);
                if (_nv[u] == NvDone || _nv[u] == NvGaveUp) { _ai[u] = AiWaitArrive; _aiWait[u] = _aiFrame[u] + P.WaitAfterArriveFrames + 1; }
            }
            else if (_ai[u] == AiWaitArrive) { if (_aiFrame[u] >= _aiWait[u]) _ai[u] = AiWaitForever; }
            else if (!MustWait(u)) { TaskFinished(u); return; }
        }
        else if (!MustWait(u)) { TaskFinished(u); return; }
        // ---- ObservationLoop ----
        FindBestTarget(u, false);
        _aiFrame[u]++;
        AtkTarget[u] = _meleeTarget[u];
        if (_task[u] == TkMoveAttack) OrdTarget[u] = _meleeTarget[u];
    }

    /// <summary>Measured: an idle (Hold) unit attacks in place an enemy up to InstinctHoldDist (3 m, incl. offset (3,0)).</summary>
    private bool HoldClose(int u, int e)
    {
        float dx = X[e] - X[u], dz = Z[e] - Z[u];
        return dx * dx + dz * dz <= P.InstinctHoldDist * P.InstinctHoldDist + 1e-3f;
    }

    private bool MustWait(int u) => _canMove[u] && _must[u] && _destTarget[u] >= 0;

    private bool HasCombatAction(int u)
    {
        if (_meleeTarget[u] >= 0) { _noActionEnd[u] = 0; return true; }
        if (_noActionEnd[u] == 0) _noActionEnd[u] = _aiFrame[u] + P.NoActionFrames;
        return _aiFrame[u] < _noActionEnd[u];
    }

    private bool HasRunDestination(int u)
    {
        if (!_canMove[u]) return false;
        if (_task[u] == TkMoveAttack) return true;           // SpaceLocation
        return _destTarget[u] >= 0;                           // object destination (ListenToRangeInstinct or Must)
    }

    private void FindBestTarget(int u, bool force)
    {
        if (_aiFrame[u] >= _nextMelee[u] || force)
        {
            if (!(_meleeTarget[u] >= 0 && _wantStrike[u]))
            {
                FindBestMeleeTarget(u);
                _nextMelee[u] = _aiFrame[u] + P.TargetRefreshFrames;
            }
        }
    }

    private int EnteringOrOccupied(int u)
    {
        if (_mo[u] == MoPath && _pathIdx[u] < _pathLen[u])
        {
            int e = _path[u * _maxPath + _pathIdx[u]];
            if (e != Cell[u]) return e;
        }
        return Cell[u];
    }

    private void FindBestMeleeAction(int u)
    {
        _wantStrike[u] = _wantStep[u] = false;
        int tg = _meleeTarget[u];
        if (tg < 0) return;
        int tc = EnteringOrOccupied(tg), oc = Cell[u], ec = EnteringOrOccupied(u);
        if (Reach(oc, tc)) { _wantStrike[u] = true; _wsFrom[u] = oc; _wsTo[u] = tc; }
        else if (Reach(ec, tc)) { _wantStrike[u] = true; _wsFrom[u] = ec; _wsTo[u] = tc; }
        else if (_task[u] == TkHold && HoldClose(u, tg)) { _wantStrike[u] = true; _wsFrom[u] = oc; _wsTo[u] = tc; }
        else if (_stepLen[u] > 0 && _step[u * 4] != ec) _wantStep[u] = true;
    }

    private bool InStance(int u) { byte s = _st[u]; return s == StRaising || s == StReady || s == StStriking || s == StReact || s == StRecover; }

    private void DoMeleeAction(int u)
    {
        int tg = _meleeTarget[u];
        if (tg < 0) return;
        bool striking = _st[u] == StStriking || _st[u] == StReact;
        if (striking) _prep[u] = 0; else if (_prep[u] > 0) _prep[u]--;
        if (_wantStrike[u] || striking || _prep[u] > 0)
        {
            if (_st[u] == StReady && _aiFrame[u] >= NextAttackFrame[u])
            {
                _prep[u] = P.PrepareFrames;
                Strike(u);   // midpoint test always passes on flat ground without doors
            }
            else if (!InStance(u))
            {
                _prep[u] = P.PrepareFrames;
                Wield(u);
                ClearPath(u);
            }
        }
        else if (_wantStep[u])
        {
            if (!_canMove[u])
            {
                if (!InStance(u)) Wield(u);
                else if (!striking)
                {
                    float dx = X[tg] - X[u], dz = Z[tg] - Z[u];
                    float dot = dx * FX[u] + dz * FZ[u], len = MathF.Sqrt(dx * dx + dz * dz);
                    if (len > 1e-4f && dot < len * 0.70710678f) { _wantYaw[u] = MathF.Atan2(dx, dz); _turnWanted[u] = true; }
                }
            }
            else if (InStance(u)) { if (_st[u] == StReady || _st[u] == StRaising || _st[u] == StRecover) BeginLower(u); }
            else FollowStrikePath(u);
        }
    }

    private void Wield(int u)
    {
        if (_st[u] == StNone || _st[u] == StLowering)
        {
            _st[u] = StRaising; _fresh[u] = true;
            _stEnd[u] = RenderTime + P.StanceRaiseFrames * (double)FixedDt;
        }
    }

    private void ClearPath(int u)
    {
        if (_mo[u] == MoPath) StartStopSlide(u);
        _pathLen[u] = 0; _pathIdx[u] = 0;
    }

    private void FollowStrikePath(int u)
    {
        int n = _stepLen[u], bas = u * _maxPath;
        if (_mo[u] == MoPath && _strikePath[u] && _pathLen[u] == n)
        {
            bool same = true;
            for (int k = 0; k < n; k++) if (_path[bas + k] != _step[u * 4 + k]) { same = false; break; }
            if (same) return;
        }
        for (int k = 0; k < n; k++) _path[bas + k] = _step[u * 4 + k];
        BeginPath(u, n, true);
    }

    private void BeginPath(int u, int n, bool strike)
    {
        if (_mo[u] != MoPath) _walk[u] = n < 2;
        _pathLen[u] = n; _pathIdx[u] = 0; _strikePath[u] = strike;
        _mo[u] = MoPath; _blockedCount[u] = 0; _navFail[u] = false;
    }

    private void Strike(int u)
    {
        int tg = _meleeTarget[u];
        if (!Rules.Skips && (Frame - _strikeAbs[u]) * FixedDt < P.SkipGuardSec) return;   // DuelPatches cycle guard (fixed time)
        bool repeat = !_fresh[u];
        int d = repeat ? P.InStanceStrikeToDamageFrames : P.StrikeToDamageFrames;
        _fresh[u] = false;
        _prevStrikeAbs[u] = _strikeAbs[u]; _strikeAbs[u] = Frame;
        _st[u] = StStriking; _skipFlag[u] = false;
        StrikeFrame[u] = Frame; StrikeTarget[u] = tg;
        _dmgAt[u] = Time + d * (double)FixedDt;
        if (P.AnimatorStrike) { _animStart[u] = -1; _animLen[u] = repeat ? P.StrikeAnimStanceSec : P.StrikeAnimFirstSec; _dmgAt[u] = double.MaxValue; }
        if (_mo[u] == MoPath) { _pathLen[u] = 0; _pathIdx[u] = 0; }
        _mo[u] = MoStrike; _strikePath[u] = false;
        int cd = P.CooldownFrames + (P.CooldownRandFrames > 0 ? RandRange(-P.CooldownRandFrames, P.CooldownRandFrames) : 0);
        NextAttackFrame[u] = _aiFrame[u] + cd;
        LastStrikeAt[u] = StrikeAt[u]; StrikeAt[u] = Time;
        Listener?.OnStrike(u, tg);
    }

    // ---------------- navigation ----------------
    private void StartNav(int u, int goal, int target, bool aggressive)
    {
        _nv[u] = NvSearch; _navAt[u] = Frame + (_mo[u] == MoPath ? P.NavStartMovingFrames : P.NavStartFrames); _navTries[u] = 0;
        _navGoal[u] = goal; _navTarget[u] = target; _navAggr[u] = aggressive; _navFail[u] = false;
    }

    private void NavTick(int u)
    {
        int tg = _navTarget[u];
        if (tg >= 0 && Dead[tg]) { _nv[u] = NvDone; return; }
        switch (_nv[u])
        {
            case NvSearch:
                if (Frame < _navAt[u]) return;
                {
                    int goal = tg >= 0 ? Cell[tg] : _navGoal[u];
                    if (tg >= 0 ? Reach(Cell[u], goal) : Cell[u] == goal) { if (_mo[u] == MoPath) ClearPath(u); _nv[u] = NvDone; return; }   // already there: the old path (previous order) stops here
                    int n = PlanPath(u, goal, _navAggr[u], tg);
                    if (n > 0 && tg >= 0 && _tmpPath[n - 1] == goal) n--;   // stop next to the followed unit
                    if (n < 0 || (n == 0 && tg < 0)) { NavFailed(u); return; }
                    if (n > _maxPath) n = _maxPath;
                    Array.Copy(_tmpPath, 0, _path, u * _maxPath, n);
                    if (n == 0) { _nv[u] = NvDone; return; }
                    BeginPath(u, n, false);
                    if (tg >= 0) { _navTPX[u] = X[tg]; _navTPZ[u] = Z[tg]; }
                    _nv[u] = NvFollow;
                }
                return;
            case NvFollow:
                if (_navFail[u]) { _navFail[u] = false; NavFailed(u); return; }
                if (tg >= 0)
                {
                    if (Reach(Cell[u], Cell[tg])) { _nv[u] = NvDone; return; }
                    float mx = X[tg] - _navTPX[u], mz = Z[tg] - _navTPZ[u];
                    float dx = X[tg] - X[u], dz = Z[tg] - Z[u];
                    float dist = MathF.Sqrt(dx * dx + dz * dz);
                    float need = MathF.Max(P.RetargetMin, MathF.Ceiling((dist - P.RetargetOffset) / P.RetargetDiv));
                    if (mx * mx + mz * mz >= need * need) { _nv[u] = NvSearch; _navAt[u] = Frame + 1; return; }
                }
                if (_mo[u] == MoNone || (_mo[u] != MoPath && _mo[u] != MoSlide))
                {
                    int goal = tg >= 0 ? Cell[tg] : _navGoal[u];
                    if (tg < 0 && Cell[u] != goal && _navTries[u] < P.NavMaxTries) { _navTries[u]++; _nv[u] = NvSearch; _navAt[u] = Frame + 1; }
                    else if (tg >= 0) { _nv[u] = NvSearch; _navAt[u] = Frame + 1; }
                    else _nv[u] = NvDone;
                }
                return;
            case NvFailed:
                if (Frame >= _navAt[u]) { _nv[u] = NvSearch; _navAt[u] = Frame; }
                return;
        }
    }

    private void NavFailed(int u)
    {
        _navTries[u]++;
        if (_navTries[u] > P.NavMaxTries) { _nv[u] = NvGaveUp; return; }
        _nv[u] = NvFailed; _navAt[u] = Frame + P.NavRetryFrames;
    }

    // ---------------- LaborInstinct ----------------
    private void Instinct(int u)
    {
        float r2 = P.InstinctAggroRadius * P.InstinctAggroRadius;
        int best = -1; float bd = float.MaxValue;
        int lo = Side[u] == 0 ? N0 : 0, hi = Side[u] == 0 ? Count : N0;
        for (int e = lo; e < hi; e++)
        {
            if (Dead[e]) continue;
            float dx = X[e] - X[u], dz = Z[e] - Z[u], d2 = dx * dx + dz * dz;
            if (d2 > r2) continue;
            if (!P.InstinctChase && !Reach(Cell[u], Cell[e])) continue;
            if (d2 < bd) { bd = d2; best = e; }
        }
        if (best < 0) return;
        _orderIssue = Frame - 1; _orderAnimT = RenderTime; InterruptByOrder(u, SimOrder.Attack);
        EndTask(u);
        if (Reach(Cell[u], Cell[best]) || HoldClose(u, best))
        {
            _task[u] = TkHold;   // HoldPositionAndAttackWithPriority
            InitAi(u, false, true, false, P.HoldSearchRadius, P.AttackSearchLimit, best);
        }
        else
        {
            _task[u] = TkAttackPrio; // GoAttackWithPriority (chase)
            InitAi(u, false, true, true, P.AttackSearchRadius, P.AttackSearchLimit, best);
        }
        OrdTarget[u] = best;
        if (P.InstinctVisibleAsAttack) { Ord[u] = SimOrder.Attack; OrdAt[u] = Time; }   // the instinct task has a GameObject target (Task.Target) like an Attack order
    }
}
