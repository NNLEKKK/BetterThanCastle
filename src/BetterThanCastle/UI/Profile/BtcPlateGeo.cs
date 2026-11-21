using System;
using System.Globalization;
using System.IO;
using Brix.UI.Builder.Panel;
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle
{
    /// <summary>
    /// Real layout of the lobby player row, measured from the live UI of the local player (plate size, avatar panel size / pivot / position, the visible
    /// picture inside the avatar panel, nick text rectangle, default font size and alignment) and cached in configs/btc_plate_geo3.txt so the Profile tab
    /// can draw the plate like the lobby even in the main menu. All numbers are real UI pixels at 100% plate height, origin = top-left of the plate.
    /// Until the lobby was opened once the defaults below (measured on the vanilla lobby) are used.
    /// </summary>
    public static partial class BtcPlate
    {
        internal struct Geo
        {
            internal float W, BaseH, AvW, AvH, AvPx, AvPy, AvPivX, AvPivY, VisX0, VisY0, VisX1, VisY1, NickX0, NickX1, NickY0, NickY1, Font;
            internal int Align;
            internal bool Real;
        }

        internal static Geo G = new Geo { W = 1189f, BaseH = 50f, AvW = 41f, AvH = 50f, AvPx = 20.5f, AvPy = 25f, AvPivX = 0.5f, AvPivY = 0.5f, VisX0 = 0f, VisY0 = 0.1f, VisX1 = 1f, VisY1 = 0.9f, NickX0 = 73f, NickX1 = 1157f, NickY0 = 0f, NickY1 = 26f, Font = 16f, Align = 3, Real = false };
        private static bool _geoLoaded, _geoCaptured;

        private static string GeoFile { get { return BtcPaths.Config("btc_plate_geo3.txt"); } }

        internal static void GeoEnsure()
        {
            if (_geoLoaded) return;
            _geoLoaded = true;
            try
            {
                if (!File.Exists(GeoFile)) return;
                string[] l = File.ReadAllLines(GeoFile);
                Geo g = G;
                for (int i = 0; i < l.Length; i++)
                {
                    int eq = l[i].IndexOf('=');
                    if (eq <= 0) continue;
                    string k = l[i].Substring(0, eq).Trim(), v = l[i].Substring(eq + 1).Trim();
                    float f;
                    if (!float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) continue;
                    switch (k)
                    {
                        case "w": g.W = f; break; case "h": g.BaseH = f; break; case "avw": g.AvW = f; break; case "avh": g.AvH = f; break;
                        case "avx": g.AvPx = f; break; case "avy": g.AvPy = f; break; case "pvx": g.AvPivX = f; break; case "pvy": g.AvPivY = f; break;
                        case "vx0": g.VisX0 = f; break; case "vy0": g.VisY0 = f; break; case "vx1": g.VisX1 = f; break; case "vy1": g.VisY1 = f; break;
                        case "nx0": g.NickX0 = f; break; case "nx1": g.NickX1 = f; break; case "ny0": g.NickY0 = f; break; case "ny1": g.NickY1 = f; break;
                        case "font": g.Font = f; break; case "align": g.Align = (int)f; break;
                    }
                }
                if (g.W > 50f && g.BaseH > 10f && g.AvW > 4f && g.AvH > 4f && g.NickX1 > g.NickX0 && g.VisX1 > g.VisX0 && g.VisY1 > g.VisY0) { g.Real = true; G = g; }
            }
            catch (Exception) { }
        }

        private static string F(float f) { return f.ToString("0.###", CultureInfo.InvariantCulture); }

        private static void GeoSave()
        {
            try
            {
                File.WriteAllText(GeoFile, "w=" + F(G.W) + "\nh=" + F(G.BaseH) + "\navw=" + F(G.AvW) + "\navh=" + F(G.AvH) + "\navx=" + F(G.AvPx) + "\navy=" + F(G.AvPy)
                    + "\npvx=" + F(G.AvPivX) + "\npvy=" + F(G.AvPivY) + "\nvx0=" + F(G.VisX0) + "\nvy0=" + F(G.VisY0) + "\nvx1=" + F(G.VisX1) + "\nvy1=" + F(G.VisY1)
                    + "\nnx0=" + F(G.NickX0) + "\nnx1=" + F(G.NickX1) + "\nny0=" + F(G.NickY0) + "\nny1=" + F(G.NickY1) + "\nfont=" + F(G.Font) + "\nalign=" + G.Align + "\n");
            }
            catch (Exception) { }
        }

        /// <summary>Called for the local row every frame until a clean measurement succeeds (the offsets and scales we applied are subtracted again).</summary>
        private static void GeoCapture(Slot s, Data d)
        {
            if (_geoCaptured || s.bg == null) return;
            try
            {
                RectTransform bg = s.bg.rectTransform;
                Rect rb = bg.rect;
                if (rb.width <= 1f || rb.height <= 1f) return;
                float num = d != null ? d.ph / 10f : 1f;
                if (s.hMod && Mathf.Abs(rb.height - s.curH) > 1.5f) return;
                RectTransform art = s.avGo != null ? s.avGo.transform as RectTransform : null;
                if (art == null || art.rect.width <= 1f || art.rect.height <= 1f || s.nick == null) return;
                RectTransform nrt = s.nick.rectTransform;
                if (nrt.rect.width <= 1f) return;
                ThumbnailPanelComponent tpc = s.bg.GetComponentInParent<ThumbnailPanelComponent>();
                RectTransform vis = (tpc != null && tpc._avatarRawImage != null) ? tpc._avatarRawImage.rectTransform : null;
                Vector3[] c = new Vector3[4];
                art.GetWorldCorners(c);
                Vector3 a0 = bg.InverseTransformPoint(c[0]), a2 = bg.InverseTransformPoint(c[2]);
                float avOffX = s.avMod && d != null ? d.ax * 2 - 120 : 0f, avOffY = s.avMod && d != null ? d.ay * 2 - 120 : 0f;
                float mw = Mathf.Abs(a2.x - a0.x), mh = Mathf.Abs(a2.y - a0.y);
                float aLeft = Mathf.Min(a0.x, a2.x) - rb.xMin, aTop = rb.yMax - Mathf.Max(a0.y, a2.y);
                Vector2 pv = art.pivot;
                float vx0 = 0f, vy0 = 0f, vx1 = 1f, vy1 = 1f;
                if (vis != null && vis.rect.width > 1f)
                {
                    // the visible picture (RawImage) inside the avatar panel, as fractions of the panel (top-left origin)
                    vis.GetWorldCorners(c);
                    Vector3 v0 = art.InverseTransformPoint(c[0]), v2 = art.InverseTransformPoint(c[2]);
                    Rect ra = art.rect;
                    vx0 = Mathf.Clamp01((Mathf.Min(v0.x, v2.x) - ra.xMin) / ra.width); vx1 = Mathf.Clamp01((Mathf.Max(v0.x, v2.x) - ra.xMin) / ra.width);
                    vy0 = Mathf.Clamp01((ra.yMax - Mathf.Max(v0.y, v2.y)) / ra.height); vy1 = Mathf.Clamp01((ra.yMax - Mathf.Min(v0.y, v2.y)) / ra.height);
                }
                nrt.GetWorldCorners(c);
                Vector3 n0 = bg.InverseTransformPoint(c[0]), n2 = bg.InverseTransformPoint(c[2]);
                float nOx = s.nickMod && d != null ? (d.ox - 100) * 2 : 0f, nOy = s.nickMod && d != null ? (d.oy - 100) * 2 : 0f;
                float x0 = Mathf.Min(n0.x, n2.x) - rb.xMin - nOx, x1 = Mathf.Max(n0.x, n2.x) - rb.xMin - nOx;
                float top = rb.yMax - Mathf.Max(n0.y, n2.y) + nOy, bot = rb.yMax - Mathf.Min(n0.y, n2.y) + nOy;
                Geo g = new Geo();
                g.W = rb.width;
                g.BaseH = s.hMod ? rb.height / num : rb.height;
                g.AvW = art.rect.width * s.defAvScale.x;
                g.AvH = art.rect.height * s.defAvScale.y;
                g.AvPivX = pv.x; g.AvPivY = pv.y;
                g.AvPx = aLeft + pv.x * mw - avOffX;              // the pivot point stays fixed when the avatar is scaled
                g.AvPy = aTop + (1f - pv.y) * mh + avOffY;
                g.VisX0 = vx0; g.VisY0 = vy0; g.VisX1 = vx1; g.VisY1 = vy1;
                g.NickX0 = Mathf.Max(0f, x0);
                g.NickX1 = Mathf.Min(rb.width, x1);
                g.NickY0 = Mathf.Max(0f, top);
                g.NickY1 = Mathf.Min(rb.height, bot);
                if (g.NickY1 - g.NickY0 < 4f) { g.NickY0 = 0f; g.NickY1 = g.BaseH; }
                g.Font = s.defSize > 4 ? s.defSize : 18f;
                g.Align = (int)s.defAlign;
                g.Real = true;
                G = g;
                _geoCaptured = true;
                GeoSave();
                BtcLog.Info("PlateGeo", "measured plate " + F(g.W) + "x" + F(rb.height) + " (ph " + F(num * 10f) + ") avatar panel " + F(g.AvW) + "x" + F(g.AvH) + " pivot " + F(pv.x) + "," + F(pv.y) + " pivot point " + F(g.AvPx) + "," + F(g.AvPy)
                    + " visible " + F(vx0) + "," + F(vy0) + ".." + F(vx1) + "," + F(vy1) + " raw " + F(aLeft) + "," + F(aTop) + " " + F(mw) + "x" + F(mh)
                    + " nick x " + F(g.NickX0) + ".." + F(g.NickX1) + " y " + F(g.NickY0) + ".." + F(g.NickY1) + " font " + F(g.Font) + " align " + g.Align);
            }
            catch (Exception ex) { BtcLog.Warn("PlateGeo", ex.Message); }
        }
    }
}
