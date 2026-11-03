namespace Btc.RL;

public struct ActorStats { public double Pg, Ent, Kl, ClipFrac; public int N; }
public struct CriticStats { public double VLoss; public int N; }

/// <summary>Masked multi-head categorical helpers. Masked logits behave like -1e9 (probability exactly 0).</summary>
public static class Categorical
{
    /// <summary>Log-sum-exp over the allowed entries of one head; returns -inf if none allowed.</summary>
    public static float Lse(float[] z, int off, int n, byte[] mask, int moff)
    {
        float mx = float.NegativeInfinity;
        for (int j = 0; j < n; j++) if (mask == null || mask[moff + j] != 0) mx = MathF.Max(mx, z[off + j]);
        if (float.IsNegativeInfinity(mx)) return mx;
        float s = 0;
        for (int j = 0; j < n; j++) if (mask == null || mask[moff + j] != 0) s += MathF.Exp(z[off + j] - mx);
        return mx + MathF.Log(s);
    }

    /// <summary>Samples one action per head (u from rng), writes act[aoff+h], returns the joint log-prob.</summary>
    public static float Sample(float[] z, int off, int[] heads, byte[] mask, int moff, Rng rng, int[] act, int aoff)
    {
        float logp = 0;
        for (int h = 0; h < heads.Length; h++)
        {
            int n = heads[h];
            float lse = Lse(z, off, n, mask, moff);
            float u = rng.NextFloat(), c = 0; int pick = -1, last = 0;
            for (int j = 0; j < n; j++)
            {
                if (mask != null && mask[moff + j] == 0) continue;
                last = j;
                c += MathF.Exp(z[off + j] - lse);
                if (u < c) { pick = j; break; }
            }
            if (pick < 0) pick = last;
            act[aoff + h] = pick;
            logp += z[off + pick] - lse;
            off += n; moff += n;
        }
        return logp;
    }
}

/// <summary>PPO losses on a block of rows already placed in the workspace input. Losses are SUMS times `scale`.</summary>
public static class PpoLoss
{
    public static double Actor(Mlp net, MlpWorkspace ws, int rows, int[] heads, byte[] mask, int[] act, float[] oldLogp, float[] adv,
        float clip, float entCoef, float scale, bool backward, ref ActorStats st)
    {
        MlpOps.Forward(net, ws, rows);
        int L = net.OutSize, H = heads.Length;
        float[] z = ws.Output, dz = ws.OutGrad;
        double loss = 0;
        for (int r = 0; r < rows; r++)
        {
            int zo = r * L, ao = r * H;
            float logp = 0, ent = 0;
            int off = 0;
            for (int h = 0; h < H; h++)
            {
                int n = heads[h];
                float lse = Categorical.Lse(z, zo + off, n, mask, zo + off);
                float eh = 0;
                for (int j = 0; j < n; j++)
                {
                    if (mask[zo + off + j] == 0) continue;
                    float lp = z[zo + off + j] - lse;
                    eh -= MathF.Exp(lp) * lp;
                }
                ent += eh;
                logp += z[zo + off + act[ao + h]] - lse;
                off += n;
            }
            float A = adv[r];
            float ratio = MathF.Exp(logp - oldLogp[r]);
            float rc = Math.Clamp(ratio, 1f - clip, 1f + clip);
            float s1 = ratio * A, s2 = rc * A;
            float pg = -MathF.Min(s1, s2);
            loss += (pg - entCoef * ent) * scale;
            st.Pg += pg; st.Ent += ent; st.N++;
            st.Kl += (ratio - 1f) - (logp - oldLogp[r]);
            if (MathF.Abs(ratio - 1f) > clip) st.ClipFrac += 1;
            if (!backward) continue;
            float g = s1 <= s2 ? -A * ratio : 0f; // dLoss/dlogp
            off = 0;
            for (int h = 0; h < H; h++)
            {
                int n = heads[h];
                float lse = Categorical.Lse(z, zo + off, n, mask, zo + off);
                float eh = 0;
                for (int j = 0; j < n; j++)
                {
                    if (mask[zo + off + j] == 0) continue;
                    float lp = z[zo + off + j] - lse; eh -= MathF.Exp(lp) * lp;
                }
                int a = act[ao + h];
                for (int j = 0; j < n; j++)
                {
                    int k = zo + off + j;
                    if (mask[k] == 0) { dz[k] = 0; continue; }
                    float lp = z[k] - lse, p = MathF.Exp(lp);
                    float d = g * ((j == a ? 1f : 0f) - p) + entCoef * p * (lp + eh);
                    dz[k] = d * scale;
                }
                off += n;
            }
        }
        if (backward) MlpOps.Backward(net, ws, rows);
        return loss;
    }

