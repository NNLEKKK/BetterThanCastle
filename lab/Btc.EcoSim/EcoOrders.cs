using System;
using Btc.EcoShared;

namespace Btc.EcoSim
{
    public sealed partial class EcoSimCore
    {
        readonly int[] UHit = new int[NU];

        // =====================================================================================================
        // command entry points
        // =====================================================================================================
        /// <summary>Immediate order: replaces the unit's queue and current action (mov.cancel). False = rejected / failed at start.</summary>
        public bool Issue(EcoCommand c) { return Apply(c, false); }
        /// <summary>Queued order (shift-click): runs after the current one.</summary>
        public bool Enqueue(EcoCommand c) { return Apply(c, true); }

        public static EcoCommand Cmd(EcoCmd cmd, int unit, int target, int wx, int wy, int wz, int p0, int p1)
        {
            return new EcoCommand { Cmd = cmd, Actor = unit + 1, Target = target, X = wx, Y = wy, Z = wz, P0 = p0, P1 = p1 };
        }

        bool Apply(EcoCommand c, bool queue)
        {
            if (c.Cmd == EcoCmd.PlaceWorker) return PlaceWorker(c.Actor - 1, c.X, c.Y, c.Z);
            if (Phase != EcoPhase.Running) return false;
            if (EcoCmdNames.IsSpawn(c.Cmd) || EcoCmdNames.IsCombat(c.Cmd)) return false;
            int t = c.Target;
            bool tv = t > 0 && t < NEnt && EK[t] != 0;
            switch (c.Cmd)
            {
                case EcoCmd.AccepterPosition: return MakeZone(c.X - World.OriginX, c.Y - World.OriginY, c.Z - World.OriginZ) > 0;
                case EcoCmd.AccepterRayon: if (!tv || EK[t] != (byte)EcoKind.Container) return false; EB[t] = c.P0 > 0 ? c.P0 : 1; return true;
                case EcoCmd.PlaceBlueprint: return PlaceBlueprint(c.X - World.OriginX, c.Y - World.OriginY, c.Z - World.OriginZ, c.P0);
                case EcoCmd.DestroyBluePrint:
                    if (!tv || EK[t] != (byte)EcoKind.Blueprint) return false;
                    FreeEnt(t); return true;
                case EcoCmd.StockpileAcceptAll: if (!tv || EK[t] != (byte)EcoKind.Container) return false; CMode[t] = 0; return true;
                case EcoCmd.StockpileAcceptOnly: if (!tv || EK[t] != (byte)EcoKind.Container) return false; CMode[t] = 1; CMask[t] = c.P0; return true;
                case EcoCmd.StockpileKeepEmpty: if (!tv || EK[t] != (byte)EcoKind.Container) return false; CMode[t] = 2; return true;
                case EcoCmd.AddExclusion: if (!tv || EK[t] != (byte)EcoKind.Container || (uint)c.P0 >= NI) return false; CExcl[t] |= 1 << c.P0; return true;
                case EcoCmd.RemoveExclusion: if (!tv || EK[t] != (byte)EcoKind.Container || (uint)c.P0 >= NI) return false; CExcl[t] &= ~(1 << c.P0); return true;
                case EcoCmd.SetAutoConvert: if (!tv || EK[t] != (byte)EcoKind.Container) return false; CAuto[t] = c.P0 != 0; if (CAuto[t]) AutoConvert(t); return true;
                case EcoCmd.ManualConversion: if (!tv || EK[t] != (byte)EcoKind.Container) return false; return DoConvert(t, false);
                case EcoCmd.CraftingQueueRecipe:
                case EcoCmd.CraftingProduce:
                    {
                        if (!tv || EK[t] != (byte)EcoKind.Station || (uint)c.P0 >= EcoDefs.RCount || EcoDefs.RStation[c.P0] != EA[t] || SQLen[t] >= SQCap) return false;
                        int q = c.P1 > 0 ? c.P1 : 1; int n = SQLen[t]++;
                        SQRec[t * SQCap + n] = c.P0; SQQty[t * SQCap + n] = q; SQOrig[t * SQCap + n] = q;
                        Poke(t); return true;
                    }
                case EcoCmd.CraftingLoop: if (!tv || EK[t] != (byte)EcoKind.Station) return false; SLoop[t] = c.P0 != 0; return true;
                case EcoCmd.CraftingDoNothing:
                case EcoCmd.CraftingRemoveProduction: if (!tv || EK[t] != (byte)EcoKind.Station) return false; SQLen[t] = 0; return true;
                case EcoCmd.CraftingRemoveAt:
                    {
                        if (!tv || EK[t] != (byte)EcoKind.Station || (uint)c.P0 >= (uint)SQLen[t]) return false;
                        for (int i = c.P0; i < SQLen[t] - 1; i++) { int a = t * SQCap + i; SQRec[a] = SQRec[a + 1]; SQQty[a] = SQQty[a + 1]; SQOrig[a] = SQOrig[a + 1]; }
                        SQLen[t]--; return true;
                    }
                case EcoCmd.CraftingAssignWorker:   // lab mapping [guess]: P0 = AutoStore flag
                    if (!tv || EK[t] != (byte)EcoKind.Station) return false; SAutoStore[t] = c.P0 != 0; return true;
            }
            int u = c.Actor - 1;
            if ((uint)u >= NU) return false;
            switch (c.Cmd)
            {
                case EcoCmd.Stop: UQLen[u] = 0; UQHead[u] = 0; AbortMotion(u); return true;
                case EcoCmd.SetAutonomy: UAutonomy[u] = (byte)c.P0; return true;   // FreeAgent == WaitForOrder in this sim
                case EcoCmd.ChangeProject: UProject[u] = c.Target; return true;     // no automatic behaviour; stored only [guess]
            }
            EcoOrder o;
            if (!Decode(u, c, out o)) return false;
            OrdersIssued++;
            if (!queue)
            {
                UQLen[u] = 0; UQHead[u] = 0; AbortMotion(u);
                return StartOrder(u, o);
            }
            if (UState[u] == 0 && UQLen[u] == 0) return StartOrder(u, o);
            if (UQLen[u] >= QCap) return false;
            UQ[u * QCap + (UQHead[u] + UQLen[u]) % QCap] = o; UQLen[u]++;
            return true;
        }

