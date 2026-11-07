using System;
using System.Collections.Generic;
using Brix.Game.Components;
using HarmonyLib;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Recording: objects flagged AlwaysInvisibleForNonAllied (armed bear traps) fail every vision test for the REC spectator, so they never
    /// reached the recording. The REC connections (local fake clients, nothing goes to real players) now observe them too. Playback needs no
    /// change: on a client IsVisibleForFaction(LocalUser.faction) is always true, and the POV view still hides them for non-allies (BtcPov.IsVisible).
    /// </summary>
    [HarmonyPatch(typeof(LocalFactionVision), "OnRebuildObservers")]
    internal static class TrapReplayObserversPatch
    {
        private static int _logged;

        private static void Postfix(LocalFactionVision __instance, HashSet<NetworkConnection> observers)
        {
            try
            {
                if (!__instance.AlwaysInvisibleForNonAllied) return;
                int n = observers.Count;
                BtcReplay.AddRecObservers(observers);
                if (observers.Count > n && _logged < 20)
                {
                    _logged++;
                    BtcLog.Info("ReplayTrap", "rec observes " + __instance.name);
                }
            }
            catch (Exception e) { BtcLog.Error("Hook", "TrapReplayObservers: " + e.Message); }
        }
    }

    /// <summary>Playback diagnostics: a trap object that reached the replay world (first 20 per session).</summary>
    [HarmonyPatch(typeof(BearTrapTrigger), "Start")]
    internal static class TrapReplayPlaybackLogPatch
    {
        private static int _logged;

        private static void Postfix(BearTrapTrigger __instance)
        {
            try
            {
                if (!BtcReplay.Active || _logged >= 20) return;
                _logged++;
                BtcLog.Info("ReplayTrap", "playback trap " + __instance.name + " at " + __instance.transform.position);
            }
            catch (Exception e) { BtcLog.Error("Hook", "TrapReplayPlaybackLog: " + e.Message); }
        }
    }
}
