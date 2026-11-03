namespace Btc.Shared
{
    /// <summary>
    /// Per-unit discrete action space of the duel policy (one categorical head, Count actions), shared by lab and mod.
    /// Decode turns an action into one of the three vanilla orders (or nothing); the caller sends it
    /// (sim: DuelSim.Order / SimBotHost.Order, game: BtcDuel.Move/Attack/MoveAttack). All coordinates arena-local, un-mirrored.
    ///   0          continue (no new order: keeps the running task, strikes and skips untouched)
    ///   1          stop (Move to own position)
    ///   2..17      run 2 m in direction k*22.5 deg (mirrored: 0 deg = towards the enemy)
    ///   18..33     run 5 m
    ///   34..49     run 12 m
    ///   50..57     attack enemy slot k (DuelObs nearest-first order)
    ///   58         attack the team focus target (enemy most targeted by our units, DuelObs slotOut[FocusSlot])
    ///   59         attack the enemy that is attacking me (slotOut[AttackerSlot]; masked when nobody attacks)
    ///   60..67     attack-move 8 m in direction k*45 deg
    ///   68         retreat: run to the own safe zone (3 m inside its edge, same z)
    /// </summary>
    public static class DuelActions
    {
        public const int Count = 69;
        public const int Continue = 0, Stop = 1, Run2 = 2, Run5 = 18, Run12 = 34, AttackSlot = 50, Focus = 58, Attacker = 59, Sweep = 60, Retreat = 68;
        public const int OrdNone = 0, OrdMove = 1, OrdAttack = 2, OrdMoveAttack = 3;

        /// <summary>No new order inside this many seconds after the unit's own strike start (an order there cancels the strike).</summary>
        public const float CancelGuard = 0.2f;

        static readonly float[] Cos16 = new float[16], Sin16 = new float[16];
        static DuelActions()
        {
            for (int k = 0; k < 16; k++)
            {
                double a = k * System.Math.PI / 8.0;
                Cos16[k] = (float)System.Math.Cos(a); Sin16[k] = (float)System.Math.Sin(a);
            }
        }

        /// <summary>Target unit index of an attack action (slots from DuelObs.Encode, length >= DuelObs.SlotCount), -1 when a is not an attack or the slot is empty.</summary>
        public static int AttackTarget(int a, int[] slots)
        {
            if (a >= AttackSlot && a < Focus) return slots[a - AttackSlot];
            if (a == Focus) return slots[DuelObs.FocusSlot];
            if (a == Attacker) return slots[DuelObs.AttackerSlot];
            return -1;
        }

        /// <summary>Decode action a of unit u. kind: OrdNone/OrdMove/OrdAttack/OrdMoveAttack; (x,z) destination or target position, target = unit index.</summary>
        public static void Decode(DuelView v, int u, int a, int[] slots, out int kind, out float x, out float z, out int target)
        {
            kind = OrdNone; x = v.X[u]; z = v.Z[u]; target = -1;
            int side = v.Side[u];
            float sgn = side == 0 ? 1f : -1f;
            if (a <= Continue || a >= Count) return;
            if (a == Stop) { kind = OrdMove; return; }
            if (a < Run5) { Dir(v, u, a - Run2, 2f, sgn, out x, out z); kind = OrdMove; return; }
            if (a < Run12) { Dir(v, u, a - Run5, 5f, sgn, out x, out z); kind = OrdMove; return; }
            if (a < AttackSlot) { Dir(v, u, a - Run12, 12f, sgn, out x, out z); kind = OrdMove; return; }
            if (a < Sweep)
            {
                int t = AttackTarget(a, slots);
                if (t < 0) return;
                kind = OrdAttack; target = t; x = v.X[t]; z = v.Z[t]; return;
            }
            if (a < Retreat) { Dir(v, u, (a - Sweep) * 2, 8f, sgn, out x, out z); kind = OrdMoveAttack; return; }
            // retreat
            x = side == 0 ? v.SafeLen - 3f : v.Len - v.SafeLen + 3f;
            kind = OrdMove;
        }

        /// <summary>Two Move/MoveAttack destinations closer than this are the same order.</summary>
        public const float RepeatDist = 1.5f;

        /// <summary>True when the decoded order repeats the unit's last owner order (same Attack target, or same Move/MoveAttack
        /// destination within RepeatDist). Re-sending an order restarts the unit's task (an Attack re-inits the AI search), so a
        /// policy that re-picks its order every decision would never get going: both controllers (sim env, game) skip repeats.</summary>
        public static bool IsRepeat(DuelView v, int u, int kind, float x, float z, int target)
        {
            int ok = v.OrdKind[u];
            if (kind == OrdAttack) return ok == 2 && target >= 0 && v.OrdTarget[u] == target;
            if (kind != OrdMove && kind != OrdMoveAttack) return false;
            if (ok != (kind == OrdMoveAttack ? 1 : 3) || !v.OrdHasDest[u]) return false;
            float dx = v.OrdX[u] - x, dz = v.OrdZ[u] - z;
            return dx * dx + dz * dz < RepeatDist * RepeatDist;
        }

        static void Dir(DuelView v, int u, int k, float dist, float sgn, out float x, out float z)
        {
            x = v.X[u] + sgn * Cos16[k] * dist;
            z = v.Z[u] + Sin16[k] * dist;
            if (x < 0.5f) x = 0.5f; else if (x > v.Len - 0.5f) x = v.Len - 0.5f;
            if (z < 0.5f) z = 0.5f; else if (z > v.Wid - 0.5f) z = v.Wid - 0.5f;
        }

        /// <summary>
        /// Legal-action mask for unit u (mask[off+a] = 1 allowed). Forbids only: orders inside the strike cancel guard,
        /// destinations inside the enemy safe zone (instant loss), attacking a unit that stands in its own safe zone (instant loss),
        /// empty slots (also focus / attacker without a candidate). Everything else is the policy's business.
        /// </summary>
        public static void Mask(DuelView v, int u, int[] slots, byte[] mask, int off)
        {
            int side = v.Side[u];
            int enemySafe = DuelView.EnemySafe(side);
            bool guard = v.Time - v.StrikeAt[u] < CancelGuard;
            mask[off + Continue] = 1;
            for (int a = 1; a < Count; a++)
            {
                byte ok = 1;
                if (guard) ok = 0;
                else if (a >= AttackSlot && a < Sweep)
                {
                    int t = AttackTarget(a, slots);
                    if (t < 0 || v.ZoneAt(v.X[t], v.Z[t]) == DuelView.OwnSafe(v.Side[t])) ok = 0;
                }
                else
                {
                    int kind, tgt; float x, z;
                    Decode(v, u, a, slots, out kind, out x, out z, out tgt);
                    if (kind == OrdNone || v.ZoneAt(x, z) == enemySafe) ok = 0;
                }
                mask[off + a] = ok;
            }
        }
    }

    /// <summary>
    /// Automatic animation skip (rule "skips on"): after the unit's strike started and the damage is committed, re-issue the same
    /// attack so the fresh AttackInfo forgets its cooldown. Fired on the fixed AI tick, identically in sim and game.
    /// The host calls ShouldFire every fixed tick; on true it re-sends Attack(target) (game: BtcDuel.Attack with force).
    /// </summary>
    public sealed class SkipExecutor
    {
        /// <summary>Fixed frames after the strike start to fire (bot Chosen uses ~11 = 220 ms; must be after the cancel window).</summary>
        public int DelayFrames = 11;
        public bool Enabled = true;
        int[] _strikeFrame = new int[DuelView.MaxUnits];
        int[] _target = new int[DuelView.MaxUnits];

        public SkipExecutor() { Clear(); }

        public void Clear() { for (int i = 0; i < _strikeFrame.Length; i++) { _strikeFrame[i] = -1; _target[i] = -1; } }

        /// <summary>Strike started (frame = fixed frame counter, target = unit index).</summary>
        public void OnStrike(int unit, int frame, int target) { _strikeFrame[unit] = frame; _target[unit] = target; }

        /// <summary>Any new order for the unit voids a pending skip (the strike is already handled by that order).</summary>
        public void OnOrder(int unit) { _strikeFrame[unit] = -1; }

        /// <summary>True exactly once per strike when the skip must fire now; target = whom to re-attack.</summary>
        public bool ShouldFire(int unit, int frame, out int target)
        {
            target = _target[unit];
            int f0 = _strikeFrame[unit];
            if (!Enabled || f0 < 0 || frame - f0 < DelayFrames) return false;
            _strikeFrame[unit] = -1;
            return target >= 0;
        }
    }
}
