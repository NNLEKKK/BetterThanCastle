namespace Btc.RL;

/// <summary>Adam with global grad-norm clipping. State (m, v, t) is checkpointed.</summary>
public sealed class Adam
{
    public readonly float[] M, V;
    public long T;
    public float Beta1 = 0.9f, Beta2 = 0.999f, Eps = 1e-5f;

    public Adam(int n) { M = new float[n]; V = new float[n]; }

    public static double Norm(float[] g)
    {
        double s = 0;
        for (int i = 0; i < g.Length; i++) s += (double)g[i] * g[i];
        return Math.Sqrt(s);
    }

    /// <summary>Clips g in place to maxNorm (if > 0), applies one Adam step. Returns the pre-clip norm.</summary>
    public double Step(float[] p, float[] g, float lr, float maxNorm)
    {
        double norm = Norm(g);
        float scale = maxNorm > 0 && norm > maxNorm ? (float)(maxNorm / (norm + 1e-6)) : 1f;
        T++;
        float b1 = Beta1, b2 = Beta2;
        float c1 = 1f / (1f - MathF.Pow(b1, T)), c2 = 1f / (1f - MathF.Pow(b2, T));
        for (int i = 0; i < p.Length; i++)
        {
            float gi = g[i] * scale;
            float m = M[i] = b1 * M[i] + (1f - b1) * gi;
            float v = V[i] = b2 * V[i] + (1f - b2) * gi * gi;
            p[i] -= lr * (m * c1) / (MathF.Sqrt(v * c2) + Eps);
        }
        return norm;
    }

    public void Write(BinaryWriter w)
    {
        w.Write(T); w.Write(M.Length);
        foreach (var x in M) w.Write(x);
        foreach (var x in V) w.Write(x);
    }

    public void Read(BinaryReader r)
    {
        T = r.ReadInt64(); int n = r.ReadInt32();
        if (n != M.Length) throw new InvalidDataException("adam size mismatch");
        for (int i = 0; i < n; i++) M[i] = r.ReadSingle();
        for (int i = 0; i < n; i++) V[i] = r.ReadSingle();
    }
}
