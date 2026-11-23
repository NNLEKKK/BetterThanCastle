using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Input;
using Brix.Network;
using Brix.UI.GameSelector;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// N13 (BC ON, host AI, per player): a CHOPPING BLOCK fetches logs lying on the floor only inside its radius (reworked 2026-11-23: other
    /// stations and builders are vanilla again). Every block can get its own radius (select it, hold the "station_radius" bind and turn the
    /// wheel, a ring shows the area while it is selected); a block without one uses its owner's Orders default "chop_block_radius" (15,
    /// host learns clients' defaults through BtcPlayerPrefs msg 134). Items in storages, racks and stations are never limited. 0 = no limit.
    /// Filter: Knowledge.ClosestObjectToPickup sets the context, Knowledge._ClosestObjectToPickup_Test rejects far floor items
    /// (Patches/StationRadiusPatches.cs).
    /// Wire (BC ON only): 121 client->host "stationNetId|radius" (-1 = back to the default); 122 host->clients "id:r;id:r;...".
    /// OFF: no handlers, no sends, no filter. Saves: btc-stationradius.txt "x y z r" in the save folder (host).
    /// </summary>
    internal static class BtcStationRadius
    {
        internal const short MsgSet = 121, MsgState = 122;
        internal const string FileName = "btc-stationradius.txt";
        private const int MaxR = 100;
        private static readonly Dictionary<uint, int> Own = new Dictionary<uint, int>();

        /// <summary>Only the chopping block has a radius (ChoppingBlock is internal: matched by type name).</summary>
        internal static bool IsChop(CraftingStation st) { return st != null && st.GetType().Name == "ChoppingBlock"; }

        /// <summary>Radius of a block without its own value: the owner's Orders default (host: BtcPlayerPrefs, client: own setting).</summary>
        private static int DefaultOf(CraftingStation st)
        {
            if (NetworkServer.active) return BtcPlayerPrefs.For(st.gameObject).ChopR;
            return BtcPlayerPrefs.LocalChopR;
        }

        private static uint IdOf(Component c)
        {
            NetworkIdentity ni = c != null ? c.GetComponent<NetworkIdentity>() : null;
            return ni != null ? ni.netId.Value : 0u;
        }

        internal static int RadiusOf(CraftingStation st)
        {
            int r;
            uint id = IdOf(st);
            return id != 0u && Own.TryGetValue(id, out r) ? r : DefaultOf(st);
        }

        internal static bool HasOwn(CraftingStation st) { return Own.ContainsKey(IdOf(st)); }

        // ------------------------------------------------------------------ fetch filter (host)
        private static bool _ctx;
        private static Vector3 _c;
        private static float _r2;
        private static int _logged;

        internal static void Begin(Labor l)
        {
            _ctx = false;
            if (l == null || !BtcNetworkGate.Enabled || !NetworkServer.active) return;
            CraftingStation st = l.Operable as CraftingStation;
            if (!IsChop(st)) st = _deciding;   // a free worker deciding on its own: it is not hired at the block yet
            if (st == null) return;
            int r = RadiusOf(st);
            if (r <= 0) return;
            _c = st.transform.position;
            _r2 = r * r;
            _ctx = true;
            if (_logged < 3) { _logged++; BtcLog.Info("StationR", "fetch limited: " + "chopping block " + st.name + " r=" + r + (st == _deciding ? " (own decision)" : "")); }
        }

        internal static void End() { _ctx = false; }

        // A worker picks station work on its own through CraftingStation.DecideWhatToDo -> CanFetch(labor) BEFORE it is hired (Operable is
        // still empty), so the block is passed as context here (Patches/StationRadiusPatches.cs, CanFetch prefix/postfix/finalizer).
        private static CraftingStation _deciding;
        private static int _loggedFar;

        internal static void BeginDecide(CraftingStation st) { _deciding = IsChop(st) && BtcNetworkGate.Enabled && NetworkServer.active ? st : null; }

        internal static void EndDecide() { _deciding = null; }

        /// <summary>CanFetch postfix: true = the log it would fetch lies on the floor outside the block's radius (the labor's resource
        /// cache may hold a result found earlier in the same decision frame without the filter).</summary>
        internal static bool FetchTooFar(CraftingStation st, Labor l)
        {
            if (_deciding == null || st != _deciding || l == null) return false;
            int r = RadiusOf(st);
            if (r <= 0) return false;
            Description d = st.StuffToFetch();
            GameObject o = d != null ? l.Find(d) : null;
            if (o == null) return false;
            Transform p = o.transform.parent;
            if (p != null && p.GetComponentInParent<Brix.Game.Semantique.Recepteur>() != null) return false;   // in a storage or station
            Vector3 a = o.transform.position, c = st.transform.position;
            float dx = a.x - c.x, dz = a.z - c.z;
            if (dx * dx + dz * dz <= r * r) return false;
            if (_loggedFar < 3) { _loggedFar++; BtcLog.Info("StationR", "own decision refused: log too far for " + st.name + " r=" + r); }
            return true;
        }

        /// <summary>true = a floor item outside the current radius (storages and stations have a recepteur and pass).</summary>
        internal static bool Reject(IGameComponent gc)
        {
            if (!_ctx || gc == null || gc.recepteur != null) return false;
            GameObject go = gc.GameObject;
            if (go == null) return false;
            Vector3 p = go.transform.position;
            float dx = p.x - _c.x, dz = p.z - _c.z;
            return dx * dx + dz * dz > _r2;
        }

        // ------------------------------------------------------------------ input + look (local)
        private static List<GameObject> Sel = new List<GameObject>();
        private static readonly List<CraftingStation> Shown = new List<CraftingStation>();
        private static float _selAt;
        private static bool _hooked;
        private static Material _mat;
        private static GUIStyle _lab;

        /// <summary>CameraBookmarkHotkey.Update.</summary>
        private static bool _muted;
        private static float _savedZoom = 1f;

        /// <summary>true while the bind is held over a selected station: the wheel belongs to the radius (camera zoom is muted).</summary>
        internal static bool WheelTaken { get; private set; }

        internal static void Tick(CameraScript cam)
        {
            bool taken = false;
            try { taken = TickCore(); }
            finally
            {
                WheelTaken = taken;
                if (cam != null && taken != _muted)
                {
                    if (taken) { _savedZoom = cam.ZoomSpeedMod; cam.ZoomSpeedMod = 0f; }
                    else if (cam.ZoomSpeedMod == 0f) cam.ZoomSpeedMod = _savedZoom;
                    _muted = taken;
                }
            }
        }

        private static bool TickCore()
        {
            if (!BtcNetworkGate.Enabled) { Shown.Clear(); return false; }
            if (!_hooked) { _hooked = true; Camera.onPostRender += OnPost; }
            try
            {
                if (Time.unscaledTime >= _selAt)
                {
                    _selAt = Time.unscaledTime + 0.2f;
                    Shown.Clear(); Sel.Clear();
                    UIGameSelector.GetSelectedEverything(ref Sel);
                    for (int i = 0; i < Sel.Count && Shown.Count < 8; i++)
                    {
                        CraftingStation st = Sel[i] != null ? Sel[i].GetComponent<CraftingStation>() : null;
                        if (IsChop(st) && User.LocalUser != null && Affiliation.IsSameOrNeutral(User.LocalUser.faction, st.faction)) Shown.Add(st);
                    }
                }
                if (Shown.Count == 0 || BtcHotkeys.MenuOpen) return false;
                KeyCode[] keys = BtcHotkeys.GetKeys("station_radius", null);
                if (keys == null || keys.Length == 0) return false;
                for (int i = 0; i < keys.Length; i++) if (!Input.GetKey(keys[i])) return false;
                float wheel = Input.mouseScrollDelta.y;
                if (wheel == 0f) return true;
                int step = (wheel > 0f ? 1 : -1) * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 5 : 1);
                for (int i = 0; i < Shown.Count; i++)
                {
                    if (Shown[i] == null) continue;
                    int r = Mathf.Clamp(RadiusOf(Shown[i]) + step, 0, MaxR);
                    Request(Shown[i], r);
                }
                BtcSfx.Tick();
                return true;
            }
            catch (Exception e) { BtcLog.Error("StationR", "Tick: " + e.Message); return false; }
        }

        private static void Request(CraftingStation st, int r)
        {
            uint id = IdOf(st);
            if (id == 0u) return;
            Own[id] = r;   // shown at once, the host confirms with its state
            if (NetworkServer.active) { Broadcast(); return; }
            UNetManager m = NetworkManager.singleton as UNetManager;
            if (m == null || m.LocalNetworkClient == null || !m.LocalNetworkClient.isConnected) return;
            m.LocalNetworkClient.Send(MsgSet, new StringMessage(id + "|" + r));
        }

        private static void OnPost(Camera cam)
        {
            if (Shown.Count == 0 || cam != Camera.main || BtcReplay.Mode == BtcReplay.State.Playing) return;
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
                Color col = BtcMenu.Accent; col.a = 0.8f;
                for (int i = 0; i < Shown.Count; i++)
                {
                    if (Shown[i] == null) continue;
                    int r = RadiusOf(Shown[i]);
                    if (r <= 0) continue;
                    Vector3 c = Shown[i].transform.position + Vector3.up * 0.08f;
                    const int N = 72;
                    GL.Begin(GL.QUADS);
                    GL.Color(col);
                    float r0 = r - 0.08f, r1 = r + 0.08f;
                    for (int k = 0; k < N; k++)
                    {
                        float a0 = k * Mathf.PI * 2f / N, a1 = (k + 1) * Mathf.PI * 2f / N;
                        float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0), c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);
                        GL.Vertex3(c.x + c0 * r0, c.y, c.z + s0 * r0);
                        GL.Vertex3(c.x + c0 * r1, c.y, c.z + s0 * r1);
                        GL.Vertex3(c.x + c1 * r1, c.y, c.z + s1 * r1);
                        GL.Vertex3(c.x + c1 * r0, c.y, c.z + s1 * r0);
                    }
                    GL.End();
                }
            }
            catch (Exception) { }
        }

        /// <summary>CameraBookmarkHotkey.OnGUI: radius label above each selected station.</summary>
        internal static void Draw()
        {
            if (Shown.Count == 0 || Event.current.type != EventType.Repaint || BtcReplay.Mode == BtcReplay.State.Playing) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            if (_lab == null)
            {
                _lab = new GUIStyle(GUI.skin.label);
                _lab.alignment = TextAnchor.MiddleCenter;
                _lab.fontStyle = FontStyle.Bold;
                _lab.normal.textColor = Color.white;
            }
            _lab.fontSize = Mathf.Max(11, Mathf.RoundToInt(Screen.height * 0.015f));
            for (int i = 0; i < Shown.Count; i++)
            {
                if (Shown[i] == null) continue;
                Vector3 p = cam.WorldToScreenPoint(Shown[i].transform.position + Vector3.up * 2.6f);
                if (p.z <= 0f) continue;
                int r = RadiusOf(Shown[i]);
                string t = (r > 0 ? BtcLang.Tr("Log pickup radius") + ": " + r : BtcLang.Tr("Log pickup radius") + ": " + BtcLang.Tr("no limit"))
                    + (HasOwn(Shown[i]) ? "" : " (" + BtcLang.Tr("default") + ")");
                Rect rc = new Rect(p.x - 160f, Screen.height - p.y - 12f, 320f, 24f);
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.Label(new Rect(rc.x + 1f, rc.y + 1f, rc.width, rc.height), t, _lab);
                GUI.color = Color.white;
                GUI.Label(rc, t, _lab);
            }
        }

        // ------------------------------------------------------------------ net
        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            Own.Clear();
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                if (NetworkServer.active)
                {
                    NetworkServer.UnregisterHandler(MsgSet);
                    NetworkServer.RegisterHandler(MsgSet, OnSetAtHost);
                }
                UNetManager m = NetworkManager.singleton as UNetManager;
                NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
                if (c != null && !NetworkServer.active)
                {
                    c.UnregisterHandler(MsgState);
                    c.RegisterHandler(MsgState, OnStateAtClient);
                }
            }
            catch (Exception e) { BtcLog.Error("StationR", "RegisterHandlers: " + e); }
        }

        private static void OnSetAtHost(NetworkMessage nm)
        {
            try
            {
                if (!BtcNetworkGate.Enabled) return;
                string[] a = nm.ReadMessage<StringMessage>().value.Split('|');
                uint id; int r;
                if (a.Length < 2 || !uint.TryParse(a[0], out id) || !int.TryParse(a[1], out r)) return;
                NetworkIdentity ni;
                if (!NetworkServer.objects.TryGetValue(new NetworkInstanceId(id), out ni) || ni == null) return;
                CraftingStation st = ni.GetComponent<CraftingStation>();
                if (!IsChop(st)) return;
                User u = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(nm.conn) : null;
                if (st == null || u == null || st.faction == null || !Affiliation.IsSameOrNeutral(u.faction, st.faction)) return;
                if (r < 0) Own.Remove(id); else Own[id] = Mathf.Clamp(r, 0, MaxR);
                Broadcast();
            }
            catch (Exception e) { BtcLog.Error("StationR", "OnSetAtHost: " + e); }
        }

        private static string Encode()
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<uint, int> kv in Own) { if (sb.Length > 0) sb.Append(';'); sb.Append(kv.Key).Append(':').Append(kv.Value); }
            return sb.ToString();
        }

        private static void Broadcast()
        {
            if (!NetworkServer.active || !BtcNetworkGate.Enabled) return;
            try
            {
                StringMessage msg = new StringMessage(Encode());
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    if (c != null && c.isConnected && !BtcNetManager.IsLocalConnection(c)) c.Send(MsgState, msg);
                }
            }
            catch (Exception e) { BtcLog.Error("StationR", "Broadcast: " + e); }
        }

        private static void OnStateAtClient(NetworkMessage nm)
        {
            try
            {
                string s = nm.ReadMessage<StringMessage>().value;
                Own.Clear();
                if (string.IsNullOrEmpty(s)) return;
                string[] parts = s.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    string[] kv = parts[i].Split(':');
                    uint id; int r;
                    if (kv.Length == 2 && uint.TryParse(kv[0], out id) && int.TryParse(kv[1], out r)) Own[id] = r;
                }
            }
            catch (Exception e) { BtcLog.Error("StationR", "OnStateAtClient: " + e); }
        }

        // ------------------------------------------------------------------ saves (host)
        private static List<Vector4> _pending;
        private static float _applyAt, _deadline;

        internal static void OnSave(string folder)
        {
            if (!NetworkServer.active || !BtcNetworkGate.Enabled || string.IsNullOrEmpty(folder)) return;
            string file = System.IO.Path.Combine(folder, FileName);
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            int n = 0;
            foreach (KeyValuePair<uint, int> kv in Own)
            {
                NetworkIdentity ni;
                if (!NetworkServer.objects.TryGetValue(new NetworkInstanceId(kv.Key), out ni) || ni == null) continue;
                Vector3 p = ni.transform.position;
                sb.Append(p.x.ToString("F2", c)).Append(' ').Append(p.y.ToString("F2", c)).Append(' ').Append(p.z.ToString("F2", c)).Append(' ').Append(kv.Value).Append('\n');
                n++;
            }
            if (n == 0) { if (File.Exists(file)) File.Delete(file); return; }
            File.WriteAllText(file, sb.ToString());
            BtcLog.Info("StationR", "saved " + n + " station radius(es)");
        }

        internal static void OnLoad(string folder)
        {
            _pending = null;
            if (!NetworkServer.active || !BtcNetworkGate.Enabled || string.IsNullOrEmpty(folder)) return;
            string file = System.IO.Path.Combine(folder, FileName);
            if (!File.Exists(file)) return;
            CultureInfo c = CultureInfo.InvariantCulture;
            List<Vector4> list = new List<Vector4>();
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] t = lines[i].Split(' ');
                float x, y, z; int r;
                if (t.Length == 4 && float.TryParse(t[0], NumberStyles.Float, c, out x) && float.TryParse(t[1], NumberStyles.Float, c, out y)
                    && float.TryParse(t[2], NumberStyles.Float, c, out z) && int.TryParse(t[3], out r))
                    list.Add(new Vector4(x, y, z, r));
            }
            if (list.Count == 0) return;
            _pending = list;
            _applyAt = Time.unscaledTime + 2f;
            _deadline = Time.unscaledTime + 30f;
            BtcLog.Info("StationR", "save has " + list.Count + " station radius(es), restoring after the load");
        }

        /// <summary>BtcTimer.Tick: match saved radii to the nearest station (within 1.5 blocks), retry for 30 s.</summary>
        internal static void LoadTick()
        {
            if (_pending == null || Time.unscaledTime < _applyAt) return;
            if (!NetworkServer.active) { _pending = null; return; }
            try
            {
                _applyAt = Time.unscaledTime + 1f;
                CraftingStation[] all = UnityEngine.Object.FindObjectsOfType<CraftingStation>();
                List<Vector4> left = new List<Vector4>();
                int done = 0;
                for (int k = 0; k < _pending.Count; k++)
                {
                    CraftingStation best = null;
                    float bestD = 1.5f * 1.5f;
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (!IsChop(all[i])) continue;
                        float d = (all[i].transform.position - (Vector3)_pending[k]).sqrMagnitude;
                        if (d < bestD) { bestD = d; best = all[i]; }
                    }
                    uint id = IdOf(best);
                    if (id == 0u) { left.Add(_pending[k]); continue; }
                    Own[id] = Mathf.Clamp(Mathf.RoundToInt(_pending[k].w), 0, MaxR);
                    done++;
                }
                if (done > 0) Broadcast();
                _pending = left.Count > 0 && Time.unscaledTime < _deadline ? left : null;
                if (_pending == null) BtcLog.Info("StationR", "restore finished, " + done + " this pass, " + left.Count + " not found");
            }
            catch (Exception e) { _pending = null; BtcLog.Error("StationR", "LoadTick: " + e); }
        }
    }
}
