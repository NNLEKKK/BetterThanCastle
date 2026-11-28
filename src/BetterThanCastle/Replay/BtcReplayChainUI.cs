using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay bar parts of a match (BtcReplayChainPlay): the sessions laid out on one line with thin joints, marks / pauses / player marks / activity
    /// graph of the sessions that are not open (from their headers and the per-session APM cache), branch lanes under the line (click = watch that
    /// branch) and the sessions button with its list (jump to a session, switch line, make it the main line). The open session is drawn by the
    /// normal replay UI inside its own part of the bar.
    /// </summary>
    internal static class BtcReplayChainUI
    {
        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }
        internal static readonly Color BranchCol = new Color(0.72f, 0.56f, 1f, 1f);

        internal static Rect StepRect(Rect r, BtcReplayChain.Line l, int i)
        {
            float t = Mathf.Max(0.001f, l.Total);
            return new Rect(r.x + r.width * l.Off(i) / t, r.y, r.width * l.S[i].End / t, r.height);
        }

        /// <summary>Part of the bar of the open session (the whole bar for a single replay).</summary>
        internal static Rect OpenRect(Rect r)
        {
            if (!BtcReplayChainPlay.Active) return r;
            return StepRect(r, BtcReplayChainPlay.L, Mathf.Clamp(BtcReplayChainPlay.Step, 0, BtcReplayChainPlay.L.S.Count - 1));
        }

        private static void Box(Rect r, Color c) { BtcReplayUI.Fill(r, c); }

        private static Color ParseCol(string hex)
        {
            Color c;
            if (hex != null && hex.Length == 6 && ColorUtility.TryParseHtmlString("#" + hex, out c)) return c;
            return new Color(0.7f, 0.7f, 0.7f, 1f);
        }

        internal static string SessionTip(BtcReplayChain.Line l, int i)
        {
            BtcReplayChain.Step s = l.S[i];
            string when = s.N.D.Ticks > 0L ? new DateTime(s.N.D.Ticks).ToString("dd.MM HH:mm") : "";
            string head = Tt("Session ", "Сессия ") + (i + 1) + "/" + l.S.Count + "  " + when + "  " + BtcReplayUI.Clock(s.End);
            if (s.End < s.N.Dur - 1f) head += Tt("  (of ", "  (из ") + BtcReplayUI.Clock(s.N.Dur) + ")";
            if (i > 0) head += "\n" + Tt("continues from the save at ", "продолжение с сейва на ") + BtcReplayUI.Clock(l.Off(i));
            return head;
        }

        // ------------------------------------------------------------------ the line: sessions + joints (after BtcReplayTrack.Draw)
        internal static void DrawSessions(Rect r2, int fs)
        {
            BtcReplayChain.Line l = BtcReplayChainPlay.L;
            Rect line = BtcReplayTrack.Line(r2);
            Event e = Event.current;
            for (int i = 0; i < l.S.Count; i++)
            {
                Rect sr = StepRect(r2, l, i);
                Rect sl = new Rect(sr.x, line.y - 3f, sr.width, line.height + 6f);
                if (i != BtcReplayChainPlay.Step) Box(new Rect(sl.x, line.y + line.height + 1f, sl.width, 2f), new Color(1f, 1f, 1f, 0.08f));
                else Box(new Rect(sl.x, line.y + line.height + 1f, sl.width, 2f), BtcMenu.AccentA(0.45f));
                if (i == 0 || !BtcReplayLayout.ChainJoints) continue;
                // joint: a gap in the line and a short notch above and below
                float x = sr.x;
                Box(new Rect(x - 1.5f, line.y - 1f, 3f, line.height + 2f), new Color(0.02f, 0.02f, 0.03f, 1f));
                Box(new Rect(x - 0.5f, line.y - 7f, 1f, 5f), new Color(1f, 1f, 1f, 0.75f));
                Box(new Rect(x - 0.5f, line.yMax + 4f, 1f, 5f), new Color(1f, 1f, 1f, 0.75f));
                Rect hit = new Rect(x - 6f, line.y - 9f, 12f, line.height + 18f);
                if (hit.Contains(e.mousePosition)) BtcReplayIcons.Hint(hit, SessionTip(l, i));
            }
        }

        // ------------------------------------------------------------------ marks of the sessions that are not open
        private struct Ev { internal char K; internal float T, A; internal int N; internal string C1, C2; }
        private static readonly Dictionary<string, List<Ev>> EvCache = new Dictionary<string, List<Ev>>();

        private static List<Ev> EventsOf(BtcReplay.Data d)
        {
            List<Ev> r;
            if (EvCache.TryGetValue(d.FileName, out r)) return r;
            r = new List<Ev>();
            EvCache[d.FileName] = r;
            if (string.IsNullOrEmpty(d.Tl)) return r;
            CultureInfo c = CultureInfo.InvariantCulture;
            foreach (string item in d.Tl.Trim().Split(';'))
            {
                string[] p = item.Split(',');
                if (p.Length < 2 || p[0].Length != 1) continue;
                float t;
                if (!float.TryParse(p[1], NumberStyles.Float, c, out t)) continue;
                Ev e = new Ev { K = p[0][0], T = t, A = t, N = 1 };
                switch (e.K)
                {
                    case 'D': if (p.Length < 4) continue; e.C1 = p[2]; int.TryParse(p[3], out e.N); break;
                    case 'C': case 'N': case 'H': if (p.Length < 4) continue; e.C1 = p[2]; e.C2 = p[3] == "-" ? null : p[3]; break;
                    case 'X': e.C1 = p[2]; break;
                    case 'F': if (p.Length < 6) continue; float.TryParse(p[2], NumberStyles.Float, c, out e.A); int.TryParse(p[3], out e.N); e.C1 = p[4] == "-" ? null : p[4]; e.C2 = p[5] == "-" ? null : p[5]; break;
                    case 'Z': if (p.Length < 3) continue; float.TryParse(p[2], NumberStyles.Float, c, out e.A); e.C1 = "FFC857"; break;
                    case 'M': if (p.Length < 3) continue; e.C1 = p[2]; break;
                    default: continue;
                }
                r.Add(e);
            }
            return r;
        }

        private static string KindName(Ev e)
        {
            switch (e.K)
            {
                case 'D': return Tt("Bricktron deaths x", "Смерти бриктронов x") + e.N;
                case 'C': return e.C2 != null ? Tt("Crystal stolen", "Кристалл отбит") : Tt("Crystal captured", "Кристалл захвачен");
                case 'N': return Tt("Crystal neutralised", "Кристалл стал нейтральным");
                case 'H': return Tt("Home crystal lost (defeat)", "Главный кристалл потерян (поражение)");
                case 'X': return Tt("Barrel explosion", "Взрыв бочки");
                case 'F': return Tt("Large fight", "Масштабный файт") + " (" + e.N + ")";
                case 'Z': return Tt("Pause", "Пауза");
                case 'M': return Tt("Player mark", "Метка игрока");
            }
            return "";
        }

        /// <summary>Marks of the other sessions on their parts of the bar (one row, same icons as the open one). Returns the height they need above the panel.</summary>
        internal static float DrawOtherMarks(Rect r2, float h, Rect panel)
        {
            if (!BtcReplayLayout.ChainMarks) return 0f;
            BtcReplayChain.Line l = BtcReplayChainPlay.L;
            Rect line = BtcReplayTrack.Line(r2);
            Event e = Event.current;
            float iw = h * 0.78f, rowH = iw + 4f, need = 0f;
            float iy = panel.y - 6f - 3f - rowH + 2f;
            for (int i = 0; i < l.S.Count; i++)
            {
                if (i == BtcReplayChainPlay.Step) continue;
                BtcReplayChain.Step s = l.S[i];
                List<Ev> evs = EventsOf(s.N.D);
                if (evs.Count == 0) continue;
                Rect sr = StepRect(r2, l, i);
                float end = Mathf.Max(0.001f, s.End), off = l.Off(i);
                float lastX = -999f;
                string lastKey = null;
                for (int k = 0; k < evs.Count; k++)
                {
                    Ev v = evs[k];
                    if (v.T > end) continue;
                    if (BtcAutoMarks.HiddenKinds.Contains(v.K)) continue;
                    if (v.K == 'M' && BtcTimer.HiddenCols.Contains(v.C1)) continue;
                    float x = sr.x + sr.width * Mathf.Clamp01(v.T / end);
                    if (v.K == 'F' || v.K == 'Z')
                    {
                        float x1 = sr.x + sr.width * Mathf.Clamp01(Mathf.Min(v.A, end) / end);
                        Box(new Rect(x, line.y - 1f, Mathf.Max(3f, x1 - x), line.height + 2f), v.K == 'F' ? new Color(1f, 0.3f, 0.2f, 0.8f) : new Color(1f, 0.65f, 0.15f, 0.8f));
                        if (v.K == 'Z') continue;
                    }
                    if (v.K == 'M')
                    {
                        Color mc = ParseCol(v.C1);
                        Box(new Rect(x - 1f, line.y - h * 0.1f, 2f, line.height + h * 0.2f), mc);
                        Box(new Rect(x - h * 0.16f, line.y - h * 0.34f, h * 0.32f, h * 0.24f), mc);
                        continue;
                    }
                    string key = v.K + v.C1;
                    if (key == lastKey && x - lastX < iw * 0.95f) continue;   // same kind close by: one icon is enough here
                    lastKey = key; lastX = x;
                    need = Mathf.Max(need, rowH + 8f);
                    Rect ir = new Rect(x - iw * 0.5f, iy, iw, iw);
                    Color sc = ParseCol(v.C1);
                    Box(new Rect(x - 0.5f, ir.yMax, 1f, Mathf.Max(0f, line.y - ir.yMax)), new Color(sc.r, sc.g, sc.b, 0.3f));
                    Box(new Rect(ir.x - 2f, ir.y - 2f, ir.width + 4f, ir.height + 4f), new Color(0.02f, 0.03f, 0.04f, 0.6f));
                    BtcMarkIcons.Draw(v.K, ir, ParseCol(v.C1), ParseCol((v.K == 'H' || v.K == 'N' ? null : v.C2) ?? v.C1));
                    if (ir.Contains(e.mousePosition))
                    {
                        BtcReplayIcons.Hint(ir, KindName(v) + "  " + BtcReplayUI.Clock(off + v.T) + "\n" + Tt("Session ", "Сессия ") + (i + 1) + Tt(" - click to jump there", " - клик, чтобы перейти"));
                        if (e.type == EventType.MouseDown && e.button == 0) { e.Use(); BtcSfx.Jump(); BtcReplayChainPlay.Seek(off + Mathf.Max(0f, v.T - 5f)); }
                    }
                }
            }
            return need;
        }

        // ------------------------------------------------------------------ activity graph of the other sessions
        internal static void DrawOtherApm(Rect wave, Rect r2)
        {
            if (Event.current.type != EventType.Repaint || wave.height < 2f) return;
            BtcReplayChain.Line l = BtcReplayChainPlay.L;
            float scale = Mathf.Max(20f, BtcApm.Scale);
            bool epm = BtcConfig.ReplayApmMode.Value == 2;
            for (int i = 0; i < l.S.Count; i++)
            {
                if (i == BtcReplayChainPlay.Step) continue;
                List<BtcChainApm.Series> ser = BtcChainApm.Get(l.S[i].N.D);
                if (ser == null) continue;
                Rect sr = StepRect(r2, l, i);
                float end = Mathf.Max(0.001f, l.S[i].End);
                int cols = Mathf.Clamp((int)(sr.width / 3f), 1, 400);
                float cw = sr.width / cols;
                for (int s = 0; s < ser.Count; s++)
                {
                    float[] v = epm && ser[s].E != null ? ser[s].E : ser[s].V;
                    if (v == null || v.Length == 0) continue;
                    Color c = ser[s].C;
                    for (int k = 0; k < cols; k++)
                    {
                        float t = end * (k + 0.5f) / cols;
                        float val = v[Mathf.Clamp((int)(t / ser[s].Bs), 0, v.Length - 1)];
                        float hh = Mathf.Clamp01(val / scale) * (wave.height - 3f);
                        if (hh < 1f) continue;
                        Box(new Rect(sr.x + k * cw, wave.yMax - hh, Mathf.Max(1f, cw - 0.5f), Mathf.Min(2f, hh)), new Color(c.r, c.g, c.b, 0.85f));
                        Box(new Rect(sr.x + k * cw, wave.yMax - hh + 2f, Mathf.Max(1f, cw - 0.5f), Mathf.Max(0f, hh - 2f)), new Color(c.r, c.g, c.b, 0.12f));
                    }
                }
            }
        }

        // ------------------------------------------------------------------ branch lanes under the line
        private static List<BtcReplayChain.Fork> _forks;
        private static string _forksKey;

        internal static List<BtcReplayChain.Fork> Forks()
        {
            string k = BtcReplayChainPlay.L.Key + "|" + BtcReplayChain.Version;
            if (_forks == null || k != _forksKey) { _forksKey = k; _forks = BtcReplayChainPlay.M.Forks(BtcReplayChainPlay.L); }
            return _forks;
        }

        internal static string ForkTip(BtcReplayChain.Fork f)
        {
            bool main = BtcReplayChainPlay.M.Main == f.L;
            string what = main ? Tt("Main line of the match", "Главная линия катки")
                : (f.Tail ? Tt("Branch: played on after the save, not continued", "Ветка: доиграно после сейва, но не продолжено") : Tt("Branch: another game from the same save", "Ветка: другая игра с того же сейва"));
            return what + "\n" + Tt("from ", "с ") + BtcReplayUI.Clock(f.At) + ", " + BtcReplayUI.Clock(f.Len) + Tt("  - click to watch", "  - клик, чтобы смотреть");
        }

        /// <summary>The pointer is on a branch lane (the bar must not start a seek drag there). controlsY = top of the controls row.</summary>
        internal static bool OverLane(Rect r2, float controlsY, Vector2 m)
        {
            if (!BtcReplayLayout.ChainBranches) return false;
            List<BtcReplayChain.Fork> fs = Forks();
            if (fs.Count == 0) return false;
            Rect line = BtcReplayTrack.Line(r2);
            float total = Mathf.Max(0.001f, BtcReplayChainPlay.Total);
            int lanes = Mathf.Clamp((int)((controlsY - line.yMax - 4f) / 6f), 1, 3);
            for (int i = 0; i < fs.Count && i < lanes; i++)
            {
                float x0 = r2.x + r2.width * Mathf.Clamp01(fs[i].At / total);
                float x1 = Mathf.Min(r2.xMax, x0 + r2.width * fs[i].Len / total);
                float ly = line.yMax + 5f + i * 6f;
                if (new Rect(x0 - 3f, ly - 3f, Mathf.Max(8f, x1 - x0) + 6f, 8f).Contains(m)) return true;
            }
            return false;
        }

        internal static void DrawBranches(Rect r2, float room)
        {
            if (!BtcReplayLayout.ChainBranches) return;
            List<BtcReplayChain.Fork> fs = Forks();
            if (fs.Count == 0) return;
            Rect line = BtcReplayTrack.Line(r2);
            Event e = Event.current;
            float total = Mathf.Max(0.001f, BtcReplayChainPlay.Total);
            int lanes = Mathf.Clamp((int)((room - 2f) / 6f), 1, 3);
            for (int i = 0; i < fs.Count && i < lanes; i++)
            {
                BtcReplayChain.Fork f = fs[i];
                bool main = BtcReplayChainPlay.M.Main == f.L;
                Color c = main ? BtcMenu.Accent : BranchCol;
                float x0 = r2.x + r2.width * Mathf.Clamp01(f.At / total);
                float x1 = Mathf.Min(r2.xMax, x0 + r2.width * f.Len / total);
                float ly = line.yMax + 5f + i * 6f;
                Rect hit = new Rect(x0 - 3f, ly - 3f, Mathf.Max(8f, x1 - x0) + 6f, 8f);
                bool hot = hit.Contains(e.mousePosition);
                Box(new Rect(x0 - 0.5f, line.yMax, 1.5f, ly - line.yMax + 2f), new Color(c.r, c.g, c.b, hot ? 1f : 0.7f));
                Box(new Rect(x0, ly, Mathf.Max(3f, x1 - x0), hot ? 3f : 2f), new Color(c.r, c.g, c.b, hot ? 1f : 0.7f));
                if (x0 + r2.width * f.Len / total > r2.xMax + 1f) Box(new Rect(x1 - 4f, ly - 2f, 4f, 6f), new Color(c.r, c.g, c.b, 0.9f));   // goes on past the end of this line
                if (hot)
                {
                    BtcReplayIcons.Hint(hit, ForkTip(f));
                    if (e.type == EventType.MouseDown && e.button == 0) { e.Use(); BtcReplayChainPlay.ShowLine(f.L, f.At); }
                }
            }
            if (fs.Count > lanes)
            {
                Rect mr = new Rect(r2.xMax - 30f, line.yMax + 4f, 30f, 10f);
                Box(mr, new Color(0f, 0f, 0f, 0.6f));
                BtcReplayIcons.Hint(mr, Tt("More branches in the sessions list", "Ещё ветки - в списке сессий"));
            }
        }

        // ------------------------------------------------------------------ sessions button + list
        internal static bool ListOpen;
        private static GUIStyle _st, _dim;

        private static void Styles(int fs)
        {
            if (_st != null && _st.fontSize == Mathf.RoundToInt(fs * 0.9f)) return;
            _st = new GUIStyle(BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel); _st.fontSize = Mathf.RoundToInt(fs * 0.9f); _st.alignment = TextAnchor.MiddleLeft; _st.clipping = TextClipping.Clip;
            _dim = new GUIStyle(_st); _dim.fontSize = Mathf.RoundToInt(fs * 0.75f); _dim.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
        }

        /// <summary>The button in the controls row: sessions glyph + "2/3"; violet while a branch is shown.</summary>
        internal static Rect DrawButton(float x, float y, float h, float ib, int fs)
        {
            Styles(fs);
            BtcReplayChain.Line l = BtcReplayChainPlay.L;
            string t = (BtcReplayChainPlay.Step + 1) + "/" + l.S.Count;
            Rect r = new Rect(x, y, ib + fs * (0.6f + t.Length * 0.55f), h);
            bool hot = r.Contains(Event.current.mousePosition) || ListOpen;
            Color c = BtcReplayChainPlay.IsMain ? new Color(1f, 1f, 1f, 0.9f) : BranchCol;
            Box(r, new Color(1f, 1f, 1f, hot ? 0.12f : 0.05f));
            BtcReplayIcons.Glyph(BtcReplayChainPlay.IsMain ? BtcReplayIcons.Sessions : BtcReplayIcons.Branch, new Rect(r.x + 2f, y + (h - ib) * 0.5f, ib, ib), c);
            Color old = GUI.contentColor; GUI.contentColor = c;
            GUI.Label(new Rect(r.x + ib + 4f, y, r.width - ib - 4f, h), t, _st);
            GUI.contentColor = old;
            BtcReplayIcons.Hint(r, (BtcReplayChainPlay.IsMain ? Tt("Match: session ", "Катка: сессия ") : Tt("Branch: session ", "Ветка: сессия ")) + t + Tt(" - click for the list of sessions and branches", " - клик: список сессий и веток"));
            if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) ListOpen = !ListOpen;
            return r;
        }

        /// <summary>List above the button: the sessions of the shown line, the other lines, back to main / make main.</summary>
        internal static void DrawList(Rect anchor, float bottomY, int fs)
        {
            if (!ListOpen || !BtcReplayChainPlay.Active) return;
            Styles(fs);
            Event e = Event.current;
            BtcReplayChain.Match m = BtcReplayChainPlay.M;
            BtcReplayChain.Line l = BtcReplayChainPlay.L;
            List<BtcReplayChain.Fork> forks = Forks();
            float rowH = fs * 1.7f, w = fs * 19f;
            int rows = 1 + l.S.Count + (forks.Count > 0 ? 1 + forks.Count : 0) + (BtcReplayChainPlay.IsMain ? 0 : 1);
            float h = rows * rowH + fs * 0.6f;
            Rect p = new Rect(Mathf.Clamp(anchor.x, 4f, Screen.width - w - 4f), bottomY - h - 4f, w, h);
            Box(p, new Color(0.03f, 0.04f, 0.05f, 0.95f));
            Box(new Rect(p.x, p.y, p.width, 2f), BtcReplayChainPlay.IsMain ? BtcMenu.AccentA(0.9f) : BranchCol);
            float y = p.y + fs * 0.3f;
            GUI.Label(new Rect(p.x + fs * 0.6f, y, w - fs, rowH), (BtcReplayChainPlay.IsMain ? Tt("Match", "Катка") : Tt("Branch", "Ветка")) + "  -  " + l.S.Count + Tt(" sessions, ", " сесс., ") + BtcReplayUI.Clock(l.Total), _st);
            y += rowH;
            for (int i = 0; i < l.S.Count; i++)
            {
                Rect rr = new Rect(p.x + 2f, y, w - 4f, rowH);
                bool cur = i == BtcReplayChainPlay.Step, hot = rr.Contains(e.mousePosition);
                if (cur) Box(new Rect(rr.x, rr.y, 3f, rr.height), BtcMenu.Accent);
                if (hot) Box(rr, new Color(1f, 1f, 1f, 0.08f));
                BtcReplay.Data d = l.S[i].N.D;
                GUI.Label(new Rect(rr.x + fs * 0.6f, y, fs * 2f, rowH), (i + 1).ToString(), _st);
                GUI.Label(new Rect(rr.x + fs * 2.2f, y, fs * 8f, rowH), d.Ticks > 0L ? new DateTime(d.Ticks).ToString("dd.MM HH:mm") : "", _dim);
                GUI.Label(new Rect(rr.x + fs * 8.6f, y, fs * 5f, rowH), BtcReplayUI.Clock(l.Off(i)), _dim);
                GUI.Label(new Rect(rr.xMax - fs * 4.6f, y, fs * 4.4f, rowH), BtcReplayUI.Clock(l.S[i].End), _st);
                BtcReplayIcons.Hint(rr, SessionTip(l, i));
                if (BtcSfx.Btn(rr, "", BtcReplayUI.sBtn)) { BtcReplayChainPlay.Seek(l.Off(i)); ListOpen = false; }
                y += rowH;
            }
            if (forks.Count > 0)
            {
                GUI.Label(new Rect(p.x + fs * 0.6f, y, w - fs, rowH), Tt("Other lines", "Другие линии"), _dim);
                y += rowH;
                for (int i = 0; i < forks.Count; i++)
                {
                    BtcReplayChain.Fork f = forks[i];
                    bool main = m.Main == f.L;
                    Rect rr = new Rect(p.x + 2f, y, w - 4f, rowH);
                    if (rr.Contains(e.mousePosition)) Box(rr, new Color(1f, 1f, 1f, 0.08f));
                    float g = rowH * 0.7f;
                    BtcReplayIcons.Glyph(main ? BtcReplayIcons.Sessions : BtcReplayIcons.Branch, new Rect(rr.x + fs * 0.4f, y + (rowH - g) * 0.5f, g, g), main ? BtcMenu.Accent : BranchCol);
                    GUI.Label(new Rect(rr.x + fs * 2.2f, y, w - fs * 7f, rowH), main ? Tt("Main line", "Главная линия") : (f.Tail ? Tt("Rest after the save", "Остаток после сейва") : Tt("Another game", "Другая игра")), _st);
                    GUI.Label(new Rect(rr.xMax - fs * 8.6f, y, fs * 4f, rowH), BtcReplayUI.Clock(f.At), _dim);
                    GUI.Label(new Rect(rr.xMax - fs * 4.6f, y, fs * 4.4f, rowH), "+" + BtcReplayUI.Clock(f.Len), _st);
                    BtcReplayIcons.Hint(rr, ForkTip(f));
                    if (BtcSfx.Btn(rr, "", BtcReplayUI.sBtn)) { BtcReplayChainPlay.ShowLine(f.L, f.At); ListOpen = false; }
                    y += rowH;
                }
            }
            if (!BtcReplayChainPlay.IsMain)
            {
                Rect rr = new Rect(p.x + 2f, y, w - 4f, rowH);
                if (rr.Contains(e.mousePosition)) Box(rr, new Color(1f, 1f, 1f, 0.08f));
                float g = rowH * 0.7f;
                BtcReplayIcons.Glyph(BtcReplayIcons.Star, new Rect(rr.x + fs * 0.4f, y + (rowH - g) * 0.5f, g, g), new Color(1f, 0.85f, 0.25f, 1f));
                GUI.Label(new Rect(rr.x + fs * 2.2f, y, w - fs * 3f, rowH), Tt("Make this branch the main line", "Сделать эту ветку главной"), _st);
                if (BtcSfx.Btn(rr, "", BtcReplayUI.sBtn)) { BtcReplayChain.MakeMain(m, l); ListOpen = false; }
            }
            // a click anywhere else closes the list
            if (e.type == EventType.MouseDown && !p.Contains(e.mousePosition) && !anchor.Contains(e.mousePosition)) ListOpen = false;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseUp) && p.Contains(e.mousePosition)) e.Use();
        }
    }

    /// <summary>Per-session activity graph cache (cash/*.apm): written while a session of a match is open, read to draw it while another one is.</summary>
    internal static class BtcChainApm
    {
        internal sealed class Series { internal Color C; internal float Bs; internal float[] V, E; }

        private static readonly Dictionary<string, List<Series>> Mem = new Dictionary<string, List<Series>>();

        private static string PathOf(BtcReplay.Data d)
        {
            string name = Path.GetFileNameWithoutExtension(d.FileName);
            foreach (char ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
            if (name.Length > 60) name = name.Substring(0, 60);
            return Path.Combine(Path.Combine(BtcPaths.Root, "cash"), name + "_" + d.FileBytes.ToString("x") + ".apm");
        }

        internal static void Capture(BtcReplay.Data d)
        {
            try
            {
                if (d == null) return;
                string p = PathOf(d);
                if (File.Exists(p)) return;
                string s = BtcApm.Summary();
                if (s.Length == 0) return;
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.WriteAllText(p, s, new UTF8Encoding(false));
                Mem.Remove(d.FileName);
            }
            catch (Exception e) { BtcLog.Warn("ReplayChain", "apm cache: " + e.Message); }
        }

        internal static List<Series> Get(BtcReplay.Data d)
        {
            List<Series> r;
            if (Mem.TryGetValue(d.FileName, out r)) return r;
            r = null;
            try
            {
                string p = PathOf(d);
                if (File.Exists(p)) r = Parse(File.ReadAllText(p, Encoding.UTF8));
            }
            catch (Exception) { r = null; }
            Mem[d.FileName] = r;
            return r;
        }

        private static float[] Nums(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            string[] p = s.Split(',');
            float[] v = new float[p.Length];
            for (int i = 0; i < p.Length; i++) float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]);
            return v;
        }

        private static List<Series> Parse(string s)
        {
            List<Series> r = new List<Series>();
            foreach (string part in s.Split('|'))
            {
                string[] p = part.Split(':');
                if (p.Length < 3) continue;
                Series x = new Series();
                Color c;
                x.C = ColorUtility.TryParseHtmlString("#" + p[0], out c) ? c : Color.white;
                float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x.Bs);
                if (x.Bs <= 0f) x.Bs = 2f;
                x.V = Nums(p[2]);
                x.E = p.Length > 3 ? Nums(p[3]) : null;
                r.Add(x);
            }
            return r.Count > 0 ? r : null;
        }

        /// <summary>Average APM per player colour over a whole line (library details): sessions without a cache are skipped.</summary>
        internal static Dictionary<string, float> AvgApm(BtcReplayChain.Line l, out int covered)
        {
            Dictionary<string, float> sum = new Dictionary<string, float>();
            Dictionary<string, float> time = new Dictionary<string, float>();
            covered = 0;
            for (int i = 0; i < l.S.Count; i++)
            {
                List<Series> ser = Get(l.S[i].N.D);
                if (ser == null) continue;
                covered++;
                float end = l.S[i].End;
                for (int k = 0; k < ser.Count; k++)
                {
                    float[] v = ser[k].V;
                    if (v == null) continue;
                    string key = ColorUtility.ToHtmlStringRGB(ser[k].C);
                    float a = 0f; int n = 0;
                    for (int b = 0; b < v.Length && b * ser[k].Bs < end; b++) { a += v[b]; n++; }
                    float s0, t0;
                    sum.TryGetValue(key, out s0); time.TryGetValue(key, out t0);
                    sum[key] = s0 + a * ser[k].Bs; time[key] = t0 + n * ser[k].Bs;
                }
            }
            Dictionary<string, float> r = new Dictionary<string, float>();
            foreach (KeyValuePair<string, float> kv in sum) r[kv.Key] = time[kv.Key] > 0f ? kv.Value / time[kv.Key] : 0f;
            return r;
        }
    }
}
