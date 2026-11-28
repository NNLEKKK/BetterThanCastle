using Brix.Game.AI;
using Brix.Legacy;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
	// Rule "work.taskR" (host rule, BC ON): bigger circle tasks (harvest, gather, clean up, repair...). Vanilla limits the circle to
	// Project.MaxSize = 16 (drag handle: Beacon.InstantiateCircleDrag -> CircleDrag.maxRadius) and clamps every radius set to
	// Beacon.MaxRadius (host CmdAccepterRayon, client NetworkRadius sync). We only raise both limits, never lower them.
	// Flatten/quarry boxes (RectZone) are not touched: Flatten has its own rule (BtcFlattenHigh).
	internal static class BtcTaskRadius
	{
		private const int Vanilla = 16;
		private const int Limit = 60;
		private static int _idx = -2;

		internal static void AddRows()
		{
			BtcGameplay.Add("work.taskR", "eco", "Max radius of circle tasks", "Макс. радиус круглых заданий", Limit, Vanilla, Limit, 1f, false,
				"Harvest, gather, clean up and the other tasks with a circle can be dragged up to this radius. Vanilla is 16.",
				"Сбор урожая, сбор ресурсов, уборку и другие задания с кругом можно растянуть до этого радиуса. В ваниле 16.");
		}

		internal static int Max()
		{
			if (!BtcNetworkGate.Enabled) return Vanilla;
			if (_idx == -2) _idx = BtcGameplay.Idx("work.taskR");
			float[] a = BtcGameplay.Active;
			if (_idx < 0 || a == null || _idx >= a.Length) return Vanilla;
			return Mathf.Clamp(Mathf.RoundToInt(a[_idx]), Vanilla, Limit);
		}

		internal static void Raise(Beacon b)
		{
			if (b == null) return;
			int m = Max();
			if (m <= Vanilla) return;
			if (b.MaxRadius < m) b.MaxRadius = m;
			Project p = b.project;
			if (p != null && p.MaxSize < m) p.MaxSize = m;
		}
	}

	[HarmonyPatch(typeof(Beacon), "InstantiateCircleDrag")]
	internal static class TaskRadiusDragPatch
	{
		private static void Prefix(Beacon __instance)
		{
			try { BtcTaskRadius.Raise(__instance); }
			catch (System.Exception e) { BtcLog.Error("Hook", "TaskRadius.Drag: " + e.Message); }
		}
	}

	// Beacon.Init runs on host and clients when the beacon is set up; the radius setter covers loads and late rule changes
	// (it may be inlined by Mono at some call sites, so Init + InstantiateCircleDrag raise the limit too).
	[HarmonyPatch(typeof(Beacon), "Init")]
	internal static class TaskRadiusInitPatch
	{
		private static void Prefix(Beacon __instance)
		{
			try { BtcTaskRadius.Raise(__instance); }
			catch (System.Exception e) { BtcLog.Error("Hook", "TaskRadius.Init: " + e.Message); }
		}
	}

	[HarmonyPatch(typeof(Beacon), "radius", MethodType.Setter)]
	internal static class TaskRadiusSetPatch
	{
		private static void Prefix(Beacon __instance)
		{
			try { if (__instance.MaxRadius < 60) BtcTaskRadius.Raise(__instance); }
			catch (System.Exception e) { BtcLog.Error("Hook", "TaskRadius.Set: " + e.Message); }
		}
	}
}
