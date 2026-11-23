using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// Smooth unit animations (BtcAnimNet). Registry from Awake; on a client that receives msg 124 for an animator, the vanilla
    /// bool SyncVar hook and the trigger/reset/play messages are ignored for that animator (the host's batches carry the same
    /// information without the 10 Hz sampling and the trigger queue). Everything else, and BC OFF, runs vanilla.
    [HarmonyPatch(typeof(UNetAnimator), "Awake")]
    internal static class UNetAnimatorAwakePatch
    {
        static void Postfix(UNetAnimator __instance)
        {
            try { BtcAnimNet.OnAwake(__instance); }
            catch (Exception e) { BtcLog.Error("AnimNet", "awake: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(UNetAnimator), "stateChanged")]
    internal static class UNetAnimatorStateChangedPatch
    {
        static bool Prefix(UNetAnimator __instance)
        {
            try { return !BtcAnimNet.Receiving || !BtcAnimNet.Owns(__instance); }
            catch (Exception e) { BtcLog.Error("AnimNet", "stateChanged: " + e.Message); return true; }
        }
    }

    internal static class UNetAnimatorMsgGate
    {
        internal static UNetAnimator Of(NetworkInstanceId id)
        {
            GameObject go = ClientScene.FindLocalObject(id);
            return go != null ? go.GetComponent<UNetAnimator>() : null;
        }
    }

    [HarmonyPatch(typeof(UNetAnimator), "triggerCallBack")]
    internal static class UNetAnimatorTriggerPatch
    {
        static bool Prefix(NetworkMessage message)
        {
            if (!BtcAnimNet.Receiving) return true;
            try
            {
                UNetAnimator.UnetAnimatorTrigger m = message.ReadMessage<UNetAnimator.UnetAnimatorTrigger>();
                UNetAnimator ua = UNetAnimatorMsgGate.Of(m.netId);
                if (ua != null && !BtcAnimNet.Owns(ua)) BtcAnimNet.VanillaQueue(ua, m.hash);
            }
            catch (Exception e) { BtcLog.Error("AnimNet", "trigger: " + e.Message); }
            return false;
        }
    }

    [HarmonyPatch(typeof(UNetAnimator), "resetTriggerCallback")]
    internal static class UNetAnimatorResetTriggerPatch
    {
        static bool Prefix(NetworkMessage message)
        {
            if (!BtcAnimNet.Receiving) return true;
            try
            {
                UNetAnimator.UnetAnimatorResetTrigger m = message.ReadMessage<UNetAnimator.UnetAnimatorResetTrigger>();
                UNetAnimator ua = UNetAnimatorMsgGate.Of(m.netId);
                if (ua != null && !BtcAnimNet.Owns(ua)) BtcAnimNet.VanillaQueue(ua, 59);
            }
            catch (Exception e) { BtcLog.Error("AnimNet", "reset: " + e.Message); }
            return false;
        }
    }

    [HarmonyPatch(typeof(UNetAnimator), "resetExitTriggerCallback")]
    internal static class UNetAnimatorResetExitTriggerPatch
    {
        static bool Prefix(NetworkMessage message)
        {
            if (!BtcAnimNet.Receiving) return true;
            try
            {
                UNetAnimator.UnetAnimatorResetTrigger m = message.ReadMessage<UNetAnimator.UnetAnimatorResetTrigger>();
                UNetAnimator ua = UNetAnimatorMsgGate.Of(m.netId);
                if (ua != null && !BtcAnimNet.Owns(ua)) BtcAnimNet.VanillaQueue(ua, 62);
            }
            catch (Exception e) { BtcLog.Error("AnimNet", "reset exit: " + e.Message); }
            return false;
        }
    }

    [HarmonyPatch(typeof(UNetAnimator), "playCallback")]
    internal static class UNetAnimatorPlayPatch
    {
        static bool Prefix(NetworkMessage message)
        {
            if (!BtcAnimNet.Receiving) return true;
            try
            {
                UNetAnimator.UnetAnimatorPlay m = message.ReadMessage<UNetAnimator.UnetAnimatorPlay>();
                UNetAnimator ua = UNetAnimatorMsgGate.Of(m.netId);
                if (ua != null && ua.animator != null && !BtcAnimNet.Owns(ua)) ua.animator.Play(m.hash);
            }
            catch (Exception e) { BtcLog.Error("AnimNet", "play: " + e.Message); }
            return false;
        }
    }
}
