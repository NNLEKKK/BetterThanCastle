using Btc.Bots;
using Btc.Shared;
using Btc.Sim;

namespace Btc.RL.Duel;

/// <summary>
/// The real RL environment of the micro-duel: N independent matches, each a DuelSim driven by a SimBotHost.
/// Side 0 = learner (External; or a scripted bot for `match`), side 1 = scripted bot inside the host or External driven by the
/// trainer's IOpponent (self-play / frozen policy). Env e uses opponent Opps[e % Opps.Length] (same rule as PpoTrainer).
///
/// Timing mirrors BtcDuelBotLuminaZero (the in-game controller): after the settle time the controller tick accumulates render dt and
/// decides when the sum reaches DecisionSec (then resets to 0). External sides skip time-based: on their strike SkipAt = now + SkipDelay;
/// at the controller tick (render frame) MoveAttack(own position) + Attack(strike target) unless the target is dead / in its safe zone.
/// The DuelView is filled exactly like BtcDuelBotLuminaZero.Fill (host smoothed Vel, fight-time clock, TargetOf = strike target within 1.3 s
/// else the Attack order target).
///
/// Rewards (side 0, per agent): terminal +1 / -1 / 0 (draw = sim time limit) + Shaping * (gamma*Phi' - Phi) (Phi' = 0 at the end)
/// + Indiv * (damage dealt - taken by this unit during the step) / MaxHp.
/// Phi = 0.5*(hp share own - hp share enemy) + 0.25*(alive own - alive enemy)/N + 0.1*(ctrl own - ctrl enemy)/SpaceControlSec.
/// </summary>
public sealed class SimDuelEnv : IEnvBatch, IOpponentSwitch
{
    public const int MaxUnitsPerSide = DuelObs.MaxPerSide;

    /// <summary>Episode distribution. May be replaced between rollouts (curriculum stage change); read at every Reset.</summary>
    public volatile DuelCurriculum Cur;
    public readonly DuelOppDef[] Opps;
    public readonly DuelOppDef Side0;      // null = learner (External)
    readonly Inst[] _e;
    static readonly int[] _heads = { DuelActions.Count };

    public SimDuelEnv(int numEnvs, DuelCurriculum cur, DuelOppDef[] opps, DuelOppDef side0 = null)
    {
        if (opps == null || opps.Length == 0) throw new ArgumentException("need at least one opponent");
        Cur = cur; Opps = opps; Side0 = side0 != null && side0.External ? null : side0;
        _e = new Inst[numEnvs];
        for (int i = 0; i < numEnvs; i++) _e[i] = new Inst(this, opps[i % opps.Length]);
    }

    public int NumEnvs => _e.Length;
    public int MaxAgents => MaxUnitsPerSide;
    public int ObsSize => DuelObs.Size;
    public int CentralSize => DuelObs.CentralSize;
    public int[] HeadSizes => _heads;
    public bool HasOpponent => true;
    public string Schema => DuelObs.Schema;

    public void Reset(int env, ulong seed) => _e[env].Reset(seed);
    /// <summary>Opponent of env for its next Reset (index into Opps).</summary>
    public void SetOpponent(int env, int opp) => _e[env].Opp = Opps[opp];
    public void Observe(int env, int side, float[] obs, int obsOff, byte[] mask, int maskOff, float[] central, int centralOff, bool[] active, int activeOff)
        => _e[env].Observe(side, obs, obsOff, mask, maskOff, central, centralOff, active, activeOff);
    public void Step(int env, int[] actions0, int[] actions1, float[] rewards0, int rewOff, out bool done, out int outcome)
        => _e[env].Step(actions0, actions1, rewards0, rewOff, out done, out outcome);

    /// <summary>Read-only access for tools (view / eval statistics). Not for the hot path.</summary>
    public SimBotHost Host(int env) => _e[env].Host;
    public DuelView View(int env) { var i = _e[env]; i.Fill(); return i.V; }
    public EpisodeInfo Info(int env) => _e[env].Info();

