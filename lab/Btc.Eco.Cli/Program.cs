using System.Globalization;
using Btc.EcoShared;
using Btc.EcoSim;

static class Program
{
    static int Main(string[] a)
    {
        if (a.Length == 0) return Usage();
        switch (a[0])
        {
            case "selftest": return EcoSelfTest.Run(Console.WriteLine) == 0 ? 0 : 1;
            case "map-info": return a.Length < 2 ? Usage() : MapInfo(a[1]);
            case "column": { var w = EcoMapLoader.Load(a[1]); int wx = int.Parse(a[2]), wz = int.Parse(a[3]); int x = wx - w.OriginX, z = wz - w.OriginZ; Console.WriteLine("top=" + w.Top(x, z) + " (world y " + (w.Top(x, z) + w.OriginY) + ")"); int t = w.Top(x, z); for (int y = t + 3; y >= t - 3; y--) Console.WriteLine(y + ": " + w.GetTerrain(x, y, z)); foreach (var tr in w.Trees) if (Math.Abs(tr.X - wx) <= 12 && Math.Abs(tr.Z - wz) <= 12) Console.WriteLine("tree " + tr.X + "," + tr.Y + "," + tr.Z); return 0; }
            case "run-script": return RunScript(a);
            case "bench": return Bench(a);
            case "cmd-parse": return a.Length < 2 ? Usage() : CmdParse(a[1]);
            default: return Usage();
        }
    }

    static int Usage()
    {
        Console.WriteLine("ecolab selftest | map-info <eco-map.bin> | cmd-parse <trace.txt>");
        return 2;
    }

    static int MapInfo(string path)
    {
        EcoWorld w;
        try { w = EcoMapLoader.Load(path); } catch (Exception e) { Console.WriteLine("load failed: " + e.Message); return 1; }
        Console.WriteLine($"origin {w.OriginX},{w.OriginY},{w.OriginZ} size {w.SizeX}x{w.SizeY}x{w.SizeZ} ({(long)w.SizeX * w.SizeY * w.SizeZ} voxels)");
        string err = GameplaySnapshot.Check(w.Gameplay);
        Console.WriteLine("gameplay: " + (err == null ? "OK" : "MISMATCH " + err));
        var h = w.TerrainHistogram();
        Console.Write("terrain:");
        for (int i = 0; i < 256; i++) if (h[i] > 0) Console.Write($" {i}={h[i]}");
        Console.WriteLine();
        int blocks = 0; foreach (ushort b in w.Block) if (b != 0) blocks++;
        Console.WriteLine($"blocks placed {blocks}, block keys {w.BlockKeys.Count - 1}");
        Console.WriteLine($"trees {w.Trees.Count}, nature {w.Nature.Count}");
        { var nt = new SortedDictionary<int, int>(); int ch = 0; foreach (var n in w.Nature) { nt.TryGetValue(n.Type, out int c0); nt[n.Type] = c0 + 1; ch += n.Charges; } Console.Write("nature types (1 plant, 2 boulder, 4 iron, 8 pyrite):"); foreach (var kv in nt) Console.Write($" {kv.Key}={kv.Value}"); Console.WriteLine($"; charges field sum {ch}"); }
        Console.WriteLine($"ore voxels (raw dump): {(w.Param.TryGetValue("oreVoxels", out var ov) ? ov : "param oreVoxels missing, converted on load: " + w.OreVoxelsConverted)}; after ore->stone: iron {h[EcoTerrain.Iron]}, orange crystal {h[EcoTerrain.OrangeCrystal]}, blue crystal {h[EcoTerrain.BlueCrystal]}");
        var kinds = new SortedDictionary<string, int>();
        foreach (var e in w.Entities) { kinds.TryGetValue(e.Kind, out int c); kinds[e.Kind] = c + 1; }
        Console.Write($"entities {w.Entities.Count}:");
        foreach (var kv in kinds) Console.Write($" {kv.Key}={kv.Value}");
        Console.WriteLine();
        Console.WriteLine($"blueprints {w.Blueprints.Count}, recipes {w.Recipes.Count}, capacities {w.Capacity.Count}, params {w.Param.Count}");
        return 0;
    }

