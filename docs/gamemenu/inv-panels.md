# inv-panels: Lua panel scripts used by the in-game menus (1:1 C# port notes)

Scope: LUI/Panels/*.lua files that GameMenu.lua or Menus/Game/*.lua load (dofile/loadfile),
plus the files they load in turn (ImprovedInputFieldPanel.lua). Line numbers are ref/lua paths.
Not read (out of scope or too big): ContouredImagedButtonPanel.lua, TaskbarProjectCrewPanel.lua
(586 lines, SidebarTaskMenu/Menu_TaskList crew rows), LabeledDividerPanel2.lua, Panel_ModalButton*.lua.
LUI2 constructor mapping: see inv-a.md (not repeated here).

## 0. Common Lua panel pattern (all files below)

- Every file starts with require("LUI2.lua"), builds _p and returns it (or _panel).
- Config: _defaultCfg.Key = ||value, isConst. The second flag true means constant (cache).
  Setters _p.Key(fnOrValue, cache) store _userCfg[Key] and return _p (ButtonPanel.lua:185-199).
- UpdateCfg() (ButtonPanel.lua:154-178) builds _cfg.Key(); each call runs _preCfg[Key], the
  value, then _postCfg[Key] (ButtonPanel.lua:169-175).
- _p.RefreshAll() refreshes every registered child; _p.RefreshHover() refreshes only hover children.
- Child factory calls and their C# AssetKey (LuiBuilder.GetNewPanel switch, LuiBuilder.cs:21-64):

| Lua call | C# class | AssetKey (default) | source |
|---|---|---|---|
| LayoutPanels.Horizontal:New() | HorizontalLayoutPanel | Layouts.Horizontal | Layouts.cs:21, HorizontalLayoutPanel.cs:85 |
| LayoutPanels.Vertical:New() | VerticalLayoutPanel | Layouts.Vertical | Layouts.cs:12, VerticalLayoutPanel.cs:92 |
| Panels.Label:New() | LabelPanel | Texts.Default | Texts.cs:9, LabelPanel.cs:67 |
| Panels.Image:New() | ImagePanel | Images.Default | Images.cs:18, ImagePanel.cs:67 |
| Panels.Buttons.Standard:New() | StandardButtonPanel | Buttons.Standard | Buttons.cs:78, StandardButtonPanel.cs:71 |
| Panels.InputField:New() | LuaInputFieldPanel | InputFields.LuaShort (per inv-a.md:49, not re-checked) | LuaInputFieldPanel.cs:75 |
| LayoutPanels.Scrollable:New() | LuaScrollableLayoutPanel | Layouts.LuaScrollable | Layouts.cs:39 |
| Panels.Dropdown:New() | LuaDropdownPanel | Buttons.LuaDropdown (per inv-a.md:48) | LuaDropdownPanel.cs:61 |

- Widget handle groups used: Widgets.LayoutElement (Min/Preferred/Flexible W/H, AnchorMin/Max,
  AnchoredPosition/Width/Height, OffsetMin/Max, IgnoreLayout, Pivot, Visible),
  Widgets.LabelLayoutElement (same set for labels), Widgets.LayoutGroup (Spacing, Padding, Alignment),
  Widgets.Image (Enabled, Sprite, Color, Fill, Raycasts), Widgets.Label (Enabled, Text, Color,
  Font, FontSize, Alignment, LineSpacing, Raycasts), Widgets.Button (Interactable, OnAction,
  OnPointerEnter/Exit/Down/Up), Widgets.FrameAnimator (Highlight).
- Enums: Alignment = LUI2.Enums.Alignment (LUI2.lua:27), Font = LUI2.Enums.Font (LUI2.lua:47),
  HighlightMode = LUI2.Enums.FrameHighlight (LUI2.lua:48). Font enum C#: LabelWidgetComponent.cs:170
  (KorolevLight case :91-92); font fields: LayoutPanelManager.cs:16 (KorolevLight),
  :46 (ProximaNovaRegular).

## 1. ButtonPanel.lua (474 lines) - the base button (used by LeftBar/RightBar/TaskList/Popup/Tooltip/IBIF2)

Root _p = LayoutPanels.Horizontal "_p" (L28). Root LayoutElement (L221-231): Visible (default true),
MinWidth/PreferredWidth = Width (default 48), MinHeight/PreferredHeight = Height (default 48),
FlexibleWidth -1, FlexibleHeight -1. LayoutGroup Alignment MiddleLeft (L228). Image enabled,
Color = RealImage_Color (L67): White when pressing AND hovering, else Image_Color
(default Color(0,0,0,127/255) = 0.498 alpha black, L66). Raycasts false.

Children, in order (skipMap key in brackets; skipping removes the child):

| # | name | Lua L | type | key properties and defaults |
|---|---|---|---|---|
| 1 | bgImg [bgImg] | 240-255 | Panels.Image | BackImage_Visible false; 32x32 (BackImage_Width/Height); anchors 0.5/0.5; pos (0,0) [LargeButton: BackImage_OffsetX/Y]; Sprite nil; Color White; Raycasts false |
| 2 | lyt_hover [hoverLyt] | 263-276 | Horizontal | IgnoreLayout; anchors 0..1; offsets 0; Padding (0,4,4,0) (L270); Image disabled; Visible true (GetHover gate commented out L264) |
| 3 | img_hoverBg [hoverBgImg] | 283-297 | Image | stretch 0..1; Enabled true; Sprite nil; Color = HoverBgImage_DynamicColor (L78): _fgColorHover when hovering else (0,0,0,0) |
| 4 | img_hover [hoverImg] | 305-320 | Image | centred 32x32; Enabled false; Sprite nil; Color White |
| 5 | lbl_hoverHotkey [hoverHotkeyLbl] | 328-346 | Label | stretch, offsets (2,2)/(-2,-2); Enabled false; Text default "lol" (L93, placeholder, caller must set); Color CastleYellow; FontSize 10; Alignment UpperRight |
| 6 | lbl_hover [hoverLbl] | 351-370 | Label | stretch 0..1, offsets 0; Enabled true; Text ""; Color = HoverLabel_DynamicColor (CastleYellow when hovering, else (0,0,0,0)); FontSize 11; Alignment LowerCenter |
| 7 | img_fg [fgImg] | 376-393 | Image | centred 32x32 (FrontImage_Width/Height); Visible true; Sprite nil; Color White; Raycasts false |
| 8 | lbl_fg [fgLbl] | 399-417 | Label | stretch 0..1; Visible = not hovering (L104); Text ""; Color CastleYellow (L107); Font ProximaNovaRegular (L108); FontSize 11 (L109); Alignment LowerCenter (L110); offsets 0 |
| 9 | btn [btn] | 423-443 | Buttons.Standard | stretch 0..1; Interactable FORCED false (L431, cfg line commented); OnAction, OnPointerEnter/Exit/Down/Up from cfg; Highlight = Button_Highlight (default HighlightMode.None) |

Behaviour (pre/post hooks, L137-147):
- Pointer enter: hovering=true, ev_onSetHover, rollover sound (Meta.Sound[SoundContext].rolloveron, context "main"), RefreshHover (L144).
- Pointer exit: hovering=false, rolloveroff sound, RefreshHover (L145).
- Pointer down/up: pressing flag + ev_onSetPress, RefreshAll (L146-147).
- OnAction: plays Meta.Sound[SoundContext][Button_OnActionSound] (default "select", L126; ImagedButtonInputFieldPanel2 sets "itemselect").
- Hover look: bg (hoverBgImg) and hover label appear only while hovering; front label hides while hovering.

Other defaults: Visible true; Width/Height 48; Image_Enabled true; Button_OnAction nil; SoundContext "main" (L132).

## 2. LargeButtonPanel.lua (481 lines) - same skeleton as ButtonPanel, larger label/icon placement

Same root, children and names as ButtonPanel (section 1, rows 1-9). Differences only:
- FrontLabel_Visible default true (L106) (ButtonPanel: not hovering).
- FrontLabel_AnchorMin/AnchorMax default zero / one (L113-114); FrontLabel_OffsetX/OffsetY (L115-116, default 0) feed AnchoredPosition (L410); OffsetMin/Max zero.
- BackImage_OffsetX/OffsetY (L72-73) feed AnchoredPosition of bgImg (L251).
- Root Alignment MiddleLeft (L232). Font ProximaNovaRegular, FontSize 11, LowerCenter (unchanged).
- Caller: SidebarFloatingMenu.lua:214 (skipMap: hoverLyt = hasNoHover, hoverHotkeyLbl = not hasHotkey, hoverLbl = not hasFgLabel, fgLbl = not hasFixedLabel, btn false).

## 3. AlertPanel.lua (465 lines) - button variant used by the floating sidebar; no hover/press logic

Same skeleton as ButtonPanel (rows 1-9) with these changes:
- Root Alignment MiddleLeft (L228). Image_Color/RealImage_Color as ButtonPanel.
- bgImg: Visible default false (L69); AnchoredPosition HARDCODED (100,0) (L246); Enabled forced true (L247); Sprite/Color from cfg (BackImage_Color default White).
- img_fg: anchors HARDCODED (0,0)..(1,1), AnchoredPosition HARDCODED (0.8,0.5) (L375-377), Sprite/Color from cfg.
- lbl_fg: FrontLabel_AnchorMin default (0.05,0.1) and AnchorMax (0.85,0.9) (L111-112); FrontLabel_FontSize 10 (L109); FrontLabel_Alignment MiddleCenter (L110); FrontLabel_Visible = not hovering (L104); LineSpacing 1.2 (L400); Font ProximaNovaRegular; Color CastleYellow.
- Hover children and hotkey label: same defaults as ButtonPanel (rows 2-6).
- Pre/post hooks are COMMENTED OUT (L137-147): _p.hovering and _p.pressing are never set,
  so GetHover() is always false (no hover bg, no hover label, front label always visible), no sounds, no RefreshHover.
- btn: Interactable, OnAction, OnPointer* handles COMMENTED OUT (L423-428). Only Visible, IgnoreLayout,
  stretch and Highlight (L429) are set. Click routing is done by the caller: SidebarFloatingMenu.lua:340 calls _m.RegisterButton(p) (BaseMenu side not read).
- Caller: SidebarFloatingMenu.lua:318-345. skipMap: bgImg false, hoverLyt = hasNoHover, hoverBgImg true,
  hoverImg true, hoverHotkeyLbl = not hasHotkey, hoverLbl true, fgImg true, fgLbl = not hasFgLabel, btn false.
  Sets: Visible, Image_Enabled true, Image_Color = BgColor (_bgColorNone), BackImage_Visible true, BackImage_Sprite = Icon, BackImage size = FrontImage_Width/Height, Width/Height (forced), BackImage_Color = IconColor, FrontLabel_Text = Count, FrontLabel_Color = CountColor, FrontLabel_Alignment = Label_Alignment (default LowerCenter, SidebarFloatingMenu.lua:85), Button_Highlight.

## 4. HeaderPanel.lua (173 lines) - title bar with close buttons, divider

Root _p = LayoutPanels.Vertical (L8): LayoutElement MinWidth -1, MinHeight 72, PreferredWidth -1,
PreferredHeight -1, FlexibleWidth 1, FlexibleHeight -1; LayoutGroup Spacing 0, Padding zero,
Alignment MiddleCenter; Image disabled.

| # | name | Lua L | content |
|---|---|---|---|
| 1 | lyt | 29-43 | Horizontal: MinH 64, PrefH 64, FlexW 1, Spacing 0, Padding zero, MiddleCenter. Holds leftBtn, labelLyt, rightBtn (in order) |
| 2 | leftBtn | 50-65 | ButtonPanel: Width 64, Height 64, Image_Enabled false, FrontImage_Sprite IconKeys.UI_X:Get64(), FrontImage_Color CastleYellow, HoverLabel_Text GetLocalized("##gamemenu_taskbar_close"), Button_OnAction print("Close") (placeholder, L57), HoverBgImage_Color (0,0,0,0.75) |
| 3 | labelLyt | 71-85 | Horizontal: MinH 64, PrefH 64, FlexW 1, Spacing 0, Padding (32,32,4,0) (L79), MiddleCenter |
| 4 | lbl | 91-106 | Label: Color (1,1,1,1), Text "sdfsd" (placeholder, L93), Font KorolevLight, FontSize 22, Alignment MiddleCenter; LabelLayoutElement IgnoreLayout, AnchorMin 0, AnchorMax 1, Pivot 0.5, offsets 0 |
| 5 | rightBtn | 125-141 | identical to leftBtn (L127-137) |
| 6 | spacer | 146-153 | SpacerPanel: Width -1, Height 8, FlexW 1, FlexH -1 (added to root _p, not to lyt) |
| 7 | divider | 158-167 | DividerPanel: Width -1, Height 1, FlexW 1 (root _p) |

- Old spacer (commented out, L108-121) is not active.
- Caller colours the title: inv-c.md:70 (CastleYellow set by the caller, not in this file).
- Users: Menus/Game/Menu_Help.lua:115, Menus/SidebarScoreMenu.lua:17, Menus/MediaMenu.lua:33 (not in GameMenu.lua).
- Trap for the port: the close buttons have a placeholder print("Close"); a C# port must wire
  the close action itself (no close handler is set in this file).

## 5. SpacerPanel.lua (66 lines) - empty row or mute-button row (NOT a divider)

Root image: Panels.Buttons.Standard if _data.hasMuteButton, else Panels.Image (L23).
LayoutElement: Visible = _p.Visible(), MinWidth/PreferredWidth = _p.Width(), MinHeight/PreferredHeight = _p.Height(),
FlexibleWidth/FlexibleHeight = _p.FlexibleWidth()/_p.FlexibleHeight() (L25-34). Image Enabled = _p.Enabled() (L35),
Sprite nil, Raycasts false.
Defaults (L53-60): Visible true, Width -1, Height -1, PreferredWidth/Height -1, FlexibleWidth -1,
FlexibleHeight -1, Enabled false.
Mute button branch (L39-43): OnPointerEnter UIGame.SetAllKeyBindingCategoryTo(false), OnPointerExit UIGame.ResetAllKeyBindingCategory().
Callers set p.Width = ||-1, p.Height = ||8 or ||64 (HeaderPanel L148-149, LeftBarMenu.lua:235/277, RightBarMenu.lua:331, Menu_Popup.lua:524, Menu_Tooltip.lua:490, Menu_TaskList.lua:797).

## 6. DividerPanel.lua (69 lines) - horizontal rule with side inset

Root LayoutPanels.Vertical (L16): Visible = _p.Visible(), Min/Preferred W = _p.Width(), Min/Preferred H = _p.Height(),
Flexible W/H from _p.FlexibleWidth()/FlexibleHeight(); Image disabled.
Child img (L31-44): Panels.Image; IgnoreLayout true; Pivot (0.5,0.5); AnchorMin (0,0.5), AnchorMax (1,0.5);
OffsetMin = _p.OffsetMin(), OffsetMax = _p.OffsetMax(); AnchoredHeight 1; Sprite nil; Color (0.5,0.5,0.5,0.5).
Defaults (L52-58): Visible true, Width -1, Height 9, FlexibleWidth 1, FlexibleHeight -1,
OffsetMin (-16,0), OffsetMax (16,0). RefreshAll refreshes root and img (L59-63).
Callers: LeftBarMenu.lua:316 (caller sets Width/Height; that call site not read), HeaderPanel L158-167.

## 7. LabeledDividerPanel.lua (145 lines) - divider with centred text (used by WorldEditor menus; not by GameMenu)

Root LayoutPanels.Horizontal (L8). Setter API (not _cfg): SetVisible(true), SetWidth(-1), SetHeight(32),
SetFlexibleWidth(1), SetFlexibleHeight(-1), SetText(""), SetTextColor(0.8,0.8,0.8,1), SetPadding(-4,-4,0,0),
SetSpacing(0). Root: LayoutElement from these (L40-47), LayoutGroup Padding and Spacing from the setters,
Alignment MiddleCenter, Image disabled.
Children: leftImage (Panels.Image: Sprite nil, Color (0.5,0.5,0.5,0.5), MinW/PrefW = Width, MinH/PrefH 1, flex from setters);
label (Panels.Label: Pivot 0.5; LabelLayoutElement MinW -1, MinH = Height (32), PrefW -1, PrefH = Height, FlexW -1, FlexH -1;
FontSize 14; Color = TextColor (0.8,0.8,0.8,1); Text; Alignment MiddleLeft; no Font set = Texts.Default font);
rightImage (same as leftImage); mute button only if _data.hasMuteButton (Panels.Buttons.Standard, stretch, same enter/exit as SpacerPanel).

## 8. ImagedButtonInputFieldPanel2.lua (250 lines) - input field + save icon button (blueprint name row)

Caller: SidebarFloatingMenu.lua:440 (dofile). Root LayoutPanels.Horizontal (L27), named _panel.
Defaults (L43-58): Visible true, Width -1, Height 32, FlexibleWidth 1, FlexibleHeight -1, IconColor CastleYellow,
InputField_Width -1, InputField_Height 48, InputField_FlexibleWidth 1, InputField_FlexibleHeight -1,
InputField_Text "x" (required), InputField_SetText no-op, InputField_LineType SingleLine, Button_OnAction no-op.
Setters Set_<key>(v) are generated for every cfg key (AddSetters, L60-70).
Root LayoutElement (L176-183): Visible, IgnoreLayout false, Min/Preferred W = Width, H = Height, flex from cfg.
LayoutGroup: Spacing 2, Padding (0,0,0,0), Alignment UpperLeft (L184-186). Image disabled.

| # | child | Lua L | content |
|---|---|---|---|
| 1 | inputfield | 191-214 | dofile ImprovedInputFieldPanel.lua (section 9): Height/Width 48, text bound to _Value(), SetText commits unless hovering the button, OnDeselect returns input to game (InputModeController.ChangeInputToGame()), Editing/Selecting flags, Button_Visible = not editing, Button_OnActionSound "itemselect", SoundContext "worldeditor" (L206), button action enters text mode (InputModeController.ChangeInputToText(), L210) |
| 2 | imagedbutton | 217-243 | loadfile ButtonPanel with skipMap: bgImg false, hoverLyt false, hoverBgImg false, hoverImg true, hoverHotkeyLbl true, hoverLbl false, fgImg true, fgLbl true, btn false (L219-227). BackImage_Visible true, BackImage_Sprite IconKeys.SaveBlueprints:Get64(), BackImage_Color = IconColor (CastleYellow), BackImage 32x32, Width = Height = InputField_Height (48), Button_OnAction = cfg |

The button is 48x48 (InputField_Height) with a 32x32 icon; the root has Spacing 2 between the two children.

## 9. ImprovedInputFieldPanel.lua (205 lines) - used by section 8 (no direct GameMenu caller)

Root LayoutPanels.Horizontal (L27). Defaults (L43-70): Visible true, Width -1, Height 48, FlexibleWidth 1,
FlexibleHeight -1, Color (0.5,0.5,0.5,1); InputField_Width -1, InputField_Height 48, flex 1/-1,
InputField_Text "0"; PhLabel_Text = GetLocalized("##mainmenu_none") (L68), PhLabel_Color = _white75 (1,1,1,0.75) (L69);
Label_Color = _white (1,1,1,1) (L72); Label_FontSize 12 (L73); Label_Raycasts true (L74).
Root LayoutGroup Padding (1,1,1,1) (L145), Alignment MiddleCenter (L144).
Children: ContouredImagedButtonPanel (dofile L153, not read) with Button_* and Image_Visible false;
inputfield = Panels.InputField (L171): Min/Preferred W/H = InputField_Width/Height, flex from cfg; TextValue, SetTextValue,
DoSelect, OnSelect, OnDeselect, Interactable, ControlColor (selection), LineType; PlaceholderLabel Alignment MiddleLeft, Text, Color, Raycasts;
Label Alignment MiddleLeft, Color, FontSize (12), Raycasts; FrameAnimator Highlight; Image Enabled = InputField_Image_Enabled.

## 10. Panel_ProgressHorizontal.lua (293 lines) - idle-energy progress bar (loaded by GameMenu.lua:2784 via h.Path, not dofile)

GameMenu data (GameMenu.lua:2784-2790): Visible true, Progress = Data.FireflyNest:GetProgress(),
ProgressText = GetLocalized("##task_idle_energy"), ProgressCount = UIGame:CountBricktrons() vs Rules.bricktronCap (GameMenu.lua:2790 onward).
Root LayoutPanels.Horizontal (L17). Defaults (L48-68): Width 160, Height 48, FlexibleWidth -1, FlexibleHeight -1,
Padding (0,8,0,0) (L52, but the root LayoutGroup at L161 hardcodes Padding (8,8,0,0), so the default is unused),
AnchoredPosition (0,2) (L54, not applied by the root), Progress, ProgressText, ProgressCount; Button_* defaults as ButtonPanel.
Root LayoutElement: MinW/PrefW = Width, MinH/PrefH = Height (160 x 48); Alignment MiddleCenter (L162).

| # | child | Lua L | content |
|---|---|---|---|
| 1 | progressLyt | 171-183 | Horizontal: MinHeight 8, FlexibleWidth 1, PreferredHeight -1 |
| 2 | bgImg | 187-200 | Image: AnchorMin (0,0.5), AnchorMax (1,0.5), AnchoredHeight 4, Color (0.25,0.25,0.25,1), Sprite nil |
| 3 | fgImg | 206-220 | Image: same anchors and height 4; Fill = Progress; Color = Data.Faction:GetAllied().factionColor (L215) |
| 4 | progress text | 226-242 | Label: OffsetMin (8,4), OffsetMax (0,0), Alignment LowerLeft, Text = ProgressText, Color CastleYellow (L237). Font size not set (default) |
| 5 | count text | 248-264 | Label: OffsetMin (0,4), OffsetMax (-8,0), Alignment LowerRight, Text = ProgressCount, Color CastleYellow (L259). Font size not set |
| 6 | btn | 270+ | Buttons.Standard (click area), same as ButtonPanel |

Refresh: ev_refresh and ev_refreshProgress listeners (around L200-225) call p.Refresh().

## 11. Callers (Lua) and where each panel is used

| panel | used by |
|---|---|
| ButtonPanel | Menus/Game/LeftBarMenu.lua:103,178; RightBarMenu.lua:289,310,348; Menu_Popup.lua:387; Menu_TaskList.lua:876; Menu_Tooltip.lua:360; ImagedButtonInputFieldPanel2 (section 8) |
| LargeButtonPanel | Menus/Game/SidebarFloatingMenu.lua:214 |
| AlertPanel | Menus/Game/SidebarFloatingMenu.lua:318 |
| SpacerPanel | HeaderPanel L146; LeftBarMenu.lua:235,277; RightBarMenu.lua:331; Menu_Popup.lua:524; Menu_Tooltip.lua:490; Menu_TaskList.lua:797 |
| DividerPanel | HeaderPanel L158; LeftBarMenu.lua:316 |
| HeaderPanel | Menus/Game/Menu_Help.lua:115 (plus non-Game menus) |
| LabeledDividerPanel | WorldEditor menus only (WeGridMenu, WeMapMenu, WeSaveMenu, ...) |
| ImagedButtonInputFieldPanel2 | Menus/Game/SidebarFloatingMenu.lua:440 |
| Panel_ProgressHorizontal | GameMenu.lua:2784 (h.Path) |
| ImprovedInputFieldPanel | via ImagedButtonInputFieldPanel2 L192 (WorldEditor uses ImagedButtonInputFieldPanel.lua) |

## 12. Colour and font constants (value, definition)

| name | value | defined at |
|---|---|---|
| CastleYellow | C# LUI2.Colors.CastleYellow (value not in ref/ Lua text); hex "ffc000ff" = RGB(255,192,0) | LUI2.lua:82, hex LUI2.lua:97 |
| _colorYellow | = CastleYellow | LUI2.lua:111 |
| CastleYellowLight | (255,212,75,1) | LUI2.lua:93 |
| CastleYellowLighter | (255,234,165,1) | LUI2.lua:94 |
| CastleBlue | (0, 160/255, 1, 1) | LUI2.lua:81 |
| CastleRed | (1, 0, 100/255, 1) | LUI2.lua:83 |
| White, Gray, DarkGray, Yellow | LUI2.Colors.* (C# side) | LUI2.lua:84-87 |
| _bgColorNone | (0,0,0,0) | LUI2.lua:101 |
| _bgColorDefault | (0.12,0.12,0.12,0.85) | LUI2.lua:102 |
| _bgColorDark | (0.07,0.07,0.07,0.98) | LUI2.lua:103 |
| _bgColorSelected | (0.10,0.10,0.10,1) (0.985 alpha version commented out) | LUI2.lua:105 |
| _bgColorAnySelected | (0.12,0.12,0.12,0.85) | LUI2.lua:106 |
| _bgColorHover | (0.12,0.12,0.12,0.85) | LUI2.lua:107 |
| _fgColorHover | (1/3,1/3,1/3,0.5) | LUI2.lua:108 |
| _colorBlack / _colorRed / _colorGreen / _colorGray / _colorOrange | (0,0,0,1) / (1,0,0,1) / (0,1,0,1) / Gray / (1,0.5,0,1) | LUI2.lua:110-115 |
| per-file locals | _black25, _black75, _black, _white, _white25 (1,1,1,0.25), _white75 (1,1,1,0.75) | top of each panel file |
| HalfWhite, HalfBlack, SlightBlack | (1,1,1,2/3), (0,0,0,1/2), (0,0,0,1/10) | LUI2.lua:89-91 |
| CountFontSize | menu handle field: GameMenu.lua:2704 = 12; default 10 in Menu_Popup.lua:331 (feeds FrontLabel_FontSize, Menu_Popup.lua:397) | - |
| LabelFontSize | default 12 (LeftBarMenu.lua:154), feeds FrontLabel_FontSize (L185) | - |
| HotkeyLabel_FontSize | default 12 (LeftBarMenu.lua:151) / 10 (Menu_Popup.lua:336) | - |
| Font.ProximaNovaRegular | LayoutPanelManager.ProximaNovaRegular | LayoutPanelManager.cs:46 |
| Font.KorolevLight | LayoutPanelManager.KorolevLight | LayoutPanelManager.cs:16 |

Sounds: Meta.Sound[context] table; context "main" names used: select, rolloveron, rolloveroff; context "worldeditor" and
"itemselect" in section 8. The Meta table is defined on the C# side (not read).

## 13. Port checklist (facts that differ from what a reader expects)

- ButtonPanel btn: Interactable is hard-forced false (L431); AlertPanel btn has no handlers at all (L423-428).
  Verify how the Brix StandardButtonPanelComponent delivers clicks before porting, or the click path is lost.
- AlertPanel hover/press never fire (hooks commented out L137-147); its hover visuals are dead in the Lua version too.
- Placeholders left in the Lua that callers must overwrite: "lol" hotkey text (ButtonPanel L93), "sdfsd" header label
  (HeaderPanel L93), print("Close") (HeaderPanel L57, L133).
- Hard-coded geometry: AlertPanel bgImg (100,0) and img_fg anchored (0.8,0.5); HeaderPanel labelLyt padding (32,32,4,0);
  Panel_ProgressHorizontal root padding (8,8,0,0) overrides its cfg default.
- Label colour default is white in ImprovedInputFieldPanel but CastleYellow in every ButtonPanel-family label.
- Not read: ContouredImagedButtonPanel.lua, TaskbarProjectCrewPanel.lua, LabeledDividerPanel2.lua, Panel_ModalButton*.lua.
