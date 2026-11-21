using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Look of a wormhole ward (BtcWormWard): the coloured (saturated) parts of the sentinel ward materials become white; a switched-off
    /// ward is a dim grey. Per object: the original sharedMaterials are kept and put back when the mark goes (pooled objects are reused
    /// as plain sentinel wards). Particle renderers keep their materials.
    /// </summary>
    internal static class BtcWormWardLook
    {
        private sealed class Saved
        {
            internal Renderer[] Rs;
            internal Material[][] Orig;
            internal Material[][] Mine;
            internal bool Off;
        }

        private static readonly Dictionary<int, Saved> Looks = new Dictionary<int, Saved>();
        private static readonly string[] ColorProps = { "_Color", "_TintColor", "_EmissionColor", "_MainColor", "_GlowColor" };
        private static bool _logged;

        /// <summary>The coloured part is the gem (Castle Story/Crystal shader); the metal base and the gold plates keep their look.</summary>
        private static bool Gem(Material m)
        {
            string s = m.shader != null ? m.shader.name : string.Empty;
            return s.IndexOf("Crystal", StringComparison.OrdinalIgnoreCase) >= 0 || m.name.IndexOf("Gem", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Sprite _icon;
        private static int _iconTries;
        private static Brix.UI.Icons.MetaIcon _meta;

        internal const string IconDb = "BtcIcons", IconName = "_BtcWormWard";
        internal const int IconId = 1700017;      // never registered in IconKey's tables (their ids travel over the network: NetworkIcons)

        /// <summary>Icon key of the wormhole ward (recipe icon/drawing, pie "Icon : _BtcWormWard"). Not registered with IconKey, so vanilla
        /// key ids stay the same; IconDB gets the icon under database BtcIcons (EnsureIcon), IconKey.FromId knows the id (patch).</summary>
        internal static readonly Brix.UI.Icons.IconKey Key = MakeKey();

        private static Brix.UI.Icons.IconKey MakeKey()
        {
            Brix.UI.Icons.IconKey k = (Brix.UI.Icons.IconKey)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Brix.UI.Icons.IconKey));
            k.Database = IconDb;
            k.Name = IconName;
            System.Reflection.FieldInfo f = typeof(Brix.UI.Icons.IconKey).GetField("id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f != null) f.SetValue(k, IconId);
            return k;
        }

        /// <summary>The MetaIcon of the wormhole ward, registered in IconDB once the sentinel icon exists; the sentinel's own as fallback.</summary>
        internal static Brix.UI.Icons.MetaIcon EnsureIcon()
        {
            if (_meta != null) return _meta;
            Sprite s = Icon64();
            if (s == null) return null;
            try
            {
                _meta = new Brix.UI.Icons.MetaIcon { Name = IconName, Image64 = s, Image32 = s };
                if (Brix.UI.Icons.IconDB._Unsafe_GetIcon_NEVERUSE(IconDb, IconName) == null) Brix.UI.Icons.IconDB.AddIcon(IconDb, IconName, _meta);
            }
            catch (Exception e)
            {
                // IconDB._databases[IconDb] does not exist until AddIcon; _Unsafe_GetIcon_NEVERUSE(db, ..) throws then
                try { Brix.UI.Icons.IconDB.AddIcon(IconDb, IconName, _meta); }
                catch (Exception e2) { BtcLog.Error("WormWard", "icon db: " + e.Message + " / " + e2.Message); }
            }
            return _meta;
        }

        /// <summary>The 64 px sentinel ward icon with its red gem turned white (the gold plates keep their hue). Null while it cannot be made.</summary>
        internal static Sprite Icon64()
        {
            if (_icon != null || _iconTries > 20) return _icon;
            _iconTries++;
            try
            {
                Sprite src = Brix.UI.Icons.IconKeys._Sentinel.Get64();
                if (src == null || src.texture == null) return null;
                Texture2D tex = src.texture;
                Rect r = src.textureRect;
                // the atlas holds the trimmed image (textureRect); the sprite itself is rect-sized with transparent margins (textureRectOffset):
                // draw the trimmed part at its offset into a rect-sized texture, so the icon keeps the vanilla size in the UI
                int w = Mathf.Max(1, Mathf.RoundToInt(src.rect.width)), h = Mathf.Max(1, Mathf.RoundToInt(src.rect.height));
                Vector2 off = src.textureRectOffset;
                RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                RenderTexture keep = RenderTexture.active;
                RenderTexture.active = rt;
                GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
                GL.PushMatrix();
                GL.LoadPixelMatrix(0f, w, h, 0f);
                Graphics.DrawTexture(new Rect(off.x, h - off.y - r.height, r.width, r.height), tex, new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height), 0, 0, 0, 0);
                GL.PopMatrix();
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
                RenderTexture.active = keep;
                RenderTexture.ReleaseTemporary(rt);
                Color[] px = t.GetPixels();
                int n = 0;
                for (int i = 0; i < px.Length; i++)
                {
                    float hu, s, v;
                    Color.RGBToHSV(px[i], out hu, out s, out v);
                    if (s < 0.35f || (hu > 0.07f && hu < 0.8f)) continue;     // red/pink gem only
                    float g = Mathf.Clamp01(0.35f + v * 0.75f);
                    px[i] = new Color(g, g, g, px[i].a);
                    n++;
                }
                t.SetPixels(px);
                t.Apply(false, false);
#if DEV
                try { System.IO.File.WriteAllBytes(System.IO.Path.Combine(Dev.BtcDevPaths.LogDir, "wormicon.png"), t.EncodeToPNG()); } catch { }
#endif
                _icon = Sprite.Create(t, new Rect(0f, 0f, w, h), new Vector2(src.pivot.x / w, src.pivot.y / h), src.pixelsPerUnit);
                _icon.name = "BtcWormWardIcon";
                BtcLog.Info("WormWard", "icon " + w + "x" + h + " (atlas part " + r.width + "x" + r.height + " at " + off + "), " + n + " gem pixel(s) whitened");
            }
            catch (Exception e) { BtcLog.Error("WormWard", "icon: " + e); _icon = null; }
            return _icon;
        }

        /// <summary>The renderer shows a sentinel ward gem (also a held prop; already whitened ones count too).</summary>
        internal static bool IsGemOf(Renderer r)
        {
            Material m = r.sharedMaterial;
            if (m == null) return false;
            string n = m.name;
            return n.IndexOf("SentinelWard", StringComparison.OrdinalIgnoreCase) >= 0 && Gem(m);
        }

        internal static bool Has(GameObject go) { return go != null && Looks.ContainsKey(go.GetInstanceID()); }

        internal static void Apply(GameObject go, bool off)
        {
            if (go == null) return;
            try
            {
                int id = go.GetInstanceID();
                Saved s;
                if (Looks.TryGetValue(id, out s))
                {
                    if (s.Off == off) return;
                    Restore(go);
                }
                s = new Saved { Off = off };
                List<Renderer> rs = new List<Renderer>();
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                    if (r != null && !(r is ParticleSystemRenderer) && !(r is LineRenderer)) rs.Add(r);
                s.Rs = rs.ToArray();
                s.Orig = new Material[s.Rs.Length][];
                s.Mine = new Material[s.Rs.Length][];
                int changed = 0;
                for (int i = 0; i < s.Rs.Length; i++)
                {
                    Material[] orig = s.Rs[i].sharedMaterials;
                    s.Orig[i] = orig;
                    Material[] mine = new Material[orig.Length];
                    for (int k = 0; k < orig.Length; k++)
                    {
                        Material m = orig[k];
                        mine[k] = m;
                        if (m == null || !Gem(m)) continue;
                        Material c = null;
                        for (int p = 0; p < ColorProps.Length; p++)
                        {
                            string prop = ColorProps[p];
                            if (!m.HasProperty(prop)) continue;
                            Color col = m.GetColor(prop);
                            float h, sat, v;
                            Color.RGBToHSV(col, out h, out sat, out v);
                            if (sat < 0.25f || v < 0.05f) continue;
                            if (c == null) { c = new Material(m); c.name = m.name + " (BtcWorm)"; }
                            float lum = Mathf.Max(col.r, Mathf.Max(col.g, col.b));    // keep the brightness (HDR glow stays a glow)
                            float k2 = off ? lum * 0.35f : lum;
                            c.SetColor(prop, new Color(k2, k2, k2, col.a));
                        }
                        if (c != null) { mine[k] = c; changed++; }
                    }
                    s.Mine[i] = mine;
                    s.Rs[i].sharedMaterials = mine;
                }
                Looks[id] = s;
                if (!_logged)
                {
                    _logged = true;
                    BtcLog.Info("WormWard", "look: " + s.Rs.Length + " renderer(s), " + changed + " material(s) whitened" + Describe(s));
                }
            }
            catch (Exception e) { BtcLog.Error("WormWard", "look: " + e); }
        }

        internal static void Restore(GameObject go)
        {
            if (go == null) return;
            Saved s;
            int id = go.GetInstanceID();
            if (!Looks.TryGetValue(id, out s)) return;
            Looks.Remove(id);
            try
            {
                for (int i = 0; i < s.Rs.Length; i++)
                {
                    if (s.Rs[i] != null) s.Rs[i].sharedMaterials = s.Orig[i];
                    Material[] mine = s.Mine[i];
                    for (int k = 0; k < mine.Length; k++)
                        if (mine[k] != null && mine[k] != s.Orig[i][k]) UnityEngine.Object.Destroy(mine[k]);
                }
            }
            catch (Exception e) { BtcLog.Error("WormWard", "restore look: " + e); }
        }

        private static string Describe(Saved s)
        {
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            for (int i = 0; i < s.Rs.Length && i < 12; i++)
            {
                b.Append(" | ").Append(s.Rs[i].name).Append(':');
                Material[] o = s.Orig[i];
                for (int k = 0; k < o.Length; k++)
                {
                    if (o[k] == null) continue;
                    b.Append(' ').Append(o[k].name).Append('/').Append(o[k].shader != null ? o[k].shader.name : "?");
                    if (o[k].HasProperty("_Color")) b.Append(" c=").Append(o[k].GetColor("_Color"));
                    if (o[k].HasProperty("_EmissionColor")) b.Append(" e=").Append(o[k].GetColor("_EmissionColor"));
                    if (s.Mine[i][k] != o[k]) b.Append(" *");
                }
            }
            return b.ToString();
        }
    }
}
