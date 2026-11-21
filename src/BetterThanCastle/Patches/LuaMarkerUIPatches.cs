using System;
using Brix.Game.Components;
using HarmonyLib;

namespace BetterThanCastle
{
    /// <summary>A marker without a group (seen on clients that rejoin or spectate a running game) threw ArgumentNullException
    /// from LuaMarkerUI.Start every time (Dictionary key null) and the rest of Start never ran. Treat it as count 0.</summary>
    [HarmonyPatch(typeof(LuaMarkerUI), "GetMarkerCount")]
    internal static class LuaMarkerUIGetMarkerCountPatch
    {
        private static bool Prefix(string m, ref int __result)
        {
            try
            {
                if (m != null) return true;
                __result = 0;
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "LuaMarkerUI.GetMarkerCount: " + e); return true; }
        }
    }
}
