using System;
using System.IO;

namespace Btc.EcoSim
{
    /// <summary>Reusable snapshot buffer (no per-call allocation after warm-up).</summary>
    public sealed class EcoSnapshotBuf
    {
        public readonly MemoryStream Ms = new MemoryStream(1 << 17);
        public readonly BinaryWriter W;
        public readonly BinaryReader R;
        public EcoSnapshotBuf() { W = new BinaryWriter(Ms); R = new BinaryReader(Ms); }
        public int Length { get { return (int)Ms.Length; } }
    }

    public sealed partial class EcoSimCore
    {
        bool ObstacleEnt(int i) { return HasObstacle(EK[i]) && !(EK[i] == (byte)EcoKind.Nature && EA[i] == 1); }

        static bool HasCont(byte k) { return k == (byte)EcoKind.Container || k == (byte)EcoKind.Station || k == (byte)EcoKind.Rack || k == (byte)EcoKind.Blueprint; }

        static bool HasObstacle(byte k)
        {
            return k == (byte)EcoKind.Tree || k == (byte)EcoKind.Nature || k == (byte)EcoKind.Crystal
                || k == (byte)EcoKind.Station || k == (byte)EcoKind.Rack;
        }

        static void WInts(BinaryWriter w, int[] a, int off, int n) { for (int i = 0; i < n; i++) w.Write(a[off + i]); }
        static void RInts(BinaryReader r, int[] a, int off, int n) { for (int i = 0; i < n; i++) a[off + i] = r.ReadInt32(); }
        static void WOrder(BinaryWriter w, EcoOrder o) { w.Write(o.Op); w.Write(o.Target); w.Write(o.X); w.Write(o.Y); w.Write(o.Z); w.Write(o.P0); w.Write(o.P1); w.Write(o.F0); }
        static EcoOrder ROrder(BinaryReader r)
        {
            EcoOrder o; o.Op = r.ReadByte(); o.Target = r.ReadInt32(); o.X = r.ReadInt32(); o.Y = r.ReadInt32(); o.Z = r.ReadInt32(); o.P0 = r.ReadInt32(); o.P1 = r.ReadInt32(); o.F0 = r.ReadSingle();
            return o;
        }

        /// <summary>Full state snapshot: voxel edits as a sparse (index,value) list, entities, units incl. order state, containers, stations,
        /// counters, milestones, RNG, clock. Load restores a StateHash-identical state. Valid only for the same loaded EcoWorld.</summary>
        public void Save(EcoSnapshotBuf b)
        {
            b.Ms.Position = 0; b.Ms.SetLength(0);
            var w = b.W;
            w.Write(Tick); w.Write(accum); w.Write(rng); w.Write((byte)Phase); w.Write(TerrHash); w.Write(DoneTick); w.Write(LastGainTick);
            w.Write(InvalidPlacements); w.Write(UnitsPushed); w.Write(Kits); w.Write(WardsMade); w.Write(OrderFails); w.Write(OrdersIssued);
            WInts(w, Cur, 0, CCount); WInts(w, Max, 0, CCount); WInts(w, MileTick, 0, MCount);
            w.Write(EditN);
            for (int k = 0; k < EditN; k++) { int i = EditIdx[k]; w.Write(i); w.Write(T[i]); }
            for (int u = 0; u < NU; u++)
            {
                w.Write(UX[u]); w.Write(UY[u]); w.Write(UZ[u]); w.Write(UFromX[u]); w.Write(UFromY[u]); w.Write(UFromZ[u]); w.Write(UT0[u]);
                w.Write(UState[u]); w.Write(UAutonomy[u]); w.Write(UCarryItem[u]); w.Write(UCarryQty[u]); w.Write(UTool[u]); w.Write(UProject[u]);
                w.Write(UWake[u]); w.Write(UPlaced[u]); w.Write(UQHead[u]); w.Write(UQLen[u]);
                w.Write(UEx[u]); w.Write(UEy[u]); w.Write(UEz[u]); w.Write(UWorkLeft[u]); w.Write(UHit[u]);
                WOrder(w, UCur[u]);
                for (int q = 0; q < QCap; q++) WOrder(w, UQ[u * QCap + q]);
            }
            w.Write(NEnt); w.Write(freeN);
            for (int i = 0; i < freeN; i++) w.Write(freeStack[i]);
            for (int i = 1; i < NEnt; i++)
            {
                w.Write(EK[i]);
                if (EK[i] == 0) continue;
                w.Write(EX[i]); w.Write(EY[i]); w.Write(EZ[i]); w.Write(EA[i]); w.Write(EB[i]);
                w.Write(EValid[i]);
                if (HasCont(EK[i])) WInts(w, ECont, i * NI, NI);
                if (EK[i] == (byte)EcoKind.Container) { w.Write(CMode[i]); w.Write(CMask[i]); w.Write(CExcl[i]); w.Write(CAuto[i]); }
                else if (EK[i] == (byte)EcoKind.Station)
                {
                    w.Write(SQLen[i]); WInts(w, SQRec, i * SQCap, SQCap); WInts(w, SQQty, i * SQCap, SQCap); WInts(w, SQOrig, i * SQCap, SQCap);
                    w.Write(SLoop[i]); w.Write(SAutoStore[i]); w.Write(SActive[i]); w.Write(SWorkLeft[i]); w.Write(SHeatLeft[i]); w.Write(SHeatWake[i]); w.Write(SCur[i]);
                }
            }
        }

