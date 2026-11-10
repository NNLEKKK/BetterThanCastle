using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Clean surface of the legacy plate engine (BtcPlate) for the Profile tab: every setting by key with its range (Get / Set / Reset),
    /// text export / import for profiles, and the editor canvas that draws the plate in real plate pixels and returns the geometry of the
    /// draggable parts. The wire format and the lobby rendering stay untouched (BtcPlate.Data / PlateMsg).
    /// </summary>
    public static partial class BtcPlate
    {
        internal struct Def
        {
            internal string Key; internal int Lo, Hi, Default;
            internal Def(string k, int lo, int hi, int d) { Key = k; Lo = lo; Hi = hi; Default = d; }
        }

        // keys of the int settings (hex colours are ints too). speed is a float (x10), nf is a string.
        internal static readonly Def[] Defs =
        {
            new Def("style", 0, 4, 0), new Def("a", 0, 0xFFFFFF, 0xFFAA00), new Def("b", 0, 0xFFFFFF, 0xFF00CC), new Def("grad", 0, 2, 0), new Def("g", 0, 0xFFFFFF, 0x00CCFF),
            new Def("fm", 0, 3, 0), new Def("fc", 0, 0xFFFFFF, 0xFFFFFF), new Def("fw", 1, 6, 2),
            new Def("nm", 0, 3, 0), new Def("nc", 0, 0xFFFFFF, 0), new Def("nw", 1, 4, 1), new Def("fs", 0, 4, 0), new Def("na", 0, 9, 0),
            new Def("ox", -200, 310, 0), new Def("oy", -200, 200, 0), new Def("nsz", 10, 60, 20), new Def("asz", 10, 60, 20),
            new Def("nx", 0, 4095, 0), new Def("nxs", 1, 30, 10), new Def("nxi", 1, 10, 5),
            new Def("ph", 10, 30, 10), new Def("sc", 0, 1, 1), new Def("ax", -120, 390, 0), new Def("ay", -120, 390, 0),
            new Def("im", 0, 2, 0), new Def("io", 1, 20, 20), new Def("idm", 0, 16, 0)
        };

        internal static Def DefOf(string key)
        {
            for (int i = 0; i < Defs.Length; i++) if (Defs[i].Key == key) return Defs[i];
            return default(Def);
        }

        internal static void EnsureLoaded() { Load(); EnsureHook(); }

        internal static int Get(string key)
        {
            Load();
            switch (key)
            {
                case "style": return _style; case "a": return _a; case "b": return _b; case "grad": return _grad; case "g": return _g;
                case "fm": return _fm; case "fc": return _fc; case "fw": return _fw;
                case "nm": return _nm; case "nc": return _nc; case "nw": return _nw; case "fs": return _fs; case "na": return _na;
                case "ox": return _ox; case "oy": return _oy; case "nsz": return _nsz; case "asz": return _asz;
                case "nx": return _nx; case "nxs": return _nxs; case "nxi": return _nxi;
                case "ph": return _ph; case "sc": return _sc; case "ax": return _ax; case "ay": return _ay;
                case "im": return _im; case "io": return _io; case "idm": return _idm;
            }
            return 0;
        }

        /// <summary>Sets a setting (clamped to its range) and queues the save + re-announce to the lobby. Returns false when nothing changed.</summary>
        internal static bool Set(string key, int v)
        {
            Load();
            Def d = DefOf(key);
            if (d.Key == null) return false;
            v = Mathf.Clamp(v, d.Lo, d.Hi);
            if (Get(key) == v) return false;
            switch (key)
            {
                case "style": _style = v; break; case "a": _a = v; _hexA = v.ToString("X6"); break; case "b": _b = v; _hexB = v.ToString("X6"); break;
                case "grad": _grad = v; break; case "g": _g = v; _hexG = v.ToString("X6"); break;
                case "fm": _fm = v; break; case "fc": _fc = v; _hexF = v.ToString("X6"); break; case "fw": _fw = v; break;
                case "nm": _nm = v; break; case "nc": _nc = v; _hexN = v.ToString("X6"); break; case "nw": _nw = v; break; case "fs": _fs = v; break; case "na": _na = v; break;
                case "ox": _ox = v; break; case "oy": _oy = v; break; case "nsz": _nsz = v; break; case "asz": _asz = v; break;
                case "nx": _nx = v; break; case "nxs": _nxs = v; break; case "nxi": _nxi = v; break;
                case "ph": _ph = v; break; case "sc": _sc = v; break; case "ax": _ax = v; break; case "ay": _ay = v; break;
                case "im": _im = v; break; case "io": _io = v; break; case "idm": _idm = v; break;
            }
            Changed();
            return true;
        }

        internal static float SpeedF { get { Load(); return _speed; } }
        internal static void SetSpeed(float v)
        {
            Load();
            v = Mathf.Clamp(Mathf.Round(v * 10f) / 10f, 0.1f, 3f);
            if (Mathf.Abs(v - _speed) < 0.001f) return;
            _speed = v;
            Changed();
        }

        internal static string FontName { get { Load(); return _nf ?? ""; } }
        internal static void SetFont(string name)
        {
            Load();
            string c = CleanFont(name ?? "");
            if (c == _nf) return;
            _nf = c;
            _nfEdit = c;
            Changed();
        }

        internal static string[] FontNames()
        {
            string[] list = FontList();
            return list;
        }

        internal static string[] LetterFxNames() { return FxNames(); }

        /// <summary>Snapshot of every plate setting as "key=value" lines (the btc_plate.txt format): used by undo and profiles.</summary>
        internal static string Export()
        {
            Load();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < Defs.Length; i++)
            {
                int v = Get(Defs[i].Key);
                bool hex = Defs[i].Hi == 0xFFFFFF;
                sb.Append(Defs[i].Key).Append('=').Append(hex ? v.ToString("X6") : v.ToString(CultureInfo.InvariantCulture)).Append("\n");
            }
            sb.Append("speed=").Append(_speed.ToString("0.0", CultureInfo.InvariantCulture)).Append("\n");
            sb.Append("nf=").Append(_nf ?? "").Append("\n");
            return sb.ToString();
        }

        /// <summary>Applies an Export() text (unknown or broken lines are skipped).</summary>
        internal static void Import(string text)
        {
            Load();
            if (string.IsNullOrEmpty(text)) return;
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i].Trim();
                int eq = l.IndexOf('=');
                if (eq <= 0) continue;
                string k = l.Substring(0, eq), v = l.Substring(eq + 1);
                if (k == "speed") { float f; if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) SetSpeed(f); continue; }
                if (k == "nf") { SetFont(v); continue; }
                Def d = DefOf(k);
                if (d.Key == null) continue;
                int n;
                bool ok = d.Hi == 0xFFFFFF ? int.TryParse(v, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n) : int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
                if (ok) Set(k, n);
            }
        }

        internal static void ResetAll()
        {
            for (int i = 0; i < Defs.Length; i++) Set(Defs[i].Key, Defs[i].Default);
            SetSpeed(1f);
            SetFont("");
        }

        // ---- editor canvas
        /// <summary>Measured real plate size (the lobby must have been open once) or a default of 256 x 64 at 100% height.</summary>
        internal static float RealW { get { GeoEnsure(); return G.W; } }
        internal static float RealBaseH
        {
            get
            {
                float k = Mathf.Max(1f, _ph / 10f);
                GeoEnsure();
                return G.BaseH;
            }
        }
        internal static bool Measured { get { GeoEnsure(); return G.Real; } }

        /// <summary>
        /// Draws the plate of the local settings into rect (real plate pixels x k): background, image, frame, avatar placeholder, nick with outline / letter effects.
        /// Returns the rects of the avatar and the nick text area so the tab can put drag handles on them. nick is the plain nick text.
        /// </summary>
        internal static void DrawCanvas(Rect plate, float k, string nick, Texture2D avatar, GUIStyle baseStyle, Texture2D px, out Rect avRect, out Rect nickRect, out Rect textRect)
        {
            Load();
            GeoEnsure();
            Data d = FieldsData();
            float t = Time.unscaledTime;
            float baseH = RealBaseH;
            Color c = new Color(1f, 0.9f, 0.2f, 1f);
            if (d.style > 0) c = ColorFor(d, t);
            else if (d.grad > 0) c = ToColor(d.a);
            if (d.fm > 0)
            {
                float fw = d.fw * k;
                Box(new Rect(plate.x - fw, plate.y - fw, plate.width + fw * 2f, plate.height + fw * 2f), EffCol(d.fm, d.fc, d.speed, t), px);
            }
            Fill(plate, c, ToColor(d.g), d.grad, px);
            BtcPlateImg.Anim img = BtcPlateImg.Local;
            if (d.im > 0 && img != null && img.tex != null)
            {
                img.used = Time.unscaledTime;
                float dim = 1f - d.idm * 0.05f;
                Color oc = GUI.color;
                GUI.color = new Color(dim, dim, dim, d.io / 20f);
                GUI.DrawTextureWithTexCoords(plate, img.tex, CoverUv(img.w, img.h, plate.width / Mathf.Max(1f, plate.height), d.im == 2));
                GUI.color = oc;
            }
            float f = k * d.asz / 20f * PlateK(d);
            float pw = G.AvW * f, ph2 = G.AvH * f;
            float pl = plate.x + (G.AvPx + (d.ax * 2 - 120)) * k - G.AvPivX * pw, pt = plate.y + (G.AvPy - (d.ay * 2 - 120)) * k - (1f - G.AvPivY) * ph2;
            avRect = new Rect(pl + G.VisX0 * pw, pt + G.VisY0 * ph2, (G.VisX1 - G.VisX0) * pw, (G.VisY1 - G.VisY0) * ph2);
            Box(avRect, new Color(0.15f, 0.15f, 0.15f, 1f), px);
            if (avatar != null) GUI.DrawTexture(new Rect(avRect.x + 1f, avRect.y + 1f, avRect.width - 2f, avRect.height - 2f), avatar, ScaleMode.ScaleToFit, true);

            if (_pvStyle == null)
            {
                _pvStyle = new GUIStyle(baseStyle);
                _pvStyle.richText = true;
                _pvStyle.alignment = TextAnchor.MiddleLeft;
                _pvOut = new GUIStyle(_pvStyle);
            }
            Font font = GetFont(d.nf);
            _pvStyle.font = font; _pvOut.font = font;
            FontStyle fst = d.fs > 0 ? StyleOf(d.fs) : FontStyle.Normal;
            _pvStyle.fontStyle = fst; _pvOut.fontStyle = fst;
            int size = Mathf.Max(6, Mathf.RoundToInt(G.Font * k * d.nsz / 20f * PlateK(d)));
            _pvStyle.fontSize = size; _pvOut.fontSize = size;
            _pvStyle.clipping = TextClipping.Overflow; _pvOut.clipping = TextClipping.Overflow;
            TextAnchor al = (TextAnchor)(d.na > 0 ? d.na - 1 : G.Align);
            _pvStyle.alignment = al; _pvOut.alignment = al;
            nickRect = new Rect(plate.x + (G.NickX0 + (d.ox - 100) * 2f) * k, plate.y + (G.NickY0 - (d.oy - 100) * 2f) * k, (G.NickX1 - G.NickX0) * k, (G.NickY1 - G.NickY0) * k);
            string plain = StripTags(nick);
            Vector2 sz = _pvStyle.CalcSize(new GUIContent(plain));
            Rect lineBox = TextRect(nickRect, al, sz);
            float vh = Mathf.Min(sz.y, size * 0.8f);
            textRect = new Rect(lineBox.x, lineBox.y + (lineBox.height - vh) * 0.5f, lineBox.width, vh);
            if (d.nx != 0) { PvFx(nickRect, plain, d, t, size); return; }
            if (d.nm > 0 && Event.current.type == EventType.Repaint)
            {
                _pvOut.normal.textColor = EffCol(d.nm, d.nc, d.speed, t);
                _pvOut.richText = false;
                float w = d.nw * k;
                for (int i = 0; i < 8; i++) _pvOut.Draw(new Rect(nickRect.x + Dx[i] * w, nickRect.y + Dy[i] * w, nickRect.width, nickRect.height), plain, false, false, false, false);
            }
            GUI.Label(nickRect, nick, _pvStyle);
        }

        private static Rect TextRect(Rect area, TextAnchor al, Vector2 sz)
        {
            float x = area.x, y = area.y;
            int a = (int)al;
            int col = a % 3, row = a / 3;
            x += col == 0 ? 0f : (col == 1 ? (area.width - sz.x) * 0.5f : area.width - sz.x);
            y += row == 0 ? 0f : (row == 1 ? (area.height - sz.y) * 0.5f : area.height - sz.y);
            return new Rect(x, y, sz.x, sz.y);
        }
    }
}
