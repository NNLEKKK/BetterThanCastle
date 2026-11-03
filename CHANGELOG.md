# Changelog

## 0.2.1
- Installer finds every copy of Castle Story (Steam libraries, uninstall entries, recently run programs, shortcuts, running game, disk scan) and lets you pick one; shows BepInEx and mod state of each copy.
- Match statistics: perfect survival (accuracy 100%) is shown in bright blue.
- Replay: Hide UI (button or F8) now hides the replay panel instead of the game UI; a faint corner button brings it back.
- Extended colours (Pink, Cyan, Black) were invisible to enemies: fixed.
- Crystals show extended colours (Purple, Pink, Cyan, Black) in their real hue, including captures; the Black colour is now Rainbow: rainbow flag icon, the whole crystal (also ward capture shards) shimmers through the rainbow; bright white on the minimap, scarves and capture bar.
- Singleplayer replays follow game time: pause and x1.5/x2/x5 speed no longer distort them.

## 0.2.0 (first public release)
- One-file installer `BetterThanCastle-Setup.exe` (BepInEx 5.4.23.5 + mod, finds the game through Steam, uninstall).
- In-game updater from GitHub Releases (SHA-256 verified; `[Update] CheckOnStart`).
- Advanced Hotkeys > Orders: give orders from any camera distance (default on).
- Match statistics panel (F7), recorded into replays.
- Everything ported from the legacy mod: mod menu, advanced hotkeys, BetterConnection multiplayer, replays, micro duel vs bot, loading and performance fixes.
