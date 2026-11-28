using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Brix.Network;
using Microsoft.Win32;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
    /// <summary>
    /// Double click on a .btcr / .btcm file in Windows opens it in the game. The mod registers both types for the current user only
    /// (HKCU\Software\Classes, command = this game exe -btcopen "file"; Replay settings switch, on by default, off removes the keys); no game file is touched.
    /// Game not running: it starts with the argument, the replay opens as soon as the main menu is ready. Game running: the new process writes
    /// BetterThanCastle/open-request.txt and quits in the plugin Awake; the running game picks the file up within a second.
    /// </summary>
    internal static class BtcReplayOpen
    {
        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private const string ProgReplay = "BetterThanCastle.Replay", ProgMatch = "BetterThanCastle.Match";
        private static string _pending;
        private static float _nextPoll, _readySince = -1f;
        private static bool _registered;

        private static string ReqFile { get { return Path.Combine(BtcPaths.Root, "open-request.txt"); } }

        internal static bool On { get { return BtcMenuState.Bool("replay:assoc", true); } }

        internal static void SetOn(bool v)
        {
            BtcMenuState.SetBool("replay:assoc", v);
            if (v) Register(); else Unregister();
        }

        private static string FileArg()
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 1; i < a.Length; i++)
            {
                if (a[i] == "-btcopen" && i + 1 < a.Length) return a[i + 1];
                string e = Path.GetExtension(a[i]).ToLowerInvariant();
                if ((e == ".btcr" || e == ".btcm") && File.Exists(a[i])) return a[i];
            }
            return null;
        }

        /// <summary>Plugin Awake: true = another game of this install already runs; the file was handed over and this process quits.</summary>
        internal static bool Boot()
        {
            try
            {
                BtcPaths.Init();
                string f = FileArg();
                if (f == null) return false;
                if (OtherInstance())
                {
                    File.WriteAllText(ReqFile, f, new UTF8Encoding(false));
                    BtcLog.Info("ReplayOpen", "game already running: handed over " + f + ", quitting");
                    Application.Quit();
                    return true;
                }
                _pending = f;
                BtcLog.Info("ReplayOpen", "opened with " + f);
            }
            catch (Exception e) { BtcLog.Error("ReplayOpen", "boot: " + e.Message); }
            return false;
        }

        private static bool OtherInstance()
        {
            Process me = Process.GetCurrentProcess();
            string myPath = null;
            try { myPath = me.MainModule.FileName; } catch (Exception) { }
            foreach (Process p in Process.GetProcessesByName(me.ProcessName))
            {
                if (p.Id == me.Id) continue;
                string path = null;
                try { path = p.MainModule.FileName; } catch (Exception) { }
                if (myPath == null || path == null || string.Equals(path, myPath, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ registry (current user only)
        [DllImport("shell32.dll")] private static extern void SHChangeNotify(int eventId, uint flags, IntPtr a, IntPtr b);

        private static string ThisExe()
        {
            try { return Process.GetCurrentProcess().MainModule.FileName; }
            catch (Exception) { return null; }
        }

        /// <summary>Game exe set by the player (several game copies), as typed or pasted from Windows "Copy as path" (quotes, a folder is fine too); "" = this game.</summary>
        internal static string ExeSetting { get { return BtcMenuState.Get("replay:assocExe", ""); } }

        /// <summary>The exe the typed setting points to, null when it is empty or not an existing exe.</summary>
        internal static string TypedExe(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            s = s.Trim().Trim('"').Trim();
            if (s.Length == 0) return null;
            try
            {
                if (Directory.Exists(s)) s = Path.Combine(s, "Castle Story.exe");
                if (File.Exists(s) && Path.GetExtension(s).ToLowerInvariant() == ".exe") return Path.GetFullPath(s);
            }
            catch (Exception) { }
            return null;
        }

        private static string ExePath() { return TypedExe(ExeSetting) ?? ThisExe(); }

        internal static void SetExe(string s)
        {
            BtcMenuState.Set("replay:assocExe", s ?? "");
            if (On && (string.IsNullOrEmpty(s) || TypedExe(s) != null)) Register();
        }

        private static GUIStyle _tf, _lbl;

        /// <summary>Replay settings row: the game exe replay files open with (empty = this game). Typing stops on a click elsewhere.</summary>
        internal static void DrawExeRow(ref float y, float w, int fs)
        {
            if (_tf == null || _tf.fontSize != Mathf.RoundToInt(fs * 0.85f))
            {
                _tf = new GUIStyle(GUI.skin.textField); _tf.fontSize = Mathf.RoundToInt(fs * 0.85f); _tf.alignment = TextAnchor.MiddleLeft;
                _tf.normal.background = null; _tf.focused.background = null; _tf.hover.background = null; _tf.active.background = null;
                _tf.normal.textColor = Color.white; _tf.focused.textColor = Color.white; _tf.hover.textColor = Color.white; _tf.padding = new RectOffset(6, 6, 0, 0);
                _lbl = new GUIStyle(BtcReplayUI.sLabel ?? GUI.skin.label); _lbl.fontSize = Mathf.RoundToInt(fs * 0.8f); _lbl.alignment = TextAnchor.MiddleLeft; _lbl.clipping = TextClipping.Clip;
            }
            float pad = fs, h = fs * 1.8f;
            string cur = ExeSetting;
            string typed = TypedExe(cur);
            bool bad = cur.Trim().Length > 0 && typed == null;
            Color old = GUI.contentColor;
            GUI.contentColor = new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(new Rect(pad, y, w - pad * 2f, h), Tt("Game for replay files (if you have several copies of the game: paste the path of its Castle Story.exe, Copy as path works)", "Игра для файлов реплеев (если у вас несколько копий игры: вставьте путь к её Castle Story.exe, подходит «Копировать как путь»)"), _lbl);
            GUI.contentColor = old;
            y += h;
            Rect fr = new Rect(pad, y, w - pad * 2f - h * 1.2f, h);
            BtcReplayUI.Fill(fr, new Color(1f, 1f, 1f, 0.07f));
            if (bad) BtcReplayUI.Fill(new Rect(fr.x, fr.yMax - 2f, fr.width, 2f), BtcReplayUI.Red);
            BtcUi.Field(fr);
            string nv = GUI.TextField(fr, cur, 260, _tf);
            if (nv != cur)
            {
                string was = cur;
                SetExe(nv);
                BtcUndo.Push("replay", Tt("Game for replay files", "Игра для файлов реплеев"), delegate { SetExe(was); }, "replay:assocExe");
            }
            if (cur.Length == 0 && GUIUtility.keyboardControl == 0)
            {
                GUI.contentColor = new Color(1f, 1f, 1f, 0.35f);
                GUI.Label(new Rect(fr.x + 6f, fr.y, fr.width - 12f, fr.height), Tt("this game: ", "эта игра: ") + (ThisExe() ?? "?"), _lbl);
                GUI.contentColor = old;
            }
            Rect st = new Rect(fr.xMax + h * 0.2f, y, h, h);
            if (bad) BtcReplayIcons.Plain(st, BtcReplayIcons.Cross, BtcReplayUI.Red, Tt("No Castle Story exe at this path: replay files still open with this game", "По этому пути нет exe Castle Story: файлы реплеев пока открываются этой игрой"));
            else BtcReplayIcons.Plain(st, BtcReplayIcons.Check, BtcReplayUI.Green, Tt("Replay files open with: ", "Файлы реплеев открываются через: ") + (ExePath() ?? "?"));
            y += h + fs * 0.5f;
        }

        /// <summary>Exe of the current .btcr association (any program), null when none.</summary>
        private static string RegisteredExe()
        {
            using (RegistryKey e = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.btcr"))
            {
                string prog = e != null ? e.GetValue("") as string : null;
                if (string.IsNullOrEmpty(prog)) return null;
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + prog + @"\shell\open\command"))
                {
                    string c = k != null ? k.GetValue("") as string : null;
                    if (string.IsNullOrEmpty(c)) return null;
                    if (c.StartsWith("\"")) { int q = c.IndexOf('"', 1); return q > 1 ? c.Substring(1, q - 1) : null; }
                    int sp = c.IndexOf(' ');
                    return sp > 0 ? c.Substring(0, sp) : c;
                }
            }
        }

        internal static void Register() { Register(true); }

        /// <summary>force = the player changed the setting; at start (force false) a working association of another game copy is kept, so several copies do not fight over it.</summary>
        internal static void Register(bool force)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            try
            {
                string exe = ExePath();
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return;
                if (!force && TypedExe(ExeSetting) == null)
                {
                    string reg = RegisteredExe();
                    if (reg != null && File.Exists(reg) && !string.Equals(Path.GetFullPath(reg), exe, StringComparison.OrdinalIgnoreCase)) { _registered = true; BtcLog.Info("ReplayOpen", "replay files stay with " + reg); return; }
                }
                string cmd = "\"" + exe + "\" -btcopen \"%1\"";
                bool changed = false;
                changed |= SetType(".btcr", ProgReplay, Tt("Castle Story replay (BetterThanCastle)", "Реплей Castle Story (BetterThanCastle)"), exe, cmd);
                changed |= SetType(".btcm", ProgMatch, Tt("Castle Story match replay (BetterThanCastle)", "Реплей катки Castle Story (BetterThanCastle)"), exe, cmd);
                _registered = true;
                if (changed)
                {
                    SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
                    BtcLog.Info("ReplayOpen", "replay files open with " + exe);
                }
            }
            catch (Exception e) { BtcLog.Warn("ReplayOpen", "register: " + e.Message); }
        }

        private static bool SetType(string ext, string prog, string desc, string exe, string cmd)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + prog + @"\shell\open\command"))
            {
                if (k.GetValue("") as string == cmd)
                {
                    using (RegistryKey e = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + ext))
                        if (e != null && e.GetValue("") as string == prog) return false;   // already ours, same exe
                }
                k.SetValue("", cmd);
            }
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + prog)) k.SetValue("", desc);
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + prog + @"\DefaultIcon")) k.SetValue("", "\"" + exe + "\",0");
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ext)) k.SetValue("", prog);
            return true;
        }

        internal static void Unregister()
        {
            try
            {
                foreach (string[] t in new[] { new[] { ".btcr", ProgReplay }, new[] { ".btcm", ProgMatch } })
                {
                    using (RegistryKey e = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + t[0]))
                        if (e != null && e.GetValue("") as string == t[1]) { e.Close(); Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + t[0]); }
                    using (RegistryKey p = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + t[1]))
                        if (p != null) { p.Close(); Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + t[1]); }
                }
                _registered = false;
                SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
                BtcLog.Info("ReplayOpen", "replay file types removed");
            }
            catch (Exception e) { BtcLog.Warn("ReplayOpen", "unregister: " + e.Message); }
        }

        // ------------------------------------------------------------------ per frame (BtcReplay.Tick)
        private static bool MenuReady()
        {
            try
            {
                // a replay started before the game finished setting up its network side never loads (stuck at "loading the world",
                // "host id out of bound"); 25 s after start is what the DEV labs use and never failed
                if (Time.realtimeSinceStartup < 25f || GameParam.instance == null || UNetManager.instance == null) return false;
                if (NetworkServer.active || NetworkClient.active || SceneManager.GetActiveScene().buildIndex == 2) return false;
                return BtcReplay.CanStartReplay() && !BtcReplay.RestartPending;
            }
            catch (Exception) { return false; }
        }

        // watchdog for a replay this class started: still loading after 45 s = stuck, restart it once
        private static BtcReplay.Data _started;
        private static float _startedAt;
        private static bool _retried;

        private static void Watch(float now)
        {
            if (_started == null) return;
            if (BtcReplay.Mode != BtcReplay.State.Playing || !BtcReplay.Loading || BtcReplaySeek.Preprocessing) { if (BtcReplay.Mode != BtcReplay.State.Playing || BtcReplay.FeedGo) _started = null; return; }
            if (now - _startedAt < 45f) return;
            BtcReplay.Data d = _started;
            _started = null;
            if (_retried) { BtcLog.Warn("ReplayOpen", "replay still loading after a restart: left as it is"); return; }
            _retried = true;
            BtcLog.Warn("ReplayOpen", "replay stuck while loading for 45 s: restarting it");
            BtcReplayChainPlay.Stop();
            _pending = d.FilePath;
            _readySince = -1f;
        }

        internal static void Tick()
        {
            float now = Time.unscaledTime;
            Watch(now);
            if (now >= _nextPoll)
            {
                _nextPoll = now + 1f;
                if (!_registered && On && Time.realtimeSinceStartup > 5f) Register(false);
                try
                {
                    string rf = ReqFile;
                    if (File.Exists(rf))
                    {
                        string f = File.ReadAllText(rf, Encoding.UTF8).Trim();
                        File.Delete(rf);
                        if (f.Length > 0) { _pending = f; _readySince = -1f; BtcLog.Info("ReplayOpen", "request from Windows: " + f); BtcReplayDrop.BringToFront(); }
                    }
                }
                catch (Exception e) { BtcLog.Warn("ReplayOpen", "request: " + e.Message); }
            }
            if (_pending == null) return;
            bool inGame = BtcReplay.Mode == BtcReplay.State.Playing || NetworkServer.active || NetworkClient.active;
            if (inGame)
            {
                // never interrupt a match or a replay: add the file and say where it is
                string f = _pending; _pending = null;
                BtcReplayDrop.Add(new List<string> { f }, false);
                return;
            }
            if (!MenuReady()) { _readySince = -1f; return; }
            if (_readySince < 0f) { _readySince = now; return; }
            if (now - _readySince < 2f) return;   // let the main menu settle first
            string file = _pending;
            _pending = null;
            List<string> names = BtcReplayDrop.Add(new List<string> { file }, true);
            if (names.Count == 0) return;
            BtcReplay.Data d = null;
            foreach (BtcReplay.Data x in BtcReplayUI.list) if (x.FileName == names[0]) { d = x; break; }
            if (d == null || d.Error != null || !d.Complete || d.SnapBytes <= 0) { BtcToast.Show(Tt("This replay file cannot be played.", "Этот файл реплея нельзя проиграть.")); return; }
            BtcReplayChain.Match m = BtcReplayChain.Of(d);
            BtcLog.Info("ReplayOpen", "playing " + d.FileName + (m != null ? " (match of " + m.Sessions + " sessions)" : ""));
            if (m != null)
            {
                float g = 0f;
                int i = m.Main.IndexOf(m.Root.D == d ? m.Root : FindNode(m, d));
                if (i > 0) g = m.Main.Off(i);
                BtcReplayChainPlay.Start(m, null, g);
            }
            else BtcReplay.StartReplay(d);
            if (BtcReplay.Mode == BtcReplay.State.Playing) { ResourcePackManager.CloseMenu(); _started = d; _startedAt = now; }
        }

        private static BtcReplayChain.Node FindNode(BtcReplayChain.Match m, BtcReplay.Data d)
        {
            for (int i = 0; i < m.Nodes.Count; i++) if (m.Nodes[i].D == d) return m.Nodes[i];
            return null;
        }
    }
}
