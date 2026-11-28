using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Network;
using Brix.UI.GameSelector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BetterThanCastle
{
    /// <summary>
    /// Mouse tool of the Terraforming task (docs/terraforming.md), a full task of the C# game menu: "Terraforming" in New task (or the
    /// Advanced Hotkeys bind "terraform_tool", Ctrl+G) starts placing a new task, the first stroke creates it on the host and selects
    /// it; while a terraform task of the own faction is selected every stroke changes it at once (no draft, no create / save button).
    /// The task popup (BtcGhPopup terraform sections) holds the mode slots (dig, tunnel / quarry ladder, landing, dig room for
    /// ladders), the kept resources, haul to stockpile and the brush size. A drag runs along the clicked face (top face: flat, Alt:
    /// down into the blocks; wall: level, sideways or into the hill, Alt: up / down). Marks act like blocks for the cursor, so a shape
    /// continues from its own marks (even inside a hill). Ladders: RMB / R rotates, a drag lays a chain up the climbing diagonal.
    /// Ctrl+LMB deletes a mark, Ctrl+Z undoes the last change of the task (unlimited, per task), Esc leaves. Brush width / height:
    /// hold the binds terraform_width / terraform_height and turn the wheel (the camera does not zoom meanwhile).
    /// All settings are remembered (BtcMenuState).
    /// </summary>
    internal static class BtcTerraformTool
    {
        internal const string HotkeyId = "terraform_tool";
        internal const string WidthKey = "terraform_width", HeightKey = "terraform_height";
        internal const string WMinusKey = "terraform_w_minus", WPlusKey = "terraform_w_plus", HMinusKey = "terraform_h_minus", HPlusKey = "terraform_h_plus";
        private static readonly string[] SizeKeys = { WMinusKey, WPlusKey, HMinusKey, HPlusKey };
        private static readonly string[] ModeKeys = { "terraform_dig", "terraform_fill", null, "terraform_ladder" };   // index = Mode (2 = old erase, gone)
        private static readonly KeyCode[] DefKeys = { KeyCode.LeftControl, KeyCode.G };
        private static readonly KeyCode[] NoKeys = new KeyCode[0];

        /// <summary>The tool owns the mouse: placing a new task or editing the selected one.</summary>
        internal static bool Active;
        private static BtcTerraform.Shape _draft = new BtcTerraform.Shape();
        private static readonly List<int> _strokes = new List<int>();

        // 0 dig 1 fill 3 ladder (2 was the erase mode: removed, marks are deleted with Ctrl+LMB)
        private static int Mode { get { int m = BtcMenuState.Int("terraform:mode", 0); return m == 2 ? 0 : m; } set { BtcMenuState.SetInt("terraform:mode", value); } }
        private static int LadKind { get { return BtcMenuState.Int("terraform:ladkind", 0); } set { BtcMenuState.SetInt("terraform:ladkind", value); } } // 0 tunnel 1 quarry 2 landing
        private static int LadDir { get { return BtcMenuState.Int("terraform:laddir", 0) & 3; } set { BtcMenuState.SetInt("terraform:laddir", value & 3); } } // climb towards Dirs[i]
        private static readonly XYZ[] Dirs = { new XYZ(0, 0, 1), new XYZ(1, 0, 0), new XYZ(0, 0, -1), new XYZ(-1, 0, 0) };
        private static XYZ _ladF;      // forward of the ladder under the cursor (low side; you climb towards -F)
        private static bool _ladOk;
        internal static int W { get { return Mathf.Clamp(BtcMenuState.Int("terraform:w", 2), 1, 16); } private set { BtcMenuState.SetInt("terraform:w", Mathf.Clamp(value, 1, 16)); } }
        internal static int H { get { return Mathf.Clamp(BtcMenuState.Int("terraform:h", 3), 1, 32); } private set { BtcMenuState.SetInt("terraform:h", Mathf.Clamp(value, 1, 32)); } }
        // defaults of a new task: kept resources all off (nothing drops, no stockpile needed), haul when the bag is full off
        private static int DefHarvest { get { return BtcMenuState.Int("terraform:keep", 0) & 7; } set { BtcMenuState.SetInt("terraform:keep", value & 7); } }
        private static bool DefStoreFull { get { return BtcMenuState.Int("terraform:storefull", 0) != 0; } set { BtcMenuState.SetInt("terraform:storefull", value ? 1 : 0); } }
        internal static bool AutoDig { get { return BtcMenuState.Int("terraform:autodig", 1) != 0; } private set { BtcMenuState.SetInt("terraform:autodig", value ? 1 : 0); } }

        /// <summary>Mode as the popup shows it: 0 dig, 1 fill, 2 tunnel ladder, 3 quarry ladder, 4 landing.</summary>
        internal static int TfMode { get { int m = Mode; return m == 3 ? 2 + Mathf.Clamp(LadKind, 0, 2) : m == 1 ? 1 : 0; } }

        // stroke state
        private static bool _down, _depth;
        private static XYZ _anchor, _last, _cursor, _n, _axis;
        private static bool _hasCursor, _topFace;
        private static string _note;
        private static float _noteUntil;

        // ladder chain (drag in ladder mode)
        private static XYZ _chainF, _chainFlat;
        private static int _chainN = int.MinValue;
        private static readonly List<BtcTerraform.Op> _chainOps = new List<BtcTerraform.Op>();
        private static readonly List<XYZ> _chainCells = new List<XYZ>();
        private static readonly List<bool> _chainOk = new List<bool>();

        // the task being edited (0 = placing a new one), the request waiting for the host, undo history per task
        private static uint _editNid, _leaveNid;
        private static string _sent, _await;
        private static float _awaitAt;
        private static bool _dirty;
        private static readonly Dictionary<uint, List<string>> _hist = new Dictionary<uint, List<string>>();   // shape before each change ("" = created here: undo deletes it)

        private static bool InMatch
        {
            get { return User.LocalUser != null && User.LocalUser.faction != null && Camera.main != null && !BtcReplay.Active && BtcGameHud.Open; }
        }

        /// <summary>Placing a new task (nothing selected yet).</summary>
        internal static bool Placing { get { return Active && _editNid == 0; } }
        internal static uint EditingTask { get { return Active ? _editNid : 0; } }

        // ------------------------------------------------------------------ input (Update)
        internal static void Tick()
        {
            try
            {
                if (!InMatch) { if (Active) Close(); return; }
                // a terraform task of the own faction selected the vanilla way (task list, beacon) = edit it; deselected = leave
                uint selNid; BtcTerraform.Shape selS;
                bool sel = BtcTerraform.SelectedOwn(out selNid, out selS);
                if (!sel) _leaveNid = 0;
                else if (selNid == _leaveNid) sel = false;   // being deselected by OpenNew / Esc
                if (sel) { if (!Active || _editNid != selNid) BeginEdit(selNid, selS); }
                else if (Active && _editNid != 0) Close();
                else if (Placing && !BtcGhLeft.IsIdle && _await == null) Close();   // another task was selected while placing
                if (!BtcHotkeys.MenuOpen)
                {
                    if (BtcHotkeys.ComboPressed(BtcHotkeys.GetKeys(HotkeyId, DefKeys)))
                    {
                        if (Placing) Close(); else OpenNew();
                        return;
                    }
                    for (int i = 0; i < ModeKeys.Length; i++)
                    {
                        if (ModeKeys[i] == null) continue;
                        KeyCode[] k = BtcHotkeys.GetKeys(ModeKeys[i], NoKeys);
                        if (k.Length == 0 || !BtcHotkeys.ComboPressed(k)) continue;
                        if (!Active) OpenNew();
                        if (Mode != i) { Mode = i; BtcSfx.Click(); Changed(); }
                        return;
                    }
                }
                if (!Active) return;
                if (_await != null && Time.unscaledTime > _awaitAt + 10f) { _await = null; Note(BtcLang.TrG("The host did not create the task")); }
                if (Input.GetKeyDown(KeyCode.Escape)) { if (_down) Cancel(); else Leave(); return; }
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                if (ctrl && Input.GetKeyDown(KeyCode.Z)) { Undo(); return; }
                if (!BtcHotkeys.MenuOpen)
                    for (int i = 0; i < SizeKeys.Length; i++)
                    {
                        KeyCode[] k = BtcHotkeys.GetKeys(SizeKeys[i], NoKeys);
                        if (k.Length != 0 && BtcHotkeys.ComboPressed(k)) Resize(i < 2 ? (i == 0 ? -1 : 1) : 0, i >= 2 ? (i == 2 ? -1 : 1) : 0);
                    }
                if (Mode == 3 && LadKind != 2 && !ctrl && Input.GetKeyDown(KeyCode.R)) Rotate(shift);
                Camera cam = Camera.main;
                Vector3 mouse = Input.mousePosition;
                bool overUi = BtcHotkeys.MenuOpen || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
                if (_down)
                {
                    if (Mode == 3) LadderDrag(cam, mouse, shift);
                    else
                    {
                        // the brush block is stretched from the anchor straight along one axis (4 ways on the face, or into it
                        // with Alt): a straight tunnel / trench of the brush's width and height
                        XYZ c;
                        if (DragCell(cam, mouse, out c)) _cursor = Straight(_anchor, c);
                        if (!_cursor.Equals(_last))
                        {
                            _last = _cursor;
                            Truncate(_strokes[_strokes.Count - 1]);
                            AddOp(Box(_anchor, _cursor));
                        }
                    }
                    if (Input.GetMouseButtonUp(0)) EndStroke();
                    else if (Input.GetMouseButtonDown(1)) Cancel();
                    return;
                }
                _hasCursor = !overUi && Pick(cam, mouse, out _cursor, out _topFace);
                if (overUi) return;
                if (ctrl && Input.GetMouseButtonDown(0)) { DeleteAt(cam, mouse); return; }
                if (Mode == 3)
                {
                    if (_hasCursor) LadderAtCursor();
                    if (Input.GetMouseButtonDown(1) && LadKind != 2) Rotate(shift);
                    if (_hasCursor && Input.GetMouseButtonDown(0))
                    {
                        _anchor = _cursor; _down = true; _chainF = _ladF; _chainFlat = new XYZ(1, 0, 0);
                        _chainRow = false; _chainN = int.MinValue;
                        SetChain(0);
                    }
                    return;
                }
                if (_hasCursor && Input.GetMouseButtonDown(0))
                {
                    _anchor = _cursor; _last = _cursor; _down = true; _depth = alt;
                    _axis = Mode == 1 ? _n : new XYZ(-_n.x, -_n.y, -_n.z);   // Alt: fill grows out of the face, dig / erase go in
                    _strokes.Add(_draft.Ops.Count);
                    AddOp(Box(_anchor, _anchor));
                }
            }
            catch (Exception e) { BtcLog.Error("Terraform", "Tool.Tick: " + e.Message); }
        }

        /// <summary>Brush size: hold the width / height bind and turn the wheel (1 per notch). True while a bind is held: the
        /// camera zoom skips the wheel then (CameraScript patch).</summary>
        internal static bool WheelHeld()
        {
            if (!Active || BtcHotkeys.MenuOpen) return false;
            bool w = Held(WidthKey, KeyCode.LeftControl), h = !w && Held(HeightKey, KeyCode.LeftShift);
            return w || h;
        }

        private static bool Held(string id, KeyCode def)
        {
            KeyCode[] k = BtcHotkeys.GetKeys(id, new[] { def });
            if (k == null || k.Length == 0) return false;
            for (int i = 0; i < k.Length; i++) if (!Input.GetKey(k[i])) return false;
            return true;
        }

        private static bool _muted;
        private static float _savedZoom = 1f;

        /// <summary>True while a brush size bind is held: the wheel belongs to the brush (honest wheel zoom skips it, vanilla zoom is muted).</summary>
        internal static bool WheelTaken { get; private set; }

        /// <summary>CameraBookmarkHotkey.Update (like BtcStationRadius.Tick): one size step per wheel notch while a bind is held.</summary>
        internal static void CamTick(Brix.Input.CameraScript cam)
        {
            bool taken = false;
            try
            {
                taken = WheelHeld();
                float d = taken ? Input.mouseScrollDelta.y : 0f;
                if (d != 0f && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
                {
                    int step = d > 0f ? 1 : -1;
                    if (Held(WidthKey, KeyCode.LeftControl)) W = W + step; else H = H + step;
                    BtcSfx.Tick();
                    Changed();
                }
            }
            catch (Exception e) { BtcLog.Error("Terraform", "Tool.Wheel: " + e.Message); }
            finally
            {
                WheelTaken = taken;
                if (cam != null)
                {
                    // every frame: HandleFastModes (earlier in the same Update) rewrites ZoomSpeedMod each frame
                    if (taken) { if (!_muted) _savedZoom = cam.ZoomSpeedMod; cam.ZoomSpeedMod = 0f; }
                    else if (_muted && cam.ZoomSpeedMod == 0f) cam.ZoomSpeedMod = _savedZoom;
                    _muted = taken;
                }
            }
        }

        private static void Open()
        {
            Active = true;
            _down = false;
            if (!BtcTerraform.On) Note(BtcLang.TrG("Terraforming needs BetterConnection (BC tab)"));
            Changed();
        }

        /// <summary>Starts placing a new task (New task grid, Ctrl+G): leaves a selected task first.</summary>
        internal static void OpenNew()
        {
            if (!InMatch) return;
            if (!BtcTerraform.On) { Note(BtcLang.TrG("Terraforming needs BetterConnection (BC tab)")); BtcSfx.Miss(); return; }
            if (_editNid != 0) _leaveNid = _editNid;
            BtcTrapTaskTool.Close();
            if (!BtcGhLeft.IsIdle) SelectIdle();
            _editNid = 0; _await = null; _dirty = false; _sent = null;
            _draft = NewShape();
            _strokes.Clear();
            Open();
            BtcSfx.Click();
        }

        private static BtcTerraform.Shape NewShape()
        {
            BtcTerraform.Shape s = new BtcTerraform.Shape();
            s.Harvest = DefHarvest;
            s.StoreFull = DefStoreFull;
            return s;
        }

        internal static void Close()
        {
            bool was = Active;
            Active = false;
            _down = false;
            _hasCursor = false;
            _editNid = 0;
            _chainOps.Clear(); _chainCells.Clear(); _chainOk.Clear();
            if (was) Changed();
        }

        /// <summary>Esc: a selected task is deselected (vanilla idle selection), placing is cancelled.</summary>
        private static void Leave()
        {
            if (_editNid != 0) { _leaveNid = _editNid; SelectIdle(); }
            Close();
        }

        private static void SelectIdle()
        {
            try { IdleProject idle = IdleProject.GetAlliedProject(); if (idle != null) UIGameSelector.Select(idle.gameObject); }
            catch (Exception e) { BtcLog.Error("Terraform", "select idle: " + e.Message); }
        }

        private static void Changed() { BtcGhPopup.RefreshTerraform(); }

        private static void Note(string text) { _note = text; _noteUntil = Time.unscaledTime + 4f; }

        private static void Rotate(bool back)
        {
            LadDir = LadDir + (back ? 3 : 1);
            BtcSfx.Click();
        }

        // ------------------------------------------------------------------ popup actions
        /// <summary>Mode slot of the popup: 0 dig, 1 fill, 2 tunnel ladder, 3 quarry ladder, 4 landing.</summary>
        internal static void SetMode(int m)
        {
            if (m <= 0) Mode = 0; else if (m == 1) Mode = 1; else { Mode = 3; LadKind = Mathf.Clamp(m - 2, 0, 2); }
            Changed();
        }

        internal static void ToggleAutoDig() { AutoDig = !AutoDig; Changed(); }

        /// <summary>Brush size step (popup - / + buttons and their binds).</summary>
        internal static void Resize(int dw, int dh) { W = W + dw; H = H + dh; BtcSfx.Tick(); Changed(); }

        /// <summary>Kept resources of the task (or of the next new task while placing).</summary>
        internal static int Harvest { get { return Active ? _draft.Harvest : DefHarvest; } }
        internal static bool StoreFull { get { return Active ? _draft.StoreFull : DefStoreFull; } }

        internal static void ToggleKeep(int flag)
        {
            int h = Harvest ^ flag;
            DefHarvest = h;
            if (!Active) { Changed(); return; }
            Remember(BtcLang.TrG("Kept resources"));
            _draft.Harvest = h;
            Commit();
        }

        internal static void ToggleStoreFull()
        {
            bool v = !StoreFull;
            DefStoreFull = v;
            if (!Active) { Changed(); return; }
            Remember(BtcLang.TrG("Haul to stockpile"));
            _draft.StoreFull = v;
            Commit();
        }

        // ------------------------------------------------------------------ strokes -> task
        private static void EndStroke()
        {
            _down = false;
            if (Mode == 3)
            {
                if (_chainOps.Count == 0)
                {
                    Note(LadChar == 'T' ? BtcLang.TrG("A tunnel ladder needs a full block under it and a wall behind it")
                                        : BtcLang.TrG("A quarry ladder replaces a full terrain block that stands on something"));
                    return;
                }
                string before = BtcTerraform.Encode(_draft);
                _strokes.Add(_draft.Ops.Count);
                for (int i = 0; i < _chainOps.Count; i++)
                {
                    int n = _draft.Ops.Count;
                    AddOp(_chainOps[i]);
                    if (_draft.Ops.Count == n) break;   // too big
                }
                _chainOps.Clear(); _chainCells.Clear(); _chainOk.Clear();
                RememberText(before, BtcLang.TrG("Ladders"));
                BtcSfx.Click();
                Commit();
                return;
            }
            // the stroke is already in the draft (AddOp while dragging): its undo entry is the draft without it
            int at = _strokes.Count > 0 ? _strokes[_strokes.Count - 1] : 0;
            BtcTerraform.Shape prev = _draft.Clone();
            if (at < prev.Ops.Count) { prev.Ops.RemoveRange(at, prev.Ops.Count - at); prev.Rebuild(); }
            RememberText(BtcTerraform.Encode(prev), BtcLang.TrG("Marks"));
            Commit();
        }

        // keeps only the biggest axis of b - a: lines run straight in one of the 4 (6 with Alt) directions
        private static XYZ Straight(XYZ a, XYZ b)
        {
            int dx = b.x - a.x, dy = b.y - a.y, dz = b.z - a.z;
            int ax = Math.Abs(dx), ay = Math.Abs(dy), az = Math.Abs(dz);
            if (ax >= ay && ax >= az) return new XYZ(b.x, a.y, a.z);
            if (ay >= az) return new XYZ(a.x, b.y, a.z);
            return new XYZ(a.x, a.y, b.z);
        }

        private static void Cancel()
        {
            bool ladder = Mode == 3;
            _down = false;
            _chainOps.Clear(); _chainCells.Clear(); _chainOk.Clear();
            if (!ladder && _strokes.Count > 0) { Truncate(_strokes[_strokes.Count - 1]); _strokes.RemoveAt(_strokes.Count - 1); }
        }

        private static void Truncate(int count)
        {
            if (count < _draft.Ops.Count) _draft.Ops.RemoveRange(count, _draft.Ops.Count - count);
            _draft.Rebuild();
        }

        private static void AddOp(BtcTerraform.Op o)
        {
            if (_draft.Ops.Count >= BtcTerraform.MaxOps) { Note(BtcLang.TrG("The shape is too big")); _down = false; return; }
            _draft.Ops.Add(o);
            if (!_draft.Rebuild())
            {
                _draft.Ops.RemoveAt(_draft.Ops.Count - 1);
                _draft.Rebuild();
                Note(BtcLang.TrG("The shape is too big"));
                _down = false;
            }
        }

        /// <summary>Sends the draft: a new task (first stroke), or the new shape of the task being edited. An empty shape deletes it.</summary>
        private static void Commit()
        {
            if (!BtcTerraform.On) { Note(BtcLang.TrG("Terraforming needs BetterConnection (BC tab)")); return; }
            if (_editNid != 0)
            {
                if (_draft.Empty) { DeleteTask(_editNid); return; }
                _sent = BtcTerraform.Encode(_draft);
                BtcTerraform.RequestEdit(_editNid, _draft.Clone());
            }
            else if (_await != null) _dirty = true;   // the task is not there yet: sent as an edit once the host made it
            else if (!_draft.Empty)
            {
                _await = BtcTerraform.Encode(_draft);
                _awaitAt = Time.unscaledTime;
                _dirty = false;
                BtcTerraform.RequestCreate(_draft.Clone());
            }
            Changed();
        }

        private static void DeleteTask(uint nid)
        {
            Project p = BtcTerraform.ProjectOf(nid);
            if (p != null && Project.ShowDelete(p) && Project.HasDelete(p)) Project.SetDelete(p);
            _hist.Remove(nid);
            Close();
        }

        /// <summary>BtcTerraform: a shape got bound to its task (created, edited by anyone, late spawn on a client).</summary>
        internal static void OnBound(uint nid, Project p, BtcTerraform.Shape s)
        {
            if (p == null || User.LocalUser == null || p.faction != User.LocalUser.faction) return;
            string text = BtcTerraform.Encode(s);
            if (_await != null && text == _await && _editNid == 0 && Active)
            {
                // our new task: select it (the vanilla way), later strokes edit it; undo of the first stroke deletes it
                _await = null;
                List<string> h = Hist(nid);
                h.Add("");
                _editNid = nid; _sent = text;
                UIGameSelector.Select(p.gameObject);
                if (_dirty) { _dirty = false; _sent = BtcTerraform.Encode(_draft); BtcTerraform.RequestEdit(nid, _draft.Clone()); }
                Changed();
                return;
            }
            // edited by someone else (an ally): reload, the strokes of this session no longer apply
            if (Active && nid == _editNid && !_down && text != _sent && _sent != null)
            {
                _draft = s.Clone();
                _strokes.Clear();
                _sent = text;
                Changed();
            }
        }

        // ------------------------------------------------------------------ undo (per task, unlimited)
        private static List<string> Hist(uint nid)
        {
            List<string> h;
            if (!_hist.TryGetValue(nid, out h)) { h = new List<string>(); _hist[nid] = h; }
            return h;
        }

        private static string _undoLabel;

        private static void Remember(string label) { RememberText(BtcTerraform.Encode(_draft), label); }

        private static void RememberText(string before, string label)
        {
            _undoLabel = label;
            if (_editNid != 0) { List<string> h = Hist(_editNid); h.Add(before); if (h.Count > 300) h.RemoveAt(0); }
        }

        private static void Undo()
        {
            if (_down) return;
            if (_editNid == 0)
            {
                // placing: only the strokes not sent yet
                if (_strokes.Count == 0 || _await != null) { Note(BtcLang.TrG("Nothing to undo")); BtcSfx.Miss(); return; }
                Truncate(_strokes[_strokes.Count - 1]);
                _strokes.RemoveAt(_strokes.Count - 1);
                BtcSfx.Jump();
                Changed();
                return;
            }
            List<string> h;
            if (!_hist.TryGetValue(_editNid, out h) || h.Count == 0) { Note(BtcLang.TrG("Nothing to undo")); BtcSfx.Miss(); return; }
            string prev = h[h.Count - 1];
            h.RemoveAt(h.Count - 1);
            BtcSfx.Jump();
            if (prev.Length == 0) { Note(BtcLang.TrG("Undone: the task is deleted")); DeleteTask(_editNid); return; }
            BtcTerraform.Shape s = BtcTerraform.Decode(prev);
            if (s == null) return;
            _draft = s;
            _strokes.Clear();
            Note(BtcLang.TrG("Undone") + (h.Count > 0 ? "   (" + h.Count + ")" : ""));
            if (_draft.Empty) { DeleteTask(_editNid); return; }
            _sent = prev;
            BtcTerraform.RequestEdit(_editNid, _draft.Clone());
            Changed();
        }

        // ------------------------------------------------------------------ delete one mark (Ctrl+LMB)
        private static void DeleteAt(Camera cam, Vector3 screen)
        {
            Ray r = cam.ScreenPointToRay(screen);
            XYZ cell, n; float d;
            if (MarkRay(r, 600f, true, out cell, out n, out d))
            {
                string before = BtcTerraform.Encode(_draft);
                _strokes.Add(_draft.Ops.Count);
                AddOp(BtcTerraform.Cell('E', cell));
                RememberText(before, BtcLang.TrG("Delete a mark"));
                BtcSfx.Click();
                Commit();
                return;
            }
            // a mark of another task of the own faction: select that task (it opens for editing), the next Ctrl+click deletes
            uint nid; BtcTerraform.Shape s;
            if (MarkRay(r, 600f, false, out cell, out n, out d) && BtcTerraform.FindOwn(cell, cell, out nid, out s) && nid != _editNid)
            {
                Project p = BtcTerraform.ProjectOf(nid);
                if (p != null) { UIGameSelector.Select(p.gameObject); BtcSfx.Click(); }
                return;
            }
            Note(BtcLang.TrG("No mark here"));
        }

        // ------------------------------------------------------------------ edit an existing task
        /// <summary>Loads a task of the local faction into the tool (every change goes to the host at once).</summary>
        internal static void BeginEdit(uint nid, BtcTerraform.Shape s)
        {
            _down = false;
            _editNid = nid;
            _await = null; _dirty = false;
            _draft = s.Clone();
            _sent = BtcTerraform.Encode(_draft);
            _strokes.Clear();
            if (!Active) Open(); else Changed();
        }

        // ------------------------------------------------------------------ ladders
        private static char LadChar { get { int k = LadKind; return k == 1 ? 'Q' : k == 2 ? 'B' : 'T'; } }

        // tunnel ladder on a wall face: it goes into the air cell in front of the wall with its back to the wall
        // (climb into the wall direction); otherwise the rotation from RMB / R
        private static void LadderAtCursor()
        {
            char k = LadChar;
            if (k == 'B') _ladF = XYZ.up;
            else if (k == 'T' && _n.y == 0 && !_crustHit) _ladF = _n;
            else { XYZ d = Dirs[LadDir]; _ladF = new XYZ(-d.x, 0, -d.z); }
            if (!_cursor.Equals(_okCell) || !_ladF.Equals(_okF) || k != _okK || _draft.Ops.Count != _okOps)
            {
                _okCell = _cursor; _okF = _ladF; _okK = k; _okOps = _draft.Ops.Count;
                _ladOk = BtcTerraform.LadderPlaceable(_cursor, k, _ladF, _draft);
            }
        }

        private static XYZ _okCell, _okF;
        private static char _okK;
        private static int _okOps = -1;

        // drag in ladder mode: quarry / tunnel ladders follow the climbing diagonal, landings a flat line
        private static void LadderDrag(Camera cam, Vector3 mouse, bool down)
        {
            Ray r = cam.ScreenPointToRay(mouse);
            char k = LadChar;
            int n;
            XYZ flat = _chainFlat;
            // Shift: a sideways row of quarry / tunnel ladders at one height instead of the climbing diagonal
            bool row = k == 'B' || down;
            if (row)
            {
                XYZ c;
                if (!PlaneHit(r, _anchor.ToVector3() + Vector3.up * 0.5f, Vector3.up, out c)) return;
                int dx = c.x - _anchor.x, dz = c.z - _anchor.z;
                if (Math.Abs(dx) >= Math.Abs(dz)) { flat = new XYZ(dx < 0 ? -1 : 1, 0, 0); n = Math.Abs(dx); }
                else { flat = new XYZ(0, 0, dz < 0 ? -1 : 1); n = Math.Abs(dz); }
            }
            else
            {
                Vector3 step = new Vector3(-_chainF.x, 1f, -_chainF.z);
                float t;
                if (!LineParam(r, _anchor.ToVector3(), step, out t)) return;
                // up the climbing diagonal, or down it when the mouse goes the other way
                n = Mathf.Clamp(Mathf.RoundToInt(t), -64, 64);
            }
            if (n != _chainN || !flat.Equals(_chainFlat) || row != _chainRow) { _chainFlat = flat; _chainRow = row; SetChain(n); }
        }

        private static bool _chainRow;

        private static void SetChain(int n)
        {
            _chainN = n;
            BtcTerraform.LadderChain(_draft, _anchor, LadChar, _chainF, _chainFlat, n, AutoDig && LadChar != 'B', _chainOps, _chainCells, _chainOk, _chainRow && LadChar != 'B');
        }

        // ------------------------------------------------------------------ geometry
        private static char Kind { get { int m = Mode; return m == 1 ? 'F' : m == 2 ? 'E' : 'D'; } }
        private static bool _crustHit;

        private static XYZ Axis(Vector3 v)
        {
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
            if (ay >= ax && ay >= az) return new XYZ(0, v.y >= 0f ? 1 : -1, 0);
            if (ax >= az) return new XYZ(v.x >= 0f ? 1 : -1, 0, 0);
            return new XYZ(0, 0, v.z >= 0f ? 1 : -1);
        }

        /// <summary>Cell under the cursor. Marks of the draft and of own tasks count as blocks (so a shape continues from its
        /// marks, even inside a hill), else the terrain hit. Dig / erase / quarry ladder: the block itself (under a crust: the
        /// full block below it); fill: the air cell in front; tunnel ladder: a crust cell itself, else the air cell in front.</summary>
        private static bool Pick(Camera cam, Vector3 screen, out XYZ cell, out bool top)
        {
            cell = default(XYZ); top = false; _crustHit = false;
            Ray ray = cam.ScreenPointToRay(screen);
            RaycastHit hit;
            bool phys = Physics.Raycast(ray, out hit, 1000f, 132096, QueryTriggerInteraction.Ignore);
            XYZ solid, n, mc; float md;
            bool mark = MarkRay(ray, phys ? hit.distance + 12f : 400f, false, out mc, out n, out md);
            if (mark) solid = mc;
            else if (phys)
            {
                Vector3 pi = hit.point - hit.normal * 0.05f;
                solid = new XYZ(Mathf.RoundToInt(pi.x), Mathf.RoundToInt(pi.y), Mathf.RoundToInt(pi.z));
                n = Axis(hit.normal);
            }
            else return false;
            _n = n;
            top = n.y > 0;
            bool crust = !mark && Voxel.IsCrust(solid);
            _crustHit = crust;
            int m = Mode;
            char lk = LadChar;
            if (mark && m != 2) cell = solid + n;   // a new mark goes ON an existing one (next to the face), never into it
            else if (m == 1) cell = crust ? solid : solid + n;   // fill a crust itself, not the air above it
            else if (m == 3 && lk == 'T') cell = crust ? solid : solid + n;
            else cell = crust && Voxel.IsFull(solid - XYZ.up) ? solid - XYZ.up : solid;
            if (crust) { _n = XYZ.up; top = true; }
            return true;
        }

        /// <summary>Voxel walk along the ray (Amanatides-Woo): the first mark cell and the face it was entered through.</summary>
        private static bool MarkRay(Ray r, float maxDist, bool draftOnly, out XYZ cell, out XYZ n, out float dist)
        {
            cell = default(XYZ); n = XYZ.up; dist = 0f;
            if (_draft.Empty && (draftOnly || !AnyTask())) return false;
            Vector3 o = r.origin + new Vector3(0.5f, 0.5f, 0.5f), d = r.direction;
            int x = Mathf.FloorToInt(o.x), y = Mathf.FloorToInt(o.y), z = Mathf.FloorToInt(o.z);
            int sx = d.x > 0f ? 1 : -1, sy = d.y > 0f ? 1 : -1, sz = d.z > 0f ? 1 : -1;
            float tdx = Mathf.Abs(d.x) > 1e-6f ? Mathf.Abs(1f / d.x) : float.MaxValue;
            float tdy = Mathf.Abs(d.y) > 1e-6f ? Mathf.Abs(1f / d.y) : float.MaxValue;
            float tdz = Mathf.Abs(d.z) > 1e-6f ? Mathf.Abs(1f / d.z) : float.MaxValue;
            float tx = tdx == float.MaxValue ? float.MaxValue : (d.x > 0f ? (x + 1 - o.x) : (o.x - x)) * tdx;
            float ty = tdy == float.MaxValue ? float.MaxValue : (d.y > 0f ? (y + 1 - o.y) : (o.y - y)) * tdy;
            float tz = tdz == float.MaxValue ? float.MaxValue : (d.z > 0f ? (z + 1 - o.z) : (o.z - z)) * tdz;
            float t = 0f;
            int axis = 1;
            for (int i = 0; i < 3000 && t <= maxDist; i++)
            {
                if (i > 0)
                {
                    XYZ c = new XYZ(x, y, z);
                    if (IsMark(c, draftOnly))
                    {
                        cell = c; dist = t;
                        n = axis == 0 ? new XYZ(-sx, 0, 0) : axis == 1 ? new XYZ(0, -sy, 0) : new XYZ(0, 0, -sz);
                        return true;
                    }
                }
                if (tx < ty && tx < tz) { x += sx; t = tx; tx += tdx; axis = 0; }
                else if (ty < tz) { y += sy; t = ty; ty += tdy; axis = 1; }
                else { z += sz; t = tz; tz += tdz; axis = 2; }
            }
            return false;
        }

        private static bool AnyTask()
        {
            using (IEnumerator<BtcTerraform.Shape> e = BtcTerraform.All().GetEnumerator()) return e.MoveNext();
        }

        private static bool IsMark(XYZ c, bool draftOnly)
        {
            if (_draft.Dig.Contains(c) || _draft.Fill.Contains(c) || _draft.Ladders.ContainsKey(c)) return true;
            return false;   // other tasks' marks are hidden, so they do not catch the cursor either
        }

        private static bool PlaneHit(Ray r, Vector3 point, Vector3 normal, out XYZ cell)
        {
            cell = _cursor;
            float den = Vector3.Dot(r.direction, normal);
            if (Mathf.Abs(den) < 1e-4f) return false;
            float t = Vector3.Dot(point - r.origin, normal) / den;
            if (t <= 0f || t > 2000f) return false;
            Vector3 p = r.origin + r.direction * t;
            cell = new XYZ(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
            return true;
        }

        /// <summary>Parameter t of the point on the line p0 + d*t closest to the ray.</summary>
        private static bool LineParam(Ray r, Vector3 p0, Vector3 d, out float t)
        {
            t = 0f;
            Vector3 w0 = p0 - r.origin, u = r.direction;
            float a = Vector3.Dot(d, d), b = Vector3.Dot(d, u), c = Vector3.Dot(u, u);
            float dd = Vector3.Dot(d, w0), e = Vector3.Dot(u, w0);
            float den = a * c - b * b;
            if (Mathf.Abs(den) < 1e-5f) return false;
            t = (b * e - c * dd) / den;
            return true;
        }

        /// <summary>Cell of the stroke under the cursor: on the plane of the clicked face (the anchor's coordinate along the
        /// face normal stays); a wall stroke stays level (into the hill or along it), Alt: on the wall plane; top / bottom face with Alt:
        /// along the axis straight into (dig) / out of (fill) the face.</summary>
        private static bool DragCell(Camera cam, Vector3 screen, out XYZ cell)
        {
            cell = _cursor;
            Ray r = cam.ScreenPointToRay(screen);
            if (_n.y == 0 && !_depth)
            {
                // wall: the stroke stays level (along the wall or straight into the hill, whichever way the mouse goes);
                // on the wall plane itself a drag towards the hill read as "up" (user 2026-11-15). Alt: up / down the wall
                XYZ h;
                if (!PlaneHit(r, _anchor.ToVector3(), Vector3.up, out h)) return false;
                cell = new XYZ(h.x, _anchor.y, h.z);
                return true;
            }
            if (_depth && _n.y != 0)
            {
                float t;
                if (!LineParam(r, _anchor.ToVector3(), _axis.ToVector3(), out t)) return false;
                int k = Mathf.Clamp(Mathf.RoundToInt(t), 0, 256);
                cell = _anchor + BtcTerraform.Mul(_axis, k);
                return true;
            }
            Vector3 nv = _n.ToVector3();
            float off = Mode == 1 ? -0.5f : 0.5f;   // fill: the anchor is the air cell in front of the face
            XYZ c;
            if (!PlaneHit(r, _anchor.ToVector3() + nv * off, nv, out c)) return false;
            if (_n.x != 0) c = new XYZ(_anchor.x, c.y, c.z);
            else if (_n.y != 0) c = new XYZ(c.x, _anchor.y, c.z);
            else c = new XYZ(c.x, c.y, _anchor.z);
            cell = c;
            return true;
        }

        private static void YRange(int ay, out int y0, out int y1)
        {
            int h = H;
            if (Mode == 2) { y0 = ay - h + 1; y1 = ay + h; return; }
            if (Mode == 0 && _topFace) { y0 = ay - h + 1; y1 = ay; return; }
            y0 = ay; y1 = ay + h - 1;
        }

        private static BtcTerraform.Op Box(XYZ a, XYZ b)
        {
            int w = W, lo = (w - 1) / 2;
            int ya0, ya1, yb0, yb1;
            YRange(a.y, out ya0, out ya1);
            YRange(b.y, out yb0, out yb1);
            int x0 = Math.Min(a.x, b.x) - lo, z0 = Math.Min(a.z, b.z) - lo;
            int x1 = Math.Max(a.x, b.x) - lo + w - 1, z1 = Math.Max(a.z, b.z) - lo + w - 1;
            BtcTerraform.Op o;
            o.K = Kind; o.A = new XYZ(x0, Math.Min(ya0, yb0), z0); o.B = new XYZ(x1, Math.Max(ya1, yb1), z1);
            return o;
        }

        // ------------------------------------------------------------------ drawing (GL, world)
        private static Material _mat;
        private static readonly Color DigCol = new Color(1f, 0.45f, 0.15f, 0.9f);
        private static readonly Color FillCol = new Color(0.35f, 0.75f, 1f, 0.9f);
        private static readonly Color EraseCol = new Color(1f, 1f, 1f, 0.7f);
        private static readonly Color LadCol = new Color(0.45f, 1f, 0.45f, 0.95f);
        private static readonly Color BadCol = new Color(1f, 0.25f, 0.25f, 0.95f);
        private const int DrawBudget = 12000;

        internal static void Render(Camera cam)
        {
            if (!Active || cam == null || cam != Camera.main) return;
            try
            {
                if (!Mat()) return;
                _mat.SetPass(0);
                GL.PushMatrix();
                GL.Begin(GL.LINES);
                int budget = DrawBudget;
                // only the task being placed / edited shows its marks (user 2026-11-26), other tasks stay hidden
                bool edit = _editNid != 0;   // the edited task: only the work still left
                budget = Cells(_draft.Dig, DigCol, budget, edit, false);
                budget = Cells(_draft.Fill, FillCol, budget, false, edit);
                Ladders(_draft, LadCol);
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (Mode == 3)
                {
                    if (_down)
                    {
                        for (int i = 0; i < _chainCells.Count; i++)
                            LadderGlyph(_chainCells[i], LadChar == 'B' ? XYZ.up : _chainF, _chainOk[i] ? LadCol : BadCol, 0.02f);
                        for (int i = 0; i < _chainOps.Count; i++)
                            if (_chainOps[i].K == 'D') { GL.Color(DigCol); XYZ p = _chainOps[i].A; Cube(p.x - 0.5f, p.y - 0.5f, p.z - 0.5f, p.x + 0.5f, p.y + 0.5f, p.z + 0.5f, 0.06f); }
                    }
                    else if (_hasCursor && !ctrl) LadderGlyph(_cursor, _ladF, _ladOk ? LadCol : BadCol, 0.02f);
                }
                else if (_down) Ghost(Box(_anchor, _cursor));
                else if (_hasCursor && !ctrl) Ghost(Box(_cursor, _cursor));
                GL.End();
                GL.PopMatrix();
            }
            catch (Exception e) { BtcLog.Error("Terraform", "Render: " + e.Message); }
        }

        private static int Cells(HashSet<XYZ> set, Color c, int budget, bool onlySolid, bool onlyOpen)
        {
            if (budget <= 0 || set.Count == 0) return budget;
            GL.Color(c);
            foreach (XYZ p in set)
            {
                if (onlySolid && Voxel.IsEmpty(p)) continue;                     // already dug
                if (onlyOpen && !Voxel.IsEmpty(p) && !Voxel.IsCrust(p)) continue; // already filled
                Cube(p.x - 0.5f, p.y - 0.5f, p.z - 0.5f, p.x + 0.5f, p.y + 0.5f, p.z + 0.5f, 0.04f);
                if (--budget <= 0) break;
            }
            return budget;
        }

        private static void Ladders(BtcTerraform.Shape s, Color c)
        {
            foreach (KeyValuePair<XYZ, BtcTerraform.Ladder> kv in s.Ladders)
            {
                if (QuarryGoalProvider2.IsStair(kv.Key)) continue;   // built
                LadderGlyph(kv.Key, kv.Value.F, c, 0.08f);
            }
        }

        // cell box plus a ramp line from the low side (+F, bottom) to the high side (-F, top); a landing is a flat cross
        private static void LadderGlyph(XYZ p, XYZ f, Color c, float inset)
        {
            GL.Color(c);
            Cube(p.x - 0.5f, p.y - 0.5f, p.z - 0.5f, p.x + 0.5f, p.y + 0.5f, p.z + 0.5f, inset);
            if (f.y != 0)
            {
                L(p.x - 0.4f, p.y + 0.45f, p.z - 0.4f, p.x + 0.4f, p.y + 0.45f, p.z + 0.4f);
                L(p.x - 0.4f, p.y + 0.45f, p.z + 0.4f, p.x + 0.4f, p.y + 0.45f, p.z - 0.4f);
                return;
            }
            float fx = f.x * 0.45f, fz = f.z * 0.45f;
            float sx = f.z * 0.3f, sz = f.x * 0.3f;   // sideways offset: two rails
            L(p.x + fx + sx, p.y - 0.45f, p.z + fz + sz, p.x - fx + sx, p.y + 0.45f, p.z - fz + sz);
            L(p.x + fx - sx, p.y - 0.45f, p.z + fz - sz, p.x - fx - sx, p.y + 0.45f, p.z - fz - sz);
            L(p.x - fx + sx, p.y + 0.45f, p.z - fz + sz, p.x - fx - sx, p.y + 0.45f, p.z - fz - sz);
        }

        private static void Ghost(BtcTerraform.Op b)
        {
            GL.Color(Mode == 0 ? DigCol : Mode == 1 ? FillCol : EraseCol);
            Cube(Math.Min(b.A.x, b.B.x) - 0.5f, Math.Min(b.A.y, b.B.y) - 0.5f, Math.Min(b.A.z, b.B.z) - 0.5f,
                 Math.Max(b.A.x, b.B.x) + 0.5f, Math.Max(b.A.y, b.B.y) + 0.5f, Math.Max(b.A.z, b.B.z) + 0.5f, 0.02f);
        }

        private static void Cube(float x0, float y0, float z0, float x1, float y1, float z1, float inset)
        {
            x0 += inset; y0 += inset; z0 += inset; x1 -= inset; y1 -= inset; z1 -= inset;
            L(x0, y0, z0, x1, y0, z0); L(x1, y0, z0, x1, y0, z1); L(x1, y0, z1, x0, y0, z1); L(x0, y0, z1, x0, y0, z0);
            L(x0, y1, z0, x1, y1, z0); L(x1, y1, z0, x1, y1, z1); L(x1, y1, z1, x0, y1, z1); L(x0, y1, z1, x0, y1, z0);
            L(x0, y0, z0, x0, y1, z0); L(x1, y0, z0, x1, y1, z0); L(x1, y0, z1, x1, y1, z1); L(x0, y0, z1, x0, y1, z1);
        }

        private static void L(float ax, float ay, float az, float bx, float by, float bz)
        {
            GL.Vertex3(ax, ay, az); GL.Vertex3(bx, by, bz);
        }

        private static bool Mat()
        {
            if (_mat != null) return true;
            Shader sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null) return false;
            _mat = new Material(sh);
            _mat.hideFlags = HideFlags.HideAndDontSave;
            _mat.SetInt("_SrcBlend", 5);
            _mat.SetInt("_DstBlend", 10);
            _mat.SetInt("_Cull", 0);
            _mat.SetInt("_ZWrite", 0);
            _mat.SetInt("_ZTest", 8);   // always: cells inside a hill stay visible
            return true;
        }

#if DEV
        // DEV labs (tfui): a stroke / undo / Esc without the mouse
        internal static void DevStroke(BtcTerraform.Op o)
        {
            if (!Active) return;
            string before = BtcTerraform.Encode(_draft);
            _strokes.Add(_draft.Ops.Count);
            AddOp(o);
            RememberText(before, "dev");
            Commit();
        }

        internal static void DevUndo() { Undo(); }
        internal static void DevLeave() { Leave(); }
#endif

        // ------------------------------------------------------------------ IMGUI: short notes above the task popup + hover tips of the popup slots
        internal static void OnGUI()
        {
            try
            {
                if (!BtcGameHud.Open) return;
                int fs = BtcUiEdit.MenuFs;
                string tip = BtcGhButton.HoverTip;
                if (tip != null || (_note != null && Time.unscaledTime < _noteUntil)) BtcReplayUI.Ensure(fs);
                if (_note != null && Time.unscaledTime < _noteUntil && Event.current.type == EventType.Repaint)
                {
                    GUIStyle st = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
                    float w = Mathf.Min(_note.Length * fs * 0.6f + fs * 2f, Screen.width - fs * 4f), h = fs * 2.2f;
                    float a = Mathf.Clamp01((_noteUntil - Time.unscaledTime) / 0.4f);
                    Rect r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height - Screen.height * 0.2f - h, w, h);
                    BtcReplayUI.Fill(r, new Color(0.02f, 0.025f, 0.03f, 0.92f * a));
                    BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, r.height), new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, a));
                    Color oc = GUI.contentColor;
                    GUI.contentColor = new Color(1f, 1f, 1f, a);
                    GUI.Label(new Rect(r.x + fs * 0.8f, r.y, r.width - fs * 1.2f, r.height), _note, st);
                    GUI.contentColor = oc;
                }
                if (tip != null)
                {
                    Vector2 m = Event.current.mousePosition;
                    BtcReplayIcons.Hint(new Rect(m.x - 4f, m.y - 4f, 8f, 8f), tip);
                    BtcReplayIcons.DrawTip(fs);
                }
            }
            catch (Exception e) { BtcLog.Error("Terraform", "OnGUI: " + e.Message); }
        }
    }
}
