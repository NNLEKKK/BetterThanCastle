# GameMenu.lua inventory B (lines 1700-4106)

Source: ref/lua/LUI/Menus/GameMenu.lua (vanilla, clean reference). Line numbers refer to that file.
Conventions:
- `||expr` = lazy getter (re-evaluated on refresh). Plain `expr` = fixed value at build time.
- Every button is `MenuHandle.New()`. `ButtonHandle`, `SectionHandle`, `DividerHandle` are aliases of `MenuHandle` (GameMenu.lua:530-532), so the constructor is the same for all four kinds.
- Panels: `LayoutPanels.Vertical:New()` / `LayoutPanels.Horizontal:New()` with `.SetWidgetHandle(Widgets.<W>, Handles.<H>, getter, isConst)` chains (isConst=true used everywhere, inferred from use).
- Colour names (CastleRed, Gray, White, _colorYellow, _colorGreen, _colorRed, _colorGray) are globals, NOT defined in 1700-4106 (not located, not checked).
- Lua data modules are loaded in ref/lua/LUI2.lua:208-218: `Data.<Name> = dofile("LUI/Data/Data_<Name>.lua")` for Faction, FireflyNest, Operable, Project, Recipe, Resource, Sound, Storage, Article. These files were NOT read in this pass.
- Engine calls (UIGame.*, Picking.*, BlueprintSelection.*, Project.*, Rules.*, Meta.*, IconKeys.*, MenuCalls.*) are globals from the C# side. Only the names are listed; C# counterparts were NOT verified in ref/vanilla/Brix/UI/Builder/Panel/*.cs.
- Hotkey labels: `UIGame.GetKeyBindingCodename("Projects", <action>)` (action names given per row).
- Common shape of a new-task button: IconColor = `Project:ProjectUnderCap() and White or Gray`, IconSize 32, FrontImage 64x64, hasFgLabel, OnAction guarded by `not UIGame.IsPaused()` and `Project:ProjectUnderCap()`, Highlight = `UIGame.IsPlacing<X>() and HighlightMode.Bottom or HighlightMode.None`, refresh via `h.ev_refresh` on `_m.mh.newTask.onOpen`, toggle via `m.AddMenuHandleToggle(h, _m.sh.newHarvestTask | newMiningTask | newConstructionTask)`, registered with `_m.mg.newTask:AddChild(h)` and `_m.bh.<name> = h`.

## 1. Task panel: new-task menu (_m.mh.newTask, 1716-2019)

### 1a. Harvest (header 1716-1726)
- Header: SectionHandle, label `##gamemenu_taskbar_harvesttasks`, CastleRed, `_m.sh.newHarvestTask`, added on `mh.newTask.onLoad`.

| line | item | icon key | label key | hotkey action | click | Highlight when |
|---|---|---|---|---|---|---|
| 1728-1751 | wood | _Cut_Tree | ##gamemenu_taskbar_trees | project_CreateWoodTask | UIGame.NewHarvestTask_Trees() | IsPlacingNewHarvestTask_Trees |
| 1753-1776 | plants | _Harvest_Plant | ##gamemenu_taskbar_plants | project_CreatePlantsTask | UIGame.NewHarvestTask_Plants() | IsPlacingNewHarvestTask_Plants |
| 1778-1801 | cleanup | _Cleanup | ##gamemenu_taskbar_cleaning | project_CreateCleaningTask | UIGame.NewCleanupTask() (no IsPaused early return, expression form) | IsPlacingNewCleanupTask |
| 1803-1826 | iron | _Harvest_Iron | ##gamemenu_taskbar_iron | project_CreateIronTask | UIGame.NewHarvestTask_Iron() | IsPlacingNewHarvestTask_Iron |
| 1828-1851 | brimstone | _Harvest_Orange | ##gamemenu_taskbar_brimstone | project_CreateBrimstoneTask | UIGame.NewHarvestTask_Brimstone() | IsPlacingNewHarvestTask_Brimstone |
| 1853-1876 | boulder | _Harvest_Stone | ##gamemenu_taskbar_boulders | project_CreateBoulderTask | UIGame.NewHarvestTask_Boulders() | IsPlacingNewHarvestTask_Boulders |
| 1878-1901 | all harvest | _Harvest | ##gamemenu_taskbar_harvest | project_CreateHarvestTask | UIGame.NewHarvestTask_All() | IsPlacingNewHarvestTask_All |

- `bh` names: newHarvestWoodTask, newHarvestPlantsTask, newCleanupTask, newHarvestIronTask, newHarvestBrimstoneTask, newHarvestBoulderTask, newHarvestTask.
- Cleanup, plants and wood differ from the rest only in statement order. The quarry row (1b) lacks hasFgLabel.

