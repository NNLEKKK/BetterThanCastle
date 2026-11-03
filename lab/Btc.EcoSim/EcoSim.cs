using System;
using Btc.EcoShared;

namespace Btc.EcoSim
{
    public enum EcoPhase : byte { Placement = 0, Running = 1, Won = 2, StallLoss = 3, TimeLoss = 4 }

    public struct EcoOrder { public byte Op; public int Target, X, Y, Z, P0, P1; public float F0; }

    /// <summary>
    /// Economy sim core. Deterministic per seed, allocation-free Step. Units act ONLY on orders (no idle job search; FreeAgent == WaitForOrder).
    /// Coordinates inside the sim are GRID cells (world minus origin); commands use WORLD voxel coords. A unit stands in the air cell above a solid voxel.
    /// Time is event driven: Step()/AdvanceTo() jump from event to event (unit wake-ups, furnace heat ticks, stall/Tmax deadlines) on the 30 Hz tick grid.
    /// Episode starts in Phase.Placement (no ticks run): PlaceWorker x6 (or StartRun) then t=0.
    /// </summary>
    public sealed partial class EcoSimCore
    {
        public const int NU = 6, EC = 4096, QCap = 16, SQCap = 8, NI = EcoDefs.NI;

        public readonly EcoWorld World;
        public readonly EcoNav Nav;
        readonly byte[] T; readonly ushort[] B; readonly byte[] Ob;
        public int Tick;
        public EcoPhase Phase;
        public float Time { get { return Tick * EcoParams.DtTick; } }
        float accum; ulong rng;
        public ulong TerrHash;
        public readonly int[] EditIdx = new int[1 << 16]; public int EditN;   // voxel edit log = sparse diff vs the loaded map (Save/Load)

        public int UnitCount { get { return NU; } }

        // ---- units (grid cells)
        public readonly int[] UX = new int[NU], UY = new int[NU], UZ = new int[NU];
        public readonly int[] UFromX = new int[NU], UFromY = new int[NU], UFromZ = new int[NU], UT0 = new int[NU];   // walk segment start (for interpolation)
        public readonly byte[] UState = new byte[NU];        // 0 idle, 1 walking, 2 acting
        public readonly byte[] UAutonomy = new byte[NU];
        public readonly int[] UCarryItem = new int[NU], UCarryQty = new int[NU], UTool = new int[NU], UProject = new int[NU];
        public readonly int[] UWake = new int[NU];
        public readonly bool[] UPlaced = new bool[NU];
        readonly EcoOrder[] UQ = new EcoOrder[NU * QCap];
        readonly int[] UQHead = new int[NU], UQLen = new int[NU];
        readonly EcoOrder[] UCur = new EcoOrder[NU];
        readonly int[] UEx = new int[NU], UEy = new int[NU], UEz = new int[NU];   // walk destination
        readonly int[] UWorkLeft = new int[NU];

        // ---- entities
        public int NEnt;
        public readonly byte[] EK = new byte[EC];
        public readonly int[] EX = new int[EC], EY = new int[EC], EZ = new int[EC], EA = new int[EC], EB = new int[EC];
        public readonly int[] ECont = new int[EC * NI];
        readonly int[] freeStack = new int[EC]; int freeN;
        public readonly byte[] CMode = new byte[EC];       // 0 accept all, 1 accept only, 2 keep empty
        public readonly int[] CMask = new int[EC], CExcl = new int[EC];
        public readonly bool[] CAuto = new bool[EC];
        // stations
        public readonly int[] SQRec = new int[EC * SQCap], SQQty = new int[EC * SQCap], SQOrig = new int[EC * SQCap], SQLen = new int[EC];
        public readonly bool[] EValid = new bool[EC];          // blueprint validity (false = red, never built)
        public int InvalidPlacements, UnitsPushed;   // UnitsPushed: workers moved out of a completed building footprint [guess]
        public float PlacementPenalty { get { return InvalidPlacements * EcoParams.InvalidBlueprintPenalty; } }
        public readonly bool[] SLoop = new bool[EC], SAutoStore = new bool[EC], SActive = new bool[EC];
        public readonly int[] SWorkLeft = new int[EC], SHeatLeft = new int[EC], SHeatWake = new int[EC], SCur = new int[EC];

