// Shadows of the Brix (Castle Story) types the duel bot sources touch: Labor.GetCurrentOrPendingTask, Task.Target/Description,
// DamageReceiver.CurrentHP/MaxHP/Heal. They read the sim arrays live.
using Btc.Sim;
using UnityEngine;

namespace Brix.Game.AI
{
    /// <summary>Vanilla Task: Target = the order value when it is a GameObject (GoAttackDirectly), else null; Description = instruction name.</summary>
    internal sealed class Task
    {
        public GameObject Target;
        public string Description = string.Empty;
    }

    /// <summary>
    /// Current task of a unit, derived from the sim's current order: Attack -> "GoAttackDirectly" with Target (null task once the target is dead,
    /// the vanilla task ends), MoveAttack -> "MoveAttack" (Target null: the auto-acquired target is not the task target), Move -> "RunToward",
    /// None/Stop -> no task. One Task object per unit, mutated in place (no allocation).
    /// </summary>
    internal sealed class Labor
    {
        internal DuelSim Sim;
        internal int I;
        internal GameObject Go;
        internal GameObject[] All;          // sim index -> GameObject
        private readonly Task _task = new Task();

        public GameObject gameObject { get { return Go; } }

        public Task GetCurrentOrPendingTask()
        {
            if (I >= Sim.Ord.Length) return null;
            switch (Sim.Ord[I])
            {
                case SimOrder.Attack:
                    {
                        int t = Sim.OrdTarget[I];
                        if (t < 0 || t >= All.Length || Sim.Dead[t]) return null;
                        _task.Target = All[t]; _task.Description = "GoAttackDirectly"; return _task;
                    }
                case SimOrder.MoveAttack: _task.Target = null; _task.Description = "MoveAttack"; return _task;
                case SimOrder.Move: _task.Target = null; _task.Description = "RunToward"; return _task;
                default: return null;
            }
        }
    }
}

namespace Brix.Game.AI.Damage
{
    internal sealed class DamageReceiver
    {
        internal DuelSim Sim;
        internal int I;
        internal GameObject Go;
        public float CurrentHP { get { return Sim.Hp[I]; } }
        public float MaxHP { get { return Sim.MaxHp[I]; } }
        public GameObject gameObject { get { return Go; } }
        public void Heal(float v)
        {
            float h = Sim.Hp[I] + v;
            Sim.Hp[I] = h > Sim.MaxHp[I] ? Sim.MaxHp[I] : h;
        }
    }
}
