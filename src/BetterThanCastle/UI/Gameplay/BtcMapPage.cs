using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Map page of the Gameplay tab: the map card with its preset (btc-map.txt in the map folder, applied by itself when the host selects the map),
    /// map symmetry (mode icons, preview where the master half is clicked and the AXIS LINES ARE DRAGGED with the mouse) and map scale (the useless
    /// legacy density and ore scaling options are gone from the UI; the saved values stay as they were). The logic and the wire stay in
    /// BtcSymmetry / BtcMapScale (msgs 89, 97); this page only edits their state through accessors. Undo scope "gameplay".
    /// </summary>
    internal sealed partial class BtcGameplayTab
    {
        private int _symDrag;               // 0 none, 1 vertical axis (X shift), 2 horizontal axis (Z shift)
        private string _symDragOld;
        private float _symCommitAt;
        private bool _symDirty;
        private bool _mapDelConfirm;
        private float _mapDelUntil;
        private string _seedText;

        private void PageMap(ref float y, float w, int fs, bool host, bool session)
        {
            BtcSymmetry.UiPrepare();
            BtcMapScale.UiPrepare();
            bool can = BtcGameplay.CanEdit && BtcBc.BcEnabled;
            float pad = fs, h = fs * 2.3f;

            // ---- map card
            string mapName = BtcMapPreset.MapName;
            bool has = BtcMapPreset.Has();
            Rect card = new Rect(pad, y, w - pad * 2f, h * 1.3f);
            BtcReplayUI.Fill(card, new Color(1f, 1f, 1f, 0.04f));
            BtcReplayUI.Fill(new Rect(card.x, card.y + card.height * 0.12f, 3f, card.height * 0.76f), has ? BtcMenu.Accent : new Color(1f, 1f, 1f, 0.25f));
            float ig = fs * 1.6f;
            BtcReplayIcons.Glyph(BtcReplayIcons.Map, new Rect(card.x + fs * 0.7f, card.center.y - ig * 0.5f, ig, ig), has ? BtcMenu.Accent : new Color(1f, 1f, 1f, 0.5f));
            string title = mapName ?? Tt("No map selected", "Карта не выбрана");
            BtcVisualRows.Txt(new Rect(card.x + fs * 2.9f, card.y, card.width - fs * 12f, card.height * 0.55f), title, _ttl, Color.white);
            BtcVisualRows.Txt(new Rect(card.x + fs * 2.9f, card.y + card.height * 0.5f, card.width - fs * 12f, card.height * 0.45f),
                has ? Tt("This map has a preset (btc-map.txt in its folder)", "У этой карты есть пресет (btc-map.txt в её папке)") : Tt("No preset: the settings below stay as they are", "Пресета нет: настройки ниже остаются как есть"), BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.55f));
            float bs = fs * 2.1f, bx = card.xMax - fs * 0.5f - bs;
            if (mapName != null)
            {
                if (has)
                {
                    bool conf = _mapDelConfirm && Time.unscaledTime < _mapDelUntil;
                    if (IcoBtn(new Rect(bx, card.center.y - bs * 0.5f, bs, bs), BtcReplayIcons.Trash, conf, conf ? Critical : BtcMenu.Accent, conf ? Tt("Click again: delete the preset file of this map", "Ещё раз: удалить файл пресета этой карты") : Tt("Delete the preset of this map", "Удалить пресет этой карты")))
                    {
                        if (conf) { BtcMapPreset.Delete(); BtcSfx.Delete(); _mapDelConfirm = false; }
                        else { _mapDelConfirm = true; _mapDelUntil = Time.unscaledTime + 3f; }
                    }
                    bx -= bs + 4f;
                    if (IcoBtn(new Rect(bx, card.center.y - bs * 0.5f, bs, bs), BtcReplayIcons.Check, false, BtcMenu.Accent, Tt("Apply the preset of this map now", "Применить пресет этой карты сейчас")))
                    {
                        if (can) { BtcMapPreset.Apply(true); BtcSfx.Ok(); }
                        else { BtcSfx.Warn(); BtcMenu.Toast(Tt("Only the host can change this in the lobby", "Менять может только хост в лобби"), BtcReplayIcons.Lock); }
                    }
                    bx -= bs + 4f;
                }
                if (IcoBtn(new Rect(bx, card.center.y - bs * 0.5f, bs, bs), BtcReplayIcons.Export, false, BtcMenu.Accent, has ? Tt("Save the current symmetry and scale into this map's preset", "Сохранить текущие симметрию и скейл в пресет этой карты") : Tt("Save the current symmetry and scale as the preset of this map", "Сохранить текущие симметрию и скейл как пресет этой карты")))
                {
                    if (BtcMapPreset.Save()) { BtcSfx.Ok(); BtcMenu.Toast(Tt("Map preset saved", "Пресет карты сохранён"), BtcReplayIcons.Check); }
                    else { BtcSfx.Warn(); BtcMenu.Toast(Tt("Could not write the preset file", "Не удалось записать файл пресета"), BtcReplayIcons.Cross); }
                }
            }
            y += card.height + fs * 0.3f;
            bool au = BtcMapPreset.Auto;
            if (BtcVisualRows.Switch(ref y, w, fs, 0, Tt("Apply the map's preset by itself when the host selects the map", "Применять пресет карты сам, когда хост выбирает карту"), au, null))
            {
                BtcMapPreset.Auto = !au;
                BtcUndo.Push(Scope, Tt("Auto map preset", "Автопресет карты"), delegate { BtcMapPreset.Auto = au; });
            }
            if (!BtcBc.BcEnabled) BtcUi.Note(ref y, w, fs, Tt("BetterConnection is off: map symmetry and scale are not applied.", "BetterConnection выключен: симметрия и скейл карты не применяются."));
            else if (!can) BtcUi.Note(ref y, w, fs, Tt("Only the host can change these settings in the lobby.", "Менять эти настройки в лобби может только хост."));
            y += fs * 0.3f;

            // ---- symmetry
            string kSym = "gp:map:sym";
            _keys.Add(kSym);
            if (BtcVisualRows.Fold(ref y, w, fs, kSym, BtcReplayIcons.Layout, Tt("Map symmetry", "Симметрия карты"), SymSummary()))
            {
                SymSection(ref y, w, fs, can);
                y += fs * 0.3f;
            }

            // ---- scale
            string kSc = "gp:map:scale";
            _keys.Add(kSc);
            if (BtcVisualRows.Fold(ref y, w, fs, kSc, BtcReplayIcons.Grid, Tt("Map scale", "Масштаб карты"), BtcMapScale.Pct == 100 ? "" : (BtcMapScale.Pct / 100f).ToString("0.##", CultureInfo.InvariantCulture) + "x"))
            {
                ScaleSection(ref y, w, fs, can);
                y += fs * 0.3f;
            }
        }

        private static string SymSummary()
        {
            string[] n = { Tt("off", "выкл"), Tt("mirror |", "зеркало |"), Tt("mirror -", "зеркало -"), Tt("rotate 180", "поворот 180"), Tt("4-fold", "4-кратная") };
            return n[Mathf.Clamp(BtcSymmetry.Mode, 0, 4)];
        }

        private void SymEdit(string oldState, string key)
        {
            BtcSymmetry.Commit();
            string os = oldState;
            BtcUndo.Push(Scope, Tt("Map symmetry", "Симметрия карты"), delegate { BtcSymmetry.ApplyState(os); }, key);
        }

        private void SymSection(ref float y, float w, int fs, bool can)
        {
            Event e = Event.current;
            int[] icons = { BtcReplayIcons.Cross, BtcReplayIcons.Lines, BtcReplayIcons.List, BtcReplayIcons.Refresh, BtcReplayIcons.Grid };
            string[] tips = { Tt("Off", "Выкл"), Tt("Mirror along a vertical axis", "Зеркало по вертикальной оси"), Tt("Mirror along a horizontal axis", "Зеркало по горизонтальной оси"), Tt("Rotate 180 degrees", "Поворот на 180 градусов"), Tt("4-fold: mirror on both axes", "4-кратная: зеркало по обеим осям") };
            int mode = BtcSymmetry.Mode;
            int ci = BtcVisualRows.Choice(ref y, w, fs, 0, Tt("Mode", "Режим"), mode, null, icons, tips);
            if (ci >= 0 && ci != mode && can)
            {
                string os = BtcSymmetry.StateString();
                BtcSymmetry.Mode = ci;
                if (ci < 3) BtcSymmetry.Keep = BtcSymmetry.Keep & 1;
                SymEdit(os, null);
                mode = ci;
            }
            if (mode == 0)
            {
                BtcUi.Note(ref y, w, fs, Tt("The master half of the map is copied onto the other half before the game starts (terrain, trees, plants, rocks, iron, brimstone). The original map file is never changed. Every player needs this mod version.", "Выбранная половина карты копируется на другую перед стартом игры (рельеф, деревья, растения, камни, железо, бримстоун). Исходный файл карты не меняется. Нужна эта версия мода у всех игроков."));
                return;
            }

            // ---- preview with draggable axes
            Texture2D tex; int minX, minZ, pw, ph, defSx, defSz; List<float[]> marks;
            if (!BtcSymmetry.PreviewData(out tex, out minX, out minZ, out pw, out ph, out defSx, out defSz, out marks))
            {
                BtcUi.Note(ref y, w, fs, Tt("No map selected or the map has no heightmap: preview unavailable.", "Карта не выбрана или у неё нет heightmap: превью недоступно."));
            }
            else
            {
                float pad = fs;
                float maxW = w - pad * 2f, maxH = fs * 22f;
                float sc = Mathf.Min(maxW / pw, maxH / ph);
                Rect pos = new Rect(pad, y, pw * sc, ph * sc);
                int sx = defSx + BtcSymmetry.Dx, sz = defSz + BtcSymmetry.Dz;
                float ax = Mathf.Clamp(pos.x + (sx * 0.5f - minX + 0.5f) / pw * pos.width, pos.x, pos.xMax);
                float ay = Mathf.Clamp(pos.yMax - (sz * 0.5f - minZ + 0.5f) / ph * pos.height, pos.y, pos.yMax);
                bool vAxis = mode == 1 || mode == 3 || mode == 4, hAxis = mode == 2 || mode == 3 || mode == 4;
                float grab = Mathf.Max(8f, fs * 0.6f);
                bool nearV = vAxis && Mathf.Abs(e.mousePosition.x - ax) <= grab && e.mousePosition.y >= pos.y && e.mousePosition.y <= pos.yMax;
                bool nearH = hAxis && Mathf.Abs(e.mousePosition.y - ay) <= grab && e.mousePosition.x >= pos.x && e.mousePosition.x <= pos.xMax;

                if (e.rawType == EventType.MouseUp && _symDrag != 0)
                {
                    _symDrag = 0;
                    if (_symDirty) { _symDirty = false; SymEdit(_symDragOld, null); }
                }
                if (can && e.type == EventType.MouseDown && e.button == 0 && pos.Contains(e.mousePosition) && !BtcHotkeys.UiClickSwallowed)
                {
                    if (nearV || nearH)
                    {
                        _symDrag = (nearV && (!nearH || Mathf.Abs(e.mousePosition.x - ax) <= Mathf.Abs(e.mousePosition.y - ay))) ? 1 : 2;
                        _symDragOld = BtcSymmetry.StateString();
                        _symDirty = false;
                    }
                    else
                    {
                        int keep = PickKeep(mode, e.mousePosition, pos, ax, ay);
                        if (keep != BtcSymmetry.Keep) { string os = BtcSymmetry.StateString(); BtcSymmetry.Keep = keep; SymEdit(os, null); }
                    }
                    e.Use();
                }
                if (can && _symDrag != 0 && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown))
                {
                    if (_symDrag == 1)
                    {
                        float nx = (e.mousePosition.x - pos.x) / pos.width * pw + minX - 0.5f;
                        int ndx = Mathf.Clamp(Mathf.RoundToInt(nx * 2f) - defSx, -400, 400);
                        if (ndx != BtcSymmetry.Dx) { BtcSymmetry.Dx = ndx; _symDirty = true; }
                    }
                    else
                    {
                        float nz = (pos.yMax - e.mousePosition.y) / pos.height * ph + minZ - 0.5f;
                        int ndz = Mathf.Clamp(Mathf.RoundToInt(nz * 2f) - defSz, -400, 400);
                        if (ndz != BtcSymmetry.Dz) { BtcSymmetry.Dz = ndz; _symDirty = true; }
                    }
                    if (_symDirty && Time.unscaledTime > _symCommitAt) { _symCommitAt = Time.unscaledTime + 0.2f; BtcSymmetry.Commit(); }
                    e.Use();
                }
                if (e.type == EventType.Repaint)
                {
                    BtcReplayUI.Fill(pos, new Color(0.08f, 0.08f, 0.08f, 1f));
                    Texture2D atex = null; List<float[]> amarks = null;
                    bool altPv = e.alt && BtcSymmetry.AltPreview(mode, sx, sz, out atex, out amarks);
                    if (!altPv) { atex = null; amarks = null; }
                    GUI.DrawTexture(pos, altPv ? atex : tex, ScaleMode.StretchToFill, true);
                    if (!altPv) SymDim(mode, BtcSymmetry.Keep, pos, ax, ay);
                    List<float[]> shownMarks = altPv ? amarks : marks;
                    for (int j = 0; j < shownMarks.Count; j++)
                    {
                        float[] m = shownMarks[j];
                        float mx = pos.x + (m[0] - minX + 0.5f) / pw * pos.width, my = pos.yMax - (m[1] - minZ + 0.5f) / ph * pos.height;
                        Color c = m[2] == 1f ? new Color(1f, 0.9f, 0.2f, 1f) : (m[2] == 2f ? new Color(0.3f, 0.9f, 1f, 1f) : (m[2] == 3f ? new Color(1f, 0.3f, 0.3f, 1f) : new Color(1f, 1f, 1f, 0.7f)));
                        float s = m[2] == 1f ? 6f : 4f;
                        BtcReplayUI.Fill(new Rect(mx - s * 0.5f, my - s * 0.5f, s, s), c);
                    }
                    Color lc = BtcMenu.Accent;
                    if (vAxis) { bool on = nearV || _symDrag == 1; BtcReplayUI.Fill(new Rect(ax - (on ? 1.5f : 1f), pos.y, on ? 3f : 2f, pos.height), on ? Color.white : lc); BtcReplayUI.Fill(new Rect(ax - fs * 0.4f, pos.center.y - fs * 0.9f, fs * 0.8f, fs * 1.8f), on ? Color.white : lc); }
                    if (hAxis) { bool on = nearH || _symDrag == 2; BtcReplayUI.Fill(new Rect(pos.x, ay - (on ? 1.5f : 1f), pos.width, on ? 3f : 2f), on ? Color.white : lc); BtcReplayUI.Fill(new Rect(pos.center.x - fs * 0.9f, ay - fs * 0.4f, fs * 1.8f, fs * 0.8f), on ? Color.white : lc); }
                }
                BtcReplayIcons.Hint(pos, new Rect(_off.x + pos.x, _off.y + pos.y, pos.width, pos.height), Tt("Click: choose the master half (bright = copied, dark = replaced). Drag a green axis line to move the axis. Yellow dots = starts, cyan = crystals, red = corruptron spawns. Hold Alt: preview the map after the symmetry.", "Клик: выбрать образец (яркая часть копируется, тёмная заменяется). Потяните зелёную линию оси, чтобы сдвинуть ось. Жёлтые точки: старты, голубые: кристаллы, красные: спавны коррупторов. Удерживайте Alt: превью карты после симметрии."));
                y += pos.height + fs * 0.4f;
                BtcVisualRows.Txt(new Rect(pad, y, w - pad * 2f, fs * 1.6f), Tt("Master: ", "Образец: ") + BtcSymmetry.KeepLabel(mode, BtcSymmetry.Keep), BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.6f));
                y += fs * 1.8f;
            }

            // ---- axis shift: sliders with +/- (the mouse drag above is the fast way)
            if (mode == 1 || mode == 3 || mode == 4)
            {
                int cur = BtcSymmetry.Dx;
                float nv = BtcVisualRows.Slider(ref y, w, fs, 0, "gp:map:dx", Tt("Axis shift X (half-voxels)", "Сдвиг оси X (полвокселя)"), cur, -400f, 400f, 1f, "+0;-0;0", "");
                if (can && Mathf.RoundToInt(nv) != cur) { string os = BtcSymmetry.StateString(); BtcSymmetry.Dx = Mathf.RoundToInt(nv); SymEdit(os, "gp:map:dx"); }
            }
            if (mode == 2 || mode == 3 || mode == 4)
            {
                int cur = BtcSymmetry.Dz;
                float nv = BtcVisualRows.Slider(ref y, w, fs, 0, "gp:map:dz", Tt("Axis shift Z (half-voxels)", "Сдвиг оси Z (полвокселя)"), cur, -400f, 400f, 1f, "+0;-0;0", "");
                if (can && Mathf.RoundToInt(nv) != cur) { string os = BtcSymmetry.StateString(); BtcSymmetry.Dz = Mathf.RoundToInt(nv); SymEdit(os, "gp:map:dz"); }
            }
            if ((BtcSymmetry.Dx != 0 || BtcSymmetry.Dz != 0) && can)
            {
                float rs = fs * 2.1f;
                BtcVisualRows.Txt(new Rect(fs + rs + fs * 0.6f, y, w - fs * 5f, rs), Tt("Axis back to the centre", "Ось в центр"), BtcVisualRows.Lbl, new Color(1f, 1f, 1f, 0.8f));
                if (IcoBtn(new Rect(fs, y, rs, rs), BtcReplayIcons.Undo, false, BtcMenu.Accent, Tt("Reset the axis shift", "Сбросить сдвиг оси")))
                {
                    string os = BtcSymmetry.StateString();
                    BtcSymmetry.Dx = 0; BtcSymmetry.Dz = 0;
                    SymEdit(os, null);
                }
                y += rs + 4f;
            }

            // ---- what else is mirrored
            if (BtcVisualRows.Switch(ref y, w, fs, 0, Tt("Also mirror editor markers (starts, crystals) and placed blocks", "Зеркалить также маркеры редактора (старты, кристаллы) и поставленные блоки"), BtcSymmetry.Obj, null) && can)
            {
                string os = BtcSymmetry.StateString(); BtcSymmetry.Obj = !BtcSymmetry.Obj; SymEdit(os, null);
            }
            string[] gn = { Tt("Mirror loose resources", "Зеркалить россыпные ресурсы"), Tt("Mirror buildings", "Зеркалить здания"), Tt("Mirror units", "Зеркалить юнитов") };
            for (int k = 0; k < 3; k++)
            {
                bool on = (BtcSymmetry.Go & (1 << k)) != 0;
                if (BtcVisualRows.Switch(ref y, w, fs, 0, gn[k], on, null) && can)
                {
                    string os = BtcSymmetry.StateString(); BtcSymmetry.Go = BtcSymmetry.Go ^ (1 << k); SymEdit(os, null);
                }
            }
            // ore seed
            float h = fs * 2.1f, pad2 = fs;
            Rect row = new Rect(pad2, y, w - pad2 * 2f, h);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.03f));
            BtcVisualRows.Txt(new Rect(row.x + fs * 0.6f, row.y, row.width * 0.5f, h), Tt("Ore seed (0 = random)", "Сид руды (0 = случайный)"), BtcVisualRows.Lbl, new Color(1f, 1f, 1f, 0.85f));
            Rect dice = new Rect(row.xMax - fs * 0.4f - h * 0.8f, row.y + h * 0.1f, h * 0.8f, h * 0.8f);
            Rect fr = new Rect(dice.x - fs * 7f, row.y + h * 0.1f, fs * 6.5f, h * 0.8f);
            BtcReplayUI.Fill(fr, new Color(1f, 1f, 1f, 0.07f));
            BtcUi.Field(fr);
            if (_seedText == null || (GUIUtility.keyboardControl == 0 && _seedText != BtcSymmetry.Seed.ToString())) _seedText = BtcSymmetry.Seed.ToString();
            string st = GUI.TextField(new Rect(fr.x + 4f, fr.y, fr.width - 8f, fr.height), _seedText, 6, _tf);
            if (st != _seedText)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < st.Length; i++) if (st[i] >= '0' && st[i] <= '9') sb.Append(st[i]);
                _seedText = sb.ToString();
                int ns;
                if (!int.TryParse(_seedText, out ns)) ns = 0;
                if (can && ns != BtcSymmetry.Seed) { string os = BtcSymmetry.StateString(); BtcSymmetry.Seed = ns; SymEdit(os, "gp:map:seed"); }
            }
            if (IcoBtn(dice, BtcReplayIcons.Refresh, false, BtcMenu.Accent, Tt("Random ore seed", "Случайный сид руды")) && can)
            {
                string os = BtcSymmetry.StateString();
                BtcSymmetry.Seed = Mathf.Abs(Environment.TickCount % 999999) + 1;
                _seedText = BtcSymmetry.Seed.ToString();
                SymEdit(os, null);
            }
            y += h + 4f;
            if (BtcSymmetry.Seed > 0 && !BtcSymmetry.HasNoise) BtcUi.Note(ref y, w, fs, Tt("This map has no noise.json: the seed cannot be forced, ore stays random.", "У этой карты нет noise.json: сид задать нельзя, руда останется случайной."));
        }

        /// <summary>Master half from a click on the preview (same rules as the old page).</summary>
        private static int PickKeep(int mode, Vector2 m, Rect pos, float ax, float ay)
        {
            if (mode == 1) return m.x > ax ? 1 : 0;
            if (mode == 2) return m.y < ay ? 1 : 0;
            if (mode == 3)
            {
                float dx = (m.x - ax) / (pos.width * 0.5f), dz = (ay - m.y) / (pos.height * 0.5f);
                return Mathf.Abs(dx) >= Mathf.Abs(dz) ? (dx > 0f ? 1 : 0) : 2 + (dz > 0f ? 1 : 0);
            }
            return (m.x > ax ? 1 : 0) | (m.y < ay ? 2 : 0);
        }

        private static void SymDim(int mode, int keep, Rect pos, float ax, float ay)
        {
            Color d = new Color(0f, 0f, 0f, 0.55f);
            if (mode == 1 || (mode == 3 && keep < 2))
            {
                if ((keep & 1) == 0) BtcReplayUI.Fill(new Rect(ax, pos.y, pos.xMax - ax, pos.height), d);
                else BtcReplayUI.Fill(new Rect(pos.x, pos.y, ax - pos.x, pos.height), d);
            }
            else if (mode == 2 || mode == 3)
            {
                if ((keep & 1) == 0) BtcReplayUI.Fill(new Rect(pos.x, pos.y, pos.width, ay - pos.y), d);
                else BtcReplayUI.Fill(new Rect(pos.x, ay, pos.width, pos.yMax - ay), d);
            }
            else
            {
                bool kx = (keep & 1) == 1, kz = (keep >> 1) == 1;
                if (kx) BtcReplayUI.Fill(new Rect(pos.x, pos.y, ax - pos.x, pos.height), d);
                else BtcReplayUI.Fill(new Rect(ax, pos.y, pos.xMax - ax, pos.height), d);
                float x0 = kx ? ax : pos.x, x1 = kx ? pos.xMax : ax;
                if (kz) BtcReplayUI.Fill(new Rect(x0, ay, x1 - x0, pos.yMax - ay), d);
                else BtcReplayUI.Fill(new Rect(x0, pos.y, x1 - x0, ay - pos.y), d);
            }
        }

        private void ScaleSection(ref float y, float w, int fs, bool can)
        {
            int pct = BtcMapScale.Pct;
            int[] q = { 50, 100, 200, 300, 400, 500 };
            string[] qt = new string[q.Length];
            int sel = -1;
            for (int i = 0; i < q.Length; i++) { qt[i] = (q[i] % 100 == 0 ? (q[i] / 100).ToString() : (q[i] / 100f).ToString("0.0", CultureInfo.InvariantCulture)) + "x"; if (q[i] == pct) sel = i; }
            int ci = BtcVisualRows.Choice(ref y, w, fs, 0, Tt("Quick scale", "Быстрый масштаб"), sel, qt, null, null);
            if (ci >= 0 && can && q[ci] != pct) ScaleSet(q[ci]);
            float nv = BtcVisualRows.Slider(ref y, w, fs, 0, "gp:map:pct", Tt("Scale, %", "Масштаб, %"), Mathf.Clamp(pct, 25, 1000), 25f, 1000f, 25f, "0", "%");
            int np = Mathf.RoundToInt(nv);
            if (can && np != Mathf.Clamp(pct, 25, 1000)) ScaleSet(np);
            if (pct != 100)
            {
                float f = pct / 100f;
                BtcUi.Note(ref y, w, fs, Tt("Length and width x", "Длина и ширина x") + f.ToString("0.##", CultureInfo.InvariantCulture) + Tt(", area x", ", площадь x") + (f * f).ToString("0.##", CultureInfo.InvariantCulture) + Tt(". Trees, plants, ore and rocks keep the same density; terrain height is not scaled, so slopes get gentler. The map is rebuilt as a cached copy before the game starts.", ". Деревья, растения, руда и камни сохраняют плотность; высота рельефа не масштабируется, поэтому склоны пологие. Карта пересобирается кэшированной копией перед стартом."));
            }
            if (pct >= 300) BtcUi.Status(ref y, w, fs, Tt("Large scale: much more memory and a longer map build; the vanilla world size limit may apply", "Большой масштаб: много памяти и долгая сборка; может сработать ванильный лимит размера мира"), Minor);
            if (!string.IsNullOrEmpty(BtcMapScale.LastBuild)) BtcUi.Note(ref y, w, fs, Tt("Last build: ", "Последняя сборка: ") + BtcMapScale.LastBuild);
            float h = fs * 2.1f, pad = fs;
            Rect row = new Rect(pad, y, w - pad * 2f, h);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.03f));
            BtcVisualRows.Txt(new Rect(row.x + fs * 0.6f, row.y, row.width * 0.6f, h), Tt("Scatter seed: ", "Сид разброса: ") + BtcMapScale.ScatterSeed, BtcVisualRows.Lbl, new Color(1f, 1f, 1f, 0.85f));
            Rect dice = new Rect(row.xMax - fs * 0.4f - h * 0.8f, row.y + h * 0.1f, h * 0.8f, h * 0.8f);
            if (IcoBtn(dice, BtcReplayIcons.Refresh, false, BtcMenu.Accent, Tt("Random scatter seed (how copies of trees and ore are spread)", "Случайный сид разброса (как раскидываются копии деревьев и руды)")) && can)
            {
                string os = BtcMapScale.StateString();
                BtcMapScale.ScatterSeed = Mathf.Abs(Environment.TickCount % 999999);
                BtcMapScale.Commit();
                BtcUndo.Push(Scope, Tt("Scatter seed", "Сид разброса"), delegate { BtcMapScale.ApplyState(os); });
            }
            y += h + 4f;
        }

        private void ScaleSet(int pct)
        {
            string os = BtcMapScale.StateString();
            BtcMapScale.Pct = pct;
            BtcMapScale.Commit();
            BtcUndo.Push(Scope, Tt("Map scale", "Масштаб карты"), delegate { BtcMapScale.ApplyState(os); }, "gp:map:pct");
        }
    }
}
