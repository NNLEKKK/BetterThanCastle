using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Local window mode selector (0 keep game setting, 1 fullscreen, 2 windowed, 3 borderless). Client-only, nothing on the wire.</summary>
    internal static class BtcDisplay
    {
        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int i);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr h, int idx);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr h, int idx, int v);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int cx, uint flags);

        private const int GWL_STYLE = -16;
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
        private const uint SWP_NOZORDER = 0x4, SWP_FRAMECHANGED = 0x20, SWP_SHOWWINDOW = 0x40;

        private static Runner _runner;
        private static int _pendingMode, _wait, _w, _h;

        internal static int Mode { get { return BtcConfig.WindowMode == null ? 0 : BtcConfig.WindowMode.Value; } }

        internal static void Init()
        {
            try { if (Mode != 0) Apply(Mode, 60); }
            catch (Exception e) { BtcLog.Error("Display", "Init: " + e); }
        }

        internal static void Apply(int mode, int delayFrames)
        {
            try
            {
                if (mode < 1 || mode > 3) return;
                if (BtcConfig.WindowMode != null) BtcConfig.WindowMode.Value = mode;
                if (_runner == null)
                {
                    GameObject go = new GameObject("BtcDisplayRunner");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _runner = go.AddComponent<Runner>();
                }
                _pendingMode = mode;
                _wait = delayFrames;
                _runner.enabled = true;
            }
            catch (Exception e) { BtcLog.Error("Display", "Apply: " + e); }
        }

        // Stage 1 (after the startup delay): SetResolution. Stage 2 (2 frames later): window style.
        private static void Step()
        {
            if (_wait > 0) { _wait--; return; }
            int dw = GetSystemMetrics(0), dh = GetSystemMetrics(1);
            if (dw <= 0 || dh <= 0) { dw = Screen.currentResolution.width; dh = Screen.currentResolution.height; }
            if (_pendingMode > 0)
            {
                int m = _pendingMode;
                _pendingMode = -m;
                if (m == 1) { _w = dw; _h = dh; Screen.SetResolution(dw, dh, true); }
                else if (m == 3) { _w = dw; _h = dh; Screen.SetResolution(dw, dh, false); }
                else
                {
                    int w = Screen.width, h = Screen.height;
                    if (w >= dw - 8 || h >= dh - 8 || Screen.fullScreen) { w = 1280; h = 720; }
                    _w = w; _h = h;
                    Screen.SetResolution(w, h, false);
                }
                _wait = 2;
                return;
            }
            int mode = -_pendingMode;
            _pendingMode = 0;
            _runner.enabled = false;
            if (mode == 1) { BtcLog.Info("Display", "fullscreen " + _w + "x" + _h); return; }
            IntPtr hwnd = GetActiveWindow();
            if (hwnd == IntPtr.Zero) { BtcLog.Warn("Display", "no window handle, style not changed"); return; }
            int style = GetWindowLong(hwnd, GWL_STYLE);
            if (mode == 3)
            {
                style = (style & ~WS_OVERLAPPEDWINDOW) | WS_POPUP | WS_VISIBLE;
                SetWindowLong(hwnd, GWL_STYLE, style);
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, dw, dh, SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
            }
            else
            {
                style = (style & ~WS_POPUP) | WS_OVERLAPPEDWINDOW | WS_VISIBLE;
                SetWindowLong(hwnd, GWL_STYLE, style);
                SetWindowPos(hwnd, IntPtr.Zero, Mathf.Max(0, (dw - _w) / 2), Mathf.Max(0, (dh - _h) / 2), _w + 16, _h + 39, SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
            }
            BtcLog.Info("Display", (mode == 3 ? "borderless " : "windowed ") + _w + "x" + _h);
        }

        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                try { Step(); }
                catch (Exception e) { BtcLog.Error("Display", "Step: " + e); _pendingMode = 0; enabled = false; }
            }
        }
    }
}
