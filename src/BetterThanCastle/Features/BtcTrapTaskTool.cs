using System;
using System.Collections.Generic;
using System.Text;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Network;
using Brix.Network;
using Brix.UI.GameSelector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BetterThanCastle
{
    /// <summary>
    /// Mouse tool of the Bear traps task (BtcTrapTask), C# game menu only. "Bear traps" in New task starts placing an own task: click
    /// a trap or drag a frame over traps and the task is made with them (it is selected then). While an own trap task is selected:
    /// click / frame adds traps to it (they leave their old task), Ctrl+click / Ctrl+frame takes them out (back to the automatic
    /// task); on the automatic task a click brings traps back from own tasks. Ctrl+Z undoes the last change, Esc leaves.
    /// Drawn: a square under every own trap (task colour), fired traps orange, the frame while dragging.
    /// </summary>
    internal static class BtcTrapTaskTool
    {
        internal static bool Active;
        private static uint _editNid;     // 0 = placing a new task
        private static uint _leaveNid;
        private static bool _down;
        private static Vector2 _from;     // GUI-space mouse at press (y down)
        private static uint _hover;
        private static float _awaitUntil;
        private static string _awaitKey;  // sorted trap list of a create request: the new task with exactly these traps gets selected
        private static string _note;
        private static float _noteUntil;
        private static readonly List<string> _undo = new List<string>();   // inverse requests, newest last
        private static readonly List<uint> _pick = new List<uint>();
        private static readonly List<BtcTrapTask.Info> _own = new List<BtcTrapTask.Info>();

        private static readonly Color Mine = new Color(0.35f, 0.9f, 0.55f, 0.95f);
        private static readonly Color Other = new Color(0.75f, 0.75f, 0.8f, 0.55f);
        private static readonly Color Fired = new Color(1f, 0.55f, 0.15f, 1f);
        private static readonly Color HoverCol = new Color(1f, 1f, 1f, 1f);
        private static Material _mat;

        private static bool InMatch
        {
            get { return User.LocalUser != null && User.LocalUser.faction != null && Camera.main != null && !BtcReplay.Active && BtcGameHud.Open; }
        }

        internal static bool Placing { get { return Active && _editNid == 0; } }

        private static int MyFaction { get { return User.LocalUser != null && User.LocalUser.faction != null ? User.LocalUser.faction.ID : -1; } }

        private static void Note(string t) { _note = t; _noteUntil = Time.unscaledTime + 4f; }

        // ------------------------------------------------------------------ open / close
        internal static void OpenNew()
        {
            if (!InMatch) return;
            if (!BtcTrapTask.On) { Note(BtcLang.Tr("Bear traps task: needs BetterConnection and the host rule")); BtcSfx.Miss(); return; }
            BtcTerraformTool.Close();
            BtcTrapTask.Info sel = BtcTrapTask.SelectedOwn();
            if (sel != null) _leaveNid = sel.Nid;
            if (!BtcGhLeft.IsIdle) SelectIdle();
            _editNid = 0; _down = false; _awaitKey = null;
            Active = true;
            BtcSfx.Click();
            Note(BtcLang.Tr("Click traps or drag a frame over them to make a task"));
        }

        internal static void Close()
        {
            Active = false; _down = false; _editNid = 0; _hover = 0;
        }

        private static void Leave()
        {
            if (_editNid != 0) { _leaveNid = _editNid; SelectIdle(); }
            Close();
        }

        private static void SelectIdle()
        {
            try { IdleProject idle = IdleProject.GetAlliedProject(); if (idle != null) UIGameSelector.Select(idle.gameObject); }
            catch (Exception e) { BtcLog.Error("TrapTask", "select idle: " + e.Message); }
        }

        // ------------------------------------------------------------------ input (Update)
        internal static void Tick()
        {
            try
            {
                if (!InMatch || !BtcTrapTask.On) { if (Active) Close(); return; }
                BtcTrapTask.Info sel = BtcTrapTask.SelectedOwn();
                if (sel == null) _leaveNid = 0;
                else if (sel.Nid == _leaveNid) sel = null;
                if (_awaitKey != null) AdoptCreated();
                if (sel != null) { if (!Active || _editNid != sel.Nid) { Active = true; _editNid = sel.Nid; _down = false; } }
                else if (Active && _editNid != 0) Close();
                else if (Placing && !BtcGhLeft.IsIdle && _awaitKey == null) Close();
                if (!Active) return;
                if (Input.GetKeyDown(KeyCode.Escape)) { if (_down) _down = false; else Leave(); return; }
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (ctrl && Input.GetKeyDown(KeyCode.Z)) { Undo(); return; }
                Camera cam = Camera.main;
                Vector3 m = Input.mousePosition;
                Vector2 gm = new Vector2(m.x, Screen.height - m.y);
                bool overUi = BtcHotkeys.MenuOpen || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
                _hover = overUi ? 0u : Nearest(cam, gm, 28f);
                if (_down)
                {
                    if (Input.GetMouseButtonDown(1)) { _down = false; return; }
                    if (!Input.GetMouseButtonUp(0)) return;
                    _down = false;
                    _pick.Clear();
                    if ((gm - _from).sqrMagnitude < 36f) { if (_hover != 0) _pick.Add(_hover); }
                    else InFrame(cam, FrameRect(gm));
                    if (_pick.Count == 0) { BtcSfx.Miss(); return; }
                    Apply(ctrl);
                    return;
                }
                if (!overUi && Input.GetMouseButtonDown(0)) { _down = true; _from = gm; }
            }
            catch (Exception e) { BtcLog.Error("TrapTask", "Tool.Tick: " + e.Message); }
        }

        private static Rect FrameRect(Vector2 to)
        {
            return Rect.MinMaxRect(Mathf.Min(_from.x, to.x), Mathf.Min(_from.y, to.y), Mathf.Max(_from.x, to.x), Mathf.Max(_from.y, to.y));
        }

        private static bool Screen2(Camera cam, Vector3 world, out Vector2 g)
        {
            Vector3 s = cam.WorldToScreenPoint(world);
            g = new Vector2(s.x, Screen.height - s.y);
            return s.z > 0f;
        }

        private static uint Nearest(Camera cam, Vector2 gm, float px)
        {
            uint best = 0; float bd = px * px;
            int f = MyFaction;
            List<BtcTrapTask.Trap> all = BtcTrapTask.Traps;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Faction != f) continue;
                Vector2 g;
                if (!Screen2(cam, all[i].Pos, out g)) continue;
                float d = (g - gm).sqrMagnitude;
                if (d < bd) { bd = d; best = all[i].Nid; }
            }
            return best;
        }

        private static void InFrame(Camera cam, Rect r)
        {
            int f = MyFaction;
            List<BtcTrapTask.Trap> all = BtcTrapTask.Traps;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Faction != f) continue;
                Vector2 g;
                if (Screen2(cam, all[i].Pos, out g) && r.Contains(g)) _pick.Add(all[i].Nid);
            }
        }

        private static string List(List<uint> traps)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < traps.Count; i++) { if (i > 0) sb.Append(' '); sb.Append(traps[i]); }
            return sb.ToString();
        }

        private static void Apply(bool remove)
        {
            // undo: every trap goes back to the task it was in
            StringBuilder inv = new StringBuilder();
            for (int i = 0; i < _pick.Count; i++)
            {
                BtcTrapTask.Info was = BtcTrapTask.TaskOfTrap(_pick[i]);
                if (was == null) continue;
                if (inv.Length > 0) inv.Append('\n');
                inv.Append("A ").Append(was.Nid).Append(' ').Append(_pick[i]);
            }
            if (_editNid == 0)
            {
                _pick.Sort();
                _awaitKey = List(_pick);
                _awaitUntil = Time.unscaledTime + 10f;
                BtcTrapTask.Send("C " + _awaitKey);
            }
            else
            {
                BtcTrapTask.Info cur;
                if (!BtcTrapTask.ByNet.TryGetValue(_editNid, out cur)) return;
                if (remove && cur.Auto) { Note(BtcLang.Tr("Traps leave the automatic task by going into an own task")); BtcSfx.Miss(); return; }
                BtcTrapTask.Send((remove ? "R " : "A ") + _editNid + " " + List(_pick));
            }
            if (inv.Length > 0) { _undo.Add(inv.ToString()); if (_undo.Count > 200) _undo.RemoveAt(0); }
            BtcSfx.Click();
        }

        private static void Undo()
        {
            if (_undo.Count == 0) { BtcSfx.Miss(); return; }
            string s = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            foreach (string line in s.Split('\n'))
            {
                string[] p = line.Split(' ');
                uint nid;
                // the old task may be gone (an emptied own task): the trap goes to the automatic task
                if (p.Length == 3 && uint.TryParse(p[1], out nid) && !BtcTrapTask.ByNet.ContainsKey(nid))
                {
                    BtcTrapTask.Info a = BtcTrapTask.AutoOf(MyFaction);
                    if (a == null) continue;
                    BtcTrapTask.Send("A " + a.Nid + " " + p[2]);
                }
                else BtcTrapTask.Send(line);
            }
            Note(BtcLang.Tr("Undo: bear traps task"));
        }

        // a create request was answered: select the new task (exactly the requested traps)
        private static void AdoptCreated()
        {
            if (Time.unscaledTime > _awaitUntil) { _awaitKey = null; Note(BtcLang.Tr("The host did not create the task")); return; }
            BtcTrapTask.OwnTasks(_own);
            for (int i = 0; i < _own.Count; i++)
            {
                BtcTrapTask.Info e = _own[i];
                if (e.Auto || e.Go == null) continue;
                _pick.Clear();
                foreach (uint t in e.Traps) _pick.Add(t);
                _pick.Sort();
                if (List(_pick) != _awaitKey) continue;
                _awaitKey = null;
                try { UIGameSelector.Select(e.Go); } catch (Exception ex) { BtcLog.Error("TrapTask", "select new: " + ex.Message); }
                return;
            }
        }

        // ------------------------------------------------------------------ draw (BtcOverlays render)
        internal static void Render(Camera cam)
        {
            if (!Active || cam == null || cam != Camera.main || BtcTrapTask.Traps.Count == 0) return;
            try
            {
                if (!Mat()) return;
                _mat.SetPass(0);
                GL.PushMatrix();
                GL.Begin(GL.LINES);
                int f = MyFaction;
                BtcTrapTask.Info cur = null;
                if (_editNid != 0) BtcTrapTask.ByNet.TryGetValue(_editNid, out cur);
                List<BtcTrapTask.Trap> all = BtcTrapTask.Traps;
                for (int i = 0; i < all.Count; i++)
                {
                    BtcTrapTask.Trap t = all[i];
                    if (t.Faction != f) continue;
                    bool inCur = cur != null && cur.Traps.Contains(t.Nid);
                    GL.Color(t.Nid == _hover ? HoverCol : inCur ? Mine : Other);
                    Square(t.Pos, inCur ? 0.62f : 0.5f);
                    if (t.Fired) { GL.Color(Fired); Square(t.Pos, 0.3f); }
                }
                GL.End();
                GL.PopMatrix();
            }
            catch (Exception e) { BtcLog.Error("TrapTask", "Render: " + e.Message); }
        }

        private static void Square(Vector3 p, float h)
        {
            float y = Mathf.Floor(p.y) + 0.06f;
            float x0 = p.x - h, x1 = p.x + h, z0 = p.z - h, z1 = p.z + h;
            GL.Vertex3(x0, y, z0); GL.Vertex3(x1, y, z0);
            GL.Vertex3(x1, y, z0); GL.Vertex3(x1, y, z1);
            GL.Vertex3(x1, y, z1); GL.Vertex3(x0, y, z1);
            GL.Vertex3(x0, y, z1); GL.Vertex3(x0, y, z0);
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
            _mat.SetInt("_ZTest", 8);
            return true;
        }

        // ------------------------------------------------------------------ IMGUI: frame + short notes
        internal static void OnGUI()
        {
            try
            {
                if (!BtcGameHud.Open) return;
                bool note = _note != null && Time.unscaledTime < _noteUntil;
                if (!note && !(Active && _down)) return;
                if (Event.current.type != EventType.Repaint) return;
                int fs = BtcUiEdit.MenuFs;
                BtcReplayUI.Ensure(fs);
                if (Active && _down)
                {
                    Vector2 m = Event.current.mousePosition;
                    Rect r = FrameRect(m);
                    bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    Color c = ctrl ? new Color(1f, 0.4f, 0.3f, 1f) : Mine;
                    BtcReplayUI.Fill(r, new Color(c.r, c.g, c.b, 0.12f));
                    BtcReplayUI.Fill(new Rect(r.x, r.y, r.width, 1f), c); BtcReplayUI.Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
                    BtcReplayUI.Fill(new Rect(r.x, r.y, 1f, r.height), c); BtcReplayUI.Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
                }
                if (note)
                {
                    GUIStyle st = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
                    float w = Mathf.Min(_note.Length * fs * 0.6f + fs * 2f, Screen.width - fs * 4f), h = fs * 2.2f;
                    float a = Mathf.Clamp01((_noteUntil - Time.unscaledTime) / 0.4f);
                    Rect r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height - Screen.height * 0.2f - h * 2.4f, w, h);
                    BtcReplayUI.Fill(r, new Color(0.02f, 0.025f, 0.03f, 0.92f * a));
                    BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, r.height), new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, a));
                    Color oc = GUI.contentColor;
                    GUI.contentColor = new Color(1f, 1f, 1f, a);
                    GUI.Label(new Rect(r.x + fs * 0.8f, r.y, r.width - fs * 1.2f, r.height), _note, st);
                    GUI.contentColor = oc;
                }
            }
            catch (Exception e) { BtcLog.Error("TrapTask", "OnGUI: " + e.Message); }
        }
    }
}
