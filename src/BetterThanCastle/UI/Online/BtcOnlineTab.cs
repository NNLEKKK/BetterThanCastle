using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Online tab (menu tab 16, header corner): mod players on the same networks (BtcPresence) as cards like lobby plates: avatar, nick in the
    /// plate colour, a stripe in the colour of what they do, two status lines, address (copy), Join / Invite / Message buttons. Own card first.
    /// Folds "Online" and "Met before" (offline history), chat on the right (common channel + direct messages), settings fold at the bottom.
    /// Remembered: folds (BtcUi.Fold), chat channel + draft (BtcMenuState online:*), every setting (BepInEx config, undo scope "online").
    /// </summary>
    internal sealed class BtcOnlineTab : BtcMenuTab
    {
        internal static readonly BtcOnlineTab Instance = new BtcOnlineTab();
        internal const int TabId = 16;
        private const string Scope = "online";

        internal override int Id { get { return TabId; } }
        internal override bool IsActive { get { return BtcMenu.CurrentId == TabId; } }
        internal override string UndoScope { get { return Scope; } }

        private Vector2 _scroll, _chatScroll;
        private float _h = 100f, _chatH = 100f;
        private int _chatCount = -1;
        private string _draft;
        private static GUIStyle _ttl, _nick, _l1, _l2, _chip, _msg, _msgNick;
        private static int _fs;
        private readonly List<BtcPresence.Peer> _on = new List<BtcPresence.Peer>(), _off = new List<BtcPresence.Peer>();

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        internal static string Channel
        {
            get { return BtcMenuState.Get("online:ch", "all"); }
            set { BtcMenuState.Set("online:ch", value); }
        }

        /// <summary>The player has this chat channel in front of them now (no unread counter, no notice).</summary>
        internal static bool Watching(string ch)
        {
            return BtcHotkeys.MenuOpen && BtcMenu.CurrentId == TabId && Channel == ch;
        }

        private static void Styles(int fs)
        {
            if (_ttl != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _ttl = new GUIStyle(b); _ttl.fontSize = Mathf.RoundToInt(fs * 1.15f); _ttl.fontStyle = FontStyle.Bold; _ttl.alignment = TextAnchor.MiddleLeft; _ttl.normal.textColor = Color.white;
            _nick = new GUIStyle(b); _nick.fontSize = Mathf.RoundToInt(fs * 1.05f); _nick.fontStyle = FontStyle.Bold; _nick.alignment = TextAnchor.MiddleLeft; _nick.clipping = TextClipping.Clip; _nick.richText = false;
            _l1 = new GUIStyle(b); _l1.fontSize = Mathf.RoundToInt(fs * 0.85f); _l1.alignment = TextAnchor.MiddleLeft; _l1.clipping = TextClipping.Clip; _l1.richText = false;
            _l2 = new GUIStyle(_l1); _l2.fontSize = Mathf.RoundToInt(fs * 0.75f); _l2.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            _chip = new GUIStyle(_l2); _chip.alignment = TextAnchor.MiddleCenter;
            _msg = new GUIStyle(_l1); _msg.wordWrap = true; _msg.alignment = TextAnchor.UpperLeft; _msg.normal.textColor = new Color(1f, 1f, 1f, 0.88f);
            _msgNick = new GUIStyle(_l2); _msgNick.fontStyle = FontStyle.Bold; _msgNick.alignment = TextAnchor.UpperLeft;
        }

        private static void Txt(Rect r, string s, GUIStyle st, Color c)
        {
            Color old = GUI.contentColor;
            GUI.contentColor = c;
            GUI.Label(r, s, st);
            GUI.contentColor = old;
        }

        private static Color Hex(int v, Color def)
        {
            if (v < 0) return def;
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
        }

        internal override void Draw(Rect area, int fs)
        {
            Styles(fs);
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == BtcMenu.MenuKeyMain && GUIUtility.keyboardControl == 0)
            {
                BtcMenu.PgDownFrame = Time.frameCount;
                ResourcePackManager.CloseMenu();
                e.Use();
                return;
            }
            BtcUi.ClickAway(e);
            float pad = fs, ib = fs * 1.9f;
            float y0 = area.y + fs * 0.4f;
            int online = BtcPresence.OnlineCount;
            GUI.Label(new Rect(area.x + pad, y0, area.width * 0.6f, ib), Tt("Online", "Онлайн") + (BtcPresence.On ? "  " + online : ""), _ttl);

            bool on = BtcPresence.On;
            Rect sw = new Rect(area.xMax - pad - fs * 2.4f, y0 + (ib - fs * 1.2f) * 0.5f, fs * 2.4f, fs * 1.2f);
            if (BtcUi.HeaderSwitch(sw, on, on ? Tt("You see mod players on your networks and they see you (click: hide and stop)", "Вы видите игроков с модом в ваших сетях, они видят вас (клик: скрыться и выключить)")
                                              : Tt("Online list is OFF: nobody sees you (click to turn on)", "Онлайн выключен: вас никто не видит (клик включит)")))
                Flip(BtcConfig.OnlineOn, Tt("Online", "Онлайн"));

            Rect body = new Rect(area.x, y0 + ib + fs * 0.4f, area.width, area.yMax - (y0 + ib + fs * 0.4f));
            if (!on)
            {
                float y = body.y + fs;
                GUI.BeginGroup(new Rect(body.x, 0f, body.width, area.yMax));
                BtcUi.Note(ref y, body.width, fs, Tt("The Online list is off. Turn it on with the switch at the top right to see who plays BTC on your Radmin / Hamachi / LAN networks, chat with them and join their games.",
                    "Онлайн выключен. Включите его переключателем справа сверху, чтобы видеть, кто играет с BTC в ваших сетях Radmin / Hamachi / LAN, общаться с ними и заходить к ним в игры."));
                GUI.EndGroup();
                BtcReplayIcons.DrawTip(fs, area.xMax, false);
                return;
            }
            bool wide = body.width > fs * 48f;
            Rect list = wide ? new Rect(body.x, body.y, body.width * 0.58f, body.height) : new Rect(body.x, body.y, body.width, body.height * 0.58f);
            Rect chat = wide ? new Rect(list.xMax, body.y, body.width - list.width, body.height) : new Rect(body.x, list.yMax, body.width, body.height - list.height);
            DrawList(list, fs);
            DrawChat(chat, fs);
            BtcReplayIcons.DrawTip(fs, area.xMax, false);
        }

        private void Flip(BepInEx.Configuration.ConfigEntry<bool> c, string label)
        {
            if (c == null) return;
            bool was = c.Value;
            c.Value = !was;
            BtcSfx.Switch(!was);
            BtcUndo.Push(Scope, label, delegate { c.Value = was; });
        }

        // ------------------------------------------------------------------ player list
        private void DrawList(Rect view, int fs)
        {
            Collect();
            float w = view.width - (_h > view.height ? 14f : 0f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, w, _h));
            float y = fs * 0.2f;
            if (!string.IsNullOrEmpty(BtcPresence.Status)) { Txt(new Rect(fs, y, w - fs * 2f, fs * 1.4f), BtcPresence.Status, _l2, BtcHud.Warn); y += fs * 1.6f; }

            for (int i = BtcPresence.DuelInvites.Count - 1; i >= 0; i--) DuelRow(ref y, w, fs, BtcPresence.DuelInvites[i]);
            for (int i = BtcPresence.Invites.Count - 1; i >= 0; i--) InviteRow(ref y, w, fs, BtcPresence.Invites[i]);
            if (BtcUi.Fold(ref y, w, fs, "online:on", Tt("Online now", "Сейчас в сети"), _on.Count + 1))
            {
                Card(ref y, w, fs, null, view);
                for (int i = 0; i < _on.Count; i++) Card(ref y, w, fs, _on[i], view);
                if (_on.Count == 0)
                    BtcUi.Note(ref y, w, fs, Tt("Nobody else with the mod is online on your networks right now. Players appear here when they start the game with BTC (Radmin, Hamachi, LAN).",
                        "Сейчас больше никого с модом в ваших сетях нет. Игроки появятся здесь, когда запустят игру с BTC (Radmin, Hamachi, LAN)."));
                y += fs * 0.4f;
            }
            if (BtcUi.Fold(ref y, w, fs, "online:off", Tt("Met before (offline)", "Встречались (не в сети)"), _off.Count))
            {
                for (int i = 0; i < _off.Count; i++) Card(ref y, w, fs, _off[i], view);
                if (_off.Count == 0) BtcUi.Note(ref y, w, fs, Tt("Everyone you play with in a lobby or match is remembered here.", "Здесь запоминаются все, с кем вы были в одном лобби или матче."));
                y += fs * 0.4f;
            }
            if (BtcUi.Fold(ref y, w, fs, "online:set", Tt("Settings", "Настройки"), 6)) Settings(ref y, w, fs);
            _h = y + fs;
            GUI.EndScrollView();
        }

        /// <summary>A pending invite: who, where, seconds left, Join and Decline.</summary>
        private void InviteRow(ref float y, float w, int fs, BtcPresence.Invite inv)
        {
            if (inv.Gone) return;
            float h = fs * 2.6f;
            Rect r = new Rect(fs, y, w - fs * 2f, h);
            y += h + fs * 0.35f;
            BtcReplayUI.Fill(r, BtcMenu.AccentA(0.14f));
            BtcReplayUI.Fill(new Rect(r.x, r.y, fs * 0.28f, r.height), BtcMenu.Accent);
            BtcReplayIcons.Glyph(BtcReplayIcons.Group, new Rect(r.x + fs * 0.7f, r.y + (h - fs * 1.3f) * 0.5f, fs * 1.3f, fs * 1.3f), BtcMenu.Accent);
            int left = Mathf.Max(0, 60 - (int)(Time.unscaledTime - inv.At));
            string where = Join(inv.Mode, inv.Map);
            float bs = fs * 1.7f, by = r.y + (h - bs) * 0.5f;
            Rect dr = new Rect(r.xMax - fs * 0.5f - bs, by, bs, bs);
            Rect jr = new Rect(dr.x - fs * 0.3f - bs * 2.6f, by, bs * 2.6f, bs);
            Txt(new Rect(r.x + fs * 2.4f, r.y + fs * 0.2f, jr.x - r.x - fs * 2.6f, fs * 1.3f), Tt(inv.Nick + " invites you", inv.Nick + " зовёт вас в игру"), _l1, Color.white);
            Txt(new Rect(r.x + fs * 2.4f, r.y + fs * 1.35f, jr.x - r.x - fs * 2.6f, fs * 1.1f), Join(where, left + Tt(" s", " с")), _l2, new Color(1f, 1f, 1f, 0.6f));
            bool hov = jr.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(jr, BtcMenu.AccentA(hov ? 0.95f : 0.75f));
            Txt(jr, Tt("Join", "Зайти"), _chip, new Color(0.03f, 0.05f, 0.04f, 1f));
            if (BtcSfx.Btn(jr, "", BtcReplayUI.sBtn))
            {
                string why;
                if (BtcPresence.JoinAddress(inv.Src.Address.ToString(), inv.Port, out why)) { inv.Gone = true; BtcSfx.Ok(); ResourcePackManager.CloseMenu(); }
                else { BtcSfx.Warn(); BtcMenu.Toast(why, BtcReplayIcons.Cross); }
            }
            if (BtcReplayIcons.Button(dr, BtcReplayIcons.Cross, false, Tt("Decline", "Отклонить"))) { inv.Gone = true; BtcSfx.Delete(); }
        }

        /// <summary>A pending duel challenge: who, the challenger's settings, seconds left, Accept and Decline.</summary>
        private void DuelRow(ref float y, float w, int fs, BtcPresence.DuelInvite d)
        {
            if (d.Gone) return;
            float h = fs * 2.6f;
            Rect r = new Rect(fs, y, w - fs * 2f, h);
            y += h + fs * 0.35f;
            BtcReplayUI.Fill(r, BtcMenu.AccentA(0.14f));
            BtcReplayUI.Fill(new Rect(r.x, r.y, fs * 0.28f, r.height), BtcMenu.Accent);
            BtcReplayIcons.Glyph(BtcReplayIcons.Swords, new Rect(r.x + fs * 0.7f, r.y + (h - fs * 1.3f) * 0.5f, fs * 1.3f, fs * 1.3f), BtcMenu.Accent);
            int left = Mathf.Max(0, (int)BtcPresence.DuelInviteFor - (int)(Time.unscaledTime - d.At));
            float bs = fs * 1.7f, by = r.y + (h - bs) * 0.5f;
            Rect dr = new Rect(r.xMax - fs * 0.5f - bs, by, bs, bs);
            Rect jr = new Rect(dr.x - fs * 0.3f - bs * 2.6f, by, bs * 2.6f, bs);
            Txt(new Rect(r.x + fs * 2.4f, r.y + fs * 0.2f, jr.x - r.x - fs * 2.6f, fs * 1.3f), Tt(d.Nick + " challenges you to a micro duel", d.Nick + " вызывает вас на микродуэль"), _l1, Color.white);
            Txt(new Rect(r.x + fs * 2.4f, r.y + fs * 1.35f, jr.x - r.x - fs * 2.6f, fs * 1.1f), Join(d.Info, left + Tt(" s", " с")), _l2, new Color(1f, 1f, 1f, 0.6f));
            bool hov = jr.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(jr, BtcMenu.AccentA(hov ? 0.95f : 0.75f));
            Txt(jr, Tt("Accept", "Принять"), _chip, new Color(0.03f, 0.05f, 0.04f, 1f));
            if (BtcSfx.Btn(jr, "", BtcReplayUI.sBtn))
            {
                string why;
                if (BtcPresence.AcceptDuel(d, out why)) { BtcSfx.Ok(); ResourcePackManager.CloseMenu(); }
                else { BtcSfx.Warn(); BtcMenu.Toast(why ?? Tt("Could not accept", "Не удалось принять"), BtcReplayIcons.Cross); }
            }
            if (BtcReplayIcons.Button(dr, BtcReplayIcons.Cross, false, Tt("Decline", "Отказаться"))) { BtcPresence.DeclineDuel(d); BtcSfx.Delete(); }
        }

        private void Collect()
        {
            _on.Clear(); _off.Clear();
            foreach (BtcPresence.Peer p in BtcPresence.Peers.Values)
            {
                if (p.Online && !p.NoMod) _on.Add(p);
                else if (p.Met) _off.Add(p);
            }
            _on.Sort(delegate (BtcPresence.Peer a, BtcPresence.Peer b)
            {
                int ja = a.Join || a.JoinMatch ? 0 : 1, jb = b.Join || b.JoinMatch ? 0 : 1;
                if (ja != jb) return ja.CompareTo(jb);
                int ma = a.Met ? 0 : 1, mb = b.Met ? 0 : 1;
                if (ma != mb) return ma.CompareTo(mb);
                return string.Compare(a.Nick, b.Nick, StringComparison.OrdinalIgnoreCase);
            });
            _off.Sort((a, b) => b.LastUtc.CompareTo(a.LastUtc));
        }

        private static string Ago(long utc)
        {
            if (utc <= 0) return "";
            TimeSpan d = DateTime.UtcNow - new DateTime(utc, DateTimeKind.Utc);
            if (d.TotalMinutes < 2) return Tt("just now", "только что");
            if (d.TotalHours < 1) return (int)d.TotalMinutes + Tt(" min ago", " мин назад");
            if (d.TotalDays < 1) return (int)d.TotalHours + Tt(" h ago", " ч назад");
            if (d.TotalDays < 60) return (int)d.TotalDays + Tt(" days ago", " дн. назад");
            return new DateTime(utc, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd");
        }

        /// <summary>One player card. p == null = the own card.</summary>
        private void Card(ref float y, float w, int fs, BtcPresence.Peer p, Rect view)
        {
            bool me = p == null;
            float x = fs, cw = w - fs * 2f, h = fs * 4.4f;
            Rect r = new Rect(x, y, cw, h);
            y += h + fs * 0.35f;
            if (r.yMax < _scroll.y || r.y > _scroll.y + view.height) return;   // off screen

            BtcActivity.Kind kind; string map, mode, det, vs, nick, ip, av, ver = null; bool host, online; int pl, mx, secs; int plateA, plateG;
            if (me)
            {
                BtcActivity.Snap s = BtcActivity.Now;
                kind = s.Kind; map = s.Map; mode = s.Mode; det = s.Detail; vs = s.Versus; host = s.Host; pl = s.Players; mx = s.Max; secs = s.Seconds;
                nick = BtcPresence.MyNick; ip = BtcConfig.OnlineShowIp.Value ? BtcPresence.MyIp : ""; av = BtcPresence.MyAvatar; online = true;
                plateA = -1; plateG = -1;
                try { plateA = BtcPlate.Get("a"); plateG = BtcPlate.Get("g"); } catch (Exception) { }
            }
            else
            {
                kind = p.Kind; map = p.Map; mode = p.Mode; det = p.Detail; vs = p.Versus; host = p.Host; pl = p.Players; mx = p.Max; online = p.Online && !p.NoMod;
                secs = p.Secs >= 0 ? p.Secs + (int)(Time.unscaledTime - p.SecsAt) : -1;
                nick = p.Nick; ip = online ? p.ShownIp : ""; av = p.Av; plateA = p.PlateA; plateG = p.PlateG;
                if (!string.IsNullOrEmpty(p.Ver) && p.Ver != BtcPlugin.Version) ver = "v" + p.Ver;
            }
            Color sc = online ? BtcActivity.ColorOf(kind) : new Color(0.4f, 0.4f, 0.42f, 1f);
            bool hover = r.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, hover ? 0.075f : 0.04f));
            // plate tint: a thin band in the player's plate colours along the top
            Color ca = Hex(plateA, sc), cg = Hex(plateG, ca);
            BtcReplayUI.Fill(new Rect(r.x, r.y, r.width * 0.5f, 2f), new Color(ca.r, ca.g, ca.b, online ? 0.8f : 0.3f));
            BtcReplayUI.Fill(new Rect(r.x + r.width * 0.5f, r.y, r.width * 0.5f, 2f), new Color(cg.r, cg.g, cg.b, online ? 0.8f : 0.3f));
            BtcReplayUI.Fill(new Rect(r.x, r.y, fs * 0.28f, r.height), sc);   // status stripe

            float avs = h - fs * 0.9f;
            Rect ar = new Rect(r.x + fs * 0.7f, r.y + (h - avs) * 0.5f, avs, avs);
            BtcReplayUI.Fill(ar, new Color(0.15f, 0.15f, 0.16f, 1f));
            Texture2D tex = BtcPresence.AvatarOf(av);
            if (tex != null) { Color oc = GUI.color; if (!online) GUI.color = new Color(1f, 1f, 1f, 0.45f); GUI.DrawTexture(ar, tex, ScaleMode.ScaleAndCrop, true); GUI.color = oc; }
            else BtcReplayIcons.Glyph(p != null && p.NoMod ? BtcReplayIcons.Ghost : BtcReplayIcons.Group, new Rect(ar.x + avs * 0.2f, ar.y + avs * 0.2f, avs * 0.6f, avs * 0.6f), new Color(1f, 1f, 1f, 0.3f));
            if (online) BtcReplayUI.Fill(new Rect(ar.xMax - fs * 0.7f, ar.yMax - fs * 0.7f, fs * 0.7f, fs * 0.7f), sc);   // status dot

            // buttons on the right (right to left)
            float bs = fs * 1.7f, bx = r.xMax - fs * 0.5f - bs, by = r.y + (h - bs) * 0.5f;
            if (!me) Buttons(p, online, kind, fs, ref bx, by, bs, view);
            else
            {
                if (!string.IsNullOrEmpty(ip) && BtcReplayIcons.Button(new Rect(bx, by, bs, bs), BtcReplayIcons.Copy, false, Tt("Copy your address: ", "Скопировать ваш адрес: ") + ip))
                { GUIUtility.systemCopyBuffer = ip; BtcMenu.Toast(Tt("Copied ", "Скопировано ") + ip, BtcReplayIcons.Copy); }
                bx -= bs + fs * 0.3f;
            }

            float tx = ar.xMax + fs * 0.7f, tw = bx + bs - tx - fs * 0.3f;
            Color nc = Hex(plateA, Color.white);
            if (nc.r + nc.g + nc.b < 0.6f) nc = Color.white;   // dark plate colour: unreadable on the dark card
            if (!online) nc = new Color(nc.r, nc.g, nc.b, 0.55f);
            string nickLine = string.IsNullOrEmpty(nick) ? "?" : nick;
            Vector2 nsz = _nick.CalcSize(new GUIContent(nickLine));
            float nw = Mathf.Min(nsz.x, tw * 0.6f);
            Txt(new Rect(tx, r.y + fs * 0.25f, nw, fs * 1.5f), nickLine, _nick, nc);
            float cx = tx + nw + fs * 0.5f;
            if (me) cx = Chip(cx, r.y + fs * 0.45f, fs, Tt("you", "вы"), BtcMenu.Accent);
            if (ver != null) cx = Chip(cx, r.y + fs * 0.45f, fs, ver, BtcHud.Warn, Tt("Another mod version: joining may fail. Update both to the same version.", "Другая версия мода: подключение может не получиться. Обновитесь до одной версии."));
            if (p != null && p.NoMod) cx = Chip(cx, r.y + fs * 0.45f, fs, Tt("no BTC", "без BTC"), new Color(1f, 1f, 1f, 0.5f), Tt("Played with you without the mod (or an old version): never shows online.", "Играл с вами без мода (или со старой версией): онлайн не показывается."));
            if (!string.IsNullOrEmpty(ip) && online) Txt(new Rect(cx, r.y + fs * 0.25f, Mathf.Max(0f, tx + tw - cx), fs * 1.5f), ip, _l2, new Color(1f, 1f, 1f, 0.45f));

            string l1, l2;
            if (online) BtcActivity.Lines(kind, map, mode, det, host, pl, mx, secs, vs, out l1, out l2);
            else { l1 = Tt("Offline", "Не в сети"); l2 = p != null ? Join(Ago(p.LastUtc), p.LastIp) : ""; }
            BtcReplayIcons.Glyph(online ? BtcActivity.IconOf(kind) : BtcReplayIcons.Slow, new Rect(tx, r.y + fs * 1.85f, fs * 0.95f, fs * 0.95f), sc);
            Txt(new Rect(tx + fs * 1.2f, r.y + fs * 1.65f, tw - fs * 1.2f, fs * 1.35f), l1, _l1, online ? sc : new Color(1f, 1f, 1f, 0.45f));
            if (!string.IsNullOrEmpty(l2)) Txt(new Rect(tx, r.y + fs * 2.9f, tw, fs * 1.25f), l2, _l2, new Color(1f, 1f, 1f, 0.55f));
        }

        private static string Join(string a, string b) { if (string.IsNullOrEmpty(a)) return b ?? ""; if (string.IsNullOrEmpty(b)) return a; return a + " · " + b; }

        private float Chip(float x, float y, int fs, string text, Color c) { return Chip(x, y, fs, text, c, null); }

        private float Chip(float x, float y, int fs, string text, Color c, string tip)
        {
            float w = _chip.CalcSize(new GUIContent(text)).x + fs * 0.8f;
            Rect r = new Rect(x, y, w, fs * 1.1f);
            BtcReplayUI.Fill(r, new Color(c.r, c.g, c.b, 0.18f));
            Txt(r, text, _chip, c);
            if (tip != null) BtcReplayIcons.Hint(r, tip);
            return r.xMax + fs * 0.4f;
        }

        private void Buttons(BtcPresence.Peer p, bool online, BtcActivity.Kind kind, int fs, ref float bx, float by, float bs, Rect view)
        {
            float step = bs + fs * 0.3f;
            if (!online)
            {
                if (BtcReplayIcons.Button(new Rect(bx, by, bs, bs), BtcReplayIcons.Trash, false, Tt("Forget this player", "Забыть игрока")))
                {
                    BtcPresence.Peer keep = p;
                    BtcPresence.Forget(p.Id);
                    BtcSfx.Delete();
                    BtcUndo.Push(Scope, Tt("Forget ", "Забыть ") + p.Nick, delegate { BtcPresence.Restore(keep); });
                    BtcMenu.Toast(Tt("Forgot ", "Забыт ") + p.Nick + Tt(" (Ctrl+Z brings it back)", " (Ctrl+Z вернёт)"), BtcReplayIcons.Trash);
                }
                bx -= step;
                if (!p.NoMod && !string.IsNullOrEmpty(p.LastIp) && BtcReplayIcons.Button(new Rect(bx, by, bs, bs), BtcReplayIcons.Copy, false, Tt("Copy the last address: ", "Скопировать последний адрес: ") + p.LastIp))
                { GUIUtility.systemCopyBuffer = p.LastIp; BtcMenu.Toast(Tt("Copied ", "Скопировано ") + p.LastIp, BtcReplayIcons.Copy); }
                bx -= step;
                return;
            }
            // join
            bool can = p.Join || p.JoinMatch;
            Rect jr = new Rect(bx - bs * 1.6f, by, bs * 2.6f, bs);
            if (can)
            {
                bool hov = jr.Contains(Event.current.mousePosition);
                BtcReplayUI.Fill(jr, BtcMenu.AccentA(hov ? 0.95f : 0.75f));
                Txt(jr, p.Join ? Tt("Join", "Зайти") : Tt("Join match", "В матч"), _chip, new Color(0.03f, 0.05f, 0.04f, 1f));
                BtcReplayIcons.Hint(jr, p.Join ? Tt("Join the lobby of ", "Зайти в лобби к ") + p.Nick + " (" + p.Src.Address + ":" + p.Port + ")"
                    : Tt("The match is running: you join as a spectator or a returning player (BetterConnection)", "Матч идёт: вы зайдёте наблюдателем или вернувшимся игроком (BetterConnection)"));
                if (BtcSfx.Btn(jr, "", BtcReplayUI.sBtn))
                {
                    string why;
                    if (BtcPresence.JoinPeer(p, out why)) { BtcSfx.Ok(); ResourcePackManager.CloseMenu(); }
                    else { BtcSfx.Warn(); BtcMenu.Toast(why, BtcReplayIcons.Cross); }
                }
                bx = jr.x - fs * 0.3f - bs;
            }
            else if (p.Host && (kind == BtcActivity.Kind.Lobby || kind == BtcActivity.Kind.Match))
            {
                BtcReplayIcons.Glyph(BtcReplayIcons.Lock, new Rect(bx + bs * 0.2f, by + bs * 0.2f, bs * 0.6f, bs * 0.6f), new Color(1f, 1f, 1f, 0.35f));
                BtcReplayIcons.Hint(new Rect(bx, by, bs, bs), kind == BtcActivity.Kind.Lobby ? Tt("The lobby is full or closed by the host", "Лобби заполнено или закрыто хостом")
                    : Tt("The host does not take players into the running match", "Хост не пускает в идущий матч"));
                bx -= step;
            }
            // micro duel challenge (both free, BetterConnection on both sides)
            {
                string why;
                bool can2 = BtcPresence.CanDuel(p, out why);
                bool waiting = BtcPresence.DuelTo == p.Id;
                string tip = waiting ? Tt("Challenge sent: waiting for the answer (click to cancel)", "Вызов отправлен: ждём ответа (нажмите, чтобы отменить)")
                    : can2 ? Tt("Challenge ", "Вызвать ") + p.Nick + Tt(" to a micro duel with your Duel tab settings: ", " на микродуэль с настройками вашей вкладки Дуэль: ") + BtcDuelPvp.SettingsLine()
                    : Tt("Micro duel: ", "Микродуэль: ") + why;
                Color oc = GUI.color;
                if (!can2 && !waiting) GUI.color = new Color(1f, 1f, 1f, 0.4f);
                bool hit = BtcReplayIcons.Button(new Rect(bx, by, bs, bs), BtcReplayIcons.Swords, waiting, tip);
                GUI.color = oc;
                if (hit)
                {
                    if (waiting) { BtcPresence.CancelDuelInvite(); BtcSfx.Delete(); }
                    else if (can2)
                    {
                        BtcPresence.SendDuelInvite(p);
                        BtcSfx.Ok();
                        BtcMenu.Toast(Tt("Duel challenge sent to ", "Вызов на дуэль отправлен: ") + p.Nick, BtcReplayIcons.Swords);
                    }
                    else { BtcSfx.Warn(); BtcMenu.Toast(why, BtcReplayIcons.Cross); }
                }
                bx -= step;
            }
            // invite (only a host in its lobby)
            BtcActivity.Snap me = BtcActivity.Now;
            if (me.Host && me.Kind == BtcActivity.Kind.Lobby)
            {
                bool busy = kind == BtcActivity.Kind.Match || kind == BtcActivity.Kind.Loading || kind == BtcActivity.Kind.Replay;
                if (BtcReplayIcons.Button(new Rect(bx, by, bs, bs), BtcReplayIcons.Plus, false, Tt("Invite to your lobby", "Пригласить в ваше лобби") + (busy ? Tt(" (busy in a game now)", " (сейчас занят игрой)") : "")))
                {
                    BtcPresence.SendInvite(p);
                    BtcSfx.Ok();
                    BtcMenu.Toast(Tt("Invite sent to ", "Приглашение отправлено: ") + p.Nick, BtcReplayIcons.Plus);
                }
                bx -= step;
            }
            // direct message
            int unread; BtcPresence.Unread.TryGetValue(p.Id, out unread);
            Rect mr = new Rect(bx, by, bs, bs);
            if (BtcReplayIcons.Button(mr, BtcReplayIcons.Chat, Channel == p.Id, Tt("Message ", "Написать ") + p.Nick)) { Channel = p.Id; BtcPresence.Unread.Remove(p.Id); _chatCount = -1; }
            if (unread > 0) Badge(mr, fs, unread);
            bx -= step;
            if (!string.IsNullOrEmpty(p.ShownIp) && BtcReplayIcons.Button(new Rect(bx, by, bs, bs), BtcReplayIcons.Copy, false, Tt("Copy the address: ", "Скопировать адрес: ") + p.ShownIp + ":" + p.Port))
            { GUIUtility.systemCopyBuffer = p.ShownIp; BtcMenu.Toast(Tt("Copied ", "Скопировано ") + p.ShownIp, BtcReplayIcons.Copy); }
            bx -= step;
        }

        private static void Badge(Rect r, int fs, int n)
        {
            Rect b = new Rect(r.xMax - fs * 0.8f, r.y - fs * 0.2f, fs * 1f, fs * 0.9f);
            BtcReplayUI.Fill(b, new Color(0.9f, 0.3f, 0.3f, 1f));
            Txt(b, n > 9 ? "9+" : n.ToString(), _chip, Color.white);
        }

        // ------------------------------------------------------------------ settings
        private void Settings(ref float y, float w, int fs)
        {
            Row(ref y, w, fs, BtcConfig.OnlineAllowJoin, Tt("Others can join my lobby from this list", "Другие могут заходить в моё лобби из этого списка"),
                Tt("Also into your running match when BetterConnection is on (spectator / returning player).", "А также в идущий матч, если включён BetterConnection (наблюдатель / вернувшийся игрок)."));
            Row(ref y, w, fs, BtcConfig.OnlineShowIp, Tt("Show my address to others", "Показывать мой адрес другим"),
                Tt("Your Radmin / Hamachi / LAN address on your card. Joining from the list works without it.", "Ваш адрес Radmin / Hamachi / LAN на карточке. Заход из списка работает и без него."));
            Row(ref y, w, fs, BtcConfig.OnlineInvites, Tt("Accept invites", "Принимать приглашения"), Tt("A host can invite you: a card with a Join button appears at the top.", "Хост может пригласить вас: сверху появится карточка с кнопкой «Зайти»."));
            Row(ref y, w, fs, BtcConfig.OnlineChat, Tt("Chat", "Чат"), null);
            Row(ref y, w, fs, BtcConfig.OnlineChatNotify, Tt("Notify about the common chat", "Уведомлять об общем чате"), Tt("Direct messages always notify.", "О личных сообщениях уведомляет всегда."));
            TextRow(ref y, w, fs, BtcConfig.OnlineMyIp, "myip", Tt("My address for others (empty = auto)", "Мой адрес для других (пусто = авто)"), BtcPresence.MyIp);
            TextRow(ref y, w, fs, BtcConfig.OnlineManual, "extra", Tt("Extra addresses to ask (comma separated)", "Доп. адреса для опроса (через запятую)"), "26.1.2.3, 26.4.5.6");
            BtcUi.Note(ref y, w, fs, Tt("Uses UDP port ", "Использует UDP порт ") + (BtcConfig.OnlinePort != null ? BtcConfig.OnlinePort.Value : 47781) +
                Tt(" (not the game connection). If someone does not show up, add their Radmin IP above.", " (не игровое соединение). Если кого-то не видно, добавьте его Radmin IP выше."));
            y += fs * 0.5f;
        }

        private void Row(ref float y, float w, int fs, BepInEx.Configuration.ConfigEntry<bool> c, string label, string hint)
        {
            if (c == null) return;
            if (BtcVisualRows.Switch(ref y, w, fs, 0, label, c.Value, hint)) Flip(c, label);
        }

        private readonly Dictionary<string, string> _edit = new Dictionary<string, string>();

        private void TextRow(ref float y, float w, int fs, BepInEx.Configuration.ConfigEntry<string> c, string key, string label, string placeholder)
        {
            if (c == null) return;
            Txt(new Rect(fs * 1.5f, y, w - fs * 3f, fs * 1.4f), label, _l2, new Color(1f, 1f, 1f, 0.7f));
            y += fs * 1.4f;
            string cur;
            if (!_edit.TryGetValue(key, out cur)) cur = c.Value;
            Rect fr = new Rect(fs * 1.5f, y, w - fs * 3f - fs * 2.4f, fs * 1.6f);
            BtcUi.Field(fr);
            GUI.SetNextControlName("online:" + key);
            string nv = GUI.TextField(fr, cur, 300);
            if (nv.Length == 0 && GUI.GetNameOfFocusedControl() != "online:" + key && !string.IsNullOrEmpty(placeholder)) Txt(new Rect(fr.x + fs * 0.4f, fr.y, fr.width, fr.height), placeholder, _l2, new Color(1f, 1f, 1f, 0.3f));
            _edit[key] = nv;
            Rect ok = new Rect(fr.xMax + fs * 0.4f, fr.y, fs * 1.6f, fs * 1.6f);
            if (nv.Trim() != c.Value && BtcReplayIcons.Button(ok, BtcReplayIcons.Check, false, Tt("Apply", "Применить")))
            {
                string was = c.Value;
                c.Value = nv.Trim();
                _edit.Remove(key);
                BtcSfx.Ok();
                BtcUndo.Push(Scope, label, delegate { c.Value = was; });
            }
            y += fs * 2.1f;
        }

        // ------------------------------------------------------------------ chat
        private void DrawChat(Rect r, int fs)
        {
            Rect box = new Rect(r.x + fs * 0.4f, r.y + fs * 0.2f, r.width - fs * 1.2f, r.height - fs * 0.6f);
            BtcReplayUI.Fill(box, new Color(0f, 0f, 0f, 0.22f));
            if (BtcConfig.OnlineChat != null && !BtcConfig.OnlineChat.Value)
            {
                Txt(new Rect(box.x + fs, box.y + fs, box.width - fs * 2f, fs * 1.5f), Tt("Chat is off (Settings)", "Чат выключен (Настройки)"), _l2, new Color(1f, 1f, 1f, 0.5f));
                return;
            }
            string ch = Channel;
            BtcPresence.Peer dm = null;
            if (ch != "all") { BtcPresence.Peers.TryGetValue(ch, out dm); if (dm == null) { ch = "all"; Channel = ch; } }
            if (Watching(ch)) BtcPresence.Unread.Remove(ch);

            // channel bar: All + direct chats that have messages
            float x = box.x + fs * 0.4f, cy = box.y + fs * 0.35f, chh = fs * 1.5f;
            x = ChanChip(x, cy, fs, "all", Tt("All online", "Все онлайн"), ch);
            HashSet<string> chans = new HashSet<string>();
            for (int i = BtcPresence.Chat.Count - 1; i >= 0 && chans.Count < 6; i--) { string c = BtcPresence.Chat[i].Ch; if (c != "all") chans.Add(c); }
            if (ch != "all") chans.Add(ch);
            foreach (string c in chans)
            {
                BtcPresence.Peer pp; BtcPresence.Peers.TryGetValue(c, out pp);
                if (x > box.xMax - fs * 6f) break;
                x = ChanChip(x, cy, fs, c, pp != null ? pp.Nick : "?", ch);
            }
            Rect clr = new Rect(box.xMax - fs * 1.9f, cy, chh, chh);
            if (BtcReplayIcons.Button(clr, BtcReplayIcons.Trash, false, Tt("Clear this chat (Ctrl+Z brings it back)", "Очистить этот чат (Ctrl+Z вернёт)")))
            {
                List<BtcPresence.Msg> gone = BtcPresence.ClearChannel(ch);
                if (gone.Count > 0) { BtcSfx.Delete(); BtcUndo.Push(Scope, Tt("Clear chat", "Очистка чата"), delegate { BtcPresence.RestoreChannel(gone); }); }
            }

            // messages
            float top = cy + chh + fs * 0.4f, inputH = fs * 1.9f;
            Rect view = new Rect(box.x, top, box.width, box.yMax - top - inputH - fs * 0.6f);
            float w = view.width - 14f;
            int count = 0;
            for (int i = 0; i < BtcPresence.Chat.Count; i++) if (BtcPresence.Chat[i].Ch == ch) count++;
            bool stick = _chatScroll.y >= _chatH - view.height - fs * 2f;
            if (count != _chatCount) { if (stick || _chatCount < 0) _chatScroll.y = 1e6f; _chatCount = count; }
            _chatScroll = GUI.BeginScrollView(view, _chatScroll, new Rect(0f, 0f, w, Mathf.Max(_chatH, view.height)));
            float y = fs * 0.3f;
            if (count == 0)
                Txt(new Rect(fs * 0.6f, y, w - fs * 1.2f, fs * 1.4f), ch == "all" ? Tt("Say hi to everyone online", "Напишите всем, кто в сети") : Tt("Direct chat with ", "Личный чат с ") + (dm != null ? dm.Nick : ""), _l2, new Color(1f, 1f, 1f, 0.4f));
            for (int i = 0; i < BtcPresence.Chat.Count; i++)
            {
                BtcPresence.Msg m = BtcPresence.Chat[i];
                if (m.Ch != ch) continue;
                string head = (m.Mine ? Tt("You", "Вы") : m.Nick) + "  " + new DateTime(m.Utc, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm");
                Color nc = m.Mine ? BtcMenu.Accent : NickColor(m.From);
                Txt(new Rect(fs * 0.6f, y, w - fs * 1.2f, fs * 1.2f), head, _msgNick, nc);
                y += fs * 1.15f;
                float th = _msg.CalcHeight(new GUIContent(m.Text), w - fs * 1.2f);
                GUI.Label(new Rect(fs * 0.6f, y, w - fs * 1.2f, th), m.Text, _msg);
                y += th + fs * 0.45f;
            }
            _chatH = y + fs * 0.3f;
            GUI.EndScrollView();

            // input
            if (_draft == null) _draft = BtcMenuState.Get("online:draft", "");
            Rect fr = new Rect(box.x + fs * 0.4f, box.yMax - inputH - fs * 0.3f, box.width - fs * 0.8f - inputH - fs * 0.3f, inputH);
            Event e = Event.current;
            bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "online:chat";
            BtcUi.Field(fr);
            GUI.SetNextControlName("online:chat");
            string nv = GUI.TextField(fr, _draft, BtcPresence.MaxChat);
            if (nv.Length == 0 && GUI.GetNameOfFocusedControl() != "online:chat")
                Txt(new Rect(fr.x + fs * 0.4f, fr.y, fr.width, fr.height), ch == "all" ? Tt("Message to everyone online...", "Сообщение всем в сети...") : Tt("Message to ", "Сообщение для ") + (dm != null ? dm.Nick : ""), _l2, new Color(1f, 1f, 1f, 0.3f));
            if (nv != _draft) { _draft = nv; BtcMenuState.Set("online:draft", nv); }
            Rect send = new Rect(fr.xMax + fs * 0.3f, fr.y, inputH, inputH);
            bool click = BtcReplayIcons.Button(send, BtcReplayIcons.Next, false, Tt("Send (Enter)", "Отправить (Enter)"));
            if ((click || enter) && _draft.Trim().Length > 0)
            {
                string why;
                if (BtcPresence.SendChat(ch, _draft, out why)) { _draft = ""; BtcMenuState.Set("online:draft", ""); _chatScroll.y = 1e6f; }
                if (why != null) BtcMenu.Toast(why, BtcReplayIcons.Chat);
                if (enter) e.Use();
            }
        }

        private float ChanChip(float x, float y, int fs, string id, string label, string cur)
        {
            int unread; BtcPresence.Unread.TryGetValue(id, out unread);
            string text = label + (unread > 0 ? "  " + unread : "");
            float w = Mathf.Min(_chip.CalcSize(new GUIContent(text)).x + fs * 1.2f, fs * 9f);
            Rect r = new Rect(x, y, w, fs * 1.5f);
            bool sel = id == cur;
            BtcReplayUI.Fill(r, sel ? BtcMenu.AccentA(0.3f) : new Color(1f, 1f, 1f, r.Contains(Event.current.mousePosition) ? 0.1f : 0.05f));
            Txt(r, text, _chip, unread > 0 && !sel ? new Color(1f, 0.55f, 0.5f, 1f) : (sel ? Color.white : new Color(1f, 1f, 1f, 0.7f)));
            if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) { Channel = id; BtcPresence.Unread.Remove(id); _chatCount = -1; }
            return r.xMax + fs * 0.35f;
        }

        private static Color NickColor(string id)
        {
            BtcPresence.Peer p;
            if (BtcPresence.Peers.TryGetValue(id, out p) && p.PlateA >= 0)
            {
                Color c = Hex(p.PlateA, Color.white);
                if (c.r + c.g + c.b >= 0.6f) return c;
            }
            return new Color(0.75f, 0.8f, 0.9f, 1f);
        }
    }
}
