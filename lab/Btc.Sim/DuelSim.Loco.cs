using System.Runtime.CompilerServices;

namespace Btc.Sim;

// Locomotion4 equivalent, run once per RENDER frame (Time.deltaTime): path following with the (a+2b+c)/4 corner-cut target,
// node advance by voxel rounding (FastForwardPath), blocking (TryPushingForward), arrival slide, strike match, stance timers,
// strike resolution (damage in StartReacting) and facing.
public sealed partial class DuelSim
{
    private void Locomote(int u, float dt)
    {
        double t = RenderTime;
        float x0 = X[u], z0 = Z[u];
        switch (_st[u])
        {
            case StRaising: if (t >= _stEnd[u]) _st[u] = StReady; break;
            case StLowering: if (t >= _stEnd[u]) _st[u] = StNone; break;
            case StRecover: if (t >= _stEnd[u]) _st[u] = StReady; break;
            case StStriking: StrikeUpdate(u, t); if (Dead[u]) return; break;
            case StReact: if (t >= _stEnd[u]) { _st[u] = StReady; StrikeFrame[u] = -1; } break;
        }
        switch (_mo[u])
        {
            case MoPath:
                byte st = _st[u];
                if (st == StNone) MovePath(u, dt);
                else if (st == StReady || st == StRaising || st == StRecover) BeginLower(u);   // StopFighting before following a path
                break;
            case MoSlide: MoveSlide(u, dt); break;
            case MoStrike: MoveStrike(u, dt); break;
            default:
                if (_turnWanted[u]) TurnTo(u, _wantYaw[u], P.TurnRateDeg, dt);
                break;
        }
        float inv = dt > 0f ? 1f / dt : 0f;
        VX[u] = (X[u] - x0) * inv; VZ[u] = (Z[u] - z0) * inv;
    }

    private void BeginLower(int u)
    {
        _st[u] = StLowering;
        _stEnd[u] = RenderTime + P.StanceLowerFrames * (double)FixedDt;
    }

