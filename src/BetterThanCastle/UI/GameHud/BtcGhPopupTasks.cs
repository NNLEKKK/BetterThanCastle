using System;
using Brix.Engine.Nature;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Compounders;
using Brix.Game.Components;
using Brix.Game.Components.Tunnel;
using Brix.Game.Network;
using Brix.Game.Rules;
using Brix.Input;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace BetterThanCastle
{
    /// Task sections of the popup: goals, build (blueprint tools), landscape, quarry, tunnel, harvest, cleanup filter (BTC), priority,
    /// task actions, idle population + crystal. Source lines in GameMenu.lua: goals 2603, build 945-1134, landscape 1139-1200, quarry
    /// 1205-1316, tunnel 1319-1469, harvest 1477-1625, priority 2838-2908, actions 2913-3023, idle 2657-2835.
    internal static partial class BtcGhPopup
    {
        private static readonly string[] Nums = MakeNums();
        private static string[] MakeNums() { string[] a = new string[100]; for (int i = 0; i < a.Length; i++) a[i] = i.ToString(); return a; }
        private static string Num(int n) { return n >= 0 && n < Nums.Length ? Nums[n] : n.ToString(); }

        /// The AddDivider / AddSection chain (GameMenu.lua 3965-4003), BTC cleanup filter after harvest, built lazily.
        private static void DefineItems()
        {
            Items.Clear();
            Items.Add(GoalsSec());
            Items.Add(new Div(M_SEL | M_INPUT, IsBuildSel, 8f)); Items.Add(BuildSec());
            Items.Add(new Div(M_SEL, IsLandSel, 8f)); Items.Add(LandSec());
            Items.Add(new Div(M_SEL, TerraShown, 8f)); Items.Add(TerraModeSec()); // BTC terraform task
            Items.Add(new Div(M_SEL, TerraShown, 8f)); Items.Add(TerraKeepSec());
            Items.Add(new Div(M_SEL | M_KEPT, IsQuarrySel, 8f)); Items.Add(QuarrySec());
            Items.Add(new Div(M_SEL | M_TUNNEL | M_KEPT, IsTunnelSel, 8f)); Items.Add(TunnelSec());
            Items.Add(new Div(M_SEL | M_INPUT, IsHarvestSel, 8f)); Items.Add(HarvestSec());
            Items.Add(new Div(M_SEL | M_CLEAN, IsCleanSel, 8f)); Items.Add(CleanSec()); // BtcCleanFilter.Inject twin
            DefineOperableItems();
            Items.Add(new Div(M_SEL, NotIdle, 8f)); Items.Add(PrioritySec());
            Items.Add(new Div(M_SEL, NotIdle, 8f)); Items.Add(ActionsSec());
            Items.Add(IdlePopSec());
            Items.Add(new Div(M_SEL | M_OPSEL, IsIdleNoOp, 8f));
            Items.Add(IdleActionsSec());
        }

        private static void AssignKeyActions()
        {
            BtcGhModes.RaisePriority = ActRaise;
            BtcGhModes.LowerPriority = ActLower;
            BtcGhModes.DeleteTask = ActDelete;
            BtcGhModes.ToggleLoop = ActLoop;
            BtcGhModes.ClearQueue = ActClear;
            BtcGhModes.BlueprintMgmt = ActBlueprintMgmt;
            BtcGhModes.QueueSlot = ActQueueSlot;
            BtcGhModes.TaskActionSlot = ActTaskSlot;
        }

        // ------------------------------------------------------------------ goals
        private static Sec GoalsSec()
        {
            return new Sec(M_SEL | M_GOALS, NotIdle, "##task_goals", delegate(Sec s)
            {
                s.Add(new Btn { Visible = IsNotBuildNotIdle, IconName = "UI_Flag", IconSize = 32f, CountFn = GoalCount, OnAction = NoOp });
                s.Add(new Btn { Visible = IsBuildNotIdle, IconName = "_UI_Brick", IconSize = 32f, CountFn = GoalCount, OnAction = NoOp });
            });
        }

        private static string GoalCount() { return Num(NotIdle() ? _sel.GoalCounts() : 0); }

        // ------------------------------------------------------------------ build: blueprint management tools
        private static Sec BuildSec()
        {
            return new Sec(M_SEL | M_INPUT, IsBuildSel, null, delegate(Sec s)
            {
                s.Add(new Btn { Visible = IsBuildSel, IconName = "SelectBlueprints", IconSize = 32f, IconColorFn = BlueprintsColor, OnAction = ActBlueprintMgmt });
                s.Add(BpTool("Blueprints_Move", null, ActBpMove));
                s.Add(BpTool("CopyBlueprints", "tb_constr_blueprints_duplicate", ActBpCopy));
                s.Add(BpTool("SplitBlueprints", null, ActBpSplit));
                s.Add(BpTool("DeleteBlueprints", "tb_constr_blueprints_delete", ActBpDelete));
                s.Add(new InnerDiv(BuildSelecting));
                s.Add(BpTool("ExpandSelection", "tb_constr_blueprints_expand", ActBpExpand));
                s.Add(BpTool("ClearSelection", "tb_constr_blueprints_clear", ActBpClear));
            });
        }

        private static Btn BpTool(string icon, string hotkeyAction, Action act)
        {
            Btn b = new Btn { Visible = BuildSelecting, IconName = icon, IconSize = 32f, IconColor = BtcGhKit.Red, OnAction = act };
            if (hotkeyAction != null) { b.HasHotkey = true; b.Hotkey = Key("tb_constr_blueprints_menu", hotkeyAction); }
            return b;
        }

        private static Color BlueprintsColor() { return Selecting() ? BtcGhKit.Red : BtcGhKit.Gray; }

        internal static void ActBlueprintMgmt()
        {
            if (!IsBuildSel()) return;
            Picking.lua_SetIsSelectingBlueprints(default(ScriptExecutionContext), BtcGhLua.Args(!InputModeController.IsSelectingBlueprints()));
            Refresh(M_INPUT);
        }

        private static void ActBpMove() { if (!Paused()) BlueprintCompounder.Move(); }
        private static void ActBpCopy() { if (!Paused()) BlueprintCompounder.Duplicate(); }
        private static void ActBpSplit() { if (!Paused()) Brix.UI.UIGameActor.SplitSelectedBlueprints(); }
        private static void ActBpDelete() { if (!Paused()) Brix.UI.UIGameActor.DeleteSelectedBlueprints(); }
        private static void ActBpExpand() { if (!Paused()) BlueprintCompounder.Expand(); }
        private static void ActBpClear() { if (!Paused()) BlueprintCompounder.Cancel(); }

        // ------------------------------------------------------------------ landscape
        private static Sec LandSec()
        {
            return new Sec(M_SEL, IsLandSel, null, delegate(Sec s)
            {
                s.Add(new Btn { Visible = IsLandSel, IconName = "UI_RaiseBrush", IconSize = 32f, HasHotkey = true, Hotkey = Key("Projects", "project_TaskActionSlot1"), OnAction = ActLandRaise });
                s.Add(new Btn { Visible = IsLandSel, IconName = "UI_TerrainHug", IconSize = 32f, HasHotkey = true, Hotkey = Key("Projects", "project_TaskActionSlot2"), OnAction = ActLandDig });
            });
        }

        private static Brix.Game.AI.UI.LandscapeProjectUI LandUi() { return _sel.GetComponentInChildren<Brix.Game.AI.UI.LandscapeProjectUI>(true); }
        private static void ActLandRaise() { if (!Paused() && IsLandSel()) { Brix.Game.AI.UI.LandscapeProjectUI u = LandUi(); if (u != null) u.SetMaxThreshold(); } }
        private static void ActLandDig() { if (!Paused() && IsLandSel()) { Brix.Game.AI.UI.LandscapeProjectUI u = LandUi(); if (u != null) u.SetMinThreshold(); } }

        // ------------------------------------------------------------------ quarry (MineProject) and tunnel keep-resource toggles
        private const int FlagStone = 1, FlagBrimstone = 2, FlagIron = 4;

        private static Sec QuarrySec()
        {
            return new Sec(M_SEL | M_KEPT, IsQuarrySel, null, delegate(Sec s)
            {
                s.Add(new Btn { Visible = IsQuarrySel, IconName = "UI_StairsDown", IconSize = 32f, HasHotkey = true, Hotkey = Key("Projects", "project_TaskActionSlot1"), OnAction = ActQuarryStairs });
                s.Add(Keep(IsQuarrySel, "_Boulder", FlagStone, 2, ActQuarryKeepStone));
                s.Add(Keep(IsQuarrySel, "_Raw_Iron", FlagIron, 3, ActQuarryKeepIron));
                s.Add(Keep(IsQuarrySel, "_Raw_Orange_Crystal", FlagBrimstone, 4, ActQuarryKeepBrimstone)); // GameMenu.lua order: slot 4
            });
        }

        private static Sec TunnelSec()
        {
            return new Sec(M_SEL | M_TUNNEL | M_KEPT, IsTunnelSel, "##task_tunnel_options", delegate(Sec s)
            {
                s.Add(TunnelDir("UI_StairsUp", TunnelType.stairsUp, 1, ActTunnelUp));
                s.Add(TunnelDir("UI_StraightRight", TunnelType.straight, 2, ActTunnelStraight));
                s.Add(TunnelDir("UI_StairsDown", TunnelType.stairsDown, 3, ActTunnelDown));
                s.Add(Keep(IsTunnelSel, "_Boulder", FlagStone, 4, ActTunnelKeepStone));
                s.Add(Keep(IsTunnelSel, "_Raw_Iron", FlagIron, 5, ActTunnelKeepIron));
                s.Add(Keep(IsTunnelSel, "_Raw_Orange_Crystal", FlagBrimstone, 6, ActTunnelKeepBrimstone));
            });
        }

        private static Btn Keep(Func<bool> vis, string icon, int flag, int slot, Action act)
        {
            return new Btn
            {
                Visible = vis, IconName = icon, IconColor = Color.white, IconSize = 64f,
                HasHotkey = true, Hotkey = Key("Projects", "project_TaskActionSlot" + slot), OnAction = act,
                Highlight = delegate { return KeptHas(flag) ? 15 : 0; },
            };
        }

        private static Btn TunnelDir(string icon, TunnelType type, int slot, Action act)
        {
            return new Btn
            {
                Visible = IsTunnelSel, IconName = icon, IconSize = 32f,
                HasHotkey = true, Hotkey = Key("Projects", "project_TaskActionSlot" + slot), OnAction = act,
                Highlight = delegate { return IsTunnelSel() && ((TunnelProject)_sel).tunnelVolume.GetExpansionType() == type ? 15 : 0; },
            };
        }

        private static bool KeptHas(int flag)
        {
            if (IsQuarrySel()) return ((int)((MineProject)_sel).TypesToHarvest & flag) == flag;
            if (IsTunnelSel()) return ((int)((TunnelProject)_sel).TypesToHarvest & flag) == flag;
            return false;
        }

        private static UNetProjectCmd ProjectCmd() { return User.LocalUser.GetComponent<UNetProjectCmd>(); }

        private static void ActQuarryStairs()
        {
            if (Paused() || !IsQuarrySel()) return;
            Brix.Input.QuarryProjectUI ui = _sel.GetComponentInChildren<Brix.Input.QuarryProjectUI>(true);
            if (ui != null) ui.NextStairs();
        }

        private static void QuarryKeep(int flag) { if (!Paused() && IsQuarrySel()) ProjectCmd().CallCmdQuarryToggleResource(_sel.gameObject, flag); }
        private static void TunnelKeep(int flag) { if (!Paused() && IsTunnelSel()) ProjectCmd().CallCmdTunnelToggleResource(_sel.gameObject, flag); }
        private static void ActQuarryKeepStone() { QuarryKeep(FlagStone); }
        private static void ActQuarryKeepIron() { QuarryKeep(FlagIron); }
        private static void ActQuarryKeepBrimstone() { QuarryKeep(FlagBrimstone); }
        private static void ActTunnelKeepStone() { TunnelKeep(FlagStone); }
        private static void ActTunnelKeepIron() { TunnelKeep(FlagIron); }
        private static void ActTunnelKeepBrimstone() { TunnelKeep(FlagBrimstone); }

        private static void SetTunnel(TunnelType t) { if (!Paused() && IsTunnelSel()) ((TunnelProject)_sel).tunnelVolume.SetExpansionType(t); }
        private static void ActTunnelUp() { SetTunnel(TunnelType.stairsUp); }
        private static void ActTunnelStraight() { SetTunnel(TunnelType.straight); }
        private static void ActTunnelDown() { SetTunnel(TunnelType.stairsDown); }

        // ------------------------------------------------------------------ harvest
        private static Sec HarvestSec()
        {
            return new Sec(M_SEL | M_INPUT, IsHarvestSel, "##task_harvest_options", delegate(Sec s)
            {
                s.Add(Nature("_Boulder", NatureType.boulder, 1));
                s.Add(Nature("_Raw_Iron", NatureType.iron, 2));
                s.Add(Nature("_Raw_Orange_Crystal", NatureType.pyrite, 3));
                s.Add(Nature("_Pine_Tree", NatureType.tree, 4));
                s.Add(Nature("_Plant", NatureType.plant, 5));
            });
        }

        private static Btn Nature(string icon, NatureType type, int slot)
        {
            return new Btn
            {
                Visible = IsHarvestSel, IconName = icon, IconColor = Color.white, IconSize = 64f,
                HasHotkey = true, Hotkey = Key("Projects", "project_TaskActionSlot" + slot),
                OnAction = delegate { ActHarvest(type); },
                Highlight = delegate { return NatureHas(type) ? 15 : 0; },
            };
        }

        private static bool NatureHas(NatureType t)
        {
            return IsHarvestSel() && (((int)((HarvestProject)_sel).data.AcceptedTypes) & (int)t) == (int)t;
        }

        private static void ActHarvest(NatureType t)
        {
            if (!Paused() && IsHarvestSel()) ProjectCmd().CallCmdHarvestToggleResource(_sel.gameObject, (int)t);
        }

        // ------------------------------------------------------------------ cleanup filter (BtcCleanFilter.Inject: dividers + 11 toggle buttons, BC ON only has an effect)
        private static Sec CleanSec()
        {
            return new Sec(M_SEL | M_CLEAN, IsCleanSel, null, delegate(Sec s)
            {
                for (int i = 0; i < BtcCleanFilter.Bits.Length; i++)
                {
                    int bit = BtcCleanFilter.Bits[i];
                    s.Add(new Btn
                    {
                        Visible = IsCleanSel, IconName = BtcCleanFilter.Icons[i], IconColor = Color.white, IconSize = 64f,
                        OnAction = delegate { ActClean(bit); },
                        Highlight = delegate { return IsCleanSel() && (BtcCleanFilter.Mask((CleanupProject)_sel) & bit) != 0 ? 15 : 0; },
                    });
                }
            });
        }

        /// Same body as BtcCleanFilter.LuaToggle (private there), then the highlight refresh the Lua does after the click.
        private static void ActClean(int bit)
        {
            if (Paused() || !IsCleanSel()) return;
            try
            {
                CleanupProject p = (CleanupProject)_sel;
                if (BtcNetworkGate.Enabled)
                {
                    if (NetworkServer.active) { BtcCleanFilter.ApplyLocal(p, bit); BtcCleanFilterNet.Broadcast(); }
                    else BtcCleanFilterNet.SendRequest(p, bit);
                }
            }
            catch (Exception ex) { BtcLog.Error("CleanFilter", ex.Message); }
            Refresh(M_CLEAN);
        }

        // ------------------------------------------------------------------ priority
        private static Sec PrioritySec()
        {
            return new Sec(M_SEL | M_PRIO, NotIdle, "##task_priority", delegate(Sec s)
            {
                s.Add(new Btn { IconName = "UI_Minus", IconSize = 32f, IconColorFn = LowerColor, HasHotkey = true, Hotkey = Key("Projects", "project_LowerPriority"), OnAction = ActLower });
                s.Add(new Btn { IconName = "UI_Plus", IconSize = 32f, IconColorFn = RaiseColor, HasHotkey = true, Hotkey = Key("Projects", "project_RaisePriority"), OnAction = ActRaise });
            });
        }

        private static Color LowerColor() { return Has() && _sel.GetPriority() != ProjectPriority.Bias.Locked ? BtcGhKit.Red : BtcGhKit.Gray; }
        private static Color RaiseColor() { return Has() && _sel.GetPriority() != ProjectPriority.Bias.High ? BtcGhKit.Red : BtcGhKit.Gray; }
        private static UNetGroupe Groupe() { return User.LocalUser.GetComponent<UNetGroupe>(); }

        internal static void ActRaise() { if (!Paused() && Has()) Groupe().CallCmdRaisePriority(_sel.gameObject); }
        internal static void ActLower() { if (!Paused() && Has()) Groupe().CallCmdLowerPriority(_sel.gameObject); }

        // ------------------------------------------------------------------ task actions (ok / apply / cancel / delete)
        private static Sec ActionsSec()
        {
            return new Sec(M_SEL | M_STATUS | M_DONE, NotIdle, "##task_actions", delegate(Sec s)
            {
                s.Add(new Btn { Visible = ShowOk, IconName = "UI_Check", IconSize = 32f, IconColorFn = OkColor, OnAction = ActOk });
                s.Add(new Btn { Visible = ShowApply, IconName = "UI_Down", IconSize = 32f, IconColorFn = ApplyColor, OnAction = ActApply });
                s.Add(new Btn { Visible = ShowCancel, IconName = "UI_X", IconSize = 32f, IconColorFn = CancelColor, OnAction = ActCancel });
                s.Add(new Btn { Visible = ShowDelete, IconName = "UI_Trash", IconSize = 32f, IconColorFn = DeleteColor, HasHotkey = true, Hotkey = Key("Projects", "project_DeleteTask"), OnAction = ActDelete });
            });
        }

        // Project.Show*/Has*/Set* are the private statics behind Project.lua_ShowStatusAction / HasStatusAction / SetStatusAction
        private static bool ShowOk() { return Has() && Project.ShowOk(_sel); }
        private static bool ShowApply() { return Has() && Project.ShowApply(_sel); }
        private static bool ShowCancel() { return Has() && Project.ShowCancel(_sel); }
        private static bool ShowDelete() { return Has() && Project.ShowDelete(_sel); }
        private static bool HasOk() { return ShowOk() && Project.HasOk(_sel); }
        private static bool HasApply() { return ShowApply() && Project.HasApply(_sel); }
        private static bool HasCancel() { return ShowCancel() && Project.HasCancel(_sel); }
        private static bool HasDelete() { return ShowDelete() && Project.HasDelete(_sel); }

        private static Color OkColor() { return HasOk() ? Green : BtcGhKit.Gray; }
        private static Color ApplyColor() { return HasApply() ? BtcGhKit.Yellow : BtcGhKit.Gray; }
        private static Color CancelColor() { return HasCancel() ? PureRed : BtcGhKit.Gray; }
        private static Color DeleteColor() { return HasDelete() && !BlueprintCompounder.IsInstantiatingCompound() ? PureRed : BtcGhKit.Gray; }

        private static void ActOk() { if (!Paused() && HasOk()) Project.SetOk(_sel); }
        private static void ActApply() { if (!Paused() && HasApply()) Project.SetApply(_sel); }
        private static void ActCancel() { if (!Paused() && HasCancel()) Project.SetCancel(_sel); }
        internal static void ActDelete() { if (!Paused() && !OfType(T_IDLE) && !BlueprintCompounder.IsInstantiatingCompound() && HasDelete()) Project.SetDelete(_sel); }

        // ------------------------------------------------------------------ _m.OnActionSelectedTask (GameMenu.lua 511): every handle guards its own project type
        internal static void ActTaskSlot(int v)
        {
            try
            {
                if (TerraShown()) { TerraSlot(v); return; }
                if (v == 1) { ActHarvest(NatureType.boulder); ActTunnelUp(); ActLandRaise(); ActQuarryStairs(); }
                else if (v == 2) { ActHarvest(NatureType.iron); ActTunnelStraight(); ActLandDig(); ActQuarryKeepStone(); }
                else if (v == 3) { ActHarvest(NatureType.pyrite); ActTunnelDown(); ActQuarryKeepIron(); }
                else if (v == 4) { ActHarvest(NatureType.tree); ActTunnelKeepStone(); ActQuarryKeepBrimstone(); }
                else if (v == 5) { ActHarvest(NatureType.plant); ActTunnelKeepIron(); }
                else if (v == 6) { ActHarvest(NatureType.plant); ActTunnelKeepBrimstone(); } // vanilla quirk kept: slot 6 toggles plants on harvest tasks
            }
            catch (Exception e) { BtcLog.Error("GameHud", "task slot " + v + ": " + e.Message); }
        }

        // ------------------------------------------------------------------ idle task: population + crystal
        private static float _fCur, _fTot;
        private static int _fSpawn, _fAuto;
        private static string _fText = "0/0", _maxText;
        private static int _popN = -1, _popCap = -1;
        private static string _popStr = "";

        private static FireflyNest AlliedNest()
        {
            User u = User.LocalUser;
            if (u == null || FireflyNest.FireflyNests == null) return null;
            Faction f = u.faction;
            foreach (FireflyNest n in FireflyNest.FireflyNests)
                if (n != null && n.isHome && !(n.faction != f)) return n;
            return null;
        }

        private static string Fmt(float f) { return Mathf.Approximately(f, Mathf.Round(f)) ? Num((int)f) : f.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }

        /// Data.FireflyNest:OnSetInfo (FireflyNest.lua_GetInfo without the exception when no home nest exists yet).
        private static void ReadFirefly()
        {
            try
            {
                FireflyNest n = AlliedNest();
                if (n == null) return;
                _fCur = (float)n.GetAvailablePurifiedEnergy();
                _fTot = (float)GeneralRules.Main.GetHomeCrystalSpawnRequiredEnergy(n.GetActiveOrSpawnableCharacterCount());
                _fSpawn = (int)(float)n.newSpawnableFireflyCount;
                _fAuto = (int)(float)n.autoRespawnFireflyCount;
                _fText = Fmt(_fCur) + "/" + Fmt(_fTot);
            }
            catch (Exception e) { BtcLog.Error("GameHud", "firefly info: " + e.Message); }
        }

        private static string PopText()
        {
            int n = UIGameObserver.bricktrons.CheckCreation().Count;
            int cap = GeneralRules.Main.bricktronCap;
            if (n != _popN || cap != _popCap) { _popN = n; _popCap = cap; _popStr = Num(n) + " / " + Num(cap); }
            return _popStr;
        }

        private static Sec IdlePopSec()
        {
            return new Sec(M_SEL | M_OPSEL | M_POP | M_FIRE, IsIdleNoOp, "##task_idle_population", delegate(Sec s)
            {
                s.Add(new Btn { IconName = "_Bricktron", IconSize = 48f, IconColor = Color.white, NoHover = true });
                s.Add(new Btn
                {
                    IconName = "_UI_Warning", IconSize = 32f, IconColor = Transparent, HoverColor = Transparent, OnAction = NoOp,
                    CountFn = PopText, CountSize = 12, CountAlign = TextAnchor.MiddleCenter, CountColor = BtcGhKit.Red,
                });
                s.Add(new Btn
                {
                    IconName = "Skull", IconSize = 32f, IconColorFn = DeadIconColor, HoverColor = Transparent, OnAction = NoOp,
                    CountFn = DeadText, CountColorFn = DeadColor, HasHotkey = true, Hotkey = DeadHotkey,
                });
            });
        }

        private static string _deadText;
        private static string DeadText() { return _deadText ?? (_deadText = BtcGameHud.Loc("##task_idle_dead")); }
        private static Color DeadIconColor() { return _fAuto != 0 ? Color.white : BtcGhKit.Gray; }
        private static Color DeadColor() { return _fAuto != 0 ? BtcGhKit.Red : BtcGhKit.Gray; }
        private static string DeadHotkey() { return _fAuto != 0 ? Num(_fAuto) : ""; }

        private static Sec IdleActionsSec()
        {
            return new Sec(M_SEL | M_OPSEL | M_FIRE | M_POP, IsIdleNoOp, "##task_idle_crystal", delegate(Sec s)
            {
                s.Add(new EnergyCell());
                s.Add(new Btn
                {
                    IconName = "_UI_Bricktron", IconSize = 32f, IconColorFn = SpawnColor, OnAction = ActSpawn,
                    CountFn = SpawnText, CountColorFn = SpawnColor, HasHotkey = true, Hotkey = SpawnHotkey,
                });
            });
        }

        private static string _spawnText;
        private static string SpawnText() { return _spawnText ?? (_spawnText = BtcGameHud.Loc("##task_idle_spawn")); }
        private static Color SpawnColor() { return _fSpawn != 0 ? BtcGhKit.Red : BtcGhKit.Gray; }
        private static string SpawnHotkey() { return _fSpawn != 0 ? Num(_fSpawn) : ""; }

        /// FireflyNest.lua_SpawnUnit (no pause guard in the Lua either).
        private static void ActSpawn()
        {
            if (_fSpawn == 0) return;
            FireflyNest n = AlliedNest();
            if (n != null) n.AssignJobRequest(Occupation.Type.Builder, null);
        }

        /// Panel_ProgressHorizontal.lua: 160 x 48, bar (bg 0.25 grey + fill in the allied faction colour), "Energy:" left, "cur/total" right.
        private sealed class EnergyCell : Cell
        {
            private static Sprite _white;
            private static Sprite WhiteSprite()
            {
                if (_white != null) return _white;
                Texture2D t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                Color32[] px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                t.SetPixels32(px); t.Apply(); t.hideFlags = HideFlags.HideAndDontSave;
                _white = Sprite.Create(t, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
                _white.hideFlags = HideFlags.HideAndDontSave;
                return _white;
            }

            private Image _fill;
            private RectTransform _fillRt;
            private Text _count;
            private BtcGhButton _b;

            internal override void Create(Sec s)
            {
                RectTransform p = BtcGhKit.Make(Layouts.Horizontal, s.Root, "");
                Go = p.gameObject;
                BtcGhKit.Rect(p, new Vector2(0f, 1f), new Vector2(0f, 1f), BtcGhKit.Mid, Vector2.zero, new Vector2(160f, 48f));
                BtcGhKit.Img(p, null, new Color32(0, 0, 0, 192), false);
                BtcGhKit.Le(p, 160f, 48f, 160f, 48f, -1f, -1f, false);
                BtcGhKit.Group(p, 8, 8, 0, 0, 2f, TextAnchor.MiddleCenter);
                RectTransform bar = BtcGhKit.Make(Layouts.Horizontal, p, "");
                BtcGhKit.Stretch(bar);
                BtcGhKit.Img(bar, null, new Color32(0, 0, 0, 192), false);
                BtcGhKit.Le(bar, -1f, 8f, -1f, -1f, 1f, -1f, false);
                BtcGhKit.Group(bar, 0, 0, 0, 0, 2f, TextAnchor.UpperLeft);
                RectTransform bg = BtcGhKit.Make(Images.Default, bar, "");
                BtcGhKit.Rect(bg, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), BtcGhKit.Mid, new Vector2(0f, 2f), new Vector2(0f, 4f));
                BtcGhKit.Img(bg, null, new Color(0.25f, 0.25f, 0.25f, 1f), true);
                BtcGhKit.Le(bg, 64f, 64f, -1f, -1f, -1f, -1f, true);
                RectTransform fg = BtcGhKit.Make(Images.Default, bar, "");
                BtcGhKit.Rect(fg, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), BtcGhKit.Mid, new Vector2(0f, 2f), new Vector2(0f, 4f));
                _fill = fg.GetComponent<Image>(); _fill.sprite = null; _fill.type = Image.Type.Simple; _fill.enabled = true; _fillRt = fg; // fill = right anchor x (Filled mode rendered a full-width line)
                BtcGhKit.Le(fg, 64f, 64f, -1f, -1f, -1f, -1f, true);
                RectTransform title = BtcGhKit.Make(Texts.Default, p, "");
                BtcGhKit.Rect(title, Vector2.zero, Vector2.one, BtcGhKit.Mid, Vector2.zero, Vector2.zero);
                title.offsetMin = new Vector2(8f, 4f); title.offsetMax = Vector2.zero;
                BtcGhKit.Txt(title, BtcGameHud.Loc("##task_idle_energy"), 12, BtcGhKit.Yellow, TextAnchor.LowerLeft);
                BtcGhKit.Le(title, 64f, 64f, -1f, -1f, -1f, -1f, true);
                RectTransform cnt = BtcGhKit.Make(Texts.Default, p, "");
                BtcGhKit.Rect(cnt, Vector2.zero, Vector2.one, BtcGhKit.Mid, Vector2.zero, Vector2.zero);
                cnt.offsetMin = new Vector2(0f, 4f); cnt.offsetMax = new Vector2(-8f, 0f);
                _count = BtcGhKit.Txt(cnt, "", 12, BtcGhKit.Yellow, TextAnchor.LowerRight);
                BtcGhKit.Le(cnt, 64f, 64f, -1f, -1f, -1f, -1f, true);
                RectTransform btn = BtcGhKit.Make(Buttons.Standard, p, "");
                BtcGhKit.Stretch(btn);
                BtcGhKit.Le(btn, 64f, 64f, 64f, 64f, -1f, -1f, true);
                _b = btn.gameObject.AddComponent<BtcGhButton>(); // rollover sounds only (the Lua panel has no click action)
                _b.Frame = btn.GetComponent<Brix.UI.Builder.Widget.FrameAnimatorWidgetComponent>() ?? btn.GetComponentInChildren<Brix.UI.Builder.Widget.FrameAnimatorWidgetComponent>(true);
                NoRaycast(p, btn);
            }

            internal override void Refresh()
            {
                float fr = _fTot > 0f ? Mathf.Clamp01(_fCur / _fTot) : 1f;
                if (_fillRt.anchorMax.x != fr) _fillRt.anchorMax = new Vector2(fr, 0.5f);
                try { User u = User.LocalUser; if (u != null && u.faction != null) _fill.color = u.faction.FactionColor; } catch { }
                string t;
                if (UIGameObserver.bricktrons.CheckCreation().Count >= GeneralRules.Main.bricktronCap) t = _maxText ?? (_maxText = BtcGameHud.Loc("##task_idle_maxenergy"));
                else t = _fText;
                if (_count.text != t) _count.text = t;
            }
        }
    }
}
