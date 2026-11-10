using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Profile tab: the Plate page and the editor canvas. The canvas draws the plate in real plate pixels (scaled), and its parts are dragged with the mouse:
    /// avatar (move, corner = size), nick (move, wheel = size), plate bottom edge (height). Parts are clamped so the nick and the avatar can never leave the plate.
    /// Below the canvas the settings are grouped in folds (Background, Frame, Nick, Letter effects, Size, Image). Undo scope "profile", snapshot = BtcPlate.Export().
    /// </summary>
    internal sealed partial class BtcProfileTab
    {
        private int _dragMode;                  // 0 none, 1 avatar, 2 nick, 3 avatar size, 4 plate height
        private Vector2 _dragStart;
        private string _dragBefore;
        private int _d0, _d1;
        private string _nfEdit;

        private static void PlateUndoPush(string label, string before, string mergeKey)
        {
            BtcUndo.Push(Scope, label, delegate { BtcPlate.Import(before); }, mergeKey);
        }

        /// <summary>Sets a plate setting and records an undo step.</summary>
        private static void PSet(string key, int v, string label, string mergeKey)
        {
            string before = BtcPlate.Export();
            if (BtcPlate.Set(key, v)) PlateUndoPush(label, before, mergeKey);
        }

        // ------------------------------------------------------------------ canvas
        /// <summary>Draws the plate canvas. interactive = the Plate page is open (drag handles). Returns the height used.</summary>
        private float DrawPlateCanvas(Rect r, int fs, bool interactive, string nickTags)
        {
            Event e = Event.current;
            bool on = BtcNick.StyleOn;
            BtcPlate.EnsureLoaded();
            float W = BtcPlate.RealW, baseH = BtcPlate.RealBaseH;
            int ph = BtcPlate.Get("ph");
            float H = baseH * ph / 10f;
            float margin = fs * 0.7f + BtcPlate.Get("fw") * 0.5f;
            float maxW = r.width - margin * 2f, maxH = fs * (interactive ? 11f : 4.2f);
            float k = Mathf.Min(maxW / W, maxH / H);
            if (!interactive) k = Mathf.Min(k, fs * 20f / W);
            Rect back = new Rect(r.x, r.y, r.width, H * k + margin * 2f);
            BtcReplayUI.Fill(back, new Color(0f, 0f, 0f, 0.3f));
            Rect plate = new Rect(back.x + (back.width - W * k) * 0.5f, back.y + margin, W * k, H * k);
            if (!on)
            {
                Txt(back, Tt("Mod styling is off: the default plate is shown to others", "Оформление мода выключено: другие видят стандартную плашку"), _center, new Color(1f, 1f, 1f, 0.45f));
                return back.height;
            }
            Rect av, nk, tx;
            BtcPlate.DrawCanvas(plate, k, nickTags, BtcAvatar.LocalTex, _lbl, _px, out av, out nk, out tx);
            if (!interactive) return back.height;

            // ---- limits so nothing leaves the plate
            int ax = BtcPlate.Get("ax"), ay = BtcPlate.Get("ay"), ox = BtcPlate.Get("ox"), oy = BtcPlate.Get("oy");
            float pad2 = 1f;
            float axLo = (plate.x + pad2 - (av.x - ax * k)) / k, axHi = (plate.xMax - av.width - pad2 - (av.x - ax * k)) / k;
            float ay0 = av.y + ay * k;
            float ayLo = (ay0 - (plate.yMax - av.height - pad2)) / k, ayHi = (ay0 - (plate.y + pad2)) / k;
            float tx0 = tx.x - ox * k, ty0 = tx.y + oy * k;
            float oxLo = (plate.x + pad2 - tx0) / k, oxHi = (plate.xMax - tx.width - pad2 - tx0) / k;
            float oyLo = (ty0 - (plate.yMax - tx.height - pad2)) / k, oyHi = (ty0 - (plate.y + pad2)) / k;
            if (axHi < axLo) axLo = axHi = (axLo + axHi) * 0.5f;
            if (ayHi < ayLo) ayLo = ayHi = (ayLo + ayHi) * 0.5f;
            if (oxHi < oxLo) oxLo = oxHi = (oxLo + oxHi) * 0.5f;
            if (oyHi < oyLo) oyLo = oyHi = (oyLo + oyHi) * 0.5f;
            float maxFit = Mathf.Min((plate.height - 2f) / Mathf.Max(1f, av.height), (plate.width - 2f) / Mathf.Max(1f, av.width));

            Rect corner = new Rect(av.xMax - fs * 0.45f, av.yMax - fs * 0.45f, fs * 0.9f, fs * 0.9f);
            Rect grip = new Rect(plate.center.x - fs * 1.6f, plate.yMax - fs * 0.35f, fs * 3.2f, fs * 0.7f);
            Rect txHit = new Rect(tx.x - 4f, tx.y - 2f, Mathf.Max(tx.width, fs) + 8f, Mathf.Max(tx.height, fs) + 4f);
            Rect nickH = new Rect(txHit.xMax - fs * 0.45f, txHit.yMax - fs * 0.45f, fs * 0.9f, fs * 0.9f);
            Vector2 mp = e.mousePosition;
            bool hAv = av.Contains(mp) || corner.Contains(mp), hNick = txHit.Contains(mp), hGrip = grip.Contains(mp);
            int hover = _dragMode != 0 ? _dragMode : (corner.Contains(mp) ? 3 : (nickH.Contains(mp) ? 5 : (hGrip ? 4 : (hAv ? 1 : (hNick ? 2 : 0)))));

            // ---- visuals of the handles
            Color ac = BtcMenu.Accent;
            DrawFrameRect(av, hover == 1 || hover == 3 ? ac : new Color(1f, 1f, 1f, 0.25f));
            BtcReplayUI.Fill(corner, hover == 3 ? Color.white : new Color(ac.r, ac.g, ac.b, 0.9f));
            DrawFrameRect(txHit, hover == 2 ? ac : new Color(1f, 1f, 1f, 0.18f));
            BtcReplayUI.Fill(nickH, hover == 5 ? Color.white : new Color(ac.r, ac.g, ac.b, 0.9f));
            BtcReplayUI.Fill(grip, hover == 4 ? Color.white : new Color(ac.r, ac.g, ac.b, 0.8f));
            if (_dragMode == 1 || _dragMode == 2)
            {
                BtcReplayUI.Fill(new Rect(plate.center.x - 0.5f, plate.y, 1f, plate.height), new Color(1f, 1f, 1f, Mathf.Abs((_dragMode == 1 ? av.center.x : tx.center.x) - plate.center.x) < 4f ? 0.7f : 0.15f));
                BtcReplayUI.Fill(new Rect(plate.x, plate.center.y - 0.5f, plate.width, 1f), new Color(1f, 1f, 1f, Mathf.Abs((_dragMode == 1 ? av.center.y : tx.center.y) - plate.center.y) < 4f ? 0.7f : 0.15f));
            }
            if (hover == 1) BtcReplayIcons.Hint(av, Tt("Drag: move the avatar", "Тяните: двигать аватарку"));
            if (hover == 2) BtcReplayIcons.Hint(txHit, Tt("Drag: move the nick, mouse wheel: nick size", "Тяните: двигать ник, колесо мыши: размер ника"));
            if (hover == 3) BtcReplayIcons.Hint(corner, Tt("Drag: avatar size", "Тяните: размер аватарки"));
            if (hover == 5) BtcReplayIcons.Hint(nickH, Tt("Drag: nick size", "Тяните: размер ника"));
            if (hover == 4) BtcReplayIcons.Hint(grip, Tt("Drag: plate height", "Тяните: высота плашки"));

            // ---- input
            if (e.type == EventType.MouseDown && e.button == 0 && hover != 0 && _dragMode == 0)
            {
                _dragMode = hover;
                _dragStart = mp;
                _dragBefore = BtcPlate.Export();
                if (hover == 1) { _d0 = ax; _d1 = ay; }
                else if (hover == 2) { _d0 = ox; _d1 = oy; }
                else if (hover == 3) _d0 = BtcPlate.Get("asz");
                else if (hover == 5) _d0 = BtcPlate.Get("nsz");
                else _d0 = ph;
                GUIUtility.keyboardControl = 0;
                BtcSfx.Tick();
                e.Use();
            }
            else if (_dragMode != 0 && e.type == EventType.MouseDrag)
            {
                float dx = (mp.x - _dragStart.x) / k, dy = (mp.y - _dragStart.y) / k;
                if (_dragMode == 1)
                {
                    BtcPlate.Set("ax", Mathf.RoundToInt(Mathf.Clamp(_d0 + dx, axLo, axHi)));
                    BtcPlate.Set("ay", Mathf.RoundToInt(Mathf.Clamp(_d1 - dy, ayLo, ayHi)));
                }
                else if (_dragMode == 2)
                {
                    BtcPlate.Set("ox", Mathf.RoundToInt(Mathf.Clamp(_d0 + dx, oxLo, oxHi)));
                    BtcPlate.Set("oy", Mathf.RoundToInt(Mathf.Clamp(_d1 - dy, oyLo, oyHi)));
                }
                else if (_dragMode == 3)
                {
                    int cur3 = BtcPlate.Get("asz"), t3 = Mathf.RoundToInt(_d0 + (dx + dy) * 0.5f / (BtcPlate.G.AvW * BtcPlateK()) * 20f);
                    if (maxFit < 1.001f) t3 = Mathf.Min(t3, cur3);
                    BtcPlate.Set("asz", t3);
                }
                else if (_dragMode == 5) BtcPlate.Set("nsz", Mathf.RoundToInt(_d0 + (dx + dy) * 0.5f / (BtcPlate.G.Font * BtcPlateK()) * 20f));
                else BtcPlate.Set("ph", Mathf.RoundToInt(_d0 + dy / baseH * 10f));
                e.Use();
            }
            if (e.rawType == EventType.MouseUp && _dragMode != 0)
            {
                _dragMode = 0;
                if (_dragBefore != null && _dragBefore != BtcPlate.Export()) PlateUndoPush(Tt("Plate layout", "Расположение на плашке"), _dragBefore, null);
                _dragBefore = null;
            }
            if (e.type == EventType.ScrollWheel && txHit.Contains(mp))
            {
                PSet("nsz", BtcPlate.Get("nsz") - (int)Mathf.Sign(e.delta.y) * 2, Tt("Nick size", "Размер ника"), "nsz");
                e.Use();
            }
            // invariant: nothing outside the plate (silent clamp after any other change, e.g. a bigger nick or another alignment)
            if (_dragMode == 0)
            {
                if (maxFit < 0.999f) BtcPlate.Set("asz", Mathf.Max(10, Mathf.FloorToInt(BtcPlate.Get("asz") * maxFit)));
                int cax = Mathf.RoundToInt(Mathf.Clamp(ax, axLo, axHi)), cay = Mathf.RoundToInt(Mathf.Clamp(ay, ayLo, ayHi));
                int cox = Mathf.RoundToInt(Mathf.Clamp(ox, oxLo, oxHi)), coy = Mathf.RoundToInt(Mathf.Clamp(oy, oyLo, oyHi));
                if (cax != ax) BtcPlate.Set("ax", cax);
                if (cay != ay) BtcPlate.Set("ay", cay);
                if (cox != ox) BtcPlate.Set("ox", cox);
                if (coy != oy) BtcPlate.Set("oy", coy);
            }
            return back.height;
        }

        private static float BtcPlateK() { return BtcPlate.Get("sc") != 0 ? BtcPlate.Get("ph") / 10f : 1f; }

        private static void DrawFrameRect(Rect r, Color c)
        {
            BtcReplayUI.Fill(new Rect(r.x, r.y, r.width, 1f), c);
            BtcReplayUI.Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            BtcReplayUI.Fill(new Rect(r.x, r.y, 1f, r.height), c);
            BtcReplayUI.Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }

        // ------------------------------------------------------------------ rows
        private void PChoice(ref float y, float w, int fs, string key, string label, string[] names, int firstValue)
        {
            float pad = fs, gap = 4f, ch = fs * 2.1f;
            if (label != null) { Txt(new Rect(pad, y, w - pad * 2f, fs * 1.5f), label, _lbl, new Color(1f, 1f, 1f, 0.7f)); y += fs * 1.6f; }
            int cur = BtcPlate.Get(key) - firstValue;
            int per = Mathf.Max(1, Mathf.Min(names.Length, (int)((w - pad * 2f) / (fs * 6.5f))));
            float cw = (w - pad * 2f - gap * (per - 1)) / per;
            for (int i = 0; i < names.Length; i++)
            {
                Rect r = new Rect(pad + (i % per) * (cw + gap), y + (i / per) * (ch + gap), cw, ch);
                if (Chip(r, names[i], cur == i, null) && cur != i) PSet(key, i + firstValue, label ?? key, null);
            }
            y += ((names.Length + per - 1) / per) * (ch + gap) + fs * 0.2f;
        }

        private void PSlider(ref float y, float w, int fs, string key, string label, int step, string suffix, float mul)
        {
            BtcPlate.Def d = BtcPlate.DefOf(key);
            int v = BtcPlate.Get(key);
            float nv = SliderRow(ref y, w, fs, "pl:" + key, label, v, d.Lo, d.Hi, step, Mathf.RoundToInt(v * mul) + suffix);
            if (Mathf.RoundToInt(nv) != v) PSet(key, Mathf.RoundToInt(nv), label, key);
        }

        private void PColor(ref float y, float w, int fs, string key, string label)
        {
            string k = key;
            BtcVisualColor.Row(ref y, w, fs, "profile:pl:" + key, label, 0, Rgb(BtcPlate.Get(key)), delegate (Color c) { PSet(k, ToRgb(c), label, "col" + k); }, null);
        }

        private void PToggle(ref float y, float w, int fs, string key, string label, string hint)
        {
            bool v = BtcPlate.Get(key) != 0;
            if (BtcUi.Toggle(ref y, w, fs, label, v, hint)) PSet(key, v ? 0 : 1, label, null);
        }

        // ------------------------------------------------------------------ page
        private void PlatePage(ref float y, float w, int fs)
        {
            float pad = fs;
            BtcPlate.EnsureLoaded();
            BtcUi.Note(ref y, w, fs, Tt("Drag the avatar, the nick and the bottom edge of the plate right on the preview above. Players with the mod see this plate in the lobby list; others see the default one.", "Двигайте аватарку, ник и нижний край плашки прямо на превью выше. Игроки с модом видят эту плашку в списке лобби, остальные - стандартную."));
            string[] off = { Tt("Off", "Выкл") };

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:pl:bg", y), Tt("Background", "Фон"), 0))
            {
                PChoice(ref y, w, fs, "style", null, new[] { Tt("Off", "Выкл"), Tt("Cycle", "Смена"), Tt("Rainbow", "Радуга"), Tt("Pulse", "Пульс"), Tt("Neon", "Неон") }, 0);
                int st = BtcPlate.Get("style"), gr = BtcPlate.Get("grad");
                if (st == 1 || st == 3 || st == 4 || gr > 0) PColor(ref y, w, fs, "a", Tt("Colour A", "Цвет A"));
                if (st == 1 || st == 4) PColor(ref y, w, fs, "b", Tt("Colour B", "Цвет B"));
                PChoice(ref y, w, fs, "grad", Tt("Gradient", "Градиент"), new[] { Tt("None", "Нет"), Tt("Horizontal", "Горизонтальный"), Tt("Vertical", "Вертикальный") }, 0);
                if (BtcPlate.Get("grad") > 0) PColor(ref y, w, fs, "g", Tt("Gradient end colour", "Конечный цвет градиента"));
                if (st > 0 || gr > 0 || BtcPlate.Get("nm") >= 2 || BtcPlate.Get("fm") >= 2)
                {
                    float sp = BtcPlate.SpeedF;
                    float nv = SliderRow(ref y, w, fs, "pl:speed", Tt("Effect speed", "Скорость эффектов"), sp, 0.1f, 3f, 0.1f, sp.ToString("0.0"));
                    if (Mathf.Abs(nv - sp) > 0.01f) { string b = BtcPlate.Export(); BtcPlate.SetSpeed(nv); PlateUndoPush(Tt("Plate speed", "Скорость плашки"), b, "pspeed"); }
                }
                y += fs * 0.3f;
            }

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:pl:frame", y), Tt("Frame", "Рамка"), 0))
            {
                PChoice(ref y, w, fs, "fm", null, new[] { Tt("Off", "Выкл"), Tt("Solid", "Сплошная"), Tt("Rainbow", "Радуга"), Tt("Pulse", "Пульс") }, 0);
                int fm = BtcPlate.Get("fm");
                if (fm == 1 || fm == 3) PColor(ref y, w, fs, "fc", Tt("Frame colour", "Цвет рамки"));
                if (fm > 0) PSlider(ref y, w, fs, "fw", Tt("Thickness", "Толщина"), 1, "", 1f);
                y += fs * 0.3f;
            }

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:pl:nick", y), Tt("Nick on the plate", "Ник на плашке"), 0))
            {
                string[] fonts = BtcPlate.FontNames();
                string curFont = BtcPlate.FontName;
                Txt(new Rect(pad, y, w - pad * 2f, fs * 1.5f), Tt("Font", "Шрифт"), _lbl, new Color(1f, 1f, 1f, 0.7f));
                y += fs * 1.6f;
                int per = Mathf.Max(1, (int)((w - pad * 2f) / (fs * 9f)));
                float gap = 4f, cw = (w - pad * 2f - gap * (per - 1)) / per, ch = fs * 2.1f;
                for (int i = 0; i < fonts.Length; i++)
                {
                    Rect r = new Rect(pad + (i % per) * (cw + gap), y + (i / per) * (ch + gap), cw, ch);
                    string nm = i == 0 ? Tt("Default", "Обычный") : fonts[i];
                    bool on = fonts[i] == curFont;
                    if (Chip(r, nm, on, null) && !on) { string b = BtcPlate.Export(); BtcPlate.SetFont(fonts[i]); PlateUndoPush(Tt("Plate font", "Шрифт плашки"), b, null); }
                }
                y += ((fonts.Length + per - 1) / per) * (ch + gap) + fs * 0.2f;
                DrawFontField(ref y, w, fs, curFont);
                PChoice(ref y, w, fs, "fs", Tt("Font style", "Начертание"), new[] { Tt("Default", "Обычный"), Tt("Normal", "Тонкий"), Tt("Bold", "Жирный"), Tt("Italic", "Курсив"), Tt("Bold italic", "Жирный курсив") }, 0);
                PSlider(ref y, w, fs, "nsz", Tt("Nick size", "Размер ника"), 1, "%", 5f);
                DrawAlignGrid(ref y, w, fs);
                PChoice(ref y, w, fs, "nm", Tt("Outline", "Обводка"), new[] { Tt("Off", "Выкл"), Tt("Solid", "Сплошная"), Tt("Rainbow", "Радуга"), Tt("Pulse", "Пульс") }, 0);
                int nm2 = BtcPlate.Get("nm");
                if (nm2 == 1 || nm2 == 3) PColor(ref y, w, fs, "nc", Tt("Outline colour", "Цвет обводки"));
                if (nm2 > 0) PSlider(ref y, w, fs, "nw", Tt("Outline width", "Толщина обводки"), 1, "", 1f);
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Refresh, Tt("Reset nick position", "Сбросить положение ника"), null))
                {
                    string b = BtcPlate.Export();
                    BtcPlate.Set("na", 0); BtcPlate.Set("ox", 0); BtcPlate.Set("oy", 0);
                    PlateUndoPush(Tt("Reset nick position", "Сброс положения ника"), b, null);
                }
                y += fs * 0.3f;
            }

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:pl:fx", y), Tt("Letter effects", "Эффекты букв"), 0))
            {
                string[] pn = { Tt("Off", "Выкл"), Tt("Glitch pack", "Глитч-пак"), Tt("Playful", "Весёлый"), Tt("Random", "Случайно") };
                float gap = 4f, cw = (w - pad * 2f - gap * 3f) / 4f, ch = fs * 2.1f;
                for (int i = 0; i < 4; i++)
                {
                    Rect r = new Rect(pad + i * (cw + gap), y, cw, ch);
                    if (Chip(r, pn[i], false, null))
                    {
                        int nxv = 0;
                        if (i == 1) nxv = 560;
                        else if (i == 2) nxv = 321;
                        else if (i == 3) { int cnt = 2 + UnityEngine.Random.Range(0, 3); for (int j = 0; j < cnt; j++) nxv |= 1 << UnityEngine.Random.Range(0, 12); }
                        PSet("nx", nxv, Tt("Letter effects", "Эффекты букв"), null);
                    }
                }
                y += ch + fs * 0.4f;
                string[] names = BtcPlate.LetterFxNames();
                int nx = BtcPlate.Get("nx");
                int cols = w > fs * 40f ? 3 : 2;
                float cw2 = (w - pad * 2f - gap * (cols - 1)) / cols;
                for (int i = 0; i < names.Length; i++)
                {
                    Rect r = new Rect(pad + (i % cols) * (cw2 + gap), y + (i / cols) * (ch + gap), cw2, ch);
                    bool on = (nx & (1 << i)) != 0;
                    if (Chip(r, names[i], on, null)) PSet("nx", nx ^ (1 << i), Tt("Letter effects", "Эффекты букв"), null);
                }
                y += ((names.Length + cols - 1) / cols) * (ch + gap) + fs * 0.3f;
                PSlider(ref y, w, fs, "nxi", Tt("Strength", "Сила"), 1, "", 1f);
                PSlider(ref y, w, fs, "nxs", Tt("Speed", "Скорость"), 1, "", 1f);
                BtcUi.Note(ref y, w, fs, Tt("Effects move each letter separately. Players with the mod see them in the lobby.", "Эффекты двигают каждую букву отдельно. Игроки с модом видят их в лобби."));
            }

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:pl:size", y), Tt("Size", "Размер"), 0))
            {
                PSlider(ref y, w, fs, "ph", Tt("Plate height", "Высота плашки"), 1, "%", 10f);
                PToggle(ref y, w, fs, "sc", Tt("Scale avatar and nick with the height", "Увеличивать аватарку и ник вместе с высотой"), null);
                PSlider(ref y, w, fs, "asz", Tt("Avatar size", "Размер аватарки"), 1, "%", 5f);
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Refresh, Tt("Reset size and avatar position", "Сбросить размер и положение аватарки"), null))
                {
                    string b = BtcPlate.Export();
                    BtcPlate.Set("ph", 10); BtcPlate.Set("sc", 1); BtcPlate.Set("asz", 20); BtcPlate.Set("ax", 0); BtcPlate.Set("ay", 0);
                    PlateUndoPush(Tt("Reset plate size", "Сброс размера плашки"), b, null);
                }
                y += fs * 0.3f;
            }

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:pl:img", y), Tt("Plate image", "Картинка плашки"), 0))
            {
                string cur = BtcPlateImg.SelectedFile ?? string.Empty;
                string[] files = BtcPlateImg.FilesList;
                float ib = fs * 1.9f, x = pad;
                if (BtcReplayIcons.Plain(new Rect(x, y, ib, ib), BtcReplayIcons.Folder, Color.white, Tt("Open the plate pictures folder: png, jpg or gif", "Открыть папку картинок плашки: png, jpg или gif"))) OpenFolder(BtcPlateImg.FolderPath);
                x += ib + 3f;
                if (BtcReplayIcons.Plain(new Rect(x, y, ib, ib), BtcReplayIcons.Refresh, Color.white, Tt("Rescan the folder", "Перечитать папку"))) { BtcPlateImg.Rescan(); ForgetThumbs(); }
                x += ib + 3f;
                if (cur.Length > 0 && BtcReplayIcons.Plain(new Rect(x, y, ib, ib), BtcReplayIcons.Cross, Color.white, Tt("No image", "Без картинки"))) ChoosePlateImg(string.Empty, cur);
                x += ib + fs;
                Txt(new Rect(x, y, w - x - pad, ib), cur.Length == 0 ? Tt("No image selected", "Картинка не выбрана") : cur, _dim, new Color(1f, 1f, 1f, 0.6f));
                y += ib + fs * 0.4f;
                FileGrid(ref y, w, fs, files, cur, BtcPlateImg.FolderPath, delegate (string f) { ChoosePlateImg(f, cur); });
                PChoice(ref y, w, fs, "im", Tt("Show image", "Показ картинки"), new[] { Tt("Off", "Выкл"), Tt("Fill (crop)", "Заполнить (обрезка)"), Tt("Stretch", "Растянуть") }, 0);
                PSlider(ref y, w, fs, "io", Tt("Opacity", "Непрозрачность"), 1, "%", 5f);
                PSlider(ref y, w, fs, "idm", Tt("Darken", "Затемнение"), 1, "%", 5f);
                string tip = BtcPlate.Measured ? Tt("Your plate is about ", "Ваша плашка примерно ") + Mathf.RoundToInt(BtcPlate.RealW) + "x" + Mathf.RoundToInt(BtcPlate.RealBaseH * BtcPlate.Get("ph") / 10f) + Tt(". Best picture size: 256x", ". Лучший размер картинки: 256x") + Mathf.Clamp(Mathf.RoundToInt(256f / (BtcPlate.RealW / (BtcPlate.RealBaseH * BtcPlate.Get("ph") / 10f))), 16, 128)
                    : Tt("Open the lobby once to measure the plate. ", "Откройте лобби один раз, чтобы измерить плашку. ");
                BtcUi.Note(ref y, w, fs, tip + Tt(". Rule of thumb: 256x64 for 100% height, 256x96 for 200%, 256x128 for 300%. Bigger files are shrunk to 256x128. Up to 32 frames and 255 colours.", ". Ориентир: 256x64 для высоты 100%, 256x96 для 200%, 256x128 для 300%. Большие файлы сжимаются до 256x128. До 32 кадров и 255 цветов."));
            }
        }

        private static void ChoosePlateImg(string file, string before)
        {
            BtcPlateImg.Choose(file);
            if (file.Length > 0 && BtcPlate.Get("im") == 0) BtcPlate.Set("im", 1);
            BtcUndo.Push(Scope, Tt("Plate image", "Картинка плашки"), delegate { BtcPlateImg.Choose(before); });
        }

        private void DrawAlignGrid(ref float y, float w, int fs)
        {
            float pad = fs, cell = fs * 1.9f, gap = 3f;
            Txt(new Rect(pad, y, w - pad * 2f, fs * 1.5f), Tt("Nick position in the plate", "Положение ника в плашке"), _lbl, new Color(1f, 1f, 1f, 0.7f));
            y += fs * 1.6f;
            int na = BtcPlate.Get("na");
            int cur = na == 0 ? 3 : na - 1;
            for (int i = 0; i < 9; i++)
            {
                Rect r = new Rect(pad + (i % 3) * (cell + gap), y + (i / 3) * (cell + gap), cell, cell);
                bool on = i == cur;
                bool hover = r.Contains(Event.current.mousePosition);
                BtcReplayUI.Fill(r, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.35f) : new Color(1f, 1f, 1f, hover ? 0.12f : 0.05f));
                float d = cell * 0.28f;
                BtcReplayUI.Fill(new Rect(r.center.x - d * 0.5f, r.center.y - d * 0.5f, d, d), on ? BtcMenu.Accent : new Color(1f, 1f, 1f, 0.4f));
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn) && !on) PSet("na", i + 1, Tt("Nick position", "Положение ника"), null);
            }
            y += 3 * (cell + gap) + fs * 0.3f;
        }

        private void DrawFontField(ref float y, float w, int fs, string cur)
        {
            float pad = fs, rh = fs * 2.2f;
            Rect row = new Rect(pad, y, w - pad * 2f, rh);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.06f));
            if (_nfEdit == null) _nfEdit = cur;
            Vector2 sp = GUIUtility.GUIToScreenPoint(new Vector2(row.x, row.y));
            BtcUi.Field(new Rect(sp.x, sp.y, row.width, row.height));
            GUI.SetNextControlName("btc_prof_font");
            bool focused = GUI.GetNameOfFocusedControl() == "btc_prof_font";
            Event e = Event.current;
            _nfEdit = GUI.TextField(row, _nfEdit, 32, _tf);
            if (_nfEdit.Length == 0 && !focused) Txt(new Rect(row.x + 8f, row.y, row.width, rh), Tt("Any installed font (exact name), Enter to apply", "Любой установленный шрифт (точное имя), Enter - применить"), _lbl, new Color(1f, 1f, 1f, 0.35f));
            if (focused && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                string b = BtcPlate.Export();
                BtcPlate.SetFont(_nfEdit);
                PlateUndoPush(Tt("Plate font", "Шрифт плашки"), b, null);
                e.Use();
            }
            if (!focused && _nfEdit != cur && GUIUtility.keyboardControl == 0) _nfEdit = cur;
            y += rh + 4f;
        }

        // ------------------------------------------------------------------ shared picture grid (avatar + plate image)
        private Texture2D Thumb(string dir, string file)
        {
            string key = dir + "|" + file;
            Texture2D t;
            if (_thumbs.TryGetValue(key, out t)) return t;
            if (_noThumb.Contains(key) || Time.frameCount == _thumbFrame) return null;
            _thumbFrame = Time.frameCount;
            try
            {
                string path = Path.Combine(dir, file);
                string ext = Path.GetExtension(path).ToLowerInvariant();
                FileInfo fi = new FileInfo(path);
                if (ext == ".gif" || !fi.Exists || fi.Length > ThumbMaxBytes) { _noThumb.Add(key); return null; }
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(path)) || tex.width * tex.height > 1500000) { UnityEngine.Object.Destroy(tex); _noThumb.Add(key); return null; }
                tex.filterMode = FilterMode.Bilinear;
                _thumbs[key] = tex;
                return tex;
            }
            catch (Exception) { _noThumb.Add(key); return null; }
        }

        private void FileGrid(ref float y, float w, int fs, string[] files, string cur, string dir, Action<string> choose)
        {
            float pad = fs;
            if (files.Length == 0)
            {
                BtcUi.Note(ref y, w, fs, Tt("The folder is empty. Drop a picture into it and press rescan.", "Папка пуста. Положите в неё картинку и нажмите «перечитать»."));
                return;
            }
            float cw = fs * 7.5f, ch = fs * 9f, gap = 6f;
            int per = Mathf.Max(1, (int)((w - pad * 2f + gap) / (cw + gap)));
            for (int i = 0; i < files.Length; i++)
            {
                Rect r = new Rect(pad + (i % per) * (cw + gap), y + (i / per) * (ch + gap), cw, ch);
                bool on = files[i] == cur;
                bool hover = r.Contains(Event.current.mousePosition);
                BtcReplayUI.Fill(r, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.2f) : new Color(1f, 1f, 1f, hover ? 0.09f : 0.04f));
                if (on) BtcReplayUI.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), BtcMenu.Accent);
                Rect ir = new Rect(r.x + fs * 0.5f, r.y + fs * 0.5f, cw - fs, cw - fs);
                BtcReplayUI.Fill(ir, new Color(0f, 0f, 0f, 0.35f));
                Texture2D t = Thumb(dir, files[i]);
                if (t != null) GUI.DrawTexture(new Rect(ir.x + 1f, ir.y + 1f, ir.width - 2f, ir.height - 2f), t, ScaleMode.ScaleToFit, true);
                else BtcReplayIcons.Glyph(Path.GetExtension(files[i]).ToLowerInvariant() == ".gif" ? BtcReplayIcons.Gif : BtcReplayIcons.Image, new Rect(ir.x + ir.width * 0.25f, ir.y + ir.height * 0.25f, ir.width * 0.5f, ir.height * 0.5f), new Color(1f, 1f, 1f, 0.3f));
                Txt(new Rect(r.x + 3f, ir.yMax, r.width - 6f, r.yMax - ir.yMax), Path.GetFileNameWithoutExtension(files[i]), _center, on ? Color.white : new Color(1f, 1f, 1f, 0.8f));
                BtcReplayIcons.Hint(r, files[i]);
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn) && !on) choose(files[i]);
            }
            y += ((files.Length + per - 1) / per) * (ch + gap) + fs * 0.3f;
        }
    }
}
