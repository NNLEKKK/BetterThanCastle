# Changelog

## Unreleased (next version)

### Bug report + in-game changelog (new menu tabs)
- New rail tab with a bug icon: "Report a bug". A form (area, severity, title, what happened, steps, expected) with the diagnostics attached automatically: system and GPU, game state (scene, multiplayer, replay, fps), installed BepInEx mods, changed mod settings, errors and warnings from the BepInEx log, the log tail, latest replay names (off by default). Every part has a switch and an optional preview of exactly what will be attached; the Windows user name is removed from paths. "Send to GitHub" saves the full report to reports/ in the BetterThanCastle user folder, copies it to the clipboard and opens a prefilled GitHub issue (Ctrl+V pastes the full diagnostics). No token or server is involved: nothing is sent without the player pressing the button. Copy, save, open folder and Ctrl+Z for the form are there too.
- New rail tab "What's new": the changelog inside the game, newest release first, every change with a kind icon (new / improved / fixed / removed) and an area tag, filters by kind and area, search. Entries that belong to a menu place carry a link chip: a click opens that tab (and the exact Visuals page or hotkey bind). The rail icon shows a dot until you open the tab after an update. The content is a plain embedded text file (src/BetterThanCastle/Resources/changelog.btcl, format in its header).

- The bug and changelog buttons sit in the bottom corner of the rail (small, above the sound button; the bug one is red) instead of the tab list. The changelog got a "Reworked" kind. Tooltips near the top edge open below the icon.
- The update card is redone in the menu style (flat dark card, icon, sounds). It shows a download progress bar; after a successful update the game restarts by itself (Steam copies through steam://, otherwise the exe) and opens the changelog.

**По-русски:** новая вкладка «Сообщить о баге» (значок жука): форма (область, серьёзность, заголовок, что произошло, шаги, ожидание) и диагностика, которая прикладывается сама: система и видеокарта, состояние игры, моды BepInEx, изменённые настройки мода, ошибки и предупреждения из лога, хвост лога. У каждой части свой переключатель и предпросмотр того, что уйдёт; имя пользователя Windows вырезается. «Отправить на GitHub» сохраняет отчёт в папку reports, копирует его в буфер и открывает готовую заявку (Ctrl+V вставляет полную диагностику). Токенов и серверов нет: без нажатия кнопки ничего не отправляется. Новая вкладка «Что нового»: ченджлог внутри игры с иконками типа изменения, фильтрами, поиском и кликабельными ссылками, которые переносят во вкладку или к нужной настройке; на значке точка, пока не откроешь после обновления.

### Replay
- GIF clips (EXPERIMENTAL, the quality is poor: 252 colours at most, no sound, big files). One bar icon: click it, drag the clip range on the timeline, see the length and the estimated size, tune the width / FPS / colours / dither / optimize, then "Clip it". The replay plays the range once and the screen is recorded as you see it (your camera or the watched player's POV, cursors, cones). The GIF is saved to Clips/ in the BetterThanCastle user folder and copied to the clipboard (pastes as a file). Up to 30 s.
- APM graph: only the personal peak mark is shown by default (idle / surge are optional); the scale caption is replaced by the single top value; new F7 settings for the graph (height, line width, opacity, fill, grid, top value, which marks).
- New replay loading screen in the style of the replay bar (replaces the old "REPLAY / Cancel" box and the plain black plate): dark cover, overall progress bar, steps with icons (reading the file, loading the world, placing keyframes for instant seek with a percentage), elapsed time, exit button (Esc). A lighter card is shown when a forward seek takes longer than a moment.
- The old scene rewind is removed: every backward seek uses the keyframes (a click near the very start restores the first keyframe instead of reloading the replay). While keyframes are still being placed, rewinding back is declined with a message.
- The Replay tab in the main menu is reworked: a grid of cards with a map relief thumbnail (read from the heightmap inside the replay, cached under previews/), mode, duration, date, size, player colours and a "keyframes ready" icon; search (map, player, name), sorting, MP/SP filter, favourites, folders, custom names (stored in Replays/replays-meta.txt), date groups. A click opens a details panel with the players and file info and icon actions (play, favourite, folder, copy file, share link, delete). The recording and playback settings moved behind the gear icon.
- The replay timeline got a new look: a thin line that swells under the pointer, time ticks under it, the played part in the accent colour (amber while seeking), a slim playhead with a halo; the marks (deaths, crystals, fights, pauses, player marks) sit on the line with colour stems, dots and dark backings, fights and pauses are drawn as bright segments on the line.
- Match format on every replay (cards, list, compact lines, details, search): 1v1, 2v2, 3v1, 2v2v2, FFA3, FFA4... The recording ghost client ("REC") is not counted as a player. Teams are recorded into the replay header since this version (new replays only); older replays are guessed from the player count and marked with "?" (for example "FFA4?").
- The main mod menu is full screen and closes when a replay starts; the rules / editor banners are not shown in replays.
- The "Show UI" button is a small corner icon in the bar style (F8 still works).
- With view cones off and only cursors on, the crooked ground frame and the look-at cross are no longer drawn.

### Undo (Ctrl+Z)
- Ctrl+Z reverts accidental changes in the Hotkeys tab (binds, vanilla binds, sliders, switches, favourites, profiles, resets) and in the Replay tab (favourites, folders, names, recording settings, deleted replays), as many steps as you like. The bottom of the menu shows what was undone, with an undo icon. Deleted replays move to Replays/.trash (restored by undo, cleaned after 14 days) instead of vanishing at once.
- UI sounds are now minimal and neutral: very short quiet ticks, no melodies.

**По-русски:** Ctrl+Z откатывает случайные изменения во вкладке хоткеев (бинды, ванильные бинды, слайдеры, переключатели, избранное, профили, сбросы) и во вкладке реплеев (избранное, папки, названия, настройки записи, удалённые реплеи), сколько угодно шагов. Внизу меню пишется, что откатилось, с иконкой отката. Удалённые реплеи уходят в Replays/.trash (откат возвращает, чистится через 14 дней). Звуки интерфейса стали минималистичными и нейтральными.

### Menu sounds
- UI sound feedback in the mod menu and the replay bar: a soft tick on hover (menu only), clicks, rising / falling tones for switches, typing and backspace ticks in text fields, a thud on delete / reset, a short chime when a key is captured or a profile is saved. The sounds are synthesized in code (no files) and play through Windows directly, so the game's audio settings do not matter. The speaker icon at the bottom of the menu rail cycles off / low / medium / high (config Display/UiSound). Muted while the game is muted.
- Menu sections fold (state is remembered), text fields release the keyboard on a click outside.

**По-русски:** звуки интерфейса: тихий тик при наведении (в меню), клики, разные тона для включения и выключения, звук печати и стирания, глухой звук при удалении и сбросе, короткий сигнал при записи клавиши и сохранении профиля. Звуки синтезируются в коде (без файлов) и играют напрямую через Windows. Значок динамика внизу рельсы меню переключает выкл / тихо / средне / громко.

### Visuals tab (the old "Overlays" tab, fully reworked)
- The tab is renamed to "Visuals" (RU: "Визуал"): it holds everything that changes how the game looks. Pages on a small icon rail: Rings, World overlays, Plates and HUD, Match statistics, Screen, Profiles. A Simple / Expert switch (icons in the header): Simple shows only switches and choices, Expert adds sliders, colours, palette fine tuning and the ring shader overrides. Search across the colored-HP settings, foldable sections with an "on / total" counter, expand / collapse all, reset.
- Colour bands of the HP / stamina / ammo rings are edited on a colour bar: drag a boundary to move it, click a band to recolour it, + adds a band; next to it you see how a ring looks at 100 / 75 / 50 / 25 / 10 %.
- New colour picker everywhere (palettes, numbers, damage, explosion map colours, overlays): square + hue strip, hex field, copy / paste a colour code, recent colours and a preset strip.
- Profiles page: presets (Clean, Competitive, Everything), your own named profiles (visual-profiles folder), copy / paste a profile as text to share it, reset to defaults. Everything is one Ctrl+Z step.
- Window mode, skip intro, Discord presence, menu pause / input block, enemy skin and brightness moved to the Screen page. Match statistics settings have their own page; the chat fade lives with the plates and HUD.
- New hotkey "Hold to see the vanilla look" (Hotkeys tab, View): colored HP and the world overlays go off while it is held.
- One master switch in the header of every Visuals page turns all visuals (colored rings and world overlays) off and back on. A star next to any setting, palette or section adds it to the new Favourites page; the new "All settings" page lists every visual in order without sections.
- Ctrl+Z works in the Visuals tab. Cycling buttons (several states in a row) are not undoable yet.

### Advanced Hotkeys tab (reworked)
- New look in the style of the Replay tab: a category rail inside the tab (all, camera and view, control groups, orders and units, match and replay, safety, vanilla, favourites, keyboard map, profiles), search by name or key, a flat row per bind with one chip per key combo, tooltips, conflict / changed filters.
- One bind can have any number of key combos: click "+" to add another, click a chip to change it, right click or the cross to remove. Any of them fires the bind (stored as `A+B|C` in hotkeys.txt; old files keep working).
- The vanilla key bindings are editable in the same tab (keyboard keys; one key or a modifier + key, as vanilla allows; extra slots work too). They are saved through the game's own file (Info/Lua/userconfigs.lua) exactly like the vanilla options menu does. Mouse binds are shown read-only.
- Key finder: press any key while the tab is open: it shows what is bound to it (mod and vanilla); press it again to jump to that bind (more presses go through the others).
- Keyboard map page: every key lit by mod / vanilla / both / conflict, layers for Shift / Ctrl / Alt, a click lists the binds of a key and jumps to one.
- Favourites (star on a row) and profiles (hotkey-profiles/*.txt: apply, overwrite, delete, copy as text, paste from the clipboard; mod and vanilla keyboard binds).

**По-русски:** вкладка Advanced Hotkeys переделана в стиле вкладки реплеев: рельса категорий внутри вкладки, поиск по названию или клавише, строка на бинд с чипами комбинаций, подсказки, фильтры конфликтов и изменённых. На один бинд можно навесить сколько угодно комбинаций (плюс, клик по чипу - изменить, правый клик или крестик - убрать), срабатывает любая. Ванильные бинды правятся в той же вкладке (через файл игры userconfigs.lua, как в ванильных настройках). Поиск по клавише: нажми клавишу - покажет, что на ней висит, нажми ещё раз - перейдёт к бинду. Карта клавиатуры, избранное и профили биндов (текстом, можно копировать и вставлять).

**По-русски:** GIF-клипы (ЭКСПЕРИМЕНТАЛЬНАЯ функция, качество плохое: не больше 252 цветов, без звука, файлы большие). Одна иконка в панели: выбор диапазона мышью на таймлайне, длительность и примерный размер, настройки ширины, FPS, цветов, дизеринга и оптимизации; GIF сохраняется в Clips/ и копируется в буфер. В APM по умолчанию только метка пика, вместо подписи шкалы одно число сверху, новые настройки графика в F7. При выключенных конусах и включённых курсорах больше не рисуется кривая рамка.

## 0.3.4
Replay analytics release. Items marked (BC) need BetterConnection ON; with it OFF the mod stays vanilla on the wire. New data is appended to the replay file in trailing sections, so older versions still open these replays and older replays still open here (they just have no new data).

### Activity graph (APM / EPM)
- A step graph above the timeline, inside the replay bar: the actions per minute of every player in their colour (presses of the trailing 10 s, sharp edges, zero hidden under the timeline line). Click on it to seek. Hover shows the value of each player at that moment.
- The bar icon cycles APM / EPM (no spam) / hidden. Marks follow the chosen series.
- Auto marks as frames with a duration: idle (snowflake: a strong drop below the player's average for 20 s), surge (flame: clearly above the average) and the personal peak. Hover shows the kind, the player and the length.
- What counts (StarCraft style, recorded per player): left / right mouse clicks, and key taps that really did something (the selection or the input mode changed, a menu shortcut ran, an advanced hotkey fired). Camera movement / rotation keys, held keys, modifiers alone, typing in a text field and keys without an effect do not count. EPM also ignores the same key repeated within 0.15 s. The detection is the same on the host and on clients. (BC) for clients.
- Match statistics got three rows: actions per minute (average), EPM and peak APM (10 s window). They are filled while a replay is recorded; old replays show 0.

### Pressed keys of the watched player
- Bar icon cycles feed / mini keyboard / hidden. Feed: pills with the key or mouse button, newest on top, fading. Keyboard: flat tiles in the bar style, the pressed key takes the player's colour, Shift / Ctrl / Alt / Caps stay lit while held; middle and side mouse buttons included.
- Hover a key or a pill to see what it is bound to. The player's own key bindings (advanced hotkeys + the game's bindings) are recorded into the replay, clients send them to the host in small chunks (BC); replays without them show your own bindings and say so.
- The block sits above the bar on the right and can be moved, scaled or hidden in the F7 editor ("Pressed keys").
- Only new replays carry key data.

### Replay view
- The mini map in a replay is pinned flush into the top-left corner and can be hidden with a bar icon; the big scoreboard panel behind it (background, header, timer) is always hidden in replays.
- View cones are now a true 3D pyramid at any camera angle (looking level or up no longer gives a flat fan).
- The control groups overlay and the ping / client pause labels are hidden in replays.
- Player cursors in the replay show the nick without colour tags, tinted in the player's colour.

**По-русски:** график активности APM / EPM над таймлайном (клик перематывает, иконка переключает APM / EPM / скрыто), авто-метки простоя, всплеска и пика рамками, подсчёт по нажатиям как в StarCraft (только клики и клавиши, давшие эффект), три новые строки в статистике, показ нажатых клавиш наблюдаемого игрока (лента или мини-клавиатура) с подсказкой, на что назначена клавиша, и записью раскладки игроков в реплей, миникарта в углу с кнопкой скрытия, полноценный 3D-конус обзора, скрыты оверлей групп и пинг, ник у курсора без цветовых тегов.

## 0.3.3
Includes everything since 0.3.0 (0.3.0a, 0.3.1a, 0.3.2 and new work). Items marked (BC) need BetterConnection ON; with it OFF the mod stays vanilla on the wire.

### Replay player
- One bottom bar instead of two panels: full screen width, flush with the bottom edge. Top: the timeline with marks; below: time, play / pause, slower / faster, -10 s / +10 s, player switch, toggles and exit. Everything is drawn as icons in one style (tooltips with hotkeys on hover), text uses a geometric font (Bahnschrift, falls back to Segoe UI / Arial).
- Icon toggles: hide UI (F8), follow the player's camera, player cursors, blueprints + tasks, view cones, UI settings (F7). The red square leaves the replay.
- Watching a player: click the nick to open a list (Spectator + all players with their colours), or use the arrows. New hotkeys Shift+A / Shift+D = previous / next POV or spectator (Hotkeys > Replay, rebindable).
- F7 settings: new "Panel height" slider; the panel position is fixed, old position/scale of the two panels is ignored. Defaults now: timeline icons 0.7, cursors 1.0, view cone 0.43.
- Camera and cursor of players are now recorded at 30 Hz and teleports (hotkeys, bookmarks, minimap clicks) are flagged, so the replay shows jumps as jumps instead of a smooth glide. Old replays keep their old 5 Hz data.
- Fixed in seek: a unit that respawned could stay invisible after jumping back to a death mark; containers refresh after a jump; match statistics are rebuilt after instant seeks and for old replays.

### Loading
- Load timer overlay while the world loads (seconds, total at the end): Display/LoadTimer.
- Menu panels are reused while the world loads, host load about 24 s -> 20.7 s in our tests: Loading/ReuseMenuPanels (turn it off if the in-game UI looks wrong after loading).
- Multiplayer (experimental): the client builds its UI while it waits for the host, waits for its home nest, and falls back to the vanilla loading by itself if something is missing. The real two-PC gain is not measured yet.

### Gameplay and saves
- Infinity heal after loading a save is now bound to its real source ward and ends when that ward is destroyed, like in vanilla (no dummy auras, no stacking).

### Updater
- Version numbers with a letter suffix (0.3.1a) are understood by the in-game updater.

**По-русски:** новая нижняя панель реплея на всю ширину с иконками, переключение игрока по клику на ник и Shift+A / Shift+D, запись камеры 30 Гц с точными телепортами, таймер загрузки и ускоренная загрузка мира, исправления перемотки реплеев.


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
