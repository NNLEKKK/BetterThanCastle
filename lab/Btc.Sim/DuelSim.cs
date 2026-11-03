namespace Btc.Sim;

/// <summary>Order kinds the duel uses (same three vanilla orders the bots and the human send) plus Stop/None.</summary>
public enum SimOrder : byte { None = 0, Move = 1, Attack = 2, MoveAttack = 3, Stop = 4 }

/// <summary>Zones along +X, numerically identical to the mod's BtcDuelZone.</summary>
public enum SimZone : byte { Out = 0, SafeA, NeutralA, Mid, NeutralB, SafeB }

/// <summary>Match rules / setup of one episode.</summary>
public sealed class SimRules
{
    public int UnitsPerSide = 5;
    public int ArenaScale = 1;          // 1 = 100x20, 3 = 300x60
    public bool Skips = true;           // false: strikes closer than 1.485 s to the previous one are blocked (DuelPatches cycle guard)
    public bool InfiniteHeal;           // +10 hp once per second of fight time
    public float SpaceControlSec = 10f;
    public float TimeLimit = 300f;      // sim-only safety net: draw after this many fight seconds
    public int Attacker = -1;           // lab roles: -1 none (time limit = draw), else the attacker side loses at the time limit
    public float SettleTime = 1.2f;     // controllers are not ticked before this fight time (landing stun in the real game)
    public int UnitsSide1 = -1;         // side 1 unit count; -1 = UnitsPerSide (asymmetric matches, log replay)
    public int OriginX, OriginZ;        // world voxel origin of the arena (CFG arenaX0/Z0): only the parity matters (banker's rounding of positions)
}

/// <summary>
/// Receives sim events (the mod's BtcDuelMatch.OnStrike / OnDamage / MarkDead equivalents). Implemented by the bot host
/// (Btc.Bots) and by the RL env. All calls happen inside Step on the stepping thread.
/// </summary>
public interface ISimListener
{
    void OnStrike(int unit, int target);
    void OnHit(int attacker, int victim, float dealt, float hpAfter);
    void OnKill(int victim, int killer);
    void OnEnd(int winner, string reason);
}

/// <summary>
/// Headless micro-duel. Struct-of-arrays state, fixed AI tick (FixedDt = 20 ms, same as the game's AttackInfo frame) and a
/// render-step clock (RenderDt, sampled per episode) for things the real game does per rendered frame.
/// Arena coordinates are arena-local: (0,0) = west-south corner, x along the length, z across.
/// Unit index i: side 0 = [0, n0), side 1 = [n0, n0+n1). Controllers issue orders through Order(...).
/// Implementation details live in docs/sim/*.md; this file is the public contract other projects code against.
/// </summary>
public sealed partial class DuelSim
{
    public const float FixedDt = 0.02f;

    // ---- setup / params ----
    public SimRules Rules = new SimRules();
    public SimParams P = SimParams.Default();
    public ISimListener Listener;

    // ---- clock ----
    public int Frame;              // fixed AI frames since reset (AttackInfo.CurrentFrame equivalent)
    public float Time;             // game time since fight start (Unity Time.time equivalent, advances by FixedDt)
    public float RenderDt = 1f / 60f;

    // ---- arena ----
    public float Len, Wid, SafeLen, NeutralLen, MidLen;

    // ---- units (struct of arrays; length = Count) ----
    public int Count;
    public int[] Side = new int[0];
    public float[] X = new float[0], Z = new float[0];        // position
    public float[] VX = new float[0], VZ = new float[0];      // actual velocity (m/s)
    public float[] FX = new float[0], FZ = new float[0];      // facing (unit vector)
    public float[] Hp = new float[0], MaxHp = new float[0];
    public bool[] Dead = new bool[0];

    // orders (current task)
    public SimOrder[] Ord = new SimOrder[0];
    public float[] OrdX = new float[0], OrdZ = new float[0];
    public int[] OrdTarget = new int[0];       // Attack: ordered target; MoveAttack: auto-acquired target or -1
    public float[] OrdAt = new float[0];