        public void Load(EcoSnapshotBuf b)
        {
            b.Ms.Position = 0;
            var r = b.R;
            for (int k = 0; k < EditN; k++) T[EditIdx[k]] = World.Terrain[EditIdx[k]];
            for (int i = 1; i < NEnt; i++) if (ObstacleEnt(i)) SetFootprintOb(i, 0);
            Tick = r.ReadInt32(); accum = r.ReadSingle(); rng = r.ReadUInt64(); Phase = (EcoPhase)r.ReadByte(); TerrHash = r.ReadUInt64(); DoneTick = r.ReadInt32(); LastGainTick = r.ReadInt32();
            InvalidPlacements = r.ReadInt32(); UnitsPushed = r.ReadInt32(); Kits = r.ReadInt32(); WardsMade = r.ReadInt32(); OrderFails = r.ReadInt32(); OrdersIssued = r.ReadInt32();
            RInts(r, Cur, 0, CCount); RInts(r, Max, 0, CCount); RInts(r, MileTick, 0, MCount);
            EditN = r.ReadInt32();
            for (int k = 0; k < EditN; k++) { int i = r.ReadInt32(); EditIdx[k] = i; T[i] = r.ReadByte(); }
            for (int u = 0; u < NU; u++)
            {
                UX[u] = r.ReadInt32(); UY[u] = r.ReadInt32(); UZ[u] = r.ReadInt32(); UFromX[u] = r.ReadInt32(); UFromY[u] = r.ReadInt32(); UFromZ[u] = r.ReadInt32(); UT0[u] = r.ReadInt32();
                UState[u] = r.ReadByte(); UAutonomy[u] = r.ReadByte(); UCarryItem[u] = r.ReadInt32(); UCarryQty[u] = r.ReadInt32(); UTool[u] = r.ReadInt32(); UProject[u] = r.ReadInt32();
                UWake[u] = r.ReadInt32(); UPlaced[u] = r.ReadBoolean(); UQHead[u] = r.ReadInt32(); UQLen[u] = r.ReadInt32();
                UEx[u] = r.ReadInt32(); UEy[u] = r.ReadInt32(); UEz[u] = r.ReadInt32(); UWorkLeft[u] = r.ReadInt32(); UHit[u] = r.ReadInt32();
                UCur[u] = ROrder(r);
                for (int q = 0; q < QCap; q++) UQ[u * QCap + q] = ROrder(r);
            }
            int oldN = NEnt;
            NEnt = r.ReadInt32(); freeN = r.ReadInt32();
            for (int i = 0; i < freeN; i++) freeStack[i] = r.ReadInt32();
            for (int i = NEnt; i < oldN; i++) EK[i] = 0;
            for (int i = 1; i < NEnt; i++)
            {
                EK[i] = r.ReadByte();
                if (EK[i] == 0) continue;
                EX[i] = r.ReadInt32(); EY[i] = r.ReadInt32(); EZ[i] = r.ReadInt32(); EA[i] = r.ReadInt32(); EB[i] = r.ReadInt32();
                EValid[i] = r.ReadBoolean();
                if (HasCont(EK[i])) RInts(r, ECont, i * NI, NI); else if (EK[i] == (byte)EcoKind.Item || EK[i] == (byte)EcoKind.Tree || EK[i] == (byte)EcoKind.Nature || EK[i] == (byte)EcoKind.Crystal) Array.Clear(ECont, i * NI, NI);
                if (EK[i] == (byte)EcoKind.Container) { CMode[i] = r.ReadByte(); CMask[i] = r.ReadInt32(); CExcl[i] = r.ReadInt32(); CAuto[i] = r.ReadBoolean(); }
                else if (EK[i] == (byte)EcoKind.Station)
                {
                    SQLen[i] = r.ReadInt32(); RInts(r, SQRec, i * SQCap, SQCap); RInts(r, SQQty, i * SQCap, SQCap); RInts(r, SQOrig, i * SQCap, SQCap);
                    SLoop[i] = r.ReadBoolean(); SAutoStore[i] = r.ReadBoolean(); SActive[i] = r.ReadBoolean(); SWorkLeft[i] = r.ReadInt32(); SHeatLeft[i] = r.ReadInt32(); SHeatWake[i] = r.ReadInt32(); SCur[i] = r.ReadInt32();
                }
                if (ObstacleEnt(i)) SetFootprintOb(i, 1);
            }
            Nav.Version++;
        }
    }
}
