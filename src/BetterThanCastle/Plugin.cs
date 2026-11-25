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
        public const string Version = "0.6.0d";
        // BepInEx parses the plugin version with System.Version: no letter suffix allowed here (the mod is skipped otherwise)
        public const string BepVersion = "0.6.0";

        internal static ManualLogSource Log;
        internal static Harmony Harmony;

        internal static BtcPlugin Inst;

        private void Awake()
        {
            Inst = this;
            Log = Logger;
            System.Diagnostics.Stopwatch awake = System.Diagnostics.Stopwatch.StartNew();
            float sinceStart = UnityEngine.Time.realtimeSinceStartup;
            BtcConfig.Init(Config);
#if DEV
            Dev.BtcDevAlloc.Install(); // first: methods compiled before it never report their objects
            Dev.BtcDevTools.Init();
#endif
            long tPre = awake.ElapsedMilliseconds;
            Harmony = new Harmony(Guid);
            BtcPatcher.Apply(Harmony, typeof(BtcPlugin).Assembly);
            long tPatch = awake.ElapsedMilliseconds;
            BtcMemFix.Init();
            BtcLiveSymUI.Init();
            BtcPing.Init();
            BtcWormWardCraft.Init();
            long tInit = awake.ElapsedMilliseconds;
#if DEV
            Dev.BtcDevProbe.Init();
            Dev.BtcDevLoadProbe.Init();
            Dev.BtcDevLoadLine.Init();
            Dev.BtcDevMethodProf.Init();
            Dev.BtcDevMenuProbe.Init();
            Dev.BtcDevLuaFileProbe.Init();
            Dev.BtcDevAutoReplay.Init();
            Dev.BtcDevWindow.Init();
            Dev.BtcDevProf.Init();
            Dev.BtcDevCmd.Init();
#endif
            long tDev = awake.ElapsedMilliseconds;
            ResourcePackManager.Boot();
            BtcUpdater.Init(Info.Location);
            BtcReplayCleanup.RunAsync(false);
            Log.LogInfo("[BTC] loaded v" + Version + ", BetterConnection=" + BtcNetworkGate.Enabled + "; awake " + awake.ElapsedMilliseconds + " ms (pre " + tPre
                + ", patches " + (tPatch - tPre) + ", init " + (tInit - tPatch) + ", dev " + (tDev - tInit) + ", boot " + (awake.ElapsedMilliseconds - tDev) + "), started at " + sinceStart.ToString("0.00") + " s");
        }
    }
}