    // attack state (AttackInfo equivalent)
    public int[] AtkTarget = new int[0];       // unit currently being attacked (-1 none)
    public int[] NextAttackFrame = new int[0]; // per-AttackInfo cooldown gate (a new order creates a fresh AttackInfo -> 0)
    public int[] StrikeFrame = new int[0];     // frame the running strike started (-1 none)
    public int[] StrikeTarget = new int[0];
    public float[] StrikeAt = new float[0];    // Time of the last strike start
    public float[] LastStrikeAt = new float[0];// Time of the previous accepted strike (skip-off cycle guard)

    // ---- match state (BtcDuelMatch equivalent) ----
    public int Winner = -2;                    // -2 running, -1 draw, 0/1 side
    public string Reason;
    public readonly bool[] Escaping = new bool[2];
    public readonly float[] Ctrl = new float[2];
    public readonly int[] Kills = new int[2], Hits = new int[2], SkipHits = new int[2];
    public bool Over => Winner != -2;

    /// <summary>Order a unit (bots: BtcDuel.Move/Attack/MoveAttack). Coordinates arena-local. target = unit index for Attack.</summary>
    public partial void Order(int unit, SimOrder kind, float x, float z, int target);

    /// <summary>Reset to a new episode with the given rules and seed (deterministic).</summary>
    public partial void Reset(SimRules rules, ulong seed);

    /// <summary>Advance one fixed AI frame (20 ms): orders, movement, collision, attacks, damage, heal, rules.</summary>
    public partial void Step();

    public SimZone ZoneAt(float x, float z)
    {
        if (x < 0f || x >= Len || z < 0f || z >= Wid) return SimZone.Out;
        if (x < SafeLen) return SimZone.SafeA;
        if (x < SafeLen + NeutralLen) return SimZone.NeutralA;
        if (x < SafeLen + NeutralLen + MidLen) return SimZone.Mid;
        if (x < Len - SafeLen) return SimZone.NeutralB;
        return SimZone.SafeB;
    }

    /// <summary>Deterministic hash of the full state (determinism tests).</summary>
    public partial ulong StateHash();
}

/// <summary>
/// Calibrated mechanics parameters (docs/sim/mech-*.md; docs/sim/measured.md when present). Defaults = best current estimate.
/// Tags: [code] value read from ref/vanilla, [log] measured from duel logs, [mod] BTC duel mod, [guess] not measured yet (MEASURE), [sim] engine knob.
/// </summary>
public sealed class SimParams
{
    // ---- clock ----
    // Render clock (docs/sim/fidelity.md "render clock"): real duel logs (4 hidden instances) show FPS median 48 (p25 37, p95 67) and
    // frequent hitches: the SNAP-interval overshoot (mean 22 ms, 5.6% > 100 ms, 1.6% > 200 ms) fits ~1.2% of frames being 50-350 ms long.
    public float RenderFps = 48f;            // [log] base render fps (FPS lines median 48)
    public float RenderFpsSpread = 0.2f;     // [log] per-episode base fps uniform +-20% (per-match FPS means 34-67)
    public float RenderJitter = 0.3f;        // [log] +-fraction of uniform per-frame dt jitter
    public float HitchProb = 0.012f;         // [log] probability that a frame is a hitch (fit to the SNAP overshoot tail)
    public float HitchMinSec = 0.05f;        // [log] hitch frame length uniform [min, max]
    public float HitchMaxSec = 0.35f;        // [log]
    public float RenderMinDt = 0.0201f;      // [sim] frames never shorter than one fixed tick: keeps SimBotHost frames (it reads RenderDt) aligned with the sim's own