        /// <summary>Idle for a while (the unit stays put). Used by scripts when nothing useful can be ordered.</summary>
        public void Wait(int u, float seconds)
        {
            if (Phase != EcoPhase.Running) return;
            UQLen[u] = 0; UQHead[u] = 0; AbortMotion(u);
            StartOrder(u, new EcoOrder { Op = (byte)EcoOp.Wait, F0 = seconds });
        }

        void AbortMotion(int u)
        {
            if (UState[u] == 1 && UWake[u] > UT0[u])
            {
                float f = (Tick - UT0[u]) / (float)(UWake[u] - UT0[u]);
                if (f >= 0.5f) { UX[u] = UEx[u]; UY[u] = UEy[u]; UZ[u] = UEz[u]; }
            }
            UFromX[u] = UX[u]; UFromY[u] = UY[u]; UFromZ[u] = UZ[u];
            UState[u] = 0;
        }

        bool Decode(int u, EcoCommand c, out EcoOrder o)
        {
            o = default(EcoOrder);
            int t = c.Target;
            bool tv = t > 0 && t < NEnt && EK[t] != 0;
            int gx = c.X - World.OriginX, gy = c.Y - World.OriginY, gz = c.Z - World.OriginZ;
            o.Target = t; o.P0 = c.P0; o.P1 = c.P1; o.F0 = c.F0;
            bool carry = UCarryQty[u] > 0;
            switch (c.Cmd)
            {
                case EcoCmd.GoAndJumpTo:
                case EcoCmd.AddMoveToGoal:
                    return MoveOrder(gx, gy, gz, ref o);
                case EcoCmd.BatchedContextualActionVector:
                case EcoCmd.AddBreakVoxelGoal:
                    if (!World.InBounds(gx, gy, gz)) return false;
                    if (T[Nav.Idx(gx, gy, gz)] != 0) { o.Op = (byte)EcoOp.Dig; o.X = gx; o.Y = gy; o.Z = gz; return true; }
                    if (c.Cmd == EcoCmd.AddBreakVoxelGoal) return false;
                    return MoveOrder(gx, gy, gz, ref o);
                case EcoCmd.GoDropAt:
                    if (!carry) return false;
                    o.Op = (byte)EcoOp.Drop; o.X = gx; o.Y = gy; o.Z = gz; return World.InBounds(gx, gy, gz);
                case EcoCmd.AddTreeGoal: if (!tv || EK[t] != (byte)EcoKind.Tree) return false; o.Op = (byte)EcoOp.Chop; return true;
                case EcoCmd.AddNatureHarvestGoal: if (!tv || EK[t] != (byte)EcoKind.Nature) return false; o.Op = (byte)EcoOp.Harvest; return true;
                case EcoCmd.Pickup:
                    if (!tv) return false;
                    if (EK[t] == (byte)EcoKind.Item) { o.Op = (byte)EcoOp.Pickup; return true; }
                    if (EK[t] == (byte)EcoKind.Container || EK[t] == (byte)EcoKind.Rack || EK[t] == (byte)EcoKind.Station) { o.Op = (byte)EcoOp.Take; return true; }
                    return false;
                case EcoCmd.GoStoreInto:
                case EcoCmd.AddSmartTransferGoal:
                    if (!tv) return false;
                    return TargetCarryOrder(u, t, ref o, c.Cmd == EcoCmd.AddSmartTransferGoal);
                case EcoCmd.BatchedContextualActionGameObject:
                    if (!tv) return false;
                    switch ((EcoKind)EK[t])
                    {
                        case EcoKind.Tree: o.Op = (byte)EcoOp.Chop; return true;
                        case EcoKind.Nature: o.Op = (byte)EcoOp.Harvest; return true;
                        case EcoKind.Item: o.Op = (byte)EcoOp.Pickup; return true;
                        case EcoKind.Container:
                        case EcoKind.Rack: return TargetCarryOrder(u, t, ref o, true);
                        case EcoKind.Station:
                            if (carry && UCarryItem[u] == (int)EcoItem.Log && EA[t] == (int)EcoBuilding.ChoppingBlock) o.Op = (byte)EcoOp.Transform;
                            else if (carry) o.Op = (byte)EcoOp.AddRes;
                            else o.Op = (byte)EcoOp.Work;
                            return true;
                        case EcoKind.Blueprint: o.Op = (byte)(carry ? EcoOp.Deliver : EcoOp.Build); return true;
                        default: return false;
                    }
                case EcoCmd.TransformLog: if (!tv || EK[t] != (byte)EcoKind.Station) return false; o.Op = (byte)EcoOp.Transform; return true;
                case EcoCmd.CraftingWorkAt: if (!tv || EK[t] != (byte)EcoKind.Station) return false; o.Op = (byte)EcoOp.Work; return true;
                case EcoCmd.CraftingAddResources: if (!tv || EK[t] != (byte)EcoKind.Station) return false; o.Op = (byte)EcoOp.AddRes; return true;
                case EcoCmd.EquipTool:
                case EcoCmd.EquipToolRackContent:
                    if (!tv || UTool[u] != 0) return false;
                    if (EK[t] != (byte)EcoKind.Item && EK[t] != (byte)EcoKind.Rack) return false;
                    o.Op = (byte)EcoOp.Equip; return true;
                case EcoCmd.UnequipIntoRack: if (!tv || EK[t] != (byte)EcoKind.Rack || UTool[u] == 0) return false; o.Op = (byte)EcoOp.UnequipRack; return true;
                case EcoCmd.UnequipAllTools: if (UTool[u] == 0) return false; o.Op = (byte)EcoOp.UnequipDrop; return true;
                case EcoCmd.AddCleanGoal: return false;
                default: return false;
            }
        }

