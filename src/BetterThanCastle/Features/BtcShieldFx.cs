using System;
using System.Collections.Generic;
using Brix.Game;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Base shields, local look and sound (every peer, nothing is sent). One shared shape (cube or sphere from the rules, look settings below) and two
    /// materials (Sprites/Default: generated honeycomb texture or plain white); every barrier owns a mesh copy whose vertex colours carry the tint,
    /// an optional view-dependent rim and a slow scan band, refreshed at ~20 Hz (skipped while nothing changes), so there is no per-frame allocation
    /// and no material instance. Flash on every hit (state counter from the host). A barrier at 0 opacity has its renderer off.
    /// Sounds are synthesized once (BtcSfx.Pin) and played for relevant events only: a barrier of yours or one that blocks you goes up/down,
    /// hits near the camera, the last seconds of a countdown. Settings (menu state, local, shown in Visuals > World overlays > Base shields):
    /// shield:alphaOwn / alphaEnemy (opacity %, 0 = hidden), shield:style (0 honeycomb, 1 solid, 2 outline), shield:detail (0 high .. 3 potato),
    /// shield:rim, shield:pulse, shield:noCaps + shield:cut (cut the top and bottom), shield:peaceHide, shield:vol (volume %),
    /// shield:sndUp / sndHit / sndTick. No on-screen UI (list chip and up/down notices were removed in 0.5.0).
    /// </summary>
    internal static class BtcShieldFx
    {
        private sealed class Vis
        {
            internal GameObject Go;
            internal Mesh Mesh;
            internal MeshRenderer Ren;
            internal Shape S;
            internal Color[] Cols;
            internal float A, Flash, PaintKey = -1f;
            internal int Hits = -1;
            internal bool Seen;
            internal bool LastUpForMe, LastOwnUp;
            internal bool Known;
        }

        // shape of a unit-size barrier (positions -0.5..0.5); a sphere and a box can be on screen at once (map boxes), rebuilt when a look setting changes
        private sealed class Shape
        {
            internal Vector3[] Pos, Nrm;
            internal Vector2[] Uvs;
            internal int[] Idx;
            internal bool Lines, Cube, Own;          // Own = built for one map box (union of parts, local coordinates in blocks, scale 1)
            internal int Sig = -1, Parts;
            internal float Y0 = -0.5f, YS = 1f;      // vertical extent of Pos (scan band)
        }
        private static readonly Shape SphereShape = new Shape(), BoxShape = new Shape { Cube = true };
        private static int _lookSig = -1;
        private static Material _matHex, _matFlat;
        private static bool _broken;
        private static readonly Dictionary<int, Vis> Vs = new Dictionary<int, Vis>();
        private static readonly List<int> Gone = new List<int>();
        private static float _nextColors, _nextCfg, _phase;

        // detail levels: high, medium, low, potato
        private static readonly int[] SLon = { 40, 24, 14, 8 }, SLat = { 22, 12, 7, 4 }, CDiv = { 8, 5, 3, 1 }, LRings = { 7, 5, 3, 3 }, LMer = { 16, 10, 6, 4 };

        // local settings (cached, re-read once per second)
        internal static int AlphaOwn = 100, AlphaEnemy = 100, Vol = 70;   // percent: barrier opacity (0 = hidden .. 150), barrier sound volume (0 = off)
        internal static int Style, Detail, Cut = 30;                       // look: 0 honeycomb / 1 solid / 2 outline; 0 high .. 3 potato; % of the height cut off at the top and bottom
        internal static bool SndUp = true, SndHit = true, SndTick = true, Rim = true, Pulse = true, NoCaps, PeaceHide;

        internal static void ReadCfg()
        {
            int old = BtcMenuState.Int("shield:alpha", 100);   // single opacity before 0.5.0: the default of both new sliders
            AlphaOwn = Mathf.Clamp(BtcMenuState.Int("shield:alphaOwn", old), 0, 150);
            AlphaEnemy = Mathf.Clamp(BtcMenuState.Int("shield:alphaEnemy", old), 0, 150);
            Vol = Mathf.Clamp(BtcMenuState.Int("shield:vol", 70), 0, 100);
            SndUp = BtcMenuState.Bool("shield:sndUp", true);
            SndHit = BtcMenuState.Bool("shield:sndHit", true);
            SndTick = BtcMenuState.Bool("shield:sndTick", true);
            Style = Mathf.Clamp(BtcMenuState.Int("shield:style", 0), 0, 2);
            Detail = Mathf.Clamp(BtcMenuState.Int("shield:detail", 0), 0, 3);
            Rim = BtcMenuState.Bool("shield:rim", true);
            Pulse = BtcMenuState.Bool("shield:pulse", true);
            NoCaps = BtcMenuState.Bool("shield:noCaps", false);
            Cut = Mathf.Clamp(BtcMenuState.Int("shield:cut", 30), 0, 90);
            PeaceHide = BtcMenuState.Bool("shield:peaceHide", false);
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
                int sig = Style | Detail << 2 | (NoCaps ? 1 : 0) << 4 | Cut << 5;
                if (sig != _lookSig)
                {
                    _lookSig = sig;
                    foreach (KeyValuePair<int, Vis> kv in Vs) Kill(kv.Value);
                    Vs.Clear();
                }
                Camera cam = Camera.main;
                Vector3 cp = cam != null ? cam.transform.position : Vector3.zero;
                int me = BtcShield.LocalFaction();
                float peaceLeft;
                bool peaceHide = PeaceHide && BtcShield.InPeace(out peaceLeft);
                foreach (KeyValuePair<int, Vis> kv in Vs) kv.Value.Seen = false;
                bool colors = un >= _nextColors;
                if (colors) _nextColors = un + 0.05f;
                _phase += Time.unscaledDeltaTime;
                float tickLeft = 99f;
                for (int i = 0; i < BtcShield.Bars.Count; i++)
                {
                    BtcShield.Bar b = BtcShield.Bars[i];
                    bool box = BtcShield.IsBox(b);
                    Vis v;
                    Vs.TryGetValue(b.Key, out v);
                    Shape sh;
                    if (b.Custom)
                    {
                        sh = v != null && v.S.Own && v.S.Sig == sig && v.S.Parts == b.PartsSig ? v.S : null;
                        if (sh == null) { sh = new Shape { Cube = true, Own = true, Sig = sig, Parts = b.PartsSig }; BuildUnion(sh, b); }
                    }
                    else
                    {
                        sh = box ? BoxShape : SphereShape;
                        if (sh.Sig != sig) { sh.Sig = sig; Build(sh); }
                    }
                    if (v != null && v.S != sh) { Kill(v); Vs.Remove(b.Key); v = null; }   // shape rule switched, a map box appeared or changed
                    if (v == null) { v = Make(sh); Vs[b.Key] = v; }
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
                            if (Pulse) v.Flash = 1f;
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

                    float look = peaceHide ? 0f : (friendly ? AlphaOwn : AlphaEnemy) / 100f;
                    float target = upForMe ? 0.6f : (ownUp ? 0.3f : 0.07f);
                    v.A = Mathf.MoveTowards(v.A, target * look, Time.unscaledDeltaTime * 0.9f);
                    v.Flash = Mathf.Max(0f, v.Flash - Time.unscaledDeltaTime * 2.2f);
                    float fl = v.Flash * Mathf.Min(1f, look);
                    bool show = v.A > 0.002f || fl > 0.01f;
                    if (v.Ren.enabled != show) { v.Ren.enabled = show; v.PaintKey = -1f; }
                    if (!show) continue;
                    Transform t = v.Go.transform;
                    t.position = b.C;
                    Vector3 sc = b.Custom ? Vector3.one : (box ? BtcShield.Half(b) * 2f : new Vector3(b.R * 2f, b.R * 2f, b.R * 2f));
                    if ((t.localScale - sc).sqrMagnitude > 0.0001f) t.localScale = sc;
                    t.rotation = !box && Pulse ? Quaternion.Euler(0f, _phase * 4f, 0f) : Quaternion.identity;   // a box stays on the voxel grid
                    if (colors) Paint(v, b, cp, upForMe, ownUp, fl, sc);
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

        private static void Paint(Vis v, BtcShield.Bar b, Vector3 cam, bool upForMe, bool ownUp, float fl, Vector3 size)
        {
            float a0 = v.A;
            if (!Rim && !Pulse)
            {
                // flat look: the colours only change with the fade or the state, skip the vertex upload otherwise
                float key = a0 + (upForMe ? 2f : 0f);
                if (Mathf.Abs(key - v.PaintKey) < 0.002f) return;
                v.PaintKey = key;
            }
            Color baseC = OwnerColor(b.Owner);
            Color c = upForMe ? Color.Lerp(baseC, Color.white, 0.25f) : baseC;
            Quaternion inv = Quaternion.Inverse(v.Go.transform.rotation);
            Vector3 camL = inv * (cam - b.C);
            bool band = Pulse && (upForMe || ownUp);
            float bandY = Mathf.Repeat(_phase * 0.18f, 1.4f) * 2f - 1.2f;   // -1.2 .. 1.6, bottom to top
            Shape s = v.S;
            float lw = s.Lines ? 2.4f : 1f;   // thin lines need more alpha to read
            Vector3[] pos = s.Pos, nrm = s.Nrm;
            for (int i = 0; i < pos.Length; i++)
            {
                float rim = 0.5f, a;
                if (Rim)
                {
                    Vector3 view = camL - Vector3.Scale(pos[i], size);
                    float m = view.magnitude;
                    float facing = m > 0.001f ? Mathf.Abs(Vector3.Dot(nrm[i], view) / m) : 1f;
                    rim = 1f - facing;
                    rim = rim * rim;
                    a = a0 * (0.25f + 1.6f * rim);
                }
                else a = a0 * 0.8f;
                if (band) { float db = (pos[i].y - s.Y0) / s.YS * 2f - 1f - bandY; a += a0 * 0.6f * Mathf.Max(0f, 1f - db * db * 60f); }
                a += fl * (0.25f + 0.5f * rim);
                Color col = c;
                col.a = Mathf.Clamp01(a * lw);
                v.Cols[i] = col;
            }
            v.Mesh.colors = v.Cols;
        }

        // ------------------------------------------------------------------ build
        private static bool Ensure()
        {
            if (_matHex != null) return true;
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("UI/Default");
            if (sh == null) { _broken = true; BtcLog.Warn("Shield", "no shader: barriers are not drawn"); return false; }
            _matHex = new Material(sh);
            _matHex.mainTexture = Honeycomb();
            _matHex.renderQueue = 3100;
            _matFlat = new Material(sh);
            _matFlat.mainTexture = Texture2D.whiteTexture;
            _matFlat.renderQueue = 3100;
            BtcLog.Info("Shield", "fx ready (" + sh.name + ")");
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

        private static readonly List<Vector3> BP = new List<Vector3>(), BN = new List<Vector3>();
        private static readonly List<Vector2> BU = new List<Vector2>();
        private static readonly List<int> BI = new List<int>();

        private static void Build(Shape s)
        {
            BP.Clear(); BN.Clear(); BU.Clear(); BI.Clear();
            s.Lines = Style == 2;
            float k = NoCaps ? 1f - Cut / 100f : 1f;   // kept half height (fraction of the size) when the top and bottom are cut
            if (s.Cube) { if (s.Lines) CubeLines(k); else CubeFaces(k); }
            else if (s.Lines) SphereLines(k);
            else SphereMesh(k);
            s.Pos = BP.ToArray(); s.Nrm = BN.ToArray(); s.Uvs = BU.ToArray(); s.Idx = BI.ToArray();
            BtcLog.Info("Shield", "fx shape " + (s.Cube ? "cube" : "sphere") + " style=" + Style + " detail=" + Detail + " cut=" + (NoCaps ? Cut : 0) + " verts=" + s.Pos.Length);
        }

        // ------------------------------------------------------------------ map box: union of parts, only the real outer surface
        private static readonly float[] UStep = { 6f, 10f, 18f, 1e6f };   // max cell size per detail level (rim gradient)
        private static readonly List<Vector3> UMin = new List<Vector3>(), UMax = new List<Vector3>();
        private static readonly List<float> SA = new List<float>(), SB = new List<float>();
        private static readonly HashSet<long> EdgeSeen = new HashSet<long>();
        private const float Eps = 0.05f;

        private static bool UIn(Vector3 p)
        {
            for (int i = 0; i < UMin.Count; i++)
            {
                Vector3 mn = UMin[i], mx = UMax[i];
                if (p.x > mn.x && p.x < mx.x && p.y > mn.y && p.y < mx.y && p.z > mn.z && p.z < mx.z) return true;
            }
            return false;
        }

        /// <summary>Split points of [a, b] on axis ax: the ends, every other part's min/max inside, the cut heights, then cells no longer than step.</summary>
        private static void Splits(List<float> into, float a, float b, int ax, float step, bool cutY, float lo, float hi)
        {
            into.Clear();
            into.Add(a); into.Add(b);
            for (int j = 0; j < UMin.Count; j++)
            {
                float m0 = UMin[j][ax], m1 = UMax[j][ax];
                if (m0 > a + Eps && m0 < b - Eps) into.Add(m0);
                if (m1 > a + Eps && m1 < b - Eps) into.Add(m1);
            }
            if (cutY && ax == 1) { if (lo > a + Eps && lo < b - Eps) into.Add(lo); if (hi > a + Eps && hi < b - Eps) into.Add(hi); }
            into.Sort();
            for (int i = into.Count - 2; i >= 0; i--)
            {
                float len = into[i + 1] - into[i];
                if (len < Eps) { into.RemoveAt(i + 1); continue; }
                int n = Mathf.CeilToInt(len / step);
                for (int k = n - 1; k >= 1; k--) into.Insert(i + 1, into[i] + len * k / n);
            }
        }

        private static void BuildUnion(Shape s, BtcShield.Bar b)
        {
            BP.Clear(); BN.Clear(); BU.Clear(); BI.Clear(); UMin.Clear(); UMax.Clear(); EdgeSeen.Clear();
            for (int i = 0; i < b.PMin.Count; i++) { UMin.Add(b.PMin[i] - b.C); UMax.Add(b.PMax[i] - b.C); }
            float ymin = -b.H.y, ymax = b.H.y;
            s.Y0 = ymin; s.YS = Mathf.Max(1f, ymax - ymin);
            s.Lines = Style == 2;
            float k = 1f - Cut / 100f, mid = (ymin + ymax) * 0.5f, half = (ymax - ymin) * 0.5f;
            float lo = NoCaps ? mid - k * half : ymin - 1f, hi = NoCaps ? mid + k * half : ymax + 1f;
            float step = UStep[Detail];
            for (int i = 0; i < UMin.Count; i++)
            {
                Vector3 mn = UMin[i], mx = UMax[i];
                if (s.Lines) { UnionEdges(mn, mx, lo, hi); continue; }
                for (int ax = 0; ax < 3; ax++)
                {
                    if (NoCaps && ax == 1) continue;   // no ceiling and floor
                    int ua = ax == 0 ? 2 : 0, va = ax == 1 ? 2 : 1;
                    Splits(SA, mn[ua], mx[ua], ua, step, NoCaps, lo, hi);
                    Splits(SB, mn[va], mx[va], va, step, NoCaps, lo, hi);
                    for (int side = 0; side < 2; side++)
                    {
                        Vector3 n = Vector3.zero; n[ax] = side == 1 ? 1f : -1f;
                        float plane = side == 1 ? mx[ax] : mn[ax];
                        for (int u = 0; u + 1 < SA.Count; u++)
                            for (int v = 0; v + 1 < SB.Count; v++)
                            {
                                Vector3 c = Vector3.zero;
                                c[ax] = plane; c[ua] = (SA[u] + SA[u + 1]) * 0.5f; c[va] = (SB[v] + SB[v + 1]) * 0.5f;
                                if (c.y < lo || c.y > hi) continue;
                                if (!UIn(c - n * Eps) || UIn(c + n * Eps)) continue;   // inside another part or on a joint: not the outer surface
                                int start = BP.Count;
                                for (int q = 0; q < 4; q++)
                                {
                                    Vector3 p = Vector3.zero;
                                    p[ax] = plane; p[ua] = (q == 1 || q == 2) ? SA[u + 1] : SA[u]; p[va] = q >= 2 ? SB[v + 1] : SB[v];
                                    BP.Add(p); BN.Add(n); BU.Add(new Vector2(p[ua] / 8f, p[va] / 8f));
                                }
                                BI.Add(start); BI.Add(start + 1); BI.Add(start + 2);
                                BI.Add(start); BI.Add(start + 2); BI.Add(start + 3);
                            }
                    }
                }
            }
            s.Pos = BP.ToArray(); s.Nrm = BN.ToArray(); s.Uvs = BU.ToArray(); s.Idx = BI.ToArray();
            BtcLog.Info("Shield", "fx map box parts=" + UMin.Count + " style=" + Style + " verts=" + s.Pos.Length);
        }

        /// <summary>Outline of the union: the 12 edges of a part, split where other parts begin or end; a piece is kept when the four
        /// quadrants around it are not a flat surface (1 or 3 filled, or 2 diagonal), so joints and seams between parts are not drawn.</summary>
        private static void UnionEdges(Vector3 mn, Vector3 mx, float lo, float hi)
        {
            for (int e = 0; e < 3; e++)
            {
                int a = (e + 1) % 3, bx = (e + 2) % 3;
                Splits(SA, mn[e], mx[e], e, 1e6f, NoCaps, lo, hi);
                for (int sa = 0; sa < 2; sa++)
                    for (int sb = 0; sb < 2; sb++)
                    {
                        float pa = sa == 1 ? mx[a] : mn[a], pb = sb == 1 ? mx[bx] : mn[bx];
                        for (int i = 0; i + 1 < SA.Count; i++)
                        {
                            Vector3 m = Vector3.zero;
                            m[e] = (SA[i] + SA[i + 1]) * 0.5f; m[a] = pa; m[bx] = pb;
                            if (m.y < lo - Eps || m.y > hi + Eps) continue;
                            Vector3 da = Vector3.zero, db = Vector3.zero; da[a] = Eps; db[bx] = Eps;
                            bool q00 = UIn(m - da - db), q01 = UIn(m - da + db), q10 = UIn(m + da - db), q11 = UIn(m + da + db);
                            int cnt = (q00 ? 1 : 0) + (q01 ? 1 : 0) + (q10 ? 1 : 0) + (q11 ? 1 : 0);
                            bool diag = cnt == 2 && q00 == q11;
                            if (!(cnt == 1 || cnt == 3 || diag)) continue;
                            long key = ((long)Mathf.RoundToInt(m.x * 2f) & 0xFFFFF) | (((long)Mathf.RoundToInt(m.y * 2f) & 0xFFFFF) << 20) | (((long)Mathf.RoundToInt(m.z * 2f) & 0xFFFFF) << 40) | ((long)e << 60);
                            if (!EdgeSeen.Add(key)) continue;
                            Vector3 p0 = m, p1 = m;
                            p0[e] = SA[i]; p1[e] = SA[i + 1];
                            Line(p0, p1);
                        }
                    }
            }
        }

        private static Vector3 Dir(float th, float ph) { return new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)); }

        private static void SphereMesh(float k)
        {
            int lon = SLon[Detail], lat = SLat[Detail];
            float th0 = Mathf.Acos(k), th1 = Mathf.Acos(-k);
            for (int j = 0; j <= lat; j++)
            {
                float th = Mathf.Lerp(th0, th1, (float)j / lat);
                for (int i = 0; i <= lon; i++)
                {
                    Vector3 d = Dir(th, 2f * Mathf.PI * i / lon);
                    BP.Add(d * 0.5f); BN.Add(d);
                    BU.Add(new Vector2(i * 10f / lon, th * 6f / Mathf.PI));
                }
            }
            for (int j = 0; j < lat; j++)
                for (int i = 0; i < lon; i++)
                {
                    int a = j * (lon + 1) + i, b = a + lon + 1;
                    BI.Add(a); BI.Add(b); BI.Add(a + 1);
                    BI.Add(a + 1); BI.Add(b); BI.Add(b + 1);
                }
        }

        private static void Line(Vector3 a, Vector3 b)
        {
            BI.Add(BP.Count); BP.Add(a); BN.Add(a.normalized); BU.Add(Vector2.zero);
            BI.Add(BP.Count); BP.Add(b); BN.Add(b.normalized); BU.Add(Vector2.zero);
        }

        private static void SphereLines(float k)
        {
            int seg = SLon[Detail], lat = SLat[Detail], rings = LRings[Detail], mer = LMer[Detail];
            float th0 = Mathf.Acos(k), th1 = Mathf.Acos(-k);
            for (int r = 0; r < rings; r++)
            {
                // cut: the cut edges are rings; full sphere: no rings at the poles
                float t = NoCaps ? (float)r / (rings - 1) : (r + 1f) / (rings + 1f);
                float th = Mathf.Lerp(th0, th1, t);
                for (int i = 0; i < seg; i++) Line(Dir(th, 2f * Mathf.PI * i / seg) * 0.5f, Dir(th, 2f * Mathf.PI * (i + 1) / seg) * 0.5f);
            }
            for (int m = 0; m < mer; m++)
            {
                float ph = 2f * Mathf.PI * m / mer;
                for (int j = 0; j < lat; j++) Line(Dir(Mathf.Lerp(th0, th1, (float)j / lat), ph) * 0.5f, Dir(Mathf.Lerp(th0, th1, (j + 1f) / lat), ph) * 0.5f);
            }
        }

        private static void CubeFaces(float k)
        {
            int n = CDiv[Detail];
            Face(Vector3.right, Vector3.forward, Vector3.up, n, k);
            Face(Vector3.left, Vector3.forward, Vector3.up, n, k);
            Face(Vector3.forward, Vector3.right, Vector3.up, n, k);
            Face(Vector3.back, Vector3.right, Vector3.up, n, k);
            if (!NoCaps)
            {
                Face(Vector3.up, Vector3.right, Vector3.forward, n, 1f);
                Face(Vector3.down, Vector3.right, Vector3.forward, n, 1f);
            }
        }

        /// <summary>One cube side: an n x n grid on normal nr, spanned by u (full width) and v (height scaled by kv).</summary>
        private static void Face(Vector3 nr, Vector3 u, Vector3 v, int n, float kv)
        {
            int start = BP.Count;
            for (int b = 0; b <= n; b++)
                for (int a = 0; a <= n; a++)
                {
                    float s = (float)a / n, t = (float)b / n;
                    BP.Add(nr * 0.5f + u * (s - 0.5f) + v * ((t - 0.5f) * kv));
                    BN.Add(nr);
                    BU.Add(new Vector2(s * 3f, t * 3f * kv));
                }
            for (int b = 0; b < n; b++)
                for (int a = 0; a < n; a++)
                {
                    int i0 = start + b * (n + 1) + a, i1 = i0 + n + 1;
                    BI.Add(i0); BI.Add(i1); BI.Add(i0 + 1);
                    BI.Add(i0 + 1); BI.Add(i1); BI.Add(i1 + 1);
                }
        }

        private static void CubeLines(float k)
        {
            float h = 0.5f * k;
            Vector3[] c = { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) };
            bool more = Detail <= 1;
            float[] ys = more ? new[] { -h, 0f, h } : new[] { -h, h };
            for (int r = 0; r < ys.Length; r++)
                for (int i = 0; i < 4; i++) Line(c[i] + Vector3.up * ys[r], c[(i + 1) % 4] + Vector3.up * ys[r]);
            for (int i = 0; i < 4; i++)
            {
                Line(c[i] + Vector3.down * h, c[i] + Vector3.up * h);
                if (more) { Vector3 mid = (c[i] + c[(i + 1) % 4]) * 0.5f; Line(mid + Vector3.down * h, mid + Vector3.up * h); }
            }
        }

        private static Vis Make(Shape s)
        {
            Vis v = new Vis();
            v.S = s;
            v.Go = new GameObject("BtcShieldBarrier");
            UnityEngine.Object.DontDestroyOnLoad(v.Go);
            v.Mesh = new Mesh();
            v.Mesh.vertices = s.Pos;
            v.Mesh.uv = s.Uvs;
            v.Cols = new Color[s.Pos.Length];
            v.Mesh.colors = v.Cols;
            if (s.Lines) v.Mesh.SetIndices(s.Idx, MeshTopology.Lines, 0);
            else v.Mesh.triangles = s.Idx;
            if (s.Own) v.Mesh.RecalculateBounds();   // map box: real size, scale 1
            else v.Mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            v.Go.AddComponent<MeshFilter>().sharedMesh = v.Mesh;
            MeshRenderer r = v.Go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Style == 0 ? _matHex : _matFlat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            v.Ren = r;
            return v;
        }

        private static void Kill(Vis v)
        {
            if (v.Go != null) UnityEngine.Object.Destroy(v.Go);
            if (v.Mesh != null) UnityEngine.Object.Destroy(v.Mesh);
        }

        // ------------------------------------------------------------------ events (no on-screen UI since 0.5.0: user decision 2026-11-14)
        private static void OnFlipFx(bool up, bool mine, BtcShield.Bar b)
        {
            if (SndUp) Play(up ? K.Up : K.Down);
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

        // ------------------------------------------------------------------ live barrier list (only the Gameplay > shield page in the menu; the on-screen chip was removed in 0.5.0)
        private static string _chipVal = "";
        private static string[] _rows = new string[0];
        private static Color[] _dots = new Color[0];
        private static float _nextChip;
        internal static string[] Rows { get { if (Time.unscaledTime >= _nextChip && BtcShield.Bars.Count > 0) { _nextChip = Time.unscaledTime + 0.25f; BuildChip(); } return _rows; } }
        internal static Color[] Dots { get { return _dots; } }

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
                case 5: w = T("artificer, falls in", "артефактор, упадёт через"); break;
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
