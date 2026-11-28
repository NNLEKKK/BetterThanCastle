using System;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// FPS counter (Visuals > Screen > FPS counter, BtcMenuState "fps:*", off by default; placed by the UI editor element "fps").
    /// Shows frames per second over the last half second and, optionally, the slowest frame of the last second in ms: a micro freeze
    /// barely moves the average but shows up there at once. Own component (alive in every scene), the text is rebuilt only when it changes.
    /// </summary>
    internal sealed class BtcFps : MonoBehaviour
    {
        private static BtcFps _inst;
        private static bool _on, _max = true, _graph = true, _read;
        private static float _len = 12f;

        internal static bool On { get { Read(); return _on; } set { _on = value; BtcMenuState.SetBool("fps:on", value); } }
        internal static bool ShowMax { get { Read(); return _max; } set { _max = value; BtcMenuState.SetBool("fps:max", value); _shownFps = -1; } }
        internal static bool Graph { get { Read(); return _graph; } set { _graph = value; BtcMenuState.SetBool("fps:graph", value); } }
        /// <summary>Seconds of history the graph shows (one column = Len / Cols seconds, its slowest frame).</summary>
        internal static float Len { get { Read(); return _len; } set { _len = Mathf.Clamp(value, 5f, 120f); BtcMenuState.SetInt("fps:len", Mathf.RoundToInt(_len)); } }

        private static void Read()
        {
            if (_read) return;
            _read = true;
            _on = BtcMenuState.Bool("fps:on", false);
            _max = BtcMenuState.Bool("fps:max", true);
            _graph = BtcMenuState.Bool("fps:graph", true);
            _len = Mathf.Clamp(BtcMenuState.Int("fps:len", 12), 5, 120);
        }

        // ------------------------------------------------------------------ frame time graph (ring texture, one column per time bucket)
        private const int Cols = 240, H = 64;
        private Texture2D _tex;
        private readonly Color32[] _colBuf = new Color32[H];
        private int _x;
        private float _bucketStart, _bucketMax;
        private int _gcSeen = -1;
        private bool _bucketGc;
        private static GUIStyle _small;
        private static int _lenShown = -1;
        private static string _lenText = "";

        // 0..33 ms linear on the lower half, 33..500 ms logarithmic on the upper half: drops and freezes both readable
        private static int Px(float ms)
        {
            if (ms <= 33.3f) return Mathf.RoundToInt(ms / 33.3f * (H / 2));
            float t = Mathf.Log(ms / 33.3f) / Mathf.Log(500f / 33.3f);
            return Mathf.Min(H - 1, H / 2 + Mathf.RoundToInt(Mathf.Clamp01(t) * (H / 2 - 1)));
        }

        private void EnsureTex()
        {
            if (_tex != null) return;
            _tex = new Texture2D(Cols, H, TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Point;
            _tex.wrapMode = TextureWrapMode.Repeat;
            _tex.hideFlags = HideFlags.HideAndDontSave;
            Color32[] all = new Color32[Cols * H];
            for (int i = 0; i < all.Length; i++) all[i] = Back(i / Cols);
            _tex.SetPixels32(all);
            _tex.Apply(false);
        }

        private static readonly int L16 = Px(16.7f), L33 = Px(33.3f), L100 = Px(100f);
        private static Color32 Back(int y)
        {
            if (y == L16 || y == L33 || y == L100) return new Color32(255, 255, 255, 60);
            return new Color32(0, 0, 0, 150);
        }

        private void PushColumn(float ms, bool gc)
        {
            EnsureTex();
            int top = Px(ms);
            Color32 c = gc ? new Color32(190, 120, 255, 255) : ms < 20f ? new Color32(110, 220, 110, 255) : ms < 40f ? new Color32(240, 210, 90, 255) : ms < 100f ? new Color32(250, 140, 60, 255) : new Color32(255, 70, 60, 255);
            for (int y = 0; y < H; y++) _colBuf[y] = y <= top ? c : Back(y);
            _tex.SetPixels32(_x, 0, 1, H, _colBuf);
            _tex.Apply(false);
            _x = (_x + 1) % Cols;
        }

        private void TickGraph(float now, float dt)
        {
            if (!_graph) { _bucketMax = 0f; _bucketStart = now; return; }
            if (dt > _bucketMax) _bucketMax = dt;
            // a garbage collection since the last column: the column is purple (tells GC pauses from other spikes)
            int gcN = GC.CollectionCount(0);
            if (gcN != _gcSeen) { _gcSeen = gcN; _bucketGc = true; }
            float step = _len / Cols;
            if (now - _bucketStart < step) return;
            // a long freeze spans several buckets: fill them all with it so its width on the timeline is true
            int n = Mathf.Clamp(Mathf.FloorToInt((now - _bucketStart) / step), 1, Cols);
            for (int i = 0; i < n; i++) PushColumn(_bucketMax * 1000f, _bucketGc);
            _bucketMax = 0f;
            _bucketGc = false;
            _bucketStart = now;
        }

        private void DrawGraph()
        {
            BtcHud.Slot sl = BtcHud.SlotOf("fpsgraph");
            if (!sl.Enabled || _tex == null) return;
            float w = 240f * sl.Scale, h = 64f * sl.Scale;
            Rect r = BtcHud.Place("fpsgraph", new Rect(Screen.width - w - 6f, 4f + 26f, w, h));
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, sl.Alpha);
            // oldest column at the left, newest at the right edge (texture wraps)
            GUI.DrawTextureWithTexCoords(r, _tex, new Rect(_x / (float)Cols, 0f, 1f, 1f));
            if (_small == null)
            {
                _small = new GUIStyle(GUI.skin.label);
                _small.alignment = TextAnchor.MiddleLeft;
                _small.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
                _small.padding = new RectOffset(2, 0, 0, 0);
            }
            _small.fontSize = Mathf.Max(7, Mathf.RoundToInt(10 * sl.Scale));
            float lh = 12f * sl.Scale;
            Label(r, L16, lh, "16");
            Label(r, L33, lh, "33");
            Label(r, L100, lh, "100 ms");
            int len = Mathf.RoundToInt(_len);
            if (len != _lenShown) { _lenShown = len; _lenText = "-" + len + " s"; }
            GUI.Label(new Rect(r.x, r.yMax - lh, 60f * sl.Scale, lh), _lenText, _small);
            GUI.color = old;
        }

        private static void Label(Rect r, int py, float lh, string s)
        {
            float y = r.yMax - (py + 0.5f) / H * r.height;
            GUI.Label(new Rect(r.xMax - 46f * (lh / 12f), y - lh * 0.5f, 46f * (lh / 12f), lh), s, _small);
        }

        private void OnDestroy() { if (_tex != null) Destroy(_tex); }

        /// <summary>ResourcePackManager.Update: creates the component once.</summary>
        internal static void Ensure()
        {
            if (_inst != null) return;
            try
            {
                GameObject go = new GameObject("BtcFps");
                DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                _inst = go.AddComponent<BtcFps>();
            }
            catch (Exception e) { BtcLog.Error("Fps", "create: " + e.Message); }
        }

        // half-second window for the average, one-second window for the slowest frame
        private float _winStart, _maxStart, _maxCur, _maxShown;
        private int _frames, _fps;
        private static int _shownFps = -1, _shownMs = -1;
        private static string _text = "";
        private GUIStyle _style;
        private Color _col = Color.white;

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (!On) { _frames = 0; _bucketStart = now; _bucketMax = 0f; return; }
            float dt = Time.unscaledDeltaTime;
            TickGraph(now, dt);
            _frames++;
            if (dt > _maxCur) _maxCur = dt;
            if (now - _winStart >= 0.5f)
            {
                _fps = Mathf.RoundToInt(_frames / Mathf.Max(0.001f, now - _winStart));
                _frames = 0;
                _winStart = now;
            }
            if (now - _maxStart >= 1f)
            {
                _maxShown = _maxCur;
                _maxCur = 0f;
                _maxStart = now;
            }
            int ms = Mathf.RoundToInt(_maxShown * 1000f);
            if (_fps != _shownFps || (ShowMax && ms != _shownMs))
            {
                _shownFps = _fps; _shownMs = ms;
                _text = ShowMax ? _fps + " FPS  " + ms + " ms" : _fps + " FPS";
                _col = _fps >= 50 && (!ShowMax || ms < 50) ? new Color(0.6f, 1f, 0.6f) : _fps >= 30 && (!ShowMax || ms < 100) ? new Color(1f, 0.9f, 0.45f) : new Color(1f, 0.5f, 0.45f);
            }
        }

        private void OnGUI()
        {
            if (useGUILayout) useGUILayout = false;   // no GUILayout pass (garbage)
            if (!_on || Event.current.type != EventType.Repaint) return;
            GUI.depth = -1150;
            if (_graph) DrawGraph();
            BtcHud.Slot sl = BtcHud.SlotOf("fps");
            if (!sl.Enabled) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label);
                _style.fontStyle = FontStyle.Bold;
                _style.alignment = TextAnchor.MiddleCenter;
                _style.normal.textColor = Color.white;
            }
            _style.fontSize = Mathf.Max(8, Mathf.RoundToInt(15 * sl.Scale));
            float w = (_max ? 130f : 70f) * sl.Scale, h = 22f * sl.Scale;
            Rect r = BtcHud.Place("fps", new Rect(Screen.width - w - 6f, 4f, w, h));
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * sl.Alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(_col.r, _col.g, _col.b, sl.Alpha);
            GUI.Label(r, _text, _style);
            GUI.color = old;
        }
    }
}
