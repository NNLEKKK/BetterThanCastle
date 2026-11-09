using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Brix.Game;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay side of the advanced timer. Recording host: events (timer start, peace end, game end, pauses, player marks) are collected with the replay
    /// clock and written into the reserved "tl=" header region when the recording stops (header size never changes, old readers ignore the line).
    /// Playback: events are parsed at load; the score panel clock follows them (Elapsed = start offset + replay clock - pauses), the replay timeline
    /// shows them (ticks, pause spans, coloured player marks that can be hidden per player). Marks are placed 10 s before the key press.
    /// Wire: BC ON clients ask the host with msg 88 kind "K" (colour), rate-limited; nothing else is sent for replays.
    /// </summary>
    internal static partial class BtcTimer
    {
        internal const int PadLen = 8192;
        private const float MarkBack = 10f;
        private const float MarkCooldown = 2f;
        private const int MarkCap = 40;

        internal struct TlEv
        {
            internal char K;      // S start, P peace end, L game end, Z pause span, M mark
            internal float T, A, B, D;
            internal string Col;
        }

        private static readonly List<TlEv> Rec = new List<TlEv>();
        internal static readonly List<TlEv> Play = new List<TlEv>();
        internal static readonly HashSet<string> HiddenCols = new HashSet<string>();
        private static readonly Dictionary<int, float> MarkLast = new Dictionary<int, float>();
        private static readonly Dictionary<string, int> MarkCount = new Dictionary<string, int>();
        private static bool _playLogged, _panelLogged;
        private static bool _peaceDone, _limitDone, _pauseOpen;
        private static float _lastMark = -99f;

        // ------------------------------------------------------------------ recording
        internal static string HeaderLine() { return "tl=" + new string(' ', PadLen) + "\n"; }

        internal static void OnRecordStart()
        {
            BtcAutoMarks.OnRecordStart();
            BtcPovRec.OnRecordStart();
            BtcApm.OnRecordStart();
            Rec.Clear();
            MarkLast.Clear();
            MarkCount.Clear();
            _pauseOpen = false;
            _peaceDone = PeaceSec > 0f && Elapsed >= PeaceSec;
            _limitDone = LimitSec > 0f && Elapsed >= LimitSec;
            if (Host && Cur == Phase.Running) AddStart();
        }

        private static void AddStart()
        {
            float t = BtcReplay.RecTime;
            if (t < 0f) return;
            Rec.Add(new TlEv { K = 'S', T = t, A = Elapsed, B = PeaceSec, D = LimitSec });
        }

        private static void RecStarted()
        {
            _peaceDone = PeaceSec > 0f && Elapsed >= PeaceSec;
            _limitDone = LimitSec > 0f && Elapsed >= LimitSec;
            _pauseOpen = false;
            AddStart();
        }

        /// <summary>Host, every frame while the timer runs: peace end, game end, pause spans.</summary>
        private static void RecTick()
        {
            float t = BtcReplay.RecTime;
            if (t < 0f) return;
            if (Cur != Phase.Running) return;
            if (!_peaceDone && PeaceSec > 0f && Elapsed >= PeaceSec) { _peaceDone = true; Rec.Add(new TlEv { K = 'P', T = t }); }
            if (!_limitDone && LimitSec > 0f && Elapsed >= LimitSec) { _limitDone = true; Rec.Add(new TlEv { K = 'L', T = t }); }
            bool paused = Time.timeScale < 0.01f;
            if (paused && !_pauseOpen) { _pauseOpen = true; Rec.Add(new TlEv { K = 'Z', T = t, A = -1f }); }
            else if (!paused && _pauseOpen)
            {
                _pauseOpen = false;
                for (int i = Rec.Count - 1; i >= 0; i--)
                    if (Rec[i].K == 'Z') { TlEv e = Rec[i]; e.A = t; Rec[i] = e; break; }
            }
        }

        internal static string TimelinePayload()
        {
            StringBuilder sb = new StringBuilder(512);
            sb.Append(BtcPovRec.TlToken());
            string apm = BtcApm.Payload();
            if (apm.Length > 0) { if (sb.Length > 0) sb.Append(';'); sb.Append(apm); }
            // structure events first, marks last (dropped first when the region is full)
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1)
                {
                    string auto = BtcAutoMarks.Payload(PadLen - sb.Length - 1);
                    if (auto.Length > 0) { if (sb.Length > 0) sb.Append(';'); sb.Append(auto); }
                }
                for (int i = 0; i < Rec.Count; i++)
                {
                    TlEv e = Rec[i];
                    if ((e.K == 'M') != (pass == 1)) continue;
                    string s = Enc(e);
                    if (sb.Length + s.Length + 1 > PadLen) return sb.ToString();
                    if (sb.Length > 0) sb.Append(';');
                    sb.Append(s);
                }
            }
            return sb.ToString();
        }

        private static string Enc(TlEv e)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            switch (e.K)
            {
                case 'S': return "S," + e.T.ToString("F1", c) + "," + e.A.ToString("F1", c) + "," + e.B.ToString("F0", c) + "," + e.D.ToString("F0", c);
                case 'Z': return "Z," + e.T.ToString("F1", c) + "," + (e.A < 0f ? e.T : e.A).ToString("F1", c);
                case 'M': return "M," + e.T.ToString("F1", c) + "," + e.Col;
                default: return e.K + "," + e.T.ToString("F1", c);
            }
        }

        // ------------------------------------------------------------------ marks (hotkey)
        private static void MarkKeyTick()
        {
            KeyCode[] keys = BtcHotkeys.GetKeys("timer_mark", new KeyCode[0]);
            if (keys == null || keys.Length == 0) return;
            for (int i = 0; i < keys.Length - 1; i++) if (!UnityEngine.Input.GetKey(keys[i])) return;
            if (!UnityEngine.Input.GetKeyDown(keys[keys.Length - 1]) || BtcHotkeys.IsSuppressed(keys)) return;
            PlaceMark();
        }

        private static string LocalColHex()
        {
            try
            {
                User u = User.LocalUser;
                if (u != null && u.faction != null) return ColorUtility.ToHtmlStringRGB(u.faction.FactionColor);
            }
            catch { }
            return "FFFFFF";
        }

        internal static void PlaceMark()
        {
            if (Cur == Phase.None) return;
            float now = Time.unscaledTime;
            if (now - _lastMark < MarkCooldown) return;
            _lastMark = now;
            string col = LocalColHex();
            if (Host) AddMark(-1, col);
            else SendToHost("K|" + col);
            BtcToast.Show(BtcGameplay.T("Replay mark placed", "Метка в реплее поставлена"));
        }

        private static void AddMark(int conn, string col)
        {
            float t = BtcReplay.RecTime;
            if (t < 0f) return;
            float last;
            if (MarkLast.TryGetValue(conn, out last) && Time.unscaledTime - last < MarkCooldown * 0.9f) return;
            MarkLast[conn] = Time.unscaledTime;
            int n;
            MarkCount.TryGetValue(col, out n);
            if (n >= MarkCap) return;
            MarkCount[col] = n + 1;
            Rec.Add(new TlEv { K = 'M', T = Mathf.Max(0f, t - MarkBack), Col = col });
        }

        /// <summary>Host: a client mark request (msg 88 kind K).</summary>
        private static void HostMark(int conn, string col)
        {
            if (col == null || col.Length != 6) return;
            for (int i = 0; i < 6; i++) if (!Uri.IsHexDigit(col[i])) return;
            AddMark(conn, col.ToUpperInvariant());
        }

        // ------------------------------------------------------------------ playback
        internal static void LoadTl(string s)
        {
            BtcAutoMarks.ClearPlay();
            BtcApm.ClearPlay();
            Play.Clear();
            _markColsN = -1;
            HiddenCols.Clear();
            _playLogged = false;
            _panelLogged = false;
            if (string.IsNullOrEmpty(s)) return;
            try
            {
                CultureInfo c = CultureInfo.InvariantCulture;
                string[] items = s.Trim().Split(';');
                for (int i = 0; i < items.Length; i++)
                {
                    string[] p = items[i].Split(',');
                    if (p.Length < 2 || p[0].Length != 1) continue;
                    if (BtcApm.TryParse(p, c)) continue;
                    if (BtcAutoMarks.TryParse(p, c)) continue;
                    TlEv e = new TlEv { K = p[0][0], T = float.Parse(p[1], c) };
                    if (e.K == 'S' && p.Length >= 5) { e.A = float.Parse(p[2], c); e.B = float.Parse(p[3], c); e.D = float.Parse(p[4], c); }
                    else if (e.K == 'Z' && p.Length >= 3) e.A = float.Parse(p[2], c);
                    else if (e.K == 'M' && p.Length >= 3) e.Col = p[2];
                    else if (e.K != 'P' && e.K != 'L') continue;
                    Play.Add(e);
                }
                BtcAutoMarks.SortPlay();
            }
            catch (Exception ex) { BtcLog.Error("Timer", "LoadTl: " + ex.Message); Play.Clear(); }
        }

        /// <summary>Replay playback: drive Cur/Elapsed/PeaceSec/LimitSec from the loaded events.</summary>
        private static void PlayTick()
        {
            MouseOver = false;
            FreezeEnergy = false;
            int si = -1;
            for (int i = 0; i < Play.Count; i++) if (Play[i].K == 'S') { si = i; break; }
            if (si < 0) { Cur = Phase.None; return; }
            TlEv s = Play[si];
            float pc = BtcReplay.Seeking ? BtcReplay.SeekTarget : BtcReplay.PlayClock;
            PeaceSec = s.B;
            LimitSec = s.D;
            if (pc < s.T) { Cur = Phase.Waiting; Elapsed = s.A; return; }
            if (!_playLogged) { _playLogged = true; BtcLog.Info("Timer", "replay clock follows tl events (S at " + s.T.ToString("F1") + ", " + Play.Count + " events)"); }
            Cur = Phase.Running;
            Elapsed = ElapsedAt(pc, s);
        }

        private static float ElapsedAt(float pc, TlEv s)
        {
            float el = s.A + (pc - s.T);
            for (int i = 0; i < Play.Count; i++)
            {
                TlEv z = Play[i];
                if (z.K != 'Z' || z.T >= pc) continue;
                float end = Mathf.Min(z.A, pc);
                if (z.T >= s.T && end > z.T) el -= end - z.T;
            }
            return Mathf.Max(0f, el);
        }

        /// <summary>Replay UI: advanced timer reading at replay position pc ("-m:ss" before the start), null when the replay has no timer.</summary>
        internal static string ClockAt(float pc)
        {
            for (int i = 0; i < Play.Count; i++)
            {
                if (Play[i].K != 'S') continue;
                if (pc < Play[i].T) return "-" + Clock(Play[i].T - pc);
                return Clock(ElapsedAt(pc, Play[i]));
            }
            return null;
        }

        // ------------------------------------------------------------------ timeline drawing
        private static void Box2(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, BtcUiStyle.White);
            GUI.color = old;
        }

        private static Color ParseCol(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString("#" + hex, out c) ? c : Color.white;
        }

        /// <summary>Replay panel: bar = timeline rect, h = row height unit, panel = whole replay panel.</summary>
        internal static void DrawTimeline(Rect bar, float h, float total, Rect panel)
        {
            if (Play.Count == 0) return;
            Event e = Event.current;
            List<string> cols = null;
            for (int i = 0; i < Play.Count; i++)
            {
                TlEv v = Play[i];
                float x = bar.x + bar.width * Mathf.Clamp01(v.T / total);
                switch (v.K)
                {
                    case 'Z':
                        {
                            float w = Mathf.Max(3f, bar.width * Mathf.Clamp01((v.A - v.T) / total));
                            Box2(new Rect(x, bar.y, w, bar.height), new Color(1f, 0.6f, 0.1f, 0.75f));
                            break;
                        }
                    case 'S': Box2(new Rect(x - 1.5f, bar.y - h * 0.15f, 3f, bar.height + h * 0.3f), new Color(1f, 1f, 1f, 0.95f)); break;
                    case 'P': Box2(new Rect(x - 1.5f, bar.y - h * 0.15f, 3f, bar.height + h * 0.3f), new Color(0.35f, 0.9f, 0.4f, 0.95f)); break;
                    case 'L': Box2(new Rect(x - 1.5f, bar.y - h * 0.15f, 3f, bar.height + h * 0.3f), new Color(1f, 0.25f, 0.2f, 0.95f)); break;
                    case 'M':
                        if (cols == null) cols = new List<string>();
                        if (!cols.Contains(v.Col)) cols.Add(v.Col);
                        if (HiddenCols.Contains(v.Col)) break;
                        Color mc = ParseCol(v.Col);
                        Box2(new Rect(x - 1f, bar.y - h * 0.1f, 2f, bar.height + h * 0.2f), mc);
                        Box2(new Rect(x - h * 0.16f, bar.y - h * 0.34f, h * 0.32f, h * 0.24f), mc);
                        break;
                }
            }
        }

        /// <summary>Distinct colours of the player marks of the loaded replay (their hide chips live in the shared chip row of BtcAutoMarks).</summary>
        private static readonly List<string> _markCols = new List<string>();
        private static int _markColsN = -1;

        internal static List<string> MarkColors()
        {
            if (_markColsN == Play.Count) return _markCols;
            _markColsN = Play.Count;
            _markCols.Clear();
            for (int i = 0; i < Play.Count; i++)
                if (Play[i].K == 'M' && !_markCols.Contains(Play[i].Col)) _markCols.Add(Play[i].Col);
            return _markCols;
        }
    }
}
