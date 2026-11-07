using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay playback only: a client CaptureClock polls its synced CaptureProgress once per IncrementTime of game time and derives
    /// capture start/ratio/end events from it (crystal creep, colour transition, vibrate). A fast feed (seek, instant seek preprocess,
    /// jump remainder) delivers many progress updates per poll, so starts/ends were swallowed and crystals stuck mid-transition in
    /// keyframes. While feeding fast, every progress update runs the clock step once, like normal viewing does.
    /// </summary>
    [HarmonyPatch(typeof(CaptureProgress), "OnDeserialize")]
    internal static class CaptureProgressFastFeedPatch
    {
        private static readonly FieldInfo Capture = typeof(CaptureClock).GetField("capture", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly MethodInfo Step = typeof(CaptureClock).GetMethod("OnTimeIncrement", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static void Postfix(CaptureProgress __instance, bool initialState)
        {
            try
            {
                if (initialState || !BtcReplay.Running || !BtcReplaySeek.FastFeed || Step == null || Capture == null) return;
                CaptureClock c = __instance.GetComponent<CaptureClock>();
                if (c == null) c = __instance.GetComponentInParent<CaptureClock>();
                if (c == null || !ReferenceEquals(Capture.GetValue(c), __instance)) return;
                Step.Invoke(c, null);
            }
            catch (Exception e) { BtcLog.Error("Hook", "CaptureProgressFastFeed: " + e.Message); }
        }
    }
}

namespace BetterThanCastle
{
    /// <summary>Replay fast feed: a capture that ends before the creep lerp (CrystalVisuals.Update) caught up leaves the creep short;
    /// normal viewing has it at the last progress by then.</summary>
    [HarmonyPatch(typeof(CrystalVisuals), "EndTransition")]
    internal static class CrystalVisualsFastEndPatch
    {
        private static void Postfix(CrystalVisuals __instance)
        {
            try
            {
                if (!BtcReplay.Running || !BtcReplaySeek.FastFeed) return;
                if (__instance.CaptureProgress > 0f) __instance.CreepProgression = __instance.CaptureProgress;
            }
            catch (Exception e) { BtcLog.Error("Hook", "CrystalVisualsFastEnd: " + e.Message); }
        }
    }
}
