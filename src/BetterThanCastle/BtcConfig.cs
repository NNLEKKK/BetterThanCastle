using BepInEx.Configuration;

namespace BetterThanCastle
{
    internal static class BtcConfig
    {
        internal static ConfigEntry<int> WindowMode;
        internal static ConfigEntry<bool> SkipIntro, NoLoadCurtain;
        internal static ConfigEntry<int> ConfigRev;
        internal static ConfigEntry<bool> LoadTimer;
        internal static ConfigEntry<bool> LoadCancel;
        internal static ConfigEntry<bool> LiveSymmetry;
        internal static ConfigEntry<bool> CleanHanging;
        internal static ConfigEntry<int> UiSound;
        internal static ConfigEntry<bool> ReuseMenuPanels;
        internal static ConfigEntry<bool> EarlyClientUi;
        internal static ConfigEntry<bool> BetterConnection;
        internal static ConfigEntry<bool> ShowPing;
        internal static ConfigEntry<bool> ShowPauseLabel;
        internal static ConfigEntry<bool> DiagLog;
        internal static ConfigEntry<bool> AnimSync;
        internal static ConfigEntry<bool> PosSync;
        internal static ConfigEntry<bool> SoundSync;
        internal static ConfigEntry<bool> FogSounds;
        internal static ConfigEntry<bool> OwnTransport;
        internal static ConfigEntry<bool> AnimatorCulling;
        internal static ConfigEntry<bool> CrowdRepath, FastOverheads;
        internal static ConfigEntry<bool> MemoryFixes, FrameSavers, FastSave, FastLoad, ThreadedTerrain, SkipModeIntro, ReplayNoGameHud, CsGameMenu, BackgroundPatching;
        internal static ConfigEntry<bool> WarmShaders, UnitNightLights, FireflyLights, CrystalShadows, PauseHiddenAnims, HideNameTags, StaticCrystals, SimpleFireflies, CrystalLights, ShortShadows, NoAmbientOcclusion, NoBloom, NoSunShadows;
        internal static ConfigEntry<bool> LogMove;
        internal static ConfigEntry<bool> LogBuildAi;
        internal static ConfigEntry<bool> LogVis;
        internal static ConfigEntry<bool> LogFaction;
        internal static ConfigEntry<bool> LogFactionDump;
        internal static ConfigEntry<bool> UpdateCheck;
        internal static ConfigEntry<string> UpdateSkipped;
        internal static ConfigEntry<bool> OnlineOn, OnlineShowIp, OnlineAllowJoin, OnlineInvites, OnlineChat, OnlineChatNotify;
        internal static ConfigEntry<string> OnlineId, OnlineMyIp, OnlineManual;
        internal static ConfigEntry<int> OnlinePort;
        internal static ConfigEntry<bool> ReplayAutoShare, ReplayDownloadFromHost, ChatFade;
        internal static ConfigEntry<float> ChatFadeSeconds;
        internal static ConfigEntry<bool> PovShowCone;
        internal static ConfigEntry<int> ReplayUnitDots;
        internal static ConfigEntry<bool> ReplaySkipPauses, ReplayAutoMarks, ReplayMinimapFights, ReplayRecordPov, PovFollowCam, PovShowCursor, ReplayInfo, ReplayHideMinimap, ReplayShowApm;
        internal static ConfigEntry<int> ReplayKeysMode, ReplayApmMode, GifWidth, GifFps, GifColors;
        internal static ConfigEntry<bool> GifDither, GifDiff;
        internal static ConfigEntry<string> ReplayHiddenKinds, ReplayUiLayout;
        internal static ConfigEntry<float> ReplayFreezeGap;
        internal static ConfigEntry<bool> StatsEnabled, StatsBar, StatsBarBottom;
        internal static ConfigEntry<float> StatsBarOffset, StatsBarScale, StatsBarOpacity, StatsPanelScale, StatsPanelOpacity;
        internal static ConfigEntry<string> StatsBarList, StatsPanelHidden;

