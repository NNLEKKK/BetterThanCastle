using System;
using System.Reflection;
using Brix.Game.Network;
using HarmonyLib;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay fast feed only (seek, instant seek preprocess, jump remainder): a client UNetTransform only lerps towards the server pose
    /// in Update, so after many updates per frame units lag far behind. Items they carry (wards, kits) get their position from the hand
    /// at the moment they are dropped (wards have no synced position), so fast-fed worlds and their keyframes had wards metres away
    /// from where they were placed (user: phantom wards next to the real spot). Each update snaps to the server pose instead.
    /// </summary>
    [HarmonyPatch(typeof(UNetTransform), "OnDeserialize")]
    internal static class UNetTransformFastFeedPatch
    {
        private static readonly FieldInfo Pos = typeof(UNetTransform).GetField("LastServerPosition", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo Rot = typeof(UNetTransform).GetField("LastServerRotation", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static void Postfix(UNetTransform __instance, bool initialState)
        {
            try
            {
                if (initialState || Pos == null || Rot == null || !BtcReplay.Running || !BtcReplaySeek.FastFeed) return;
                __instance.transform.position = (UnityEngine.Vector3)Pos.GetValue(__instance);
                __instance.transform.rotation = (UnityEngine.Quaternion)Rot.GetValue(__instance);
            }
            catch (Exception e) { BtcLog.Error("Hook", "UNetTransformFastFeed: " + e.Message); }
        }
    }
}
