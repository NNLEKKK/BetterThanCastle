using BepInEx.Configuration;

namespace BetterThanCastle
{
    internal static class BtcConfig
    {
        internal static ConfigEntry<int> WindowMode;
        internal static ConfigEntry<bool> SkipIntro;
        internal static ConfigEntry<bool> BetterConnection;
        internal static ConfigEntry<bool> ShowPing;
        internal static ConfigEntry<bool> ShowPauseLabel;
        internal static ConfigEntry<bool> DiagLog;
        internal static ConfigEntry<bool> AnimatorCulling;
        internal static ConfigEntry<bool> LogMove;
        internal static ConfigEntry<bool> LogBuildAi;
        internal static ConfigEntry<bool> LogVis;
        internal static ConfigEntry<bool> LogFaction;
        internal static ConfigEntry<bool> LogFactionDump;
        internal static ConfigEntry<bool> UpdateCheck;
        internal static ConfigEntry<string> UpdateSkipped;
        internal static ConfigEntry<bool> ReplayAutoShare;
        internal static ConfigEntry<bool> StatsEnabled, StatsBar, StatsBarBottom;
        internal static ConfigEntry<float> StatsBarOffset, StatsBarScale, StatsBarOpacity, StatsPanelScale, StatsPanelOpacity;
        internal static ConfigEntry<string> StatsBarList, StatsPanelHidden;

        internal static void Init(ConfigFile cfg)
        {
            BtcDuelConfig.Init(cfg);
            WindowMode = cfg.Bind("Display", "WindowMode", 3, "0 = keep the game setting, 1 = fullscreen, 2 = windowed, 3 = borderless.");
            SkipIntro = cfg.Bind("Display", "SkipIntro", true, "Skip the logo screens on startup (applies on next launch).");
            UpdateCheck = cfg.Bind("Update", "CheckOnStart", true, "Check GitHub Releases at game start and offer to update the mod.");
            UpdateSkipped = cfg.Bind("Update", "SkippedVersion", "", "Version the player chose to skip.");
            BetterConnection = cfg.Bind("Network", "BetterConnection", true,
                "ON: mod network extensions are active (all players need the mod). " +
                "OFF: the mod behaves exactly like vanilla on the wire (play with vanilla clients/hosts).");
            ShowPing = cfg.Bind("Network", "ShowPing", true, "Show ping overlay (BetterConnection only).");
            ShowPauseLabel = cfg.Bind("Network", "ShowPauseLabel", true, "Show the pause label (BetterConnection only).");
            DiagLog = cfg.Bind("Network", "DiagLog", true, "Write network diagnostics to the log.");
            LogMove = cfg.Bind("Logging", "PathBlocks", false, "Write path blocks to BetterThanCastle/qol_blocked.log (debug).");
            LogBuildAi = cfg.Bind("Logging", "BuildAI", false, "Log build AI decisions to btc_netdiag.txt (debug).");
            LogVis = cfg.Bind("Logging", "VisObjects", false, "Log object names and categories of the vision rules (debug).");
            LogFaction = cfg.Bind("Logging", "FactionReplay", false, "Verbose log of the faction replay (debug).");
            LogFactionDump = cfg.Bind("Logging", "FactionDump", false, "Log minimap object positions once after load (debug).");
            ReplayAutoShare = cfg.Bind("Replay", "AutoShare", true, "Host: after a multiplayer match send the recorded replay (.btcr) to every connected BetterConnection client (BetterConnection only).");
            AnimatorCulling = cfg.Bind("Performance", "AnimatorCulling", false,
                "Unit animators skip skeleton/transform updates while off-screen (CullUpdateTransforms). Visual only; needs BetterConnection ON. Measured: no frame-time gain (state machine cost dominates), so OFF by default.");
            StatsEnabled = cfg.Bind("Stats", "Enabled", true, "Match statistics: collecting, bar and panel. Off = nothing is collected, sent or recorded (players see only their own stats; spectators and replays see everyone).");
            StatsBar = cfg.Bind("Stats", "ShowBar", true, "Show the thin match statistics bar.");
            StatsBarBottom = cfg.Bind("Stats", "BarAtBottom", false, "Bar at the bottom of the screen instead of the top.");
            StatsBarOffset = cfg.Bind("Stats", "BarOffset", 0f, "Bar distance from the screen edge (px).");
            StatsBarScale = cfg.Bind("Stats", "BarScale", 1f, "Bar scale.");
            StatsBarOpacity = cfg.Bind("Stats", "BarOpacity", 0.85f, "Bar opacity.");
            StatsPanelScale = cfg.Bind("Stats", "PanelScale", 1f, "Panel scale.");
            StatsPanelOpacity = cfg.Bind("Stats", "PanelOpacity", 0.95f, "Panel opacity.");
            StatsBarList = cfg.Bind("Stats", "BarStats", "0,1,2,3,4,7,9", "Stat indices shown in the bar (see the Overlays tab, section Match statistics).");
            StatsPanelHidden = cfg.Bind("Stats", "PanelHidden", "", "Stat indices hidden in the panel.");
            BtcDisplay.Init();
        }
    }
}