    // Strike animation on the render clock (Animator): the crank starts at the first render frame after the strike tick, damage lands at the first
    // render frame >= start + StrikeAnim*Sec (StartReacting), and an order cancels the strike when the animator progress at its last completed
    // An order cancels the strike when the animator progress (render time of the last animator update completed before the order was issued,
    // minus the crank start) is < CancelCommit*Sec: a hitch frame between the strike and the order (bot skip at ~220 ms) leaves the crank short,
    // and the skip cancels (real logs: ~9% of skip orders miss, 2.8% of strikes lose their hit to an order > 300 ms after the strike).
    // false: legacy fixed-tick model (StrikeToDamageFrames / CancelWindowFrames).
    public bool AnimatorStrike = true;       // [measured] lab: 20 fps adds ~2 ticks to strike->damage; real logs: skip orders at ~220 ms miss after hitches
    public float StrikeAnimFirstSec = 0.455f;  // [measured] first strike / after a new order: 23 ticks at lab fps (0.48 @60 fps, 0.53 @20 fps)
    public float StrikeAnimStanceSec = 0.335f; // [measured] in stance: 17 ticks at lab fps (0.36 @60, 0.38 @20)
    public float CancelCommitSec = 0.165f;     // [measured] lab Attack/MoveAttack re-orders at K <= 8 ticks cancel, K = 9 lands 8/9
    public float CancelCommitMoveSec = 0.185f; // [measured] lab Move order at K = 10 still cancels 3/9
    public bool DeferDamage = true;          // [code] Unity order: FixedUpdate, Update (bots, rules), Animator (StartReacting damage): a hit of frame f is seen in frame f+1
    public int OrderLatencyFrames = 0;       // [sim] extra fixed frames before an Order() reaches the unit (0 = next tick, like SendOrder)
    public int OrderLatencyJitter = 0;       // [sim] + uniform [0, n] frames
    public bool BusyOrders = true;           // [measured] lab-reorder: a running / just-ordered unit takes a new order only after BusyOrderSec, newer orders replace it
    public float BusyOrderSec = 0.35f;       // [measured] lab-reorder4: reversal 0.35-0.45 s after the order while running; best fit on a real LuminaZero match log
    public float BusyWindowSec = 0.5f;       // [measured range] a unit stopped from a Move stays busy this long (0.21 s busy, 0.8 s idle measured)

    // ---- grid ----
    public float CellCentreOffset = 0f;      // [log] arena-local voxel centre = i + offset; settled units sit on integer world coords
    public bool Diagonal8 = true;            // [measured] octile paths: straight first, then 45-degree diagonal (measured.md Behaviours 2)
    public bool SpawnSnap = true;            // [log] spawned units snap to the nearest voxel centre (banker's rounding) while landing

    // ---- locomotion (Running.cs / Walking.cs / Stepping.cs / TurnOnSpot.cs) ----
    public float RunSpeed = 5.95f;           // [measured] cruise 5.95 m/s (prefab RunSpeedMperS 7 is not the observed speed)
    public float WalkSpeed = 4.0f;           // [prefab] WalkSpeedMperS 4: path with a single node
    public float ArrivalDistance = 1.0f;     // [prefab] RunArrivalDistance: last metre before the final node at ArriveSpeed (fits 1/2/3/5.8/40 m move times)
    public float ArriveSpeed = 2.5f;         // [prefab] RunArriveSpeedMperS / WalkArriveSpeedMperS 2.5
    public float StrikeRunBonus = 3.0f;      // [code] RunSpeedStrikeBonus * clamp01(1 - d/StrikeRunBonusDist) on strike paths
    public float StrikeRunBonusDist = 4.0f;  // [code]
    public float CombatStepSpeed = 5.0f;     // [code] Stepping.CombatStepSpeed: strike match onto WantStrikeFrom
    public float TurnRateDeg = 750f;         // [measured] 180 deg reversal while running ~0.24 s
    public float TurnOnSpotDeg = 720f;       // [code] TurnOnSpot.TurningSpeed 2
    public float BlockedDelay = 0.1f;        // [code] RunBlockedDelay
    public float BlockedMoveFactor = 0.1f;   // [code] RunBlockedMoveFactor
    public int BlockedMaxCount = 20;         // [code] RunBlockedMaxCount (2 s) then the path fails
    public bool AllyPassThrough = true;      // [code] Builder.CanPassThrough: allies pass when the mover is not Fighting (duel units are prof=Builder)
    public int NavStartFrames = 5;           // [measured] first movement 0.12 s after the order (1 tick order delivery + 5)
    public int NavStartMovingFrames = 0;     // [measured] a running unit re-paths at once: reversal while running moves the new way from the next frame
    public int NavRetryFrames = 20;          // [code] PathFailed -> WaitFor.Ticks(20)
    public int NavMaxTries = 6;              // [guess] failures before a Move task gives up
    public int PathSearchLimit = 6000;       // [sim] A* expansion cap (fallback: straight staircase)
    public int RepathLimit = 64;             // [sim] local detour BFS cap
    public int RepathAhead = 3;              // [code] DestinationResolver.RepathMoveNext target = Current+3
    public bool PlanAroundUnits = true;      // [code] path search presence test blocks other units' voxels (aggressive: enemies ignored)
    public float RetargetMin = 1f;           // [code] Navigation.NeedsRetargeting: moved >= max(Min, ceil((dist-Offset)/Div))
    public float RetargetOffset = 2f;        // [code]
    public float RetargetDiv = 5f;           // [code]

