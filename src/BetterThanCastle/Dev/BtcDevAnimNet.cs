#if DEV
using System;
using System.Collections.Generic;
using Brix.Game;
using Brix.Game.AI;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV lab for smooth unit animations (BtcAnimNet). Scenario commands:
    /// animnet observe 0|1  - client: 1 = measure only (vanilla animations), 0 = correct;
    /// animnet dump &lt;tag&gt;   - LAB line with the counters since the last reset, then reset;
    /// animnet meet         - host: every unit of the two biggest factions gets a move-attack order to the middle between them;
    /// animnet walk         - host: every unit of those factions runs to a random point 6-12 voxels away.
    /// </summary>
    internal static class BtcDevAnimNet
    {
        internal static bool Cmd(string[] p)
        {
            string sub = p.Length > 1 ? p[1] : "dump";
            try
            {
                switch (sub)
                {
                    case "observe": BtcAnimNet.ObserveOnly = p.Length > 2 && p[2] == "1"; BtcAnimNet.DevReset(); BtcDevLab.Lab("animnet observe=" + BtcAnimNet.ObserveOnly); break;
                    case "dump": BtcDevLab.Lab("animnet " + (p.Length > 2 ? p[2] : "-") + (BtcAnimNet.ObserveOnly ? " observe " : " fix ") + BtcAnimNet.DevNow()); BtcAnimNet.DevReset(); break;
                    case "meet": Meet(); break;
                    case "walk": Walk(); break;
                    case "spawn": Spawn(p.Length > 2 ? int.Parse(p[2]) : 8); break;
                    default: BtcDevLab.Lab("animnet: unknown " + sub); break;
                }
            }
            catch (Exception e) { BtcDevLab.Lab("animnet " + sub + " error: " + e); }
            return true;
        }

        private static void Sides(out List<Labor> a, out List<Labor> b)
        {
            Dictionary<Faction, List<Labor>> by = new Dictionary<Faction, List<Labor>>();
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
            {
                if (l == null || l.faction == null || !l.gameObject.activeInHierarchy) continue;
                List<Labor> list;
                if (!by.TryGetValue(l.faction, out list)) by[l.faction] = list = new List<Labor>();
                list.Add(l);
            }
            a = null; b = null;
            foreach (List<Labor> list in by.Values)
            {
                if (a == null || list.Count > a.Count) { b = a; a = list; }
                else if (b == null || list.Count > b.Count) b = list;
            }
            if (a == null) a = new List<Labor>();
            if (b == null) b = new List<Labor>();
        }

        private static Vector3 Center(List<Labor> l)
        {
            Vector3 s = Vector3.zero;
            for (int i = 0; i < l.Count; i++) s += l[i].transform.position;
            return l.Count > 0 ? s / l.Count : s;
        }

        private static void Meet()
        {
            List<Labor> a, b;
            Sides(out a, out b);
            if (a.Count == 0 || b.Count == 0) { BtcDevLab.Lab("animnet meet: need two sides, got " + a.Count + "/" + b.Count); return; }
            Vector3 mid = (Center(a) + Center(b)) * 0.5f;
            for (int i = 0; i < a.Count; i++) Order.Combat.MoveAttack.Send(a[i], new SpaceLocation(mid, true), Labor.DecisionSource.User);
            for (int i = 0; i < b.Count; i++) Order.Combat.MoveAttack.Send(b[i], new SpaceLocation(mid, true), Labor.DecisionSource.User);
            BtcDevLab.Lab("animnet meet sides=" + a.Count + "/" + b.Count + " mid=" + mid);
        }

        /// <summary>Host: n units for the host and n for the first other player, side by side near the host's units (both see them).</summary>
        private static void Spawn(int n)
        {
            Faction mine = User.LocalUser != null ? User.LocalUser.faction : null;
            Faction other = null;
            foreach (User u in UnityEngine.Object.FindObjectsOfType<User>())
                if (u != null && u.faction != null && u.faction != mine) { other = u.faction; break; }
            if (mine == null || other == null) { BtcDevLab.Lab("animnet spawn: factions " + (mine != null) + "/" + (other != null)); return; }
            List<Labor> a, b;
            Sides(out a, out b);
            List<Labor> own = a.Count > 0 && a[0].faction == mine ? a : b;
            Vector3 c = Center(own);
            Vector3 side = new Vector3(6f, 0f, 0f);
            int x = BtcDevMatch.SpawnNow("bricktron", n, mine, c - side);
            int y = BtcDevMatch.SpawnNow("bricktron", n, other, c + side);
            BtcDevLab.Lab("animnet spawn host=" + x + " other=" + y + " at=" + c);
        }

        private static readonly System.Random Rng = new System.Random(7);
        private static void Walk()
        {
            List<Labor> a, b;
            Sides(out a, out b);
            a.AddRange(b);
            for (int i = 0; i < a.Count; i++)
            {
                double ang = Rng.NextDouble() * Math.PI * 2.0, r = 6.0 + Rng.NextDouble() * 6.0;
                Vector3 to = a[i].transform.position + new Vector3((float)(Math.Cos(ang) * r), 0f, (float)(Math.Sin(ang) * r));
                Order.Move.RunToward.Send(a[i], new SpaceLocation(to, true), Labor.DecisionSource.User);
            }
            BtcDevLab.Lab("animnet walk units=" + a.Count);
        }
    }
}
#endif
