using System;
using System.Reflection;
using Brix.Lua;
using Brix.UI.Builder.Menu;
using HarmonyLib;
using MoonSharp.Interpreter;

namespace BetterThanCastle.Patches
{
    // Appends the cleanup-task filter buttons to the game's own GameMenu.lua text when the script loader hands it out.
    [HarmonyPatch]
    internal static class BrixScriptLoaderLoadFilePatch
    {
        private static MethodBase TargetMethod()
        {
            foreach (MethodInfo m in typeof(BrixScriptLoader).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (m.Name.EndsWith("LoadFile", StringComparison.Ordinal)) return m;
            return null;
        }

        private static void Postfix(string file, ref object __result)
        {
            try
            {
                string s = __result as string;
                if (s == null || file == null) return;
                if (!file.Replace('\\', '/').EndsWith("LUI/Menus/GameMenu.lua", StringComparison.OrdinalIgnoreCase)) return;
                __result = BtcCleanFilter.Inject(s);
            }
            catch (Exception ex) { BtcLog.Error("CleanFilter", "LoadFile: " + ex.Message); }
        }
    }

    // The Lua side calls back into C# through three globals.
    [HarmonyPatch]
    internal static class LuaMenuReadMenuTablePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(LuaMenu), "ReadMenuTable", new Type[] { typeof(Script).MakeByRefType(), typeof(string), typeof(Action<Table, Script>) });
        }

        private static void Postfix(Script outscript)
        {
            try { BtcCleanFilter.Install(outscript); }
            catch (Exception ex) { BtcLog.Error("CleanFilter", "ReadMenuTable: " + ex.Message); }
        }
    }
}
