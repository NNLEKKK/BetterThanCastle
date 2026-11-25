using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.Game.AI;
using Brix.Game.AI.KnowledgeSpace;
using Brix.Game.AI.Nodes;
using Brix.Game.Components;
using Brix.Game.Components.Volumes;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
    // Rule mov.allies "Pass through allies", reworked after Castle Story Plus (Tricky12321, GPLv3: PassThroughAllies +
    // CrowdPathing). Allies never block each other's route search (enemies still do), so a crowd no longer fails its paths
    // and stands AFK; but the walk itself is vanilla: a soldier does not step INTO an ally's voxel. The old version let units
    // walk into each other, which packed melee units into one voxel: they unstacked step by step forever (SingleStep loops,
    // lab grouplag) and could not take attack slots. When a unit finds its walk blocked by an ally, that voxel is closed to
    // the unit's own searches for a few seconds, so its repath walks round the ally instead of into it again.
    // CSP 0.10 additions: where the closed voxel is the only way (a stair one voxel wide) the search that fails after it was
    // refused a closed voxel reopens the unit's closed voxels and searches again; those voxels count as the only way (chokes)
    // for 20 s and are not closed again meanwhile (AllyClosures, CrowdPathingSearchAgain; see also BtcClimbPastAllies).
    internal static class BtcAllyPass
    {
        internal const float ClosedSeconds = 5f;
        internal const float ChokeSeconds = 20f;

        /// <summary>Ally voxels closed to one unit's route searches plus voxels found to be the only way (chokes).</summary>
        internal sealed class Closure
        {
            private const int Max = 4;
            private readonly XYZ[] _closed = new XYZ[Max];
            private readonly float[] _closedUntil = new float[Max];
            private readonly bool[] _closedUsed = new bool[Max];
            private readonly XYZ[] _chokes = new XYZ[Max];
            private readonly float[] _chokeUntil = new float[Max];
            private readonly bool[] _chokeUsed = new bool[Max];
            public int Count;
            /// <summary>Times a search was refused a closed voxel since the search started.</summary>
            public int Refused;
            public readonly List<Func<XYZ, bool>> Inner = new List<Func<XYZ, bool>>(3);
            public readonly List<Func<XYZ, bool>> Wrapped = new List<Func<XYZ, bool>>(3);

            private static int Slot(XYZ[] keys, float[] until, bool[] used, XYZ key)
            {
                int free = -1, first = 0;
                for (int i = 0; i < keys.Length; i++)
                {
                    if (used[i] && keys[i] == key) return i;
                    if (!used[i]) { if (free < 0) free = i; }
                    else if (until[i] < until[first]) first = i;
                }
                return free >= 0 ? free : first;
            }

            /// <summary>Closes a voxel until <paramref name="until"/>; a choke stays open (false).</summary>
            public bool Close(XYZ voxel, float now, float until)
            {
                if (IsChoke(voxel, now)) return false;
                int slot = Slot(_closed, _closedUntil, _closedUsed, voxel);
                if (!_closedUsed[slot]) { _closedUsed[slot] = true; Count++; }
                _closed[slot] = voxel;
                _closedUntil[slot] = until;
                return true;
            }

            public bool IsChoke(XYZ voxel, float now)
            {
                for (int i = 0; i < Max; i++)
                {
                    if (!_chokeUsed[i] || _chokes[i] != voxel) continue;
                    if (now < _chokeUntil[i]) return true;
                    _chokeUsed[i] = false;
                }
                return false;
            }

            private bool Refuses(XYZ p, float now)
            {
                for (int i = 0; i < Max; i++)
                {
                    if (!_closedUsed[i] || _closed[i] != p) continue;
                    if (now < _closedUntil[i]) { Refused++; return true; }
                    _closedUsed[i] = false;
                    Count--;
                }
                return false;
            }

            /// <summary>The search with closed voxels found no route: open them, remember as chokes. Returns how many were still closed.</summary>
            public int ReopenAsChokes(float now, float seconds)
            {
                int reopened = 0;
                for (int i = 0; i < Max; i++)
                {
                    if (!_closedUsed[i]) continue;
                    if (now < _closedUntil[i])
                    {
                        int slot = Slot(_chokes, _chokeUntil, _chokeUsed, _closed[i]);
                        _chokes[slot] = _closed[i];
                        _chokeUntil[slot] = now + seconds;
                        _chokeUsed[slot] = true;
                        reopened++;
                    }
                    _closedUsed[i] = false;
                }
                Count = 0;
                Refused = 0;
                return reopened;
            }

            public Func<XYZ, bool> Wrap(Func<XYZ, bool> inner)
            {
                for (int i = 0; i < Inner.Count; i++) if (Inner[i] == inner) return Wrapped[i];
                Func<XYZ, bool> w = p => (Count == 0 || !BtcQol.AlliesPass || !Refuses(p, Time.time)) && inner(p);
                Inner.Add(inner);
                Wrapped.Add(w);
                return w;
            }
        }

        private static readonly Dictionary<int, Closure> Closures = new Dictionary<int, Closure>();
        private static readonly Func<IVolume, VoxelPresence, bool> BlocksFunc = Blocks;
        internal static int Closed;
        internal static int KeptOpen;
        internal static int Reopened;
        /// <summary>&gt;0 while a route search runs on this (main) thread.</summary>
        [ThreadStatic] internal static int SearchDepth;

        internal static Closure Of(VoxelPresence p)
        {
            Closure c;
            int id = p.GetInstanceID();
            if (!Closures.TryGetValue(id, out c))
            {
                if (Closures.Count > 4000) Closures.Clear();
                c = new Closure();
                Closures[id] = c;
            }
            return c;
        }

        /// <summary>A unit on a targeted attack order (focus) keeps vanilla pathing: allies block its searches and walk, so the group queues for slots round the SAME target and nobody drops the order after a failed walk.</summary>
        internal static bool Focused(Component c)
        {
            Labor labor = c != null ? c.GetComponent<Labor>() : null;
            AttackInfo a = labor != null ? labor.DebugAttack : null;
            return a != null && a.Active && a.AttackDestination != AttackInfo.AttackDestinationBehaviour.DontPrioritizeDestination && a.DestinationTarget != null;
        }

        internal static Closure Find(VoxelPresence p)
        {
            Closure c;
            return p != null && Closures.TryGetValue(p.GetInstanceID(), out c) ? c : null;
        }

        /// <summary>Whether the voxel was found to be the unit's only way a moment ago.</summary>
        internal static bool IsChoke(VoxelPresence p, XYZ voxel)
        {
            Closure c = Find(p);
            return c != null && c.IsChoke(voxel, Time.time);
        }

        /// <summary>Locomotion4.PathBlockedAt: the unit's walk was stopped; close the ally's voxel for its next searches.</summary>
        internal static void Blocked(Locomotion4 l, Vector3 position)
        {
            if (!BtcQol.AlliesPass) return;
            VoxelPresence presence = l.presence;
            if (presence == null || l.Labor == null || l.Labor.Profession == null || l.Labor.Profession.CanPassThrough() || Focused(presence)) return;
            XYZ occupied = l.PositionOccupied;
            XYZ voxel = l.PositionEntering;
            if (!IsBlocking(voxel, occupied, presence))
            {
                voxel = XYZ.FromVector3(position);
                if (!IsBlocking(voxel, occupied, presence)) return;
            }
            if (!Of(presence).Close(voxel, Time.time, Time.time + ClosedSeconds)) { KeptOpen++; return; }
            Closed++;
        }

        private static bool IsBlocking(XYZ voxel, XYZ occupied, VoxelPresence self)
        {
            return voxel != XYZ.zero && voxel != occupied && VolumeRegistry.HasVolumesWithPropertyAt(voxel, VProperty.character, BlocksFunc, self);
        }

        private static bool Blocks(IVolume volume, VoxelPresence self)
        {
            VoxelPresence other = volume != null ? volume.GetComponent<VoxelPresence>() : null;
            return other != null && other != self && other.affiliation != null && other.affiliation.IsAllied(self);
        }
    }
}

