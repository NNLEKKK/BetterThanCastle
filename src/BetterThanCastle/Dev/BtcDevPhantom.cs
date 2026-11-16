#if DEV
using System;
using System.Collections.Generic;
using System.Text;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV lab for phantoms (BtcPhantom). Scenario commands:
    /// phantom dump &lt;tag&gt;  - per player faction: unit count, spawn price, live phantoms (HP, stamina, vision, look), corpses on the map;
    /// phantom kill [n]     - kills n phantoms (default 1) through the vanilla damage receiver;
    /// phantom fist         - a phantom punches an own worker (HP must not change), then a worker hits the phantom (it must die).
    /// Every result is a LAB line (agent.log, tag Lab).
    /// </summary>
    internal static class BtcDevPhantom
    {
        internal static bool Cmd(string[] p)
        {
            string sub = p.Length > 1 ? p[1] : "dump";
            try
            {
                switch (sub)
                {
                    case "dump": Dump(p.Length > 2 ? p[2] : "-"); break;
                    case "kill": Kill(p.Length > 2 ? int.Parse(p[2]) : 1); break;
                    case "fist": Fist(); break;
                    default: BtcDevLab.Lab("phantom: unknown " + sub); break;
                }
            }
            catch (Exception e) { BtcDevLab.Lab("phantom " + sub + " error: " + e); }
            return true;
        }

        private static void Dump(string tag)
        {
            List<GameObject> live = BtcPhantom.DevLive();
            StringBuilder sb = new StringBuilder("phantom dump " + tag + " live=" + BtcPhantom.LiveCount + " list=" + live.Count);
            int corpses = 0;
            foreach (Corpse c in UnityEngine.Object.FindObjectsOfType<Corpse>()) if (c.gameObject.activeInHierarchy) corpses++;
            sb.Append(" corpses=").Append(corpses);
            List<FireflyNest> nests = BtcMatchStats.NestList();
            if (nests != null)
                foreach (FireflyNest n in nests)
                {
                    if (n == null || !n.isHome || n.faction == null || n.faction.isAI) continue;
                    int ph = 0;
                    foreach (GameObject g in live) { Labor l = g.GetComponent<Labor>(); if (l != null && l.faction == n.faction) ph++; }
                    int real = 0;
                    foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>()) if (l.faction == n.faction && l.gameObject.activeInHierarchy) real++;
                    sb.Append(" | fac=").Append(n.faction.ID).Append(" unitCount=").Append(n.faction.UnitCount).Append(" laborsOnMap=").Append(real)
                      .Append(" phantoms=").Append(ph).Append(" price=").Append(n.NewFireflyRequiredEnergy).Append(" energy=").Append(n.GetAvailablePurifiedEnergy());
                }
            BtcDevLab.Lab(sb.ToString());
            foreach (GameObject g in live)
            {
                BricktronDamageReceiver dr = g.GetComponent<BricktronDamageReceiver>();
                LocalFactionVision v = g.GetComponent<LocalFactionVision>();
                Occupation o = g.GetComponent<Occupation>();
                BtcDevLab.Lab("  phantom " + g.name + " active=" + g.activeInHierarchy + " occ=" + (o != null ? o.CurrentOccupation.ToString() : "?")
                    + " hp=" + (dr != null ? dr.CurrentHP + "/" + dr.MaxHP : "?") + " stam=" + (dr != null ? dr.CurrentStamina + "/" + dr.MaxStamina : "?")
                    + " vis=" + (v != null ? v.LineOfSightRange.ToString() : "?") + " look=" + (g.GetComponent<BtcPhantomLook>() != null)
                    + " pos=" + g.transform.position);
            }
        }

        private static void Kill(int n)
        {
            List<GameObject> live = BtcPhantom.DevLive();
            int k = 0;
            for (int i = 0; i < live.Count && k < n; i++)
            {
                BricktronDamageReceiver dr = live[i].GetComponent<BricktronDamageReceiver>();
                if (dr == null) continue;
                dr.ReceiveAttack(5f, DamageType.Melee, DamageReaction.Twitch, live[i].transform.position, Vector3.zero, 0f, null);
                k++;
            }
            BtcDevLab.Lab("phantom kill " + k + "/" + n + " liveAfter=" + BtcPhantom.LiveCount);
        }

        private static void Fist()
        {
            List<GameObject> live = BtcPhantom.DevLive();
            if (live.Count == 0) { BtcDevLab.Lab("phantom fist: no phantom"); return; }
            GameObject ph = live[0];
            Labor pl = ph.GetComponent<Labor>();
            GameObject worker = null;
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
                if (l.faction == pl.faction && l.gameObject.activeInHierarchy && !BtcPhantom.IsPhantom(l.gameObject)) { worker = l.gameObject; break; }
            if (worker == null) { BtcDevLab.Lab("phantom fist: no worker"); return; }
            BricktronDamageReceiver wdr = worker.GetComponent<BricktronDamageReceiver>();
            float before = wdr.CurrentHP;
            DamageReaction r = wdr.ReceiveAttack(20f, DamageType.Melee, DamageReaction.Twitch, worker.transform.position, Vector3.zero, 0f, ph);
            BtcDevLab.Lab("phantom fist: worker hp " + before + " -> " + wdr.CurrentHP + " reaction=" + r + " (expect unchanged, Ignored)");
            BricktronDamageReceiver pdr = ph.GetComponent<BricktronDamageReceiver>();
            pdr.ReceiveAttack(1f, DamageType.Melee, DamageReaction.Twitch, ph.transform.position, Vector3.zero, 0f, worker);
            BtcDevLab.Lab("phantom fist: phantom hit by worker -> isPhantomStill=" + BtcPhantom.IsPhantom(ph) + " active=" + ph.activeInHierarchy + " hp=" + pdr.CurrentHP);
        }
    }
}
#endif
