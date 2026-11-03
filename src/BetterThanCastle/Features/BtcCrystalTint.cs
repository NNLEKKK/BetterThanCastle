using System;
using System.Collections.Generic;
using Brix.Game;
using Brix.Rendering;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Crystals show extended player colours (BtcLobby.ExtendProtos) in their real hue. Findings (DEV crystaldump): the Animator parameters
    /// "Color" / "Creep Color" write _Color, _SecondaryColor, _GlowColor, _LightColor into the MaterialPropertyBlock of the animated source
    /// renderers (ShareMaterialPropertyBlock.sourceRenderer): "Shard NN" = owner colour, "Creep NN" (shader CrystalCorrupted) = creep colour
    /// of the capturer; ShareMaterialPropertyBlock copies the source block to the other shards in Update. After the Animator (LateUpdate)
    /// the animated colour is re-hued to the faction colour (animated value keeps its brightness pattern). Local visuals only.
    /// Vanilla colours: component never added or disabled, nothing touched.
    /// </summary>
    internal sealed class BtcCrystalTint : MonoBehaviour
    {
        private static readonly Dictionary<int, BtcCrystalTint> Map = new Dictionary<int, BtcCrystalTint>();
        private static readonly string[] PropNames = { "_Color", "_SecondaryColor", "_GlowColor", "_LightColor" };
        private static int[] _ids;

        internal bool OwnerExt, CreepExt;
        internal bool OwnerRainbow, CreepRainbow; // "Rainbow" faction: crystal hue cycles through the rainbow instead of a dark tint
        internal Color OwnerCol, CreepCol;
        internal int Applied; // number of property writes (DEV diagnostics)
        private CrystalVisuals cv;
        private Renderer[] src;
        private bool[] isCreep;
        private Vector4[] last; // [renderer * 4 + prop]: value we wrote, so an un-rewritten animator value is not tinted twice
        private MaterialPropertyBlock pb;
        private bool ready;

        internal static bool IsExtended(Faction f, out Color c)
        {
            c = Color.white;
            if (f == null) return false;
            CrystalVisuals.CrystalColorEnum e; Sprite s;
            if (!BtcLobby.ExtraVisuals(f.name, out e, out s)) return false;
            // "Rainbow" (old name "Black") is shown as a rainbow crystal (Force detects it by the zero colour), whatever RGB the faction uses.
            c = (f.name == "Rainbow" || f.name == "Black") ? Color.black : f.FactionColor;
            return true;
        }

        internal static void SetOwner(CrystalVisuals v, Faction f)
        {
            if (v == null) return;
            Color c;
            bool ext = IsExtended(f, out c);
            Force(v, ext, c, true);
        }

        internal static void SetCapturer(CrystalVisuals v, Faction f)
        {
            if (v == null) return;
            Color c;
            bool ext = IsExtended(f, out c);
            Force(v, ext, c, false);
        }

        /// <summary>Set the owner (owner=true) or creep tint of a crystal; ext=false clears it.</summary>
        internal static void Force(CrystalVisuals v, bool ext, Color c, bool owner)
        {
            BtcCrystalTint t = Get(v, ext);
            if (t == null) return;
            bool rb = ext && IsRainbow(c);
            if (owner) { t.OwnerExt = ext; t.OwnerCol = c; } else { t.CreepExt = ext; t.CreepCol = c; }
            if (owner) t.OwnerRainbow = rb; else t.CreepRainbow = rb;
            t.enabled = t.OwnerExt || t.CreepExt;
        }

        // Black (near-zero brightness) extended colour is shown as a rainbow crystal.
        private static bool IsRainbow(Color c) { return c.r < 0.12f && c.g < 0.12f && c.b < 0.12f; }

        private const float RainbowPeriod = 6f;   // seconds per full hue cycle

        internal static BtcCrystalTint Find(CrystalVisuals v)
        {
            BtcCrystalTint t;
            return v != null && Map.TryGetValue(v.GetInstanceID(), out t) ? t : null;
        }

        private static BtcCrystalTint Get(CrystalVisuals v, bool create)
        {
            BtcCrystalTint t;
            int id = v.GetInstanceID();
            if (Map.TryGetValue(id, out t) && t != null) return t;
            if (!create) return null;
            t = v.GetComponent<BtcCrystalTint>();
            if (t == null) t = v.gameObject.AddComponent<BtcCrystalTint>();
            t.cv = v;
            Map[id] = t;
            return t;
        }

        private void OnDestroy() { if (cv != null) Map.Remove(cv.GetInstanceID()); }

        // Rainbow: the whole crystal (source + Share targets) cycles through the hue circle as one colour; colour properties are
        // slightly offset (soft shimmer inside a shard). Saturation 1, brightness >= 1 (idempotent on our own values).
        private static readonly float[] PropHue = { 0f, 0.1f, 0.05f, 0f };
        private Renderer[] tgt;
        private int[] tgtOf;
        private readonly float[] rbV = new float[4];
        private readonly float[] rbA = new float[4];
        private readonly bool[] rbOk = new bool[4];

        private void Setup()
        {
            ready = true;
            if (_ids == null)
            {
                _ids = new int[PropNames.Length];
                for (int i = 0; i < _ids.Length; i++) _ids[i] = Shader.PropertyToID(PropNames[i]);
            }
            ShareMaterialPropertyBlock[] sh = GetComponentsInChildren<ShareMaterialPropertyBlock>(true);
            List<Renderer> l = new List<Renderer>(sh.Length);
            for (int i = 0; i < sh.Length; i++) if (sh[i].sourceRenderer != null && !l.Contains(sh[i].sourceRenderer)) l.Add(sh[i].sourceRenderer);
            src = l.ToArray();
            isCreep = new bool[src.Length];
            for (int i = 0; i < src.Length; i++) isCreep[i] = src[i].name.StartsWith("Creep", StringComparison.Ordinal);
            last = new Vector4[src.Length * _ids.Length];
            pb = new MaterialPropertyBlock();

            List<Renderer> tl = new List<Renderer>();
            List<int> to = new List<int>();
            for (int i = 0; i < sh.Length; i++)
            {
                int s = l.IndexOf(sh[i].sourceRenderer);
                if (s < 0 || sh[i].targetRenderers == null) continue;
                foreach (Renderer t in sh[i].targetRenderers)
                    if (t != null && t != sh[i].sourceRenderer && !tl.Contains(t)) { tl.Add(t); to.Add(s); }
            }
            tgt = tl.ToArray();
            tgtOf = to.ToArray();
        }

        private void LateUpdate()
        {
            if (!ready) Setup();
            float t0 = Time.time / RainbowPeriod;
            for (int r = 0; r < src.Length; r++)
            {
                Renderer rd = src[r];
                if (rd == null) continue;
                bool creep = isCreep[r];
                if (creep ? !CreepExt : !OwnerExt) continue;
                if (creep ? CreepRainbow : OwnerRainbow) { Rainbow(r, rd, t0); continue; }
                Color fc = creep ? CreepCol : OwnerCol;
                float fh, fs, fv;
                Color.RGBToHSV(fc, out fh, out fs, out fv);
                float sf = Mathf.Clamp01(fs * 1.5f), vf = Mathf.Lerp(0.2f, 1f, fv);
                rd.GetPropertyBlock(pb);
                bool dirty = false;
                for (int p = 0; p < _ids.Length; p++)
                {
                    Vector4 cur = pb.GetVector(_ids[p]);
                    int slot = r * _ids.Length + p;
                    if (cur == last[slot] || (cur.x == 0f && cur.y == 0f && cur.z == 0f && cur.w == 0f)) continue;
                    float h, s, v;
                    Color.RGBToHSV(new Color(cur.x, cur.y, cur.z), out h, out s, out v);
                    Color o = Color.HSVToRGB(fh, Mathf.Clamp01(s * sf), Mathf.Clamp01(v * vf));
                    Vector4 nv = new Vector4(o.r, o.g, o.b, cur.w);
                    pb.SetVector(_ids[p], nv);
                    last[slot] = nv;
                    dirty = true;
                    Applied++;
                }
                if (dirty) rd.SetPropertyBlock(pb);
            }
        }

        private void Rainbow(int r, Renderer rd, float t0)
        {
            rd.GetPropertyBlock(pb);
            bool any = false;
            for (int p = 0; p < _ids.Length; p++)
            {
                Vector4 cur = pb.GetVector(_ids[p]);
                rbOk[p] = !(cur.x == 0f && cur.y == 0f && cur.z == 0f && cur.w == 0f);
                if (!rbOk[p]) continue;
                float h, s, v;
                Color.RGBToHSV(new Color(cur.x, cur.y, cur.z), out h, out s, out v);
                rbV[p] = Mathf.Max(v, 1f);
                rbA[p] = cur.w;
                any = true;
            }
            if (!any) return;
            WriteRainbow(t0);
            rd.SetPropertyBlock(pb);
            Applied++;
            // Targets get the same block now (vanilla Share would copy it only next Update: no one-frame lag).
            for (int i = 0; i < tgt.Length; i++)
            {
                if (tgtOf[i] != r || tgt[i] == null) continue;
                tgt[i].SetPropertyBlock(pb);
            }
        }

        private void WriteRainbow(float phase)
        {
            for (int p = 0; p < _ids.Length; p++)
            {
                if (!rbOk[p]) continue;
                float hh = phase + PropHue[p];
                Color o = Color.HSVToRGB(hh - Mathf.Floor(hh), 1f, rbV[p]);
                pb.SetVector(_ids[p], new Vector4(o.r, o.g, o.b, rbA[p]));
            }
        }
    }
}
