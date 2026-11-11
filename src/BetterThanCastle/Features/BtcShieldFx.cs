using System;
using System.Collections.Generic;
using Brix.Game;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Base shields, local look and sound (every peer, nothing is sent). One shared sphere mesh shape + one material (Sprites/Default, a generated
    /// honeycomb texture); every barrier owns a mesh copy whose vertex colours carry the tint, a view-dependent rim and a slow scan band, refreshed
    /// at ~20 Hz, so there is no per-frame allocation and no material instance. Flash on every hit (state counter from the host).
    /// Sounds are synthesized once (BtcSfx.Pin) and played for relevant events only: a barrier of yours or one that blocks you goes up/down,
    /// hits near the camera, the last seconds of a countdown. Settings (menu state, local, shown in Visuals > World overlays > Base shields): shield:alpha (opacity %), shield:vol (volume %),
    /// shield:sndUp / sndHit / sndTick, shield:notes, shield:hud.
    /// </summary>
    internal static class BtcShieldFx
    {
        private sealed class Vis
        {
            internal GameObject Go;
            internal Mesh Mesh;
            internal Color[] Cols;
            internal float A, Flash;
            internal int Hits = -1;
            internal bool Seen;
            internal bool LastUpForMe, LastOwnUp;
            internal bool Known;
        }

        private const int Lon = 40, Lat = 22;
        private static Vector3[] _dirs;
        private static Vector2[] _uvs;
        private static int[] _tris;
        private static Material _mat;
        private static bool _broken;
        private static readonly Dictionary<int, Vis> Vs = new Dictionary<int, Vis>();
        private static readonly List<int> Gone = new List<int>();
        private static float _nextColors, _nextCfg, _phase;

        // local settings (cached, re-read once per second)
        internal static int Alpha = 100, Vol = 70;   // percent: barrier opacity (10..150), barrier sound volume (0 = off)
        internal static bool SndUp = true, SndHit = true, SndTick = true, Notes = true, Hud = true;

        internal static void ReadCfg()
        {
            Alpha = Mathf.Clamp(BtcMenuState.Int("shield:alpha", 100), 10, 150);
            Vol = Mathf.Clamp(BtcMenuState.Int("shield:vol", 70), 0, 100);
            SndUp = BtcMenuState.Bool("shield:sndUp", true);
            SndHit = BtcMenuState.Bool("shield:sndHit", true);
            SndTick = BtcMenuState.Bool("shield:sndTick", true);
            Notes = BtcMenuState.Bool("shield:notes", true);
            Hud = BtcMenuState.Bool("shield:hud", true);
        }

        // ------------------------------------------------------------------ tick
        internal static void Tick(bool on)
        {
            if (_broken) return;
            try
            {
                if (!on || BtcShield.Bars.Count == 0)
                {
                    if (Vs.Count > 0) { foreach (KeyValuePair<int, Vis> kv in Vs) Kill(kv.Value); Vs.Clear(); }
                    return;
                }
                float un = Time.unscaledTime;
                if (un >= _nextCfg) { _nextCfg = un + 1f; ReadCfg(); }
                if (!Ensure()) return;
                Camera cam = Camera.main;
                Vector3 cp = cam != null ? cam.transform.position : Vector3.zero;
                int me = BtcShield.LocalFaction();
                foreach (KeyValuePair<int, Vis> kv in Vs) kv.Value.Seen = false;
                bool colors = un >= _nextColors;
                if (colors) _nextColors = un + 0.05f;
                _phase += Time.unscaledDeltaTime;
                float tickLeft = 99f;
                for (int i = 0; i < BtcShield.Bars.Count; i++)
                {
                    BtcShield.Bar b = BtcShield.Bars[i];
                    Vis v;
                    if (!Vs.TryGetValue(b.Key, out v)) { v = Make(); Vs[b.Key] = v; }
                    v.Seen = true;
                    bool friendly = BtcShield.Friendly(b, me);
                    bool upForMe = !friendly && me != int.MinValue && b.UpFor(me);
                    bool ownUp = friendly && b.UpAny;
                    // events (skip the first state of a barrier)
                    if (v.Known)
                    {
                        if (upForMe != v.LastUpForMe) OnFlipFx(upForMe, false, b);
                        if (ownUp != v.LastOwnUp) OnFlipFx(ownUp, true, b);
                        if (b.Hits != v.Hits)
                        {
                            v.Flash = 1f;
                            if (SndHit && (cp - b.C).sqrMagnitude < (b.R + 45f) * (b.R + 45f)) Play(K.Hit);
                        }
                    }
                    v.Known = true;
                    v.Hits = b.Hits;
                    v.LastUpForMe = upForMe;
                    v.LastOwnUp = ownUp;
                    // countdown of the barrier that matters to me
                    int k = me != int.MinValue ? b.IndexOf(me) : -1;
                    if (k >= 0) { float s = BtcShield.Left(b.Secs[k]); if (s >= 0f) tickLeft = Mathf.Min(tickLeft, s); }
                    else if (friendly) for (int q = 0; q < b.Fac.Count; q++) { float s = BtcShield.Left(b.Secs[q]); if (s >= 0f) tickLeft = Mathf.Min(tickLeft, s); }

                    float look = Alpha / 100f;
                    float target = upForMe ? 0.6f : (ownUp ? 0.3f : 0.07f);
                    v.A = Mathf.MoveTowards(v.A, target * look, Time.unscaledDeltaTime * 0.9f);
                    v.Flash = Mathf.Max(0f, v.Flash - Time.unscaledDeltaTime * 2.2f);
                    Transform t = v.Go.transform;
                    t.position = b.C;
                    float d = b.R * 2f;
                    if (Mathf.Abs(t.localScale.x - d) > 0.01f) t.localScale = new Vector3(d, d, d);
                    t.rotation = Quaternion.Euler(0f, _phase * 4f, 0f);
                    if (colors) Paint(v, b, cp, upForMe, ownUp);
                }
                Gone.Clear();
                foreach (KeyValuePair<int, Vis> kv in Vs) if (!kv.Value.Seen) Gone.Add(kv.Key);
                for (int i = 0; i < Gone.Count; i++) { Kill(Vs[Gone[i]]); Vs.Remove(Gone[i]); }
                CountdownTick(tickLeft);
            }
            catch (Exception e) { _broken = true; BtcLog.Error("Shield", "fx: " + e); }
        }

        private static Color OwnerColor(int fid)
        {
            Faction f = BtcMatchStats.FactionOf(fid);
            Color c = f != null ? f.FactionColor : new Color(0.4f, 0.75f, 1f, 1f);
            c.a = 1f;
            return c;
        }

        private static void Paint(Vis v, BtcShield.Bar b, Vector3 cam, bool upForMe, bool ownUp)
        {
            Color baseC = OwnerColor(b.Owner);
            Color c = upForMe ? Color.Lerp(baseC, Color.white, 0.25f) : baseC;
            Quaternion inv = Quaternion.Inverse(v.Go.transform.rotation);
            Vector3 camL = inv * (cam - b.C);
            bool up = upForMe || ownUp;
            float band = Mathf.Repeat(_phase * 0.18f, 1.4f) * 2f - 1.2f;   // -1.2 .. 1.6, bottom to top
            float a0 = v.A, fl = v.Flash;
            for (int i = 0; i < _dirs.Length; i++)
            {
                Vector3 dir = _dirs[i];
                Vector3 view = camL - dir * b.R;
                float m = view.magnitude;
                float facing = m > 0.001f ? Mathf.Abs(Vector3.Dot(dir, view) / m) : 1f;
                float rim = 1f - facing;
                rim = rim * rim;
                float a = a0 * (0.25f + 1.6f * rim);
                if (up) { float db = dir.y - band; a += a0 * 0.6f * Mathf.Max(0f, 1f - db * db * 60f); }
                a += fl * (0.25f + 0.5f * rim);
                Color col = c;
                col.a = Mathf.Clamp01(a);
                v.Cols[i] = col;
            }
            v.Mesh.colors = v.Cols;
        }

        // ------------------------------------------------------------------ build
        private static bool Ensure()
        {
            if (_mat != null) return true;
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("UI/Default");
            if (sh == null) { _broken = true; BtcLog.Warn("Shield", "no shader: barriers are not drawn"); return false; }
            _mat = new Material(sh);
            _mat.mainTexture = Honeycomb();
            _mat.renderQueue = 3100;
            BuildShape();
            BtcLog.Info("Shield", "fx ready (" + sh.name + ", " + _dirs.Length + " verts)");
            return true;
        }

        private static Texture2D Honeycomb()
        {
            const int S = 64;
            Texture2D tex = new Texture2D(S, S, TextureFormat.ARGB32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            Color[] px = new Color[S * S];
            // hexagon cells: distance to the nearest of a few cell centres, lines where two are equally near
            Vector2[] cs = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f) };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / S, (y + 0.5f) / S);
                    float d1 = 9f, d2 = 9f;
                    for (int i = 0; i < cs.Length; i++)
                    {
                        Vector2 q = p - cs[i];
                        q.y *= 0.866f;
                        float d = q.magnitude;
                        if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
                    }
                    float edge = Mathf.Clamp01(1f - (d2 - d1) * 14f);
                    float a = 0.22f + 0.78f * edge * edge;
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }

        private static void BuildShape()
        {
            int n = (Lon + 1) * (Lat + 1);
            _dirs = new Vector3[n];
            _uvs = new Vector2[n];
            for (int j = 0; j <= Lat; j++)
            {
                float th = Mathf.PI * j / Lat;
                for (int i = 0; i <= Lon; i++)
                {
                    float ph = 2f * Mathf.PI * i / Lon;
                    int k = j * (Lon + 1) + i;
                    _dirs[k] = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    _uvs[k] = new Vector2(i * 10f / Lon, j * 6f / Lat);
                }
            }
            _tris = new int[Lon * Lat * 6];
            int t = 0;
            for (int j = 0; j < Lat; j++)
                for (int i = 0; i < Lon; i++)
                {
                    int a = j * (Lon + 1) + i, b = a + Lon + 1;
                    _tris[t++] = a; _tris[t++] = b; _tris[t++] = a + 1;
                    _tris[t++] = a + 1; _tris[t++] = b; _tris[t++] = b + 1;
                }
        }

        private static Vis Make()
        {
            Vis v = new Vis();
            v.Go = new GameObject("BtcShieldBarrier");
            UnityEngine.Object.DontDestroyOnLoad(v.Go);
            v.Mesh = new Mesh();
            Vector3[] verts = new Vector3[_dirs.Length];
            for (int i = 0; i < verts.Length; i++) verts[i] = _dirs[i] * 0.5f;
            v.Mesh.vertices = verts;
            v.Mesh.uv = _uvs;
            v.Mesh.triangles = _tris;
            v.Mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            v.Cols = new Color[_dirs.Length];
            v.Mesh.colors = v.Cols;
            v.Go.AddComponent<MeshFilter>().sharedMesh = v.Mesh;
            MeshRenderer r = v.Go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return v;
        }

        private static void Kill(Vis v)
        {
            if (v.Go != null) UnityEngine.Object.Destroy(v.Go);
            if (v.Mesh != null) UnityEngine.Object.Destroy(v.Mesh);
        }

        // ------------------------------------------------------------------ events, notices
        private static float _lastNote;

        private static void OnFlipFx(bool up, bool mine, BtcShield.Bar b)
        {
            if (SndUp) Play(up ? K.Up : K.Down);
            if (!Notes || Time.unscaledTime - _lastNote < 1.5f) return;
            _lastNote = Time.unscaledTime;
            string where = b.Home ? T("home crystal", "домашний кристалл") : T("crystal", "кристалл");
            if (mine)
                BtcHud.Notice(up ? T("Your shield is up (" + where + ")", "Ваш барьер активен (" + where + ")")
                                 : T("Your shield is down (" + where + "): enemies can attack", "Ваш барьер снят (" + where + "): враг может атаковать"),
                              BtcReplayIcons.Shield, up ? BtcHud.Ok : BtcHud.Danger);
            else
                BtcHud.Notice(up ? T("Enemy shield is back up (" + where + ")", "Вражеский барьер снова активен (" + where + ")")
                                 : T("Enemy shield is down for you (" + where + "): attack!", "Вражеский барьер снят для вас (" + where + "): атакуйте!"),
                              BtcReplayIcons.Shield, up ? BtcHud.Warn : BtcHud.Ok);
        }

        private static int _lastTickSec = -1;

        private static void CountdownTick(float left)
        {
            if (!SndTick || left > 5.2f || left < 0f) { _lastTickSec = -1; return; }
            int s = Mathf.CeilToInt(left);
            if (s == _lastTickSec || s <= 0) return;
            _lastTickSec = s;
            Play(K.Tick);
        }

        private static string T(string en, string ru) { return BtcReplayUI.T(en, ru); }

        // ------------------------------------------------------------------ sounds
        internal enum K { Up, Down, Hit, Tick }
        private static readonly IntPtr[] Wavs = new IntPtr[4];
        private static int _built = -1;
        private static float _lastPlay;

        internal static void Play(K k)
        {
            int lvl = Vol;
            if (lvl <= 0) return;
            float now = Time.realtimeSinceStartup;
            if (now - _lastPlay < 0.12f && k != K.Up && k != K.Down) return;
            _lastPlay = now;
            if (_built != lvl)
            {
                _built = lvl;
                float v = lvl / 100f;
                // up: a soft rising two-step hum; down: the same falling; hit: a dull zap; tick: a dry click
                Wavs[(int)K.Up] = BtcSfx.Pin(BtcSfx.Cat(BtcSfx.Soft(220f, 330f, 0.16f, 9f, 0.22f * v, 0.02f, 1800f), BtcSfx.Soft(330f, 440f, 0.22f, 7f, 0.2f * v, 0.01f, 2200f)));
                Wavs[(int)K.Down] = BtcSfx.Pin(BtcSfx.Cat(BtcSfx.Soft(440f, 300f, 0.16f, 9f, 0.2f * v, 0.01f, 2000f), BtcSfx.Soft(300f, 180f, 0.24f, 7f, 0.2f * v, 0.01f, 1500f)));
                Wavs[(int)K.Hit] = BtcSfx.Pin(BtcSfx.Tick(160f, 0.09f, 40f, 0.22f * v, 0.35f, 1400f));
                Wavs[(int)K.Tick] = BtcSfx.Pin(BtcSfx.Tick(1250f, 0.03f, 110f, 0.14f * v, 0.03f, 3000f));
            }
            BtcSfx.PlayPtr(Wavs[(int)k]);
        }

        private static float _lastPreview;
        /// <summary>Preview from the settings (rate-limited: a slider drag must not spam).</summary>
        internal static void Preview(K k) { ReadCfg(); if (Time.realtimeSinceStartup - _lastPreview < 0.4f) return; _lastPreview = Time.realtimeSinceStartup; Play(k); }

        // ------------------------------------------------------------------ HUD chip (top-left column)
        private static string _chipVal = "";
        private static string[] _rows = new string[0];
        private static Color[] _dots = new Color[0];
        private static float _nextChip;
        internal static string[] Rows { get { if (Time.unscaledTime >= _nextChip && BtcShield.Bars.Count > 0) { _nextChip = Time.unscaledTime + 0.25f; BuildChip(); } return _rows; } }
        internal static Color[] Dots { get { return _dots; } }

        internal static void OnGUI()
        {
            if (!Hud || BtcShield.Bars.Count == 0 || !BtcShield.RuleOn || Event.current == null) return;
            try
            {
                if (Time.unscaledTime >= _nextChip && Event.current.type == EventType.Layout) { _nextChip = Time.unscaledTime + 0.25f; BuildChip(); }
                BtcHud.BeginLeft();
                BtcHud.Chip("shield", BtcReplayIcons.Shield, BtcMenu.Accent, _chipVal, _rows, _dots);
            }
            catch (Exception e) { BtcLog.Error("Shield", "hud: " + e.Message); Hud = false; }
        }

        private static string Clock(float s) { int t = Mathf.CeilToInt(s); return (t / 60) + ":" + (t % 60).ToString("00"); }

        private static string Reason(byte why, float left)
        {
            string w;
            switch (why)
            {
                case 1: w = T("peace", "мир"); break;
                case 2: w = T("crystals", "осколки"); break;
                case 3: w = T("artificer", "артефактор"); break;
                case 4: w = T("permanent", "постоянно"); break;
                default: w = ""; break;
            }
            if (left >= 0f) w += (w.Length > 0 ? " " : "") + Clock(left);
            return w.Length > 0 ? " (" + w + ")" : "";
        }

        private static void BuildChip()
        {
            int me = BtcShield.LocalFaction();
            int myUp = 0, myAll = 0, enemyUp = 0, enemyAll = 0;
            List<string> rows = new List<string>();
            List<Color> dots = new List<Color>();
            float soon = -1f;
            for (int i = 0; i < BtcShield.Bars.Count; i++)
            {
                BtcShield.Bar b = BtcShield.Bars[i];
                bool friendly = BtcShield.Friendly(b, me);
                string name = b.Home ? T("Home", "Дом") : T("Crystal", "Кристалл");
                Faction o = BtcMatchStats.FactionOf(b.Owner);
                string owner = o != null ? o.name : "?";
                string line;
                if (friendly)
                {
                    myAll++;
                    if (b.UpAny) myUp++;
                    int up = 0; byte why = 0; float left = -1f;
                    for (int k = 0; k < b.Fac.Count; k++)
                    {
                        if (b.Up[k]) up++;
                        float s = BtcShield.Left(b.Secs[k]);
                        if (s >= 0f && (left < 0f || s < left)) { left = s; why = b.Why[k]; }
                        if (why == 0) why = b.Why[k];
                    }
                    line = (b.Owner == me ? T("Your ", "Ваш ") : owner + ": ") + name.ToLowerInvariant() + " - " +
                           (b.Fac.Count == 0 ? T("no enemies", "нет врагов") : (up == b.Fac.Count ? T("UP vs all", "АКТИВЕН для всех") : (up == 0 ? T("DOWN", "СНЯТ") : T("up vs ", "активен для ") + up + "/" + b.Fac.Count))) + Reason(why, left);
                    if (left >= 0f && (soon < 0f || left < soon)) soon = left;
                }
                else
                {
                    enemyAll++;
                    int k = b.IndexOf(me);
                    bool up = k >= 0 && b.Up[k];
                    if (up) enemyUp++;
                    float left = k >= 0 ? BtcShield.Left(b.Secs[k]) : -1f;
                    line = owner + " " + name.ToLowerInvariant() + " - " + (up ? T("blocks you", "блокирует вас") : T("open for you", "открыт для вас")) + (k >= 0 ? Reason(b.Why[k], left) : "");
                    if (left >= 0f && (soon < 0f || left < soon)) soon = left;
                }
                rows.Add(line);
                dots.Add(OwnerColor(b.Owner));
            }
            _rows = rows.ToArray();
            _dots = dots.ToArray();
            string v = T("Shields ", "Барьеры ");
            if (myAll > 0) v += myUp + "/" + myAll;
            if (enemyAll > 0) v += (myAll > 0 ? "  " : "") + T("enemy ", "враг ") + enemyUp + "/" + enemyAll;
            if (soon >= 0f) v += "  " + Clock(soon);
            _chipVal = v;
        }
    }
}
