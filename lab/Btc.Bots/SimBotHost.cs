using System;
using System.Collections.Generic;
using BetterThanCastle;
using Btc.Sim;
using UnityEngine;

namespace Btc.Bots
{
    /// <summary>Controller of one side. Kind codes follow the DEV command `duelplayerbot level kind tier` (BtcDuelMatch.StartFight).</summary>
    public sealed class SimSideSpec
    {
        public const int Idle = -1;        // nobody gives orders (units only do what the engine does on its own)
        public const int External = -2;    // orders come from SimBotHost.Order / an ISideController (RL policy, scripted test)
        public const int KindV1 = 0, KindV2 = 1, KindProd = 2, KindHunter = 3, KindSpread = 4, KindSweeper = 5, KindBaiter = 6, KindClump = 7, KindX = 8, KindLumina = 9, KindPresser = 10, KindHunter2 = 11;

        public int Kind = Idle;
        public int Level = 1;
        public int Tier = 4;               // 0 Easy, 1 Medium, 2 Hard, 3 Extreme, 4 Chosen
        // static bot configuration, swapped in before every call into this side's bot (league opponents with different masks)
        public int Feat = SimBotDefaults.Feat, Feat2 = SimBotDefaults.Feat2, SkipFrames = SimBotDefaults.SkipFrames;
        public float SkipDelayMs = SimBotDefaults.SkipDelayMs, V2SkipDelayMs = SimBotDefaults.V2SkipDelayMs, XSkipDelayMs = SimBotDefaults.XSkipDelayMs;
        public int XFeat = SimBotDefaults.XFeat;
        public bool LabForceSkip;
        public ISideController Controller; // Kind == External: ticked at the bot tick point (render frame, after settle)
        public Btc.Shared.MlpPolicy Policy;  // Kind == KindLumina: the LuminaZero policy for this side (the mod controller BtcDuelBotLuminaZero runs unchanged)

        /// <summary>What the real match puts on the bot side: new BtcDuelBot(m, 1, tier == 0 ? 0 : 1, tier, true).</summary>
        public static SimSideSpec Production(int tier) { return new SimSideSpec { Kind = KindProd, Level = tier == 0 ? 0 : 1, Tier = tier }; }
        /// <summary>Self-play opponent like `duelplayerbot level kind tier`.</summary>
        public static SimSideSpec AutoPlayer(int level, int kind, int tier) { return new SimSideSpec { Kind = kind, Level = level, Tier = tier }; }
        public static SimSideSpec Ext(ISideController c = null) { return new SimSideSpec { Kind = External, Controller = c }; }
        public SimSideSpec Clone() { return (SimSideSpec)MemberwiseClone(); }
    }

    public interface ISideController { void Tick(SimBotHost host, int side, float dt); }

    public sealed class SimBotSetup
    {
        public SimSideSpec Side0 = new SimSideSpec();                 // the "human" side (west)
        public SimSideSpec Side1 = SimSideSpec.Production(4);         // the bot side (east)
        public float TimeOrigin = 60f;     // Unity Time.time at fight start (the real game is never near 0; bots compare against -1/-100/0 sentinels)
        public int FrameOrigin = 3600;     // Time.frameCount at fight start
        public float LabDodgeMs;
        /// <summary>Diagnostics: log sink for hosts whose setup has none (this thread only).</summary>
        [ThreadStatic] public static Action<float, string, string> GlobalLogSink;
        public Action<float, string, string> LogSink;                 // match log lines (t, tag, msg); null = off (bots still build some strings)
        public bool LogBotOrders = true;
    }

    /// <summary>Production defaults of the bot statics, captured once from the mod sources (their [ThreadStatic] initialisers run on one thread only).</summary>
    public static class SimBotDefaults
    {
        public static readonly int Feat, Feat2, SkipFrames, XFeat;
        public static readonly float SkipDelayMs, V2SkipDelayMs, XSkipDelayMs;
        static SimBotDefaults()
        {
            // This class must be the first code touching the bot statics: their initialisers run in the type initialiser on the first touching
            // thread and only that thread sees them. Everything else in Btc.Bots reaches the bots through SimSideSpec/SimBotHost, which touch us first.
            Feat = BtcDuelBot.Feat; Feat2 = BtcDuelBot.Feat2; SkipFrames = BtcDuelBot.SkipFrames; SkipDelayMs = BtcDuelBot.SkipDelayMs;
            V2SkipDelayMs = BtcDuelBotV2.SkipDelayMs;
            XFeat = BtcDuelBotX.Feat; XSkipDelayMs = BtcDuelBotX.SkipDelayMs;
            if (Feat == 0 || Feat2 == 0 || XFeat == 0 || SkipDelayMs <= 0f || V2SkipDelayMs <= 0f || XSkipDelayMs <= 0f)
                throw new InvalidOperationException("bot statics were initialised on another thread before SimBotDefaults");
        }
    }

