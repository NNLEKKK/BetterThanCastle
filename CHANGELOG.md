# Changelog

## 0.3.3
Includes everything since 0.3.0 (0.3.0a, 0.3.1a, 0.3.2 and new work). Items marked (BC) need BetterConnection ON; with it OFF the mod stays vanilla on the wire.

### Replay player
- One bottom bar instead of two panels: full screen width, flush with the bottom edge. Top: the timeline with marks; below: time, play / pause, slower / faster, -10 s / +10 s, player switch, toggles and exit. Everything is drawn as icons in one style (tooltips with hotkeys on hover), text uses a geometric font (Bahnschrift, falls back to Segoe UI / Arial).
- Icon toggles: hide UI (F8), follow the player's camera, player cursors, blueprints + tasks, view cones, UI settings (F7). The red square leaves the replay.
- Watching a player: click the nick to open a list (Spectator + all players with their colours), or use the arrows. New hotkeys Shift+A / Shift+D = previous / next POV or spectator (Hotkeys > Replay, rebindable).
- F7 settings: new "Panel height" slider; the panel position is fixed, old position/scale of the two panels is ignored. Defaults now: timeline icons 0.7, cursors 1.0, view cone 0.43.
- Camera and cursor of players are now recorded at 30 Hz and teleports (hotkeys, bookmarks, minimap clicks) are flagged, so the replay shows jumps as jumps instead of a smooth glide. Old replays keep their old 5 Hz data.
- Fixed: flickering fog of war while watching a player (vanilla re-enabled objects for the spectator faction between our updates).
- Fixed in seek: a unit that respawned could stay invisible after jumping back to a death mark; containers refresh after a jump; match statistics are rebuilt after instant seeks and for old replays.

### Loading
- Load timer overlay while the world loads (seconds, total at the end): Display/LoadTimer.
- Menu panels are reused while the world loads, host load about 24 s -> 20.7 s in our tests: Loading/ReuseMenuPanels (turn it off if the in-game UI looks wrong after loading).
- Multiplayer (experimental): the client builds its UI while it waits for the host, waits for its home nest, and falls back to the vanilla loading by itself if something is missing. The real two-PC gain is not measured yet.

### Gameplay and saves
- Infinity heal after loading a save is now bound to its real source ward and ends when that ward is destroyed, like in vanilla (no dummy auras, no stacking).

### Updater
- Version numbers with a letter suffix (0.3.1a) are understood by the in-game updater.

**По-русски:** новая нижняя панель реплея на всю ширину с иконками, переключение игрока по клику на ник и Shift+A / Shift+D, запись камеры 30 Гц с точными телепортами, исправлено мерцание тумана войны в режиме просмотра игрока, таймер загрузки и ускоренная загрузка мира, исправления перемотки реплеев.


## 0.3.0
Items marked (BC) need BetterConnection ON; with it OFF the mod stays vanilla on the wire.

### Replays
- Instant seek: jumping anywhere on the timeline (click, +10 / -10, C / V) takes a fraction of a second instead of re-playing the match. The first time a replay is opened it is prepared in the background (a plate with progress, no sound or picture, a few seconds for a short match, under a minute for a long one); the result is cached in `BetterThanCastle/cash/` and reused next time. Works for old replays too.
- A seek shows the same world as normal viewing at that moment: terrain (digging, crust, sand piles), cut trees, captured crystals with their owner, colour, creep and capture progress, capture circles, wards and carried items where they were placed, contents of altars, racks and stockpiles, crafting station effects.
- Fixed: phantom wards next to the real ones, frozen clone workers that stood still forever after many seeks (+10/-10 spam), invisible units, terrain from the end of the game shown at the start.
- Auto-skip host freezes: the "auto-skip pauses" checkbox now also jumps over moments where the host's game hung (no recorded messages for more than 1 s, setting `Replay/FreezeGap`), so playback no longer repeats them.
- True slow motion: speeds below x1 slow down the world itself (animations, projectiles), not only the packet feed.
- Bear traps are recorded and visible in replays.
- Replays no longer write keyframes while recording (less load during the match); old replays with keyframes still play.
- Fixed broken Russian text in the replay and duel menus.

### Gameplay and multiplayer
- Gameplay Settings > Economy: "Spawn energy: +cost per extra bricktron" now defaults to the vanilla 60 and is no longer highlighted at 60 (BC OFF uses the map value as before).
- Gameplay Settings can be edited offline (main menu) and in singleplayer, not only in a host lobby of a multiplayer game; in multiplayer the rules are still locked after the start.
- Advanced match timer works only in multiplayer; singleplayer and the world editor use the vanilla clock.
- Multiplayer (BC): mod HP numbers, damage numbers, entity centre dots and always-visible rings are no longer drawn on enemy units and buildings (they would reveal hidden objects). Replays still show them.
- Hotkey "Shoot at": the ray ignores the aura volume of wards (a Healing Ward aura no longer swallows the shot at a block), like Dig voxel and Break object.
- Gameplay > Movement "Ban animation skips" (BC): melee units keep their full strike cooldown after a new order, so the move-attack animation skip does not work. Applies in every game mode, not only in duels.
- Gameplay > Movement "Shoot straight down" (BC): ranged units may shoot at targets right below them; the minimum range is measured in 3D for targets lower than the shooter instead of the flat distance only.
- Gameplay > Economy "Workbench/forge: max queued orders" (BC): 8 to 40 orders per crafting station (vanilla 8). Orders above 8 are synced to clients with a new mod message (id 107); the part above 8 is not stored in saves.
- Saves (host, BC): the "infinity heal" state of competitive play survives a save: units that keep a healing aura after the ward was lifted are stored next to the save (`btc-infheal.txt`) and get the same heal back after loading. Heal never stacks (one value: the larger of aura and own regeneration).
- Saves (host, BC): wards carried by workers are no longer lost (vanilla drops them); they are stored in `btc-heldwards.txt` inside the save folder and handed back to the nearest worker after loading. Both files are ignored by vanilla and by clients without the mod.
- Gameplay > Balance "Artificer focus mark" (BC, default OFF): hotkey marks the enemy under the cursor with a selected Artificer; every Sentinel Ward that can fire at it shoots it first (new message id 106).
- Text rule: UI strings use only plain Latin and Cyrillic letters (no look-alike symbols).

## 0.2.7
- Replays: after a multiplayer match the host sends the replay to every client (BetterConnection ON). New Replay tab checkboxes: "Download the replay from the host" (client) and "Send the replay to all clients" (host). The saved file name carries the host: `..._host-NICK.btcr`. Leaving the match while a transfer runs is postponed (up to 90 s) so nobody loses the file.
- In-game chat: messages disappear after 10 seconds (Overlays > Chat: switch and seconds slider). Opening the chat shows the history (100 lines), scroll with the mouse wheel or the bar left of the chat. The lobby chat is unchanged.
- Discord RPC: the About Mod button opens the GitHub page of the mod; the replay recorder no longer shows up as a player "Player". With the advanced timer the status shows the phase (task planning before Ready, peace time with a negative countdown, time limit countdown, time is up) and the RPC clock follows the advanced timer.

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
