using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Btc.Sim;
using UnityEngine;

namespace Btc.Bots
{
    /// <summary>
    /// Self test of the bot host: Unity shadow semantics, bot-vs-bot smoke (all controller kinds), determinism (same seed -> same order hash,
    /// interleaved and multi-threaded hosts equal to a lone host), tick cost. While the engine is a stub (Reset leaves Count = 0) the test drives
    /// the matches with StubPhysics (straight-line movement, reach 2.6, 75-frame cooldown reset by a new order, damage 23 frames after the strike).
    /// </summary>
    public static class BotSelfTest
    {
        public static List<string> Run(Action<string> log = null)
        {
            List<string> fail = new List<string>();
            Action<string> L = log ?? (s => { });
            try { Semantics(fail); } catch (Exception e) { fail.Add("semantics threw: " + e); }
            try { Smoke(fail, L); } catch (Exception e) { fail.Add("smoke threw: " + e); }
            try { Determinism(fail, L); } catch (Exception e) { fail.Add("determinism threw: " + e); }
            try { Bench(L, 10, 4); } catch (Exception e) { fail.Add("bench threw: " + e); }
            SimBotHost.DeactivateThread();
            return fail;
        }

        // ---------------- Unity semantics ----------------
        private static void Check(List<string> f, bool ok, string what) { if (!ok) f.Add("semantics: " + what); }

        private static void Semantics(List<string> f)
        {
            Check(f, Vector3.Normalize(new Vector3(1e-6f, 0f, 0f)) == Vector3.zero, "normalized below 1e-5 is zero");
            Check(f, new Vector3(3f, 0f, 4f).normalized.x == 0.6f, "normalized (3,0,4).x = 0.6");
            Check(f, new Vector3(1f, 2f, 3f) == new Vector3(1f, 2f, 3f + 5e-6f), "== tolerance (sqr diff < 1e-10)");
            Check(f, new Vector3(1f, 2f, 3f) != new Vector3(1f, 2f, 3.0001f), "!= beyond tolerance");
            Check(f, Vector3.Distance(new Vector3(0, 0, 0), new Vector3(3, 4, 0)) == 5f, "Distance 3-4-5");
            Check(f, Vector3.Lerp(Vector3.zero, Vector3.one, 2f) == Vector3.one, "Lerp clamps t");
            Check(f, Vector3.Cross(Vector3.right, Vector3.up) == Vector3.forward, "Cross(right, up) = forward");
            Check(f, Mathf.Abs(Vector3.Angle(Vector3.right, Vector3.forward) - 90f) < 1e-4f, "Angle 90");
            Check(f, Vector3.MoveTowards(Vector3.zero, new Vector3(10, 0, 0), 3f).x == 3f, "MoveTowards");
            Check(f, Vector3.ClampMagnitude(new Vector3(0, 0, 10), 2f).z == 2f, "ClampMagnitude");
            Check(f, Mathf.Sign(0f) == 1f && Mathf.Sign(-0.5f) == -1f, "Sign(0) = 1");
            Check(f, Mathf.RoundToInt(2.5f) == 2 && Mathf.RoundToInt(3.5f) == 4 && Mathf.RoundToInt(-2.5f) == -2, "RoundToInt banker's");
            Check(f, Mathf.Round(0.5f) == 0f && Mathf.Round(1.5f) == 2f, "Round banker's");
            Check(f, Mathf.FloorToInt(-0.5f) == -1 && Mathf.CeilToInt(-0.5f) == 0 && Mathf.CeilToInt(1.01f) == 2, "Floor/CeilToInt");
            Check(f, Mathf.Clamp01(1.5f) == 1f && Mathf.Clamp(5, 0, 3) == 3, "Clamp");
            Check(f, Mathf.Lerp(0f, 10f, -1f) == 0f && Mathf.Lerp(0f, 10f, 0.25f) == 2.5f, "Lerp clamped");
            Check(f, Mathf.Repeat(-1f, 3f) == 2f && Mathf.Repeat(7f, 3f) == 1f, "Repeat");
            Check(f, Mathf.Approximately(1f, 1f + 1e-7f) && !Mathf.Approximately(1f, 1.0001f), "Approximately");
            Check(f, Mathf.Epsilon == 1.401298E-45f && Mathf.Max(1f, 3f, 2f) == 3f && Mathf.Min(4, 2) == 2, "Epsilon / params Max");
            Check(f, Mathf.MoveTowards(0f, 1f, 0.3f) == 0.3f && Mathf.DeltaAngle(10f, 350f) == -20f, "MoveTowards / DeltaAngle");
            Check(f, Mathf.InverseLerp(2f, 2f, 5f) == 0f, "InverseLerp a==b");
            // Random: value in [0,1], int Range max exclusive, float Range inclusive
            SimContext.Cur = null; UnityEngine.Random.InitState(42);
            bool okR = true; for (int i = 0; i < 20000; i++) { float v = UnityEngine.Random.value; int r = UnityEngine.Random.Range(0, 3); float g = UnityEngine.Random.Range(2f, 3f); if (v < 0f || v > 1f || r < 0 || r > 2 || g < 2f || g > 3f) okR = false; }
            Check(f, okR, "Random ranges");
            Check(f, UnityEngine.Random.Range(5, 5) == 5, "Random.Range(n,n) = n");
        }

