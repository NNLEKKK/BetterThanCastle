using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// BetterConnection tab (menu tab 2) in the new menu style: header master switch (locked while hosting or connected), live status card,
    /// on-screen options, diagnostics (copy report, open log folder) and the session block (pause, client pause permission, ping rows with
    /// colour and host kick). UI only: with BetterConnection OFF nothing of this touches the wire. Undo scope "bc".
    /// </summary>
    internal sealed class BtcBcTab : BtcMenuTab
    {
        internal static readonly BtcBcTab Instance = new BtcBcTab();

        internal override int Id { get { return 2; } }
        internal override bool IsActive { get { return BtcMenu.CurrentId == 2; } }
        internal override string UndoScope { get { return Scope; } }

        internal const string Scope = "bc";

        private Vector2 _scroll;
        private float _h;
        private GUIStyle _ttl, _val;
        private int _fs;
        private int _confirmId = -1;
        private float _confirmUntil;
        private readonly List<string> _keys = new List<string>();

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private void Styles(int fs)
        {
            if (_ttl != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _ttl = new GUIStyle(b); _ttl.fontSize = Mathf.RoundToInt(fs * 1.15f); _ttl.fontStyle = FontStyle.Bold; _ttl.alignment = TextAnchor.MiddleLeft; _ttl.normal.textColor = Color.white;
            _val = new GUIStyle(b); _val.fontSize = Mathf.RoundToInt(fs * 0.95f); _val.alignment = TextAnchor.MiddleRight; _val.normal.textColor = Color.white;
        }

        internal override void Draw(Rect area, int fs)
        {
            Styles(fs);
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == BtcMenu.MenuKeyMain)
            {
                GUIUtility.keyboardControl = 0;
                BtcMenu.PgDownFrame = Time.frameCount;
                ResourcePackManager.CloseMenu();
                e.Use();
                return;
            }
            BtcUi.ClickAway(e);
            float pad = fs, ib = fs * 1.9f;
            float y0 = area.y + fs * 0.4f;
            GUI.Label(new Rect(area.x + pad, y0, area.width * 0.5f, ib), "BetterConnection", _ttl);

            bool on = BtcBc.BcEnabled, can = BtcBc.BcCanChangeEnabled;
            float x = area.xMax - pad - fs * 2.4f;
            Rect sw = new Rect(x, y0 + (ib - fs * 1.2f) * 0.5f, fs * 2.4f, fs * 1.2f);
            string tip = !can ? Tt("Locked while hosting or connected: leave the lobby or game to switch it", "Заблокировано, пока вы хост или подключены: выйдите из лобби или игры")
                : (on ? Tt("BetterConnection is ON (click to switch to vanilla networking)", "BetterConnection включён (клик - ванильная сеть)") : Tt("BetterConnection is OFF: vanilla networking (click to turn on)", "BetterConnection выключен: ванильная сеть (клик - включить)"));
            if (BtcUi.HeaderSwitch(sw, on, tip))
            {
                if (!can) { BtcSfx.Warn(); BtcMenu.Toast(Tt("Locked while hosting or connected", "Заблокировано, пока вы в сетевой игре"), BtcReplayIcons.Lock); }
                else
                {
                    BtcBc.BcSetEnabled(!on);
                    BtcSfx.Switch(!on);
                    bool was = on;
                    BtcUndo.Push(Scope, "BetterConnection", delegate { BtcBc.BcSetEnabled(was); });
                }
            }
            if (!can) BtcReplayIcons.Glyph(BtcReplayIcons.Lock, new Rect(sw.x - fs * 1.6f, sw.y - fs * 0.1f, fs * 1.3f, fs * 1.3f), new Color(1f, 1f, 1f, 0.55f));

            float y = y0 + ib + fs * 0.5f;
            float fx = area.x + pad;
            bool allOpen = true;
            for (int i = 0; i < _keys.Count; i++) if (!BtcUi.IsOpen(_keys[i])) { allOpen = false; break; }
            if (on && BtcReplayIcons.Plain(new Rect(fx, y, ib, ib), allOpen ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, Color.white, allOpen ? Tt("Collapse all sections", "Свернуть все разделы") : Tt("Expand all sections", "Развернуть все разделы")))
                BtcUi.SetOpen(_keys, !allOpen);
            if (on && BtcReplayIcons.Plain(new Rect(fx + ib + 3f, y, ib, ib), BtcReplayIcons.Copy, Color.white, Tt("Copy a connection report to the clipboard", "Скопировать отчёт о соединении в буфер")))
            {
                GUIUtility.systemCopyBuffer = Report();
                BtcSfx.Ok();
                BtcMenu.Toast(Tt("Connection report copied", "Отчёт о соединении скопирован"), BtcReplayIcons.Copy);
            }
            if (on) y += ib + fs * 0.6f; else y += fs * 0.2f;
            BtcReplayUI.Fill(new Rect(area.x + pad, y - fs * 0.25f, area.width - pad * 2f, 1f), BtcMenu.Line);

            Rect view = new Rect(area.x, y, area.width, area.yMax - y);
            float vw = view.width - (_h > view.height ? 14f : 0f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, vw, _h));
            float cy = fs * 0.2f;
            _keys.Clear();
            Body(ref cy, vw, fs, on);
            GUI.EndScrollView();
            _h = cy + fs * 3f;
            BtcReplayIcons.DrawTip(fs, area.xMax, false);
        }

        private static string StatusLoc(string s, out Color c)
        {
            c = new Color(0.55f, 0.85f, 0.55f, 1f);
            if (s.StartsWith("RECOVERING")) { c = new Color(0.95f, 0.78f, 0.1f, 1f); return Tt("Recovering the connection...", "Восстанавливаем соединение..."); }
            if (s.StartsWith("HARDPAUSED")) { c = new Color(0.9f, 0.3f, 0.3f, 1f); return Tt("Connection lost: game on hard pause", "Связь потеряна: игра на жёсткой паузе"); }
            if (s.StartsWith("PAUSED BY")) { c = new Color(0.95f, 0.78f, 0.1f, 1f); return Tt("Paused by " + s.Substring(10), "Пауза от " + s.Substring(10)); }
            if (s.StartsWith("Not in")) { c = new Color(1f, 1f, 1f, 0.5f); return Tt("Not in a network session", "Нет сетевой сессии"); }
            return Tt("Connection OK", "Соединение в порядке");
        }

        private void Body(ref float y, float w, int fs, bool on)
        {
            if (!on)
            {
                BtcUi.Status(ref y, w, fs, Tt("Vanilla networking", "Ванильная сеть"), new Color(1f, 1f, 1f, 0.8f));
                BtcUi.Note(ref y, w, fs, Tt("Off: nothing is added on the wire, so you can play with vanilla players as host or client. Turn it on for improved connection: heartbeat, automatic reconnect instead of being kicked to the menu, pings and voluntary pause. Everybody in the game then needs BetterConnection too.", "Выкл: в сеть ничего не добавляется, можно играть с ванильными игроками как хост или клиент. Включите для улучшенного соединения: heartbeat, автопереподключение вместо вылета в меню, пинги и добровольная пауза. Тогда BetterConnection нужен у всех в игре."));
                return;
            }

            Color sc;
            bool inSession = BtcBc.BcInSession;
            string st = StatusLoc(BtcBc.BcStatusText(), out sc);
            {
                float pad = fs, h = fs * 3.2f;
                Rect card = new Rect(pad, y, w - pad * 2f, h);
                BtcReplayUI.Fill(card, new Color(1f, 1f, 1f, 0.04f));
                BtcReplayUI.Fill(new Rect(card.x, card.y + h * 0.15f, 3f, h * 0.7f), sc);
                BtcReplayUI.Fill(new Rect(card.x + fs * 0.9f, card.center.y - fs * 0.35f, fs * 0.7f, fs * 0.7f), sc);
                GUI.Label(new Rect(card.x + fs * 2.3f, card.y, card.width - fs * 3f, h * 0.6f), st, _ttl);
                string role = !inSession ? Tt("Options below apply to the next game", "Настройки ниже действуют со следующей игры") : (BtcBc.BcIsHost ? Tt("You are the host", "Вы хост") : Tt("You are a client", "Вы клиент"));
                GUI.Label(new Rect(card.x + fs * 2.3f, card.y + h * 0.5f, card.width - fs * 3f, h * 0.45f), role, Sub());
                y += h + fs * 0.5f;
            }
            BtcUi.Note(ref y, w, fs, Tt("On: improved connection (heartbeat, auto-reconnect, pings, voluntary pause). Everyone in the game needs BetterConnection.", "Вкл: улучшенное соединение (heartbeat, автопереподключение, пинги, добровольная пауза). BetterConnection нужен у всех в игре."));

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("bc:screen"), Tt("On screen", "На экране"), 0))
            {
                BtcUi.ToggleUndo(ref y, w, fs, Scope, Tt("Show ping", "Показывать пинг"), BtcBc.BcShowPing, Tt("Ping of the players in the corner during a game", "Пинг игроков в углу во время игры"), BtcBc.BcSetShowPing);
                BtcUi.ToggleUndo(ref y, w, fs, Scope, Tt("Show \"Client pause: allowed\" label", "Показывать метку \"Пауза клиентов: разрешена\""), BtcBc.BcShowPauseLabel, null, BtcBc.BcSetShowPauseLabel);
                y += fs * 0.4f;
            }
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("bc:diag"), Tt("Diagnostics", "Диагностика"), 0))
            {
                BtcUi.ToggleUndo(ref y, w, fs, Scope, Tt("Diagnostic log", "Диагностический лог"), BtcBc.BcDiagLog, "unet_netdiag_*.log", BtcBc.BcSetDiagLog);
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Copy, Tt("Copy connection report", "Скопировать отчёт о соединении"), Tt("Status, role and pings as text, for a bug report", "Статус, роль и пинги текстом, для баг-репорта")))
                {
                    GUIUtility.systemCopyBuffer = Report();
                    BtcSfx.Ok();
                    BtcMenu.Toast(Tt("Connection report copied", "Отчёт о соединении скопирован"), BtcReplayIcons.Copy);
                }
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Folder, Tt("Open the log folder", "Открыть папку логов"), null)) OpenDir(BtcPaths.Debug);
                y += fs * 0.4f;
            }
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("bc:session"), Tt("Session", "Сессия"), 0))
            {
                if (!inSession)
                    BtcUi.Note(ref y, w, fs, Tt("Pause, pings and client permissions appear here during a network game.", "Пауза, пинги и права клиентов появятся здесь во время сетевой игры."));
                else
                {
                    bool paused = BtcBc.BcVoluntaryPauseActive;
                    if (BtcUi.Action(ref y, w, fs, paused ? BtcReplayIcons.Play : BtcReplayIcons.Pause, paused ? Tt("Resume the game", "Продолжить игру") : Tt("Pause the game", "Поставить игру на паузу"), paused ? Tt("Voluntary pause is on", "Добровольная пауза включена") : null))
                        BtcBc.BcTogglePause();
                    bool can = BtcBc.BcClientsCanPause;
                    if (BtcBc.BcIsHost)
                        BtcUi.ToggleUndo(ref y, w, fs, Scope, Tt("Clients may pause the game", "Клиенты могут ставить паузу"), can, null, delegate (bool nv) { if (BtcBc.BcClientsCanPause != nv) BtcBc.BcToggleClientPausePermission(); });
                    else
                        BtcUi.Status(ref y, w, fs, Tt("Pause allowed by the host: ", "Пауза разрешена хостом: ") + (can ? Tt("yes", "да") : Tt("no", "нет")), new Color(1f, 1f, 1f, 0.75f));
                    y += fs * 0.3f;
                    BtcUi.Section(ref y, w, fs, Tt("Pings", "Пинги"));
                    PingRows(ref y, w, fs);
                }
                y += fs * 0.4f;
            }
        }

        private string KeyAdd(string k) { _keys.Add(k); return k; }

        private GUIStyle _subSt;
        private GUIStyle Sub()
        {
            if (_subSt == null || _subSt.fontSize != Mathf.RoundToInt(_fs * 0.78f))
            {
                _subSt = new GUIStyle(_ttl); _subSt.fontStyle = FontStyle.Normal; _subSt.fontSize = Mathf.RoundToInt(_fs * 0.78f); _subSt.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            }
            return _subSt;
        }

        private static bool ParsePing(string line, out int ms)
        {
            ms = -1;
            int i = line.LastIndexOf(": ");
            if (i < 0) return false;
            string v = line.Substring(i + 2).Trim();
            if (!v.EndsWith("ms")) return false;
            return int.TryParse(v.Substring(0, v.Length - 2), out ms);
        }

        private void PingRows(ref float y, float w, int fs)
        {
            List<string> lines = BtcBc.BcGetPingLines();
            if (lines.Count == 0) { BtcUi.Note(ref y, w, fs, Tt("No data yet", "Данных пока нет")); return; }
            NetworkConnection[] conns = BtcKick.RemoteConns();
            bool kick = NetworkServer.active && conns.Length == lines.Count;
            float pad = fs, rh = fs * 2.1f;
            for (int i = 0; i < lines.Count; i++)
            {
                string name = BtcKick.PlayerOfLine(lines[i]);
                int ms;
                bool ok = ParsePing(lines[i], out ms);
                Color pc = !ok ? new Color(1f, 1f, 1f, 0.5f) : (ms < 80 ? new Color(0.55f, 0.85f, 0.55f, 1f) : (ms < 150 ? new Color(0.95f, 0.78f, 0.1f, 1f) : new Color(0.9f, 0.3f, 0.3f, 1f)));
                Rect row = new Rect(pad, y, w - pad * 2f, rh);
                BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.03f));
                BtcReplayUI.Fill(new Rect(row.x + fs * 0.7f, row.center.y - fs * 0.3f, fs * 0.6f, fs * 0.6f), pc);
                string badge = lines[i].StartsWith("[P] ") ? "  " + Tt("(paused)", "(пауза)") : (lines[i].StartsWith("[!] ") ? "  " + Tt("(reconnecting)", "(переподключение)") : "");
                float kw = kick ? fs * 5f : 0f;
                GUI.Label(new Rect(row.x + fs * 2f, row.y, row.width - fs * 2f - kw - fs * 6f, rh), name + badge + BtcHandicap.Label(name), Name());
                _val.normal.textColor = pc;
                GUI.Label(new Rect(row.xMax - kw - fs * 6.5f, row.y, fs * 6f, rh), ok ? ms + " ms" : "...", _val);
                if (kick)
                {
                    NetworkConnection c = conns[i];
                    bool conf = _confirmId == c.connectionId && Time.unscaledTime < _confirmUntil;
                    Rect kr = new Rect(row.xMax - kw - fs * 0.4f, row.y + rh * 0.1f, kw, rh * 0.8f);
                    BtcReplayUI.Fill(kr, conf ? new Color(0.9f, 0.3f, 0.3f, 0.35f) : new Color(1f, 1f, 1f, kr.Contains(Event.current.mousePosition) ? 0.14f : 0.07f));
                    GUI.Label(kr, conf ? Tt("Sure?", "Точно?") : Tt("Kick", "Кик"), Center());
                    if (BtcSfx.Btn(kr, "", BtcReplayUI.sBtn))
                    {
                        if (conf) { _confirmId = -1; BtcSfx.Delete(); BtcKick.Kick(c, name); y += rh + 3f; return; }
                        _confirmId = c.connectionId; _confirmUntil = Time.unscaledTime + 3f;
                    }
                }
                y += rh + 3f;
            }
        }

        private GUIStyle _nameSt, _centerSt;
        private GUIStyle Name()
        {
            if (_nameSt == null) { _nameSt = new GUIStyle(_ttl); _nameSt.fontStyle = FontStyle.Normal; _nameSt.fontSize = Mathf.RoundToInt(_fs * 0.95f); _nameSt.clipping = TextClipping.Clip; }
            return _nameSt;
        }
        private GUIStyle Center()
        {
            if (_centerSt == null) { _centerSt = new GUIStyle(Name()); _centerSt.alignment = TextAnchor.MiddleCenter; }
            return _centerSt;
        }

        private static string Report()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("BetterConnection: ").Append(BtcBc.BcEnabled ? "ON" : "OFF").Append("\r\n");
            sb.Append("Status: ").Append(BtcBc.BcStatusText()).Append("\r\n");
            sb.Append("Role: ").Append(!BtcBc.BcInSession ? "none" : (BtcBc.BcIsHost ? "host" : "client")).Append("\r\n");
            sb.Append("Show ping: ").Append(BtcBc.BcShowPing).Append(", diag log: ").Append(BtcBc.BcDiagLog).Append("\r\n");
            List<string> l = BtcBc.BcGetPingLines();
            for (int i = 0; i < l.Count; i++) sb.Append("  ").Append(l[i]).Append("\r\n");
            return sb.ToString();
        }

        private static void OpenDir(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex) { BtcLog.Warn("Bc", "open folder failed: " + ex.Message); }
        }
    }
}
