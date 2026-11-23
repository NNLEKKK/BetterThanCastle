using System;
using System.Collections.Generic;
using Brix.Game.Components;
using Brix.Game.Components.Wards;
using Brix.Legacy;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Spawn state on clients (BC ON, local, no wire change). An object spawned on a client (match start, an enemy leaving the fog,
    /// a rejoin) gets its SyncVars as plain fields: UNET runs no hooks for the initial state, and these classes only update their
    /// look in the hook, so an open door looked closed, a shut trap open, a hot ward cold and a stockpile filter was not applied
    /// until the next change. One frame after the spawn the look is set from the fields, without the hooks' sounds.
    /// </summary>
    internal static class BtcSpawnState
    {
        private static readonly List<NetworkIdentity> Queue = new List<NetworkIdentity>();
        private static readonly List<NetworkIdentity> Work = new List<NetworkIdentity>();
        private static BtcSpawnStateRunner _runner;
        private static int _fixed;
        private static float _logAt;

        private static readonly AccessTools.FieldRef<Porte, Animation> DoorAnim = AccessTools.FieldRefAccess<Porte, Animation>("_childAnimation");
        private static readonly System.Reflection.FieldInfo TrapShut = AccessTools.Field(typeof(BearTrapTrigger), "isShut");
        private static readonly AccessTools.FieldRef<BearTrapTrigger, Animator> TrapAnim = AccessTools.FieldRefAccess<BearTrapTrigger, Animator>("_anim");
        private static readonly AccessTools.FieldRef<WardAttack, float> WardSpeed = AccessTools.FieldRefAccess<WardAttack, float>("anim_speed");
        private static readonly AccessTools.FieldRef<WardAttack, bool> WardGlow = AccessTools.FieldRefAccess<WardAttack, bool>("anim_glow");
        private static readonly AccessTools.FieldRef<WardAttack, bool> WardOverheat = AccessTools.FieldRefAccess<WardAttack, bool>("anim_overheat");
        private static readonly System.Reflection.FieldInfo RecChanged = AccessTools.Field(typeof(RecepteurState), "changed");

        internal static void OnClientSpawn(NetworkIdentity ni)
        {
            if (NetworkServer.active || ni == null || !BtcNetworkGate.Enabled) return;
            Queue.Add(ni);
            if (_runner == null)
            {
                GameObject go = new GameObject("BtcSpawnState");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<BtcSpawnStateRunner>();
            }
        }

        internal static void Tick()
        {
            if (Queue.Count == 0) return;
            Work.Clear();
            Work.AddRange(Queue);
            Queue.Clear();
            for (int i = 0; i < Work.Count; i++)
            {
                NetworkIdentity ni = Work[i];
                if (ni == null) continue;
                try { Apply(ni.gameObject); }
                catch (Exception e) { BtcLog.Warn("SpawnState", ni.name + ": " + e.Message); }
            }
            Work.Clear();
            if (_fixed > 0 && Time.realtimeSinceStartup > _logAt)
            {
                BtcLog.Info("SpawnState", "spawned objects set from their state: " + _fixed);
                _fixed = 0;
                _logAt = Time.realtimeSinceStartup + 30f;
            }
        }

        private static void Apply(GameObject go)
        {
            Porte door = go.GetComponent<Porte>();
            if (door != null && door._doorState == Porte.Etat.Ouverte)
            {
                Animation a = DoorAnim(door);
                if (a != null && a["Ouvrir"] != null) { a.Play("Ouvrir"); a["Ouvrir"].normalizedTime = 1f; _fixed++; }
            }
            BearTrapTrigger trap = go.GetComponent<BearTrapTrigger>();
            if (trap != null && TrapShut != null)
            {
                Animator an = TrapAnim(trap);
                string s = TrapShut.GetValue(trap).ToString();
                if (an != null && s != "open")
                {
                    an.SetBool("Half Shut", s == "halfshut");
                    an.SetBool("Shut", true);
                    _fixed++;
                }
            }
            WardAttack ward = go.GetComponent<WardAttack>();
            if (ward != null && ward.animator != null)
            {
                ward.animator.SetFloat("Speed", WardSpeed(ward));
                bool glow = WardGlow(ward);
                ward.animator.SetBool("Glow", glow);
                ward.animator.SetFloat("Glow Amount", glow ? ward.GlowIntensity : 0f);
                if (WardOverheat(ward) && ward.overheatParticles != null) ward.overheatParticles.Play();
                ward.HeatChangedSignal.Invoke();
                _fixed++;
            }
            RecepteurState rs = go.GetComponent<RecepteurState>();
            if (rs != null && RecChanged != null)
            {
                Brix.External.Signals.Signal sig = RecChanged.GetValue(rs) as Brix.External.Signals.Signal;
                if (sig != null) { sig.Invoke(); _fixed++; }
            }
        }
    }

    internal sealed class BtcSpawnStateRunner : MonoBehaviour
    {
        private void LateUpdate()
        {
            try { BtcSpawnState.Tick(); }
            catch (Exception e) { BtcLog.Error("SpawnState", "tick: " + e.Message); }
        }
    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(NetworkIdentity), "OnStartClient")]
    internal static class NetworkIdentityOnStartClientPatch
    {
        private static void Postfix(NetworkIdentity __instance) { try { BtcSpawnState.OnClientSpawn(__instance); } catch (Exception) { } }
    }
}
