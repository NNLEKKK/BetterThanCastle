# C# replacement of GameMenu.lua (plan, 2026-11-21)

User order: replace the in-game Lua HUD (LUI/Menus/GameMenu.lua) with C#, look almost 1:1 with vanilla, every part customisable
in the UI editor (tab 12, BtcHud slots). Replays: no GameMenu at all, only mini map + selected unit info; HP/stamina bars are C#
overheads (OverheadDisplayDriver), not GameMenu, so they stay.

## Why (measured, scenario gc-probe vs gc-probe-nomenu, sandbox, 60 workers, MemoryFixes on)
| | with GameMenu | without |
|---|---|---|
| live heap after GC | ~1470 MB | ~600 MB |
| full GC pause | 0.72-1.04 s | 0.26-0.44 s |
| fps (prof window) | 67 | 87 |
| world load | ~20 s | 9.6 s |

## Size of the Lua side
GameMenu.lua 4106 lines + 24 files in LUI/Menus/Game (7316 lines: TopBar, LeftBar, LeftContent, RightBar, Sidebar* (Floating, Task,
Home, Map, Stats, Score, Quick, Chat, Alerts), Menu_TaskList 1203, Menu_Popup 665, Menu_Tooltip 633, Menu_Modal 463, Menu_Help,
Menu_Article, IsPausedMenu, Tutorial*) + LUI2 framework + LUI/Data modules (Data_Project etc.). GameMenu.lua also holds the
input-mode key handlers (Projects / Tasks / Bricks / Structures / Blueprints, lines 30-320): they must be ported too.

## Approach: drive the game's own C# builder, not our own widgets
- Lua only DESCRIBES menus; the widgets are C# panel classes in Brix.UI.Builder.Panel (StandardButtonPanel, SquareButtonPanel,
  SidebarTitlePanel, VerticalLayoutPanel, ScrollableLayoutPanel, MinimapPanel, ProgressbarPanel, HeaderPanel, ...), each a prefab
  (`Factory.Instantiate(AssetKey)`), with a non-Lua constructor (`StandardButtonPanel.New(onReset, onRefresh)`, luaDriven=false)
  and menus as BaseMenu subclasses. Building the same panels from C# = same prefabs, sprites, fonts, sizes = 1:1 look for free.
- Data from the game's C# objects (UIGame, Picking, ProjectDatabase, Faction, crystals...) instead of the Lua data modules
  (Castle Story Plus GameUi/GameMenuModel.cs is a reference for which C# objects hold what; its views are its own uGUI, not 1:1).
- Build lazily: a sub-menu (build catalogue, task options) is built the first time it opens, not at load.
- UI editor: each region (top bar, left bar, task panel, task list, mini map, popups) is one BtcHud slot (move/scale/hide/opacity),
  remembered like every other slot.
- Switch Display/CsGameMenu (default OFF until parity, then ON); OFF = vanilla Lua menu, nothing changes.
- Our own Lua injections into GameMenu.lua must get C# twins: cleanup filter buttons (BtcCleanFilter), wormhole ward build button
  (BtcWormWardBuild), altar lines (BtcWormWardCraft), call to arms crystal fix (CallToArmsPatches), BtcLoadBoost early client UI.
- Gamemode ui.lua files still dofile Menu_Modal.lua (intro/victory dialogs): keep Lua there at first.

## Phases (each one: build, hidden lab, user look check, commit)
0. Inventory: one table per Lua file: panels used (class + variant), sizes/anchors, data source, actions, hotkeys, our injections.
1. Replay: skip GameMenu while a replay plays (like aim), add the mini map prefab (`Factory.Instantiate(Images.MiniMap)`, holder 256x256
   sized BEFORE the prefab starts: MiniMap reads its size once) + selected unit info, as BtcHud slots. Quick win, proves prefabs.
2. Frame: top bar, left bar (mode buttons, home, settings, chat, alerts, pause label), input-mode hotkeys.
3. Build: structures, blocks, blueprints library, build options (wards incl. wormhole ward).
4. Tasks: new task panel (repair/harvest/tunnel/landscape/quarry/build options, cleanup filter), priority, population, actions,
   task list, task limit alert.
5. Popups/tooltips/help/articles/modal/paused; tutorial last (or keep Lua for the tutorial mode).
6. Parity pass with the user (side by side), then default ON; MP check (UI is local, wire unchanged: BC OFF stays vanilla).

## DEV tools
- cmd `skipmenu 1` (DEV only) skips GameMenu for the next loads; scenario gc-probe-nomenu.
- cmd `gcprobe <tag>`: full GC time + heap.

## Status (2026-11-21, end of session 1)
- DONE phase 1 (bf0ce6a): replay playback skips GameMenu (Replay/NoGameHud, default on). The mini map is NOT from GameMenu: it lives in
  the gamemode scoreboard menu (LUI/Menus/MinimapMenu.lua, Panels.Minimap = MinimapPanel, asset Images.MiniMap), which still loads;
  BtcReplayMinimap spawns its own prefab only if no Lua one binds. Replay (Komorebi, hidden): heap 1450 -> 620 MB, GC 0.95 -> 0.31 s,
  fps 124 -> 166, world load 9.4 s. HP/stamina bars = C# overheads. "Selected unit panel": no such panel in GameMenu (closest:
  Menu_TaskList shows the selected operable/building name + crew) -> ask the user what they mean.
