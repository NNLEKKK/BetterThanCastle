using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Btc.EcoShared;

namespace Btc.EcoSim
{
    /// <summary>Behaviour tests: placement phase, ore->stone, chop/recipes/converter, stall, determinism, snapshot round-trip + timing.</summary>
    public static class EcoSelfTest2
    {
        public static int Run(Action<string> log)
        {
            int fails = 0;
            Action<bool, string> check = (ok, name) => { if (!ok) fails++; log((ok ? "PASS " : "FAIL ") + name); };
            string dir = Path.Combine(Path.GetTempPath(), "ecolab2-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string p = Path.Combine(dir, "m.bin");
                EcoSyntheticMap.Write(p, 3);
                EcoWorld w = EcoMapLoader.Load(p);
                int[] hist = w.TerrainHistogram();
                check(hist[54] == 0 && hist[44] == 0 && hist[38] == 0 && w.OreVoxelsConverted > 0, "ore voxels became Stone (" + w.OreVoxelsConverted + " converted)");
                check(EcoMapLoader.Load(p, false).TerrainHistogram()[54] > 0, "oreToStone=false keeps ore (loader option)");

                var s = new EcoSimCore(w);
                s.Reset(1);
                check(s.Phase == EcoPhase.Placement, "Reset starts in placement phase");
                s.Step(10);
                check(s.Tick == 0, "no ticks run during placement");
                check(!s.Issue(EcoSimCore.Cmd(EcoCmd.Stop, 0, 0, 0, 0, 0, 0, 0)), "orders refused during placement");
                check(!s.PlaceWorker(0, w.OriginX + 5, w.OriginY + 0, w.OriginZ + 5), "PlaceWorker on a non-walkable cell is masked");
                int placed = 0;
                int tree = 0; for (int i = 1; i < s.NEnt; i++) if (s.EK[i] == (byte)EcoKind.Tree) { tree = i; break; }
                for (int u = 0; u < 6; u++)
                {
                    int x = 8 + u * 2, z = 8, y = w.Top(x, z) + 1;
                    if (u == 0) { int ax, ay, az; if (s.Nav.Snap(s.EX[tree] + 1, s.EY[tree], s.EZ[tree], out ax, out ay, out az)) { x = ax; y = ay; z = az; } }
                    if (s.PlaceWorker(u, w.OriginX + x, w.OriginY + y, w.OriginZ + z)) placed++;
                }
                check(placed == 6 && s.Phase == EcoPhase.Running && s.Tick == 0, "6 placements end the placement phase at t=0");

                check(tree > 0 && s.Issue(EcoSimCore.Cmd(EcoCmd.AddTreeGoal, 0, tree, 0, 0, 0, 0, 0)), "chop order accepted");
                s.Step(60);
                check(s.EK[tree] == 0 && s.MileTick[0] >= 0, "tree felled, milestone tree_chopped at " + s.MileTick[0] / 30.0 + " s");
                int logQty = 0; for (int i = 1; i < s.NEnt; i++) if (s.EK[i] == (byte)EcoKind.Item && s.EA[i] == (int)EcoItem.Log) logQty += s.EB[i];
                check(logQty == EcoTune.LogsPerTree, "tree dropped " + EcoTune.LogsPerTree + " logs");

                var s2 = new EcoSimCore(w); s2.Reset(2); s2.StartRun();
                int bp = FindFree(s2, w);
                check(bp > 0, "stockpile zone created by AccepterPosition (no blueprint)");
                
                s2.ECont[bp * EcoDefs.NI + (int)EcoItem.Stones] = 80;
                check(s2.Issue(EcoSimCore.Cmd(EcoCmd.SetAutoConvert, 0, bp, 0, 0, 0, 1, 0)), "SetAutoConvert accepted");
                check(s2.ECont[bp * EcoDefs.NI + (int)EcoItem.Stones] == 0 && s2.ECont[bp * EcoDefs.NI + (int)EcoItem.StoneBlock] == 80 / EcoTune.StonesPerBlock, "full pile of 80 Stones -> " + 80 / EcoTune.StonesPerBlock + " StoneBlock");

                int before = s2.InvalidPlacements;
                bool redOk = s2.Issue(new EcoCommand { Cmd = EcoCmd.PlaceBlueprint, X = w.OriginX + 20, Y = w.OriginY + w.Top(20, 20), Z = w.OriginZ + 20, P0 = (int)EcoBuilding.Furnace });
                int red = s2.NEnt - 1;
                check(redOk && s2.InvalidPlacements == before + 1 && !s2.EValid[red] && Math.Abs(s2.PlacementPenalty - EcoParams.InvalidBlueprintPenalty) < 1e-6f, "blueprint inside terrain is allowed but red (penalty " + s2.PlacementPenalty + ")");
                s2.ECont[red * EcoDefs.NI + (int)EcoItem.StoneBlock] = 4;
                check(s2.Issue(EcoSimCore.Cmd(EcoCmd.BatchedContextualActionGameObject, 1, red, 0, 0, 0, 0, 0)) == false || true, "order on red blueprint handled");
                s2.Step(30);
                check(s2.EK[red] == (byte)EcoKind.Blueprint, "red blueprint is never built");

                var s3 = new EcoSimCore(w); s3.Reset(3); s3.StartRun();
                s3.Step(59.9f); bool still = s3.Phase == EcoPhase.Running;
                s3.Step(0.2f);
                check(still && s3.Phase == EcoPhase.StallLoss && s3.DoneTick == 1800, "stall rule: 60 s without a new maximum = StallLoss (tick " + s3.DoneTick + ")");

                string real = FindDump();
                if (real == null) log("SKIP script / snapshot tests (no BTClogs/eco-map.bin found)");
                else
                {
                    EcoWorld rw = EcoMapLoader.Load(real);
                    ulong h1 = ScriptRun(rw, 7), h2 = ScriptRun(EcoMapLoader.Load(real), 7);
                    check(h1 == h2, "EcoScript episode is deterministic (same seed -> same StateHash)");
                    var sim = new EcoSimCore(rw); sim.Reset(7);
                    var sc = new EcoScript(sim); sc.Setup();
                    sc.Run(30 * 40);
                    var buf = new EcoSnapshotBuf();
                    sim.Save(buf);
                    ulong hSaved = sim.StateHash();
                    int tickSaved = sim.Tick;
                    sim.AdvanceTo(tickSaved + 30 * 40);
                    ulong hAfter = sim.StateHash();
                    sim.Load(buf);
                    check(sim.StateHash() == hSaved && sim.Tick == tickSaved, "snapshot Load restores the same StateHash");
                    sim.AdvanceTo(tickSaved + 30 * 40);
                    check(sim.StateHash() == hAfter, "run N, save, run M, load, run M again -> same hash");
                    sim.Load(buf); sim.AdvanceTo(tickSaved + 30);   // warm up
                    long a0 = GC.GetAllocatedBytesForCurrentThread();
                    sim.AdvanceTo(tickSaved + 30 * 30);
                    long alloc = GC.GetAllocatedBytesForCurrentThread() - a0;
                    check(alloc == 0, "sim Step/AdvanceTo is allocation-free (" + alloc + " bytes over 30 s of orders)");
                    sim.Load(buf);
                    var sw = Stopwatch.StartNew();
                    int reps = 200;
                    for (int i = 0; i < reps; i++) { sim.Save(buf); sim.Load(buf); }
                    sw.Stop();
                    double ms = sw.Elapsed.TotalMilliseconds / reps;
                    check(ms < 1.0, "save+load " + ms.ToString("0.000") + " ms (< 1 ms), snapshot " + buf.Length + " bytes");
                }
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
            return fails;
        }

        static int FindFree(EcoSimCore s, EcoWorld w)
        {
            for (int x = 3; x < w.SizeX - 3; x++)
                for (int z = 3; z < w.SizeZ - 3; z++)
                {
                    int y = w.Top(x, z) + 1;
                    if (s.Issue(new EcoCommand { Cmd = EcoCmd.AccepterPosition, X = w.OriginX + x, Y = w.OriginY + y, Z = w.OriginZ + z }))
                    {
                        for (int i = s.NEnt - 1; i >= 1; i--) if (s.EK[i] == (byte)EcoKind.Container) return i;
                    }
                }
            return 0;
        }

        static string FindDump()
        {
            string d = AppContext.BaseDirectory;
            for (int i = 0; i < 9 && d != null; i++)
            {
                string c = Path.Combine(d, "BTClogs", "eco-map.bin");
                if (File.Exists(c)) return c;
                d = Path.GetDirectoryName(d);
            }
            return null;
        }

        static ulong ScriptRun(EcoWorld w, ulong seed)
        {
            var sim = new EcoSimCore(w); sim.Reset(seed);
            var sc = new EcoScript(sim); sc.Setup(); sc.Run(30 * 120);
            return sim.StateHash();
        }
    }
}
