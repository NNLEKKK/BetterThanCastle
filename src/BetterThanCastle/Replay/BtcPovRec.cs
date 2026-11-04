using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Brix.Game;
using Brix.Input;
using Brix.Network;
using Brix.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Player cameras and cursors in replays. Recording (host / single player): the local camera, the world cursor, the mouse position, the mouse
    /// buttons and the pie menu state are sampled at 10 Hz; BetterConnection clients send the same samples to the host (msg 93, batches at 2 Hz,
    /// ~300 B/s each, BC ON only, nothing is sent with BC OFF). The tracks are stored delta-coded and deflated in ONE extra block of the replay file
    /// between the packet stream and the map snapshot (old readers ignore the trailing bytes of the stream; the length is the "V" token of the tl= header).
    /// Playback: every player's cursor flies over the map (arrow, name, click ripples, pie ring); watching a player in BtcPov also follows their camera
    /// and shows their cursor at the recorded screen position. Local only: no wire traffic in playback.
    /// </summary>
    internal static class BtcPovRec
    {
        internal const short Msg = 93;
        private const float SampleEvery = 0.2f;
        private const float SendEvery = 0.5f;

        internal struct S
        {
            internal int T;                 // ms
            internal int Cx, Cy, Cz;        // camera position, 1/8 voxel
            internal int Rx, Ry, Rz;        // camera euler, 0.1 deg
            internal int D;                 // camera distance, 0.1
            internal int Wx, Wy, Wz;        // cursor world position, 1/8 voxel
            internal int Mu, Mv;            // mouse on screen, 0..1000 (v up)
            internal int Fl;                // 1 LMB, 2 RMB, 4 MMB, 8 pie open, 16 world cursor valid
            internal int Pr;                // pie opening progress 0..255 (0 = none)
        }

        internal sealed class Track
        {
            internal int Fac;
            internal string Name, Col;
            internal List<S> L = new List<S>();
            internal int Hint;
        }

        private static readonly Dictionary<int, Track> RecTracks = new Dictionary<int, Track>();
        internal static readonly List<Track> Play = new List<Track>();
        private static byte[] _pendingBlob;
        private static int _blobLen;
        private static float _nextSample, _nextSend, _nextPie;
        private static readonly List<S> NetBuf = new List<S>();
        private static bool _clickRipplesInit;

        // ------------------------------------------------------------------ capture
        private static int Q8(float v) { return Mathf.RoundToInt(v * 8f); }

        private static bool Capture(out S s)
        {
            s = new S();
            CameraScript cam = CameraScript.Instance;
            if (cam == null) return false;
            Vector3 p = cam.transform.position;
            Vector3 e = cam.transform.rotation.eulerAngles;
            s.Cx = Q8(p.x); s.Cy = Q8(p.y); s.Cz = Q8(p.z);
            s.Rx = Mathf.RoundToInt(e.x * 10f); s.Ry = Mathf.RoundToInt(e.y * 10f); s.Rz = Mathf.RoundToInt(e.z * 10f);
            s.D = Mathf.RoundToInt(cam.Distance * 10f);
            int fl = 0;
            if (CursorPicker.IsHoveringTerrain)
            {
                Vector3 w = CursorPicker.TerrainHovered;
                s.Wx = Q8(w.x); s.Wy = Q8(w.y); s.Wz = Q8(w.z);
                fl |= 16;
            }
            Vector3 m = UnityEngine.Input.mousePosition;
            s.Mu = Mathf.Clamp(Mathf.RoundToInt(m.x / Mathf.Max(1, Screen.width) * 1000f), 0, 1000);
            s.Mv = Mathf.Clamp(Mathf.RoundToInt(m.y / Mathf.Max(1, Screen.height) * 1000f), 0, 1000);
            if (UnityEngine.Input.GetMouseButton(0)) fl |= 1;
            if (UnityEngine.Input.GetMouseButton(1)) fl |= 2;
            if (UnityEngine.Input.GetMouseButton(2)) fl |= 4;
            if (RadialLayoutGroup.PieMenuIsOpen) fl |= 8;
            float pr = RadialLayoutGroup.OpenProcessProgression;
            s.Pr = pr < 0f ? 0 : Mathf.Clamp(1 + Mathf.RoundToInt(pr * 254f), 0, 255);
            s.Fl = fl;
            return true;
        }

        private static string LocalName()
        {
            UNetManager m = NetworkManager.singleton as UNetManager;
            string n = m != null ? m.LocalPlayerName : null;
            return string.IsNullOrEmpty(n) ? "?" : n.Replace('|', ' ');
        }

        private static bool Same(S a, S b)
        {
            return a.Cx == b.Cx && a.Cy == b.Cy && a.Cz == b.Cz && a.Rx == b.Rx && a.Ry == b.Ry && a.Rz == b.Rz && a.D == b.D
                && a.Wx == b.Wx && a.Wy == b.Wy && a.Wz == b.Wz && a.Mu == b.Mu && a.Mv == b.Mv && a.Fl == b.Fl && a.Pr == b.Pr;
        }

        // ------------------------------------------------------------------ recording
        internal static void OnRecordStart()
        {
            RecTracks.Clear();
            NetBuf.Clear();
            _blobLen = 0;
        }

        private static void Add(int fac, string name, string col, S s)
        {
            Track t;
            if (!RecTracks.TryGetValue(fac, out t)) { t = new Track { Fac = fac, Name = name, Col = col }; RecTracks[fac] = t; }
            if (t.L.Count > 0)
            {
                S last = t.L[t.L.Count - 1];
                if (Same(last, s) || s.T < last.T) return;
            }
            t.L.Add(s);
        }

        /// <summary>Every frame (BtcTimer.Tick): record / send the local player's camera and cursor.</summary>
        internal static void Tick()
        {
            if (BtcConfig.ReplayRecordPov == null || !BtcConfig.ReplayRecordPov.Value) return;
            bool rec = BtcReplay.Mode == BtcReplay.State.Recording;
            if (!rec && (BtcReplay.Mode == BtcReplay.State.Playing || NetworkServer.active || !BtcNetworkGate.Enabled)) return;
            float now = Time.unscaledTime;
            if (now < _nextSample) return;
            _nextSample = now + SampleEvery;
            User u = User.LocalUser;
            if (u == null || u.faction == null || !BtcMatchStats.Tracks(u.faction)) return;
            S s;
            if (!Capture(out s)) return;
            if (rec && (NetworkServer.active || BtcReplay.Mode == BtcReplay.State.Recording))
            {
                float t = BtcReplay.RecTime;
                if (t < 0f) return;
                s.T = Mathf.RoundToInt(t * 1000f);
                Add(u.faction.ID, LocalName(), ColorUtility.ToHtmlStringRGB(u.faction.FactionColor), s);
                return;
            }
            // pure BC client: batch to the host
            s.T = Mathf.RoundToInt(now * 1000f);
            NetBuf.Add(s);
            if (now >= _nextSend) { _nextSend = now + SendEvery; SendBatch(u); }
        }

        // ------------------------------------------------------------------ net (client -> host)
        internal sealed class PovMsg : MessageBase
        {
            internal int fac;
            internal string name, col;
            internal byte[] data;

            public override void Serialize(NetworkWriter w)
            {
                w.Write(fac);
                w.Write(name ?? string.Empty);
                w.Write(col ?? string.Empty);
                w.WriteBytesFull(data ?? new byte[0]);
            }

            public override void Deserialize(NetworkReader r)
            {
                fac = r.ReadInt32();
                name = r.ReadString();
                col = r.ReadString();
                data = r.ReadBytesAndSize();
            }
        }

        private static void SendBatch(User u)
        {
            if (NetBuf.Count == 0) return;
            try
            {
                UNetManager m = NetworkManager.singleton as UNetManager;
                if (m == null || m.LocalNetworkClient == null || !m.LocalNetworkClient.isConnected) { NetBuf.Clear(); return; }
                byte[] d = EncodeBatch(NetBuf);
                NetBuf.Clear();
                m.LocalNetworkClient.Send(Msg, new PovMsg { fac = u.faction.ID, name = LocalName(), col = ColorUtility.ToHtmlStringRGB(u.faction.FactionColor), data = d });
            }
            catch (Exception e) { BtcLog.Error("PovRec", "send: " + e.Message); NetBuf.Clear(); }
        }

        internal static void RegisterHandlers()
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                if (NetworkServer.active)
                {
                    NetworkServer.UnregisterHandler(Msg);
                    NetworkServer.RegisterHandler(Msg, OnAtHost);
                }
            }
            catch (Exception e) { BtcLog.Error("PovRec", "RegisterHandlers: " + e); }
        }

        private static void OnAtHost(NetworkMessage nm)
        {
            try
            {
                PovMsg msg = nm.ReadMessage<PovMsg>();
                if (BtcReplay.Mode != BtcReplay.State.Recording || !BtcNetworkGate.Enabled || msg.data == null || msg.data.Length > 4096) return;
                float rt = BtcReplay.RecTime;
                if (rt < 0f) return;
                if (msg.col == null || msg.col.Length != 6) return;
                string name = msg.name ?? "?";
                if (name.Length > 40) name = name.Substring(0, 40);
                List<S> l = DecodeBatch(msg.data);
                if (l.Count == 0) return;
                int span = l[l.Count - 1].T;
                int now = Mathf.RoundToInt(rt * 1000f);
                for (int i = 0; i < l.Count; i++)
                {
                    S s = l[i];
                    s.T = Mathf.Max(0, now - (span - s.T));
                    Add(msg.fac, name, msg.col.ToUpperInvariant(), s);
                }
            }
            catch (Exception e) { BtcLog.Error("PovRec", "host: " + e.Message); }
        }

        // ------------------------------------------------------------------ codec (zigzag varints of deltas)
        internal static void PutV(Stream o, uint v)
        {
            while (v >= 0x80) { o.WriteByte((byte)(v | 0x80)); v >>= 7; }
            o.WriteByte((byte)v);
        }

        internal static void PutZ(Stream o, int d) { PutV(o, (uint)((d << 1) ^ (d >> 31))); }

        internal static uint GetV(byte[] b, ref int p)
        {
            uint v = 0; int sh = 0;
            while (p < b.Length) { byte x = b[p++]; v |= (uint)(x & 0x7F) << sh; if ((x & 0x80) == 0) break; sh += 7; }
            return v;
        }

        internal static int GetZ(byte[] b, ref int p)
        {
            uint u = GetV(b, ref p);
            return (int)(u >> 1) ^ -(int)(u & 1);
        }

        private static void PutSample(Stream o, S a, S p)
        {
            PutZ(o, a.T - p.T);
            PutZ(o, a.Cx - p.Cx); PutZ(o, a.Cy - p.Cy); PutZ(o, a.Cz - p.Cz);
            PutZ(o, a.Rx - p.Rx); PutZ(o, a.Ry - p.Ry); PutZ(o, a.Rz - p.Rz);
            PutZ(o, a.D - p.D);
            PutZ(o, a.Wx - p.Wx); PutZ(o, a.Wy - p.Wy); PutZ(o, a.Wz - p.Wz);
            PutZ(o, a.Mu - p.Mu); PutZ(o, a.Mv - p.Mv);
            o.WriteByte((byte)a.Fl); o.WriteByte((byte)a.Pr);
        }

        private static S GetSample(byte[] b, ref int pos, S p)
        {
            S a = new S();
            a.T = p.T + GetZ(b, ref pos);
            a.Cx = p.Cx + GetZ(b, ref pos); a.Cy = p.Cy + GetZ(b, ref pos); a.Cz = p.Cz + GetZ(b, ref pos);
            a.Rx = p.Rx + GetZ(b, ref pos); a.Ry = p.Ry + GetZ(b, ref pos); a.Rz = p.Rz + GetZ(b, ref pos);
            a.D = p.D + GetZ(b, ref pos);
            a.Wx = p.Wx + GetZ(b, ref pos); a.Wy = p.Wy + GetZ(b, ref pos); a.Wz = p.Wz + GetZ(b, ref pos);
            a.Mu = p.Mu + GetZ(b, ref pos); a.Mv = p.Mv + GetZ(b, ref pos);
            a.Fl = pos < b.Length ? b[pos++] : 0; a.Pr = pos < b.Length ? b[pos++] : 0;
            return a;
        }

        private static byte[] EncodeBatch(List<S> l)
        {
            MemoryStream ms = new MemoryStream();
            int t0 = l[0].T;
            PutV(ms, (uint)l.Count);
            S prev = new S();
            for (int i = 0; i < l.Count; i++)
            {
                S a = l[i];
                a.T -= t0;
                PutSample(ms, a, prev);
                prev = a;
            }
            return ms.ToArray();
        }

        private static List<S> DecodeBatch(byte[] b)
        {
            List<S> l = new List<S>();
            int p = 0;
            int n = (int)GetV(b, ref p);
            if (n > 64) return l;
            S prev = new S();
            for (int i = 0; i < n && p < b.Length; i++) { prev = GetSample(b, ref p, prev); l.Add(prev); }
            return l;
        }

        // ------------------------------------------------------------------ file block
        /// <summary>Recorder: the deflated track block (null when nothing was recorded); sets the length for the tl= token.</summary>
        internal static byte[] BuildBlob()
        {
            _blobLen = 0;
            try
            {
                if (RecTracks.Count == 0) return null;
                MemoryStream raw = new MemoryStream();
                raw.WriteByte((byte)'P'); raw.WriteByte((byte)'V'); raw.WriteByte(1);
                PutV(raw, (uint)RecTracks.Count);
                foreach (KeyValuePair<int, Track> kv in RecTracks)
                {
                    Track t = kv.Value;
                    PutZ(raw, t.Fac);
                    byte[] nb = System.Text.Encoding.UTF8.GetBytes(t.Name ?? "?");
                    PutV(raw, (uint)nb.Length); raw.Write(nb, 0, nb.Length);
                    byte[] cb = System.Text.Encoding.ASCII.GetBytes(t.Col ?? "FFFFFF");
                    PutV(raw, (uint)cb.Length); raw.Write(cb, 0, cb.Length);
                    PutV(raw, (uint)t.L.Count);
                    S prev = new S();
                    for (int i = 0; i < t.L.Count; i++) { PutSample(raw, t.L[i], prev); prev = t.L[i]; }
                }
                MemoryStream outp = new MemoryStream();
                using (DeflateStream ds = new DeflateStream(outp, CompressionMode.Compress, true))
                {
                    byte[] r = raw.ToArray();
                    ds.Write(r, 0, r.Length);
                }
                byte[] res = outp.ToArray();
                _blobLen = res.Length;
                BtcLog.Info("PovRec", "block " + res.Length + " bytes, " + RecTracks.Count + " player(s), " + raw.Length + " raw");
                return res;
            }
            catch (Exception e) { BtcLog.Error("PovRec", "BuildBlob: " + e.Message); _blobLen = 0; return null; }
        }

        /// <summary>tl= header token with the block length ("V,len;" or empty).</summary>
        internal static string TlToken() { return _blobLen > 0 ? "V," + _blobLen : ""; }

        /// <summary>Reader: block length from the tl= text (0 when absent).</summary>
        internal static int BlobLenFromTl(string tl)
        {
            if (string.IsNullOrEmpty(tl)) return 0;
            string[] items = tl.Trim().Split(';');
            for (int i = 0; i < items.Length; i++)
                if (items[i].StartsWith("V,"))
                {
                    int v;
                    if (int.TryParse(items[i].Substring(2), out v) && v > 0 && v < 200000000) return v;
                }
            return 0;
        }

        /// <summary>Reader thread: the block bytes of the replay being opened (parsed lazily on the main thread).</summary>
        internal static void SetBlob(byte[] b) { _pendingBlob = b; }

        private static void ParsePending()
        {
            byte[] b = _pendingBlob;
            if (b == null) return;
            _pendingBlob = null;
            Play.Clear();
            try
            {
                MemoryStream inp = new MemoryStream(b);
                MemoryStream raw = new MemoryStream();
                using (DeflateStream ds = new DeflateStream(inp, CompressionMode.Decompress))
                {
                    byte[] buf = new byte[65536]; int n;
                    while ((n = ds.Read(buf, 0, buf.Length)) > 0) raw.Write(buf, 0, n);
                }
                byte[] r = raw.ToArray();
                if (r.Length < 4 || r[0] != 'P' || r[1] != 'V' || r[2] != 1) return;
                int p = 3;
                int nt = (int)GetV(r, ref p);
                for (int i = 0; i < nt && p < r.Length; i++)
                {
                    Track t = new Track();
                    t.Fac = GetZ(r, ref p);
                    int nl = (int)GetV(r, ref p);
                    t.Name = System.Text.Encoding.UTF8.GetString(r, p, nl); p += nl;
                    int cl = (int)GetV(r, ref p);
                    t.Col = System.Text.Encoding.ASCII.GetString(r, p, cl); p += cl;
                    int cnt = (int)GetV(r, ref p);
                    S prev = new S();
                    t.L.Capacity = cnt;
                    for (int k = 0; k < cnt && p < r.Length; k++) { prev = GetSample(r, ref p, prev); t.L.Add(prev); }
                    Play.Add(t);
                }
                BtcLog.Info("PovRec", "replay has " + Play.Count + " player track(s)");
            }
            catch (Exception e) { BtcLog.Error("PovRec", "ParsePending: " + e.Message); Play.Clear(); }
        }

        // ------------------------------------------------------------------ playback
        private static bool At(Track t, float sec, out S a, out S b, out float f)
        {
            a = b = new S(); f = 0f;
            int n = t.L.Count;
            if (n == 0) return false;
            int ms = Mathf.RoundToInt(sec * 1000f);
            int i = Mathf.Clamp(t.Hint, 0, n - 1);
            while (i > 0 && t.L[i].T > ms) i--;
            while (i + 1 < n && t.L[i + 1].T <= ms) i++;
            t.Hint = i;
            a = t.L[i];
            if (ms < a.T - 3000) return false;           // before the first sample
            b = i + 1 < n ? t.L[i + 1] : a;
            // a gap in the data (no samples while the camera did not move is fine; more than 3 s: hold the last one)
            f = b.T > a.T ? Mathf.Clamp01((float)(ms - a.T) / (b.T - a.T)) : 0f;
            if (b.T - a.T > 3000) f = 0f;
            return true;
        }

        internal static Track TrackOf(int fac)
        {
            for (int i = 0; i < Play.Count; i++) if (Play[i].Fac == fac) return Play[i];
            return null;
        }

        private static Vector3 CamPos(S s) { return new Vector3(s.Cx / 8f, s.Cy / 8f, s.Cz / 8f); }

        private static bool _wasFollowing;
        private static float _holdUntil, _lastClock = -1f, _nextErrLog;
        private static int _camErr;

        /// <summary>Playback, every frame: watching a player (BtcPov) moves the camera to theirs. Held off while the replay loads, seeks or after a rewind (the scene is rebuilt and the camera is not ready).</summary>
        internal static void PlayTick()
        {
            if (_pendingBlob != null) ParsePending();
            BtcReplayWorld.Tick();
            if (Play.Count == 0 || BtcReplay.Mode != BtcReplay.State.Playing) { if (_mini.Count > 0) ClearMini(); _lastClock = -1f; return; }
            float now = Time.unscaledTime;
            float pc = BtcReplay.PlayClock;
            if (_lastClock >= 0f && pc < _lastClock - 1.5f) { _holdUntil = now + 3f; ClearMini(); _wasFollowing = false; }
            _lastClock = pc;
            if (BtcReplay.Loading || BtcReplay.Seeking || now < _holdUntil) return;
            try { UpdateMini(); }
            catch (Exception e) { LogErr("mini", e); }
            bool follow = BtcPov.Active && BtcPov.PovFaction != null && BtcConfig.PovFollowCam.Value;
            if (!follow) { _wasFollowing = false; return; }
            Track t = TrackOf(BtcPov.PovFaction.ID);
            CameraScript cam = CameraScript.Instance;
            S a, b; float f;
            if (t == null || cam == null || !cam.isActiveAndEnabled || Camera.main == null || !At(t, pc, out a, out b, out f)) return;
            Vector3 p = Vector3.Lerp(CamPos(a), CamPos(b), f);
            Quaternion q = Quaternion.Euler(Mathf.LerpAngle(a.Rx / 10f, b.Rx / 10f, f), Mathf.LerpAngle(a.Ry / 10f, b.Ry / 10f, f), Mathf.LerpAngle(a.Rz / 10f, b.Rz / 10f, f));
            float d = Mathf.Lerp(a.D / 10f, b.D / 10f, f);
            try { cam.Positionner(p, q, d); _camErr = 0; }
            catch (Exception e) { LogErr("camera", e); if (++_camErr >= 3) { _camErr = 0; _holdUntil = now + 5f; } }
            if (!_wasFollowing) { _wasFollowing = true; BtcLog.Info("PovRec", "following " + t.Name); }
        }

        private static void LogErr(string what, Exception e)
        {
            if (Time.unscaledTime < _nextErrLog) return;
            _nextErrLog = Time.unscaledTime + 5f;
            BtcLog.Error("PovRec", what + ": " + e.Message);
        }

        // ------------------------------------------------------------------ mini map: where every player looks and where their cursor is
        private sealed class MiniP
        {
            internal GameObject Look, Cur;
            internal MiniMap.Trackable TL, TC;
        }

        private static readonly Dictionary<int, MiniP> _mini = new Dictionary<int, MiniP>();
        private static MiniMap _mm;
        private static Sprite _sprLook, _sprCur;
        private static float _nextMmFind;

        private static Vector3 LookPoint(S s, float f, S b)
        {
            Vector3 p = Vector3.Lerp(CamPos(s), CamPos(b), f);
            Quaternion q = Quaternion.Euler(Mathf.LerpAngle(s.Rx / 10f, b.Rx / 10f, f), Mathf.LerpAngle(s.Ry / 10f, b.Ry / 10f, f), 0f);
            return p + q * Vector3.forward * Mathf.Lerp(s.D / 10f, b.D / 10f, f);
        }

        private static void UpdateMini()
        {
            if (!BtcConfig.PovShowCursor.Value) { if (_mini.Count > 0) ClearMini(); return; }
            if (_mm == null && Time.unscaledTime >= _nextMmFind) { _nextMmFind = Time.unscaledTime + 2f; _mm = UnityEngine.Object.FindObjectOfType<MiniMap>(); }
            if (_mm == null) return;
            if (_sprLook == null) _sprLook = Sprite.Create(BtcMarkIcons.RingTex(false), new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 100f);
            if (_sprCur == null) _sprCur = BtcMarkIcons.SpriteOf('A', new Vector2(9f / 40f, 35f / 40f));
            float pc = BtcReplay.PlayClock;
            for (int i = 0; i < Play.Count && i < 16; i++)
            {
                Track t = Play[i];
                S a, b; float f;
                if (!At(t, pc, out a, out b, out f)) continue;
                MiniP m;
                if (!_mini.TryGetValue(t.Fac, out m)) { m = new MiniP(); _mini[t.Fac] = m; }
                Color c = ParseCol(t.Col);
                if (m.Look == null) { m.Look = new GameObject("BtcLookMark"); m.Look.hideFlags = HideFlags.HideAndDontSave; }
                if (m.Cur == null) { m.Cur = new GameObject("BtcCursorMark"); m.Cur.hideFlags = HideFlags.HideAndDontSave; }
                m.Look.transform.position = LookPoint(a, f, b);
                if ((a.Fl & 16) != 0) m.Cur.transform.position = new Vector3(a.Wx / 8f, a.Wy / 8f, a.Wz / 8f);
                if (m.TL == null || m.TL.Transform == null) m.TL = MiniMap.AddTrackable(m.Look.transform, _sprLook, c);
                if (m.TC == null || m.TC.Transform == null) m.TC = MiniMap.AddTrackable(m.Cur.transform, _sprCur, c);
            }
        }

        private static void ClearMini()
        {
            foreach (KeyValuePair<int, MiniP> kv in _mini)
            {
                MiniP m = kv.Value;
                try { if (m.TL != null && m.TL.Transform != null) MiniMap.RemoveTrackable(m.TL); } catch { }
                try { if (m.TC != null && m.TC.Transform != null) MiniMap.RemoveTrackable(m.TC); } catch { }
                if (m.Look != null) UnityEngine.Object.Destroy(m.Look);
                if (m.Cur != null) UnityEngine.Object.Destroy(m.Cur);
            }
            _mini.Clear();
            _mm = null;
        }

        // ------------------------------------------------------------------ cursors (IMGUI overlay)
        private static GUIStyle _name;

        private static Color ParseCol(string hex)
        {
            Color c;
            return hex != null && hex.Length == 6 && ColorUtility.TryParseHtmlString("#" + hex, out c) ? c : Color.white;
        }

        /// <summary>Playback overlay: the cursors of all recorded players (the watched player's cursor at the recorded screen position).</summary>
        internal static void DrawCursors(float fs)
        {
            BtcReplayWorld.Draw(fs);
            if (Play.Count == 0 || BtcReplay.Mode != BtcReplay.State.Playing || BtcReplay.Seeking || BtcReplay.Loading || Time.unscaledTime < _holdUntil) return;
            if (BtcConfig.PovShowCursor == null || !BtcConfig.PovShowCursor.Value) return;
            if (Event.current.type != EventType.Repaint) return;
            Camera cam = Camera.main;
            float pc = BtcReplay.PlayClock;
            float sz = fs * 1.5f;
            if (_name == null) { _name = new GUIStyle(GUI.skin.label); _name.fontStyle = FontStyle.Bold; _name.normal.textColor = Color.white; _name.alignment = TextAnchor.MiddleLeft; _name.clipping = TextClipping.Overflow; }
            _name.fontSize = Mathf.Max(11, (int)(fs * 0.62f));
            for (int i = 0; i < Play.Count; i++)
            {
                Track t = Play[i];
                S a, b; float f;
                if (!At(t, pc, out a, out b, out f)) continue;
                bool pov = BtcPov.Active && BtcPov.PovFaction != null && BtcPov.PovFaction.ID == t.Fac && BtcConfig.PovFollowCam.Value;
                if (!pov && cam != null) DrawView(cam, a, b, f, ParseCol(t.Col), t.Name, sz);
                Vector2 pos;
                if (pov)
                {
                    float u = Mathf.Lerp(a.Mu, b.Mu, f) / 1000f, v = Mathf.Lerp(a.Mv, b.Mv, f) / 1000f;
                    pos = new Vector2(u * Screen.width, (1f - v) * Screen.height);
                }
                else
                {
                    if (cam == null || (a.Fl & 16) == 0) continue;
                    Vector3 w = new Vector3(Mathf.Lerp(a.Wx, (b.Fl & 16) != 0 ? b.Wx : a.Wx, f), Mathf.Lerp(a.Wy, (b.Fl & 16) != 0 ? b.Wy : a.Wy, f), Mathf.Lerp(a.Wz, (b.Fl & 16) != 0 ? b.Wz : a.Wz, f)) / 8f;
                    Vector3 sp = cam.WorldToScreenPoint(w);
                    if (sp.z <= 0f || sp.x < -50f || sp.x > Screen.width + 50f || sp.y < -50f || sp.y > Screen.height + 50f) continue;
                    pos = new Vector2(sp.x, Screen.height - sp.y);
                }
                Color c = ParseCol(t.Col);
                float al = (BtcPov.Active && !pov && BtcPov.PovFaction != null) ? 0.75f : 1f;
                // pie menu: ring filling while it opens, disc when open
                if ((a.Fl & 8) != 0) Ring(pos, sz * 1.5f, c, 0.95f, true);
                else if (a.Pr > 0) Ring(pos, sz * (0.5f + 0.9f * (a.Pr / 255f)), c, 0.8f, false);
                // click ripple
                if ((a.Fl & 7) != 0) Ring(pos, sz * (0.9f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f))), Color.white, 0.9f, false);
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, al);
                BtcMarkIcons.Draw('A', new Rect(pos.x - sz * 9f / 40f, pos.y - sz * 5f / 40f, sz, sz), c, c);
                GUI.color = old;
                if (!pov)
                {
                    Rect lr = new Rect(pos.x + sz * 0.55f, pos.y + sz * 0.45f, sz * 8f, sz * 0.7f);
                    _name.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
                    GUI.Label(new Rect(lr.x + 1f, lr.y + 1f, lr.width, lr.height), t.Name, _name);
                    _name.normal.textColor = new Color(c.r * 0.5f + 0.5f, c.g * 0.5f + 0.5f, c.b * 0.5f + 0.5f, 1f);
                    GUI.Label(lr, t.Name, _name);
                }
            }
        }

        internal static void Line(Vector2 a, Vector2 b, Color c, float w)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1f) return;
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(a.x, a.y - w * 0.5f, len, w), BtcUiStyle.White);
            GUI.color = old;
            GUI.matrix = m;
        }

        /// <summary>Free camera: the ground area a player's camera shows (footprint of the view corners) as a coloured outline with the name at the look-at point.</summary>
        private static void DrawView(Camera cam, S a, S b, float f, Color c, string name, float sz)
        {
            Vector3 p = Vector3.Lerp(CamPos(a), CamPos(b), f);
            Quaternion q = Quaternion.Euler(Mathf.LerpAngle(a.Rx / 10f, b.Rx / 10f, f), Mathf.LerpAngle(a.Ry / 10f, b.Ry / 10f, f), 0f);
            Vector3 look = LookPoint(a, f, b);
            float th = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), tw = th * cam.aspect;
            Vector2[] pts = ViewPts;
            bool ok = true;
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = q * new Vector3((i == 0 || i == 3) ? -tw : tw, (i < 2) ? th : -th, 1f);
                Vector3 g;
                float tt = dir.y < -0.02f ? (look.y - p.y) / dir.y : -1f;
                Vector3 hit = p + dir * tt;
                float hx = hit.x - p.x, hz = hit.z - p.z;
                if (tt > 0f && hx * hx + hz * hz <= ViewRange * ViewRange) g = hit;
                else
                {
                    Vector2 hd = new Vector2(dir.x, dir.z);
                    if (hd.sqrMagnitude < 1e-6f) hd = new Vector2((q * Vector3.forward).x, (q * Vector3.forward).z);
                    hd = hd.normalized * ViewRange;
                    g = new Vector3(p.x + hd.x, look.y, p.z + hd.y);
                }
                Vector3 sp = cam.WorldToScreenPoint(g);
                if (sp.z <= 0f) { ok = false; break; }
                pts[i] = new Vector2(sp.x, Screen.height - sp.y);
            }
            Color lc = new Color(c.r, c.g, c.b, 0.85f);
            if (ok)
                for (int i = 0; i < 4; i++) Line(pts[i], pts[(i + 1) % 4], lc, 2.5f);
            Vector3 ls = cam.WorldToScreenPoint(look);
            if (ls.z > 0f)
            {
                Vector2 lp = new Vector2(ls.x, Screen.height - ls.y);
                Line(lp + new Vector2(-sz * 0.4f, 0f), lp + new Vector2(sz * 0.4f, 0f), lc, 2.5f);
                Line(lp + new Vector2(0f, -sz * 0.4f), lp + new Vector2(0f, sz * 0.4f), lc, 2.5f);
            }
        }

        private static readonly Vector2[] ViewPts = new Vector2[4];
        private const float ViewRange = 70f;

        private static void Ring(Vector2 c, float r, Color col, float alpha, bool filled)
        {
            Texture2D tex = BtcMarkIcons.RingTex(filled);
            Color old = GUI.color;
            GUI.color = new Color(col.r, col.g, col.b, alpha);
            GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), tex);
            GUI.color = old;
        }
    }
}
