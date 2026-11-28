using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Library side of matches split over saves (BtcReplayChain): the match card (first session stands for all, sessions badge, whole length,
    /// players of every session), its details panel (sessions strip with joints and branch lanes, session rows, branches, play / export / unlink)
    /// and the merge mode (pick replays, glue them into one match).
    /// </summary>
    internal static partial class BtcReplayLibrary
    {
        private static bool _merging;
        private static readonly List<string> _picks = new List<string>();

        private static float DurOf(BtcReplay.Data d)
        {
            BtcReplayChain.Match m = BtcReplayChain.Of(d);
            return m != null && m.Main != null ? m.Main.Total : d.Duration;
        }

        private static List<Pl> MatchPlayers(BtcReplayChain.Match m)
        {
            List<Pl> r = new List<Pl>();
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < m.Nodes.Count; i++)
            {
                List<Pl> p = ParsePlayers(m.Nodes[i].D);
                for (int k = 0; k < p.Count; k++) if (seen.Add(p[k].Nick.ToLowerInvariant())) r.Add(p[k]);
            }
            return r;
        }

        private static void PlayItem(BtcReplay.Data d)
        {
            BtcReplayChain.Match m = BtcReplayChain.Of(d);
            if (m != null) BtcReplayChainPlay.Start(m, null, 0f);
            else BtcReplay.StartReplay(d);
            if (BtcReplay.Mode == BtcReplay.State.Playing) ResourcePackManager.CloseMenu();
        }

        /// <summary>"3 sessions" pill after the other badges; returns the x where the next thing starts.</summary>
        private static float SessionsBadge(Rect at, BtcReplay.Data d, int fs)
        {
            BtcReplayChain.Match m = BtcReplayChain.Of(d);
            if (m == null) return at.x;
            string n = m.Sessions.ToString();
            Rect r = new Rect(at.x, at.y, at.height * 1.15f + fs * (0.35f + n.Length * 0.5f), at.height);
            BtcReplayUI.Fill(r, new Color(0f, 0f, 0f, 0.6f));
            BtcReplayIcons.Glyph(BtcReplayIcons.Sessions, new Rect(r.x + 1f, r.y, r.height, r.height), Accent);
            Txt(new Rect(r.x + r.height, r.y, r.width - r.height, r.height), n, _badge, Accent);
            BtcReplayIcons.Hint(r, Tt("One match in ", "Одна катка из ") + n + Tt(" sessions (continued from saves)", " сессий (продолжена с сейвов)") + (m.Lines.Count > 1 ? Tt(", with branches", ", есть ветки") : ""));
            return r.xMax + fs * 0.3f;
        }

        // ------------------------------------------------------------------ merge mode
        private static void ToggleMerge()
        {
            _merging = !_merging;
            _picks.Clear();
            if (_merging) { _selected = null; BtcSfx.Open(); } else BtcSfx.Close();
        }

        private static void TogglePick(BtcReplay.Data d)
        {
            if (d.Error != null || !d.Complete || d.SnapBytes <= 0) { BtcSfx.Miss(); BtcToast.Show(Tt("This replay cannot be played, so it cannot be merged.", "Этот реплей нельзя проиграть, поэтому его нельзя склеить.")); return; }
            if (_picks.Contains(d.FileName)) _picks.Remove(d.FileName); else _picks.Add(d.FileName);
        }

        private static void PickMark(Rect r, BtcReplay.Data d, int fs)
        {
            bool on = _picks.Contains(d.FileName);
            float s = fs * 1.3f;
            Rect b = new Rect(r.x + fs * 0.35f, r.y + (_viewMode == 2 ? (r.height - s) * 0.5f : fs * 0.35f), s, s);
            if (_viewMode == 2) b.x = r.xMax - s - fs * 2.4f;
            BtcReplayUI.Fill(b, on ? Accent : new Color(0f, 0f, 0f, 0.7f));
            if (on)
            {
                BtcReplayIcons.Glyph(BtcReplayIcons.Check, b, new Color(0.02f, 0.05f, 0.03f, 1f));
                BtcReplayUI.Fill(new Rect(r.x, r.y, r.width, 2f), Accent); BtcReplayUI.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), Accent);
                BtcReplayUI.Fill(new Rect(r.x, r.y, 2f, r.height), Accent); BtcReplayUI.Fill(new Rect(r.xMax - 2f, r.y, 2f, r.height), Accent);
            }
            else { BtcReplayUI.Fill(new Rect(b.x, b.y, b.width, 1f), new Color(1f, 1f, 1f, 0.5f)); BtcReplayUI.Fill(new Rect(b.x, b.yMax - 1f, b.width, 1f), new Color(1f, 1f, 1f, 0.5f)); BtcReplayUI.Fill(new Rect(b.x, b.y, 1f, b.height), new Color(1f, 1f, 1f, 0.5f)); BtcReplayUI.Fill(new Rect(b.xMax - 1f, b.y, 1f, b.height), new Color(1f, 1f, 1f, 0.5f)); }
        }

        private static float DrawMergeBar(Event e, float y, float w, int fs, float pad)
        {
            float h = fs * 2.4f;
            Rect p = new Rect(pad, y, w - pad * 2f, h);
            BtcReplayUI.Fill(p, new Color(Accent.r, Accent.g, Accent.b, 0.12f));
            BtcReplayUI.Fill(new Rect(p.x, p.y, 3f, p.height), Accent);
            float g = h * 0.6f;
            BtcReplayIcons.Glyph(BtcReplayIcons.Link, new Rect(p.x + fs * 0.6f, p.center.y - g * 0.5f, g, g), Accent);
            string msg = _picks.Count == 0
                ? Tt("Click the sessions of one match (any order): they are glued in recording order", "Кликните сессии одной катки (в любом порядке): они склеятся по времени записи")
                : Tt("Picked: ", "Выбрано: ") + _picks.Count + Tt("  - each next one continues the end of the previous one", "  - каждая следующая продолжает конец предыдущей");
            Txt(new Rect(p.x + fs * 2.4f, p.y, p.width - fs * 17f, h), msg, _lbl, Color.white);
            float bw = fs * 7.5f, bh = h - fs * 0.6f;
            Rect ok = new Rect(p.xMax - bw * 2f - fs * 1.2f, p.y + fs * 0.3f, bw, bh);
            bool can = _picks.Count >= 2;
            BtcReplayUI.Fill(ok, can ? new Color(Accent.r, Accent.g, Accent.b, ok.Contains(e.mousePosition) ? 0.55f : 0.4f) : new Color(1f, 1f, 1f, 0.06f));
            BtcReplayIcons.Glyph(BtcReplayIcons.Link, new Rect(ok.x + fs * 0.3f, ok.center.y - bh * 0.35f, bh * 0.7f, bh * 0.7f), can ? Color.white : new Color(1f, 1f, 1f, 0.35f));
            Txt(new Rect(ok.x + bh, ok.y, ok.width - bh, ok.height), Tt("Merge", "Склеить"), _lbl, can ? Color.white : new Color(1f, 1f, 1f, 0.35f));
            if (BtcSfx.Btn(ok, "", BtcReplayUI.sBtn) && can) DoMerge();
            Rect cn = new Rect(ok.xMax + fs * 0.6f, ok.y, bw, bh);
            BtcReplayUI.Fill(cn, new Color(1f, 1f, 1f, cn.Contains(e.mousePosition) ? 0.12f : 0.06f));
            Txt(new Rect(cn.x + fs * 0.6f, cn.y, cn.width, cn.height), Tt("Cancel", "Отмена"), _lbl, new Color(1f, 1f, 1f, 0.8f));
            if (BtcSfx.Btn(cn, "", BtcReplayUI.sBtn)) ToggleMerge();
            return y + h + fs * 0.5f;
        }

        private static void DoMerge()
        {
            List<BtcReplay.Data> ds = new List<BtcReplay.Data>();
            for (int i = 0; i < BtcReplayUI.list.Count; i++) if (_picks.Contains(BtcReplayUI.list[i].FileName)) ds.Add(BtcReplayUI.list[i]);
            string err = BtcReplayChain.Merge(ds);
            if (err != null) { BtcSfx.Warn(); BtcToast.Show(err); return; }
            BtcReplay.Data first = null;
            for (int i = 0; i < ds.Count; i++) if (first == null || ds[i].Ticks < first.Ticks) first = ds[i];
            BtcReplayChain.Match m = first != null ? BtcReplayChain.Of(first) : null;
            _merging = false;
            _picks.Clear();
            _selected = m != null ? m.Root.D.FileName : null;
            _viewKey = "";
            BtcToast.Show(Tt("Merged into one match (Ctrl+Z to undo)", "Склеено в одну катку (Ctrl+Z - отменить)"));
        }

        // ------------------------------------------------------------------ match details
        private static string _confirmDeleteAll;

        private static float DrawMatchDetails(Event e, float y, float w, int fs, float pad, BtcReplay.Data d, BtcReplayChain.Match match)
        {
            Meta m = M(d);
            List<Pl> pl = Players(d);
            BtcReplayChain.Line main = match.Main;
            List<BtcReplayChain.Fork> forks = match.Forks(main);
            float rowH = fs * 1.7f;
            int plRows = Mathf.Max(1, (pl.Count + 1) / 2);
            float stripH = fs * 1.2f + 6f + Mathf.Min(forks.Count, 3) * 6f;
            float h = fs * 5.4f + stripH + fs * 0.8f + rowH * main.S.Count + (forks.Count > 0 ? rowH * (forks.Count + 1) : 0f) + rowH * 1.2f + rowH * plRows + fs * 3.8f;
            h = Mathf.Max(fs * 15f, h);
            Rect p = new Rect(pad, y, w - pad * 2f, h);
            BtcReplayUI.Fill(p, new Color(1f, 1f, 1f, 0.05f));
            BtcReplayUI.Fill(new Rect(p.x, p.y, p.width, 2f), Accent);
            float pw = Mathf.Min(p.width * 0.3f, fs * 22f);
            Rect pr = new Rect(p.x, p.y + 2f, pw, p.height - 2f);
            Thumb(pr, d, fs);
            Shade(new Rect(pr.x, pr.y + pr.height * 0.6f, pr.width, pr.height * 0.4f));
            Txt(new Rect(pr.x + fs * 0.7f, pr.yMax - fs * 2.6f, pr.width - fs * 1.4f, fs * 1.6f), MapOf(d), _ttl, Color.white);
            Txt(new Rect(pr.x + fs * 0.7f, pr.yMax - fs * 1.3f, pr.width - fs * 1.4f, fs * 1.1f), (d.Multiplayer ? "MP" : "SP") + "  -  " + InfoOf(d).Format + (string.IsNullOrEmpty(d.Mode) ? "" : "  -  " + d.Mode), _dim, new Color(1f, 1f, 1f, 0.8f));
            float x0 = pr.xMax + fs * 1.2f, rx = p.xMax - fs, y0 = p.y + fs * 0.8f;

            // name
            Txt(new Rect(x0, y0, fs * 5f, rowH), Tt("Name", "Название"), _dim, new Color(1f, 1f, 1f, 0.55f));
            Rect nr = new Rect(x0 + fs * 5f, y0 + 2f, Mathf.Min(fs * 22f, rx - x0 - fs * 5f), rowH - 4f);
            BtcReplayUI.Fill(nr, new Color(1f, 1f, 1f, 0.07f));
            BtcUi.Field(nr);
            string nn = GUI.TextField(nr, m.Name, 40, _tf);
            if (nn != m.Name) { m.Name = nn; Dirty(false); }
            if (m.Name.Length == 0 && GUIUtility.keyboardControl == 0) Txt(new Rect(nr.x + 4f, nr.y, nr.width, nr.height), Title(d), _dim, new Color(1f, 1f, 1f, 0.35f));
            y0 += rowH * 1.2f;

            // summary
            int kf = 0;
            long bytes = 0L;
            for (int i = 0; i < match.Nodes.Count; i++) { if (BtcReplaySeek.HasCacheFor(match.Nodes[i].D.FilePath)) kf++; bytes += match.Nodes[i].D.FileBytes; }
            BtcReplay.Data last = main.S[main.S.Count - 1].N.D;
            BtcReplayIcons.Glyph(BtcReplayIcons.Sessions, new Rect(x0, y0 + rowH * 0.15f, rowH * 0.7f, rowH * 0.7f), Accent);
            Txt(new Rect(x0 + rowH * 0.9f, y0, rx - x0, rowH), Tt("Match  ", "Катка  ") + main.S.Count + Tt(" sessions  -  ", " сесс.  -  ") + BtcReplayUI.Clock(main.Total) + (forks.Count > 0 ? "  -  " + forks.Count + Tt(" other lines", " др. линий") : ""), _lbl, Color.white);
            y0 += rowH * 0.9f;
            string span = (d.Ticks > 0L ? new DateTime(d.Ticks).ToString("dd.MM.yy HH:mm") : "?") + "  ->  " + (last.Ticks > 0L ? new DateTime(last.Ticks).ToString("dd.MM.yy HH:mm") : "?");
            Txt(new Rect(x0, y0, rx - x0, rowH), span + "   " + BtcReplayUI.Size(bytes) + "   " + Tt("keyframes ", "ключ. кадры ") + kf + "/" + match.Nodes.Count + (kf < match.Nodes.Count ? Tt(" (the rest on the first open)", " (остальные при первом открытии)") : ""), _dim, new Color(1f, 1f, 1f, 0.65f));
            y0 += rowH * 1.1f;

            // strip: the sessions of the main line, joints, branch lanes; click = watch from there
            Rect sr = new Rect(x0, y0, rx - x0, fs * 1.1f);
            float total = Mathf.Max(0.001f, main.Total);
            for (int i = 0; i < main.S.Count; i++)
            {
                float a = sr.x + sr.width * main.Off(i) / total, b = sr.x + sr.width * (main.Off(i) + main.S[i].End) / total;
                Rect br = new Rect(a + (i > 0 ? 1.5f : 0f), sr.y, Mathf.Max(2f, b - a - (i > 0 ? 1.5f : 0f)), sr.height);
                bool hot = br.Contains(e.mousePosition);
                BtcReplayUI.Fill(br, new Color(Accent.r, Accent.g, Accent.b, (i % 2 == 0 ? 0.42f : 0.3f) + (hot ? 0.2f : 0f)));
                if (br.width > fs * 2f) Txt(new Rect(br.x + fs * 0.3f, br.y, br.width - fs * 0.4f, br.height), (i + 1).ToString(), _badge, new Color(1f, 1f, 1f, 0.9f));
                if (hot)
                {
                    float g = main.Off(i) + main.S[i].End * Mathf.Clamp01((e.mousePosition.x - br.x) / Mathf.Max(1f, br.width));
                    BtcReplayUI.Fill(new Rect(e.mousePosition.x - 0.5f, sr.y - 2f, 1f, sr.height + 4f), Color.white);
                    BtcReplayIcons.Hint(br, BtcReplayChainUI.SessionTip(main, i) + "\n" + Tt("Click: watch the match from ", "Клик: смотреть катку с ") + BtcReplayUI.Clock(g));
                    if (BtcSfx.Btn(br, "", BtcReplayUI.sBtn)) { Seen.Add(d.FileName); BtcReplayChainPlay.Start(match, main, g); if (BtcReplay.Mode == BtcReplay.State.Playing) ResourcePackManager.CloseMenu(); }
                }
            }
            for (int i = 0; i < forks.Count && i < 3; i++)
            {
                BtcReplayChain.Fork f = forks[i];
                float fx0 = sr.x + sr.width * Mathf.Clamp01(f.At / total), fx1 = Mathf.Min(sr.xMax, fx0 + sr.width * f.Len / total);
                float ly = sr.yMax + 4f + i * 6f;
                Rect hit = new Rect(fx0 - 3f, ly - 3f, Mathf.Max(8f, fx1 - fx0) + 6f, 8f);
                bool hot = hit.Contains(e.mousePosition);
                Color c = BtcReplayChainUI.BranchCol;
                BtcReplayUI.Fill(new Rect(fx0 - 0.5f, sr.yMax, 1.5f, ly - sr.yMax + 2f), new Color(c.r, c.g, c.b, hot ? 1f : 0.7f));
                BtcReplayUI.Fill(new Rect(fx0, ly, Mathf.Max(3f, fx1 - fx0), hot ? 3f : 2f), new Color(c.r, c.g, c.b, hot ? 1f : 0.7f));
                if (hot)
                {
                    BtcReplayIcons.Hint(hit, BtcReplayChainUI.ForkTip(f));
                    if (BtcSfx.Btn(hit, "", BtcReplayUI.sBtn)) { BtcReplayChainPlay.Start(match, f.L, f.At); if (BtcReplay.Mode == BtcReplay.State.Playing) ResourcePackManager.CloseMenu(); }
                }
            }
            y0 += stripH + fs * 0.4f;

            // session rows
            float ib = rowH * 0.85f;
            for (int i = 0; i < main.S.Count; i++)
            {
                BtcReplayChain.Node n = main.S[i].N;
                Rect rr = new Rect(x0, y0, rx - x0, rowH);
                if (rr.Contains(e.mousePosition)) BtcReplayUI.Fill(rr, new Color(1f, 1f, 1f, 0.05f));
                Txt(new Rect(rr.x + fs * 0.3f, y0, fs * 1.6f, rowH), (i + 1).ToString(), _lbl, Accent);
                Txt(new Rect(rr.x + fs * 1.8f, y0, fs * 8f, rowH), n.D.Ticks > 0L ? new DateTime(n.D.Ticks).ToString("dd.MM.yy HH:mm") : "?", _dim, new Color(1f, 1f, 1f, 0.7f));
                string len = BtcReplayUI.Clock(main.S[i].End) + (main.S[i].End < n.Dur - 1f ? Tt("  (of ", "  (из ") + BtcReplayUI.Clock(n.Dur) + ")" : "");
                Txt(new Rect(rr.x + fs * 9.8f, y0, fs * 10f, rowH), len, _lbl, Color.white);
                if (i > 0) Txt(new Rect(rr.x + fs * 19.5f, y0, Mathf.Max(0f, rx - rr.x - fs * 19.5f - ib * 3.5f), rowH), Tt("from the save at ", "с сейва на ") + BtcReplayUI.Clock(main.Off(i)) + (n.Manual ? Tt("  (merged by hand)", "  (склеено вручную)") : ""), _dim, new Color(1f, 1f, 1f, 0.5f));
                float ax = rx - ib;
                if (n.Parent != null)
                {
                    if (BtcReplayIcons.Plain(new Rect(ax, y0 + (rowH - ib) * 0.5f, ib, ib), BtcReplayIcons.Unlink, new Color(1f, 1f, 1f, 0.75f), Tt("Not the same match: unlink this session (and what follows it)", "Это другая катка: отсоединить эту сессию (и то, что после неё)"))) BtcReplayChain.Unlink(n);
                    ax -= ib + 3f;
                }
                if (BtcReplayIcons.Plain(new Rect(ax, y0 + (rowH - ib) * 0.5f, ib, ib), BtcReplayIcons.Play, BtcReplayUI.Green, Tt("Watch the match from this session", "Смотреть катку с этой сессии"))) { Seen.Add(d.FileName); BtcReplayChainPlay.Start(match, main, main.Off(i)); if (BtcReplay.Mode == BtcReplay.State.Playing) ResourcePackManager.CloseMenu(); }
                ax -= ib + 3f;
                if (BtcReplaySeek.HasCacheFor(n.D.FilePath)) BtcReplayIcons.Glyph(BtcReplayIcons.Wave, new Rect(ax, y0 + (rowH - ib) * 0.5f, ib, ib), new Color(Accent.r, Accent.g, Accent.b, 0.8f));
                y0 += rowH;
            }

            // other lines
            if (forks.Count > 0)
            {
                y0 += rowH * 0.2f;
                Txt(new Rect(x0, y0, rx - x0, rowH), Tt("Other lines", "Другие линии"), _ttl, Color.white);
                y0 += rowH;
                for (int i = 0; i < forks.Count; i++)
                {
                    BtcReplayChain.Fork f = forks[i];
                    Rect rr = new Rect(x0, y0, rx - x0, rowH);
                    if (rr.Contains(e.mousePosition)) BtcReplayUI.Fill(rr, new Color(1f, 1f, 1f, 0.05f));
                    BtcReplayIcons.Glyph(BtcReplayIcons.Branch, new Rect(rr.x + fs * 0.2f, y0 + rowH * 0.15f, rowH * 0.7f, rowH * 0.7f), BtcReplayChainUI.BranchCol);
                    Txt(new Rect(rr.x + fs * 1.8f, y0, fs * 18f, rowH), (f.Tail ? Tt("Rest after the save", "Остаток после сейва") : Tt("Another game from the save", "Другая игра с сейва")) + Tt(" at ", " на ") + BtcReplayUI.Clock(f.At), _lbl, Color.white);
                    Txt(new Rect(rr.x + fs * 19.5f, y0, fs * 6f, rowH), "+" + BtcReplayUI.Clock(f.Len), _dim, new Color(1f, 1f, 1f, 0.7f));
                    float ax = rx - ib;
                    if (BtcReplayIcons.Plain(new Rect(ax, y0 + (rowH - ib) * 0.5f, ib, ib), BtcReplayIcons.Star, new Color(1f, 0.85f, 0.25f, 1f), Tt("Make this the main line of the match", "Сделать это главной линией катки"))) BtcReplayChain.MakeMain(match, f.L);
                    ax -= ib + 3f;
                    if (BtcReplayIcons.Plain(new Rect(ax, y0 + (rowH - ib) * 0.5f, ib, ib), BtcReplayIcons.Play, BtcReplayUI.Green, Tt("Watch this line", "Смотреть эту линию"))) { BtcReplayChainPlay.Start(match, f.L, f.At); if (BtcReplay.Mode == BtcReplay.State.Playing) ResourcePackManager.CloseMenu(); }
                    y0 += rowH;
                }
            }

            // players of all sessions, with the average APM over the match where the sessions were opened once
            y0 += rowH * 0.2f;
            int covered;
            Dictionary<string, float> apm = BtcChainApm.AvgApm(main, out covered);
            Txt(new Rect(x0, y0, rx - x0, rowH), Tt("Players", "Игроки") + "  " + pl.Count + (covered > 0 ? Tt("   APM over the match", "   APM за катку") + (covered < main.S.Count ? " (" + covered + "/" + main.S.Count + ")" : "") : ""), _ttl, Color.white);
            y0 += rowH * 1.1f;
            float colW = Mathf.Max(fs * 10f, (rx - x0) / 2f);
            for (int i = 0; i < pl.Count; i++)
            {
                float px = x0 + (i % 2) * colW, py = y0 + (i / 2) * rowH;
                BtcReplayUI.Fill(new Rect(px, py + rowH * 0.2f, fs * 0.9f, fs * 0.9f), pl[i].Col);
                float a;
                string ak = ColorUtility.ToHtmlStringRGB(pl[i].Col);
                bool has = apm.TryGetValue(ak, out a);
                Txt(new Rect(px + fs * 1.3f, py, colW - fs * 6.5f, rowH), Clip(pl[i].Nick, (int)((colW - fs * 5f) / (fs * 0.55f))), _lbl, Color.white);
                if (has) Txt(new Rect(px + colW - fs * 5.4f, py, fs * 5f, rowH), Mathf.RoundToInt(a) + " APM", _dim, new Color(1f, 1f, 1f, 0.7f));
            }

            // actions
            float ab = fs * 2.2f, ay = p.yMax - ab - fs * 0.6f, axx = x0;
            if (BtcReplayIcons.Plain(new Rect(axx, ay, ab * 1.8f, ab), BtcReplayIcons.Play, BtcReplayUI.Green, Tt("Watch the whole match (double click)", "Смотреть всю катку (двойной клик)"))) { Seen.Add(d.FileName); PlayItem(d); }
            axx += ab * 1.8f + 5f;
            if (IsFresh(d))
            {
                if (BtcReplayIcons.Plain(new Rect(axx, ay, ab, ab), BtcReplayIcons.Check, Blue, Tt("Mark as seen", "Отметить просмотренным"))) { Seen.Add(d.FileName); _viewKey = ""; }
                axx += ab + 5f;
            }
            if (Icon(new Rect(axx, ay, ab, ab), BtcReplayIcons.Star, m.Fav, m.Fav ? Tt("Remove from favourites", "Убрать из избранного") : Tt("Add to favourites", "В избранное"))) { m.Fav = !m.Fav; Dirty(_filter == 1); }
            axx += ab + 5f;
            string folderTip = Tt("Folder: ", "Папка: ") + (m.Folder.Length == 0 ? Tt("none", "нет") : m.Folder) + Tt("  (click: next folder)", "  (клик: следующая папка)");
            if (Icon(new Rect(axx, ay, ab, ab), BtcReplayIcons.Folder, m.Folder.Length > 0, folderTip))
            {
                int fi = Folders.IndexOf(m.Folder);
                m.Folder = fi + 1 < Folders.Count ? Folders[fi + 1] : "";
                Dirty(true);
            }
            axx += ab + 5f;
            if (Icon(new Rect(axx, ay, ab, ab), BtcReplayIcons.Export, false, Tt("Save the match as one file (.btcm) and copy it: paste it into a chat; the other player drops it into the replays folder", "Сохранить катку одним файлом (.btcm) и скопировать: вставьте в чат, другой игрок кладёт его в папку реплеев")))
            {
                string path = BtcReplayChain.Export(match, Title(d));
                if (path != null) { BtcReplayShare.CopyFile(path); BtcSfx.Ok(); BtcToast.Show(Tt("Saved as one file and copied: ", "Сохранено одним файлом и скопировано: ") + Path.GetFileName(path)); }
                else { BtcSfx.Warn(); BtcToast.Show(Tt("Could not write the match file (see the log).", "Не удалось записать файл катки (см. лог).")); }
            }
            axx += ab + 5f;
            bool sure = _confirmDeleteAll == d.FileName && Time.unscaledTime < _confirmUntil;
            Rect dr = new Rect(rx - ab, ay, ab, ab);
            if (sure) Txt(new Rect(dr.x - fs * 5f, ay, fs * 4.8f, ab), Tt("Sure?", "Точно?"), _lbl, BtcReplayUI.Red);
            if (BtcReplayIcons.Plain(dr, BtcReplayIcons.Trash, BtcReplayUI.Red, sure ? Tt("Click again to delete every session of this match", "Нажмите ещё раз: удалить все сессии катки") : Tt("Delete the match (all its sessions)", "Удалить катку (все её сессии)")))
            {
                if (sure)
                {
                    List<BtcReplay.Data> files = new List<BtcReplay.Data>();
                    for (int i = 0; i < match.Nodes.Count; i++) files.Add(match.Nodes[i].D);
                    BtcUndo.Group("replay", Tt("Match deleted: ", "Катка удалена: ") + Title(d), delegate
                    {
                        for (int i = 0; i < files.Count; i++) { SoftDelete(files[i]); _meta.Remove(files[i].FileName); }
                        BtcSfx.Delete(); Dirty(true); _selected = null; _confirmDeleteAll = null; BtcReplayUI.Refresh();
                    });
                }
                else { _confirmDeleteAll = d.FileName; _confirmUntil = Time.unscaledTime + 3f; }
            }
            return y + h + fs * 0.5f;
        }
    }
}