    // ---------------- path following ----------------
    private void MovePath(int u, float dt)
    {
        float rem = dt;
        int bas = u * _maxPath;
        for (int iter = 0; iter < 4 && rem > 1e-6f; iter++)
        {
            if (RenderTime < _blockedUntil[u]) return;
            FastForward(u);
            int idx = _pathIdx[u];
            if (idx >= _pathLen[u]) { StartStopSlide(u); MoveSlide(u, rem); return; }
            int ent = _path[bas + idx];
            if (ent != Cell[u] && _occHead[ent] != 0)
            {
                int blk = OtherAt(ent, u);
                if (blk >= 0)
                {
                    if (Repath(u)) { }
                    else if (CanPassThrough(u, blk)) { }
                    else if (IsIdleBlocker(blk) || _blockedCount[u] >= P.BlockedMaxCount) { FailPath(u); return; }
                    else { WaitActively(u); return; }
                }
            }
            _blockedCount[u] = 0;
            idx = _pathIdx[u];
            if (idx >= _pathLen[u]) { StartStopSlide(u); MoveSlide(u, rem); return; }
            CurrentTarget(u, out float tx, out float tz);
            float sp = PathSpeed(u);
            float dx = tx - X[u], dz = tz - Z[u];
            float d = MathF.Sqrt(dx * dx + dz * dz);
            if (d > 1e-5f) TurnToDir(u, dx, dz, P.TurnRateDeg, rem);
            float step = sp * rem;
            if (step >= d)
            {
                X[u] = tx; Z[u] = tz; rem -= d / sp;
                UpdateCell(u);
                if (d < 1e-4f) break;
            }
            else
            {
                float k = step / d;
                X[u] += dx * k; Z[u] += dz * k; rem = 0f;
                UpdateCell(u);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FastForward(int u)
    {
        int len = _pathLen[u], idx = _pathIdx[u], c = Cell[u], bas = u * _maxPath;
        int from = idx > 0 ? idx - 1 : 0, to = Math.Min(len, idx + 8);
        for (int n = from; n < to; n++)
            if (_path[bas + n] == c) { if (n >= idx) _pathIdx[u] = n + 1; break; }
    }

    /// <summary>LocomotionWalkableCycleState.GetCurrentTarget: ((a+b)/2 + (b+c)/2)/2, or 10% past the last edge toward the last node.</summary>
    private void CurrentTarget(int u, out float tx, out float tz)
    {
        int bas = u * _maxPath, idx = _pathIdx[u];
        int a = Cell[u], b = _path[bas + idx];
        float ax = a % _w, az = a / _w, bx = b % _w, bz = b / _w;
        float ex = (ax + bx) * 0.5f, ez = (az + bz) * 0.5f;
        if (idx + 1 < _pathLen[u])
        {
            int c = _path[bas + idx + 1];
            float cx = c % _w, cz = c / _w;
            tx = (ex + (bx + cx) * 0.5f) * 0.5f + _off;
            tz = (ez + (bz + cz) * 0.5f) * 0.5f + _off;
        }
        else
        {
            tx = ex + (bx - ex) * 0.1f + _off;
            tz = ez + (bz - ez) * 0.1f + _off;
        }
    }

    private float PathSpeed(int u)
    {
        if (_walk[u] && _pathIdx[u] + 1 < _pathLen[u]) _walk[u] = false;   // Walking.Update upgrades to Run when Peek exists
        if (_strikePath[u]) return _walk[u] ? P.WalkSpeed : StrikeRunSpeed(u);
        if (_pathIdx[u] + 1 >= _pathLen[u] && P.ArrivalDistance > 0f)
        {
            int last = _path[u * _maxPath + _pathLen[u] - 1];
            float dx = CellX(last) - X[u], dz = CellZ(last) - Z[u];
            if (dx * dx + dz * dz <= P.ArrivalDistance * P.ArrivalDistance) return P.ArriveSpeed;   // RunArrivalDistance
        }
        return _walk[u] ? P.WalkSpeed : P.RunSpeed;
    }

    /// <summary>Running.GetStrikingRunSpeed: 6 + 3 * clamp01(1 - |destination - position| / 4).</summary>
    private float StrikeRunSpeed(int u)
    {
        float d = 0f;
        int len = _pathLen[u];
        if (len > 0)
        {
            int last = _path[u * _maxPath + len - 1];
            float dx = CellX(last) - X[u], dz = CellZ(last) - Z[u];
            d = MathF.Sqrt(dx * dx + dz * dz);
        }
        float k = 1f - d / P.StrikeRunBonusDist;
        k = k < 0f ? 0f : (k > 1f ? 1f : k);
        return P.RunSpeed + P.StrikeRunBonus * k;
    }

    private bool IsFighting(int u) { byte k = _task[u]; return k == TkAttack || k == TkMoveAttack || k == TkAttackPrio || k == TkHold; }
    private bool CanPassThrough(int u, int blk) => P.AllyPassThrough && Side[blk] == Side[u] && !IsFighting(u);
    private bool IsIdleBlocker(int b) => !(_mo[b] == MoPath && _pathIdx[b] < _pathLen[b]);

    private void WaitActively(int u)
    {
        float rx = CellX(CellOf(X[u], Z[u])), rz = CellZ(CellOf(X[u], Z[u]));
        X[u] += (rx - X[u]) * P.BlockedMoveFactor; Z[u] += (rz - Z[u]) * P.BlockedMoveFactor;
        _blockedUntil[u] = RenderTime + P.BlockedDelay;
        _blockedCount[u]++;
    }

    private void FailPath(int u)
    {
        _blockedCount[u] = 0;
        _navFail[u] = true;
        _strikePath[u] = false;
        StartStopSlide(u);
    }

    /// <summary>Running.RunStop: slide to the voxel centre at 2 m/s (strike-run speed on strike paths).</summary>
    private void StartStopSlide(int u)
    {
        float sp = _strikePath[u] && _mo[u] == MoPath ? StrikeRunSpeed(u) : P.ArriveSpeed;
        int c = CellOf(X[u], Z[u]);
        _slideX[u] = CellX(c); _slideZ[u] = CellZ(c); _slideV[u] = sp;
        _mo[u] = MoSlide; _pathLen[u] = 0; _pathIdx[u] = 0;
    }

    private void MoveSlide(int u, float dt)
    {
        float dx = _slideX[u] - X[u], dz = _slideZ[u] - Z[u];
        float d = MathF.Sqrt(dx * dx + dz * dz), step = _slideV[u] * dt;
        if (step >= d) { X[u] = _slideX[u]; Z[u] = _slideZ[u]; _mo[u] = MoNone; _strikePath[u] = false; }
        else { float k = step / d; X[u] += dx * k; Z[u] += dz * k; }
        UpdateCell(u);
        if (_turnWanted[u]) TurnTo(u, _wantYaw[u], P.TurnOnSpotDeg, dt);
    }

    /// <summary>Strike match (MatchTarget onto StrikeEndPosition = WantStrikeFrom) at combat-step speed; WantsStop re-targets the own voxel.</summary>
    private void MoveStrike(int u, float dt)
    {
        int from = _wsFrom[u], cur = Cell[u];
        int dst = from;
        if (from != cur)
        {
            int fx = from % _w, fz = from / _w, cx = cur % _w, cz = cur / _w;
            if (Math.Abs(fx - cx) + Math.Abs(fz - cz) != 1 || OccupiedByOther(from, u)) dst = cur;
        }
        float tx = CellX(dst), tz = CellZ(dst);
        float dx = tx - X[u], dz = tz - Z[u];
        float d = MathF.Sqrt(dx * dx + dz * dz), step = P.CombatStepSpeed * dt;
        if (d > 1e-5f)
        {
            if (step >= d) { X[u] = tx; Z[u] = tz; }
            else { float k = step / d; X[u] += dx * k; Z[u] += dz * k; }
            UpdateCell(u);
        }
        int tg = StrikeTarget[u];
        if (tg >= 0 && !Dead[tg]) TurnToDir(u, X[tg] - X[u], Z[tg] - Z[u], P.TurnOnSpotDeg, dt);
    }

    // ---------------- strike resolution ----------------
    private void StrikeUpdate(int u, double t)
    {
        int tg = StrikeTarget[u];
        if (tg < 0 || Dead[tg]) { AbortStrike(u); return; }
        float dx = X[tg] - X[u], dz = Z[tg] - Z[u];
        if (dx * dx + dz * dz > P.StrikeAbortDist * P.StrikeAbortDist) { AbortStrike(u); return; }
        if (P.AnimatorStrike && _animStart[u] < 0) { _animStart[u] = t; _dmgAt[u] = t + _animLen[u] - 1e-6; }   // crank starts on this render frame
        if (t < _dmgAt[u]) return;
        // StartReacting -> SwordStrike.Apply
        _st[u] = StReact;
        _stEnd[u] = _dmgAt[u] + P.ReactFrames * (double)FixedDt;
        if (_skipFlag[u]) _stEnd[u] = Math.Min(_stEnd[u], Math.Max(_dmgAt[u], _skipReadyAt[u]));
        if (_mo[u] == MoStrike) _mo[u] = MoNone;
        if (P.DeferDamage) { if (_hitN < _hitA.Length) { _hitA[_hitN] = u; _hitV[_hitN] = tg; _hitN++; } return; }
        ApplyHit(u, tg);
    }

    /// <summary>SwordStrike.Apply -> ReceiveAttack -> ApplyDamage (+ the mod OnDamage rules).</summary>
    private void ApplyHit(int u, int tg)
    {
        if (Dead[tg]) return;
        float before = Hp[tg];
        float after = before - P.Damage; if (after < 0f) after = 0f;
        Hp[tg] = after;
        _lastDamageT[tg] = Time;
        OnDamageRules(u, tg, before - after, after);
        if (after <= 0f && !Dead[tg]) { KillUnit(tg); Listener?.OnKill(tg, u); }
    }

    /// <summary>Damage of render frame f is applied by the Animator after Update(f): bots and rules see it in frame f+1, so it is applied at the
    /// start of the next fixed step (= the first step of the next SimBotHost frame; listeners then see that frame's time).</summary>
    private void FlushHits()
    {
        int n = _hitN; _hitN = 0;
        for (int i = 0; i < n; i++) ApplyHit(_hitA[i], _hitV[i]);
    }

    /// <summary>Strike dropped (cancel window, target released or beyond 9.5 m): no damage, cooldown not refunded.</summary>
    private void AbortStrike(int u)
    {
        _st[u] = StRecover;
        _stEnd[u] = RenderTime + P.CancelRecoverFrames * (double)FixedDt;
        StrikeFrame[u] = -1;
        if (_mo[u] == MoStrike) _mo[u] = MoNone;
    }

    // ---------------- facing ----------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TurnToDir(int u, float dx, float dz, float rateDeg, float dt) => TurnTo(u, MathF.Atan2(dx, dz), rateDeg, dt);

    private void TurnTo(int u, float want, float rateDeg, float dt)
    {
        float y = _yaw[u], diff = want - y;
        if (diff > MathF.PI) diff -= 2f * MathF.PI; else if (diff < -MathF.PI) diff += 2f * MathF.PI;
        float stepMax = rateDeg * (MathF.PI / 180f) * dt;
        if (MathF.Abs(diff) <= stepMax) { y = want; _turnWanted[u] = false; }
        else y += diff > 0f ? stepMax : -stepMax;
        if (y > MathF.PI) y -= 2f * MathF.PI; else if (y < -MathF.PI) y += 2f * MathF.PI;
        _yaw[u] = y;
        FX[u] = MathF.Sin(y); FZ[u] = MathF.Cos(y);
    }
}