        // ---- counters / milestones
        public static readonly string[] MileName = {
            "tree_chopped", "chopping_block", "planks", "stockpile", "bricks", "furnace", "ingot_started", "weapon_rack", "workbench", "halberd",
            "kettle_hat", "altar", "capture_ward", "log_delivered", "raw_iron", "orange_crystal", "ore_in_furnace", "ingot",
            "kit_1", "kit_2", "kit_3", "kit_4", "kit_5", "kit_6", "kit_7", "kit_8", "ward_1", "ward_2" };
        public const int MKit1 = 18, MWard1 = 26, MCount = 28;
        public readonly int[] MileTick = new int[MCount];

        public const int CLogs = 0, CPlanks = 1, CRawIron = 2, CCrystal = 3, CIngots = 4, CStones = 5, CDelivered = 6, CBuildHits = 7, CWorkHits = 8, CHeat = 9, CRack = 10, CBlocks = 11, CCount = 12;
        public static readonly string[] CounterName = { "logs", "planks", "raw_iron", "crystal", "ingots", "stones", "delivered", "build_hits", "work_hits", "heat", "rack_items", "stone_blocks" };
        public readonly int[] Cur = new int[CCount], Max = new int[CCount];
        public static int StoneUsefulCap = 100;     // [guess] stones count as "useful" only up to this many (anti-grind)
        public int LastGainTick;
        public int Kits, WardsMade, OrderFails, OrdersIssued, DoneTick = -1;
        public const int GoalKits = 8, GoalWards = 2;
        public static int StallTicks = (int)(EcoParams.StallSeconds * EcoParams.TicksPerSecond);
        public static int TmaxTicks = 40 * 60 * EcoParams.TicksPerSecond;

        public EcoSimCore(EcoWorld world)
        {
            string err = GameplaySnapshot.Check(world.Gameplay);
            if (err != null) throw new InvalidOperationException("EcoSim refuses to run: " + err);
            World = world;
            T = new byte[world.Terrain.Length]; B = new ushort[world.Block.Length]; Ob = new byte[world.Terrain.Length];
            Nav = new EcoNav(world, T, B, Ob);
        }

        public ulong NextU64() { rng ^= rng << 13; rng ^= rng >> 7; rng ^= rng << 17; return rng; }

