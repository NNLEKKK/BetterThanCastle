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

    /// Client spawn applies the bool flags with stateChanged(state), which only writes bits that differ from previous_state. That
    /// field is never reset when the pool hands the unit out again (fog re-show, replay seek, rejoin), so bits equal to the OLD life
    /// were skipped and the animator kept wrong bools (units stuck in the run cycle). Every bit counts as changed here. Local look only.
    [HarmonyPatch(typeof(UNetAnimator), "OnStartPureClient")]
    internal static class UNetAnimatorSpawnFlagsPatch
    {
        internal static readonly AccessTools.FieldRef<UNetAnimator, ulong> Prev = AccessTools.FieldRefAccess<UNetAnimator, ulong>("previous_state");

        static void Prefix(UNetAnimator __instance)
        {
            try { Prev(__instance) = ~__instance.state; }
            catch (Exception e) { BtcLog.Error("Hook", "UNetAnimator spawn flags: " + e.Message); }
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

        /// The vanilla hook never writes the SyncVar field, so on a client `state` keeps the spawn-time bits forever. Anything that
        /// serialises the unit locally (replay keyframes: a unit respawned from one ran with its FIRST flags, e.g. stuck in the run
        /// cycle) or reads it got stale data. Keep it equal to what the hook applied. Client-local field, nothing goes on the wire.
        static void Postfix(UNetAnimator __instance, ulong __0)
        {
            if (!NetworkServer.active) __instance.state = __0;
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
