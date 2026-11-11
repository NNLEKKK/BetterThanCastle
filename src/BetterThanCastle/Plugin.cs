using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace BetterThanCastle
{
    [BepInPlugin(Guid, Name, BepVersion)]
    public class BtcPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.btc.betterthancastle";
        public const string Name = "Better Than Castle";
        public const string Version = "0.4.1";
        // BepInEx parses the plugin version with System.Version: no letter suffix allowed here (the mod is skipped otherwise)
        public const string BepVersion = "0.4.1";

        internal static ManualLogSource Log;
        internal static Harmony Harmony;

        internal static BtcPlugin Inst;

        private void Awake()
        {
            Inst = this;
            Log = Logger;
            BtcConfig.Init(Config);
#if DEV
            Dev.BtcDevTools.Init();
#endif
            Harmony = new Harmony(Guid);
            BtcPatcher.Apply(Harmony, typeof(BtcPlugin).Assembly);
#if DEV
            Dev.BtcDevProbe.Init();
            Dev.BtcDevLoadProbe.Init();
            Dev.BtcDevMethodProf.Init();
            Dev.BtcDevMenuProbe.Init();
            Dev.BtcDevLuaFileProbe.Init();
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
