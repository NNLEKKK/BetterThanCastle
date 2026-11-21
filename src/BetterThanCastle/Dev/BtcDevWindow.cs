#if DEV
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV only. env BTC_HIDDEN=1: game keeps running in background, window minimized without activation, audio muted.</summary>
    internal sealed class BtcDevWindow : MonoBehaviour
    {
        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

        private const int SW_SHOWMINNOACTIVE = 7;
        private const uint SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;
        private float _next;
        private int _tries;

        internal static void Init()
        {
            if (Environment.GetEnvironmentVariable("BTC_HIDDEN") != "1") return;
            Application.runInBackground = true;
            GameObject go = new GameObject("BtcDevWindow");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<BtcDevWindow>();
            BtcLog.Info("Window", "hidden mode armed");
        }

        // Re-applied a few times: the game may reset window state on scene/resolution changes.
        private void Update()
        {
            AudioListener.volume = 0f;
            Brix.Audio.SoundEngine.mute = true;        // Wwise ignores AudioListener; runtime only, never saved to the user settings
            if (_tries >= 8 || Time.realtimeSinceStartup < _next) return;
            _next = Time.realtimeSinceStartup + 5f;
            _tries++;
            try
            {
                IntPtr h = GetActiveWindow();
                if (h == IntPtr.Zero) return;
                SetWindowPos(h, IntPtr.Zero, -32000, -32000, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                ShowWindow(h, SW_SHOWMINNOACTIVE);
            }
            catch (Exception e) { BtcLog.Warn("Window", e.Message); _tries = 8; }
        }
    }
}
#endif
