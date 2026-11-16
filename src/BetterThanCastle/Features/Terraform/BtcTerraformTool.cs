using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Network;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BetterThanCastle
{
    /// <summary>
    /// Client tool of the Terraforming task (docs/terraforming.md): paint a draft shape with brushes, then create the task
    /// on the host (BtcTerraform.RequestCreate). Opened with the Advanced Hotkeys bind "terraform_tool" (Ctrl+G) or a bind per mode.
    /// Modes: dig / fill / erase / ladder. Brushes: block (click), line (hold and drag), box (drag two corners). A drag runs along
    /// the clicked face (top face: flat, Alt: down into the blocks; wall: level, sideways or into the hill, Alt: up / down). Marks act
    /// like blocks for the cursor, so a shape can be continued from its own marks (even inside a hill). Dig and quarry ladders
    /// aim at the full block under a crust; a tunnel ladder goes into the crust. Ladders: RMB / R rotates, a drag lays a chain
    /// up the climbing diagonal. Ctrl+LMB deletes a mark, Ctrl+Z undoes the last stroke. All settings are remembered (BtcMenuState).
    /// </summary>
    internal static class BtcTerraformTool
    {
        internal const string HotkeyId = "terraform_tool";
        private static readonly string[] ModeKeys = { "terraform_dig", null, null, "terraform_ladder" };   // index = Mode (2 = old erase, gone)
        private static readonly KeyCode[] DefKeys = { KeyCode.LeftControl, KeyCode.G };
        private static readonly KeyCode[] NoKeys = new KeyCode[0];

        internal static bool Active;
        private static BtcTerraform.Shape _draft = new BtcTerraform.Shape();
        private static readonly List<int> _strokes = new List<int>();

        // 0 dig 3 ladder (2 was the erase mode: removed, marks are deleted with Ctrl+LMB; 1 fill is TEMPORARILY off in the UI: it hangs workers, needs a fix, see STATUS)
        private static int Mode { get { int m = BtcMenuState.Int("terraform:mode", 0); return m == 2 || m == 1 ? 0 : m; } set { BtcMenuState.SetInt("terraform:mode", value); } }
        private static int LadKind { get { return BtcMenuState.Int("terraform:ladkind", 0); } set { BtcMenuState.SetInt("terraform:ladkind", value); } } // 0 tunnel 1 quarry 2 landing
        private static int LadDir { get { return BtcMenuState.Int("terraform:laddir", 0) & 3; } set { BtcMenuState.SetInt("terraform:laddir", value & 3); } } // climb towards Dirs[i]
        private static readonly XYZ[] Dirs = { new XYZ(0, 0, 1), new XYZ(1, 0, 0), new XYZ(0, 0, -1), new XYZ(-1, 0, 0) };
        private static XYZ _ladF;      // forward of the ladder under the cursor (low side; you climb towards -F)
        private static bool _ladOk;
        private static int W { get { return Mathf.Clamp(BtcMenuState.Int("terraform:w", 2), 1, 16); } set { BtcMenuState.SetInt("terraform:w", Mathf.Clamp(value, 1, 16)); } }
        private static int H { get { return Mathf.Clamp(BtcMenuState.Int("terraform:h", 3), 1, 32); } set { BtcMenuState.SetInt("terraform:h", Mathf.Clamp(value, 1, 32)); } }
        // kept resources: all off by default (nothing drops, no stockpile needed)
        private static int Harvest { get { return BtcMenuState.Int("terraform:keep", 0) & 7; } set { BtcMenuState.SetInt("terraform:keep", value & 7); } }
        private static bool AutoDig { get { return BtcMenuState.Int("terraform:autodig", 1) != 0; } set { BtcMenuState.SetInt("terraform:autodig", value ? 1 : 0); } }

        // stroke state
        private static bool _down, _depth;
        private static XYZ _anchor, _last, _cursor, _n, _axis;
        private static bool _hasCursor, _topFace;
        private static Rect _panel;
        private static string _note;
        private static float _noteUntil;

        // ladder chain (drag in ladder mode)
        private static XYZ _chainF, _chainFlat;
        private static int _chainN = int.MinValue;
        private static readonly List<BtcTerraform.Op> _chainOps = new List<BtcTerraform.Op>();
        private static readonly List<XYZ> _chainCells = new List<XYZ>();
        private static readonly List<bool> _chainOk = new List<bool>();

        private static bool InMatch
        {
            get { return User.LocalUser != null && User.LocalUser.faction != null && Camera.main != null && !BtcReplay.Active; }
        }

        // ------------------------------------------------------------------ input (Update)
        internal static void Tick()
        {
            try
            {
                if (!InMatch) { if (Active) Close(); return; }
                if (!BtcHotkeys.MenuOpen)
                {
                    // the binds always start a NEW task (an existing one is edited by selecting it in the vanilla task list)
                    if (BtcHotkeys.ComboPressed(BtcHotkeys.GetKeys(HotkeyId, DefKeys)))
                    {
                        if (Active && _editNid == 0) Close(); else OpenNew();
                        return;
                    }
                    for (int i = 0; i < ModeKeys.Length; i++)
                    {
                        if (ModeKeys[i] == null) continue;
                        KeyCode[] k = BtcHotkeys.GetKeys(ModeKeys[i], NoKeys);
                        if (k.Length == 0 || !BtcHotkeys.ComboPressed(k)) continue;
                        if (Active && Mode == i && _editNid == 0) Close();
                        else { if (!Active || _editNid != 0) OpenNew(); if (Mode != i) { Mode = i; BtcSfx.Click(); } }
                        return;
                    }
                }
                // selecting one of the own terraform tasks the vanilla way opens it here for editing
                uint selNid; BtcTerraform.Shape selS;
                bool sel = BtcTerraform.SelectedOwn(out selNid, out selS);
                if (sel && selNid != _lastSel && selNid != _editNid)
                {
                    if (_draft.Empty || _editNid != 0) BeginEdit(selNid, selS);
                    else { if (!Active) Open(); Note(BtcLang.Tr("Create or clear your marks first, then pick the task with Edit a task")); }
                }
                _lastSel = sel ? selNid : 0;
                if (!Active) return;
                if (Input.GetKeyDown(KeyCode.Escape)) { if (_down) Cancel(); else Close(); return; }
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                if (ctrl && Input.GetKeyDown(KeyCode.Z)) { Undo(); return; }
                if (Mode == 3 && !ctrl && Input.GetKeyDown(KeyCode.R)) Rotate(shift);
                float wheel = Input.GetAxis("Mouse ScrollWheel");
                if (wheel != 0f && (ctrl || shift))
                {
                    int d = wheel > 0f ? 1 : -1;
                    if (shift) H = H + d; else W = W + d;
                }
                Camera cam = Camera.main;
                Vector3 mouse = Input.mousePosition;
                Vector2 gui = new Vector2(mouse.x, Screen.height - mouse.y);
                bool overUi = _panel.Contains(gui) || BtcHotkeys.MenuOpen || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
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
                if (_picking)
                {
                    if (!overUi && Input.GetMouseButtonDown(0)) PickTask(cam, mouse);
                    if (Input.GetMouseButtonDown(1)) _picking = false;
                    return;
                }
                if (overUi) return;
                if (ctrl && Input.GetMouseButtonDown(0)) { DeleteAt(cam, mouse); return; }
                if (Mode == 3)
                {
                    if (_hasCursor) LadderAtCursor();
                    if (Input.GetMouseButtonDown(1)) Rotate(shift);
                    if (_hasCursor && Input.GetMouseButtonDown(0))
                    {
                        _anchor = _cursor; _down = true; _chainF = _ladF; _chainFlat = new XYZ(1, 0, 0);
                        _chainN = int.MinValue;
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

        private static void Open()
        {
            Active = true;
            _down = false;
            if (!BtcTerraform.On) Note(BtcLang.Tr("Terraforming needs BetterConnection (BC tab)"));
            BtcSfx.Click();
        }

        // tool for a new task: leaves the editing of an existing task (its unsaved changes are dropped), keeps a new-task draft
        private static void OpenNew()
        {
            if (_editNid != 0) StopEditing();
            if (!Active) Open();
        }

        internal static void Close()
        {
            Active = false;
            _down = false;
            _hasCursor = false;
            _picking = false;
        }

        private static void Note(string text) { _note = text; _noteUntil = Time.unscaledTime + 4f; }

        private static void Rotate(bool back)
        {
            LadDir = LadDir + (back ? 3 : 1);
            BtcSfx.Click();
        }

        private static void EndStroke()
        {
            if (Mode == 3)
            {
                _down = false;
                if (_chainOps.Count == 0)
                {
                    Note(LadChar == 'T' ? BtcLang.Tr("A tunnel ladder needs a full block under it and a wall behind it")
                                        : BtcLang.Tr("A quarry ladder replaces a full terrain block that stands on something"));
                    return;
                }
                _strokes.Add(_draft.Ops.Count);
                for (int i = 0; i < _chainOps.Count; i++)
                {
                    int before = _draft.Ops.Count;
                    AddOp(_chainOps[i]);
                    if (_draft.Ops.Count == before) break;   // too big
                }
                _chainOps.Clear(); _chainCells.Clear(); _chainOk.Clear();
                BtcSfx.Click();
                return;
            }
            _down = false;
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

        private static BtcTerraform.Shape _cleared;
        private static List<int> _clearedStrokes;

        private static void Undo()
        {
            if (_strokes.Count == 0)
            {
                if (_cleared == null || !_draft.Empty) return;
                _draft = _cleared; _strokes.AddRange(_clearedStrokes); _cleared = null;
                BtcSfx.Click();
                return;
            }
            Truncate(_strokes[_strokes.Count - 1]);
            _strokes.RemoveAt(_strokes.Count - 1);
            BtcSfx.Click();
        }

        private static void Truncate(int count)
        {
            if (count < _draft.Ops.Count) _draft.Ops.RemoveRange(count, _draft.Ops.Count - count);
            _draft.Rebuild();
        }

        private static void AddOp(BtcTerraform.Op o)
        {
            if (_draft.Ops.Count >= BtcTerraform.MaxOps) { Note(BtcLang.Tr("The shape is too big")); _down = false; return; }
            _draft.Ops.Add(o);
            if (!_draft.Rebuild())
            {
                _draft.Ops.RemoveAt(_draft.Ops.Count - 1);
                _draft.Rebuild();
                Note(BtcLang.Tr("The shape is too big"));
                _down = false;
            }
        }

        private static void ClearDraft()
        {
            if (_draft.Empty) return;
            _cleared = _draft; _clearedStrokes = new List<int>(_strokes);
            _strokes.Clear(); _draft = new BtcTerraform.Shape();
            Note(BtcLang.Tr("Shape cleared (Ctrl+Z brings it back)"));
            BtcSfx.Click();
        }

        // ------------------------------------------------------------------ delete one mark (Ctrl+LMB)
        private static void DeleteAt(Camera cam, Vector3 screen)
        {
            Ray r = cam.ScreenPointToRay(screen);
            XYZ cell, n; float d;
            if (MarkRay(r, 600f, true, out cell, out n, out d))
            {
                _strokes.Add(_draft.Ops.Count);
                AddOp(BtcTerraform.Cell('E', cell));
                BtcSfx.Click();
                return;
            }
            // a mark of a task already created: open that task for editing with the cell removed
            uint nid; BtcTerraform.Shape s;
            if (_editNid == 0 && MarkRay(r, 600f, false, out cell, out n, out d) && BtcTerraform.FindOwn(cell, cell, out nid, out s))
            {
                BeginEdit(nid, s);
                _strokes.Add(_draft.Ops.Count);
                AddOp(BtcTerraform.Cell('E', cell));
                return;
            }
            Note(BtcLang.Tr("No mark here"));
        }

        // ------------------------------------------------------------------ edit an existing task
        private static bool _picking;
        private static uint _editNid, _lastSel;

        private static void PickTask(Camera cam, Vector3 screen)
        {
            Ray ray = cam.ScreenPointToRay(screen);
            XYZ a = default(XYZ), b = default(XYZ), n; float md;
            bool mark = MarkRay(ray, 600f, false, out a, out n, out md);
            if (mark) b = a;
            else
            {
                RaycastHit hit;
                if (!Physics.Raycast(ray, out hit, 1000f, 132096, QueryTriggerInteraction.Ignore)) return;
                Vector3 pi = hit.point - hit.normal * 0.05f, po = hit.point + hit.normal * 0.05f;
                a = new XYZ(Mathf.RoundToInt(pi.x), Mathf.RoundToInt(pi.y), Mathf.RoundToInt(pi.z));
                b = new XYZ(Mathf.RoundToInt(po.x), Mathf.RoundToInt(po.y), Mathf.RoundToInt(po.z));
            }
            uint nid; BtcTerraform.Shape s;
            if (!BtcTerraform.FindOwn(a, b, out nid, out s)) { Note(BtcLang.Tr("No terraform task of yours here")); return; }
            BeginEdit(nid, s);
        }

        /// <summary>Loads a task of the local faction into the draft (Save changes sends it back to the host).</summary>
        internal static void BeginEdit(uint nid, BtcTerraform.Shape s)
        {
            if (!Active) Open();
            _picking = false;
            _down = false;
            _editNid = nid;
            _draft = s.Clone();
            _strokes.Clear();
            Harvest = s.Harvest;
            Note(BtcLang.Tr("Editing the task: change it, then Save changes"));
            BtcSfx.Click();
        }

        internal static uint EditingTask { get { return Active ? _editNid : 0; } }

        private static void StopEditing()
        {
            _editNid = 0;
            _draft = new BtcTerraform.Shape();
            _strokes.Clear();
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
            if (k == 'B')
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
            if (n != _chainN || !flat.Equals(_chainFlat)) { _chainFlat = flat; SetChain(n); }
        }

        private static void SetChain(int n)
        {
            _chainN = n;
            BtcTerraform.LadderChain(_draft, _anchor, LadChar, _chainF, _chainFlat, n, AutoDig && LadChar != 'B', _chainOps, _chainCells, _chainOk);
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
            if (draftOnly) return false;
            foreach (BtcTerraform.Shape s in BtcTerraform.All())
            {
                if (s.Dig.Contains(c)) { if (!Voxel.IsEmpty(c)) return true; }
                else if (s.Fill.Contains(c)) { if (Voxel.IsEmpty(c) || Voxel.IsCrust(c)) return true; }
                else if (s.Ladders.ContainsKey(c) && !QuarryGoalProvider2.IsStair(c)) return true;
            }
            return false;
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
                foreach (BtcTerraform.Shape s in BtcTerraform.All())
                {
                    budget = Cells(s.Dig, new Color(DigCol.r, DigCol.g, DigCol.b, 0.35f), budget, true, false);
                    budget = Cells(s.Fill, new Color(FillCol.r, FillCol.g, FillCol.b, 0.35f), budget, false, true);
                }
                budget = Cells(_draft.Dig, DigCol, budget, false, false);
                budget = Cells(_draft.Fill, FillCol, budget, false, false);
                foreach (BtcTerraform.Shape s in BtcTerraform.All()) Ladders(s, new Color(LadCol.r, LadCol.g, LadCol.b, 0.45f));
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

        // ------------------------------------------------------------------ panel (IMGUI)
        private static Vector2 _scroll;

        internal static void OnGUI()
        {
            if (!Active) { _panel = default(Rect); return; }
            try
            {
                int fs = BtcUiEdit.MenuFs;
                BtcReplayUI.Ensure(fs);
                float w = Mathf.Round(fs * 21f);
                float noteH = fs * 2.2f + 4f;
                float top = fs * 4f, avail = Mathf.Max(fs * 8f, Screen.height - top - noteH - fs);
                float h = Mathf.Min(Mathf.Max(_panelH, fs * 10f), avail);
                // centred on the right edge, but never below the screen (the panel used to slide off the bottom)
                float y = Mathf.Clamp(Screen.height * 0.5f - h * 0.5f, top, Screen.height - h - noteH - fs);
                _panel = new Rect(Screen.width - w - fs, Mathf.Max(top, y), w, h);
                BtcReplayUI.Fill(_panel, BtcMenu.Bg);
                BtcReplayUI.Fill(new Rect(_panel.x, _panel.y, _panel.width, 2f), BtcMenu.Accent);
                GUI.BeginGroup(_panel);
                if (_panelH > h + 1f)
                {
                    float sw = 14f;
                    _scroll = GUI.BeginScrollView(new Rect(0f, 0f, w, h), _scroll, new Rect(0f, 0f, w - sw, _panelH), false, true);
                    _panelH = Panel(w - sw, fs);
                    GUI.EndScrollView();
                }
                else _panelH = Panel(w, fs);
                GUI.EndGroup();
                if (_note != null && Time.unscaledTime < _noteUntil)
                {
                    Rect nr = new Rect(_panel.x, Mathf.Min(_panel.yMax + 4f, Screen.height - noteH), _panel.width, fs * 2.2f);
                    BtcReplayUI.Fill(nr, BtcMenu.Bg);
                    GUI.BeginGroup(nr);
                    float ny = fs * 0.2f;
                    BtcUi.Status(ref ny, nr.width, fs, _note, BtcMenu.Accent);
                    GUI.EndGroup();
                }
                BtcReplayIcons.DrawTip(fs);
                if (Event.current.isMouse && _panel.Contains(Event.current.mousePosition)) Event.current.Use();
            }
            catch (Exception e) { BtcLog.Error("Terraform", "OnGUI: " + e.Message); }
        }

        private static float _panelH;

        // menu-style content (BtcUi rows) in panel-local coordinates; returns the used height
        private static float Panel(float w, int fs)
        {
            float pad = fs, y = fs * 0.7f;
            BtcReplayIcons.Glyph(BtcReplayIcons.Grid, new Rect(pad, y + fs * 0.25f, fs * 1.3f, fs * 1.3f), BtcMenu.Accent);
            GUI.Label(new Rect(pad + fs * 1.9f, y, w - fs * 6f, fs * 1.8f), BtcLang.Tr("Terraforming"), Title(fs));
            if (BtcReplayIcons.Plain(new Rect(w - pad - fs * 1.6f, y + fs * 0.1f, fs * 1.6f, fs * 1.6f), BtcReplayIcons.Cross, new Color(1f, 1f, 1f, 0.8f), BtcLang.Tr("Close (Esc)"))) Close();
            y += fs * 2.4f;
            if (!BtcTerraform.On) BtcUi.Status(ref y, w, fs, BtcLang.Tr("Terraforming needs BetterConnection (BC tab)"), new Color(1f, 0.55f, 0.3f, 1f));

            int cur = Mode == 3 ? 2 : Mode;
            int m = Chips(ref y, w, fs, cur == 2 ? 1 : 0, BtcLang.Tr("Dig"), BtcLang.Tr("Ladder"));
            if (m != (cur == 2 ? 1 : 0)) Mode = m == 1 ? 3 : 0;
            if (Mode == 3)
            {
                int k = Chips(ref y, w, fs, LadKind, BtcLang.Tr("Tunnel"), BtcLang.Tr("Quarry"), BtcLang.Tr("Landing"));
                if (k != LadKind) LadKind = k;
                if (LadKind != 2 && BtcUi.Action(ref y, w, fs, BtcReplayIcons.Refresh, BtcLang.Tr("Rotate (RMB or R)"), null)) Rotate(false);
                if (LadKind != 2 && BtcUi.Toggle(ref y, w, fs, BtcLang.Tr("Dig room for ladders"), AutoDig, BtcLang.Tr("Marks the block above a ladder (and a tunnel ladder's own block) for digging")))
                    AutoDig = !AutoDig;
            }
            else
            {
                int d = BtcUi.Stepper(ref y, w, fs, BtcLang.Tr("Width"), W.ToString()); if (d != 0) W = W + d;
                d = BtcUi.Stepper(ref y, w, fs, BtcLang.Tr("Height"), H.ToString()); if (d != 0) H = H + d;
            }

            y += fs * 0.3f;
            BtcUi.Section(ref y, w, fs, BtcLang.Tr("Keep dug resources"));
            int hv = Harvest;
            int t = MultiChips(ref y, w, fs, hv, BtcLang.Tr("Stone"), BtcLang.Tr("Brimstone"), BtcLang.Tr("Iron"));
            if (t != hv) Harvest = t;
            BtcUi.Note(ref y, w, fs, Harvest == 0 ? BtcLang.Tr("Nothing drops: no stockpile needed") : BtcLang.Tr("Kept stone goes straight into the fill; a full bag with nothing to fill leaves the task"));

            y += fs * 0.3f;
            BtcUi.Section(ref y, w, fs, BtcLang.Tr("Task"));
            BtcUi.Status(ref y, w, fs, string.Format(BtcLang.Tr("Dig {0}, fill {1}, ladders {2}"), _draft.Dig.Count, _draft.Fill.Count, _draft.Ladders.Count), new Color(1f, 1f, 1f, 0.75f));
            bool can = BtcTerraform.On && !_draft.Empty;
            Color old = GUI.color;
            if (_editNid != 0) BtcUi.Status(ref y, w, fs, BtcLang.Tr("Editing a task"), BtcMenu.Accent);
            else if (_picking) BtcUi.Status(ref y, w, fs, BtcLang.Tr("Click a cell of one of your terraform tasks (RMB cancels)"), BtcMenu.Accent);
            if (!can) GUI.color = new Color(1f, 1f, 1f, 0.4f);
            if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Check, _editNid != 0 ? BtcLang.Tr("Save changes") : BtcLang.Tr("Create task"), null) && can) Create();
            GUI.color = old;
            // editing an existing task: select it in the vanilla task list; Ctrl+G always starts a new one
            int act;
            if (_editNid != 0) act = Buttons(ref y, w, fs, BtcLang.Tr("Stop editing"), BtcLang.Tr("Clear all marks"));
            else act = Buttons(ref y, w, fs, BtcLang.Tr("Clear all marks")) == 0 ? 1 : -1;
            if (act == 0) StopEditing();
            else if (act == 1) ClearDraft();
            y += fs * 0.2f;
            BtcUi.Note(ref y, w, fs, BtcLang.Tr("LMB hold and drag: a straight line along the face, Alt: into the blocks; ladders: drag up or down the slope. Ctrl+LMB deletes a mark, RMB rotates a ladder, Ctrl+Z undo, Shift / Ctrl+wheel height / width, Esc close. Edit a task: select it in the task list"));
            return y + fs * 0.4f;
        }

        private static GUIStyle _title;
        private static int _titleFs;

        private static GUIStyle Title(int fs)
        {
            if (_title != null && _titleFs == fs) return _title;
            _titleFs = fs;
            _title = new GUIStyle(BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel);
            _title.fontSize = Mathf.RoundToInt(fs * 1.15f);
            _title.fontStyle = FontStyle.Bold;
            _title.alignment = TextAnchor.MiddleLeft;
            _title.normal.textColor = Color.white;
            return _title;
        }

        private static GUIStyle _chip;
        private static int _chipFs;

        private static GUIStyle ChipStyle(int fs)
        {
            if (_chip == null || _chipFs != fs)
            {
                _chipFs = fs;
                _chip = new GUIStyle(BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel);
                _chip.fontSize = Mathf.RoundToInt(fs * 0.9f);
                _chip.alignment = TextAnchor.MiddleCenter;
                _chip.clipping = TextClipping.Clip;
            }
            return _chip;
        }

        // one flat cell in the menu look; returns true when clicked
        private static bool Chip(Rect c, string text, bool sel)
        {
            bool hover = c.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(c, sel ? BtcMenu.AccentA(0.22f) : new Color(1f, 1f, 1f, hover ? 0.08f : 0.04f));
            if (sel) BtcReplayUI.Fill(new Rect(c.x, c.yMax - 2f, c.width, 2f), BtcMenu.Accent);
            Color oc = GUI.contentColor;
            GUI.contentColor = sel ? BtcMenu.Accent : new Color(1f, 1f, 1f, hover ? 1f : 0.75f);
            GUI.Label(c, text, _chip);
            GUI.contentColor = oc;
            return BtcSfx.Btn(c, "", BtcReplayUI.sBtn);
        }

        private static Rect ChipRect(float y, float w, int fs, int i, int count)
        {
            float pad = fs, gap = 3f;
            float cw = (w - pad * 2f - gap * (count - 1)) / count;
            return new Rect(pad + (cw + gap) * i, y, cw, fs * 2f);
        }

        /// <summary>Segmented row in the menu look: flat cells, the selected one in accent. Returns the selected index.</summary>
        private static int Chips(ref float y, float w, int fs, int cur, params string[] items)
        {
            ChipStyle(fs);
            for (int i = 0; i < items.Length; i++)
                if (Chip(ChipRect(y, w, fs, i, items.Length), items[i], i == cur) && i != cur) { cur = i; BtcSfx.Click(); }
            y += fs * 2.5f;
            return cur;
        }

        /// <summary>Row of independent on / off cells over a bit mask (bit i = item i). Returns the new mask.</summary>
        private static int MultiChips(ref float y, float w, int fs, int mask, params string[] items)
        {
            ChipStyle(fs);
            for (int i = 0; i < items.Length; i++)
            {
                bool on = (mask & (1 << i)) != 0;
                if (Chip(ChipRect(y, w, fs, i, items.Length), items[i], on)) { mask ^= 1 << i; BtcSfx.Switch(!on); }
            }
            y += fs * 2.5f;
            return mask;
        }

        /// <summary>Row of plain buttons. Returns the clicked index or -1.</summary>
        private static int Buttons(ref float y, float w, int fs, params string[] items)
        {
            ChipStyle(fs);
            int hit = -1;
            for (int i = 0; i < items.Length; i++)
                if (Chip(ChipRect(y, w, fs, i, items.Length), items[i], false)) { hit = i; BtcSfx.Click(); }
            y += fs * 2.5f;
            return hit;
        }

        private static void Create()
        {
            BtcTerraform.Shape s = _draft.Clone();
            s.Harvest = Harvest;
            bool edit = _editNid != 0;
            if (edit) BtcTerraform.RequestEdit(_editNid, s); else BtcTerraform.RequestCreate(s);
            _editNid = 0;
            _draft = new BtcTerraform.Shape();
            _strokes.Clear();
            Note(edit ? BtcLang.Tr("Task changed") : BtcLang.Tr("Terraforming task created"));
            BtcSfx.Click();
        }
    }
}
