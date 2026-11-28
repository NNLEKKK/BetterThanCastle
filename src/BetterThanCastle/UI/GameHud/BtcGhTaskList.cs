using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.Engine.Nature;
using Brix.External.Factories;
using Brix.External.Signals;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Network;
using Brix.Input;
using Brix.UI;
using Brix.UI.Builder.Widget;
using Brix.UI.GameSelector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BetterThanCastle
{
    /// Bottom task list of the C# game menu (Menu_TaskList.lua + TaskbarProjectCrewPanel.lua): idle task, selected operable, one panel
    /// per project (pooled slots, created the first time they are needed), a flexible spacer, melee and ranged army panels.
    /// Event driven: project selected/started/deleted, crew info (collapsed by the engine to once per frame), crafting selection,
    /// icon changed. No Update; modifier keys are only listened to while the pointer is over a crew button. Refreshes compare
    /// against the last applied values so repeated game signals cost counting only. Crew faces are created on demand.
    internal static class BtcGhTaskList
    {
        // ------------------------------------------------------------------ hooks for the popup / tooltip menus (main session wires them)
        /// Popup target changed (selected panel root, or the operable panel); null target = reset.
        internal static Action<RectTransform> PopupSet;
        internal static Action PopupReset;
        /// m_popup.Visible(): the popup arrow of the selected panel is shown while this is true. Call RefreshArrows after it changes.
        internal static Func<bool> PopupVisible;
        /// Task tooltip menu: target rect + tooltip path (e.g. Task/All/Select); reset; Visible().
        internal static Action<RectTransform, string[]> TooltipSet;
        internal static Action TooltipReset;
        internal static Func<bool> TooltipVisible;

        /// Root of the selected task panel (the operable panel while a crafting station is selected), or null.
        internal static RectTransform SelectedPanelRect
        {
            get
            {
                if (_list == null) return null;
                if (_station != null && _operable != null) return _operable.Root;
                Slot s = Find(_sel);
                return s != null ? s.Root : null;
            }
        }

        // ------------------------------------------------------------------ constants
        private const int KIdle = 0, KOperable = 1, KKnight = 2, KArcher = 3, KRegular = 4;
        private enum PT { None, Build, Quarry, Cleanup, Harvest, Repair, Landscape, Tunnel, Idle }

        private static readonly Color ArrowColor = new Color(22f / 255f, 22f / 255f, 22f / 255f, 1f);
        private static readonly string[] TipSelect = { "Task", "All", "Select" };
        private static readonly string[] TipDeselect = { "Task", "All", "Deselect" };
        private static readonly string[] TipSelectCrew = { "Task", "All", "SelectCrew" };
        private static readonly string[] TipRaise = { "Task", "All", "RaisePriority" };
        private static readonly string[] TipLower = { "Task", "All", "LowerPriority" };

        // ------------------------------------------------------------------ sprites (IconKeys of the Lua panels)
        private static class Spr
        {
            internal static Sprite Arrow, Up, Down, Plus, Minus, Select, Help, Sword, Bow;
            internal static Sprite PLow, PMed, PHigh, PLocked;
            internal static Sprite Build, Quarry, Cleanup, Harvest, Repair, Landscape, Tunnel, Crystal;
            internal static Sprite HStone, HIron, HBrim, HWood, HPlants, HAll;
            internal static Sprite Worker, WorkerSel, Empty;
            internal static Sprite Hal, HalSel, Knight, KnightSel, Archer, ArcherSel, Arbalist, ArbalistSel, Artificer, ArtificerSel, Alchemist, AlchemistSel;
            internal static Sprite Furnace, Chopping, Loom, Workbench, Forge, Machine, Lab, Altar;
        }

        private static Sprite Ic(string key)
        {
            Sprite s = BtcGhKit.Icon(key);
            if (s == null) BtcLog.Warn("GameHud", "task list: icon missing: " + key);
            return s;
        }

        private static void LoadSprites()
        {
            Spr.Arrow = Ic("_UI_Big_Arrow_Down"); Spr.Up = Ic("UI_Up"); Spr.Down = Ic("UI_Down"); Spr.Plus = Ic("UI_Plus"); Spr.Minus = Ic("UI_Minus");
            Spr.Select = Ic("UI_Select"); Spr.Help = Ic("UI_Help"); Spr.Sword = Ic("UI_Sword"); Spr.Bow = Ic("UI_Bow");
            Spr.PLow = Ic("_UI_PriorityLow_alt"); Spr.PMed = Ic("_UI_PriorityMedium_alt"); Spr.PHigh = Ic("_UI_PriorityHigh_alt"); Spr.PLocked = Ic("_UI_PriorityLocked_alt");
            Spr.Build = Ic("_UI_Build"); Spr.Quarry = Ic("_UI_Quarry"); Spr.Cleanup = Ic("_UI_Cleanup"); Spr.Harvest = Ic("_UI_Pick"); Spr.Repair = Ic("_UI_Repair");
            Spr.Landscape = Ic("_UI_Landscape"); Spr.Tunnel = Ic("_UI_Tunnel"); Spr.Crystal = Ic("_UI_CrystalOutline");
            Spr.HStone = Ic("_UI_HarvestStone"); Spr.HIron = Ic("_UI_HarvestIron"); Spr.HBrim = Ic("_UI_HarvestBrimstone"); Spr.HWood = Ic("_UI_HarvestWood");
            Spr.HPlants = Ic("_UI_HarvestPlants"); Spr.HAll = Ic("_UI_HarvestAll");
            Spr.Worker = Ic("_Face_Worker"); Spr.WorkerSel = Ic("_Face_Worker_Selected"); Spr.Empty = Ic("_Face_Empty");
            Spr.Hal = Ic("_Face_Halberdier"); Spr.HalSel = Ic("_Face_Halberdier_Selected"); Spr.Knight = Ic("_Face_Knight"); Spr.KnightSel = Ic("_Face_Knight_Selected");
            Spr.Archer = Ic("_Face_Archer"); Spr.ArcherSel = Ic("_Face_Archer_Selected"); Spr.Arbalist = Ic("_Face_Arbalist"); Spr.ArbalistSel = Ic("_Face_Arbalist_Selected");
            Spr.Artificer = Ic("_Face_Artificer"); Spr.ArtificerSel = Ic("_Face_Artificer_Selected"); Spr.Alchemist = Ic("_Face_Alchemist"); Spr.AlchemistSel = Ic("_Face_Alchemist_Selected");
            Spr.Furnace = Ic("_Furnace"); Spr.Chopping = Ic("_Chopping_Block"); Spr.Loom = Ic("_Loom"); Spr.Workbench = Ic("_Workbench");
            Spr.Forge = Ic("_Forge"); Spr.Machine = Ic("_Machine_Shop"); Spr.Lab = Ic("_Lab"); Spr.Altar = Ic("_Altar");
        }

        // ------------------------------------------------------------------ state
        private sealed class Crew
        {
            internal Slot Owner;
            internal int Max, Shown;
            internal RectTransform Root, Group;
            internal RectTransform[] Faces;
            internal Image[] Imgs;
            internal Image HoverBg, HoverImg, Arrow;
            internal bool Hovering, TipOn;
            internal readonly int[] K = { -1, -1, -1, -1, -1 };
        }

        private sealed class Slot
        {
            internal int Kind, Id;
            internal Project Proj;
            internal PT Type;
            internal RectTransform Root, TaskLyt;
            internal Image Icon, HoverBg, HoverImg, Prio, TipArrow, PopArrow;
            internal Text Title;
            internal string TitleKey;
            internal BtcGhTlPtr Ptr;
            internal Crew Crew;
            internal bool Hover;
        }

        private static RectTransform _list, _spacer;
        private static readonly List<Slot> Pool = new List<Slot>(); // project slots (ids 3..), pooled like the Lua panels
        private static readonly Dictionary<Project, Slot> ByProject = new Dictionary<Project, Slot>();
        private static Slot _idle, _operable, _knight, _archer;
        private static readonly List<SignalConnection> Conns = new List<SignalConnection>();
        private static Project _sel;
        private static CraftingStation _station;
        private static Crew _modCrew;
        private static bool _modsOn;

        // scratch for army counts and face segments (single thread, no allocations)
        private static readonly int[] JobCount = new int[8], JobSel = new int[8];
        private static int _total, _melee, _ranged;
        private static readonly Sprite[] SegSp = new Sprite[10];
        private static readonly int[] SegN = new int[10];
        private static int _segs;

        // ------------------------------------------------------------------ build / close
        internal static void Build(RectTransform root)
        {
            Close();
            try
            {
                float t0 = Time.realtimeSinceStartup;
                LoadSprites();
                // GameMenu.lua 3958: Horizontal a=(0,0)-(1,0) p=(0.5,0) height 72, offsets (49,0)/(-49,0), spacing 1, image off
                _list = BtcGhKit.Make(Layouts.Horizontal, root, "");
                BtcGhKit.Rect(_list, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(-98f, 72f));
                BtcGhKit.Img(_list, null, new Color32(0, 0, 0, 192), false).raycastTarget = false;
                BtcGhKit.Le(_list, -1f, -1f, -1f, -1f, -1f, -1f, true);
                BtcGhKit.Group(_list, 0, 0, 0, 0, 1f, TextAnchor.UpperLeft);
                BtcHudSlots.Register("gh.tasks", _list);

                _sel = FindSelected();
                _station = CurrentStation();

                _idle = NewSlot(KIdle, 1);
                _idle.Proj = IdleProject.GetAlliedProject();
                _idle.Type = PT.Idle;
                if (_idle.Proj != null) ByProject[_idle.Proj] = _idle;
                // SpacerPanel: Images.Default, image off, min height 48, flexible width 1
                _spacer = BtcGhKit.Make(Images.Default, _list, "");
                BtcGhKit.Rect(_spacer, new Vector2(0f, 1f), new Vector2(0f, 1f), BtcGhKit.Mid, Vector2.zero, new Vector2(0f, 48f));
                BtcGhKit.Img(_spacer, null, Color.white, false).raycastTarget = false;
                BtcGhKit.Le(_spacer, -1f, 48f, 0f, 0f, 1f, -1f, false);
                _knight = NewSlot(KKnight, 111);
                _archer = NewSlot(KArcher, 112);

                if (_station != null) EnsureOperable();
                if (UIGameObserver.projects != null)
                    foreach (Project p in UIGameObserver.projects.all) Register(p);

                Connect();
                ComputeArmy();
                RefreshAllSlots();
                UpdatePopup();
                BtcLog.Info("GameHud", "task list built in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms (" + (Pool.Count + 4) + " panels)");
            }
            catch (Exception e) { BtcLog.Error("GameHud", "task list build: " + e); }
        }

        internal static void Close()
        {
            for (int i = 0; i < Conns.Count; i++) { try { Conns[i].Disconnect(); } catch { } }
            Conns.Clear();
            ModDelegates(false);
            _modCrew = null;
            Pool.Clear();
            ByProject.Clear();
            _idle = _operable = _knight = _archer = null;
            _list = _spacer = null;
            _sel = null; _station = null;
        }

        private static void Connect()
        {
            Conns.Add(Project.lua_Selected._signal.Connect(new Action<Project>(OnProjectSelected)));
            Conns.Add(GameSignals.Connect<Project>(Project.ProjectStarted, new Action<Project>(OnProjectStarted), null));
            Conns.Add(GameSignals.Connect<Project>(Project.ProjectDeleted, new Action<Project>(OnProjectDeleted), null));
            Conns.Add(Project.lua_OnIconChanged._signal.Connect(new Action<Project>(OnIconChanged)));
            Conns.Add(CraftingStation.lua_OnSetSelected._signal.Connect(new Action(OnStationSelected)));
            if (Project.lua_OnSetCrewInfo != null)
                Conns.Add(Project.lua_OnSetCrewInfo._signal.Connect(new Action<List<Project>>(OnCrewInfo)));
            else
            {
                // collapsed hook not created yet: use the raw hooks (a refresh per event, still change-detected)
                BtcLog.Warn("GameHud", "task list: lua_OnSetCrewInfo missing, using raw crew hooks");
                Conns.Add(Project.lua_OnSetCrewInfoA._signal.Connect(new Action<Project>(OnCrewOne)));
                Conns.Add(Project.lua_OnSetCrewInfoB._signal.Connect(new Action<Project>(OnCrewOne)));
                Conns.Add(Project.lua_OnSetCrewInfoC._signal.Connect(new Action<Project>(OnCrewOne)));
                Conns.Add(Project.lua_OnSetCrewInfoPriority._signal.Connect(new Action<Project>(OnCrewOne)));
                Conns.Add(Project.lua_OnSetSelectedCrewCount._signal.Connect(new Action<Project>(OnCrewOne)));
            }
        }

        // ------------------------------------------------------------------ data (Data.Project / Data.Operable)
        private static Project FindSelected()
        {
            if (UIGameObserver.projects != null)
                foreach (Project p in UIGameObserver.projects.all) if (p != null && p.selection) return p;
            return IdleProject.GetAlliedProject();
        }

        private static CraftingStation CurrentStation()
        {
            CraftingStation st = UIGameObserver.crafting != null ? UIGameObserver.crafting.CurrentSelected : null;
            return (st == null || st.IsNullOrReleased()) ? null : st;
        }

        private static Slot Find(Project p)
        {
            Slot s;
            return (p != null && ByProject.TryGetValue(p, out s)) ? s : null;
        }

        private static PT TypeOf(Project p)
        {
            if (p is IdleProject) return PT.Idle;
            if (p is BuildProject) return PT.Build;
            if (p is MineProject) return PT.Quarry;
            if (p is CleanupProject) return PT.Cleanup;
            if (p is HarvestProject) return PT.Harvest;
            if (p is RepairProject) return PT.Repair;
            if (p is LandscapeProject) return PT.Landscape;
            if (p is TunnelProject) return PT.Tunnel;
            return PT.None;
        }

        private static bool Exists(Slot s)
        {
            switch (s.Kind)
            {
                case KOperable: return _station != null;
                case KRegular: return s.Proj != null && !s.Proj.IsNullOrReleased();
                default: return true;
            }
        }

        private static bool IsSelected(Slot s)
        {
            switch (s.Kind)
            {
                case KOperable: return _station != null;
                case KIdle:
                case KRegular: return _station == null && s.Proj != null && s.Proj == _sel;
                default: return false;
            }
        }

        private static void Builders(Project p, out int all, out int sel)
        {
            all = 0; sel = 0;
            if (p == null) return;
            ICollection<Labor> crew = p.Crew();
            WorkForce wf = crew as WorkForce;
            if (wf != null)
            {
                foreach (Labor l in wf)
                {
                    if (l == null || l.Occupation.CurrentJob != Occupation.Job.Builder) continue;
                    all++;
                    if (l.selection) sel++;
                }
            }
            else if (crew != null)
            {
                foreach (Labor l in crew)
                {
                    if (l == null || l.Occupation.CurrentJob != Occupation.Job.Builder) continue;
                    all++;
                    if (l.selection) sel++;
                }
            }
        }

        /// Project.GetFightersInfo / GetIdlersInfo without the Lua tables: per-job counts, selected counts, occupation counts.
        private static void ComputeArmy()
        {
            Array.Clear(JobCount, 0, JobCount.Length);
            Array.Clear(JobSel, 0, JobSel.Length);
            _total = 0; _melee = 0; _ranged = 0;
            if (UIGameObserver.bricktrons == null) return;
            foreach (Labor l in UIGameObserver.bricktrons.CheckCreation())
            {
                if (l == null) continue;
                _total++;
                JobCount[(int)l.Occupation.CurrentJob]++;
                Occupation.Type o = l.Occupation.CurrentOccupation;
                if (o == Occupation.Type.Knight) _melee++;
                else if (o == Occupation.Type.Archer) _ranged++;
            }
            foreach (Labor l in UIGameObserver.bricktrons.CheckSelection())
                if (l != null) JobSel[(int)l.Occupation.CurrentJob]++;
        }

        // ------------------------------------------------------------------ slots
        private static Slot NewSlot(int kind, int id)
        {
            Slot s = new Slot();
            s.Kind = kind; s.Id = id;
            bool army = kind == KKnight || kind == KArcher;

            s.Root = BtcGhKit.Make(Layouts.Horizontal, _list, "");
            BtcGhKit.Rect(s.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), BtcGhKit.Mid, Vector2.zero, new Vector2(0f, 72f));
            BtcGhKit.Img(s.Root, null, BtcGhKit.Selected, true).raycastTarget = false; // _BgColor: selected and default are the same colour
            BtcGhKit.Le(s.Root, -1f, 72f, -1f, 72f, -1f, -1f, false);
            BtcGhKit.Group(s.Root, 2, 2, 2, 2, 0f, TextAnchor.LowerLeft);

            // task layout: icon, hover bg, hover arrow
            s.TaskLyt = BtcGhKit.Make(Layouts.Horizontal, s.Root, "");
            BtcGhKit.Rect(s.TaskLyt, new Vector2(0f, 1f), new Vector2(0f, 1f), BtcGhKit.Mid, Vector2.zero, new Vector2(48f, 48f));
            BtcGhKit.Img(s.TaskLyt, null, new Color32(0, 0, 0, 192), false);
            BtcGhKit.Le(s.TaskLyt, 12f, 48f, 48f, 48f, -1f, -1f, false);
            BtcGhKit.Group(s.TaskLyt, 0, 0, 0, 0, 2f, TextAnchor.MiddleLeft);
            float isz = (kind == KIdle || army) ? 32f : 48f;
            RectTransform ic = BtcGhKit.Make(Images.Default, s.TaskLyt, "");
            BtcGhKit.Centre(ic, isz, isz);
            BtcGhKit.Le(ic, 64f, 64f, -1f, -1f, -1f, -1f, true);
            s.Icon = BtcGhKit.Img(ic, null, Color.white, true);
            s.Icon.raycastTarget = false;
            if (!army)
            {
                RectTransform hb = BtcGhKit.Make(Images.Default, s.TaskLyt, "");
                BtcGhKit.Stretch(hb);
                BtcGhKit.Le(hb, 64f, 64f, -1f, -1f, -1f, -1f, true);
                s.HoverBg = BtcGhKit.Img(hb, null, BtcGhButton.FgHover, false);
                s.HoverBg.raycastTarget = false;
                RectTransform hi = BtcGhKit.Make(Images.Default, s.TaskLyt, "");
                BtcGhKit.Centre(hi, 32f, 32f);
                BtcGhKit.Le(hi, 64f, 64f, -1f, -1f, -1f, -1f, true);
                s.HoverImg = BtcGhKit.Img(hi, Spr.Up, BtcGhKit.Yellow, false);
                s.HoverImg.raycastTarget = false;
            }

            // task button over the whole panel
            s.Ptr = Pointer(s.Root);
            s.Ptr.OnEnter = delegate { HoverOn(s); };
            s.Ptr.OnExit = delegate { HoverOff(s); };
            s.Ptr.OnReset = delegate { s.Hover = false; };
            s.Ptr.OnClick = delegate(int mode) { if (mode != BtcGhTlPtr.Right) ToggleSelect(s); };

            s.Crew = NewCrew(s);

            // title label (offsets 8,6 / -8,-6)
            RectTransform tl = BtcGhKit.Make(Texts.Default, s.Root, "");
            BtcGhKit.Rect(tl, Vector2.zero, Vector2.one, BtcGhKit.Mid, Vector2.zero, new Vector2(-16f, -12f));
            s.Title = BtcGhKit.Txt(tl, "", 12, BtcGhKit.Yellow, TextAnchor.UpperLeft);
            if (s.Title != null) s.Title.raycastTarget = false;
            BtcGhKit.Le(tl, 64f, 64f, -1f, -1f, -1f, -1f, true);

            if (kind == KRegular)
            {
                RectTransform pr = BtcGhKit.Make(Images.Default, s.Root, "");
                BtcGhKit.Rect(pr, Vector2.one, Vector2.one, Vector2.one, new Vector2(0f, 6f), new Vector2(32f, 32f));
                BtcGhKit.Le(pr, 64f, 64f, -1f, -1f, -1f, -1f, true);
                s.Prio = BtcGhKit.Img(pr, null, Color.white, true);
                s.Prio.raycastTarget = false;
            }
            if (kind == KOperable || kind == KRegular) s.Root.gameObject.SetActive(false);
            return s;
        }

        /// Buttons.Standard stretched over its parent with the pointer behaviour (no sounds, like the raw Lua widgets).
        private static BtcGhTlPtr Pointer(RectTransform parent)
        {
            RectTransform btn = BtcGhKit.Make(Buttons.Standard, parent, "");
            BtcGhKit.Stretch(btn);
            BtcGhKit.Le(btn, 64f, 64f, 64f, 64f, -1f, -1f, true);
            BtcGhTlPtr p = btn.gameObject.AddComponent<BtcGhTlPtr>();
            p.Frame = btn.GetComponent<FrameAnimatorWidgetComponent>() ?? btn.GetComponentInChildren<FrameAnimatorWidgetComponent>(true);
            return p;
        }

        private static Crew NewCrew(Slot s)
        {
            Crew c = new Crew();
            c.Owner = s;
            bool army = s.Kind == KKnight || s.Kind == KArcher;
            c.Max = s.Kind == KIdle ? 15 : (army ? 30 : 8);
            c.Root = BtcGhKit.Make(Layouts.Horizontal, s.Root, "TaskbarProjectCrewPanel.lua @ nil");
            BtcGhKit.Rect(c.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), BtcGhKit.Mid, Vector2.zero, new Vector2(48f, 48f));
            BtcGhKit.Img(c.Root, null, new Color32(0, 0, 0, 192), false);
            BtcGhKit.Le(c.Root, army ? 20f : 48f, 48f, -1f, 48f, -1f, -1f, false);
            BtcGhKit.Group(c.Root, 0, 12, 0, 0, 0f, TextAnchor.MiddleLeft);
            c.Faces = new RectTransform[c.Max];
            c.Imgs = new Image[c.Max];

            // crew button group: hover bg + hover icon over the whole crew area (ignores the layout)
            c.Group = BtcGhKit.Make(Layouts.Horizontal, c.Root, "");
            BtcGhKit.Stretch(c.Group);
            BtcGhKit.Img(c.Group, null, new Color32(0, 0, 0, 192), false);
            BtcGhKit.Le(c.Group, -1f, -1f, -1f, -1f, -1f, -1f, true);
            BtcGhKit.Group(c.Group, 0, 0, 0, 0, 2f, TextAnchor.MiddleLeft);
            RectTransform hb = BtcGhKit.Make(Images.Default, c.Group, "");
            BtcGhKit.Stretch(hb);
            BtcGhKit.Le(hb, 64f, 64f, -1f, -1f, -1f, -1f, true);
            c.HoverBg = BtcGhKit.Img(hb, null, BtcGhButton.FgHover, false);
            c.HoverBg.raycastTarget = false;
            RectTransform hi = BtcGhKit.Make(Images.Default, c.Group, "");
            BtcGhKit.Centre(hi, 32f, 32f);
            BtcGhKit.Le(hi, 64f, 64f, -1f, -1f, -1f, -1f, true);
            c.HoverImg = BtcGhKit.Img(hi, Spr.Select, BtcGhKit.Yellow, false);
            c.HoverImg.raycastTarget = false;
            BtcGhTlPtr p = Pointer(c.Group);
            p.OnEnter = delegate { c.Hovering = true; ModDelegates(true); _modCrew = c; RefreshCrewHover(c); };
            p.OnExit = delegate { c.Hovering = false; if (_modCrew == c) { _modCrew = null; ModDelegates(false); } RefreshCrewHover(c); };
            p.OnReset = delegate { c.Hovering = false; c.TipOn = false; if (_modCrew == c) { _modCrew = null; ModDelegates(false); } };
            p.OnClick = delegate(int mode) { CrewClick(s, mode); };
            return c;
        }

        private static Slot FreeSlot()
        {
            for (int i = 0; i < Pool.Count; i++) if (Pool[i].Proj == null) return Pool[i];
            Slot s = NewSlot(KRegular, 3 + Pool.Count);
            s.Root.SetSiblingIndex(_spacer.GetSiblingIndex()); // before the spacer, after the older pool panels
            Pool.Add(s);
            return s;
        }

        private static void EnsureOperable()
        {
            if (_operable != null) return;
            _operable = NewSlot(KOperable, 2);
            _operable.Root.SetSiblingIndex(1); // right after the idle panel
        }

        /// ObjectRegistry + PanelPooler: a started project takes the first free slot.
        private static void Register(Project p)
        {
            if (p == null || p.IsNullOrReleased() || p.IsBeingPlaced) return;
            if (ByProject.ContainsKey(p)) return;
            PT t = TypeOf(p);
            if (t == PT.None || t == PT.Idle) return; // not in Meta.Project / the allied idle project has its own slot
            Slot s = FreeSlot();
            s.Proj = p; s.Type = t; s.TitleKey = null;
            s.Crew.K[0] = -1;
            ByProject[p] = s;
            RefreshTask(s);
            RefreshCrew(s);
            RefreshPriority(s);
        }


        private static void Release(Project p)
        {
            Slot s = Find(p);
            if (s == null || s.Kind != KRegular) return;
            ByProject.Remove(p);
            s.Proj = null;
            s.Hover = false;
            s.Root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ events
        private static void OnProjectStarted(Project p)
        {
            try
            {
                if (_list == null || p == null || p.IsNullOrReleased() || p.IsBeingPlaced) return;
                Register(p);
            }
            catch (Exception e) { BtcLog.Error("GameHud", "task list started: " + e); }
        }

        private static void OnProjectDeleted(Project p)
        {
            try
            {
                if (_list == null) return;
                Release(p);
                if (p != null && p == _sel)
                {
                    _sel = FindSelected();
                    RefreshSelection(null, Find(_sel));
                    UpdatePopup();
                }
            }
            catch (Exception e) { BtcLog.Error("GameHud", "task list deleted: " + e); }
        }

        private static void OnProjectSelected(Project p)
        {
            try
            {
                if (_list == null) return;
                Project old = _sel;
                _sel = FindSelected();
                RefreshSelection(Find(old), Find(_sel));
                if (old != _sel) UpdatePopup();
                else RefreshArrows();
            }
            catch (Exception e) { BtcLog.Error("GameHud", "task list selected: " + e); }
        }

        private static void OnStationSelected()
        {
            try
            {
                if (_list == null) return;
                CraftingStation st = CurrentStation();
                if (st == _station) return;
                _station = st;
                if (st != null) EnsureOperable();
                if (_operable != null) RefreshTask(_operable);
                RefreshTask(Find(_sel));
                UpdatePopup();
            }
            catch (Exception e) { BtcLog.Error("GameHud", "task list station: " + e); }
        }

        private static void OnIconChanged(Project p)
        {
            try
            {
                if (_list == null) return;
                Slot s = Find(p);
                if (s != null) RefreshTask(s);
            }
            catch (Exception e) { BtcLog.Error("GameHud", "task list icon: " + e); }
        }

        /// Project.hk_OnSetCrewInfo (collapsed by the engine to once per frame): army panels and the idle panel always, plus the named projects.
        private static void OnCrewInfo(List<Project> list)
        {
            try
            {
                if (_list == null) return;
                ComputeArmy();
                RefreshCrew(_idle); RefreshCrew(_knight); RefreshCrew(_archer);
                if (list == null) return;
                for (int i = 0; i < list.Count; i++)
                {
                    Slot s = Find(list[i]);
                    if (s == null || s.Kind != KRegular) continue;
                    RefreshPriority(s);
                    RefreshCrew(s);
                }
            }
            catch (Exception e) { BtcLog.Error("GameHud", "task list crew: " + e); }
        }

        private static void OnCrewOne(Project p)
        {
            try
            {
                if (_list == null) return;
                ComputeArmy();
                RefreshCrew(_idle); RefreshCrew(_knight); RefreshCrew(_archer);
                Slot s = Find(p);
                if (s != null && s.Kind == KRegular) { RefreshPriority(s); RefreshCrew(s); }
            }
            catch (Exception e) { BtcLog.Error("GameHud", "task list crew: " + e); }
        }

        /// OnSelectProject: refresh source and destination panels (+ the operable panel while a station is selected).
        private static void RefreshSelection(Slot a, Slot b)
        {
            RefreshTask(a);
            if (b != a) RefreshTask(b);
            if (_station != null && _operable != null) RefreshTask(_operable);
        }

        private static void UpdatePopup()
        {
            RectTransform r = SelectedPanelRect;
            if (r != null) { if (PopupSet != null) PopupSet(r); }
            else if (PopupReset != null) PopupReset();
            RefreshArrows();
        }

        /// A terraform shape got bound to its Flatten task (after the slot was built): new title and icon.
        internal static void RefreshProject(Project p)
        {
            if (_list == null) return;
            try { RefreshTask(Find(p)); }
            catch (Exception e) { BtcLog.Error("GameHud", "task list refresh: " + e.Message); }
        }

        /// Popup / tooltip visibility changed: re-evaluate the popup arrows.
        internal static void RefreshArrows()
        {
            if (_list == null) return;
            bool pv = PopupVisible != null && PopupVisible();
            RefreshPopupArrow(_idle, pv); RefreshPopupArrow(_operable, pv);
            for (int i = 0; i < Pool.Count; i++) RefreshPopupArrow(Pool[i], pv);
        }

        private static void RefreshPopupArrow(Slot s, bool popupVisible)
        {
            if (s == null) return;
            bool show = popupVisible && Exists(s) && IsSelected(s);
            if (show && s.PopArrow == null) s.PopArrow = MakeArrow(s.Root, new Vector2(0.5f, 0f), new Vector2(0f, 52f));
            if (s.PopArrow != null) SetActive(s.PopArrow.gameObject, show);
        }

        private static Image MakeArrow(Transform parent, Vector2 anchor, Vector2 pos)
        {
            RectTransform a = BtcGhKit.Make(Images.Default, parent, "");
            BtcGhKit.Rect(a, anchor, anchor, new Vector2(0.5f, 0f), pos, new Vector2(64f, 64f));
            BtcGhKit.Le(a, 64f, 64f, -1f, -1f, -1f, -1f, true);
            Image im = BtcGhKit.Img(a, Spr.Arrow, ArrowColor, true);
            im.raycastTarget = false;
            a.SetAsFirstSibling();
            return im;
        }

        // ------------------------------------------------------------------ refresh (Lua ev_refresh of one task panel)
        private static void RefreshAllSlots()
        {
            RefreshTask(_idle); RefreshTask(_operable); RefreshTask(_knight); RefreshTask(_archer);
            for (int i = 0; i < Pool.Count; i++) RefreshTask(Pool[i]);
            RefreshCrew(_idle); RefreshCrew(_knight); RefreshCrew(_archer);
            for (int i = 0; i < Pool.Count; i++) if (Pool[i].Proj != null) { RefreshCrew(Pool[i]); RefreshPriority(Pool[i]); }
        }

        private static void SetActive(GameObject go, bool on)
        {
            if (go.activeSelf != on) go.SetActive(on);
        }

        private static void RefreshTask(Slot s)
        {
            if (s == null) return;
            bool ex = Exists(s);
            SetActive(s.Root.gameObject, ex);
            if (!ex) return;
            if (s.Kind == KIdle && s.Proj == null) { s.Proj = IdleProject.GetAlliedProject(); if (s.Proj != null) ByProject[s.Proj] = s; }

            string key; Sprite sp; Color col;
            Look(s, out key, out sp, out col);
            if (s.Icon.sprite != sp) s.Icon.sprite = sp;
            s.Icon.enabled = sp != null;
            s.Icon.color = col;
            if (s.Title != null && s.TitleKey != key)
            {
                s.TitleKey = key;
                s.Title.text = key != null && key.StartsWith("##") ? BtcGameHud.Loc(key) : (key ?? "");
            }
            bool sel = IsSelected(s);
            if (s.HoverBg != null)
            {
                s.HoverBg.enabled = s.Hover;
                s.HoverImg.enabled = s.Hover;
                Sprite hs = sel ? Spr.Down : Spr.Up;
                if (s.HoverImg.sprite != hs) s.HoverImg.sprite = hs;
            }
            s.Ptr.SetHighlight(sel ? 15 : 0);
            bool tip = s.Hover && TooltipVisible != null && TooltipVisible();
            if (tip && s.TipArrow == null) s.TipArrow = MakeArrow(s.Root, new Vector2(0f, 0f), new Vector2(28f, 52f));
            if (s.TipArrow != null) SetActive(s.TipArrow.gameObject, tip);
            RefreshPopupArrow(s, PopupVisible != null && PopupVisible());
        }

        /// _data.GetProjectName + _IconSprite + _IconColor.
        private static void Look(Slot s, out string key, out Sprite sp, out Color col)
        {
            col = Color.white;
            switch (s.Kind)
            {
                case KKnight: key = "##gamemenu_taskbar_meleeunits"; sp = Spr.Sword; col = BtcGhKit.Yellow; return;
                case KArcher: key = "##gamemenu_taskbar_rangedunits"; sp = Spr.Bow; col = BtcGhKit.Yellow; return;
                case KOperable: StationLook(_station, out key, out sp); return;
                case KIdle:
                    key = "##task_idle"; sp = Spr.Crystal;
                    if (Faction.DefaultAllied != null) col = Faction.DefaultAllied.FactionColor;
                    return;
            }
            switch (s.Type)
            {
                case PT.Build: key = "##task_build"; sp = Spr.Build; return;
                case PT.Quarry: key = "##task_quarry"; sp = Spr.Quarry; return;
                case PT.Cleanup: key = "##task_cleanup"; sp = Spr.Cleanup; return;
                case PT.Repair:
                    BtcTrapTask.Info ti = BtcTrapTask.Of(s.Proj);
                    if (ti != null && BtcTrapTask.Icon != null) { key = BtcTrapTask.Title(ti); sp = BtcTrapTask.Icon; return; }
                    key = "##task_repair"; sp = Spr.Repair; return;
                case PT.Landscape:
                    if (BtcTerraform.Of(s.Proj) != null) { key = BtcLang.TrG("Terraform"); sp = BtcTerraformIcon.Get(); return; }
                    key = "##task_landscape"; sp = Spr.Landscape; return;
                case PT.Tunnel: key = "##task_tunnel"; sp = Spr.Tunnel; return;
                case PT.Harvest:
                    key = "##task_harvest"; sp = HarvestIcon(s.Proj as HarvestProject);
                    col = sp == Spr.Harvest ? BtcGhKit.Yellow : Color.white; // nature icons are white, the generic pick is yellow
                    return;
            }
            key = ""; sp = Spr.Help;
        }

        /// Data.Project:GetHarvestIcon: exactly one nature type accepted -> its icon, else the "all" icon.
        private static Sprite HarvestIcon(HarvestProject h)
        {
            if (h == null) return Spr.Harvest;
            NatureType a = h.AcceptedTypes;
            int n = 0; Sprite one = null;
            if ((a & NatureType.boulder) != 0) { n++; one = Spr.HStone; }
            if ((a & NatureType.iron) != 0) { n++; one = Spr.HIron; }
            if ((a & NatureType.pyrite) != 0) { n++; one = Spr.HBrim; }
            if ((a & NatureType.tree) != 0) { n++; one = Spr.HWood; }
            if ((a & NatureType.plant) != 0) { n++; one = Spr.HPlants; }
            return n == 1 ? one : Spr.HAll;
        }

        /// Meta.Operable name + icon of the selected crafting station.
        private static void StationLook(CraftingStation st, out string key, out Sprite sp)
        {
            string n = st != null ? st.GetType().Name : "";
            switch (n)
            {
                case "Furnace": key = "##operable_furnace"; sp = Spr.Furnace; return;
                case "ChoppingBlock": key = "##operable_choppingblock"; sp = Spr.Chopping; return;
                case "Loom": key = "##operable_loom"; sp = Spr.Loom; return;
                case "Workbench": key = "##operable_workbench"; sp = Spr.Workbench; return;
                case "Forge": key = "##operable_forge"; sp = Spr.Forge; return;
                case "MachineShop": key = "##operable_machineshop"; sp = Spr.Machine; return;
                case "Lab": key = "##operable_laboratory"; sp = Spr.Lab; return;
                case "Altar": key = "##operable_altar"; sp = Spr.Altar; return;
            }
            key = n; sp = Spr.Help;
        }

        private static void RefreshPriority(Slot s)
        {
            if (s == null || s.Prio == null || s.Proj == null || s.Proj.IsNullOrReleased()) return;
            Sprite sp = null;
            switch (s.Proj.GetPriority())
            {
                case ProjectPriority.Bias.Low: sp = Spr.PLow; break;
                case ProjectPriority.Bias.Medium: sp = Spr.PMed; break;
                case ProjectPriority.Bias.High: sp = Spr.PHigh; break;
                case ProjectPriority.Bias.Locked: sp = Spr.PLocked; break;
            }
            if (s.Prio.sprite != sp) s.Prio.sprite = sp;
            s.Prio.enabled = sp != null;
        }

        // ------------------------------------------------------------------ crew faces (TaskbarProjectCrewPanel.lua)
        private static void Seg(Sprite sp, int n)
        {
            if (n <= 0 || _segs >= SegN.Length) return;
            SegSp[_segs] = sp; SegN[_segs] = n; _segs++;
        }

        private static void RefreshCrew(Slot s)
        {
            if (s == null) return;
            Crew c = s.Crew;
            int shown;
            _segs = 0;
            switch (s.Kind)
            {
                case KOperable:
                    return; // Lua asks Data.Project for the crew of the station and has nothing to show
                case KIdle:
                {
                    int all, sel;
                    Builders(s.Proj, out all, out sel);
                    if (!Changed(c, all, sel, _total, 0, 0)) return;
                    shown = _total;
                    Seg(Spr.WorkerSel, sel); Seg(Spr.Worker, all - sel); Seg(Spr.Empty, _total - all);
                    break;
                }
                case KKnight:
                {
                    int hc = JobCount[(int)Occupation.Job.Halberdier], hs = JobSel[(int)Occupation.Job.Halberdier];
                    int kc = JobCount[(int)Occupation.Job.Knight], ks = JobSel[(int)Occupation.Job.Knight];
                    if (!Changed(c, hc, hs, kc, ks, _melee)) return;
                    shown = _melee;
                    Seg(Spr.HalSel, hs); Seg(Spr.Hal, hc - hs); Seg(Spr.KnightSel, ks); Seg(Spr.Knight, kc - ks);
                    break;
                }
                case KArcher:
                {
                    int a = (int)Occupation.Job.Archer, b = (int)Occupation.Job.Arbalist, r = (int)Occupation.Job.Artificer, m = (int)Occupation.Job.Alchemist;
                    int h = JobCount[a] * 31 + JobSel[a];
                    int h2 = JobCount[b] * 31 + JobSel[b];
                    int h3 = JobCount[r] * 31 + JobSel[r];
                    int h4 = JobCount[m] * 31 + JobSel[m];
                    if (!Changed(c, h, h2, h3, h4, _ranged)) return;
                    shown = _ranged;
                    Seg(Spr.ArcherSel, JobSel[a]); Seg(Spr.Archer, JobCount[a] - JobSel[a]);
                    Seg(Spr.ArbalistSel, JobSel[b]); Seg(Spr.Arbalist, JobCount[b] - JobSel[b]);
                    Seg(Spr.ArtificerSel, JobSel[r]); Seg(Spr.Artificer, JobCount[r] - JobSel[r]);
                    break; // the alchemist pair is appended after the switch
                }
                default:
                {
                    if (s.Proj == null) return;
                    int all, sel;
                    Builders(s.Proj, out all, out sel);
                    if (!Changed(c, all, sel, 0, 0, 0)) return;
                    shown = all;
                    Seg(Spr.WorkerSel, sel); Seg(Spr.Worker, all - sel);
                    break;
                }
            }
            if (s.Kind == KArcher)
            {
                int m = (int)Occupation.Job.Alchemist;
                Seg(Spr.AlchemistSel, JobSel[m]); Seg(Spr.Alchemist, JobCount[m] - JobSel[m]);
            }
            ApplyFaces(c, shown);
        }

        private static bool Changed(Crew c, int a, int b, int d, int e, int f)
        {
            int[] k = c.K;
            if (k[0] == a && k[1] == b && k[2] == d && k[3] == e && k[4] == f) return false;
            k[0] = a; k[1] = b; k[2] = d; k[3] = e; k[4] = f;
            return true;
        }

        private static void ApplyFaces(Crew c, int shown)
        {
            if (shown > c.Max) shown = c.Max;
            if (shown < 0) shown = 0;
            for (int i = 0; i < shown; i++)
            {
                if (c.Faces[i] == null) CreateFace(c, i);
            }
            int seg = 0, left = _segs > 0 ? SegN[0] : 0;
            for (int i = 0; i < shown; i++)
            {
                while (seg < _segs && left <= 0) { seg++; left = seg < _segs ? SegN[seg] : 0; }
                Sprite sp = null;
                if (seg < _segs) { sp = SegSp[seg]; left--; }
                SetActive(c.Faces[i].gameObject, true);
                Image im = c.Imgs[i];
                if (sp != null) { if (im.sprite != sp) im.sprite = sp; if (!im.enabled) im.enabled = true; }
                else if (im.enabled) im.enabled = false;
            }
            for (int i = shown; i < c.Shown; i++) if (c.Faces[i] != null) SetActive(c.Faces[i].gameObject, false);
            c.Shown = shown;
        }

        /// crewPanels[id]: Horizontal 10x10 with a 32x32 face that alternates up/down (pos 4, +-10) so faces overlap like a fan.
        private static void CreateFace(Crew c, int i)
        {
            int id = i + 1;
            RectTransform lyt = BtcGhKit.Make(Layouts.Horizontal, c.Root, "TaskbarProjectCrewPanel.lua @ _p.crewPanels[" + id + "]");
            BtcGhKit.Rect(lyt, new Vector2(0f, 1f), new Vector2(0f, 1f), BtcGhKit.Mid, Vector2.zero, new Vector2(10f, 10f));
            BtcGhKit.Img(lyt, null, new Color32(0, 0, 0, 192), false);
            BtcGhKit.Le(lyt, -1f, -1f, 10f, 10f, -1f, -1f, false);
            BtcGhKit.Group(lyt, 0, 0, 0, 0, 0f, TextAnchor.UpperLeft);
            RectTransform f = BtcGhKit.Make(Images.Default, lyt, "");
            BtcGhKit.Rect(f, BtcGhKit.Mid, BtcGhKit.Mid, BtcGhKit.Mid, new Vector2(4f, id % 2 == 0 ? -10f : 10f), new Vector2(32f, 32f));
            BtcGhKit.Le(f, 64f, 64f, -1f, -1f, -1f, -1f, true);
            c.Imgs[i] = BtcGhKit.Img(f, null, Color.white, true);
            c.Faces[i] = lyt;
            lyt.SetSiblingIndex(i); // faces stay before the crew button group
        }

        // ------------------------------------------------------------------ hover + modifier keys
        private static bool Ctrl() { return PickingUtility.Player.GetButton("cam_mouse_sec"); }
        private static bool Shift() { return PickingUtility.Player.GetButton("cam_mouse_alt"); }

        private static readonly Action<Rewired.InputActionEventData> ModCb = OnMod;
        private static readonly string[] ModNames = { "cam_mouse_alt", "cam_mouse_sec" };

        private static void OnMod(Rewired.InputActionEventData d)
        {
            try { if (_modCrew != null && _list != null) RefreshCrewHover(_modCrew); }
            catch (Exception e) { BtcLog.Error("GameHud", "task list mod: " + e); }
        }

        /// Shift/Ctrl change the crew hover icon and tooltip; only listened to while a crew button is hovered.
        private static void ModDelegates(bool on)
        {
            if (on == _modsOn) return;
            try
            {
                Rewired.Player p = PickingUtility.Player;
                if (p == null) return;
                _modsOn = on;
                for (int i = 0; i < ModNames.Length; i++)
                {
                    if (on)
                    {
                        p.AddInputEventDelegate(ModCb, Rewired.UpdateLoopType.Update, Rewired.InputActionEventType.ButtonJustPressed, ModNames[i]);
                        p.AddInputEventDelegate(ModCb, Rewired.UpdateLoopType.Update, Rewired.InputActionEventType.ButtonJustReleased, ModNames[i]);
                    }
                    else
                    {
                        p.RemoveInputEventDelegate(ModCb, Rewired.UpdateLoopType.Update, Rewired.InputActionEventType.ButtonJustPressed, ModNames[i]);
                        p.RemoveInputEventDelegate(ModCb, Rewired.UpdateLoopType.Update, Rewired.InputActionEventType.ButtonJustReleased, ModNames[i]);
                    }
                }
            }
            catch (Exception e) { BtcLog.Error("GameHud", "task list modifiers: " + e); }
        }

        private static bool Regular(Slot s) { return s.Kind == KRegular; }

        /// ev_refreshHoverPanels: tooltip path, tooltip arrow, hover bg, hover icon.
        private static void RefreshCrewHover(Crew c)
        {
            if (_list == null) return;
            Slot s = c.Owner;
            bool h = c.Hovering;
            c.HoverBg.enabled = h;
            c.HoverImg.enabled = h;
            string[] path = null;
            if (h)
            {
                bool ctrl = Ctrl(), shift = Shift();
                Sprite hs = Spr.Select;
                if (Regular(s)) { if (ctrl) hs = Spr.Minus; else if (shift) hs = Spr.Plus; }
                if (c.HoverImg.sprite != hs) c.HoverImg.sprite = hs;
                path = ctrl ? TipLower : (shift ? TipRaise : TipSelectCrew);
            }
            if (path != null)
            {
                if (TooltipSet != null) TooltipSet(c.Group, path);
                c.TipOn = true;
            }
            else if (c.TipOn)
            {
                c.TipOn = false;
                if (TooltipReset != null) TooltipReset();
            }
            bool arrow = h && TooltipVisible != null && TooltipVisible();
            if (arrow && c.Arrow == null) c.Arrow = MakeArrow(c.Group, new Vector2(0.5f, 0f), new Vector2(0f, 50f));
            if (c.Arrow != null) SetActive(c.Arrow.gameObject, arrow);
        }

        /// _SetHoverOn: tooltip Select/Deselect (not for the army panels), hover state, refresh.
        private static void HoverOn(Slot s)
        {
            try
            {
                if (_list == null) return;
                if (s.Kind == KIdle && _sel is IdleProject) return; // already selected: nothing to toggle
                if (s.Kind != KKnight && s.Kind != KArcher && TooltipSet != null) TooltipSet(s.TaskLyt, IsSelected(s) ? TipDeselect : TipSelect);
                s.Hover = true;
                RefreshTask(s);
            }
            catch (Exception e) { BtcLog.Error("GameHud", "task list hover: " + e); }
        }

        private static void HoverOff(Slot s)
        {
            try
            {
                if (_list == null) return;
                if (TooltipReset != null) TooltipReset();
                s.Hover = false;
                RefreshTask(s);
            }
            catch (Exception e) { BtcLog.Error("GameHud", "task list hover: " + e); }
        }

        // ------------------------------------------------------------------ actions
        /// _DoToggleSelectTask (Data.Project:ToggleSelection).
        private static void ToggleSelect(Slot s)
        {
            try
            {
                if (s.Kind == KOperable) { UIGameActor.SelectIdleProject(); return; } // Project.Deselect
                if (s.Kind == KKnight || s.Kind == KArcher) return;
                Project dst = s.Proj;
                if (dst == null || dst.IsNullOrReleased()) return;
                Project src = _sel;
                if (dst == src) UIGameActor.SelectIdleProject();
                else UIGameSelector.Select(dst.gameObject);
                if (s.Kind == KIdle && _station != null) UIGameSelector.UnselectAll(SelectionType.Crafting, null);
                if (src != dst)
                {
                    if (TooltipReset != null) TooltipReset(); // ForceResetTaskTooltipMenu
                    RefreshTask(s);
                }
            }
            catch (Exception e) { BtcLog.Error("GameHud", "task list select: " + e); }
        }

        /// TaskbarProjectCrewPanel OnAction / OnAltAction / OnShiftAction / OnCtrlAction (mode: 0 click, 1 shift, 2 ctrl, 3 right).
        private static void CrewClick(Slot s, int mode)
        {
            try
            {
                if (_list == null || s.Kind == KOperable) return;
                Project p = s.Proj;
                bool proj = p != null && !p.IsNullOrReleased();
                switch (mode)
                {
                    case BtcGhTlPtr.Plain:
                        if (s.Kind == KKnight) { UIGameActor.SelectIdleProject(); UIGameActor.SelectByOccupation(Occupation.Type.Knight, Faction.DefaultAllied, false); }
                        else if (s.Kind == KArcher) { UIGameActor.SelectIdleProject(); UIGameActor.SelectByOccupation(Occupation.Type.Archer, Faction.DefaultAllied, false); }
                        else if (proj) { UIGameActor.SelectIdleProject(); UIGameActor.SelectProjectMember(p, true, true, false); }
                        break;
                    case BtcGhTlPtr.Right:
                        if (s.Kind == KIdle) UIGameActor.MakeSelectionFreeAgents();
                        else if (s.Kind == KRegular && proj) UIGameActor.AssignSelectionToProject(p);
                        break;
                    case BtcGhTlPtr.Shift:
                        if (s.Kind == KIdle) { if (proj) { UIGameActor.SelectIdleProject(); UIGameActor.SelectProjectMember(p, true, false, false); } }
                        else if (s.Kind == KRegular && proj) { User.LocalUser.GetComponent<UNetGroupe>().CallCmdRaisePriority(p.gameObject); RefreshCrewHover(s.Crew); }
                        break;
                    case BtcGhTlPtr.Ctrl:
                        if (s.Kind == KRegular && proj) { User.LocalUser.GetComponent<UNetGroupe>().CallCmdLowerPriority(p.gameObject); RefreshCrewHover(s.Crew); }
                        break;
                }
            }
            catch (Exception e) { BtcLog.Error("GameHud", "task list crew click: " + e); }
        }
    }

    /// Pointer behaviour of the raw Buttons.Standard widgets in the task list: hover enter/exit, left click (shift / ctrl variants like
    /// ButtonWidgetComponent), right click, frame highlight. No sounds (the Lua task list has none). OnReset runs when the button is
    /// disabled while hovered (the widget would send a pointer exit) and must not touch the UI.
    internal sealed class BtcGhTlPtr : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        internal const int Plain = 0, Shift = 1, Ctrl = 2, Right = 3;
        internal Action OnEnter, OnExit, OnReset;
        internal Action<int> OnClick;
        internal FrameAnimatorWidgetComponent Frame;
        private bool _hover;
        private int _hl = -1;

        public void OnPointerEnter(PointerEventData e) { _hover = true; if (OnEnter != null) OnEnter(); }
        public void OnPointerExit(PointerEventData e) { _hover = false; if (OnExit != null) OnExit(); }

        public void OnPointerClick(PointerEventData e)
        {
            if (OnClick == null) return;
            if (e.button == PointerEventData.InputButton.Left)
            {
                bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
                bool ctrl = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
                OnClick(shift ? Shift : (ctrl ? Ctrl : Plain));
            }
            else if (e.button == PointerEventData.InputButton.Right) OnClick(Right);
        }

        private void OnDisable()
        {
            if (_hover) { _hover = false; if (OnReset != null) OnReset(); }
        }

        private void OnEnable() { _hl = -1; }

        internal void SetHighlight(int flag)
        {
            if (flag == _hl) return;
            _hl = flag;
            BtcGhKit.Highlight(Frame, flag);
        }
    }
}