    // ---- combat stance / strike (AttackInfo.cs, Striking.cs; timings are animator clips) ----
    public int StanceRaiseFrames = 1;        // [measured] the draw is inside the 23-tick first strike (20 m Attack -> first strike 2.9-3.1 s = run time)
    public int StanceLowerFrames = 10;       // [measured] chaser leaves stance 1.0-1.5 s after its strike start (ready at +0.88)
    public int PrepareFrames = 10;           // [code] IsPreparingToStrike grace
    public int StrikeToDamageFrames = 23;    // [measured] first strike of an approach / after a new order: damage +23 ticks (0.46 s)
    public int InStanceStrikeToDamageFrames = 17; // [measured] strike from stance without a new order since the previous strike: +17 ticks (0.34 s)
    public float StrikeAbortDist = 9.5f;     // [code] IsStrikeTargetValid distance
    public int ReactFrames = 27;             // [measured] ready again at +0.88 s (in stance) / +1.00 s (first) = damage + 27 ticks
    public int SkipRestrikeFrames = 18;      // [measured] order at K>=9 during a strike: next strike 18 ticks after the order (fast mode)
    public int SkipRestrikeSlowFrames = 35;  // [measured] ... or 34-35 ticks (slow mode)
    public float SkipSlowProb = 0.5f;        // [measured] about half of the re-orders hit the slow mode
    public int CancelWindowFrames = 9;       // [measured] order K <= 8 ticks after the strike start cancels the hit, K >= 9 lands
    public int CancelRecoverFrames = 17;     // [measured] after a cancelling order the next strike starts 17 ticks after the order (no cooldown)
    public int CooldownFrames = 75;          // [code+log] Profession.AttackCooldown, 1.5 s cycle
    public int CooldownRandFrames = 0;       // [code] AttackCooldownRandomness (Random.Range(-r, r))
    public float SkipGuardSec = 1.485f;      // [mod] DuelPatches: skips off -> Strike blocked if Time.time - StrikeAt < 1.5 - 0.015 (fixed time)
    public bool StrikeBlockedByUnits = false;// [guess] units between attacker and target block diagonal / 2-cell strikes

    // ---- targeting (AttackInfo.cs) ----
    public int TargetRefreshFrames = 5;      // [code] NextMeleeTargetFrame period
    public int NoActionFrames = 11;          // [code] HasCombatAction stays true max(5,10)+1 frames without target (also at order start)
    public int AttackSearchRadius = 3;       // [code] GoAttack*: radius 3, limit 30
    public int AttackSearchLimit = 30;       // [code]
    public int MoveAttackSearchRadius = 10;  // [code] MoveAttack: 10 / 200
    public int MoveAttackSearchLimit = 200;  // [code]
    public int HoldSearchRadius = 2;         // [code] CanMove=false
    public bool AttackOnSight = true;        // [guess] bricktron CombatPriority >= AttackOnSight: first enemy in BFS order wins at once
    public int WaitAfterArriveFrames = 50;   // [code] RunTowardDestination -> WaitForTicks(50) -> MoveAttack task ends