        bool MoveOrder(int gx, int gy, int gz, ref EcoOrder o)
        {
            int sx, sy, sz;
            if (!Nav.Snap(gx, gy, gz, out sx, out sy, out sz)) return false;
            o.Op = (byte)EcoOp.Move; o.X = sx; o.Y = sy; o.Z = sz; return true;
        }

        /// <summary>Container / rack / station / blueprint contextual: carrying -> store/add/deliver, else take.</summary>
        bool TargetCarryOrder(int u, int t, ref EcoOrder o, bool smart)
        {
            bool carry = UCarryQty[u] > 0;
            switch ((EcoKind)EK[t])
            {
                case EcoKind.Container:
                case EcoKind.Rack:
                    if (carry) { o.Op = (byte)EcoOp.Store; return true; }
                    if (!smart) return false;
                    o.Op = (byte)EcoOp.Take; return true;
                case EcoKind.Station: if (!carry) return false; o.Op = (byte)EcoOp.AddRes; return true;
                case EcoKind.Blueprint: if (!carry) return false; o.Op = (byte)EcoOp.Deliver; return true;
                default: return false;
            }
        }

        // =====================================================================================================
        // order state machine
        // =====================================================================================================
        bool StartOrder(int u, EcoOrder o)
        {
            UCur[u] = o;
            EcoOp op = (EcoOp)o.Op;
            if (op == EcoOp.Wait) { UState[u] = 2; UHit[u] = EcoTune.Ticks(o.F0 > 0 ? o.F0 : 1f); UWake[u] = Tick + UHit[u]; return true; }
            if (op == EcoOp.UnequipDrop) return BeginAct(u);
            int tx, ty, tz, r = 1, dmin = -1, dmax = 1;
            if (op == EcoOp.Move) { tx = o.X; ty = o.Y; tz = o.Z; r = 0; dmin = dmax = 0; }
            else if (op == EcoOp.Dig) { tx = o.X; ty = o.Y; tz = o.Z; dmin = -1; dmax = 2; }
            else if (op == EcoOp.Drop) { tx = o.X; ty = o.Y; tz = o.Z; }
            else
            {
                int t = o.Target;
                if (t <= 0 || t >= NEnt || EK[t] == 0) { Fail(u); return false; }
                tx = EX[t]; ty = EY[t]; tz = EZ[t];
                if (EK[t] == (byte)EcoKind.Station || EK[t] == (byte)EcoKind.Blueprint || EK[t] == (byte)EcoKind.Rack) r = ReachFor(EA[t]);
                else if (EK[t] == (byte)EcoKind.Container) r = EcoTune.BuildReach;
            }
            int ex, ey, ez;
            int cost = Nav.Path(UX[u], UY[u], UZ[u], tx, ty, tz, r, dmin, dmax, out ex, out ey, out ez);
            if (cost < 0) { Fail(u); return false; }
            if (cost == 0) return BeginAct(u);
            UState[u] = 1; UFromX[u] = UX[u]; UFromY[u] = UY[u]; UFromZ[u] = UZ[u]; UT0[u] = Tick;
            UEx[u] = ex; UEy[u] = ey; UEz[u] = ez;
            UWake[u] = Tick + EcoTune.Ticks(cost / 10f / EcoTune.WalkSpeed);
            return true;
        }