    /// <summary>
    /// Runs the mod's unchanged duel bots against a DuelSim. Reproduces what BtcDuelMatch (fight tick, rules tick, OnOrder/OnStrike/OnDamage/MarkDead)
    /// and DuelPatches (strike record, NextFrame skip timing) do around the bots. Unity frame order per Update(renderDt):
    /// fixed steps (DuelPatches NextFrame check, Sim.Step with listener events) then the render update (bot ticks, rules step).
    /// One host = one match; hosts may interleave on one thread (every public entry point activates its context) or run on separate threads.
    /// </summary>
    public sealed class SimBotHost : ISimListener
    {
        public readonly DuelSim Sim;
        public ISimListener Downstream;               // receives every sim event after the host handled it (RL env)
        public Action<int, SimOrder, float, float, int> OrderIssued;   // every order sent to the sim (bots and external), after bookkeeping
        public Action AfterStep;                       // after every Sim.Step (tests: stub-engine physics)

        private readonly SimContext _ctx = new SimContext();
        private readonly BtcDuelMatch _m = new BtcDuelMatch();
        private SimBotSetup _setup = new SimBotSetup();
        private readonly SimSideSpec[] _spec = new SimSideSpec[2];
        private readonly IDuelBot[] _bot = new IDuelBot[2];
        private BtcDuelUnitRef[] _u = new BtcDuelUnitRef[0];
        private GameObject[] _go = new GameObject[0];
        private int[] _orderGen = new int[0], _strikeGen = new int[0];
        private double _renderT, _fixedT;             // since fight start
        private int _fixedFrames, _curFrame;
        private float _rulesAcc;
        private bool _fight;
        private int _scale = 1;

        public float FightTime { get { return _m.FightTime; } }
        public bool Fighting { get { return _fight; } }
        public int BotErrors { get { return _ctx.Errors; } }
        public string LastBotError { get { return _ctx.LastError; } }
        public ulong OrderHash { get; private set; }
        public int OrderCount { get; private set; }
        public int BotOrderCount { get; private set; }
        public int RenderFrames { get; private set; }
        /// <summary>Profiling of bot Tick calls (Stopwatch ticks, calls).</summary>
        public bool Profile;
        public long ProfTicks, ProfCalls;

        static SimBotHost() { if (SimBotDefaults.Feat == 0) throw new InvalidOperationException("no bot defaults"); }

        public SimBotHost(DuelSim sim) { Sim = sim; }

        // ---------------- setup ----------------
        /// <summary>Resets the sim (rules, seed), mirrors its units and builds the controllers exactly like BtcDuelMatch.StartFight.</summary>
        public void Begin(SimRules rules, ulong seed, SimBotSetup setup)
        {
            Sim.Listener = this;
            Sim.Reset(rules, seed);
            BeginFromSim(seed, setup);
        }

