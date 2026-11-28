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

## Small list items 2026-11-07
- Better Workbenches (craft.better + craft.queue, BC ON): see section "Better Workbenches".
- G5: put a healing ward, stand in, lift it so it is unpowered, units heal anywhere; save, reload: they still heal (log `[InfHeal] saved/restored`).
- I2: open a replay that has POV tracks: the camera starts on the first player's camera (log `[PovRec] start camera set`).
- noskip (Movement): move-attack before a hit no longer cancels the strike. H2: enemy HP numbers/centres hidden in MP, visible in replays. G8/J3 see STATUS.

## Replay v4 (branch replay-v4, 2026-11-07)
- Traps: record a match with armed bear traps (own and enemy). Replay: free camera shows all traps; a player's POV shows only own/allied ones. Log `[ReplayTrap] rec observes` (recording), `[ReplayTrap] playback trap` (playback).
- Slow motion: x0.25 / x0.5: units, animations, arrows, particles really slow down; pause freezes the world; x4/x8 speeds everything up; camera and UI stay at normal speed. replay.txt `TimeScale=0` = old behaviour.
- Instant seek: first open shows "Preparing seek N%" (fast forward to the end, then back); afterwards clicking anywhere on the timeline (back or forward) is near-instant. Second open of the same replay skips the preparation (cache in BetterThanCastle/cash/*.btcs). Log `[ReplaySeek] jump ... total N ms`.
- After jumps check: carried items in hands, items on racks/stockpiles, buildings/blueprints, crafting queues, dug terrain and fallen trees look like normal playback at that time. Old replays (v0.2.x) play and seek the same way. Fallback: replay.txt `InstantSeek=0`.

## Base shields (2026-11-11, Gameplay > shield page, BC ON on every PC)
- Host: Gameplay > shield page: master row "Base shields" on, summary card reads the rules in plain words; dependent rows appear only with their parent on. Client sees the host's values.
- Match (2 PCs, advanced timer with peace 2-3 min): spheres around home crystals; enemy units ordered inside are knocked back 2-3 blocks by a weak smash and halt; a unit jumping onto the dome slides off and lands outside; artificer drop needs the barrier edge in its reach; arrows vanish at the edge; catapult shots bounce off; digging/blowing blocks inside from outside does nothing; owner shoots and fights out freely. Top-left chip "Shields" with rows and timers; notices + sounds when a shield goes up/down.
- Peace end with only "Up during peace": all shields fall at once (sound + notice). With "Drop by captured crystals" N=1: capture one neutral crystal -> enemy shields open for you only (chip says "open for you"); lose it -> back up, your units inside lose 20 HP/s and cannot hit anything until they walk out.
- Artificer mode: walk an artificer near an enemy crystal -> its shield falls for you; walk away -> countdown (ticks in the last 5 s) -> back up.
- Captured crystal shield goes up right after the capture; enemies on it are trapped.
- Gameplay shield page has a small link line to Visuals > World overlays > Base shields: opacity slider, volume slider (preview while dragging), per-sound switches, notices, HUD chip; all survive a restart.
- BC OFF on everybody: nothing of this exists (no spheres, no msg 109). Logs: `[Shield]` lines (bars/up/trapped every 10 s, flips).

## BetterConnection v2 (2026-11-11, all players same BTC version, BC ON)
1. 2 PCs, normal match 10 min: no "Waiting for a player" card at all (alt-tab, minimise, menu open on either side).
2. Load a big map with 3 players: no pause card during or right after the load; compare load time with 0.4.0 (early client UI is off now).
3. Pull the client's network cable 10 s: host + client both show "Waiting for a player" with the client's name ("is not responding"); plug back -> both resume within ~2 s.
4. Pull the cable 30 s: client shows "Reconnecting to the host...", reconnects by itself; host: card "X disconnected, waiting" -> back with the same units/colour.
5. During 4, host presses "Continue without them": the game runs for everybody else; the client still rejoins later.
6. Client card "Leave to menu" while reconnecting -> menu; host keeps waiting -> "Continue without them".
7. Host quits the match: clients go to the menu (no endless reconnect).
8. Voluntary pause (key) still works for host and clients; client-pause permission plate unchanged.
Logs: `[Net] link:` / `link pause ON/OFF` lines (host has the reason and the session total).

## N1/N16 5+ players, crash, rejoin, spectator (2026-11-14, BC ON on every PC, Radmin is fine)
Hidden labs passed (5 games on one PC + clumsy Radmin profile): mp5-pause, mp-rejoin, mp4-cut. Real PCs still needed:
1. 5-6 players + replay recording: everybody gets into the lobby and the match (the 5th/6th were refused before).
2. One client kills the game (Task Manager) mid-match: host card "X disconnected, waiting" within ~30 s, NOBODY else drops
   in the next 3 minutes; X starts the game again and joins the same host -> back with own units (no 0% hang).
3. Two clients lose the network together for 10 s, then 40 s: both come back by themselves ("quick return"), no menu kick.
4. A new player joins a running match as a spectator: loads in < 1 min, sees the game; host game frozen only while it loads.
5. Pause key in the lobby does nothing; pause pressed by the host while a client still loads the map: that client loads anyway
   and is paused only once it is in.
Logs: `[Net] Server: old connection N is silent`, `[Spec] quick return`, `[Spec] player 'X' is back`, `transport pools: 4096`.

## Team pings (2026-11-15, BC ON on every PC; hidden labs mp-ping + ping-play OK)
1. Bind "Ping allies" (Advanced Hotkeys > Orders). Press over a block: ring pulses on the ground, "!" above in your colour, mini map pulse, chime; gone after 5 s.
2. Press over the mini map: the ping lands on the surface at that map point.
3. Turn the camera away: an arrow with "!" sits at the screen edge pointing at the ping.
4. 2v2 with teams: ally sees your ping (and hears it), enemies see nothing; spectator sees all pings.
5. Visuals > World overlays > Team pings: duration, cooldown, max per player (4th ping removes the oldest at max 3), size, volume (slider plays the chime), hide allies' pings, edge arrow. Settings survive a restart.
6. Replay of that match: pings appear at their time with the chime; after a seek they appear silently.
7. BC OFF: the bind does nothing, no message 117 on the wire.

## Wormhole wards (2026-11-17, BC ON, phantoms + "Wormhole wards" rule set in the lobby BEFORE the match; labs wormward-lab/-craft/-build/-mp OK)
1. Altar panel: "Wormhole Ward" right after the Sentinel ward, white icon, cost 1 iron ingot + 30 brimstone; queue it, the result is a white-gem sentinel ward.
2. Altar pie (right-click wheel): "Wormhole Ward" queue button. Rule off: the slot is empty, panel has no entry.
3. Build menu > wards section: "Wormhole Ward" button (no hotkey). Place 1-2 blueprints: builders bring ONLY wormhole wards (a plain sentinel blueprint never takes a wormhole ward). Built ward is white.
4. Phantom sent far: walks to the nearest own wormhole ward / crystal (crystals only with Crystal wormholes on), appears at the one nearest to the goal. Normal workers never use them.
5. Wormhole ward never shoots enemies. Enemies can break it and carry it away like any ward.
6. Bury a ward (block on top / dirt over it): no teleport through it. Carried / stored ward: no teleport.
7. "Only when powered" rule: an unpowered wormhole ward (far from crystals/pylons) does not work, powered does.
8. Pie with bricktrons selected hovering an own wormhole ward: "Teleport: off" / "on"; same on the home crystal pie (needs Crystal wormholes). Client: the switch reaches the host.
9. Save + load (host): marks, switched-off wards and crystals, tagged blueprints, a carried wormhole ward come back.
10. Client sees own wormhole wards white; replay shows them white.

## Multi-camera (2026-11-18, local only, no net; labs multicam-lab/-hover/-restore1/-restore2 OK, look NOT verified by screenshot)
Binds: Hotkeys tab > View > "Multi-camera" (all unassigned except the drag modifier LeftAlt): split, toggle, close, max, float, dock all, next/prev, sync, focus 1..8.
1. Bind "Split": the screen halves, both views show the same place; the view under the mouse has a green frame and owns the camera (WASD, mouse drag, selection box, orders, camera binds act in THAT view only). Move the mouse to the other view: it takes over, the first keeps its place.
2. Split up to 8 (2 side by side, 3 = 2 + 1 wide, 4 = 2x2, 5 = 3 + 2 ... 8 = 4x2). The 9th does nothing.
3. Title bar (top-left of every view): number, free / max / x. "max" = that view fills the screen (others stop rendering), again = back. Double click on the bar = max. "x" closes; closing the second to last one returns to one normal camera at the place of the remaining view.
4. Drag a title bar of a grid view onto another view = they swap places. "free" (or Alt + left drag anywhere) pulls the view out as a window: drag by its bar, resize by the corner handle (or Alt + right drag); "dock" puts it back. A free window is drawn over the grid.
5. Bind "Toggle": collapses to one camera (stays where the active view looked), press again = the old layout and view places come back. "Sync" = every view jumps to the active one. "Focus N / next / prev" keep the chosen view until the mouse moves.
6. Clicks on the title bars must NOT select or order anything. Orders / selection boxes across views: select in one view, give the order in another (selection is shared).
7. Close the game with a split, start a map: the same split comes back about 3 s after the load, every view on the start camera (option "Remember the split between games").
8. Replay: same things work while watching a replay. FPS: 4 views cost roughly 40% more render time, 8 views about 2x (lab 640x360); "Lighter picture on the views you are not controlling" removes ambient occlusion there.
Known limits: world labels drawn by scripts (HP bars, pings, unit plates) follow the view under the mouse only; the fog of war vision list is picked around the active camera; the mini map shows the active view.

## PvP net pass (2026-11-22, BC ON on both PCs; tab BC > Synchronisation)
1. Smooth movement v2 (BC ON both sides, BC tab "Smooth unit movement"): client watches walking groups and a fight; no jumps back/forward, no teleports, units stop where the host shows them. Log: `[PosNet] client 30 s` line (delay, jitter, underruns, jumps, snaps) from the client BepInEx log.
2. Instant orders (host rule Movement > Instant orders, default ON): redirect running units many times fast: they react at once, no freezes; compare with the rule off.
3. HP/stamina: hits show on HP bars at once on the client.
4. Kits after rejoin / fog / replay seek: units keep their weapons, racks show their kits. Log: `[TrackFix] late holder links applied=N lost=M`.
5. Own transport (OFF by default, turn ON on BOTH, reconnect): `[OwnNet] ... sends through the own transport` on both logs; pull the cable 30 s and 2 min: the game freezes and continues, no return to the menu; 5-20% loss (clumsy): no long stalls. Log line every 30 s: `[OwnNet] 30 s conn N: out=.. resent=.. rtt=.. queued=..`.

## CSP 0.10 port (2026-11-23, e1410f7)
1. Fight: a unit hit several times in a row (stuns) must take a move order within ~2 s; log `[LegsFix] ... put back on its feet` when it happens.
2. Crowd at a 1-high step / 1-wide stair: no standing below the step; on a 1-wide stair units wait, do not turn back.
3. Workers (rule work.baseArea): no auto-run to loose items in the middle of the map or near enemy units; a direct order still works.
4. Quicksave + save menu: no freeze, log `[FastSave] ... saved`; load: phantoms / inf heal / wards / station radius restored.
5. 2v2 BC ON: ally's blueprint deliveries hidden on your client; someone un-readies during start -> map still loads (`[LobbyStart]`); save with allies, load, rejoin -> same teams (`[SavedTeams]`). BC OFF: no `[LobbyStart]`/`[SavedTeams]` lines.

## Unit sounds like the host (2026-11-23, BC ON on host + client; tab BC > Synchronisation "Unit sounds like the host", "No sounds from the fog")
1. Client: give a worker a knight kit, then take it off (or let it re-equip as worker): its voice/hurt/death sounds must match the job after each change (was: worker sounding like a knight).
2. Client near a fight with archers/arbalists: hears drawing bows, "spotted an enemy" cries, arrow hit/miss, kill cries (before: only the host heard them). No doubled sounds (equip, hurt, deaths, rack/workbench sounds once).
3. Host: enemy units/bases hidden in the fog make NO sound (workbench, spawns, fights, cries); the same objects become audible as soon as they are seen. Same on a client.
4. Turn "Unit sounds like the host" off on the client, reconnect: vanilla behaviour (no relayed sounds). Turn "No sounds from the fog" off: host hears everything again (at once).
5. Log lines: host `[SoundNet] host 30 s: ... KB/s` (lab: 0.13-0.14 KB/s in a 30-unit fight), client `[SoundNet] client 30 s: played=.. doubles skipped=..`.

## Personal work settings (2026-11-23, BC ON; Hotkeys > Orders)
1. Trap pie: select a builder, open the pie on your trap: slot after "Arm Bear Trap" shows "Auto re-arm: ON" (check icon). Click -> OFF (X icon). Fire a trap: workers do not re-arm it; secondary order still re-arms. Hover your trap with nothing selected: pie with only the switch.
2. Same as a client (host log `[Prefs] conn N (...): trap=0;...`); host's own traps unaffected by the client's switch.
3. Checkbox "Traps: ... at the match start" off -> new match starts with OFF.
4. "Tree cutting: ... logs stay" on: a cutting task leaves logs and stumps; off = vanilla. Client's choice applies only to the client's tasks.
5. Chopping block: select it, hold Left Ctrl + wheel: ring + label "Log pickup radius: N (default)"; its worker fetches only logs inside the ring. Workbench/furnace/builders unchanged (vanilla). Client sets own block radius; save + load keeps it.

## Visual desync fixes (2026-11-25, 0.6.1; replay + client, BC ON for fog parking)
1. Replay with weapon racks/stockpiles: jump forward/back several times: racks show their kits, stockpiles their content, altars/stations their state; nothing empty that was full in normal viewing.
2. Replay jumps: no unit keeps running on the spot; units that stand idle in normal viewing stand idle after a jump.
3. MP client (BC ON): enemy armed units leave your vision and come back: they still carry their kit; your racks/stockpiles show content after a rejoin. Log line `[TrackFix] ... parked=N` shows parking happened; `[PoolRevive] reconnected N` after reuse.
4. Lab method: scenarios desync-ref then desync-jump (Komorebi): J* vs D* show no rdisp Toolrack diffs, animFlagMismatch 0, J900 unit renderers same.

## Online tab (corner tab 16, 2026-11-26)
Two PCs on the same Radmin network, both with this build. Lab online-mp (two copies on one PC) passed discovery, avatars, join from the list, chat both ways, met history.
1. Open the menu, globe icon in the top right corner: own card first (avatar, nick in plate colour, status "In the main menu", Radmin 26.x address, copy button). The other PC shows up within ~3 s, also when the vanilla lobby list does not show its host.
2. PC A hosts a lobby: on PC B the card turns green "Hosting a lobby", map, mode, 1/N and a Join button. Join -> waiting window -> in the lobby (no IP typed).
3. Both in the lobby / match: statuses update (lobby of X, match with clock and "vs" names, pause, replay, duel vs bot with score, aim training). Discord shows the same.
4. A (host in lobby) presses + (Invite) on B's card while B is in the main menu: B gets a notice + a top card "A invites you" with Join for 60 s, and the same invite at the top of B's Online tab (Join / Decline). Join works from both.
5. Chat: All online + direct messages (chat icon on a card). Enter sends, history kept after restart, red dot on the globe for unread, notice for direct messages. Trash clears a chat, Ctrl+Z brings it back.
6. Quit B: on A the card goes to "Met before (offline)" at once (bye), with "x min ago" and last address; trash forgets it (Ctrl+Z restores).
7. Settings fold: switching off "Others can join" removes the Join button on the other PC; "Show my address" off hides the address; header switch off = nobody sees you, globe greyed.
8. Firewall: Windows may ask to allow UDP for the game once; if a player never shows, add their Radmin IP in Settings > Extra addresses.

## Online duel + void arena (2026-11-26, 0b08edb)
1. Duel tab Start (bot): loads fast, arena = thin floor in the void, zone colours, camera on the arena; push a unit off the edge -> it dies and counts as a kill.
2. Two PCs, both BC ON, Online tab: crossed swords on the other card (tooltip shows your settings); other side gets a card, Accept -> lobby -> duel loads by itself (host start may freeze ~11 s: vanilla hosting).
3. Opponent sees the floor, commands own units (Bot column), attacks yours; HUD names "A vs B", round number, score line at the bottom.
4. Round end -> Next round x/2 on both; starts only when both pressed. Give up + reset mid-fight: presser loses, new round at once, both score lines agree.
5. Third PC: Online list "Join match" on the host -> spectator, sees floor + HUD with names, Exit only.
6. Opponent leaves mid-round -> host gets a win + notice. Elo / all time survive a restart (configs/duel-pvp.txt), duel-logs/pvp-history.txt has one line per round.
7. BC OFF on either side: swords button greyed with a reason; nothing new on the wire.

## Better Workbenches (2026-11-26, rule craft.better default ON + craft.queue 8..40 default 40, BC ON, msg 139)
- Host: queue 40 orders at a workbench; the queue is ONE row 8 slots wide, LMB drag (or wheel) scrolls it, a drag never removes a slot, a plain click removes that one.
- Client (2 PCs): same station shows the same 40 icons in the same order after every add/remove/finish/loop; host and client lists never differ (msg 139 resend every 3 s).
- Leftovers: junk nobody can store (no stockpile for it) inside a workbench does not stop workers from carrying finished kit parts to racks nor from crafting the next order.
- Lab: put many barrels/bombs/resources in by hand (contextual store), no limit.
- Part buttons: workbench shows poleaxe, kettle hat, bow, quiver, cap; each queues only that part. Hidden when the rule is off.
- Save with a 20-order queue, load: all 20 back (btc-craftqueue.txt in the save folder).
- Rule OFF or BC OFF: limit 8, no part buttons, vanilla capacity, no msg 139.
- Keep for later: queue bow+quiver with loop, 8 rope + 16 planks inside; after the bow, nobody carries the ropes out while the quiver is crafted.
- Rack limit: 1 rack with a Halberdier filter and 1 free poleaxe slot; queue poleaxe x3 + bow at a workbench with the rack button red: one poleaxe is crafted, then the bow, the other poleaxes wait; take a kit off the rack -> the next poleaxe starts. Button grey = vanilla. Orders > "Better Workbenches: by default..." sets new stations' mode; a client's toggle shows on the host and survives save/load.
- Prefetch: loop bow + quiver, 2+ workers: while the operator makes the quiver, exactly one other worker brings rope/planks; never two helpers at one station; the operator is never replaced.
- Helper switch: worker button grey = no helper (only the operator fetches for the current order); Hotkeys > Crafting stations default off -> new stations start grey; client toggle reaches the host and survives save/load.

## Bear traps task (2026-11-26, rule work.trapTask, BC ON, C# game menu)
1. Place a trap: task "Bear traps" with the trap icon appears in the task list (and on the beacon). Remove all traps: it goes.
2. Fire several traps: goal counter = fired traps; workers each run to a DIFFERENT trap, nearest first; extra workers do other work.
3. Priority lowest / crew 0: nobody re-arms. Manual builder re-arm still works.
4. Worker carrying an ingot/plank/kit part: stores it first, then re-arms; with no fitting container he does not take the task.
5. New task > Bear traps: click a trap / drag a frame -> own task "Bear traps (own)" selected; click/frame adds, Ctrl removes, Ctrl+Z undo, Esc leaves.
6. Traps of an own task are not in the auto one; delete the own task -> they return. Save + load: tasks and members come back.
7. MP client: same as above from the client; enemy never sees your task membership.

## Movie panel in MP + terraform look (2026-11-26, BC ON, host rule gen.editor)
1. Host + 1 client, editor panel shared. Client opens movie sidebar: slow 0.5 / 0.1, fast 1.5 / 5 -> both PCs run at that speed (units, anims, arrows); same button again = normal. Host sees notice "X - movie panel: slow 0.5".
2. Pause / step 1s / step 0.1s from either side: everyone pauses (pause card), steps run ~1 s / 0.1 s on all.
3. Stop the Sun, Time of Day slider, Sun Speed slider, Toggle Fog of War: change on both PCs; notice to the others (slider once per 2 s).
4. Slow 0.1, then client drops/rejoins: rejoiner loads normally, then runs at 0.1. Leave the match at 0.1: main menu runs at normal speed.
5. Editor rule off mid-match at 0.5: speed back to 1 for all. SP: movie buttons behave as vanilla.
6. Terraform: select a terraform task -> no vanilla Flatten box/threshold/voxel highlight, only our marks of THIS task; other tasks' marks hidden; nothing shown with no task selected. Vanilla Flatten tasks still show the vanilla box.
Logs: [Movie] speed / host speed / fog lines.

## Replay chains (2026-11-27, one match over several sessions)
1. Play a match (MP or SP with recording), save, play on 1-2 min, leave. Load the save, play, leave. Library: ONE card with the sessions badge "2", length = session 1 up to the save + session 2.
2. Open the card: strip with 2 blocks and a violet branch lane (the part after the save). Rows: play from a session, unlink (Ctrl+Z brings it back), branch row: watch / star = make main.
3. Watch the match: first open prepares session 2 (cover "Preparing the match: session 1/1"), then plays; at the joint the next session opens by itself under the cover. Bar: joints, marks of both sessions, APM of the other session (after it was opened once), sessions button "1/2" with the list.
4. C / V and clicks across the joint, branch lane click, "Make this branch the main line".
5. Merge by hand: link button in the library, pick 2 replays of one map, Merge; different maps are refused.
6. Export (arrow icon in the match details): .btcm in Replays + copied; on another PC drop it into the Replays folder: sessions unpacked, card appears.
7. MP client: the host replay arrives with the links (client library shows the same match).

## Replay files from Windows (2026-11-27)
1. Explorer: double click a .btcr with the game closed -> the game starts and plays it after the main menu (no Steam). With the game open in the menu -> a second window flashes and closes, the running game plays it. In a match -> only a toast, the file is in the list.
2. Copy a .btcr / .btcm in Explorer, Ctrl+V in the game (not in a text field) -> copied to Replays, menu opens on Replays, the card blinks.
3. Drag a .btcr onto the game window -> same. A non-replay file -> toast "only replay files".
4. Replay settings > Playback: switch off -> file types removed (icons gone); exe path field: paste "Copy as path" of another copy -> green check, double click opens that copy; wrong path -> red cross.
8. Seamless joint (Replay settings switch on): at the joint no loading screen, the world goes on; click a far point of another session on the bar -> short hitch, no loading screen; switch off -> the old reload. MP replay with many units: watch for units popping in place or wrong colours of factions after a joint.

## Advanced blueprints (2026-11-28, presets need BC ON)
- Pie on own stockpile / rack marks sets the preset; hover shows a summary; build mode shows icons above marks.
- Click a station mark: the station panel edits the preset (kits, parts, queue stacks with Shift+click, loop, clear, rack limit, helper). Built station gets it.
- Save a compound with 2+ build tasks of different priority and an anchor (LeftAlt+X over a selected mark); place it: anchor under the cursor, LeftAlt+X over another block moves it, marks land in separate new tasks with the saved priorities, presets on the new marks.
- World save with preset marks -> load -> presets kept. MP: client presets on own marks applied by the host; enemy/ally cannot edit; BC OFF: no pie branch, vanilla wire.