        void OnWake(int u)
        {
            if (UState[u] == 1)
            {
                UX[u] = UEx[u]; UY[u] = UEy[u]; UZ[u] = UEz[u]; UFromX[u] = UX[u]; UFromY[u] = UY[u]; UFromZ[u] = UZ[u];
                BeginAct(u);
                return;
            }
            int r = Hit(u);
            if (r < 0) Fail(u);
            else if (r == 0) CompleteOrder(u);
            else UWake[u] = Tick + UHit[u];
        }

        void CompleteOrder(int u)
        {
            UState[u] = 0;
            if (UQLen[u] > 0)
            {
                EcoOrder o = UQ[u * QCap + UQHead[u]];
                UQHead[u] = (UQHead[u] + 1) % QCap; UQLen[u]--;
                StartOrder(u, o);
            }
        }

        public readonly int[] FailOp = new int[32];
        public int LastFailOp, LastFailU;
        void Fail(int u)
        {
            FailOp[UCur[u].Op & 31]++; LastFailOp = UCur[u].Op; LastFailU = u;
            OrderFails++;
            UState[u] = 0; UQLen[u] = 0; UQHead[u] = 0;
        }

        void SetHit(int u, float sec) { UHit[u] = EcoTune.Ticks(sec); UState[u] = 2; UWake[u] = Tick + UHit[u]; }

        static int ResOfNature(int type, out int yield)
        {
            switch (type)
            {
                case 4: yield = EcoTune.HarvestYield; return (int)EcoItem.RawIron;
                case 8: yield = EcoTune.HarvestYield; return (int)EcoItem.OrangeCrystal;
                case 1: yield = 2; return (int)EcoItem.Fiber;
                default: yield = 4; return (int)EcoItem.Stones;
            }
        }

        int CarryRoom(int u, int item)
        {
            if (UCarryQty[u] > 0 && UCarryItem[u] != item) return 0;
            return EcoDefs.CarryCap[item] - (UCarryQty[u] > 0 ? UCarryQty[u] : 0);
        }

        int AddCarry(int u, int item, int qty)
        {
            int n = Math.Min(qty, CarryRoom(u, item));
            if (n <= 0) return 0;
            UCarryItem[u] = item; UCarryQty[u] += n; return n;
        }

        void ClearCarryIfEmpty(int u) { if (UCarryQty[u] <= 0) { UCarryQty[u] = 0; UCarryItem[u] = 0; } }

        /// <summary>Called when the unit has reached the target: validates and starts the first hit (state Acting). Fails the order if impossible.</summary>
        bool BeginAct(int u)
        {
            EcoOrder o = UCur[u];
            int t = o.Target;
            EcoOp op = (EcoOp)o.Op;
            switch (op)
            {
                case EcoOp.Move: CompleteOrder(u); return true;
                case EcoOp.Chop:
                    if (EK[t] != (byte)EcoKind.Tree) break;
                    SetHit(u, EcoTune.ChopHitSec); return true;
                case EcoOp.Dig:
                    {
                        int i = Nav.Idx(o.X, o.Y, o.Z);
                        if (T[i] == 0) break;
                        if (CarryRoom(u, (int)EcoItem.Stones) < EcoTune.DigYield) break;
                        SetHit(u, T[i] == EcoTerrain.Stone ? EcoTune.DigSecStone : EcoTune.DigSecEarth); return true;
                    }
                case EcoOp.Harvest:
                    {
                        if (EK[t] != (byte)EcoKind.Nature) break;
                        int y; int item = ResOfNature(EA[t], out y);
                        if (CarryRoom(u, item) < y) break;
                        SetHit(u, EcoTune.HarvestSec); return true;
                    }
                case EcoOp.Pickup:
                    if (EK[t] != (byte)EcoKind.Item || CarryRoom(u, EA[t]) <= 0) break;
                    SetHit(u, EcoTune.PickupSec); return true;
                case EcoOp.Take:
                    {
                        int item = TakeItem(u, t, o.P0);
                        if (item <= 0) break;
                        SetHit(u, EcoTune.TakeSec); return true;
                    }
                case EcoOp.Drop:
                    if (UCarryQty[u] <= 0) break;
                    SetHit(u, EcoTune.DropSec); return true;
                case EcoOp.Store:
                    if (UCarryQty[u] <= 0 || (EK[t] != (byte)EcoKind.Container && EK[t] != (byte)EcoKind.Rack) || !Accepts(t, UCarryItem[u]) || Room(t, UCarryItem[u]) <= 0) break;
                    SetHit(u, EcoTune.StoreSec); return true;
                case EcoOp.AddRes:
                    if (UCarryQty[u] <= 0 || EK[t] != (byte)EcoKind.Station || !StationUses(t, UCarryItem[u]) || Room(t, UCarryItem[u]) <= 0) break;
                    SetHit(u, EcoTune.StoreSec); return true;
                case EcoOp.Transform:
                    if (UCarryQty[u] <= 0 || UCarryItem[u] != (int)EcoItem.Log || EK[t] != (byte)EcoKind.Station || EA[t] != (int)EcoBuilding.ChoppingBlock) break;
                    UWorkLeft[u] = EcoDefs.RWork[EcoDefs.RPlanks];
                    SetHit(u, EcoTune.WorkHitSec); return true;
                case EcoOp.Work:
                    if (EK[t] != (byte)EcoKind.Station) break;
                    Poke(t);
                    if (!(SActive[t] && SWorkLeft[t] > 0)) break;
                    SetHit(u, EcoTune.WorkHitSec); return true;
                case EcoOp.Deliver:
                    if (EK[t] != (byte)EcoKind.Blueprint || !EValid[t] || UCarryQty[u] <= 0 || BpNeed(t, UCarryItem[u]) <= 0) break;
                    SetHit(u, EcoTune.DeliverSec); return true;
                case EcoOp.Build:
                    if (EK[t] != (byte)EcoKind.Blueprint || !EValid[t] || BpMissing(t)) break;
                    SetHit(u, EcoTune.BuildHitSec); return true;
                case EcoOp.Equip:
                    if (UTool[u] != 0) break;
                    if (EK[t] == (byte)EcoKind.Item) { if (!EcoDefs.IsTool(EA[t])) break; }
                    else if (EK[t] == (byte)EcoKind.Rack) { if (o.P0 <= 0 || ECont[t * NI + o.P0] <= 0 || !EcoDefs.IsTool(o.P0)) break; }
                    else break;
                    SetHit(u, EcoTune.EquipSec); return true;
                case EcoOp.UnequipRack:
                    if (UTool[u] == 0 || EK[t] != (byte)EcoKind.Rack || Room(t, UTool[u]) <= 0) break;
                    SetHit(u, EcoTune.EquipSec); return true;
                case EcoOp.UnequipDrop:
                    if (UTool[u] == 0) break;
                    SetHit(u, EcoTune.DropSec); return true;
            }
            Fail(u);
            return false;
        }

