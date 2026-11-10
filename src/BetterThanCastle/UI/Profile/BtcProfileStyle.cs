using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Profile tab: the Colours and gradients page (presets, custom stops, reverse / repeat, apply) and the Animation page.</summary>
    internal sealed partial class BtcProfileTab
    {
        // snapshot of the gradient settings for undo
        private sealed class GradState
        {
            internal int Sel; internal bool Rev; internal float Spread; internal int[] Cust;
        }

        private static GradState GradNow()
        {
            GradState g = new GradState();
            g.Sel = BtcNick.GradSel; g.Rev = BtcNick.Reverse; g.Spread = BtcNick.Spread;
            g.Cust = new int[BtcNick.CustomCount];
            for (int i = 0; i < g.Cust.Length; i++) g.Cust[i] = BtcNick.CustomStop(i);
            return g;
        }

        private static void GradSet(GradState g)
        {
            BtcNick.SetCustomStops(g.Cust);
            BtcNick.GradSel = g.Sel;
            BtcNick.Reverse = g.Rev;
            BtcNick.Spread = g.Spread;
        }

        private static void GradUndo(string label, string mergeKey, GradState before)
        {
            BtcUndo.Push(Scope, label, delegate { GradSet(before); }, mergeKey);
        }

        /// <summary>Paints a horizontal gradient bar made of thin slices (stops sampled with the nick engine).</summary>
        private static void GradBar(Rect r, int[] stops, bool loop)
        {
            int seg = Mathf.Clamp((int)(r.width / 4f), 8, 80);
            float sw = r.width / seg;
            for (int i = 0; i < seg; i++)
            {
                float t = seg == 1 ? 0f : (float)i / (seg - 1);
                BtcReplayUI.Fill(new Rect(r.x + i * sw, r.y, sw + 0.6f, r.height), Rgb(BtcNick.ColorOfGradient(stops, t)));
            }
        }

        // ------------------------------------------------------------------ Style page
        private void StylePage(ref float y, float w, int fs)
        {
            float pad = fs;
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:presets", y), Tt("Gradient presets", "Готовые градиенты"), BtcNick.PresetCount))
            {
                int cols = w > fs * 52f ? 4 : 3;
                float gap = 4f, cw = (w - pad * 2f - gap * (cols - 1)) / cols, ch = fs * 2.3f;
                for (int i = 0; i < BtcNick.PresetCount; i++)
                {
                    Rect r = new Rect(pad + (i % cols) * (cw + gap), y + (i / cols) * (ch + gap), cw, ch);
                    bool on = BtcNick.GradSel == i;
                    bool hover = r.Contains(Event.current.mousePosition);
                    BtcReplayUI.Fill(r, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.22f) : new Color(1f, 1f, 1f, hover ? 0.1f : 0.04f));
                    if (on) BtcReplayUI.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), BtcMenu.Accent);
                    GradBar(new Rect(r.x + 4f, r.y + 4f, r.width * 0.42f, r.height - 8f), BtcNick.PresetStops(i), false);
                    Txt(new Rect(r.x + r.width * 0.46f, r.y, r.width * 0.54f, r.height), BtcLang.Tr(BtcNick.PresetName(i)), _lbl, Color.white);
                    BtcReplayIcons.Hint(r, Tt("Click: use this gradient (then apply it to letters below)", "Клик: взять этот градиент (потом применить к буквам ниже)"));
                    if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn) && !on)
                    {
                        GradState before = GradNow();
                        BtcNick.GradSel = i;
                        GradUndo(Tt("Gradient preset", "Готовый градиент"), null, before);
                    }
                }
                y += ((BtcNick.PresetCount + cols - 1) / cols) * (ch + gap) + fs * 0.3f;
            }

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:stops", y), Tt("Your gradient", "Ваш градиент"), 0))
            {
                int[] st = BtcNick.CurrentStops();
                if (st != null && st.Length > 0)
                {
                    Rect bar = new Rect(pad, y, w - pad * 2f, fs * 2.2f);
                    BtcReplayUI.Fill(new Rect(bar.x - 1f, bar.y - 1f, bar.width + 2f, bar.height + 2f), new Color(1f, 1f, 1f, 0.15f));
                    GradBar(bar, st, BtcNick.CurrentLoop);
                    y += bar.height + fs * 0.5f;
                }
                string cur = BtcNick.GradSel >= 0 ? BtcLang.Tr(BtcNick.PresetName(BtcNick.GradSel)) : Tt("Custom", "Свой");
                Txt(new Rect(pad, y, w - pad * 2f, fs * 1.6f), Tt("Now: ", "Сейчас: ") + cur, _lbl, new Color(1f, 1f, 1f, 0.7f));
                y += fs * 1.8f;
                StopRows(ref y, w, fs);
                bool rev = BtcNick.Reverse;
                if (BtcUi.Toggle(ref y, w, fs, Tt("Reverse direction", "Обратное направление"), rev, null))
                {
                    GradState before = GradNow();
                    BtcNick.Reverse = !rev;
                    GradUndo(Tt("Gradient direction", "Направление градиента"), null, before);
                }
                float sp = BtcNick.Spread;
                float nsp = SliderRow(ref y, w, fs, "prof:spread", Tt("Repeat", "Повторов"), sp, 1f, 6f, 0.5f, sp.ToString("0.0"));
                if (Mathf.Abs(nsp - sp) > 0.001f)
                {
                    GradState before = GradNow();
                    BtcNick.Spread = nsp;
                    GradUndo(Tt("Gradient repeat", "Повторы градиента"), "spread", before);
                }
                y += fs * 0.3f;
            }

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:applygrad", y), Tt("Apply to the nick", "Применить к нику"), 0))
            {
                int n = BtcNick.PlainText.Length;
                int lo, hi;
                Sel(out lo, out hi);
                string selHint = _selA < 0 ? Tt("No letters selected: the whole nick is used (select letters on the Nick page)", "Буквы не выделены: берётся весь ник (выделение - на странице Ник)") : Tt("Letters ", "Буквы ") + (lo + 1) + "-" + (hi + 1);
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Palette, Tt("Gradient on the selection", "Градиент на выделение"), selHint) && n > 0)
                {
                    string[] before = BtcNick.Snapshot();
                    BtcNick.PaintGradient(lo, hi);
                    if (BtcNick.Snapshot()[1] != before[1]) PushNick(Tt("Gradient", "Градиент"), before);
                }
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Palette, Tt("Gradient on the whole nick", "Градиент на весь ник"), null) && n > 0)
                {
                    string[] before = BtcNick.Snapshot();
                    BtcNick.PaintGradient(0, n - 1);
                    if (BtcNick.Snapshot()[1] != before[1]) PushNick(Tt("Gradient", "Градиент"), before);
                }
                string msg = BtcNick.Message;
                if (msg != null) BtcUi.Status(ref y, w, fs, msg, new Color(0.95f, 0.78f, 0.1f, 1f));
                y += fs * 0.3f;
            }
        }

        /// <summary>Editable stops of the custom gradient. Editing a preset first copies it into the custom slots.</summary>
        private void StopRows(ref float y, float w, int fs)
        {
            float pad = fs, ib = fs * 1.9f;
            if (BtcNick.GradSel >= 0)
            {
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Gear, Tt("Edit this gradient", "Править этот градиент"), Tt("Copies the preset into your own stops that you can change", "Копирует готовый градиент в ваши точки, которые можно менять")))
                {
                    GradState before = GradNow();
                    int[] src = BtcNick.CurrentStops();
                    List<int> l = new List<int>();
                    for (int i = 0; i < src.Length && l.Count < 6; i++) l.Add(src[i]);
                    BtcNick.SetCustomStops(l);
                    BtcNick.GradSel = -1;
                    GradUndo(Tt("Edit gradient", "Правка градиента"), null, before);
                }
                return;
            }
            int cnt = BtcNick.CustomCount;
            for (int i = 0; i < cnt; i++)
            {
                int idx = i;
                float rowY = y;
                bool canDel = cnt > 2;
                if (canDel && BtcReplayIcons.Plain(new Rect(w - pad - ib, rowY, ib, fs * 2.1f), BtcReplayIcons.Trash, Color.white, Tt("Remove this colour stop", "Убрать эту точку цвета")))
                {
                    GradState before = GradNow();
                    List<int> l = StopList();
                    l.RemoveAt(idx);
                    BtcNick.SetCustomStops(l);
                    BtcVisualColor.Close();
                    GradUndo(Tt("Remove gradient stop", "Удаление точки градиента"), null, before);
                    return;
                }
                BtcVisualColor.Row(ref y, canDel ? w - ib - 3f : w, fs, "profile:stop" + idx, Tt("Colour ", "Цвет ") + (idx + 1), 0, Rgb(BtcNick.CustomStop(idx)), delegate (Color c)
                {
                    GradState before = GradNow();
                    List<int> l = StopList();
                    if (idx < l.Count) l[idx] = ToRgb(c);
                    BtcNick.SetCustomStops(l);
                    GradUndo(Tt("Gradient colour", "Цвет градиента"), "stop" + idx, before);
                }, null);
            }
            if (cnt < 6 && BtcUi.Action(ref y, w, fs, BtcReplayIcons.Plus, Tt("Add a colour stop", "Добавить точку цвета"), null))
            {
                GradState before = GradNow();
                List<int> l = StopList();
                l.Add(l.Count > 0 ? l[l.Count - 1] : 0xFFFFFF);
                BtcNick.SetCustomStops(l);
                GradUndo(Tt("Add gradient stop", "Добавление точки градиента"), null, before);
            }
        }

        private static List<int> StopList()
        {
            List<int> l = new List<int>();
            for (int i = 0; i < BtcNick.CustomCount; i++) l.Add(BtcNick.CustomStop(i));
            return l;
        }

        // ------------------------------------------------------------------ Animation page
        private void AnimPage(ref float y, float w, int fs, bool can, bool inLobby)
        {
            float pad = fs;
            bool longNick = BtcNick.ActiveSlot == 0;
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:anim", y), Tt("Lobby animation", "Анимация в лобби"), 0))
            {
                BtcUi.ToggleUndo(ref y, w, fs, Scope, Tt("Animate the nick in the lobby", "Анимировать ник в лобби"), BtcNick.AnimOn,
                    Tt("The nick changes colours while you wait in the lobby; frozen when the match starts", "Ник переливается, пока вы в лобби; в матче замирает"), delegate (bool v) { BtcNick.AnimOn = v; });
                if (!longNick) BtcUi.Status(ref y, w, fs, Tt("Works only with the long nick (BetterConnection on)", "Работает только с длинным ником (BetterConnection включён)"), new Color(0.95f, 0.78f, 0.1f, 1f));
                string[] names = { Tt("Gradient scroll", "Бегущий градиент"), Tt("Wave", "Волна"), Tt("Shine", "Блик"), Tt("Flicker", "Мерцание"), Tt("Crossfade", "Плавная смена") };
                Txt(new Rect(pad, y, w - pad * 2f, fs * 1.6f), Tt("Effect", "Эффект"), _lbl, new Color(1f, 1f, 1f, 0.7f));
                y += fs * 1.7f;
                float gap = 4f, cw = (w - pad * 2f - gap * (names.Length - 1)) / names.Length, ch = fs * 2.2f;
                for (int i = 0; i < names.Length; i++)
                {
                    Rect r = new Rect(pad + i * (cw + gap), y, cw, ch);
                    if (Chip(r, names[i], BtcFx.Mode == i, null) && BtcFx.Mode != i)
                    {
                        int old = BtcFx.Mode;
                        BtcFx.Mode = i;
                        BtcUndo.Push(Scope, Tt("Animation effect", "Эффект анимации"), delegate { BtcFx.Mode = old; });
                    }
                }
                y += ch + fs * 0.5f;
                BtcUi.Note(ref y, w, fs, Tt("Effects use the colours of your nick and its gradient (Colours and gradients page).", "Эффекты используют цвета вашего ника и его градиент (страница Цвета и градиенты)."));
            }
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:animtune", y), Tt("Speed", "Скорость"), 0))
            {
                float v = BtcNick.AnimSpeed;
                float nv = SliderRow(ref y, w, fs, "prof:aspeed", Tt("Speed", "Скорость"), v, 0.05f, 2f, 0.05f, v.ToString("0.00"));
                if (Mathf.Abs(nv - v) > 0.0005f) { BtcNick.AnimSpeed = nv; BtcUndo.Push(Scope, Tt("Animation speed", "Скорость анимации"), delegate { BtcNick.AnimSpeed = v; }, "aspeed"); }
                float r0 = BtcNick.AnimRate;
                float nr = SliderRow(ref y, w, fs, "prof:arate", Tt("Updates per second", "Обновлений в секунду"), r0, 1f, 10f, 1f, Mathf.RoundToInt(r0).ToString());
                if (Mathf.Abs(nr - r0) > 0.4f) { BtcNick.AnimRate = nr; BtcUndo.Push(Scope, Tt("Animation updates", "Обновления анимации"), delegate { BtcNick.AnimRate = r0; }, "arate"); }
                float sp = BtcNick.Spread;
                float nsp = SliderRow(ref y, w, fs, "prof:aspread", Tt("Repeat", "Повторов"), sp, 1f, 6f, 0.5f, sp.ToString("0.0"));
                if (Mathf.Abs(nsp - sp) > 0.001f) { BtcNick.Spread = nsp; BtcUndo.Push(Scope, Tt("Gradient repeat", "Повторы градиента"), delegate { BtcNick.Spread = sp; }, "spread"); }
                BtcUi.Status(ref y, w, fs, Tt("Real rate: ", "Реальная частота: ") + BtcNick.AnimEffectiveRate.ToString("0.0") + Tt("/s (long nicks update less often to spare the network)", "/с (длинные ники обновляются реже, чтобы беречь сеть)"), new Color(1f, 1f, 1f, 0.6f));
                y += fs * 0.3f;
            }
            BtcUi.Note(ref y, w, fs, Tt("Works only in the lobby, never in the game. The preview above shows the running animation.", "Работает только в лобби, никогда в игре. Превью сверху показывает идущую анимацию."));
        }
    }
}
