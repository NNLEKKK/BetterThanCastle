#if DEV
using System;
using System.Diagnostics;
using Brix.Engine;
using Brix.Legacy;
using HarmonyLib;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV: who removes trees during replay playback (position, fast feed flag, callers).</summary>
    [HarmonyPatch(typeof(TreeRegistry), "Remove")]
    internal static class BtcDevTreeProbe
    {
        private static int _n;

        private static void Postfix(XYZ position, bool __result)
        {
            try
            {
                if (!BtcReplay.Running || _n >= 60) return;
                _n++;
                StackTrace st = new StackTrace(2, false);
                string c = "";
                for (int i = 0; i < st.FrameCount && i < 4; i++) { var m = st.GetFrame(i).GetMethod(); if (m != null) c += m.DeclaringType.Name + "." + m.Name + " < "; }
                BtcLog.Info("TreeProbe", (__result ? "removed " : "NOT removed ") + position + (BtcReplaySeek.FastFeed ? " fast" : "") + " t=" + BtcReplay.PlayClock.ToString("0.0") + " by " + c);
            }
            catch (Exception) { }
        }
    }
}
#endif
