using System;
using Brix.Game.Semantique;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Alt overlay (cam_show_overheads): a filtered tool rack shows its kit icon as the big icon instead of the first item's icon.
    [HarmonyPatch(typeof(RecepteurOverheadDisplayDriver), "SetBigIcon")]
    internal static class RecepteurOverheadSetBigIconPatch
    {
        private static void Postfix(RecepteurOverheadDisplayDriver __instance)
        {
            try
            {
                if (!BtcNetworkGate.Enabled) return;
                ToolRack r = __instance.GetComponent<ToolRack>();
                if (r == null) return;
                int kit = BtcRackFilter.Get(r);
                if (kit < 0) return;
                Sprite s = BtcRackFilter.KitSprite(kit);
                if (s == null || __instance.overheadDisplay == null || !__instance.overheadDisplay.BigIcon.IsSpawned) return;
                __instance.overheadDisplay.BigIcon.Get.SetIcon(s);
                __instance.overheadDisplay.ShowBigIcon(true);
                __instance.overheadDisplay.BigIcon.Get.Show = true;
            }
            catch (Exception e) { BtcLog.Error("Hook", "RecepteurOverheadDisplayDriver.SetBigIcon: " + e); }
        }
    }
}
