using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using BepInEx;
using Brix.Engine;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
    /// <summary>
    /// Online micro duel (player vs player), BetterConnection ON only. Set-up after an accepted challenge (BtcPresence.Duel):
    /// the challenger hosts a vanilla multiplayer lobby on the duel map (sandbox), tells the opponent the port, both lobbies press ready by
    /// themselves and the match loads; from then on BtcDuelMatch runs the rules on the host and mirrors them to the opponent and to
    /// spectators (BtcDuelNet msgs 135/136). Also keeps the score against every opponent: session, all time, a two-player Elo
    /// (configs/duel-pvp.txt) and one compact line per round in duel-logs/pvp-history.txt.
    /// </summary>
    internal static class BtcDuelPvp
    {
        private enum Stage { None = 0, HostStart, HostLobby, HostWaitGuest, HostWaitReady, GuestWaitGo, GuestJoin, GuestLobby, Loading }
        private static Stage _st;
        private static float _stAt, _goAt, _leaveAt;
        private static string _oppId, _oppNick;
        private static IPEndPoint _oppSrc;
        private static int _port;

        internal static bool Busy { get { return _st != Stage.None; } }
        internal static bool HostingLobby { get { return _st == Stage.HostLobby || _st == Stage.HostWaitGuest || _st == Stage.HostWaitReady; } }

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static void Go(Stage s) { _st = s; _stAt = Time.unscaledTime; BtcLog.Info("DuelPvp", "stage " + s); }

        // ================================================================== set-up
        /// <summary>Challenger: the opponent accepted. Starts the duel lobby (on the next ticks).</summary>
        internal static bool HostBegin(string oppId, string oppNick, IPEndPoint oppSrc)
        {
            if (Busy) return false;
            if (!BtcDuelVoid.Ensure()) { BtcLog.Warn("DuelPvp", "duel map missing"); return false; }
            _oppId = oppId; _oppNick = oppNick; _oppSrc = oppSrc;
            _leaveAt = Time.unscaledTime;
            if (NetworkServer.active || NetworkClient.active)
            {
                try { UNetManager.instance.Disconnect(); } catch (Exception e) { BtcLog.Warn("DuelPvp", "leave lobby: " + e.Message); }
                _leaveAt = Time.unscaledTime + 1.5f;
            }
            Go(Stage.HostStart);
            return true;
        }

        internal static void GuestAccepted(string hostId, string hostNick)
        {
            _oppId = hostId; _oppNick = hostNick; _oppSrc = null;
            Go(Stage.GuestWaitGo);
        }

        internal static bool ExpectsGo(string id) { return _st == Stage.GuestWaitGo && id == _oppId; }

        internal static void GuestJoining() { Go(Stage.GuestJoin); }

        internal static void PeerDeclined(string id)
        {
            if (_st == Stage.None || id != _oppId) return;
            Abort(Tt(_oppNick + " cancelled the duel", _oppNick + " отменил дуэль"), false);
        }

        internal static void Abort(string why, bool tellPeer)
        {
            BtcLog.Warn("DuelPvp", "abort at " + _st + ": " + why);
            bool hosting = _st == Stage.HostLobby || _st == Stage.HostWaitGuest || _st == Stage.HostWaitReady;
            bool guest = _st == Stage.GuestJoin || _st == Stage.GuestLobby;
            if (tellPeer && _oppSrc != null) BtcPresence.SendDuelCancel(_oppSrc);
            _st = Stage.None;
            if (hosting || guest) { try { if (NetworkServer.active || NetworkClient.active) UNetManager.instance.Disconnect(); } catch (Exception) { } }
            BtcSfx.Warn();
            BtcHud.Notice(why, BtcReplayIcons.Cross, BtcHud.Warn);
        }

        /// <summary>Every frame (BtcPresence.Tick). Only a few checks per stage.</summary>
        internal static void Tick(float now)
        {
            if (_st == Stage.None) return;
            float age = now - _stAt;
            UNetManager m = UNetManager.instance;
            switch (_st)
            {
                case Stage.HostStart:
                    if (now < _leaveAt || NetworkServer.active || NetworkClient.active) { if (age > 10f) Abort(Tt("The old lobby did not close", "Старое лобби не закрылось"), true); return; }
                    if (m == null) return;
                    _port = UNetManager.HostingPort > 0 ? UNetManager.HostingPort : 3000;
                    if (string.IsNullOrEmpty(m.LocalPlayerName)) m.LocalPlayerName = Neo.multiplayerName;
                    m.maxConnections = UNetManager.HostingMaxConnections;
                    float t0 = Time.realtimeSinceStartup;
                    BtcLog.Info("DuelPvp", "host start after " + age.ToString("0.0") + " s");
                    NetworkClient c = m.StartHostMultiplayer(UNetManager.HostingIP, _port);
                    BtcLog.Info("DuelPvp", "host started client=" + (c != null) + " server=" + NetworkServer.active + " port=" + _port + " call " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s");
                    if (c == null) { Abort(Tt("Could not host the duel (port " + _port + " busy?)", "Не удалось создать дуэль (порт " + _port + " занят?)"), true); return; }
                    Go(Stage.HostLobby);
                    return;
                case Stage.HostLobby:
                    if (m == null || m.LocalLobbyPlayer == null) { if (age > 30f) Abort(Tt("The duel lobby did not open", "Лобби дуэли не открылось"), true); return; }
                    if (!BtcDuelLaunch.PickSandbox(BtcDuelVoid.Codename)) { Abort(Tt("The duel map could not be selected", "Не удалось выбрать карту дуэли"), true); return; }
                    try { GameParam.UpdateParameters(); } catch (Exception e) { BtcLog.Warn("DuelPvp", "UpdateParameters: " + e.Message); }
                    BtcPresence.SendDuelGo(_oppSrc, _port);
                    _goAt = now;
                    Go(Stage.HostWaitGuest);
                    return;
                case Stage.HostWaitGuest:
                    if (!NetworkServer.active) { Abort(Tt("The duel lobby closed", "Лобби дуэли закрылось"), true); return; }
                    if (Guest(m) != null)
                    {
                        LobbyPlayer me = m.LocalLobbyPlayer;
                        if (me != null) { me.NetworkIsHostReady = true; m.AbortUploads(); m.SendMapToClients(); }
                        Go(Stage.HostWaitReady);
                        return;
                    }
                    if (now - _goAt > 3f) { _goAt = now; BtcPresence.SendDuelGo(_oppSrc, _port); }   // the first copies may be lost
                    if (age > 45f) Abort(Tt(_oppNick + " did not come to the duel", _oppNick + " не зашёл в дуэль"), true);
                    return;
                case Stage.HostWaitReady:
                    {
                        if (!NetworkServer.active) { Abort(Tt("The duel lobby closed", "Лобби дуэли закрылось"), true); return; }
                        LobbyPlayer g = Guest(m);
                        if (g == null) { Abort(Tt(_oppNick + " left the duel lobby", _oppNick + " вышел из лобби дуэли"), false); return; }
                        if (!g.readyToBegin) { if (age > 120f) Abort(Tt(_oppNick + " is not ready (map download?)", _oppNick + " не готов (загрузка карты?)"), true); return; }
                        BtcDuel.BeginPvpHost(_oppId, _oppNick);
                        m.LocalLobbyPlayer.SendReadyToBeginMessage();
                        BtcLog.Info("DuelPvp", "start pressed vs " + _oppNick);
                        Go(Stage.Loading);
                        return;
                    }
                case Stage.GuestWaitGo:
                    if (age > 40f) Abort(Tt("The duel host did not answer", "Хост дуэли не ответил"), true);
                    return;
                case Stage.GuestJoin:
                    if (m != null && m.LocalLobbyPlayer != null) { Go(Stage.GuestLobby); return; }
                    if (age > 30f) Abort(Tt("Could not join the duel", "Не удалось зайти в дуэль"), true);
                    return;
                case Stage.GuestLobby:
                    {
                        if (!NetworkClient.active || m == null || m.LocalLobbyPlayer == null) { Abort(Tt("The duel lobby closed", "Лобби дуэли закрылось"), false); return; }
                        bool hostReady = false;
                        foreach (LobbyPlayer p in m.LobbyPlayerList) if (p != null && p.isHost && p.IsHostReady) hostReady = true;
                        if (!hostReady || GameParam.Map == null || m.LocalLobbyPlayer.downloadProgression != -1f) { if (age > 120f) Abort(Tt("The duel did not start", "Дуэль не началась"), true); return; }
                        if (!m.LocalLobbyPlayer.readyToBegin) m.LocalLobbyPlayer.SendReadyToBeginMessage();
                        BtcDuel.ExpectPvpGuest(_oppId, _oppNick);
                        BtcLog.Info("DuelPvp", "guest ready sent");
                        Go(Stage.Loading);
                        return;
                    }
                case Stage.Loading:
                    if (SceneManager.GetActiveScene().buildIndex == 2) { _st = Stage.None; return; }
                    if (!NetworkServer.active && !NetworkClient.active) { _st = Stage.None; BtcDuel.CancelExpect(); return; }
                    if (age > 240f) { _st = Stage.None; BtcDuel.CancelExpect(); }
                    return;
            }
        }

        private static LobbyPlayer Guest(UNetManager m)
        {
            if (m == null || m.LobbyPlayerList == null) return null;
            foreach (LobbyPlayer p in m.LobbyPlayerList) if (p != null && !p.isLocalPlayer && !p.isHost) return p;
            return null;
        }

        /// <summary>Waiting card while a duel is being set up (any scene but the match).</summary>
        internal static void DrawHud()
        {
            if (_st == Stage.None || _st == Stage.Loading) return;
            if (SceneManager.GetActiveScene().buildIndex == 2) return;
            string t = _st == Stage.GuestWaitGo || _st == Stage.GuestJoin || _st == Stage.GuestLobby
                ? Tt("Joining the duel of ", "Заходим в дуэль к ") + _oppNick + "..."
                : Tt("Waiting for ", "Ждём ") + _oppNick + "...";
            BtcHud.Card("btcduelsetup", BtcReplayIcons.Swords, BtcHud.Ok, Tt("Micro duel", "Микродуэль"), t, null, false, Tt("Cancel", "Отмена"),
                delegate { Abort(Tt("Duel cancelled", "Дуэль отменена"), true); });
            BtcHud.ForceOpen("btcduelsetup");
        }

        /// <summary>The challenger's settings in one line (invite card, HUD).</summary>
        internal static string SettingsLine()
        {
            string a = Units(BtcDuelConfig.PlayerCount), b = Units(BtcDuelConfig.BotCount);
            return a + " vs " + b + " · " + Tt("arena x", "арена x") + BtcDuelConfig.ArenaScale +
                   " · " + Tt("skips ", "скипы ") + (BtcDuelConfig.SkipAnims ? Tt("on", "вкл") : Tt("off", "выкл")) +
                   " · " + Tt("heal ", "лечение ") + (BtcDuelConfig.InfiniteHeal ? Tt("on", "вкл") : Tt("off", "выкл"));
        }

        internal static string Units(int[] c)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < c.Length; i++) if (c[i] > 0) { if (sb.Length > 0) sb.Append('+'); sb.Append(c[i]).Append(' ').Append(BtcDuelConfig.UnitNames[i]); }
            return sb.Length == 0 ? "-" : sb.ToString();
        }

        // ================================================================== score + Elo
        private sealed class Rec { internal string Id = "", Nick = ""; internal int W, L, D; internal float Me = 1500f, Them = 1500f; }
        private static Dictionary<string, Rec> _recs;
        private static readonly Dictionary<string, int[]> _session = new Dictionary<string, int[]>();
        private const float EloK = 32f;

        private static string RecFile { get { return BtcPaths.Config("duel-pvp.txt"); } }

        private static void Load()
        {
            if (_recs != null) return;
            _recs = new Dictionary<string, Rec>();
            try
            {
                if (!File.Exists(RecFile)) return;
                foreach (string line in File.ReadAllLines(RecFile, Encoding.UTF8))
                {
                    string[] f = line.Split('\t');
                    if (f.Length < 7 || f[0].Length == 0) continue;
                    Rec r = new Rec { Id = f[0], Nick = f[1] };
                    int.TryParse(f[2], out r.W); int.TryParse(f[3], out r.L); int.TryParse(f[4], out r.D);
                    float.TryParse(f[5], NumberStyles.Float, CultureInfo.InvariantCulture, out r.Me);
                    float.TryParse(f[6], NumberStyles.Float, CultureInfo.InvariantCulture, out r.Them);
                    _recs[r.Id] = r;
                }
            }
            catch (Exception e) { BtcLog.Warn("DuelPvp", "score load: " + e.Message); }
        }

        private static void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (Rec r in _recs.Values)
                    sb.Append(r.Id).Append('\t').Append((r.Nick ?? "").Replace("\t", " ")).Append('\t').Append(r.W).Append('\t').Append(r.L).Append('\t').Append(r.D)
                      .Append('\t').Append(r.Me.ToString("0.0", CultureInfo.InvariantCulture)).Append('\t').Append(r.Them.ToString("0.0", CultureInfo.InvariantCulture)).Append("\r\n");
                File.WriteAllText(RecFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { BtcLog.Warn("DuelPvp", "score save: " + e.Message); }
        }

        private static Rec RecOf(string id, string nick)
        {
            Load();
            Rec r;
            if (!_recs.TryGetValue(id ?? "", out r)) { r = new Rec { Id = id ?? "", Nick = nick ?? "" }; _recs[r.Id] = r; }
            if (!string.IsNullOrEmpty(nick)) r.Nick = nick;
            return r;
        }

        /// <summary>A round ended (host: Finish; opponent: state message with a new result number). Spectators keep nothing.</summary>
        internal static void RoundOver(int winner, string reason, float t, int k0, int k1)
        {
            int me = BtcDuel.LocalSide;
            if (me < 0) return;
            string oppId = me == 0 ? BtcDuel.GuestId : BtcDuel.HostId, oppNick = me == 0 ? BtcDuel.GuestNick : BtcDuel.HostNick;
            if (string.IsNullOrEmpty(oppId)) return;
            Rec r = RecOf(oppId, oppNick);
            int[] s;
            if (!_session.TryGetValue(r.Id, out s)) { s = new int[3]; _session[r.Id] = s; }
            float score = winner < 0 ? 0.5f : (winner == me ? 1f : 0f);
            if (winner < 0) { r.D++; s[2]++; } else if (winner == me) { r.W++; s[0]++; } else { r.L++; s[1]++; }
            float exp = 1f / (1f + Mathf.Pow(10f, (r.Them - r.Me) / 400f));
            float d = EloK * (score - exp);
            r.Me += d; r.Them -= d;
            Save();
            try
            {
                string dir = Path.Combine(Path.Combine(Paths.GameRootPath, "BetterThanCastle"), "duel-logs");
                Directory.CreateDirectory(dir);
                int myK = me == 0 ? k0 : k1, opK = me == 0 ? k1 : k0;
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\tvs " + (oppNick ?? "?").Replace("\t", " ") + "\t" + (winner < 0 ? "DRAW" : (winner == me ? "WIN" : "LOSS")) +
                              "\t" + reason + "\tt=" + t.ToString("0.0", CultureInfo.InvariantCulture) + "\tkills " + myK + ":" + opK + "\t" + (me == 0 ? "host" : "guest") +
                              "\t" + BtcDuel.SetupLine + "\tsession " + s[0] + ":" + s[1] + (s[2] > 0 ? ":" + s[2] : "") + "\ttotal " + r.W + ":" + r.L + (r.D > 0 ? ":" + r.D : "") +
                              "\telo " + r.Me.ToString("0") + ":" + r.Them.ToString("0") + "\r\n";
                File.AppendAllText(Path.Combine(dir, "pvp-history.txt"), line, new UTF8Encoding(false));
            }
            catch (Exception e) { BtcLog.Warn("DuelPvp", "history: " + e.Message); }
            BtcLog.Info("DuelPvp", "round vs " + oppNick + ": " + (winner < 0 ? "draw" : (winner == me ? "win" : "loss")) + " elo " + r.Me.ToString("0") + ":" + r.Them.ToString("0"));
        }

        /// <summary>HUD line for the players: session score, all time, Elo of both.</summary>
        internal static string ScoreLine()
        {
            int me = BtcDuel.LocalSide;
            if (me < 0) return "";
            string oppId = me == 0 ? BtcDuel.GuestId : BtcDuel.HostId;
            if (string.IsNullOrEmpty(oppId)) return "";
            Rec r = RecOf(oppId, me == 0 ? BtcDuel.GuestNick : BtcDuel.HostNick);
            int[] s; if (!_session.TryGetValue(r.Id, out s)) s = new int[3];
            return Tt("Session ", "Сессия ") + s[0] + ":" + s[1] + "  ·  " + Tt("All time ", "Всего ") + r.W + ":" + r.L + (r.D > 0 ? " (" + r.D + Tt(" draws", " ничьих") + ")" : "") +
                   "  ·  Elo " + r.Me.ToString("0") + " / " + r.Them.ToString("0");
        }

        /// <summary>Session score against the current opponent (Online tab / Discord status).</summary>
        internal static void Session(out int w, out int l)
        {
            w = l = 0;
            int me = BtcDuel.LocalSide;
            if (me < 0) return;
            int[] s; if (_session.TryGetValue((me == 0 ? BtcDuel.GuestId : BtcDuel.HostId) ?? "", out s)) { w = s[0]; l = s[1]; }
        }
    }
}
