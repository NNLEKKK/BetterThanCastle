using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game;
using Brix.Game.Components;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    /// <summary>Shared helper: redirects calls on the LocalFactionVision.FactionsSeeingMe struct to BtcFactionCap.</summary>
    internal static class G2Util
    {
        internal static IEnumerable<CodeInstruction> RedirectSubsetCall(IEnumerable<CodeInstruction> ins, string method, MethodInfo repl, string where)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(ins);
            FieldInfo fld = AccessTools.Field(typeof(LocalFactionVision), "FactionsSeeingMe");
            int hits = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].opcode != OpCodes.Ldflda || !object.Equals(list[i].operand, fld)) continue;
                for (int j = i + 1; j < list.Count && j < i + 10; j++)
                {
                    MethodInfo m = list[j].operand as MethodInfo;
                    if ((list[j].opcode == OpCodes.Call || list[j].opcode == OpCodes.Callvirt) && m != null
                        && m.DeclaringType == fld.FieldType && m.Name == method)
                    {
                        list[i].opcode = OpCodes.Nop; // keeps labels; 'this' (LocalFactionVision) stays on the stack
                        list[i].operand = null;
                        list[j].opcode = OpCodes.Call;
                        list[j].operand = repl;
                        hits++;
                        break;
                    }
                }
            }
            if (hits == 0) BtcLog.Error("Hook", where + ": FactionsSeeingMe." + method + " pattern not found");
            return list;
        }
    }

    [HarmonyPatch(typeof(LocalFactionVision), "OnRequested")]
    internal static class LocalFactionVisionOnRequestedPatch
    {
        // vanilla: LineOfSightRange = ...GameRule...; mod: LineOfSightRange = BtcBalanceExtra.Vision(gameObject, LineOfSightRange)
        private static void Postfix(LocalFactionVision __instance)
        {
            try { __instance.LineOfSightRange = BtcBalanceExtra.Vision(__instance.gameObject, __instance.LineOfSightRange); }
            catch (Exception e) { BtcLog.Error("Hook", "LocalFactionVision.OnRequested: " + e); }
        }

        // replay feed spawns objects without a faction/rules: swallow the NRE only while a replay is active
        private static Exception Finalizer(Exception __exception)
        {
            if (__exception is NullReferenceException && BtcReplay.Active) return null;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(LocalFactionVision), "UpdateHostVisibility")]
    internal static class LocalFactionVisionUpdateHostVisibilityPatch
    {
        // replay POV: vanilla sets renderers by the spectator faction's vision, which fights BtcPov's per-POV fog -> flicker
        private static void Postfix(LocalFactionVision __instance)
        {
            if (!BtcPov.Active) return;
            try { BtcPov.ReapplyHostVisibility(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "LocalFactionVision.UpdateHostVisibility: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(LocalFactionVision), "ResetComponent")]
    internal static class LocalFactionVisionResetComponentPatch
    {
        private static void Prefix(LocalFactionVision __instance)
        {
            try { BtcFactionCap.SeeClear(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "LocalFactionVision.ResetComponent: " + e); }
        }
    }

    [HarmonyPatch(typeof(LocalFactionVision), "IsVisibleForFaction")]
    internal static class LocalFactionVisionIsVisibleForFactionPatch
    {
        // vanilla last line: this.FactionsSeeingMe.Contains(faction.ID)  ->  BtcFactionCap.SeeHas(this, faction.ID)
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            return G2Util.RedirectSubsetCall(ins, "Contains",
                AccessTools.Method(typeof(BtcFactionCap), "SeeHas"), "LocalFactionVision.IsVisibleForFaction");
        }
    }

    [HarmonyPatch(typeof(LocalFactionVision), "GetAlliedVision")]
    internal static class LocalFactionVisionGetAlliedVisionPatch
    {
        private static bool Prefix(LocalFactionVision __instance, Faction otherFaction, ref bool __result)
        {
            try
            {
                List<Faction> allies = otherFaction.AllyFactions;
                __result = false;
                if (allies == null) return false;
                for (int i = 0; i < allies.Count; i++)
                {
                    Faction f = allies[i];
                    if (f != null && BtcFactionCap.SeeHas(__instance, f.ID)) { __result = true; return false; }
                }
                return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "LocalFactionVision.GetAlliedVision: " + e);
                return true;
            }
        }
    }
}