        // ---------------- stub-engine physics ----------------
        /// <summary>Minimal stand-in for the M3 engine so the host + bots can be exercised while DuelSim.Core is a stub.</summary>
        internal sealed class StubPhysics
        {
            private readonly DuelSim _s; private readonly SimBotHost _h;
            private int[] _pend, _pendAt, _next;
            public StubPhysics(DuelSim s, SimBotHost h) { _s = s; _h = h; }

            public static void Fill(DuelSim s, int n0, int n1, int scale)
            {
                s.Len = 100 * scale; s.Wid = 20 * scale; s.SafeLen = 15 * scale; s.NeutralLen = 20 * scale; s.MidLen = 30 * scale;
                int n = n0 + n1; s.Count = n;
                s.Side = new int[n]; s.X = new float[n]; s.Z = new float[n]; s.VX = new float[n]; s.VZ = new float[n]; s.FX = new float[n]; s.FZ = new float[n];
                s.Hp = new float[n]; s.MaxHp = new float[n]; s.Dead = new bool[n];
                s.Ord = new SimOrder[n]; s.OrdX = new float[n]; s.OrdZ = new float[n]; s.OrdTarget = new int[n]; s.OrdAt = new float[n];
                s.AtkTarget = new int[n]; s.NextAttackFrame = new int[n]; s.StrikeFrame = new int[n]; s.StrikeTarget = new int[n]; s.StrikeAt = new float[n]; s.LastStrikeAt = new float[n];
                for (int i = 0; i < n; i++)
                {
                    int side = i < n0 ? 0 : 1, k = side == 0 ? i : i - n0, cnt = side == 0 ? n0 : n1;
                    int rows = Math.Max(1, Math.Min(cnt, (int)s.Wid - 4)), col = k / rows, row = k % rows, inCol = Math.Min(rows, cnt - col * rows);
                    s.Side[i] = side;
                    s.X[i] = side == 0 ? 2.5f + col * 1.5f : s.Len - 2.5f - col * 1.5f;
                    s.Z[i] = s.Wid * 0.5f + (row - (inCol - 1) * 0.5f) * 1.2f;
                    s.FX[i] = side == 0 ? 1f : -1f;
                    s.Hp[i] = s.MaxHp[i] = 500f; s.OrdTarget[i] = -1; s.AtkTarget[i] = -1; s.StrikeFrame[i] = -1; s.StrikeTarget[i] = -1;
                }
                s.Winner = -2;
            }

            public void Attach()
            {
                int n = _s.Count; _pend = new int[n]; _pendAt = new int[n]; _next = new int[n];
                for (int i = 0; i < n; i++) _pend[i] = -1;
                _h.OrderIssued = OnOrder; _h.AfterStep = Step;
            }

            private void OnOrder(int u, SimOrder k, float x, float z, int t)
            {
                _s.Ord[u] = k; _s.OrdX[u] = x; _s.OrdZ[u] = z; _s.OrdTarget[u] = t;
                if (_pend[u] >= 0 && _s.Frame - _pendAt[u] < 9) _pend[u] = -1;     // early order cancels the swing
                if (_s.Rules.Skips) _next[u] = 0;                                      // fresh AttackInfo
            }

