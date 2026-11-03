using Brix.Game.AI;
using Brix.Game.AI.Damage;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>One duel unit: side 0 = human (west), side 1 = bot (east).</summary>
    internal sealed class BtcDuelUnitRef
    {
        public int Id;
        public int Side;
        public BtcDuelUnit Kind;
        public GameObject Go;
        public Labor Labor;
        public DamageReceiver Dr;
        public Transform Tf;
        public float MaxHp;
        public bool Dead;
        public BtcDuelZone Zone;
        public float LastHitAt = -100f;   // time of the last hit this unit dealt (for the interval / skip log)
        public int StrikeFrame, SkipFrameWait, SkipFrameK;   // simulation-frame skip timing (AttackInfo.CurrentFrame at the strike / frames to wait)
        public object StrikeAI;
        public float StrikeAt = -100f;     // time the current melee strike started (AttackInfo.Strike)
        public GameObject StrikeTarget;
        public float SkipAt = -1f;         // bot: time to fire the skip order for the running strike
        public Vector3 PrevPos, Vel;       // smoothed velocity (m/s), updated by the match rules tick
        public int ThreatCount;            // bot: enemies that currently target this unit
        public int FoeTargetId = -1;       // bot: whom this (enemy) unit is attacking
        public bool Pulling;               // bot: dragged behind the friendly line (bait)
        public float PullFree = -1f;       // time since which the unit is not threatened
        public int SlotIdx = -1, SlotFoe = -1; // body block slot (0..3) around SlotFoe
        public Vector3 SlotPos;
        public int SwapIn = -1;            // replacement unit id on its way to take this unit's slot
        public float IgnoreUntil = -1f;    // bot: do not chase this foe before this time
        public float HoldUntil = -1f;      // bot: safe-edge hold / evade hold (other logic leaves the unit alone)
        public bool Rest;                  // bot: wounded, waits behind the line until healed
        public bool Dodging;               // bot: runs from a chase until nobody targets it any more
        public float ChasedAt;             // bot: last time somebody targeted this unit while it was dodging
        public float[] HitT = { -100f, -100f, -100f, -100f };   // times of the last 4 hits taken
        public int HitN;
        public float LastHurtAt = -100f;   // last time this unit took damage (running only while it is really being hit)
        public float MovedAt;               // last time the unit moved faster than ~0.5 m/s (stillness detection)
        public float DodgeAt = -1f;        // lab: time to step away from the incoming strike
        public float LastOrderAt, NextOrderAt;  // bot: order throttling / idle detection
        public Vector3 LastDest;
        public bool Defending;             // bot: assigned to kill a chaser of a withdrawn unit this tick
        public int GroupOf = -1, GroupStamp, SlotCell;
        public bool Retreating;            // bot: wounded unit on its way to / waiting in the safe zone
        public int TargetId = -1;         // last target the bot ordered this unit to attack
        public int OrdKind;                // last order given to this unit by its owner: 0 none, 1 sweep (MoveAttack on the ground), 2 attack a unit, 3 move
        public int OrdTargetId = -1;       // target of that order (OrdKind 2)
        public float OrdAt = -100f;
        public Vector3 OrdDest;            // destination of that order (world), valid when OrdHasDest (Move / MoveAttack on the ground)
        public bool OrdHasDest;
        public int FocusThreat;            // bot: enemies whose ORDER (GoAttack) targets this unit: a real focus, not attack-move auto targeting
        public bool Anchor;                // bot: holds the mid for space control, leaves it only to heal
        public int OddsK, OddsF;           // bot: enemies / own units within 9 m (local numbers)
        public float ChaseD, ChaseAt = -1f; // bot (on foes): distance to the nearest attacker at the start of the chase window
        public int Slots = 8;              // bot (on foes): free strike spots around this unit
        public float SlotD;                // bot (on foes): distance from our group to the nearest free spot
        public Vector3 PathDest;           // bot: final destination of a path-steered move (zero = none)
        public float PathAt = -100f;       // bot: last replan of that move
        public float PathLen = -1f, PathDanger; // bot (on foes): path length from our group (around bodies) and metres of it inside enemy reach
    }

    internal interface IDuelBot { void Tick(float dt); void OnStrike(BtcDuelUnitRef u); }
}
