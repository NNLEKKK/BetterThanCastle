using System;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// The victory window gets the same "Spectate" button as the defeat window (closes the window, the match keeps running for you to watch).
    /// ShowVictoryDialog of the mode script is wrapped: while it runs, the modal open (Hooks.Invoke with the window table) gets a visible
    /// left button when the mode left it hidden, with the defeat window's own texts and a plain close. Lua of the local script only.
    /// </summary>
    [HarmonyPatch(typeof(Brix.Assets.Asset_Gamemode), "Disk_LoadGamemode")]
    internal static class VictorySpectatePatch
    {
        private const string Lua =
            "if ShowVictoryDialog ~= nil then " +
            "  local orig = ShowVictoryDialog " +
            "  ShowVictoryDialog = function(...) " +
            "    local inv = Hooks.Invoke " +
            "    Hooks.Invoke = function(h, a) " +
            "      if type(a) == 'table' and type(a[1]) == 'table' and a[1].btn_left ~= nil and a[1].btn_right ~= nil then " +
            "        local t = a[1] " +
            "        local shown = t.btn_left.Visible ~= nil and t.btn_left.Visible() " +
            "        if not shown then " +
            "          t.btn_left.Visible = function() return true end " +
            "          t.btn_left.OnAction = function() inv(UIGame.hk_CloseModalMenu) end " +
            "          t.btn_left.lbl_top = { Text = function() return GetLocalized('##mode_conquest_defeat_continue'):upper() end } " +
            "          t.btn_left.lbl_bot = { Text = function() return GetLocalized('##mode_conquest_defeat_continue_info') end } " +
            "        end " +
            "      end " +
            "      return inv(h, a) " +
            "    end " +
            "    local ok, err = pcall(orig, ...) " +
            "    Hooks.Invoke = inv " +
            "    if not ok then error(err, 0) end " +
            "  end " +
            "end";

        private static void Postfix(Brix.Assets.Asset_Gamemode __instance, ref MoonSharp.Interpreter.Script outscript)
        {
            try
            {
                if (outscript == null) return;
                if (__instance != null && __instance.codename == "tutorial") return;
                outscript.DoString(Lua, null, "btc_victoryspectate");
            }
            catch (Exception e) { BtcLog.Error("Hook", "VictorySpectate: " + e.Message); }
        }
    }
}