- Inventories: docs/gamemenu/inv-a.md (LUI2 map + GameMenu 1-1700), inv-b.md (GameMenu 1700-4106, layout, BTC injections), inv-c.md
  (Game/ sub-menus; RightBar, SidebarTask/Stats/Quick/Map are dead code).
- Reference dump: scenario uidump-vanilla -> BTClogs/uidump-vanilla.txt (13 "Menu:" roots, "Menu: Taskbar (Lua)" ~4190 nodes at 640x360).
  DEV cmd `uidump <prefix> <tag>` (underscores = spaces). Parity check = diff of the C# menu dump against it (same resolution).
- Method for phase 2+: instantiate the SAME prefabs directly (Factory.Instantiate(Layouts.Vertical / Horizontal, Buttons.Standard, ...))
  and set texts/sprites/colours/LayoutElement like the Lua panels in LUI/Panels/*.lua (ButtonPanel, LargeButtonPanel, HeaderPanel,
  LabeledDividerPanel, AlertPanel, SpacerPanel, DividerPanel) do; no BaseMenu/Lua machinery. Root = "Menu: BtcTaskbar" under the same
  canvas, same anchors as "Menu: Taskbar (Lua)". Every region = BtcHud slot for the UI editor.
- Next: inventory of LUI/Panels/*.lua used by the game menu + LUI2 colour constants; then the frame (left bar, right bar, top bar,
  LeftContent socket) + input-mode hotkeys, behind Display/CsGameMenu (default off).
- 2026-11-21 PROOF of the method: UI/GameHud/BtcGhKit.cs (Make/Rect/Le/Img/Txt/Group/Icon + RailButton = LeftBarMenu toggle ->
  ButtonPanel.lua) rebuilt Score / Blueprints / New Task of the left rail; DEV cmd ghproto + scenario gh-proto dump both; after
  ignoring rect/pos (spacer cut out) the button subtrees are IDENTICAL to the Lua ones (prefabs, sprites, colours, fonts, layout).
  Gotchas: Image prefab default sprite is UI_ColorWhite (set null where Lua has none); icon keys are "_UI_Xxx" (sprite name UI_Xxx).
- 2026-11-21 phase 2 started: UI/GameHud/BtcGameHud.cs (cfg Display/CsGameMenu, default OFF; built from the GameMenu load skip into
  the LuaMenuComponent object; 4 Hz Tick refresh; Bind() = KeyBindingsUtility.RegisterAction like UIGame.RegisterAction; Loc() =
  I2Helper.TryGet like Lua GetLocalized) + BtcGhRightBar (home F10, settings, help, spacer, back to work X, call to arms C: working
  actions + hotkeys, same C# bodies as UIGameObserver.lua_BackToWork/lua_CallToArms). Scenario gh-hud (set CsGameMenu = true in the
  game cfg first, restore after): texts/sprites/hotkeys of the right bar = vanilla. With the C# menu: load 9.5 s, heap ~580 MB,
  GC 0.24-0.37 s. TODO right bar: settings + help sidebars, the dev buttons Debug/Editor/movie that vanilla shows (TestDebugSidebar,
  TestMovieSidebar, CreativeSidebar), chat/alerts (SidebarHomeMenu). NEXT: left bar (inv-a 2.9: score, blueprints, blocks,
  structures, new task, spacers, task limit) + LeftContent socket + SidebarFloatingMenu, input-mode hotkey sets (inv-data, GameMenu
  lines 30-320), then the task list (Menu_TaskList) and popups.
- 2026-11-21 session 2 (af9cd83, user order "max optimisation"): left bar + left content + sidebars Blocks / Structures (+ wormhole
  ward button twin) / New task (spacer, too-many-tasks alert, maintenance/harvest/mining/construction grids) + input modes
  (UI/GameHud/BtcGhModes.cs = GameMenu.lua 30-320 lists incl. quirks) + UI editor slots gh.left / gh.leftContent / gh.right
  (BtcHudSlots, applied from BtcUiEdit.Apply). Files: BtcGhKit (prefab kit + BtcGhButton), BtcGhLeft (state machine of mg.left,
  selection from UIGameObserver.projects, signals Project.lua_Selected / ProjectStarted / ProjectDeleted /
  PickingObserver.lua_OverridePickerSetWithOptions / CraftingStation.lua_OnSetSelected; BtcGhSidebar shell), BtcGhBuild (Meta
  tables copied as arrays), BtcGhNewTask (UIGameObserver.lua_New* / lua_IsPlacing* called with default() args: MoonSharp
  ScriptExecutionContext/CallbackArguments are STRUCTS here). Perf: no Update/poll at all (the 4 Hz ticker is gone), sidebars built
  on first open (blocks 49 ms / structures 70 ms / new task 37 ms), hidden Lua skipMap nodes not created. Scenario gh-left
  (DEV cmd `gh blocks|structures|newtask|open <Side>`): blocks from idle -> build project -> blocks sidebar auto-opens; uidump
  diff of all three sidebars + left rail vs vanilla = identical except the hidden nodes we skip.
  TODO next: blueprints sidebar (name field, saved list 16 thumbnails, load/publish/delete), task list (Menu_TaskList), popup
  (task options, priority, actions, operable recipes/queue: fills BtcGhModes.QueueSlot/TaskActionSlot/RaisePriority/...),
  right sidebars (settings, help), top bar, paused overlay, tooltips; cleanup filter popup twin (BtcCleanFilter); scoreboard
  hide = CanvasGroup of "Menu: Scoreboard (Lua)".
- 2026-11-21 session 2 (later, 8ecfa18..ca7deaa): blueprints sidebar (BtcGhBlueprints: name field, library, compounds pooled,
  library load = InitializeCompoundDictionary spread 1 compound/frame, thumbnails 1/frame), settings sidebar in the right content
  (BtcGhRightBar.FillSettings), paused plate (BtcGhPaused), task list (BtcGhTaskList, sonnet agent: idle/operable/projects/
  fighters, crew faces, clicks with shift/ctrl), task popup (BtcGhPopup*.cs, sonnet agent: all Menu_Popup sections incl. crafting
  and BTC cleanup filter, fills BtcGhModes key slots; BtcGhPopupTrack places it over the selected task one frame later). UI editor
  slots gh.left/leftContent/right/rightContent/tasks/popup (+gh.paused registered, no Elem). Labs gh-left, gh-tasks: every dumped
  region identical to vanilla except hidden nodes (idle popup 38/38). Menu build 64 ms at load (Lua GameMenu ~5 s; the blueprint disk scan was moved behind the first coroutine yield;
  check), heap ~600 MB, GC 0.27-0.42 s (Lua menu: ~1470 MB / 0.7-1.0 s).
  NOT DONE: help sidebar (Menu_Help + LTD.lua sandbox tutorial), tooltips (Menu_Tooltip: popup/task-list hover tooltips, arrows
  hooks BtcGhTaskList.TooltipSet exist), dev buttons Editor/movie/creative (TestDebugSidebar, TestMovieSidebar, CreativeSidebar),
  top bar (hidden in vanilla), chat/alerts sidebars, world editor mode. NOT hand-tested by the user at all.
- 2026-11-22 dev sidebars (UI/GameHud/BtcGhDev.cs): right bar buttons Editor (TestDebugSidebar.lua) and movie (TestMovieSidebar.lua),
  visible while Architecte.allowDebugMenu (host rule gen.editor via BtcEditorShare, settings "toggle debug"; setter patch + SetAllow refresh
  the rail and close an open dev sidebar). UI = BtcGhSidebar + kit (dividers, 64 px icon grids, wide text buttons, Buttons.Dropdown,
  Sliders.StandardWithInput, InputFields.ReskinInput prefabs); actions = the original Lua statements run in our own script
  (LuaLoader.LoadString + LuaLibs.BigLib, killed on LeaveGame; prelude: selection hooks, GetRounded, spawn/raid helpers). Vanilla raid
  buttons used an undefined `raid` and errored: fixed. Omegatron / preroll buttons (vanilla "Missing function") left out. Scenario gh-dev
  (DEV gh allowdebug|editor|movie|devrun|deveval|devcount): spawns +6 units, values read back. CreativeSidebar (player Debug) not ported.
  NOT hand-checked: look, dropdown lists open, slider drag, MP client spawns (go through BtcEditorShare msg 67 like the Lua panel).
