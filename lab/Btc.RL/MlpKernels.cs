using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Btc.RL;

/// <summary>
/// Cache-blocked AVX2/FMA linear kernels (Zen2: 32 KB L1d, 512 KB L2 per core). The widest layer (critic input 1101 x 256 = 1.1 MB)
/// does not fit L2, so every kernel walks a row block that stays in L2 while the weights stream through once per block.
/// The previous simple kernels live in MlpOpsRef (kernel tests, bench-mlp --ref).
/// </summary>
public static unsafe partial class MlpOps
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Vector256<float> Fma(Vector256<float> a, Vector256<float> b, Vector256<float> c) => Vector256.FusedMultiplyAdd(a, b, c);

    public const int RowBlock = 48;
    /// <summary>2-row remainder tile in LinearForward (A/B switch for bench-train; results are bit-identical either way).</summary>
    public static bool Tile2 = true;

    // Y[r,o] = b[o] + sum_i X[r,i] W[o,i]. Register tile: 4 rows x 3 outputs (12 accumulators, 7 loads per 12 FMA).
    public static void LinearForward(float* X, int rows, int nIn, float* W, float* B, int nOut, float* Y) => LinearForward(X, rows, nIn, W, B, nOut, Y, nIn);

    /// <summary>ldw = row stride of W (W may be a column slice of a wider matrix: critic split).</summary>
    public static void LinearForward(float* X, int rows, int nIn, float* W, float* B, int nOut, float* Y, int ldw)
    {
        for (int rb = 0; rb < rows; rb += RowBlock)
        {
            int re = Math.Min(rows, rb + RowBlock);
            int o = 0;
            for (; o + 3 <= nOut; o += 3)
            {
                float* w0 = W + o * ldw, w1 = w0 + ldw, w2 = w1 + ldw;
                float b0 = B[o], b1 = B[o + 1], b2 = B[o + 2];
                int r = rb;
                for (; r + 4 <= re; r += 4)
                {
                    float* x0 = X + r * nIn, x1 = x0 + nIn, x2 = x1 + nIn, x3 = x2 + nIn;
                    var a00 = Vector256<float>.Zero; var a01 = a00; var a02 = a00;
                    var a10 = a00; var a11 = a00; var a12 = a00;
                    var a20 = a00; var a21 = a00; var a22 = a00;
                    var a30 = a00; var a31 = a00; var a32 = a00;
                    int i = 0;
                    for (; i + 8 <= nIn; i += 8)
                    {
                        var v0 = Vector256.Load(w0 + i); var v1 = Vector256.Load(w1 + i); var v2 = Vector256.Load(w2 + i);
                        var x = Vector256.Load(x0 + i); a00 = Fma(x, v0, a00); a01 = Fma(x, v1, a01); a02 = Fma(x, v2, a02);
                        x = Vector256.Load(x1 + i); a10 = Fma(x, v0, a10); a11 = Fma(x, v1, a11); a12 = Fma(x, v2, a12);
                        x = Vector256.Load(x2 + i); a20 = Fma(x, v0, a20); a21 = Fma(x, v1, a21); a22 = Fma(x, v2, a22);
                        x = Vector256.Load(x3 + i); a30 = Fma(x, v0, a30); a31 = Fma(x, v1, a31); a32 = Fma(x, v2, a32);
                    }
                    float s00 = Vector256.Sum(a00), s01 = Vector256.Sum(a01), s02 = Vector256.Sum(a02);
                    float s10 = Vector256.Sum(a10), s11 = Vector256.Sum(a11), s12 = Vector256.Sum(a12);
                    float s20 = Vector256.Sum(a20), s21 = Vector256.Sum(a21), s22 = Vector256.Sum(a22);
                    float s30 = Vector256.Sum(a30), s31 = Vector256.Sum(a31), s32 = Vector256.Sum(a32);
                    for (; i < nIn; i++)
                    {
                        float u0 = w0[i], u1 = w1[i], u2 = w2[i];
                        float q = x0[i]; s00 += q * u0; s01 += q * u1; s02 += q * u2;
                        q = x1[i]; s10 += q * u0; s11 += q * u1; s12 += q * u2;
                        q = x2[i]; s20 += q * u0; s21 += q * u1; s22 += q * u2;
                        q = x3[i]; s30 += q * u0; s31 += q * u1; s32 += q * u2;
                    }
                    float* y = Y + r * nOut + o;
                    y[0] = s00 + b0; y[1] = s01 + b1; y[2] = s02 + b2; y += nOut;
                    y[0] = s10 + b0; y[1] = s11 + b1; y[2] = s12 + b2; y += nOut;
                    y[0] = s20 + b0; y[1] = s21 + b1; y[2] = s22 + b2; y += nOut;
                    y[0] = s30 + b0; y[1] = s31 + b1; y[2] = s32 + b2;
                }
                // 2-row tile for the remainder (inference batches of 6 live rows = 4 + 2): same per-row summation order as the 1-row path
                for (; Tile2 && r + 2 <= re; r += 2)
                {
                    float* x0 = X + r * nIn, x1 = x0 + nIn;
                    var a00 = Vector256<float>.Zero; var a01 = a00; var a02 = a00;
                    var a10 = a00; var a11 = a00; var a12 = a00;
                    int i = 0;
                    for (; i + 8 <= nIn; i += 8)
                    {
                        var v0 = Vector256.Load(w0 + i); var v1 = Vector256.Load(w1 + i); var v2 = Vector256.Load(w2 + i);
                        var x = Vector256.Load(x0 + i); a00 = Fma(x, v0, a00); a01 = Fma(x, v1, a01); a02 = Fma(x, v2, a02);
                        x = Vector256.Load(x1 + i); a10 = Fma(x, v0, a10); a11 = Fma(x, v1, a11); a12 = Fma(x, v2, a12);
                    }
                    float s00 = Vector256.Sum(a00), s01 = Vector256.Sum(a01), s02 = Vector256.Sum(a02);
                    float s10 = Vector256.Sum(a10), s11 = Vector256.Sum(a11), s12 = Vector256.Sum(a12);
                    for (; i < nIn; i++)
                    {
                        float u0 = w0[i], u1 = w1[i], u2 = w2[i];
                        float q = x0[i]; s00 += q * u0; s01 += q * u1; s02 += q * u2;
                        q = x1[i]; s10 += q * u0; s11 += q * u1; s12 += q * u2;
                    }
                    float* y = Y + r * nOut + o;
                    y[0] = s00 + b0; y[1] = s01 + b1; y[2] = s02 + b2; y += nOut;
                    y[0] = s10 + b0; y[1] = s11 + b1; y[2] = s12 + b2;
                }
                for (; r < re; r++)
                {
                    float* x0 = X + r * nIn;
                    var a0 = Vector256<float>.Zero; var a1 = a0; var a2 = a0;
                    int i = 0;
                    for (; i + 8 <= nIn; i += 8)
                    {
                        var x = Vector256.Load(x0 + i);
                        a0 = Fma(x, Vector256.Load(w0 + i), a0); a1 = Fma(x, Vector256.Load(w1 + i), a1); a2 = Fma(x, Vector256.Load(w2 + i), a2);
                    }
                    float s0 = Vector256.Sum(a0), s1 = Vector256.Sum(a1), s2 = Vector256.Sum(a2);
                    for (; i < nIn; i++) { float q = x0[i]; s0 += q * w0[i]; s1 += q * w1[i]; s2 += q * w2[i]; }
                    float* y = Y + r * nOut + o;
                    y[0] = s0 + b0; y[1] = s1 + b1; y[2] = s2 + b2;
                }
            }
            for (; o < nOut; o++)
            {
                float* w0 = W + o * ldw;
                int r = rb;
                for (; r + 4 <= re; r += 4)
                {
                    float* x0 = X + r * nIn, x1 = x0 + nIn, x2 = x1 + nIn, x3 = x2 + nIn;
                    var a0 = Vector256<float>.Zero; var a1 = a0; var a2 = a0; var a3 = a0;
                    int i = 0;
                    for (; i + 8 <= nIn; i += 8)
                    {
                        var wv = Vector256.Load(w0 + i);
                        a0 = Fma(Vector256.Load(x0 + i), wv, a0); a1 = Fma(Vector256.Load(x1 + i), wv, a1);
                        a2 = Fma(Vector256.Load(x2 + i), wv, a2); a3 = Fma(Vector256.Load(x3 + i), wv, a3);
                    }
                    float s0 = Vector256.Sum(a0), s1 = Vector256.Sum(a1), s2 = Vector256.Sum(a2), s3 = Vector256.Sum(a3);
                    for (; i < nIn; i++) { float u = w0[i]; s0 += x0[i] * u; s1 += x1[i] * u; s2 += x2[i] * u; s3 += x3[i] * u; }
                    float bo = B[o];
                    Y[r * nOut + o] = s0 + bo; Y[(r + 1) * nOut + o] = s1 + bo; Y[(r + 2) * nOut + o] = s2 + bo; Y[(r + 3) * nOut + o] = s3 + bo;
                }
                for (; r < re; r++)
                {
                    float* x0 = X + r * nIn;
                    var a0 = Vector256<float>.Zero;
                    int i = 0;
                    for (; i + 8 <= nIn; i += 8) a0 = Fma(Vector256.Load(x0 + i), Vector256.Load(w0 + i), a0);
                    float s0 = Vector256.Sum(a0);
                    for (; i < nIn; i++) s0 += x0[i] * w0[i];
                    Y[r * nOut + o] = s0 + B[o];
                }
            }
        }
    }

    // dX[r,i] = sum_o dY[r,o] W[o,i] (overwrites dX). Register tile: 4 rows x 16 inputs; the W strip (nOut x 16) stays in L1
    // while the row tiles of the block run over it.
    public static void LinearBackwardInput(float* dY, int rows, int nOut, float* W, int nIn, float* dX)
    {
        for (int rb = 0; rb < rows; rb += RowBlock)
        {
            int re = Math.Min(rows, rb + RowBlock);
            int i = 0;
            for (; i + 16 <= nIn; i += 16)
            {
                int r = rb;
                for (; r + 4 <= re; r += 4)
                {
                    float* d0 = dY + r * nOut, d1 = d0 + nOut, d2 = d1 + nOut, d3 = d2 + nOut;
                    var a0 = Vector256<float>.Zero; var b0 = a0; var a1 = a0; var b1 = a0; var a2 = a0; var b2 = a0; var a3 = a0; var b3 = a0;
                    float* wp = W + i;
                    for (int o = 0; o < nOut; o++, wp += nIn)
                    {
                        var wa = Vector256.Load(wp); var wb = Vector256.Load(wp + 8);
                        var g = Vector256.Create(d0[o]); a0 = Fma(g, wa, a0); b0 = Fma(g, wb, b0);
                        g = Vector256.Create(d1[o]); a1 = Fma(g, wa, a1); b1 = Fma(g, wb, b1);
                        g = Vector256.Create(d2[o]); a2 = Fma(g, wa, a2); b2 = Fma(g, wb, b2);
                        g = Vector256.Create(d3[o]); a3 = Fma(g, wa, a3); b3 = Fma(g, wb, b3);
                    }
                    float* x = dX + r * nIn + i;
                    a0.Store(x); b0.Store(x + 8); x += nIn;
                    a1.Store(x); b1.Store(x + 8); x += nIn;
                    a2.Store(x); b2.Store(x + 8); x += nIn;
                    a3.Store(x); b3.Store(x + 8);
                }
                for (; r < re; r++)
                {
                    float* d0 = dY + r * nOut;
                    var a0 = Vector256<float>.Zero; var b0 = a0;
                    float* wp = W + i;
                    for (int o = 0; o < nOut; o++, wp += nIn)
                    {
                        var g = Vector256.Create(d0[o]);
                        a0 = Fma(g, Vector256.Load(wp), a0); b0 = Fma(g, Vector256.Load(wp + 8), b0);
                    }
                    float* x = dX + r * nIn + i;
                    a0.Store(x); b0.Store(x + 8);
                }
            }
            for (; i < nIn; i++)
                for (int r = rb; r < re; r++)
                {
                    float* d0 = dY + r * nOut;
                    float s = 0;
                    for (int o = 0; o < nOut; o++) s += d0[o] * W[o * nIn + i];
                    dX[r * nIn + i] = s;
                }
        }
    }

    // dW[o,i] += sum_r dY[r,o] X[r,i]; dB[o] += sum_r dY[r,o]. Per row block the X strip (block x 16 inputs, 3 KB) stays in L1
    // while every output quad accumulates over it. Register tile: 4 outputs x 16 inputs.
    public static void LinearBackwardWeights(float* dY, float* X, int rows, int nOut, int nIn, float* dW, float* dB) => LinearBackwardWeights(dY, X, rows, nOut, nIn, dW, dB, nIn);

    public static void LinearBackwardWeights(float* dY, float* X, int rows, int nOut, int nIn, float* dW, float* dB, int ldw)
    {
        for (int rb = 0; rb < rows; rb += RowBlock)
        {
            int re = Math.Min(rows, rb + RowBlock);
            int i = 0;
            for (; i + 16 <= nIn; i += 16)
            {
                int o = 0;
                for (; o + 4 <= nOut; o += 4)
                {
                    float* w0 = dW + o * ldw + i, w1 = w0 + ldw, w2 = w1 + ldw, w3 = w2 + ldw;
                    var a0 = Vector256.Load(w0); var b0 = Vector256.Load(w0 + 8);
                    var a1 = Vector256.Load(w1); var b1 = Vector256.Load(w1 + 8);
                    var a2 = Vector256.Load(w2); var b2 = Vector256.Load(w2 + 8);
                    var a3 = Vector256.Load(w3); var b3 = Vector256.Load(w3 + 8);
                    float* xr = X + rb * nIn + i; float* dr = dY + rb * nOut + o;
                    for (int r = rb; r < re; r++, xr += nIn, dr += nOut)
                    {
                        var xa = Vector256.Load(xr); var xb = Vector256.Load(xr + 8);
                        var g = Vector256.Create(dr[0]); a0 = Fma(g, xa, a0); b0 = Fma(g, xb, b0);
                        g = Vector256.Create(dr[1]); a1 = Fma(g, xa, a1); b1 = Fma(g, xb, b1);
                        g = Vector256.Create(dr[2]); a2 = Fma(g, xa, a2); b2 = Fma(g, xb, b2);
                        g = Vector256.Create(dr[3]); a3 = Fma(g, xa, a3); b3 = Fma(g, xb, b3);
                    }
                    a0.Store(w0); b0.Store(w0 + 8); a1.Store(w1); b1.Store(w1 + 8);
                    a2.Store(w2); b2.Store(w2 + 8); a3.Store(w3); b3.Store(w3 + 8);
                }
                for (; o < nOut; o++)
                {
                    float* w0 = dW + o * ldw + i;
                    var a0 = Vector256.Load(w0); var b0 = Vector256.Load(w0 + 8);
                    float* xr = X + rb * nIn + i;
                    for (int r = rb; r < re; r++, xr += nIn)
                    {
                        var g = Vector256.Create(dY[r * nOut + o]);
                        a0 = Fma(g, Vector256.Load(xr), a0); b0 = Fma(g, Vector256.Load(xr + 8), b0);
                    }
                    a0.Store(w0); b0.Store(w0 + 8);
                }
            }
            for (; i < nIn; i++)
                for (int o = 0; o < nOut; o++)
                {
                    float s = 0;
                    for (int r = rb; r < re; r++) s += dY[r * nOut + o] * X[r * nIn + i];
                    dW[o * ldw + i] += s;
                }
        }
        for (int k = 0; k < nOut; k++)
        {
            float s = 0;
            for (int r = 0; r < rows; r++) s += dY[r * nOut + k];
            dB[k] += s;
        }
    }

    /// <summary>In-place tanh: tanh(x) = (e - 1) / (e + 1), e = exp(2x) (Cephes expf polynomial), x clamped to [-9, 9]
    /// (tanh(9) = 1 - 3e-8). Absolute error ~1e-7 vs MathF.Tanh. Training only: shipped inference (MlpPolicy) uses Math.Tanh.</summary>
    public static void TanhInPlace(float* y, int n)
    {
        int i = 0;
        if (Avx2.IsSupported && System.Runtime.Intrinsics.X86.Fma.IsSupported)
        {
            var lim = Vector256.Create(9f); var nlim = Vector256.Create(-9f);
            var log2e = Vector256.Create(1.44269504088896341f);
            var c1 = Vector256.Create(0.693359375f); var c2 = Vector256.Create(-2.12194440e-4f);
            var p0 = Vector256.Create(1.9875691500E-4f); var p1 = Vector256.Create(1.3981999507E-3f);
            var p2 = Vector256.Create(8.3334519073E-3f); var p3 = Vector256.Create(4.1665795894E-2f);
            var p4 = Vector256.Create(1.6666665459E-1f); var p5 = Vector256.Create(5.0000001201E-1f);
            var one = Vector256.Create(1f); var bias = Vector256.Create(127);
            for (; i + 8 <= n; i += 8)
            {
                var x = Avx.Min(Avx.Max(Vector256.Load(y + i), nlim), lim);
                var t = x + x;
                var fx = Avx.RoundToNearestInteger(t * log2e);
                var r = t - fx * c1 - fx * c2;
                var r2 = r * r;
                var p = Fma(p0, r, p1); p = Fma(p, r, p2); p = Fma(p, r, p3); p = Fma(p, r, p4); p = Fma(p, r, p5);
                p = Fma(p, r2, r) + one;
                var e = p * Avx2.ShiftLeftLogical(Avx.ConvertToVector256Int32(fx) + bias, 23).AsSingle();
                Avx.Divide(e - one, e + one).Store(y + i);
            }
        }
        for (; i < n; i++) y[i] = MathF.Tanh(y[i]);
    }
}
