using System;
using System.Collections.Generic;
using System.Globalization;
using Brix.Game;
using Brix.Game.Components;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// J4 recovery time (Gameplay > Timer, rules timer.recov + timer.recovMin, BC ON, host decides; needs the advanced timer and a game
    /// length). When the game time is up the host counts the crystals of both teams (home crystals + shards, only games with exactly
    /// 2 teams). Equal = draw at once. Otherwise the trailing team gets the recovery time: if at least once during it they hold MORE
    /// crystals than the leader, the match is a draw when it runs out; if never, the trailing team loses.
    /// Wire (BC ON only): 123 host->clients "phase|leader|trailer|gotMore|result|endT" (phase 0 none 1 recovery 2 over; result 0 none,
    /// 1 draw, 2 leader wins). OFF: no handler, no send, nothing happens.
    /// </summary>
    internal static class BtcRecovery
    {
        internal const short MsgState = 123;
        private static int _iOn = -2, _iMin = -2;
        private static int _phase, _leader = -1, _trailer = -1, _result;
        private static bool _gotMore;
        private static float _endT, _nextPoll, _nextSend;
        private static string _last = "";
        private static readonly Dictionary<int, int> Count = new Dictionary<int, int>();

        internal static void AddRows()
        {
            BtcGameplay.Add("timer.recov", "timer", "Recovery time after the game time", "Recovery time после игрового времени", 0f, 0f, 1f, 1f, true,
                "Needs the advanced timer and a game length, 2 teams only. When the game time is up, equal crystals = draw. Otherwise the team with fewer crystals (home crystals + shards) gets the recovery time: if at least once during it they hold more crystals than the leader, it is a draw when the time runs out; if never, they lose.",
                "Нужны продвинутый таймер и длительность игры, только 2 команды. Когда игровое время вышло, кристаллов поровну = ничья. Иначе команда с меньшим числом кристаллов (домашние + осколки) получает recovery time: если хотя бы раз за него у неё было больше кристаллов, чем у лидера, по окончании времени ничья; если ни разу, она проигрывает.");
            BtcGameplay.Add("timer.recovMin", "timer", "Recovery time (minutes)", "Recovery time (минуты)", 10f, 1f, 60f, 1f, false,
                "How long the trailing team has to get ahead at least once.", "Сколько времени у отстающей команды, чтобы хотя бы раз выйти вперёд.");
        }

        private static float Val(ref int idx, string id, float def)
        {
            if (idx == -2) idx = BtcGameplay.Idx(id);
            float[] a = BtcGameplay.Active;
            return idx >= 0 && a != null && idx < a.Length ? a[idx] : def;
        }

        private static bool On { get { return BtcNetworkGate.Enabled && Val(ref _iOn, "timer.recov", 0f) > 0.5f; } }

        internal static void Reset() { _phase = 0; _leader = _trailer = -1; _result = 0; _gotMore = false; _last = ""; }

        // ------------------------------------------------------------------ host
        /// <summary>BtcTimer.Tick (every frame).</summary>
        internal static void Tick()
        {
            if (!NetworkServer.active || !On) return;
            if (BtcTimer.Cur == BtcTimer.Phase.None) { if (_phase != 0) Reset(); return; }
            if (BtcTimer.Cur != BtcTimer.Phase.Running || BtcTimer.LimitSec <= 0f || _phase == 2) { SendIfDue(); return; }
            float now = Time.unscaledTime;
            if (now < _nextPoll) { SendIfDue(); return; }
            _nextPoll = now + 0.5f;
            try
            {
                float t = BtcTimer.Elapsed;
                if (_phase == 0)
                {
                    if (t < BtcTimer.LimitSec) return;
                    if (!CountTeams()) { _phase = 2; _result = 0; BtcLog.Info("Recovery", "skipped: not exactly 2 teams"); Send(); return; }
                    int a = -1, b = -1;
                    foreach (int k in Count.Keys) { if (a < 0) a = k; else b = k; }
                    int ca = Count[a], cb = Count[b];
                    if (ca == cb) { _phase = 2; _result = 1; _leader = a; _trailer = b; BtcLog.Info("Recovery", "equal crystals " + ca + ":" + cb + " -> draw"); Send(); return; }
                    _leader = ca > cb ? a : b; _trailer = ca > cb ? b : a;
                    _phase = 1; _gotMore = false;
                    _endT = BtcTimer.LimitSec + Val(ref _iMin, "timer.recovMin", 10f) * 60f;
                    BtcLog.Info("Recovery", "start: leader team " + _leader + " (" + Mathf.Max(ca, cb) + ") trailer team " + _trailer + " (" + Mathf.Min(ca, cb) + "), ends at " + _endT);
                    Send();
                    return;
                }
                // recovery running
                if (!_gotMore && CountTeams() && Get(_trailer) > Get(_leader)) { _gotMore = true; BtcLog.Info("Recovery", "trailer got ahead " + Get(_trailer) + ":" + Get(_leader)); Send(); }
                if (t >= _endT)
                {
                    _phase = 2; _result = _gotMore ? 1 : 2;
                    BtcLog.Info("Recovery", "over: " + (_gotMore ? "draw" : "leader team " + _leader + " wins"));
                    Send();
                }
                else SendIfDue();
            }
            catch (Exception e) { BtcLog.Error("Recovery", "Tick: " + e.Message); }
        }

        private static int Get(int team) { int c; return Count.TryGetValue(team, out c) ? c : 0; }

        // crystals per team of every player faction that owns any crystal; true when exactly 2 teams are in the game
        private static bool CountTeams()
        {
            Count.Clear();
            User[] users = BtcRegistry.UsersArray();
            for (int i = 0; i < users.Length; i++)
                if (users[i] != null && users[i].faction != null && !Count.ContainsKey(users[i].TeamNumber) && HasNest(users[i].faction)) Count[users[i].TeamNumber] = 0;
            List<FireflyNest> nests = BtcMatchStats.NestList();
            if (nests == null) return false;
            for (int i = 0; i < nests.Count; i++)
            {
                FireflyNest n = nests[i];
                AffiliationComponent aff = n != null ? n.GetComponent<AffiliationComponent>() : null;
                Faction f = aff != null ? aff.faction : null;
                User o = f != null ? f.Owner : null;
                if (o == null || !Count.ContainsKey(o.TeamNumber)) continue;
                Count[o.TeamNumber]++;
            }
            return Count.Count == 2;
        }

        private static bool HasNest(Faction f)
        {
            List<FireflyNest> nests = BtcMatchStats.NestList();
            if (nests == null) return false;
            for (int i = 0; i < nests.Count; i++)
            {
                AffiliationComponent aff = nests[i] != null ? nests[i].GetComponent<AffiliationComponent>() : null;
                if (aff != null && aff.faction == f) return true;
            }
            // a team with no crystal left still plays (it lost them); count it through its owner's team
            return f.Owner != null;
        }

        private static string Encode()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return _phase + "|" + _leader + "|" + _trailer + "|" + (_gotMore ? 1 : 0) + "|" + _result + "|" + _endT.ToString("F1", c);
        }

        private static void SendIfDue()
        {
            if (_phase != 0 && Time.unscaledTime >= _nextSend) Send();
        }

        private static void Send()
        {
            _nextSend = Time.unscaledTime + 3f;
            if (!NetworkServer.active || !BtcNetworkGate.Enabled) return;
            try
            {
                StringMessage msg = new StringMessage(Encode());
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection cn = NetworkServer.connections[i];
                    if (cn != null && cn.isConnected && !BtcNetManager.IsLocalConnection(cn)) cn.Send(MsgState, msg);
                }
            }
            catch (Exception e) { BtcLog.Error("Recovery", "Send: " + e.Message); }
        }

        // ------------------------------------------------------------------ clients
        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            Reset();
            if (!BtcNetworkGate.Enabled || NetworkServer.active) return;
            try
            {
                UNetManager m = NetworkManager.singleton as UNetManager;
                NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
                if (c == null) return;
                c.UnregisterHandler(MsgState);
                c.RegisterHandler(MsgState, OnState);
            }
            catch (Exception e) { BtcLog.Error("Recovery", "RegisterHandlers: " + e); }
        }

        private static void OnState(NetworkMessage nm)
        {
            try
            {
                string[] p = nm.ReadMessage<StringMessage>().value.Split('|');
                if (p.Length < 6) return;
                int.TryParse(p[0], out _phase); int.TryParse(p[1], out _leader); int.TryParse(p[2], out _trailer);
                _gotMore = p[3] == "1"; int.TryParse(p[4], out _result);
                float.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out _endT);
            }
            catch (Exception e) { BtcLog.Error("Recovery", "OnState: " + e.Message); }
        }

        // ------------------------------------------------------------------ look (BtcTimer.OnGUI)
        private static string TeamName(int t) { return BtcGameplay.T("Team ", "Команда ") + (char)('A' + Mathf.Clamp(t, 0, 25)); }

        internal static void Draw()
        {
            if (_phase == 0 || BtcReplay.Mode == BtcReplay.State.Playing) return;
            int mine = User.LocalUser != null ? User.LocalUser.TeamNumber : -99;
            if (_phase == 1)
            {
                string left = BtcTimer.Clock(Mathf.Max(0f, _endT - BtcTimer.Elapsed));
                string text;
                if (mine == _trailer)
                    text = _gotMore ? BtcGameplay.T("You were ahead once: hold on until the end for a draw", "Вы уже были впереди: дождитесь конца, будет ничья")
                                    : BtcGameplay.T("Get more crystals than the enemy at least once, or you lose", "Хотя бы раз наберите больше кристаллов, чем у врага, иначе поражение");
                else if (mine == _leader)
                    text = _gotMore ? BtcGameplay.T("The enemy was ahead once: it ends in a draw", "Враг уже был впереди: будет ничья")
                                    : BtcGameplay.T("Do not let the enemy get more crystals than you", "Не дайте врагу набрать больше кристаллов, чем у вас");
                else text = TeamName(_trailer) + BtcGameplay.T(" is recovering", " отыгрывается") + (_gotMore ? BtcGameplay.T(" (was ahead once)", " (уже был впереди)") : "");
                BtcHud.Card("recovery", BtcReplayIcons.Refresh, _gotMore ? BtcHud.Ok : BtcHud.Warn, "Recovery time " + left, text, null, false);
                return;
            }
            if (_result == 0) return;
            string title, sub;
            Color col;
            if (_result == 1) { title = BtcGameplay.T("Draw", "Ничья"); sub = BtcGameplay.T("Recovery time decided the match", "Матч решён recovery time"); col = BtcHud.Warn; }
            else if (mine == _leader) { title = BtcGameplay.T("Victory", "Победа"); sub = BtcGameplay.T("The enemy did not get ahead during the recovery time", "Враг ни разу не вышел вперёд за recovery time"); col = BtcHud.Ok; }
            else if (mine == _trailer) { title = BtcGameplay.T("Defeat", "Поражение"); sub = BtcGameplay.T("You never got more crystals during the recovery time", "Вы ни разу не набрали больше кристаллов за recovery time"); col = BtcHud.Danger; }
            else { title = TeamName(_leader) + BtcGameplay.T(" wins", " побеждает"); sub = BtcGameplay.T("Recovery time is over", "Recovery time закончилось"); col = BtcHud.Ok; }
            BtcHud.Card("recovresult", BtcReplayIcons.Star, col, title, sub, null, true);
        }
    }
}