        // =====================================================================================================
        // reset
        // =====================================================================================================
        public void Reset(ulong seed)
        {
            Tick = 0; accum = 0; Phase = EcoPhase.Placement; DoneTick = -1;
            rng = seed * 0x9E3779B97F4A7C15UL + 0x1234567UL;
            Buffer.BlockCopy(World.Terrain, 0, T, 0, T.Length);
            Buffer.BlockCopy(World.Block, 0, B, 0, B.Length * 2);
            Array.Clear(Ob, 0, Ob.Length);
            EditN = 0; TerrHash = 0; Nav.Version++;
            Array.Clear(EK, 0, EK.Length); Array.Clear(ECont, 0, ECont.Length); NEnt = 1; freeN = 0;
            Array.Clear(CMode, 0, CMode.Length); Array.Clear(CMask, 0, CMask.Length); Array.Clear(CExcl, 0, CExcl.Length); Array.Clear(CAuto, 0, CAuto.Length);
            Array.Clear(SQLen, 0, SQLen.Length); Array.Clear(SLoop, 0, SLoop.Length); Array.Clear(SAutoStore, 0, SAutoStore.Length); Array.Clear(SActive, 0, SActive.Length);
            Array.Clear(SHeatWake, 0, SHeatWake.Length);
            for (int i = 0; i < MCount; i++) MileTick[i] = -1;
            Array.Clear(Cur, 0, CCount); Array.Clear(Max, 0, CCount);
            LastGainTick = 0; Kits = 0; WardsMade = 0; OrderFails = 0; OrdersIssued = 0; InvalidPlacements = 0; UnitsPushed = 0;

            foreach (var t in World.Trees)
            {
                int x = t.X - World.OriginX, y = t.Y - World.OriginY + 1, z = t.Z - World.OriginZ;
                if (!World.InBounds(x, y, z)) continue;
                int id = NewEnt(EcoKind.Tree, x, y, z); if (id < 0) break;
                EA[id] = t.Type; EB[id] = EcoTune.ChopHits;
                Ob[Nav.Idx(x, y, z)] = 1;
            }
            foreach (var n in World.Nature)
            {
                int x = n.X - World.OriginX, y = n.Y - World.OriginY, z = n.Z - World.OriginZ;
                if (!World.InBounds(x, y, z)) continue;
                while (y < World.SizeY - 2 && Nav.Solid(x, y, z)) y++;
                int fy = Nav.FallTo(x, y, z); if (fy > 0) y = fy;
                int id = NewEnt(EcoKind.Nature, x, y, z); if (id < 0) break;
                EA[id] = n.Type;
                EB[id] = n.Charges > 0 ? n.Charges : (n.Type == 4 ? EcoTune.IronCharges : n.Type == 8 ? EcoTune.PyriteCharges : 3);
                if (n.Type != 1) Ob[Nav.Idx(x, y, z)] = 1;   // plants do not block
            }
            int k = 0;
            for (int i = 0; i < NU; i++) { UState[i] = 0; UAutonomy[i] = (byte)EcoAutonomy.WaitForOrder; UCarryItem[i] = 0; UCarryQty[i] = 0; UTool[i] = 0; UProject[i] = -1; UQLen[i] = 0; UQHead[i] = 0; UPlaced[i] = false; UWake[i] = 0; }
            foreach (var e in World.Entities)
            {
                int x = (int)Math.Floor(e.Px + 0.5f) - World.OriginX, y = (int)Math.Floor(e.Py + 0.5f) - World.OriginY, z = (int)Math.Floor(e.Pz + 0.5f) - World.OriginZ;
                if (e.Kind == "unit")
                {
                    if (k >= NU) continue;
                    int sx, sy, sz;
                    if (!Nav.Snap(x, y + 1, z, out sx, out sy, out sz)) { sx = x; sy = y + 1; sz = z; }
                    // keep the 6 workers on distinct cells where possible
                    for (int tries = 0; tries < 12 && CellHasUnit(sx, sy, sz, k); tries++)
                    {
                        int nx, ny, nz;
                        if (Nav.Snap(sx + 1 + (tries % 3), sy, sz + (tries / 3), out nx, out ny, out nz)) { sx = nx; sy = ny; sz = nz; }
                    }
                    UX[k] = sx; UY[k] = sy; UZ[k] = sz; UFromX[k] = sx; UFromY[k] = sy; UFromZ[k] = sz; k++;
                }
                else if (e.Kind == "building" && e.Key != null && e.Key.IndexOf("Crystal", StringComparison.Ordinal) >= 0 && World.InBounds(x, y, z))
                {
                    while (y < World.SizeY - 2 && Nav.Solid(x, y, z)) y++;
                    int id = NewEnt(EcoKind.Crystal, x, y, z);
                    if (id >= 0) Ob[Nav.Idx(x, y, z)] = 1;
                }
            }
        }

        bool CellHasUnit(int x, int y, int z, int upTo)
        {
            for (int i = 0; i < upTo; i++) if (UX[i] == x && UY[i] == y && UZ[i] == z) return true;
            return false;
        }

        int NewEnt(EcoKind kind, int x, int y, int z)
        {
            int id;
            if (freeN > 0) id = freeStack[--freeN];
            else { if (NEnt >= EC) return -1; id = NEnt++; }
            EK[id] = (byte)kind; EX[id] = x; EY[id] = y; EZ[id] = z; EA[id] = 0; EB[id] = 0;
            Array.Clear(ECont, id * NI, NI);
            EValid[id] = true;
            CMode[id] = 0; CMask[id] = 0; CExcl[id] = 0; CAuto[id] = false;
            SQLen[id] = 0; SLoop[id] = false; SAutoStore[id] = false; SActive[id] = false; SHeatWake[id] = 0; SWorkLeft[id] = 0; SHeatLeft[id] = 0;
            return id;
        }

