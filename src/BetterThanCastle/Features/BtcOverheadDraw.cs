using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.NewUI.OverheadDisplay;
using Brix.Util;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle
{
    // Performance/FastOverheads (local, default ON, live switch): the icons / name tags / thought bubbles over bricktrons
    // are drawn by the mod from the vanilla overheads' own geometry.
    // Why: vanilla moves every shown overhead each frame the camera moves (TransformConstraint), and Unity dispatches the
    // transform change to every active RectTransform under it (~11 us each, 55 shown units ~10 ms per frame; lab
    // overhead-nrec2, docs/lessons.md 2026-11-27). A canvas of its own per overhead does not help (same transforms move).
    // How: the vanilla overhead stays parked (Constrain only stores the screen point) with its canvases disabled; all vanilla
    // logic (show rules, lines, animators, layout, sorting) keeps running. The mod asks each visible Graphic for its mesh the
    // way the canvas does (OnPopulateMesh + mesh modifiers, CanvasGroup/CanvasRenderer alpha and colour), caches it relative to
    // the parked anchor and draws all overheads in the vanilla sort order (canvas sortingOrder, then hierarchy) with one mesh
    // from an own camera after the world. Geometry is rebuilt only when a graphic is marked dirty, enabled/disabled, moved or
    // faded inside its overhead (animations), or a font atlas is rebuilt.
    internal static class BtcOverheadDraw
    {
        private sealed class G
        {
            public Graphic Gr;
            public Canvas SortCanvas;      // nearest canvas with overrideSorting (decides the draw order)
            public bool Emitted;
            public Matrix4x4 Rel;          // localToWorld relative to the parked anchor at the last build
            public float CrAlpha;
            public Color CrColor;
        }

        private sealed class Item
        {
            public Canvas Canvas;
            public int Mat, Start, Count, Seq;
        }

        private sealed class Entry
        {
            public OverheadDisplay Od;
            public TransformConstraint Tc;
            public RectTransform Slave;
            public Canvas[] Canvases;
            public bool[] CanvasWasOn;
            public CanvasGroup[] Groups;
            public float[] GroupAlpha;
            public readonly List<G> Gs = new List<G>();
            public readonly List<Vector3> Pos = new List<Vector3>();
            public readonly List<Color32> Col = new List<Color32>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<Item> Items = new List<Item>();
            public Vector3 Parked;
            public Vector3 Target;
            public bool HasTarget, Applied, Dirty = true, Restructure = true;
            public int Sibling;
        }

        private static readonly Dictionary<int, Entry> ByTc = new Dictionary<int, Entry>();
        private static readonly Dictionary<int, Entry> ByRoot = new Dictionary<int, Entry>();
        private static readonly Dictionary<int, Entry> ByGraphic = new Dictionary<int, Entry>();   // null value = not ours (negative cache)
        private static readonly List<Entry> All = new List<Entry>();
        private static readonly List<Entry> DeadList = new List<Entry>();
        // a disabled canvas reports overrideSorting=false: remember which ones had it while they were on
        private static readonly HashSet<int> Overrides = new HashSet<int>();
        private static bool IsOverride(Canvas c)
        {
            if (c.enabled) { if (c.overrideSorting) Overrides.Add(c.GetInstanceID()); else Overrides.Remove(c.GetInstanceID()); return c.overrideSorting; }
            return Overrides.Contains(c.GetInstanceID());
        }

        private static Camera _main, _cam;
        private static Transform _mainT, _origin;
        private static Canvas _originRoot;
        private static bool _wasOn, _frameDirty, _hooked;
        private static Mesh _mesh;

        // materials: (source material, texture) -> clone with that texture, _TextureSampleAdd and an open clip rect
        private static readonly Dictionary<long, int> MatIndex = new Dictionary<long, int>();
        private static readonly List<Material> Mats = new List<Material>();

        private static Action<Graphic, VertexHelper> _populate;
        private static readonly VertexHelper Vh = new VertexHelper();
        private static readonly List<UIVertex> Stream = new List<UIVertex>();
        private static readonly List<Component> Mods = new List<Component>();

        internal static int Regens, LastVerts;
        internal static bool On { get { return BtcConfig.FastOverheads == null || BtcConfig.FastOverheads.Value; } }

        // ---------------- registration ----------------
        internal static void OnCreated(OverheadDisplayDriver driver, OverheadDisplay od)
        {
            if (od == null) return;
            TransformConstraint tc = od.GetComponent<TransformConstraint>();
            if (tc == null) return;
            Entry e;
            ByTc.TryGetValue(tc.GetInstanceID(), out e);
            if (!(driver is BricktronOverheadDisplayDriver))
            {
                if (e != null) { Unapply(e); Remove(e); }   // pooled object reused for a barrel / beacon: vanilla again
                return;
            }
            if (e == null)
            {
                e = new Entry { Od = od, Tc = tc };
                ByTc[tc.GetInstanceID()] = e;
                ByRoot[od.transform.GetInstanceID()] = e;
                All.Add(e);
            }
            e.Slave = tc.slaveRectTransform;
            e.Restructure = true; e.Dirty = true; e.HasTarget = false;
            Transform parent = od.transform.parent;
            if (parent != null)
            {
                Canvas pc = parent.GetComponentInParent<Canvas>();
                if (pc != null) { _origin = parent; _originRoot = pc.rootCanvas; }
            }
            Hook();
            EnsureCam();
            if (On) Apply(e);
        }

        private static void Remove(Entry e)
        {
            if (e.Tc != null) ByTc.Remove(e.Tc.GetInstanceID());
            if (e.Od != null) ByRoot.Remove(e.Od.transform.GetInstanceID());
            for (int i = 0; i < e.Gs.Count; i++) if (e.Gs[i].Gr != null) ByGraphic.Remove(e.Gs[i].Gr.GetInstanceID());
            All.Remove(e);
            _frameDirty = true;
        }

        private static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            try
            {
                MethodInfo mi = typeof(Graphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null);
                if (mi != null) _populate = (Action<Graphic, VertexHelper>)Delegate.CreateDelegate(typeof(Action<Graphic, VertexHelper>), mi);
                else BtcLog.Error("Overhead", "Graphic.OnPopulateMesh(VertexHelper) not found: fast overheads off");
                Font.textureRebuilt += f => { for (int i = 0; i < All.Count; i++) All[i].Dirty = true; };
            }
            catch (Exception ex) { BtcLog.Error("Overhead", "hook: " + ex.Message); }
        }

        private static void Apply(Entry e)
        {
            if (e.Applied || _populate == null) return;
            if (e.Canvases == null || e.Restructure) Capture(e);
            for (int i = 0; i < e.Canvases.Length; i++) if (e.Canvases[i] != null) { IsOverride(e.Canvases[i]); e.CanvasWasOn[i] = e.Canvases[i].enabled; e.Canvases[i].enabled = false; }
            // parked far off screen: even a canvas switched on later by someone else draws nothing visible
            if (e.Slave != null) e.Slave.position = new Vector3(-20000f, -20000f, e.Slave.position.z);
            e.Applied = true; e.Dirty = true; _frameDirty = true;
        }

        private static void Unapply(Entry e)
        {
            if (!e.Applied) return;
            e.Applied = false; e.HasTarget = false;
            if (e.Canvases != null)
                for (int i = 0; i < e.Canvases.Length; i++) if (e.Canvases[i] != null) e.Canvases[i].enabled = e.CanvasWasOn[i];
            if (e.Tc != null && e.Tc.isActiveAndEnabled) e.Tc.Constrain();   // back to the vanilla place at once
            _frameDirty = true;
        }

        // structure of one overhead: graphics in draw (hierarchy) order, canvases, canvas groups
        private static void Capture(Entry e)
        {
            for (int i = 0; i < e.Gs.Count; i++) if (e.Gs[i].Gr != null) ByGraphic.Remove(e.Gs[i].Gr.GetInstanceID());
            e.Gs.Clear();
            Graphic[] gs = e.Od.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < gs.Length; i++)
            {
                G g = new G { Gr = gs[i] };
                for (Transform t = gs[i].transform; t != null; t = t.parent)
                {
                    Canvas c = t.GetComponent<Canvas>();
                    if (c != null && IsOverride(c)) { g.SortCanvas = c; break; }
                    if (t == e.Od.transform) break;
                }
                if (g.SortCanvas == null) g.SortCanvas = e.Od.GetComponent<Canvas>();
                e.Gs.Add(g);
                ByGraphic[gs[i].GetInstanceID()] = e;
            }
            Canvas[] cs = e.Od.GetComponentsInChildren<Canvas>(true);
            if (e.Canvases != null && e.Applied)
            {
                // keep the saved vanilla states of canvases we already switched off; new ones are switched off now
                List<bool> was = new List<bool>();
                for (int i = 0; i < cs.Length; i++)
                {
                    int k = Array.IndexOf(e.Canvases, cs[i]);
                    was.Add(k >= 0 ? e.CanvasWasOn[k] : cs[i].enabled);
                    IsOverride(cs[i]); cs[i].enabled = false;
                }
                e.CanvasWasOn = was.ToArray();
            }
            else e.CanvasWasOn = new bool[cs.Length];
            e.Canvases = cs;
            e.Groups = e.Od.GetComponentsInChildren<CanvasGroup>(true);
            e.GroupAlpha = new float[e.Groups.Length];
            for (int i = 0; i < e.Groups.Length; i++) e.GroupAlpha[i] = e.Groups[i].alpha;
            e.Restructure = false; e.Dirty = true;
        }

        // ---------------- hooks from patches ----------------
        internal static bool Constrain(TransformConstraint tc)
        {
            Entry e;
            if (All.Count == 0 || !ByTc.TryGetValue(tc.GetInstanceID(), out e) || !e.Applied) return true;
            if (tc.masterTransform == null) return false;
            if (_main == null || !_main.isActiveAndEnabled) { _main = Camera.main; _mainT = _main != null ? _main.transform : null; }
            if (_main == null) return false;
            Vector3 p = tc.cameraSpace == TransformConstraint.CameraSpace.Screen
                ? _main.WorldToScreenPoint(tc.masterTransform.position + tc.worldOffset)
                : _main.WorldToViewportPoint(tc.masterTransform.position + tc.worldOffset);
            tc.outDistance = p.z;
            if (tc.inverseWhenBehindCamera && Vector3.Dot(tc.masterTransform.position - _mainT.position, _mainT.forward) < 0f) { p.x = -p.x; p.y = -p.y; }
            p.z = 0f;
            Vector3 t = Calcule.RoundVecteur(p) + tc.screenOffset;
            t.z = 0f;
            if (!e.HasTarget || t != e.Target) { e.Target = t; e.HasTarget = true; _frameDirty = true; }
            return false;
        }

        internal static void GraphicDirty(Graphic g)
        {
            if (All.Count == 0 || g == null) return;
            int id = g.GetInstanceID();
            Entry e;
            if (ByGraphic.TryGetValue(id, out e))
            {
                if (e != null) e.Dirty = true;
                return;
            }
            // a graphic we have not seen: a new line spawned into one of our overheads, or any other UI (cached as not ours)
            Transform t = g.transform;
            for (int i = 0; i < 16 && t != null; i++, t = t.parent)
                if (ByRoot.TryGetValue(t.GetInstanceID(), out e)) { e.Restructure = true; e.Dirty = true; return; }
            if (ByGraphic.Count > 30000) ByGraphic.Clear();
            ByGraphic[id] = null;
        }

        // ---------------- per frame ----------------
        internal static void Tick()
        {
            if (All.Count == 0) return;
            bool on = On && _populate != null;
            if (on != _wasOn)
            {
                _wasOn = on;
                for (int i = 0; i < All.Count; i++) { if (on) Apply(All[i]); else Unapply(All[i]); }
                BtcLog.Info("Overhead", "fast overheads " + (on ? "on" : "off") + " units=" + All.Count);
            }
            if (!on) return;
            DeadList.Clear();
            for (int i = 0; i < All.Count; i++)
            {
                Entry e = All[i];
                if (e.Od == null || e.Tc == null) { DeadList.Add(e); continue; }
                if (!e.Applied) Apply(e);
                if (!e.Od.gameObject.activeInHierarchy) continue;
                if (e.Restructure) Capture(e);
                if (!e.Dirty) e.Dirty = Changed(e);
                if (e.Dirty) { Build(e); e.Dirty = false; _frameDirty = true; }
                int sib = e.Od.transform.GetSiblingIndex();
                if (sib != e.Sibling) { e.Sibling = sib; _frameDirty = true; }
            }
            for (int i = 0; i < DeadList.Count; i++) Remove(DeadList[i]);
        }

        private static bool Changed(Entry e)
        {
            for (int i = 0; i < e.Groups.Length; i++)
            {
                CanvasGroup cg = e.Groups[i];
                if (cg == null) return true;
                if (cg.alpha != e.GroupAlpha[i]) return true;
            }
            Vector3 park = e.Slave != null ? e.Slave.position : Vector3.zero;
            for (int i = 0; i < e.Gs.Count; i++)
            {
                G g = e.Gs[i];
                if (!g.Emitted) continue;
                if (g.Gr == null) return true;
                CanvasRenderer cr = g.Gr.canvasRenderer;
                if (cr.GetAlpha() != g.CrAlpha || cr.GetColor() != g.CrColor) return true;
                Matrix4x4 m = g.Gr.transform.localToWorldMatrix;
                m.m03 -= park.x; m.m13 -= park.y; m.m23 -= park.z;
                if (m != g.Rel) return true;
            }
            return false;
        }

        private static float GroupAlpha(Transform t, Transform root)
        {
            float a = 1f;
            for (; t != null; t = t.parent)
            {
                CanvasGroup cg = t.GetComponent<CanvasGroup>();
                if (cg != null) { a *= cg.alpha; if (cg.ignoreParentGroups) break; }
                if (t == root) break;
            }
            return a;
        }

        // geometry of one overhead relative to its parked anchor
        private static void Build(Entry e)
        {
            Regens++;
            e.Pos.Clear(); e.Col.Clear(); e.Uv.Clear(); e.Items.Clear();
            for (int i = 0; i < e.Groups.Length; i++) e.GroupAlpha[i] = e.Groups[i] != null ? e.Groups[i].alpha : 0f;
            Vector3 park = e.Slave != null ? e.Slave.position : Vector3.zero;
            e.Parked = park;
            Transform root = e.Od.transform;
            int seq = 0;
            for (int gi = 0; gi < e.Gs.Count; gi++)
            {
                G g = e.Gs[gi];
                g.Emitted = false;
                Graphic gr = g.Gr;
                if (gr == null || !gr.isActiveAndEnabled) continue;
                CanvasRenderer cr = gr.canvasRenderer;
                if (cr == null || cr.cull) continue;
                float alpha = GroupAlpha(gr.transform, root) * cr.GetAlpha();
                Color tint = cr.GetColor();
                g.CrAlpha = cr.GetAlpha(); g.CrColor = tint;
                Matrix4x4 m = gr.transform.localToWorldMatrix;
                g.Rel = m; g.Rel.m03 -= park.x; g.Rel.m13 -= park.y; g.Rel.m23 -= park.z;
                g.Emitted = true;   // watched for moves/fades even while invisible (a fade-in must rebuild)
                if (alpha * tint.a <= 0.001f) continue;
                Vh.Clear();
                try
                {
                    _populate(gr, Vh);
                    Mods.Clear();
                    gr.GetComponents(typeof(IMeshModifier), Mods);
                    for (int k = 0; k < Mods.Count; k++)
                    {
                        Behaviour b = Mods[k] as Behaviour;
                        if (b != null && !b.enabled) continue;
                        ((IMeshModifier)Mods[k]).ModifyMesh(Vh);
                    }
                }
                catch (Exception ex) { BtcLog.Warn("Overhead", "mesh " + gr.name + ": " + ex.Message); continue; }
                Stream.Clear();
                Vh.GetUIVertexStream(Stream);
                if (Stream.Count == 0) continue;
                int mat = MatFor(gr.materialForRendering, gr.mainTexture);
                if (mat < 0) continue;
                int start = e.Pos.Count;
                float tr = tint.r, tg = tint.g, tb = tint.b, ta = tint.a * alpha;
                // the game runs in Linear colour space: the canvas converts UI vertex colours from gamma to linear, a mesh
                // does not (dark grey 0.125 backgrounds of the carried item pill showed as light grey, user 2026-11-28)
                bool lin = QualitySettings.activeColorSpace == ColorSpace.Linear;
                for (int v = 0; v < Stream.Count; v++)
                {
                    UIVertex u = Stream[v];
                    Vector3 w = m.MultiplyPoint3x4(u.position);
                    e.Pos.Add(new Vector3(w.x - park.x, w.y - park.y, 0f));
                    Color32 c = u.color;
                    float r = c.r * tr / 255f, gg = c.g * tg / 255f, b = c.b * tb / 255f;
                    if (lin) { r = Mathf.GammaToLinearSpace(r); gg = Mathf.GammaToLinearSpace(gg); b = Mathf.GammaToLinearSpace(b); }
                    e.Col.Add(new Color32((byte)(r * 255f + 0.5f), (byte)(gg * 255f + 0.5f), (byte)(b * 255f + 0.5f), (byte)(c.a * ta)));
                    e.Uv.Add(u.uv0);
                }
                Item last = e.Items.Count > 0 ? e.Items[e.Items.Count - 1] : null;
                if (last != null && last.Mat == mat && last.Canvas == g.SortCanvas && last.Start + last.Count == start) last.Count += Stream.Count;
                else e.Items.Add(new Item { Canvas = g.SortCanvas, Mat = mat, Start = start, Count = Stream.Count, Seq = seq++ });
            }
        }

        private static int MatFor(Material src, Texture tex)
        {
            if (src == null) return -1;
            long key = ((long)src.GetInstanceID() << 32) ^ (uint)(tex != null ? tex.GetInstanceID() : 0);
            int idx;
            if (MatIndex.TryGetValue(key, out idx)) return idx;
            Material m = new Material(src);
            m.name = "BtcOverhead " + src.name;
            m.mainTexture = tex;
            Texture2D t2 = tex as Texture2D;
            m.SetVector("_TextureSampleAdd", t2 != null && t2.format == TextureFormat.Alpha8 ? new Vector4(1f, 1f, 1f, 0f) : Vector4.zero);
            m.SetVector("_ClipRect", new Vector4(-32767f, -32767f, 32767f, 32767f));
            idx = Mats.Count;
            Mats.Add(m);
            MatIndex[key] = idx;
            return idx;
        }

        // ---------------- drawing ----------------
        private struct Draw { public int Order, Sib, Seq; public Entry E; public Item It; }
        private sealed class DrawCmp : IComparer<Draw>
        {
            public int Compare(Draw a, Draw b)
            {
                if (a.Order != b.Order) return a.Order < b.Order ? -1 : 1;
                if (a.Sib != b.Sib) return a.Sib < b.Sib ? -1 : 1;
                return a.Seq.CompareTo(b.Seq);
            }
        }
        private static readonly DrawCmp Cmp = new DrawCmp();
        private static readonly List<Draw> Draws = new List<Draw>();
        private static readonly List<Vector3> MPos = new List<Vector3>();
        private static readonly List<Color32> MCol = new List<Color32>();
        private static readonly List<Vector2> MUv = new List<Vector2>();
        private static readonly List<List<int>> MTris = new List<List<int>>();
        private static readonly List<int> SubMat = new List<int>();
        private static int _subs;

        private static void EnsureCam()
        {
            if (_cam != null) return;
            GameObject go = new GameObject("BtcOverheadCam");
            _cam = go.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.Depth;
            _cam.cullingMask = 0;
            _cam.depth = 99f;
            _cam.orthographic = true;
            _cam.useOcclusionCulling = false;
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            go.AddComponent<BtcOverheadDrawCam>();
            _frameDirty = true;
        }

        private static void Rebuild()
        {
            Draws.Clear();
            for (int i = 0; i < All.Count; i++)
            {
                Entry e = All[i];
                if (!e.Applied || !e.HasTarget || e.Items.Count == 0 || e.Od == null || !e.Od.gameObject.activeInHierarchy) continue;
                for (int k = 0; k < e.Items.Count; k++)
                {
                    Item it = e.Items[k];
                    Draws.Add(new Draw { Order = it.Canvas != null ? it.Canvas.sortingOrder : 0, Sib = e.Sibling, Seq = it.Seq, E = e, It = it });
                }
            }
            Draws.Sort(Cmp);
            MPos.Clear(); MCol.Clear(); MUv.Clear(); SubMat.Clear();
            _subs = 0;
            int curMat = -1;
            for (int d = 0; d < Draws.Count; d++)
            {
                Draw dr = Draws[d];
                Item it = dr.It;
                if (MPos.Count + it.Count > 64000) break;
                if (it.Mat != curMat)
                {
                    curMat = it.Mat;
                    if (MTris.Count <= _subs) MTris.Add(new List<int>());
                    MTris[_subs].Clear();
                    SubMat.Add(curMat);
                    _subs++;
                }
                List<int> tris = MTris[_subs - 1];
                Vector3 off = dr.E.Target;
                for (int v = it.Start; v < it.Start + it.Count; v++)
                {
                    tris.Add(MPos.Count);
                    Vector3 p = dr.E.Pos[v];
                    MPos.Add(new Vector3(p.x + off.x, p.y + off.y, 0f));
                    MCol.Add(dr.E.Col[v]);
                    MUv.Add(dr.E.Uv[v]);
                }
            }
            if (_mesh == null) { _mesh = new Mesh(); _mesh.name = "BtcOverheads"; _mesh.MarkDynamic(); }
            _mesh.Clear();
            _mesh.SetVertices(MPos);
            _mesh.SetColors(MCol);
            _mesh.SetUVs(0, MUv);
            _mesh.subMeshCount = Math.Max(1, _subs);
            for (int s = 0; s < _subs; s++) _mesh.SetTriangles(MTris[s], s, false);
            LastVerts = MPos.Count;
        }

        // our camera's OnPostRender: after the world, under the overlay UI (where the vanilla overheads were)
        internal static void Render()
        {
            if (All.Count == 0 || !_wasOn) return;
            bool vis = _origin != null && _origin.gameObject.activeInHierarchy && _originRoot != null && _originRoot.isActiveAndEnabled;
            if (!vis) return;
            // sorting orders change every frame with the camera distance: cheap to compare through the rebuild flag
            if (_frameDirty || OrdersMoved()) { Rebuild(); _frameDirty = false; }
            if (_mesh == null || _subs == 0) return;
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            for (int s = 0; s < _subs; s++)
            {
                Material m = Mats[SubMat[s]];
                if (m == null || !m.SetPass(0)) continue;
                Graphics.DrawMeshNow(_mesh, Matrix4x4.identity, s);
            }
            GL.PopMatrix();
        }

        private static bool OrdersMoved()
        {
            for (int d = 0; d < Draws.Count; d++)
            {
                Canvas c = Draws[d].It.Canvas;
                if ((c != null ? c.sortingOrder : 0) != Draws[d].Order) return true;
            }
            return false;
        }

        internal static string Info()
        {
            int applied = 0, items = 0, verts = 0;
            for (int i = 0; i < All.Count; i++) { if (All[i].Applied) applied++; items += All[i].Items.Count; verts += All[i].Pos.Count; }
            return "units=" + All.Count + " applied=" + applied + " items=" + items + " cachedVerts=" + verts + " meshVerts=" + LastVerts + " subs=" + _subs
                + " mats=" + Mats.Count + " regens=" + Regens + " cam=" + (_cam != null);
        }

        // DEV check: per emitted graphic of the first shown overhead, bbox of our vertices vs its RectTransform corners on screen
        internal static string Check()
        {
            Vector3[] corners = new Vector3[4];
            for (int i = 0; i < All.Count; i++)
            {
                Entry e = All[i];
                if (!e.HasTarget || e.Items.Count == 0) continue;
                System.Text.StringBuilder sb = new System.Text.StringBuilder("check " + e.Od.name + " target=" + e.Target + ":");
                for (int k = 0; k < e.Items.Count; k++)
                {
                    Item it = e.Items[k];
                    Vector2 mn = new Vector2(1e9f, 1e9f), mx = new Vector2(-1e9f, -1e9f);
                    for (int v = it.Start; v < it.Start + it.Count; v++) { mn = Vector2.Min(mn, e.Pos[v]); mx = Vector2.Max(mx, e.Pos[v]); }
                    sb.Append(" [mat=").Append(Mats[it.Mat].name).Append(" n=").Append(it.Count).Append(" box=").Append(mn).Append('-').Append(mx).Append(']');
                }
                for (int g = 0; g < e.Gs.Count; g++)
                {
                    G gg = e.Gs[g];
                    if (!gg.Emitted || gg.Gr == null) continue;
                    gg.Gr.rectTransform.GetWorldCorners(corners);
                    sb.Append(" {").Append(gg.Gr.name).Append(' ').Append(gg.Gr.GetType().Name).Append(" rect=").Append((Vector2)(corners[0] - e.Parked)).Append('-').Append((Vector2)(corners[2] - e.Parked)).Append('}');
                }
                return sb.ToString();
            }
            return "check: nothing shown";
        }
    }

    internal sealed class BtcOverheadDrawCam : MonoBehaviour
    {
        private void OnPostRender()
        {
            try { BtcOverheadDraw.Render(); }
            catch (Exception e) { BtcLog.Error("Overhead", "render: " + e.Message); }
        }

        private void LateUpdate()
        {
            try { BtcOverheadDraw.Tick(); }
            catch (Exception e) { BtcLog.Error("Overhead", "tick: " + e.Message); }
        }
    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(OverheadDisplayDriver), "CreateOverheadDisplay")]
    internal static class OverheadDrawCreatePatch
    {
        private static void Postfix(OverheadDisplayDriver __instance, OverheadDisplay __result)
        {
            try { BtcOverheadDraw.OnCreated(__instance, __result); }
            catch (Exception e) { BtcLog.Error("Hook", "OverheadDraw create: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(TransformConstraint), "Constrain")]
    internal static class OverheadDrawConstrainPatch
    {
        private static bool Prefix(TransformConstraint __instance)
        {
            try { return BtcOverheadDraw.Constrain(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "OverheadDraw constrain: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(Graphic), "SetVerticesDirty")]
    internal static class OverheadDrawVertsDirtyPatch
    {
        private static void Postfix(Graphic __instance) { try { BtcOverheadDraw.GraphicDirty(__instance); } catch (Exception e) { BtcLog.Error("Hook", "OverheadDraw dirty: " + e.Message); } }
    }

    [HarmonyPatch(typeof(Graphic), "SetMaterialDirty")]
    internal static class OverheadDrawMatDirtyPatch
    {
        private static void Postfix(Graphic __instance) { try { BtcOverheadDraw.GraphicDirty(__instance); } catch (Exception e) { BtcLog.Error("Hook", "OverheadDraw dirty: " + e.Message); } }
    }

    // enable/disable of a graphic (a line shown or hidden by SetActive): OnEnable already marks it dirty, OnDisable does not
    [HarmonyPatch(typeof(Graphic), "OnDisable")]
    internal static class OverheadDrawDisablePatch
    {
        private static void Postfix(Graphic __instance) { try { BtcOverheadDraw.GraphicDirty(__instance); } catch (Exception e) { BtcLog.Error("Hook", "OverheadDraw disable: " + e.Message); } }
    }
}
