#if DEV
using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle.Dev
{
    /// <summary>Lab for BtcWorkHighlight (scenario whl-mp): "whl dig" = host orders the first worker of every faction to dig the
    /// ground voxel next to it (vanilla GoDig = DigGoal with the yellow highlight); "whl dump tag" logs the counters.</summary>
    internal static class BtcDevWorkHl
    {
        internal static void Cmd(string[] p)
        {
            string a = p.Length > 1 ? p[1] : "dump";
            try
            {
                if (a == "dig") Dig();
                else if (a == "move") Move(p.Length > 2 ? p[2] : "1");
                else if (a == "probe") Probe(p.Length > 2 ? p[2] : "x");
                else BtcLog.Info("DevWhl", "dump " + (p.Length > 2 ? p[2] : "x") + " shown=" + BtcWorkHighlight.LocalShown + " hidden=" + BtcWorkHighlight.LocalHidden
                    + " sent=" + BtcWorkHighlight.SentAdds + " got=" + BtcWorkHighlight.GotAdds + " bornesSent=" + BtcWorkHighlight.SentBornes + " bornesGot=" + BtcWorkHighlight.GotBornes + " server=" + NetworkServer.active
                    + " local=" + (User.LocalUser != null && User.LocalUser.faction != null ? User.LocalUser.faction.name : "?"));
            }
            catch (Exception e) { BtcLog.Warn("DevWhl", a + ": " + e.Message); }
        }

        /// <summary>Host: own workers get a move order through the same Command body a click uses (type 0/1/2 = Primary/Secondary/Ternary).</summary>
        private static void Move(string type)
        {
            User u = User.LocalUser;
            if (u == null || u.faction == null) { BtcLog.Warn("DevWhl", "move: no local user"); return; }
            Brix.Game.Network.UNetContextualCmd cmd = u.GetComponent<Brix.Game.Network.UNetContextualCmd>();
            List<GameObject> own = new List<GameObject>();
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>()) if (l.faction == u.faction && own.Count < 3) own.Add(l.gameObject);
            if (cmd == null || own.Count == 0) { BtcLog.Warn("DevWhl", "move: cmd=" + (cmd != null) + " units=" + own.Count); return; }
            Vector3 b = own[0].transform.position;
            RaycastHit hit;
            if (!Physics.Raycast(new Vector3(b.x + 8f, b.y + 10f, b.z), Vector3.down, out hit, 40f, 132096, QueryTriggerInteraction.Ignore)) { BtcLog.Warn("DevWhl", "move: no ground"); return; }
            int t; int.TryParse(type, out t);
            if (NetworkServer.active) cmd.CmdBatchedContextualActionVector(own.ToArray(), hit.point, (Brix.Input.ContextualOrders.ActionType)t);
            else HarmonyLib.Traverse.Create(cmd).Method("CallCmdBatchedContextualActionVector", new object[] { own.ToArray(), hit.point, (Brix.Input.ContextualOrders.ActionType)t }).GetValue(); // weaver stub sends the Command
            BtcLog.Info("DevWhl", "move " + own.Count + " units to " + hit.point + " type " + t);
        }

        /// <summary>Logs every scene object whose sprite / texture / material name contains "move" (the vanilla move marker hunt).</summary>
        private static void Probe(string tag)
        {
            int n = 0;
            foreach (Renderer r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (r == null || r.gameObject.scene.name == null) continue;
                string s = "";
                SpriteRenderer sr = r as SpriteRenderer;
                if (sr != null && sr.sprite != null) s += " sprite=" + sr.sprite.name;
                Material m = r.sharedMaterial;
                if (m != null) { s += " mat=" + m.name; if (m.mainTexture != null) s += " tex=" + m.mainTexture.name; }
                if (s.IndexOf("move", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (n++ < 30) BtcLog.Info("DevWhl", "probe " + tag + " R " + Path(r.transform) + " act=" + r.gameObject.activeInHierarchy + " en=" + r.enabled + " pos=" + r.transform.position + s + " comps=" + Comps(r.gameObject));
            }
            foreach (UnityEngine.UI.Image im in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Image>())
            {
                if (im == null || im.gameObject.scene.name == null || im.sprite == null || im.sprite.name.IndexOf("move", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (n++ < 30) BtcLog.Info("DevWhl", "probe " + tag + " I " + Path(im.transform) + " act=" + im.gameObject.activeInHierarchy + " pos=" + im.transform.position + " sprite=" + im.sprite.name + " comps=" + Comps(im.gameObject));
            }
            BtcLog.Info("DevWhl", "probe " + tag + " found " + n);
        }

        private static string Path(Transform t)
        {
            string s = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        private static string Comps(GameObject g)
        {
            string s = "";
            for (Transform t = g.transform; t != null; t = t.parent)
                foreach (Component c in t.GetComponents<Component>())
                    if (c != null && !(c is Transform)) s += c.GetType().Name + ",";
            return s.Length > 300 ? s.Substring(0, 300) : s;
        }

        private static void Dig()
        {
            if (!NetworkServer.active) { BtcLog.Warn("DevWhl", "dig: host only"); return; }
            HashSet<Faction> done = new HashSet<Faction>();
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
            {
                if (l == null || l.faction == null || l.faction.IsSpectator || !done.Add(l.faction)) continue;
                Vector3 b = l.transform.position;
                RaycastHit hit;
                if (!Physics.Raycast(new Vector3(b.x + 2f, b.y + 10f, b.z), Vector3.down, out hit, 40f, 132096, QueryTriggerInteraction.Ignore)) continue;
                Vector3 q = hit.point - Vector3.up * 0.05f;
                XYZ v = new XYZ(Mathf.RoundToInt(q.x), Mathf.RoundToInt(q.y), Mathf.RoundToInt(q.z));
                Order.ForLabor.GoDig.Send(l, Location.Of(v));
                BtcLog.Info("DevWhl", "dig order " + l.faction.name + " at " + v);
            }
        }
    }
}
#endif