    /// <summary>Role statistics of the learner (side 0) since the last TakeRoleStats: [role 0 attacker / 1 defender, k]; k 0 episodes, 1 wins,
    /// 2.. deciding reason (2 kill and escape, 3 space control, 4 eliminated, 5 safe zone, 6 time limit, 7 other). Thread-safe.</summary>
    public static readonly string[] RoleCats = { "ke", "sc", "dead", "safe", "time", "other" };
    readonly long[] _roleStats = new long[2 * 8];
    public long[] TakeRoleStats() { var r = new long[_roleStats.Length]; for (int i = 0; i < r.Length; i++) r[i] = System.Threading.Interlocked.Exchange(ref _roleStats[i], 0); return r; }

    public struct EpisodeInfo
    {
        public int Units0, Units1, Arena, Decisions, Orders, Kills0, Kills1, Winner;
        public bool Skips, Heal; public float Fps, FightTime; public string Reason; public string Opp;
    }

    // =====================================================================================
    sealed class Inst : ISimListener
    {
        readonly SimDuelEnv _o;
        public DuelOppDef Opp;
        DuelCurriculum _cur;    // the (stage) distribution this episode was drawn from
        readonly float[] _ordPen = new float[MaxUnitsPerSide];
        readonly bool[] _diedNow = new bool[DuelView.MaxUnits];
        public readonly DuelSim Sim = new DuelSim();
        public readonly SimBotHost Host;
        public readonly DuelView V = new DuelView();
        readonly SkipCtl[] _ctl = new SkipCtl[2];
        readonly bool[] _ext = new bool[2];
        readonly Rng _rng = new Rng(1);

        int _n0, _n1, _arena, _decisions;
        bool _skips, _heal; float _fps;
        float _acc; bool _due;
        bool _viewValid; readonly bool[] _slotsValid = new bool[2];
        readonly int[][] _slots = { new int[MaxUnitsPerSide * DuelObs.SlotCount], new int[MaxUnitsPerSide * DuelObs.SlotCount] };
        readonly float[] _skipAt = new float[DuelView.MaxUnits], _hurtAt = new float[DuelView.MaxUnits];
        readonly int[] _strikeTgt = new int[DuelView.MaxUnits];
        readonly float[] _dealt = new float[DuelView.MaxUnits], _taken = new float[DuelView.MaxUnits];
        readonly int[] _idx = new int[DuelView.MaxUnits]; readonly float[] _d = new float[DuelView.MaxUnits];
        readonly float[] _scratchObs = new float[DuelObs.Size];
        bool _done; int _outcome;
        float _lastProg, _bestGap, _ftPrev;     // passive penalty of a learner-attacker: last progress time, closest gap so far, previous fight time

        public Inst(SimDuelEnv o, DuelOppDef opp)
        {
            _o = o; Opp = opp; Host = new SimBotHost(Sim);
            _ctl[0] = new SkipCtl(this); _ctl[1] = new SkipCtl(this);
        }

