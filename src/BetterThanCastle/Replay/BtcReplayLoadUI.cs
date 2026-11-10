using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// One loading screen for the replay in the style of the replay bar: a dark cover, a card with an overall bar and the steps (read the file,
    /// load the world, place the keyframes for instant seek), a step spinner, the elapsed time and an exit button (Esc). Also shown, lighter,
    /// when a forward seek takes longer than a moment. Replaces the old "REPLAY: status / Cancel" box and the plain preprocess plate.
    /// </summary>
    internal static class BtcReplayLoadUI
    {
        private static float _t0 = -1f, _seekSince = -1f, _seekFrom, _settleSince = -1f;
        private static bool _loadSeen;
        private static int _lastFrame = -1;
        private static EventType _lastType;
        private static GUIStyle _big;

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        /// <summary>Own OnGUI at depth -10000 so the cover is above every other mod overlay (banners, load timer, replay bar).</summary>
        private sealed class Host : MonoBehaviour
        {
            private void OnGUI()
            {
                GUI.depth = -10000;
                Draw();
            }
        }

        private static GameObject _host;

        internal static void EnsureHost()
        {
            if (_host != null) return;
            _host = new GameObject("BtcReplayLoadUI");
            _host.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _host.AddComponent<Host>();
        }

        /// <summary>Which screen to show: 0 none, 1 loading / keyframes (opaque), 2 long seek (light).</summary>
        internal static int Mode()
        {
            if (BtcReplay.Mode != BtcReplay.State.Playing) { _t0 = -1f; _seekSince = -1f; _loadSeen = false; _settleSince = -1f; return 0; }
            if (BtcReplaySeek.Preprocessing || BtcReplay.Loading) { _seekSince = -1f; _loadSeen = true; _settleSince = -1f; if (_t0 < 0f) _t0 = Time.unscaledTime; return 1; }
            // the world finished loading but the keyframe pass has not started yet: keep the cover so the world is not shown in between
            if (_loadSeen && BtcReplaySeek.Settling && !BtcReplay.Seeking)
            {
                if (_settleSince < 0f) _settleSince = Time.unscaledTime;
                if (Time.unscaledTime - _settleSince < 8f) return 1;
            }
            else _loadSeen = false;
            _t0 = -1f;
            if (BtcReplay.Seeking)
            {
                if (_seekSince < 0f) { _seekSince = Time.unscaledTime; _seekFrom = BtcReplay.PlayClock; }
                return Time.unscaledTime - _seekSince > 1.2f ? 2 : 0;
            }
            _seekSince = -1f;
            return 0;
        }

        /// <summary>Draws at most once per frame and event type (called from the replay UI and from the preprocess cover).</summary>
        internal static void Draw()
        {
            int mode = Mode();
            if (mode == 0) return;
            Event e = Event.current;
            if (_lastFrame == Time.frameCount && _lastType == e.type) return;
            _lastFrame = Time.frameCount; _lastType = e.type;
            int fs = Mathf.Max(14, Screen.height / 38);
            BtcReplayUI.Ensure(fs);
            if (BtcReplayUI.sBarLabel == null) return;
            int depth = GUI.depth;
            GUI.depth = -10000;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { e.Use(); BtcReplay.StopReplay(); GUI.depth = depth; return; }

            bool light = mode == 2;
            BtcReplayUI.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.02f, 0.02f, 0.03f, light ? 0.55f : 1f));
            float w = Mathf.Min(fs * 32f, Screen.width - 40f), rowH = fs * 2.2f;
            int n = light ? 0 : 3;
            float h = fs * 5.4f + rowH * n + (light ? 0f : fs * 1.2f);
            Rect card = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            BtcReplayUI.Fill(card, new Color(0f, 0f, 0f, 0.78f));
            BtcReplayUI.Fill(new Rect(card.x, card.y, card.width, 2f), BtcMenu.AccentA(0.9f));

            float el = _t0 >= 0f ? Time.unscaledTime - _t0 : (_seekSince >= 0f ? Time.unscaledTime - _seekSince : 0f);
            string head = light ? Tt("Seeking to ", "Перемотка к ") + BtcReplayUI.TClock(BtcReplay.SeekTarget) : Tt("Opening the replay", "Открываю реплей");
            if (_big == null || _big.fontSize != fs + 4) { _big = new GUIStyle(BtcReplayUI.sBarLabel); _big.fontSize = fs + 4; }
            float y = card.y + fs * 0.7f;
            BtcReplayIcons.Glyph(light ? BtcReplayIcons.Fwd10 : BtcReplayIcons.Play, new Rect(card.x + fs * 0.6f, y, fs * 1.8f, fs * 1.8f), BtcMenu.AccentA(1f));
            GUI.Label(new Rect(card.x + fs * 2.8f, y, w - fs * 8f, fs * 1.8f), head, _big);
            GUI.Label(new Rect(card.xMax - fs * 5.2f, y, fs * 4.6f, fs * 1.8f), el.ToString("0") + " s", BtcReplayUI.sBarLabel);
            y += fs * 2.2f;

            int step; float prog = -1f;
            if (light)
            {
                float span = Mathf.Max(0.1f, BtcReplay.SeekTarget - _seekFrom);
                prog = Mathf.Clamp01((BtcReplay.PlayClock - _seekFrom) / span);
            }
            else
            {
                bool cached = BtcReplaySeek.HasCacheFor(BtcReplay.FeedFile);
                step = BtcReplaySeek.Preprocessing ? 3 : (BtcReplay.Loading ? (BtcReplay.LoadStep > 0 ? BtcReplay.LoadStep : 2) : 3);
                float sp = step == 3 ? BtcReplaySeek.PrepFraction : -1f;
                prog = cached ? Mathf.Clamp01(((Mathf.Min(step, 3) - 1) + (step >= 3 ? 1f : 0.5f)) / 3f) : Mathf.Clamp01(((step - 1) + (sp >= 0f ? sp : 0.5f)) / 3f);
                string[] names = { Tt("Reading the replay file", "Чтение файла реплея"), Tt("Loading the world", "Загрузка мира"), cached ? Tt("Keyframes ready (from the cache)", "Ключевые кадры готовы (из кэша)") : Tt("Placing keyframes for instant seek", "Расстановка ключевых кадров для мгновенной перемотки") };
                int[] icons = { BtcReplayIcons.Blueprint, BtcReplayIcons.Map, BtcReplayIcons.Wave };
                for (int i = 0; i < 3; i++)
                {
                    int num = i + 1;
                    Rect ir = new Rect(card.x + fs * 0.6f, y + rowH * 0.1f, rowH * 0.8f, rowH * 0.8f);
                    Color dim = new Color(1f, 1f, 1f, 0.35f);
                    if (num < step || (num == 3 && cached)) { BtcReplayUI.Fill(ir, BtcMenu.AccentA(0.15f)); BtcReplayIcons.Glyph(BtcReplayIcons.Check, ir, BtcMenu.AccentA(0.95f)); }
                    else if (num == step) { BtcReplayUI.Fill(ir, new Color(1f, 1f, 1f, 0.1f)); Spinner(ir); }
                    else { BtcReplayUI.Fill(ir, new Color(1f, 1f, 1f, 0.05f)); BtcReplayIcons.Glyph(icons[i], ir, dim); }
                    Color old = GUI.contentColor;
                    GUI.contentColor = num == step ? Color.white : (num < step ? new Color(1f, 1f, 1f, 0.7f) : dim);
                    GUI.Label(new Rect(ir.xMax + fs * 0.7f, y, w - rowH - fs * 7f, rowH), names[i], BtcReplayUI.sBarLabel);
                    GUI.contentColor = old;
                    if (num == step && sp >= 0f) GUI.Label(new Rect(card.xMax - fs * 5.2f, y, fs * 4.6f, rowH), Mathf.RoundToInt(sp * 100f) + "%", BtcReplayUI.sBarLabel);
                    y += rowH;
                }
                y += fs * 0.3f;
            }
            Rect bar = new Rect(card.x + fs * 0.6f, y, w - fs * 1.2f, fs * 0.5f);
            BtcReplayUI.Fill(bar, new Color(1f, 1f, 1f, 0.14f));
            BtcReplayUI.Fill(new Rect(bar.x, bar.y, bar.width * prog, bar.height), BtcMenu.AccentA(0.95f));
            y += fs * 1.2f;
            float ib = fs * 1.6f;
            Rect ex = new Rect(card.x + fs * 0.6f, y, ib, ib);
            if (BtcReplayIcons.Plain(ex, BtcReplayIcons.Stop, BtcReplayUI.Red, Tt("Exit the replay (Esc)", "Выйти из реплея (Esc)"))) BtcReplay.StopReplay();
            GUI.Label(new Rect(ex.xMax + fs * 0.5f, y, fs * 14f, ib), Tt("Exit (Esc)", "Выйти (Esc)"), BtcReplayUI.sBarLabel);
            BtcReplayIcons.DrawTip(fs);
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseUp) && !light) e.Use();   // the cover swallows clicks
            GUI.depth = depth;
        }

        /// <summary>Eight squares on a ring, one bright and a fading tail, turning.</summary>
        private static void Spinner(Rect r)
        {
            float cx = r.center.x, cy = r.center.y, rad = r.width * 0.28f, sz = r.width * 0.16f;
            int head = (int)(Time.unscaledTime * 10f) % 8;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                float age = (head - i + 8) % 8;
                BtcReplayUI.Fill(new Rect(cx + Mathf.Cos(a) * rad - sz * 0.5f, cy + Mathf.Sin(a) * rad - sz * 0.5f, sz, sz), new Color(0.55f, 0.85f, 0.55f, Mathf.Clamp01(1f - age / 6f)));
            }
        }
    }
}
