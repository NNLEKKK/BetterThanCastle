using System;
using System.Reflection;
using Brix.Assets;
using Brix.Game.Components;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Advanced timer (Features/BtcTimer.cs): no crystal energy before the start; timer state next to the save.

    [HarmonyPatch]
    internal static class TimerBreweryCanBrewPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.Inner(typeof(FireflyNest), "Brewery"), "CanBrew");
        }

        private static void Postfix(ref bool __result)
        {
            if (BtcTimer.FreezeEnergy) __result = false;
        }
    }

    [HarmonyPatch(typeof(Asset_Map), "Disk_Overwrite")]
    internal static class TimerSaveOverwritePatch
    {
        private static void Postfix(Asset_Map __instance) { BtcTimer.WriteSide(__instance); }
    }

    [HarmonyPatch(typeof(Asset_Map), "Disk_WriteAsNew")]
    internal static class TimerSaveNewPatch
    {
        private static void Postfix(Asset_Map __instance) { BtcTimer.WriteSide(__instance); }
    }

    // The score panel clock (conquest/credit/pumpkin menus: Registry.gameTimer:FormatedElapsedSeconds) shows the advanced timer while it is active.
    [HarmonyPatch(typeof(Brix.Lua.LuaTimer), "FormatedElapsedSeconds")]
    internal static class TimerPanelTextPatch
    {
        // Prefix: our text is returned before the Lua arguments are parsed (a nil offset after a replay seek made the vanilla call throw and froze the clock)
        private static bool Prefix(ref MoonSharp.Interpreter.DynValue __result)
        {
            try
            {
                string s = BtcTimer.PanelText();
                if (s == null) return true;
                __result = MoonSharp.Interpreter.DynValue.NewString(s);
                return false;
            }
            catch (System.Exception e) { BtcLog.Error("Hook", "LuaTimer.FormatedElapsedSeconds: " + e); return true; }
        }
    }
}