        public void Reset(ulong seed)
        {
            var c = _o.Cur;
            _rng.State = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
            // replay of earlier curriculum stages (units/arena only; rules knobs stay those of the current stage)
            var ep = c.Earlier != null && c.Earlier.Length > 0 && c.EarlierProb > 0f && _rng.NextFloat() < c.EarlierProb ? c.Earlier[_rng.Next(c.Earlier.Length)] : c;
            _cur = c;
            int n = DuelCurriculum.Pick(ep.Units, ep.UnitW, _rng.NextFloat());
            _arena = DuelCurriculum.Pick(ep.Arena, ep.ArenaW, _rng.NextFloat());
            _skips = _rng.NextFloat() < c.SkipsProb;
            _heal = _rng.NextFloat() < c.HealProb;
            _fps = c.FpsMin + (c.FpsMax - c.FpsMin) * _rng.NextFloat();
            ulong simSeed = _rng.NextU64();

            ulong drSeed = _rng.NextU64();
            var p = c.DomainRand > 0f ? SimParams.Default().Jittered(drSeed, c.DomainRand) : SimParams.Default();
            p.RenderFps = _fps; p.RenderJitter = Math.Max(p.RenderJitter, c.RenderJitter);
            Sim.P = p;
            int n0 = n, n1 = n;
            if (c.HandicapProb > 0f && c.HandicapMax > 0 && _rng.NextFloat() < c.HandicapProb)
            {
                int big = Math.Min(MaxUnitsPerSide, n + c.HandicapMin + _rng.Next(Math.Max(1, c.HandicapMax - c.HandicapMin + 1)));
                if (c.HandicapOwnProb > 0f && _rng.NextFloat() < c.HandicapOwnProb) n0 = big; else n1 = big;      // default: in favour of side 1 (the opponent)
            }
            var rules = new SimRules { UnitsPerSide = n0, UnitsSide1 = n1, ArenaScale = _arena, Skips = _skips, InfiniteHeal = _heal, TimeLimit = c.TimeLimit };
            rules.Attacker = c.Roles == 1 ? (_rng.NextFloat() < c.AttackProb ? 0 : 1) : (c.Roles == 2 ? 0 : -1);
            SimSideSpec s0 = _o.Side0 != null ? _o.Side0.Bot() : SimSideSpec.Ext(_ctl[0]);
            SimSideSpec s1 = Opp.External ? SimSideSpec.Ext(_ctl[1]) : Opp.Bot();
            _ext[0] = s0.Kind == SimSideSpec.External; _ext[1] = s1.Kind == SimSideSpec.External;
            Host.Downstream = this;
            Host.Begin(rules, simSeed, new SimBotSetup { Side0 = s0, Side1 = s1, LogSink = null, LogBotOrders = false });
            if (Sim.Count == 0) throw new InvalidOperationException("DuelSim engine is a stub (Reset left Count = 0)");
            _n0 = Sim.N0; _n1 = Sim.N1;
            for (int i = 0; i < DuelView.MaxUnits; i++) { _skipAt[i] = -1f; _hurtAt[i] = -100f; _strikeTgt[i] = -1; _dealt[i] = _taken[i] = 0f; }
            _acc = 0f; _due = false; _decisions = 0; _done = false; _outcome = 0;
            _lastProg = 0f; _bestGap = float.MaxValue; _ftPrev = 0f;
            V.SetArena(_arena); V.Skips = _skips; V.InfiniteHeal = _heal; V.SpaceControlSec = rules.SpaceControlSec; V.Attacker = rules.Attacker;
            V.StartUnits[0] = _n0; V.StartUnits[1] = _n1;
            Advance();
        }

        /// <summary>Runs render frames until the next decision point (or the end of the match).</summary>
        void Advance()
        {
            _due = false;
            float settle = Sim.Rules.SettleTime;
            int guard = 0, maxFrames = (int)((_cur.TimeLimit + 30f) * 400f);
            while (!Sim.Over && guard++ < maxFrames)
            {
                float dt = Sim.RenderDt;
                Host.Update(dt);
                // BtcDuelBotLuminaZero.Tick: ticked by the match only after the settle time; _acc += dt; decide when >= DecisionStep, then _acc = 0
                if (Host.Fighting && Host.FightTime > settle)
                {
                    _acc += dt;
                    if (_acc >= _cur.DecisionSec) { _acc = 0f; _due = true; break; }
                }
                if (Host.FightTime > _cur.TimeLimit + 10f) break;   // safety net: the sim's own time limit should have fired
            }
            _viewValid = false; _slotsValid[0] = _slotsValid[1] = false;
            if (Sim.Over || !_due)
            {
                _done = true;
                _outcome = Sim.Winner == 0 ? 1 : (Sim.Winner == 1 ? -1 : 0);
                int att = Sim.Rules.Attacker;
                if (att >= 0)
                {
                    int r = att == 0 ? 0 : 1, ci = Array.IndexOf(RoleCats, MatchStats.Cat(Sim.Reason)); if (ci < 0) ci = 5;
                    var rs = _o._roleStats; System.Threading.Interlocked.Increment(ref rs[r * 8]);
                    if (_outcome > 0) System.Threading.Interlocked.Increment(ref rs[r * 8 + 1]);
                    System.Threading.Interlocked.Increment(ref rs[r * 8 + 2 + ci]);
                }
            }
        }

