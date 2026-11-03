using System.Diagnostics;
using Btc.Gpu;
using Btc.RL;
using Btc.RL.Duel;
using Btc.Sim;

namespace Btc.Lab.Cli;

static partial class DuelCommands
{
    /// <summary>gpu-check [--envs 64 --units 6 --updates 3 --threads T]: two identical trainers (same seed, same rollouts), one PPO update
    /// path on the CPU, one on the GPU backend; prints the parameter drift between them and both update times.</summary>
    public static int GpuCheck(Args a)
    {
        int envs = a.Int("envs", 64), updates = a.Int("updates", 3), threads = a.Int("threads", Environment.ProcessorCount);
        string units = a.Str("units", "6"), ckpt = a.Str("ckpt", "");
        float dec = (float)a.Dbl("dec", 0.2);
        bool resetEach = a.Int("reset", 0) != 0;   // recreate the GPU backend before every update (tests the TDR recovery path)
        bool par = a.Int("parallel", 0) != 0;   // a second GPU trainer updates concurrently on another thread (main + exploiter setup)
        foreach (var kv in a.Unused) throw new ArgumentException("unknown option --" + kv.Key);
        PpoTrainer Make()
        {
            var cfg = new PpoConfig { NumEnvs = envs, Steps = 128, Threads = threads, Hidden = "256,256", CriticHidden = "256,256", Seed = 5 };
            var cur = new DuelCurriculum { DomainRand = 0f, Shaping = 1f, TimeLimit = 90f, DecisionSec = dec };
            cur.SetUnits(units); cur.SetArena("1");
            var def = DuelOpponents.Get("prod2");
            var env = new SimDuelEnv(envs, cur, new[] { def });
            return new PpoTrainer(env, new[] { def.Spec }, cfg);
        }
        var cpu = Make(); var gpu = Make();
        if (ckpt != "") { cpu.LoadCheckpoint(ckpt); gpu.LoadCheckpoint(ckpt); Console.WriteLine("loaded " + ckpt); }
        using var be = new GpuPpo();
        gpu.Gpu = be;
        Console.WriteLine("gpu: " + be.Name);
        PpoTrainer gpu2 = null; GpuPpo be2 = null;
        if (par) { gpu2 = Make(); be2 = new GpuPpo(); gpu2.Gpu = be2; if (ckpt != "") gpu2.LoadCheckpoint(ckpt); }
        for (int u = 0; u < updates; u++)
        {
            if (resetEach && u > 0) be.Reset();
            var sc = cpu.Update();
            Task<UpdateStats> t2 = par ? Task.Run(() => gpu2.Update()) : null;
            var sg = gpu.Update();
            if (t2 != null) { var s2 = t2.Result; Console.WriteLine(FormattableString.Invariant($"   parallel trainer: kl {s2.Kl:F5} ent {s2.Entropy:F4} mb {s2.MinibatchesDone}")); }
            double maxA = 0, maxC = 0, normA = 0;
            for (int i = 0; i < cpu.Actor.P.Length; i++) { maxA = Math.Max(maxA, Math.Abs(cpu.Actor.P[i] - gpu.Actor.P[i])); normA = Math.Max(normA, Math.Abs(cpu.Actor.P[i])); }
            for (int i = 0; i < cpu.Critic.P.Length; i++) maxC = Math.Max(maxC, Math.Abs(cpu.Critic.P[i] - gpu.Critic.P[i]));
            Console.WriteLine(FormattableString.Invariant(
                $"u{u + 1}: samples {sc.Samples}/{sg.Samples} mb {sc.MinibatchesDone}/{sg.MinibatchesDone} | train cpu {sc.TrainSec:F2}s gpu {sg.TrainSec:F2}s (mb {sg.TMinibatch:F2} adam {sg.TAdam:F2}) | kl {sc.Kl:F5}/{sg.Kl:F5} ent {sc.Entropy:F4}/{sg.Entropy:F4} vl {sc.VLoss:F5}/{sg.VLoss:F5} | max |dP| actor {maxA:E1} critic {maxC:E1}"));
            Console.WriteLine("   gpu prof: " + be.Prof()); be.TUp = be.TFwd = be.TBwd = be.TDown = be.TBegin = be.TW = 0;
            // re-sync so the next update compares one step of drift, not the accumulated one
            Array.Copy(cpu.Actor.P, gpu.Actor.P, cpu.Actor.P.Length); Array.Copy(cpu.Critic.P, gpu.Critic.P, cpu.Critic.P.Length);
            foreach (var (s, d) in new[] { (cpu.ActorOpt, gpu.ActorOpt), (cpu.CriticOpt, gpu.CriticOpt) }) { Array.Copy(s.M, d.M, s.M.Length); Array.Copy(s.V, d.V, s.V.Length); d.T = s.T; }
        }
        return 0;
    }
}

