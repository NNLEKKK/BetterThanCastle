#if DEV
using System;
using System.Collections;
using System.IO;
using System.Text;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Semantique;
using Brix.Legacy;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV rack filter lab -> BTClogs/rack-filter.txt. Commands: "rackinfo" (components, overhead driver, pie menu build),
    /// "rackset Kit|Any" (same path as the pie button: Instructions.Execute), "rackfill Knight|Archer", "rackcheck label", "rackclear" (wipe).</summary>
    internal sealed class DeathLog : MonoBehaviour
    {
        private void OnDisable() { BtcLog.Info("RackLab", "DISABLE " + gameObject.name + " t=" + Time.realtimeSinceStartup + "\n" + new System.Diagnostics.StackTrace(true)); }
        private void OnDestroy() { BtcLog.Info("RackLab", "DESTROY " + gameObject.name + "\n" + new System.Diagnostics.StackTrace(true)); }
    }

    internal static class BtcDevRack
    {
        private static void L(string s)
        {
            try { File.AppendAllText(System.IO.Path.Combine(BtcDevPaths.LogDir, "rack-filter.txt"), s + "\n"); } catch { }
            BtcLog.Info("RackLab", s);
        }

        internal static ToolRack Rack()
        {
            Faction f = User.LocalUser != null ? User.LocalUser.faction : null;
            ToolRack best = null;
            foreach (ToolRack r in UnityEngine.Object.FindObjectsOfType<ToolRack>())
                if (r != null && (best == null || r.faction == f)) best = r;
            return best;
        }

        private static readonly System.Collections.Generic.List<GameObject> Spawned = new System.Collections.Generic.List<GameObject>();

        internal static void Alive()
        {
            foreach (GameObject g in Spawned) L("alive? " + (g != null ? g.name + " active=" + g.activeInHierarchy + " pos=" + g.transform.position + " parent=" + (g.transform.parent != null ? g.transform.parent.name : "-") : "destroyed"));
        }

        private static bool _keepPatched;
        private static bool KeepPrefix() { return false; }

        internal static void Spawn(string name, float dy, int dx, bool other)
        {
            if (!_keepPatched)
            {
                _keepPatched = true;
                try
                {
                    HarmonyLib.Harmony h = new HarmonyLib.Harmony("btc.dev.rackkeep");
                    h.Patch(HarmonyLib.AccessTools.Method(typeof(DefaultDOValidator), "SetUnstable"), new HarmonyLib.HarmonyMethod(typeof(BtcDevRack).GetMethod("KeepPrefix", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)));
                    L("DEV: DefaultDOValidator.SetUnstable disabled (spawned lab objects stay)");
                }
                catch (Exception e) { L("keep patch failed " + e.Message); }
            }
            try
            {
                User u = User.LocalUser;
                Labor[] ls = UnityEngine.Object.FindObjectsOfType<Labor>();
                Vector3 pos = Vector3.zero;
                for (int i = 0; i < ls.Length; i++) if (ls[i] != null && u.faction.IsSame(ls[i].gameObject)) { pos = ls[i].transform.position; break; }
                XYZ p = Brix.Engine.Architecte.PigerUneCase(pos, 10f, 30);
                int ix = Mathf.RoundToInt(pos.x) + dx, iz = Mathf.RoundToInt(pos.z);
                int top = VoxelEngine.GetColumnTop(ix, iz);
                L("labor pos " + pos + " columnTop(" + ix + "," + iz + ")=" + top);
                Vector3 at = new Vector3(ix, top + dy, iz);
                Brix.External.Factories.Factory.AssetKey key = new Brix.External.Factories.Factory.AssetKey(name.StartsWith("bp") ? "Blueprints" : "ObjetsDynamiques", name.StartsWith("bp") ? name.Substring(2) : name);
                L("peek " + name + " = " + (Brix.External.Factories.Factory.Peek(key) != null));
                Faction fac = u.faction;
                if (other)
                {
                    foreach (User uu in UnityEngine.Object.FindObjectsOfType<User>()) if (uu != u && uu.faction != null) { fac = uu.faction; break; }
                    L("spawning for other faction " + (fac != u.faction));
                }
                Brix.Lua.LuaDriver d = Brix.Lua.LuaFactory.InstantiateFromCLR(key, fac, at, Quaternion.identity);
                GameObject go = d != null ? d.gameObject : null;
                Blueprint bp = go != null ? go.GetComponent<Blueprint>() : null;
                if (bp != null)
                {
                    L("blueprint state=" + bp.State + " placed=" + bp.isPlaced);
                    go = bp.MorphToObject();
                    L("morphed -> " + (go != null ? go.name + " toolrack=" + (go.GetComponent<ToolRack>() != null) : "null"));
                }
                if (go != null) { Spawned.Add(go); go.AddComponent<DeathLog>(); }
                L("spawned " + name + " at " + at + " driver=" + (d != null) + " go=" + (go != null ? go.name + " activeSelf=" + go.activeSelf + " inHier=" + go.activeInHierarchy + " scene=" + go.scene.name : "-") + " toolrack=" + (go != null && go.GetComponent<ToolRack>() != null));
            }
            catch (Exception e) { L("SPAWN EXC " + e); }
        }

        internal static void Info()
        {
            ToolRack r = Rack();
            if (r == null)
            {
                L("NO RACK");
                foreach (Recepteur rc0 in UnityEngine.Object.FindObjectsOfType<Recepteur>()) { L("  recepteur " + rc0.gameObject.name + " active=" + rc0.gameObject.activeInHierarchy + " tr=" + (rc0.GetComponent<ToolRack>() != null));
                    if (rc0.gameObject.name.Contains("ack")) { StringBuilder s0 = new StringBuilder("    comps:"); foreach (MonoBehaviour mb in rc0.GetComponents<MonoBehaviour>()) if (mb != null) s0.Append(" ").Append(mb.GetType().Name); L(s0.ToString()); } }
                foreach (ToolRack tr0 in Resources.FindObjectsOfTypeAll<ToolRack>()) L("  all ToolRack " + tr0.gameObject.name + " scene=" + tr0.gameObject.scene.name + " active=" + tr0.gameObject.activeInHierarchy);
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("rack key=").Append(BtcRackFilter.KeyOf(r)).Append(" gate=").Append(BtcNetworkGate.Enabled).Append(" own=").Append(BtcRackFilter.IsOwn(r));
            sb.Append(" server=").Append(UnityEngine.Networking.NetworkServer.active);
            L(sb.ToString());
            sb.Length = 0; sb.Append("components:");
            foreach (MonoBehaviour mb in r.GetComponents<MonoBehaviour>()) if (mb != null) sb.Append(' ').Append(mb.GetType().Name);
            L(sb.ToString());
            sb.Length = 0; sb.Append("children components:");
            foreach (MonoBehaviour mb in r.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null && mb.gameObject != r.gameObject) sb.Append(' ').Append(mb.GetType().Name);
            L(sb.ToString());
            RecepteurOverheadDisplayDriver d = r.GetComponentInChildren<RecepteurOverheadDisplayDriver>(true);
            OverheadDisplayDriver od = r.GetComponentInChildren<OverheadDisplayDriver>(true);
            L("overhead: RecepteurOverheadDisplayDriver=" + (d != null) + " anyOverheadDriver=" + (od != null ? od.GetType().Name : "none"));
            PieDump(r);
        }

        private static void PieDump(ToolRack r)
        {
            try
            {
                var menus = (System.Collections.IDictionary)HarmonyLib.AccessTools.Field(typeof(PieContenu), "menus").GetValue(null);
                var bts = (System.Collections.IDictionary)HarmonyLib.AccessTools.Field(typeof(PieContenu), "boutons").GetValue(null);
                L("pie buttons defined: Hub=" + bts.Contains("RackKitHub") + " Any=" + bts.Contains("RackKitAny") + " Knight=" + bts.Contains("RackKitKnight") + " Artificer=" + bts.Contains("RackKitArtificer"));
                object mn = menus["BuilderToolRack"];
                IEnumerable ins = mn.GetType().GetField("instructions").GetValue(mn) as IEnumerable;
                foreach (object o in ins)
                {
                    System.Type it = o.GetType();
                    string[] a = it.GetField("args").GetValue(o) as string[];
                    L("  ins line=" + it.GetField("ligne").GetValue(o) + " depth=" + it.GetField("depth").GetValue(o) + " " + it.GetField("entete").GetValue(o) + " [" + (a != null ? string.Join(",", a) : "") + "] next=" + it.GetField("nextAt").GetValue(o) + " end=" + it.GetField("endAt").GetValue(o));
                }
                RadialLayoutGroup.pieMenuTarget = r.gameObject;
                var list = PieContenu.GetGeneratedButtonData("BuilderToolRack", Vector3.zero);
                L("pie BuilderToolRack: " + (list.Count - 1) + " slots, title=" + (list[0]["titre"] ?? "-"));
                for (int i = 1; i < list.Count; i++) DumpButton(list[i] as Hashtable, 1, i);
            }
            catch (Exception e) { L("PIE EXC " + e); }
        }

        private static void DumpButton(Hashtable h, int depth, int idx)
        {
            if (h == null) return;
            string pad = new string(' ', depth * 2);
            object ti = h["titre"]; object sp = h["sprite"]; object ic = h["icone"];
            bool chk = true;
            try { Func<bool> c = h["check"] as Func<bool>; if (c != null) chk = c(); } catch (Exception e) { L(pad + "check EXC " + e.Message); }
            L(pad + "[" + idx + "] titre=" + (ti ?? "-") + " sprite=" + (sp != null) + " icone=" + (ic != null) + " check=" + chk + " accept=" + (h["accept"] != null) + " test=" + (h["testTerme"] ?? "-"));
            IList sub = h["submenu"] as IList;
            if (sub != null) for (int i = 1; i < sub.Count; i++) DumpButton(sub[i] as Hashtable, depth + 1, i);
        }

        /// <summary>Host only: bypass the ownership check (lab).</summary>
        internal static void HostSet(string kit)
        {
            ToolRack r = Rack();
            if (r == null) { L("NO RACK"); return; }
            int k = kit == "Any" ? -1 : System.Array.IndexOf(BtcRackFilter.KitNames, kit);
            BtcRackFilter.ApplyLocal(r, k);
            BtcRackFilterNet.Broadcast();
            L("hostset " + kit + " -> filter=" + BtcRackFilter.Get(r));
        }

        internal static void Set(string kit)
        {
            ToolRack r = Rack();
            if (r == null) { L("NO RACK"); return; }
            Instructions.objet = r.gameObject;
            Instructions.sujets = new MonoBehaviour[0];
            string ev = "Event_Rack_Filter_" + kit;
            L("execute " + ev);
            Instructions.Execute(ev);
        }

        internal static void Fill(string kit)
        {
            ToolRack r = Rack();
            if (r == null) return;
            int before = Count(r);
            if (kit == "Knight") r.FillWithKnightKit(); else r.FillWithArcherKit();
            L("fill " + kit + ": items " + before + " -> " + Count(r));
        }

        private static int Count(ToolRack r)
        {
            int n = 0;
            for (int k = 0; k < BtcRackFilter.KitNames.Length; k++)
                foreach (Ressource a in BtcRackFilter.ListFor(k)) n += r.recepteur.ContentDescription.Value(a);
            return n;
        }

        internal static void Clear()
        {
            ToolRack r = Rack();
            if (r == null) return;
            r.recepteur.Wipe();
            r.RefreshExclusivity();
            L("wiped, items=" + Count(r));
        }

        internal static void Check(string label)
        {
            ToolRack r = Rack();
            if (r == null) { L("NO RACK"); return; }
            Recepteur rc = r.recepteur;
            StringBuilder sb = new StringBuilder();
            sb.Append("CHECK ").Append(label).Append(": filter=").Append(BtcRackFilter.Get(r)).Append(" items=").Append(Count(r));
            sb.Append(" isFor[K,A,H,Ab,Al,Ar]=").Append(r.IsForKnight ? 1 : 0).Append(r.IsForArcher ? 1 : 0).Append(r.IsForHalberdier ? 1 : 0).Append(r.IsForArbalist ? 1 : 0).Append(r.IsForAlchemist ? 1 : 0).Append(r.IsForArtificer ? 1 : 0);
            sb.Append(" hasExcl=").Append(rc.HasExclusions());
            sb.Append(" excl/cap first-of-kit:");
            for (int k = 0; k < BtcRackFilter.KitNames.Length; k++)
            {
                Ressource a = BtcRackFilter.ListFor(k)[0];
                sb.Append(' ').Append(BtcRackFilter.KitNames[k]).Append('=').Append(rc.IsExcluding(a) ? "X" : "ok").Append('/').Append(rc.CurrentCapacity.Value(a));
            }
            sb.Append(" hasRoomForKit=").Append(r.HasRoomForKit());
            int fk = BtcRackFilter.Get(r);
            if (fk >= 0) { UnityEngine.Sprite sp = BtcRackFilter.KitSprite(fk); sb.Append(" kitSprite=").Append(sp != null ? sp.name : "NULL"); }
            RecepteurOverheadDisplayDriver od = r.GetComponentInChildren<RecepteurOverheadDisplayDriver>(true);
            if (od != null && od.overheadDisplay != null) sb.Append(" bigIconSpawned=").Append(od.overheadDisplay.BigIcon.IsSpawned);
            L(sb.ToString());
        }
    }
}
#endif
