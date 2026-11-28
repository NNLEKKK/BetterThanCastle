using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// UI editor, shared canvas mouse logic + LIVE mode (F7 in a match): the menu window is replaced by a see-through overlay over the real
    /// game screen, every element gets a frame and is dragged / resized where it really is. Shift+click selects several blocks (they move
    /// together), locked blocks cannot be picked, a toolbar aligns and distributes the selection. The menu stays logically open, so the
    /// game gets no clicks meanwhile; Esc, F7 or Done leave the mode. Undo scope uiedit (Ctrl+Z works here too).
    /// </summary>
    internal sealed partial class BtcUiEditorTab
    {
        /// <summary>The menu draws the live overlay instead of its window (set by <see cref="StartLive"/>).</summary>
        internal static bool Live;
        private static int _liveFrame = -10;
        private static bool _liveLogged;
        private readonly HashSet<string> _multi = new HashSet<string>();
        private readonly Dictionary<string, Vector2> _dragStarts = new Dictionary<string, Vector2>();

        internal static bool Locked(string id) { return BtcMenuState.Bool("uiedit:lock:" + id, false); }
        private static void SetLocked(string id, bool v) { BtcMenuState.SetBool("uiedit:lock:" + id, v); }

        /// <summary>F7 in a match: edit the real screen.</summary>
        internal static void StartLive()
        {
            Live = true;
            _liveFrame = Time.frameCount;
            Page = PLayout;
            Instance._barOff = new Vector2(BtcMenuState.Int("uiedit:barx", 0), BtcMenuState.Int("uiedit:bary", 0));
            BtcSfx.Ok();
        }

        private List<string> Selection()
        {
            List<string> l = new List<string>();
            string sel = Sel;
            if (BtcUiEdit.ElemOf(sel) != null) l.Add(sel);
            foreach (string id in _multi) if (id != sel && BtcUiEdit.ElemOf(id) != null) l.Add(id);
            return l;
        }

        // ------------------------------------------------------------------ canvas mouse (preview page and live mode)
        private void HandleCanvasMouse(Rect canvas, Event e)
        {
            float k = canvas.width / Screen.width, H = Screen.height, W = Screen.width;
            BtcUiEdit.Elem[] all = BtcUiEdit.Elems;
            if (e.type == EventType.MouseDown && e.button == 0 && canvas.Contains(e.mousePosition) && _dragId == null)
            {
                // topmost unlocked block under the pointer: the selected one first, then reverse order
                string hit = null; int mode = 1;
                string sel = Sel;
                BtcUiEdit.Elem se = BtcUiEdit.ElemOf(sel);
                if (se != null && !Locked(sel))
                {
                    BtcUiEdit.E ss = BtcUiEdit.Get(sel);
                    Rect sr = ToCanvas(BtcUiEdit.ScreenRect(se, ss, true), canvas);
                    if (ss.En && _multi.Count <= 1 && HandleRect(sr, _fs).Contains(e.mousePosition)) { hit = sel; mode = 2; }
                    else if (sr.Contains(e.mousePosition)) hit = sel;
                }
                if (hit == null)
                    for (int i = all.Length - 1; i >= 0; i--)
                    {
                        BtcUiEdit.Elem el = all[i];
                        if (Locked(el.Id)) continue;
                        if (ToCanvas(BtcUiEdit.ScreenRect(el, BtcUiEdit.Get(el.Id), true), canvas).Contains(e.mousePosition)) { hit = el.Id; break; }
                    }
                if (hit == null)
                {
                    if (!e.shift && Live) { _multi.Clear(); }
                    return;
                }
                if (e.shift)
                {
                    if (_multi.Count == 0 && BtcUiEdit.ElemOf(Sel) != null) _multi.Add(Sel);
                    if (!_multi.Add(hit)) { _multi.Remove(hit); if (hit == Sel) foreach (string m in _multi) { Sel = m; break; } BtcSfx.Tick(); e.Use(); return; }
                }
                else if (!_multi.Contains(hit)) _multi.Clear();
                Sel = hit;
                BtcUiEdit.E s = BtcUiEdit.Get(hit);
                _dragId = hit; _dragMode = mode; _dragStart = e.mousePosition;
                _dragNx = s.Nx; _dragNy = s.Ny; _dragScale = s.Scale;
                _dragStarts.Clear();
                foreach (string id in Selection()) { BtcUiEdit.E es = BtcUiEdit.Get(id); _dragStarts[id] = new Vector2(es.Nx, es.Ny); }
                _dragBefore = BtcUiEdit.Serialize();
                BtcSfx.Tick();
                e.Use();
            }
            else if (_dragId != null && e.type == EventType.MouseDrag)
            {
                BtcUiEdit.Elem el = BtcUiEdit.ElemOf(_dragId);
                BtcUiEdit.E s = BtcUiEdit.Get(_dragId);
                if (el == null) { _dragId = null; return; }
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
                        for (int i = 0; i < all.Length; i++)
                        {
                            BtcUiEdit.Elem o = all[i];
                            if (_dragStarts.ContainsKey(o.Id) || o.Id == _dragId) continue;
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
                    // the rest of the selection follows the same (snapped, clamped) shift
                    float mx = s.Nx - _dragNx, my = s.Ny - _dragNy;
                    foreach (KeyValuePair<string, Vector2> kv in _dragStarts)
                    {
                        if (kv.Key == _dragId) continue;
                        BtcUiEdit.E o = BtcUiEdit.Get(kv.Key);
                        o.Nx = kv.Value.x + mx; o.Ny = kv.Value.y + my;
                        BtcUiEdit.Elem oe = BtcUiEdit.ElemOf(kv.Key);
                        if (oe != null) ClampToScreen(oe, o);
                    }
                }
                else
                {
                    Rect r0 = BtcUiEdit.ScreenRect(el, new BtcUiEdit.E { Scale = _dragScale, Nx = _dragNx, Ny = _dragNy }, true);
                    float f = r0.width > 1f ? (r0.width + d.x) / r0.width : 1f;
                    s.Scale = Mathf.Clamp(_dragScale * f, 0.3f, 3f);
                }
                BtcUiEdit.Apply();
                e.Use();
            }
            else if (_dragId != null && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                BtcUiEdit.Elem el = BtcUiEdit.ElemOf(_dragId);
                foreach (string id in _dragStarts.Keys)
                {
                    BtcUiEdit.Elem oe = BtcUiEdit.ElemOf(id);
                    if (oe == null) continue;
                    if (NoOverlap && _dragStarts.Count == 1) ResolveOverlap(id); else ClampToScreen(oe, BtcUiEdit.Get(id));
                }
                string before = _dragBefore;
                BtcUiEdit.Changed();
                if (before != BtcUiEdit.Serialize() && el != null)
                    BtcUndo.Push(Scope, (_dragMode == 2 ? Tt("Size: ", "Размер: ") : Tt("Moved: ", "Перемещено: ")) + (_dragStarts.Count > 1 ? Tt("several blocks", "несколько блоков") : Tt(el.En, el.Ru)), delegate { BtcUiEdit.Restore(before); });
                _dragId = null; _guideX.Clear(); _guideY.Clear(); _dragStarts.Clear();
                if (e.type == EventType.MouseUp) e.Use();
            }
            else if (_dragId == null && e.type == EventType.ScrollWheel && canvas.Contains(e.mousePosition))
            {
                // wheel over a selected block: size, Alt + wheel: opacity
                List<string> selList = Selection();
                bool over = false;
                foreach (string id in selList) if (ToCanvas(BtcUiEdit.ScreenRect(BtcUiEdit.ElemOf(id), BtcUiEdit.Get(id), true), canvas).Contains(e.mousePosition)) over = true;
                if (!over) return;
                float step = e.delta.y < 0f ? 1f : -1f;
                bool alpha = e.alt;
                Edit(alpha ? Tt("Opacity", "Прозрачность") : Tt("Size", "Размер"), alpha ? "uie:wa" : "uie:ws", delegate
                {
                    foreach (string id in selList)
                    {
                        BtcUiEdit.E x = BtcUiEdit.Get(id);
                        if (alpha) x.Alpha = Mathf.Clamp(x.Alpha + step * 0.05f, 0.1f, 1f);
                        else x.Scale = Mathf.Clamp(x.Scale + step * 0.05f, 0.3f, 3f);
                    }
                });
                e.Use();
            }
        }

        // ------------------------------------------------------------------ align / distribute the selection
        /// <summary>mode: 0 left, 1 centre, 2 right, 3 top, 4 middle, 5 bottom, 6 spread across, 7 spread down. One block aligns to the screen.</summary>
        private void Align(int mode)
        {
            List<string> sel = Selection();
            if (sel.Count == 0) return;
            float H = Screen.height, W = Screen.width;
            List<Rect> rs = new List<Rect>();
            foreach (string id in sel) rs.Add(BtcUiEdit.ScreenRect(BtcUiEdit.ElemOf(id), BtcUiEdit.Get(id), true));
            Rect box = sel.Count == 1 ? new Rect(0f, 0f, W, H) : rs[0];
            for (int i = 1; i < rs.Count; i++) box = Rect.MinMaxRect(Mathf.Min(box.xMin, rs[i].xMin), Mathf.Min(box.yMin, rs[i].yMin), Mathf.Max(box.xMax, rs[i].xMax), Mathf.Max(box.yMax, rs[i].yMax));
            if (mode >= 6 && sel.Count < 3) return;
            Edit(Tt("Aligned", "Выровнено"), null, delegate
            {
                if (mode >= 6)
                {
                    bool across = mode == 6;
                    List<int> order = new List<int>();
                    for (int i = 0; i < rs.Count; i++) order.Add(i);
                    order.Sort(delegate (int a, int b) { return across ? rs[a].center.x.CompareTo(rs[b].center.x) : rs[a].center.y.CompareTo(rs[b].center.y); });
                    float first = across ? rs[order[0]].center.x : rs[order[0]].center.y, last = across ? rs[order[order.Count - 1]].center.x : rs[order[order.Count - 1]].center.y;
                    for (int j = 1; j < order.Count - 1; j++)
                    {
                        int i = order[j];
                        float want = first + (last - first) * j / (order.Count - 1), cur = across ? rs[i].center.x : rs[i].center.y;
                        BtcUiEdit.E x = BtcUiEdit.Get(sel[i]);
                        if (across) x.Nx += (want - cur) / H; else x.Ny += (want - cur) / H;
                    }
                    return;
                }
                for (int i = 0; i < sel.Count; i++)
                {
                    Rect r = rs[i];
                    float dx = 0f, dy = 0f;
                    switch (mode)
                    {
                        case 0: dx = box.xMin - r.xMin; break;
                        case 1: dx = box.center.x - r.center.x; break;
                        case 2: dx = box.xMax - r.xMax; break;
                        case 3: dy = box.yMin - r.yMin; break;
                        case 4: dy = box.center.y - r.center.y; break;
                        default: dy = box.yMax - r.yMax; break;
                    }
                    BtcUiEdit.E x = BtcUiEdit.Get(sel[i]);
                    x.Nx += dx / H; x.Ny += dy / H;
                    ClampToScreen(BtcUiEdit.ElemOf(sel[i]), x);
                }
            });
            BtcSfx.Ok();
        }

        // ------------------------------------------------------------------ live overlay
        /// <summary>Called by the menu shell instead of the menu window while <see cref="Live"/> is set.</summary>
        internal void DrawLive(int fs)
        {
            if (Time.frameCount - _liveFrame > 3) { Live = false; return; }   // the menu was closed in between: next open is the normal menu
            _liveFrame = Time.frameCount;
            Styles(fs);
            Event e = Event.current;
            BtcInterface.Touch();
            if (!_liveLogged && e.type == EventType.Repaint) { _liveLogged = true; BtcLog.Info("UiEdit", "live overlay: " + BtcUiEdit.Elems.Length + " elements, game HUD panels " + BtcInterface.Count); }
            if (e.type == EventType.KeyDown && (e.control || e.command) && e.keyCode == KeyCode.Z && GUIUtility.keyboardControl == 0) { BtcUndo.Undo(Scope); e.Use(); }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete) { foreach (string id in Selection()) { string cid = id; Edit(Tt("Hidden", "Скрыто"), null, delegate { BtcUiEdit.Get(cid).En = false; }); } e.Use(); }
            Rect canvas = new Rect(0f, 0f, Screen.width, Screen.height);
            if (e.type == EventType.Repaint) BtcReplayUI.Fill(canvas, new Color(0f, 0f, 0f, 0.18f));

            Rect bar = LiveToolbarRect(fs);
            if (e.type == EventType.MouseDrag && _barDrag) { _barOff += e.delta; e.Use(); }
            if (e.rawType == EventType.MouseUp && _barDrag)
            {
                _barDrag = false;
                BtcMenuState.SetInt("uiedit:barx", Mathf.RoundToInt(_barOff.x));
                BtcMenuState.SetInt("uiedit:bary", Mathf.RoundToInt(_barOff.y));
            }
            bar.position += _barOff;
            // frames of the real elements (selected last = on top)
            string sel = Sel;
            BtcUiEdit.Elem[] all = BtcUiEdit.Elems;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < all.Length; i++)
                {
                    BtcUiEdit.Elem el = all[i];
                    bool isSel = el.Id == sel || _multi.Contains(el.Id);
                    if (isSel != (pass == 1)) continue;
                    LiveFrame(el, isSel, el.Id == sel, fs, e);
                }
            if (_dragId != null)
            {
                Color gc = new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.8f);
                for (int i = 0; i < _guideX.Count; i++) BtcReplayUI.Fill(new Rect(_guideX[i], 0f, 1f, Screen.height), gc);
                for (int i = 0; i < _guideY.Count; i++) BtcReplayUI.Fill(new Rect(0f, _guideY[i], Screen.width, 1f), gc);
            }
            bool overBar = bar.Contains(e.mousePosition);
            DrawLiveToolbar(bar, fs);
            if (!overBar) HandleCanvasMouse(canvas, e);
            if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();   // nothing reaches the game
            BtcReplayIcons.DrawTip(fs, Screen.width, false);
        }

        private void LiveFrame(BtcUiEdit.Elem el, bool inSel, bool primary, int fs, Event e)
        {
            BtcUiEdit.E s = BtcUiEdit.Get(el.Id);
            Rect r = BtcUiEdit.ScreenRect(el, s, true);
            bool locked = Locked(el.Id), hover = r.Contains(e.mousePosition) && _dragId == null;
            if (e.type == EventType.Repaint)
            {
                Color c = locked ? new Color(1f, 1f, 1f, 0.35f) : (inSel ? BtcMenu.Accent : new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, hover ? 0.85f : 0.45f));
                if (!s.En) c = new Color(1f, 0.6f, 0.4f, 0.55f);
                float t = inSel ? 2f : 1f;
                BtcReplayUI.Fill(r, new Color(c.r, c.g, c.b, inSel ? 0.1f : (hover ? 0.07f : 0.03f)));
                BtcReplayUI.Fill(new Rect(r.x, r.y, r.width, t), c); BtcReplayUI.Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
                BtcReplayUI.Fill(new Rect(r.x, r.y, t, r.height), c); BtcReplayUI.Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
                if (hover || inSel)
                {
                    string name = Tt(el.En, el.Ru) + (s.En ? "" : Tt(" (hidden)", " (скрыто)")) + (locked ? Tt(" (locked)", " (закреплено)") : "");
                    float tw = Mathf.Min(fs * 0.62f * name.Length + fs, Screen.width * 0.5f), th = fs * 1.4f;
                    Rect tag = new Rect(Mathf.Clamp(r.x, 0f, Screen.width - tw), r.y - th - 2f < 0f ? r.yMax + 2f : r.y - th - 2f, tw, th);
                    BtcReplayUI.Fill(tag, new Color(0f, 0f, 0f, 0.85f));
                    BtcVisualRows.Txt(new Rect(tag.x + fs * 0.4f, tag.y, tag.width - fs * 0.5f, tag.height), name, _tiny, Color.white);
                }
                if (primary && s.En && !locked && _multi.Count <= 1) BtcReplayUI.Fill(HandleRect(r, fs), new Color(1f, 0.85f, 0.4f, 1f));
            }
        }

        private float _liveBarH = 40f, _btnY, _btnRow;
        private Vector2 _barOff;      // the toolbar can be dragged by its left grip (it may cover what you want to edit)
        private bool _barDrag;
        private GUIStyle _liveBtnSt;
        private int _liveBtnFs;

        private Rect LiveToolbarRect(int fs)
        {
            float w = Mathf.Min(Screen.width - fs * 2f, fs * 46f);
            return new Rect((Screen.width - w) * 0.5f, Screen.height * 0.3f, w, _liveBarH);
        }

        /// <summary>Toolbar button; wraps to the next row when the row is full.</summary>
        private bool LiveBtn(ref float x, Rect bar, int fs, string text, string tip, bool on)
        {
            int tfs = Mathf.Max(9, Mathf.RoundToInt(fs * 0.78f));
            if (_liveBtnSt == null || _liveBtnFs != tfs)
            {
                _liveBtnFs = tfs;
                _liveBtnSt = new GUIStyle(BtcVisualRows.Lbl); _liveBtnSt.fontSize = tfs; _liveBtnSt.alignment = TextAnchor.MiddleCenter; _liveBtnSt.clipping = TextClipping.Clip;
            }
            float w = _liveBtnSt.CalcSize(new GUIContent(text)).x + tfs * 1.2f;
            if (x + w > bar.xMax - fs * 0.4f && x > bar.x + fs) { x = bar.x + fs * 0.5f; _btnY += _btnRow; }
            Rect r = new Rect(x, _btnY, w, _btnRow - fs * 0.3f);
            bool hv = r.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(r, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.3f) : new Color(1f, 1f, 1f, hv ? 0.16f : 0.07f));
            BtcVisualRows.Txt(r, text, _liveBtnSt, on ? Color.white : new Color(1f, 1f, 1f, hv ? 1f : 0.8f));
            if (tip != null) BtcReplayIcons.Hint(r, tip);
            x += w + fs * 0.25f;
            return BtcSfx.Btn(r, "", BtcReplayUI.sBtn);
        }

        private void NewRow(ref float x, Rect bar, int fs) { if (x > bar.x + fs) { x = bar.x + fs * 0.5f; _btnY += _btnRow; } }

        private void DrawLiveToolbar(Rect bar, int fs)
        {
            BtcReplayUI.Fill(bar, new Color(0.02f, 0.025f, 0.03f, 0.94f));
            Rect grip = new Rect(bar.x, bar.y, fs * 0.9f, bar.height);
            BtcReplayUI.Fill(grip, new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, grip.Contains(Event.current.mousePosition) || _barDrag ? 0.6f : 0.3f));
            BtcReplayIcons.Hint(grip, Tt("Drag to move the toolbar", "Тащите, чтобы сдвинуть панель"));
            if (Event.current.type == EventType.MouseDown && grip.Contains(Event.current.mousePosition)) { _barDrag = true; Event.current.Use(); }
            _btnRow = fs * 1.9f;
            _btnY = bar.y + fs * 0.3f;
            float x = bar.x + fs * 1.2f;
            DrawLiveButtons(ref x, bar, fs);
            _liveBarH = _btnY + _btnRow + fs * 0.1f - bar.y;   // next frame's height (rows used now)
        }

        private void DrawLiveButtons(ref float x, Rect bar, int fs)
        {
            if (LiveBtn(ref x, bar, fs, Tt("Done", "Готово"), Tt("Back to the game (Esc, F7)", "Назад в игру (Esc, F7)"), true)) { Live = false; ResourcePackManager.CloseMenu(); return; }
            if (LiveBtn(ref x, bar, fs, Tt("Full editor", "Весь редактор"), Tt("Open the UI editor tab with all settings", "Открыть вкладку UI редактора со всеми настройками"), false)) { Live = false; return; }
            x += fs * 0.4f;
            if (LiveBtn(ref x, bar, fs, Tt("Snap", "Прилипание"), Tt("Snap to edges and other blocks", "Прилипание к краям и блокам"), Snap)) Snap = !Snap;
            if (LiveBtn(ref x, bar, fs, Tt("Push apart", "Расталкивать"), Tt("After a drop the block is pushed out of the others", "После отпускания блок выталкивается из других"), NoOverlap)) NoOverlap = !NoOverlap;
            List<string> sel = Selection();
            if (sel.Count > 0) NewRow(ref x, bar, fs);
            bool anyLocked = false;
            foreach (string id in sel) if (Locked(id)) anyLocked = true;
            if (sel.Count > 0 && LiveBtn(ref x, bar, fs, anyLocked ? Tt("Unlock", "Открепить") : Tt("Lock", "Закрепить"), Tt("A locked block cannot be picked or moved by accident", "Закреплённый блок нельзя случайно схватить или сдвинуть"), anyLocked))
            {
                foreach (string id in sel) SetLocked(id, !anyLocked);
                BtcSfx.Switch(!anyLocked);
            }
            if (sel.Count > 0)
            {
                BtcUiEdit.E first = BtcUiEdit.Get(sel[0]);
                bool show = !first.En;
                if (LiveBtn(ref x, bar, fs, show ? Tt("Show", "Показать") : Tt("Hide", "Скрыть"), Tt("Show or hide the selected blocks (Delete hides)", "Показать или скрыть выбранные блоки (Delete скрывает)"), false))
                    Edit(show ? Tt("Shown", "Показано") : Tt("Hidden", "Скрыто"), null, delegate { foreach (string id in sel) BtcUiEdit.Get(id).En = show; });
                if (LiveBtn(ref x, bar, fs, Tt("Reset", "Сброс"), Tt("Default place, size and opacity of the selected blocks", "Обычные место, размер и прозрачность выбранных блоков"), false))
                    Edit(Tt("Reset", "Сброс"), null, delegate { foreach (string id in sel) BtcUiEdit.CfgSet(id, BtcUiEdit.DefaultOf(id)); });
                x += fs * 0.4f;
                string[] al = { Tt("Left", "Лево"), Tt("Centre", "Центр"), Tt("Right", "Право"), Tt("Top", "Верх"), Tt("Middle", "Середина"), Tt("Bottom", "Низ") };
                string scope = sel.Count == 1 ? Tt(" of the screen", " экрана") : Tt(" of the selection", " выделения");
                for (int i = 0; i < al.Length; i++)
                    if (LiveBtn(ref x, bar, fs, al[i], Tt("Align to the ", "Выровнять по: ") + al[i].ToLowerInvariant() + scope, false)) Align(i);
                if (sel.Count >= 3)
                {
                    if (LiveBtn(ref x, bar, fs, Tt("Spread across", "Распределить по ширине"), null, false)) Align(6);
                    if (LiveBtn(ref x, bar, fs, Tt("Spread down", "Распределить по высоте"), null, false)) Align(7);
                }
            }
            // hint line under the toolbar
            Rect hint = new Rect(bar.x, bar.yMax + 2f, bar.width, fs * 1.4f);
            BtcReplayUI.Fill(hint, new Color(0f, 0f, 0f, 0.6f));
            BtcVisualRows.Txt(new Rect(hint.x + fs * 0.5f, hint.y, hint.width - fs, hint.height),
                Tt("Drag a frame to move it, the yellow corner resizes. Shift+click: several blocks. Wheel: size, Alt+wheel: opacity. Ctrl+Z: undo.",
                   "Тащите рамку, чтобы переместить, жёлтый угол меняет размер. Shift+клик: несколько блоков. Колесо: размер, Alt+колесо: прозрачность. Ctrl+Z: отмена."),
                _tiny, new Color(1f, 1f, 1f, 0.75f));
        }
    }
}
