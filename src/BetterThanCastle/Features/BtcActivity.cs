using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.Game;
using Brix.Input;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
    /// <summary>
    /// What the local player is doing right now, in one place: the Online tab (own card + presence beacon), the card colours and the
    /// Discord status all read this snapshot. Refreshed once per second from ResourcePackManager.Update (cheap, no allocations per frame).
    /// Kinds are ordered by "how interesting": the first matching rule wins (Refresh).
    /// </summary>
    internal static class BtcActivity
    {
        internal enum Kind { Menu = 0, Lobby, Loading, Rejoin, Match, Spectate, Paused, Result, Duel, Aim, Replay, Editor, Afk }

        internal sealed class Snap
        {
            internal Kind Kind;
            internal string Map = "", Mode = "", Detail = "";
            internal bool Host, Multiplayer;
            internal int Players, Max;
            internal int Seconds = -1;        // match clock, -1 = none
            internal int Result;              // +1 won, -1 lost
            internal string Versus = "";      // other player names in the match / lobby
        }

        internal static readonly Snap Now = new Snap();
        private static float _next, _lastInput, _matchStart = -1f;
        private static Vector3 _lastMouse;
        private const float AfkAfter = 300f;

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        /// <summary>Colour of a status (card stripe, dot, Online tab filter chips).</summary>
        internal static Color ColorOf(Kind k)
        {
            switch (k)
            {
                case Kind.Lobby: return new Color(0.45f, 0.85f, 0.45f, 1f);
                case Kind.Loading: case Kind.Rejoin: return new Color(0.35f, 0.8f, 0.8f, 1f);
                case Kind.Match: case Kind.Paused: case Kind.Result: return new Color(0.95f, 0.6f, 0.25f, 1f);
                case Kind.Spectate: return new Color(0.85f, 0.75f, 0.4f, 1f);
                case Kind.Duel: case Kind.Aim: return new Color(0.7f, 0.5f, 0.95f, 1f);
                case Kind.Replay: return new Color(0.4f, 0.6f, 1f, 1f);
                case Kind.Editor: return new Color(0.75f, 0.55f, 0.35f, 1f);
                case Kind.Afk: return new Color(0.5f, 0.5f, 0.5f, 1f);
            }
            return new Color(0.68f, 0.7f, 0.74f, 1f);
        }

        internal static int IconOf(Kind k)
        {
            switch (k)
            {
                case Kind.Lobby: return BtcReplayIcons.Group;
                case Kind.Loading: case Kind.Rejoin: return BtcReplayIcons.Refresh;
                case Kind.Match: case Kind.Result: return BtcReplayIcons.Swords;
                case Kind.Paused: return BtcReplayIcons.Pause;
                case Kind.Spectate: return BtcReplayIcons.Eye;
                case Kind.Duel: return BtcReplayIcons.Swords;
                case Kind.Aim: return BtcReplayIcons.Target;
                case Kind.Replay: return BtcReplayIcons.Play;
                case Kind.Editor: return BtcReplayIcons.Blueprint;
                case Kind.Afk: return BtcReplayIcons.Slow;
            }
            return BtcReplayIcons.Monitor;
        }

        /// <summary>Short status word (filters, Discord small text).</summary>
        internal static string Name(Kind k)
        {
            switch (k)
            {
                case Kind.Lobby: return Tt("In a lobby", "В лобби");
                case Kind.Loading: return Tt("Loading", "Загрузка");
                case Kind.Rejoin: return Tt("Reconnecting", "Переподключение");
                case Kind.Match: return Tt("In a match", "В матче");
                case Kind.Spectate: return Tt("Spectating", "Наблюдает");
                case Kind.Paused: return Tt("Paused", "Пауза");
                case Kind.Result: return Tt("Match over", "Матч окончен");
                case Kind.Duel: return Tt("Micro duel", "Микродуэль");
                case Kind.Aim: return Tt("Aim training", "Аим-тренировка");
                case Kind.Replay: return Tt("Watching a replay", "Смотрит реплей");
                case Kind.Editor: return Tt("Map editor", "Редактор карт");
                case Kind.Afk: return Tt("Away", "Отошёл");
            }
            return Tt("In the menu", "В меню");
        }

        /// <summary>Two-line text of any snapshot (own card, other players' cards: they send the same fields).</summary>
        internal static void Lines(Kind k, string map, string mode, string detail, bool host, int players, int max, int secs, string versus, out string l1, out string l2)
        {
            string clock = secs >= 0 ? Clock(secs) : "";
            string mm = Join(" · ", mode, map);
            l1 = Name(k); l2 = "";
            switch (k)
            {
                case Kind.Lobby:
                    l1 = host ? Tt("Hosting a lobby", "Хостит лобби") : Tt("In a lobby", "В лобби") + (string.IsNullOrEmpty(detail) ? "" : " · " + detail);
                    l2 = Join(" · ", mm, max > 0 ? players + "/" + max : "", versus);
                    break;
                case Kind.Loading: l1 = Tt("Loading", "Загружает") + (string.IsNullOrEmpty(map) ? "" : " " + map); l2 = Join(" · ", detail, mode); break;
                case Kind.Rejoin: l1 = Tt("Reconnecting to the match", "Переподключается к матчу"); l2 = detail; break;
                case Kind.Match: case Kind.Paused: case Kind.Spectate:
                    l1 = (k == Kind.Paused ? Tt("Paused", "Пауза") + " · " : k == Kind.Spectate ? Tt("Spectating", "Наблюдает") + " · " : "")
                        + (string.IsNullOrEmpty(mm) ? Tt("In a match", "В матче") : mm);
                    l2 = Join(" · ", clock, detail, string.IsNullOrEmpty(versus) ? "" : "vs " + versus);
                    break;
                case Kind.Result: l1 = (detail == "won" ? Tt("Won", "Победа") : Tt("Lost", "Поражение")) + (string.IsNullOrEmpty(mm) ? "" : " · " + mm); l2 = Join(" · ", clock, versus); break;
                case Kind.Duel:
                    l1 = string.IsNullOrEmpty(versus) ? Tt("Micro duel vs bot", "Микродуэль с ботом") : Tt("Micro duel vs ", "Микродуэль против ") + versus;
                    l2 = detail; break;
                case Kind.Aim: l1 = Tt("Aim training", "Аим-тренировка"); l2 = detail; break;
                case Kind.Replay: l1 = Tt("Watching a replay", "Смотрит реплей"); l2 = Join(" · ", map, detail); break;
                case Kind.Editor: l1 = Tt("Map editor", "Редактор карт"); l2 = map; break;
                case Kind.Afk: l1 = Tt("Away", "Отошёл"); l2 = detail; break;
                default: l1 = Tt("In the main menu", "В главном меню"); l2 = detail; break;
            }
        }

        internal static string Clock(int s) { return s >= 3600 ? (s / 3600) + ":" + (s / 60 % 60).ToString("00") + ":" + (s % 60).ToString("00") : (s / 60) + ":" + (s % 60).ToString("00"); }

        private static string Join(string sep, params string[] parts)
        {
            string r = "";
            for (int i = 0; i < parts.Length; i++) if (!string.IsNullOrEmpty(parts[i])) r = r.Length == 0 ? parts[i] : r + sep + parts[i];
            return r;
        }

        /// <summary>Per frame (ResourcePackManager.Update): input watch for AFK, snapshot once per second.</summary>
        internal static void Tick()
        {
            float now = Time.unscaledTime;
            Vector3 mp = Input.mousePosition;
            if (Input.anyKey || (mp - _lastMouse).sqrMagnitude > 4f || Input.mouseScrollDelta.y != 0f) _lastInput = now;
            _lastMouse = mp;
            if (now < _next) return;
            _next = now + 1f;
            try { Refresh(now); } catch (Exception e) { _next = now + 10f; BtcLog.Warn("Activity", "refresh: " + e.Message); }
        }

        private static void Refresh(float now)
        {
            Snap s = Now;
            s.Map = ""; s.Mode = ""; s.Detail = ""; s.Versus = ""; s.Players = 0; s.Max = 0; s.Seconds = -1; s.Result = 0;
            UNetManager m = null;
            try { m = UNetManager.instance; } catch (Exception) { }
            s.Multiplayer = Neo.multiplayer && m != null && (NetworkServer.active || NetworkClient.active);
            s.Host = NetworkServer.active;
            try
            {
                if (GameParam.instance != null)
                {
                    if (GameParam.Map != null) s.Map = GameParam.Map.displayName ?? "";
                    if (GameParam.Gamemode != null) s.Mode = GameParam.Gamemode.displayName ?? "";
                }
            }
            catch (Exception) { }
            Scene sc = SceneManager.GetActiveScene();
            bool inGame = sc.buildIndex == 2;
            if (!inGame) _matchStart = -1f;
            if (s.Multiplayer) Players(m, s, inGame);

            if (BtcNetManager.Rejoining) { s.Kind = Kind.Rejoin; return; }
            if (inGame && BtcReplayBoost.IsReplay()) { s.Kind = Kind.Replay; s.Detail = ReplayLine(); return; }
            if (BtcDuel.Active) { s.Kind = Kind.Duel; s.Detail = DuelLine(); return; }
            if (BtcAim.Active) { s.Kind = Kind.Aim; s.Detail = AimLine(); return; }
            bool loading = inGame ? !BtcDiscord.LevelReadyPub : (m != null && s.Multiplayer && BtcBc.IsLoadingLevel(m));
            if (loading || sc.name.ToLowerInvariant().Contains("load"))
            {
                s.Kind = Kind.Loading;
                int p = BtcDiscord.LoadPercentPub;
                s.Detail = p >= 0 ? p + "%" : "";
                return;
            }
            if (inGame)
            {
                if (_matchStart < 0f) _matchStart = Time.realtimeSinceStartup;
                s.Seconds = (int)(Time.realtimeSinceStartup - _matchStart);
                float adv; string phase;
                if (BtcTimer.DiscordInfo(out adv, out phase)) { if (adv >= 0f) s.Seconds = (int)adv; if (phase != null) s.Detail = phase; }
                bool editor = false, spec = false;
                try { editor = InputModeController.DefaultMode == InputMode.worldEditor; } catch (Exception) { }
                try { Faction f = User.LocalUser != null ? User.LocalUser.faction : null; spec = f != null && f.IsSpectator; } catch (Exception) { }
                int res = BtcDiscord.ResultPub;
                if (editor) s.Kind = Kind.Editor;
                else if (res != 0) { s.Kind = Kind.Result; s.Result = res; s.Detail = res > 0 ? "won" : "lost"; }
                else if (now - _lastInput > AfkAfter) { s.Kind = Kind.Afk; s.Detail = Join(" · ", s.Mode, s.Map); }
                else if (Time.timeScale <= 0.0001f) s.Kind = Kind.Paused;
                else if (spec) s.Kind = Kind.Spectate;
                else
                {
                    s.Kind = Kind.Match;
                    if (!s.Multiplayer) s.Detail = Join(" · ", s.Detail, Tt("single player", "одиночная игра"));
                }
                return;
            }
            if (s.Multiplayer && BtcGameplay.InLobby)
            {
                s.Kind = Kind.Lobby;
                if (!s.Host) s.Detail = HostName(m);
                return;
            }
            s.Map = ""; s.Mode = "";
            s.Kind = now - _lastInput > AfkAfter ? Kind.Afk : Kind.Menu;
        }

        private static readonly List<string> _names = new List<string>();

        private static void Players(UNetManager m, Snap s, bool inGame)
        {
            _names.Clear();
            int n = 0;
            string me = "";
            try { me = BtcNick.Plain(m.LocalPlayerName ?? ""); } catch (Exception) { }
            try
            {
                List<LobbyPlayer> list = m.LobbyPlayerList;
                if (list != null && list.Count > 0)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        LobbyPlayer p = list[i];
                        if (p == null) continue;
                        n++;
                        if (!p.isLocalPlayer) _names.Add(BtcNick.Plain(p.NetworkplayerName));
                    }
                }
                else if (inGame)
                {
                    foreach (User u in BtcRegistry.UsersArray())
                    {
                        if (u == null || u.Network_screenName == "REC") continue;
                        try { if (BtcReplay.IsRec(u.connectionToClient)) continue; } catch (Exception) { }
                        n++;
                        string nm = BtcNick.Plain(u.ScreenName);
                        if (u != User.LocalUser && nm != me) _names.Add(nm);
                    }
                }
            }
            catch (Exception) { }
            s.Players = n;
            s.Max = m.maxConnections;
            string v = "";
            for (int i = 0; i < _names.Count && i < 4; i++) v = v.Length == 0 ? _names[i] : v + ", " + _names[i];
            if (_names.Count > 4) v += " +" + (_names.Count - 4);
            s.Versus = v;
        }

        private static string HostName(UNetManager m)
        {
            try
            {
                List<LobbyPlayer> list = m.LobbyPlayerList;
                for (int i = 0; list != null && i < list.Count; i++) if (list[i] != null && list[i].NetworkisHost) return BtcNick.Plain(list[i].NetworkplayerName);
            }
            catch (Exception) { }
            return "";
        }

        private static string DuelLine()
        {
            if (BtcDuel.Pvp)
            {
                string pl = BtcDuel.SetupLine;
                BtcDuelMatch pm = BtcDuel.Match;
                if (pm != null && pm.MatchNo > 0) pl += " · " + Tt("round ", "раунд ") + pm.MatchNo;
                if (BtcDuel.LocalSide >= 0) { int w, l; BtcDuelPvp.Session(out w, out l); pl += " · " + Tt("score ", "счёт ") + w + ":" + l; }
                else pl = BtcDuel.HostNick + " vs " + BtcDuel.GuestNick + " · " + pl;
                return pl;
            }
            string diff = BtcDuelConfig.Difficulty.ToString();
            int a = BtcDuelConfig.Total(BtcDuelConfig.PlayerCount), b = BtcDuelConfig.Total(BtcDuelConfig.BotCount);
            string line = diff + " · " + a + "v" + b;
            BtcDuelMatch mt = BtcDuel.Match;
            if (mt != null && mt.MatchNo > 0) line += " · " + Tt("match ", "матч ") + mt.MatchNo;
            line += " · " + Tt("score ", "счёт ") + BtcDuel.Wins + ":" + BtcDuel.Losses;
            return line;
        }

        private static string ReplayLine()
        {
            try
            {
                string t = Clock((int)BtcReplay.PlayClock);
                if (BtcReplay.PlayTotal > 0.5f) t += " / " + Clock((int)BtcReplay.PlayTotal);
                return (BtcReplay.PlayPaused ? Tt("paused", "пауза") + " · " : "") + t;
            }
            catch (Exception) { return ""; }
        }

        private static string AimLine()
        {
            string line = BtcAimConfig.Game + " · " + BtcAimConfig.Move + " · " + BtcAimConfig.Targets + Tt(" targets", " целей");
            BtcAim run = BtcAim.Run;
            if (run != null) line += " · " + Tt("hits ", "попаданий ") + run.Hits;
            return line;
        }

        /// <summary>The players list names of the current game (Online tab: marks everyone met in a lobby or match).</summary>
        internal static void CollectNames(List<string> into)
        {
            into.Clear();
            try
            {
                UNetManager m = UNetManager.instance;
                if (m == null || !(NetworkServer.active || NetworkClient.active)) return;
                for (int i = 0; i < _names.Count; i++) into.Add(_names[i]);
            }
            catch (Exception) { }
        }
    }
}
