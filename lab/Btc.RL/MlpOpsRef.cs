using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Btc.Shared;

namespace Btc.RL;

/// <summary>Previous (simple) linear kernels, kept as the reference for kernel tests and bench-mlp --ref.</summary>
public static unsafe class MlpOpsRef
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Vector256<float> Fma(Vector256<float> a, Vector256<float> b, Vector256<float> c) => Vector256.FusedMultiplyAdd(a, b, c);

    // Y[r,o] = b[o] + sum_i X[r,i] W[o,i]. Tiles: 4 rows x 2 outputs. Each row's arithmetic is independent of tiling.
    public static void LinearForward(float* X, int rows, int nIn, float* W, float* B, int nOut, float* Y)
    {
        int r = 0;
        for (; r + 4 <= rows; r += 4)
        {
            float* x0 = X + r * nIn, x1 = x0 + nIn, x2 = x1 + nIn, x3 = x2 + nIn;
            float* y0 = Y + r * nOut;
            for (int o = 0; o < nOut; o++)
            {
                float* w = W + o * nIn;
                var a0 = Vector256<float>.Zero; var a1 = a0; var a2 = a0; var a3 = a0;
                int i = 0;
                for (; i + 8 <= nIn; i += 8)
                {
                    var wv = Vector256.Load(w + i);
                    a0 = Fma(Vector256.Load(x0 + i), wv, a0);
                    a1 = Fma(Vector256.Load(x1 + i), wv, a1);
                    a2 = Fma(Vector256.Load(x2 + i), wv, a2);
                    a3 = Fma(Vector256.Load(x3 + i), wv, a3);
                }
                float s0 = Vector256.Sum(a0), s1 = Vector256.Sum(a1), s2 = Vector256.Sum(a2), s3 = Vector256.Sum(a3);
                for (; i < nIn; i++) { float wi = w[i]; s0 += x0[i] * wi; s1 += x1[i] * wi; s2 += x2[i] * wi; s3 += x3[i] * wi; }
                float bo = B[o];
                y0[o] = s0 + bo; y0[nOut + o] = s1 + bo; y0[2 * nOut + o] = s2 + bo; y0[3 * nOut + o] = s3 + bo;
            }
        }
        for (; r < rows; r++)
        {
            float* x0 = X + r * nIn; float* y0 = Y + r * nOut;
            for (int o = 0; o < nOut; o++)
            {
                float* w = W + o * nIn;
                var a0 = Vector256<float>.Zero;
                int i = 0;
                for (; i + 8 <= nIn; i += 8) a0 = Fma(Vector256.Load(x0 + i), Vector256.Load(w + i), a0);
                float s0 = Vector256.Sum(a0);
                for (; i < nIn; i++) s0 += x0[i] * w[i];
                y0[o] = s0 + B[o];
            }
        }
    }

    // dX[r,i] = sum_o dY[r,o] W[o,i] (overwrites dX). Tiles: 4 rows x 8 inputs.
    public static void LinearBackwardInput(float* dY, int rows, int nOut, float* W, int nIn, float* dX)
    {
        int r = 0;
        for (; r + 4 <= rows; r += 4)
        {
            float* d0 = dY + r * nOut, d1 = d0 + nOut, d2 = d1 + nOut, d3 = d2 + nOut;
            float* x0 = dX + r * nIn;
            int i = 0;
            for (; i + 8 <= nIn; i += 8)
            {
                var a0 = Vector256<float>.Zero; var a1 = a0; var a2 = a0; var a3 = a0;
                for (int o = 0; o < nOut; o++)
                {
                    var wv = Vector256.Load(W + o * nIn + i);
                    a0 = Fma(Vector256.Create(d0[o]), wv, a0);
                    a1 = Fma(Vector256.Create(d1[o]), wv, a1);
                    a2 = Fma(Vector256.Create(d2[o]), wv, a2);
                    a3 = Fma(Vector256.Create(d3[o]), wv, a3);
                }
                a0.Store(x0 + i); a1.Store(x0 + nIn + i); a2.Store(x0 + 2 * nIn + i); a3.Store(x0 + 3 * nIn + i);
            }
            for (; i < nIn; i++)
            {
                float s0 = 0, s1 = 0, s2 = 0, s3 = 0;
                for (int o = 0; o < nOut; o++) { float wv = W[o * nIn + i]; s0 += d0[o] * wv; s1 += d1[o] * wv; s2 += d2[o] * wv; s3 += d3[o] * wv; }
                x0[i] = s0; x0[nIn + i] = s1; x0[2 * nIn + i] = s2; x0[3 * nIn + i] = s3;
            }
        }
        for (; r < rows; r++)
        {
            float* d0 = dY + r * nOut; float* x0 = dX + r * nIn;
            for (int i = 0; i < nIn; i++)
            {
                float s0 = 0;
                for (int o = 0; o < nOut; o++) s0 += d0[o] * W[o * nIn + i];
                x0[i] = s0;
            }
        }
    }

    // dW[o,i] += sum_r dY[r,o] X[r,i]; dB[o] += sum_r dY[r,o]. Tiles: 4 outputs x 16 inputs.
    public static void LinearBackwardWeights(float* dY, float* X, int rows, int nOut, int nIn, float* dW, float* dB)
    {
        int o = 0;
        for (; o + 4 <= nOut; o += 4)
        {
            float* w0 = dW + o * nIn, w1 = w0 + nIn, w2 = w1 + nIn, w3 = w2 + nIn;
            int i = 0;
            for (; i + 16 <= nIn; i += 16)
            {
                var a0 = Vector256.Load(w0 + i); var b0 = Vector256.Load(w0 + i + 8);
                var a1 = Vector256.Load(w1 + i); var b1 = Vector256.Load(w1 + i + 8);
                var a2 = Vector256.Load(w2 + i); var b2 = Vector256.Load(w2 + i + 8);
                var a3 = Vector256.Load(w3 + i); var b3 = Vector256.Load(w3 + i + 8);
                for (int r = 0; r < rows; r++)
                {
                    float* xr = X + r * nIn + i; float* dr = dY + r * nOut + o;
                    var xa = Vector256.Load(xr); var xb = Vector256.Load(xr + 8);
                    var g0 = Vector256.Create(dr[0]); var g1 = Vector256.Create(dr[1]);
                    var g2 = Vector256.Create(dr[2]); var g3 = Vector256.Create(dr[3]);
                    a0 = Fma(g0, xa, a0); b0 = Fma(g0, xb, b0);
                    a1 = Fma(g1, xa, a1); b1 = Fma(g1, xb, b1);
                    a2 = Fma(g2, xa, a2); b2 = Fma(g2, xb, b2);
                    a3 = Fma(g3, xa, a3); b3 = Fma(g3, xb, b3);
                }
                a0.Store(w0 + i); b0.Store(w0 + i + 8); a1.Store(w1 + i); b1.Store(w1 + i + 8);
                a2.Store(w2 + i); b2.Store(w2 + i + 8); a3.Store(w3 + i); b3.Store(w3 + i + 8);
            }
            for (; i < nIn; i++)
            {
                float s0 = 0, s1 = 0, s2 = 0, s3 = 0;
                for (int r = 0; r < rows; r++)
                {
                    float xv = X[r * nIn + i]; float* dr = dY + r * nOut + o;
                    s0 += dr[0] * xv; s1 += dr[1] * xv; s2 += dr[2] * xv; s3 += dr[3] * xv;
                }
                w0[i] += s0; w1[i] += s1; w2[i] += s2; w3[i] += s3;
            }
        }
        for (; o < nOut; o++)
        {
            float* w0 = dW + o * nIn;
            for (int i = 0; i < nIn; i++)
            {
                float s0 = 0;
                for (int r = 0; r < rows; r++) s0 += dY[r * nOut + o] * X[r * nIn + i];
                w0[i] += s0;
            }
        }
        for (int k = 0; k < nOut; k++)
        {
            float s = 0;
            for (int r = 0; r < rows; r++) s += dY[r * nOut + k];
            dB[k] += s;
        }
    }
}
