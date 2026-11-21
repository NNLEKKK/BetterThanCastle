using System;
using Brix.Input;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Legacy: Start() ends with "if (BtcQol.CamLock) EnableEditorCamera();"
    [HarmonyPatch(typeof(CameraScript), "Start")]
    internal static class CameraScriptStartPatch
    {
        private static void Postfix(CameraScript __instance)
        {
            try
            {
                if (BtcQol.CamLock) __instance.EnableEditorCamera();
            }
            catch (Exception e) { BtcLog.Error("Hook", "CameraScript.Start: " + e); }
#if DEV       // multi-camera unfinished: DEV builds only (user order 2026-11-19)
            try { BtcMultiCam.Attach(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "CameraScript.Start multicam: " + e); }
#endif
        }
    }

    // Legacy: DisableEditorCamera() body skipped while CamLock is on.
    [HarmonyPatch(typeof(CameraScript), "DisableEditorCamera")]
    internal static class CameraScriptDisableEditorCameraPatch
    {
        private static bool Prefix()
        {
            try
            {
                return !BtcQol.CamLock;
            }
            catch (Exception e) { BtcLog.Error("Hook", "CameraScript.DisableEditorCamera: " + e); return true; }
        }
    }
}
