using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay playback quality of life: hotkeys (configurable in Hotkeys > Replay: Space pause/play, Z slower, X faster, C back 10 s, V forward 10 s)
    /// and "auto-skip pauses" (cfg Replay/SkipPauses, checkbox in the Replay tab): when playback reaches a recorded pause of the advanced timer
    /// it jumps to the end of that pause at once. The same checkbox skips host freezes: while the host hangs nothing is recorded, so
    /// playback would stand still as long; a gap between two records longer than Replay/FreezeGap jumps to the next record.
    /// Pure local playback behaviour, nothing is sent.
    /// </summary>
    internal static class BtcReplayKeys
    {
        internal static void AddKeys()
        {
            const string g = "Replay";
            BtcHotkeys.AddKey(g, "replay_pause", "Replay: pause / play", 0, "Space", false);
            BtcHotkeys.AddKey(g, "replay_slower", "Replay: slower", 0, "Z", false);
            BtcHotkeys.AddKey(g, "replay_faster", "Replay: faster", 0, "X", false);
            BtcHotkeys.AddKey(g, "replay_back", "Replay: back 10 s", 0, "C", false);
            BtcHotkeys.AddKey(g, "replay_fwd", "Replay: forward 10 s", 0, "V", false);
        }

        internal static bool SkipPauses
        {
            get { return BtcConfig.ReplaySkipPauses != null && BtcConfig.ReplaySkipPauses.Value; }
            set { if (BtcConfig.ReplaySkipPauses != null) BtcConfig.ReplaySkipPauses.Value = value; }
        }

        private static bool Pressed(string id, KeyCode[] def)
        {
            KeyCode[] keys = BtcHotkeys.GetKeys(id, def);
            if (keys == null || keys.Length == 0) return false;
            for (int i = 0; i < keys.Length - 1; i++) if (!UnityEngine.Input.GetKey(keys[i])) return false;
            return UnityEngine.Input.GetKeyDown(keys[keys.Length - 1]) && !BtcHotkeys.IsSuppressed(keys);
        }

        private static float _lastSkipFrom = -1f;
        private static long _freezeFrom = -1L;
        private static int _freezeN;
        private static float _freezeS;

        internal static void Tick()
        {
            if (BtcReplay.Mode != BtcReplay.State.Playing) { _lastSkipFrom = -1f; return; }
            if (Pressed("replay_pause", new[] { KeyCode.Space })) BtcReplay.TogglePause();
            if (Pressed("replay_slower", new[] { KeyCode.Z })) BtcReplay.SpeedStep(-1);
            if (Pressed("replay_faster", new[] { KeyCode.X })) BtcReplay.SpeedStep(1);
            float pc = BtcReplay.Seeking ? BtcReplay.SeekTarget : BtcReplay.PlayClock;
            if (Pressed("replay_back", new[] { KeyCode.C })) BtcReplay.SeekTo(Mathf.Max(0f, pc - 10f));
            if (Pressed("replay_fwd", new[] { KeyCode.V })) BtcReplay.SeekTo(pc + 10f);
            if (SkipPauses && !BtcReplay.Seeking) SkipTick();
        }

        private static void SkipTick()
        {
            float pc = BtcReplay.PlayClock;
            if (pc < _lastSkipFrom) _lastSkipFrom = -1f;
            for (int i = 0; i < BtcTimer.Play.Count; i++)
            {
                BtcTimer.TlEv z = BtcTimer.Play[i];
                if (z.K != 'Z' || z.A - z.T < 0.5f) continue;
                if (pc >= z.T && pc < z.A - 0.1f && !Mathf.Approximately(_lastSkipFrom, z.T))
                {
                    _lastSkipFrom = z.T;   // one jump per pause: stepping back into it by hand is allowed
                    BtcReplay.SeekTo(z.A);
                    return;
                }
            }
            FreezeTick(pc);
        }

        private static void FreezeTick(float pc)
        {
            if (BtcReplay.PlayPaused || BtcConfig.ReplayFreezeGap == null) return;
            long next;
            if (!BtcReplay.NextRecordMs(out next)) return;
            long last = BtcReplay.FeedMs;
            float gap = (next - last) / 1000f;
            // inside a gap only: the clock is past the last record and the next one is more than FreezeGap after it
            if (gap < Mathf.Max(0.3f, BtcConfig.ReplayFreezeGap.Value) || pc * 1000f < last || last == _freezeFrom) return;
            _freezeFrom = last;
            _freezeN++;
            _freezeS += gap;
            BtcLog.Info("Replay", "host freeze skipped: " + (last / 1000f).ToString("0.0") + " -> " + (next / 1000f).ToString("0.0") + " s (" + gap.ToString("0.0")
                + " s; total " + _freezeN + ", " + _freezeS.ToString("0") + " s)");
            BtcReplay.SeekTo(next / 1000f);
        }
    }
}