        /// <summary>As Begin, for a sim that was already Reset (or filled by a test). Sets Sim.Listener to this host.</summary>
        public void BeginFromSim(ulong seed, SimBotSetup setup)
        {
            Sim.Listener = this;
            _setup = setup ?? new SimBotSetup();
            _spec[0] = _setup.Side0 ?? new SimSideSpec();
            _spec[1] = _setup.Side1 ?? new SimSideSpec();
            SimRules r = Sim.Rules ?? new SimRules();
            _scale = r.ArenaScale == 3 ? 3 : (r.ArenaScale < 1 ? 1 : r.ArenaScale);
            _renderT = _fixedT = 0.0; _fixedFrames = 0; _curFrame = 0; _rulesAcc = 0f; _fight = true;
            OrderHash = 14695981039346656037UL; OrderCount = 0; BotOrderCount = 0; RenderFrames = 0;

            SimContext c = _ctx;
            c.Host = this; c.Match = _m;
            c.Time = c.FixedTime = _setup.TimeOrigin; c.DeltaTime = 0f; c.FrameCount = _setup.FrameOrigin;
            c.Seed((uint)(seed ^ (seed >> 32)) ^ 0x9E3779B9u);
            c.ById.Clear(); c.GuardOrders[0] = c.GuardOrders[1] = false;
            c.SkipAnims = r.Skips; c.InfiniteHeal = r.InfiniteHeal; c.ArenaScale = _scale;
            c.Difficulty = (BtcDuelDifficulty)Math.Max(0, Math.Min(4, _spec[1].Tier));
            c.LabDodgeMs = _setup.LabDodgeMs;
            c.LogSink = _setup.LogSink ?? SimBotSetup.GlobalLogSink; c.BotOrders = _setup.LogBotOrders; c.LogT0 = (float)c.Time; c.Errors = 0; c.LastError = null;
            bool auto = _spec[0].Kind >= 0;
            c.AutoPlayerLevel = auto ? _spec[0].Level : -1; c.AutoPlayerKind = auto ? _spec[0].Kind : 0; c.AutoPlayerTier = auto ? _spec[0].Tier : 4;
            Activate();

            // units: same order and ids as BtcDuelMatch.SpawnSide (side 0 first, then side 1; sim index i = Id)
            int n = Sim.Count;
            _m.Units.Clear(); _m.AvgDmg[0] = _m.AvgDmg[1] = 32f; _m.FightTime = 0f; _m.Winner = -1;
            _m.Escaping = Sim.Escaping; _m.Ctrl = Sim.Ctrl;
            _u = new BtcDuelUnitRef[n]; _go = new GameObject[n]; _orderGen = new int[n]; _strikeGen = new int[n];
            int[] cnt = new int[2];
            for (int i = 0; i < n; i++) _go[i] = new GameObject("u" + i, i);
            float floor = BtcDuelMap.Floor;
            for (int i = 0; i < n; i++)
            {
                GameObject go = _go[i];
                Transform tf = new Transform { Sim = Sim, I = i, Y = floor, Go = go };
                go.Tf = tf;
                BtcDuelUnitRef u = new BtcDuelUnitRef();
                u.Id = i; u.Side = Sim.Side[i]; u.Kind = BtcDuelUnit.Halberdier; u.Go = go; u.Tf = tf;
                u.Labor = new Brix.Game.AI.Labor { Sim = Sim, I = i, Go = go, All = _go };
                u.Dr = new Brix.Game.AI.Damage.DamageReceiver { Sim = Sim, I = i, Go = go };
                u.MaxHp = Sim.MaxHp[i];
                Vector3 p = tf.position;
                u.Zone = BtcDuelMap.ZoneAt(p.x, p.z);
                u.PrevPos = p;
                u.Dead = Sim.Dead[i];
                go.Unit = u;
                _u[i] = u; _m.Units.Add(u); c.ById[go.GetInstanceID()] = u;
                cnt[u.Side & 1]++;
            }
            Array.Clear(c.PlayerCount, 0, 8); Array.Clear(c.BotCount, 0, 8);
            c.PlayerCount[(int)BtcDuelUnit.Halberdier] = cnt[0]; c.BotCount[(int)BtcDuelUnit.Halberdier] = cnt[1];
            BtcDuelLog.SetT0();

            // controllers: _bot (side 1) is built before _botP (side 0), as in StartFight
            _bot[1] = Make(1); _bot[0] = Make(0);
        }

        private IDuelBot Make(int side)
        {
            SimSideSpec s = _spec[side];
            if (s.Kind < 0) return null;
            ApplyCfg(s);
            switch (s.Kind)
            {
                case SimSideSpec.KindV1: return new BtcDuelBotV2(_m, side, s.Level, s.Tier, false);
                case SimSideSpec.KindV2: return new BtcDuelBotV2(_m, side, s.Level, s.Tier, true);
                case SimSideSpec.KindProd: return new BtcDuelBot(_m, side, s.Level, s.Tier, true);
                case SimSideSpec.KindX: return new BtcDuelBotX(_m, side, s.Level, s.Tier, true);
                case SimSideSpec.KindLumina: BtcDuelBotLuminaZero.Policy = s.Policy; return new BtcDuelBotLuminaZero(_m, side);
                default:
                    if ((s.Kind >= 3 && s.Kind <= 7) || s.Kind == SimSideSpec.KindPresser || s.Kind == SimSideSpec.KindHunter2) return new BtcDuelHunter(_m, side, s.Kind == 4, s.Kind == 5, s.Kind);
                    return null;
            }
        }

