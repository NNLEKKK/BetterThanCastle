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
        internal static ConfigEntry<bool> ReplayAutoShare, ReplayDownloadFromHost, ChatFade;
        internal static ConfigEntry<float> ChatFadeSeconds;
        internal static ConfigEntry<bool> PovShowCone;
        internal static ConfigEntry<int> ReplayUnitDots;
        internal static ConfigEntry<bool> ReplaySkipPauses, ReplayAutoMarks, ReplayMinimapFights, ReplayRecordPov, PovFollowCam, PovShowCursor, ReplayInfo;
        internal static ConfigEntry<string> ReplayHiddenKinds, ReplayUiLayout;
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
            ReplayDownloadFromHost = cfg.Bind("Replay", "DownloadFromHost", true, "Client: accept the replay the host sends after a multiplayer match and save it to Replays (file name carries the host name; BetterConnection only).");
            ChatFade = cfg.Bind("Chat", "FadeMessages", true, "In-game chat: hide messages after FadeSeconds; opening the chat shows the history.");
            ChatFadeSeconds = cfg.Bind("Chat", "FadeSeconds", 10f, new ConfigDescription("Seconds an in-game chat message stays visible.", new AcceptableValueRange<float>(2f, 60f)));
            ReplaySkipPauses = cfg.Bind("Replay", "SkipPauses", false, "Replay playback: jump over the recorded pauses of the advanced timer at once.");
            ReplayAutoMarks = cfg.Bind("Replay", "AutoMarks", true, "Record automatic timeline marks (deaths, crystals, barrels, large fights) into new replays.");
            ReplayMinimapFights = cfg.Bind("Replay", "MinimapFights", true, "Highlight combat on the mini map while watching a replay.");
            ReplayHiddenKinds = cfg.Bind("Replay", "HiddenMarkKinds", "", "Timeline mark kinds hidden by the player (D C X F Z).");
            ReplayRecordPov = cfg.Bind("Replay", "RecordCameraCursor", true, "Record player cameras and cursors into new replays (BetterConnection clients send them to the host, ~300 B/s each).");
            PovFollowCam = cfg.Bind("Replay", "PovFollowCamera", true, "Watching a player in a replay: follow their recorded camera.");
            PovShowCursor = cfg.Bind("Replay", "ShowCursors", true, "Replay: draw the recorded cursors of the players.");
            ReplayInfo = cfg.Bind("Replay", "ShowBlueprintsAndQueues", true, "Replay: draw the blueprints of all players and the crafting queues of all buildings.");
            PovShowCone = cfg.Bind("Replay", "ShowViewCones", true, "Replay: draw the field of view of every player as a translucent pyramid over the map.");
            ReplayUnitDots = cfg.Bind("Replay", "UnitOwnerDots", 1, "Replay: coloured owner dot over every unit. 0 off, 1 only while Alt is held, 2 always.");
            ReplayUiLayout = cfg.Bind("Replay", "UiLayout", "", "Replay player UI layout (scale, position, visibility); edited in the replay with F7.");
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
