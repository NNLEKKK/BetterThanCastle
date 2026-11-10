using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Profile tab: the Nick page (slot, text field, per-letter painting with the mouse, palette, history) and the shared small widgets.</summary>
    internal sealed partial class BtcProfileTab
    {
        private int _selA = -1, _selB = -1;      // letter selection (anchor, cursor); -1 = none = "all letters"
        private bool _drag;
        private Color _paintC = new Color(1f, 0.25f, 0.25f, 1f);
        private string _dragId;

        // ------------------------------------------------------------------ small widgets
        private bool Chip(Rect r, string text, bool on, string tip)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(r, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.2f) : new Color(1f, 1f, 1f, hover ? 0.1f : 0.05f));
            if (on) BtcReplayUI.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), BtcMenu.Accent);
            Txt(r, text, _center, on ? Color.white : new Color(1f, 1f, 1f, 0.75f));
            if (!string.IsNullOrEmpty(tip)) BtcReplayIcons.Hint(r, tip);
            return BtcSfx.Btn(r, "", BtcReplayUI.sBtn);
        }

        private bool StepBtn(Rect r, bool plus)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, hover ? 0.18f : 0.08f));
            Color c = hover ? Color.white : new Color(1f, 1f, 1f, 0.7f);
            BtcReplayUI.Fill(new Rect(r.x + r.width * 0.28f, r.center.y - 1f, r.width * 0.44f, 2f), c);
            if (plus) BtcReplayUI.Fill(new Rect(r.center.x - 1f, r.y + r.height * 0.28f, 2f, r.height * 0.44f), c);
            return BtcSfx.Btn(r, "", BtcReplayUI.sBtn);
        }

        /// <summary>Label + slider + value + minus/plus. Returns the new value (== v when untouched).</summary>
        private float SliderRow(ref float y, float w, int fs, string id, string label, float v, float min, float max, float step, string val)
        {
            Event e = Event.current;
            float pad = fs, rh = fs * 2.3f;
            Rect row = new Rect(pad, y, w - pad * 2f, rh);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.03f));
            float lw = Mathf.Max(fs * 8f, row.width * 0.25f);
            Txt(new Rect(row.x + fs * 0.6f, row.y, lw, rh), label, _lbl, new Color(1f, 1f, 1f, 0.85f));
            float b = rh * 0.7f;
            Rect pr = new Rect(row.xMax - fs * 0.5f - b, row.y + (rh - b) * 0.5f, b, b);
            Rect vr = new Rect(pr.x - fs * 4.2f, row.y, fs * 4f, rh);
            Rect mr = new Rect(vr.x - b - 2f, pr.y, b, b);
            Rect track = new Rect(row.x + fs * 0.6f + lw, row.center.y - 2f, mr.x - (row.x + fs * 0.6f + lw) - fs, 4f);
            Rect hit = new Rect(track.x - 6f, track.y - fs * 0.6f, track.width + 12f, track.height + fs * 1.2f);
            bool hov = hit.Contains(e.mousePosition) || _dragId == id;
            BtcReplayUI.Fill(track, new Color(1f, 1f, 1f, 0.12f));
            float f = Mathf.Clamp01((v - min) / Mathf.Max(0.0001f, max - min));
            BtcReplayUI.Fill(new Rect(track.x, track.y, track.width * f, track.height), new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, hov ? 1f : 0.8f));
            float kw = fs * 0.55f, kh = fs * 1.3f, kx = Mathf.Clamp(track.x + track.width * f - kw * 0.5f, track.x, track.xMax - kw);
            if (hov) BtcReplayUI.Fill(new Rect(kx - 2f, track.center.y - kh * 0.5f - 2f, kw + 4f, kh + 4f), new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.25f));
            BtcReplayUI.Fill(new Rect(kx, track.center.y - kh * 0.5f, kw, kh), hov ? Color.white : new Color(1f, 1f, 1f, 0.85f));
            Txt(vr, val, _center, Color.white);
            float nv = v;
            if (e.rawType == EventType.MouseUp) _dragId = null;
            if (e.type == EventType.MouseDown && hit.Contains(e.mousePosition) && e.button == 0) _dragId = id;
            if (_dragId == id && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                float t = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                nv = min + t * (max - min);
                if (step > 0f) nv = Mathf.Round(nv / step) * step;
                nv = Mathf.Clamp(nv, min, max);
                e.Use();
            }
            if (StepBtn(mr, false)) nv = Mathf.Clamp(Mathf.Round((v - step) / step) * step, min, max);
            if (StepBtn(pr, true)) nv = Mathf.Clamp(Mathf.Round((v + step) / step) * step, min, max);
            y += rh + 3f;
            return nv;
        }

        private void PushNick(string label, string[] before, string mergeKey)
        {
            BtcUndo.Push(Scope, label, delegate { BtcNick.Restore(before); _force = true; }, mergeKey);
        }

        // ------------------------------------------------------------------ selection helpers
        private void Sel(out int lo, out int hi)
        {
            int n = BtcNick.PlainText.Length;
            if (_selA < 0 || n == 0) { lo = 0; hi = Math.Max(0, n - 1); return; }
            lo = Mathf.Clamp(Math.Min(_selA, _selB), 0, n - 1);
            hi = Mathf.Clamp(Math.Max(_selA, _selB), 0, n - 1);
        }

        private void PaintSel(int rgb, string label, string mergeKey)
        {
            if (BtcNick.PlainText.Length == 0) return;
            int lo, hi;
            Sel(out lo, out hi);
            string[] before = BtcNick.Snapshot();
            BtcNick.Paint(lo, hi, rgb);
            string[] after = BtcNick.Snapshot();
            if (after[1] != before[1]) PushNick(label, before, mergeKey);
        }

        // ------------------------------------------------------------------ Nick page
        private void NickPage(ref float y, float w, int fs, bool can)
        {
            float pad = fs;
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:slot", y), Tt("Which nick", "Какой ник"), 0))
            {
                float gap = 4f, cw = (w - pad * 2f - gap) * 0.5f, ch = fs * 2.3f;
                int active = BtcNick.ActiveSlot;
                for (int s = 0; s < 2; s++)
                {
                    Rect r = new Rect(pad + s * (cw + gap), y, cw, ch);
                    string name = s == 0 ? Tt("Long nick (BetterConnection)", "Длинный ник (BetterConnection)") : Tt("Short nick (vanilla lobby)", "Короткий ник (ванильное лобби)");
                    string tip = s == 0 ? Tt("Up to 999 characters with colours. Used when BetterConnection is ON", "До 999 символов с цветами. Используется при включённом BetterConnection")
                        : Tt("30 characters including colour tags. Used when BetterConnection is OFF (vanilla lobby)", "30 символов, включая теги цвета. Используется при выключенном BetterConnection (ванильное лобби)");
                    if (Chip(r, name, BtcNick.Slot == s, tip)) { BtcNick.SelectSlot(s); _selA = _selB = -1; GUIUtility.keyboardControl = 0; }
                    if (active == s) BtcReplayUI.Fill(new Rect(r.xMax - fs * 1.1f, r.y + fs * 0.45f, fs * 0.6f, fs * 0.6f), BtcMenu.Accent);
                }
                y += ch + 4f;
                BtcUi.Note(ref y, w, fs, Tt("The green dot marks the nick that is used now (it depends on BetterConnection). The other one is saved and used when the mode changes.", "Зелёная точка - ник, который используется сейчас (зависит от BetterConnection). Второй сохранён и подставится при смене режима."));
            }

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:text", y), Tt("Text", "Текст"), 0))
            {
                DrawTextField(ref y, w, fs);
                int used = BtcNick.UsedChars;
                if (used < BtcNick.PlainText.Length)
                    BtcUi.Status(ref y, w, fs, Tt("Does not fit the length limit: ", "Не помещается в лимит длины: ") + (BtcNick.PlainText.Length - used) + Tt(" characters are cut", " символов обрезается"), new Color(0.95f, 0.4f, 0.4f, 1f));
                string msg = BtcNick.Message;
                if (msg != null) BtcUi.Status(ref y, w, fs, msg, new Color(0.95f, 0.78f, 0.1f, 1f));
                if (BtcNick.AnimOn && BtcNick.Slot == 0)
                    BtcUi.Note(ref y, w, fs, Tt("The lobby animation is on: in the lobby it overrides these colours (Animation page).", "Анимация в лобби включена: в лобби она перекрывает эти цвета (страница Анимация)."));
            }

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:paint", y), Tt("Colour the letters", "Покраска букв"), 0))
            {
                DrawLetters(ref y, w, fs);
                DrawPaintTools(ref y, w, fs);
                DrawPalette(ref y, w, fs);
                int rgb = ToRgb(_paintC);
                Color pc = _paintC;
                BtcVisualColor.Row(ref y, w, fs, "profile:paint", Tt("Any colour", "Любой цвет"), 0, pc, delegate (Color c) { _paintC = c; PaintSel(ToRgb(c), Tt("Paint letters", "Покраска букв"), "paintpick"); }, Tt("Pick a colour: the selected letters (or all) are painted live", "Выберите цвет: выделенные буквы (или все) красятся сразу"));
                y += fs * 0.3f;
            }

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:opts", y), Tt("Saving", "Сохранение"), 0))
            {
                BtcUi.ToggleUndo(ref y, w, fs, Scope, Tt("Auto-apply changes", "Применять сразу"), BtcNick.AutoApply, Tt("Every change goes to the lobby at once; off = use the check icon in the header", "Каждое изменение сразу уходит в лобби; выкл = кнопка-галочка в шапке"), delegate (bool v) { BtcNick.AutoApply = v; });
                BtcUi.ToggleUndo(ref y, w, fs, Scope, Tt("Keep this nick for good", "Запоминать ник навсегда"), BtcNick.KeepForGood, Tt("Writes the nick into the game settings; off = this run only", "Записывает ник в настройки игры; выкл = только до выхода"), delegate (bool v) { BtcNick.KeepForGood = v; });
                y += fs * 0.3f;
            }

            IList<string> hist = BtcNick.History;
            if (hist.Count > 0 && BtcUi.Fold(ref y, w, fs, KeyAdd("profile:hist", y), Tt("Recent nicks", "Недавние ники"), hist.Count))
            {
                for (int i = 0; i < hist.Count; i++)
                {
                    Rect r = new Rect(pad, y, w - pad * 2f, fs * 2.2f);
                    bool hover = r.Contains(Event.current.mousePosition);
                    BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, hover ? 0.08f : 0.03f));
                    Txt(new Rect(r.x + fs * 0.6f, r.y, r.width - fs * 1.2f, r.height), hist[i], _richSmall(), Color.white);
                    BtcReplayIcons.Hint(r, Tt("Click: load this nick into the editor", "Клик: загрузить этот ник в редактор"));
                    if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn))
                    {
                        string[] before = BtcNick.Snapshot();
                        BtcNick.SetTagsRaw(hist[i]);
                        PushNick(Tt("Load nick", "Загрузка ника"), before);
                        _selA = _selB = -1;
                    }
                    y += r.height + 3f;
                }
            }
        }

        private GUIStyle _richS;
        private GUIStyle _richSmall()
        {
            if (_richS == null || _richS.fontSize != Mathf.RoundToInt(_fs * 1.1f))
            {
                _richS = new GUIStyle(_rich);
                _richS.fontSize = Mathf.RoundToInt(_fs * 1.1f);
            }
            return _richS;
        }

        private void DrawTextField(ref float y, float w, int fs)
        {
            Event e = Event.current;
            float pad = fs, rh = fs * 2.4f;
            Rect row = new Rect(pad, y, w - pad * 2f, rh);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.06f));
            BtcReplayUI.Fill(new Rect(row.x, row.yMax - 1f, row.width, 1f), new Color(1f, 1f, 1f, 0.15f));
            float cw = fs * 9f;
            Rect fr = new Rect(row.x, row.y, row.width - cw, rh);
            Vector2 sp = GUIUtility.GUIToScreenPoint(new Vector2(fr.x, fr.y));
            BtcUi.Field(new Rect(sp.x, sp.y, fr.width, fr.height));
            GUI.SetNextControlName("btc_prof_nick");
            bool focused = GUI.GetNameOfFocusedControl() == "btc_prof_nick";
            if (focused && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                bool il;
                if (BtcNick.EditAllowed(out il)) { BtcNick.ApplyNow(); BtcSfx.Ok(); }
                e.Use();
            }
            string plain = BtcNick.PlainText;
            string np = GUI.TextField(fr, plain, BtcNick.Limit, _tf);
            if (np != plain)
            {
                string[] before = BtcNick.Snapshot();
                BtcNick.SetPlain(np);
                PushNick(Tt("Edit nick", "Правка ника"), before, "nicktext");
                if (_selA >= np.Length) _selA = _selB = -1;
            }
            if (plain.Length == 0 && !focused) Txt(new Rect(fr.x + 8f, fr.y, fr.width, rh), Tt("Type a nick", "Введите ник"), _lbl, new Color(1f, 1f, 1f, 0.35f));
            Txt(new Rect(row.xMax - cw, row.y, cw - fs * 0.5f, rh), BtcNick.Tags.Length + "/" + BtcNick.Limit, _rightDim(), new Color(1f, 1f, 1f, 0.55f));
            y += rh + 3f;
            BtcUi.Note(ref y, w, fs, Tt("The counter is the length with colour tags. Every coloured letter costs about 23 characters of the limit, so long nicks may need fewer colour changes.", "Счётчик - длина вместе с тегами цвета. Каждая смена цвета стоит около 23 символов лимита, поэтому длинным никам нужно меньше смен цвета."));
        }

        private GUIStyle _rightS;
        private GUIStyle _rightDim()
        {
            if (_rightS == null) { _rightS = new GUIStyle(_dim); _rightS.alignment = TextAnchor.MiddleRight; }
            return _rightS;
        }

        // ------------------------------------------------------------------ letters
        private void DrawLetters(ref float y, float w, int fs)
        {
            Event e = Event.current;
            float pad = fs;
            string p = BtcNick.PlainText;
            int[] cols = BtcNick.Colors;
            int n = p.Length;
            float cw = fs * 1.4f, chh = fs * 2.4f;
            int per = Mathf.Max(1, (int)((w - pad * 2f) / cw));
            int rows = Mathf.Max(1, (n + per - 1) / per);
            Rect strip = new Rect(pad, y, w - pad * 2f, rows * chh + 4f);
            BtcReplayUI.Fill(strip, new Color(0f, 0f, 0f, 0.35f));
            if (n == 0)
            {
                Txt(strip, Tt("Type a nick above, then drag over the letters to select them", "Введите ник выше, затем проведите мышью по буквам, чтобы выделить"), _center, new Color(1f, 1f, 1f, 0.4f));
                y += strip.height + 4f;
                return;
            }
            if (_selA >= n) _selA = _selB = -1;
            Vector2 mp = e.mousePosition;
            int idx = -1;
            if (strip.Contains(mp) || _drag)
            {
                int cx = Mathf.Clamp((int)((mp.x - strip.x) / cw), 0, per - 1);
                int cyy = Mathf.Clamp((int)((mp.y - strip.y - 2f) / chh), 0, rows - 1);
                idx = Mathf.Clamp(cyy * per + cx, 0, n - 1);
            }
            if (e.type == EventType.MouseDown && e.button == 0 && strip.Contains(mp) && idx >= 0)
            {
                if (e.shift && _selA >= 0) _selB = idx; else { _selA = idx; _selB = idx; }
                _drag = true;
                BtcSfx.Tick();
                e.Use();
            }
            else if (_drag && e.type == EventType.MouseDrag && idx >= 0) { _selB = idx; e.Use(); }
            if (e.rawType == EventType.MouseUp) _drag = false;
            int lo = -1, hi = -1;
            if (_selA >= 0) { lo = Math.Min(_selA, _selB); hi = Math.Max(_selA, _selB); }
            {
                for (int i = 0; i < n; i++)
                {
                    int cxi = i % per, cyi = i / per;
                    Rect c = new Rect(strip.x + cxi * cw, strip.y + 2f + cyi * chh, cw, chh);
                    bool sel = i >= lo && i <= hi && lo >= 0;
                    if (sel) BtcReplayUI.Fill(new Rect(c.x, c.y + 1f, c.width, c.height - 2f), new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.28f));
                    else if (i == idx) BtcReplayUI.Fill(new Rect(c.x, c.y + 1f, c.width, c.height - 2f), new Color(1f, 1f, 1f, 0.06f));
                    Color lc = cols[i] < 0 ? Color.white : Rgb(cols[i]);
                    string ch = p[i] == ' ' ? "·" : p[i].ToString();
                    Txt(new Rect(c.x, c.y, c.width, c.height - fs * 0.35f), ch, _center, p[i] == ' ' ? new Color(1f, 1f, 1f, 0.3f) : lc);
                    BtcReplayUI.Fill(new Rect(c.x + 2f, c.yMax - fs * 0.45f, c.width - 4f, fs * 0.3f), cols[i] < 0 ? new Color(1f, 1f, 1f, 0.25f) : lc);
                }
            }
            BtcReplayIcons.Hint(strip, Tt("Drag over letters to select a range, click for one letter, Shift+click extends. Nothing selected = all letters", "Проведите мышью по буквам - диапазон, клик - одна буква, Shift+клик расширяет. Ничего не выбрано = все буквы"));
            y += strip.height + 4f;
        }

        private void DrawPaintTools(ref float y, float w, int fs)
        {
            float pad = fs, ib = fs * 1.9f;
            float x = pad;
            int n = BtcNick.PlainText.Length;
            int lo, hi;
            Sel(out lo, out hi);
            if (BtcReplayIcons.Plain(new Rect(x, y, ib, ib), BtcReplayIcons.Grid, Color.white, Tt("Select all letters", "Выделить все буквы"))) { _selA = 0; _selB = Math.Max(0, n - 1); }
            x += ib + 3f;
            if (BtcReplayIcons.Plain(new Rect(x, y, ib, ib), BtcReplayIcons.Cross, Color.white, Tt("Clear the selection (colouring then affects all letters)", "Снять выделение (покраска тогда идёт по всем буквам)"))) { _selA = _selB = -1; }
            x += ib + 3f;
            if (BtcReplayIcons.Plain(new Rect(x, y, ib, ib), BtcReplayIcons.Palette, Color.white, Tt("Paint the selection with the gradient chosen on the Colours and gradients page", "Покрасить выделение градиентом со страницы Цвета и градиенты")) && n > 0)
            {
                string[] before = BtcNick.Snapshot();
                BtcNick.PaintGradient(lo, hi);
                if (BtcNick.Snapshot()[1] != before[1]) PushNick(Tt("Gradient", "Градиент"), before);
            }
            x += ib + 3f;
            if (BtcReplayIcons.Plain(new Rect(x, y, ib, ib), BtcReplayIcons.Trash, Color.white, Tt("Remove all colours (plain white nick)", "Убрать все цвета (обычный белый ник)")) && n > 0)
            {
                string[] before = BtcNick.Snapshot();
                BtcNick.StripColors();
                PushNick(Tt("Strip colours", "Снятие цветов"), before);
            }
            x += ib + fs;
            string info = n == 0 ? "" : (_selA < 0 ? Tt("Painting: all ", "Красим: все ") + n + Tt(" letters", " букв") : Tt("Selected: ", "Выбрано: ") + (hi - lo + 1) + " (" + (lo + 1) + "-" + (hi + 1) + ")");
            Txt(new Rect(x, y, w - x - pad, ib), info, _lbl, new Color(1f, 1f, 1f, 0.7f));
            y += ib + fs * 0.4f;
        }

        private void DrawPalette(ref float y, float w, int fs)
        {
            float pad = fs, gap = 3f;
            string[] pal = BtcNick.PaletteHex;
            int per = 12;
            float s = Mathf.Min(fs * 2.2f, (w - pad * 2f - gap * (per - 1)) / per);
            for (int i = 0; i < pal.Length; i++)
            {
                int rgb;
                BtcNick.HexToRgb(pal[i], out rgb);
                Rect r = new Rect(pad + (i % per) * (s + gap), y + (i / per) * (s + gap), s, s);
                BtcVisualColor.Swatch(r, Rgb(rgb), ToRgb(_paintC) == rgb);
                if (r.Contains(Event.current.mousePosition)) BtcReplayIcons.Hint(r, "#" + pal[i].ToUpperInvariant());
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) { _paintC = Rgb(rgb); PaintSel(rgb, Tt("Paint letters", "Покраска букв"), null); }
            }
            y += ((pal.Length + per - 1) / per) * (s + gap) + fs * 0.4f;
        }
    }
}
