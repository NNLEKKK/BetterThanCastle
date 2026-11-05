using System.Collections.Generic;
using Brix.Engine;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay: the field of view of every recorded player as a translucent 3D pyramid in the world. The base is the REAL footprint: the player's
    /// view frustum is sampled along the four screen edges (6 points each) and every sample ray is marched through the voxel terrain, so the outline
    /// follows hills and walls; rays that see only sky end on the ground at the camera's far distance. Real world geometry (Graphics.DrawMesh, depth
    /// tested), one small mesh pair per player, vertices rewritten in place; the voxel marching runs ~14 times a second per player, not every frame.
    /// </summary>
    internal static class BtcPovCone
    {
        private const int N = 6;                 // samples per screen side
        private const int M = N * 4;             // ring points
        private const float MaxFar = 300f;
        private const float Every = 0.07f;

        private sealed class Cone
        {
            internal Mesh Faces, Edges;
            internal readonly Vector3[] Fv = new Vector3[1 + M + 1 + M];
            internal readonly Vector3[] Ev = new Vector3[1 + M];
            internal readonly Color32[] Fc = new Color32[1 + M + 1 + M];
            internal readonly Color32[] Ec = new Color32[1 + M];
            internal readonly Vector3[] Ring = new Vector3[M];
            internal float Next;
            internal bool Has;
        }

        private static readonly Dictionary<int, Cone> Cones = new Dictionary<int, Cone>();
        private static Material _mat;

        private static Material Mat()
        {
            if (_mat != null) return _mat;
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null) { BtcLog.Error("PovCone", "no shader found"); return null; }
            BtcLog.Info("PovCone", "shader " + sh.name);
            _mat = new Material(sh);
            _mat.hideFlags = HideFlags.HideAndDontSave;
            _mat.renderQueue = 3500;
            return _mat;
        }

        private static Cone Get(int fac)
        {
            Cone c;
            if (Cones.TryGetValue(fac, out c) && c.Faces != null) return c;
            c = new Cone();
            c.Faces = new Mesh { name = "BtcConeFaces", hideFlags = HideFlags.HideAndDontSave };
            c.Edges = new Mesh { name = "BtcConeEdges", hideFlags = HideFlags.HideAndDontSave };
            c.Faces.vertices = c.Fv;
            int[] tri = new int[M * 3 * 2];
            for (int i = 0; i < M; i++)
            {
                int j = (i + 1) % M;
                tri[i * 3] = 0; tri[i * 3 + 1] = 1 + i; tri[i * 3 + 2] = 1 + j;
                int b = M * 3 + i * 3;
                tri[b] = M + 1; tri[b + 1] = M + 2 + i; tri[b + 2] = M + 2 + j;
            }
            c.Faces.triangles = tri;
            c.Edges.vertices = c.Ev;
            int[] li = new int[(M + 4) * 2];
            for (int i = 0; i < M; i++) { li[i * 2] = 1 + i; li[i * 2 + 1] = 1 + (i + 1) % M; }
            for (int k = 0; k < 4; k++) { li[(M + k) * 2] = 0; li[(M + k) * 2 + 1] = 1 + k * N; }
            c.Edges.SetIndices(li, MeshTopology.Lines, 0);
            Cones[fac] = c;
            return c;
        }

        // ------------------------------------------------------------------ voxel marching
        private static bool Solid(Vector3 p)
        {
            if (p.y < 0.5f) return true;
            return Voxel.IsNonEmpty(new XYZ(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z)));
        }

        /// <summary>First solid point along o + d*t (d normalised), t in (0, maxD]; false when the ray ends in the air.</summary>
        private static bool March(Vector3 o, Vector3 d, float maxD, out Vector3 hit)
        {
            const float step = 2f;
            float prev = 0f;
            for (float t = step; ; t += step)
            {
                float tt = Mathf.Min(t, maxD);
                if (Solid(o + d * tt))
                {
                    float lo = prev, hi = tt;
                    for (int k = 0; k < 6; k++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        if (Solid(o + d * mid)) hi = mid; else lo = mid;
                    }
                    hit = o + d * hi;
                    return true;
                }
                prev = tt;
                if (tt >= maxD) break;
            }
            hit = o + d * maxD;
            return false;
        }

        /// <summary>Ground height under (x, z): marched down from startY.</summary>
        private static float GroundY(float x, float z, float startY)
        {
            float y = Mathf.Clamp(startY, 2f, 400f);
            Vector3 hit;
            if (March(new Vector3(x, y, z), Vector3.down, y, out hit)) return hit.y;
            return 0.5f;
        }

        private static readonly Vector3[] Dirs = new Vector3[M];
        private static readonly float[] Ys = new float[M];

        private static void Footprint(Cone c, Vector3 apex, Quaternion q, float th, float tw, float far)
        {
            int nh = 0;
            for (int i = 0; i < M; i++)
            {
                int side = i / N, k = i % N;
                float s = (float)k / N;
                float u, v;
                switch (side)
                {
                    case 0: u = -1f + 2f * s; v = 1f; break;
                    case 1: u = 1f; v = 1f - 2f * s; break;
                    case 2: u = 1f - 2f * s; v = -1f; break;
                    default: u = -1f; v = -1f + 2f * s; break;
                }
                Vector3 d = (q * new Vector3(u * tw, v * th, 1f)).normalized;
                Dirs[i] = d;
                Vector3 hit;
                if (March(apex, d, far, out hit)) { Ys[nh++] = hit.y; }
            }
            // one ground plane at the median terrain height under the view: a clean outline instead of a jagged one that follows every hill
            float gy = 0.5f;
            if (nh > 0)
            {
                System.Array.Sort(Ys, 0, nh);
                gy = Ys[nh / 2];
            }
            for (int i = 0; i < M; i++)
            {
                Vector3 d = Dirs[i];
                float t = d.y < -0.02f ? (gy - apex.y) / d.y : -1f;
                if (t > 0f && t <= far) { c.Ring[i] = apex + d * t; continue; }
                float hl = Mathf.Sqrt(d.x * d.x + d.z * d.z);
                Vector2 h = hl > 1e-4f ? new Vector2(d.x, d.z) / hl * far : Vector2.zero;
                c.Ring[i] = new Vector3(apex.x + h.x, gy, apex.z + h.y);
            }
            c.Has = true;
        }

        // ------------------------------------------------------------------ frame
        /// <summary>apex = the player's camera position, q = its rotation; fov/aspect/far from the viewing camera.</summary>
        internal static void Frame(int fac, Vector3 apex, Quaternion q, Color col, float fovDeg, float aspect, float farClip, float alpha, Camera cam)
        {
            Material mat = Mat();
            if (mat == null || cam == null) return;
            Cone c = Get(fac);
            float th = Mathf.Tan(fovDeg * 0.5f * Mathf.Deg2Rad), tw = th * aspect;
            float now = Time.unscaledTime;
            if (!c.Has || now >= c.Next)
            {
                c.Next = now + Every + (fac & 7) * 0.004f;
                Footprint(c, apex, q, th, tw, Mathf.Min(Mathf.Max(farClip, 50f), MaxFar));
            }
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < M; i++) centre += c.Ring[i];
            centre /= M;
            c.Fv[0] = apex; c.Ev[0] = apex;
            for (int i = 0; i < M; i++) { c.Fv[1 + i] = c.Ring[i]; c.Fv[M + 2 + i] = c.Ring[i]; c.Ev[1 + i] = c.Ring[i]; }
            c.Fv[M + 1] = centre;
            byte aSide = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * 0.14f * alpha), 0, 255);
            byte aBase = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * 0.22f * alpha), 0, 255);
            byte aEdge = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * 0.9f * alpha), 0, 255);
            byte aTip = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * 0.35f * alpha), 0, 255);
            byte r = (byte)(col.r * 255f), g = (byte)(col.g * 255f), b = (byte)(col.b * 255f);
            c.Fc[0] = new Color32(r, g, b, 0);
            for (int i = 0; i < M; i++) { c.Fc[1 + i] = new Color32(r, g, b, aSide); c.Fc[M + 2 + i] = new Color32(r, g, b, aBase); c.Ec[1 + i] = new Color32(r, g, b, aEdge); }
            c.Fc[M + 1] = new Color32(r, g, b, aBase);
            c.Ec[0] = new Color32(r, g, b, aTip);
            c.Faces.vertices = c.Fv; c.Faces.colors32 = c.Fc; c.Faces.RecalculateBounds();
            c.Edges.vertices = c.Ev; c.Edges.colors32 = c.Ec; c.Edges.RecalculateBounds();
            Graphics.DrawMesh(c.Faces, Matrix4x4.identity, mat, 0, cam, 0, null, false, false);
            Graphics.DrawMesh(c.Edges, Matrix4x4.identity, mat, 0, cam, 0, null, false, false);
        }

        internal static void Clear()
        {
            foreach (KeyValuePair<int, Cone> kv in Cones)
            {
                if (kv.Value.Faces != null) Object.Destroy(kv.Value.Faces);
                if (kv.Value.Edges != null) Object.Destroy(kv.Value.Edges);
            }
            Cones.Clear();
        }
    }
}