### 1b. Tunnel / mining (header 1905-1914, label ##gamemenu_taskbar_miningtasks, `_m.sh.newMiningTask`)

| line | item | icon | label key | hotkey | click | Highlight |
|---|---|---|---|---|---|---|
| 1916-1939 | tunnel | _Tool_Pickaxe | ##gamemenu_taskbar_tunnel | project_CreateTunnelTask | UIGame.NewTunnelTask() | IsPlacingNewTunnelTask |
| 1941-1964 | landscape | _Break_Voxel | ##gamemenu_taskbar_landscape | project_CreateLandscapeTask | UIGame.NewLandscapeTask() | IsPlacingNewLandscapeTask |
| 1966-1988 | quarry | _Tool_Shovel | ##gamemenu_taskbar_quarry | project_CreateQuarryTask | UIGame.NewQuarryTask() | IsPlacingNewQuarryTask |

- Quarry: no hasFgLabel (lines 1970-1974), so its label is drawn differently from tunnel and landscape.

### 1c. Build (header 1990-1998, label ##gamemenu_taskbar_constructiontasks, `_m.sh.newConstructionTask`)
- 2000-2019: build, icon _Build_Brick, label ##gamemenu_taskbar_build, hotkey project_CreateBuildTask, click UIGame.NewBuildTask(). No Highlight. `_m.bh.newTaskBuild`.

## 2. Blueprint actions and saved list (_m.mh.blueprintsMenu, 2020-2194)

Shared state (2024-2035): `maxCompound = 16`; `compoundList` = BlueprintSelection.GetCompoundList(), refreshed by UpdateCompoundList (2026-2030), which also invokes `_m.OnUpdateCompounds`.

| line | item | type | icon | label / text | visibility / enable | click |
|---|---|---|---|---|---|---|
| 2037-2045 | header "actions" | Section | - | ##gamemenu_taskbar_blueprintactions, CastleRed | always | - |
| 2047-2068 | name input (InputField) | ButtonHandle, no label | - | IconColor CastleRed when: selection exists, count < 16, name non-empty | refresh on onBPSelectionChanged, OnCompoundDeleted | OnAction -> UIGame.UpdateBlueprintThumbnail() (same conditions). Screenshot callback 2061-2065: DuplicateAndSave, UpdateCompoundList, onSaveCompoundBlueprint |
| 2070-2082 | header "saved" | Section | - | ##gamemenu_taskbar_blueprintssaved + " (n/16)" | hideHeader=false | - |
| 2084-2100 | Load selected | ButtonHandle | LoadBlueprints (32px) | ##gamemenu_taskbar_loadblueprint | CastleRed if HasSelectedAny, else Gray | BlueprintSelection.LoadSelected() |
| 2101-2123 | Publish to Steam Workshop | ButtonHandle | _UI_Steam (32px) | ##gamemenu_taskbar_publishblueprint | CastleRed if selected AND Steam connected AND not from workshop | SendToWorkshop(), UpdateCompoundList, notification ##gamemenu_taskbar_blueprintpublished 3 s |
| 2124-2140 | Delete | ButtonHandle | UI_Trash (32px) | ##gamemenu_taskbar_blueprintdelete | Color(1,0.1,0.1,1) if selected and not from workshop | DeleteSelected(), UpdateCompoundList |

- Load, Publish and Delete refresh on OnCompoundDeleted and OnCompoundSelected, and join via `AddMenuHandleToggle(h, _m.sh.blueprintsMenuInfo)` on load.

