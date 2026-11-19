using System;
using BepInEx.Configuration;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Stand still, strafe left-right, wander one by one, or move as one pack in changing directions (most like a real fight).</summary>
    internal enum BtcAimMove { Stand = 0, Strafe, Wander, Pack }

    /// <summary>Target class = movement speed: knight with the shield up (defensive, slowest), knight, worker, halberdier (fastest).</summary>
    internal enum BtcAimUnit { KnightShield = 0, Knight, Worker, Halberdier }

    /// <summary>0 Focus: one wounded target at a time. 1 Most wounded: several drop at once, click the lowest one.</summary>
    internal enum BtcAimGame { Focus = 0, Triage }

    /// <summary>Aim trainer settings (Practice tab, page Aim training). Changes apply at the next run (Again reads them).
    /// Defaults = the author's own setup (2026-11-19).</summary>
    internal static class BtcAimConfig
    {
        internal const int MinTargets = 2, MaxTargets = 24;
        internal const float MinSpacing = 1.5f, MaxSpacing = 8f;
        internal const float MinHeight = 8f, MaxHeight = 1000f;
        internal const float MinPitch = 40f, MaxPitch = 90f;
        internal const int MinDuration = 15, MaxDuration = 300;
        internal const int MinWound = 10, MaxWound = 100, MinStep = 1, MaxStep = 25;

        internal const int DefTargets = 9, DefDuration = 15, DefWound = 60, DefStep = 25;
        internal const float DefSpacing = 4f, DefHeight = 30f;

        internal static int Targets = DefTargets;
        /// <summary>Blocks between neighbouring targets.</summary>
        internal static float Spacing = DefSpacing;
        /// <summary>Camera distance from the floor centre (blocks).</summary>
        internal static float Height = DefHeight;
        /// <summary>Camera pitch in degrees, 90 = straight down.</summary>
        internal static float Pitch = 90f;
        internal static float Yaw = 0f;
        internal static int Duration = DefDuration;
        internal static BtcAimMove Move = BtcAimMove.Stand;
        internal static BtcAimUnit Unit = BtcAimUnit.Halberdier;
        internal static BtcAimGame Game = BtcAimGame.Focus;
        /// <summary>Most wounded: share of the targets that drop every round (percent).</summary>
        internal static int WoundPct = DefWound;
        /// <summary>Most wounded: HP step between wounded targets (percent of max HP).</summary>
        internal static int StepPct = DefStep;
        /// <summary>Fraction of max HP a target drops to in the Focus game.</summary>
        internal const float LowHp = 0.25f;

        private static ConfigEntry<int> _cTargets, _cDuration, _cMove, _cUnit, _cGame, _cWound, _cStep;
        private static ConfigEntry<float> _cSpacing, _cHeight, _cPitch, _cYaw;
        private static ConfigEntry<bool> _cEmpty;

        /// <summary>Load the trainer's own empty map (fast). Turned off by itself when sandbox gave no faction there; then the default map is used.</summary>
        internal static bool EmptyMap { get { return _cEmpty == null || _cEmpty.Value; } set { if (_cEmpty != null) _cEmpty.Value = value; } }

        internal static void Init(ConfigFile cfg)
        {
            _cTargets = cfg.Bind("Practice", "AimTargets", DefTargets, "Aim trainer: targets on the floor (2-24).");
            _cSpacing = cfg.Bind("Practice", "AimSpacing", DefSpacing, "Aim trainer: blocks between neighbouring targets (1.5-8).");
            _cHeight = cfg.Bind("Practice", "AimCameraDistance", DefHeight, "Aim trainer: camera distance from the floor (8-1000 blocks).");
            _cPitch = cfg.Bind("Practice", "AimCameraPitch", 90f, "Aim trainer: camera angle, 90 = straight down (40-90).");
            _cYaw = cfg.Bind("Practice", "AimCameraYaw", 0f, "Aim trainer: camera turn around the floor (degrees).");
            _cDuration = cfg.Bind("Practice", "AimDuration", DefDuration, "Aim trainer: run length in seconds (15-300).");
            _cMove = cfg.Bind("Practice", "AimMove", 0, "Aim trainer: 0 targets stand, 1 strafe left-right, 2 wander one by one, 3 move as one pack.");
            _cUnit = cfg.Bind("Practice", "AimUnit", (int)BtcAimUnit.Halberdier, "Aim trainer: target class (= speed) 0 knight with shield up, 1 knight, 2 worker, 3 halberdier.");
            _cGame = cfg.Bind("Practice", "AimGame", 0, "Aim trainer: 0 Focus (one wounded target), 1 Most wounded (several drop, click the lowest).");
            _cWound = cfg.Bind("Practice", "AimWoundedPercent", DefWound, "Most wounded: share of the targets that drop each round (10-100 %).");
            _cStep = cfg.Bind("Practice", "AimHpStepPercent", DefStep, "Most wounded: HP step between wounded targets (1-25 % of max HP).");
            _cEmpty = cfg.Bind("Practice", "AimEmptyMap", true, "Aim trainer: load its own empty map (fast). Set to false by itself when the empty map does not work; then the default map is used.");
            Targets = _cTargets.Value; Spacing = _cSpacing.Value; Height = _cHeight.Value; Pitch = _cPitch.Value; Yaw = _cYaw.Value;
            Duration = _cDuration.Value; Move = (BtcAimMove)_cMove.Value; Unit = (BtcAimUnit)_cUnit.Value;
            Game = (BtcAimGame)_cGame.Value; WoundPct = _cWound.Value; StepPct = _cStep.Value;
            Clamp();
        }

        internal static void Clamp()
        {
            Targets = Mathf.Clamp(Targets, MinTargets, MaxTargets);
            Spacing = Mathf.Clamp(Spacing, MinSpacing, MaxSpacing);
            Height = Mathf.Clamp(Height, MinHeight, MaxHeight);
            Pitch = Mathf.Clamp(Pitch, MinPitch, MaxPitch);
            Yaw = Mathf.Repeat(Yaw, 360f);
            Duration = Mathf.Clamp(Duration, MinDuration, MaxDuration);
            if ((int)Move < 0 || (int)Move > 3) Move = BtcAimMove.Stand;
            if ((int)Unit < 0 || (int)Unit > 3) Unit = BtcAimUnit.Halberdier;
            if ((int)Game < 0 || (int)Game > 1) Game = BtcAimGame.Focus;
            WoundPct = Mathf.Clamp(WoundPct, MinWound, MaxWound);
            StepPct = Mathf.Clamp(StepPct, MinStep, MaxStep);
        }

        internal static void Save()
        {
            Clamp();
            if (_cTargets == null) return;
            _cTargets.Value = Targets; _cSpacing.Value = Spacing; _cHeight.Value = Height; _cPitch.Value = Pitch; _cYaw.Value = Yaw;
            _cDuration.Value = Duration; _cMove.Value = (int)Move; _cUnit.Value = (int)Unit;
            _cGame.Value = (int)Game; _cWound.Value = WoundPct; _cStep.Value = StepPct;
        }

        /// <summary>The author's setup (Reset button).</summary>
        internal static void Defaults()
        {
            Targets = DefTargets; Spacing = DefSpacing; Height = DefHeight; Pitch = 90f; Yaw = 0f; Duration = DefDuration;
            Move = BtcAimMove.Stand; Unit = BtcAimUnit.Halberdier; WoundPct = DefWound; StepPct = DefStep;
        }

        /// <summary>Camera distance step: finer up close, coarser far away (the range goes up to 1000 blocks).</summary>
        internal static float HeightStep(float h, bool shift) { float s = h < 40f ? 2f : (h < 150f ? 10f : 50f); return shift ? s * 5f : s; }

        /// <summary>Wounded targets per round in the Most wounded game (at least 2, at most all).</summary>
        internal static int WoundedCount(int targets, int pct) { return Mathf.Clamp(Mathf.RoundToInt(targets * pct / 100f), 2, targets); }

        /// <summary>Settings key for personal bests: runs only compare with runs of the same setup. Halberdier targets add nothing (old keys stay valid).</summary>
        internal static string Key(Snap s)
        {
            return "n" + s.Targets + "_s" + s.Spacing.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "_h" + Mathf.RoundToInt(s.Height) +
                   "_p" + Mathf.RoundToInt(s.Pitch) + "_t" + s.Duration + "_m" + s.Move + (s.Unit == (int)BtcAimUnit.Halberdier ? "" : "_u" + s.Unit) +
                   (s.Game == (int)BtcAimGame.Triage ? "_g1_w" + s.Wound + "_st" + s.Step : "");
        }

        internal struct Snap { internal int Targets, Duration, Move, Unit, Game, Wound, Step; internal float Spacing, Height, Pitch, Yaw; }

        internal static Snap Take()
        {
            return new Snap { Targets = Targets, Duration = Duration, Move = (int)Move, Unit = (int)Unit, Spacing = Spacing, Height = Height, Pitch = Pitch, Yaw = Yaw,
                              Game = (int)Game, Wound = WoundPct, Step = StepPct };
        }

        internal static void Put(Snap s)
        {
            Targets = s.Targets; Duration = s.Duration; Move = (BtcAimMove)s.Move; Unit = (BtcAimUnit)s.Unit; Spacing = s.Spacing; Height = s.Height; Pitch = s.Pitch; Yaw = s.Yaw;
            Game = (BtcAimGame)s.Game; WoundPct = s.Wound; StepPct = s.Step;
            Save();
        }
    }
}
