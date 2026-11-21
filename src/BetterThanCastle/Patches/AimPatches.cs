using System;
using Brix.Game.Components;
using Brix.Input;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// Aim trainer: the camera is fixed while a run is on. CameraScript.Update is the only per-frame camera driver (inputs, damping, bounds):
    /// skipped and replaced by the fixed pose while BtcAim.Active. Outside the trainer this is vanilla (one bool check).
    /// </summary>
    [HarmonyPatch(typeof(CameraScript), "Update")]
    internal static class AimCameraLockPatch
    {
        private static bool Prefix(CameraScript __instance)
        {
            if (!BtcAim.CameraLocked) return true;
            try { BtcAim.PoseCamera(__instance); }
            catch (Exception e) { BtcLog.Error("Aim", "camera lock: " + e.Message); }
            return false;
        }
    }

    /// <summary>
    /// Aim trainer: a healed unit's HP ring is dropped from the dispenser at once but its object is released 1 s later ("linger"); a unit that
    /// is wounded again inside that second shows the stale ring, and the late release can take a reused pooled ring. Delay 0 while the trainer runs.
    /// </summary>
    [HarmonyPatch(typeof(SelectionCircleDispenser), "Remove", new Type[] { typeof(GameObject), typeof(float) })]
    internal static class AimRingNoLingerPatch
    {
        private static void Prefix(ref float delay)
        {
            if (BtcAim.Active) delay = 0f;
        }
    }

    /// <summary>
    /// Aim trainer: sandbox starts with 0 workers (sv_Settings.startingWorkersCount = 0 right before OnNewLevelLoaded). Removing the start
    /// workers afterwards made their fireflies respawn them on a cooldown. Local server Lua only, offline mode, nothing on the wire.
    /// </summary>
    [HarmonyPatch(typeof(Brix.Assets.Asset_Gamemode), "Disk_LoadGamemode")]
    internal static class AimNoStartWorkersPatch
    {
        private static void Postfix(ref MoonSharp.Interpreter.Script outscript)
        {
            if (!BtcAim.Active || outscript == null) return;
            try
            {
                outscript.DoString("if gamemode and gamemode.OnNewLevelLoaded then local f = gamemode.OnNewLevelLoaded; gamemode.OnNewLevelLoaded = function(...) " +
                                   "if sv_Settings then sv_Settings.startingWorkersCount = 0 end; " +
                                   // the empty map has no voxels around the start point: an error there must not stop the level from becoming ready
                                   "local ok, err = pcall(f, ...); if not ok then print('[BTC aim] OnNewLevelLoaded: ' .. tostring(err)) end end end");
                // the start-workers loop runs right after SetAsHomeCrystal (same hook point as the handicap): 0 there
                outscript.DoString("if SetAsHomeCrystal ~= nil then local s = SetAsHomeCrystal; SetAsHomeCrystal = function(c) s(c); if sv_Settings then sv_Settings.startingWorkersCount = 0 end end end");
                BtcLog.Info("Aim", "start workers set to 0");
            }
            catch (Exception e) { BtcLog.Warn("Aim", "start workers: " + e.Message); }
        }
    }

    /// <summary>Aim trainer: the home crystal hands out no workers (only the targets live on the floor).</summary>
    [HarmonyPatch(typeof(FireflyNest), "ProcessHomeSpawns")]
    internal static class AimNoHomeSpawnsPatch
    {
        private static bool Prefix() { return !BtcAim.Active; }
    }
}
