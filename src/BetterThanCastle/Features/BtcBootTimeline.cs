using System;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
    /// <summary>One "[Boot] timeline" log line per session: process start -> Unity -> plugin -> scenes -> main menu,
    /// plus the long frames of the first 20 s in the menu. BepInEx flushes its disk log in batches, so wall-clock
    /// timestamps of log lines are useless for boot timing; these are taken in-game.</summary>
    internal static class BtcBootTimeline
    {
        private static readonly StringBuilder Sb = new StringBuilder(512);
        private static double _preUnity = -1;   // seconds the process lived before Unity's clock started
        private static bool _done;

        internal static void Init()
        {
            try
            {
                double alive = (DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalSeconds;
                _preUnity = alive - Time.realtimeSinceStartup;
                SceneManager.sceneLoaded += OnScene;
                new GameObject("BtcBootTimeline") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Runner>();
            }
            catch (Exception e) { BtcLog.Warn("Boot", "timeline: " + e.Message); }
        }

        /// <summary>Time since process start (s), on Unity's clock.</summary>
        internal static float Now { get { return (float)(Time.realtimeSinceStartup + (_preUnity < 0 ? 0 : _preUnity)); } }

        internal static void Mark(string name)
        {
            if (_done) return;
            Sb.Append(' ').Append(name).Append('=').Append(Now.ToString("0.00"));
        }

        private static void OnScene(Scene s, LoadSceneMode m) { Mark("scene:" + s.name); }

        private sealed class Runner : MonoBehaviour
        {
            private float _menuAt = -1f;
            private int _frames;
            private float _long;
            private int _longN;
            private readonly StringBuilder _spikes = new StringBuilder(256);

            private void Start() { DontDestroyOnLoad(gameObject); }

            private void Update()
            {
                if (_done) { Destroy(gameObject); return; }
                if (_menuAt < 0f)
                {
                    if (SceneManager.GetActiveScene().name != "SceneMenu") return;
                    _menuAt = Now; Mark("menuFrame1");
                    return;
                }
                _frames++;
                float dt = Time.unscaledDeltaTime;
                if (_frames == 2) Mark("menuFrame2");
                if (dt > 0.1f)
                {
                    _long += dt; _longN++;
                    if (_longN <= 12) _spikes.Append(' ').Append((Now - dt).ToString("0.0")).Append(':').Append((int)(dt * 1000f));
                }
                bool left = SceneManager.GetActiveScene().name != "SceneMenu" || BtcLoadBoost.Loading;   // joined a game early
                if (!left && Now - _menuAt < 20f) return;
                if (left) Mark("leftMenu");
                _done = true;
#if DEV
                if (!left) Dev.BtcDevProbe.SnapshotEarly("menu");
#endif
                BtcLog.Info("Boot", "timeline (s since process start): preUnity=" + _preUnity.ToString("0.00") + Sb
                    + " | menu long frames " + _longN + " (" + (int)(_long * 1000f) + " ms):" + _spikes);
            }
        }
    }
}
