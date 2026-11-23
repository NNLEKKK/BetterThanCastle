using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Assets;
using Brix.Game;
using Brix.Game.Network;
using Brix.Network;
using Brix.UI.Builder.Menu;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    // Three multiplayer bug fixes after Castle Story Plus 0.10.0 (Tricky12321, GPLv3: HiddenDeliveries, LobbyStart, SavedTeams).
    // None of them adds a network message or changes serialization. CSP used no custom message ids for these.
    //  - HiddenDeliveries: purely local on a client (runs with BetterConnection on or off; no wire effect).
    //  - LobbyStart, SavedTeams: change what the HOST does in the lobby / at game start, so they run only with
    //    BtcNetworkGate.Enabled (read at call time). With BC OFF every hook falls through to vanilla.
    //    Everything they send is a vanilla message (LobbyReadyToBegin, SyncVars) in a state the vanilla host could also produce.

    // ---------------------------------------------------------------------------------------------------------------
    // HiddenDeliveries: an item delivered to a blueprint is held by the blueprint's receptor; on a client the item's tracker
    // names the blueprint, but a blueprint is only sent to its own faction's players. An allied player's game had the item
    // but not the blueprint: the tracker found nothing to hold it and the item lay on the ground as an uncleanable heap.
    // Such an item is hidden as the receptor would, and shown again when it is tracked by something else.
    // ---------------------------------------------------------------------------------------------------------------
    internal static class BtcHiddenDeliveries
    {
        private const int ResourceTracker = 2; // TrackingPair.type of an IResourceTracker
        internal static readonly HashSet<GameObject> Hidden = new HashSet<GameObject>();

        internal static void Before(GenericParentTracker t, TrackingPair cur, TrackingPair prev)
        {
            if (NetworkServer.active || cur.IsSame(prev) || Hidden.Count == 0) return;
            if (Hidden.Remove(t.gameObject)) t.gameObject.SetActive(true);
        }

        internal static void After(GenericParentTracker t, TrackingPair cur)
        {
            if (NetworkServer.active || cur.type != ResourceTracker || cur.IsValid) return;
            if (Hidden.Count > 64) Hidden.RemoveWhere(delegate (GameObject g) { return g == null; });
            Hidden.Add(t.gameObject);
            t.gameObject.SetActive(false);
        }
    }

    [HarmonyPatch(typeof(GenericParentTracker), "UpdateTrackers")]
    internal static class BtcHiddenDeliveriesPatch
    {
        private static void Prefix(GenericParentTracker __instance, TrackingPair ___currentTracker, TrackingPair ___previousTracker)
        {
            try { BtcHiddenDeliveries.Before(__instance, ___currentTracker, ___previousTracker); }
            catch (Exception e) { BtcLog.Error("Hook", "HiddenDeliveries.Prefix: " + e); }
        }

        private static void Postfix(GenericParentTracker __instance, TrackingPair ___currentTracker)
        {
            try { BtcHiddenDeliveries.After(__instance, ___currentTracker); }
            catch (Exception e) { BtcLog.Error("Hook", "HiddenDeliveries.Postfix: " + e); }
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // LobbyStart: after the host pressed start, a client's "not ready" (or a lobby event that sets everyone not ready, e.g. a
    // rename) left the map never loading: everybody sat at 0 % on the loading screen. While Starting (host, BC ON):
    // a client's "not ready" is turned back into ready, "everyone not ready" is skipped, a player connecting is turned away,
    // a player leaving re-runs the all-ready check, a client's own button no longer sends "not ready" once its loading
    // screen began. After 60 s the lobby's normal rules apply again.
    // ---------------------------------------------------------------------------------------------------------------
    internal static class BtcLobbyStart
    {
        private const float TimeoutSeconds = 60f;
        private static bool _starting;
        private static float _startedAt;
        private static float _recheckAt = -1f;
        internal static bool LoadingShown; // client: own loading screen began (BC ON)
        private static GameObject _driver;

        internal static bool Starting
        {
            get
            {
                if (!_starting) return false;
                if (!BtcNetworkGate.Enabled || !NetworkServer.active) { Reset(); return false; }
                return true;
            }
        }

        internal static void Reset()
        {
            _starting = false;
            LoadingShown = false;
            _recheckAt = -1f;
        }

        internal static void Begin()
        {
            if (!BtcNetworkGate.Enabled || !NetworkServer.active) return;
            _starting = true;
            _startedAt = Time.unscaledTime;
            if (_driver == null)
            {
                _driver = new GameObject("BtcLobbyStart");
                UnityEngine.Object.DontDestroyOnLoad(_driver);
                _driver.AddComponent<BtcLobbyStartDriver>();
            }
            BtcLog.Info("LobbyStart", "host started the game; ready changes are held until the map loads");
        }

        internal static void RecheckSoon() { _recheckAt = Time.unscaledTime + 0.5f; }

        internal static void Done() { _starting = false; _recheckAt = -1f; }

        internal static void Tick()
        {
            if (!_starting) return;
            if (!Starting) return;
            if (_recheckAt >= 0f && Time.unscaledTime >= _recheckAt)
            {
                _recheckAt = -1f;
                UNetManager m = UNetManager.instance;
                if (m != null)
                {
                    BtcLog.Info("LobbyStart", "a player left while starting; checking whether the rest are ready");
                    m.CheckReadyToBegin();
                }
            }
            if (_starting && Time.unscaledTime - _startedAt > TimeoutSeconds)
            {
                BtcLog.Warn("LobbyStart", "the map did not load within " + TimeoutSeconds + " s of the start; normal ready rules apply again");
                _starting = false;
            }
        }
    }

    internal sealed class BtcLobbyStartDriver : MonoBehaviour
    {
        private void Update() { BtcLobbyStart.Tick(); }
    }

    [HarmonyPatch(typeof(GameParam), "BeginLoadingScreen")]
    internal static class BtcLobbyStartBeginPatch
    {
        private static void Prefix()
        {
            try { BtcLobbyStart.Begin(); } catch (Exception e) { BtcLog.Error("Hook", "LobbyStart.Begin: " + e); }
        }
    }

    // the lobby manager loaded the map (it returns early while someone is not ready)
    [HarmonyPatch(typeof(UNetManager), "OnLobbyServerPlayersReady")]
    internal static class BtcLobbyStartReadyPatch
    {
        private static void Postfix(UNetManager __instance)
        {
            try
            {
                if (BtcLobbyStart.Starting && __instance.LobbyPlayerList.TrueForAll(delegate (LobbyPlayer p) { return p.readyToBegin; }))
                    BtcLobbyStart.Done();
            }
            catch (Exception e) { BtcLog.Error("Hook", "LobbyStart.PlayersReady: " + e); }
        }
    }

    // a client's "not ready" after the start is turned back into ready (vanilla LobbyReadyToBegin message, like the lobby sends itself)
    [HarmonyPatch(typeof(NetworkLobbyManager), "OnServerReadyToBeginMessage")]
    internal static class BtcLobbyStartMessagePatch
    {
        private static void Postfix(NetworkLobbyManager __instance, NetworkMessage netMsg)
        {
            try
            {
                if (!BtcLobbyStart.Starting || netMsg == null || netMsg.conn == null) return;
                UNetManager m = UNetManager.instance;
                if (m == null || m.LobbyPlayerList == null) return;
                bool fixedAny = false;
                foreach (LobbyPlayer p in new List<LobbyPlayer>(m.LobbyPlayerList))
                {
                    if (p == null || p.readyToBegin || p.connectionToClient != netMsg.conn) continue;
                    BtcLog.Info("LobbyStart", "a player pressed not ready after the start; kept ready");
                    p.readyToBegin = true;
                    BtcLobbyReadyMsg ready = new BtcLobbyReadyMsg();
                    ready.slotId = p.slot;
                    ready.readyState = true;
                    NetworkServer.SendToReady(null, MsgType.LobbyReadyToBegin, ready);
                    fixedAny = true;
                }
                if (fixedAny) __instance.CheckReadyToBegin();
            }
            catch (Exception e) { BtcLog.Error("Hook", "LobbyStart.ReadyMessage: " + e); }
        }
    }

    [HarmonyPatch(typeof(LobbyMenu), "SetEveryoneNotReady")]
    internal static class BtcLobbyStartNotReadyPatch
    {
        private static bool Prefix() { return !BtcLobbyStart.Starting; }
    }

    // a player connecting after the start would count as not ready: turned away
    [HarmonyPatch(typeof(UNetManager), "OnLobbyServerConnect")]
    internal static class BtcLobbyStartConnectPatch
    {
        private static bool Prefix(UNetManager __instance, NetworkConnection conn)
        {
            try
            {
                if (!BtcLobbyStart.Starting) return true;
                BtcLog.Info("LobbyStart", "a player tried to join while the game was starting; turned away");
                __instance.SafeDisconnectConnection(conn);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "LobbyStart.Connect: " + e); return true; }
        }
    }

    [HarmonyPatch(typeof(UNetManager), "OnLobbyServerDisconnect")]
    internal static class BtcLobbyStartDisconnectPatch
    {
        private static void Postfix()
        {
            if (BtcLobbyStart.Starting) BtcLobbyStart.RecheckSoon();
        }
    }

    // client: own loading screen began
    [HarmonyPatch(typeof(GameParam), "OnPlayLoadingAnimation")]
    internal static class BtcLobbyStartShownPatch
    {
        private static void Postfix()
        {
            if (BtcNetworkGate.Enabled && !NetworkServer.active) BtcLobbyStart.LoadingShown = true;
        }
    }

    // client: "not ready" is not sent once the loading screen began
    [HarmonyPatch(typeof(LobbyMenu), "SetIsReady")]
    internal static class BtcLobbyStartSetReadyPatch
    {
        private static bool Prefix(bool value)
        {
            if (!value && BtcLobbyStart.LoadingShown && BtcNetworkGate.Enabled && !NetworkServer.active)
            {
                BtcLog.Info("LobbyStart", "not ready ignored, the game is loading");
                return false;
            }
            return true;
        }
    }

    // back in a lobby / left: the start of the last one is over
    [HarmonyPatch(typeof(LobbyMenu), "OpenSelf")]
    internal static class BtcLobbyStartOpenPatch
    {
        private static void Postfix() { BtcLobbyStart.Reset(); }
    }

    [HarmonyPatch(typeof(UNetManager), "OnStopClient")]
    internal static class BtcLobbyStartStopPatch
    {
        private static void Postfix() { BtcLobbyStart.Reset(); }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // SavedTeams: the game does not save which factions are allied; alliances are made at game start from the lobby's team
    // numbers, and a player joining the lobby of a loaded save gets the first free team. Two allies were two teams after
    // loading. Host + BC ON: the teams of the save's factions are written into the save folder (btc-teams.txt sidecar, vanilla
    // save bytes untouched, lines "team<TAB>faction name"); in the lobby of a loaded save each player is put in the team his
    // faction had; a player who picks a team himself keeps it; factions nobody takes get their saved relations at game start.
    // ---------------------------------------------------------------------------------------------------------------
    internal static class BtcSavedTeams
    {
        private const string FileName = "btc-teams.txt";
        internal static bool Applying;
        internal static readonly HashSet<LobbyPlayer> Chosen = new HashSet<LobbyPlayer>();
        private static readonly Dictionary<LobbyPlayer, int> BeforeFix = new Dictionary<LobbyPlayer, int>();

        internal static bool On
        {
            get { return BtcNetworkGate.Enabled && NetworkServer.active; }
        }

        internal static void SnapshotTeams()
        {
            BeforeFix.Clear();
            UNetManager m = UNetManager.instance;
            if (m == null || m.LobbyPlayerList == null) return;
            foreach (LobbyPlayer p in m.LobbyPlayerList) if (p != null) BeforeFix[p] = p.TeamNumber;
        }

        internal static void Write(string folder)
        {
            if (!On || string.IsNullOrEmpty(folder)) return;
            List<Faction> factions = Faction.GetAllFactions();
            if (factions == null || factions.Count == 0) return;
            Faction.FactionMeta[] metas = Faction.GetAllFactionsMeta();
            List<Faction> players = new List<Faction>();
            for (int i = 0; i < factions.Count && i < metas.Length; i++)
            {
                Faction f = factions[i];
                if (metas[i].assigned && f != null && !f.isAI && !f.IsSpectator && f != Faction.DefaultNeutral) players.Add(f);
            }
            string file = Path.Combine(folder, FileName);
            if (players.Count == 0) return;
            int[] teams = new int[players.Count];
            for (int i = 0; i < teams.Length; i++) teams[i] = -1;
            int next = 0;
            for (int i = 0; i < players.Count; i++)
            {
                if (teams[i] >= 0) continue;
                teams[i] = next++;
                for (int j = i + 1; j < players.Count; j++)
                    if (teams[j] < 0 && Affiliation.IsAllied(players[i], players[j])) teams[j] = teams[i];
            }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < players.Count; i++)
                sb.Append(teams[i].ToString(CultureInfo.InvariantCulture)).Append('\t').Append(players[i].name).Append('\n');
            File.WriteAllText(file, sb.ToString());
            BtcLog.Info("SavedTeams", "saved teams of " + players.Count + " faction(s)");
        }

        // faction name -> team, or null (older save, new game, no file)
        internal static Dictionary<string, int> TeamsByName(Asset_Map map)
        {
            if (map == null || map.IsNewGame() || string.IsNullOrEmpty(map.path)) return null;
            string file = Path.Combine(map.path, FileName);
            if (!File.Exists(file)) return null;
            Dictionary<string, int> r = new Dictionary<string, int>();
            foreach (string line in File.ReadAllLines(file))
            {
                int tab = line.IndexOf('\t');
                int t;
                if (tab <= 0 || !int.TryParse(line.Substring(0, tab), NumberStyles.Integer, CultureInfo.InvariantCulture, out t)) continue;
                r[line.Substring(tab + 1)] = t;
            }
            return r.Count == 0 ? null : r;
        }

        // the lobby numbers the save's factions that had a player, in the save's order (GameParam.RefreshFactions)
        private static int TeamOf(Asset_Map map, int factionNumber)
        {
            if (factionNumber < 0) return -1;
            Dictionary<string, int> byName = TeamsByName(map);
            if (byName == null) return -1;
            Faction.FactionMeta[] metas = map.Disk_ReadFactionMeta();
            if (metas == null) return -1;
            int index = 0;
            foreach (Faction.FactionMeta meta in metas)
            {
                if (!meta.assigned) continue;
                if (index == factionNumber)
                {
                    int t;
                    return byName.TryGetValue(meta.name, out t) ? t : -1;
                }
                index++;
            }
            return -1;
        }

        internal static void Apply(LobbyPlayer player)
        {
            if (!On || player == null) return;
            Asset_Map map = GameParam.Map;
            if (map == null || map.IsNewGame() || string.IsNullOrEmpty(map.path)) return;
            bool outer = Applying;
            Applying = true;
            try
            {
                if (Chosen.Contains(player)) return;
                int team = TeamOf(map, player.factionNumber);
                if (team < 0 || team >= GameParam.MaxTeamCount || team == player.TeamNumber) return;
                int was = player.TeamNumber;
                player.NetworkTeamNumber = team;
                BtcLog.Info("SavedTeams", "faction " + player.factionNumber + " put in team " + (team + 1) + " as in the save (was " + (was + 1) + ")");
            }
            catch (Exception e) { BtcLog.Warn("SavedTeams", "could not read the save's teams: " + e.Message); }
            finally { Applying = outer; }
        }

        internal static void ApplyAll()
        {
            UNetManager m = UNetManager.instance;
            if (!On || m == null || m.LobbyPlayerList == null) return;
            bool outer = Applying;
            Applying = true;
            try { foreach (LobbyPlayer p in new List<LobbyPlayer>(m.LobbyPlayerList)) Apply(p); }
            finally { Applying = outer; }
        }

        // after the game's lobby check (GameParam.FixTeams) moved players: the saved teams come back if valid for the mode
        internal static void KeepSaved()
        {
            UNetManager m = UNetManager.instance;
            if (!On || Applying || m == null || m.LobbyPlayerList == null) return;
            if (GameParam.Map == null || GameParam.Map.IsNewGame()) return;
            List<LobbyPlayer> players = new List<LobbyPlayer>(m.LobbyPlayerList);
            players.RemoveAll(delegate (LobbyPlayer p) { return p == null; });
            Chosen.RemoveWhere(delegate (LobbyPlayer p) { return p == null; });
            int[] repaired = new int[players.Count];
            for (int i = 0; i < repaired.Length; i++) repaired[i] = players[i].TeamNumber;
            ApplyAll();
            Applying = true;
            try
            {
                foreach (LobbyPlayer p in players)
                {
                    int before;
                    if (Chosen.Contains(p) && BeforeFix.TryGetValue(p, out before) && p.TeamNumber != before) p.NetworkTeamNumber = before;
                }
            }
            finally { Applying = false; }
            if (SavedAreValid(players)) return;
            Applying = true;
            try { for (int i = 0; i < repaired.Length; i++) players[i].NetworkTeamNumber = repaired[i]; }
            finally { Applying = false; }
        }

        // GameParam.AreTeamsValid wants every team slot filled, so the mode's limits are checked here
        private static bool SavedAreValid(List<LobbyPlayer> players)
        {
            GameParam.GameParameters lim = GameParam.instance.Parameters;
            int slots = GameParam.CurrentTeamInfos.Length, minT = GameParam.MinTeamCount, maxT = GameParam.MaxTeamCount;
            Dictionary<int, int> sizes = new Dictionary<int, int>();
            foreach (LobbyPlayer p in players)
            {
                int t = p.TeamNumber;
                if (t < 0 || t >= slots) return false;
                int n;
                sizes.TryGetValue(t, out n);
                sizes[t] = n + 1;
            }
            if (minT != -1 && sizes.Count < minT) return false;
            if (maxT != -1 && sizes.Count > maxT) return false;
            foreach (int s in sizes.Values)
            {
                if (lim.MinPlayersPerTeam != -1 && s < lim.MinPlayersPerTeam) return false;
                if (lim.MaxPlayersPerTeam != -1 && s > lim.MaxPlayersPerTeam) return false;
            }
            return true;
        }

        // host at game start: a saved faction no player took over has no relations from the game; it gets the saved ones
        internal static void SetRelationsOfFreeFactions()
        {
            if (!On) return;
            try
            {
                Dictionary<string, int> teams = TeamsByName(GameParam.Map);
                if (teams == null) return;
                List<Faction> saved = new List<Faction>();
                foreach (Faction f in Faction.GetAllFactions()) if (f != null && teams.ContainsKey(f.name)) saved.Add(f);
                foreach (Faction free in saved)
                {
                    if (free.AllOwners.Count > 0) continue;
                    foreach (Faction other in saved)
                    {
                        if (other == free) continue;
                        Affiliation.Relation r = teams[free.name] == teams[other.name] ? Affiliation.Relation.Allied : Affiliation.Relation.Enemy;
                        free.SetFactionRelation(other, r);
                        other.SetFactionRelation(free, r);
                    }
                    free.SetFactionRelation(Faction.DefaultEnemy, Affiliation.Relation.Enemy);
                    free.SetFactionRelation(Faction.DefaultNeutral, Affiliation.Relation.Neutral);
                    Faction.DefaultEnemy.SetFactionRelation(free, Affiliation.Relation.Enemy);
                    Faction.DefaultNeutral.SetFactionRelation(free, Affiliation.Relation.Neutral);
                    BtcLog.Info("SavedTeams", free.name + " has no player; relations as in the save");
                }
            }
            catch (Exception e) { BtcLog.Warn("SavedTeams", "could not set relations of free factions: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(GameParam), "FixTeams")]
    internal static class BtcSavedTeamsFixPatch
    {
        private static void Prefix() { try { BtcSavedTeams.SnapshotTeams(); } catch (Exception e) { BtcLog.Error("Hook", "SavedTeams.Snapshot: " + e); } }
        private static void Postfix() { try { BtcSavedTeams.KeepSaved(); } catch (Exception e) { BtcLog.Error("Hook", "SavedTeams.Keep: " + e); } }
    }

    [HarmonyPatch(typeof(Asset_Map), "Disk_Save")]
    internal static class BtcSavedTeamsSavePatch
    {
        private static void Postfix(Asset_Map __instance)
        {
            try { BtcSavedTeams.Write(__instance.path); }
            catch (Exception e) { BtcLog.Error("Hook", "SavedTeams.Save: " + e); }
        }
    }

    // a joining player: the game just gave him the first free team; every player is put back in the saved team
    [HarmonyPatch(typeof(LobbyPlayer), "Start")]
    internal static class BtcSavedTeamsJoinPatch
    {
        private static void Postfix(LobbyPlayer __instance)
        {
            try { BtcSavedTeams.Apply(__instance); BtcSavedTeams.ApplyAll(); }
            catch (Exception e) { BtcLog.Error("Hook", "SavedTeams.Join: " + e); }
        }
    }

    // a player taking another of the save's factions takes its team along
    [HarmonyPatch(typeof(LobbyPlayer), "NetworkfactionNumber", MethodType.Setter)]
    internal static class BtcSavedTeamsFactionPatch
    {
        private static void Prefix(LobbyPlayer __instance, out int __state) { __state = __instance.factionNumber; }

        private static void Postfix(LobbyPlayer __instance, int __state)
        {
            try { if (__instance.factionNumber != __state) BtcSavedTeams.Apply(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "SavedTeams.Faction: " + e); }
        }
    }

    // the host picking another save while players wait
    [HarmonyPatch(typeof(GameParam), "Map", MethodType.Setter)]
    internal static class BtcSavedTeamsMapPatch
    {
        private static void Postfix()
        {
            try
            {
                if (!BtcSavedTeams.On) return;
                BtcSavedTeams.Chosen.Clear();
                BtcSavedTeams.ApplyAll();
            }
            catch (Exception e) { BtcLog.Error("Hook", "SavedTeams.Map: " + e); }
        }
    }

    // a player picking his team: it is his, the saved team no longer overrides it (runs on the host: Cmd body)
    [HarmonyPatch(typeof(LobbyPlayer), "CmdSetTeam")]
    internal static class BtcSavedTeamsChosenPatch
    {
        private static void Prefix(LobbyPlayer __instance)
        {
            if (BtcSavedTeams.On && !BtcSavedTeams.Applying) BtcSavedTeams.Chosen.Add(__instance);
        }
    }

    // game start: relations of free saved factions (after vanilla Run or the BC copy HostRun)
    [HarmonyPatch(typeof(SimpleHostScript), "Run")]
    internal static class BtcSavedTeamsRelationsPatch
    {
        private static void Postfix() { BtcSavedTeams.SetRelationsOfFreeFactions(); }
    }
}

namespace BetterThanCastle
{
    /// <summary>Same layout as UnityEngine.Networking.NetworkSystem.LobbyReadyToBeginMessage (internal in the engine): byte slotId, bool readyState; sent with the vanilla id MsgType.LobbyReadyToBegin.</summary>
    internal sealed class BtcLobbyReadyMsg : MessageBase
    {
        public byte slotId;
        public bool readyState;
        public override void Serialize(NetworkWriter writer) { writer.Write(slotId); writer.Write(readyState); }
        public override void Deserialize(NetworkReader reader) { slotId = reader.ReadByte(); readyState = reader.ReadBoolean(); }
    }
}