        void FreeEnt(int id)
        {
            if (EK[id] == (byte)EcoKind.Free) return;
            EK[id] = (byte)EcoKind.Free;
            if (freeN < EC) freeStack[freeN++] = id;
        }

        // =====================================================================================================
        // placement phase
        // =====================================================================================================
        public bool StandableWorld(int wx, int wy, int wz) { return Nav.Standable(wx - World.OriginX, wy - World.OriginY, wz - World.OriginZ); }

        /// <summary>Placement phase only: put worker u (0-based) on a stand cell (world voxel of the air cell above the ground). Invalid cell = false.</summary>
        public bool PlaceWorker(int u, int wx, int wy, int wz)
        {
            if (Phase != EcoPhase.Placement || (uint)u >= NU) return false;
            int x = wx - World.OriginX, y = wy - World.OriginY, z = wz - World.OriginZ;
            if (!Nav.Standable(x, y, z)) return false;
            UX[u] = x; UY[u] = y; UZ[u] = z; UFromX[u] = x; UFromY[u] = y; UFromZ[u] = z; UPlaced[u] = true;
            for (int i = 0; i < NU; i++) if (!UPlaced[i]) return true;
            StartRun();
            return true;
        }

        /// <summary>Ends the placement phase (unplaced workers keep their snapped default cell). Milestone clock starts at t=0 here.</summary>
        public void StartRun()
        {
            if (Phase != EcoPhase.Placement) return;
            Phase = EcoPhase.Running; Tick = 0; LastGainTick = 0;
        }

        // =====================================================================================================
        // time
        // =====================================================================================================
        /// <summary>Advance by dt seconds in 30 Hz ticks (event driven inside). Allocation-free.</summary>
        public void Step(float dt)
        {
            if (Phase != EcoPhase.Running) return;
            accum += dt;
            int n = (int)(accum / EcoParams.DtTick);
            if (n <= 0) return;
            accum -= n * EcoParams.DtTick;
            AdvanceTo(Tick + n);
        }

        public bool AnyIdle()
        {
            for (int u = 0; u < NU; u++) if (UState[u] == 0) return true;
            return false;
        }

        /// <summary>Jumps to the next event (>= Tick+1, <= limit) and processes it.</summary>
        public void AdvanceToNextEvent(int limitTick)
        {
            if (Phase != EcoPhase.Running) return;
            int next = limitTick;
            for (int u = 0; u < NU; u++) if (UState[u] != 0 && UWake[u] < next) next = UWake[u];
            for (int i = 1; i < NEnt; i++) if (SHeatWake[i] > 0 && SHeatWake[i] < next) next = SHeatWake[i];
            int dl = LastGainTick + StallTicks; if (dl < next) next = dl;
            if (TmaxTicks < next) next = TmaxTicks;
            if (next <= Tick) next = Tick + 1;
            if (next > limitTick) next = limitTick;
            Tick = next;
            Process();
        }

        public void AdvanceTo(int targetTick)
        {
            while (Phase == EcoPhase.Running && Tick < targetTick) AdvanceToNextEvent(targetTick);
        }

        void Process()
        {
            for (int u = 0; u < NU; u++)
            {
                int guard = 0;
                while (UState[u] != 0 && UWake[u] <= Tick && guard++ < 64) OnWake(u);
            }
            for (int i = 1; i < NEnt; i++)
                if (SHeatWake[i] > 0 && SHeatWake[i] <= Tick) HeatTick(i);
            if (Kits >= GoalKits && WardsMade >= GoalWards) { Phase = EcoPhase.Won; DoneTick = Tick; return; }
            if (Tick - LastGainTick >= StallTicks) { Phase = EcoPhase.StallLoss; DoneTick = Tick; return; }
            if (Tick >= TmaxTicks) { Phase = EcoPhase.TimeLoss; DoneTick = Tick; }
        }

        // =====================================================================================================
        // counters
        // =====================================================================================================
        void Mile(int m) { if (MileTick[m] < 0) MileTick[m] = Tick; }

