using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.Game.AI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Crowd repath cache (cfg Performance/CrowdRepath, host/single player, BetterConnection ON).
    /// What the game does: a unit whose next voxel is taken by another unit re-searches a detour (Locomotion4.RepathMoveNext,
    /// ~1 ms) on every movement update until it gets past. In a crowd (50 workers round the idle spot, a squad in a corridor)
    /// that is ~300 searches a second = the regular 25-50 ms hitches every 0.1-0.5 s (lab idle-units, same with BC OFF).
    /// What the cache does: a search that FAILED is not repeated by the same unit from the same voxel to the same
    /// destination for a short time; the call answers "no detour" at once, exactly what the search found a moment ago,
    /// and the game goes on as after a failed search (waits, passes, or reports the path blocked). Successful searches and
    /// any change of place, next voxel or destination run the real search immediately, so orders react as before.
    /// </summary>
    internal static class BtcCrowdRepath
    {
        // lab idle-units: 0.25 s skipped 22% of the calls (a blocked unit retries every ~0.15-0.3 s), 0.6 s is still short next to a walk step
        private const float Hold = 0.6f;

        private struct Miss { public XYZ Occ, Ent, Dst; public float Until; }

        private static readonly Dictionary<int, Miss> Misses = new Dictionary<int, Miss>();
        private static bool? _cfg;
        internal static int Calls, Failed, Skipped, MisT, MisO, MisE, MisD;
        private static float _logAt;

        private static bool Cfg { get { if (!_cfg.HasValue) _cfg = BtcConfig.CrowdRepath == null || BtcConfig.CrowdRepath.Value; return _cfg.Value; } }

        internal static bool Active { get { return Cfg && BtcNetworkGate.Enabled && (NetworkServer.active || !NetworkClient.active); } }

        private static XYZ Dest(Locomotion4 l)
        {
            WalkNode d = l.PathIterator != null ? l.PathIterator.Destination : null;
            return d != null ? XYZ.FromVector3(d.Position) : XYZ.zero;
        }

        private static bool Idle(Locomotion4 l)
        {
            Labor b = l.Labor;
            return b != null && (b.Activity == Activity.Chilling || b.Activity == Activity.Idle);
        }

        /// <summary>Prefix: true = skip the search, the answer is false.</summary>
        internal static bool SkipSearch(Locomotion4 l)
        {
            Calls++;
            Miss m;
            if (!Misses.TryGetValue(l.GetInstanceID(), out m)) return false;
            if (Time.time >= m.Until) { MisT++; return false; }
            if (m.Occ != l.PositionOccupied) { MisO++; return false; }
            // the next voxel is NOT part of the key: after a failed search the unit turns to another neighbour (lab: 99% of the
            // repeats), but the detour search starts from the same voxel to the same goal through the same crowd
            if (m.Ent != l.PositionEntering) MisE++;
            // idle / chilling workers in a crowd pick a new spot after every failed search (lab: the goal changed in 96% of the
            // repeats): for them the voxel alone is the key, they stand still for a moment instead of searching every update.
            // Units on an order or a task keep the goal in the key, a new goal always gets a real search.
            if (m.Dst != Dest(l) && !Idle(l)) { MisD++; return false; }
            Skipped++;
            return true;
        }

        internal static void Searched(Locomotion4 l, bool ok)
        {
            int id = l.GetInstanceID();
            if (ok) { Misses.Remove(id); }
            else
            {
                Failed++;
                if (Misses.Count > 2000) Misses.Clear();
                Misses[id] = new Miss { Occ = l.PositionOccupied, Ent = l.PositionEntering, Dst = Dest(l), Until = Time.time + Hold };
            }
            if (Time.unscaledTime >= _logAt)
            {
#if DEV
                if (_logAt > 0f && Calls > 0) BtcLog.Info("CrowdRepath", "60 s: calls " + Calls + " failed " + Failed + " skipped " + Skipped + " after a fail: expired " + MisT + " moved " + MisO + " (next changed " + MisE + ") goal " + MisD);
#endif
                _logAt = Time.unscaledTime + 60f;
                Calls = 0; Failed = 0; Skipped = 0; MisT = 0; MisO = 0; MisE = 0; MisD = 0;
            }
        }
    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(Locomotion4), "RepathMoveNext")]
    internal static class CrowdRepathPatch
    {
        private static bool Prefix(Locomotion4 __instance, ref bool __result, out bool __state)
        {
            __state = false;
            try
            {
                if (!BtcCrowdRepath.Active) return true;
                __state = true;
                if (!BtcCrowdRepath.SkipSearch(__instance)) return true;
                __state = false;
                __result = false;
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "CrowdRepath prefix: " + e.Message); return true; }
        }

        private static void Postfix(Locomotion4 __instance, bool __result, bool __state)
        {
            if (!__state) return;
            try { BtcCrowdRepath.Searched(__instance, __result); }
            catch (Exception e) { BtcLog.Error("Hook", "CrowdRepath postfix: " + e.Message); }
        }
    }
}
