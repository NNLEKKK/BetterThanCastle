using Smooth.Slinq;
using Brix.Utils.Locomotion;
using Brix.Utils;
using Brix.Pathfinding.Test;
using Brix.Pathfinding;
using Brix.Game.AI.Pathfinding;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Engine;
using Brix.Game.AI;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    // Vanilla GetRepathDestination: GetNth(CurrentIndex + 3) -> + BtcQol.RepathAhead
    [HarmonyPatch(typeof(DestinationResolver), "GetRepathDestination")]
    internal static class DestinationResolverGetRepathDestinationPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            FieldInfo f = AccessTools.Field(typeof(BtcQol), "RepathAhead");
            for (int i = 0; i + 1 < list.Count; i++)
            {
                if (list[i].opcode == OpCodes.Ldc_I4_3 && list[i + 1].opcode == OpCodes.Add)
                {
                    var repl = new CodeInstruction(OpCodes.Ldsfld, f);
                    repl.MoveLabelsFrom(list[i]);
                    list[i] = repl;
                    return list;
                }
            }
            BtcLog.Error("Hook", "DestinationResolver.GetRepathDestination: + 3 not found");
            return list;
        }
    }

    // Legacy: RepathMoveNext tries several lookahead distances (BtcQol.AheadFar / AheadVanilla).
    [HarmonyPatch(typeof(DestinationResolver), "RepathMoveNext", new Type[] { typeof(bool), typeof(bool) })]
    internal static class DestinationResolverRepathMoveNextPatch
    {
        private static bool Prefix(DestinationResolver __instance, bool tryPassThrough, bool canClimb, ref bool __result)
        {
            try
            {
                DestinationResolver self = __instance;
                int[] ahead = BtcQol.RepathOn ? BtcQol.AheadFar : BtcQol.AheadVanilla;
                XYZ prev = XYZ.zero;
                bool ok = false;
                try
                {
                    for (int i = 0; i < ahead.Length; i++)
                    {
                        BtcQol.RepathAhead = ahead[i];
                        XYZ dest = self.GetRepathDestination(tryPassThrough);
                        if (i > 0 && (dest == prev || dest == XYZ.zero)) break;
                        prev = dest;
                        DestinationRepathSearch search = ObjectPool<DestinationRepathSearch>.Reserve().Init(self._locomotion, dest, tryPassThrough, canClimb);
                        List<PathBranch> branches = CollectionPool.RequestList<PathBranch>();
                        Dictionary<XYZ, int> positions = CollectionPool.RequestDictionary<XYZ, int>();
                        List<WalkNode> locations = CollectionPool.RequestList<WalkNode>();
                        ok = self.MergePaths(search, branches, positions, locations);
                        ObjectPool<DestinationRepathSearch>.Release(search);
                        branches.Dispose<PathBranch>();
                        positions.Dispose<XYZ, int>();
                        locations.Dispose<WalkNode>();
                        if (ok) break;
                    }
                }
                finally
                {
                    BtcQol.RepathAhead = 3;
                }
                __result = ok;
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "DestinationResolver.RepathMoveNext: " + e); }
            return true;
        }
    }
}