    /// <summary>Actor() minus the network: rows r0..r1 of logits z (row r = sample s = sIdx[r]; mask/act/oldLogp/adv indexed by s),
    /// writes dLoss/dz into dz (same layout as z). Same math as Actor().</summary>
    public static void ActorGrad(float[] z, float[] dz, int r0, int r1, int L, int[] heads, int[] sIdx, byte[] mask, int[] act,
        float[] oldLogp, float[] adv, float clip, float entCoef, float scale, ref ActorStats st)
    {
        int H = heads.Length;
        for (int r = r0; r < r1; r++)
        {
            int s = sIdx[r], zo = r * L, mo = s * L, ao = s * H;
            float logp = 0, ent = 0;
            int off = 0;
            for (int h = 0; h < H; h++)
            {
                int n = heads[h];
                float lse = Categorical.Lse(z, zo + off, n, mask, mo + off);
                float eh = 0;
                for (int j = 0; j < n; j++)
                {
                    if (mask[mo + off + j] == 0) continue;
                    float lp = z[zo + off + j] - lse;
                    eh -= MathF.Exp(lp) * lp;
                }
                ent += eh;
                logp += z[zo + off + act[ao + h]] - lse;
                off += n;
            }
            float A = adv[s];
            float ratio = MathF.Exp(logp - oldLogp[s]);
            float rc = Math.Clamp(ratio, 1f - clip, 1f + clip);
            float s1 = ratio * A, s2 = rc * A;
            float pg = -MathF.Min(s1, s2);
            st.Pg += pg; st.Ent += ent; st.N++;
            st.Kl += (ratio - 1f) - (logp - oldLogp[s]);
            if (MathF.Abs(ratio - 1f) > clip) st.ClipFrac += 1;
            float g = s1 <= s2 ? -A * ratio : 0f;
            off = 0;
            for (int h = 0; h < H; h++)
            {
                int n = heads[h];
                float lse = Categorical.Lse(z, zo + off, n, mask, mo + off);
                float eh = 0;
                for (int j = 0; j < n; j++)
                {
                    if (mask[mo + off + j] == 0) continue;
                    float lp = z[zo + off + j] - lse; eh -= MathF.Exp(lp) * lp;
                }
                int a = act[ao + h];
                for (int j = 0; j < n; j++)
                {
                    int k = zo + off + j;
                    if (mask[mo + off + j] == 0) { dz[k] = 0; continue; }
                    float lp = z[k] - lse, p = MathF.Exp(lp);
                    dz[k] = (g * ((j == a ? 1f : 0f) - p) + entCoef * p * (lp + eh)) * scale;
                }
                off += n;
            }
        }
    }

    /// <summary>Critic() minus the network: values v of rows r0..r1 (ret/oldV indexed by s = sIdx[r]) -> dv.</summary>
    public static void CriticGrad(float[] v, float[] dv, int r0, int r1, int[] sIdx, float[] ret, float[] oldV, float vclip, float coef,
        float scale, ref CriticStats st)
    {
        for (int r = r0; r < r1; r++)
        {
            int s = sIdx[r];
            float val = v[r], R = ret[s];
            float l1 = (val - R) * (val - R);
            float l = l1, gr = val - R;
            if (vclip > 0)
            {
                float vc = oldV[s] + Math.Clamp(val - oldV[s], -vclip, vclip);
                float l2 = (vc - R) * (vc - R);
                if (l2 > l1) { l = l2; gr = 0f; }
            }
            st.VLoss += 0.5 * l; st.N++;
            dv[r] = gr * coef * scale;
        }
    }

    public static double Critic(Mlp net, MlpWorkspace ws, int rows, float[] ret, float[] oldV, float vclip, float coef, float scale,
        bool backward, ref CriticStats st)
    {
        MlpOps.Forward(net, ws, rows);
        float[] v = ws.Output, dv = ws.OutGrad;
        double loss = 0;
        for (int r = 0; r < rows; r++)
        {
            float val = v[r], R = ret[r];
            float l1 = (val - R) * (val - R);
            float l = l1, gr = val - R;
            if (vclip > 0)
            {
                float vc = oldV[r] + Math.Clamp(val - oldV[r], -vclip, vclip);
                float l2 = (vc - R) * (vc - R);
                if (l2 > l1) { l = l2; gr = 0f; }
            }
            loss += 0.5 * l * coef * scale;
            st.VLoss += 0.5 * l; st.N++;
            if (backward) dv[r] = gr * coef * scale;
        }
        if (backward) MlpOps.Backward(net, ws, rows);
        return loss;
    }
}
