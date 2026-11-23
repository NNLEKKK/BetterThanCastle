# GameMenu Lua data modules -> C# equivalents (inventory)

Scope: ref/lua/LUI/Data/Data_Project.lua, Data_Operable.lua (all functions), plus the other Data.* modules
that GameMenu.lua, Menu_TaskList.lua and Menu_Popup.lua call (Recipe, Resource, Storage, FireflyNest, Sound, Article).
Lua side: ref/lua (clean reference). C# side: ref/vanilla (clean since 2026-10-29).
Paths below are relative to ref/vanilla unless marked otherwise.

Legend: "pure Lua" = state or logic that exists only in the Lua module and must be re-implemented in C#
(cache fields, derived counts, Event.New() signals, Meta.* lookups). "inferred" = mapped by name, not verified.

## How the Lua globals reach C#
- Globals Project, CraftingStation, FireflyNest, FactionStorage, Recipe, Resource are set with script.Globals.Set
  (Project.cs:1166, CraftingStation.cs:594, FireflyNest.cs:686, FactionStorage.cs:105, LuaCrafting.cs:398-400).
- Globals UIGame, Picking, BlueprintSelection, fy_<X>Project, fy_Bricktron, Sound, AssetKey: handles of
  LuaStaticDriver; each handle becomes a global (LuaStaticDriver.cs:34). Methods come from [LuaDrivable("Name")]
  attributes on C# members. BlueprintSelection is set directly at BlueprintCompounder.cs:277.
- fy_<X>Project names are LuaDriver handles from scene/prefab data (not in ref). Name-based guesses are marked inferred.
- Lua fy_idleProject.GetAlliedProject() -> IdleProject.GetAlliedProject (IdleProject.cs:22; LuaDrivable at :254).
- Lua callback wrappers use CallbackArguments extensions (ref/vanilla/Brix/Lua/CallbackArgumentExtensions.cs).

## Data.Project (Data_Project.lua)

