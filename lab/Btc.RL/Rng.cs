namespace Btc.RL;

/// <summary>Deterministic SplitMix64 generator (reference type: never copied by accident).</summary>
public sealed class Rng
{
    public ulong State;
    public Rng(ulong seed) { State = seed; }

    public ulong NextU64()
    {
        ulong z = (State += 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform in [0,1).</summary>
    public float NextFloat() => (NextU64() >> 40) * (1f / 16777216f);
    public double NextDouble() => (NextU64() >> 11) * (1.0 / 9007199254740992.0);
    public int Next(int n) => (int)((NextU64() >> 33) % (ulong)n);

    public double Gaussian()
    {
        double u1 = 1.0 - NextDouble(), u2 = NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    public static ulong Mix(ulong a, ulong b)
    {
        var r = new Rng(a * 0x9E3779B97F4A7C15UL ^ b);
        r.NextU64();
        return r.NextU64();
    }
}
