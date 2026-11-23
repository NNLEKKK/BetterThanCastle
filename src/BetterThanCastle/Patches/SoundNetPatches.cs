using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Audio;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.AI.Nodes;
using Brix.Game.Locomotion;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle.Patches
{
    /// <summary>Every unit/object sound: fog-of-war mute, host relay capture, client double check (BtcSoundNet.OnPlay).</summary>
    [HarmonyPatch(typeof(SoundEngine), "Play", new Type[] { typeof(string), typeof(GameObject) })]
    internal static class SoundEnginePlaySoundNetPatch
    {
        private static bool Prefix(string name, GameObject emitter)
        {
            try { return BtcSoundNet.OnPlay(name, emitter); }
            catch (Exception e) { BtcLog.Error("SoundNet", "play: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(SoundEngine), "Play", new Type[] { typeof(SoundEngine.PlayInfo) })]
    internal static class SoundEnginePlayInfoSoundNetPatch
    {
        private static bool Prefix(SoundEngine.PlayInfo info)
        {
            try { return info.emitter == null || !BtcSoundNet.FogOn || BtcSoundNet.Audible(info.emitter); }
            catch (Exception) { return true; }
        }
    }

    [HarmonyPatch(typeof(SoundEngine), "SetSwitch", new Type[] { typeof(string), typeof(string), typeof(GameObject) })]
    internal static class SoundEngineSetSwitchSoundNetPatch
    {
        private static void Prefix(string group, string data, GameObject emitter)
        {
            if (BtcSoundNet.Relay > 0) BtcSoundNet.OnHostSwitch(group, data, emitter);
        }
    }

    /// <summary>
    /// Host-only code that plays unit sounds (lab snd-mp: never heard on a client): aiming, spotting an enemy, arrow hits, the kill
    /// cry, thoughts, end-of-task cries, rack equip and the SoundController queues (shots, racks, workbenches, spawns).
    /// Sounds the client also plays itself are matched 1:1 on the client.
    /// </summary>
    [HarmonyPatch]
    internal static class SoundNetRelayScopePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            string[] archery = { "StartAiming", "AbortAimingOnHurt", "EndAimingOnHurtSmall", "EndAimingOnHurtBig", "ShootAt", "AimingToIdleArchery" };
            for (int i = 0; i < archery.Length; i++) yield return AccessTools.Method(typeof(Archery), archery[i]);
            yield return AccessTools.Method(typeof(AttackInfo), "SpotEnemy");
            yield return AccessTools.Method(typeof(KinematicProjectile), "PlayArcherSound");
            yield return AccessTools.Method(typeof(BricktronDamageReceiver), "_Kill");
            yield return AccessTools.Method(typeof(Thinker), "PlaySound");
            yield return AccessTools.Method(typeof(Goal), "EndWork");
            yield return AccessTools.Method(typeof(ToolNode), "EquipRackContent");
            yield return AccessTools.Method(typeof(SoundController), "PlayQueuedEvents");
        }

        private static void Prefix(out bool __state)
        {
            __state = BtcSoundNet.HostRelay && NetworkServer.active;
            if (__state) BtcSoundNet.Relay++;
        }

        private static void Finalizer(bool __state)
        {
            if (__state && BtcSoundNet.Relay > 0) BtcSoundNet.Relay--;
        }
    }

    [HarmonyPatch(typeof(Occupation), "Start")]
    internal static class OccupationStartSoundNetPatch
    {
        private static void Postfix(Occupation __instance)
        {
            try { BtcSoundNet.OnOccupationStart(__instance); }
            catch (Exception e) { BtcLog.Error("SoundNet", "occ start: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Occupation), "SetOccupation")]
    internal static class OccupationSetSoundNetPatch
    {
        private static void Postfix(Occupation __instance)
        {
            try { BtcSoundNet.OnHostOccupationChanged(__instance); }
            catch (Exception e) { BtcLog.Error("SoundNet", "occ set: " + e.Message); }
        }
    }

    /// <summary>Client: the host job wins over the local toolbag guess; host: the change is sent and the voice switch follows it.</summary>
    [HarmonyPatch(typeof(Occupation), "UpdateOccupationTool")]
    internal static class OccupationToolSoundNetPatch
    {
        private static bool Prefix(Occupation __instance)
        {
            try { return BtcSoundNet.ClientToolChanged(__instance); }
            catch (Exception e) { BtcLog.Error("SoundNet", "occ tool: " + e.Message); return true; }
        }

        private static void Postfix(Occupation __instance)
        {
            try { BtcSoundNet.OnHostOccupationChanged(__instance); }
            catch (Exception e) { BtcLog.Error("SoundNet", "occ tool post: " + e.Message); }
        }
    }
}
