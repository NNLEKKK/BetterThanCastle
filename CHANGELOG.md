# Changelog

## 0.2.6
- Replays: layout editor (F7 or the Layout button): scale, move and hide the control panel and the player box, sliders for timeline icons, cursors, nick text and view cones, named templates.
- Replays: field of view of every player shown as a translucent 3D pyramid over the map (base follows the terrain height under the view). Button View toggles it.
- Replays: the camera and cursor of multiplayer clients are recorded and can be watched (the message id collided with the plate sync). Player list shows coloured nicks without tags.
- Replays: tasks of all players (beacons with radius circle, flatten and quarry volumes) with the task plate in the owner colour; same button as blueprints (BP+tasks). Coloured owner dot over every unit: off / while Alt / always.
- Replays: new timeline marks (home crystal lost = flag, crystal became neutral = crossed-out crystal), death marks count every unit once.
- Multiplayer (BetterConnection ON): mod message ids of match statistics, rack filter and the match timer changed to stop colliding with other features; everybody in a match needs the same version.

## 0.2.5
- Auto gamma (Overlays > Other): checkbox + Master gamma slider, same brightness day and night. Bottom attribution strip removed, version shown in the menu banner.
- Gameplay > Movement: "Do not pick up lying items automatically" (default on, BetterConnection ON): workers on a task skip loose items on their way, a miner takes only its quarry's resource, items dropped from a bag are mess; Cleanup and manual pie-menu orders are unrestricted.
- Cleanup task: 11 resource filter buttons in the bottom task menu (stone, brimstone, iron ore, grass, logs, planks, bricks, ingots, gears, fabric, rope), synced to multiplayer clients.
- Hotkeys Dig voxel / Break object ignore ward auras and do not dig through stockpiles (issue #1).
- Replays: automatic timeline marks, camera and cursor recording of all players, blueprints of all players with owner tint and queues overlay.

## 0.2.4
- Advanced match timer (Gameplay Settings > Timer, on by default, BetterConnection ON): after the level loads everybody presses Ready (or the host starts by force), 3-2-1 countdown, then the timer runs from 0 on every machine. Crystals give no energy until the start. Follows game pause and speed, loaded saves stay on pause until everybody is ready (state stored next to the save).
- The timer replaces the vanilla clock in the score panel: `m:ss (-m:ss)`, green during peace time, grey in the normal phase, red when the game time is over. Optional milliseconds. Settings: peace time and game length in minutes.
- Replays: the timeline shows the advanced timer time, ticks for start / peace end / game end, orange spans where the game was paused, and player marks (hotkey Hotkeys > Timer, unbound by default; placed 10 s before the press, in the player colour, can be hidden per player).
- Replay hotkeys (configurable): Space pause/play, Z slower, X faster, C back 10 s, V forward 10 s. New Replay tab option: auto-skip recorded pauses.
- Match statistics: energy now counts souls flying to a crystal and dead units waiting there.

## 0.2.3
- Jump-off line, chop 1x1 blocks, weapon rack kit filter (pie submenu), window mode and skip intro.


## 0.2.2
- Updater works after the GitHub account rename (lokillFamalli -> NNLEKKK). 0.2.0 / 0.2.1 cannot update themselves: install 0.2.2 with the installer once.

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