namespace BetterThanCastle.Patches
{
    /// <summary>Allies never block a route search (rule mov.allies). Only inside route searches: the same test also decides unstacking,
    /// combat steps and dig spots, which must keep seeing allies (else a whole squad strikes or digs from one block).</summary>
    [HarmonyPatch(typeof(VoxelPresence), "PreventsMovement")]
    internal static class VoxelPresencePreventsMovementPatch
    {
        private static bool Prefix(VoxelPresence __instance, VoxelPresence presence, ref bool __result)
        {
            if (BtcAllyPass.SearchDepth <= 0 || presence == null || presence == __instance || presence.affiliation == null || !BtcQol.AlliesPass || !presence.affiliation.IsAllied(__instance)) return true;
            __result = false;
            return false;
        }
    }

    /// <summary>The workers' reachability map ignores bricktrons, like the game's other reachability map (rule mov.allies).</summary>
    [HarmonyPatch(typeof(KnowledgeConnectedAccessFinder.BranchEvaluator), "PresenceTest")]
    internal static class KnowledgeConnectedAccessPresencePatch
    {
        private static bool Prefix(XYZ p, ref bool __result)
        {
            if (!BtcQol.AlliesPass) return true;
            __result = KnowledgeDisjointRegionsFinder.BranchEvaluator.PresenceTest(p);
            return false;
        }
    }

