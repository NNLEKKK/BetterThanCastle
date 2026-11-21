using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Game.Network;
using HarmonyLib;
using UnityEngine.Networking;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// Legacy replay hook shared by all UNet*Cmd classes. The old mod put
    /// `if (BtcReplay.Active &amp;&amp; BtcReplay.Intercept(this, "CmdX", args)) return;` at the top of every
    /// server-side [Command] body (user code, not the weaver Call*/InvokeCmd* plumbing).
    /// Local only (wire-neutral): it never sends anything, so it is not gated on BetterConnection
    /// (the legacy code ran it regardless of BC).
    /// </summary>
    internal static class UNetCmdReplay
    {
        /// <summary>Resolves each name to the single declared [Command] method of that name.</summary>
        internal static IEnumerable<MethodBase> Targets(Type type, string[] names)
        {
            List<MethodBase> list = new List<MethodBase>();
            foreach (string n in names)
            {
                MethodInfo found = null;
                int count = 0;
                foreach (MethodInfo m in AccessTools.GetDeclaredMethods(type))
                {
                    if (m.Name != n || !m.IsDefined(typeof(CommandAttribute), false)) continue;
                    found = m;
                    count++;
                }
                if (count != 1)
                {
                    BtcLog.Error("Hook", type.Name + "." + n + ": expected 1 [Command] method, found " + count + " (replay hook skipped)");
                    continue;
                }
                list.Add(found);
            }
            return list;
        }

        /// <returns>false = skip the vanilla command body (legacy: Intercept returned true).</returns>
        internal static bool Run(NetworkBehaviour self, object[] args, MethodBase original)
        {
            try
            {
#if DEV
                BetterThanCastle.Dev.BtcDevEcoTrace.Cmd(original.Name, args);
#endif
                BtcApm.Note(original.Name, args);
                if (BtcReplay.Active && BtcReplay.Intercept(self, original.Name, args)) return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "Replay intercept " + original.Name, e);
            }
            return true;
        }
    }

    [HarmonyPatch]
    internal static class UNetProjectCmdReplayPatch
    {
        private static readonly string[] Names =
        {
            "CmdChangeProject", "CmdAccepterPosition", "CmdAccepterRayon", "CmdTransformLog", "CmdAddMoveToGoal",
            "CmdSetAutonomy", "CmdAddMoveToVolumeGoal", "CmdAddEmergencyRecallGoal", "CmdAddCallToArmsGoal",
            "CmdAddBackToWorkGoal", "CmdGoDetonate", "CmdAddCleanGoal", "CmdAddTreeGoal", "CmdAddNatureHarvestGoal",
            "CmdGoDropAt", "CmdGoStoreInto", "CmdAddSmartTransferGoal", "CmdRemoveAllLabor", "CmdAddBreakVoxelGoal",
            "CmdRepair", "CmdBreakObject", "CmdAttack", "CmdMoveAttack", "CmdArmBearTrap", "CmdPickup",
            "CmdSpawnFireflyNest", "CmdSpawn", "CmdSpawnWithLevel", "CmdSpawnCrossbowTron", "CmdSpawnAlchemist",
            "CmdSpawnGeomancer", "CmdSpawnHalbardier", "CmdInstantiateTunnelUnit", "CmdRemoveTunnelUnit",
            "CmdCraftingAssignWorker", "CmdHarvestToggleResource", "CmdHarvestSetResource", "CmdQuarryToggleResource",
            "CmdTunnelToggleResource", "CmdCraftingRemoveResources", "CmdCraftingAddResources",
            "CmdCraftingRemoveProduction", "CmdCraftingWorkAt", "CmdcraftingQueueRecipe", "CmdcraftingProduce",
            "CmdcraftingDoNothing", "CmdcraftingLoop", "CmdcraftingRemoveAt", "CmdStockpileKeepEmpty",
            "CmdStockpileAcceptAll", "CmdStockpileAcceptOnly"
        };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            return UNetCmdReplay.Targets(typeof(UNetProjectCmd), Names);
        }

        private static bool Prefix(UNetProjectCmd __instance, object[] __args, MethodBase __originalMethod)
        {
            return UNetCmdReplay.Run(__instance, __args, __originalMethod);
        }
    }
}
