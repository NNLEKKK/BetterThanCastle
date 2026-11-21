using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Duel bot. One instance drives one side. Level 0 = naive sparring partner, level 1 = "Chosen" (tiers 1..3 are weaker limits of it).
    /// It acts only through BtcDuel.Attack/MoveAttack/Move, i.e. the same Order objects a player triggers; what it knows beyond a human:
    /// who every enemy unit is attacking (strike target / current task), exact hit points, velocities (position prediction), and it reacts every 50 ms.
    /// v2 (flag): threat map, wounded units are dragged BEHIND the friendly line so chasers walk through the army and get punished,
    /// body block (4 slots around an unprotected enemy) with live swap of wounded blockers, chase leash, safe-edge discipline.
    /// Planning every Step seconds, skip scheduler every frame, no allocations in the tick.
    /// </summary>
    internal sealed partial class BtcDuelBot : IDuelBot
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

        /// <summary>Feature mask for A/B testing (each bit = one increment over the frozen v2 base; see Has() uses).</summary>
#if SIM
        [System.ThreadStatic]
#endif
        internal static int Feat = 2050056 | 4194304 | 2097152 | 33554432 | 67108864 | 134217728 | 268435456;                 // production default: only increments that showed an A/B gain (2048 = engaged units stay on their target: 26/40 vs frozen v2)
        private static bool Has(int bit) { return (Feat & bit) != 0; }

        /// <summary>Skip in simulation frames after the strike (0 = use milliseconds). LabFrames cycles values for the timing lab.</summary>
#if SIM
        [System.ThreadStatic]
#endif
        internal static int SkipFrames = 0;
#if SIM
        [System.ThreadStatic]
#endif
        internal static int[] LabFrames;
#if SIM
        [System.ThreadStatic]
