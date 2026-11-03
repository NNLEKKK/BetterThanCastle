namespace Btc.Shared
{
    /// <summary>
    /// Engine-neutral snapshot of a duel, filled by the simulator (lab) and by the mod (real game) from their own state.
    /// DuelObs / DuelActions read ONLY this, so the policy sees identical features in both worlds.
    /// Coordinates are arena-local metres: x along the length from the west wall (side 0 = west), z across from the south wall.
    /// net35-safe: plain arrays, no LINQ, no Span. Capacity is fixed at construction; Count = units in use.
    /// </summary>
    public sealed class DuelView
    {
        public const int MaxUnits = 64;

        // arena / rules
        public float Len = 100f, Wid = 20f, SafeLen = 15f, NeutralLen = 20f, MidLen = 30f;
        public bool Skips = true, InfiniteHeal;
        public float SpaceControlSec = 10f;
        public float Time;                    // fight seconds
        /// <summary>Attacker side (-1 = no roles): the attacker loses when the time limit runs out (lab training rule; the mod always gives LuminaZero the attacker role).</summary>
        public int Attacker = -1;

        // match state
        public readonly bool[] Escaping = new bool[2];
        public readonly float[] Ctrl = new float[2];
        public readonly int[] Kills = new int[2];
        public readonly int[] StartUnits = new int[2];

        // units
        public int Count;
        public readonly int[] Side = new int[MaxUnits];
        public readonly bool[] Dead = new bool[MaxUnits];
        public readonly float[] X = new float[MaxUnits], Z = new float[MaxUnits];
        public readonly float[] VX = new float[MaxUnits], VZ = new float[MaxUnits];
        public readonly float[] FX = new float[MaxUnits], FZ = new float[MaxUnits];
        public readonly float[] Hp = new float[MaxUnits], MaxHp = new float[MaxUnits];
        /// <summary>Time of the unit's last strike start (Time units); very negative = never.</summary>
        public readonly float[] StrikeAt = new float[MaxUnits];
        /// <summary>Index of the unit it is currently attacking (strike target or attack-order target), -1 none.</summary>
        public readonly int[] TargetOf = new int[MaxUnits];
        /// <summary>Last order kind given by its owner: 0 none, 1 sweep (MoveAttack on ground), 2 attack a unit, 3 move.</summary>
        public readonly int[] OrdKind = new int[MaxUnits];
        /// <summary>Time of the last damage taken; very negative = never.</summary>
        public readonly float[] HurtAt = new float[MaxUnits];
        /// <summary>Destination of the last owner order (arena-local), valid when OrdHasDest (Move / MoveAttack on the ground).</summary>
        public readonly float[] OrdX = new float[MaxUnits], OrdZ = new float[MaxUnits];
        public readonly bool[] OrdHasDest = new bool[MaxUnits];
        /// <summary>Unit index the last owner order targets (OrdKind 2), -1 none.</summary>
        public readonly int[] OrdTarget = new int[MaxUnits];
        /// <summary>Time of the last owner order (Time units); very negative = never.</summary>
        public readonly float[] OrdAt = new float[MaxUnits];
        /// <summary>Unit type, BtcDuelConfig order: 0 Worker 1 BarrelWorker 2 Knight 3 Archer 4 Halberdier 5 Arbalist 6 Alchemist 7 Geomancer.</summary>
        public readonly int[] Type = new int[MaxUnits];
        public const int TypeCount = 8, TypeHalberdier = 4;

        public float MidX0 { get { return SafeLen + NeutralLen; } }
        public float MidX1 { get { return SafeLen + NeutralLen + MidLen; } }

        /// <summary>0 out, 1 SafeA, 2 NeutralA, 3 Mid, 4 NeutralB, 5 SafeB (same numbering as BtcDuelZone / SimZone).</summary>
        public int ZoneAt(float x, float z)
        {
            if (x < 0f || x >= Len || z < 0f || z >= Wid) return 0;
            if (x < SafeLen) return 1;
            if (x < SafeLen + NeutralLen) return 2;
            if (x < SafeLen + NeutralLen + MidLen) return 3;
            if (x < Len - SafeLen) return 4;
            return 5;
        }

        public static int OwnSafe(int side) { return side == 0 ? 1 : 5; }
        public static int EnemySafe(int side) { return side == 0 ? 5 : 1; }

        public void SetArena(int scale)
        {
            if (scale < 1) scale = 1;
            Len = 100f * scale; Wid = 20f * scale; SafeLen = 15f * scale; NeutralLen = 20f * scale; MidLen = 30f * scale;
        }
    }
}
