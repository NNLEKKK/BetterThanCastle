using System;
using System.Collections.Generic;
using System.IO;
using Brix.Game;
using Brix.Game.AI;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Phantom look (every peer, added by BtcPhantom): the skinned body becomes a faint see-through ghost (Sprites/Default, the bricktron texture
    /// tinted blue at low alpha, or Phantom.png of the selected resource pack with its own alpha) and gets a glowing blue outline.
    /// Outline = inverted hull: a copy of the mesh pushed out along smoothed normals, drawn back faces only (Hidden/Internal-Colored, _Cull Front).
    /// A depth-only copy of the body (alpha 0, ZWrite on) is drawn between them so the hull shows only around the silhouette.
    /// The hull width is in world units, so it is swapped between 5 pre-built widths by camera distance (stays about 2 px wide).
    /// Materials are restored when the phantom dies or the object goes back to the pool (OnDisable), so a pooled bricktron is never left ghostly.
    /// </summary>
    internal sealed class BtcPhantomLook : MonoBehaviour
    {
        private const string Tag = "BtcPhantom";
        private static readonly float[] BandDist = { 12f, 25f, 45f, 80f };
        private static readonly float[] BandWidth = { 0.025f, 0.045f, 0.08f, 0.14f, 0.22f };   // world units

        private sealed class Part
        {
            internal SkinnedMeshRenderer Src;
            internal Material[] Orig;
            internal Material[] Mine;
            internal SkinnedMeshRenderer Depth, Hull;
            internal Mesh[] Bands;
            internal int Band = -1;
        }

        private sealed class MeshSet { internal Mesh Depth; internal Mesh[] Bands; internal bool Failed; }

        private static readonly Dictionary<Mesh, MeshSet> MeshCache = new Dictionary<Mesh, MeshSet>();
        private static readonly Dictionary<int, Material[]> Pristine = new Dictionary<int, Material[]>();
        private static Material _depthMat;
        private static Texture2D _packTex;
        private static string _packDir;
        private static int _texVersion;
        private static float _nextPackCheck;
        private static bool _loggedNoRead;
        private static Camera _cam;
        private static int _camFrame = -1;

        private readonly List<Part> _parts = new List<Part>();
        private int _myVersion = -1, _colKey = int.MinValue;
        private float _nextCheck, _nextBand;
        private bool _removing;

        // ------------------------------------------------------------------ entry points
        internal static void Ensure(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return;
            try
            {
                BtcPhantomLook l = go.GetComponent<BtcPhantomLook>();
                if (l == null) l = go.AddComponent<BtcPhantomLook>();
                l.enabled = true;
            }
            catch (Exception e) { BtcLog.Warn("Phantom", "look: " + e.Message); }
        }

        internal static void Remove(GameObject go)
        {
            if (go == null) return;
            BtcPhantomLook l = go.GetComponent<BtcPhantomLook>();
            if (l == null) return;
            l.Restore();
            l._removing = true;
            Destroy(l);
        }

        /// <summary>Every 2 s: the selected pack (and its Phantom.png) changed -> every phantom re-applies.</summary>
        internal static void CheckPack()
        {
            float now = Time.unscaledTime;
            if (now < _nextPackCheck) return;
            _nextPackCheck = now + 2f;
            string dir = ResourcePackManager.CurrentPackDir;
            if (dir == _packDir) return;
            _packDir = dir;
            Texture2D old = _packTex;
            _packTex = null;
            try
            {
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    string[] f = Directory.GetFiles(dir, "Phantom.png", SearchOption.AllDirectories);
                    if (f.Length > 0)
                    {
                        Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                        if (t.LoadImage(File.ReadAllBytes(f[0]))) { t.name = Tag + "Tex"; _packTex = t; BtcLog.Info("Phantom", "pack texture " + f[0] + " " + t.width + "x" + t.height); }
                        else Destroy(t);
                    }
                }
            }
            catch (Exception e) { BtcLog.Warn("Phantom", "pack texture: " + e.Message); }
            if (old != _packTex) { _texVersion++; if (old != null) Destroy(old); }
        }

        // ------------------------------------------------------------------ lifecycle
        private void OnEnable()
        {
            if (_removing) return;
            Apply();
        }

        private void OnDisable()
        {
            if (_removing) return;
            Restore();
            _removing = true;
            Destroy(this);   // the object went back to the pool: BtcPhantom adds a fresh look if it is still a phantom
        }

        private void LateUpdate()
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                Part p = _parts[i];
                if (p.Src == null) continue;
                bool vis = p.Src.enabled;
                if (p.Hull != null && p.Hull.enabled != vis) p.Hull.enabled = vis;
                if (p.Depth != null && p.Depth.enabled != vis) p.Depth.enabled = vis;
            }
            float now = Time.unscaledTime;
            if (now >= _nextBand)
            {
                _nextBand = now + 0.3f;
                Camera c = Cam();
                if (c != null)
                {
                    float d = Vector3.Distance(c.transform.position, transform.position);
                    int b = 0;
                    while (b < BandDist.Length && d > BandDist[b]) b++;
                    for (int i = 0; i < _parts.Count; i++)
                    {
                        Part p = _parts[i];
                        if (p.Hull == null || p.Bands == null || p.Band == b) continue;
                        p.Band = b;
                        p.Hull.sharedMesh = p.Bands[b];
                    }
                }
            }
            if (now >= _nextCheck)
            {
                _nextCheck = now + 0.5f;
                Color nc;
                bool redo = _myVersion != _texVersion || ColorKey(gameObject, out nc) != _colKey;   // faction known late on a client, pack changed
                for (int i = 0; i < _parts.Count && !redo; i++)
                {
                    Part p = _parts[i];
                    if (p.Src == null) continue;
                    Material[] cur = p.Src.sharedMaterials;
                    for (int k = 0; k < cur.Length; k++) if (cur[k] != null && !Ours(cur[k])) { redo = true; break; }   // level up, face, pack swap replaced a material
                }
                if (!redo) redo = FindNewRenderers();
                if (redo) { Restore(); Apply(); }
            }
        }

        private static Camera Cam()
        {
            if (_camFrame != Time.frameCount) { _camFrame = Time.frameCount; _cam = Camera.main; }
            return _cam;
        }

        private static bool Ours(Material m) { return m != null && m.name.IndexOf(Tag, StringComparison.Ordinal) >= 0; }

        private bool FindNewRenderers()
        {
            SkinnedMeshRenderer[] all = GetComponentsInChildren<SkinnedMeshRenderer>(false);
            int n = 0;
            for (int i = 0; i < all.Length; i++) if (!all[i].name.StartsWith(Tag, StringComparison.Ordinal)) n++;
            return n != _parts.Count;
        }

        // ------------------------------------------------------------------ apply / restore
        private void Apply()
        {
            try
            {
                _myVersion = _texVersion;
                Color col;
                _colKey = ColorKey(gameObject, out col);
                SkinnedMeshRenderer[] all = GetComponentsInChildren<SkinnedMeshRenderer>(false);
                for (int i = 0; i < all.Length; i++)
                {
                    SkinnedMeshRenderer src = all[i];
                    if (src.name.StartsWith(Tag, StringComparison.Ordinal) || src.sharedMesh == null) continue;
                    // the last real (non phantom) material of every slot, kept per renderer across looks: restoring never leaves a ghost material on a pooled unit
                    Material[] cur = src.sharedMaterials;
                    int rid = src.GetInstanceID();
                    Material[] pr;
                    if (!Pristine.TryGetValue(rid, out pr) || pr.Length != cur.Length) { pr = new Material[cur.Length]; Pristine[rid] = pr; }
                    for (int k = 0; k < cur.Length; k++) if (cur[k] != null && !Ours(cur[k])) pr[k] = cur[k];
                    Part p = new Part { Src = src, Orig = (Material[])pr.Clone() };
                    MeshSet ms = Meshes(src);
                    bool hull = ms != null && !ms.Failed;
                    p.Mine = new Material[p.Orig.Length];
                    for (int k = 0; k < p.Orig.Length; k++)
                    {
                        Material o = p.Orig[k];
                        if (Ours(o)) { p.Mine[k] = o; continue; }
                        p.Mine[k] = Body(o != null ? o.mainTexture : null, hull, _colKey, col);
                    }
                    src.sharedMaterials = p.Mine;
                    if (hull)
                    {
                        p.Depth = Child(src, Tag + "Depth", ms.Depth, DepthMat());
                        p.Bands = ms.Bands;
                        p.Hull = Child(src, Tag + "Hull", ms.Bands[0], OutlineMat(_colKey, col));
                        p.Band = 0;
                    }
                    _parts.Add(p);
                }
                _nextBand = 0f;
            }
            catch (Exception e) { BtcLog.Warn("Phantom", "apply look: " + e.Message); }
        }

        private void Restore()
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                Part p = _parts[i];
                try
                {
                    if (p.Src != null && p.Orig != null)
                    {
                        Material[] cur = p.Src.sharedMaterials;
                        if (cur.Length == p.Orig.Length)
                        {
                            for (int k = 0; k < cur.Length; k++) if (p.Orig[k] != null) cur[k] = p.Orig[k];
                            p.Src.sharedMaterials = cur;
                        }
                    }
                    if (p.Depth != null) Destroy(p.Depth.gameObject);
                    if (p.Hull != null) Destroy(p.Hull.gameObject);
                }
                catch (Exception e) { BtcLog.Warn("Phantom", "restore look: " + e.Message); }
            }
            _parts.Clear();
        }

        private static SkinnedMeshRenderer Child(SkinnedMeshRenderer src, string name, Mesh mesh, Material mat)
        {
            GameObject g = new GameObject(name);
            g.layer = src.gameObject.layer;
            g.transform.SetParent(src.transform, false);
            SkinnedMeshRenderer r = g.AddComponent<SkinnedMeshRenderer>();
            r.bones = src.bones;
            r.rootBone = src.rootBone;
            r.sharedMesh = mesh;
            r.localBounds = src.localBounds;
            r.updateWhenOffscreen = src.updateWhenOffscreen;
            r.quality = src.quality;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterial = mat;
            r.enabled = src.enabled;
            return r;
        }

        // ------------------------------------------------------------------ colours (the owner's faction colour; Rainbow cycles its hue)
        private const int RainbowKey = -1;

        /// <summary>Colour key of a unit's faction: packed RGB, RainbowKey for the rainbow colour, 0 = no faction yet (light blue).</summary>
        internal static int ColorKey(GameObject go, out Color c)
        {
            c = new Color(0.35f, 0.8f, 1f);
            Labor l = go != null ? go.GetComponent<Labor>() : null;
            Faction f = l != null ? l.faction : null;
            if (f == null) return 0;
            if (f.name == "Rainbow" || f.name == "Black") { c = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.25f, 1f), 0.8f, 1f); return RainbowKey; }
            c = f.FactionColor;
            // very dark colours would vanish on the ground: lift them
            float lum = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
            if (lum < 0.3f) c = Color.Lerp(c, Color.white, 0.5f);
            c.a = 1f;
            Color32 q = c;
            return 1 + (q.r << 16 | q.g << 8 | q.b);
        }

        /// <summary>Colour of the death effect (current rainbow hue for a rainbow faction).</summary>
        internal static Color FxColor(GameObject go) { Color c; ColorKey(go, out c); return c; }

        /// <summary>Every frame from BtcPhantom.Tick: the rainbow outline/body materials follow the hue.</summary>
        internal static void TickShared()
        {
            if (!OutlineCache.ContainsKey(RainbowKey) && _rainbowBodies.Count == 0) return;
            Color c = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.25f, 1f), 0.8f, 1f);
            Material o;
            if (OutlineCache.TryGetValue(RainbowKey, out o) && o != null) o.SetColor("_Color", new Color(c.r, c.g, c.b, 0.95f));
            for (int i = 0; i < _rainbowBodies.Count; i++) if (_rainbowBodies[i] != null) _rainbowBodies[i].color = BodyTint(c, true);
        }

        private static readonly Dictionary<int, Material> OutlineCache = new Dictionary<int, Material>();
        private static readonly Dictionary<long, Material> BodyCache2 = new Dictionary<long, Material>();
        private static readonly List<Material> _rainbowBodies = new List<Material>();

        private static Color BodyTint(Color c, bool hull)
        {
            Color t = Color.Lerp(c, Color.white, 0.35f);
            t.a = hull ? 0.12f : 0.4f;
            return t;
        }

        // ------------------------------------------------------------------ materials
        private static Material Body(Texture orig, bool hull, int key, Color col)
        {
            Texture tex = _packTex != null ? (Texture)_packTex : orig;
            bool pack = _packTex != null;
            long ck = ((long)(tex != null ? tex.GetInstanceID() : 0) << 32) ^ (uint)(pack ? 7 : key) ^ (hull ? 0x40000000L : 0L);
            Material m;
            if (BodyCache2.TryGetValue(ck, out m) && m != null) return m;
            m = new Material(Shader.Find("Sprites/Default"));
            m.name = Tag + "Body";
            if (tex != null) m.mainTexture = tex;
            m.color = pack ? Color.white : BodyTint(col, hull);
            m.renderQueue = 3000;
            BodyCache2[ck] = m;
            if (!pack && key == RainbowKey) _rainbowBodies.Add(m);
            return m;
        }

        private static Material Colored(string name, Color c, int cull, int zwrite, int src, int dst, int queue)
        {
            Material m = new Material(Shader.Find("Hidden/Internal-Colored"));
            m.name = name;
            m.hideFlags = HideFlags.HideAndDontSave;
            m.SetColor("_Color", c);
            m.SetInt("_Cull", cull);
            m.SetInt("_ZWrite", zwrite);
            m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            m.SetInt("_SrcBlend", src);
            m.SetInt("_DstBlend", dst);
            m.renderQueue = queue;
            return m;
        }

        private static Material OutlineMat(int key, Color col)
        {
            Material m;
            if (OutlineCache.TryGetValue(key, out m) && m != null) return m;
            m = Colored(Tag + "Outline", new Color(col.r, col.g, col.b, 0.95f), (int)UnityEngine.Rendering.CullMode.Front, 0,
                (int)UnityEngine.Rendering.BlendMode.SrcAlpha, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, 3002);
            OutlineCache[key] = m;
            return m;
        }

        private static Material DepthMat()
        {
            if (_depthMat == null)
                _depthMat = Colored(Tag + "Depth", new Color(0f, 0f, 0f, 0f), (int)UnityEngine.Rendering.CullMode.Back, 1,
                    (int)UnityEngine.Rendering.BlendMode.Zero, (int)UnityEngine.Rendering.BlendMode.One, 3001);
            return _depthMat;
        }

        // ------------------------------------------------------------------ meshes (built once per source mesh)
        private static MeshSet Meshes(SkinnedMeshRenderer src)
        {
            Mesh mesh = src.sharedMesh;
            MeshSet ms;
            if (MeshCache.TryGetValue(mesh, out ms)) return ms;
            ms = new MeshSet();
            MeshCache[mesh] = ms;
            try
            {
                Vector3[] v = mesh.vertices;
                Vector3[] nrm = mesh.normals;
                int[] tris = mesh.triangles;
                if (v == null || v.Length == 0 || nrm == null || nrm.Length != v.Length || tris == null || tris.Length == 0) throw new Exception("no mesh data");
                // world units per mesh unit (pose-dependent, good enough for an outline width)
                float meshH = Mathf.Max(mesh.bounds.size.y, Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z));
                float worldH = Mathf.Max(src.bounds.size.y, Mathf.Max(src.bounds.size.x, src.bounds.size.z));
                float scale = meshH > 1e-5f && worldH > 1e-5f ? worldH / meshH : 1f;
                // smoothed normals: hard-edged blocks would split apart when pushed along their face normals
                Dictionary<Vector3, Vector3> sum = new Dictionary<Vector3, Vector3>();
                Vector3[] key = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 k = new Vector3(Mathf.Round(v[i].x * 1000f), Mathf.Round(v[i].y * 1000f), Mathf.Round(v[i].z * 1000f));
                    key[i] = k;
                    Vector3 s;
                    sum.TryGetValue(k, out s);
                    sum[k] = s + nrm[i];
                }
                Vector3[] smooth = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++) { Vector3 s = sum[key[i]]; smooth[i] = s.sqrMagnitude > 1e-8f ? s.normalized : nrm[i]; }
                ms.Depth = Single(mesh, tris, null);
                ms.Bands = new Mesh[BandWidth.Length];
                for (int b = 0; b < BandWidth.Length; b++)
                {
                    float w = BandWidth[b] / scale;
                    Vector3[] pv = new Vector3[v.Length];
                    for (int i = 0; i < v.Length; i++) pv[i] = v[i] + smooth[i] * w;
                    ms.Bands[b] = Single(mesh, tris, pv);
                }
            }
            catch (Exception e)
            {
                ms.Failed = true;
                if (!_loggedNoRead) { _loggedNoRead = true; BtcLog.Warn("Phantom", "no outline (mesh " + mesh.name + "): " + e.Message + "; body only"); }
            }
            return ms;
        }

        /// <summary>Copy with every submesh merged into one (one material draws the whole body), optionally with moved vertices.</summary>
        private static Mesh Single(Mesh src, int[] tris, Vector3[] verts)
        {
            Mesh m = UnityEngine.Object.Instantiate(src);
            m.name = Tag + "Mesh";
            m.hideFlags = HideFlags.HideAndDontSave;
            if (verts != null) m.vertices = verts;
            m.subMeshCount = 1;
            m.triangles = tris;
            m.bounds = src.bounds;
            return m;
        }
    }
}