        /// <summary>Item that a Take from container t would move (0 = nothing possible).</summary>
        int TakeItem(int u, int t, int want)
        {
            if (EK[t] != (byte)EcoKind.Container && EK[t] != (byte)EcoKind.Rack && EK[t] != (byte)EcoKind.Station) return 0;
            if (want > 0) return ((uint)want < NI && ECont[t * NI + want] > 0 && CarryRoom(u, want) > 0) ? want : 0;
            for (int i = 1; i < NI; i++) if (ECont[t * NI + i] > 0 && CarryRoom(u, i) > 0) return i;
            return 0;
        }

        /// <summary>Executes one hit. Returns 0 = order finished, 1 = more hits follow, -1 = fail.</summary>
        int Hit(int u)
        {
            EcoOrder o = UCur[u];
            int t = o.Target;
            switch ((EcoOp)o.Op)
            {
                case EcoOp.Wait: return 0;
                case EcoOp.Chop:
                    {
                        if (EK[t] != (byte)EcoKind.Tree) return 0;
                        if (--EB[t] > 0) return 1;
                        Ob[Nav.Idx(EX[t], EY[t], EZ[t])] = 0; Nav.Version++;
                        DropItem((int)EcoItem.Log, EcoTune.LogsPerTree, EX[t], EY[t], EZ[t]);
                        FreeEnt(t);
                        Count(CLogs, EcoTune.LogsPerTree); Mile(0);
                        return 0;
                    }
                case EcoOp.Dig:
                    {
                        int i = Nav.Idx(o.X, o.Y, o.Z);
                        if (T[i] == 0) return 0;
                        SetTerrain(o.X, o.Y, o.Z, 0);
                        int n = AddCarry(u, (int)EcoItem.Stones, EcoTune.DigYield);
                        if (n < EcoTune.DigYield) DropItem((int)EcoItem.Stones, EcoTune.DigYield - n, UX[u], UY[u], UZ[u]);
                        Count(CStones, EcoTune.DigYield);
                        return 0;
                    }
                case EcoOp.Harvest:
                    {
                        if (EK[t] != (byte)EcoKind.Nature) return 0;
                        int y; int item = ResOfNature(EA[t], out y);
                        int n = AddCarry(u, item, y);
                        if (n < y) return n > 0 ? 0 : -1;
                        if (item == (int)EcoItem.RawIron) { Count(CRawIron, y); Mile(14); }
                        else if (item == (int)EcoItem.OrangeCrystal) { Count(CCrystal, y); Mile(15); }
                        if (--EB[t] <= 0) { Ob[Nav.Idx(EX[t], EY[t], EZ[t])] = 0; Nav.Version++; FreeEnt(t); return 0; }
                        return CarryRoom(u, item) >= y ? 1 : 0;
                    }
                case EcoOp.Pickup:
                    {
                        if (EK[t] != (byte)EcoKind.Item) return -1;
                        int want = o.P1 > 0 ? o.P1 : int.MaxValue;
                        int n = AddCarry(u, EA[t], Math.Min(EB[t], want));
                        if (n <= 0) return -1;
                        EB[t] -= n; if (EB[t] <= 0) FreeEnt(t);
                        return 0;
                    }
                case EcoOp.Take:
                    {
                        int item = TakeItem(u, t, o.P0);
                        if (item <= 0) return -1;
                        int want = o.P1 > 0 ? o.P1 : int.MaxValue;
                        int n = AddCarry(u, item, Math.Min(ECont[t * NI + item], want));
                        ECont[t * NI + item] -= n;
                        if (EK[t] == (byte)EcoKind.Rack) RecountRacks();
                        return 0;
                    }
                case EcoOp.Drop:
                    DropItem(UCarryItem[u], UCarryQty[u], o.X, o.Y, o.Z);
                    UCarryQty[u] = 0; UCarryItem[u] = 0; return 0;
                case EcoOp.Store:
                    {
                        if (UCarryQty[u] <= 0 || EK[t] == 0) return -1;
                        int n = ContAdd(t, UCarryItem[u], UCarryQty[u]);
                        if (n <= 0) return -1;
                        UCarryQty[u] -= n; ClearCarryIfEmpty(u); return 0;
                    }
                case EcoOp.AddRes:
                    {
                        if (UCarryQty[u] <= 0 || EK[t] != (byte)EcoKind.Station) return -1;
                        int item = UCarryItem[u];
                        int n = ContAdd(t, item, UCarryQty[u]);
                        if (n <= 0) return -1;
                        UCarryQty[u] -= n; ClearCarryIfEmpty(u);
                        if (item == (int)EcoItem.Log) Mile(13);
                        if (EA[t] == (int)EcoBuilding.Furnace && (item == (int)EcoItem.RawIron || item == (int)EcoItem.OrangeCrystal)) Mile(16);
                        Poke(t); TryHeat(t);
                        return 0;
                    }
                case EcoOp.Transform:
                    {
                        if (UCarryItem[u] != (int)EcoItem.Log) return -1;
                        Count(CWorkHits, 1);
                        if (--UWorkLeft[u] > 0) return 1;
                        int q = EcoDefs.ROutQ[EcoDefs.RPlanks];
                        UCarryQty[u] -= 1;
                        ClearCarryIfEmpty(u);
                        int n = AddCarry(u, (int)EcoItem.Plank, q);
                        if (n < q) DropItem((int)EcoItem.Plank, q - n, UX[u], UY[u], UZ[u]);
                        Count(CPlanks, q); Mile(2);
                        return 0;
                    }
                case EcoOp.Work:
                    {
                        if (!(SActive[t] && SWorkLeft[t] > 0)) return 0;
                        SWorkLeft[t]--; Count(CWorkHits, 1);
                        if (SWorkLeft[t] == 0 && SHeatLeft[t] == 0) Finish(t);
                        return (SActive[t] && SWorkLeft[t] > 0) ? 1 : 0;
                    }
                case EcoOp.Deliver:
                    {
                        if (EK[t] != (byte)EcoKind.Blueprint || UCarryQty[u] <= 0) return -1;
                        int item = UCarryItem[u];
                        int n = Math.Min(UCarryQty[u], BpNeed(t, item));
                        if (n <= 0) return -1;
                        ECont[t * NI + item] += n; UCarryQty[u] -= n; ClearCarryIfEmpty(u);
                        Count(CDelivered, n);
                        if (item == (int)EcoItem.Log) Mile(13);
                        return 0;
                    }
                case EcoOp.Build:
                    {
                        if (EK[t] != (byte)EcoKind.Blueprint) return 0;
                        EB[t]++; Count(CBuildHits, 1);
                        if (EB[t] >= EcoDefs.BHits[EA[t]]) { CompleteBuilding(t); return 0; }
                        return 1;
                    }
                case EcoOp.Equip:
                    {
                        if (UTool[u] != 0) return -1;
                        if (EK[t] == (byte)EcoKind.Item) { UTool[u] = EA[t]; if (--EB[t] <= 0) FreeEnt(t); }
                        else if (EK[t] == (byte)EcoKind.Rack && ECont[t * NI + o.P0] > 0) { UTool[u] = o.P0; ECont[t * NI + o.P0]--; RecountRacks(); }
                        else return -1;
                        return 0;
                    }
                case EcoOp.UnequipRack:
                    if (UTool[u] == 0 || ContAdd(t, UTool[u], 1) <= 0) return -1;
                    UTool[u] = 0; return 0;
                case EcoOp.UnequipDrop:
                    DropItem(UTool[u], 1, UX[u], UY[u], UZ[u]); UTool[u] = 0; return 0;
            }
            return -1;
        }

