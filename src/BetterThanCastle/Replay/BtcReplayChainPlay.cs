using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Playback of a match made of several sessions (BtcReplayChain). One session file is open at a time; the bar shows the whole line of the match
    /// (global time = sessions laid end to end, each one up to the save the next one started from). Crossing a joint opens the next file through the
    /// same path the snapshot restart uses (menu scene + StartReplay, the loading cover stays up) and then jumps by keyframe. On the first open the
    /// sessions without a keyframe cache are prepared one after another, so later jumps between sessions are a reload plus an instant seek.
    /// The open session keeps its own replay clock: every in-world system (pings, cursors, timer) works as for a single replay.
    /// </summary>
    internal static class BtcReplayChainPlay
    {
        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static string _rootId, _lineKey;
        private static BtcReplayChain.Match _m;
        private static BtcReplayChain.Line _l;
        private static int _ver = -1;
        private static int _i;                    // open step
        private static float _seekLocal = -1f;    // local seek once the open session is ready
        private static float _readyWait;
        private static bool _switching;
        private static float _switchAt;
        private static float _speed = 1f;
        private static bool _paused;
        private static bool _pruned;
        private static readonly List<int> _prep = new List<int>();   // steps still to prepare, in order
        private static int _prepTotal, _prepDone;
        private static int _targetStep;
        private static float _targetLocal;

        internal static bool Active { get { return _m != null && _l != null; } }
        internal static bool Preparing { get { return Active && _prepTotal > 0 && (_prep.Count > 0 || _prepDone < _prepTotal); } }
        internal static bool Switching { get { return Active && (_switching || BtcReplay.RestartPending); } }
        internal static BtcReplayChain.Match M { get { return _m; } }
        internal static BtcReplayChain.Line L { get { return _l; } }
        internal static int Step { get { return _i; } }
        internal static int PrepIndex { get { return _prepDone + 1; } }
        internal static int PrepTotal { get { return _prepTotal; } }

        internal static float Off { get { return Active ? _l.Off(_i) : 0f; } }
        internal static float End { get { return Active ? _l.S[_i].End : BtcReplay.PlayTotal; } }

        /// <summary>Bar clock: global time on the shown line (the local replay clock for a single replay).</summary>
        internal static float Clock
        {
            get
            {
                float pc = BtcReplay.Seeking ? BtcReplay.SeekTarget : BtcReplay.PlayClock;
                if (!Active) return pc;
                if (Switching || _seekLocal >= 0f) return _l.Off(_i) + Mathf.Max(0f, _seekLocal);
                return _l.Off(_i) + Mathf.Min(pc, _l.S[_i].End);
            }
        }

        internal static float Total { get { return Active ? _l.Total : BtcReplay.PlayTotal; } }

        internal static bool IsMain { get { return Active && _m.Main != null && _l.Key == _m.Main.Key; } }

        private static void Clear(string why)
        {
            if (_m != null) BtcLog.Info("ReplayChain", "match playback ended: " + why);
            _m = null; _l = null; _rootId = null; _lineKey = null;
            _prep.Clear(); _prepTotal = 0; _prepDone = 0;
            _seekLocal = -1f; _switching = false; _pruned = false;
            BtcChainStats.OnStep();
        }

        /// <summary>Library: play a match line from global time g (0 = the start).</summary>
        internal static void Start(BtcReplayChain.Match m, BtcReplayChain.Line l, float g)
        {
            if (m == null) return;
            if (l == null) l = m.Main;
            if (!BtcReplay.CanStartReplay()) { BtcReplay.Say(Tt("Leave the current game first.", "Сначала выйдите из текущей игры.")); return; }
            Clear("restart");
            _m = m; _l = l; _rootId = m.Root.Id; _lineKey = l.Key; _ver = BtcReplayChain.Version;
            _speed = 1f; _paused = false;
            _targetStep = l.StepAt(Mathf.Clamp(g, 0f, l.Total));
            _targetLocal = Mathf.Max(0f, g - l.Off(_targetStep));
            // sessions without keyframes are prepared first (a reload each), so later jumps between sessions are instant
            if (BtcReplaySeek.Enabled)
                for (int k = 0; k < l.S.Count; k++)
                    if (k != _targetStep && l.S[k].N.Dur >= 20f && !BtcReplaySeek.HasCacheFor(l.S[k].N.D.FilePath)) _prep.Add(k);
            _prepTotal = _prep.Count;
            _prepDone = 0;
            BtcLog.Info("ReplayChain", "match " + m.Root.Id + ": " + l.S.Count + " sessions, " + BtcReplayChain.Clock(l.Total) + ", start at step " + _targetStep + " +" + _targetLocal.ToString("0.0", CultureInfo.InvariantCulture) + " s, prepare " + _prepTotal);
            int first = _prep.Count > 0 ? _prep[0] : _targetStep;
            _i = first;
            _seekLocal = _prep.Count > 0 ? -1f : _targetLocal;
            _readyWait = Time.unscaledTime;
            _pruned = false;
            BtcReplay.StartReplay(l.S[first].N.D);
            if (BtcReplay.Mode != BtcReplay.State.Playing) Clear("start failed");
        }

        /// <summary>Exit button / Esc while the cover is up between two sessions.</summary>
        internal static void Stop()
        {
            BtcReplay.CancelRestart();
            Clear("stopped");
            if (BtcReplay.Mode == BtcReplay.State.Playing) BtcReplay.StopReplay();
        }

        private static void Open(int step, float local)
        {
            if (step == _i && BtcReplay.Mode == BtcReplay.State.Playing && !Switching)
            {
                BtcReplay.SeekTo(Mathf.Clamp(local, 0f, Mathf.Max(0f, _l.S[step].End - 0.1f)));
                return;
            }
            // another session while one is open: the same in-place swap as at a joint, then a keyframe seek inside it
            if (!_forceReload && BtcReplay.Mode == BtcReplay.State.Playing && !Switching && _i >= 0 && _i < _l.S.Count && BtcReplay.FeedGo && SwapNow(step))
            {
                float t = Mathf.Clamp(local, 0f, Mathf.Max(0f, _l.S[step].End - 0.1f));
                if (t > BtcReplay.PlayClock + 0.2f || t < BtcReplay.PlayClock - 0.2f) BtcReplay.SeekTo(t);
                return;
            }
            _forceReload = false;
            _speed = BtcReplay.PlaySpeed;
            _paused = BtcReplay.PlayPaused;
            _i = step;
            _seekLocal = Mathf.Max(0f, local);
            _switching = true;
            _switchAt = Time.unscaledTime;
            _pruned = false;
            BtcReplay.ChainSwitch(_l.S[step].N.D);
        }

        /// <summary>Seek on the bar / marks / keys, in global time of the shown line.</summary>
        internal static void Seek(float g)
        {
            if (!Active) { BtcReplay.SeekTo(g); return; }
            if (Preparing) return;
            g = Mathf.Clamp(g, 0f, _l.Total);
            int j = _l.StepAt(g);
            Open(j, g - _l.Off(j));
        }

        /// <summary>Show another line (a branch or back to the main line); keeps the moment when it is shared, else starts where the line leaves.</summary>
        internal static void ShowLine(BtcReplayChain.Line l, float atG)
        {
            if (!Active || l == null || l == _l) return;
            int curStep = _i;
            BtcReplayChain.Node curNode = _l.S[curStep].N;
            float local = BtcReplay.PlayClock;
            _l = l; _lineKey = l.Key;
            // same session at the same index: stay in it without a reload
            if (curStep < l.S.Count && l.S[curStep].N == curNode && local <= l.S[curStep].End && atG < 0f) { BtcSfx.Click(); BtcChainStats.OnStep(); return; }
            BtcSfx.Jump();
            float g = atG >= 0f ? atG : 0f;
            int j = l.StepAt(g);
            if (curStep < l.S.Count && l.S[curStep].N == curNode && j == curStep) { _i = curStep; BtcChainStats.OnStep(); BtcReplay.SeekTo(g - l.Off(j)); return; }
            _i = -1;
            Open(j, g - l.Off(j));
        }

        // ------------------------------------------------------------------ per frame (BtcReplay.Tick)
        internal static void Tick()
        {
            if (_m == null) return;
            try
            {
                if (_ver != BtcReplayChain.Version) Resolve();
                if (_m == null) return;
                bool playing = BtcReplay.Mode == BtcReplay.State.Playing;
                if (!playing)
                {
                    if (BtcReplay.RestartPending) return;
                    if (_switching && Time.unscaledTime - _switchAt < 3f) return;
                    Clear("replay closed");
                    return;
                }
                if (_i < 0 || _i >= _l.S.Count || !string.Equals(BtcReplay.FeedFile, _l.S[_i].N.D.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (_switching && Time.unscaledTime - _switchAt < 3f) return;
                    Clear("another replay opened");
                    return;
                }
                _switching = false;
                if (BtcReplay.Loading || !BtcReplay.FeedGo) return;
                if (!_pruned) { _pruned = true; Prune(); if (_prep.Count == 0) BtcChainStats.OnStep(); }
                bool ready = BtcReplaySeek.Ready || BtcReplaySeek.WillNotPrepare || Time.unscaledTime - _readyWait > 180f;
                if (_prep.Count > 0)
                {
                    if (!ready) return;
                    BtcChainApm.Capture(_l.S[_i].N.D);
                    _prep.RemoveAt(0);
                    _prepDone++;
                    _readyWait = Time.unscaledTime;
                    if (_prep.Count > 0) { BtcLog.Info("ReplayChain", "prepared, next session to prepare: step " + _prep[0]); OpenPrep(_prep[0]); }
                    else { BtcLog.Info("ReplayChain", "all sessions prepared, opening step " + _targetStep); _prepTotal = 0; _prepDone = 0; if (_targetStep == _i) { _seekLocal = _targetLocal; } else Open(_targetStep, _targetLocal); }
                    return;
                }
                if (_seekLocal >= 0f)
                {
                    if (!ready) return;
                    float t = _seekLocal;
                    _seekLocal = -1f;
                    BtcChainApm.Capture(_l.S[_i].N.D);
                    if (t > 0.3f) BtcReplay.SeekTo(Mathf.Clamp(t, 0f, Mathf.Max(0f, _l.S[_i].End - 0.1f)));
                    BtcReplay.ChainSpeed(_speed, _paused);
                    return;
                }
                // the joint: the next session continues from the save made here
                if (_i + 1 < _l.S.Count && !BtcReplay.Seeking)
                {
                    StartPreload(_i + 1);
                    float pc = BtcReplay.PlayClock;
                    if (pc >= _l.S[_i].End - 0.05f || BtcReplay.Finished)
                    {
                        BtcLog.Info("ReplayChain", "joint reached at " + pc.ToString("0.0", CultureInfo.InvariantCulture) + " s: next session");
                        if (!TrySeamless(_i + 1)) Open(_i + 1, 0f);
                    }
                }
                else if (_i + 1 >= _l.S.Count && !BtcReplay.Seeking && !BtcReplay.PlayPaused && BtcReplay.PlayClock >= _l.S[_i].End - 0.05f && _l.S[_i].End < BtcReplay.PlayTotal - 0.5f)
                    BtcReplay.TogglePause();   // a line that ends before its file does (the rest is another branch): stop at the end of the line
            }
            catch (Exception e) { BtcLog.Error("ReplayChain", "tick: " + e); Clear("error"); }
        }

        // ------------------------------------------------------------------ seamless joint (no reload)
        /// <summary>Replay settings switch: cross a joint without loading the world again (falls back to the reload when it cannot).</summary>
        internal static bool SeamlessOn { get { return BtcMenuState.Bool("replay:seamless", true); } set { BtcMenuState.SetBool("replay:seamless", value); } }

        private sealed class Pre
        {
            internal string File;
            internal volatile bool Done;
            internal byte[] Stream, Pov;
            internal BtcReplaySeek.SessionCache Cache;
            internal string Err;
        }

        private static Pre _pre;
        private static bool _forceReload;

        /// <summary>Reads the next session (stream + keyframe cache) on a worker thread while this one plays.</summary>
        private static void StartPreload(int step)
        {
            if (!SeamlessOn || step >= _l.S.Count) return;
            BtcReplay.Data d = _l.S[step].N.D;
            if (_pre != null && _pre.File == d.FilePath) return;
            Pre p = new Pre { File = d.FilePath };
            _pre = p;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    byte[] s, v;
                    if (BtcReplay.ReadStream(d, out s, out v)) { p.Stream = s; p.Pov = v; }
                    p.Cache = BtcReplaySeek.ReadSession(d.FilePath);
                    if (p.Cache == null) p.Err = "no keyframe cache";
                }
                catch (Exception e) { p.Err = e.Message; }
                p.Done = true;
            });
        }

        /// <summary>Seek into another session: read it now (stream + keyframe cache, a short hitch instead of a reload) and swap in place.</summary>
        private static bool SwapNow(int step)
        {
            if (!SeamlessOn) return false;
            BtcReplay.Data d = _l.S[step].N.D;
            if (_pre == null || _pre.File != d.FilePath || !_pre.Done)
            {
                if (!BtcReplaySeek.HasCacheFor(d.FilePath)) return false;
                Pre p = new Pre { File = d.FilePath };
                try
                {
                    byte[] s, v;
                    if (BtcReplay.ReadStream(d, out s, out v)) { p.Stream = s; p.Pov = v; }
                    p.Cache = BtcReplaySeek.ReadSession(d.FilePath);
                    if (p.Cache == null) p.Err = "no keyframe cache";
                }
                catch (Exception e) { p.Err = e.Message; }
                p.Done = true;
                _pre = p;
            }
            return TrySeamless(step);
        }

        private static bool TrySeamless(int step)
        {
            if (!SeamlessOn) return false;
            Pre p = _pre;
            BtcReplay.Data d = _l.S[step].N.D;
            if (p == null || p.File != d.FilePath) { BtcLog.Info("ReplayChain", "seamless joint: next session not preloaded, reloading"); return false; }
            if (!p.Done) { BtcLog.Info("ReplayChain", "seamless joint: next session still loading, reloading"); return false; }
            if (p.Err != null || p.Stream == null || p.Cache == null) { BtcLog.Info("ReplayChain", "seamless joint not possible (" + (p.Err ?? "no stream") + "), reloading"); return false; }
            _pre = null;
            float at;
            bool ok;
            try { ok = BtcReplaySeek.SwapSession(d, p.Cache, p.Stream, p.Pov, out at); }
            catch (Exception e) { BtcLog.Error("ReplayChain", "seamless joint: " + e); ok = false; at = 0f; }
            if (!ok)
            {
                // the world may be half way: the reload puts the next session in place cleanly
                BtcLog.Warn("ReplayChain", "seamless joint failed, reloading: " + BtcReplaySeek.LastSwap);
                _forceReload = true;   // the caller's Open must reload, never try the swap again on this world
                return false;
            }
            _i = step;
            _pruned = false;
            _seekLocal = -1f;
            AfterSwap();
            BtcLog.Info("ReplayChain", "seamless joint into session " + (step + 1) + " at " + at.ToString("0.0", CultureInfo.InvariantCulture) + " s");
            return true;
        }

        /// <summary>The replay viewer is the recording ghost "REC" of the new session; spectator factions and score panel are fixed again.</summary>
        private static void AfterSwap()
        {
            try
            {
                Brix.Game.User[] us = BtcRegistry.UsersArray();
                for (int k = 0; k < us.Length; k++)
                {
                    Brix.Game.User u = us[k];
                    if (u == null) continue;
                    if (BtcStr.Plain(u.ScreenName) == "REC") { Brix.Game.User.LocalUser = u; break; }
                }
                Brix.Game.Faction.DefaultNeutral = null;   // a cached faction of the old session (capture clocks read it); found again by name
                BtcFactionFix.Reset();
            }
            catch (Exception e) { BtcLog.Error("ReplayChain", "after seamless joint: " + e.Message); }
        }

        private static void OpenPrep(int step)
        {
            _i = step;
            _seekLocal = -1f;
            _switching = true;
            _switchAt = Time.unscaledTime;
            _pruned = false;
            BtcReplay.ChainSwitch(_l.S[step].N.D);
        }

        /// <summary>The library rebuilt the matches (a link changed): find the same match and line again.</summary>
        private static void Resolve()
        {
            _ver = BtcReplayChain.Version;
            BtcReplay.Data cur = _l != null && _i >= 0 && _i < _l.S.Count ? _l.S[_i].N.D : null;
            BtcReplayChain.Match m = cur != null ? BtcReplayChain.Of(cur) : null;
            if (m == null) { Clear("the match was unlinked"); return; }
            BtcReplayChain.Line l = m.LineByKey(_lineKey) ?? m.Main;
            int idx = -1;
            for (int k = 0; k < l.S.Count; k++) if (l.S[k].N.D.FileName == cur.FileName) { idx = k; break; }
            if (idx < 0) { l = m.Main; for (int k = 0; k < l.S.Count; k++) if (l.S[k].N.D.FileName == cur.FileName) { idx = k; break; } }
            if (idx < 0) { Clear("the open session left the line"); return; }
            _m = m; _l = l; _i = idx; _rootId = m.Root.Id; _lineKey = l.Key;
        }

        /// <summary>The open file plays past the joint only as another branch: its marks and timer events after the joint are not on this line.</summary>
        private static void Prune()
        {
            float end = _l.S[_i].End;
            if (end >= BtcReplay.PlayTotal - 0.05f) return;
            int a = BtcAutoMarks.Play.RemoveAll(delegate (BtcAutoMarks.AEv e) { return e.T > end; });
            for (int k = 0; k < BtcAutoMarks.Play.Count; k++) { BtcAutoMarks.AEv e = BtcAutoMarks.Play[k]; if (e.A > end) { e.A = end; BtcAutoMarks.Play[k] = e; } }
            int b = BtcTimer.Play.RemoveAll(delegate (BtcTimer.TlEv e) { return e.T > end; });
            for (int k = 0; k < BtcTimer.Play.Count; k++) { BtcTimer.TlEv e = BtcTimer.Play[k]; if (e.K == 'Z' && e.A > end) { e.A = end; BtcTimer.Play[k] = e; } }
            if (a + b > 0) BtcLog.Info("ReplayChain", "line ends at " + end.ToString("0.0", CultureInfo.InvariantCulture) + " s: " + a + " marks, " + b + " timer events after it hidden");
        }
    }
}
