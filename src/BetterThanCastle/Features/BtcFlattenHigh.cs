using Brix.Game.AI;
using Brix.Game.AI.UI;
using Brix.Engine;
using Brix.Game.Network;
using Brix.Input;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
	// Rule "work.flatHigh" (host rule): the fill line of a vanilla Flatten task can be raised above the terrain. Vanilla clamps the
	// threshold plane to the task box (its top = highest terrain in the box), so a mound cannot be higher than the land around.
	// With the rule the plane may go up (to the world top) and the box grows to hold the line, so goals exist up to it.
	// Terraform tasks (own shape) are not touched.
	internal static class BtcFlattenHigh
	{
		private const int WorldTop = 400;
		private static int _idx = -2;

		internal static void AddRows()
		{
			BtcGameplay.Add("work.flatHigh", "eco", "Flatten: raise the fill line freely", "Выравнивание: насыпь на любую высоту", 1f, 0f, 1f, 1f, true,
				"The fill line of the vanilla Flatten task is no longer limited by the highest terrain in its box: drag it as high as you like and the workers build the mound up to it. The box size is free too: from 1x1 up to 64x64 (vanilla: 3x3 to 16x16).",
				"Линию насыпи ванильного задания Выровнять больше не ограничивает самая высокая точка земли в его рамке: тяни её куда угодно, рабочие насыпят холм до неё. Размер рамки тоже свободный: от 1x1 до 64x64 (в ванили от 3x3 до 16x16).");
		}

		internal static bool On
		{
			get
			{
				if (_idx == -2) _idx = BtcGameplay.Idx("work.flatHigh");
				float[] a = BtcGameplay.Active;
				return _idx >= 0 && a != null && _idx < a.Length && a[_idx] > 0.5f;
			}
		}

		internal static void Reset() { _idx = -2; }

		internal static bool IsLandscape(Component c)
		{
			return c != null && c.GetComponentInParent<LandscapeProjectUI>() != null;
		}
	}

	[HarmonyPatch(typeof(DraggableVolumePlane), "SetY")]
	internal static class FlattenHighSetYPatch
	{
		private static readonly AccessTools.FieldRef<DraggableVolumePlane, int> YRef = AccessTools.FieldRefAccess<DraggableVolumePlane, int>("Y");
		private static readonly AccessTools.FieldRef<DraggableVolumePlane, int> MinRef = AccessTools.FieldRefAccess<DraggableVolumePlane, int>("relativeMinY");

		private static bool Prefix(DraggableVolumePlane __instance, int y)
		{
			try
			{
				if (!BtcFlattenHigh.On || !BtcFlattenHigh.IsLandscape(__instance)) return true;
				var vol = AccessTools.Property(typeof(DraggableVolumePlane), "Volume").GetValue(__instance, null);
				if (vol == null) return true;
				int min = ((VoxelBounds)vol).Bounds.min.y - MinRef(__instance);
				YRef(__instance) = Mathf.Clamp(y, min, 400);
				AccessTools.Method(typeof(DraggableVolumePlane), "UpdateVisuals").Invoke(__instance, null);
				return false;
			}
			catch (System.Exception e) { BtcLog.Error("Hook", "FlattenHigh.SetY: " + e.Message); return true; }
		}
	}

	[HarmonyPatch(typeof(UNetGroupe), "CmdSetLandscape")]
	internal static class FlattenHighCmdPatch
	{
		private static void Prefix(GameObject instance, ref Vector3 max, int threshold)
		{
			try
			{
				if (!BtcFlattenHigh.On || instance == null) return;
				LandscapeGoalProvider p = instance.GetComponent<LandscapeGoalProvider>();
				if (p == null || BtcTerraform.Of(p) != null) return;
				if (max.y < threshold) max.y = Mathf.Min(threshold, 400);
			}
			catch (System.Exception e) { BtcLog.Error("Hook", "FlattenHigh.Cmd: " + e.Message); }
		}
	}
}

namespace BetterThanCastle
{
	// The same rule frees the box size: vanilla keeps a Flatten box between 3x3 and 16x16 (RectZone.OnDragEnd -> ExtendToPoint(.., 3, 16)).
	[HarmonyPatch(typeof(RectZone), "OnDragEnd")]
	internal static class FlattenHighSizePatch
	{
		internal static int MinFor(RectZone z) { return BtcFlattenHigh.On && BtcFlattenHigh.IsLandscape(z) ? 1 : 3; }
		internal static int MaxFor(RectZone z) { return BtcFlattenHigh.On && BtcFlattenHigh.IsLandscape(z) ? 64 : 16; }

		private static System.Collections.Generic.IEnumerable<CodeInstruction> Transpiler(System.Collections.Generic.IEnumerable<CodeInstruction> ins)
		{
			var list = new System.Collections.Generic.List<CodeInstruction>(ins);
			int n = 0;
			for (int i = 0; i < list.Count; i++)
			{
				var c = list[i];
				if (c.opcode == System.Reflection.Emit.OpCodes.Ldc_I4_3)
				{
					list[i] = new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0);
					list.Insert(i + 1, new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AccessTools.Method(typeof(FlattenHighSizePatch), "MinFor")));
					n++; i++;
				}
				else if ((c.opcode == System.Reflection.Emit.OpCodes.Ldc_I4_S || c.opcode == System.Reflection.Emit.OpCodes.Ldc_I4) && System.Convert.ToInt32(c.operand) == 16)
				{
					list[i] = new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0);
					list.Insert(i + 1, new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AccessTools.Method(typeof(FlattenHighSizePatch), "MaxFor")));
					n++; i++;
				}
			}
			if (n != 2) { BtcLog.Error("Hook", "FlattenHigh.Size: expected 2 constants, found " + n); return ins; }
			return list;
		}
	}
}
