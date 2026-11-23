using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Input;
using Brix.Utils.UI;
using UnityEngine;

namespace BetterThanCastle
{
    /// C# game menu (replaces LUI/Menus/GameMenu.lua when Display/CsGameMenu is on; plan + status docs/gamemenu-cs.md).
    /// Built inside the GameMenu LuaMenuComponent's own object ("Menu: Taskbar (Lua)") so canvas, scale and sorting stay vanilla.
    /// Perf rules: event driven (game signals, pointer events), no Update/poll, sidebars built on first open, no Lua at all.
    internal static class BtcGameHud
    {
        internal static bool Enabled { get { return BtcConfig.CsGameMenu != null && BtcConfig.CsGameMenu.Value; } }
        internal static bool Open { get { return _root != null; } }

        private static RectTransform _root;
        private static readonly List<BtcGhButton> Buttons = new List<BtcGhButton>();

        /// Called from the GameMenu.lua load skip (LuaMenuComponentLoadPatch) with the component's transform.
        internal static void Build(Transform host)
        {
            try
            {
                Close();
                RectTransform h = host as RectTransform;
                if (h == null) { BtcLog.Error("GameHud", "host has no RectTransform"); return; }
                float t = Time.realtimeSinceStartup;
                _root = h;
                if (h.GetComponent<BtcGameHudLife>() == null) h.gameObject.AddComponent<BtcGameHudLife>();
                BtcGhLeft.Build(h);
                BtcGhRightBar.Build(h);
                BtcGhPopup.Build(h);
                BtcGhTaskList.PopupSet = BtcGhPopup.Track;
                BtcGhTaskList.PopupReset = BtcGhPopup.Untrack;
                BtcGhTaskList.PopupVisible = delegate { return BtcGhPopup.Shown; };
                BtcGhTaskList.Build(h);
                BtcGhPaused.Build(h);
                BtcGhBlueprints.InitLibrary(); // GameMenu.lua top: BlueprintSelection.InitializeCompounds(), spread over frames
                BtcGhModes.ActivateProjects(); // GameMenu OnOpen
                RefreshAll();
                BtcLog.Info("GameHud", "C# game menu built in " + ((Time.realtimeSinceStartup - t) * 1000f).ToString("0") + " ms (" + Buttons.Count + " buttons)");
            }
            catch (Exception e) { BtcLog.Error("GameHud", "build: " + e); }
        }

        internal static void Close()
        {
            if (_root == null && Buttons.Count == 0) return;
            try { BtcGhModes.UnregisterAll(); } catch (Exception e) { BtcLog.Info("GameHud", "unregister at teardown: " + e.Message); }
            BtcGhLeft.Close();
            BtcGhRightBar.Close();
            BtcGhPaused.Close();
            BtcGhTaskList.Close();
            BtcGhPopup.Close();
            Buttons.Clear();
            _root = null;
        }

        internal static void Track(BtcGhButton b) { Buttons.Add(b); }

        /// Rail buttons: visibility, state colours, hotkey labels (on selection / open changes, like the Lua ev_refresh).
        internal static void RefreshAll()
        {
            for (int i = 0; i < Buttons.Count; i++) if (Buttons[i] != null) Buttons[i].Refresh();
        }

        internal static string Hotkey(string category, string action)
        {
            if (string.IsNullOrEmpty(action)) return "";
            try { return KeyBindingsUtility.GetKeyBindingCodename(category, action) ?? ""; } catch { return ""; }
        }

        internal static string Loc(string key)
        {
            try { return Brix.Utils.I2Helper.TryGet(key); } catch { return key; }
        }
    }

    /// Lives on the menu host: the menu objects die with the scene, this drops the key actions and signal connections.
    internal class BtcGameHudLife : MonoBehaviour
    {
        internal static BtcGameHudLife Inst;
        private void Awake() { Inst = this; }
        private void OnDestroy() { if (Inst == this) Inst = null; BtcGameHud.Close(); }
    }