#endif
        private static int _labFr;

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

        internal BtcDuelBot(BtcDuelMatch m, int side, int level, int tier, bool v2)
        {
            _m = m; _side = side; _level = level; _tier = tier; _v2 = v2;
            Step = tier <= 1 ? 0.4f : (tier == 2 ? 0.2f : (tier == 3 ? 0.1f : 0.05f));
            _dir = side == 0 ? 1 : -1;
            BtcDuel.GuardOrders[side] = Has(1);          // bit 1: no orders inside the strike window (they cancel the swing)
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
            if ((SkipFrames > 0 || LabFrames != null || Has(4096)) && _tier >= 3)
            {
                int k = LabFrames != null && LabFrames.Length > 0 ? LabFrames[_labFr++ % LabFrames.Length] : (SkipFrames > 0 ? SkipFrames : 10);
                u.SkipFrameWait = k; u.SkipFrameK = k; u.SkipAt = -1f;
                return;
            }
            float d = _tier == 2 ? 330f + UnityEngine.Random.Range(0f, 60f) : (_tier == 3 ? SkipDelayMs + UnityEngine.Random.Range(0f, 30f) : (Has(256) ? 214f : SkipDelayMs));
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
                BtcDuelLog.Line("SKIP", "u" + u.Id + ">u" + t.Id + " delay=" + (int)((now - u.StrikeAt) * 1000f) + " fr=" + u.SkipFrameK);
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
            if (ContestMid()) return;
            if (Has(268435456)) UpdateSweep();
            if (Has(536870912)) TacticsPre();
            if (Has(134217728)) Guard();
            if (Has(536870912)) TacticsPost();
            if (Has(16)) Watchdog();
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
        /// <summary>The enemy alone in the mid is counting towards the 5 s space-control win: everybody goes in (wounded too after 2.5 s). Losing the match to a stopwatch is worse than any trade.</summary>
        private float _contestAt;
        private bool ContestMid()
        {
            float c = _m.Ctrl[1 - _side];
            if (c < 1.5f) return false;
            float now = Time.time;
            bool all = c > 5f;
            Vector3 centre = Floor3(MidCenterX, BtcDuelMap.CenterZ);
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef u = _my[i];
                if (u.Zone == BtcDuelZone.Mid) continue;
                if (!all && (u.Retreating || u.Pulling)) continue;
                if (now - u.LastOrderAt < 0.8f && u.TargetId == -12) continue;
                u.TargetId = -12; u.Pulling = false; u.Retreating = false; u.SkipAt = -1f; u.SlotIdx = -1;
                float ang = (u.Id * 2.399f) % 6.283f;
                BtcDuel.MoveAttack(u, Floor3(centre.x + Mathf.Cos(ang) * 4f, centre.z + Mathf.Sin(ang) * 4f), true);
            }
            if (now - _contestAt > 1.0f) { _contestAt = now; BtcDuelLog.Line("BOT", "contest mid: enemy space timer " + c.ToString("0.0") + " all=" + all); }
            return true;      // overrides the rest until a unit stands in the mid (the timer then resets)
        }

        /// <summary>Evasion layer (bit 134217728). (1) Dodge: when >=25% of the enemy army targets one unit it runs (equal speed, nobody catches a runner) and KEEPS running, never stopping behind the army, for as long as anybody within 14 m still targets it; the rest of the army hits the strung-out chasers (PickFocus bonus). Nobody targets it any more -> released at once and fights; the new victim of a switch starts its own dodge. Inside the own safe zone it stays while chased. (2) Rest: a wounded unit never fights, keeps away from every enemy and otherwise waits behind the healthy group until healed. Units under a hold are left alone by the rest of the bot.</summary>
        private float _guardLog;
        private static readonly float[] DodgeAng = { 0f, 0.7854f, -0.7854f, 1.5708f, -1.5708f, 2.356f, -2.356f };
        private void Guard()
        {
            float now = Time.time;
            int foeN = 0;
            for (int j = 0; j < _foe.Count; j++) if (_foe[j].Zone != _enemySafe) foeN++;
            int thr = Mathf.Max(2, Mathf.CeilToInt(foeN * 0.25f));
            int restN = 0; for (int q = 0; q < _my.Count; q++) if (_my[q].Rest) restN++;
            int restCap = Mathf.Max(1, Mathf.CeilToInt(_my.Count * 0.3f));     // against a shift+RMB sweep everybody gets hurt: only the weakest third may sit out, the rest keeps trading
            float healAt = BtcDuelConfig.InfiniteHeal ? 0.8f : 0.85f;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                float frac = me.Dr.CurrentHP / Mathf.Max(1f, me.MaxHp);
                if (me.Rest && frac > healAt) { restN--; me.Rest = false; me.HoldUntil = -1f; me.TargetId = -1; BtcDuelLog.Line("BOT", "rest over u" + me.Id); }
                else if (!me.Rest && frac < RestFrac(me) && restN < restCap && me.Zone != _ownSafe && !me.Anchor) { restN++; me.Rest = true; me.Dodging = false; me.Pulling = false; me.Retreating = false; me.PullFree = -1f; me.SwapIn = -1; me.HoldUntil = -1f; BtcDuelLog.Line("BOT", "rest u" + me.Id + " hp=" + (int)me.Dr.CurrentHP); }
                if (me.HoldUntil > now) continue;
                Vector3 p = me.Tf.position;
                float rx = 0f, rz = 0f, minD = 999f, fx = 0f, fz = 0f; int focus = 0, near = 0;
                for (int j = 0; j < _foe.Count; j++)
                {
                    BtcDuelUnitRef f = _foe[j];
                    if (f.Zone == _enemySafe) continue;
                    Vector3 fp = f.Tf.position;
                    float dx = p.x - fp.x, dz = p.z - fp.z, d2 = dx * dx + dz * dz, d = Mathf.Sqrt(d2);
                    if (f.FoeTargetId == me.Id && d < (me.Dodging ? 14f : 8.5f) && !(Has(268435456) && Sweeper(f))) { focus++; fx += fp.x; fz += fp.z; near++; }
                    if (d > 12f) continue;
                    if (d < minD) minD = d;
                    float w = 1f / Mathf.Max(1f, d2);
                    rx += dx * w; rz += dz * w;
                }
                if (!me.Rest)
                {
                    if (!me.Dodging && focus >= thr && !(Tac(8) && frac > 0.8f && me.FocusThreat < 2)) { me.Dodging = true; me.ChasedAt = now; BtcDuelLog.Line("BOT", "dodge start u" + me.Id + " focus=" + focus + "/" + thr + " hp=" + (int)me.Dr.CurrentHP); }
                    if (me.Dodging)
                    {
                        if (near > 0) me.ChasedAt = now;
                        if (now - me.ChasedAt > 0.5f) { me.Dodging = false; me.TargetId = -1; me.SkipAt = -1f; BtcDuelLog.Line("BOT", "dodge end u" + me.Id); }
                    }
                }
                if (me.Zone == _ownSafe) { if (me.Rest || me.Dodging) { me.HoldUntil = now + 0.4f; me.TargetId = -4; } continue; }
                if (EdgeGuard(me)) continue;
                if (!me.Rest && !me.Dodging) continue;
                float ax, az, hold;
                if (me.Dodging) { ax = p.x - fx / Mathf.Max(1, focus); az = p.z - fz / Mathf.Max(1, focus); hold = 0.5f; }
                else if (minD < 9f) { ax = rx; az = rz; hold = 0.7f; }
                else
                {
                    Vector3 gc = GroupCentre(me, 8f);
                    Vector3 spot = gc == Vector3.zero ? Floor3(OwnSafeInnerX, p.z) : Floor3(gc.x - _dir * 9f, gc.z);
                    me.HoldUntil = now + 1.0f;
                    if (Dist2D(p, spot) > 3.5f && (me.TargetId != -4 || me.Vel.sqrMagnitude < 0.04f)) { me.TargetId = -4; MoveSafe(me, spot, true); }
                    continue;
                }
                float al = Mathf.Sqrt(ax * ax + az * az);
                if (al < 0.05f) { ax = -_dir; az = 0f; al = 1f; }
                ax /= al; az /= al;
                // best escape direction: far from the chasers, with room to run (arena walls and the enemy safe zone count as dead ends), slightly towards the own side
                Vector3 bestPt = p; float bestSc = float.MinValue;
                float cx0 = p.x - ax * 8f, cz0 = p.z - az * 8f;      // chasers' centroid
                for (int k = 0; k < DodgeAng.Length; k++)
                {
                    float c = Mathf.Cos(DodgeAng[k]), s = Mathf.Sin(DodgeAng[k]);
                    float dx2 = ax * c - az * s, dz2 = ax * s + az * c;
                    Vector3 e = Floor3(p.x + dx2 * 20f, p.z + dz2 * 20f);
                    if (Past(e.x, 6f)) e.x = EnemySafeEdgeX - _dir * 6f;
                    float sc = Dist2D(e, new Vector3(cx0, 0f, cz0)) + 0.5f * Dist2D(e, p) + (-_dir * (e.x - p.x)) * 0.3f;
                    if (Tac(32)) sc += RouteScore(me, p, e);      // through or along the enemy army = free hits; towards their safe zone = bad position
                    if (sc > bestSc) { bestSc = sc; bestPt = e; }
                }
                me.HoldUntil = now + hold; me.TargetId = -4; me.SkipAt = -1f; me.SlotIdx = -1; me.Pulling = false; me.Retreating = false;
                MoveSafe(me, bestPt, true);
                if (now - _guardLog > 0.4f) { _guardLog = now; BtcDuelLog.Line("BOT", (me.Dodging ? "dodge u" : "keepaway u") + me.Id + " focus=" + focus + "/" + thr + " near=" + minD.ToString("0.0") + " hp=" + (int)me.Dr.CurrentHP); }
            }
        }

        // ---------------- anti-sweep (bit 268435456) ----------------
        // Shift+RMB on the ground (MoveAttack) makes every enemy hit its NEAREST unit. Those auto targets look like a focus, the old bot dodged/pulled
        // every unit in turn, never struck back and lost 38 vs 143 hits. Now: attack-move targets are not a focus (no dodge/pull), the army stands
        // and focus-fires (1 target, split on 2 with 10+ units) against the spread sweep; a real ordered focus (GoAttack on a unit) still triggers the dodge.
        private bool _sweep;
        private float _sweepOffAt = -1f;
        private readonly List<BtcDuelUnitRef> _swFree = new List<BtcDuelUnitRef>(48), _swCand = new List<BtcDuelUnitRef>(48);
        private readonly List<float> _swScore = new List<float>(48);

        private static bool Sweeper(BtcDuelUnitRef f) { return f.OrdKind == 1 && Time.time - f.OrdAt < 15f; }
        private static bool Runner(BtcDuelUnitRef f) { return f.OrdKind == 3 && Time.time - f.OrdAt < 6f; }

        private void UpdateSweep()
        {
            int n = 0, sw = 0;
            for (int j = 0; j < _foe.Count; j++) { BtcDuelUnitRef f = _foe[j]; if (f.Zone == _enemySafe || Runner(f)) continue; n++; if (Sweeper(f)) sw++; }
            float now = Time.time;
            if (!_sweep)
            {
                if (sw >= 3 && sw * 2 >= n) { _sweep = true; _sweepOffAt = -1f; BtcDuelLog.Line("BOT", "sweep on " + sw + "/" + n); }
            }
            else if (sw < 2 || sw * 10 < n * 3)
            {
                if (_sweepOffAt < 0f) _sweepOffAt = now;
                else if (now - _sweepOffAt > 1f) { _sweep = false; BtcDuelLog.Line("BOT", "sweep off " + sw + "/" + n); }
            }
            else _sweepOffAt = -1f;
        }

        /// <summary>Focus fire against the sweep: all free units on ONE target (fewer than 10 fighters) or a split focus on TWO (10+). A target that is being
        /// pulled away and still needs more than 3 hits is dropped at once (equal speed, a chase only wastes damage); with 3 hits or fewer it is chased.
        /// Returns false when there is nothing to fight here (normal logic advances).</summary>
        private int _swA = -1, _swB = -1;
        private bool SweepCounter(float now)
        {
            _swFree.Clear(); _swCand.Clear(); _swScore.Clear();
            Vector3 gc = Vector3.zero;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef u = _my[i];
                if (u.Retreating || u.Pulling || u.Rest || u.Dodging || EdgeGuard(u)) continue;
                _swFree.Add(u); gc += u.Tf.position;
            }
            if (_swFree.Count == 0) return false;
            gc /= _swFree.Count;
            for (int j = 0; j < _foe.Count; j++)
            {
                BtcDuelUnitRef f = _foe[j];
                if (f.Zone == _enemySafe || Past(f.Tf.position.x, 2.5f)) continue;
                float dmin = float.MaxValue; BtcDuelUnitRef nu = null;
                for (int i = 0; i < _swFree.Count; i++) { float d = Dist2D(_swFree[i].Tf.position, f.Tf.position); if (d < dmin) { dmin = d; nu = _swFree[i]; } }
                if (dmin > 10f) continue;
                float hits = HitsToDie(f, _side);
                if (Has(536870912) && (f.IgnoreUntil > now || TacSkip(f, hits, false))) continue;
                Vector3 away = f.Tf.position - nu.Tf.position;
                bool fleeing = Runner(f) || (f.Vel.sqrMagnitude > 4f && Vector3.Dot(f.Vel, away) > 0f);
                if (fleeing && hits > 3f && dmin > 2.5f)
                {
                    if (f.Id == _swA || f.Id == _swB) BtcDuelLog.Line("BOT", "sdrop u" + f.Id + " hits=" + hits + " d=" + dmin.ToString("0.0"));
                    continue;
                }
                int support = 0;
                for (int k = 0; k < _foe.Count; k++) if (k != j && _foe[k].Zone != _enemySafe && Dist2D(_foe[k].Tf.position, f.Tf.position) < 3.5f) support++;
                float s = f.Dr.CurrentHP + 20f * support + 6f * Dist2D(f.Tf.position, gc) - ((f.Id == _swA || f.Id == _swB) ? 80f : 0f) + 3.2f * TacScore(f);
                int at = 0; while (at < _swScore.Count && _swScore[at] <= s) at++;
                _swCand.Insert(at, f); _swScore.Insert(at, s);
            }
            if (_swCand.Count == 0) { _swA = _swB = -1; return false; }
            float dmg = _m.AvgDmg[_side];
            BtcDuelUnitRef a = _swCand[0];
            BtcDuelUnitRef b = _swFree.Count >= 10 && _swCand.Count > 1 ? _swCand[1] : null;
            // strikes already in flight kill A: the units not striking it go on (to B, else to the next candidate)
            float inflight = 0f;
            for (int i = 0; i < _my.Count; i++) { BtcDuelUnitRef u = _my[i]; if (u.StrikeTarget == a.Go && now - u.StrikeAt < 0.46f) inflight += dmg; }
            bool aDone = inflight >= a.Dr.CurrentHP;
            BtcDuelUnitRef next = b ?? (_swCand.Count > 1 ? _swCand[1] : null);
            if (a.Id != _swA || (b != null ? b.Id : -1) != _swB)
                BtcDuelLog.Line("BOT", "sfocus u" + a.Id + " hp=" + (int)a.Dr.CurrentHP + (b != null ? " + u" + b.Id + " hp=" + (int)b.Dr.CurrentHP : "") + " n=" + _swFree.Count);
            _swA = a.Id; _swB = b != null ? b.Id : -1;
            int cap = b != null ? (_swFree.Count + 1) / 2 : _swFree.Count, nA = 0, nB = 0;
            for (int i = 0; i < _swFree.Count; i++)
            {
                BtcDuelUnitRef me = _swFree[i];
                if (me.StrikeTarget != null && now - me.StrikeAt < 0.5f) { if (me.StrikeTarget == a.Go) nA++; else if (b != null && me.StrikeTarget == b.Go) nB++; continue; }     // never cancel a running strike
                BtcDuelUnitRef t = a;
                if (b != null)
                {
                    float da = Dist2D(me.Tf.position, a.Tf.position) - (me.TargetId == a.Id ? 2f : 0f), db = Dist2D(me.Tf.position, b.Tf.position) - (me.TargetId == b.Id ? 2f : 0f);
                    t = (db < da && nB < cap) || nA >= cap ? b : a;
                }
                if (t == a && aDone && next != null) t = next;
                if (Tac(16)) t = SlotCap(me, t, a, b, false);
                if (t == a) nA++; else nB++;
                bool idle = me.Vel.sqrMagnitude < 0.05f && now - me.LastOrderAt > 0.8f && Dist2D(me.Tf.position, t.Tf.position) > 3.5f;
                if (me.TargetId != t.Id || idle) { me.TargetId = t.Id; me.SlotIdx = -1; me.SkipAt = -1f; BtcDuel.Attack(me, t.Go); }
            }
            return true;
        }

        /// <summary>Orders get lost (landing stun, blocked path): a unit that should be moving but stands still gets its order again.</summary>
        private void Watchdog()
        {
            float now = Time.time;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef u = _my[i];
                if (u.Vel.sqrMagnitude > 0.04f || now - u.LastOrderAt < 0.9f || u.Zone == _ownSafe) continue;
                if (u.TargetId == -3 || u.Retreating) { u.TargetId = -3; MoveSafe(u, RearPoint(u)); }
                else if (u.TargetId == -6) MoveSafe(u, PullPoint(u));
                else if (u.TargetId == -2) { _advanced = false; }
                else if (u.TargetId >= 0)
                {
                    BtcDuelUnitRef t = ByIdAll(u.TargetId);
                    if (t != null && !t.Dead && LeashBlocked(t)) u.TargetId = -1;
                    else if (t != null && !t.Dead && Dist2D(u.Tf.position, t.Tf.position) > 3.6f) BtcDuel.Attack(u, t.Go);
                }
            }
        }

        private void Escape()
        {
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                if (me.Zone == _ownSafe) continue;
                me.SkipAt = -1f; me.SlotIdx = -1; me.Pulling = false;
                if (me.TargetId == -3) continue;
                me.TargetId = -3;
                MoveSafe(me, Floor3(OwnSafeInnerX, me.Tf.position.z));
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
        private float Ttd(BtcDuelUnitRef m)
        {
            if (m.ThreatCount == 0) return 99f;
            return Mathf.Max(0f, m.Dr.CurrentHP) / (m.ThreatCount * Mathf.Max(1f, _m.AvgDmg[1 - _side]) / 0.7f);
        }

        private int CountFoesWithin(BtcDuelUnitRef m, float r)
        {
            int n = 0;
            for (int j = 0; j < _foe.Count; j++) if (_foe[j].Zone != _enemySafe && Dist2D(m.Tf.position, _foe[j].Tf.position) < r) n++;
            return n;
        }

        /// <summary>Hits this unit took in the last w seconds (a real attack, not just a batch order that may be switched away).</summary>
        private static int RecentHits(BtcDuelUnitRef u, float w)
        {
            float now = Time.time; int n = 0;
            for (int i = 0; i < 4; i++) if (now - u.HitT[i] < w) n++;
            return n;
        }

        private int PullingCount()
        {
            int n = 0;
            for (int i = 0; i < _my.Count; i++) if (_my[i].Pulling) n++;
            return n;
        }

        private BtcDuelUnitRef ByIdAll(int id)
        {
            for (int i = 0; i < _my.Count; i++) if (_my[i].Id == id) return _my[i];
            for (int i = 0; i < _foe.Count; i++) if (_foe[i].Id == id) return _foe[i];
            return null;
        }

        private void UpdateThreat()
        {
            for (int i = 0; i < _my.Count; i++) { _my[i].ThreatCount = 0; _my[i].FocusThreat = 0; }
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
                if (t != null && t.Side == _side && Dist2D(f.Tf.position, t.Tf.position) < 8f) { t.ThreatCount++; if (Has(536870912) ? (f.OrdKind == 2 && f.OrdTargetId == t.Id && Time.time - f.OrdAt < 20f) : !Sweeper(f)) t.FocusThreat++; }     // tactics off: exactly the 5b86a49 rule
            }
        }

        /// <summary>Never let a unit drift into the enemy safe zone (instant loss): pull it back to a hold line.</summary>
        private bool EdgeGuard(BtcDuelUnitRef me)
        {
            float now = Time.time;
            if (me.HoldUntil > now) return true;
            Vector3 p = Pred(me, 0.5f);
            p = Pred(me, 0.9f);
            if (!Past(p.x, 6.0f) && !Past(me.Tf.position.x, 6.0f)) return false;
            me.HoldUntil = now + 1.2f; me.TargetId = -4; me.SkipAt = -1f; me.SlotIdx = -1;
            MoveSafe(me, Floor3(EnemySafeEdgeX - _dir * 6f, me.Tf.position.z), true);
            BtcDuelLog.Line("BOT", "edge hold u" + me.Id);
            return true;
        }

        private void ChosenThink()
        {
            UpdateThreat();
            bool engaged = false;
            for (int i = 0; i < _my.Count && !engaged; i++)
                for (int j = 0; j < _foe.Count; j++)
                    if (_foe[j].Zone != _enemySafe && (Dist2D(_my[i].Tf.position, _foe[j].Tf.position) < 9f || Dist2D(_my[i].Tf.position, Pred(_foe[j], 0.8f)) < 12.5f)) { engaged = true; break; }

            if (!engaged) { if (_m.Escaping[1 - _side]) Pursue(); else Advance(); return; }
            _advanced = false;
            Fight();
        }

        /// <summary>No enemy near: gather and take the middle of the mid zone (space control), wounded units stay back.</summary>
        /// <summary>The enemy has its kill and walks home: chase the stragglers (weakest / nearest) up to the safe-zone edge, no leash, no waiting in the middle.</summary>
        private void Pursue()
        {
            float now = Time.time;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                if (me.Retreating || me.Pulling || EdgeGuard(me)) continue;
                BtcDuelUnitRef best = null; float bs = float.MaxValue;
                for (int j = 0; j < _foe.Count; j++)
                {
                    BtcDuelUnitRef e = _foe[j];
                    if (e.Zone == _enemySafe || Past(e.Tf.position.x, 8f) || Past(Pred(e, 0.6f).x, 7f)) continue;
                    float s = Dist2D(me.Tf.position, e.Tf.position) + e.Dr.CurrentHP * 0.04f;
                    if (s < bs) { bs = s; best = e; }
                }
                if (best == null) continue;
                bool idle = me.Vel.sqrMagnitude < 0.05f && now - me.LastOrderAt > 0.8f;
                if (me.TargetId != best.Id || idle) { me.TargetId = best.Id; BtcDuel.Attack(me, best.Go); }
            }
        }

        private void Advance()
        {
            if (Has(4194304)) { PostureArc(); return; }
            if (_advanced) { for (int i = 0; i < _my.Count; i++) EdgeGuard(_my[i]); RetreatWounded(); return; }
            _advanced = true;
            if (Has(32)) { FormUp(); return; }
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                if (me.Retreating || me.Pulling) continue;
                me.TargetId = -2; me.SkipAt = -1f; me.SlotIdx = -1;
                BtcDuel.MoveAttack(me, Floor3(MidCenterX - _dir * 1.5f, me.Tf.position.z));
            }
        }

        /// <summary>Posture without contact. All free units get their own cell on an arc that faces the enemy (centre leads, wings trail), the arc is moved toward the enemy while we are not weaker, held otherwise. Every unit is re-ordered on its own timer, with a little individual jitter.</summary>
        private float _postLog;
        private void PostureArc()
        {
            float now = Time.time;
            _fm.Clear();
            for (int i = 0; i < _my.Count; i++) { BtcDuelUnitRef u = _my[i]; if (u.Retreating) { RetreatOne2(u); continue; } if (u.Pulling) continue; if (EdgeGuard(u)) continue; _fm.Add(u); }
            int n = _fm.Count; if (n == 0) return;
            Vector3 c = Vector3.zero;
            for (int i = 0; i < n; i++) c += _fm[i].Tf.position;
            c /= n;
            BtcDuelUnitRef near = null; float nd = float.MaxValue; float theirs = 0f, mine = 0f;
            for (int i = 0; i < n; i++) mine += _fm[i].Dr.CurrentHP;
            for (int j = 0; j < _foe.Count; j++)
            {
                BtcDuelUnitRef e = _foe[j];
                if (e.Zone == _enemySafe || Past(e.Tf.position.x, 4f)) continue;
                theirs += e.Dr.CurrentHP;
                float d = Dist2D(c, e.Tf.position);
                if (d < nd) { nd = d; near = e; }
            }
            Vector3 anchor; Vector3 fwd = new Vector3(_dir, 0f, 0f);
            bool push = near != null && nd < 70f && (mine >= theirs * 0.95f || near.Zone == BtcDuelZone.Mid);
            if (push)
            {
                Vector3 pe = Pred(near, 0.8f);
                fwd = pe - c; fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = new Vector3(_dir, 0f, 0f); fwd.Normalize();
                anchor = pe - fwd * 9.5f;                                          // the contact check (9 m) takes over from here
            }
            else anchor = new Vector3(MidCenterX - _dir * 3f, 0f, BtcDuelMap.CenterZ);
            if (Past(anchor.x, 8f)) anchor.x = EnemySafeEdgeX - _dir * 8f;
            if (now - _postLog > 2f) { _postLog = now; BtcDuelLog.Line("POST", "n=" + n + " push=" + push + " anchor=" + anchor.x.ToString("0") + "," + anchor.z.ToString("0") + " nd=" + nd.ToString("0") + " mine=" + (int)mine + " theirs=" + (int)theirs + " u0 dest=" + _fm[0].LastDest.x.ToString("0") + " vel=" + _fm[0].Vel.magnitude.ToString("0.0") + " tid=" + _fm[0].TargetId + " zone=" + _fm[0].Zone + " task=" + (_fm[0].Labor.GetCurrentOrPendingTask() == null ? "none" : _fm[0].Labor.GetCurrentOrPendingTask().Description) + " lastOrderAgo=" + (now - _fm[0].LastOrderAt).ToString("0.0") + " hold=" + (_fm[0].HoldUntil - now).ToString("0.0")); }
            Vector3 lat = new Vector3(-fwd.z, 0f, fwd.x);
            _fm.Sort(delegate (BtcDuelUnitRef a, BtcDuelUnitRef b2) { return Vector3.Dot(a.Tf.position, lat).CompareTo(Vector3.Dot(b2.Tf.position, lat)); });
            int cols = (n + 1) / 2;                                              // two ranks: a compact wedge about 1.5 m per column
            float span = Mathf.Min(n * 2.1f, BtcDuelMap.Wid - 8f);
            for (int k = 0; k < n; k++)
            {
                BtcDuelUnitRef me = _fm[k];
                Vector3 cell;
                if (Has(67108864))
                {
                    int col = k / 2, rank = k % 2;
                    float lo = (col - (cols - 1) * 0.5f) * 1.55f;
                    cell = anchor + lat * lo - fwd * (rank * 1.7f + Mathf.Abs(lo) * 0.18f);
                }
                else
                {
                    float f = n == 1 ? 0f : ((k + 0.5f) / n - 0.5f) * 2f;
                    cell = anchor + lat * (f * span * 0.5f) - fwd * (Mathf.Abs(f) * 2.2f);
                }
                cell.x += (UnityEngine.Random.value - 0.5f) * 1.2f; cell.z += (UnityEngine.Random.value - 0.5f) * 1.2f;     // own small jitter
                // a long order needs seconds to be pathed: re-ordering it on a timer restarts the search forever (units stood at the spawn). Refresh only close to the cell.
                float dcell = Dist2D(me.Tf.position, cell);
                bool idle = me.Vel.sqrMagnitude < 0.04f && now - me.LastOrderAt > 2.5f && dcell > 3f;
                Vector3 dest = Floor3(cell.x, cell.z);                           // same height as LastDest (Y used to differ: re-ordered every tick)
                float dd = Dist2D(me.LastDest, dest);
                bool far = dd > 4f;
                bool refresh = dcell < 25f && now >= me.NextOrderAt;
                if (!idle && !far && !refresh) continue;
                me.NextOrderAt = now + 1.3f + UnityEngine.Random.value * 1.2f;
                me.TargetId = -2; me.SkipAt = -1f; me.SlotIdx = -1;
                BtcDuel.MoveAttack(me, dest);
            }
        }

        private void RetreatOne2(BtcDuelUnitRef me)
        {
            if (me.Zone == _ownSafe) return;
            if (me.TargetId != -3 || (me.Vel.sqrMagnitude < 0.04f && Time.time - me.LastOrderAt > 1.2f)) { me.TargetId = -3; MoveSafe(me, RearPoint(me)); }
        }

        // Arc formation: every unit gets its own cell, sorted by current z so paths do not cross; the middle is slightly ahead (no single-file column)
        private readonly List<BtcDuelUnitRef> _fm = new List<BtcDuelUnitRef>(48);
        private void FormUp()
        {
            _fm.Clear();
            for (int i = 0; i < _my.Count; i++) if (!_my[i].Retreating) _fm.Add(_my[i]);
            _fm.Sort(delegate (BtcDuelUnitRef a, BtcDuelUnitRef b) { return a.Tf.position.z.CompareTo(b.Tf.position.z); });
            int n = _fm.Count; if (n == 0) return;
            float width = Mathf.Min(n * 2.3f, BtcDuelMap.Wid - 6f);
            float ax = MidCenterX - _dir * 1.5f;
            for (int k = 0; k < n; k++)
            {
                BtcDuelUnitRef me = _fm[k];
                float fz = n == 1 ? 0.5f : (k + 0.5f) / n;
                float zz = BtcDuelMap.CenterZ - width * 0.5f + fz * width;
                float mid = Mathf.Abs(fz - 0.5f) * 2f;                       // 0 at the centre, 1 at the wings
                float xx = ax + _dir * (1.8f * (1f - mid));
                me.TargetId = -2; me.SkipAt = -1f; me.Pulling = false; me.SlotIdx = -1;
                BtcDuel.MoveAttack(me, Floor3(xx, zz));
            }
        }

        /// <summary>Group centre of healthy free units (excluding one), or zero.</summary>
        private Vector3 GroupCentre(BtcDuelUnitRef except, float minHits)
        {
            float cx = 0f, cz = 0f; int n = 0;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef c = _my[i];
                if (c == except || c.Retreating || c.Pulling || HitsToDie(c, 1 - _side) < minHits) continue;
                cx += c.Tf.position.x; cz += c.Tf.position.z; n++;
            }
            return n == 0 ? Vector3.zero : new Vector3(cx / n, 0f, cz / n);
        }

        /// <summary>Where a withdrawn unit waits: 9 m behind the healthy group (inside its umbrella), never the far end of the arena. Falls back to the safe zone when nobody is left.</summary>
        private Vector3 RearPoint(BtcDuelUnitRef m)
        {
            Vector3 g = GroupCentre(m, 6f);
            if (g == Vector3.zero) return Floor3(OwnSafeInnerX, m.Tf.position.z);
            float x = g.x - _dir * (Has(33554432) ? 3.5f : 9f);
            float lim = OwnSafeInnerX;                         // never further back than the safe zone anchor
            x = _dir > 0 ? Mathf.Max(x, lim) : Mathf.Min(x, lim);
            float off = ((m.Id % 5) - 2) * 1.7f;
            return Floor3(x, g.z + off);
        }

        private void RetreatWounded()
        {
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                if (!me.Retreating || me.Zone == _ownSafe) continue;
                if (me.TargetId != -3) { me.TargetId = -3; MoveSafe(me, RearPoint(me)); }
            }
        }

        private void StartRetreat(BtcDuelUnitRef me, float left, BtcDuelUnitRef cover)
        {
            if (Has(134217728) && (me.Rest || me.HoldUntil > Time.time)) return;
            me.Retreating = true; me.Pulling = false; me.SkipAt = -1f; me.TargetId = -3; me.SlotIdx = -1; me.SwapIn = -1;
            BtcDuelLog.Line("BOT", "retreat u" + me.Id + " hits_left=" + left + (cover != null ? " cover=u" + cover.Id : ""));
            MoveSafe(me, RearPoint(me), left <= 2f);
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
                if (Has(8388608) && !allIn && me.ThreatCount >= 4 && RecentHits(me, 2.0f) >= 2 && me.Zone != _ownSafe)
                {
                    // 4+ attackers and the hits are landing: running only gives them free hits (data: real chases of 8-10 units killed 7/7 pulled units). It stays and the army counters.
                    me.Pulling = false; me.Retreating = false; me.PullFree = -1f; me.SwapIn = -1;
                    if (me.TargetId <= -2) me.TargetId = -1;
                    continue;
                }
                if ((me.Retreating || me.Pulling) && me.Zone != _ownSafe && !allIn && now - me.LastOrderAt > 0.7f && me.Vel.sqrMagnitude < 0.09f && now - me.LastHurtAt < 1.0f)
                {
                    // blocked by enemy bodies while hit: running is over, hit the weakest neighbour instead of standing still
                    BtcDuelUnitRef bt = null; float bh = float.MaxValue;
                    for (int j = 0; j < _foe.Count; j++) { float dj = Dist2D(me.Tf.position, _foe[j].Tf.position); if (dj < 3.4f && _foe[j].Dr.CurrentHP < bh) { bh = _foe[j].Dr.CurrentHP; bt = _foe[j]; } }
                    if (bt != null)
                    {
                        BtcDuelLog.Line("BOT", "trapped u" + me.Id + " fights u" + bt.Id);
                        me.Retreating = false; me.Pulling = false; me.TargetId = bt.Id; me.PullFree = -1f;
                        BtcDuel.Attack(me, bt.Go, true);
                        continue;
                    }
                }
                if (me.Retreating && me.Dr.CurrentHP > me.MaxHp * 0.85f) { me.Retreating = false; me.TargetId = -1; }     // healed up: back from the rear with the group
                if (Has(4) && (me.Retreating || me.Pulling) && me.Zone != _ownSafe && !allIn)
                {
                    // data: 76% of retreating units died while running without dealing damage; a runner needs ~2 hits from each chaser
                    float l0 = HitsToDie(me, 1 - _side);
                    if (me.ThreatCount > 0 && l0 < 2f * me.ThreatCount + 2f && NearFoe(me, 2.8f))
                    {
                        BtcDuelLog.Line("BOT", "turn and fight u" + me.Id + " hits_left=" + l0 + " threat=" + me.ThreatCount);
                        me.Retreating = false; me.Pulling = false; me.TargetId = -1; me.PullFree = -1f;
                        continue;
                    }
                }
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
                    if (Has(4) && me.ThreatCount > 0 && left < 2f * me.ThreatCount + 2f && !me.Pulling) continue;     // cannot survive running: keep hitting
                    if (Has(16777216) ? ((left <= 6f && thr && me.ThreatCount <= 2) || (left <= 3f && thr && me.ThreatCount <= 3) || (left <= 1f && thr)) : ((left <= 3f && (thr || left <= 1f)) || (me.ThreatCount >= 3 && left / me.ThreatCount <= 1.0f && HealthyFriends(me, 8f) < 2)))
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
                    else if ((Has(268435456) && _sweep && me.FocusThreat < 2) || (Tac(8) && me.FocusThreat < 2 && me.Dr.CurrentHP > me.MaxHp * 0.8f))
                    {
                        // attack-move threat is incidental (nearest unit): running hands the sweep free hits, stay and trade (SweepCounter)
                        if (me.Pulling) { me.Pulling = false; me.PullFree = -1f; me.TargetId = -1; }
                    }
                    else if (Has(524288) && !me.Pulling && me.ThreatCount >= 2 && Surrounded(me) >= 3 && HealthyFriends(me, 8f) >= 2 && PullingCount() < Mathf.Max(1, CountFighters() / 4)) StartPull(me);
                    else if ((!Has(16777216) || (me.ThreatCount <= 3 && RecentHits(me, 2.0f) >= 1)) && (Has(8) ? (me.ThreatCount >= 1 && Ttd(me) < 3.2f && PullingCount() < Mathf.Max(1, CountFighters() / 4)) : ((left <= 7f && me.ThreatCount >= 1) || (me.ThreatCount >= 2 && left / me.ThreatCount <= 2.6f))) && HealthyFriends(me, 8f) >= 2)
                    {
                        if (!me.Pulling) StartPull(me); else if (Has(16384)) KeepRunning(me, now);
                    }
                    else if (me.Pulling && Has(16384) && (me.ThreatCount > 0 || now - me.LastHurtAt < 1.5f)) KeepRunning(me, now);
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

            // 1b) counter: one of ours is under a real mass focus -> everybody hits the WEAKEST of its attackers (concentration beats the 8-10 spread hits)
            if (!allIn && Has(268435456) && _sweep && SweepCounter(now)) return;

            if (!allIn && Has(8388608))
            {
                BtcDuelUnitRef victim = null;
                for (int i = 0; i < _my.Count; i++)
                {
                    BtcDuelUnitRef u = _my[i];
                    if (u.ThreatCount >= 4 && RecentHits(u, 2.0f) >= 2 && (victim == null || u.ThreatCount > victim.ThreatCount)) victim = u;
                }
                if (victim != null)
                {
                    BtcDuelUnitRef counter = null; float ch = float.MaxValue;
                    for (int j = 0; j < _foe.Count; j++)
                    {
                        BtcDuelUnitRef f = _foe[j];
                        if (f.FoeTargetId != victim.Id || f.Zone == _enemySafe || Past(f.Tf.position.x, 2.5f)) continue;
                        if (Dist2D(f.Tf.position, victim.Tf.position) > 6f) continue;
                        float s = f.Dr.CurrentHP + Dist2D(f.Tf.position, victim.Tf.position) * 4f;
                        if (s < ch) { ch = s; counter = f; }
                    }
                    if (counter != null)
                    {
                        if (_focusA != counter.Id) BtcDuelLog.Line("BOT", "counter victim=u" + victim.Id + " threat=" + victim.ThreatCount + " -> weakest attacker u" + counter.Id + " hp=" + (int)counter.Dr.CurrentHP);
                        _focusA = counter.Id; _focusB = -1;
                        for (int i = 0; i < _my.Count; i++)
                        {
                            BtcDuelUnitRef u = _my[i];
                            if (u.Retreating || u.Pulling || EdgeGuard(u)) continue;
                            bool idle = u.Vel.sqrMagnitude < 0.05f && now - u.LastOrderAt > 0.8f;
                            if (u.TargetId != counter.Id || idle) { u.TargetId = counter.Id; u.SlotIdx = -1; BtcDuel.Attack(u, counter.Go); }
                        }
                        return;
                    }
                }
            }

            // 2) focus targets
            int want = CountFighters() >= (Has(512) ? 10 : 7) ? 2 : 1;
            BtcDuelUnitRef fa = PickFocus(_focusA, -1, allIn);
            BtcDuelUnitRef fb = want > 1 ? PickFocus(_focusB, fa != null ? fa.Id : -1, allIn) : null;
            _focusA = fa != null ? fa.Id : -1; _focusB = fb != null ? fb.Id : -1;
            if (fa == null && allIn)
            {
                // the enemy has its kill and walks home, nobody is catchable: do not give the round away, press up to the safe zone edge and hit whoever lingers
                for (int i = 0; i < _my.Count; i++)
                {
                    BtcDuelUnitRef u = _my[i];
                    if (u.Retreating || u.Pulling || u.Rest || EdgeGuard(u)) continue;
                    bool idle = u.Vel.sqrMagnitude < 0.05f && now - u.LastOrderAt > 0.8f;
                    if (u.TargetId != -13 || idle) { u.TargetId = -13; u.SkipAt = -1f; u.SlotIdx = -1; BtcDuel.MoveAttack(u, Floor3(PressX, u.Tf.position.z)); }
                }
                return;
            }
            if (fa == null) { Advance(); return; }

            // 3) body block on a single unprotected target, else plain assignment
            bool block = _v2 && fb == null && CountFighters() >= 3 && BlockWorthy(fa);
            if (!block) ClearSlots();
            if (block) ManageBlock(fa);

            BtcDuelUnitRef alt = null;
            if (Has(2) && !block)
            {
                // strikes already in flight kill the focus: everyone else starts on the next target instead of wasting hits
                float inflight = 0f;
                for (int i = 0; i < _my.Count; i++) { BtcDuelUnitRef u = _my[i]; if (u.StrikeTarget == fa.Go && now - u.StrikeAt < 0.46f) inflight += _m.AvgDmg[_side]; }
                if (inflight >= fa.Dr.CurrentHP) alt = PickFocus(-1, fa.Id, allIn);
            }
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                if (me.Retreating || me.Pulling) continue;
                if (EdgeGuard(me)) continue;
                if (block && me.SlotIdx >= 0) continue;     // handled by ManageBlock
                if (!allIn && me.ThreatCount == 0 && !NearFoe(me, 2.9f))
                {
                    float nf = float.MaxValue;
                    for (int q = 0; q < _my.Count; q++) { if (_my[q] == me || _my[q].Retreating) continue; float dq = Dist2D(me.Tf.position, _my[q].Tf.position); if (dq < nf) nf = dq; }
                    Vector3 gcI = GroupCentre(me, 4f);
                    if (nf > 9f && gcI != Vector3.zero && CountFoesWithin(me, 14f) > 0)
                    {
                        if (me.TargetId != -11 || (me.Vel.sqrMagnitude < 0.05f && now - me.LastOrderAt > 0.8f))
                        { me.TargetId = -11; me.SkipAt = -1f; BtcDuelLog.Line("BOT", "regroup isolated u" + me.Id + " nearest friend " + nf.ToString("0")); MoveSafe(me, Floor3(gcI.x, gcI.z + ((me.Id % 5) - 2) * 1.6f)); }
                        continue;
                    }
                }
                if (Has(1048576) && !allIn && me.ThreatCount == 0 && !NearFoe(me, 5.5f))
                {
                    // tight group: a free unit that lags behind returns to its own place next to the group instead of trailing in a line
                    Vector3 gc = Vector3.zero; int gn = 0;
                    for (int q = 0; q < _my.Count; q++) { if (_my[q] == me || _my[q].Retreating || _my[q].Pulling) continue; gc += _my[q].Tf.position; gn++; }
                    if (gn >= 2)
                    {
                        gc /= gn;
                        if (Dist2D(me.Tf.position, gc) > 5.5f + 0.4f * gn && (me.TargetId != -10 || (me.Vel.sqrMagnitude < 0.05f && now - me.LastOrderAt > 1.0f)))
                        {
                            float ang = (me.Id * 2.399f) % 6.283f;                                    // own fixed slot around the group centre
                            me.TargetId = -10; MoveSafe(me, Floor3(gc.x + Mathf.Cos(ang) * 2.2f, gc.z + Mathf.Sin(ang) * 2.2f));
                            continue;
                        }
                    }
                }
                if (Has(131072) && !allIn && me.ThreatCount == 0 && HitsToDie(me, 1 - _side) <= 10f && HealthyFriends(me, 11f) >= 3)
                {
                    // time-to-kill is tiny: a half-HP unit that walks into the melee dies. It waits behind the line until it is needed.
                    if (me.TargetId != -8 || (me.Vel.sqrMagnitude < 0.05f && now - me.LastOrderAt > 1.5f)) { me.TargetId = -8; me.SkipAt = -1f; MoveSafe(me, PullPoint(me)); }
                    continue;
                }
                if (Has(1024) || Has(2048))
                {
                    // data: 70% of the long pauses between strikes (>1.6 s) are target switches. An engaged unit stays on its target.
                    BtcDuelUnitRef cur = me.TargetId >= 0 ? ByIdAll(me.TargetId) : null;
                    if (cur != null && !cur.Dead && cur.Zone != _enemySafe && !Past(cur.Tf.position.x, 2.5f) && Dist2D(me.Tf.position, cur.Tf.position) < 3.6f && (Has(1024) || HitsToDie(cur, _side) <= (Has(8192) ? 12f : 8f))) continue;
                }
                BtcDuelUnitRef t = fa;
                if (alt != null && !(me.StrikeTarget == fa.Go && now - me.StrikeAt < 0.46f)) t = alt;
                if (alt == null && fb != null && Dist2D(me.Tf.position, fb.Tf.position) < Dist2D(me.Tf.position, fa.Tf.position) - 0.5f) t = fb;
                if (Tac(16)) t = SlotCap(me, t, fa, fb, allIn);
                if (me.TargetId == t.Id) continue;
                me.TargetId = t.Id;
                BtcDuel.Attack(me, t.Go);
            }
        }

        /// <summary>A dodged focus must not stand still after the first step (the chasers just follow): keep running to the safe zone while anybody chases, order again if the unit idles.</summary>
        private void KeepRunning(BtcDuelUnitRef me, float now)
        {
            me.PullFree = -1f;
            if (me.Zone == _ownSafe) return;
            if (me.TargetId != -7 || (me.Vel.sqrMagnitude < 0.06f && now - me.LastOrderAt > 0.5f))
            {
                me.TargetId = -7;
                // really hit and nobody around: all the way to the safe zone; otherwise only to the rear of the group
                bool alone = GroupCentre(me, 6f) == Vector3.zero || Dist2D(me.Tf.position, GroupCentre(me, 6f)) > 14f;
                MoveSafe(me, (now - me.LastHurtAt < 1.0f && alone) ? Floor3(OwnSafeInnerX, me.Tf.position.z) : RearPoint(me), true);
            }
        }

        /// <summary>Quadrants (E/W/N/S) around the unit that hold an enemy within 2.7: 3-4 means it is being ringed in.</summary>
        private int Surrounded(BtcDuelUnitRef me)
        {
            bool qE = false, qW = false, qN = false, qS = false;
            for (int j = 0; j < _foe.Count; j++)
            {
                float dx = _foe[j].Tf.position.x - me.Tf.position.x, dz = _foe[j].Tf.position.z - me.Tf.position.z;
                if (dx * dx + dz * dz > 7.3f) continue;
                if (Mathf.Abs(dx) >= Mathf.Abs(dz)) { if (dx > 0) qE = true; else qW = true; } else { if (dz > 0) qN = true; else qS = true; }
            }
            return (qE ? 1 : 0) + (qW ? 1 : 0) + (qN ? 1 : 0) + (qS ? 1 : 0);
        }

        private void StartPull(BtcDuelUnitRef me)
        {
            if (Has(134217728) && (me.Rest || me.HoldUntil > Time.time)) return;
            me.Pulling = true; me.PullFree = -1f; me.SkipAt = -1f; me.TargetId = -6; me.SlotIdx = -1;
            BtcDuelLog.Line("BOT", "pull u" + me.Id + " hp=" + (int)me.Dr.CurrentHP + " threat=" + me.ThreatCount);
            MoveSafe(me, PullPoint(me));
        }

        // ---------------- body block ----------------
        private bool BlockWorthy(BtcDuelUnitRef b)
        {
            if (b.Zone == _enemySafe || Past(b.Tf.position.x, 6f) || LeashBlocked(b)) return false;
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
                if (Past(e.Tf.position.x, 8f) || Past(pe.x, 7f)) continue;             // never chase a unit that is about to enter its safe zone
                if (_v2 && e.IgnoreUntil > now) continue;
                float dist = Dist2D(c, e.Tf.position);
                if (Tac(32) && e.PathLen >= 0f) dist = Mathf.Max(dist, e.PathLen);      // walking around bodies is longer than the straight line
                if (dist > maxDist) continue;
                float h = HitsToDie(e, _side);
                if (Has(536870912) && TacSkip(e, h, allIn)) continue;
                int guards = 0;
                for (int k = 0; k < _foe.Count; k++) if (k != j && Dist2D(_foe[k].Tf.position, e.Tf.position) < 3.5f) guards++;
                // leash: do not run after a unit that walks away towards its own army / safe zone
                if (Has(64))
                {
                    if (allIn && e.Vel.x * _dir > 1.0f)
                    {
                        bool closeBy = false;
                        for (int q = 0; q < _my.Count && !closeBy; q++) if (!_my[q].Retreating && !_my[q].Pulling && !_my[q].Rest && Dist2D(_my[q].Tf.position, e.Tf.position) < 7f) closeBy = true;
                        if (!closeBy) continue;
                    }
                    else if (e.Vel.x * _dir > 1.0f)
                    {
                        int onIt = 0;
                        for (int q = 0; q < _my.Count; q++) if (!_my[q].Retreating && !_my[q].Pulling && Dist2D(_my[q].Tf.position, e.Tf.position) < 2.8f) onIt++;
                        if (!(onIt >= 2 && h <= 3f)) { e.IgnoreUntil = now + 2.5f; continue; }
                    }
                }
                else if (_v2 && !allIn && h > 1f && dist > 9f && e.Vel.x * _dir > 1.2f) { e.IgnoreUntil = now + 3f; continue; }
                float score = h * 10f + dist * 0.8f + guards * 4f + TacScore(e);
                if (Has(262144))
                {
                    // pre-surrounded targets die faster: count the quadrants around it that already hold one of ours
                    bool qE = false, qW = false, qN = false, qS = false;
                    for (int q = 0; q < _my.Count; q++)
                    {
                        BtcDuelUnitRef m = _my[q];
                        if (m.Retreating || m.Pulling) continue;
                        float dx = m.Tf.position.x - e.Tf.position.x, dz = m.Tf.position.z - e.Tf.position.z;
                        if (dx * dx + dz * dz > 11.6f) continue;
                        if (Mathf.Abs(dx) >= Mathf.Abs(dz)) { if (dx > 0) qE = true; else qW = true; } else { if (dz > 0) qN = true; else qS = true; }
                    }
                    int sides = (qE ? 1 : 0) + (qW ? 1 : 0) + (qN ? 1 : 0) + (qS ? 1 : 0);
                    score -= 3.5f * sides;
                }
                if (Has(128))
                {
                    // reach: how many of ours can hit it right now (more attackers = it dies sooner)
                    int reach = 0;
                    for (int q = 0; q < _my.Count; q++) if (!_my[q].Retreating && !_my[q].Pulling && Dist2D(_my[q].Tf.position, e.Tf.position) < 3.4f) reach++;
                    score -= 1.5f * reach;
                }
                if (_v2)
                {
                    BtcDuelUnitRef tgt = e.FoeTargetId >= 0 ? ById(e.FoeTargetId) : null;
                    if (tgt != null && (tgt.Pulling || tgt.Retreating || tgt.ThreatCount >= 3)) score -= Has(65536) ? 30f : 12f;     // punish the chaser of our bait
                    if (tgt != null && Has(134217728) && (tgt.Dodging || tgt.HoldUntil > now || tgt.Rest)) score -= 45f;     // the chasers of a dodging unit run behind it, strung out: hit them
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