        // ---------------- view (BtcDuelBotLuminaZero.Fill) ----------------
        public void Fill()
        {
            if (_viewValid) return;
            _viewValid = true;
            var v = V; var s = Sim;
            float now = Host.Now, ft = Host.FightTime, t0 = now - ft;
            v.Time = ft;
            // arena geometry (x3 arena = 300 x 60): without it DuelView kept the 100 x 20 defaults, so on x3 Run targets were clipped
            // to x <= 99.5 / z <= 19.5 and zones/obs used the small-arena layout (the game controller sets it from BtcDuelMap)
            v.Len = s.Len; v.Wid = s.Wid; v.SafeLen = s.SafeLen; v.NeutralLen = s.NeutralLen; v.MidLen = s.MidLen;
            v.Escaping[0] = s.Escaping[0]; v.Escaping[1] = s.Escaping[1];
            v.Ctrl[0] = s.Ctrl[0]; v.Ctrl[1] = s.Ctrl[1];
            int n = Math.Min(s.Count, DuelView.MaxUnits), dead0 = 0, dead1 = 0;
            for (int i = 0; i < n; i++)
            {
                int side = s.Side[i];
                v.Side[i] = side;
                bool dead = s.Dead[i];
                v.Dead[i] = dead;
                if (dead) { if (side == 0) dead0++; else dead1++; v.Hp[i] = 0f; v.TargetOf[i] = -1; continue; }
                v.X[i] = s.X[i]; v.Z[i] = s.Z[i];
                v.VX[i] = Host.UnitVelX(i); v.VZ[i] = Host.UnitVelZ(i);
                float fx = s.FX[i], fz = s.FZ[i], fl = MathF.Sqrt(fx * fx + fz * fz);
                v.FX[i] = fl > 1e-4f ? fx / fl : 0f; v.FZ[i] = fl > 1e-4f ? fz / fl : 0f;
                v.Hp[i] = s.Hp[i]; v.MaxHp[i] = s.MaxHp[i];
                v.StrikeAt[i] = Host.UnitStrikeAt(i) - t0;
                v.HurtAt[i] = _hurtAt[i] - t0;
                int ok = Host.UnitOrdKind(i);
                v.OrdKind[i] = ok;
                v.OrdAt[i] = Host.UnitOrdAt(i) - t0;
                v.OrdHasDest[i] = Host.UnitOrdHasDest(i);
                v.OrdX[i] = Host.UnitOrdX(i); v.OrdZ[i] = Host.UnitOrdZ(i);
                int ot = ok == 2 ? Host.UnitOrdTargetId(i) : -1;
                v.OrdTarget[i] = ot >= 0 && ot < n && !s.Dead[ot] ? ot : -1;
                v.Type[i] = DuelView.TypeHalberdier;
                v.TargetOf[i] = -1;
            }
            v.Count = n;
            v.Kills[0] = dead1; v.Kills[1] = dead0;
            for (int i = 0; i < n; i++)
            {
                if (v.Dead[i]) continue;
                int tg = -1;
                if (now - Host.UnitStrikeAt(i) < 1.3f && _strikeTgt[i] >= 0) tg = _strikeTgt[i];
                else if (s.Ord[i] == SimOrder.Attack) { int t = s.OrdTarget[i]; if (t >= 0 && t < n && !s.Dead[t]) tg = t; }
                if (tg >= 0 && !v.Dead[tg]) v.TargetOf[i] = tg;
            }
        }

        int Base(int side) => side == 0 ? 0 : _n0;
        int Cnt(int side) => side == 0 ? _n0 : _n1;