static class ReorderLab
{
    /// <summary>Sim side of tests/scenarios/lab-reorder.txt: one unit at (20,10) runs to x=80 under an order stream; prints the
    /// distance covered in 3 s per sequence (compare with the game trace of the same scenario).</summary>
    public static int Run(int set = 1)
    {
        if (set == 2) return Run2();
        var seqs = new (string name, float dt, SimOrder k1, float z1, SimOrder k2, float z2)[]
        {
            ("MM01", 0.1f, SimOrder.Move, 10f, SimOrder.Move, 10.6f), ("MA01", 0.1f, SimOrder.Move, 10f, SimOrder.MoveAttack, 10.6f),
            ("AA01", 0.1f, SimOrder.MoveAttack, 10f, SimOrder.MoveAttack, 10.6f), ("MM02", 0.2f, SimOrder.Move, 10f, SimOrder.Move, 10.6f),
            ("MA02", 0.2f, SimOrder.Move, 10f, SimOrder.MoveAttack, 10.6f), ("MA03", 0.3f, SimOrder.Move, 10f, SimOrder.MoveAttack, 10.6f),
            ("ZZ01", 0.1f, SimOrder.Move, 6f, SimOrder.Move, 14f), ("ONE", 3f, SimOrder.Move, 10f, SimOrder.Move, 10f),
        };
        foreach (var s in seqs)
        {
            var sim = new DuelSim { P = SimParams.Default(), MechanicsEnabled = true };
            sim.Reset(new SimRules { UnitsPerSide = 1, UnitsSide1 = 1, ArenaScale = 1, Skips = true, TimeLimit = 100000f }, 1);
            sim.RulesEnabled = false;
            sim.SetUnitState(0, 20f, 10f, 500f, false); sim.SetUnitState(1, 98f, 18f, 500f, false);
            void Until(double t) { while (sim.Time < t) sim.Step(); }
            Until(1.5);
            float x0 = sim.X[0], z0 = sim.Z[0];
            int n = (int)(3f / s.dt);
            double t0 = sim.Time;
            for (int i = 1; i <= n; i++)
            {
                bool odd = (i & 1) == 1;
                sim.Order(0, odd ? s.k1 : s.k2, 80f, odd ? s.z1 : s.z2, -1);
                Until(t0 + i * s.dt);
            }
            Console.WriteLine(FormattableString.Invariant($"{s.name}: covered {MathF.Sqrt((sim.X[0] - x0) * (sim.X[0] - x0) + (sim.Z[0] - z0) * (sim.Z[0] - z0)):F1} m in 3 s (x {x0:F1} -> {sim.X[0]:F1})"));
        }
        return 0;
    }

    // tests/scenarios/lab-reorder2.txt: (name, dt, order a, order b); an order = (kind, x, z) or Attack on unit 1 (x = -1)
    static int Run2()
    {
        var seqs = new (string n, float dt, SimOrder ka, float xa, float za, SimOrder kb, float xb, float zb)[]
        {
            ("SH2", 0.1f, SimOrder.Move, 22, 10, SimOrder.Move, 18, 10), ("SH2S", 0.3f, SimOrder.Move, 22, 10, SimOrder.Move, 18, 10),
            ("SH5", 0.1f, SimOrder.Move, 25, 10, SimOrder.Move, 15, 10), ("FW2", 0.1f, SimOrder.Move, 22, 10, SimOrder.Move, 22, 10.5f),
            ("MIX", 0.1f, SimOrder.Move, 80, 10, SimOrder.Move, 22, 10), ("ATK", 0.1f, SimOrder.Move, 80, 10, SimOrder.Attack, -1, 0),
            ("ATK3", 0.3f, SimOrder.Move, 80, 10, SimOrder.Attack, -1, 0),
        };
        foreach (var s in seqs)
        {
            var sim = new DuelSim { P = SimParams.Default(), MechanicsEnabled = true };
            sim.Reset(new SimRules { UnitsPerSide = 1, UnitsSide1 = 1, ArenaScale = 1, Skips = true, TimeLimit = 100000f }, 1);
            sim.RulesEnabled = false;
            sim.SetUnitState(0, 20f, 10f, 500f, false); sim.SetUnitState(1, 95f, 10f, 500f, false);
            void Until(double t) { while (sim.Time < t) sim.Step(); }
            Until(1.5);
            float x0 = sim.X[0], z0 = sim.Z[0]; double path = 0; float px = x0, pz = z0;
            int n = (int)(3f / s.dt); double t0 = sim.Time;
            for (int i = 1; i <= n; i++)
            {
                bool odd = (i & 1) == 1;
                var k = odd ? s.ka : s.kb; float x = odd ? s.xa : s.xb, z = odd ? s.za : s.zb;
                if (k == SimOrder.Attack) sim.Order(0, SimOrder.Attack, sim.X[1], sim.Z[1], 1); else sim.Order(0, k, x, z, -1);
                double tt = t0 + i * s.dt;
                while (sim.Time < tt) { sim.Step(); path += MathF.Sqrt((sim.X[0] - px) * (sim.X[0] - px) + (sim.Z[0] - pz) * (sim.Z[0] - pz)); px = sim.X[0]; pz = sim.Z[0]; }
            }
            Console.WriteLine(FormattableString.Invariant($"{s.n}: path {path:F1} m, net {MathF.Sqrt((sim.X[0] - x0) * (sim.X[0] - x0) + (sim.Z[0] - z0) * (sim.Z[0] - z0)):F1} m in 3 s (sim)"));
        }
        return 0;
    }
}