        // =====================================================================================================
        // blueprints / buildings
        // =====================================================================================================
        /// <summary>Blueprints may be placed anywhere in bounds (the game shows invalid ones red). Valid = CanPlace (footprint rules, [guess] reachable). Invalid ones cost
        /// InvalidBlueprintPenalty (counted in InvalidPlacements) and can never be built. A blueprint is only a marker: no obstacle, no interaction with workers [user].
        /// Stockpile is not a blueprint (see AccepterPosition).</summary>
        bool PlaceBlueprint(int x, int y, int z, int building)
        {
            if (building <= 1 || building >= (int)EcoBuilding.Count) return false;
            if (!World.InBounds(x, y, z)) return false;
            bool valid = CanPlace(x, y, z, building);
            int id = NewEnt(EcoKind.Blueprint, x, y, z);
            if (id < 0) return false;
            EA[id] = building; EValid[id] = valid;
            if (!valid) InvalidPlacements++;
            return true;
        }

        /// <summary>CmdAccepterPosition: creates a stockpile zone container at a stand cell (no cost, no build, no obstacle). Returns the entity id or 0.</summary>
        int MakeZone(int x, int y, int z)
        {
            if (!Nav.Standable(x, y, z)) return 0;
            for (int i = 1; i < NEnt; i++) if (EK[i] == (byte)EcoKind.Container && EX[i] == x && EY[i] == y && EZ[i] == z) return 0;
            int id = NewEnt(EcoKind.Container, x, y, z);
            if (id < 0) return 0;
            EA[id] = (int)EcoBuilding.Stockpile; CAuto[id] = true; EB[id] = 1;   // EB = zone radius [guess: informational]
            Mile(3); LastGainTick = Tick;
            return id;
        }

