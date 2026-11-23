using System;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Small map-editor panel for live symmetry (on/off, type, axis shift) plus the axis drawn as a grid wall in the world.
    /// Shown only in the world editor. Settings are the same as Gameplay settings (BtcSymmetry), saved via Commit().
    /// </summary>
    internal class BtcLiveSymUI : MonoBehaviour
    {
        internal static bool Over;          // pointer over the panel: no mirroring of clicks that land on it
        private static BtcLiveSymUI _inst;
        private static Material _mat;
        private bool _open = true;
        private bool _axis = true;
        private bool _loaded;
        private const float W = 214f;

        internal static void Init()
        {
            if (_inst != null) return;
            GameObject go = new GameObject("BtcLiveSymUI");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<BtcLiveSymUI>();
            Camera.onPostRender += OnPost;
        }

        private static bool InEditor()
        {
            try { return Brix.Input.InputModeController.DefaultMode == Brix.Input.InputMode.worldEditor; }
            catch (Exception) { return false; }
        }

        private static string T(string en, string ru) { return BtcGameplay.T(en, ru); }

        private void Update()
        {
            if (InEditor()) { BtcLiveSym.EnsureImplPatched(); BtcLiveSym.Tick(); BtcCarve.Tick(); BtcEdUndo.Tick(); BtcShieldBox.Tick(); }
            else { BtcCarve.On = false; BtcShieldBox.On = false; }
        }

        private void OnGUI()
        {
        	if (useGUILayout) useGUILayout = false; // no GUILayout here: skip Unity's per-event layout pass (garbage)
            Over = false;
            if (!InEditor() || BtcConfig.LiveSymmetry == null) return;
            if (!_loaded) { BtcSymmetry.StateString(); _loaded = true; }   // loads the saved symmetry prefs once
            float x = Screen.width - W - 8f, y = 8f;
            float h = _open ? 196f + 126f + 96f : 24f;
            Rect box = new Rect(x, y, W, h);
            Over = box.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
            if (BtcShieldBox.Labels()) Over = true;
            if (BtcShieldBox.Panel(x, y + h + 6f, W)) Over = true;
            GUI.Box(box, GUIContent.none);
            bool on = BtcConfig.LiveSymmetry.Value;
            if (GUI.Button(new Rect(x + 2, y + 2, W - 4, 20), (_open ? "- " : "+ ") + T("Symmetry: ", "Симметрия: ") + (on && BtcSymmetry.Mode != 0 ? T("ON", "ВКЛ") : T("off", "выкл"))))
                _open = !_open;
            if (!_open) return;
            float cy = y + 28f;
            bool nv = GUI.Toggle(new Rect(x + 8, cy, W - 16, 20), on, " " + T("Live symmetry", "Живая симметрия"));
            if (nv != on) BtcConfig.LiveSymmetry.Value = nv;
            cy += 24f;
            string[] names =
            {
                T("None", "Нет"), T("Mirror X", "Зерк. X"), T("Mirror Z", "Зерк. Z"), T("Point", "Точка"), T("Quarters", "Четверти")
            };
            for (int i = 0; i < names.Length; i++)
            {
                Rect r = new Rect(x + 8 + (i % 3) * 66, cy + (i / 3) * 24, 64, 22);
                bool sel = BtcSymmetry.Mode == i;
                bool b = GUI.Toggle(r, sel, names[i], GUI.skin.button);
                if (b && !sel) { BtcSymmetry.Mode = i; BtcSymmetry.Commit(); }
            }
            cy += 52f;
            int m = BtcSymmetry.Mode;
            if (m == 1 || m == 3 || m == 4) BtcSymmetry.Dx = Shift(x, ref cy, "Axis X", "Ось X", BtcSymmetry.Dx);
            if (m == 2 || m == 3 || m == 4) BtcSymmetry.Dz = Shift(x, ref cy, "Axis Z", "Ось Z", BtcSymmetry.Dz);
            _axis = GUI.Toggle(new Rect(x + 8, y + 196f - 24, W - 16, 20), _axis, " " + T("Show axis in the world", "Показать ось в мире"));
            // Carve tool: break / add voxels of any size anywhere (caves, multi-level passages)
            float ky = y + 196f;
            GUI.Label(new Rect(x + 8, ky, W - 16, 18), T("Carve: LMB, Alt = drag, Ctrl+Z", "Воксели: ЛКМ, Alt = вести, Ctrl+Z"));
            ky += 20f;
            bool carve = GUI.Toggle(new Rect(x + 8, ky, W - 16, 20), BtcCarve.On, " " + T("Carve tool", "Инструмент вокселей"));
            if (carve != BtcCarve.On) { BtcCarve.On = carve; if (carve) BtcShieldBox.On = false; }
            ky += 24f;
            if (GUI.Toggle(new Rect(x + 8, ky, 100, 22), !BtcCarve.AddMode, T("Break", "Ломать"), GUI.skin.button)) BtcCarve.AddMode = false;
            if (GUI.Toggle(new Rect(x + 110, ky, 96, 22), BtcCarve.AddMode, T("Add", "Добавить"), GUI.skin.button)) BtcCarve.AddMode = true;
            ky += 26f;
            for (int i = 1; i <= 4; i++)
                if (GUI.Toggle(new Rect(x + 8 + (i - 1) * 50, ky, 48, 22), BtcCarve.Size == i, i + "x" + i, GUI.skin.button)) BtcCarve.Size = i;
            ky += 26f;
            string[] tn = { T("Stone", "Камень"), T("Earth", "Земля"), T("Grass", "Трава"), T("Sand", "Песок"), T("Snow", "Снег"), T("Paved", "Брусчатка") };
            for (int i = 0; i < tn.Length; i++)
                if (GUI.Toggle(new Rect(x + 8 + (i % 3) * 66, ky + (i / 3) * 22, 64, 20), BtcCarve.TypeIdx == i, tn[i], GUI.skin.button)) BtcCarve.TypeIdx = i;
            // Island extras: square brush and slope
            ky += 50f;
            GUI.Label(new Rect(x + 8, ky, W - 16, 18), T("Brushes (Island etc.)", "Кисти (Остров и др.)"));
            ky += 20f;
            BtcIsland.Square = GUI.Toggle(new Rect(x + 8, ky, W - 16, 20), BtcIsland.Square, " " + T("Square brush", "Квадратная кисть"));
            ky += 24f;
            GUI.Label(new Rect(x + 8, ky, 62, 20), T("Slope %", "Уклон %") + " " + BtcIsland.Slope);
            int[] ss = { -10, -1, 1, 10 };
            for (int i = 0; i < ss.Length; i++)
                if (GUI.Button(new Rect(x + 74 + i * 33, ky, 31, 20), (ss[i] > 0 ? "+" : "") + ss[i])) BtcIsland.Slope += ss[i];        }

        private static int Shift(float x, ref float cy, string en, string ru, int v)
        {
            GUI.Label(new Rect(x + 8, cy, 64, 20), T(en, ru));
            int[] steps = { -4, -1, 1, 4 };
            int old = v;
            for (int i = 0; i < steps.Length; i++)
                if (GUI.Button(new Rect(x + 70 + i * 34, cy, 32, 20), (steps[i] > 0 ? "+" : "") + steps[i]))
                    v += steps[i];
            cy += 24f;
            v = Mathf.Clamp(v, -400, 400);
            if (v != old) BtcSymmetry.Commit();
            return v;
        }

        // Axis wall(s) near the camera: translucent quad plus grid lines every 10 units.
        private static void OnPost(Camera cam)
        {
            try
            {
                if (_inst == null || cam != Camera.main || !InEditor()) return;
                bool axisOn = _inst._axis && _inst._open && BtcConfig.LiveSymmetry != null && BtcConfig.LiveSymmetry.Value;
                int m = 0, keep, sx = 0, sz = 0;
                if (axisOn && !BtcSymmetry.LiveAxes(out m, out keep, out sx, out sz)) axisOn = false;
                if (!axisOn && !BtcCarve.On && !BtcShieldBox.On) return;
                if (_mat == null)
                {
                    Shader s = Shader.Find("Hidden/Internal-Colored");
                    if (s == null) return;
                    _mat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
                    _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    _mat.SetInt("_Cull", 0);
                    _mat.SetInt("_ZWrite", 0);
                }
                _mat.SetPass(0);
                Vector3 c = cam.transform.position;
                if (axisOn && (m == 1 || m == 3 || m == 4)) Wall(sx * 0.5f, c.z, true);
                if (axisOn && (m == 2 || m == 3 || m == 4)) Wall(sz * 0.5f, c.x, false);
                BtcCarve.Draw();
                BtcShieldBox.Draw(cam);
            }
            catch (Exception) { }
        }

        private static void Wall(float axis, float center, bool alongZ)
        {
            const float half = 120f, y0 = 0f, y1 = 260f;   // full build height (columns are 0..255)
            float a = Mathf.Floor(center / 10f) * 10f - half, b = a + 2f * half;
            GL.Begin(GL.QUADS);
            GL.Color(new Color(0.2f, 0.9f, 0.4f, 0.12f));
            P(axis, y0, a, alongZ); P(axis, y0, b, alongZ); P(axis, y1, b, alongZ); P(axis, y1, a, alongZ);
            GL.End();
            GL.Begin(GL.LINES);
            GL.Color(new Color(0.3f, 1f, 0.5f, 0.55f));
            for (float t = a; t <= b; t += 10f) { P(axis, y0, t, alongZ); P(axis, y1, t, alongZ); }
            for (float yy = y0; yy <= y1; yy += 10f) { P(axis, yy, a, alongZ); P(axis, yy, b, alongZ); }
            GL.End();
        }

        private static void P(float axis, float y, float t, bool alongZ)
        {
            GL.Vertex3(alongZ ? axis : t, y, alongZ ? t : axis);
        }
    }
}
