using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay clip to GIF. One master icon in the bar: click it, drag the clip range on the timeline, check the length and the estimated file size,
    /// tune the GIF (width, fps, palette, dither, optimize), then "Clip it" replays the range once and grabs the screen exactly as the viewer sees it
    /// (current camera / POV, cursors, cones; the replay UI is hidden meanwhile). A worker thread LZW-encodes (BtcGifEncoder), writes
    /// Clips/btc-clip-*.gif under the BTC user folder and puts the file on the clipboard (Set-Clipboard: pastes as a file into chats).
    /// </summary>
    internal static class BtcReplayGif
    {
        private const float MaxSec = 30f;
        private enum St { Off, Select, Rec, Enc }
        private static St _st;

        private static float _a = -1f, _b = -1f;   // range ends in replay seconds (unordered while dragging)
        private static bool _drag, _cancel;
        private static string _msg = "";
        private static float _msgUntil;
        private static readonly List<byte[]> Frames = new List<byte[]>();
        private static int _fw, _fh, _level;
        private static bool _dither;
        private static volatile string _doneMsg;
        private static Texture2D _grab;

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }
        private static void Msg(string s) { _msg = s; _msgUntil = Time.unscaledTime + 8f; }

        private static float Lo { get { return Mathf.Min(_a, _b); } }
        private static float Hi { get { return Mathf.Max(_a, _b); } }
        private static bool HasRange { get { return _a >= 0f && _b >= 0f && Hi - Lo >= 0.5f; } }

        private static int Height(int w) { return Mathf.Max(2, Mathf.RoundToInt(w * (float)Screen.height / Screen.width)) & ~1; }

        /// <summary>Rough size: measured ~0.33 byte per pixel per frame at 252 colours with dither and no optimisation.</summary>
        private static float EstimateMb(float seconds)
        {
            int w = BtcConfig.GifWidth.Value, fps = BtcConfig.GifFps.Value;
            float[] colF = { 1f, 0.8f, 0.6f, 0.4f };
            float bpp = 0.33f * colF[Mathf.Clamp(BtcConfig.GifColors.Value, 0, 3)] * (BtcConfig.GifDither.Value ? 1f : 0.5f) * (BtcConfig.GifDiff.Value ? 0.55f : 1f);
            return seconds * fps * w * Height(w) * bpp / (1024f * 1024f);
        }

        // ------------------------------------------------------------------ timeline
        /// <summary>Call BEFORE the seek handling of the progress track: in select mode a drag on the track sets the clip range instead of seeking.</summary>
        internal static void HandleTrack(Rect area, Rect track, float total)
        {
            if (_st != St.Select) return;
            Event e = Event.current;
            float frac = Mathf.Clamp01((e.mousePosition.x - track.x) / Mathf.Max(1f, track.width));
            if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition)) { _drag = true; _a = _b = frac * total; e.Use(); }
            else if (_drag && e.type == EventType.MouseDrag) { _b = frac * total; e.Use(); }
            else if (_drag && e.type == EventType.MouseUp) { _b = frac * total; _drag = false; if (Hi - Lo > MaxSec) { if (_b > _a) _b = _a + MaxSec; else _b = _a - MaxSec; Msg(Tt("Clip cut to " + (int)MaxSec + " s", "Клип обрезан до " + (int)MaxSec + " с")); } e.Use(); }
        }

        /// <summary>Range band on the progress track.</summary>
        internal static void DrawRange(Rect r2, float total)
        {
            if (_st == St.Off || Event.current.type != EventType.Repaint || total <= 0f || _a < 0f) return;
            float x0 = r2.x + r2.width * Mathf.Clamp01(Lo / total), x1 = r2.x + r2.width * Mathf.Clamp01(Hi / total);
            Color c = new Color(0.45f, 0.8f, 1f, 1f);
            BtcReplayUI.Fill(new Rect(x0, r2.center.y - 6f, Mathf.Max(2f, x1 - x0), 12f), new Color(c.r, c.g, c.b, 0.3f));
            BtcReplayUI.Fill(new Rect(x0 - 1f, r2.center.y - 11f, 3f, 22f), c);
            BtcReplayUI.Fill(new Rect(x1 - 1f, r2.center.y - 11f, 3f, 22f), c);
        }

        // ------------------------------------------------------------------ master button and panel
        /// <summary>The one bar icon; advances cx.</summary>
        internal static void DrawButton(ref float cx, float iy, float ib, float gap)
        {
            string tip = _st == St.Off ? Tt("Make a GIF clip", "Сделать GIF-клип") : (_st == St.Select ? Tt("Cancel the clip", "Отменить клип") : (_st == St.Rec ? Tt("Recording GIF - click to stop", "Запись GIF - клик остановит") : Tt("Encoding GIF..", "Кодирую GIF..")));
            if (BtcReplayIcons.Button(new Rect(cx, iy, ib, ib), BtcReplayIcons.Gif, _st != St.Off, tip))
            {
                if (_st == St.Off) { _st = St.Select; _a = _b = -1f; _drag = false; }
                else if (_st == St.Select) { _st = St.Off; _drag = false; }
                else if (_st == St.Rec) _cancel = true;
            }
            cx += ib + gap;
        }

        private static float Slider(Rect panel, ref float y, string label, float v, float min, float max, string shown, float fs, float rowH)
        {
            GUI.Label(new Rect(panel.x + fs * 0.6f, y, fs * 7.5f, rowH), label, BtcReplayUI.sBarLabel);
            Rect sr = new Rect(panel.x + fs * 8.2f, y + rowH * 0.3f, panel.width - fs * 8.2f - fs * 5f, rowH * 0.4f);
            float nv = GUI.HorizontalSlider(sr, v, min, max);
            GUI.Label(new Rect(sr.xMax + fs * 0.5f, y, fs * 4.5f, rowH), shown, BtcReplayUI.sBarLabel);
            y += rowH;
            return nv;
        }

        /// <summary>Settings panel above the bar (select mode) or the status line.</summary>
        internal static void DrawPanel(Rect bar, float lift, float fs)
        {
            if (_st != St.Select) { DrawStatus(bar, lift, fs); return; }
            Event e = Event.current;
            float rowH = fs * 1.5f, w = fs * 24f;
            float h = rowH * 7f + fs * 0.4f;
            Rect p = new Rect(bar.xMax - w - fs * 0.5f, bar.y - lift - h - 6f, w, h);
            BtcReplayUI.Fill(p, new Color(0f, 0f, 0f, 0.88f));
            float y = p.y + fs * 0.2f;
            string info;
            if (HasRange) { float len = Hi - Lo; info = Tt("Clip ", "Клип ") + len.ToString("0.0") + Tt(" s   ~", " с   ~") + EstimateMb(len).ToString("0.0") + Tt(" MB (estimate)", " МБ (оценка)"); }
            else info = Tt("Drag on the timeline to set the clip", "Протяните по таймлайну, чтобы выбрать клип");
            GUI.Label(new Rect(p.x + fs * 0.6f, y, w - fs, rowH), info, BtcReplayUI.sBarLabel);
            y += rowH;
            int wd = Mathf.RoundToInt(Slider(p, ref y, Tt("Width", "Ширина"), BtcConfig.GifWidth.Value, 160f, 960f, BtcConfig.GifWidth.Value + " px", fs, rowH) / 20f) * 20;
            int fp = Mathf.RoundToInt(Slider(p, ref y, "FPS", BtcConfig.GifFps.Value, 5f, 25f, BtcConfig.GifFps.Value.ToString(), fs, rowH));
            int col = 3 - Mathf.RoundToInt(Slider(p, ref y, Tt("Colours", "Цвета"), 3 - BtcConfig.GifColors.Value, 0f, 3f, BtcGifEncoder.Colors[BtcConfig.GifColors.Value].ToString(), fs, rowH));
            if (wd != BtcConfig.GifWidth.Value) BtcConfig.GifWidth.Value = wd;
            if (fp != BtcConfig.GifFps.Value) BtcConfig.GifFps.Value = fp;
            if (col != BtcConfig.GifColors.Value) BtcConfig.GifColors.Value = col;
            bool dt = GUI.Toggle(new Rect(p.x + fs * 0.6f, y, w * 0.5f - fs, rowH), BtcConfig.GifDither.Value, Tt(" dither (smoother)", " дизеринг (плавнее)"));
            bool df = GUI.Toggle(new Rect(p.x + w * 0.5f, y, w * 0.5f - fs * 0.5f, rowH), BtcConfig.GifDiff.Value, Tt(" optimize (smaller)", " оптимизация (меньше)"));
            if (dt != BtcConfig.GifDither.Value) BtcConfig.GifDither.Value = dt;
            if (df != BtcConfig.GifDiff.Value) BtcConfig.GifDiff.Value = df;
            y += rowH;
            GUI.Label(new Rect(p.x + fs * 0.6f, y, w - fs, rowH), Tt("Width and colours matter most for the size", "Сильнее всего на размер влияют ширина и цвета"), BtcReplayUI.sBarLabel);
            y += rowH;
            // buttons: clip it / cancel
            float ib = rowH * 0.95f;
            Rect ok = new Rect(p.x + fs * 0.6f, y, ib, ib);
            if (BtcReplayIcons.Plain(ok, BtcReplayIcons.Check, HasRange ? BtcReplayUI.Green : new Color(1f, 1f, 1f, 0.3f), Tt("Clip it", "Заклипать")) && HasRange) Start();
            GUI.Label(new Rect(ok.xMax + fs * 0.4f, y, fs * 7f, ib), Tt("Clip it", "Заклипать"), BtcReplayUI.sBarLabel);
            Rect no = new Rect(p.x + fs * 9.5f, y, ib, ib);
            if (BtcReplayIcons.Plain(no, BtcReplayIcons.Cross, BtcReplayUI.Red, Tt("Cancel", "Отменить"))) { _st = St.Off; _drag = false; }
            GUI.Label(new Rect(no.xMax + fs * 0.4f, y, fs * 7f, ib), Tt("Cancel", "Отменить"), BtcReplayUI.sBarLabel);
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseUp) && p.Contains(e.mousePosition)) e.Use();
            DrawStatus(bar, lift + h + 8f, fs);
        }

        private static void DrawStatus(Rect bar, float lift, float fs)
        {
            if (_msg.Length == 0 || Time.unscaledTime >= _msgUntil || Event.current.type != EventType.Repaint) return;
            float h = fs * 1.5f, sw = fs * 0.62f * _msg.Length + fs * 1.2f;
            Rect t = new Rect(bar.xMax - sw - fs * 0.5f, bar.y - lift - h - 4f, sw, h);
            BtcReplayUI.Fill(t, new Color(0f, 0f, 0f, 0.88f));
            GUI.Label(new Rect(t.x + fs * 0.6f, t.y, t.width, t.height), _msg, BtcReplayUI.sBarLabel);
        }

        // ------------------------------------------------------------------ capture
        private static void Start()
        {
            if (BtcPlugin.Inst == null || !HasRange) return;
            float t0 = Lo, t1 = Mathf.Min(Hi, t0 + MaxSec);
            _st = St.Rec; _cancel = false;
            BtcPlugin.Inst.StartCoroutine(Run(t0, t1));
        }

        private static IEnumerator Run(float t0, float t1)
        {
            bool wasHidden = BtcReplayUI.Hidden;
            Frames.Clear();
            bool ok = false;
            int fps = BtcConfig.GifFps.Value, width = BtcConfig.GifWidth.Value;
            _level = Mathf.Clamp(BtcConfig.GifColors.Value, 0, 3); _dither = BtcConfig.GifDither.Value;
            bool diff = BtcConfig.GifDiff.Value;
            BtcReplay.SeekTo(t0);
            float wait = Time.unscaledTime;
            while ((BtcReplay.Seeking || Mathf.Abs(BtcReplay.PlayClock - t0) > 1.5f) && Time.unscaledTime - wait < 90f && !_cancel) yield return null;
            if (!_cancel && BtcReplay.Mode == BtcReplay.State.Playing)
            {
                if (BtcReplay.PlayPaused) BtcReplay.TogglePause();
                BtcReplayUI.Hidden = true;
                _fw = width; _fh = Height(width);
                float step = 1f / fps, next = t0, started = Time.unscaledTime;
                while (!_cancel && BtcReplay.Mode == BtcReplay.State.Playing && BtcReplay.PlayClock < t1 && Time.unscaledTime - started < (t1 - t0) * 6f + 30f)
                {
                    yield return new WaitForEndOfFrame();
                    float pc = BtcReplay.PlayClock;
                    if (pc < next) continue;
                    try { Grab(); } catch (Exception ex) { BtcLog.Error("Gif", "grab: " + ex.Message); _cancel = true; break; }
                    next += step;
                    if (pc > next + step * 3f) next = pc + step;
                }
                ok = Frames.Count >= 2;
                if (!BtcReplay.PlayPaused && BtcReplay.Mode == BtcReplay.State.Playing) BtcReplay.TogglePause();   // back to the paused state the user started from
            }
            BtcReplayUI.Hidden = wasHidden;
            if (_grab != null) { UnityEngine.Object.Destroy(_grab); _grab = null; }
            if (!ok) { Frames.Clear(); _st = St.Off; Msg(Tt("GIF cancelled", "GIF отменён")); yield break; }
            _st = St.Enc;
            Msg(Tt("Encoding GIF, " + Frames.Count + " frames..", "Кодирую GIF, кадров: " + Frames.Count));
            List<byte[]> fr = new List<byte[]>(Frames);
            Frames.Clear();
            int w = _fw, h = _fh, lvl = _level;
            _doneMsg = null;
            Thread th = new Thread(delegate () { Encode(fr, w, h, fps, lvl, diff); });
            th.IsBackground = true;
            th.Start();
            while (_doneMsg == null) yield return null;
            _st = St.Off;
            Msg(_doneMsg);
        }

        /// <summary>One screen grab, box-filtered down to the GIF size and quantized straight to palette indices (top-down).</summary>
        private static void Grab()
        {
            int sw = Screen.width, sh = Screen.height;
            if (_grab == null || _grab.width != sw || _grab.height != sh) { if (_grab != null) UnityEngine.Object.Destroy(_grab); _grab = new Texture2D(sw, sh, TextureFormat.RGB24, false); }
            _grab.ReadPixels(new Rect(0f, 0f, sw, sh), 0, 0, false);
            Color32[] src = _grab.GetPixels32();
            byte[] dst = new byte[_fw * _fh];
            float kx = (float)sw / _fw, ky = (float)sh / _fh;
            for (int y = 0; y < _fh; y++)
                for (int x = 0; x < _fw; x++)
                {
                    int r = 0, g = 0, b = 0;
                    for (int j = 0; j < 3; j++)
                    {
                        int sy = sh - 1 - Mathf.Min(sh - 1, (int)((y + (j + 0.5f) / 3f) * ky));   // ReadPixels is bottom-up
                        for (int i = 0; i < 3; i++)
                        {
                            Color32 p = src[sy * sw + Mathf.Min(sw - 1, (int)((x + (i + 0.5f) / 3f) * kx))];
                            r += p.r; g += p.g; b += p.b;
                        }
                    }
                    dst[y * _fw + x] = BtcGifEncoder.Index(r / 9, g / 9, b / 9, x, y, _level, _dither);
                }
            Frames.Add(dst);
        }

        private static void Encode(List<byte[]> frames, int w, int h, int fps, int level, bool diff)
        {
            try
            {
                string dir = Path.Combine(BtcPaths.Root, "Clips");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "btc-clip-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".gif");
                using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write)) BtcGifEncoder.Write(fs, frames, w, h, fps, level, diff);
                bool copied = Clip(path);
                long kb = new FileInfo(path).Length / 1024;
                _doneMsg = (copied ? Tt("GIF copied to the clipboard", "GIF скопирован в буфер") : Tt("GIF saved (clipboard failed)", "GIF сохранён (буфер не удался)")) + " - " + (kb / 1024f).ToString("0.0") + " MB";
                BtcLog.Info("Gif", "saved " + path + " " + kb + " KB, " + frames.Count + " frames, " + w + "x" + h + ", clipboard=" + copied);
            }
            catch (Exception ex)
            {
                BtcLog.Error("Gif", "encode: " + ex.Message);
                _doneMsg = Tt("GIF failed: ", "GIF не вышел: ") + ex.Message;
            }
        }

        /// <summary>Put the file on the clipboard as a file drop (pastes into Discord and Explorer). The command is passed encoded so any path characters are safe.</summary>
        private static bool Clip(string path)
        {
            try
            {
                string cmd = "Set-Clipboard -LiteralPath '" + path.Replace("'", "''") + "'";
                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -STA -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(cmd)));
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    if (!p.WaitForExit(20000)) { try { p.Kill(); } catch { } return false; }
                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex) { BtcLog.Warn("Gif", "clipboard: " + ex.Message); return false; }
        }
    }
}