        /// <summary>Items of kind 'item' still missing for blueprint bp (4 cost slots, [measured] costs).</summary>
        public int BpNeed(int bp, int item)
        {
            int b = EA[bp], need = 0;
            for (int k = 0; k < EcoDefs.CostSlots; k++) if (item != 0 && EcoDefs.BCostItem[b][k] == item) need += EcoDefs.BCostQty[b][k];
            return Math.Max(0, need - ECont[bp * NI + item]);
        }

        public bool BpMissing(int bp)
        {
            int b = EA[bp];
            for (int k = 0; k < EcoDefs.CostSlots; k++) { int it = EcoDefs.BCostItem[b][k]; if (it != 0 && BpNeed(bp, it) > 0) return true; }
            return false;
        }

        /// <summary>Footprint cell k of building b placed at (x,y,z).</summary>
        public static void FootCell(int b, int k, int x, int y, int z, out int cx, out int cy, out int cz)
        {
            cx = x + EcoDefs.FootX[b][k]; cy = y + EcoDefs.FootY[b][k]; cz = z + EcoDefs.FootZ[b][k];
        }

        /// <summary>Game rule as far as known: every footprint cell in bounds, free of terrain and of other buildings/trees/nodes; cells at the placement height need ground below.
        /// Plus [guess] reachable by some worker. Pure query (no side effects).</summary>
        public bool CanPlace(int x, int y, int z, int building)
        {
            if (building <= 1 || building >= (int)EcoBuilding.Count) return false;
            for (int k = 0; k < EcoDefs.FootN[building]; k++)
            {
                int cx, cy, cz; FootCell(building, k, x, y, z, out cx, out cy, out cz);
                if (!World.InBounds(cx, cy, cz) || cy < 1) return false;
                int i = Nav.Idx(cx, cy, cz);
                if (T[i] != 0 || B[i] != 0 || Ob[i] != 0) return false;
                if (EcoDefs.FootY[building][k] == 0 && T[i - 1] == 0 && B[i - 1] == 0) return false;
            }
            int ex, ey, ez;
            for (int u = 0; u < NU; u++) if (Nav.Path(UX[u], UY[u], UZ[u], x, y, z, ReachFor(building), -1, 2, out ex, out ey, out ez) >= 0) return true;
            return false;
        }

        /// <summary>Horizontal reach to a building: 1 for single cells, 2 for multi-cell footprints [guess].</summary>
        public static int ReachFor(int building) { return (building > 0 && EcoDefs.FootN[building] > 1) ? 2 : EcoTune.BuildReach; }

        void SetFootprintOb(int id, byte v)
        {
            if (EK[id] == (byte)EcoKind.Station || EK[id] == (byte)EcoKind.Rack)
            {
                int b = EA[id];
                for (int k = 0; k < EcoDefs.FootN[b]; k++)
                {
                    int cx, cy, cz; FootCell(b, k, EX[id], EY[id], EZ[id], out cx, out cy, out cz);
                    if (World.InBounds(cx, cy, cz)) Ob[Nav.Idx(cx, cy, cz)] = v;
                }
            }
            else if (World.InBounds(EX[id], EY[id], EZ[id])) Ob[Nav.Idx(EX[id], EY[id], EZ[id])] = v;
        }

        void CompleteBuilding(int bp)
        {
            int b = EA[bp];
            Array.Clear(ECont, bp * NI, NI);
            EK[bp] = (byte)EcoDefs.KindOfBuilding(b); EB[bp] = 0;
            CAuto[bp] = b == (int)EcoBuilding.Stockpile;
            switch ((EcoBuilding)b)
            {
                case EcoBuilding.Stockpile: Mile(3); break;
                case EcoBuilding.ChoppingBlock: Mile(1); break;
                case EcoBuilding.Furnace: Mile(5); break;
                case EcoBuilding.WeaponRack: Mile(7); break;
                case EcoBuilding.Workbench: Mile(8); break;
                case EcoBuilding.Altar: Mile(11); break;
            }
            SetFootprintOb(bp, 1); Nav.Version++;
            for (int u = 0; u < NU; u++)
            {
                int ci = Nav.Idx(UX[u], UY[u], UZ[u]);
                if (UState[u] != 1 && (Ob[ci] != 0 || Ob[ci + 1] != 0))
                {
                    int sx, sy, sz;
                    if (Nav.Snap(UX[u], UY[u], UZ[u], out sx, out sy, out sz)) { UX[u] = sx; UY[u] = sy; UZ[u] = sz; UFromX[u] = sx; UFromY[u] = sy; UFromZ[u] = sz; UnitsPushed++; }
                }
            }
            LastGainTick = Tick;
        }