        public void Observe(int side, float[] obs, int oo, byte[] mask, int mo, float[] cen, int co, bool[] act, int ao)
        {
            Fill();
            int O = DuelObs.Size, L = DuelActions.Count, C = DuelObs.CentralSize, K = DuelObs.SlotCount;
            int b = Base(side), cnt = Cnt(side);
            var slots = _slots[side];
            int cenAt = -1;
            for (int a = 0; a < MaxUnitsPerSide; a++)
            {
                int u = b + a;
                bool live = !_done && a < cnt && !V.Dead[u];
                act[ao + a] = live;
                if (!live)
                {
                    Array.Clear(obs, oo + a * O, O); Array.Clear(mask, mo + a * L, L); mask[mo + a * L] = 1;
                    if (C > 0) Array.Clear(cen, co + a * C, C);
                    for (int k = 0; k < K; k++) slots[a * K + k] = -1;
                    continue;
                }
                DuelObs.Encode(V, u, obs, oo + a * O, _tmpSlots, _idx, _d);
                Array.Copy(_tmpSlots, 0, slots, a * K, K);
                DuelActions.Mask(V, u, _tmpSlots, mask, mo + a * L);
                if (C > 0)
                {
                    if (cenAt < 0) { DuelObs.EncodeCentral(V, side, cen, co + a * C); cenAt = co + a * C; }   // identical for the whole team: encode once
                    else Array.Copy(cen, cenAt, cen, co + a * C, C);
                }
            }
            _slotsValid[side] = true;
        }
        readonly int[] _tmpSlots = new int[DuelObs.SlotCount];

        void EnsureSlots(int side)
        {
            if (_slotsValid[side]) return;
            Fill();
            int K = DuelObs.SlotCount, b = Base(side), cnt = Cnt(side);
            for (int a = 0; a < cnt; a++)
            {
                int u = b + a;
                if (V.Dead[u]) continue;
                DuelObs.Encode(V, u, _scratchObs, 0, _tmpSlots, _idx, _d);
                Array.Copy(_tmpSlots, 0, _slots[side], a * K, K);
            }
            _slotsValid[side] = true;
        }

        void Apply(int side, int[] acts)
        {
            if (acts == null || !_ext[side]) return;
            EnsureSlots(side);
            int K = DuelObs.SlotCount, b = Base(side), cnt = Cnt(side);
            var slots = _slots[side];
            for (int a = 0; a < cnt; a++)
            {
                int u = b + a, act = acts[a];
                if (V.Dead[u] || act == DuelActions.Continue) continue;
                Array.Copy(slots, a * K, _tmpSlots, 0, K);
                DuelActions.Decode(V, u, act, _tmpSlots, out int kind, out float x, out float z, out int target);
                if (DuelActions.IsRepeat(V, u, kind, x, z, target)) continue;
                switch (kind)
                {
                    case DuelActions.OrdMove: Host.Order(u, SimOrder.Move, x, z, -1); break;
                    case DuelActions.OrdMoveAttack: Host.Order(u, SimOrder.MoveAttack, x, z, -1); break;
                    case DuelActions.OrdAttack: if (target >= 0) Host.Order(u, SimOrder.Attack, x, z, target); break;
                }
            }
        }

        /// <summary>Closest distance between a living side-0 unit and a living side-1 unit (MaxValue if a side is empty).</summary>
        float Gap()
        {
            var s = Sim; float best = float.MaxValue;
            for (int i = 0; i < s.Count; i++)
            {
                if (s.Dead[i] || s.Side[i] != 0) continue;
                for (int j = 0; j < s.Count; j++)
                {
                    if (s.Dead[j] || s.Side[j] != 1) continue;
                    float dx = s.X[i] - s.X[j], dz = s.Z[i] - s.Z[j], d = dx * dx + dz * dz;
                    if (d < best) best = d;
                }
            }
            return best == float.MaxValue ? best : MathF.Sqrt(best);
        }

