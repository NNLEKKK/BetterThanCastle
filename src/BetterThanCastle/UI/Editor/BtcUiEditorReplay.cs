using System;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay page of the UI editor (F7 in a replay opens it): a preview of the replay screen where the pressed-keys module is dragged and resized,
    /// and every look setting of the replay UI (control bar size, world marks, cursor, labels, view cones, APM graph, pressed keys mode).
    /// Data: BtcReplayLayout (the old F7 window is gone), modes in BtcConfig.
    /// </summary>
    internal sealed partial class BtcUiEditorTab
    {
        private bool _rDrag;
        private int _rMode;                      // 1 move, 2 resize
        private Vector2 _rStart;
        private float _rOx, _rOy, _rScale;
        private string _rBefore;
        private int _rKeys, _rApm;
        private Vector2 _rScroll;
        private float _rH = 100f;

        private static void REdit(string label, string mergeKey, Action change)
        {
            string before = BtcReplayLayout.Serialize();
            int km = BtcConfig.ReplayKeysMode.Value, am = BtcConfig.ReplayApmMode.Value;
            change();
            BtcReplayLayout.Commit();
            BtcUndo.Push(Scope, label, delegate { BtcReplayLayout.Restore(before); BtcConfig.ReplayKeysMode.Value = km; BtcConfig.ReplayApmMode.Value = am; }, mergeKey);
        }

        private static float RSlider(ref float y, float w, int fs, string id, string label, float v, float min, float max, float step, string fmt, string suf, Action<float> set)
        {
            float nv = BtcVisualRows.Slider(ref y, w, fs, 0, id, label, v, min, max, step, fmt, suf);
            if (Mathf.Abs(nv - v) > 0.0001f) REdit(label, "uier:" + id, delegate { set(nv); });
            return nv;
        }

        private static bool RToggle(ref float y, float w, int fs, string label, bool v, Action<bool> set)
        {
            if (BtcUi.Toggle(ref y, w, fs, label, v, null)) { bool nv = !v; REdit(label, null, delegate { set(nv); }); return true; }
            return false;
        }

        /// <summary>Rectangle of the pressed-keys module on the screen (what BtcReplayInput.Draw + BtcReplayLayout.Begin produce), in screen pixels.</summary>
        private static Rect KeysRect(int fsR, float barH, bool transformed)
        {
            int mode = BtcConfig.ReplayKeysMode.Value;
            if (mode == 0) mode = 1;
            float c = fsR * 1.25f, g = fsR * 0.12f, u = c + g, pad = fsR * 0.4f;
            float w = mode == 1 ? fsR * 6.5f : 15f * u - g + pad * 2f;
            float h = mode == 1 ? fsR * 1.45f * 6f : 5f * u - g + pad * 2f;
            Rect b = new Rect(Screen.width - w - fsR, Screen.height - barH - 4f - h, w, h);
            if (!transformed) return b;
            float s, ox, oy; bool vis;
            BtcReplayLayout.GetMod(BtcReplayLayout.Keys, out s, out ox, out oy, out vis);
            Vector2 pv = b.center, off = new Vector2(ox * Screen.width, oy * Screen.height);
            return new Rect(pv.x + off.x + (b.x - pv.x) * s, pv.y + off.y + (b.y - pv.y) * s, b.width * s, b.height * s);
        }

        private void DrawReplay(Rect body, int fs)
        {
            Event e = Event.current;
            float pad = fs, gap = fs;
            float propsW = Mathf.Clamp(body.width * 0.34f, fs * 17f, fs * 25f);
            Rect avail = new Rect(body.x + pad, body.y + fs * 0.2f, body.width - propsW - pad * 2f - gap, body.height - fs * 1.2f);
            float aspect = (float)Screen.width / Screen.height;
            float cw = avail.width, ch = cw / aspect;
            if (ch > avail.height - fs * 2.2f) { ch = avail.height - fs * 2.2f; cw = ch * aspect; }
            Rect canvas = new Rect(avail.x, avail.y + fs * 2.2f, cw, ch);
            float k = canvas.width / Screen.width, W = Screen.width, H = Screen.height;
            int fsR = Mathf.Max(14, Screen.height / 38);
            BtcVisualRows.Txt(new Rect(avail.x, avail.y, avail.width, fs * 2f), Tt("Preview of a replay screen. Drag the pressed-keys block, the corner handle resizes it. The control bar always spans the screen bottom.", "Превью экрана реплея. Тащите блок нажатых клавиш, угловой маркер меняет размер. Панель управления всегда на всю ширину внизу."), _tiny, new Color(1f, 1f, 1f, 0.55f));

            BtcReplayUI.Fill(canvas, new Color(0.012f, 0.016f, 0.02f, 1f));
            BtcReplayUI.Fill(new Rect(canvas.x, canvas.y, canvas.width, 1f), BtcMenu.Line);
            BtcReplayUI.Fill(new Rect(canvas.x, canvas.yMax - 1f, canvas.width, 1f), BtcMenu.Line);
            BtcReplayUI.Fill(new Rect(canvas.x, canvas.y, 1f, canvas.height), BtcMenu.Line);
            BtcReplayUI.Fill(new Rect(canvas.xMax - 1f, canvas.y, 1f, canvas.height), BtcMenu.Line);

            // control bar (fixed)
            float hs = BtcReplayLayout.BarScale;
            bool apmOn = BtcConfig.ReplayApmMode.Value != 0;
            float apmH = apmOn ? fsR * 1.7f * hs * BtcReplayLayout.ApmHeight : 0f;
            float barH = fsR * 4.5f * hs + apmH;
            Rect bar = new Rect(canvas.x, canvas.yMax - barH * k, canvas.width, barH * k);
            BtcReplayUI.Fill(bar, new Color(0f, 0f, 0f, 0.7f));
            if (apmOn)
            {
                Rect ap = new Rect(bar.x + 8f, bar.y + 2f, bar.width - 16f, apmH * k - 4f);
                BtcReplayUI.Fill(ap, BtcMenu.AccentA(BtcReplayLayout.ApmFill + 0.05f));
                for (int i = 0; i < 24; i++)
                {
                    float f = 0.25f + 0.6f * Mathf.Abs(Mathf.Sin(i * 0.9f));
                    BtcReplayUI.Fill(new Rect(ap.x + ap.width * i / 24f, ap.yMax - ap.height * f, Mathf.Max(1f, BtcReplayLayout.ApmWidth * k * 1.2f), ap.height * f), BtcMenu.AccentA(BtcReplayLayout.ApmAlpha * 0.8f));
                }
            }
            Rect tl = new Rect(bar.x + 8f, bar.y + apmH * k + (bar.height - apmH * k) * 0.55f, bar.width - 16f, Mathf.Max(2f, 4f * k * fsR * 0.2f));
            BtcReplayUI.Fill(tl, new Color(1f, 1f, 1f, 0.25f));
            BtcReplayUI.Fill(new Rect(tl.x, tl.y, tl.width * 0.37f, tl.height), BtcMenu.Accent);
            BtcVisualRows.Txt(new Rect(bar.x + 10f, bar.y + apmH * k, bar.width * 0.5f, Mathf.Min(bar.height - apmH * k, fs * 1.6f)), Tt("Control panel", "Панель управления"), _tiny, new Color(1f, 1f, 1f, 0.6f));

            // pressed keys module
            float s, ox, oy; bool vis;
            BtcReplayLayout.GetMod(BtcReplayLayout.Keys, out s, out ox, out oy, out vis);
            bool keysOn = BtcConfig.ReplayKeysMode.Value != 0;
            Rect sr = KeysRect(fsR, barH, true);
            Rect kr = new Rect(canvas.x + sr.x * k, canvas.y + sr.y * k, sr.width * k, sr.height * k);
            bool hover = kr.Contains(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                float a = vis && keysOn ? 1f : 0.3f;
                BtcReplayUI.Fill(kr, new Color(BtcHud.Panel.r, BtcHud.Panel.g, BtcHud.Panel.b, 0.5f + 0.5f * a));
                BtcReplayUI.Fill(new Rect(kr.x, kr.y, 3f, kr.height), BtcMenu.AccentA(a));
                BtcReplayUI.Fill(new Rect(kr.x - 1f, kr.y - 1f, kr.width + 2f, 1f), BtcMenu.AccentA(a)); BtcReplayUI.Fill(new Rect(kr.x - 1f, kr.yMax, kr.width + 2f, 1f), BtcMenu.AccentA(a));
                BtcReplayUI.Fill(new Rect(kr.x - 1f, kr.y, 1f, kr.height), BtcMenu.AccentA(a)); BtcReplayUI.Fill(new Rect(kr.xMax, kr.y, 1f, kr.height), BtcMenu.AccentA(a));
                float g = Mathf.Min(kr.height * 0.4f, fs * 1.3f);
                BtcReplayIcons.Glyph(BtcReplayIcons.Keys, new Rect(kr.x + fs * 0.6f, kr.center.y - g * 0.5f, g, g), BtcMenu.AccentA(a));
                BtcVisualRows.Txt(new Rect(kr.x + fs * 0.6f + g + fs * 0.4f, kr.y, kr.width - g - fs, kr.height), BtcReplayLayout.ModName(BtcReplayLayout.Keys), _tiny, new Color(1f, 1f, 1f, 0.85f * a));
                if (vis) BtcReplayUI.Fill(new Rect(kr.xMax - 5f, kr.yMax - 5f, 10f, 10f), new Color(1f, 0.85f, 0.4f, 1f));
            }
            if (hover) BtcReplayIcons.Hint(kr, Tt("Pressed keys of the watched player (appears when a player with recorded keys is watched)", "Нажатые клавиши наблюдаемого игрока (появляется, когда выбран игрок с записанными клавишами)"));

            if (e.type == EventType.MouseDown && e.button == 0 && canvas.Contains(e.mousePosition) && !_rDrag && vis)
            {
                Rect handle = new Rect(kr.xMax - 8f, kr.yMax - 8f, 16f, 16f);
                if (handle.Contains(e.mousePosition) || kr.Contains(e.mousePosition))
                {
                    _rDrag = true; _rMode = handle.Contains(e.mousePosition) ? 2 : 1; _rStart = e.mousePosition;
                    _rOx = ox; _rOy = oy; _rScale = s; _rBefore = BtcReplayLayout.Serialize();
                    BtcSfx.Tick(); e.Use();
                }
            }
            else if (_rDrag && e.type == EventType.MouseDrag)
            {
                Vector2 d = (e.mousePosition - _rStart) / k;       // screen pixels
                if (_rMode == 1) BtcReplayLayout.SetMod(BtcReplayLayout.Keys, _rScale, _rOx + d.x / W, _rOy + d.y / H, true);
                else
                {
                    Rect r0 = KeysRect(fsR, barH, false);
                    float f = (r0.width * _rScale + d.x) / Mathf.Max(1f, r0.width * _rScale);
                    BtcReplayLayout.SetMod(BtcReplayLayout.Keys, _rScale * f, _rOx, _rOy, true);
                }
                e.Use();
            }
            else if (_rDrag && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                // keep the block on the screen
                float s2, ox2, oy2; bool v2;
                BtcReplayLayout.GetMod(BtcReplayLayout.Keys, out s2, out ox2, out oy2, out v2);
                Rect r = KeysRect(fsR, barH, true);
                float mx = 0f, my = 0f;
                if (r.x < 0f) mx = -r.x; else if (r.xMax > W) mx = W - r.xMax;
                if (r.y < 0f) my = -r.y; else if (r.yMax > H - barH) my = H - barH - r.yMax;
                BtcReplayLayout.SetMod(BtcReplayLayout.Keys, s2, ox2 + mx / W, oy2 + my / H, v2);
                BtcReplayLayout.Commit();
                string before = _rBefore;
                if (before != BtcReplayLayout.Serialize())
                    BtcUndo.Push(Scope, Tt("Moved: ", "Перемещено: ") + BtcReplayLayout.ModName(BtcReplayLayout.Keys), delegate { BtcReplayLayout.Restore(before); });
                _rDrag = false;
                if (e.type == EventType.MouseUp) e.Use();
            }

            Rect props = new Rect(body.xMax - propsW - pad, body.y + fs * 0.2f, propsW, body.height - fs * 0.4f);
            BtcReplayUI.Fill(props, new Color(1f, 1f, 1f, 0.02f));
            DrawReplayProps(props, fs);
        }

        private void DrawReplayProps(Rect props, int fs)
        {
            float vw = props.width - (_rH > props.height ? 14f : 0f);
            _rScroll = GUI.BeginScrollView(props, _rScroll, new Rect(0f, 0f, vw, _rH));
            float y = fs * 0.4f, w = vw;
            float s, ox, oy; bool vis;
            BtcReplayLayout.GetMod(BtcReplayLayout.Keys, out s, out ox, out oy, out vis);

            BtcUi.Section(ref y, w, fs, Tt("Pressed keys", "Нажатые клавиши"));
            int km = BtcConfig.ReplayKeysMode.Value;
            string[] kt = { Tt("Off", "Выкл"), Tt("Feed", "Лента"), Tt("Keyboard", "Клавиатура") };
            int kc = BtcVisualRows.Choice(ref y, w, fs, 0, Tt("Mode", "Режим"), km, kt, null, null);
            if (kc >= 0 && kc != km) { int v = kc; REdit(Tt("Pressed keys mode", "Режим нажатых клавиш"), null, delegate { BtcConfig.ReplayKeysMode.Value = v; }); }
            RToggle(ref y, w, fs, Tt("Show the block", "Показывать блок"), vis, delegate (bool nv) { float s1, a1, b1; bool v1; BtcReplayLayout.GetMod(BtcReplayLayout.Keys, out s1, out a1, out b1, out v1); BtcReplayLayout.SetMod(BtcReplayLayout.Keys, s1, a1, b1, nv); });
            RSlider(ref y, w, fs, "ks", Tt("Size", "Размер"), s, 0.3f, 3f, 0.05f, "0.00", "x", delegate (float nv) { float s1, a1, b1; bool v1; BtcReplayLayout.GetMod(BtcReplayLayout.Keys, out s1, out a1, out b1, out v1); BtcReplayLayout.SetMod(BtcReplayLayout.Keys, nv, a1, b1, v1); });
            RSlider(ref y, w, fs, "kx", Tt("Shift right", "Сдвиг вправо"), ox * 100f, -100f, 100f, 0.5f, "0.0", " %", delegate (float nv) { float s1, a1, b1; bool v1; BtcReplayLayout.GetMod(BtcReplayLayout.Keys, out s1, out a1, out b1, out v1); BtcReplayLayout.SetMod(BtcReplayLayout.Keys, s1, nv / 100f, b1, v1); });
            RSlider(ref y, w, fs, "ky", Tt("Shift down", "Сдвиг вниз"), oy * 100f, -100f, 100f, 0.5f, "0.0", " %", delegate (float nv) { float s1, a1, b1; bool v1; BtcReplayLayout.GetMod(BtcReplayLayout.Keys, out s1, out a1, out b1, out v1); BtcReplayLayout.SetMod(BtcReplayLayout.Keys, s1, a1, nv / 100f, v1); });
            y += fs * 0.4f;

            BtcUi.Section(ref y, w, fs, Tt("Control bar", "Панель управления"));
            RSlider(ref y, w, fs, "bs", Tt("Size", "Размер"), BtcReplayLayout.BarScale, 0.6f, 1.6f, 0.05f, "0.00", "x", delegate (float nv) { BtcReplayLayout.BarScale = nv; });
            y += fs * 0.4f;

            BtcUi.Section(ref y, w, fs, Tt("In the world", "В мире"));
            RSlider(ref y, w, fs, "mk", Tt("Marks size", "Размер меток"), BtcReplayLayout.MarkScale, 0.4f, 3f, 0.05f, "0.00", "x", delegate (float nv) { BtcReplayLayout.MarkScale = nv; });
            RSlider(ref y, w, fs, "cs", Tt("Cursor size", "Размер курсора"), BtcReplayLayout.CursorScale, 0.4f, 3f, 0.05f, "0.00", "x", delegate (float nv) { BtcReplayLayout.CursorScale = nv; });
            RSlider(ref y, w, fs, "ls", Tt("Labels size", "Размер подписей"), BtcReplayLayout.LabelScale, 0.4f, 3f, 0.05f, "0.00", "x", delegate (float nv) { BtcReplayLayout.LabelScale = nv; });
            RSlider(ref y, w, fs, "ca", Tt("View cone opacity", "Прозрачность конуса обзора"), BtcReplayLayout.ConeAlpha * 100f, 0f, 200f, 5f, "0", " %", delegate (float nv) { BtcReplayLayout.ConeAlpha = nv / 100f; });
            y += fs * 0.4f;

            BtcUi.Section(ref y, w, fs, Tt("APM graph", "График APM"));
            int am = BtcConfig.ReplayApmMode.Value;
            string[] at = { Tt("Off", "Выкл"), "APM", Tt("EPM", "EPM") };
            int ac = BtcVisualRows.Choice(ref y, w, fs, 0, Tt("Graph", "График"), am, at, null, null);
            if (ac >= 0 && ac != am) { int v = ac; REdit(Tt("APM graph", "График APM"), null, delegate { BtcConfig.ReplayApmMode.Value = v; }); }
            RSlider(ref y, w, fs, "ah", Tt("Height", "Высота"), BtcReplayLayout.ApmHeight, 0.5f, 3f, 0.05f, "0.00", "x", delegate (float nv) { BtcReplayLayout.ApmHeight = nv; });
            RSlider(ref y, w, fs, "aa", Tt("Line opacity", "Прозрачность линии"), BtcReplayLayout.ApmAlpha * 100f, 10f, 100f, 5f, "0", " %", delegate (float nv) { BtcReplayLayout.ApmAlpha = nv / 100f; });
            RSlider(ref y, w, fs, "af", Tt("Fill opacity", "Прозрачность заливки"), BtcReplayLayout.ApmFill * 100f, 0f, 50f, 1f, "0", " %", delegate (float nv) { BtcReplayLayout.ApmFill = nv / 100f; });
            RSlider(ref y, w, fs, "aw", Tt("Line width", "Толщина линии"), BtcReplayLayout.ApmWidth, 0.5f, 5f, 0.1f, "0.0", " px", delegate (float nv) { BtcReplayLayout.ApmWidth = nv; });
            RToggle(ref y, w, fs, Tt("Top value label", "Подпись верхнего значения"), BtcReplayLayout.ApmLabel, delegate (bool nv) { BtcReplayLayout.ApmLabel = nv; });
            RToggle(ref y, w, fs, Tt("Grid lines", "Линии сетки"), BtcReplayLayout.ApmGrid, delegate (bool nv) { BtcReplayLayout.ApmGrid = nv; });
            int mk = BtcReplayLayout.ApmMarks;
            RToggle(ref y, w, fs, Tt("Mark: idle", "Метка: простой"), (mk & 1) != 0, delegate (bool nv) { BtcReplayLayout.ApmMarks = nv ? (mk | 1) : (mk & ~1); });
            RToggle(ref y, w, fs, Tt("Mark: surge", "Метка: всплеск"), (mk & 2) != 0, delegate (bool nv) { BtcReplayLayout.ApmMarks = nv ? (mk | 2) : (mk & ~2); });
            RToggle(ref y, w, fs, Tt("Mark: peak", "Метка: пик"), (mk & 4) != 0, delegate (bool nv) { BtcReplayLayout.ApmMarks = nv ? (mk | 4) : (mk & ~4); });
            y += fs * 0.4f;

            if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Undo, Tt("Reset the replay interface", "Сбросить интерфейс реплея"), ""))
            {
                REdit(Tt("Replay UI reset", "Сброс интерфейса реплея"), null, delegate { BtcReplayLayout.Reset(); BtcConfig.ReplayKeysMode.Value = 1; BtcConfig.ReplayApmMode.Value = 1; });
                BtcSfx.Ok();
            }
            _rH = y + fs;
            GUI.EndScrollView();
        }
    }
}
