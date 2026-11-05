# Human test plans (user runs, reports symptom + approximate time; agents read BTClogs/digest.txt, hits.txt)
Always: build Debug (smoke), launch via Steam, afterwards give the log time.

## A. Single player (after every wave)
- Game loads, main menu OK. PgDown opens mod menu; checkboxes work; Hotkeys tab; Gameplay Settings tabs.
- Start a skirmish: loading finishes (bar moves), units act, order units to a point (PickerOrderGiver), camera bookmarks, job-select hotkeys, build something, fight (balance hooks), drop items (Tool/Garnotte), logs/trees.
- Check `BTClogs/hits.txt` "never called" for hooks you expected to run.

## B. Lobby / map (host, BetterConnection ON)
- Gameplay Settings -> Map: map preview picture visible; symmetry buttons (Off/Mirror |/Mirror -/Rotate 180/4-fold) and half selector visible and working. They require: you are host + in lobby + BC ON (`BtcGameplay.CanEdit`).
- Nick: type 40+ characters (should not be cut when BC ON); Rainbow tab: no "name changed" chat lines.

## C. Multiplayer, both with mod, BC ON
- Host + join; units animate smoothly on client (walk/attack/shield/carry); join mid-game and check running animations.
- All command types (move, harvest, craft, catapult, blueprint, groups, spawn); no "Replay intercept" errors.
- Ping lines + pause label; voluntary pause from both sides; host toggles client pause permission.
- Unplug client network 5 s: both show HARDPAUSED then RECOVERING, game resumes; >30 s: reconnect loop, player re-attached, no main menu.
- Spectator join mid-game. Load level during pause releases pause.
- Replay: record, play back; during playback player commands are blocked.
- Log keywords: `[Net]`, `[Hook]`, `Unknown message`, `[Patch] FAIL`, `peer BetterConnection mismatch`.

