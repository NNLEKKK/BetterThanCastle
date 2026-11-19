using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Aim trainer screen: run bar (time, hits, accuracy), countdown, loading card and the result card (rating + rank, stat tiles,
    /// reaction-time bars, history line, live settings, Again / Exit). Flat dark panels in the menu/replay style (BtcHud.Panel, accent).
    /// </summary>
    internal static class BtcAimHud
    {
        /// <summary>Mouse was over a HUD panel in the last painted frame: right clicks there are not shots.</summary>
        internal static bool MouseOverHud;

        private static float _flashAt = -10f;
        private static bool _flashHit;
        private static GUIStyle _big, _num, _lab, _dim, _ctr, _btn;
        private static int _fs;
        private static Font _font;
        private static bool _fontTried;
        private static readonly List<Rect> _hud = new List<Rect>();

        private static string T(string en, string ru) { return BtcGameplay.T(en, ru); }

        internal static void Flash(bool hit) { _flashAt = Time.unscaledTime; _flashHit = hit; }

        private static GUIStyle _rankSt;

        /// <summary>Rank letters packed together: each next letter overlaps the previous one (SS, SSS, XX...), back letters a bit darker,
        /// a thin shadow keeps them apart. Left-aligned in r; returns the drawn width.</summary>
        internal static float DrawRank(Rect r, string name, GUIStyle st, Color c)
        {
            if (_rankSt == null || _rankSt.fontSize != st.fontSize || _rankSt.font != st.font)
            {
                _rankSt = new GUIStyle(st); _rankSt.alignment = TextAnchor.MiddleLeft; _rankSt.clipping = TextClipping.Overflow; _rankSt.wordWrap = false;
            }
            int n = name.Length;
            float w = _rankSt.CalcSize(new GUIContent(name.Substring(0, 1))).x;
            float step = n > 1 ? w * 0.42f : 0f;
            Color was = _rankSt.normal.textColor;
            for (int i = n - 1; i >= 0; i--)        // back to front: the first letter ends on top
            {
                float k = n > 1 ? 1f - 0.22f * i / (n - 1) : 1f;
                Rect lr = new Rect(r.x + i * step, r.y, w * 1.4f, r.height);
                _rankSt.normal.textColor = new Color(0f, 0f, 0f, 0.55f * c.a);
                GUI.Label(new Rect(lr.x + 1.5f, lr.y + 1.5f, lr.width, lr.height), name.Substring(i, 1), _rankSt);
                _rankSt.normal.textColor = new Color(c.r * k, c.g * k, c.b * k, c.a);
                GUI.Label(lr, name.Substring(i, 1), _rankSt);
            }
            _rankSt.normal.textColor = was;
            return w + step * (n - 1);
        }

        private static void Styles(int fs)
        {
            if (_lab != null && _fs == fs) return;
            _fs = fs;
            if (!_fontTried)
            {
                _fontTried = true;
                try { _font = Font.CreateDynamicFontFromOSFont(new string[] { "Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Semibold", "Arial" }, 16); } catch (Exception) { }
            }
            _lab = new GUIStyle(GUI.skin.label); _lab.fontSize = fs; _lab.alignment = TextAnchor.MiddleLeft; _lab.clipping = TextClipping.Clip; _lab.wordWrap = false; _lab.normal.textColor = Color.white;
            if (_font != null) _lab.font = _font;
            _dim = new GUIStyle(_lab); _dim.fontSize = Mathf.RoundToInt(fs * 0.8f); _dim.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            _ctr = new GUIStyle(_lab); _ctr.alignment = TextAnchor.MiddleCenter;
            _num = new GUIStyle(_ctr); _num.fontSize = Mathf.RoundToInt(fs * 1.5f); _num.fontStyle = FontStyle.Bold;
            _big = new GUIStyle(_ctr); _big.fontSize = Mathf.RoundToInt(fs * 4f); _big.fontStyle = FontStyle.Bold;
            _btn = new GUIStyle();
        }

        private static Color Acc(float a) { Color c = BtcMenu.Accent; return new Color(c.r, c.g, c.b, a); }

        private static void Panel(Rect r) { BtcReplayUI.Fill(r, BtcHud.Panel); _hud.Add(r); }

        internal static void Draw(BtcAim a)
        {
            int fs = BtcHud.Fs;
            Styles(fs);
            Event e = Event.current;
            if (e.type == EventType.Layout) return;
            _hud.Clear();
            float sw = Screen.width, sh = Screen.height, cx = sw * 0.5f;
            switch (a.State)
            {
                case BtcAimState.Loading:
                    {
                        Rect r = new Rect(cx - fs * 12f, sh * 0.42f, fs * 24f, fs * 3.4f);
                        Panel(r);
                        BtcReplayUI.Fill(new Rect(r.x, r.y + r.height * 0.15f, 3f, r.height * 0.7f), BtcMenu.Accent);
                        BtcReplayIcons.Glyph(BtcReplayIcons.Target, new Rect(r.x + fs, r.center.y - fs * 0.8f, fs * 1.6f, fs * 1.6f), BtcMenu.Accent);
                        GUI.Label(new Rect(r.x + fs * 3.4f, r.y, r.width - fs * 4f, r.height * 0.6f), T("Aim training", "Тренировка аима"), _lab);
                        GUI.Label(new Rect(r.x + fs * 3.4f, r.y + r.height * 0.45f, r.width - fs * 4f, r.height * 0.45f), T("Preparing the floor...", "Подготовка площадки..."), _dim);
                        break;
                    }
                case BtcAimState.Countdown:
                    GUI.Label(new Rect(cx - fs * 6f, sh * 0.3f, fs * 12f, fs * 6f), Mathf.CeilToInt(Mathf.Max(0.01f, a.CountdownLeft)).ToString(), _big);
                    GUI.Label(new Rect(cx - fs * 16f, sh * 0.3f + fs * 6f, fs * 32f, fs * 1.6f), BtcAimConfig.Game == BtcAimGame.Triage ? T("Right click the MOST wounded halberdier", "ПКМ по САМОМУ раненому алебардщику") : T("Right click the wounded halberdier", "ПКМ по раненому алебардщику"), _ctr);
                    RunBar(a, fs, cx);
                    break;
                case BtcAimState.Run: RunBar(a, fs, cx); Marker(a, fs); break;
                case BtcAimState.Over: Result(a, fs, e); break;
            }
            Corner(a, fs, sw);
            if (e.type == EventType.Repaint)
            {
                Vector2 m = e.mousePosition; bool over = false;
                for (int i = 0; i < _hud.Count; i++) if (_hud[i].Contains(m)) { over = true; break; }
                MouseOverHud = over;
            }
        }

        // short hit/miss pulse around the cursor
        private static void Marker(BtcAim a, int fs)
        {
            float age = Time.unscaledTime - _flashAt;
            if (age > 0.25f) return;
            Vector2 m = Event.current.mousePosition;
            float s = fs * (1f + age * 6f), al = 1f - age / 0.25f;
            Color c = _flashHit ? Acc(al) : new Color(0.92f, 0.34f, 0.32f, al);
            BtcReplayUI.Fill(new Rect(m.x - s, m.y - 1f, s * 0.6f, 2f), c); BtcReplayUI.Fill(new Rect(m.x + s * 0.4f, m.y - 1f, s * 0.6f, 2f), c);
            BtcReplayUI.Fill(new Rect(m.x - 1f, m.y - s, 2f, s * 0.6f), c); BtcReplayUI.Fill(new Rect(m.x - 1f, m.y + s * 0.4f, 2f, s * 0.6f), c);
        }

        private static void RunBar(BtcAim a, int fs, float cx)
        {
            float w = fs * 22f, h = fs * 3f;
            Rect r = new Rect(cx - w * 0.5f, fs * 0.6f, w, h);
            Panel(r);
            float dur = Mathf.Max(1f, a.State == BtcAimState.Run ? a.TimeLeft + a.Elapsed : BtcAimConfig.Duration);
            float left = a.State == BtcAimState.Run ? a.TimeLeft : dur;
            BtcReplayUI.Fill(new Rect(r.x, r.yMax - 3f, r.width * Mathf.Clamp01(left / dur), 3f), left < 5f ? new Color(0.95f, 0.78f, 0.25f, 1f) : BtcMenu.Accent);
            float cw = w / 3f;
            int clicks = a.Hits + a.Misses;
            Cell(new Rect(r.x, r.y, cw, h - 3f), left.ToString("0.0"), T("time", "время"));
            Cell(new Rect(r.x + cw, r.y, cw, h - 3f), a.Hits.ToString(), T("hits", "попадания"));
            Cell(new Rect(r.x + cw * 2f, r.y, cw, h - 3f), clicks > 0 ? Mathf.RoundToInt(100f * a.Hits / clicks) + "%" : "-", T("accuracy", "точность"));
        }

        private static void Cell(Rect r, string val, string cap)
        {
            GUI.Label(new Rect(r.x, r.y + r.height * 0.05f, r.width, r.height * 0.6f), val, _num);
            GUI.Label(new Rect(r.x, r.y + r.height * 0.6f, r.width, r.height * 0.4f), cap, CtrDim());
        }

        private static GUIStyle _ctrDim;
        private static GUIStyle CtrDim() { if (_ctrDim == null || _ctrDim.fontSize != _dim.fontSize) { _ctrDim = new GUIStyle(_dim); _ctrDim.alignment = TextAnchor.MiddleCenter; } return _ctrDim; }

        // Again / Exit, always reachable (top right)
        private static void Corner(BtcAim a, int fs, float sw)
        {
            if (a.State == BtcAimState.Loading) return;
            float bw = fs * 7.5f, bh = fs * 2f, y = fs * 0.6f;
            Rect ex = new Rect(sw - fs * 0.6f - bw, y, bw, bh), ag = new Rect(ex.x - bw - fs * 0.4f, y, bw, bh);
            if (Button(ag, BtcReplayIcons.Refresh, T("Again (R)", "Ещё раз (R)"), false)) a.Again();
            if (Button(ex, BtcReplayIcons.Cross, T("Exit", "Выход"), false)) a.ExitToMenu();
        }

        private static bool Button(Rect r, int icon, string text, bool primary)
        {
            _hud.Add(r);
            bool hov = r.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(r, primary ? Acc(hov ? 0.95f : 0.8f) : (hov ? new Color(0.12f, 0.14f, 0.16f, 0.95f) : BtcHud.Panel));
            float g = r.height * 0.5f;
            Color tc = primary ? new Color(0.03f, 0.05f, 0.04f, 1f) : Color.white;
            BtcReplayIcons.Glyph(icon, new Rect(r.x + r.height * 0.35f, r.center.y - g * 0.5f, g, g), primary ? tc : BtcMenu.Accent);
            Color was = _lab.normal.textColor; _lab.normal.textColor = tc;
            GUI.Label(new Rect(r.x + r.height * 1.1f, r.y, r.width - r.height * 1.2f, r.height), text, _lab);
            _lab.normal.textColor = was;
            return BtcSfx.Btn(r, "", _btn);
        }

        // ================= result card =================
        private static void Result(BtcAim a, int fs, Event e)
        {
            BtcAimScore.Result r = a.Last;
            if (r == null) return;
            float sw = Screen.width, sh = Screen.height;
            float w = Mathf.Min(sw - fs * 2f, fs * 58f), h = Mathf.Min(sh - fs * 2f, fs * 33f);
            Rect card = new Rect((sw - w) * 0.5f, (sh - h) * 0.5f, w, h);
            Panel(card);
            BtcReplayUI.Fill(new Rect(card.x, card.y, card.width, 2f), BtcMenu.Accent);
            float pad = fs * 1.2f;
            float leftW = (w - pad * 3f) * 0.62f, rightX = card.x + pad * 2f + leftW, rightW = w - pad * 3f - leftW;
            float x = card.x + pad, y = card.y + pad;

            // header
            BtcReplayIcons.Glyph(BtcReplayIcons.Target, new Rect(x, y + fs * 0.1f, fs * 1.3f, fs * 1.3f), BtcMenu.Accent);
            GUI.Label(new Rect(x + fs * 1.8f, y, leftW, fs * 1.5f), T("AIM TRAINING  ·  ", "ТРЕНИРОВКА АИМА  ·  ") + BtcAimScore.CatName(BtcAimScore.CatOf(r.Cfg)).ToUpperInvariant(), _lab);
            y += fs * 2.2f;

            // rating + rank
            int rank = BtcAimScore.RankOf(r.Rating);
            Color rc = BtcAimScore.RankColor(rank);
            Rect rb = new Rect(x, y, leftW, fs * 5.2f);
            BtcReplayUI.Fill(rb, new Color(1f, 1f, 1f, 0.04f));
            BtcReplayUI.Fill(new Rect(rb.x, rb.y, 3f, rb.height), rc);
            GUIStyle rs = new GUIStyle(_big); rs.fontSize = Mathf.RoundToInt(fs * 3f); rs.alignment = TextAnchor.MiddleLeft; rs.normal.textColor = Color.white;
            GUI.Label(new Rect(rb.x + fs, rb.y, fs * 9f, rb.height * 0.75f), r.Rating.ToString(), rs);
            GUI.Label(new Rect(rb.x + fs, rb.y + rb.height * 0.62f, fs * 9f, rb.height * 0.35f), T("performance", "перформанс"), _dim);
            Color lc = _lab.normal.textColor; _lab.normal.textColor = rc;
            GUIStyle rn = new GUIStyle(_lab); rn.fontSize = Mathf.RoundToInt(fs * 1.4f); rn.fontStyle = FontStyle.Bold;
            _lab.normal.textColor = lc;
            float rx = rb.x + fs * 10f, rw = rb.width - fs * 11f;
            DrawRank(new Rect(rx, rb.y + fs * 0.4f, rw, fs * 1.8f), BtcAimScore.RankName(rank), rn, rc);
            float pr = BtcAimScore.RankProgress(r.Rating);
            BtcReplayUI.Fill(new Rect(rx, rb.y + fs * 2.5f, rw, 4f), new Color(1f, 1f, 1f, 0.12f));
            BtcReplayUI.Fill(new Rect(rx, rb.y + fs * 2.5f, rw * pr, 4f), rc);
            int next = BtcAimScore.NextRankAt(r.Rating);
            string sub = next > 0 ? T("next rank at ", "следующий ранг: ") + next : T("top rank", "высший ранг");
            if (r.NewBest && r.PrevBest >= 0) sub = T("NEW PERSONAL BEST", "НОВЫЙ ЛИЧНЫЙ РЕКОРД") + "  (+" + (r.Rating - r.PrevBest) + ")";
            else if (r.PrevBest >= 0) sub += "   ·   " + T("best ", "рекорд ") + r.PrevBest;
            if (r.NewCatBest && r.PrevBestAll >= 0) sub += "   ·   " + T("best in the mode!", "рекорд режима!");
            else if (r.PrevBestAll > r.PrevBest && r.PrevBestAll >= 0) sub += "   ·   " + T("mode best ", "рекорд режима ") + r.PrevBestAll;
            Color sc = _dim.normal.textColor; if (r.NewBest) _dim.normal.textColor = new Color(1f, 0.82f, 0.3f, 1f);
            GUI.Label(new Rect(rx, rb.y + fs * 3.1f, rw, fs * 1.4f), sub, _dim);
            _dim.normal.textColor = sc;
            y += rb.height + fs * 0.8f;

            // tiles
            string[] v =
            {
                r.Hits.Count.ToString(), r.PerMin.ToString("0"), Mathf.RoundToInt(r.Acc * 100f) + "%", Ms(r.AvgMt),
                Ms(r.MedMt), Ms(r.BestMt), "±" + Ms(r.SdMt), (r.Cfg.Game == 1 ? r.MissPick + " / " : "") + r.MissWrong + " / " + r.MissEmpty,
                r.Tp.ToString("0.00"), r.AvgId.ToString("0.0"), Mathf.RoundToInt(r.AvgOff * 100f) + "%", r.Clicks.ToString()
            };
            string[] c =
            {
                T("hits", "попадания"), T("hits / min", "попаданий / мин"), T("accuracy", "точность"), T("avg reaction", "средняя реакция"),
                T("median", "медиана"), T("best", "лучшая"), T("consistency", "стабильность"), r.Cfg.Game == 1 ? T("miss: wounded / unit / empty", "мимо: раненый / юнит / пусто") : T("miss: unit / empty", "мимо: юнит / пусто"),
                T("throughput, bit/s", "пропускная, бит/с"), T("difficulty, bit", "сложность, бит"), T("off centre", "от центра"), T("clicks", "клики")
            };
            string[] tip =
            {
                null, null, T("Hits / all right clicks", "Попадания / все ПКМ"), T("From the moment the target dropped to your click", "От падения HP цели до клика"),
                null, null, T("Spread of the reaction time (lower = steadier)", "Разброс времени реакции (меньше = стабильнее)"),
                r.Cfg.Game == 1 ? T("Clicked a wounded one that was not the lowest / a healthy one / nothing", "Кликнул по раненому, но не самому / по здоровому / в пустоту") : T("Clicked another halberdier / clicked nothing", "Кликнул по другому алебардщику / в пустоту"),
                T("Fitts throughput: difficulty of every target / time. Hardly depends on the settings: this is your hand speed", "Пропускная способность по Фиттсу: сложность каждой цели / время. Почти не зависит от настроек: это скорость руки"),
                T("Average bits per target: distance / size on screen + search among N + tiny-target and motion bonus", "Средние биты на цель: дистанция / размер на экране + поиск среди N + бонус за мелкие и движущиеся цели"),
                T("Distance from the target centre, in target widths", "Отступ от центра цели, в ширинах цели"), null
            };
            int cols = 4; float gap = fs * 0.4f, tw = (leftW - gap * (cols - 1)) / cols, th = fs * 3.2f;
            for (int i = 0; i < v.Length; i++)
            {
                Rect tr = new Rect(x + (i % cols) * (tw + gap), y + (i / cols) * (th + gap), tw, th);
                BtcReplayUI.Fill(tr, new Color(1f, 1f, 1f, 0.04f));
                GUIStyle vs = new GUIStyle(_num); vs.alignment = TextAnchor.MiddleLeft; vs.fontSize = Mathf.RoundToInt(fs * 1.35f);
                GUI.Label(new Rect(tr.x + fs * 0.6f, tr.y + fs * 0.15f, tr.width - fs, th * 0.55f), v[i], vs);
                GUI.Label(new Rect(tr.x + fs * 0.6f, tr.y + th * 0.55f, tr.width - fs, th * 0.4f), c[i], _dim);
                if (tip[i] != null) BtcReplayIcons.Hint(tr, tip[i]);
            }
            y += (th + gap) * Mathf.Ceil(v.Length / (float)cols) + fs * 0.4f;

            // reaction time per hit
            float gh = Mathf.Max(fs * 3f, card.yMax - pad - y - fs * 1.4f);
            GUI.Label(new Rect(x, y, leftW, fs * 1.2f), T("reaction per hit", "реакция по попаданиям"), _dim);
            y += fs * 1.3f;
            Rect g = new Rect(x, y, leftW, gh);
            BtcReplayUI.Fill(g, new Color(1f, 1f, 1f, 0.03f));
            int n = r.Hits.Count;
            if (n > 0)
            {
                float mx = 0.001f; for (int i = 0; i < n; i++) mx = Mathf.Max(mx, r.Hits[i].Mt);
                float bw = Mathf.Min(fs * 1.2f, g.width / n);
                for (int i = 0; i < n; i++)
                {
                    float bh = g.height * 0.9f * r.Hits[i].Mt / mx;
                    bool good = r.Hits[i].Mt <= r.MedMt;
                    BtcReplayUI.Fill(new Rect(g.x + i * bw + 1f, g.yMax - bh, Mathf.Max(1f, bw - 2f), bh), good ? Acc(0.85f) : new Color(1f, 1f, 1f, 0.35f));
                }
                float my = g.yMax - g.height * 0.9f * r.AvgMt / mx;
                BtcReplayUI.Fill(new Rect(g.x, my, g.width, 1f), new Color(1f, 0.82f, 0.3f, 0.7f));
            }
            else GUI.Label(g, T("no hits", "нет попаданий"), CtrDim());

            // right column: settings + buttons
            Settings(new Rect(rightX, card.y + pad, rightW, card.height - pad * 2f - fs * 2.6f), fs, e);
            float by = card.yMax - pad - fs * 2.2f, bw2 = (rightW - fs * 0.4f) * 0.5f;
            if (Button(new Rect(rightX, by, bw2, fs * 2.2f), BtcReplayIcons.Refresh, T("Again (R)", "Ещё раз (R)"), true)) a.Again();
            if (Button(new Rect(rightX + bw2 + fs * 0.4f, by, bw2, fs * 2.2f), BtcReplayIcons.Cross, T("Exit", "Выход"), false)) a.ExitToMenu();
            BtcReplayIcons.DrawTip(fs, sw, false);
        }

        private static string Ms(float s) { return Mathf.RoundToInt(s * 1000f) + T(" ms", " мс"); }

        /// <summary>Live settings on the result card (same values as the Practice tab; Again uses them).</summary>
        private static void Settings(Rect r, int fs, Event e)
        {
            float x = r.x, y = r.y, w = r.width, rh = fs * 2f;
            GUI.Label(new Rect(x, y, w, fs * 1.5f), T("SETTINGS", "НАСТРОЙКИ"), _lab);
            y += fs * 1.9f;
            int g = Chips(ref y, x, w, rh, new string[] { T("Focus", "Фокус"), T("Most wounded", "Самый раненый") }, (int)BtcAimConfig.Game, e);
            if (g >= 0) { BtcAimConfig.Game = (BtcAimGame)g; BtcAimConfig.Save(); }
            int dir;
            if (BtcAimConfig.Game == BtcAimGame.Triage)
            {
                dir = Step(ref y, x, w, rh, T("Wounded", "Раненых"), BtcAimConfig.WoundPct + "% (" + BtcAimConfig.WoundedCount(BtcAimConfig.Targets, BtcAimConfig.WoundPct) + ")", e);
                if (dir != 0) { BtcAimConfig.WoundPct += dir * 10; BtcAimConfig.Save(); }
                dir = Step(ref y, x, w, rh, T("HP step", "Шаг HP"), BtcAimConfig.StepPct + "%", e);
                if (dir != 0) { BtcAimConfig.StepPct += dir; BtcAimConfig.Save(); }
            }
            dir = Step(ref y, x, w, rh, T("Targets", "Цели"), BtcAimConfig.Targets.ToString(), e);
            if (dir != 0) { BtcAimConfig.Targets += dir * (e.shift ? 4 : 1); BtcAimConfig.Save(); }
            dir = Step(ref y, x, w, rh, T("Spacing", "Расстояние"), BtcAimConfig.Spacing.ToString("0.0"), e);
            if (dir != 0) { BtcAimConfig.Spacing += dir * 0.5f; BtcAimConfig.Save(); }
            dir = Step(ref y, x, w, rh, T("Camera distance", "Отдаление камеры"), Mathf.RoundToInt(BtcAimConfig.Height).ToString(), e);
            if (dir != 0) { BtcAimConfig.Height += dir * BtcAimConfig.HeightStep(BtcAimConfig.Height + (dir < 0 ? -0.01f : 0f), e.shift); BtcAimConfig.Save(); }
            dir = Step(ref y, x, w, rh, T("Camera angle", "Угол камеры"), Mathf.RoundToInt(BtcAimConfig.Pitch) + "°", e);
            if (dir != 0) { BtcAimConfig.Pitch += dir * 5f; BtcAimConfig.Save(); }
            dir = Step(ref y, x, w, rh, T("Time", "Время"), BtcAimConfig.Duration + T(" s", " с"), e);
            if (dir != 0) { BtcAimConfig.Duration += dir * (e.shift ? 30 : 15); BtcAimConfig.Save(); }
            y += fs * 0.3f;
            int m = Chips(ref y, x, w, rh, BtcAimTab.MoveNames(), (int)BtcAimConfig.Move, e);
            if (m >= 0) { BtcAimConfig.Move = (BtcAimMove)m; BtcAimConfig.Save(); }
            int s = Chips(ref y, x, w, rh, BtcAimTab.UnitShort(), (int)BtcAimConfig.Unit, e);
            if (s >= 0) { BtcAimConfig.Unit = (BtcAimUnit)s; BtcAimConfig.Save(); }
            float bits = BtcAimScore.Predict(BtcAim.UnitWidth, BtcAim.Fov);
            y += fs * 0.4f;
            GUI.Label(new Rect(x, y, w, fs * 1.4f), T("Difficulty: ", "Сложность: ") + BtcAimScore.DifficultyName(bits) + "  (" + bits.ToString("0.0") + T(" bit", " бит") + ")", _dim);
            y += fs * 1.4f;
            int pb = BtcAimScore.Best(BtcAimConfig.Key(BtcAimConfig.Take()));
            GUI.Label(new Rect(x, y, w, fs * 1.4f), pb >= 0 ? T("Best on this setup: ", "Рекорд на этих настройках: ") + pb : T("No runs on this setup yet", "На этих настройках ещё не играл"), _dim);
            y += fs * 1.4f;
            int runs, cb = BtcAimScore.BestCat(BtcAimScore.CatOf(BtcAimConfig.Take()), out runs);
            if (cb >= 0) GUI.Label(new Rect(x, y, w, fs * 1.4f), T("Mode best: ", "Рекорд режима: ") + cb + "  ·  " + BtcAimScore.RankName(BtcAimScore.RankOf(cb)), _dim);
        }

        private static int Step(ref float y, float x, float w, float rh, string label, string value, Event e)
        {
            Rect row = new Rect(x, y, w, rh);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.03f));
            GUI.Label(new Rect(x + _fs * 0.5f, y, w * 0.55f, rh), label, _lab);
            float b = rh * 0.8f, by = y + rh * 0.1f;
            Rect p = new Rect(row.xMax - b - 2f, by, b, b), v = new Rect(p.x - _fs * 4f, by, _fs * 4f, b), mi = new Rect(v.x - b, by, b, b);
            BtcReplayUI.Fill(mi, new Color(1f, 1f, 1f, mi.Contains(e.mousePosition) ? 0.18f : 0.09f));
            BtcReplayUI.Fill(p, new Color(1f, 1f, 1f, p.Contains(e.mousePosition) ? 0.18f : 0.09f));
            BtcReplayUI.Fill(new Rect(mi.x + b * 0.28f, mi.center.y - 1f, b * 0.44f, 2f), Color.white);
            BtcReplayUI.Fill(new Rect(p.x + b * 0.28f, p.center.y - 1f, b * 0.44f, 2f), Color.white);
            BtcReplayUI.Fill(new Rect(p.center.x - 1f, p.y + b * 0.28f, 2f, b * 0.44f), Color.white);
            GUI.Label(v, value, _ctr);
            y += rh + 2f;
            if (BtcSfx.Btn(mi, "", _btn)) return -1;
            if (BtcSfx.Btn(p, "", _btn)) return 1;
            return 0;
        }

        private static int Chips(ref float y, float x, float w, float rh, string[] names, int cur, Event e)
        {
            float gap = 3f, cw = (w - gap * (names.Length - 1)) / names.Length;
            int res = -1;
            for (int i = 0; i < names.Length; i++)
            {
                Rect r = new Rect(x + i * (cw + gap), y, cw, rh);
                bool on = i == cur, hov = r.Contains(e.mousePosition);
                BtcReplayUI.Fill(r, on ? Acc(0.28f) : new Color(1f, 1f, 1f, hov ? 0.12f : 0.05f));
                GUI.Label(r, names[i], _ctr);
                if (BtcSfx.Btn(r, "", _btn) && !on) res = i;
            }
            y += rh + 3f;
            return res;
        }
    }
}