        // =====================================================================================================
        // crafting stations
        // =====================================================================================================
        bool StationUses(int st, int item)
        {
            int b = EA[st];
            if (b == (int)EcoBuilding.Furnace && item == (int)EcoItem.OrangeCrystal) return true;   // fuel
            for (int r = 0; r < EcoDefs.RCount; r++)
                if (EcoDefs.RStation[r] == b && (EcoDefs.RIn0[r] == item || (EcoDefs.RIn1[r] != 0 && EcoDefs.RIn1[r] == item))) return true;
            return false;
        }

        void Poke(int st)
        {
            if (SActive[st] || SQLen[st] == 0) return;
            int r = SQRec[st * SQCap];
            if (ECont[st * NI + EcoDefs.RIn0[r]] < EcoDefs.RQ0[r]) return;
            if (EcoDefs.RIn1[r] != 0 && ECont[st * NI + EcoDefs.RIn1[r]] < EcoDefs.RQ1[r]) return;
            ECont[st * NI + EcoDefs.RIn0[r]] -= EcoDefs.RQ0[r];
            if (EcoDefs.RIn1[r] != 0) ECont[st * NI + EcoDefs.RIn1[r]] -= EcoDefs.RQ1[r];
            SActive[st] = true; SCur[st] = r; SWorkLeft[st] = EcoDefs.RWork[r]; SHeatLeft[st] = EcoDefs.RHeat[r];
            if (r == EcoDefs.RIronIngot) Mile(6);
            if (SWorkLeft[st] == 0 && SHeatLeft[st] == 0) { Finish(st); return; }
            TryHeat(st);
        }

        void TryHeat(int st)
        {
            if (!SActive[st] || SHeatLeft[st] <= 0 || SHeatWake[st] != 0) return;
            int f = st * NI + (int)EcoItem.OrangeCrystal;
            if (ECont[f] < 1) return;
            ECont[f]--;
            SHeatWake[st] = Tick + EcoTune.Ticks(EcoTune.HeatSecPerUnit);
        }

        void HeatTick(int st)
        {
            SHeatWake[st] = 0;
            if (!SActive[st]) return;
            SHeatLeft[st]--; Count(CHeat, 1);
            if (SHeatLeft[st] > 0) TryHeat(st);
            else if (SWorkLeft[st] == 0) Finish(st);
        }

        void Finish(int st)
        {
            int r = SCur[st];
            int outItem = EcoDefs.ROut[r], q = EcoDefs.ROutQ[r];
            ECont[st * NI + outItem] += q;
            SActive[st] = false;
            switch (r)
            {
                case EcoDefs.RPlanks: Count(CPlanks, q); Mile(2); break;
                case EcoDefs.RIronIngot: Count(CIngots, q); Mile(17); break;
                case EcoDefs.RPoleaxe: Mile(9); LastGainTick = Tick; break;
                case EcoDefs.RKettleHat: Mile(10); LastGainTick = Tick; break;
                case EcoDefs.RCaptureWard:
                    WardsMade++; Mile(12); Mile(MWard1 + Math.Min(WardsMade, 2) - 1); LastGainTick = Tick; break;
            }
            // queue bookkeeping
            if (SQLen[st] > 0)
            {
                int a = st * SQCap;
                if (--SQQty[a] <= 0)
                {
                    int rec = SQRec[a], orig = SQOrig[a];
                    for (int i = 0; i < SQLen[st] - 1; i++) { SQRec[a + i] = SQRec[a + i + 1]; SQQty[a + i] = SQQty[a + i + 1]; SQOrig[a + i] = SQOrig[a + i + 1]; }
                    SQLen[st]--;
                    if (SLoop[st]) { int n = SQLen[st]++; SQRec[a + n] = rec; SQQty[a + n] = orig; SQOrig[a + n] = orig; }
                }
            }
            if (SAutoStore[st]) AutoStoreOut(st, outItem);
            Poke(st);
        }

        void AutoStoreOut(int st, int item)
        {
            int best = -1, bd = int.MaxValue;
            for (int i = 1; i < NEnt; i++)
            {
                if (EK[i] != (byte)EcoKind.Container && EK[i] != (byte)EcoKind.Rack) continue;
                if (!Accepts(i, item) || Room(i, item) <= 0) continue;
                int d = Math.Max(Math.Abs(EX[i] - EX[st]), Math.Abs(EZ[i] - EZ[st]));
                if (d < bd) { bd = d; best = i; }
            }
            if (best < 0) return;
            int n = Math.Min(ECont[st * NI + item], Room(best, item));
            if (n <= 0) return;
            ECont[st * NI + item] -= n;
            ContAdd(best, item, n);
        }
    }
}
