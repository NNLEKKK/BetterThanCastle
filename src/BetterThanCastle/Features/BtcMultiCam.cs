using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Game.Components.Skybox;
using Brix.Input;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Multi-camera: the screen is split into 2..8 independent camera views (auto grid, any view can be pulled out as a free window).
    /// The game has ONE camera rig (CameraScript + main camera + sky camera), so the views are time-multiplexed:
    /// the view under the mouse is the ACTIVE one and owns the rig (all camera binds, picking, selection boxes and orders work through it as usual);
    /// every other view keeps its own pose and is drawn by re-posing the same two cameras (sky + main) with another viewport rect and rendering them by hand.
    /// Order of a frame: early Update (hover -> swap the rig state), vanilla Update, late Update (render the other views, leave the top view for the
    /// automatic render), Camera.onPostRender (rig goes back to the active view for OnGUI / next frame).
    /// Local only: nothing is sent over the network, so BetterConnection OFF stays vanilla. The layout is remembered (configs/multicam.txt).
    /// </summary>
    internal static class BtcMultiCam
    {
        internal sealed class View
        {
            public bool Floating;
            public bool Mirror;   // restored from the file: follows the active view until the player takes control of this one (the start camera is still flying)
            public Rect Grid = new Rect(0f, 0f, 1f, 1f);   // normalized, origin bottom-left (Camera.rect style)
            public Rect Free = new Rect(0.55f, 0.08f, 0.42f, 0.42f);
            public Vector3 Pos, Target;
            public Quaternion Rot = Quaternion.identity;
            public float Distance, Angle, Orient;
        }

        internal const int MaxViews = 8;
        internal static readonly List<View> Views = new List<View>();
        internal static View Active;
        internal static bool MouseOverUi;
        internal static bool On { get { return Views.Count >= 2; } }

        private static CameraScript _cs;
        private static Camera _main, _sky;
        private static SkyboxConfigurator _conf;
        private static Behaviour _ao;
        private static View _max;                       // maximised view (others are not drawn)
        private static List<View> _backup;              // layout kept by Collapse so Toggle can bring it back
        private static int _backupActive;
        private static bool _pendingRestore;
        private static float _attachAt;
        private static FogOfWarRenderer _fog;
        internal static bool UseMouseOverride;          // DEV lab only (hidden games have no real mouse)
        internal static Vector3 MouseOverride;
        internal static Vector3 Mouse() { return UseMouseOverride ? MouseOverride : Input.mousePosition; }
        private static bool _manual, _hooked, _lockMouse, _pin, _gridDirty = true;
        private static Vector3 _pinPos, _lastMouse;
        private static float _lastStripClick;
        private static View _lastStripView;

        private enum Drag { None, Move, Swap, Resize }
        private static Drag _drag;
        private static View _dragView;

        // ------------------------------------------------------------------ lifecycle

        internal static void Attach(CameraScript cs)
        {
            if (cs == null || cs == _cs) return;
            _cs = cs;
            Views.Clear(); Active = null; _max = null; _backup = null; MouseOverUi = false; _drag = Drag.None; _lockMouse = false; _pin = false;
            _main = cs.GetComponent<Camera>();
            SkyboxCameraDrawer d = cs.GetComponent<SkyboxCameraDrawer>();
            _sky = d != null ? d.skyboxCamera : null;
            _conf = UnityEngine.Object.FindObjectOfType<SkyboxConfigurator>();
            _ao = cs.GetComponent("AmplifyOcclusionEffect") as Behaviour;
            if (_main != null) _main.ResetAspect();
            if (_sky != null) _sky.ResetAspect();
            if (cs.GetComponent<BtcMultiCamEarly>() == null) cs.gameObject.AddComponent<BtcMultiCamEarly>();
            if (cs.GetComponent<BtcMultiCamLate>() == null) cs.gameObject.AddComponent<BtcMultiCamLate>();
            if (!_hooked) { _hooked = true; Camera.onPostRender += OnPostRender; }
            // the saved layout comes back a little after the map load, when the camera stands where the game put it (views copy that pose)
            _pendingRestore = BtcHotkeys.GetBool("multicam_restore", true);
            _attachAt = Time.unscaledTime;
            BtcLog.Info("MultiCam", "attached main=" + (_main != null) + " sky=" + (_sky != null) + " conf=" + (_conf != null) + " ao=" + (_ao != null) + " views=" + Views.Count);
        }

        internal static void Shutdown()
        {
            if (_main != null) ResetRects();
            Views.Clear(); Active = null; _max = null; _cs = null; MouseOverUi = false;
        }

        private static bool Ready()
        {
            if (_cs == null || _main == null) return false;
            if (CameraScript.Instance != _cs) return false;
            return true;
        }

        // ------------------------------------------------------------------ commands (binds + UI)

        internal static void Split()
        {
            if (!Ready()) return;
            if (Views.Count >= MaxViews) { BtcLog.Info("MultiCam", "split: already " + MaxViews + " views"); return; }
            if (Views.Count == 0)
            {
                View a = new View(); CaptureInto(a); Views.Add(a); Active = a;
            }
            else CaptureInto(Active);
            View v = Clone(Active);
            Views.Add(v);
            _max = null; _gridDirty = true; Save();
            BtcLog.Info("MultiCam", "split -> " + Views.Count + " views");
        }

        internal static void CloseView(View v)
        {
            if (!Ready() || v == null || !Views.Contains(v)) return;
            CaptureInto(Active);
            Views.Remove(v);
            if (_max == v) _max = null;
            if (Active == v) Active = Views.Count > 0 ? Views[Views.Count - 1] : null;
            if (Active != null) ApplyFrom(Active);
            if (Views.Count <= 1) { Collapse(true); return; }
            _gridDirty = true; Save();
        }

        internal static void CloseActive() { CloseView(Active); }

        /// <summary>Back to one normal camera. The camera keeps the view that was active.</summary>
        internal static void Collapse(bool forget)
        {
            if (!Ready()) { Views.Clear(); return; }
            if (Views.Count >= 2 && !forget)
            {
                CaptureInto(Active);
                _backupActive = Views.IndexOf(Active);
                _backup = new List<View>();
                for (int i = 0; i < Views.Count; i++) _backup.Add(Clone(Views[i]));
            }
            Views.Clear(); Active = null; _max = null; _drag = Drag.None; MouseOverUi = false;
            ResetRects();
            Save();
            BtcLog.Info("MultiCam", "collapsed to one camera");
        }

        /// <summary>One key: split into two when single, collapse when split (the next press brings the old layout back).</summary>
        internal static void Toggle()
        {
            if (On) { Collapse(false); return; }
            if (_backup != null && _backup.Count >= 2 && Ready())
            {
                Views.Clear();
                for (int i = 0; i < _backup.Count; i++) Views.Add(Clone(_backup[i]));
                _backup = null;
                // the camera on screen is the one that was active when the split was collapsed: it keeps going, the others come back as they were
                Active = Views[Mathf.Clamp(_backupActive, 0, Views.Count - 1)];
                CaptureInto(Active);
                _gridDirty = true; Save();
                return;
            }
            Split();
        }

        internal static void ToggleMax(View v)
        {
            if (!On || v == null) return;
            _max = _max == v ? null : v;
        }

        internal static void ToggleFloat(View v)
        {
            if (!On || v == null) return;
            if (v.Floating) Dock(v); else Detach(v);
        }

        internal static void DockAll()
        {
            for (int i = 0; i < Views.Count; i++) Views[i].Floating = false;
            _gridDirty = true; Save();
        }

        internal static void Focus(int i)
        {
            if (!On || i < 0 || i >= Views.Count) return;
            SwitchTo(Views[i]);
            _pin = true; _pinPos = Mouse();
        }

        internal static void FocusNext(int dir)
        {
            if (!On || Active == null) return;
            int i = Views.IndexOf(Active);
            Focus((i + dir + Views.Count) % Views.Count);
        }

        /// <summary>Every other view jumps to the pose of the active one.</summary>
        internal static void SyncAll()
        {
            if (!On || !Ready()) return;
            CaptureInto(Active);
            for (int i = 0; i < Views.Count; i++) if (Views[i] != Active) CopyPose(Views[i], Active);
        }

        internal static int IndexOf(View v) { return Views.IndexOf(v); }

        // ------------------------------------------------------------------ state swap

        private static void CaptureInto(View v)
        {
            if (v == null || _cs == null) return;
            Transform t = _cs.transform;
            v.Pos = t.position; v.Rot = t.rotation; v.Target = CameraScript.Target;
            v.Distance = _cs.Distance; v.Angle = _cs.AngleOfView; v.Orient = _cs.CamOrientation;
        }

        private static void ApplyFrom(View v)
        {
            if (v == null || _cs == null) return;
            _cs.Distance = v.Distance; _cs.AngleOfView = v.Angle; _cs.CamOrientation = v.Orient;
            CameraScript.Target = v.Target;
            Transform t = _cs.transform;
            t.position = v.Pos; t.rotation = v.Rot;
        }

        private static void CopyPose(View to, View from)
        {
            to.Pos = from.Pos; to.Rot = from.Rot; to.Target = from.Target; to.Distance = from.Distance; to.Angle = from.Angle; to.Orient = from.Orient;
        }

        private static View Clone(View s)
        {
            View v = new View { Floating = s.Floating, Grid = s.Grid, Free = s.Free };
            CopyPose(v, s);
            return v;
        }

        private static void SwitchTo(View v)
        {
            if (v == null || v == Active) return;
            CaptureInto(Active);
            Active = v;
            v.Mirror = false;
            ApplyFrom(v);
            PoseRig(v);
        }

        // ------------------------------------------------------------------ layout

        private static Rect RectOf(View v)
        {
            if (_max != null) return v == _max ? new Rect(0f, 0f, 1f, 1f) : new Rect(0f, 0f, 0f, 0f);
            return v.Floating ? v.Free : v.Grid;
        }

        private static readonly int[] ColsFor = { 1, 1, 2, 2, 2, 3, 3, 4, 4 };

        private static void Relayout()
        {
            if (!_gridDirty) return;
            _gridDirty = false;
            List<View> g = new List<View>();
            for (int i = 0; i < Views.Count; i++) if (!Views[i].Floating) g.Add(Views[i]);
            int n = g.Count;
            if (n == 0) return;
            int cols = ColsFor[Mathf.Min(n, MaxViews)];
            int rows = (n + cols - 1) / cols;
            int k = 0;
            for (int r = 0; r < rows; r++)
            {
                int inRow = Mathf.Min(cols, n - k);
                float h = 1f / rows, w = 1f / inRow;
                float y = 1f - (r + 1) * h;
                for (int c = 0; c < inRow; c++) g[k++].Grid = new Rect(c * w, y, w, h);
            }
        }

        private static Rect Px(Rect n) { return new Rect(n.x * Screen.width, n.y * Screen.height, n.width * Screen.width, n.height * Screen.height); }

        // IMGUI space (origin top-left)
        private static Rect Gui(Rect n) { return new Rect(n.x * Screen.width, (1f - n.y - n.height) * Screen.height, n.width * Screen.width, n.height * Screen.height); }

        private static Rect StripGui(View v)
        {
            Rect r = Gui(RectOf(v));
            return new Rect(r.x, r.y, Mathf.Min(r.width, 118f), 18f);
        }

        private static Rect ButtonGui(View v, int i)
        {
            Rect s = StripGui(v);
            return new Rect(s.x + 26f + i * 30f, s.y + 1f, 28f, 16f);
        }

        private static Rect HandleGui(View v)
        {
            Rect r = Gui(RectOf(v));
            return new Rect(r.xMax - 16f, r.yMax - 16f, 16f, 16f);
        }

        // ------------------------------------------------------------------ rig

        private static void SetRects(Rect r)
        {
            if (_main != null) _main.rect = r;
            if (_sky != null) _sky.rect = r;
        }

        private static void ResetRects()
        {
            SetRects(new Rect(0f, 0f, 1f, 1f));
            if (_ao != null) _ao.enabled = true;
        }

        private static void PoseRig(View v)
        {
            if (_main == null) return;
            Transform t = _main.transform;
            t.position = v.Pos; t.rotation = v.Rot;
            SetRects(RectOf(v));
            if (_conf != null) { try { _conf.LateUpdate(); } catch (Exception) { } }
            try
            {
                if (_fog == null) _fog = UnityEngine.Object.FindObjectOfType<FogOfWarRenderer>();
                if (_fog != null && _fog._camEffectMat != null) _fog._camEffectMat.SetVector(_fog._HighlightCamera, v.Pos);
            }
            catch (Exception) { }
        }

        private static void OnPostRender(Camera c)
        {
            if (_manual || c != _main || !On || Active == null) return;
            try { PoseRig(Active); }
            catch (Exception e) { BtcLog.Error("MultiCam", "post render: " + e.Message); }
        }

        // ------------------------------------------------------------------ per frame

        /// <summary>Execution order -32000: hover -> swap the rig to the view under the mouse, handle the strips and drags.</summary>
        internal static void EarlyTick()
        {
            MouseOverUi = false;
            if (_pendingRestore && Ready() && !BtcLoadBoost.Loading && Time.unscaledTime - _attachAt > 3f)
            {
                _pendingRestore = false;
                LoadLayout();
            }
            if (!Ready() || !On) return;
            if (Active == null || !Views.Contains(Active)) { Active = Views[0]; ApplyFrom(Active); }
            Relayout();
            Vector3 m = Mouse();
            HandleMouse(m);
            View hov = HoverView(m);
            if (hov != null && hov != Active) SwitchTo(hov);
            if (_drag == Drag.None && !_lockMouse) PoseRigIfNeeded();
            _lastMouse = m;
        }

        private static void PoseRigIfNeeded()
        {
            if (_main == null || Active == null) return;
            Rect want = RectOf(Active);
            if (_main.rect != want || (_sky != null && _sky.rect != want)) SetRects(want);
        }

        private static View HoverView(Vector3 m)
        {
            if (_drag != Drag.None) return Active;
            bool down = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
            if (down) { if (!_lockMouse) _lockMouse = true; }
            else _lockMouse = false;
            if (_lockMouse) return Active;
            if (_pin)
            {
                if ((m - _pinPos).sqrMagnitude < 24f * 24f) return Active;
                _pin = false;
            }
            if (_max != null) return _max;
            // front-most first: free windows (last in list = on top), then the grid
            for (int i = Views.Count - 1; i >= 0; i--)
                if (Views[i].Floating && Px(RectOf(Views[i])).Contains(new Vector2(m.x, m.y))) return Views[i];
            for (int i = 0; i < Views.Count; i++)
                if (!Views[i].Floating && Px(RectOf(Views[i])).Contains(new Vector2(m.x, m.y))) return Views[i];
            return Active;
        }

        private static bool Held(string id)
        {
            KeyCode[] k = BtcHotkeys.GetKeys(id, new KeyCode[] { KeyCode.LeftAlt });
            if (k == null || k.Length == 0) return false;
            for (int i = 0; i < k.Length; i++) if (!Input.GetKey(k[i])) return false;
            return true;
        }

        private static void HandleMouse(Vector3 m)
        {
            bool show = BtcHotkeys.GetBool("multicam_title", true);
            Vector2 g = new Vector2(m.x, Screen.height - m.y);   // IMGUI space
            bool lmbDown = Input.GetMouseButtonDown(0), rmbDown = Input.GetMouseButtonDown(1);
            float dx = (m.x - _lastMouse.x) / Screen.width, dy = (m.y - _lastMouse.y) / Screen.height;

            if (_drag != Drag.None)
            {
                MouseOverUi = true;
                if (!Input.GetMouseButton(_dragButton))
                {
                    if (_drag == Drag.Swap && _dragView != null)
                    {
                        View to = null;
                        for (int i = 0; i < Views.Count; i++)
                            if (Views[i] != _dragView && !Views[i].Floating && Px(Views[i].Grid).Contains(new Vector2(m.x, m.y))) { to = Views[i]; break; }
                        if (to != null)
                        {
                            int a = Views.IndexOf(_dragView), b = Views.IndexOf(to);
                            Views[a] = to; Views[b] = _dragView; _gridDirty = true;
                        }
                    }
                    _drag = Drag.None; _dragView = null; Save();
                    return;
                }
                if (_dragView == null || !Views.Contains(_dragView)) { _drag = Drag.None; return; }
                if (_drag == Drag.Move)
                {
                    Rect f = _dragView.Free;
                    f.x = Mathf.Clamp(f.x + dx, -f.width * 0.8f, 1f - f.width * 0.2f);
                    f.y = Mathf.Clamp(f.y + dy, -f.height * 0.8f, 1f - f.height * 0.2f);
                    _dragView.Free = f;
                }
                else if (_drag == Drag.Resize)
                {
                    Rect f = _dragView.Free;
                    float w = Mathf.Clamp(f.width + dx, 160f / Screen.width, 1f), h = Mathf.Clamp(f.height - dy, 90f / Screen.height, 1f);
                    f.y += f.height - h; f.width = w; f.height = h;
                    _dragView.Free = f;
                }
                return;
            }

            bool altMove = lmbDown && Held("multicam_drag_modifier"), altSize = rmbDown && Held("multicam_drag_modifier");
            for (int i = Views.Count - 1; i >= 0; i--)
            {
                View v = Views[i];
                if (_max != null && v != _max) continue;
                Rect full = Gui(RectOf(v));
                bool inside = full.Contains(g);
                if (!inside) continue;
                if (show)
                {
                    Rect s = StripGui(v);
                    if (s.Contains(g)) MouseOverUi = true;
                    if (v.Floating && HandleGui(v).Contains(g)) { MouseOverUi = true; if (lmbDown) { Raise(v); BeginDrag(Drag.Resize, v, 0); } }
                    if (lmbDown && s.Contains(g))
                    {
                        for (int b = 0; b < 3; b++)
                            if (ButtonGui(v, b).Contains(g))
                            {
                                if (b == 0) ToggleFloat(v); else if (b == 1) ToggleMax(v); else CloseView(v);
                                return;
                            }
                        float now = Time.unscaledTime;
                        if (_lastStripView == v && now - _lastStripClick < 0.35f) { ToggleMax(v); _lastStripView = null; return; }
                        _lastStripView = v; _lastStripClick = now;
                        if (v.Floating) { Raise(v); BeginDrag(Drag.Move, v, 0); } else BeginDrag(Drag.Swap, v, 0);
                        return;
                    }
                }
                if (altMove) { if (!v.Floating) Detach(v); Raise(v); BeginDrag(Drag.Move, v, 0); return; }
                if (altSize) { if (!v.Floating) Detach(v); Raise(v); BeginDrag(Drag.Resize, v, 1); return; }
                break;   // only the front-most view under the cursor takes part
            }
        }

        private static int _dragButton;

        private static void BeginDrag(Drag d, View v, int button)
        {
            _drag = d; _dragView = v; _dragButton = button; MouseOverUi = true;
            if (Active != v) SwitchTo(v);
        }

        private static void Raise(View v)
        {
            if (!v.Floating) return;
            Views.Remove(v); Views.Add(v);
        }

        private static void Detach(View v)
        {
            Rect r = v.Grid;
            float w = r.width * 0.85f, h = r.height * 0.85f;
            v.Free = new Rect(r.x + (r.width - w) * 0.5f, r.y + (r.height - h) * 0.5f, w, h);
            v.Floating = true; Raise(v);
            _gridDirty = true; Save();
        }

        private static void Dock(View v)
        {
            v.Floating = false;
            // docked views keep their list order; a view that was pulled out goes back to the end of the grid
            Views.Remove(v); Views.Insert(FirstFloating(), v);
            _gridDirty = true; Save();
        }

        private static int FirstFloating()
        {
            for (int i = 0; i < Views.Count; i++) if (Views[i].Floating) return i;
            return Views.Count;
        }

        /// <summary>Execution order +32000 (after every LateUpdate): draw the other views, leave the top one for the automatic render.</summary>
        internal static void LateTick()
        {
            if (!Ready() || !On || Active == null) return;
            try
            {
                CaptureInto(Active);
                for (int i = 0; i < Views.Count; i++) if (Views[i].Mirror && Views[i] != Active) CopyPose(Views[i], Active);
                List<View> order = new List<View>(Views.Count);
                if (_max != null) order.Add(_max);
                else
                {
                    for (int i = 0; i < Views.Count; i++) if (!Views[i].Floating) order.Add(Views[i]);
                    for (int i = 0; i < Views.Count; i++) if (Views[i].Floating) order.Add(Views[i]);
                }
                bool lite = BtcHotkeys.GetBool("multicam_lite", true);
                bool aoWas = _ao != null && _ao.enabled;
                _manual = true;
                for (int i = 0; i < order.Count - 1; i++)
                {
                    PoseRig(order[i]);
                    if (_ao != null && lite && order[i] != Active) _ao.enabled = false;
                    else if (_ao != null) _ao.enabled = aoWas;
                    if (_sky != null) _sky.Render();
                    _main.Render();
                }
                _manual = false;
                if (_ao != null) _ao.enabled = aoWas;
                PoseRig(order[order.Count - 1]);
            }
            catch (Exception e) { _manual = false; BtcLog.Error("MultiCam", "late: " + e); }
        }

        // ------------------------------------------------------------------ drawing

        private static GUIStyle _label;
        private static Texture2D _white;

        internal static void Draw()
        {
            if (!On || !Ready()) return;
            if (Event.current.type != EventType.Repaint) return;
            if (_white == null) _white = Texture2D.whiteTexture;
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip, richText = false };
                _label.padding = new RectOffset(0, 0, 0, 0);
            }
            bool show = BtcHotkeys.GetBool("multicam_title", true);
            Color accent = new Color(0.30f, 0.85f, 0.55f, 1f);
            Color old = GUI.color;
            List<View> order = new List<View>();
            for (int i = 0; i < Views.Count; i++) if (!Views[i].Floating) order.Add(Views[i]);
            for (int i = 0; i < Views.Count; i++) if (Views[i].Floating) order.Add(Views[i]);
            Vector2 g = new Vector2(Mouse().x, Screen.height - Mouse().y);
            for (int i = 0; i < order.Count; i++)
            {
                View v = order[i];
                if (_max != null && v != _max) continue;
                Rect r = Gui(RectOf(v));
                bool act = v == Active;
                Color bc = act ? accent : new Color(0f, 0f, 0f, 0.85f);
                float bw = act ? 2f : 1f;
                GUI.color = bc;
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, bw), _white);
                GUI.DrawTexture(new Rect(r.x, r.yMax - bw, r.width, bw), _white);
                GUI.DrawTexture(new Rect(r.x, r.y, bw, r.height), _white);
                GUI.DrawTexture(new Rect(r.xMax - bw, r.y, bw, r.height), _white);
                if (!show) continue;
                Rect s = StripGui(v);
                bool hover = r.Contains(g);
                GUI.color = new Color(0f, 0f, 0f, hover || act ? 0.72f : 0.4f);
                GUI.DrawTexture(s, _white);
                GUI.color = act ? accent : new Color(1f, 1f, 1f, 0.85f);
                GUI.Label(new Rect(s.x, s.y, 24f, s.height), (Views.IndexOf(v) + 1).ToString(CultureInfo.InvariantCulture), _label);
                string[] t = { v.Floating ? "dock" : "free", _max == v ? "min" : "max", "x" };
                for (int b = 0; b < 3; b++)
                {
                    Rect br = ButtonGui(v, b);
                    bool bh = br.Contains(g);
                    GUI.color = new Color(1f, 1f, 1f, bh ? 0.28f : 0.10f);
                    GUI.DrawTexture(br, _white);
                    GUI.color = bh ? Color.white : new Color(1f, 1f, 1f, 0.8f);
                    GUI.Label(br, t[b], _label);
                }
                if (v.Floating)
                {
                    Rect h = HandleGui(v);
                    GUI.color = new Color(1f, 1f, 1f, h.Contains(g) ? 0.9f : 0.45f);
                    GUI.DrawTexture(new Rect(h.xMax - 3f, h.y + 4f, 2f, 12f), _white);
                    GUI.DrawTexture(new Rect(h.x + 4f, h.yMax - 3f, 12f, 2f), _white);
                }
            }
            if (_drag == Drag.Swap && _dragView != null)
            {
                for (int i = 0; i < Views.Count; i++)
                    if (Views[i] != _dragView && !Views[i].Floating && Px(Views[i].Grid).Contains(Mouse()))
                    {
                        Rect r = Gui(Views[i].Grid);
                        GUI.color = new Color(accent.r, accent.g, accent.b, 0.22f);
                        GUI.DrawTexture(r, _white);
                    }
            }
            GUI.color = old;
        }

        // ------------------------------------------------------------------ persistence (configs/multicam.txt)

        private static string FilePath { get { return BtcPaths.Config("multicam.txt"); } }

        private static void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("count=").Append(Views.Count).Append('\n');
                for (int i = 0; i < Views.Count; i++)
                {
                    View v = Views[i];
                    sb.Append("v").Append(i).Append('=').Append(v.Floating ? 1 : 0).Append(',')
                      .Append(F(v.Free.x)).Append(',').Append(F(v.Free.y)).Append(',').Append(F(v.Free.width)).Append(',').Append(F(v.Free.height)).Append('\n');
                }
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        private static string F(float f) { return f.ToString("0.####", CultureInfo.InvariantCulture); }

        private static void LoadLayout()
        {
            try
            {
                if (!File.Exists(FilePath) || !Ready()) return;
                string[] lines = File.ReadAllLines(FilePath, Encoding.UTF8);
                int count = 0;
                List<string> vs = new List<string>();
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("count=")) int.TryParse(lines[i].Substring(6), out count);
                    else if (lines[i].StartsWith("v") && lines[i].IndexOf('=') > 0) vs.Add(lines[i].Substring(lines[i].IndexOf('=') + 1));
                }
                if (count < 2 || vs.Count < 2) return;
                count = Mathf.Min(count, MaxViews);
                View first = new View(); CaptureInto(first);
                for (int i = 0; i < count && i < vs.Count; i++)
                {
                    View v = Clone(first);
                    v.Mirror = true;
                    string[] p = vs[i].Split(',');
                    if (p.Length >= 5)
                    {
                        v.Floating = p[0] == "1";
                        float x, y, w, h;
                        if (float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                            && float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out w) && float.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out h))
                            v.Free = new Rect(x, y, Mathf.Clamp(w, 0.1f, 1f), Mathf.Clamp(h, 0.1f, 1f));
                    }
                    Views.Add(v);
                }
                Active = Views.Count > 0 ? Views[0] : null;
                _gridDirty = true;
            }
            catch (Exception e) { BtcLog.Warn("MultiCam", "layout load: " + e.Message); }
        }

        // ------------------------------------------------------------------ DEV / info

        internal static string Info()
        {
            StringBuilder sb = new StringBuilder("views=" + Views.Count + " active=" + Views.IndexOf(Active) + " max=" + Views.IndexOf(_max));
            for (int i = 0; i < Views.Count; i++)
            {
                View v = Views[i];
                sb.Append(" [").Append(i).Append(v.Floating ? "F " : " ").Append(RectOf(v)).Append(" pos=").Append(v.Pos).Append(']');
            }
            return sb.ToString();
        }
    }

    /// <summary>Runs before every other script of the frame (hover, swap, strips).</summary>
    [DefaultExecutionOrder(-32000)]
    internal sealed class BtcMultiCamEarly : MonoBehaviour
    {
        private void Update()
        {
            try { BtcMultiCam.EarlyTick(); }
            catch (Exception e) { BtcLog.Error("MultiCam", "early: " + e); }
        }

        private void OnGUI()
        {
        	if (useGUILayout) useGUILayout = false; // no GUILayout here: skip Unity's per-event layout pass (garbage)
            try { BtcMultiCam.Draw(); }
            catch (Exception e) { BtcLog.Error("MultiCam", "gui: " + e.Message); }
        }

        private void OnDestroy() { BtcMultiCam.Shutdown(); }
    }

    /// <summary>Runs after every other LateUpdate (renders the other views).</summary>
    [DefaultExecutionOrder(32000)]
    internal sealed class BtcMultiCamLate : MonoBehaviour
    {
        private void LateUpdate() { BtcMultiCam.LateTick(); }
    }
}
