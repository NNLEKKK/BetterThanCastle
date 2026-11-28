#if DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Brix.Assets;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Compounders;
using Brix.Game.Components.Validators.Graphs;
using Brix.Game.Network;
using Brix.Legacy;
using Brix.Game.Semantique;
using Path = System.IO.Path;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV lab of advanced blueprints (BtcBpPreset) -> BTClogs/bp-preset.txt. Commands:
    /// "bp spawn Asset dx" real blueprint through UNetBlueprint (works on clients), "bp pie Asset" builds MENU Game over it and dumps slots,
    /// "bp ev Asset Event" pie event on it, "bp build Asset" (host) MorphToObject, "bp check label" own buildings + table,
    /// "bp compound dx" own live blueprints -> graph -> compound placed dx further, "bp file" compound file round trip in BTClogs.</summary>
    internal static class BtcDevBpPreset
    {
        private static void L(string s)
        {
            try { File.AppendAllText(Path.Combine(BtcDevPaths.LogDir, "bp-preset.txt"), s + "\n"); } catch { }
            BtcLog.Info("BpLab", s);
        }

        private static Vector3 Base()
        {
            User u = User.LocalUser;
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
                if (l != null && u != null && u.faction.IsSame(l.gameObject)) return l.transform.position;
            return Vector3.zero;
        }

        internal static void Run(string[] p)
        {
            try
            {
                string op = p.Length > 1 ? p[1] : "check";
                string a = p.Length > 2 ? p[2] : "Pallet";
                switch (op)
                {
                    case "spawn": Spawn(a, p.Length > 3 ? int.Parse(p[3]) : 3); break;
                    case "pie": Pie(a); break;
                    case "ev": Ev(a, p.Length > 3 ? p[3] : "Event_BtcBp_Clear"); break;
                    case "build": Build(a); break;
                    case "check": Check(a); break;
                    case "compound": Compound(p.Length > 2 ? int.Parse(p[2]) : 12); break;
                    case "file": FileTrip(); break;
                    case "anchor": { Blueprint ab = Find(a); BtcBpPreset.DevAnchor(ab); L("anchor set on " + (ab != null ? a + " at " + ab.transform.position : "NONE")); break; }
                    default: L("unknown op " + op); break;
                }
            }
            catch (Exception e) { L("EXC " + e); }
        }

        private static bool _keep;
        private static bool KeepPrefix() { return false; }

        private static void Spawn(string asset, int dx)
        {
            if (!_keep)
            {
                _keep = true;   // lab objects may float one voxel up: keep them (like BtcDevRack)
                new HarmonyLib.Harmony("btc.dev.bpkeep").Patch(HarmonyLib.AccessTools.Method(typeof(DefaultDOValidator), "SetUnstable"),
                    new HarmonyLib.HarmonyMethod(typeof(BtcDevBpPreset).GetMethod("KeepPrefix", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)));
            }
            Vector3 b = Base();
            int ix = Mathf.RoundToInt(b.x) + dx, iz = Mathf.RoundToInt(b.z) + 4;
            int top = VoxelEngine.GetColumnTop(ix, iz);
            Vector3 at = new Vector3(ix, top + 1, iz);
            User.LocalUser.GetComponent<UNetBlueprint>().CallCmdSpawnPlacedBlueprint("Blueprints", asset, at, Quaternion.identity, false, null, false);
            L("spawn " + asset + " at " + at);
        }

        private static Blueprint Find(string asset)
        {
            User u = User.LocalUser;
            Blueprint best = null;
            foreach (Blueprint b in UnityEngine.Object.FindObjectsOfType<Blueprint>())
                if (b != null && b.AssetKey != null && b.AssetKey.Name == asset && u != null && ReferenceEquals(b.faction, u.faction) && BtcBpPreset.KeyOf(b) != 0u) best = b;
            return best;
        }

        private static List<Hashtable> Gen(Blueprint b)
        {
            RadialLayoutGroup.pieMenuTarget = b.gameObject;
            Instructions.sujets = new MonoBehaviour[0];
            return PieContenu.GetGeneratedButtonData("Game", Vector3.zero);
        }

        private static void Pie(string asset)
        {
            Blueprint b = Find(asset);
            if (b == null) { L("pie: no own blueprint " + asset); return; }
            List<Hashtable> list = Gen(b);
            L("pie over " + asset + " " + BtcBpPreset.KeyOf(b) + ": " + (list.Count - 1) + " slots");
            for (int i = 1; i < list.Count; i++) Dump(list[i], 1, i);
        }

        private static void Dump(Hashtable h, int depth, int idx)
        {
            if (h == null) { L(new string(' ', depth * 2) + "[" + idx + "] hole"); return; }
            bool chk = true;
            try { Func<bool> c = h["check"] as Func<bool>; if (c != null) chk = c(); } catch (Exception e) { L("check EXC " + e.Message); }
            L(new string(' ', depth * 2) + "[" + idx + "] " + (h["titre"] ?? "-") + " check=" + chk + " icon=" + (h["icone"] != null || h["sprite"] != null));
            IList sub = h["submenu"] as IList;
            if (sub != null) for (int i = 1; i < sub.Count; i++) Dump(sub[i] as Hashtable, depth + 1, i);
        }

        private static void Ev(string asset, string ev)
        {
            Blueprint b = Find(asset);
            if (b == null) { L("ev: no own blueprint " + asset); return; }
            Gen(b);   // sets the pie target like opening the pie
            Instructions.objet = b.gameObject;
            Instructions.Execute(ev);
            L("ev " + ev + " on " + asset + " -> '" + BtcBpPreset.Get(b) + "'");
        }

        private static void Build(string asset)
        {
            User u = User.LocalUser;
            int n = 0;
            foreach (Blueprint b in UnityEngine.Object.FindObjectsOfType<Blueprint>())
            {
                if (b == null || b.AssetKey == null || b.AssetKey.Name != asset || u == null || !ReferenceEquals(b.faction, u.faction) || BtcBpPreset.KeyOf(b) == 0u) continue;
                GameObject go = b.MorphToObject();
                L("build " + asset + " at " + b.transform.position + " -> " + (go != null ? go.name : "null"));
                n++;
            }
            if (n == 0) L("build: no own blueprint " + asset);
        }

        private static void Check(string label)
        {
            User u = User.LocalUser;
            StringBuilder sb = new StringBuilder("CHECK " + label + " " + BtcBpPreset.DevState());
            foreach (Blueprint b in UnityEngine.Object.FindObjectsOfType<Blueprint>())
                if (b != null && u != null && ReferenceEquals(b.faction, u.faction) && BtcBpPreset.KeyOf(b) != 0u)
                    sb.Append("\n  bp ").Append(b.AssetKey.Name).Append(' ').Append(b.transform.position).Append(" = '").Append(BtcBpPreset.Get(b)).Append('\'');
            foreach (Recepteur r in UnityEngine.Object.FindObjectsOfType<Recepteur>())
            {
                if (r == null || u == null || !u.faction.IsSame(r.gameObject) || r.GetComponent<Blueprint>() != null) continue;
                if (r.GetComponent<ToolRack>() != null)
                    sb.Append("\n  rack ").Append(r.transform.position).Append(" kit=").Append(BtcRackFilter.Get(r.GetComponent<ToolRack>()));
                else if (r.gameObject.name.Contains("Pallet") || r.gameObject.name.Contains("alette"))
                    sb.Append("\n  stock ").Append(r.gameObject.name).Append(' ').Append(r.transform.position).Append(" excludeAll=").Append(r.ExcludeAll)
                      .Append(" only=").Append(r.ExclusiveRessource != null ? r.ExclusiveRessource.GetType().Name : "-");
            }
            foreach (CraftingStation st in UnityEngine.Object.FindObjectsOfType<CraftingStation>())
            {
                if (st == null || u == null || !ReferenceEquals(st.faction, u.faction)) continue;
                sb.Append("\n  station ").Append(st.GetType().Name).Append(' ').Append(st.transform.position).Append(" loop=").Append(st.Looping)
                  .Append(" limit=").Append(BtcWorkbench.LimitOn(st)).Append(" help=").Append(BtcWorkbench.HelpOn(st)).Append(" queue=");
                List<RecipeInfo> q = st.QueuedRecipes;
                if (q != null) for (int i = 0; i < q.Count; i++) sb.Append(i > 0 ? "+" : "").Append(q[i] != null && q[i].Creates != null ? q[i].Creates.Name : "?");
            }
            L(sb.ToString());
        }

        private static void Compound(int dx)
        {
            User u = User.LocalUser;
            List<GameObject> gos = new List<GameObject>();
            foreach (Blueprint b in UnityEngine.Object.FindObjectsOfType<Blueprint>())
                if (b != null && u != null && ReferenceEquals(b.faction, u.faction) && BtcBpPreset.KeyOf(b) != 0u) gos.Add(b.gameObject);
            BlueprintGraph g = BlueprintCompounder.GenerateBlueprintGraph(gos);
            Asset_Blueprints shell = Asset_Blueprints.CreateShell();
            shell.customName = "BtcLab";
            shell.Graph = g;
            BlueprintCompoundComponent bcc = BlueprintCompounder.LoadCompoundBlueprints(shell, false);
            Vector3 target = g.OriginalSourcePosition + new Vector3(0f, 0f, dx);
            bcc.gameObject.SetActive(true);
            bcc.MoveAt(target);
            L("compound of " + gos.Count + " blueprints, nodes " + g.Nodes.Count + ", cursor " + target + ", origin " + g.OriginalSourcePosition + ", anchor: " + BtcBpPreset.DevAnchorState(bcc));
            bcc.gameObject.SetActive(true);   // the placement picker activates it in the real game
            bcc.StartPlaceBlueprints();
        }

        private static void FileTrip()
        {
            User u = User.LocalUser;
            List<GameObject> gos = new List<GameObject>();
            foreach (Blueprint b in UnityEngine.Object.FindObjectsOfType<Blueprint>())
                if (b != null && u != null && ReferenceEquals(b.faction, u.faction) && BtcBpPreset.KeyOf(b) != 0u) gos.Add(b.gameObject);
            Asset_Blueprints shell = Asset_Blueprints.CreateShell();
            shell.customName = "BtcLabFile";
            shell.Graph = BlueprintCompounder.GenerateBlueprintGraph(gos);
            string dir = Path.Combine(BtcDevPaths.LogDir, "bp-compound");
            Directory.CreateDirectory(dir);
            shell.path = dir;
            BtcBpPreset.OnCompoundSaved(shell);
            string f = Path.Combine(dir, BtcBpPreset.CompoundFile);
            L("file " + (File.Exists(f) ? File.ReadAllText(f).Replace('\n', '|') : "MISSING"));
            Asset_Blueprints shell2 = Asset_Blueprints.CreateShell();
            shell2.customName = "BtcLabFile2";
            shell2.Graph = BlueprintCompounder.GenerateBlueprintGraph(new List<GameObject>());   // a graph object unknown to the table
            shell2.path = dir;
            BtcBpPreset.OnCompoundLoad(shell2);
            L("file reload: " + BtcBpPreset.DevState());
        }
    }
}
#endif
