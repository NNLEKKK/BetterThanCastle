using System;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// Performance/SkipModeIntro: sandbox, conquest and invasion open an intro window at the start of a new match (a 1024x1024 image read
    /// from disk, one button). The mode's intro function is wrapped: while it runs, the modal open (Hooks.Invoke with the window table) is
    /// replaced by the action of its visible button, exactly what the player's click would do (conquest/invasion single player: timers and
    /// waves start). Lua of the local script only, nothing on the wire. The tutorial keeps its window.
    /// </summary>
    [HarmonyPatch(typeof(Brix.Assets.Asset_Gamemode), "Disk_LoadGamemode")]
    internal static class ModeIntroSkipPatch
    {
        private const string Lua =
            "if ShowIntroDialog ~= nil or DoShowIntroDialog ~= nil then " +
            "  local function press(t) " +
            "    local b = t.btn_left " +
            "    if b == nil or b.Visible == nil or not b.Visible() then b = t.btn_right end " +
            "    if b ~= nil and b.OnAction ~= nil then b.OnAction() end " +
            "    __BtcIntroSkipped() " +
            "  end " +
            "  local function skip(f) " +
            "    return function(...) " +
            "      local inv = Hooks.Invoke " +
            "      local hit = false " +
            "      Hooks.Invoke = function(h, a) " +
            "        if not hit and type(a) == 'table' and type(a[1]) == 'table' and (a[1].btn_left ~= nil or a[1].btn_right ~= nil) then hit = true; press(a[1]) " +
            "        else return inv(h, a) end " +
            "      end " +
            "      local ok, err = pcall(f, ...) " +
            "      Hooks.Invoke = inv " +
            "      if not ok then error(err, 0) end " +
            "    end " +
            "  end " +
            // conquest/invasion defer the window until the modal menu is loaded: wrap the deferred function, the timing stays the same
            "  if DoShowIntroDialog ~= nil then DoShowIntroDialog = skip(DoShowIntroDialog) else ShowIntroDialog = skip(ShowIntroDialog) end " +
            "end";

        private static void Postfix(Brix.Assets.Asset_Gamemode __instance, ref MoonSharp.Interpreter.Script outscript)
        {
            try
            {
                if (outscript == null || BtcConfig.SkipModeIntro == null || !BtcConfig.SkipModeIntro.Value) return;
                if (__instance != null && __instance.codename == "tutorial") return;
                outscript.Globals["__BtcIntroSkipped"] = MoonSharp.Interpreter.DynValue.NewCallback((c, a) => { BtcLog.Info("ModeIntro", "intro window skipped (button action run)"); return MoonSharp.Interpreter.DynValue.Void; }, null);
                outscript.DoString(Lua, null, "btc_skipintro");
            }
            catch (Exception e) { BtcLog.Error("Hook", "ModeIntroSkip: " + e.Message); }
        }
    }
}