        void Count(int c, int n)
        {
            int v = Cur[c] + n;
            if (c == CStones && v > StoneUsefulCap) v = StoneUsefulCap;
            Cur[c] = v;
            if (v > Max[c]) { Max[c] = v; LastGainTick = Tick; }
        }

        void SetGauge(int c, int v)
        {
            Cur[c] = v;
            if (v > Max[c]) { Max[c] = v; LastGainTick = Tick; }
        }

        void RecountRacks()
        {
            int pol = 0, hat = 0;
            for (int i = 1; i < NEnt; i++)
                if (EK[i] == (byte)EcoKind.Rack) { pol += ECont[i * NI + (int)EcoItem.Poleaxe]; hat += ECont[i * NI + (int)EcoItem.KettleHat]; }
            SetGauge(CRack, pol + hat);
            Kits = Math.Min(pol, hat);
            for (int k = 1; k <= 8; k++) if (Kits >= k) Mile(MKit1 + k - 1);
        }

        // =====================================================================================================
        // containers
        // =====================================================================================================
        public bool Accepts(int id, int item)
        {
            int kind = EK[id];
            if (kind == (int)EcoKind.Rack) return EcoDefs.IsTool(item);
            if (kind == (int)EcoKind.Station) return true;
            if (kind != (int)EcoKind.Container) return false;
            if (CMode[id] == 2) return false;
            if (CMode[id] == 1 && (CMask[id] & (1 << item)) == 0) return false;
            if ((CExcl[id] & (1 << item)) != 0) return false;
            return true;
        }

        public int Room(int id, int item)
        {
            int kind = EK[id];
            if (kind == (int)EcoKind.Rack)
            {
                int tot = 0; for (int j = 0; j < NI; j++) if (EcoDefs.IsTool(j)) tot += ECont[id * NI + j];
                return EcoDefs.RackSlots - tot;
            }
            int cap = kind == (int)EcoKind.Station ? EcoDefs.StationCap : EcoDefs.PileCap[item];
            return cap - ECont[id * NI + item];
        }

        int ContAdd(int id, int item, int qty)
        {
            if (!Accepts(id, item)) return 0;
            int n = Math.Min(qty, Room(id, item));
            if (n <= 0) return 0;
            ECont[id * NI + item] += n;
            if (EK[id] == (byte)EcoKind.Container) { if (item == (int)EcoItem.Log) Mile(13); AutoConvert(id); }
            else if (EK[id] == (byte)EcoKind.Rack) RecountRacks();
            return n;
        }

        void AutoConvert(int id)
        {
            if (!CAuto[id]) return;
            DoConvert(id, true);
        }

        bool DoConvert(int id, bool onlyIfFull)
        {
            int s = (int)EcoItem.Stones, b = (int)EcoItem.StoneBlock;
            int have = ECont[id * NI + s];
            if (have <= 0) return false;
            if (onlyIfFull && have < EcoDefs.PileCap[s]) return false;
            int blocks = Math.Min(have / EcoTune.StonesPerBlock, EcoDefs.PileCap[b] - ECont[id * NI + b]);
            if (blocks <= 0) return false;
            ECont[id * NI + s] -= blocks * EcoTune.StonesPerBlock;
            ECont[id * NI + b] += blocks;
            Mile(4); Count(CBlocks, blocks);
            return true;
        }

        // =====================================================================================================
        // terrain edits and settling
        // =====================================================================================================
        void SetTerrain(int x, int y, int z, byte v)
        {
            int i = Nav.Idx(x, y, z);
            T[i] = v;
            if (EditN < EditIdx.Length) EditIdx[EditN++] = i; else throw new InvalidOperationException("terrain edit log overflow");
            TerrHash = (TerrHash ^ (ulong)(uint)i * 0x9E3779B1UL ^ v) * 1099511628211UL;
            Nav.Invalidate(x, y, z);
            Settle(x, z);
        }

