using System;
using System.IO;
using System.Reflection;
using Brix.Assets;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Normal in-game map saves obey the Share-tab thumbnail scale slider (BtcSaveFormat.ThumbFactor; 1 = vanilla).
    [HarmonyPatch(typeof(Asset_Map), "Disk_WriteThumbnail")]
    internal static class AssetMapWriteThumbnailPatch
    {
        private static readonly MethodInfo PathGetter = AccessTools.Method(typeof(Asset_Map), "GetThumbnailPath");

        private static bool Prefix(Asset_Map __instance)
        {
            try
            {
                BtcSaveFormat.LoadSettings();
                int f = Math.Max(1, Math.Min(15, BtcSaveFormat.ThumbFactor));
                if (f <= 1 || __instance.thumbnail == null || PathGetter == null) return true;
                byte[] png = __instance.thumbnail.EncodeToPNG();
                byte[] small;
                if (!BtcSaveFormat.Downscale(png, f, out small) || small == null) return true;
                string path = (string)PathGetter.Invoke(__instance, null);
                File.WriteAllBytes(path, small);
                return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "Asset_Map.Disk_WriteThumbnail: " + e);
                return true;
            }
        }
    }
}
