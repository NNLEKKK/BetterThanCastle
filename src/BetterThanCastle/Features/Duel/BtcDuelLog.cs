using System;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Per-match duel log: &lt;game&gt;/BetterThanCastle/duel-logs/duel-YYYYMMDD-HHMMSS-N.txt. One line per event "t|TAG|...", t = seconds since fight start.
    /// Tags: CFG settings, ORD order (side,unit,kind,target), HIT dealt damage (attacker,victim,dmg,interval ms, SKIP flag when below the vanilla cycle),
    /// KILL, ZONE enter/leave, MODE escape/trade/space timers, SNAP 5 Hz positions+hp, BOT decision reasons, END result + summary.
    /// Written in Release too (feature logic). Buffered, flushed every 2 s and at match end. Not for per-frame allocations: callers use the *Fmt helpers sparingly.
    /// </summary>
    internal static class BtcDuelLog
    {
        private static StreamWriter _w;
        private static float _t0;
        private static float _lastFlush;
        private static int _matchNo;
        internal static string CurrentPath;
        internal static bool Open { get { return _w != null; } }
        /// <summary>Log BORD lines (every order the bot / auto-player sends through BtcDuel.Attack/Move/MoveAttack).</summary>
        internal static bool BotOrders = true;

        internal static void Begin()
        {
            End();
            try
            {
                string dir = Path.Combine(Path.Combine(Paths.GameRootPath, "BetterThanCastle"), "duel-logs");
                string sub = Environment.GetEnvironmentVariable("BTC_LOGDIR");          // parallel test instances write to their own sub folder
                if (!string.IsNullOrEmpty(sub) && sub.IndexOfAny(new[] { '\\', '/', ':' }) < 0) dir = Path.Combine(dir, sub);
                Directory.CreateDirectory(dir);
                TrimOld(dir, 40);
                _matchNo++;
                CurrentPath = Path.Combine(dir, "duel-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + _matchNo + ".txt");
                _w = new StreamWriter(CurrentPath, false, new UTF8Encoding(false));
                _t0 = Time.time; _lastFlush = Time.realtimeSinceStartup;
            }
            catch (Exception e) { _w = null; BtcLog.Warn("Duel", "log open failed: " + e.Message); }
        }

        internal static void SetT0() { _t0 = Time.time; }

        internal static void Line(string tag, string msg)
        {
            if (_w == null) return;
            try
            {
                _w.Write((Time.time - _t0).ToString("0.00")); _w.Write('|'); _w.Write(tag); _w.Write('|'); _w.WriteLine(msg);
                if (Time.realtimeSinceStartup - _lastFlush > 2f) { _w.Flush(); _lastFlush = Time.realtimeSinceStartup; }
            }
            catch (Exception) { _w = null; }
        }

        internal static void End()
        {
            if (_w == null) return;
            try { _w.Flush(); _w.Dispose(); } catch (Exception) { }
            _w = null;
        }

        private static void TrimOld(string dir, int keep)
        {
            try
            {
                string[] f = Directory.GetFiles(dir, "duel-*.txt");
                if (f.Length <= keep) return;
                Array.Sort(f, StringComparer.Ordinal);
                for (int i = 0; i < f.Length - keep; i++) File.Delete(f[i]);
            }
            catch (Exception) { }
        }
    }
}
