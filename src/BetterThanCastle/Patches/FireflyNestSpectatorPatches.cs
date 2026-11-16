using System;
using Brix.Game.Components;
using HarmonyLib;
using MoonSharp.Interpreter;

namespace BetterThanCastle.Patches
{
    // The game HUD (GameMenu idle task panel) asks Lua FireflyNest.GetInfo for the local player's home crystal while the level
    // loads. A spectator who joins a running game (BC ON only) has no home crystal: vanilla throws "no firefly nest found" and the
    // spectator's load aborts at "Entering World". Give the HUD an empty answer instead (BC ON only; OFF keeps the vanilla throw).
    [HarmonyPatch(typeof(FireflyNest), "lua_GetInfo")]
    internal static class FireflyNestGetInfoSpectatorPatch
    {
        private static Exception Finalizer(Exception __exception, ScriptExecutionContext context, ref DynValue __result)
        {
            if (__exception == null || !FireflyNestSpectator.NoNest(__exception)) return __exception;
            try
            {
                DynValue t = DynValue.NewTable(context.OwnerScript);
                t.Table.Set("currentXp", DynValue.NewNumber(0));
                t.Table.Set("totalXp", DynValue.NewNumber(1));
                t.Table.Set("spawnableCount", DynValue.NewNumber(0));
                t.Table.Set("autoRespawnFireflyCount", DynValue.NewNumber(0));
                __result = t;
                FireflyNestSpectator.LogOnce();
                return null;
            }
            catch (Exception e) { BtcLog.Error("Hook", "FireflyNest.GetInfo (no nest): " + e.Message); return __exception; }
        }
    }

    [HarmonyPatch(typeof(FireflyNest), "lua_SpawnUnit")]
    internal static class FireflyNestSpawnUnitSpectatorPatch
    {
        private static Exception Finalizer(Exception __exception, ref DynValue __result)
        {
            if (__exception == null || !FireflyNestSpectator.NoNest(__exception)) return __exception;
            __result = DynValue.Void;
            return null;
        }
    }

    internal static class FireflyNestSpectator
    {
        private static bool _logged;

        internal static bool NoNest(Exception e)
        {
            return BtcNetworkGate.Enabled && e.GetType() == typeof(Exception) && e.Message == "no firefly nest found";
        }

        internal static void LogOnce()
        {
            if (_logged) return;
            _logged = true;
            BtcLog.Info("Spec", "FireflyNest.GetInfo: local faction has no home crystal (spectator), HUD gets zeros");
        }
    }
}
