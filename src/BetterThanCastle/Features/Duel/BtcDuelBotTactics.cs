using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Tactics layer (bit 536870912, sub-bits in Feat2), built from human games (Kirusha / Monotony logs, 2026-10-30):
    /// 16 reachable-slot targeting: a unit inside a knot of its friends cannot be hit, pick targets with free strike spots and cap attackers per target;
    /// 1 local odds: a unit (or group) that faces 2+ more enemies than it has friends nearby does not take the fight, it joins the nearest friends or backs off;
    /// 2 mid anchor + army leash: one healthy unit always holds the mid (blocks the enemy space timer, runs ours), nobody chases far past the mid;
    /// 4 chase leash: a target that the attackers do not close on (equal speed) is dropped after 1.5 s, runners that lead into the enemy block are not chased;
    /// 8 hp management: rest earlier when outnumbered, no dodge/pull at high hp without a real ordered focus.
    /// 32 paths: moves are steered around bodies and away from enemy reach (BtcDuelPath), target distance = path length, dodges never run towards
    ///    the enemy safe zone / through the enemy army, idle units far past the mid (camping under the enemy safe zone) come back.
    /// </summary>
    internal sealed partial class BtcDuelBot
    {
#if SIM
        [System.ThreadStatic]
#endif
        internal static int Feat2 = 63;
        private static bool Tac(int bit) { return (Feat & 536870912) != 0 && (Feat2 & bit) != 0; }

        private BtcDuelUnitRef _anchor;
        private float _tacLog, _noslotLog;
        private static readonly float[] SlotCos = new float[8], SlotSin = new float[8];
        static BtcDuelBot()
        {
            for (int k = 0; k < 8; k++) { SlotCos[k] = Mathf.Cos(k * 0.7854f); SlotSin[k] = Mathf.Sin(k * 0.7854f); }
        }

        private float RestFrac(BtcDuelUnitRef me) { return Tac(8) && me.OddsK >= 2 && me.OddsK >= me.OddsF + 1 ? 0.6f : 0.45f; }

        private float OwnMidEdgeX { get { return _dir > 0 ? BtcDuelMap.MidX0 : BtcDuelMap.MidX1; } }
        private float EnemyMidEdgeX { get { return _dir > 0 ? BtcDuelMap.MidX1 : BtcDuelMap.MidX0; } }
        private bool BeyondLeash(float x) { float line = EnemyMidEdgeX + _dir * 10f; return _dir > 0 ? x > line : x < line; }
        /// <summary>Army leash: no chase far past the mid (into the enemy block / after wounded walking home) unless the kill is on.</summary>
        private bool LeashBlocked(BtcDuelUnitRef e) { return Tac(2) && !_m.Escaping[1 - _side] && BeyondLeash(e.Tf.position.x) && !(HitsToDie(e, _side) <= 2f && InReach(e) > 0); }
        private static bool Outnumbered(BtcDuelUnitRef u) { return (u.OddsF == 1 && u.OddsK >= 2) || (u.OddsK >= u.OddsF + 2 && u.OddsK * 2 >= u.OddsF * 3); }     // 1v2, 2v4, 3v5, 4v6 .. but not 8v10: big even-ish fights are traded

        private int InReach(BtcDuelUnitRef e)
        {
            int n = 0;
            for (int q = 0; q < _my.Count; q++) if (!_my[q].Retreating && !_my[q].Pulling && Dist2D(_my[q].Tf.position, e.Tf.position) < 3.4f) n++;
            return n;
        }

        private Vector3 FreeCentre()
        {
            Vector3 c = Vector3.zero; int n = 0;
            for (int i = 0; i < _my.Count; i++) { BtcDuelUnitRef u = _my[i]; if (u.Retreating || u.Pulling || u.Rest || u.Anchor) continue; c += u.Tf.position; n++; }
            return n > 0 ? c / n : Vector3.zero;
        }

        /// <summary>Before Guard: free strike spots around every enemy, local numbers around every own unit, anchor validity.</summary>
        private void TacticsPre()
        {
            if (Tac(16))
            {
                Vector3 gc = FreeCentre();
                for (int j = 0; j < _foe.Count; j++)
                {
                    BtcDuelUnitRef e = _foe[j];
                    Vector3 ep = e.Tf.position;
                    int free = 0; float best = 999f;
                    for (int k = 0; k < 8; k++)
                    {
                        float sx = ep.x + SlotCos[k] * 1.7f, sz = ep.z + SlotSin[k] * 1.7f;
                        bool taken = false;
                        for (int q = 0; q < _foe.Count && !taken; q++) { if (q == j) continue; float dx = _foe[q].Tf.position.x - sx, dz = _foe[q].Tf.position.z - sz; if (dx * dx + dz * dz < 0.81f) taken = true; }
                        for (int q = 0; q < _my.Count && !taken; q++) { float dx = _my[q].Tf.position.x - sx, dz = _my[q].Tf.position.z - sz; if (dx * dx + dz * dz < 0.81f) taken = true; }
                        if (taken) continue;
                        free++;
                        if (gc != Vector3.zero)
                        {
                            float d = Mathf.Sqrt((gc.x - sx) * (gc.x - sx) + (gc.z - sz) * (gc.z - sz));
                            if ((sx - ep.x) * (gc.x - ep.x) + (sz - ep.z) * (gc.z - ep.z) < 0f) d += 3f;     // spot on the far side: we must walk around the target / its friends
                            if (d < best) best = d;
                        }
                    }
                    e.Slots = free; e.SlotD = best < 999f ? best : 0f;
                }
            }
            if (Tac(32)) FoePaths();
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                Vector3 p = me.Tf.position;
                int k = 0, f = 0;
                for (int j = 0; j < _foe.Count; j++) if (_foe[j].Zone != _enemySafe && Dist2D(_foe[j].Tf.position, p) < 9f) k++;
                for (int q = 0; q < _my.Count; q++) if (!_my[q].Rest && Dist2D(_my[q].Tf.position, p) < 9f) f++;
                me.OddsK = k; me.OddsF = Mathf.Max(1, f);
            }
            if (_anchor != null && (_anchor.Dead || !_my.Contains(_anchor) || _anchor.Rest || _anchor.Retreating || _anchor.Dr.CurrentHP < _anchor.MaxHp * 0.45f || !Tac(2) || _my.Count < 3 || _m.Escaping[1 - _side]))
            {
                _anchor.Anchor = false; _anchor.HoldUntil = -1f; _anchor.TargetId = -1;
                BtcDuelLog.Line("BOT", "anchor off u" + _anchor.Id + " hp=" + (int)_anchor.Dr.CurrentHP);
                _anchor = null;
            }
        }

        /// <summary>After Guard: anchor, local odds, chase leash.</summary>
        private void TacticsPost()
        {
            float now = Time.time;
            bool allIn = _m.Escaping[1 - _side];
            if (Tac(2) && !allIn && _my.Count >= 3) Anchor(now);
            if (Tac(1)) Odds(now);
            if (Tac(4)) ChaseLeash(now);
            if (Tac(32)) { ReturnIdle(now, allIn); Steer(now); }
        }

        private void Anchor(float now)
        {
            Vector3 hold = Floor3(OwnMidEdgeX + _dir * 4f, BtcDuelMap.CenterZ);
            // only needed when the army is away from the mid: a unit standing alone at the mid edge while the fight is next to it = playing a unit down
            Vector3 army = FreeCentre();
            bool away = army != Vector3.zero && (army.x < BtcDuelMap.MidX0 - 8f || army.x > BtcDuelMap.MidX1 + 8f);
            bool give = _anchor != null && Outnumbered(_anchor) && NearFoe(_anchor, 5f);     // alone vs many: give the mid up, ContestMid takes it back with the army
            if (!away || give)
            {
                if (_anchor != null) { _anchor.Anchor = false; _anchor.HoldUntil = -1f; _anchor.TargetId = -1; BtcDuelLog.Line("BOT", "anchor off u" + _anchor.Id + (give ? " outnumbered" : " army near")); _anchor = null; }
                return;
            }
            if (_anchor == null)
            {
                BtcDuelUnitRef best = null; float bs = float.MaxValue;
                for (int i = 0; i < _my.Count; i++)
                {
                    BtcDuelUnitRef u = _my[i];
                    if (u.Rest || u.Retreating || u.Pulling || u.Dodging || u.Dr.CurrentHP < u.MaxHp * 0.55f || u.Zone == _ownSafe) continue;
                    float s = Dist2D(u.Tf.position, hold) - 10f * u.Dr.CurrentHP / Mathf.Max(1f, u.MaxHp) + 3f * u.ThreatCount;
                    if (s < bs) { bs = s; best = u; }
                }
                if (best == null) return;
                _anchor = best; best.Anchor = true; best.Pulling = false; best.SlotIdx = -1; best.SwapIn = -1; best.TargetId = -1;
                BtcDuelLog.Line("BOT", "anchor u" + best.Id + " hp=" + (int)best.Dr.CurrentHP);
            }
            BtcDuelUnitRef a = _anchor;
            if (a.Dodging) return;
            Vector3 p = a.Tf.position;
            a.HoldUntil = now + 0.3f;           // the rest of the bot leaves the anchor alone
            bool idle = a.Vel.sqrMagnitude < 0.05f && now - a.LastOrderAt > 0.8f;
            if (a.Zone != BtcDuelZone.Mid)
            {
                if (a.TargetId != -20 || idle) { a.TargetId = -20; a.SkipAt = -1f; MoveSafe(a, Floor3(hold.x, Mathf.Clamp(p.z, BtcDuelMap.CenterZ - 6f, BtcDuelMap.CenterZ + 6f)), true); }
                return;
            }
            if (Outnumbered(a))
            {
                // keep the mid blocked but give no free hits: stand at our edge of it, the army is behind
                Vector3 back = Floor3(OwnMidEdgeX + _dir * 1.2f, p.z);
                if (a.StrikeTarget != null && now - a.StrikeAt < 0.45f) return;
                if (a.TargetId != -21 || (idle && Dist2D(p, back) > 1.5f)) { a.TargetId = -21; a.SkipAt = -1f; MoveSafe(a, back, true); }
                return;
            }
            BtcDuelUnitRef t = null; float td = 6f;
            for (int j = 0; j < _foe.Count; j++) { BtcDuelUnitRef e = _foe[j]; if (e.Zone != BtcDuelZone.Mid) continue; float d = Dist2D(p, e.Tf.position); if (d < td) { td = d; t = e; } }
            if (t != null) { if (a.TargetId != t.Id || idle) { a.TargetId = t.Id; BtcDuel.Attack(a, t.Go); } return; }
            if (a.StrikeTarget != null && now - a.StrikeAt < 0.45f) return;
            if (Dist2D(p, hold) > 3f && (a.TargetId != -20 || idle)) { a.TargetId = -20; a.SkipAt = -1f; MoveSafe(a, Floor3(hold.x, Mathf.Clamp(p.z, BtcDuelMap.CenterZ - 6f, BtcDuelMap.CenterZ + 6f)), true); }
        }

        /// <summary>Never take a fight at a numeric disadvantage: an outnumbered unit/group joins the nearest friends that are not outnumbered, else backs off together.</summary>
        private void Odds(float now)
        {
            // join point: the biggest group of friends that is not outnumbered (a lone unit is no shelter)
            Vector3 safe = Vector3.zero; int sn = 0;
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef u = _my[i];
                if (u.Rest || u.Anchor || Outnumbered(u)) continue;
                int g = 0; Vector3 c = Vector3.zero;
                for (int q = 0; q < _my.Count; q++) { BtcDuelUnitRef v = _my[q]; if (v.Rest || v.Anchor || Outnumbered(v) || Dist2D(v.Tf.position, u.Tf.position) > 8f) continue; g++; c += v.Tf.position; }
                if (g >= 2 && g > sn) { sn = g; safe = c / g; }
            }
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef me = _my[i];
                if (me.Anchor || me.Rest || me.Dodging || me.Retreating || me.HoldUntil > now || me.Zone == _ownSafe) continue;
                bool backing = me.TargetId == -22;
                bool bad = backing ? me.OddsK > me.OddsF : Outnumbered(me);
                float md = 99f;
                for (int j = 0; j < _foe.Count; j++) { float dj = Dist2D(_foe[j].Tf.position, me.Tf.position); if (dj < md) md = dj; }
                if (md < 3.2f) bad = false;      // already in contact: running from equal-speed melee units only hands out free hits, the fight logic (trapped/counter) handles it
                if (!bad) { if (backing) me.TargetId = -1; continue; }
                if (me.StrikeTarget != null && now - me.StrikeAt < 0.45f) continue;          // never cancel a running strike
                BtcDuelUnitRef cur = me.TargetId >= 0 ? ByIdAll(me.TargetId) : null;
                if (cur != null && !cur.Dead && HitsToDie(cur, _side) <= 1f && Dist2D(me.Tf.position, cur.Tf.position) < 3.6f) continue;   // finish the kill first
                Vector3 p = me.Tf.position;
                Vector3 dest = sn > 0 && Dist2D(p, safe) > 4f && Dist2D(p, safe) < 40f ? Floor3(safe.x - _dir * 1.5f, safe.z + ((me.Id % 5) - 2) * 1.5f) : Floor3(p.x - _dir * 8f, p.z);
                me.HoldUntil = now + 0.6f; me.Pulling = false; me.SlotIdx = -1; me.SwapIn = -1; me.SkipAt = -1f;
                if (!backing || me.Vel.sqrMagnitude < 0.05f || Dist2D(me.LastDest, dest) > 3f) { me.TargetId = -22; MoveSafe(me, dest, true); }
                if (now - _tacLog > 0.5f) { _tacLog = now; BtcDuelLog.Line("BOT", "odds u" + me.Id + " " + me.OddsK + "v" + me.OddsF + " hp=" + (int)me.Dr.CurrentHP + (sn > 0 ? " join" : " back")); }
            }
        }

        /// <summary>Equal speed: a target the attackers do not close on is never caught. Drop it after 1.5 s unless it is nearly dead.</summary>
        private void ChaseLeash(float now)
        {
            for (int j = 0; j < _foe.Count; j++)
            {
                BtcDuelUnitRef e = _foe[j];
                float d = float.MaxValue; int n = 0;
                for (int i = 0; i < _my.Count; i++) { BtcDuelUnitRef u = _my[i]; if (u.TargetId != e.Id) continue; n++; float di = Dist2D(u.Tf.position, e.Tf.position); if (di < d) d = di; }
                if (n == 0 || d < 3.6f) { e.ChaseAt = -1f; continue; }
                if (e.ChaseAt < 0f) { e.ChaseAt = now; e.ChaseD = d; continue; }
                float dt = now - e.ChaseAt;
                if (dt < 1.5f) continue;
                if ((e.ChaseD - d) / dt < 0.5f && HitsToDie(e, _side) > 3f)
                {
                    e.IgnoreUntil = now + 3f;
                    for (int i = 0; i < _my.Count; i++) if (_my[i].TargetId == e.Id) _my[i].TargetId = -1;
                    if (_focusA == e.Id) _focusA = -1;
                    if (_focusB == e.Id) _focusB = -1;
                    BtcDuelLog.Line("BOT", "leash u" + e.Id + " d=" + d.ToString("0.0") + " hits=" + HitsToDie(e, _side) + " by " + n);
                    e.ChaseAt = -1f;
                }
                else { e.ChaseAt = now; e.ChaseD = d; }
            }
        }

        /// <summary>PickFocus filter: targets that cannot be reached (no free spot), lie far past the mid, or are bait running into the enemy block.</summary>
        private bool TacSkip(BtcDuelUnitRef e, float h, bool allIn)
        {
            if (Tac(16) && e.Slots == 0 && InReach(e) == 0) return true;
            if (allIn) return false;
            if (LeashBlocked(e)) return true;
            if (Tac(4) && e.Vel.sqrMagnitude > 4f)
            {
                Vector3 gc = FreeCentre();
                if (gc != Vector3.zero && Vector3.Dot(e.Vel, e.Tf.position - gc) > 0f && InReach(e) < 2)
                {
                    Vector3 q = Pred(e, 1.5f); int block = 0;
                    for (int k = 0; k < _foe.Count; k++) if (_foe[k] != e && Dist2D(_foe[k].Tf.position, q) < 10f) block++;
                    if (block >= 3) return true;
                }
            }
            return false;
        }

        private float TacScore(BtcDuelUnitRef e) { return (Tac(16) ? -6f * e.Slots + 0.8f * e.SlotD : 0f) + (Tac(32) ? 2f * e.PathDanger : 0f); }

        private BtcDuelUnitRef _spill; private float _spillAt = -1f;
        /// <summary>Attackers per target are capped by its free spots: the extra units (and units whose target has no free spot) take another target.</summary>
        private BtcDuelUnitRef SlotCap(BtcDuelUnitRef me, BtcDuelUnitRef t, BtcDuelUnitRef fa, BtcDuelUnitRef fb, bool allIn)
        {
            float dm = Dist2D(me.Tf.position, t.Tf.position);
            if (dm < 3.6f) return t;
            int on = 0;
            for (int i = 0; i < _my.Count; i++) if (_my[i] != me && _my[i].TargetId == t.Id) on++;
            bool full = on >= t.Slots + InReach(t);
            if (!full && t.Slots > 0) return t;
            BtcDuelUnitRef o = t == fa ? fb : fa;
            if (o != null && o != t && o.Slots > 0)
            {
                int on2 = 0;
                for (int i = 0; i < _my.Count; i++) if (_my[i] != me && _my[i].TargetId == o.Id) on2++;
                if (on2 < o.Slots + InReach(o)) return o;
            }
            float now = Time.time;
            if (_spillAt != now) { _spillAt = now; _spill = PickFocus(-1, fa != null ? fa.Id : -1, allIn); if (_spill != null && fb != null && _spill.Id == fb.Id) _spill = null; }
            if (_spill != null && _spill != t && _spill.Slots > 0)
            {
                if (t.Slots == 0 && now - _noslotLog > 0.5f) { _noslotLog = now; BtcDuelLog.Line("BOT", "noslot u" + t.Id + " -> u" + _spill.Id + " by u" + me.Id); }
                return _spill;
            }
            return t;
        }

        // ---------------- paths (bit 32) ----------------
        private int _planFrame = -1, _planN;
        private float ForbidX { get { return EnemySafeEdgeX - _dir * 1.5f; } }
        private float PressX { get { return Tac(2) ? EnemyMidEdgeX + _dir * 5f : EnemySafeEdgeX - _dir * 10f; } }

        /// <summary>Plan budget per think tick (A* on a 64x64 window); over budget -> straight line.</summary>
        private bool PlanBudget()
        {
            int fr = Time.frameCount;
            if (fr != _planFrame) { _planFrame = fr; _planN = 0; }
            return ++_planN <= 40;
        }

        private float PlanTo(BtcDuelUnitRef self, Vector3 from, Vector3 to, BtcDuelUnitRef target, out Vector3 wp, out float danger)
        {
            if (!PlanBudget()) { wp = to; danger = 0f; return -1f; }
            return BtcDuelPath.Plan(from, to, self, target, _my, _foe, _enemySafe, ForbidX, _dir, out wp, out danger);
        }

        /// <summary>Move along a path around bodies and away from enemy reach; the unit is re-steered every 0.4 s until it arrives (Steer).</summary>
        private void MoveSafe(BtcDuelUnitRef me, Vector3 dest, bool force = false)
        {
            if (!Tac(32)) { BtcDuel.Move(me, dest, force); return; }
            Vector3 wp; float dg;
            float len = PlanTo(me, me.Tf.position, dest, null, out wp, out dg);
            BtcDuel.Move(me, len < 0f ? dest : Floor3(wp.x, wp.z), force);
            me.LastDest = dest; me.PathDest = dest; me.PathAt = Time.time;
        }

        private void Steer(float now)
        {
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef u = _my[i];
                if (u.PathDest == Vector3.zero) continue;
                if (Dist2D(u.Tf.position, u.PathDest) < 1.5f) { u.PathDest = Vector3.zero; continue; }
                if (now - u.PathAt < 0.4f || now - u.StrikeAt < BtcDuel.StrikeWindow) continue;
                MoveSafe(u, u.PathDest, true);
            }
        }

        /// <summary>Dodge direction score: metres inside enemy reach on the way cost a lot, the enemy side of the arena (leash line) is a bad place to end up.</summary>
        private float RouteScore(BtcDuelUnitRef me, Vector3 from, Vector3 to)
        {
            Vector3 wp; float dg;
            float len = PlanTo(me, from, to, null, out wp, out dg);
            float s = len < 0f ? -25f : -3f * dg - 0.3f * Mathf.Max(0f, len - Dist2D(from, to));
            if (BeyondLeash(to.x)) s -= 15f;
            s += (-_dir * (to.x - from.x)) * 0.5f;
            return s;
        }

        /// <summary>Path length / danger from our group to every enemy (target selection: a unit behind its friends is far away even when it is close).</summary>
        private void FoePaths()
        {
            Vector3 gc = FreeCentre();
            for (int j = 0; j < _foe.Count; j++)
            {
                BtcDuelUnitRef e = _foe[j];
                if (gc == Vector3.zero || e.Zone == _enemySafe || Dist2D(gc, e.Tf.position) > 45f) { e.PathLen = -1f; e.PathDanger = 0f; continue; }
                Vector3 wp; float dg;
                e.PathLen = PlanTo(null, gc, e.Tf.position, e, out wp, out dg);
                e.PathDanger = e.PathLen >= 0f ? Mathf.Max(0f, dg - Reach0) : 0f;
            }
        }
        private const float Reach0 = 3.4f;    // the last metres to the target are always inside its friends' reach when it has any: count only the extra

        /// <summary>Units far past the mid with nothing to fight (camping under the enemy safe zone while the enemy harasses our wounded in mid) come back to the group.</summary>
        private float _retLog;
        private void ReturnIdle(float now, bool allIn)
        {
            for (int i = 0; i < _my.Count; i++)
            {
                BtcDuelUnitRef u = _my[i];
                if (u.Anchor || u.Rest || u.Retreating || u.HoldUntil > now || !BeyondLeash(u.Tf.position.x)) continue;
                BtcDuelUnitRef t = u.TargetId >= 0 ? ByIdAll(u.TargetId) : null;
                bool hunting = t != null && !t.Dead && t.Zone != _enemySafe && !LeashBlocked(t) && Dist2D(u.Tf.position, t.Tf.position) < 8f;
                if (hunting) continue;
                bool foeNear = false;
                for (int j = 0; j < _foe.Count && !foeNear; j++) if (_foe[j].Zone != _enemySafe && Dist2D(_foe[j].Tf.position, u.Tf.position) < (allIn ? 10f : 6f)) foeNear = true;
                if (foeNear) continue;
                Vector3 gc = FreeCentre();
                Vector3 dest = gc != Vector3.zero && !BeyondLeash(gc.x) ? Floor3(gc.x, gc.z + ((u.Id % 5) - 2) * 1.5f) : Floor3(BtcDuelMap.MidX0 * 0.5f + BtcDuelMap.MidX1 * 0.5f, u.Tf.position.z);
                u.TargetId = -23; u.SkipAt = -1f; u.SlotIdx = -1; u.HoldUntil = now + 1.0f;
                MoveSafe(u, dest, true);
                if (now - _retLog > 0.5f) { _retLog = now; BtcDuelLog.Line("BOT", "return u" + u.Id + " hp=" + (int)u.Dr.CurrentHP + (allIn ? " allIn" : "")); }
            }
        }
    }
}
