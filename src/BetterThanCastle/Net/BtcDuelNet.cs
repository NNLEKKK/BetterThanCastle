using System;
using System.Globalization;
using System.Text;
using Brix.Game;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
    /// <summary>
    /// Online micro duel wire (BetterConnection ON only; OFF = no handlers, no sends). msg 135 host -> every client (opponent + spectators):
    /// duel state as one '|' string at 4 Hz and at once on every change (round state, countdown, winner, timers, kills, arena centre/scale,
    /// next-round presses, result number, player ids/nicks, settings line). msg 136 client -> host: "next" (wants the next round) / "reset"
    /// (gives up the round, a new one starts at once). The host accepts 136 only from the opponent's connection. Units, orders, damage = vanilla.
    /// </summary>
    internal static class BtcDuelNet
    {
        internal const short MsgState = 135, MsgAct = 136;
        private const float Every = 0.25f;
        private static float _next;
        private static readonly StringBuilder _sb = new StringBuilder(256);

        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                if (NetworkServer.active)
                {
                    NetworkServer.UnregisterHandler(MsgAct);
                    NetworkServer.RegisterHandler(MsgAct, OnActAtHost);
                }
                UNetManager m = NetworkManager.singleton as UNetManager;
                NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
                if (c != null && !NetworkServer.active)
                {
                    c.UnregisterHandler(MsgState);
                    c.RegisterHandler(MsgState, OnStateAtClient);
                }
            }
            catch (Exception e) { BtcLog.Error("DuelNet", "RegisterHandlers: " + e); }
        }

        internal static void SendNow() { _next = 0f; }

        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace('|', '/'); }
        private static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        /// <summary>Host, every frame of a PvP duel (BtcDuelMatch.Update).</summary>
        internal static void HostTick()
        {
            if (!BtcNetworkGate.Enabled || !NetworkServer.active) return;
            float now = Time.unscaledTime;
            if (now < _next) return;
            _next = now + Every;
            BtcDuelMatch mt = BtcDuel.Match;
            if (mt == null) return;
            _sb.Length = 0;
            _sb.Append("d1|").Append((int)mt.State).Append('|').Append(F(mt.CountdownLeft)).Append('|').Append(mt.Winner).Append('|').Append(Clean(mt.Reason))
               .Append('|').Append(F(mt.Ctrl[0])).Append('|').Append(F(mt.Ctrl[1])).Append('|').Append(mt.Escaping[0] ? 1 : 0).Append('|').Append(mt.Escaping[1] ? 1 : 0)
               .Append('|').Append(mt.MatchNo).Append('|').Append(F(mt.FightTime)).Append('|').Append(mt.Kills[0]).Append('|').Append(mt.Kills[1])
               .Append('|').Append(BtcDuelMap.Built ? BtcDuelMap.CenterXi : 0).Append('|').Append(BtcDuelMap.Built ? BtcDuelMap.CenterZi : 0).Append('|').Append(BtcDuelMap.Built ? BtcDuelMap.Scale : 0)
               .Append('|').Append(BtcDuel.WantNext[0] ? 1 : 0).Append('|').Append(BtcDuel.WantNext[1] ? 1 : 0).Append('|').Append(mt.ResultSeq)
               .Append('|').Append(Clean(BtcDuel.HostId)).Append('|').Append(Clean(BtcDuel.GuestId)).Append('|').Append(Clean(BtcDuel.HostNick)).Append('|').Append(Clean(BtcDuel.GuestNick))
               .Append('|').Append(Clean(BtcDuel.SetupLine))
               .Append('|').Append(mt.LastWinner).Append('|').Append(Clean(mt.LastReason)).Append('|').Append(F(mt.LastTime)).Append('|').Append(mt.LastK0).Append('|').Append(mt.LastK1);
            StringMessage msg = new StringMessage(_sb.ToString());
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                NetworkConnection c = NetworkServer.connections[i];
                if (c == null || !c.isConnected || BtcNetManager.IsLocalConnection(c) || BtcReplay.IsRec(c)) continue;
                c.Send(MsgState, msg);
            }
        }

        private static int Int(string s) { int v; return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0; }
        private static float Flt(string s) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !float.IsNaN(v) ? v : 0f; }

        private static void OnStateAtClient(NetworkMessage nm)
        {
            try
            {
                string[] a = nm.ReadMessage<StringMessage>().value.Split('|');
                if (a.Length < 29 || a[0] != "d1") return;
                if (SceneManager.GetActiveScene().buildIndex != 2) return;   // still loading the map: the next message comes in 0.25 s
                if (!BtcDuel.Active || !BtcDuel.IsClient)
                {
                    if (BtcDuel.Active) return;   // never mix with a local bot duel
                    BtcDuel.EnterClient(a[19], a[20], a[21], a[22]);
                }
                BtcDuel.SetupLine = a[23];
                BtcDuel.HostNick = a[21]; BtcDuel.GuestNick = a[22];
                int st = Mathf.Clamp(Int(a[1]), 0, (int)BtcDuelState.Over);
                BtcDuel.Match.ApplyState((BtcDuelState)st, Flt(a[2]), Int(a[3]), a[4], Flt(a[5]), Flt(a[6]), a[7] == "1", a[8] == "1", Int(a[9]), Flt(a[10]),
                    Int(a[11]), Int(a[12]), Int(a[13]), Int(a[14]), Mathf.Clamp(Int(a[15]), 0, 3), a[16] == "1", a[17] == "1", Int(a[18]),
                    Int(a[24]), a[25], Flt(a[26]), Int(a[27]), Int(a[28]));
            }
            catch (Exception e) { BtcLog.Error("DuelNet", "state: " + e.Message); }
        }

        /// <summary>Opponent -> host: "next" or "reset".</summary>
        internal static void SendAction(string what)
        {
            if (!BtcNetworkGate.Enabled) return;
            UNetManager m = NetworkManager.singleton as UNetManager;
            if (m == null || m.LocalNetworkClient == null || !m.LocalNetworkClient.isConnected) return;
            m.LocalNetworkClient.Send(MsgAct, new StringMessage(what));
            BtcLog.Info("DuelNet", "action " + what);
        }

        private static void OnActAtHost(NetworkMessage nm)
        {
            try
            {
                string what = nm.ReadMessage<StringMessage>().value;
                BtcDuelMatch mt = BtcDuel.Match;
                if (!BtcDuel.Active || !BtcDuel.Pvp || BtcDuel.IsClient || mt == null) return;
                User u = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(nm.conn) : null;
                if (u == null || u.faction == null || u.faction != mt.Fac[1]) return;   // spectators and strangers cannot steer the duel
                BtcLog.Info("DuelNet", "action from " + BtcNick.Plain(u.ScreenName) + ": " + what);
                if (what == "next") mt.WantNextRound(1);
                else if (what == "reset") mt.Surrender(1);
            }
            catch (Exception e) { BtcLog.Error("DuelNet", "action: " + e.Message); }
        }
    }
}
