using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace BetterThanCastle
{
    [BepInPlugin(Guid, Name, Version)]
    public class BtcPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.btc.betterthancastle";
        public const string Name = "Better Than Castle";
        public const string Version = "0.2.4";

        internal static ManualLogSource Log;
        internal static Harmony Harmony;

        private void Awake()
        {
            Log = Logger;
            BtcConfig.Init(Config);
#if DEV
            Dev.BtcDevTools.Init();
#endif
            Harmony = new Harmony(Guid);
            BtcPatcher.Apply(Harmony, typeof(BtcPlugin).Assembly);
#if DEV
            Dev.BtcDevProbe.Init();
            Dev.BtcDevAutoReplay.Init();
            Dev.BtcDevWindow.Init();
            Dev.BtcDevProf.Init();
            Dev.BtcDevCmd.Init();
#endif
            ResourcePackManager.Boot();
            BtcUpdater.Init(Info.Location);
            Log.LogInfo("[BTC] loaded v" + Version + ", BetterConnection=" + BtcNetworkGate.Enabled);
        }
    }
}
