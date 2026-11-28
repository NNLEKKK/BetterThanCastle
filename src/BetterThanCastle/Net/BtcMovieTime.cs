using System;
using System.Globalization;
using Brix.Engine;
using Brix.Game;
using Brix.Game.Components;
using Brix.Game.Network;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Movie sidebar (BtcGhDev.BuildMovie) actions that change the whole match: game speed, pause, 1 s / 0.1 s steps, stop the sun,
    /// time of day, sun speed, fog of war. Single player / no shared editor panel: the vanilla calls, local as before.
    /// Shared editor panel (host rule gen.editor, BC ON, BtcEditorShare.Active): the host decides. A client sends the action with
    /// msg 137 (kind KindMovie), the host applies it, tells everyone (msg 138 notice) and sends the state with msg 144:
    /// the game speed (every peer runs Time.timeScale = host speed, so units, animations, projectiles and effects slow down like
    /// in single player; sounds behave as in single player too), sun speed and fog of war. Pause and steps use the voluntary
    /// pause of BtcNetManager; time of day and the stopped sun travel with vanilla UNetJourneeSync.
    /// Vanilla MP forces ClockConfig to speed 1 and blocks its pause, so nothing of this exists in vanilla multiplayer.
    /// BC OFF: Active is false, nothing is sent or registered.
    /// </summary>
    internal static class BtcMovieTime
    {
        internal const short MsgState = 144;
        private const byte KScale = 0, KSunSpeed = 1, KFog = 2;

        private static float _scale = 1f;          // unpaused game speed of the match (host decides, clients follow)
        private static float _stepUntil = -1f;      // host: real time a step pause comes back
        private static float _nextResend;

        private static bool Shared { get { return BtcEditorShare.Active; } }

        internal static float Speed { get { return _scale; } }

        // ------------------------------------------------------------------ buttons
        /// <summary>A movie sidebar action: op = "speed 0.5", "pause", "step 1", "sun", "day 12.5", "sunspeed 3", "fog".</summary>
        internal static void Do(string op, string note)
        {
            try
            {
                if (!Shared) { Local(op); return; }
                if (!NetworkServer.active) { BtcEditorShare.SendAct(BtcEditorShare.KindMovie, op, note, null, null); return; }
                if (HostApply(op, BtcNetManager.MovieHostConn, User.LocalUser)) BtcEditorShare.AnnounceLocal(BtcEditorShare.MovieMark + note);
            }
            catch (Exception e) { BtcLog.Error("Movie", "Do " + op + ": " + e.Message); }
        }

        private static float Arg(string op)
        {
            int sp = op.IndexOf(' ');
            float v;
            return sp > 0 && float.TryParse(op.Substring(sp + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
        }

        private static string Verb(string op) { int sp = op.IndexOf(' '); return sp > 0 ? op.Substring(0, sp) : op; }

        // single player / not shared: exactly the vanilla sidebar calls
        private static void Local(string op)
        {
            float v = Arg(op);
            switch (Verb(op))
            {
                case "speed": ClockConfig.ToggleClockSpeed(SpeedOf(v)); break;
                case "pause": ClockConfig.Paused = !ClockConfig.Paused; break;
                case "step": if (v < 0.5f) Neo.StepOneTenth(); else Neo.StepOneSecond(); break;
                case "sun": if (Journee.instance != null) Journee.instance.IsPaused = !Journee.instance.IsPaused; break;
                case "day": SetDay(v); break;
                case "sunspeed": if (Journee.instance != null) Journee.instance.sunSpeed = v; break;
                case "fog": LocalFactionVision.AllVisible = !LocalFactionVision.AllVisible; break;
            }
        }

        private static ClockConfig.ClockSpeed SpeedOf(float v)
        {
            if (Mathf.Approximately(v, 0.5f)) return ClockConfig.ClockSpeed.Half;
            if (Mathf.Approximately(v, 0.1f)) return ClockConfig.ClockSpeed.Tenth;
            if (Mathf.Approximately(v, 1.5f)) return ClockConfig.ClockSpeed.Fast;
            if (Mathf.Approximately(v, 5f)) return ClockConfig.ClockSpeed.VeryFast;
            return ClockConfig.ClockSpeed.Default;
        }

        private static void SetDay(float v)
        {
            if (Journee.instance == null) return;
            Journee.instance.SetTime(Mathf.Clamp(v, 0f, 24f));
            if (NetworkServer.active && UNetJourneeSync.instance != null) UNetJourneeSync.instance.ForceUpdateHeure();
        }

        // ------------------------------------------------------------------ host
        /// <summary>Host: apply a movie action of the host or a client (msg 137). False = refused / nothing to tell.</summary>
        internal static bool HostApply(string op, int connId, User who)
        {
            if (!NetworkServer.active || !Shared || BtcGameplay.InLobby || string.IsNullOrEmpty(op)) return false;
            float v = Arg(op);
            string name = "?";
            try { if (who != null) name = who.ScreenName; } catch (Exception) { }
            switch (Verb(op))
            {
                case "speed":
                    if (SpeedOf(v) == ClockConfig.ClockSpeed.Default) return false;
                    _scale = Mathf.Approximately(_scale, v) ? 1f : v;   // like ClockConfig.ToggleClockSpeed: the same button again = normal
                    if (Time.timeScale > 0f) Time.timeScale = _scale;
                    SendAll(KScale, _scale, false);
                    BtcLog.Info("Movie", "speed " + _scale + " by " + name);
                    return true;
                case "pause":
                    _stepUntil = -1f;
                    return BtcNetManager.MoviePause(null, connId, name);
                case "step":
                    if (!BtcNetManager.MoviePause(false, connId, name)) return false;
                    _stepUntil = Time.realtimeSinceStartup + (v < 0.5f ? 0.1f : 1f);
                    _stepConn = connId; _stepName = name;
                    return true;
                case "sun":
                    if (Journee.instance == null) return false;
                    Journee.instance.IsPaused = !Journee.instance.IsPaused;   // UNetJourneeSync sends it (Journee.OnSetIsPaused)
                    return true;
                case "day":
                    SetDay(v);
                    return true;
                case "sunspeed":
                    if (Journee.instance == null) return false;
                    Journee.instance.sunSpeed = Mathf.Clamp(v, 0f, 20f);
                    SendAll(KSunSpeed, Journee.instance.sunSpeed, false);
                    return true;
                case "fog":
                    LocalFactionVision.AllVisible = !LocalFactionVision.AllVisible;
                    SendAll(KFog, 0f, LocalFactionVision.AllVisible);
                    BtcLog.Info("Movie", "fog of war " + (LocalFactionVision.AllVisible ? "OFF" : "ON") + " by " + name);
                    return true;
            }
            BtcLog.Warn("Movie", "unknown action " + op);
            return false;
        }

        private static int _stepConn;
        private static string _stepName;

        private static void SendAll(byte kind, float value, bool flag)
        {
            if (!NetworkServer.active || !BtcNetworkGate.Enabled) return;
            try { NetworkServer.SendToAll(MsgState, new StateMsg { kind = kind, value = value, flag = flag }); }
            catch (Exception e) { BtcLog.Error("Movie", "send: " + e.Message); }
        }

        // ------------------------------------------------------------------ every frame (BtcEditorShare.Tick)
        internal static void Tick()
        {
            if (!Shared)
            {
                // the match / session / editor rule is over: back to normal speed (vanilla MP never leaves speed 1)
                if (_scale != 1f) { _scale = 1f; _stepUntil = -1f; if (Time.timeScale > 0f && !BtcReplay.Active) Time.timeScale = 1f; BtcLog.Info("Movie", "speed reset (panel no longer shared)"); }
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (NetworkServer.active)
            {
                if (_stepUntil > 0f && now >= _stepUntil) { _stepUntil = -1f; BtcNetManager.MoviePause(true, _stepConn, _stepName); }
                // newcomers and rejoiners get the speed too
                if (_scale != 1f && now >= _nextResend) { _nextResend = now + 2f; SendAll(KScale, _scale, false); }
            }
            // pauses of the mod save and restore the speed they found; anything that restored a wrong one is corrected here.
            // A paused game (timeScale 0) is never touched, nor a peer that is still loading the map.
            if (_scale != 1f || !Mathf.Approximately(Time.timeScale, 1f))
            {
                if (Time.timeScale > 0f && !Mathf.Approximately(Time.timeScale, _scale) && !BtcFastSave.Busy && !BtcReplay.Active && BtcNetManager.Live)
                    Time.timeScale = _scale;
            }
        }

        // ------------------------------------------------------------------ client
        internal static void RegisterClient(NetworkClient c)
        {
            _scale = 1f;
            _stepUntil = -1f;
            if (c == null || NetworkServer.active) return;
            c.UnregisterHandler(MsgState);
            c.RegisterHandler(MsgState, OnState);
        }

        internal static void ResetHost() { _scale = 1f; _stepUntil = -1f; }

        private static void OnState(NetworkMessage nm)
        {
            try
            {
                StateMsg m = nm.ReadMessage<StateMsg>();
                if (NetworkServer.active || !BtcNetworkGate.Enabled) return;
                if (m.kind == KScale)
                {
                    float v = Mathf.Clamp(m.value, 0.05f, 5f);
                    if (!Mathf.Approximately(v, _scale)) BtcLog.Info("Movie", "host speed " + v);
                    _scale = v;
                    if (Time.timeScale > 0f && BtcNetManager.Live) Time.timeScale = _scale;
                }
                else if (m.kind == KSunSpeed) { if (Journee.instance != null) Journee.instance.sunSpeed = Mathf.Clamp(m.value, 0f, 20f); }
                else if (m.kind == KFog) LocalFactionVision.AllVisible = m.flag;
            }
            catch (Exception e) { BtcLog.Error("Movie", "OnState: " + e.Message); }
        }

        internal sealed class StateMsg : MessageBase
        {
            internal byte kind;
            internal float value;
            internal bool flag;
            public override void Serialize(NetworkWriter w) { w.Write(kind); w.Write(value); w.Write(flag); }
            public override void Deserialize(NetworkReader r) { kind = r.ReadByte(); value = r.ReadSingle(); flag = r.ReadBoolean(); }
        }
    }
}
