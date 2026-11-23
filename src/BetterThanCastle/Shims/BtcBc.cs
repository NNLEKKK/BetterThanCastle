using System.Collections.Generic;
using Brix.Network;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Replaces the static "Bc*" surface the legacy mod bolted onto vanilla UNetManager.
    /// Settings are BepInEx config (BtcConfig, Network section); network behaviour is Net/BtcNetManager (wave 4).
    /// </summary>
    internal static class BtcBc
    {
        // ---- settings ----
        internal static bool BcEnabled { get { return BtcNetworkGate.Enabled; } }
        internal static bool BcShowPing { get { return BtcConfig.ShowPing.Value; } }
        internal static bool BcShowPauseLabel { get { return BtcConfig.ShowPauseLabel.Value; } }
        internal static bool BcDiagLog { get { return BtcConfig.DiagLog.Value; } }
        internal static bool BcCanChangeEnabled { get { return !NetworkServer.active && !NetworkClient.active; } }
        internal static bool BcIsHost { get { return NetworkServer.active; } }

        internal static void BcSetEnabled(bool value)
        {
            if (BtcConfig.BetterConnection.Value == value || !BcCanChangeEnabled) return;
            BtcConfig.BetterConnection.Value = value;
            BtcNetManager.OnBcToggled();   // legacy: ApplyBcTransportConfig + ApplyMaxBufferedPackets
            BtcLog.Info("Net", "BetterConnection set to " + value);
        }

        internal static void BcSetShowPing(bool value) { BtcConfig.ShowPing.Value = value; }
        internal static void BcSetShowPauseLabel(bool value) { BtcConfig.ShowPauseLabel.Value = value; }
        internal static void BcSetDiagLog(bool value) { BtcConfig.DiagLog.Value = value; }
        internal static bool BcAnimSync { get { return BtcConfig.AnimSync.Value; } }
        internal static void BcSetAnimSync(bool value) { BtcConfig.AnimSync.Value = value; }
        internal static bool BcPosSync { get { return BtcConfig.PosSync.Value; } }
        internal static void BcSetPosSync(bool value) { BtcConfig.PosSync.Value = value; }
        internal static bool BcSoundSync { get { return BtcConfig.SoundSync.Value; } }
        internal static void BcSetSoundSync(bool value) { BtcConfig.SoundSync.Value = value; }
        internal static bool BcFogSounds { get { return BtcConfig.FogSounds.Value; } }
        internal static void BcSetFogSounds(bool value) { BtcConfig.FogSounds.Value = value; }
        internal static bool BcOwnTransport { get { return BtcConfig.OwnTransport.Value; } }
        internal static void BcSetOwnTransport(bool value) { BtcConfig.OwnTransport.Value = value; }

        // ---- network state (BtcNetManager) ----
        internal static bool BcInSession { get { return BtcNetManager.InSession; } }
        internal static bool BcVoluntaryPauseActive { get { return BtcNetManager.VoluntaryPauseActive; } }
        internal static string BcVoluntaryPauseOwner { get { return BtcNetManager.VoluntaryPauseOwner; } }
        internal static bool BcClientsCanPause { get { return BtcNetManager.ClientsCanPause; } }
        internal static void BcTogglePause() { BtcNetManager.TogglePause(); }
        internal static void BcToggleClientPausePermission() { BtcNetManager.ToggleClientPausePermission(); }
        internal static string BcStatusText() { return BtcNetManager.StatusText(); }
        internal static List<string> BcGetPingLines() { return BtcNetManager.PingLines(); }

        /// <summary>Legacy UNetManager.BtcIsLoadingLevel (instance property).</summary>
        internal static bool IsLoadingLevel(object unetManager) { return BtcNetManager.IsLoadingLevel(unetManager as UNetManager); }
    }
}
