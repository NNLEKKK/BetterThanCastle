using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay files from outside the game: Ctrl+V with .btcr / .btcm files in the Windows clipboard (copied in Explorer or Discord), or dragged
    /// onto the game window. The files are copied into the replays folder (a .btcm match is unpacked by BtcReplayChain), the list is refreshed and
    /// the new items blink in the library; out of a match the menu opens on the Replay tab, in a match only a toast tells about it.
    /// Windows only (user32/shell32); the drop is seen through a WH_GETMESSAGE hook of the main thread (WM_DROPFILES), no window subclassing.
    /// </summary>
    internal static class BtcReplayDrop
    {
        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private const uint CF_HDROP = 15;
        private const int WH_GETMESSAGE = 3;
        private const uint WM_DROPFILES = 0x0233;

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int ptX, ptY; }

        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr owner);
        [DllImport("user32.dll")] private static extern bool CloseClipboard();
        [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint DragQueryFileW(IntPtr hDrop, uint index, StringBuilder file, uint size);
        [DllImport("shell32.dll")] private static extern void DragFinish(IntPtr hDrop);
        [DllImport("shell32.dll")] private static extern void DragAcceptFiles(IntPtr hwnd, bool accept);
        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc fn, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr hWnd, StringBuilder name, int max);

        private static bool _init, _failed;
        private static IntPtr _hook = IntPtr.Zero, _hwnd = IntPtr.Zero;
        private static HookProc _proc;              // kept alive: the native hook calls it
        private static EnumWindowsProc _enum;
        private static readonly List<string> Dropped = new List<string>();   // filled inside the hook (main thread, message pump)
        private static float _nextInitTry;

        private static bool IsReplayFile(string p)
        {
            if (string.IsNullOrEmpty(p)) return false;
            string e = Path.GetExtension(p).ToLowerInvariant();
            return e == ".btcr" || e == ".btcm";
        }

        /// <summary>Every frame (BtcReplay.Tick).</summary>
        internal static void Tick()
        {
            if (_failed || Application.platform != RuntimePlatform.WindowsPlayer) return;
            try
            {
                if (!_init && Time.unscaledTime >= _nextInitTry) Init();
                if (Dropped.Count > 0)
                {
                    List<string> files = new List<string>(Dropped);
                    Dropped.Clear();
                    Take(files, Tt("dropped", "перетащено"));
                }
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (ctrl && Input.GetKeyDown(KeyCode.V) && GUIUtility.keyboardControl == 0)
                {
                    List<string> files = ClipboardFiles();
                    if (files.Count > 0) Take(files, Tt("pasted", "вставлено"));
                }
            }
            catch (Exception e) { BtcLog.Error("ReplayDrop", "tick: " + e.Message); _failed = true; }
        }

        private static void Init()
        {
            _nextInitTry = Time.unscaledTime + 5f;
            uint tid = GetCurrentThreadId();
            _hwnd = IntPtr.Zero;
            _enum = delegate (IntPtr h, IntPtr l)
            {
                StringBuilder sb = new StringBuilder(64);
                GetClassNameW(h, sb, 64);
                if (sb.ToString() == "UnityWndClass") { _hwnd = h; return false; }
                return true;
            };
            EnumThreadWindows(tid, _enum, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero) return;   // window not there yet: try again later
            DragAcceptFiles(_hwnd, true);
            _proc = OnHook;
            _hook = SetWindowsHookEx(WH_GETMESSAGE, _proc, IntPtr.Zero, tid);
            _init = true;
            BtcLog.Info("ReplayDrop", "drag and drop " + (_hook != IntPtr.Zero ? "on" : "hook failed") + ", Ctrl+V on");
        }

        private static IntPtr OnHook(int code, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (code >= 0 && lParam != IntPtr.Zero)
                {
                    // every window message of the thread passes here (mouse moves too): read only the id, no boxed MSG copy (garbage per message)
                    uint msg = (uint)Marshal.ReadInt32(lParam, IntPtr.Size);
                    if (msg == WM_DROPFILES)
                    {
                        IntPtr hDrop = Marshal.ReadIntPtr(lParam, IntPtr.Size * 2);
                        uint n = DragQueryFileW(hDrop, 0xFFFFFFFF, null, 0);
                        for (uint i = 0; i < n && i < 64; i++)
                        {
                            StringBuilder sb = new StringBuilder(1024);
                            DragQueryFileW(hDrop, i, sb, 1024);
                            Dropped.Add(sb.ToString());
                        }
                        DragFinish(hDrop);
                    }
                }
            }
            catch (Exception) { }
            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        private static List<string> ClipboardFiles()
        {
            List<string> r = new List<string>();
            if (!IsClipboardFormatAvailable(CF_HDROP) || !OpenClipboard(IntPtr.Zero)) return r;
            try
            {
                IntPtr h = GetClipboardData(CF_HDROP);
                if (h == IntPtr.Zero) return r;
                uint n = DragQueryFileW(h, 0xFFFFFFFF, null, 0);
                for (uint i = 0; i < n && i < 64; i++)
                {
                    StringBuilder sb = new StringBuilder(1024);
                    DragQueryFileW(h, i, sb, 1024);
                    r.Add(sb.ToString());
                }
            }
            finally { CloseClipboard(); }
            return r;
        }

        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);

        /// <summary>A file opened from Windows while the game runs: bring the game window up.</summary>
        internal static void BringToFront()
        {
            try { if (_hwnd == IntPtr.Zero) return; if (IsIconic(_hwnd)) ShowWindow(_hwnd, 9); SetForegroundWindow(_hwnd); }
            catch (Exception) { }
        }

        /// <summary>Opened from Windows (BtcReplayOpen): copy in, refresh, blink; openTab = open the Replay tab (no toast then). Returns the replay file names.</summary>
        internal static List<string> Add(List<string> files, bool quiet)
        {
            return Take(files, Tt("opened", "открыто"), quiet);
        }

        private static void Take(List<string> files, string how) { Take(files, how, false); }

        /// <summary>Copies the replay files into the replays folder and shows them.</summary>
        private static List<string> Take(List<string> files, string how, bool quiet)
        {
            string dir = BtcReplay.ReplaysDir;
            Directory.CreateDirectory(dir);
            List<string> got = new List<string>();
            int skipped = 0, packs = 0;
            for (int i = 0; i < files.Count; i++)
            {
                string src = files[i];
                if (!IsReplayFile(src) || !File.Exists(src)) { skipped++; continue; }
                try
                {
                    string name = Path.GetFileName(src);
                    string dst = Path.Combine(dir, name);
                    bool same = File.Exists(dst) && new FileInfo(dst).Length == new FileInfo(src).Length;
                    if (!string.Equals(Path.GetFullPath(src), Path.GetFullPath(dst), StringComparison.OrdinalIgnoreCase) && !same)
                    {
                        if (File.Exists(dst))
                        {
                            string b = Path.GetFileNameWithoutExtension(name), ext = Path.GetExtension(name);
                            for (int k = 2; File.Exists(dst); k++) dst = Path.Combine(dir, b + "_" + k + ext);
                        }
                        File.Copy(src, dst);
                    }
                    if (Path.GetExtension(dst).ToLowerInvariant() == ".btcm") { packs++; got.AddRange(BtcReplayChain.ImportNow(dst)); }
                    else got.Add(Path.GetFileName(dst));
                }
                catch (Exception e) { BtcLog.Warn("ReplayDrop", "copy " + src + ": " + e.Message); skipped++; }
            }
            if (got.Count == 0)
            {
                if (skipped > 0 && files.Count == skipped && how == Tt("dropped", "перетащено")) { BtcSfx.Miss(); BtcToast.Show(Tt("Only replay files (.btcr, .btcm) can be dropped here", "Сюда можно перетащить только файлы реплеев (.btcr, .btcm)")); }
                return got;
            }
            BtcLog.Info("ReplayDrop", "taken: " + got.Count + " replay file(s)" + (packs > 0 ? ", " + packs + " match file(s)" : "") + (skipped > 0 ? ", skipped " + skipped : ""));
            BtcReplayUI.Refresh();
            BtcReplayChain.Invalidate();
            BtcReplayLibrary.Flash(got);
            if (quiet) return got;   // opened from Windows out of a match: it starts playing right away
            BtcSfx.Ok();
            bool inGame = BtcReplay.Mode == BtcReplay.State.Playing || !BtcReplay.CanStartReplay();
            string msg = (got.Count == 1 ? Tt("Replay added: ", "Реплей добавлен: ") + Path.GetFileNameWithoutExtension(got[0]) : Tt("Replays added: ", "Реплеев добавлено: ") + got.Count)
                + (inGame ? Tt("  (Replays tab)", "  (вкладка Реплеи)") : "");
            BtcToast.Show(msg);
            if (!inGame) ResourcePackManager.OpenReplays();
            return got;
        }

        /// <summary>Same as Ctrl+V (lab check of the clipboard path); returns the number of files found in the clipboard.</summary>
        internal static int PasteNow()
        {
            List<string> files = ClipboardFiles();
            BtcLog.Info("ReplayDrop", "clipboard: " + files.Count + " file(s)" + (files.Count > 0 ? " " + files[0] : "") + ", hook " + (_hook != IntPtr.Zero));
            if (files.Count > 0) Take(files, Tt("pasted", "вставлено"));
            return files.Count;
        }

        internal static void Shutdown()
        {
            try { if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook); } catch (Exception) { }
            _hook = IntPtr.Zero;
        }
    }
}
