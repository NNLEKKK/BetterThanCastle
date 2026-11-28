using System;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
	[HarmonyPatch(typeof(Journee), "Update")]
	// rule gen.noCycle (BtcNoDayCycle): the host keeps the sun stopped
	internal static class JourneeNoCyclePatch
	{
		private static void Prefix(Journee __instance)
		{
			try { BtcNoDayCycle.Tick(__instance); }
			catch (Exception e) { BtcLog.Error("DayCycle", "tick: " + e.Message); }
		}
	}
}
