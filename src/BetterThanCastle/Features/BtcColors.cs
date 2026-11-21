using System;
using System.Collections.Generic;
using System.Text;
using Brix.Assets;
using Brix.Game;
using Brix.Game.Components.Tracking;
using Brix.Network;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Faction colour plumbing for extended colours (BtcLobby.ExtendProtos: Purple/Pink/Cyan/Black after the gamemode's
    /// playable factions). Identity of a colour on the wire = Faction.ID (= prototype uid, SyncVar in the spawn payload);
    /// FactionColor/name are SyncVars; CrystalColor/FactionIcon/CrystalIcon/usesFireflies are LOCAL (PopulateFactionsLocally).
    /// Tables indexed by Faction.ID: LocalFactionVision.FactionsSeeingMe (8 bits, BtcFactionCap extends to 39) and
    /// TrackingRegistry.TrackingGroup.FactionEntryCount (int[8], Patches/TrackingRegistryPatches.cs extends to TrackCap);
    /// int bitmasks `1 << ID` (trackedFactions, EnemyOrNeutralFactions) are fine below 32.
    /// </summary>
    internal static class BtcColors
    {
        internal const int TrackCap = 64;
        internal static bool TrackPatched;

        /// <summary>
        /// BC ON replacement of SimpleHostScript.PopulateFactionsLocally (client). Vanilla: for each local prototype
        /// `allFactions.First(x =&gt; x.ID == uid)` -&gt; throws (load coroutine dies) when the local list has a uid the host
        /// did not spawn, and leaves host factions without local visuals when the local list is shorter. Here: match each
        /// networked faction by ID, then by synced name; unknown factions get extended-colour visuals by name or a template.
        /// </summary>
        internal static void PopulateLocal()
        {
            Asset_Gamemode.FactionPrototype[] protos = GameParam.Gamemode.Disk_ReadFactionPrototypes();
            List<Faction> all = new List<Faction>(Faction.GetAllFactions());
            bool[] used = new bool[protos.Length];
            int tpl = -1;
            for (int i = 0; i < protos.Length; i++)
                if (!protos[i].isAI && !protos[i].isDefault && !protos[i].isSpectator) tpl = i;

            for (int k = 0; k < all.Count; k++)
            {
                Faction f = all[k];
                if (f == null) continue;
                string synced = f.name;
                int p = -1;
                for (int i = 0; i < protos.Length; i++)
                    if (!used[i] && protos[i].uid == f.ID) { p = i; break; }
                if (p < 0 && !string.IsNullOrEmpty(synced))
                    for (int i = 0; i < protos.Length; i++)
                        if (!used[i] && protos[i].name == synced) { p = i; break; }
                if (p >= 0)
                {
                    used[p] = true;
                    if (!string.IsNullOrEmpty(synced) && synced != protos[p].name)
                        BtcLog.Warn("Colors", "faction id=" + f.ID + " host name=" + synced + " local proto=" + protos[p].name + " (uid " + protos[p].uid + "): lists differ (host/client BC or gamemode mismatch)");
                    protos[p].PopulateFaction(f);
                    continue;
                }
                // Host spawned a faction this client has no prototype for: keep synced name/colour, fill local visuals.
                CrystalVisuals.CrystalColorEnum crystal;
                Sprite flag;
                bool extra = BtcLobby.ExtraVisuals(synced, out crystal, out flag);
                if (tpl >= 0)
                {
                    f.CrystalIcon = protos[tpl].crystalIcon;
                    f.usesFireflies = protos[tpl].usesFireflies;
                    if (!extra) { crystal = protos[tpl].crystalColor; flag = protos[tpl].flagIcon; }
                }
                f.CrystalColor = crystal;
                if (f.FactionIcon == null) f.FactionIcon = flag;
                BtcLog.Warn("Colors", "faction id=" + f.ID + " name=" + synced + " has no local prototype: " + (extra ? "extended-colour visuals" : "template visuals"));
            }
            for (int i = 0; i < protos.Length; i++)
                if (!used[i]) BtcLog.Warn("Colors", "local proto uid=" + protos[i].uid + " name=" + protos[i].name + " not spawned by host (skipped)");
        }

        /// <summary>One-shot load diagnostic: every faction with id, colour, crystal, icon and the Faction.ID tables' capacity.</summary>
        internal static void LogFactions(string where)
        {
            try
            {
                int cap = -1;
                try { cap = new TrackingRegistry.TrackingGroup().FactionEntryCount.Length; } catch (Exception) { }
                User lu = User.LocalUser;
                Faction lf = lu != null ? lu.faction : null;
                BtcLog.Info("Colors", where + " BC=" + (BtcNetworkGate.Enabled ? "on" : "off") + " host=" + UnityEngine.Networking.NetworkServer.active
                    + " trackCap=" + cap + " visionCap=40 local=" + (lf != null ? lf.name + "#" + lf.ID : "none"));
                List<Faction> all = Faction.GetAllFactions();
                for (int k = 0; all != null && k < all.Count; k++)
                {
                    Faction f = all[k];
                    if (f == null) continue;
                    StringBuilder sb = new StringBuilder(160);
                    sb.Append(where).Append(" faction id=").Append(f.ID).Append(" name=").Append(f.name)
                      .Append(" col=#").Append(ColorUtility.ToHtmlStringRGB(f.FactionColor))
                      .Append(" crystal=").Append(f.CrystalColor)
                      .Append(" icon=").Append(f.FactionIcon != null ? "y" : "n")
                      .Append(" playable=").Append(f.isPlayable ? "y" : "n")
                      .Append(" spec=").Append(f.IsSpectator ? "y" : "n");
                    if (f.ID >= cap) sb.Append(" TRACK-OVERFLOW");
                    if (f.ID >= 40) sb.Append(" VISION-OVERFLOW");
                    try
                    {
                        List<User> owners = f.AllOwners;
                        sb.Append(" owners=");
                        for (int i = 0; owners != null && i < owners.Count; i++)
                            if (owners[i] != null) sb.Append(i > 0 ? "," : "").Append(BtcNick.Plain(owners[i].ScreenName));
                    }
                    catch (Exception) { }
                    BtcLog.Info("Colors", sb.ToString());
                }
                if (UnityEngine.Networking.NetworkServer.active && UNetManager.instance != null && UNetManager.instance.UserList != null)
                {
                    foreach (User u in UNetManager.instance.UserList)
                    {
                        if (u == null) continue;
                        Faction uf = u.faction;
                        BtcLog.Info("Colors", where + " user=" + BtcNick.Plain(u.ScreenName) + " factionNumber=" + u.factionNumber + " team=" + u.TeamNumber
                            + " -> " + (uf != null ? uf.name + "#" + uf.ID : "none"));
                    }
                }
            }
            catch (Exception e) { BtcLog.Error("Colors", "LogFactions: " + e); }
        }
    }
}
