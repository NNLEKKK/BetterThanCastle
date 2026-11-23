using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.External.Factories;
using Brix.External.Signals;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Input;
using Brix.UI.Builder.Widget;
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle
{
    /// C# twin of the task popup above the bottom task list (Menu_Popup.lua + the GameMenu.lua sections it is filled with:
    /// goals, build, landscape, quarry, tunnel, harvest, cleanup filter (BtcCleanFilter's Lua injection), craft recipes / needs /
    /// finished / queue, priority, task actions, idle population + crystal). Same prefabs and values as the Lua panels (vanilla dump
    /// BTClogs/uidump-vanilla.txt, node "TooltipMenu.lyt"). Perf: no Update/poll; every section (and every button) is built the
    /// first time it becomes visible; refresh runs only on the game signals the Lua connects (selection, commit status, priority,
    /// picker/input mode, tunnel type, kept resources, firefly info, crew info, crafting station selected/queue/loop/content).
    /// Not ported: hover tooltips and the hover arrow (Menu_Tooltip region), hideHeader is ignored like the Lua popup does.
    internal static partial class BtcGhPopup
    {
        // refresh masks (which signal can change which item)
        private const int M_SEL = 1, M_STATUS = 2, M_PRIO = 4, M_INPUT = 8, M_TUNNEL = 16, M_KEPT = 32, M_DONE = 64, M_FIRE = 128, M_POP = 256,
            M_OPSEL = 512, M_OPQ = 1024, M_OPLOOP = 2048, M_OPCONTENT = 4096, M_GOALS = 8192, M_CLEAN = 16384, M_ALL = ~0;

        private static RectTransform _pop, _lyt;
        private static readonly List<Item> Items = new List<Item>();
        private static readonly List<SignalConnection> Conns = new List<SignalConnection>();
        private static readonly Dictionary<string, Sprite> IconCache = new Dictionary<string, Sprite>();

        private static readonly Color Green = new Color(0f, 1f, 0f, 1f);
        private static readonly Color PureRed = new Color(1f, 0f, 0f, 1f);
        private static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);

        internal static RectTransform Root { get { return _pop; } }

        // ------------------------------------------------------------------ public entry
        internal static void Build(RectTransform root)
        {
            try
            {
                Close();
                float t = Time.realtimeSinceStartup;
                // popup layout (GameMenu.lua 3701-3730): a=(0,0)-(1,0) p=(0.5,0) pos y 80 h 72, offsets +-49
                _pop = BtcGhKit.Make(Layouts.Horizontal, root, "");
                BtcGhKit.Rect(_pop, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(-98f, 72f));
                BtcGhKit.Img(_pop, null, new Color(0.5f, 0.5f, 1f, 0.25f), false);
                BtcGhKit.Le(_pop, -1f, -1f, -1f, -1f, -1f, -1f, true);
                BtcGhKit.Group(_pop, 0, 0, 0, 0, 0f, TextAnchor.UpperLeft);
                // Menu_Popup "TooltipMenu.lyt": left anchored, height 72, width = preferred (ContentSizeFitter, layout driven: no polling)
                _lyt = BtcGhKit.Make(Layouts.Horizontal, _pop, "TooltipMenu.lyt");
                BtcGhKit.Rect(_lyt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 72f));
                BtcGhKit.Img(_lyt, null, BtcGhKit.Selected, true).raycastTarget = false;
                BtcGhKit.Le(_lyt, -1f, -1f, -1f, -1f, -1f, -1f, true);
                BtcGhKit.Group(_lyt, 0, 0, 0, 0, 0f, TextAnchor.LowerLeft);
                ContentSizeFitterPlus plus = _lyt.GetComponent<ContentSizeFitterPlus>();
                if (plus != null) plus.enabled = false; // the Lua one polls every frame
                ContentSizeFitter csf = _lyt.GetComponent<ContentSizeFitter>() ?? _lyt.gameObject.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                csf.enabled = true;
                BtcHudSlots.Register("gh.popup", _pop);

                DefineItems();
                AssignKeyActions();
                UpdateSel();
                UpdateOp();
                ReadFirefly();
                Connect();
                Refresh(M_ALL);
                BtcLog.Info("GameHud", "popup built in " + ((Time.realtimeSinceStartup - t) * 1000f).ToString("0") + " ms (" + Items.Count + " items, lazy)");
            }
            catch (Exception e) { BtcLog.Error("GameHud", "popup build: " + e); }
        }

        internal static void Close()
        {
            for (int i = 0; i < Conns.Count; i++) { try { Conns[i].Disconnect(); } catch { } }
            Conns.Clear();
            Items.Clear();
            IconCache.Clear();
            _pop = null; _lyt = null; _sel = null; _op = null; _type = T_NONE;
            BtcGhModes.RaisePriority = null; BtcGhModes.LowerPriority = null; BtcGhModes.DeleteTask = null; BtcGhModes.ToggleLoop = null;
            BtcGhModes.ClearQueue = null; BtcGhModes.BlueprintMgmt = null; BtcGhModes.QueueSlot = null; BtcGhModes.TaskActionSlot = null;
        }

        // ------------------------------------------------------------------ signals
        private static void Connect()
        {
            Conns.Add(Project.lua_Selected._signal.Connect(new Action<Project>(OnSelected)));
            Conns.Add(GameSignals.Connect<Project>(Project.ProjectStarted, new Action<Project>(OnProjectStarted), null));
            Conns.Add(GameSignals.Connect<Project>(Project.OnSetGoalCountSignal, new Action<Project>(OnGoalCount), null));
            Conns.Add(Project.lua_OnSetCommitStatus._signal.Connect(new Action<Project>(OnStatus)));
            Conns.Add(Project.lua_OnSetCrewInfoPriority._signal.Connect(new Action<Project>(OnPriority)));
            if (Project.lua_OnSetCrewInfo != null) Conns.Add(Project.lua_OnSetCrewInfo._signal.Connect(new Action<List<Project>>(OnCrewList)));
            else
            {
                Conns.Add(Project.lua_OnSetCrewInfoA._signal.Connect(new Action<Project>(OnCrew)));
                Conns.Add(Project.lua_OnSetCrewInfoB._signal.Connect(new Action<Project>(OnCrew)));
                Conns.Add(Project.lua_OnSetCrewInfoC._signal.Connect(new Action<Project>(OnCrew)));
            }
            Conns.Add(GameSignals.Connect(PickingObserver.lua_OverridePickerSetWithOptions, new Action(OnInput), null));
            Conns.Add(GameSignals.Connect<Picker, Picker>(PickingObserver.lua_OnSetUserActivePicker, new Action<Picker, Picker>(OnPicker), null));
            Conns.Add(Brix.Game.Compounders.BlueprintCompounder.lua_OnDoneInstantiating._signal.Connect(new Action(OnDoneInstantiating)));
            Conns.Add(GameSignals.Connect(Brix.Game.Components.Tunnel.TunnelVolume.ExpansionTunnelTypeChanged, new Action(OnTunnel), null));
            Conns.Add(GameSignals.Connect(MineProject.ResourceTypeKeptChanged, new Action(OnKept), null));
            Conns.Add(GameSignals.Connect(TunnelProject.ResourceTypeKeptChanged, new Action(OnKept), null));
            if (FireflyNest.lua_OnSetInfo != null) Conns.Add(FireflyNest.lua_OnSetInfo._signal.Connect(new Action(OnFirefly)));
            else
            {
                Conns.Add(FireflyNest.lua_OnSetCurrentXp._signal.Connect(new Action(OnFirefly)));
                Conns.Add(FireflyNest.lua_OnSetTotalXp._signal.Connect(new Action(OnFirefly)));
                Conns.Add(FireflyNest.lua_OnSetSpawnableCount._signal.Connect(new Action(OnFirefly)));
            }
            Conns.Add(CraftingStation.lua_OnSetSelected._signal.Connect(new Action(OnOpSelected)));
            Conns.Add(CraftingStation.lua_OnSetQueue._signal.Connect(new Action(OnOpQueue)));
            Conns.Add(CraftingStation.lua_OnSetLoop._signal.Connect(new Action(OnOpLoop)));
            Conns.Add(CraftingStation.lua_OnSetContent._signal.Connect(new Action(OnOpContent)));
        }

        private static void OnSelected(Project p) { try { UpdateSel(); Refresh(M_ALL); } catch (Exception e) { BtcLog.Error("GameHud", "popup selected: " + e); } }
        private static void OnProjectStarted(Project p) { try { UpdateSel(); Refresh(M_SEL); } catch (Exception e) { BtcLog.Error("GameHud", "popup started: " + e); } }
        private static void OnGoalCount(Project p) { if (p == _sel) Refresh(M_GOALS); }
        private static void OnStatus(Project p) { Refresh(M_STATUS); }
        private static void OnPriority(Project p) { Refresh(M_PRIO); }
        private static void OnCrew(Project p) { Refresh(M_POP); }
        private static void OnCrewList(List<Project> l) { Refresh(M_POP); }
        private static void OnInput() { Refresh(M_INPUT); }
        private static void OnPicker(Picker a, Picker b) { Refresh(M_INPUT); }
        private static void OnDoneInstantiating() { Refresh(M_DONE); }
        private static void OnTunnel() { Refresh(M_TUNNEL); }
        private static void OnKept() { Refresh(M_KEPT); }
        private static void OnFirefly() { ReadFirefly(); Refresh(M_FIRE); }
        private static void OnOpSelected() { UpdateOp(); Refresh(M_OPSEL | M_OPQ | M_OPLOOP | M_OPCONTENT); }
        private static void OnOpQueue() { Refresh(M_OPQ); }
        private static void OnOpLoop() { Refresh(M_OPLOOP); }
        private static void OnOpContent() { ReadOpContent(); Refresh(M_OPCONTENT); }

        private static void Refresh(int mask)
        {
            if (_lyt == null) return;
            for (int i = 0; i < Items.Count; i++)
            {
                Item it = Items[i];
                if ((it.Mask & mask) == 0) continue;
                try { it.Update(); }
                catch (Exception e) { BtcLog.Error("GameHud", "popup refresh item " + i + ": " + e); }
            }
        }

        // ------------------------------------------------------------------ selection (Data.Project)
        private const int T_NONE = 0, T_IDLE = 1, T_BUILD = 2, T_REPAIR = 3, T_LAND = 4, T_QUARRY = 5, T_TUNNEL = 6, T_HARVEST = 7, T_CLEAN = 8, T_OTHER = 9;
        private static Project _sel;
        private static int _type;

        private static Project FindSelected()
        {
            if (UIGameObserver.projects != null)
                foreach (Project p in UIGameObserver.projects.all) if (p != null && p.selection) return p;
            return IdleProject.GetAlliedProject();
        }

        private static int TypeOf(Project p)
        {
            if (p is IdleProject) return T_IDLE;
            if (p is BuildProject) return T_BUILD;
            if (p is RepairProject) return T_REPAIR;
            if (p is LandscapeProject) return T_LAND;
            if (p is MineProject) return T_QUARRY;
            if (p is TunnelProject) return T_TUNNEL;
            if (p is HarvestProject) return T_HARVEST;
            if (p is CleanupProject) return T_CLEAN;
            return T_OTHER;
        }

        /// Data.Project:OnSetSelected: the type is taken at selection time (false while the project is still being placed).
        private static void UpdateSel()
        {
            _sel = FindSelected();
            _type = _sel != null && !_sel.IsBeingPlaced ? TypeOf(_sel) : T_NONE;
        }

        private static bool Has() { return _sel != null && !_sel.IsBeingPlaced; }
        private static bool OfType(int t) { return Has() && _type == t; }
        private static bool NotIdle() { return Has() && _type != T_IDLE; }
        private static bool IsBuildSel() { return OfType(T_BUILD); }
        private static bool IsLandSel() { return OfType(T_LAND); }
        private static bool IsQuarrySel() { return OfType(T_QUARRY); }
        private static bool IsTunnelSel() { return OfType(T_TUNNEL); }
        private static bool IsHarvestSel() { return OfType(T_HARVEST); }
        private static bool IsCleanSel() { return OfType(T_CLEAN); }
        private static bool IsIdleNoOp() { return OfType(T_IDLE) && _op == null; }
        private static bool IsNotBuildNotIdle() { return NotIdle() && _type != T_BUILD; }
        private static bool IsBuildNotIdle() { return NotIdle() && _type == T_BUILD; }
        private static bool Selecting() { return InputModeController.IsSelectingBlueprints(); }
        private static bool BuildSelecting() { return OfType(T_BUILD) && InputModeController.IsSelectingBlueprints(); }
        private static bool Paused() { return ClockConfig.Paused; }
        private static bool Always() { return true; }

        // ------------------------------------------------------------------ items (the AddDivider / AddSection chain of GameMenu.lua 3965-4003)
        private abstract class Item
        {
            internal int Mask;
            internal Func<bool> Visible;
            internal RectTransform Root;

            internal abstract void Build();
            internal virtual void RefreshContent() { }

            internal void Update()
            {
                bool v = Visible();
                if (v && Root == null)
                {
                    Build();
                    Root.SetSiblingIndex(BuiltBefore(this));
                }
                if (Root == null) return;
                if (Root.gameObject.activeSelf != v) Root.gameObject.SetActive(v);
                if (v) RefreshContent();
            }
        }

        private static int BuiltBefore(Item it)
        {
            int n = 0;
            for (int i = 0; i < Items.Count && Items[i] != it; i++) if (Items[i].Root != null) n++;
            return n;
        }

        /// Menu_Popup AddDivider: Vertical 5 x 48, bg 25, a 1 px grey line (offsets 8 from below, OffsetMax y 8 default or -8).
        private sealed class Div : Item
        {
            private readonly float _maxY;
            internal Div(int mask, Func<bool> vis, float maxY) { Mask = mask; Visible = vis; _maxY = maxY; }
            internal override void Build() { Root = MakeDivider(_lyt, _maxY); }
        }

        internal static RectTransform MakeDivider(Transform parent, float maxY)
        {
            RectTransform d = BtcGhKit.Make(Layouts.Vertical, parent, "");
            BtcGhKit.Stretch(d);
            BtcGhKit.Img(d, null, BtcGhKit.Selected, true).raycastTarget = false;
            BtcGhKit.Le(d, 5f, 48f, 5f, 48f, -1f, -1f, false);
            BtcGhKit.Group(d, 0, 0, 0, 0, 2f, TextAnchor.MiddleCenter);
            RectTransform line = BtcGhKit.Make(Images.Default, d, "");
            float sdy = maxY - 8f;
            BtcGhKit.Rect(line, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), BtcGhKit.Mid, new Vector2(0f, 8f + sdy * 0.5f), new Vector2(1f, sdy));
            BtcGhKit.Img(line, null, BtcGhKit.Divider, true).raycastTarget = false;
            BtcGhKit.Le(line, 64f, 64f, -1f, -1f, -1f, -1f, true);
            return d;
        }

        private abstract class Cell
        {
            internal Func<bool> Visible;
            internal GameObject Go;
            internal abstract void Create(Sec s);
            internal abstract void Refresh();
        }

        /// Menu_Popup AddSection: Horizontal 72 high (pad 2, spacing 1, lower left), red size-12 label at the upper left.
        private sealed class Sec : Item
        {
            private readonly string _labelKey;
            private readonly Action<Sec> _fill;
            internal readonly List<Cell> Cells = new List<Cell>();
            private RectTransform _label;

            internal Sec(int mask, Func<bool> vis, string labelKey, Action<Sec> fill) { Mask = mask; Visible = vis; _labelKey = labelKey; _fill = fill; }

            internal Sec Add(Cell c) { Cells.Add(c); return this; }

            internal override void Build()
            {
                Root = BtcGhKit.Box(Layouts.Horizontal, _lyt, "", 2, 2, 2, 2, 1f, TextAnchor.LowerLeft);
                BtcGhKit.Le(Root, -1f, 72f, -1f, 72f, -1f, -1f, false);
                _fill(this);
                string text = _labelKey == null ? "" : BtcGameHud.Loc(_labelKey);
                if (!string.IsNullOrEmpty(text))
                {
                    _label = BtcGhKit.Make(Texts.Default, Root, "");
                    BtcGhKit.Rect(_label, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(-16f, -12f));
                    Text t = BtcGhKit.Txt(_label, text, 12, BtcGhKit.Red, TextAnchor.UpperLeft);
                    if (t != null) t.raycastTarget = false;
                    BtcGhKit.Le(_label, 64f, 64f, -1f, -1f, -1f, -1f, true);
                }
            }

            internal override void RefreshContent()
            {
                int before = 0;
                for (int i = 0; i < Cells.Count; i++)
                {
                    Cell c = Cells[i];
                    if (c.Go == null)
                    {
                        if (c.Visible != null && !c.Visible()) continue;
                        c.Create(this);
                        c.Go.transform.SetSiblingIndex(before);
                        if (_label != null) _label.SetAsLastSibling();
                    }
                    c.Refresh();
                    before++;
                }
            }
        }

        /// In-section divider (blueprint tools, queue options): OffsetMax (-8, -8).
        private sealed class InnerDiv : Cell
        {
            internal InnerDiv(Func<bool> vis) { Visible = vis; }
            internal override void Create(Sec s) { Go = MakeDivider(s.Root, -8f).gameObject; }
            internal override void Refresh()
            {
                bool v = Visible == null || Visible();
                if (Go.activeSelf != v) Go.SetActive(v);
            }
        }

        // ------------------------------------------------------------------ ButtonPanel as AddMenuHandleToggle builds it
        private sealed class Btn : Cell
        {
            internal string IconName;
            internal Func<Sprite> IconFn;
            internal float IconSize = 64f;
            internal Color IconColor = BtcGhKit.Yellow;
            internal Func<Color> IconColorFn;
            internal bool NoHover, HasHotkey;
            internal int HotkeySize = 10;
            internal Func<string> Hotkey;
            internal string CountConst = "";
            internal Func<string> CountFn;
            internal int CountSize = 10;
            internal TextAnchor CountAlign = TextAnchor.LowerCenter;
            internal Color CountColor = BtcGhKit.Yellow;
            internal Func<Color> CountColorFn;
            internal Color HoverColor = BtcGhButton.FgHover;
            internal Action OnAction;
            internal Func<int> Highlight;

            private BtcGhButton _b;
            private Image _fg;
            private Text _count;

            internal override void Create(Sec s)
            {
                RectTransform p = BtcGhKit.Make(Layouts.Horizontal, s.Root, "ButtonPanel @ _p");
                Go = p.gameObject;
                BtcGhKit.Rect(p, new Vector2(0f, 1f), new Vector2(0f, 1f), BtcGhKit.Mid, Vector2.zero, new Vector2(48f, 48f));
                BtcGhKit.Img(p, null, new Color32(0, 0, 0, 127), false);
                BtcGhKit.Le(p, 48f, 48f, 48f, 48f, -1f, -1f, false);
                BtcGhKit.Group(p, 0, 0, 0, 0, 2f, TextAnchor.MiddleLeft);

                RectTransform hov = BtcGhKit.Make(Layouts.Horizontal, p, "ButtonPanel @ _p.lyt_hover");
                BtcGhKit.Stretch(hov);
                BtcGhKit.Img(hov, null, new Color32(0, 0, 0, 192), false);
                BtcGhKit.Le(hov, -1f, -1f, -1f, -1f, -1f, -1f, true);
                BtcGhKit.Group(hov, 0, 4, 4, 0, 2f, TextAnchor.UpperLeft);
                Image hoverBg = null;
                Text hotkey = null;
                if (!NoHover)
                {
                    RectTransform hb = BtcGhKit.Make(Images.Default, hov, "ButtonPanel @ _p.lyt_hover.img_hoverBg");
                    BtcGhKit.Stretch(hb);
                    hoverBg = BtcGhKit.Img(hb, null, BtcGhKit.Clear, true);
                    BtcGhKit.Le(hb, 64f, 64f, -1f, -1f, -1f, -1f, true);
                }
                if (HasHotkey)
                {
                    RectTransform hk = BtcGhKit.Make(Texts.Default, hov, "ButtonPanel @ _p.lyt_hover.lbl_hoverHotkey");
                    BtcGhKit.Stretch(hk, 4f);
                    hotkey = BtcGhKit.Txt(hk, "", HotkeySize, BtcGhKit.Yellow, TextAnchor.UpperRight);
                    BtcGhKit.Le(hk, 64f, 64f, -1f, -1f, -1f, -1f, true);
                }
                RectTransform fg = BtcGhKit.Make(Images.Default, p, "ButtonPanel @ _p.img_fg");
                BtcGhKit.Centre(fg, IconSize, IconSize);
                Sprite sp = IconFn != null ? IconFn() : (IconName != null ? Ico(IconName) : null);
                _fg = BtcGhKit.Img(fg, sp, IconColorFn != null ? IconColorFn() : IconColor, true);
                BtcGhKit.Le(fg, 64f, 64f, -1f, -1f, -1f, -1f, true);
                RectTransform lbl = BtcGhKit.Make(Texts.Default, p, "ButtonPanel @ _p.lbl_fg");
                BtcGhKit.Stretch(lbl);
                _count = BtcGhKit.Txt(lbl, CountFn != null ? CountFn() : CountConst, CountSize, CountColorFn != null ? CountColorFn() : CountColor, CountAlign);
                BtcGhKit.Le(lbl, 64f, 64f, -1f, -1f, -1f, -1f, true);
                RectTransform btnRt = null;
                if (!NoHover)
                {
                    btnRt = BtcGhKit.Make(Buttons.Standard, p, "ButtonPanel @ _p.btn");
                    BtcGhKit.Stretch(btnRt);
                    BtcGhKit.Le(btnRt, 64f, 64f, 64f, 64f, -1f, -1f, true);
                    _b = btnRt.gameObject.AddComponent<BtcGhButton>();
                    _b.Frame = btnRt.GetComponent<FrameAnimatorWidgetComponent>() ?? btnRt.GetComponentInChildren<FrameAnimatorWidgetComponent>(true);
                }
                else
                {
                    _b = p.gameObject.AddComponent<BtcGhButton>(); // state holder only: no pointer target, no sounds
                    _b.Silent = true;
                }
                _b.HoverBg = hoverBg; _b.HotkeyLabel = hotkey; _b.Icon = _fg; _b.HoverColor = HoverColor;
                _b.HotkeyFn = Hotkey; _b.OnAction = OnAction; _b.Highlight = Highlight;
                if (IconColorFn != null) _b.IconColor = IconColorFn;
                NoRaycast(p, btnRt);
            }

            internal override void Refresh()
            {
                bool v = Visible == null || Visible();
                if (Go.activeSelf != v) Go.SetActive(v);
                if (!v) return;
                if (IconFn != null) { Sprite sp = IconFn(); if (_fg.sprite != sp) _fg.sprite = sp; }
                if (CountFn != null) { string s = CountFn(); if (_count.text != s) _count.text = s; }
                if (CountColorFn != null) _count.color = CountColorFn();
                _b.Refresh();
            }
        }

        /// Lua panels set Raycasts false on everything but the button, so empty popup areas never swallow world clicks.
        private static void NoRaycast(Transform root, Transform keep)
        {
            Graphic[] gs = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < gs.Length; i++)
            {
                if (keep != null && gs[i].transform.IsChildOf(keep)) continue;
                gs[i].raycastTarget = false;
            }
        }

        private static Sprite Ico(string name)
        {
            Sprite s;
            if (!IconCache.TryGetValue(name, out s)) { s = BtcGhKit.Icon(name); IconCache[name] = s; }
            return s;
        }

        private static Func<string> Key(string category, string action)
        {
            return delegate { return BtcGameHud.Hotkey(category, action); };
        }

        private static readonly Action NoOp = delegate { };
    }
}
