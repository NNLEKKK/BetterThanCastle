using System;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Phantom death effect (every peer, from BtcPhantom.OnDie, before the unit goes back to the pool): the skinned body is baked once into a
    /// plain mesh, which glitches for 0.3 s (sideways jerks, flicker, stretch) and then collapses like a CRT switching off (to a line, then a dot).
    /// One additive faction-coloured material per colour, one baked mesh per death, nothing per frame but a transform and a colour.
    /// Sound: a short synthesized digital zap with a falling, stuttering pitch (BtcSfx.Pin), only for deaths near the camera and in view,
    /// three volume steps by distance, at most one every 0.12 s.
    /// </summary>
    internal sealed class BtcPhantomFx : MonoBehaviour
    {
        private const float Glitch = 0.3f, Collapse = 0.16f;
        private static IntPtr[] _snd;
        private static float _lastSnd;
        internal static int Bursts, Sounds;

        private Transform _pivot;
        private Material _mat;
        private Color _col;
        private float _t, _nextJerk;
        private Vector3 _jerk, _base;
        private Mesh _mesh;
        private readonly System.Random _rnd = new System.Random();

        internal static void Burst(GameObject unit, Color col)
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
                if (visible) { Bursts++; Sound(unit.transform.position); }
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
        private static void Sound(Vector3 p)
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
            if (_snd == null)
            {
                _snd = new IntPtr[3];
                float[] amp = { 0.2f, 0.12f, 0.06f };
                for (int i = 0; i < 3; i++) _snd[i] = BtcSfx.Pin(Zap(amp[i]));
            }
            Sounds++;
            BtcSfx.PlayPtr(_snd[d < 30f ? 0 : (d < 60f ? 1 : 2)]);
        }

        /// <summary>0.28 s digital zap: square wave in 22 ms steps with a falling, randomly jumping pitch, sample-and-hold crush, a few dropouts.</summary>
        private static float[] Zap(float amp)
        {
            int sr = BtcSfx.SR;
            int n = (int)(0.28f * sr);
            float[] s = new float[n];
            System.Random r = new System.Random(7);
            int seg = (int)(0.022f * sr), hold = 1;
            float f = 900f, held = 0f, y = 0f;
            bool mute = false;
            double ph = 0.0;
            float lp = Mathf.Exp(-2f * Mathf.PI * 3500f / sr);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                if (i % seg == 0)
                {
                    f = Mathf.Lerp(900f, 140f, t) * (0.7f + (float)r.NextDouble() * 0.7f);
                    hold = 1 + r.Next(6);
                    mute = i > 0 && r.NextDouble() < 0.12;
                }
                ph += 2.0 * Math.PI * f / sr;
                if (i % hold == 0) held = Math.Sin(ph) >= 0.0 ? 1f : -1f;
                float q = Mathf.Round(held * 3f) / 3f;
                float env = Mathf.Min(1f, i / (0.002f * sr)) * Mathf.Exp(-t * 3.2f) * Mathf.Min(1f, (n - i) / (0.01f * sr));
                float x = mute ? 0f : q * env;
                y = (1f - lp) * x + lp * y;
                s[i] = y * amp;
            }
            return s;
        }
    }
}
