using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
    /// <summary>
    /// Duel challenges over the presence channel (Online tab "Duel" button). Flow: challenger "dinv" (settings summary) -> the other side
    /// presses Accept: "dacc" -> challenger hosts a duel lobby (BtcDuelPvp) and sends "dgo" (game port) -> the opponent joins by address,
    /// both lobbies go ready by themselves and the duel loads. "ddec" = declined / cancelled. Nothing here touches the game connection.
    /// </summary>
    internal static partial class BtcPresence
    {
        internal sealed class DuelInvite { internal string From, Nick, Info; internal IPEndPoint Src; internal float At; internal bool Gone; }
        internal static readonly List<DuelInvite> DuelInvites = new List<DuelInvite>();
        internal const float DuelInviteFor = 60f;

        /// <summary>Challenger: the peer we wait for (null = none) and since when.</summary>
        internal static string DuelTo; internal static float DuelToAt;

        /// <summary>Both sides must be free (main menu) and run BetterConnection: the duel messages ride on it.</summary>
        internal static bool CanDuel(Peer p, out string why)
        {
            why = null;
            if (p == null || !p.Online || p.NoMod || p.Src == null) { why = Tt("The player is offline", "Игрок не в сети"); return false; }
            if (!BtcNetworkGate.Enabled) { why = Tt("Turn BetterConnection on to play duels online", "Включите BetterConnection, чтобы играть дуэли по сети"); return false; }
            if (!p.Bc) { why = Tt(p.Nick + " has BetterConnection off", "У " + p.Nick + " выключен BetterConnection"); return false; }
            if (!FreeForDuel(out why)) return false;
            if (p.Kind != BtcActivity.Kind.Menu && p.Kind != BtcActivity.Kind.Afk && p.Kind != BtcActivity.Kind.Lobby) { why = Tt(p.Nick + " is busy right now", p.Nick + " сейчас занят"); return false; }
            return true;
        }

        /// <summary>The local game can start / join a duel now: not in a match, not loading, no duel running.</summary>
        internal static bool FreeForDuel(out string why)
        {
            why = null;
            if (SceneManager.GetActiveScene().buildIndex == 2) { why = Tt("Leave your match first", "Сначала выйдите из своего матча"); return false; }
            if (BtcActivity.Now.Kind == BtcActivity.Kind.Loading) { why = Tt("Wait until the load is over", "Дождитесь конца загрузки"); return false; }
            if (BtcDuel.Active || BtcAim.Active || BtcDuelPvp.Busy) { why = Tt("A duel is already being set up", "Дуэль уже готовится"); return false; }
            return true;
        }

        internal static void SendDuelInvite(Peer p)
        {
            if (p == null || p.Src == null) return;
            DuelTo = p.Id; DuelToAt = Time.unscaledTime;
            byte[] b = Pack("dinv", "id", MyId, "n", Cut(NickPlain(), 64), "info", Cut(BtcDuelPvp.SettingsLine(), 300));
            for (int i = 0; i < 3; i++) Send(p.Src, b);
            BtcLog.Info("Presence", "duel challenge -> " + p.Nick);
        }

        internal static void CancelDuelInvite()
        {
            if (DuelTo == null) return;
            Peer p = GetPeer(DuelTo, false);
            if (p != null && p.Src != null) { byte[] b = Pack("ddec", "id", MyId); for (int i = 0; i < 2; i++) Send(p.Src, b); }
            DuelTo = null;
        }

        private static void HandleDuel(string type, string id, IPEndPoint src, float now)
        {
            switch (type)
            {
                case "dinv": OnDuelInvite(id, src, now); break;
                case "dacc": OnDuelAccept(id, src); break;
                case "ddec": OnDuelDecline(id); break;
                case "dgo": OnDuelGo(id, src); break;
            }
        }

        private static void OnDuelInvite(string id, IPEndPoint src, float now)
        {
            if (BtcConfig.OnlineInvites != null && !BtcConfig.OnlineInvites.Value) return;
            for (int i = 0; i < DuelInvites.Count; i++) if (DuelInvites[i].From == id && !DuelInvites[i].Gone && now - DuelInvites[i].At < 10f) return;
            DuelInvites.RemoveAll(x => x.From == id);
            DuelInvite d = new DuelInvite { From = id, Nick = V("n"), Info = V("info"), Src = From(src), At = now };
            DuelInvites.Add(d);
            BtcSfx.Open();
            BtcHud.Notice(Tt(d.Nick + " challenges you to a micro duel", d.Nick + " вызывает вас на микродуэль"), BtcReplayIcons.Swords, BtcHud.Ok);
            BtcLog.Info("Presence", "duel challenge from " + d.Nick + " (" + d.Info + ")");
        }

        /// <summary>Opponent side: Accept on the card / Online tab row.</summary>
        internal static bool AcceptDuel(DuelInvite d, out string why)
        {
            why = null;
            if (d == null || d.Gone) return false;
            if (!BtcNetworkGate.Enabled) { why = Tt("Turn BetterConnection on to play duels online", "Включите BetterConnection, чтобы играть дуэли по сети"); return false; }
            if (!FreeForDuel(out why)) return false;
            if (!BtcDuelVoid.Ensure()) { why = Tt("The duel map could not be prepared", "Не удалось подготовить карту дуэли"); return false; }
            d.Gone = true;
            byte[] b = Pack("dacc", "id", MyId, "n", Cut(NickPlain(), 64));
            for (int i = 0; i < 3; i++) Send(d.Src, b);
            BtcDuelPvp.GuestAccepted(d.From, d.Nick);
            BtcLog.Info("Presence", "duel accepted: " + d.Nick);
            return true;
        }

        internal static void DeclineDuel(DuelInvite d)
        {
            if (d == null) return;
            d.Gone = true;
            if (d.Src != null) Send(d.Src, Pack("ddec", "id", MyId));
        }

        private static void OnDuelAccept(string id, IPEndPoint src)
        {
            if (DuelTo != id) return;   // copies of the packet, or a late accept of a cancelled challenge
            DuelTo = null;
            Peer p = GetPeer(id, false);
            string nick = V("n");
            if (p == null) { p = GetPeer(id, true); p.Nick = nick; }
            p.Src = From(src);
            string why;
            if (!FreeForDuel(out why) || !BtcDuelPvp.HostBegin(id, nick, p.Src))
            {
                Send(From(src), Pack("ddec", "id", MyId));
                BtcHud.Notice(why ?? Tt("The duel could not start", "Дуэль не запустилась"), BtcReplayIcons.Cross, BtcHud.Warn);
                return;
            }
            BtcSfx.Ok();
            BtcHud.Notice(Tt(nick + " accepted: preparing the duel", nick + " принял вызов: готовим дуэль"), BtcReplayIcons.Swords, BtcHud.Ok);
        }

        private static void OnDuelDecline(string id)
        {
            for (int i = 0; i < DuelInvites.Count; i++) if (DuelInvites[i].From == id) DuelInvites[i].Gone = true;
            if (DuelTo == id)
            {
                DuelTo = null;
                Peer p = GetPeer(id, false);
                BtcSfx.Warn();
                BtcHud.Notice(Tt((p != null ? p.Nick : "?") + " declined the duel", (p != null ? p.Nick : "?") + " отказался от дуэли"), BtcReplayIcons.Cross, BtcHud.Warn);
            }
            BtcDuelPvp.PeerDeclined(id);
        }

        /// <summary>Host -> opponent: the duel lobby listens on this port.</summary>
        internal static void SendDuelGo(IPEndPoint to, int port)
        {
            byte[] b = Pack("dgo", "id", MyId, "n", Cut(NickPlain(), 64), "port", port.ToString());
            for (int i = 0; i < 3; i++) Send(to, b);
        }

        internal static void SendDuelCancel(IPEndPoint to) { if (to != null) { byte[] b = Pack("ddec", "id", MyId); for (int i = 0; i < 2; i++) Send(to, b); } }

        private static void OnDuelGo(string id, IPEndPoint src)
        {
            if (!BtcDuelPvp.ExpectsGo(id)) return;
            int port = I("port", 3000);
            string why;
            if (!JoinAddress(src.Address.ToString(), port, out why))
            {
                BtcHud.Notice(why, BtcReplayIcons.Cross, BtcHud.Warn);
                BtcDuelPvp.Abort("join failed: " + why, true);
                return;
            }
            BtcDuelPvp.GuestJoining();
        }

        /// <summary>Duel challenge cards (menu scenes only) + the challenger's waiting card.</summary>
        private static void DrawDuelHud()
        {
            if (Event.current == null) return;
            float now = Time.unscaledTime;
            bool inMatch = SceneManager.GetActiveScene().buildIndex == 2;
            for (int i = DuelInvites.Count - 1; i >= 0; i--)
            {
                DuelInvite d = DuelInvites[i];
                if (d.Gone || now - d.At > DuelInviteFor) { DuelInvites.RemoveAt(i); continue; }
                if (inMatch) continue;
                string cid = "btcduelinv:" + d.From;
                DuelInvite cur = d;
                bool x = BtcHud.Card(cid, BtcReplayIcons.Swords, BtcHud.Ok, Tt(d.Nick + " challenges you to a micro duel", d.Nick + " вызывает вас на микродуэль"),
                    (string.IsNullOrEmpty(d.Info) ? "" : d.Info + ". ") + Tt("Accept? (Online tab: decline)", "Принять? (вкладка Онлайн: отказаться)"), null, false, Tt("Accept", "Принять"),
                    delegate
                    {
                        string why;
                        if (AcceptDuel(cur, out why)) BtcSfx.Ok();
                        else { BtcSfx.Warn(); if (why != null) BtcHud.Notice(why, BtcReplayIcons.Cross, BtcHud.Warn); }
                    });
                if (!x) BtcHud.ForceOpen(cid);
            }
            if (DuelTo != null && now - DuelToAt > DuelInviteFor) { CancelDuelInvite(); BtcHud.Notice(Tt("No answer to the duel challenge", "Нет ответа на вызов"), BtcReplayIcons.Cross, BtcHud.Warn); }
            BtcDuelPvp.DrawHud();
        }

        /// <summary>DEV: accept the newest duel challenge.</summary>
        internal static bool AcceptDuelNewest()
        {
            for (int i = DuelInvites.Count - 1; i >= 0; i--) if (!DuelInvites[i].Gone) { string why; return AcceptDuel(DuelInvites[i], out why); }
            return false;
        }

        /// <summary>DEV: challenge the first online mod peer.</summary>
        internal static bool DuelFirst()
        {
            foreach (Peer p in Peers.Values) { string why; if (CanDuel(p, out why)) { SendDuelInvite(p); return true; } }
            return false;
        }
    }
}
