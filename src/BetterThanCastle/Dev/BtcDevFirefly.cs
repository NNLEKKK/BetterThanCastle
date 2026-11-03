#if DEV
using System;
using System.Collections.Generic;
using System.Text;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using Brix.Network;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV: firefly economy per faction (extended colours debug). "hostfaction N" (lobby, host picks faction slot N),
    /// "ffdump tag" (factions: usesFireflies/units, nests: isHome/doBrew, fireflies by faction/mode), "ffkill N" (kill N own bricktrons).</summary>
    internal static class BtcDevFirefly
    {
        internal static void HostFaction(int n)
        {
            UNetManager m = UNetManager.instance;
            if (m == null || m.LocalLobbyPlayer == null) { BtcLog.Warn("Dev", "hostfaction: no lobby player"); return; }
            if (UnityEngine.Networking.NetworkServer.active) m.LocalLobbyPlayer.NetworkfactionNumber = n;
            else Traverse.Create(m.LocalLobbyPlayer).Method("CallCmdSetFaction", new object[] { n }).GetValue(); // weaver stub sends the Command
            BtcLog.Info("Dev", "hostfaction " + n);
        }

        /// <summary>"gameplay k=v;k=v": overwrite the live gameplay values in memory only (no MarkDirty, nothing saved to the user's file).</summary>
        internal static void Gameplay(string data)
        {
            try
            {
                float[] v = Traverse.Create(typeof(BtcGameplay)).Field("V").GetValue<float[]>();
                float[] d = BtcGameplay.Decode(data);
                if (v == null) { BtcLog.Warn("Dev", "gameplay: V null"); return; }
                Array.Copy(d, v, Math.Min(d.Length, v.Length));
                BtcGameplay.Refresh();
                BtcLog.Info("Dev", "gameplay set " + data + " -> " + BtcGameplay.Encode(BtcGameplay.E));
            }
            catch (Exception e) { BtcLog.Error("Dev", "gameplay: " + e); }
        }

        internal static void Dump(string tag)
        {
            try
            {
                StringBuilder sb = new StringBuilder("ffdump ").Append(tag).Append('\n');
                foreach (Faction f in Faction.GetAllFactions())
                {
                    if (f == null) continue;
                    sb.Append("  fac id=").Append(f.ID).Append(' ').Append(f.name).Append(" uses=").Append(f.usesFireflies)
                      .Append(" ai=").Append(f.isAI).Append(" owner=").Append(f.HasOwner()).Append(" units=").Append(f.UnitCount)
                      .Append(" spawnable=").Append(Firefly.SpawnableFirefliesForFaction(f)).Append(" respawning=").Append(Firefly.RespawningFirefliesForFaction(f)).Append('\n');
                }
                foreach (FireflyNest n in UnityEngine.Object.FindObjectsOfType<FireflyNest>())
                {
                    Faction f = n.faction;
                    sb.Append("  nest ").Append(n.name).Append(" fac=").Append(f != null ? f.name : "null").Append(" home=").Append(n.isHome)
                      .Append(" brew=").Append(n.doBrew).Append(" recFac=").Append(n.mainRecepteur != null && n.mainRecepteur.faction != null ? n.mainRecepteur.faction.name : "-");
                    try { sb.Append(" active=").Append(n.GetActiveOrSpawnableCharacterCount()); } catch (Exception e) { sb.Append(" active=ERR ").Append(e.GetType().Name); }
                    sb.Append('\n');
                }
                FireflyNest[] nests = UnityEngine.Object.FindObjectsOfType<FireflyNest>();
                int shown = 0;
                foreach (Firefly x in UnityEngine.Object.FindObjectsOfType<Firefly>())
                {
                    if (shown++ >= 30) break;
                    Faction f = x.faction;
                    float best = -1f;
                    foreach (FireflyNest n in nests)
                    {
                        if (n.faction != f || !n.isHome) continue;
                        float d = Vector3.Distance(n.transform.position, x.transform.position);
                        if (best < 0f || d < best) best = d;
                    }
                    Transform p = x.transform.parent;
                    sb.Append("  ff ").Append(f != null ? f.name : "null").Append(" named=").Append(x.IsNamed).Append(" old=").Append(x.isOldUnit)
                      .Append(" mode=").Append(x.movementMode).Append(" e=").Append(x.pureEnergy).Append(" homeDist=").Append(best.ToString("F1"))
                      .Append(" parent=").Append(p != null ? p.name : "-").Append(" step=").Append(Traverse.Create(x).Field("spawningStep").GetValue()).Append('\n');
                }
                BtcLog.Info("Dev", sb.ToString().TrimEnd('\n'));
            }
            catch (Exception e) { BtcLog.Error("Dev", "ffdump: " + e); }
        }

        internal static void Kill(int count, string facName)
        {
            try
            {
                Faction mine = User.LocalUser != null ? User.LocalUser.faction : null;
                if (!string.IsNullOrEmpty(facName))
                    foreach (Faction f in Faction.GetAllFactions()) if (f != null && f.name == facName) mine = f;
                int k = 0;
                foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
                {
                    if (k >= count) break;
                    AffiliationComponent a = l.GetComponent<AffiliationComponent>();
                    if (a == null || a.faction != mine) continue;
                    IDamageReceiver dr = l.GetComponent(typeof(IDamageReceiver)) as IDamageReceiver;
                    if (dr == null) continue;
                    dr.Kill();
                    k++;
                }
                BtcLog.Info("Dev", "ffkill " + k + " of " + (mine != null ? mine.name : "null"));
            }
            catch (Exception e) { BtcLog.Error("Dev", "ffkill: " + e); }
        }
    }
}
#endif
