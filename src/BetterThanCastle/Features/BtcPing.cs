using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Brix.Game;
using Brix.Input;
using Brix.Network;
using Brix.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
    /// <summary>
    /// Team pings (BetterConnection ON only). The "Ping allies" hotkey (Advanced Hotkeys > Orders, unassigned by default) drops a mark on the point under the
    /// cursor (3D hit, or the surface under the cursor on the mini map). The mark has the colour of the player who placed it: pulsing rings on the ground,
    /// a "!" icon above it (clamped to the screen edge with an arrow when off screen), a pulse on the mini map and a short chime. Seen by the placer, allied
    /// factions and spectators; the recording host writes it into the replay timeline (token Q), playback shows every ping.
    /// Look/sound/limits are personal (Visuals > World overlays > Team pings, BtcMenuState "ping:*").
    /// Wire (BC ON only): 117 client->host "x|y|z"; 117 host->allied clients and spectators "x|y|z|RRGGBB|key|name". OFF: no handler, no send.
    /// </summary>
    internal sealed class BtcPing : MonoBehaviour
    {
        internal const short MsgPing = 117;
        private const int RecCap = 300;
        private const float HostMinGap = 0.03f;   // only a flood guard: the sender limits bursts itself

        private sealed class Ping
        {
            internal Vector3 P;
            internal Color C;
            internal string Name, Key;
            internal float Born, ReplayT = -1f;
            internal GameObject Go;
            internal MiniMap.Trackable Tr;
        }

        private struct RecEv { internal float T; internal Vector3 P; internal string Col; }

        private static BtcPing _inst;
        private static readonly List<Ping> Active = new List<Ping>();
        private static readonly List<RecEv> Rec = new List<RecEv>();
        private static readonly List<RecEv> Play = new List<RecEv>();
        private static readonly Dictionary<int, float> HostLast = new Dictionary<int, float>();
        private static float _lastSend = -99f, _lastPc = -1f, _lockUntil = -99f, _burstStart = -99f;
        private static int _burstN;
        private const float BurstWindow = 3f;   // pings come freely; a burst of MaxPer inside this window earns the pause (Cooldown)
        private static Material _mat;
        private static Texture2D _disc, _tri, _ringTex;
        private static Sprite _ringSprite;
        private static GUIStyle _bang, _name;
        private static MiniMap _mm;
        private static IntPtr _snd = IntPtr.Zero;
        private static int _sndVol = -1;
        private static bool _loggedFirst;

        // ------------------------------------------------------------------ personal settings (Visuals > World overlays > Team pings)
        internal static bool ShowOthers { get { return BtcMenuState.Bool("ping:show", true); } }
        internal static float Life { get { return Mathf.Clamp(BtcMenuState.Int("ping:life", 50), 10, 300) * 0.1f; } }
        internal static float Cooldown { get { return Mathf.Clamp(BtcMenuState.Int("ping:cd", 100), 0, 500) * 0.01f; } }
        internal static int Volume { get { return Mathf.Clamp(BtcMenuState.Int("ping:vol", 70), 0, 100); } }
        internal static float Size { get { return Mathf.Clamp(BtcMenuState.Int("ping:size", 100), 50, 250) * 0.01f; } }
        internal static int MaxPer { get { return Mathf.Clamp(BtcMenuState.Int("ping:max", 5), 0, 10); } }
        internal static bool EdgeArrow { get { return BtcMenuState.Bool("ping:arrow", true); } }

        internal static int ActiveCount { get { return Active.Count; } }
        private static bool On { get { return BtcNetworkGate.Enabled; } }
        private static bool Playing { get { return BtcReplay.Mode == BtcReplay.State.Playing; } }

        internal static void Init()
        {
            if (_inst != null) return;
            GameObject go = new GameObject("BtcPing");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _inst = go.AddComponent<BtcPing>();
            Camera.onPostRender += OnPost;
            SceneManager.sceneLoaded += (s, m) => { ClearActive(); _lastPc = -1f; };
        }

        // ------------------------------------------------------------------ placing (hotkey)
        internal static void Place()
        {
            if (!On || Playing) return;
            try
            {
                float now = Time.unscaledTime;
                if (now < _lockUntil) return;   // spam pause
                int burst = Mathf.Max(MaxPer, 1);
                if (now - _burstStart > BurstWindow) { _burstStart = now; _burstN = 0; }
                if (++_burstN >= burst) { _lockUntil = now + Cooldown; _burstStart = -99f; }
                Vector3 p;
                if (CursorPoint(out p)) PlaceAt(p);
            }
            catch (Exception e) { BtcLog.Error("Ping", "Place: " + e); }
        }

        internal static void PlaceAt(Vector3 p)
        {
            if (!On || Playing) return;
            try
            {
                _lastSend = Time.unscaledTime;
                User u = User.LocalUser;
                Color col = u != null && u.faction != null ? u.faction.FactionColor : Color.white;
                string name = Clean(u != null ? u.ScreenName : "");
                Show(p, col, "self", name, true);
                if (NetworkServer.active) HostRelay(p, col, u != null ? u.faction : null, -1, name);
                else SendToHost(p);
                if (!_loggedFirst) { _loggedFirst = true; BtcLog.Info("Ping", "placed at " + p.ToString("F1") + " host=" + NetworkServer.active); }
            }
            catch (Exception e) { BtcLog.Error("Ping", "PlaceAt: " + e); }
        }

        /// <summary>World point under the cursor: the mini map surface when hovering it, else the first solid hit of the camera ray.</summary>
        private static bool CursorPoint(out Vector3 p)
        {
            p = Vector3.zero;
            if (CursorPicker.IsHoveringMinimap && MinimapPoint(out p)) return true;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
            Camera cam = Camera.main;
            if (cam == null) return false;
            RaycastHit hit;
            if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit, 1000f, -1, QueryTriggerInteraction.Ignore)) return false;
            p = hit.point;
            return true;
        }

        /// <summary>World surface point under the cursor when it hovers the mini map (camera binds reuse it).</summary>
        internal static bool MinimapCursorPoint(out Vector3 p)
        {
            p = Vector3.zero;
            return CursorPicker.IsHoveringMinimap && MinimapPoint(out p);
        }

        private static bool MinimapPoint(out Vector3 p)
        {
            p = Vector3.zero;
            if (_mm == null) _mm = FindObjectOfType<MiniMap>();
            if (_mm == null) return false;
            RectTransform rt = _mm.GetComponent<RectTransform>();
            if (rt == null) return false;
            Canvas cv = _mm.GetComponentInParent<Canvas>();
            Camera ec = cv == null || cv.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.worldCamera;
            Vector2 v;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, Input.mousePosition, ec, out v)) return false;
            // same mapping as MiniMap.GetSurfaceAt
            v = new Vector2(Mathf.Clamp01(v.x / rt.rect.size.x + 0.5f), Mathf.Clamp01(v.y / rt.rect.size.y + 0.5f));
            Bounds b = _mm.mapBounds;
            Vector3 q = new Vector3(v.x * b.size.x + b.min.x, CameraScript.Target.y, v.y * b.size.z + b.min.z);
            p = Brix.Engine.Architecte.SurfaceRaycast(q, 50, false);
            return true;
        }

        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(24);
            bool tag = false;
            for (int i = 0; i < s.Length && sb.Length < 24; i++)
            {
                char ch = s[i];
                if (ch == '<') { tag = true; continue; }
                if (ch == '>') { tag = false; continue; }
                if (tag || ch == '|' || ch == ';' || ch == ',' || char.IsControl(ch)) continue;
                sb.Append(ch);
            }
            return sb.ToString().Trim();
        }

        private static string F(float v) { return v.ToString("F2", CultureInfo.InvariantCulture); }

        private static void SendToHost(Vector3 p)
        {
            UNetManager m = NetworkManager.singleton as UNetManager;
            if (m == null || m.LocalNetworkClient == null || !m.LocalNetworkClient.isConnected) return;
            m.LocalNetworkClient.Send(MsgPing, new StringMessage(F(p.x) + "|" + F(p.y) + "|" + F(p.z)));
        }

        // ------------------------------------------------------------------ network
        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                HostLast.Clear();
                if (NetworkServer.active)
                {
                    NetworkServer.UnregisterHandler(MsgPing);
                    NetworkServer.RegisterHandler(MsgPing, OnPingAtHost);
                }
                UNetManager m = NetworkManager.singleton as UNetManager;
                NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
                if (c != null && !NetworkServer.active)
                {
                    c.UnregisterHandler(MsgPing);
                    c.RegisterHandler(MsgPing, OnPingAtClient);
                }
            }
            catch (Exception e) { BtcLog.Error("Ping", "RegisterHandlers: " + e); }
        }

        private static bool ParseP(string[] a, out Vector3 p)
        {
            p = Vector3.zero;
            CultureInfo c = CultureInfo.InvariantCulture;
            float x, y, z;
            if (a.Length < 3 || !float.TryParse(a[0], NumberStyles.Float, c, out x) || !float.TryParse(a[1], NumberStyles.Float, c, out y) || !float.TryParse(a[2], NumberStyles.Float, c, out z)) return false;
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) || Mathf.Abs(x) > 5000f || Mathf.Abs(y) > 5000f || Mathf.Abs(z) > 5000f) return false;
            p = new Vector3(x, y, z);
            return true;
        }

        private static void OnPingAtHost(NetworkMessage nm)
        {
            try
            {
                string[] a = nm.ReadMessage<StringMessage>().value.Split('|');
                if (!On || !NetworkServer.active) return;
                int id = nm.conn.connectionId;
                float now = Time.unscaledTime, last;
                if (HostLast.TryGetValue(id, out last) && now - last < HostMinGap) return;
                HostLast[id] = now;
                Vector3 p;
                if (!ParseP(a, out p)) return;
                User u = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(nm.conn) : null;
                if (u == null) return;
                Color col = u.faction != null ? u.faction.FactionColor : Color.white;
                string name = Clean(u.ScreenName);
                HostRelay(p, col, u.faction, id, name);
                // the host's own screen
                bool sees = Sees(User.LocalUser, u.faction);
                if (sees) Show(p, col, "c" + id, name, false);
                BtcLog.Info("Ping", "from c" + id + " host sees=" + sees);
            }
            catch (Exception e) { BtcLog.Error("Ping", "OnPingAtHost: " + e); }
        }

        /// <summary>Does this viewer see pings of faction f? Same or allied faction; spectators see everything; a spectator's ping goes to spectators only.</summary>
        private static bool Sees(User viewer, Faction f)
        {
            if (viewer == null) return false;
            Faction v = viewer.faction;
            bool vSpec = v == null || v.IsSpectator;
            bool fSpec = f == null || f.IsSpectator;
            if (vSpec) return true;
            if (fSpec) return false;
            return v == f || v.GetRelation(f) == Affiliation.Relation.Allied;
        }

        /// <summary>Host: send to every other allied / spectator client, record for the replay.</summary>
        private static void HostRelay(Vector3 p, Color col, Faction f, int fromConn, string name)
        {
            string hex = ColorUtility.ToHtmlStringRGB(col);
            float t = BtcReplay.RecTime;
            if (t >= 0f && Rec.Count < RecCap) Rec.Add(new RecEv { T = t, P = p, Col = hex });
            if (UNetManager.instance == null) return;
            string key = fromConn < 0 ? "h" : "c" + fromConn;
            StringMessage msg = new StringMessage(F(p.x) + "|" + F(p.y) + "|" + F(p.z) + "|" + hex + "|" + key + "|" + name);
            int sent = 0;
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                NetworkConnection c = NetworkServer.connections[i];
                if (c == null || !c.isConnected || c.connectionId == fromConn || BtcNetManager.IsLocalConnection(c) || BtcReplay.IsRec(c)) continue;
                User u = UNetManager.instance.GetUserFromConnection(c);
                if (u == null || !Sees(u, f)) continue;
                c.Send(MsgPing, msg);
                sent++;
            }
            BtcLog.Info("Ping", "relay from " + key + " to " + sent + " client(s)");
        }

        private static void OnPingAtClient(NetworkMessage nm)
        {
            try
            {
                string[] a = nm.ReadMessage<StringMessage>().value.Split('|');
                if (!On || a.Length < 6) return;
                Vector3 p;
                Color col;
                if (!ParseP(a, out p) || !ColorUtility.TryParseHtmlString("#" + a[3], out col)) return;
                Show(p, col, a[4], a[5], false);
                BtcLog.Info("Ping", "got from " + a[4] + " col=" + a[3] + " active=" + Active.Count);
            }
            catch (Exception e) { BtcLog.Error("Ping", "OnPingAtClient: " + e); }
        }

        // ------------------------------------------------------------------ show
        private static void Show(Vector3 p, Color col, string key, string name, bool own)
        {
            if (!own && !ShowOthers) return;
            AddActive(p, col, key, name, Time.unscaledTime, -1f);
            Chime();
        }

        private static void AddActive(Vector3 p, Color col, string key, string name, float born, float replayT)
        {
            int max = MaxPer;
            if (max > 0)
            {
                int n = 0, oldest = -1;
                for (int i = 0; i < Active.Count; i++)
                {
                    if (Active[i].Key != key) continue;
                    n++;
                    if (oldest < 0) oldest = i;   // Active is in creation order
                }
                if (n >= max && oldest >= 0) Remove(oldest);
            }
            col.a = 1f;
            Ping g = new Ping { P = p, C = col, Key = key, Name = name, Born = born, ReplayT = replayT };
            try
            {
                g.Go = new GameObject("BtcPingMark");
                g.Go.hideFlags = HideFlags.HideAndDontSave;
                g.Go.transform.position = p;
                g.Tr = MiniMap.AddTrackable(g.Go.transform, RingSprite(), col);
            }
            catch (Exception e) { BtcLog.Warn("Ping", "minimap mark: " + e.Message); }
            Active.Add(g);
        }

        private static void Remove(int i)
        {
            Ping g = Active[i];
            try { if (g.Tr != null && g.Tr.Transform != null) MiniMap.RemoveTrackable(g.Tr); } catch { }
            if (g.Go != null) Destroy(g.Go);
            Active.RemoveAt(i);
        }

        private static void ClearActive()
        {
            for (int i = Active.Count - 1; i >= 0; i--) Remove(i);
        }

        private static float Age(Ping g)
        {
            return g.ReplayT >= 0f ? BtcReplay.PlayClock - g.ReplayT : Time.unscaledTime - g.Born;
        }

        private static void Chime()
        {
            int vol = Volume;
            if (vol <= 0) return;
            if (vol != _sndVol || _snd == IntPtr.Zero)
            {
                _sndVol = vol;
                float a = vol * 0.01f * 0.3f;
                // short bright "ding": two partials with a soft attack and a ringing decay
                float[] s1 = BtcSfx.Soft(1568f, 1568f, 0.32f, 11f, a, 0.002f, 7000f);
                float[] s2 = BtcSfx.Soft(2349f, 2349f, 0.32f, 16f, a * 0.45f, 0.002f, 7000f);
                for (int i = 0; i < s1.Length && i < s2.Length; i++) s1[i] += s2[i];
                _snd = BtcSfx.Pin(s1);
            }
            BtcSfx.PlayPtr(_snd);
        }

        /// <summary>Settings preview (volume slider).</summary>
        internal static void PreviewSound() { Chime(); }

        // ------------------------------------------------------------------ replay timeline (BtcTimer "tl=" region)
        internal static void OnRecordStart() { Rec.Clear(); }

        internal static string Payload(int budget)
        {
            if (Rec.Count == 0 || budget <= 0) return "";
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder(Rec.Count * 30);
            for (int i = 0; i < Rec.Count; i++)
            {
                RecEv e = Rec[i];
                string s = "Q," + e.T.ToString("F1", c) + "," + e.P.x.ToString("F1", c) + "," + e.P.y.ToString("F1", c) + "," + e.P.z.ToString("F1", c) + "," + e.Col;
                if (sb.Length + s.Length + 1 > budget) break;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(s);
            }
            return sb.ToString();
        }

        internal static void ClearPlay() { Play.Clear(); _lastPc = -1f; ClearActive(); }

        internal static bool TryParse(string[] p, CultureInfo c)
        {
            if (p[0] != "Q") return false;
            if (p.Length < 6) return true;
            float t, x, y, z;
            if (!float.TryParse(p[1], NumberStyles.Float, c, out t) || !float.TryParse(p[2], NumberStyles.Float, c, out x)
                || !float.TryParse(p[3], NumberStyles.Float, c, out y) || !float.TryParse(p[4], NumberStyles.Float, c, out z)) return true;
            Play.Add(new RecEv { T = t, P = new Vector3(x, y, z), Col = p[5] });
            if (Play.Count > 1 && Play[Play.Count - 2].T > t) Play.Sort((a, b) => a.T.CompareTo(b.T));
            return true;
        }

        /// <summary>Playback: spawn the pings the clock passed; a seek or jump rebuilds the live set silently.</summary>
        private static void PlayTick()
        {
            if (Play.Count == 0) return;
            if (BtcReplay.Seeking) { if (Active.Count > 0) ClearActive(); _lastPc = -1f; return; }
            float pc = BtcReplay.PlayClock, life = Life;
            bool jump = _lastPc < 0f || pc < _lastPc || pc - _lastPc > 2f;
            if (jump)
            {
                ClearActive();
                for (int i = 0; i < Play.Count; i++)
                    if (Play[i].T <= pc && pc - Play[i].T < life) Spawn(Play[i], false);
            }
            else
            {
                for (int i = 0; i < Play.Count; i++)
                    if (Play[i].T > _lastPc && Play[i].T <= pc) Spawn(Play[i], true);
            }
            _lastPc = pc;
        }

        private static void Spawn(RecEv e, bool sound)
        {
            Color col;
            if (!ColorUtility.TryParseHtmlString("#" + e.Col, out col)) col = Color.white;
            AddActive(e.P, col, e.Col, "", 0f, e.T);
            if (sound) Chime();
            BtcLog.Info("Ping", "replay ping t=" + e.T.ToString("F1") + " live=" + sound);
        }

        // ------------------------------------------------------------------ frame
        private void Update()
        {
            try
            {
                if (Playing) PlayTick();
                else if (_lastPc >= 0f) { _lastPc = -1f; ClearActive(); }
                if (Active.Count == 0) return;
                float life = Life, now = Time.unscaledTime;
                for (int i = Active.Count - 1; i >= 0; i--)
                {
                    float age = Age(Active[i]);
                    if (age > life || age < -0.5f) Remove(i);
                }
                float sz = Size;
                for (int i = 0; i < Active.Count; i++)
                {
                    Ping g = Active[i];
                    if (g.Tr == null || g.Tr.TargetImage == null) continue;
                    float age = Age(g);
                    float pulse = Mathf.Sin(age * 7f);
                    Color c = g.C;
                    c.a = Fade(age, life) * (0.75f + 0.25f * pulse);
                    g.Tr.TargetImage.color = c;
                    float sc = 1.4f * sz * (1f + 0.25f * pulse);
                    g.Tr.TargetImage.rectTransform.localScale = new Vector3(sc, sc, 1f);
                    g.Tr.TargetImage.rectTransform.SetAsLastSibling();
                }
            }
            catch (Exception e) { BtcLog.Error("Ping", "Update: " + e.Message); ClearActive(); }
        }

        private static float Fade(float age, float life)
        {
            return Mathf.Clamp01(age / 0.12f) * Mathf.Clamp01((life - age) / 0.8f);
        }

        // world rings + beam (Camera.onPostRender, GL, drawn over the terrain)
        private static void OnPost(Camera cam)
        {
            if (Active.Count == 0 || cam != Camera.main) return;
            try
            {
                if (_mat == null)
                {
                    Shader s = Shader.Find("Hidden/Internal-Colored");
                    if (s == null) return;
                    _mat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
                    _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    _mat.SetInt("_Cull", 0);
                    _mat.SetInt("_ZWrite", 0);
                    _mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                }
                _mat.SetPass(0);
                float life = Life, sz = Size;
                for (int i = 0; i < Active.Count; i++)
                {
                    Ping g = Active[i];
                    float age = Age(g), f = Fade(age, life);
                    if (f <= 0f) continue;
                    Vector3 c = g.P + Vector3.up * 0.06f;
                    // two ripples expanding outwards, one fixed inner ring
                    for (int k = 0; k < 2; k++)
                    {
                        float ph = Mathf.Repeat(age / 1.1f + k * 0.5f, 1f);
                        Ring(c, Mathf.Lerp(0.35f, 2.8f, ph) * sz, 0.14f * sz, g.C, (1f - ph) * 0.85f * f);
                    }
                    Ring(c, 0.45f * sz, 0.12f * sz, g.C, 0.95f * f);
                    GL.Begin(GL.LINES);
                    Color bc = g.C; bc.a = 0.7f * f;
                    GL.Color(bc);
                    GL.Vertex(c); GL.Vertex(c + Vector3.up * 2.2f * sz);
                    GL.End();
                }
            }
            catch (Exception) { }
        }

        private static void Ring(Vector3 c, float r, float w, Color col, float a)
        {
            if (a <= 0.01f) return;
            col.a = a;
            const int N = 40;
            GL.Begin(GL.QUADS);
            GL.Color(col);
            float r0 = Mathf.Max(0f, r - w * 0.5f), r1 = r + w * 0.5f;
            for (int i = 0; i < N; i++)
            {
                float a0 = i * Mathf.PI * 2f / N, a1 = (i + 1) * Mathf.PI * 2f / N;
                float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0), c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);
                GL.Vertex3(c.x + c0 * r0, c.y, c.z + s0 * r0);
                GL.Vertex3(c.x + c0 * r1, c.y, c.z + s0 * r1);
                GL.Vertex3(c.x + c1 * r1, c.y, c.z + s1 * r1);
                GL.Vertex3(c.x + c1 * r0, c.y, c.z + s1 * r0);
            }
            GL.End();
        }

        // "!" icons (on screen above the mark; clamped to the edge with an arrow when off screen)
        private void OnGUI()
        {
            if (Active.Count == 0 || Event.current.type != EventType.Repaint) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            try
            {
                EnsureGui();
                float life = Life, sz = Size, sw = Screen.width, sh = Screen.height;
                bool arrow = EdgeArrow;
                Color old = GUI.color;
                Matrix4x4 m0 = GUI.matrix;
                for (int i = 0; i < Active.Count; i++)
                {
                    Ping g = Active[i];
                    float age = Age(g), f = Fade(age, life);
                    if (f <= 0f) continue;
                    float d = 30f * sz * (1f + 0.1f * Mathf.Sin(age * 7f));
                    Vector3 sp = cam.WorldToScreenPoint(g.P + Vector3.up * 2.6f * sz);
                    bool behind = sp.z < 0f;
                    if (behind) { sp.x = sw - sp.x; sp.y = sh - sp.y; }
                    Vector2 q = new Vector2(sp.x, sh - sp.y);
                    float mg = d * 0.5f + 8f;
                    bool off = behind || q.x < mg || q.x > sw - mg || q.y < mg || q.y > sh - mg;
                    if (off && !arrow) continue;
                    float ang = 0f;
                    if (off)
                    {
                        Vector2 ctr = new Vector2(sw * 0.5f, sh * 0.5f), dir = q - ctr;
                        if (dir.sqrMagnitude < 1f) dir = Vector2.down;
                        float em = d * 0.5f + 22f;
                        float k = Mathf.Min((sw * 0.5f - em) / Mathf.Max(0.001f, Mathf.Abs(dir.x)), (sh * 0.5f - em) / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
                        q = ctr + dir * k;
                        ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    }
                    Rect r = new Rect(q.x - d * 0.5f, q.y - d * 0.5f, d, d);
                    if (off)
                    {
                        float t = d * 0.55f;
                        GUIUtility.RotateAroundPivot(ang, q);
                        GUI.color = new Color(g.C.r, g.C.g, g.C.b, f);
                        GUI.DrawTexture(new Rect(q.x + d * 0.5f + 1f, q.y - t * 0.5f, t, t), _tri);
                        GUI.matrix = m0;
                    }
                    GUI.color = new Color(0f, 0f, 0f, 0.55f * f);
                    GUI.DrawTexture(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), _disc);
                    GUI.color = new Color(g.C.r, g.C.g, g.C.b, f);
                    GUI.DrawTexture(r, _disc);
                    _bang.fontSize = Mathf.Max(10, (int)(d * 0.78f));
                    GUI.color = new Color(0f, 0f, 0f, 0.6f * f);
                    GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), "!", _bang);
                    GUI.color = new Color(1f, 1f, 1f, f);
                    GUI.Label(r, "!", _bang);
                    if (!off && g.Name.Length > 0)
                    {
                        _name.fontSize = Mathf.Max(10, (int)(11f * sz));
                        Rect nr = new Rect(q.x - 80f, r.yMax + 1f, 160f, _name.fontSize + 6f);
                        GUI.color = new Color(0f, 0f, 0f, 0.7f * f);
                        GUI.Label(new Rect(nr.x + 1f, nr.y + 1f, nr.width, nr.height), g.Name, _name);
                        GUI.color = new Color(1f, 1f, 1f, 0.9f * f);
                        GUI.Label(nr, g.Name, _name);
                    }
                }
                GUI.matrix = m0;
                GUI.color = old;
            }
            catch (Exception e) { BtcLog.Error("Ping", "OnGUI: " + e.Message); }
        }

        private static void EnsureGui()
        {
            if (_bang == null)
            {
                _bang = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = false };
                _bang.normal.textColor = Color.white;
                _bang.padding = new RectOffset(0, 0, 0, 0);
                _name = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, richText = false, wordWrap = false, clipping = TextClipping.Clip };
                _name.normal.textColor = Color.white;
            }
            if (_disc == null) _disc = MakeTex(64, (x, y) => Mathf.Clamp01(30.5f - Mathf.Sqrt(x * x + y * y)));
            if (_tri == null) _tri = MakeTex(32, (x, y) => Mathf.Clamp01(Mathf.Min(14f - x, Mathf.Min((x + 14f) * 0.5f - y, (x + 14f) * 0.5f + y)) * 0.9f));
        }

        /// <summary>White alpha texture of size n; cov(x, y) gives coverage with (0, 0) at the centre.</summary>
        private static Texture2D MakeTex(int n, Func<float, float, float> cov)
        {
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color32[] px = new Color32[n * n];
            float h = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(cov(x - h, y - h)) * 255f));
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        private static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            // ring + centre dot for the mini map
            _ringTex = MakeTex(48, (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y);
                return Mathf.Max(Mathf.Clamp01(3f - Mathf.Abs(r - 19f)), Mathf.Clamp01(7f - r));
            });
            _ringSprite = Sprite.Create(_ringTex, new Rect(0f, 0f, 48f, 48f), new Vector2(0.5f, 0.5f), 100f);
            return _ringSprite;
        }
    }
}