        private static void ApplyCfg(SimSideSpec s)
        {
            BtcDuelBot.Feat = s.Feat; BtcDuelBot.Feat2 = s.Feat2; BtcDuelBot.SkipFrames = s.SkipFrames; BtcDuelBot.SkipDelayMs = s.SkipDelayMs;
            BtcDuelBot.LabForceSkip = s.LabForceSkip; BtcDuelBot.LabFrames = null; BtcDuelBot.LabDelays = null;
            BtcDuelBotV2.SkipDelayMs = s.V2SkipDelayMs; BtcDuelBotV2.LabForceSkip = s.LabForceSkip; BtcDuelBotV2.LabDelays = null;
            BtcDuelBotX.Feat = s.XFeat; BtcDuelBotX.SkipDelayMs = s.XSkipDelayMs; BtcDuelBotX.LabForceSkip = s.LabForceSkip; BtcDuelBotX.LabDelays = null;
        }

        /// <summary>Makes this match the current one on this thread: shadow context + the mod's per-thread statics (arena geometry).</summary>
        public void Activate()
        {
            SimContext.Cur = _ctx;
            BtcDuelMap.Built = false;
            BtcDuelMap.SetScale(_scale);
            BtcDuelMap.X0 = 0; BtcDuelMap.Z0 = 0; BtcDuelMap.Top = 0;     // arena-local coordinates; Floor = 1 (y of every unit)
            ApplyCfg(_spec[1] ?? _spec[0] ?? new SimSideSpec());
        }

        public static void DeactivateThread() { SimContext.Cur = null; }

        // ---------------- loop ----------------
        /// <summary>One render frame of Sim.RenderDt.</summary>
        public void Frame() { Update(Sim.RenderDt); }

        /// <summary>One Unity frame: the fixed steps that fit into the new frame time, then the render update (bot ticks, rules step).</summary>
        public void Update(float renderDt)
        {
            Activate();
            SimContext c = _ctx;
            double origin = _setup.TimeOrigin;
            _renderT += renderDt;
            while (!Sim.Over && _fixedT + DuelSim.FixedDt <= _renderT + 1e-7)
            {
                _fixedT += DuelSim.FixedDt;
                c.FixedTime = c.Time = origin + _fixedT; c.DeltaTime = DuelSim.FixedDt;
                _curFrame = _fixedFrames;
                NextFrameCheck();
                Sim.Step();
                _fixedFrames++;
                if (AfterStep != null) AfterStep();
            }
            c.Time = origin + _renderT; c.DeltaTime = renderDt; c.FrameCount++;
            RenderFrames++;
            if (Sim.Over) _fight = false;
            if (_fight) TickFight(renderDt);
        }

        /// <summary>DuelPatches.DuelAttackInfoNextFramePatch: the skip is due K sim frames after the strike, only while the strike's AttackInfo (= the order it ran under) lives.</summary>
        private void NextFrameCheck()
        {
            float now = (float)_ctx.Time;
            for (int i = 0; i < _u.Length; i++)
            {
                BtcDuelUnitRef u = _u[i];
                if (u.SkipFrameWait <= 0 || _orderGen[i] != _strikeGen[i]) continue;
                if (i < Sim.Ord.Length && Sim.Ord[i] != SimOrder.Attack && Sim.Ord[i] != SimOrder.MoveAttack) continue;
                if (_curFrame - u.StrikeFrame >= u.SkipFrameWait) { u.SkipFrameWait = 0; u.SkipAt = now; }
            }
        }

        private void TickFight(float dt)
        {
            _m.FightTime += dt;
            _rulesAcc += dt;
            if (_ctx.LabDodgeMs > 0f) DodgeTick();
            if (_m.FightTime > Sim.Rules.SettleTime)
            {
                for (int s = 1; s >= 0; s--)          // _bot (side 1) then _botP (side 0)
                {
                    if (_bot[s] != null)
                    {
                        ApplyCfg(_spec[s]);
                        if (Profile) { long t0 = System.Diagnostics.Stopwatch.GetTimestamp(); _bot[s].Tick(dt); ProfTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; ProfCalls++; }
                        else _bot[s].Tick(dt);
                    }
                    else if (_spec[s].Kind == SimSideSpec.External && _spec[s].Controller != null) _spec[s].Controller.Tick(this, s, dt);
                }
            }
            if (_rulesAcc >= BtcDuelMatchConst.RulesStep) { RulesTick(_rulesAcc); _rulesAcc = 0f; }
        }