| Lua call | meaning | C# equivalent |
|---|---|---|
| Exists(project) | non-nil, not released, not being placed | pure Lua + Project.IsBeingPlaced -> Project.cs:1576 lua_IsBeingPlaced |
| TypeExists(type) | Meta.Project lookup | pure Lua (LUI/Meta/Meta_Project.lua) |
| GetAll() | all projects | Project.GetAll -> Project.cs:1190 lua_GetAll (UIGameObserver.projects.all) |
| AddProject / RemoveProject | Lua list edit; not called by menus; uses commented-out ev_onSetAll (would error) | pure Lua, unused |
| GetIdle() | cached idle project | fy_idleProject.GetAlliedProject -> IdleProject.cs:22 |
| IsIdle(project) | cache compare | pure Lua |
| HasSelected() | Exists(GetSelected()) | pure Lua |
| GetSelected() | cached selection | cache filled by Project.GetSelected -> Project.cs:1178 lua_GetSelected (first project with .selection) |
| GetSelectedType() | cached type | pure Lua (cache of selected.type) |
| IsSelected(project) | compare | pure Lua |
| IsSelectedOfType(type) | compare | pure Lua |
| IsSelectedNotOfTypeIdle() | HasSelected and not idle | pure Lua |
| IsSelectedOfTypeIdle() | type == fy_idleProject | pure Lua; handle IdleProject.cs:19 |
| IsSelectedOfTypeBuild() | type == fy_BuildBlocksProject | pure Lua; handle inferred BuildProject.cs:9 |
| IsSelectedOfTypeRepair() | type == fy_RepairProject | pure Lua; inferred RepairProject.cs:14 |
| IsSelectedOfTypeLandscape() | type == fy_LandscapeProject | pure Lua; inferred LandscapeProject.cs:16 |
| IsSelectedOfTypeQuarry() | type == fy_QuarryProject | pure Lua; inferred MineProject.cs:17 |
| IsSelectedOfTypeTunnel() | type == fy_TunnelProject | pure Lua; TunnelProject.cs:28 |
| IsSelectedOfTypeHarvest() | type == fy_HarvestProject | pure Lua; HarvestProject.cs:22 |
| IsSelectedOfTypeBuildOrRepair() | Build or Repair | pure Lua |
| OnSetSelected() | selection = Project.GetSelected() or idle; fires ev_onSetSelected | hook Project.hk_OnProjectSelected (Project.cs:1114) -> GameMenu.lua:4058 |
| OnResetSelected() | clear cache | pure Lua |
| _SetSelection(p, flag) | select or deselect | select: Project.Select -> Project.cs:1529 lua_Select -> UIGameSelector.Select(GameObject) (UIGameSelector.cs:406). deselect: Project.Deselect -> Project.cs:1537 lua_Deselect -> UIGameActor.SelectIdleProject (UIGameActor.cs:54) |
| SetSelection(p, flag) | guard nil/released, then _SetSelection | pure Lua guard. WARNING: lua_Select reads args.AsGameObject (CallbackArgumentExtensions.cs:24-26 needs a LuaDriver) but Project is a MultiBehavior (MultiBehavior.cs:12), not a LuaDriver. Likely error when select=true (GameMenu uses SetSelection 3x). Verify in game. |
| ToggleSelection(p) | SetSelection(p, not IsSelected) | pure Lua |
| SelectCrew(p, fixed, free) | select the project crew | Project.SelectWorkers -> Project.cs:1477 lua_SelectWorkers -> UIGameActor.SelectIdleProject (:54) + UIGameActor.SelectProjectMember(p, fixed, free, false) (UIGameActor.cs:60) |
| GetName(type) | Meta.Project[type].Name() | pure Lua (Meta_Project.lua) |
| GetIcon(type) | Meta.Project[type].Icon() | pure Lua (Meta) |
| GetHarvestIcon(project) | icon chosen by nature flags | project:GetNatureTypeFlag -> HarvestProject.cs:25 (Lua wrapper lua_GetNatureTypeFlag :150); icons IconKeys._UI_Harvest* (C# IconKeys, file not located) |
| GetHarvestIconColor(icon) | White or CastleYellow | pure Lua |
| GetIconColor(type) | Meta.Project[type].IconColor() | pure Lua (Meta) |
| IsBlueprintManagementReady(p) | storage / bp management ready | Project.IsBlueprintManagementReady -> LuaProjectStorage.cs:135 lua_IsBlueprintManagementReady -> IsStorageManagementReady (LuaProjectStorage.cs:118) |
| GetSelectedGoalCount() | goal count of selection | Project.GetGoalCount -> Project.cs:1452 lua_GetGoalCount -> project.GoalCounts() (Project.cs:413, virtual) |
| ShowSelectedStatusAction(a) | commit button shown | Project.ShowStatusAction -> Project.cs:1311 lua_ShowStatusAction -> ShowOk / ShowApply / ShowCancel / ShowDelete (Project.cs:1371, 1377, ...) |
| HasSelectedStatusAction(a) | commit button active | Project.HasStatusAction -> Project.cs:1330 lua_HasStatusAction -> HasOk / HasApply / ... |
| SetSelectedStatusAction(a) | press commit button | Project.SetStatusAction -> Project.cs:1349 lua_SetStatusAction -> SetOk / SetApply / ... |
| ev_onSetSelectedStatus / OnSetSelectedStatus() | status event. BUG: uses undefined srcGetSelected (Data_Project.lua:331) | pure Lua; C# source Project.hk_OnSetStatus (Project.cs:1134) |
| ev_onSetHeldBlueprint / OnSetHeldBlueprint() | cache of held blueprint + event | pure Lua; reads Picking.GetHeldBlueprint; no ConnectWhen for it in GameMenu.lua |
| GetHeldBlueprint() | cache | Picking.GetHeldBlueprint -> Picking.cs:705 |
| GetPriority(p) | priority bias enum | Project.GetPriority -> Project.cs:1620 lua_GetPriority -> project.GetPriority() (LuaEnumProxy of ProjectPriority.Bias) |
| AddSelectionToCrew(p) | selected labor -> project | Project.AssignSelection -> Project.cs:1508 -> UIGameActor.AssignSelectionToProject (UIGameActor.cs:42) |
| RemoveSelectionFromCrew(p) | selected labor -> idle | Project.UnassignSelection -> Project.cs:1516: IdleProject -> UIGameActor.MakeSelectionFreeAgents (UIGameActor.cs:30); else AssignSelectionToProject(IdleProject.GetAlliedProject()) |
| GetFightersInfo() | cache of fighters table | Project.GetFightersInfo -> Project.cs:1203 lua_GetFightersInfo (table keyed by Occupation.Job, fields count and selectedCount; source UIGameObserver.bricktrons CheckCreation / CheckSelection) |
| OnSetFightersInfo() | refresh cache | pure Lua over Project.GetFightersInfo; no ConnectWhen (called from TaskList) |
| GetIdlersInfo() / OnSetIdlersInfo() | cache | Project.GetIdlersInfo -> Project.cs:1242 lua_GetIdlersInfo (all / busy / selected lists) |
| GetBuildersInfo(p) / OnSetBuildersInfo(p) | per-project cache | Project.GetBuildersInfo -> Project.cs:1271 lua_GetBuildersInfo |
| OnSetBuildersInfoMany(ps) | loop of OnSetBuildersInfo | pure Lua |
| OnResetBuildersInfo(p) | clear one entry | pure Lua |
| GetFightersCount() | cache.count | pure Lua |
| GetFightersCountAtJob(job) | cache[job].count | pure Lua |
| GetUnselectedFightersCountAtJob(job) | count minus selectedCount | pure Lua |
| GetSelectedFightersCountAtJob(job) | cache[job].selectedCount | pure Lua |
| GetAllIdlersCount() / GetBusyIdlersCount() / GetSelectedIdlersCount() | length of all / busy / selected | pure Lua |
| GetAllBuildersCount(p) / GetSelectedBuildersCount(p) | length of all / selected | pure Lua |
| idleId=1, operableId=2, knightId=111, archerId=112 | fake project ids used by TaskList | pure Lua constants |
| ev_onSetSelected, ev_onSetAll, ev_onSetSelectedStatus, ev_onSetHeldBlueprint | Event.New() signals | pure Lua; C# = plain event or GameSignal |

## Data.Operable (Data_Operable.lua)

| Lua call | meaning | C# equivalent |
|---|---|---|
| HasSelected() | cache not nil | pure Lua |
| GetSelected() | cache | filled from CraftingStation.GetSelected |
| GetSelectedType() | cache of selected.type | pure Lua |
| DeselectAll() | deselect station | CraftingStation.lua_DeselectAll (CraftingStation.cs:694) |
| OnSetSelected() / ev_onSetSelected | cache station, then OnSetIsQueueLooping, OnSetQueue, OnSetContent | CraftingStation.lua_GetSelected (CraftingStation.cs:683, uses CraftingStation.SelectedStation()); hook CraftingStation.hk_OnSetSelected (table :583, field :662, LuaHook) -> GameMenu.lua:4087 |
| HasRecipe(type, recipe) | recipe present in Meta.Operable[type].Recipes | pure Lua (Meta) |
| GetRecipeAtSlotIndex(type, i) | Meta recipe list entry | pure Lua. Defined twice (Operable.lua:63 and :102); the second wins; the first would index nil _getRecipeSlotIndex |
| HasRecipeAtSlotIndex(type, i) | entry exists | pure Lua |
| GetHighestRecipeSlotCount() | max #Recipes over Meta.Operable | pure Lua |
| GetTooltipPath(type) | {"Operable", tooltipName} | pure Lua (Meta) |
| GetName(type) / GetIcon(type) | Meta.Operable[type].Name() / Icon() | pure Lua (Meta) |
| HasInputResource(type, res) / GetInputResourceCount(type, res) | sum over recipes of input count | pure Lua over Data.Recipe:GetInputResourceCount (see below) |
| HasOutputResource / GetOutputResourceCount | same, output side | pure Lua over Data.Recipe |
| _GetResourceCount(type, res, addIn, addOut) | helper | pure Lua |
| GetContentResourceCount(op, res) | _getContent[res] or 0 | pure Lua over cache from CraftingStation.lua_GetContent (CraftingStation.cs:783) |
| HasContentResource(op, res) | count not 0 | pure Lua |
| OnSetContent() / ev_onSetContent | cache CraftingStation.GetContent() | CraftingStation.lua_GetContent (CraftingStation.cs:783); hook CraftingStation.hk_OnSetContent (table :585, field :668, LuaHook) -> GameMenu.lua:4090 |
| HasOutputContent(type) | any output resource in content | pure Lua. BUG: passes undefined `operable` (Operable.lua:242) |
| GetActiveRecipe() | queue[1] or Recipe.None | pure Lua (Recipe enum = LuaCrafting.cs:399) |
| HasActiveRecipe() | active is not None | pure Lua |
| GetQueue() / HasQueue() | queue cache | CraftingStation.lua_GetQueue (CraftingStation.cs:701) |
| OnSetQueue() / ev_onSetQueue | refresh queue cache | hook CraftingStation.hk_OnSetQueue (table :590, field :674) -> GameMenu.lua:4088 |
| AddToQueue(recipe) | append recipe | CraftingStation.lua_AddToQueue (CraftingStation.cs:717) -> CallCmdcraftingQueueRecipe(station.gameObject, id, 1) (:728), a UNet command |
| RemoveFromQueue(index) | index minus 1 (0-based) | CraftingStation.lua_RemoveFromQueue (CraftingStation.cs:770) |
| ClearQueue() | clear | CraftingStation.lua_ClearQueue (CraftingStation.cs:734) |
| GetIsQueueLooping() | cache | CraftingStation.lua_GetIsQueueLooping (CraftingStation.cs:746) |
| SetIsQueueLooping(flag) | set loop | CraftingStation.lua_SetIsQueueLooping (CraftingStation.cs:757) |
| OnSetIsQueueLooping() / ev_onSetIsQueueLooping | refresh loop cache | hook CraftingStation.hk_OnSetIsQueueLooping (table :593, field lua_OnSetLoop :680) -> GameMenu.lua:4089 |
| GetRecipeAtQueueIndex(i) / HasRecipeAtQueueIndex(i) | queue[i] | pure Lua |
| GetQueueSize() | constant 8 | pure Lua. Mod note: craft.max (BtcCraftQueue) changes real capacity; this constant does not follow it |

## Other Data.* modules called from the menus

| Lua call | meaning | C# equivalent |
|---|---|---|
| Data.Recipe:_GetInfo(recipe) | Crafting.GetRecipeInfo plus resource lists | Crafting.GetRecipeInfo -> LuaCrafting.cs:397 (callback), delegate at :427 (GetRecipeInfo), ToRecipeInfo |
| Data.Recipe:GetInputResourceCount / GetOutputResourceCount / GetInputResources / GetOutputResources / *SlotCount / *AtSlotIndex / GetResourceInfos | derived from _GetInfo.input and .output | pure Lua over recipe info |
| Data.Recipe:GetName / GetIcon / GetTooltipPath | Meta.Recipe lookups | pure Lua (Meta) |
| Data.Resource:GetName / GetIcon / GetStockpiledIcon / GetTooltipPath | Meta.Resource lookups | pure Lua (Meta) |
| Data.FireflyNest:OnSetInfo() | FireflyNest.GetInfo() then refresh all xp, unit and progress caches | FireflyNest.lua_GetInfo (FireflyNest.cs:646); hook FireflyNest.hk_OnSetInfo (table :683, LuaHookCollapsed) -> GameMenu.lua:4084 |
| Data.FireflyNest:GetCurrentXp / GetTotalXp / GetSpawnableUnitCount / GetAutoRespawnFireflyCount | cached fields | pure Lua (filled from lua_GetInfo) |
| Data.FireflyNest:GetProgress / GetProgressText | currentXp / totalXp | pure Lua |
| Data.FireflyNest:SpawnUnit() | spawn a builder | FireflyNest.lua_SpawnUnit (FireflyNest.cs:662) -> AssignJobRequest(Occupation.Type.Builder, null) |
| Data.Storage:OnSetContent() / GetResourceCount / HasResourceCount | FactionStorage content cache | FactionStorage.lua_GetContent (FactionStorage.cs:119); hook FactionStorage.hk_OnSetContent (FactionStorage.cs:103, LuaHookCollapsed) -> GameMenu.lua:4085 |
| Data.Sound:Play(name, onlyIfReady) | play UI sound | Sound.Play -> SoundEngine.lua_Play (SoundEngine.cs:679-681, [LuaDrivable("Play")]) |
| Data.Article:Init / GetFunc / ToMenu / Tooltip | help and tooltip article loader | pure Lua (LUI/Article files); only signatures checked, not read in full |

## Hooks GameMenu.lua connects (ConnectWhen, GameMenu.lua:4056-4090)

| Lua hook | C# declaring member (file:line) | signal type |
|---|---|---|
| Project.hk_OnSetStatus | Project.lua_OnSetCommitStatus (Project.cs:1134, field :1832) | LuaHook<Project> |
| Project.hk_OnProjectSelected | Project.lua_Selected (Project.cs:1114, field :1820) | LuaHook<Project> |
| Project.hk_OnProjectDeleted | Project.ProjectDeleted (Project.cs:1113) | LuaHook<Project> |
| Project.hk_OnSetCrewInfoPriority | Project.lua_OnSetCrewInfoPriority (Project.cs:1122) | LuaHook<Project> |
| Project.hk_OnSetCrewInfo | Project.lua_OnSetCrewInfo (Project.cs:1121) | LuaHookCollapsed<Project> |
| Picking.hk_OnSetPicker | Picking.cs:955-956 (PickingObserver.lua_OnSetUserActivePicker) | LuaHook<Picker, Picker> |
| Picking.hk_OnSetPickerOptions | Picking.cs:951-952 | LuaHook |
| Picking.hk_BPSelectionChanged | Picking.cs:963-964 | LuaHook |
| Picking.hk_OnSetInputMode | Picking.cs:959 (commented out at GameMenu.lua:4063) | LuaHook<InputMode> |
| BlueprintSelection.hk_OnDoneInstantiating | BlueprintCompounder.cs:535-536 | LuaHook |
| BlueprintSelection.hk_CompoundSelectionChanged | BlueprintCompounder.cs:539-540 | LuaHook |
| BlueprintSelection.hk_OnCompoundDeleted | BlueprintCompounder.cs:543-544 | LuaHook |
| fy_TunnelProject.hk_OnSetTunnelType | TunnelProject.cs:350 (TunnelVolume.ExpansionTunnelTypeChanged) | LuaHook |
| fy_TunnelProject.hk_ResourceTypeKeptChanged | TunnelProject.cs:354 | LuaHook |
| fy_QuarryProject.hk_ResourceTypeKeptChanged | MineProject.cs:139 (handle name inferred) | LuaHook |
| UIGame.hk_OnSetShowHelpAtGameStart | UIGameObserver.cs:1675-1676 | LuaHook |
| UIGame.hk_ShowHelp | UIGameObserver.cs:1679-1680 (lua_ShowHelp) | LuaHook |
| UIGame.hk_OnSetAllowDebugMode | UIGameObserver.cs:1687-1688 | LuaHook |
| UIGame.hk_OnUpdateMapThumbnail | UIGameObserver.cs:1759 | LuaHook (type not re-checked) |
| UIGame.hk_OnUpdateBlueprintThumbnail | UIGameObserver.cs:1763 | LuaHook (type not re-checked) |
| UIGame.hk_OnBlueprintScreenshotTaken | UIGameObserver.cs:1767 (field set at :1454) | LuaHook |
| FireflyNest.hk_OnSetInfo | FireflyNest.cs:683 | LuaHookCollapsed |
| FactionStorage.hk_OnSetContent | FactionStorage.cs:103 | LuaHookCollapsed |
| CraftingStation.hk_OnSetSelected | CraftingStation.cs:583 (field :662) | LuaHook |
| CraftingStation.hk_OnSetQueue | CraftingStation.cs:590 (field :674) | LuaHook |
| CraftingStation.hk_OnSetIsQueueLooping | CraftingStation.cs:593 (field :680, lua_OnSetLoop) | LuaHook |
| CraftingStation.hk_OnSetContent | CraftingStation.cs:585 (field :668) | LuaHook |

Note: UIGame.hk_OnSetInputMode (UIGameObserver.cs:1683; LuaHook<InputMode> bound to InputModeController.InputModeChanged at :1434) exists, but GameMenu.lua uses the Picking hooks instead.

## Input modes (GameMenu.lua:31-320) -> C#

The Lua Activate/Deactivate functions are wiring only: they register and unregister key actions and set the key binding category.
The real mode is C# state (Picking flags, held blueprint, InputModeController).

| Lua function (GameMenu.lua) | Lua calls | C# equivalent |
|---|---|---|
| ActivateInputModeProjects (:31) | UIGame.RegisterAction x27, fy_Bricktron.ToggleHotkeys(true), UIGame.SetKeyBindingCategoryTo(General,false) and (Projects,true) | UIGame.RegisterAction -> UIGameObserver.lua_RegisterAction (UIGameObserver.cs:842) -> KeyBindingsUtility.RegisterAction; SetKeyBindingCategoryTo (:865) -> LuaMenuUtility.SetKeyBindingsTo; ToggleHotkeys -> CombatHotkeys.cs:75-78 (fy_Bricktron handle, inferred from LuaLibs.cs:396-401) |
| DeactivateInputModeProjects (:63) | UIGame.UnregisterAction x22, ToggleHotkeys(false), category flip | UIGameObserver.lua_UnregisterAction (:856) -> KeyBindingsUtility.UnregisterAction; same category calls |
| ActivateInputModeTasks (:96) / Deactivate (:116) | RegisterAction for project_Create*Task (12) and command_* | same RegisterAction / Unregister path; _m.bh.* handlers are pure Lua |
| ActivateInputModeBricks (:137) / Deactivate (~:153) | RegisterAction for brick keys; each calls Picking.SetHeldBlueprint(AssetKey.New("Blueprints", name)) | Picking.SetHeldBlueprint (Picking.cs:713, LuaDrivable); AssetKey.New -> LuaAssetKey.cs:13 (global AssetKey) |
| ActivateInputModeStructures (:232) / Deactivate (~:250) | RegisterAction for CallToArms, BlocksMenu, CreateProject, priority | same RegisterAction path; no category change |
| ActivateInputModeBlueprints (:296) / Deactivate (~:320) | BlueprintSelection.Duplicate / Delete / Expand / Cancel; category Blueprints | BlueprintCompounder.cs:289 Duplicate, :305 Delete, :313 Expand, :321 Cancel (global at :277) |
| _m.OnSetInputMode (:328) | blueprint management open or close from _IsBlueprintSelection() | _IsBlueprintSelection (GameMenu.lua:472) -> Picking.IsSelectingBlueprints (Picking.cs:810) |
| _ToggleBlueprintSelection (:473) | toggle | Picking.SetIsSelectingBlueprints (Picking.cs:722) |
| IsHeldBlueprintOfMetaType (:480) | held blueprint kind test | Data.Project:GetHeldBlueprint -> Picking.GetHeldBlueprint (Picking.cs:705) + pure Lua Meta test |
| OnDeselectBuildProject (~:410) | deactivate Blueprints, Bricks, Structures; activate Projects | pure Lua wiring |
| InputMode enum | none, command, blueprint, planning, worldEditor, storage, pause, menu | InputMode.cs:7; GetInputMode / SetInputMode via UIGameObserver.cs:95-108 -> InputModeController.GetCurrentMode / SetCurrentMode; signal InputModeController.cs:985 |

## Lua-only state to re-implement in C#
- All ev_* Event.New() signals and the _get* / OnSet* cache fields in Data_Project, Data_Operable, Data_FireflyNest, Data_Storage.
- Derived counts (fighter, idler and builder counts; progress text and fraction; queue size constant 8; harvest icon choice).
- Meta.Project / Meta.Operable / Meta.Recipe / Meta.Resource tables (names, icons, colours, recipe lists). Data, not C# objects.
- Recipe info list building in Data.Recipe:__GetInfo (appends resources into info.input / info.output).
- Input-mode wiring (Activate / Deactivate functions) and the _m.mh / _m.bh handler tables.

## Lua bugs found while reading (do not copy)
- Data_Project.lua:331 OnSetSelectedStatus passes undefined srcGetSelected.
- Data_Project.lua:121 IsSelectedNotOfTypeIdle calls Data.Project:IsSelectedOfTypeIdle through the global (works).
- Data_Project.lua:30 stray "endProject.GetAll()" inside a comment block (harmless).
- Data_Operable.lua:63-68 GetRecipeAtSlotIndex uses nil table _getRecipeSlotIndex; shadowed by line 102 (dead code).
- Data_Operable.lua:242 HasOutputContent passes undefined `operable`.
- GameMenu.lua is not valid UTF-8 (Windows-1252 bytes); edit it only with byte-safe tools.

## Open questions
- Project.Select with a Project argument vs AsGameObject / LuaDriver (see SetSelection row): confirm in game or in a lab.
- fy_* handle to C# class names are inferred for BuildBlocks, Repair, Landscape, Quarry, Harvest, Tunnel, Bricktron: check the scene or prefab LuaDriver handles if an exact mapping is needed.
