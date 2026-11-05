using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay player UI layout: every module (control panel, player box) has a scale, an offset and a visibility flag; the marks, cursors, nick
    /// text and view cones have their own size sliders. The layout is applied with GUI.matrix around the module (no per-widget rewrite), edited
    /// in the replay with F7 (drag the yellow handles, sliders in the window), kept in the config and in named templates (ReplayUI.txt).
    /// </summary>
    internal static class BtcReplayLayout
    {
        internal const int Bar = 0, Pov = 1, N = 2;
        private static readonly string[] Names = { "Control panel", "Player box" };
        private static readonly string[] NamesRu = { "Панель управления", "Блок игрока" };
        private static readonly float[] Scale = { 1f, 1f }, Ox = { 0f, 0f }, Oy = { 0f, 0f };
        private static readonly bool[] Vis = { true, true };
        private static readonly Rect[] Tr = new Rect[N];
        private static readonly bool[] TrOk = new bool[N];

        internal static float MarkScale = 1f, CursorScale = 1f, LabelScale = 1f, ConeAlpha = 1f;
        internal static bool Edit;

        private static bool _loaded, _dirty;
        private static Matrix4x4 _saved;
        private static int _cur = -1, _drag = -1;
        private static Vector2 _win = new Vector2(-1f, -1f);
        private static string _newName = "";
        private static List<string[]> _presets;

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        // ------------------------------------------------------------------ state
        private static void Ensure()
        {
            if (_loaded) return;
            _loaded = true;
            try { Parse(BtcConfig.ReplayUiLayout != null ? BtcConfig.ReplayUiLayout.Value : ""); }
            catch (Exception e) { BtcLog.Error("ReplayUI", "layout parse: " + e.Message); }
        }

        internal static bool Visible(int id) { Ensure(); return Vis[id]; }

        internal static void Reset()
        {
            for (int i = 0; i < N; i++) { Scale[i] = 1f; Ox[i] = 0f; Oy[i] = 0f; Vis[i] = true; }
            MarkScale = CursorScale = LabelScale = ConeAlpha = 1f;
            _dirty = true;
        }

        private static void Compact()
        {
            for (int i = 0; i < N; i++) Scale[i] = 0.7f;
            MarkScale = 0.7f; CursorScale = 0.8f; LabelScale = 0.8f;
            _dirty = true;
        }

        private static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

        internal static string Serialize()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < N; i++)
                sb.Append("m").Append(i).Append('=').Append(F(Scale[i])).Append(',').Append(F(Ox[i])).Append(',').Append(F(Oy[i])).Append(',').Append(Vis[i] ? 1 : 0).Append(';');
            sb.Append("mk=").Append(F(MarkScale)).Append(";cs=").Append(F(CursorScale)).Append(";ls=").Append(F(LabelScale)).Append(";ca=").Append(F(ConeAlpha));
            return sb.ToString();
        }

        private static float P(string s, float d)
        {
            float v;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : d;
        }

        internal static void Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            string[] parts = text.Split(';');
            for (int k = 0; k < parts.Length; k++)
            {
                int eq = parts[k].IndexOf('=');
                if (eq < 1) continue;
                string key = parts[k].Substring(0, eq), val = parts[k].Substring(eq + 1);
                if (key.Length == 2 && key[0] == 'm' && key[1] >= '0' && key[1] < '0' + N)
                {
                    int id = key[1] - '0';
                    string[] f = val.Split(',');
                    if (f.Length < 4) continue;
                    Scale[id] = Mathf.Clamp(P(f[0], 1f), 0.3f, 3f);
                    Ox[id] = Mathf.Clamp(P(f[1], 0f), -1.5f, 1.5f);
                    Oy[id] = Mathf.Clamp(P(f[2], 0f), -1.5f, 1.5f);
                    Vis[id] = f[3] != "0";
                }
                else if (key == "mk") MarkScale = Mathf.Clamp(P(val, 1f), 0.4f, 3f);
                else if (key == "cs") CursorScale = Mathf.Clamp(P(val, 1f), 0.4f, 3f);
                else if (key == "ls") LabelScale = Mathf.Clamp(P(val, 1f), 0.4f, 3f);
                else if (key == "ca") ConeAlpha = Mathf.Clamp(P(val, 1f), 0f, 2f);
            }
        }

        private static void Save()
        {
            _dirty = false;
            if (BtcConfig.ReplayUiLayout != null) BtcConfig.ReplayUiLayout.Value = Serialize();
        }

        // ------------------------------------------------------------------ transform
        /// <summary>Everything drawn until End() is scaled and moved as module id; b = the module's untransformed rect.</summary>
        internal static void Begin(int id, Rect b)
        {
            Ensure();
            _cur = id;
            float s = Scale[id];
            Vector2 pv = id == Bar ? new Vector2(b.center.x, b.yMax) : b.center;
            Vector2 off = new Vector2(Ox[id] * Screen.width, Oy[id] * Screen.height);
            _saved = GUI.matrix;
            Matrix4x4 m = Matrix4x4.TRS(new Vector3(pv.x + off.x, pv.y + off.y, 0f), Quaternion.identity, new Vector3(s, s, 1f))
                          * Matrix4x4.TRS(new Vector3(-pv.x, -pv.y, 0f), Quaternion.identity, Vector3.one);
            GUI.matrix = _saved * m;
            Tr[id] = new Rect(pv.x + off.x + (b.x - pv.x) * s, pv.y + off.y + (b.y - pv.y) * s, b.width * s, b.height * s);
            TrOk[id] = true;
        }

        internal static void End()
        {
            if (_cur < 0) return;
            GUI.matrix = _saved;
            _cur = -1;
        }

        /// <summary>The module's rect on screen (last drawn); false before the first draw.</summary>
        internal static bool Placed(int id, out Rect r) { r = Tr[id]; return TrOk[id]; }

        // ------------------------------------------------------------------ edit mode (F7)
        internal static void ToggleEdit()
        {
            Ensure();
            Edit = !Edit;
            if (!Edit && _dirty) Save();
        }

        /// <summary>Drag handles of the modules (call BEFORE drawing them so the handles get the mouse first).</summary>
        internal static void DrawHandles(int fs)
        {
            if (!Edit) return;
            Event e = Event.current;
            float hh = fs * 1.3f;
            for (int id = 0; id < N; id++)
            {
                if (!Vis[id] || !TrOk[id]) continue;
                Rect r = Tr[id];
                Color yc = new Color(1f, 0.85f, 0.25f, 0.9f);
                BtcReplayUI.Fill(new Rect(r.x, r.y, r.width, 2f), yc);
                BtcReplayUI.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), yc);
                BtcReplayUI.Fill(new Rect(r.x, r.y, 2f, r.height), yc);
                BtcReplayUI.Fill(new Rect(r.xMax - 2f, r.y, 2f, r.height), yc);
                Rect h = new Rect(r.x, Mathf.Max(0f, r.y - hh), Mathf.Max(fs * 8f, 0f), hh);
                BtcReplayUI.Fill(h, new Color(1f, 0.85f, 0.25f, 0.92f));
                Color old = GUI.contentColor;
                GUI.contentColor = Color.black;
                GUI.Label(new Rect(h.x + 4f, h.y, h.width - 4f, h.height), "+ " + Tt(Names[id], NamesRu[id]), BtcReplayUI.sLabel);
                GUI.contentColor = old;
                if (e.type == EventType.MouseDown && e.button == 0 && h.Contains(e.mousePosition)) { _drag = id; e.Use(); }
            }
            if (_drag >= 0)
            {
                if (e.type == EventType.MouseDrag) { Ox[_drag] += e.delta.x / Screen.width; Oy[_drag] += e.delta.y / Screen.height; _dirty = true; e.Use(); }
                else if (e.type == EventType.MouseUp) { _drag = -1; Save(); e.Use(); }
            }
        }

        private static float Slider(Rect win, ref float y, string label, float v, float min, float max, int fs, float rowH)
        {
            GUI.Label(new Rect(win.x + fs * 0.7f, y, fs * 9.5f, rowH), label, BtcReplayUI.sLabel);
            Rect sr = new Rect(win.x + fs * 10.5f, y + rowH * 0.3f, win.width - fs * 10.5f - fs * 4.6f, rowH * 0.4f);
            float nv = GUI.HorizontalSlider(sr, v, min, max);
            GUI.Label(new Rect(sr.xMax + fs * 0.5f, y, fs * 3.5f, rowH), nv.ToString("0.00", CultureInfo.InvariantCulture), BtcReplayUI.sLabel);
            if (Mathf.Abs(nv - v) > 0.0005f) _dirty = true;
            y += rowH;
            return nv;
        }

        internal static void DrawWindow(int fs)
        {
            if (!Edit) return;
            Ensure();
            Event e = Event.current;
            float rowH = fs * 1.6f;
            float w = fs * 27f;
            if (_presets == null) LoadPresets();
            float h = rowH * (4f + N + 5f + 2f + 1f + Mathf.Min(_presets.Count, 6)) + fs;
            if (_win.x < 0f) _win = new Vector2(Screen.width - w - fs, fs * 2f);
            _win.x = Mathf.Clamp(_win.x, 0f, Mathf.Max(0f, Screen.width - w));
            _win.y = Mathf.Clamp(_win.y, 0f, Mathf.Max(0f, Screen.height - h));
            Rect win = new Rect(_win.x, _win.y, w, h);
            BtcReplayUI.Fill(win, new Color(0.04f, 0.04f, 0.06f, 0.94f));
            Rect title = new Rect(win.x, win.y, w, rowH);
            BtcReplayUI.Fill(title, new Color(1f, 0.85f, 0.25f, 0.9f));
            Color oc = GUI.contentColor;
            GUI.contentColor = Color.black;
            GUI.Label(new Rect(title.x + fs * 0.7f, title.y, w - fs * 6f, rowH), Tt("Replay UI layout (F7)  - drag the yellow handles", "Раскладка UI реплея (F7) - тяни жёлтые метки"), BtcReplayUI.sLabel);
            GUI.contentColor = oc;
            if (e.type == EventType.MouseDown && e.button == 0 && title.Contains(e.mousePosition) && _drag < 0) { _drag = 100; e.Use(); }
            if (_drag == 100)
            {
                if (e.type == EventType.MouseDrag) { _win += e.delta; e.Use(); }
                else if (e.type == EventType.MouseUp) { _drag = -1; e.Use(); }
            }
            if (BtcReplayUI.Button(new Rect(title.xMax - fs * 5f, title.y + 2f, fs * 4.8f, rowH - 4f), Tt("Close", "Закрыть"), Color.white)) ToggleEdit();

            float y = title.yMax + 4f;
            for (int id = 0; id < N; id++)
            {
                bool vis = GUI.Toggle(new Rect(win.x + fs * 0.7f, y + rowH * 0.15f, fs * 1.2f, rowH * 0.7f), Vis[id], "");
                if (vis != Vis[id]) { Vis[id] = vis; _dirty = true; }
                GUI.Label(new Rect(win.x + fs * 2.2f, y, fs * 8.2f, rowH), Tt(Names[id], NamesRu[id]), BtcReplayUI.sLabel);
                Rect sr = new Rect(win.x + fs * 10.5f, y + rowH * 0.3f, w - fs * 10.5f - fs * 8.4f, rowH * 0.4f);
                float nv = GUI.HorizontalSlider(sr, Scale[id], 0.4f, 2f);
                if (Mathf.Abs(nv - Scale[id]) > 0.0005f) { Scale[id] = nv; _dirty = true; }
                GUI.Label(new Rect(sr.xMax + fs * 0.4f, y, fs * 3f, rowH), Scale[id].ToString("0.00", CultureInfo.InvariantCulture), BtcReplayUI.sLabel);
                if (BtcReplayUI.Button(new Rect(win.xMax - fs * 4.2f, y + 2f, fs * 3.8f, rowH - 4f), Tt("Pos", "Поз."), Color.white)) { Ox[id] = 0f; Oy[id] = 0f; _dirty = true; }
                y += rowH;
            }
            MarkScale = Slider(win, ref y, Tt("Timeline icons", "Иконки меток"), MarkScale, 0.4f, 2.5f, fs, rowH);
            CursorScale = Slider(win, ref y, Tt("Cursors", "Курсоры"), CursorScale, 0.4f, 3f, fs, rowH);
            LabelScale = Slider(win, ref y, Tt("Nick text", "Текст ников"), LabelScale, 0.4f, 3f, fs, rowH);
            ConeAlpha = Slider(win, ref y, Tt("View cone", "Конус обзора"), ConeAlpha, 0f, 2f, fs, rowH);
            if (BtcReplayUI.Button(new Rect(win.x + fs * 0.7f, y + 2f, w - fs * 1.4f, rowH - 4f), Tt("Unit owner dots: ", "Точки владельца: ") + (BtcUnitDots.Mode == 0 ? Tt("off", "выкл") : (BtcUnitDots.Mode == 1 ? Tt("while Alt", "с Alt") : Tt("always", "всегда"))), Color.white)) BtcUnitDots.Cycle();
            y += rowH;
            float bw = (w - fs * 2.1f) / 2f;
            if (BtcReplayUI.Button(new Rect(win.x + fs * 0.7f, y + 2f, bw, rowH - 4f), Tt("Compact", "Компактно"), Color.white)) { Compact(); }
            if (BtcReplayUI.Button(new Rect(win.x + fs * 1.4f + bw, y + 2f, bw, rowH - 4f), Tt("Reset all", "Сбросить всё"), BtcReplayUI.Red)) { Reset(); }
            y += rowH;

            GUI.Label(new Rect(win.x + fs * 0.7f, y, w, rowH), Tt("Templates", "Шаблоны"), BtcReplayUI.sHead);
            y += rowH;
            _newName = GUI.TextField(new Rect(win.x + fs * 0.7f, y + 3f, w - fs * 7.4f, rowH - 6f), _newName ?? "", 24);
            if (BtcReplayUI.Button(new Rect(win.xMax - fs * 6.4f, y + 2f, fs * 5.8f, rowH - 4f), Tt("Save as", "Сохранить"), BtcReplayUI.Green)) SavePreset(_newName);
            y += rowH;
            int shown = 0;
            for (int i = 0; i < _presets.Count && shown < 6; i++, shown++)
            {
                GUI.Label(new Rect(win.x + fs * 0.7f, y, w - fs * 12f, rowH), _presets[i][0], BtcReplayUI.sLabel);
                if (BtcReplayUI.Button(new Rect(win.xMax - fs * 11f, y + 2f, fs * 5.6f, rowH - 4f), Tt("Load", "Загрузить"), Color.white)) { Reset(); Parse(_presets[i][1]); _dirty = true; }
                if (BtcReplayUI.Button(new Rect(win.xMax - fs * 5f, y + 2f, fs * 4.4f, rowH - 4f), Tt("Del", "Удал."), BtcReplayUI.Red)) { _presets.RemoveAt(i); WritePresets(); break; }
                y += rowH;
            }
            if (e.type == EventType.MouseUp && _dirty) Save();
            // swallow clicks on the empty window area so they do not reach the panel below
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseUp) && win.Contains(e.mousePosition)) e.Use();
        }

        // ------------------------------------------------------------------ templates
        private static string PresetFile { get { return Path.Combine(BtcPaths.Root, "ReplayUI.txt"); } }

        private static void LoadPresets()
        {
            _presets = new List<string[]>();
            try
            {
                if (!File.Exists(PresetFile)) return;
                string[] lines = File.ReadAllLines(PresetFile);
                for (int i = 0; i < lines.Length; i++)
                {
                    int t = lines[i].IndexOf('\t');
                    if (t > 0) _presets.Add(new[] { lines[i].Substring(0, t), lines[i].Substring(t + 1) });
                }
            }
            catch (Exception ex) { BtcLog.Error("ReplayUI", "presets load: " + ex.Message); }
        }

        private static void WritePresets()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < _presets.Count; i++) sb.Append(_presets[i][0]).Append('\t').Append(_presets[i][1]).Append('\n');
                Directory.CreateDirectory(BtcPaths.Root);
                File.WriteAllText(PresetFile, sb.ToString());
            }
            catch (Exception ex) { BtcLog.Error("ReplayUI", "presets save: " + ex.Message); }
        }

        private static void SavePreset(string name)
        {
            name = (name ?? "").Replace('\t', ' ').Replace('\n', ' ').Trim();
            if (name.Length == 0) return;
            if (_dirty) Save();
            string data = Serialize();
            for (int i = 0; i < _presets.Count; i++) if (_presets[i][0] == name) { _presets[i][1] = data; WritePresets(); return; }
            _presets.Add(new[] { name, data });
            WritePresets();
        }
    }
}
