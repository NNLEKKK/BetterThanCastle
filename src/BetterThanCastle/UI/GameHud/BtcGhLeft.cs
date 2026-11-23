using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.External.Factories;
using Brix.External.Signals;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Input;
using Brix.Lua;
using Brix.UI;
using Brix.UI.Builder.Menu;
using Brix.UI.GameSelector;
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle
{
    /// GameMenu.lua left bar (mg.left, exclusive) + left content sockets (LeftContentMenu.lua) with the SidebarFloatingMenu sidebars
    /// for blueprints, new task, structures and blocks. Event driven (Project selected/started/deleted, picker options, operable
    /// selected); no per-frame work. Sidebars are built the first time they open (Lua builds all four at load).
    internal static class BtcGhLeft
    {
        internal enum Side { None, Score, Blueprints, NewTask, Structures, Blocks }

        private static Side _open = Side.None;
        private static BtcGhButton _score, _bp, _newTask, _structures, _blocks;
        private static Image _spacerFlex2;
        private static Text _taskCount;
        private static RectTransform _content;
        private static readonly BtcGhSidebar[] Bars = new BtcGhSidebar[6];
        private static readonly List<SignalConnection> Conns = new List<SignalConnection>();
        private static bool _wasBuild, _wasIdle = true;
        private static Project _sel;
        private static CanvasGroup _scoreboard;

        private static readonly Factory.AssetKey StoneKey = new Factory.AssetKey("Blueprints", "Stone_DoubleBrick");
        private static readonly Factory.AssetKey StockpileKey = new Factory.AssetKey("Blueprints", "Pallet");

        // ------------------------------------------------------------------ selection (Data.Project)
        internal static Project Selected { get { return _sel; } }
        internal static bool IsIdle { get { return _sel == null || _sel is IdleProject; } }
        internal static bool IsBuild { get { return _sel is BuildProject; } }
        internal static bool Paused { get { return ClockConfig.Paused; } }

        private static Project FindSelected()
        {
            if (UIGameObserver.projects != null)
                foreach (Project p in UIGameObserver.projects.all) if (p != null && p.selection) return p;
            return IdleProject.GetAlliedProject();
        }

        internal static bool UnderCap() { return Project.ProjectUnderCap(); }

        // ------------------------------------------------------------------ build
        internal static void Build(RectTransform root)
        {
            _open = Side.None;
            for (int i = 0; i < Bars.Length; i++) Bars[i] = null;
            _sel = FindSelected();
            _wasBuild = IsBuild; _wasIdle = IsIdle;

            // left bar: Vertical a=0,0-0,1 p=0,0.5, 48 wide, ignores the parent layout
            RectTransform rail = BtcGhKit.Make(Layouts.Vertical, root, "");
            BtcGhKit.Rect(rail, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(48f, 0f));
            BtcGhKit.Img(rail, null, new Color32(0, 0, 0, 192), false);
            BtcGhKit.Le(rail, -1f, -1f, -1f, -1f, -1f, -1f, true);
            BtcGhKit.Group(rail, 0, 0, 0, 0, 0f, TextAnchor.UpperLeft);
            BtcHudSlots.Register("gh.left", rail);

            _score = Toggle(rail, Side.Score, "UI_Score", "##gamemenu_score", BtcGhKit.Yellow, null, delegate { ToggleSide(Side.Score); });
            BtcGhKit.Spacer(rail, -1f, 1f, BtcGhKit.RailBg, true);
            _bp = Toggle(rail, Side.Blueprints, "UI_SelectBlueprints", "##gamemenu_taskbar_blueprints", BtcGhKit.Red, null, OnBlueprints);
            _bp.IconColor = delegate { return IsBuild ? BtcGhKit.Red : BtcGhKit.Gray; };
            _newTask = Toggle(rail, Side.NewTask, "_UI_Pick", "##gamemenu_taskbar_newtask", BtcGhKit.Red, "command_CreateProject", OnNewTask);
            _structures = Toggle(rail, Side.Structures, "UI_Stockpile", "##gamemenu_taskbar_structures", BtcGhKit.Red, "command_StructuresMenu", OnStructures);
            _blocks = Toggle(rail, Side.Blocks, "UI_Brick", "##gamemenu_taskbar_blocks", BtcGhKit.Red, "command_BlocksMenu", OnBlocks);
            BtcGhKit.Spacer(rail, 1f, -1f, BtcGhKit.RailBg, true);
            _spacerFlex2 = BtcGhKit.Spacer(rail, -1f, 1f, BtcGhKit.RailBg, true);
            BtcGhKit.DisplayButton(rail, BtcGhKit.Icon("_UI_Pick"), BtcGameHud.Loc("##gamemenu_taskbar_tasknumber"), BtcGhKit.Red, out _taskCount);

            // left content: Horizontal a=0,0-0,1 p=0,0.5 offsetMin (48,154) offsetMax (48,0) width 272
            _content = BtcGhKit.Make(Layouts.Horizontal, root, "");
            BtcGhKit.Rect(_content, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(48f, 77f), new Vector2(272f, -154f));
            BtcGhKit.Img(_content, null, new Color32(0, 0, 0, 192), false);
            BtcGhKit.Le(_content, -1f, -1f, -1f, -1f, -1f, -1f, true);
            BtcGhKit.Group(_content, 0, 0, 0, 0, 0f, TextAnchor.UpperLeft);
            BtcHudSlots.Register("gh.leftContent", _content);

            BtcGhModes.BlocksMenu = OnBlocks;
            BtcGhModes.StructuresMenu = OnStructures;
            BtcGhModes.NewTask = OnNewTask;
            BtcGhNewTask.BindKeys();

            Connect();
            RefreshRail();
        }

        private static BtcGhButton Toggle(RectTransform rail, Side side, string icon, string loc, Color color, string hotkeyAction, Action act)
        {
            BtcGhButton b;
            BtcGhKit.RailButton(rail, BtcGhKit.Icon(icon), color, BtcGameHud.Loc(loc), "", color, out b);
            b.OnAction = act;
            if (hotkeyAction != null) b.HotkeyFn = delegate { return BtcGameHud.Hotkey("Camera", hotkeyAction); };
            b.BgColor = delegate { return _open == side ? BtcGhKit.Selected : BtcGhKit.RailBg; };
            BtcGameHud.Track(b);
            return b;
        }

        internal static void Close()
        {
            for (int i = 0; i < Conns.Count; i++) { try { Conns[i].Disconnect(); } catch { } }
            Conns.Clear();
            for (int i = 0; i < Bars.Length; i++) Bars[i] = null;
            _open = Side.None;
            _content = null; _scoreboard = null; _sel = null;
            BtcGhBlueprints.Close();
        }

        private static void Connect()
        {
            Conns.Add(Project.lua_Selected._signal.Connect(new Action<Project>(OnProjectSelected)));
            Conns.Add(GameSignals.Connect<Project>(Project.ProjectStarted, new Action<Project>(OnProjectCount), null));
            Conns.Add(GameSignals.Connect<Project>(Project.ProjectDeleted, new Action<Project>(OnProjectCount), null));
            Conns.Add(GameSignals.Connect(PickingObserver.lua_OverridePickerSetWithOptions, new Action(OnPickerOptions), null));
            Conns.Add(CraftingStation.lua_OnSetSelected._signal.Connect(new Action(OnOperableSelected)));
        }

        // ------------------------------------------------------------------ events
        /// Data.Project:OnSetSelected -> _m._OnSetSelectedProject + its listeners.
        private static void OnProjectSelected(Project p)
        {
            try
            {
                _sel = FindSelected();
                bool build = IsBuild, idle = IsIdle;
                if (_wasBuild && !build) OnDeselectBuildProject();
                _wasBuild = build;
                // OnToggleSidebarSpacerHack
                LuaHookBase.InvokeFromCLR<bool>(UIGameObserver.lua_SetSidebarSpacer, !idle);
                BtcGhButton.Play(!idle ? "Edit_Mode_In" : "Edit_Mode_Out");
                _wasIdle = idle;
                if (!idle && _open == Side.NewTask) SetOpen(Side.None);
                BtcGameHud.RefreshAll();
            }
            catch (Exception e) { BtcLog.Error("GameHud", "selected: " + e); }
        }

        private static void OnProjectCount(Project p)
        {
            try
            {
                RefreshCount();
                BtcGhSidebar nt = Bars[(int)Side.NewTask];
                if (nt != null && _open == Side.NewTask) nt.Refresh();
            }
            catch (Exception e) { BtcLog.Error("GameHud", "count: " + e); }
        }

        /// Picking.hk_OnSetPickerOptions: Data.Project:OnSetHeldBlueprint (highlights) + OnReopenBuild.
        private static void OnPickerOptions()
        {
            try
            {
                BtcGhSidebar s = _open != Side.None ? Bars[(int)_open] : null;
                if (s != null) s.RefreshHighlights();
                if (!IsBuild) return;
                BtcGhModes.ActivateBlueprints();
                Factory.AssetKey held = Picking.BlueprintBeingPlaced();
                if (BtcGhBuild.IsBlock(held)) SetOpen(Side.Blocks);
                else if (BtcGhBuild.IsStructure(held)) SetOpen(Side.Structures);
            }
            catch (Exception e) { BtcLog.Error("GameHud", "picker: " + e); }
        }

        private static void OnOperableSelected() { if (_open == Side.NewTask) SetOpen(Side.None); }

        /// OnDeselectBuildProject
        private static void OnDeselectBuildProject()
        {
            BtcGhModes.DeactivateBlueprints();
            BtcGhModes.DeactivateBricks();
            BtcGhModes.DeactivateStructures();
            BtcGhModes.ActivateProjects();
            SetOpen(Side.None);
        }

        // ------------------------------------------------------------------ actions (mh.*.OnAction)
        internal static void OnBlueprints()
        {
            if (Paused || !IsBuild) return;
            SetOpen(Side.Blueprints);
            Picking.lua_SetIsSelectingBlueprints(default(MoonSharp.Interpreter.ScriptExecutionContext), BtcGhLua.Args(true));
        }

        private static void OnBlocks()
        {
            if (Paused) return;
            InputContextComponent.defaultHeldObject = Blueprints.Stone_DoubleBrick;
            if (IsBuild) { Picking.SetBlueprintInPicker(StoneKey); SetOpen(Side.Blocks); }
            else if (_sel != null) { SelectIdle(); InputModeController.bricksPanelButtonPressed = true; }
        }

        private static void OnStructures()
        {
            if (Paused) return;
            InputContextComponent.defaultHeldObject = Blueprints.Pallet;
            if (IsBuild) { Picking.SetBlueprintInPicker(StockpileKey); SetOpen(Side.Structures); }
            else if (_sel != null) { SelectIdle(); InputModeController.structuresPanelButtonPressed = true; }
        }

        private static void OnNewTask()
        {
            if (Paused) return;
            BtcGhModes.DeactivateProjects();
            BtcGhModes.ActivateTasks();
            if (!IsIdle) { UIGameActor.SelectIdleProject(); SetOpen(Side.NewTask); }
            else ToggleSide(Side.NewTask);
        }

        /// Data.Project:SetSelection(GetIdle(), true) -> Project.Select -> UIGameSelector.Select(GameObject).
        private static void SelectIdle()
        {
            IdleProject idle = IdleProject.GetAlliedProject();
            if (idle != null) UIGameSelector.Select(idle.gameObject);
        }

        // ------------------------------------------------------------------ open / close (MenuGroup "leftFloating", exclusive)
        private static void ToggleSide(Side s) { SetOpen(_open == s ? Side.None : s); }

        internal static void SetOpen(Side s)
        {
            if (_open == s) return;
            Side old = _open;
            _open = s;
            if (old != Side.None)
            {
                if (Bars[(int)old] != null) Bars[(int)old].SetActive(false);
                if (old == Side.NewTask) { BtcGhModes.DeactivateTasks(); BtcGhModes.ActivateProjects(); }
            }
            if (s != Side.None)
            {
                BtcGhSidebar bar = Bar(s);
                if (bar != null) { bar.SetActive(true); if (s == Side.Blueprints) BtcGhBlueprints.OnOpen(); bar.Refresh(); }
                if (s == Side.Blocks) { BtcGhModes.DeactivateProjects(); BtcGhModes.DeactivateStructures(); BtcGhModes.ActivateBricks(); }
                else if (s == Side.Structures) { BtcGhModes.DeactivateProjects(); BtcGhModes.DeactivateBricks(); BtcGhModes.ActivateStructures(); }
                else if (s == Side.NewTask) UIGameSelector.UnselectAll(SelectionType.Crafting, null); // Data.Operable:DeselectAll
            }
            else LuaMenuUtility.ResetAllKeyBindings();
            SetScoreboardVisible(s == Side.None);
            RefreshRail();
        }

        private static BtcGhSidebar Bar(Side s)
        {
            if (s == Side.Score || _content == null) return null;
            BtcGhSidebar b = Bars[(int)s];
            if (b != null) return b;
            float t = Time.realtimeSinceStartup;
            switch (s)
            {
                case Side.Blocks: b = new BtcGhSidebar(_content, BtcGameHud.Loc("##gamemenu_taskbar_blocks"), delegate { SetOpen(Side.None); }); BtcGhBuild.FillBlocks(b); break;
                case Side.Structures: b = new BtcGhSidebar(_content, BtcGameHud.Loc("##gamemenu_taskbar_structures"), delegate { SetOpen(Side.None); }); BtcGhBuild.FillStructures(b); break;
                case Side.NewTask: b = new BtcGhSidebar(_content, BtcGameHud.Loc("##gamemenu_taskbar_newtask"), delegate { SetOpen(Side.None); }); BtcGhNewTask.Fill(b); break;
                case Side.Blueprints: b = new BtcGhSidebar(_content, BtcGameHud.Loc("##gamemenu_taskbar_blueprints"), delegate { SetOpen(Side.None); }); BtcGhBlueprints.Fill(b); break;
            }
            Bars[(int)s] = b;
            BtcLog.Info("GameHud", "sidebar " + s + " built in " + ((Time.realtimeSinceStartup - t) * 1000f).ToString("0") + " ms (" + (b != null ? b.Buttons.Count : 0) + " buttons)");
            return b;
        }

        /// _m.mg.left.onAnyOpen: GS_Scoreboard:SetVisible(not IsAnyOpened()) = LuaMenuComponent.SetVisible (CanvasGroup alpha + raycasts).
        private static void SetScoreboardVisible(bool v)
        {
            if (_scoreboard == null)
            {
                GameObject go = GameObject.Find("Menu: Scoreboard (Lua)");
                if (go != null) _scoreboard = go.GetComponent<CanvasGroup>();
            }
            if (_scoreboard == null) return;
            _scoreboard.alpha = v ? 1f : 0f;
            _scoreboard.blocksRaycasts = v;
        }

        // ------------------------------------------------------------------ refresh
        private static void RefreshRail()
        {
            if (_score == null) return;
            _score.Refresh(); _bp.Refresh(); _newTask.Refresh(); _structures.Refresh(); _blocks.Refresh();
            if (_spacerFlex2 != null) _spacerFlex2.color = _open == Side.NewTask ? BtcGhKit.Selected : BtcGhKit.RailBg;
            RefreshCount();
        }

        private static void RefreshCount()
        {
            if (_taskCount == null || UIGameObserver.projects == null) return;
            // minus 1: the idle task
            string s = (UIGameObserver.projects.all.Count - 1).ToString() + " / " + (Project.projectCap - 1).ToString();
            if (_taskCount.text != s) _taskCount.text = s;
        }
    }

    /// MoonSharp argument helper for the few lua_* game callbacks that are easier to call than to copy (CallbackArguments is a struct here).
    internal static class BtcGhLua
    {
        private static readonly MoonSharp.Interpreter.CallbackArguments True = new MoonSharp.Interpreter.CallbackArguments(new List<MoonSharp.Interpreter.DynValue> { MoonSharp.Interpreter.DynValue.True }, false);
        private static readonly MoonSharp.Interpreter.CallbackArguments False = new MoonSharp.Interpreter.CallbackArguments(new List<MoonSharp.Interpreter.DynValue> { MoonSharp.Interpreter.DynValue.False }, false);
        internal static MoonSharp.Interpreter.CallbackArguments Args(bool b) { return b ? True : False; }
    }

    /// SidebarFloatingMenu.lua shell inside a LeftContentMenu socket: base (25,25,25), header (title Korolev 22 yellow, close button,
    /// 8 px spacer, 1 px divider), scrollable body (LuaScrollableRight) with an inner Vertical (pad 8,8,0,0), then sections
    /// (LabeledDividerPanel header + Grid of 64x64 cells).
    internal sealed class BtcGhSidebar
    {
        internal readonly RectTransform Socket, Body;
        internal readonly List<BtcGhButton> Buttons = new List<BtcGhButton>();

        internal BtcGhSidebar(RectTransform content, string title, Action close)
        {
            Socket = BtcGhKit.Box(Layouts.Vertical, content, "", 0, 0, 0, 0, 2f, TextAnchor.UpperLeft);
            BtcGhKit.Le(Socket, -1f, -1f, -1f, -1f, -1f, -1f, false);
            RectTransform baseLyt = BtcGhKit.Make(Layouts.Vertical, Socket, "");
            BtcGhKit.Stretch(baseLyt);
            BtcGhKit.Img(baseLyt, null, BtcGhKit.Selected, true);
            BtcGhKit.Le(baseLyt, -1f, -1f, -1f, -1f, 1f, 1f, false);
            BtcGhKit.Group(baseLyt, 0, 0, 0, 0, 0f, TextAnchor.UpperLeft);

            RectTransform header = BtcGhKit.Box(Layouts.Vertical, baseLyt, "", 0, 0, 0, 0, 0f, TextAnchor.MiddleCenter);
            BtcGhKit.Le(header, -1f, 72f, -1f, -1f, 1f, -1f, false);
            RectTransform lyt = BtcGhKit.Box(Layouts.Horizontal, header, "", 0, 0, 0, 0, 0f, TextAnchor.MiddleCenter);
            BtcGhKit.Le(lyt, -1f, 64f, -1f, 64f, 1f, -1f, false);
            RectTransform labelLyt = BtcGhKit.Box(Layouts.Horizontal, lyt, "", 32, 32, 4, 0, 0f, TextAnchor.MiddleCenter);
            BtcGhKit.Le(labelLyt, -1f, 64f, -1f, 64f, 1f, -1f, false);
            RectTransform lbl = BtcGhKit.Make(Texts.Default, labelLyt, "");
            BtcGhKit.Stretch(lbl);
            Text t = BtcGhKit.Txt(lbl, title, 22, BtcGhKit.Yellow, TextAnchor.MiddleCenter);
            if (t != null && BtcGhKit.Korolev != null) t.font = BtcGhKit.Korolev;
            BtcGhKit.Le(lbl, 64f, 64f, -1f, -1f, -1f, -1f, true);
            BtcGhButton closeBtn = BtcGhKit.CloseButton(lyt);
            closeBtn.OnAction = close;
            RectTransform sp = BtcGhKit.Make(Images.Default, header, "");
            BtcGhKit.Img(sp, null, Color.white, false);
            BtcGhKit.Le(sp, -1f, 8f, 0f, 0f, 1f, -1f, false);
            RectTransform div = BtcGhKit.Box(Layouts.Vertical, header, "", 0, 0, 0, 0, 2f, TextAnchor.UpperLeft);
            BtcGhKit.Le(div, -1f, 1f, -1f, 1f, 1f, -1f, false);
            RectTransform line = BtcGhKit.Make(Images.Default, div, "");
            BtcGhKit.Rect(line, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), BtcGhKit.Mid, Vector2.zero, new Vector2(0f, 1f));
            BtcGhKit.Img(line, null, BtcGhKit.Divider, true).raycastTarget = false;
            BtcGhKit.Le(line, 64f, 64f, -1f, -1f, -1f, -1f, true);

            RectTransform scroll = BtcGhKit.Make(Layouts.LuaScrollableRight, baseLyt, "");
            BtcGhKit.Le(scroll, -1f, -1f, -1f, -1f, 1f, -1f, false);
            Transform c = FindDeep(scroll, "Content");
            Body = BtcGhKit.Box(Layouts.Vertical, c != null ? c : scroll, "", 8, 8, 0, 0, 0f, TextAnchor.UpperLeft);
            BtcGhKit.Le(Body, -1f, 0f, -1f, -1f, 1f, -1f, false);
            // stretched button behind the cells: catches clicks between buttons so they never reach the world
            RectTransform catcher = BtcGhKit.Make(Brix.External.Factories.Buttons.Standard, Body, "");
            BtcGhKit.Stretch(catcher);
            BtcGhKit.Le(catcher, 64f, 64f, 64f, 64f, -1f, -1f, true);
            Socket.gameObject.SetActive(false);
        }

        private static Transform FindDeep(Transform t, string name)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                Transform ch = t.GetChild(i);
                if (ch.name == name) return ch;
                Transform r = FindDeep(ch, name);
                if (r != null) return r;
            }
            return null;
        }

        internal RectTransform Section(string label)
        {
            BtcGhKit.SectionHeader(Body, label, BtcGhKit.Red);
            return BtcGhKit.Grid(Body, 64f, 64f);
        }

        internal BtcGhButton Add(BtcGhButton b) { Buttons.Add(b); return b; }

        internal void SetActive(bool on) { if (Socket != null && Socket.gameObject.activeSelf != on) Socket.gameObject.SetActive(on); }

        internal void Refresh() { for (int i = 0; i < Buttons.Count; i++) Buttons[i].Refresh(); }

        internal void RefreshHighlights() { for (int i = 0; i < Buttons.Count; i++) Buttons[i].RefreshHighlight(); }
    }
}
