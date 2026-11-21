using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Engine;
using Brix.Game.Components;
using Brix.Lua;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Custom base shield boxes made in the map editor: an axis-aligned box (voxel bounds) bound to one crystal (a player spawn crystal or a shard).
    /// While the shield rule is on, the barrier of that crystal is this box instead of the automatic cube/sphere (BtcShield.RefreshNests).
    /// Stored inside the save folder (btc-shieldboxes.txt: "cx cy cz x0 y0 z0 x1 y1 z1" = crystal position + inclusive voxel bounds), so it travels
    /// with the map; clients and replays get the boxes from the host in msg 109. Editor tool: panel under the symmetry panel (BtcLiveSymUI),
    /// handles on the 8 corners, 6 faces and the centre (drag with LMB, Shift = height, Ctrl+wheel on a face = one block), labels on every crystal.
    /// </summary>
    internal static class BtcShieldBox
    {
        internal const string FileName = "btc-shieldboxes.txt";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal sealed class Box
        {
            internal Vector3 Crystal;
            internal int X0, Y0, Z0, X1, Y1, Z1;   // inclusive voxel bounds
            internal Vector3 Min { get { return new XYZ(X0, Y0, Z0).ToVector3() - new Vector3(0.5f, 0.5f, 0.5f); } }
            internal Vector3 Max { get { return new XYZ(X1, Y1, Z1).ToVector3() + new Vector3(0.5f, 0.5f, 0.5f); } }
            internal Vector3 Center { get { return (Min + Max) * 0.5f; } }
            internal Vector3 Half { get { return (Max - Min) * 0.5f; } }
            internal Box Copy() { return (Box)MemberwiseClone(); }
        }

        internal static readonly List<Box> Boxes = new List<Box>();

        /// <summary>The box bound to the crystal at this position (within 6 blocks), or null.</summary>
        /// <summary>Every part (box) bound to the same crystal as the box nearest to this position; their union is the barrier.</summary>
        internal static void AllFor(Vector3 crystal, List<Box> into)
        {
            Box first = For(crystal);
            if (first == null) return;
            for (int i = 0; i < Boxes.Count; i++) if ((Boxes[i].Crystal - first.Crystal).sqrMagnitude < 0.01f) into.Add(Boxes[i]);
        }

        internal static Box For(Vector3 crystal)
        {
            Box best = null;
            float bd = Near;
            for (int i = 0; i < Boxes.Count; i++)
            {
                float d = (Boxes[i].Crystal - crystal).sqrMagnitude;
                if (d < bd) { bd = d; best = Boxes[i]; }
            }
            return best;
        }

        // ------------------------------------------------------------------ save folder
        internal static void OnSave(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            string file = Path.Combine(folder, FileName);
            if (Boxes.Count == 0) { if (File.Exists(file)) File.Delete(file); return; }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Boxes.Count; i++)
            {
                Box b = Boxes[i];
                sb.Append(F(b.Crystal.x)).Append(' ').Append(F(b.Crystal.y)).Append(' ').Append(F(b.Crystal.z)).Append(' ')
                  .Append(b.X0).Append(' ').Append(b.Y0).Append(' ').Append(b.Z0).Append(' ').Append(b.X1).Append(' ').Append(b.Y1).Append(' ').Append(b.Z1).Append('\n');
            }
            File.WriteAllText(file, sb.ToString());
            BtcLog.Info("ShieldBox", "saved " + Boxes.Count + " boxes");
        }

        private static string F(float v) { return v.ToString("F2", Inv); }

        internal static void OnLoad(string folder)
        {
            Boxes.Clear();
            _undo.Clear();
            if (string.IsNullOrEmpty(folder)) return;
            string file = Path.Combine(folder, FileName);
            if (!File.Exists(file)) return;
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] a = lines[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (a.Length < 9) continue;
                try
                {
                    Box b = new Box
                    {
                        Crystal = new Vector3(float.Parse(a[0], Inv), float.Parse(a[1], Inv), float.Parse(a[2], Inv)),
                        X0 = int.Parse(a[3], Inv), Y0 = int.Parse(a[4], Inv), Z0 = int.Parse(a[5], Inv),
                        X1 = int.Parse(a[6], Inv), Y1 = int.Parse(a[7], Inv), Z1 = int.Parse(a[8], Inv)
                    };
                    Order(b);
                    Boxes.Add(b);
                }
                catch (Exception e) { BtcLog.Warn("ShieldBox", "bad line " + (i + 1) + ": " + e.Message); }
            }
            BtcLog.Info("ShieldBox", "loaded " + Boxes.Count + " boxes");
        }

        private static void Order(Box b)
        {
            if (b.X0 > b.X1) { int t = b.X0; b.X0 = b.X1; b.X1 = t; }
            if (b.Y0 > b.Y1) { int t = b.Y0; b.Y0 = b.Y1; b.Y1 = t; }
            if (b.Z0 > b.Z1) { int t = b.Z0; b.Z0 = b.Z1; b.Z1 = t; }
        }

        // ------------------------------------------------------------------ editor tool
        internal static bool On;
        private static bool _open = true;
        private static readonly List<Vector3> Cry = new List<Vector3>();
        private static readonly List<bool> CrySpawn = new List<bool>();
        private static float _nextScan;
        private static Vector3 _sel;
        private static bool _hasSel;
        private static int _hover = -1, _drag = -1;
        private static Box _dragStart;
        private static Vector3 _dragAnchor;
        private static XYZ _dragV0;
        private static readonly List<List<Box>> _undo = new List<List<Box>>();
        private static readonly Color Accent = new Color(1f, 0.82f, 0.25f, 1f);

        private static string T(string en, string ru) { return BtcGameplay.T(en, ru); }

        private static int _part;   // selected part among the boxes of the selected crystal
        private static readonly List<Box> SelParts = new List<Box>();

        private static Box Sel
        {
            get
            {
                if (!_hasSel) return null;
                SelParts.Clear();
                AllFor(_sel, SelParts);
                if (SelParts.Count == 0) return null;
                _part = Mathf.Clamp(_part, 0, SelParts.Count - 1);
                return SelParts[_part];
            }
        }

        private static void Pick(Vector3 crystal) { if (!_hasSel || (crystal - _sel).sqrMagnitude >= Near) _part = 0; _sel = crystal; _hasSel = true; }

        private static void Scan()
        {
            Cry.Clear(); CrySpawn.Clear();
            // the map editor has no crystals, only markers: the gamemode creates a home crystal at every "Player Starting Point" marker
            // and a capture crystal at every "Crystal" marker (spawn.position), so a box is bound to the marker position.
            // Crystals that already exist (editing a running game) are listed too.
            int nSpawn = Markers(BtcSpawnPick.StartGroup, true), nShard = Markers(CrystalGroup, false), nNest = 0;
            List<FireflyNest> list = BtcMatchStats.NestList();
            if (list != null)
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null && AddCry(list[i].transform.position, list[i].isHome)) nNest++;
            int sig = nSpawn | nShard << 10 | nNest << 20;
            if (sig != _scanSig) { _scanSig = sig; BtcLog.Info("ShieldBox", "crystal points: spawn markers=" + nSpawn + " crystal markers=" + nShard + " crystals=" + nNest); }
            if (_hasSel)
            {
                bool still = false;
                for (int i = 0; i < Cry.Count; i++) if ((Cry[i] - _sel).sqrMagnitude < Near) { _sel = Cry[i]; still = true; break; }
                if (!still) _hasSel = false;
            }
            if (!_hasSel && Cry.Count > 0) { _sel = Cry[0]; _hasSel = true; }
        }

        private static int _scanSig = -1;

        private const string CrystalGroup = "Crystal";
        private const float Near = 36f;   // squared: a crystal is created at its marker, allow 6 blocks of settling

        private static int Markers(string group, bool spawn)
        {
            int n = 0;
            try
            {
                List<LuaMarker> ms = LuaMarker.GetGroup(group);
                if (ms != null) for (int i = 0; i < ms.Count; i++) if (ms[i] != null && AddCry(ms[i].transform.position, spawn)) n++;
            }
            catch (Exception e) { BtcLog.Warn("ShieldBox", "markers " + group + ": " + e.Message); }
            return n;
        }

        private static bool AddCry(Vector3 p, bool spawn)
        {
            for (int i = 0; i < Cry.Count; i++) if ((Cry[i] - p).sqrMagnitude < 4f) return false;
            Cry.Add(p); CrySpawn.Add(spawn);
            return true;
        }

        private static int SelIndex()
        {
            for (int i = 0; i < Cry.Count; i++) if ((Cry[i] - _sel).sqrMagnitude < Near) return i;
            return -1;
        }

        private static string CryName(int i)
        {
            if (i < 0 || i >= Cry.Count) return "-";
            return "#" + (i + 1) + " " + (CrySpawn[i] ? T("spawn", "спавн") : T("shard", "осколок"));
        }

        private static void PushUndo()
        {
            List<Box> snap = new List<Box>(Boxes.Count);
            for (int i = 0; i < Boxes.Count; i++) snap.Add(Boxes[i].Copy());
            _undo.Add(snap);
            if (_undo.Count > 100) _undo.RemoveAt(0);
        }

        private static void Undo()
        {
            if (_undo.Count == 0) return;
            List<Box> snap = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            Boxes.Clear();
            Boxes.AddRange(snap);
        }

        // ---- handles: 0..7 corners (bit0 x max, bit1 y max, bit2 z max), 8..13 faces (8 + axis * 2 + max), 14 centre
        private static Vector3 HandlePos(Box b, int id)
        {
            Vector3 mn = b.Min, mx = b.Max, c = (mn + mx) * 0.5f;
            if (id < 8) return new Vector3((id & 1) != 0 ? mx.x : mn.x, (id & 2) != 0 ? mx.y : mn.y, (id & 4) != 0 ? mx.z : mn.z);
            if (id < 14)
            {
                int ax = (id - 8) / 2; bool max = ((id - 8) & 1) != 0;
                if (ax == 0) c.x = max ? mx.x : mn.x; else if (ax == 1) c.y = max ? mx.y : mn.y; else c.z = max ? mx.z : mn.z;
                return c;
            }
            return c;
        }

        internal static void Tick()
        {
            if (!On) { _drag = -1; _hover = -1; return; }
            try
            {
                if (Time.unscaledTime >= _nextScan) { _nextScan = Time.unscaledTime + 1f; Scan(); }
                Camera cam = Camera.main;
                Box b = Sel;
                if (cam == null || b == null) { _drag = -1; _hover = -1; return; }
                if (_drag >= 0)
                {
                    if (!Input.GetMouseButton(0)) { _drag = -1; return; }
                    Drag(cam, b);
                    return;
                }
                _hover = -1;
                if (BtcLiveSymUI.Over) return;
                Vector2 m = Input.mousePosition;
                float bd = 16f * 16f;
                for (int id = 0; id < 15; id++)
                {
                    Vector3 s = cam.WorldToScreenPoint(HandlePos(b, id));
                    if (s.z <= 0f) continue;
                    float d = (new Vector2(s.x, s.y) - m).sqrMagnitude;
                    if (d < bd) { bd = d; _hover = id; }
                }
                // centres of the other parts of this crystal: a click selects that part
                for (int k = 0; k < SelParts.Count; k++)
                {
                    if (k == _part) continue;
                    Vector3 s = cam.WorldToScreenPoint(SelParts[k].Center);
                    if (s.z <= 0f) continue;
                    float d = (new Vector2(s.x, s.y) - m).sqrMagnitude;
                    if (d < bd) { bd = d; _hover = 100 + k; }
                }
                if (_hover < 0) return;
                if (_hover >= 100) { if (Input.GetMouseButtonDown(0)) { _part = _hover - 100; _hover = -1; } return; }
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                float wheel = Input.mouseScrollDelta.y;
                if (ctrl && wheel != 0f && _hover >= 8 && _hover < 14)
                {
                    PushUndo();
                    int step = wheel > 0f ? 1 : -1, ax = (_hover - 8) / 2; bool max = ((_hover - 8) & 1) != 0;
                    Nudge(b, ax, max, max ? step : -step);
                }
                if (Input.GetMouseButtonDown(0))
                {
                    PushUndo();
                    _drag = _hover;
                    _dragStart = b.Copy();
                    _dragAnchor = HandlePos(b, _drag);
                    _dragV0 = XYZ.FromVector3(_dragAnchor);
                }
            }
            catch (Exception e) { BtcLog.Error("ShieldBox", "tick: " + e); _drag = -1; }
        }

        private static void Nudge(Box b, int ax, bool max, int d)
        {
            if (ax == 0) { if (max) b.X1 = Mathf.Max(b.X0, b.X1 + d); else b.X0 = Mathf.Min(b.X1, b.X0 + d); }
            else if (ax == 1) { if (max) b.Y1 = Mathf.Clamp(b.Y1 + d, b.Y0, 255); else b.Y0 = Mathf.Clamp(b.Y0 + d, 0, b.Y1); }
            else { if (max) b.Z1 = Mathf.Max(b.Z0, b.Z1 + d); else b.Z0 = Mathf.Min(b.Z1, b.Z0 + d); }
        }

        private static void Drag(Camera cam, Box b)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool face = _drag >= 8 && _drag < 14;
            int fax = face ? (_drag - 8) / 2 : -1;
            bool vert = face ? fax == 1 : shift;
            Ray r = cam.ScreenPointToRay(Input.mousePosition);
            Vector3 hit;
            if (!vert)
            {
                if (Mathf.Abs(r.direction.y) < 1e-4f) return;
                float t = (_dragAnchor.y - r.origin.y) / r.direction.y;
                if (t < 0f) return;
                hit = r.origin + r.direction * t;
            }
            else
            {
                Vector3 n = cam.transform.forward; n.y = 0f;
                if (n.sqrMagnitude < 1e-4f) return;
                Plane p = new Plane(n.normalized, _dragAnchor);
                float t;
                if (!p.Raycast(r, out t)) return;
                hit = r.origin + r.direction * t;
            }
            XYZ v = XYZ.FromVector3(hit);
            Box s = _dragStart;
            b.X0 = s.X0; b.X1 = s.X1; b.Y0 = s.Y0; b.Y1 = s.Y1; b.Z0 = s.Z0; b.Z1 = s.Z1;
            if (_drag == 14)
            {
                int dx = vert ? 0 : v.x - _dragV0.x, dz = vert ? 0 : v.z - _dragV0.z, dy = vert ? v.y - _dragV0.y : 0;
                dy = Mathf.Clamp(dy, -s.Y0, 255 - s.Y1);
                b.X0 += dx; b.X1 += dx; b.Z0 += dz; b.Z1 += dz; b.Y0 += dy; b.Y1 += dy;
                return;
            }
            if (face)
            {
                bool max = ((_drag - 8) & 1) != 0;
                if (fax == 0) { if (max) b.X1 = Mathf.Max(s.X0, v.x); else b.X0 = Mathf.Min(s.X1, v.x); }
                else if (fax == 1) { if (max) b.Y1 = Mathf.Clamp(v.y, s.Y0, 255); else b.Y0 = Mathf.Clamp(v.y, 0, s.Y1); }
                else { if (max) b.Z1 = Mathf.Max(s.Z0, v.z); else b.Z0 = Mathf.Min(s.Z1, v.z); }
                return;
            }
            // corner: horizontal = its two side faces, Shift = its top or bottom face
            bool xm = (_drag & 1) != 0, ym = (_drag & 2) != 0, zm = (_drag & 4) != 0;
            if (vert) { if (ym) b.Y1 = Mathf.Clamp(v.y, s.Y0, 255); else b.Y0 = Mathf.Clamp(v.y, 0, s.Y1); }
            else
            {
                if (xm) b.X1 = Mathf.Max(s.X0, v.x); else b.X0 = Mathf.Min(s.X1, v.x);
                if (zm) b.Z1 = Mathf.Max(s.Z0, v.z); else b.Z0 = Mathf.Min(s.Z1, v.z);
            }
        }

        /// <summary>The selected part becomes a cube of the rule radius around the crystal (a new first part when it has none).</summary>
        private static void Create()
        {
            PushUndo();
            XYZ c = XYZ.FromVector3(_sel);
            int r = Mathf.RoundToInt(BtcShield.Radius);
            Box b = Sel;
            if (b == null) { b = new Box { Crystal = _sel }; Boxes.Add(b); _part = 0; }
            b.X0 = c.x - r; b.X1 = c.x + r; b.Z0 = c.z - r; b.Z1 = c.z + r;
            b.Y0 = Mathf.Max(0, c.y - r); b.Y1 = Mathf.Min(255, c.y + r);
        }

        /// <summary>Another part for the selected crystal: a copy of the selected part shifted east by half its width (overlapping, so the union stays one shape).</summary>
        private static void AddPart(Box cur)
        {
            PushUndo();
            Box n = cur.Copy();
            int dx = Mathf.Max(1, (cur.X1 - cur.X0 + 1) / 2);
            n.X0 += dx; n.X1 += dx;
            n.Crystal = cur.Crystal;
            Boxes.Add(n);
            SelParts.Clear(); AllFor(_sel, SelParts);
            _part = SelParts.IndexOf(n);
        }

        // ------------------------------------------------------------------ world view (Camera.onPostRender, GL lines with the symmetry material set)
        internal static void Draw(Camera cam)
        {
            if (!On) return;
            GL.Begin(GL.LINES);
            for (int i = 0; i < Cry.Count; i++)
            {
                bool sel = _hasSel && (Cry[i] - _sel).sqrMagnitude < Near;
                Box b = For(Cry[i]);
                GL.Color(sel ? Accent : (b != null ? new Color(0.4f, 1f, 0.5f, 0.8f) : new Color(1f, 1f, 1f, 0.5f)));
                Vector3 p = Cry[i];
                GL.Vertex(p); GL.Vertex(p + Vector3.up * 14f);
                if (b != null) { GL.Vertex(p); GL.Vertex(b.Center); }
            }
            for (int i = 0; i < Boxes.Count; i++)
            {
                Box b = Boxes[i];
                bool sel = _hasSel && (b.Crystal - _sel).sqrMagnitude < Near;
                GL.Color(sel ? Accent : new Color(0.4f, 1f, 0.5f, 0.55f));
                Wire(b.Min, b.Max);
            }
            Box s = Sel;
            if (s != null)
            {
                GL.Color(Color.white);
                Wire(s.Min, s.Max);   // the selected part on top of the others
                for (int k = 0; k < SelParts.Count; k++)
                {
                    if (k == _part) continue;
                    bool hot = _hover == 100 + k;
                    GL.Color(hot ? Color.white : new Color(1f, 0.5f, 0.9f, 0.7f));
                    Vector3 c = SelParts[k].Center;
                    float h = Mathf.Clamp((cam.transform.position - c).magnitude * 0.012f, 0.25f, 3f) * (hot ? 1.6f : 1f);
                    Wire(c - new Vector3(h, h, h), c + new Vector3(h, h, h));
                }
                for (int id = 0; id < 15; id++)
                {
                    bool hot = id == _hover || id == _drag;
                    GL.Color(hot ? Color.white : (id < 8 ? Accent : (id < 14 ? new Color(0.3f, 0.9f, 1f, 1f) : new Color(1f, 0.5f, 0.9f, 1f))));
                    Vector3 c = HandlePos(s, id);
                    float h = Mathf.Clamp((cam.transform.position - c).magnitude * 0.012f, 0.25f, 3f) * (hot ? 1.6f : 1f);
                    Wire(c - new Vector3(h, h, h), c + new Vector3(h, h, h));
                }
            }
            GL.End();
        }

        private static void Wire(Vector3 a, Vector3 b)
        {
            E(a.x, a.y, a.z, b.x, a.y, a.z); E(a.x, b.y, a.z, b.x, b.y, a.z); E(a.x, a.y, b.z, b.x, a.y, b.z); E(a.x, b.y, b.z, b.x, b.y, b.z);
            E(a.x, a.y, a.z, a.x, b.y, a.z); E(b.x, a.y, a.z, b.x, b.y, a.z); E(a.x, a.y, b.z, a.x, b.y, b.z); E(b.x, a.y, b.z, b.x, b.y, b.z);
            E(a.x, a.y, a.z, a.x, a.y, b.z); E(b.x, a.y, a.z, b.x, a.y, b.z); E(a.x, b.y, a.z, a.x, b.y, b.z); E(b.x, b.y, a.z, b.x, b.y, b.z);
        }

        private static void E(float x0, float y0, float z0, float x1, float y1, float z1) { GL.Vertex3(x0, y0, z0); GL.Vertex3(x1, y1, z1); }

        // ------------------------------------------------------------------ IMGUI: crystal labels in the world + the panel
        /// <summary>Clickable label above every crystal (selects it). Returns true when the pointer is over one.</summary>
        internal static bool Labels()
        {
            if (!On) return false;
            Camera cam = Camera.main;
            if (cam == null) return false;
            bool over = false;
            Vector2 mouse = Event.current.mousePosition;
            for (int i = 0; i < Cry.Count; i++)
            {
                Vector3 s = cam.WorldToScreenPoint(Cry[i] + Vector3.up * 15f);
                if (s.z <= 0f) continue;
                bool sel = _hasSel && (Cry[i] - _sel).sqrMagnitude < Near;
                string txt = CryName(i) + (For(Cry[i]) != null ? T(" [box]", " [бокс]") : "");
                Rect r = new Rect(s.x - 55f, Screen.height - s.y - 11f, 110f, 22f);
                if (r.Contains(mouse)) over = true;
                Color old = GUI.color;
                GUI.color = sel ? Accent : Color.white;
                if (GUI.Button(r, txt)) Pick(Cry[i]);
                GUI.color = old;
            }
            return over;
        }

        /// <summary>Editor panel at (x, y), width w. Returns true when the pointer is over it.</summary>
        internal static bool Panel(float x, float y, float w)
        {
            Box b = On ? Sel : null;
            float h = !_open ? 24f : (!On ? 52f : (b == null ? 52f + 74f : 52f + 74f + 26f + 6 * 24f + 74f));
            Rect box = new Rect(x, y, w, h);
            GUI.Box(box, GUIContent.none);
            if (GUI.Button(new Rect(x + 2, y + 2, w - 4, 20), (_open ? "- " : "+ ") + T("Base bounds: ", "Границы баз: ") + Boxes.Count)) _open = !_open;
            bool over = box.Contains(Event.current.mousePosition);
            if (!_open) return over;
            float cy = y + 28f;
            bool nv = GUI.Toggle(new Rect(x + 8, cy, w - 16, 20), On, " " + T("Base bounds tool", "Инструмент границ баз"));
            if (nv != On) { On = nv; if (On) { BtcCarve.On = false; _nextScan = 0f; } }
            if (!On) return over;
            cy += 24f;
            int si = SelIndex();
            GUI.Label(new Rect(x + 8, cy, 60, 20), T("Crystal", "Кристалл"));
            if (GUI.Button(new Rect(x + 68, cy, 24, 20), "<") && Cry.Count > 0) { si = si <= 0 ? Cry.Count - 1 : si - 1; Pick(Cry[si]); }
            GUI.Label(new Rect(x + 96, cy, 86, 20), CryName(si));
            if (GUI.Button(new Rect(x + w - 32, cy, 24, 20), ">") && Cry.Count > 0) { si = (si + 1) % Cry.Count; Pick(Cry[si]); }
            cy += 24f;
            GUI.Label(new Rect(x + 8, cy, w - 16, 20), T("or click a label above a crystal", "или кликните подпись над кристаллом"));
            cy += 24f;
            if (b == null)
            {
                if (GUI.Button(new Rect(x + 8, cy, w - 16, 22), T("Create box for this crystal", "Создать бокс для кристалла")) && _hasSel) Create();
                return over;
            }
            // parts of this crystal: the barrier is their union (no walls between them)
            int np = SelParts.Count;
            GUI.Label(new Rect(x + 8, cy, 60, 20), T("Box ", "Бокс ") + (_part + 1) + "/" + np);
            if (GUI.Button(new Rect(x + 68, cy, 24, 20), "<") && np > 1) _part = (_part + np - 1) % np;
            if (GUI.Button(new Rect(x + 94, cy, 24, 20), ">") && np > 1) _part = (_part + 1) % np;
            if (GUI.Button(new Rect(x + 122, cy, w - 130, 22), T("+ Add box", "+ Добавить бокс"))) { AddPart(b); b = Sel; }
            cy += 26f;
            GUI.Label(new Rect(x + 8, cy, w - 16, 20), T("Size ", "Размер ") + (b.X1 - b.X0 + 1) + " x " + (b.Z1 - b.Z0 + 1) + T(", height ", ", высота ") + (b.Y1 - b.Y0 + 1));
            cy += 24f;
            Row(x, ref cy, b, 0, false, T("West", "Запад"));
            Row(x, ref cy, b, 0, true, T("East", "Восток"));
            Row(x, ref cy, b, 2, false, T("South", "Юг"));
            Row(x, ref cy, b, 2, true, T("North", "Север"));
            Row(x, ref cy, b, 1, false, T("Bottom", "Низ"));
            Row(x, ref cy, b, 1, true, T("Top", "Верх"));
            float bw = (w - 16f - 8f) / 3f;
            if (GUI.Button(new Rect(x + 8, cy, bw, 22), T("Cube", "Куб"))) Create();
            if (GUI.Button(new Rect(x + 12 + bw, cy, bw, 22), T("Undo", "Отменить"))) Undo();
            if (GUI.Button(new Rect(x + 16 + bw * 2, cy, bw, 22), T("Delete", "Удалить"))) { PushUndo(); Boxes.Remove(b); _part = Mathf.Max(0, _part - 1); }
            cy += 26f;
            GUI.Label(new Rect(x + 8, cy, w - 16, 44), T("Drag corners and faces, centre = move, Shift = height, Ctrl+wheel = 1 block, pink = other boxes (click)",
                                                          "Тяните вершины и грани, центр = сдвиг, Shift = высота, Ctrl+колесо = 1 блок, розовые = другие боксы (клик)"));
            return over;
        }

        private static void Row(float x, ref float cy, Box b, int ax, bool max, string name)
        {
            int v = ax == 0 ? (max ? b.X1 : b.X0) : (ax == 1 ? (max ? b.Y1 : b.Y0) : (max ? b.Z1 : b.Z0));
            GUI.Label(new Rect(x + 8, cy, 74, 20), name + " " + v);
            int[] steps = { -4, -1, 1, 4 };
            for (int i = 0; i < steps.Length; i++)
                if (GUI.Button(new Rect(x + 82 + i * 31, cy, 29, 20), (steps[i] > 0 ? "+" : "") + steps[i])) { PushUndo(); Nudge(b, ax, max, steps[i]); }
            cy += 24f;
        }
    }
}
