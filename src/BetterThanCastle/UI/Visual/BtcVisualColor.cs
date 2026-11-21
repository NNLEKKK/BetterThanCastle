using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Colour picker of the Visuals tab in the new menu style: a swatch row that opens an inline panel (saturation/value square, hue strip,
    /// hex field, copy / paste icons, recent colours and a neutral preset strip). Every change records a Ctrl+Z step (scope "visual").
    /// The picker never owns a colour: callers pass the current value and a setter, so it works for settings, palette stops and overrides.
    /// </summary>
    internal static class BtcVisualColor
    {
        private static string _open;
        private static float _h, _s, _v;
        private static string _drag;       // "sv" / "hue"
        private static string _hex = "";
        private static string _hexFor;
        private static Texture2D _svTex, _hueTex;
        private static float _svHue = -1f;
        private static readonly List<Color> Recent = new List<Color>();
        private static GUIStyle _field;
        private static readonly Color[] Presets =
        {
            new Color(1f, 1f, 1f), new Color(0.75f, 0.75f, 0.75f), new Color(0.45f, 0.45f, 0.45f), new Color(0.1f, 0.1f, 0.1f),
            new Color(1f, 0.25f, 0.25f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.92f, 0.1f), new Color(0.55f, 0.9f, 0.2f),
            new Color(0.1f, 0.8f, 0.45f), new Color(0.1f, 0.8f, 0.9f), new Color(0.25f, 0.5f, 1f), new Color(0.6f, 0.35f, 1f),
            new Color(1f, 0.25f, 0.9f), new Color(0.9f, 0.4f, 0.6f)
        };

        internal static bool IsOpen(string key) { return _open == key; }
        internal static void Open(string key, Color c) { _open = key; _drag = null; Color.RGBToHSV(c, out _h, out _s, out _v); _hexFor = null; }
        internal static void Close() { _open = null; _drag = null; }

        internal static string Hex(Color c)
        {
            return ((int)Mathf.Round(c.r * 255f)).ToString("X2") + ((int)Mathf.Round(c.g * 255f)).ToString("X2") + ((int)Mathf.Round(c.b * 255f)).ToString("X2");
        }

        internal static bool TryHex(string s, out Color c)
        {
            c = Color.white;
            if (s == null) return false;
            s = s.Trim().TrimStart('#');
            if (s.Length == 3) s = "" + s[0] + s[0] + s[1] + s[1] + s[2] + s[2];
            int n;
            if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n)) return false;
            c = new Color(((n >> 16) & 255) / 255f, ((n >> 8) & 255) / 255f, (n & 255) / 255f, 1f);
            return true;
        }

        /// <summary>Flat swatch (colour + thin outline, accent outline when selected).</summary>
        internal static void Swatch(Rect r, Color c, bool selected)
        {
            c.a = 1f;
            BtcReplayUI.Fill(r, selected ? BtcMenu.Accent : new Color(1f, 1f, 1f, 0.28f));
            BtcReplayUI.Fill(new Rect(r.x + 1.5f, r.y + 1.5f, r.width - 3f, r.height - 3f), c);
        }

        private static void Remember(Color c)
        {
            c.a = 1f;
            for (int i = 0; i < Recent.Count; i++)
                if (Mathf.Abs(Recent[i].r - c.r) < 0.004f && Mathf.Abs(Recent[i].g - c.g) < 0.004f && Mathf.Abs(Recent[i].b - c.b) < 0.004f) { Recent.RemoveAt(i); break; }
            Recent.Insert(0, c);
            if (Recent.Count > 10) Recent.RemoveAt(Recent.Count - 1);
        }

        private static void Textures()
        {
            if (_hueTex == null)
            {
                _hueTex = new Texture2D(1, 96, TextureFormat.RGB24, false);
                _hueTex.wrapMode = TextureWrapMode.Clamp; _hueTex.filterMode = FilterMode.Bilinear;
                for (int i = 0; i < 96; i++) _hueTex.SetPixel(0, i, Color.HSVToRGB(i / 95f, 1f, 1f));
                _hueTex.Apply();
            }
            if (_svTex == null)
            {
                _svTex = new Texture2D(24, 24, TextureFormat.RGB24, false);
                _svTex.wrapMode = TextureWrapMode.Clamp; _svTex.filterMode = FilterMode.Bilinear;
                _svHue = -1f;
            }
            if (Mathf.Abs(_svHue - _h) > 0.002f)
            {
                _svHue = _h;
                for (int x = 0; x < 24; x++) for (int y = 0; y < 24; y++) _svTex.SetPixel(x, y, Color.HSVToRGB(_h, x / 23f, y / 23f));
                _svTex.Apply();
            }
        }

        /// <summary>
        /// One colour row: icon-free swatch + hex, whole row opens the panel. indent = leading depth in font units, expertOnly rows are filtered by the caller.
        /// Returns true while the panel is open (the caller may want to know).
        /// </summary>
        internal static bool Row(ref float y, float w, int fs, string key, string label, int indent, Color c, Action<Color> set, string tip)
        {
            Event e = Event.current;
            float pad = fs, h = fs * 2.1f;
            Rect row = new Rect(pad + indent * fs * 1.2f, y, w - pad * 2f - indent * fs * 1.2f, h);
            bool hover = row.Contains(e.mousePosition);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, hover ? 0.07f : 0.03f));
            Rect sw = new Rect(row.x + fs * 0.6f, row.y + h * 0.2f, fs * 2.5f, h * 0.6f);
            Swatch(sw, c, _open == key);
            GUIStyle st = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            Color oc = GUI.contentColor;
            GUI.contentColor = Color.white;
            int ofs = st.fontSize;
            st.fontSize = Mathf.RoundToInt(fs * 0.95f);
            TextAnchor oa = st.alignment; st.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(sw.xMax + fs * 0.8f, row.y, row.width - fs * 11f, h), label, st);
            GUI.contentColor = new Color(1f, 1f, 1f, 0.5f);
            st.fontSize = Mathf.RoundToInt(fs * 0.8f); st.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(row.xMax - fs * 7f, row.y, fs * 6.4f, h), "#" + Hex(c), st);
            st.fontSize = ofs; st.alignment = oa; GUI.contentColor = oc;
            y += h + 3f;
            if (!string.IsNullOrEmpty(tip)) BtcReplayIcons.Hint(row, tip);
            if (BtcSfx.Btn(row, "", BtcReplayUI.sBtn))
            {
                if (_open == key) Close();
                else
                {
                    _open = key; _drag = null;
                    Color.RGBToHSV(c, out _h, out _s, out _v);
                    _hexFor = null;
                    BtcSfx.Open();
                }
            }
            if (_open != key) return false;
            Panel(ref y, w - indent * fs * 1.2f, fs, key, label, c, set, row.x - pad);
            return true;
        }

        /// <summary>The picker panel itself (inline under a row, or under a palette stop).</summary>
        internal static void Panel(ref float y, float w, int fs, string key, string label, Color c, Action<Color> set, float xShift)
        {
            Event e = Event.current;
            _open = key;
            Textures();
            float pad = fs, sq = fs * 8f, gap = fs * 0.8f;
            float x0 = pad + xShift + fs * 0.6f;
            float ph = sq + fs * 5.2f;
            Rect panel = new Rect(pad + xShift, y, w - pad * 2f, ph);
            BtcReplayUI.Fill(panel, new Color(0f, 0f, 0f, 0.28f));
            BtcReplayUI.Fill(new Rect(panel.x, panel.y, 2f, panel.height), BtcMenu.Accent);
            Rect svr = new Rect(x0 + fs * 0.4f, y + fs * 0.7f, sq, sq);
            Rect hr = new Rect(svr.xMax + gap, svr.y, fs * 1.3f, sq);
            bool changed = false;
            if (e.rawType == EventType.MouseUp) { if (_drag != null) Remember(Color.HSVToRGB(_h, _s, _v)); _drag = null; }
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (svr.Contains(e.mousePosition)) _drag = "sv";
                else if (hr.Contains(e.mousePosition)) _drag = "hue";
            }
            if (_drag != null && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                if (_drag == "sv") { _s = Mathf.Clamp01((e.mousePosition.x - svr.x) / svr.width); _v = 1f - Mathf.Clamp01((e.mousePosition.y - svr.y) / svr.height); }
                else _h = Mathf.Clamp01((e.mousePosition.y - hr.y) / hr.height);
                changed = true;
                e.Use();
            }
            if (e.type == EventType.Repaint)
            {
                Color oc = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(svr, _svTex);
                GUI.DrawTexture(hr, _hueTex);
                float cx = svr.x + _s * svr.width, cy = svr.y + (1f - _v) * svr.height;
                BtcReplayUI.Fill(new Rect(cx - 6f, cy - 6f, 12f, 12f), Color.black);
                BtcReplayUI.Fill(new Rect(cx - 5f, cy - 5f, 10f, 10f), Color.white);
                Color cc = Color.HSVToRGB(_h, _s, _v);
                BtcReplayUI.Fill(new Rect(cx - 3f, cy - 3f, 6f, 6f), cc);
                float hy = hr.y + _h * hr.height;
                BtcReplayUI.Fill(new Rect(hr.x - 3f, hy - 2.5f, hr.width + 6f, 5f), Color.black);
                BtcReplayUI.Fill(new Rect(hr.x - 3f, hy - 1.5f, hr.width + 6f, 3f), Color.white);
                GUI.color = oc;
            }
            // right column: preview, hex field, copy / paste
            float rx = hr.xMax + gap * 1.6f, rw = Mathf.Min(fs * 9f, panel.xMax - rx - fs * 0.6f);
            Color cur = Color.HSVToRGB(_h, _s, _v);
            Rect pv = new Rect(rx, svr.y, rw, fs * 2.2f);
            BtcReplayUI.Fill(pv, new Color(c.r, c.g, c.b, 1f));
            Rect hx = new Rect(rx, pv.yMax + fs * 0.5f, rw - fs * 4.2f, fs * 1.9f);
            BtcReplayUI.Fill(hx, new Color(1f, 1f, 1f, 0.06f));
            if (_field == null) { _field = new GUIStyle(GUI.skin.textField); }
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _field.font = b.font; _field.fontSize = Mathf.RoundToInt(fs * 0.9f); _field.alignment = TextAnchor.MiddleLeft;
            _field.normal.background = null; _field.focused.background = null; _field.hover.background = null; _field.active.background = null;
            _field.normal.textColor = Color.white; _field.focused.textColor = Color.white; _field.hover.textColor = Color.white; _field.padding = new RectOffset(6, 4, 0, 0);
            if (_hexFor != key || GUIUtility.keyboardControl == 0) { _hex = "#" + Hex(c); _hexFor = key; }
            BtcUi.Field(new Rect(GUIUtility.GUIToScreenPoint(hx.position), hx.size));
            string nh = GUI.TextField(hx, _hex, 7, _field);
            if (nh != _hex)
            {
                _hex = nh;
                Color pc;
                if (TryHex(nh, out pc)) { Color.RGBToHSV(pc, out _h, out _s, out _v); changed = true; }
            }
            float ib = fs * 1.9f;
            Rect cp = new Rect(hx.xMax + 3f, hx.y, ib, ib), ps = new Rect(cp.xMax + 3f, hx.y, ib, ib);
            if (BtcReplayIcons.Plain(cp, BtcReplayIcons.Copy, Color.white, BtcReplayUI.T("Copy the colour code", "Копировать код цвета")))
            {
                try { GUIUtility.systemCopyBuffer = "#" + Hex(c); } catch (Exception) { }
                BtcMenu.Toast(BtcReplayUI.T("Copied #", "Скопировано #") + Hex(c), BtcReplayIcons.Copy);
            }
            if (BtcReplayIcons.Plain(ps, BtcReplayIcons.Import, Color.white, BtcReplayUI.T("Paste a colour code", "Вставить код цвета")))
            {
                Color pc;
                string clip = "";
                try { clip = GUIUtility.systemCopyBuffer; } catch (Exception) { }
                if (TryHex(clip, out pc)) { Color.RGBToHSV(pc, out _h, out _s, out _v); changed = true; BtcSfx.Ok(); }
                else { BtcSfx.Miss(); BtcMenu.Toast(BtcReplayUI.T("No colour code in the clipboard", "В буфере нет кода цвета"), -1); }
            }
            // recent + presets
            float sy = hx.yMax + fs * 0.6f, sz = fs * 1.5f;
            int per = Mathf.Max(4, Mathf.FloorToInt((panel.xMax - rx - fs * 0.6f) / (sz + 3f)));
            int n = 0;
            Color pick = Color.clear; bool picked = false;
            for (int i = 0; i < Recent.Count; i++, n++)
            {
                Rect r = new Rect(rx + (n % per) * (sz + 3f), sy + (n / per) * (sz + 3f), sz, sz);
                Swatch(r, Recent[i], false);
                BtcReplayIcons.Hint(r, BtcReplayUI.T("Recent: #", "Недавний: #") + Hex(Recent[i]));
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) { pick = Recent[i]; picked = true; }
            }
            if (Recent.Count > 0) n = ((n + per - 1) / per) * per;
            for (int i = 0; i < Presets.Length; i++, n++)
            {
                Rect r = new Rect(rx + (n % per) * (sz + 3f), sy + (n / per) * (sz + 3f), sz, sz);
                if (r.yMax > panel.yMax) break;
                Swatch(r, Presets[i], false);
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) { pick = Presets[i]; picked = true; }
            }
            if (picked) { Color.RGBToHSV(pick, out _h, out _s, out _v); changed = true; }
            if (changed)
            {
                cur = Color.HSVToRGB(_h, _s, _v);
                Color old = c;
                set(cur);
                if (picked) Remember(cur);
                string k = "col:" + key;
                BtcUndo.Push("visual", label, delegate { set(old); }, k);
            }
            y += ph + fs * 0.3f;
        }
    }
}
