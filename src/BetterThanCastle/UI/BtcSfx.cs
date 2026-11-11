using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// UI sound feedback for the mod menu and the replay bar (hover, clicks, switches, typing, delete, confirm). No audio files: the sounds are synthesized
    /// at start into tiny PCM WAV buffers and played through the Windows winmm PlaySound (async, one at a time: a new sound replaces the old one), so the
    /// game's own audio setup (Wwise, Unity audio on or off) does not matter. Level 0..3 = off / low / medium / high (cfg Display/UiSound, the speaker icon at
    /// the bottom of the menu rail cycles it). Muted whenever the game is muted (hidden test runs, replay seeking). Local only, nothing is sent.
    /// </summary>
    internal static class BtcSfx
    {
        private enum K { Hover, Click, On, Off, Type0, Type1, Type2, Back, Delete, Ok, Tab, Find, Miss, Jump, Warn, Open, Close, Tick }

        private const int SR = 44100;
        private static readonly byte[][] Wav = new byte[18][];
        private static readonly GCHandle[] Pins = new GCHandle[18];
        private static int _built = -1;
        private static float _lastStrong = -10f, _lastAny = -10f;
        private static long _hoverKey;
        private static readonly System.Random Rnd = new System.Random();
        private static bool _broken;

        [DllImport("winmm.dll", EntryPoint = "PlaySound")]
        private static extern bool PlaySoundMem(IntPtr pszSound, IntPtr hmod, uint fdwSound);

        internal static int Level
        {
            get { return BtcConfig.UiSound != null ? Mathf.Clamp(BtcConfig.UiSound.Value, 0, 3) : 0; }
            set { if (BtcConfig.UiSound != null) BtcConfig.UiSound.Value = Mathf.Clamp(value, 0, 3); }
        }

        internal static void CycleLevel() { Level = (Level + 1) % 4; Click(); }

        internal static string LevelName
        {
            get
            {
                switch (Level)
                {
                    case 0: return BtcReplayUI.T("UI sounds: off", "Звуки интерфейса: выкл");
                    case 1: return BtcReplayUI.T("UI sounds: low", "Звуки интерфейса: тихо");
                    case 2: return BtcReplayUI.T("UI sounds: medium", "Звуки интерфейса: средне");
                    default: return BtcReplayUI.T("UI sounds: high", "Звуки интерфейса: громко");
                }
            }
        }

        // ------------------------------------------------------------------ public triggers
        internal static void Click() { Play(K.Click, true); }
        internal static void Tab() { Play(K.Tab, true); }
        internal static void Delete() { Play(K.Delete, true); }
        internal static void Ok() { Play(K.Ok, true); }
        internal static void Back() { Play(K.Back, false); }
        internal static void Switch(bool on) { Play(on ? K.On : K.Off, true); }
        internal static void Find() { Play(K.Find, true); }
        internal static void Miss() { Play(K.Miss, true); }
        internal static void Jump() { Play(K.Jump, true); }
        internal static void Warn() { Play(K.Warn, true); }
        internal static void Open() { Play(K.Open, true); }
        internal static void Close() { Play(K.Close, true); }
        internal static void Tick() { Play(K.Tick, false); }

        internal static bool Btn(Rect r, string text, GUIStyle st)
        {
            Hover(r);
            bool c = GUI.Button(r, text, st);
            if (c) Click();
            return c;
        }

        internal static bool Btn(Rect r, GUIContent content, GUIStyle st)
        {
            Hover(r);
            bool c = GUI.Button(r, content, st);
            if (c) Click();
            return c;
        }

        /// <summary>Soft tick when the pointer enters a control (menu open only; Repaint events only).</summary>
        private static float _hoverAt;

        internal static void Hover(Rect r)
        {
            if (Level == 0 || Event.current.type != EventType.Repaint) return;
            long key = ((long)(r.x * 4f) << 40) ^ ((long)(r.y * 4f) << 20) ^ (long)(r.width * 4f) ^ ((long)(r.height * 4f) << 10);
            if (r.Contains(Event.current.mousePosition))
            {
                if (key == _hoverKey) return;
                _hoverKey = key;
                // two overlapping controls under the pointer would alternate every frame: at most one tick per 0.1 s
                if (Time.unscaledTime - _hoverAt < 0.1f) return;
                _hoverAt = Time.unscaledTime;
                if (BtcHotkeys.MenuOpen || BtcReplay.Mode == BtcReplay.State.Playing) Play(K.Hover, false);
            }
            else if (key == _hoverKey) _hoverKey = 0L;
        }

        /// <summary>Keyboard feedback while a text field has the focus (call from a tab's Draw).</summary>
        internal static void Typing(Event e)
        {
            if (Level == 0 || e.type != EventType.KeyDown || GUIUtility.keyboardControl == 0) return;
            if (e.keyCode == KeyCode.Backspace || e.keyCode == KeyCode.Delete) { Back(); return; }
            if (e.character != '\0' && e.character != '\n' && e.character != '\r' && e.character != '\t') Play((K)((int)K.Type0 + Rnd.Next(3)), false);
        }

        // ------------------------------------------------------------------ playback
        private static void Play(K k, bool strong)
        {
            if (_broken) return;
            int lvl = Level;
            if (lvl == 0) return;
            try
            {
                if (Brix.Audio.SoundEngine.mute) return;
                float now = Time.realtimeSinceStartup;
                if (!strong && (now - _lastStrong < 0.09f || now - _lastAny < 0.04f)) return;
                Ensure(lvl);
                byte[] w = Wav[(int)k];
                if (w == null) return;
                _lastAny = now;
                if (strong) _lastStrong = now;
                PlaySoundMem(Pins[(int)k].AddrOfPinnedObject(), IntPtr.Zero, 0x1 | 0x2 | 0x4); // SND_ASYNC | SND_NODEFAULT | SND_MEMORY
            }
            catch (Exception e)
            {
                _broken = true;
                BtcLog.Warn("Sfx", "UI sounds disabled: " + e.Message);
            }
        }

        /// <summary>Builds a pinned WAV buffer for other sound sets (base shields); keep the pointer, the buffer stays pinned for the session.</summary>
        internal static IntPtr Pin(float[] s)
        {
            byte[] b = ToWav(s);
            return GCHandle.Alloc(b, GCHandleType.Pinned).AddrOfPinnedObject();
        }

        /// <summary>Plays a buffer from <see cref="Pin"/> (async, replaces the current sound). Respects the game mute, not the UI sound level.</summary>
        internal static void PlayPtr(IntPtr p)
        {
            if (_broken || p == IntPtr.Zero) return;
            try
            {
                if (Brix.Audio.SoundEngine.mute) return;
                _lastAny = Time.realtimeSinceStartup;
                _lastStrong = _lastAny;
                PlaySoundMem(p, IntPtr.Zero, 0x1 | 0x2 | 0x4);
            }
            catch (Exception e) { _broken = true; BtcLog.Warn("Sfx", "sounds disabled: " + e.Message); }
        }

        private static void Ensure(int lvl)
        {
            if (_built == lvl) return;
            _built = lvl;
            float v = lvl == 1 ? 0.35f : (lvl == 2 ? 0.7f : 1f);
            // minimal and neutral: very short, quiet, dry ticks and soft thuds; one pitch per sound (no melodies, no sweeps), tiny noise tap for texture
            Set(K.Hover, Tick(1800f, 0.012f, 250f, 0.035f * v, 0f, 4000f));
            Set(K.Click, Tick(1100f, 0.028f, 130f, 0.20f * v, 0.05f, 3200f));
            Set(K.Tab, Tick(900f, 0.03f, 120f, 0.18f * v, 0.04f, 2800f));
            Set(K.On, Tick(1300f, 0.034f, 100f, 0.18f * v, 0.03f, 3200f));
            Set(K.Off, Tick(800f, 0.034f, 100f, 0.17f * v, 0.03f, 2800f));
            Set(K.Type0, Tick(650f, 0.018f, 220f, 0.11f * v, 0.07f, 2400f));
            Set(K.Type1, Tick(720f, 0.018f, 220f, 0.11f * v, 0.07f, 2400f));
            Set(K.Type2, Tick(800f, 0.018f, 220f, 0.11f * v, 0.07f, 2400f));
            Set(K.Back, Tick(520f, 0.025f, 160f, 0.09f * v, 0.04f, 2000f));
            Set(K.Delete, Soft(300f, 240f, 0.075f, 45f, 0.2f * v, 0.003f, 1800f));
            Set(K.Ok, Soft(1000f, 1000f, 0.075f, 35f, 0.15f * v, 0.004f, 3200f));
            Set(K.Find, Tick(1200f, 0.03f, 110f, 0.14f * v, 0.03f, 3000f));
            Set(K.Miss, Soft(420f, 420f, 0.045f, 70f, 0.09f * v, 0.003f, 1800f));
            Set(K.Jump, Tick(1400f, 0.042f, 80f, 0.15f * v, 0.03f, 3200f));
            Set(K.Warn, Soft(380f, 380f, 0.065f, 50f, 0.14f * v, 0.003f, 1800f));
            Set(K.Open, Soft(700f, 700f, 0.055f, 50f, 0.1f * v, 0.01f, 2400f));
            Set(K.Close, Soft(560f, 560f, 0.055f, 50f, 0.09f * v, 0.008f, 2200f));
            Set(K.Tick, Tick(1500f, 0.012f, 250f, 0.055f * v, 0f, 3600f));
        }

        /// <summary>A dry tick: a fixed-pitch sine with a very fast decay plus an optional low-passed noise tap, one-pole low-passed.</summary>
        internal static float[] Tick(float f, float dur, float decay, float amp, float noise, float cutoff)
        {
            int n = Mathf.Max(8, (int)(dur * SR));
            float[] s = new float[n];
            double ph = 0.0;
            float a = Mathf.Exp(-2f * Mathf.PI * cutoff / SR), y = 0f;
            System.Random r = new System.Random((int)f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                ph += 2.0 * Math.PI * f / SR;
                float tail = Mathf.Min(1f, (n - i) / (0.005f * SR));
                float env = (1f - Mathf.Exp(-t / 0.0015f)) * Mathf.Exp(-t * decay) * tail;
                float x = (float)Math.Sin(ph) + noise * ((float)r.NextDouble() * 2f - 1f);
                y = (1f - a) * x + a * y;
                s[i] = y * env * amp;
            }
            return s;
        }

        private static void Set(K k, float[] s)
        {
            byte[] b = ToWav(s);
            Wav[(int)k] = b;
            Pins[(int)k] = GCHandle.Alloc(b, GCHandleType.Pinned); // old buffers stay pinned (a few KB), a sound may still be reading them
        }

        /// <summary>A soft sine note: f0 -> f1 glide, smooth attack (att, seconds), exponential decay, tiny 2nd harmonic, one-pole low-pass at cutoff Hz.</summary>
        internal static float[] Soft(float f0, float f1, float dur, float decay, float amp, float att, float cutoff)
        {
            int n = Mathf.Max(8, (int)(dur * SR));
            float[] s = new float[n];
            double ph = 0.0;
            float a = Mathf.Exp(-2f * Mathf.PI * cutoff / SR), y = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float f = Mathf.Lerp(f0, f1, (float)i / n);
                ph += 2.0 * Math.PI * f / SR;
                float tail = Mathf.Min(1f, (n - i) / (0.008f * SR)); // fade the last 8 ms: no click at the end
                float env = (1f - Mathf.Exp(-t / att)) * Mathf.Exp(-t * decay) * tail;
                float x = (float)Math.Sin(ph) + 0.1f * (float)Math.Sin(ph * 2.0);
                y = (1f - a) * x + a * y;
                s[i] = y * env * amp;
            }
            return s;
        }

        internal static float[] Cat(params float[][] parts)
        {
            int n = 0;
            for (int i = 0; i < parts.Length; i++) n += parts[i].Length;
            float[] r = new float[n];
            int o = 0;
            for (int i = 0; i < parts.Length; i++) { Array.Copy(parts[i], 0, r, o, parts[i].Length); o += parts[i].Length; }
            return r;
        }

        private static byte[] ToWav(float[] s)
        {
            int dataLen = s.Length * 2;
            byte[] b = new byte[44 + dataLen];
            Put(b, 0, "RIFF"); Int(b, 4, 36 + dataLen); Put(b, 8, "WAVE"); Put(b, 12, "fmt ");
            Int(b, 16, 16); Short(b, 20, 1); Short(b, 22, 1); Int(b, 24, SR); Int(b, 28, SR * 2); Short(b, 32, 2); Short(b, 34, 16);
            Put(b, 36, "data"); Int(b, 40, dataLen);
            for (int i = 0; i < s.Length; i++) Short(b, 44 + i * 2, (short)Mathf.Clamp(Mathf.RoundToInt(s[i] * 32000f), -32767, 32767));
            return b;
        }

        private static void Put(byte[] b, int o, string t) { for (int i = 0; i < t.Length; i++) b[o + i] = (byte)t[i]; }
        private static void Int(byte[] b, int o, int v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); b[o + 2] = (byte)(v >> 16); b[o + 3] = (byte)(v >> 24); }
        private static void Short(byte[] b, int o, short v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); }
    }
}
