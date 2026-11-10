using System;
using System.Collections.Generic;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Compatibility check between players (BetterConnection ON only, message 108): every side tells the other its mod version.
    /// Shows clickable cards at the top: versions differ, the other side runs an older mod without version info, the other side looks vanilla
    /// (no BetterConnection: "turn BetterConnection off to join it") and, after a connection error, a hint to switch BetterConnection
    /// (works with BetterConnection OFF too: the hint only reads local state, nothing is sent). Cards open the BetterConnection tab (master switch).
    /// Wire: BC OFF sends and registers nothing (this class is only called from BC ON paths); one small message 108 per connection otherwise.
    /// </summary>
    internal static class BtcNetCheck
    {
        internal const short MsgInfo = 108;

        internal class InfoMessage : MessageBase
        {
            public string version = "";
            public override void Serialize(NetworkWriter writer) { writer.Write(version ?? ""); }
            public override void Deserialize(NetworkReader reader) { version = reader.ReadString(); }
        }

        // client side (the host we joined)
        private static float _connectedAt = -1f;
        private static bool _hostInfo, _hostHeartbeat;
        private static string _hostVersion;
        // host side (per connection id)
        private static readonly Dictionary<int, string> SrvVersion = new Dictionary<int, string>();
        private static readonly Dictionary<int, float> SrvSeen = new Dictionary<int, float>();
        private static readonly HashSet<int> SrvHeartbeat = new HashSet<int>();
        // connection error hint
        private static float _errUntil;
        private static bool _errBcOn;
        private static readonly HashSet<string> Dismissed = new HashSet<string>();
        private static readonly HashSet<string> Opened = new HashSet<string>();

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }
        private static string Mine { get { return BtcPlugin.Version; } }

        // ------------------------------------------------------------------ wire (BC ON)
        internal static void RegisterHandlers(UNetManager m)
        {
            try
            {
                if (NetworkServer.active)
                {
                    if (NetworkServer.handlers.ContainsKey(MsgInfo)) NetworkServer.UnregisterHandler(MsgInfo);
                    NetworkServer.RegisterHandler(MsgInfo, delegate (NetworkMessage msg)
                    {
                        InfoMessage im = msg.ReadMessage<InfoMessage>();
                        if (msg.conn == null) return;
                        SrvVersion[msg.conn.connectionId] = im.version;
                        msg.conn.Send(MsgInfo, new InfoMessage { version = Mine });
                    });
                }
                else if (m.LocalNetworkClient != null)
                {
                    NetworkClient c = m.LocalNetworkClient;
                    if (c.handlers.ContainsKey(MsgInfo)) c.UnregisterHandler(MsgInfo);
                    c.RegisterHandler(MsgInfo, delegate (NetworkMessage msg)
                    {
                        InfoMessage im = msg.ReadMessage<InfoMessage>();
                        _hostInfo = true; _hostVersion = im.version;
                    });
                }
            }
            catch (Exception e) { BtcLog.Error("NetCheck", "register: " + e.Message); }
        }

        /// <summary>Client connected (BC ON): remember the moment and tell the host our version.</summary>
        internal static void OnClientConnected(UNetManager m)
        {
            if (NetworkServer.active) return;   // the host is its own local client: nothing to check
            _connectedAt = Time.unscaledTime; _hostInfo = false; _hostVersion = null; _hostHeartbeat = false;
            Dismissed.Clear(); Opened.Clear();
            RegisterHandlers(m);
            try { if (m.LocalNetworkClient != null && m.LocalNetworkClient.isConnected) m.LocalNetworkClient.Send(MsgInfo, new InfoMessage { version = Mine }); }
            catch (Exception e) { BtcLog.Error("NetCheck", "send: " + e.Message); }
        }

        internal static void OnServerStarted(UNetManager m)
        {
            SrvVersion.Clear(); SrvSeen.Clear(); SrvHeartbeat.Clear();
            Dismissed.Clear(); Opened.Clear();
            RegisterHandlers(m);
        }

        private static float _nextTick, _lastSend;

        /// <summary>Called every frame while BetterConnection is connected: keeps the handler registered (UNET drops handlers when the server restarts for a game) and repeats our info until the other side answered, so a lost first message is not reported as a version mismatch.</summary>
        internal static void Tick(UNetManager m)
        {
            float now = Time.unscaledTime;
            if (now < _nextTick) return;
            _nextTick = now + 2f;
            try
            {
                if (NetworkServer.active) { if (!NetworkServer.handlers.ContainsKey(MsgInfo)) RegisterHandlers(m); return; }
                NetworkClient c = m.LocalNetworkClient;
                if (_connectedAt < 0f || c == null || !c.isConnected) return;
                if (!c.handlers.ContainsKey(MsgInfo)) RegisterHandlers(m);
                if (!_hostInfo && now - _lastSend > 3f) { _lastSend = now; c.Send(MsgInfo, new InfoMessage { version = Mine }); }
            }
            catch (Exception e) { BtcLog.Error("NetCheck", "tick: " + e.Message); }
        }

        /// <summary>A heartbeat arrived (connId = connection on the host, -1 on the client): the other side runs BetterConnection.</summary>
        internal static void HeartbeatFrom(int connId)
        {
            if (connId < 0) _hostHeartbeat = true; else SrvHeartbeat.Add(connId);
        }

        /// <summary>A fatal client error (called for BC ON and OFF): the hint card suggests switching BetterConnection.</summary>
        internal static void OnClientError(bool bcOn)
        {
            _errUntil = Time.unscaledTime + 90f; _errBcOn = bcOn;
            Dismissed.Remove("netfail");
        }

        // ------------------------------------------------------------------ cards
        private static void OpenBc() { ResourcePackManager.GoTab(2); }

        private static void Show(string id, int icon, Color col, string title, string text, List<string> details)
        {
            if (Dismissed.Contains(id)) return;
            string act = Tt("Open BetterConnection", "Открыть BetterConnection");
            bool x = BtcHud.Card(id, icon, col, title, text, details, true, act, OpenBc, BtcReplayIcons.Wave);
            if (x) Dismissed.Add(id);
            else if (Opened.Add(id)) BtcHud.ForceOpen(id);   // clickable hint: open it at once so the button is one click away
        }

        /// <summary>Call every OnGUI (also with BetterConnection OFF: reads only local state).</summary>
        internal static void Draw()
        {
            if (Event.current == null) return;
            float now = Time.unscaledTime;
            UNetManager m = null;
            try { m = UNetManager.instance; } catch (Exception) { }
            if (m != null && UNetManager.IsConnected && BtcNetworkGate.Enabled)
            {
                if (NetworkServer.active) DrawHost(m, now);
                else DrawClient(now);
            }
            if (now < _errUntil)
            {
                bool vanillaHost = _errBcOn && _connectedAt > 0f && !_hostInfo && !_hostHeartbeat;
                if (_errBcOn)
                    Show("netfail", BtcReplayIcons.Wave, BtcHud.Warn, Tt("Connection problem", "Проблема с подключением"),
                        Tt("BetterConnection is ON. If this game is vanilla (the host has no BetterConnection), turn it OFF in the BetterConnection tab and join again.", "BetterConnection включён. Если эта игра ванильная (у хоста нет BetterConnection), выключите его во вкладке BetterConnection и зайдите снова.") + (vanillaHost ? Tt(" The host does not answer like a BetterConnection host.", " Хост не отвечает как хост с BetterConnection.") : ""), null);
                else
                    Show("netfail", BtcReplayIcons.Wave, BtcHud.Warn, Tt("Connection problem", "Проблема с подключением"),
                        Tt("BetterConnection is OFF. If the host uses BetterConnection, turn it ON in the BetterConnection tab and join again.", "BetterConnection выключен. Если хост использует BetterConnection, включите его во вкладке BetterConnection и зайдите снова."), null);
            }
        }

        private static void DrawClient(float now)
        {
            if (_connectedAt < 0f) return;
            if (_hostInfo)
            {
                if (_hostVersion != Mine)
                    Show("netver", BtcReplayIcons.Bug, BtcHud.Warn, Tt("Mod versions differ", "Версии мода не совпадают"),
                        Tt("You have " + Mine + ", the host has " + _hostVersion + ". BetterConnection features may not work. Everyone should use the same version (the update card in the main menu updates the mod).", "У вас " + Mine + ", у хоста " + _hostVersion + ". Функции BetterConnection могут не работать. У всех должна быть одна версия (карточка обновления в главном меню обновляет мод)."), null);
                return;
            }
            if (now - _connectedAt < 25f) return;
            if (_hostHeartbeat)
                Show("netver", BtcReplayIcons.Bug, BtcHud.Warn, Tt("Mod versions differ", "Версии мода не совпадают"),
                    Tt("The host runs an older BetterThanCastle without version info (yours is " + Mine + "). Everyone should use the same version.", "У хоста старая версия BetterThanCastle без данных о версии (у вас " + Mine + "). У всех должна быть одна версия."), null);
            else
                Show("netvanilla", BtcReplayIcons.Wave, BtcHud.Warn, Tt("The host looks vanilla", "Хост выглядит ванильным"),
                    Tt("The host does not answer like a BetterConnection host, so the game may break (spawn data, pauses). If the host has no BetterConnection, turn it OFF in the BetterConnection tab and join again.", "Хост не отвечает как хост с BetterConnection, поэтому игра может ломаться (стартовые данные, паузы). Если у хоста нет BetterConnection, выключите его во вкладке BetterConnection и зайдите снова."), null);
        }

        private static void DrawHost(UNetManager m, float now)
        {
            List<string> diff = new List<string>(), vanilla = new List<string>();
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                NetworkConnection c = NetworkServer.connections[i];
                if (c == null || BtcNetManager.IsLocalConnection(c)) continue;
                int id = c.connectionId;
                if (!SrvSeen.ContainsKey(id)) SrvSeen[id] = now;
                string name = BtcNetManager.LobbyPlayerNameFromConnection(m, c);
                string v;
                if (SrvVersion.TryGetValue(id, out v)) { if (v != Mine) diff.Add(name + ": " + v); }
                else if (now - SrvSeen[id] > 25f)
                {
                    if (SrvHeartbeat.Contains(id)) diff.Add(name + ": " + Tt("older version", "старая версия"));
                    else vanilla.Add(name);
                }
            }
            if (diff.Count > 0)
                Show("netver", BtcReplayIcons.Bug, BtcHud.Warn, Tt("Mod versions differ", "Версии мода не совпадают"),
                    Tt("Your version is " + Mine + ". These players have another one; BetterConnection features may not work for them.", "У вас версия " + Mine + ". У этих игроков другая; функции BetterConnection могут у них не работать."), diff);
            if (vanilla.Count > 0)
                Show("netvanilla", BtcReplayIcons.Wave, BtcHud.Warn, Tt("A player has no BetterConnection", "У игрока нет BetterConnection"),
                    Tt("These players do not answer like BetterConnection players (vanilla or BetterConnection off). Their spawn data and pauses may break.", "Эти игроки не отвечают как игроки с BetterConnection (ваниль или BetterConnection выключен). Их стартовые данные и паузы могут ломаться."), vanilla);
        }
    }
}