        float Phi()
        {
            Fill();
            var v = V;
            float hp0 = 0f, hp1 = 0f; int a0 = 0, a1 = 0;
            for (int i = 0; i < v.Count; i++)
            {
                if (v.Dead[i]) continue;
                float h = v.MaxHp[i] > 0f ? v.Hp[i] / v.MaxHp[i] : 0f;
                if (v.Side[i] == 0) { hp0 += h; a0++; } else { hp1 += h; a1++; }
            }
            float n0 = Math.Max(1, _n0), n1 = Math.Max(1, _n1), N = Math.Max(n0, n1);
            return 0.5f * (hp0 / n0 - hp1 / n1) + 0.25f * (a0 - a1) / N + 0.1f * (v.Ctrl[0] - v.Ctrl[1]) / Math.Max(1f, v.SpaceControlSec);
        }

        public void Step(int[] a0, int[] a1, float[] rew, int ro, out bool done, out int outcome)
        {
            Array.Clear(rew, ro, MaxUnitsPerSide);
            if (_done) { done = true; outcome = _outcome; return; }
            var c = _o.Cur;
            Fill();              // pre-step state in V (also valid after Advance until the next Fill: used for the death diff)
            float phi = c.Shaping != 0f ? Phi() : 0f;
            // order cost: every non-Continue action of a live learner unit (makes "keep the current order" the default)
            Array.Clear(_ordPen);
            if (c.OrderCost > 0f && a0 != null && _ext[0])
                for (int a = 0; a < _n0 && a < MaxUnitsPerSide; a++)
                    if (!V.Dead[a] && a0[a] != DuelActions.Continue) _ordPen[a] = c.OrderCost;
            Apply(0, a0);
            Apply(1, a1);
            Array.Clear(_dealt); Array.Clear(_taken);
            Advance();
            _decisions++;
            // rewards v2: deaths this step (V still holds the pre-step Dead flags)
            int enemyDeaths = 0, ownDeaths = 0;
            Array.Clear(_diedNow);
            for (int i = 0; i < _n0 + _n1 && i < DuelView.MaxUnits; i++)
                if (!V.Dead[i] && Sim.Dead[i]) { if (i < _n0) { ownDeaths++; _diedNow[i] = true; } else enemyDeaths++; }
            float shaped = c.Shaping != 0f ? c.Shaping * ((_done ? 0f : c.Gamma * Phi()) - phi) : 0f;
            float term = _done ? (_outcome == 0 ? c.DrawReward : _outcome > 0 && MatchStats.Cat(Sim.Reason) == "safe" ? c.SafeWinReward : _outcome) : 0f;
            if (_done && _outcome > 0 && c.WinBonus != null) { int bi = Array.IndexOf(RoleCats, MatchStats.Cat(Sim.Reason)); if (bi >= 0 && bi < c.WinBonus.Length) term += c.WinBonus[bi]; }
            // roles: a learner-attacker pays PassivePen per fight second without progress (damage dealt, a kill, or the closest
            // own-enemy gap shrinking by 0.5 m) once PassiveGrace has passed; after a hit, approaching again counts as progress
            float passive = 0f;
            if (c.PassivePen > 0f && Sim.Rules.Attacker == 0)
            {
                float ft = Host.FightTime, gap = Gap();
                bool prog = enemyDeaths > 0;
                for (int i = 0; i < _n0 && !prog; i++) if (_dealt[i] > 0.5f) prog = true;
                if (prog) _bestGap = float.MaxValue;
                if (gap < _bestGap - 0.5f) { _bestGap = gap; prog = true; }
                if (prog) _lastProg = ft;
                float idleFrom = Math.Max(_lastProg + c.PassiveGrace, _ftPrev);
                if (ft > idleFrom) passive = c.PassivePen * (ft - idleFrom);
                _ftPrev = ft;
            }
            float inv = c.Indiv / 500f;
            float N0 = Math.Max(1, _n0), N1 = Math.Max(1, _n1), mh = Math.Max(1f, Sim.MaxHp[0]);
            float shared = 0f, ts = c.TeamShare, dealtTeam = 0f, takenTeam = 0f;
            if (c.InterScale != 0f)
            {
                for (int i = 0; i < _n0; i++) { dealtTeam += _dealt[i]; takenTeam += _taken[i]; }
                shared = c.KillReward * enemyDeaths / N1
                    + ts * (c.DmgDealt * dealtTeam / (N1 * mh) - c.DmgTaken * takenTeam / (N0 * mh) - c.DeathPenalty * ownDeaths / N0);
            }
            for (int a = 0; a < _n0 && a < MaxUnitsPerSide; a++)
            {
                float r = term + shaped + inv * (_dealt[a] - _taken[a]) - _ordPen[a] - passive;
                if (c.InterScale != 0f)
                {
                    // per-unit part: scaled by N0 so that its team mean equals the shared version
                    float own = c.DmgDealt * _dealt[a] / (N1 * mh) - c.DmgTaken * _taken[a] / (N0 * mh) - (_diedNow[a] ? c.DeathPenalty / N0 : 0f);
                    r += c.InterScale * (shared + (1f - ts) * N0 * own);
                }
                rew[ro + a] = r;
            }
            done = _done; outcome = _outcome;
        }

