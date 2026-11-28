using System;
using System.Collections.Generic;
using Brix.Game;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// Personal work settings of every player that the host AI applies to that player's own units and buildings (BC ON):
    ///   logs  1/0  tree cutting leaves logs and stumps on the ground (Orders "chop_keep_logs", BtcChopRules)
    ///   chopR int  default floor radius of a new chopping block, 0 = no limit (Orders "chop_block_radius", BtcStationRadius)
    ///   rack  1/0  default rack limit of crafting stations without their own mode (Orders "craft_rack_limit", BtcWorkbench)
    ///   help  1/0  default helper of crafting stations without their own mode (Orders "craft_helpers", default on, BtcWorkbench)
    /// Wire: 134 client->host "logs=0;chopR=15;rack=0;help=1", sent at handler registration, on change and every 10 s ("trap=" of older
    /// versions is ignored: trap re-arming is the bear traps task now, BtcTrapTask).
    /// OFF: no handler, no send; every faction gets the vanilla values (logs carried, no radius).
    /// Players that never sent (vanilla clients, AI) also get the vanilla values.
    /// </summary>
    internal static class BtcPlayerPrefs
    {
        internal const short Msg = 134;

        internal sealed class Prefs
        {
            public bool KeepLogs;
            public int ChopR;
            public bool RackLimit;   // Better Workbenches: stations without their own mode skip kit parts no rack has room for
            public bool Helpers = true;   // Better Workbenches: stations without their own mode get one helper bringing ingredients of later orders
        }

        private static readonly Prefs Vanilla = new Prefs();
        private static readonly Prefs Local = new Prefs();
        private static readonly Dictionary<int, KeyValuePair<User, Prefs>> Remote = new Dictionary<int, KeyValuePair<User, Prefs>>();
        private static string _sent;
        private static float _sendAt, _sentAt;

        // ------------------------------------------------------------------ local values
        internal static bool LocalKeepLogs { get { return BtcHotkeys.GetBool("chop_keep_logs", false); } }
        internal static bool LocalRackLimit { get { return BtcHotkeys.GetBool("craft_rack_limit", false); } }
        internal static bool LocalHelpers { get { return BtcHotkeys.GetBool("craft_helpers", true); } }
        internal static int LocalChopR { get { return Mathf.Clamp(Mathf.RoundToInt(BtcHotkeys.GetFloat("chop_block_radius", 15f)), 0, 100); } }

        private static string Encode()
        {
            return "logs=" + (LocalKeepLogs ? 1 : 0) + ";chopR=" + LocalChopR + ";rack=" + (LocalRackLimit ? 1 : 0) + ";help=" + (LocalHelpers ? 1 : 0);
        }

        // ------------------------------------------------------------------ host lookup
        /// <summary>Host: settings of the player owning this faction (vanilla values when unknown or BC OFF).</summary>
        internal static Prefs For(Faction f)
        {
            if (f == null || !BtcNetworkGate.Enabled) return Vanilla;
            User lu = User.LocalUser;
            if (lu != null && ReferenceEquals(lu.faction, f))
            {
                Local.KeepLogs = LocalKeepLogs;
                Local.ChopR = LocalChopR;
                Local.RackLimit = LocalRackLimit;
                Local.Helpers = LocalHelpers;
                return Local;
            }
            foreach (KeyValuePair<int, KeyValuePair<User, Prefs>> kv in Remote)
            {
                User u = kv.Value.Key;
                if (u != null && ReferenceEquals(u.faction, f)) return kv.Value.Value;
            }
            return Vanilla;
        }

        /// <summary>Host: settings of the owner of a building, trap or project part (the faction is looked up through the parents).</summary>
        internal static Prefs For(GameObject go) { return go != null && BtcNetworkGate.Enabled ? For(FactionOf(go)) : Vanilla; }

        /// <summary>Owner faction of a building, trap or project part (looked up through the parents).</summary>
        internal static Faction FactionOf(GameObject go)
        {
            if (go == null) return null;
            Brix.Game.AI.Project p = go.GetComponentInParent<Brix.Game.AI.Project>();
            if (p != null && p.faction != null) return p.faction;
            Brix.Game.Components.AffiliationComponent a = go.GetComponentInParent<Brix.Game.Components.AffiliationComponent>();
            if (a != null && a.faction != null) return a.faction;
            return Faction.GetFaction(go);
        }

        // ------------------------------------------------------------------ net
        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            Remote.Clear();
            _sent = null;
            _sendAt = 0f;
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                if (NetworkServer.active)
                {
                    NetworkServer.UnregisterHandler(Msg);
                    NetworkServer.RegisterHandler(Msg, OnPrefsAtHost);
                }
            }
            catch (Exception e) { BtcLog.Error("Prefs", "RegisterHandlers: " + e); }
        }

        /// <summary>BtcTimer.Tick: a client sends its settings on change and every 10 s (the host may have missed the first one).</summary>
        internal static void Tick()
        {
            if (NetworkServer.active || !BtcNetworkGate.Enabled) return;
            float now = Time.unscaledTime;
            if (now < _sendAt) return;
            try
            {
                UNetManager m = NetworkManager.singleton as UNetManager;
                if (m == null || m.LocalNetworkClient == null || !m.LocalNetworkClient.isConnected) { _sendAt = now + 1f; return; }
                string s = Encode();
                if (s == _sent && now < _sentAt + 10f) { _sendAt = now + 1f; return; }
                m.LocalNetworkClient.Send(Msg, new StringMessage(s));
                if (s != _sent) BtcLog.Info("Prefs", "sent " + s);
                _sent = s;
                _sentAt = now;
                _sendAt = now + 1f;
            }
            catch (Exception e) { _sendAt = now + 5f; BtcLog.Error("Prefs", "Tick: " + e.Message); }
        }

        private static void OnPrefsAtHost(NetworkMessage nm)
        {
            try
            {
                if (!BtcNetworkGate.Enabled || nm.conn == null) return;
                string s = nm.ReadMessage<StringMessage>().value ?? string.Empty;
                User u = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(nm.conn) : null;
                KeyValuePair<User, Prefs> old;
                Prefs p = Remote.TryGetValue(nm.conn.connectionId, out old) ? old.Value : new Prefs();
                string before = p.KeepLogs + "/" + p.ChopR + "/" + p.RackLimit + "/" + p.Helpers;
                string[] parts = s.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    string[] kv = parts[i].Split('=');
                    int v;
                    if (kv.Length != 2 || !int.TryParse(kv[1], out v)) continue;
                    if (kv[0] == "logs") p.KeepLogs = v != 0;
                    else if (kv[0] == "chopR") p.ChopR = Mathf.Clamp(v, 0, 100);
                    else if (kv[0] == "rack") p.RackLimit = v != 0;
                    else if (kv[0] == "help") p.Helpers = v != 0;
                }
                Remote[nm.conn.connectionId] = new KeyValuePair<User, Prefs>(u, p);
                string after = p.KeepLogs + "/" + p.ChopR + "/" + p.RackLimit + "/" + p.Helpers;
                if (old.Value == null || before != after) BtcLog.Info("Prefs", "conn " + nm.conn.connectionId + " (" + (u != null ? u.name : "?") + "): " + s);
            }
            catch (Exception e) { BtcLog.Error("Prefs", "OnPrefsAtHost: " + e); }
        }
    }
}
