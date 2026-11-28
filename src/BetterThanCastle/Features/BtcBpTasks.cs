using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Components.Validators.Graphs;
using Brix.Game.Network;
using UnityEngine;

namespace BetterThanCastle
{
    /// Build tasks inside compound blueprints: every mark remembers which build task it was in and each task its priority. Placing a
    /// compound that had two or more tasks puts each group into its own new build task (vanilla UNetBlueprint.CmdSendBlueprintsToNewProject)
    /// and steps its priority to the saved one (vanilla UNetGroupe raise/lower). One task: vanilla (the marks join the current task).
    /// Vanilla commands only, so it works with BetterConnection OFF too. File lines (btc-presets.txt): "@task g bias", "@node key g".
    internal static partial class BtcBpPreset
    {
        private static readonly Dictionary<BlueprintGraph, Dictionary<string, int>> TaskOfNode = new Dictionary<BlueprintGraph, Dictionary<string, int>>();
        private static readonly Dictionary<BlueprintGraph, List<int>> TaskPrio = new Dictionary<BlueprintGraph, List<int>>();
        private static readonly Dictionary<int, int> CopyTask = new Dictionary<int, int>();    // preview copy instance id -> task group

        private sealed class TPend { internal Vector3 Pos; internal string Name; internal int G; }

        private sealed class Place
        {
            internal readonly List<TPend> Wait = new List<TPend>();
            internal readonly Dictionary<int, List<GameObject>> Got = new Dictionary<int, List<GameObject>>();
            internal List<int> Prio;
            internal float SendAt, Until, NextStep;
            internal bool Sent;
            internal int Steps;
        }

        private static readonly List<Place> Places = new List<Place>();

        private static bool TasksOn { get { return BtcHotkeys.GetBool("bp_presets", true); } }

        // ------------------------------------------------------------------ recording
        private static void OnGraphTasks(BlueprintGraph g)
        {
            TaskOfNode.Remove(g);
            TaskPrio.Remove(g);
            if (!TasksOn) return;
            Dictionary<Project, int> ids = new Dictionary<Project, int>();
            Dictionary<string, int> m = new Dictionary<string, int>();
            List<int> prio = new List<int>();
            List<int> srcPrio = null;
            foreach (ValidationNode n in g.Nodes)
            {
                if (n.Target == null) continue;
                Blueprint b = n.Target.GetComponent<Blueprint>();
                int grp = -1;
                if (b != null && KeyOf(b) != 0u)
                {
                    Project p = b.Project;
                    if (p == null) continue;
                    if (!ids.TryGetValue(p, out grp)) { grp = ids.Count; ids[p] = grp; prio.Add((int)p.GetPriority()); }
                }
                else if (CopyTask.TryGetValue(n.Target.GetInstanceID(), out grp))
                {
                    if (srcPrio == null && _placingGraph != null) TaskPrio.TryGetValue(_placingGraph, out srcPrio);   // graph rebuilt from preview copies
                    while (prio.Count <= grp) prio.Add(srcPrio != null && prio.Count < srcPrio.Count ? srcPrio[prio.Count] : (int)ProjectPriority.Bias.Medium);
                }
                else continue;
                m[NodeKey(n)] = grp;
            }
            if (m.Count == 0) return;
            TaskOfNode[g] = m;
            TaskPrio[g] = prio;
            BtcLog.Info("BpPreset", "compound graph keeps " + prio.Count + " build task(s)");
        }

        private static void SpawnedUnderTasks(BlueprintGraph g)
        {
            Dictionary<string, int> m;
            TaskOfNode.TryGetValue(g, out m);
            foreach (ValidationNode n in g.Nodes)
            {
                if (n.Target == null) continue;
                int grp;
                if (m != null && m.TryGetValue(NodeKey(n), out grp)) CopyTask[n.Target.GetInstanceID()] = grp;
                else CopyTask.Remove(n.Target.GetInstanceID());
            }
        }

        private static void AppendTasks(BlueprintGraph g, StringBuilder sb)
        {
            Dictionary<string, int> m;
            List<int> prio;
            if (g == null || !TaskOfNode.TryGetValue(g, out m) || !TaskPrio.TryGetValue(g, out prio)) return;
            for (int i = 0; i < prio.Count; i++) sb.Append("@task ").Append(i).Append(' ').Append(prio[i]).Append('\n');
            foreach (KeyValuePair<string, int> kv in m) sb.Append("@node ").Append(kv.Key).Append(' ').Append(kv.Value).Append('\n');
        }

