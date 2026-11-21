using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Test opponent that plays like the human top player does against the bot: ALL units hit ONE target (lowest HP in reach), press the order again
    /// when a unit idles, keep chasing a target that is pulled away (but never into the safe zone), wounded units walk home. No skips.
    /// Only used for self-play measurements (BtcDuel.AutoPlayerKind 3), never in a real match.
    /// </summary>
    internal sealed class BtcDuelHunter : IDuelBot
    {
        private readonly BtcDuelMatch _m;
        private readonly int _side, _dir;
        private readonly BtcDuelZone _enemySafe, _ownSafe;
        private float _acc;
        private int _target = -1;
        private readonly List<BtcDuelUnitRef> _my = new List<BtcDuelUnitRef>(48), _foe = new List<BtcDuelUnitRef>(48);

        private readonly bool _spread;      // Shift+RMB style: every unit attack-moves onto a random enemy position, hits whatever it meets
        private readonly bool _sweeper;     // kind 5: plays like the user: whole line attack-moves past the bot (Shift+RMB), wounded walk home and return, now and then a short real focus
        private float _nextSweep, _focusUntil, _nextFocus = -1f;
        private readonly List<int> _focusers = new List<int>(4);
        private bool Fair { get { return _kind == 11; } }   // kind 11: hunter that respects the safe rule like a human (drops a target near its safe edge, never attack-moves)
        private readonly int _kind;         // 6 Baiter (runner pulls the bot into a waiting block, a unit takes the empty mid), 7 Clump (all move to ONE point, knot, attack-hold), 10 Presser (line pushes the bot to its safe edge, one unit holds the mid)
        internal BtcDuelHunter(BtcDuelMatch m, int side, bool spread, bool sweeper = false, int kind = 3)
        {
            _spread = spread; _sweeper = sweeper; _kind = kind;
            _m = m; _side = side; _dir = side == 0 ? 1 : -1;
            _ownSafe = side == 0 ? BtcDuelZone.SafeA : BtcDuelZone.SafeB;
            _enemySafe = side == 0 ? BtcDuelZone.SafeB : BtcDuelZone.SafeA;
        }

        public void OnStrike(BtcDuelUnitRef u) { }

        public void Tick(float dt)
        {
            _acc += dt;
            if (_acc < 0.1f) return;
            _acc = 0f;
            try { Think(); } catch (Exception e) { BtcLog.Error("DuelHunter", "Think", e); }
        }

        private float EnemyEdgeX { get { return _side == 0 ? BtcDuelMap.X0 + BtcDuelMap.Len - BtcDuelMap.SafeLen - 3f : BtcDuelMap.X0 + BtcDuelMap.SafeLen + 3f; } }
        private bool Past(float x) { return _dir > 0 ? x > EnemyEdgeX : x < EnemyEdgeX; }
        private bool PastBy(float x, float m) { float e = EnemyEdgeX - _dir * m; return _dir > 0 ? x > e : x < e; }
        private bool TooDeep(float x) { return Fair ? PastBy(x, 3f) : Past(x); }
        private static float D2(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        private void Think()
        {
            _my.Clear(); _foe.Clear();
            for (int i = 0; i < _m.Units.Count; i++)
            {
                BtcDuelUnitRef u = _m.Units[i];
                if (u.Dead || u.Tf == null || u.Dr == null) continue;
                if (u.Side == _side) _my.Add(u); else _foe.Add(u);
            }
            if (_my.Count == 0 || _foe.Count == 0) return;
            float now = Time.time;
            Vector3 c = Vector3.zero;
            for (int i = 0; i < _my.Count; i++) c += _my[i].Tf.position;
            c /= _my.Count;
            if (_m.Escaping[_side])
            {
                for (int i = 0; i < _my.Count; i++)
                {
                    BtcDuelUnitRef u = _my[i];
                    if (u.Zone == _ownSafe || (u.TargetId == -3 && now - u.LastOrderAt < 0.8f)) continue;
                    u.TargetId = -3; BtcDuel.Move(u, new Vector3(_side == 0 ? BtcDuelMap.X0 + 5f : BtcDuelMap.X0 + BtcDuelMap.Len - 5f, BtcDuelMap.Floor, u.Tf.position.z));
                }
                return;
            }
            if (_kind == 6) { BaiterThink(now); return; }
            if (_kind == 7) { ClumpThink(now); return; }
            if (_kind == 10) { PresserThink(now); return; }
            if (_sweeper) { SweeperThink(now); return; }
            if (_spread)
            {
                for (int i = 0; i < _my.Count; i++)
                {
                    BtcDuelUnitRef u = _my[i];
                    float left0 = u.Dr.CurrentHP / Mathf.Max(1f, _m.AvgDmg[1 - _side]);
                    if (left0 <= 3f && u.Zone != _ownSafe) { if (u.TargetId != -3 || (u.Vel.sqrMagnitude < 0.05f && now - u.LastOrderAt > 0.8f)) { u.TargetId = -3; BtcDuel.Move(u, new Vector3(_side == 0 ? BtcDuelMap.X0 + 5f : BtcDuelMap.X0 + BtcDuelMap.Len - 5f, BtcDuelMap.Floor, u.Tf.position.z)); } continue; }
                    if (now < u.NextOrderAt) continue;
                    u.NextOrderAt = now + 1.0f + UnityEngine.Random.value * 1.5f;
                    BtcDuelUnitRef pick = null; int tries = 0;
                    while (pick == null && tries++ < 6) { BtcDuelUnitRef e = _foe[UnityEngine.Random.Range(0, _foe.Count)]; if (e.Zone != _enemySafe && !Past(e.Tf.position.x)) pick = e; }
                    if (pick != null) { u.TargetId = -2; BtcDuel.MoveAttack(u, new Vector3(pick.Tf.position.x, BtcDuelMap.Floor, pick.Tf.position.z)); }
                }
                return;
            }
            // target: keep while alive and reachable, else the foe with the fewest hits left (closest to us on ties)
            BtcDuelUnitRef t = null;
            for (int j = 0; j < _foe.Count; j++) if (_foe[j].Id == _target && _foe[j].Zone != _enemySafe && !TooDeep(_foe[j].Tf.position.x)) t = _foe[j];
            if (t == null)
            {
                float bs = float.MaxValue;
                for (int j = 0; j < _foe.Count; j++)
                {
                    BtcDuelUnitRef e = _foe[j];
                    if (e.Zone == _enemySafe || TooDeep(e.Tf.position.x)) continue;
                    float dist = D2(c, e.Tf.position);
                    if (dist > 40f) continue;
                    float s = e.Dr.CurrentHP + dist * 3f;
                    if (s < bs) { bs = s; t = e; }
                }
                _target = t != null ? t.Id : -1;
            }
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef u = _my[i];
                float left = u.Dr.CurrentHP / Mathf.Max(1f, _m.AvgDmg[1 - _side]);
                if (left <= 3f && D2(u.Tf.position, c) < 60f && u.Zone != _ownSafe)
                {
                    if (u.TargetId != -3 || (u.Vel.sqrMagnitude < 0.05f && now - u.LastOrderAt > 0.8f))
                    { u.TargetId = -3; BtcDuel.Move(u, new Vector3(_side == 0 ? BtcDuelMap.X0 + 5f : BtcDuelMap.X0 + BtcDuelMap.Len - 5f, BtcDuelMap.Floor, u.Tf.position.z)); }
                    continue;
                }
                if (u.Zone == _ownSafe && left <= 3f) continue;
                if (Fair && Past(u.Tf.position.x))
                {
                    // too close to the enemy safe zone: a chase or a strike would cross the line, step back first
                    if (u.TargetId != -42 || (u.Vel.sqrMagnitude < 0.05f && now - u.LastOrderAt > 0.8f))
                    { u.TargetId = -42; BtcDuel.Move(u, new Vector3(EnemyEdgeX - _dir * 5f, BtcDuelMap.Floor, u.Tf.position.z)); }
                    continue;
                }
                if (t == null)
                {
                    if (Fair) { if (u.TargetId != -2 || (u.Vel.sqrMagnitude < 0.05f && now - u.LastOrderAt > 1.0f)) { u.TargetId = -2; BtcDuel.Move(u, new Vector3((BtcDuelMap.MidX0 + BtcDuelMap.MidX1) * 0.5f - _dir * 1.5f, BtcDuelMap.Floor, u.Tf.position.z)); } continue; }
                    if (u.TargetId != -2 || (u.Vel.sqrMagnitude < 0.05f && now - u.LastOrderAt > 1.0f))
                    { u.TargetId = -2; BtcDuel.MoveAttack(u, new Vector3((BtcDuelMap.MidX0 + BtcDuelMap.MidX1) * 0.5f - _dir * 1.5f, BtcDuelMap.Floor, u.Tf.position.z)); }
                    continue;
                }
                // the human presses again when a unit stands still or the target ran off
                bool idle = u.Vel.sqrMagnitude < 0.05f && now - u.LastOrderAt > 0.7f && D2(u.Tf.position, t.Tf.position) > 3.5f;
                if (u.TargetId != t.Id || idle) { u.TargetId = t.Id; BtcDuel.Attack(u, t.Go); }
            }
        }

        private float HomeX { get { return _side == 0 ? BtcDuelMap.X0 + 5f : BtcDuelMap.X0 + BtcDuelMap.Len - 5f; } }

        private void SweeperThink(float now)
        {
            if (_nextFocus < 0f) _nextFocus = now + 12f;
            // wounded walk home (RunToward) and come back healed, like the user does
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef u = _my[i];
                float frac = u.Dr.CurrentHP / Mathf.Max(1f, u.MaxHp);
                if (!u.Rest && frac < 0.4f && u.Zone != _ownSafe) u.Rest = true;
                else if (u.Rest && frac > 0.85f) { u.Rest = false; u.TargetId = -1; }
                if (u.Rest && u.Zone != _ownSafe && (u.TargetId != -3 || (u.Vel.sqrMagnitude < 0.05f && now - u.LastOrderAt > 0.8f)))
                { u.TargetId = -3; BtcDuel.Move(u, new Vector3(HomeX, BtcDuelMap.Floor, u.Tf.position.z)); }
            }
            // now and then a real focus: 3 healthy units on the weakest enemy in reach for 3 s (the bot must dodge this one)
            if (now >= _nextFocus)
            {
                _nextFocus = now + 10f + UnityEngine.Random.value * 5f;
                BtcDuelUnitRef w = null;
                for (int j = 0; j < _foe.Count; j++) { BtcDuelUnitRef e = _foe[j]; if (e.Zone == _enemySafe || Past(e.Tf.position.x)) continue; if (w == null || e.Dr.CurrentHP < w.Dr.CurrentHP) w = e; }
                _focusers.Clear();
                if (w != null)
                {
                    for (int g = 0; g < 3; g++)
                    {
                        BtcDuelUnitRef b = null; float bd = 10f;
                        for (int i = 0; i < _my.Count; i++) { BtcDuelUnitRef u = _my[i]; if (u.Rest || _focusers.Contains(u.Id)) continue; float d = D2(u.Tf.position, w.Tf.position); if (d < bd) { bd = d; b = u; } }
                        if (b == null) break;
                        _focusers.Add(b.Id); b.TargetId = w.Id; BtcDuel.Attack(b, w.Go);
                    }
                    if (_focusers.Count > 0) _focusUntil = now + 3f;
                }
            }
            if (now >= _focusUntil) _focusers.Clear();
            // the sweep: the whole healthy line attack-moves to a point 6 m behind the nearest enemies, re-pressed every ~3 s
            bool press = now >= _nextSweep;
            if (press) _nextSweep = now + 2.5f + UnityEngine.Random.value;
            float fx = 0f; int fn = 0;
            for (int j = 0; j < _foe.Count; j++) { BtcDuelUnitRef e = _foe[j]; if (e.Zone == _enemySafe) continue; fx += e.Tf.position.x; fn++; }
            float tx = fn > 0 ? fx / fn + _dir * 6f : (BtcDuelMap.MidX0 + BtcDuelMap.MidX1) * 0.5f;
            if (Past(tx)) tx = EnemyEdgeX - _dir * 1f;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef u = _my[i];
                if (u.Rest || _focusers.Contains(u.Id)) continue;
                bool idle = u.Vel.sqrMagnitude < 0.05f && now - u.LastOrderAt > 1.2f;
                if (press || u.TargetId != -2 || idle) { u.TargetId = -2; BtcDuel.MoveAttack(u, new Vector3(tx, BtcDuelMap.Floor, u.Tf.position.z)); }
            }
        }

        /// <summary>Wounded (< 40%) walk home and come back at > 85%. Returns true when the unit is busy with that.</summary>
        private bool WalkHome(BtcDuelUnitRef u, float now)
        {
            float frac = u.Dr.CurrentHP / Mathf.Max(1f, u.MaxHp);
            if (!u.Rest && frac < 0.4f && u.Zone != _ownSafe) u.Rest = true;
            else if (u.Rest && frac > 0.85f) { u.Rest = false; u.TargetId = -1; }
            if (!u.Rest) return false;
            if (u.Zone != _ownSafe && (u.TargetId != -3 || (u.Vel.sqrMagnitude < 0.05f && now - u.LastOrderAt > 0.8f)))
            { u.TargetId = -3; BtcDuel.Move(u, new Vector3(HomeX, BtcDuelMap.Floor, u.Tf.position.z)); }
            return true;
        }

        private float OwnMidEdgeX { get { return _dir > 0 ? BtcDuelMap.MidX0 : BtcDuelMap.MidX1; } }

        // ---- kind 6: Baiter (Kirusha's play) ----
        private int _bait = -1, _sitter = -1, _baitPhase;
        private float _baitAt;
        private void BaiterThink(float now)
        {
            Vector3 b = new Vector3(OwnMidEdgeX - _dir * 10f, BtcDuelMap.Floor, BtcDuelMap.CenterZ);
            BtcDuelUnitRef bait = null, sit = null;
            for (int i = 0; i < _my.Count; i++) { if (_my[i].Id == _bait && !_my[i].Rest) bait = _my[i]; if (_my[i].Id == _sitter && !_my[i].Rest) sit = _my[i]; }
            if (bait == null) { float bh = 0f; for (int i = 0; i < _my.Count; i++) { BtcDuelUnitRef u = _my[i]; if (u.Rest || u == sit || u.Dr.CurrentHP < u.MaxHp * 0.8f) continue; if (u.Dr.CurrentHP > bh) { bh = u.Dr.CurrentHP; bait = u; } } _bait = bait != null ? bait.Id : -1; _baitPhase = 0; }
            if (sit == null && _my.Count >= 4) { float bh = 0f; for (int i = 0; i < _my.Count; i++) { BtcDuelUnitRef u = _my[i]; if (u.Rest || u == bait || u.Dr.CurrentHP < u.MaxHp * 0.8f) continue; if (u.Dr.CurrentHP > bh) { bh = u.Dr.CurrentHP; sit = u; } } _sitter = sit != null ? sit.Id : -1; }
            bool botInMid = false;
            for (int j = 0; j < _foe.Count; j++) if (_foe[j].Zone == BtcDuelZone.Mid) botInMid = true;
            // block: waits at B, the whole block hits any bot unit that comes close
            Vector3 bc = Vector3.zero; int bn = 0;
            for (int i = 0; i < _my.Count; i++) { BtcDuelUnitRef u = _my[i]; if (u == bait || u == sit || u.Rest) continue; bc += u.Tf.position; bn++; }
            if (bn > 0) bc /= bn; else bc = b;
            BtcDuelUnitRef victim = null; float vh = float.MaxValue;
            for (int j = 0; j < _foe.Count; j++) { BtcDuelUnitRef e = _foe[j]; if (e.Zone == _enemySafe || Past(e.Tf.position.x) || D2(e.Tf.position, bc) > 9f) continue; if (e.Dr.CurrentHP < vh) { vh = e.Dr.CurrentHP; victim = e; } }
            int slot = 0;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef u = _my[i];
                if (WalkHome(u, now)) continue;
                bool idle = u.Vel.sqrMagnitude < 0.05f && now - u.LastOrderAt > 0.8f;
                if (u == bait)
                {
                    BtcDuelUnitRef near = null; float nd = float.MaxValue;
                    for (int j = 0; j < _foe.Count; j++) { float d = D2(u.Tf.position, _foe[j].Tf.position); if (d < nd) { nd = d; near = _foe[j]; } }
                    if (_baitPhase == 0)
                    {
                        if (near != null && nd < 7f) { _baitPhase = 1; _baitAt = now; }
                        else if (near != null && (u.TargetId != -30 || idle || now - u.LastOrderAt > 1f)) { u.TargetId = -30; BtcDuel.Move(u, new Vector3(near.Tf.position.x, BtcDuelMap.Floor, near.Tf.position.z)); }
                    }
                    if (_baitPhase == 1)
                    {
                        if (u.TargetId != -31 || idle) { u.TargetId = -31; BtcDuel.Move(u, new Vector3(b.x - _dir * 3f, BtcDuelMap.Floor, b.z)); }
                        if (D2(u.Tf.position, b) < 4f && now - _baitAt > 3f) _baitPhase = 0;
                    }
                    continue;
                }
                if (u == sit)
                {
                    BtcDuelUnitRef near = null; float nd = float.MaxValue;
                    for (int j = 0; j < _foe.Count; j++) { float d = D2(u.Tf.position, _foe[j].Tf.position); if (d < nd) { nd = d; near = _foe[j]; } }
                    Vector3 dest = !botInMid && nd > 8f ? new Vector3((BtcDuelMap.MidX0 + BtcDuelMap.MidX1) * 0.5f, BtcDuelMap.Floor, BtcDuelMap.CenterZ) : new Vector3(b.x, BtcDuelMap.Floor, b.z + 3f);
                    if (u.TargetId != -32 || idle || D2(u.LastDest, dest) > 2f) { u.TargetId = -32; BtcDuel.Move(u, dest); }
                    continue;
                }
                if (victim != null) { if (u.TargetId != victim.Id || idle) { u.TargetId = victim.Id; BtcDuel.Attack(u, victim.Go); } continue; }
                Vector3 spot = new Vector3(b.x, BtcDuelMap.Floor, b.z + ((slot++ % 5) - 2) * 1.6f);
                if (D2(u.Tf.position, spot) > 2f && (u.TargetId != -33 || idle)) { u.TargetId = -33; BtcDuel.Move(u, spot); }
            }
        }

        // ---- kind 7: Clump (Monotony's play): all units ordered onto ONE point, the game pushes them into a knot; attack-hold there ----
        private Vector3 _clumpP; private float _clumpAt = -1f;
        private void ClumpThink(float now)
        {
            if (_clumpAt < 0f) { _clumpP = new Vector3(OwnMidEdgeX + _dir * 2f, BtcDuelMap.Floor, BtcDuelMap.CenterZ); _clumpAt = now - 10f; }
            bool regroup = now - _clumpAt > 6f;
            if (regroup)
            {
                _clumpAt = now;
                Vector3 fc = Vector3.zero; int fn = 0;
                for (int j = 0; j < _foe.Count; j++) { if (_foe[j].Zone == _enemySafe) continue; fc += _foe[j].Tf.position; fn++; }
                if (fn > 0)
                {
                    fc /= fn;
                    Vector3 d = fc - _clumpP; d.y = 0f;
                    if (d.magnitude > 6f) _clumpP += d.normalized * 5f;
                    if (Past(_clumpP.x)) _clumpP.x = EnemyEdgeX - _dir * 2f;
                }
            }
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef u = _my[i];
                if (WalkHome(u, now)) continue;
                if (regroup) { u.TargetId = -34; BtcDuel.Move(u, _clumpP); continue; }
                bool idle = u.Vel.sqrMagnitude < 0.05f && now - u.LastOrderAt > 2f;
                if ((u.TargetId == -34 && now - _clumpAt > 2.5f) || idle) { u.TargetId = -35; BtcDuel.MoveAttack(u, _clumpP); }
            }
        }

        // ---- kind 10: Presser (user's anti-LuminaZero play): the line attack-moves to the bot's safe edge and hits whatever is in reach, one
        // healthy unit stands in the middle of the mid zone (space control once the bot is pushed out of the mid) ----
        private int _midder = -1; private float _nextPress;
        private void PresserThink(float now)
        {
            BtcDuelUnitRef sit = null;
            for (int i = 0; i < _my.Count; i++) if (_my[i].Id == _midder && !_my[i].Rest) sit = _my[i];
            if (sit == null && _my.Count >= 2)
            {
                float bh = 0f;
                for (int i = 0; i < _my.Count; i++) { BtcDuelUnitRef u = _my[i]; if (u.Rest) continue; if (u.Dr.CurrentHP > bh) { bh = u.Dr.CurrentHP; sit = u; } }
                _midder = sit != null ? sit.Id : -1;
            }
            bool press = now >= _nextPress;
            if (press) _nextPress = now + 2.5f + UnityEngine.Random.value;
            float tx = EnemyEdgeX - _dir * 1f;
            int slot = 0;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef u = _my[i];
                if (WalkHome(u, now)) continue;
                bool idle = u.Vel.sqrMagnitude < 0.05f && now - u.LastOrderAt > 1.0f;
                if (u == sit)
                {
                    Vector3 dest = new Vector3((BtcDuelMap.MidX0 + BtcDuelMap.MidX1) * 0.5f, BtcDuelMap.Floor, BtcDuelMap.CenterZ);
                    if (u.TargetId != -40 || (idle && D2(u.Tf.position, dest) > 2f)) { u.TargetId = -40; BtcDuel.Move(u, dest); }   // plain move: an attack-move chases a fleeing unit into its safe zone
                    continue;
                }
                // too close to the bot's safe zone: step back first (a chase or a pushing knot would cross the line)
                if (Past(u.Tf.position.x)) { if (u.TargetId != -42 || idle) { u.TargetId = -42; BtcDuel.Move(u, new Vector3(tx - _dir * 4f, BtcDuelMap.Floor, u.Tf.position.z)); } slot++; continue; }
                // a foe in reach (not in its safe zone, not past the edge): hit the weakest one
                BtcDuelUnitRef v = null; float vh = float.MaxValue;
                for (int j = 0; j < _foe.Count; j++)
                {
                    BtcDuelUnitRef e = _foe[j];
                    if (e.Zone == _enemySafe || PastBy(e.Tf.position.x, 4f) || D2(e.Tf.position, u.Tf.position) > 6f) continue;   // never chase toward the safe edge (strikes land late)
                    if (e.Dr.CurrentHP < vh) { vh = e.Dr.CurrentHP; v = e; }
                }
                if (v != null) { if (u.TargetId != v.Id || idle) { u.TargetId = v.Id; BtcDuel.Attack(u, v.Go); } slot++; continue; }
                Vector3 spot = new Vector3(tx - _dir * 1.5f, BtcDuelMap.Floor, BtcDuelMap.CenterZ + ((slot++ % 5) - 2) * 3f);
                if (press || u.TargetId != -41 || idle) { u.TargetId = -41; BtcDuel.Move(u, spot); }   // plain move: attack-move auto-acquires units in the safe zone
            }
        }
    }
}
