#if DEV
using System;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using BetterThanCastle.Patches;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>DEV scenario helper for the terraforming task: tfspawn [harvest] makes a pit and a mound next to the workers,
    /// tfhire puts every worker into the terraform tasks, tfdump prints what is left.</summary>
    internal static class BtcDevTerraform
    {
        private static bool Ground(float x, float z, float yRef, out XYZ top)
        {
            top = default(XYZ);
            RaycastHit hit;
            if (!Physics.Raycast(new Vector3(x, yRef + 40f, z), Vector3.down, out hit, 120f, 132096, QueryTriggerInteraction.Ignore)) return false;
            Vector3 p = hit.point - Vector3.up * 0.05f;
            top = new XYZ(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
            return true;
        }

        internal static void Spawn(int harvest)
        {
            try
            {
                Occupation[] occ = UnityEngine.Object.FindObjectsOfType<Occupation>();
                if (occ.Length == 0) { BtcLog.Warn("DevTf", "no workers"); return; }
                Vector3 b = occ[0].transform.position;
                XYZ pit, mound;
                if (!Ground(b.x + 6f, b.z, b.y, out pit) || !Ground(b.x - 6f, b.z, b.y, out mound)) { BtcLog.Warn("DevTf", "no ground"); return; }
                BtcTerraform.Shape s = new BtcTerraform.Shape();
                s.Harvest = harvest & 7;
                s.NoStore = (harvest & 8) != 0;
                BtcTerraform.Op d; d.K = 'D'; d.A = new XYZ(pit.x - 1, pit.y - 2, pit.z - 1); d.B = new XYZ(pit.x + 1, pit.y, pit.z + 1);
                BtcTerraform.Op f; f.K = 'F'; f.A = new XYZ(mound.x, mound.y + 1, mound.z); f.B = new XYZ(mound.x, mound.y + 3, mound.z);   // a column 3 high: fill reach test
                s.Ops.Add(d); s.Ops.Add(f);
                s.Rebuild();
                BtcTerraform.RequestCreate(s);
                BtcLog.Info("DevTf", "spawn pit=" + pit + " mound=" + mound + " dig=" + s.Dig.Count + " fill=" + s.Fill.Count + " harvest=" + s.Harvest + " nostore=" + s.NoStore);
            }
            catch (Exception e) { BtcLog.Warn("DevTf", "spawn: " + e); }
        }

        /// <summary>User case 2026-11-15: one task = a row dug 1 deep + a row filled 1 high (len long, 3 apart).</summary>
        internal static void Rows(int harvest, int len)
        {
            try
            {
                Occupation[] occ = UnityEngine.Object.FindObjectsOfType<Occupation>();
                if (occ.Length == 0) { BtcLog.Warn("DevTf", "no workers"); return; }
                Vector3 b = occ[0].transform.position;
                XYZ a, c;
                if (!Ground(b.x + 5f, b.z, b.y, out a) || !Ground(b.x + 8f, b.z, b.y, out c)) { BtcLog.Warn("DevTf", "no ground"); return; }
                BtcTerraform.Shape s = new BtcTerraform.Shape();
                s.Harvest = harvest & 7;
                s.NoStore = (harvest & 8) != 0;
                for (int i = 0; i < len; i++)
                {
                    XYZ g;
                    if (Ground(a.x, a.z + i - len / 2, a.y, out g)) { BtcTerraform.Op d; d.K = 'D'; d.A = g; d.B = g; s.Ops.Add(d); }
                    if (Ground(c.x, c.z + i - len / 2, c.y, out g)) { BtcTerraform.Op f; f.K = 'F'; f.A = g + XYZ.up; f.B = g + XYZ.up; s.Ops.Add(f); }
                }
                s.Rebuild();
                BtcTerraform.RequestCreate(s);
                BtcLog.Info("DevTf", "rows dig@" + a + " fill@" + c + " dig=" + s.Dig.Count + " fill=" + s.Fill.Count + " harvest=" + s.Harvest);
            }
            catch (Exception e) { BtcLog.Warn("DevTf", "rows: " + e); }
        }

        // two-phase labs (user cases 2026-11-15): tfpit digs a 3x3 pit 4 deep so the workers end up on its floor with no way out,
        // then tftunnel H L (1 wide, H high (2), L long (6) into the west wall at floor level) or tfclimb (quarry ladder chain up the
        // north wall, room dug) is created from the pit floor
        private static XYZ _pit;
        private static bool _hasPit;

        internal static void Pit()
        {
            try
            {
                Occupation[] occ = UnityEngine.Object.FindObjectsOfType<Occupation>();
                if (occ.Length == 0) { BtcLog.Warn("DevTf", "no workers"); return; }
                Vector3 b = occ[0].transform.position;
                if (!Ground(b.x + 7f, b.z, b.y, out _pit)) { BtcLog.Warn("DevTf", "no ground"); return; }
                _hasPit = true;
                BtcTerraform.Shape s = new BtcTerraform.Shape();
                BtcTerraform.Op d; d.K = 'D'; d.A = new XYZ(_pit.x - 1, _pit.y - 3, _pit.z - 1); d.B = new XYZ(_pit.x + 1, _pit.y + 1, _pit.z + 1);
                s.Ops.Add(d);
                s.Rebuild();
                BtcTerraform.RequestCreate(s);
                BtcLog.Info("DevTf", "pit top=" + _pit + " floor y=" + (_pit.y - 4) + " dig=" + s.Dig.Count);
            }
            catch (Exception e) { BtcLog.Warn("DevTf", "pit: " + e); }
        }

        internal static void Tunnel(int h, int len)
        {
            if (!_hasPit) { BtcLog.Warn("DevTf", "tunnel: no pit"); return; }
            BtcTerraform.Shape s = new BtcTerraform.Shape();
            BtcTerraform.Op d; d.K = 'D'; d.A = new XYZ(_pit.x - 1 - Mathf.Clamp(len, 1, 30), _pit.y - 3, _pit.z); d.B = new XYZ(_pit.x - 2, _pit.y - 4 + Mathf.Clamp(h, 1, 3), _pit.z);
            s.Ops.Add(d);
            s.Rebuild();
            BtcTerraform.RequestCreate(s);
            BtcLog.Info("DevTf", "tunnel from " + d.A + " to " + d.B + " dig=" + s.Dig.Count);
        }

        internal static void Climb()
        {
            if (!_hasPit) { BtcLog.Warn("DevTf", "climb: no pit"); return; }
            BtcTerraform.Shape s = new BtcTerraform.Shape();
            var ops = new System.Collections.Generic.List<BtcTerraform.Op>();
            var cells = new System.Collections.Generic.List<XYZ>();
            var ok = new System.Collections.Generic.List<bool>();
            // first ramp in the north wall at floor level, low side towards the pit (+z), climbing north
            BtcTerraform.LadderChain(s, new XYZ(_pit.x, _pit.y - 3, _pit.z - 2), 'Q', new XYZ(0, 0, 1), new XYZ(1, 0, 0), 3, true, ops, cells, ok);
            s.Ops.AddRange(ops);
            s.Rebuild();
            string at = "";
            for (int i = 0; i < cells.Count; i++)
            {
                XYZ u = cells[i] + XYZ.up;
                at += " " + cells[i] + (ok[i] ? " ok" : " BAD") + "[full=" + Voxel.IsFull(cells[i]) + " inv=" + Voxel.IsInvincible(cells[i])
                    + " up:ne=" + Voxel.IsNonEmpty(u) + ",inv=" + Voxel.IsInvincible(u) + ",crust=" + Voxel.IsCrust(u) + "]";
            }
            at += " ops=" + ops.Count + " pit=" + _pit;
            BtcLog.Info("DevTf", "climb dig=" + s.Dig.Count + " ladders=" + s.Ladders.Count + at);
            BtcTerraform.RequestCreate(s);
        }

        // tfladder: a 3x3 pit 3 deep with a tunnel ladder on its floor against the west wall, a quarry ladder in the ground
        // east of the pit and a quarry landing south of it (all harvested)
        internal static void Ladders(int harvest)
        {
            try
            {
                Occupation[] occ = UnityEngine.Object.FindObjectsOfType<Occupation>();
                if (occ.Length == 0) { BtcLog.Warn("DevTf", "no workers"); return; }
                Vector3 b = occ[0].transform.position;
                XYZ pit;
                if (!Ground(b.x + 7f, b.z, b.y, out pit)) { BtcLog.Warn("DevTf", "no ground"); return; }
                BtcTerraform.Shape s = new BtcTerraform.Shape();
                s.Harvest = harvest & 7;
                s.NoStore = (harvest & 8) != 0;
                BtcTerraform.Op d; d.K = 'D'; d.A = new XYZ(pit.x - 1, pit.y - 2, pit.z - 1); d.B = new XYZ(pit.x + 1, pit.y, pit.z + 1);
                BtcTerraform.Op t; t.K = 'T'; t.A = new XYZ(pit.x - 1, pit.y - 2, pit.z); t.B = new XYZ(1, 0, 0);
                BtcTerraform.Op q; q.K = 'Q'; q.A = new XYZ(pit.x + 3, pit.y, pit.z); q.B = new XYZ(0, 0, 1);
                BtcTerraform.Op l; l.K = 'B'; l.A = new XYZ(pit.x, pit.y, pit.z + 3); l.B = XYZ.up;
                // like the tool: a quarry ladder / landing aimed at a crust goes into the full block under it (the crust stays)
                if (Voxel.IsCrust(q.A)) q.A = q.A - XYZ.up;
                if (Voxel.IsCrust(l.A)) l.A = l.A - XYZ.up;
                BtcLog.Info("DevTf", "q at " + q.A + " crustAbove=" + Voxel.IsCrust(q.A + XYZ.up) + ", b at " + l.A + " crustAbove=" + Voxel.IsCrust(l.A + XYZ.up));
                s.Ops.Add(d); s.Ops.Add(t); s.Ops.Add(q); s.Ops.Add(l);
                s.Rebuild();
                BtcLog.Info("DevTf", "ladders pit=" + pit + " dig=" + s.Dig.Count + " ladders=" + s.Ladders.Count
                    + " q.ok=" + BtcTerraform.LadderPlaceable(q.A, 'Q', q.B, s) + " t.ok=" + BtcTerraform.LadderPlaceable(t.A, 'T', t.B, s)
                    + " b.ok=" + BtcTerraform.LadderPlaceable(l.A, 'B', l.B, s));
                BtcTerraform.RequestCreate(s);
            }
            catch (Exception e) { BtcLog.Warn("DevTf", "ladders: " + e); }
        }

        // tfsteps: a 2x3 pit, then a chain of 3 quarry ladders climbing out of it along -x with room dug above each
        // (the tool's ladder drag with "Dig room for ladders"), nothing kept
        internal static void Steps()
        {
            try
            {
                Occupation[] occ = UnityEngine.Object.FindObjectsOfType<Occupation>();
                if (occ.Length == 0) { BtcLog.Warn("DevTf", "no workers"); return; }
                Vector3 b = occ[0].transform.position;
                XYZ top;
                if (!Ground(b.x + 6f, b.z, b.y, out top)) { BtcLog.Warn("DevTf", "no ground"); return; }
                BtcTerraform.Shape s = new BtcTerraform.Shape();
                s.Harvest = 0;
                BtcTerraform.Op d; d.K = 'D';
                d.A = new XYZ(top.x, top.y - 4, top.z); d.B = new XYZ(top.x + 1, top.y + 1, top.z + 2);
                s.Ops.Add(d);
                s.Rebuild();
                // first ladder in the pit wall at floor level, low side towards the pit (+x), climbing towards -x inside the
                // hill (the rooms of ladders 2-3 are sealed in rock: the passage dig must open them)
                var ops = new System.Collections.Generic.List<BtcTerraform.Op>();
                var cells = new System.Collections.Generic.List<XYZ>();
                var ok = new System.Collections.Generic.List<bool>();
                BtcTerraform.LadderChain(s, new XYZ(top.x - 1, top.y - 4, top.z + 1), 'Q', new XYZ(1, 0, 0), new XYZ(1, 0, 0), 3, true, ops, cells, ok);
                s.Ops.AddRange(ops);
                s.Rebuild();
                int n = 0;
                for (int i = 0; i < ok.Count; i++) if (ok[i]) n++;
                string at = "";
                for (int i = 0; i < cells.Count; i++)
                    at += " c" + i + "=" + cells[i] + (ok[i] ? " ok" : " BAD") + " full=" + Voxel.IsFull(cells[i]) + " crust=" + Voxel.IsCrust(cells[i])
                        + " terr=" + Voxel.IsTerrain(cells[i]) + " under=" + Voxel.IsNonEmpty(cells[i] - XYZ.up) + ";";
                foreach (System.Collections.Generic.KeyValuePair<XYZ, BtcTerraform.Ladder> kv in s.Ladders) at += " " + kv.Key + "/" + kv.Value.F;
                BtcLog.Info("DevTf", "steps top=" + top + " dig=" + s.Dig.Count + " autoLadders=" + n + at);
                BtcTerraform.RequestCreate(s);
            }
            catch (Exception e) { BtcLog.Warn("DevTf", "steps: " + e); }
        }

        // tfedit: edits the first terraform task: erases all its fill cells and extends the dig by a 2x2x2 box east of it
        internal static void Edit()
        {
            try
            {
                foreach (LandscapeProject p in UnityEngine.Object.FindObjectsOfType<LandscapeProject>())
                {
                    BtcTerraform.Shape cur = BtcTerraform.Of(p);
                    if (cur == null) continue;
                    BtcTerraform.Shape s = cur.Clone();
                    BtcTerraform.Op e; e.K = 'E'; e.A = new XYZ(s.Min.x - 4, s.Min.y - 4, s.Min.z - 4); e.B = new XYZ(s.Max.x + 4, s.Max.y + 4, s.Max.z + 4);
                    XYZ m = new XYZ(int.MinValue, 0, 0);
                    foreach (XYZ c in cur.Dig) if (c.x > m.x || (c.x == m.x && c.y > m.y)) m = c;
                    BtcTerraform.Op d; d.K = 'D'; d.A = new XYZ(m.x + 1, m.y - 1, m.z); d.B = new XYZ(m.x + 2, m.y, m.z + 1);
                    // keep the dig, drop the fill: erase everything, then re-add the old dig cells and the new box
                    s.Ops.Clear(); s.Ops.Add(e);
                    foreach (XYZ c in cur.Dig) { BtcTerraform.Op o; o.K = 'D'; o.A = c; o.B = c; s.Ops.Add(o); }
                    s.Ops.Add(d);
                    s.Rebuild();
                    uint nid = p.GetComponent<UnityEngine.Networking.NetworkIdentity>().netId.Value;
                    BtcLog.Info("DevTf", "edit task " + nid + ": dig " + cur.Dig.Count + "->" + s.Dig.Count + " fill " + cur.Fill.Count + "->" + s.Fill.Count);
                    BtcTerraform.RequestEdit(nid, s);
                    return;
                }
                BtcLog.Warn("DevTf", "edit: no terraform task");
            }
            catch (Exception ex) { BtcLog.Warn("DevTf", "edit: " + ex); }
        }

        /// <summary>tfui new | dig N (a 1x1 column N deep next to the workers, through the tool = create or edit) | keep F | haul |
        /// mode M | undo | esc | state: the task tool as the C# popup drives it (no mouse).</summary>
        internal static void Ui(string what, int n)
        {
            try
            {
                switch (what)
                {
                    case "new": BtcTerraformTool.OpenNew(); break;
                    case "dig":
                        {
                            Occupation[] occ = UnityEngine.Object.FindObjectsOfType<Occupation>();
                            if (occ.Length == 0) { BtcLog.Warn("DevTf", "ui dig: no workers"); return; }
                            Vector3 b = occ[0].transform.position;
                            XYZ g;
                            if (!Ground(b.x + 5f + n, b.z + 3f, b.y, out g)) { BtcLog.Warn("DevTf", "ui dig: no ground"); return; }
                            BtcTerraform.Op d; d.K = 'D'; d.A = new XYZ(g.x, g.y - Mathf.Max(0, n - 1), g.z); d.B = g;
                            BtcTerraformTool.DevStroke(d);
                            break;
                        }
                    case "keep": BtcTerraformTool.ToggleKeep(n); break;
                    case "haul": BtcTerraformTool.ToggleStoreFull(); break;
                    case "mode": BtcTerraformTool.SetMode(n); break;
                    case "undo": BtcTerraformTool.DevUndo(); break;
                    case "esc": BtcTerraformTool.DevLeave(); break;
                }
                uint nid; BtcTerraform.Shape s;
                bool sel = BtcTerraform.SelectedOwn(out nid, out s);
                BtcLog.Info("DevTf", "ui " + what + " " + n + ": active=" + BtcTerraformTool.Active + " placing=" + BtcTerraformTool.Placing
                    + " edit=" + BtcTerraformTool.EditingTask + " sel=" + (sel ? nid + " dig=" + s.Dig.Count + " keep=" + s.Harvest + " haul=" + s.StoreFull : "-")
                    + " mode=" + BtcTerraformTool.TfMode + " keepTool=" + BtcTerraformTool.Harvest + " size=" + BtcTerraformTool.W + "x" + BtcTerraformTool.H
                    + " tasks=" + UnityEngine.Object.FindObjectsOfType<LandscapeProject>().Length + " popupShown=" + BtcGhPopup.Shown);
            }
            catch (Exception e) { BtcLog.Warn("DevTf", "ui: " + e); }
        }

        internal static void Hire()
        {
            try
            {
                int n = 0;
                foreach (LandscapeProject p in UnityEngine.Object.FindObjectsOfType<LandscapeProject>())
                {
                    if (BtcTerraform.Of(p) == null) continue;
                    foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
                        if (l.faction == p.faction && p.Hire(l)) n++;
                }
                BtcLog.Info("DevTf", "hired " + n);
            }
            catch (Exception e) { BtcLog.Warn("DevTf", "hire: " + e); }
        }

        private static string GoalKinds(LandscapeProject p)
        {
            System.Collections.Generic.List<Goal> all = new System.Collections.Generic.List<Goal>();
            p.goalProvider.GetAll(all);
            int dig = 0, fill = 0, other = 0, stairs = 0, nul = 0;
            foreach (Goal g in all)
            {
                if (g.IsNullOrReleased()) { nul++; continue; }
                LandscapeGoal lg = g as LandscapeGoal;
                if (lg != null) { if (lg.GetLandscapeType() == LandscapeGoal.Landscape.Fill) fill++; else if (lg.GetLandscapeType() == LandscapeGoal.Landscape.Dig) dig++; else other++; }
                else if (g is BuildStairsGoal) stairs++; else if (g is DigGoal) dig++; else other++;
            }
            Labor l0 = null;
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>()) if (l.faction == p.faction) { l0 = l; break; }
            string w = l0 == null ? "-" : (p.HasWorkFor(l0) + "/room=" + l0.recepteur.HasRoomInBag());
            if (l0 != null)
            {
                // which goals the knowledge space lets a worker reach (and where they are)
                int reach = 0; string gs = "";
                foreach (Goal g in all)
                {
                    if (g.IsNullOrReleased()) continue;
                    bool r = Brix.Game.AI.KnowledgeSpace.Knowledge.Instance.CanReach(l0, g, -1);
                    if (r) reach++;
                    if (gs.Length < 400) gs += " " + g.voxel.position + (r ? "+" : "-");
                }
                w += " reach=" + reach + " w0@" + XYZ.FromVector3(l0.transform.position) + " cur=" + l0.presence.CurrentVoxel + " goals:" + gs;
                // access cells of the first goals (offsets from the goal voxel) and which of them are walkable
                int shown = 0;
                foreach (Goal g in all)
                {
                    if (g.IsNullOrReleased() || shown++ >= 3) continue;
                    GameObject go = g.gameObject;
                    XYZ c = g.voxel.position;
                    string acc = "";
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dy = -2; dy <= 2; dy++)
                            for (int dz = -2; dz <= 2; dz++)
                            {
                                XYZ q = new XYZ(c.x + dx, c.y + dy, c.z + dz);
                                if (!Brix.Game.Components.Volumes.VolumeRegistry.HasVolumesWithPropertyAt(q, Brix.Game.Components.Volumes.VProperty.access, v => v.GameObject == go)) continue;
                                acc += " (" + dx + "," + dy + "," + dz + (Brix.Game.Components.Volumes.VolumeRegistry.HasVolumesWithPropertyAt(q, Brix.Game.Components.Volumes.VProperty.walkable) ? " W" : "") + ")";
                            }
                    w += " | acc " + c + ":" + acc;
                }
            }
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
            {
                if (l.faction != p.faction) continue;
                AIError err = p.errorStore.GetMostImportantError(l);
                Goal cur = l.Goal;
                w += " | " + l.GetInstanceID() + " proj=" + (l.Project == null ? "-" : l.Project.GetType().Name)
                    + " goal=" + (cur == null ? "-" : cur.GetType().Name + "@" + cur.voxel.position) + " err=" + (err == null ? "-" : err.ToString());
            }
            return "kinds dig=" + dig + " fill=" + fill + " stairs=" + stairs + " other=" + other + " null=" + nul + " work=" + w;
        }

        internal static void Dump(string tag)
        {
            try
            {
                int tasks = 0;
                foreach (LandscapeProject p in UnityEngine.Object.FindObjectsOfType<LandscapeProject>())
                {
                    BtcTerraform.Shape s = BtcTerraform.Of(p);
                    if (s == null) continue;
                    tasks++;
                    int digLeft = 0, fillLeft = 0;
                    string why = "";
                    foreach (XYZ c in s.Dig)
                    {
                        if (Voxel.IsEmpty(c)) continue;
                        digLeft++;
                        if (digLeft <= 4)
                            why += " " + c + "[full=" + Voxel.IsFull(c) + " crust=" + Voxel.IsCrust(c) + " terr=" + Voxel.IsTerrain(c) + " block=" + Voxel.IsBlock(c)
                                + " airNb=" + Voxel.HasEmptyNeighbor(c) + " needs=" + p.goalProvider.NeedsDig(c) + " inv=" + Voxel.IsInvincible(c) + "]";
                    }
                    if (why.Length > 0) BtcLog.Info("DevTf", tag + ": dig left" + why);
                    foreach (XYZ c in s.Fill) if (Voxel.IsEmpty(c) || Voxel.IsCrust(c)) fillLeft++;
                    int built = 0;
                    foreach (XYZ c in s.Ladders.Keys) if (QuarryGoalProvider2.IsStair(c)) built++;
                    foreach (System.Collections.Generic.KeyValuePair<XYZ, BtcTerraform.Ladder> kv in s.Ladders)
                    {
                        XYZ c = kv.Key;
                        BtcLog.Info("DevTf", tag + ": ladder " + kv.Value.K + "@" + c + " ready=" + BtcTerraform.LadderReady(c, kv.Value)
                            + " stair=" + QuarryGoalProvider2.IsStair(c) + " empty=" + Voxel.IsEmpty(c) + " block=" + Voxel.IsBlock(c)
                            + " inv=" + Voxel.IsInvincible(c) + " upEmpty=" + Voxel.IsEmpty(c + XYZ.up) + " upCrust=" + Voxel.IsCrust(c + XYZ.up)
                            + " downEmpty=" + Voxel.IsEmpty(c - XYZ.up) + " nature=" + Brix.Engine.Nature.NatureRegistry.HasNatureBlockerAt(c)
                            + "/" + Brix.Engine.Nature.NatureRegistry.HasNatureBlockerAt(c + XYZ.up) + " inVol=" + s.Vol.Contains(c)
                            + " upInDig=" + s.Dig.Contains(c + XYZ.up) + " upFull=" + Voxel.IsFull(c + XYZ.up) + " upTerr=" + Voxel.IsTerrain(c + XYZ.up)
                            + " upGeo=" + Brix.Engine.Trackers.GeostaticityTracker.IsGeostatic(c + XYZ.up));
                    }
                    BtcLog.Info("DevTf", tag + ": task digLeft=" + digLeft + "/" + s.Dig.Count + " fillLeft=" + fillLeft + "/" + s.Fill.Count
                        + " ladders=" + built + "/" + s.Ladders.Count + " " + GoalKinds(p)
                        + " goals=" + p.goalProvider.GoalCount() + " crew=" + p.CrewCount() + " harvest=" + s.Harvest + " nostore=" + s.NoStore);
                }
                BtcLog.Info("DevTf", tag + ": terraform tasks=" + tasks + " landscape=" + UnityEngine.Object.FindObjectsOfType<LandscapeProject>().Length);
                Workers(tag);
            }
            catch (Exception e) { BtcLog.Warn("DevTf", "dump: " + e); }
        }

        private static readonly System.Collections.Generic.Dictionary<int, Vector3> LastPos = new System.Collections.Generic.Dictionary<int, Vector3>();

        // every worker: transform cell vs locomotion cell (a mismatch = moved by BuryBehavior), moved since the last dump, task, icon
        private static void Workers(string tag)
        {
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
            {
                if (l.navigation == null || l.navigation.Locomotion == null) continue;
                Locomotion4 loc = l.navigation.Locomotion;
                Vector3 at = l.transform.position;
                Vector3 last;
                float moved = LastPos.TryGetValue(l.GetInstanceID(), out last) ? Vector3.Distance(last, at) : -1f;
                LastPos[l.GetInstanceID()] = at;
                XYZ cell = XYZ.FromVector3(at);
                BtcLog.Info("DevTf", tag + ": worker " + l.name + " cell=" + cell + " occ=" + loc.PositionOccupied + " ent=" + loc.PositionEntering
                    + " st=" + loc.state + " inPlace=" + loc.IsInPlace() + " moved=" + moved.ToString("0.0")
                    + " feetSolid=" + (!Voxel.IsEmpty(cell)) + " headSolid=" + (!Voxel.IsEmpty(cell + XYZ.up))
                    + " task=" + (l.CurrentTask == null ? "-" : l.CurrentTask.GetType().Name)
                    + " goal=" + (l.Goal.IsNullOrReleased() ? "-" : l.Goal.GetType().Name + "@" + l.Goal.voxel.position + (TerraformDigSetting.IsFill(l.Goal) ? "F" : ""))
                    + " bag=" + l.recepteur.ContentDescription.Value<Stones>());
            }
        }
    }
}
#endif
