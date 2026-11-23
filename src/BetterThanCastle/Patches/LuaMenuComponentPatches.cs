using System;
using System.Diagnostics;
using Brix.UI.Builder.Menu;
using Brix.UI.Builder.Menu.Component;
using HarmonyLib;
using MoonSharp.Interpreter;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// Load(): the per-frame "_isStop" step resumes the menu's Lua coroutine repeatedly within a time budget
    /// (BtcLoadBoost.MenuSliceOn / MenuBudgetMs) instead of once per frame. Replaces the vanilla delegate set by Load().
    /// </summary>
    [HarmonyPatch(typeof(LuaMenuComponent), "Load")]
    internal static class LuaMenuComponentLoadPatch
    {
#if DEV
        internal static bool DevSkipGameMenu; // DEV: measure a match without the game HUD (cmd skipmenu)
#endif
        /// replay playback without the game HUD (Replay/NoGameHud): the mini map comes from BtcReplayMinimap
        private static bool ReplaySkip { get { return BtcReplay.IsPlayback && BtcConfig.ReplayNoGameHud != null && BtcConfig.ReplayNoGameHud.Value; } }
        [HarmonyPrefix]
        private static bool Prefix(LuaMenuComponent __instance, ref bool __result)
        {
            // aim trainer: the game HUD (GameMenu.lua: taskbar, task panels, mini map...) is not loaded at all; clicks use their own ray
#if DEV
            if (!BtcAim.Active && !ReplaySkip && !DevSkipGameMenu && !BtcGameHud.Enabled) return true;
#else
            if (!BtcAim.Active && !ReplaySkip && !BtcGameHud.Enabled) return true;
#endif
            try
            {
                string p = __instance.Path ?? "";
                if (!p.Replace('\\', '/').EndsWith("LUI/Menus/GameMenu.lua", StringComparison.OrdinalIgnoreCase)) return true;
                BtcLog.Info(BtcAim.Active ? "Aim" : "Hud", "game HUD skipped: " + p);
                if (!BtcAim.Active) BtcReplayMinimap.GameHudSkipped = true;
                // C# game menu (Display/CsGameMenu) in normal games: built into the same object the Lua menu would fill
                if (!BtcAim.Active && !ReplaySkip && BtcGameHud.Enabled) BtcGameHud.Build(__instance.transform);
                __result = true;
                return false;
            }
            catch (Exception) { return true; }
        }

        [HarmonyPostfix]
        private static void Postfix(LuaMenuComponent __instance)
        {
            try
            {
                if (__instance._isStop == null) return;
                __instance._isStop = Step;
            }
            catch (Exception e) { BtcLog.Error("Hook", "LuaMenuComponent.Load: " + e); }
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (BtcReplay.Loading || BtcReplay.Running)
            {
                BtcLog.Error("Loader", "LuaMenuComponent.Load threw during replay (swallowed): " + __exception.Message);
                return null;
            }
            return __exception;
        }

        private static bool Step(DynValue t, LuaMenuComponent lmc)
        {
            long t0 = Stopwatch.GetTimestamp();
            for (;;)
            {
                long r0 = Stopwatch.GetTimestamp();
                t.Tuple[1].Coroutine.Resume();
                double ms = BtcLoadBoost.ElapsedMs(r0);
                if (ms >= 150.0) BtcLoadBoost.Note("  slow menu resume: " + ms.ToString("F0") + " ms" + BtcLoadBoost.GcInfo());
                if (t.Tuple[1].Coroutine.State == CoroutineState.Dead) break;
                if (!BtcLoadBoost.MenuSliceOn()) return false;
                if (BtcLoadBoost.ElapsedMs(t0) >= (double)BtcLoadBoost.MenuBudgetMs) return false;
            }
            lmc._menu = LuaMenu.LoadMenuTable(t.Tuple[0].Table, lmc.GetParent, lmc.OnMenuLoadedHook, lmc.Path, false, true, false);
            return true;
        }
    }
}
