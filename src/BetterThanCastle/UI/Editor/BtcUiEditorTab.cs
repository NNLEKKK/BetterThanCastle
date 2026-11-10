using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// UI editor (menu tab 12, layout icon in the top corner next to the changelog): customise how the mod looks and where its screen elements sit.
    /// Pages: Layout (a screen preview where every HUD element is dragged, resized and checked for overlaps, plus a property panel), Menu (sizes, rail style
    /// icons / words, accent colour), Presets (built-in looks, own presets, copy / paste as text), More (shortcuts to the other customisation places).
    /// Data and engine: <see cref="BtcUiEdit"/>. Every change is undoable (scope uiedit).
    /// </summary>
    internal sealed partial class BtcUiEditorTab : BtcMenuTab
    {
        internal static readonly BtcUiEditorTab Instance = new BtcUiEditorTab();
        private const string Scope = "uiedit";
        private const int PLayout = 0, PMenu = 1, PPresets = 2, PMore = 3;
        internal const int PReplay = 4;
        private static readonly int[] Pages = { PLayout, PReplay, PMenu, PSetup, PPresets, PMore };

        internal override int Id { get { return 12; } }
        internal override bool IsActive { get { return BtcMenu.CurrentId == 12; } }
        internal override string UndoScope { get { return Scope; } }

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static int Page { get { return BtcMenuState.Int("uiedit:page", 0); } set { BtcMenuState.SetInt("uiedit:page", value); } }
        private static string Sel { get { return BtcMenuState.Get("uiedit:sel", "launcher"); } set { BtcMenuState.Set("uiedit:sel", value); } }
        private static bool Snap { get { return BtcMenuState.Bool("uiedit:snap", true); } set { BtcMenuState.SetBool("uiedit:snap", value); } }
        private static bool NoOverlap { get { return BtcMenuState.Bool("uiedit:nooverlap", true); } set { BtcMenuState.SetBool("uiedit:nooverlap", value); } }
        private static bool ShowGrid { get { return BtcMenuState.Bool("uiedit:grid", true); } set { BtcMenuState.SetBool("uiedit:grid", value); } }

        private Vector2 _scroll, _propScroll;
        private float _h = 100f, _propH = 100f;
        private GUIStyle _tf, _ttl, _tiny;
        private int _fs;
        private string _name = "";
        private string _confirmDelete;
        private float _pendMenu = -1f, _pendHud = -1f;
        private bool _resetArmed;

        // drag state of the layout canvas
        private string _dragId;
        private int _dragMode;                 // 1 move, 2 resize
        private Vector2 _dragStart;
        private float _dragNx, _dragNy, _dragScale;
        private string _dragBefore;
        private readonly List<float> _guideX = new List<float>(), _guideY = new List<float>();
        private bool _dragOverlap;

        // ------------------------------------------------------------------ styles / frame
        private void Styles(int fs)
        {
            BtcVisualRows.Styles(fs);
            if (_tf != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _ttl = new GUIStyle(BtcVisualRows.Lbl); _ttl.fontSize = Mathf.RoundToInt(fs * 1.15f); _ttl.fontStyle = FontStyle.Bold;
            _tiny = new GUIStyle(BtcVisualRows.Lbl); _tiny.fontSize = Mathf.Max(9, Mathf.RoundToInt(fs * 0.7f)); _tiny.clipping = TextClipping.Clip;
            _tf = new GUIStyle(GUI.skin.textField); _tf.font = b.font; _tf.fontSize = Mathf.RoundToInt(fs * 0.9f); _tf.alignment = TextAnchor.MiddleLeft;
            _tf.normal.background = null; _tf.focused.background = null; _tf.hover.background = null; _tf.active.background = null;
            _tf.normal.textColor = Color.white; _tf.focused.textColor = Color.white; _tf.hover.textColor = Color.white; _tf.padding = new RectOffset(4, 4, 0, 0);
        }

        private static string PageName(int p)
        {
            switch (p)
            {
                case PLayout: return Tt("Screen layout", "Расположение на экране");
                case PReplay: return Tt("Replay interface", "Интерфейс реплея");
                case PSetup: return Tt("Whole-mod setups", "Сетапы всего мода");
                case PMenu: return Tt("Menu look", "Вид меню");
                case PPresets: return Tt("Presets", "Пресеты");
                default: return Tt("More places", "Другие места");
            }
        }

        private static int PageIcon(int p)
        {
            switch (p) { case PLayout: return BtcReplayIcons.Layout; case PReplay: return BtcReplayIcons.Play; case PSetup: return BtcReplayIcons.Share; case PMenu: return BtcReplayIcons.Palette; case PPresets: return BtcReplayIcons.Star; default: return BtcReplayIcons.Grid; }
        }

        /// <summary>Runs a change with an undo step: the whole editor state before the change is restored by Ctrl+Z.</summary>
        private static void Edit(string label, string mergeKey, Action change)
        {
            string before = BtcUiEdit.Serialize();
            change();
            BtcUiEdit.Changed();
            BtcUndo.Push(Scope, label, delegate { BtcUiEdit.Restore(before); }, mergeKey);
        }

        // ------------------------------------------------------------------ draw
        internal override void Draw(Rect area, int fs)
        {
            Styles(fs);
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == BtcMenu.MenuKeyMain && GUIUtility.keyboardControl != 0)
            {
                GUIUtility.keyboardControl = 0;
                BtcMenu.PgDownFrame = Time.frameCount;
                ResourcePackManager.CloseMenu();
                e.Use();
                return;
            }
            BtcUi.ClickAway(e);
            float railW = BtcMenu.SubRailWidth(fs);
            DrawRail(new Rect(area.x, area.y, railW, area.height), fs);
            Rect content = new Rect(area.x + railW, area.y, area.width - railW, area.height);
            BtcReplayUI.Fill(new Rect(content.x - 1f, content.y, 1f, content.height), BtcMenu.Line);

            float pad = fs, ib = fs * 1.9f, y0 = content.y + fs * 0.4f;
            BtcVisualRows.Txt(new Rect(content.x + pad, y0, content.width * 0.4f, ib), PageName(Page), _ttl, Color.white);
            float x = content.xMax - pad - ib;
            bool armed = _resetArmed;
            bool changed = BtcUiEdit.AnyChanged();
            if (BtcReplayIcons.Plain(new Rect(x, y0, ib, ib), BtcReplayIcons.Refresh, armed ? new Color(1f, 0.45f, 0.45f, 1f) : new Color(1f, 1f, 1f, changed ? 1f : 0.35f),
                armed ? Tt("Click again: reset the whole look to the default", "Ещё раз: сбросить весь вид на обычный") : Tt("Reset the whole look to the default", "Сбросить весь вид на обычный")) && changed)
            {
                if (armed) { Edit(Tt("Look reset", "Сброс вида"), null, BtcUiEdit.ResetAll); BtcSfx.Delete(); BtcMenu.Toast(Tt("Default look restored (Ctrl+Z to undo)", "Обычный вид восстановлен (Ctrl+Z отменит)"), -1); _resetArmed = false; }
                else _resetArmed = true;
            }
            else if (armed && e.type == EventType.MouseDown && !new Rect(x, y0, ib, ib).Contains(e.mousePosition)) _resetArmed = false;
            x -= ib + fs;
            Rect sw = new Rect(x - fs * 2.6f, y0 + (ib - fs * 1.2f) * 0.5f, fs * 2.4f, fs * 1.2f);
            if (BtcUi.HeaderSwitch(sw, BtcUiEdit.On, BtcUiEdit.On ? Tt("Your UI customisation is ON (click: back to the default look, your settings are kept)", "Ваша настройка интерфейса ВКЛЮЧЕНА (клик: обычный вид, ваши настройки сохраняются)") : Tt("Your UI customisation is OFF (default look). Click to turn it back on", "Ваша настройка интерфейса ВЫКЛЮЧЕНА (обычный вид). Клик включит её обратно")))
            {
                bool was = BtcUiEdit.On;
                BtcUiEdit.On = !was;
                BtcUndo.Push(Scope, Tt("UI customisation", "Настройка интерфейса"), delegate { BtcUiEdit.On = was; });
            }

            Rect body = new Rect(content.x, y0 + ib + fs * 0.5f, content.width, content.yMax - (y0 + ib + fs * 0.5f));
            BtcReplayUI.Fill(new Rect(content.x + pad, body.y - fs * 0.25f, content.width - pad * 2f, 1f), BtcMenu.Line);
            if (Page == PLayout) DrawLayout(body, fs);
            else if (Page == PReplay) DrawReplay(body, fs);
            else
            {
                float vw = body.width - (_h > body.height ? 14f : 0f);
                _scroll = GUI.BeginScrollView(body, _scroll, new Rect(0f, 0f, vw, _h));
                float cy = fs * 0.2f;
                switch (Page)
                {
                    case PMenu: PageMenu(ref cy, vw, fs); break;
                    case PPresets: PagePresets(ref cy, vw, fs); break;
                    case PSetup: PageSetups(ref cy, vw, fs); break;
                    default: PageMore(ref cy, vw, fs); break;
                }
                _h = cy + fs;
                GUI.EndScrollView();
            }
            BtcReplayIcons.DrawTip(fs, area.xMax, false);
        }

        private void DrawRail(Rect r, int fs)
        {
            Event e = Event.current;
            float item = r.width * 0.82f, x = r.x + (r.width - item) * 0.5f, y = r.y + fs * 0.6f, itemW = item;
            BtcMenu.RailGeometry(r, fs, ref item, ref itemW, ref x);
            for (int i = 0; i < Pages.Length; i++)
            {
                int p = Pages[i];
                Rect ir = new Rect(x, y, itemW, item);
                bool on = p == Page, hover = ir.Contains(e.mousePosition);
                if (on) { BtcReplayUI.Fill(ir, new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.14f)); BtcReplayUI.Fill(new Rect(r.x, y + item * 0.15f, 3f, item * 0.7f), BtcMenu.Accent); }
                else if (hover) BtcReplayUI.Fill(ir, new Color(1f, 1f, 1f, 0.07f));
                float g = item * 0.55f;
                BtcMenu.RailGlyph(ir, fs, PageIcon(p), PageName(p), on, hover);
                BtcReplayIcons.Hint(ir, PageName(p));
                if (BtcSfx.Btn(ir, "", BtcReplayUI.sBtn)) { Page = p; _scroll = Vector2.zero; }
                y += item + fs * 0.3f;
            }
        }

        // ------------------------------------------------------------------ LAYOUT page
        private static Rect ToCanvas(Rect screenRect, Rect canvas)
        {
            float k = canvas.width / Screen.width;
            return new Rect(canvas.x + screenRect.x * k, canvas.y + screenRect.y * k, screenRect.width * k, screenRect.height * k);
        }

        private static bool Overlaps(string id, Rect r, out string with)
        {
            with = null;
            for (int i = 0; i < BtcUiEdit.Elems.Length; i++)
            {
                BtcUiEdit.Elem o = BtcUiEdit.Elems[i];
                if (o.Id == id) continue;
                BtcUiEdit.E eo = BtcUiEdit.Get(o.Id);
                if (!eo.En) continue;
                Rect ro = BtcUiEdit.ScreenRect(o, eo, true);
                if (r.Overlaps(ro)) { with = o.Id; return true; }
            }
            return false;
        }

        /// <summary>Moves an element out of the others (smallest push) and back onto the screen.</summary>
        private static void ResolveOverlap(string id)
        {
            BtcUiEdit.Elem el = BtcUiEdit.ElemOf(id);
            BtcUiEdit.E e = BtcUiEdit.Get(id);
            float H = Screen.height, W = Screen.width;
            for (int it = 0; it < 12; it++)
            {
                Rect r = BtcUiEdit.ScreenRect(el, e, true);
                string other;
                if (!Overlaps(id, r, out other)) break;
                BtcUiEdit.Elem oe = BtcUiEdit.ElemOf(other);
                Rect ro = BtcUiEdit.ScreenRect(oe, BtcUiEdit.Get(other), true);
                float g = 4f;
                float[] dx = { ro.x - r.xMax - g, ro.xMax - r.x + g, 0f, 0f };
                float[] dy = { 0f, 0f, ro.y - r.yMax - g, ro.yMax - r.y + g };
                int best = -1; float bd = float.MaxValue;
                for (int k = 0; k < 4; k++)
                {
                    Rect t = new Rect(r.x + dx[k], r.y + dy[k], r.width, r.height);
                    if (t.x < 0f || t.y < 0f || t.xMax > W || t.yMax > H) continue;
                    float d = Mathf.Abs(dx[k]) + Mathf.Abs(dy[k]);
                    if (d < bd) { bd = d; best = k; }
                }
                if (best < 0) break;
                e.Nx += dx[best] / H; e.Ny += dy[best] / H;
            }
            ClampToScreen(el, e);
        }

        private static void ClampToScreen(BtcUiEdit.Elem el, BtcUiEdit.E e)
        {
            float H = Screen.height, W = Screen.width;
            Rect r = BtcUiEdit.ScreenRect(el, e, true);
            float mx = Mathf.Min(0f, W - r.xMax), my = Mathf.Min(0f, H - r.yMax);
            if (r.x < 0f) mx = -r.x;
            if (r.y < 0f) my = -r.y;
            e.Nx += mx / H; e.Ny += my / H;
        }

        private void DrawLayout(Rect body, int fs)
        {
            Event e = Event.current;
            float pad = fs, gap = fs;
            float propsW = Mathf.Clamp(body.width * 0.34f, fs * 17f, fs * 25f);
            Rect avail = new Rect(body.x + pad, body.y + fs * 0.2f, body.width - propsW - pad * 2f - gap, body.height - fs * 1.2f);
            float aspect = (float)Screen.width / Screen.height;
            float cw = avail.width, ch = cw / aspect;
            if (ch > avail.height - fs * 2.2f) { ch = avail.height - fs * 2.2f; cw = ch * aspect; }
            Rect canvas = new Rect(avail.x, avail.y + fs * 2.2f, cw, ch);
            float k = canvas.width / Screen.width;
            BtcVisualRows.Txt(new Rect(avail.x, avail.y, avail.width, fs * 2f), Tt("Preview of the game screen. Drag a block to move it, the corner handle resizes it. Sizes of messages depend on their text, the preview is approximate.", "Превью экрана игры. Тащите блок, чтобы переместить, угловой маркер меняет размер. Размер сообщений зависит от текста, превью приблизительное."), _tiny, new Color(1f, 1f, 1f, 0.55f));

            // canvas background + guides
            BtcReplayUI.Fill(canvas, new Color(0.012f, 0.016f, 0.02f, 1f));
            BtcReplayUI.Fill(new Rect(canvas.x, canvas.y, canvas.width, 1f), BtcMenu.Line);
            BtcReplayUI.Fill(new Rect(canvas.x, canvas.yMax - 1f, canvas.width, 1f), BtcMenu.Line);
            BtcReplayUI.Fill(new Rect(canvas.x, canvas.y, 1f, canvas.height), BtcMenu.Line);
            BtcReplayUI.Fill(new Rect(canvas.xMax - 1f, canvas.y, 1f, canvas.height), BtcMenu.Line);
            if (ShowGrid)
            {
                Color gc = new Color(1f, 1f, 1f, 0.045f);
                for (int i = 1; i < 6; i++) BtcReplayUI.Fill(new Rect(canvas.x + canvas.width * i / 6f, canvas.y, 1f, canvas.height), gc);
                for (int i = 1; i < 4; i++) BtcReplayUI.Fill(new Rect(canvas.x, canvas.y + canvas.height * i / 4f, canvas.width, 1f), gc);
                BtcReplayUI.Fill(new Rect(canvas.center.x, canvas.y, 1f, canvas.height), new Color(1f, 1f, 1f, 0.09f));
            }
            // a hint of the playfield: the minimap corner and the bottom bar of the game (not editable)
            BtcReplayUI.Fill(new Rect(canvas.x + canvas.width * 0.30f, canvas.yMax - canvas.height * 0.13f, canvas.width * 0.40f, canvas.height * 0.13f), new Color(1f, 1f, 1f, 0.025f));

            // draw order: unselected first, selected last (on top)
            string sel = Sel;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < BtcUiEdit.Elems.Length; i++)
                {
                    BtcUiEdit.Elem el = BtcUiEdit.Elems[i];
                    if ((el.Id == sel) != (pass == 1)) continue;
                    DrawElement(el, canvas, fs);
                }

            // snap guides while dragging
            if (_dragId != null)
            {
                Color gc2 = new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.7f);
                for (int i = 0; i < _guideX.Count; i++) BtcReplayUI.Fill(new Rect(canvas.x + _guideX[i] * k, canvas.y, 1f, canvas.height), gc2);
                for (int i = 0; i < _guideY.Count; i++) BtcReplayUI.Fill(new Rect(canvas.x, canvas.y + _guideY[i] * k, canvas.width, 1f), gc2);
            }

            HandleCanvasMouse(canvas, e);

            // property panel
            Rect props = new Rect(body.xMax - propsW - pad, body.y + fs * 0.2f, propsW, body.height - fs * 0.4f);
            BtcReplayUI.Fill(props, new Color(1f, 1f, 1f, 0.02f));
            DrawProps(props, fs);
        }

        private void DrawElement(BtcUiEdit.Elem el, Rect canvas, int fs)
        {
            Event e = Event.current;
            BtcUiEdit.E s = BtcUiEdit.Get(el.Id);
            Rect r = ToCanvas(BtcUiEdit.ScreenRect(el, s, true), canvas);
            bool selected = el.Id == Sel, hover = r.Contains(e.mousePosition) && (_dragId == null || _dragId == el.Id);
            string with;
            bool bad = s.En && Overlaps(el.Id, BtcUiEdit.ScreenRect(el, s, true), out with);
            if (e.type == EventType.Repaint)
            {
                float a = s.En ? Mathf.Lerp(0.35f, 1f, s.Alpha) : 0.18f;
                BtcReplayUI.Fill(r, new Color(BtcHud.Panel.r, BtcHud.Panel.g, BtcHud.Panel.b, a));
                Color ac = bad ? new Color(1f, 0.4f, 0.38f, 1f) : BtcMenu.Accent;
                BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, r.height), new Color(ac.r, ac.g, ac.b, s.En ? 1f : 0.35f));
                if (hover || selected) BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, selected ? 0.07f : 0.05f));
                if (selected)
                {
                    Color oc = bad ? new Color(1f, 0.4f, 0.38f, 1f) : BtcMenu.Accent;
                    BtcReplayUI.Fill(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, 1f), oc); BtcReplayUI.Fill(new Rect(r.x - 1f, r.yMax, r.width + 2f, 1f), oc);
                    BtcReplayUI.Fill(new Rect(r.x - 1f, r.y, 1f, r.height), oc); BtcReplayUI.Fill(new Rect(r.xMax, r.y, 1f, r.height), oc);
                }
                else if (bad) { Color oc = new Color(1f, 0.4f, 0.38f, 0.8f); BtcReplayUI.Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), oc); }
                float g = Mathf.Min(r.height * 0.7f, fs * 1.3f);
                if (r.width > g * 2f) BtcReplayIcons.Glyph(el.Icon, new Rect(r.x + Mathf.Min(fs * 0.5f, r.width * 0.1f), r.center.y - g * 0.5f, g, g), new Color(ac.r, ac.g, ac.b, s.En ? 1f : 0.35f));
                if (r.width > g * 4f && r.height > fs * 0.9f) BtcVisualRows.Txt(new Rect(r.x + g + fs * 0.9f, r.y, r.width - g - fs * 1.1f, r.height), Tt(el.En, el.Ru), _tiny, new Color(1f, 1f, 1f, s.En ? 0.85f : 0.3f));
                if (selected && s.En)
                {
                    Rect h = HandleRect(r, fs);
                    BtcReplayUI.Fill(h, new Color(1f, 0.85f, 0.4f, 1f));
                }
            }
            if (hover) BtcReplayIcons.Hint(r, Tt(el.En, el.Ru) + (s.En ? "" : Tt(" (hidden)", " (скрыто)")) + ": " + Tt(el.HintEn, el.HintRu));
        }

        private static Rect HandleRect(Rect r, int fs) { float h = Mathf.Max(8f, fs * 0.55f); return new Rect(r.xMax - h * 0.5f, r.yMax - h * 0.5f, h, h); }

        private void HandleCanvasMouse(Rect canvas, Event e)
        {
            float k = canvas.width / Screen.width, H = Screen.height, W = Screen.width;
            if (e.type == EventType.MouseDown && e.button == 0 && canvas.Contains(e.mousePosition) && _dragId == null)
            {
                // topmost block under the pointer: the selected one first, then reverse order
                string hit = null; int mode = 1;
                string sel = Sel;
                BtcUiEdit.Elem se = BtcUiEdit.ElemOf(sel);
                if (se != null)
                {
                    BtcUiEdit.E ss = BtcUiEdit.Get(sel);
                    Rect sr = ToCanvas(BtcUiEdit.ScreenRect(se, ss, true), canvas);
                    if (ss.En && HandleRect(sr, _fs).Contains(e.mousePosition)) { hit = sel; mode = 2; }
                    else if (sr.Contains(e.mousePosition)) hit = sel;
                }
                if (hit == null)
                    for (int i = BtcUiEdit.Elems.Length - 1; i >= 0; i--)
                    {
                        BtcUiEdit.Elem el = BtcUiEdit.Elems[i];
                        if (ToCanvas(BtcUiEdit.ScreenRect(el, BtcUiEdit.Get(el.Id), true), canvas).Contains(e.mousePosition)) { hit = el.Id; break; }
                    }
                if (hit != null)
                {
                    Sel = hit;
                    BtcUiEdit.E s = BtcUiEdit.Get(hit);
                    _dragId = hit; _dragMode = mode; _dragStart = e.mousePosition;
                    _dragNx = s.Nx; _dragNy = s.Ny; _dragScale = s.Scale;
                    _dragBefore = BtcUiEdit.Serialize();
                    BtcSfx.Tick();
                    e.Use();
                }
            }
            else if (_dragId != null && e.type == EventType.MouseDrag)
            {
                BtcUiEdit.Elem el = BtcUiEdit.ElemOf(_dragId);
                BtcUiEdit.E s = BtcUiEdit.Get(_dragId);
                Vector2 d = (e.mousePosition - _dragStart) / k;      // screen pixels
                _guideX.Clear(); _guideY.Clear();
                if (_dragMode == 1)
                {
                    s.Nx = _dragNx + d.x / H; s.Ny = _dragNy + d.y / H;
                    Rect r = BtcUiEdit.ScreenRect(el, s, true);
                    if (Snap)
                    {
                        float th = 7f / k;
                        List<float> xs = new List<float> { 0f, W * 0.5f, W }, ys = new List<float> { 0f, H * 0.5f, H };
                        for (int i = 0; i < BtcUiEdit.Elems.Length; i++)
                        {
                            BtcUiEdit.Elem o = BtcUiEdit.Elems[i];
                            if (o.Id == _dragId) continue;
                            BtcUiEdit.E eo = BtcUiEdit.Get(o.Id);
                            if (!eo.En) continue;
                            Rect ro = BtcUiEdit.ScreenRect(o, eo, true);
                            xs.Add(ro.x); xs.Add(ro.xMax); xs.Add(ro.center.x); ys.Add(ro.y); ys.Add(ro.yMax); ys.Add(ro.center.y);
                        }
                        float bx = th, byv = th, sx = 0f, sy = 0f; float gx = -1f, gy = -1f;
                        float[] mine = { r.x, r.center.x, r.xMax };
                        for (int a = 0; a < 3; a++) for (int i = 0; i < xs.Count; i++) { float dd = xs[i] - mine[a]; if (Mathf.Abs(dd) < bx) { bx = Mathf.Abs(dd); sx = dd; gx = xs[i]; } }
                        float[] mineY = { r.y, r.center.y, r.yMax };
                        for (int a = 0; a < 3; a++) for (int i = 0; i < ys.Count; i++) { float dd = ys[i] - mineY[a]; if (Mathf.Abs(dd) < byv) { byv = Mathf.Abs(dd); sy = dd; gy = ys[i]; } }
                        if (gx >= 0f) { s.Nx += sx / H; _guideX.Add(gx); }
                        if (gy >= 0f) { s.Ny += sy / H; _guideY.Add(gy); }
                    }
                    ClampToScreen(el, s);
                }
                else
                {
                    Rect r0 = BtcUiEdit.ScreenRect(el, new BtcUiEdit.E { Scale = _dragScale, Nx = _dragNx, Ny = _dragNy }, true);
                    float f = r0.width > 1f ? (r0.width + d.x) / r0.width : 1f;
                    s.Scale = Mathf.Clamp(_dragScale * f, 0.4f, 2.5f);
                }
                string w;
                _dragOverlap = Overlaps(_dragId, BtcUiEdit.ScreenRect(el, s, true), out w);
                BtcUiEdit.Apply();
                e.Use();
            }
            else if (_dragId != null && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                BtcUiEdit.Elem el = BtcUiEdit.ElemOf(_dragId);
                BtcUiEdit.E s = BtcUiEdit.Get(_dragId);
                if (NoOverlap) ResolveOverlap(_dragId); else ClampToScreen(el, s);
                string before = _dragBefore;
                BtcUiEdit.Changed();
                if (before != BtcUiEdit.Serialize())
                    BtcUndo.Push(Scope, Tt("Moved: ", "Перемещено: ") + Tt(el.En, el.Ru), delegate { BtcUiEdit.Restore(before); });
                _dragId = null; _guideX.Clear(); _guideY.Clear();
                if (e.type == EventType.MouseUp) e.Use();
            }
        }

        // ------------------------------------------------------------------ property panel
        private void DrawProps(Rect props, int fs)
        {
            float vw = props.width - (_propH > props.height ? 14f : 0f);
            _propScroll = GUI.BeginScrollView(props, _propScroll, new Rect(0f, 0f, vw, _propH));
            float y = fs * 0.4f, w = vw;
            BtcUi.Section(ref y, w, fs, Tt("Elements", "Элементы"));
            Event e = Event.current;
            for (int i = 0; i < BtcUiEdit.Elems.Length; i++)
            {
                BtcUiEdit.Elem el = BtcUiEdit.Elems[i];
                BtcUiEdit.E s = BtcUiEdit.Get(el.Id);
                Rect row = new Rect(fs * 0.5f, y, w - fs, fs * 2f);
                bool on = el.Id == Sel, hover = row.Contains(e.mousePosition);
                BtcReplayUI.Fill(row, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.14f) : new Color(1f, 1f, 1f, hover ? 0.07f : 0.03f));
                float g = fs * 1.2f;
                BtcReplayIcons.Glyph(el.Icon, new Rect(row.x + fs * 0.5f, row.center.y - g * 0.5f, g, g), on ? BtcMenu.Accent : new Color(1f, 1f, 1f, s.En ? 0.7f : 0.25f));
                BtcVisualRows.Txt(new Rect(row.x + fs * 2.3f, row.y, row.width - fs * 5f, row.height), Tt(el.En, el.Ru), BtcVisualRows.Lbl, new Color(1f, 1f, 1f, s.En ? 1f : 0.4f));
                // eye: show / hide (drawn before the row button so it gets the click)
                Rect eye = new Rect(row.xMax - fs * 2.1f, row.y + fs * 0.1f, fs * 1.8f, fs * 1.8f);
                bool eh = eye.Contains(e.mousePosition);
                BtcReplayIcons.Glyph(BtcReplayIcons.Eye, new Rect(eye.x + fs * 0.2f, eye.y + fs * 0.2f, fs * 1.4f, fs * 1.4f), s.En ? (eh ? Color.white : BtcMenu.Accent) : new Color(1f, 1f, 1f, eh ? 0.7f : 0.28f));
                BtcReplayIcons.Hint(eye, s.En ? Tt("Hide this element", "Скрыть этот элемент") : Tt("Show this element", "Показать этот элемент"));
                if (BtcSfx.Btn(eye, "", BtcReplayUI.sBtn))
                {
                    string id = el.Id; bool nv = !s.En;
                    Edit((nv ? Tt("Shown: ", "Показано: ") : Tt("Hidden: ", "Скрыто: ")) + Tt(el.En, el.Ru), null, delegate { BtcUiEdit.Get(id).En = nv; });
                    BtcSfx.Switch(nv);
                }
                if (BtcSfx.Btn(row, "", BtcReplayUI.sBtn)) Sel = el.Id;
                y += fs * 2f + 3f;
            }
            y += fs * 0.4f;

            BtcUiEdit.Elem cur = BtcUiEdit.ElemOf(Sel) ?? BtcUiEdit.Elems[0];
            BtcUiEdit.E ce = BtcUiEdit.Get(cur.Id);
            BtcUi.Section(ref y, w, fs, Tt(cur.En, cur.Ru));
            BtcUi.Note(ref y, w, fs, Tt(cur.HintEn, cur.HintRu));
            string cid = cur.Id;
            float ns = BtcVisualRows.Slider(ref y, w, fs, 0, "uie:s", Tt("Size", "Размер"), ce.Scale, 0.4f, 2.5f, 0.05f, "0.00", "x");
            if (Mathf.Abs(ns - ce.Scale) > 0.0001f) { Edit(Tt("Size: ", "Размер: ") + Tt(cur.En, cur.Ru), "uie:s:" + cid, delegate { BtcUiEdit.Get(cid).Scale = ns; if (NoOverlap) ResolveOverlap(cid); }); }
            float na = BtcVisualRows.Slider(ref y, w, fs, 0, "uie:a", Tt("Opacity", "Прозрачность"), ce.Alpha * 100f, 10f, 100f, 5f, "0", " %") / 100f;
            if (Mathf.Abs(na - ce.Alpha) > 0.0001f) { Edit(Tt("Opacity: ", "Прозрачность: ") + Tt(cur.En, cur.Ru), "uie:a:" + cid, delegate { BtcUiEdit.Get(cid).Alpha = na; }); }
            float aspect = (float)Screen.width / Screen.height;
            float nx = BtcVisualRows.Slider(ref y, w, fs, 0, "uie:x", Tt("Shift right", "Сдвиг вправо"), ce.Nx * 100f, -aspect * 100f, aspect * 100f, 0.5f, "0.0", " %");
            if (Mathf.Abs(nx - ce.Nx * 100f) > 0.001f) { Edit(Tt("Position: ", "Позиция: ") + Tt(cur.En, cur.Ru), "uie:x:" + cid, delegate { BtcUiEdit.Get(cid).Nx = nx / 100f; }); }
            float ny = BtcVisualRows.Slider(ref y, w, fs, 0, "uie:y", Tt("Shift down", "Сдвиг вниз"), ce.Ny * 100f, -100f, 100f, 0.5f, "0.0", " %");
            if (Mathf.Abs(ny - ce.Ny * 100f) > 0.001f) { Edit(Tt("Position: ", "Позиция: ") + Tt(cur.En, cur.Ru), "uie:y:" + cid, delegate { BtcUiEdit.Get(cid).Ny = ny / 100f; }); }
            if (!BtcUiEdit.IsDefault(cid) && BtcUi.Action(ref y, w, fs, BtcReplayIcons.Undo, Tt("Reset this element", "Сбросить этот элемент"), ""))
            {
                Edit(Tt("Reset: ", "Сброс: ") + Tt(cur.En, cur.Ru), null, delegate { BtcUiEdit.CfgSet(cid, BtcUiEdit.DefaultOf(cid)); });
                BtcSfx.Ok();
            }
            y += fs * 0.4f;
            BtcUi.Section(ref y, w, fs, Tt("Helpers", "Помощники"));
            if (BtcUi.Toggle(ref y, w, fs, Tt("Snap to edges and other blocks", "Прилипание к краям и блокам"), Snap, null)) Snap = !Snap;
            if (BtcUi.Toggle(ref y, w, fs, Tt("Never leave blocks overlapping", "Не оставлять блоки наложенными"), NoOverlap, Tt("After a drop the block is pushed aside", "После отпускания блок отодвигается")) ) NoOverlap = !NoOverlap;
            if (BtcUi.Toggle(ref y, w, fs, Tt("Show grid", "Показывать сетку"), ShowGrid, null)) ShowGrid = !ShowGrid;
            _propH = y + fs;
            GUI.EndScrollView();
        }

        // ------------------------------------------------------------------ MENU page
        private static readonly Color[] Swatches =
        {
            new Color(0.55f, 0.85f, 0.55f, 1f), new Color(0.36f, 0.78f, 1f, 1f), new Color(0.71f, 0.55f, 1f, 1f), new Color(1f, 0.6f, 0.78f, 1f),
            new Color(1f, 0.72f, 0.34f, 1f), new Color(1f, 0.85f, 0.4f, 1f), new Color(0.45f, 0.9f, 0.85f, 1f), new Color(1f, 0.45f, 0.42f, 1f), new Color(0.85f, 0.88f, 0.9f, 1f)
        };

        private void PageMenu(ref float y, float w, int fs)
        {
            if (BtcUi.Fold(ref y, w, fs, "uie:size", Tt("Sizes", "Размеры"), 0))
            {
                BtcUi.Note(ref y, w, fs, Tt("Menu size scales this whole window (text, buttons, icons) and applies when you release the slider. HUD size scales the on-screen elements (plates, cards, ping, BTC button) on top of the per-element size.", "Размер меню масштабирует всё это окно (текст, кнопки, иконки) и применяется, когда вы отпускаете ползунок. Размер HUD масштабирует элементы на экране (плашки, карточки, пинг, кнопку BTC) поверх размера каждого элемента."));
                float m = _pendMenu > 0f ? _pendMenu : BtcUiEdit.MenuScaleRaw;
                float nm = BtcVisualRows.Slider(ref y, w, fs, 0, "uie:menu", Tt("Menu size", "Размер меню"), m, 0.7f, 1.6f, 0.05f, "0.00", "x");
                if (Mathf.Abs(nm - m) > 0.0001f) _pendMenu = nm;
                float h = _pendHud > 0f ? _pendHud : BtcUiEdit.HudScaleRaw;
                float nh = BtcVisualRows.Slider(ref y, w, fs, 0, "uie:hud", Tt("HUD size", "Размер HUD"), h, 0.5f, 2f, 0.05f, "0.00", "x");
                if (Mathf.Abs(nh - h) > 0.0001f) { string before = BtcUiEdit.Serialize(); BtcUiEdit.HudScaleRaw = nh; BtcUiEdit.Changed(); BtcUndo.Push(Scope, Tt("HUD size", "Размер HUD"), delegate { BtcUiEdit.Restore(before); }, "uie:hud"); }
                if (Event.current.rawType == EventType.MouseUp && _pendMenu > 0f)
                {
                    float v = _pendMenu; _pendMenu = -1f;
                    if (Mathf.Abs(v - BtcUiEdit.MenuScaleRaw) > 0.0001f) Edit(Tt("Menu size", "Размер меню"), null, delegate { BtcUiEdit.MenuScaleRaw = v; });
                }
                y += fs * 0.4f;
            }
            if (BtcUi.Fold(ref y, w, fs, "uie:rail", Tt("Menu buttons", "Кнопки меню"), 0))
            {
                BtcUi.Note(ref y, w, fs, Tt("How the tab buttons on the left of this window look: icons only (default), icons with words, or just words.", "Как выглядят кнопки вкладок слева в этом окне: только иконки (по умолчанию), иконки со словами или только слова."));
                string[] txt = { Tt("Icons", "Иконки"), Tt("Icons + words", "Иконки + слова"), Tt("Words", "Слова") };
                int sel = BtcVisualRows.Choice(ref y, w, fs, 0, Tt("Tab buttons", "Кнопки вкладок"), BtcUiEdit.RailModeRaw, txt, null, null);
                if (sel >= 0 && sel != BtcUiEdit.RailModeRaw) { int v = sel; Edit(Tt("Menu buttons", "Кнопки меню"), null, delegate { BtcUiEdit.RailModeRaw = v; }); }
                BtcUi.Note(ref y, w, fs, Tt("The same for the page buttons inside a tab (Hotkeys, Visuals, Gameplay, Packs, Profile and this editor).", "То же для кнопок страниц внутри вкладки (Хоткеи, Визуал, Геймплей, Паки, Профиль и этот редактор)."));
                int sub = BtcVisualRows.Choice(ref y, w, fs, 0, Tt("Page buttons", "Кнопки страниц"), BtcUiEdit.SubRailModeRaw, txt, null, null);
                if (sub >= 0 && sub != BtcUiEdit.SubRailModeRaw) { int v2 = sub; Edit(Tt("Page buttons", "Кнопки страниц"), null, delegate { BtcUiEdit.SubRailModeRaw = v2; }); }
                y += fs * 0.4f;
            }
            if (BtcUi.Fold(ref y, w, fs, "uie:accent", Tt("Accent colour", "Цвет акцента"), 0))
            {
                BtcUi.Note(ref y, w, fs, Tt("The colour of the active tab, switches, sliders and headings.", "Цвет активной вкладки, переключателей, ползунков и заголовков."));
                float sz = fs * 2.2f, gx = fs * 0.5f;
                float x = fs + fs * 0.5f;
                Event e = Event.current;
                Color cur = BtcUiEdit.AccentRaw;
                for (int i = 0; i < Swatches.Length; i++)
                {
                    Rect r = new Rect(x + i * (sz + gx), y, sz, sz);
                    bool on = Mathf.Abs(Swatches[i].r - cur.r) + Mathf.Abs(Swatches[i].g - cur.g) + Mathf.Abs(Swatches[i].b - cur.b) < 0.02f;
                    bool hv = r.Contains(e.mousePosition);
                    BtcReplayUI.Fill(r, Swatches[i]);
                    if (on) { BtcReplayUI.Fill(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, 2f), Color.white); BtcReplayUI.Fill(new Rect(r.x - 2f, r.yMax, r.width + 4f, 2f), Color.white); BtcReplayUI.Fill(new Rect(r.x - 2f, r.y, 2f, r.height), Color.white); BtcReplayUI.Fill(new Rect(r.xMax, r.y, 2f, r.height), Color.white); }
                    else if (hv) BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, 0.2f));
                    if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) { Color c = Swatches[i]; Edit(Tt("Accent colour", "Цвет акцента"), null, delegate { BtcUiEdit.AccentRaw = c; }); }
                }
                y += sz + fs * 0.8f;
            }
            if (BtcUi.Fold(ref y, w, fs, "uie:hint", Tt("New-player hint", "Подсказка новичку"), 0))
            {
                BtcUi.Note(ref y, w, fs, Tt("The green BTC button on the screen opens this menu. New players see a bubble next to it once.", "Зелёная кнопка BTC на экране открывает это меню. Новичок один раз видит рядом с ней подсказку."));
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Play, Tt("Show the first-run tour again", "Показать вводный тур снова"), Tt("Five short steps through the menu", "Четыре коротких шага по меню"))) BtcTour.Restart();
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Eye, Tt("Show the hint again", "Показать подсказку снова"), ""))
                {
                    BtcMenuState.SetBool("launcher:hintSeen", false);
                    BtcMenu.Toast(Tt("The hint will show next to the BTC button once the menu is closed", "Подсказка появится у кнопки BTC после закрытия меню"), BtcReplayIcons.Eye);
                }
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Keys, Tt("Change the menu key (Advanced Hotkeys)", "Сменить клавишу меню (Advanced Hotkeys)"), Tt("Now: ", "Сейчас: ") + BtcHotkeys.ComboText("menu_toggle")))
                    BtcChangelog.Go("tab:3:menu_toggle");
            }
            _h = y + fs;
        }

        // ------------------------------------------------------------------ PRESETS page
        private void PagePresets(ref float y, float w, int fs)
        {
            if (BtcUi.Fold(ref y, w, fs, "uie:pre", Tt("Ready looks", "Готовые виды"), 0))
            {
                BtcUi.Note(ref y, w, fs, Tt("A preset sets the sizes, the menu buttons style and the HUD placement. Your master switch stays as it is. Ctrl+Z undoes a preset.", "Пресет задаёт размеры, стиль кнопок меню и расположение HUD. Ваш главный переключатель не меняется. Ctrl+Z отменяет пресет."));
                List<BtcUiEdit.Preset> bi = BtcUiEdit.BuiltIn();
                for (int i = 0; i < bi.Count; i++)
                {
                    BtcUiEdit.Preset p = bi[i];
                    if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Check, Tt(p.En, p.Ru), ""))
                    {
                        string text = p.Text;
                        Edit(Tt("Preset: ", "Пресет: ") + Tt(p.En, p.Ru), null, delegate { BtcUiEdit.ApplyPreset(text); });
                        BtcSfx.Ok();
                        BtcMenu.Toast(Tt("Preset applied", "Пресет применён"), BtcReplayIcons.Check);
                    }
                }
                y += fs * 0.4f;
            }
            if (BtcUi.Fold(ref y, w, fs, "uie:own", Tt("My presets", "Мои пресеты"), 0))
            {
                List<BtcUiEdit.Preset> own = BtcUiEdit.OwnPresets();
                if (own.Count == 0) BtcUi.Note(ref y, w, fs, Tt("No saved presets yet. Set the UI up and save it below.", "Сохранённых пресетов пока нет. Настройте интерфейс и сохраните ниже."));
                Event e = Event.current;
                for (int i = 0; i < own.Count; i++)
                {
                    BtcUiEdit.Preset p = own[i];
                    Rect row = new Rect(fs, y, w - fs * 2f, fs * 2.1f);
                    bool hover = row.Contains(e.mousePosition);
                    BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, hover ? 0.07f : 0.03f));
                    float g = fs * 1.2f;
                    BtcReplayIcons.Glyph(BtcReplayIcons.Star, new Rect(row.x + fs * 0.7f, row.center.y - g * 0.5f, g, g), new Color(1f, 0.85f, 0.4f, 1f));
                    BtcVisualRows.Txt(new Rect(row.x + fs * 2.6f, row.y, row.width - fs * 6f, row.height), p.Name, BtcVisualRows.Lbl, Color.white);
                    Rect tr = new Rect(row.xMax - fs * 2.2f, row.y + fs * 0.15f, fs * 1.8f, fs * 1.8f);
                    bool conf = _confirmDelete == p.Name;
                    if (BtcReplayIcons.Plain(tr, BtcReplayIcons.Trash, conf ? new Color(1f, 0.45f, 0.45f, 1f) : Color.white, conf ? Tt("Click again to delete (kept in Ctrl+Z)", "Ещё раз: удалить (вернётся по Ctrl+Z)") : Tt("Delete this preset", "Удалить этот пресет")))
                    {
                        if (conf)
                        {
                            string name = p.Name, text = p.Text;
                            BtcUiEdit.DeleteOwn(name); _confirmDelete = null; BtcSfx.Delete();
                            BtcUndo.Push(Scope, Tt("Preset deleted: ", "Пресет удалён: ") + name, delegate { try { System.IO.Directory.CreateDirectory(BtcUiEdit.PresetDir); System.IO.File.WriteAllText(System.IO.Path.Combine(BtcUiEdit.PresetDir, name + ".txt"), text, new System.Text.UTF8Encoding(false)); } catch (Exception) { } });
                        }
                        else _confirmDelete = p.Name;
                    }
                    if (BtcSfx.Btn(row, "", BtcReplayUI.sBtn))
                    {
                        string text = p.Text;
                        Edit(Tt("Preset: ", "Пресет: ") + p.Name, null, delegate { BtcUiEdit.ApplyPreset(text); });
                        BtcSfx.Ok();
                    }
                    y += row.height + 3f;
                }
                y += fs * 0.4f;
                // save current
                Rect sr = new Rect(fs, y, Mathf.Min(w - fs * 2f - fs * 9f, fs * 22f), fs * 2.1f);
                BtcReplayUI.Fill(sr, new Color(1f, 1f, 1f, 0.06f));
                BtcUi.Field(sr);
                GUI.SetNextControlName("btc_uie_name");
                _name = GUI.TextField(new Rect(sr.x + 4f, sr.y, sr.width - 8f, sr.height), _name, 30, _tf);
                if (_name.Length == 0 && GUIUtility.keyboardControl == 0) BtcVisualRows.Txt(new Rect(sr.x + 8f, sr.y, sr.width, sr.height), Tt("Name of the preset", "Название пресета"), BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.35f));
                Rect sb = new Rect(sr.xMax + fs * 0.5f, sr.y, fs * 8f, sr.height);
                bool hv = sb.Contains(e.mousePosition);
                BtcReplayUI.Fill(sb, new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, hv ? 0.3f : 0.18f));
                BtcVisualRows.Txt(sb, Tt("Save current", "Сохранить текущий"), BtcVisualRows.Lbl, BtcMenu.Accent);
                if (BtcSfx.Btn(sb, "", BtcReplayUI.sBtn))
                {
                    string nm = BtcUiEdit.SafeName(_name);
                    if (nm.Length == 0) { BtcSfx.Warn(); BtcMenu.Toast(Tt("Type a name first", "Сначала введите название"), BtcReplayIcons.Plus); }
                    else if (BtcUiEdit.SaveOwn(nm))
                    {
                        _name = ""; GUIUtility.keyboardControl = 0; BtcSfx.Ok();
                        BtcUndo.Push(Scope, Tt("Preset saved: ", "Пресет сохранён: ") + nm, delegate { BtcUiEdit.DeleteOwn(nm); });
                        BtcMenu.Toast(Tt("Saved: ", "Сохранено: ") + nm, BtcReplayIcons.Check);
                    }
                    else { BtcSfx.Warn(); }
                }
                y += sr.height + fs * 0.6f;
            }
            if (BtcUi.Fold(ref y, w, fs, "uie:share", Tt("Share as text", "Поделиться текстом"), 0))
            {
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Copy, Tt("Copy my look as text", "Скопировать мой вид текстом"), Tt("Send it to a friend, they paste it below", "Отправьте другу, он вставит его ниже")))
                {
                    GUIUtility.systemCopyBuffer = "BTCUI\n" + BtcUiEdit.Serialize();
                    BtcSfx.Ok(); BtcMenu.Toast(Tt("Copied to the clipboard", "Скопировано в буфер обмена"), BtcReplayIcons.Copy);
                }
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Import, Tt("Paste a look from the clipboard", "Вставить вид из буфера обмена"), ""))
                {
                    string t = GUIUtility.systemCopyBuffer ?? "";
                    if (t.StartsWith("BTCUI"))
                    {
                        Edit(Tt("Pasted look", "Вставленный вид"), null, delegate { BtcUiEdit.ApplyPreset(t.Substring(5)); });
                        BtcSfx.Ok(); BtcMenu.Toast(Tt("Look applied", "Вид применён"), BtcReplayIcons.Check);
                    }
                    else { BtcSfx.Warn(); BtcMenu.Toast(Tt("The clipboard has no BTC look", "В буфере обмена нет вида BTC"), BtcReplayIcons.Cross); }
                }
            }
            _h = y + fs;
        }

        // ------------------------------------------------------------------ MORE page
        private void PageMore(ref float y, float w, int fs)
        {
            BtcUi.Note(ref y, w, fs, Tt("Other parts of the interface are customised where they live. Shortcuts:", "Остальные части интерфейса настраиваются там, где они находятся. Быстрые переходы:"));
            if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Eye, Tt("Visuals: rings, plates, HUD panels, colours", "Визуал: кольца, плашки, панели HUD, цвета"), "")) BtcChangelog.Go("tab:1");
            if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Group, Tt("Profile: your lobby plate, nick, avatar", "Профиль: ваша плашка в лобби, ник, аватар"), "")) BtcChangelog.Go("tab:6");
            if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Chart, Tt("Statistics: the table, the bar and the panel", "Статистика: таблица, полоса и панель"), "")) BtcChangelog.Go("tab:11");
            if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Keys, Tt("Hotkeys: every key, including the menu key", "Хоткеи: любые клавиши, включая клавишу меню"), "")) BtcChangelog.Go("tab:3");
            y += fs * 0.5f;
            BtcUi.Note(ref y, w, fs, Tt("Tip: the BTC button on the screen opens this menu; its position and size are set on the Screen layout page.", "Совет: кнопка BTC на экране открывает это меню; её положение и размер задаются на странице «Расположение на экране»."));
            _h = y + fs;
        }
    }
}