    /// GameMenu right bar (mg.right: home, settings, help, debug/editor/movie dev buttons, spacer, back to work, call to arms),
    /// LeftBarMenu.lua layout on the right edge (Vertical a=1,0-1,1 p=1,0.5 48 wide).
    internal static class BtcGhRightBar
    {
        internal static void Build(RectTransform root)
        {
            RectTransform rail = BtcGhKit.Make(Brix.External.Factories.Layouts.Vertical, root, "");
            BtcGhKit.Rect(rail, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(48f, 0f));
            BtcGhKit.Img(rail, null, new Color32(0, 0, 0, 192), false);
            BtcGhKit.Le(rail, -1f, -1f, -1f, -1f, -1f, -1f, true);
            BtcGhKit.Group(rail, 0, 0, 0, 0, 0f, TextAnchor.UpperLeft);
            Color y = BtcGhKit.Yellow;

            Add(rail, "UI_Home", "##gamemenu_home", "F10", delegate
            {
                if (InputModeController.IsPaused()) InputModeController.TogglePause();
                InputModeController.ToggleMainMenu();
            }, null);
            // settings / help open floating sidebars in the right content socket (LeftContentMenu.lua, anchored right)
            _content = BtcGhKit.Make(Brix.External.Factories.Layouts.Horizontal, root, "");
            BtcGhKit.Rect(_content, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-48f, 77f), new Vector2(272f, -154f));
            BtcGhKit.Img(_content, null, new Color32(0, 0, 0, 192), false);
            BtcGhKit.Le(_content, -1f, -1f, -1f, -1f, -1f, -1f, true);
            BtcGhKit.Group(_content, 0, 0, 0, 0, 0f, TextAnchor.UpperLeft);
            BtcHudSlots.Register("gh.rightContent", _content);
            _settingsBtn = Add(rail, "UI_Settings", "##gamemenu_topmenu_settings", "", delegate { Toggle(1); }, null);
            _settingsBtn.BgColor = delegate { return _open == 1 ? BtcGhKit.Selected : BtcGhKit.RailBg; };
            _helpBtn = Add(rail, "UI_Help", "##gamemenu_ingamehelp_title", "", delegate { BtcLog.Info("GameHud", "help sidebar (Menu_Help.lua): not built yet"); },
                delegate { return !Neo.IsMultiplayer && IsSandbox(); });
            // developers' Editor and movie sidebars (GameMenu.lua mh.debug / mh.movie), while debug menus are allowed
            BtcGhButton ed = Add(rail, "UI_Console", "Editor", "", delegate { Toggle(3); }, delegate { return BtcGhDev.Allowed; });
            ed.BgColor = delegate { return _open == 3 ? BtcGhKit.Selected : BtcGhKit.RailBg; };
            BtcGhButton mv = Add(rail, "UI_Play", "movie", "", delegate { Toggle(4); }, delegate { return BtcGhDev.Allowed; });
            mv.BgColor = delegate { return _open == 4 ? BtcGhKit.Selected : BtcGhKit.RailBg; };

            RectTransform sp = BtcGhKit.Make(Brix.External.Factories.Images.Default, rail, "");
            BtcGhKit.Rect(sp, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 24f));
            BtcGhKit.Img(sp, null, BtcGhKit.RailBg, true);
            BtcGhKit.Le(sp, -1f, -1f, 0f, 0f, 1f, 1f, false);

