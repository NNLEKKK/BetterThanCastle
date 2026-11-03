using Brix.Game;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Game.Components;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    internal static class FireflyNestHelpers
    {
        internal static int RespawnBase(int vanilla, FireflyNest nest)
        {
            try { return BtcGameplay.RespawnBase(nest.faction, vanilla); }
            catch (Exception e) { BtcLog.Error("Hook", "FireflyNest.RespawnBase: " + e); return vanilla; }
        }

        internal static int NestCap(int vanilla, FireflyNest nest)
        {
            try { return LogCap(nest.faction, vanilla, BtcHandicap.Cap(nest.faction, vanilla), "spawn"); }
            catch (Exception e) { BtcLog.Error("Hook", "FireflyNest.NestCap: " + e); return vanilla; }
        }

        private static readonly HashSet<int> _capLogged = new HashSet<int>();

        // once per faction: shows the user (and us) that the per-player cap really took effect
        private static int LogCap(Faction f, int vanilla, int eff, string where)
        {
            if (eff != vanilla && f != null && _capLogged.Add(f.GetInstanceID()))
                BtcLog.Info("Handicap", "cap faction=" + f.name + " vanilla=" + vanilla + " effective=" + eff + " (" + where + ")");
            return eff;
        }

        internal static int BreweryCap(int vanilla, object brewery)
        {
            try
            {
                Faction f = Traverse.Create(brewery).Property("faction").GetValue<Faction>();
                return LogCap(f, vanilla, BtcHandicap.Cap(f, vanilla), "brew");
            }
            catch (Exception e) { BtcLog.Error("Hook", "FireflyNest.BreweryCap: " + e); return vanilla; }
        }

        /// <summary>After the first call to targetName insert "ldarg.0; call wrap".</summary>
        internal static IEnumerable<CodeInstruction> WrapAfter(IEnumerable<CodeInstruction> ins, string targetName, MethodInfo wrap, string where)
        {
            CodeMatcher cm = new CodeMatcher(ins);
            cm.MatchForward(false, new CodeMatch(i => (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt)
                && i.operand is MethodInfo m && m.Name == targetName));
            if (cm.IsInvalid)
            {
                BtcLog.Error("Hook", where + ": call " + targetName + " not found");
                return ins;
            }
            cm.Advance(1).Insert(new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Call, wrap));
            return cm.InstructionEnumeration();
        }
    }

    [HarmonyPatch(typeof(FireflyNest), "FeedQueuedFireflies")]
    internal static class FireflyNestFeedQueuedFirefliesPatch
    {
        // vanilla line 440: int num = GetActiveOrSpawnableCharacterCount();  -> BtcGameplay.RespawnBase(faction, ...)
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            return FireflyNestHelpers.WrapAfter(ins, "GetActiveOrSpawnableCharacterCount",
                AccessTools.Method(typeof(FireflyNestHelpers), "RespawnBase"), "FireflyNest.FeedQueuedFireflies");
        }
    }

    [HarmonyPatch(typeof(FireflyNest), "SpawnNamedFireflies")]
    internal static class FireflyNestSpawnNamedFirefliesPatch
    {
        private static bool Prefix(FireflyNest __instance)
        {
            try { return !BtcGameplay.HoldNewSpawns(__instance.faction); }
            catch (Exception e) { BtcLog.Error("Hook", "FireflyNest.SpawnNamedFireflies: " + e); return true; }
        }

        // vanilla line 462: num < GeneralRules.Main.bricktronCap  -> BtcHandicap.Cap(faction, bricktronCap)
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            return FireflyNestHelpers.WrapAfter(ins, "get_bricktronCap",
                AccessTools.Method(typeof(FireflyNestHelpers), "NestCap"), "FireflyNest.SpawnNamedFireflies");
        }
    }

    [HarmonyPatch]
    internal static class FireflyNestBreweryCanBrewPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.Inner(typeof(FireflyNest), "Brewery"), "CanBrew");
        }

        // vanilla line 1040: Nest.GetActiveOrSpawnableCharacterCount() < GeneralRules.Main.bricktronCap
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            return FireflyNestHelpers.WrapAfter(ins, "get_bricktronCap",
                AccessTools.Method(typeof(FireflyNestHelpers), "BreweryCap"), "FireflyNest.Brewery.CanBrew");
        }
    }

    [HarmonyPatch]
    internal static class FireflyNestBreweryProcessFireflyPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.Inner(typeof(FireflyNest), "Brewery"), "ProcessFirefly");
        }

        // vanilla: component.pureEnergy = fireflyFilling -> BtcHandicap.Income(faction, BtcGameplay.Income(fireflyFilling))
        private static void Postfix(object __instance, UnityEngine.GameObject go)
        {
            try
            {
                Traverse t = Traverse.Create(__instance);
                FireflyNest nest = t.Field("Nest").GetValue<FireflyNest>();
                Firefly ff = go.GetComponent<Firefly>();
                if (nest == null || ff == null) return;
                Faction f = (nest.mainRecepteur != null) ? nest.mainRecepteur.faction : nest.faction;
                ff.pureEnergy = BtcHandicap.Income(f, BtcGameplay.Income(nest.fireflyFilling));
            }
            catch (Exception e) { BtcLog.Error("Hook", "FireflyNest.Brewery.ProcessFirefly: " + e); }
        }
    }
}