        private void DodgeTick()
        {
            float now = Time.time;
            for (int i = 0; i < _u.Length; i++)
            {
                BtcDuelUnitRef u = _u[i];
                if (u.DodgeAt < 0f || now < u.DodgeAt || u.Dead) continue;
                u.DodgeAt = -1f;
                Vector3 p = u.Tf.position;
                BtcDuel.Move(u, new Vector3(p.x + (u.Side == 0 ? -3f : 3f), BtcDuelMap.Floor, p.z));
            }
        }

        /// <summary>BtcDuelMatch.RulesTick without the win checks (the sim owns the rules): zone, smoothed velocity, stillness time.</summary>
        private void RulesTick(float dt)
        {
            float now = Time.time;
            for (int i = 0; i < _u.Length; i++)
            {
                BtcDuelUnitRef u = _u[i];
                if (u.Dead) continue;
                if (Sim.Dead[i]) { MarkDead(u); continue; }
                Vector3 pp = u.Tf.position;
                BtcDuelZone z = BtcDuelMap.ZoneAt(pp.x, pp.z);
                float ivl = Mathf.Max(0.02f, dt); u.Vel = Vector3.Lerp(u.Vel, (pp - u.PrevPos) / ivl, 0.5f); u.PrevPos = pp; if (u.Vel.sqrMagnitude > 0.25f) u.MovedAt = now;
                if (z != u.Zone) { if (_ctx.LogSink != null) BtcDuelLog.Line("ZONE", "u" + u.Id + " s" + u.Side + " " + u.Zone + "->" + z); u.Zone = z; }
            }
        }

        // ---------------- orders ----------------
        /// <summary>External control (RL policy / scripted side): same path as a bot order minus the bot-side bookkeeping (LastOrderAt, guard).</summary>
        public void Order(int unit, SimOrder kind, float x, float z, int target)
        {
            Activate();
            IssueOrder(unit, kind, x, z, target, false);
        }

        internal void IssueOrder(int unit, SimOrder kind, float x, float z, int target) { IssueOrder(unit, kind, x, z, target, true); }

        private void IssueOrder(int unit, SimOrder kind, float x, float z, int target, bool fromBot)
        {
            if (unit < 0 || unit >= _u.Length) return;
            _orderGen[unit]++;                                    // a new order = a new AttackInfo (kills a pending frame skip)
            BtcDuelUnitRef u = _u[unit];
            if (_fight)
            {
                // BtcDuelMatch.OnOrder (Labor.SendOrder prefix): what kind of order the owner gave
                bool tgt = kind == SimOrder.Attack && target >= 0 && target < _u.Length;
                u.OrdKind = tgt ? 2 : (kind == SimOrder.MoveAttack ? 1 : 3);
                u.OrdTargetId = tgt ? target : -1;
                u.OrdAt = Time.time;
                u.OrdHasDest = !tgt; u.OrdDest = new Vector3(x, BtcDuelMap.Floor, z);   // BtcDuelMatch.OnOrder: dest only for orders without a unit target (world = arena-local here)
            }
            ulong h = OrderHash;
            h = Mix(h, (uint)_ctx.FrameCount); h = Mix(h, (uint)unit); h = Mix(h, (uint)kind);
            h = Mix(h, BitConverter.SingleToUInt32Bits(x)); h = Mix(h, BitConverter.SingleToUInt32Bits(z)); h = Mix(h, (uint)target);
            OrderHash = h; OrderCount++; if (fromBot) BotOrderCount++;
            if (OrderIssued != null) OrderIssued(unit, kind, x, z, target);
            Sim.Order(unit, kind, x, z, target);
        }

        private static ulong Mix(ulong h, uint v) { for (int k = 0; k < 4; k++) { h ^= (v >> (k * 8)) & 0xFFu; h *= 1099511628211UL; } return h; }

