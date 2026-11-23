using Brix.Game.AI;
using Brix.Pathfinding;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Frame saver (cfg Performance/FrameSavers), after Castle Story Plus (Tricky12321, GPLv3: Pathfinding/SearchOwnVolume.cs).
    // Every slice of a route search takes the searching unit out of its own node's obstacle count via
    // TestPathfinding.VoxelPresences. The game never fills that dictionary, so the lookup is always voxel (0,0,0) and walks the
    // whole column (~512 node lookups per slice) to find nothing. With the dictionary empty the slice skips it.
    [HarmonyPatch(typeof(SearchPathRequest), "RemoveVolumeTemporarily")]
    internal static class FrameSaveSearchOwnVolume
    {
        static bool Prepare() { return BtcFrameSave.On; }

        private static bool Prefix(SearchPathRequest __instance, ref PathNode __result)
        {
            if (__instance.TestPathfinding == null || __instance.TestPathfinding.VoxelPresences.Count > 0) return true;
            __result = PathNode.Empty;
            return false;
        }
    }
}