            private void Step()
            {
                DuelSim s = _s; int f = s.Frame; float dt = DuelSim.FixedDt;
                for (int i = 0; i < s.Count; i++)
                {
                    if (s.Dead[i]) continue;
                    if (_pend[i] >= 0 && f - _pendAt[i] >= 23)
                    {
                        int v = _pend[i]; _pend[i] = -1;
                        if (!s.Dead[v])
                        {
                            s.Hp[v] = Math.Max(0f, s.Hp[v] - 32f);
                            _h.OnHit(i, v, 32f, s.Hp[v]);
                            if (s.Hp[v] <= 0f) { s.Dead[v] = true; _h.OnKill(v, i); }
                        }
                    }
                    int t = -1;
                    if (s.Ord[i] == SimOrder.Attack) { t = s.OrdTarget[i]; if (t >= 0 && s.Dead[t]) { s.Ord[i] = SimOrder.None; t = -1; } }
                    else if (s.Ord[i] == SimOrder.MoveAttack)
                    {
                        float bd = 81f;
                        for (int j = 0; j < s.Count; j++) { if (s.Dead[j] || s.Side[j] == s.Side[i]) continue; float dx = s.X[j] - s.X[i], dz = s.Z[j] - s.Z[i], d = dx * dx + dz * dz; if (d < bd) { bd = d; t = j; } }
                    }
                    float gx = s.OrdX[i], gz = s.OrdZ[i];
                    if (t >= 0)
                    {
                        float dx = s.X[t] - s.X[i], dz = s.Z[t] - s.Z[i];
                        if (dx * dx + dz * dz <= 2.6f * 2.6f)
                        {
                            s.VX[i] = s.VZ[i] = 0f;
                            if (_pend[i] < 0 && f >= _next[i]) { _pend[i] = t; _pendAt[i] = f; _next[i] = f + 75; _h.OnStrike(i, t); }
                            continue;
                        }
                        gx = s.X[t]; gz = s.Z[t];
                    }
                    if (s.Ord[i] == SimOrder.None || s.Ord[i] == SimOrder.Stop) { s.VX[i] = s.VZ[i] = 0f; continue; }
                    float ex = gx - s.X[i], ez = gz - s.Z[i], dd = (float)Math.Sqrt(ex * ex + ez * ez);
                    if (dd < 0.3f) { s.VX[i] = s.VZ[i] = 0f; if (s.Ord[i] == SimOrder.Move) s.Ord[i] = SimOrder.None; continue; }
                    float step = Math.Min(dd, 6f * dt);
                    s.X[i] = Math.Clamp(s.X[i] + ex / dd * step, 0.3f, s.Len - 0.3f); s.Z[i] = Math.Clamp(s.Z[i] + ez / dd * step, 0.3f, s.Wid - 0.3f);
                    s.VX[i] = ex / dd * 6f; s.VZ[i] = ez / dd * 6f; s.FX[i] = ex / dd; s.FZ[i] = ez / dd;
                }
                int a0 = 0, a1 = 0;
                for (int i = 0; i < s.Count; i++) if (!s.Dead[i]) { if (s.Side[i] == 0) a0++; else a1++; }
                if (s.Winner == -2 && (a0 == 0 || a1 == 0)) { s.Winner = a0 == 0 && a1 == 0 ? -1 : (a0 == 0 ? 1 : 0); s.Reason = "all dead"; _h.OnEnd(s.Winner, s.Reason); }
                else if (s.Winner == -2 && s.Time > s.Rules.TimeLimit) { s.Winner = -1; s.Reason = "time"; _h.OnEnd(-1, "time"); }
            }
        }

        /// <summary>Starts a match on a fresh sim; uses StubPhysics when the engine is still a stub (Count stays 0 after Reset).</summary>
        internal static SimBotHost NewMatch(int n, ulong seed, SimSideSpec side0, SimSideSpec side1, float timeLimit, out bool stub)
        {
            DuelSim sim = new DuelSim();
            SimRules r = new SimRules { UnitsPerSide = n, TimeLimit = timeLimit };
            SimBotHost h = new SimBotHost(sim);
            sim.Listener = h;
            sim.Reset(r, seed);
            stub = sim.Count == 0;
            if (stub) StubPhysics.Fill(sim, n, n, 1);
            h.BeginFromSim(seed, new SimBotSetup { Side0 = side0, Side1 = side1 });
            if (stub) new StubPhysics(sim, h).Attach();
            return h;
        }

        private static void RunFor(SimBotHost h, float seconds) { int frames = (int)(seconds / h.Sim.RenderDt); for (int i = 0; i < frames && !h.Sim.Over; i++) h.Frame(); }

        // ---------------- smoke ----------------
        private static void Smoke(List<string> f, Action<string> L)
        {
            int[] kinds = { SimSideSpec.KindProd, SimSideSpec.KindV1, SimSideSpec.KindV2, SimSideSpec.KindX, 3, 4, 5, 6, 7, SimSideSpec.Idle };
            for (int k = 0; k < kinds.Length; k++)
                for (int tier = 2; tier <= 4; tier += 2)
                {
                    bool stub;
                    SimSideSpec s0 = kinds[k] == SimSideSpec.Idle ? new SimSideSpec() : SimSideSpec.AutoPlayer(1, kinds[k], tier);
                    SimBotHost h = NewMatch(5, (ulong)(100 + k), s0, SimSideSpec.Production(tier), 120f, out stub);
                    RunFor(h, 120f);
                    L("smoke kind=" + kinds[k] + " tier=" + tier + " stub=" + stub + " t=" + h.FightTime.ToString("0.0") + " winner=" + h.Sim.Winner + " orders=" + h.OrderCount + " errors=" + h.BotErrors);
                    if (h.BotErrors > 0) f.Add("smoke kind " + kinds[k] + " tier " + tier + ": bot error " + h.LastBotError);
                    if (h.BotOrderCount == 0) f.Add("smoke kind " + kinds[k] + " tier " + tier + ": no bot orders");
                }
        }

