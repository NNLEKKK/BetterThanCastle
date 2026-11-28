using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Engine;
using Brix.Engine.Nature;
using Brix.Engine.Volumes;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Network;
using Brix.Network;
using Unity;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// Terraforming task (docs/terraforming.md). A terraform task is a vanilla Flatten (LandscapeProject) whose goal provider
    /// walks a custom voxel set instead of its bounds: cells to dig and cells to fill, built from box operations (brushes).
    /// So priority, crew, delete, task bar and save of the task stay vanilla; Patches/TerraformPatches.cs swaps the voxel
    /// decision, the dig/fill type of each goal and the harvest toggles. Host-authoritative, BetterConnection ON only:
    /// msg 112 client->host (create), msg 113 host->clients (shape of a task); both chunked. Shapes are saved next to the
    /// save (btc-terraform.txt) and matched back to their Flatten task by bounds on load.
    /// </summary>
    internal static class BtcTerraform
    {
        internal const short MsgCreate = 112;
        internal const short MsgShape = 113;
        internal const int MaxCells = 40000;
        internal const int MaxOps = 4000;
        private const string FileName = "btc-terraform.txt";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { return BtcNetworkGate.Enabled; } }

        // ------------------------------------------------------------------ model
        internal struct Op
        {
            internal char K;   // 'D' dig, 'F' fill, 'E' erase; ladders (one cell A, B = forward): 'T' tunnel, 'Q' quarry, 'B' quarry flat
            internal XYZ A, B; // inclusive corners (any order)
        }

        internal static bool IsLadderKind(char k) { return k == 'T' || k == 'Q' || k == 'B'; }

        /// <summary>A ladder cell. Forward points to the low side (vanilla BuildStairsGoal): you climb towards -F, onto
        /// cell - F + up. Tunnel ladder ('T') goes into an empty cell against a wall; quarry ladder ('Q') replaces a terrain
        /// block, 'B' is the flat quarry landing (forward = up, Mine_Stairs_Block).</summary>
        internal struct Ladder
        {
            internal char K;
            internal XYZ F;
        }

        /// <summary>Vanilla ladder rules now (TunnelProject.VoxelNeedsAStairGoal + StairsDirection, QuarryGoalProvider2
        /// .VoxelNeedsGoal + StairsComputer.StairIsValid): true when a BuildStairsGoal may be spawned on this cell.</summary>
        internal static bool LadderReady(XYZ p, Ladder l) { return LadderReady(p, l, null); }

        internal static bool LadderReady(XYZ p, Ladder l, Shape s)
        {
            if (QuarryGoalProvider2.IsStair(p) || Voxel.IsInvincible(p)) return false;
            // a crust above does not matter: BuildStairsGoal removes it (RemoveCroute; IsValid is patched to agree). A block
            // above that the task digs does not either: a worker can only work on cells next to where he stands and stands
            // IN a ramp, so on a staircase climbing out of a pit the room over the next ramp is only reachable from that ramp:
            // the ramp is built first, the block over it dug afterwards from the ramp
            XYZ up = p + XYZ.up;
            bool upFree = Voxel.IsEmpty(up) || Voxel.IsCrust(up) || (s != null && s.Dig.Contains(up) && !s.Ladders.ContainsKey(up));
            if (l.K == 'T')   // an empty cell or a crust (the ladder replaces it), never a full block
                return (Voxel.IsEmpty(p) || Voxel.IsCrust(p)) && Voxel.IsFull(p - XYZ.up) && Voxel.IsNonEmpty(p - l.F) && upFree;
            if (QuarryGoalProvider2.IsStair(p + XYZ.up) || !Voxel.IsFull(p) || Voxel.IsBlock(p)) return false;   // quarry: full blocks only, no crust
            if (NatureRegistry.HasNatureBlockerAt(p) || NatureRegistry.HasNatureBlockerAt(p + XYZ.up)) return false;
            XYZ b = p - XYZ.up;
            return (!Voxel.IsEmpty(b) || Architecte.IsUnderIsland(b)) && upFree;
        }

        /// <summary>True when a terraform task holds a ladder on this cell (host and clients).</summary>
        internal static bool IsLadderCell(XYZ p) { return LadderShape(p) != null; }

        /// <summary>The terraform shape that holds a ladder on this cell, or null.</summary>
        internal static Shape LadderShape(XYZ p)
        {
            if (ByObj.Count == 0) return null;
            foreach (KeyValuePair<int, Shape> kv in ByObj) if (kv.Value.Ladders.ContainsKey(p)) return kv.Value;
            return null;
        }

        /// <summary>Tool check: the same rules against the world as it will be once the shape is dug / filled
        /// (the cell above may still be dug out). A tunnel ladder without its two supports cannot be placed.</summary>
        internal static bool LadderPlaceable(XYZ p, char k, XYZ f, Shape s)
        {
            if (Voxel.IsInvincible(p) || QuarryGoalProvider2.IsStair(p)) return false;
            if (k == 'T') return !Full(p, s) && Full(p - XYZ.up, s) && Solid(p - f, s);   // air or crust, never a full block
            if (!Full(p, s) || Voxel.IsBlock(p) || !Voxel.IsTerrain(p)) return false;      // quarry ladder / landing: full blocks only
            XYZ b = p - XYZ.up;
            return Solid(b, s) || Architecte.IsUnderIsland(b);
        }

        internal static XYZ Mul(XYZ v, int k) { return new XYZ(v.x * k, v.y * k, v.z * k); }

        /// <summary>Ladder ops for one ladder: with autoDig the room it needs is dug too (quarry ladder: the cell above; tunnel
        /// ladder: its own cell unless it is crust, and the cell above).</summary>
        internal static void LadderOps(Shape s, XYZ p, char k, XYZ f, bool autoDig, List<Op> outOps) { LadderOps(s, p, k, f, autoDig, false, outOps); }

        // the room over each ramp of a staircase is dug from the ramp itself (LadderReady lets the ramp come first)
        private static void LadderOps(Shape s, XYZ p, char k, XYZ f, bool autoDig, bool chained, List<Op> outOps)
        {
            if (autoDig)
            {
                // a crust is never dug for room: the ladder build removes the crust over it, a worker walks through crust
                if (k == 'T' && Full(p, s)) outOps.Add(Cell('D', p));
                XYZ up = p + XYZ.up;
                if (k != 'B' && Rock(up, s)) outOps.Add(Cell('D', up));
            }
            Op o; o.K = k; o.A = p; o.B = k == 'B' ? XYZ.up : f;
            outOps.Add(o);
        }

        internal static Op Cell(char k, XYZ p) { Op o; o.K = k; o.A = p; o.B = p; return o; }

        /// <summary>A dragged chain of ladders from (p, f): quarry / tunnel ladders go one step up the climbing diagonal (-f + up)
        /// per ladder (n &lt; 0 goes down), landings along 'flat'. Fills ops (with the room each needs when autoDig) and, for the
        /// preview, every chain cell with whether it can be placed (checked against the draft plus the ladders before it).</summary>
        internal static void LadderChain(Shape draft, XYZ p, char k, XYZ f, XYZ flat, int n, bool autoDig,
                                         List<Op> outOps, List<XYZ> cells, List<bool> ok, bool row = false)
        {
            outOps.Clear(); cells.Clear(); ok.Clear();
            // row = a sideways row of ladders at one height (like landings), else one step up the climbing diagonal per ladder
            XYZ step = k == 'B' || row ? flat : new XYZ(-f.x, 1, -f.z);
            int sgn = n < 0 ? -1 : 1, count = Math.Min(Math.Abs(n), 64);
            Shape tmp = count > 0 ? draft.Clone() : draft;
            List<Op> one = new List<Op>(3);
            for (int i = 0; i <= count; i++)
            {
                XYZ c = p + Mul(step, i * sgn);
                one.Clear();
                // going up, ladder i sits above ladder i-1; going down, ladder i-1 sits above ladder i
                LadderOps(tmp, c, k, f, autoDig, i > 0, one);
                if (count > 0) for (int j = 0; j < one.Count; j++) tmp.ApplyOne(one[j]);
                // the auto digs only matter for a tunnel ladder inside a block (chain cells), so one ladder is checked on the draft
                bool good = LadderPlaceable(c, k, f, tmp);
                cells.Add(c); ok.Add(good);
                if (good) outOps.AddRange(one);
            }
        }

        private static bool Solid(XYZ p, Shape s)
        {
            if (s != null && s.Fill.Contains(p)) return true;
            if (s != null && s.Dig.Contains(p)) return false;
            return Voxel.IsNonEmpty(p);
        }

        // solid and worth digging for room: not a crust (crust is ignored unless marked by hand), not invincible
        private static bool Rock(XYZ p, Shape s)
        {
            if (!Solid(p, s) || Voxel.IsInvincible(p)) return false;
            return s.Fill.Contains(p) || !Voxel.IsCrust(p);
        }

        /// <summary>A dig cell is open to a worker when a neighbour is air or a crust (workers stand in crust, so the
        /// full block under a crust is dug without removing the crust first).</summary>
        internal static bool OpenToDig(XYZ p)
        {
            if (Voxel.HasEmptyNeighbor(p)) return true;
            // a crust or a built ramp next to it: a worker stands in both (the block over a quarry ladder inside a hill has
            // no air around it, only the ramp under it)
            for (int i = 0; i < Six.Length; i++) { XYZ n = p + Six[i]; if (Voxel.IsCrust(n) || QuarryGoalProvider2.IsStair(n)) return true; }
            return false;
        }

        private static readonly XYZ[] Six = { new XYZ(1, 0, 0), new XYZ(-1, 0, 0), new XYZ(0, 1, 0), new XYZ(0, -1, 0), new XYZ(0, 0, 1), new XYZ(0, 0, -1) };

        private static bool Full(XYZ p, Shape s)
        {
            if (s != null && s.Fill.Contains(p)) return true;
            if (s != null && s.Dig.Contains(p)) return false;
            return Voxel.IsFull(p);
        }

        internal sealed class Shape
        {
            internal readonly List<Op> Ops = new List<Op>();
            internal int Harvest;       // GroundResource flags: Stone 1, Brimstone 2, Iron 4 (off = destroyed with the voxel)
            internal bool StoreFull;    // a worker whose bag cannot take the next kept voxel leaves the task and carries the stone to a stockpile (bit 16 on the wire)
            internal bool NoStore;      // never use stockpiles: no storage check, a full bag is dropped next to the task (bit 8 on the wire)
            internal readonly HashSet<XYZ> Dig = new HashSet<XYZ>();
            internal readonly HashSet<XYZ> Fill = new HashSet<XYZ>();
            internal readonly Dictionary<XYZ, Ladder> Ladders = new Dictionary<XYZ, Ladder>();
            internal readonly VoxelSet Vol = new VoxelSet();
            internal XYZ Min, Max;
            internal LandscapeGoalProvider Provider;   // host: the task's goal provider (Kick)
            internal float PendingAt;
            internal int PendingDig, PendingAll;
            internal List<XYZ> FillReady;   // host: fill cells workable now (BtcTerraformFill.Ready)
            internal float FillReadyAt;
            internal bool Empty { get { return Dig.Count + Fill.Count + Ladders.Count == 0; } }
            internal int Cells { get { return Dig.Count + Fill.Count + Ladders.Count; } }

            /// <summary>Replays the operations into the dig / fill / ladder sets. Returns false when the cell limit is hit.</summary>
            internal bool Rebuild()
            {
                Dig.Clear(); Fill.Clear(); Ladders.Clear();
                bool ok = true;
                for (int i = 0; i < Ops.Count && ok; i++) ok = Apply(Ops[i]);
                Vol.Clear();
                List<XYZ> all = new List<XYZ>(Dig.Count + Fill.Count + Ladders.Count);
                all.AddRange(Dig); all.AddRange(Fill);
                foreach (XYZ p in Ladders.Keys) if (!Dig.Contains(p)) all.Add(p);
                Vol.AddRange(all);
                if (all.Count > 0)
                {
                    int x0 = int.MaxValue, y0 = int.MaxValue, z0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue, z1 = int.MinValue;
                    for (int i = 0; i < all.Count; i++)
                    {
                        XYZ p = all[i];
                        if (p.x < x0) x0 = p.x; if (p.y < y0) y0 = p.y; if (p.z < z0) z0 = p.z;
                        if (p.x > x1) x1 = p.x; if (p.y > y1) y1 = p.y; if (p.z > z1) z1 = p.z;
                    }
                    Min = new XYZ(x0, y0, z0); Max = new XYZ(x1, y1, z1);
                }
                return ok;
            }

            /// <summary>Applies one op to the dig / fill / ladder sets only (no volume / bounds), for quick what-if checks.</summary>
            internal void ApplyOne(Op o) { Apply(o); }

            private bool Apply(Op o)
            {
                if (IsLadderKind(o.K))
                {
                    // a quarry ladder replaces the block: never dug or filled; a tunnel ladder sits in a dug (or empty) cell
                    Fill.Remove(o.A);
                    if (o.K != 'T') Dig.Remove(o.A);
                    Ladder l; l.K = o.K; l.F = o.K == 'B' ? XYZ.up : o.B;
                    Ladders[o.A] = l;
                    return Dig.Count + Fill.Count + Ladders.Count <= MaxCells;
                }
                int x0 = Math.Min(o.A.x, o.B.x), x1 = Math.Max(o.A.x, o.B.x);
                int y0 = Math.Min(o.A.y, o.B.y), y1 = Math.Max(o.A.y, o.B.y);
                int z0 = Math.Min(o.A.z, o.B.z), z1 = Math.Max(o.A.z, o.B.z);
                long vol = (long)(x1 - x0 + 1) * (y1 - y0 + 1) * (z1 - z0 + 1);
                if (vol > MaxCells) return false;
                for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                        for (int z = z0; z <= z1; z++)
                        {
                            XYZ p = new XYZ(x, y, z);
                            if (o.K == 'D')
                            {
                                Fill.Remove(p); Dig.Add(p);
                                Ladder l;
                                if (Ladders.TryGetValue(p, out l) && l.K != 'T') Ladders.Remove(p);
                            }
                            else if (o.K == 'F') { Dig.Remove(p); Fill.Add(p); Ladders.Remove(p); }
                            else { Dig.Remove(p); Fill.Remove(p); Ladders.Remove(p); }
                        }
                return Dig.Count + Fill.Count + Ladders.Count <= MaxCells;
            }

            /// <summary>Host: a crust voxel right under the lowest fill cells is filled too (vanilla Flatten fills crust to a full
            /// block first, at the lower price 10 minus what the crust holds); without it the fill above it never starts.
            /// A crust on top of a dug cell is left alone (only dug when marked by hand). Bounds grow to cover the added cells.</summary>
            internal void AddCrust()
            {
                List<XYZ> fill = null;
                foreach (XYZ p in Fill)
                {
                    XYZ b = p - XYZ.up;
                    if (Fill.Contains(b) || Dig.Contains(b) || !Voxel.IsCrust(b)) continue;
                    if (fill == null) fill = new List<XYZ>();
                    fill.Add(b);
                }
                if (fill != null) { for (int i = 0; i < fill.Count; i++) { Fill.Add(fill[i]); Grow(fill[i]); } Vol.AddRange(fill); }
            }

            private void Grow(XYZ p)
            {
                Min = new XYZ(Math.Min(Min.x, p.x), Math.Min(Min.y, p.y), Math.Min(Min.z, p.z));
                Max = new XYZ(Math.Max(Max.x, p.x), Math.Max(Max.y, p.y), Math.Max(Max.z, p.z));
            }

            /// <summary>Takes over the operations and settings of another shape (host edit: the object stays the same).</summary>
            internal void CopyFrom(Shape o)
            {
                Harvest = o.Harvest;
                NoStore = o.NoStore;
                StoreFull = o.StoreFull;
                Ops.Clear();
                Ops.AddRange(o.Ops);
                Rebuild();
                PendingAt = 0f;
            }

            internal Shape Clone()
            {
                Shape s = new Shape();
                s.Harvest = Harvest;
                s.NoStore = NoStore;
                s.StoreFull = StoreFull;
                s.Ops.AddRange(Ops);
                s.Rebuild();
                return s;
            }
        }

        internal static string Encode(Shape s)
        {
            StringBuilder sb = new StringBuilder(16 + s.Ops.Count * 28);
            sb.Append('1').Append(';').Append(s.Harvest | (s.NoStore ? 8 : 0) | (s.StoreFull ? 16 : 0));
            for (int i = 0; i < s.Ops.Count; i++)
            {
                Op o = s.Ops[i];
                sb.Append(';').Append(o.K).Append(',').Append(o.A.x).Append(',').Append(o.A.y).Append(',').Append(o.A.z)
                  .Append(',').Append(o.B.x).Append(',').Append(o.B.y).Append(',').Append(o.B.z);
            }
            return sb.ToString();
        }

        internal static Shape Decode(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string[] parts = text.Split(';');
            if (parts.Length < 2 || parts[0] != "1") return null;
            Shape s = new Shape();
            int h;
            if (!int.TryParse(parts[1], NumberStyles.Integer, Inv, out h)) return null;
            s.Harvest = h & 7;
            s.NoStore = (h & 8) != 0;
            s.StoreFull = (h & 16) != 0;
            if (parts.Length - 2 > MaxOps) return null;
            for (int i = 2; i < parts.Length; i++)
            {
                string[] a = parts[i].Split(',');
                if (a.Length != 7 || a[0].Length != 1) return null;
                char k = a[0][0];
                if (k != 'D' && k != 'F' && k != 'E' && !IsLadderKind(k)) return null;
                int[] v = new int[6];
                for (int j = 0; j < 6; j++) if (!int.TryParse(a[j + 1], NumberStyles.Integer, Inv, out v[j])) return null;
                Op o; o.K = k; o.A = new XYZ(v[0], v[1], v[2]); o.B = new XYZ(v[3], v[4], v[5]);
                s.Ops.Add(o);
            }
            if (!s.Rebuild()) return null;
            return s;
        }

        // ------------------------------------------------------------------ registry
        private sealed class Entry
        {
            internal GameObject Go;
            internal uint NetId;
            internal Shape S;
        }

        /// <summary>Shape per Flatten task GameObject instance id (goal provider / goals look it up here; host and clients).</summary>
        internal static readonly Dictionary<int, Shape> ByObj = new Dictionary<int, Shape>();
        private static readonly Dictionary<uint, Entry> ByNet = new Dictionary<uint, Entry>();
        private static readonly List<uint> _tmp = new List<uint>();

        internal static Shape Of(Component c)
        {
            if (ByObj.Count == 0 || c == null) return null;
            Shape s;
            return ByObj.TryGetValue(c.gameObject.GetInstanceID(), out s) ? s : null;
        }

        /// <summary>All known terraform shapes (for drawing).</summary>
        internal static IEnumerable<Shape> All()
        {
            foreach (KeyValuePair<uint, Entry> kv in ByNet) if (kv.Value.Go != null) yield return kv.Value.S;
        }

        private static void Bind(GameObject go, uint netId, Shape s)
        {
            Entry e;
            if (ByNet.TryGetValue(netId, out e) && e.Go != null) ByObj.Remove(e.Go.GetInstanceID());
            e = new Entry { Go = go, NetId = netId, S = s };
            ByNet[netId] = e;
            if (NetworkServer.active) s.AddCrust();
            if (go != null)
            {
                ByObj[go.GetInstanceID()] = s;
                LandscapeGoalProvider p = go.GetComponent<LandscapeGoalProvider>();
                if (p != null) { p.SetVolume(s.Vol); p.StartUpdate(); }
                // vanilla Flatten picks the best of a small sample of goals (the first ones of its list), so the nearest goal
                // was often never looked at: a terraform task compares all of them
                LandscapeProject lp = go.GetComponent<LandscapeProject>();
                if (lp != null && NetworkServer.active) lp.SetGoalSelectionModel(GoalSelector.Type.CompareAll, false);
                // vanilla re-walks the cells every 15 s only, so a cell opened by a dig got its goal up to 15 s later
                // (workers dug one block and stood): a terraform task re-walks twice a second with a bigger frame budget
                if (p != null) { p.AutoRefreshDelay = 0.5f; p.AutoRefreshFrameBudget = 0.03f; }
                s.Provider = p;
                OnBound(go, netId, s);
                Patches.TerraformVanillaUiPatch.Refresh(go);
            }
        }

        private const string MetaName = "BtcTerraformMeta";

        /// <summary>Beacon icon / name: vanilla takes them from the SHARED LandscapeBeaconMeta prefab (Beacon.Meta, BeaconDriver.beaconMeta),
        /// so the task gets its own inactive BeaconMeta child with our sprite; off = back to the prefab one (pooled task objects).
        /// Also used by the bear traps task (BtcTrapTask) with its own sprite and name.</summary>
        private static void BeaconLook(GameObject go, bool on) { BeaconLook(go, on, BtcTerraformIcon.Get(), BtcLang.TrG("Terraform")); }

        internal static void BeaconLook(GameObject go, bool on, Sprite sprite, string name)
        {
            try
            {
                Brix.Game.Beacons.BeaconDriver d = go.GetComponentInChildren<Brix.Game.Beacons.BeaconDriver>(true);
                Brix.Legacy.Beacon b = d != null ? d.Beacon : go.GetComponentInChildren<Brix.Legacy.Beacon>(true);
                if (b == null) return;
                Brix.Game.Beacons.BeaconMeta m;
                if (on)
                {
                    Transform t = b.transform.Find(MetaName);
                    if (t == null)
                    {
                        GameObject h = new GameObject(MetaName);
                        h.SetActive(false);   // no Awake: its icon key link is empty, the sprite override is used
                        h.transform.SetParent(b.transform, false);
                        m = h.AddComponent<Brix.Game.Beacons.BeaconMeta>();
                        Brix.Game.Beacons.BeaconMeta src = b.Meta ?? (d != null ? d.beaconMeta : null);
                        if (src != null) m.sound = src.sound;
                        HarmonyLib.Traverse.Create(m).Field("spriteOverride").SetValue(sprite);
                        m.displayName = name;
                    }
                    else m = t.GetComponent<Brix.Game.Beacons.BeaconMeta>();
                }
                else
                {
                    Transform t = b.transform.Find(MetaName);
                    if (t == null) return;
                    UnityEngine.Object.Destroy(t.gameObject);
                    m = d != null && d.MetaAssetKey != null ? Brix.External.Factories.Factory.Peek(d.MetaAssetKey).GetComponent<Brix.Game.Beacons.BeaconMeta>() : null;
                    if (m == null) return;
                }
                b.Meta = m;
                if (d != null) d.beaconMeta = m;
                BaliseOverheadDisplayDriver od = b.GetComponent<BaliseOverheadDisplayDriver>();
                if (od != null && od.overheadDisplay != null) od.overheadDisplay.NameTag.Get.WithIcon(m.sprite, true);
            }
            catch (Exception e) { BtcLog.Error("Terraform", "beacon look: " + e.Message); }
        }

        /// <summary>A shape is attached to its task object: own name + icon (beacon, task list), the tool adopts / reloads it.</summary>
        private static void OnBound(GameObject go, uint netId, Shape s)
        {
            try
            {
                BeaconLook(go, true);
                Project pr = go.GetComponent<Project>();
                if (pr != null) BtcGhTaskList.RefreshProject(pr);
                BtcTerraformTool.OnBound(netId, pr, s);
            }
            catch (Exception e) { BtcLog.Error("Terraform", "OnBound: " + e.Message); }
        }

        internal static void ClearAll()
        {
            ByObj.Clear(); ByNet.Clear(); _pending = null;
            BtcTerraformFill.ClearAll();
            _rx.Clear();
        }

        private static float _nextPrune;
        private struct KickAt { internal Shape S; internal XYZ P; internal int Frame; internal float Until; }
        private static readonly List<KickAt> _kicks = new List<KickAt>();

        /// <summary>Host: a goal of the task finished at p (voxel dug, ladder built). The cells around p get their goals right away
        /// (local walk of the 5x5x5 cube, queued straight into the provider's spawner, the next frames until the voxel change has
        /// landed), and the whole task is re-walked, so the worker keeps digging what he just opened instead of running off.</summary>
        internal static void Kick(Shape s, XYZ p)
        {
            if (s == null || s.Provider == null) return;
            s.Provider.StartUpdate();
            KickAt k; k.S = s; k.P = p; k.Frame = Time.frameCount + 1; k.Until = Time.unscaledTime + 0.4f;
            _kicks.Add(k);
            if (_kicks.Count > 64) _kicks.RemoveAt(0);
        }

        // every frame while kicks are pending: spawn / replace the goals of the cells around each kicked voxel
        private static void RunKicks()
        {
            float now = Time.unscaledTime;
            for (int i = _kicks.Count - 1; i >= 0; i--)
            {
                KickAt k = _kicks[i];
                if (Time.frameCount < k.Frame) continue;
                LandscapeGoalProvider pr = k.S.Provider;
                // only while the spawner is idle: a queued add is not in spawnPerVoxel yet and would be queued twice
                if (pr != null && pr.goalSpawner.Done())
                    for (int x = -2; x <= 2; x++)
                        for (int y = -2; y <= 2; y++)
                            for (int z = -2; z <= 2; z++)
                            {
                                XYZ c = new XYZ(k.P.x + x, k.P.y + y, k.P.z + z);
                                if (!k.S.Vol.Contains(c)) continue;
                                // add only: a removal here can race the vanilla re-walk, and a goal released twice
                                // breaks the spawner for good (no new goals at all); removals stay with the re-walk
                                if (pr.spawnPerVoxel.ContainsKey(c)) continue;
                                SpawnOrder want = pr.VoxelNeedsGoal(c);
                                if (want.key != null) pr.goalSpawner.QueueAdd(want);
                            }
                if (now >= k.Until || pr == null) _kicks.RemoveAt(i);
            }
        }

        /// <summary>Update(): drops tasks that are gone, resolves late spawns on clients, applies a loaded save.</summary>
        internal static void Tick()
        {
            try
            {
                if (_kicks.Count > 0) RunKicks();
                if (ByNet.Count == 0 && _pending == null) return;
#if DEV
                BtcTerraformFill.DevWatch();
#endif
                float now = Time.unscaledTime;
                if (now < _nextPrune) return;
                _nextPrune = now + 1f;
                if (!NetworkServer.active && !NetworkClient.active) { ClearAll(); return; }
                _tmp.Clear();
                foreach (KeyValuePair<uint, Entry> kv in ByNet)
                {
                    Entry e = kv.Value;
                    if (e.Go == null && !NetworkServer.active)
                    {
                        GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(kv.Key));
                        if (go != null) { e.Go = go; ByObj[go.GetInstanceID()] = e.S; OnBound(go, kv.Key, e.S); continue; }
                    }
                    bool dead = e.Go == null || !e.Go.activeInHierarchy;
                    if (!dead)
                    {
                        NetworkIdentity ni = e.Go.GetComponent<NetworkIdentity>();
                        dead = ni == null || ni.netId.Value != kv.Key;
                    }
                    // a finished task whose crew left before the last cell was done: vanilla checks the auto delete only when
                    // the crew empties (our prefix held it then), so ask again
                    if (!dead && NetworkServer.active)
                    {
                        int digLeft, allLeft;
                        Pending(e.S, out digLeft, out allLeft);
                        LandscapeProject lp = e.Go.GetComponent<LandscapeProject>();
                        if (allLeft == 0 && lp != null && lp.CrewCount() == 0) lp.CheckForAutoDelete();
                    }
                    if (dead && (e.Go != null || NetworkServer.active)) _tmp.Add(kv.Key);
                }
                for (int i = 0; i < _tmp.Count; i++)
                {
                    Entry e = ByNet[_tmp[i]];
                    if (e.Go != null) { ByObj.Remove(e.Go.GetInstanceID()); BeaconLook(e.Go, false); }
                    ByNet.Remove(_tmp[i]);
                    BtcLog.Info("Terraform", "task " + _tmp[i] + " gone");
                }
                if (_pending != null) ApplyPending(now);
            }
            catch (Exception e) { BtcLog.Error("Terraform", "Tick: " + e.Message); }
        }

        // ------------------------------------------------------------------ host: create
        /// <summary>Client side: ask the host to create a terraform task from a shape.</summary>
        internal static void RequestCreate(Shape s)
        {
            if (!On || s == null || s.Empty) return;
            UNetManager m = NetworkManager.singleton as UNetManager;
            if (m == null || m.LocalNetworkClient == null || !m.LocalNetworkClient.isConnected) return;
            SendChunked(Encode(s), str => m.LocalNetworkClient.Send(MsgCreate, new StringMessage(str)));
            BtcLog.Info("Terraform", "create request: " + s.Ops.Count + " ops, " + s.Dig.Count + " dig, " + s.Fill.Count + " fill");
        }

        /// <summary>Client side: replace the shape of an existing task of the local faction (same msg 112, text "E&lt;netId&gt;#shape").</summary>
        internal static void RequestEdit(uint nid, Shape s)
        {
            if (!On || s == null || s.Empty) return;
            UNetManager m = NetworkManager.singleton as UNetManager;
            if (m == null || m.LocalNetworkClient == null || !m.LocalNetworkClient.isConnected) return;
            SendChunked("E" + nid.ToString(Inv) + "#" + Encode(s), str => m.LocalNetworkClient.Send(MsgCreate, new StringMessage(str)));
            BtcLog.Info("Terraform", "edit request: task " + nid + ", " + s.Ops.Count + " ops");
        }

        internal struct OwnTask
        {
            internal uint NetId;
            internal Project P;
            internal Shape S;
        }

        /// <summary>Terraform tasks of the local faction (for the task list of the tool panel).</summary>
        internal static void OwnTasks(List<OwnTask> outList)
        {
            outList.Clear();
            Faction mine = User.LocalUser != null ? User.LocalUser.faction : null;
            if (mine == null) return;
            foreach (KeyValuePair<uint, Entry> kv in ByNet)
            {
                if (kv.Value.Go == null || !kv.Value.Go.activeInHierarchy) continue;
                Project p = kv.Value.Go.GetComponent<Project>();
                if (p == null || p.faction != mine) continue;
                OwnTask t; t.NetId = kv.Key; t.P = p; t.S = kv.Value.S;
                outList.Add(t);
            }
            outList.Sort((a, b) => a.NetId.CompareTo(b.NetId));
        }

        /// <summary>The selected (vanilla task selection) terraform task of the local faction, if any.</summary>
        internal static bool SelectedOwn(out uint nid, out Shape shape)
        {
            nid = 0; shape = null;
            if (ByNet.Count == 0) return false;
            Faction mine = User.LocalUser != null ? User.LocalUser.faction : null;
            foreach (KeyValuePair<uint, Entry> kv in ByNet)
            {
                if (kv.Value.Go == null) continue;
                Project p = kv.Value.Go.GetComponent<Project>();
                if (p == null || p.faction != mine || !p.selection) continue;
                nid = kv.Key; shape = kv.Value.S;
                return true;
            }
            return false;
        }

        internal static Project ProjectOf(uint nid)
        {
            Entry e;
            return ByNet.TryGetValue(nid, out e) && e.Go != null ? e.Go.GetComponent<Project>() : null;
        }

        /// <summary>Cells still to do: dig cells not dug, fill cells not filled, ladders not built.</summary>
        internal static int Left(Shape s)
        {
            int n = DigLeft(s);
            foreach (XYZ p in s.Fill) if (Voxel.IsEmpty(p) || Voxel.IsCrust(p)) n++;
            return n;
        }

        /// <summary>Dig cells not dug plus ladders not built (work that needs no stones).</summary>
        internal static int DigLeft(Shape s)
        {
            int n = 0;
            // a built ladder (block) or a crust nobody marked by hand is not work left
            foreach (XYZ p in s.Dig) if (!Voxel.IsEmpty(p) && !Voxel.IsInvincible(p) && !Voxel.IsBlock(p)) n++;
            foreach (XYZ p in s.Ladders.Keys) if (!QuarryGoalProvider2.IsStair(p) && !Voxel.IsInvincible(p)) n++;
            return n;
        }

        /// <summary>DigLeft / Left cached for half a second (asked per worker by the task AI).</summary>
        internal static void Pending(Shape s, out int dig, out int all)
        {
            float now = Time.unscaledTime;
            if (now >= s.PendingAt)
            {
                s.PendingAt = now + 0.5f;
                s.PendingDig = DigLeft(s);
                s.PendingAll = Left(s);
            }
            dig = s.PendingDig; all = s.PendingAll;
        }

        /// <summary>The task of the local faction that owns one of these cells (dig, fill or ladder), for editing.</summary>
        internal static bool FindOwn(XYZ a, XYZ b, out uint nid, out Shape shape)
        {
            nid = 0; shape = null;
            Faction mine = User.LocalUser != null ? User.LocalUser.faction : null;
            foreach (KeyValuePair<uint, Entry> kv in ByNet)
            {
                Entry e = kv.Value;
                if (e.Go == null) continue;
                Project p = e.Go.GetComponent<Project>();
                if (p == null || p.faction != mine) continue;
                Shape s = e.S;
                if (s.Dig.Contains(a) || s.Fill.Contains(a) || s.Ladders.ContainsKey(a) || s.Dig.Contains(b) || s.Fill.Contains(b) || s.Ladders.ContainsKey(b))
                { nid = kv.Key; shape = s; return true; }
            }
            return false;
        }

        private static void OnCreateAtHost(NetworkMessage nm)
        {
            try
            {
                string text = Collect(nm.conn != null ? nm.conn.connectionId : -1, nm.ReadMessage<StringMessage>().value);
                if (text == null || !On || !NetworkServer.active) return;
                uint editId = 0;
                if (text.Length > 1 && text[0] == 'E')
                {
                    int hash = text.IndexOf('#');
                    if (hash <= 1 || !uint.TryParse(text.Substring(1, hash - 1), NumberStyles.Integer, Inv, out editId)) { BtcLog.Warn("Terraform", "bad edit request"); return; }
                    text = text.Substring(hash + 1);
                }
                Shape s = Decode(text);
                if (s == null || s.Empty) { BtcLog.Warn("Terraform", "bad create request"); return; }
                UNetManager m = NetworkManager.singleton as UNetManager;
                User u = m == null ? null : (m.debugLoader ? User.LocalUser : m.UserList.Find(x => x != null && x.connectionToClient != null && x.connectionToClient.connectionId == nm.conn.connectionId));
                if (u == null || u.faction == null) { BtcLog.Warn("Terraform", "create: no user for connection"); return; }
                if (editId != 0) Edit(u, editId, s); else Spawn(u, s);
            }
            catch (Exception e) { BtcLog.Error("Terraform", "OnCreateAtHost: " + e); }
        }

        /// <summary>Host: swaps the shape of a task (only its owner faction may); bounds follow the new shape, goals are re-walked.</summary>
        private static void Edit(User u, uint nid, Shape s)
        {
            Entry e;
            if (!ByNet.TryGetValue(nid, out e) || e.Go == null) { BtcLog.Warn("Terraform", "edit: task " + nid + " not found"); return; }
            Project pr = e.Go.GetComponent<Project>();
            if (pr == null || pr.faction != u.faction) { BtcLog.Warn("Terraform", "edit: task " + nid + " is not of faction " + u.faction.ID); return; }
            // in place: live goals keep a reference to the task's shape (TerraformDigSetting.S), so a new object would leave them on
            // the old kept resources / haul setting until the task was made again
            int oldHarvest = e.S.Harvest;
            e.S.CopyFrom(s);
            s = e.S;
            // vanilla DigGoal decides "stones into the bag while digging" once, when its work node is built (SetupBuilder:
            // EatGarnotteWhileWorking only if the voxel is not overkilled): rebuild the live goals so a kept resource is picked up at once
            if (oldHarvest != s.Harvest)
                foreach (DigGoal g in e.Go.GetComponentsInChildren<DigGoal>(false))
                    if (BetterThanCastle.Patches.TerraformDigSetting.Get(g) != null) g.RebuildNode();
            s.AddCrust();
            ByObj[e.Go.GetInstanceID()] = s;   // before Set: the data change walks the new cells
            e.Go.GetComponent<LandscapeData>().Set(s.Min.ToVector3(), s.Max.ToVector3(), s.Min.y);
            Bind(e.Go, nid, s);
            Broadcast(nid, s, null);
            BtcLog.Info("Terraform", "task " + nid + " edited: " + s.Dig.Count + " dig, " + s.Fill.Count + " fill, " + s.Ladders.Count + " ladders");
        }

        private static GameObject Spawn(User u, Shape s)
        {
            UNetGroupe ug = u.GetComponent<UNetGroupe>();
            if (ug == null) return null;
            s.AddCrust();
            Vector3 min = s.Min.ToVector3(), max = s.Max.ToVector3();
            Vector3 pos = new Vector3((min.x + max.x) * 0.5f, max.y + 1f, (min.z + max.z) * 0.5f);
            GameObject go = ug.CreateGroup(Groupes.Landscape, pos, false);
            ByObj[go.GetInstanceID()] = s;   // before Set: the provider's data change already walks our cells
            go.GetComponent<LandscapeData>().Set(min, max, s.Min.y);
            NetworkServer.Spawn(go, go.GetComponent<AssetKeyHolder>().AssetKey.AssetId);
            uint nid = go.GetComponent<NetworkIdentity>().netId.Value;
            Bind(go, nid, s);
            // vanilla commits a task when the player deselects it; ours is never selected, so commit at once
            Project pr = go.GetComponent<Project>();
            if (pr != null) pr.Commit();
            Broadcast(nid, s, null);
            BtcLog.Info("Terraform", "task " + nid + " created for faction " + u.faction.ID + ": " + s.Dig.Count + " dig, " + s.Fill.Count + " fill, harvest " + s.Harvest);
            return go;
        }

        // ------------------------------------------------------------------ host -> clients
        private static void Broadcast(uint nid, Shape s, NetworkConnection only)
        {
            if (!NetworkServer.active || !On) return;
            string text = nid.ToString(Inv) + "#" + Encode(s);
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                NetworkConnection c = NetworkServer.connections[i];
                if (c == null || !c.isConnected || BtcNetManager.IsLocalConnection(c)) continue;
                if (only != null && c != only) continue;
                SendChunked(text, str => c.Send(MsgShape, new StringMessage(str)));
            }
        }

        /// <summary>Host: a client became ready, send it every terraform shape.</summary>
        internal static void SendState(NetworkConnection conn)
        {
            if (!NetworkServer.active || !On || conn == null) return;
            try { foreach (KeyValuePair<uint, Entry> kv in ByNet) if (kv.Value.Go != null) Broadcast(kv.Key, kv.Value.S, conn); }
            catch (Exception e) { BtcLog.Error("Terraform", "SendState: " + e.Message); }
        }

        private static void OnShapeAtClient(NetworkMessage nm)
        {
            try
            {
                string text = Collect(-2, nm.ReadMessage<StringMessage>().value);
                if (text == null || NetworkServer.active || !On) return;
                int hash = text.IndexOf('#');
                uint nid;
                if (hash <= 0 || !uint.TryParse(text.Substring(0, hash), NumberStyles.Integer, Inv, out nid)) return;
                Shape s = Decode(text.Substring(hash + 1));
                if (s == null) return;
                Bind(ClientScene.FindLocalObject(new NetworkInstanceId(nid)), nid, s);
            }
            catch (Exception e) { BtcLog.Error("Terraform", "OnShapeAtClient: " + e.Message); }
        }

        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                if (NetworkServer.active)
                {
                    NetworkServer.UnregisterHandler(MsgCreate);
                    NetworkServer.RegisterHandler(MsgCreate, OnCreateAtHost);
                }
                UNetManager m = NetworkManager.singleton as UNetManager;
                NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
                if (c != null)
                {
                    c.UnregisterHandler(MsgShape);
                    c.RegisterHandler(MsgShape, OnShapeAtClient);
                }
            }
            catch (Exception e) { BtcLog.Error("Terraform", "RegisterHandlers: " + e); }
        }

        // ------------------------------------------------------------------ chunks ("id/i/n|data", <= 1000 chars each)
        private const int Chunk = 1000;
        private static int _seq;
        private static readonly Dictionary<string, string[]> _rx = new Dictionary<string, string[]>();

        private static void SendChunked(string text, Action<string> send)
        {
            int n = Math.Max(1, (text.Length + Chunk - 1) / Chunk);
            int id = ++_seq;
            for (int i = 0; i < n; i++)
            {
                int start = i * Chunk;
                string part = start >= text.Length ? string.Empty : text.Substring(start, Math.Min(Chunk, text.Length - start));
                send(id.ToString(Inv) + "/" + i.ToString(Inv) + "/" + n.ToString(Inv) + "|" + part);
            }
        }

        /// <summary>Collects one chunk; returns the whole text when the last missing chunk arrived, else null.</summary>
        private static string Collect(int conn, string msg)
        {
            int bar = msg.IndexOf('|');
            if (bar < 0) return null;
            string[] h = msg.Substring(0, bar).Split('/');
            int i, n;
            if (h.Length != 3 || !int.TryParse(h[1], NumberStyles.Integer, Inv, out i) || !int.TryParse(h[2], NumberStyles.Integer, Inv, out n)) return null;
            if (n < 1 || n > 512 || i < 0 || i >= n) return null;
            string body = msg.Substring(bar + 1);
            if (n == 1) return body;
            string key = conn.ToString(Inv) + ":" + h[0];
            string[] parts;
            if (!_rx.TryGetValue(key, out parts) || parts.Length != n) { parts = new string[n]; _rx[key] = parts; }
            parts[i] = body;
            for (int k = 0; k < n; k++) if (parts[k] == null) return null;
            _rx.Remove(key);
            return string.Concat(parts);
        }

        // ------------------------------------------------------------------ save / load (host, BC ON)
        private sealed class Pend { internal XYZ Min, Max; internal Shape S; }
        private static List<Pend> _pending;
        private static float _giveUpAt;

        internal static void OnSave(string folder)
        {
            if (!NetworkServer.active || !On || string.IsNullOrEmpty(folder)) return;
            string file = System.IO.Path.Combine(folder, FileName);
            StringBuilder sb = new StringBuilder();
            int n = 0;
            foreach (KeyValuePair<uint, Entry> kv in ByNet)
            {
                Entry e = kv.Value;
                if (e.Go == null || !e.Go.activeInHierarchy) continue;
                LandscapeData d = e.Go.GetComponent<LandscapeData>();
                if (d == null) continue;
                VoxelBounds b = d.Bounds;
                sb.Append(b.min.x).Append(' ').Append(b.min.y).Append(' ').Append(b.min.z).Append(' ')
                  .Append(b.max.x).Append(' ').Append(b.max.y).Append(' ').Append(b.max.z).Append(' ').Append(Encode(e.S)).Append('\n');
                n++;
            }
            if (n == 0) { if (File.Exists(file)) File.Delete(file); return; }
            File.WriteAllText(file, sb.ToString());
            BtcLog.Info("Terraform", "saved " + n + " terraform task(s)");
        }

        internal static void OnLoad(string folder)
        {
            ClearAll();
            if (!NetworkServer.active || !On || string.IsNullOrEmpty(folder)) return;
            string file = System.IO.Path.Combine(folder, FileName);
            if (!File.Exists(file)) return;
            List<Pend> list = new List<Pend>();
            foreach (string line in File.ReadAllLines(file))
            {
                string[] t = line.Split(new[] { ' ' }, 7);
                if (t.Length < 7) continue;
                int[] v = new int[6];
                bool ok = true;
                for (int j = 0; j < 6 && ok; j++) ok = int.TryParse(t[j], NumberStyles.Integer, Inv, out v[j]);
                Shape s = ok ? Decode(t[6]) : null;
                if (s == null || s.Empty) continue;
                list.Add(new Pend { Min = new XYZ(v[0], v[1], v[2]), Max = new XYZ(v[3], v[4], v[5]), S = s });
            }
            if (list.Count == 0) return;
            _pending = list;
            _giveUpAt = Time.unscaledTime + 90f;
            BtcLog.Info("Terraform", "save has " + list.Count + " terraform task(s), matching after the load");
        }

        private static void ApplyPending(float now)
        {
            if (!NetworkServer.active) { _pending = null; return; }
            LandscapeData[] all = UnityEngine.Object.FindObjectsOfType<LandscapeData>();
            for (int i = 0; i < all.Length; i++)
            {
                LandscapeData d = all[i];
                if (d == null || ByObj.ContainsKey(d.gameObject.GetInstanceID())) continue;
                NetworkIdentity ni = d.GetComponent<NetworkIdentity>();
                if (ni == null || ni.netId.Value == 0) continue;
                VoxelBounds b = d.Bounds;
                for (int k = 0; k < _pending.Count; k++)
                {
                    Pend p = _pending[k];
                    if (!b.min.Equals(p.Min) || !b.max.Equals(p.Max)) continue;
                    Bind(d.gameObject, ni.netId.Value, p.S);
                    Broadcast(ni.netId.Value, p.S, null);
                    _pending.RemoveAt(k);
                    BtcLog.Info("Terraform", "restored task " + ni.netId.Value);
                    break;
                }
            }
            if (_pending.Count == 0 || now > _giveUpAt)
            {
                if (_pending.Count > 0) BtcLog.Warn("Terraform", _pending.Count + " saved terraform task(s) not found after the load");
                _pending = null;
            }
        }
    }
}