    [HarmonyPatch(typeof(VoxelPresence), "BuildPresenceTest")]
    internal static class VoxelPresenceBuildPresenceTestPatch
    {
        private static void Postfix(VoxelPresence __instance, ref Func<XYZ, bool> __result)
        {
            try { if (__result != null && BtcQol.AlliesPass) __result = BtcAllyPass.Of(__instance).Wrap(__result); }
            catch (Exception e) { BtcLog.Error("Hook", "VoxelPresence.BuildPresenceTest: " + e.Message); }
        }
    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(SearchPathRequest), "Work")]
    internal static class SearchPathRequestWorkScopePatch
    {
        private static void Prefix(SearchPathRequest __instance, out bool __state) { __state = !BtcAllyPass.Focused(__instance.Presence); if (__state) BtcAllyPass.SearchDepth++; }
        private static void Finalizer(bool __state) { if (__state) BtcAllyPass.SearchDepth--; }
    }

    [HarmonyPatch(typeof(SearchPathRequest), "LoopImprovePath")]
    internal static class SearchPathRequestImproveScopePatch
    {
        private static void Prefix(SearchPathRequest __instance, out bool __state) { __state = !BtcAllyPass.Focused(__instance.Presence); if (__state) BtcAllyPass.SearchDepth++; }
        private static void Finalizer(bool __state) { if (__state) BtcAllyPass.SearchDepth--; }
    }

    [HarmonyPatch(typeof(DestinationRepathSearch), "Search")]
    internal static class DestinationRepathSearchScopePatch
    {
        private static void Prefix(GameObject ____gameObject, out bool __state) { __state = !(____gameObject != null && BtcAllyPass.Focused(____gameObject.transform)); if (__state) BtcAllyPass.SearchDepth++; }
        private static void Finalizer(bool __state) { if (__state) BtcAllyPass.SearchDepth--; }
    }

    /// <summary>A search starts counting the closed voxels it is refused afresh (CSP CrowdPathingSearchStart).</summary>
    [HarmonyPatch(typeof(SearchPathRequest), "InitSearch")]
    internal static class SearchPathRequestInitSearchAllyPatch
    {
        private static void Postfix(SearchPathRequest __instance)
        {
            try
            {
                if (!BtcQol.AlliesPass || __instance.Presence == null) return;
                BtcAllyPass.Closure c = BtcAllyPass.Find(__instance.Presence);
                if (c != null) c.Refused = 0;
            }
            catch (Exception e) { BtcLog.Error("Hook", "SearchPathRequest.InitSearch: " + e.Message); }
        }
    }

    /// <summary>A search to a place that found no route after it was refused a closed ally voxel: the closed voxels were the only
    /// way. Reopen them (chokes for 20 s, not closed again) and restart the search in the same request (CSP CrowdPathingSearchAgain).</summary>
    [HarmonyPatch(typeof(SearchPathRequest), "Work")]
    internal static class SearchPathRequestWorkAgainPatch
    {
        private static Action<SearchPathRequest> _init;

        private static void Postfix(SearchPathRequest __instance, ref bool __result)
        {
            try
            {
                if (!__result || !__instance.Done || __instance.SearchAway || __instance.Presence == null || __instance.Search == null || !BtcQol.AlliesPass) return;
                SearchPathRequest.ResultAccepter accepter = __instance.Accepter as SearchPathRequest.ResultAccepter;
                if (accepter == null || accepter.Result != null) return;
                BtcAllyPass.Closure c = BtcAllyPass.Find(__instance.Presence);
                if (c == null || c.Refused == 0) return;
                if (c.ReopenAsChokes(Time.time, BtcAllyPass.ChokeSeconds) == 0) return;
                BtcAllyPass.Reopened++;
                if (_init == null)
                    _init = (Action<SearchPathRequest>)Delegate.CreateDelegate(typeof(Action<SearchPathRequest>), AccessTools.Method(typeof(SearchPathRequest), "InitSearch"));
                _init(__instance);
                __result = false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "SearchPathRequest.Work again: " + e.Message); }
        }
    }

    /// <summary>Chilling workers pass allies like workers at work (CSP ChillingPassAllies), rule mov.allies.</summary>
    [HarmonyPatch(typeof(Builder), "CanPassThrough")]
    internal static class BuilderChillingPassAlliesPatch
    {
        private static void Postfix(Builder __instance, Labor ___labor, ref bool __result)
        {
            if (__result || ___labor == null || ___labor.Activity != Activity.Chilling || !BtcQol.AlliesPass) return;
            __result = __instance.IsWorker() || ___labor.hackedDigGoal.IsNullOrReleased();
        }
    }
}
