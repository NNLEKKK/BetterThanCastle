using System;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Phantom death effect (every peer, from BtcPhantom.OnDie, before the unit goes back to the pool): the skinned body is baked once into a
    /// plain mesh, which glitches for 0.3 s (sideways jerks, flicker, stretch) and then collapses like a CRT switching off (to a line, then a dot).
    /// One additive faction-coloured material per colour, one baked mesh per death, nothing per frame but a transform and a colour.
    /// Sound: a short soft synthesized "dissolve" (gliding detuned sines, BtcSfx.Pin), only for deaths near the camera and in view,
    /// three volume steps by distance, at most one every 0.12 s.
    /// </summary>
    internal sealed class BtcPhantomFx : MonoBehaviour
    {
        private const float Glitch = 0.3f, Collapse = 0.16f;
        private static IntPtr[] _snd, _warp;
        private static float _lastSnd;
        internal static int Bursts, Sounds;

        private Transform _pivot;
        private Material _mat;
        private Color _col;
        private float _t, _nextJerk;
        private Vector3 _jerk, _base;
        private Mesh _mesh;
        private readonly System.Random _rnd = new System.Random();

        internal static void Burst(GameObject unit, Color col) { Burst(unit, col, false); }

        /// <summary>A wormhole trip (BtcPhantomWorm): the same glitch flash, with the short warp sound instead of the dissolve.</summary>
        internal static void Warp(GameObject unit, Color col) { Burst(unit, col, true); }

        private static void Burst(GameObject unit, Color col, bool warp)
        {
            try
            {
                SkinnedMeshRenderer[] all = unit.GetComponentsInChildren<SkinnedMeshRenderer>(false);
                bool visible = false;
                for (int i = 0; i < all.Length; i++)
                {
                    SkinnedMeshRenderer src = all[i];
                    if (src.sharedMesh == null || src.name.StartsWith("BtcPhantom", StringComparison.Ordinal)) continue;
                    if (!src.enabled) continue;            // hidden by the fog of war: no effect, no sound
                    visible = true;
                    Mesh m = new Mesh();
                    src.BakeMesh(m);
                    Vector3 c = src.bounds.center;
                    GameObject root = new GameObject("BtcPhantomFx");
                    root.transform.position = c;
                    root.transform.rotation = Quaternion.identity;
                    GameObject g = new GameObject("mesh");
                    g.transform.SetParent(root.transform, false);
                    g.transform.position = src.transform.position;
                    g.transform.rotation = src.transform.rotation;
                    g.AddComponent<MeshFilter>().sharedMesh = m;
                    MeshRenderer mr = g.AddComponent<MeshRenderer>();
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    BtcPhantomFx fx = root.AddComponent<BtcPhantomFx>();
                    fx._pivot = root.transform;
                    fx._mesh = m;
                    fx._col = col;
                    fx._base = c;
                    fx._mat = new Material(Shader.Find("Hidden/Internal-Colored"));
                    fx._mat.hideFlags = HideFlags.HideAndDontSave;
                    fx._mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    fx._mat.SetInt("_ZWrite", 0);
                    fx._mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    fx._mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    fx._mat.renderQueue = 3003;
                    fx._mat.SetColor("_Color", new Color(col.r, col.g, col.b, 0.8f));
                    mr.sharedMaterial = fx._mat;
                }
                if (visible) { Bursts++; Sound(unit.transform.position, warp); }
            }
            catch (Exception e) { BtcLog.Warn("Phantom", "death fx: " + e.Message); }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) dt = Time.unscaledDeltaTime;   // paused game: still finish the effect
            _t += dt;
            if (_t < Glitch)
            {
                if (_t >= _nextJerk)
                {
                    _nextJerk = _t + 0.03f + (float)_rnd.NextDouble() * 0.04f;
                    Camera cam = Camera.main;
                    Vector3 right = cam != null ? cam.transform.right : Vector3.right;
                    _jerk = right * (((float)_rnd.NextDouble() * 2f - 1f) * 0.22f);
                    float sx = 1f + ((float)_rnd.NextDouble() * 2f - 1f) * 0.25f;
                    _pivot.localScale = new Vector3(sx, 1f + ((float)_rnd.NextDouble() - 0.5f) * 0.1f, sx);
                    float a = _rnd.NextDouble() < 0.25 ? 0.15f : 0.5f + (float)_rnd.NextDouble() * 0.45f;
                    _mat.SetColor("_Color", new Color(_col.r, _col.g, _col.b, a));
                }
                _pivot.position = _base + _jerk;
                return;
            }
            float k = (_t - Glitch) / Collapse;
            if (k >= 1f) { Destroy(gameObject); return; }
            _pivot.position = _base;
            // CRT off: height collapses first (bright line), then the line shrinks to a dot and fades
            float h = Mathf.Max(0.02f, 1f - k * 2f);
            float w = k < 0.5f ? 1f + k * 0.8f : Mathf.Max(0.02f, 1.4f * (1f - (k - 0.5f) * 2f));
            _pivot.localScale = new Vector3(w, h, w);
            Color c = Color.Lerp(_col, Color.white, 0.5f);
            _mat.SetColor("_Color", new Color(c.r, c.g, c.b, 0.95f * (1f - k * k)));
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_mat != null) Destroy(_mat);
        }

        // ------------------------------------------------------------------ sound
        private static void Sound(Vector3 p, bool warp)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            float d = Vector3.Distance(cam.transform.position, p);
            if (d > 90f) return;
            Vector3 v = cam.WorldToViewportPoint(p);
            if (v.z < 0f || v.x < -0.1f || v.x > 1.1f || v.y < -0.1f || v.y > 1.1f) return;
            float now = Time.realtimeSinceStartup;
            if (now - _lastSnd < 0.12f) return;
            _lastSnd = now;
            float[] amp = { 0.16f, 0.1f, 0.05f };
            if (_snd == null)
            {
                _snd = new IntPtr[3];
                for (int i = 0; i < 3; i++) _snd[i] = BtcSfx.Pin(Dissolve(amp[i]));
            }
            if (_warp == null)
            {
                _warp = new IntPtr[3];
                for (int i = 0; i < 3; i++) _warp[i] = BtcSfx.Pin(Whoosh(amp[i] * 0.8f));
            }
            Sounds++;
            BtcSfx.PlayPtr((warp ? _warp : _snd)[d < 30f ? 0 : (d < 60f ? 1 : 2)]);
        }

        /// <summary>0.3 s soft "whoosh" of a wormhole trip: one sine gliding up (260 -> 720 Hz) with its fifth, a breath of low-passed
        /// noise, smooth attack and release, through a 1.8 kHz low-pass. Rising, so it never sounds like the death dissolve.</summary>
        private static float[] Whoosh(float amp)
        {
            int sr = BtcSfx.SR;
            int n = (int)(0.3f * sr);
            float[] s = new float[n];
            System.Random r = new System.Random(23);
            double p1 = 0.0, p2 = 0.0;
            float y = 0f, ny = 0f;
            float lp = Mathf.Exp(-2f * Mathf.PI * 1800f / sr), nlp = Mathf.Exp(-2f * Mathf.PI * 1200f / sr);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr, k = (float)i / n;
                float f = 260f * Mathf.Pow(720f / 260f, k);
                p1 += 2.0 * Math.PI * f / sr;
                p2 += 2.0 * Math.PI * f * 1.5 / sr;
                float tone = (float)Math.Sin(p1) + 0.25f * (float)Math.Sin(p2);
                ny = (1f - nlp) * ((float)r.NextDouble() * 2f - 1f) + nlp * ny;
                float env = (1f - Mathf.Exp(-t / 0.03f)) * Mathf.Min(1f, (n - i) / (0.08f * sr));
                float x = (tone * 0.8f + ny * 0.6f * (1f - k)) * env;
                y = (1f - lp) * x + lp * y;
                s[i] = y * amp;
            }
            return s;
        }

        /// <summary>0.45 s soft "dissolve": two slightly detuned sines and a quiet sub-octave glide down (560 -> 240 Hz) with a slow attack,
        /// a gentle 14 Hz shimmer, a breath of low-passed noise, all through a 1.6 kHz low-pass. No square waves, no steps, no hard edges.</summary>
        private static float[] Dissolve(float amp)
        {
            int sr = BtcSfx.SR;
            int n = (int)(0.45f * sr);
            float[] s = new float[n];
            System.Random r = new System.Random(11);
            double p1 = 0.0, p2 = 0.0, p3 = 0.0;
            float y = 0f, ny = 0f;
            float lp = Mathf.Exp(-2f * Mathf.PI * 1600f / sr), nlp = Mathf.Exp(-2f * Mathf.PI * 900f / sr);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr, k = (float)i / n;
                float f = 560f * Mathf.Pow(240f / 560f, k);           // exponential glide: sounds even, not sliding
                p1 += 2.0 * Math.PI * f / sr;
                p2 += 2.0 * Math.PI * f * 1.012 / sr;
                p3 += 2.0 * Math.PI * f * 0.5 / sr;
                float tone = (float)(Math.Sin(p1) + Math.Sin(p2)) * 0.5f + 0.3f * (float)Math.Sin(p3);
                float shimmer = 1f - 0.18f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 14f * t));
                ny = (1f - nlp) * ((float)r.NextDouble() * 2f - 1f) + nlp * ny;
                float breath = ny * 0.5f * Mathf.Exp(-t * 9f);
                float env = (1f - Mathf.Exp(-t / 0.025f)) * Mathf.Exp(-t * 5f) * Mathf.Min(1f, (n - i) / (0.04f * sr));
                float x = (tone * shimmer + breath) * env;
                y = (1f - lp) * x + lp * y;
                s[i] = y * amp;
            }
            return s;
        }
    }
}
