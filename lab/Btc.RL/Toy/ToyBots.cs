namespace Btc.RL.Toy;

/// <summary>Scripted toy opponents. They read the game state directly and emit egocentric actions.</summary>
public static class ToyBots
{
    public static OpponentSpec Spec(string name) => name switch
    {
        "random" => new OpponentSpec(name, () => new RandomBot()),
        "rusher" => new OpponentSpec(name, () => new RusherBot()),
        "hunter" => new OpponentSpec(name, () => new HunterBot()),
        _ => throw new ArgumentException("unknown toy opponent: " + name),
    };

    public static readonly string[] Names = { "random", "rusher", "hunter" };

    /// <summary>Changes to a random action with probability 0.15 per decision, otherwise keeps the current order.</summary>
    sealed class RandomBot : IOpponent
    {
        public void Act(IEnvBatch env, int e, int side, Rng rng, int[] a)
        {
            var g = ((ToyDuelBatch)env).Game(e);
            for (int i = 0; i < g.N; i++) a[i] = rng.NextFloat() < 0.15f ? rng.Next(ToyDuelGame.NumActions) : ToyDuelGame.ActContinue;
        }
    }

    /// <summary>Attacks the nearest enemy that is not in its own safe zone; otherwise walks to the middle and holds it.</summary>
    sealed class RusherBot : IOpponent
    {
        readonly int[] _buf = new int[8];
        public void Act(IEnvBatch env, int e, int side, Rng rng, int[] a)
        {
            var g = ((ToyDuelBatch)env).Game(e);
            int u0 = side == 0 ? 0 : g.N;
            for (int i = 0; i < g.N; i++)
            {
                int u = u0 + i;
                int c = g.SortedEnemies(u, _buf), pick = -1;
                for (int k = 0; k < c && k < ToyDuelGame.MaxK; k++) if (!g.InOwnSafe(_buf[k])) { pick = k; break; }
                a[i] = pick >= 0 ? ToyDuelGame.ActAttack0 + pick : HoldMid(g, u);
            }
        }
    }

    /// <summary>Focuses the lowest-hp enemy outside its safe zone; retreats home below 40% hp.</summary>
    sealed class HunterBot : IOpponent
    {
        readonly int[] _buf = new int[8];
        public void Act(IEnvBatch env, int e, int side, Rng rng, int[] a)
        {
            var g = ((ToyDuelBatch)env).Game(e);
            int u0 = side == 0 ? 0 : g.N;
            for (int i = 0; i < g.N; i++)
            {
                int u = u0 + i;
                if (g.Hp[u] < 0.4f * ToyDuelGame.MaxHp) { a[i] = ToyDuelGame.ActRetreat; continue; }
                int c = g.SortedEnemies(u, _buf), pick = -1; float best = float.MaxValue;
                for (int k = 0; k < c && k < ToyDuelGame.MaxK; k++)
                    if (!g.InOwnSafe(_buf[k]) && g.Hp[_buf[k]] < best) { best = g.Hp[_buf[k]]; pick = k; }
                a[i] = pick >= 0 ? ToyDuelGame.ActAttack0 + pick : HoldMid(g, u);
            }
        }
    }

    static int HoldMid(ToyDuelGame g, int u)
    {
        float dx = 50f - g.X[u], dy = 10f - g.Y[u];
        if (dx * dx + dy * dy < 4f) return ToyDuelGame.ActStop;
        return g.MoveActionToward(u, 50f, 10f);
    }
}
