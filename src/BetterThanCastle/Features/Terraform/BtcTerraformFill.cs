using System;
using System.Collections.Generic;
using BetterThanCastle.Patches;
using Brix.Engine;
using Brix.Game.AI;
using Brix.Game.AI.InstructionType;
using Brix.Game.AI.Nodes;
using Brix.Game.AI.Pathfinding;
using Brix.Pathfinding;
using Brix.Pathfinding.Test;
using Brix.Game.Batchers;
using Brix.Game.Components;
using Brix.Game.Locomotion;
using Brix.Game.Semantique;
using Brix.UI.Icons;
using Motus.Behavior;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Fill cells of a terraform task (host AI, own work instead of goals; rebuilt 2026-11-28). Gameplay as vanilla
    /// Flatten fill: a cell needs a floor, the lowest layer goes first, one tool hit turns it into a full sand block, the
    /// price is 10 stones minus what the cell already holds (a crust is cheaper) and, as in vanilla, a worker with fewer
    /// stones still fills the whole block with what he has. Stones come from the bag, then from the ground near the worker,
    /// then from the nearest source (vanilla FindAndPickUp). The worker stands on any path node next to the cell, so a mound
    /// grows from the bottom with the workers standing on what they filled. A cell with a unit in it waits (vanilla buries it).
    /// Each cell is held by one worker; a failed walk skips that cell for that worker 30 s.</summary>
    internal static class BtcTerraformFill
    {
        private static readonly IconKey Icon = IconKeys.Landscape;
        private const float FailedFor = 30f, BlockedFor = 2f;

        internal static bool Filled(XYZ p) { return !Voxel.IsEmpty(p) && !Voxel.IsCrust(p); }

        // a floor under the cell: a filled fill cell, or any voxel that is not part of the fill
        private static bool Supported(BtcTerraform.Shape s, XYZ p)
        {
            XYZ b = p - XYZ.up;
            return s.Fill.Contains(b) ? Filled(b) : !Voxel.IsEmpty(b);
        }

        /// <summary>Fill cells that can be worked now (not full, on a floor, a path node at or next to them), cached 0.5 s.</summary>
        internal static List<XYZ> Ready(BtcTerraform.Shape s)
        {
            float now = Time.unscaledTime;
            if (s.FillReady != null && now < s.FillReadyAt) return s.FillReady;
            if (s.FillReady == null) s.FillReady = new List<XYZ>();
            s.FillReady.Clear();
            s.FillReadyAt = now + 0.5f;
            foreach (XYZ p in s.Fill)
                if (!Filled(p) && Supported(s, p) && PathNodeRegistry.WalkableAt(p) != null) s.FillReady.Add(p);
            return s.FillReady;
        }

        internal static void Dirty(BtcTerraform.Shape s) { if (s != null) s.FillReadyAt = 0f; }

        // ------------------------------------------------------------------ reservations / failed walks
        private struct Hold { internal Labor L; internal float At; }
        private static readonly Dictionary<XYZ, Hold> Holds = new Dictionary<XYZ, Hold>();
        private static readonly Dictionary<long, float> Failed = new Dictionary<long, float>();
        private static readonly Dictionary<XYZ, float> Blocked = new Dictionary<XYZ, float>();
        private struct CellFail { internal int Who; internal int N; internal float At; }
        private static readonly Dictionary<XYZ, CellFail> CellFails = new Dictionary<XYZ, CellFail>();   // 2 workers failed the walk: skipped for all
        private static readonly List<XYZ> _rm = new List<XYZ>();

        private static long Key(Labor l, XYZ p)
        {
            return ((long)l.GetInstanceID() << 32) ^ (uint)((p.x & 0x7FF) | ((p.y & 0x3FF) << 11) | ((p.z & 0x7FF) << 21));
        }

        // a worker keeps his cell while he works for this task; the first seconds count before he is hired
        private static bool Held(Hold h, Project task, float now)
        {
            if (h.L == null || h.L.IsNullOrReleased() || now - h.At > 60f) return false;
            return now - h.At < 4f || h.L.Project == task;
        }

        private static bool Skip(XYZ p, Labor labor, Project task, float now)
        {
            Hold h;
            if (Holds.TryGetValue(p, out h) && h.L != labor && Held(h, task, now)) return true;
            float t;
            if (Blocked.TryGetValue(p, out t) && now < t) return true;
            if (Failed.TryGetValue(Key(labor, p), out t) && now - t < FailedFor) return true;
            CellFail cf;
            if (CellFails.TryGetValue(p, out cf) && cf.N >= 2 && now - cf.At < FailedFor) return true;
            return VoxelPresence.HasPresenceAt(p) || VoxelPresence.HasPresenceAt(p - XYZ.up);
        }

        private static void MarkFailed(Labor l, XYZ p)
        {
            float now = Time.unscaledTime;
            if (Failed.Count > 1024)
            {
                List<long> old = new List<long>();
                foreach (KeyValuePair<long, float> kv in Failed) if (now - kv.Value >= FailedFor) old.Add(kv.Key);
                for (int i = 0; i < old.Count; i++) Failed.Remove(old[i]);
                if (Failed.Count > 4096) Failed.Clear();
            }
            Failed[Key(l, p)] = now;
            CellFail cf;
            if (!CellFails.TryGetValue(p, out cf) || now - cf.At >= FailedFor) { cf.N = 0; cf.Who = 0; }
            if (cf.Who != l.GetInstanceID()) { cf.N++; cf.Who = l.GetInstanceID(); }
            cf.At = now;
            if (CellFails.Count > 2048) CellFails.Clear();
            CellFails[p] = cf;
        }

        private static void Release(Labor l, XYZ p)
        {
            Hold h;
            if (Holds.TryGetValue(p, out h) && h.L == l) Holds.Remove(p);
        }

        /// <summary>The cell this worker would fill: lowest layer first, then the nearest (reserve: hold it for him).</summary>
        private static bool Pick(BtcTerraform.Shape s, Project task, Labor labor, bool reserve, out XYZ best)
        {
            best = XYZ.zero;
            List<XYZ> ready = Ready(s);
            if (ready.Count == 0) return false;
            float now = Time.unscaledTime, bestScore = float.MaxValue;
            Vector3 lp = labor.transform.position;
            bool found = false;
            for (int i = 0; i < ready.Count; i++)
            {
                XYZ p = ready[i];
                if (Skip(p, labor, task, now)) continue;
                float score = p.y * 64f + Mathf.Min(Vector3.Distance(lp, p.ToVector3()), 63f);
                if (score < bestScore) { bestScore = score; best = p; found = true; }
            }
            if (found && reserve)
            {
                _rm.Clear();
                foreach (KeyValuePair<XYZ, Hold> kv in Holds) if (kv.Value.L == labor || !Held(kv.Value, task, now) && now - kv.Value.At > 60f) _rm.Add(kv.Key);
                for (int i = 0; i < _rm.Count; i++) Holds.Remove(_rm[i]);
                Hold h; h.L = labor; h.At = now;
                Holds[best] = h;
            }
            return found;
        }

        private static int BagStones(Labor l) { return l.recepteur != null ? l.recepteur.ContentDescription.Value<Stones>() : 0; }

        private static bool CanGetStones(Labor l) { return BagStones(l) > 0 || l.Find(Adjectif.stones.Profile) != null; }

        /// <summary>LandscapeProject.HasWorkFor part: a fill cell for this worker and stones he can get.</summary>
        internal static bool HasWork(BtcTerraform.Shape s, Project task, Labor labor)
        {
            if (labor == null || s.Fill.Count == 0) return false;
            XYZ p;
            return Pick(s, task, labor, false, out p) && CanGetStones(labor);
        }

        /// <summary>This worker fills now instead of digging: he carries stones or stones lie near him, or the task has no dig
        /// work he can reach (then he fetches stones from anywhere).</summary>
        internal static bool Wants(BtcTerraform.Shape s, LandscapeProject task, Labor labor)
        {
            if (labor == null || s.Fill.Count == 0 || !task.ReadyToAssignGoals()) return false;
            XYZ p;
            if (!Pick(s, task, labor, false, out p) || !CanGetStones(labor)) return false;
            if (BagStones(labor) > 0 || TerraformPriority.LooseStonesNear(labor)) return true;
            return !TerraformGoals.Has(task.goalProvider, 0, labor, true) && !TerraformGoals.Has(task.goalProvider, 2, labor, true);
        }

        internal static Node GetInstruction(BtcTerraform.Shape s, Project task, Labor labor, Var<OrderPackage<Labor>> result)
        {
            XYZ p;
            if (!Pick(s, task, labor, true, out p)) return Node.Fail(FailException.Generic);
            WorkInstruction<Labor> wi = new WorkInstruction<Labor>("Terraform fill", Icon, l => Work(s, l, p));
            OrderPackage<Labor> pk = Order.Package.Of<Labor>(wi, Labor.DecisionSource.Project);
            pk.SetPrepare(delegate(BaseLabor bl) { return task.Hire(bl as Labor); });
            result.Set(pk);
            return Node.Empty;
        }

        // ------------------------------------------------------------------ the work
#if DEV
        // DEV: the step each worker's fill work is in (tfdump), to find where a worker hangs
        private static readonly Dictionary<int, string> Steps = new Dictionary<int, string>();
        private static void Mark(Labor l, string step) { if (l != null) Steps[l.GetInstanceID()] = step + "@" + Time.time.ToString("0"); }
        internal static string StepOf(Labor l) { string s; return l != null && Steps.TryGetValue(l.GetInstanceID(), out s) ? s : "-"; }
        private static float _nextWatch;
        private static readonly Dictionary<int, int> _stuckLogged = new Dictionary<int, int>();

        /// <summary>DEV (BtcTerraform.Tick): a worker whose fill step has not changed for 30 s is logged once per step (TfStuck).</summary>
        internal static void DevWatch()
        {
            float now = Time.time;
            if (now < _nextWatch || Steps.Count == 0) return;
            _nextWatch = now + 10f;
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
            {
                string s;
                if (l == null || !Steps.TryGetValue(l.GetInstanceID(), out s)) continue;
                int at = s.LastIndexOf('@');
                float t;
                if (at < 0 || !float.TryParse(s.Substring(at + 1), out t)) continue;
                // stuck inside the fill work (same step 30 s), or after it: a task of a terraform worker running for over 60 s
                bool inFill = !s.StartsWith("end") && now - t >= 30f;
                bool longTask = l.CurrentTask != null && now - l.CurrentTask.TaskStartedTimestamp > 60f && l.Project != null && BtcTerraform.Of(l.Project) != null;
                if (!inFill && !longTask) continue;
                int key = (s + (l.CurrentTask != null ? l.CurrentTask.TaskStartedTimestamp.ToString() : "")).GetHashCode();
                int had;
                if (_stuckLogged.TryGetValue(l.GetInstanceID(), out had) && had == key) continue;
                _stuckLogged[l.GetInstanceID()] = key;
                Locomotion4 loc = l.navigation.Locomotion;
                Task tk = l.CurrentTask;
                BtcLog.Info("TfStuck", l.name + " step=" + s + " for " + (now - t).ToString("0") + " s, loc=" + loc.state + " canMove=" + loc.CanMove()
                    + " feet=" + XYZ.FromVector3(l.transform.position) + " occ=" + loc.PositionOccupied + " ent=" + loc.PositionEntering
                    + " task=" + (tk == null ? "-" : tk.Description + " int=" + tk.CanInterrupt + " age=" + (now - tk.TaskStartedTimestamp).ToString("0"))
                    + " brain=" + l.enabled + " pending=" + (l.PendingTask != null) + " work=" + (l.CurrentWork != null) + " bag=" + BagStones(l) + " proj=" + (l.Project != null ? l.Project.name : "-"));
            }
        }
#endif

        private sealed class Job { internal bool Stones, Reached; }

        [System.Diagnostics.Conditional("DEV")]
        private static void Step(Labor l, string step)
        {
#if DEV
            Mark(l, step);
#endif
        }

        private static Node Work(BtcTerraform.Shape s, Labor l, XYZ p)
        {
            Job j = new Job();
            return Node.Sequence
                .Do(() => { Step(l, "stones " + p); return Safe(() => Stones(l, p, j), "Stones"); })
                .Do(() => { Step(l, "walk"); return Safe(() => Walk(s, l, p), "Walk"); })
                .Do(delegate { j.Reached = true; })
                .Do(() => { Step(l, "swing"); return Safe(() => Swing(s, l, p), "Swing"); })
                .Finally(delegate(Labor w)
                {
                    try
                    {
                        Step(w, "end");
                        Release(w, p);
                        if (j.Stones && !j.Reached && !Filled(p)) MarkFailed(w, p);
                    }
                    catch (Exception e) { BtcLog.Error("Terraform", "Fill.Finally: " + e.Message); }
                }, l).ToNode;
        }

        // an exception inside a unit's behaviour tick disables its Behavior for good (frozen unit, orders ignored): every step of
        // the fill work turns one into a plain work failure
        private static Node Safe(Func<Node> step, string what)
        {
            try { return step(); }
            catch (Exception e)
            {
                BtcLog.Error("Terraform", "Fill." + what + ": " + e);
                return Node.FailWork<Exceptions.NullTarget>();
            }
        }

        // the bag is enough, else vanilla: the nearest source (vanilla Find, which also skips stones another worker reserved);
        // a few stones in the bag are enough when nothing else is found (vanilla fills the whole block with what the worker has).
        // No own "loose stone nearby" pickup any more: it ran whole crews into one stack and froze them (user 2026-11-28)
        private static Node Stones(Labor l, XYZ p, Job j)
        {
            if (Filled(p)) return Node.FailWork<Exceptions.VoxelHasBeenBuildOver>();
            int need = 10 - TerraformHarvest.InGround(p), have = BagStones(l);
            j.Stones = true;
            if (have >= need) return Node.Empty;
            if (have > 0) return Node.Empty;
            GameObject far = l.Find(Adjectif.stones.Profile);
            if (far == null || !l.recepteur.HasRoomFor(far)) { j.Stones = false; return Node.FailWork<Exceptions.CannotFindWhatINeedToPickUp>(); }
            return PickUpNode.FindAndPickUp(l, Adjectif.stones.Profile, 20);
        }

        // to a path node next to the cell, never into it: the cell itself is walkable (it has a floor), so a "leading to"
        // location made the path end inside the cell, outside the target set, and every walk failed (lab terraform-fill)
        private static Node Walk(BtcTerraform.Shape s, Labor l, XYZ p)
        {
            if (Filled(p)) return Node.FailWork<Exceptions.VoxelHasBeenBuildOver>();
            if (BagStones(l) == 0) return Node.FailWork<Exceptions.CannotFindWhatINeedToPickUp>();
            XYZ at = XYZ.FromVector3(l.transform.position), stand;
            if (at != p && Next(p, at)) { Step(l, "turn here"); return l.TurnToward(p); }
            if (!Stand(s, l, p, out stand)) return Node.FailWork<Exceptions.CantReach>();
            Step(l, "run " + stand);
            return Node.Sequence.Do(l.RunToExactly(Location.Of(stand))).Do(() => { Step(l, "turn"); return l.TurnToward(p); }).ToNode;
        }

        // the path node of the cell links to this one (one step: side, climb or jump)
        private static bool Next(XYZ p, XYZ at)
        {
            WalkablePathNode n = PathNodeRegistry.WalkableAt(p);
            if (n == null) return false;
            foreach (PathBranch b in PathNodeRegistry.GetBranches(n)) if (b.Node.RepresentativePosition == at) return true;
            return false;
        }

        // the stand cell for filling p: a node linked to the cell, free of units, not an unfilled fill cell if possible
        // (someone could fill it under the worker), nearest to the worker
        private static bool Stand(BtcTerraform.Shape s, Labor l, XYZ p, out XYZ best)
        {
            best = XYZ.zero;
            WalkablePathNode n = PathNodeRegistry.WalkableAt(p);
            if (n == null) return false;
            Vector3 lp = l.transform.position;
            float bestScore = float.MaxValue;
            bool found = false;
            foreach (PathBranch b in PathNodeRegistry.GetBranches(n))
            {
                XYZ c = b.Node.RepresentativePosition;
                if (c == p || VoxelPresence.HasPresenceAt(c)) continue;
                float score = Vector3.Distance(lp, c.ToVector3()) + Mathf.Abs(c.y - p.y) * 2f + (s.Fill.Contains(c) && !Filled(c) ? 20f : 0f);
                if (score < bestScore) { bestScore = score; best = c; found = true; }
            }
            return found;
        }

        private static Node Swing(BtcTerraform.Shape s, Labor l, XYZ p)
        {
            if (Filled(p)) return Node.Empty;
            if (Occupied(p, l))
            {
#if DEV
                Locomotion4 me = l.navigation.Locomotion;
                BtcLog.Info("TfFill", l.name + " occupied " + p + " presP=" + VoxelPresence.HasPresenceAt(p) + " presDown=" + VoxelPresence.HasPresenceAt(p - XYZ.up)
                    + " self=" + In(me, l.transform, p) + " feet=" + XYZ.FromVector3(l.transform.position) + " occ=" + me.PositionOccupied + " ent=" + me.PositionEntering);
#endif
                Blocked[p] = Time.unscaledTime + BlockedFor;
                return Node.FailWork<Exceptions.GoalTooCrowded>();
            }
            Transform t = Target(l, p);
            Step(l, "anim");
            StaticNode hit = Node.Sequence.Do(() => Safe(() => Apply(s, l, p), "Apply")).Label("Terraform fill").ToNode;
            return AnimationNodes.Once(hit, Working.WorkStates.Pick, t, l);
        }

        // at the tool hit: never into a unit; the vanilla fill (AddInGround + RemoveGarnotes)
        private static Node Apply(BtcTerraform.Shape s, Labor l, XYZ p)
        {
            if (Filled(p)) return Node.Empty;
            if (Occupied(p, l)) { Blocked[p] = Time.unscaledTime + BlockedFor; return Node.FailWork<Exceptions.GoalTooCrowded>(); }
            int qty = 10 - TerraformHarvest.InGround(p);
            if (qty <= 0) return Node.FailWork<Exceptions.VoxelHasBeenBuildOver>();
            List<GameObject> take = new List<GameObject>();
            foreach (IDescriptor d in l.recepteur.StoredItems)
            {
                if (take.Count >= qty) break;
                if (d != null && d.Description.Value<Stones>() == 1) take.Add(d.GameObject);
            }
            if (take.Count == 0) return Node.FailWork<Exceptions.CannotFindWhatINeedToPickUp>();
            VoxelRequestBatcher.Instance.CallRpcAddTerrainResources(p.x, p.y, p.z, VoxelTerrainType.Sand, qty, true, true);
            for (int i = 0; i < take.Count; i++) take[i].SafeNetworkDestroy();
            Dirty(s);
            BtcTerraform.Kick(s, p);   // ladders / crusts around the cell may take their goal now
#if DEV
            BtcLog.Info("TfFill", l.name + " filled " + p + " (" + take.Count + " stones)");
#endif
            return Node.Empty;
        }

        // the animation faces a transform: one helper object per worker, moved to his cell
        private static readonly Dictionary<int, Transform> Targets = new Dictionary<int, Transform>();

        private static Transform Target(Labor l, XYZ p)
        {
            int id = l.GetInstanceID();
            Transform t;
            if (!Targets.TryGetValue(id, out t) || t == null)
            {
                if (Targets.Count > 256) ClearTargets();
                GameObject go = new GameObject("BtcFillTarget");
                go.hideFlags = HideFlags.HideAndDontSave;
                t = go.transform;
                Targets[id] = t;
            }
            t.position = p.ToVector3();
            return t;
        }

        private static void ClearTargets()
        {
            foreach (Transform t in Targets.Values) if (t != null) UnityEngine.Object.Destroy(t.gameObject);
            Targets.Clear();
        }

        internal static void ClearAll()
        {
            Holds.Clear(); Failed.Clear(); Blocked.Clear(); CellFails.Clear();
            ClearTargets();
        }

        // ------------------------------------------------------------------ units in the cell
        /// <summary>A bricktron stands in the cell (feet in p, or feet under it and the head in p), or is walking into it. Vanilla
        /// fills it anyway and its BuryBehavior moves only the transform out of the earth: the unit hangs for good.</summary>
        internal static bool Occupied(XYZ p, Labor labor)
        {
            if (VoxelPresence.HasPresenceAt(p) || VoxelPresence.HasPresenceAt(p - XYZ.up)) return true;
            if (labor != null && In(labor.navigation.Locomotion, labor.transform, p)) return true;
            int n = Physics.OverlapSphereNonAlloc(p.ToVector3(), 1.6f, Hits, ~0, QueryTriggerInteraction.Ignore);
            bool hit = false;
            Bounds cell = new Bounds(p.ToVector3(), new Vector3(0.6f, 0.9f, 0.6f));
            for (int i = 0; i < n && !hit; i++)
            {
                Collider c = Hits[i];
                if (c == null) continue;
                Locomotion4 loc = c.GetComponentInParent<Locomotion4>();
                // the filling worker stands right next to the cell: his collider touches it, only his cells count (In above)
                if (loc == null || (labor != null && loc == labor.navigation.Locomotion)) continue;
                hit = c.bounds.Intersects(cell) || In(loc, loc.transform, p);
            }
            Array.Clear(Hits, 0, n);
            return hit;
        }

        private static readonly Collider[] Hits = new Collider[64];

        private static bool In(Locomotion4 loc, Transform t, XYZ p)
        {
            XYZ low = p - XYZ.up;
            XYZ feet = XYZ.FromVector3(t.position);
            if (feet == p || feet == low) return true;
            if (loc == null) return false;
            XYZ a = loc.PositionOccupied, b = loc.PositionEntering;
            return a == p || a == low || b == p || b == low;
        }
    }
}
