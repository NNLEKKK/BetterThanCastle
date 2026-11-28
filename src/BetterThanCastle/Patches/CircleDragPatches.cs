using System;
using System.Collections.Generic;
using Brix.External.Signals;
using Brix.Input;
using Brix.UI;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// Vanilla bug fix (task radius circle could not be dragged, stayed at the default radius, pool error in the log).
    /// CircleDrag.OnRequested spawns a new radius drag handle and zone highlight every time the pool hands the circle out, but the
    /// pool releases only the root object: the old handle and highlight stay as children, are never given back, and the DragHandle
    /// pool counts them as in use forever. After enough task circles were reused (tasks deleted/finished, objects respawned)
    /// the pool hits its instance limit ("Pool request ... could not be completed"), Spawn throws inside OnRequested, the circle gets
    /// no handle and the beacon setup stops before the radius is wired to the server. Here the leftovers of the previous use are
    /// released first, and RadiusChanged gets a fresh signal (its only subscriber is the old handle's scale sync, which would keep
    /// rescaling that handle after another owner takes it from the pool). Local only, nothing on the wire.
    /// </summary>
    [HarmonyPatch(typeof(CircleDrag), "OnRequested")]
    internal static class CircleDragReuseLeakPatch
    {
        private static readonly AccessTools.FieldRef<CircleDrag, DragHandle.Spawner> Flex = AccessTools.FieldRefAccess<CircleDrag, DragHandle.Spawner>("flexHandleSpawner");
        private static readonly AccessTools.FieldRef<CircleDrag, SphereHighlight.Spawner> Zone = AccessTools.FieldRefAccess<CircleDrag, SphereHighlight.Spawner>("wholeZoneHighlight");
        private static readonly AccessTools.FieldRef<SphereHighlight.Spawner, SphereHighlight> ZoneInst = AccessTools.FieldRefAccess<SphereHighlight.Spawner, SphereHighlight>("instance");
        private static readonly HashSet<int> Seen = new HashSet<int>();
        private static int _freed;

        private static void Prefix(CircleDrag __instance)
        {
            try
            {
                if (__instance == null || Seen.Add(__instance.GetInstanceID())) return;   // first hand-out: nothing left from before
                Transform t = __instance.transform;
                bool any = false;
                DragHandle.Spawner fs = Flex(__instance);
                DragHandle h = fs != null ? fs.Instance : null;
                if (h != null && h.transform.IsChildOf(t)) { fs.Release(); any = true; }
                SphereHighlight.Spawner zs = Zone(__instance);
                SphereHighlight z = zs != null ? ZoneInst(zs) : null;
                if (z != null && z.transform.IsChildOf(t)) { zs.Release(); any = true; }
                __instance.RadiusChanged = new Signal<float>();
                if (any && _freed++ < 3) BtcLog.Info("CircleDrag", "reused task circle: released the old radius handle/highlight (vanilla leak)");
            }
            catch (Exception e) { BtcLog.Error("CircleDrag", "reuse: " + e.Message); }
        }
    }
}
