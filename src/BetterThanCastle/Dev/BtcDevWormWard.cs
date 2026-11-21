#if DEV
using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV lab for wormhole wards (BtcWormWard). Scenario commands:
    /// wormward spawn dx dz   - a sentinel ward of the local player at home crystal + (dx, dz) on the surface, marked as wormhole ward;
    /// wormward plain dx dz   - the same without the mark (a vanilla sentinel ward);
    /// wormward dump tag      - rule state and every mark (placed/powered/clear/usable), trips/aborts of the wormhole network;
    /// wormward toggle i      - switches the teleport of mark i (spawn order) like the pie menu;
    /// wormward nest          - switches the teleport of the local home crystal like the pie menu;
    /// wormward bury i        - puts a voxel (copy of the ground under it) in the cell above ward i;
    /// wormward go i          - the first phantom runs to 3 blocks beside ward i;
    /// wormward craft         - the host finishes one wormhole ward recipe at the home crystal position (marks the result).
    /// </summary>
    internal static class BtcDevWormWard
    {
        private static readonly List<GameObject> Spawned = new List<GameObject>();
        internal static readonly List<WeakReference> Scripts = new List<WeakReference>();

        internal static bool Cmd(string[] p)
        {
            string sub = p.Length > 1 ? p[1] : "dump";
            try
            {
                switch (sub)
                {
                    case "spawn": Spawn(p, true); break;
                    case "plain": Spawn(p, false); break;
                    case "dump": BtcDevLab.Lab("wormward dump " + (p.Length > 2 ? p[2] : "-") + " " + BtcWormWard.Dump() + " trips=" + BtcPhantomWorm.Trips + " aborted=" + BtcPhantomWorm.Aborted
                                               + " recipe=" + (BtcWormWardCraft.WormRecipe != null ? BtcWormWardCraft.WormRecipe.id.ToString() : "null")); break;
                    case "toggle": Toggle(I(p)); break;
                    case "nest": Nest(); break;
                    case "bury": Bury(I(p)); break;
                    case "go": Go(I(p)); break;
                    case "craft": Craft(); break;
                    case "bp": Bp(p); break;
                    case "find": FindFill(false); break;
                    case "fillwrong": FillWrong(); break;
                    case "drop": DropHeld(); break;
                    case "pickup": PickUp(I(p)); break;
                    case "ctoggle": CToggle(p.Length > 2 ? p[2] : "own"); break;
                    case "find0": BtcWormWardBuild.DevNoFilter = true; FindFill(false); BtcWormWardBuild.DevNoFilter = false; break;
                    case "fill": FindFill(true); break;
                    case "wards": Wards(p.Length > 2 ? p[2] : "-"); break;
                    case "lua": Lua(); break;
                    default: BtcDevLab.Lab("wormward: unknown " + sub); break;
                }
            }
            catch (Exception e) { BtcDevLab.Lab("wormward " + sub + " failed: " + e.Message); }
            return true;
        }

        private static int I(string[] p) { return p.Length > 2 ? int.Parse(p[2]) : 0; }

        private static FireflyNest Home()
        {
            User u = User.LocalUser;
            List<FireflyNest> nests = BtcMatchStats.NestList();
            if (u == null || nests == null) return null;
            foreach (FireflyNest n in nests) if (n != null && n.isHome && n.faction == u.faction) return n;
            return null;
        }

        private static GameObject Ward(int i) { return i >= 0 && i < Spawned.Count ? Spawned[i] : null; }

        private static void Spawn(string[] p, bool mark)
        {
            Faction fac = User.LocalUser.faction;
            if (p.Length > 4 && p[4] == "other")
            {
                List<FireflyNest> ns = BtcMatchStats.NestList();
                if (ns != null) foreach (FireflyNest n in ns) if (n != null && n.isHome && n.faction != null && n.faction != fac) { fac = n.faction; break; }
            }
            FireflyNest home = null;
            List<FireflyNest> nests = BtcMatchStats.NestList();
            if (nests != null) foreach (FireflyNest n in nests) if (n != null && n.isHome && n.faction == fac) home = n;
            if (home == null) { BtcDevLab.Lab("wormward spawn: no home crystal"); return; }
            float dx = p.Length > 2 ? float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture) : 6f;
            float dz = p.Length > 3 ? float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture) : 0f;
            Vector3 s = Vector3.zero;
            bool found = false;
            for (int r = 0; r <= 14 && !found; r++)          // the nearest open, walkable, object-free surface cell round the asked point
                for (int ox = -r; ox <= r && !found; ox++)
                    for (int oz = -r; oz <= r && !found; oz++)
                    {
                        if (Mathf.Max(Mathf.Abs(ox), Mathf.Abs(oz)) != r) continue;
                        s = Architecte.SurfaceRaycast(home.transform.position + new Vector3(dx + ox, 20f, dz + oz), 60, true);
                        XYZ c = XYZ.FromVector3(s + Vector3.up * 0.1f);
                        found = Mathf.Abs(s.y - home.transform.position.y) <= 6f && Voxel.IsOpenAndWalkable(c) && BtcWormWard.Clear(s)
                                && !Brix.Game.Components.Volumes.VolumeRegistry.HasVolumesWithPropertyAt(c, Brix.Game.Components.Volumes.VPropertyMask.obstacle);
                    }
            Vector3 at = s + Vector3.up * 0.05f;      // x.5 heights round into the ground cell (the ward then goes back to the pool)
            Brix.Lua.LuaDriver d = Brix.Lua.LuaFactory.InstantiateFromCLR(ObjetsDynamiques.SentinelWard, fac, at, Quaternion.identity);
            GameObject go = d != null ? d.gameObject : null;
            if (go == null) { BtcDevLab.Lab("wormward spawn: no object"); return; }
            if (mark) BtcWormWard.Mark(go, false);
            Spawned.Add(go);
            BtcDevLab.Lab("wormward spawn #" + (Spawned.Count - 1) + " mark=" + mark + " at=" + go.transform.position + " surface=" + s + " dist=" + Vector3.Distance(at, home.transform.position).ToString("F1"));
        }

        /// <summary>wormward ctoggle own|other: the pie switch on the first marked ward of this peer (own) or of someone else (other; must be refused).</summary>
        private static void CToggle(string which)
        {
            foreach (GameObject go in BtcWormWard.AllMarked())
            {
                bool own = Faction.GetFaction(go) == User.LocalUser.faction;
                if (own != (which == "own")) continue;
                object keep = Brix.Legacy.Instructions.objet;
                Brix.Legacy.Instructions.objet = go;
                bool vis = BtcWormWard.PieWard(false) || BtcWormWard.PieWard(true);
                bool before = BtcWormWard.IsOff(go);
                if (vis || which != "own") BtcWormWard.PieToggleWard();
                Brix.Legacy.Instructions.objet = keep;
                BtcDevLab.Lab("wormward ctoggle " + which + " " + go.name + " pieVisible=" + vis + " offBefore=" + before);
                return;
            }
            BtcDevLab.Lab("wormward ctoggle " + which + ": no such ward");
        }

        /// <summary>wormward fillwrong: puts a plain sentinel ward (spawned list) into the first tagged blueprint: it must be refused (dropped).</summary>
        private static void FillWrong()
        {
            foreach (Blueprint b in UnityEngine.Object.FindObjectsOfType<Blueprint>())
            {
                if (!BtcWormWardBuild.IsTagged(b.gameObject)) continue;
                GameObject plain = null;
                foreach (GameObject s in Spawned) if (s != null && s.activeInHierarchy && !BtcWormWard.IsWorm(s) && s.transform.parent != null && s.transform.parent.name == "ObjetsDynamiques") { plain = s; break; }
                if (plain == null) { BtcDevLab.Lab("wormward fillwrong: no plain ward"); return; }
                b.recepteur.AddObject(plain);
                string kids = "";
                Transform ct = b.recepteur.Container;
                if (ct != null) for (int k = 0; k < ct.childCount; k++) kids += " " + ct.GetChild(k).name + (BtcWormWard.IsWorm(ct.GetChild(k).gameObject) ? "(W)" : "");
                BtcDevLab.Lab("wormward fillwrong added: full=" + b.recepteur.IsFull() + " bpName=" + b.name + " plain=" + plain.name + " id=" + plain.GetInstanceID() + " kids:" + kids);
                BtcDevLab.Lab("wormward fillwrong bp at=" + b.transform.position + " -> blueprint " + (b != null && b.gameObject.activeInHierarchy ? "still there" : "GONE") + " plain ward parent=" + (plain.transform.parent != null ? plain.transform.parent.name : "-") + " act=" + plain.activeInHierarchy);
                return;
            }
            BtcDevLab.Lab("wormward fillwrong: no tagged blueprint");
        }

        /// <summary>wormward drop: every own worker that holds a sentinel ward drops it (vanilla Drop order).</summary>
        private static void DropHeld()
        {
            int n = 0;
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
            {
                GameObject held;
                if (l.faction != User.LocalUser.faction || l.recepteur == null || !l.recepteur.FirstObjectOf(ObjetsDynamiques.SentinelWard, out held)) continue;
                BtcDevLab.Lab("wormward drop by " + l.name + " ward=" + held.name + " id=" + held.GetInstanceID() + " worm=" + BtcWormWard.IsWorm(held));
                Order.Transfer.Drop.Send(l, Labor.DecisionSource.User);
                n++;
            }
            if (n == 0) BtcDevLab.Lab("wormward drop: nobody holds a ward");
        }

        /// <summary>wormward pickup i: the nearest free own worker picks up ward i (Spawned list, or the first loose marked ward if i &lt; 0).</summary>
        private static void PickUp(int i)
        {
            GameObject go = Ward(i);
            if (go == null) foreach (GameObject m in BtcWormWard.AllMarked()) if (m.activeInHierarchy && BtcWormWard.Placed(m)) { go = m; break; }
            if (go == null) { BtcDevLab.Lab("wormward pickup: no ward"); return; }
            Labor best = null; float bd = float.MaxValue;
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
            {
                if (l.faction != User.LocalUser.faction || !l.gameObject.activeInHierarchy || BtcPhantom.IsPhantom(l.gameObject) || l.recepteur.CarriesSomething()) continue;
                float d = (l.transform.position - go.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = l; }
            }
            if (best == null) { BtcDevLab.Lab("wormward pickup: no worker"); return; }
            Order.Transfer.GoPickUpOne.Send(best, go, Labor.DecisionSource.User);
            BtcDevLab.Lab("wormward pickup " + go.name + " id=" + go.GetInstanceID() + " worm=" + BtcWormWard.IsWorm(go) + " by " + best.name);
        }

        private static void Toggle(int i)
        {
            GameObject go = Ward(i);
            if (go == null) { BtcDevLab.Lab("wormward toggle: no ward " + i); return; }
            object keep = Brix.Legacy.Instructions.objet;
            Brix.Legacy.Instructions.objet = go;
            bool vis = BtcWormWard.PieWard(false) || BtcWormWard.PieWard(true);
            BtcWormWard.PieToggleWard();
            Brix.Legacy.Instructions.objet = keep;
            BtcDevLab.Lab("wormward toggle #" + i + " pieVisible=" + vis + " off=" + BtcWormWard.IsOff(go));
        }

        private static void Nest()
        {
            FireflyNest home = Home();
            if (home == null) { BtcDevLab.Lab("wormward nest: no home crystal"); return; }
            object keep = Brix.Legacy.Instructions.objet;
            Brix.Legacy.Instructions.objet = home.gameObject;
            bool vis = BtcWormWard.PieNest(false) || BtcWormWard.PieNest(true);
            BtcWormWard.PieToggleNest();
            Brix.Legacy.Instructions.objet = keep;
            BtcDevLab.Lab("wormward nest pieVisible=" + vis + " off=" + BtcWormWard.NestOff(home));
        }

        private static void Bury(int i)
        {
            GameObject go = Ward(i);
            if (go == null) { BtcDevLab.Lab("wormward bury: no ward " + i); return; }
            XYZ c = XYZ.FromVector3(go.transform.position + Vector3.up * 0.1f);
            int top = VoxelEngine.GetColumnTop(c.x, c.z);
            VoxelEngine.BeginChanges();
            VoxelEngine.SetColumnTopVoxel(c.x, c.z, c.y + 2);     // the ward column grows 2 blocks: the ward is buried
            VoxelEngine.ApplyChanges(false);
            for (int y = c.y - 1; y <= c.y + 3; y++)
            {
                XYZ q = new XYZ(c.x, y, c.z);
                GameSignals.Invoke<XYZ>(GameSignals.VoxelChanged, q);
                GameSignals.Invoke<XYZ>(GameSignals.TerrainChanged, q);
                Architecte.Changement(q);
            }
            BtcDevLab.Lab("wormward bury #" + i + " cell=" + c + " top " + top + " -> " + VoxelEngine.GetColumnTop(c.x, c.z) + " clear=" + BtcWormWard.Clear(go.transform.position)
                          + " ward=" + (go != null && go.activeInHierarchy));
        }

        /// <summary>wormward bp dx dz worm|plain: a sentinel blueprint at home + (dx, dz) in the current project, tagged like the build menu does.</summary>
        private static void Bp(string[] p)
        {
            FireflyNest home = Home();
            if (home == null) { BtcDevLab.Lab("wormward bp: no home"); return; }
            float dx = p.Length > 2 ? float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture) : 6f;
            float dz = p.Length > 3 ? float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture) : 0f;
            bool worm = p.Length > 4 && p[4] == "worm";
            int x = Mathf.FloorToInt(home.transform.position.x + dx), z = Mathf.FloorToInt(home.transform.position.z + dz);
            Vector3 pos = Architecte.SurfaceRaycast(new Vector3(x, home.transform.position.y + 20f, z), 60, true);
            GameObject proj = null;
            try { if (UIGameObserver.projects != null && UIGameObserver.projects.CurrentSelected != null) proj = UIGameObserver.projects.CurrentSelected.gameObject; } catch { }
            if (proj == null) { BuildProject[] all = UnityEngine.Object.FindObjectsOfType<BuildProject>(); for (int i = 0; i < all.Length; i++) { BtcDevLab.Lab("wormward bp project cand " + all[i].name + " bgp=" + (all[i].GetComponent<BuildGoalProvider>() != null) + " fac=" + Faction.GetFaction(all[i].gameObject) + " fac=" + Faction.GetFaction(all[i].gameObject)); if (proj == null && all[i].GetComponent<BuildGoalProvider>() != null && Faction.GetFaction(all[i].gameObject) == User.LocalUser.faction) proj = all[i].gameObject; } }
            if (proj == null)
            {
                User.LocalUser.GetComponent<Brix.Game.Network.UNetGroupe>().CmdSpawnGoupe(Groupes.Construction.Factory, Groupes.Construction.Name, home.transform.position, Quaternion.identity, false);
                foreach (BuildProject b in UnityEngine.Object.FindObjectsOfType<BuildProject>()) if (Faction.GetFaction(b.gameObject) == User.LocalUser.faction || Faction.GetFaction(b.gameObject) == null) proj = b.gameObject;
                int n = 0;
                if (proj != null)
                    foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
                        if (l.faction == User.LocalUser.faction && l.gameObject.activeInHierarchy && !BtcPhantom.IsPhantom(l.gameObject)) { User.LocalUser.GetComponent<Brix.Game.Network.UNetProjectCmd>().CmdChangeProject(l.gameObject, proj); n++; }
                BtcDevLab.Lab("wormward bp: new build project " + (proj != null ? proj.name : "null") + ", " + n + " worker(s) assigned");
            }
            if (worm) BtcWormWardBuild.DevTag(pos, User.LocalUser.faction);
            int before = UnityEngine.Object.FindObjectsOfType<Blueprint>().Length;
            User.LocalUser.GetComponent<Brix.Game.Network.UNetBlueprint>().CmdSpawnPlacedBlueprint("Blueprints", "SentinelWard", pos, Quaternion.identity, false, proj, false);
            BtcDevLab.Lab("wormward bp worm=" + worm + " at=" + pos + " project=" + (proj != null ? proj.name : "null") + " blueprints " + before + " -> " + UnityEngine.Object.FindObjectsOfType<Blueprint>().Length + " tagged=" + BtcWormWardBuild.TaggedCount);
        }

        /// <summary>wormward find|fill: for every sentinel blueprint, what Labor.Find returns for an own worker (the item a builder would
        /// fetch); fill also puts that item into the blueprint (the blueprint turns into a ward: MorphToObject).</summary>
        private static void FindFill(bool fill)
        {
            Labor w = null;
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
                if (l.faction == User.LocalUser.faction && l.gameObject.activeInHierarchy && !BtcPhantom.IsPhantom(l.gameObject) && !l.recepteur.CarriesSomething()) { w = l; break; }
            if (w == null) { BtcDevLab.Lab("wormward find: no free worker"); return; }
            foreach (Blueprint b in UnityEngine.Object.FindObjectsOfType<Blueprint>())
            {
                if (b.AssetKey != Brix.External.Factories.Blueprints.SentinelWard) continue;
                GameObject got = w.Find(b.recepteur);
                string cap = "";
                try { cap = b.recepteur.CurrentCapacity != null ? b.recepteur.CurrentCapacity.ToString() : "null"; } catch (Exception e) { cap = "err " + e.Message; }
                string kind = got == null ? "none" : BtcWormWard.IsWorm(got) ? "WORM" : "plain";
                string res = "";
                if (fill && got == null)      // DEV-spawned loose wards are not in the builders' knowledge: use the spawned ones directly
                    foreach (GameObject s in Spawned) { if (s != null && s.activeInHierarchy && s.transform.parent != null && s.transform.parent.name == "ObjetsDynamiques" && BtcWormWard.IsWorm(s) == BtcWormWardBuild.IsTagged(b.gameObject)) { got = s; break; } }
                BtcWormWardBuild.BeginFind(b.recepteur);
                string acc = "";
                foreach (GameObject s in Spawned) if (s != null) acc += (BtcWormWard.IsWorm(s) ? " worm:" : " plain:") + BtcWormWardBuild.Accept(s, null);
                BtcWormWardBuild.Want = 0;
                kind = got == null ? "none" : BtcWormWard.IsWorm(got) ? "WORM" : "plain";
                cap += " accept[" + acc + " ]";
                if (fill && got != null)
                {
                    Vector3 at = b.transform.position;
                    b.recepteur.AddObject(got);
                    res = " -> after fill: blueprint " + (b != null && b.gameObject.activeInHierarchy ? "still there" : "gone");
                }
                BtcDevLab.Lab("wormward " + (fill ? "fill" : "find") + " bp at=" + b.transform.position + " tagged=" + BtcWormWardBuild.IsTagged(b.gameObject) + " cap=" + cap + " picks=" + kind + (got != null ? " " + got.name : "") + res);
            }
        }

        /// <summary>wormward wards tag: every sentinel ward object (standing, stored, held) with its kind.</summary>
        private static void Wards(string tag)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder("wormward wards " + tag + ":");
            foreach (Brix.Game.Components.Wards.WardAttack a in Resources.FindObjectsOfTypeAll<Brix.Game.Components.Wards.WardAttack>())
            {
                if (a == null || a.gameObject.scene.name == null || !a.gameObject.scene.IsValid()) continue;
                Transform par = a.transform.parent;
                if (!a.gameObject.activeInHierarchy && par == null) continue;   // pooled
                sb.Append(" [").Append(BtcWormWard.IsWorm(a.gameObject) ? "WORM" : "plain").Append(" act=").Append(a.gameObject.activeInHierarchy).Append(" parent=").Append(par != null ? par.name : "-").Append(" at=").Append(a.transform.position).Append(']');
            }
            sb.Append(" blueprints=").Append(UnityEngine.Object.FindObjectsOfType<Blueprint>().Length).Append(" tagged=").Append(BtcWormWardBuild.TaggedCount);
            BtcDevLab.Lab(sb.ToString());
        }

        private static void Craft()
        {
            List<GameObject> live = BtcPhantom.DevLive();
            Labor w = null;
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
                if (l.faction == User.LocalUser.faction && l.gameObject.activeInHierarchy && !BtcPhantom.IsPhantom(l.gameObject)) { w = l; break; }
            if (w == null || BtcWormWardCraft.WormRecipe == null) { BtcDevLab.Lab("wormward craft: no worker / recipe"); return; }
            int before = BtcWormWard.Count;
            Recipe r = BtcWormWardCraft.WormRecipe.GetRecipe();
            r.Finish(w.recepteur, null);
            GameObject held;
            bool has = w.recepteur.FirstObjectOf(ObjetsDynamiques.SentinelWard, out held);
            if (has) Spawned.Add(held);
            BtcDevLab.Lab("wormward craft marks " + before + " -> " + BtcWormWard.Count + " held=" + has + (has ? " #" + (Spawned.Count - 1) + " worm=" + BtcWormWard.IsWorm(held) + " placed=" + BtcWormWard.Placed(held) + " usable=" + BtcWormWard.Usable(held) + " parent=" + (held.transform.parent != null ? held.transform.parent.name : "-") : "")
                          + " toLua=" + (int)Brix.Lua.LuaCrafting.ToLuaRecipe(BtcWormWardCraft.WormRecipe) + " back=" + Brix.Lua.LuaCrafting.ToRecipeInfo((Brix.Lua.LuaCrafting.LuaRecipe)BtcWormWardCraft.LuaValue)[0].id);
        }

        private static void Lua()
        {
            int n = 0;
            foreach (WeakReference wr in Scripts)
            {
                MoonSharp.Interpreter.Script s = wr.Target as MoonSharp.Interpreter.Script;
                if (s == null) continue;
                string res;
                try
                {
                    MoonSharp.Interpreter.DynValue v = s.DoString("if Meta == nil or Meta.Operable == nil or fy_Altar == nil or Meta.Operable[fy_Altar] == nil then return \"no altar meta\" end local l = Meta.Operable[fy_Altar].Recipes local o = {} for i = 1, #l do local r = Meta.Recipe and Meta.Recipe[l[i]] o[#o + 1] = tostring(l[i]) .. \"=\" .. ((r and r.Name) and r.Name() or \"?\") end local r = __BtcWormRecipe and __BtcWormRecipe() local ok, inf = false, nil if r ~= nil then ok, inf = pcall(Crafting.GetRecipeInfo, r) end local same = false for i = 1, #l do if l[i] == r then same = true end end return table.concat(o, \", \") .. \" | worm=\" .. tostring(r) .. \" listHasFresh=\" .. tostring(same) .. \" getInfo=\" .. tostring(ok) .. \" \" .. tostring(inf) .. \" meta=\" .. tostring(r ~= nil and Meta.Recipe[r] ~= nil)");
                    res = v.ToPrintString();
                }
                catch (Exception e) { res = "err " + e.Message; }
                BtcDevLab.Lab("wormward lua script#" + (n++) + ": " + res);
            }
            if (n == 0) BtcDevLab.Lab("wormward lua: no script");
        }

        private static void Go(int i)
        {
            GameObject go = Ward(i);
            List<GameObject> live = BtcPhantom.DevLive();
            if (go == null || live.Count == 0) { BtcDevLab.Lab("wormward go: ward " + i + " / phantoms " + live.Count); return; }
            GameObject ph = live[0];
            Vector3 dst = Architecte.SurfaceRaycast(go.transform.position + new Vector3(3f, 10f, 0f), 30, true);
            Labor l = ph.GetComponent<Labor>();
            Order.Move.RunToward.Send(l, new SpaceLocation(dst, true), Labor.DecisionSource.User);
            BtcDevLab.Lab("wormward go #" + i + " phantom=" + ph.GetInstanceID() + " from=" + ph.transform.position + " to=" + dst + " dist=" + Vector3.Distance(ph.transform.position, dst).ToString("F1"));
        }
    }
}
#endif