    static string Opt(string[] a, string name, string def) { for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1]; return def; }

    static EcoWorld LoadForRun(string[] a, out int injected)
    {
        var w = EcoMapLoader.Load(a[1]);
        injected = 0;
        int synth = int.Parse(Opt(a, "--synth-nature", "40"));
        if (w.Nature.Count == 0 && synth > 0) injected = EcoSyntheticNature.Inject(w, synth, synth, 1, int.Parse(Opt(a, "--charges", "5")));
        double scale = double.Parse(Opt(a, "--ore-scale", "1"), System.Globalization.CultureInfo.InvariantCulture);
        if (scale != 1) for (int i = 0; i < w.Nature.Count; i++) { var n = w.Nature[i]; if (n.Type == 4 || n.Type == 8) { if (n.Charges == 0) n.Charges = 24; n.Charges = (int)Math.Round(n.Charges * scale); w.Nature[i] = n; } }
        return w;
    }

    static int RunScript(string[] a)
    {
        if (a.Length < 2) return Usage();
        int inj; var w = LoadForRun(a, out inj);
        ulong seed = ulong.Parse(Opt(a, "--seed", "1"));
        var sim = new EcoSimCore(w);
        sim.Reset(seed);
        var sc = new EcoScript(sim);
        sc.Setup();
        if (Opt(a, "--dbg", "0") == "1") { for (int u = 0; u < 6; u++) { int ex, ey, ez; int c = -2; for (int e = 1; e < sim.NEnt; e++) if (sim.EK[e] == (byte)EcoKind.Blueprint) { c = sim.Nav.Path(sim.UX[u], sim.UY[u], sim.UZ[u], sim.EX[e], sim.EY[e], sim.EZ[e], 1, -1, 1, out ex, out ey, out ez); Console.WriteLine($"u{u} cell {sim.UX[u]},{sim.UY[u]},{sim.UZ[u]} -> bp{e} ({sim.EX[e]},{sim.EY[e]},{sim.EZ[e]}) cost {c} standable {sim.Nav.Standable(sim.UX[u], sim.UY[u], sim.UZ[u])}"); } } }
        if (Opt(a, "--dbg", "0") == "1") { for (int dz = -6; dz <= 6; dz++) { var sb = new System.Text.StringBuilder(); for (int dx = -8; dx <= 8; dx++) { int x = sim.UX[1] + dx, z = sim.UZ[1] + dz; int top = w.Top(x, z); char ch = (dx == 0 && dz == 0) ? (char)64 : sim.Nav.Standable(x, sim.UY[1], z) ? (char)46 : sim.Nav.Standable(x, sim.UY[1] + 1, z) ? (char)94 : sim.Nav.Standable(x, sim.UY[1] - 1, z) ? (char)118 : (char)35; sb.Append(ch); } Console.WriteLine(sb.ToString()); } }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ph = sc.Run(EcoSimCore.TmaxTicks);
        sw.Stop();
        Console.WriteLine($"map {w.SizeX}x{w.SizeY}x{w.SizeZ} trees {w.Trees.Count} nature {w.Nature.Count} (synthetic added {inj}), ores->stone {w.OreVoxelsConverted}, workers added {w.WorkersAdded}");
        Console.WriteLine($"result {ph} at t={sim.Time:0.0}s  kits {sim.Kits}/8 wards {sim.WardsMade}/2  orderFails {sim.OrderFails} orders {sim.OrdersIssued} dispatches {sc.Dispatches}");
        for (int i = 0; i < EcoSimCore.MCount; i++) Console.WriteLine($"  {EcoSimCore.MileName[i],-14} " + (sim.MileTick[i] < 0 ? "-" : (sim.MileTick[i] / 30.0).ToString("0.0") + "s"));
        if (Opt(a, "--dbg", "0") == "2") Console.Write(sc.Dump());
        Console.Write("counters:"); for (int i = 0; i < EcoSimCore.CCount; i++) Console.Write($" {EcoSimCore.CounterName[i]}={sim.Cur[i]}"); Console.WriteLine();
        if (sc.Log.Length > 0) Console.WriteLine("script log: " + sc.Log);
        Console.Write("fails by op:"); for (int i = 0; i < 32; i++) if (sim.FailOp[i] > 0) Console.Write($" {(EcoOp)i}={sim.FailOp[i]}"); Console.WriteLine();
        Console.WriteLine($"wall {sw.ElapsedMilliseconds} ms -> {sim.Time / Math.Max(1e-3, sw.Elapsed.TotalSeconds):0}x realtime, nav expansions {sim.Nav.Expanded}");
        return ph == EcoPhase.Won ? 0 : 3;
    }

    static int Bench(string[] a)
    {
        if (a.Length < 2) return Usage();
        int inj; var w = LoadForRun(a, out inj);
        int n = int.Parse(Opt(a, "--n", "10"));
        var sim = new EcoSimCore(w);
        double simSec = 0; int won = 0; var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < n; i++)
        {
            sim.Reset((ulong)(i + 1));
            var sc = new EcoScript(sim); sc.Setup(); var ph = sc.Run(EcoSimCore.TmaxTicks);
            simSec += sim.Time; if (ph == EcoPhase.Won) won++;
        }
        sw.Stop();
        Console.WriteLine($"{n} episodes, won {won}, sim {simSec:0}s in wall {sw.Elapsed.TotalSeconds:0.00}s -> {simSec / sw.Elapsed.TotalSeconds:0}x realtime per thread (incl. script + Reset)");
        return 0;
    }

    static int CmdParse(string path)
    {
        var counts = new int[(int)EcoCmd.Count];
        int total = 0, bad = 0, lines = 0;
        float last = 0;
        foreach (string line in File.ReadLines(path))
        {
            lines++;
            int a = line.IndexOf('|');
            if (a < 0 || line.Length < a + 5 || line.Substring(a + 1, 3) != "CMD" || line[a + 4] != '|') continue;
            if (EcoCommandText.TryParseLine(line, out float t, out EcoCommand c)) { counts[(int)c.Cmd]++; total++; last = t; }
            else { bad++; if (bad <= 5) Console.WriteLine("unparsed: " + line); }
        }
        Console.WriteLine($"lines {lines}, CMD parsed {total}, unparsed {bad}, last t {last.ToString("0.000", CultureInfo.InvariantCulture)}");
        for (int i = 1; i < counts.Length; i++) if (counts[i] > 0) Console.WriteLine($"  {EcoCmdNames.Names[i]} {counts[i]}");
        return bad == 0 ? 0 : 1;
    }
}