        internal static void Init(ConfigFile cfg)
        {
            BtcDuelConfig.Init(cfg);
            BtcAimConfig.Init(cfg);
            WindowMode = cfg.Bind("Display", "WindowMode", 3, "0 = keep the game setting, 1 = fullscreen, 2 = windowed, 3 = borderless.");
            SkipIntro = cfg.Bind("Display", "SkipIntro", true, "Skip the logo screens on startup (applies on next launch).");
            NoLoadCurtain = cfg.Bind("Display", "NoLoadCurtain", false, "No loading curtain: the black halves that slide over the screen are hidden, so the world is seen loading in real time. Progress and tips stay. Local only, applies at once.");
            UiSound = cfg.Bind("Display", "UiSound", 2, "Sound feedback of the mod menu (hover, clicks, typing, delete): 0 = off, 1 = low, 2 = medium, 3 = high.");
            LiveSymmetry = cfg.Bind("Editor", "LiveSymmetry", true, "Map editor: when a symmetry mode is set in Gameplay settings (mirror/point/quadrants), objects, trees and Island terrain you place are mirrored instantly to the other side. Uses the map Heightmap.png for the axis.");
            CleanHanging = cfg.Bind("Editor", "CleanHanging", true, "Map editor: after you dig or lower terrain, ore, plants, boulders and trees that no longer sit on the surface are removed automatically.");
            CsGameMenu = cfg.Bind("Display", "CsGameMenu", true, "The in-game interface is built in C# instead of the game Lua menu (much less memory, shorter freezes, faster loads). Off = vanilla Lua menu.");
            LoadTimer = cfg.Bind("Display", "LoadTimer", true, "Show how many seconds the world load has been running (and the total after it).");
            LoadCancel = cfg.Bind("Display", "LoadCancel", true, "Show a cross at the bottom right while a world loads; it aborts the load and returns to the main menu.");
            ReuseMenuPanels = cfg.Bind("LoadingExperimental", "ReuseMenuPanels", false, "EXPERIMENTAL (off since 0.5.0, loads were slower with it). While the world loads, reuse already built menu panels instead of destroying and re-creating them (faster load). Turn off if the in-game UI looks wrong.");
            EarlyClientUi = cfg.Bind("LoadingExperimental", "EarlyClientUi", false, "EXPERIMENTAL (off since 0.5.0: host and clients load together like 0.3.0). MP client: build the in-game UI while the host finishes loading. Off = vanilla order.");
            UpdateCheck = cfg.Bind("Update", "CheckOnStart", true, "Check GitHub Releases at game start and offer to update the mod.");
            UpdateSkipped = cfg.Bind("Update", "SkippedVersion", "", "Version the player chose to skip.");
            OnlineOn = cfg.Bind("Online", "Enabled", true, "Online tab: show mod players on your networks (Radmin, Hamachi, LAN) and show yourself to them (own UDP port, not the game connection).");
            OnlineId = cfg.Bind("Online", "Id", "", "Random id of this player in the Online list (made once).");
            OnlinePort = cfg.Bind("Online", "Port", 47781, "UDP port of the Online list (all players must use the same one).");
            OnlineShowIp = cfg.Bind("Online", "ShowIp", true, "Show your game address (Radmin / LAN IP) to other mod players.");
            OnlineMyIp = cfg.Bind("Online", "MyIp", "", "Address shown to others instead of the detected one (empty = auto: Radmin/Hamachi first, then LAN).");
            OnlineManual = cfg.Bind("Online", "ExtraAddresses", "", "Extra addresses to ask directly (comma separated), for networks that drop broadcasts.");
            OnlineAllowJoin = cfg.Bind("Online", "AllowJoin", true, "Others can join your lobby (and your running BetterConnection match) from the Online list.");
            OnlineInvites = cfg.Bind("Online", "Invites", true, "Accept invites from hosts (a card with a Join button).");
            OnlineChat = cfg.Bind("Online", "Chat", true, "Online chat with other mod players.");
            OnlineChatNotify = cfg.Bind("Online", "ChatNotify", true, "Notify about new messages in the common chat (direct messages always notify).");
            BetterConnection = cfg.Bind("Network", "BetterConnection", true,
                "ON: mod network extensions are active (all players need the mod). " +
                "OFF: the mod behaves exactly like vanilla on the wire (play with vanilla clients/hosts).");
            ShowPing = cfg.Bind("Network", "ShowPing", true, "Show ping overlay (BetterConnection only).");
            ShowPauseLabel = cfg.Bind("Network", "ShowPauseLabel", true, "Show the pause label (BetterConnection only).");
            AnimSync = cfg.Bind("Network", "SmoothAnimations", true, "Clients see unit animations (strikes, hits, steps) like the host, without the vanilla 10 Hz sampling and lost updates; no extra delay. Host: sends them to clients that ask; client: asks the host. BetterConnection only.");
            PosSync = cfg.Bind("Network", "SmoothMovement", true, "Clients get unit positions at 20 Hz with the host clock and interpolate with an adaptive delay (vanilla 6 Hz lerp). BetterConnection only, both sides.");
            SoundSync = cfg.Bind("Network", "UnitSounds", true, "Clients hear unit sounds like the host: the host sends unit jobs (right voice after a kit change) and the sounds only its own code plays (aiming, spotting, arrow hits, kills, racks). BetterConnection only, both sides.");
            FogSounds = cfg.Bind("Network", "NoFogSounds", true, "Nobody hears objects their faction cannot see (fog of war; vanilla host hears everything). BetterConnection only, local.");
            OwnTransport = cfg.Bind("Network", "OwnTransport2", true, "Reliable UNET traffic goes through the mod's own delivery over the unreliable channel (never gives up, separate lanes). BetterConnection only, both sides (negotiated per connection, a peer without it keeps vanilla delivery). Key renamed in 0.7.1 so everyone starts with it ON.");
            DiagLog = cfg.Bind("Network", "DiagLog2", false, "Write detailed network diagnostics files (BetterThanCastle/debug/unet_netdiag_*.log, max 20 MB per session). Normal [Net] lines in the BepInEx log are always written.");
            LogMove = cfg.Bind("Logging", "PathBlocks", false, "Write path blocks to BetterThanCastle/qol_blocked.log (debug).");
            LogBuildAi = cfg.Bind("Logging", "BuildAI", false, "Log build AI decisions to btc_netdiag.txt (debug).");
            LogVis = cfg.Bind("Logging", "VisObjects", false, "Log object names and categories of the vision rules (debug).");
            LogFaction = cfg.Bind("Logging", "FactionReplay", false, "Verbose log of the faction replay (debug).");
            LogFactionDump = cfg.Bind("Logging", "FactionDump", false, "Log minimap object positions once after load (debug).");
            ReplayAutoShare = cfg.Bind("Replay", "AutoShare", true, "Host: after a multiplayer match send the recorded replay (.btcr) to every connected BetterConnection client (BetterConnection only).");
            ReplayDownloadFromHost = cfg.Bind("Replay", "DownloadFromHost", true, "Client: accept the replay the host sends after a multiplayer match and save it to Replays (file name carries the host name; BetterConnection only).");
            ChatFade = cfg.Bind("Chat", "FadeMessages", true, "In-game chat: hide messages after FadeSeconds; opening the chat shows the history.");
            ChatFadeSeconds = cfg.Bind("Chat", "FadeSeconds", 10f, new ConfigDescription("Seconds an in-game chat message stays visible.", new AcceptableValueRange<float>(2f, 60f)));
            ReplaySkipPauses = cfg.Bind("Replay", "SkipPauses", false, "Replay playback: jump over the recorded pauses of the advanced timer and over host freezes (record gaps) at once.");
            ReplayFreezeGap = cfg.Bind("Replay", "FreezeGap", 1f, "With SkipPauses: a gap without any recorded message longer than this (seconds) counts as a host freeze and is skipped.");
            ReplayAutoMarks = cfg.Bind("Replay", "AutoMarks", true, "Record automatic timeline marks (deaths, crystals, barrels, large fights) into new replays.");
            ReplayMinimapFights = cfg.Bind("Replay", "MinimapFights", true, "Highlight combat on the mini map while watching a replay.");
            ReplayHiddenKinds = cfg.Bind("Replay", "HiddenMarkKinds", "", "Timeline mark kinds hidden by the player (D C X F Z).");
            ReplayRecordPov = cfg.Bind("Replay", "RecordCameraCursor", true, "Record player cameras and cursors into new replays (BetterConnection clients send them to the host, ~300 B/s each).");
            PovFollowCam = cfg.Bind("Replay", "PovFollowCamera", true, "Watching a player in a replay: follow their recorded camera.");
            PovShowCursor = cfg.Bind("Replay", "ShowCursors", true, "Replay: draw the recorded cursors of the players.");
            ReplayInfo = cfg.Bind("Replay", "ShowBlueprintsAndQueues", true, "Replay: draw the blueprints of all players and the crafting queues of all buildings.");
            ReplayNoGameHud = cfg.Bind("Replay", "NoGameHud", true, "Replay playback: the game HUD (task bar, task and build panels) is not loaded at all: faster replay loads, much less memory and shorter freezes. The mini map is shown on its own.");
            ReplayHideMinimap = cfg.Bind("Replay", "HideMinimap", false, "Replay: hide the mini map (bar icon).");
            ReplayShowApm = cfg.Bind("Replay", "ShowApm", true, "Replay: draw the actions-per-minute graph of every player above the timeline (replays recorded since this feature only).");
            ReplayKeysMode = cfg.Bind("Replay", "PressedKeysMode", 1, "Replay: pressed keys of the watched player (0 hidden, 1 feed, 2 keyboard).");
            ReplayApmMode = cfg.Bind("Replay", "ApmGraphMode", 1, "Replay: graph above the timeline (0 hidden, 1 APM, 2 EPM without spam).");
            GifWidth = cfg.Bind("Replay", "GifWidth", 480, "Replay clip GIF: width in pixels (height follows the screen aspect).");
            GifFps = cfg.Bind("Replay", "GifFps", 12, "Replay clip GIF: frames per second.");
            GifColors = cfg.Bind("Replay", "GifColors", 0, "Replay clip GIF: palette size, 0 = 252 colours, 1 = 125, 2 = 64, 3 = 27 (fewer = smaller file).");
            GifDither = cfg.Bind("Replay", "GifDither", true, "Replay clip GIF: ordered dithering (nicer gradients, bigger file).");
            GifDiff = cfg.Bind("Replay", "GifOptimize", true, "Replay clip GIF: skip pixels that did not change since the previous frame (much smaller file for calm scenes).");
            PovShowCone = cfg.Bind("Replay", "ShowViewCones", true, "Replay: draw the field of view of every player as a translucent pyramid over the map.");
            ReplayUnitDots = cfg.Bind("Replay", "UnitOwnerDots", 1, "Replay: coloured owner dot over every unit. 0 off, 1 only while Alt is held, 2 always.");
            ReplayUiLayout = cfg.Bind("Replay", "UiLayout", "", "Replay player UI layout (scale, position, visibility); edited in the replay with F7.");
            AnimatorCulling = cfg.Bind("Performance", "AnimatorCulling", true,
                "Unit animators skip skeleton/transform updates while off-screen (CullUpdateTransforms). Visual only; needs BetterConnection ON. Measured: no frame-time gain (state machine cost dominates), so OFF by default.");
            MemoryFixes = cfg.Bind("Performance", "MemoryFixes", true, "Fewer and shorter freezes from garbage collection: smaller Lua heap and stacks, fixes of game memory leaks, no dead analytics (ported from Castle Story Plus). Local only, nothing changes on the wire. Applies after a restart.");
            CrowdRepath = cfg.Bind("Performance", "CrowdRepath", true, "Units blocked by other units in a crowd do not repeat a detour search that just failed (same place, same goal) for 0.6 s: removes the regular hitches of big crowds. Host / single player with BetterConnection. Applies after a restart.");
            FastOverheads = cfg.Bind("Performance", "FastOverheads", true, "Icons, name tags and thought bubbles over units are drawn by the mod from the vanilla overheads (same look): moving the camera no longer moves every shown overhead (about 10 ms per frame with 50 shown). Local only, switches at once.");
            FrameSavers = cfg.Bind("Performance", "FrameSavers", true, "Less CPU per frame for the same game: work the game repeated for the same answer is done once (overhead icons, sound positions, input names, animator states, picking rays...; ported from Castle Story Plus). Local only, nothing changes on the wire or in the simulation. Applies after a restart.");
            FastSave = cfg.Bind("Performance", "FastSave", true, "Saving a running game no longer freezes it: quicksave and the save menu write the game objects a slice per frame, and the big data files are written by a background thread into a new folder that then replaces the old save (instead of copying the whole save back and forth). Ported from Castle Story Plus. Local only; applies at once.");
            FastLoad = cfg.Bind("Performance", "FastLoad", true, "Faster world load: the terrain is drawn in a few frames instead of one step per frame, loader waits are checked every frame, the loading curtain is faster, and the serializer is warmed up in the background so the replay snapshot at match start no longer freezes the game (~1.6 s). Ideas partly from Castle Story Plus. Local only, nothing changes on the wire. Applies after a restart.");
            BackgroundPatching = cfg.Bind("Performance", "BackgroundPatching", true, "Faster game start: the mod hooks that are only needed in a world are installed on a background thread while the game loads its menu (about 4-5 s less to the main menu). A world load waits for them if they are not done yet. Applies after a restart.");
            ThreadedTerrain = cfg.Bind("Performance", "ThreadedTerrain", true, "Build the terrain on several CPU threads: the two heaviest terrain passes (levels/config and normals/UVs) are split over the cores, bit-identical result, ~0.7 s faster world load. Local only, nothing changes on the wire. Applies at once.");
            SkipModeIntro = cfg.Bind("Performance", "SkipModeIntro", true, "Skip the intro window of sandbox, conquest and invasion: its button action runs at once (conquest/invasion single player timers start as after the click). The tutorial keeps it. Local only. Applies at the next match.");
            // key renamed from WarmShaders (default was ON in 0.6.0): the new key starts OFF for everyone, the first world load is 1.6 s shorter
            WarmShaders = cfg.Bind("Performance", "PrepareShaders", false, "Compile the shaders once per game start (about 2 s pause in the main menu, 1.6 s longer first world load) instead of in the middle of a match: the first night, fireflies or effects then no longer freeze the game for up to half a second. Local only.");
            UnitNightLights = cfg.Bind("Performance", "UnitNightLights", false, "Lanterns of your units at night (one point light per unit). Off = darker units at night, less GPU work. Local only.");
            FireflyLights = cfg.Bind("Performance", "FireflyLights", false, "Glow lights of the night fireflies around crystals (dozens of small point lights). Off = fireflies keep their particles, less GPU work. Local only.");
            CrystalShadows = cfg.Bind("Performance", "CrystalShadows", false, "Crystal lights cast shadows (point light shadows: six extra shadow renders per crystal). Off = crystals still glow, much less GPU work. Local only.");
            PauseHiddenAnims = cfg.Bind("Performance", "PauseHiddenAnims", true, "Overhead icons, name tags and thought bubbles animate only while shown; crystal shards only on screen; beacon handles only right after a change. Looks the same. Local only.");
            HideNameTags = cfg.Bind("Performance", "HideNameTags", false, "Never show the name tags over units and beacons (maximum performance). Local only.");
            // one-time default changes: a config written by an older version keeps its old value, so the new default is set once here
            ConfigRev = cfg.Bind("Meta", "ConfigRev", 0, "Internal: which one-time default changes are applied. Do not edit.");
            if (ConfigRev.Value < 1) { HideNameTags.Value = false; ConfigRev.Value = 1; }
            StaticCrystals = cfg.Bind("Performance", "StaticCrystals", false, "Crystals do not animate at all (no breathing, shaking, capture fill or day glow); their colour changes only when a capture or neutralisation completes. The capture progress bar stays. Local only.");
            SimpleFireflies = cfg.Bind("Performance", "SimpleFireflies", true, "Fireflies without their particles and trail (about 16 particles each, dozens of fireflies at every home crystal); the glowing ball stays. Local only.");
            CrystalLights = cfg.Bind("Performance", "CrystalLights", false, "The light that crystals cast on the ground around them. Off = crystals still glow themselves, fewer lights to draw. Local only.");
            ShortShadows = cfg.Bind("Performance", "ShortShadows", true, "Sun and moon shadows drawn up to 120 m from the camera instead of 400 m: far fewer shadow renders, sharper shadows nearby. Local only.");
            NoAmbientOcclusion = cfg.Bind("Performance", "NoAmbientOcclusion", true, "Turn off ambient occlusion (soft contact shadows) whatever the game graphics option says. Local only.");
            NoBloom = cfg.Bind("Performance", "NoBloom", true, "Turn off bloom (glow around bright things) whatever the game graphics option says. Local only.");
            NoSunShadows = cfg.Bind("Performance", "NoSunShadows", true, "No shadows from the sun and moon at all, whatever the game graphics option says (maximum performance: a day then costs as much as a night). Local only.");
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
