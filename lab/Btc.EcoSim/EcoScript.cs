using System;
using System.Collections.Generic;
using Btc.EcoShared;

namespace Btc.EcoSim
{
    /// <summary>
    /// Scripted manual-control expert (baseline + first fidelity probe). It only uses the public command grammar (Issue/Enqueue) and reads sim state;
    /// it never touches sim internals. Greedy dispatcher: whenever a unit is idle it (1) routes what it carries, (2) builds finished blueprints,
    /// (3) works / empties stations, (4) gathers (wood, stones, ore) under per-resource reservations. Not optimal; a stable lower bound.
    /// </summary>
    public sealed class EcoScript
    {
        readonly EcoSimCore s;
        readonly EcoWorld W;
        int ox, oy, oz;
        // placed buildings (entity ids are stable when a blueprint completes)
        int cb, pile, furnace, bench, altar; readonly int[] rack = new int[2];
        int baseX, baseZ;
        // reservations (cumulative)
        readonly int[] iSub = new int[EcoSimCore.NU];
        int stoneRes, ironRes, crysRes;
        public const int NeedLogs = 10, NeedStones = 50, NeedIron = 130, NeedCrystal = 150;   // altar: 40 crystal + 2 ingots now part of its cost
        // delivered by category (completed deliveries)
        const int CatIron = 0, CatFuel = 1, CatCryAltar = 2, CatIngBench = 3, CatIngAltar = 4, CatPlankBench = 5, CatN = 6;
        readonly int[] sent = new int[CatN];
        static readonly int[] CatTarget = { 130, 28, 80, 8, 2, 9 };
        // per-unit intent
        readonly int[] iKind = new int[EcoSimCore.NU];      // 0 none 1 wood 2 stone 3 ore 4 build 5 work 6 take 7 route
        readonly int[] iCat = new int[EcoSimCore.NU], iQty = new int[EcoSimCore.NU], iCarry0 = new int[EcoSimCore.NU], iDest = new int[EcoSimCore.NU];
        readonly bool[] usedTree, usedNat; readonly int[] natRes = new int[EcoSimCore.EC];
        readonly List<int> digList = new List<int>();   // grid voxel indices (x,y,z packed)
        int digNext;
        bool queuedFurnace, queuedBench, queuedAltar;
        public string Dump()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"res stone {stoneRes} iron {ironRes} cry {crysRes}; sent iron {sent[0]} fuel {sent[1]} cryAltar {sent[2]} ingBench {sent[3]} ingAltar {sent[4]} plankBench {sent[5]}; digNext {digNext}/{digList.Count}");
            for (int u = 0; u < EcoSimCore.NU; u++) sb.AppendLine($"u{u} st {s.UState[u]} intent {iKind[u]} carry {(EcoItem)s.UCarryItem[u]}x{s.UCarryQty[u]} at {s.UX[u]},{s.UY[u]},{s.UZ[u]}");
            foreach (int st in new int[] { pile, furnace, bench, altar, rack[0], rack[1] })
            {
                if (st <= 0) continue;
                sb.Append($"{(EcoKind)s.EK[st]} {st}:"); for (int i = 1; i < EcoDefs.NI; i++) if (s.ECont[st * EcoDefs.NI + i] > 0) sb.Append($" {(EcoItem)i}={s.ECont[st * EcoDefs.NI + i]}");
                if (s.EK[st] == (byte)EcoKind.Station) sb.Append($" active={s.SActive[st]} work={s.SWorkLeft[st]} heat={s.SHeatLeft[st]} q={s.SQLen[st]}");
                sb.AppendLine();
            }
            return sb.ToString();
        }
        public string Log = "";
        public int Dispatches;

        public EcoScript(EcoSimCore sim)
        {
            s = sim; W = sim.World; ox = W.OriginX; oy = W.OriginY; oz = W.OriginZ;
            usedTree = new bool[EcoSimCore.EC]; usedNat = new bool[EcoSimCore.EC];
        }

        int Wx(int gx) { return gx + ox; }
        int Wy(int gy) { return gy + oy; }
        int Wz(int gz) { return gz + oz; }

        bool Go(EcoCmd c, int u, int target, int gx, int gy, int gz, int p0, int p1, bool enq)
        {
            var cmd = EcoSimCore.Cmd(c, u, target, Wx(gx), Wy(gy), Wz(gz), p0, p1);
            return enq ? s.Enqueue(cmd) : s.Issue(cmd);
        }

        // =====================================================================================================
        // placement phase + blueprint layout
        // =====================================================================================================
        /// <summary>Runs the placement phase (6 workers near trees / dig site), then places all blueprints. Call once after sim.Reset().</summary>
        public void Setup()
        {
            // base = centroid of the default worker cells
            long sx = 0, sz = 0; for (int u = 0; u < EcoSimCore.NU; u++) { sx += s.UX[u]; sz += s.UZ[u]; }
            baseX = (int)(sx / EcoSimCore.NU); baseZ = (int)(sz / EcoSimCore.NU);
            // 7 building cells, spread >= 3 apart, nearest to base
            var cells = new List<int[]>();
            for (int r = 0; r <= 18 && cells.Count < 30; r++)
                for (int dx = -r; dx <= r && cells.Count < 30; dx++)
                    for (int dz = -r; dz <= r && cells.Count < 30; dz++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                        int x = baseX + dx, z = baseZ + dz;
                        int y; if (!ColStand(x, z, out y)) continue;
                        bool ok = true; foreach (var c in cells) if (Math.Max(Math.Abs(c[0] - x), Math.Abs(c[2] - z)) < 4) { ok = false; break; }
                        if (ok) cells.Add(new int[] { x, y, z });
                    }
            int[] want = { (int)EcoBuilding.ChoppingBlock, (int)EcoBuilding.Stockpile, (int)EcoBuilding.Furnace, (int)EcoBuilding.Workbench, (int)EcoBuilding.Altar, (int)EcoBuilding.WeaponRack, (int)EcoBuilding.WeaponRack };
            // workers: 3 next to the nearest trees, 3 at the base
            var trees = NearestTrees(baseX, baseZ, 3);
            // placement happens BEFORE blueprints (a unit standing in a blueprint cell is pushed anyway)
            for (int u = 0; u < EcoSimCore.NU; u++)
            {
                int x = s.UX[u], y = s.UY[u], z = s.UZ[u];
                if (u < 3 && u < trees.Count) { int t = trees[u]; int ax, ay, az; int ex2, ey2, ez2; if (s.Nav.Snap(s.EX[t] + 1, s.EY[t], s.EZ[t], out ax, out ay, out az) && s.Nav.Path(ax, ay, az, baseX, s.UY[u], baseZ, 6, -3, 3, out ex2, out ey2, out ez2) >= 0) { x = ax; y = ay; z = az; } }
                s.PlaceWorker(u, Wx(x), Wy(y), Wz(z));
            }
            if (s.Phase == EcoPhase.Placement) s.StartRun();
            int ri = 0;
            int ci = 0;
            // stockpile zone (CmdAccepterPosition) first
            for (; ci < cells.Count && pile == 0; ci++)
            {
                var c0 = cells[ci];
                if (s.Issue(new EcoCommand { Cmd = EcoCmd.AccepterPosition, X = Wx(c0[0]), Y = Wy(c0[1]), Z = Wz(c0[2]) }))
                    for (int i = s.NEnt - 1; i >= 1; i--) if (s.EK[i] == (byte)EcoKind.Container) { pile = i; break; }
            }
            for (int i = 0; i < want.Length; i++)
            {
                if (want[i] == (int)EcoBuilding.Stockpile) continue;
                bool placed = false; int[] c = null;
                while (ci < cells.Count && !placed) { c = cells[ci++]; if (s.CanPlace(c[0], c[1], c[2], want[i])) placed = s.Issue(new EcoCommand { Cmd = EcoCmd.PlaceBlueprint, X = Wx(c[0]), Y = Wy(c[1]), Z = Wz(c[2]), P0 = want[i] }); }
                if (!placed) { Log += "no cell for " + (EcoBuilding)want[i] + "; "; continue; }
                int id = FindNewest(EcoKind.Blueprint, want[i], c[0], c[1], c[2]);
                switch ((EcoBuilding)want[i])
                {
                    case EcoBuilding.ChoppingBlock: cb = id; break;
                    case EcoBuilding.Furnace: furnace = id; break;
                    case EcoBuilding.Workbench: bench = id; break;
                    case EcoBuilding.Altar: altar = id; break;
                    default: if (ri < 2) rack[ri++] = id; break;
                }
            }
            BuildDigList();
        }

        bool ColStand(int x, int z, out int y)
        {
            y = 0;
            if ((uint)x >= (uint)W.SizeX || (uint)z >= (uint)W.SizeZ) return false;
            int top = W.Top(x, z); if (top < 0) return false;
            y = top + 1;
            return s.Nav.Standable(x, y, z);
        }

        int FindNewest(EcoKind k, int b, int x, int y, int z)
        {
            for (int i = s.NEnt - 1; i >= 1; i--) if (s.EK[i] == (byte)k && s.EA[i] == b && s.EX[i] == x && s.EY[i] == y && s.EZ[i] == z) return i;
            return 0;
        }

        List<int> NearestTrees(int cx, int cz, int n)
        {
            var l = new List<int>();
            for (int i = 1; i < s.NEnt; i++) if (s.EK[i] == (byte)EcoKind.Tree) l.Add(i);
            l.Sort((a, b) => D2(s.EX[a], s.EZ[a], cx, cz).CompareTo(D2(s.EX[b], s.EZ[b], cx, cz)));
            if (l.Count > n) l.RemoveRange(n, l.Count - n);
            return l;
        }

        static int D2(int x, int z, int cx, int cz) { return (x - cx) * (x - cx) + (z - cz) * (z - cz); }

        void BuildDigList()
        {
            var cols = new List<int>();
            int R = 14;
            for (int dx = -R; dx <= R; dx++)
                for (int dz = -R; dz <= R; dz++)
                {
                    int x = baseX + dx, z = baseZ + dz;
                    if ((uint)x >= (uint)W.SizeX || (uint)z >= (uint)W.SizeZ) continue;
                    int top = W.Top(x, z); if (top < 1) continue;
                    if (!s.Nav.Standable(x, top + 1, z)) continue;     // skip trees / buildings / cliffs
                    bool near = false;
                    for (int i = 1; i < s.NEnt && !near; i++)
                    {
                        byte k = s.EK[i];
                        if ((k == (byte)EcoKind.Blueprint || k == (byte)EcoKind.Tree || (k == (byte)EcoKind.Nature && s.EA[i] != 1)) && Math.Abs(s.EX[i] - x) <= 1 && Math.Abs(s.EZ[i] - z) <= 1) near = true;
                    }
                    if (near) continue;
                    cols.Add((x * W.SizeZ + z));
                }
            cols.Sort((a, b) => D2(a / W.SizeZ, a % W.SizeZ, baseX, baseZ).CompareTo(D2(b / W.SizeZ, b % W.SizeZ, baseX, baseZ)));
            foreach (int c in cols) digList.Add(c);
        }

        // =====================================================================================================
        // main loop
        // =====================================================================================================
        /// <summary>Runs until the episode ends. Returns the final phase.</summary>
        public EcoPhase Run(int maxTick)
        {
            while (s.Phase == EcoPhase.Running && s.Tick < maxTick)
            {
                for (int u = 0; u < EcoSimCore.NU; u++) if (s.UState[u] == 0 && s.Phase == EcoPhase.Running) Decide(u);
                s.AdvanceToNextEvent(maxTick);
            }
            return s.Phase;
        }

        bool Built(int id) { return id > 0 && s.EK[id] != (byte)EcoKind.Blueprint && s.EK[id] != 0; }
        bool Exists(int id) { return id > 0 && s.EK[id] != 0; }

        int Inflight(int u, int dest, int item)
        {
            int n = 0;
            for (int v = 0; v < EcoSimCore.NU; v++)
                if (v != u && iKind[v] == 7 && iDest[v] == dest && iCat[v] == item) n += iQty[v];
            return n;
        }

        int CatInflight(int u, int cat)
        {
            int n = 0;
            for (int v = 0; v < EcoSimCore.NU; v++) if (v != u && iKind[v] == 7 && iCat[v] == 100 + cat) n += iQty[v];
            return n;
        }

        void Decide(int u)
        {
            Dispatches++;
            // settle previous delivery
            if (iKind[u] == 7 && iCat[u] >= 100) { int d = iCarry0[u] - s.UCarryQty[u]; if (d > 0) sent[iCat[u] - 100] += d; }
            iKind[u] = 0; iSub[u] = 0; iCat[u] = -1; iQty[u] = 0; iDest[u] = 0;
            if (s.UCarryQty[u] > 0) { if (Route(u)) return; }
            if (TryBuild(u)) return;
            QueueStations();
            if (TryWorkOrTake(u)) return;
            if (s.UCarryQty[u] == 0 && TryGather(u)) return;
            s.Wait(u, 1f);
        }

        // ---- routing of carried items --------------------------------------------------------------------
        bool Deliver(int u, int dest, int item, bool station, int cat)
        {
            int q = s.UCarryQty[u];
            bool ok = station ? Go(EcoCmd.CraftingAddResources, u, dest, s.EX[dest], s.EY[dest], s.EZ[dest], 0, 0, false)
                              : Go(EcoCmd.BatchedContextualActionGameObject, u, dest, s.EX[dest], s.EY[dest], s.EZ[dest], 0, 0, false);
            if (!ok) return false;
            iKind[u] = 7; iDest[u] = dest; iCat[u] = cat >= 0 ? 100 + cat : item; iQty[u] = q; iCarry0[u] = q;
            return true;
        }

        int BpNeedFree(int u, int bp, int item)
        {
            if (!Exists(bp) || s.EK[bp] != (byte)EcoKind.Blueprint) return 0;
            int n = s.BpNeed(bp, item);
            for (int v = 0; v < EcoSimCore.NU; v++) if (v != u && iKind[v] == 7 && iDest[v] == bp && s.UCarryItem[v] == item) n -= iQty[v];
            return n;
        }

        bool Route(int u)
        {
            int item = s.UCarryItem[u], q = s.UCarryQty[u];
            switch ((EcoItem)item)
            {
                case EcoItem.Log:
                    if (Built(cb)) { if (Go(EcoCmd.TransformLog, u, cb, s.EX[cb], s.EY[cb], s.EZ[cb], 0, 0, false)) { iKind[u] = 1; return true; } }
                    else if (BpNeedFree(u, cb, item) > 0) return Deliver(u, cb, item, false, -1);
                    break;
                case EcoItem.Plank:
                    {
                        int[] order = { bench, altar, rack[0], rack[1] };
                        foreach (int bp in order) if (BpNeedFree(u, bp, item) > 0) return Deliver(u, bp, item, false, -1);
                        if (Built(bench) && CatTarget[CatPlankBench] - sent[CatPlankBench] - CatInflight(u, CatPlankBench) > 0) return Deliver(u, bench, item, true, CatPlankBench);
                        break;
                    }
                case EcoItem.Stones:
                    if (Built(pile)) return Store(u, pile);
                    break;
                case EcoItem.StoneBlock:
                    foreach (int bp in new int[] { furnace, altar }) if (BpNeedFree(u, bp, item) > 0) return Deliver(u, bp, item, false, -1);
                    if (Built(pile)) return Store(u, pile);
                    break;
                case EcoItem.RawIron:
                    if (Built(furnace) && CatTarget[CatIron] - sent[CatIron] - CatInflight(u, CatIron) > 0) return Deliver(u, furnace, item, true, CatIron);
                    break;
                case EcoItem.OrangeCrystal:
                    {
                        if (BpNeedFree(u, altar, item) > 0) return Deliver(u, altar, item, false, -1);
                        for (int pass = 0; pass < 2; pass++)
                        {
                            int min = pass == 0 ? q : 1;
                            if (Built(furnace) && CatTarget[CatFuel] - sent[CatFuel] - CatInflight(u, CatFuel) >= min) return Deliver(u, furnace, item, true, CatFuel);
                            if (Built(altar) && CatTarget[CatCryAltar] - sent[CatCryAltar] - CatInflight(u, CatCryAltar) >= min) return Deliver(u, altar, item, true, CatCryAltar);
                        }
                        break;
                    }
                case EcoItem.IronIngot:
                    if (BpNeedFree(u, altar, item) > 0) return Deliver(u, altar, item, false, -1);
                    if (Built(altar) && CatTarget[CatIngAltar] - sent[CatIngAltar] - CatInflight(u, CatIngAltar) >= q) return Deliver(u, altar, item, true, CatIngAltar);
                    if (Built(bench) && CatTarget[CatIngBench] - sent[CatIngBench] - CatInflight(u, CatIngBench) >= q) return Deliver(u, bench, item, true, CatIngBench);
                    if (Built(altar) && CatTarget[CatIngAltar] - sent[CatIngAltar] - CatInflight(u, CatIngAltar) > 0) return Deliver(u, altar, item, true, CatIngAltar);
                    if (Built(bench) && CatTarget[CatIngBench] - sent[CatIngBench] - CatInflight(u, CatIngBench) > 0) return Deliver(u, bench, item, true, CatIngBench);
                    break;
                case EcoItem.Poleaxe:
                case EcoItem.KettleHat:
                    foreach (int r in rack) if (Built(r) && s.Room(r, item) > 0) return Store(u, r);
                    break;
                case EcoItem.Fiber:
                    return Go(EcoCmd.GoDropAt, u, 0, s.UX[u], s.UY[u], s.UZ[u], 0, 0, false);
            }
            // nothing sensible yet: hold the item and wait
            s.Wait(u, 1f); iKind[u] = 0; return true;
        }

        bool Store(int u, int dest)
        {
            if (!Go(EcoCmd.GoStoreInto, u, dest, s.EX[dest], s.EY[dest], s.EZ[dest], 0, 0, false)) return false;
            iKind[u] = 7; iDest[u] = dest; iCat[u] = 0; iQty[u] = s.UCarryQty[u]; iCarry0[u] = s.UCarryQty[u];
            return true;
        }

        // ---- buildings and stations ----------------------------------------------------------------------
        bool TryBuild(int u)
        {
            if (s.UCarryQty[u] > 0) return false;
            int[] all = { cb, furnace, bench, altar, rack[0], rack[1] };
            foreach (int bp in all)
            {
                if (!Exists(bp) || s.EK[bp] != (byte)EcoKind.Blueprint || s.BpMissing(bp)) continue;
                int builders = 0; for (int v = 0; v < EcoSimCore.NU; v++) if (v != u && iKind[v] == 4 && iDest[v] == bp) builders++;
                if (builders >= 2) continue;
                if (Go(EcoCmd.BatchedContextualActionGameObject, u, bp, s.EX[bp], s.EY[bp], s.EZ[bp], 0, 0, false)) { iKind[u] = 4; iDest[u] = bp; return true; }
            }
            return false;
        }

        void QueueStations()
        {
            if (!queuedFurnace && Built(furnace))
            {
                queuedFurnace = true;
                Go(EcoCmd.CraftingQueueRecipe, 0, furnace, 0, 0, 0, EcoDefs.RIronIngot, 8, false);
                Go(EcoCmd.CraftingQueueRecipe, 0, furnace, 0, 0, 0, EcoDefs.RIronIngot, 4, false);
            }
            if (!queuedBench && Built(bench))
            {
                queuedBench = true;
                Go(EcoCmd.CraftingQueueRecipe, 0, bench, 0, 0, 0, EcoDefs.RPoleaxe, 8, false);
                Go(EcoCmd.CraftingQueueRecipe, 0, bench, 0, 0, 0, EcoDefs.RKettleHat, 8, false);
            }
            if (Built(pile) && s.Cur[EcoSimCore.CBlocks] < 5)
            {
                int st = s.ECont[pile * EcoDefs.NI + (int)EcoItem.Stones];
                if (st >= Math.Min(40, 10 * (5 - s.Cur[EcoSimCore.CBlocks])))
                    Go(EcoCmd.ManualConversion, 0, pile, 0, 0, 0, 0, 0, false);
            }
            if (!queuedAltar && Built(altar))
            {
                queuedAltar = true;
                Go(EcoCmd.CraftingQueueRecipe, 0, altar, 0, 0, 0, EcoDefs.RCaptureWard, 2, false);
            }
        }

        bool TryWorkOrTake(int u)
        {
            if (s.UCarryQty[u] > 0) return false;
            foreach (int st in new int[] { bench, altar })
            {
                if (!Built(st) || !s.SActive[st] || s.SWorkLeft[st] <= 0) continue;
                int w = 0; for (int v = 0; v < EcoSimCore.NU; v++) if (v != u && iKind[v] == 5 && iDest[v] == st) w++;
                if (w >= 2) continue;
                if (Go(EcoCmd.CraftingWorkAt, u, st, s.EX[st], s.EY[st], s.EZ[st], 0, 0, false)) { iKind[u] = 5; iDest[u] = st; return true; }
            }
            if (Built(pile) && s.ECont[pile * EcoDefs.NI + (int)EcoItem.StoneBlock] > 0 && BpNeedFree(u, furnace, (int)EcoItem.StoneBlock) + BpNeedFree(u, altar, (int)EcoItem.StoneBlock) > 0)
            {
                int takers = 0; for (int v = 0; v < EcoSimCore.NU; v++) if (v != u && iKind[v] == 6 && iDest[v] == pile) takers++;
                if (takers < 3 && Go(EcoCmd.Pickup, u, pile, s.EX[pile], s.EY[pile], s.EZ[pile], (int)EcoItem.StoneBlock, 1, false)) { iKind[u] = 6; iDest[u] = pile; return true; }
            }
            // collect: ingots from the furnace, tools from the bench
            if (Built(furnace) && s.ECont[furnace * EcoDefs.NI + (int)EcoItem.IronIngot] > 0 && !Taker(u, furnace))
                if (Go(EcoCmd.Pickup, u, furnace, s.EX[furnace], s.EY[furnace], s.EZ[furnace], (int)EcoItem.IronIngot, 0, false)) { iKind[u] = 6; iDest[u] = furnace; return true; }
            if (Built(bench))
                foreach (int item in new int[] { (int)EcoItem.Poleaxe, (int)EcoItem.KettleHat })
                    if (s.ECont[bench * EcoDefs.NI + item] > 0 && !Taker(u, bench))
                        if (Go(EcoCmd.Pickup, u, bench, s.EX[bench], s.EY[bench], s.EZ[bench], item, 0, false)) { iKind[u] = 6; iDest[u] = bench; return true; }
            return false;
        }

        bool Taker(int u, int st)
        {
            for (int v = 0; v < EcoSimCore.NU; v++) if (v != u && iKind[v] == 6 && iDest[v] == st) return true;
            return false;
        }

        // ---- gathering -----------------------------------------------------------------------------------
        int Count(int kind) { int n = 0; for (int v = 0; v < EcoSimCore.NU; v++) if (iKind[v] == kind) n++; return n; }

        bool TryGather(int u)
        {
            // 1. loose logs on the ground
            int groundLogs = 0, carriedLogs = 0, pickers = 0, choppers = 0;
            for (int i = 1; i < s.NEnt; i++) if (s.EK[i] == (byte)EcoKind.Item && s.EA[i] == (int)EcoItem.Log) groundLogs += s.EB[i];
            for (int v = 0; v < EcoSimCore.NU; v++) { if (s.UCarryItem[v] == (int)EcoItem.Log) carriedLogs += s.UCarryQty[v]; if (v != u && iSub[v] == 2) pickers++; if (v != u && iSub[v] == 1) choppers++; }
            int logsUsed = s.Cur[EcoSimCore.CPlanks] / 3 + carriedLogs + (cb > 0 && s.EK[cb] != (byte)EcoKind.Blueprint ? 1 : s.ECont[cb * EcoDefs.NI + (int)EcoItem.Log]);
            if (logsUsed + pickers < NeedLogs && groundLogs > pickers && Count(1) < 3)
            {
                int best = -1, bd = int.MaxValue;
                for (int i = 1; i < s.NEnt; i++)
                    if (s.EK[i] == (byte)EcoKind.Item && s.EA[i] == (int)EcoItem.Log && s.EB[i] > 0)
                    {
                        int d = D2(s.EX[i], s.EZ[i], s.UX[u], s.UZ[u]);
                        if (d < bd) { bd = d; best = i; }
                    }
                if (best > 0 && Go(EcoCmd.Pickup, u, best, s.EX[best], s.EY[best], s.EZ[best], 0, 1, false)) { iKind[u] = 1; iSub[u] = 2; return true; }
            }
            // 2. chop
            if (logsUsed + pickers + groundLogs - Math.Min(groundLogs, pickers) + choppers * EcoTune.LogsPerTree < NeedLogs && Count(1) < 3)
            {
                int best = -1, bd = int.MaxValue;
                for (int i = 1; i < s.NEnt; i++)
                    if (s.EK[i] == (byte)EcoKind.Tree && !usedTree[i])
                    {
                        int d = D2(s.EX[i], s.EZ[i], s.UX[u], s.UZ[u]);
                        if (d < bd) { bd = d; best = i; }
                    }
                if (best > 0 && Go(EcoCmd.AddTreeGoal, u, best, s.EX[best], s.EY[best], s.EZ[best], 0, 0, false)) { usedTree[best] = true; iKind[u] = 1; iSub[u] = 1; return true; }
            }
            // 3. stones
            if (stoneRes < NeedStones && Count(2) < 3 && digNext < digList.Count)
            {
                int n = Math.Min(20, NeedStones - stoneRes);
                int issued = 0;
                for (int k = 0; k < n && digNext < digList.Count; k++)
                {
                    int col = digList[digNext++];
                    int x = col / W.SizeZ, z = col % W.SizeZ;
                    int top = W.Top(x, z);
                    if (Go(EcoCmd.AddBreakVoxelGoal, u, 0, x, top, z, 0, 0, issued > 0)) issued++;
                }
                if (issued > 0) { stoneRes += issued; iKind[u] = 2; return true; }
            }
            // 4. ore (iron first, balanced by fraction)
            bool wantIron = ironRes < NeedIron, wantCry = crysRes < NeedCrystal;
            if (wantIron || wantCry)
            {
                bool iron = wantIron && (!wantCry || ironRes <= crysRes);
                if (HarvestTrip(u, iron ? 4 : 8, iron)) { iKind[u] = 3; return true; }
                if (HarvestTrip(u, iron ? 8 : 4, !iron)) { iKind[u] = 3; return true; }
            }
            // 5. help with stones/ore beyond the caps
            if (stoneRes < NeedStones && digNext < digList.Count)
            {
                int n = Math.Min(20, NeedStones - stoneRes), issued = 0;
                for (int k = 0; k < n && digNext < digList.Count; k++)
                {
                    int col = digList[digNext++];
                    int x = col / W.SizeZ, z = col % W.SizeZ;
                    if (Go(EcoCmd.AddBreakVoxelGoal, u, 0, x, W.Top(x, z), z, 0, 0, issued > 0)) issued++;
                }
                if (issued > 0) { stoneRes += issued; iKind[u] = 2; return true; }
            }
            return false;
        }

        bool HarvestTrip(int u, int natType, bool isIron)
        {
            int want = Math.Min(20, isIron ? NeedIron - ironRes : NeedCrystal - crysRes);
            if (want <= 0) return false;
            int got = 0, first = 1;
            while (got < want)
            {
                int best = -1, bd = int.MaxValue;
                for (int i = 1; i < s.NEnt; i++)
                    if (s.EK[i] == (byte)EcoKind.Nature && s.EA[i] == natType && !usedNat[i])
                    {
                        int d = D2(s.EX[i], s.EZ[i], s.UX[u], s.UZ[u]);
                        if (d < bd) { bd = d; best = i; }
                    }
                if (best < 0) break;
                if (!Go(EcoCmd.AddNatureHarvestGoal, u, best, s.EX[best], s.EY[best], s.EZ[best], 0, 0, first == 0)) { usedNat[best] = true; continue; }
                first = 0;
                int take = Math.Min(s.EB[best] - natRes[best], want - got); if (take < 1) take = 1;
                natRes[best] += take; got += take;
                if (natRes[best] >= s.EB[best]) usedNat[best] = true;
            }
            if (got <= 0) return false;
            got = Math.Min(got, want);
            if (isIron) ironRes += got; else crysRes += got;
            return true;
        }
    }
}