## D. Mod <-> vanilla, BC OFF (both directions)
- Lobby, nick cut at 30, rename shows chat line, play, disconnect gives normal error + menu.
- Neither log may contain `Unknown message ID` or `[Patch] FAIL`. Known: host ON + client OFF is NOT supported (docs/open-questions.md #11).

## E. Release proof
`build.ps1 -Release`; copy dist/BetterThanCastle.dll alone into a clean game with stock BepInEx; start game; BepInEx log shows `Loading [Better Than Castle]`.

## F. "Shoot at" hotkey (Advanced Hotkeys -> Orders -> shoot_at)
- Unbound by default; bind a key in the UI. Local input only, no new messages (works with BC OFF).
- Archers/arbalists selected + cursor on terrain: they shoot that block (same as pie menu Shoot At). On a storage/rack/workbench object: they shoot the object.
- Cursor on a bricktron/corruptron: nothing happens. Artificers selected: nothing happens. Typing in a text field / menu open: key ignored.

## G. Big package session 1 (all UNTESTED; BC OFF unless noted; report symptom + time)
- Menu size: open PgDown at 1280x720 and 1920x1080: tabs wrap into rows, nothing clipped, all 8 tabs reachable (Packs, Overlays, BC, Hotkeys, Gameplay, Share, Profile, Replay). Packs tab: Dump textures/buildings at the bottom.
- Menu click-through: main menu and lobby with the mod menu open: buttons under the window must not react; close the menu, clicks work again within ~0.5 s. Lua-driven UI may still react (known).
- Profile tab: checkbox looks like a tall rectangle (known, cosmetic). Anim sync UI must be absent from BC tab (expected).
- Movement foldout: exactly one mov entry, no duplicates.
- Log toggles: change PathBlocks/BuildAI/VisObjects/FactionReplay/FactionDump in BepInEx cfg section Logging; the matching log lines appear or stop.
- Presets: a preset saved before this session may now set wrong values (Defs row indices shifted). Note which preset and which row.
- Symmetry (host, lobby, BC ON): with a crystal and a start at map center, Mirror |, Mirror -, Rotate 180, 4-fold: center crystal and start survive; no duplicate on axis.
- Symmetry Alt (Map tab): hold Alt: post-symmetry preview shows, hint text visible; release: normal preview back.
- Thumbnail slider (Share tab) applies to a normal save: save, check the thumbnail size in the save browser.
- Hotkeys near own crystal (Shoot at / Break object): crystal and its trigger do not block the action on terrain; Break on storage/rack/workbench works; Break on a unit does nothing; Dig next to a building: refused when the voxel or the one above overlaps a building; digging a free voxel 1-2 blocks away works. If too strict/loose, report the spot and time (OverlapBox 0.45 to tune).
- Rules banner: change a Players row, gcol, a recipe: yellow top banner appears, game is NOT blocked. Excluded rows (facfix, facidle, facsel, facspec, faclua, hudfix, mapfix, pause, ask): no banner. First sync with a changed rule: red banner.
- Handicap, each field: brewery/firefly income increases; melee damage changes; Cap applies; Extra Death Penalty 0 / 50 / 100: refund of firefly energy on death drops accordingly. StartWorkers note is shown and nothing else happens.
- Updater (Repo placeholder): no banner, no network call. After owner sets Repo and a test pre-release is published: banner Update/Later/Skip; Skip hides it on next start; wrong sha256 is refused; old dll renamed to .old.
- BC OFF both sides (section D rules apply): no `Unknown message ID` and no `[Patch] FAIL` in logs after the menu changes.

## H. Micro duel vs bot (offline, no wire change; menu tab "Duel", PgDown menu in main menu)
- Start: tab Duel -> pick units (default 5 halberdiers each) -> "Start duel". Loads sandbox + arena (~25 s + ~8 s settle), countdown 3 s, units spawn in own safe zone (west = you, blue floor; east = bot, orange floor; sand = neutral; stone = mid). Camera jumps to arena.
- Rules: step into the orange safe zone = instant loss; hitting a bot unit standing in the blue/orange safe = instant loss; kill then run all units home = win; bot kills back before that = trade, game goes on; hold mid alone 5 s = win (HUD shows counter).
- Skip toggle OFF: spam move-attack then focus: your hits stay >= 1.5 s apart per unit. ON: faster hits possible (gap ~0.6-0.9 s when done right).
- Infinite heal ON: all units heal 10 HP/s. Rematch / Exit buttons at the top of the screen; change settings in the menu, press Rematch: new settings apply at once without reloading the map.
- Barrel worker: worker picks up an explosive barrel after spawn (unverified in play; detonate with the normal barrel order).
- After each match send the newest file in `<game>/BetterThanCastle/duel-logs/` (HIT/SKIP/ORD/SNAP lines, see BtcDuelLog) for analysis.

## I. Match statistics (bar + F7 panel; Overlays tab -> "Match statistics"; all UNTESTED by a human)
Log keywords: `[MatchStats] match start`, `sum t=..` (one line per faction every 60 s), `sum end`, `built key=`, `craft key=`, `host stats stream`, `first stats message`.
- Single player sandbox/conquest (BC any): thin bar at the top centre with only your faction: U (alive), L, S %, E, crystal icon + count, W, Tr. Click a value: it turns into +/- (change over 60 s), click again: back. F7 opens the panel (game input locked), Esc or X closes; clicking a stat name switches the graph; graph grows every 5 s.
- Settings: Enabled off hides everything; bar top/bottom, offset, scale, opacity, panel scale/opacity apply live; "Bar: X" / "Panel: X" checkboxes add/remove rows; values persist after restart (BepInEx cfg [Stats]).
- Economy: gather stone/wood/iron into a stockpile: Gathered rows and W grow (stockpile contents at match start are not counted). Build a stockpile/workbench: Built +1 (log `built key=`); demolish it: Buildings lost +1. Craft a bomb/ward: Crafted +1 (log `craft key=`); use/destroy it: Items lost +1. Leave workers idle in auto mode: Idle grows; locked units do not count.
- Combat (duel or MP): kill an enemy and lose one in the same fight within 5 s: Trades +1 each side; a unit killed without a kill back: Free +1 after 5 s. Deaths by profession rows match the dead units.
- Crystals: capture a crystal, lose it later: Avg capture hold shows how long it was held; home crystal does not count as a capture.
- MP, both BC ON: player sees only own faction in bar and panel (header "your stats only", source "host data" on the client). Spectator (mod spectate) sees all players, a late-joining spectator gets the full graph history within ~20 s.
- MP, BC OFF (either side): no `Unknown message ID 84`, no `first stats message` in the host log; client shows only own stats with source "local (some n/a)" (n/a: idle, crafted, built, lost).
- Replay playback: all players visible; after a rewind the stats restart from zero and rebuild (log `replay rewind: reset`).

## H. Advanced timer (BtcTimer, BC ON; 2026-11-04)
1. Lobby: Gameplay Settings > Timer (on by default, peace, game length, milliseconds checkbox). Start a match: panel "Waiting for players n/m", crystals give no energy; everybody presses Ready (or host Start now) -> 3,2,1 -> score panel clock runs from 0:00, green in peace `(-m:ss)`, grey after, red when the length is over.
2. Pause (host voluntary pause / game pause) stops the clock for everybody; speed changes affect it equally. Client and host clocks match within ~1 s.
3. Save during the game, load it: the game stays on host pause with the Ready panel until all press Ready, the clock resumes from the saved value (file btc-timer.txt in the save folder).
4. Energy stat: kill a bricktron, the flying soul energy stays in the faction energy (stats panel).
5. Replay: record a match with the timer; timeline shows white start tick, green peace end, red game end, orange pause spans; the score clock follows the recorded timer. Bind Hotkeys > Timer "replay mark" (unbound by default): marks appear 10 s before the press in the player colour; chips above the panel hide marks per player. Old replays: no ticks, vanilla clock.
6. BC OFF: no timer UI, no mod messages (87/88), vanilla clock.
Untested at the time of writing: items 1-5 in real MP, header patch with a real start event, mark hotkey.

## Replay UI / cones / client POV (2026-11-05, all UNTESTED in a real match)
1. MP (BC ON, 2+ players): record a match, play it: the `< name >` box lists EVERY player (client POV selectable, nick in faction colour, no <color> tags); log has `[PovRec] host: recording track of faction N`.
2. Client cursors: with Cur on, all cursors fly over the map and the client's camera is followed when you pick them.
3. View cones: translucent pyramids (apex = camera, base = ground area seen) per player; button "View" toggles; opacity slider in the layout window.
4. F7 or button "Layout (F7)": yellow handles drag the panel / player box; sliders: scale, icons, cursors, nick text, cone opacity; checkbox hides a module; Compact / Reset; templates (Save as / Load / Del) in <game>/BetterThanCastle/ReplayUI.txt.
5. Marks: flag icon = home crystal lost (defeat), crossed-out crystal = crystal became neutral, normal crystal = captured/stolen. Death marks count each unit once.
6. Mod message ids moved (PovRec 100, match stats 101, rack filter 102/103, timer 104/105): everyone in MP needs the same build.
7. Tasks in replays (button "BP+tasks", new replays only): beacon + radius ring of EVERY player's task in the owner's colour; flatten and quarry show their volume, threshold plane and dig/fill blocks. Ring is my own flat mesh at the beacon height (the game's circle shader has no colour input). If dig/fill blocks are missing send `[ReplayTasks]` lines. DEV: scenarios tasks-rec / tasks-play (`taskdump` line).
8. View cone: far range now 260 (was 70) so the two top corners reach the ground when looking from high up.
9. (2026-11-05 b) Tasks: the game's flatten/quarry visuals are NOT recoloured any more; owner = my overlay: coloured pillar+diamond on every task beacon, coloured ring for the radius, coloured frame around the whole flatten/quarry volume. View cone base = real footprint (24 rays marched through the voxel terrain, sky rays end on the ground at the far distance); look parallel to the ground and compare the outline with the real screen edges.
10. (2026-11-05 c) Tasks: overlay removed (no pillar/ring/frame); game visuals as in-game; the task PLATE over each beacon gets a frame + badge + text in the owner colour (log `plates coloured N` in the DEV `taskdump`). Unit owner dots: layout window (F7) button "Unit owner dots": off / while Alt / always (cfg Replay/UnitOwnerDots, default Alt). UNTESTED: a game was running, only compiled (Debug + Release).