        void Settle(int x, int z)
        {
            for (int u = 0; u < NU; u++)
                if (UX[u] == x && UZ[u] == z && !Nav.Standable(x, UY[u], z))
                {
                    int f = Nav.FallTo(x, UY[u], z);
                    if (f > 0) { UY[u] = f; if (UState[u] != 1) UFromY[u] = f; }
                }
            for (int i = 1; i < NEnt; i++)
                if (EK[i] == (byte)EcoKind.Item && EX[i] == x && EZ[i] == z)
                {
                    int ix = Nav.Idx(x, EY[i] - 1, z);
                    if (T[ix] != 0 || B[ix] != 0) continue;
                    int f = Nav.FallTo(x, EY[i], z);
                    if (f > 0) { MoveItem(i, x, f, z); }
                }
        }

        void MoveItem(int i, int x, int y, int z)
        {
            for (int j = 1; j < NEnt; j++)
                if (j != i && EK[j] == (byte)EcoKind.Item && EA[j] == EA[i] && EX[j] == x && EY[j] == y && EZ[j] == z) { EB[j] += EB[i]; FreeEnt(i); return; }
            EX[i] = x; EY[i] = y; EZ[i] = z;
        }

        void DropItem(int item, int qty, int x, int y, int z)
        {
            if (qty <= 0) return;
            int f = Nav.FallTo(x, y, z); if (f > 0) y = f;
            for (int j = 1; j < NEnt; j++)
                if (EK[j] == (byte)EcoKind.Item && EA[j] == item && EX[j] == x && EY[j] == y && EZ[j] == z) { EB[j] += qty; return; }
            int id = NewEnt(EcoKind.Item, x, y, z);
            if (id >= 0) { EA[id] = item; EB[id] = qty; }
        }

        // =====================================================================================================
        // hashing
        // =====================================================================================================
        static ulong Mix(ulong h, ulong v) { return (h ^ v) * 1099511628211UL; }

        public ulong StateHash()
        {
            ulong h = 1469598103934665603UL;
            h = Mix(h, (ulong)InvalidPlacements); h = Mix(h, (ulong)UnitsPushed); h = Mix(h, (ulong)Tick); h = Mix(h, rng); h = Mix(h, (ulong)Phase); h = Mix(h, TerrHash);
            for (int i = 0; i < NU; i++)
            {
                h = Mix(h, (ulong)(uint)UX[i]); h = Mix(h, (ulong)(uint)UY[i]); h = Mix(h, (ulong)(uint)UZ[i]);
                h = Mix(h, UState[i]); h = Mix(h, UAutonomy[i]); h = Mix(h, (ulong)(uint)UWake[i]);
                h = Mix(h, (ulong)UCarryItem[i]); h = Mix(h, (ulong)UCarryQty[i]); h = Mix(h, (ulong)UTool[i]); h = Mix(h, (ulong)(uint)UProject[i]);
            }
            for (int i = 1; i < NEnt; i++)
            {
                h = Mix(h, EK[i]);
                if (EK[i] == 0) continue;
                h = Mix(h, (ulong)(uint)EX[i]); h = Mix(h, (ulong)(uint)EY[i]); h = Mix(h, (ulong)(uint)EZ[i]); h = Mix(h, (ulong)(uint)EA[i]); h = Mix(h, (ulong)(uint)EB[i]);
                for (int j = 0; j < NI; j++) h = Mix(h, (ulong)(uint)ECont[i * NI + j]);
            }
            for (int i = 0; i < CCount; i++) h = Mix(h, (ulong)(uint)Cur[i]);
            for (int i = 0; i < MCount; i++) h = Mix(h, (ulong)(uint)MileTick[i]);
            return h;
        }

        /// <summary>Interpolated unit position (grid coords) for observations.</summary>
        public void UnitPos(int u, out float x, out float y, out float z)
        {
            if (UState[u] == 1 && UWake[u] > UT0[u])
            {
                float t = (Tick - UT0[u]) / (float)(UWake[u] - UT0[u]); if (t > 1) t = 1;
                x = UFromX[u] + (UEx[u] - UFromX[u]) * t; y = UFromY[u] + (UEy[u] - UFromY[u]) * t; z = UFromZ[u] + (UEz[u] - UFromZ[u]) * t;
            }
            else { x = UX[u]; y = UY[u]; z = UZ[u]; }
        }
    }
}
