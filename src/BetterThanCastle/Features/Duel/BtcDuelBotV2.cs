using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// FROZEN COPY of bot v2 (used as the opponent in A/B self-play). Duel bot. One instance drives one side. Level 0 = naive sparring partner, level 1 = "Chosen" (tiers 1..3 are weaker limits of it).
    /// It acts only through BtcDuel.Attack/MoveAttack/Move, i.e. the same Order objects a player triggers; what it knows beyond a human:
    /// who every enemy unit is attacking (strike target / current task), exact hit points, velocities (position prediction), and it reacts every 50 ms.
    /// v2 (flag): threat map, wounded units are dragged BEHIND the friendly line so chasers walk through the army and get punished,
    /// body block (4 slots around an unprotected enemy) with live swap of wounded blockers, chase leash, safe-edge discipline.
    /// Planning every Step seconds, skip scheduler every frame, no allocations in the tick.
    /// </summary>
    internal sealed class BtcDuelBotV2 : IDuelBot
    {
        private readonly BtcDuelMatch _m;
        private readonly int _side, _level;
        private readonly bool _v2;
        private readonly int _dir;                       // +1 = enemy is east, -1 = enemy is west
        private readonly BtcDuelZone _ownSafe, _enemySafe;
        private readonly List<BtcDuelUnitRef> _my = new List<BtcDuelUnitRef>(48), _foe = new List<BtcDuelUnitRef>(48);
        private float _acc;
        private float Step = 0.05f;
        private readonly int _tier;
        private int _focusA = -1, _focusB = -1;
        private bool _advanced;
        private float _dbg;
        private const float Reach = 2.6f;               // "in melee contact" distance between unit centres (tuned from HIT dist logs)
        private const float SlotR = 1.55f;              // body block ring radius
        private static readonly Vector2[] SlotOff = { new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f) };

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

        internal BtcDuelBotV2(BtcDuelMatch m, int side, int level, int tier, bool v2)
        {
            _m = m; _side = side; _level = level; _tier = tier; _v2 = v2;
            Step = tier <= 1 ? 0.4f : (tier == 2 ? 0.2f : (tier == 3 ? 0.1f : 0.05f));
            _dir = side == 0 ? 1 : -1;
            _ownSafe = side == 0 ? BtcDuelZone.SafeA : BtcDuelZone.SafeB;
            _enemySafe = side == 0 ? BtcDuelZone.SafeB : BtcDuelZone.SafeA;
        }

        // ---------------- geometry (side relative) ----------------
        private float OwnSafeInnerX { get { return _side == 0 ? BtcDuelMap.X0 + 5f : BtcDuelMap.X0 + BtcDuelMap.Len - 5f; } }
        private float MidCenterX { get { return (BtcDuelMap.MidX0 + BtcDuelMap.MidX1) * 0.5f; } }
        /// <summary>X just outside the enemy safe zone.</summary>
        private float EnemySafeEdgeX { get { return _side == 0 ? BtcDuelMap.X0 + BtcDuelMap.Len - BtcDuelMap.SafeLen - 1.5f : BtcDuelMap.X0 + BtcDuelMap.SafeLen + 1.5f; } }
        /// <summary>true when x lies on the enemy side of the line "edge - margin * dir" (margin > 0 = stricter).</summary>
        private bool Past(float x, float margin) { float line = EnemySafeEdgeX - _dir * margin; return _dir > 0 ? x > line : x < line; }
        private static float Dist2D(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }
        private static Vector3 Pred(BtcDuelUnitRef u, float t) { Vector3 p = u.Tf.position; p.x += u.Vel.x * t; p.z += u.Vel.z * t; return p; }
        private Vector3 Floor3(float x, float z) { return new Vector3(x, BtcDuelMap.Floor, Mathf.Clamp(z, BtcDuelMap.Z0 + 1.5f, BtcDuelMap.Z0 + BtcDuelMap.Wid - 1.5f)); }

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
                BtcDuel.MoveAttack(u, u.Tf.position);
                BtcDuel.Attack(u, u.StrikeTarget);
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
                if (me.TargetId == -3) continue;
                me.TargetId = -3;
                BtcDuel.Move(me, Floor3(OwnSafeInnerX, me.Tf.position.z));
            }
        }

        // ---------------- Chosen ----------------
        private BtcDuelUnitRef ById(int id)
        {
            for (int i = 0; i < _my.Count; i++) if (_my[i].Id == id) return _my[i];
            return null;
        }

        private bool NearFoe(BtcDuelUnitRef m, float r)
        {
            for (int j = 0; j < _foe.Count; j++) if (Dist2D(m.Tf.position, _foe[j].Tf.position) < r) return true;
            return false;
        }

        /// <summary>Who is every enemy attacking right now (strike target, else its order target). Humans cannot see this for 15 units at once.</summary>
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

        /// <summary>Never let a unit drift into the enemy safe zone (instant loss): pull it back to a hold line.</summary>
        private bool EdgeGuard(BtcDuelUnitRef me)
        {
            float now = Time.time;
            if (me.HoldUntil > now) return true;
            Vector3 p = Pred(me, 0.5f);
            if (!Past(p.x, 3.0f) && !Past(me.Tf.position.x, 3.0f)) return false;
            me.HoldUntil = now + 1.2f; me.TargetId = -4; me.SkipAt = -1f; me.SlotIdx = -1;
            BtcDuel.Move(me, Floor3(EnemySafeEdgeX - _dir * 6f, me.Tf.position.z));
            BtcDuelLog.Line("BOT", "edge hold u" + me.Id);
            return true;
        }

        private void ChosenThink()
        {
            UpdateThreat();
            bool engaged = false;
            for (int i = 0; i < _my.Count && !engaged; i++)
                for (int j = 0; j < _foe.Count; j++)
                    if (_foe[j].Zone != _enemySafe && Dist2D(_my[i].Tf.position, _foe[j].Tf.position) < 9f) { engaged = true; break; }

            if (!engaged) { Advance(); return; }
            _advanced = false;
            Fight();
        }

        /// <summary>No enemy near: gather and take the middle of the mid zone (space control), wounded units stay back.</summary>
        private void Advance()
        {
            if (_advanced) { for (int i = 0; i < _my.Count; i++) EdgeGuard(_my[i]); RetreatWounded(); return; }
            _advanced = true;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                if (me.Retreating) continue;
                me.TargetId = -2; me.SkipAt = -1f; me.Pulling = false; me.SlotIdx = -1;
                BtcDuel.MoveAttack(me, Floor3(MidCenterX - _dir * 1.5f, me.Tf.position.z));
            }
        }

        private void RetreatWounded()
        {
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                if (!me.Retreating || me.Zone == _ownSafe) continue;
                if (me.TargetId != -3) { me.TargetId = -3; BtcDuel.Move(me, Floor3(OwnSafeInnerX, me.Tf.position.z)); }
            }
        }

        private void StartRetreat(BtcDuelUnitRef me, float left, BtcDuelUnitRef cover)
        {
            me.Retreating = true; me.Pulling = false; me.SkipAt = -1f; me.TargetId = -3; me.SlotIdx = -1; me.SwapIn = -1;
            BtcDuelLog.Line("BOT", "retreat u" + me.Id + " hits_left=" + left + (cover != null ? " cover=u" + cover.Id : ""));
            BtcDuel.Move(me, Floor3(OwnSafeInnerX, me.Tf.position.z));
        }

        private int CountFighters()
        {
            int n = 0;
            for (int i = 0; i < _my.Count; i++) if (!_my[i].Retreating && !_my[i].Pulling) n++;
            return n;
        }

        private int HealthyFriends(BtcDuelUnitRef except, float minHits)
        {
            int n = 0;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef c = _my[i];
                if (c == except || c.Retreating || c.Pulling) continue;
                if (HitsToDie(c, 1 - _side) >= minHits) n++;
            }
            return n;
        }

        /// <summary>Healthy fighter closest to a wounded unit, not already holding a slot.</summary>
        private BtcDuelUnitRef Replacement(BtcDuelUnitRef wounded, bool freeOfSlot)
        {
            BtcDuelUnitRef best = null; float bd = float.MaxValue;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef c = _my[i];
                if (c == wounded || c.Retreating || c.Pulling || HitsToDie(c, 1 - _side) <= 6f) continue;
                if (freeOfSlot && (c.SlotIdx >= 0 || c.SwapIn >= 0)) continue;
                float d = Dist2D(c.Tf.position, wounded.Tf.position);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        /// <summary>Point behind the healthy friends (towards the own safe zone) where a bait unit waits so chasers must cross the line.</summary>
        private Vector3 PullPoint(BtcDuelUnitRef m)
        {
            float cx = 0f, cz = 0f; int n = 0;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef c = _my[i];
                if (c == m || c.Retreating || c.Pulling || HitsToDie(c, 1 - _side) < 8f) continue;
                cx += c.Tf.position.x; cz += c.Tf.position.z; n++;
            }
            if (n == 0) return Floor3(OwnSafeInnerX, m.Tf.position.z);
            return Floor3(cx / n - _dir * 3.5f, cz / n);
        }

        private void Fight()
        {
            bool allIn = _m.Escaping[1 - _side];
            float now = Time.time;
            // 1) wounded handling
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                if (me.Retreating)
                {
                    if (BtcDuelConfig.InfiniteHeal && me.Dr.CurrentHP > me.MaxHp * 0.8f) { me.Retreating = false; me.TargetId = -1; }
                    continue;
                }
                float left = HitsToDie(me, 1 - _side);
                bool thr = me.ThreatCount > 0 || NearFoe(me, 3.2f);
                if (allIn) continue;
                if (_v2)
                {
                    // blocker waiting for its replacement
                    if (me.SwapIn >= 0)
                    {
                        BtcDuelUnitRef r = ById(me.SwapIn);
                        if (r == null || r.Retreating || Dist2D(r.Tf.position, me.Tf.position) < 1.9f || left <= 1f)
                        {
                            me.SwapIn = -1;
                            if (left <= 3f) StartRetreat(me, left, r); else if (HealthyFriends(me, 8f) >= 2) StartPull(me);
                        }
                        continue;
                    }
                    if ((left <= 3f && (thr || left <= 1f)) || (me.ThreatCount >= 3 && left / me.ThreatCount <= 1.0f && HealthyFriends(me, 8f) < 2))
                    {
                        if (me.SlotIdx >= 0 && left > 1f)
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
                    else if (((left <= 7f && me.ThreatCount >= 1) || (me.ThreatCount >= 2 && left / me.ThreatCount <= 2.6f)) && HealthyFriends(me, 8f) >= 2)
                    {
                        if (!me.Pulling) StartPull(me);
                    }
                    else if (me.Pulling)
                    {
                        if (me.ThreatCount == 0 && !NearFoe(me, 4f))
                        {
                            if (me.PullFree < 0f) me.PullFree = now; else if (now - me.PullFree > 1.5f) { me.Pulling = false; me.TargetId = -1; }
                        }
                        else me.PullFree = -1f;
                    }
                }
                else
                {
                    if (left > 3f) continue;
                    if (!thr && left > 1f) continue;
                    BtcDuelUnitRef cover = Replacement(me, false);
                    if (cover != null && left > 1f && Dist2D(cover.Tf.position, me.Tf.position) > 2.6f) continue;
                    StartRetreat(me, left, cover);
                }
            }
            if (CountFighters() == 0) return;

            // 2) focus targets
            int want = CountFighters() >= 7 ? 2 : 1;
            BtcDuelUnitRef fa = PickFocus(_focusA, -1, allIn);
            BtcDuelUnitRef fb = want > 1 ? PickFocus(_focusB, fa != null ? fa.Id : -1, allIn) : null;
            _focusA = fa != null ? fa.Id : -1; _focusB = fb != null ? fb.Id : -1;
            if (fa == null) { Advance(); return; }

            // 3) body block on a single unprotected target, else plain assignment
            bool block = _v2 && fb == null && CountFighters() >= 3 && BlockWorthy(fa);
            if (!block) ClearSlots();
            if (block) ManageBlock(fa);

            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                if (me.Retreating || me.Pulling) continue;
                if (EdgeGuard(me)) continue;
                if (block && me.SlotIdx >= 0) continue;     // handled by ManageBlock
                BtcDuelUnitRef t = fa;
                if (fb != null && Dist2D(me.Tf.position, fb.Tf.position) < Dist2D(me.Tf.position, fa.Tf.position) - 0.5f) t = fb;
                if (me.TargetId == t.Id) continue;
                me.TargetId = t.Id;
                BtcDuel.Attack(me, t.Go);
            }
        }

        private void StartPull(BtcDuelUnitRef me)
        {
            me.Pulling = true; me.PullFree = -1f; me.SkipAt = -1f; me.TargetId = -6; me.SlotIdx = -1;
            BtcDuelLog.Line("BOT", "pull u" + me.Id + " hp=" + (int)me.Dr.CurrentHP + " threat=" + me.ThreatCount);
            BtcDuel.Move(me, PullPoint(me));
        }

        // ---------------- body block ----------------
        private bool BlockWorthy(BtcDuelUnitRef b)
        {
            if (b.Zone == _enemySafe || Past(b.Tf.position.x, 6f)) return false;
            int guards = 0;
            for (int k = 0; k < _foe.Count; k++) if (_foe[k] != b && Dist2D(_foe[k].Tf.position, b.Tf.position) < 4.5f) guards++;
            if (guards > 1) return false;
            int near = 0;
            for (int i = 0; i < _my.Count; i++) if (!_my[i].Retreating && !_my[i].Pulling && Dist2D(_my[i].Tf.position, b.Tf.position) < 12f) near++;
            return near >= 3;
        }

        private void ClearSlots()
        {
            for (int i = 0; i < _my.Count; i++) { _my[i].SlotIdx = -1; _my[i].SlotFoe = -1; }
        }

        private Vector3 SlotWorld(BtcDuelUnitRef b, int s)
        {
            Vector3 p = Pred(b, 0.25f);
            return new Vector3(p.x + SlotOff[s].x * SlotR, BtcDuelMap.Floor, p.z + SlotOff[s].y * SlotR);
        }

        private bool SlotValid(Vector3 w) { return w.z > BtcDuelMap.Z0 + 1.1f && w.z < BtcDuelMap.Z0 + BtcDuelMap.Wid - 1.1f && !Past(w.x, 2f); }

        private void ManageBlock(BtcDuelUnitRef b)
        {
            for (int i = 0; i < _my.Count; i++) if (_my[i].SlotIdx >= 0 && _my[i].SlotFoe != b.Id) { _my[i].SlotIdx = -1; _my[i].SlotFoe = -1; }
            float now = Time.time;
            for (int s = 0; s < 4; s++)
            {
                Vector3 w = SlotWorld(b, s);
                if (!SlotValid(w)) continue;                                      // the wall closes this side
                BtcDuelUnitRef holder = null;
                for (int i = 0; i < _my.Count; i++) if (_my[i].SlotIdx == s && _my[i].SlotFoe == b.Id) { holder = _my[i]; break; }
                if (holder == null)
                {
                    BtcDuelUnitRef best = null; float bd = float.MaxValue;
                    for (int i = 0; i < _my.Count; i++)
                    {
                        BtcDuelUnitRef c = _my[i];
                        if (c.Retreating || c.Pulling || c.SlotIdx >= 0 || c.SwapIn >= 0 || c.HoldUntil > now) continue;
                        if (HitsToDie(c, 1 - _side) <= 4f) continue;
                        float d = Dist2D(c.Tf.position, w);
                        if (d < bd) { bd = d; best = c; }
                    }
                    if (best == null) continue;
                    best.SlotIdx = s; best.SlotFoe = b.Id; best.TargetId = -5;
                    holder = best;
                    BtcDuelLog.Line("BLOCK", "u" + best.Id + " takes slot " + s + " of u" + b.Id);
                }
                holder.SlotPos = w;
                if (Dist2D(holder.Tf.position, w) > 0.95f)
                {
                    if (holder.TargetId != -5 || (int)(now * 4f) % 2 == 0) { holder.TargetId = -5; BtcDuel.Move(holder, w); }
                }
                else if (holder.TargetId != b.Id)
                {
                    holder.TargetId = b.Id; BtcDuel.Attack(holder, b.Go);
                }
            }
            // everyone else just attacks the target (handled by the caller loop)
        }

        // ---------------- targeting ----------------
        private BtcDuelUnitRef PickFocus(int keepId, int exceptId, bool allIn)
        {
            float cx = 0f, cz = 0f; int n = 0;
            for (int i = 0; i < _my.Count; i++) { if (_my[i].Retreating || _my[i].Pulling) continue; cx += _my[i].Tf.position.x; cz += _my[i].Tf.position.z; n++; }
            if (n == 0) return null;
            Vector3 c = new Vector3(cx / n, 0f, cz / n);
            float now = Time.time;
            float maxDist = allIn ? 80f : 24f;
            BtcDuelUnitRef best = null, keep = null;
            float bs = float.MaxValue, keepScore = float.MaxValue;
            for (int j = 0; j < _foe.Count; j++)
            {
                BtcDuelUnitRef e = _foe[j];
                if (e.Id == exceptId || e.Zone == _enemySafe) continue;
                Vector3 pe = Pred(e, 0.6f);
                if (Past(e.Tf.position.x, 2.5f) || Past(pe.x, 1.5f)) continue;
                if (_v2 && e.IgnoreUntil > now) continue;
                float dist = Dist2D(c, e.Tf.position);
                if (dist > maxDist) continue;
                float h = HitsToDie(e, _side);
                int guards = 0;
                for (int k = 0; k < _foe.Count; k++) if (k != j && Dist2D(_foe[k].Tf.position, e.Tf.position) < 3.5f) guards++;
                // leash: do not run after a unit that walks away towards its own army / safe zone
                if (_v2 && !allIn && h > 1f && dist > 9f && e.Vel.x * _dir > 1.2f) { e.IgnoreUntil = now + 3f; continue; }
                float score = h * 10f + dist * 0.8f + guards * 4f;
                if (_v2)
                {
                    BtcDuelUnitRef tgt = e.FoeTargetId >= 0 ? ById(e.FoeTargetId) : null;
                    if (tgt != null && (tgt.Pulling || tgt.Retreating)) score -= 12f;     // punish the chaser of our bait
                    if (guards == 0 && dist > 4f) score -= 5f;                                // isolated unit
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
            for (int i = 0; i < _my.Count; i++) { if (_my[i].Retreating) ret++; if (_my[i].Pulling) pull++; if (_my[i].SlotIdx >= 0) slot++; }
            BtcDuelLog.Line("BOT", "s" + _side + " L" + _level + (_v2 ? "v2" : "v1") + " alive=" + _my.Count + " retreating=" + ret + " pulling=" + pull + " blockers=" + slot + " foeAlive=" + _foe.Count + " focus=" + _focusA + "," + _focusB + " dmgAvg=" + _m.AvgDmg[1 - _side].ToString("0.0"));
        }
    }
}
