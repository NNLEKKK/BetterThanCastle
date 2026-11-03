namespace Btc.RL;

/// <summary>
/// Gradient check: analytic float32 gradients from the batched kernels (MlpOps + PpoLoss) versus central finite
/// differences of an independent float64 scalar re-implementation of the same network and losses.
/// </summary>
public static class GradCheck
{
    public sealed class Result { public double ActorMaxRel, CriticMaxRel; public int ActorChecked, CriticChecked; }

    const float Clip = 0.2f, Ent = 0.05f, VClip = 0.5f;

    public static Result Run(ulong seed = 7, double eps = 1e-5, double minMag = 1e-4)
    {
        var rng = new Rng(seed);
        int[] heads = { 3, 4 }; int L = 7, H = 2, O = 20, rows = 9;
        var actor = new Mlp(O, new[] { 20, 16 }, L, Act.Tanh);
        actor.InitOrthogonal(rng, 1.3f, 1.0f);
        var critic = new Mlp(O + 3, new[] { 24, 16 }, 1, Act.Tanh);
        critic.InitOrthogonal(rng, 1.3f, 1.0f);
        var wa = new MlpWorkspace(actor, rows); var wc = new MlpWorkspace(critic, rows);
        for (int i = 0; i < rows * O; i++) wa.Input[i] = (float)rng.Gaussian();
        for (int i = 0; i < rows * (O + 3); i++) wc.Input[i] = (float)rng.Gaussian();
        var mask = new byte[rows * L]; var act = new int[rows * H];
        var oldLogp = new float[rows]; var adv = new float[rows]; var ret = new float[rows]; var oldV = new float[rows];
        for (int r = 0; r < rows; r++)
        {
            for (int j = 0; j < L; j++) mask[r * L + j] = 1;
            mask[r * L + rng.Next(3)] = 0;            // one masked action in head 0
            int off = 0;
            for (int h = 0; h < H; h++)
            {
                int a; do a = rng.Next(heads[h]); while (mask[r * L + off + a] == 0);
                act[r * H + h] = a; off += heads[h];
            }
            adv[r] = (float)rng.Gaussian(); ret[r] = (float)rng.Gaussian();
        }
        // old logp = current logp + noise so most ratios are inside the clip range (some may be outside: both branches)
        MlpOps.Forward(actor, wa, rows);
        for (int r = 0; r < rows; r++)
        {
            float lp = 0; int off = 0;
            for (int h = 0; h < H; h++)
            {
                lp += wa.Output[r * L + off + act[r * H + h]] - Categorical.Lse(wa.Output, r * L + off, heads[h], mask, r * L + off);
                off += heads[h];
            }
            oldLogp[r] = lp + (float)(0.1 * rng.Gaussian());
        }
        MlpOps.Forward(critic, wc, rows);
        for (int r = 0; r < rows; r++) oldV[r] = wc.Output[r] + (float)(0.3 * rng.Gaussian());

        var res = new Result();
        var sa = new ActorStats(); var sc = new CriticStats();
        Array.Clear(wa.Grad); PpoLoss.Actor(actor, wa, rows, heads, mask, act, oldLogp, adv, Clip, Ent, 1f, true, ref sa);
        Array.Clear(wc.Grad); PpoLoss.Critic(critic, wc, rows, ret, oldV, VClip, 1f, 1f, true, ref sc);

        double ActorRef(double[] p)
        {
            double loss = 0; var z = new double[L];
            for (int r = 0; r < rows; r++)
            {
                RefForward(actor, p, wa.Input, r, z);
                double logp = 0, ent = 0; int off = 0;
                for (int h = 0; h < H; h++)
                {
                    int n = heads[h]; double mx = double.NegativeInfinity;
                    for (int j = 0; j < n; j++) if (mask[r * L + off + j] != 0) mx = Math.Max(mx, z[off + j]);
                    double s = 0;
                    for (int j = 0; j < n; j++) if (mask[r * L + off + j] != 0) s += Math.Exp(z[off + j] - mx);
                    double lse = mx + Math.Log(s);
                    for (int j = 0; j < n; j++) if (mask[r * L + off + j] != 0) { double lp = z[off + j] - lse; ent -= Math.Exp(lp) * lp; }
                    logp += z[off + act[r * H + h]] - lse; off += n;
                }
                double ratio = Math.Exp(logp - oldLogp[r]);
                double s1 = ratio * adv[r], s2 = Math.Clamp(ratio, 1 - Clip, 1 + Clip) * adv[r];
                loss += -Math.Min(s1, s2) - Ent * ent;
            }
            return loss;
        }
        double CriticRef(double[] p)
        {
            double loss = 0; var z = new double[1];
            for (int r = 0; r < rows; r++)
            {
                RefForward(critic, p, wc.Input, r, z);
                double v = z[0], R = ret[r];
                double l1 = (v - R) * (v - R);
                double vc = oldV[r] + Math.Clamp(v - oldV[r], -VClip, VClip);
                loss += 0.5 * Math.Max(l1, (vc - R) * (vc - R));
            }
            return loss;
        }
        (res.ActorMaxRel, res.ActorChecked) = Compare(actor.P, wa.Grad, ActorRef, eps, minMag);
        (res.CriticMaxRel, res.CriticChecked) = Compare(critic.P, wc.Grad, CriticRef, eps, minMag);
        return res;
    }

    static void RefForward(Mlp net, double[] p, float[] input, int row, double[] outp)
    {
        double[] x = new double[net.InSize];
        for (int i = 0; i < x.Length; i++) x[i] = input[row * net.InSize + i];
        for (int l = 0; l < net.NumLayers; l++)
        {
            int nIn = net.Sizes[l], nOut = net.Sizes[l + 1];
            var y = new double[nOut];
            for (int o = 0; o < nOut; o++)
            {
                double s = p[net.BOff[l] + o];
                for (int i = 0; i < nIn; i++) s += x[i] * p[net.WOff[l] + o * nIn + i];
                if (net.Acts[l] == Act.Tanh) s = Math.Tanh(s); else if (net.Acts[l] == Act.Relu) s = Math.Max(0, s);
                y[o] = s;
            }
            x = y;
        }
        Array.Copy(x, outp, x.Length);
    }

    static (double, int) Compare(float[] pf, float[] g, Func<double[], double> loss, double eps, double minMag)
    {
        var p = new double[pf.Length];
        for (int i = 0; i < p.Length; i++) p[i] = pf[i];
        double worst = 0; int n = 0;
        for (int i = 0; i < p.Length; i++)
        {
            double x = p[i];
            p[i] = x + eps; double lp = loss(p);
            p[i] = x - eps; double lm = loss(p);
            p[i] = x;
            double num = (lp - lm) / (2 * eps);
            double mag = Math.Max(Math.Abs(num), Math.Abs(g[i]));
            if (mag < minMag) continue;
            worst = Math.Max(worst, Math.Abs(num - g[i]) / mag); n++;
        }
        return (worst, n);
    }
}