            BtcGhButton bw = Add(rail, "_UI_Hammer", "##gamemenu_crystalwheel_backtowork", null, BackToWork, null);
            bw.HotkeyFn = delegate { return BtcGameHud.Hotkey("Camera", "command_BackToWork"); };
            BtcGhButton ca = Add(rail, "_UI_CallToArms", "##gamemenu_crystalwheel_calltoarms", null, CallToArms, null);
            ca.HotkeyFn = delegate { return BtcGameHud.Hotkey("Camera", "command_CallToArms"); };
            BtcGhModes.BackToWork = BackToWork;
            BtcGhModes.CallToArms = CallToArms;
            BtcHudSlots.Register("gh.right", rail);
        }


        private static RectTransform _content;
        private static BtcGhSidebar _settings;
        private static BtcGhButton _settingsBtn, _helpBtn;
        private static int _open; // mg.right (exclusive): 0 none, 1 settings, 2 help

        private static bool IsSandbox()
        {
            try { return UIGameObserver.lua_GetModeName(default(MoonSharp.Interpreter.ScriptExecutionContext), default(MoonSharp.Interpreter.CallbackArguments)).String == "##mode_sandbox"; }
            catch { return false; }
        }

        internal static void Close() { _content = null; _settings = null; _editor = null; _movie = null; _open = 0; BtcGhDev.Close(); }

        private static BtcGhSidebar _editor, _movie;

        internal static void Toggle(int which)
        {
            _open = _open == which ? 0 : which;
            if ((_open == 3 || _open == 4) && !BtcGhDev.Allowed) _open = 0;
            if (_open == 1 && _settings == null && _content != null) { _settings = new BtcGhSidebar(_content, BtcGameHud.Loc("##gamemenu_topmenu_settings"), delegate { Toggle(1); }); FillSettings(_settings); }
            if (_open == 3 && _editor == null && _content != null) { _editor = new BtcGhSidebar(_content, "Editor", delegate { Toggle(3); }); BtcGhDev.Track(_editor, BtcGhDev.BuildEditor(_editor)); }
            if (_open == 4 && _movie == null && _content != null) { _movie = new BtcGhSidebar(_content, "movie", delegate { Toggle(4); }); BtcGhDev.Track(_movie, BtcGhDev.BuildMovie(_movie)); }
            if (_settings != null) { _settings.SetActive(_open == 1); if (_open == 1) _settings.Refresh(); }
            if (_editor != null) { _editor.SetActive(_open == 3); if (_open == 3) { _editor.Refresh(); BtcGhDev.RefreshValues(_editor); } }
            if (_movie != null) { _movie.SetActive(_open == 4); if (_open == 4) { _movie.Refresh(); BtcGhDev.RefreshValues(_movie); } }
            BtcGameHud.RefreshAll(); // mg.right.onAnyOpen -> RefreshAll
        }

        /// "Compact panel" switched: the Editor sidebar is built again (it stays open)
        internal static void RebuildEditor()
        {
            if (_editor == null) return;
            BtcGhDev.Forget(_editor);
            if (_editor.Socket != null) UnityEngine.Object.Destroy(_editor.Socket.gameObject);
            _editor = null;
            if (_open == 3) { _open = 0; Toggle(3); }
        }

        /// debug menus switched off while a dev sidebar is open (host turned gen.editor off): close it
        internal static void CheckDev() { if ((_open == 3 || _open == 4) && !BtcGhDev.Allowed) Toggle(_open); }

        /// GameMenu.lua 3328-3530: pause + quicksave (SP, not world editor), screenshot, mute, xray + allow debug menu (not world
        /// editor), editor camera. IsBuildDebug() is false in release builds, so its fog/speed/audio/gc buttons are not shown.
        private static void FillSettings(BtcGhSidebar bar)
        {
            RectTransform grid = BtcGhKit.Grid(bar.Body, 64f, 64f);
            bool we = InputModeController.DefaultMode == InputMode.worldEditor;
            if (!Neo.IsMultiplayer && !we)
            {
                Setting(bar, grid, "UI_Pause", "##gamemenu_topmenu_pause", delegate
                {
                    InputModeController.TogglePause();
                    BtcGhButton.Play(InputModeController.IsPaused() ? "Pause_In" : "Pause_Out");
                }, null);
                Setting(bar, grid, "UI_Quicksave", "##gamemenu_topmenu_quicksave", delegate { Brix.NewUI.CornerMenus.MenuCalls.QuickSave(default(MoonSharp.Interpreter.ScriptExecutionContext), default(MoonSharp.Interpreter.CallbackArguments)); }, null);
            }
            Setting(bar, grid, "UI_Screenshot", "##gamemenu_topmenu_screenshot", delegate { Brix.NewUI.CornerMenus.MenuCalls.TakeScreenShot(default(MoonSharp.Interpreter.ScriptExecutionContext), default(MoonSharp.Interpreter.CallbackArguments)); }, null);
            BtcGhButton mute = null;
            mute = Setting(bar, grid, "UI_Mute", "##gamemenu_topmenu_mute", delegate { Brix.Audio.SoundEngine.mute = !Brix.Audio.SoundEngine.mute; mute.Refresh(); }, null);
            mute.IconSprite = delegate { return BtcGhKit.Icon(Brix.Audio.SoundEngine.mute ? "UI_Mute" : "UI_VolumeHigh"); };
            if (!we)
            {
                Setting(bar, grid, "UI_XRay", "##gamemenu_topmenu_xray", delegate { if (XRayImpl.instance != null) XRayImpl.instance.enabled = !XRayImpl.instance.enabled; }, null);
                Setting(bar, grid, "UI_Console", "##gamemenu_topmenu_toggledebug", delegate { Architecte.allowDebugMenu = !Architecte.allowDebugMenu; }, null); // ToggleAllowDebugMenu
            }
            Setting(bar, grid, "UI_TerrainHug", "##gamemenu_topmenu_editorcamera", delegate
            {
                CameraScript cs = Camera.main != null ? Camera.main.GetComponent<CameraScript>() : null;
                if (cs != null) cs.ToggleEditorCamera(!cs.editorCamera);
            }, null);
        }

        private static BtcGhButton Setting(BtcGhSidebar bar, RectTransform grid, string icon, string loc, Action act, Func<bool> visible)
        {
            BtcGhButton b = BtcGhKit.GridButton(grid, BtcGhKit.Icon(icon), BtcGhKit.Red, 32f, BtcGameHud.Loc(loc), true, null, false);
            b.OnAction = act;
            b.Visible = visible;
            return bar.Add(b);
        }
        private static BtcGhButton Add(RectTransform rail, string icon, string locKey, string hotkey, Action act, Func<bool> visible)
        {
            BtcGhButton b;
            BtcGhKit.RailButton(rail, BtcGhKit.Icon(icon), BtcGhKit.Yellow, locKey.StartsWith("##") ? BtcGameHud.Loc(locKey) : locKey, hotkey ?? "", BtcGhKit.Yellow, out b);
            b.OnAction = act;
            b.Visible = visible;
            BtcGameHud.Track(b);
            return b;
        }

        /// UIGameObserver.lua_BackToWork: every non-builder bricktron gets a back-to-work goal.
        internal static void BackToWork()
        {
            if (InputModeController.IsPaused()) return;
            Brix.Audio.SoundEngine.Play("Back_To_Work");
            foreach (Labor labor in UIGameObserver.bricktrons.CheckCreation())
                if (labor.Profession.OccupationType != Occupation.Type.Builder) DirectOrderProject.AddBackToworkGoal(labor);
        }

        /// UIGameObserver.lua_CallToArms with the local player's crystal (same lookup as CallToArmsPatches).
        internal static void CallToArms()
        {
            if (InputModeController.IsPaused()) return;
            GameObject crystal = Patches.UIGameObserverCallToArmsPatch.FindOwnCrystal();
            if (crystal == null) { BtcLog.Warn("GameHud", "call to arms: no own crystal"); return; }
            Brix.Audio.SoundEngine.Play("Call_To_Arm");
            foreach (Labor labor in UIGameObserver.bricktrons.CheckCreation())
                if (labor.Profession.OccupationType == Occupation.Type.Builder) DirectOrderProject.AddCallToArmsGoal(labor, crystal);
        }
    }
}
