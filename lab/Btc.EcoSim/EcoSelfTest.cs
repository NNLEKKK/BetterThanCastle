using System;
using System.Collections.Generic;
using System.IO;
using Btc.EcoShared;

namespace Btc.EcoSim
{
    public static class EcoSelfTest
    {
        public static int Run(Action<string> log)
        {
            int fails = 0;
            Action<bool, string> check = (ok, name) => { if (!ok) fails++; log((ok ? "PASS " : "FAIL ") + name); };

            check((int)EcoCmd.Count == EcoCmdNames.Names.Length, "EcoCmd count matches name table (" + ((int)EcoCmd.Count - 1) + " commands)");
            bool allParse = true;
            for (int i = 1; i < (int)EcoCmd.Count; i++) { EcoCmd c; if (!EcoCmdNames.TryParse(EcoCmdNames.Names[i], out c) || (int)c != i) allParse = false; }
            check(allParse, "every command name round-trips through TryParse");
            var cmd = new EcoCommand { Cmd = EcoCmd.Pickup, Actor = 12, Target = 34, X = -5, Y = 7, Z = 900, P0 = 8, P3 = -1, F0 = 1.5f, F1 = -0.25f };
            string line = EcoCommandText.FormatLine(12.345f, cmd);
            float t; EcoCommand back;
            check(EcoCommandText.TryParseLine(line, out t, out back) && Math.Abs(t - 12.345f) < 1e-4f
                  && back.Cmd == cmd.Cmd && back.Actor == 12 && back.Target == 34 && back.X == -5 && back.Y == 7 && back.Z == 900
                  && back.P0 == 8 && back.P3 == -1 && back.F0 == 1.5f && back.F1 == -0.25f, "CMD line format/parse round-trip: " + line);
            check(!EcoCommandText.TryParseLine("1.000|TASK|5|start", out t, out back), "non-CMD line ignored");
            check(EcoCommandText.TryParseLine("2.000|CMD|CmdStop|7", out t, out back) && back.Cmd == EcoCmd.Stop && back.Actor == 7, "short CMD line (optional trailing fields)");

            check(EcoParams.Planks.Work == 3 && EcoParams.Planks.QtyCreated == 3, "[code] Planks work 3 -> 3 planks");
            check(EcoParams.IronIngot.Heat == 2 && EcoParams.IronIngot.InQty[0] == 10, "[code] IronIngot heat 2, 10 raw iron");
            check(EcoParams.CaptureWard.InQty[0] == 40 && EcoParams.CaptureWard.Work == 10, "[code] CaptureWard 40 orange crystal");
            check((int)EcoBias.Locked == -10 && (int)EcoBias.High == 8, "[code] Bias values");
            check(GameplaySnapshot.Check(GameplaySnapshot.Expected + ";gen.maxp=8") == null, "GameplaySnapshot accepts expected");
            check(GameplaySnapshot.Check(GameplaySnapshot.Expected.Replace("bai.hold=5", "bai.hold=3")) != null, "GameplaySnapshot rejects changed value");
            check(GameplaySnapshot.Check("") != null, "GameplaySnapshot rejects empty");

            string dir = Path.Combine(Path.GetTempPath(), "ecolab-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string p = Path.Combine(dir, "eco-map.bin");
                EcoSyntheticMap.Write(p, 7);
                EcoWorld w = EcoMapLoader.Load(p);
                check(w.SizeX == 32 && w.SizeY == 24 && w.SizeZ == 32 && w.OriginX == 100 && w.OriginY == -4 && w.OriginZ == 200, "map header");
                check(w.Trees.Count == 12 && w.Nature.Count == 1 && w.Entities.Count == 7 && w.Blueprints.Count == 1 && w.Recipes.Count == 1, "map meta counts");
                check(w.BlockKeys.Count == 2 && w.BlockKeys[1] == "Wood_DoublePlank_AA" && w.GetBlock(5, 7, 5) == 1, "block table + block voxel");
                check(GameplaySnapshot.Check(w.Gameplay) == null, "map gp snapshot ok");
                int top = w.Top(10, 10);
                check(top >= 0 && w.Solid(10, top, 10) && !w.Solid(10, top + 1, 10), "column top cache");
                var w2 = EcoMapLoader.Load(p);
                check(w.Hash() == w2.Hash(), "load is deterministic");
                string p2 = Path.Combine(dir, "again.bin");
                EcoMapLoader.Write(p2, w.OriginX, w.OriginY, w.OriginZ, w.SizeX, w.SizeY, w.SizeZ, w.Terrain, w.Block, new string[] { "gp|" + w.Gameplay });
                check(EcoMapLoader.Load(p2).Hash() == w.Hash(), "terrain/block write-load round-trip");

                ulong h1 = Sim(w, 5), h2 = Sim(EcoMapLoader.Load(p), 5);
                check(h1 == h2, "same seed -> same StateHash");
                bool refused = false;
                try { var bad = EcoMapLoader.Load(p); bad.Gameplay = "mov.simple=0"; new EcoSimCore(bad); } catch (InvalidOperationException) { refused = true; }
                check(refused, "sim refuses wrong gameplay snapshot");
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
            fails += EcoSelfTest2.Run(log);
            log(fails == 0 ? "SELFTEST OK" : "SELFTEST FAILED (" + fails + ")");
            return fails;
        }

        static ulong Sim(EcoWorld w, ulong seed)
        {
            var s = new EcoSimCore(w);
            s.Reset(seed); s.StartRun();
            for (int i = 0; i < 100; i++) s.Step(0.1f);
            return s.StateHash();
        }
    }
}
