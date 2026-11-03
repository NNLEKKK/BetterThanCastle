using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Duel bot v3 ("Chosen"). One instance drives one side; level 0 = naive sparring partner. Acts only through BtcDuel.Attack/MoveAttack/Move
    /// (the same Order objects a player triggers). Beyond a human it knows: whom every enemy unit attacks, exact HP, velocities (prediction),
    /// and it reacts every 50 ms with every unit independently.
    /// Posture (no contact): every unit walks its OWN random route around the group anchor (unpredictable, hard to click), the group pushes the enemy.
    /// Fight: the army splits into groups of ~6 per target; every attacker gets its OWN grid cell around the target (the four orthogonal cells form the
    /// body block, the rest fill ring 2) so nobody is wasted behind a teammate; wounded units are withdrawn early by time-to-die, chasers of a withdrawn
    /// unit are killed by nearby defenders, blockers are swapped live. Chase leash, safe-edge guard, skips with exact timing (220 ms after the strike starts).
    /// Planning every Step seconds, skip scheduler every frame, no allocations in the tick.
    /// </summary>
    internal sealed class BtcDuelBotX : IDuelBot
    {
        private readonly BtcDuelMatch _m;
        private readonly int _side, _level;
        private readonly bool _adv;                      // advanced behaviours (false = plain v1 sparring partner)
        private readonly int _dir;                       // +1 = enemy is east, -1 = enemy is west
        private readonly BtcDuelZone _ownSafe, _enemySafe;
        private readonly List<BtcDuelUnitRef> _my = new List<BtcDuelUnitRef>(48), _foe = new List<BtcDuelUnitRef>(48);
        private readonly List<BtcDuelUnitRef> _grp = new List<BtcDuelUnitRef>(48);
        private readonly BtcDuelUnitRef[] _tgt = new BtcDuelUnitRef[3];
        private float _acc;
        private float Step = 0.05f;
        private readonly int _tier;
        private float _dbg, _lockLog;
        private const float SwingTime = 0.7f;            // seconds per hit of one skipping attacker (dps estimate)

        // cell priority around a target: 4 orthogonal (block), 4 diagonal, 4 at distance 2, 8 knight cells
        private static readonly int[] CX = { 1, -1, 0, 0, 1, 1, -1, -1, 2, -2, 0, 0, 2, 2, -2, -2, 1, 1, -1, -1 };
        private static readonly int[] CZ = { 0, 0, 1, -1, 1, -1, 1, -1, 0, 0, 2, -2, 1, -1, 1, -1, 2, -2, 2, -2 };

        /// <summary>Feature mask for A/B testing: 1 chaos walk, 2 push, 4 grid cells, 8 chaser defenders, 16 time-to-die withdrawal, 32 up to 3 groups.</summary>
#if SIM
        [System.ThreadStatic]
#endif
        internal static int Feat = 53;                  // 1 chaos, 4 cells, 16 ttd, 32 groups on (push and chaser defenders still lose in A/B)
        private static bool Has(int bit) { return (Feat & bit) != 0; }

#if SIM
        [System.ThreadStatic]
#endif
        internal static float SkipDelayMs = 220f;
#if SIM
        [System.ThreadStatic]
#endif
        internal static float[] LabDelays;
#if SIM
        [System.ThreadStatic]
#endif
        internal static bool LabForceSkip;
#if SIM
        [System.ThreadStatic]
#endif
        private static int _labIdx;

        internal BtcDuelBotX(BtcDuelMatch m, int side, int level, int tier, bool advanced)
        {
            _m = m; _side = side; _level = level; _tier = tier; _adv = advanced && (Feat & 256) == 0;
            Step = tier <= 1 ? 0.4f : (tier == 2 ? 0.2f : (tier == 3 ? 0.1f : 0.05f));
            _dir = side == 0 ? 1 : -1;
            BtcDuel.GuardOrders[side] = _adv && (Feat & 128) == 0;
            _ownSafe = side == 0 ? BtcDuelZone.SafeA : BtcDuelZone.SafeB;
            _enemySafe = side == 0 ? BtcDuelZone.SafeB : BtcDuelZone.SafeA;
        }

        // ---------------- geometry (side relative) ----------------
        private float OwnSafeInnerX { get { return _side == 0 ? BtcDuelMap.X0 + 5f * BtcDuelMap.Scale : BtcDuelMap.X0 + BtcDuelMap.Len - 5f * BtcDuelMap.Scale; } }
        private float MidCenterX { get { return (BtcDuelMap.MidX0 + BtcDuelMap.MidX1) * 0.5f; } }
        private float EnemySafeEdgeX { get { return _side == 0 ? BtcDuelMap.X0 + BtcDuelMap.Len - BtcDuelMap.SafeLen - 1.5f : BtcDuelMap.X0 + BtcDuelMap.SafeLen + 1.5f; } }
        /// <summary>true when x lies beyond the line "enemy safe edge - margin * dir".</summary>
        private bool Past(float x, float margin) { float line = EnemySafeEdgeX - _dir * margin; return _dir > 0 ? x > line : x < line; }
        private static float Dist2D(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }
        private static Vector3 Pred(BtcDuelUnitRef u, float t) { Vector3 p = u.Tf.position; p.x += u.Vel.x * t; p.z += u.Vel.z * t; return p; }
        private static Vector3 Floor3(float x, float z) { return new Vector3(x, BtcDuelMap.Floor, Mathf.Clamp(z, BtcDuelMap.Z0 + 1.5f, BtcDuelMap.Z0 + BtcDuelMap.Wid - 1.5f)); }

        // ---------------- events ----------------
        public void OnStrike(BtcDuelUnitRef u)
        {
            if (u.Side != _side || _level < 1 || !(BtcDuel.SkipAnims || LabForceSkip)) return;
            if (_tier == 1) return;                       // Medium: no skips
            float d = _tier == 2 ? 330f + UnityEngine.Random.Range(0f, 60f) : (_tier == 3 ? SkipDelayMs + UnityEngine.Random.Range(0f, 30f) : SkipDelayMs);
            if (LabDelays != null && LabDelays.Length > 0) { d = LabDelays[_labIdx % LabDelays.Length]; _labIdx++; }
            u.SkipAt = Time.time + d * 0.001f;
        }

        // ---------------- tick ----------------
        public void Tick(float dt)
        {
            try
            {
                if (_level >= 1) SkipTick();
                _acc += dt;
                if (_acc < Step) return;
                _acc = 0f;
                Think();
                _dbg += Step;
                if (_dbg >= 2f) { _dbg = 0f; DebugLine(); }
            }
            catch (Exception e) { BtcLog.Error("DuelBot", "Tick", e); }
        }

        private void SkipTick()
        {
            List<BtcDuelUnitRef> us = _m.Units;
            float now = Time.time;
            for (int i = 0; i < us.Count; i++)
            {
                BtcDuelUnitRef u = us[i];
                if (u.Side != _side || u.SkipAt < 0f || now < u.SkipAt) continue;
                u.SkipAt = -1f;
                if (u.Dead || u.Retreating || u.Pulling || u.StrikeTarget == null) continue;
                BtcDuelUnitRef t;
                if (!BtcDuel.TryGet(u.StrikeTarget, out t) || t.Dead || t.Zone == _enemySafe) continue;
                if (Past(u.Tf.position.x, 1.0f)) continue;
                // move-attack onto the ground (resets the strike recovery), then refocus the same target
                BtcDuel.MoveAttack(u, u.Tf.position, true);
                BtcDuel.Attack(u, u.StrikeTarget, true);
                u.TargetId = t.Id;
                BtcDuelLog.Line("SKIP", "u" + u.Id + ">u" + t.Id + " delay=" + (int)((now - u.StrikeAt) * 1000f));
            }
        }

        private void Collect()
        {
            _my.Clear(); _foe.Clear();
            List<BtcDuelUnitRef> us = _m.Units;
            for (int i = 0; i < us.Count; i++)
            {
                BtcDuelUnitRef u = us[i];
                if (u.Dead || u.Tf == null || u.Dr == null) continue;
                if (u.Side == _side) _my.Add(u); else _foe.Add(u);
            }
        }

        private float HitsToDie(BtcDuelUnitRef u, int attackerSide)
        {
            float dmg = Mathf.Max(1f, _m.AvgDmg[attackerSide]);
            return Mathf.Ceil(Mathf.Max(0f, u.Dr.CurrentHP) / dmg);
        }

        private void Think()
        {
            Collect();
            if (_my.Count == 0 || _foe.Count == 0) return;
            if (_m.Escaping[_side]) { Escape(); return; }
            if (_level == 0) { NaiveThink(); return; }
            ChosenThink();
        }

        // ---------------- level 0 ----------------
        private void NaiveThink()
        {
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                BtcDuelUnitRef best = Nearest(me, 40f);
                if (best == null)
                {
                    if (me.TargetId != -2) { me.TargetId = -2; BtcDuel.MoveAttack(me, Floor3(MidCenterX, me.Tf.position.z)); }
                    continue;
                }
                if (me.TargetId == best.Id) continue;
                me.TargetId = best.Id; BtcDuel.Attack(me, best.Go);
            }
        }

        private BtcDuelUnitRef Nearest(BtcDuelUnitRef me, float maxD)
        {
            BtcDuelUnitRef best = null; float bd = maxD;
            for (int j = 0; j < _foe.Count; j++)
            {
                BtcDuelUnitRef e = _foe[j];
                if (e.Zone == _enemySafe) continue;
                float d = Dist2D(me.Tf.position, e.Tf.position);
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        // ---------------- escape ----------------
        private void Escape()
        {
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                if (me.Zone == _ownSafe) continue;
                me.SkipAt = -1f; me.SlotIdx = -1; me.Pulling = false;
                bool idle = me.Vel.sqrMagnitude < 0.05f && Time.time - me.LastOrderAt > 1.5f;
                if (me.TargetId == -3 && !idle) continue;
                me.TargetId = -3;
                BtcDuel.Move(me, Floor3(OwnSafeInnerX, me.Tf.position.z));
            }
        }

        // ---------------- helpers ----------------
        private BtcDuelUnitRef ById(int id)
        {
            for (int i = 0; i < _my.Count; i++) if (_my[i].Id == id) return _my[i];
            return null;
        }

        private int CountNear(BtcDuelUnitRef m, float r)
        {
            int n = 0;
            for (int j = 0; j < _foe.Count; j++) if (_foe[j].Zone != _enemySafe && Dist2D(m.Tf.position, _foe[j].Tf.position) < r) n++;
            return n;
        }

        /// <summary>Seconds this unit survives if its current attackers keep hitting with skips.</summary>
        private float Ttd(BtcDuelUnitRef m)
        {
            int att = m.ThreatCount;                       // only enemies that really target this unit
            if (att == 0) return 99f;
            float dps = att * Mathf.Max(1f, _m.AvgDmg[1 - _side]) / SwingTime;
            return Mathf.Max(0f, m.Dr.CurrentHP) / dps;
        }

        private void UpdateThreat()
        {
            for (int i = 0; i < _my.Count; i++) _my[i].ThreatCount = 0;
            float now = Time.time;
            for (int j = 0; j < _foe.Count; j++)
            {
                BtcDuelUnitRef f = _foe[j];
                BtcDuelUnitRef t = null;
                if (now - f.StrikeAt < 1.3f && f.StrikeTarget != null) BtcDuel.TryGet(f.StrikeTarget, out t);
                if (t == null && f.Labor != null)
                {
                    Brix.Game.AI.Task k = f.Labor.GetCurrentOrPendingTask();
                    if (k != null && k.Target != null) BtcDuel.TryGet(k.Target, out t);
                }
                f.FoeTargetId = t != null ? t.Id : -1;
                if (t != null && t.Side == _side && Dist2D(f.Tf.position, t.Tf.position) < 8f) t.ThreatCount++;
            }
        }

        private bool EdgeGuard(BtcDuelUnitRef me)
        {
            float now = Time.time;
            if (me.HoldUntil > now) return true;
            Vector3 p = Pred(me, 0.5f);
            if (!Past(p.x, 3.0f) && !Past(me.Tf.position.x, 3.0f)) return false;
            me.HoldUntil = now + 1.2f; me.TargetId = -4; me.SkipAt = -1f; me.SlotIdx = -1;
            BtcDuel.Move(me, Floor3(EnemySafeEdgeX - _dir * 6f, me.Tf.position.z), true);
            BtcDuelLog.Line("BOT", "edge hold u" + me.Id);
            return true;
        }

        private int PullingCount()
        {
            int n = 0;
            for (int i = 0; i < _my.Count; i++) if (_my[i].Pulling) n++;
            return n;
        }

        private bool Free(BtcDuelUnitRef u) { return !u.Retreating && !u.Pulling; }

        private int CountFree()
        {
            int n = 0;
            for (int i = 0; i < _my.Count; i++) if (Free(_my[i])) n++;
            return n;
        }

        private Vector3 Centroid(bool freeOnly)
        {
            float cx = 0f, cz = 0f; int n = 0;
            for (int i = 0; i < _my.Count; i++) { if (freeOnly && !Free(_my[i])) continue; cx += _my[i].Tf.position.x; cz += _my[i].Tf.position.z; n++; }
            return n == 0 ? Vector3.zero : new Vector3(cx / n, 0f, cz / n);
        }

        private float Force(List<BtcDuelUnitRef> list, Vector3 around, float r, int attackerSide)
        {
            float s = 0f;
            for (int i = 0; i < list.Count; i++) if (Dist2D(list[i].Tf.position, around) < r) s += list[i].Dr.CurrentHP;
            return s;
        }

        // ---------------- Chosen ----------------
        private void ChosenThink()
        {
            UpdateThreat();
            bool engaged = false;
            for (int i = 0; i < _my.Count && !engaged; i++)
            {
                if (!Free(_my[i]) && _my[i].ThreatCount == 0) continue;
                for (int j = 0; j < _foe.Count; j++)
                    if (_foe[j].Zone != _enemySafe && Dist2D(_my[i].Tf.position, _foe[j].Tf.position) < 9f) { engaged = true; break; }
            }
            if (!engaged) { Posture(); return; }
            Fight();
        }

        // ---------------- posture: no contact ----------------
        /// <summary>Gather, take the middle, then push the enemy. Every unit walks its own random route (independent timers/directions) around the anchor.</summary>
        private void Posture()
        {
            Vector3 c = Centroid(true);
            if (c == Vector3.zero) { RetreatWounded(); return; }
            // nearest enemy to the group
            BtcDuelUnitRef near = null; float nd = float.MaxValue;
            for (int j = 0; j < _foe.Count; j++)
            {
                BtcDuelUnitRef e = _foe[j];
                if (e.Zone == _enemySafe || Past(e.Tf.position.x, 4f)) continue;
                float d = Dist2D(c, e.Tf.position);
                if (d < nd) { nd = d; near = e; }
            }
            Vector3 anchor;
            float radius = 3.5f;
            bool push = false;
            if (near != null && nd < 32f)
            {
                float mine = Force(_my, c, 14f, _side), theirs = Force(_foe, near.Tf.position, 14f, 1 - _side);
                push = Has(2) && (mine >= theirs * 0.8f || near.Zone == BtcDuelZone.Mid);
            }
            if (push)
            {
                // stand ~8 cells from the nearest enemy on our side of it; contact happens by itself, the fight logic takes over inside 9
                Vector3 pe = Pred(near, 0.8f);
                Vector3 dirv = c - pe; dirv.y = 0f;
                if (dirv.sqrMagnitude < 0.01f) dirv = new Vector3(-_dir, 0f, 0f);
                dirv.Normalize();
                anchor = pe + dirv * 7.5f;
                radius = 3f;
            }
            else
            {
                float ax = MidCenterX - _dir * 2f;
                anchor = new Vector3(ax, 0f, BtcDuelMap.CenterZ);
                radius = Mathf.Max(5f, BtcDuelMap.Scale * 5f);
            }
            if (Past(anchor.x, 7f)) anchor.x = EnemySafeEdgeX - _dir * 7f;
            float now = Time.time;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                if (me.Retreating) { RetreatOne(me); continue; }
                if (me.Pulling) continue;
                if (EdgeGuard(me)) continue;
                me.SkipAt = -1f; me.SlotIdx = -1;
                bool idle = me.Vel.sqrMagnitude < 0.04f && now - me.LastOrderAt > 1.2f;
                if (now < me.NextOrderAt && !idle) continue;
                // own random waypoint: independent per unit (own angle, own radius, own timer)
                float ang = UnityEngine.Random.value * 6.2832f, rad = Has(1) ? Mathf.Sqrt(UnityEngine.Random.value) * radius : 0f;
                Vector3 wp = new Vector3(anchor.x + Mathf.Cos(ang) * rad, 0f, anchor.z + Mathf.Sin(ang) * rad);
                if (Past(wp.x, 7f)) wp.x = EnemySafeEdgeX - _dir * 7f;
                me.TargetId = -2;
                me.NextOrderAt = now + 0.45f + UnityEngine.Random.value * 0.9f;
                BtcDuel.MoveAttack(me, Floor3(wp.x, wp.z));
            }
        }

        private void RetreatWounded()
        {
            for (int i = 0; i < _my.Count; i++) if (_my[i].Retreating) RetreatOne(_my[i]);
        }

        private void RetreatOne(BtcDuelUnitRef me)
        {
            if (me.Zone == _ownSafe) return;
            bool idle = me.Vel.sqrMagnitude < 0.04f && Time.time - me.LastOrderAt > 1.5f;
            if (me.TargetId != -3 || idle) { me.TargetId = -3; BtcDuel.Move(me, Floor3(OwnSafeInnerX, me.Tf.position.z)); }
        }

        private void StartRetreat(BtcDuelUnitRef me, float left, BtcDuelUnitRef cover)
        {
            me.Retreating = true; me.Pulling = false; me.SkipAt = -1f; me.TargetId = -3; me.SlotIdx = -1; me.SwapIn = -1;
            BtcDuelLog.Line("BOT", "retreat u" + me.Id + " hits_left=" + left + " ttd=" + Ttd(me).ToString("0.0") + (cover != null ? " cover=u" + cover.Id : ""));
            BtcDuel.Move(me, Floor3(OwnSafeInnerX, me.Tf.position.z), Ttd(me) < 2f);
        }

        private int HealthyFriends(BtcDuelUnitRef except, float minHits)
        {
            int n = 0;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef c = _my[i];
                if (c == except || !Free(c)) continue;
                if (HitsToDie(c, 1 - _side) >= minHits) n++;
            }
            return n;
        }

        private BtcDuelUnitRef Replacement(BtcDuelUnitRef wounded, bool freeOfSlot)
        {
            BtcDuelUnitRef best = null; float bd = float.MaxValue;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef c = _my[i];
                if (c == wounded || !Free(c) || HitsToDie(c, 1 - _side) <= 7f) continue;
                if (freeOfSlot && (c.SlotIdx >= 0 || c.SwapIn >= 0)) continue;
                float d = Dist2D(c.Tf.position, wounded.Tf.position);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        private Vector3 PullPoint(BtcDuelUnitRef m)
        {
            float cx = 0f, cz = 0f; int n = 0;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef c = _my[i];
                if (c == m || !Free(c) || HitsToDie(c, 1 - _side) < 8f) continue;
                cx += c.Tf.position.x; cz += c.Tf.position.z; n++;
            }
            if (n == 0) return Floor3(OwnSafeInnerX, m.Tf.position.z);
            return Floor3(cx / n - _dir * 4f, cz / n);
        }

        private void StartPull(BtcDuelUnitRef me)
        {
            me.Pulling = true; me.PullFree = -1f; me.SkipAt = -1f; me.TargetId = -6; me.SlotIdx = -1;
            BtcDuelLog.Line("BOT", "pull u" + me.Id + " hp=" + (int)me.Dr.CurrentHP + " threat=" + me.ThreatCount + " ttd=" + Ttd(me).ToString("0.0"));
            BtcDuel.Move(me, PullPoint(me));
        }

        // ---------------- fight ----------------
        private void Fight()
        {
            bool allIn = _m.Escaping[1 - _side];
            float now = Time.time;
            // 1) wounded handling (by time-to-die, early)
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                if (_adv && Has(64) && (me.Retreating || me.Pulling) && me.Zone != _ownSafe && !allIn)
                {
                    // A/B data: 76% of retreating units were killed while running (no damage dealt). Running only works while it is survivable;
                    // a runner needs ~2 hits from each chaser, so when attackers are on it and it cannot afford them, it turns and fights.
                    float l0 = HitsToDie(me, 1 - _side);
                    if (me.ThreatCount > 0 && l0 < 2f * me.ThreatCount + 2f && CountNear(me, 2.8f) > 0)
                    {
                        BtcDuelLog.Line("BOT", "turn and fight u" + me.Id + " hits_left=" + l0 + " threat=" + me.ThreatCount);
                        me.Retreating = false; me.Pulling = false; me.TargetId = -1; me.PullFree = -1f;
                        continue;
                    }
                }
                if (me.Retreating)
                {
                    if (BtcDuelConfig.InfiniteHeal && me.Dr.CurrentHP > me.MaxHp * 0.8f) { me.Retreating = false; me.TargetId = -1; }
                    else RetreatOne(me);
                    continue;
                }
                if (allIn) continue;
                float left = HitsToDie(me, 1 - _side);
                float ttd = Ttd(me);
                if (!_adv)
                {
                    bool thr1 = me.ThreatCount > 0 || CountNear(me, 3.2f) > 0;
                    if (left > 3f || (!thr1 && left > 1f)) continue;
                    BtcDuelUnitRef cover1 = Replacement(me, false);
                    if (cover1 != null && left > 1f && Dist2D(cover1.Tf.position, me.Tf.position) > 2.6f) continue;
                    StartRetreat(me, left, cover1);
                    continue;
                }
                if (me.SwapIn >= 0)
                {
                    BtcDuelUnitRef r = ById(me.SwapIn);
                    if (r == null || r.Retreating || Dist2D(r.Tf.position, me.Tf.position) < 1.9f || left <= 1f || ttd < 0.9f)
                    {
                        me.SwapIn = -1;
                        if (left <= 4f || ttd < 1.5f) StartRetreat(me, left, r); else if (HealthyFriends(me, 8f) >= 2) StartPull(me);
                    }
                    continue;
                }
                if (!Has(16))
                {
                    bool thr2 = me.ThreatCount > 0 || CountNear(me, 3.2f) > 0;
                    if (left <= 3f && (thr2 || left <= 1f)) StartRetreat(me, left, null);
                    else if (left <= 7f && me.ThreatCount >= 1 && HealthyFriends(me, 8f) >= 2 && !me.Pulling) StartPull(me);
                    else if (me.Pulling && me.ThreatCount == 0 && !(CountNear(me, 4f) > 0)) { if (me.PullFree < 0f) me.PullFree = now; else if (now - me.PullFree > 1.5f) { me.Pulling = false; me.TargetId = -1; } }
                    continue;
                }
                bool endangered = ttd < 3.2f;
                if (Has(64) && me.ThreatCount > 0 && left < 2f * me.ThreatCount + 2f) continue;     // cannot survive a retreat: keep hitting
                if (me.Pulling)
                {
                    if (left <= 4f && ttd < 99f) { StartRetreat(me, left, null); continue; }
                    if (ttd >= 99f && me.ThreatCount == 0)
                    {
                        if (me.PullFree < 0f) me.PullFree = now; else if (now - me.PullFree > 1.5f) { me.Pulling = false; me.TargetId = -1; }
                    }
                    else me.PullFree = -1f;
                    continue;
                }
                if (left <= 4f && (endangered || left <= 1f))
                {
                    if (me.SlotIdx >= 0 && left > 1f && ttd > 1.4f)
                    {
                        BtcDuelUnitRef r = Replacement(me, true);
                        if (r != null)
                        {
                            r.SlotIdx = me.SlotIdx; r.SlotFoe = me.SlotFoe; r.SlotPos = me.SlotPos; r.TargetId = -5; r.Pulling = false;
                            me.SwapIn = r.Id; me.SlotIdx = -1;
                            BtcDuelLog.Line("BLOCK", "swap u" + me.Id + " -> u" + r.Id + " slot " + r.SlotIdx);
                            BtcDuel.Move(r, Floor3(r.SlotPos.x, r.SlotPos.z));
                            continue;
                        }
                    }
                    StartRetreat(me, left, null);
                }
                else if (endangered && me.ThreatCount > 0 && HealthyFriends(me, 8f) >= 2 && PullingCount() < Mathf.Max(1, CountFree() / 4)) StartPull(me);
            }
            if (CountFree() == 0) return;

            // 2) defenders kill the chasers of withdrawn / endangered units first
            _grp.Clear();
            DefendChasers();

            // 3) targets and groups
            int free = 0;
            for (int i = 0; i < _my.Count; i++) if (Free(_my[i]) && !_my[i].Defending) free++;
            if (free == 0) return;
            // one burst target until the army is big: splitting 8 units in two loses the race to the first kill (A/B 8v8); 10-15 -> 2 groups, 16+ -> 3
            int nt = (_adv && Has(32)) ? (free >= 16 ? 3 : (free >= 10 ? 2 : 1)) : (free >= 7 ? 2 : 1);
            if (BtcDuelConfig.Total(BtcDuelConfig.BotCount) < 5 && _adv && Has(32)) nt = 1;
            int got = 0;
            _tgt[0] = _tgt[1] = _tgt[2] = null;
            for (int k = 0; k < nt; k++)
            {
                int except1 = _tgt[0] != null ? _tgt[0].Id : -1, except2 = _tgt[1] != null ? _tgt[1].Id : -1;
                BtcDuelUnitRef t = PickFocus(k == 0 ? _focusKeep0 : (k == 1 ? _focusKeep1 : -1), except1, except2, allIn);
                if (t == null) break;
                _tgt[k] = t; got++;
            }
            _focusKeep0 = _tgt[0] != null ? _tgt[0].Id : -1; _focusKeep1 = _tgt[1] != null ? _tgt[1].Id : -1;
            if (got == 0) { Posture(); return; }

            // split: each free unit goes to the nearest target whose group is not full
            int cap = (free + got - 1) / got;
            for (int k = 0; k < got; k++)
            {
                _grp.Clear();
                // collect units for this target: sort by distance to it (simple selection each time)
                for (int i = 0; i < _my.Count; i++)
                {
                    BtcDuelUnitRef u = _my[i];
                    if (!Free(u) || u.Defending || u.GroupOf >= 0 && u.GroupStamp == _stamp) continue;
                    _grp.Add(u);
                }
                // keep the `cap` nearest to the target
                BtcDuelUnitRef tg = _tgt[k];
                _grp.Sort(delegate (BtcDuelUnitRef a, BtcDuelUnitRef b2) { return Dist2D(a.Tf.position, tg.Tf.position).CompareTo(Dist2D(b2.Tf.position, tg.Tf.position)); });
                int take = k == got - 1 ? _grp.Count : Mathf.Min(cap, _grp.Count);
                for (int i = 0; i < _grp.Count; i++) { if (i < take) { _grp[i].GroupOf = k; _grp[i].GroupStamp = _stamp; } }
                AssignCells(tg, k);
            }
            _stamp++;
        }

        private int _focusKeep0 = -1, _focusKeep1 = -1;
        private int _stamp = 1;

        private void DefendChasers()
        {
            float now = Time.time;
            for (int i = 0; i < _my.Count; i++) _my[i].Defending = false;
            if (!_adv || !Has(8)) return;
            for (int j = 0; j < _foe.Count; j++)
            {
                BtcDuelUnitRef f = _foe[j];
                if (f.Zone == _enemySafe || f.FoeTargetId < 0) continue;
                BtcDuelUnitRef v = ById(f.FoeTargetId);
                if (v == null || !(v.Pulling || v.Retreating || Ttd(v) < 3.2f)) continue;
                if (Past(f.Tf.position.x, 3f)) continue;
                // up to 4 healthy nearest units kill the chaser
                for (int n = 0; n < 4; n++)
                {
                    BtcDuelUnitRef best = null; float bd = 10f;
                    for (int i = 0; i < _my.Count; i++)
                    {
                        BtcDuelUnitRef c = _my[i];
                        if (!Free(c) || c.Defending || c == v || HitsToDie(c, 1 - _side) <= 6f) continue;
                        float d = Dist2D(c.Tf.position, f.Tf.position);
                        if (d < bd) { bd = d; best = c; }
                    }
                    if (best == null) break;
                    best.Defending = true; best.SlotIdx = -1;
                    if (best.TargetId != f.Id || (Time.time - best.LastOrderAt > 1.8f && best.Vel.sqrMagnitude < 0.05f)) { best.TargetId = f.Id; BtcDuel.Attack(best, f.Go); }
                }
            }
        }

        // ---------------- surround: every attacker gets its own cell ----------------
        private void PlainAttack(int group, BtcDuelUnitRef t, float now)
        {
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef u = _my[i];
                if (u.GroupOf != group || u.GroupStamp != _stamp || !Free(u) || u.Defending) continue;
                u.SlotIdx = -1; u.SlotFoe = -1;
                if (u.TargetId != t.Id || (now - u.LastOrderAt > 1.8f && u.Vel.sqrMagnitude < 0.05f)) { u.TargetId = t.Id; BtcDuel.Attack(u, t.Go); }
            }
        }

        private bool CellOk(float x, float z)
        {
            return z > BtcDuelMap.Z0 + 1.0f && z < BtcDuelMap.Z0 + BtcDuelMap.Wid - 1.0f && !Past(x, 2.5f);
        }

        private void AssignCells(BtcDuelUnitRef t, int group)
        {
            float now = Time.time;
            if (!Has(4) || t.Vel.sqrMagnitude > 1.5f)
            {
                // no cell plan: a moving target cannot be surrounded by walking to shifting cells; everybody hits directly
                for (int i = 0; i < _my.Count; i++)
                {
                    BtcDuelUnitRef u = _my[i];
                    if (u.GroupOf != group || u.GroupStamp != _stamp || !Free(u) || u.Defending) continue;
                    u.SlotIdx = -1; u.SlotFoe = -1;
                    if (u.TargetId != t.Id || (now - u.LastOrderAt > 1.8f && u.Vel.sqrMagnitude < 0.05f)) { u.TargetId = t.Id; BtcDuel.Attack(u, t.Go); }
                }
                return;
            }
            // block only when it is fast and sure: the target has stood still for a while (and is busy fighting) and enough of ours are close to every side
            if (now - t.MovedAt < 0.8f || t.StrikeAt < now - 2f) { PlainAttack(group, t, now); return; }
            {
                int closeN = 0;
                for (int i = 0; i < _my.Count; i++) { BtcDuelUnitRef u = _my[i]; if (u.GroupOf == group && u.GroupStamp == _stamp && Free(u) && !u.Defending && Dist2D(u.Tf.position, t.Tf.position) < 5.5f) closeN++; }
                if (closeN < 4) { PlainAttack(group, t, now); return; }
            }
            Vector3 tp = Pred(t, 0.3f);
            float bx = Mathf.Round(tp.x), bz = Mathf.Round(tp.z);
            // gather this group's units
            _grp.Clear();
            for (int i = 0; i < _my.Count; i++) { BtcDuelUnitRef u = _my[i]; if (u.GroupOf == group && u.GroupStamp == _stamp && Free(u) && !u.Defending) _grp.Add(u); }
            for (int i = 0; i < _grp.Count; i++) _grp[i].SlotIdx = -1;
            int assigned = 0, orthFilled = 0, orthNeeded = 0;
            for (int c = 0; c < CX.Length && assigned < _grp.Count; c++)
            {
                float wx = bx + CX[c], wz = bz + CZ[c];
                if (!CellOk(wx, wz)) continue;
                if (c < 4) orthNeeded++;
                BtcDuelUnitRef best = null; float bd = float.MaxValue;
                for (int i = 0; i < _grp.Count; i++)
                {
                    BtcDuelUnitRef u = _grp[i];
                    if (u.SlotIdx >= 0) continue;
                    // keep the unit that already holds this cell
                    float d = Dist2D(u.Tf.position, new Vector3(wx, 0f, wz));
                    if (u.SlotFoe == t.Id && u.SlotCell == c) d -= 1.5f;
                    if (d < bd) { bd = d; best = u; }
                }
                if (best == null) break;
                best.SlotIdx = c; best.SlotFoe = t.Id; best.SlotCell = c; best.SlotPos = new Vector3(wx, BtcDuelMap.Floor, wz);
                assigned++;
                float ds = Dist2D(best.Tf.position, best.SlotPos);
                float dt2 = Dist2D(best.Tf.position, t.Tf.position);
                if (c < 4)
                {
                    // block cell: stand exactly there, hit from there
                    if (ds > 1.1f)
                    {
                        bool moved = (best.LastDest - best.SlotPos).sqrMagnitude > 0.6f;
                        if (best.TargetId != -5 || moved || (now - best.LastOrderAt > 1.0f && best.Vel.sqrMagnitude < 0.05f)) { best.TargetId = -5; BtcDuel.Move(best, best.SlotPos); }
                    }
                    else { orthFilled++; if (best.TargetId != t.Id) { best.TargetId = t.Id; BtcDuel.Attack(best, t.Go); } }
                }
                else
                {
                    // outer cells: already within reach -> just hit; otherwise walk to the cell, then hit
                    if (dt2 <= 2.4f || ds <= 0.7f) { if (best.TargetId != t.Id) { best.TargetId = t.Id; BtcDuel.Attack(best, t.Go); } }
                    else
                    {
                        bool moved = (best.LastDest - best.SlotPos).sqrMagnitude > 0.6f;
                        if (best.TargetId != -5 || moved || (now - best.LastOrderAt > 1.0f && best.Vel.sqrMagnitude < 0.05f)) { best.TargetId = -5; BtcDuel.Move(best, best.SlotPos); }
                    }
                }
            }
            // surplus units (no cell left) simply attack
            for (int i = 0; i < _grp.Count; i++)
            {
                BtcDuelUnitRef u = _grp[i];
                if (u.SlotIdx >= 0) continue;
                u.SlotFoe = -1;
                if (u.TargetId != t.Id || (now - u.LastOrderAt > 1.8f && u.Vel.sqrMagnitude < 0.05f)) { u.TargetId = t.Id; BtcDuel.Attack(u, t.Go); }
            }
            if (orthNeeded > 0 && orthFilled >= orthNeeded && now - _lockLog > 1.0f)
            {
                _lockLog = now;
                BtcDuelLog.Line("LOCK", "u" + t.Id + " surrounded on " + orthFilled + " sides, group=" + _grp.Count + " speed=" + t.Vel.magnitude.ToString("0.0"));
            }
        }

        // ---------------- targeting ----------------
        private BtcDuelUnitRef PickFocus(int keepId, int exceptA, int exceptB, bool allIn)
        {
            Vector3 c = Centroid(true);
            if (c == Vector3.zero) return null;
            float now = Time.time;
            float maxDist = allIn ? 90f : 26f;
            BtcDuelUnitRef best = null, keep = null;
            float bs = float.MaxValue, keepScore = float.MaxValue;
            for (int j = 0; j < _foe.Count; j++)
            {
                BtcDuelUnitRef e = _foe[j];
                if (e.Id == exceptA || e.Id == exceptB || e.Zone == _enemySafe) continue;
                Vector3 pe = Pred(e, 0.6f);
                if (Past(e.Tf.position.x, 2.5f) || Past(pe.x, 1.5f)) continue;
                if (_adv && e.IgnoreUntil > now) continue;
                float dist = Dist2D(c, e.Tf.position);
                if (dist > maxDist) continue;
                float h = HitsToDie(e, _side);
                int guards = 0;
                for (int k = 0; k < _foe.Count; k++) if (k != j && Dist2D(_foe[k].Tf.position, e.Tf.position) < 3.5f) guards++;
                // leash: do not run after a unit that walks away towards its own army / safe zone
                if (_adv && !allIn && e.Vel.x * _dir > 1.0f)
                {
                    // it walks away towards its side: equal speed, we never catch it. Chase only if two of ours already stand on it and it dies within ~3 hits.
                    int onIt = 0;
                    for (int i = 0; i < _my.Count; i++) if (Free(_my[i]) && Dist2D(_my[i].Tf.position, e.Tf.position) < 2.8f) onIt++;
                    if (!(onIt >= 2 && h <= 3f)) { e.IgnoreUntil = now + 2.5f; continue; }
                }
                float score = h * 10f + dist * 0.8f + guards * 4f;
                if (_adv)
                {
                    BtcDuelUnitRef tgt = e.FoeTargetId >= 0 ? ById(e.FoeTargetId) : null;
                    if (tgt != null && (tgt.Pulling || tgt.Retreating)) score -= 12f;
                    if (guards == 0 && dist > 4f) score -= 5f;
                    if (e.Vel.sqrMagnitude < 0.3f && e.StrikeAt > now - 1.5f) score -= 4f;     // busy fighting and standing still: easy to surround
                }
                if (e.Id == keepId) { keepScore = score; keep = e; }
                if (score < bs) { bs = score; best = e; }
            }
            if (keep != null && keepScore <= bs * 1.25f + 2f) return keep;
            return best;
        }

        private void DebugLine()
        {
            if (_m.Units.Count == 0) return;
            int ret = 0, pull = 0, slot = 0;
            for (int i = 0; i < _my.Count; i++) { if (_my[i].Retreating) ret++; if (_my[i].Pulling) pull++; if (_my[i].SlotIdx >= 0 && _my[i].SlotIdx < 4) slot++; }
            BtcDuelLog.Line("BOT", "s" + _side + " L" + _level + (_adv ? "v3" : "v1") + " alive=" + _my.Count + " retreating=" + ret + " pulling=" + pull + " blockers=" + slot + " foeAlive=" + _foe.Count + " focus=" + _focusKeep0 + "," + _focusKeep1 + " dmgAvg=" + _m.AvgDmg[1 - _side].ToString("0.0"));
        }
    }
}
