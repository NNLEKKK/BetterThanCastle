# GameMenu inventory A (LUI2 framework map + GameMenu.lua 1-1700)

Scope: read-only lookup for the C# rebuild of ref/lua/LUI/Menus/GameMenu.lua.
Sources: ref/lua/LUI2.lua, MenuHandle.lua, MenuGroup.lua, LUI/Menus/GameMenu.lua, LUI/Menus/Game/LeftBarMenu.lua,
LUI/Panels/ButtonPanel.lua, ref/vanilla/Brix/UI/Builder/{Lui/LuiBuilder.cs, Menu/LuaMenuUtility.cs, Menu/BaseMenu.cs,
Widget/LuaWidget.cs, Panel/*.cs}, ref/vanilla/Brix/External/Factories/Buttons.cs.
Caveats: (a) ref/vanilla is flagged DIRTY in the project rules; the C# mapping was read from it without checking the clean DLL.
(b) GameMenu.lua and LUI/Menus/Game/*.lua are cp1252, not UTF-8: read or grep only.
Line numbers refer to GameMenu.lua unless a file is named.

---------------------------------------------------------------------------
## 1. LUI2 framework map

### 1.1 Lua globals to C#
| Lua global | defined | C# object |
|---|---|---|
| Menu | LUI2.lua:17 `Menu = LUI2.Builder.Menu` | LuiBuilder.MenuLuiBuilder (LuiBuilder.cs:489) |
| LayoutPanels | LUI2.lua:18 | table in LuaMenuUtility.cs:85-95 (Vertical, Horizontal, Grid, Scrollable, SquareButton, Slidables.Top/Bottom/Left/Right) |
| Panels | LUI2.lua:19 | table LuaMenuUtility.cs:110-138 (Label, Image, RawImage, Slider, Dropdown, InputField, Compass, Media, Minimap, Buttons.Standard) |
| Widgets | LUI2.lua:20 | table LuaMenuUtility.cs:147-178 (LayoutGroup, LayoutElement, Label, Button, Image, Slider, Dropdown, InputField, ...) |
| Handles | LUI2.lua:21 | table LuaMenuUtility.cs:185-276 (Visible, AnchorMin, OnAction, Count, Highlight, Font, ...) |
| Enums, Colors, Funcs | LUI2.lua:26-48, 80-90, 120 | LuaMenuUtility lua_enums / lua_colors / lua_loc (LuaMenuUtility.cs:74-76) |
| Event, Group, MenuGroup, MenuHandle | LUI2.lua:172-176 (dofile) | pure Lua: ref/lua/Event.lua, Group.lua, MenuGroup.lua, MenuHandle.lua |
| LiveValue, Trigger | LUI2.lua:370-371 | pure Lua |
| ButtonHandle, SectionHandle, DividerHandle | GameMenu.lua:530-532 `= MenuHandle` | aliases of one Lua class |
| MyPanels.Button, ModalButtonBg, ModalButtonFg | LUI2.lua:55-74 (loadfile of LUI/Panels/ButtonPanel.lua, Panel_ModalButtonBg.lua, Panel_ModalButtonFg.lua) | Lua panels, not C# |

GameMenu.lua never calls Panels.*, LayoutPanels.* or Widgets.* in lines 1-1700. It creates MenuHandle data tables,
MenuGroups and one Menu (L23). Widgets are built later by the sidebar Lua helpers (1.5).

### 1.2 Lua constructor to C# class to Factory.AssetKey (LuiBuilder.GetNewPanel, LuiBuilder.cs:21-64)
| Lua call | C# class (New(null,null) / LuaNew) | AssetKey (field default) | variants / notes |
|---|---|---|---|
| LayoutPanels.Vertical:New(n) | VerticalLayoutPanel | Layouts.Vertical (VerticalLayoutPanel.cs:92) | VerticalRoot, VerticalLibrary (L48-58) |
| LayoutPanels.Horizontal:New(n) | HorizontalLayoutPanel | Layouts.Horizontal (HorizontalLayoutPanel.cs:85) | HorizontalRoot (L46); 110 uses in menus |
| LayoutPanels.Grid:New(n) | GridLayoutPanel | Layouts.Grid (GridLayoutPanel.cs:68) | |
| LayoutPanels.Scrollable:New(n) | LuaScrollableLayoutPanel .As(Default) | Layouts.LuaScrollable (cs:75); Right variant Layouts.LuaScrollableRight | plain ScrollableLayoutPanel (Layouts.Scrollable) is not in the Lua switch |
| LayoutPanels.SquareButton:New(n) | SquareButtonPanel | Buttons.Square (SquareButtonPanel.cs:88) | |
| LayoutPanels.Slidables.Top/Bottom/Left/Right | SlidableLayoutPanel .As(Variant) | Layouts.SlidableTop/Bottom/Left/Right (SlidableLayoutPanel.cs:47-56) | |
| Panels.Buttons.Standard:New(n) | StandardButtonPanel | Buttons.Standard (StandardButtonPanel.cs:71; Buttons.cs:78) | WidgetId: LayoutElement, Button, FrameAnimator; Variant Default |
| Panels.Label:New(n) | LabelPanel | Texts.Default (LabelPanel.cs:67) | |
| Panels.Image:New(n) | ImagePanel | Images.Default (ImagePanel.cs:67) | 105 uses in menus |
| Panels.Media:New(n) | MediaPanel | Images.Default (MediaPanel.cs:67) | |
| Panels.RawImage:New(n) | LuaTexturePanel .As(Default) | Images.LuaTexture (cs:67) | |
| Panels.Compass:New(n) | LuaCompassPanel .As(Default) | Images.LuaCompass (cs:64) | |
| Panels.Minimap:New(n) | MinimapPanel .As(Default) | Images.MiniMap (cs:67) | |
| Panels.Slider:New(n) | SliderPanel | Sliders.Short (SliderPanel.cs:71) | SliderInputPanel uses Sliders.StandardWithInput; not in Lua switch |
| Panels.Dropdown:New(n) | LuaDropdownPanel .As(Default) | Buttons.LuaDropdown (LuaDropdownPanel.cs:61; field default Buttons.Dropdown) | |
| Panels.InputField:New(n) | LuaInputFieldPanel .As(Default) | InputFields.LuaShort (LuaInputFieldPanel.cs:75) | |
| any other name | - | - | LuiBuilder.cs:63-64 throws NotImplementedException |

Not reachable from Lua Panels.* (no case in GetNewPanel, not in the LuaMenuUtility Panels table):
HeaderPanel (Panels.CollapsableHeader), SidebarTitlePanel (Panels.SidebarTitle), ProgressbarPanel (Panels.Progressbar),
ModalPanel (Panels.Modal), LobbyPanel, PlayerEntryPanel, ScrollableLayoutPanel, StandardLayoutPanel (Layouts.Standard).
Lua equivalents live in LUI/Panels/*.lua: HeaderPanel.lua, ProgressBarPanel.lua, SpacerPanel.lua, DividerPanel.lua,
Panel_ModalButtonBg.lua, Panel_ModalButtonFg.lua. Spacer and divider have no C# panel at all.

### 1.3 Setters and handles (how Lua property calls reach C# widgets)
- Lua panels build setters from a config table: ButtonPanel.lua:186-198 `AddSetters` makes `_p[k] = function(value, cache)`
  for every key of `_cfg` (for example `.Visible()`, `.Image_Color()`, `.Button_OnAction()`). These are Lua only: they store a
  closure in `_userCfg`, call UpdateCfg and return the panel for chaining.
- Widget properties reach C# via `:SetWidgetHandle(Widgets.<W>, Handles.<H>, closure, cache)` (ButtonPanel.lua:221-432).
  `Widgets.<W>` is only a tag; the component is chosen by the C# panel.
- C# path: LuiBuilder.Apply.Widget.SetHandle (LuiBuilder.cs:1557) -> LuaWidget.Lua_SetHandle (LuaWidget.cs:50). The name is
  looked up in LuaWidget.HandleNameTypeMap (LuaWidget.cs:20-46), built from every IWidgetHandle type: the type name minus
  a leading "I" and the text "Get". So Handles.OnAction = IGetOnAction, Handles.Visible = IGetVisible, Handles.Highlight = IGetHighlight.
  The closure is called by the getter wrappers set in LuaWidget.Lua_Init (LuaWidget.cs:267-300).
- Handle names (LuaMenuUtility.cs:185-276): layout (MinWidth, MinHeight, PreferredWidth/Height, FlexibleWidth/Height,
  AnchorMin/Max, OffsetMin/Max, AnchoredWidth/Height, AnchoredPosition, Pivot, IgnoreLayout, Padding, Spacing, Alignment);
  Image (Enabled, Sprite, Color, Raycasts, PreserveAspect); Label (Text, Font, FontSize, Alignment, LineSpacing);
  Button (OnAction, OnDoubleAction, OnAltAction, OnPointerEnter/Exit/Up/Down, CanHover, CanPress, Discreet, Interactable);
  Count, IconAsCount, Highlight, Visible, Enabled, Fill, FillMethod, Options/Selection, TextValue/SetTextValue,
  OnShow/OnHide/OnEndShowing/OnEndHiding, OnCtrlAction, OnShiftAction, HideFirstFrame.
- Lua chaining used by panel code: Panels.X:New(name).SetWidgetHandle(...).AddPanel(child) (LuiBuilder.Apply.Panel.AddPanel).

### 1.4 MenuHandle, MenuGroup, Menu (menu plumbing)
| Concept | Lua definition | C# counterpart | notes |
|---|---|---|---|
| Menu:New() | GameMenu.lua:23; LuiBuilder.cs:382 NewLui, DoNewLui L497-499 | LuiBuilder.MenuLui (cs:675) -> BaseMenu (Menu/BaseMenu.cs:20, abstract) | Lua method names Open/Close/Reopen/Refresh/Load/Reload* are strings in LuiBuilder.cs:113-155 |
| menu from file | DoNewLui: LuaMenu.ReadMenuTable(script, path) | LuaMenu | MenuHandle:Load does dofile(self.path) |
| MenuHandle | MenuHandle.lua:1-134 | none (pure Lua) | fields path, menu, onLoad, onBeforeOpen, onOpen, onBeforeClose, onClose, instantiationFlag (1 OnLoad, 2 OnOpen) |
| SetPath(p) | MenuHandle.lua | none | sidebars: SidebarFloatingMenu.lua (most), SidebarScoreMenu, Menu_Help, TestDebugSidebar, TestMovieSidebar, CreativeSidebar, SidebarHomeMenu |
| Load() | MenuHandle.lua: dofile, SetFlag(OpenOnLoad) or ClearFlag, onLoad:Invoke(menu) | BaseMenu.BuilderFlag LoadOnInit=1, LoadOnOpen=2, ReloadOnOpen=4 (BaseMenu.cs:1678) | the name OpenOnLoad is not in that enum; see Apply.Menu.SetFlag (LuiBuilder.cs:1426) |
| MenuGroup | MenuGroup.lua:1-183; GameMenu L538-556 | none (pure Lua) | SetExclusive(true) (L539, L544); onAnyOpen Event (MenuGroup.lua:30, 66, 85); AddChild(h) |
| Open/Close/Toggle on handle | MenuHandle.lua delegates to parent group | BaseMenu.Open() cs:428, Close() cs:510, Refresh() cs:655, Load() cs:254, Reload() cs:224 | |
| ConnectWhen(hook, fn, MenuState.X) | GameMenu.lua:4056-4082 (outside 1-1700) | LuiBuilder connections; BaseMenu.MenuState: Loaded, Opened (BaseMenu.cs:1437) | fn runs while the menu is in that state |
| Event.New / Invoke / AddListener | Event.lua (306 L) | none | ev_refresh, ev_onLoad, onOpen, onClose, onRefreshHighlight are Events |
| Hooks.Invoke(UIGame.hk_*) | GameMenu.lua:386, 389 | game Hooks | |

Handle tables in GameMenu.lua (L523-528): `_m.mg` MenuGroups; `_m.mh` action handles (buttons and toggles, used by
hotkeys and other handles); `_m.sh` section handles (header with ev_onLoad; children attach on that event);
`_m.bh` button handles for hotkeys and the queue (mostly past L1700); `_m.dh` dividers. All are plain Lua tables.

### 1.5 Sidebar helpers that turn a MenuHandle into a panel (Lua)
| helper | where | what it does |
|---|---|---|
| AddMenuHandleToggle(h, sh) | LeftBarMenu.lua:67-135; same name in SidebarFloatingMenu.lua:78, TopBarMenu.lua:49, SidebarHomeMenu.lua:61, SidebarTaskMenu.lua:79, Menu_Popup.lua:323, Menu_TaskList.lua:839, Menu_Tooltip.lua:297 | defaults (Visible true, OnAction = Toggle, IconColor/LabelColor CastleYellow, BgColor* from _bgColor*); builds ButtonPanel.lua with `.Visible(||h.Visible())`, `.FrontImage_Sprite(||h.Icon())`, `.FrontImage_Color(||h.IconColor())`, `.HoverLabel_Text(||h.Label())`, `.HoverLabel_Color(||h.LabelColor())`, `.Button_OnAction(||h.OnAction())`, `.HoverHotkeyLabel_Enabled(||h.hasHotkey)`, `.HoverHotkeyLabel_Text(||h.HotkeyLabel_Text())`, `.Image_Color(||h.BgColor())`; h.ev_refresh -> p.RefreshAll(); AddPanel |
| AddMenuHandleSpacer(h, sh) | LeftBarMenu.lua:212; SidebarFloatingMenu.lua:366; TopBarMenu.lua:89 | spacer row |
| AddAlertHandle(h, sh) | SidebarFloatingMenu.lua:286 | alert row (Count text + icon) |
| AddDivider(h, sh) | Menu_Popup.lua:542; Menu_Tooltip.lua:509 | DividerPanel.lua |
| AddSection(sh) | SidebarFloatingMenu.lua:463; SidebarTaskMenu.lua:138; Menu_Popup.lua:578; Menu_Tooltip.lua:546 | section header; fires sHandle.ev_onLoad:Invoke(menu, sHandle) (Menu_Popup.lua:608, Menu_Tooltip.lua:577) so children call AddMenuHandleToggle |
| AddDisplayHandle | LeftBarMenu.lua:137 | display-only row |
| AddCompoundHandleToggle, AddMenuHandleInputField | SidebarFloatingMenu.lua:176, 413 | not used in 1-1700 |
| AddMenuHandleSocket | LeftContentMenu.lua:24 | host container |
| SetMenuHandle / RegisterButton / ResetButtons | SidebarFloatingMenu.lua:51, 64, 68 | GameMenu onOpen calls h.menu.ResetButtons() |

Which sidebar file hosts sh.build, sh.landscape, sh.quarry, sh.tunnel, sh.harvest and sh.newRepairTask was not traced;
their ev_onLoad is fired by that host AddSection.

### 1.6 Sizes, anchors, padding, colours
- ButtonPanel.lua defaults: Width 48, Height 48 (L60-61); FlexibleWidth/Height -1 (L62-63); back image 32x32 (L70-71);
  front image 32 (L99); hover image 32x32 (L81-82); hover label font 11 (L90); hotkey label font 10 (L95), UpperRight (L96).
  Root = Horizontal layout; hover layout Padding Vector4(0,4,4,0) (L270); anchors 0..1, offsets zero (L266-269).
  Layout element MinWidth/PreferredWidth = Width, MinHeight/PreferredHeight = Height (L222-225).
- MenuHandle sizing: IconSize 32 or 64 (e.g. L978, L1171, L1267); spacer `Height = 1` (L753) or `FlexibleHeight = 1` with `Height = -1` (L768-769);
  divider `OffsetMax = Vector2(-8,-8)` (L1081); task limit LabelFontSize 12 (L787), hotkey label font 12 (L792).
- Colours (LUI2.lua:80-114): CastleBlue (0,160/255,1), CastleRed (1,0,100/255), CastleYellow (LUI2.Colors), Gray, DarkGray, White,
  _bgColorNone (0,0,0,0), _bgColorDefault (0.12,0.12,0.12,0.85), _bgColorDark, _bgColorSelected (0.10,0.10,0.10,1),
  _bgColorHover, _fgColorHover (1/3,1/3,1/3,0.5), _colorOrange (1,0.5,0,1).
- Anchors: Vector2(0.5,0.5) for centred images (ButtonPanel.lua:245-247); Vector2(0,0)..(1,1) for overlays.

---------------------------------------------------------------------------
## 2. GameMenu.lua lines 1-1700, section by section

Legend: "bind" = UIGame.RegisterAction(name, closure). Activate and Deactivate pairs are listed once.
Icon keys are IconKeys.X:Get64(). Loc keys are GetLocalized("##...") (LUI2.lua Funcs). "paused" = guard `not UIGame.IsPaused()`.

### 2.1 Header, require, menu object (L1-27)
| L | content |
|---|---|
| 14 | require("LUI2.lua") |
| 15 | sandbox only: require("LTD.lua") when UIGame.GetModeName() == "##mode_sandbox" |
| 16 | BlueprintSelection.InitializeCompounds() |
| 23 | local _m = Menu:New() (the whole HUD bar is one Menu) |

### 2.2 Input mode PROJECTS (L30-93), Activate and Deactivate pair
Activate registers (L34-56); Deactivate unregisters the same names (L66-87).
| bind | action called | notes |
|---|---|---|
| command_CallToArms | _m.mh.calltoarms.OnAction() -> UIGame.CallToArms(userCrystal) | |
| command_BackToWork | _m.mh.backtowork.OnAction() -> UIGame.BackToWork() | |
| command_BlocksMenu | _m.mh.buildStoneBlocks.OnAction() | |
| command_StructuresMenu | _m.mh.buildStructures.OnAction() | |
| command_CreateProject | _m.mh.newTask.OnAction() | |
| project_QueueSlot1..5 | _m.bh.operableRecipes[1..5].OnAction() | defined past L1700 (L3058-3085) |
| project_ToggleLoop | _m.mh.toggleLoop.OnAction() | L3297 |
| project_ClearQueue | _m.mh.clearQueue.OnAction() | L3321 |
| project_TaskActionSlot1..6 | _m.OnActionSelectedTask(n) | L511-519 (see 2.7) |
| project_RaisePriority / project_LowerPriority | _m.bh.priorityRaise / priorityLower .OnAction() | L2907 |
| project_DeleteTask | _m.bh.taskActionsDelete.OnAction() | L3021 |
| project_OpenBlueprintManagement | _m.mh.BlueprintActive.OnAction() | L989 |
| project_ToggleXRay | MenuCalls.ToggleXRay() | |

Activate also calls fy_Bricktron.ToggleHotkeys(true) and sets KeyBindingsCategory General off, Projects on (L57-60).
Deactivate reverses these (L88-91).

### 2.3 Input mode TASKS (L95-134)
| bind | action (handle) | defined at |
|---|---|---|
| project_CreateHarvestTask | _m.bh.newHarvestTask | L1725 / L1900 (past range) |
| project_CreateWoodTask | _m.bh.newHarvestWoodTask | past range |
| project_CreateBoulderTask, CreateIronTask, CreateBrimstoneTask, CreatePlantsTask | _m.bh.newHarvestBoulderTask, newHarvestIronTask, newHarvestBrimstoneTask, newHarvestPlantsTask | past range |
| project_CreateRepairTask | _m.bh.newRepairTask | L1711 |
| project_CreateQuarryTask | _m.bh.newQuarryTask | L1987 |
| project_CreateCleaningTask | _m.bh.newCleanupTask | L1800 |
| project_CreateLandscapeTask | _m.bh.newLandscapeTask | L1963 |
| project_CreateTunnelTask | _m.bh.newTunnelTask | L1938 |
| project_CreateBuildTask | _m.bh.newTaskBuild | L2018 |
| command_CallToArms, command_BlocksMenu, command_StructuresMenu | same handles as in Projects | |

Deactivate (L116-134) unregisters the 12 project_Create* names and the 3 command_* names. No KeyBindingsCategory change here.

### 2.4 Input mode BRICKS (L136-229)
Activate (L137-182) registers command_CallToArms (mh.calltoarms), command_StructuresMenu (mh.buildStructures) and
command_CreateProject (mh.newTask). Each project_* bind below calls Picking.SetHeldBlueprint(AssetKey.New("Blueprints", KEY)):

| bind | key | bind | key |
|---|---|---|---|
| project_StandardBrick | Stone_DoubleBrick | project_RoundBrick | Stone_Brick_Round |
| project_StairBrick | Stone_Staircase | project_RoundRight | Stone_DoubleBrick_RoundR |
| project_HalfStair | Stone_StairSingle | project_RoundLeft | Stone_DoubleBrick_RoundL |
| project_StairSpiralLeft | Stone_StairSpiralLeft | project_RoundHalf | Stone_DoubleBrick_RoundLR |
| project_StairSpiralRight | Stone_StairSpiralRight | project_Parapet, Parapet2, Parapet3 | Stone_Parapet1, Stone_Parapet2, Stone_Parapet3 |
| project_StairCornerLeft | Stone_StairCornerLeft | project_HalfBrick | Stone_Brick |
| project_StairCornerRight | Stone_StairCornerRight | project_Plank | Wood_SinglePlank |
| project_StairCornerSingle | Stone_StairCornerSingle | project_Corbel, Corbel2 | Stone_Brace1, Stone_Brace2 |
| project_PlugBrick | Stone_BeamPlug_OneSide | project_WoodenRamp | Wood_Ramp_TopA |
| project_PlugH | Stone_BeamPlug_TwoSides | project_RampJunction | Wood_Ramp_JunctionA |
| project_PlugL | Stone_BeamPlug_Corner | project_WoodColumn | Wood_VerticalBeam |
| project_PlugT | Stone_BeamPlug_ThreeSides | project_WoodBeam | Wood_Beam |
| project_PlugX | Stone_BeamPlug_AllSides | project_Log | Wood_SingleLog |
| project_ArchBrick | Stone_Arch | project_Brace | Wood_Brace |
| project_Pillar | Stone_Column_Base | project_Merlon, MerlonDouble, MerlonCorner, MerlonConcave | Stone_Merlon, Stone_MerlonDouble, Stone_MerlonCorner, Stone_MerlonInverseCorner |

Also project_RaisePriority, project_LowerPriority, project_DeleteTask (L179-181) call the same handles as Projects.
Deactivate (L185-228) unregisters the 36 brick names above, the 3 priority/delete names, and command_CallToArms,
command_StructuresMenu, command_CreateProject.

### 2.5 Input mode STRUCTURES (L231-292)
Activate (L232-261) registers command_CallToArms, command_BlocksMenu and command_CreateProject (L234-237; same names as
the Deactivate list at L266-268). Then each bind calls Picking.SetHeldBlueprint(AssetKey.New("Blueprints", KEY)) (L238-257):

| bind | key | bind | key |
|---|---|---|---|
| project_Stockpile | Pallet | project_Door | Door |
| project_WeaponRack | Toolrack | project_BearTrap | BearTrap |
| project_Workbench | Workbench | project_ExplosiveBarrel | Barrel |
| project_Loom | Loom | project_Lantern | LanternWard |
| project_ChoppingBlock | ChoppingBlock | project_Sentinel | SentinelWard |
| project_Forge | Forge | project_Healing | HealingWard |
| project_MachineShop | MachineShop | project_Capture | CaptureWard |
| project_Altar | Altar | project_Catapult | Catapult |
| project_Laboratory | Lab | project_Furnace | Furnace |
| project_RopeBridge | RopeBridgePlacement | project_Pylon | PylonGround |

Plus project_RaisePriority, project_LowerPriority, project_DeleteTask (L258-260).
Deactivate (L263-292) unregisters the 20 project_* names above, the 3 priority/delete names and the 3 command_* names.

### 2.6 Input mode BLUEPRINTS (L295-320)
Activate (L296-307): tb_constr_blueprints_duplicate -> BlueprintSelection.Duplicate(); tb_constr_blueprints_delete ->
BlueprintSelection.Delete(); tb_constr_blueprints_expand -> BlueprintSelection.Expand(); tb_constr_blueprints_clear ->
BlueprintSelection.Cancel(). KeyBindingsCategory General off, Blueprints on.
Deactivate (L309-320) unregisters those 4 names; Blueprints off, General on.

### 2.7 Menu events and selection plumbing (L322-447)
| L | item | what it does |
|---|---|---|
| 327-350 | _m.OnSetInputMode | if _IsBlueprintSelection() open mh.blueprintMgmt, else close it |
| 338-345 | _m.OnToggleBlueprintManagement | Activate or Deactivate the Blueprints binds |
| 347-348 | _m.onSetInputMode, _m.onToggleBlueprintManagement (Event.New) | onSetInputMode is invoked by many OnActions and by ConnectWhen(Picking.hk_OnSetPicker and hk_OnSetPickerOptions, Opened) at L4064-4065 |
| 352-355 | _m.onSetStatus, onSetPriority, onBricktronPopChanged, onDeleteProject | events; onDeleteProject from ConnectWhen(Project.hk_OnProjectDeleted) L4059 |
| 358-374 | _m._OnSetSelectedProject | records isIdleProject / isBuildProject; invokes onDeselectBuildProject when leaving a build; then invokes onSetSelectedProject |
| 376 | Data.Project.ev_onSetSelected:AddListener(...) | entry point of the above |
| 378-379 | onSetSelectedProject, onDeselectBuildProject | refresh source for most handles in 1-1700 |
| 383-392 | OnToggleSidebarSpacerHack | Hooks.Invoke(UIGame.hk_SetSidebarSpacer, true or false) and edit-mode sound in or out |
| 394-407 | OnReopenBuild | if build project: ActivateInputModeBlueprints, then reopen mh.buildStoneBlocks or mh.buildStructures by held blueprint meta type |
| 409-419 | OnDeselectBuildProject | Deactivate Blueprints, Bricks, Structures; Activate Projects; _m.mg.left:CloseAll() |
| 421-428 | listeners | onSetSelectedProject -> spacer hack; onDeselectBuildProject -> OnDeselectBuildProject; onSetPickerOptions -> Data.Project:OnSetHeldBlueprint() and OnReopenBuild |
| 424, 426 | onResourceTypeKeptChanged, onSetPickerOptions | onResourceTypeKeptChanged invoked by ConnectWhen L4074-4075 (tunnel and quarry hk_ResourceTypeKeptChanged); onSetPickerOptions by ConnectWhen(Picking.hk_OnSetPickerOptions) L4066 |
| 432-441 | onSetTunnelType, onSaveCompoundBlueprint, onUpdateMapThumbnail, onUpdateBlueprintThumbnail, OnDoneInstantiating, OnCompoundSelected, OnUpdateCompounds, OnBlueprintScreenshotTaken, OnCompoundDeleted, onBPSelectionChanged (L4067) | Event objects |
| 443-444 | showHelp -> mh.tutorial:Open() | invoked by ConnectWhen(UIGame.hk_ShowHelp) L4078 |
| 446 | onSetAllowDebugMode | ConnectWhen(UIGame.hk_OnSetAllowDebugMode) L4079 |
| 452-460 | userCrystal | the fy_Crystal whose faction equals fy_User.localUser.alliedFaction; computed once at load; used by Call to Arms (L933) |
| 469-473 | _IsHoldingBlueprints, _SetIsHoldingBlueprints, _IsBlueprintSelection, _ToggleBlueprintSelection | Picking.IsHoldingBlueprints / SetIsHoldingBlueprints / IsSelectingBlueprints / SetIsSelectingBlueprints |
| 475-477 | _stoneBlockKey (Stone_DoubleBrick), _woodLogKey (Wood_SingleLog), _stockpileKey (Pallet) | AssetKey.New("Blueprints", ...) |
| 480-488 | IsHeldBlueprintOfMetaType(tgtMeta) | checks Data.Project:GetHeldBlueprint() against a Meta table |
| 489-497 | GetHeldBlueprintHighlightMode(tgtBp) | HighlightMode.Bottom if the held blueprint equals tgtBp, else None |
| 500-509 | TogglePaused | MenuCalls.TogglePause(); sound pausein or pauseout based on InputModeController.IsPaused() |
| 511-519 | OnActionSelectedTask(v) | slot 1: harvestBoulder, tunnelDirUp, landscapeRaiseAll, quarryStairs. slot 2: harvestIron, tunnelDirStraight, landscapeDigAll, quarryKeepStone. slot 3: harvestBrimstone, tunnelDirDown, quarryKeepIron. slot 4: harvestTree, tunnelKeepStone, quarryKeepBrimstone. slot 5: harvestPlant, tunnelKeepIron. slot 6: harvestPlant, tunnelKeepBrimstone. Each calls handle.OnAction() |
| 523-528 | _m.mg, _m.mh, _m.sh, _m.bh, _m.dh | plain tables |
| 530-532 | ButtonHandle, SectionHandle, DividerHandle = MenuHandle | aliases |
| 538-556 | MenuGroups | left (exclusive; onAnyOpen: ResetAllKeyBindingCategory when none open, GS_Scoreboard:SetVisible(not any open)), right (exclusive), top, projectContext, newTask, blueprintMgmt, settings, tutorial, chat |

### 2.8 Top bar (L561-583)
| handle (L) | sets | refresh |
|---|---|---|
| mh.topSpacer (564-570) | Visible true; BgColorDefault = _bgColorSelected; added to mg.top | none |
| mh.topLabel (574-582) | LabelText = Data.Project:GetName(Data.Project:GetSelectedType()) | ev_refresh on onSetSelectedProject |

### 2.9 Left floating bar, project list (L588-801)
| handle (L) | Path | Label loc key | Icon | Click / OnAction | Visible / refresh / notes |
|---|---|---|---|---|---|
| mh.score (590-605) | SidebarScoreMenu.lua | ##gamemenu_score | UI_Score | h:Toggle() | skipOpenClose; hasHotkey false; refresh on onSetSelectedProject and left.onAnyOpen |
| mh.blueprintsMenu (609-637) | SidebarFloatingMenu.lua | ##gamemenu_taskbar_blueprints | SelectBlueprints; IconColor = build project ? CastleRed : Gray; LabelColor CastleRed | if not paused and build project: h:Open(), Picking.SetIsSelectingBlueprints(true), onSetInputMode | onOpen resets buttons; no hotkey |
| mh.buildStoneBlocks (641-673) | SidebarFloatingMenu.lua | ##gamemenu_taskbar_blocks | UI_Brick, red | fy_BuildBlocksProject.ChangeDefaultBlueprint("Stone_DoubleBrick"); build project: SetHeldBlueprint(_stoneBlockKey) and h:Open(); else if HasSelected: Data.Project:SetSelection(GetIdle(), true) and InputModeController.BricksPanelButtonPressed() | onOpen: DeactivateProjects, DeactivateStructures, ActivateBricks; hotkey Camera/command_BlocksMenu |
| mh.buildStructures (677-710) | SidebarFloatingMenu.lua | ##gamemenu_taskbar_structures | UI_Stockpile, red | ChangeDefaultBlueprint("Pallet"); held _stockpileKey; InputModeController.StructuresPanelButtonPressed() | onOpen: DeactivateProjects, DeactivateBricks, ActivateStructures; hotkey Camera/command_StructuresMenu |
| mh.newTask (714-747) | SidebarFloatingMenu.lua | ##gamemenu_taskbar_newtask | _UI_Pick, red | DeactivateProjects, ActivateTasks; if IsSelectedNotOfTypeIdle: SetSelection(GetSelected(), false) and h:Open(), else h:Toggle() | onOpen: Data.Operable:DeselectAll(); onClose: DeactivateTasks, ActivateProjects; closed by onSetSelectedProject (non-idle, L741) and Data.Operable.ev_onSetSelected (L742); hotkey Camera/command_CreateProject |
| mh.leftSpacer1px (749-761) | none | - | - | - | Height = 1; refresh on selection and left.onAnyOpen |
| mh.leftSpacer (764-778) | none | - | - | - | FlexibleHeight 1, Height -1; BgColorDefault selected while newTask is open |
| mh.taskLimit (780-801) | none (ButtonHandle) | (#Data.Project:GetAll() - 1) .. " / " .. (Project:GetProjectCap() - 1) | _UI_Pick, transparent | none | LabelColor CastleRed, LabelFontSize 12, Alignment MiddleCenter; hotkey text ##gamemenu_taskbar_tasknumber (font 12, UpperCenter); refresh on onSetSelectedProject and onDeleteProject |

### 2.10 Right bar (L805-899)
| handle (L) | Path | Label | Icon | Action | Visible / notes |
|---|---|---|---|---|---|
| mh.home (806-822) | none (ButtonHandle) | ##gamemenu_home | UI_Home | if paused: TogglePaused(); MenuCalls.ToggleMainMenu() | hotkey text literal "F10" |
| mh.settings (825-834) | SidebarFloatingMenu.lua | ##gamemenu_topmenu_settings | UI_Settings | none (default Toggle from AddMenuHandleToggle) | hasHotkey false |
| mh.tutorial (837-847) | Menu_Help.lua | ##gamemenu_ingamehelp_title | UI_Help | Toggle via path | Visible = sandbox mode and not IsMultiplayer() |
| mh.debug (850-859) | TestDebugSidebar.lua | "Editor" (hard-coded English) | UI_Console | via path | no Visible: always shown (dev) |
| mh.movie (862-871) | TestMovieSidebar.lua | "movie" (hard-coded) | UI_Play | via path | dev |
| mh.creative (874-887) | CreativeSidebar.lua | ##gamemenu_debug_title | UI_Console | via path | Visible = AllowsDebugMenu(); refresh on onSetAllowDebugMode |
| mh.chat (890-899) | SidebarHomeMenu.lua | "Chat" (hard-coded) | UI_Chat | via path | hasHotkey false |
| mh.alerts (902-911) | SidebarHomeMenu.lua | ##gamemenu_topmenu_Alerts | UI_Alerts | via path | hasHotkey false |
| mh.backtowork (916-925) | none (ButtonHandle) | ##gamemenu_crystalwheel_backtowork | _UI_Hammer | not paused and UIGame.BackToWork() | hotkey Camera/command_BackToWork |
| mh.calltoarms (930-939) | none (ButtonHandle) | ##gamemenu_crystalwheel_calltoarms | _UI_CallToArms | not paused and UIGame.CallToArms(userCrystal) | hotkey Camera/command_CallToArms |

### 2.11 Build project section (L945-1134)
| handle (L) | kind | Visible | Label / icon | OnAction | notes |
|---|---|---|---|---|---|
| dh.build (946-954) | Divider | build project | - | - | refresh on selection |
| sh.build (957-970) | Section | build project | Label "" (hideHeader), LabelColor red | - | ev_onLoad: children call AddMenuHandleToggle / AddDivider |
| mh.BlueprintActive (973-990) | toggle | build project | SelectBlueprints; IconColor red if IsSelectingBlueprints else Gray; IconSize 32; ##gamemenu_taskbar_blueprints | toggles Picking.SetIsSelectingBlueprints and invokes onSetInputMode | TooltipPath Task/Build/BlueprintActive; refresh on onSetInputMode; in mg.projectContext; registered on sh.build.ev_onLoad (L986) |
| mh.blueprintMove (994-1011) | toggle | build AND selecting | Blueprints_Move, red, 32, ##gamemenu_taskbar_blueprints_move | not paused and BlueprintSelection.Move() | TooltipPath BlueprintMove |
| mh.blueprintCopy (1014-1033) | toggle | build AND selecting | CopyBlueprints, red, 32, ##..._copy | BlueprintSelection.Duplicate() | hotkey tb_constr_blueprints_menu / tb_constr_blueprints_duplicate |
| mh.blueprintSplit (1036-1053) | toggle | build AND selecting | SplitBlueprints, red, 32, ##..._split | BlueprintSelection.Split() | no hotkey |
| mh.blueprintDelete (1056-1075) | toggle | build AND selecting | DeleteBlueprints, red, 32, ##..._delete | BlueprintSelection.Delete() | hotkey tb_constr_blueprints_menu / tb_constr_blueprints_delete |
| dh.blueprintDivider (1078-1090) | Divider | build AND selecting | - | - | OffsetMax (-8,-8); ev_onLoad -> AddDivider |
| mh.blueprintExpand (1093-1112) | toggle | build AND selecting | ExpandSelection, red, 32, ##..._expand | BlueprintSelection.Expand() | hotkey tb_constr_blueprints_menu / tb_constr_blueprints_expand |
| mh.blueprintClear (1115-1134) | toggle | build AND selecting | ClearSelection, red, 32, ##..._clear | BlueprintSelection.Cancel() | hotkey tb_constr_blueprints_menu / tb_constr_blueprints_clear |

All blueprint toggles refresh on onSetInputMode and are registered on sh.build.ev_onLoad (L986-1130).

### 2.12 Landscape section (L1139-1200)
| handle (L) | Visible | Label / icon | Action | Hotkey / notes |
|---|---|---|---|---|
| dh.landscape (1141-1148) | landscape project | - | - | refresh on selection |
| sh.landscape (1151-1164) | landscape project | Label "" (hideHeader), red | - | ev_onLoad |
| mh.landscapeRaiseAll (1167-1182) | landscape | ##gamemenu_taskbar_raiseall, UI_RaiseBrush, 32 | not paused and landscape: Data.Project:GetSelected().UI:RaiseAll() | Projects/project_TaskActionSlot1; TooltipPath Task/Landscape/Raise |
| mh.landscapeDigAll (1185-1200) | landscape | ##gamemenu_taskbar_digall, UI_TerrainHug, 32 | not paused and landscape: Data.Project:GetSelected().UI:DigAll() | Projects/project_TaskActionSlot2; TooltipPath Task/Landscape/Lower |

Both action handles register on sh.landscape.ev_onLoad (L1178). Section and divider refresh on onSetSelectedProject (L1145, 1161).

### 2.13 Quarry section (L1205-1316)
| handle (L) | Visible | Label / icon | Action | Highlight | Hotkey / listeners |
|---|---|---|---|---|---|
| dh.quarry (1205-1210) | quarry project | - | - | - | - |
| sh.quarry (1211-1225) | quarry | Label "", red, no hideHeader | - | - | ev_onLoad |
| mh.quarryStairs (1241-1248) | quarry | ##gamemenu_taskbar_stairs, UI_StairsDown, 32 | UI:NextStairs() | none | Projects/project_TaskActionSlot1 |
| mh.quarryKeepStone (1251-1268) | quarry | ##gamemenu_taskbar_keepstone, _Boulder, White, 64 | ToggleResourceTypeFlag(1) | All if GetResourceTypeFlag(1) | TaskActionSlot2; onResourceTypeKeptChanged (L1268) |
| mh.quarryKeepIron (1283-1290) | quarry | ##gamemenu_taskbar_keepiron, _Raw_Iron, White, 64 | ToggleResourceTypeFlag(4) | flag 4 | TaskActionSlot3; onResourceTypeKeptChanged (L1290) |
| mh.quarryKeepBrimstone (1305-1312) | quarry | ##gamemenu_taskbar_keepiron (same key as iron; likely a Lua copy bug), _Raw_Orange_Crystal, White, 64 | ToggleResourceTypeFlag(2) | flag 2 | TaskActionSlot4; onResourceTypeKeptChanged (L1312) |

Each quarry handle has TooltipPath Task/Quarry/... and registers on sh.quarry.ev_onLoad.
### 2.14 Tunnel section (L1319-1469)
| handle (L) | Visible | Label / icon | Action | Highlight | Hotkey / notes |
|---|---|---|---|---|---|
| dh.tunnel (1322-1325) | tunnel project | - | - | - | - |
| sh.tunnel (1333-1341) | tunnel | ##task_tunnel_options, red | - | - | ev_onLoad |
| mh.tunnelDirUp (1348-1360) | tunnel | ##gamemenu_taskbar_tunnel_up, UI_StairsUp, 32 | SetTunnelType(fy_TunnelProject.stairs.stairsUp) | tunnel type == stairsUp | TaskActionSlot1 |
| mh.tunnelDirStraight (1369-1381) | tunnel | ##gamemenu_taskbar_tunnel_straight, UI_StraightRight, 32 | SetTunnelType(stairs.straight) | == straight | TaskActionSlot2 |
| mh.tunnelDirDown (1390-1402) | tunnel | ##gamemenu_taskbar_tunnel_down, UI_StairsDown, 32 | SetTunnelType(stairs.stairsDown) | == stairsDown | TaskActionSlot3 |
| mh.tunnelKeepStone (1412-1425) | tunnel | ##gamemenu_taskbar_keepiron (wrong key for stone), _Boulder, White, 64 | ToggleResourceTypeFlag(1) | flag 1 | TaskActionSlot4; TooltipPath says Quarry/KeepStone (copy); onResourceTypeKeptChanged (L1425) |
| mh.tunnelKeepIron (1434-1447) | tunnel | ##gamemenu_taskbar_keepiron, _Raw_Iron, White, 64 | ToggleResourceTypeFlag(4) | flag 4 | TaskActionSlot5; L1447 |
| mh.tunnelKeepBrimstone (1456-1469) | tunnel | ##gamemenu_taskbar_keepiron (same key), _Raw_Orange_Crystal, White, 64 | ToggleResourceTypeFlag(2) | flag 2 | TaskActionSlot6; L1469 |

### 2.15 Harvest section (L1477-1625)
| handle (L) | Visible | Label / icon | Action | Hotkey / listeners |
|---|---|---|---|---|
| sh.harvest (1477-1488) | harvest project | ##task_harvest_options, red, hideHeader | - | ev_refresh and ev_onLoad; refresh on onSetSelectedProject |
| dh.harvest (1495-1500) | harvest | - | - | refresh on selection |
| mh.harvestBoulder (1506-1520) | harvest | ##gamemenu_taskbar_boulders, _Boulder, White, 64 | ToggleNatureTypeFlag(NatureType.boulder) | TaskActionSlot1; Highlight via GetNatureTypeFlag |
| mh.harvestIron (1531-1545) | harvest | ##gamemenu_taskbar_iron, _Raw_Iron, White, 64 | ToggleNatureTypeFlag(NatureType.iron) | TaskActionSlot2 |
| mh.harvestBrimstone (1556-1570) | harvest | ##gamemenu_taskbar_brimstone, _Raw_Orange_Crystal, White, 64 | ToggleNatureTypeFlag(NatureType.pyrite) | TaskActionSlot3 (enum name is pyrite) |
| mh.harvestTree (1581-1595) | harvest | ##gamemenu_taskbar_trees, _Pine_Tree, White, 64 | NatureType.tree | TaskActionSlot4 |
| mh.harvestPlant (1604-1620) | harvest | ##gamemenu_taskbar_plants, _Plant, White, 64 | NatureType.plant | TaskActionSlot5 |

Each harvest action: onRefreshHighlight on onSetPickerOptions (L1521, 1546, 1571, 1596, 1621); ev_refresh on onSetSelectedProject;
registered on sh.harvest.ev_onLoad. Guard in every action: not paused and Data.Project:IsSelectedOfTypeHarvest().

### 2.16 Alerts and task panel header (L1628-1700)
| handle (L) | content |
|---|---|
| bh.spacerPanel (1628-1640) | MenuHandle, Visible true; on mh.newTask.onLoad -> m.AddMenuHandleSpacer(h, sh.newRepairTask); refresh on newTask.onOpen |
| bh.alertPanel (1645-1670) | "too many tasks": Visible = not Project:ProjectUnderCap(); Icon _UI_Warning, red, 32; Count = ##gamemenu_help_popup_toomanytasks; CountColor (0.6,0.6,0.6,1); Label_Alignment MiddleLeft; Highlight All; on newTask.onLoad -> m.AddAlertHandle(h, sh.newRepairTask); refresh on onSetPickerOptions and newTask.onOpen |
| sh.newRepairTask (1680-1686) | SectionHandle; Label ##gamemenu_taskbar_maintenancetasks, red; on newTask.onLoad -> m.AddSection(h) |
| bh.newRepairTask (1691-1711; fields seen 1691-1700) | Icon _Repair_Brick; IconColor ProjectUnderCap ? White : Gray; IconSize 32; Label ##gamemenu_taskbar_repair; hasFgLabel; FrontImage 64x64; OnAction not paused and under cap and UIGame.NewRepairTask(); Highlight Bottom if UIGame.IsPlacingNewRepairTask() |

Section 1700 onward (rest of repair task, harvest/cleanup/landscape/quarry/build task creators, operable queue) is outside this file (see inv-b).

---------------------------------------------------------------------------
## 3. Open items and oddities for the C# port
1. Copy-paste loc keys: quarryKeepBrimstone (L1305-1312), tunnelKeepStone (L1412-1425) and tunnelKeepBrimstone (L1456-1469) use ##gamemenu_taskbar_keepiron. Port as-is or fix knowingly.
2. Hard-coded English labels: "Editor" (L853), "movie" (L865), "Chat" (L893). Other labels are loc keys.
3. Hotkey text "F10" is a literal (L818); other hotkey labels come from UIGame.GetKeyBindingCodename(category, bind).
4. _m.bh.operableRecipes (L3058) and the priority/delete handles are defined past L1700.
5. The host sidebar for sh.build and the other section handles was not traced (section 1.5).
6. userCrystal is captured once at load (L452-460) and is not refreshed; Call to Arms uses that value.
7. Task-limit label (L783) shows (count - 1) and (cap - 1) because the idle task is excluded.
8. GameMenu.lua is cp1252: do not round-trip it through UTF-8 tools.

### 1.7 Setter-name check (requested SetLabel / SetIcon / SetSize / SetColor / SetOnClick)
- These names do NOT occur in GameMenu.lua, LUI2.lua, MenuHandle.lua, LUI/Panels/ButtonPanel.lua or LeftBarMenu.lua (0 hits).
  The only Lua file with SetLabel is LUI/Menus/GS_TopMenuB.lua (legacy, not in scope).
- Equivalent in this code base: handle fields Label, Icon, IconColor, IconSize, OnAction, Visible, Highlight, Count (plain Lua keys)
  and, at widget level, Handles.<Name> via SetWidgetHandle (1.3). Flags: MenuFlag / BuilderFlag via Menu SetFlag / ClearFlag (1.4).