        public EpisodeInfo Info() => new EpisodeInfo
        {
            Units0 = _n0, Units1 = _n1, Arena = _arena, Skips = _skips, Heal = _heal, Fps = _fps, Decisions = _decisions,
            Orders = Host.OrderCount, Kills0 = Sim.Kills[0], Kills1 = Sim.Kills[1], Winner = Sim.Winner, Reason = Sim.Reason,
            FightTime = Host.FightTime, Opp = Opp.Name,
        };

        // ---------------- sim events (downstream of the host) ----------------
        public void OnStrike(int unit, int target)
        {
            if (unit < 0 || unit >= DuelView.MaxUnits) return;
            _strikeTgt[unit] = target;
            // BtcDuelBotLuminaZero.OnStrike: only for sides the env controls, only with rule "skips"
            if (Host.Fighting && _ext[Sim.Side[unit]] && Sim.Rules.Skips) _skipAt[unit] = Host.Now + _cur.SkipDelay;
        }

        public void OnHit(int attacker, int victim, float dealt, float hpAfter)
        {
            if (victim >= 0 && victim < DuelView.MaxUnits)
            {
                if (dealt > 0.5f) _hurtAt[victim] = Host.Now;
                _taken[victim] += dealt;
            }
            if (attacker >= 0 && attacker < DuelView.MaxUnits && victim >= 0 && Sim.Side[attacker] != Sim.Side[victim]) _dealt[attacker] += dealt;
        }

        public void OnKill(int victim, int killer) { }
        public void OnEnd(int winner, string reason) { }

        /// <summary>BtcDuelBotLuminaZero.SkipTick for an External side (runs at the bot tick point of the render frame).</summary>
        sealed class SkipCtl : ISideController
        {
            readonly Inst _i;
            public SkipCtl(Inst i) { _i = i; }
            public void Tick(SimBotHost host, int side, float dt)
            {
                var s = _i.Sim; float now = host.Now;
                int b = _i.Base(side), cnt = _i.Cnt(side);
                var enemySafe = side == 0 ? SimZone.SafeB : SimZone.SafeA;
                for (int u = b; u < b + cnt; u++)
                {
                    float at = _i._skipAt[u];
                    if (at < 0f || now < at) continue;
                    _i._skipAt[u] = -1f;
                    int t = _i._strikeTgt[u];
                    if (s.Dead[u] || t < 0 || s.Dead[t]) continue;
                    if (s.ZoneAt(s.X[t], s.Z[t]) == enemySafe) continue;
                    host.Order(u, SimOrder.MoveAttack, s.X[u], s.Z[u], -1);
                    host.Order(u, SimOrder.Attack, s.X[t], s.Z[t], t);
                }
            }
        }
    }
}
