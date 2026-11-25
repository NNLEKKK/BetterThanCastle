using System;
using Brix.Game.AI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// Toolbag is a plain MonoBehaviour: the pool never clears its slot references. A unit handed out again still points at its old
    /// tools, whose GameObjects the pool may already have given to another unit. Equipping into that slot then "unequips" the old
    /// reference (ClasseMoi.Move) and pulls a LIVE tool off someone else: that unit looks like a worker although it is armed.
    /// Pure client: a slot reference that is not on this unit's body is dropped instead of unequipped. Local look only.
    /// </summary>
    [HarmonyPatch(typeof(Toolbag), "OnEquipTool")]
    internal static class ToolbagStaleSlotPatch
    {
        private static void Prefix(Toolbag __instance, GameObject __0)
        {
            if (NetworkServer.active || __0 == null) return;
            try
            {
                Slot slot = Grip.SlotOf(__0);
                if (slot == Slot.Invalid) return;
                Toolbag.ToolLimbPair p = __instance.GetToolLimbPair(slot);
                if (p == null || p.Tool == null || p.Tool == __0 || p.Tool.transform.IsChildOf(__instance.transform)) return;
                BtcLog.Info("TrackFix", "stale toolbag slot " + slot + " on " + __instance.name + " -> " + p.Tool.name + " (not on this unit), dropped");
                p.Tool = null;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Toolbag.OnEquipTool stale slot: " + e.Message); }
        }
    }
}