        private static bool ReadTasks(BlueprintGraph g, string line)
        {
            if (line.StartsWith("@task "))
            {
                string[] t = line.Substring(6).Trim().Split(' ');
                int i, b;
                if (t.Length == 2 && int.TryParse(t[0], out i) && int.TryParse(t[1], out b) && i >= 0 && i < 256)
                {
                    List<int> prio;
                    if (!TaskPrio.TryGetValue(g, out prio)) { prio = new List<int>(); TaskPrio[g] = prio; }
                    while (prio.Count <= i) prio.Add((int)ProjectPriority.Bias.Medium);
                    prio[i] = b;
                }
                return true;
            }
            if (line.StartsWith("@node "))
            {
                string[] t = line.Substring(6).Trim().Split(' ');
                int grp;
                if (t.Length == 2 && int.TryParse(t[1], out grp))
                {
                    Dictionary<string, int> m;
                    if (!TaskOfNode.TryGetValue(g, out m)) { m = new Dictionary<string, int>(); TaskOfNode[g] = m; }
                    m[t[0]] = grp;
                }
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ placing
        private static void OnStartPlaceTasks(BlueprintCompoundComponent bcc)
        {
            if (!TasksOn || bcc == null || bcc.Graph == null) return;
            Place pl = new Place();
            HashSet<int> groups = new HashSet<int>();
            foreach (ValidationNode node in bcc.Graph.Nodes)
            {
                int grp;
                if (node.Target == null || node.IsObstructed || !CopyTask.TryGetValue(node.Target.GetInstanceID(), out grp)) continue;
                pl.Wait.Add(new TPend { Pos = node.Target.transform.position, Name = node.Type != null ? node.Type.Name : null, G = grp });
                groups.Add(grp);
            }
            if (groups.Count < 2) return;
            List<int> prio = null;
            if (_placingGraph != null) TaskPrio.TryGetValue(_placingGraph, out prio);
            if (prio == null) TaskPrio.TryGetValue(bcc.Graph, out prio);
            pl.Prio = prio;
            float now = Time.unscaledTime;
            pl.SendAt = now + 6f;   // latest: send what arrived
            pl.Until = now + 30f;
            Places.Add(pl);
            BtcLog.Info("BpPreset", "placing a compound with " + groups.Count + " build tasks");
        }

        private static void MatchTask(Blueprint b)
        {
            if (Places.Count == 0 || b == null || User.LocalUser == null || !ReferenceEquals(b.faction, User.LocalUser.faction)) return;
            string name = NameOfAsset(b);
            Vector3 pos = b.transform.position;
            for (int k = 0; k < Places.Count; k++)
            {
                Place pl = Places[k];
                for (int i = 0; i < pl.Wait.Count; i++)
                {
                    TPend p = pl.Wait[i];
                    if (p.Name != name || (p.Pos - pos).sqrMagnitude > 0.04f) continue;
                    pl.Wait.RemoveAt(i);
                    List<GameObject> l;
                    if (!pl.Got.TryGetValue(p.G, out l)) { l = new List<GameObject>(); pl.Got[p.G] = l; }
                    l.Add(b.gameObject);
                    if (pl.Wait.Count == 0) pl.SendAt = Mathf.Min(pl.SendAt, Time.unscaledTime + 0.3f);
                    return;
                }
            }
        }

        /// per frame (local): split arrived marks into new build tasks, then step their priorities
        private static void TasksTick()
        {
            if (Places.Count == 0) return;
            float now = Time.unscaledTime;
            User u = User.LocalUser;
            for (int k = Places.Count - 1; k >= 0; k--)
            {
                Place pl = Places[k];
                try
                {
                    if (u == null || now > pl.Until) { Places.RemoveAt(k); continue; }
                    if (!pl.Sent)
                    {
                        if (now < pl.SendAt) continue;
                        UNetBlueprint ub = u.GetComponent<UNetBlueprint>();
                        foreach (KeyValuePair<int, List<GameObject>> kv in pl.Got)
                        {
                            kv.Value.RemoveAll(g => g == null);
                            if (kv.Value.Count > 0) ub.CallCmdSendBlueprintsToNewProject(kv.Value.ToArray());
                        }
                        pl.Sent = true;
                        pl.NextStep = now + 0.8f;
                        BtcLog.Info("BpPreset", "compound marks sent to " + pl.Got.Count + " new build task(s)");
                        continue;
                    }
                    if (now < pl.NextStep) continue;
                    pl.NextStep = now + 0.6f;
                    if (pl.Prio == null || ++pl.Steps > 40) { Places.RemoveAt(k); continue; }
                    bool all = true;
                    UNetGroupe ug = u.GetComponent<UNetGroupe>();
                    foreach (KeyValuePair<int, List<GameObject>> kv in pl.Got)
                    {
                        if (kv.Key >= pl.Prio.Count || kv.Value.Count == 0) continue;
                        Blueprint b = kv.Value[0] != null ? kv.Value[0].GetComponent<Blueprint>() : null;
                        Project p = b != null ? b.Project : null;
                        if (p == null) { all = false; continue; }
                        int cur = Rank((int)p.GetPriority()), want = Rank(pl.Prio[kv.Key]);
                        if (cur == want) continue;
                        all = false;
                        if (want > cur) ug.CallCmdRaisePriority(p.gameObject); else ug.CallCmdLowerPriority(p.gameObject);
                    }
                    if (all) { Places.RemoveAt(k); BtcLog.Info("BpPreset", "compound build task priorities set"); }
                }
                catch (Exception e) { Places.RemoveAt(k); BtcLog.Error("BpPreset", "tasks: " + e); }
            }
        }

        /// Locked < Low < Medium < High
        private static int Rank(int bias)
        {
            if (bias <= (int)ProjectPriority.Bias.Locked) return 0;
            if (bias < (int)ProjectPriority.Bias.Medium) return 1;
            if (bias < (int)ProjectPriority.Bias.High) return 2;
            return 3;
        }
    }
}