        // ---------------- determinism ----------------
        private static ulong OneRun(ulong seed, out int orders)
        {
            bool stub;
            SimBotHost h = NewMatch(5, seed, SimSideSpec.AutoPlayer(1, SimSideSpec.KindProd, 3), SimSideSpec.Production(4), 90f, out stub);
            RunFor(h, 90f);
            orders = h.OrderCount;
            return h.OrderHash ^ h.Sim.StateHash();
        }

        private static void Determinism(List<string> f, Action<string> L)
        {
            int o1, o2, o3;
            ulong a = OneRun(7, out o1), b = OneRun(7, out o2), c = OneRun(8, out o3);
            if (a != b) f.Add("determinism: same seed gave different order hashes");
            L("determinism seed7=" + a.ToString("X16") + " (" + o1 + " orders) seed8=" + c.ToString("X16") + " (" + o3 + ") differs=" + (a != c));
            // two matches interleaved on one thread must equal the lone runs
            bool s1, s2;
            SimBotHost h1 = NewMatch(5, 7, SimSideSpec.AutoPlayer(1, SimSideSpec.KindProd, 3), SimSideSpec.Production(4), 90f, out s1);
            SimBotHost h2 = NewMatch(5, 8, SimSideSpec.AutoPlayer(1, SimSideSpec.KindProd, 3), SimSideSpec.Production(4), 90f, out s2);
            int fr = (int)(90f / h1.Sim.RenderDt);
            for (int i = 0; i < fr; i++) { if (!h1.Sim.Over) h1.Frame(); if (!h2.Sim.Over) h2.Frame(); }
            if ((h1.OrderHash ^ h1.Sim.StateHash()) != a || (h2.OrderHash ^ h2.Sim.StateHash()) != c) f.Add("determinism: interleaved matches differ from lone runs (static leak between matches)");
            // and on other threads
            ulong t1 = 0, t2 = 0; int x;
            Thread th1 = new Thread(() => { t1 = OneRun(7, out x); }), th2 = new Thread(() => { int y; t2 = OneRun(8, out y); });
            th1.Start(); th2.Start(); th1.Join(); th2.Join();
            if (t1 != a || t2 != c) f.Add("determinism: threaded runs differ from the main-thread runs");
        }

        // ---------------- perf ----------------
        /// <summary>Bot tick cost: n v n, both sides production bots at the given tier, 60 s fight. Returns ns per Tick call and bytes per call.</summary>
        public static string Bench(Action<string> L, int n = 10, int tier = 4, bool log = false)
        {
            bool stub;
            SimBotHost w = NewMatch(n, 1, SimSideSpec.AutoPlayer(1, SimSideSpec.KindProd, tier), SimSideSpec.Production(tier), 60f, out stub);
            RunFor(w, 20f);     // warm-up (JIT tiers)
            SimBotHost h = NewMatch(n, 2, SimSideSpec.AutoPlayer(1, SimSideSpec.KindProd, tier), SimSideSpec.Production(tier), 60f, out stub);
            if (log) h.Sim.Listener = h;
            h.Profile = true;
            long b0 = GC.GetAllocatedBytesForCurrentThread();
            long t0 = Stopwatch.GetTimestamp();
            RunFor(h, 60f);
            long wall = Stopwatch.GetTimestamp() - t0;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - b0;
            double nsPerTick = h.ProfCalls > 0 ? h.ProfTicks * 1e9 / Stopwatch.Frequency / h.ProfCalls : 0;
            string r = "bench " + n + "v" + n + " tier " + tier + " stub=" + stub + ": " + h.ProfCalls + " bot ticks, " + nsPerTick.ToString("0") + " ns/tick, " +
                       (h.ProfCalls > 0 ? (bytes / (double)h.ProfCalls).ToString("0") : "0") + " B/tick (whole loop alloc " + bytes + " B), frames " + h.RenderFrames +
                       ", wall " + (wall * 1000.0 / Stopwatch.Frequency).ToString("0.0") + " ms, orders " + h.OrderCount + ", winner " + h.Sim.Winner + " at " + h.FightTime.ToString("0.0") + " s";
            L(r);
            return r;
        }
    }
}
