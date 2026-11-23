using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Engine;
using Brix.Game.AI;
using Brix.Game.Components.Volumes;
using Brix.Game.Locomotion;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Rule mov.allies: port of Castle Story Plus 0.10 ClimbPastAllies (Tricky12321, GPLv3).
    // InPlace.Update refuses to start a climb up / step down into a voxel where an ally stands
    // (IsBlockedByCharacterOrObstacle(false)); the unit re-searched every frame and stood up to 5 s. Now the climb or step is
    // started from the in place state at once for units that may pass allies (Profession.CanPassThrough). Others wait up to
    // PatienceSeconds (ChokePatienceSeconds where the ally's voxel is the only way, see BtcAllyPass chokes) for the ally to
    // move off, then give the walk up as the game does (PathBlockedAt), so BtcAllyPass closes the voxel and the next route
    // climbs beside it. Enemies, doors and other obstacles in the voxel are left to the game.
    [HarmonyPatch(typeof(InPlace), "Update")]
    [HarmonyPriority(Priority.Low)]
    internal static class BtcClimbPastAlliesPatch
    {
        internal const float PatienceSeconds = 1f;
        internal const float ChokePatienceSeconds = 6f;

        private sealed class Wait
        {
            public XYZ Voxel;
            public float Since;
            public float Last;
        }

        private static readonly Dictionary<int, Wait> Waits = new Dictionary<int, Wait>();
        private static readonly Func<IVolume, VoxelPresence, bool> IsOtherNonAllyFunc = IsOtherNonAlly;
        private static readonly Func<IVolume, VoxelPresence, bool> IsOtherAllyFunc = IsOtherAlly;

        internal static int Passed;
        internal static int GaveUp;

        // Protected members of the locomotion state classes, reached through delegates built once.
        private static FieldInfo _locomotion;
        private static Func<InPlace, Animator> _animator;
        private static Func<InPlace, TurnOnSpot> _turn;
        private static Func<InPlace, WalkNode, bool> _onSpot;
        private static Func<InPlace, WalkNode, float, bool> _towards;
        private static Func<InPlace, bool> _dynObstacle;
        private static Action<InPlace> _climb;
        private static Action<InPlace> _step;
        private static bool _ready, _failed;

        private static bool Init()
        {
            if (_ready) return true;
            if (_failed) return false;
            try
            {
                _locomotion = AccessTools.Field(typeof(InPlace), "Locomotion");
                _animator = (Func<InPlace, Animator>)Delegate.CreateDelegate(typeof(Func<InPlace, Animator>), AccessTools.PropertyGetter(typeof(InPlace), "animator"));
                _turn = (Func<InPlace, TurnOnSpot>)Delegate.CreateDelegate(typeof(Func<InPlace, TurnOnSpot>), AccessTools.PropertyGetter(typeof(InPlace), "TurnOnSpot"));
                _onSpot = (Func<InPlace, WalkNode, bool>)Delegate.CreateDelegate(typeof(Func<InPlace, WalkNode, bool>), AccessTools.Method(typeof(InPlace), "IsOnSpot", new[] { typeof(WalkNode) }));
                _towards = (Func<InPlace, WalkNode, float, bool>)Delegate.CreateDelegate(typeof(Func<InPlace, WalkNode, float, bool>), AccessTools.Method(typeof(InPlace), "IsTowards", new[] { typeof(WalkNode), typeof(float) }));
                _dynObstacle = (Func<InPlace, bool>)Delegate.CreateDelegate(typeof(Func<InPlace, bool>), AccessTools.Method(typeof(InPlace), "IsBlockedByDynamicObstacle", new Type[0]));
                _climb = (Action<InPlace>)Delegate.CreateDelegate(typeof(Action<InPlace>), AccessTools.Method(typeof(InPlace), "DoClimbUp"));
                _step = (Action<InPlace>)Delegate.CreateDelegate(typeof(Action<InPlace>), AccessTools.Method(typeof(InPlace), "DoStepDown"));
                _ready = _locomotion != null;
            }
            catch (Exception e) { BtcLog.Error("Hook", "ClimbPastAllies init: " + e.Message); }
            if (!_ready) _failed = true;
            return _ready;
        }

        private static bool Prefix(InPlace __instance, bool __runOriginal)
        {
            if (!__runOriginal) return false;
            try
            {
                if (!BtcQol.AlliesPass || !Init()) return true;
                Locomotion4 locomotion = (Locomotion4)_locomotion.GetValue(__instance);
                if (locomotion == null) return true;
                DestinationIterator path = locomotion.PathIterator;
                if (path == null || !path.HasCurrent || _animator(__instance).IsInTransition(0)) return true;
                WalkNode current = path.Current;
                if (current == null || _onSpot(__instance, current) || current.MustFail() || !(current.MustClimbUp() || current.MustStepDown())) return true;
                VoxelPresence self = locomotion.presence;
                XYZ entering = locomotion.PositionEntering;
                if (self == null || entering == XYZ.zero || entering != current.Voxel) return true;
                // Nobody there (the game climbs), or someone the game must judge: an enemy, a door or other obstacle.
                if (!VolumeRegistry.HasVolumesWithPropertyAt(entering, VProperty.character, IsOtherAllyFunc, self)
                    || VolumeRegistry.HasVolumesWithPropertyAt(entering, VProperty.character, IsOtherNonAllyFunc, self)
                    || _dynObstacle(__instance)) return true;

                Labor labor = locomotion.Labor;
                bool mayPass = labor != null && labor.Profession != null && labor.Profession.CanPassThrough();
                if (!mayPass)
                {
                    int id = locomotion.GetInstanceID();
                    Wait wait;
                    if (!Waits.TryGetValue(id, out wait))
                    {
                        if (Waits.Count > 4000) Waits.Clear();
                        wait = new Wait();
                        Waits[id] = wait;
                    }
                    float now = Time.time;
                    if (wait.Voxel != entering || now - wait.Last > 0.5f)
                    {
                        wait.Voxel = entering;
                        wait.Since = now;
                    }
                    wait.Last = now;
                    float patience = BtcAllyPass.IsChoke(self, entering) ? ChokePatienceSeconds : PatienceSeconds;
                    if (now - wait.Since < patience) return false;
                    Waits.Remove(id);
                    GaveUp++;
                    locomotion.PathBlockedAt(current.Position);
                    return false;
                }
                if (!_towards(__instance, current, 4f))
                {
                    _turn(__instance).TurnToDestinationPosition(current);
                    return false;
                }
                if (current.MustClimbUp())
                {
                    if (!locomotion.CanClimb) return true;
                    _climb(__instance);
                }
                else _step(__instance);
                Passed++;
                return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "InPlace.Update ally climb: " + e.Message);
                return true;
            }
        }

        private static bool IsOtherAlly(IVolume volume, VoxelPresence self)
        {
            VoxelPresence other = volume != null ? volume.GetComponent<VoxelPresence>() : null;
            return other != null && other != self && other.affiliation != null && other.affiliation.IsAllied(self);
        }

        private static bool IsOtherNonAlly(IVolume volume, VoxelPresence self)
        {
            VoxelPresence other = volume != null ? volume.GetComponent<VoxelPresence>() : null;
            return other != null && other != self && (other.affiliation == null || !other.affiliation.IsAllied(self));
        }
    }
}
