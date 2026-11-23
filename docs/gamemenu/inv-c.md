# Game menu sub-files: inventory C (ref/lua/LUI/Menus/Game/)

Read-only lookup of the vanilla Lua reference (ref/lua). Line numbers = file lines.
Loader column: where GameMenu.lua (ref/lua/LUI/Menus/GameMenu.lua) loads the file. "none" = not loaded there (checked by grep).
Abbrev: BtnPanel = LUI/Panels/ButtonPanel.lua; LBtnPanel = LargeButtonPanel.lua; HdrPanel = HeaderPanel.lua; LblDivider = LabeledDividerPanel.lua.

## Summary table

| file | lines | loader | purpose (what the player sees) |
|---|---|---|---|
| TopBarMenu.lua | 201 | GameMenu 3542 | top strip of icon buttons with hover labels; toggle / spacer / flex spacer / label helpers |
| LeftBarMenu.lua | 346 | GameMenu 3591, 3822 | left vertical icon rail (toggle, compound toggle, spacer, group spacer, divider) |
| LeftContentMenu.lua | 81 | GameMenu 3651, 3796 | socket: opens the child menu of a rail button in a panel with a header |
| RightBarMenu.lua | 469 | none (dead) | old right rail (Home/Chat/Alerts sockets); dofile paths point to LUI/Menus/SidebarHomeMenu.lua which does not exist |
| SidebarFloatingMenu.lua | 628 | GameMenu 611,643,679,716,827 (SetPath) | floating side panel: blueprints, blocks, structures, new task, settings; build/landscape/quarry/tunnel sub-buttons |
| SidebarTaskMenu.lua | 282 | none | old task sidebar (toggle, LblDivider section, header); read only to line 200 |
| SidebarHomeMenu.lua | 165 | GameMenu 892, 904 (Chat, Alerts handles) | upper-right strip of toggles (Count, icon, hover label) |
| SidebarMapMenu.lua | 99 | none (only LeftSidebarMenu.lua:431, itself unused) | EMPTY container, NO minimap |
| SidebarStatsMenu.lua | 67 | none | EMPTY container with header |
| SidebarScoreMenu.lua | 67 | GameMenu 592 (label ##gamemenu_score) | EMPTY container with header; score content not in this file |
| SidebarQuickMenu.lua | 46 | none | EMPTY horizontal container, disabled image |
| SidebarChatMenu.lua | 68 | none (RightBarMenu:403 only) | header text hard-coded "Chat" (line 20), empty container |
| SidebarAlertsMenu.lua | 68 | none (RightBarMenu:431 only) | header text hard-coded "Alerts" (line 20), empty container |
| Menu_TaskList.lua | 1203 | GameMenu 3676 | task list: project rows (crew, priority, selected operable) |
| Menu_Popup.lua | 665 | GameMenu 3704 | popup for a task row (targetPanel set by TaskList) |
| Menu_Tooltip.lua | 633 | GameMenu 3735, 3765 (doShowTooltips) | tooltip box and hover arrow, article content |
| IsPausedMenu.lua | 164 | GameMenu 3849 | "paused" banner over the screen |
| Menu_Modal.lua | 463 | GameMenu 3873 | modal dialog host (one line only) |
| Menu_Help.lua | 260 | GameMenu 839 (SetPath) | in-game help article list (one line only) |
| Menu_Article.lua | 75 | Data/Data_Article.lua:179 (loadfile) | article page (one line only) |
| Tutorial*Menu.lua | - | TutorialSidebarMenu.lua:144 | tutorial sidebar, info, objective, article (one line only, not read) |

## Common building blocks (all Game/ files)
- Menu:New(), then `_m.AddPanel(p)` / `_m.lyt.AddPanel(p)`; flags via `_m.SetFlag(MenuFlag.OpenedByParentMenu / OpensChildMenus / OpenOnLoad / OnOpen / OnClose)`.
- Every panel is built with the fluent `LayoutPanels.X:New().SetWidgetHandle(Widgets.Y, Handles.Z, ||value, true)` API. Constructor names map to C# in ref/vanilla/Brix/UI/Builder/Lui/LuiBuilder.cs:25-62:
  LayoutPanels.Vertical/Horizontal/Grid/Scrollable/Slidables -> VerticalLayoutPanel, HorizontalLayoutPanel, GridLayoutPanel, LuaScrollableLayoutPanel, SlidableLayoutPanel;
  Panels.Label -> LabelPanel; Panels.Image -> ImagePanel; Panels.Buttons.Standard -> StandardButtonPanel; Panels.InputField -> LuaInputFieldPanel; Panels.Minimap -> MinimapPanel.
- Visibility: `Handles.Visible` with `||fn()` (re-evaluated by the C# side each refresh).
- Colours used: `_bgColorSelected`, `_bgColorNone`, `_bgColorHover`, `_colorYellow`, `CastleYellow`, `Color.New(0,0,0,0.8)`, arrow colour `Color.New(22/255,22/255,22/255,1)`. Their Lua definitions are outside Game/ (not traced).
- Refresh: `ev_refresh` / `Event` per panel; `onOpen` / `onClose` of the menu handle; pointer enter/exit calls `_m.RefreshAll()`.
- Hover/highlight: `_GetHover()` over registered listeners (TopBar 26-31, SidebarHome 25-31).
- Shared icon/label source: GameMenu.lua sets `h.Label = ||GetLocalized("##...")` and `h.Icon = ||IconKeys.<Name>:Get64()`.

## TopBarMenu.lua (201)
- AddMenuHandleToggle L49-87: BtnPanel; Image bg 0,0,0,0.8 (hover 0,0,0,0.75 via HoverBgImage); FrontImage_Sprite = mHandle.Icon(); FrontLabel = Count (font CountFontSize); HoverLabel = Label; Button_OnAction = mHandle.OnAction (default mHandle:Toggle()); Button_Highlight; attached to `_m.sections[sHandle]` or `_m.lyt`.
- AddMenuHandleSpacer L89-125: SpacerPanel, width -1, height 64, flexible width; bg = BgColorSelected / AnySelected / Hover / Default.
- AddMenuGroupSpacer L127-155: flexible spacer, bg by group open state.
- AddMenuHandleLabel L157-186: Panels.Label stretched, text = LabelText(), colour _colorYellow, font Font.KorolevLight, size 24, centred.
- Flags: OpenOnLoad, OpensChildMenus, OpenedByParentMenu (L193-195).
- Data: none (only handle fields). Refresh triggers: onOpen/onClose, ev_refresh (L178-180 of the label helper).

## LeftBarMenu.lua (346)
- AddMenuHandleToggle L67-124: BtnPanel, icon (FrontImage_Sprite), hover label (HoverLabel_Text, LabelColor yellow), hotkey label (HoverHotkeyLabel, font from HotkeyLabel_FontSize). Fires `mHandle.OnAction`. Refresh: ev_refresh.
- AddMenuHandleCompound L140-200: BtnPanel with FrontLabel under the icon (LabelFontSize 12, LowerCenter), hotkey label UpperRight size 12, bg per state.
- AddMenuHandleSpacer L212-250: SpacerPanel, width -1, height = Height (default 48), flexible width; visible = mHandle:IsOpened() by default.
- AddMenuGroupSpacer L259-290: flexible spacer, bg = group any-open colour.
- AddMenuGroupDivider L296-322: DividerPanel, width 48, height 1.
- Side effect: L51-54 `Hooks.Invoke(UIGame.hk_SetSidebarSpacer, {true|false})` when a hover/open child exists.
- Labels/icons: none here (set by GameMenu.lua).

## LeftContentMenu.lua (81)
- AddMenuHandleSocket L24-67: `m = mHandle:Load()`; a LayoutPanels.Vertical with Visible = mHandle:IsOpened(), IgnoreLayout, anchors stretch (zero..one), pivot (0.5,0), image disabled. Header label = mHandle.Label() (L44), leftBtn hidden, rightBtn shown (L45-46). Refresh on onOpen/onClose (L54-55).
- Yields a coroutine (L61-64) so the child menu loads over several frames.

## RightBarMenu.lua (469) - NOT LOADED
- Sockets for Home/Chat/Alerts (L375, 403, 431 use dofile "LUI/Menus/Sidebar*Menu.lua", paths without /Game/: missing files). Buttons: IconKeys.UI_Home (L291), UI_Chat (L312), UI_Alerts (L350); widths 48 (L244) / 320 (L263) pivot right.
- Treat as legacy. Not traced further.

## SidebarFloatingMenu.lua (628)
- Root baseLyt L~530-541; header HdrPanel L545-565 (lbl colour CastleYellow L558, leftBtn/rightBtn -> _OnClose L560-561, divider 1 px).
- Scroll body: LayoutPanels.Scrollable L568-579, inner Vertical padding (8,8,0,0), spacing 0, upper-left (L582-597); scroll button Panels.Buttons.Standard, stretched (L601-613).
- AddMenuHandleToggle L78-165: BtnPanel (L80-160) or LBtnPanel (L214) or AlertPanel (L318); size Width/Height default 64x64, icon FrontImage 32x32 (L89-92); BackImage_Sprite = Icon; FrontLabel = Count; HoverLabel = Label; highlight; BgColor 0,0,0,0.8. Each path has a skipMap (L98-110, L202-214, L306-318).
- AddMenuHandleSpacer L366-405: height 48 default, flexible, bg by state.
- AddMenuHandleInputField L413-450: ImagedButtonInputFieldPanel2, height 48, bound to BlueprintSelection.GetCurrentName / SetCurrentName (L435-436). Used for the blueprint name.
- AddSection L463-508: LblDivider, default label GetLocalized("str_we_help") (L467, marked TODO), label pivot (0,0.5).
- Data sources: Data.Project (IsSelectedOfType*, GetAll, GetProjectCap), Picking.IsSelectingBlueprints(), BlueprintSelection. Set in GameMenu.lua, not here.
- Sub-sections built in GameMenu.lua via `_m.sh.build / landscape / quarry / tunnel` `ev_onLoad` (GameMenu 986-1417): visible only when the selected project is of that type (IsSelectedOfTypeBuild / Landscape / Quarry / Tunnel, GameMenu 948-1417).
- Hooks: ev_refresh and onRefreshHighlight per handle (L153, 157, 263, 267, 343, 347).
- Not read: lines ~80-540 read by grep only, inner details of the three toggle paths.

## SidebarTaskMenu.lua (282) - NOT LOADED (read to line 200)
- AddMenuHandleToggle L79 (BtnPanel, same pattern as Home), AddSection L141-146 (LblDivider, "str_we_help"), spacer L187, header L221. Legacy, superseded by SidebarFloatingMenu sections.

## SidebarHomeMenu.lua (165)
- Listeners L11-31 (_RegisterListener, _GetHover); RefreshAll L40-45; SetMenuHandle / Close L49-58.
- AddMenuHandleToggle L61-110: BtnPanel, Visible = mHandle.Visible(), Image_Color = BgColor() (hover 0,0,0,1 else 0,0,0,0.8), FrontImage_Sprite = Icon, FrontImage_Color = IconColor (default CastleYellow), FrontLabel = Count, HoverLabel = Label, Button_Highlight, pointer enter/exit -> RefreshAll (L90-91). Adds itself to `_m.lyt` (L102).
- Root L120-138: LayoutPanels.Horizontal, alignment UpperRight, MinHeight 48, flexible width -1 (no fill), bg 0,0,0,0.8.
- Spacer L142-152: SpacerPanel, width -1, height 48.
- Used by GameMenu.lua 892 (Chat) and 904 (Alerts) via SetPath.

## SidebarMapMenu.lua (99) - EMPTY, NO MINIMAP
- Three nested Vertical containers (L19-35, L86-93): bg 0,0,0,0.8 on the first one, header HdrPanel (L39-52). No Panels.Minimap, no labels, no data.
- Loaded only by LeftSidebarMenu.lua:431 (dofile "LUI/Menus/SidebarMapMenu.lua", a path that does not exist). LeftSidebarMenu.lua is not loaded by GameMenu.lua. Treat as dead.

## SidebarStatsMenu.lua (67), SidebarScoreMenu.lua (67) - EMPTY
- Vertical container, bg 0,0,0,0.8, header HdrPanel (no text set), flags OpensChildMenus / OpenedByParentMenu.
- Score: GameMenu 592-594: handle Label ##gamemenu_score, Icon IconKeys.UI_Score. The label is copied into the header by LeftContentMenu L43-44. Score rows are not in this file.
- Stats: not referenced by GameMenu.lua.

## SidebarQuickMenu.lua (46) - EMPTY
- Horizontal container, Image Enabled false, flexible 1/1, IgnoreLayout false. Not referenced.

## SidebarChatMenu.lua (68), SidebarAlertsMenu.lua (68) - EMPTY with hard-coded header
- Header HdrPanel with lbl text hard-coded: "Chat" (L20) and "Alerts" (L20). Not localised (bypasses GetLocalized).
- Body: empty Vertical, image disabled. Real chat/alerts UI is elsewhere (GameMenu handles Chat/Alerts load SidebarHomeMenu.lua at 892/904).

## Menu_TaskList.lua (1203) - TASK LIST (projects), NOT units
- Loader GameMenu 3676. Module state: _data (L100-272) with query helpers: GetFightersInfo 103, GetIdlersInfo 108, GetBuildersInfo 113, GetTotalWorkerCount 120, GetTotalMeleeCount 126, GetTotalRangedCount 131, ProjectExists 137, GetSelected 150, GetProject 167, GetProjectCrewCount 176, GetProjectAssignedCrewCount 184, GetProjectFreeAgentCount 200, GetProjectName 256, GetProjectPrioritySprite 272.
- Row panel (_GetProjectPanel, from L444): Horizontal, MinHeight/PreferredHeight 72, padding 2, alignment LowerLeft, bg = _BgColor(id) (L466), Raycasts false.
  - tooltip arrow: Panels.Image 64x64 anchored bottom-left, position (28,52), pivot (0.5,0), sprite IconKeys._UI_Big_Arrow_Down (L474-495).
  - popup arrow: Panels.Image 64x64 bottom-centre, position (0,52) (L498-519).
  - task lyt: Horizontal, MinWidth 12, MinHeight 48, Preferred 48 (L522-529).
  - icon: Panels.Image sized _IconSize(id), sprite _IconSprite(id) (L544-551, alignment MiddleLeft L532).
  - label: Panels.Label, offsets (8,6)/(-8,-6) (L640-646).
  - priority badge: Panels.Image 32x32, bottom-right position (0,6), sprite _data.GetProjectPrioritySprite (L661-672).
- Header/title text (L256-264): knights -> ##gamemenu_taskbar_meleeunits, archers -> ##gamemenu_taskbar_rangedunits, operable -> Data.Operable:GetName(Data.Operable:GetSelectedType()) (L262-264), else project name.
- Icons: priority IconKeys._UI_PriorityLow_alt / Medium / High / Locked (L275-278); edit/close text ##gamemenu_taskbar_edit / ##gamemenu_taskbar_close (L311-316); up/down UI_Up/UI_Down (L301-306); knights UI_Sword (L322), archers UI_Bow (L323), default UI_Help (L321), operable icon Data.Operable:GetIcon(projectType) (L328).
- Crew list: loadfile("LUI/Panels/TaskbarProjectCrewPanel.lua")(_data) (L625), not read.
- Actions: Data.Project:IsSelected/GetSelected, Data.Operable:DeselectAll (L367), popup open via `_m.m_popup.ev_onSet:Invoke({targetPanel = ...})` (L786, 925), tooltip via `_m.m_tooltip.ev_onSet` (L411).
- Data: Data.Project (many calls), Data.Operable (HasSelected L140, 161-170, 262-264, 328; ev_onSetSelected L1150/1157). Definition of Data.Operable not found by grep in ref/lua/LUI (semantics inferred: the selected placed object/building, not a unit).
- Hooks (L1165-1185, all with MenuState.Opened): Picking.hk_OnSetPickerOptions; Project.hk_OnProjectStarted, hk_OnProjectDeleted, hk_OnProjectSelected, hk_OnAddLabor2, hk_OnRemoveLabor2, hk_OnSetCrewInfo; UIGame.hk_OnOccupationChanged; hk_OnSetIsShift / hk_OnSetIsCtrl (crew hover refresh); hk_Update (per frame, comment says ~0.6k alloc per frame, L1185).

## Menu_Popup.lua (665)
- Loader GameMenu 3704. Target: a task row (`ev_onSet` with targetPanel, TaskList 786/925).
- Buttons: loadfile("LUI/Panels/ButtonPanel.lua")({skipMap, str_name}) with skipMap flags hasNoHover / hasHotkey (L375-387); bg _bgColorSelected (L212, 520).
- Article content: Data.Article (5 hits). Not read in full.

## Menu_Tooltip.lua (633)
- Loader GameMenu 3735 and 3765 (loadfile with doShowTooltips = true): two instances; offset -50 x when not doShowTooltips (L54).
- Box: LayoutPanels.Horizontal "TooltipMenu.lyt" (L201-219): Visible = _m.Visible() (= targetDriver or targetPanel set, L46), HideFirstFrame, AnchoredPositionTracking = _PosTracking() (L207), AnchoredPositionClamping = _Clamping() (L208), anchors (0,0.5) pivot (0,0.5), AnchoredHeight = _Height() (L217), width _Width() (L51 -> _width), alignment LowerLeft, bg _bgColorSelected (L212).
- Article panes: one Vertical per Data.Article.Tooltip entry, Visible = tooltipActive == func (L239-249, L207-217 in the 2nd copy).
- Hover arrow: Panels.Image 64x64 at (0,50), sprite UI_Big_Arrow_Down, visible while hovering (L276-290 region, L300-313).
- Trigger: `_m.m_tooltip.ev_onSet:Invoke({targetPanel, tooltip})` from button pointer enter (L310 region). Not read in full beyond these hits.

## IsPausedMenu.lua (164)
- Loader GameMenu 3849. Data: UIGame.IsPaused() (L45 visibility).
- Root L44-57: Horizontal, Visible = UIGame.IsPaused(), IgnoreLayout, anchors stretch, image disabled, colour clear.
- Box L63-80: Horizontal 256 x 60 (AnchoredWidth/Height), anchor top-centre (0.5,1), position (0,-100), bg _bgColorSelected (L75).
- Title L103-121: Panels.Label, text GetLocalized("##gamemenu_paused"), colour _colorYellow, font Font.KorolevLight, size 24, centred, PreferredHeight 24.
- Subtitle L125-143: text GetLocalized("##gamemenu_paused_text"), size 16, PreferredHeight 16.
- Refresh: hk UIGame.hk_OnSetIsPaused via ConnectWhen(..., MenuState.Loaded) (L150) -> onSetIsPaused -> RefreshAll (L16).

## Menu_Modal.lua (463), Menu_Help.lua (260), Menu_Article.lua (75), Tutorial*.lua
- Modal (463): generic modal dialog host, loaded GameMenu 3873. One line, not read further.
- Help (260): in-game help articles; GameMenu 839-842 shows the button only in sandbox and not multiplayer (UIGame.GetModeName() == "##mode_sandbox" and not IsMultiplayer()), label ##gamemenu_ingamehelp_title, icon UI_Help.
- Article (75): article page, loaded per article by Data/Data_Article.lua:179 (loadfile with func_getData, t_path, menu).
- Tutorial*: tutorial sidebar / info / objective / article panels, opened from TutorialSidebarMenu.lua:144 (TutorialArticleMenu). Not in the GameMenu loader list.

## GameMenu.lua sidebar handles (icons/labels, lines from grep)
| handle | line | label key | icon | file |
|---|---|---|---|---|
| score | 592-594 | ##gamemenu_score | UI_Score | SidebarScoreMenu.lua |
| blueprints | 611-614 | ##gamemenu_taskbar_blueprints | SelectBlueprints | SidebarFloatingMenu.lua |
| blocks | 643-646 | ##gamemenu_taskbar_blocks | UI_Brick | SidebarFloatingMenu.lua |
| structures | 679-682 | ##gamemenu_taskbar_structures | UI_Stockpile | SidebarFloatingMenu.lua |
| new task | 716-718, 783-784 | ##gamemenu_taskbar_newtask (text n / cap) | _UI_Pick | SidebarFloatingMenu.lua |
| home | 808-809 | ##gamemenu_home | UI_Home | (no SetPath) |
| settings | 827-829 | ##gamemenu_topmenu_settings | UI_Settings | SidebarFloatingMenu.lua |
| help | 839-842 | ##gamemenu_ingamehelp_title | UI_Help | Menu_Help.lua |
| chat | 892-894 | Chat (hard-coded) | UI_Chat | SidebarHomeMenu.lua |
| alerts | 904-906 | ##gamemenu_topmenu_Alerts | UI_Alerts | SidebarHomeMenu.lua |
| back to work | 917-918 | ##gamemenu_crystalwheel_backtowork | _UI_Hammer | (wheel) |
| call to arms | 931-932 | ##gamemenu_crystalwheel_calltoarms | _UI_CallToArms | (wheel) |

## Mini map (answer)
- Panel: `Panels.Minimap:New()` at LUI/Menus/MinimapMenu.lua:36 (file is top-level, not under Game/).
- C#: MinimapPanel (ref/vanilla/Brix/UI/Builder/Panel/MinimapPanel.cs:8, New at :51, default asset Images.MiniMap at :39 and :67, widget WidgetId.Minimap -> MinimapWidgetComponent, ref/vanilla/Brix/UI/Builder/Widget/MinimapWidgetComponent.cs:8). Engine side: ref/vanilla/Brix/Engine/Minimap.cs:7.
- Layout (MinimapMenu.lua:37-42): Visible true, IgnoreLayout true, anchors zero..one, offsets zero: it fills its parent. No fixed size.
- Menu flags: OpenOnLoad, OpenedByParentMenu (L54-55). Menu is created by gamemode FactionPlayersPanel scripts, e.g. ref/lua/Gamemodes/conquest/Menu/ConquestFactionPlayersPanel.lua:178, ref/lua/Gamemodes/sandbox/Menu/SandboxFactionPlayersPanel.lua:142, and creditisland, invasion, grabthegems (PumpkinFactionPlayersPanel.lua:160).
- Toggle: GS_TopMenu.lua:66-68 "Minimap" button calls MenuCalls.ToggleMinimap() (C# side not traced).
- Game/SidebarMapMenu.lua is NOT the minimap (empty, see above).
- The foreign conquest mod folder (Gamemodes/conquest mod) was not read; only the original conquest folder was cited.

## Selected unit (answer)
- NONE of the Game/ files shows a selected unit's name, job, or health.
- Closest: Menu_TaskList.lua:256-264 header and :328 icon show the selected OPERABLE (Data.Operable: placed object / building selection), not a unit. Project rows show counts only (_data.GetFightersInfo 103, GetIdlersInfo 108, GetBuildersInfo 113, GetTotalWorkerCount 120). Crew rows come from TaskbarProjectCrewPanel.lua (L625, not read).
- Floating panel (SidebarFloatingMenu) selects by project type, not unit.

## Not read / limits
- SidebarFloatingMenu 80-540 and Menu_TaskList 100-440 + 530-1200 read only by grep hits.
- Menu_Popup, Menu_Tooltip, Menu_Modal, Menu_Help, Menu_Article: grep hits + one-liners.
- SidebarTaskMenu 200-282 not read. RightBarMenu read by grep only.
- Lua definitions of colour globals (_bgColorSelected, _colorYellow, CastleYellow) live outside Game/ and were not traced.