        // ---------------- sim events (ISimListener) ----------------
        public void OnStrike(int unit, int target)
        {
            if (unit >= 0 && unit < _u.Length)
            {
                BtcDuelUnitRef u = _u[unit];
                // DuelAttackInfoStrikePatch.Postfix
                u.StrikeFrame = _curFrame; u.StrikeAI = u.Go; _strikeGen[unit] = _orderGen[unit];
                // BtcDuelMatch.OnStrike
                if (_fight)
                {
                    float now = Time.time;
                    GameObject tg = target >= 0 && target < _go.Length ? _go[target] : null;
                    BtcDuelUnitRef t = tg != null ? tg.Unit : null;
                    if (_ctx.LogSink != null) BtcDuelLog.Line("STRIKE", "u" + u.Id + ">" + (t != null ? "u" + t.Id : "?") + " since=" + (int)((now - u.StrikeAt) * 1000f));
                    u.StrikeAt = now; u.StrikeTarget = tg;
                    if (_ctx.LabDodgeMs > 0f && t != null && !t.Dead) t.DodgeAt = now + _ctx.LabDodgeMs * 0.001f;
                    if (_bot[1] != null) { ApplyCfg(_spec[1]); _bot[1].OnStrike(u); }
                    if (_bot[0] != null) { ApplyCfg(_spec[0]); _bot[0].OnStrike(u); }
                }
            }
            if (Downstream != null) Downstream.OnStrike(unit, target);
        }

        /// <summary>BtcDuelMatch.OnDamage (BricktronDamageReceiver.ApplyDamage postfix) minus logging and the safe-zone rule (sim).</summary>
        public void OnHit(int attacker, int victim, float dealt, float hpAfter)
        {
            if (_fight && victim >= 0 && victim < _u.Length && !_u[victim].Dead)
            {
                BtcDuelUnitRef v = _u[victim];
                BtcDuelUnitRef att = attacker >= 0 && attacker < _u.Length ? _u[attacker] : null;
                float now = Time.time;
                if (dealt > 0.5f) { v.LastHurtAt = now; v.HitT[v.HitN++ & 3] = now; }
                if (att != null && att.Side != v.Side)
                {
                    float gap = att.LastHitAt > 0f ? (now - att.LastHitAt) * 1000f : 0f;
                    att.LastHitAt = now;
                    if (dealt > 0.5f) _m.AvgDmg[att.Side] = _m.AvgDmg[att.Side] * 0.9f + dealt * 0.1f;
                    if (_ctx.LogSink != null) BtcDuelLog.Line("HIT", "u" + att.Id + ">u" + v.Id + " dmg=" + dealt.ToString("0.0") + " hp=" + hpAfter.ToString("0") + " gap=" + (int)gap);
                }
                if (hpAfter <= 0f) MarkDead(v);
            }
            if (Downstream != null) Downstream.OnHit(attacker, victim, dealt, hpAfter);
        }

        public void OnKill(int victim, int killer)
        {
            if (victim >= 0 && victim < _u.Length) MarkDead(_u[victim]);
            if (Downstream != null) Downstream.OnKill(victim, killer);
        }

        public void OnEnd(int winner, string reason)
        {
            _fight = false; _m.Winner = winner;
            if (_ctx.LogSink != null) BtcDuelLog.Line("END", "winner=" + winner + " reason=" + reason);
            if (Downstream != null) Downstream.OnEnd(winner, reason);
        }

        private void MarkDead(BtcDuelUnitRef v)
        {
            if (v.Dead) return;
            v.Dead = true;
            if (_ctx.LogSink != null) BtcDuelLog.Line("KILL", "u" + v.Id + " s" + v.Side);
        }

        // ---------------- read-only views for other controllers ----------------
        /// <summary>Bot-side bookkeeping of unit i (what the bots know about it): time of its current strike start, smoothed velocity, last order kind.</summary>
        public float UnitStrikeAt(int i) { return _u[i].StrikeAt; }
        public float UnitVelX(int i) { return _u[i].Vel.x; }
        public float UnitVelZ(int i) { return _u[i].Vel.z; }
        public int UnitOrdKind(int i) { return _u[i].OrdKind; }
        public bool UnitOrdHasDest(int i) { return _u[i].OrdHasDest; }
        public float UnitOrdX(int i) { return _u[i].OrdDest.x; }
        public float UnitOrdZ(int i) { return _u[i].OrdDest.z; }
        public int UnitOrdTargetId(int i) { return _u[i].OrdTargetId; }
        public float UnitOrdAt(int i) { return _u[i].OrdAt; }
        public float Now { get { return (float)_ctx.Time; } }
    }

    internal static class BtcDuelMatchConst { internal const float RulesStep = 0.1f; }
}