### 2b. Saved list (2142-2194)
- Header 2144-2156: SectionHandle, label ##gamemenu_taskbar_blueprintssaved + " (count/16)", CellSize (256,40), hideHeader=true, `_m.sh.blueprintsMenuList`. Refreshed on OnUpdateCompounds.
- Content 2159-2194: loop of 16 ButtonHandles (built once; hidden when the slot is empty):
  - Visible = index <= #compoundList (2163).
  - Icon = BlueprintSelection.GetThumbnailFromFile(compoundList[i]), IconSize 96 (2164, 2166).
  - IconColor White unless instantiating, then Gray (2165).
  - Count = compoundList[i] (the name text, 2168). Label anchored 0.5/0.5, MiddleLeft, OffsetX 70, hasFixedLabel (2169-2173). The Label line (2167) is commented out.
  - Width 240, Height 40, FrontImage 60x40 at OffsetX -90 (2177-2181).
  - OnAction -> BlueprintSelection.SelectCompound(name) unless instantiating (2175).
  - Highlight = HighlightMode.All when GetCurrentSelected() == name (2176).
  - Refresh triggers: onSetPickerOptions, OnCompoundSelected, onSaveCompoundBlueprint, OnDoneInstantiating.
  - Joins via `AddCompoundHandleToggle(h, _m.sh.blueprintsMenuList)` (2188).
  - Ordering = order of BlueprintSelection.GetCompoundList() (C# side, not checked).

## 3. Build > Structures (_m.mh.buildStructures, 2196-2415)

Common rule (all six groups): `local meta = Meta.Structure; for i = 1, #meta do local k = meta[i]; if meta[k].groupId == N then ... end end`. Each button: Icon = meta[k].Icon (fixed), IconColor White, IconSize 32, Label = meta[k].Name (fixed string), hasHotkey, HotkeyLabel_Text = `UIGame.GetKeyBindingCodename("Projects", meta[k].Hotkey)`, hasFgLabel, FrontImage 64x64, OnAction = `not UIGame.IsPaused() and Picking.SetHeldBlueprint(k)`, Highlight = `_m.GetHeldBlueprintHighlightMode(k)`. Refresh: `Data.Project.ev_onSetHeldBlueprint` (only when the menu is open) and `buildStructures.onOpen`. Joins via `AddMenuHandleToggle(h, <section>)` on `mh.buildStructures.onLoad`.

| line range | groupId | section header (label key) | sh name |
|---|---|---|---|
| 2197-2232 | 1 | ##gamemenu_taskbar_storage (2201) | buildStructuresStorage |
| 2233-2268 | 2 | ##gamemenu_taskbar_production (2237) | buildStructuresTransformation |
| 2269-2304 | 3 | ##gamemenu_taskbar_crafting (2273) | buildStructuresCrafting |
| 2305-2340 | 4 | ##gamemenu_taskbar_structures (2309) | buildStructuresProps |
| 2342-2377 | 5 | ##gamemenu_taskbar_wards (2346) | buildStructuresWards |
| 2379-2414 | 6 | ##gamemenu_taskbar_decorations (2383) | buildStructuresDecorations |

- Headers: SectionHandle, CastleRed, `mh.buildStructures.onLoad -> m.AddSection(h)`.
- The wards group (2342-2377) is the anchor used by the wormhole-ward injection (section 13).

## 4. Build > Stone blocks (_m.mh.buildStoneBlocks, 2418-2597)

Same button shape as section 3, but the loop uses `Meta.StoneBlock` and `_m.mh.buildStoneBlocks`. Refresh via `Data.Project.ev_onSetHeldBlueprint` when the stone menu is open.

| line range | groupId | header label key | sh name |
|---|---|---|---|
| 2420-2455 | 1 | ##gamemenu_taskbar_stoneblocks (2424) | buildStoneBlocksStandardBricks |
| 2456-2491 | 2 | ##gamemenu_taskbar_plugs (2460) | buildStoneBlocksPlugs |
| 2492-2527 | 3 | ##gamemenu_taskbar_roundbricks (2496) | buildStoneBlocksRoundBricks |
| 2528-2562 | 4 | ##gamemenu_taskbar_crenelations (2532) | buildStoneBlocksCrenelations |
| 2564-2597 | 5 | ##gamemenu_taskbar_woodblocks (2568) | buildWoodBlocks (name has no "StoneBlocks") |

## 5. Task goals (2603-2654)

| line | item | Visible | icon / colour | label | count | tooltip | parent |
|---|---|---|---|---|---|---|---|
| 2604-2617 | section (hidden header) | Project:IsSelectedNotOfTypeIdle() | - | ##task_goals, hideHeader | - | - | sh.goals |
| 2619-2636 | goals (non-build) | not idle AND not IsSelectedOfTypeBuild() | UI_Flag 32px, _colorYellow | ##task_goals | GetSelectedGoalCount() as string, "0" if idle | {"Task","All","Goals"} | mg.projectContext |
| 2638-2654 | goals (build) | not idle AND IsSelectedOfTypeBuild() | _UI_Brick 32px, _colorYellow | ##task_goals | same | {"Task","All","Goals"} | mg.projectContext |

- Both MenuHandles are toggled by `_m.sh.goals.ev_onLoad`. Refresh: `_m.onSetSelectedProject`.
- Two buttons share one label; only one is visible at a time (build vs non-build).

## 6. Idle task (Bricktron population and crystal actions, 2657-2835)

### 6a. Population section
- Section 2659-2673: Visible = IsSelectedOfTypeIdle() AND not Data.Operable:HasSelected(); label ##task_idle_population; hideHeader; `_m.sh.idleTaskPopulation`. Refresh: onSetSelectedProject, Operable.ev_onSetSelected.
- Image 2675-2694: ButtonHandle, Visible true, Icon _Bricktron 48px, IconColor White, hasNoHover, tooltip {"Task","Idle","SpawnUnit"}, parent `mg.projectContext`, bh `idleTaskPopulationImage`. Refresh: selection change, FireflyNest.ev_onSetSpawnableUnitCount (registered in ev_onLoad). No OnAction.
- Count 2697-2723: transparent icon (0,0,0,0), CountColor CastleRed, CountFontSize 12, MiddleCenter, transparent hover. Count function = `UIGame:CountBricktrons() .. " / " .. Rules.bricktronCap` (2707-2710). Assigns GLOBAL `currentBricktrons` (2708). Tooltip {"Task","Idle","Population"}. Refresh: onBricktronPopChanged plus selection. bh `idleTaskPopulationCount`.

### 6b. Dead Bricktrons (2726-2750)
- ButtonHandle, Visible true, Icon Skull 32px, IconColor White if FireflyNest:HasAutoRespawnFireflyCount(), else Gray. Count = ##task_idle_dead text, CountColor CastleRed or Gray, HoverColor transparent.
- Hotkey label = GetAutoRespawnFireflyCount() when non-zero, else "" (2738), UpperRight.
- Tooltip {"Task","Idle","Dead"}. No OnAction. Refresh: selection, FireflyNest.ev_onSetAutoRespawnFireflyCount.
- Stored as `_m.bh.idleTaskActionsMortuary` (2749), a name that does not match its position in the population block.

### 6c. Crystal actions (2753-2835)
- Divider 2753-2763: ButtonHandle, Visible = IsSelectedOfTypeIdle() and not Operable:HasSelected(); `_m.dh.idleTaskPopulation`.
- Section 2765-2779: same Visible; label ##task_idle_crystal; hideHeader; `_m.sh.idleTaskActions`.
- Progress 2781-2808: ButtonHandle with `Path = "LUI/Panels/Panel_ProgressHorizontal.lua"` (2784), a sub-panel added with `m.AddPanelHandle(h, sh)` on `sh.idleTaskActions.ev_onLoad` (2803). Progress = FireflyNest:GetProgress(). ProgressText ##task_idle_energy. ProgressCount: if bricktrons >= Rules.bricktronCap then ##task_idle_maxenergy, else FireflyNest:GetProgressText(). Tooltip {"Task","Idle","Progress"} (marked TODO in source). Refresh: ev_refresh and ev_refreshProgress (fed by FireflyNest.ev_onSetProgress). Assigns GLOBAL `currentBricktrons` (2789).
- Spawn unit 2810-2834: Icon _UI_Bricktron 32px; IconColor and CountColor CastleRed if FireflyNest:HasSpawnableUnitCount(), else Gray; Count = ##task_idle_spawn; OnAction = `HasSpawnableUnitCount() and FireflyNest:SpawnUnit() or nil` (2819). NOTE: unlike the other actions there is no `not UIGame.IsPaused()` guard here. Tooltip {"Task","Idle","SpawnUnit"}; hotkey label = GetSpawnableUnitCount() when non-zero. Refresh: selection, ev_onSetSpawnableUnitCount.

## 7. Task priority (2838-2908)
- Divider 2840-2849: ButtonHandle, Visible = IsSelectedNotOfTypeIdle(); `_m.dh.priority`.
- Section 2851-2864: same Visible; label ##task_priority; hideHeader; `_m.sh.priority`.
- Lower 2866-2886: ButtonHandle, Visible true, label ##task_actions_priority_lower, icon UI_Minus 32px, IconColor CastleRed when selected and priority ~= Priority.Locked, else _colorGray. OnAction = not paused, HasSelected, `Project.LowerPriority(sel)`. Hotkey project_LowerPriority. Tooltip {"Task","All","LowerPriority"}. Refresh: onSetSelectedProject, onSetPriority. Toggled via `sh.priority.ev_onLoad`. Stored as `_m.bh.priorityLower`.
- Raise 2888-2908: UI_Plus 32px, ##task_actions_priority_raise, colour CastleRed when priority ~= Priority.High, OnAction `Project.RaisePriority(sel)`, hotkey project_RaisePriority, tooltip {"Task","All","RaisePriority"}. Stored as `_m.bh.priorityRaise`.
- Lower and raise carry no FrontImage and no hasFgLabel (icon + label row style).

## 8. Task actions: ok / apply / cancel / delete (2913-3023)
- Divider 2915-2924: ButtonHandle, Visible = IsSelectedNotOfTypeIdle(); `_m.dh.taskActions`.
- Section 2926-2939: same visibility; label ##task_actions; `_m.sh.taskActions`.

| line | item | Visible | icon (32px) | label key | colour | click |
|---|---|---|---|---|---|---|
| 2941-2959 | ok | ShowSelectedStatusAction(Project.StatusAction.Ok) | UI_Check | ##task_actions_ok | _colorGreen if HasSelectedStatusAction(Ok), else gray | not paused -> SetSelectedStatusAction(Ok) |
| 2961-2979 | apply | ShowSelectedStatusAction(Apply) | UI_Down | ##task_actions_apply | _colorYellow if HasSelectedStatusAction(Apply) | SetSelectedStatusAction(Apply) |
| 2981-2999 | cancel | ShowSelectedStatusAction(Cancel) | UI_X | ##task_actions_cancel | _colorRed if HasSelectedStatusAction(Cancel) | SetSelectedStatusAction(Cancel) |
| 3001-3022 | delete | ShowSelectedStatusAction(Delete) (MenuHandle) | UI_Trash | ##task_actions_delete | _colorRed unless BlueprintSelection.IsInstantiating() | not paused, not idle, not instantiating -> SetSelectedStatusAction(Delete) |

- ok/apply/cancel/delete are toggled through `sh.taskActions.ev_onLoad`; `_m.bh.taskActionsOk|Apply|Cancel|Delete`.
- No hotkeys on these four rows.

## 9. Operable (crafting station) panels (3026-3322; in range, outside the requested list)
- 3029-3038 divider; 3041-3055 section `operableRecipes` (##operable_craft); 3057-3087 recipe slots (ButtonHandle, Visible = HasSelected and HasRecipeAtSlotIndex; Label Data.Recipe:GetName, Icon Data.Recipe:GetIcon; OnAction Data.Operable:AddToQueue(recipe)); refresh on selected/queue.
- 3091-3101 divider; 3103-3116 section `operableResourceInput` (##operable_resources_requirements); 3119-3142 input rows (MenuHandle, Count "have/need" CastleRed; Label/Icon from Data.Resource).
- 3147-3157 divider; 3159-3172 section `operableResourceOutput` (##operable_resources_finished); 3182-3205 output rows (Count = content count, CastleRed).
- 3209-3219 divider; 3221-3234 section `operableQueue` (##operable_queue); 3237-3259 queue rows (click = Data.Operable:RemoveFromQueue(i); Highlight All for slot 1).
- 3262-3273 divider; 3275-3298 looping toggle (`mh.toggleLoop`, IconKeys._UI_Wheel, CastleRed when looping, click SetIsQueueLooping(not ...)).
- 3300-3322 clear queue (`mh.clearQueue`, _UI_X red, click Data.Operable:ClearQueue(), hotkey project_ClearQueue, tooltip {"Operable","Queue","Clear"}).

## 10. Settings / top-menu buttons (3328-3530)

Header 3330-3339: SectionHandle, label ##gamemenu_topmenu_settings, hideHeader, `_m.sh.settings`, on `mh.settings.onLoad -> AddSection`. All buttons: `_m.mg.settings:AddChild(h)` and `mh.settings.onLoad -> AddMenuHandleToggle(h, _m.sh.settings)`. Icon colour CastleRed for all.

| line | item | guard | icon | label key | click |
|---|---|---|---|---|---|
| 3341-3354 | pause | `if not IsMultiplayer() and not IsWorldEditor()` (3341) | UI_Pause | ##gamemenu_topmenu_pause | _m.TogglePaused() |
| 3356-3368 | quicksave | same as pause (block ends 3369) | UI_Quicksave | ##gamemenu_topmenu_quicksave | MenuCalls.QuickSave() |
| 3371-3383 | screenshot | NONE (outside the MP guard) | UI_Screenshot | ##gamemenu_topmenu_screenshot | MenuCalls.TakeScreenShot() |
| 3385-3398 | mute | NONE (outside the MP guard) | icon = Sound.mute and UI_Mute or UI_VolumeHigh (lazy) | ##gamemenu_topmenu_mute | `Sound.mute = not Sound.mute; h.ev_refresh:Invoke()` |
| 3400-3413 | xray | `if not IsWorldEditor()` (3400) | UI_XRay | ##gamemenu_topmenu_xray | MenuCalls.ToggleXRay() |
| 3416-3428 | allow debug menu | inside the 3400 block | UI_Console | ##gamemenu_topmenu_toggledebug | ToggleAllowDebugMenu() |
| 3431-3442 | grid | COMMENTED OUT (`--[[` at 3431, `--]]` at 3442) | Grid | str_we_grid_menu_title | UIGame.SetGridVisibility(not GetGridVisibility()) |
| 3446-3458 | fog of war | `if IsBuildDebug()` and not WorldEditor | UI_FogOfWar | ##gamemenu_topmenu_fogofwar | MenuCalls.ToggleFogOfWar() |
| 3462-3473 | debug | `if IsBuildDebug()` | UI_Console | ##gamemenu_topmenu_toggledebug | MenuCalls.ToggleDebugs() |
| 3476-3488 | speed x2 | `if IsBuildDebug()` | UI_Plus | ##gamemenu_topmenu_speedx2 | MenuCalls.ToggleSpeedX2() |
| 3490-3502 | audio debug | `if IsBuildDebug()` | UI_VolumeHigh | ##gamemenu_topmenu_audiodebug | MenuCalls.SetAudioDebug(not GetAudioDebug()) |
| 3504-3515 | memory collection | `if IsBuildDebug()` | UI_Trash | ##gamemenu_topmenu_gc | MemoryCollection() |
| 3518-3530 | editor camera | `if IsBuildDebug()` | UI_TerrainHug | ##gamemenu_topmenu_editorcamera | ToggleDebugCamera() |

- Mute is the only button whose icon is a lazy getter tied to global state (`Sound.mute`).
- The IsBuildDebug block starts at 3444; the fog entry sits in a nested `if not IsWorldEditor()` at 3445.

## 11. Layout blocks (3536-3867)

Panel defaults for all blocks: Image Enabled=false and Raycasts=false unless noted.

| line | block | dofile() | panel | anchor min/max | pivot | size / offsets / position | tint | stored as |
|---|---|---|---|---|---|---|---|---|
| 3539-3568 | top bar | LUI/Menus/Game/TopBarMenu.lua | Vertical | (0.5,1)/(0.5,1) | (0.5,1) | W 320, H 64, pos 0,0 | - | _m.menu_top |
| 3570-3586 | (container) | none | Horizontal | - | - | flex 1/1, spacing 0 | - | _m.lyt |
| 3588-3611 | left bar | LUI/Menus/Game/LeftBarMenu.lua | Vertical | (0,0)/(0,1) | (0,0.5) | W 48 | - | _m.menu_leftBar |
| 3613-3629 | center content | none | Vertical | - | - | flex 1/1 | - | _m.lyt.centerLyt |
| 3631-3646 | center padding | none | Horizontal | - | - | flex 1/1 | - | child of centerLyt |
| 3648-3671 | left content overlay | LUI/Menus/Game/LeftContentMenu.lua | Horizontal | (0,0)/(0,1) | (0,0.5) | offsetMin (48,154), offsetMax (48,0), W 272 | - | _m.menu_leftContent |
| 3673-3699 | task list | LUI/Menus/Game/Menu_TaskList.lua | Horizontal | (0,0)/(1,0) | (0.5,0) | H = _m.projectMenuHeight (= 72, 3694), offsets (49,0)/(-49,0), spacing 1 | - | _m.menu_projectList |
| 3701-3730 | popup menu | LUI/Menus/Game/Menu_Popup.lua | Horizontal | (0,0)/(1,0) | (0.5,0) | H 72 (popupMenuHeight), posY = projectMenuHeight + 8 = 80 (3725) | blue 0.5,0.5,1,0.25 | _m.menu_popup |
| 3732-3761 | tooltip menu | LUI/Menus/Game/Menu_Tooltip.lua via loadfile(...)({doShowTooltips=true}) | Horizontal | (0,0)/(1,0) | (0.5,0) | H 192 (64*3), posY = popupPosY + popupH + 8 = 160 (3756) | green 0.5,1,0.5,0.25 | _m.menu_tooltip |
| 3763-3791 | tooltip task menu | Menu_Tooltip.lua (second instance) | Horizontal | (0,0)/(1,0) | (0.5,0) | H 192, posY = projectMenuHeight + 8 = 80 (3786) | red 1,0.5,0.5,0.25 | _m.menu_tooltipTask |
| 3793-3816 | right content overlay | LUI/Menus/Game/LeftContentMenu.lua (same file as left) | Horizontal | (1,0)/(1,1) | (1,0.5) | offsetMin (-48,154), offsetMax (-48,0), W 272 | - | _m.menu_rightContent |
| 3819-3844 | right bar | LUI/Menus/Game/LeftBarMenu.lua (same file as left bar) | Vertical | (1,0)/(1,1) | (1,0.5) | W 48 | - | _m.menu_rightBar; `_m.mg.right.onAnyOpen -> RefreshAll()` (3842) |
| 3846-3867 | is paused | LUI/Menus/Game/IsPausedMenu.lua | Vertical | (0,0)/(1,1) | - | full stretch | - | _m.menu_isPaused |
| 3869-3891 | modal | LUI/Menus/Game/Menu_Modal.lua | Vertical | full | - | full | - | COMMENTED OUT (`--[[` 3869, `--]]` 3891) |

Notes:
- Both bars use the same menu file (LeftBarMenu.lua), and both content overlays use LeftContentMenu.lua. The right variants differ only in anchors and pivot.
- Left bar = mode and section rail (score, blueprints, newTask, buildStructures, buildStoneBlocks, task count display).
- Right bar = home, settings, tutorial, creative, debug, movie, backtowork, calltoarms.
- Rows 3673-3791 form the bottom strip: task list at the bottom, popup above it, tooltip above the popup, and a second tooltip above the task list.

### 11b. Coroutine that wires the menus (3898-4023)
- Runs in `toCoroutine` (3898). Each `coroutine.yield()` splits the work.
- Yield 1 (3900-3905): top menu gets topSpacer, topLabel.
- Yield 2 (3907-3919): left bar gets score, blueprintsMenu, newTask, buildStructures, buildStoneBlocks toggles; spacers; taskLimit display; hoverable when IsSelectedNotOfTypeIdle.
- Yield 3 (3921-3926): left content sockets for blueprintsMenu, newTask, buildStructures, buildStoneBlocks.
- Yield 4 (3928-3935): right bar toggles home, settings, tutorial, creative, debug, movie.
- Yield 5 (3937-3941): right bar backtowork, calltoarms.
- Yield 6 (3943-3946): right content sockets settings, tutorial. `_m.menu_help` = the menu of the tutorial entry (3948).
- Non-WorldEditor (3950-3956): right content sockets for debug, movie, creative.
- Task list (3958-3963): `.SetPopupMenu(menu_popup).SetTaskTooltipMenu(menu_tooltipTask).Begin()`.
- Popup (3965-4003): ordered sections and dividers: goals, build, landscape, quarry, tunnel, harvest (`_m.sh.harvest` at 3981, the anchor for the cleanup injection), operableRecipes, operableResourceInput/Output/Queue, priority, taskActions, idleTaskPopulation, idleTaskActions; then `.SetTooltipMenu(menu_tooltip)`.
- Tooltip init (4010-4013): Data.Article:Init(coroutine); LoadArticleMenus on menu_tooltip, menu_tooltipTask, menu_help.
- Tutorial auto-open (4018-4020) if TutorialConditionsAreTrue().

## 12. Init, flags and event hooks (4028-4106)

Init (4030-4032): Data.Project:OnSetSelected(), Data.Storage:OnSetContent(), Data.FireflyNest:OnSetInfo().

Flag callbacks (4039-4049):
- OnOpen: ActivateInputModeProjects(); ConnectLTD() if defined.
- OnClose: DisconnectLTD() if defined; DeactivateInputModeProjects().

Flags (4096-4100): MenuFlag.OnOpen = OnOpen, OnClose = OnClose, OpenedByParentMenu, OpensChildMenus, OpenOnLoad.

`_m.ConnectWhen(source hook, handler, MenuState.Opened)`, every line gated on Opened:

| line | source hook | handler |
|---|---|---|
| 4056 | Project.hk_OnSetStatus | _m.onSetStatus:Invoke() |
| 4058 | Project.hk_OnProjectSelected | Data.Project:OnSetSelected() |
| 4059 | Project.hk_OnProjectDeleted | _m.onDeleteProject:Invoke() |
| 4060 | Project.hk_OnSetCrewInfoPriority | _m.onSetPriority:Invoke() |
| 4062 | Project.hk_OnSetCrewInfo | _m.onBricktronPopChanged:Invoke() |
| 4064 | Picking.hk_OnSetPicker | _m.onSetInputMode:Invoke() |
| 4065 | Picking.hk_OnSetPickerOptions | _m.onSetInputMode:Invoke() |
| 4066 | Picking.hk_OnSetPickerOptions | _m.onSetPickerOptions:Invoke() |
| 4067 | Picking.hk_BPSelectionChanged | _m.onBPSelectionChanged:Invoke() |
| 4068 | BlueprintSelection.hk_OnDoneInstantiating | _m.OnDoneInstantiating:Invoke() |
| 4069 | BlueprintSelection.hk_CompoundSelectionChanged | _m.OnCompoundSelected:Invoke() |
| 4070 | BlueprintSelection.hk_OnCompoundDeleted | _m.OnCompoundDeleted:Invoke() |
| 4073 | fy_TunnelProject.hk_OnSetTunnelType | _m.onSetTunnelType:Invoke() |
| 4074 | fy_TunnelProject.hk_ResourceTypeKeptChanged | _m.onResourceTypeKeptChanged:Invoke() |
| 4075 | fy_QuarryProject.hk_ResourceTypeKeptChanged | _m.onResourceTypeKeptChanged:Invoke() |
| 4077 | UIGame.hk_OnSetShowHelpAtGameStart | _m.onSetShowHelpAtGameStart:Invoke() |
| 4078 | UIGame.hk_ShowHelp | _m.showHelp:Invoke() |
| 4079 | UIGame.hk_OnSetAllowDebugMode | _m.onSetAllowDebugMode:Invoke() |
| 4080 | UIGame.hk_OnUpdateMapThumbnail | _m.onUpdateMapThumbnail:Invoke() |
| 4081 | UIGame.hk_OnUpdateBlueprintThumbnail | _m.onUpdateBlueprintThumbnail:Invoke() |
| 4082 | UIGame.hk_OnBlueprintScreenshotTaken | _m.OnBlueprintScreenshotTaken:Invoke() |
| 4084 | FireflyNest.hk_OnSetInfo | Data.FireflyNest:OnSetInfo() |
| 4085 | FactionStorage.hk_OnSetContent | Data.Storage:OnSetContent() |
| 4087 | CraftingStation.hk_OnSetSelected | Data.Operable:OnSetSelected() |
| 4088 | CraftingStation.hk_OnSetQueue | Data.Operable:OnSetQueue() |
| 4089 | CraftingStation.hk_OnSetIsQueueLooping | Data.Operable:OnSetIsQueueLooping() |
| 4090 | CraftingStation.hk_OnSetContent | Data.Operable:OnSetContent() |

- Line 4063 (Picking.hk_OnSetInputMode) is commented out; 4064-4065 exist because switching placement and selection does not fire setInputMode.
- Line 4072 comment: hk_OnSetPickerOptions seems to cause a leak.
- Event fields used by the sections (`_m.onSetSelectedProject`, `_m.onSetPriority`, `_m.onBricktronPopChanged`, ...) are created before line 1700 (not checked).

## 13. BTC mod injections into GameMenu.lua (grep of src/BetterThanCastle, excluding obj/)

| file | how it touches GameMenu.lua | GameMenu section it patches |
|---|---|---|
| Features/BtcCleanFilter.cs (Inject, 129-185) + Patches/CleanFilterPatches.cs:27 | Text injection at load: inserts a DividerHandle block "--BTC cleanup filters" plus btcCleanBtn(...) calls BEFORE `local toCoroutine = function()` (3898). Rewrites the popup chain at 3981 `.AddSection(_m.sh.harvest)` to add `.AddDivider(_m.dh.btcClean)` and `.AddSection(_m.sh.btcClean)` after it. Anchors: "local toCoroutine = function()" and ".AddSection(_m.sh.harvest)". Logs an error if either is missing. | Task popup (popup lyt 3701-3730, coroutine 3965-4003) |
| Features/BtcWormWardBuild.cs PatchGameMenu (72-92) | Inserts a ButtonHandle "Wormhole Ward" right after the anchor `m.AddMenuHandleToggle(h, _m.sh.buildStructuresWards) end)` (2373), before the `------` line that follows (2378). Guarded by `__BtcWormBuildOn()`. Uses `_m.mh.buildStructures` and `_m.sh.buildStructuresWards`. | Build > Structures, wards group (2342-2377) |
| Features/BtcWormWardCraft.cs:97-106 | Calls BtcWormWardBuild.PatchGameMenu when the loaded file ends with LUI/Menus/GameMenu.lua; also sets the global __BtcWormIcon. The same file also patches LUI/Meta/Meta_Recipe.lua and Meta_Operable.lua (not GameMenu). | Build > Structures, wards group (via the call above) |
| Patches/CallToArmsPatches.cs | NO GameMenu.lua text change. It patches the C# method UIGameObserver.lua_CallToArms. Its comment says GameMenu.lua resolves `userCrystal` once at menu load (location not in 1700-4106, not located). | none in range |
| Patches/LuaMenuComponentPatches.cs:34 | Whole-file gate: when BtcAim.Active, ReplaySkip or DevSkipGameMenu is set, the GameMenu.lua load is skipped. | none (whole file) |

Other GameMenu.lua names the BTC code relies on: `_m.dh.btcClean`, `_m.sh.btcClean`, `_m.mh.buildStructures`, `_m.sh.buildStructuresWards`, `_m.sh.harvest`.

## 14. Quirks and risks found while reading (for the C# port)
1. Grid toggle (3431-3442) is commented out in vanilla; do not port unless wanted.
2. Modal (3869-3891) is commented out; the menu_modal slot is unused.
3. spawnUnit OnAction (2819) lacks the IsPaused guard that the other actions have.
4. Two globals are written in Count functions: `currentBricktrons` (2708, 2789). Port as a local.
5. `_m.bh.idleTaskActionsMortuary` (2749) holds the DeadBricktrons button; the name does not match its position.
6. Quarry (1966-1988) has no hasFgLabel; the tunnel and landscape rows do.
7. Mute (3385-3398) and screenshot (3371-3383) are shown in MP and in the world editor (outside the MP guard). Pause and quicksave are not.
8. Right content and right bar reuse LeftContentMenu.lua and LeftBarMenu.lua; a C# port needs one panel class per anchor set, not per file.
9. Lua Data modules (LUI/Data/Data_*.lua) hold most state; not read in this pass.