    // ---- instinct (LaborInstinct.cs) ----
    public bool InstinctEnabled = true;      // [log] idle units auto-engage (ORD HoldPositionAndAttackWithPriority / GoAttackWithPriority)
    public int InstinctEveryFrames = 10;     // [code] FightableEnemy every 10 ticks
    public float InstinctAggroRadius = 13f;  // [measured] idle reaction at 10.5-13.5 m, none at 15-16 m (prefab BaseAggroRange 15)
    public float InstinctHoldDist = 3.0f;    // [measured] idle unit with an enemy <= 3 m (incl. (3,0)) holds and attacks in place
    public bool InstinctChase = true;        // [log] enemy in aggro range but not in reach -> GoAttackWithPriority (chase)
    public bool InstinctPreemptsOrders = false; // [guess] instinct only for idle units (no running user order)
    public bool InstinctVisibleAsAttack = true; // [code] GoAttackWithPriority / HoldPositionAndAttackWithPriority carry the enemy as Task.Target: Ord = Attack, OrdTarget = enemy for observers

    // ---- damage / hp (BricktronDamageReceiver.cs) ----
    public float Damage = 32f;               // [log] 3062 hits of 32
    public float MaxHp = 500f;               // [log] UNIT maxhp=500
    public float RegenPerSec = 4f;           // [prefab+measured] +4 HP per 1 s tick, also while running
    public float HurtDelay = 12f;            // [prefab+measured] delayBeforeRegenPops 12 s
    public float HealPerStep = 10f;          // [mod] BtcDuelMatch.HealPerStep (InfiniteHeal)
    public float HealStep = 1f;              // [mod] BtcDuelMatch.HealStep

    public static SimParams Default() => new SimParams();

    /// <summary>Lab conditions of docs/sim/measured.md micro measurements: steady high fps, no hitches (SimSelfTest timing checks).</summary>
    public static SimParams Lab(float fps = 90f)
    {
        var p = new SimParams();
        p.RenderFps = fps; p.RenderFpsSpread = 0f; p.RenderJitter = 0f; p.HitchProb = 0f; p.RenderMinDt = 0.001f;
        return p;
    }
    public SimParams Clone() => (SimParams)MemberwiseClone();

    /// <summary>Domain randomisation: copy with fps / latency / timing jitter drawn from seed. scale 0 = identical copy, 1 = default spread.</summary>
    public SimParams Jittered(ulong seed, float scale = 1f)
    {
        var p = Clone();
        ulong s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        float U() { s ^= s << 13; s ^= s >> 7; s ^= s << 17; return (s >> 40) * (1f / 16777216f); }
        p.RenderFps = Math.Max(20f, RenderFps * (1f + scale * (U() * 1.5f - 0.5f)));   // 60 -> 30..120
        p.RenderJitter = RenderJitter + scale * 0.3f * U();
        p.OrderLatencyFrames = OrderLatencyFrames + (int)(scale * 3f * U());
        p.OrderLatencyJitter = OrderLatencyJitter + (int)(scale * 2f * U());
        p.NavStartFrames = NavStartFrames + (int)(scale * 3f * U());
        p.StanceRaiseFrames = Math.Max(1, StanceRaiseFrames + (int)(scale * U() * 3f));
        p.StrikeToDamageFrames = StrikeToDamageFrames + (int)(scale * (U() * 2.99f - 1f));
        p.InStanceStrikeToDamageFrames = InStanceStrikeToDamageFrames + (int)(scale * (U() * 2.99f - 1f));
        p.RunSpeed = RunSpeed * (1f + scale * 0.03f * (U() * 2f - 1f));
        return p;
    }
}
