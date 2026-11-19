using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Aim training page of the Practice tab: start card, target / camera / run settings (BtcAimConfig, undo scope "aim"), predicted
    /// difficulty, personal best and the history line. The same settings can be changed on the result card in game.
    /// </summary>
    internal sealed class BtcAimTab
    {
        internal static readonly BtcAimTab Instance = new BtcAimTab();
        internal const string Scope = "aim";

        private Vector2 _scroll;
        private float _h;
        private GUIStyle _ttl, _ctr;
        private int _fs;
        private readonly List<string> _keys = new List<string>();

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private void Styles(int fs)
        {
            if (_ttl != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _ttl = new GUIStyle(b); _ttl.fontSize = Mathf.RoundToInt(fs * 1.15f); _ttl.fontStyle = FontStyle.Bold; _ttl.alignment = TextAnchor.MiddleLeft; _ttl.normal.textColor = Color.white;
            _ctr = new GUIStyle(b); _ctr.fontSize = Mathf.RoundToInt(fs * 0.95f); _ctr.alignment = TextAnchor.MiddleCenter; _ctr.clipping = TextClipping.Clip; _ctr.normal.textColor = Color.white;
        }

        internal void Draw(Rect area, int fs)
        {
            Styles(fs);
            Event e = Event.current;
            float pad = fs, ib = fs * 1.9f;
            float y0 = area.y + fs * 0.4f;
            GUI.Label(new Rect(area.x + pad, y0, area.width * 0.6f, ib), Tt("Aim training", "Тренировка аима"), _ttl);
            float y = y0 + ib + fs * 0.5f, fx = area.x + pad;
            bool allOpen = true;
            for (int i = 0; i < _keys.Count; i++) if (!BtcUi.IsOpen(_keys[i])) { allOpen = false; break; }
            if (BtcReplayIcons.Plain(new Rect(fx, y, ib, ib), allOpen ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, Color.white, allOpen ? Tt("Collapse all sections", "Свернуть все разделы") : Tt("Expand all sections", "Развернуть все разделы")))
                BtcUi.SetOpen(_keys, !allOpen);
            if (BtcReplayIcons.Plain(new Rect(fx + ib + 3f, y, ib, ib), BtcReplayIcons.Folder, Color.white, Tt("Open the results folder", "Открыть папку с результатами")))
                OpenLogs();
            y += ib + fs * 0.6f;
            BtcReplayUI.Fill(new Rect(area.x + pad, y - fs * 0.25f, area.width - pad * 2f, 1f), BtcMenu.Line);

            Rect view = new Rect(area.x, y, area.width, area.yMax - y);
            float vw = view.width - (_h > view.height ? 14f : 0f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, vw, _h));
            float cy = fs * 0.2f;
            _keys.Clear();
            Body(ref cy, vw, fs, e);
            GUI.EndScrollView();
            _h = cy + fs * 3f;
        }

        private static void OpenLogs()
        {
            try { Directory.CreateDirectory(BtcAimScore.LogDir); System.Diagnostics.Process.Start("explorer.exe", "\"" + BtcAimScore.LogDir + "\""); }
            catch (Exception ex) { BtcLog.Warn("Aim", "open logs failed: " + ex.Message); }
        }

        private string KeyAdd(string k) { _keys.Add(k); return k; }

        /// <summary>Applies a change and records an undo step (one merged step per control while it is clicked repeatedly).</summary>
        private static void Change(string label, Action apply)
        {
            BtcAimConfig.Snap old = BtcAimConfig.Take();
            apply();
            BtcAimConfig.Save();
            BtcUndo.Push(Scope, label, delegate { BtcAimConfig.Put(old); }, "aim:" + label);
        }

        private void Body(ref float y, float w, int fs, Event e)
        {
            bool busy = BtcAim.Active || BtcDuel.Active;
            float bits = BtcAimScore.Predict(BtcAim.UnitWidth, BtcAim.Fov);
            // start card
            {
                float h = fs * 3.2f, pad = fs;
                Rect card = new Rect(pad, y, w - pad * 2f, h);
                bool hov = card.Contains(e.mousePosition) && !busy;
                Color warn = new Color(0.95f, 0.78f, 0.1f, 1f);
                BtcReplayUI.Fill(card, busy ? new Color(1f, 1f, 1f, 0.04f) : BtcMenu.AccentA(hov ? 0.28f : 0.18f));
                BtcReplayUI.Fill(new Rect(card.x, card.y + h * 0.15f, 3f, h * 0.7f), busy ? warn : BtcMenu.Accent);
                float g = h * 0.5f;
                BtcReplayIcons.Glyph(busy ? BtcReplayIcons.Target : BtcReplayIcons.Play, new Rect(card.x + fs * 0.9f, card.center.y - g * 0.5f, g, g), busy ? warn : BtcMenu.Accent);
                GUI.Label(new Rect(card.x + fs * 3f, card.y, card.width - fs * 3.5f, h * 0.6f),
                    BtcAim.Active ? Tt("Aim training is running", "Тренировка идёт") : (BtcDuel.Active ? Tt("A duel is running", "Идёт дуэль") : Tt("Start aim training", "Начать тренировку")), _ttl);
                GUI.Label(new Rect(card.x + fs * 3f, card.y + h * 0.5f, card.width - fs * 3.5f, h * 0.45f),
                    BtcAim.Active ? Tt("Changes apply on Again (R) - no reload.", "Изменения сработают по «Ещё раз» (R) - без перезагрузки.")
                                  : BtcAimConfig.Targets + Tt(" targets", " целей") + "  ·  " + BtcAimConfig.Duration + Tt(" s", " с") + "  ·  " + BtcAimScore.DifficultyName(bits), BtcReplayUI.sLabel);
                if (!busy && BtcSfx.Btn(card, "", BtcReplayUI.sBtn)) { if (BtcAim.Launch()) ResourcePackManager.CloseMenu(); }
                y += h + fs * 0.5f;
            }
            BtcUi.Note(ref y, w, fs, Tt("An empty sky with a one-block floor and enemy halberdiers under a fixed camera. One of them drops to 25% HP: right click it (a focus order) as fast as you can, then the next one drops. Misclicks lower the accuracy.",
                "Пустое небо, пол в один блок и вражеские алебардщики под неподвижной камерой. Один из них падает до 25% HP: кликни по нему ПКМ (как фокус) как можно быстрее, и падает следующий. Промахи снижают точность."));

            Ranks(ref y, w, fs);
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("aim:mode"), Tt("Mode", "Режим"), 0))
            {
                Chips(ref y, w, fs, new string[] { Tt("Focus", "Фокус"), Tt("Most wounded", "Самый раненый") }, (int)BtcAimConfig.Game, e,
                    delegate (int i) { Change(Tt("Mode", "Режим"), delegate { BtcAimConfig.Game = (BtcAimGame)i; }); });
                if (BtcAimConfig.Game == BtcAimGame.Triage)
                {
                    int d = BtcUi.Stepper(ref y, w, fs, Tt("Wounded per round", "Раненых за раунд"), BtcAimConfig.WoundPct + "%  (" + BtcAimConfig.WoundedCount(BtcAimConfig.Targets, BtcAimConfig.WoundPct) + ")");
                    if (d != 0) Change(Tt("Wounded per round", "Раненых за раунд"), delegate { BtcAimConfig.WoundPct += d * 10; });
                    d = BtcUi.Stepper(ref y, w, fs, Tt("HP step between them", "Шаг HP между ними"), BtcAimConfig.StepPct + "%");
                    if (d != 0) Change(Tt("HP step", "Шаг HP"), delegate { BtcAimConfig.StepPct += d * (e.shift ? 5 : 1); });
                    BtcUi.Note(ref y, w, fs, Tt("Several halberdiers drop at once to HP levels that differ by the step (some may be equal). Right click the lowest one; any of the equal lowest counts. A smaller step is harder to read.",
                        "Сразу несколько алебардщиков падают до уровней HP, отличающихся на шаг (некоторые могут совпадать). Кликни ПКМ по самому раненому; если таких несколько, засчитывается любой. Чем меньше шаг, тем сложнее различить."));
                }
                else BtcUi.Note(ref y, w, fs, Tt("One halberdier drops to 25% HP, right click it, the next one drops.", "Один алебардщик падает до 25% HP, кликни ПКМ, падает следующий."));
            }
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("aim:targets"), Tt("Targets", "Цели"), 0))
            {
                int d = BtcUi.Stepper(ref y, w, fs, Tt("Targets", "Цели"), BtcAimConfig.Targets.ToString());
                if (d != 0) Change(Tt("Targets", "Цели"), delegate { BtcAimConfig.Targets += d * (e.shift ? 4 : 1); });
                d = BtcUi.Stepper(ref y, w, fs, Tt("Spacing, blocks", "Расстояние, блоки"), BtcAimConfig.Spacing.ToString("0.0"));
                if (d != 0) Change(Tt("Spacing", "Расстояние"), delegate { BtcAimConfig.Spacing += d * 0.5f; });
                Chips(ref y, w, fs, UnitNames(), (int)BtcAimConfig.Unit, e,
                    delegate (int i) { Change(Tt("Unit", "Юнит"), delegate { BtcAimConfig.Unit = (BtcAimUnit)i; }); });
                Chips(ref y, w, fs, MoveNames(), (int)BtcAimConfig.Move, e,
                    delegate (int i) { Change(Tt("Movement", "Движение"), delegate { BtcAimConfig.Move = (BtcAimMove)i; }); });
                BtcUi.Note(ref y, w, fs, Tt("Shift = +/-4. The unit sets the speed: a knight with the shield up walks (slowest), then knight, worker, halberdier (fastest). Strafe: left-right across the screen. Wander: each on its own. Pack: everyone moves together in changing directions, as in a real fight.",
                    "Shift = +/-4. Юнит задаёт скорость: рыцарь с поднятым щитом идёт шагом (медленнее всех), дальше рыцарь, рабочий, алебардщик (быстрее всех). Стрейф: влево-вправо по экрану. Бродят: каждый сам по себе. Пачка: все двигаются вместе, меняя направление, как в реальном бою."));
            }
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("aim:camera"), Tt("Camera", "Камера"), 0))
            {
                int d = BtcUi.Stepper(ref y, w, fs, Tt("Distance", "Отдаление"), Mathf.RoundToInt(BtcAimConfig.Height).ToString());
                if (d != 0) Change(Tt("Camera distance", "Отдаление камеры"), delegate { BtcAimConfig.Height += d * BtcAimConfig.HeightStep(BtcAimConfig.Height + (d < 0 ? -0.01f : 0f), e.shift); });
                d = BtcUi.Stepper(ref y, w, fs, Tt("Angle (90 = straight down)", "Угол (90 = строго вниз)"), Mathf.RoundToInt(BtcAimConfig.Pitch) + "°");
                if (d != 0) Change(Tt("Camera angle", "Угол камеры"), delegate { BtcAimConfig.Pitch += d * 5f; });
                d = BtcUi.Stepper(ref y, w, fs, Tt("Turn", "Поворот"), Mathf.RoundToInt(BtcAimConfig.Yaw) + "°");
                if (d != 0) Change(Tt("Camera turn", "Поворот камеры"), delegate { BtcAimConfig.Yaw += d * 15f; });
                BtcUi.Note(ref y, w, fs, Tt("The camera cannot be moved during the training.", "Во время тренировки камера не двигается."));
            }
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("aim:run"), Tt("Run", "Попытка"), 0))
            {
                int d = BtcUi.Stepper(ref y, w, fs, Tt("Time, seconds", "Время, секунды"), BtcAimConfig.Duration.ToString());
                if (d != 0) Change(Tt("Time", "Время"), delegate { BtcAimConfig.Duration += d * (e.shift ? 30 : 15); });
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Undo, Tt("Reset to defaults", "Сбросить по умолчанию"),
                        BtcAimConfig.DefTargets + " · " + BtcAimConfig.DefSpacing.ToString("0.0") + " · " + BtcAimConfig.DefHeight + " · 90° · " + BtcAimConfig.DefDuration + Tt(" s", " с") +
                        " · " + BtcAimConfig.DefWound + "% / " + BtcAimConfig.DefStep + "%"))
                    Change(Tt("Reset", "Сброс"), BtcAimConfig.Defaults);
            }
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("aim:score"), Tt("Difficulty and score", "Сложность и счёт"), 0))
            {
                BtcUi.Status(ref y, w, fs, Tt("Difficulty: ", "Сложность: ") + BtcAimScore.DifficultyName(bits) + "  ·  " + bits.ToString("0.0") + Tt(" bit per target", " бит на цель"), BtcMenu.Accent);
                int pb = BtcAimScore.Best(BtcAimConfig.Key(BtcAimConfig.Take())), all = BtcAimScore.Best(null);
                BtcUi.Status(ref y, w, fs, (pb >= 0 ? Tt("Best on this setup: ", "Рекорд на этих настройках: ") + pb + "  ·  " + BtcAimScore.RankName(BtcAimScore.RankOf(pb)) : Tt("No runs on this setup yet", "На этих настройках ещё не играл"))
                    + (all >= 0 ? "   ·   " + Tt("overall ", "общий ") + all : ""), Color.white);
                History(ref y, w, fs);
                BtcUi.Note(ref y, w, fs, Tt("Performance = 150 x throughput x accuracy². Throughput (Fitts' law, bit/s) = difficulty of every target / your reaction time; difficulty grows with the distance to the target, smaller targets (farther camera), more targets to search and movement. So an easy setup gives fast but cheap clicks, a hard one slow but valuable clicks: the number shows your hand, not the settings.",
                    "Перформанс = 150 x пропускная способность x точность². Пропускная способность (закон Фиттса, бит/с) = сложность каждой цели / время реакции; сложность растёт с дистанцией до цели, с уменьшением цели (дальше камера), числом целей для поиска и движением. Лёгкие настройки дают быстрые, но дешёвые клики, сложные - медленные, но дорогие: число отражает руку, а не настройки."));
            }
        }

        /// <summary>Your best performance and rank in each mode (Focus / Most wounded x standing / moving); the current mode is framed.</summary>
        private void Ranks(ref float y, float w, int fs)
        {
            float pad = fs, gap = fs * 0.4f, ch = fs * 4.4f;
            float cw = (w - pad * 2f - gap * (BtcAimScore.Cats - 1)) / BtcAimScore.Cats;
            int cur = BtcAimScore.CatOf(BtcAimConfig.Take());
            GUIStyle big = new GUIStyle(_ttl); big.fontSize = Mathf.RoundToInt(fs * 1.9f); big.alignment = TextAnchor.MiddleLeft;
            GUIStyle dim = new GUIStyle(_ctr); dim.fontSize = Mathf.RoundToInt(fs * 0.78f); dim.alignment = TextAnchor.MiddleLeft; dim.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            for (int c = 0; c < BtcAimScore.Cats; c++)
            {
                Rect r = new Rect(pad + c * (cw + gap), y, cw, ch);
                int runs, best = BtcAimScore.BestCat(c, out runs);
                int rank = best >= 0 ? BtcAimScore.RankOf(best) : -1;
                Color rc = rank >= 0 ? BtcAimScore.RankColor(rank) : new Color(1f, 1f, 1f, 0.25f);
                BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, c == cur ? 0.08f : 0.04f));
                BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, r.height), rc);
                if (c == cur) BtcReplayUI.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), BtcMenu.Accent);
                big.normal.textColor = rc;
                BtcAimHud.DrawRank(new Rect(r.x + fs * 0.7f, r.y + fs * 0.2f, fs * 3.4f, fs * 2.4f), rank >= 0 ? BtcAimScore.RankName(rank) : "-", big, big.normal.textColor);
                GUI.Label(new Rect(r.x + fs * 4.4f, r.y + fs * 0.35f, r.width - fs * 4.8f, fs * 1.2f), best >= 0 ? best.ToString() : Tt("no runs", "нет попыток"), _ctr.fontSize > 0 ? LeftOf(_ctr) : _ctr);
                GUI.Label(new Rect(r.x + fs * 4.4f, r.y + fs * 1.45f, r.width - fs * 4.8f, fs * 1f), runs > 0 ? runs + Tt(" runs", " попыток") : "", dim);
                GUI.Label(new Rect(r.x + fs * 0.7f, r.y + ch - fs * 1.5f, r.width - fs, fs * 1.2f), BtcAimScore.CatName(c), dim);
                BtcReplayIcons.Hint(r, Tt("Best performance in this mode, its rank and the number of runs", "Лучший перформанс в этом режиме, его ранг и число попыток"));
            }
            y += ch + fs * 0.6f;
        }

        private GUIStyle _left;
        private GUIStyle LeftOf(GUIStyle s) { if (_left == null || _left.fontSize != s.fontSize) { _left = new GUIStyle(s); _left.alignment = TextAnchor.MiddleLeft; } return _left; }

        internal static string[] UnitNames()
        {
            return new string[] { Tt("Knight, shield up", "Рыцарь со щитом"), Tt("Knight", "Рыцарь"), Tt("Worker", "Рабочий"), Tt("Halberdier", "Алебардщик") };
        }

        /// <summary>Short class names for the narrow result card.</summary>
        internal static string[] UnitShort()
        {
            return new string[] { Tt("Shield", "Щит"), Tt("Knight", "Рыцарь"), Tt("Worker", "Рабочий"), Tt("Halberd", "Алебарда") };
        }

        internal static string[] MoveNames()
        {
            return new string[] { Tt("Stand", "Стоят"), Tt("Strafe", "Стрейф"), Tt("Wander", "Бродят"), Tt("Pack", "Пачка") };
        }

        private void History(ref float y, float w, int fs)
        {
            List<int> r = BtcAimScore.Recent(40);
            if (r.Count < 2) return;
            float pad = fs, h = fs * 3.2f;
            Rect g = new Rect(pad, y, w - pad * 2f, h);
            BtcReplayUI.Fill(g, new Color(1f, 1f, 1f, 0.03f));
            int mx = 1; for (int i = 0; i < r.Count; i++) mx = Mathf.Max(mx, r[i]);
            float bw = g.width / r.Count;
            for (int i = 0; i < r.Count; i++)
            {
                float bh = (g.height - 4f) * r[i] / mx;
                BtcReplayUI.Fill(new Rect(g.x + i * bw + 1f, g.yMax - bh, Mathf.Max(1f, bw - 2f), bh), i == r.Count - 1 ? BtcMenu.Accent : BtcAimScore.RankColor(BtcAimScore.RankOf(r[i])) * new Color(1f, 1f, 1f, 0.6f));
            }
            BtcReplayIcons.Hint(g, Tt("Last runs (all setups), newest on the right", "Последние попытки (все настройки), новые справа"));
            y += h + fs * 0.5f;
        }

        private void Chips(ref float y, float w, int fs, string[] names, int cur, Event e, Action<int> pick)
        {
            float pad = fs, gap = 3f, ch = fs * 2f;
            float cw = (w - pad * 2f - gap * (names.Length - 1)) / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                Rect r = new Rect(pad + i * (cw + gap), y, cw, ch);
                bool on = i == cur, hov = r.Contains(e.mousePosition);
                BtcReplayUI.Fill(r, on ? BtcMenu.AccentA(0.28f) : new Color(1f, 1f, 1f, hov ? 0.12f : 0.05f));
                GUI.Label(r, names[i], _ctr);
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn) && !on) pick(i);
            }
            y += ch + fs * 0.4f;
        }
    }
}
