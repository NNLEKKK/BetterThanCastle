using System;

namespace BetterThanCastle
{
    /// <summary>
    /// The gamemode scoreboard menus (Gamemodes/*/Menu/*Menu.lua: player rows + the mini map) read the local faction's home crystal right
    /// after they build (_data.homeCrystal.availablePureEnergy). A spectator picked in the lobby has no home crystal: the menu threw
    /// "attempt to index a nil value", the scoreboard stayed empty and the spectator had no mini map. When the menu finds no own home crystal,
    /// it now borrows the first firefly home crystal of the map (spectators see everything anyway). Players keep the vanilla path.
    /// </summary>
    internal static class BtcSpecScoreboard
    {
        private const string Anchor = "_data.lv_currentXp = LiveValue.New()";
        private const string Fix = "if _data.homeCrystal == nil then for _, c in pairs(fy_Crystal:GetAll()) do if c.isFireflyHome then _data.homeCrystal = c break end end end ";

        /// BrixScriptLoader.LoadFile postfix (WormWardLoadFilePatch): source text of a Lua file before it is compiled.
        internal static string PatchLua(string file, string code)
        {
            if (code == null || file == null) return code;
            string f = file.Replace('\\', '/');
            // the foreign "conquest mod" gamemode is never touched
            if (f.IndexOf("/Gamemodes/", StringComparison.OrdinalIgnoreCase) < 0 || f.IndexOf("/Menu/", StringComparison.OrdinalIgnoreCase) < 0
                || f.IndexOf("conquest mod", StringComparison.OrdinalIgnoreCase) >= 0) return code;
            int i = code.IndexOf(Anchor, StringComparison.Ordinal);
            if (i < 0) return code;
            BtcLog.Info("Spec", "scoreboard spectator fallback added to " + f.Substring(f.LastIndexOf("/Gamemodes/", StringComparison.OrdinalIgnoreCase) + 1));
            return code.Substring(0, i) + Fix + code.Substring(i);
        }
    }
}
